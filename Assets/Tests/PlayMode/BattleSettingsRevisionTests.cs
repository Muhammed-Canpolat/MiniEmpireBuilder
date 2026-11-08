using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BattleSettingsRevisionTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [UnityTest]
    public IEnumerator ForfeitUsesDefeatFlowAndKeepsGold()
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Spear") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, null);
        int originalGold = (int)save.GetType().GetField("gold").GetValue(save);
        Type("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 50; frame++) yield return null;
        yield return new WaitForSecondsRealtime(1.8f);

        Assert.IsNull(GameObject.Find("Minimap"));
        Assert.NotNull(Object.FindFirstObjectByType(Type("BattleOffscreenIndicators")));
        Capture("item-03-battle-ui.png");
        yield return new WaitForSecondsRealtime(3.2f);
        Capture("item-03-offscreen-arrows.png");
        var battle = Object.FindFirstObjectByType(Type("BattleManager")) as Component;
        Assert.IsTrue((bool)battle.GetType().GetProperty("IsBattleActive").GetValue(battle));
        var settings = Object.FindFirstObjectByType(Type("AudioSettingsUI")) as Component;
        Assert.NotNull(settings);
        settings.GetType().GetMethod("Open").Invoke(settings, null);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(GameObject.Find("BattleSettingsBlocker").activeSelf);
        Assert.IsTrue(GameObject.Find("AudioSettingsPanel").activeSelf);
        Capture("item-02-settings-modal.png");
        GameObject.Find("ForfeitButton").GetComponent<Button>().onClick.Invoke();
        Assert.IsTrue(GameObject.Find("ForfeitConfirm").activeSelf);
        Capture("item-02-forfeit-confirm.png");
        var confirm = GameObject.Find("ConfirmForfeit").GetComponent<Button>();
        confirm.onClick.Invoke();
        confirm.onClick.Invoke();
        yield return null;
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse((bool)battle.GetType().GetProperty("IsBattleActive").GetValue(battle));
        Assert.AreEqual(0, Object.FindObjectsByType(Type("EnemyController"), FindObjectsSortMode.None).Length);
        Assert.AreEqual(0, Object.FindObjectsByType(Type("ProjectileController"), FindObjectsSortMode.None).Length);
        Assert.IsTrue(GameObject.Find("VictoryOverlay").activeSelf);
        Assert.IsTrue(GameObject.Find("RetryCampaignBattle").activeSelf);
        Assert.AreEqual(originalGold, (int)save.GetType().GetField("gold").GetValue(save));
        Assert.IsFalse((bool)Type("AudioSettingsUI").GetProperty("BattleModalOpen")?.GetValue(null));
        yield return new WaitForSecondsRealtime(2.2f);
        Capture("item-02-defeat.png");

        GameObject.Find("RetryCampaignBattle").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(1.2f);
        for (int frame = 0; frame < 45; frame++) yield return null;
        Assert.AreEqual("BattleScene", SceneManager.GetActiveScene().name);
        battle = Object.FindFirstObjectByType(Type("BattleManager")) as Component;
        Assert.NotNull(battle);
        yield return new WaitForSecondsRealtime(1.7f);
        Assert.IsTrue((bool)battle.GetType().GetProperty("IsBattleActive").GetValue(battle));
        settings = Object.FindFirstObjectByType(Type("AudioSettingsUI")) as Component;
        settings.GetType().GetMethod("Open").Invoke(settings, null);
        GameObject.Find("ForfeitButton").GetComponent<Button>().onClick.Invoke();
        GameObject.Find("ConfirmForfeit").GetComponent<Button>().onClick.Invoke();
        yield return null;
        GameObject.Find("VictoryReturnButton").GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.AreEqual("CampaignScene", SceneManager.GetActiveScene().name);
        Type("BattleSession").GetMethod("Clear").Invoke(null, null);
    }

    private static void Capture(string name)
    {
        typeof(HudExpansionReviewTests).GetMethod("Capture",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { name });
    }
}
