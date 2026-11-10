using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Sonsuz Sefer katları ve Ejder İstilası haftalık ödülü; savaş bağlamı (BattleSession).</summary>
public class SpecialModesTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Call(string type, string method, params object[] args) =>
        Type(type).GetMethod(method).Invoke(null, args);

    private static object Static(string type, string member) => Type(type).GetProperty(member).GetValue(null);

    [Test]
    public void EndlessFloorsCycleThemesAndBosses()
    {
        var level4 = Call("EndlessMode", "Level", 4);
        var level5 = Call("EndlessMode", "Level", 5);
        Assert.AreNotEqual("Boss", level4.GetType().GetField("kind").GetValue(level4).ToString());
        Assert.AreEqual("Boss", level5.GetType().GetField("kind").GetValue(level5).ToString());
        Assert.AreEqual(31, Call("EndlessMode", "BattleLevel", 1), "güç 6. bölgenin sonundan devam eder");

        // Katlar 1-5 Bölge 1, 6-10 Bölge 2 temasında.
        var theme1 = Call("EndlessMode", "Theme", 3);
        var theme2 = Call("EndlessMode", "Theme", 7);
        Assert.AreEqual(1, theme1.GetType().GetField("id").GetValue(theme1));
        Assert.AreEqual(2, theme2.GetType().GetField("id").GetValue(theme2));
        var region10 = Call("EndlessMode", "Region", 10);
        Assert.AreEqual("ForestWitch", region10.GetType().GetField("bossType").GetValue(region10).ToString());
        Assert.AreEqual(99, region10.GetType().GetField("id").GetValue(region10));

        // Ejder İstilası yalnız hafta sonu; kalan süre hesabı.
        var saturday = new DateTime(2026, 11, 14, 10, 0, 0);
        var wednesday = new DateTime(2026, 11, 11, 10, 0, 0);
        Assert.IsTrue((bool)Call("DragonInvasion", "IsWeekend", saturday));
        Assert.IsFalse((bool)Call("DragonInvasion", "IsWeekend", wednesday));
        Assert.AreEqual(TimeSpan.FromHours(38), Call("DragonInvasion", "TimeLeft", saturday), "Pazar gece yarısına");
        Assert.AreEqual(TimeSpan.FromHours(62), Call("DragonInvasion", "TimeLeft", wednesday), "Cumartesiye");
    }

    [UnityTest]
    public IEnumerator SessionAndRewards()
    {
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null,
            new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        int Gems() => (int)managerType.GetProperty("Gems").GetValue(manager);

        try
        {
            // Sonsuz Sefer bağlamı sefer akışını kullanır, kendi gücüyle.
            Call("BattleSession", "BeginEndless", 7);
            Assert.IsTrue((bool)Static("BattleSession", "IsCampaign"));
            Assert.IsTrue((bool)Static("BattleSession", "IsEndless"));
            Assert.AreEqual(37, Static("BattleSession", "BattleLevel"));
            Call("BattleSession", "Begin", 2, 3);
            Assert.IsFalse((bool)Static("BattleSession", "IsSpecial"), "sefer başlatınca özel mod kapanır");
            Assert.AreEqual(8, Static("BattleSession", "BattleLevel"));

            // Kat ödülü: 5'in katı ilk kez geçilince +3 elmas; tekrar ve ara katlar elmas vermez.
            int gems = Gems();
            Assert.AreEqual(0, Call("EndlessMode", "RecordClear", 4));
            Assert.AreEqual(3, Call("EndlessMode", "RecordClear", 5));
            Assert.AreEqual(0, Call("EndlessMode", "RecordClear", 5));
            Assert.AreEqual(5, Static("EndlessMode", "BestFloor"));
            Assert.AreEqual(6, Static("EndlessMode", "NextFloor"));
            Assert.AreEqual(gems + 3, Gems());

            // İstila: haftada bir ilk zafer elması.
            Assert.IsTrue((bool)Static("DragonInvasion", "FirstWinAvailable"));
            Assert.AreEqual(15, Call("DragonInvasion", "RecordWin"));
            Assert.AreEqual(0, Call("DragonInvasion", "RecordWin"));
            Assert.IsFalse((bool)Static("DragonInvasion", "FirstWinAvailable"));
        }
        finally
        {
            Call("BattleSession", "Clear");
        }
    }
}
