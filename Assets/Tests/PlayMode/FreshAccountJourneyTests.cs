using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// SON aşama: yeni hesapla Ana Üs → Ada → Sefer akışını baştan oynar (sınırsız elmas kapalı). Sürücü oyun kodunda:
/// PlaytestJourney (yalnız editör/dev). Rapor: PlaytestOutput/journey/journey.md. Gerçek kayıt test sonunda geri yüklenir.
/// Tek başına: -testFilter FreshAccountJourneyTests
/// </summary>
public class FreshAccountJourneyTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
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
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
        for (int i = 0; i < SaveKeys.Length; i++)
        {
            if (savedPrefs[i] == null) PlayerPrefs.DeleteKey(SaveKeys[i]);
            else PlayerPrefs.SetString(SaveKeys[i], savedPrefs[i]);
        }
        PlayerPrefs.Save();
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
    }

    [UnityTest, Timeout(3600000)]
    public IEnumerator NewAccountPlaysBaseIslandCampaign()
    {
        Screen.SetResolution(390, 844, false);
        var journey = T("PlaytestJourney");
        yield return (IEnumerator)journey.GetMethod("Run").Invoke(null, new object[] { 16, 3 });
        var report = journey.GetProperty("Last").GetValue(null);
        object F(string name) => report.GetType().GetField(name).GetValue(report);
        var problems = (System.Collections.Generic.List<string>)F("problems");
        Debug.Log("[Journey] sorunlar: " + string.Join(" | ", problems));

        Assert.IsTrue((bool)F("unlimitedGemsOff"), "sınırsız elmas kapalı oynanır");
        Assert.GreaterOrEqual((int)F("finalMainBase"), 5, "Ana Üs 5'e (ada) ulaşılır");
        Assert.IsTrue((bool)F("islandVisited"), "ADA düğmesiyle ada açılır");
        Assert.IsTrue((bool)F("namePrompted"), "yeni oyuncuya ada adı sorulur");
        Assert.IsTrue((bool)F("campaignPanelSeen"), "SEFER düğmesiyle aşama paneli (kartlar + ödül) açılır");
        Assert.Greater((int)F("wins"), 0, "en az bir zafer");
        Assert.IsFalse(problems.Any(p => p.Contains("setup_failed") || p.Contains("yüklenmedi") || p.Contains("kurulamadı")),
            "akış kırılmadan sürer: " + string.Join(" | ", problems));
    }
}
