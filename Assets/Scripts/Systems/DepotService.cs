using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D121 (grup 9, bug 11): bölgeye özgü sahne/kayıt işlemleri. Ana Üs (<see cref="BaseDepotAdapter"/>) ve Ada (<see cref="IslandDepotAdapter"/>)
/// aynı <see cref="DepotService"/> yolunu kullanır; adaptör yalnız "bul, sahneden düşür, kur, kaydet" yapar, depo listesine dokunmaz.
/// </summary>
public interface IDepotRegion
{
    int Region { get; }
    /// <summary>Yerleşik nesneden depo öğesi taslağı (kimlik, tür, seviye; adada stok/işçi). Yoksa null.</summary>
    DepotItem Snapshot(string id, int category);
    /// <summary>Nesneyi sahneden ve yerleşik kayıt listesinden kaldırır (diske YAZMAZ).</summary>
    bool Despawn(DepotItem item);
    /// <summary>Öğeyi verilen yere kurar ve yerleşik kayıt listesine yazar (diske YAZMAZ). Kurulamazsa false (hiçbir şey değişmez).</summary>
    bool Spawn(DepotItem item, Vector2 at);
    /// <summary>Bölge durumunu yeniler (seviye/üretim/işçi) ve TEK kez diske kaydeder.</summary>
    void Persist();
}

/// <summary>
/// D121: Depo — bina/dekoru araziden kaldırıp sonra ÜCRETSİZ, SÜRESİZ, seviyesi korunarak geri kurmak. Depodaki nesne hiçbir etkiye
/// (savaş, üretim, kapasite, işçi) katılmaz: ayrı listede durduğu için yerleşik listeleri okuyan her sistem onu kendiliğinden görmez.
///
/// Kurallar tek yerde: hariç türler (<see cref="ShowButton"/>), ret sebepleri (<see cref="BusyReason"/>, <see cref="RestoreBlockedReason"/>),
/// sayım (<see cref="OwnedCount"/>: depodakiler bina limitine ve ilk-kurulum fiyat sayımına SAYILIR) ve kayıt sırası
/// (<see cref="Store"/>/<see cref="Restore"/> atomik: tek SaveGame; kurma başarısızsa depo değişmez, çift kopya/kayıp yok).
/// Depoyu (<see cref="PlayerSaveData.depot"/>) yalnız bu sınıf okur/yazar (+ <see cref="SaveInvariants.RepairDepot"/> kayıt onarımı).
/// </summary>
public static class DepotService
{
    public const int RegionBase = 0, RegionIsland = 1;
    public const int CategoryBuilding = 0, CategoryDecor = 1;

    /// <summary>Telemetri (PlaytestBot/IslandStats): bu oturumda depoya alma / depodan kurma sayısı.</summary>
    public static int StoredThisSession { get; private set; }
    public static int RestoredThisSession { get; private set; }
    /// <summary>Depo değişince (arayüz rozetleri/sayaçlar yenilensin).</summary>
    public static event Action Changed;

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    private static List<DepotItem> List
    {
        get
        {
            var data = Data;
            if (data == null) return null;
            if (data.depot == null) data.depot = new List<DepotItem>();
            return data.depot;
        }
    }

    // ==================== SORGULAR ====================

    /// <summary>Bölgedeki depo öğeleri (kopya liste; sıra = depoya giriş sırası).</summary>
    public static List<DepotItem> Items(int region)
    {
        var result = new List<DepotItem>();
        var list = List;
        if (list != null) foreach (var item in list) if (item != null && item.region == region) result.Add(item);
        return result;
    }

    public static DepotItem Find(int region, string id)
    {
        var list = List;
        return list == null || id == null ? null : list.Find(item => item != null && item.region == region && item.id == id);
    }

    /// <summary>Depodaki bu türden bina sayısı (bölge içinde).</summary>
    public static int StoredCount(int region, string type)
    {
        int n = 0;
        var list = List;
        if (list != null)
            foreach (var item in list)
                if (item != null && item.region == region && item.category == CategoryBuilding && item.type == type) n++;
        return n;
    }

