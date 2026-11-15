using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 9. oturum (TECH_AUDIT K-1…K-6): kayıt güvenliği ve göç hattı.
/// <see cref="Session9SaveBaselineTests"/> değişiklikten ÖNCE de geçen, mevcut davranışı kilitleyen testlerdir (eski sürüm kayıtları
/// açılır, oyuncunun değerleri korunur). <see cref="Session9SaveTests"/> yeni güvenceleri sınar.
/// Bütün testler gerçek kaydı saklar ve geri koyar; ürettikleri yedek/karantina dosyalarını siler.
/// </summary>
public abstract class Session9SaveFixture
{
    protected const string Key = "MiniEmpireBuilder_SaveData";
    protected const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    protected const string CorruptKey = "MiniEmpireBuilder_SaveData_Corrupt";
    protected static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");
    protected static string BackupDir => Path.Combine(Application.persistentDataPath, "SaveBackups");

    protected static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    protected static object Get(object target, string field) => target.GetType().GetField(field).GetValue(target);
    protected static void Set(object target, string field, object value) => target.GetType().GetField(field).SetValue(target, value);
    protected static object Static(string type, string member)
    {
        var t = T(type);
        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (p != null) return p.GetValue(null);
        return t.GetField(member, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    }

    private string savedKey, savedBackup, savedCorrupt, savedFile;
    protected DateTime startedUtc;

    /// <summary>Bu testin ürettiği yedek dosyaları (başka testlerin bıraktıkları sayılmaz).</summary>
    protected string[] BackupFiles(string pattern) => !Directory.Exists(BackupDir) ? new string[0]
        : Directory.GetFiles(BackupDir, pattern).Where(file => File.GetLastWriteTimeUtc(file) >= startedUtc).ToArray();

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedCorrupt = PlayerPrefs.HasKey(CorruptKey) ? PlayerPrefs.GetString(CorruptKey) : null;
        savedFile = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        startedUtc = DateTime.UtcNow.AddSeconds(-2);
        Wipe();
    }

