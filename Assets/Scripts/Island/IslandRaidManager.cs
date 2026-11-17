using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Akın yöneticisi (island_base.md §3 ve §5): dalgalar tekne → iniş → yürüyüş evrelerinden geçer; tekneler 2,5 sn arayla
/// doğar, dalgalar arası 8 sn mola. Kazanınca ödül (Et/Odun ada cüzdanına, altın/elmas Ana Üs'e) verilir.
/// Savaş altını ve savaş içi inşaat yoktur. Okçunun düşmesi yenilgi değildir; yalnız Kıyı Kalesi canı 0 olunca yenilgi.
/// </summary>
public sealed class IslandRaidManager : MonoBehaviour
{
    public enum State { Idle, Running, Won, Lost }

    public State Phase { get; private set; } = State.Idle;
    public int Akin { get; private set; }
    public int Wave { get; private set; }
    public int TotalWaves { get; private set; }
    /// <summary>Faz D: bu savaş zamanlı baskın savunması (güç = en yüksek Akın − 2, 2 dalga, tek hedef kumsal; Akın ödülü/merdiveni değişmez).</summary>
    public bool IsRaid { get; private set; }
    public int RaidBeach { get; private set; }
    public IslandRaidScheduler.Outcome LastRaidOutcome { get; private set; }
    public int BattleLevel { get; private set; }
    public float BreakLeft { get; private set; }
    public IslandSession.Reward LastReward { get; private set; }
    public string DefeatReason { get; private set; } = "";
    /// <summary>Yenilgi etiketi (§7.6): telemetri anahtarı; açıklanamayan yenilgi için <see cref="IslandDefeat.Unexplained"/>.</summary>
    public string DefeatTag { get; private set; } = "";
    public float Elapsed { get; private set; }
    /// <summary>Bu zafer öğreticiyi bitirdi (HUD "Ağıl" ipucunu gösterir).</summary>
    public bool TutorialFinishedThisRun { get; private set; }
    public BattleStructure Castle => castle;
    public IslandArcher Archer => archer;
    public float CastleHealthPercent => castle != null ? castle.HealthPercent : 0f;
    /// <summary>Öğretici adımı (0 başlamadı … 4 bitti); kayıttan okunur.</summary>
    public int TutorialStep => IslandSession.Save != null ? IslandSession.Save.tutorialStep : 4;

    public event Action<State> StateChanged;
    public event Action<string> HintChanged;
    public event Action TutorialChanged;

    private BattleStructure castle;
    private IslandArcher archer;
    private Coroutine run;
    private int spawnedBoats, finishedBoats;
    private RaidBoat bossBoat;
    private bool bossSpawned;
    private bool tutorialRun;
    private int pendingFlyers;
    /// <summary>D120: ada akın tanıtımı (HARİTA/TAKİP, atış yeri, ATEŞ, dolu atış, yetenekler). Bir kez gösterilir, kayda yazılır.</summary>
    public readonly RaidIntro Intro = new RaidIntro();
    private readonly HashSet<int> ratThieves = new HashSet<int>();

    public void Bind(BattleStructure castleStructure, IslandArcher islandArcher)
    {
        castle = castleStructure;
        archer = islandArcher;
        if (castle != null) castle.Destroyed += OnCastleDestroyed;
        if (archer != null) { archer.PostChanged += OnPostChanged; archer.Shot += OnArcherShot; }
        Intro.Persist = () => GameManager.Instance?.SaveGame();
    }

    private void OnDestroy()
    {
        IslandRaidScheduler.LiveFightActive = false;
        IslandEnemy.StatScale = 1f;
        if (castle != null) castle.Destroyed -= OnCastleDestroyed;
        if (archer != null) { archer.PostChanged -= OnPostChanged; archer.Shot -= OnArcherShot; }
    }

    private void Update()
    {
        if (Phase == State.Running) Elapsed += Time.deltaTime;
        TickIntro(Time.deltaTime);
    }

    private void BeginIntro()
    {
        Intro.Begin(IslandSession.Save, PlayerPrefs.GetInt(IslandSceneSetup.PostHintKey, 0) == 1);
    }

