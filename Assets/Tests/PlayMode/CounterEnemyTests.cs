using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Karşı düşmanlar (Zırhlı/Uçan/Bombacı) ve Boss Kalkanı (round2 C).</summary>
public class CounterEnemyTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Enum(string typeName, string value) => System.Enum.Parse(Type(typeName), value);

    private static IEnumerator StartBattle(int region, int level)
    {
        Screen.SetResolution(390, 844, false);
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum("WeaponType", "Spear") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, null);
        Type("BattleSession").GetMethod("Begin").Invoke(null, new object[] { region, level });
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 45; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.4f);
    }

    private static Component SpawnVariant(string type, string variant, Vector3 position)
    {
        var battle = Object.FindFirstObjectByType(Type("BattleManager")) as Component;
        return battle.GetType().GetMethod("SpawnVariantAt")
            .Invoke(battle, new[] { Enum("EnemyType", type), position, Enum("EnemyVariant", variant) }) as Component;
    }

    private static void Hit(Component enemy, float damage, string source)
    {
        enemy.GetType().GetProperty("DamageTakenMultiplier").SetValue(enemy, 1f); // boss yetenekleri çarpanı değiştirmesin
        enemy.GetType().GetMethod("TakeDamage").Invoke(enemy, new[] { damage, Enum("DamageSource", source) });
    }

    private static float Health(Component enemy) => (float)enemy.GetType().GetProperty("CurrentHealth").GetValue(enemy);
    private static T Prop<T>(Component enemy, string name) => (T)enemy.GetType().GetProperty(name).GetValue(enemy);

    private static (int, int, int) Mix(int region, int stage, int wave)
    {
        var tuple = Type("Progression").GetMethod("CounterMix")
            .Invoke(null, new object[] { region, stage, wave, System.Type.Missing });
        var t = tuple.GetType();
        return ((int)t.GetField("Item1").GetValue(tuple), (int)t.GetField("Item2").GetValue(tuple),
            (int)t.GetField("Item3").GetValue(tuple));
    }

    [Test]
    public void CounterMixNeverReturnsVariantsForFirstWave()
    {
        for (int region = 1; region <= 6; region++)
            for (int stage = 1; stage <= 5; stage++)
                Assert.AreEqual((0, 0, 0), Mix(region, stage, 0), $"R{region} S{stage} dalga 1 karşı düşman içermemeli");
        Assert.AreEqual((0, 0, 0), Mix(6, 1, 1), "1. aşama tek dalga: karşı düşman yok");
        Assert.AreEqual((0, 5, 4), Mix(6, 5, 1), "R6 S5 dalga 2: Uçan 5, Bombacı 4");
        Assert.AreEqual((0, 0, 2), Mix(2, 3, 1));
        Assert.AreEqual((3, 0, 0), Mix(3, 5, 2));
        var small = Type("Progression").GetMethod("CounterMix")
            .Invoke(null, new object[] { 6, 5, 1, 5 });
        int total = (int)small.GetType().GetField("Item1").GetValue(small) + (int)small.GetType().GetField("Item2").GetValue(small)
            + (int)small.GetType().GetField("Item3").GetValue(small);
        Assert.LessOrEqual(total, 3, "toplam en fazla count-2");
    }

    [UnityTest]
    public IEnumerator ArmoredResistsArchersButNotHero()
    {
        yield return StartBattle(1, 1);
        Vector3 far = (Object.FindFirstObjectByType(Type("HeroController")) as Component).transform.position + new Vector3(0f, 14f);
        var armored = SpawnVariant("Orc", "Armored", far);
        yield return null;
        Assert.AreEqual(Enum("EnemyVariant", "Armored"), armored.GetType().GetProperty("Variant").GetValue(armored));
        float start = Health(armored);
        Hit(armored, 100f, "Archer");
        Assert.AreEqual(start - 35f, Health(armored), 0.01f, "Okçu x0.35");
        start = Health(armored);
        Hit(armored, 20f, "Hero");
        Assert.AreEqual(start - 20f, Health(armored), 0.01f, "Kahraman x1.0");
        start = Health(armored);
        Hit(armored, 50f, "Cannon"); // kalan can 57: 100 hasar düşmanı öldürürdü
        Assert.AreEqual(start - 30f, Health(armored), 0.01f, "Topçu x0.6");
    }

    [UnityTest]
    public IEnumerator CannonCannotTargetFlyingEnemies()
    {
        yield return StartBattle(1, 1);
        Vector3 far = (Object.FindFirstObjectByType(Type("HeroController")) as Component).transform.position + new Vector3(0f, 14f);
        var flying = SpawnVariant("ShadowWolf", "Flying", far);
        var plain = SpawnVariant("Wolf", "None", far);
        yield return null;
        var canTarget = Type("TowerController").GetMethod("CanTarget");
        object cannon = Enum("BuildingType", "CannonTower"), archer = Enum("BuildingType", "ArcherTower");
        Assert.IsFalse((bool)canTarget.Invoke(null, new object[] { cannon, flying }), "Topçu uçanı vuramaz");
        Assert.IsTrue((bool)canTarget.Invoke(null, new object[] { archer, flying }), "Okçu uçanı vurur");
        Assert.IsTrue((bool)canTarget.Invoke(null, new object[] { cannon, plain }), "Topçu yer düşmanını vurur");
    }

    [UnityTest]
    public IEnumerator BossWardAbsorbsHeroDamageReducesTowersAndExposes()
    {
        yield return StartBattle(1, 1);
        Vector3 far = (Object.FindFirstObjectByType(Type("HeroController")) as Component).transform.position + new Vector3(0f, 14f);
        var boss = SpawnVariant("CastleGuardian", "None", far);
        yield return null;
        Assert.IsTrue(Prop<bool>(boss, "WardActive"), "Boss kalkanla doğar");
        float ward = Prop<float>(boss, "WardMax");
        float hp = Health(boss);
        Assert.AreEqual(hp * 0.15f, ward, 0.01f, "Bölge 1 kalkan havuzu %15");

        Hit(boss, 40f, "Archer"); // Bölge 1: kule x0.5
        Assert.AreEqual(hp - 20f, Health(boss), 0.01f);
        Assert.AreEqual(ward, Prop<float>(boss, "WardHealth"), 0.01f, "Kule kalkana işlemez");

        Hit(boss, 10f, "Hero");
        Assert.AreEqual(hp - 20f, Health(boss), 0.01f, "Kahraman önce kalkanı eritir");
        Assert.AreEqual(ward - 10f, Prop<float>(boss, "WardHealth"), 0.01f);

        Hit(boss, ward, "Hero"); // kalkanı kır (taşan hasar cana geçer)
        Assert.IsFalse(Prop<bool>(boss, "WardActive"));
        Assert.AreEqual(1, Prop<int>(boss, "WardBreaks"));
        Assert.IsTrue(Prop<bool>(boss, "IsExposed"));
        float exposedHp = Health(boss);
        Hit(boss, 20f, "Archer"); // Açık: x1.5, kalkan çarpanı yok
        Assert.AreEqual(exposedHp - 30f, Health(boss), 0.01f);
    }
}
