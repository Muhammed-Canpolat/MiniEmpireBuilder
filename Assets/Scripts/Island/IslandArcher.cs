using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Okun hedefi: ada düşmanı ya da tekne (ikisi için ortak sorgular).</summary>
public sealed class IslandTarget
{
    public readonly IslandEnemy enemy;
    public readonly RaidBoat boat;
    public IslandTarget(IslandEnemy e) { enemy = e; }
    public IslandTarget(RaidBoat b) { boat = b; }

    public bool Valid => enemy != null ? enemy.Alive : boat != null && boat.Alive;
    public Vector2 Position => enemy != null ? enemy.AimPoint : boat != null ? boat.Position : Vector2.zero;
    public bool IsBoat => boat != null;
    public Transform Transform => enemy != null ? enemy.transform : boat != null ? boat.transform : null;

    public void Hit(float damage, bool charged, Vector2? from = null)
    {
        if (!Valid) return;
        if (enemy != null) enemy.Controller.TakeDamage(enemy.ArrowDamage(damage, charged, from), DamageSource.IslandArcher);
        else boat.TakeArrow(damage, charged);
    }

    /// <summary>Ateşli Ok isabeti (§ yetenekler): tekne %6/sn × 5 sn; birlik 0,5 × ok hasarı/sn × 3 sn.</summary>
    public void Ignite(float arrowDamage)
    {
        if (!Valid) return;
        IslandStats.Ignitions++;
        if (enemy != null) enemy.Ignite(Progression.Island.FireUnitSeconds, arrowDamage * Progression.Island.FireUnitArrowFractionPerSecond);
        else boat.Ignite(Progression.Island.FireBoatSeconds, Progression.Island.FireBoatFractionPerSecond);
    }

    public bool Is(IslandTarget other) => other != null && other.Transform == Transform && Transform != null;

    /// <summary>Canlı tüm hedefler (düşmanlar + tekneler).</summary>
    public static List<IslandTarget> Collect()
    {
        var list = new List<IslandTarget>();
        foreach (var e in IslandEnemy.All) if (e != null && e.Alive) list.Add(new IslandTarget(e));
        foreach (var b in RaidBoat.All) if (b != null && b.Alive) list.Add(new IslandTarget(b));
        return list;
    }
}

/// <summary>Savaş başına ada telemetrisi (bot/test/yenilgi etiketi için).</summary>
public static class IslandStats
{
    public static int Shots, AutoShots, ChargedShots, Taps, PostSwitches, Drowned, DrownedGold, CrewTotal, Landed, ArcherDeaths;
    public static float ArcherDamage;
    // Faz C: kime ne kadar hasar verildi (yenilgi etiketleri, §7.6)
    public static readonly Dictionary<EnemyType, float> DamageByType = new Dictionary<EnemyType, float>();
    public static readonly float[] CastleDamageByBeach = new float[4];
    public static int ShieldBlocks, GolemArmorHits, BuildingsLost, CrystalWeakHits, WebHits, Ignitions, AbilityUses;
    public static float MageHealed, VampireHealed;
    public static float BuildingDamage, ArcherDamageTaken, LancerDamageTaken, LancerDamageDealt;

    public static void Reset()
    {
        Shots = AutoShots = ChargedShots = Taps = PostSwitches = Drowned = DrownedGold = CrewTotal = Landed = ArcherDeaths = 0;
        ArcherDamage = 0f;
        DamageByType.Clear();
        System.Array.Clear(CastleDamageByBeach, 0, CastleDamageByBeach.Length);
        ShieldBlocks = GolemArmorHits = BuildingsLost = CrystalWeakHits = WebHits = Ignitions = AbilityUses = 0;
        MageHealed = VampireHealed = 0f;
        BuildingDamage = ArcherDamageTaken = LancerDamageTaken = LancerDamageDealt = 0f;
    }

