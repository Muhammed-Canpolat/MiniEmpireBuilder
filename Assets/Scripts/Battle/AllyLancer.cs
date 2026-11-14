using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Ana Üs Kışlası (island_base.md §7, island_base_v2 Faz F): sefer savaşında kurulu her Kışla için 1 + ⌊L/5⌋ mavi Lancer doğurur.
/// Lancer'lar yalnız Ana Üs'ün <see cref="Progression.AllyGuardRadius"/> çevresini korur (avlanmaz), ölünce
/// <see cref="Progression.AllyRespawn"/> sn sonra yeniden doğar. Savaş dışında yoktur. Ada Kışlası ayrıdır (<c>IslandBarracks</c>).
/// Görsel: Ada Lancer'ının Tiny Swords kareleri (<c>IslandVisualCatalog</c> "blue_lancer_*"); katalog yoksa renkli yedek şekil.
/// </summary>
public sealed class AllyBarracks : MonoBehaviour
{
    private sealed class Slot { public AllyLancer lancer; public float respawnLeft; public int level; }

    private readonly List<Slot> slots = new List<Slot>();
    private Transform home;
    private BattleManager battle;

    /// <summary>Savaş boyunca doğan toplam Lancer (ilk doğuş + yeniden doğuşlar) ve ölenler: telemetri/test.</summary>
    public static int Spawned, Deaths;
    public static AllyBarracks Instance { get; private set; }

    public int SlotCount => slots.Count;
    public int Alive { get { int n = 0; foreach (var s in slots) if (s.lancer != null && s.lancer.Alive) n++; return n; } }
    public Transform Home => home;

    /// <summary>Kayıttaki kurulu Kışla seviyeleri (her bina kendi seviyesindeki kadar Lancer verir).</summary>
    public static List<int> BarracksLevels(PlayerSaveData data)
    {
        var levels = new List<int>();
        if (data?.placedBuildings == null) return levels;
        foreach (var placed in data.placedBuildings)
            if (placed != null && placed.level > 0 && placed.buildingType == nameof(BuildingType.Barracks)) levels.Add(placed.level);
        return levels;
    }

    /// <summary>Savaş sahnesinde Kışla garnizonunu kurar; kurulu Kışla yoksa null (nesne oluşturulmaz).</summary>
    public static AllyBarracks Install(Transform mainBase, BattleManager battle)
    {
        var levels = BarracksLevels(GameManager.Instance?.PlayerData);
        return levels.Count == 0 || mainBase == null ? null : Create(mainBase, levels, battle);
    }

    public static AllyBarracks Create(Transform mainBase, IList<int> barracksLevels, BattleManager battle = null)
    {
        var go = new GameObject("AllyBarracks");
        var barracks = go.AddComponent<AllyBarracks>();
        barracks.home = mainBase;
        barracks.battle = battle;
        int total = 0;
        foreach (int level in barracksLevels) total += Progression.AllyLancerCount(level);
        foreach (int level in barracksLevels)
            for (int i = 0, n = Progression.AllyLancerCount(level); i < n; i++) barracks.slots.Add(new Slot { level = level });
        for (int i = 0; i < barracks.slots.Count; i++) barracks.Spawn(i, total);
        Instance = barracks;
        return barracks;
    }

    private void Spawn(int index, int total)
    {
        var slot = slots[index];
        var lancer = AllyLancer.Create(home, slot.level, index, total);
        slot.lancer = lancer;
        Spawned++;
        lancer.Died += l =>
        {
            Deaths++;
            if (slot.lancer == l) { slot.lancer = null; slot.respawnLeft = Progression.AllyRespawn; }
        };
    }

    private void Update()
    {
        if (battle == null) battle = BattleManager.Instance;
        if (battle != null && !battle.IsBattleActive) return;   // zafer/yenilgi sonrası yeni Lancer yok
        Advance(Time.deltaTime);
    }

