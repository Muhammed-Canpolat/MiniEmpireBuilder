using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>Martı Adası kaydı: eski (v5) kayıt güvenle açılır, ada verisi gidip gelir, sürüm 6.</summary>
public class IslandSaveTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private string savedKey, savedBackup, savedFile;

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
    }

    [TearDown]
    public void RestoreRealSave()
    {
        void Put(string key, string value)
        {
            if (value != null) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(FilePath, savedFile);
        else if (File.Exists(FilePath)) File.Delete(FilePath);
        PlayerPrefs.Save();
    }

    private static object NewSave()
    {
        return Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(Type("WeaponType"), "Axe") });
    }

    private static object Load() => Type("SaveSystem").GetMethod("Load").Invoke(null, null);
    private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);

    [Test]
    public void NewSaveHasIslandAndCurrentVersion()
    {
        var save = NewSave();
        Assert.AreEqual(6, Type("PlayerSaveData").GetField("CurrentVersion") != null
            ? Type("PlayerSaveData").GetField("CurrentVersion").GetValue(null) : 6);
        Assert.AreEqual(6, Field(save, "saveVersion"));
        var island = Field(save, "island");
        Assert.NotNull(island);
        Assert.AreEqual(1, Field(island, "keepLevel"));
        Assert.AreEqual(0, Field(island, "highestAkin"));
        Assert.AreEqual(0, Field(island, "tutorialStep"));
    }

    [Test]
    public void OldVersionFiveSaveWithoutIslandLoadsSafely()
    {
        Type("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = NewSave();
        save.GetType().GetField("gold").SetValue(save, 321);
        string json = JsonUtility.ToJson(save);
        // Eski kayıtta ada alanı yoktur ve sürüm 5'tir.
        json = Regex.Replace(json, "\"island\":\\{[^{}]*\\},?", "");
        json = Regex.Replace(json, "\"saveVersion\":\\d+", "\"saveVersion\":5");
        StringAssert.DoesNotContain("\"island\"", json);
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();

        var loaded = Load();
        Assert.NotNull(loaded, "eski kayıt açılmalı");
        Assert.AreEqual(321, Field(loaded, "gold"), "diğer veriler korunur");
        var island = Field(loaded, "island");
        Assert.NotNull(island, "island yüklemede oluşturulur");
        Assert.GreaterOrEqual((int)Field(island, "keepLevel"), 1);
        Assert.GreaterOrEqual((int)Field(island, "lastAkin"), 1);
        Assert.AreEqual(6, Field(loaded, "saveVersion"), "kayıt sürümü 6'ya taşınır");
    }

    [Test]
    public void IslandProgressRoundTrips()
    {
        Type("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = NewSave();
        var island = Field(save, "island");
        void Set(string name, object value) => island.GetType().GetField(name).SetValue(island, value);
        Set("highestAkin", 7);
        Set("meat", 120);
        Set("wood", 95);
        Set("tutorialStep", 4);
        Set("autoFire", true);
        Type("SaveSystem").GetMethod("Save").Invoke(null, new[] { save });
        var loaded = Load();
        var back = Field(loaded, "island");
        Assert.AreEqual(7, Field(back, "highestAkin"));
        Assert.AreEqual(120, Field(back, "meat"));
        Assert.AreEqual(95, Field(back, "wood"));
        Assert.AreEqual(4, Field(back, "tutorialStep"));
        Assert.AreEqual(true, Field(back, "autoFire"));
    }
}
