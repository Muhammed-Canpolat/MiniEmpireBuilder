using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// 7. oturum: bölgeye özgü savaş haritaları. V = veri doğrulama, B = savaş kurulumu/oynanış, G = görüntüler.
/// </summary>
public class Session7Tests
{
    private static Type T(string name) => Session6Tests.T(name);
    private static object S(string type, string method, params object[] args) => Session6Tests.S(type, method, args);
    private static object F(object o, string f) => Session6Tests.F(o, f);
    private static object P(object o, string p) => Session6Tests.P(o, p);

    internal static string Folder
    {
        get
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session7"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    // ==================== V: VERİ DOĞRULAMA ====================

    /// <summary>
    /// Her bölge × her seviye: rota üst köprüden başlayıp Ana Üs'e kesintisiz ve kara üstünden ulaşır; hiçbir yuva yol/su/üs/maden/başka yuvayla
    /// çakışmaz; her yuva menziliyle en az bir dönemeci kapsar; maden yoldan yürünerek ulaşılır (BattleLayoutGenerator.Validate).
    /// </summary>
    [Test]
    public void V1_AllThirtyLayoutsAreValid()
    {
        var errors = new List<string>();
        int checkedCount = 0;
        for (int region = 1; region <= 6; region++)
        {
            var map = S("BattleMapCatalog", "Region", region);
            Assert.NotNull(map, $"Bölge {region} haritası");
            for (int level = 1; level <= 5; level++)
            {
                var layout = S("BattleMapCatalog", "Level", region, level);
                Assert.NotNull(layout, $"B{region}-S{level} yerleşimi");
                errors.AddRange((List<string>)S("BattleLayoutGenerator", "Validate", map, layout));
                checkedCount++;
            }
        }
        File.WriteAllLines(Path.Combine(Folder, "V1_validation.txt"), errors.Count == 0 ? new[] { "30/30 geçerli" } : errors.ToArray());
        Assert.AreEqual(30, checkedCount);
        Assert.IsEmpty(errors, string.Join("\n", errors));
    }

    /// <summary>Her bölgenin 1. seviyesi Sefer Haritası'ndaki yolun aynısı (generate.py noktaları); seviye türleri bölge verisiyle aynı.</summary>
    [Test]
    public void V2_FirstLevelUsesTheCampaignMapRouteAndKindsMatch()
    {
        for (int region = 1; region <= 6; region++)
        {
            var template = (IList)S("BattleLayoutGenerator", "RouteTemplate", region, 1);
            var expected = (Vector2[])template[0];
            var layout = S("BattleMapCatalog", "Level", region, 1);
            var routes = (IList)F(layout, "routes");
            Assert.AreEqual(1, routes.Count, $"B{region}-S1 tek yol");
            var points = (Vector2[])F(routes[0], "points");
            Assert.AreEqual(expected.Length, points.Length, $"B{region}-S1 nokta sayısı");
            for (int i = 0; i < points.Length; i++)
                Assert.Less(Vector2.Distance(expected[i], points[i]), 0.01f, $"B{region}-S1 nokta {i}: harita yolu");
            // Zikzak yönü: tek bölgelerde Ana Üs'e yakın büyük kıvrım SAĞDA (haritada alttan önce sağa)
            int side = region % 2 == 1 ? 1 : -1;
            Assert.Greater(points[4].x * side, 3f, $"B{region}-S1 alt kıvrım haritadaki tarafta");
            Assert.Less(points[3].x * side, -3f, $"B{region}-S1 orta kıvrım ters tarafta");
            var regionData = S("CampaignCatalog", "Get", region);
            var levels = (Array)F(regionData, "levels");
            for (int level = 1; level <= 5; level++)
            {
                var lay = S("BattleMapCatalog", "Level", region, level);
                Assert.AreEqual(F(levels.GetValue(level - 1), "kind").ToString(), F(lay, "kind").ToString(), $"B{region}-S{level} türü");
            }
        }
    }

    /// <summary>Tekrar hissi: aynı bölgenin 5 seviyesinin rotaları birbirinden farklı; 4. seviye çatallı, boss rotası en uzun.</summary>
    [Test]
    public void V3_RoutesVaryAcrossLevels()
    {
        for (int region = 1; region <= 6; region++)
        {
            var lengths = new List<float>();
            var slotSets = new List<string>();
            for (int level = 1; level <= 5; level++)
            {
                var layout = S("BattleMapCatalog", "Level", region, level);
                var routes = (Array)P(layout, "Routes");
                lengths.Add((float)P(routes.GetValue(0), "Length"));
                if (level == 4) Assert.AreEqual(2, routes.Length, $"B{region}-S4 ikiye ayrılan yol");
                slotSets.Add(string.Join(";", ((Vector2[])F(layout, "slots")).Select(v => v.ToString())));
            }
            Assert.AreEqual(5, lengths.Select(l => Mathf.Round(l * 10f)).Distinct().Count(), $"B{region}: 5 farklı rota");
            Assert.AreEqual(lengths.Max(), lengths[4], $"B{region}: boss rotası en uzun");
            Assert.AreEqual(5, slotSets.Distinct().Count(), $"B{region}: yuva düzenleri farklı");
        }
    }
}