    /// <summary>Düşman vuruşu (EnemyController, yalnız ada): kale / bina / okçu hasarı türe ve kumsala göre toplanır.</summary>
    public static void RecordEnemyHit(EnemyController enemy, Transform target, float damage)
    {
        if (enemy == null || target == null || damage <= 0f) return;
        if (target.GetComponent<IslandLancer>() != null) { LancerDamageTaken += damage; return; }   // Faz E1: yenilgi etiketi payına girmez
        DamageByType.TryGetValue(enemy.Type, out float total);
        DamageByType[enemy.Type] = total + damage;
        if (target.GetComponent<IslandArcher>() != null) { ArcherDamageTaken += damage; return; }
        var building = target.GetComponent<IslandBuilding>();
        if (building == null) return;
        if (building.IsCastle)
        {
            var island = enemy.GetComponent<IslandEnemy>();
            if (island != null && island.BeachIndex >= 0 && island.BeachIndex < CastleDamageByBeach.Length)
                CastleDamageByBeach[island.BeachIndex] += damage;
        }
        else
        {
            BuildingDamage += damage;
            // Faz D: canlı baskında üretim binasına ulaşan Fare kaçak sayılır (%2 çalar)
            if (enemy.Type == EnemyType.Rat && building.IsProduction && IslandSceneSetup.Instance != null && IslandSceneSetup.Instance.Raid != null)
                IslandSceneSetup.Instance.Raid.NoteRatTheft(enemy);
        }
    }
}

/// <summary>Hedefe güdümlü ok (ıska yok); dolu atış 3 hedefi deler.</summary>
public sealed class IslandArrow : MonoBehaviour
{
    private IslandTarget target;
    private Vector2 lastKnown, origin;
    private float damage;
    private bool charged, fire;

    public static void Fire(Vector2 from, IslandTarget target, float damage, bool charged, bool fire = false)
    {
        var go = IslandFx.NewArrow(fire ? new Color(1f, 0.5f, 0.15f) : charged ? new Color(1f, 0.85f, 0.35f) : Color.white, from);
        if (charged) go.transform.localScale = new Vector3(1.4f, 2f, 1f);
        if (fire) go.AddComponent<IslandFlameTrail>();   // alev izi
        var arrow = go.AddComponent<IslandArrow>();
        arrow.target = target;
        arrow.origin = from;
        arrow.lastKnown = target.Position;
        arrow.damage = damage;
        arrow.charged = charged;
        arrow.fire = fire;
    }

    private void Update()
    {
        if (target != null && target.Valid) lastKnown = target.Position;
        Vector2 position = transform.position;
        Vector2 next = Vector2.MoveTowards(position, lastKnown, Progression.Island.ArrowSpeed * Time.deltaTime);
        IslandFx.PointArrow(transform, next - position);
        transform.position = next;
        if (Vector2.Distance(next, lastKnown) > 0.2f) return;
        if (target != null && target.Valid) Impact();
        Destroy(gameObject);
    }

    private void Impact()
    {
        target.Hit(damage, charged, origin);
        AudioManager.Instance?.PlaySfx("arrowImpact");   // 10. oturum (D110)
        if (fire)
        {
            target.Ignite(damage);
            IslandFx.Play("explosion_1", target.Position, 0.9f, 18f);   // küçük alev patlaması
            IslandAbilityArt.Sfx("swordHit");
        }
        if (!charged) return;
        // Delme: hedefin ardındaki çizgide (≤0,7 u) en yakın 2 hedef daha.
        Vector2 dir = (target.Position - origin).normalized;
        var others = new List<(float along, IslandTarget t)>();
        foreach (var other in IslandTarget.Collect())
        {
            if (other.Is(target)) continue;
            Vector2 rel = other.Position - origin;
            float along = Vector2.Dot(rel, dir);
            if (along <= Vector2.Distance(origin, target.Position) - 0.1f || along > Progression.Island.ArcherRange + 3f) continue;
            float side = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
            if (side <= 0.7f) others.Add((along, other));
        }
        others.Sort((a, b) => a.along.CompareTo(b.along));
        for (int i = 0; i < others.Count && i < Progression.Island.ChargedPierce - 1; i++)
            others[i].t.Hit(damage, true, origin);
    }
}

