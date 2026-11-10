using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>7 günlük giriş takvimi: sırayla ilerler, gün kaçınca başa döner, 7. günden sonra yeni döngü.</summary>
public class DailyRewardsTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Static(string member) => Type("DailyRewards").GetProperty(member).GetValue(null);
    private static void SetToday(DateTime day) => Type("DailyRewards").GetProperty("TodayOverride").SetValue(null, (DateTime?)day);
    private static object Claim() => Type("DailyRewards").GetMethod("Claim").Invoke(null, null);

    [UnityTest]
    public IEnumerator StreakAdvancesResetsAndGrantsRewards()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        object Field(string name) => save.GetType().GetField(name).GetValue(save);
        try
        {
            var day = new DateTime(2026, 11, 10);
            SetToday(day);
            Assert.AreEqual(1, Static("TodayDay"));
            int gold = (int)Field("gold");
            Assert.NotNull(Claim(), "1. gün: altın");
            Assert.Greater((int)Field("gold"), gold);
            Assert.IsNull(Claim(), "Aynı gün iki kez alınmaz");

            SetToday(day.AddDays(1));
            Assert.AreEqual(2, Static("TodayDay"));
            Claim();
            Assert.AreEqual(30, Field("pendingSpeedupMinutes"), "Yükseltme yoksa hızlandırma saklanır");

            // Bir gün kaçtı: takvim 1. güne döner.
            SetToday(day.AddDays(3));
            Assert.IsTrue((bool)Static("StreakBroken"));
            Assert.AreEqual(1, Static("TodayDay"));

            // 7 gün art arda: jeton, 2x maden, sınırsız can, elmas + sandık.
            for (int i = 0; i < 7; i++)
            {
                SetToday(day.AddDays(3 + i));
                Assert.AreEqual(i + 1, Static("TodayDay"));
                Claim();
            }
            Assert.GreaterOrEqual((int)Field("cardTokens"), 2, "4. gün jeton + 7. gün sandık jetonu");
            Assert.Greater((long)Field("mineBoostUntilTicks"), 0L);
            Assert.Greater((long)Field("unlimitedLivesUntilTicks"), 0L);
            Assert.AreEqual(20, Field("gems"));

            // 7. günden sonra yeni döngü.
            SetToday(day.AddDays(10));
            Assert.AreEqual(1, Static("TodayDay"));
            Assert.IsFalse((bool)Static("StreakBroken"));
        }
        finally
        {
            Type("DailyRewards").GetProperty("TodayOverride").SetValue(null, null);
        }
    }
}
