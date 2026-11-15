using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 8. oturum SON: her bölgenin 2. seviyesi ve boss seviyesi (5) bot ile (kule + kahraman) oynanır; başlangıç / savaş ortası / son görüntüleri
/// (yeni üst bar + düşman davranışı) PlaytestOutput/screens altına, sonuç satırları PlaytestOutput/session8/SON_play.txt'ye yazılır.
/// Uzun → [Explicit]. Kayıt geri yüklenir.
/// </summary>
[Explicit]
public class Session8SonTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

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

    [UnityTest, Timeout(7200000)]
    public IEnumerator PlayTwoLevelsPerRegion()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session8"));
        Directory.CreateDirectory(folder);
        var lines = new List<string>();
        var bot = Type("PlaytestBot");
        for (int region = 1; region <= 6; region++)
            foreach (int level in new[] { 2, 5 })
            {
                yield return (IEnumerator)bot.GetMethod("PlayBattle").Invoke(null, new object[]
                    { region, level, 3f, 900f, true, 8000 + region * 10 + level, false, "combined", false });
                var t = bot.GetProperty("Last").GetValue(null);
                string F(string f) => Convert.ToString(t.GetType().GetField(f).GetValue(t), System.Globalization.CultureInfo.InvariantCulture);
                var shots = (IList)t.GetType().GetField("screenshots").GetValue(t);
                lines.Add($"B{region}-S{level}: {F("result")} {F("defeatReason")}, süre {F("durationSec")} sn, Ana Üs canı {F("baseHealthEnd")}, " +
                          $"düşman {F("enemiesKilled")}/{F("enemiesSpawned")}, dalga {F("wavesReached")}, görüntüler: {string.Join(" ", shots.Cast<string>())}");
                File.WriteAllLines(Path.Combine(folder, "SON_play.txt"), lines);
            }
    }
}
