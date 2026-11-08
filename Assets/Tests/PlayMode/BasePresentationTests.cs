using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BasePresentationTests
{
    private static Type RuntimeType(string name) =>
        System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
    private static object StaticField(Type type, string name) =>
        type.GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    private static object Field(object value, string name) =>
        value.GetType().GetField(name).GetValue(value);
    private static void Call(object value, string name, params object[] args) =>
        value.GetType().GetMethod(name).Invoke(value, args);

    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(390, 844), new Vector2Int(360, 800),
        new Vector2Int(412, 915), new Vector2Int(768, 1024)
    };

    [UnityTest]
    public IEnumerator PanelsCloseCompletelyAndButtonsWorkAtMobileResolutions()
    {
        var original = new Vector2Int(Screen.width, Screen.height);
        var eventSystem = new GameObject("TestEventSystem", typeof(EventSystem));
        var canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var panelType = RuntimeType("PanelController");
        var panels = new List<Component>();
        foreach (string name in new[] { "BuildMenuPanel", "BuildingInfo", "LevelDetail", "ToastPill" })
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = name == "ToastPill" ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(300, 180);
            rt.anchoredPosition = name == "ToastPill" ? new Vector2(0, -110) : new Vector2(0, 100);
            var controller = go.AddComponent(panelType);
            Call(controller, "Initialize", name != "ToastPill");
            panels.Add(controller);
        }
        try
        {
            foreach (var resolution in Resolutions)
            {
                Screen.SetResolution(resolution.x, resolution.y, false);
                yield return null;
                foreach (var panel in panels)
                for (int cycle = 0; cycle < 10; cycle++)
                {
                    Call(panel, "Show");
                    Assert.IsTrue(panel.gameObject.activeSelf);
                    Assert.IsTrue(panel.GetComponent<CanvasGroup>().blocksRaycasts);
                    int clicks = 0;
                    var buttonGo = new GameObject("Probe", typeof(RectTransform), typeof(Image), typeof(Button));
                    buttonGo.transform.SetParent(panel.transform, false);
                    buttonGo.GetComponent<Button>().onClick.AddListener(() => clicks++);
                    ExecuteEvents.Execute(buttonGo, new PointerEventData(EventSystem.current),
                        ExecuteEvents.pointerClickHandler);
                    Assert.AreEqual(1, clicks, panel.name + " open click");
                    Object.Destroy(buttonGo);
                    yield return new WaitForSecondsRealtime(0.3f);
                    Call(panel, "Hide");
                    Assert.IsFalse(panel.GetComponent<CanvasGroup>().blocksRaycasts);
                    yield return new WaitForSecondsRealtime(0.3f);
                    Assert.IsFalse(panel.gameObject.activeSelf, panel.name);
                    Assert.AreEqual(0f, panel.GetComponent<CanvasGroup>().alpha);
                    var corners = new Vector3[4];
                    ((RectTransform)panel.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                        Assert.LessOrEqual(RectTransformUtility.WorldToScreenPoint(null, corner).y,
                            0f, panel.name + " left pixels below screen");
                }
            }
        }
        finally
        {
            Screen.SetResolution(original.x, original.y, false);
            Object.Destroy(canvasGo);
            Object.Destroy(eventSystem);
        }
    }

    [Test]
    public void BuildingFootprintsStayInsideInteriorAndNeverOverlap()
    {
        var occupied = new HashSet<Vector2Int>();
        var layout = RuntimeType("BaseWorldLayout");
        var bounds = (RectInt)StaticField(layout, "Buildable");
        foreach (var slot in (Array)StaticField(layout, "Slots"))
        {
            var footprint = (Vector2Int)Field(slot, "footprint");
            var topLeft = (Vector2Int)Field(slot, "topLeft");
            var id = (string)Field(slot, "id");
            for (int y = 0; y < footprint.y; y++)
            for (int x = 0; x < footprint.x; x++)
            {
                var cell = new Vector2Int(bounds.xMin + topLeft.x + x,
                    bounds.yMax - 1 - topLeft.y - y);
                Assert.IsTrue(bounds.Contains(cell), id + " outside interior " + cell);
                Assert.IsTrue(occupied.Add(cell), id + " overlaps at " + cell);
            }
        }
    }

    [UnityTest]
    public IEnumerator EconomyNetEqualsEnemyGoldPlusRewardMinusBuildCost()
    {
        var go = new GameObject("EconomyTestManager");
        go.SetActive(false);
        var managerType = RuntimeType("GameManager");
        var manager = go.AddComponent(managerType);
        var playerType = RuntimeType("PlayerSaveData");
        var weaponType = RuntimeType("WeaponType");
        var player = playerType.GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(weaponType, "Axe") });
        playerType.GetField("gold").SetValue(player, 100);
        managerType.GetProperty("PlayerData", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(manager, player);
        var buildingsProperty = managerType.GetProperty("Buildings", BindingFlags.Instance | BindingFlags.Public);
        buildingsProperty.SetValue(manager, Activator.CreateInstance(buildingsProperty.PropertyType));
        managerType.GetMethod("RebuildBuildingData", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
        const int enemyGold = 20, reward = 35, buildCost = 45;
        int before = (int)managerType.GetProperty("Gold").GetValue(manager);
        Call(manager, "AddGold", enemyGold);
        Call(manager, "AddGold", reward);
        Assert.IsTrue((bool)managerType.GetMethod("SpendGold").Invoke(manager, new object[] { buildCost }));
        Assert.AreEqual(enemyGold + reward - buildCost,
            (int)managerType.GetProperty("Gold").GetValue(manager) - before);
        Object.Destroy(go);
        yield return null;
    }
}
