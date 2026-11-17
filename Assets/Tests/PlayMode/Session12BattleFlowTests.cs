using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// D116 (Grup 4 / E: BUGLAR 25, 28, 30): açılmış atış yeri bulut altında da kullanılabilir ve Kıyı Kalesi yükseltmesi sahnede
/// hemen geçerli; takılı düşman watchdog ile yedek hedefe (kale) döner; güney kenarından doğan yarasa okçu yokken kaleye,
/// okçu varken okçuya uçar (adacıkta beklemez) ve dalga biter. Yalıtılmış test kaydı.
/// </summary>
public class Session12BattleFlowTests
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
        devWas = (bool)T("DevSettings").GetProperty("Enabled").GetValue(null);
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
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        PlayerPrefs.Save();
    }

    // ---- yardımcılar ----
    private static object Raid => Prop(Setup, "Raid");
    private static Component Archer => (Component)T("IslandArcher").GetProperty("Instance").GetValue(null);
    private static Component Castle => (Component)Prop(Setup, "CastleStructure");
    private static Vector2 PostWorld(int i) => (Vector2)Prop(((Array)T("IslandLayout").GetField("Posts").GetValue(null)).GetValue(i), "World");
    private static int CurrentPost => (int)Prop(Archer, "CurrentPost");
    private static string Phase => Prop(Raid, "Phase").ToString();

    private static void StartAkin(int n) =>
        Assert.IsTrue((bool)Raid.GetType().GetMethod("StartAkin").Invoke(Raid, new object[] { n }), "Akın başladı");

    private static void Tap(Vector2 world) => Setup.GetType().GetMethod("HandleWorldTap").Invoke(Setup, new object[] { world });

    private static Component Spawn(string type, Vector2 at, Vector2[] route) =>
        (Component)T("IslandEnemy").GetMethod("Spawn").Invoke(null,
            new object[] { Enum.Parse(T("EnemyType"), type), 1, Castle.transform, at, route, -1, false, false });

    private static object Ctl(Component enemy) => Prop(enemy, "Controller");
    private static Vector2 Cell(float col, float row) => new Vector2(col, (int)T("IslandLayout").GetField("Height").GetValue(null) - row);

    private static bool ZoneOpenAt(float col, float row)
    {
        int zone = (int)Call("IslandLayout", "ZoneAt", (int)col, (int)row);
        int mask = (int)Field(CurrentIsland(), "zoneMask");
        return (bool)Call("IslandGrid", "ZoneOpen", zone, mask);
    }

    private static void SinkBoats()
    {
        foreach (var b in ((IEnumerable)T("RaidBoat").GetProperty("All").GetValue(null)).Cast<Component>().ToList())
            if (b != null && (bool)Prop(b, "Alive")) b.GetType().GetMethod("TakeArrow").Invoke(b, new object[] { 100000f, false });
    }

    // ==================== 25 / 28: BULUT ALTINDAKİ ATIŞ YERİ ====================

    [UnityTest, Timeout(120000)]
    public IEnumerator OpenedPostUnderCloudIsWalkableAndKeepUpgradeAppliesWithoutReload()
    {
        yield return LoadIsland(null, 1, v => { });
        StartAkin(1);
        yield return new WaitForSecondsRealtime(0.5f);
        var archerType = T("IslandArcher");

        // P4 (doğu yar başı) Doğu Kıyısı bulutunun altında (bölge satın alınmadı) ama KK1'de açık: Akın 1 öğreticisi buraya yollar.
        Assert.IsFalse(ZoneOpenAt(16.5f, 13.5f), "P4 bulut altında (Z1 kapalı)");
        Assert.IsTrue((bool)archerType.GetMethod("PostOpen").Invoke(Archer, new object[] { 3 }), "P4 KK1'de açık");
        Tap(PostWorld(3));
        float until = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < until && CurrentPost != 3) yield return null;
        Assert.AreEqual(3, CurrentPost, "okçu bulut altındaki P4'e yürüdü");

        // Sahnede Kıyı Kalesi yükseltmesi (kayıt değişir): yeniden yüklemeden P5 (Orta Çayır bulutu altında) açılır ve gidilir.
        Assert.IsFalse((bool)archerType.GetMethod("PostOpen").Invoke(Archer, new object[] { 4 }), "P5 KK1'de kilitli");
        Set(CurrentIsland(), "keepLevel", 5);
        Assert.AreEqual(5, (int)Prop(Archer, "KeepLevel"), "okçu canlı Kıyı Kalesi seviyesini okur");
        Assert.IsFalse(ZoneOpenAt(9.5f, 24.5f), "P5 bulut altında (Z5 kapalı)");
        yield return null;
        var marker = ((IEnumerable)Prop(Setup, "PostMarkerList")).Cast<object>().ElementAt(4);
        Assert.AreNotEqual("locked", (string)Field(marker, "State"), "işaret de açık gösterir (tek kural)");
        Tap(PostWorld(4));
        until = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < until && CurrentPost != 4) yield return null;
        Assert.AreEqual(4, CurrentPost, "okçu bulut altındaki P5'e yürüdü");
    }

    // ==================== WATCHDOG ====================

    [UnityTest, Timeout(120000)]
    public IEnumerator StuckEnemyIsRedirectedToTheKeepAfterTheWatchdogWindow()
    {
        yield return LoadIsland(null, 3, v => { });
        T("IslandBot").GetMethod("StripDefenses").Invoke(null, null);
        // Güney çayırında bir Akıncı; hedefi denizin ortasında ulaşılamaz bir nokta (yol bulma en yakın kıyıda durur → takılır).
        Vector2 at = Cell(9.5f, 24.5f);
        var enemy = Spawn("Raider", at, new Vector2[0]);
        var controller = Ctl(enemy);
        var type = controller.GetType();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var dummy = new GameObject("UnreachableTarget").transform;
        dummy.position = Cell(11f, 35.5f);
        yield return null;
        type.GetMethod("SetTarget", flags).Invoke(controller, new object[] { dummy, (Vector2)dummy.position, 0.2f });
        type.GetField("nextRetarget", flags).SetValue(controller, Time.time + 1000f);

        float until = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < until && (int)Prop(controller, "WatchdogTrips") == 0) yield return null;
        Assert.GreaterOrEqual((int)Prop(controller, "WatchdogTrips"), 1, "takılı düşman watchdog'u tetikledi");
        Assert.IsTrue((bool)Prop(controller, "InFallback"), "yedek hedef kipinde");
        Assert.AreEqual(Castle.transform, Prop(controller, "CurrentTarget"), "yedek hedef kale");
        Vector2 castle = (Vector2)T("IslandLayout").GetProperty("CastleCenter").GetValue(null);
        float before = Vector2.Distance(enemy.transform.position, castle);
        yield return new WaitForSeconds(2f);
        Assert.Less(Vector2.Distance(enemy.transform.position, castle), before - 0.5f, "kaleye doğru ilerliyor");
        Object.Destroy(dummy.gameObject);
        Object.Destroy(enemy.gameObject);
    }

    // ==================== 30: GÜNEYDEN GELEN YARASA ====================

    [UnityTest, Timeout(120000)]
    public IEnumerator SouthEdgeBatsReachTheKeepWithoutArcher()
    {
        yield return LoadIsland(null, 4, v => { });
        T("IslandBot").GetMethod("StripDefenses").Invoke(null, null);
        Archer.gameObject.SetActive(false);   // Okçuluk Alanı yokken olduğu gibi: okçu sahnede yok
        Vector2 edge = (Vector2)Call("IslandWaves", "FlyerEdgePoint", 2);   // güney kenarı (harita dışı)
        var bats = new[] { Spawn("Bat", edge + new Vector2(-1.8f, 0f), new Vector2[0]), Spawn("Bat", edge + new Vector2(1.8f, 0f), new Vector2[0]) };
        Time.timeScale = 3f;
        float until = Time.realtimeSinceStartup + 25f;
        bool AllAtKeep() => bats.All(b => b == null || (float)Prop(Ctl(b), "CurrentHealth") <= 0f
            || ((Component)Prop(Ctl(b), "CurrentTarget")) == Castle.transform
               && Vector2.Distance(b.transform.position, Castle.transform.position) <= (float)Prop(Ctl(b), "AttackRange") + 3f);
        while (Time.realtimeSinceStartup < until && !AllAtKeep()) yield return null;
        Time.timeScale = 1f;
        Assert.IsTrue(AllAtKeep(), "yarasalar adacıkta beklemeden kaleye ulaştı");
        foreach (var b in bats)
        {
            Assert.AreEqual(0, (int)Prop(Ctl(b), "WatchdogTrips"), "düz uçuş: watchdog'a gerek kalmadı");
            Object.Destroy(b.gameObject);
        }
        Archer.gameObject.SetActive(true);
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator SouthEdgeBatsFlyToTheArcherAndTheWaveEnds()
    {
        yield return LoadIsland(island => Set(island, "highestAkin", 3), 4, v => { });
        T("IslandBot").GetMethod("StripDefenses").Invoke(null, null);
        StartAkin(1);   // tek dalga: dalga bitince akın kazanılır
        Archer.GetType().GetProperty("AutoFire").SetValue(Archer, true);
        // Okçu P3'te (batı yar başı): güney kenarındaki sürü menzil dışında doğar.
        Archer.GetType().GetMethod("MoveToPost").Invoke(Archer, new object[] { 2 });
        Vector2 edge = (Vector2)Call("IslandWaves", "FlyerEdgePoint", 2);
        var bats = new[] { Spawn("Bat", edge + new Vector2(-1.8f, 0f), new Vector2[0]), Spawn("Bat", edge + new Vector2(1.8f, 0f), new Vector2[0]) };
        Time.timeScale = 3f;
        float until = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < until && Phase == "Running")
        {
            SinkBoats();   // tekneler hemen batar: dalganın bitmesini yalnız yarasalar belirler
            yield return null;
        }
        Time.timeScale = 1f;
        Assert.AreEqual("Won", Phase, "dalga (ve tek dalgalı Akın 1) bitti");
        Assert.IsTrue(bats.All(b => b == null || (float)Prop(Ctl(b), "CurrentHealth") <= 0f), "yarasalar okçuya uçtu ve düşürüldü");
        Assert.AreEqual(0, (int)Prop(Raid, "StallInterventions"), "dalga güvenlik ağına gerek kalmadı");
    }
}
