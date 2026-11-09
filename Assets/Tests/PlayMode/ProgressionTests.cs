using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>15 seviyelik Ana Üs, bina başına seviye ve gerçek saatli yükseltme.</summary>
public class ProgressionTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Building(string name) => Enum.Parse(Type("BuildingType"), name);

    private static object Call(Type type, string method, params object[] args) =>
        type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

    [Test]
    public void HeroShieldAndBalanceMatchDesign()
    {
        // Kalkan: 1. seviye %10, seviye başına +%2, 15. seviye %38.
        var heroType = Type("HeroData");
        for (int level = 1; level <= 15; level++)
            Assert.AreEqual(0.10f + 0.02f * (level - 1), (float)Call(heroType, "ShieldForLevel", level), 0.0001f,
                "kalkan seviye " + level);
        Assert.AreEqual(0.38f, (float)Call(heroType, "ShieldForLevel", 99), 0.0001f, "tavan 15. seviye");

        // Tek kahraman (Warrior): taban can 200, kalkan veride.
        var hero = Activator.CreateInstance(heroType);
        heroType.GetField("weaponType").SetValue(hero, Enum.Parse(Type("WeaponType"), "Axe"));
        heroType.GetField("level").SetValue(hero, 1);
        heroType.GetMethod("ApplyLevelStats").Invoke(hero, null);
        Assert.AreEqual(200f, (float)heroType.GetField("maxHealth").GetValue(hero), 0.01f);
        Assert.AreEqual(0.10f, (float)heroType.GetField("shield").GetValue(hero), 0.0001f);

        // Düşman hasarı aşama başına %6, canı %8,5 artar.
        var statsType = Type("EnemyStats");
        var wolf = Call(statsType, "GetBaseStats", Enum.Parse(Type("EnemyType"), "Wolf"));
        float baseDamage = (float)statsType.GetField("damage").GetValue(wolf);
        float baseHealth = (float)statsType.GetField("health").GetValue(wolf);
        statsType.GetMethod("ScaleToLevel").Invoke(wolf, new object[] { 11 });
        Assert.AreEqual(baseDamage * 1.6f, (float)statsType.GetField("damage").GetValue(wolf), 0.01f);
        Assert.AreEqual(baseHealth * 1.85f, (float)statsType.GetField("health").GetValue(wolf), 0.01f);

        // Yeni kayıt her zaman Warrior ile başlar (eski silah seçimi yok sayılır).
        var save = Call(Type("PlayerSaveData"), "CreateNew", Enum.Parse(Type("WeaponType"), "Bow"));
        var savedHero = save.GetType().GetField("hero").GetValue(save);
        Assert.AreEqual("Axe", heroType.GetField("weaponType").GetValue(savedHero).ToString());
    }

    [Test]
    public void TablesMatchDesign()
    {
        var progression = Type("Progression");
        Assert.AreEqual(15, progression.GetField("MaxLevel").GetValue(null));
        Assert.AreEqual(15, Call(progression, "UpgradeSecondsTo", 2));
        Assert.AreEqual(60, Call(progression, "UpgradeSecondsTo", 3));
        Assert.AreEqual(30 * 60, Call(progression, "UpgradeSecondsTo", 4));
        Assert.AreEqual(3600, Call(progression, "UpgradeSecondsTo", 5));
        Assert.AreEqual(24 * 3600, Call(progression, "UpgradeSecondsTo", 10));

        foreach (var name in new[] { "MainBase", "ArcherTower", "CannonTower", "Monastery", "GoldMine" })
            for (int level = 3; level <= 15; level++)
                Assert.Greater((int)Call(progression, "UpgradeCost", Building(name), level),
                    (int)Call(progression, "UpgradeCost", Building(name), level - 1), name + " " + level);

        // Renk her 3 seviyede değişir.
        int[] expectedTier = { 0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4 };
        for (int level = 1; level <= 15; level++)
            Assert.AreEqual(expectedTier[level - 1], Call(progression, "ColorTier", level), "seviye " + level);

        var data = Type("BuildingData");
        int Count(string name, int mainBase) => (int)data.GetMethod("GetMaxBuildCount")
            .Invoke(null, new[] { Building(name), (object)mainBase });
        Assert.AreEqual(0, Count("ArcherTower", 1)); Assert.AreEqual(1, Count("ArcherTower", 2));
        Assert.AreEqual(1, Count("ArcherTower", 4)); Assert.AreEqual(2, Count("ArcherTower", 5));
        Assert.AreEqual(0, Count("CannonTower", 3)); Assert.AreEqual(1, Count("CannonTower", 4));
        Assert.AreEqual(2, Count("CannonTower", 8));
        Assert.AreEqual(0, Count("Monastery", 5)); Assert.AreEqual(1, Count("Monastery", 6));
        Assert.AreEqual(2, Count("Monastery", 10)); Assert.AreEqual(2, Count("Monastery", 15));
        Assert.AreEqual(1, Count("GoldMine", 15));

        var catalog = Type("CampaignCatalog");
        Assert.AreEqual(3, Call(catalog, "BattleSlotCount", 1));
        Assert.AreEqual(3, Call(catalog, "BattleSlotCount", 2));
        Assert.AreEqual(4, Call(catalog, "BattleSlotCount", 3));
        Assert.AreEqual(5, Call(catalog, "BattleSlotCount", 5));
        Assert.AreEqual(5, Call(catalog, "BattleSlotCount", 6), "Sonsuz Sefer 5'te kalır");
    }

    [UnityEngine.TestTools.UnityTest]
    public IEnumerator EachBuildingUpgradesSeparatelyThroughTimedBuilder()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Spear") });
        foreach (var entry in (System.Collections.IEnumerable)save.GetType().GetField("buildings").GetValue(save))
        {
            string type = entry.GetType().GetField("buildingType").GetValue(entry).ToString();
            if (type == "MainBase") entry.GetType().GetField("level").SetValue(entry, 6);
            if (type == "ArcherTower")
            {
                entry.GetType().GetField("level").SetValue(entry, 3);
                entry.GetType().GetField("isUnlocked").SetValue(entry, true);
            }
        }
        var placedList = (System.Collections.IList)save.GetType().GetField("placedBuildings").GetValue(save);
        var record = Type("PlacedBuildingData");
        object Placed(string slot, string type, int level)
        {
            var item = Activator.CreateInstance(record);
            record.GetField("slotId").SetValue(item, slot);
            record.GetField("buildingType").SetValue(item, type);
            record.GetField("level").SetValue(item, level);
            placedList.Add(item);
            return item;
        }
        Placed("main", "MainBase", 6);
        var strong = Placed("archer_1", "ArcherTower", 3);
        var fresh = Placed("archer_4", "ArcherTower", 1);
        save.GetType().GetField("gold").SetValue(save, 5000);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, null);

        var archer = Building("ArcherTower");
        object Level(string slot) => managerType.GetMethod("GetInstanceLevel").Invoke(manager, new[] { slot, archer });
        Assert.AreEqual(3, Level("archer_1"));
        Assert.AreEqual(1, Level("archer_4"), "İkinci bina birincinin seviyesiyle doğmamalı.");

        int goldBefore = (int)managerType.GetProperty("Gold").GetValue(manager);
        Assert.IsTrue((bool)managerType.GetMethod("StartUpgrade").Invoke(manager, new[] { "archer_4", archer }));
        Assert.IsTrue((bool)managerType.GetProperty("IsBuilderBusy").GetValue(manager));
        int cost = (int)Type("Progression").GetMethod("UpgradeCost").Invoke(null, new[] { archer, (object)2 });
        Assert.AreEqual(goldBefore - cost, (int)managerType.GetProperty("Gold").GetValue(manager));
        Assert.AreEqual(1, Level("archer_4"), "Seviye süre bitmeden artmamalı.");
        Assert.IsFalse((bool)managerType.GetMethod("StartUpgrade").Invoke(manager, new[] { "archer_1", archer }),
            "Tek inşaatçı meşgulken ikinci yükseltme başlamamalı.");
        double remaining = (double)managerType.GetProperty("BuilderSecondsRemaining").GetValue(manager);
        Assert.That(remaining, Is.InRange(13d, 15.5d), "1→2 yükseltmesi 15 sn sürmeli.");

        // Süreyi geçmişe al: gerçek saatle tamamlanma (oyun kapalıyken de aynı yol).
        var task = managerType.GetProperty("BuilderTask").GetValue(manager);
        task.GetType().GetField("endTicks").SetValue(task, DateTime.UtcNow.Ticks - 1);
        yield return null;
        yield return null;
        Assert.IsFalse((bool)managerType.GetProperty("IsBuilderBusy").GetValue(manager));
        Assert.AreEqual(2, Level("archer_4"));
        Assert.AreEqual(3, Level("archer_1"), "Diğer Okçu binası değişmemeli.");

        var tower = managerType.GetMethod("GetBattleTowerLevel");
        Assert.AreEqual(3, tower.Invoke(manager, new[] { archer, (object)0 }), "İlk savaş kulesi en güçlü bina");
        Assert.AreEqual(2, tower.Invoke(manager, new[] { archer, (object)1 }));

        Assert.AreEqual(6, managerType.GetMethod("GetHeroMaxLevel").Invoke(manager, null),
            "Kahraman tavanı Ana Üs seviyesi");
    }
}