    private void OnArcherShot(bool charged) { if (archer == null || !archer.LastShotAuto) Intro.OnShot(charged); }

    /// <summary>Tanıtım bağlamını kurup adımı ilerletir (Update çağırır; testler dt vererek de çağırır).</summary>
    public void TickIntro(float dt)
    {
        var c = new RaidIntro.Context { Running = Phase == State.Running, ArcherAlive = archer == null || archer.IsAlive };
        if (c.Running)
        {
            c.BoatSeen = spawnedBoats > 0;
            c.PostStage = spawnedBoats >= 2 || Wave >= 2;
            var cam = IslandSceneSetup.Instance != null ? IslandSceneSetup.Instance.CameraController : null;
            c.FullFrame = cam != null && cam.FullFrame;
            if (archer != null)
                foreach (var e in IslandEnemy.All)
                    if (e != null && e.Alive && Vector2.Distance(e.Position, archer.Position) <= archer.Range) { c.TargetInRange = true; break; }
            var ab = IslandAbilities.Instance;
            if (ab != null)
                for (int i = 0; i < 3; i++) if (ab.Unlocked((IslandAbilities.Kind)i)) c.UnlockedAbilities |= 1 << i;
        }
        Intro.Tick(dt, c);
    }

    // ==================== AKIN BAŞLAT ====================

    /// <summary>Akın N'yi başlatır (kapı denetimi çağıranda: <see cref="CanStart"/>). Çalışıyorsa false.</summary>
    public bool StartAkin(int akin)
    {
        if (Phase == State.Running) return false;
        IsRaid = false;
        IslandRaidScheduler.LiveFightActive = false;
        Akin = Mathf.Max(1, akin);
        BattleLevel = Progression.Island.AkinBattleLevel(Akin);
        IslandEnemy.StatScale = IslandEconomy.AkinScale(IslandSession.Save?.placed, IslandSession.KeepLevel());
        TotalWaves = Progression.Island.AkinWaves(Akin);
        Wave = 0;
        Elapsed = 0f;
        spawnedBoats = finishedBoats = 0;
        bossBoat = null; bossSpawned = false;
        DefeatReason = ""; DefeatTag = "";
        pendingFlyers = 0;
        TutorialFinishedThisRun = false;
        IslandStats.Reset();
        tutorialRun = Akin == 1 && IslandSession.Save != null && IslandSession.Save.tutorialStep < 3;
        if (tutorialRun && IslandSession.Save.tutorialStep == 0) SetTutorial(1);
        BeginIntro();
        if (IslandSession.Save != null) IslandSession.Save.lastAkin = Akin;
        Phase = State.Running;
        StateChanged?.Invoke(Phase);
        run = StartCoroutine(RunAkin());
        return true;
    }

    /// <summary>
    /// Zamanlı baskını canlı savunur (§7.3/§7.8): pencere açıkken (T0..T0+2 sa) çağrılır. Güç = en yüksek Akın − 2, 2 dalga, tüm tekneler
    /// hedef kumsala. Sonuç <see cref="IslandRaidScheduler.CompleteLive"/> ile işlenir; Akın ilerlemesi ve Akın ödülü değişmez.
    /// </summary>
    public bool StartRaid()
    {
        var save = IslandSession.Save;
        if (Phase == State.Running || save == null) return false;
        if (IslandRaidScheduler.PhaseAt(save, IslandRepair.Now) != IslandRaidScheduler.Phase.Live) return false;
        IsRaid = true;
        RaidBeach = Mathf.Clamp(save.raidBeach, 0, IslandLayout.Beaches.Length - 1);
        Akin = Progression.Island.RaidPower(save.highestAkin);
        BattleLevel = Progression.Island.AkinBattleLevel(Akin);
        IslandEnemy.StatScale = 1f;   // zamanlı baskın: D/R formülü korunur (kuleler D'ye zaten katılır)
        TotalWaves = Progression.Island.RaidWaves;
        Wave = 0;
        Elapsed = 0f;
        spawnedBoats = finishedBoats = 0;
        bossBoat = null; bossSpawned = false;
        DefeatReason = ""; DefeatTag = "";
        pendingFlyers = 0;
        ratThieves.Clear();
        TutorialFinishedThisRun = false;
        tutorialRun = false;
        BeginIntro();
        IslandStats.Reset();
        IslandRaidScheduler.LiveFightActive = true;
        Phase = State.Running;
        StateChanged?.Invoke(Phase);
        run = StartCoroutine(RunAkin());
        return true;
    }

