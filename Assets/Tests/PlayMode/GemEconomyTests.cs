using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Elmas: hemen bitir, İnşaat İksiri, misafir inşaatçı, eksik altın, kazanç yolları, can ve seri.</summary>
public class GemEconomyTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Call(string type, string method, params object[] args)
        => Type(type).GetMethod(method).Invoke(null, args);

    private static object Static(string type, string member)
    {
        var t = Type(type);
        var property = t.GetProperty(member);
        return property != null ? property.GetValue(null) : t.GetField(member).GetValue(null);
    }

    private Component manager;
    private Type managerType;
    private object save;

    private IEnumerator Setup()
    {
        managerType = Type("GameManager");
        manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        SetField("gems", 1000);
    }

    private void SetField(string name, object value) => save.GetType().GetField(name).SetValue(save, value);
    private object GetField(string name) => save.GetType().GetField(name).GetValue(save);
    private object Prop(string name) => managerType.GetProperty(name).GetValue(manager);
    private object Invoke(string method, params object[] args) => managerType.GetMethod(method).Invoke(manager, args);
    private int Gems => (int)Prop("Gems");

    private object StartTask(float seconds) =>
        (bool)Invoke("StartBuilderTask", "remove", "Kaldırma", seconds, Vector3.zero, "", "")
            ? Prop("LastStartedTask") : null;

    private static double Remaining(object task) =>
        (double)Type("GameManager").GetMethod("SecondsRemaining").Invoke(null, new[] { task });

    [Test]
    public void PricesMatchDesign()
    {
        int Finish(double seconds) => (int)Call("GemEconomy", "FinishCost", seconds);
        Assert.AreEqual(0, Finish(30), "1 dk altı bedava");
        Assert.AreEqual(12, Finish(30 * 60));
        Assert.AreEqual(17, Finish(3600));
        Assert.AreEqual(42, Finish(6 * 3600));
        Assert.AreEqual(83, Finish(24 * 3600));
        Assert.AreEqual(118, Finish(48 * 3600));
        Assert.AreEqual(2, Call("GemEconomy", "GoldTopUpCost", 41));
        Assert.AreEqual(0, Call("GemEconomy", "GoldTopUpCost", 0));

        long hour = TimeSpan.FromHours(1).Ticks;
        Assert.AreEqual(hour / 4, Call("GemEconomy", "Boosted", hour, 2 * hour), "pencere içinde biten iş 4x");
        Assert.AreEqual(4 * hour, Call("GemEconomy", "Boosted", 10 * hour, 2 * hour), "2 saatte 6 saat kazanılır");
    }

    [UnityTest]
    public IEnumerator FinishNowPotionAndGuestBuilder()
    {
        yield return Setup();

        // Hemen bitir: kalan süreye göre elmas, iş anında tamamlanır.
        var task = StartTask(3600);
        Assert.NotNull(task);
        int cost = (int)Type("GameManager").GetMethod("FinishNowCost").Invoke(null, new[] { task });
        Assert.That(cost, Is.InRange(16, 17));
        Assert.IsTrue((bool)Invoke("FinishTaskNow", task));
        Assert.AreEqual(1000 - cost, Gems);
        Assert.AreEqual(0, Prop("BusyBuilderCount"));

        // İksir: süren 1 saatlik iş 15 dakikaya iner; yeni işler de hızlı; sürerken yeniden alınamaz.
        var running = StartTask(3600);
        int before = Gems;
        Assert.IsTrue((bool)Invoke("ActivateBuildPotion"));
        Assert.AreEqual(before - 40, Gems);
        Assert.That(Remaining(running), Is.InRange(890d, 905d));
        Assert.IsFalse((bool)Invoke("ActivateBuildPotion"), "iksir sürerken ikinci kez alınmaz");
        Assert.IsTrue((bool)Invoke("FinishTaskNow", running));
        var fast = StartTask(1800);
        Assert.That(Remaining(fast), Is.InRange(440d, 455d), "iksir sürerken başlayan iş de 4x");
        Assert.IsTrue((bool)Invoke("FinishTaskNow", fast));

        // Misafir inşaatçı: tek inşaatçı meşgulken bir iş daha başlatılabilir.
        SetField("buildPotionUntilTicks", 0L);
        StartTask(3600);
        Assert.AreEqual(0, Prop("FreeBuilderCount"));
        Assert.IsTrue((bool)Invoke("RentTempBuilder"));
        Assert.AreEqual(2, Prop("WorkingBuilderCount"));
        Assert.AreEqual(1, Prop("BuilderCount"), "kiralama bedeli kalıcı sayıya bakar");
        var guestTask = StartTask(3600);
        Assert.NotNull(guestTask);
        Assert.AreEqual(1, guestTask.GetType().GetField("builderIndex").GetValue(guestTask));
        // Süre bitse de elindeki işi bitirene kadar çalışır, yeni iş almaz.
        SetField("tempBuilderUntilTicks", DateTime.UtcNow.Ticks - 1);
        Assert.AreEqual(2, Prop("WorkingBuilderCount"));
        Assert.AreEqual(0, Prop("FreeBuilderCount"));
        Assert.IsTrue((bool)Invoke("FinishTaskNow", guestTask));
        Assert.AreEqual(1, Prop("WorkingBuilderCount"));
    }

    [UnityTest]
    public IEnumerator GoldTopUpLivesStreakAndRewards()
    {
        yield return Setup();

        // Eksik altın: 81 altın eksik → 3 elmas.
        SetField("gold", 100);
        Assert.IsTrue((bool)Invoke("TopUpGold", 181));
        Assert.AreEqual(181, Prop("Gold"));
        Assert.AreEqual(997, Gems);

        // Canları doldur.
        SetField("lives", 2);
        SetField("livesLastRegenTicks", DateTime.UtcNow.Ticks);
        Assert.IsTrue((bool)Call("LivesSystem", "RefillWithGems"));
        Assert.AreEqual(5, Static("LivesSystem", "Lives"));
        Assert.AreEqual(982, Gems);
        Assert.IsFalse((bool)Call("LivesSystem", "RefillWithGems"), "dolu can yeniden doldurulmaz");

        // Seri kurtarma: tek gün kaçtıysa.
        var daily = Type("DailyRewards");
        var today = new DateTime(2026, 11, 10);
        daily.GetProperty("TodayOverride").SetValue(null, (DateTime?)today);
        try
        {
            int Stamp(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;
            SetField("dailyStreak", 3);
            SetField("dailyLastClaimDay", Stamp(today.AddDays(-2)));
            Assert.IsTrue((bool)Static("DailyRewards", "CanSaveStreak"));
            Assert.IsTrue((bool)Call("DailyRewards", "SaveStreak"));
            Assert.AreEqual(4, Static("DailyRewards", "TodayDay"));
            Assert.AreEqual(972, Gems);
            SetField("dailyLastClaimDay", Stamp(today.AddDays(-3)));
            Assert.IsFalse((bool)Static("DailyRewards", "CanSaveStreak"), "iki gün kaçtıysa kurtarılamaz");
        }
        finally
        {
            daily.GetProperty("TodayOverride").SetValue(null, null);
        }

        // Kazanç: yeni yıldızlar + ilk boss zaferi; tekrar aynı yıldız elmas vermez.
        Assert.AreEqual(13, Call("GemRewards", "ForVictory", 1, 5, 3));
        Call("CampaignProgress", "RecordVictory", 1, 5, 3);
        Assert.AreEqual(0, Call("GemRewards", "ForVictory", 1, 5, 3));
        Assert.AreEqual(2, Call("GemRewards", "ForVictory", 1, 1, 2));
        int gems = Gems;
        Call("GemRewards", "ForMainBaseLevel", 3);
        Call("GemRewards", "ForMainBaseLevel", 3);
        Assert.AreEqual(gems + 5, Gems, "her Ana Üs seviyesi bir kez");

        // Reklamla elmas: günde 3.
        for (int i = 0; i < 3; i++) Assert.IsTrue((bool)Call("GemRewards", "RewardAdGems"));
        Assert.IsFalse((bool)Call("GemRewards", "RewardAdGems"));
        Assert.AreEqual(gems + 5 + 6, Gems);
    }
}
