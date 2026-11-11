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

    private const int Seeds = 3;

    private static IEnumerator PlayAll(int region)
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            yield return Play(region, 1, seed, seed == 1);
            yield return Play(region, 5, seed, seed == 1);
        }
    }

    private static IEnumerator Play(int region, int level, int seed, bool capture)
    {
        Screen.SetResolution(390, 844, false);
        var bot = Type("PlaytestBot");
        var routine = (IEnumerator)bot.GetMethod("PlayBattle")
            .Invoke(null, new object[] { region, level, 4f, 420f, capture, seed * 7919 + region * 31 + level });
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
}
