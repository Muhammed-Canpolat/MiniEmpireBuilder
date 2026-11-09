using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Elmas, 1–5 inşaatçı, kiralama bedelleri ve aynı anda süren görevler.</summary>
public class BuilderSystemTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Building(string name) => Enum.Parse(Type("BuildingType"), name);

    private static (int gold, int gems) Cost(int builderNumber)
    {
        var result = Type("Progression").GetMethod("BuilderCost").Invoke(null, new object[] { builderNumber });
        var t = result.GetType();
        return ((int)t.GetField("Item1").GetValue(result), (int)t.GetField("Item2").GetValue(result));
    }

    [Test]
    public void BuilderCostsMatchDesign()
    {
        int mainBase5To6 = (int)Type("Progression").GetMethod("UpgradeCost")
            .Invoke(null, new[] { Building("MainBase"), (object)6 });
        Assert.AreEqual((mainBase5To6, 0), Cost(2), "2. inşaatçı = Ana Üs 5→6 bedeli kadar altın");
        Assert.AreEqual((0, 100), Cost(3));
        Assert.AreEqual((0, 200), Cost(4));
        Assert.AreEqual((0, 300), Cost(5));
        Assert.AreEqual((0, 0), Cost(6), "En fazla 5 inşaatçı");
        Assert.AreEqual(5, Type("Progression").GetField("MaxBuilders").GetValue(null));
        Assert.AreEqual(5, Type("Progression").GetField("SecondBuilderMainBaseLevel").GetValue(null));
    }

    [UnityEngine.TestTools.UnityTest]
    public IEnumerator HiringAndParallelUpgrades()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;

        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Sword") });
        Assert.AreEqual(5, save.GetType().GetField("saveVersion").GetValue(save));
        Assert.AreEqual(1, save.GetType().GetField("builderCount").GetValue(save));
        Assert.AreEqual(0, save.GetType().GetField("gems").GetValue(save));

        foreach (var entry in (IEnumerable)save.GetType().GetField("buildings").GetValue(save))
        {
            string type = entry.GetType().GetField("buildingType").GetValue(entry).ToString();
            if (type == "MainBase") entry.GetType().GetField("level").SetValue(entry, 4);
            if (type == "ArcherTower")
            {
                entry.GetType().GetField("level").SetValue(entry, 1);
                entry.GetType().GetField("isUnlocked").SetValue(entry, true);
            }
        }
        var placedList = (IList)save.GetType().GetField("placedBuildings").GetValue(save);
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
        var main = Placed("main", "MainBase", 4);
        Placed("archer_1", "ArcherTower", 1);
        Placed("archer_4", "ArcherTower", 1);
        save.GetType().GetField("gold").SetValue(save, 1200);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        void Rebuild() => managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(manager, null);
        Rebuild();

        object Prop(string name) => managerType.GetProperty(name).GetValue(manager);
        object Hire() => managerType.GetMethod("HireBuilder").Invoke(manager, null);
        string Block() => managerType.GetMethod("CanHireBuilder").Invoke(manager, null).ToString();

        Assert.AreEqual("MainBaseLevel", Block(), "Ana Üs 4'te 2. inşaatçı kilitli");
        Assert.IsFalse((bool)Hire());

        record.GetField("level").SetValue(main, 5);
        Rebuild();
        Assert.AreEqual("None", Block());
        int goldBefore = (int)Prop("Gold");
        Assert.IsTrue((bool)Hire());
        Assert.AreEqual(2, Prop("BuilderCount"));
        Assert.AreEqual(goldBefore - Cost(2).gold, (int)Prop("Gold"));

        Assert.AreEqual("NoGems", Block(), "3. inşaatçı elmasla");
        managerType.GetMethod("AddGems").Invoke(manager, new object[] { 600, false });
        Assert.AreEqual(600, Prop("Gems"));
        Assert.IsTrue((bool)Hire());
        Assert.IsTrue((bool)Hire());
        Assert.IsTrue((bool)Hire());
        Assert.AreEqual(5, Prop("BuilderCount"));
        Assert.AreEqual(0, Prop("Gems"), "100 + 200 + 300 elmas harcanır");
        Assert.AreEqual("MaxReached", Block());
        Assert.IsFalse((bool)Hire());

        // Beş inşaatçı: iki farklı bina aynı anda yükselir, aynı bina iki kez yükseltilemez.
        save.GetType().GetField("gold").SetValue(save, 1200);
        var archer = Building("ArcherTower");
        var start = managerType.GetMethod("StartUpgrade");
        Assert.IsTrue((bool)start.Invoke(manager, new[] { "archer_1", archer }));
        Assert.IsTrue((bool)start.Invoke(manager, new[] { "archer_4", archer }));
        Assert.AreEqual(2, Prop("BusyBuilderCount"));
        Assert.AreEqual(3, Prop("FreeBuilderCount"));
        Assert.IsFalse((bool)Prop("IsBuilderBusy"), "Boşta inşaatçı varken meşgul sayılmaz");
        Assert.AreEqual("Upgrading", managerType.GetMethod("CanUpgrade")
            .Invoke(manager, new[] { "archer_1", archer }).ToString());

        var tasks = (IEnumerable)Prop("BuilderTasks");
        var indices = tasks.Cast<object>().Select(t => (int)t.GetType().GetField("builderIndex").GetValue(t)).ToList();
        CollectionAssert.AreEquivalent(new[] { 0, 1 }, indices, "Görevler farklı inşaatçılara dağılır");

        foreach (var task in tasks.Cast<object>().ToList())
            task.GetType().GetField("endTicks").SetValue(task, DateTime.UtcNow.Ticks - 1);
        yield return null;
        yield return null;
        Assert.AreEqual(0, Prop("BusyBuilderCount"));
        object Level(string slot) => managerType.GetMethod("GetInstanceLevel").Invoke(manager, new[] { slot, archer });
        Assert.AreEqual(2, Level("archer_1"));
        Assert.AreEqual(2, Level("archer_4"));
    }
}
