#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Otomatik oyuncu (yalnız editör/Development). Bir sefer aşamasını "beklenen oyuncu" profiliyle oynar:
/// kahramanı en yakın düşmana sürer, canı azalınca üsse çekilir, boş yuvalara yapı kurar.
/// Sonuçları <see cref="Telemetry"/> olarak PlaytestOutput/ altına JSON yazar, kritik anlarda görüntü alır.
/// PlayMode testi <c>PlaytestBotTests</c> tarafından çalıştırılır.
/// </summary>
public static class PlaytestBot
{
    [Serializable]
    public class EnemyStat { public string type; public int spawned; public int killed; public float avgTtk; }

    [Serializable]
    public class Telemetry
    {
        public int seed, region, level, battleLevel, mainBaseLevel, heroLevel, towerLevel;
        public string result = "timeout";      // win / lose / timeout
        public string defeatReason = "";
        public float durationSec;
        public int wavesReached;
        public int heroDowns;
        public float heroDistance;      // kahramanın yürüdüğü toplam yol
        public float heroStuckSec;      // girdi varken ilerleyemediği süre
        public float heroDamageTaken;
        public float baseDamageTaken;
        public float baseHealthEnd;
        public int structuresBuilt;
        public int structuresDestroyed;
        public int goldEarned;
        public int enemiesSpawned, enemiesKilled;
        public List<EnemyStat> enemies = new List<EnemyStat>();
        public List<string> goldCurve = new List<string>(); // "t:altın" her 10 sn
        public List<string> screenshots = new List<string>();
        public List<string> errors = new List<string>();
    }

    public static string OutputDir =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlaytestOutput"));

    public static Telemetry Last { get; private set; }

    /// <summary>Bölge şartı + aşamaya göre "beklenen" Ana Üs seviyesi (orta zorluk varsayımı).</summary>
    public static int ExpectedBaseLevel(int region, int level)
    {
        var data = CampaignCatalog.Get(region);
        int required = data != null ? Mathf.Max(1, data.requiredBaseLevel) : 1;
        return Mathf.Clamp(required + (level - 1) / 2, 1, Progression.MaxLevel);
    }

    /// <summary>Profil: Ana Üs/kahraman = baseLevel, kuleler baseLevel-1 (açıksa).</summary>
    public static PlayerSaveData CreateProfile(int baseLevel)
    {
        var save = PlayerSaveData.CreateNew(WeaponType.Spear);
        save.hero.level = baseLevel;
        save.hero.ApplyLevelStats();
        save.apprenticeLevel = baseLevel;
        save.firstBattleTowerBuilt = true;
        save.gems = 0; // devam teklifi elmasla kabul edilemesin: bot yenilgiyi görsün
        foreach (var entry in save.buildings)
        {
            int unlock = Progression.FirstUnlockLevel(entry.buildingType);
            if (entry.buildingType == BuildingType.MainBase) entry.level = baseLevel;
            else if (baseLevel >= unlock) entry.level = Mathf.Max(1, baseLevel - 1);
            entry.isUnlocked = entry.buildingType == BuildingType.MainBase || baseLevel >= unlock;
        }
        save.placedBuildings.Clear();
        int slot = 0;
        foreach (var entry in save.buildings)
            if (entry.isUnlocked && entry.level > 0)
                save.placedBuildings.Add(new PlacedBuildingData
                    { slotId = "bot_" + slot++, buildingType = entry.buildingType.ToString(), level = entry.level });
        return save;
    }

    /// <summary>Bir aşamayı oynar. maxSeconds oyun zamanıdır; timeScale ile hızlandırılır.</summary>
    public static IEnumerator PlayBattle(int region, int level, float timeScale = 4f, float maxSeconds = 420f,
        bool capture = true, int seed = 0)
    {
        int baseLevel = ExpectedBaseLevel(region, level);
        if (seed != 0) UnityEngine.Random.InitState(seed);
        var t = new Telemetry { seed = seed, region = region, level = level, mainBaseLevel = baseLevel, heroLevel = baseLevel,
            towerLevel = Mathf.Max(1, baseLevel - 1) };
        Last = t;
        Application.logMessageReceived += OnLog;
        currentTelemetry = t;

        var gm = GameManager.Instance;
        if (gm == null) gm = new GameObject("BotGameManager").AddComponent<GameManager>();
        yield return null;
        gm.DevSetPlayerData(CreateProfile(baseLevel));
        BattleSession.Begin(region, level);
        t.battleLevel = BattleSession.BattleLevel;
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 40; i++) yield return null;

