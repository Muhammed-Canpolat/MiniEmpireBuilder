using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Yerel bildirim planı: gece sessizliği, inşaat/can/maden/günlük ödül bildirimleri.</summary>
public class NotificationPlannerTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static DateTime AvoidNight(DateTime time) =>
        (DateTime)Type("NotificationPlanner").GetMethod("AvoidNight").Invoke(null, new object[] { time });

    [Test]
    public void NightNotificationsMoveToMorning()
    {
        var day = new DateTime(2026, 11, 10);
        Assert.AreEqual(day.AddHours(15), AvoidNight(day.AddHours(15)), "gündüz değişmez");
        Assert.AreEqual(day.AddDays(1).AddHours(9), AvoidNight(day.AddHours(23)), "gece yarısından önce: ertesi sabah");
        Assert.AreEqual(day.AddHours(9), AvoidNight(day.AddHours(3)), "gece: aynı günün sabahı");
    }

    [UnityTest]
    public IEnumerator PlanCoversBuilderLivesAndDailyReward()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        save.GetType().GetField("lives").SetValue(save, 2);
        save.GetType().GetField("livesLastRegenTicks").SetValue(save, DateTime.UtcNow.Ticks);
        managerType.GetMethod("StartBuilderTask").Invoke(manager,
            new object[] { "remove", "Kaldırma", 3600f, Vector3.zero, "", "" });

        // Öğle vakti: hiçbir bildirim geceye düşmesin.
        var now = DateTime.Now.Date.AddHours(12);
        var plan = (IEnumerable)Type("NotificationPlanner").GetMethod("Plan").Invoke(null, new object[] { manager, now });
        var ids = plan.Cast<object>().Select(p => (int)p.GetType().GetField("id").GetValue(p)).ToList();
        Assert.Contains(200, ids, "can dolunca");
        Assert.Contains(400, ids, "günlük ödül");
        Assert.Contains(900, ids, "geri dönüş hatırlatması");
        foreach (var item in plan)
        {
            var when = (DateTime)item.GetType().GetField("fireLocal").GetValue(item);
            Assert.That(when.Hour, Is.InRange(9, 21), "gece bildirimi yok");
            Assert.Greater(when, now);
        }
    }
}
