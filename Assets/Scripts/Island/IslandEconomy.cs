using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ada ekonomisi (island_base_v2 §5–§6, §8): Et/Odun cüzdanı, işçi dağılımı, UTC tick ile üretim (çevrimdışı dahil, 8 sa depo tavanı,
/// onarım ×0,5), Ada Ustası görevleri (yeni bina, yükseltme, bölge keşfi; elmasla bitirme) ve hazır köy tohumlama.
/// Saf mantık: <see cref="IslandSaveData"/> üzerinde çalışır, sahne gerektirmez (testler doğrudan çağırır). Sayılar yalnız
/// <see cref="Progression.Island"/> ve <see cref="GemEconomy"/>'dedir. Et cüzdanı API'si (<see cref="Meat"/>, <see cref="AddMeat"/>,
/// <see cref="TrySpendMeat"/>, <see cref="MeatShortfallGems"/>) Kart Atölyesi'nin harcaması için temiz tutulur.
/// </summary>
public static class IslandEconomy
{
    public static IslandSaveData Save => IslandSession.Save;
    public static long Now => IslandRepair.Now;
    public static int Keep => IslandSession.KeepLevel();

    /// <summary>Usta görevi bitti (yeni bina, yükseltme ya da bölge): görseller bunu dinler.</summary>
    public static event Action<IslandBuilderTask> TaskCompleted;

    /// <summary>
    /// Bölge sınırı değişince (12 Kasım 2026: orta çayır Z1/Z2'ye bölündü) eski kayıtta oyuncunun binası kilitli bölgeye düşebilir:
    /// o bölge bedava açılır (bina bulut altında kalmaz, üretimi düşmez). Değişen maskeyi döndürür; idempotent.
    /// </summary>
    public static bool OpenZonesUnderBuildings(IslandSaveData save)
    {
        if (save?.placed == null) return false;
        int before = save.zoneMask;
        foreach (var b in save.placed)
        {
            if (b == null || !Enum.TryParse(b.kind, out IslandLayout.BuildingKind kind)) continue;
            if (b.id != null && b.id.Contains("@")) continue;   // hazır köy (adacık kuleleri Z1/Z3'te durur, bölgeyi açmaz)
            var size = IslandGrid.Size(kind);
            for (int c = b.col; c < b.col + size.x; c++)
                for (int r = b.row; r < b.row + size.y; r++)
                {
                    int zone = IslandLayout.ZoneAt(c, r);
                    if (zone > 0) save.zoneMask |= 1 << zone;
                }
        }
        return save.zoneMask != before;
    }

    // ==================== KERESTE SEVKİYATI (Odun → Ana Üs inşaatı) ====================

    public static int LumberShipmentsToday
    {
        get
        {
            var data = GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
            return data == null || data.lumberShipDay != GameClock.TodayId ? 0 : data.lumberShipCount;
        }
    }

    public static int LumberShipmentsLeft => Mathf.Max(0, GemEconomy.DailyLumberShipments - LumberShipmentsToday);

    /// <summary>Sevkiyat seçeneği gösterilsin mi: ada açık (Ana Üs 5+) ve bugün hak kalmış. Odun yetmese de görünür (neden yazar).</summary>
    public static bool LumberShipmentOffered => IslandSession.Unlocked() && LumberShipmentsLeft > 0 && Save != null;

    public static bool CanShipLumber => LumberShipmentOffered && Wood >= GemEconomy.LumberShipmentWood;

    /// <summary>100 Odun → süren Ana Üs inşaatı 10 dk kısalır (günde 3). Odun yoksa ya da iş geçersizse hiçbir şey harcanmaz.</summary>
    public static bool TryShipLumber(BuilderTaskSave task)
    {
        var gm = GameManager.Instance;
        if (gm == null || task == null || !CanShipLumber) return false;
        if (!gm.SpeedUpTask(task, GemEconomy.LumberShipmentMinutes)) return false;
        Save.wood -= GemEconomy.LumberShipmentWood;
        var data = gm.PlayerData;
        if (data.lumberShipDay != GameClock.TodayId) { data.lumberShipDay = GameClock.TodayId; data.lumberShipCount = 0; }
        data.lumberShipCount++;
        gm.SaveGame();
        return true;
    }

    // ==================== CÜZDAN (Et / Odun) ====================

    public static int Meat => Save != null ? Save.meat : 0;
    public static int Wood => Save != null ? Save.wood : 0;
    public static int MeatCap => Progression.Island.WalletMeat(Keep);
    public static int WoodCap => Progression.Island.WalletWood(Keep);

    /// <summary>Ödül (Akın, hediye): cüzdan tavanını aşabilir, ödül kaybolmaz. Üretim toplamak tavana uyar (<see cref="CollectFrom"/>).</summary>
    public static void AddMeat(int amount) { if (Save != null && amount > 0) Save.meat += amount; }
    public static void AddWood(int amount) { if (Save != null && amount > 0) Save.wood += amount; }

    public static bool CanSpend(int wood, int meat) => Save != null && Save.wood >= wood && Save.meat >= meat;

    public static bool TrySpend(int wood, int meat)
    {
        if (!CanSpend(wood, meat)) return false;
        Save.wood -= Mathf.Max(0, wood);
        Save.meat -= Mathf.Max(0, meat);
        return true;
    }

    /// <summary>Kart Atölyesi gibi yalnız Et harcayanlar için.</summary>
    public static bool TrySpendMeat(int meat) => TrySpend(0, meat);

    /// <summary>Eksik Et/Odun'u elmasla tamamlama bedeli (0: eksik yok).</summary>
    public static int MeatShortfallGems(int meat) => GemEconomy.MeatTopUpCost(Mathf.Max(0, meat - Meat));
    public static int ShortfallGems(int wood, int meat) =>
        GemEconomy.WoodTopUpCost(Mathf.Max(0, wood - Wood)) + GemEconomy.MeatTopUpCost(Mathf.Max(0, meat - Meat));

    /// <summary>Bedeli öder; eksikleri elmasla tamamlar (useGems). Elmas yetmezse hiçbir şey harcamaz.</summary>
    public static bool TrySpendWithGems(int wood, int meat, bool useGems)
    {
        if (Save == null) return false;
        if (CanSpend(wood, meat)) return TrySpend(wood, meat);
        if (!useGems) return false;
        int gems = ShortfallGems(wood, meat);
        var gm = GameManager.Instance;
        if (gm == null || !gm.SpendGems(gems)) return false;
        Save.wood = Mathf.Max(0, Save.wood - wood);
        Save.meat = Mathf.Max(0, Save.meat - meat);
        return true;
    }

    // ==================== BİNA SORGULARI ====================

    public static IslandPlacedBuilding Find(string id)
    {
        var save = Save;
        return save == null || id == null ? null : save.placed.Find(b => b != null && b.id == id);
    }

    public static IslandLayout.BuildingKind KindOf(IslandPlacedBuilding b) => IslandGrid.ParseKind(b.kind);

    public static bool IsProducer(IslandLayout.BuildingKind kind) =>
        kind == IslandLayout.BuildingKind.Lumber || kind == IslandLayout.BuildingKind.Pasture;

    public static int CountOf(IslandLayout.BuildingKind kind, IList<IslandPlacedBuilding> placed = null)
    {
        int count = 0;
        foreach (var b in placed ?? Save?.placed) if (b != null && b.kind == kind.ToString()) count++;
        return count;
    }

    /// <summary>Hazır köyü (Kale hariç) kayda yazar; yalnız bir kez (v6'dan geçişte seviyeler Faz C ile aynı, stok 0).</summary>
    public static void EnsureSeeded(IslandSaveData save, int keep)
    {
        if (save == null || save.villageSeeded) return;
        save.placed.Clear();
        foreach (var spot in IslandLayout.Buildings)
        {
            if (spot.kind == IslandLayout.BuildingKind.Castle) continue;
            save.placed.Add(new IslandPlacedBuilding
            {
                id = $"{spot.kind}@{spot.col},{spot.row}", kind = spot.kind.ToString(),
                level = Mathf.Max(1, Progression.Island.PrebuiltLevel(spot.kind.ToString(), keep)),
                col = spot.col, row = spot.row, stock = 0f, lastTick = Now,
            });
        }
        save.villageSeeded = true;
    }

    // ==================== İŞÇİ DAĞILIMI (§5) ====================

    /// <summary>Piyon sayısı = 2 (kale) + tamamlanmış Ev başına 2/3/4.</summary>
    public static int PawnPool(IList<IslandPlacedBuilding> placed)
    {
        int pool = Progression.Island.CastlePawns;
        foreach (var b in placed)
            if (b != null && b.level >= 1 && b.kind == IslandLayout.BuildingKind.House.ToString()) pool += Progression.Island.HousePawns(b.level);
        return pool;
    }

    /// <summary>
    /// İşçi dağılımı: önce elle ayarlananlar (yuva ve havuzla sınırlı), kalanlar en düşük doluluk oranlı üreticiye gider
    /// (eşitlikte Ağıl). Dönen sözlük: bina kimliği → işçi.
    /// </summary>
    public static Dictionary<string, int> AssignWorkers(IList<IslandPlacedBuilding> placed)
    {
        var result = new Dictionary<string, int>();
        int pool = PawnPool(placed);
        var auto = new List<IslandPlacedBuilding>();
        foreach (var b in placed)
        {
            if (b == null || b.level < 1) continue;
            var kind = KindOf(b);
            if (!IsProducer(kind)) continue;
            result[b.id] = 0;
            if (b.workers >= 0)
            {
                int give = Mathf.Min(Mathf.Min(b.workers, Progression.Island.Slots(kind, b.level)), pool);
                result[b.id] = give;
                pool -= give;
            }
            else auto.Add(b);
        }
        while (pool > 0)
        {
            IslandPlacedBuilding best = null;
            float bestRatio = float.MaxValue;
            foreach (var b in auto)
            {
                var kind = KindOf(b);
                int slots = Progression.Island.Slots(kind, b.level);
                if (result[b.id] >= slots) continue;
                float ratio = result[b.id] / (float)slots;
                bool better = ratio < bestRatio - 1e-4f
                    || Mathf.Abs(ratio - bestRatio) <= 1e-4f && kind == IslandLayout.BuildingKind.Pasture && KindOf(best) != IslandLayout.BuildingKind.Pasture;
                if (better) { best = b; bestRatio = ratio; }
            }
            if (best == null) break;
            result[best.id]++;
            pool--;
        }
        return result;
    }

    /// <summary>Elle işçi ayarı (−1: otomatik). Yuva ve havuza göre sınırlanır; ayarlar yuvaya göre sıkıştırılır.</summary>
    public static bool SetWorkers(string id, int workers)
    {
        var b = Find(id);
        if (b == null || b.level < 1 || !IsProducer(KindOf(b))) return false;
        b.workers = workers < 0 ? -1 : Mathf.Clamp(workers, 0, Progression.Island.Slots(KindOf(b), b.level));
        GameManager.Instance?.SaveGame();
        return true;
    }

    // ==================== ÜRETİM (§6) ====================

    /// <summary>Doğa çarpanı: Oduncu ağaç / (2 × yuva) (en çok 1); Ağıl çayır hücresi / 6 → 1 ya da ×0,5 (yetersiz).</summary>
    public static float NatureFactor(IslandPlacedBuilding b, int zoneMask)
    {
        var kind = KindOf(b);
        var center = IslandGrid.CenterCell(kind, b.col, b.row);
        if (kind == IslandLayout.BuildingKind.Lumber)
        {
            int need = Progression.Island.TreesPerSlot * Progression.Island.LumberSlots(b.level);
            return Mathf.Clamp01(IslandGrid.TreesNear(center, zoneMask) / (float)need);
        }
        if (kind == IslandLayout.BuildingKind.Pasture)
            return IslandGrid.MeadowNear(center) >= Progression.Island.MeadowCells ? 1f : Progression.Island.MeadowShortMult;
        return 1f;
    }

    /// <summary>Tam kadro, doğa kısıtsız saatlik üretim (seviyeye göre).</summary>
    public static float FullRate(IslandPlacedBuilding b) =>
        KindOf(b) == IslandLayout.BuildingKind.Lumber ? Progression.Island.WoodPerHour(b.level) : Progression.Island.MeatPerHour(b.level);

    /// <summary>İkinci Ağıl çarpanı (ayar düğmesi §11).</summary>
    private static float PastureOrderMult(IslandPlacedBuilding b, IList<IslandPlacedBuilding> placed)
    {
        if (KindOf(b) != IslandLayout.BuildingKind.Pasture) return 1f;
        int index = 0;
        foreach (var other in placed) { if (other == b) break; if (other != null && other.kind == b.kind && other.level >= 1) index++; }
        return index >= 1 ? Progression.Island.SecondPastureMult : 1f;
    }

    /// <summary>Gerçek saatlik üretim: tam kadro × işçi/yuva × doğa × (2. Ağıl). Onarım çarpanı zaman penceresinde ayrıca uygulanır.</summary>
    public static float Rate(IslandPlacedBuilding b, int workers, int zoneMask, IList<IslandPlacedBuilding> placed)
    {
        var kind = KindOf(b);
        int slots = Mathf.Max(1, Progression.Island.Slots(kind, b.level));
        return FullRate(b) * Mathf.Clamp01(workers / (float)slots) * NatureFactor(b, zoneMask) * PastureOrderMult(b, placed) * MeadowBonusMult(b, zoneMask);
    }

    /// <summary>Orta Çayır (Z5) açıkken Ağıl üretimi +%15 (§4, §6; <see cref="Progression.Island.MeadowBonus"/>).</summary>
    public static float MeadowBonusMult(IslandPlacedBuilding b, int zoneMask) =>
        KindOf(b) == IslandLayout.BuildingKind.Pasture && IslandGrid.ZoneOpen(Progression.Island.MeadowZone, zoneMask) ? 1f + Progression.Island.MeadowBonus : 1f;

    /// <summary>Bina deposu: 8 sa tam kadro üretim (doğa kısıtı depoyu küçültmez).</summary>
    public static float Capacity(IslandPlacedBuilding b) => FullRate(b) * Progression.Island.ProducerStorageHours;

    /// <summary>Stokları <paramref name="now"/> anına getirir (çevrimdışı dahil). Onarım süresi ×0,5 üretir; tavanı aşmaz.</summary>
    public static void Accumulate(IslandSaveData save, long now)
    {
        if (save == null) return;
        var workers = AssignWorkers(save.placed);
        foreach (var b in save.placed)
        {
            if (b == null) continue;
            var kind = KindOf(b);
            if (b.level < 1 || !IsProducer(kind) || b.lastTick <= 0 || b.lastTick > now) { b.lastTick = now; continue; }
            double seconds = IslandRepair.EffectiveSeconds(save, b.lastTick, now);
            workers.TryGetValue(b.id, out int crew);
            float produced = Rate(b, crew, save.zoneMask, save.placed) * (float)(seconds / 3600.0);
            b.stock = Mathf.Min(Capacity(b), b.stock + produced);
            b.lastTick = now;
        }
        AccumulateVein(save, now);
    }

    // ==================== ALTIN DAMARI (Z3, Faz E1: §5, §6) ====================

    /// <summary>Altın Damarı Z3 açılınca çalışır (doğa nesnesi: kurulmaz, yükseltilmez; seviye = Kıyı Kalesi).</summary>
    public static bool VeinOpen(IslandSaveData save) => save != null && IslandGrid.ZoneOpen(Progression.Island.VeinZone, save.zoneMask);

    public static float VeinRate(int keep) => Progression.Island.GoldVeinPerHour(keep);

    /// <summary>Damar deposu: 8 sa üretim (toplamak için dokunulur). Madenci piyon havuzdan düşmez (sapma, §20).</summary>
    public static float VeinCapacity(int keep) => VeinRate(keep) * Progression.Island.ProducerStorageHours;

    /// <summary>Damar stokunu <paramref name="now"/> anına getirir (çevrimdışı dahil, 8 sa tavan). Z3 kapalıysa hiçbir şey yapmaz.</summary>
    public static void AccumulateVein(IslandSaveData save, long now)
    {
        if (save == null) return;
        if (!VeinOpen(save)) { save.veinTick = 0; return; }
        if (save.veinTick <= 0 || save.veinTick > now) { save.veinTick = now; return; }
        double hours = (now - save.veinTick) / (double)TimeSpan.TicksPerHour;
        int keep = Mathf.Max(1, Progression.Island.EffectiveKeepLevel(save.keepLevel, IslandSession.MainBaseLevel));
        save.veinStock = Mathf.Min(VeinCapacity(keep), save.veinStock + VeinRate(keep) * (float)hours);
        save.veinTick = now;
    }

    /// <summary>Damardaki altını Ana Üs altınına ekler (toplanmamış stok). Eklenen miktarı döndürür.</summary>
    public static int CollectVein()
    {
        var save = Save;
        if (save == null || !VeinOpen(save)) return 0;
        AccumulateVein(save, Now);
        int amount = Mathf.FloorToInt(save.veinStock);
        if (amount <= 0) return 0;
        save.veinStock -= amount;
        GameManager.Instance?.AddGold(amount);
        GameManager.Instance?.SaveGame();
        return amount;
    }

    /// <summary>Yığın simgesi sayısı 0–6 (depo doluluğu; 1 altının altı 0).</summary>
    public static int VeinPileCount(IslandSaveData save, int keep)
    {
        if (save == null || !VeinOpen(save) || save.veinStock < 1f) return 0;
        return Mathf.Clamp(Mathf.CeilToInt(save.veinStock / Mathf.Max(1f, VeinCapacity(keep)) * 6f - 0.001f), 1, 6);
    }

    // ==================== KIŞLA / MANASTIR SORGULARI (Faz E1) ====================

    /// <summary>Tamamlanmış (level ≥ 1) ilk bina bu türde mi; yoksa null.</summary>
    public static IslandPlacedBuilding CompletedOf(IslandLayout.BuildingKind kind, IList<IslandPlacedBuilding> placed = null)
    {
        foreach (var b in placed ?? Save?.placed)
            if (b != null && b.level >= 1 && b.kind == kind.ToString()) return b;
        return null;
    }

    /// <summary>Tamamlanmış bir Manastır var mı (onarım süresi ×0,5, §7.7).</summary>
    public static bool HasMonastery() => Save != null && CompletedOf(IslandLayout.BuildingKind.Monastery, Save.placed) != null;

    /// <summary>Yığındaki 0–6 simge sayısı (doluluk oranı; stok 1'in altındaysa 0).</summary>
    public static int PileCount(IslandPlacedBuilding b)
    {
        if (b == null || b.stock < 1f) return 0;
        return Mathf.Clamp(Mathf.CeilToInt(b.stock / Capacity(b) * 6f - 0.001f), 1, 6);
    }

    /// <summary>Yığını toplar: cüzdan tavanına kadar. Eklenen miktarı döndürür (0: boş ya da cüzdan dolu).</summary>
    public static int CollectFrom(IslandPlacedBuilding b)
    {
        var save = Save;
        if (save == null || b == null || b.level < 1) return 0;
        Accumulate(save, Now);
        bool wood = KindOf(b) == IslandLayout.BuildingKind.Lumber;
        int room = Mathf.Max(0, (wood ? WoodCap - save.wood : MeatCap - save.meat));
        int amount = Mathf.Min(Mathf.FloorToInt(b.stock), room);
        if (amount <= 0) return 0;
        b.stock -= amount;
        if (wood) save.wood += amount; else save.meat += amount;
        GameManager.Instance?.SaveGame();
        return amount;
    }

    // ==================== ADA USTASI (§5) ====================

    public static bool BuilderBusy => Save != null && Save.task != null && Save.task.Active;

    public static double TaskSecondsLeft =>
        BuilderBusy ? Math.Max(0d, new TimeSpan(Save.task.endTicks - Now).TotalSeconds) : 0d;

    /// <summary>Savaşta inşa/taşıma kilitlidir (§3).</summary>
    public static bool InDefense => IslandSceneSetup.Instance != null && IslandSceneSetup.Instance.InDefense;

    private static string BlockedReason()
    {
        if (Save == null) return L.T("Kayıt yok", "No save");
        if (InDefense) return L.T("Savaşta inşa yapılamaz", "Cannot build during defense");
        if (BuilderBusy) return L.T("Ada Ustası meşgul", "Island Builder is busy");
        return null;
    }

    private static void StartTask(string kind, string id, int zone, int level, int seconds)
    {
        long now = Now;
        Save.task = new IslandBuilderTask
        {
            kind = kind, buildingId = id ?? "", zone = zone, targetLevel = level, startTicks = now,
            endTicks = now + (long)seconds * TimeSpan.TicksPerSecond,
        };
    }

    public static (int wood, int meat, int seconds) UpgradeCost(IslandPlacedBuilding b)
    {
        int target = b.level + 1;
        var kind = KindOf(b);
        return (Progression.Island.UpgradeWood(kind, target), Progression.Island.UpgradeMeat(kind, target), Progression.Island.BuildSeconds(target));
    }

    /// <summary>Yükseltilebilir mi (Okçuluk Kıyı Kalesi'ni izler: yükseltme yok). Neden yoksa null.</summary>
    public static string UpgradeBlockedReason(IslandPlacedBuilding b)
    {
        if (b == null) return L.T("Bina yok", "No building");
        var kind = KindOf(b);
        if (kind == IslandLayout.BuildingKind.Archery) return L.T("Okçu, Kıyı Kalesi ile yükselir", "The archer levels with the keep");
        if (b.level < 1) return L.T("İnşaat sürüyor", "Under construction");
        if (b.level >= Progression.Island.MaxBuildingLevel(Keep))
            return L.T($"Kıyı Kalesi {Keep}: tavan", $"Keep {Keep}: level cap");
        return BlockedReason();
    }

    public static bool TryStartUpgrade(string id, bool useGems, out string reason)
    {
        var b = Find(id);
        reason = UpgradeBlockedReason(b);
        if (reason != null) return false;
        var cost = UpgradeCost(b);
        if (!TrySpendWithGems(cost.wood, cost.meat, useGems)) { reason = L.T("Kaynak yetersiz", "Not enough resources"); return false; }
        StartTask("upgrade", b.id, 0, b.level + 1, cost.seconds);
        GameManager.Instance?.SaveGame();
        return true;
    }

    /// <summary>Yeni bina: geçerlilik + bedel + Usta. Başarılıysa inşaat altındaki (level 0) kayıt döner.</summary>
    public static IslandPlacedBuilding TryStartBuild(IslandLayout.BuildingKind kind, int col, int row, bool useGems, out string reason)
    {
        reason = BlockedReason();
        if (reason != null) return null;
        var check = IslandGrid.Check(kind, col, row, Save.placed, Save.zoneMask, null, Keep);
        if (check != IslandGrid.Reason.None) { reason = IslandGrid.ReasonText(check); return null; }
        var cost = Progression.Island.PlaceCost(kind);
        if (kind == IslandLayout.BuildingKind.Archery || cost.wood + cost.meat <= 0 && kind != IslandLayout.BuildingKind.House)
        { reason = L.T("Bu bina kurulamaz", "This building cannot be built"); return null; }
        if (!TrySpendWithGems(cost.wood, cost.meat, useGems)) { reason = L.T("Kaynak yetersiz", "Not enough resources"); return null; }
        var entry = new IslandPlacedBuilding
        {
            id = $"{kind}#{Save.nextBuildingSerial++}", kind = kind.ToString(), level = 0, col = col, row = row, lastTick = Now, workers = -1,
        };
        Save.placed.Add(entry);
        StartTask("build", entry.id, 0, 1, Progression.Island.BuildSeconds(1));
        GameManager.Instance?.SaveGame();
        return entry;
    }

    /// <summary>Taşıma (bedava, anında): geçerli hücreye alır. Savaşta yok.</summary>
    public static bool TryMove(string id, int col, int row, out string reason)
    {
        reason = InDefense ? L.T("Savaşta taşınamaz", "Cannot move during defense") : null;
        var b = Find(id);
        if (reason != null || b == null) return false;
        var kind = KindOf(b);
        var check = IslandGrid.Check(kind, col, row, Save.placed, Save.zoneMask, id, Keep);
        if (check != IslandGrid.Reason.None) { reason = IslandGrid.ReasonText(check); return false; }
        Accumulate(Save, Now);
        b.col = col; b.row = row;
        GameManager.Instance?.SaveGame();
        return true;
    }

    public static string ZoneBlockedReason(int zone)
    {
        var save = Save;
        if (save == null) return L.T("Kayıt yok", "No save");
        if (IslandGrid.ZoneOpen(zone, save.zoneMask)) return L.T("Bölge zaten açık", "Zone already open");
        if (!Progression.Island.ZoneRevealable(zone)) return L.T("Bu bölge sonraki güncellemede açılır", "This zone opens in a later update");
        if (Keep < Progression.Island.ZoneKeep[zone]) return L.T($"Kıyı Kalesi {Progression.Island.ZoneKeep[zone]} gerekir", $"Requires Keep {Progression.Island.ZoneKeep[zone]}");
        return BlockedReason();
    }

    public static bool TryStartZone(int zone, bool useGems, out string reason)
    {
        reason = ZoneBlockedReason(zone);
        if (reason != null) return false;
        if (!TrySpendWithGems(Progression.Island.ZoneWood[zone], Progression.Island.ZoneMeat[zone], useGems))
        { reason = L.T("Kaynak yetersiz", "Not enough resources"); return false; }
        StartTask("zone", null, zone, 0, Progression.Island.ZoneSeconds[zone]);
        GameManager.Instance?.SaveGame();
        return true;
    }

    /// <summary>Elmasla bitirme bedeli (<see cref="GemEconomy.FinishCost"/>: 1 dk altı bedava).</summary>
    public static int FinishGemCost() => BuilderBusy ? GemEconomy.FinishCost(TaskSecondsLeft) : 0;

    public static bool TryFinishNow(out string reason)
    {
        reason = null;
        if (!BuilderBusy) { reason = L.T("Görev yok", "No task"); return false; }
        int cost = FinishGemCost();
        if (cost > 0 && (GameManager.Instance == null || !GameManager.Instance.SpendGems(cost)))
        { reason = L.T("Elmas yetersiz", "Not enough gems"); return false; }
        Save.task.endTicks = Now;
        Tick(Now, true);
        return true;
    }

    /// <summary>Görev bitiş anında üretimi eski seviyeyle işler, görevi uygular; sonra stokları şimdiye getirir.</summary>
    public static void Tick(long now, bool allowComplete = true)
    {
        var save = Save;
        if (save == null) return;
        var task = save.task;
        if (allowComplete && task != null && task.Active && now >= task.endTicks && !InDefense)
        {
            Accumulate(save, Math.Min(now, Math.Max(task.endTicks, 1)));
            var done = task;
            var entry = Find(done.buildingId);
            switch (done.kind)
            {
                case "build": if (entry != null) { entry.level = 1; entry.lastTick = task.endTicks; } break;
                case "upgrade": if (entry != null) entry.level = Mathf.Max(entry.level, done.targetLevel); break;
                case "zone":
                    save.zoneMask |= 1 << done.zone;
                    if (done.zone == Progression.Island.VeinZone) { save.veinTick = task.endTicks; save.veinStock = 0f; }   // damar bu andan çalışır
                    break;
            }
            save.task = new IslandBuilderTask();
            GameManager.Instance?.SaveGame();
            TaskCompleted?.Invoke(done);
        }
        Accumulate(save, now);
    }
}
