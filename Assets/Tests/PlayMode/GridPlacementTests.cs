using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Serbest ızgara yerleştirme: bina/dekor/kulübe çakışmaz, slot dışı hücreler geçerlidir, taşınan
/// binanın eski hücreleri boşalır, kayıt-yükleme konumu korur ve çakışan kayıt onarılır.
/// </summary>
public class GridPlacementTests
{
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object BuildingType(string name) => Enum.Parse(RuntimeType("BuildingType"), name);

    private static Component Map() => Object.FindFirstObjectByType(RuntimeType("BaseMapManager")) as Component;

    private static object Manager() => RuntimeType("GameManager").GetProperty("Instance").GetValue(null);

    private static IEnumerator LoadFreshBase()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = RuntimeType("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = RuntimeType("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(RuntimeType("WeaponType"), "Spear") });
        save.GetType().GetField("firstBaseGuideDone").SetValue(save, true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(manager, null);
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 60; frame++) yield return null;
        Assert.NotNull(Map(), "BaseMapManager sahnede olmalı");
    }

    private static bool CanPlaceBuilding(string type, Vector2 center, object moving = null) =>
        (bool)Map().GetType().GetMethod("CanPlaceBuildingAt")
            .Invoke(Map(), new object[] { BuildingType(type), center, moving });

    private static object SpawnBuilding(string type, Vector2 center, string id = null) =>
        Map().GetType().GetMethod("SpawnBuilding").Invoke(Map(),
            new object[] { BuildingType(type), new Vector3(center.x, center.y, 0f), id });

    private static GameObject ObjectOf(object placed) =>
        (GameObject)placed.GetType().GetField("gameObject").GetValue(placed);

    private static string IdOf(object placed) => (string)placed.GetType().GetField("id").GetValue(placed);

    private static Vector2 PositionOf(object placed) => ObjectOf(placed).transform.position;

    private static object PlacedById(string id) =>
        Map().GetType().GetMethod("GetMapObjectBySlot").Invoke(Map(), new object[] { id });

    private static bool IsLegacySlotCenter(Vector2 center)
    {
        var slots = (Array)RuntimeType("BaseWorldLayout").GetField("Slots").GetValue(null);
        foreach (var slot in slots)
        {
            var slotCenter = (Vector2)slot.GetType().GetProperty("center").GetValue(slot);
            if (Vector2.Distance(slotCenter, center) < 0.01f) return true;
        }
        return false;
    }

    /// <summary>Verilen türden bir bina için, slot olmayan ve geçerli ilk merkezler (uzaklığa göre).</summary>
    private static List<Vector2> FreeCenters(string type)
    {
        var footprint = (Vector2Int)RuntimeType("BaseWorldLayout").GetMethod("Footprint")
            .Invoke(null, new[] { BuildingType(type) });
        var list = new List<Vector2>();
        for (int y = 3; y < 34; y++)
        for (int x = 3; x < 19; x++)
        {
            var center = new Vector2(x + footprint.x * 0.5f, y + footprint.y * 0.5f);
            if (IsLegacySlotCenter(center) || !CanPlaceBuilding(type, center)) continue;
            list.Add(center);
        }
        return list;
    }

    private static (Component placement, object data) SmallestDecor()
    {
        var placement = Object.FindFirstObjectByType(RuntimeType("DecorPlacementController")) as Component;
        Assert.NotNull(placement);
        var all = Resources.LoadAll("Decor", RuntimeType("DecorData"));
        Assert.Greater(all.Length, 0);
        object best = null;
        int bestArea = int.MaxValue;
        foreach (var item in all)
        {
            var footprint = (Vector2Int)item.GetType().GetField("footprint").GetValue(item);
            if (footprint.x * footprint.y >= bestArea) continue;
            bestArea = footprint.x * footprint.y;
            best = item;
        }
        return (placement, best);
    }

    private static bool CanPlaceDecor(Component placement, object data, Vector2Int cell) =>
        (bool)placement.GetType().GetMethod("CanPlace").Invoke(placement, new[] { data, cell, null });

    private static List<RectInt> AllFootprints()
    {
        var rects = new List<RectInt>();
        var layout = RuntimeType("BaseWorldLayout");
        foreach (var component in Object.FindObjectsByType(RuntimeType("BuildingIdentifier"),
                     FindObjectsSortMode.None).OfType<Component>())
        {
            var type = component.GetType().GetField("buildingType").GetValue(component);
            var size = (Vector2Int)layout.GetMethod("Footprint").Invoke(null, new[] { type });
            Vector3 p = component.transform.position;
            rects.Add(new RectInt(Mathf.RoundToInt(p.x - size.x * 0.5f), Mathf.RoundToInt(p.y - size.y * 0.5f),
                size.x, size.y));
        }
        foreach (var hut in Object.FindObjectsByType(RuntimeType("BuilderHut"), FindObjectsSortMode.None)
                     .OfType<Component>())
        {
            Vector3 p = hut.transform.position;
            rects.Add(new RectInt(Mathf.RoundToInt(p.x - 1f), Mathf.RoundToInt(p.y - 1f), 2, 2));
        }
        return rects;
    }

    private static void AssertNoOverlaps()
    {
        var rects = AllFootprints();
        for (int i = 0; i < rects.Count; i++)
        for (int j = i + 1; j < rects.Count; j++)
        {
            bool overlap = rects[i].xMin < rects[j].xMax && rects[i].xMax > rects[j].xMin &&
                rects[i].yMin < rects[j].yMax && rects[i].yMax > rects[j].yMin;
            Assert.IsFalse(overlap, $"Çakışma: {rects[i]} ve {rects[j]}");
        }
    }

    [UnityTest]
    public IEnumerator OverlapRejected_FreeNonSlotCellAccepted_MoveFreesOldCells()
    {
        yield return LoadFreshBase();
        var main = PlacedById("main");
        Assert.NotNull(main, "Yeni oyunda Ana Üs olmalı");
        Vector2 mainPosition = PositionOf(main);

        // (a) bina bina üstüne, dekor binanın üstüne konamaz
        Assert.IsFalse(CanPlaceBuilding("CannonTower", mainPosition), "Ana Üs üstüne bina");
        Assert.IsFalse(CanPlaceBuilding("ArcherTower", mainPosition + new Vector2(2f, 1f)), "Ana Üs'e kısmi çakışma");
        var (placement, decor) = SmallestDecor();
        var mainCell = new Vector2Int(Mathf.RoundToInt(mainPosition.x), Mathf.RoundToInt(mainPosition.y));
        Assert.IsFalse(CanPlaceDecor(placement, decor, mainCell), "Ana Üs üstüne dekor");

        // (b) slot olmayan boş hücre kabul edilir ve tam o hücreye konur
        var spots = FreeCenters("CannonTower");
        Assert.GreaterOrEqual(spots.Count, 2, "Açık arazide boş hücre bulunmalı");
        Vector2 a = spots[0];
        Assert.IsTrue(CanPlaceBuilding("CannonTower", a));
        var first = SpawnBuilding("CannonTower", a);
        Assert.NotNull(first);
        Assert.AreEqual(a.x, PositionOf(first).x, 0.01f);
        Assert.AreEqual(a.y, PositionOf(first).y, 0.01f);
        Assert.IsTrue(IdOf(first).StartsWith("cannontower_"), "Benzersiz kimlik: " + IdOf(first));
        Assert.IsFalse(CanPlaceBuilding("CannonTower", a), "Aynı hücreye ikinci bina");
        Assert.IsFalse(CanPlaceBuilding("CannonTower", a + Vector2.right), "Kısmi çakışma");
        var aCell = new Vector2Int(Mathf.FloorToInt(a.x), Mathf.FloorToInt(a.y));
        Assert.IsFalse(CanPlaceDecor(placement, decor, aCell), "Yeni binanın üstüne dekor");

        // (c) taşıma: eski hücreler boşalır, yeni hücreler dolar, kendi kendini yok sayar
        Vector2 b = spots.First(s => Vector2.Distance(s, a) > 5f && CanPlaceBuilding("CannonTower", s));
        Assert.IsTrue((bool)Map().GetType().GetMethod("MoveObjectTo")
            .Invoke(Map(), new[] { first, (object)new Vector3(b.x, b.y, 0f) }));
        Assert.IsTrue(CanPlaceBuilding("CannonTower", a), "Eski hücreler boşalmalı");
        Assert.IsFalse(CanPlaceBuilding("CannonTower", b), "Yeni hücreler dolu");
        Assert.IsTrue(CanPlaceBuilding("CannonTower", b, first), "Taşınan nesne kendini yok sayar");
        Assert.IsFalse(CanPlaceBuilding("CannonTower", b + Vector2.right), "Taşınanın üstüne yeni bina");
        var second = SpawnBuilding("CannonTower", a);
        Assert.NotNull(second);
        Assert.AreNotEqual(IdOf(first), IdOf(second), "Kimlikler benzersiz");
        Assert.AreEqual(a.x, PositionOf(second).x, 0.01f);
        AssertNoOverlaps();
    }

    [UnityTest]
    public IEnumerator SaveLoadKeepsPositions_AndRepairsOverlappingSave()
    {
        yield return LoadFreshBase();
        var main = PlacedById("main");
        Vector2 mainPosition = PositionOf(main);
        var spots = FreeCenters("CannonTower");
        Vector2 a = spots[0];
        var placed = SpawnBuilding("CannonTower", a);
        string placedId = IdOf(placed);
        Map().GetType().GetMethod("SaveAll").Invoke(Map(), null);

        // (d1) kayıt -> yeniden yükleme konumu korur
        yield return Reload();
        var reloaded = PlacedById(placedId);
        Assert.NotNull(reloaded, "Kayıtlı bina geri gelmeli");
        Assert.AreEqual(a.x, PositionOf(reloaded).x, 0.01f);
        Assert.AreEqual(a.y, PositionOf(reloaded).y, 0.01f);
        AssertNoOverlaps();

        // (d2) yapay çakışan kayıt: Ana Üs'ün üstüne ve birbirinin üstüne bina kayıtları
        var player = RuntimeType("GameManager").GetProperty("PlayerData").GetValue(Manager());
        var list = (System.Collections.IList)player.GetType().GetField("placedBuildings").GetValue(player);
        var recordType = RuntimeType("PlacedBuildingData");
        Vector2 stack = spots.Last();
        foreach (var (id, p) in new[] { ("overlap_main", mainPosition), ("overlap_a", stack), ("overlap_b", stack) })
        {
            var record = Activator.CreateInstance(recordType);
            recordType.GetField("slotId").SetValue(record, id);
            recordType.GetField("buildingType").SetValue(record, "CannonTower");
            recordType.GetField("hasPosition").SetValue(record, true);
            recordType.GetField("x").SetValue(record, p.x);
            recordType.GetField("y").SetValue(record, p.y);
            recordType.GetField("level").SetValue(record, 1);
            list.Add(record);
        }
        yield return Reload();
        foreach (var id in new[] { "overlap_main", "overlap_a", "overlap_b" })
            Assert.NotNull(PlacedById(id), id + " kaybolmamalı");
        Assert.Greater(Vector2.Distance(PositionOf(PlacedById("overlap_main")), mainPosition), 1f,
            "Ana Üs'e binen kayıt taşınmalı");
        Assert.AreNotEqual(PositionOf(PlacedById("overlap_a")), PositionOf(PlacedById("overlap_b")),
            "Üst üste kayıtlar ayrılmalı");
        AssertNoOverlaps();

        // Onarım kaydedilmiş olmalı: tekrar yüklemede de çakışma yok
        yield return Reload();
        AssertNoOverlaps();
    }

    private static IEnumerator Reload()
    {
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 60; frame++) yield return null;
        Assert.NotNull(Map());
    }
}
