using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz A görsel inceleme (island_base_v2 §10): ada sahnesi gerçek karolarla kurulur, portre 1080x1920 görüntüler
/// <c>PlaytestOutput/island/*.png</c> içine yazılır (UIReviewScreenshots'a yazmaz). Ayrıca "kodla çizilmiş sprite yok" denetimi.
/// Kayıt PlayerPrefs/dosya yedeklenir ve geri yüklenir.
/// </summary>
public partial class IslandReviewScreenshots
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private const int W = 1080, H = 1920;

    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

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
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        var dev = Type("DevSettings").GetProperty("Enabled");
        dev.SetValue(null, devWas);
        Type("IslandSession").GetMethod("Clear").Invoke(null, null);
        Time.timeScale = 1f;
        Type("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
    }

    private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

    /// <summary>Ada görüntüsü: eski tarz tohumlanmış köy (AŞAMA 2 öncesi görüntüler hazır köyü gösterir).</summary>
    private IEnumerator LoadIsland(int keep = 0, int highest = 0)
    {
        var dev = Type("DevSettings").GetProperty("Enabled");
        devWas = (bool)dev.GetValue(null);
        dev.SetValue(null, true);
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        if (keep > 0)
        {
            // Savunma ölçümleri: Kıyı Kalesi ve Akın ilerlemesi kayıtla belirlenir (öğretici bitmiş sayılır).
            var island = save.GetType().GetField("island").GetValue(save) ?? Activator.CreateInstance(Type("IslandSaveData"));
            island.GetType().GetField("keepLevel").SetValue(island, keep);
            island.GetType().GetField("highestAkin").SetValue(island, highest);
            island.GetType().GetField("tutorialStep").SetValue(island, 4);
            save.GetType().GetField("island").SetValue(save, island);
        }
        {
            var island = save.GetType().GetField("island").GetValue(save);
            Type("IslandEconomy").GetMethod("EnsureSeeded").Invoke(null, new[] { island, (object)Math.Max(1, keep) });
        }
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        Type("IslandSession").GetMethod("Begin").Invoke(null, null);
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 20; i++) yield return null;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            var setup = UnityEngine.Object.FindFirstObjectByType(Type("IslandSceneSetup"));
            if (setup != null && (bool)Prop(setup, "Ready")) break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.6f);
    }

    private static Component Setup => (Component)UnityEngine.Object.FindFirstObjectByType(Type("IslandSceneSetup"));

    private static void Capture(string name)
    {
        var setup = Setup;
        var camera = (Camera)Prop(setup, "WorldCamera");
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/island"));
        Directory.CreateDirectory(folder);
        var texture = new RenderTexture(W, H, 24);
        camera.targetTexture = texture;
        camera.aspect = (float)W / H;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;   // gerçek oyunda Overlay: arayüz bulutların üstündedir
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(W, H, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        UnityEngine.Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        texture.Release();
        UnityEngine.Object.Destroy(texture);
    }

    private static void View(string method, params object[] args)
    {
        var controller = Prop(Setup, "CameraController");
        controller.GetType().GetMethod(method).Invoke(controller, args);
    }

    private static void SetCamera(Vector2 center, float size)
    {
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        camera.orthographicSize = size;
        camera.transform.position = new Vector3(center.x, center.y, camera.transform.position.z);
        var controller = Prop(Setup, "CameraController");
        controller.GetType().GetMethod("ClampView").Invoke(controller, null);
    }

    [UnityTest]
    public IEnumerator CaptureIslandViews()
    {
        yield return LoadIsland();
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;

        // 1. Varsayılan görünüm (KK 1: bölge bulutları var)
        View("SetDefaultView");
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("01_default_view");

        // 2. Tüm bölgeler açık, tam uzak görünüm
        Setup.GetType().GetMethod("SetKeepOverride").Invoke(Setup, new object[] { 15 });
        View("SetFullView");
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("02_full_all_open");

        // 3. Yakın görünümler: kale + yayla, güney çayır
        float full = camera.orthographicSize;
        SetCamera(new Vector2(10.5f, 26.6f), full * 0.45f);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("03_zoom_castle");
        SetCamera(new Vector2(10.5f, 11f), full * 0.45f);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("04_zoom_south");
        SetCamera(new Vector2(10.5f, 18f), full * 0.6f);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("05_zoom_middle");
        SetCamera(new Vector2(14f, 27f), full * 0.3f);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("06_stairs_S1");
        SetCamera(new Vector2(5f, 21f), full * 0.3f);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("07_stairs_S2");
        SetCamera(new Vector2(16f, 21f), full * 0.3f);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("08_stairs_S3");
    }

    [UnityTest]
    public IEnumerator CaptureBattleFrame()
    {
        yield return LoadIsland();
        var raid = Prop(Setup, "Raid");
        Setup.GetType().GetMethod("SetKeepOverride").Invoke(Setup, new object[] { 15 });
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 });
        yield return new WaitForSecondsRealtime(1.2f);   // kamera savaş çerçevesine kayar
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        View("SetBattleView");
        yield return new WaitForSecondsRealtime(6f);
        Capture("10_battle_frame");
        yield return new WaitForSecondsRealtime(40f);   // Akın 1 öğreticisi: ilk tekne 0,6 hızla ≈45 sn'de varır
        Capture("11_battle_landing");
        yield return new WaitForSecondsRealtime(6f);
        Capture("12_battle_fight");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    private static object EnemyList(params string[] names)
    {
        var enemyType = Type("EnemyType");
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(enemyType));
        foreach (var name in names) list.Add(Enum.Parse(enemyType, name));
        return list;
    }

    [UnityTest]
    public IEnumerator CaptureEnemiesAndBoatsLineup()
    {
        yield return LoadIsland();
        Setup.GetType().GetMethod("SetKeepOverride").Invoke(Setup, new object[] { 15 });
        var castle = (Component)Prop(Setup, "CastleStructure");
        var enemyType = Type("IslandEnemy");
        var spawn = enemyType.GetMethod("Spawn");
        var types = new[] { "Deckhand", "Raider", "ShoreArcher", "Raider" };
        for (int i = 0; i < types.Length; i++)
        {
            bool boss = i == 3;
            spawn.Invoke(null, new object[] { Enum.Parse(Type("EnemyType"), types[i]), 3, castle.transform,
                new Vector2(7.5f + i * 2f, 25.5f), null, 0, boss, false });
        }
        // Gemiler: Sandal, Akıncı I (kırmızı), II (siyah), IV (sarı), Mavna; hasar kareleri
        var boatType = Type("RaidBoat");
        var kindType = Type("Progression+Island+BoatKind");
        var specs = new[] { ("Rowboat", 1, 0.5f), ("Longboat", 5, 1f), ("Longboat", 5, 0.5f), ("Longboat", 5, 0.2f), ("Longboat", 14, 1f), ("Longboat", 26, 0.5f), ("Barge", 14, 1f) };
        var boats = new List<Component>();
        for (int i = 0; i < specs.Length; i++)
        {
            var boat = (Component)boatType.GetMethod("Spawn").Invoke(null,
                new[] { Enum.Parse(kindType, specs[i].Item1), specs[i].Item2, 2, EnemyList("Deckhand", "Deckhand"), 0.0001f });
            boats.Add(boat);
        }
        yield return null;
        for (int i = 0; i < boats.Count; i++)
        {
            boats[i].transform.position = new Vector3(2.5f + i * 2.6f, 7f, 0f);
            float fraction = specs[i].Item3;
            if (fraction < 1f)
            {
                float max = (float)boats[i].GetType().GetProperty("MaxHealth").GetValue(boats[i]);
                boats[i].GetType().GetMethod("TakeArrow").Invoke(boats[i], new object[] { max * (1f - fraction), false });
            }
        }
        // sahnede yürüyen düşmanlar için görünümler
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        SetCamera(new Vector2(10.5f, 26f), 19.5f * 0.34f);
        Prop(Setup, "CameraController").GetType().GetProperty("Locked").SetValue(Prop(Setup, "CameraController"), true);
        SetCamera(new Vector2(10.5f, 26f), 19.5f * 0.34f);
        yield return new WaitForSecondsRealtime(0.25f);
        Capture("09_enemy_lineup");
        SetCamera(new Vector2(10.5f, 7f), 19.5f * 0.34f);
        foreach (var boat in boats) boat.transform.position = new Vector3(boat.transform.position.x, 7f, 0f);
        yield return null;
        for (int i = 0; i < boats.Count; i++) boats[i].transform.position = new Vector3(2.5f + i * 2.6f, 7f, 0f);
        Capture("09b_boats_lineup");
        foreach (var boat in boats) UnityEngine.Object.Destroy(boat.gameObject);
    }

    [UnityTest]
    public IEnumerator IslandSceneHasNoCodeDrawnSprites()
    {
        yield return LoadIsland();
        yield return new WaitForSecondsRealtime(0.5f);
        int checkedSprites = 0;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Savaş efektleri (hasar yazısı vb.) sprite kullanmaz; her SpriteRenderer sprite'ı bir asset olmalı.
            if (renderer.sprite == null) continue;
            if (renderer.name == "BarFrame" || renderer.name == "BarBg" || renderer.name == "BarFill") continue;   // paylaşılan savaş çubuğu (1 piksel dolgu), ada sanatı değil
            checkedSprites++;
            Assert.IsFalse(renderer.sprite.texture.name.StartsWith("isl_"), $"{renderer.name}: kodla çizilmiş sprite ({renderer.sprite.texture.name})");
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(renderer.sprite);
            Assert.IsTrue(!string.IsNullOrEmpty(path), $"{renderer.name}: sprite bir asset değil ({renderer.sprite.name})");
#endif
        }
        Assert.Greater(checkedSprites, 100, "ada sahnesinde sprite yok");
        int tiles = 0;
        foreach (var tilemap in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var sprites = new Sprite[tilemap.GetUsedSpritesCount()];
            tilemap.GetUsedSpritesNonAlloc(sprites);
            foreach (var sprite in sprites)
            {
                tiles++;
                Assert.IsFalse(sprite.texture.name.StartsWith("isl_"), $"{tilemap.name}: kodla çizilmiş karo");
#if UNITY_EDITOR
                Assert.IsTrue(!string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(sprite)), $"{tilemap.name}: karo sprite'ı asset değil");
#endif
            }
        }
        Assert.Greater(tiles, 30, "karo yok");

        // Kontrol listesi sayıları (kit §10 / v2 §10): ağaç, çalı, kaya, su kayası, koyun, piyon, bulut
        Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType(Type("IslandTree"), FindObjectsSortMode.None).Length, 24);
        // AŞAMA 2 §3: koyunlar yalnız Ağıl çevresinde (hazır köyde 1 Ağıl × sheepPerPasture)
        var featureConfig = Type("FeatureConfig").GetProperty("Instance").GetValue(null);
        Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType(Type("IslandSheep"), FindObjectsSortMode.None).Length,
            (int)featureConfig.GetType().GetField("sheepPerPasture").GetValue(featureConfig));
        Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType(Type("IslandPawn"), FindObjectsSortMode.None).Length, 3);
        Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType(Type("IslandCloud"), FindObjectsSortMode.None).Length, 2);
        Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType(Type("IslandZoneCloud"), FindObjectsInactive.Include, FindObjectsSortMode.None).Length, 5);
        var water = GameObject.Find("WaterDecor");
        Assert.NotNull(water);
        Assert.GreaterOrEqual(water.transform.childCount, 6, "su kayaları");
    }
}