/// <summary>
/// Okçu Kaptan (island_base.md §3): atış yerlerinde durur; ATEŞ dokunuşu / basılı tutma (dolu atış) / oto-ateş ile
/// hedefe güdümlü ok atar, P işaretine dokununca merdiven yolundan koşar. Hedef: kilit → kale tehlikesi → en yakın.
/// </summary>
public sealed class IslandArcher : MonoBehaviour
{
    public static IslandArcher Instance { get; private set; }

    public int Level { get; private set; } = 1;
    public float MaxHp { get; private set; }
    public float Hp { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public bool Running { get; private set; }
    public int CurrentPost { get; private set; }
    public bool AutoFire { get; set; }
    public bool Charging { get; private set; }
    public float Charge01 { get; private set; }
    public float RespawnLeft { get; private set; }
    public IslandTarget Locked { get; private set; }
    public Vector2 Position => transform.position;
    public float Range => IslandLayout.Posts[CurrentPost].Range;
    /// <summary>Ateş bekleme dolumu 0..1 (1 = az önce ateşlendi): ATEŞ düğmesi üstündeki dolum.</summary>
    public float CooldownFraction => Mathf.Clamp01((nextShot - Time.time) / Progression.Island.FireCooldown);
    public int KeepLevel { get; private set; } = 1;

    public event Action<int> PostChanged;
    public event Action<bool> Shot;          // (charged)
    public event Action Died;

    private SpriteRenderer sr;
    private IslandSpriteAnimator anim;
    private HealthBar healthBar;
    private Transform rangeRing;
    private Transform lockMarker;
    private float nextShot, nextAuto;
    private int queuedTaps;
    private bool holding;
    private float holdStart;
    private List<Vector2> path;
    private int pathIndex;
    private float pathSpeed;
    private int pendingPost = -1;
    private float archerScale = Progression.Island.UnitScale;
    private const float FeetPixels = 40f;   // Tiny Swords Archer: ayak çizgisi merkezden 40 px aşağıda

    public static IslandArcher Create(int level, int keepLevel, int startPost = 0)
    {
        var go = new GameObject("IslandArcher");
        var archer = go.AddComponent<IslandArcher>();
        archer.Init(level, keepLevel, startPost);
        return archer;
    }

    private void Init(int level, int keepLevel, int startPost)
    {
        Instance = this;
        Level = Mathf.Max(1, level);
        KeepLevel = Mathf.Max(1, keepLevel);
        MaxHp = Progression.Island.ArcherHp(Level);
        Hp = MaxHp;
        CurrentPost = Mathf.Clamp(startPost, 0, IslandLayout.Posts.Length - 1);
        transform.position = IslandLayout.Posts[CurrentPost].World;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0f, FeetPixels / 64f * archerScale, 0f);
        sr = visual.AddComponent<SpriteRenderer>();
        var catalog = IslandVisualCatalog.Instance;
        var frames = new IslandUnitFrames();
        if (catalog != null) { frames.idle = catalog.archerIdle; frames.run = catalog.archerRun; frames.attack = catalog.archerShoot; }
        if (frames.HasArt) sr.sprite = IslandVisualCatalog.Has(frames.idle) ? frames.idle[0] : frames.run[0];
        visual.transform.localScale = Vector3.one * archerScale;
        anim = visual.AddComponent<IslandSpriteAnimator>();
        anim.Setup(sr, frames);
        anim.AttackSeconds = 0.4f;
        // Ayak çizgisi köktedir: sıralama kök y'sinden (görsel çocuk ayaktan yukarıda durur).
        gameObject.AddComponent<IslandYSort>().bias = 8;

        // Menzil halkası çizilmez (v2: halka yok); yalnız açık/kapalı durumunu taşıyan boş düğüm kalır.
        var ring = new GameObject("RangeRing");
        ring.transform.SetParent(transform, false);
        rangeRing = ring.transform;

        // Hedef işareti: Tiny Swords imleç köşeleri (UI Elements/Cursors).
        var marker = new GameObject("LockMarker");
        var markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = catalog != null ? catalog.Single("cursor_brackets") : null;
        markerRenderer.color = new Color(1f, 0.35f, 0.3f, 0.95f);
        markerRenderer.sortingOrder = IslandFx.FxSorting + 2;
        lockMarker = marker.transform;
        lockMarker.localScale = Vector3.one * 0.6f;
        marker.SetActive(false);

        healthBar = HealthBar.Create(transform, MaxHp, new Vector3(0f, 1.5f, 0f), new Color(0.35f, 0.65f, 1f));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (healthBar != null) Destroy(healthBar.gameObject);
        if (lockMarker != null) Destroy(lockMarker.gameObject);
    }

