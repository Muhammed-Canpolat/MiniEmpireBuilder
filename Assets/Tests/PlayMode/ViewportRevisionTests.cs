using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class ViewportRevisionTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++) yield return null;
    }

    private static IEnumerator Base()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("ReviewGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Spear") });
        save.GetType().GetField("firstBaseGuideDone").SetValue(save, true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        yield return Frames(45);
    }

    private static Rect ScreenRect(RectTransform transform)
    {
        var corners = new Vector3[4];
        transform.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static void AssertBars(string scene)
    {
        var camera = Camera.main;
        var top = GameObject.Find(scene == "BaseScene" ? "TopBarBG" : "TopBar")
            .GetComponent<RectTransform>();
        var bottom = GameObject.Find(scene == "BaseScene" ? "BottomDock" : "BattleBottomDock")
            .GetComponent<RectTransform>();
        var topRect = ScreenRect(top);
        var bottomRect = ScreenRect(bottom);
        Assert.LessOrEqual(Mathf.Abs(camera.pixelRect.yMax - topRect.yMin), 1f);
        Assert.LessOrEqual(Mathf.Abs(camera.pixelRect.yMin - bottomRect.yMax), 1f);
        Assert.AreEqual(0f, camera.pixelRect.xMin, 0.1f);
        Assert.AreEqual(Screen.width, camera.pixelRect.width, 0.1f);
        foreach (var image in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
        {
            if (!image.isActiveAndEnabled || !image.raycastTarget || image.color.a < 0.02f ||
                image.name.Contains("Debug")) continue;
            var rect = ScreenRect(image.rectTransform);
            float left = Mathf.Max(rect.xMin, camera.pixelRect.xMin);
            float right = Mathf.Min(rect.xMax, camera.pixelRect.xMax);
            float low = Mathf.Max(rect.yMin, camera.pixelRect.yMin);
            float high = Mathf.Min(rect.yMax, camera.pixelRect.yMax);
            float overlap = Mathf.Max(0f, right - left) * Mathf.Max(0f, high - low);
            Assert.LessOrEqual(overlap, camera.pixelRect.width * camera.pixelRect.height * 0.02f,
                image.name);
        }
    }

    [UnityTest]
    public IEnumerator BaseViewportAndRecommendedLayout()
    {
        yield return Base();
        var managerType = Type("GameManager");
        var map = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
        var layout = Type("BaseWorldLayout");
        AssertBars("BaseScene");
        var camera = Camera.main;
        var topRect = ScreenRect(GameObject.Find("TopBarBG").GetComponent<RectTransform>());
        var bottomRect = ScreenRect(GameObject.Find("BottomDock").GetComponent<RectTransform>());
        foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(360, 800),
                     new Vector2Int(412, 915), new Vector2Int(768, 1024) })
        {
            // Headless Unity keeps its 640x480 Game view after SetResolution.
            // Check the same viewport fit algebra at the requested target sizes.
            float lower = size.y * bottomRect.yMax / Screen.height;
            float upper = size.y * topRect.yMin / Screen.height;
            float aspect = size.x / (upper - lower);
            float orthographicSize = Mathf.Max(22f / (2f * aspect), 19f);
            float visibleWidth = 2f * orthographicSize * aspect;
            float visibleHeight = 2f * orthographicSize;
            Assert.GreaterOrEqual(visibleWidth, 22f, size.ToString());
            Assert.GreaterOrEqual(visibleHeight, 38f, size.ToString());
            Debug.Log($"[ViewportSimulation] {size.x}x{size.y} rect=" +
                $"(0,{lower:F1},{size.x},{upper - lower:F1}) aspect={aspect:F3} " +
                $"ortho={orthographicSize:F2}");
        }
        Vector3 bottomLeft = camera.WorldToViewportPoint(Vector3.zero);
        Vector3 topRight = camera.WorldToViewportPoint(new Vector3(22f, 38f));
        Assert.GreaterOrEqual(bottomLeft.x, -0.005f);
        Assert.GreaterOrEqual(bottomLeft.y, -0.005f);
        Assert.LessOrEqual(topRight.x, 1.005f);
        Assert.LessOrEqual(topRight.y, 1.005f);
        var debug = Object.FindFirstObjectByType(Type("BaseDebugOverlay"));
        Assert.IsFalse((bool)debug.GetType().GetProperty("Visible").GetValue(debug));
        Capture("revision-01-camera-clean.png");
        Capture("ux-base-clean.png");
        for (int frame = 0; frame < 60 && GameObject.Find("AudioSettingsButton") == null; frame++)
            yield return null;
        Assert.NotNull(GameObject.Find("AudioSettingsButton"));
        GameObject.Find("AudioSettingsButton").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.IsTrue(GameObject.Find("AudioSettingsPanel").activeSelf);
        Capture("ux-settings-open.png");
        GameObject.Find("CloseAudioSettings").GetComponent<Button>().onClick.Invoke();
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { true });
        Capture("revision-01-camera-debug.png");
        var readout = GameObject.Find("BaseDebugReadout")?.GetComponentInChildren(
            Type("TMPro.TextMeshProUGUI"));
        if (readout != null)
            Debug.Log("[ViewportDiagnosis] " + readout.GetType().GetProperty("text").GetValue(readout));
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { false });
        var trees = Object.FindObjectsByType(Type("MapObjectTag"), FindObjectsSortMode.None)
            .Cast<Component>().Where(item => item.name.StartsWith("Tree_")).ToArray();
        Assert.GreaterOrEqual(trees.Length, 8);
        Assert.IsTrue(trees.Any(item => item.transform.position.y > 5f &&
            item.transform.position.y < 33f));
        var treeCount = trees.Length;
        map.GetType().GetField("minTreeCount", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(map, treeCount + 1);
        map.GetType().GetField("_respawnTargetTime", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(map, 0.01f);
        yield return Frames(3);
        Assert.Greater(Object.FindObjectsByType(Type("MapObjectTag"),
            FindObjectsSortMode.None).Cast<Component>()
            .Count(item => item.name.StartsWith("Tree_")), treeCount);

        foreach (var entry in new[] { new[] { "ArcherTower", "archer_1" },
                     new[] { "CannonTower", "cannon_1" },
                     new[] { "Monastery", "monastery_1" } })
        {
            var slot = layout.GetMethod("Get").Invoke(null, new object[] { entry[1] });
            var center = (Vector2)slot.GetType().GetProperty("center").GetValue(slot);
            Assert.NotNull(map.GetType().GetMethod("SpawnBuilding").Invoke(map,
                new object[] { Enum.Parse(Type("BuildingType"), entry[0]),
                    new Vector3(center.x, center.y), entry[1] }));
        }
        map.GetType().GetMethod("ApplyRecommendedLayout").Invoke(map, null);
        yield return Frames(10);
        yield return new WaitForSecondsRealtime(0.45f);
        foreach (var id in new[] { "main", "mine_1", "archer_1", "cannon_1", "monastery_1" })
        {
            var slot = layout.GetMethod("Get").Invoke(null, new object[] { id });
            var center = (Vector2)slot.GetType().GetProperty("center").GetValue(slot);
            Assert.Less(Vector2.Distance(GameObject.Find("Building_" +
                (id == "main" ? "MainBase" : id == "mine_1" ? "GoldMine" :
                 id == "archer_1" ? "ArcherTower" : id == "cannon_1" ? "CannonTower" : "Monastery")
                + "_" + id).transform.position, center), 0.01f);
        }
        Capture("revision-02-layout-clean.png");
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { true });
        Capture("revision-02-layout-debug.png");
        Assert.IsTrue(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Any(item => item.name == "RecommendedLayout"));
        var road = GameObject.Find("StoneRoadTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        Assert.Greater(road.GetUsedTilesCount(), 0);
        Assert.IsTrue(road.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().enabled);
        Capture("revision-03-road-debug.png");
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { false });
        Capture("revision-03-road-clean.png");

        var actors = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            .Where(item => item.gameObject.name.StartsWith("Garrison_")).ToArray();
        Assert.GreaterOrEqual(actors.Length, 6);
        foreach (var actor in actors)
        {
            var sprite = actor.GetComponent<SpriteRenderer>();
            Assert.NotNull(sprite.sprite, actor.name);
            Assert.GreaterOrEqual(sprite.bounds.size.y, 1.58f, actor.name);
            Assert.NotNull(actor.runtimeAnimatorController, actor.name);
            Assert.GreaterOrEqual(actor.runtimeAnimatorController.animationClips.Length, 2);
        }
        for (int i = 0; i < actors.Length; i++) CaptureActor(actors[i], i);
        yield return new WaitForSecondsRealtime(6.2f);
        foreach (var visual in Object.FindObjectsByType(Type("BaseGarrisonVisual"),
            FindObjectsSortMode.None).Cast<Component>())
            Assert.Greater((int)visual.GetType().GetProperty("ActionCount").GetValue(visual),
                0, visual.transform.parent.name);
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { true });
        Capture("revision-06-garrison-debug.png");
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { false });
        Capture("revision-06-garrison-clean.png");
        var sound = GameObject.Find("AudioSettingsButton").GetComponent<RectTransform>();
        Assert.Greater(ScreenRect(sound).yMin, ScreenRect(GameObject.Find("TopBarBG")
            .GetComponent<RectTransform>()).yMin);
        Capture("revision-07-base-ui-clean.png");
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { true });
        Capture("revision-07-base-ui-debug.png");
        Assert.AreEqual(layout.GetField("Buildable").GetValue(null),
            layout.GetMethod("CleanAtLevel").Invoke(null, new object[] { 13 }));
        var baseSetup = Object.FindFirstObjectByType(Type("BaseSceneSetup"));
        baseSetup.GetType().GetMethod("ClearUnlockedLand").Invoke(baseSetup, new object[] { 13 });
        Assert.IsNull(GameObject.Find("NaturalBoundaryBand"));
        var gameManager = managerType.GetProperty("Instance").GetValue(null) as Component;
        var buildings = managerType.GetProperty("Buildings").GetValue(gameManager)
            as System.Collections.IDictionary;
        var mainData = buildings[Enum.Parse(Type("BuildingType"), "MainBase")];
        mainData.GetType().GetField("level").SetValue(mainData, 13); // tam açık arazi
        SceneManager.LoadScene("BaseScene");
        yield return Frames(50);
        Assert.IsNull(GameObject.Find("NaturalBoundaryBand"));
        Assert.IsFalse((bool)Object.FindFirstObjectByType(Type("BaseDebugOverlay"))
            .GetType().GetProperty("Visible").GetValue(
                Object.FindFirstObjectByType(Type("BaseDebugOverlay"))));
        var levelFiveTrees = Object.FindObjectsByType(Type("MapObjectTag"),
            FindObjectsSortMode.None).Cast<Component>()
            .Where(item => item.name.StartsWith("Tree_")).ToArray();
        Debug.Log("[LevelFiveResources] trees=" + levelFiveTrees.Length + " positions=" +
            string.Join(",", levelFiveTrees.Select(item => item.transform.position.ToString())));
        Assert.GreaterOrEqual(levelFiveTrees.Length, 8);
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            bool right = (quadrant & 1) != 0;
            bool north = quadrant >= 2;
            Assert.IsTrue(levelFiveTrees.Any(item =>
                (item.transform.position.x >= 11f) == right &&
                (item.transform.position.y >= 19f) == north),
                "Ağaç dağılımı: bölge " + quadrant);
        }
        Assert.NotNull(GameObject.Find("ExteriorScenery"));
        Assert.GreaterOrEqual(GameObject.Find("ExteriorScenery").transform.childCount, 28);
        Assert.IsNull(GameObject.Find("BaseVignette"));
        Assert.AreEqual(0, GameObject.Find("BuildableGround").transform.childCount);
        foreach (Transform item in GameObject.Find("ExteriorScenery").transform)
        {
            var bounds = item.GetComponent<SpriteRenderer>().bounds;
            if (item.name.StartsWith("Exterior_0_")) Assert.LessOrEqual(bounds.max.x, 1.01f);
            else if (item.name.StartsWith("Exterior_1_")) Assert.GreaterOrEqual(bounds.min.x, 20.99f);
            else if (item.name.StartsWith("Exterior_2_")) Assert.LessOrEqual(bounds.max.y, 1.01f);
            else if (item.name.StartsWith("Exterior_3_")) Assert.GreaterOrEqual(bounds.min.y, 36.99f);
        }
        Assert.NotNull(GameObject.Find("MineAuthoredArt"));
        Assert.AreEqual("mine_stage_1", GameObject.Find("MineAuthoredArt")
            .GetComponent<SpriteRenderer>().sprite.name);
        var lift = GameObject.Find("MineLiftBox");
        Assert.NotNull(lift);
        float liftMin = lift.transform.localPosition.y;
        float liftMax = liftMin;
        for (int sample = 0; sample < 5; sample++)
        {
            yield return new WaitForSecondsRealtime(0.20f);
            liftMin = Mathf.Min(liftMin, lift.transform.localPosition.y);
            liftMax = Mathf.Max(liftMax, lift.transform.localPosition.y);
        }
        Assert.Greater(liftMax - liftMin, 0.08f);
        Assert.Greater(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Count(item => item.name == "FenceLantern"), 0);
        Assert.NotNull(GameObject.Find("DockRivet_0_0"));
        Capture("ux-base-level5-clean.png");
        Capture("reference-base-level5.png");
        CaptureWorldCloseup("reference-mine-closeup.png",
            GameObject.Find("MineAuthoredArt").transform.position, 6.1f);
        var mineVisual = Object.FindFirstObjectByType(Type("BaseMineVisual")) as Component;
        var upgradeMine = mineVisual.GetType().GetMethod("OnBuildingUpgraded",
            BindingFlags.NonPublic | BindingFlags.Instance);
        for (int mineLevel = 1; mineLevel <= 8; mineLevel++)
        {
            upgradeMine.Invoke(mineVisual, new object[] {
                Enum.Parse(Type("BuildingType"), "GoldMine"), mineLevel });
            yield return null;
            Assert.AreEqual("mine_stage_" + mineLevel, GameObject.Find("MineAuthoredArt")
                .GetComponent<SpriteRenderer>().sprite.name);
        }
        CaptureWorldCloseup("reference-mine-stage8-closeup.png",
            GameObject.Find("MineAuthoredArt").transform.position, 6.1f);
    }

    [UnityTest]
    public IEnumerator ForestBattleViewportAndApprentice()
    {
        yield return Base();
        Type("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 2, 1 });
        SceneManager.LoadScene("BattleScene");
        yield return Frames(40);
        yield return new WaitForSecondsRealtime(0.6f);
        AssertBars("BattleScene");
        var ground = GameObject.Find("BattleGround").GetComponent<SpriteRenderer>();
        Assert.Greater(ground.color.r, 0.7f);
        var fog = GameObject.Find("ForestRadialFog").GetComponent<SpriteRenderer>();
        Assert.LessOrEqual(fog.sprite.texture.GetPixel(0, 0).a, 0.46f);
        Assert.LessOrEqual(fog.sprite.texture.GetPixel(64, 112).a, 0.01f);
        var apprentice = Object.FindFirstObjectByType(Type("BattleApprentice")) as Component;
        Assert.NotNull(apprentice);
        var pawnSprite = apprentice.GetComponent<SpriteRenderer>();
        Assert.NotNull(pawnSprite.sprite);
        var hero = Object.FindFirstObjectByType(Type("HeroController")) as Component;
        var heroSprite = hero.GetComponent<SpriteRenderer>();
        Assert.AreEqual(heroSprite.sortingOrder - 1, pawnSprite.sortingOrder);
        Assert.AreEqual(1.925f, pawnSprite.bounds.size.y, 0.12f); // kahraman 1.75 × pawn boşluk payı 1.1
        Assert.NotNull(hero.transform.Find("ReadableVisual"));
        var battleBase = Object.FindFirstObjectByType(Type("MainBaseController")) as Component;
        Assert.NotNull(battleBase.transform.Find("ReadableVisual"));
        Assert.IsFalse(Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Any(item => item.name.StartsWith("Prop_") && item.enabled));
        Assert.Less(Vector2.Distance(apprentice.transform.position,
            hero.transform.position + new Vector3(-0.9f, 0.2f)), 0.7f);
        var debug = Object.FindFirstObjectByType(Type("BattleDebugOverlay")) as Component;
        Assert.NotNull(debug);
        Capture("revision-04-forest-clean.png");
        Capture("ux-battle-clean.png");
        var brightnessPath = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview/revision-04-forest-clean.png"));
        var pixels = new Texture2D(2, 2);
        pixels.LoadImage(File.ReadAllBytes(brightnessPath));
        float brightness = 0f;
        int samples = 0;
        for (int y = 90; y < 700; y += 5)
        for (int x = 20; x < 370; x += 5)
        {
            var sample = pixels.GetPixel(x, y);
            brightness += (sample.r + sample.g + sample.b) / 3f;
            samples++;
        }
        brightness /= samples;
        Debug.Log($"[ForestBrightness] mean={brightness:F3}");
        Assert.GreaterOrEqual(brightness, 0.22f);
        Object.Destroy(pixels);
        debug.GetType().GetMethod("SetVisible", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(debug, new object[] { true });
        Capture("revision-04-forest-debug.png");
        Capture("revision-05-apprentice-debug.png");
        debug.GetType().GetMethod("SetVisible", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(debug, new object[] { false });
        Capture("revision-05-apprentice-clean.png");
        Assert.NotNull(GameObject.Find("ApprenticeHealth"));
        Assert.NotNull(GameObject.Find("RemainingRights"));
        Capture("revision-07-battle-ui-clean.png");
        debug.GetType().GetMethod("SetVisible", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(debug, new object[] { true });
        Capture("revision-07-battle-ui-debug.png");
        Type("BattleSession").GetMethod("Clear").Invoke(null, null);
    }

    private static void Capture(string file)
    {
        typeof(HudExpansionReviewTests).GetMethod("Capture",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { file });
    }

    private static void CaptureWorldCloseup(string file, Vector3 center, float size)
    {
        var camera = Camera.main;
        var originalTarget = camera.targetTexture;
        var originalPosition = camera.transform.position;
        var originalRect = camera.rect;
        var originalSize = camera.orthographicSize;
        var originalAspect = camera.aspect;
        var target = new RenderTexture(390, 844, 24);
        camera.targetTexture = target;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.aspect = 390f / 844f;
        camera.orthographicSize = size;
        camera.transform.position = new Vector3(center.x, center.y,
            originalPosition.z);
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
        pixels.Apply();
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, file), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = originalTarget;
        camera.rect = originalRect;
        camera.orthographicSize = originalSize;
        camera.aspect = originalAspect;
        camera.transform.position = originalPosition;
        target.Release();
        Object.Destroy(target);
    }

    private static void CaptureActor(Animator actor, int index)
    {
        var camera = Camera.main;
        var originalTarget = camera.targetTexture;
        var originalPosition = camera.transform.position;
        var originalRect = camera.rect;
        var originalSize = camera.orthographicSize;
        var originalAspect = camera.aspect;
        var target = new RenderTexture(390, 844, 24);
        camera.targetTexture = target;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.aspect = 390f / 844f;
        camera.orthographicSize = 3.8f;
        camera.transform.position = new Vector3(actor.transform.position.x,
            actor.transform.position.y + 0.4f, originalPosition.z);
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
        pixels.Apply();
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,
            $"revision-06-soldier-{index:D2}-{actor.name}.png"), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = originalTarget;
        camera.rect = originalRect;
        camera.orthographicSize = originalSize;
        camera.aspect = originalAspect;
        camera.transform.position = originalPosition;
        target.Release();
        Object.Destroy(target);
    }
}
