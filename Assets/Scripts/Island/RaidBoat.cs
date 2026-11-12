using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Korsan teknesi (Docs/design/island_base_v2.md §7.2): kenardan kumsala sabit hızla gider. Batarsa içindeki herkes boğulur;
/// kumsala varırsa tayfa 0,4 sn arayla iner, boş tekne geri çekilip kaybolur. Görsel: Kenney Pirate Pack gemileri
/// (üstten, pruva aşağı bakar; dönüş = hız yönü + 90°). Hasarla sprite +6 / +12 kareye geçer, batışta +18 (gri batık).
/// Düşman teknesi hiçbir zaman mavi değil (mavi oyuncunun rengi).
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
    /// <summary>Gövde sprite'ının dünya boyu (testler ve rozet yeri).</summary>
    public float HullLength => hullLength;
    public SpriteRenderer HullRenderer => sr;

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
    private Transform hull;
    private HealthBar healthBar;
    private Vector2 landing;
    private float bobSeed;
    private int shipNumber;                 // 0: Sandal (hasar karesi yok)
    private int damageStage;
    private float heading;                  // derece (sprite pruvası aşağı: heading = hız açısı + 90)
    private TextMeshPro crewBadge;
    private float hullLength;

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

        var catalog = IslandVisualCatalog.Instance;
        var hullObject = new GameObject("Hull");
        hullObject.transform.SetParent(transform, false);
        hull = hullObject.transform;
        sr = hullObject.AddComponent<SpriteRenderer>();
        int akin = Mathf.Max(1, Mathf.RoundToInt((battleLevel - 1) / 0.8f));
        // Kademe I kırmızı (ship 3) · II–III siyah (ship 2) · IV sarı (ship 6); mavi kullanılmaz (§7.2).
        int tier = Progression.Island.BoatShipNumber(akin);
        float scale;
        if (kind == Progression.Island.BoatKind.Rowboat)
        {
            shipNumber = 0;
            sr.sprite = catalog != null ? catalog.Single("dinghy" + (1 + UnityEngine.Random.Range(0, 3))) : null;
            scale = Progression.Island.RowboatScale;
        }
        else
        {
            shipNumber = tier;
            sr.sprite = catalog != null ? catalog.Single("ship_" + tier) : null;
            scale = kind == Progression.Island.BoatKind.Barge ? Progression.Island.BargeScale : Progression.Island.LongboatScale;
        }
        hull.localScale = new Vector3(scale, scale, 1f);
        hullLength = sr.sprite != null ? sr.sprite.bounds.size.y * scale : 1.5f;
        bobSeed = UnityEngine.Random.value * 6f;
        gameObject.AddComponent<IslandYSort>().bias = -20; // suda: karadaki birimlerin altında
        healthBar = HealthBar.Create(transform, MaxHealth, new Vector3(0f, hullLength * 0.5f + 0.25f, 0f), new Color(0.9f, 0.35f, 0.25f));
        Vector2 first = path.Length > 1 ? path[1] - path[0] : Vector2.down;
        heading = Mathf.Atan2(first.y, first.x) * Mathf.Rad2Deg + 90f;
        BuildCrewBadge();
        all.Add(this);
    }

    private void BuildCrewBadge()
    {
        var go = new GameObject("CrewBadge");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -hullLength * 0.5f - 0.15f, 0f);
        crewBadge = go.AddComponent<TextMeshPro>();
        crewBadge.fontSize = 3.2f;
        crewBadge.alignment = TextAlignmentOptions.Center;
        crewBadge.color = new Color(1f, 0.95f, 0.85f);
        crewBadge.fontStyle = FontStyles.Bold;
        IslandFx.Outline(crewBadge);
        crewBadge.sortingOrder = IslandFx.FxSorting;
        crewBadge.text = "×" + crew.Count;
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
        // Pruva hız yönüne döner; hafif dalgalanma yalnız görsel (Sandal %50 canın altında yalpalar).
        if (State != Phase.Sinking && hull != null)
        {
            bool wobbly = shipNumber == 0 && Health < MaxHealth * 0.5f;
            float bob = Mathf.Sin(Time.time * (wobbly ? 5f : 2.2f) + bobSeed) * (wobbly ? 7f : 2.5f);
            hull.localRotation = Quaternion.Euler(0f, 0f, heading + bob);
        }
        if (crewBadge != null && crew.Count > 0 && crewBadge.text != "×" + crew.Count) crewBadge.text = "×" + crew.Count;
        TickBurn();
    }

    // ---- Faz E2: okçu yetenekleri (Ağ Oku yavaşlatır, Ateşli Ok tutuşturur) ----
    private float slowUntil, slowMult = 1f, burnUntil, burnFraction, burnTick;

    /// <summary>Ağ Oku: tekne <paramref name="seconds"/> sn boyunca hızı <paramref name="mult"/> ile çarpılır.</summary>
    public void Slow(float mult, float seconds)
    {
        if (!Alive) return;
        slowMult = Mathf.Min(slowMult < 1f && Time.time < slowUntil ? slowMult : 1f, mult);
        slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
    }

    /// <summary>Ateşli Ok: tekne <paramref name="seconds"/> sn boyunca azami canın <paramref name="fractionPerSecond"/> oranını saniyede yitirir.</summary>
    public void Ignite(float seconds, float fractionPerSecond)
    {
        if (!Alive) return;
        burnUntil = Time.time + seconds;
        burnFraction = fractionPerSecond;
        burnTick = Time.time + 0.5f;
        IslandFx.Play("fire_2", transform.position, 0.9f, 14f, false, transform);
    }

    public bool Burning => Alive && Time.time < burnUntil;

    private void TickBurn()
    {
        if (!Alive || Time.time >= burnUntil || Time.time < burnTick) return;
        burnTick = Time.time + 0.5f;
        TakeFire(MaxHealth * burnFraction * 0.5f);
    }

    /// <summary>Çarpansız hasar (yangın): teknenin hasar karesi ve batışı normal çalışır.</summary>
    public void TakeFire(float damage)
    {
        if (!Alive) return;
        Health = Mathf.Max(0f, Health - damage);
        if (healthBar != null) healthBar.UpdateHealth(Health);
        UpdateDamageSprite();
        if (Health <= 0f) Sink();
    }

    private void Sail()
    {
        if (segment >= path.Length) { State = Phase.Landing; nextDisembark = Time.time + 0.5f; return; }
        Vector2 target = path[segment];
        Vector2 position = transform.position;
        Vector2 next = Vector2.MoveTowards(position, target, speed * (Time.time < slowUntil ? slowMult : 1f) * Time.deltaTime);
        transform.position = next;
        Face(next - position);
        if (Vector2.Distance(next, target) < 0.02f) segment++;
    }

    private void Face(Vector2 delta)
    {
        if (delta.sqrMagnitude < 0.000001f) return;
        float target = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f;
        heading = Mathf.MoveTowardsAngle(heading, target, 240f * Time.deltaTime);
    }

    private void Land()
    {
        Face(landing - (Vector2)transform.position);   // pruvayla kumsala yanaşır
        if (Time.time < nextDisembark) return;
        if (crew.Count == 0)
        {
            State = Phase.Leaving;
            if (crewBadge != null) crewBadge.gameObject.SetActive(false);
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
        UpdateDamageSprite();
        if (Health <= 0f) Sink();
    }

    /// <summary>Can %66'nın altında +6, %33'ün altında +12 kare (kırmızı 3 → 9 → 15); Sandalın hasar karesi yok.</summary>
    private void UpdateDamageSprite()
    {
        if (shipNumber == 0 || Health <= 0f) return;
        float fraction = Health / Mathf.Max(1f, MaxHealth);
        int stage = fraction < Progression.Island.BoatDamageAt[1] ? 2 : fraction < Progression.Island.BoatDamageAt[0] ? 1 : 0;
        if (stage == damageStage) return;
        damageStage = stage;
        var catalog = IslandVisualCatalog.Instance;
        var sprite = catalog != null ? catalog.Single("ship_" + (shipNumber + 6 * stage)) : null;
        if (sprite != null) sr.sprite = sprite;
        if (stage == 2) AddDeckFire();
    }

    private GameObject deckFire;

    /// <summary>Ağır hasarlı teknede güvertede yangın (Kenney Pirate Pack fire1/fire2, gerçek asset): batmak üzere olduğu okunur.</summary>
    private void AddDeckFire()
    {
        if (deckFire != null) return;
        var catalog = IslandVisualCatalog.Instance;
        var fireSprite = catalog != null ? catalog.Single("kenney_fire" + UnityEngine.Random.Range(1, 3)) : null;
        if (fireSprite == null) return;
        deckFire = new GameObject("DeckFire");
        deckFire.transform.SetParent(hull, false);
        deckFire.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        deckFire.transform.localRotation = Quaternion.Inverse(Quaternion.Euler(0f, 0f, heading));   // tekne dönse de alev dikey kalır
        var fireRenderer = deckFire.AddComponent<SpriteRenderer>();
        fireRenderer.sprite = fireSprite;
        fireRenderer.sortingOrder = IslandFx.FxSorting - 3;
        deckFire.transform.localScale = Vector3.one * 1.1f;
        deckFire.transform.DOScale(Vector3.one * 1.6f, 0.22f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(deckFire);
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
        if (shipNumber != 0) IslandFx.Pulse("kenney_explosion" + UnityEngine.Random.Range(1, 4), transform.position, 1.9f, 0.7f);
        if (deckFire != null) Destroy(deckFire);
        if (drowned > 0)
            IslandFx.FloatText(transform.position + Vector3.up * 0.5f,
                L.T($"+{drowned} boğuldu", $"+{drowned} drowned"), new Color(0.6f, 0.85f, 1f));
        if (healthBar != null) healthBar.gameObject.SetActive(false);
        if (crewBadge != null) crewBadge.gameObject.SetActive(false);
        float d = Progression.Island.BoatSinkSeconds;
        // Batık kare (+18, gri); %70 ölçeğe küçülür ve saydamlaşır. Sandal yalnız yatar ve kaybolur.
        var catalog = IslandVisualCatalog.Instance;
        if (shipNumber != 0 && catalog != null)
        {
            var wreck = catalog.Single("ship_" + (shipNumber + 18));
            if (wreck != null) sr.sprite = wreck;
        }
        hull.DOScale(hull.localScale * 0.7f, d).SetLink(gameObject);
        hull.DOLocalRotate(new Vector3(0f, 0f, heading + (UnityEngine.Random.value < 0.5f ? 28f : -28f)), d).SetLink(gameObject);
        sr.DOFade(0f, d).SetDelay(0.1f).OnComplete(() => Destroy(gameObject)).SetLink(gameObject);
    }

    /// <summary>Test/telemetri: tekneyi hemen batır.</summary>
    public void SinkNow() { if (Alive) { Health = 0f; Sink(); } }
}