    // ==================== GİRDİ ====================

    /// <summary>ATEŞ düğmesine basıldı.</summary>
    public void PressFire()
    {
        if (!IsAlive) return;
        holding = true;
        holdStart = Time.time;
    }

    /// <summary>ATEŞ düğmesi bırakıldı: dolu atış (tam dolduysa) ya da normal dokunuş.</summary>
    public void ReleaseFire()
    {
        if (!holding) return;
        holding = false;
        bool full = Charging && Charge01 >= 1f;
        Charging = false;
        Charge01 = 0f;
        if (!IsAlive || Running || Wandering) return;
        if (full) { Shoot(true, false); return; }
        IslandStats.Taps++;
        if (Time.time >= nextShot) Shoot(false, false);
        else if (queuedTaps < Progression.Island.TapBuffer) queuedTaps++;
    }

    /// <summary>Hedefe dokunuldu: kilitle (boş suya dokunmak kaldırır).</summary>
    public void LockTarget(IslandTarget target)
    {
        Locked = target != null && target.Valid ? target : null;
    }

    /// <summary>P işaretine dokunuldu: açıksa oraya koşar. Koşarken verilen son istek sıraya alınır.</summary>
    public bool MoveToPost(int post)
    {
        if (!IsAlive || post < 0 || post >= IslandLayout.Posts.Length) return false;
        if (!IslandLayout.PostUnlocked(post, KeepLevel)) return false;
        if (Wandering) EndWander(CurrentPost);   // D115: gezinmeden atış düzenine dön (yol hesabı atış yerinden başlar)
        if (Running) { pendingPost = post == CurrentPost ? -1 : post; return true; }
        if (post == CurrentPost) return false;
        BeginRun(post);
        return true;
    }

    private void BeginRun(int post)
    {
        path = IslandLayout.PathBetween(CurrentPost, post);
        float length = 0f;
        for (int i = 1; i < path.Count; i++) length += Vector2.Distance(path[i - 1], path[i]);
        pathSpeed = length / Mathf.Max(Progression.Island.MinPostSwitch, length / Progression.Island.ArcherRunSpeed);
        pathIndex = 1;
        pendingPost = -1;
        runTarget = post;
        Running = true;
        holding = false; Charging = false; Charge01 = 0f; queuedTaps = 0;
        if (anim != null) anim.ForceRunning = true;
        rangeRing.gameObject.SetActive(false);
        IslandStats.PostSwitches++;
    }

    private int runTarget;
    /// <summary>Koşarken gidilen atış yeri (yoksa -1): işaret vurgusu.</summary>
    public int TargetPost => Running ? (pendingPost >= 0 ? pendingPost : runTarget) : -1;

    // ==================== GÜNCELLEME ====================

    private void Update()
    {
        if (!IsAlive)
        {
            RespawnLeft -= Time.deltaTime;
            if (RespawnLeft <= 0f) Respawn();
            return;
        }
        if (Running) { UpdateRun(); return; }
        if (Wandering) { UpdateWander(); return; }

        ValidateLock();
        if (holding)
        {
            float held = Time.time - holdStart;
            Charging = held >= Progression.Island.ChargeStart;
            Charge01 = Charging ? Mathf.Clamp01((held - Progression.Island.ChargeStart)
                / (Progression.Island.ChargeFull - Progression.Island.ChargeStart)) : 0f;
        }
        if (queuedTaps > 0 && Time.time >= nextShot)
        {
            queuedTaps--;
            Shoot(false, false);
        }
        if (AutoFire && !holding && Time.time >= nextShot && Time.time >= nextAuto && Resolve() != null)
            Shoot(false, true);
        UpdateLockMarker();
    }

