using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Can sistemi (5 can, 30 dk'da bir) ve savaş kartları (+%10, Kraliyet %15, ilk kart bedava).</summary>
public class LivesAndCardsTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Static(string type, string member)
    {
        var t = Type(type);
        var property = t.GetProperty(member);
        return property != null ? property.GetValue(null) : t.GetField(member).GetValue(null);
    }

    private static object Call(string type, string method, params object[] args)
        => Type(type).GetMethod(method).Invoke(null, args);

    private object save;

    private IEnumerator Setup()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        Call("BattleCards", "ResetForTests");
    }

    private void SetField(string name, object value) => save.GetType().GetField(name).SetValue(save, value);
    private object GetField(string name) => save.GetType().GetField(name).GetValue(save);

    [UnityTest]
    public IEnumerator LivesDropOnDefeatAndRegenerate()
    {
        yield return Setup();
        Assert.AreEqual(5, Static("LivesSystem", "Lives"), "Yeni oyuncu 5 canla başlar");
        Call("LivesSystem", "LoseLife");
        Call("LivesSystem", "LoseLife");
        Assert.AreEqual(3, Static("LivesSystem", "Lives"));

        // 65 dakika geçti: 2 can gelir, kalan 5 dk'lık ilerleme korunur.
        SetField("livesLastRegenTicks", DateTime.UtcNow.AddMinutes(-65).Ticks);
        Assert.AreEqual(5, Static("LivesSystem", "Lives"));

        for (int i = 0; i < 5; i++) Call("LivesSystem", "LoseLife");
        Assert.AreEqual(0, Static("LivesSystem", "Lives"));
        Assert.IsFalse((bool)Static("LivesSystem", "CanPlay"), "Can yoksa sefer başlamaz");

        // Sınırsız can: oynanır ve can düşmez.
        Call("LivesSystem", "GrantUnlimited", TimeSpan.FromHours(2));
        Assert.IsTrue((bool)Static("LivesSystem", "CanPlay"));
        Call("LivesSystem", "LoseLife");
        Assert.AreEqual(0, GetField("lives"));
        SetField("unlimitedLivesUntilTicks", 0L);

        // Reklamla can: günde en fazla 3.
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue((bool)Static("LivesSystem", "CanWatchAdForLife"));
            Call("LivesSystem", "RewardAdLife");
        }
        Assert.AreEqual(3, Static("LivesSystem", "Lives"));
        Assert.IsFalse((bool)Static("LivesSystem", "CanWatchAdForLife"), "Günlük reklam hakkı bitti");
    }

    [UnityTest]
    public IEnumerator CardsApplyForOneBattle()
    {
        yield return Setup();
        var card = Type("BattleCard");
        object damage = Enum.Parse(card, "HeroDamage"), health = Enum.Parse(card, "HeroHealth"),
            fortify = Enum.Parse(card, "Fortify");
        float Multiplier(object c) => (float)Call("BattleCards", "Multiplier", c);

        // İlk savaş: bir kart bedava; ikinci kart reklam ister.
        Assert.IsTrue((bool)Static("BattleCards", "FreeCardAvailable"));
        Assert.IsTrue((bool)Call("BattleCards", "TryUnlockWithoutAd", damage));
        Assert.IsFalse((bool)Call("BattleCards", "TryUnlockWithoutAd", health), "İkinci kart bedava değil");
        Call("BattleCards", "UnlockFromAd", fortify);
        Call("BattleCards", "BeginBattle");
        Assert.AreEqual(1.10f, Multiplier(damage), 0.0001f);
        Assert.AreEqual(1.00f, Multiplier(health), 0.0001f);
        Assert.AreEqual(1.10f, Multiplier(fortify), 0.0001f);
        Assert.IsTrue((bool)GetField("freeCardUsed"), "Bedava hak savaş başlayınca kullanılmış sayılır");
        Call("BattleCards", "EndBattle");
        Assert.AreEqual(1.00f, Multiplier(damage), 0.0001f, "Kartlar tek savaşlık");
        Assert.IsFalse((bool)Static("BattleCards", "FreeCardAvailable"));

        // Kart jetonu reklamsız açar.
        SetField("cardTokens", 1);
        Assert.IsTrue((bool)Call("BattleCards", "TryUnlockWithoutAd", health));
        Assert.AreEqual(0, GetField("cardTokens"));

        // Kraliyet Üyeliği: üç kart da otomatik %15.
        SetField("royalUntilTicks", DateTime.UtcNow.AddDays(30).Ticks);
        Call("BattleCards", "BeginBattle");
        Assert.AreEqual(1.15f, Multiplier(damage), 0.0001f);
        Assert.AreEqual(1.15f, Multiplier(health), 0.0001f);
        Assert.AreEqual(1.15f, Multiplier(fortify), 0.0001f);
        Call("BattleCards", "EndBattle");

        // Rekabet modunda kart etkisiz.
        Type("BattleCards").GetProperty("Competitive").SetValue(null, true);
        Call("BattleCards", "BeginBattle");
        Assert.AreEqual(1.00f, Multiplier(damage), 0.0001f);
        Call("BattleCards", "ResetForTests");
    }
}
