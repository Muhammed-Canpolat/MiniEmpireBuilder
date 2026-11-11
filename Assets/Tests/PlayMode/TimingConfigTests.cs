using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;

/// <summary>TimingConfig: devam eden yükseltmeler eski kayıtta yeni süreye oransal geçer.</summary>
public class TimingConfigTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [Test]
    public void RunningUpgradeIsScaledToNewTable()
    {
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        save.GetType().GetField("timingVersion").SetValue(save, 0);
        var placedType = Type("PlacedBuildingData");
        var placed = Activator.CreateInstance(placedType);
        placedType.GetField("slotId").SetValue(placed, "archer_test");
        placedType.GetField("buildingType").SetValue(placed, "ArcherTower");
        placedType.GetField("level").SetValue(placed, 5); // hedef 6: eski 3 sa, yeni 10 dk
        ((IList)save.GetType().GetField("placedBuildings").GetValue(save)).Add(placed);

        var taskType = Type("BuilderTaskSave");
        var task = Activator.CreateInstance(taskType);
        long now = DateTime.UtcNow.Ticks;
        taskType.GetField("kind").SetValue(task, "upgrade");
        taskType.GetField("slotId").SetValue(task, "archer_test");
        taskType.GetField("startTicks").SetValue(task, now - TimeSpan.FromSeconds(5400).Ticks);
        taskType.GetField("endTicks").SetValue(task, now + TimeSpan.FromSeconds(5400).Ticks);
        ((IList)save.GetType().GetField("builderTasks").GetValue(save)).Add(task);

        Type("SaveSystem").GetMethod("MigrateTimings").Invoke(null, new[] { save });

        double remaining = TimeSpan.FromTicks((long)taskType.GetField("endTicks").GetValue(task) - DateTime.UtcNow.Ticks).TotalSeconds;
        Assert.AreEqual(300, remaining, 5, "Yarısı geçmiş 3 saatlik iş, 10 dk'lık yeni tabloda 5 dk kalmalı");
        Assert.AreEqual(2, save.GetType().GetField("timingVersion").GetValue(save));

        // İkinci kez çalışmaz (sürüm güncel).
        Type("SaveSystem").GetMethod("MigrateTimings").Invoke(null, new[] { save });
        double again = TimeSpan.FromTicks((long)taskType.GetField("endTicks").GetValue(task) - DateTime.UtcNow.Ticks).TotalSeconds;
        Assert.AreEqual(remaining, again, 5);
    }
}