    private void UpdateRun()
    {
        Vector2 position = transform.position;
        Vector2 goal = path[pathIndex];
        Vector2 next = Vector2.MoveTowards(position, goal, pathSpeed * Time.deltaTime);
        transform.position = next;
        if (Vector2.Distance(next, goal) < 0.02f) pathIndex++;
        if (pathIndex < path.Count) return;
        Running = false;
        CurrentPost = runTarget;
        transform.position = IslandLayout.Posts[CurrentPost].World;
        if (anim != null) anim.ForceRunning = false;
        rangeRing.gameObject.SetActive(true);
        PostChanged?.Invoke(CurrentPost);
        if (pendingPost >= 0 && pendingPost != CurrentPost) MoveToPost(pendingPost);
    }

    private void ValidateLock()
    {
        if (Locked == null) return;
        if (!Locked.Valid || Vector2.Distance(Position, Locked.Position) > Range) Locked = null;
    }

    private void UpdateLockMarker()
    {
        bool show = Locked != null && Locked.Valid;
        if (lockMarker.gameObject.activeSelf != show) lockMarker.gameObject.SetActive(show);
        if (!show) return;
        lockMarker.position = Locked.Position;
        lockMarker.localScale = Vector3.one * (0.55f + 0.06f * Mathf.Sin(Time.time * 8f));
    }

    // ==================== HEDEF ====================

    /// <summary>Otomatik nişan sırası: kilitli (menzilde) → kaleye 3 u'dan yakın → menzildeki en yakın.</summary>
    public IslandTarget Resolve()
    {
        ValidateLock();
        if (Locked != null) return Locked;
        Vector2 castle = IslandLayout.CastleCenter;
        IslandTarget danger = null, nearest = null;
        float dangerBest = float.MaxValue, nearBest = float.MaxValue;
        foreach (var t in IslandTarget.Collect())
        {
            float d = Vector2.Distance(Position, t.Position);
            if (d > Range) continue;
            if (!t.IsBoat && Vector2.Distance(t.Position, castle) <= Progression.Island.CastleDangerRadius)
            {
                float toCastle = Vector2.Distance(t.Position, castle);
                if (toCastle < dangerBest) { dangerBest = toCastle; danger = t; }
            }
            if (d < nearBest) { nearBest = d; nearest = t; }
        }
        return danger ?? nearest;
    }

    // ==================== ATIŞ ====================

    private void Shoot(bool charged, bool auto)
    {
        var target = Resolve();
        if (target == null) return;
        float webbed = Time.time < webbedUntil ? Progression.Island.WebFireRateMult : 1f;   // Örümcek ağı: atış hızı −%40
        nextShot = Time.time + Progression.Island.FireCooldown / webbed;
        if (auto) nextAuto = Time.time + Progression.Island.FireCooldown / Progression.Island.AutoFireRateMult / webbed;
        float damage = Progression.Island.ArcherDamage(Level);
        if (charged) damage *= Progression.Island.ChargedMult;
        else if (UnityEngine.Random.value < Progression.Island.CritChance) damage *= Progression.Island.CritMult;
        IslandStats.Shots++;
        if (auto) IslandStats.AutoShots++;
        if (charged) IslandStats.ChargedShots++;
        IslandStats.ArcherDamage += damage;
        Vector2 from = (Vector2)transform.position + Vector2.up * 0.7f;
        bool fireShot = IslandAbilities.TakeFireShot();
        IslandArrow.Fire(from, target, damage, charged, fireShot);
        if (anim != null)
        {
            anim.Attack();
            // Hedefe dön (sprite sağa bakar).
            sr.flipX = target.Position.x < Position.x;
        }
        Shot?.Invoke(charged);
    }

