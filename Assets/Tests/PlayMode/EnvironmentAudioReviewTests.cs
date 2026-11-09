using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class EnvironmentAudioReviewTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static readonly MethodInfo CaptureMethod = typeof(HudExpansionReviewTests)
        .GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static);

    private static void Capture(string name) => CaptureMethod.Invoke(null, new object[] { name });

    [UnityTest]
    public IEnumerator CaptureMineAndGarrison()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("ReviewGameManager").AddComponent(managerType);
        yield return null;
        gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Spear") });
        save.GetType().GetField("firstBaseGuideDone").SetValue(save, true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new object[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 35; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.NotNull(Object.FindFirstObjectByType(Type("BaseMineVisual")));
        Assert.NotNull(GameObject.Find("MineHouse"));
        Assert.NotNull(GameObject.Find("MinePawn"));
        Assert.AreEqual(new Vector2Int(4, 4), Type("BaseWorldLayout").GetMethod("Footprint")
            .Invoke(null, new[] { Enum.Parse(Type("BuildingType"), "GoldMine") }));
        Capture("new-item-01-mine.png");
        var map = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
        var layout = Type("BaseWorldLayout");
        var spawn = map.GetType().GetMethod("SpawnBuilding");
        foreach (var entry in new[] { new[] { "ArcherTower", "archer_1" },
                     new[] { "CannonTower", "cannon_1" },
                     new[] { "Monastery", "monastery_1" } })
        {
            var slot = layout.GetMethod("Get").Invoke(null, new object[] { entry[1] });
            var center = (Vector2)slot.GetType().GetProperty("center").GetValue(slot);
            var placed = spawn.Invoke(map, new object[] {
                Enum.Parse(Type("BuildingType"), entry[0]),
                new Vector3(center.x, center.y), entry[1] });
            Assert.NotNull(placed);
            if (entry[0] == "CannonTower")
                Assert.IsTrue((bool)map.GetType().GetMethod("MoveObjectTo").Invoke(map,
                    new object[] { placed, new Vector3(5f, 18f) }));
        }
        map.GetType().GetMethod("SaveAll").Invoke(map, null);
        yield return new WaitForSecondsRealtime(0.45f);
        Assert.NotNull(Object.FindFirstObjectByType(Type("BaseGarrisonVisual")));
        Capture("new-item-02-garrison.png");
        var debug = Object.FindFirstObjectByType(Type("BaseDebugOverlay"));
        debug?.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { false });
        Capture("new-item-02-garrison-clean.png");
    }

    [UnityTest]
    public IEnumerator CapturePatchesAndRoads()
    {
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 35; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsNull(GameObject.Find("YamaTilemap"));
        Assert.NotNull(GameObject.Find("StoneRoadTilemap"));
        var road = GameObject.Find("StoneRoadTilemap").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        Assert.Greater(road.GetUsedTilesCount(), 0);
        Assert.IsTrue(road.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().enabled);
        Capture("new-item-03-soft-shadows-roads.png");
        var mine = Object.FindFirstObjectByType(Type("BaseMineVisual")) as Component;
        var map = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
        Assert.NotNull(mine);
        var original = mine.transform.parent.position;
        var placed = map.GetType().GetMethod("GetMapObject").Invoke(map,
            new object[] { mine.transform.parent.gameObject });
        Assert.IsTrue((bool)map.GetType().GetMethod("MoveObjectTo").Invoke(map,
            new object[] { placed, new Vector3(17f, 16f) }));
        Assert.NotNull(mine.transform.parent.Find("ZeminGolgesi"));
        Assert.AreEqual(mine.transform.parent.position, mine.transform.parent.Find("ZeminGolgesi").parent.position);
        Assert.Greater(road.GetUsedTilesCount(), 0);
    }

    [UnityTest]
    public IEnumerator LevelVisualsUseCatalogSprites()
    {
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 35; i++) yield return null;
        var gm = Type("GameManager").GetProperty("Instance").GetValue(null);
        var dictionary = gm.GetType().GetProperty("Buildings").GetValue(gm)
            as System.Collections.IDictionary;
        var map = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
        foreach (var name in new[] { "MainBase", "ArcherTower", "CannonTower", "Monastery" })
        {
            var building = Object.FindObjectsByType(Type("BuildingIdentifier"), FindObjectsSortMode.None)
                .Cast<Component>().FirstOrDefault(item => item.GetType().GetField("buildingType")
                    .GetValue(item).ToString() == name);
            if (building == null)
            {
                string slotId = name == "ArcherTower" ? "archer_1" :
                    name == "CannonTower" ? "cannon_1" : "monastery_1";
                var slot = Type("BaseWorldLayout").GetMethod("Get").Invoke(null,
                    new object[] { slotId });
                Vector2 center = (Vector2)slot.GetType().GetProperty("center").GetValue(slot);
                var placed = map.GetType().GetMethod("SpawnBuilding").Invoke(map,
                    new object[] { Enum.Parse(Type("BuildingType"), name),
                        new Vector3(center.x, center.y), slotId });
                building = ((GameObject)placed.GetType().GetField("gameObject").GetValue(placed))
                    .GetComponent(Type("BuildingIdentifier"));
            }
            var data = dictionary[Enum.Parse(Type("BuildingType"), name)];
            var visual = building.GetComponent(Type("BaseBuildingVisual"));
            var body = building.GetComponent<SpriteRenderer>();
            var apply = visual.GetType().GetMethod("ApplyLevel", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int level = 1; level <= 5; level++)
            {
                var expected = data.GetType().GetMethod("VisualForLevel").Invoke(data, new object[] { level });
                var sprite = expected.GetType().GetField("sprite").GetValue(expected) as Sprite;
                Assert.NotNull(sprite, name + " seviye sprite'ı eksik: " + level);
                apply.Invoke(visual, new object[] { level });
                yield return null;
                Assert.AreSame(sprite, body.sprite, name + " yanlış seviye sprite'ı: " + level);
            }
        }
        var mine = Object.FindFirstObjectByType(Type("BaseMineVisual"));
        Assert.NotNull(mine);
        var mineData = dictionary[Enum.Parse(Type("BuildingType"), "GoldMine")];
        var upgraded = mine.GetType().GetMethod("OnBuildingUpgraded",
            BindingFlags.NonPublic | BindingFlags.Instance);
        for (int level = 1; level <= 5; level++)
        {
            upgraded.Invoke(mine, new[] { Enum.Parse(Type("BuildingType"), "GoldMine"), (object)level });
            var expected = mineData.GetType().GetMethod("VisualForLevel").Invoke(mineData,
                new object[] { level });
            Assert.AreEqual(expected.GetType().GetField("color").GetValue(expected),
                mine.GetType().GetProperty("ThemeColor").GetValue(mine));
        }
        Capture("new-item-04-level-five.png");
    }

    [UnityTest]
    public IEnumerator ForestIdentityLeavesRegionOneUntouched()
    {
        var session = Type("BattleSession");
        session.GetMethod("Begin").Invoke(null, new object[] { 2, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 35; i++) yield return null;
        Assert.NotNull(GameObject.Find("ForestRadialFog"));
        var region = session.GetProperty("Region").GetValue(null);
        var expectedGround = region.GetType().GetField("groundTile").GetValue(region) as Sprite;
        Assert.AreSame(expectedGround, GameObject.Find("BattleGround").GetComponent<SpriteRenderer>().sprite);
        Capture("new-item-05-forest.png");

        session.GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 35; i++) yield return null;
        Assert.IsNull(GameObject.Find("ForestRadialFog"));
        region = session.GetProperty("Region").GetValue(null);
        expectedGround = region.GetType().GetField("groundTile").GetValue(region) as Sprite;
        Assert.AreSame(expectedGround, GameObject.Find("BattleGround").GetComponent<SpriteRenderer>().sprite);
        session.GetMethod("Clear").Invoke(null, null);
    }

    [UnityTest]
    public IEnumerator AudioSettingsShowsAndPersistsControls()
    {
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 35; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        var audio = Object.FindFirstObjectByType(Type("AudioManager"));
        var settings = Object.FindFirstObjectByType(Type("AudioSettingsUI"));
        Assert.NotNull(audio);
        Assert.NotNull(settings);
        Assert.IsTrue(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
            .Any(listener => listener.enabled));
        Assert.NotNull(GameObject.Find("AudioSettingsButton"));
        settings.GetType().GetMethod("Open").Invoke(settings, null);
        Assert.IsTrue(GameObject.Find("AudioSettingsPanel").activeSelf);
        Capture("new-item-06-audio-settings.png");
    }
}
