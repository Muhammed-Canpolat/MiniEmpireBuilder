using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// Radyal menüyü varsayan testlerin (RadialMenuTests, Session3/4/5, Nov13, Viewport) yerine eski dalı AÇIKÇA sınamak için
/// "miniEmpireBottomBar" bayrağını kapatır (D107). Kapalıyken bugünkü radyal menü davranışı aynen çalışır.
/// </summary>
internal static class LegacyDockFlag
{
    private static bool? was;
    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);
    public static void Off()
    {
        var field = Config.GetType().GetField("miniEmpireBottomBar");
        if (was == null) was = (bool)field.GetValue(Config);
        field.SetValue(Config, false);
    }
    public static void Restore()
    {
        if (was == null) return;
        Config.GetType().GetField("miniEmpireBottomBar").SetValue(Config, was.Value);
        was = null;
    }
}

/// <summary>Ana Üs yeni alt bar (D107): kurulum, ok kaydırma + kalıcılık, kamera sabitliği, modal gizleme, rehber, bayrak kapalı davranışı.</summary>
public class MiniEmpireBottomBarTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private const string OpenKey = "MEB_BaseBarOpen";
    private string savedKey, savedBackup, savedFile;
    private int savedOpen;

    private static string SaveFile => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    [SetUp]
    public void Keep()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = System.IO.File.Exists(SaveFile) ? System.IO.File.ReadAllText(SaveFile) : null;
        savedOpen = PlayerPrefs.HasKey(OpenKey) ? PlayerPrefs.GetInt(OpenKey) : -1;
        LegacyDockFlag.Restore();
    }

    [TearDown]
    public void Restore()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) System.IO.File.WriteAllText(SaveFile, savedFile);
        else if (System.IO.File.Exists(SaveFile)) System.IO.File.Delete(SaveFile);
        if (savedOpen >= 0) PlayerPrefs.SetInt(OpenKey, savedOpen); else PlayerPrefs.DeleteKey(OpenKey);
        PlayerPrefs.Save();
        LegacyDockFlag.Restore();
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
    }

    private static Transform Hud => GameObject.Find("BaseCanvas").transform.Find("SafeArea/HudRoot");
    private static Component Bar => Hud.Find("BottomDock").GetComponent("MiniEmpireBottomBar");
    private static R Call<R>(Component c, string method, params object[] args) => (R)c.GetType().GetMethod(method).Invoke(c, args);
    private static string LabelOf(Transform t)
    {
        foreach (var c in t.GetComponentsInChildren<Component>(true))
            if (c != null && c.GetType().Name == "TextMeshProUGUI") return (string)c.GetType().GetProperty("text").GetValue(c);
        return null;
    }
    private static CanvasGroup Group(string button) => Hud.Find(button).GetComponent<CanvasGroup>();

    private static IEnumerator LoadBase(int mainBase, bool guideDone = true)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", 5000);
        Set(save, "firstBaseGuideDone", guideDone);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        T("AwayReportUI").GetMethod("CloseIfOpen").Invoke(null, null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator BarBuildsFourButtonsWithoutRadialAndKeepsNames()
    {
        PlayerPrefs.DeleteKey(OpenKey);
        yield return LoadBase(3);
        var hud = Hud;
        foreach (var name in new[] { "BottomDock", "BuildBtn", "BattleBtn", "HeroBtn", "IslandBtn", "BarArrowTab" }) Assert.NotNull(hud.Find(name), name);
        Assert.IsNull(hud.Find("RadialMainBtn"), "yeni barda radyal menü kurulmaz");
        Assert.IsNull(T("RadialDockMenu").GetProperty("Current").GetValue(null), "RadialDockMenu.Current null");
        Assert.NotNull(Bar);
        Assert.IsTrue(Call<bool>(Bar, "get_IsOpen"), "ilk açılışta açık");
        foreach (var name in new[] { "BuildBtn", "BattleBtn", "HeroBtn", "IslandBtn" })
            Assert.IsNotNull(hud.Find(name).Find("Visual/Frame"), name + " yeni çerçeveyle giydirildi");
        Assert.AreEqual("ADA · 5", LabelOf(hud.Find("IslandBtn")), "kilitli ADA etiketi");
    }

    /// <summary>D125 (İş 2): Ana Üs kamerası tam ekran çizer; 9:16, 9:21 ve 4:3'te alt %15 ve üst çentik bandı #14110D (siyah) değil, orman.</summary>
    [UnityTest]
    public IEnumerator OuterGroundFillsBottomAndNotchAtAllRatios()
    {
        PlayerPrefs.SetInt(OpenKey, 0);
        yield return LoadBase(7);
        var cam = Camera.main;
        var wallsType = T("BaseCastleWalls");
        var bandProp = wallsType.GetProperty("FramingBand");
        var apply = wallsType.GetMethod("ApplyFraming");
        var bars = Object.FindFirstObjectByType(T("WorldViewportBars")) as Component;
        Assert.IsTrue((bool)bars.GetType().GetProperty("FullBleed").GetValue(bars), "Ana Üs fullBleed");
        Rect oldRect = cam.rect; float oldSize = cam.orthographicSize; Vector3 oldPos = cam.transform.position;
        foreach (var (w, h, name) in new[] { (360, 640, "9:16"), (360, 840, "9:21"), (768, 1024, "4:3") })
        {
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.aspect = (float)w / h;
            bandProp.SetValue(null, new Rect(0f, h * 0.11f, w, h * 0.80f));   // iki bar arası (alt dock ~%11, üst bar ~%9)
            Assert.IsTrue((bool)apply.Invoke(null, new object[] { cam }), name);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            int dark = 0, total = 0;
            var bg = new Color32(0x14, 0x11, 0x0D, 255);
            void Scan(int y0, int y1)
            {
                for (int y = y0; y < y1; y += 2)
                for (int x = 0; x < w; x += 2)
                {
                    var c = (Color32)tex.GetPixel(x, y);
                    total++;
                    if (Mathf.Abs(c.r - bg.r) < 8 && Mathf.Abs(c.g - bg.g) < 8 && Mathf.Abs(c.b - bg.b) < 8) dark++;
                }
            }
            Scan(0, (int)(h * 0.15f));              // alt %15
            Scan((int)(h * 0.97f), h);              // üst çentik bandı
            var sample = (Color32)tex.GetPixel(w / 2, 2);
            Object.Destroy(tex);
            cam.targetTexture = null;
            rt.Release(); Object.Destroy(rt);
            Assert.AreEqual(0, dark, name + ": #14110D (siyah) piksel var (" + dark + "/" + total + ")");
            Assert.Greater(sample.g, sample.b, name + ": alt orta piksel orman yeşili değil");
        }
        cam.rect = oldRect; cam.ResetAspect(); cam.orthographicSize = oldSize; cam.transform.position = oldPos;
        bars.GetType().GetMethod("UpdateLayout").Invoke(bars, null);
    }

    private static System.Collections.Generic.List<Vector3> FarBeltPositions()
    {
        var root = GameObject.Find("ForestFarBelt");
        Assert.NotNull(root, "ForestFarBelt");
        var list = new System.Collections.Generic.List<Vector3>();
        foreach (Transform child in root.transform) { list.Add(child.position); Assert.IsNull(child.GetComponent<Collider2D>(), "collider yok"); }
        return list;
    }

    /// <summary>D125: dış dekor sabit tohumlu; iki açılışta ForestFarBelt konumları aynı, ≤32 nesne, kayda girmez.</summary>
    [UnityTest]
    public IEnumerator ForestFarBeltIsStableAcrossTwoLoads()
    {
        yield return LoadBase(7);
        var first = FarBeltPositions();
        Assert.That(first.Count, Is.InRange(1, 32));
        yield return LoadBase(7);
        var second = FarBeltPositions();
        Assert.AreEqual(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i], second[i], "far belt " + i);
    }

    [UnityTest]
    public IEnumerator ArrowTogglesBarPersistsAndNeverMovesCamera()
    {
        PlayerPrefs.DeleteKey(OpenKey);
        yield return LoadBase(7);
        var cam = Camera.main;
        float ortho = cam.orthographicSize; Rect pixel = cam.pixelRect; Vector3 pos = cam.transform.position;
        var dock = (RectTransform)Hud.Find("BottomDock");
        float dockTop = dock.TransformPoint(new Vector3(0f, dock.rect.yMax, 0f)).y;
        Hud.Find("BarArrowTab").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsFalse(Call<bool>(Bar, "get_IsOpen"));
        Assert.AreEqual(0, PlayerPrefs.GetInt(OpenKey, 1), "kapalı durum MEB_BaseBarOpen'da");
        Assert.AreEqual(ortho, cam.orthographicSize, 1e-4f);
        Assert.AreEqual(pixel, cam.pixelRect);
        Assert.AreEqual(pos, cam.transform.position);
        Assert.AreEqual(dockTop, dock.TransformPoint(new Vector3(0f, dock.rect.yMax, 0f)).y, 0.01f, "dock sabit");
        foreach (var name in new[] { "BuildBtn", "BattleBtn", "HeroBtn", "IslandBtn" })
        {
            Assert.IsFalse(Group(name).blocksRaycasts, name + " dokunuş yutmaz");
            Assert.AreEqual(0f, Group(name).alpha, 0.01f);
        }
        Assert.IsTrue(Hud.Find("BarArrowTab").gameObject.activeInHierarchy, "ok sekmesi görünür kalır");
        Hud.Find("BarArrowTab").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsTrue(Call<bool>(Bar, "get_IsOpen"));
        Assert.IsTrue(Group("BattleBtn").blocksRaycasts);
        Assert.AreEqual(1, PlayerPrefs.GetInt(OpenKey, 0));
    }

    [UnityTest]
    public IEnumerator ClosedStatePersistsAcrossSceneLoadAndModalHidesAndShows()
    {
        PlayerPrefs.SetInt(OpenKey, 0);
        yield return LoadBase(7);
        Assert.IsFalse(Call<bool>(Bar, "get_IsOpen"), "kapalı durum sahne yüklemesinde korunur");
        Call<object>(Bar, "SetOpen", true, false, false);
        Assert.AreEqual(0, PlayerPrefs.GetInt(OpenKey, 1), "persist=false tercihi bozmaz");
        var ui = T("BaseWorldUI").GetProperty("Instance").GetValue(null);
        ui.GetType().GetMethod("ToggleBottomButtons").Invoke(ui, new object[] { false });
        Assert.AreEqual(0f, Group("BuildBtn").alpha, 0.01f, "modal: gizli");
        Assert.IsFalse(Group("BuildBtn").blocksRaycasts);
        ui.GetType().GetMethod("ToggleBottomButtons").Invoke(ui, new object[] { true });
        Assert.AreEqual(1f, Group("BuildBtn").alpha, 0.01f, "modal sonrası görünür");
        Assert.IsTrue(Group("BuildBtn").blocksRaycasts);
    }

    [UnityTest]
    public IEnumerator IslandButtonLockedBelowFiveAndOpensAtFive()
    {
        PlayerPrefs.DeleteKey(OpenKey);
        yield return LoadBase(3);
        var island = Hud.Find("IslandBtn");
        Assert.AreEqual("ADA · 5", LabelOf(island));
        yield return new WaitForSecondsRealtime(0.7f);
        Assert.Less(island.Find("Visual/Frame").GetComponent<Image>().color.r, 0.99f, "kilitliyken gri ton");
        yield return LoadBase(5);
        Assert.AreEqual("ADA", LabelOf(Hud.Find("IslandBtn")));
    }

    [UnityTest]
    public IEnumerator FirstBaseGuideReopensClosedBarWithoutChangingPreference()
    {
        PlayerPrefs.SetInt(OpenKey, 0);
        yield return LoadBase(1, guideDone: false);
        var guide = Object.FindFirstObjectByType(T("FirstBaseGuide"));
        Assume.That(guide != null, "rehber bu kayıtta başlamadı");
        T("FirstBaseGuide").GetMethod("OnMineTapped").Invoke(guide, null);
        Assert.IsTrue(Call<bool>(Bar, "get_IsOpen"), "rehber barı açar");
        Assert.AreEqual(0, PlayerPrefs.GetInt(OpenKey, 1), "tercih bozulmaz");
    }

    [UnityTest]
    public IEnumerator FlagOffKeepsRadialMenu()
    {
        LegacyDockFlag.Off();
        yield return LoadBase(7);
        Assert.IsNull(Hud.Find("BarArrowTab"), "bayrak kapalı: yeni bar yok");
        Assert.NotNull(Hud.Find("RadialMainBtn"), "radyal menü bugünkü gibi");
        LegacyDockFlag.Restore();
    }
}
