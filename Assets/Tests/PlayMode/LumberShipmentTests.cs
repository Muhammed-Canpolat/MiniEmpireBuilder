using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Kereste Sevkiyatı (island_base_v2 §6, §18): 100 ada Odun'u süren Ana Üs inşaatını 10 dk kısaltır, günde 3; Ana Üs 5'ten önce yok.
/// Ekran görüntüsü <c>PlaytestOutput/lumber/</c>.
/// </summary>
public class LumberShipmentTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;

    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    private static object Get(string member) => T("IslandEconomy").GetProperty(member).GetValue(null);
    private static Component Manager => T("GameManager").GetProperty("Instance").GetValue(null) as Component;
    private static object Save => Manager.GetType().GetProperty("PlayerData").GetValue(Manager);
    private static object Island => Field(Save, "island");
    private static int Wood => (int)Field(Island, "wood");

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

    /// <summary>Ana Üs <paramref name="mainBase"/>, ada Odun'u <paramref name="wood"/>, 60 dk kalan bir Ana Üs yükseltmesi.</summary>
    private static IEnumerator Setup(int mainBase, int wood, Action<object> onTask)
    {
        var managerType = T("GameManager");
        var manager = Manager;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });
        foreach (var entry in (IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(Field(save, "island"), "wood", wood);
        var task = Activator.CreateInstance(T("BuilderTaskSave"));
        Set(task, "kind", "upgrade");
        Set(task, "label", "Ana Üs");
        Set(task, "buildingType", "MainBase");
        Set(task, "slotId", "main");
        Set(task, "startTicks", DateTime.UtcNow.AddMinutes(-5).Ticks);
        Set(task, "endTicks", DateTime.UtcNow.AddMinutes(60).Ticks);
        ((IList)Field(save, "builderTasks")).Add(task);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        onTask(task);
    }

    private static bool Ship(object task) => (bool)T("IslandEconomy").GetMethod("TryShipLumber").Invoke(null, new[] { task });
    private static double MinutesLeft(object task) => new TimeSpan((long)Field(task, "endTicks") - DateTime.UtcNow.Ticks).TotalMinutes;

    [UnityTest]
    public IEnumerator HundredWoodCutsTenMinutesThreeTimesADay()
    {
        object task = null;
        yield return Setup(6, 350, t => task = t);
        Assert.IsTrue((bool)Get("LumberShipmentOffered"));
        Assert.AreEqual(3, (int)Get("LumberShipmentsLeft"));
        double before = MinutesLeft(task);
        Assert.IsTrue(Ship(task));
        Assert.AreEqual(250, Wood, "100 Odun");
        Assert.AreEqual(before - 10d, MinutesLeft(task), 0.2d, "−10 dk");
        Assert.IsTrue(Ship(task)); Assert.IsTrue(Ship(task));
        Assert.AreEqual(50, Wood);
        Assert.AreEqual(0, (int)Get("LumberShipmentsLeft"));
        Assert.IsFalse((bool)Get("LumberShipmentOffered"), "günlük hak bitti: seçenek kalkar");
        Set(Island, "wood", 500);
        Assert.IsFalse(Ship(task), "günde en çok 3");
        Assert.AreEqual(500, Wood);
    }

    [UnityTest]
    public IEnumerator NotEnoughWoodSpendsNothingAndLockedBeforeIsland()
    {
        object task = null;
        yield return Setup(6, 60, t => task = t);
        double before = MinutesLeft(task);
        Assert.IsTrue((bool)Get("LumberShipmentOffered"), "Odun yetmese de seçenek görünür (soluk)");
        Assert.IsFalse((bool)Get("CanShipLumber"));
        Assert.IsFalse(Ship(task));
        Assert.AreEqual(60, Wood);
        Assert.AreEqual(before, MinutesLeft(task), 0.2d);
        Assert.AreEqual(3, (int)Get("LumberShipmentsLeft"), "hak düşmez");

        yield return Setup(4, 900, t => task = t);
        Assert.IsFalse((bool)Get("LumberShipmentOffered"), "ada (Ana Üs 5) açılmadan sevkiyat yok");
        Assert.IsFalse(Ship(task));
        Assert.AreEqual(900, Wood);
    }

    [UnityTest]
    public IEnumerator CaptureFinishDialogWithLumberOption()
    {
        Screen.SetResolution(390, 844, false);
        object task = null;
        yield return Setup(6, 420, t => task = t);
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 30; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        // Sahne yüklenince görev listesi aynı kayıttan gelir.
        task = ((IList)Field(Save, "builderTasks"))[0];
        T("GemActions").GetMethod("OfferFinish").Invoke(null, new[] { task, null });
        yield return new WaitForSecondsRealtime(0.4f);
        CardWorkshopTests.CaptureScreen("lumber_finish_dialog", "lumber");
        T("GemConfirm").GetMethod("Close").Invoke(null, new object[] { false });
    }
}
