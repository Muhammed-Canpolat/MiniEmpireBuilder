using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// Ana Üs yeni UI (D106) görüntü yakalayıcı + kısa denetim. Parametreler ortam değişkeni (boşsa varsayılan):
/// UI_CAPTURE_W/H (1080x1920), UI_CAPTURE_OUT (Docs/playtest/ui-ana-us/ui-capture.png, proje köküne göre),
/// UI_CAPTURE_FILTER (point | bilinear; boş = importer'ın ayarı), UI_CAPTURE_GOLD (2581), UI_CAPTURE_LEVEL (7).
/// Sonraki bölümler (9:21, 4:3) aynı testi başka çözünürlükle koşar.
/// </summary>
public class UiAnaUsCapture
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;

    private static string Env(string name, string fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

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
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
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

    private static IEnumerator LoadBase(int mainBase, int meat, int wood, int gold, int gems)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", gold);
        Set(save, "gems", gems);
        Set(save, "firstBaseGuideDone", true);
        var island = Field(save, "island");
        Set(island, "meat", meat); Set(island, "wood", wood);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.0f);
        T("AwayReportUI").GetMethod("CloseIfOpen").Invoke(null, null);   // "Sen yokken" penceresi görüntüyü kapatmasın
        yield return null;
    }

    private static Transform Hud => GameObject.Find("BaseCanvas").transform.Find("SafeArea/HudRoot");

    private static string TextOf(Transform t)
    {
        var tmp = t.GetComponent("TextMeshProUGUI");
        return tmp != null ? (string)tmp.GetType().GetProperty("text").GetValue(tmp) : null;
    }

    [UnityTest]
    public IEnumerator CaptureBaseTopBar()
    {
        int w = int.Parse(Env("UI_CAPTURE_W", "1080")), h = int.Parse(Env("UI_CAPTURE_H", "1920"));
        string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Env("UI_CAPTURE_OUT", "Docs/playtest/ui-ana-us/ui-capture.png")));
        int gold = int.Parse(Env("UI_CAPTURE_GOLD", "2581")), level = int.Parse(Env("UI_CAPTURE_LEVEL", "7"));
        yield return LoadBase(level, 43, 227, gold, 8);

        var hud = Hud;
        Assert.NotNull(hud);
        foreach (var name in new[] { "TopBarBG", "GoldPill", "GemPill", "BuilderPill", "LevelCrest", "ResourceTopBar/MeatPill", "ResourceTopBar/WoodPill" })
            Assert.NotNull(hud.Find(name), name);
        Assert.AreEqual(gold.ToString(), TextOf(hud.Find("GoldPill/GoldText")), "altın canlı");
        Assert.AreEqual("8", TextOf(hud.Find("GemPill/GemText")), "elmas canlı");
        Assert.AreEqual(level.ToString(), TextOf(hud.Find("LevelCrest/Number")), "seviye kalkanda");
        Assert.AreEqual("43", TextOf(hud.Find("ResourceTopBar/MeatPill/Value")));
        Assert.AreEqual("227", TextOf(hud.Find("ResourceTopBar/WoodPill/Value")));
        var gps = hud.Find("GoldPill/GpsText");
        Assert.IsTrue(gps == null || !gps.gameObject.activeInHierarchy, "GpsText yeni barda gösterilmez");

        string filter = Env("UI_CAPTURE_FILTER", "").ToLowerInvariant();
        if (filter == "point" || filter == "bilinear")
        {
            var mode = filter == "point" ? FilterMode.Point : FilterMode.Bilinear;
            foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (image.sprite != null && image.sprite.texture != null && AssetPathIsMiniEmpire(image.sprite))
                    image.sprite.texture.filterMode = mode;
        }
        yield return null;

        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        var camera = Camera.main;
        Assert.NotNull(camera);
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
        File.WriteAllBytes(outPath, pixels.EncodeToPNG());
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
        Debug.Log("UiAnaUsCapture wrote " + outPath);
    }

    private static readonly string[] PackTextures =
    {
        "topbar_bg", "res_pill_bg", "level_shield_empty", "banner_left", "banner_right", "btn_settings",
        "icon_gold", "icon_wood", "icon_meat", "icon_gem", "icon_builder"
    };

    private static bool AssetPathIsMiniEmpire(Sprite sprite) => Array.IndexOf(PackTextures, sprite.texture.name) >= 0;
}
