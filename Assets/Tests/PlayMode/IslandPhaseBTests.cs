using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz B (island_base_v2 §5, §6, §8, §17): formül testleri, 8 sa çevrimdışı üretim (tavan + onarım çarpanı), işçi dağılımı,
/// yerleştirme geçerliliği, sahnede inşa/yükseltme/toplama/bölge keşfi ve Et:altın ekonomi simülasyonu. Yansıma ile (Assembly-CSharp).
/// </summary>
public class IslandPhaseBTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");

    internal static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private string savedKey, savedBackup, savedFile;
    private bool devWas, gemsWas;

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
    }

    [TearDown]
    public void RestoreRealSave()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    // ==================== YARDIMCILAR ====================

    internal static object Call(string type, string method, params object[] args) => T(type).GetMethod(method).Invoke(null, args);
    internal static object Isl(string method, params object[] args) => T("Progression+Island").GetMethod(method).Invoke(null, args);
    internal static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
    internal static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    internal static object Kind(string name) => Enum.Parse(T("IslandLayout+BuildingKind"), name);
    internal static void SetClock(long ticks) => T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => ticks));
    internal const long Hour = TimeSpan.TicksPerHour;

    internal static object NewSave() => T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });

    internal static object NewIsland(int keep)
    {
        var island = Field(NewSave(), "island");
        Set(island, "keepLevel", keep);
        return island;
    }

    internal static System.Collections.IList Placed(object island) => (System.Collections.IList)Field(island, "placed");

    internal static object Entry(string id, string kind, int level, int col, int row)
    {
        var entry = Activator.CreateInstance(T("IslandPlacedBuilding"));
        Set(entry, "id", id); Set(entry, "kind", kind); Set(entry, "level", level); Set(entry, "col", col); Set(entry, "row", row);
        return entry;
    }

    internal static object FindEntry(object island, string kind)
    {
        foreach (var entry in Placed(island)) if ((string)Field(entry, "kind") == kind) return entry;
        return null;
    }

    internal static string Check(string kind, int col, int row, object island, int mask, string movingId = null, int keep = 15) =>
        Call("IslandGrid", "Check", Kind(kind), col, row, Placed(island), mask, movingId, keep).ToString();

    internal static string StaticReason(int col, int row) => Call("IslandGrid", "StaticReason", col, row).ToString();

    internal static bool FindCell(string kind, string wantedReason, object island, int mask, out int col, out int row, int keep = 15)
    {
        for (int c = 0; c < 22; c++)
            for (int r = 0; r < 36; r++)
                if (Check(kind, c, r, island, mask, null, keep) == wantedReason) { col = c; row = r; return true; }
        col = row = -1;
        return false;
    }

    /// <summary>Geçerli (None) hücrelerin hedefe en yakını (ekran görüntüleri için köy merkezine yakın yerleşim).</summary>
    internal static bool FindCellNear(string kind, object island, int mask, int nearCol, int nearRow, out int col, out int row, int keep = 15)
    {
        float best = float.MaxValue; col = row = -1;
        for (int c = 0; c < 22; c++)
            for (int r = 0; r < 36; r++)
            {
                float d = Vector2.Distance(new Vector2(c, r), new Vector2(nearCol, nearRow));
                if (d >= best || Check(kind, c, r, island, mask, null, keep) != "None") continue;
                best = d; col = c; row = r;
            }
        return col >= 0;
    }

    // ==================== FORMÜLLER (§8) ====================

    [Test]
    public void ProductionAndWalletFormulasMatchDesign()
    {
        Assert.AreEqual(30f, (float)Isl("WoodPerHour", 1)); Assert.AreEqual(90f, (float)Isl("WoodPerHour", 5)); Assert.AreEqual(165f, (float)Isl("WoodPerHour", 10));
        Assert.AreEqual(15f, (float)Isl("MeatPerHour", 1)); Assert.AreEqual(47f, (float)Isl("MeatPerHour", 5));
        Assert.AreEqual(87f, (float)Isl("MeatPerHour", 10)); Assert.AreEqual(127f, (float)Isl("MeatPerHour", 15));
        Assert.AreEqual(600, (int)Isl("WalletWood", 1)); Assert.AreEqual(6200, (int)Isl("WalletWood", 15));
        Assert.AreEqual(300, (int)Isl("WalletMeat", 1)); Assert.AreEqual(3100, (int)Isl("WalletMeat", 15));
        Assert.AreEqual(2, (int)Isl("HousePawns", 1)); Assert.AreEqual(3, (int)Isl("HousePawns", 5)); Assert.AreEqual(4, (int)Isl("HousePawns", 10));
        Assert.AreEqual(2, (int)Isl("LumberSlots", 1)); Assert.AreEqual(3, (int)Isl("LumberSlots", 5)); Assert.AreEqual(4, (int)Isl("LumberSlots", 10));
        Assert.AreEqual(1, (int)Isl("PastureSlots", 1)); Assert.AreEqual(2, (int)Isl("PastureSlots", 5)); Assert.AreEqual(3, (int)Isl("PastureSlots", 10));
        // Okçu yükseltmesi: 1→2 35 · 4→5 75 · 9→10 250 · 14→15 870 (≈865: formül 5'e yuvarlanır)
        Assert.AreEqual(35, (int)Isl("ArcherUpgradeMeat", 1)); Assert.AreEqual(75, (int)Isl("ArcherUpgradeMeat", 4));
        Assert.AreEqual(250, (int)Isl("ArcherUpgradeMeat", 9)); Assert.AreEqual(865, (int)Isl("ArcherUpgradeMeat", 14), "35 × 1,28^13 = 866,6 → 5'e yuvarlanır (belgedeki 870 yaklaşık)");
    }

    [Test]
    public void UpgradeCostsAndBuilderTimesMatchDesign()
    {
        // Kale (KK5): 380 Odun + 110 Et (W[5] × 1,0 / 0,3)
        Assert.AreEqual(380, (int)Isl("UpgradeWood", Kind("Castle"), 5));
        Assert.AreEqual(110, (int)Isl("UpgradeMeat", Kind("Castle"), 5));
        // Ağıl yalnız Odun (0,45 / 0): Sv5 = 380 × 0,45 = 171 → 170
        Assert.AreEqual(170, (int)Isl("UpgradeWood", Kind("Pasture"), 5));
        Assert.AreEqual(0, (int)Isl("UpgradeMeat", Kind("Pasture"), 5));
        // Oduncu Sv5: 380 × 0,15 → 60 Odun, 380 × 0,2 → 80 Et
        Assert.AreEqual(60, (int)Isl("UpgradeWood", Kind("Lumber"), 5));
        Assert.AreEqual(80, (int)Isl("UpgradeMeat", Kind("Lumber"), 5));
        Assert.AreEqual(0, (int)Isl("UpgradeMeat", Kind("House"), 9), "Ev Et istemez");
        // Süreler: Ana Üs tablosu × 0,5 (sv 2→15: 5 sn, 30 sn, 1,5 dk … 6 sa)
        var expected = new[] { 5, 30, 90, 180, 300, 450, 900, 1800, 3600, 5400, 7200, 10800, 14400, 21600 };
        for (int level = 2; level <= 15; level++) Assert.AreEqual(expected[level - 2], (int)Isl("BuildSeconds", level), "sv " + level);
        Assert.AreEqual(5, (int)Isl("BuildSeconds", 1), "yeni bina");
        // Yerleştirme bedelleri ve adet tavanları
        Assert.AreEqual((40, 0), ((int, int))Isl("PlaceCost", Kind("House")));
        Assert.AreEqual((60, 10), ((int, int))Isl("PlaceCost", Kind("Tower")));
        Assert.AreEqual((300, 60), ((int, int))Isl("PlaceCost", Kind("Lumber")));
        Assert.AreEqual((600, 0), ((int, int))Isl("PlaceCost", Kind("Pasture")));
        Assert.AreEqual(1, (int)Isl("MaxCount", Kind("House"), 1)); Assert.AreEqual(2, (int)Isl("MaxCount", Kind("House"), 3));
        Assert.AreEqual(3, (int)Isl("MaxCount", Kind("House"), 6)); Assert.AreEqual(4, (int)Isl("MaxCount", Kind("House"), 9));
        Assert.AreEqual(1, (int)Isl("MaxCount", Kind("Tower"), 1)); Assert.AreEqual(3, (int)Isl("MaxCount", Kind("Tower"), 4));
        Assert.AreEqual(5, (int)Isl("MaxCount", Kind("Tower"), 9));
        Assert.AreEqual(1, (int)Isl("MaxCount", Kind("Lumber"), 6)); Assert.AreEqual(2, (int)Isl("MaxCount", Kind("Lumber"), 7));
        Assert.AreEqual(1, (int)Isl("MaxCount", Kind("Pasture"), 9)); Assert.AreEqual(2, (int)Isl("MaxCount", Kind("Pasture"), 10));
    }

    [Test]
    public void GemEconomyTopUpsAndFinishFollowDesign()
    {
        var gem = T("GemEconomy");
        Assert.AreEqual(8, (int)gem.GetField("MeatPerGem").GetValue(null), "1 elmas = 8 Et");
        Assert.AreEqual(25, (int)gem.GetField("WoodPerGem").GetValue(null));
        Assert.AreEqual(13, (int)Call("GemEconomy", "MeatTopUpCost", 100));   // ⌈100/8⌉
        Assert.AreEqual(0, (int)Call("GemEconomy", "MeatTopUpCost", 0));
        Assert.AreEqual(2, (int)Call("GemEconomy", "WoodTopUpCost", 26));      // ⌈26/25⌉
        Assert.AreEqual(0, (int)Call("GemEconomy", "FinishCost", 30d), "1 dk altı bedava");
        Assert.Greater((int)Call("GemEconomy", "FinishCost", 3600d), 0);
    }

    // ==================== ÇEVRİMDIŞI ÜRETİM (8 sa, tavan, onarım ×0,5) ====================

    private static object SeededIsland(long t0, int keep = 5)
    {
        SetClock(t0);
        var island = NewIsland(keep);
        Call("IslandEconomy", "EnsureSeeded", island, keep);
        return island;
    }

    private static float Stock(object entry) => (float)Field(entry, "stock");
    private static float Factor(object entry, int mask = 1) => (float)Call("IslandEconomy", "NatureFactor", entry, mask);

    [Test]
    public void EightHoursOfflineProductionFillsStorageButNeverExceedsCap()
    {
        long t0 = 636_000_000_000_000_000L;
        var island = SeededIsland(t0);
        var lumber = FindEntry(island, "Lumber"); var pasture = FindEntry(island, "Pasture");
        Assert.NotNull(lumber); Assert.NotNull(pasture);
        float treeFactor = Factor(lumber), meadowFactor = Factor(pasture);
        Assert.AreEqual(1f, meadowFactor, 0.001f, "hazır Ağıl'ın yanında 6 çayır hücresi var");
        SetClock(t0 + 8 * Hour);
        Call("IslandEconomy", "Accumulate", island, t0 + 8 * Hour);
        // Sv1 Oduncu: 2/2 işçi × 30/sa × 8 sa = 240 (tavan 240) × ağaç çarpanı; Ağıl 1/1 × 15 × 8 = 120 (tavan 120)
        Assert.AreEqual(240f * treeFactor, Stock(lumber), 0.5f, "8 sa Odun");
        Assert.AreEqual(120f, Stock(pasture), 0.5f, "8 sa Et");
        // 20 sa daha: tavan aşılmaz
        Call("IslandEconomy", "Accumulate", island, t0 + 28 * Hour);
        Assert.LessOrEqual(Stock(lumber), 240f + 0.01f);
        Assert.AreEqual(120f, Stock(pasture), 0.5f, "Et deposu 8 saatlik tavanda kalır");
        Assert.AreEqual(240f, (float)Call("IslandEconomy", "Capacity", lumber), 0.01f);
    }

    [Test]
    public void RepairSlowsProductionToHalfOnlyWhileRepairing()
    {
        long t0 = 636_000_000_000_000_000L;
        var fullRepair = SeededIsland(t0);
        var save = Activator.CreateInstance(T("IslandBuildingSave"));
        Set(save, "id", "House@9,10"); Set(save, "hpFraction", 0.5f); Set(save, "repairEndTicks", t0 + 100 * Hour);
        ((System.Collections.IList)Field(fullRepair, "buildings")).Add(save);
        Call("IslandEconomy", "Accumulate", fullRepair, t0 + 8 * Hour);
        Assert.AreEqual(60f, Stock(FindEntry(fullRepair, "Pasture")), 0.5f, "8 sa boyunca onarım: Et ×0,5");

        // Onarım 4. saatte biter: 4 sa × 0,5 + 4 sa × 1 = 6 sa eşdeğeri
        var partial = SeededIsland(t0);
        var save2 = Activator.CreateInstance(T("IslandBuildingSave"));
        Set(save2, "id", "House@9,10"); Set(save2, "hpFraction", 0.5f); Set(save2, "repairEndTicks", t0 + 4 * Hour);
        ((System.Collections.IList)Field(partial, "buildings")).Add(save2);
        Call("IslandEconomy", "Accumulate", partial, t0 + 8 * Hour);
        Assert.AreEqual(15f * 6f, Stock(FindEntry(partial, "Pasture")), 0.5f);
        Assert.AreEqual(Convert.ToSingle(T("Progression+Island").GetField("RepairProductionMult").GetValue(null)), 0.5f, 0.0001f);
    }

    [Test]
    public void WorkerAssignmentFillsLowestRatioAndHonorsManualOverrides()
    {
        // Eski (kadro v1) dağılım: kale piyonları havuza sayılır. Kadro v2 IslandFreshStartTests'te; burada bayrak geçici kapalı.
        var config = T("FeatureConfig").GetProperty("Instance").GetValue(null);
        var staffFlag = config.GetType().GetField("islandStaffV2");
        bool staffWas = (bool)staffFlag.GetValue(config);
        staffFlag.SetValue(config, false);
        try { LegacyWorkerAssignment(); }
        finally { staffFlag.SetValue(config, staffWas); }
    }

    private void LegacyWorkerAssignment()
    {
        long t0 = 636_000_000_000_000_000L;
        var island = SeededIsland(t0);
        var lumber = FindEntry(island, "Lumber"); var pasture = FindEntry(island, "Pasture");
        string lumberId = (string)Field(lumber, "id"), pastureId = (string)Field(pasture, "id");
        // Başlangıç: 2 (kale) + 1 Ev (2) = 4 piyon; Oduncu 2 yuva, Ağıl 1 yuva
        Assert.AreEqual(4, (int)Call("IslandEconomy", "PawnPool", Placed(island)));
        var assign = (Dictionary<string, int>)Call("IslandEconomy", "AssignWorkers", Placed(island));
        Assert.AreEqual(2, assign[lumberId]); Assert.AreEqual(1, assign[pastureId]);
        // Sv5: Oduncu 3 yuva, Ağıl 2 yuva; havuz 4 → eşitlikte Ağıl önce: Oduncu 2, Ağıl 2
        Set(lumber, "level", 5); Set(pasture, "level", 5);
        assign = (Dictionary<string, int>)Call("IslandEconomy", "AssignWorkers", Placed(island));
        Assert.AreEqual(2, assign[lumberId]); Assert.AreEqual(2, assign[pastureId]);
        // Elle: Oduncu 0 → piyonlar Ağıl'a (yuva 2) kalır
        Set(lumber, "workers", 0);
        assign = (Dictionary<string, int>)Call("IslandEconomy", "AssignWorkers", Placed(island));
        Assert.AreEqual(0, assign[lumberId]); Assert.AreEqual(2, assign[pastureId]);
        // Üretim işçi oranıyla orantılı
        Set(lumber, "workers", -1);
        float full = (float)Call("IslandEconomy", "Rate", lumber, 3, 1, Placed(island));
        float twoThirds = (float)Call("IslandEconomy", "Rate", lumber, 2, 1, Placed(island));
        Assert.AreEqual(full * 2f / 3f, twoThirds, 0.01f);
    }

    // ==================== YERLEŞTİRME GEÇERLİLİĞİ ====================

    [Test]
    public void PlacementRejectsIllegalCellsOverlapsAndLockedZones()
    {
        var island = NewIsland(15);
        const int allZones = 63;
        // Su, yar yüzü, şerit, doğa kapalı
        Assert.AreEqual("Water", Check("House", 0, 0, island, allZones));
        Assert.IsTrue(FindCell("House", "Cliff", island, allZones, out _, out _), "yar yüzü hücresi reddedilmeli");
        Assert.IsTrue(FindCell("House", "Lane", island, allZones, out _, out _), "yürüyüş şeridi reddedilmeli");
        Assert.IsTrue(FindCell("House", "Nature", island, allZones, out _, out _), "ağaç/kaya/çayır reddedilmeli");
        Assert.IsTrue(FindCell("House", "Stairs", island, allZones, out _, out _), "merdiven önü reddedilmeli");
        Assert.AreEqual("OutOfMap", Check("House", 21, 35, island, allZones));
        // Geçerli bir hücre var; üstüne ikinci bina çakışır
        Assert.IsTrue(FindCellNear("House", island, 1, 10, 9, out int c, out int r), "Z0'da en az bir geçerli Ev hücresi olmalı");
        var placed = Placed(island);
        placed.Add(Entry("House#1", "House", 1, c, r));
        Assert.AreEqual("Overlap", Check("House", c, r, island, 1, null, 15), "çakışma reddedilir");
        bool partialOverlap = false;
        foreach (var offset in new[] { (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) })
            partialOverlap |= Check("House", c + offset.Item1, r + offset.Item2, island, 1, null, 15) == "Overlap";
        Assert.IsTrue(partialOverlap, "ayak izi kesişimi de çakışır (kısmi örtüşme)");
        Assert.AreEqual("None", Check("House", c, r, island, 1, "House#1", 15), "taşınan bina kendisiyle çakışmaz");
        // Adet tavanı: KK1'de tek Ev
        Assert.IsTrue(FindCell("House", "Limit", island, 1, out _, out _, 1), "KK1: tek Ev; ikincisi sınırda reddedilir");
        // Kilitli bölge: Z1/Z2 hücresi maske yalnız Z0 iken reddedilir, bölge açılınca geçer (Z1 doğu şeridi ve Z2 koyunda yalnız adacık kulesi kesin var)
        foreach (var (zone, kind) in new[] { (1, "Tower"), (2, "Tower") })
        {
            bool found = false;
            for (int cc = 0; cc < 22 && !found; cc++)
                for (int rr = 0; rr < 36 && !found; rr++)
                    if ((int)Call("IslandLayout", "ZoneAt", cc, rr) == zone && Check(kind, cc, rr, island, 7, null, 15) == "None")
                    {
                        found = true;
                        Assert.AreEqual("Locked", Check(kind, cc, rr, island, 1, null, 15), $"Z{zone} kilitli");
                        Assert.AreEqual("None", Check(kind, cc, rr, island, 7, null, 15), $"Z{zone} açık");
                    }
            Assert.IsTrue(found, $"Z{zone}'de geçerli bir {kind} hücresi olmalı");
        }
        // Bilgi: bölge başına geçerli hücre sayıları (günlüğe; Z1/Z2 getirisi)
        var counts = new System.Text.StringBuilder("Geçerli hücre sayıları (KK15, tüm bölgeler açık, boş ada): ");
        foreach (var kind in new[] { "House", "Lumber", "Pasture", "Tower" })
        {
            var perZone = new int[6];
            for (int cc = 0; cc < 22; cc++)
                for (int rr = 0; rr < 36; rr++)
                    if (Check(kind, cc, rr, island, 63, null, 15) == "None") perZone[(int)Call("IslandLayout", "ZoneAt", cc, rr)]++;
            counts.Append($"{kind}[Z0 {perZone[0]}, Z1 {perZone[1]}, Z2 {perZone[2]}, Z3 {perZone[3]}, Z4 {perZone[4]}, Z5 {perZone[5]}]  ");
        }
        Debug.Log("[IslandGrid] " + counts);
        // Adacıkta yalnız kule
        Assert.IsTrue(FindCell("House", "TowerOnly", island, allZones, out int ic, out int ir), "adacığa Ev kurulamaz");
        Assert.AreEqual("None", Check("Tower", ic, ir, island, allZones, null, 15), "adacığa kule kurulur");
    }

    [Test]
    public void PrebuiltVillageSitsOnLegalCells()
    {
        var island = NewIsland(15);
        var layout = T("IslandLayout");
        var spots = (Array)layout.GetField("Buildings").GetValue(null);
        foreach (var spot in spots)
        {
            string kind = Field(spot, "kind").ToString();
            if (kind == "Castle") continue;
            int col = (int)Field(spot, "col"), row = (int)Field(spot, "row");
            Assert.AreEqual("None", Check(kind, col, row, island, 63, null, 15), $"hazır köy {kind}@{col},{row}");
        }
    }

    [Test]
    public void LumberNeedsTreesAndPastureNeedsMeadow()
    {
        var island = NewIsland(15);
        Assert.IsTrue(FindCell("Lumber", "NeedTrees", island, 63, out _, out _), "ağaçsız yere Oduncu kurulamaz");
        Assert.IsTrue(FindCell("Pasture", "NeedMeadow", island, 63, out _, out _), "çayırsız yere Ağıl kurulamaz");
        Assert.IsTrue(FindCell("Lumber", "None", island, 63, out _, out _));
        Assert.IsTrue(FindCell("Pasture", "None", island, 63, out _, out _));
    }

    // ==================== SAHNEDE İNŞA / YÜKSELTME / TOPLAMA / BÖLGE ====================

    /// <summary>
    /// Adayı sahnede açar. Varsayılan: eski tarz (v10 öncesi) tohumlanmış köy — AŞAMA 2'den önce yazılmış testler hazır köyü varsayar
    /// (Ev, Oduncu, Okçuluk, Ağıl, 2 Kule; bu türlerin ilk yapım hakkı kullanılmış). <paramref name="fresh"/>: yeni oyuncu adası (yalnız kale).
    /// </summary>
    internal static IEnumerator LoadIsland(Action<object> configure, int keep, Action<bool> setDev, bool fresh = false)
    {
        var dev = T("DevSettings").GetProperty("Enabled");
        setDev((bool)dev.GetValue(null));
        dev.SetValue(null, true);
        var managerType = T("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        var island = Field(save, "island");
        Set(island, "keepLevel", keep);
        Set(island, "tutorialStep", 4);
        if (!fresh) Call("IslandEconomy", "EnsureSeeded", island, Math.Max(1, keep));
        configure?.Invoke(island);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        T("IslandSession").GetMethod("Begin").Invoke(null, null);
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 20; i++) yield return null;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            var setup = UnityEngine.Object.FindFirstObjectByType(T("IslandSceneSetup"));
            if (setup != null && (bool)setup.GetType().GetProperty("Ready").GetValue(setup)) break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.4f);
    }

    internal static Component Setup => (Component)UnityEngine.Object.FindFirstObjectByType(T("IslandSceneSetup"));
    internal static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

    internal static object CurrentIsland()
    {
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        return Field(Prop(gm, "PlayerData"), "island");
    }

    [UnityTest]
    public IEnumerator PlacingBuildingUpgradingCollectingAndRevealingZoneWorkOnTheIsland()
    {
        yield return LoadIsland(island =>
        {
            Set(island, "wood", 1500); Set(island, "meat", 400);
        }, 5, v => devWas = v);
        var island0 = CurrentIsland();
        long t0 = DateTime.UtcNow.Ticks;
        SetClock(t0);
        var village = Prop(Setup, "Village");
        var hud = Prop(Setup, "Hud");
        int liveBefore = ((System.Collections.ICollection)T("IslandBuilding").GetProperty("All").GetValue(null)).Count;
        Assert.AreEqual(7, liveBefore, "Kale + Ev, Oduncu, Okçuluk, Ağıl, 2 Kule");

        // --- Yerleştirme: şerit hücresi kırmızı, geçerli hücre yeşil, onay inşaat alanı açar ---
        hud.GetType().GetMethod("StartPlacement").Invoke(hud, new[] { Call("IslandPlacementHost", "ForNew", Kind("House")) });
        var controller = (Component)UnityEngine.Object.FindFirstObjectByType(T("IslandPlacementController"));
        Assert.IsTrue((bool)Prop(controller, "IsPlacing"));
        Assert.IsTrue(FindCell("House", "Lane", island0, 1, out int lc, out int lr, 5));
        controller.GetType().GetMethod("MoveGhostTo").Invoke(controller, new object[] { lc, lr });
        Assert.IsFalse((bool)Prop(controller, "Valid"), "şerit hücresi geçersiz (kırmızı)");
        Assert.IsFalse((bool)controller.GetType().GetMethod("Confirm").Invoke(controller, null), "geçersiz hücre onaylanmaz");
        Assert.IsTrue(FindCell("House", "None", island0, 1, out int gc, out int gr, 5));
        controller.GetType().GetMethod("MoveGhostTo").Invoke(controller, new object[] { gc, gr });
        Assert.IsTrue((bool)Prop(controller, "Valid"), "geçerli hücre (yeşil)");
        Assert.IsTrue((bool)controller.GetType().GetMethod("Confirm").Invoke(controller, null), "onay");
        Assert.IsFalse((bool)Prop(controller, "IsPlacing"), "mod kapanır");
        Assert.AreEqual(1500 - 40, (int)Field(island0, "wood"), "Ev 40 Odun");
        Assert.IsTrue((bool)T("IslandEconomy").GetProperty("BuilderBusy").GetValue(null), "Ada Ustası meşgul");
        var house = FindEntryById(island0, "House#1");
        Assert.NotNull(house); Assert.AreEqual(0, (int)Field(house, "level"), "inşaat alanı");

        // Usta meşgulken ikinci bina / yükseltme reddedilir
        var args = new object[] { "House#1", false, null };
        Assert.IsFalse((bool)T("IslandEconomy").GetMethod("TryStartUpgrade").Invoke(null, args));

        // 5 sn sonra bina tamamlanır
        SetClock(t0 + 6 * TimeSpan.TicksPerSecond);
        T("IslandEconomy").GetMethod("Tick").Invoke(null, new object[] { t0 + 6 * TimeSpan.TicksPerSecond, true });
        yield return null;
        Assert.AreEqual(1, (int)Field(house, "level"), "5 sn sonra Ev Sv1");
        Assert.IsFalse((bool)T("IslandEconomy").GetProperty("BuilderBusy").GetValue(null));
        int liveAfter = ((System.Collections.ICollection)T("IslandBuilding").GetProperty("All").GetValue(null)).Count;
        Assert.AreEqual(liveBefore + 1, liveAfter, "tamamlanan bina savaş yapısı olur");
        Assert.AreEqual(6, (int)Call("IslandEconomy", "PawnPool", Placed(island0)), "ikinci Ev +2 piyon");

        // --- Yükseltme: Ağıl Sv1 → Sv2 (Odun), elmasla bitir ---
        var pasture = FindEntry(island0, "Pasture");
        string pastureId = (string)Field(pasture, "id");
        int woodBefore = (int)Field(island0, "wood");
        args = new object[] { pastureId, false, null };
        Assert.IsTrue((bool)T("IslandEconomy").GetMethod("TryStartUpgrade").Invoke(null, args), (string)args[2]);
        Assert.AreEqual(woodBefore - (int)Isl("UpgradeWood", Kind("Pasture"), 2), (int)Field(island0, "wood"));
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        Set(Prop(gm, "PlayerData"), "gems", 50);
        var finishArgs = new object[] { null };
        Assert.IsTrue((bool)T("IslandEconomy").GetMethod("TryFinishNow").Invoke(null, finishArgs), (string)finishArgs[0]);
        yield return null;
        Assert.AreEqual(2, (int)Field(pasture, "level"), "Ağıl Sv2");

        // --- Üretim, yığın ve toplama ---
        var lumber = FindEntry(island0, "Lumber");
        Set(lumber, "stock", (float)Call("IslandEconomy", "Capacity", lumber));
        Set(lumber, "lastTick", T("IslandRepair").GetProperty("Now").GetValue(null));
        village.GetType().GetMethod("RefreshPiles").Invoke(village, new object[] { true });
        Assert.AreEqual(6, (int)village.GetType().GetMethod("PileShown").Invoke(village, new object[] { (string)Field(lumber, "id") }), "dolu depo 6 kütük gösterir");
        int woodNow = (int)Field(island0, "wood");
        int collected = (int)Call("IslandEconomy", "CollectFrom", lumber);
        Assert.Greater(collected, 200);
        Assert.AreEqual(woodNow + collected, (int)Field(island0, "wood"));
        Assert.Less(Stock(lumber), 1f);

        // --- Bölge keşfi: bedel + süre, bulutlar 1,5 sn'de dağılır ---
        args = new object[] { 1, false, null };
        Assert.IsTrue((bool)T("IslandEconomy").GetMethod("TryStartZone").Invoke(null, args), (string)args[2]);
        long tz = DateTime.UtcNow.Ticks + 40 * TimeSpan.TicksPerSecond;
        SetClock(tz);
        T("IslandEconomy").GetMethod("Tick").Invoke(null, new object[] { tz, true });
        Assert.AreEqual(3, (int)Field(island0, "zoneMask") & 3, "Z1 açık");
        yield return new WaitForSecondsRealtime(0.5f);
        var clouds = UnityEngine.Object.FindObjectsByType(T("IslandZoneCloud"), FindObjectsInactive.Include, FindObjectsSortMode.None);
        var zone1 = clouds.Where(cloud => (int)Field(cloud, "zone") == 1).ToList();
        Assert.IsNotEmpty(zone1);
        Assert.IsTrue(zone1.Any(cloud => (bool)Prop(cloud, "Dissolving")), "bulutlar dağılıyor");
        yield return new WaitForSecondsRealtime(1.6f);
        Assert.IsTrue(zone1.All(cloud => !((Component)cloud).gameObject.activeSelf), "Z1 bulutları dağıldı");
        Assert.IsTrue(clouds.Where(cloud => (int)Field(cloud, "zone") == 2).All(cloud => ((Component)cloud).gameObject.activeSelf), "Z2 bulutları yerinde");
    }

    /// <summary>12 Kasım 2026: orta çayır Z1/Z2'ye bölündü (önce birer ev sığıyordu); hazır köy Z0'da kalır.</summary>
    [Test]
    public void MeadowSplitGivesZoneOneAndTwoRoomAndKeepsVillageInZoneZero()
    {
        var island = NewIsland(15);
        Placed(island).Clear();
        int Count(string kind, int zone)
        {
            int n = 0;
            for (int c = 0; c < 22; c++)
                for (int r = 0; r < 36; r++)
                    if ((int)Call("IslandLayout", "ZoneAt", c, r) == zone && Check(kind, c, r, island, 63, null, 15) == "None") n++;
            return n;
        }
        Assert.GreaterOrEqual(Count("House", 2), 5, "Z2: birden çok ev yeri");
        Assert.GreaterOrEqual(Count("House", 1), 3, "Z1: en az 3 ev yeri (önce 1)");
        Assert.GreaterOrEqual(Count("House", 0), 15, "Z0 hâlâ geniş");
        var spots = (Array)T("IslandLayout").GetField("Buildings").GetValue(null);
        foreach (var spot in spots)
        {
            string kind = Field(spot, "kind").ToString();
            if (kind == "Tower") continue;   // adacık kuleleri kendi bölgelerinde
            int col = (int)Field(spot, "col"), row = (int)Field(spot, "row"), w = (int)Field(spot, "w"), h = (int)Field(spot, "h");
            for (int c = col; c < col + w; c++)
                for (int r = row; r < row + h; r++)
                    Assert.AreEqual(0, (int)Call("IslandLayout", "ZoneAt", c, r), $"hazır köy {kind} Z0'da kalır");
        }
    }

    [Test]
    public void OldSaveBuildingInNewlyLockedZoneOpensThatZoneForFree()
    {
        var island = NewIsland(3);
        Set(island, "zoneMask", 1);
        var placed = Placed(island);
        placed.Clear();
        // Hazır köy kuleleri (kimlikte @) adacıklarda durur ama bölge açmaz.
        placed.Add(Entry("Tower@19,10", "Tower", 1, 19, 10));
        Assert.IsFalse((bool)Call("IslandEconomy", "OpenZonesUnderBuildings", island));
        Assert.AreEqual(1, (int)Field(island, "zoneMask"));
        // Eskiden Z0 olan orta çayırın batısına kurulmuş Ev → Z2 bedava açılır.
        placed.Add(Entry("House#4", "House", 1, 5, 16));
        Assert.AreEqual(2, (int)Call("IslandLayout", "ZoneAt", 5, 16));
        Assert.IsTrue((bool)Call("IslandEconomy", "OpenZonesUnderBuildings", island));
        Assert.AreEqual(1 | 4, (int)Field(island, "zoneMask"), "Z2 açıldı, Z1 kapalı kaldı");
        Assert.IsFalse((bool)Call("IslandEconomy", "OpenZonesUnderBuildings", island), "ikinci kez değişmez");
    }

    internal static object FindEntryById(object island, string id)
    {
        foreach (var entry in Placed(island)) if ((string)Field(entry, "id") == id) return entry;
        return null;
    }
}