    /// <summary>Yeniden doğma sayaçlarını <paramref name="seconds"/> ilerletir (Update çağırır; testler doğrudan kullanır).</summary>
    public void Advance(float seconds)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.lancer != null) continue;
            slot.respawnLeft -= seconds;
            if (slot.respawnLeft <= 0f) Spawn(i, slots.Count);
        }
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}

/// <summary>
/// Mavi Lancer: Ana Üs çevresindeki (<see cref="Progression.AllyGuardRadius"/>) en yakın boştaki düşmana gider ve vurur
/// (<see cref="DamageSource.Ally"/>); düşman bölgeden çıkarsa bırakır, bekleme noktasına döner. Uçanı vuramaz.
/// Düşman yakın dövüşçüyse yanındaki Lancer'a da vurabilir (<see cref="EnemyController"/>).
/// </summary>
public sealed class AllyLancer : MonoBehaviour
{
    public static readonly List<AllyLancer> All = new List<AllyLancer>();
    /// <summary>Lancer kare ayağının merkezden aşağıda kalan payı (ada ile aynı: 38/64 u, ölçek uygulanır).</summary>
    private const float VisualScale = 1.2f;
    private const float FootLift = 38f / 64f * VisualScale;

    public bool Alive { get; private set; } = true;
    public float Hp { get; private set; }
    public float MaxHp { get; private set; }
    public float Damage { get; private set; }
    public int Level { get; private set; }
    public int Slot { get; private set; }
    public int SlotCount { get; private set; }
    public int Hits { get; private set; }
    public EnemyController Target { get; private set; }
    public Vector2 Position => transform.position;
    public event Action<AllyLancer> Died;

    private Transform baseTransform;
    private SpriteRenderer sr;
    private IslandSpriteAnimator anim;
    private HealthBar bar;
    private HeroController hero;
    private float nextHit;

