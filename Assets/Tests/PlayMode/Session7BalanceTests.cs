using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 7. oturum (AŞAMA 7): bölge haritalarıyla zorluk eğrisi. Her bölge: 5 seviye "iyi oyuncu" (kule + kahraman) + boss'ta baskın strateji
/// (yalnız kule / yalnız kahraman / kahramansız). Telemetri: PlaytestOutput/battle_r*_l*_s*[_profil].json; özet Tools/balance_s7.py.
/// Uzun (bölge başına ~6 dk) → [Explicit]: -testFilter "Session7BalanceTests.Region1" gibi adla koşturulur. Kayıt geri yüklenir.
/// </summary>
[Explicit]
public class Session7BalanceTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);

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

    private static readonly int Seeds = int.TryParse(Environment.GetEnvironmentVariable("PLAYTEST_SEEDS"), out int n) && n > 0 ? n : 1;

    private static IEnumerator Play(int region, int level, int seed, string strategy, bool capture)
    {
        Screen.SetResolution(390, 844, false);
        var bot = Type("PlaytestBot");
        yield return (IEnumerator)bot.GetMethod("PlayBattle").Invoke(null, new object[]
            { region, level, 4f, 600f, capture, seed * 7919 + region * 31 + level, false, strategy, false });
        var last = bot.GetProperty("Last").GetValue(null);
        string result = (string)last.GetType().GetField("result").GetValue(last);
        Debug.Log($"[S7Balance] B{region} S{level} {strategy} tohum {seed}: {result}");
        Assert.AreNotEqual("setup_failed", result);
    }

    private static IEnumerator RegionCurve(int region)
    {
        for (int seed = 1; seed <= Seeds; seed++)
            for (int level = 1; level <= 5; level++)
                yield return Play(region, level, seed, "combined", seed == 1);
        foreach (var strategy in new[] { "towerOnly", "heroOnly", "noHero" })
            for (int seed = 1; seed <= Seeds; seed++)
                yield return Play(region, 5, seed, strategy, false);
    }

    [UnityTest, Timeout(3600000)] public IEnumerator Region1() { yield return RegionCurve(1); }
    [UnityTest, Timeout(3600000)] public IEnumerator Region2() { yield return RegionCurve(2); }
    [UnityTest, Timeout(3600000)] public IEnumerator Region3() { yield return RegionCurve(3); }
    [UnityTest, Timeout(3600000)] public IEnumerator Region4() { yield return RegionCurve(4); }
    [UnityTest, Timeout(3600000)] public IEnumerator Region5() { yield return RegionCurve(5); }
    [UnityTest, Timeout(3600000)] public IEnumerator Region6() { yield return RegionCurve(6); }
}
