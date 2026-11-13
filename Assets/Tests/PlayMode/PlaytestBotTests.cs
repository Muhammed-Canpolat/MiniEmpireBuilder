using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Otomatik oyuncu: her bölgenin 1. ve 5. aşamasını "beklenen oyuncu" profiliyle oynar ve
/// PlaytestOutput/battle_r*_l*.json telemetrisini yazar. Kayıt (PlayerPrefs + save.json) test sonunda geri yüklenir.
/// Batchmode: -runTests -testPlatform PlayMode -testFilter PlaytestBotTests
/// </summary>
public class PlaytestBotTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static readonly string[] SaveKeys =
        { "MiniEmpireBuilder_SaveData", "MiniEmpireBuilder_SaveData_Backup", "MiniEmpireBuilder_SaveData_Corrupt" };
    private string[] savedPrefs;
    private string savedFile;
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");

    [OneTimeSetUp]
    public void BackupSave()
    {
        savedPrefs = SaveKeys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
    }

    [OneTimeTearDown]
    public void RestoreSave()
    {
        Time.timeScale = 1f;
        for (int i = 0; i < SaveKeys.Length; i++)
        {
            if (savedPrefs[i] == null) PlayerPrefs.DeleteKey(SaveKeys[i]);
            else PlayerPrefs.SetString(SaveKeys[i], savedPrefs[i]);
        }
        PlayerPrefs.Save();
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
    }

    /// <summary>Tohum sayısı: varsayılan 3; ölçüm turlarında PLAYTEST_SEEDS ortam değişkeniyle artırılır (gürültüyü azaltmak için).</summary>
    private static readonly int Seeds = int.TryParse(Environment.GetEnvironmentVariable("PLAYTEST_SEEDS"), out int n) && n > 0 ? n : 3;

    private static IEnumerator PlayAll(int region)
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            yield return Play(region, 1, seed, seed == 1);
            yield return Play(region, 5, seed, seed == 1);
        }
    }

    private static IEnumerator PlayCasual(int region)
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            yield return Play(region, 1, seed, false, true);
            yield return Play(region, 5, seed, false, true);
        }
    }

    /// <summary>Baskın strateji testi: yalnız boss aşaması (S5), yalnız kule / yalnız kahraman.</summary>
    private static IEnumerator PlayStrategy(int region, string strategy)
    {
        for (int seed = 1; seed <= Seeds; seed++)
            yield return Play(region, 5, seed, false, false, strategy);
    }

    private static IEnumerator Play(int region, int level, int seed, bool capture, bool casual = false, string strategy = "combined")
    {
        Screen.SetResolution(390, 844, false);
        var bot = Type("PlaytestBot");
        var routine = (IEnumerator)bot.GetMethod("PlayBattle")
            .Invoke(null, new object[] { region, level, 4f, 600f, capture, seed * 7919 + region * 31 + level, casual, strategy });
        yield return routine;
        var last = bot.GetProperty("Last").GetValue(null);
        string result = (string)last.GetType().GetField("result").GetValue(last);
        Debug.Log($"[PlaytestBot] Bölge {region} Aşama {level} tohum {seed}: {result}");
        Assert.AreNotEqual("setup_failed", result, "Savaş sahnesi kurulamadı");
    }

    [UnityTest, Timeout(1800000)] public IEnumerator Region1() { yield return PlayAll(1); }
    [UnityTest, Timeout(1800000)] public IEnumerator Region2() { yield return PlayAll(2); }
    [UnityTest, Timeout(1800000)] public IEnumerator Region3() { yield return PlayAll(3); }
    [UnityTest, Timeout(1800000)] public IEnumerator Region4() { yield return PlayAll(4); }
    [UnityTest, Timeout(1800000)] public IEnumerator Region5() { yield return PlayAll(5); }
    [UnityTest, Timeout(1800000)] public IEnumerator Region6() { yield return PlayAll(6); }

    // Sıradan oyuncu profili (ayrı çalıştırılır: -testFilter PlaytestBotTests.Casual)
    [UnityTest, Timeout(1800000)] public IEnumerator CasualRegion1() { yield return PlayCasual(1); }
    [UnityTest, Timeout(1800000)] public IEnumerator CasualRegion2() { yield return PlayCasual(2); }
    [UnityTest, Timeout(1800000)] public IEnumerator CasualRegion3() { yield return PlayCasual(3); }
    [UnityTest, Timeout(1800000)] public IEnumerator CasualRegion4() { yield return PlayCasual(4); }
    [UnityTest, Timeout(1800000)] public IEnumerator CasualRegion5() { yield return PlayCasual(5); }
    [UnityTest, Timeout(1800000)] public IEnumerator CasualRegion6() { yield return PlayCasual(6); }

    // Baskın strateji (-testFilter PlaytestBotTests.Strategy)
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyTowerOnly1() { yield return PlayStrategy(1, "towerOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyHeroOnly1() { yield return PlayStrategy(1, "heroOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyTowerOnly2() { yield return PlayStrategy(2, "towerOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyHeroOnly2() { yield return PlayStrategy(2, "heroOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyTowerOnly3() { yield return PlayStrategy(3, "towerOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyHeroOnly3() { yield return PlayStrategy(3, "heroOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyTowerOnly4() { yield return PlayStrategy(4, "towerOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyHeroOnly4() { yield return PlayStrategy(4, "heroOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyTowerOnly5() { yield return PlayStrategy(5, "towerOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyHeroOnly5() { yield return PlayStrategy(5, "heroOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyTowerOnly6() { yield return PlayStrategy(6, "towerOnly"); }
    [UnityTest, Timeout(1800000)] public IEnumerator StrategyHeroOnly6() { yield return PlayStrategy(6, "heroOnly"); }
}
