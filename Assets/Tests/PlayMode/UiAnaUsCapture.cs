using System;
using System.Collections;
using System.IO;
using System.Linq;
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

    internal static IEnumerator LoadBase(int mainBase, int meat, int wood, int gold, int gems, Action<object> edit = null)
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
        edit?.Invoke(save);
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
        Assert.AreEqual(Call("NumberFormat", "Short", (long)gold, (long)(int)Field(T("FeatureConfig").GetProperty("Instance").GetValue(null), "topBarShortFrom")), TextOf(hud.Find("GoldPill/GoldText")), "altın canlı");
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

        Shoot(w, h, outPath);
    }

    /// <summary>Oyundaki kamerayla (duvar kadrajı + bar bandı) çeker; tüm oranlarda aynı yol.</summary>
    private static void Shoot(int w, int h, string outPath) => ShootFramed(w, h, outPath);

    /// <summary>Alt bar (D107): bar açık ve kapalı görüntüsü + canlı denetimler. UI_CAPTURE_OUT = açık görüntü; kapalı olan "-kapali" ekiyle yazılır.</summary>
    [UnityTest]
    public IEnumerator CaptureBaseBottomBar()
    {
        int w = int.Parse(Env("UI_CAPTURE_W", "1080")), h = int.Parse(Env("UI_CAPTURE_H", "1920"));
        string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Env("UI_CAPTURE_OUT", "Docs/playtest/ui-ana-us/02-altbar.png")));
        PlayerPrefs.SetInt("MEB_BaseBarOpen", 1);
        yield return LoadBase(7, 43, 227, 2581, 8);
        var hud = Hud;
        foreach (var name in new[] { "BottomDock", "BuildBtn", "BattleBtn", "HeroBtn", "IslandBtn", "BarArrowTab" })
            Assert.NotNull(hud.Find(name), name);
        Assert.IsNull(hud.Find("RadialMainBtn"), "yeni barda radyal menü kurulmaz");
        var bar = (Component)hud.Find("BottomDock").GetComponent("MiniEmpireBottomBar");
        Assert.NotNull(bar, "alt bar bileşeni");
        var dock = (RectTransform)hud.Find("BottomDock");
        float dockTop = dock.TransformPoint(new Vector3(0f, dock.rect.yMax, 0f)).y;
        var cam = Camera.main;
        float ortho = cam.orthographicSize, camY = cam.transform.position.y;
        Rect pixel = cam.pixelRect;
        yield return null;
        Shoot(w, h, outPath);

        var setOpen = bar.GetType().GetMethod("SetOpen");
        setOpen.Invoke(bar, new object[] { false, false, false });
        yield return null; yield return null; yield return null;
        Assert.AreEqual(ortho, cam.orthographicSize, 0.0001f, "kamera zoom bar kapanınca değişmez");
        Assert.AreEqual(camY, cam.transform.position.y, 0.0001f, "kamera konumu değişmez");
        Assert.AreEqual(pixel, cam.pixelRect, "görüş alanı değişmez");
        Assert.AreEqual(dockTop, dock.TransformPoint(new Vector3(0f, dock.rect.yMax, 0f)).y, 0.01f, "BottomDock sabit");
        foreach (var name in new[] { "BuildBtn", "BattleBtn", "HeroBtn", "IslandBtn" })
            Assert.IsFalse(hud.Find(name).GetComponent<CanvasGroup>().blocksRaycasts, name + " kapalıyken dokunuş yutmaz");
        Assert.IsTrue(hud.Find("BarArrowTab").GetComponent<CanvasGroup>().blocksRaycasts, "ok sekmesi dokunulabilir");
        string closed = Path.Combine(Path.GetDirectoryName(outPath), Path.GetFileNameWithoutExtension(outPath) + "-kapali.png");
        Shoot(w, h, closed);
        setOpen.Invoke(bar, new object[] { true, false, false });
        PlayerPrefs.DeleteKey("MEB_BaseBarOpen");
    }

    // ==================== D108: PixelCastle duvarları + kadraj ====================

    /// <summary>Dolu kayıt (UI_CAPTURE_FULL=1, varsayılan): Ana Üs 7, açılmış arazi, referanstaki dizilime yakın binalar + dekor.</summary>
    internal static void FillReferenceBase(object save, int landTier)
    {
        Set(save, "landOpened", landTier);
        Set(Field(save, "island"), "announced", true);   // "Ufukta bir ada!" afişi görüntüyü kapatmasın
        var placed = (System.Collections.IList)Field(save, "placedBuildings");
        var present = placed.Cast<object>().Select(e => (string)Field(e, "slotId")).ToList();
        foreach (var (slot, type, level) in new[] {
                     ("main", "MainBase", 7), ("mine_1", "GoldMine", 5), ("archer_1", "ArcherTower", 4), ("barracks_1", "Barracks", 3),
                     ("barracks_2", "Barracks", 1), ("house_1", "House", 3), ("house_2", "House", 2),
                     ("workshop_1", "CardWorkshop", 2), ("cannon_3", "CannonTower", 2) })
        {
            if (present.Contains(slot)) continue;
            var entry = Activator.CreateInstance(T("PlacedBuildingData"));
            Set(entry, "slotId", slot); Set(entry, "buildingType", type); Set(entry, "level", level);
            placed.Add(entry);
        }
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
        {
            string type = Field(entry, "buildingType").ToString();
            if (type == "MainBase" || type == "GoldMine") continue;
            Set(entry, "isUnlocked", true);
            Set(entry, "level", 1);
        }
        var decor = (System.Collections.IList)Field(save, "placedDecor");
        int n = 0;
        foreach (var (id, x, y) in new[] { ("bush", 3, 33), ("bush2", 17, 33), ("bush3", 19, 6), ("bush", 2, 4) })
        {
            var record = Activator.CreateInstance(T("PlacedDecorSave"));
            Set(record, "instanceId", "capture_" + n++); Set(record, "decorId", id); Set(record, "x", x); Set(record, "y", y);
            decor.Add(record);
        }
    }

    private static bool Full => Env("UI_CAPTURE_FULL", "1") == "1";
    private static string CaptureDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", Env("UI_CAPTURE_DIR", "Docs/playtest/ui-ana-us")));

    /// <summary>
    /// D108: duvarlar + kadraj. Çıktılar (UI_CAPTURE_DIR, varsayılan Docs/playtest/ui-ana-us): 03-duvar-9x16/9x19.5/9x21/4x3.png,
    /// 03-duvar-tasima.png (bina taşıma anı). Denetim: duvar kökünde collider yok, kamera zoom/kaydırma etkisiz, kadraj duvarı kapsar.
    /// </summary>
    [UnityTest]
    public IEnumerator CaptureBaseWalls()
    {
        string dir = CaptureDir;
        PlayerPrefs.SetInt("MEB_BaseBarOpen", 1);
        if (Full) yield return LoadBase(7, 43, 227, 2581, 8, save => FillReferenceBase(save, 4));
        else yield return LoadBase(7, 43, 227, 2581, 8);
        yield return new WaitForSecondsRealtime(1.0f);
        T("AwayReportUI").GetMethod("CloseIfOpen").Invoke(null, null);
        var walls = GameObject.Find("CastleWalls");
        Assert.NotNull(walls, "PixelCastle duvarları kuruldu");
        Assert.AreEqual(0, walls.GetComponentsInChildren<Collider2D>(true).Length, "duvarda collider yok");
        Assert.IsNull(walls.GetComponentInChildren<Rigidbody2D>(true), "duvarda Rigidbody yok");
        var cam = Camera.main;
        float ortho = cam.orthographicSize; Vector3 pos = cam.transform.position;
        var controller = cam.GetComponent(T("BaseCameraController"));
        controller.GetType().GetMethod("ZoomBy").Invoke(controller, new object[] { 0.5f, new Vector2(100f, 100f) });
        controller.GetType().GetMethod("EdgeScroll").Invoke(controller, new object[] { new Vector2(1f, 1f) });
        controller.GetType().GetMethod("EnsureVisible").Invoke(controller, new object[] { new Vector2(2f, 3f) });
        Assert.AreEqual(ortho, cam.orthographicSize, 1e-4f, "Ana Üs'te zoom yok");
        Assert.AreEqual(pos, cam.transform.position, "kenar kaydırma/odak kamerayı oynatmaz");
        var current = T("BaseCastleWalls").GetProperty("Current").GetValue(null);
        var frame = (Rect)current.GetType().GetProperty("FrameBounds").GetValue(current);
        Vector3 lo = cam.WorldToViewportPoint(new Vector3(frame.xMin + 1.1f, frame.yMin + 0.3f));
        Vector3 hi = cam.WorldToViewportPoint(new Vector3(frame.xMax - 1.1f, frame.yMax - 1.2f));
        Assert.IsTrue(lo.x >= -0.001f && lo.y >= -0.001f && hi.x <= 1.001f && hi.y <= 1.001f, $"duvarlar görüş alanında {lo} {hi}");

        foreach (var (name, w, h) in new[] { ("9x16", 1080, 1920), ("9x19.5", 1080, 2340), ("9x21", 1080, 2520), ("4x3", 1536, 2048) })
            ShootFramed(w, h, Path.Combine(dir, "03-duvar-" + name + ".png"));

        // Bina taşıma anı (bug 1: yeşil ekran yok, kamera kaymaz).
        var map = T("BaseMapManager").GetProperty("Instance").GetValue(null);
        var mine = ((System.Collections.IEnumerable)map.GetType().GetProperty("MapObjects").GetValue(map)).Cast<object>()
            .First(o => Field(o, "buildingType")?.ToString() == "GoldMine");
        var placement = T("PlacementController").GetProperty("Instance").GetValue(null);
        placement.GetType().GetMethod("BeginMove").Invoke(placement, new[] { mine });
        yield return null;
        controller.GetType().GetMethod("EdgeScroll").Invoke(controller, new object[] { new Vector2(2f, 2f) });
        yield return null;
        Assert.AreEqual(ortho, cam.orthographicSize, 1e-4f, "taşımada zoom değişmez");
        Assert.AreEqual(pos, cam.transform.position, "taşımada kamera kaymaz");
        ShootFramed(1080, 1920, Path.Combine(dir, "03-duvar-tasima.png"));
        placement.GetType().GetMethod("CancelPlacement").Invoke(placement, null);
        yield return null;
        PlayerPrefs.DeleteKey("MEB_BaseBarOpen");
    }

    /// <summary>D108: en küçük (0) ve en büyük (4) arazi genişlemesinde duvar aynı çit çizgisine oturur (dinamik kurulum kanıtı).</summary>
    [UnityTest]
    public IEnumerator CaptureBaseWallsLandTiers()
    {
        string dir = CaptureDir;
        foreach (var (tier, mb, file) in new[] { (0, 1, "03-duvar-arazi-en-kucuk.png"), (4, 13, "03-duvar-arazi-en-buyuk.png") })
        {
            yield return LoadBase(mb, 43, 227, 2581, 8, save => { Set(save, "landOpened", tier); Set(Field(save, "island"), "announced", true); });
            Assert.NotNull(GameObject.Find("CastleWalls"), "duvar " + tier);
            ShootFramed(1080, 1920, Path.Combine(dir, file));
        }
    }

    /// <summary>
    /// Oyundaki dünya kadrajının aynısı: kadraj, üst ve alt bar arasındaki alanın en-boyuna göre <c>BaseCastleWalls.ApplyFraming</c>
    /// ile hesaplanır (WorldViewportBars gibi); tek kamera tam ekran çizer, ölçek/merkez o alana denk gelecek şekilde kaydırılır
    /// (barların arkasında koyu dolgu yerine dünyanın devamı görünür; aradaki alan oyundakiyle birebir).
    /// </summary>
    internal static void ShootFramed(int w, int h, string outPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        var camera = Camera.main;
        Assert.NotNull(camera);
        var texture = new RenderTexture(w, h, 24);
        Rect oldRect = camera.rect; float oldSize = camera.orthographicSize; Vector3 oldPos = camera.transform.position;
        camera.targetTexture = texture;
        camera.rect = new Rect(0, 0, 1, 1);
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
        float upper = h, lower = 0f;
        var hud = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea/HudRoot");
        var corners = new Vector3[4];
        var top = hud != null ? hud.Find("TopBarBG") as RectTransform : null;
        var bottom = hud != null ? hud.Find("BottomDock") as RectTransform : null;
        if (top != null) { top.GetWorldCorners(corners); upper = camera.WorldToScreenPoint(corners[0]).y; }
        if (bottom != null) { bottom.GetWorldCorners(corners); lower = camera.WorldToScreenPoint(corners[2]).y; }
        float band = Mathf.Max(1f, upper - lower);
        camera.aspect = w / band;
        T("BaseCastleWalls").GetProperty("FramingBand").SetValue(null, default(Rect));   // D125: bant kadrajı burada elle hesaplanır
        if (!(bool)T("BaseCastleWalls").GetMethod("ApplyFraming").Invoke(null, new object[] { camera }))
        {
            camera.orthographicSize = Mathf.Max(22f / (2f * camera.aspect), 19f);
            camera.transform.position = new Vector3(11f, 19f, camera.transform.position.z);
        }
        float shotSize = camera.orthographicSize;
        Vector3 center = camera.transform.position;
        float perPixel = 2f * shotSize / band;
        camera.aspect = (float)w / h;
        camera.orthographicSize = shotSize * h / band;
        camera.transform.position = new Vector3(center.x, center.y - ((lower + upper) * 0.5f - h * 0.5f) * perPixel, center.z);
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
        camera.rect = oldRect;
        foreach (var (canvas, mode, cam, order, over) in changed)
        {
            if (canvas == null) continue;
            canvas.renderMode = mode; canvas.worldCamera = cam; canvas.sortingOrder = order; canvas.overrideSorting = over;
        }
        texture.Release();
        Object.Destroy(texture);
        camera.ResetAspect();
        camera.orthographicSize = oldSize; camera.transform.position = oldPos;
        Canvas.ForceUpdateCanvases();
        var bars = Object.FindFirstObjectByType(T("WorldViewportBars"));
        if (bars != null) bars.GetType().GetMethod("UpdateLayout").Invoke(bars, null);
        Debug.Log($"UiAnaUsCapture wrote {outPath} (world y {lower:0}-{upper:0} px, ortho {shotSize:0.00})");
    }

    /// <summary>
    /// D109: sağ üst droplist. Çıktılar (UI_CAPTURE_DIR): 04-droplist-kapali.png (rozetli), 04-droplist-acik.png (rozetli, açık),
    /// 04-droplist-etkinlik.png ("Yakında" kutusu). Denetim: eski sol düğmeler yok, ok düğmesi + satır sayısı.
    /// </summary>
    [UnityTest]
    public IEnumerator CaptureBaseDropMenu()
    {
        string dir = CaptureDir;
        int cw = int.Parse(Env("UI_CAPTURE_W", "1080")), ch = int.Parse(Env("UI_CAPTURE_H", "1920"));
        string name = Env("UI_CAPTURE_NAME", "");   // doluysa: <ad>-kapali.png / <ad>-droplist-acik.png
        int goldAmount = int.Parse(Env("UI_CAPTURE_GOLD", "2581"));
        PlayerPrefs.SetInt("MEB_BaseBarOpen", 1);
        if (Full) yield return LoadBase(7, 43, 227, goldAmount, 8, save => FillReferenceBase(save, 4));
        else yield return LoadBase(7, 43, 227, goldAmount, 8);
        yield return new WaitForSecondsRealtime(1.0f);
        T("AwayReportUI").GetMethod("CloseIfOpen").Invoke(null, null);
        T("DailyRewardsUI").GetMethod("CloseIfOpen").Invoke(null, null);
        yield return null;
        var hud = Hud;
        Assert.IsNull(GameObject.Find("DailyButton"), "eski GÜNLÜK düğmesi yok");
        Assert.IsNull(GameObject.Find("QuestsButton"), "eski GÖREV düğmesi yok");
        var menu = (Component)hud.GetComponentInChildren(T("BaseDropMenu"), true);
        Assert.NotNull(menu, "BaseDropMenu");
        var type = menu.GetType();
        type.GetMethod("Refresh").Invoke(menu, null);
        Assert.IsTrue((bool)type.GetProperty("ToggleBadgeShown").GetValue(menu), "günlük ödül hazır: ok düğmesinde rozet");
        ShootFramed(cw, ch, Path.Combine(dir, name == "" ? "04-droplist-kapali.png" : name + "-kapali.png"));
        type.GetMethod("Open").Invoke(menu, null);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(3, (int)type.GetProperty("RowCount").GetValue(menu));
        ShootFramed(cw, ch, Path.Combine(dir, name == "" ? "04-droplist-acik.png" : name + "-droplist-acik.png"));
        type.GetMethod("Close").Invoke(menu, null);
        yield return new WaitForSecondsRealtime(0.4f);
        type.GetMethod("ShowComingSoon").Invoke(menu, null);
        yield return null;
        if (name == "") ShootFramed(cw, ch, Path.Combine(dir, "04-droplist-etkinlik.png"));
        type.GetMethod("CloseComingSoon").Invoke(menu, null);
        PlayerPrefs.DeleteKey("MEB_BaseBarOpen");
    }

    private static readonly string[] PackTextures =
    {
        "topbar_bg", "res_pill_bg", "level_shield_empty", "banner_left", "banner_right", "btn_settings",
        "icon_gold", "icon_wood", "icon_meat", "icon_gem", "icon_builder",
        "bottombar_bg", "menu_button_frame_noplate", "menu_label_plate", "separator_banner", "btn_arrow_down", "btn_arrow_up",
        "icon_build", "icon_expedition", "icon_hero", "icon_island"
    };

    private static bool AssetPathIsMiniEmpire(Sprite sprite) => Array.IndexOf(PackTextures, sprite.texture.name) >= 0;
}
