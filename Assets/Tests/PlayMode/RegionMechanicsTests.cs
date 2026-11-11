using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Bölge 4–6 kimliği (round2 E): donan kule, Lanetli Türbe, lav zemini.</summary>
public class RegionMechanicsTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static IEnumerator StartBattle(int region, int level)
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
        Type("BattleSession").GetMethod("Begin").Invoke(null, new object[] { region, level });
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 45; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.4f);
    }

    private static Component Hero() => Object.FindFirstObjectByType(Type("HeroController")) as Component;

    private static float Health(Component c) => (float)c.GetType().GetProperty("CurrentHealth").GetValue(c);

    private static object Enum_(string type, string value) => Enum.Parse(Type(type), value);

    private static Component SpawnVariant(string type, string variant, Vector3 position)
    {
        var battle = Object.FindFirstObjectByType(Type("BattleManager")) as Component;
        return battle.GetType().GetMethod("SpawnVariantAt")
            .Invoke(battle, new[] { Enum_("EnemyType", type), (object)position, Enum_("EnemyVariant", variant) }) as Component;
    }

    [UnityTest]
    public IEnumerator ShrineIgnoresTowerDamageButHeroHurtsIt()
    {
        yield return StartBattle(5, 1);
        var shrineType = Type("CursedShrine");
        var shrine = shrineType.GetMethod("Create").Invoke(null, new object[] { new Vector2(-8f, 12f), 0f }) as Component;
        yield return null;
        var take = shrineType.GetMethod("TakeDamage");
        float full = Health(shrine);
        Assert.Greater(full, 0f);
        foreach (var source in new[] { "Archer", "Cannon", "Apprentice", "Env" })
            take.Invoke(shrine, new[] { 25f, Enum_("DamageSource", source) });
        Assert.AreEqual(full, Health(shrine), 0.001f, "Kule/çırak/çevre hasarı türbeyi etkilememeli.");
        take.Invoke(shrine, new[] { 25f, Enum_("DamageSource", "Hero") });
        Assert.AreEqual(full - 25f, Health(shrine), 0.001f);
    }

    [UnityTest]
    public IEnumerator FrozenTowerThawsWhenHeroStandsNear()
    {
        yield return StartBattle(4, 1);
        var hero = Hero();
        var towerObject = new GameObject("TestTower");
        towerObject.transform.position = new Vector3(4f, -6f, 0f);
        var structure = towerObject.AddComponent(Type("BattleStructure"));
        structure.GetType().GetMethod("Initialize").Invoke(structure,
            new object[] { Enum_("BuildingType", "ArcherTower"), 1, null, new Vector2(4f, -6f), 1f });
        var tower = towerObject.AddComponent(Type("TowerController"));
        yield return null;
        var freeze = tower.GetType().GetMethod("Freeze");
        var frozen = tower.GetType().GetProperty("IsFrozen");
        freeze.Invoke(tower, new object[] { 5f });
        Assert.IsTrue((bool)frozen.GetValue(tower));

        // Uzakta: çözülmez.
        hero.transform.position = new Vector3(-6f, 6f, 0f);
        yield return new WaitForSeconds(1.3f);
        Assert.IsTrue((bool)frozen.GetValue(tower), "Kahraman uzaktayken kule çözülmemeli.");

        hero.transform.position = new Vector3(4.5f, -6f, 0f);
        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue((bool)frozen.GetValue(tower), "1 sn dolmadan çözülmemeli.");
        yield return new WaitForSeconds(0.9f);
        Assert.IsFalse((bool)frozen.GetValue(tower), "Kahraman >1 sn yanında durunca kule çözülmeli.");
        Assert.GreaterOrEqual((int)Type("RegionMechanicsStats").GetField("Thaws").GetValue(null), 1);
    }

    [UnityTest]
    public IEnumerator LavaGroundBurnsGroundEnemyButNotFlying()
    {
        yield return StartBattle(6, 1);
        Vector3 spot = new Vector3(9f, 12f, 0f);
        var ground = SpawnVariant("Wolf", "None", spot);
        var flier = SpawnVariant("Wolf", "Flying", spot + new Vector3(0.3f, 0f, 0f));
        yield return null;
        float groundBefore = Health(ground), flierBefore = Health(flier);
        Type("LavaGround").GetMethod("Create").Invoke(null, new object[] { (Vector2)spot, 4f, 5f });
        yield return new WaitForSeconds(1.3f);
        Assert.Less(Health(ground), groundBefore, "Yanan zemin yer düşmanına hasar vermeli.");
        Assert.AreEqual(flierBefore, Health(flier), 0.001f, "Uçan düşman lavdan etkilenmemeli.");
        Assert.Greater((float)Type("RegionMechanicsStats").GetField("EruptionDamageToEnemies").GetValue(null), 0f);
    }
}