    // ==================== HASAR / ÖLÜM ====================

    private float webbedUntil;
    public bool Webbed => Time.time < webbedUntil;

    /// <summary>Örümcek ağı: <paramref name="seconds"/> sn atış hızı −%40.</summary>
    public void Web(float seconds)
    {
        webbedUntil = Mathf.Max(webbedUntil, Time.time + seconds);
        IslandStats.WebHits++;
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        Hp = Mathf.Max(0f, Hp - amount);
        if (healthBar != null) healthBar.UpdateHealth(Hp);
        sr.DOKill();
        sr.color = new Color(1f, 0.5f, 0.5f);
        sr.DOColor(Color.white, 0.15f).SetLink(sr.gameObject);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(transform.position + Vector3.up * 0.6f, amount, amount);
        if (Hp <= 0f) Die();
    }

    /// <summary>Manastır şifası (Faz E1): tavanı aşmaz; gerçekten iyileştiyse true.</summary>
    public bool Heal(float amount)
    {
        if (!IsAlive || amount <= 0f || Hp >= MaxHp) return false;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        if (healthBar != null) healthBar.UpdateHealth(Hp);
        return true;
    }

    private void Die()
    {
        IsAlive = false;
        Running = false;
        holding = false; Charging = false; Charge01 = 0f; queuedTaps = 0;
        Locked = null;
        RespawnLeft = Progression.Island.ArcherRespawn;
        sr.enabled = false;
        rangeRing.gameObject.SetActive(false);
        lockMarker.gameObject.SetActive(false);
        if (healthBar != null) healthBar.gameObject.SetActive(false);
        IslandStats.ArcherDeaths++;
        IslandFx.FloatText(transform.position + Vector3.up * 0.4f, L.T("Okçu düştü!", "Archer down!"), new Color(1f, 0.5f, 0.4f));
        Died?.Invoke();
    }

    private void Respawn()
    {
        IsAlive = true;
        Hp = MaxHp;
        CurrentPost = 0;
        // D115: akın dışında (gezinme düzeni) Okçuluk Alanı'nda, akında eskisi gibi P1'de doğar.
        transform.position = Wandering ? (Vector3)WanderHome : (Vector3)IslandLayout.Posts[0].World;
        sr.enabled = true;
        sr.color = Color.white;
        rangeRing.gameObject.SetActive(!Wandering);
        if (healthBar != null) { healthBar.gameObject.SetActive(true); healthBar.SetMax(MaxHp); }
        PostChanged?.Invoke(0);
    }

    // ==================== BOŞTA GEZİNME (D115, madde 19/29) ====================
    // Akın dışında (sahneye giriş, akın sonrası) okçu kaptan kıyı kalesinin yanındaki P1'de dikilmez: Okçuluk Alanı'nın kapısı
    // çevresinde yürür/durur (yürüme = koşu kareleri yavaş, durma = bekleme kareleri). Akın başlayınca eskisi gibi P1'e geçer; savaş
    // davranışı (atış yerleri, hedefleme sırası, ölüm/doğuş) değişmez.

    public bool Wandering { get; private set; }
    public Vector2 WanderHome { get; private set; }
    /// <summary>Okçuluk Alanı kapısının güncel yeri (bina taşınırsa okçu oraya geçer); yoksa null.</summary>
    public Func<Vector2?> HomeProvider;
    public const float WanderSpeed = 1.1f, WanderRadius = 2.2f;
    private readonly List<Vector2> wanderSpots = new List<Vector2>();
    private Vector2? wanderTarget;
    private float wanderWaitUntil;

