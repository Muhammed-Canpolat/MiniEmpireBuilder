using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz E1 (island_base_v2 §5, §6, §7.5, §20): Kışla + bayrak (yerleştirme, yükseltme, Lancer'lar savaşta), Manastır (şifa, onarım ×0,5),
/// Altın Damarı (günlük miktar, depo tavanı, Ana Üs altın etkisi ≤%10, toplama) ve Z3–Z5 keşfi (bedel, süre, bulut, kilit, KK şartı). Yansıma ile.
/// </summary>
public class IslandPhaseE1Tests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");

    private static Type T(string name) => IslandPhaseBTests.T(name);
    private static object Isl(string name) => T("Progression+Island").GetField(name).GetValue(null);
    private static object IslCall(string method, params object[] args) => T("Progression+Island").GetMethod(method).Invoke(null, args);
    private static object Call(string type, string method, params object[] args) => IslandPhaseBTests.Call(type, method, args);
    private static object Field(object target, string name) => IslandPhaseBTests.Field(target, name);
    private static void Set(object target, string name, object value) => IslandPhaseBTests.Set(target, name, value);
    private static object Prop(object target, string name) => IslandPhaseBTests.Prop(target, name);
    private static object Kind(string name) => IslandPhaseBTests.Kind(name);

    private string savedKey, savedBackup, savedFile;
    private bool devWas;

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
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    // ==================== SAYILAR (§5, §6, §7.5, §8) ====================

    [Test]
    public void FormulasMatchTheDesignDocument()
    {
        // Kışla: kurulum 150/40, yükseltme çarpanı 0,35/0,25, KK3'te 1, can 120 + 30(L−1)
        Assert.AreEqual((150, 40), ((int, int))IslCall("PlaceCost", Kind("Barracks")));
        Assert.AreEqual((400, 80), ((int, int))IslCall("PlaceCost", Kind("Monastery")));
        Assert.AreEqual(0, (int)IslCall("MaxCount", Kind("Barracks"), 2)); Assert.AreEqual(1, (int)IslCall("MaxCount", Kind("Barracks"), 3));
        Assert.AreEqual(0, (int)IslCall("MaxCount", Kind("Monastery"), 5)); Assert.AreEqual(1, (int)IslCall("MaxCount", Kind("Monastery"), 6));
        Assert.AreEqual(1, (int)IslCall("MaxCount", Kind("Barracks"), 15)); Assert.AreEqual(1, (int)IslCall("MaxCount", Kind("Monastery"), 15));
        Assert.AreEqual(120f, (float)IslCall("StructureHp", "Barracks", 1), 1e-3f);
        Assert.AreEqual(210f, (float)IslCall("StructureHp", "Barracks", 4), 1e-3f);
        Assert.AreEqual(90f, (float)IslCall("StructureHp", "Monastery", 1), 1e-3f);
        Assert.AreEqual(165f, (float)IslCall("StructureHp", "Monastery", 4), 1e-3f);
        // KK5 L5 yükseltme: W[5] = 380 → Kışla 0,35 → 130, 0,25 → 100 (10'a yuvarlı); Manastır 0,4 → 150, 0,15 → 60
        Assert.AreEqual(130, (int)IslCall("UpgradeWood", Kind("Barracks"), 5)); Assert.AreEqual(100, (int)IslCall("UpgradeMeat", Kind("Barracks"), 5));
        Assert.AreEqual(150, (int)IslCall("UpgradeWood", Kind("Monastery"), 5)); Assert.AreEqual(60, (int)IslCall("UpgradeMeat", Kind("Monastery"), 5));
        // Lancer: 1 + ⌊L/5⌋ adet, can 70·(1+0,15(L−1)), hasar 6·(…), 12 sn yeniden doğuş
        Assert.AreEqual(1, (int)IslCall("LancerCount", 1)); Assert.AreEqual(1, (int)IslCall("LancerCount", 4));
        Assert.AreEqual(2, (int)IslCall("LancerCount", 5)); Assert.AreEqual(3, (int)IslCall("LancerCount", 10)); Assert.AreEqual(4, (int)IslCall("LancerCount", 15));
        Assert.AreEqual(70f, (float)IslCall("LancerHp", 1), 1e-3f); Assert.AreEqual(112f, (float)IslCall("LancerHp", 5), 1e-3f);
        Assert.AreEqual(6f, (float)IslCall("LancerDamage", 1), 1e-3f); Assert.AreEqual(9.6f, (float)IslCall("LancerDamage", 5), 1e-3f);
        Assert.AreEqual(12f, (float)Isl("LancerRespawn")); Assert.AreEqual(1f, (float)Isl("LancerHitInterval"));
        // Manastır Monk: 4 sn'de bir, 3 u içine %8; onarım ×0,5 (yalnız bekleme süresi)
        Assert.AreEqual(0.08f, (float)Isl("MonkHealFraction")); Assert.AreEqual(4f, (float)Isl("MonkHealInterval")); Assert.AreEqual(3f, (float)Isl("MonkHealRadius"));
        Assert.AreEqual(35f, (float)IslCall("RepairSeconds", 0.5f, false, true), 1e-3f, "(20 + 100·0,5) × 0,5");
        Assert.AreEqual(70f, (float)IslCall("RepairSeconds", 0.5f, false, false), 1e-3f);
        // Altın Damarı: 6 + 3(k−1)
        Assert.AreEqual(15f, (float)IslCall("GoldVeinPerHour", 4)); Assert.AreEqual(18f, (float)IslCall("GoldVeinPerHour", 5)); Assert.AreEqual(33f, (float)IslCall("GoldVeinPerHour", 10));
        // Z3–Z5 bedelleri (§4, §8)
        Assert.AreEqual(new[] { 0, 150, 300, 500, 800, 1500 }, (int[])Isl("ZoneWood"));
        Assert.AreEqual(new[] { 0, 0, 30, 50, 80, 150 }, (int[])Isl("ZoneMeat"));
        Assert.AreEqual(new[] { 0, 30, 180, 300, 900, 3600 }, (int[])Isl("ZoneSeconds"));
        Assert.AreEqual(5, (int)Isl("LastRevealableZone"));
        for (int z = 1; z <= 5; z++) Assert.IsTrue((bool)IslCall("ZoneRevealable", z));
        Assert.IsFalse((bool)IslCall("ZoneRevealable", 6));
    }

    // ==================== ALTIN DAMARI: GÜNLÜK MİKTAR VE ≤%10 ====================

    [Test]
    public void GoldVeinAccumulatesOnlyWhenZoneThreeIsOpenAndCapsAtEightHours()
    {
        long t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        var island = IslandPhaseBTests.NewIsland(5);
        Call("IslandEconomy", "AccumulateVein", island, t0);
        Assert.AreEqual(0f, (float)Field(island, "veinStock")); Assert.AreEqual(0L, (long)Field(island, "veinTick"), "Z3 kapalıyken damar çalışmaz");
        Set(island, "zoneMask", 1 | (1 << 3));
        Call("IslandEconomy", "AccumulateVein", island, t0);
        Assert.AreEqual(t0, (long)Field(island, "veinTick"), "açılınca saat başlar");
        Call("IslandEconomy", "AccumulateVein", island, t0 + 6 * IslandPhaseBTests.Hour);
        Assert.AreEqual(18f * 6f, (float)Field(island, "veinStock"), 1e-3f, "KK5: 18 altın/sa × 6 sa");
        Call("IslandEconomy", "AccumulateVein", island, t0 + 40 * IslandPhaseBTests.Hour);
        Assert.AreEqual(18f * 8f, (float)Field(island, "veinStock"), 1e-3f, "depo tavanı 8 sa (çevrimdışı 40 sa)");
        // Kıyı Kalesi yükselince oran da yükselir (KK10 → 33/sa)
        var island10 = IslandPhaseBTests.NewIsland(10);
        Set(island10, "zoneMask", 1 | (1 << 3));
        Call("IslandEconomy", "AccumulateVein", island10, t0);
        Call("IslandEconomy", "AccumulateVein", island10, t0 + 4 * IslandPhaseBTests.Hour);
        Assert.AreEqual(33f * 4f, (float)Field(island10, "veinStock"), 1e-3f);
    }

    /// <summary>
    /// §6/§10 E doğrulaması: adadan Ana Üs'e giden altın ≤ %10. Varsayım (ekonomi simülasyonuyla aynı): günde ≈16 sa yakalanan üretim
    /// (iki toplama × 8 sa depo); Ana Üs günlük altını belge değerleri KK5 ≈3.500, KK10 ≈6.600, arası/üstü doğrusal.
    /// </summary>
    [Test]
    public void GoldVeinStaysUnderTenPercentOfTheMainBaseDailyGold()
    {
        var log = new System.Text.StringBuilder("[IslandVein] KK: damar/gün (16 sa) · Ana Üs/gün · oran | en kötü 24 sa: ");
        for (int keep = 5; keep <= 15; keep++)
        {
            float rate = (float)IslCall("GoldVeinPerHour", keep);
            float daily = rate * 16f, worst = rate * 24f;
            float mainBase = 3500f + (keep - 5) * 620f;   // KK10 = 6.600
            Assert.LessOrEqual(daily / mainBase, 0.10f, $"KK{keep}: damar {daily:0}/gün, Ana Üs {mainBase:0}");
            Assert.Greater(daily / mainBase, 0.04f, $"KK{keep}: damar görünür olmalı (≥%4), oran {daily / mainBase:P1}");
            log.Append($"KK{keep}: {daily:0}/{mainBase:0}={daily / mainBase:P1} ({worst / mainBase:P1})  ");
        }
        Debug.Log(log.ToString());
        // KK5 belge örneği: 18/sa ≈ 288/gün ≈ %8,2
        Assert.AreEqual(288f, 18f * 16f); Assert.AreEqual(528f, 33f * 16f);
    }

    // ==================== ORTA ÇAYIR (Z5) +%15 Et ====================

    [Test]
    public void MiddleMeadowAddsFifteenPercentToPastureOutput()
    {
        var island = IslandPhaseBTests.NewIsland(10);
        var pasture = IslandPhaseBTests.Entry("Pasture@12,18", "Pasture", 5, 12, 18);
        IslandPhaseBTests.Placed(island).Add(pasture);
        float Rate(int mask) => (float)Call("IslandEconomy", "Rate", pasture, 1, mask, IslandPhaseBTests.Placed(island));
        Assert.AreEqual(1.15f, Rate(1 | (1 << 5)) / Rate(1), 1e-4f, "Z5 açık: Ağıl +%15");
        Assert.AreEqual(1f, Rate(1 | (1 << 4)) / Rate(1), 1e-4f, "Z4 Ağıl'ı etkilemez");
        var lumber = IslandPhaseBTests.Entry("Lumber#1", "Lumber", 5, 8, 14);
        IslandPhaseBTests.Placed(island).Add(lumber);
        Assert.AreEqual((float)Call("IslandEconomy", "Rate", lumber, 2, 1, IslandPhaseBTests.Placed(island)),
            (float)Call("IslandEconomy", "Rate", lumber, 2, 1 | (1 << 5), IslandPhaseBTests.Placed(island)), 1e-4f, "Oduncu etkilenmez");
    }

    // ==================== YERLEŞTİRME: KIŞLA 3×2, MANASTIR 3×3 ====================

    [Test]
    public void BarracksAndMonasteryHaveTheirFootprintsRulesAndLimits()
    {
        var island = IslandPhaseBTests.NewIsland(15);
        Call("IslandEconomy", "EnsureSeeded", island, 15);
        Assert.AreEqual(new Vector2Int(3, 2), (Vector2Int)Call("IslandGrid", "Size", Kind("Barracks")));
        Assert.AreEqual(new Vector2Int(3, 3), (Vector2Int)Call("IslandGrid", "Size", Kind("Monastery")));
        Assert.IsTrue(IslandPhaseBTests.FindCell("Barracks", "None", island, 63, out int bc, out int br, 15), "geçerli Kışla hücresi");
        Assert.IsTrue(IslandPhaseBTests.FindCell("Monastery", "None", island, 63, out int mc, out int mr, 15), "geçerli Manastır hücresi");
        // Kışla zirveye (yükseklik 2) kurulmaz
        Assert.IsTrue(IslandPhaseBTests.FindCell("Barracks", "Height", island, 63, out _, out _, 15), "Kışla zirvede 'Height'");
        // KK şartı: Kışla KK3'ten, Manastır KK6'dan önce adet 0 → Limit
        Assert.AreEqual("Limit", IslandPhaseBTests.Check("Barracks", bc, br, island, 63, null, 2));
        Assert.AreEqual("None", IslandPhaseBTests.Check("Barracks", bc, br, island, 63, null, 3));
        Assert.AreEqual("Limit", IslandPhaseBTests.Check("Monastery", mc, mr, island, 63, null, 5));
        Assert.AreEqual("None", IslandPhaseBTests.Check("Monastery", mc, mr, island, 63, null, 6));
        // Kilitli bölgede kurulmaz (maske yalnız Z0)
        Assert.IsTrue(IslandPhaseBTests.FindCell("Barracks", "Locked", island, 1, out _, out _, 15), "kilitli bölgede Kışla yok");
        // Kurulunca ikincisi Limit; ayak izi çakışması Overlap
        IslandPhaseBTests.Placed(island).Add(IslandPhaseBTests.Entry("Barracks#1", "Barracks", 1, bc, br));
        Assert.IsTrue(IslandPhaseBTests.FindCell("Barracks", "Limit", island, 63, out _, out _, 15), "tek Kışla");
        Assert.IsTrue(IslandPhaseBTests.FindCell("Monastery", "Overlap", island, 63, out _, out _, 15), "3×3 Manastır Kışla'nın üstüne gelemez");
        // Yer bolluğu (bilgi + denetim): bölge başına geçerli sol-üst hücre sayısı; Kışla kurulduktan sonra Manastır'a yer kalmalı
        var counts = new System.Text.StringBuilder("[IslandGrid] E1 geçerli hücre (KK15, boş köy üstünde, tüm bölgeler açık): ");
        var empty = IslandPhaseBTests.NewIsland(15);
        Call("IslandEconomy", "EnsureSeeded", empty, 15);
        foreach (var kind in new[] { "Barracks", "Monastery" })
        {
            var perZone = new int[6];
            for (int cc = 0; cc < 22; cc++)
                for (int rr = 0; rr < 36; rr++)
                    if (IslandPhaseBTests.Check(kind, cc, rr, empty, 63, null, 15) == "None") perZone[(int)Call("IslandLayout", "ZoneAt", cc, rr)]++;
            counts.Append($"{kind}[Z0 {perZone[0]}, Z1 {perZone[1]}, Z2 {perZone[2]}, Z3 {perZone[3]}, Z4 {perZone[4]}, Z5 {perZone[5]}]  ");
            Assert.Greater(perZone.Sum(), 0, $"{kind} için yer var");
        }
        Debug.Log(counts.ToString());
        // İkisi birlikte kurulabilmeli: Z0'daki tek Manastır hücresi + başka bir Kışla hücresi (Z0 ya da Z2); Z0 tek başına yetmezse Z2 keşfi gerekir
        bool PairExists(int mask)
        {
            for (int mcc = 0; mcc < 22; mcc++)
                for (int mrr = 0; mrr < 36; mrr++)
                {
                    if (IslandPhaseBTests.Check("Monastery", mcc, mrr, empty, mask, null, 15) != "None") continue;
                    var withMonastery = IslandPhaseBTests.NewIsland(15);
                    foreach (var e in IslandPhaseBTests.Placed(empty)) IslandPhaseBTests.Placed(withMonastery).Add(e);
                    IslandPhaseBTests.Placed(withMonastery).Add(IslandPhaseBTests.Entry("Monastery#1", "Monastery", 1, mcc, mrr));
                    if (IslandPhaseBTests.FindCell("Barracks", "None", withMonastery, mask, out _, out _, 15)) return true;
                }
            return false;
        }
        bool z0Pair = PairExists(1), z2Pair = PairExists(7);
        Debug.Log($"[IslandGrid] E1 Kışla+Manastır birlikte: yalnız Z0 {z0Pair}, Z0+Z1+Z2 {z2Pair}");
        Assert.IsTrue(z2Pair, "Z1/Z2 açıkken Kışla ve Manastır birlikte kurulabilir");
        // Taşıma adet saymaz
        Assert.AreEqual("None", IslandPhaseBTests.Check("Barracks", bc, br, island, 63, "Barracks#1", 15));
    }

    // ==================== KAYIT: YENİ ALANLAR ====================

    [Test]
    public void FlagAndVeinFieldsRoundTripAndOldSavesOpenWithDefaults()
    {
        T("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = IslandPhaseBTests.NewSave();
        var island = Field(save, "island");
        Set(island, "keepLevel", 6); Set(island, "zoneMask", 1 | (1 << 3) | (1 << 5));
        Set(island, "flagStair", 2); Set(island, "veinStock", 77.5f); Set(island, "veinTick", 636_000_000_000_000_000L);
        IslandPhaseBTests.Placed(island).Add(IslandPhaseBTests.Entry("Barracks#2", "Barracks", 3, 5, 10));
        IslandPhaseBTests.Placed(island).Add(IslandPhaseBTests.Entry("Monastery#3", "Monastery", 2, 8, 11));
        Set(island, "villageSeeded", true);
        T("SaveSystem").GetMethod("Save").Invoke(null, new[] { save });
        var back = Field(T("SaveSystem").GetMethod("Load").Invoke(null, null), "island");
        Assert.AreEqual(2, (int)Field(back, "flagStair"));
        Assert.AreEqual(77.5f, (float)Field(back, "veinStock"), 1e-3f);
        Assert.AreEqual(636_000_000_000_000_000L, (long)Field(back, "veinTick"));
        var placed = IslandPhaseBTests.Placed(back);
        Assert.AreEqual(2, placed.Count);
        Assert.AreEqual("Barracks", Field(placed[0], "kind")); Assert.AreEqual("Monastery", Field(placed[1], "kind"));
        Assert.AreEqual(Kind("Barracks"), Call("IslandGrid", "ParseKind", "Barracks"));
        Assert.AreEqual(Kind("Monastery"), Call("IslandGrid", "ParseKind", "Monastery"));

        // Faz D (v8) kaydı: Faz E1 alanları yok → varsayılanlarla açılır (bayrak S1, damar boş)
        string json = JsonUtility.ToJson(save);
        string stripped = Regex.Replace(json, @",""flagStair"":-?\d+,""veinStock"":[-0-9.eE+]+,""veinTick"":-?\d+", "");
        StringAssert.DoesNotContain("flagStair", stripped);
        StringAssert.DoesNotContain("veinStock", stripped);
        Assert.AreNotEqual(json, stripped);
        T("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        PlayerPrefs.SetString(Key, stripped); PlayerPrefs.Save();
        var old = T("SaveSystem").GetMethod("Load").Invoke(null, null);
        Assert.NotNull(old, "eski kayıt açılmalı");
        var oldIsland = Field(old, "island");
        Assert.AreEqual(0, (int)Field(oldIsland, "flagStair"), "varsayılan bayrak S1");
        Assert.AreEqual(0f, (float)Field(oldIsland, "veinStock"));
        Assert.AreEqual(0L, (long)Field(oldIsland, "veinTick"));
        Assert.AreEqual(6, (int)Field(oldIsland, "keepLevel"), "diğer alanlar korunur");
        Assert.AreEqual(2, IslandPhaseBTests.Placed(oldIsland).Count);
    }

    // ==================== SAHNE: KIŞLA + BAYRAK + LANCER'LAR ====================

    private static object AddBuilding(object island, string kind, int level, int nearCol, int nearRow, string id)
    {
        Call("IslandEconomy", "EnsureSeeded", island, 6);
        Assert.IsTrue(IslandPhaseBTests.FindCellNear(kind, island, 1, nearCol, nearRow, out int c, out int r, 6), $"{kind} için geçerli hücre");
        var entry = IslandPhaseBTests.Entry(id, kind, level, c, r);
        Set(entry, "lastTick", DateTime.UtcNow.Ticks); Set(entry, "workers", -1);
        IslandPhaseBTests.Placed(island).Add(entry);
        return entry;
    }

    private static IEnumerator SpawnTestEnemy(Vector2 at, Vector2 castleAt, Action<Component> got, string type = "Raider")
    {
        var castle = (Component)Prop(IslandPhaseBTests.Setup, "CastleStructure");
        var spawn = T("IslandEnemy").GetMethod("Spawn");
        var route = new[] { at, castleAt };
        var enemy = (Component)spawn.Invoke(null, new object[] { Enum.Parse(T("EnemyType"), type), 1, castle.transform, at, route, 0, false, false });
        got(enemy);
        yield return null;
    }

    private static void ClearBoats()
    {
        var all = (System.Collections.IEnumerable)T("RaidBoat").GetProperty("All").GetValue(null);
        foreach (var boat in all.Cast<Component>().ToList()) if (boat != null) UnityEngine.Object.Destroy(boat.gameObject);
    }

    [UnityTest]
    public IEnumerator BarracksPlacedFlagMovedAndLancersHoldTheEnemyAtTheFlag()
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            Set(island, "highestAkin", 3);
            AddBuilding(island, "Barracks", 5, 11, 12, "Barracks#1");
        }, 6, v => devWas = v);
        var island0 = IslandPhaseBTests.CurrentIsland();
        var village = Prop(IslandPhaseBTests.Setup, "Village");
        var flag = Prop(village, "Flag");
        Assert.NotNull(flag, "tamamlanmış Kışla varsa bayrak durur");
        Assert.AreEqual(0, (int)Prop(flag, "Stair"), "varsayılan S1");
        int live = ((System.Collections.ICollection)T("IslandBuilding").GetProperty("All").GetValue(null)).Count;
        Assert.AreEqual(8, live, "Kale + Ev, Oduncu, Okçuluk, Ağıl, 2 Kule + Kışla (her biri savaş yapısı)");
        // Bayrak sıradaki merdivene taşınır, kayıtta kalır
        Assert.AreEqual(1, (int)village.GetType().GetMethod("CycleFlag").Invoke(village, null));
        Assert.AreEqual(1, (int)Field(island0, "flagStair"));
        Assert.AreEqual(2, (int)village.GetType().GetMethod("CycleFlag").Invoke(village, null));
        Assert.AreEqual(0, (int)village.GetType().GetMethod("CycleFlag").Invoke(village, null), "S3 → S1");
        var points = (Vector2[])T("IslandLayout").GetField("FlagPoints").GetValue(null);
        Assert.IsTrue((bool)flag.GetType().GetMethod("Hit").Invoke(flag, new object[] { points[0] + Vector2.up * 0.9f }), "bayrağa dokunuş");
        Assert.IsFalse((bool)flag.GetType().GetMethod("Hit").Invoke(flag, new object[] { points[1] + Vector2.up * 0.9f }), "başka merdivene dokunuş bayrak değil");

        // Savaş: 1 + ⌊5/5⌋ = 2 Lancer
        var raid = Prop(IslandPhaseBTests.Setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 3 }));
        ClearBoats();
        Time.timeScale = 3f;
        yield return new WaitForSecondsRealtime(0.5f);
        var lancers = (System.Collections.IList)T("IslandLancer").GetField("All").GetValue(null);
        Assert.AreEqual(2, lancers.Count, "Kışla Sv5: 2 Lancer");
        float hp = (float)Prop(lancers[0], "MaxHp");
        Assert.AreEqual(112f, hp, 1e-3f);
        yield return new WaitForSecondsRealtime(1.8f);   // bayrağa koşu (6 oyun sn)
        var flagPoint = points[0];
        foreach (var lancer in lancers.Cast<Component>())
            Assert.Less(Vector2.Distance(lancer.transform.position, flagPoint + Vector2.up * 0.59f), 1.4f, "Lancer bayrakta toplandı");

        // Bayrak çevresine düşman: Lancer'lar vurur, düşman Lancer'ı hedefler
        Component enemy = null;
        var castleCenter = (Vector2)T("IslandLayout").GetProperty("CastleCenter").GetValue(null);
        yield return SpawnTestEnemy(flagPoint + new Vector2(2.2f, 0.2f), castleCenter, e => enemy = e);
        var controller = Prop(enemy, "Controller");
        float maxHp = (float)Prop(controller, "MaxHealth");
        bool targetedLancer = false;
        float deadline = Time.realtimeSinceStartup + 4f;
        while (enemy != null && Time.realtimeSinceStartup < deadline)
        {
            var target = (Transform)Prop(controller, "CurrentTarget");
            if (target != null && target.GetComponent(T("IslandLancer")) != null) targetedLancer = true;
            yield return null;
        }
        Assert.IsTrue(enemy == null || (float)Prop(controller, "CurrentHealth") < maxHp, "Lancer'lar düşmanı vurdu");
        Assert.IsTrue(targetedLancer, "yakın dövüşçü şeritteki Lancer'ı hedef aldı (§7.5)");
        Assert.Greater((float)T("IslandStats").GetField("LancerDamageDealt").GetValue(null), 0f);

        // Ölen Lancer 12 sn sonra yeniden doğar
        var first = (Component)lancers[0];
        first.GetType().GetMethod("TakeDamage").Invoke(first, new object[] { 100000f });
        yield return null; yield return null;
        var barracks = UnityEngine.Object.FindObjectsByType(T("IslandBarracks"), FindObjectsSortMode.None)[0];
        Assert.AreEqual(1, (int)Prop(barracks, "Alive"), "bir Lancer düştü");
        yield return new WaitForSecondsRealtime(4.6f);   // 13,8 oyun sn
        Assert.AreEqual(2, (int)Prop(barracks, "Alive"), "12 sn sonra yeniden doğdu");

        // Savaş bitince Lancer yok
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.AreEqual(0, ((System.Collections.IList)T("IslandLancer").GetField("All").GetValue(null)).Count, "ada görünümünde Lancer yok");
    }

    [UnityTest]
    public IEnumerator FlyersCannotBeHeldAndMeleeEnemiesIgnoreTheFlagFarAway()
    {
        yield return IslandPhaseBTests.LoadIsland(island => AddBuilding(island, "Barracks", 1, 11, 12, "Barracks#1"), 6, v => devWas = v);
        var raid = Prop(IslandPhaseBTests.Setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 });
        ClearBoats();
        Time.timeScale = 3f;
        yield return new WaitForSecondsRealtime(2f);
        var lancers = (System.Collections.IList)T("IslandLancer").GetField("All").GetValue(null);
        Assert.AreEqual(1, lancers.Count, "Sv1: tek Lancer");
        var flagPoint = ((Vector2[])T("IslandLayout").GetField("FlagPoints").GetValue(null))[0];
        // Uçan (Yarasa) tutulmaz: Lancer'ın hedefi boş kalır
        Component bat = null;
        yield return SpawnTestEnemy(flagPoint + new Vector2(1.5f, 0f), flagPoint + new Vector2(3f, -4f), e => bat = e, "Bat");
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.IsNull(Prop(lancers[0], "Target"), "Lancer uçanı vuramaz");
        // Bayraktan çok uzaktaki (LancerEngageRadius dışı) düşman tutulmaz
        UnityEngine.Object.Destroy(bat.gameObject);
        Component far = null;
        yield return SpawnTestEnemy(flagPoint + new Vector2(-9f, 6f), flagPoint + new Vector2(-9f, 3f), e => far = e);
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.IsNull(Prop(lancers[0], "Target"), "bayrak çevresi dışındaki düşman tutulmaz");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    // ==================== SAHNE: MANASTIR ====================

    [UnityTest]
    public IEnumerator MonasteryHealsNearbyBuildingsHalvesRepairAndKeepsItsMonk()
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            Set(island, "highestAkin", 3);
            AddBuilding(island, "Monastery", 3, 11, 12, "Monastery#1");
        }, 6, v => devWas = v);
        var monasteryType = T("IslandMonastery");
        var monastery = (Component)UnityEngine.Object.FindFirstObjectByType(monasteryType);
        Assert.NotNull(monastery, "Manastır binası şifa bileşeni taşır");
        Assert.NotNull(GameObject.Find("Monk"), "Monk kapıda durur (ada görünümünde de)");
        var building = monastery.GetComponent(T("IslandBuilding"));
        var structure = (Component)Prop(building, "Structure");
        float max = (float)Prop(structure, "MaxHealth");
        Assert.AreEqual(90f + 25f * 2f, max, 1e-3f, "Manastır Sv3 canı 90 + 25·2");

        var raid = Prop(IslandPhaseBTests.Setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 3 });
        ClearBoats();
        yield return new WaitForSecondsRealtime(0.3f);
        structure.GetType().GetMethod("TakeDamage").Invoke(structure, new object[] { max * 0.5f });
        float before = (float)Prop(structure, "CurrentHealth");
        int healed = (int)monasteryType.GetMethod("Pulse").Invoke(monastery, null);
        Assert.GreaterOrEqual(healed, 1, "yakındaki hasarlı bina iyileşti");
        Assert.AreEqual(before + max * 0.08f, (float)Prop(structure, "CurrentHealth"), 1e-2f, "%8 can");
        // Zamanlı vuruş: 4 sn'de bir kendiliğinden
        int pulses = (int)Prop(monastery, "Pulses");
        Time.timeScale = 3f;
        yield return new WaitForSecondsRealtime(2.2f);   // 6,6 oyun sn
        Assert.Greater((int)Prop(monastery, "Pulses"), pulses, "Monk 4 sn'de bir şifa verir");
        Assert.Greater((float)Prop(structure, "CurrentHealth"), before + max * 0.08f - 0.01f);
        // 3 u dışındaki bina etkilenmez
        foreach (var other in ((System.Collections.IEnumerable)T("IslandBuilding").GetProperty("All").GetValue(null)).Cast<Component>())
        {
            if (other == building || (bool)Prop(other, "IsCastle")) continue;
            var otherStructure = (Component)Prop(other, "Structure");
            if (Vector2.Distance((Vector2)Prop(other, "Center"), (Vector2)Prop(building, "Center")) <= 3f + (float)Prop(other, "Radius") + 0.01f) continue;
            otherStructure.GetType().GetMethod("TakeDamage").Invoke(otherStructure, new object[] { 10f });
            float otherBefore = (float)Prop(otherStructure, "CurrentHealth");
            monasteryType.GetMethod("Pulse").Invoke(monastery, null);
            Assert.AreEqual(otherBefore, (float)Prop(otherStructure, "CurrentHealth"), 1e-3f, "menzil dışı bina iyileşmez");
            break;
        }

        // Onarım ×0,5: savaş sonrası yarı canlı Kule (20 + 100·0,5) × 0,5 = 35 sn
        var tower = ((System.Collections.IEnumerable)T("IslandBuilding").GetProperty("All").GetValue(null)).Cast<Component>()
            .First(b => (bool)Prop(b, "IsTower"));
        var towerStructure = (Component)Prop(tower, "Structure");
        float towerMax = (float)Prop(towerStructure, "MaxHealth");
        towerStructure.GetType().GetMethod("TakeDamage").Invoke(towerStructure, new object[] { towerMax * 0.5f });
        Time.timeScale = 1f;
        T("IslandRepair").GetMethod("CommitBattle").Invoke(null, null);
        float seconds = (float)Prop(tower, "RepairSecondsLeft");
        Assert.AreEqual(35f, seconds, 3f, $"Manastır varken onarım ×0,5 (≈35 sn, bulundu {seconds:0})");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    // ==================== SAHNE: ALTIN DAMARI ====================

    [UnityTest]
    public IEnumerator GoldVeinCollectsIntoMainBaseGoldAndShowsAPile()
    {
        long t0 = DateTime.UtcNow.Ticks;
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            Set(island, "zoneMask", 1 | (1 << 3));
            Set(island, "veinTick", t0 - 6 * IslandPhaseBTests.Hour);
        }, 5, v => devWas = v);
        IslandPhaseBTests.SetClock(t0);
        var island0 = IslandPhaseBTests.CurrentIsland();
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        var player = Prop(gm, "PlayerData");
        int goldBefore = (int)Field(player, "gold");
        var village = Prop(IslandPhaseBTests.Setup, "Village");
        village.GetType().GetMethod("RefreshPiles").Invoke(village, new object[] { true });
        // Sahne açılırken Tick vein'i işledi; 6 sa × 18 = 108 → 4 simge (108/144·6 = 4,5 → 5)
        Assert.Greater((int)Prop(village, "VeinPileShown"), 0, "damar yığını görünür");
        int collected = (int)Call("IslandEconomy", "CollectVein");
        Assert.That(collected, Is.InRange(105, 112), $"KK5: 6 sa ≈ 108 altın, toplanan {collected}");
        Assert.AreEqual(goldBefore + collected, (int)Field(player, "gold"), "altın Ana Üs'e eklendi");
        Assert.Less((float)Field(island0, "veinStock"), 1f, "stok boşaldı");
        village.GetType().GetMethod("RefreshPiles").Invoke(village, new object[] { true });
        Assert.AreEqual(0, (int)Prop(village, "VeinPileShown"), "boş damarda yığın yok");
        // Madenci piyon Z3 açıkken sahnede
        var miners = UnityEngine.Object.FindObjectsByType(T("IslandPawn"), FindObjectsSortMode.None)
            .Cast<Component>().Where(p => Prop(p, "Kind").ToString() == "Miner").ToList();
        Assert.AreEqual(1, miners.Count, "Z3 açıkken madenci altın taşını kazar");
    }

    // ==================== SAHNE: Z3–Z5 KEŞFİ ====================

    [UnityTest]
    public IEnumerator ZonesThreeToFiveRevealThroughTheBuilderWithCostTimeCloudsAndLocks()
    {
        long t0 = DateTime.UtcNow.Ticks;
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            Set(island, "wood", 6000); Set(island, "meat", 1000); Set(island, "highestAkin", 3);
        }, 5, v => devWas = v);
        IslandPhaseBTests.SetClock(t0);
        var island0 = IslandPhaseBTests.CurrentIsland();
        var economy = T("IslandEconomy");
        string Reason(int zone) => (string)economy.GetMethod("ZoneBlockedReason").Invoke(null, new object[] { zone });
        // KK şartı: Z5 için Kıyı Kalesi 6 gerekir; Z3 (KK4) ve Z4 (KK5) açılabilir
        Assert.IsNull(Reason(3)); Assert.IsNull(Reason(4));
        StringAssert.Contains("6", Reason(5), "Z5 KK6 ister");
        Assert.IsNotNull(Reason(5));
        Set(island0, "keepLevel", 6);
        Assert.IsNull(Reason(5));

        var clouds = UnityEngine.Object.FindObjectsByType(T("IslandZoneCloud"), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().ToList();
        long now = t0;
        int[] wood = (int[])Isl("ZoneWood"), meat = (int[])Isl("ZoneMeat"), seconds = (int[])Isl("ZoneSeconds");
        foreach (int zone in new[] { 3, 4, 5 })
        {
            Assert.IsTrue(clouds.Any(c => (int)Field(c, "zone") == zone), $"Z{zone} bulutu var");
            // Kilitliyken bölgedeki kule hücresi geçersiz, açılınca geçerli
            // Z3 (Altın Burnu) yalnız Altın Damarı + P2 getirir: zirve şeritli/doğa dolu, kule hücresi yok (belge §4 "güvenli ödül")
            bool hasCell = FindZoneTowerCell(island0, zone, out int tc, out int tr);
            Assert.AreEqual(zone != 3, hasCell, $"Z{zone}: yeni bina yeri {(zone == 3 ? "yok (yalnız damar)" : "var")}");
            if (zone == 3) Assert.IsFalse((bool)economy.GetMethod("VeinOpen").Invoke(null, new object[] { island0 }), "damar Z3 açılmadan çalışmaz");
            int woodBefore = (int)Field(island0, "wood"), meatBefore = (int)Field(island0, "meat");
            var args = new object[] { zone, false, null };
            Assert.IsTrue((bool)economy.GetMethod("TryStartZone").Invoke(null, args), $"Z{zone} başlatıldı: {args[2]}");
            Assert.AreEqual(woodBefore - wood[zone], (int)Field(island0, "wood"), $"Z{zone} Odun bedeli");
            Assert.AreEqual(meatBefore - meat[zone], (int)Field(island0, "meat"), $"Z{zone} Et bedeli");
            var task = Field(island0, "task");
            Assert.AreEqual("zone", (string)Field(task, "kind")); Assert.AreEqual(zone, (int)Field(task, "zone"));
            Assert.AreEqual(seconds[zone] * TimeSpan.TicksPerSecond, (long)Field(task, "endTicks") - (long)Field(task, "startTicks"), $"Z{zone} süresi");
            Assert.IsTrue((bool)economy.GetProperty("BuilderBusy").GetValue(null));
            Assert.AreEqual(0, (int)Field(island0, "zoneMask") & (1 << zone), $"Z{zone} süre dolmadan kapalı");
            // Usta meşgulken başka bölge reddedilir
            Assert.IsNotNull(Reason(zone == 5 ? 3 : zone + 1));

            now = t0 + (long)(seconds[zone] + 2) * TimeSpan.TicksPerSecond + (zone - 3) * 4 * IslandPhaseBTests.Hour;
            IslandPhaseBTests.SetClock(now);
            economy.GetMethod("Tick").Invoke(null, new object[] { now, true });
            yield return null;
            Assert.AreEqual(1 << zone, (int)Field(island0, "zoneMask") & (1 << zone), $"Z{zone} açıldı");
            Assert.IsFalse((bool)economy.GetProperty("BuilderBusy").GetValue(null));
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.IsTrue(clouds.Where(c => (int)Field(c, "zone") == zone).All(c => !c.gameObject.activeSelf), $"Z{zone} bulutları dağıldı");
            if (hasCell) Assert.AreEqual("None", IslandPhaseBTests.Check("Tower", tc, tr, island0, (int)Field(island0, "zoneMask"), null, 6), $"Z{zone} açılınca kule hücresi geçerli");
            if (zone == 3)
            {
                Assert.IsTrue((bool)economy.GetMethod("VeinOpen").Invoke(null, new object[] { island0 }), "Z3 açılınca Altın Damarı çalışır");
                Assert.AreEqual(now, (long)Field(island0, "veinTick"), 0, "damar saati görev bitişinden başlar");
            }
        }
        Assert.AreEqual(1, (int)Field(island0, "zoneMask") & 7, "Z1/Z2 etkilenmedi: kapalı kaldı");
        Assert.AreEqual(1 | (1 << 3) | (1 << 4) | (1 << 5), (int)Field(island0, "zoneMask"));
    }

    private static bool FindZoneTowerCell(object island, int zone, out int col, out int row)
    {
        for (int c = 0; c < 21; c++)
            for (int r = 0; r < 35; r++)
            {
                if ((int)Call("IslandLayout", "ZoneAt", c, r) != zone) continue;
                if (IslandPhaseBTests.Check("Tower", c, r, island, 1, null, 6) != "Locked") continue;
                if (IslandPhaseBTests.Check("Tower", c, r, island, 1 | (1 << zone), null, 6) != "None") continue;
                col = c; row = r; return true;
            }
        col = row = -1;
        return false;
    }
}
