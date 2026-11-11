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
        public string profile = "good";
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
        public List<string> stallDump = new List<string>(); // kilitlenmede canlı düşmanlar
        public float heroDamageShare;   // kahraman + Çırak hasarının toplam hasara oranı
        public List<string> damageBySource = new List<string>();
        public int wardBreaks;          // boss kalkanı kaç kez kırıldı
        public List<string> saboteurs = new List<string>(); // hero/tower/detonated sayıları
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
        bool capture = true, int seed = 0, bool casual = false, string strategy = "combined")
    {
        int baseLevel = ExpectedBaseLevel(region, level);
        if (seed != 0) UnityEngine.Random.InitState(seed);
        Casual = casual;
        Strategy = strategy ?? "combined";
        casualPickAt = 0f; casualTarget = null;
        var t = new Telemetry { profile = casual ? "casual" : Strategy == "combined" ? "good" : Strategy, seed = seed, region = region, level = level, mainBaseLevel = baseLevel, heroLevel = baseLevel,
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
        int maxWardBreaks = 0;
        int lastKilled = 0; float lastBaseDmg = 0f, lastProgress = 0f;
        retreatSince = -1f;
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
                foreach (var boss in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    if (boss != null && boss.WardBreaks > maxWardBreaks) maxWardBreaks = boss.WardBreaks;
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

            DriveHero(hero, home, mainBase.transform.position, t, battle.WaveEdges);
            if (now >= nextBuild) { nextBuild = now + 1f; TryBuildAll(battle); }
            if (now >= nextCurve) { nextCurve = now + 10f; t.goldCurve.Add($"{now:0}:{battle.GoldEarned}"); }
            // Kilitlenme: düşman var ama 60 sn boyunca ne ölen var ne üs hasar alıyor.
            int alive = spawnTime.Count;
            int killedNow = stats.Values.Sum(v => v.killed);
            if (killedNow != lastKilled || t.baseDamageTaken != lastBaseDmg || alive == 0)
            { lastKilled = killedNow; lastBaseDmg = t.baseDamageTaken; lastProgress = now; }
            else if (now - lastProgress > 60f)
            {
                t.result = "stall";
                foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    if (!e.IsDead)
                        t.stallDump.Add($"{e.Type} pos={(Vector2)e.transform.position} role={e.EnemyRole} target={(e.CurrentTarget != null ? e.CurrentTarget.name : "null")} " +
                            $"attackPoint={e.AttackPoint} dist={Vector2.Distance(e.transform.position, e.AttackPoint):0.00} hero={(Vector2)hero.transform.position}");
                if (capture) t.screenshots.Add(Capture($"r{region}_l{level}_s{seed}_stall"));
                break;
            }
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
        float totalDamage = 0f, heroDamage = 0f;
        foreach (var pair in EnemyController.DamageBySource)
        {
            totalDamage += pair.Value;
            if (pair.Key == DamageSource.Hero || pair.Key == DamageSource.Apprentice) heroDamage += pair.Value;
            t.damageBySource.Add($"{pair.Key}:{pair.Value:0}");
        }
        t.heroDamageShare = totalDamage > 0f ? heroDamage / totalDamage : 0f;
        foreach (var pair in EnemyController.SaboteurOutcomes) t.saboteurs.Add($"{pair.Key}:{pair.Value}");
        t.wardBreaks = maxWardBreaks;
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
        File.WriteAllText(Path.Combine(OutputDir, $"battle_r{t.region}_l{t.level}_s{t.seed}{(t.profile == "good" ? "" : "_" + t.profile)}.json"), JsonUtility.ToJson(t, true));
    }

    private static Vector3 lastHeroPos;
    private static float stuckFor, sidestepUntil, retreatSince = -1f;
    /// <summary>
    /// Sıradan oyuncu profili: uyarı oklarını kullanmaz, kahramana en yakın düşmana gider, hedefini saniyede bir
    /// günceller, en fazla 1 Okçu kulesi kurar. Orta zorluk iki profil arasında olmalı.
    /// </summary>
    private static bool Casual;
    /// <summary>Strateji profili: combined (kule+kahraman), towerOnly (kahraman kıpırdamaz), heroOnly (yapı yok).</summary>
    private static string Strategy = "combined";
    private static float casualPickAt;
    private static EnemyController casualTarget;
    private static float sidestepSign = 1f;

    /// <summary>
    /// Basit oyuncu: canı %35 altındaysa üsse çekil; değilse üsse en yakın tehdide koş.
    /// İlerleyemezse (yapıya takıldıysa) 0,7 sn yana kayar.
    /// </summary>
    private static void DriveHero(HeroController hero, Vector3 home, Vector3 baseCenter, Telemetry t, int[] edges)
    {
        if (hero == null || hero.IsDowned) { if (hero != null) hero.SetMoveInput(Vector2.zero); return; }
        // Yalnız kule stratejisi: kahraman yerinden kıpırdamaz (yalnız menziline gireni kendiliğinden vurur).
        if (Strategy == "towerOnly") { hero.SetMoveInput(Vector2.zero); return; }
        Vector3 pos = hero.transform.position;
        float step = Vector2.Distance(pos, lastHeroPos);
        t.heroDistance += step;
        lastHeroPos = pos;

        // Düşman yokken duyurulan tarafa doğru (üsten 3 birim) konumlan: oyuncu uyarı oklarını kullanır.
        Vector3 target = home;
        if (!Casual && edges != null && edges.Length > 0)
        {
            int e = edges[0];
            Vector3 side = e == 0 ? Vector3.up : e == 1 ? Vector3.right : e == 2 ? Vector3.down : Vector3.left;
            target = home + new Vector3(0f, 1.6f, 0f) + side * 3.2f;
        }
        // Geri çekilmek yalnız iyileştiren Manastır varsa anlamlı; yoksa savaşmaya devam (düşerse 8 sn'de kalkar).
        bool canHeal = BattleStructure.All.Any(st => st != null && !st.IsDestroyed && st.Type == BuildingType.Monastery);
        // En fazla 6 sn çekil (Manastır menzili dışındaysa iyileşmeden beklemesin).
        bool retreating = false;
        if (canHeal && hero.HealthPercent < 0.35f)
        {
            if (retreatSince < 0f) retreatSince = Time.time;
            retreating = Time.time - retreatSince < 6f;
        }
        else retreatSince = -1f;
        if (retreating) target = home;
        else if (Casual)
        {
            if (Time.time >= casualPickAt || casualTarget == null || casualTarget.IsDead)
            {
                casualPickAt = Time.time + 1f;
                casualTarget = null;
                float bestDistance = float.MaxValue;
                foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (e.IsDead) continue;
                    float d = Vector2.Distance(e.transform.position, pos);
                    if (d < bestDistance) { bestDistance = d; casualTarget = e; }
                }
            }
            if (casualTarget != null && !casualTarget.IsDead) target = casualTarget.transform.position;
        }
        else
        {
            EnemyController best = null;
            float bestScore = float.MaxValue;
            foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (e.IsDead) continue;
                // Üsse yakın olanı ve kahramana yakın olanı birlikte gözet.
                float score = Vector2.Distance(e.transform.position, home) + Vector2.Distance(e.transform.position, pos);
                // İyi oyuncu: sabotajcıyı ve kalkanı açık boss'u önce yakalar (yalnız kahraman durdurabilir).
                if (e.Variant == EnemyVariant.Saboteur) score -= 12f;
                else if (e.WardActive) score -= 6f;
                if (score < bestScore) { bestScore = score; best = e; }
            }
            if (best != null) target = best.transform.position;
        }
        // Hedefe düz yol Ana Üs'ün içinden geçiyorsa önce üssün yanından dolaş (oyuncu da böyle yürür).
        Vector2 toTarget = target - pos;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            Vector2 a = pos, b = target, c = baseCenter;
            float u = Mathf.Clamp01(Vector2.Dot(c - a, b - a) / (b - a).sqrMagnitude);
            if (Vector2.Distance(a + (b - a) * u, c) < 2.4f && u > 0.05f && u < 0.95f)
            {
                // Üssün çevresinde hedefin tarafına doğru 70° ilerleyen ara nokta (aynı noktada kilitlenmez).
                Vector2 fromBase = (a - c).sqrMagnitude > 0.01f ? (a - c).normalized : Vector2.down;
                float sign = Mathf.Sign(fromBase.x * (b - c).y - fromBase.y * (b - c).x);
                if (sign == 0f) sign = 1f;
                float rad = 70f * Mathf.Deg2Rad * sign;
                Vector2 around = new Vector2(fromBase.x * Mathf.Cos(rad) - fromBase.y * Mathf.Sin(rad),
                    fromBase.x * Mathf.Sin(rad) + fromBase.y * Mathf.Cos(rad));
                target = (Vector3)(c + around * 3.8f);
            }
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
        if (Strategy == "heroOnly") return; // yalnız kahraman: hiç yapı kurulmaz
        bool hasMonastery = BattleStructure.All.Any(s => s != null && !s.IsDestroyed && s.Type == BuildingType.Monastery);
        if (Casual)
        {
            if (BattleStructure.All.Any(s => s != null && !s.IsDestroyed && s.Type != BuildingType.MainBase)) return;
            foreach (var slot in UnityEngine.Object.FindObjectsByType<BattleBuildSlot>(FindObjectsSortMode.None))
            {
                if (slot.IsBuilt || !battle.CanBuildFromSlot(BuildingType.ArcherTower, slot.CostFor(BuildingType.ArcherTower))) continue;
                TryBuildMethod?.Invoke(slot, new object[] { BuildingType.ArcherTower });
                if (slot.IsBuilt) return;
            }
            return;
        }
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
