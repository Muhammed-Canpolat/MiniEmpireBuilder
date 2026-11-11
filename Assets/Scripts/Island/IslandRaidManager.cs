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
    public int BattleLevel { get; private set; }
    public float BreakLeft { get; private set; }
    public IslandSession.Reward LastReward { get; private set; }
    public string DefeatReason { get; private set; } = "";
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

    public void Bind(BattleStructure castleStructure, IslandArcher islandArcher)
    {
        castle = castleStructure;
        archer = islandArcher;
        if (castle != null) castle.Destroyed += OnCastleDestroyed;
        if (archer != null) archer.PostChanged += OnPostChanged;
    }

    private void OnDestroy()
    {
        if (castle != null) castle.Destroyed -= OnCastleDestroyed;
        if (archer != null) archer.PostChanged -= OnPostChanged;
    }

    private void Update()
    {
        if (Phase == State.Running) Elapsed += Time.deltaTime;
    }

    // ==================== AKIN BAŞLAT ====================

    /// <summary>Akın N'yi başlatır (kapı denetimi çağıranda: <see cref="CanStart"/>). Çalışıyorsa false.</summary>
    public bool StartAkin(int akin)
    {
        if (Phase == State.Running) return false;
        Akin = Mathf.Max(1, akin);
        BattleLevel = Progression.Island.AkinBattleLevel(Akin);
        TotalWaves = Progression.Island.AkinWaves(Akin);
        Wave = 0;
        Elapsed = 0f;
        spawnedBoats = finishedBoats = 0;
        bossBoat = null; bossSpawned = false;
        DefeatReason = "";
        TutorialFinishedThisRun = false;
        IslandStats.Reset();
        tutorialRun = Akin == 1 && IslandSession.Save != null && IslandSession.Save.tutorialStep < 3;
        if (tutorialRun && IslandSession.Save.tutorialStep == 0) SetTutorial(1);
        if (IslandSession.Save != null) IslandSession.Save.lastAkin = Akin;
        Phase = State.Running;
        StateChanged?.Invoke(Phase);
        run = StartCoroutine(RunAkin());
        return true;
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
            yield return SpawnWave(wave);
            // Dalga bitene kadar: tekneler batar/boşalır, inen düşmanlar ölür.
            while (Phase == State.Running && !WaveCleared()) yield return null;
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
        for (int i = 0; i < boats; i++)
        {
            if (Phase != State.Running) yield break;
            int beach = (i + wave - 1) % openBeaches;
            // Öğretici: ikinci tekne doğu kumsalından gelir (atış yeri değiştirmeyi öğretir).
            if (tutorialRun && Akin == 1 && i == 1) beach = 1;
            var kind = BoatKindFor(i);
            bool isBossBoat = Progression.Island.IsBossAkin(Akin) && lastWave && i == boats - 1;
            if (isBossBoat) kind = Progression.Island.BoatKind.Barge;
            var crew = CrewFor(kind, i, isBossBoat, Akin);
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

    /// <summary>Tayfa: Akın 1'de Tayfa; sonra Tayfa/Akıncı dönüşür, Akın 4+ her üçüncüsü Kıyı Okçusu. Boss teknesinde ilk kişi Kaptan.</summary>
    public static List<EnemyType> CrewFor(Progression.Island.BoatKind kind, int boatIndex, bool bossBoat, int akin)
    {
        var crew = new List<EnemyType>();
        int count = Progression.Island.BoatCrew(kind);
        for (int i = 0; i < count; i++)
        {
            EnemyType type;
            if (akin < 2) type = EnemyType.Deckhand;
            else if (akin >= 4 && (i + boatIndex) % 3 == 2) type = EnemyType.ShoreArcher;
            else type = (i + boatIndex) % 2 == 0 ? EnemyType.Deckhand : EnemyType.Raider;
            crew.Add(type);
        }
        if (bossBoat && crew.Count > 0) crew[0] = EnemyType.Raider; // Kaptan
        return crew;
    }

    // ==================== OLAYLAR ====================

    private void OnDisembarked(RaidBoat boat, EnemyType type, Vector2 landing)
    {
        if (Phase != State.Running) return;
        var beach = IslandLayout.Beaches[boat.BeachIndex];
        bool boss = boat == bossBoat && !bossSpawned;
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
        if (tutorialRun && TutorialStep == 2 && post == 3) SetTutorial(3);
    }

    private bool WaveCleared()
    {
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
    public string TutorialHint()
    {
        if (!tutorialRun || Phase != State.Running) return "";
        switch (TutorialStep)
        {
            case 1: return L.T("Tekne geliyor! ATEŞ'e dokun.", "A boat is coming! Tap FIRE.");
            case 2: return L.T("Doğu kumsalını görmek için atış yerine dokun.", "Tap a firing post to watch the east beach.");
            default: return "";
        }
    }

    // ==================== SONUÇ ====================

    private void Win()
    {
        Phase = State.Won;
        LastReward = IslandSession.GrantVictory(Akin);
        if (tutorialRun) { SetTutorial(4); TutorialFinishedThisRun = true; }
        StateChanged?.Invoke(Phase);
    }

    private void OnCastleDestroyed(BattleStructure structure)
    {
        if (Phase != State.Running) return;
        DefeatReason = ComputeDefeatReason();
        Phase = State.Lost;
        if (run != null) StopCoroutine(run);
        StateChanged?.Invoke(Phase);
    }

    /// <summary>Yenilgi etiketi (okunabilirlik): kaleyi vuran düşmanların geldiği kumsal izlenmiyorsa onu söyler.</summary>
    private string ComputeDefeatReason()
    {
        var counts = new int[IslandLayout.Beaches.Length];
        foreach (var enemy in IslandEnemy.All)
            if (enemy != null && enemy.Alive && enemy.BeachIndex >= 0 && enemy.BeachIndex < counts.Length) counts[enemy.BeachIndex]++;
        int worst = 0;
        for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[worst]) worst = i;
        if (counts[worst] == 0) return L.T("Kale düştü.", "The keep fell.");
        var beach = IslandLayout.Beaches[worst];
        bool watched = archer != null && archer.IsAlive &&
            Vector2.Distance(archer.Position, beach.landing) <= archer.Range + 1f;
        return watched
            ? L.T("Kale düştü: hedefi kilitleyip dolu atış dene.", "The keep fell: lock a target and try a charged shot.")
            : L.T($"{beach.name} kumsalı izlenmedi.", $"Beach {beach.name} was not watched.");
    }

    /// <summary>Sonuç ekranından ya da testlerden: akını bırak.</summary>
    public void Abandon()
    {
        if (Phase != State.Running) return;
        if (run != null) StopCoroutine(run);
        Phase = State.Idle;
        foreach (var boat in new List<RaidBoat>(RaidBoat.All)) if (boat != null) Destroy(boat.gameObject);
        foreach (var enemy in new List<IslandEnemy>(IslandEnemy.All)) if (enemy != null) Destroy(enemy.gameObject);
        StateChanged?.Invoke(Phase);
    }

    /// <summary>Sonuçtan sonra yeni Akın seçimine dön.</summary>
    public void ReturnToIdle()
    {
        if (Phase == State.Running) return;
        Phase = State.Idle;
        StateChanged?.Invoke(Phase);
    }
}
