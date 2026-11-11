using System;
using System.Linq;
using NUnit.Framework;

/// <summary>Savaş dengesi v1 (Docs/design/balance_v1.md): başlangıç altını, ilk dalga sınırı, taraf sayısı, doğma aralığı.</summary>
public class BattleBalanceTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Call(string method, int region) =>
        Type("Progression").GetMethod(method).Invoke(null, new object[] { region });

    [Test]
    public void BalanceCurvesMatchDesignNote()
    {
        // Başlangıç altını Ana Üs seviyesiyle açılır (round2 D).
        Assert.AreEqual(0, Call("BattleStartGold", 1));
        Assert.AreEqual(0, Call("BattleStartGold", 6));
        Assert.AreEqual(25, Call("BattleStartGold", 7));
        Assert.AreEqual(50, Call("BattleStartGold", 10));
        Assert.AreEqual(75, Call("BattleStartGold", 15));
        Assert.AreEqual(7, Call("Wave1Cap", 1));
        Assert.AreEqual(12, Call("Wave1Cap", 6));
        Assert.AreEqual(1, Call("WaveSpawnSides", 2));
        Assert.AreEqual(2, Call("WaveSpawnSides", 3));
        Assert.AreEqual(1.25f, (float)Call("SpawnInterval", 1), 0.001f);
        Assert.AreEqual(0.9f, (float)Call("SpawnInterval", 6), 0.001f);
    }

    [Test]
    public void SixthRegionOpensWithHeroHunters()
    {
        var region = Type("CampaignCatalog").GetMethod("Get").Invoke(null, new object[] { 6 });
        var types = (Array)region.GetType().GetField("enemyTypes").GetValue(region);
        Assert.AreEqual("ShadowWolf", types.GetValue(0).ToString(), "Bölge 6 ilk dalgası kahramanı kovalar (üsse yığılmaz)");
    }
}
