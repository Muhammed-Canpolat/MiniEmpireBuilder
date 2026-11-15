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
/// 8. oturum: A = düşman yol durum makinesi (D96), E = düşman sayıları/ekonomi (D97), H = savaş üst barı (D98), O = Ana Üs yerleşim (D100).
/// </summary>
public class Session8Tests
{
    // Kullanıcının kaydı korunur (testler kendi kayıtlarını yazar).
    private static readonly string[] SaveKeys =
        { "MiniEmpireBuilder_SaveData", "MiniEmpireBuilder_SaveData_Backup", "MiniEmpireBuilder_SaveData_Corrupt" };
    private string[] savedPrefs;
    private string savedFile;
    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

    [NUnit.Framework.OneTimeSetUp]
    public void BackupSave()
    {
        savedPrefs = SaveKeys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
        savedFile = File.Exists(SaveFilePath) ? File.ReadAllText(SaveFilePath) : null;
    }

    [NUnit.Framework.OneTimeTearDown]
    public void RestoreSave()
    {
        Time.timeScale = 1f;
        for (int i = 0; i < SaveKeys.Length; i++)
        {
            if (savedPrefs[i] == null) PlayerPrefs.DeleteKey(SaveKeys[i]);
            else PlayerPrefs.SetString(SaveKeys[i], savedPrefs[i]);
        }
        PlayerPrefs.Save();
        if (savedFile != null) File.WriteAllText(SaveFilePath, savedFile);
        else if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
    }

    private static Type T(string name) => Session6Tests.T(name);
    private static object S(string type, string method, params object[] args) => Session6Tests.S(type, method, args);
    private static object SP(string type, string member) => Session6Tests.SP(type, member);
    private static object F(object o, string f) => Session6Tests.F(o, f);
    private static object P(object o, string p) => Session6Tests.P(o, p);
    private static object M(object o, string m, params object[] a) => Session6Tests.M(o, m, a);