        var battle = BattleManager.Instance;
        var hero = UnityEngine.Object.FindFirstObjectByType<HeroController>();
        var mainBase = UnityEngine.Object.FindFirstObjectByType<MainBaseController>();
        if (battle == null || hero == null || mainBase == null)
        {
            t.result = "setup_failed";
            Finish(t);
            yield break;
        }
        foreach (var joystick in UnityEngine.Object.FindObjectsByType<VirtualJoystick>(FindObjectsSortMode.None))
            joystick.enabled = false;

        bool ended = false;
        battle.OnBattleEnded += won => { ended = true; t.result = won ? "win" : "lose"; };
        var builtSeen = new HashSet<BattleStructure>();
        var destroyedSeen = new HashSet<BattleStructure>();
        var spawnTime = new Dictionary<EnemyController, float>();
        var typeOf = new Dictionary<EnemyController, EnemyType>();
        var stats = new Dictionary<EnemyType, (int spawned, int killed, float ttk)>();
        float lastHeroHp = hero.CurrentHealth, lastBaseHp = mainBase.CurrentHealth;
        Vector3 home = mainBase.transform.position + new Vector3(0f, -1.6f, 0f);
        float start = Time.time, nextBuild = 0f, nextCurve = 0f, nextScan = 0f;
        bool midShot = false;
        Time.timeScale = timeScale;
        lastHeroPos = hero.transform.position;
        stuckFor = 0f; sidestepUntil = 0f;
        if (capture) t.screenshots.Add(Capture($"r{region}_l{level}_s{seed}_start"));

        while (!ended && Time.time - start < maxSeconds)
        {
            float now = Time.time - start;
            // Elmasla devam penceresi: bot kabul etmez → yenilgi say.
            if (Time.timeScale == 0f && battle.DefeatReason == "base")
            {
                t.result = "lose";
                t.defeatReason = "base";
                break;
            }

            if (now >= nextScan)
            {
                nextScan = now + 0.2f;
                foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (e.IsDead || spawnTime.ContainsKey(e)) continue;
                    spawnTime[e] = now;
                    typeOf[e] = e.Type;
                    var s = stats.TryGetValue(e.Type, out var v) ? v : (0, 0, 0f);
                    stats[e.Type] = (s.Item1 + 1, s.Item2, s.Item3);
                }
                foreach (var e in spawnTime.Keys.ToList())
                {
                    if (e != null && !e.IsDead) continue;
                    var type = typeOf[e];
                    var s = stats[type];
                    stats[type] = (s.spawned, s.killed + 1, s.ttk + (now - spawnTime[e]));
                    spawnTime.Remove(e);
                }
                foreach (var st in BattleStructure.All)
                {
                    if (st == null || st.Type == BuildingType.MainBase) continue;
                    builtSeen.Add(st);
                    if (st.IsDestroyed) destroyedSeen.Add(st);
                }
            }

            if (hero.CurrentHealth < lastHeroHp) t.heroDamageTaken += lastHeroHp - hero.CurrentHealth;
            lastHeroHp = hero.CurrentHealth;
            if (mainBase.CurrentHealth < lastBaseHp) t.baseDamageTaken += lastBaseHp - mainBase.CurrentHealth;
            lastBaseHp = mainBase.CurrentHealth;

