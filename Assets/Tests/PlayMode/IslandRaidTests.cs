using System;
using System.Collections;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz D, zamanlı baskın (island_base_v2 §7.8, §10 D): %20 hırsızlık tavanı, 4 sa üretim sınırı, gece kayması, birikmeme,
/// 24 sa+ saat durması, kule kapsaması, canlı zafer ödülü ve eski kayıt göçü. Yansıma ile (Assembly-CSharp); saat enjekte edilir.
/// </summary>
public class IslandRaidTests
{
    private const long Hour = TimeSpan.TicksPerHour;
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");

    private static Type Sched => IslandPhaseBTests.T("IslandRaidScheduler");
    private static object Isl(string name) => IslandPhaseBTests.T("Progression+Island").GetField(name).GetValue(null);

    private string savedKey, savedBackup, savedFile;
    private object oldToLocal, oldKeep, oldArcher;
    private bool devWas;

    [SetUp]
    public void Setup()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
        oldToLocal = Sched.GetField("ToLocal").GetValue(null);
        oldKeep = Sched.GetField("KeepOf").GetValue(null);
        oldArcher = Sched.GetField("ArcherOf").GetValue(null);
        SetLocalOffset(0);
        IslandPhaseBTests.SetClock(new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks);
    }

    [TearDown]
    public void TearDown()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        Sched.GetField("ToLocal").SetValue(null, oldToLocal);
        Sched.GetField("KeepOf").SetValue(null, oldKeep);
        Sched.GetField("ArcherOf").SetValue(null, oldArcher);
        Sched.GetField("LiveFightActive").SetValue(null, false);
        IslandPhaseBTests.T("IslandSession").GetMethod("Clear").Invoke(null, null);
        IslandPhaseBTests.T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        IslandPhaseBTests.T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        Time.timeScale = 1f;
    }

    // ==================== YARDIMCILAR ====================

    private static void SetLocalOffset(int hours)
    {
        Func<DateTime, DateTime> f = utc => utc.AddHours(hours);
        Sched.GetField("ToLocal").SetValue(null, f);
    }

    private static void SetFixedLevel(string field, int value)
    {
        var f = Sched.GetField(field);
        var ft = f.FieldType;
        var p = Expression.Parameter(ft.GetGenericArguments()[0]);
        f.SetValue(null, Expression.Lambda(ft, Expression.Constant(value), p).Compile());
    }

    private static object SeededIsland(int keep, int highest, long now)
    {
        IslandPhaseBTests.SetClock(now);
        var island = IslandPhaseBTests.NewIsland(keep);
        IslandPhaseBTests.Set(island, "highestAkin", highest);
        IslandPhaseBTests.Call("IslandEconomy", "EnsureSeeded", island, keep);
        foreach (var entry in IslandPhaseBTests.Placed(island)) IslandPhaseBTests.Set(entry, "lastTick", now);
        SetFixedLevel("KeepOf", keep);
        SetFixedLevel("ArcherOf", keep);
        return island;
    }

    private static void FillStocks(object island, float ofCapacity)
    {
        foreach (var entry in IslandPhaseBTests.Placed(island))
        {
            string kind = (string)IslandPhaseBTests.Field(entry, "kind");
            if (kind == "Lumber" || kind == "Pasture")
                IslandPhaseBTests.Set(entry, "stock", (float)IslandPhaseBTests.Call("IslandEconomy", "Capacity", entry) * ofCapacity);
        }
    }

    private static (float wood, float meat) Stocks(object island)
    {
        float wood = 0f, meat = 0f;
        foreach (var entry in IslandPhaseBTests.Placed(island))
        {
            float stock = (float)IslandPhaseBTests.Field(entry, "stock");
            string kind = (string)IslandPhaseBTests.Field(entry, "kind");
            if (kind == "Lumber") wood += stock; else if (kind == "Pasture") meat += stock;
        }
        return (wood, meat);
    }

    private static object Call(string method, params object[] args) => Sched.GetMethod(method).Invoke(null, args);
    private static long Long(object island, string field) => (long)IslandPhaseBTests.Field(island, field);
    private static int Int(object island, string field) => (int)IslandPhaseBTests.Field(island, field);
    private static DateTime Utc(long ticks) => new DateTime(ticks, DateTimeKind.Utc);

    // ==================== %20 TAVAN ====================

    [Test]
    public void TheftIsNeverAboveTwentyPercentEvenWithZeroDefense()
    {
        Assert.AreEqual(0.20f, (float)Isl("TheftCap"), 1e-6f, "kullanıcı kararı: %20");
        for (int keep = 1; keep <= 15; keep++)
        {
            float r = 6f * keep;
            Assert.AreEqual(0.20f, (float)Call("TheftFraction", 0f, r), 1e-6f, $"D=0 → tam tavan (k={keep})");
            foreach (float d in new[] { -5f, 0f, 1f, r * 0.5f, r, r * 3f })
                Assert.That((float)Call("TheftFraction", d, r), Is.InRange(0f, 0.20f), $"k={keep} D={d}");
            Assert.AreEqual(0f, (float)Call("TheftFraction", r, r), 1e-6f, "D ≥ R: hiçbir şey çalınmaz");
        }
        // Savunmasız Assess (kule yok, Okçu/KK 0): çalınan oran ≤ %20
        var island = SeededIsland(5, 8, new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks);
        var empty = IslandPhaseBTests.NewIsland(5);
        var none = Call("Assess", empty, 0, 0, 0, 0, false);
        Assert.LessOrEqual((float)IslandPhaseBTests.Field(none, "fraction"), 0.20f + 1e-6f);

        // Gerçek çalma: tam depo, her yoldan en çok %20 (Fare sayısı sınırsız bile olsa)
        FillStocks(island, 1f);
        var before = Stocks(island);
        var stolen = Call("ApplyTheft", island, 5f);   // 500 % istenirse de tavana kırpılır
        int wood = (int)stolen.GetType().GetField("Item1").GetValue(stolen), meat = (int)stolen.GetType().GetField("Item2").GetValue(stolen);
        Assert.LessOrEqual(wood, before.wood * 0.20f + 0.001f);
        Assert.LessOrEqual(meat, before.meat * 0.20f + 0.001f);

        FillStocks(island, 1f);
        before = Stocks(island);
        long now = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        IslandPhaseBTests.Set(island, "nextRaidTicks", now);
        var lost = Call("CompleteLive", island, false, 999, now);
        Assert.LessOrEqual((float)IslandPhaseBTests.Field(lost, "fraction"), 0.20f + 1e-6f, "yenilgi + 999 Fare yine ≤ %20");
        var after = Stocks(island);
        Assert.GreaterOrEqual(after.wood, before.wood * 0.80f - 1f);
        Assert.GreaterOrEqual(after.meat, before.meat * 0.80f - 1f);
    }

    [Test]
    public void OnlyFourHoursOfUncollectedProductionCanBeStolen()
    {
        long now = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        var island = SeededIsland(5, 8, now);
        FillStocks(island, 1f);   // 8 sa dolu depo
        var lumber = IslandPhaseBTests.FindEntry(island, "Lumber");
        var pasture = IslandPhaseBTests.FindEntry(island, "Pasture");
        float lumberRate = (float)IslandPhaseBTests.Call("IslandEconomy", "FullRate", lumber);
        float pastureRate = (float)IslandPhaseBTests.Call("IslandEconomy", "FullRate", pasture);
        Assert.AreEqual(8f * lumberRate, (float)IslandPhaseBTests.Field(lumber, "stock"), 0.01f, "depo 8 sa dolu");
        var stolen = Call("ApplyTheft", island, 0.20f);
        int wood = (int)stolen.GetType().GetField("Item1").GetValue(stolen), meat = (int)stolen.GetType().GetField("Item2").GetValue(stolen);
        Assert.AreEqual(Mathf.FloorToInt(0.20f * 4f * lumberRate), wood, "Odun: yalnız 4 saatlik üretimin %20'si");
        Assert.AreEqual(Mathf.FloorToInt(0.20f * 4f * pastureRate), meat, "Et: yalnız 4 saatlik üretimin %20'si");
        // Az stok: stoğun kendisi sınırdır
        foreach (var entry in IslandPhaseBTests.Placed(island)) IslandPhaseBTests.Set(entry, "stock", 10f);
        var small = Call("ApplyTheft", island, 0.20f);
        Assert.AreEqual(2, (int)small.GetType().GetField("Item1").GetValue(small), "10 stoktan %20 = 2");
        // Stok 0 ise çalınacak şey yok
        foreach (var entry in IslandPhaseBTests.Placed(island)) IslandPhaseBTests.Set(entry, "stock", 0f);
        var none = Call("ApplyTheft", island, 0.20f);
        Assert.AreEqual(0, (int)none.GetType().GetField("Item1").GetValue(none) + (int)none.GetType().GetField("Item2").GetValue(none));
    }

    // ==================== ZAMANLAMA ====================

    [Test]
    public void ArrivalsNeverLandInTheNightAndKeepTheEightHourRhythm()
    {
        foreach (int offset in new[] { 0, 3, -5 })
        {
            SetLocalOffset(offset);
            var island = IslandPhaseBTests.NewIsland(5);
            IslandPhaseBTests.Set(island, "highestAkin", 5);
            long start = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc).Ticks;
            int shifted = 0;
            for (int minute = 0; minute < 3 * 24 * 60; minute += 37)
            {
                long from = start + minute * TimeSpan.TicksPerMinute;
                Call("Schedule", island, from);
                long arrival = Long(island, "nextRaidTicks");
                var local = Utc(arrival).AddHours(offset);
                Assert.That(local.Hour, Is.InRange(9, 21), $"varış gece olmamalı: {local:HH:mm} (ofset {offset})");
                Assert.GreaterOrEqual(arrival - from, 8 * Hour - 45 * TimeSpan.TicksPerMinute - TimeSpan.TicksPerSecond, "en az 8 sa − 45 dk");
                if (arrival - from > 8 * Hour + 45 * TimeSpan.TicksPerMinute + TimeSpan.TicksPerSecond) shifted++;
                Assert.That((int)IslandPhaseBTests.Field(island, "raidBeach"), Is.InRange(0, 1), "Akın 5: yalnız A ve B açık");
            }
            Assert.Greater(shifted, 0, "gece düşen varışlar gerçekten kaydırıldı (ofset " + offset + ")");
        }
    }

    [Test]
    public void FirstRaidComesOnlyAfterAkinFiveAndPendingRaidDoesNotAccumulate()
    {
        long t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        var island = IslandPhaseBTests.NewIsland(5);
        IslandPhaseBTests.Set(island, "highestAkin", 4);
        Call("Tick", island, t0);
        Assert.AreEqual(0L, Long(island, "nextRaidTicks"), "Akın 4: baskın yok");
        IslandPhaseBTests.Set(island, "highestAkin", 5);
        Call("Tick", island, t0 + Hour);
        Assert.Greater(Long(island, "nextRaidTicks"), t0 + Hour, "Akın 5 kazanıldı: ilk baskın planlandı");
        Assert.AreEqual(0, Int(island, "raidsResolved"));

        // 48 sa hiç açılmadı: kapalıyken biriken baskınlar yok, en çok 1 çözüm
        long back = t0 + Hour + 48 * Hour;
        Call("Tick", island, back);
        Assert.AreEqual(1, Int(island, "raidsResolved"), "48 sa yokluk: tek çözüm");
        Call("Tick", island, back + TimeSpan.TicksPerMinute);
        Call("Tick", island, back + 5 * TimeSpan.TicksPerMinute);
        Assert.AreEqual(1, Int(island, "raidsResolved"), "art arda Tick ikinci çözüm üretmez");
        Assert.Greater(Long(island, "nextRaidTicks"), back, "sonraki baskın şimdiden sonra planlanır");
        Assert.AreNotEqual("", (string)IslandPhaseBTests.Field(island, "raidReportKind"), "rapor bekliyor");

        // Gün boyu saat başı çalışan oyun: baskınlar 8 sa ritmine yakın ve hiçbir an iki baskın bekleyemez
        long t = back; int resolved0 = Int(island, "raidsResolved");
        for (int h = 1; h <= 72; h++)
        {
            t += Hour;
            Call("Tick", island, t);
            Assert.That((int)Sched.GetMethod("PhaseAt").Invoke(null, new object[] { island, t }), Is.InRange(0, 2));
        }
        int perThreeDays = Int(island, "raidsResolved") - resolved0;
        Assert.That(perThreeDays, Is.InRange(3, 9), "72 sa'te ≈ 72 / (8 + 2 pencere) baskın");
    }

    [Test]
    public void RaidClockStopsWhenTheGameStaysClosedForMoreThanADay()
    {
        long t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        var island = IslandPhaseBTests.NewIsland(5);
        IslandPhaseBTests.Set(island, "highestAkin", 6);
        IslandPhaseBTests.Set(island, "raidSeenTicks", t0);
        long planned = t0 + 26 * Hour;                    // 26 sa sonra (11:00 yerel)
        IslandPhaseBTests.Set(island, "nextRaidTicks", planned);
        // 36 sa sonra dönüldü: saat durmasaydı pencere (26–28 sa) çoktan bitmiş ve çözülmüş olurdu
        Call("Tick", island, t0 + 36 * Hour);
        Assert.AreEqual(0, Int(island, "raidsResolved"), "saat 24 sa'ten sonra durdu: baskın hâlâ gelecekte");
        Assert.GreaterOrEqual(Long(island, "nextRaidTicks"), planned + 12 * Hour, "36 − 24 = 12 sa öteleme");
        Assert.Greater(Long(island, "nextRaidTicks"), t0 + 36 * Hour);
        // Kontrol: 20 sa kapalı kalma (sınırın altı) saati durdurmaz
        var island2 = IslandPhaseBTests.NewIsland(5);
        IslandPhaseBTests.Set(island2, "highestAkin", 6);
        IslandPhaseBTests.Set(island2, "raidSeenTicks", t0);
        IslandPhaseBTests.Set(island2, "nextRaidTicks", t0 + 10 * Hour);
        Call("Tick", island2, t0 + 20 * Hour);
        Assert.AreEqual(1, Int(island2, "raidsResolved"), "20 sa kapalı: normal çevrimdışı çözüm");
    }

    // ==================== KAPSAMA ====================

    [Test]
    public void ATowerThatCoversTheTargetLaneStealsLessThanOneThatDoesNot()
    {
        long t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        var empty = IslandPhaseBTests.NewIsland(5);
        int coverCol = -1, coverRow = -1, farCol = -1, farRow = -1;
        float range = (float)Isl("TowerRange");
        for (int c = 0; c < 22; c++)
            for (int r = 0; r < 36; r++)
            {
                var cell = new Vector2(c + 1f, r + 1f);   // 2×2 kule ayak izi merkezi
                var world = new Vector2(cell.x, 36 - cell.y);
                bool covers = (bool)Call("TowerCoversBeach", world, range, 0);
                if (covers && coverCol < 0) { coverCol = c; coverRow = r; }
                if (!covers && farCol < 0) { farCol = c; farRow = r; }
            }
        Assert.GreaterOrEqual(coverCol, 0, "A kumsalını kapsayan hücre var");
        Assert.GreaterOrEqual(farCol, 0, "A kumsalını kapsamayan hücre var");

        float Fraction(int col, int row, int level, out int covering)
        {
            var island = IslandPhaseBTests.NewIsland(5);
            IslandPhaseBTests.Placed(island).Add(IslandPhaseBTests.Entry("Tower#1", "Tower", level, col, row));
            var assess = Call("Assess", island, 0, 5, 5, 0, false);
            covering = (int)IslandPhaseBTests.Field(assess, "covering");
            return (float)IslandPhaseBTests.Field(assess, "fraction");
        }
        float covered = Fraction(coverCol, coverRow, 3, out int coveringA);
        float uncovered = Fraction(farCol, farRow, 3, out int coveringB);
        Assert.AreEqual(1, coveringA); Assert.AreEqual(0, coveringB);
        Assert.Less(covered, uncovered, "kapsayan kule daha az çaldırır");
        Assert.LessOrEqual(uncovered, 0.20f);
        // Aynı kule, kapsarken 2·L, kapsamazken 1·L (kısmi) katkı: D farkı = 2·3·0,5 = 3
        var a1 = IslandPhaseBTests.NewIsland(5); IslandPhaseBTests.Placed(a1).Add(IslandPhaseBTests.Entry("Tower#1", "Tower", 3, coverCol, coverRow));
        var a2 = IslandPhaseBTests.NewIsland(5); IslandPhaseBTests.Placed(a2).Add(IslandPhaseBTests.Entry("Tower#1", "Tower", 3, farCol, farRow));
        float d1 = (float)IslandPhaseBTests.Field(Call("Assess", a1, 0, 5, 5, 0, false), "defense");
        float d2 = (float)IslandPhaseBTests.Field(Call("Assess", a2, 0, 5, 5, 0, false), "defense");
        Assert.AreEqual(3f, d1 - d2, 1e-4f);
        Assert.AreEqual(5f + 5f + 6f, d1, 1e-4f, "D = Okçu 5 + KK 5 + kule 2·3·1");
        Assert.AreEqual(30f, (float)IslandPhaseBTests.Field(Call("Assess", a1, 0, 5, 5, 0, false), "required"), 1e-4f, "R = 6k");
        // İnşaattaki (Sv0) kule sayılmaz
        var a3 = IslandPhaseBTests.NewIsland(5); IslandPhaseBTests.Placed(a3).Add(IslandPhaseBTests.Entry("Tower#1", "Tower", 0, coverCol, coverRow));
        Assert.AreEqual(10f, (float)IslandPhaseBTests.Field(Call("Assess", a3, 0, 5, 5, 0, false), "defense"), 1e-4f);

        // Çevrimdışı çözümde de iki durum ayrışır (rapor "N kuleye takıldı")
        long Resolve(int col, int row, out int covering)
        {
            var island = SeededIsland(5, 8, t0);
            IslandPhaseBTests.Placed(island).Clear();
            IslandPhaseBTests.Set(island, "villageSeeded", true);
            IslandPhaseBTests.Placed(island).Add(IslandPhaseBTests.Entry("Tower#1", "Tower", 3, col, row));
            var lumber = IslandPhaseBTests.Entry("Lumber#1", "Lumber", 5, 8, 14);
            IslandPhaseBTests.Set(lumber, "lastTick", t0); IslandPhaseBTests.Set(lumber, "stock", 300f);
            IslandPhaseBTests.Placed(island).Add(lumber);
            IslandPhaseBTests.Set(island, "raidBeach", 0);
            IslandPhaseBTests.Set(island, "nextRaidTicks", t0 - 3 * Hour);
            IslandPhaseBTests.Set(island, "raidSeenTicks", t0);
            Call("Tick", island, t0);
            covering = Int(island, "raidReportTowers");
            Assert.AreEqual("offline", (string)IslandPhaseBTests.Field(island, "raidReportKind"));
            return Int(island, "raidReportWood");
        }
        long stolenCovered = Resolve(coverCol, coverRow, out int c1);
        long stolenFar = Resolve(farCol, farRow, out int c2);
        Assert.AreEqual(1, c1); Assert.AreEqual(0, c2);
        Assert.LessOrEqual(stolenCovered, stolenFar);
        Assert.Greater(stolenFar, 0, "Odun yığını stoktan çalındı");
    }

    // ==================== CANLI SAVUNMA SONUCU ====================

    [Test]
    public void LiveVictoryAddsTenPercentAndGoldLossHalvesTheTheftAndRatsAreCapped()
    {
        long t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        int keep = 5;
        // Zafer: çalıntı yok, bekleyen üretime +%10, 10 + 2k altın
        var island = SeededIsland(keep, 8, t0);
        foreach (var entry in IslandPhaseBTests.Placed(island))
        {
            string kind = (string)IslandPhaseBTests.Field(entry, "kind");
            if (kind == "Lumber" || kind == "Pasture") IslandPhaseBTests.Set(entry, "stock", 50f);
        }
        IslandPhaseBTests.Set(island, "nextRaidTicks", t0 - Hour);
        var won = Call("CompleteLive", island, true, 0, t0);
        Assert.AreEqual(10 + 2 * keep, (int)IslandPhaseBTests.Field(won, "gold"), "10 + 2k altın");
        Assert.AreEqual(0, (int)IslandPhaseBTests.Field(won, "wood") + (int)IslandPhaseBTests.Field(won, "meat"));
        Assert.AreEqual(55f, (float)IslandPhaseBTests.Field(IslandPhaseBTests.FindEntry(island, "Lumber"), "stock"), 0.01f, "+%10");
        Assert.AreEqual("won", (string)IslandPhaseBTests.Field(island, "raidReportKind"));
        Assert.AreEqual(1, Int(island, "raidsResolved"));
        Assert.Greater(Long(island, "nextRaidTicks"), t0, "sonraki baskın planlandı");

        // Fareler: her kaçak %2, toplam tavan %20
        var rats = SeededIsland(keep, 8, t0); FillStocks(rats, 0.5f);
        var three = Call("CompleteLive", rats, true, 3, t0);
        Assert.AreEqual(0.06f, (float)IslandPhaseBTests.Field(three, "fraction"), 1e-5f, "3 Fare = %6");
        var ratsMany = SeededIsland(keep, 8, t0); FillStocks(ratsMany, 0.5f);
        var many = Call("CompleteLive", ratsMany, true, 40, t0);
        Assert.AreEqual(0.20f, (float)IslandPhaseBTests.Field(many, "fraction"), 1e-5f, "40 Fare yine %20");

        // Yenilgi: çevrimdışı çalıntının yarısı (Fare yok), altın yok
        var lose = SeededIsland(keep, 8, t0); FillStocks(lose, 0.5f);
        float offlineFraction = (float)IslandPhaseBTests.Field(Call("Assess", lose, 0, keep, keep, 0, false), "fraction");
        var lost = Call("CompleteLive", lose, false, 0, t0);
        Assert.AreEqual(offlineFraction * 0.5f, (float)IslandPhaseBTests.Field(lost, "fraction"), 1e-5f);
        Assert.AreEqual(0, (int)IslandPhaseBTests.Field(lost, "gold"));
        Assert.AreEqual("lost", (string)IslandPhaseBTests.Field(lose, "raidReportKind"));

        // Rapor satırı örneği
        IslandPhaseBTests.Set(island, "raidReportKind", "offline");
        IslandPhaseBTests.Set(island, "raidReportBeach", 1); IslandPhaseBTests.Set(island, "raidReportTowers", 2);
        IslandPhaseBTests.Set(island, "raidReportWood", 0); IslandPhaseBTests.Set(island, "raidReportMeat", 60);
        string line = (string)Call("ReportText", island);
        StringAssert.Contains("B", line); StringAssert.Contains("60", line);
        Assert.AreEqual(line, (string)Call("TakeReport", island));
        Assert.AreEqual("", (string)Call("TakeReport", island), "rapor bir kez verilir");
    }

    // ==================== ESKİ KAYIT ====================

    [Test]
    public void OldSaveWithoutRaidFieldsOpensSafelyAndSchedulesTheFirstRaid()
    {
        T("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = IslandPhaseBTests.NewSave();
        var island = IslandPhaseBTests.Field(save, "island");
        IslandPhaseBTests.Set(island, "keepLevel", 6); IslandPhaseBTests.Set(island, "highestAkin", 6);
        IslandPhaseBTests.Set(island, "wood", 222);
        string json = JsonUtility.ToJson(save);
        string stripped = Regex.Replace(json, @",""raidBeach"":-?\d+,""raidSeed"":-?\d+,""raidSerial"":-?\d+,""raidsResolved"":-?\d+,""raidSeenTicks"":-?\d+,""raidReportKind"":""[^""]*"",""raidReportBeach"":-?\d+,""raidReportTowers"":-?\d+,""raidReportWood"":-?\d+,""raidReportMeat"":-?\d+", "");
        StringAssert.DoesNotContain("raidSeed", stripped, "Faz D alanları çıkarıldı (eski kayıt)");
        Assert.AreNotEqual(json, stripped);
        PlayerPrefs.SetString(Key, stripped);
        PlayerPrefs.Save();

        var loaded = T("SaveSystem").GetMethod("Load").Invoke(null, null);
        Assert.NotNull(loaded, "eski kayıt açılmalı");
        var back = IslandPhaseBTests.Field(loaded, "island");
        Assert.AreEqual(222, Int(back, "wood"), "diğer veriler korunur");
        Assert.AreEqual(0L, Long(back, "nextRaidTicks")); Assert.AreEqual(0, Int(back, "raidsResolved"));
        Assert.AreEqual(0L, Long(back, "raidSeenTicks"));
        Assert.AreEqual("", (string)IslandPhaseBTests.Field(back, "raidReportKind"));
        long now = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc).Ticks;
        Assert.DoesNotThrow(() => Call("Tick", back, now));
        Assert.Greater(Long(back, "nextRaidTicks"), now, "eski kaydın ilk Tick'inde baskın planlanır");
        Assert.AreEqual(0, (int)Sched.GetMethod("PhaseAt").Invoke(null, new object[] { back, now }), "hemen baskın yok (Sakin)");

        // Yeni alanlar gidiş-dönüş
        Call("DevBring", back, now);
        var again = JsonUtility.FromJson(JsonUtility.ToJson(back), back.GetType());
        Assert.AreEqual(Long(back, "nextRaidTicks"), Long(again, "nextRaidTicks"));
        Assert.AreEqual(Int(back, "raidBeach"), Int(again, "raidBeach"));
    }

    private static Type T(string name) => IslandPhaseBTests.T(name);

    // ==================== SAHNEDE: ROZET + CANLI SAVUNMA ====================

    [UnityTest, Timeout(400000)]
    public IEnumerator LiveRaidInTheSceneUsesRaidPowerAndPaysOutOrStealsWithinTheCap()
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "highestAkin", 8);
        }, 8, v => devWas = v);
        long t0 = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc).Ticks;
        IslandPhaseBTests.SetClock(t0);
        var island0 = IslandPhaseBTests.CurrentIsland();
        FillStocks(island0, 0.5f);
        foreach (var entry in IslandPhaseBTests.Placed(island0)) IslandPhaseBTests.Set(entry, "lastTick", t0);
        var raid = IslandPhaseBTests.Prop(IslandPhaseBTests.Setup, "Raid");
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        var player = IslandPhaseBTests.Prop(gm, "PlayerData");

        // Sakin: pencere kapalı, savunma başlamaz; rozet metni "Sakin · …"
        Sched.GetMethod("Tick").Invoke(null, new object[] { island0, t0 });
        string calm = (string)T("IslandHud").GetMethod("RaidBadgeText").Invoke(null, new object[] { island0, t0 });
        StringAssert.StartsWith(L("Sakin", "Calm"), calm);
        Assert.IsFalse((bool)raid.GetType().GetMethod("StartRaid").Invoke(raid, null), "pencere yokken savunma başlamaz");
        // Dev "BASKINI GETİR": T−30 dk
        Call("DevBring", island0, t0);
        StringAssert.StartsWith(L("Baskın geliyor", "Raid incoming"), (string)T("IslandHud").GetMethod("RaidBadgeText").Invoke(null, new object[] { island0, t0 }));
        Assert.IsFalse((bool)raid.GetType().GetMethod("StartRaid").Invoke(raid, null), "T−30'da savunma henüz başlamaz");
        // T0 geçti: pencere açık
        long live = t0 + 31 * TimeSpan.TicksPerMinute;
        IslandPhaseBTests.SetClock(live);
        StringAssert.StartsWith(L("BASKIN!", "RAID!"), (string)T("IslandHud").GetMethod("RaidBadgeText").Invoke(null, new object[] { island0, live }));
        long gold0 = (long)(int)IslandPhaseBTests.Field(player, "gold");
        var stocksBefore = Stocks(island0);
        int highestBefore = Int(island0, "highestAkin");
        int lastAkinBefore = Int(island0, "lastAkin");

        T("IslandBot").GetMethod("Attach").Invoke(null, new object[] { IslandPhaseBTests.Setup, Enum.Parse(T("IslandBot+Profile"), "Good") });
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartRaid").Invoke(raid, null));
        Assert.IsTrue((bool)IslandPhaseBTests.Prop(raid, "IsRaid"));
        Assert.AreEqual(6, (int)IslandPhaseBTests.Prop(raid, "Akin"), "güç = en yüksek Akın − 2");
        Assert.AreEqual(2, (int)IslandPhaseBTests.Prop(raid, "TotalWaves"), "2 dalga");
        Assert.AreEqual(Int(island0, "raidBeach"), (int)IslandPhaseBTests.Prop(raid, "RaidBeach"));
        Time.timeScale = 6f;
        float start = Time.realtimeSinceStartup;
        while (IslandPhaseBTests.Prop(raid, "Phase").ToString() == "Running" && Time.realtimeSinceStartup - start < 180f) yield return null;
        Time.timeScale = 1f;
        string phase = IslandPhaseBTests.Prop(raid, "Phase").ToString();
        Assert.That(phase, Is.EqualTo("Won").Or.EqualTo("Lost"), "baskın bitti");
        Assert.AreEqual(1, Int(island0, "raidsResolved"));
        Assert.AreEqual(highestBefore, Int(island0, "highestAkin"), "baskın Akın ilerlemesini değiştirmez");
        Assert.AreEqual(lastAkinBefore, Int(island0, "lastAkin"));
        var outcome = IslandPhaseBTests.Prop(raid, "LastRaidOutcome");
        Assert.LessOrEqual((float)IslandPhaseBTests.Field(outcome, "fraction"), 0.20f + 1e-6f);
        Assert.Greater(Long(island0, "nextRaidTicks"), live, "sonraki baskın planlandı");
        if (phase == "Won")
        {
            int keep = (int)T("IslandSession").GetMethod("KeepLevel").Invoke(null, null);
            Assert.AreEqual(10 + 2 * keep, (int)IslandPhaseBTests.Field(outcome, "gold"));
            Assert.AreEqual(gold0 + 10 + 2 * keep, (long)(int)IslandPhaseBTests.Field(player, "gold"), "10 + 2k Ana Üs altını");
            Assert.AreEqual("won", (string)IslandPhaseBTests.Field(island0, "raidReportKind"));
        }
        else Assert.AreEqual("lost", (string)IslandPhaseBTests.Field(island0, "raidReportKind"));
        UnityEngine.Debug.Log($"[IslandRaid] canlı baskın sonucu: {phase}, çalınan {IslandPhaseBTests.Field(outcome, "wood")} Odun / {IslandPhaseBTests.Field(outcome, "meat")} Et, " +
            $"stok önce {stocksBefore.wood:0}/{stocksBefore.meat:0}");
    }

    private static string L(string tr, string en) => (string)T("L").GetMethod("T", new[] { typeof(string), typeof(string) }).Invoke(null, new object[] { tr, en });
}