    /// <summary>
    /// Sahip olunan bina sayısı = yerleşik + depo. Limit ve ilk-kurulum fiyatı bunu kullanır (depola → yeni kur ile sınırsız bina/ucuz ilk
    /// kurulum açığı kapanır). Ana Üs yerleşik sayısı sahnede harita yöneticisinden, yoksa kayıttan; adada kayıttan.
    /// </summary>
    public static int OwnedCount(int region, string type) => PlacedCount(region, type) + StoredCount(region, type);

    public static int PlacedCount(int region, string type)
    {
        if (region == RegionBase)
        {
            if (BaseMapManager.Instance != null && Enum.TryParse(type, out BuildingType bt)) return BaseMapManager.Instance.GetPlacedCount(bt);
            int n = 0;
            var placed = Data?.placedBuildings;
            if (placed != null) foreach (var p in placed) if (p != null && p.buildingType == type) n++;
            return n;
        }
        int count = 0;
        var island = Data?.island?.placed;
        if (island != null) foreach (var b in island) if (b != null && b.kind == type) count++;
        return count;
    }

    /// <summary>
    /// Ana Üs türü arazide mi: yerleşik kopyası yoksa ve depoda varsa false (ör. Altın Madeni depodayken üretim/kapasite 0).
    /// Hiç kaydı olmayan (eski kayıt/test verisi) tür için true: davranış depo öncesiyle aynı kalır.
    /// </summary>
    public static bool IsPlaced(BuildingType type)
    {
        if (StoredCount(RegionBase, type.ToString()) == 0) return true;
        return PlacedCount(RegionBase, type.ToString()) > 0;
    }

    /// <summary>Depodaki savaş kulesi sayısı (Okçu Kışlası, Topçu Kulesi, Manastır): ORTA zorluk ipucu "1 kule depoda".</summary>
    public static int StoredTowerCount()
    {
        return StoredCount(RegionBase, nameof(BuildingType.ArcherTower)) + StoredCount(RegionBase, nameof(BuildingType.CannonTower))
            + StoredCount(RegionBase, nameof(BuildingType.Monastery));
    }

    /// <summary>Savaş hazırlığı/yenilgi ekranı ipucu; depoda kule yoksa null.</summary>
    public static string StoredTowerHint()
    {
        int n = StoredTowerCount();
        return n <= 0 ? null : L.T($"{n} kule depoda", n == 1 ? "1 tower in storage" : $"{n} towers in storage");
    }

    // ==================== KURALLAR ====================

    /// <summary>
    /// DEPOYA düğmesi bu nesnede hiç gösterilir mi. Hariç: Ana Üs kalesi (inşaatçı kulübeleri bina menüsüne zaten gelmez), adada Kıyı Kalesi
    /// (yerleşik listede yok) ve Okçuluk Alanı (Okçu Kaptan/akın/öğretici ona bağlı). İlk rehber (Ana Üs) / inşa öğreticisi (ada) bitmeden yok.
    /// </summary>
    public static bool ShowButton(int region, int category, string type)
    {
        var data = Data;
        if (data == null || string.IsNullOrEmpty(type)) return false;
        if (region == RegionBase)
            return data.firstBaseGuideDone && (category == CategoryDecor || type != nameof(BuildingType.MainBase));
        if (category != CategoryBuilding || data.island == null || data.island.buildTutorial < 5) return false;
        return type != nameof(IslandLayout.BuildingKind.Castle) && type != nameof(IslandLayout.BuildingKind.Archery);
    }