    /// <summary>Canlı baskında kumsala çıkan Fare üretim binasına vurdu: bu Fare bir kez %2 çalar (toplam TheftCap'e bağlı).</summary>
    public void NoteRatTheft(EnemyController enemy)
    {
        if (IsRaid && Phase == State.Running && enemy != null) ratThieves.Add(enemy.GetInstanceID());
    }

    public bool CanStart(int akin)
    {
        var save = IslandSession.Save;
        return Progression.Island.CanStartAkin(akin, IslandSession.KeepLevel(), save != null ? save.highestAkin : 0);
    }

    private IEnumerator RunAkin()
    {
        for (int wave = 1; wave <= TotalWaves; wave++)
        {
            Wave = wave;
            if (BattleVfxManager.Instance != null) BattleVfxManager.Instance.ShowWaveBanner(wave);
            if (wave == 1 && IslandWaves.NewTypeAt(Akin) is EnemyType fresh && BattleVfxManager.Instance != null)
                BattleVfxManager.Instance.ShowNoticeBanner(L.T($"YENİ DÜŞMAN: {EnemyTypeInfo.DisplayName(fresh)}", $"NEW ENEMY: {EnemyTypeInfo.DisplayName(fresh)}"));
            yield return SpawnWave(wave);
            // Dalga bitene kadar: tekneler batar/boşalır, inen düşmanlar ölür.
            yield return WaitWaveCleared();
            if (Phase != State.Running) yield break;
            if (wave < TotalWaves)
            {
                BreakLeft = Progression.Island.WaveBreak;
                if (BattleVfxManager.Instance != null)
                    BattleVfxManager.Instance.ShowNoticeBanner(L.T("Sonraki dalga", "Next wave"));
                while (BreakLeft > 0f && Phase == State.Running) { BreakLeft -= Time.deltaTime; yield return null; }
            }
        }
        if (Phase != State.Running) yield break;
        Win();
    }

    private IEnumerator SpawnWave(int wave)
    {
        int boats = Progression.Island.AkinBoats(Akin);
        if (tutorialRun && Akin == 1) boats = 2;
        int openBeaches = Mathf.Min(Progression.Island.BeachesOpen(Akin), IslandLayout.Beaches.Length);
        bool lastWave = wave == TotalWaves;
        bool dragonWave = !IsRaid && lastWave && Progression.Island.IsBossAkin(Akin) && Progression.Island.BossFor(Akin) == EnemyType.RedDragon;
        if (IslandWaves.FlyerSwarmSize(Akin, wave) > 0 || dragonWave) { pendingFlyers++; StartCoroutine(SpawnFlyers(wave, dragonWave)); }
        for (int i = 0; i < boats; i++)
        {
            if (Phase != State.Running) yield break;
            int beach = IsRaid ? RaidBeach : (i + wave - 1) % openBeaches;
            // Öğretici: ikinci tekne doğu kumsalından gelir (atış yeri değiştirmeyi öğretir).
            if (tutorialRun && Akin == 1 && i == 1) beach = 1;
            var kind = BoatKindFor(i);
            bool isBossBoat = !IsRaid && Progression.Island.IsBossAkin(Akin) && lastWave && i == boats - 1;   // baskında boss yok (RaidPower boss Akın'ına düşmez)
            if (isBossBoat) kind = Progression.Island.BoatKind.Barge;
            var crew = IslandWaves.CrewFor(kind, i, isBossBoat, Akin, wave);
            float speedMult = tutorialRun && Akin == 1 && i == 0 ? Progression.Island.TutorialBoatSpeedMult : 1f;
            var boat = RaidBoat.Spawn(kind, BattleLevel, beach, crew, speedMult);
            if (isBossBoat) { bossBoat = boat; bossSpawned = false; }
            spawnedBoats++;
            IslandStats.CrewTotal += crew.Count;
            boat.Sunk += OnBoatSunk;
            boat.Finished += OnBoatFinished;
            boat.Disembarked += OnDisembarked;
            if (i < boats - 1) yield return new WaitForSeconds(Progression.Island.BoatStagger);
        }
    }

