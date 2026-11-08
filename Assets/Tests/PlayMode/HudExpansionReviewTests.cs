using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class HudExpansionReviewTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [UnityTest]
    public IEnumerator OwnershipBattleCardAndLevelVisual()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("ReviewGameManager").AddComponent(managerType);
        yield return null;
        gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        Assert.NotNull(gm);
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Spear") });
        gm.GetType().GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        gm.GetType().GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(gm, null);
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 24; frame++) yield return null;
        var manager = Object.FindFirstObjectByType(Type("BattleManager"));
        var archer = Enum.Parse(Type("BuildingType"), "ArcherTower");
        var allowance = manager.GetType().GetMethod("GetBattleBuildAllowance");
        Assert.AreEqual(0, allowance.Invoke(manager, new[] { archer }));
        var hero = Object.FindFirstObjectByType(Type("HeroController")) as Component;
        var slot = Object.FindObjectsByType(Type("BattleBuildSlot"), FindObjectsSortMode.None)
            .Cast<Component>().First();
        manager.GetType().GetField("battleActive", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(manager, true);
        hero.transform.position = slot.transform.position;
        var body = hero.GetComponent<Rigidbody2D>();
        if (body != null) body.position = slot.transform.position;
        Physics2D.SyncTransforms();
        slot.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(slot, null);
        Capture("ownership-item-01.png");

        var apprentice = Object.FindFirstObjectByType(Type("BattleApprentice"));
        Assert.NotNull(apprentice);
        var progression = Resources.Load("ApprenticeProgressionData", Type("ApprenticeProgressionData"));
        Assert.NotNull(progression);
        var expectedHealth = (int)progression.GetType().GetMethod("Health").Invoke(progression,
            new[] { Enum.Parse(Type("WeaponType"), "Spear"), (object)1 });
        Assert.AreEqual(expectedHealth, apprentice.GetType().GetProperty("MaxHealth").GetValue(apprentice));
        hero.transform.position = new Vector3(-2f, 0f, 0f);
        if (body != null) body.position = hero.transform.position;
        slot.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(slot, null);
        Capture("battle-item-06.png");

        var placed = (System.Collections.IList)save.GetType().GetField("placedBuildings").GetValue(save);
        var recordType = Type("PlacedBuildingData");
        for (int i = 0; i < 2; i++)
        {
            var entry = Activator.CreateInstance(recordType);
            recordType.GetField("buildingType").SetValue(entry, "ArcherTower");
            recordType.GetField("slotId").SetValue(entry, "archer_" + (i + 1));
            placed.GetType().GetMethod("Add").Invoke(placed, new[] { entry });
        }
        Assert.AreEqual(2, allowance.Invoke(manager, new[] { archer }));
        var slots = Object.FindObjectsByType(Type("BattleBuildSlot"), FindObjectsSortMode.None)
            .Cast<Component>().ToArray();
        var secondEntry = placed[placed.Count - 1];
        placed.RemoveAt(placed.Count - 1);
        slots[0].GetType().GetField("isBuilt", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(slots[0], true);
        slots[0].GetType().GetField("builtType", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(slots[0], archer);
        manager.GetType().GetField("totalGoldEarned", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(manager, 500);
        var buildings = gm.GetType().GetProperty("Buildings").GetValue(gm) as System.Collections.IDictionary;
        var archerData = buildings[archer];
        archerData.GetType().GetField("isUnlocked").SetValue(archerData, true);
        Assert.IsFalse((bool)manager.GetType().GetMethod("CanBuildFromSlot")
            .Invoke(manager, new[] { archer, (object)50 }));
        placed.GetType().GetMethod("Add").Invoke(placed, new[] { secondEntry });
        slots[0].GetType().GetField("isBuilt", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(slots[0], false);
        for (int i = 0; i < 2; i++)
        {
            slots[i].GetType().GetField("isBuilt", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(slots[i], true);
            slots[i].GetType().GetField("builtType", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(slots[i], archer);
            Assert.AreEqual(1 - i, manager.GetType().GetMethod("GetBattleBuildRemaining")
                .Invoke(manager, new[] { archer }));
        }
        var visualAtOne = archerData.GetType().GetMethod("VisualForLevel").Invoke(archerData,
            new object[] { 1 });
        // Renk 3 seviyede bir değişir: 4. seviye ikinci (sarı) kademe. Savaş kulesi
        // binanın kendi seviyesini kullanır, bu yüzden kayıtlı Okçu binaları da 4 yapılır.
        archerData.GetType().GetField("level").SetValue(archerData, 4);
        foreach (var entry in placed)
            if ((string)recordType.GetField("buildingType").GetValue(entry) == "ArcherTower")
                recordType.GetField("level").SetValue(entry, 4);
        var visualAtThree = archerData.GetType().GetMethod("VisualForLevel").Invoke(archerData,
            new object[] { 4 });
        Assert.AreNotEqual(visualAtOne.GetType().GetField("sprite").GetValue(visualAtOne),
            visualAtThree.GetType().GetField("sprite").GetValue(visualAtThree));
        manager.GetType().GetMethod("CreateTower", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, new object[] { new Vector3(0f, 0f, 0f), archer });
        var tower = GameObject.Find("ArcherTower");
        Assert.NotNull(tower);
        Assert.AreEqual(visualAtThree.GetType().GetField("sprite").GetValue(visualAtThree),
            tower.GetComponent<SpriteRenderer>().sprite);
    }

    [UnityTest]
    public IEnumerator BaseHudLandMenuMoveAndApprenticeReview()
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
        gm.GetType().GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        gm.GetType().GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 30; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        var debug = Object.FindFirstObjectByType(Type("BaseDebugOverlay")) as Component;
        Assert.NotNull(debug);
        debug.GetType().GetMethod("SetVisible").Invoke(debug, new object[] { true });
        Assert.Greater(Camera.main.rect.yMin, 0f);
        Assert.Less(Camera.main.rect.yMax, 1f);
        Capture("viewport-item-02.png");

        var layout = Type("BaseWorldLayout");
        var atLevel = layout.GetMethod("CleanAtLevel");
        var first = (RectInt)atLevel.Invoke(null, new object[] { 1 });
        // Arazi renk kademesiyle genişler: 1–3 aynı, 13'te tamamen açık.
        var third = (RectInt)atLevel.Invoke(null, new object[] { 3 });
        var full = (RectInt)atLevel.Invoke(null, new object[] { 13 });
        Assert.AreEqual(new RectInt(2, 12, 18, 14), first);
        Assert.AreEqual(first, third);
        Assert.AreEqual(new RectInt(2, 2, 18, 34), full);
        Capture("land-item-03.png");

        var menu = Object.FindFirstObjectByType(Type("BuildMenuController"));
        menu.GetType().GetMethod("Show").Invoke(menu, null);
        yield return new WaitForSecondsRealtime(0.35f);
        Capture("build-menu-item-04.png");
        menu.GetType().GetMethod("Hide").Invoke(menu, null);
        yield return new WaitForSecondsRealtime(0.35f);

        var map = Object.FindFirstObjectByType(Type("BaseMapManager"));
        var archer = Enum.Parse(Type("BuildingType"), "ArcherTower");
        var spawned = map.GetType().GetMethod("SpawnBuilding").Invoke(map,
            new object[] { archer, Vector3.zero, "archer_1" });
        Assert.NotNull(spawned);
        var canPlace = map.GetType().GetMethod("CanPlaceBuildingAt");
        Assert.IsFalse((bool)canPlace.Invoke(map, new object[] { archer, new Vector2(0, 16), spawned }));
        Assert.IsFalse((bool)canPlace.Invoke(map, new object[] { archer, new Vector2(4, 5), spawned }));
        Assert.IsFalse((bool)canPlace.Invoke(map, new object[] { archer, new Vector2(10.5f, 22), spawned }));
        Vector2 moveTarget = Vector2.zero;
        bool foundMoveTarget = false;
        for (int y = 15; y <= 28 && !foundMoveTarget; y++)
        for (int x = 4; x <= 17 && !foundMoveTarget; x++)
        {
            var candidate = new Vector2(x, y);
            if (!(bool)canPlace.Invoke(map, new object[] { archer, candidate, spawned })) continue;
            moveTarget = candidate;
            foundMoveTarget = true;
        }
        Assert.IsTrue(foundMoveTarget, "A valid Archer Tower move cell is available");
        var placement = Object.FindFirstObjectByType(Type("PlacementController"));
        placement.GetType().GetMethod("BeginMove").Invoke(placement, new[] { spawned });
        Capture("move-item-05.png");
        placement.GetType().GetMethod("CancelPlacement").Invoke(placement, null);
        Assert.IsTrue((bool)map.GetType().GetMethod("MoveObjectTo").Invoke(map,
            new object[] { spawned, new Vector3(moveTarget.x, moveTarget.y, 0) }));
        var placed = (System.Collections.IList)save.GetType().GetField("placedBuildings").GetValue(save);
        var movedRecord = placed.Cast<object>().First(item =>
            (string)item.GetType().GetField("buildingType").GetValue(item) == "ArcherTower");
        Assert.IsTrue((bool)movedRecord.GetType().GetField("hasPosition").GetValue(movedRecord));
        Assert.AreEqual(moveTarget.x, (float)movedRecord.GetType().GetField("x").GetValue(movedRecord));

        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 25; frame++) yield return null;
        var reloadedArcher = GameObject.Find("Building_ArcherTower_archer_1");
        Assert.NotNull(reloadedArcher);
        Assert.AreEqual(moveTarget.x, reloadedArcher.transform.position.x, 0.01f);
        Assert.AreEqual(moveTarget.y, reloadedArcher.transform.position.y, 0.01f);

        var ui = Object.FindFirstObjectByType(Type("BaseWorldUI"));
        ui.GetType().GetMethod("ShowApprenticeInfo").Invoke(ui, null);
        yield return new WaitForSecondsRealtime(0.35f);
        var progression = Resources.Load("ApprenticeProgressionData", Type("ApprenticeProgressionData"));
        Assert.NotNull(progression);
        var multiplier = progression.GetType().GetMethod("Multiplier");
        var bonus = progression.GetType().GetMethod("MineBonus");
        float[] expectedMultipliers = { 1f, 1.3f, 1.65f, 2.05f, 2.5f };
        for (int level = 1; level <= 5; level++)
        {
            Assert.AreEqual(expectedMultipliers[level - 1],
                (float)multiplier.Invoke(progression, new object[] { level }), 0.001f);
            Assert.AreEqual(5 + level * 5,
                (int)bonus.Invoke(progression, new object[] { level }));
        }
        var health = progression.GetType().GetMethod("Health");
        int levelOneHealth = (int)health.Invoke(progression,
            new[] { Enum.Parse(Type("WeaponType"), "Spear"), (object)1 });
        var stats = ui.GetType().GetField("heroStatsText", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(ui);
        string statsText = (string)stats.GetType().GetProperty("text").GetValue(stats);
        Assert.IsTrue(statsText.Contains(levelOneHealth.ToString()));
        Capture("apprentice-item-07.png");
    }

    private static void Capture(string file)
    {
        var camera = Camera.main;
        Assert.NotNull(camera);
        var texture = new RenderTexture(390, 844, 24);
        var oldTarget = camera.targetTexture;
        float oldAspect = camera.aspect;
        float oldSize = camera.orthographicSize;
        Rect oldRect = camera.rect;
        camera.targetTexture = texture;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.aspect = 390f / 844f;
        camera.orthographicSize = SceneManager.GetActiveScene().name == "BaseScene"
            ? 22f / (2f * camera.aspect) : 8f * 844f / (844f - 84f - 139f);
        var canvases = new List<Canvas>();
        var oldModes = new List<RenderMode>();
        var oldCameras = new List<Camera>();
        var oldOrders = new List<int>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            canvases.Add(canvas);
            oldModes.Add(canvas.renderMode);
            oldCameras.Add(canvas.worldCamera);
            oldOrders.Add(canvas.sortingOrder);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
        pixels.Apply();
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, file), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = oldTarget;
        camera.aspect = oldAspect;
        camera.orthographicSize = oldSize;
        camera.rect = oldRect;
        for (int i = 0; i < canvases.Count; i++)
        {
            if (canvases[i] == null) continue;
            canvases[i].renderMode = oldModes[i];
            canvases[i].worldCamera = oldCameras[i];
            canvases[i].sortingOrder = oldOrders[i];
        }
        texture.Release();
        Object.Destroy(texture);
    }
}
