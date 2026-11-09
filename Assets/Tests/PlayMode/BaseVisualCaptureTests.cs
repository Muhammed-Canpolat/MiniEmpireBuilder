using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class BaseVisualCaptureTests
{
    private static System.Type RuntimeType(string name) =>
        System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [UnityTest]
    public IEnumerator BattleCosmeticsAttachWithoutCombatChanges()
    {
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 20; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.NotNull(Object.FindFirstObjectByType(RuntimeType("BattleCosmeticObserver")));
        var hero = Object.FindFirstObjectByType(RuntimeType("HeroController")) as Component;
        Assert.NotNull(hero);
        var animator = hero.GetComponent<Animator>();
        Assert.NotNull(animator);
        Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
    }

    [UnityTest]
    public IEnumerator CaptureCurrentBaseGameView()
    {
        Screen.SetResolution(390, 844, false);
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 20; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);

        // Other PlayMode tests may leave a save with no default mine or base.
        var defaultMap = Object.FindFirstObjectByType(RuntimeType("BaseMapManager"));
        var defaultBuildingType = RuntimeType("BuildingType");
        var spawnDefault = defaultMap.GetType().GetMethod("SpawnBuilding");
        if (Object.FindFirstObjectByType(RuntimeType("BaseGarrisonVisual")) == null)
            spawnDefault.Invoke(defaultMap, new object[] {
                System.Enum.Parse(defaultBuildingType, "MainBase"), new Vector3(10.5f, 22f), "main" });
        if (Object.FindFirstObjectByType(RuntimeType("BaseMineVisual")) == null)
            spawnDefault.Invoke(defaultMap, new object[] {
                System.Enum.Parse(defaultBuildingType, "GoldMine"), new Vector3(18.5f, 21.5f), "mine_1" });

        Assert.NotNull(Object.FindFirstObjectByType(RuntimeType("BaseMineVisual")));
        Assert.NotNull(Object.FindFirstObjectByType(RuntimeType("BaseGarrisonVisual")));
        Assert.NotNull(Object.FindFirstObjectByType(RuntimeType("BaseAmbientMotion")));
        Assert.NotNull(Object.FindFirstObjectByType(RuntimeType("FirstBaseGuide")));

        var mine = Object.FindFirstObjectByType(RuntimeType("BaseMineVisual")) as Component;
        Assert.NotNull(mine.transform.Find("MineLayers/MineHouse"));
        Assert.GreaterOrEqual(mine.GetComponentsInChildren<SpriteRenderer>()
            .Count(sprite => sprite.gameObject.name.StartsWith("MineGoldStone_")), 2);
        Assert.NotNull(mine.transform.Find("MineLayers/MinePawn"));
        Assert.GreaterOrEqual(mine.GetComponentsInChildren<Animator>()
            .Count(animator => animator.gameObject.name == "MinePawn"), 1);

        string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../UIReviewScreenshots/CurrentReview"));
        Directory.CreateDirectory(folder);
        var camera = Camera.main;
        Assert.NotNull(camera);
        var texture = new RenderTexture(390, 844, 24);
        camera.targetTexture = texture;
        camera.aspect = 390f / 844f;
        camera.orthographicSize = 22f / (2f * camera.aspect);
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }
        object player = null;
        System.Reflection.FieldInfo guideDone = null;
        bool wasGuideDone = false;
        for (int item = 1; item <= 6; item++)
        {
            if (item == 2)
            {
                var map = Object.FindFirstObjectByType(RuntimeType("BaseMapManager"));
                var buildingType = RuntimeType("BuildingType");
                var spawn = map.GetType().GetMethod("SpawnBuilding");
                spawn.Invoke(map, new object[] {
                    System.Enum.Parse(buildingType, "ArcherTower"), Vector3.zero, "archer_1" });
                spawn.Invoke(map, new object[] {
                    System.Enum.Parse(buildingType, "CannonTower"), Vector3.zero, "cannon_1" });
                yield return new WaitForSecondsRealtime(0.4f);
            }
            if (item == 6)
            {
                var manager = Object.FindFirstObjectByType(RuntimeType("GameManager"));
                player = manager.GetType().GetProperty("PlayerData").GetValue(manager);
                guideDone = player.GetType().GetField("firstBaseGuideDone");
                wasGuideDone = (bool)guideDone.GetValue(player);
                guideDone.SetValue(player, false);
                var oldGuide = Object.FindFirstObjectByType(RuntimeType("FirstBaseGuide"));
                Object.DestroyImmediate(oldGuide);
                (Object.FindFirstObjectByType(RuntimeType("BaseSceneSetup")) as Component)
                    .gameObject.AddComponent(RuntimeType("FirstBaseGuide"));
            }
            for (int frame = 0; frame < 4; frame++) yield return null;
            if (item == 6)
            {
                var arrow = GameObject.Find("FirstBaseGuideArrow");
                Assert.NotNull(arrow, "First-play arrow did not appear");
                Assert.Greater(arrow.GetComponent<RectTransform>().rect.width, 100f);
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder, $"visual-item-{item:00}.png"),
                pixels.EncodeToPNG());
            Object.Destroy(pixels);
            RenderTexture.active = previous;
        }
        if (guideDone != null) guideDone.SetValue(player, wasGuideDone);
        camera.targetTexture = null;
        texture.Release();
        Object.Destroy(texture);
    }
}
