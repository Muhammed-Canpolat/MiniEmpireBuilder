using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IslandPhaseBTests;

/// <summary>
/// Grup 5 (BUGLAR 16, 22): yeni ada Odun/Et 0 ile başlar; ada üssünde malzeme eksikken elmas YALNIZ onay penceresinden sonra düşer.
/// Yalıtılmış test kaydı; sınıf sonunda test GameManager'ı yok edilir.
/// </summary>
public class IslandGemConfirmTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
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
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        T("GemConfirm").GetMethod("Close").Invoke(null, new object[] { false });
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
        PlayerPrefs.Save();
    }

    [Test]
    public void NewIslandStartsWithZeroWoodAndMeat()
    {
        var island = NewIsland(5);
        Call("IslandEconomy", "EnsureStarted", island, 5);
        Assert.AreEqual(0, (int)Field(island, "wood"));
        Assert.AreEqual(0, (int)Field(island, "meat"));
    }

    [UnityTest]
    public IEnumerator ShortfallUpgradeSpendsGemsOnlyAfterConfirm()
    {
        yield return LoadIsland(island => { Set(island, "wood", 0); Set(island, "meat", 0); }, 5, v => devWas = v);
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        Set(Prop(gm, "PlayerData"), "gems", 500);
        var pasture = FindEntry(CurrentIsland(), "Pasture");
        Assert.NotNull(pasture);
        string id = (string)Field(pasture, "id");
        var cost = ((int wood, int meat, int seconds))Call("IslandEconomy", "UpgradeCost", pasture);
        Assert.IsFalse((bool)Call("IslandEconomy", "CanSpend", cost.wood, cost.meat), "malzeme eksik");

        bool started = false;
        Func<bool, bool> start = useGems =>
        {
            var args = new object[] { id, useGems, null };
            started = (bool)T("IslandEconomy").GetMethod("TryStartUpgrade").Invoke(null, args);
            return started;
        };
        T("GemActions").GetMethod("OfferIslandTopUp").Invoke(null, new object[] { cost.wood, cost.meat, "x", start, null });
        yield return null;

        Assert.IsTrue((bool)T("GemConfirm").GetProperty("IsOpen").GetValue(null), "onay penceresi açıldı");
        Assert.AreEqual(500, (int)Prop(gm, "Gems"), "onaydan önce elmas düşmez");
        Assert.IsFalse(started, "onaydan önce yükseltme başlamaz");

        // Vazgeç: hâlâ düşmez
        T("GemConfirm").GetMethod("Close").Invoke(null, new object[] { true });
        Assert.AreEqual(500, (int)Prop(gm, "Gems"));
        Assert.IsFalse(started);

        T("GemActions").GetMethod("OfferIslandTopUp").Invoke(null, new object[] { cost.wood, cost.meat, "x", start, null });
        yield return null;
        Button button = null;
        foreach (var b in GameObject.Find("GemConfirm").GetComponentsInChildren<Button>(true))
            if (b.name == "ConfirmButton") button = b;
        button.onClick.Invoke();
        Assert.IsTrue(started, "onayla başlar");
        Assert.Less((int)Prop(gm, "Gems"), 500, "onayla elmas düşer");
    }
}
