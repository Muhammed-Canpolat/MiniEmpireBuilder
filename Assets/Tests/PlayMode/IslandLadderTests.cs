using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Martı Adası: Akın formülleri, kapı, ödül, oto-ateş kapısı, yerleşim ve okçu/tekne temel davranışı (yansıma ile).</summary>
public class IslandLadderTests
{
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private static Type IslandType => Type("Progression+Island");

    private static object Call(Type type, string method, params object[] args) => type.GetMethod(method).Invoke(null, args);
    private static int Int(string method, params object[] args) => (int)Call(IslandType, method, args);
    private static float Float(string method, params object[] args) => (float)Call(IslandType, method, args);
    private static object Const(Type type, string name) => type.GetField(name).GetValue(null);

    [Test]
    public void BattleLevelWavesAndBoatsMatchDesign()
    {
        // island_base.md §5 örnek tablosu: N = 1 / 5 / 10 / 20 / 30
        CollectionAssert.AreEqual(new[] { 2, 5, 9, 17, 25 }, new[] { 1, 5, 10, 20, 30 }.Select(n => Int("AkinBattleLevel", n)).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 3, 5, 7, 7 }, new[] { 1, 5, 10, 20, 30 }.Select(n => Int("AkinBoats", n)).ToArray());
        Assert.AreEqual(1, Int("AkinWaves", 1));
        Assert.AreEqual(1, Int("AkinWaves", 2));
        Assert.AreEqual(2, Int("AkinWaves", 5));
        Assert.AreEqual(2, Int("AkinWaves", 9));
        Assert.AreEqual(3, Int("AkinWaves", 20));
        Assert.AreEqual(4, Int("AkinWaves", 30));
    }

    [Test]
    public void BeachesOpenWithLadder()
    {
        Assert.AreEqual(1, Int("BeachesOpen", 1));
        Assert.AreEqual(1, Int("BeachesOpen", 3));
        Assert.AreEqual(2, Int("BeachesOpen", 4));
        Assert.AreEqual(3, Int("BeachesOpen", 9));
        Assert.AreEqual(4, Int("BeachesOpen", 15));
    }

    [Test]
    public void GateRequiresKeepLevelAndSequentialOrder()
    {
        // Şart: KK >= ceil(N/3): KK 1 / 2 / 4 / 7 / 10 → N = 1 / 5 / 10 / 20 / 30
        CollectionAssert.AreEqual(new[] { 1, 2, 4, 7, 10 }, new[] { 1, 5, 10, 20, 30 }.Select(n => Int("AkinRequiredCastle", n)).ToArray());
        bool Can(int n, int keep, int highest) => (bool)Call(IslandType, "CanStartAkin", n, keep, highest);
        Assert.IsTrue(Can(1, 1, 0));
        Assert.IsTrue(Can(3, 1, 2));
        Assert.IsFalse(Can(4, 1, 3), "Akın 4 Kıyı Kalesi 2 ister");
        Assert.IsTrue(Can(4, 2, 3));
        Assert.IsFalse(Can(5, 2, 3), "Sıralı: en çok en yüksek + 1");
        Assert.IsTrue(Can(2, 1, 5), "Tekrar oynanabilir");
        Assert.AreEqual(15, Int("MaxAkin", 5), "En yüksek Akın = 3 × KK");
    }

    [Test]
    public void RewardsMatchDesignAndBalanceChecks()
    {
        Assert.AreEqual(7, Int("AkinMeat", 1), "v2: Et 5 + 2N");
        Assert.AreEqual(105, Int("AkinWood", 10));
        Assert.AreEqual(25, Int("AkinGold", 1));
        // Denge kontrolü: Akın 1–15 ilk zafer altını toplamı 900 (= MB 5→6 yükseltmesi).
        Assert.AreEqual(900, Enumerable.Range(1, 15).Sum(n => Int("AkinGold", n)));
        // Elmas: Akın 45'e kadar 27 + 20 = 47.
        Assert.AreEqual(47, Enumerable.Range(1, 45).Sum(n => Int("AkinGems", n)));
        Assert.AreEqual(0, Int("AkinGems", 4));
        Assert.AreEqual(3, Int("AkinGems", 5));
        Assert.AreEqual(8, Int("AkinGems", 10), "boss + her 5. Akın");
    }

    [Test]
    public void SessionRewardIsFullFirstTimeThenThirtyPercent()
    {
        var session = Type("IslandSession");
        object first = Call(session, "RewardFor", 10, 9);
        object replay = Call(session, "RewardFor", 10, 10);
        int F(object reward, string field) => (int)reward.GetType().GetField(field).GetValue(reward);
        Assert.AreEqual(70, F(first, "gold"));
        Assert.AreEqual(8, F(first, "gems"));
        Assert.IsTrue((bool)first.GetType().GetField("firstWin").GetValue(first));
        Assert.AreEqual(21, F(replay, "gold"), "tekrar ödülün %30'u");
        Assert.AreEqual(0, F(replay, "gems"), "elmas yalnız ilk zaferde");
        Assert.IsFalse((bool)replay.GetType().GetField("firstWin").GetValue(replay));
    }

    [Test]
    public void ArcherAndCastleStatsFollowCurves()
    {
        Assert.AreEqual(12f, Float("ArcherDamage", 1), 0.001f);
        Assert.AreEqual(19.2f, Float("ArcherDamage", 5), 0.01f);
        Assert.AreEqual(37.2f, Float("ArcherDamage", 15), 0.01f);
        Assert.AreEqual(150f, Float("ArcherHp", 1), 0.001f);
        Assert.AreEqual(300f, Float("CastleHp", 1), 0.001f);
        Assert.AreEqual(620f, Float("CastleHp", 5), 0.001f);
    }

    [Test]
    public void AutoFireIsGatedByKeepLevelAndOnlyTheExplicitDevBypassOpensIt()
    {
        Assert.AreEqual(5, Const(IslandType, "AutoFireUnlockKeepLevel"));
        Assert.AreEqual(0.75f, (float)Const(IslandType, "AutoFireRateMult"), 0.0001f);
        var dev = Type("DevSettings");
        // 9. oturum (KN-4a): ana anahtar (Enabled) tek başına kural değiştirmez; yalnız açıkça seçilen "ada kilitlerini atla".
        var master = dev.GetProperty("Enabled");
        bool masterWas = (bool)master.GetValue(null);
        master.SetValue(null, true);
        var enabled = dev.GetProperty("IslandLockBypass");
        bool original = (bool)enabled.GetValue(null);
        var session = Type("IslandSession");
        try
        {
            enabled.SetValue(null, false);
            if ((bool)dev.GetField("Available").GetValue(null))
            {
                Assert.IsFalse((bool)Call(session, "AutoFireUnlocked", 4));
                Assert.IsTrue((bool)Call(session, "AutoFireUnlocked", 5));
                enabled.SetValue(null, true);
                Assert.IsTrue((bool)Call(session, "AutoFireUnlocked", 1), "atlama açıkken test için açık");
            }
        }
        finally { enabled.SetValue(null, original); master.SetValue(null, masterWas); }
    }

    [Test]
    public void LayoutV2HasThreeLevelsStairsAndRoutesOfDesignedLength()
    {
        var layout = Type("IslandLayout");
        Assert.AreEqual(22, Const(layout, "Width"));
        Assert.AreEqual(36, Const(layout, "Height"));
        var rows = (string[])layout.GetField("Rows").GetValue(null);
        Assert.AreEqual(36, rows.Length);
        foreach (var row in rows) Assert.AreEqual(22, row.Length);
        Assert.AreEqual('~', (char)Call(layout, "Cell", 0, 0));
        Assert.AreEqual(2, Call(layout, "HeightAt", 10, 4), "kale zirvede (sv2)");
        Assert.AreEqual(1, Call(layout, "HeightAt", 10, 10), "yayla sv1");
        Assert.AreEqual(0, Call(layout, "HeightAt", 10, 18), "ova sv0");
        Assert.AreEqual(-1, Call(layout, "HeightAt", 0, 0), "su");
        Assert.IsTrue((bool)Call(layout, "IsCliff", 10, 8), "zirve yarı");
        Assert.IsTrue((bool)Call(layout, "IsCliff", 5, 14), "yayla yarı");
        Assert.IsTrue((bool)Call(layout, "IsStair", 15, 6), "S1");
        Assert.IsTrue((bool)Call(layout, "IsStair", 11, 23), "S4 (küçük yayla)");
        Assert.IsTrue((bool)Call(layout, "IsCliff", 12, 24), "küçük yayla yarı");
        Assert.IsTrue((bool)Call(layout, "IsStair", 3, 13), "S2");
        Assert.IsTrue((bool)Call(layout, "IsStair", 18, 13), "S3");

        var posts = (Array)layout.GetField("Posts").GetValue(null);
        Assert.AreEqual(5, posts.Length);
        var beaches = (Array)layout.GetField("Beaches").GetValue(null);
        Assert.AreEqual(4, beaches.Length);
        // Tekne görünür süresi (Sandal 1,0 u/s): A/B uzun kanal, C/D kısa güney yaklaşması (v2 §4).
        int i = 0;
        foreach (var beach in beaches)
        {
            var path = (Vector2[])beach.GetType().GetField("boatPath").GetValue(beach);
            float length = (float)Call(layout, "PathLength", new object[] { path });
            if (i < 2) Assert.That(length, Is.InRange(18f, 32f), "A/B tekne yolu (sn @1 u/s)");
            else Assert.That(length, Is.InRange(4f, 9f), "C/D tekne yolu");
            Assert.GreaterOrEqual(((Vector2[])beach.GetType().GetField("walkPath").GetValue(beach)).Length, 8);
            i++;
        }
        // Komşu atış yerleri arası koşu (9 u/sn): yayla geçişleri 1–3 sn.
        foreach (var (a, b) in new[] { (0, 1), (0, 2), (2, 3), (2, 4), (3, 4) })
        {
            float seconds = (float)Call(layout, "PathSeconds", a, b);
            Assert.That(seconds, Is.InRange(0.6f, 3.5f), $"P{a + 1}→P{b + 1}");
        }
        var path01 = (List<Vector2>)Call(layout, "PathBetween", 0, 1);
        Assert.GreaterOrEqual(path01.Count, 3);
    }

    [Test]
    public void TerrainRulesProduceNoWarningsAndFollowKitTileTables()
    {
        var rules = Type("IslandTerrainRules");
        var layout = Type("IslandLayout");
        var result = layout.GetProperty("Terrain").GetValue(null);
        var warnings = (System.Collections.IList)result.GetType().GetField("warnings").GetValue(result);
        Assert.AreEqual(0, warnings.Count, string.Join("; ", warnings.Cast<string>()));
        var stairs = (System.Collections.IList)result.GetType().GetField("stairs").GetValue(result);
        Assert.AreEqual(4, stairs.Count, "3 ana merdiven + küçük yayla");
        // Kit §4.1: tablo uzunlukları ve örnek maskeler (15 = merkez)
        Assert.AreEqual(9, ((int[])rules.GetField("FlatByMask").GetValue(null))[15]);
        Assert.AreEqual(13, ((int[])rules.GetField("ElevByMask").GetValue(null))[15]);
        Assert.AreEqual(35, ((int[])rules.GetField("CliffGround").GetValue(null))[3], "yar ortası C18");
        Assert.AreEqual(41, ((int[])rules.GetField("CliffWater").GetValue(null))[3], "suya bakan yar ortası C22");
        // Küçük harita: 7 genişliğinde sv1 plato, altı su → suya bakan yar C21 · C22… · C23 (kit §4.2)
        var small = (object)rules.GetMethod("Build").Invoke(null, new object[] { new[]
        {
            "~~~~~~~~~",
            "~1111111~",
            "~1111111~",
            "~~~~~~~~~",
        } });
        var elev = (System.Collections.IList)small.GetType().GetField("elev").GetValue(small);
        var level1 = (System.Collections.IDictionary)elev[1];
        Assert.AreEqual(40, level1[new Vector2Int(1, 0)], "sol uç C21");
        Assert.AreEqual(41, level1[new Vector2Int(4, 0)], "orta C22");
        Assert.AreEqual(42, level1[new Vector2Int(7, 0)], "sağ uç C23");
        Assert.AreEqual(4, level1[new Vector2Int(1, 2)], "sol-üst köşe E1");
        Assert.AreEqual(5, level1[new Vector2Int(4, 2)], "üst kenar E2");
    }

    // ---- Oynanış: tekne batışı ve okçu (sahne nesneleriyle) ----

    private static object EnemyList(params string[] names)
    {
        var enemyType = Type("EnemyType");
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(enemyType));
        foreach (var name in names) list.Add(Enum.Parse(enemyType, name));
        return list;
    }

    private static Component SpawnBoat(int beach, string kind = "Rowboat")
    {
        var boatType = Type("RaidBoat");
        var kindType = Type("Progression+Island+BoatKind");
        return (Component)boatType.GetMethod("Spawn").Invoke(null,
            new[] { Enum.Parse(kindType, kind), 1, beach, EnemyList("Deckhand", "Deckhand"), 1f });
    }

    [UnityTest]
    public IEnumerator SunkBoatDrownsItsCrew()
    {
        var boat = SpawnBoat(0);
        yield return null;
        var boatType = boat.GetType();
        Assert.AreEqual(2, boatType.GetProperty("CrewLeft").GetValue(boat));
        Assert.IsTrue((bool)boatType.GetProperty("Alive").GetValue(boat));
        boatType.GetMethod("TakeArrow").Invoke(boat, new object[] { 10000f, false });
        Assert.IsFalse((bool)boatType.GetProperty("Alive").GetValue(boat), "batan tekne canlı sayılmaz");
        Assert.AreEqual("Sinking", boatType.GetProperty("State").GetValue(boat).ToString());
        Assert.AreEqual(0, boatType.GetProperty("CrewLeft").GetValue(boat), "içindekiler boğuldu");
        yield return new WaitForSeconds(1.2f);
        Assert.IsTrue(boat == null, "batık yok olur");
    }

    [UnityTest]
    public IEnumerator ArcherRunsBetweenPostsAndShootsBoatInRange()
    {
        var archerType = Type("IslandArcher");
        var archer = (Component)archerType.GetMethod("Create").Invoke(null, new object[] { 5, 5, 0 });
        yield return null;
        Assert.AreEqual(0, archerType.GetProperty("CurrentPost").GetValue(archer));
        Assert.IsTrue((bool)archerType.GetMethod("MoveToPost").Invoke(archer, new object[] { 2 }), "P3 açık (KK1)");
        yield return new WaitForSeconds(3.4f);
        Assert.AreEqual(2, archerType.GetProperty("CurrentPost").GetValue(archer), "P3'e vardı");
        Assert.IsFalse((bool)archerType.GetProperty("Running").GetValue(archer));

        var boat = SpawnBoat(0);
        // Tekneyi P3'ün menzilindeki kıyıya (A kumsalı açığı) götür.
        boat.transform.position = new Vector3(1.5f, 18f, 0f);
        yield return null;
        float before = (float)boat.GetType().GetProperty("Health").GetValue(boat);
        Assert.IsTrue((bool)archerType.GetMethod("DebugFireAtNearest").Invoke(archer, null), "menzilde hedef var");
        yield return new WaitForSeconds(0.7f);
        float after = (float)boat.GetType().GetProperty("Health").GetValue(boat);
        Assert.Less(after, before, "ok tekneye işledi");
        UnityEngine.Object.Destroy(boat.gameObject);
        UnityEngine.Object.Destroy(archer.gameObject);
    }
}
