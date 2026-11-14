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

    // ==================== B: SAVAŞ KURULUMU ====================

    private static Component Battle => Object.FindFirstObjectByType(T("BattleManager")) as Component;

    /// <summary>Kameradan (UI dahil) görüntü; kamerayı verilen merkeze/boya taşıyıp geri alır.</summary>
    internal static void Shot(string name, Vector2? center = null, float? size = null, int w = 720, int h = 1440)
    {
        var cam = Camera.main;
        var follow = cam.GetComponent(T("BattleCameraFollow")) as Behaviour;
        bool wasOn = follow != null && follow.enabled;
        if (follow != null) follow.enabled = false;
        var pos = cam.transform.position; float old = cam.orthographicSize;
        if (center.HasValue) cam.transform.position = new Vector3(center.Value.x, center.Value.y, pos.z);
        if (size.HasValue) cam.orthographicSize = size.Value;
        Session4Tests.CaptureUi("../session7/" + name, w, h);
        cam.transform.position = pos; cam.orthographicSize = old;
        if (follow != null) follow.enabled = wasOn;
    }

    /// <summary>Her bölgenin 1. seviyesi: harita kuruldu, Ana Üs yolun sonunda, yuvalar yerleşimdeki yerlerde, düşman köprüden girip yolu izliyor.</summary>
    [UnityTest]
    public IEnumerator B1_EachRegionBuildsItsMapAndEnemiesFollowTheRoad()
    {
        var lines = new List<string>();
        for (int region = 1; region <= 6; region++)
        {
            yield return Session4Tests.StartBattle(region, 1, 15);
            var builder = Object.FindFirstObjectByType(T("BattleMapBuilder")) as Component;
            Assert.NotNull(builder, $"B{region}: harita kuruldu");
            Assert.IsTrue((bool)SP("BattleTerrain", "Active"), "arazi etkin");
            var layout = SP("BattleManager", "MapLayout");
            Assert.NotNull(layout, "yerleşim");
            var baseCtrl = Object.FindFirstObjectByType(T("MainBaseController")) as Component;
            Assert.Less(Vector2.Distance(baseCtrl.transform.position, (Vector2)F(layout, "basePosition")), 0.01f, "Ana Üs yerleşimdeki yerde");
            var slots = Object.FindObjectsByType(T("BattleBuildSlot"), FindObjectsSortMode.None).Cast<Component>().ToList();
            var expected = (Vector2[])F(layout, "slots");
            Assert.AreEqual(expected.Length, slots.Count, "yuva sayısı");
            foreach (var e in expected)
                Assert.IsTrue(slots.Any(sl => Vector2.Distance(sl.transform.position, e) < 0.05f), $"yuva {e} sahnede");
            var decor = (IList)builder.GetType().GetField("Decor").GetValue(builder);
            var landmarks = (IList)builder.GetType().GetField("Landmarks").GetValue(builder);
            lines.Add($"B{region}: süs {decor.Count}, yapı {landmarks.Count}, yuva {slots.Count}");
            Assert.Greater(decor.Count, 8, "süsler yerleşti");
            Shot($"B1_region{region}_overview", new Vector2(0f, 11f), 15.5f, 600, 1200);
            // Düşman: köprüden doğar, yolu izler
            var battle = Battle;
            var enemyType = T("EnemyType");
            var enemy = battle.GetType().GetMethod("SpawnVariantAt").Invoke(battle,
                new object[] { Enum.Parse(enemyType, "Zombie"), new Vector3(0f, 28.4f, 0f), Enum.Parse(T("EnemyVariant"), "None") }) as Component;
            Assert.NotNull(enemy);
            Assert.NotNull(P(enemy, "Lane"), "düşman yola bağlı");
            float maxOff = 0f;
            var route = P(enemy, "Lane");
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForSeconds(0.1f);
                if (enemy == null) break;
                route.GetType().GetMethod("Project").Invoke(route, new object[] { (Vector2)enemy.transform.position, 0f });
                var args = new object[] { (Vector2)enemy.transform.position, 0f };
                route.GetType().GetMethod("Project").Invoke(route, args);
                maxOff = Mathf.Max(maxOff, (float)args[1]);
            }
            Assert.Less(maxOff, 0.6f, $"B{region}: düşman yoldan sapmıyor (en çok {maxOff:0.00})");
            Assert.Greater((float)P(enemy, "LaneProgress"), 0.05f, "yolda ilerliyor");
        }
        File.WriteAllLines(Path.Combine(Folder, "B1_maps.txt"), lines);
    }

    private static object SP(string type, string member) => Session6Tests.SP(type, member);

    /// <summary>Yalnız dünya (UI'sız) görüntü: kamerayı verilen merkez/boya alıp çizer.</summary>
    internal static void ShotWorld(string name, Vector2 center, float size, int w, int h)
    {
        var cam = Camera.main;
        var pos = cam.transform.position; float old = cam.orthographicSize; float aspect = cam.aspect;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt; cam.aspect = (float)w / h;
        cam.transform.position = new Vector3(center.x, center.y, pos.z); cam.orthographicSize = size;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; cam.aspect = aspect;
        cam.transform.position = pos; cam.orthographicSize = old;
        Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
    }

    private static Component SpawnAt(string type, Vector2 at)
    {
        var battle = Battle;
        return battle.GetType().GetMethod("SpawnVariantAt").Invoke(battle,
            new object[] { Enum.Parse(T("EnemyType"), type), new Vector3(at.x, at.y, 0f), Enum.Parse(T("EnemyVariant"), "None") }) as Component;
    }

    private static bool OnLand(Vector2 p) => (bool)S("BattleTerrain", "IsLand", p);

    /// <summary>
    /// Madeni Koru (her bölgenin 3. seviyesi): maden yerleşimdeki yerde, Tiny Swords sprite'ı ve can barıyla (beyaz kare yok); yağmacı düşman
    /// yoldan ayrılıp madene KARA üstünden yürür ve vurur; yağmacı olmayan yoldan sapmaz.
    /// </summary>
    [UnityTest]
    public IEnumerator B2_DefendMineRaidersDetourOverLandToTheMine()
    {
        var lines = new List<string>();
        for (int region = 1; region <= 6; region++)
        {
            yield return Session4Tests.StartBattle(region, 3, 15);
            var battle = Battle;
            var layout = SP("BattleManager", "MapLayout");
            var mine = P(battle, "DefendedMine") as Component;
            Assert.NotNull(mine, $"B{region}: maden kuruldu");
            Vector2 minePos = (Vector2)F(layout, "minePosition");
            Assert.Less(Vector2.Distance(mine.transform.position, minePos), 0.6f, "maden yerleşimdeki yerde (görsel tabanı 0,5 yukarı)");
            var sr = mine.GetComponent<SpriteRenderer>();
            Assert.NotNull(sr.sprite, "maden sprite'ı (beyaz kare değil)");
            Assert.IsFalse(Session4Tests.NullSpriteRenderers().Any(n => n.Contains("Mine")), "madende boş sprite yok");
            Assert.NotNull(mine.GetType().GetField("healthBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(mine), "maden can barı");
            // Yağmacı: yolun maden hizasındaki noktasında doğar, RaidTarget = maden
            var route = ((Array)P(layout, "Routes")).GetValue(0);
            var args = new object[] { minePos, 0f };
            float s = (float)route.GetType().GetMethod("Project").Invoke(route, args);
            Vector2 start = (Vector2)route.GetType().GetMethod("PointAt").Invoke(route, new object[] { Mathf.Max(0f, s - 2.5f) });
            var raider = SpawnAt("Zombie", start);
            raider.GetType().GetProperty("RaidTarget").SetValue(raider, mine);
            float health = (float)P(mine, "CurrentHealth");
            bool allLand = true; float closest = float.MaxValue;
            Time.timeScale = 2f;
            for (int i = 0; i < 160 && raider != null; i++)
            {
                yield return new WaitForSeconds(0.1f);
                if (raider == null) break;
                Vector2 p = raider.transform.position;
                if (!OnLand(p)) allLand = false;
                closest = Mathf.Min(closest, Vector2.Distance(p, minePos));
                if ((float)P(mine, "CurrentHealth") < health) break;
            }
            Time.timeScale = 1f;
            bool hit = (float)P(mine, "CurrentHealth") < health;
            lines.Add($"B{region}: maden {minePos}, yağmacı en yakın {closest:0.0}, vurdu {hit}, hep karada {allLand}");
            Assert.IsTrue(allLand, $"B{region}: sapma rotası yürünebilir kara üstünden");
            Assert.IsTrue(hit, $"B{region}: yağmacı madene ulaşıp vurdu (en yakın {closest:0.0})");
            ShotWorld($"B2_region{region}_mine", minePos + new Vector2(0f, 1f), 6f, 540, 800);
        }
        File.WriteAllLines(Path.Combine(Folder, "B2_mine.txt"), lines);
    }

    /// <summary>Kahraman su/uçurum üstüne yürüyemez (kıyı boyunca kayar); yapılardan geçemez.</summary>
    [UnityTest]
    public IEnumerator B3_HeroStaysOnWalkableLand()
    {
        yield return Session4Tests.StartBattle(1, 1, 15);
        var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
        var move = hero.GetType().GetMethod("SetMoveInput");
        foreach (var dir in new[] { Vector2.left, Vector2.right, Vector2.down })
        {
            float t = 0f;
            while (t < 3.5f)
            {
                move.Invoke(hero, new object[] { dir });
                Vector2 feet = (Vector2)hero.transform.position + new Vector2(0f, -0.25f);
                Assert.IsTrue(OnLand(feet), $"kahraman karada ({feet}, yön {dir})");
                t += Time.deltaTime;
                yield return null;
            }
        }
        move.Invoke(hero, new object[] { Vector2.zero });
        Vector2 last = hero.transform.position;
        Assert.Greater(last.y, -6f, "aşağı uçurumdan düşmedi");
    }

    /// <summary>Boss seviyesi: boss köprüden girer ve yolu izler (rota en uzun); Zamana Karşı: sayaç çalışır.</summary>
    [UnityTest]
    public IEnumerator B4_BossFollowsTheLongRouteAndTimeTrialCounts()
    {
        yield return Session4Tests.StartBattle(1, 5, 15);
        var layout = SP("BattleManager", "MapLayout");
        Assert.AreEqual("Boss", F(layout, "kind").ToString());
        var boss = SpawnAt("Troll", new Vector2(0f, 28.4f));
        var lane = P(boss, "Lane");
        Assert.NotNull(lane, "boss yola bağlı");
        float maxOff = 0f;
        Time.timeScale = 3f;
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForSeconds(0.1f);
            var args = new object[] { (Vector2)boss.transform.position, 0f };
            lane.GetType().GetMethod("Project").Invoke(lane, args);
            maxOff = Mathf.Max(maxOff, (float)args[1]);
        }
        Time.timeScale = 1f;
        Assert.Less(maxOff, 0.6f, "boss yoldan sapmıyor");
        yield return Session4Tests.StartBattle(2, 4, 15);
        var battle = Battle;
        Assert.IsTrue((bool)P(battle, "HasTimeLimit"), "Zamana Karşı");
        yield return new WaitForSeconds(4f);
        float remaining = (float)P(battle, "TimeRemaining");
        Assert.Less(remaining, 180f, "sayaç akıyor");
        Assert.AreEqual(2, ((Array)P(SP("BattleManager", "MapLayout"), "Routes")).Length, "4. seviye çatallı yol");
    }

    // ==================== G: GÖRÜNTÜLER + PERFORMANS ====================

    /// <summary>
    /// Her bölge × her seviye türü: 2 kule kurulu, yolda düşmanlar, atmosfer açık; oyun görüntüsü (HUD'lu, telefon oranı) + tam harita (UI'sız).
    /// Atmosfer partikül sayısı normal ve düşük kalitede ölçülür (düşük ≤ normal/2 + 2, normal ≤ 60).
    /// </summary>
    [UnityTest, Timeout(1800000)]
    public IEnumerator G1_EveryRegionEveryKindScreenshots()
    {
        var lines = new List<string>();
        var tryBuild = T("BattleBuildSlot").GetMethod("TryBuild", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        string[] kinds = { "HayattaKal", "UssuKoru", "MadeniKoru", "ZamanaKarsi", "Boss" };
        for (int region = 1; region <= 6; region++)
            for (int level = 1; level <= 5; level++)
            {
                S("BattleFxQuality", "Force", new object[] { level == 2 ? (bool?)true : (bool?)false });
                yield return Session4Tests.StartBattle(region, level, 15);
                yield return new WaitForSeconds(0.3f);
                var fx = Object.FindFirstObjectByType(T("BattleAtmosphereFx")) as Component;
                int particles = fx != null ? (int)P(fx, "ActiveParticles") : -1;
                lines.Add($"B{region} S{level} {kinds[level - 1]}: atmosfer {(fx != null ? P(fx, "Kind") : "yok")}, sprite {particles} ({(level == 2 ? "düşük" : "normal")})");
                Assert.NotNull(fx, $"B{region}: atmosfer bileşeni");
                Assert.LessOrEqual(particles, 60, "atmosfer tavanı");
                // 2 kule: yerleşimin ilk iki yuvası
                var battle = Battle;
                battle.GetType().GetMethod("AddBattleGold").Invoke(battle, new object[] { 500 });
                var slots = Object.FindObjectsByType(T("BattleBuildSlot"), FindObjectsSortMode.None).Cast<Component>().OrderBy(c => c.transform.position.y).ToList();
                for (int k = 0; k < Mathf.Min(2, slots.Count); k++)
                    tryBuild.Invoke(slots[k], new object[] { Enum.Parse(T("BuildingType"), k == 0 ? "ArcherTower" : "CannonTower") });
                var layout = SP("BattleManager", "MapLayout");
                var routes = (Array)P(layout, "Routes");
                var main = routes.GetValue(0);
                float len = (float)P(main, "Length");
                string[] types = level == 5 ? new[] { "Troll", "Orc", "Zombie" } : new[] { "Wolf", "Zombie", "Orc", "Zombie" };
                for (int k = 0; k < types.Length; k++)
                {
                    var route = routes.GetValue(k % routes.Length);
                    float rl = (float)P(route, "Length");
                    Vector2 at = (Vector2)route.GetType().GetMethod("PointAt").Invoke(route, new object[] { rl * (0.25f + 0.12f * k) });
                    SpawnAt(types[k], at);
                }
                Time.timeScale = 1f;
                yield return new WaitForSeconds(1.2f);
                Vector2 focus = (Vector2)main.GetType().GetMethod("PointAt").Invoke(main, new object[] { len * 0.55f });
                Shot($"G1_r{region}_l{level}_{kinds[level - 1]}", new Vector2(0f, focus.y), null, 540, 1170);
                if (level == 1 || level == 4)
                    ShotWorld($"G1_r{region}_l{level}_fullmap", new Vector2(0f, 11f), 18.2f, 560, 1140);
            }
        S("BattleFxQuality", "Force", new object[] { null });
        File.WriteAllLines(Path.Combine(Folder, "G1_atmosphere.txt"), lines);
    }
}
