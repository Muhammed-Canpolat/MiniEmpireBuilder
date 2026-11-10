using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Kayıt güvenliği: üç kopya (asıl, yedek, dosya); bozuk ya da yarım kayıt yedekten açılır.</summary>
public class SaveSystemTests
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

    private static object NewSave(int gold)
    {
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        save.GetType().GetField("gold").SetValue(save, gold);
        return save;
    }

    private static void Save(object data) => Type("SaveSystem").GetMethod("Save").Invoke(null, new[] { data });
    private static int LoadedGold()
    {
        var data = Type("SaveSystem").GetMethod("Load").Invoke(null, null);
        Assert.NotNull(data, "kayıt açılamadı");
        return (int)data.GetType().GetField("gold").GetValue(data);
    }

    [Test]
    public void CorruptSaveFallsBackToNewestHealthyCopy()
    {
        Type("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        Save(NewSave(111));
        System.Threading.Thread.Sleep(5);
        Save(NewSave(222));
        Assert.AreEqual(222, LoadedGold());
        Assert.IsTrue(File.Exists(FilePath), "dosya kopyası yazılmalı");

        // Asıl kayıt yarım kaldı: dosyadaki en yeni sağlam kopya açılır.
        PlayerPrefs.SetString(Key, "{\"gold\":999,\"build");
        Assert.AreEqual(222, LoadedGold());

        // Dosya da bozuk: bir önceki kaydın yedeği açılır.
        File.WriteAllText(FilePath, "bozuk");
        Assert.AreEqual(111, LoadedGold());

        // Hepsi bozuksa null döner (oyun yeni kayda geçer), bozuk kayıt saklanır.
        PlayerPrefs.SetString(BackupKey, "{");
        Assert.IsNull(Type("SaveSystem").GetMethod("Load").Invoke(null, null));
        Assert.IsTrue(PlayerPrefs.HasKey("MiniEmpireBuilder_SaveData_Corrupt"));
        PlayerPrefs.DeleteKey("MiniEmpireBuilder_SaveData_Corrupt");
    }
}