    /// <summary>
    /// Şu an depoya alınamıyorsa tek satır sebep (düğme gri + toast), alınabiliyorsa null. Elmas açığı kapalı: süren inşa/yükseltme
    /// (FinishNow ile birleşince yükseltme seviyesi tür kaydına yazılırdı) reddedilir.
    /// </summary>
    public static string BusyReason(int region, int category, string id)
    {
        var data = Data;
        if (data == null) return L.T("Kayıt yok", "No save");
        if (region == RegionBase)
        {
            if (category == CategoryDecor) return null;
            var placed = data.placedBuildings?.Find(p => p != null && p.slotId == id);
            if (placed == null) return L.T("Bina bulunamadı", "Building not found");
            bool working = BuildingLevels.UnderConstruction(data, id) || BuildingLevels.Upgrading(data, id)
                || (GameManager.Instance != null && GameManager.Instance.TaskForSlot(id) != null);
            if (working) return L.T("İnşaat sürerken depoya alınamaz", "Can't store while building");
            if (placed.buildingType == nameof(BuildingType.CardWorkshop) && data.workshop?.queue != null && data.workshop.queue.Count > 0)
                return L.T("Üretim sürüyor", "Production in progress");
            return null;
        }
        var island = data.island;
        if (IslandEconomy.InDefense) return L.T("Savaşta depoya alınamaz", "Can't store during defense");
        var b = island?.placed?.Find(p => p != null && p.id == id);
        if (b == null) return L.T("Bina bulunamadı", "Building not found");
        if (b.level < 1 || (island.task != null && island.task.Active && island.task.buildingId == id))
            return L.T("İnşaat sürerken depoya alınamaz", "Can't store while building");
        if (IslandDamaged(island, id)) return L.T("Önce onar", "Repair it first");
        return null;
    }

    private static bool IslandDamaged(IslandSaveData island, string id)
    {
        if (island?.buildings != null)
            foreach (var e in island.buildings)
                if (e != null && e.id == id && (e.hpFraction < 1f || e.repairEndTicks > IslandRepair.Now)) return true;
        foreach (var live in IslandBuilding.All)
            if (live != null && live.Id == id && live.UnderRepair) return true;
        return false;
    }

    /// <summary>
    /// Depodan kurma şu an yapılamıyorsa sebep. Ek koşul (a): seviye o anki tavanı (Ana Üs: tür/Ana Üs tavanı, Ada: Kıyı Kalesi tavanı)
    /// aşıyorsa kırpılmaz, kurma reddedilir (seviye kaybı yok). Adada savunma sürerken kurulamaz.
    /// </summary>
    public static string RestoreBlockedReason(DepotItem item)
    {
        if (item == null) return L.T("Depoda yok", "Not in storage");
        if (item.region == RegionBase)
        {
            if (item.category == CategoryDecor) return null;
            var gm = GameManager.Instance;
            if (gm == null || !Enum.TryParse(item.type, false, out BuildingType type)) return L.T("Bilinmeyen bina", "Unknown building");
            if (!gm.Buildings.TryGetValue(type, out var bd)) return L.T("Bilinmeyen bina", "Unknown building");
            if (!ShowButton(RegionBase, item.category, item.type)) return L.T("Bu yapı depodan kurulamaz", "This can't be placed from storage");
            int limit = bd.MaxCountAtBaseLevel(gm.GetMainBaseLevel());
            if (PlacedCount(RegionBase, item.type) >= limit)
                return L.T($"Bu türden en fazla {limit} bina kurulabilir", $"At most {limit} of this building allowed");
            int cap = bd.GetMaxLevel(gm.GetMainBaseLevel());
            if (item.level > cap)
                return L.T($"Seviye tavanı aşılıyor (Sev. {item.level} > {cap})", $"Level cap exceeded (Lv. {item.level} > {cap})");
            return null;
        }
        if (IslandEconomy.InDefense) return L.T("Savaşta kurulamaz", "Can't place during defense");
        if (item.type == nameof(IslandLayout.BuildingKind.Castle) || item.type == nameof(IslandLayout.BuildingKind.Archery))
            return L.T("Bu yapı depodan kurulamaz", "This can't be placed from storage");
        int islandCap = Progression.Island.MaxBuildingLevel(IslandEconomy.Keep);
        if (item.level > islandCap)
            return L.T($"Kıyı Kalesi tavanı aşılıyor (Sev. {item.level} > {islandCap})", $"Keep level cap exceeded (Lv. {item.level} > {islandCap})");
        return null;
    }

