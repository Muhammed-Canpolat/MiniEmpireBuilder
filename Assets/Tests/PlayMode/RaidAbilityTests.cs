using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// 3. oturum 3.2 / 3.3: akında yuvarlak atış yeri işaretleri + ilk akın ipucu, yetenek düğmelerinin yay düzeni ve
/// Ok Yağmuru (nişan + gökten ok) / Ateşli Ok (alev, yanma) / Ağ (ağ fırlatma, üstte ağ) efektleri.
/// Görüntüler <c>PlaytestOutput/session3/R*.png</c>. Yalıtılmış test kaydı.
/// </summary>
public class RaidAbilityTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private const string HintKey = "MEB_PostHintShown";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private int savedHint;
    private bool devWas;

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
        savedHint = PlayerPrefs.GetInt(HintKey, -1);
        devWas = (bool)T("DevSettings").GetProperty("Enabled").GetValue(null);
    }

    [TearDown]
    public void RestoreRealSave()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedHint >= 0) PlayerPrefs.SetInt(HintKey, savedHint); else PlayerPrefs.DeleteKey(HintKey);
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
    private static object K(string n) => Enum.Parse(T("IslandAbilities+Kind"), n);
    private static Component Abilities => (Component)T("IslandAbilities").GetProperty("Instance").GetValue(null);
    private static Component Archer => (Component)T("IslandArcher").GetProperty("Instance").GetValue(null);
    private static Component Castle => (Component)Prop(Setup, "CastleStructure");

    private static void StartAkin(int n) =>
        Assert.IsTrue((bool)Raid.GetType().GetMethod("StartAkin").Invoke(Raid, new object[] { n }), "Akın başladı");

    private static Component SpawnEnemy(Vector2 at)
    {
        var enemy = (Component)T("IslandEnemy").GetMethod("Spawn").Invoke(null,
            new object[] { Enum.Parse(T("EnemyType"), "ZombieWarrior"), 1, Castle.transform, at,
                new[] { at, (Vector2)T("IslandLayout").GetProperty("CastleCenter").GetValue(null) }, 0, false, false });
        var controller = Prop(enemy, "Controller");
        controller.GetType().GetProperty("MovementLocked").SetValue(controller, true);
        return enemy;
    }

    private static float Health(Component enemy) => (float)Prop(Prop(enemy, "Controller"), "CurrentHealth");

    private static void ClearEnemies()
    {
        foreach (var e in ((IEnumerable)T("IslandEnemy").GetProperty("All").GetValue(null)).Cast<Component>().ToList()) if (e != null) Object.Destroy(e.gameObject);
        foreach (var b in ((IEnumerable)T("RaidBoat").GetProperty("All").GetValue(null)).Cast<Component>().ToList()) if (b != null) Object.Destroy(b.gameObject);
    }

    private static IEnumerator ArenaWithEnemy(Action<Component, Vector2> ready)
    {
        yield return LoadIsland(island => Set(island, "highestAkin", 3), 6, v => { });
        StartAkin(3);
        ClearEnemies();
        T("IslandBot").GetMethod("StripDefenses").Invoke(null, null);   // kuleler ateş etmesin
        yield return new WaitForSecondsRealtime(0.3f);
        var at = (Vector2)Prop(Archer, "Position");
        var enemy = SpawnEnemy(at + new Vector2(0f, 3.5f));
        yield return null;
        ready(enemy, at + new Vector2(0f, 3.5f));
    }

    private static RectTransform Rt(string name)
    {
        var go = GameObject.Find(name);
        Assert.NotNull(go, name + " bulunamadı");
        return (RectTransform)go.transform;
    }

    private static IEnumerable GetMarkers() => (IEnumerable)Prop(Setup, "PostMarkerList");

    // ==================== 3.2 ATIŞ YERİ İŞARETLERİ ====================

    [UnityTest, Timeout(120000)]
    public IEnumerator PostMarkersAreRoundWithStatesAndTheHintShowsOnlyOnce()
    {
        PlayerPrefs.DeleteKey(HintKey);
        yield return LoadIsland(null, 3, v => { });
        StartAkin(1);
        yield return new WaitForSecondsRealtime(1.0f);
        var markers = GetMarkers().Cast<object>().ToList();
        Assert.AreEqual(5, markers.Count, "5 atış yeri işareti");
        var states = markers.Select(m => (string)Field(m, "State")).ToList();
        Assert.AreEqual(new[] { "current", "locked", "reachable", "reachable", "locked" }, states.ToArray(), "Kıyı Kalesi 3: P1 altın, P2/P5 kilitli, P3/P4 ulaşılabilir");
        foreach (var m in markers)
        {
            var ring = (SpriteRenderer)Field(m, "ring");
            Assert.IsTrue(ring.enabled, "halka görünür");
            Assert.AreEqual(ring.sprite.rect.width, ring.sprite.rect.height, "yuvarlak halka sprite'ı kare dokulu");
            Assert.AreEqual("IslandAbilityRing", ring.sprite.name);
        }
        Assert.IsTrue(((SpriteRenderer)Field(markers[1], "lockIcon")).enabled, "kilitli işarette kilit simgesi");
        Assert.IsFalse(((SpriteRenderer)Field(markers[2], "lockIcon")).enabled, "açık işarette kilit yok");
        Assert.Greater(((SpriteRenderer)Field(markers[0], "ring")).color.r, 0.9f, "mevcut: altın");
        Assert.AreEqual(1, PlayerPrefs.GetInt(HintKey, 0), "ipucu bir kez gösterildi ve işaretlendi");
        Assert.IsTrue((bool)Prop(Setup, "PostHintVisible"), "en yakın işaret üstünde küçük yazı");
        yield return Session3Tests.Capture("R1_post_markers_hint");

        // Koşarken hedef işareti turuncu
        Assert.IsTrue((bool)Archer.GetType().GetMethod("MoveToPost").Invoke(Archer, new object[] { 3 }));
        yield return new WaitForSecondsRealtime(0.25f);
        Assert.AreEqual("target", (string)Field(GetMarkers().Cast<object>().ElementAt(3), "State"), "gidilen yer vurgulu");
        yield return Session3Tests.Capture("R1_post_markers_running");
        yield return new WaitForSecondsRealtime(4f);
        Assert.AreEqual("current", (string)Field(GetMarkers().Cast<object>().ElementAt(3), "State"), "varınca altın");

        // İkinci akın (yeni ada yüklemesi): ipucu bir daha çıkmaz
        yield return LoadIsland(null, 3, v => { });
        StartAkin(1);
        yield return new WaitForSecondsRealtime(1.0f);
        Assert.IsFalse((bool)Prop(Setup, "PostHintVisible"), "ipucu kayıt başına bir kez");
    }

    // ==================== 3.3 DÜZEN ====================

    [UnityTest, Timeout(120000)]
    public IEnumerator AbilityButtonsSitOnAnArcAroundFireAndUtilityButtonsAreSeparate()
    {
        yield return LoadIsland(island => Set(island, "highestAkin", 3), 6, v => { });
        StartAkin(3);
        yield return new WaitForSecondsRealtime(0.8f);
        var fire = Rt("FireButton");
        var abilityRects = new List<Rect>();
        foreach (var n in new[] { "AbilityVolley", "AbilityFireArrow", "AbilityNet" })
        {
            var rt = Rt(n);
            Assert.GreaterOrEqual(rt.rect.width, 52f, n + " ≥ 52");
            Assert.GreaterOrEqual(rt.rect.height, 52f, n + " ≥ 52");
            Vector2 d = rt.anchoredPosition - fire.anchoredPosition;
            Assert.Less(d.x, 0f, n + ": ATEŞ'in solunda");
            Assert.Greater(d.y, -1f, n + ": ATEŞ merkezinin üstünde ya da hizasında");
            Assert.AreEqual(112f, d.magnitude, 8f, n + ": yay yarıçapı");
            Assert.AreEqual(1f, rt.anchorMin.x); Assert.AreEqual(0f, rt.anchorMin.y);   // sağ-alt
            abilityRects.Add(new Rect(rt.anchoredPosition - rt.rect.size * 0.5f, rt.rect.size));
        }
        var fireRect = new Rect(fire.anchoredPosition - fire.rect.size * 0.5f, fire.rect.size);
        // Düğmeler yuvarlak: daireler örtüşmemeli (merkezler arası ≥ yarıçaplar toplamı)
        foreach (var r in abilityRects)
            Assert.GreaterOrEqual(Vector2.Distance(r.center, fireRect.center), (r.width + fireRect.width) * 0.5f - 0.5f, "yetenek düğmesi ATEŞ'i örtmez");
        for (int i = 0; i < 3; i++)
            for (int j = i + 1; j < 3; j++) Assert.GreaterOrEqual(Vector2.Distance(abilityRects[i].center, abilityRects[j].center), abilityRects[i].width - 0.5f, "yetenekler birbirini örtmez");
        foreach (var n in new[] { "AutoButton", "MapButton" })
        {
            var rt = Rt(n);
            Assert.GreaterOrEqual(rt.rect.width, 48f, n + " ≥ 48"); Assert.GreaterOrEqual(rt.rect.height, 48f);
            var r = new Rect(rt.anchoredPosition - rt.rect.size * 0.5f, rt.rect.size);
            foreach (var a in abilityRects) Assert.IsFalse(r.Overlaps(a), n + " yay grubundan ayrı");
            Assert.IsFalse(r.Overlaps(fireRect), n + " ATEŞ'ten ayrı");
            Assert.Greater(r.center.y, fireRect.yMax, n + ": ATEŞ'in üstünde");
        }
        // Bekleme: kullanınca dairesel dolgu + kalan saniye yazısı
        var abilities = Abilities;
        Assert.IsTrue((bool)abilities.GetType().GetMethod("UseAt").Invoke(abilities, new object[] { K("FireArrow"), (Vector2)Prop(Archer, "Position") }));
        yield return new WaitForSecondsRealtime(0.3f);
        var seconds = Rt("AbilityFireArrow").Find("Seconds");
        Assert.NotNull(seconds);
        var tmp = seconds.GetComponents<Component>().First(c => c.GetType().Name == "TextMeshProUGUI");
        Assert.IsTrue(((string)Prop(tmp, "text")).Length > 0, "kalan saniye yazısı");
        Assert.Greater(Rt("AbilityFireArrow").Find("Cooldown").GetComponent<UnityEngine.UI.Image>().fillAmount, 0.5f, "dairesel bekleme dolgusu");
        yield return Session3Tests.Capture("R2_ability_layout");
    }

    // ==================== 3.3 YAĞMUR ====================

    [UnityTest, Timeout(180000)]
    public IEnumerator VolleyAimThenUseAtRainsArrowsAndDamagesTheCircle()
    {
        Component enemy = null; Vector2 center = default;
        yield return ArenaWithEnemy((e, c) => { enemy = e; center = c; });
        var abilities = Abilities;
        var type = abilities.GetType();
        Assert.IsFalse((bool)Prop(abilities, "Aiming"));
        Assert.IsTrue((bool)type.GetMethod("BeginAim").Invoke(abilities, null), "nişan kipi açıldı");
        Assert.IsTrue((bool)Prop(abilities, "Aiming"));
        yield return null;
        var ring = GameObject.Find("VolleyAimRing");
        Assert.NotNull(ring, "yer halkası önizlemesi"); Assert.IsTrue(ring.activeInHierarchy);
        Assert.AreEqual(1.8f * 2f, ring.transform.Find("Ring").localScale.x, 0.01f, "halka çapı = 2 × VolleyRadius");
        type.GetMethod("SetAim").Invoke(abilities, new object[] { center + new Vector2(0.4f, 0f) });
        var aim = (Vector2)Prop(abilities, "AimCenter");
        Assert.AreEqual(center.x + 0.4f, aim.x, 0.01f);
        // Menzil sınırı
        type.GetMethod("SetAim").Invoke(abilities, new object[] { (Vector2)Prop(Archer, "Position") + new Vector2(60f, 0f) });
        Assert.LessOrEqual(Vector2.Distance((Vector2)Prop(Archer, "Position"), (Vector2)Prop(abilities, "AimCenter")), (float)Prop(Archer, "Range") + 2.01f, "menzil sınırı");
        type.GetMethod("SetAim").Invoke(abilities, new object[] { center });
        yield return Session3Tests.Capture("R3_volley_aim");

        float arrowDamage = (float)Isl("ArcherDamage", (int)Prop(Archer, "Level"));
        float before = Health(enemy);
        Assert.IsTrue((bool)type.GetMethod("ConfirmAim").Invoke(abilities, null), "ikinci basış ateşler");
        Assert.IsFalse((bool)Prop(abilities, "Aiming"));
        Assert.IsFalse((bool)type.GetMethod("CanReady").Invoke(abilities, new[] { K("Volley") }), "bekleme başladı");

        var seen = new HashSet<int>();
        float until = Time.realtimeSinceStartup + 1.8f;
        bool shot = false;
        while (Time.realtimeSinceStartup < until)
        {
            foreach (var go in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (go.name == "IslandArrow") seen.Add(go.gameObject.GetInstanceID());
            if (!shot && seen.Count >= 6) { shot = true; yield return Session3Tests.Capture("R3_volley_arrows_falling"); }
            yield return null;
        }
        Assert.GreaterOrEqual(seen.Count, 12, "gökten düşen ~15 ok");
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.AreEqual(before - 3f * arrowDamage, Health(enemy), 0.1f, "daire içindeki düşman 3 × ok hasarı aldı");

        // Anlık Use(kind) botu bozmadan çalışır: bekleme bitince aynı hedefe
        Assert.IsFalse((bool)type.GetMethod("Use").Invoke(abilities, new[] { K("Volley") }), "bekleme sürerken kullanılamaz");
    }

    // ==================== 3.3 ATEŞLİ OK ====================

    [UnityTest, Timeout(180000)]
    public IEnumerator FireArrowLeavesFlameTrailAndSetsABurningOverlayThatEnds()
    {
        Component enemy = null; Vector2 center = default;
        yield return ArenaWithEnemy((e, c) => { enemy = e; center = c; });
        var abilities = Abilities;
        Assert.IsTrue((bool)abilities.GetType().GetMethod("UseAt").Invoke(abilities, new object[] { K("FireArrow"), center }));
        Assert.AreEqual(3, (int)Prop(abilities, "FireShotsLeft"));
        var target = T("IslandTarget").GetConstructor(new[] { T("IslandEnemy") }).Invoke(new object[] { enemy });
        Archer.GetType().GetMethod("LockTarget").Invoke(Archer, new[] { target });
        Assert.IsTrue((bool)Archer.GetType().GetMethod("DebugFireAtNearest").Invoke(Archer, null));
        bool trail = false;
        for (int i = 0; i < 25 && !trail; i++)
        {
            trail = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Any(s => s.name == "FlameTrail");
            yield return null;
        }
        Assert.IsTrue(trail, "okun arkasında alev izi");
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.IsTrue((bool)Prop(enemy, "Burning"), "hedef yanıyor");
        Assert.IsTrue((bool)Prop(enemy, "HasBurnFx"), "yanan düşmanın üstünde dönen alev");
        Assert.NotNull(enemy.transform.Find("BurnFx"), "alev düşmana bağlı");
        yield return Session3Tests.Capture("R4_fire_arrow_burning");
        Time.timeScale = 3f;
        float until = Time.realtimeSinceStartup + 8f;
        while ((bool)Prop(enemy, "Burning") && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        Time.timeScale = 1f;
        Assert.IsFalse((bool)Prop(enemy, "HasBurnFx"), "yanma bitince alev söner");
    }

    // ==================== 3.3 AĞ ====================

    [UnityTest, Timeout(180000)]
    public IEnumerator NetIsThrownAndAnOverlayCoversTheRootedEnemyUntilItEnds()
    {
        Component enemy = null; Vector2 center = default;
        yield return ArenaWithEnemy((e, c) => { enemy = e; center = c; });
        var abilities = Abilities;
        Assert.IsTrue((bool)abilities.GetType().GetMethod("Use").Invoke(abilities, new[] { K("Net") }));
        bool flying = false;
        for (int i = 0; i < 30 && !flying; i++) { flying = GameObject.Find("NetProjectile") != null; yield return null; }
        Assert.IsTrue(flying, "fırlatılan ağ nesnesi görünür");
        yield return Session3Tests.Capture("R5_net_thrown");
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.IsTrue((bool)Prop(enemy, "Rooted"), "kökleşti");
        var overlay = enemy.transform.Find("NetOverlay");
        Assert.NotNull(overlay, "ağ düşmanın üstünde");
        Assert.IsTrue(overlay.GetComponent<SpriteRenderer>().enabled);
        yield return Session3Tests.Capture("R5_net_on_enemy");
        Time.timeScale = 3f;
        float until = Time.realtimeSinceStartup + 6f;
        while ((bool)Prop(enemy, "Rooted") && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
        Time.timeScale = 1f;
        Assert.IsNull(enemy.transform.Find("NetOverlay"), "kök bitince ağ kalkar");
    }
}
