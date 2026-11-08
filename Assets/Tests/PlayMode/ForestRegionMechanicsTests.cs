using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Bölge 2 (Sisli Orman): sis görüşü, Gölge Kurt ve Orman Cadısı.</summary>
public class ForestRegionMechanicsTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Enemy(string name) => Enum.Parse(Type("EnemyType"), name);

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

    private static Component Spawn(string type, Vector3 position)
    {
        var battle = Object.FindFirstObjectByType(Type("BattleManager")) as Component;
        return battle.GetType().GetMethod("SpawnEnemyAt")
            .Invoke(battle, new[] { Enemy(type), (object)position }) as Component;
    }

    private static Component Hero() => Object.FindFirstObjectByType(Type("HeroController")) as Component;

    private static bool Visible(Vector3 position) =>
        (bool)Type("FogOfWar").GetMethod("IsVisible").Invoke(null, new object[] { position });

    private static float Health(Component c) => (float)c.GetType().GetProperty("CurrentHealth").GetValue(c);

    [UnityTest]
    public IEnumerator RegionDataUsesForestRosterAndKeepsRegionOneBoss()
    {
        var catalog = Type("CampaignCatalog");
        var forest = catalog.GetMethod("Get").Invoke(null, new object[] { 2 });
        var first = catalog.GetMethod("Get").Invoke(null, new object[] { 1 });
        var types = (Array)forest.GetType().GetField("enemyTypes").GetValue(forest);
        Assert.AreEqual(Enemy("ShadowWolf"), types.GetValue(1));
        Assert.AreEqual(Enemy("ForestWitch"), forest.GetType().GetField("bossType").GetValue(forest));
        Assert.AreEqual(Enemy("Troll"), first.GetType().GetField("bossType").GetValue(first));
        Assert.AreEqual("Sis", forest.GetType().GetField("specialMechanic").GetValue(forest));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FogHidesDistantEnemiesOnlyInForest()
    {
        yield return StartBattle(1, 1);
        Assert.IsNull(Type("FogOfWar").GetProperty("Active").GetValue(null));
        Assert.IsTrue(Visible(Hero().transform.position + new Vector3(12f, 0f)));

        yield return StartBattle(2, 2);
        Assert.NotNull(Type("FogOfWar").GetProperty("Active").GetValue(null));
        Vector3 hero = Hero().transform.position;
        Assert.IsTrue(Visible(hero + new Vector3(3f, 0f)));
        Assert.IsFalse(Visible(hero + new Vector3(0f, 9f)));

        var shadow = Spawn("ShadowWolf", hero + new Vector3(0f, 11f));
        var wolf = Spawn("Wolf", hero + new Vector3(0f, -11f));
        Assert.NotNull(shadow.GetComponent(Type("ShadowWolfBehaviour")));
        yield return new WaitForSeconds(0.5f);
        Assert.LessOrEqual(shadow.GetComponent<SpriteRenderer>().color.a, 0.05f);
        Assert.AreEqual(0.16f, wolf.GetComponent<SpriteRenderer>().color.a, 0.05f);

        var near = Spawn("Wolf", hero + new Vector3(2.5f, 0f));
        yield return new WaitForSeconds(0.4f);
        Assert.Greater(near.GetComponent<SpriteRenderer>().color.a, 0.9f);
        Capture("forest-fog-visibility.png");
    }

    [UnityTest]
    public IEnumerator ShadowWolfPouncesAtHero()
    {
        yield return StartBattle(2, 2);
        var hero = Hero();
        var shadow = Spawn("ShadowWolf", hero.transform.position + new Vector3(2.6f, 0f));
        var behaviour = shadow.GetComponent(Type("ShadowWolfBehaviour"));
        var pouncing = behaviour.GetType().GetProperty("IsPouncing");
        bool sawPounce = false;
        float until = Time.time + 4f;
        while (Time.time < until && !sawPounce)
        {
            sawPounce = (bool)pouncing.GetValue(behaviour);
            yield return null;
        }
        Assert.IsTrue(sawPounce, "Gölge Kurt 4 sn içinde atılmadı.");
    }

    [UnityTest]
    public IEnumerator ForestWitchPoisonSummonAndFogStep()
    {
        yield return StartBattle(2, 5);
        var hero = Hero();
        Vector3 heroPos = hero.transform.position;
        var witch = Spawn("ForestWitch", heroPos + new Vector3(4f, 2f));
        yield return null;
        yield return null;
        var boss = witch.GetComponent(Type("ForestWitchBoss"));
        Assert.NotNull(boss);
        Assert.NotNull(witch.GetComponent<Animator>(), "Cadı keşiş animatörünü kullanmalı.");
        var ability = Type("ForestWitchBoss+Ability");
        var use = boss.GetType().GetMethod("Use");

        float before = Health(hero);
        use.Invoke(boss, new[] { Enum.Parse(ability, "Poison"), (object)hero });
        yield return new WaitForSeconds(0.3f);
        Assert.NotNull(GameObject.Find("WitchPoisonPool"));
        yield return new WaitForSeconds(1.6f);
        Assert.Less(Health(hero), before, "Zehir alanı kahramana hasar vermedi.");
        Capture("forest-witch-poison.png");

        use.Invoke(boss, new[] { Enum.Parse(ability, "Summon"), (object)hero });
        yield return new WaitForSeconds(0.5f);
        var live = boss.GetType().GetProperty("LiveSummons");
        Assert.AreEqual(3, (int)live.GetValue(boss));
        use.Invoke(boss, new[] { Enum.Parse(ability, "Summon"), (object)hero });
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(5, (int)live.GetValue(boss), "Çağrı sınırı 5 olmalı.");
        var summoned = Object.FindObjectsByType(Type("EnemyController"), FindObjectsSortMode.None)
            .Cast<Component>().First(e => e != witch && e.name == "Enemy_Wolf");
        var reward = summoned.GetType().GetField("goldReward", BindingFlags.NonPublic | BindingFlags.Instance);
        Capture("forest-witch-summon.png");

        var enemy = witch.GetComponent(Type("EnemyController"));
        var invulnerable = enemy.GetType().GetProperty("Invulnerable");
        use.Invoke(boss, new[] { Enum.Parse(ability, "FogStep"), (object)hero });
        yield return null;
        Assert.IsTrue((bool)invulnerable.GetValue(enemy));
        yield return new WaitForSeconds(1.1f);
        Assert.IsFalse((bool)invulnerable.GetValue(enemy));
        float distance = Vector2.Distance(witch.transform.position, hero.transform.position);
        Assert.That(distance, Is.InRange(3.5f, 6f), "Cadı kahramandan ~5 birim uzakta belirmeli.");
        Assert.AreEqual(0, (int)reward.GetValue(summoned), "Çağrılan kurt altın düşürmemeli.");
    }

    private static void Capture(string name)
    {
        typeof(HudExpansionReviewTests).GetMethod("Capture",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { name });
    }
}