    public static AllyLancer Create(Transform mainBase, int barracksLevel, int slot, int slotCount)
    {
        var go = new GameObject("AllyLancer");
        var lancer = go.AddComponent<AllyLancer>();
        lancer.baseTransform = mainBase;
        lancer.Level = barracksLevel;
        lancer.Slot = slot; lancer.SlotCount = Mathf.Max(1, slotCount);
        lancer.MaxHp = Progression.AllyLancerHp(barracksLevel);
        lancer.Hp = lancer.MaxHp;
        lancer.Damage = Progression.AllyLancerDamage(barracksLevel);
        go.transform.localScale = Vector3.one * VisualScale;
        go.transform.position = lancer.HomePoint();
        lancer.sr = go.AddComponent<SpriteRenderer>();
        lancer.sr.sortingOrder = 34;
        var catalog = IslandVisualCatalog.Instance;
        var frames = new IslandUnitFrames();
        if (catalog != null)
        {
            frames.idle = catalog.Frames("blue_lancer_idle");
            frames.run = catalog.Frames("blue_lancer_run");
            frames.attack = catalog.Frames("blue_lancer_attack");
        }
        if (frames.HasArt) lancer.sr.sprite = IslandVisualCatalog.Has(frames.idle) ? frames.idle[0] : frames.run[0];
        lancer.anim = go.AddComponent<IslandSpriteAnimator>();
        lancer.anim.Setup(lancer.sr, frames);
        lancer.bar = HealthBar.Create(go.transform, lancer.MaxHp, new Vector3(0f, 1.15f, 0f), new Color(0.35f, 0.65f, 1f));
        return lancer;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>Bekleme noktası: Ana Üs'ün alt yarısında yay üzerinde, <see cref="Progression.AllyHomeRadius"/> uzaklıkta.</summary>
    public Vector2 HomePoint()
    {
        Vector2 center = baseTransform != null ? (Vector2)baseTransform.position : Vector2.zero;
        float t = SlotCount <= 1 ? 0f : (Slot / (float)(SlotCount - 1) - 0.5f) * 2f;   // -1..1
        float angle = (-90f + t * Mathf.Min(70f, 14f * (SlotCount - 1))) * Mathf.Deg2Rad;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Progression.AllyHomeRadius + Vector2.up * FootLift;
    }

    private static bool CanHold(EnemyController e) => e != null && !e.IsDead && !e.IsFlying && FogOfWar.IsVisible(e.transform.position);

    private int ClaimsOn(EnemyController e)
    {
        int n = 0;
        foreach (var other in All) if (other != this && other.Alive && other.Target == e) n++;
        return n;
    }

    private EnemyController PickTarget(Vector2 center)
    {
        EnemyController best = null;
        float bestScore = float.MaxValue;
        foreach (var e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (!CanHold(e)) continue;
            if (Vector2.Distance(center, e.transform.position) > Progression.AllyGuardRadius) continue;
            float score = Vector2.Distance(Position, e.transform.position) + ClaimsOn(e) * 100f;   // her Lancer bir düşman
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    /// <summary><paramref name="from"/> noktasına <paramref name="range"/> içindeki en yakın canlı Lancer (düşman hedeflemesi).</summary>
    public static AllyLancer NearestTo(Vector2 from, float range)
    {
        AllyLancer best = null;
        float bestDistance = range;
        foreach (var l in All)
        {
            if (l == null || !l.Alive) continue;
            float d = Vector2.Distance(from, l.Position);
            if (d <= bestDistance) { bestDistance = d; best = l; }
        }
        return best;
    }

    private void Update()
    {
        if (!Alive) return;
        var battle = BattleManager.Instance;
        if (battle != null && !battle.IsBattleActive) { Target = null; }
        if (hero == null) hero = FindFirstObjectByType<HeroController>();
        var heroRenderer = hero != null ? hero.GetComponent<SpriteRenderer>() : null;
        sr.sortingOrder = heroRenderer != null ? heroRenderer.sortingOrder : 34;

        Vector2 center = baseTransform != null ? (Vector2)baseTransform.position : Vector2.zero;
        if (Target != null && (!CanHold(Target) || Vector2.Distance(center, Target.transform.position) > Progression.AllyGuardRadius)) Target = null;
        if (Target == null && (battle == null || battle.IsBattleActive)) Target = PickTarget(center);
        Vector2 goal = HomePoint();
        if (Target != null)
        {
            Vector2 aim = Target.transform.position;
            float d = Vector2.Distance(Position, aim);
            if (d <= Progression.AllyLancerReach)
            {
                goal = Position;
                sr.flipX = aim.x < Position.x;
                if (Time.time >= nextHit)
                {
                    nextHit = Time.time + Progression.AllyHitInterval;
                    Hits++;
                    anim.Attack();
                    Target.TakeDamage(Damage, DamageSource.Ally);
                }
            }
            else goal = aim;
        }
        Vector2 to = goal - Position;
        if (to.sqrMagnitude > 0.04f)
        {
            if (Mathf.Abs(to.x) > 0.05f && Target == null) sr.flipX = to.x < 0f;
            steer = steer ?? new GridSteer(BattleWalkGrid.Shared);   // 4. oturum (1.1): yapıların içinden geçmez
            transform.position = steer.Step(transform.position, goal, Progression.AllyLancerSpeed * Time.deltaTime, out _);
        }
    }

    private GridSteer steer;

    public void TakeDamage(float amount)
    {
        if (!Alive || amount <= 0f) return;
        Hp = Mathf.Max(0f, Hp - amount);
        if (bar != null) bar.UpdateHealth(Hp);
        sr.DOKill();
        sr.color = new Color(1f, 0.5f, 0.5f);
        sr.DOColor(Color.white, 0.15f).SetLink(sr.gameObject);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(transform.position + Vector3.up * 0.6f, amount, amount);
        if (Hp <= 0f) Die();
    }

    /// <summary>Manastır vb. şifa: tavanı aşmaz.</summary>
    public bool Heal(float amount)
    {
        if (!Alive || amount <= 0f || Hp >= MaxHp) return false;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        if (bar != null) bar.UpdateHealth(Hp);
        return true;
    }

    private void Die()
    {
        Alive = false;
        Target = null;
        Died?.Invoke(this);
        Destroy(gameObject);
    }
}