            DriveHero(hero, home, t);
            if (now >= nextBuild) { nextBuild = now + 1f; TryBuildAll(battle); }
            if (now >= nextCurve) { nextCurve = now + 10f; t.goldCurve.Add($"{now:0}:{battle.GoldEarned}"); }
            if (capture && !midShot && battle.CurrentWave >= 2 && spawnTime.Count >= 4)
            {
                midShot = true;
                t.screenshots.Add(Capture($"r{region}_l{level}_s{seed}_fight"));
            }
            yield return null;
        }

        t.durationSec = Time.time - start;
        t.wavesReached = battle != null ? battle.CurrentWave : 0;
        t.heroDowns = hero != null ? hero.TimesDowned : 0;
        t.baseHealthEnd = mainBase != null ? mainBase.HealthPercent : 0f;
        t.goldEarned = battle != null ? battle.GoldEarned : 0;
        if (string.IsNullOrEmpty(t.defeatReason) && battle != null) t.defeatReason = battle.DefeatReason;
        t.structuresBuilt = builtSeen.Count;
        t.structuresDestroyed = destroyedSeen.Count;
        foreach (var pair in stats)
        {
            t.enemies.Add(new EnemyStat { type = pair.Key.ToString(), spawned = pair.Value.spawned,
                killed = pair.Value.killed, avgTtk = pair.Value.killed > 0 ? pair.Value.ttk / pair.Value.killed : 0f });
            t.enemiesSpawned += pair.Value.spawned;
            t.enemiesKilled += pair.Value.killed;
        }
        if (capture) t.screenshots.Add(Capture($"r{region}_l{level}_s{seed}_end_{t.result}"));
        Time.timeScale = 1f;
        Finish(t);
    }

    private static Telemetry currentTelemetry;

    private static void OnLog(string message, string stack, LogType type)
    {
        if (currentTelemetry == null || (type != LogType.Error && type != LogType.Exception)) return;
        if (currentTelemetry.errors.Count < 20) currentTelemetry.errors.Add(message.Length > 300 ? message.Substring(0, 300) : message);
    }

    private static void Finish(Telemetry t)
    {
        Application.logMessageReceived -= OnLog;
        currentTelemetry = null;
        Directory.CreateDirectory(OutputDir);
        File.WriteAllText(Path.Combine(OutputDir, $"battle_r{t.region}_l{t.level}_s{t.seed}.json"), JsonUtility.ToJson(t, true));
    }

    private static Vector3 lastHeroPos;
    private static float stuckFor, sidestepUntil;
    private static float sidestepSign = 1f;

    /// <summary>
    /// Basit oyuncu: canı %35 altındaysa üsse çekil; değilse üsse en yakın tehdide koş.
    /// İlerleyemezse (yapıya takıldıysa) 0,7 sn yana kayar.
    /// </summary>
    private static void DriveHero(HeroController hero, Vector3 home, Telemetry t)
    {
        if (hero == null || hero.IsDowned) { if (hero != null) hero.SetMoveInput(Vector2.zero); return; }
        Vector3 pos = hero.transform.position;
        float step = Vector2.Distance(pos, lastHeroPos);
        t.heroDistance += step;
        lastHeroPos = pos;

        Vector3 target = home;
        if (hero.HealthPercent >= 0.35f)
        {
            EnemyController best = null;
            float bestScore = float.MaxValue;
            foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (e.IsDead) continue;
                // Üsse yakın olanı ve kahramana yakın olanı birlikte gözet.
                float score = Vector2.Distance(e.transform.position, home) + Vector2.Distance(e.transform.position, pos);
                if (score < bestScore) { bestScore = score; best = e; }
            }
            if (best != null) target = best.transform.position;
        }
        Vector2 delta = target - pos;
        if (delta.magnitude < 0.9f) { hero.SetMoveInput(Vector2.zero); stuckFor = 0f; return; }
        Vector2 dir = delta.normalized;
        float dt = Time.deltaTime;
        if (step < 0.2f * dt * 2f) { stuckFor += dt; t.heroStuckSec += dt; } else stuckFor = 0f;
        if (stuckFor > 0.35f && Time.time > sidestepUntil)
        {
            sidestepUntil = Time.time + 0.7f;
            sidestepSign = -sidestepSign;
            stuckFor = 0f;
        }
        if (Time.time < sidestepUntil) dir = (new Vector2(-dir.y, dir.x) * sidestepSign + dir * 0.3f).normalized;
        hero.SetMoveInput(dir);
    }

    private static readonly System.Reflection.MethodInfo TryBuildMethod =
        typeof(BattleBuildSlot).GetMethod("TryBuild", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    /// <summary>Boş yuvalara sırayla Manastır (1 tane) → Topçu → Okçu kurmayı dener.</summary>
    private static void TryBuildAll(BattleManager battle)
    {
        bool hasMonastery = BattleStructure.All.Any(s => s != null && !s.IsDestroyed && s.Type == BuildingType.Monastery);
        foreach (var slot in UnityEngine.Object.FindObjectsByType<BattleBuildSlot>(FindObjectsSortMode.None))
        {
            if (slot.IsBuilt) continue;
            var order = hasMonastery
                ? new[] { BuildingType.CannonTower, BuildingType.ArcherTower }
                : new[] { BuildingType.Monastery, BuildingType.CannonTower, BuildingType.ArcherTower };
            foreach (var type in order)
            {
                if (battle.GetBattleBuildRemaining(type) <= 0 || !battle.CanBuildFromSlot(type, slot.CostFor(type))) continue;
                TryBuildMethod?.Invoke(slot, new object[] { type });
                if (slot.IsBuilt) { if (type == BuildingType.Monastery) hasMonastery = true; break; }
            }
        }
    }

    /// <summary>Ana kamerayı (HUD dahil değil) PNG'ye çizer; batchmode'da da çalışır.</summary>
    public static string Capture(string name)
    {
        try
        {
            var cam = Camera.main;
            if (cam == null) return "";
            int w = 540, h = 1170;
            var rt = new RenderTexture(w, h, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = old;
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.Combine(OutputDir, "shots"));
            string path = Path.Combine(OutputDir, "shots", name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PlaytestBot] Görüntü alınamadı: " + ex.Message);
            return "";
        }
    }
}
#endif
