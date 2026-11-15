using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 9. oturum (TECH_AUDIT KN-1): kaydın "her zaman doğru olması gereken" kuralları tek yerde.
///
/// Neden: aynı bilginin kayıtta iki kopyası olduğunda (ör. tür seviyesi ↔ bina seviyeleri, kilit bayrağı ↔ kilit kuralı) kopyalar
/// zamanla ayrışıyor ve hata ancak oyuncu fark edince görülüyordu. <see cref="Repair"/> türetilmiş kopyaları her yüklemede kuraldan
/// yeniden üretir; <see cref="Check"/> kalan tutarsızlıkları listeler (geliştirici derlemesinde uyarı, testlerde hata).
///
/// Yeni türetilmiş alan eklerken: kaynağı burada tanımla, <see cref="Repair"/>'e yeniden üretimi, <see cref="Check"/>'e denetimi ekle.
/// Onarım yalnız oyuncuya zarar vermeyen yönde çalışır (kilit açar, kilitlemez; seviye uydurmaz).
/// </summary>
public static class SaveInvariants
{
    /// <summary>Türetilmiş kopyaları kuraldan yeniden üretir. Dönüş: veri değişti mi.</summary>
    public static bool Repair(PlayerSaveData data)
    {
        if (data?.buildings == null) return false;
        bool changed = false;

        // 1) Tür seviyesi (buildings[].level) = o türün tamamlanmış en yüksek bina seviyesi. Hiç tamamlanmış binası olmayan türde
        //    (ya da binaları henüz yerleştirilmemiş eski kayıtta) kayıtlı değer korunur.
        foreach (var entry in data.buildings)
        {
            if (entry == null) continue;
            int best = BuildingLevels.HighestCompleted(data, entry.buildingType);
            if (best > 0 && entry.level != best)
            {
                entry.level = best;
                changed = true;
            }
        }

        // 2) Kilit = kural. Eskiden yalnız Ana Üs yükselirken hesaplanıyordu: açılış seviyesi bir güncellemede düşürülürse o seviyeyi
        //    geçmiş oyuncu binayı göremiyordu. Yalnız AÇAR (oyuncudan bir şey geri alınmaz).
        int mainBase = MainBaseLevel(data);
        foreach (var entry in data.buildings)
        {
            if (entry == null || entry.isUnlocked) continue;
            if (mainBase >= Progression.FirstUnlockLevel(entry.buildingType))
            {
                entry.isUnlocked = true;
                entry.level = Mathf.Max(1, entry.level);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>Kayıttaki Ana Üs seviyesi (tür girdisi; yoksa 1).</summary>
    public static int MainBaseLevel(PlayerSaveData data)
    {
        var main = data?.buildings?.Find(entry => entry != null && entry.buildingType == BuildingType.MainBase);
        return main != null ? Mathf.Max(1, main.level) : 1;
    }

    /// <summary>Tutarsızlıkların listesi (boş = sağlam). Veriyi değiştirmez.</summary>
    public static List<string> Check(PlayerSaveData data)
    {
        var problems = new List<string>();
        if (data == null) { problems.Add("kayıt yok"); return problems; }

        // --- Binalar ---
        if (data.buildings == null || !data.buildings.Exists(entry => entry != null && entry.buildingType == BuildingType.MainBase))
            problems.Add("Ana Üs tür girdisi yok");
        int mainBase = MainBaseLevel(data);
        if (data.buildings != null)
        {
            var seenTypes = new HashSet<BuildingType>();
            foreach (var entry in data.buildings)
            {
                if (entry == null) { problems.Add("boş bina türü girdisi"); continue; }
                if (!seenTypes.Add(entry.buildingType)) problems.Add("bina türü iki kez kayıtlı: " + entry.buildingType);
                int best = BuildingLevels.HighestCompleted(data, entry.buildingType);
                if (best > 0 && entry.level != best)
                    problems.Add($"tür seviyesi ayrışmış: {entry.buildingType} kayıt {entry.level}, binalar {best}");
                if (!entry.isUnlocked && mainBase >= Progression.FirstUnlockLevel(entry.buildingType))
                    problems.Add($"kilit kuralla uyuşmuyor: {entry.buildingType} kilitli, Ana Üs {mainBase}");
                if (entry.level < 0 || entry.level > Progression.MaxLevel)
                    problems.Add($"tür seviyesi sınır dışı: {entry.buildingType} {entry.level}");
            }
        }
        if (data.placedBuildings != null)
        {
            var seenSlots = new HashSet<string>();
            foreach (var placed in data.placedBuildings)
            {
                if (placed == null) { problems.Add("boş bina kaydı"); continue; }
                if (!string.IsNullOrEmpty(placed.slotId) && !seenSlots.Add(placed.slotId))
                    problems.Add("aynı bina kimliği iki kez: " + placed.slotId);
                if (placed.level < 1 || placed.level > Progression.MaxLevel)
                    problems.Add($"bina seviyesi sınır dışı: {placed.slotId} {placed.level}");
            }
        }

        // --- Kaynaklar ve karakterler ---
        if (data.gold < 0) problems.Add("altın eksi: " + data.gold);
        if (data.goldMineStoredGold < 0) problems.Add("maden deposu eksi: " + data.goldMineStoredGold);
        if (data.gems < 0 || data.gems > GemEconomy.MaxGems) problems.Add("elmas sınır dışı: " + data.gems);
        if (data.lives < 0) problems.Add("can eksi: " + data.lives);
        if (data.hero == null) problems.Add("kahraman verisi yok");
        else if (data.hero.level < 1 || data.hero.level > Progression.MaxLevel) problems.Add("kahraman seviyesi sınır dışı: " + data.hero.level);
        if (data.apprenticeLevel < 1 || data.apprenticeLevel > Progression.MaxLevel) problems.Add("çırak seviyesi sınır dışı: " + data.apprenticeLevel);
        if (data.builderCount < 1 || data.builderCount > Progression.MaxBuilders) problems.Add("inşaatçı sayısı sınır dışı: " + data.builderCount);

        // --- Görevler ---
        if (data.builderTasks != null)
            foreach (var task in data.builderTasks)
            {
                if (task == null) { problems.Add("boş inşaatçı görevi"); continue; }
                if (task.endTicks < task.startTicks) problems.Add($"görev bitişi başlangıcından önce: {task.kind} {task.slotId}");
            }

        // --- Ada ---
        var island = data.island;
        if (island == null) problems.Add("ada verisi yok");
        else
        {
            if (island.meat < 0 || island.wood < 0) problems.Add($"ada kaynağı eksi: Et {island.meat}, Odun {island.wood}");
            if (island.keepLevel < 1 || island.keepLevel > Progression.MaxLevel) problems.Add("Kıyı Kalesi seviyesi sınır dışı: " + island.keepLevel);
            if (island.placed != null)
            {
                var seenIds = new HashSet<string>();
                foreach (var building in island.placed)
                {
                    if (building == null) { problems.Add("boş ada binası"); continue; }
                    if (!string.IsNullOrEmpty(building.id) && !seenIds.Add(building.id)) problems.Add("aynı ada binası iki kez: " + building.id);
                }
            }
        }
        return problems;
    }
}
