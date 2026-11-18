using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// D124 (UI2 Madde 1+5) kontrol ekranları: dalga kutusu (ada akını + sefer), seviye banner'ı, İnşa Et sekmeleri, Kahraman/Çırak sekmeleri,
/// ada savaş HUD'ı. Her ekranda sahnedeki TÜM etkin TMP yazıları TmpTextIntegrity ile ölçülür (eksik glif + ağ/malzeme uyumu = 0 olmalı);
/// 9:16 (1080x1920) ve 4:3 tablet (1536x2048) görüntüleri Docs/playtest/ui2/ altına yazılır.
/// </summary>
public class FontCaptureTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private bool devWas;
    private static readonly (int w, int h, string tag)[] Sizes = { (1080, 1920, "9x16"), (1536, 2048, "4x3") };

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

    internal static string Out(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs/playtest/ui2", name + ".png"));

    internal static int Scan(string where, List<string> problems)
    {
        var args = new object[] { null };
        int bad = (int)T("TmpTextIntegrity").GetMethod("ScanLoaded").Invoke(null, args);
        Debug.Log($"[FontCapture] {where}: {args[0]}");
        if (bad > 0) problems.Add(where + ": " + args[0]);
        return bad;
    }

    /// <summary>Taşma: filtreye uyan yazılarda metin kutusunu aşan varsa <paramref name="problems"/>'a ekler (assert=false ise yalnız günlük).</summary>
    internal static void Overflow(string where, string[] filter, List<string> problems, bool assert = true)
    {
        var args = new object[] { filter, null };
        int bad = (int)T("TmpTextIntegrity").GetMethod("ScanOverflow").Invoke(null, args);
        Debug.Log($"[FontCapture] taşma {where}: {bad} | {args[1]}");
        if (bad > 0 && assert) problems.Add($"taşma {where}: {args[1]}");
    }

    /// <summary>Overlay canvas'ları geçici olarak kameraya bağlayıp hedef çözünürlükte çeker, sonra geri alır.</summary>
    internal static void Shoot(Camera camera, int w, int h, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var texture = new RenderTexture(w, h, 24);
        // Kök neden (İş 5): ScreenSpaceCamera tuvali kameranın rect'ine sığar. Dünya kamerası WorldViewportBars ile (0,0.12,1,0.68) gibi
        // daraltıldığından HUD da o kutuya sıkışıp üstte/altta siyah bant çıkıyordu (yalnız yakalama hatası). Tuvaller tam ekran
        // bir UI kamerasına bağlanır; dünya kamerası HUD'ın gerçek bar yüksekliklerinden hesaplanan rect ile çizilir.
        var uiGo = new GameObject("CaptureUiCamera");
        var uiCam = uiGo.AddComponent<Camera>();
        uiCam.orthographic = true; uiCam.clearFlags = CameraClearFlags.SolidColor; uiCam.backgroundColor = new Color(0, 0, 0, 0); uiCam.cullingMask = 1 << 31;
        uiCam.rect = new Rect(0, 0, 1, 1); uiCam.depth = 1000;
        var uiTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        uiCam.targetTexture = uiTexture;
        var layers = new List<(GameObject go, int layer)>();   // tuvaller geçici olarak 31. katmana; dünya kamerası görmez, UI kamerası yalnız bunu çizer
        var changed = new List<(Canvas c, RenderMode m, Camera cam, int order, bool over, float dist)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            changed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.sortingOrder, canvas.overrideSorting, canvas.planeDistance));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            foreach (var tr in canvas.GetComponentsInChildren<Transform>(true)) { layers.Add((tr.gameObject, tr.gameObject.layer)); tr.gameObject.layer = 31; }
            canvas.worldCamera = uiCam;
            canvas.planeDistance = 10f - canvas.sortingOrder * 0.001f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000 + canvas.sortingOrder;
        }
        var oldTarget = camera.targetTexture;
        var oldRect = camera.rect;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        Canvas.ForceUpdateCanvases();
        // Kök neden (İş 5): oyunda WorldViewportBars kamera rect'ini EKRAN çözünürlüğüne göre kurar; yakalama farklı çözünürlükte
        // (1080x1920 / 1536x2048) çizdiği için bu normalize rect üst/alt barlarla uyuşmaz ve siyah bant oluşur.
        // Yakalamada rect'i yakalama tuvalinin gerçek bar yüksekliklerinden yeniden hesapla.
        float barTop = 0f, barBottom = 0f;
        var barsType = T("WorldViewportBars");
        foreach (var obj in Object.FindObjectsByType(barsType, FindObjectsSortMode.None))
        {
            var cv = ((Component)obj).GetComponent<Canvas>();
            if (cv == null) continue;
            var hudTop = cv.transform.Find("SafeArea/HudRoot/TopBarBG") as RectTransform ?? cv.transform.Find("SafeArea/TopBar") as RectTransform;
            var hudBottom = cv.transform.Find("SafeArea/HudRoot/BottomDock") as RectTransform ?? cv.transform.Find("SafeArea/BattleBottomDock") as RectTransform;
            if (hudTop == null || hudBottom == null) continue;
            var cc = new Vector3[4]; var tc = new Vector3[4]; var bc = new Vector3[4];
            ((RectTransform)cv.transform).GetWorldCorners(cc);
            hudTop.GetWorldCorners(tc);
            hudBottom.GetWorldCorners(bc);
            float full = Mathf.Max(0.0001f, cc[1].y - cc[0].y);
            barTop = Mathf.Max(barTop, (cc[1].y - tc[0].y) / full * h);
            barBottom = Mathf.Max(barBottom, (bc[1].y - cc[0].y) / full * h);
        }
        Debug.Log($"[FontCapture] kadraj {w}x{h}: ekran {Screen.width}x{Screen.height} kamera.rect(oyun)={oldRect} bar üst/alt px(yakalama)={barTop:0}/{barBottom:0}");
        if (barTop + barBottom > 0f)
        {
            camera.rect = new Rect(0f, barBottom / h, 1f, Mathf.Clamp01((h - barTop - barBottom) / h));
            camera.aspect = (float)w / Mathf.Max(1f, h - barTop - barBottom);
        }
        foreach (var part in new[] { "MapButton", "AutoButton", "Ability", "Item_", "NextWave" })
        {
            string d = (string)T("TmpTextIntegrity").GetMethod("Describe").Invoke(null, new object[] { part });
            if (!string.IsNullOrEmpty(d)) Debug.Log($"[FontCapture] ölçü {w}x{h} {part}: {d}");
        }
        camera.Render();
        uiCam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(w, h, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        var uiPixels = new Texture2D(w, h, TextureFormat.RGBA32, false);
        RenderTexture.active = uiTexture;
        uiPixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        var world = pixels.GetPixels32();
        var ui = uiPixels.GetPixels32();
        for (int i = 0; i < world.Length; i++)   // UI kamerası şeffaf zeminde çizdi (premultiplied): ui + dünya*(1-a)
        {
            int inv = 255 - ui[i].a;
            world[i] = new Color32((byte)Mathf.Min(255, ui[i].r + world[i].r * inv / 255), (byte)Mathf.Min(255, ui[i].g + world[i].g * inv / 255),
                (byte)Mathf.Min(255, ui[i].b + world[i].b * inv / 255), 255);
        }
        pixels.SetPixels32(world);
        pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        Object.Destroy(pixels);
        Object.Destroy(uiPixels);
        RenderTexture.active = previous;
        camera.targetTexture = oldTarget;
        camera.rect = oldRect;
        camera.ResetAspect();
        foreach (var (c, m, cam, order, over, dist) in changed)
        {
            if (c == null) continue;
            c.renderMode = m; c.worldCamera = cam; c.sortingOrder = order; c.overrideSorting = over; c.planeDistance = dist;
        }
        foreach (var (go, layer) in layers) if (go != null) go.layer = layer;
        Object.Destroy(uiGo);
        uiTexture.Release();
        Object.Destroy(uiTexture);
        texture.Release();
        Object.Destroy(texture);
        Canvas.ForceUpdateCanvases();
    }

    private static void LogPanelFit(string when)
    {
        foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (rt.name != "HeroInfo") continue;
            var canvas = rt.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) continue;
            var cc = new Vector3[4]; var pc = new Vector3[4];
            ((RectTransform)canvas.transform).GetWorldCorners(cc);
            rt.GetWorldCorners(pc);
            float full = cc[1].y - cc[0].y;
            Debug.Log($"[FontCapture] {when}: HeroInfo alt={(pc[0].y - cc[0].y) / full:0.000} üst={(pc[1].y - cc[0].y) / full:0.000} (0..1 dışı = taşma) scale={rt.lossyScale.y:0.00} aktif={rt.gameObject.activeInHierarchy}");
        }
    }

    private static void ShootBoth(Camera camera, string name)
    {
        foreach (var (w, h, tag) in Sizes) Shoot(camera, w, h, Out($"{name}-{tag}"));
    }

    // ==================== ADA AKINI: dalga kutusu + HUD (HARİTA/TAKİP/OTO/yetenekler) ====================

    [UnityTest]
    public IEnumerator IslandRaid_WaveBannerAndHud_HaveNoBrokenText()
    {
        var problems = new List<string>();
        yield return LoadIsland(island => { Set(island, "highestAkin", 5); }, 6, v => { });
        var setup = Setup;
        var raid = Prop(setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 6 });
        yield return new WaitForSecondsRealtime(1.5f);
        var camera = (Camera)Prop(setup, "WorldCamera");
        var vfx = T("BattleVfxManager").GetProperty("Instance").GetValue(null);
        Assert.NotNull(vfx, "ada sahnesinde BattleVfxManager");
        for (int round = 0; round < 3; round++)   // "bazen görünüyor": ardışık dalga kutuları her seferinde temiz olmalı
        {
            vfx.GetType().GetMethod("ShowWaveBanner").Invoke(vfx, new object[] { round + 1 });
            yield return new WaitForSecondsRealtime(0.4f);
            Scan($"ada dalga kutusu {round + 1}", problems);
            if (round == 0) ShootBoth(camera, "ada-akin-dalga-kutusu");
            yield return new WaitForSecondsRealtime(1.2f);
        }
        vfx.GetType().GetMethod("ShowNoticeBanner").Invoke(vfx, new object[] { "Sonraki dalga · YENİ DÜŞMAN: Zırhlı" });
        yield return new WaitForSecondsRealtime(0.4f);
        Scan("ada duyuru kutusu", problems);
        ShootBoth(camera, "ada-akin-duyuru");
        yield return new WaitForSecondsRealtime(1.2f);
        ShootBoth(camera, "ada-akin-hud");
        Overflow("ada HUD", new[] { "MapButton", "AutoButton", "Ability", "FireButton" }, problems);
        Overflow("ada tümü", null, problems, false);
        var controller = Prop(setup, "CameraController");
        controller.GetType().GetMethod("ToggleFullFrame").Invoke(controller, null);
        yield return new WaitForSecondsRealtime(0.6f);
        Scan("ada HUD (HARİTA/TAKİP)", problems);
        Overflow("ada HUD takip", new[] { "MapButton", "AutoButton", "Ability" }, problems);
        ShootBoth(camera, "ada-akin-hud-takip");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
        yield return null;
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // ==================== SEFER: dalga kutusu + "Sonraki dalga" geri sayımı ====================

    [UnityTest]
    public IEnumerator CampaignBattle_WaveBannerAndCountdown_HaveNoBrokenText()
    {
        var problems = new List<string>();
        var managerType = T("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        T("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 50; frame++) yield return null;
        yield return new WaitForSecondsRealtime(1.5f);
        var camera = Camera.main;
        var vfx = T("BattleVfxManager").GetProperty("Instance").GetValue(null);
        for (int round = 0; round < 3; round++)
        {
            vfx.GetType().GetMethod("ShowWaveBanner").Invoke(vfx, new object[] { round + 1 });
            yield return new WaitForSecondsRealtime(0.4f);
            Scan($"sefer dalga kutusu {round + 1}", problems);
            if (round == 0) ShootBoth(camera, "sefer-dalga-kutusu");
            yield return new WaitForSecondsRealtime(1.2f);
        }
        // "Sonraki dalga N" geri sayımı (normalde dalga arasında görünür): kontrast için zorla göster
        var countdown = GameObject.Find("NextWaveCountdown");
        if (countdown == null)
            foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "NextWaveCountdown") { countdown = t.gameObject; break; }
        if (countdown != null)
        {
            var ui = Object.FindFirstObjectByType(T("BattleUI")) as Behaviour;
            if (ui != null) ui.enabled = false;   // her kare gizlemesin
            countdown.SetActive(true);
            var tmp = countdown.GetComponent("TextMeshProUGUI");
            tmp.GetType().GetProperty("text").SetValue(tmp, "Sonraki dalga 3");
            yield return null; yield return null;
            Scan("sefer sonraki dalga", problems);
            ShootBoth(camera, "sefer-sonraki-dalga");
            Overflow("sefer", null, problems, false);
            if (ui != null) ui.enabled = true;
        }
        else problems.Add("NextWaveCountdown bulunamadı");
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // ==================== ANA ÜS: İnşa Et sekmeleri, Kahraman/Çırak sekmeleri, seviye banner'ı ====================

    [UnityTest]
    public IEnumerator Base_BuildTabs_HeroTabs_LevelBanner_HaveNoBrokenText()
    {
        var problems = new List<string>();
        yield return UiAnaUsCapture.LoadBase(7, 77, 625, 2700, 24, save => UiAnaUsCapture.FillReferenceBase(save, 2));
        var camera = Camera.main;
        Scan("ana üs HUD", problems);
        foreach (var (w, h, tag) in Sizes) UiAnaUsCapture.ShootFramed(w, h, Out("ana-us-hud-" + tag));

        // İnşa Et: dört sekme, her biri birkaç kez aç-kapa
        var menu = T("BuildMenuController").GetProperty("Instance").GetValue(null);
        Assert.NotNull(menu, "BuildMenuController");
        var show = menu.GetType().GetMethod("Show");
        var hide = menu.GetType().GetMethod("Hide");
        var select = menu.GetType().GetMethod("SelectTab");
        for (int round = 0; round < 3; round++)
        {
            show.Invoke(menu, null);
            for (int tab = 0; tab < 4; tab++)
            {
                select.Invoke(menu, new object[] { tab });
                yield return null; yield return null;
                Scan($"İnşa Et sekme {tab} tur {round}", problems);
                if (round == 0) Overflow($"İnşa Et sekme {tab}", null, problems, false);
                if (round == 0 && (tab == 0 || tab == 1)) foreach (var (w, h, tag) in Sizes) UiAnaUsCapture.ShootFramed(w, h, Out($"insa-et-sekme{tab}-{tag}"));
            }
            hide.Invoke(menu, null);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // Kahraman / Çırak: sekmeler arası geçiş ve panel aç-kapa
        var world = T("BaseWorldUI").GetProperty("Instance").GetValue(null);
        Assert.NotNull(world, "BaseWorldUI");
        for (int round = 0; round < 3; round++)
        {
            world.GetType().GetMethod("ShowHeroInfo").Invoke(world, null);
            yield return null; yield return null;
            if (round == 0)
            {
                // İş 4: açılış animasyonu karesi mi gerçek taşma mı? Hemen ve animasyon bittikten sonra panel kutusunu tuvalle karşılaştır.
                LogPanelFit("kahraman +2 kare");
                yield return new WaitForSecondsRealtime(1.2f);
                LogPanelFit("kahraman +1.2 sn");
            }
            Scan($"Kahraman tur {round}", problems);
            if (round == 0) foreach (var (w, h, tag) in Sizes) UiAnaUsCapture.ShootFramed(w, h, Out("kahraman-sekme-" + tag));
            world.GetType().GetMethod("ShowApprenticeInfo").Invoke(world, null);
            yield return null; yield return null;
            Scan($"Çırak tur {round}", problems);
            if (round == 0) foreach (var (w, h, tag) in Sizes) UiAnaUsCapture.ShootFramed(w, h, Out("cirak-sekme-" + tag));
            world.GetType().GetMethod("HideAllPanelsPublic").Invoke(world, null);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        // Seviye banner'ı (Ana Üs yükseltmesi olayı)
        var banner = Object.FindFirstObjectByType(T("BaseLevelBanner"));
        Assert.NotNull(banner, "BaseLevelBanner");
        banner.GetType().GetMethod("OnUpgraded", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(banner, new[] { Enum.Parse(T("BuildingType"), "MainBase"), (object)8 });
        yield return new WaitForSecondsRealtime(0.5f);
        Scan("seviye banner'ı", problems);
        foreach (var (w, h, tag) in Sizes) UiAnaUsCapture.ShootFramed(w, h, Out("seviye-banner-" + tag));
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    /// <summary>Sahne geçişi sonrası (ana üs → sefer → ana üs) varsayılan font ve gövde fontu aynı asset kalır; atlas/malzeme sağlam.</summary>
    [UnityTest]
    public IEnumerator SceneTransitions_KeepFontAssetsIntact()
    {
        var bootstrap = T("TmpFontBootstrap");
        var body = bootstrap.GetProperty("BodyFont").GetValue(null) as Object;
        yield return UiAnaUsCapture.LoadBase(7, 10, 10, 100, 1);
        T("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 40; frame++) yield return null;
        Resources.UnloadUnusedAssets();
        yield return null;
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 40; frame++) yield return null;
        Assert.AreSame(body, bootstrap.GetProperty("BodyFont").GetValue(null), "gövde fontu sahne geçişinde değişmez");
        var args = new object[] { null };
        Assert.IsTrue((bool)bootstrap.GetMethod("SelfCheck").Invoke(null, args), args[0] as string);
        var problems = new List<string>();
        Scan("geçiş sonrası ana üs", problems);
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    // ==================== D126 (UI2 İş 3): ada üst barı ana üs stilinde ====================

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
    }

    [UnityTest]
    public IEnumerator IslandTopBar_MatchesBaseStyle()
    {
        var problems = new List<string>();
        yield return LoadIsland(island => { Set(island, "highestAkin", 5); }, 6, v => { });
        var setup = Setup;
        var camera = (Camera)Prop(setup, "WorldCamera");
        var bar = GameObject.Find("TopBar");
        Assert.NotNull(bar, "TopBar adı korunur");
        var barRt = (RectTransform)bar.transform;
        var safe = (RectTransform)barRt.parent;
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.AreEqual("KIYI KALESİ", Txt(bar.transform.Find("LevelCrest/Caption")), "kalkan etiketi");
        int keep = (int)T("IslandSession").GetMethod("KeepLevel").Invoke(null, null);
        Assert.AreEqual(keep.ToString(), Txt(bar.transform.Find("LevelCrest/Number")), "kalkan = gerçek Kıyı Kalesi seviyesi");
        Assert.NotNull(bar.transform.Find("HomeButton"), "ANA ÜS düğmesi");
        Assert.NotNull(bar.transform.Find("IslandNameButton"), "ada adı");
        Assert.NotNull(bar.transform.Find("GoldPill/Value"), "altın değeri");
        // Yükseltme: seviye değişince kalkan güncellenir
        var island = CurrentIsland();
        Set(island, "keepLevel", Mathf.Min(keep + 1, 15));
        yield return null; yield return null;
        int keep2 = (int)T("IslandSession").GetMethod("KeepLevel").Invoke(null, null);
        Assert.AreEqual(keep2.ToString(), Txt(bar.transform.Find("LevelCrest/Number")), "yükseltmede kalkan güncellenir");

        // Çakışma / Safe Area: öğe dikdörtgenleri kesişmez, hepsi SafeArea içinde
        Canvas.ForceUpdateCanvases();
        var items = new List<(string name, Rect rect)>();
        foreach (var name in new[] { "GoldPill", "WoodPill", "MeatPill", "GemPill", "BuilderPill", "LevelCrest", "HomeButton", "IslandNameButton" })
        {
            var t = bar.transform.Find(name) as RectTransform;
            if (t != null && t.gameObject.activeInHierarchy) items.Add((name, WorldRect(t)));
        }
        RectTransform gear = null;
        foreach (var r in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)) if (r.name == "AudioSettingsButton") gear = r;
        Assert.NotNull(gear, "ayarlar dişlisi korunur");
        items.Add(("AudioSettingsButton", WorldRect(gear)));
        var safeRect = WorldRect(safe);
        for (int i = 0; i < items.Count; i++)
        {
            if (!safeRect.Contains(items[i].rect.min) || !safeRect.Contains(items[i].rect.max - new Vector2(0.001f, 0.001f)))
                problems.Add($"{items[i].name} SafeArea dışında");
            Debug.Log($"[IslandBar] {items[i].name} {items[i].rect} (güvenli {safeRect})");
            for (int j = i + 1; j < items.Count; j++)
                if (items[i].rect.Overlaps(items[j].rect)) problems.Add($"{items[i].name} ile {items[j].name} kesişiyor");
        }
        Scan("ada üst bar", problems);
        var sizes = new[] { (1080, 1920, "9x16"), (1080, 2340, "9x21"), (1536, 2048, "4x3") };
        foreach (var (w, h, tag) in sizes) Shoot(camera, w, h, Out("is3-ada-normal-" + tag));

        var raid = Prop(setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 6 });
        yield return new WaitForSecondsRealtime(1.5f);
        var plate = Txt(bar.transform.Find("IslandNameButton/Title"));
        StringAssert.Contains("6", plate, "akın bilgisi");
        StringAssert.Contains("1/", plate, "dalga bilgisi");
        Scan("ada üst bar akın", problems);
        foreach (var (w, h, tag) in new[] { (1080, 1920, "9x16"), (1536, 2048, "4x3") }) Shoot(camera, w, h, Out("is3-ada-akin-" + tag));
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
        yield return null;
        Assert.IsEmpty(problems, string.Join("; ", problems));
    }

    private static string Txt(Transform t)
    {
        var tmp = t.GetComponent("TextMeshProUGUI");
        return (string)tmp.GetType().GetProperty("text").GetValue(tmp);
    }

    private static RectTransform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<RectTransform>(false)) if (t.name == name) return t;
        return null;
    }
}
