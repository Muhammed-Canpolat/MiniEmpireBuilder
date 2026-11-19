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
/// Grup 7 (ana üs) regresyonları: zoom girdisi kadrajı değiştirmez (13), maden göstergesi ilk karede doğru ölçekte (7), köylü evi mor (9),
/// Okçu Kışlası tek okçu (12), DoorGuard yok (8), başlangıç ağacı ölçeği DecorScale ±%8 (14), kahraman ekran oranı iki sahnede eşit (34).
/// Ekran görüntüleri Docs/playtest/bugs-g7/.
/// </summary>
public class Session13G7Tests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

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

    private static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/playtest/bugs-g7"));

    /// <summary>Kahramanın Warrior karesinin (3 u × kök ölçek) kameranın görünür yüksekliğine oranı (sprite karesinden bağımsız).</summary>
    private static float HeroRatio(Camera cam, Transform hero)
    {
        float frame = (float)T("HeroLook").GetField("WarriorFrameHeight").GetValue(null);
        return hero.lossyScale.y * frame / (2f * cam.orthographicSize);
    }

    [UnityTest]
    public IEnumerator BaseScene_G7_Regressions()
    {
        PlayerPrefs.SetInt("MEB_BaseBarOpen", 1);
        yield return UiAnaUsCapture.LoadBase(7, 43, 227, 2581, 8, save => UiAnaUsCapture.FillReferenceBase(save, 4));
        yield return new WaitForSecondsRealtime(1.0f);
        T("AwayReportUI").GetMethod("CloseIfOpen").Invoke(null, null);
        yield return null;

        // 13: zoom girdisi (kod yolu + girdi işleyicisi) kadrajı değiştirmez; sıkıştırma işleyicisi yok.
        var cam = Camera.main;
        var controller = cam.GetComponent(T("BaseCameraController"));
        Assert.NotNull(controller);
        Assert.IsNull(controller.GetType().GetMethod("HandlePinch", Any), "sıkıştırma zoom işleyicisi kaldırıldı");
        float ortho = cam.orthographicSize; Vector3 pos = cam.transform.position;
        controller.GetType().GetMethod("ZoomBy").Invoke(controller, new object[] { 0.5f, new Vector2(100f, 100f) });
        controller.GetType().GetMethod("HandleInput", Any).Invoke(controller, null);
        yield return null; yield return null;
        Assert.AreEqual(ortho, cam.orthographicSize, 1e-4f, "zoom kadrajı değiştirmez");
        Assert.AreEqual(pos, cam.transform.position, "kamera konumu değişmez");

        // 9: Köylü Evi tüm kademelerde mor (Purple Buildings).
        var catalog = T("BaseVisualCatalog").GetProperty("Instance").GetValue(null);
        var houseType = Enum.Parse(T("BuildingType"), "House");
        var levels = (Array)catalog.GetType().GetMethod("GetLevels").Invoke(catalog, new[] { houseType });
        Assert.AreEqual(5, levels.Length);
        foreach (var level in levels)
        {
            var sprite = (Sprite)level.GetType().GetField("sprite").GetValue(level);
            Assert.NotNull(sprite);
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(sprite);
            StringAssert.Contains("Purple Buildings", path, "köylü evi mor");
            StringAssert.DoesNotContain("House2", path, "House2 Kart Atölyesi'nde");
#endif
        }

        // 12 + 8: Okçu Kışlasında tek okçu; ana üste kapı muhafızı yok, çatı okçuları kaldı.
        var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Assert.AreEqual(0, all.Count(t => t.name == "Garrison_DoorGuard"), "DoorGuard yok");
        Assert.AreEqual(2, all.Count(t => t.name == "Garrison_RoofArcher"), "çatı okçuları kaldı");
        var archerGarrison = Object.FindObjectsByType(T("BaseGarrisonVisual"), FindObjectsSortMode.None).Cast<Component>()
            .First(g => g.GetComponentsInChildren<Transform>().Any(t => t.name == "Garrison_PracticeArcher"));
        Assert.AreEqual(1, archerGarrison.GetComponentsInChildren<Transform>().Count(t => t.name == "Garrison_PracticeArcher"), "Okçu Kışlası: tek okçu");

        // 14: başlangıç ağaçları DecorScale ± %8.
        var decorScale = T("DecorScale");
        var treeData = decorScale.GetMethod("Find").Invoke(null, new object[] { "tree" });
        Assert.NotNull(treeData);
        var trees = all.Where(t => t.name.StartsWith("Tree_")).ToArray();
        Assert.IsNotEmpty(trees, "başlangıç ağaçları");
        foreach (var tree in trees)
        {
            var sprite = tree.GetComponent<SpriteRenderer>().sprite;
            float expected = (float)decorScale.GetMethod("For").Invoke(null, new object[] { treeData, sprite });
            Assert.AreEqual(1f, tree.localScale.x / expected, 0.081f, tree.name + " ölçeği DecorScale ±%8");
        }

        // 34: kahraman ekran oranı.
        var hero = GameObject.Find("Hero");
        Assert.NotNull(hero);
        for (int i = 0; i < 10; i++) yield return null;
        float baseRatio = HeroRatio(cam, hero.transform);
        Debug.Log($"G7 base hero ratio {baseRatio:F4} ortho {cam.orthographicSize:F2} localScale {hero.transform.localScale.x:F4}");
        float fraction = (float)T("HeroLook").GetField("ScreenHeightFraction").GetValue(null);
        float baseScale = (float)T("HeroLook").GetField("BaseSceneScale").GetValue(null);
        Assert.AreEqual(0.85f, baseScale, 1e-4f, "ana üs kahraman görseli %15 küçük (D128)");
        Assert.AreEqual(fraction * baseScale, baseRatio, fraction * 0.05f, "ana üs kahraman oranı (×0.85)");
        var heroCircle = hero.GetComponent<CircleCollider2D>();
        Assert.NotNull(heroCircle);
        Assert.Less(heroCircle.radius * hero.transform.lossyScale.x, 1.2f, "çarpışma yarıçapı görselle birlikte küçük");
        UiAnaUsCapture.ShootFramed(1080, 1920, Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs/playtest/ui2/kahraman-boyut-9x16.png")));

        UiAnaUsCapture.ShootFramed(1080, 1920, Path.Combine(OutDir, "g7_ana_us.png"));
        PlayerPrefs.DeleteKey("MEB_BaseBarOpen");
    }

    [UnityTest]
    public IEnumerator MineGauge_PlacedCorrectlyOnFirstFrameOfGrowingBuilding()
    {
        var mine = new GameObject("GaugeMine").transform;
        mine.localScale = Vector3.one * 0.001f;
        var sr = new GameObject("body").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(mine, false);
        var gauge = (Component)T("MineStorageGauge").GetMethod("Attach").Invoke(null, new object[] { mine });
        mine.localScale = Vector3.one;   // tween bitti
        yield return null;
        Assert.AreEqual(0.021f, gauge.transform.lossyScale.x, 0.0005f, "gösterge ilk karede doğru ölçekte");
        Object.Destroy(mine.gameObject);
    }

    [UnityTest]
    public IEnumerator BattleScene_HeroScreenRatioMatchesBase()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = T("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", Any).Invoke(manager, null);
        T("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 60; i++) yield return null;
        var hero = (Component)Object.FindFirstObjectByType(T("HeroController"));
        Assert.NotNull(hero);
        var cam = Camera.main;
        float ratio = HeroRatio(cam, hero.transform);
        float fraction = (float)T("HeroLook").GetField("ScreenHeightFraction").GetValue(null);
        Debug.Log($"G7 battle hero ratio {ratio:F4} ortho {cam.orthographicSize:F2}");
        Assert.AreEqual(fraction, ratio, fraction * 0.05f, "sefer kahraman oranı ana üsle eşit");

        Directory.CreateDirectory(OutDir);
        var texture = new RenderTexture(1080, 1920, 24);
        Rect oldRect = cam.rect;
        cam.targetTexture = texture;
        cam.rect = new Rect(0, 0, 1, 1);
        cam.aspect = 1080f / 1920f;
        cam.orthographicSize = (float)T("BattleSceneSetup").GetMethod("CameraSizeFor").Invoke(null, new object[] { cam.aspect });
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, "g7_sefer_savasi.png"), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        cam.targetTexture = null;
        cam.rect = oldRect;
        cam.ResetAspect();
        texture.Release();
        T("BattleSession").GetMethod("Clear").Invoke(null, null);
    }
}
