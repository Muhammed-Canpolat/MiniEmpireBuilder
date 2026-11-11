using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Korsan teknesi (Docs/design/island_base.md §3): kenardan kumsala sabit hızla gider. Batarsa içindeki herkes boğulur;
/// kumsala varırsa tayfa 0,4 sn arayla iner, boş tekne geri çekilip kaybolur. Görsel kodla çizilir (gövde + yelken).
/// </summary>
public sealed class RaidBoat : MonoBehaviour
{
    private static readonly List<RaidBoat> all = new List<RaidBoat>();
    public static IReadOnlyList<RaidBoat> All => all;

    public enum Phase { Sailing, Landing, Leaving, Sinking, Gone }

    public Progression.Island.BoatKind Kind { get; private set; }
    public int BeachIndex { get; private set; }
    public Phase State { get; private set; } = Phase.Sailing;
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public bool Alive => State == Phase.Sailing || State == Phase.Landing;
    public Vector2 Position => transform.position;
    public int CrewLeft => crew.Count;

    /// <summary>Tekne battı: (tekne, boğulan tayfa sayısı, boğulan tayfanın altın değeri).</summary>
    public event Action<RaidBoat, int, int> Sunk;
    /// <summary>Tekne kıyıya yanaştı ve boşaldı / çekildi.</summary>
    public event Action<RaidBoat> Finished;
    /// <summary>Tayfa indi: (tekne, tür, iniş noktası).</summary>
    public event Action<RaidBoat, EnemyType, Vector2> Disembarked;

    private readonly List<EnemyType> crew = new List<EnemyType>();
    private Vector2[] path;
    private int segment;
    private float speed;
    private float nextDisembark;
    private SpriteRenderer sr;
    private HealthBar healthBar;
    private Vector2 landing;
    private Vector3 baseScale;
    private float bobSeed;
    private Vector2 lastPosition;

    public static RaidBoat Spawn(Progression.Island.BoatKind kind, int battleLevel, int beachIndex,
        IList<EnemyType> crewTypes, float speedMultiplier = 1f)
    {
        var go = new GameObject("RaidBoat_" + kind);
        var boat = go.AddComponent<RaidBoat>();
        boat.Init(kind, battleLevel, beachIndex, crewTypes, speedMultiplier);
        return boat;
    }

    private void Init(Progression.Island.BoatKind kind, int battleLevel, int beachIndex, IList<EnemyType> crewTypes,
        float speedMultiplier)
    {
        Kind = kind;
        BeachIndex = beachIndex;
        crew.AddRange(crewTypes);
        // Tekne canı: tasarım tablosu (≈ tayfa canının %70'i) × düşman can çarpanı.
        MaxHealth = Progression.Island.BoatBaseHp(kind) * Progression.Island.EnemyHealthMult(battleLevel);
        Health = MaxHealth;
        speed = Progression.Island.BoatSpeed(kind) * speedMultiplier;
        var beach = IslandLayout.Beaches[beachIndex];
        path = beach.boatPath;
        landing = beach.landing;
        transform.position = path[0];
        segment = 1;
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = IslandArt.Boat(kind);
        float s = kind == Progression.Island.BoatKind.Barge ? 1.7f : kind == Progression.Island.BoatKind.Longboat ? 1.35f : 1.05f;
        baseScale = new Vector3(s, s, 1f);
        transform.localScale = baseScale;
        bobSeed = UnityEngine.Random.value * 6f;
        gameObject.AddComponent<IslandYSort>().bias = -20; // suda: karadaki birimlerin altında
        healthBar = HealthBar.Create(transform, MaxHealth, new Vector3(0f, 1.0f * s, 0f), new Color(0.9f, 0.35f, 0.25f));
        lastPosition = path[0];
        all.Add(this);
    }

    private void OnDestroy()
    {
        all.Remove(this);
        if (healthBar != null) Destroy(healthBar.gameObject);
    }