    public void BeginWander(Vector2 home)
    {
        Wandering = true;
        WanderHome = home;
        Running = false; path = null; pendingPost = -1;
        holding = false; Charging = false; Charge01 = 0f; queuedTaps = 0;
        Locked = null;
        transform.position = new Vector3(home.x, home.y, transform.position.z);
        if (anim != null) anim.ForceRunning = null;   // yürürken koşu kareleri, dururken bekleme kareleri
        if (rangeRing != null) rangeRing.gameObject.SetActive(false);
        if (lockMarker != null) lockMarker.gameObject.SetActive(false);
        BuildWanderSpots(home);
        wanderTarget = null;
        wanderWaitUntil = Time.time + UnityEngine.Random.Range(0.6f, 1.6f);
    }

    /// <summary>Gezinmeden atış düzenine: <paramref name="post"/> atış yerine geçer (akın başı; eskiden okçu zaten P1'de beklerdi).</summary>
    public void EndWander(int post = 0)
    {
        if (!Wandering) return;
        Wandering = false;
        wanderTarget = null;
        CurrentPost = Mathf.Clamp(post, 0, IslandLayout.Posts.Length - 1);
        transform.position = IslandLayout.Posts[CurrentPost].World;
        if (anim != null) anim.ForceRunning = false;
        if (rangeRing != null) rangeRing.gameObject.SetActive(IsAlive);
    }

    /// <summary>Kapı çevresinde, kapıyla aynı yükseklikte ve kapıdan düz çizgiyle yürünebilen noktalar (yar/su/bina içine girmez).</summary>
    private void BuildWanderSpots(Vector2 home)
    {
        wanderSpots.Clear();
        var grid = IslandWalkGrid.Shared;
        Vector2Int homeCell = grid.CellOf(home);
        int homeHeight = IslandLayout.HeightAt(homeCell.x, homeCell.y);
        int r = Mathf.CeilToInt(WanderRadius);
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            var cell = homeCell + new Vector2Int(dx, dy);
            Vector2 spot = grid.Center(cell);
            float d = Vector2.Distance(spot, home);
            if (d < 0.6f || d > WanderRadius || !grid.Walkable(cell) || IslandLayout.HeightAt(cell.x, cell.y) != homeHeight) continue;
            if (StraightWalkable(home, spot, homeHeight)) wanderSpots.Add(spot);
        }
    }

    private static bool StraightWalkable(Vector2 a, Vector2 b, int height)
    {
        var grid = IslandWalkGrid.Shared;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.2f));
        for (int i = 1; i <= steps; i++)
        {
            var cell = grid.CellOf(Vector2.Lerp(a, b, i / (float)steps));
            if (!grid.Walkable(cell) || IslandLayout.HeightAt(cell.x, cell.y) != height) return false;
        }
        return true;
    }

    private void UpdateWander()
    {
        if (wanderTarget == null)
        {
            if (Time.time < wanderWaitUntil) return;
            // Bina taşındıysa yeni kapıya geçilir.
            var home = HomeProvider?.Invoke();
            if (home.HasValue && Vector2.Distance(home.Value, WanderHome) > 0.5f) { BeginWander(home.Value); return; }
            // Her yürüyüş kapı ↔ nokta arasıdır (yalnız bu doğrular denetlendi): uzaktaysa kapıya döner, kapıdaysa rastgele noktaya.
            bool atHome = Vector2.Distance(transform.position, WanderHome) < 0.1f;
            if (!atHome) wanderTarget = WanderHome;
            else if (wanderSpots.Count > 0) wanderTarget = wanderSpots[UnityEngine.Random.Range(0, wanderSpots.Count)];
            else { wanderWaitUntil = Time.time + 3f; return; }
        }
        Vector2 position = transform.position;
        Vector2 next = Vector2.MoveTowards(position, wanderTarget.Value, WanderSpeed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        if (Vector2.Distance(next, wanderTarget.Value) > 0.02f) return;
        wanderTarget = null;
        wanderWaitUntil = Time.time + UnityEngine.Random.Range(1.4f, 3.6f);
    }

    /// <summary>Kareden kareye toplanmış hızlandırma testleri için: ok hemen vurur.</summary>
    public bool DebugFireAtNearest() { var t = Resolve(); if (t == null) return false; Shoot(false, false); return true; }
}
