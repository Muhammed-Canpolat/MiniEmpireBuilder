using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>Martı Adası kaydı: eski (v5) kayıt güvenle açılır, ada verisi gidip gelir, sürüm 7 (Faz B: yerleştirilmiş binalar, bölge maskesi, Usta görevi).</summary>
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
    /// <summary>Güncel kayıt sürümü (v8: Kart Atölyesi); testler sabit sayıya bağlanmaz.</summary>
    private static int Current => (int)Type("PlayerSaveData").GetField("CurrentVersion").GetValue(null);

    [Test]
    public void NewSaveHasIslandAndCurrentVersion()
    {
        var save = NewSave();
        Assert.GreaterOrEqual(Current, 7, "ada kaydı en az v7");
        Assert.AreEqual(Current, Field(save, "saveVersion"));
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
        // island artık JSON'un son alanı: önceki virgülle birlikte silinir (aksi hâlde ",}" geçersiz JSON olur).
        json = Regex.Replace(json, @"(,""island"":\{[^{}]*""task"":\{[^{}]*\}\})|(""island"":\{[^{}]*""task"":\{[^{}]*\}\},)", "");
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
        Assert.AreEqual(Current, Field(loaded, "saveVersion"), "kayıt güncel sürüme taşınır");
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

    private static object NewEntry(string id, string kind, int level, int col, int row, float stock, long tick)
    {
        var entry = Activator.CreateInstance(Type("IslandPlacedBuilding"));
        void Put(string name, object value) => entry.GetType().GetField(name).SetValue(entry, value);
        Put("id", id); Put("kind", kind); Put("level", level); Put("col", col); Put("row", row); Put("stock", stock); Put("lastTick", tick);
        return entry;
    }

    [Test]
    public void V7BuildingsZonesPilesAndBuilderTaskRoundTrip()
    {
        Type("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = NewSave();
        var island = Field(save, "island");
        void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
        var placed = (System.Collections.IList)Field(island, "placed");
        placed.Add(NewEntry("Pasture@12,18", "Pasture", 6, 12, 18, 41.5f, 636_000_000_000_000_000L));
        placed.Add(NewEntry("House#3", "House", 0, 5, 20, 0f, 0L));
        Set(island, "villageSeeded", true);
        Set(island, "nextBuildingSerial", 4);
        Set(island, "zoneMask", 7);
        var task = Activator.CreateInstance(Type("IslandBuilderTask"));
        Set(task, "kind", "build"); Set(task, "buildingId", "House#3"); Set(task, "targetLevel", 1);
        Set(task, "startTicks", 636_000_000_000_000_000L); Set(task, "endTicks", 636_000_050_000_000_000L);
        Set(island, "task", task);
        Type("SaveSystem").GetMethod("Save").Invoke(null, new[] { save });

        var back = Field(Load(), "island");
        Assert.AreEqual(true, Field(back, "villageSeeded"));
        Assert.AreEqual(4, Field(back, "nextBuildingSerial"));
        Assert.AreEqual(7, Field(back, "zoneMask"));
        var list = (System.Collections.IList)Field(back, "placed");
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("Pasture", Field(list[0], "kind"));
        Assert.AreEqual(6, Field(list[0], "level"));
        Assert.AreEqual(41.5f, (float)Field(list[0], "stock"), 0.001f);
        Assert.AreEqual(636_000_000_000_000_000L, Field(list[0], "lastTick"));
        Assert.AreEqual(0, Field(list[1], "level"), "inşaattaki bina level 0 kalır");
        var backTask = Field(back, "task");
        Assert.AreEqual("build", Field(backTask, "kind"));
        Assert.AreEqual("House#3", Field(backTask, "buildingId"));
        Assert.AreEqual(636_000_050_000_000_000L, Field(backTask, "endTicks"));
    }

    [Test]
    public void VersionSixSaveMigratesToSevenAndSeedsTheVillageWithoutLoss()
    {
        Type("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = NewSave();
        var island = Field(save, "island");
        island.GetType().GetField("keepLevel").SetValue(island, 6);
        island.GetType().GetField("wood").SetValue(island, 222);
        island.GetType().GetField("meat").SetValue(island, 77);
        island.GetType().GetField("highestAkin").SetValue(island, 5);
        string json = JsonUtility.ToJson(save);
        // v6 kaydında Faz B alanları yoktur: yerleştirilmiş binalar, bölge maskesi, Usta görevi, tohum bayrağı
        json = Regex.Replace(json, @",""villageSeeded"":(true|false),""placed"":\[[^\]]*\],""nextBuildingSerial"":\d+,""zoneMask"":\d+,""task"":\{[^{}]*\}", "");
        StringAssert.DoesNotContain("zoneMask", json);
        StringAssert.DoesNotContain("villageSeeded", json);
        json = Regex.Replace(json, "\"saveVersion\":\\d+", "\"saveVersion\":6");
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();

        var loaded = Load();
        Assert.NotNull(loaded, "v6 kaydı açılmalı");
        Assert.AreEqual(Current, Field(loaded, "saveVersion"));
        var back = Field(loaded, "island");
        Assert.AreEqual(222, Field(back, "wood"), "cüzdan korunur");
        Assert.AreEqual(77, Field(back, "meat"));
        Assert.AreEqual(5, Field(back, "highestAkin"));
        Assert.AreEqual(1, Field(back, "zoneMask"), "yalnız Z0 açık");
        Assert.NotNull(Field(back, "task"));
        Assert.AreEqual(false, Field(back, "villageSeeded"));
        Assert.AreEqual(0, ((System.Collections.IList)Field(back, "placed")).Count);

        // İlk açılışta hazır köy Faz C seviyeleriyle yazılır (kule 1 + KK/2, Ev/Oduncu/Ağıl 1), stok 0
        Type("IslandEconomy").GetMethod("EnsureSeeded").Invoke(null, new[] { back, 6 });
        var placed = (System.Collections.IList)Field(back, "placed");
        Assert.AreEqual(6, placed.Count, "Ev, Oduncu, Okçuluk, Ağıl, 2 Kule");
        foreach (var entry in placed)
        {
            string kind = (string)Field(entry, "kind");
            if (kind == "Tower") Assert.AreEqual(1 + 6 / 2, Field(entry, "level"));
            if (kind == "House" || kind == "Lumber" || kind == "Pasture") Assert.AreEqual(1, Field(entry, "level"));
            Assert.AreEqual(0f, (float)Field(entry, "stock"), 0.001f);
        }
        Assert.AreEqual(true, Field(back, "villageSeeded"));
        // Yeniden çağırmak üzerine yazmaz
        placed.RemoveAt(0);
        Type("IslandEconomy").GetMethod("EnsureSeeded").Invoke(null, new[] { back, 6 });
        Assert.AreEqual(5, ((System.Collections.IList)Field(back, "placed")).Count);
    }
}