    public static bool CanStore(IDepotRegion region, string id, int category, out string reason)
    {
        reason = null;
        if (region == null || Data == null) { reason = L.T("Kayıt yok", "No save"); return false; }
        var snapshot = region.Snapshot(id, category);
        if (snapshot == null) { reason = L.T("Bulunamadı", "Not found"); return false; }
        if (!ShowButton(region.Region, category, snapshot.type)) { reason = L.T("Bu yapı depoya alınamaz", "This can't be stored"); return false; }
        reason = BusyReason(region.Region, category, id);
        return reason == null;
    }

    public static bool CanRestore(DepotItem item, out string reason)
    {
        reason = RestoreBlockedReason(item);
        return reason == null;
    }

    // ==================== İŞLEMLER ====================

    /// <summary>
    /// Depoya al: sahneden düş + yerleşik listeden çıkar + depoya ekle + TEK SaveGame. Aynı kimlik depoda zaten varsa (bozuk durum) önce
    /// silinir: tek kopya.
    /// </summary>
    public static bool Store(IDepotRegion region, string id, int category, out string reason)
    {
        if (!CanStore(region, id, category, out reason)) return false;
        var item = region.Snapshot(id, category);
        item.region = region.Region;
        item.category = category;
        item.level = Mathf.Clamp(item.level, 1, Progression.MaxLevel);
        item.storedTicks = GameClock.UtcNow.Ticks;
        if (!region.Despawn(item)) { reason = L.T("Kaldırılamadı", "Couldn't remove"); return false; }
        var list = List;
        list.RemoveAll(other => other != null && other.region == item.region && other.category == item.category && other.id == item.id);
        list.Add(item);
        StoredThisSession++;
        region.Persist();
        Debug.Log($"[Depot] depoya alındı: {item.type} {item.id} Sev.{item.level} (bölge {item.region}) · {Telemetry()}");
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Depodan kur (ücretsiz, süresiz, seviye korunur): önce kur; başarılıysa depodan çıkar + TEK SaveGame. Kurulamazsa depo değişmez.
    /// Ek koşul (b): öğe en çok bir kez kurulur (kurulunca listeden çıkar; ikinci çağrı "depoda yok" döner).
    /// </summary>
    public static bool Restore(IDepotRegion region, string id, Vector2 at, out string reason)
    {
        reason = null;
        if (region == null) { reason = L.T("Kayıt yok", "No save"); return false; }
        var item = Find(region.Region, id);
        if (item == null) { reason = L.T("Depoda yok", "Not in storage"); return false; }
        if (!CanRestore(item, out reason)) return false;
        if (!region.Spawn(item, at)) { reason = L.T("Buraya kurulamıyor", "Can't place here"); return false; }
        List.Remove(item);
        RestoredThisSession++;
        region.Persist();
        Debug.Log($"[Depot] depodan kuruldu: {item.type} {item.id} Sev.{item.level} (bölge {item.region}) · {Telemetry()}");
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Telemetri satırı (PlaytestBot/IslandStats günlüğü): oturumdaki alma/kurma sayısı, depodaki tür dağılımı ve
    /// "yerleşik kule / sahip olunan kule" (savaş başı yorumunda depodaki kule görünür olsun).
    /// </summary>
    public static string Telemetry()
    {
        var counts = new SortedDictionary<string, int>();
        var list = List;
        if (list != null)
            foreach (var item in list)
                if (item != null)
                {
                    string key = (item.region == RegionBase ? "base:" : "island:") + item.type;
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                }
        var mix = new System.Text.StringBuilder();
        foreach (var pair in counts) mix.Append(pair.Key).Append('=').Append(pair.Value).Append(' ');
        int stored = StoredTowerCount();
        int placed = PlacedCount(RegionBase, nameof(BuildingType.ArcherTower)) + PlacedCount(RegionBase, nameof(BuildingType.CannonTower))
            + PlacedCount(RegionBase, nameof(BuildingType.Monastery));
        return $"alma {StoredThisSession} · kurma {RestoredThisSession} · depo [{mix.ToString().Trim()}] · kule {placed}/{placed + stored}";
    }

    /// <summary>Test: oturum sayaçlarını sıfırlar.</summary>
    public static void ResetCountersForTests() { StoredThisSession = 0; RestoredThisSession = 0; }
}
