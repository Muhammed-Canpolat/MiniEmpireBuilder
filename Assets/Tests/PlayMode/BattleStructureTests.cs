using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Savaş kuleleri: yerleşim, can/yıkılma/yeniden inşa bedeli, düşman rolleri.</summary>
public class BattleStructureTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static IEnumerator StartBattle(int region)
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
        Type("BattleSession").GetMethod("Begin").Invoke(null, new object[] { region, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 45; frame++) yield return null;
    }

    [UnityTest]
    public IEnumerator FourSlotsAreEquidistantCorners()
    {
        yield return StartBattle(3);
        var slots = Object.FindObjectsByType(Type("BattleBuildSlot"), FindObjectsSortMode.None)
            .Cast<Component>().Select(s => (Vector2)s.transform.position).ToArray();
        Assert.AreEqual(4, slots.Length);
        var baseCenter = (Vector2)(Object.FindFirstObjectByType(Type("MainBaseController")) as Component).transform.position;
        Assert.AreEqual(2, slots.Count(p => p.x < baseCenter.x));
        Assert.AreEqual(2, slots.Count(p => p.y > baseCenter.y));
        float upper = Vector2.Distance(slots.First(p => p.y > 0), baseCenter);
        foreach (var p in slots.Where(p => p.y > 0))
            Assert.AreEqual(upper, Vector2.Distance(p, baseCenter), 0.01f, "Üst köşeler eşit uzaklıkta");
        Assert.AreEqual(Mathf.Abs(slots.Where(p => p.x < 0).Max(p => p.x)),
            slots.Where(p => p.x > 0).Min(p => p.x), 0.01f, "Sol/sağ simetrik");
        Type("BattleSession").GetMethod("Clear").Invoke(null, null);
    }

    [UnityTest]
    public IEnumerator TowerCollapsesLeavesRuinAndRebuildCostsMore()
    {
        yield return StartBattle(1);
        var battle = Object.FindFirstObjectByType(Type("BattleManager")) as Component;
        var slot = Object.FindObjectsByType(Type("BattleBuildSlot"), FindObjectsSortMode.None).Cast<Component>().First();
        var archer = Enum.Parse(Type("BuildingType"), "ArcherTower");
        battle.GetType().GetMethod("BuildTower", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(battle, new object[] { slot.transform.position, archer, slot });
        slot.GetType().GetField("isBuilt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(slot, true);
        yield return null;

        var structure = Object.FindFirstObjectByType(Type("BattleStructure")) as Component;
        Assert.NotNull(structure, "Kule BattleStructure taşımalı");
        Assert.NotNull(structure.GetComponent(Type("BattleGarrison")), "Kulede asker olmalı");
        Assert.NotNull(structure.transform.Find("GarrisonUnit"));
        var costFor = slot.GetType().GetMethod("CostFor");
        Assert.AreEqual(50, costFor.Invoke(slot, new[] { archer }));

        // Düşman rolleri: Ork kuleye, kurt (kahraman yakınsa) kahramana yönelir.
        var structurePoint = (Vector2)structure.GetType().GetProperty("BasePoint").GetValue(structure);
        var spawn = battle.GetType().GetMethod("SpawnEnemyAt");
        var orc = spawn.Invoke(battle, new[] { Enum.Parse(Type("EnemyType"), "Orc"),
            (object)(Vector3)(structurePoint + new Vector2(3f, 0f)) }) as Component;
        var hero = Object.FindFirstObjectByType(Type("HeroController")) as Component;
        var wolf = spawn.Invoke(battle, new[] { Enum.Parse(Type("EnemyType"), "Wolf"),
            (object)(hero.transform.position + new Vector3(2f, 0f, 0f)) }) as Component;
        yield return null;
        yield return null;
        var currentTarget = Type("EnemyController").GetProperty("CurrentTarget");
        Assert.AreEqual(structure.transform, currentTarget.GetValue(orc), "Yıkıcı kuleyi hedefler");
        Assert.AreEqual(hero.transform, currentTarget.GetValue(wolf), "Avcı kahramana atılır");

        // Kışkırtma: kahraman vurunca Ork kahramana döner.
        orc.GetType().GetMethod("Provoke").Invoke(orc, null);
        orc.GetType().GetField("nextRetarget", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(orc, 0f);
        yield return null;
        Assert.AreEqual(hero.transform, currentTarget.GetValue(orc), "Kışkırtılan düşman kahramana döner");

        foreach (var enemy in new[] { orc, wolf }) Object.Destroy(enemy.gameObject);
        structure.GetType().GetMethod("TakeDamage").Invoke(structure, new object[] { 100000f });
        yield return new WaitForSeconds(0.4f);
        Assert.IsTrue(structure == null, "Yıkılan kule sahneden kalkar");
        Assert.IsFalse((bool)slot.GetType().GetProperty("IsBuilt").GetValue(slot));
        Assert.NotNull(GameObject.Find("StructureRuin"), "Yerinde yıkıntı kalır");
        Assert.AreEqual(75, costFor.Invoke(slot, new[] { archer }), "Yeniden inşa +%50");
        Type("BattleSession").GetMethod("Clear").Invoke(null, null);
    }
}