    private void Update()
    {
        switch (State)
        {
            case Phase.Sailing: Sail(); break;
            case Phase.Landing: Land(); break;
            case Phase.Leaving: Leave(); break;
        }
        // Hafif dalgalanma: yalnız görsel.
        if (State != Phase.Sinking)
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2.2f + bobSeed) * 2.5f);
    }

    private void Sail()
    {
        if (segment >= path.Length) { State = Phase.Landing; nextDisembark = Time.time + 0.5f; return; }
        Vector2 target = path[segment];
        Vector2 position = transform.position;
        Vector2 next = Vector2.MoveTowards(position, target, speed * Time.deltaTime);
        transform.position = next;
        Face(next - position);
        if (Vector2.Distance(next, target) < 0.02f) segment++;
    }

    private void Face(Vector2 delta)
    {
        // Sprite sola bakar: sağa gidiyorsa çevir.
        if (Mathf.Abs(delta.x) > 0.0005f) sr.flipX = delta.x > 0f;
    }

    private void Land()
    {
        if (Time.time < nextDisembark) return;
        if (crew.Count == 0)
        {
            State = Phase.Leaving;
            Finished?.Invoke(this);
            return;
        }
        nextDisembark = Time.time + Progression.Island.DisembarkInterval;
        EnemyType type = crew[0];
        crew.RemoveAt(0);
        Disembarked?.Invoke(this, type, landing);
    }

    private float leaveStart = -1f;

    private void Leave()
    {
        if (leaveStart < 0f) leaveStart = Time.time;
        float t = (Time.time - leaveStart) / 1.5f;
        // Geldiği yöne doğru çekilir ve solar.
        Vector2 back = path.Length >= 2 ? (path[path.Length - 2] - path[path.Length - 1]).normalized : Vector2.down;
        transform.position += (Vector3)(back * 0.6f * Time.deltaTime);
        sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(t));
        if (t >= 1f) { State = Phase.Gone; Destroy(gameObject); }
    }

    /// <summary>Ok isabeti. <paramref name="damage"/> tamsa (kritik/dolu ×2,5 dahil); dolu atış tekneye ek ×1,5, Mavna'da tek tık ×0,6.</summary>
    public void TakeArrow(float damage, bool charged)
    {
        if (!Alive) return;
        float multiplier = charged ? Progression.Island.ChargedHullMult
            : Kind == Progression.Island.BoatKind.Barge ? Progression.Island.BargeTapTaken : 1f;
        damage *= multiplier;
        Health = Mathf.Max(0f, Health - damage);
        if (healthBar != null) healthBar.UpdateHealth(Health);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(transform.position + Vector3.up * 0.6f, damage, damage);
        sr.DOKill();
        sr.color = new Color(1f, 0.6f, 0.55f);
        sr.DOColor(Color.white, 0.15f).SetLink(gameObject);
        if (Health <= 0f) Sink();
    }

    private void Sink()
    {
        State = Phase.Sinking;
        int drowned = crew.Count;
        int goldValue = 0;
        foreach (var type in crew) goldValue += EnemyStats.GetBaseStats(type).goldDrop;
        crew.Clear();
        Sunk?.Invoke(this, drowned, goldValue);
        IslandFx.Splash(transform.position, 2f);
        if (drowned > 0)
            IslandFx.FloatText(transform.position + Vector3.up * 0.5f,
                L.T($"+{drowned} boğuldu", $"+{drowned} drowned"), new Color(0.6f, 0.85f, 1f));
        if (healthBar != null) healthBar.gameObject.SetActive(false);
        float d = Progression.Island.BoatSinkSeconds;
        transform.DORotate(new Vector3(0f, 0f, UnityEngine.Random.value < 0.5f ? 28f : -28f), d).SetLink(gameObject);
        transform.DOMoveY(transform.position.y - 0.5f, d).SetLink(gameObject);
        sr.DOFade(0f, d).SetDelay(0.1f).OnComplete(() => Destroy(gameObject)).SetLink(gameObject);
    }

    /// <summary>Test/telemetri: tekneyi hemen batır.</summary>
    public void SinkNow() { if (Alive) { Health = 0f; Sink(); } }
}
