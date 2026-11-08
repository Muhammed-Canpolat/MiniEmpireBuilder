using System.Collections;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class ExpansionFeatureTests
{
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Value(object target, string name) =>
        target.GetType().GetProperty(name)?.GetValue(target) ??
        target.GetType().GetField(name)?.GetValue(target);

    [UnityTest]
    public IEnumerator DecorCatalogGridAndTabs()
    {
        Screen.SetResolution(390, 844, false);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 20; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        var decor = Resources.LoadAll("Decor", RuntimeType("DecorData"));
        Assert.AreEqual(17, decor.Length); // 8 eski kimlik + 9 yeni köy/doğa dekoru
        var fountain = decor.Single(item => (string)Value(item, "id") == "fountain");
        Assert.AreEqual(80, Value(fountain, "cost"));
        Assert.AreEqual(3, Value(fountain, "unlockMainBaseLevel"));
        Assert.AreEqual(new Vector2Int(2, 2), Value(
            decor.Single(item => (string)Value(item, "id") == "tree"), "footprint"));
        Assert.IsTrue(decor.All(item => Value(item, "sprite") != null));
        var placement = Object.FindFirstObjectByType(RuntimeType("DecorPlacementController"));
        Assert.NotNull(placement);
        var map = Object.FindFirstObjectByType(RuntimeType("BaseMapManager"));
        if (!Object.FindObjectsByType(RuntimeType("BuildingIdentifier"), FindObjectsSortMode.None)
            .Any(item => Value(item, "buildingType").ToString() == "MainBase"))
            map.GetType().GetMethod("SpawnBuilding").Invoke(map, new object[] {
                Enum.Parse(RuntimeType("BuildingType"), "MainBase"),
                new Vector3(10.5f, 22f), "main" });
        var bush = decor.Single(item => (string)Value(item, "id") == "bush");
        var canPlace = placement.GetType().GetMethod("CanPlace");
        Assert.IsFalse((bool)canPlace.Invoke(placement, new object[] { bush, Vector2Int.zero, null }));
        Assert.IsFalse((bool)canPlace.Invoke(placement, new object[] { bush, new Vector2Int(10, 22), null }));
        var freeCell = Enumerable.Range(12, 14)
            .SelectMany(y => Enumerable.Range(2, 18).Select(x => new Vector2Int(x, y)))
            .First(cell => (bool)canPlace.Invoke(placement, new object[] { bush, cell, null }));
        Assert.IsTrue((bool)canPlace.Invoke(placement, new object[] { bush, freeCell, null }));
        var spawn = placement.GetType().GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
        var previewItem = (Component)spawn.Invoke(placement,
            new object[] { bush, freeCell, "test-only" });
        Assert.IsFalse((bool)canPlace.Invoke(placement, new object[] { bush, freeCell, null }));
        Assert.IsTrue((bool)canPlace.Invoke(placement, new object[] { bush, freeCell, previewItem }));
        var runtimePlaced = placement.GetType().GetField("placed", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(placement) as System.Collections.IList;
        runtimePlaced.Remove(previewItem);
        Object.Destroy(previewItem.gameObject);
        var playerType = RuntimeType("PlayerSaveData");
        var recordType = RuntimeType("PlacedDecorSave");
        var player = Activator.CreateInstance(playerType);
        var record = Activator.CreateInstance(recordType);
        recordType.GetField("instanceId").SetValue(record, "sample");
        recordType.GetField("decorId").SetValue(record, "tree");
        recordType.GetField("x").SetValue(record, 3);
        recordType.GetField("y").SetValue(record, 7);
        var records = playerType.GetField("placedDecor").GetValue(player);
        records.GetType().GetMethod("Add").Invoke(records, new[] { record });
        var copy = JsonUtility.FromJson(JsonUtility.ToJson(player), playerType);
        var copies = playerType.GetField("placedDecor").GetValue(copy) as System.Collections.IList;
        Assert.AreEqual("tree", Value(copies[0], "decorId"));

        var menu = Object.FindFirstObjectByType(RuntimeType("BuildMenuController"));
        menu.GetType().GetMethod("Show").Invoke(menu, null);
        var tab = GameObject.Find("Tab_DEKOR");
        Assert.NotNull(tab);
        tab.GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(tab.transform.Find("GoldUnderline").gameObject.activeSelf);
        Assert.AreEqual(17, GameObject.Find("BuildMenuPanel").GetComponentsInChildren<Button>()
            .Count(button => button.name.StartsWith("Decor_")));
        yield return new WaitForSecondsRealtime(0.35f);
        Capture("expansion-decor-menu.png");
        var gameManager = Object.FindFirstObjectByType(RuntimeType("GameManager"));
        var activeSave = Value(gameManager, "PlayerData");
        var apprenticeLevel = activeSave.GetType().GetField("apprenticeLevel");
        int savedLevel = (int)apprenticeLevel.GetValue(activeSave);
        try
        {
            float baseIncome = 0f;
            for (int level = 1; level <= 5; level++)
            {
                apprenticeLevel.SetValue(activeSave, level);
                int cost = (int)gameManager.GetType().GetMethod("GetApprenticeUpgradeCost")
                    .Invoke(gameManager, null);
                // 15 seviyelik eğri: 80 × 1.3^(sev-1), 10'a yuvarlı.
                Assert.AreEqual(new[] { 80, 100, 140, 180, 230 }[level - 1], cost);
                float income = (float)gameManager.GetType().GetMethod("GetGoldPerSecond")
                    .Invoke(gameManager, null);
                if (level == 1) baseIncome = income / 1.1f;
                Assert.AreEqual(baseIncome * (1.1f + (level - 1) * 0.05f), income, 0.001f);
            }
        }
        finally { apprenticeLevel.SetValue(activeSave, savedLevel); }
        var worldUi = Object.FindFirstObjectByType(RuntimeType("BaseWorldUI"));
        worldUi.GetType().GetMethod("ShowApprenticeInfo").Invoke(worldUi, null);
        yield return new WaitForSecondsRealtime(0.35f);
        Capture("expansion-apprentice-panel.png");
    }

    [UnityTest]
    public IEnumerator MonasteryAndApprenticeFriendlyBehavior()
    {
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 25; i++) yield return null;
        var apprentice = Object.FindFirstObjectByType(RuntimeType("BattleApprentice"));
        Assert.NotNull(apprentice);
        Assert.Greater((float)Value(apprentice, "MaxHealth"), 0f);
        Assert.NotNull(GameObject.Find("ApprenticeHealth"));
        float before = (float)Value(apprentice, "CurrentHealth");
        apprentice.GetType().GetMethod("TakeDamage").Invoke(apprentice, new object[] { 3f });
        Assert.AreEqual(before - 3f, (float)Value(apprentice, "CurrentHealth"), 0.01f);
        var hero = Object.FindFirstObjectByType(RuntimeType("HeroController"));
        var mainBase = Object.FindFirstObjectByType(RuntimeType("MainBaseController"));
        Assert.NotNull(hero);
        Assert.NotNull(mainBase);
        var monastery = new GameObject("TestMonastery");
        monastery.transform.position = new Vector3(-1f, 0f, 0f);
        var healer = monastery.AddComponent(RuntimeType("MonasteryHealer"));
        hero.GetType().GetMethod("TakeDamage").Invoke(hero, new object[] { 10f });
        mainBase.GetType().GetMethod("TakeDamage").Invoke(mainBase, new object[] { 10f });
        float heroBefore = (float)Value(hero, "CurrentHealth");
        float baseBefore = (float)Value(mainBase, "CurrentHealth");
        yield return null;
        healer.GetType().GetField("nextHeal", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(healer, Time.time - 1f);
        yield return null;
        Assert.Greater((float)Value(hero, "CurrentHealth"), heroBefore);
        Assert.Greater((float)Value(mainBase, "CurrentHealth"), baseBefore);
        Assert.LessOrEqual((float)Value(hero, "CurrentHealth"), (float)Value(hero, "MaxHealth"));
        Assert.LessOrEqual((float)Value(mainBase, "CurrentHealth"), (float)Value(mainBase, "MaxHealth"));
        var manager = Object.FindFirstObjectByType(RuntimeType("BattleManager"));
        var monasteryType = Enum.Parse(RuntimeType("BuildingType"), "Monastery");
        Assert.AreEqual(60, manager.GetType().GetMethod("GetBuildCost")
            .Invoke(manager, new[] { monasteryType }));
        manager.GetType().GetMethod("CreateMonastery", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, new object[] { new Vector3(2f, -2f, 0f) });
        yield return null;
        Assert.NotNull(GameObject.Find("Monastery"));
        Capture("expansion-battle-friendly.png");
        Object.Destroy(monastery);
    }

    private static void Capture(string file)
    {
        var camera = Camera.main;
        Assert.NotNull(camera);
        var texture = new RenderTexture(390, 844, 24);
        camera.targetTexture = texture;
        camera.aspect = 390f / 844f;
        camera.orthographicSize = 22f / (2f * camera.aspect);
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }
        camera.Render();
        var old = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
        pixels.Apply();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, file), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = old;
        camera.targetTexture = null;
        texture.Release();
        Object.Destroy(texture);
    }
}