    private Progression.Island.BoatKind BoatKindFor(int index)
    {
        if (Akin >= Progression.Island.BargeFromAkin && index % 3 == 2) return Progression.Island.BoatKind.Barge;
        if (Akin >= Progression.Island.LongboatFromAkin && index % 2 == 1) return Progression.Island.BoatKind.Longboat;
        return Progression.Island.BoatKind.Rowboat;
    }

    /// <summary>Uçanlar teknesiz gelir (§7.1): kenardan 3–5'lik sürü, 2 sn önce yön okuyla haber verilir.</summary>
    private IEnumerator SpawnFlyers(int wave, bool dragon = false)
    {
        int size = IslandWaves.FlyerSwarmSize(Akin, wave);
        Vector2 edge = IslandWaves.FlyerEdgePoint(Akin + wave);
        Vector2 inward = (IslandLayout.CastleCenter - edge).normalized;
        float telegraph = Progression.Island.FlyerTelegraph;
        IslandFx.Telegraph(edge, inward, telegraph);
        float until = Time.time + telegraph;
        while (Time.time < until) { if (Phase != State.Running) { pendingFlyers--; yield break; } yield return null; }
        for (int k = 0; k < size; k++)
        {
            if (Phase != State.Running) break;
            Vector2 at = edge + new Vector2(Mathf.Abs(inward.x) > Mathf.Abs(inward.y) ? 0f : (k - size * 0.5f) * 0.9f,
                Mathf.Abs(inward.x) > Mathf.Abs(inward.y) ? (k - size * 0.5f) * 0.9f : 0f);
            IslandEnemy.Spawn(IslandWaves.FlyerTypeAt(Akin, k), BattleLevel, castle != null ? castle.transform : null, at, new Vector2[0], -1, false, false);
            IslandStats.CrewTotal++; IslandStats.Landed++;
            yield return new WaitForSeconds(0.2f);
        }
        if (dragon && Phase == State.Running)
        {
            // Akın 30 bossu: Kızıl Ejder teknesiz, kenardan gelir (uçan boss)
            IslandEnemy.Spawn(EnemyType.RedDragon, BattleLevel, castle != null ? castle.transform : null, edge + inward * 0.5f, new Vector2[0], -1, true, false);
            IslandStats.CrewTotal++; IslandStats.Landed++;
            bossSpawned = true;
        }
        pendingFlyers--;
    }

    // ==================== OLAYLAR ====================

    private void OnDisembarked(RaidBoat boat, EnemyType type, Vector2 landing)
    {
        if (Phase != State.Running) return;
        var beach = IslandLayout.Beaches[boat.BeachIndex];
        bool boss = Progression.Island.IsBossType(type);
        if (boss) bossSpawned = true;
        var route = (Vector2[])beach.walkPath.Clone();
        IslandEnemy.Spawn(type, BattleLevel, castle != null ? castle.transform : null,
            landing, route, boat.BeachIndex, boss, true);
        IslandStats.Landed++;
    }

    private void OnBoatSunk(RaidBoat boat, int drowned, int goldValue)
    {
        finishedBoats++;
        IslandStats.Drowned += drowned;
        IslandStats.DrownedGold += goldValue;
        if (tutorialRun && TutorialStep == 1)
        {
            SetTutorial(2);
            HintChanged?.Invoke(L.T("İçindekiler boğuldu!", "They all drowned!"));
        }
    }

    private void OnBoatFinished(RaidBoat boat)
    {
        finishedBoats++;
        if (tutorialRun && TutorialStep == 1) SetTutorial(2);
    }

