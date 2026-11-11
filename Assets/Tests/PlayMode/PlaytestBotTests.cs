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

    private static IEnumerator Play(int region, int level)
    {
        Screen.SetResolution(390, 844, false);
        var bot = Type("PlaytestBot");
        var routine = (IEnumerator)bot.GetMethod("PlayBattle")
            .Invoke(null, new object[] { region, level, 4f, 420f, true });
        yield return routine;
        var last = bot.GetProperty("Last").GetValue(null);
        string result = (string)last.GetType().GetField("result").GetValue(last);
        Debug.Log($"[PlaytestBot] Bölge {region} Aşama {level}: {result}");
        Assert.AreNotEqual("setup_failed", result, "Savaş sahnesi kurulamadı");
    }

    [UnityTest, Timeout(600000)] public IEnumerator Region1() { yield return Play(1, 1); yield return Play(1, 5); }
    [UnityTest, Timeout(600000)] public IEnumerator Region2() { yield return Play(2, 1); yield return Play(2, 5); }
    [UnityTest, Timeout(600000)] public IEnumerator Region3() { yield return Play(3, 1); yield return Play(3, 5); }
    [UnityTest, Timeout(600000)] public IEnumerator Region4() { yield return Play(4, 1); yield return Play(4, 5); }
    [UnityTest, Timeout(600000)] public IEnumerator Region5() { yield return Play(5, 1); yield return Play(5, 5); }
    [UnityTest, Timeout(600000)] public IEnumerator Region6() { yield return Play(6, 1); yield return Play(6, 5); }
}
