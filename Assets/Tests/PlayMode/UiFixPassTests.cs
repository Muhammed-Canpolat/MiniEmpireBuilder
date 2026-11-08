using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Cihaz testi sonrası Paket A düzeltmeleri.</summary>
public class UiFixPassTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static IEnumerator FreshSave()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Spear") });
        save.GetType().GetField("firstBaseGuideDone").SetValue(save, true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(manager, null);
    }

    [UnityTest]
    public IEnumerator MovingMineThroughPlacementKeepsHiddenRootInvisible()
    {
        yield return FreshSave();
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 50; i++) yield return null;
        var map = Object.FindFirstObjectByType(Type("BaseMapManager")) as Component;
        var mine = Object.FindObjectsByType(Type("BuildingIdentifier"), FindObjectsSortMode.None)
            .OfType<Component>().First(b => b.GetType().GetField("buildingType")
                .GetValue(b).ToString() == "GoldMine");
        var root = mine.GetComponent<SpriteRenderer>();
        Assert.AreEqual(0f, root.color.a, 0.001f, "Maden kök renderer'ı başlangıçta görünmez olmalı.");
        var placed = map.GetType().GetMethod("GetMapObject").Invoke(map, new object[] { mine.gameObject });
        var placementType = Type("PlacementController");
        var placement = placementType.GetProperty("Instance").GetValue(null);
        Assert.NotNull(placement);

        placementType.GetMethod("BeginMove").Invoke(placement, new[] { placed });
        yield return null;
        Assert.AreEqual(0f, root.color.a, 0.001f, "Taşıma sırasında gizli kare görünür olmamalı.");
        var ghost = GameObject.Find("GhostPlacement").GetComponent<SpriteRenderer>();
        Assert.AreNotSame(root.sprite, ghost.sprite, "Hayalet, madenin asıl görselini kullanmalı.");
        Assert.Greater(ghost.bounds.size.x, 3f, "Hayalet madenin gerçek boyutunda olmalı.");

        placementType.GetMethod("CancelPlacement").Invoke(placement, null);
        yield return null;
        Assert.AreEqual(0f, root.color.a, 0.001f, "Taşıma bitince siyah kare çıkmamalı.");
    }

    [UnityTest]
    public IEnumerator BattleSlotsClearBaseAndInfoPillReplacesLockedMenu()
    {
        yield return FreshSave();
        Type("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 45; i++) yield return null;

        var slots = Object.FindObjectsByType(Type("BattleBuildSlot"), FindObjectsSortMode.None)
            .Cast<Component>().ToArray();
        Assert.AreEqual(3, slots.Length);
        var hero = Object.FindFirstObjectByType(Type("HeroController")) as Component;
        foreach (var slot in slots)
        {
            Vector3 p = slot.transform.position;
            bool clearOfBase = Mathf.Abs(p.x) >= 2.85f || p.y <= -1.95f || p.y >= 2.55f;
            Assert.IsTrue(clearOfBase, $"Yuva {p} Ana Üs görseliyle çakışıyor.");
            Assert.Greater(Vector2.Distance(hero.transform.position, p), 1.35f,
                "Kahraman bir yuvanın içinde başlamamalı.");
        }

        var manager = Object.FindFirstObjectByType(Type("BattleManager"));
        manager.GetType().GetField("battleActive", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(manager, true);
        hero.transform.position = slots[0].transform.position;
        var body = hero.GetComponent<Rigidbody2D>();
        if (body != null) body.position = slots[0].transform.position;
        slots[0].GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(slots[0], null);
        yield return null;

        // Yeni oyuncunun Ana Üs'te kulesi yok: menü yerine dokunmayı engellemeyen balon.
        var pill = GameObject.Find("SlotInfoPill");
        Assert.NotNull(pill);
        Assert.IsNull(GameObject.Find("SlotChoicePanel"), "Kilitli seçenek menüsü açılmamalı.");
        Assert.IsFalse(pill.GetComponentsInChildren<UnityEngine.UI.Graphic>().Any(g => g.raycastTarget));
        Capture("uifix-slot-info-pill.png");

        // TMPro test assembly'sine bağlı değil; metinler yansıma ile okunur.
        var tmpType = Type("TMPro.TextMeshProUGUI");
        foreach (Component label in Object.FindObjectsByType(tmpType, FindObjectsSortMode.None))
            StringAssert.DoesNotContain("BINASI", (string)tmpType.GetProperty("text").GetValue(label),
                "Türkçe büyük İ kaybolmamalı.");
        Type("BattleSession").GetMethod("Clear").Invoke(null, null);
    }

    private static void Capture(string name)
    {
        typeof(HudExpansionReviewTests).GetMethod("Capture",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { name });
    }
}
