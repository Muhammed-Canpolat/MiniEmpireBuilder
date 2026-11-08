using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class BaseShadowRevisionTests
{
    private static System.Type Type(string name) => System.AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [UnityTest]
    public IEnumerator DiagnoseMovedMineLayers()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { System.Enum.Parse(Type("WeaponType"), "Spear") });
        save.GetType().GetField("firstBaseGuideDone").SetValue(save, true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData",
            BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(manager, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 50; i++) yield return null;
        var map = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
        var mine = Object.FindObjectsByType(Type("BuildingIdentifier"), FindObjectsSortMode.None)
            .OfType<Component>().FirstOrDefault(b => b.GetType().GetField("buildingType")
                .GetValue(b).ToString() == "GoldMine");
        Assert.NotNull(mine);
        var placed = map.GetType().GetMethod("GetMapObject").Invoke(map,
            new object[] { mine.gameObject });
        Assert.NotNull(placed);
        bool moved = false;
        for (int y = 4; y < 33 && !moved; y++)
        for (int x = 4; x < 19 && !moved; x++)
        {
            var candidate = new Vector3(x + 0.5f, y, 0f);
            if (Vector3.Distance(candidate, mine.transform.position) < 5f) continue;
            moved = (bool)map.GetType().GetMethod("MoveObjectTo").Invoke(map,
                new[] { placed, (object)candidate });
        }
        Assert.IsTrue(moved);
        yield return null;
        string diagnosticFolder = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview"));
        Directory.CreateDirectory(diagnosticFolder);
        var area = new Bounds(mine.transform.position, new Vector3(6f, 6f, 10f));
        var report = new System.Text.StringBuilder();
        report.AppendLine("mine=" + mine.transform.position);
        var road = GameObject.Find("StoneRoadTilemap")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        report.AppendLine("road tiles=" + road?.GetUsedTilesCount() +
            " enabled=" + road?.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>()?.enabled);
        report.AppendLine("yama exists=" + (GameObject.Find("YamaTilemap") != null));
        report.AppendLine("shadow exists=" + (mine.transform.Find("ZeminGolgesi") != null));
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (renderer.enabled && renderer.bounds.Intersects(area))
                report.AppendLine(renderer.name + " order=" + renderer.sortingOrder +
                    " bounds=" + renderer.bounds + " material=" + renderer.sharedMaterial?.name);
        File.WriteAllText(Path.Combine(diagnosticFolder, "diagnostic-renderers.txt"), report.ToString());
        var debug = Object.FindFirstObjectByType(Type("BaseDebugOverlay")) as Component;
        Assert.NotNull(debug);
        var setLayers = debug.GetType().GetMethod("SetDiagnosticLayers");
        Capture("diagnostic-all-on");
        setLayers.Invoke(debug, new object[] { false, true, true });
        Capture("diagnostic-no-yama");
        setLayers.Invoke(debug, new object[] { true, false, true });
        Capture("diagnostic-no-building");
        setLayers.Invoke(debug, new object[] { true, true, false });
        Capture("diagnostic-no-shadow");
        setLayers.Invoke(debug, new object[] { true, true, true });
    }

    private static void Capture(string name)
    {
        var camera = Camera.main;
        var target = new RenderTexture(390, 844, 24);
        var oldRect = camera.rect;
        var oldSize = camera.orthographicSize;
        camera.targetTexture = target;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.aspect = 390f / 844f;
        camera.orthographicSize = 22f / (2f * camera.aspect);
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
        pixels.Apply();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.rect = oldRect;
        camera.orthographicSize = oldSize;
        target.Release();
        Object.Destroy(pixels);
        Object.Destroy(target);
    }

    [UnityTest]
    public IEnumerator ShadowsRemainSoftAfterMovesAndReloads()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { System.Enum.Parse(Type("WeaponType"), "Spear") });
        save.GetType().GetField("firstBaseGuideDone").SetValue(save, true);
        var entries = save.GetType().GetField("buildings").GetValue(save) as System.Collections.IEnumerable;
        foreach (var entry in entries)
            if (entry.GetType().GetField("buildingType").GetValue(entry).ToString() == "MainBase")
                entry.GetType().GetField("level").SetValue(entry, 5);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, null);
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 50; frame++) yield return null;

        var previousPositions = new System.Collections.Generic.List<Vector3>();
        for (int move = 0; move < 3; move++)
        {
            var map = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
            var mine = FindBuilding("GoldMine");
            var placed = map.GetType().GetMethod("GetMapObject").Invoke(map,
                new object[] { mine.gameObject });
            bool moved = false;
            for (int y = 5; y < 32 && !moved; y++)
            for (int x = 4; x < 18 && !moved; x++)
            {
                var candidate = new Vector3(x + 0.5f, y, 0f);
                if (previousPositions.Any(old => Vector3.Distance(old, candidate) < 7f)) continue;
                if (Vector3.Distance(mine.transform.position, candidate) < 5f) continue;
                moved = (bool)map.GetType().GetMethod("MoveObjectTo").Invoke(map,
                    new[] { placed, (object)candidate });
            }
            Assert.IsTrue(moved, "Three distinct valid mine positions required");
            previousPositions.Add(mine.transform.position);
            for (int frame = 0; frame < 3; frame++) yield return null;
            var road = GameObject.Find("StoneRoadTilemap")?.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            Assert.NotNull(road);
            Assert.Greater(road.GetUsedTilesCount(), 0, "A* route after mine move");
            AssertSoftCorners(mine, "mine move " + move);
            Capture("item-01-mine-move-" + (move + 1));
            SceneManager.LoadScene("BaseScene");
            for (int frame = 0; frame < 50; frame++) yield return null;
            AssertSoftCorners(FindBuilding("GoldMine"), "mine reload " + move);
        }

        var finalMap = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
        foreach (var name in new[] { "ArcherTower", "CannonTower", "Monastery" })
        {
            var building = FindBuilding(name);
            if (building == null)
            {
                finalMap.GetType().GetMethod("SpawnBuilding").Invoke(finalMap, new object[] {
                    System.Enum.Parse(Type("BuildingType"), name), Vector3.zero, null });
                for (int frame = 0; frame < 3; frame++) yield return null;
                building = FindBuilding(name);
            }
            Assert.NotNull(building, name);
            AssertSoftCorners(building, name);
        }
        Capture("item-01-all-building-shadows");
        CaptureGameView("item-01-base-game-view.png");
        finalMap.GetType().GetMethod("ApplyRecommendedLayout").Invoke(finalMap, null);
        for (int frame = 0; frame < 3; frame++) yield return null;
        CaptureGameView("item-04-base-roads-grass.png");
    }

    private static Component FindBuilding(string name) =>
        Object.FindObjectsByType(Type("BuildingIdentifier"), FindObjectsSortMode.None)
            .OfType<Component>().FirstOrDefault(item =>
                item.GetType().GetField("buildingType").GetValue(item).ToString() == name);

    private static void AssertSoftCorners(Component building, string context)
    {
        Assert.NotNull(building.transform.Find("ZeminGolgesi"), context + " shadow child");
        var camera = Camera.main;
        var target = new RenderTexture(390, 844, 24);
        var oldRect = camera.rect;
        var oldSize = camera.orthographicSize;
        camera.targetTexture = target;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.aspect = 390f / 844f;
        camera.orthographicSize = 22f / (2f * camera.aspect);
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
        pixels.Apply();
        var footprint = (Vector2)(Vector2Int)Type("BaseWorldLayout").GetMethod("Footprint").Invoke(null,
            new[] { building.GetType().GetField("buildingType").GetValue(building) });
        var center = (Vector2)building.transform.position;
        foreach (int sx in new[] { -1, 1 })
        foreach (int sy in new[] { -1, 1 })
        {
            // Probe the visible ground just outside each building corner, where
            // opaque wall/roof pixels cannot be mistaken for a ground patch.
            var corner = center + new Vector2(sx * footprint.x * 0.55f,
                sy * footprint.y * 0.55f);
            var neighbor = center + new Vector2(sx * (footprint.x * 0.5f + 0.7f),
                sy * (footprint.y * 0.5f + 0.7f));
            float value = Luminance(pixels, camera, corner);
            float adjacent = Luminance(pixels, camera, neighbor);
            Assert.GreaterOrEqual(value, 0.18f, context + " black corner " + sx + "," + sy);
            Assert.GreaterOrEqual(value, adjacent * 0.55f,
                context + " corner exceeds 45% shadow " + sx + "," + sy);
        }
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.rect = oldRect;
        camera.orthographicSize = oldSize;
        target.Release();
        Object.Destroy(pixels);
        Object.Destroy(target);
    }

    private static float Luminance(Texture2D pixels, Camera camera, Vector2 world)
    {
        Vector3 view = camera.WorldToViewportPoint(world);
        int x = Mathf.Clamp(Mathf.RoundToInt(view.x * 389f), 0, 389);
        int y = Mathf.Clamp(Mathf.RoundToInt(view.y * 843f), 0, 843);
        Color color = pixels.GetPixel(x, y);
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }

    private static void CaptureGameView(string file)
    {
        typeof(HudExpansionReviewTests).GetMethod("Capture",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { file });
    }
}
