using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Bölge bossları (1/3/4/5), "Don" fırtınası çarpanları ve savaş kamerası boyu.</summary>
public class RegionBossTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Enemy(string name) => Enum.Parse(Type("EnemyType"), name);

    [Test]
    public void EachRegionHasItsOwnBoss()
    {
        string[] expected = { "CastleGuardian", "ForestWitch", "MineWarden", "FrostLord", "DarkKing", "DragonWarrior" };
        var catalog = Type("CampaignCatalog");
        for (int id = 1; id <= 6; id++)
        {
            var region = catalog.GetMethod("Get").Invoke(null, new object[] { id });
            Assert.AreEqual(Enemy(expected[id - 1]), region.GetType().GetField("bossType").GetValue(region), "bölge " + id);
            Assert.IsTrue((bool)Type("EnemyTypeInfo").GetMethod("IsBoss").Invoke(null, new[] { Enemy(expected[id - 1]) }));
        }
        // Renkler birbirinden ayrışır.
        var tints = new[] { "CastleGuardian", "MineWarden", "FrostLord", "DarkKing" }
            .Select(n => (Color)Type("RegionBoss").GetMethod("Tint").Invoke(null, new[] { Enemy(n) })).ToList();
        Assert.AreEqual(4, tints.Distinct().Count());
        var region4 = catalog.GetMethod("Get").Invoke(null, new object[] { 4 });
        Assert.AreEqual("Don", region4.GetType().GetField("specialMechanic").GetValue(region4));
    }

    [Test]
    public void WeatherIsNeutralWithoutStormAndCameraFitsSlots()
    {
        var weather = Type("BattleWeather");
        Assert.AreEqual(1f, (float)weather.GetProperty("TowerCooldownMultiplier").GetValue(null));
        Assert.AreEqual(1f, (float)weather.GetProperty("HeroSpeedMultiplier").GetValue(null));

        float Size(float aspect) => (float)Type("BattleSceneSetup").GetMethod("CameraSizeFor").Invoke(null, new object[] { aspect });
        Assert.AreEqual(8f, Size(0.75f), 0.001f, "tablet: eski boy");
        float phone = Size(390f / 844f);
        Assert.GreaterOrEqual(phone * (390f / 844f), 4.4f, "dar telefonda yuvalar (±4,4) sığar");
        Assert.LessOrEqual(phone, 9.6f);
    }

    [UnityTest]
    public IEnumerator ShieldWallReducesDamageAndHealIsCapped()
    {
        var go = new GameObject("TestEnemy", typeof(SpriteRenderer));
        var enemy = go.AddComponent(Type("EnemyController"));
        yield return null;
        var t = enemy.GetType();
        t.GetMethod("Initialize").Invoke(enemy, new object[] { Enemy("CastleGuardian"), 1, null });
        float max = (float)t.GetProperty("MaxHealth").GetValue(enemy);
        t.GetProperty("DamageTakenMultiplier").SetValue(enemy, 0.3f);
        t.GetMethod("TakeDamage").Invoke(enemy, new object[] { 100f });
        Assert.AreEqual(max - 30f, (float)t.GetProperty("CurrentHealth").GetValue(enemy), 0.01f);
        t.GetMethod("Heal").Invoke(enemy, new object[] { 1000f });
        Assert.AreEqual(max, (float)t.GetProperty("CurrentHealth").GetValue(enemy), 0.01f);
        UnityEngine.Object.Destroy(go);
    }
}
