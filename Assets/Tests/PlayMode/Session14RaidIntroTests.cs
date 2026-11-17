using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// Grup 8 (J): ada akın tanıtımı (bug 26, D120) ve destek e-postası (bug 32). Adımlar sırayla, kayda yazılır, ikinci akında yok, eski kayıt (alan yok),
/// ATLA, okçu ölüyken gizli, mailto adresi. Ekran görüntüleri Docs/playtest/bugs-g8/.
/// </summary>
public class Session14RaidIntroTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private static readonly string OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/playtest/bugs-g8"));
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
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        Time.timeScale = 1f;
    }

    // ---- RaidIntro yansıma yardımcıları ----
    private static Type IntroType => T("RaidIntro");
    private static int Bit(string name) => (int)IntroType.GetField(name).GetValue(null);
    private static object NewIntro(object island, bool postHint = false)
    {
        var intro = Activator.CreateInstance(IntroType);
        intro.GetType().GetMethod("Begin").Invoke(intro, new[] { island, postHint });
        return intro;
    }
    private static object Ctx(bool running = true, bool alive = true, bool boat = true, bool target = true, bool postStage = true, bool full = false, int abilities = 0)
    {
        var c = Activator.CreateInstance(T("RaidIntro+Context"));
        void S(string f, object v) => c.GetType().GetField(f).SetValue(c, v);
        S("Running", running); S("ArcherAlive", alive); S("BoatSeen", boat); S("TargetInRange", target);
        S("PostStage", postStage); S("FullFrame", full); S("UnlockedAbilities", abilities);
        return c;
    }
    private static void Tick(object intro, float dt, object ctx) => intro.GetType().GetMethod("Tick").Invoke(intro, new[] { dt, ctx });
    private static int Current(object intro) => (int)Prop(intro, "Current");
    private static int Seen(object intro) => (int)Prop(intro, "Seen");
    private static object FreshIsland() => Activator.CreateInstance(T("IslandSaveData"));
    private static void Call0(object intro, string m, params object[] a) => intro.GetType().GetMethod(m).Invoke(intro, a);

    /// <summary>Boşluğu bekler, sıradaki adımın çıkmasını doğrular.</summary>
    private static void ExpectStep(object intro, int bit, object ctx)
    {
        for (int i = 0; i < 20 && Current(intro) == 0; i++) Tick(intro, 1f, ctx);
        Assert.AreEqual(bit, Current(intro), "sıradaki adım");
    }

    [Test]
    public void StepsProgressInOrderAndAreWrittenToTheSave()
    {
        var save = FreshIsland();
        int persisted = 0;
        var intro = NewIntro(save);
        intro.GetType().GetField("Persist").SetValue(intro, (Action)(() => persisted++));
        var ctx = Ctx();
        ExpectStep(intro, Bit("Fire"), ctx);
        Assert.AreNotEqual("", (string)Prop(intro, "Message"));
        Call0(intro, "OnShot", false);
        Assert.AreEqual(Bit("Fire"), Seen(intro), "ATEŞ bitti → kayda yazıldı");
        ExpectStep(intro, Bit("Charge"), ctx);
        Call0(intro, "OnShot", false);
        Assert.AreEqual(Bit("Charge"), Current(intro), "dokunma dolu atış adımını kapatmaz");
        Call0(intro, "OnShot", true);
        ExpectStep(intro, Bit("Post"), ctx);
        Call0(intro, "OnPostChanged");
        ExpectStep(intro, Bit("Map"), ctx);
        Tick(intro, 0.1f, Ctx(full: true));   // HARİTA'ya basıldı → TAKİP hemen sonra
        Assert.AreEqual(0, Current(intro));
        Tick(intro, 0.1f, Ctx(full: true));
        Assert.AreEqual(Bit("Follow"), Current(intro));
        Tick(intro, 0.1f, Ctx(full: false));
        Assert.AreEqual(0, Current(intro));
        Assert.AreEqual(31, Seen(intro), "beş temel adım işaretli");
        Assert.AreEqual(31, (int)save.GetType().GetField("raidIntroSeen").GetValue(save));
        Assert.GreaterOrEqual(persisted, 5, "her kapanışta kayıt");
        Assert.AreEqual(1f, Time.timeScale, "oyun donmaz");
    }

    [Test]
    public void SecondRaidDoesNotShowSeenSteps()
    {
        var save = FreshIsland();
        save.GetType().GetField("raidIntroSeen").SetValue(save, 31);
        var intro = NewIntro(save);
        for (int i = 0; i < 30; i++) Tick(intro, 1f, Ctx());
        Assert.AreEqual(0, Current(intro), "ikinci akında gösterilmez");
        var timeout = NewIntro(FreshIsland());
        ExpectStep(timeout, Bit("Fire"), Ctx());
        Tick(timeout, 8f, Ctx());   // 7 sn sonra kapanır ve işaretlenir
        Assert.AreEqual(0, Current(timeout));
        Assert.AreEqual(Bit("Fire"), Seen(timeout));
    }

    [Test]
    public void SaveWithoutTheFieldLoadsAsZeroWithoutErrors()
    {
        var island = JsonUtility.FromJson("{\"keepLevel\":3,\"highestAkin\":0,\"tutorialStep\":1}", T("IslandSaveData"));
        Assert.NotNull(island);
        Assert.AreEqual(0, (int)island.GetType().GetField("raidIntroSeen").GetValue(island));
        var intro = NewIntro(island);
        ExpectStep(intro, Bit("Fire"), Ctx());
        // Eski oyuncu (highestAkin>0): ATEŞ ve atış yeri öğretilmiş sayılır; dolu atış/HARİTA/TAKİP kalır
        var old = JsonUtility.FromJson("{\"keepLevel\":5,\"highestAkin\":4,\"tutorialStep\":4}", T("IslandSaveData"));
        var oldIntro = NewIntro(old);
        Assert.AreEqual(Bit("Fire") | Bit("Post"), Seen(oldIntro));
        ExpectStep(oldIntro, Bit("Charge"), Ctx());
    }

    [Test]
    public void SkipMarksAllBasicStepsButNotAbilities()
    {
        var intro = NewIntro(FreshIsland());
        ExpectStep(intro, Bit("Fire"), Ctx());
        Call0(intro, "Skip");
        Assert.AreEqual(31, Seen(intro));
        Assert.AreEqual(0, Current(intro));
        for (int i = 0; i < 20; i++) Tick(intro, 1f, Ctx());
        Assert.AreEqual(0, Current(intro), "ATLA sonrası temel adım çıkmaz");
        // yetenek bitleri ayrı: Ok Yağmuru açılınca bir kez gösterilir
        for (int i = 0; i < 20 && Current(intro) == 0; i++) Tick(intro, 1f, Ctx(abilities: 1));
        Assert.AreEqual(Bit("Ability0"), Current(intro));
        Call0(intro, "OnAbilityUsed", 0);
        Assert.AreEqual(31 | Bit("Ability0"), Seen(intro));
    }

    [Test]
    public void BubbleHidesAndTimerPausesWhileArcherIsDown()
    {
        var intro = NewIntro(FreshIsland());
        ExpectStep(intro, Bit("Fire"), Ctx());
        Assert.AreNotEqual("", (string)Prop(intro, "Message"));
        Tick(intro, 30f, Ctx(alive: false));
        Assert.AreEqual("", (string)Prop(intro, "Message"), "okçu ölüyken gizli");
        Assert.AreEqual(Bit("Fire"), Current(intro), "süre durur, adım kaybolmaz");
        Tick(intro, 0.1f, Ctx());
        Assert.AreNotEqual("", (string)Prop(intro, "Message"));
    }

    [Test]
    public void SupportMailtoGoesToTheProjectAddress()
    {
        var ui = T("AudioSettingsUI");
        Assert.AreEqual("miniempirebuilder@gmail.com", (string)ui.GetField("SupportEmail").GetValue(null));
        string url = (string)ui.GetMethod("BuildMailtoUrl").Invoke(null, new object[] { "Mini Empire & Builder", "Merhaba\nşu: a+b" });
        Assert.AreEqual("mailto:miniempirebuilder@gmail.com?subject=Mini%20Empire%20%26%20Builder&body=Merhaba%0A%C5%9Fu%3A%20a%2Bb", url);
        Assert.AreEqual("mailto:miniempirebuilder@gmail.com", (string)ui.GetMethod("BuildMailtoUrl").Invoke(null, new object[] { null, null }));
    }

    // ==================== SAHNE: akışta baloncuk + görüntü ====================

    private IEnumerator OpenIsland(int seenMask)
    {
        yield return LoadIsland(island => { Set(island, "raidIntroSeen", seenMask); Set(island, "tutorialStep", 0); Set(island, "buildTutorial", 5); }, 5, v => devWas = v, fresh: true);
        var raid = Prop(Setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
    }

    private static IEnumerator CaptureAt(string name, int w, int h)
    {
        var camera = (Camera)Prop(Setup, "WorldCamera");
        Directory.CreateDirectory(OutDir);
        var texture = new RenderTexture(w, h, 24);
        var oldAspect = camera.aspect;
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;
        }
        yield return null;   // HUD ok/hap yerleşimi bu kadraja göre yeniden hesaplanır
        yield return null;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.aspect = oldAspect;
        texture.Release();
        Object.Destroy(texture);
    }

    private static IEnumerator WaitForStep(object raid, int bit, float seconds = 25f)
    {
        var intro = raid.GetType().GetField("Intro").GetValue(raid);
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end && Current(intro) != bit) yield return null;
        Assert.AreEqual(bit, Current(intro), "adım sahnede çıktı");
        Assert.AreEqual(1f, Time.timeScale, "oyun donmaz");
        yield return new WaitForSecondsRealtime(0.3f);
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator FireBubbleAppearsInRaidOneAndIsCaptured()
    {
        yield return OpenIsland(0);
        var raid = Prop(Setup, "Raid");
        yield return WaitForStep(raid, Bit("Fire"));
        yield return CaptureAt("g8_baloncuk_ates_telefon_1080x1920", 1080, 1920);
        yield return CaptureAt("g8_baloncuk_ates_tablet_1536x2048", 1536, 2048);
        var intro = raid.GetType().GetField("Intro").GetValue(raid);
        var archer = Prop(Setup, "Archer");
        archer.GetType().GetMethod("PressFire").Invoke(archer, null);
        yield return new WaitForSecondsRealtime(0.2f);
        archer.GetType().GetMethod("ReleaseFire").Invoke(archer, null);
        float end = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < end && (Seen(intro) & Bit("Fire")) == 0) yield return null;
        Assert.AreNotEqual(0, Seen(intro) & Bit("Fire"), "ilk atış ATEŞ adımını kapatıp kayda yazdı");
        Assert.AreNotEqual(0, (int)Field(CurrentIsland(), "raidIntroSeen") & Bit("Fire"), "oyuncu kaydındaki ada verisine yazıldı");
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator MapBubbleAppearsAfterEarlierStepsAndIsCaptured()
    {
        yield return OpenIsland(Bit("Fire") | Bit("Charge") | Bit("Post"));
        var raid = Prop(Setup, "Raid");
        yield return WaitForStep(raid, Bit("Map"));
        yield return CaptureAt("g8_baloncuk_harita_telefon_1080x1920", 1080, 1920);
        yield return CaptureAt("g8_baloncuk_harita_tablet_1536x2048", 1536, 2048);
    }
}