    internal static string Folder
    {
        get
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session8"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static Component Battle => Object.FindFirstObjectByType(T("BattleManager")) as Component;

    private static Component SpawnAt(string type, Vector2 at)
    {
        var battle = Battle;
        return battle.GetType().GetMethod("SpawnVariantAt").Invoke(battle,
            new object[] { Enum.Parse(T("EnemyType"), type), new Vector3(at.x, at.y, 0f), Enum.Parse(T("EnemyVariant"), "None") }) as Component;
    }

    /// <summary>Doğal dalgaları durdur ve var olan düşmanları kaldır (ölçülen düşman tek kalsın).</summary>
    private static void SilenceWaves()
    {
        var battle = Battle as MonoBehaviour;
        battle.StopAllCoroutines();
        foreach (var e in Object.FindObjectsByType(T("EnemyController"), FindObjectsSortMode.None).Cast<Component>())
            Object.Destroy(e.gameObject);
        // Sanal joystick her kare kahraman girdisini kendi değeriyle ezer: test kahramanı sürerken kapalı.
        foreach (var j in Object.FindObjectsByType(T("VirtualJoystick"), FindObjectsSortMode.None).Cast<Behaviour>()) j.enabled = false;
        // Çırak ve Lancer'lar da kaldırılır: ölçülen düşman yalnız kahramana kilitlenebilsin.
        foreach (var name in new[] { "BattleApprentice", "AllyLancer" })
            foreach (var c in Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>())
                Object.Destroy(c.gameObject);
    }

    private static Vector2 RoutePoint(object route, float s) => (Vector2)route.GetType().GetMethod("PointAt").Invoke(route, new object[] { s });
    private static Vector2 RouteNormal(object route, float s) => (Vector2)route.GetType().GetMethod("NormalAt").Invoke(route, new object[] { s });
    private static Vector2 RouteTangent(object route, float s) => (Vector2)route.GetType().GetMethod("TangentAt").Invoke(route, new object[] { s });
    private static bool Walkable(Vector2 p) => (bool)S("BattleTerrain", "Walkable", p, 0.2f);

    /// <summary>Yolun hangi yanında daha geniş kara var (kahraman o yana kaçar).</summary>
    private static float LandSide(object route, float s)
    {
        int Count(float sign)
        {
            int n = 0;
            for (float k = 1.5f; k <= 7f; k += 0.5f) { if (Walkable(RoutePoint(route, s) + RouteNormal(route, s) * k * sign)) n++; else break; }
            return n;
        }
        return Count(1f) >= Count(-1f) ? 1f : -1f;
    }

    private sealed class Trace
    {
        public float LastS = -1f, MaxBack, MaxLateral, BackFrames;
        public bool Monotonic = true, AlwaysWalkable = true;
        public int Frames;
        public readonly List<string> States = new List<string>();
        public Vector2 Prev;
        public bool HasPrev;
    }

    /// <summary>Bir kare kaydı: ilerleme (LaneDistance) azalmasın, yol yönünde geri hareket olmasın, yürünemez yere girmesin.</summary>
    private static void Record(Trace t, Component enemy, object route)
    {
        float s = (float)P(enemy, "LaneDistance");
        string state = P(enemy, "LaneMode").ToString();
        Vector2 pos = enemy.transform.position;
        if (t.LastS >= 0f && s < t.LastS - 1e-4f) t.Monotonic = false;
        if (t.HasPrev)
        {
            float along = Vector2.Dot(pos - t.Prev, RouteTangent(route, s));
            if (along < 0f) { t.MaxBack = Mathf.Max(t.MaxBack, -along); if (along < -0.01f) t.BackFrames++; }
        }
        var args = new object[] { pos, 0f };
        route.GetType().GetMethod("Project").Invoke(route, args);
        t.MaxLateral = Mathf.Max(t.MaxLateral, (float)args[1]);
        if (!Walkable(pos)) t.AlwaysWalkable = false;
        if (t.States.Count == 0 || t.States[t.States.Count - 1] != state) t.States.Add(state);
        t.LastS = s; t.Prev = pos; t.HasPrev = true; t.Frames++;
    }

    private static IEnumerator DriveHero(Component hero, Func<Vector2> goal, float seconds, Trace t, Component enemy, object route, Func<bool> until = null)
    {
        var move = hero.GetType().GetMethod("SetMoveInput");
        float elapsed = 0f;
        while (elapsed < seconds && enemy != null)
        {
            Vector2 d = goal() - (Vector2)hero.transform.position;
            move.Invoke(hero, new object[] { d.magnitude > 0.2f ? d.normalized : Vector2.zero });
            Record(t, enemy, route);
            if (until != null && until()) break;
            elapsed += Time.deltaTime;
            yield return null;
        }
        move.Invoke(hero, new object[] { Vector2.zero });
    }

    /// <summary>
    /// A1: kahraman bir Kurt'u (Avcı) yoldan çeker, yol boyunca önde gezdirir, sonra (a) yandan uzaklaşır ya da (b) geriye kaçar.
    /// Her karede: ilerleme (yolda kat edilen mesafe) asla azalmaz; yol yönünde geri hareket yok; düşman hep yürünebilir yerde;
    /// takip bırakılınca İLERİDEN yola katılır (katıldığı ilerleme ≥ ayrıldığı ilerleme); bırakma mesafesinin dışına çıkmaz.
    /// 6 bölgenin 1. ve 3. seviyesinde.
    /// </summary>
    [UnityTest, Timeout(900000)]
    public IEnumerator A1_EnemyNeverWalksBackAndRejoinsAhead()
    {
        _ = Folder;
        var lines = new List<string>();
        var failures = new List<string>();
        string only = Environment.GetEnvironmentVariable("S8_REGIONS");
        int[] regionList = string.IsNullOrEmpty(only) ? new[] { 1, 2, 3, 4, 5, 6 } : only.Split(',').Select(int.Parse).ToArray();
        foreach (int region in regionList)
            foreach (int level in new[] { 1, 3 })
                foreach (string variant in new[] { "side", "back" })
                {
                    yield return Session4Tests.StartBattle(region, level, 15);
                    SilenceWaves();
                    yield return null;
                    var layout = SP("BattleManager", "MapLayout");
                    var route = ((Array)P(layout, "Routes")).GetValue(0);
                    float len = (float)P(route, "Length");
                    float s0 = len * 0.32f;
                    float side = LandSide(route, s0 + 1.5f);
                    var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
                    hero.transform.position = RoutePoint(route, s0 + 1.5f) + RouteNormal(route, s0 + 1.5f) * side * 1.9f;
                    var wolf = SpawnAt("Wolf", RoutePoint(route, s0 - 2.5f));
                    wolf.GetType().GetProperty("Invulnerable").SetValue(wolf, true);
                    wolf.GetType().GetProperty("RaidTarget").SetValue(wolf, null);   // Madeni Koru: bu kurt yağmacı olmasın
                    var t = new Trace();
                    float minHero = float.MaxValue;
                    Func<bool> watch = () => { minHero = Mathf.Min(minHero, Vector2.Distance(wolf.transform.position, hero.transform.position)); return P(wolf, "LaneMode").ToString() == "Fight"; };
                    // 1) Yem: kahraman yol kenarında bekler, kurt kilitlenip savaşa girer
                    yield return DriveHero(hero, () => hero.transform.position, 8f, t, wolf, route, watch);
                    bool engaged = P(wolf, "LaneMode").ToString() == "Fight";
                    string diag = $"kahraman {(Vector2)hero.transform.position}, can {(float)P(hero, "CurrentHealth"):0}, kurt {(Vector2)wolf.transform.position}, hedef {P(wolf, "CurrentTarget")}, en yakın {minHero:0.00}, kahraman sayısı {Object.FindObjectsByType(T("HeroController"), FindObjectsSortMode.None).Length}";
                    float leave = (float)P(wolf, "LastLeaveS");
                    // 2) Kahraman yol boyunca ileri yürür (kurt peşinden, ilerleme artar)
                    float heroS = s0 + 1.5f;
                    yield return DriveHero(hero, () => RoutePoint(route, heroS + 3.5f) + RouteNormal(route, heroS + 3.5f) * side * 1.9f, 1.6f, t, wolf, route);
                    float afterChase = (float)P(wolf, "LaneDistance");
                    // 3) Uzaklaş: (a) yoldan dik uzağa; (b) yolun gerisine
                    Vector2 far = variant == "side"
                        ? RoutePoint(route, heroS + 3.5f) + RouteNormal(route, heroS + 3.5f) * side * 9f
                        : RoutePoint(route, Mathf.Max(0f, s0 - 9f)) + RouteNormal(route, Mathf.Max(0f, s0 - 9f)) * side * 1.5f;
                    yield return DriveHero(hero, () => far, 4f, t, wolf, route, () => P(wolf, "LaneMode").ToString() == "Rejoin");
                    bool dropped = P(wolf, "LaneMode").ToString() == "Rejoin" || P(wolf, "LaneMode").ToString() == "FollowLane";
                    string how = $"koşarak (kahraman {(Vector2)hero.transform.position}, hedef nokta {far}, kurt {(Vector2)wolf.transform.position}, kurt hedefi {P(wolf, "CurrentTarget")}, durum {P(wolf, "LaneMode")})";
                    if (!dropped)
                    {
                        // Ada dar: kahraman kıyıya dayandıysa (bırakma mesafesinin dışına çıkamadı) Ana Üs'ün yanına alınır.
                        hero.transform.position = (Vector2)F(layout, "heroSpawn");
                        how = "ışınla; koşu sonu: " + how.Substring(8);
                    }
                    else
                    {
                        // Bırakıldı; ama kahraman kıyıya/engele takılıp yol kenarında kaldıysa düşman ileriden dönerken onu yeniden (haklı olarak)
                        // görür. Ölçülen "yola dönüş" olduğundan kahraman yoldan uzağa alınır.
                        var a2 = new object[] { (Vector2)hero.transform.position, 0f };
                        route.GetType().GetMethod("Project").Invoke(route, a2);
                        if ((float)a2[1] < (float)SP("EnemyController", "LeashDistance"))
                        {
                            hero.transform.position = (Vector2)F(layout, "heroSpawn");
                            how += " + kahraman yol kenarında kaldı → uzağa alındı";
                        }
                    }
                    // 4) Kahraman uzakta bekler; kurt ileriden yola katılıp yürür
                    yield return DriveHero(hero, () => hero.transform.position, 6f, t, wolf, route,
                        () => (int)P(wolf, "RejoinCount") > 0 && P(wolf, "LaneMode").ToString() == "FollowLane" && (float)P(wolf, "LaneDistance") > (float)P(wolf, "LastJoinS") + 1.5f);
                    float join = (float)P(wolf, "LastJoinS");
                    int rejoins = (int)P(wolf, "RejoinCount");
                    string line = $"B{region}-S{level} {variant}: kilit {engaged}, ayrıldı s={leave:0.0}, kovalama sonrası s={afterChase:0.0}, bırakma {how}, " +
                                  $"katıldı s={join:0.0} (dönüş {rejoins}), en çok geri adım {t.MaxBack:0.000} ({t.BackFrames} kare), yoldan en uzak {t.MaxLateral:0.0}, " +
                                  $"ilerleme hep arttı {t.Monotonic}, hep yürünebilir {t.AlwaysWalkable}, durumlar {string.Join(">", t.States)}";
                    lines.Add(line);
                    if (!engaged) failures.Add(line + " → kilitlenmedi: " + diag);
                    if (!t.Monotonic) failures.Add(line + " → ilerleme azaldı");
                    if (t.BackFrames > 0 || t.MaxBack > 0.02f) failures.Add(line + " → geri hareket");
                    if (!t.AlwaysWalkable) failures.Add(line + " → yürünemez yere girdi");
                    if (rejoins < 1 || join < leave) failures.Add(line + " → ileriden katılmadı");
                    if (t.MaxLateral > (float)SP("EnemyController", "LeashDistance") + 1f) failures.Add(line + " → bırakma mesafesini aştı");
                    if (P(wolf, "LaneMode").ToString() != "FollowLane") failures.Add(line + " → yola dönmedi: hedef " + P(wolf, "CurrentTarget") + " @" + (Vector2)((Transform)P(wolf, "CurrentTarget")).position + ", kurt " + (Vector2)wolf.transform.position + ", kahraman " + (Vector2)hero.transform.position);
                    if (region == 4 && level == 1 && variant == "side")
                        Session7Tests.ShotWorld("../session8/A1_rejoin_region4", wolf.transform.position, 6f, 540, 800);
                    Object.Destroy(wolf.gameObject);
                }
        File.WriteAllLines(Path.Combine(Folder, "A1_rejoin.txt"), lines.Concat(new[] { "", "HATALAR:" }).Concat(failures));
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    /// <summary>
    /// E1 (D97): 6 bölge × 5 seviye — savaşta doğan toplam düşman tabloyla aynı (CampaignEnemyConfig), hiçbir dalga üst sınırı aşmıyor,
    /// dalgalar eski dalga oranlarında (bölünmediyse ±1); düşman başına ölçeklenen öldürme altınının toplamı eski planın toplamıyla aynı (±1 yuvarlama).
    /// </summary>
    [Test]
    public void E1_EnemyCountsMatchTableAndKillGoldStaysTheSame()
    {
        _ = Folder;
        var table = new[,] { { 8, 14, 17, 19, 20 }, { 16, 20, 21, 24, 26 }, { 18, 25, 27, 30, 32 }, { 20, 27, 30, 33, 38 }, { 22, 32, 35, 38, 50 }, { 24, 35, 38, 40, 55 } };
        var config = SP("CampaignEnemyConfig", "Active");
        int maxPerWave = (int)F(config, "maxPerWave");
        var lines = new List<string> { "bölge-seviye | tür | eski doğan | yeni doğan | dalgalar | eski altın | yeni altın (ölçekli) | çarpan" };
        var errors = new List<string>();
        var bm = T("BattleManager");
        for (int region = 1; region <= 6; region++)
            for (int level = 1; level <= 5; level++)
            {
                S("BattleSession", "Begin", region, level);
                var regionData = SP("BattleSession", "Region");
                var levelData = SP("BattleSession", "Level");
                int battleLevel = (int)SP("BattleSession", "BattleLevel");
                var plan = S("BattleManager", "PlanFor", regionData, levelData, false);
                var legacy = S("BattleManager", "PlanFor", regionData, levelData, true);
                int planned = (int)S("BattleManager", "PlannedEnemies", plan);
                int legacyCount = (int)S("BattleManager", "PlannedEnemies", legacy);
                var waves = ((IList)F(plan, "waves")).Cast<object>().Select(w => ((IList)F(w, "enemies")).Cast<object>().Sum(e => (int)F(e, "count"))).ToList();
                int legacyGold = (int)S("BattleManager", "ExpectedKillGold", legacy, battleLevel);
                int newGold = (int)S("BattleManager", "ExpectedKillGold", plan, battleLevel);
                float scale = legacyGold / (float)newGold;
                // Oyundaki kesir taşımalı dağıtımın aynısı
                float carry = 0f; int paid = 0;
                foreach (var w in (IList)F(plan, "waves"))
                    foreach (var e in (IList)F(w, "enemies"))
                    {
                        var stats = S("EnemyStats", "GetBaseStats", F(e, "enemyType"));
                        stats.GetType().GetMethod("ScaleToLevel").Invoke(stats, new object[] { battleLevel });
                        int baseGold = (int)P(stats, "goldReward");
                        string variant = F(e, "variant").ToString();
                        if (variant != "None")
                        {
                            var counters = T("Progression").GetNestedType("Counters");
                            float mult = (float)counters.GetField(variant + "Gold").GetValue(null);
                            baseGold = Mathf.Max(1, Mathf.RoundToInt(baseGold * mult));
                        }
                        for (int k = 0; k < (int)F(e, "count"); k++) { carry += baseGold * scale; int g = Mathf.FloorToInt(carry + 1e-4f); carry -= g; paid += g; }
                    }
                string kind = F(levelData, "kind").ToString();
                lines.Add($"B{region}-S{level} | {kind} | {legacyCount} ({string.Join("+", ((IList)F(legacy, "waves")).Cast<object>().Select(w => ((IList)F(w, "enemies")).Cast<object>().Sum(e => (int)F(e, "count"))))}) | {planned} | {string.Join("+", waves)} | {legacyGold} | {paid} | {scale:0.000}");
                if (planned != table[region - 1, level - 1]) errors.Add($"B{region}-S{level}: toplam {planned} ≠ tablo {table[region - 1, level - 1]}");
                if (waves.Max() > maxPerWave) errors.Add($"B{region}-S{level}: dalga {waves.Max()} > {maxPerWave}");
                var legacyWaves = ((IList)F(legacy, "waves")).Cast<object>().Select(w => ((IList)F(w, "enemies")).Cast<object>().Sum(e => (int)F(e, "count"))).ToList();
                if (waves.Count == legacyWaves.Count)
                    for (int w = 0; w < waves.Count; w++)
                    {
                        float expected = planned * legacyWaves[w] / (float)legacyWaves.Sum();
                        if (Mathf.Abs(waves[w] - expected) > 1.01f) errors.Add($"B{region}-S{level}: dalga {w + 1} = {waves[w]}, eski orana göre {expected:0.0}");
                    }
                if (Mathf.Abs(paid - legacyGold) > 1) errors.Add($"B{region}-S{level}: öldürme altını {paid} ≠ eski {legacyGold}");
            }
        S("BattleSession", "Clear");
        File.WriteAllLines(Path.Combine(Folder, "E1_counts.txt"), lines.Concat(new[] { "", "HATALAR:" }).Concat(errors));
        Assert.IsEmpty(errors, string.Join("\n", errors));
    }

    /// <summary>
    /// P1 (D97): en kalabalık seviye (Ejderin İni boss, 55 düşman) — düşük kalitede 3 dalga × 18 düşman yolda, 4 kule ateş ederken kare süresi
    /// (ortalama / %95) ve yönetilen bellek artışı; ölen düşmanların kabuğu havuza döner ve sonraki dalgada yeniden kullanılır.
    /// Not: editör ölçümü masaüstü CPU'dadır; cihaz kare hızı değil, karşılaştırmalı yük göstergesidir.
    /// </summary>
    [UnityTest, Timeout(600000)]
    public IEnumerator P1_CrowdPerformanceAndEnemyPooling()
    {
        _ = Folder;
        S("BattleFxQuality", "Force", new object[] { (bool?)true });
        yield return Session4Tests.StartBattle(6, 5, 15);
        SilenceWaves();
        yield return null;
        var battle = Battle;
        var layout = SP("BattleManager", "MapLayout");
        var route = ((Array)P(layout, "Routes")).GetValue(0);
        float len = (float)P(route, "Length");
        battle.GetType().GetMethod("AddBattleGold").Invoke(battle, new object[] { 2000 });
        var tryBuild = T("BattleBuildSlot").GetMethod("TryBuild", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slots = Object.FindObjectsByType(T("BattleBuildSlot"), FindObjectsSortMode.None).Cast<Component>().ToList();
        for (int k = 0; k < Mathf.Min(4, slots.Count); k++)
            tryBuild.Invoke(slots[k], new object[] { Enum.Parse(T("BuildingType"), k % 2 == 0 ? "ArcherTower" : "CannonTower") });
        var frames = new List<float>();
        var stuck = new List<string>();
        long memStart = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        string[] types = { "Zombie", "Orc", "Wolf", "ShadowWolf" };   // Gölge Kurt: RequireComponent bağımlılığı (havuz ayıklama hatasını yakalar)
        for (int wave = 0; wave < 3; wave++)
        {
            var spawned = new List<Component>();
            for (int i = 0; i < 18; i++)
            {
                spawned.Add(SpawnAt(types[i % 4], RoutePoint(route, Mathf.Min(len - 2f, 0.5f + i * 0.7f))));
                frames.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }
            float t = 0f;
            while (t < 4f) { frames.Add(Time.unscaledDeltaTime * 1000f); t += Time.unscaledDeltaTime; yield return null; }
            foreach (var e in spawned)
            {
                if (e == null) continue;
                int controllers = e.GetComponents(T("EnemyController")).Length;
                if (controllers != 1) stuck.Add($"dalga {wave + 1} {e.name}: {controllers} EnemyController");
                else if (P(e, "CurrentTarget") == null) stuck.Add($"dalga {wave + 1} {e.name}: hedefsiz {(Vector2)e.transform.position}");
            }
            foreach (var e in spawned) if (e != null && !(bool)P(e, "IsDead")) M(e, "TakeDamage", 1e9f, Enum.Parse(T("DamageSource"), "Env"));
            yield return new WaitForSeconds(2.6f);   // ölüm efekti + havuz bekleme süresi
        }
        long memEnd = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        frames.Sort();
        float avg = frames.Average(), p95 = frames[(int)(frames.Count * 0.95f)];
        int created = (int)P(battle, "EnemyObjectsCreated"), reused = (int)P(battle, "EnemyObjectsReused");
        string line = $"B6 boss, düşük kalite: {frames.Count} kare, ortalama {avg:0.0} ms, %95 {p95:0.0} ms, en kötü {frames[frames.Count - 1]:0.0} ms; " +
                      $"bellek artışı {(memEnd - memStart) / 1024f:0} KB; düşman kabuğu yeni {created}, havuzdan {reused}, havuzda {P(battle, "EnemyPoolSize")}, ayıklama hatası {P(battle, "EnemyPoolStripFailures")}, takılan {stuck.Count}";
        File.WriteAllLines(Path.Combine(Folder, "P1_performance.txt"), new[] { line });
        S("BattleFxQuality", "Force", new object[] { null });
        Assert.AreEqual(0, (int)P(battle, "EnemyPoolStripFailures"), "her kabuk ayıklandı: " + line);
        Assert.IsEmpty(stuck, "havuzdan gelen düşmanlar tek controller'lı ve hedefli: " + string.Join("; ", stuck));
        Assert.Greater(reused, 20, "ölen düşmanların kabuğu sonraki dalgalarda yeniden kullanıldı: " + line);
        Assert.Less(p95, 50f, "kalabalıkta %95 kare süresi makul: " + line);
    }

    /// <summary>
    /// V4 (D99): 30 seviyenin her birinde gerçekten ÇİZİLEN yol (BattleMapBuilder.RoadCovers, rota matematiğinden bağımsız) — yuva halkası
    /// (yarıçap 0,95), altındaki "N HAK" etiketi ve kule görseli yola değmez; bölge yapılarının (kule/kışla/kale) tamamı kıyıdan içeride karada;
    /// kamera yatayda karanın dışına çıkmaz. Eski doğrulama (V1) yalnız rota uzaklığına bakıyordu ve etiketi hiç denetlemiyordu.
    /// </summary>
    [UnityTest, Timeout(1200000)]
    public IEnumerator V4_SlotsLabelsAndLandmarksStayOffTheDrawnRoadAndOnLand()
    {
        _ = Folder;
        var errors = new List<string>();
        var lines = new List<string>();
        for (int region = 1; region <= 6; region++)
            for (int level = 1; level <= 5; level++)
            {
                yield return Session4Tests.StartBattle(region, level, 15);
                var builder = Object.FindFirstObjectByType(T("BattleMapBuilder")) as Component;
                var covers = builder.GetType().GetMethod("RoadCovers");
                bool Road(Vector2 p) => (bool)covers.Invoke(builder, new object[] { p });
                var layout = SP("BattleManager", "MapLayout");
                int hits = 0;
                foreach (var slot in (Vector2[])F(layout, "slots"))
                {
                    var bad = new List<string>();
                    for (int a = 0; a < 24; a++)
                    {
                        float ang = a * Mathf.PI / 12f;
                        for (float r = 0.2f; r <= 0.95f; r += 0.25f)
                            if (Road(slot + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r)) { bad.Add("halka"); goto label; }
                    }
                    label:
                    for (float lx = -0.62f; lx <= 0.621f; lx += 0.155f)
                        for (float ly = -1.38f; ly <= -0.979f; ly += 0.1f)
                            if (Road(slot + new Vector2(lx, ly))) { bad.Add("etiket"); lx = 9f; break; }
                    for (float tx = -0.8f; tx <= 0.81f; tx += 0.2f)
                        for (float ty = -0.3f; ty <= 2.2f; ty += 0.25f)
                            if (Road(slot + new Vector2(tx, ty))) { bad.Add("kule görseli"); tx = 9f; break; }
                    if (bad.Count > 0) { hits++; errors.Add($"B{region}-S{level} yuva {slot}: çizilen yola değiyor ({string.Join(", ", bad.Distinct())})"); }
                }
                var landmarks = (IList)builder.GetType().GetField("Landmarks").GetValue(builder);
                foreach (GameObject lm in landmarks)
                {
                    var sr = lm.GetComponentInChildren<SpriteRenderer>();
                    var b = sr.bounds;
                    for (float x = b.min.x + b.size.x * 0.1f; x <= b.max.x - b.size.x * 0.1f + 0.01f; x += b.size.x * 0.2f)
                        foreach (float y in new[] { b.min.y + 0.05f, b.center.y, b.max.y - 0.05f })
                            if (!(bool)S("BattleTerrain", "IsLand", new Vector2(x, y))) { errors.Add($"B{region}-S{level} yapı {lm.name}: suya taşıyor ({x:0.0},{y:0.0})"); x = 99f; break; }
                }
                var bounds = (Rect)SP("BattleTerrain", "CameraBounds");
                lines.Add($"B{region}-S{level}: yuva {((Vector2[])F(layout, "slots")).Length} (değen {hits}), yapı {landmarks.Count}, kamera x [{bounds.xMin:0.0}, {bounds.xMax:0.0}]");
            }
        File.WriteAllLines(Path.Combine(Folder, "V4_drawn_road.txt"), lines.Concat(new[] { "", "HATALAR:" }).Concat(errors));
        Assert.IsEmpty(errors, string.Join("\n", errors));
    }

    /// <summary>M0 (görsel teşhis): Donmuş Geçit S3 telefon oranında savaş başı (kamera dokunulmadan) + haritanın 3 kat geniş çevresi.</summary>
    [UnityTest]
    public IEnumerator M0_DeviceLikeShotsOfFrozenPassLevel3()
    {
        _ = Folder;
        yield return Session4Tests.StartBattle(4, 3, 15);
        yield return new WaitForSeconds(1.5f);
        Session4Tests.CaptureUi("../session8/M0_r4_l3_phone", 654, 1424);
        Session7Tests.ShotWorld("../session8/M0_r4_l3_wide", new Vector2(0f, 13f), 26f, 900, 1200);
    }

    /// <summary>
    /// A2: kalabalık — 14 düşman aynı noktada doğar. 3 sn sonra çiftlerin örtüşmesi (gövde yarıçapları toplamının %60'ından yakın çift)
    /// azdır; hiçbiri geri gitmez; bakış yönü saniyede 2'den fazla dönmez (titreme yok).
    /// </summary>
    [UnityTest]
    public IEnumerator A2_CrowdSeparatesWithoutJitter()
    {
        _ = Folder;
        var lines = new List<string>();
        foreach (int region in new[] { 1, 4 })
        {
            yield return Session4Tests.StartBattle(region, 2, 15);
            SilenceWaves();
            yield return null;
            var layout = SP("BattleManager", "MapLayout");
            var route = ((Array)P(layout, "Routes")).GetValue(0);
            var enemies = new List<Component>();
            string[] types = { "Zombie", "Orc", "Wolf", "Zombie", "Zombie", "Orc", "Wolf" };
            for (int i = 0; i < 14; i++)
            {
                var e = SpawnAt(types[i % types.Length], RoutePoint(route, 2f));
                e.GetType().GetProperty("Invulnerable").SetValue(e, true);
                enemies.Add(e);
            }
            var flips = new int[enemies.Count];
            var lastFlip = new bool[enemies.Count];
            var lastS = new float[enemies.Count];
            bool monotonic = true;
            int worstOverlap = 0;
            float elapsed = 0f;
            while (elapsed < 6f)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var sr = enemies[i].GetComponent<SpriteRenderer>();
                    if (elapsed > 0.5f && sr.flipX != lastFlip[i]) flips[i]++;
                    lastFlip[i] = sr.flipX;
                    float s = (float)P(enemies[i], "LaneDistance");
                    if (s < lastS[i] - 1e-4f) monotonic = false;
                    lastS[i] = s;
                }
                if (elapsed > 3f)
                {
                    int overlap = 0;
                    for (int i = 0; i < enemies.Count; i++)
                        for (int j = i + 1; j < enemies.Count; j++)
                            if (Vector2.Distance(enemies[i].transform.position, enemies[j].transform.position) < 0.25f) overlap++;
                    worstOverlap = Mathf.Max(worstOverlap, overlap);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            float maxFlipRate = flips.Max() / 5.5f;
            foreach (var e in enemies)
            {
                var pa = new object[] { (Vector2)e.transform.position, 0f };
                route.GetType().GetMethod("Project").Invoke(route, pa);
                lines.Add($"   {e.name} {P(e, "LaneMode")} s={(float)P(e, "LaneDistance"):0.0} yoldan {(float)pa[1]:0.00} hedef {P(e, "CurrentTarget")}");
            }
            lines.Add($"B{region}: 14 düşman, en kötü örtüşen çift {worstOverlap}, en çok yön dönüşü {maxFlipRate:0.0}/sn, ilerleme hep arttı {monotonic}");
            File.WriteAllLines(Path.Combine(Folder, "A2_crowd.txt"), lines);
            Session7Tests.ShotWorld($"../session8/A2_crowd_region{region}", enemies[0].transform.position, 5f, 540, 700);
            Assert.IsTrue(monotonic, $"B{region}: kalabalıkta ilerleme azalmaz");
            Assert.LessOrEqual(worstOverlap, 3, $"B{region}: iç içe giren çift az");
            Assert.LessOrEqual(maxFlipRate, 2f, $"B{region}: titreme yok");
            foreach (var e in enemies) if (e != null) Object.Destroy(e.gameObject);
        }
        File.WriteAllLines(Path.Combine(Folder, "A2_crowd.txt"), lines);
    }
}