    [TearDown]
    public void RestoreRealSave()
    {
        Wipe();
        void Put(string key, string value)
        {
            if (value != null) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        Put(CorruptKey, savedCorrupt);
        if (savedFile != null) File.WriteAllText(FilePath, savedFile);
        PlayerPrefs.Save();
        // Testin ürettiği yedek/karantina dosyaları (gerçek oyuncunun eski yedeklerine dokunulmaz).
        if (Directory.Exists(BackupDir))
            foreach (var file in Directory.GetFiles(BackupDir))
                if (File.GetLastWriteTimeUtc(file) >= startedUtc) File.Delete(file);
        var fault = T("SaveSystem").GetField("MigrationFaultForTests", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        fault?.SetValue(null, null);
    }

    /// <summary>Kayıt kopyalarını siler (yedek klasörüne arşivlemeden): testin temiz başlangıcı.</summary>
    protected static void Wipe()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.DeleteKey(BackupKey);
        PlayerPrefs.DeleteKey(CorruptKey);
        if (File.Exists(FilePath)) File.Delete(FilePath);
        if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
        var reset = T("SaveSystem").GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (reset != null) reset.Invoke(null, null);
        else T("SaveSystem").GetField("lastGoodJson", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
    }

    protected static object NewSave() => T("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
        new[] { Enum.Parse(T("WeaponType"), "Axe") });

    protected static void Save(object data) => T("SaveSystem").GetMethod("Save").Invoke(null, new[] { data });
    protected static object Load() => T("SaveSystem").GetMethod("Load").Invoke(null, null);

    /// <summary>Oynanmış bir kayıt: altın/elmas, kahraman, üç bina, sefer ilerlemesi, girilmiş ada.</summary>
    protected static object PlayedSave()
    {
        var save = NewSave();
        Set(save, "gold", 777);
        Set(save, "gems", 33);
        Set(save, "apprenticeLevel", 3);
        Set(save, "builderCount", 2);
        Set(Get(save, "hero"), "level", 4);
        var buildings = (IList)Get(save, "buildings");
        foreach (var entry in buildings)
        {
            string type = Get(entry, "buildingType").ToString();
            if (type == "MainBase") Set(entry, "level", 5);
            if (type == "ArcherTower") { Set(entry, "level", 2); Set(entry, "isUnlocked", true); }
        }
        var placed = (IList)Get(save, "placedBuildings");
        void Place(string slot, string type, int level, float x, float y)
        {
            var p = Activator.CreateInstance(T("PlacedBuildingData"));
            Set(p, "slotId", slot); Set(p, "buildingType", type); Set(p, "level", level);
            Set(p, "hasPosition", true); Set(p, "x", x); Set(p, "y", y);
            placed.Add(p);
        }
        Place("main_base", "MainBase", 5, 12f, 14f);
        Place("gold_mine", "GoldMine", 3, 6f, 10f);
        Place("archer_tower_1", "ArcherTower", 2, 18f, 10f);
        var progress = (IList)Get(save, "campaignProgress");
        for (int level = 1; level <= 2; level++)
        {
            var p = Activator.CreateInstance(T("CampaignLevelProgress"));
            Set(p, "regionId", 1); Set(p, "levelNumber", level); Set(p, "stars", 3);
            progress.Add(p);
        }
        var island = Get(save, "island");
        Set(island, "villageSeeded", true);
        Set(island, "meat", 12);
        Set(island, "wood", 34);
        Set(island, "keepLevel", 2);
        Set(island, "highestAkin", 3);
        return save;
    }

    protected static string Json(object save) => JsonUtility.ToJson(save);
    protected static string AsVersion(string json, int version) =>
        Regex.Replace(json, "\"saveVersion\":\\d+", "\"saveVersion\":" + version);

    protected static void WriteAllCopies(string json)
    {
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.DeleteKey(BackupKey);
        File.WriteAllText(FilePath, json);
        PlayerPrefs.Save();
    }

    protected static void AssertPlayerValuesKept(object data, string context, int builders = 2)
    {
        Assert.NotNull(data, context + ": kayıt açılamadı");
        Assert.AreEqual(777, Get(data, "gold"), context + ": altın");
        Assert.AreEqual(33, Get(data, "gems"), context + ": elmas");
        Assert.AreEqual(3, Get(data, "apprenticeLevel"), context + ": çırak");
        Assert.AreEqual(builders, Get(data, "builderCount"), context + ": inşaatçı");
        Assert.AreEqual(4, Get(Get(data, "hero"), "level"), context + ": kahraman");
        var placed = (IList)Get(data, "placedBuildings");
        Assert.AreEqual(3, placed.Count, context + ": bina sayısı");
        Assert.AreEqual(5, Get(placed[0], "level"), context + ": Ana Üs seviyesi");
        Assert.AreEqual(3, Get(placed[1], "level"), context + ": maden seviyesi");
        Assert.AreEqual(2, Get(placed[2], "level"), context + ": okçu seviyesi");
        Assert.AreEqual(2, ((IList)Get(data, "campaignProgress")).Count, context + ": sefer ilerlemesi");
        var island = Get(data, "island");
        Assert.AreEqual(12, Get(island, "meat"), context + ": Et");
        Assert.AreEqual(34, Get(island, "wood"), context + ": Odun");
        Assert.AreEqual(3, Get(island, "highestAkin"), context + ": Akın");
    }

    protected static int CurrentVersion => (int)T("PlayerSaveData").GetField("CurrentVersion").GetValue(null);
}

/// <summary>Mevcut davranışı kilitler: değişiklikten önce ve sonra aynı sonucu vermeli.</summary>
public class Session9SaveBaselineTests : Session9SaveFixture
{
    [Test]
    public void B1_EveryOldVersionLoadsAndKeepsPlayerValues()
    {
        string json = Json(PlayedSave());
        for (int version = 2; version <= CurrentVersion; version++)
        {
            Wipe();
            WriteAllCopies(AsVersion(json, version));
            var data = Load();
            // v5 öncesinde birden çok inşaatçı yoktu: göç 1 inşaatçıyla başlatır.
            AssertPlayerValuesKept(data, "v" + version, version < 5 ? 1 : 2);
            Assert.AreEqual(CurrentVersion, Get(data, "saveVersion"), "v" + version + ": sürüm güncellenmeli");
            // v13 göçü: girilmiş adada Kıyı Kalesi o anki etkin değerde (Ana Üs 5) sabitlenir; güncel kayıtta dokunulmaz.
            Assert.AreEqual(version < 13 ? 5 : 2, Get(Get(data, "island"), "keepLevel"), "v" + version + ": Kıyı Kalesi");
        }
    }

    [Test]
    public void B2_MigratedSaveIsWrittenBackAndReloadsIdentically()
    {
        WriteAllCopies(AsVersion(Json(PlayedSave()), 6));
        var first = Load();
        AssertPlayerValuesKept(first, "ilk yükleme");
        // Göçten sonra diskteki kayıt güncel sürümdedir; ikinci yükleme aynı veriyi verir.
        var second = Load();
        AssertPlayerValuesKept(second, "ikinci yükleme");
        Assert.AreEqual(CurrentVersion, Get(second, "saveVersion"));
        StringAssert.Contains("\"saveVersion\":" + CurrentVersion, PlayerPrefs.GetString(Key));
    }

    [Test]
    public void B3_EditorSaveCopyLoads()
    {
        string path = Path.Combine(Application.dataPath, "Tests/PlayMode/Fixtures/editor_save_v13_2026-11-15.json");
        Assert.IsTrue(File.Exists(path), "kayıt örneği yok: " + path);
        string json = File.ReadAllText(path);
        WriteAllCopies(json);
        var data = Load();
        Assert.NotNull(data);
        var original = JsonUtility.FromJson(json, T("PlayerSaveData"));
        foreach (string field in new[] { "gold", "gems", "apprenticeLevel", "builderCount", "lives", "dailyStreak", "endlessBestFloor" })
            Assert.AreEqual(Get(original, field), Get(data, field), field);
        Assert.AreEqual(((IList)Get(original, "placedBuildings")).Count, ((IList)Get(data, "placedBuildings")).Count);
        Assert.AreEqual(((IList)Get(original, "placedDecor")).Count, ((IList)Get(data, "placedDecor")).Count);
        Assert.AreEqual(((IList)Get(original, "campaignProgress")).Count, ((IList)Get(data, "campaignProgress")).Count);
        Assert.AreEqual(((IList)Get(Get(original, "island"), "placed")).Count, ((IList)Get(Get(data, "island"), "placed")).Count);
    }

    [Test]
    public void B4_NoSaveReturnsNull()
    {
        Assert.IsNull(Load());
        Assert.IsFalse((bool)T("SaveSystem").GetMethod("HasSave").Invoke(null, null));
    }
}

/// <summary>Yeni güvenceler (TECH_AUDIT K-1…K-5).</summary>
public class Session9SaveTests : Session9SaveFixture
{
    private static string Status => Static("SaveSystem", "LastLoadStatus").ToString();

    // ---------- K-2: göç orijinal kaydı ezmez ----------

    [Test]
    public void K2_MigrationKeepsOriginalCopy()
    {
        string original = AsVersion(Json(PlayedSave()), 6);
        WriteAllCopies(original);
        AssertPlayerValuesKept(Load(), "göç");
        Assert.AreEqual("Loaded", Status);
        // Göç öncesi kayıt dosya olarak saklanır ve içeriği birebir aynıdır.
        var snapshots = BackupFiles("save.v6.premigration.*.json");
        Assert.AreEqual(1, snapshots.Length, "göç öncesi yedek dosyası");
        Assert.AreEqual(original, File.ReadAllText(snapshots[0]));
        // PlayerPrefs yedeği de ara sürüm değil, orijinal kayıttır (göç tek kayıtla biter).
        Assert.AreEqual(original, PlayerPrefs.GetString(BackupKey));
    }

    [Test]
    public void K2_CurrentVersionLoadWritesNoSnapshot()
    {
        WriteAllCopies(Json(PlayedSave()));
        AssertPlayerValuesKept(Load(), "güncel");
        Assert.AreEqual(0, BackupFiles("*.premigration.*").Length);
    }

    // ---------- K-1: açılamayan kaydın üstüne yazılmaz ----------

    [Test]
    public void K1_MigrationFailureKeepsEveryCopyAndBlocksOverwrite()
    {
        string original = AsVersion(Json(PlayedSave()), 9);
        WriteAllCopies(original);
        T("SaveSystem").GetField("MigrationFaultForTests", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .SetValue(null, Delegate.CreateDelegate(typeof(Action), typeof(Session9SaveTests).GetMethod(nameof(Boom), BindingFlags.NonPublic | BindingFlags.Static)));
        LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\]"));
        Assert.IsNull(Load());
        Assert.AreEqual("Failed", Status);
        Assert.AreEqual(original, PlayerPrefs.GetString(Key), "asıl kopya değişmemeli");
        Assert.AreEqual(original, File.ReadAllText(FilePath), "dosya kopyası değişmemeli");
        Assert.GreaterOrEqual(BackupFiles("save.quarantine.*.json").Length, 1, "karantina dosyası");

        // Oyun bu durumda kaydetmeye çalışırsa (ör. yanlışlıkla yeni oyun) kaydın üstüne yazılmaz.
        LogAssert.Expect(LogType.Warning, new Regex(@"\[SaveSystem\]"));
        Save(NewSave());
        Assert.AreEqual(original, PlayerPrefs.GetString(Key));
        Assert.AreEqual(original, File.ReadAllText(FilePath));

        // Hata giderilince (sonraki sürüm) aynı kayıt açılır.
        T("SaveSystem").GetField("MigrationFaultForTests", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        AssertPlayerValuesKept(Load(), "hata giderildi");
        Assert.AreEqual("Loaded", Status);
    }

    private static void Boom() => throw new InvalidOperationException("test: göç hatası");

    [Test]
    public void K1_UnreadableSaveIsQuarantinedAndNotOverwritten()
    {
        PlayerPrefs.SetString(Key, "{\"gold\":999,\"build");
        File.WriteAllText(FilePath, "bozuk");
        PlayerPrefs.Save();
        LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\]"));
        Assert.IsNull(Load());
        Assert.AreEqual("Failed", Status);
        Assert.GreaterOrEqual(BackupFiles("save.quarantine.*.json").Length, 2, "iki bozuk kopya da saklanmalı");
        LogAssert.Expect(LogType.Warning, new Regex(@"\[SaveSystem\]"));
        Save(NewSave());
        Assert.AreEqual("{\"gold\":999,\"build", PlayerPrefs.GetString(Key));
    }

    [Test]
    public void K1_ExplicitNewGameArchivesOldSaveThenSavesNormally()
    {
        string original = Json(PlayedSave());
        WriteAllCopies(original);
        T("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        Assert.AreEqual("NoSave", Status);
        var archived = BackupFiles("save.deleted.*.json");
        Assert.IsTrue(archived.Any(file => File.ReadAllText(file) == original), "silinen kayıt birebir arşivlenmeli");
        var fresh = NewSave();
        Set(fresh, "gold", 5);
        Save(fresh);
        Assert.AreEqual(5, Get(Load(), "gold"));
    }

    // ---------- K-3: yarım kalan dosya yazımı ----------

    [Test]
    public void K3_LeftoverTempFileIsUsedWhenItIsTheNewestHealthyCopy()
    {
        var save = PlayedSave();
        Save(save);
        // Yeni kayıt geçici dosyaya yazıldı, yerine taşınmadan uygulama kapandı; PlayerPrefs de yazılamadı.
        string older = PlayerPrefs.GetString(Key);
        Set(save, "gold", 4242);
        Save(save);
        string newer = PlayerPrefs.GetString(Key);
        Wipe();
        PlayerPrefs.SetString(Key, older);
        File.WriteAllText(FilePath + ".tmp", newer);
        Assert.AreEqual(4242, Get(Load(), "gold"));
    }

    // ---------- K-4: en yeni kopya saatten bağımsız seçilir ----------

    [Test]
    public void K4_NewestCopyIsChosenBySerialNotWallClock()
    {
        var save = PlayedSave();
        Save(save);
        string first = PlayerPrefs.GetString(Key);
        Set(save, "gold", 900);
        Save(save);
        string second = PlayerPrefs.GetString(Key);
        // Cihaz saati geri alınmış gibi: yeni kaydın zaman damgası eskisinden küçük.
        long firstTicks = long.Parse(Regex.Match(first, "\"lastSavedTicks\":(\\d+)").Groups[1].Value);
        second = Regex.Replace(second, "\"lastSavedTicks\":\\d+", "\"lastSavedTicks\":" + (firstTicks - TimeSpan.FromDays(1).Ticks));
        Wipe();
        PlayerPrefs.SetString(Key, second);
        PlayerPrefs.SetString(BackupKey, first);
        File.WriteAllText(FilePath, second);
        Assert.AreEqual(900, Get(Load(), "gold"), "saat geri alınsa da en son kayıt açılmalı");
    }

    // ---------- K-5: boş JSON ve gelecek sürüm ----------

    [Test]
    public void K5_EmptyObjectIsNotAValidSave()
    {
        Assert.IsFalse((bool)T("SaveSystem").GetMethod("TryParse").Invoke(null, new object[] { "{}", 0L }));
        string good = Json(PlayedSave());
        PlayerPrefs.SetString(Key, "{}");
        File.WriteAllText(FilePath, good);
        AssertPlayerValuesKept(Load(), "boş asıl kopya");
    }

    [Test]
    public void K5_NewerVersionSaveIsSnapshottedBeforeUse()
    {
        string future = AsVersion(Json(PlayedSave()), CurrentVersion + 3);
        WriteAllCopies(future);
        LogAssert.Expect(LogType.Warning, new Regex(@"\[SaveSystem\]"));
        AssertPlayerValuesKept(Load(), "gelecek sürüm");
        var snapshots = BackupFiles("save.v" + (CurrentVersion + 3) + ".newer.*.json");
        Assert.AreEqual(1, snapshots.Length);
        Assert.AreEqual(future, File.ReadAllText(snapshots[0]));
    }

    // ---------- Değişmezlikler ----------

    [Test]
    public void I1_FreshAndMigratedSavesHaveNoInvariantViolations()
    {
        var check = T("SaveInvariants").GetMethod("Check");
        Assert.IsEmpty((ICollection)check.Invoke(null, new[] { NewSave() }), "yeni kayıt");
        string json = Json(PlayedSave());
        for (int version = 2; version <= CurrentVersion; version++)
        {
            Wipe();
            WriteAllCopies(AsVersion(json, version));
            var problems = (ICollection)check.Invoke(null, new[] { Load() });
            Assert.IsEmpty(problems, "v" + version + ": " + string.Join(" | ", problems.Cast<string>()));
        }
    }

    [Test]
    public void I2_TypeLevelCopyIsResyncedFromBuildings()
    {
        var save = PlayedSave();
        // Kayıttaki ikinci kopya (tür seviyesi) binaların gerçek seviyesinden ayrışmış.
        foreach (var entry in (IList)Get(save, "buildings"))
            if (Get(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", 9);
        WriteAllCopies(Json(save));
        var data = Load();
        foreach (var entry in (IList)Get(data, "buildings"))
            if (Get(entry, "buildingType").ToString() == "MainBase")
                Assert.AreEqual(5, Get(entry, "level"), "tür seviyesi = tamamlanmış en yüksek bina seviyesi");
    }

    [Test]
    public void O1_NewAccountHasGoldInTheMineForTheFirstGuideStep()
    {
        Assert.AreEqual(40, Get(NewSave(), "goldMineStoredGold"));
    }
}
