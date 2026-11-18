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

    private static string Out(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs/playtest/ui2", name + ".png"));

    private static int Scan(string where, List<string> problems)
    {
        var args = new object[] { null };
        int bad = (int)T("TmpTextIntegrity").GetMethod("ScanLoaded").Invoke(null, args);
        Debug.Log($"[FontCapture] {where}: {args[0]}");
        if (bad > 0) problems.Add(where + ": " + args[0]);
        return bad;
    }

    /// <summary>Taşma: filtreye uyan yazılarda metin kutusunu aşan varsa <paramref name="problems"/>'a ekler (assert=false ise yalnız günlük).</summary>
    private static void Overflow(string where, string[] filter, List<string> problems, bool assert = true)
    {
        var args = new object[] { filter, null };
        int bad = (int)T("TmpTextIntegrity").GetMethod("ScanOverflow").Invoke(null, args);
        Debug.Log($"[FontCapture] taşma {where}: {bad} | {args[1]}");
        if (bad > 0 && assert) problems.Add($"taşma {where}: {args[1]}");
    }

    /// <summary>Overlay canvas'ları geçici olarak kameraya bağlayıp hedef çözünürlükte çeker, sonra geri alır.</summary>
    private static void Shoot(Camera camera, int w, int h, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var texture = new RenderTexture(w, h, 24);
        var changed = new List<(Canvas c, RenderMode m, Camera cam, int order, bool over, float dist)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            changed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.sortingOrder, canvas.overrideSorting, canvas.planeDistance));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f - canvas.sortingOrder * 0.001f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000 + canvas.sortingOrder;
        }
        var oldTarget = camera.targetTexture;
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        Canvas.ForceUpdateCanvases();
        foreach (var part in new[] { "MapButton", "AutoButton", "Ability", "Item_", "NextWave" })
        {
            string d = (string)T("TmpTextIntegrity").GetMethod("Describe").Invoke(null, new object[] { part });
            if (!string.IsNullOrEmpty(d)) Debug.Log($"[FontCapture] ölçü {w}x{h} {part}: {d}");
        }
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = oldTarget;
        camera.ResetAspect();
        foreach (var (c, m, cam, order, over, dist) in changed)
        {
            if (c == null) continue;
            c.renderMode = m; c.worldCamera = cam; c.sortingOrder = order; c.overrideSorting = over; c.planeDistance = dist;
        }
        texture.Release();
        Object.Destroy(texture);
        Canvas.ForceUpdateCanvases();
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
}