    private void OnPostChanged(int post)
    {
        if (tutorialRun && TutorialStep == 2) SetTutorial(3);   // D120: herhangi atış yerine geçiş
        Intro.OnPostChanged();
    }

    /// <summary>D116: dalga güvenlik ağının kaç kez devreye girdiği (1. kademe: kaleye zorla; 2. kademe: dalgayı kapat). Test/telemetri.</summary>
    public int StallInterventions { get; private set; }

    /// <summary>
    /// D116: dalga bitene kadar bekler; son güvenlik ağı. Tekneler bitip uçan sürüsü doğduktan sonra <see cref="Progression.Island.WaveStallSeconds"/>
    /// boyunca hiçbir ilerleme (düşman sayısı/canı/konumu, kale canı) yoksa kalan düşmanlar kaleye zorlanır (<see cref="EnemyController.ForceFallback"/>);
    /// iki katında hâlâ yoksa kalanlar düşürülür → hiçbir akın "bitmeyen dalga"da kalmaz.
    /// </summary>
    private IEnumerator WaitWaveCleared()
    {
        float stall = 0f, nextSample = 0f;
        Vector4 last = new Vector4(float.NaN, 0f, 0f, 0f);
        bool forced = false;
        while (Phase == State.Running && !WaveCleared())
        {
            bool spawning = pendingFlyers > 0;
            foreach (var boat in RaidBoat.All) if (boat != null && boat.Alive) { spawning = true; break; }
            if (spawning) { stall = 0f; forced = false; }
            else if (Time.time >= nextSample)
            {
                nextSample = Time.time + 1f;
                var now = ActivitySample();
                bool active = float.IsNaN(last.x) || Mathf.Abs(now.x - last.x) > 0.01f || Mathf.Abs(now.y - last.y) > 0.01f
                    || Mathf.Abs(now.z - last.z) > 0.01f || Mathf.Abs(now.w - last.w) > 0.2f;
                last = now;
                if (active) { stall = 0f; forced = false; }
            }
            if (!spawning) stall += Time.deltaTime;
            if (!forced && stall >= Progression.Island.WaveStallSeconds)
            {
                forced = true;
                StallInterventions++;
                Debug.LogWarning($"[Island] Dalga {Wave} ilerlemiyor: kalan düşmanlar kaleye yönlendirildi (D116).");
                foreach (var enemy in IslandEnemy.All) if (enemy != null && enemy.Alive) enemy.Controller.ForceFallback();
            }
            else if (stall >= 2f * Progression.Island.WaveStallSeconds)
            {
                StallInterventions++;
                Debug.LogWarning($"[Island] Dalga {Wave} hâlâ ilerlemiyor: kalan düşmanlar düşürüldü (D116).");
                foreach (var enemy in new List<IslandEnemy>(IslandEnemy.All))
                    if (enemy != null && enemy.Alive) enemy.Controller.TakeDamage(1e9f, DamageSource.Env);
                stall = 0f; forced = false;
            }
            yield return null;
        }
    }

    /// <summary>İlerleme imzası: (düşman sayısı, toplam düşman canı, kale + okçu canı, düşman konumları toplamı).</summary>
    private Vector4 ActivitySample()
    {
        float count = 0f, hp = 0f, moved = 0f;
        foreach (var enemy in IslandEnemy.All)
        {
            if (enemy == null || !enemy.Alive) continue;
            count++;
            hp += enemy.Controller.CurrentHealth;
            moved += Mathf.Abs(enemy.Position.x) + Mathf.Abs(enemy.Position.y);
        }
        float defenders = (castle != null ? castle.CurrentHealth : 0f) + (archer != null ? archer.Hp : 0f);
        return new Vector4(count, hp, defenders, moved);
    }

    private bool WaveCleared()
    {
        if (pendingFlyers > 0) return false;
        foreach (var boat in RaidBoat.All)
            if (boat != null && boat.Alive) return false;
        foreach (var enemy in IslandEnemy.All)
            if (enemy != null && enemy.Alive) return false;
        return true;
    }

