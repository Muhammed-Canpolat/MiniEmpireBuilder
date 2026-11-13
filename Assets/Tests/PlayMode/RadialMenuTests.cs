using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// AŞAMA 3 §3F: Ana Üs radyal alt menüsü (RadialDockMenu). Yalıtılmış test kaydı; PlayerPrefs "MEB_RadialOpen" geri yüklenir.
/// Ekran görüntüleri <c>PlaytestOutput/ui_night/N3F_*.png</c>.
/// </summary>
public class RadialMenuTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private const string PrefKey = "MEB_RadialOpen";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private int savedPref;
    private bool flagWas;

    [SetUp]
    public void KeepRealState()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
        savedPref = PlayerPrefs.HasKey(PrefKey) ? PlayerPrefs.GetInt(PrefKey) : -1;
        flagWas = (bool)Field(Config, "radialDockMenu");
    }

    [TearDown]
    public void RestoreRealState()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        if (savedPref >= 0) PlayerPrefs.SetInt(PrefKey, savedPref); else PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
        Set(Config, "radialDockMenu", flagWas);
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);
    private static Transform Hud => GameObject.Find("BaseCanvas").transform.Find("SafeArea/HudRoot");
    private static Component Menu => (Component)T("RadialDockMenu").GetProperty("Current").GetValue(null);
    private static System.Collections.Generic.List<object> Items => ((System.Collections.IEnumerable)Prop(Menu, "Items")).Cast<object>().ToList();
    private static object Item(string id) => Items.First(i => (string)Field(i, "id") == id);
    private static bool IsOpen => (bool)Prop(Menu, "IsOpen");
    private static void SetOpen(bool open, bool instant = true, bool save = true) =>
        Menu.GetType().GetMethod("SetOpen").Invoke(Menu, new object[] { open, instant, save });
    private static bool Visible(object item) => (bool)Prop(item, "Visible");

    private static IEnumerator LoadBase(int mainBase, bool guideDone = true)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", 6345); Set(save, "gems", 111); Set(save, "firstBaseGuideDone", guideDone);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
    }

    [UnityTest]
    public IEnumerator FirstLaunchOpensWithFourItemsInOrder()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        yield return LoadBase(7);
        Assert.NotNull(Menu, "radyal menü kurulu");
        Assert.IsTrue(IsOpen, "ilk açılışta menü açık");
        CollectionAssert.AreEqual(new[] { "build", "campaign", "hero", "island" }, Items.Select(i => (string)Field(i, "id")).ToArray());
        Assert.IsTrue(Items.All(Visible), "hepsi görünür");
        Assert.IsNotNull(Hud.Find("RadialMainBtn"));
        foreach (var name in new[] { "BuildBtn", "BattleBtn", "HeroBtn", "IslandBtn" }) Assert.IsNotNull(Hud.Find(name), name);
        Assert.IsNotNull(Hud.Find("BottomDock"), "şerit adı korunur");
        // yay: İNŞA ET ve ADA alçakta, SEFER ve KAHRAMAN yüksekte
        float Y(string id) => ((RectTransform)Prop(Item(id), "Rect")).anchoredPosition.y;
        Assert.Greater(Y("campaign"), Y("build"));
        Assert.Greater(Y("hero"), Y("island"));
    }

    [UnityTest]
    public IEnumerator ToggleChangesItemVisibilityAndPersistsInPlayerPrefs()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        yield return LoadBase(7);
        SetOpen(false);
        Assert.IsFalse(IsOpen);
        Assert.IsTrue(Items.All(i => !Visible(i)), "kapalıyken öğeler gizli");
        Assert.AreEqual(0, PlayerPrefs.GetInt(PrefKey, 1), "kapalı durum kaydedildi");
        SetOpen(true);
        Assert.IsTrue(Items.All(Visible), "açıkken görünür");
        Assert.AreEqual(1, PlayerPrefs.GetInt(PrefKey, 0));
        // Toggle() animasyonlu: bitince yerine oturur
        Menu.GetType().GetMethod("Toggle").Invoke(Menu, null);
        Assert.IsFalse(IsOpen);
        yield return new WaitForSecondsRealtime(0.9f);
        Assert.IsTrue(Items.All(i => !Visible(i)), "kapanma animasyonu bitince gizli");
        // kapalı durum sahne yenilenince hatırlanır
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 40; i++) yield return null;
        Assert.IsFalse(IsOpen, "son durum hatırlanır");
        Assert.IsTrue(Items.All(i => !Visible(i)));
        PlayerPrefs.DeleteKey(PrefKey);
    }

    [UnityTest]
    public IEnumerator IslandItemLockedBelowMainBase5AndUnlockedAtFive()
    {
        yield return LoadBase(4);
        Assert.IsTrue((bool)Prop(Item("island"), "IsLocked"), "Ana Üs 4: ADA kilitli");
        Assert.IsTrue(Hud.Find("IslandBtn/Visual/LockIcon").gameObject.activeSelf, "kilit simgesi");
        StringAssert.Contains("5", TextOf(Hud.Find("IslandBtn/Visual/Label")), "kilitli etiket seviyeyi söyler");
        yield return LoadBase(5);
        Assert.IsFalse((bool)Prop(Item("island"), "IsLocked"), "Ana Üs 5: ADA açık");
        Assert.IsFalse(Hud.Find("IslandBtn/Visual/LockIcon").gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator ItemsAreAtLeast48UnitsAndFitScreen()
    {
        yield return LoadBase(7);
        var scaler = GameObject.Find("BaseCanvas").GetComponent<UnityEngine.UI.CanvasScaler>();
        Assert.LessOrEqual(scaler.referenceResolution.x, 400f, "referans genişlik ~390: 1 birim yaklaşık 1 dp");
        var safe = (RectTransform)GameObject.Find("BaseCanvas").transform.Find("SafeArea");
        var safeCorners = new Vector3[4];
        safe.GetWorldCorners(safeCorners);
        foreach (var item in Items)
        {
            var rt = (RectTransform)Prop(item, "Rect");
            Assert.GreaterOrEqual(Mathf.Min(rt.rect.width, rt.rect.height), 48f, Field(item, "id") + " >= 48 dp");
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].x, safeCorners[0].x);
            Assert.LessOrEqual(corners[2].x, safeCorners[2].x);
        }
        var main = (RectTransform)Prop(Menu, "MainButton");
        Assert.GreaterOrEqual(main.rect.width, 64f);
        Assert.Greater(main.rect.width, ((RectTransform)Prop(Item("build"), "Rect")).rect.width, "ana düğme alt düğmelerden büyük");
    }

    [UnityTest]
    public IEnumerator BadgeProviderShowsDot()
    {
        yield return LoadBase(7);
        var build = Item("build");
        Set(build, "badgeProvider", (Func<int>)(() => 0));
        Menu.GetType().GetMethod("RefreshBadges").Invoke(Menu, null);
        Assert.IsFalse((bool)Prop(build, "BadgeShown"));
        Set(build, "badgeProvider", (Func<int>)(() => 1));
        Menu.GetType().GetMethod("RefreshBadges").Invoke(Menu, null);
        Assert.IsTrue((bool)Prop(build, "BadgeShown"), "sağlayıcı 1: nokta");
        Set(build, "badgeProvider", (Func<int>)(() => 5));
        Menu.GetType().GetMethod("RefreshBadges").Invoke(Menu, null);
        Assert.AreEqual("5", TextOf(Hud.Find("BuildBtn/Visual/Badge/Count")), "N>1: sayı");
    }

    [UnityTest]
    public IEnumerator FlagOffKeepsOldDock()
    {
        Set(Config, "radialDockMenu", false);
        yield return LoadBase(7);
        Assert.IsNull(Hud.Find("RadialMainBtn"), "bayrak kapalı: radyal ana düğme yok");
        var battle = (RectTransform)Hud.Find("BattleBtn");
        Assert.IsTrue(battle.gameObject.activeSelf);
        Assert.Greater(battle.sizeDelta.x, 100f, "eski geniş dock düğmesi");
        Assert.IsNotNull(Hud.Find("IslandBtn"));
        Set(Config, "radialDockMenu", flagWas);
    }

    [UnityTest]
    public IEnumerator FirstBaseGuideStillFindsCampaignButtonAndReopensClosedMenu()
    {
        yield return LoadBase(7, guideDone: false);
        var worldUi = (Component)Object.FindFirstObjectByType(T("BaseWorldUI"));
        var button = (UnityEngine.UI.Button)Prop(worldUi, "BattleButton");
        Assert.NotNull(button);
        Assert.AreEqual("BattleBtn", button.name);
        Assert.AreSame(Field(Item("campaign"), "button"), button, "SEFER öğesi = rehberin bulduğu düğme");
        Assert.AreEqual(Hud, button.transform.parent);
        SetOpen(false);
        Assert.IsFalse(button.gameObject.activeSelf);
        var guide = Object.FindFirstObjectByType(T("FirstBaseGuide")) as Component;
        if (guide != null)   // rehber 2. adıma geçerken kapalı menüyü açar
        {
            guide.GetType().GetField("step", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(guide, 0);
            guide.GetType().GetMethod("OnMineTapped").Invoke(guide, null);
            Assert.IsTrue(IsOpen, "rehber SEFER'i göstermek için menüyü açtı");
            Assert.IsTrue(button.gameObject.activeSelf);
        }
    }

    [UnityTest]
    public IEnumerator Screenshots()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        yield return LoadBase(7);
        SetOpen(false);
        for (int i = 0; i < 5; i++) yield return null;
        yield return Capture("N3F_radial_closed", 1080, 1920);
        SetOpen(true, false);
        yield return new WaitForSecondsRealtime(1.0f);
        yield return Capture("N3F_radial_open", 1080, 1920);
        yield return Capture("N3F_radial_open_3x4", 1080, 1440);
        SetOpen(false);
        yield return new WaitForSecondsRealtime(0.5f);
        SetOpen(true, false);
        yield return new WaitForSecondsRealtime(0.18f);
        yield return Capture("N3F_radial_burst", 1080, 1920);
    }

    private static string TextOf(Component c)
    {
        var tmp = c.GetComponent("TextMeshProUGUI");
        return (string)tmp.GetType().GetProperty("text").GetValue(tmp);
    }

    private static IEnumerator Capture(string name, int w, int h)
    {
        yield return null;
        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/ui_night"));
        Directory.CreateDirectory(folder);
        var texture = new RenderTexture(w, h, 24);
        var rect = camera.rect;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        var changed = new System.Collections.Generic.List<(Canvas canvas, RenderMode mode, Camera cam, int order, bool over)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            changed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.sortingOrder, canvas.overrideSorting));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000 + canvas.sortingOrder;
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.rect = rect;
        camera.ResetAspect();
        texture.Release();
        Object.Destroy(texture);
        foreach (var (canvas, mode, cam, order, over) in changed)
        {
            if (canvas == null) continue;
            canvas.renderMode = mode; canvas.worldCamera = cam; canvas.sortingOrder = order; canvas.overrideSorting = over;
        }
    }
}
