using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Faz B ekonomi simülasyonu (island_base_v2 §6, §11): sıradan oyuncu günde 3 oturum açar, topladığını yükseltmeye harcar.
/// Gerçek <c>IslandEconomy</c> kodu (üretim, depo tavanı, Usta, bedeller) sanal saatle sürülür. Ana Üs altını varsayımdır
/// (≈3.500/gün gün 1–3, gün 7'de ≈5.000). Sonuç bir aralık denetimidir (kesin sayı değil) ve günlüğe yazılır.
/// </summary>
public class IslandEconomySimTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;

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
        IslandPhaseBTests.T("IslandSession").GetMethod("Clear").Invoke(null, null);
        IslandPhaseBTests.T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
    }

    private static object Eco(string method, params object[] args) => IslandPhaseBTests.T("IslandEconomy").GetMethod(method).Invoke(null, args);
    private static object Isl(string method, params object[] args) => IslandPhaseBTests.Isl(method, args);

    private sealed class Day
    {
        public int number, cardMeat, productionMeat, akinMeat, giftMeat, productionWood, akinWood, meatSpent, woodSpent, pastureLevel, lumberLevel, houses;
        public float MeatIncome => productionMeat + akinMeat;
        public float Gold => 3500f;   // KK5 ≈ Ana Üs 5: ≈3.500/gün (§6); gün 7'de de aynı (adanın Ana Üs seviyesi artmadan)
    }

    private IEnumerator Simulate(string profile, double[] day1Sessions, double[] otherSessions, List<Day> days, StringBuilder log)
    {
        // GameManager (Start kayıtlı oyunu yükler: bir kare bekle), sonra yalnız ada kaydı kurulur
        var managerType = IslandPhaseBTests.T("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = IslandPhaseBTests.NewSave();
        var island = IslandPhaseBTests.Field(save, "island");
        IslandPhaseBTests.Set(island, "keepLevel", 5);
        IslandPhaseBTests.Set(island, "wood", 150);      // başlangıç hediyesi (§6)
        IslandPhaseBTests.Set(island, "meat", 30);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        IslandPhaseBTests.Set(save, "gems", 0);

        const long Hour = TimeSpan.TicksPerHour;
        long t0 = 636_000_000_000_000_000L;
        double hourNow = 12.0;                                      // açılış günü öğlen başlar
        long Clock() => t0 + (long)(hourNow * Hour);
        IslandPhaseBTests.SetClock(Clock());
        Eco("EnsureSeeded", island, 5);
        var placed = IslandPhaseBTests.Placed(island);


        int nextAkin = 1;
        var akinPerDay = new Dictionary<int, int> { { 1, 3 }, { 2, 1 }, { 3, 0 } };   // sıradan bot Akın 1–4'ü kazanır (§16); sonrası tekrar: Et 0
        bool houseBuilt = false;

        for (int dayNumber = 1; dayNumber <= 7; dayNumber++)
        {
            var day = new Day { number = dayNumber };
            double[] sessions = dayNumber == 1 ? day1Sessions : otherSessions;
            foreach (double hour in sessions)
            {
                hourNow = (dayNumber - 1) * 24.0 + hour;
                long now = Clock();
                IslandPhaseBTests.SetClock(now);
                Eco("Tick", now, true);

                // Topla: tüm üreticiler
                foreach (var entry in placed.Cast<object>().ToList())
                {
                    string kind = (string)IslandPhaseBTests.Field(entry, "kind");
                    if (kind != "Lumber" && kind != "Pasture") continue;
                    int taken = (int)Eco("CollectFrom", entry);
                    if (kind == "Lumber") day.productionWood += taken; else day.productionMeat += taken;
                }
                // Akın: ilk zaferler (Et 5 + 2N, Odun 25 + 8N)
                if (hour == sessions[Mathf.Min(1, sessions.Length - 1)])
                {
                    int wins = akinPerDay.TryGetValue(dayNumber, out int w) ? w : 0;
                    if (wins > 0)
                        for (int i = 0; i < wins && nextAkin <= 4; i++, nextAkin++)
                        {
                            int meat = (int)Isl("AkinMeat", nextAkin), wood = (int)Isl("AkinWood", nextAkin);
                            Eco("AddMeat", meat); Eco("AddWood", wood);
                            day.akinMeat += meat; day.akinWood += wood;
                        }
                }
                if (dayNumber == 1 && hour == 12.0) day.giftMeat = 30;

                // Gün sonu: Kart Atölyesi (gelecek Et harcaması): cüzdanda 60'a kadar Et harcanır (kart paketleri); cüzdan dolup üretimi durdurmasın
                if (hour == sessions[sessions.Length - 1])
                {
                    int meatNow = (int)IslandPhaseBTests.Field(island, "meat");
                    if (meatNow > 60 && (bool)Eco("TrySpendMeat", meatNow - 60)) day.cardMeat += meatNow - 60;
                }
                // Harca: Usta boştaysa en iyi yatırım (Ev → bölge → Ağıl/Oduncu dengeli)
                if (!(bool)IslandPhaseBTests.T("IslandEconomy").GetProperty("BuilderBusy").GetValue(null))
                {
                    int woodBefore = (int)IslandPhaseBTests.Field(island, "wood"), meatBefore = (int)IslandPhaseBTests.Field(island, "meat");
                    bool started = false;
                    string reason = null;
                    if (!houseBuilt && dayNumber >= 2)
                    {
                        // ikinci Ev: +2 piyon (KK3+ ikinci Ev açılır)
                        if (IslandPhaseBTests.FindCell("House", "None", island, 1, out int c, out int r, 5))
                        {
                            var args = new object[] { IslandPhaseBTests.Kind("House"), c, r, false, null };
                            started = IslandPhaseBTests.T("IslandEconomy").GetMethod("TryStartBuild").Invoke(null, args) != null;
                            houseBuilt = started;
                        }
                    }
                    if (!started && dayNumber >= 2 && !ZoneOpen(island, 1)) started = TryZone(island, 1);
                    if (!started && dayNumber >= 3 && ZoneOpen(island, 1) && !ZoneOpen(island, 2)) started = TryZone(island, 2);
                    if (!started)
                    {
                        var pasture = IslandPhaseBTests.FindEntry(island, "Pasture"); var lumber = IslandPhaseBTests.FindEntry(island, "Lumber");
                        var order = (int)IslandPhaseBTests.Field(pasture, "level") <= (int)IslandPhaseBTests.Field(lumber, "level")
                            ? new[] { pasture, lumber } : new[] { lumber, pasture };
                        foreach (var target in order)
                        {
                            var args = new object[] { IslandPhaseBTests.Field(target, "id"), false, null };
                            if ((bool)IslandPhaseBTests.T("IslandEconomy").GetMethod("TryStartUpgrade").Invoke(null, args)) { started = true; reason = null; break; }
                        }
                    }
                    if (started)
                    {
                        day.woodSpent += woodBefore - (int)IslandPhaseBTests.Field(island, "wood");
                        day.meatSpent += meatBefore - (int)IslandPhaseBTests.Field(island, "meat");
                    }
                }
            }
            day.pastureLevel = (int)IslandPhaseBTests.Field(IslandPhaseBTests.FindEntry(island, "Pasture"), "level");
            day.lumberLevel = (int)IslandPhaseBTests.Field(IslandPhaseBTests.FindEntry(island, "Lumber"), "level");
            day.houses = placed.Cast<object>().Count(e => (string)IslandPhaseBTests.Field(e, "kind") == "House" && (int)IslandPhaseBTests.Field(e, "level") >= 1);
            days.Add(day);
            log.AppendLine($"Gün {day.number}: Et üretim {day.productionMeat} + Akın {day.akinMeat} = {day.MeatIncome}" + (day.giftMeat > 0 ? $" (+{day.giftMeat} hediye)" : "") +
                $" · Odun üretim {day.productionWood} + Akın {day.akinWood} · harcanan Odun {day.woodSpent} Et {day.meatSpent} (yükseltme) + {day.cardMeat} (kart) · Ağıl Sv{day.pastureLevel} Oduncu Sv{day.lumberLevel} Ev {day.houses}" +
                $" · altın {day.Gold:0}:Et = {day.Gold / Mathf.Max(1f, day.MeatIncome):0.0}:1 (hediyeli {day.Gold / Mathf.Max(1f, day.MeatIncome + day.giftMeat):0.0}:1)");
        }
    }

    private static string Summarize(string profile, List<Day> days, StringBuilder log)
    {
        string summary = $"=== {profile} ===\n" + log;
        Debug.Log("[IslandEconomySim] " + summary);
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/island"));
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "economy_sim.txt"), summary + "\n");
        return summary;
    }

    /// <summary>Sıradan oyuncu: günde 2 oturum (08:00, 21:00) → 8 sa depo tavanı yüzünden ≈16 sa yakalanan üretim (§6 varsayımı).</summary>
    [UnityTest]
    public IEnumerator CasualPlayerKeepsMeatScarceDayOneAndDaySeven()
    {
        var days = new List<Day>(); var log = new StringBuilder();
        yield return Simulate("Sıradan (2 oturum/gün, ≈16 sa yakalanan üretim)", new[] { 12.0, 22.0 }, new[] { 8.0, 21.0 }, days, log);
        string summary = Summarize("Sıradan (2 oturum/gün, ≈16 sa yakalanan üretim)", days, log);
        var d1 = days[0]; var d7 = days[6];
        float ratio1 = d1.Gold / Mathf.Max(1f, d1.MeatIncome), ratio7 = d7.Gold / Mathf.Max(1f, d7.MeatIncome);
        Assert.That(ratio1, Is.InRange(10f, 30f), "gün 1: Et çok kıt (doc ≈17:1)\n" + summary);
        Assert.That(ratio7, Is.InRange(3.5f, 6f), "gün 7: gelişmiş ada ≈4–5:1 (hedef 3,5–5)\n" + summary);
        Assert.Greater(ratio1, ratio7 * 1.8f, "Et erken çok kıt, gelişince ≈4:1'e iner");
        Assert.That(d1.MeatIncome, Is.InRange(120f, 280f), "gün 1 Et geliri (doc 150–250)");
        Assert.That(days[1].MeatIncome, Is.InRange(380f, 600f), "gün 2 Et geliri (doc 400–550)");
        foreach (var day in days.Where(x => x.number >= 3)) Assert.LessOrEqual(day.akinMeat, 0.15f * day.MeatIncome + 0.01f, "Akın Eti payı gün " + day.number);
        Assert.AreEqual(5, d7.pastureLevel, "Ağıl KK5'e (tavan) yükselmiş olmalı");
    }

    /// <summary>Düzenli oyuncu: günde 3 oturum (≈21 sa yakalanan üretim): yalnız günlüğe yazılır, kıtlık hâlâ korunur.</summary>
    [UnityTest]
    public IEnumerator RegularPlayerThreeSessionsStillKeepsMeatScarce()
    {
        var days = new List<Day>(); var log = new StringBuilder();
        yield return Simulate("Düzenli (3 oturum/gün, ≈21 sa yakalanan üretim)", new[] { 12.0, 17.0, 22.0 }, new[] { 8.0, 15.0, 22.0 }, days, log);
        string summary = Summarize("Düzenli (3 oturum/gün, ≈21 sa yakalanan üretim)", days, log);
        float ratio7 = days[6].Gold / Mathf.Max(1f, days[6].MeatIncome);
        Assert.That(ratio7, Is.InRange(2.8f, 6f), "gün 7 (düzenli oyuncu) Et hâlâ altının ≈1/3–1/5'i\n" + summary);
        Assert.Greater(days[0].Gold / Mathf.Max(1f, days[0].MeatIncome), ratio7 * 1.5f);
    }

    private static bool ZoneOpen(object island, int zone) => (((int)IslandPhaseBTests.Field(island, "zoneMask") >> zone) & 1) == 1;

    private static bool TryZone(object island, int zone)
    {
        var args = new object[] { zone, false, null };
        return (bool)IslandPhaseBTests.T("IslandEconomy").GetMethod("TryStartZone").Invoke(null, args);
    }
}