    private void SetTutorial(int step)
    {
        var save = IslandSession.Save;
        if (save == null) return;
        if (step > save.tutorialStep) save.tutorialStep = step;
        TutorialChanged?.Invoke();
    }

    /// <summary>Öğretici ipucu metni (HUD gösterir). Boş: ipucu yok.</summary>
    public string TutorialHint() => Phase == State.Running ? Intro.Message : "";

    // ==================== SONUÇ ====================

    private void Win()
    {
        Phase = State.Won;
        if (IsRaid) { FinishRaid(true); StateChanged?.Invoke(Phase); return; }
        LastReward = IslandSession.GrantVictory(Akin);
        IslandRepair.CommitBattle();
        if (tutorialRun) { SetTutorial(4); TutorialFinishedThisRun = true; }
        StateChanged?.Invoke(Phase);
    }

    private void OnCastleDestroyed(BattleStructure structure)
    {
        if (Phase != State.Running) return;
        var label = ComputeDefeatLabel();
        DefeatReason = label.text; DefeatTag = label.tag;
        Phase = State.Lost;
        if (IsRaid) FinishRaid(false);
        // Yenilgide kalan düşmanlar donar (kale onarılınca tekrar vurulmasın)
        foreach (var enemy in IslandEnemy.All)
            if (enemy != null && enemy.Controller != null) enemy.Controller.MovementLocked = true;
        foreach (var boat in RaidBoat.All) if (boat != null) boat.enabled = false;
        IslandRepair.CommitBattle();
        if (run != null) StopCoroutine(run);
        StateChanged?.Invoke(Phase);
    }

    /// <summary>Canlı baskın sonucu: çalıntı/ödül kayda geçer, sonraki baskın planlanır (birikmez).</summary>
    private void FinishRaid(bool won)
    {
        IslandRaidScheduler.LiveFightActive = false;
        var save = IslandSession.Save;
        if (save != null)
        {
            LastRaidOutcome = IslandRaidScheduler.CompleteLive(save, won, ratThieves.Count, IslandRepair.Now);
            var gm = GameManager.Instance;
            if (gm != null)
            {
                if (LastRaidOutcome.gold > 0) gm.AddGold(LastRaidOutcome.gold);
                gm.SaveGame();
            }
        }
        IslandRepair.CommitBattle();
    }

    /// <summary>Yenilgi etiketi (§7.6): hasar dağılımı + kumsal izleme durumundan (≥%90 açıklanmalı).</summary>
    private IslandDefeatLabel ComputeDefeatLabel() =>
        IslandDefeat.Classify(IslandStats.DamageByType, IslandStats.CastleDamageByBeach, IslandStats.ShieldBlocks,
            beach => IslandDefeat.BeachWatched(beach, archer));

    /// <summary>Sonuç ekranından ya da testlerden: akını bırak.</summary>
    public void Abandon()
    {
        if (Phase != State.Running) return;
        if (run != null) StopCoroutine(run);
        IslandRaidScheduler.LiveFightActive = false;   // baskın bekler; pencere dolarsa çevrimdışı çözülür
        IsRaid = false;
        Phase = State.Idle;
        foreach (var boat in new List<RaidBoat>(RaidBoat.All)) if (boat != null) Destroy(boat.gameObject);
        foreach (var enemy in new List<IslandEnemy>(IslandEnemy.All)) if (enemy != null) Destroy(enemy.gameObject);
        StateChanged?.Invoke(Phase);
    }

    /// <summary>Sonuçtan sonra yeni Akın seçimine dön.</summary>
    public void ReturnToIdle()
    {
        if (Phase == State.Running) return;
        IsRaid = false;
        // Yenilgide kalan (donmuş) düşman ve tekneler ada görünümüne geçerken temizlenir
        foreach (var boat in new List<RaidBoat>(RaidBoat.All)) if (boat != null) Destroy(boat.gameObject);
        foreach (var enemy in new List<IslandEnemy>(IslandEnemy.All)) if (enemy != null) Destroy(enemy.gameObject);
        Phase = State.Idle;
        StateChanged?.Invoke(Phase);
    }
}
