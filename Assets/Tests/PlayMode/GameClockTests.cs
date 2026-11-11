using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>GameClock: saat geri alınınca günlük sınırlar yenilenmez; 7 günden fazla sapmada sıfırlanır.</summary>
public class GameClockTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    [UnityTest]
    public IEnumerator ClockDoesNotGoBackwardsButRecoversFromLongSkew()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        bool created = manager == null;
        if (created) manager = new GameObject("ClockTestGM").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        var field = save.GetType().GetField("clockHighWaterTicks");
        var clock = Type("GameClock");
        Func<DateTime> utc = () => (DateTime)clock.GetProperty("UtcNow").GetValue(null);

        // Oyun yarını görmüş, telefon bugüne geri alınmış: oyun yarında kalır.
        long tomorrow = DateTime.UtcNow.AddDays(1).Ticks;
        field.SetValue(save, tomorrow);
        Assert.AreEqual(tomorrow, utc().Ticks);

        // 10 gün ileri sapma: saat yanlış ayarlanmış sayılır, gerçek saate döner.
        field.SetValue(save, DateTime.UtcNow.AddDays(10).Ticks);
        Assert.Less(Math.Abs((utc() - DateTime.UtcNow).TotalMinutes), 1.0);
        Assert.Less((long)field.GetValue(save), DateTime.UtcNow.AddMinutes(1).Ticks);

        // Normal akış: saat ilerledikçe sınır da ilerler.
        Assert.GreaterOrEqual((long)field.GetValue(save), DateTime.UtcNow.AddMinutes(-1).Ticks);
        if (created) UnityEngine.Object.Destroy(manager.gameObject);
    }
}
