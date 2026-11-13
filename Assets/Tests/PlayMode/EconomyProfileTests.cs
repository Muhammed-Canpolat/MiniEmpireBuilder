using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AŞAMA 4 ekonomi: EconomyConfig profilleri (Legacy birebir eski değerler, V2 = ECONOMY.md §7), asset ↔ kod eşliği,
/// Sonsuz/İstila kule bedeli (§5), ganimet payı, 2x zafer günlük hakkı ve Ada Tüccarı.
/// </summary>
public class EconomyProfileTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static object Load(string profile) => Type("Economy").GetMethod("Load").Invoke(null, new object[] { profile });
    private static object Create(string method) => Type("EconomyConfig").GetMethod(method).Invoke(null, null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);

    private static void Override(string profile) => Type("Economy").GetMethod("OverrideForTests").Invoke(null, new object[] { profile });
    private static void ClearOverride() => Type("Economy").GetMethod("ClearOverride").Invoke(null, null);

    [TearDown]
    public void TearDown() => ClearOverride();

    [Test]
    public void AssetsMatchCodeProfiles()
    {
        foreach (var (asset, code) in new[] { ("Economy_Legacy", "CreateLegacy"), ("Economy_V2", "CreateV2") })
        {
            var loaded = Resources.Load("Config/" + asset, Type("EconomyConfig"));
            Assert.NotNull(loaded, asset + " asset'i Resources/Config altında olmalı");
            var expected = Create(code);
            foreach (var field in Type("EconomyConfig").GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object a = field.GetValue(loaded), b = field.GetValue(expected);
                if (a is Array arrayA) CollectionAssert.AreEqual((Array)b, arrayA, $"{asset}.{field.Name}");
                else if (a is float fa) Assert.AreEqual((float)b, fa, 0.0001f, $"{asset}.{field.Name}");
                else Assert.AreEqual(b, a, $"{asset}.{field.Name}");
            }
        }
    }

    [Test]
    public void DefaultProfileIsV2()
    {
        ClearOverride();
        var active = Type("Economy").GetProperty("Active").GetValue(null);
        Assert.AreEqual("V2", Field(active, "profileName"));
    }

    [Test]
    public void LegacyKeepsOldNumbers()
    {
        var legacy = Load("Legacy");
        Assert.AreEqual(6500, Call(legacy, "MainBaseCost", 15));
        Assert.AreEqual(850, Call(legacy, "MainBaseCost", 6));
        Assert.AreEqual(5.5f, (float)Call(legacy, "MineGps", 10), 0.0001f);
        Assert.AreEqual(1550, Call(legacy, "MineStorage", 10));
        Assert.AreEqual(10, Call(legacy, "ApprenticeMineBonusPercent", 1));
        Assert.AreEqual(50, Call(legacy, "ApprenticeMineBonusPercent", 15));
        Assert.AreEqual(60, Call(legacy, "HeroCost", 1));
        Assert.AreEqual(80, Call(legacy, "ApprenticeCost", 1));
        Assert.AreEqual(9800, Call(legacy, "WalletCap", 6500));
        Assert.AreEqual(1f, (float)Field(legacy, "killGoldTransferRate"));
        Assert.AreEqual(0, Field(legacy, "victoryDoubleAdsPerDay"), "LEGACY: 2x sınırsız");
        Assert.IsTrue((bool)Field(legacy, "victoryRewardFromAsset"));
        Assert.IsFalse((bool)Field(legacy, "traderEnabled"));
    }

    [Test]
    public void V2MatchesEconomyDoc()
    {
        var v2 = Load("V2");
        int[] mb = { 0, 0, 150, 250, 400, 600, 850, 1250, 1800, 2500, 3500, 4800, 6400, 8400, 10800, 13800 };
        for (int level = 2; level <= 15; level++) Assert.AreEqual(mb[level], Call(v2, "MainBaseCost", level), "MB " + level);
        Assert.AreEqual(850, Field(v2, "builder2GoldCost"), "2. inşaatçı Ana Üs tablosundan ayrık, 850");
        Assert.AreEqual(0.6f, (float)Call(v2, "MineGps", 1), 0.0001f);
        Assert.AreEqual(2.4f, (float)Call(v2, "MineGps", 10), 0.0001f);
        Assert.AreEqual(5, Call(v2, "ApprenticeMineBonusPercent", 1));
        Assert.AreEqual(25, Call(v2, "ApprenticeMineBonusPercent", 15));
        Assert.AreEqual(40, Call(v2, "VictoryRewardFormula", 1, 1));
        Assert.AreEqual(40 + 150 + 48, Call(v2, "VictoryRewardFormula", 6, 5));
        Assert.AreEqual(0.75f, (float)Field(v2, "killGoldTransferRate"), 0.0001f);
        Assert.AreEqual(0, Field(v2, "victoryDoubleAdsPerDay"), "13 Kasım: V2'de de 2x sınırsız (D17 kaldırıldı)");
        Assert.AreEqual(400, Call(v2, "TraderPackCost", 5));
        Assert.AreEqual(1000, Call(v2, "TraderPackCost", 10));
        // Hiçbir değer 3 kattan fazla değişmez (ECONOMY §7).
        var legacy = Load("Legacy");
        for (int level = 2; level <= 15; level++)
        {
            float ratio = (int)Call(v2, "MainBaseCost", level) / (float)(int)Call(legacy, "MainBaseCost", level);
            Assert.LessOrEqual(ratio, 3f, "MB " + level);
        }
    }

    [Test]
    public void EndlessAndInvasionTowerCostsFollowFormula()
    {
        var v2 = Load("V2");
        int Cost(int baseCost, int battleLevel, float perWave) => (int)Call(v2, "EndlessBuildCost", baseCost, battleLevel, perWave);
        int PerWave(int floor) => (int)Type("EndlessMode").GetMethod("PerWave").Invoke(null, new object[] { floor });
        int Level(int floor) => (int)Type("EndlessMode").GetMethod("BattleLevel").Invoke(null, new object[] { floor });
        // ECONOMY §5 tablosu: kat 1 → 170 / 255 / 205, kat 11 → Okçu 300.
        Assert.AreEqual(170, Cost(50, Level(1), PerWave(1)));
        Assert.AreEqual(255, Cost(75, Level(1), PerWave(1)));
        Assert.AreEqual(205, Cost(60, Level(1), PerWave(1)));
        Assert.AreEqual(300, Cost(50, Level(11), PerWave(11)));
        Assert.Less(Cost(50, Level(1), PerWave(1)), 483, "ilk Okçu kat 1'de başlangıç + 1. dalga altınından ucuz");
        Assert.Less(Cost(50, 50, 14f), 1030, "İstila: eski ×20 hatası yok");
    }

    [Test]
    public void PayoutUsesLootShareAndReplayRate()
    {
        var compute = Type("BattleManager").GetMethod("ComputePayout");
        Override("V2");
        Assert.AreEqual(850, compute.Invoke(null, new object[] { 1000, 100, false }), "V2: net savaş altınının %75'i + ödül");
        Assert.AreEqual(255, compute.Invoke(null, new object[] { 1000, 100, true }), "tekrar: ×0,3");
        Override("Legacy");
        Assert.AreEqual(1100, compute.Invoke(null, new object[] { 1000, 100, false }));
        Assert.AreEqual(330, compute.Invoke(null, new object[] { 1000, 100, true }));
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
    }

    private void SetField(string name, object value) => save.GetType().GetField(name).SetValue(save, value);

    [UnityTest]
    public IEnumerator VictoryDoubleAdHasDailyLimitInV2()
    {
        yield return Setup();
        var ads = Type("AdRewards");
        bool Available() => (bool)ads.GetProperty("VictoryDoubleAvailable").GetValue(null);
        int Left() => (int)ads.GetProperty("VictoryDoubleLeft").GetValue(null);
        var consume = ads.GetMethod("ConsumeVictoryDouble");

        // 13 Kasım (D30): V2'de de günlük sınır yok (D17 kaldırıldı)
        Override("V2");
        for (int i = 0; i < 12; i++) { Assert.IsTrue(Available(), "hak " + (i + 1)); consume.Invoke(null, null); }
        Assert.IsTrue(Available(), "V2: sınırsız");

        Override("Legacy");
        for (int i = 0; i < 12; i++) consume.Invoke(null, null);
        Assert.IsTrue(Available(), "LEGACY: sınırsız");
    }

    [UnityTest]
    public IEnumerator IslandTraderSellsPacksForGold()
    {
        yield return Setup();
        var buildings = (IDictionary)managerType.GetProperty("Buildings").GetValue(manager);
        var mainBaseKey = Enum.Parse(Type("BuildingType"), "MainBase");
        if (!buildings.Contains(mainBaseKey))
        {
            var data = Activator.CreateInstance(Type("BuildingData"));
            buildings[mainBaseKey] = data;
        }
        var mainBase = buildings[mainBaseKey];
        var levelField = mainBase.GetType().GetField("level");
        object savedLevel = levelField.GetValue(mainBase);
        levelField.SetValue(mainBase, 6);
        try
        {
            var island = save.GetType().GetField("island").GetValue(save);
            Assert.NotNull(island, "yeni kayıtta ada verisi var");
            var economy = Type("IslandEconomy");
            int Prop(string name) => (int)economy.GetProperty(name).GetValue(null);
            var buy = economy.GetMethod("TryBuyTraderPack");
            bool Buy(bool meat) => (bool)buy.Invoke(null, new object[] { meat, null });

            Override("Legacy");
            Assert.IsFalse((bool)economy.GetProperty("TraderEnabled").GetValue(null), "LEGACY'de tüccar yok");

            Override("V2");
            Assert.IsTrue((bool)economy.GetProperty("TraderEnabled").GetValue(null));
            Assert.AreEqual(520, Prop("TraderPackCost"), "400 + 120 × (6 − 5)");
            SetField("gold", 100);
            int meat0 = Prop("Meat"), wood0 = Prop("Wood");
            Assert.IsFalse(Buy(true), "altın yetmezse alınmaz");
            Assert.AreEqual(meat0, Prop("Meat"));
            SetField("gold", 2000);
            Assert.IsTrue(Buy(true));
            Assert.AreEqual(meat0 + 50, Prop("Meat"));
            Assert.AreEqual(1480, (int)save.GetType().GetField("gold").GetValue(save));
            Assert.IsTrue(Buy(false));
            Assert.AreEqual(wood0 + 50, Prop("Wood"));
            Assert.AreEqual(0, Prop("TraderPacksLeft"));
            Assert.IsFalse(Buy(true), "günde 2 paket");
            Assert.AreEqual(960, (int)save.GetType().GetField("gold").GetValue(save));
        }
        finally { levelField.SetValue(mainBase, savedLevel); }
    }
}
