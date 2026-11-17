using UnityEngine;
using DG.Tweening;

/// <summary>
/// Düşman davranışı — Ana Üs'e doğru yürür, yolda engel varsa saldırır
/// Öncelik: Duvar → Kule → Ana Üs
/// Eğer kahraman çok yakınsa ve saldırıyorsa ona da döner (opsiyonel)
/// </summary>
public class EnemyController : MonoBehaviour
{
    private const int EliteWaveInterval = 3;

    [Header("Statlar")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private float damage = 5f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int goldReward = 10;

    // Durum
    private float currentHealth;
    private float lastAttackTime;
    private bool isDead = false;
    private Transform currentTarget;

    // Bileşenler
    private SpriteRenderer spriteRenderer;
    private HealthBar healthBar;
    /// <summary>Can çubuğu (ayrı nesne; ada görseli konumunu/gizlenmesini buradan yönetir).</summary>
    public HealthBar Bar => healthBar;

    // Public erişim
    public bool IsDead => isDead;
    /// <summary>Son saldırı anı (EnemyMotion atılma animasyonu için).</summary>
    public float LastAttackTime => lastAttackTime;
    /// <summary>Son hasar alma anı (EnemyMotion geri tepme animasyonu için).</summary>
    public float LastHitTime { get; private set; }
    /// <summary>Saldırılan hedef noktası.</summary>
    public Vector2 AttackPoint => targetPoint;
    public EnemyType Type => enemyType;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => currentHealth / maxHealth;
    public float Damage => damage;
    public float AttackRange => attackRange;
    public float MoveSpeed => moveSpeed;
    public Transform CurrentTarget => currentTarget;

    // ==================== KARŞI DÜŞMAN ÇEŞİTLERİ / BOSS KALKANI (round2 C) ====================

    /// <summary>Bu savaşta kaynağa göre verilen hasar (telemetri). Savaş başında sıfırlanır.</summary>
    public static readonly System.Collections.Generic.Dictionary<DamageSource, float> DamageBySource =
        new System.Collections.Generic.Dictionary<DamageSource, float>();
    /// <summary>Bombacı sonuçları: "hero" (kahraman/Çırak öldürdü), "tower" (kule öldürdü), "detonated" (patladı).</summary>
    public static readonly System.Collections.Generic.Dictionary<string, int> SaboteurOutcomes =
        new System.Collections.Generic.Dictionary<string, int>();

    public static void ResetBattleStats()
    {
        DamageBySource.Clear();
        SaboteurOutcomes.Clear();
    }

    private static void CountSaboteur(string outcome)
    {
        SaboteurOutcomes.TryGetValue(outcome, out int n);
        SaboteurOutcomes[outcome] = n + 1;
    }

    public EnemyVariant Variant { get; private set; }
    public bool IsFlying => Variant == EnemyVariant.Flying;
    private bool IsSaboteur => Variant == EnemyVariant.Saboteur;

    private float wardMax, wardHp, wardTowerMult = 1f, exposedUntil, staggerUntil;
    private HealthBar wardBar;
    private SpriteRenderer wardBubble;
    public bool HasWard => wardMax > 0f;
    /// <summary>Boss kalkanı ayakta (kule hasarı azalır, kahraman kalkanı eritir).</summary>
    public bool WardActive => wardMax > 0f && wardHp > 0f && !IsExposed;
    public bool IsExposed => Time.time < exposedUntil;
    public bool IsStaggered => Time.time < staggerUntil;
    public int WardBreaks { get; private set; }
    public float WardHealth => wardHp;
    public float WardMax => wardMax;

    /// <summary>Bir boss kalkanı hiç kırılmadan ayakta mı (yenilgi ipucu için).</summary>
    public static bool AnyIntactWardAlive()
    {
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (enemy != null && !enemy.isDead && enemy.HasWard && enemy.WardBreaks == 0) return true;
        return false;
    }

    public static string VariantName(EnemyVariant variant) =>
        variant == EnemyVariant.Armored ? L.T("Zırhlı", "Armored")
        : variant == EnemyVariant.Flying ? L.T("Uçan", "Flying")
        : variant == EnemyVariant.Saboteur ? L.T("Bombacı", "Saboteur") : "";

    /// <summary>Kaynağa ve çeşide göre alınan hasar çarpanı.</summary>
    public float SourceMultiplier(DamageSource source)
    {
        switch (Variant)
        {
            case EnemyVariant.Armored:
                return source == DamageSource.Archer ? Progression.Counters.ArmoredArcherTaken
                    : source == DamageSource.Cannon ? Progression.Counters.ArmoredCannonTaken : 1f;
            case EnemyVariant.Saboteur:
                return source == DamageSource.Archer || source == DamageSource.Cannon
                    ? Progression.Counters.SaboteurTowerTaken : 1f;
            default: return 1f;
        }
    }

    /// <summary>ScaleToLevel'den sonra çağrılır: çeşidin çarpanları, renk ve boyut.</summary>
    public void ApplyVariant(EnemyVariant variant)
    {
        Variant = variant;
        if (variant == EnemyVariant.None) return;
        float hp = 1f, dmg = 1f, speed = 1f, gold = 1f, scale = 1f;
        Color? tint = null;
        switch (variant)
        {
            case EnemyVariant.Armored:
                hp = Progression.Counters.ArmoredHp; dmg = Progression.Counters.ArmoredDamage;
                speed = Progression.Counters.ArmoredSpeed; gold = Progression.Counters.ArmoredGold;
                scale = Progression.Counters.ArmoredScale; tint = Progression.Counters.ArmoredTint;
                break;
            case EnemyVariant.Flying:
                hp = Progression.Counters.FlyingHp; dmg = Progression.Counters.FlyingDamage;
                speed = Progression.Counters.FlyingSpeed; gold = Progression.Counters.FlyingGold;
                tint = Progression.Counters.FlyingTint;
                break;
            case EnemyVariant.Saboteur:
                hp = Progression.Counters.SaboteurHp; speed = Progression.Counters.SaboteurSpeed;
                gold = Progression.Counters.SaboteurGold; scale = Progression.Counters.SaboteurScale;
                tint = Progression.Counters.SaboteurTint;
                break;
        }
        maxHealth *= hp;
        currentHealth = maxHealth;
        damage *= dmg;
        moveSpeed *= speed;
        goldReward = Mathf.Max(1, Mathf.RoundToInt(goldReward * gold));
        if (healthBar != null) healthBar.SetMax(maxHealth);
        transform.localScale *= scale;
        if (spriteRenderer != null && tint.HasValue)
        {
            Color c = tint.Value;
            c.a = variant == EnemyVariant.Flying ? Progression.Counters.FlyingTintAlpha : spriteRenderer.color.a;
            spriteRenderer.color = c;
            BaseTint = c;
        }
        if (variant == EnemyVariant.Flying) SetupFlight();
    }

    private void SetupFlight()
    {
        if (GetComponent<EnemyFlightVisual>() == null)
            gameObject.AddComponent<EnemyFlightVisual>();
        float lift = Progression.Counters.FlyingYOffset;
        if (healthBar != null) healthBar.Offset += Vector3.up * lift;
        if (groundShadow != null)
        {
            // Gölge yerde kalır: kök yükselirken yerel konumu aşağı çekilir.
            float sy = Mathf.Max(0.01f, Mathf.Abs(transform.localScale.y));
            groundShadow.transform.localPosition -= new Vector3(0f, lift / sy, 0f);
            groundShadow.color = new Color(0f, 0f, 0f, Progression.Counters.FlyingShadowAlpha);
        }
    }

    private void SetupWard()
    {
        int region = Progression.Counters.WardRegion(enemyType);
        if (region <= 0) return;
        wardMax = maxHealth * Progression.Counters.WardFraction(region) * BattleCards.WardPoolMultiplier;   // Kalkan Kıran: havuz −%30
        wardHp = wardMax;
        wardTowerMult = Progression.Counters.WardTowerMult(region);
        wardBar = HealthBar.Create(transform, wardMax, new Vector3(0f, 0.88f, 0f), new Color(0.35f, 0.65f, 1f));
        wardBar.Resize(new Vector2(0.55f, 0.1f));
        if (spriteRenderer != null)
        {
            var go = new GameObject("WardBubble");
            go.transform.SetParent(transform, false);
            wardBubble = go.AddComponent<SpriteRenderer>();
            wardBubble.sprite = BaseMapManager.MakeCircleSprite(Color.white);
            Vector2 circle = wardBubble.sprite.bounds.size;
            // Sprite henüz yoksa (animatörlü Cadı) sabit bir dünya çapı kullanılır.
            Bounds b = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds
                : new Bounds(new Vector3(0f, 1f / Mathf.Max(0.01f, Mathf.Abs(transform.localScale.y)), 0f),
                    Vector3.one * (2.2f / Mathf.Max(0.01f, Mathf.Abs(transform.localScale.x))));
            float diameter = Mathf.Max(b.size.x, b.size.y) * 1.15f;
            go.transform.localPosition = new Vector3(b.center.x, b.center.y, 0f);
            go.transform.localScale = new Vector3(diameter / Mathf.Max(0.01f, circle.x), diameter / Mathf.Max(0.01f, circle.y), 1f);
            wardBubble.color = new Color(0.4f, 0.7f, 1f, 0.28f);
            wardBubble.sortingOrder = spriteRenderer.sortingOrder + 1;
            wardBubble.DOFade(0.14f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetLink(go);
        }
    }

    private void SetWardVisible(bool visible)
    {
        if (wardBubble != null) wardBubble.gameObject.SetActive(visible);
        if (wardBar != null) wardBar.gameObject.SetActive(visible);
    }

    private void UpdateWardTimers()
    {
        if (wardMax <= 0f || wardHp > 0f || IsExposed) return;
        // Açık pencere bitti: kalkan tam dolu yeniden oluşur.
        wardHp = wardMax;
        if (wardBar != null) wardBar.SetMax(wardMax);
        SetWardVisible(true);
    }

    private void BreakWard()
    {
        wardHp = 0f;
        WardBreaks++;
        exposedUntil = Time.time + Progression.Counters.WardExposedSeconds + BattleCards.WardExposedBonus;   // Kalkan Kıran: +2 sn
        staggerUntil = Time.time + Progression.Counters.WardStaggerSeconds;
        SetWardVisible(false);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnSpark(transform.position + Vector3.up * 0.8f);
    }

    // Özel davranışlar (Gölge Kurt atılması, Cadı ışınlanması) için
    public bool MovementLocked { get; set; }

    // ---- Martı Adası (Docs/design/island_base.md §3): rota kuyruğu. route == null iken hiçbir şey değişmez. ----
    private Vector2[] route;
    private int routeIndex;
    /// <summary>Düşman ölünce (ödül/istatistik için). Yalnız ada modu dinler.</summary>
    public event System.Action<EnemyController> Died;
    public int GoldValue => goldReward;
    /// <summary>Sabit ara noktalar: düşman sırayla bu noktalara yürür, kale menzile girince durur.</summary>
    public void SetRoute(Vector2[] points) { route = points; routeIndex = 0; }

    // ---- 7. oturum (D93) → 8. oturum (D96): sefer haritası yolu, durum makinesi. lane == null iken eski davranış (serbest yürüyüş). ----
    /// <summary>Yol durumları: Yolu izle → Hedefe kilitlen → Savaş → Yola dön (ileriden katılır).</summary>
    public enum LaneState { FollowLane, Lock, Fight, Rejoin }

    private BattleRoute lane;
    /// <summary>Yolda kat edilen mesafe (birim). Yalnız ARTAR: sapma, savaş ve dönüş boyunca asla geri gitmez.</summary>
    private float laneS;
    private float laneOffset, sepOffset;
    private float rejoinS, lockS, stateSince, relockAfter;
    private LaneState laneState = LaneState.FollowLane;

    /// <summary>Kilitlenme menzili: hedef yolun (düşmanın ilerleme noktası çevresi) bu kadar yakınındaysa düşman ona sapar.</summary>
    public const float LaneLeash = 3.0f;
    /// <summary>Bırakma (leash) mesafesi: hedef ya da düşman yoldan bu kadar uzaklaşırsa takip biter, düşman ileriden yola döner.</summary>
    public const float LeashDistance = 4.5f;
    /// <summary>Hareketli hedef (kahraman/Çırak/Lancer) peşinde yol boyunca en çok bu kadar ileri gidilir; haritanın sonuna kadar kovalamaz.</summary>
    public const float ChaseAhead = 6f;
    /// <summary>Düşmanın gerisinde kalan hedefe (bu paydan fazla) kilitlenmez: geriye koşmaz.</summary>
    public const float BackTolerance = 0.75f;
    private const float RelockCooldown = 1.2f;   // bıraktıktan sonra yeniden kilitlenme beklemesi (titreme olmasın)
    private const float ProgressWindow = 8f;     // ilerleme izdüşümü penceresi (paralel kola atlamaz)

    public BattleRoute Lane => lane;
    public LaneState LaneMode => laneState;
    /// <summary>Yolda kat edilen mesafe (birim, yalnız artar).</summary>
    public float LaneDistance => laneS;
    /// <summary>Son yola dönüşte: ayrıldığı ilerleme ve katıldığı ilerleme (test/telemetri).</summary>
    public float LastLeaveS { get; private set; } = -1f;
    public float LastJoinS { get; private set; } = -1f;
    public int RejoinCount { get; private set; }
    /// <summary>Yolda ilerleyiş (0 köprü … 1 Ana Üs).</summary>
    public float LaneProgress => lane == null ? 0f : laneS / Mathf.Max(0.01f, lane.Length);
    /// <summary>Yoldaki konumu (şerit kayması + ayrışma dahil).</summary>
    public Vector2 LanePoint => lane == null ? (Vector2)transform.position : LanePointAt(laneS);
    private Vector2 LanePointAt(float s) => lane.PointAt(s) + lane.NormalAt(s) * (laneOffset + sepOffset);

    /// <summary>Yolu izle: konum yola izdüşürülür; <paramref name="offset"/> şerit kayması (yan yana yürüyüş, ±0,35).</summary>
    public void SetLane(BattleRoute path, float offset)
    {
        lane = path;
        laneOffset = offset;
        laneS = path != null ? path.Project(transform.position, out _) : 0f;
        laneState = LaneState.FollowLane;
        stateSince = Time.time;
    }

    /// <summary>Konumun bu düşmanın ilerleme penceresindeki izdüşümü (yalnız ileri).</summary>
    private float LocalProgress(Vector2 p, out float lateral) => lane.ProjectRange(p, laneS, laneS + ProgressWindow, out lateral);

    /// <summary>
    /// Hedef yol kenarında ve İLERİDE mi: yolun düşmanın ilerleme noktası çevresine (geri BackTolerance, ileri pencere) izdüşümü
    /// <paramref name="reach"/> içinde. Yol yoksa hep true.
    /// </summary>
    private bool InLeash(Vector2 point, float reach)
    {
        if (lane == null) return true;
        if (Time.time < relockAfter) return false;
        float s = lane.ProjectRange(point, laneS - BackTolerance, laneS + ProgressWindow, out float lateral);
        return lateral <= reach && s >= laneS - BackTolerance;
    }

    private bool NearLane(Vector2 point, float extra = 0f) => InLeash(point, LaneLeash + extra);

    private void SetLaneState(LaneState next)
    {
        if (laneState == next) return;
        if ((next == LaneState.Lock || next == LaneState.Fight) && (laneState == LaneState.FollowLane || laneState == LaneState.Rejoin)) { lockS = laneS; LastLeaveS = laneS; }
        if (next == LaneState.Rejoin) rejoinS = laneS;
        laneState = next;
        stateSince = Time.time;
    }

    /// <summary>Can ve hasarı çarpanla ölçekler (ada Akın ölçeği, AŞAMA 2 §4); can dolu başlar.</summary>
    public void ScaleStats(float hpMult, float damageMult)
    {
        if (Mathf.Approximately(hpMult, 1f) && Mathf.Approximately(damageMult, 1f)) return;
        maxHealth = Mathf.Max(1f, maxHealth * hpMult);
        currentHealth = maxHealth;
        damage *= damageMult;
        if (healthBar != null) healthBar.SetMax(maxHealth);
    }
    public bool HasRoute => route != null;
    /// <summary>Alınan hasar çarpanı (ör. Kale Muhafızı'nın Kalkan Duvarı 0.3).</summary>
    public float DamageTakenMultiplier { get; set; } = 1f;
    /// <summary>Asıl renk: vurulma parlaması bitince buna döner (üst üste darbede kırmızı kalmasın).</summary>
    public Color BaseTint { get; set; } = Color.white;
    public bool Invulnerable { get; set; }
    // true iken FogOfWar alfa değerine dokunmaz (davranış kendi soluklaşmasını yönetir).
    public bool FogOverride { get; set; }

    /// <summary>Martı Adası: true iken ölüm efektini (kırmızı/küçülme) <see cref="Died"/> dinleyicisi (IslandEnemy) üstlenir.</summary>
    public bool SuppressDeathEffect { get; set; }
    /// <summary>Geçici hız çarpanı (Kasap Kaptan "Nara"); 1 = normal.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Çağrılmış birimler altın düşürmez; boss ile altın kasılmasını önler.</summary>
    public void SuppressGoldReward() => goldReward = 0;

    /// <summary>Havuza dönerken: ayrı nesne olan can/kalkan çubuklarını kaldırır (sahibi yaşamaya devam ettiği için kendileri silinmez).</summary>
    public void DetachExternals()
    {
        if (healthBar != null) Destroy(healthBar.gameObject);
        if (wardBar != null) Destroy(wardBar.gameObject);
        healthBar = null; wardBar = null;
    }
    /// <summary>Öldürme altını (D97: dalga düşmanı, eski toplam altına eşitleme).</summary>
    public void SetGoldReward(int gold) => goldReward = Mathf.Max(0, gold);

    // Hedef referansları (BattleManager tarafından atanır)
    private Transform mainBaseTransform;
    private EnemyType enemyType;
    private int battleLevelValue;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
    }

    /// <summary>
    /// Düşmanı başlat — BattleManager spawn ederken çağırır
    /// </summary>
    public void Initialize(EnemyType type, int battleLevel, Transform baseTarget)
    {
        enemyType = type;
        battleLevelValue = battleLevel;
        mainBaseTransform = baseTarget;
        currentTarget = baseTarget;

        // spriteRenderer'ı hemen al (Start'tan önce çağrılabilir)
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        // Statları hesapla
        EnemyStats stats = EnemyStats.GetBaseStats(type);
        stats.ScaleToLevel(battleLevel);

        maxHealth = stats.health;
        currentHealth = maxHealth;
        damage = stats.damage;
        moveSpeed = stats.moveSpeed;
        attackRange = stats.attackRange;
        attackCooldown = 1f / stats.attackSpeed;
        goldReward = stats.goldReward;

        // Can barı oluştur
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 0.6f, 0), new Color(0.8f, 0.15f, 0.15f));

        // Düşman tipine göre sprite ve boyut
        if (spriteRenderer != null)
        {
            // SpriteManager varsa gerçek sprite kullan
            if (SpriteManager.Instance != null)
            {
                Sprite enemySprite = SpriteManager.Instance.GetEnemySprite(type);
                if (enemySprite != null)
                {
                    spriteRenderer.sprite = enemySprite;
                    spriteRenderer.color = Color.white; // Gerçek sprite — tint yok
                }
            }

            // Imported enemy art has different source dimensions. Size by its
            // actual sprite bounds so each enemy stays readable on a phone.
            bool hasEnemyArt = spriteRenderer.sprite != null &&
                spriteRenderer.sprite.name.StartsWith("enemy_");
            if (hasEnemyArt)
            {
                float targetWidth = EnemyTypeInfo.IsRegionBoss(type) ? 2.9f :
                    type == EnemyType.Troll ? 2.5f :
                    type == EnemyType.ShadowWolf ? 1.6f :
                    type == EnemyType.Orc ? 1.85f :
                    type == EnemyType.Wolf ? 1.75f : 1.6f;
                float scale = targetWidth / Mathf.Max(0.01f, spriteRenderer.sprite.bounds.size.x);
                transform.localScale = new Vector3(scale, scale, 1f);
                if (type == EnemyType.ShadowWolf)
                    spriteRenderer.color = new Color(0.42f, 0.36f, 0.58f);
                if (EnemyTypeInfo.IsRegionBoss(type))
                    spriteRenderer.color = RegionBoss.Tint(type);
                // Tek kareli görsel: yürüme, saldırı ve vurulma kodla canlandırılır.
                if (GetComponent<EnemyMotion>() == null) gameObject.AddComponent<EnemyMotion>();
            }
            else switch (type)
            {
                case EnemyType.Wolf:
                    if (SpriteManager.Instance == null)
                        spriteRenderer.color = new Color(0.55f, 0.45f, 0.35f);
                    transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                    break;
                case EnemyType.Zombie:
                    if (SpriteManager.Instance == null)
                        spriteRenderer.color = new Color(0.35f, 0.55f, 0.3f);
                    transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                    break;
                case EnemyType.Orc:
                    if (SpriteManager.Instance == null)
                        spriteRenderer.color = new Color(0.3f, 0.5f, 0.15f);
                    transform.localScale = new Vector3(0.65f, 0.65f, 1f);
                    break;
                case EnemyType.Troll:
                    if (SpriteManager.Instance == null)
                        spriteRenderer.color = new Color(0.5f, 0.2f, 0.5f);
                    transform.localScale = new Vector3(0.8f, 0.8f, 1f); // Boss büyük
                    break;
            }
        }

        EnemyVisuals visuals = GetComponent<EnemyVisuals>();
        if (visuals != null)
        {
            int finalWave = Mathf.Max(1, Mathf.CeilToInt(battleLevelValue / 2f) + 2);
            int currentWave = BattleManager.Instance != null ? BattleManager.Instance.CurrentWave : 1;
            bool isBoss = EnemyTypeInfo.IsBoss(type) &&
                (BattleSession.Level?.kind == CampaignLevelKind.Boss
                    || currentWave >= finalWave);
            bool isElite = !isBoss && currentWave % EliteWaveInterval == 0;
            visuals.ApplyTier(isElite, isBoss);
            if (isBoss) AudioManager.Instance?.PlayBossBgm();
        }

        AddGroundShadow();

        if (type == EnemyType.ShadowWolf && GetComponent<ShadowWolfBehaviour>() == null)
            gameObject.AddComponent<ShadowWolfBehaviour>();
        if (type == EnemyType.ForestWitch && GetComponent<ForestWitchBoss>() == null)
            gameObject.AddComponent<ForestWitchBoss>();
        if (type == EnemyType.DragonWarrior && GetComponent<DragonBoss>() == null)
            gameObject.AddComponent<DragonBoss>();
        if (EnemyTypeInfo.IsRegionBoss(type) && GetComponent<RegionBoss>() == null)
            gameObject.AddComponent<RegionBoss>();
        if (spriteRenderer != null) BaseTint = spriteRenderer.color;
        SetupWard();
    }

    private const float ShadowAlpha = 0.38f;
    private SpriteRenderer groundShadow;

    /// <summary>Ayak altında yumuşak gölge: düşman zeminden ayrışır.</summary>
    private void AddGroundShadow()
    {
        if (groundShadow != null || spriteRenderer == null || spriteRenderer.sprite == null) return;
        if (!spriteRenderer.sprite.name.StartsWith("enemy_")) return; // Cadı animatörle çizilir
        Bounds local = spriteRenderer.sprite.bounds;
        var shadow = new GameObject("GroundShadow");
        shadow.transform.SetParent(transform, false);
        shadow.transform.localPosition = new Vector3(local.center.x, local.min.y + local.size.y * 0.12f, 0.01f);
        groundShadow = shadow.AddComponent<SpriteRenderer>();
        groundShadow.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        Vector2 circle = groundShadow.sprite.bounds.size;
        shadow.transform.localScale = new Vector3(local.size.x * 0.62f / Mathf.Max(0.01f, circle.x),
            local.size.y * 0.16f / Mathf.Max(0.01f, circle.y), 1f);
        groundShadow.color = new Color(0f, 0f, 0f, ShadowAlpha);
        groundShadow.sortingOrder = spriteRenderer.sortingOrder - 1;
    }

    /// <summary>FogOfWar tarafından çağrılır; yalnız görseli etkiler.</summary>
    public void SetFogAlpha(float alpha)
    {
        if (groundShadow != null) groundShadow.color = new Color(0f, 0f, 0f, ShadowAlpha * alpha);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }
        if (healthBar != null)
        {
            bool show = alpha > 0.5f;
            if (healthBar.gameObject.activeSelf != show) healthBar.gameObject.SetActive(show);
        }
    }

    private void Update()
    {
        if (isDead) return;

        UpdateWardTimers();
        FindBestTarget();
        if (IsSaboteur) { UpdateSaboteur(); return; }
        if (IsStaggered) return; // Kalkan kırıldı: sersemleme (hareket ve saldırı yok)
        if (MovementLocked) return;
        MoveToTarget();
        TryAttack();
        TickWatchdog();
    }

    // ==================== KİLİTLENME ÖNLEYİCİ (D116) ====================
    // Ortak güvenlik ağı (ada + sefer): vuruş menzili dışındayken Progression.EnemyWatchdog.StuckSeconds boyunca hedefine
    // MinProgress kadar yaklaşamayan (ya da sefer yolunda ilerleyemeyen) veya hedefsiz düşman FallbackSeconds boyunca yedek
    // hedefe (Ana Üs/Kale) yönelir. Ada: kaleye rota/düz çizgiyle (engel tanımaz); sefer yolu: D96'nın bırakma kuralıyla aynı
    // biçimde yan hedef bırakılır ve ileriden yola dönülür (yolu izleyen düşmana dokunulmaz → D96 davranışı değişmez).

    private float watchStart = -1f, watchDistance, watchLaneS, lastWatchTick = -1f, fallbackUntil = -1f;
    /// <summary>Watchdog bu düşmanı kaç kez yedek hedefe yönlendirdi (test/telemetri).</summary>
    public int WatchdogTrips { get; private set; }
    /// <summary>Tüm düşmanlarda toplam watchdog tetiklemesi (savaş başına sıfırlanmaz; test/telemetri).</summary>
    public static int TotalWatchdogTrips { get; private set; }
    /// <summary>Yedek hedefe zorlanmış durumda mı.</summary>
    public bool InFallback => Time.time < fallbackUntil;

    private Vector2 FallbackPoint
    {
        get
        {
            if (mainBaseTransform == null) return transform.position;
            var structure = mainBaseTransform.GetComponent<BattleStructure>();
            return structure != null ? structure.BasePoint : (Vector2)mainBaseTransform.position;
        }
    }

    /// <summary>Düşmanı hemen yedek hedefe yönlendirir (watchdog ya da dalga güvenlik ağı çağırır).</summary>
    public void ForceFallback()
    {
        if (isDead || mainBaseTransform == null) return;
        fallbackUntil = Time.time + Progression.EnemyWatchdog.FallbackSeconds;
        WatchdogTrips++;
        TotalWatchdogTrips++;
        SetTarget(mainBaseTransform, FallbackPoint, route != null ? IslandLayout.CastleRadius : BaseRadius);
        nextRetarget = Time.time + RetargetInterval;
        if (lane != null)
        {
            relockAfter = Time.time + Progression.EnemyWatchdog.FallbackSeconds;
            if (laneState == LaneState.Lock || laneState == LaneState.Fight) SetLaneState(LaneState.Rejoin);
        }
        watchStart = -1f;
    }

    private void TickWatchdog()
    {
        float now = Time.time;
        bool continuous = now - lastWatchTick <= 0.5f;   // sersemleme/kilit/kök gibi duraklamalar pencereyi yeniden başlatır
        lastWatchTick = now;
        bool paused = IsSaboteur || SpeedMultiplier <= 0.01f || InFallback;
        bool engaged = currentTarget != null && DistanceToTarget <= attackRange;
        // Sefer yolunda Ana Üs'e giden (yolu izleyen) düşman: D96 kalabalık/ayrışma düzenine karışılmaz.
        // Ada: kaleye giden düşman rotada/düz çizgide engel tanımadan yürür (takılamaz); rota kaleden uzaklaşan kesimlerde yanlış alarm vermesin.
        bool laneToBase = (lane != null || route != null) && currentTarget == mainBaseTransform;
        if (!continuous || paused || engaged || laneToBase || now - lastAttackTime < 1.5f) { watchStart = -1f; return; }
        float distance = currentTarget != null ? DistanceToTarget : float.MaxValue;
        if (watchStart < 0f || (currentTarget != null && watchDistance - distance >= Progression.EnemyWatchdog.MinProgress)
            || (lane != null && laneS - watchLaneS >= Progression.EnemyWatchdog.MinProgress))
        {
            watchStart = now; watchDistance = distance; watchLaneS = lane != null ? laneS : 0f;
            return;
        }
        if (now - watchStart < Progression.EnemyWatchdog.StuckSeconds) return;
        ForceFallback();
    }

    // ---- Bombacı: en yakın kuleye koşar, fitil yanar, patlar ----
    private float fuseEndsAt = -1f;
    private Transform fuseTarget;

    private static BattleStructure NearestTower(Vector2 from)
    {
        BattleStructure nearest = null;
        float best = float.MaxValue;
        foreach (var structure in BattleStructure.All)
        {
            if (structure == null || structure.IsDestroyed) continue;
            if (structure.Type != BuildingType.ArcherTower && structure.Type != BuildingType.CannonTower) continue;
            float d = Vector2.Distance(from, structure.BasePoint);
            if (d < best) { best = d; nearest = structure; }
        }
        return nearest;
    }

    private void UpdateSaboteur()
    {
        if (fuseEndsAt >= 0f)
        {
            if (Time.time >= fuseEndsAt) Detonate();
            return;
        }
        if (MovementLocked) return;
        if (currentTarget != null && DistanceToTarget <= attackRange)
        {
            fuseEndsAt = Time.time + Progression.Counters.SaboteurFuseSeconds;
            fuseTarget = currentTarget;
            if (spriteRenderer != null)
                spriteRenderer.DOColor(new Color(1f, 0.9f, 0.4f), 0.15f).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
            if (BattleVfxManager.Instance != null)
                BattleVfxManager.Instance.SpawnSpark(transform.position + Vector3.up * 0.5f);
            return;
        }
        MoveToTarget();
    }

    private void Detonate()
    {
        if (isDead) return;
        CountSaboteur("detonated");
        if (fuseTarget != null)
        {
            var structure = fuseTarget.GetComponent<BattleStructure>();
            var baseCtrl = fuseTarget.GetComponent<MainBaseController>();
            if (structure != null && !structure.IsDestroyed)
                structure.TakeDamage(structure.MaxHealth * Progression.Counters.SaboteurTowerDamageFraction);
            else if (baseCtrl != null)
                baseCtrl.TakeDamage(baseCtrl.MaxHealth * Progression.Counters.SaboteurBaseDamageFraction);
        }
        if (BattleVfxManager.Instance != null)
        {
            BattleVfxManager.Instance.SpawnSpark(transform.position);
            BattleVfxManager.Instance.TryScreenShake(25f);
        }
        TinyFx.Explosion(transform.position);
        goldReward = 0; // Patlayan Bombacı altın düşürmez
        if (spriteRenderer != null) spriteRenderer.DOKill();
        currentHealth = 0f;
        Die();
    }

    // ==================== HEDEF BULMA ====================

    // 8. oturum (D97 performans): her düşman saniyede ~4 kez hedef arıyordu; kahraman/Çırak sahnede aranmak yerine önbellekten
    // (yok edilince Unity null'ı yeniden arar; bulunamazsa aynı karede tekrar aranmaz).
    private static HeroController cachedHero;
    private static BattleApprentice cachedApprentice;
    private static int heroMissFrame = -1, apprenticeMissFrame = -1;
    private static HeroController CachedHero
    {
        get
        {
            if (cachedHero != null || heroMissFrame == Time.frameCount) return cachedHero;
            cachedHero = FindFirstObjectByType<HeroController>();
            if (cachedHero == null) heroMissFrame = Time.frameCount;
            return cachedHero;
        }
    }
    private static BattleApprentice CachedApprentice
    {
        get
        {
            if (cachedApprentice != null || apprenticeMissFrame == Time.frameCount) return cachedApprentice;
            cachedApprentice = FindFirstObjectByType<BattleApprentice>();
            if (cachedApprentice == null) apprenticeMissFrame = Time.frameCount;
            return cachedApprentice;
        }
    }

    /// <summary>Düşman rolü: kime öncelik verdiği (tasarım tablosu).</summary>
    public enum Role { Hunter, Besieger, Breaker, Siege }

    public Role EnemyRole => enemyType == EnemyType.Wolf || enemyType == EnemyType.ShadowWolf ? Role.Hunter
        : enemyType == EnemyType.Zombie ? Role.Besieger
        : enemyType == EnemyType.Orc ? Role.Breaker
        : Role.Siege;

    private const float HunterAggro = 3f;      // Avcı: kahraman/Çırak'a atılma mesafesi
    private const float BesiegerReach = 4f;    // Kuşatıcı: yolundaki kuleyi fark etme
    private const float BreakerReach = 5.5f;   // Yıkıcı: kuleleri arama
    private const float ProvokeSeconds = 3f;   // Kahraman vurunca ona dönme süresi
    private const float RetargetInterval = 0.25f;
    private const float AllyLancerAggroMaxRange = 2.5f;   // bundan uzun menzilli düşman Lancer'ı hedeflemez
    private const float BaseRadius = 1.2f;     // Ana Üs görseli geniş: kenarından vurulur
    private const float StructureRadius = 0.55f;

    private Vector2 targetPoint;
    private float targetRadius;
    private float nextRetarget;
    private float provokedUntil;

    /// <summary>Kahraman bu düşmana vurdu: kısa süre kahramana döner (oyuncu kuleyi koruyabilir).</summary>
    public void Provoke() => provokedUntil = Time.time + ProvokeSeconds;

    private bool TargetStillValid()
    {
        if (currentTarget == null) return false;
        if (route != null)
        {
            var islandArcher = currentTarget.GetComponent<IslandArcher>();
            if (islandArcher != null) return islandArcher.Targetable;
        }
        var structure = currentTarget.GetComponent<BattleStructure>();
        if (structure != null) return !structure.IsDestroyed;
        var heroTarget = currentTarget.GetComponent<HeroController>();
        if (heroTarget != null) return heroTarget.CurrentHealth > 0f;
        var apprentice = currentTarget.GetComponent<BattleApprentice>();
        if (apprentice != null) return apprentice.CurrentHealth > 0f;
        var allyTarget = currentTarget.GetComponent<AllyLancer>();
        if (allyTarget != null) return allyTarget.Alive;
        return true;
    }

    private void FindBestTarget()
    {
        if (Time.time < nextRetarget && TargetStillValid())
        {
            RefreshTargetPoint();
            return;
        }
        nextRetarget = Time.time + RetargetInterval * UnityEngine.Random.Range(0.8f, 1.2f);
        Vector2 position = transform.position;
        // D116 watchdog: takılı düşman yedek hedefe (Ana Üs/Kale) kilitli kalır (sefer yolunda D96 bırakma kuralıyla aynı: Ana Üs'e dön).
        if (InFallback && mainBaseTransform != null)
        {
            SetTarget(mainBaseTransform, FallbackPoint, route != null ? IslandLayout.CastleRadius : BaseRadius);
            return;
        }
        if (route != null) { FindIslandTarget(position); return; }
        if (IsSaboteur)
        {
            // Bombacı: kışkırtmayı yok sayar; kuleyi (her mesafeden) ya da kule yoksa Ana Üs'ü hedefler.
            if (fuseEndsAt >= 0f) return;
            var tower = NearestTower(position);
            if (tower != null && !NearLane(tower.BasePoint, 0.8f)) tower = null;   // 7. oturum: yalnız yol kenarındaki kule
            if (tower != null) SetTarget(tower.transform, tower.BasePoint, StructureRadius);
            else if (mainBaseTransform != null) SetTarget(mainBaseTransform, mainBaseTransform.position, BaseRadius);
            return;
        }
        HeroController hero = CachedHero;
        bool heroAlive = hero != null && hero.CurrentHealth > 0f;
        float heroDistance = heroAlive ? Vector2.Distance(position, hero.transform.position) : float.MaxValue;

        // Ortak kurallar: kışkırtılmışsa kahraman; kahraman dibindeyse (vuruş menzili) kahraman.
        if (heroAlive && (Time.time < provokedUntil || heroDistance <= attackRange + 0.3f) && NearLane(hero.transform.position, 0.5f))
        {
            SetTarget(hero.transform, hero.transform.position, 0f);
            return;
        }
        // Kışla Lancer'ı (Faz F): yakın dövüşçü, vuruş menzilindeki Lancer'a vurur (menzilliler ve uçanlar Ana Üs'e devam eder).
        if (attackRange <= AllyLancerAggroMaxRange && !IsFlying)
        {
            var ally = AllyLancer.NearestTo(position, attackRange + 0.3f);
            if (ally != null && NearLane(ally.Position, 0.5f)) { SetTarget(ally.transform, ally.Position, 0f); return; }
        }

        switch (EnemyRole)
        {
            case Role.Hunter:
            {
                Transform prey = null;
                float best = HunterAggro;
                if (heroAlive && heroDistance <= best && NearLane(hero.transform.position)) { prey = hero.transform; best = heroDistance; }
                var apprentice = CachedApprentice;
                if (apprentice != null && apprentice.CurrentHealth > 0f)
                {
                    float d = Vector2.Distance(position, apprentice.transform.position);
                    if (d < best && NearLane(apprentice.transform.position)) { prey = apprentice.transform; best = d; }
                }
                if (prey != null) { SetTarget(prey, prey.position, 0f); return; }
                break;
            }
            case Role.Besieger:
            case Role.Breaker:
            {
                float reach = EnemyRole == Role.Breaker ? BreakerReach : BesiegerReach;
                var structure = NearestStructure(position, reach);
                if (structure != null && NearLane(structure.BasePoint, 0.8f)) { SetTarget(structure.transform, structure.BasePoint, StructureRadius); return; }
                break;
            }
            case Role.Siege:
            {
                // Boss üsse yürür; yolunun üstündeki kuleyi ezer.
                var structure = NearestStructure(position, attackRange + 1f);
                if (structure != null && NearLane(structure.BasePoint, 0.8f)) { SetTarget(structure.transform, structure.BasePoint, StructureRadius); return; }
                break;
            }
        }
        // "Madeni koru": yağmacı düşman, maden ayaktayken üs yerine madene yürür.
        if (RaidTarget != null && !RaidTarget.IsDestroyed)
        {
            SetTarget(RaidTarget.transform, RaidTarget.BasePoint, StructureRadius);
            return;
        }
        if (mainBaseTransform != null)
            SetTarget(mainBaseTransform, mainBaseTransform.position, BaseRadius);
    }

    /// <summary>Ada: Kıyı Okçusu menzildeki okçuyu vurur; diğerleri kaleye yürür.</summary>
    private void FindIslandTarget(Vector2 position)
    {
        // v2 Faz C: rol tabanlı hedefleme (island_base_v2 §7.5); sonuç yoksa aşağıdaki v1 kuralı.
        var picked = IslandTargeting.Pick(enemyType, position, mainBaseTransform, attackRange);
        if (picked.target != null) { SetTarget(picked.target, picked.point, picked.radius); return; }
        var archer = IslandArcher.Instance;
        if (enemyType == EnemyType.ShoreArcher && archer != null && archer.IsAlive &&
            Vector2.Distance(position, archer.Position) <= attackRange + 0.5f)
        {
            SetTarget(archer.transform, archer.Position, 0.3f);
            return;
        }
        if (mainBaseTransform != null)
        {
            var castle = mainBaseTransform.GetComponent<BattleStructure>();
            SetTarget(mainBaseTransform, castle != null ? castle.BasePoint : (Vector2)mainBaseTransform.position,
                IslandLayout.CastleRadius);
        }
    }

    /// <summary>"Madeni koru" aşamasında bu düşmanın asıl hedefi olan maden (yoksa null: Ana Üs).</summary>
    public BattleStructure RaidTarget { get; set; }

    private void SetTarget(Transform target, Vector2 point, float radius)
    {
        currentTarget = target;
        targetPoint = point;
        targetRadius = radius;
        // 5. oturum (1.2): yapının yürünemez alanı görselden büyüdü; vuruş mesafesi alanın kenarından da ölçülür.
        targetObstacle = null;
        if (target != null)
        {
            var structure = target.GetComponent<BattleStructure>();
            if (structure != null) targetObstacle = structure;
            else
            {
                var castle = target.GetComponent<MainBaseController>();
                if (castle != null) targetObstacle = castle;
            }
        }
    }

    private Object targetObstacle;

    /// <summary>Hareketli hedeflerde (kahraman, Çırak) nokta her karede güncellenir.</summary>
    private void RefreshTargetPoint()
    {
        if (currentTarget == null) return;
        if (currentTarget.GetComponent<BattleStructure>() is BattleStructure structure && structure != null)
            targetPoint = structure.BasePoint;
        else targetPoint = currentTarget.position;
    }

    private static BattleStructure NearestStructure(Vector2 from, float reach)
    {
        BattleStructure nearest = null;
        float best = reach;
        foreach (var structure in BattleStructure.All)
        {
            if (structure == null || structure.IsDestroyed) continue;
            float d = Vector2.Distance(from, structure.BasePoint);
            if (d < best) { best = d; nearest = structure; }
        }
        return nearest;
    }

    private float DistanceToTarget
    {
        get
        {
            float d = Vector2.Distance(transform.position, targetPoint) - targetRadius;
            if (targetObstacle != null && BattleObstacles.TryGet(targetObstacle, out var area))
                d = Mathf.Min(d, BattleObstacles.DistanceTo(area, transform.position));
            return d;
        }
    }

    // ==================== HAREKET ====================

    private void MoveToTarget()
    {
        if (currentTarget == null) return;
        // 8. oturum (D96): yol durum makinesi (izle → kilitlen → savaş → ileriden yola dön).
        if (lane != null) { LaneTick(moveSpeed * SpeedMultiplier * Time.deltaTime); return; }
        if (DistanceToTarget <= attackRange) return;
        Vector3 aim = targetPoint;
        if (route != null && currentTarget == mainBaseTransform && !InFallback)
        {
            while (routeIndex < route.Length && Vector2.Distance(transform.position, route[routeIndex]) < 0.25f) routeIndex++;
            if (routeIndex < route.Length) aim = route[routeIndex];
        }
        Vector3 direction = (aim - transform.position);
        direction.z = 0f;
        direction.Normalize();
        float step = moveSpeed * SpeedMultiplier * Time.deltaTime;
        bool onLane = route != null && currentTarget == mainBaseTransform;
        // D116 (madde 30): ada uçanları (Yarasa/Hayalet/Ejder; Variant Flying değil) okçuya da düz uçar. Eskiden okçu hedefliyken
        // yer ızgarasıyla yürüyordu: güney kenarından (harita dışı) doğan sürü en yakın "yürünebilir" hücre olan GB/GD adacıklarına
        // iniyor, oradan anakaraya yol bulamayıp sonsuza dek bekliyordu → dalga bitmiyordu. Watchdog yedeği de düz çizgidir.
        bool islandFlyer = route != null && IslandTargeting.RoleOf(enemyType) == IslandTargeting.Role.Flyer;
        if (IsFlying || onLane || islandFlyer || InFallback)
            transform.position += direction * step;
        else
        {
            // 4. oturum (1.1): yürüyen düşman kuleleri/Ana Üs'ü/binaları delip geçmez; ortak yol bulma ile dolanır.
            steer = steer ?? new GridSteer(route != null ? (IWalkGrid)IslandWalkGrid.Shared : BattleWalkGrid.Shared);
            Vector2 next = steer.Step(transform.position, aim, step, out float facing);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            if (Mathf.Abs(facing) > 0.0001f) direction = new Vector3(facing, 0f, 0f);
        }
        if (spriteRenderer != null && direction.x != 0)
            spriteRenderer.flipX = direction.x > 0; // Düşmanlar sağdan gelirse sola baksın
    }
    private GridSteer steer;

    // ==================== YOL DURUM MAKİNESİ (8. oturum, D96) ====================

    /// <summary>
    /// Her kare: (1) ilerleme güncellenir (yalnız artar), (2) hedefe göre durum seçilir, (3) duruma göre hareket.
    /// Ana Üs'e giderken yolu izler; yol kenarı hedefe kilitlenince ortak yol bulmayla (su/uçurum/yapı geçmez) ona yürür,
    /// menzilde savaşır; hedef ölür/uzaklaşır/bırakma mesafesini aşarsa ileriden (geriye değil) eğriyle yola katılır.
    /// </summary>
    private void LaneTick(float step)
    {
        Vector2 pos = transform.position;
        bool sideTarget = currentTarget != mainBaseTransform;

        if (laneState != LaneState.FollowLane)
        {
            float s = LocalProgress(pos, out float lateral);
            // Işınlanma/atılma (Cadı, Gölge Kurt): pencerede yol çok uzaksa tüm yola bakılır; yine yalnız ileri alınır.
            // Maden yağmacısı karadan bilerek uzağa yürür: onun ilerlemesi tüm yola izdüşümle (yine yalnız ileri).
            if (lateral > 6f || RaidTargetIsCurrent) s = Mathf.Max(s, lane.Project(pos, out _));
            laneS = Mathf.Max(laneS, s);
        }

        if (sideTarget && !RaidTargetIsCurrent && LeashBroken(pos))
        {
            // Bırak: hedef ya da düşman yoldan çok uzak / hedef geride / yeterince kovalandı → ileriden yola dön.
            SetTarget(mainBaseTransform, mainBaseTransform != null ? (Vector2)mainBaseTransform.position : pos, BaseRadius);
            relockAfter = Time.time + RelockCooldown;
            nextRetarget = Time.time + RetargetInterval;
            sideTarget = false;
        }

        if (sideTarget)
        {
            bool inReach = DistanceToTarget <= attackRange;
            SetLaneState(inReach ? LaneState.Fight : LaneState.Lock);
            // Hareketli hedefin (kahraman/Çırak/Lancer) peşinde yol yönünde geri adım atılmaz; sabit yapıya (en çok BackTolerance geride) erişilir.
            if (!inReach) StepOffLane(pos, targetPoint, step, allowBackward: targetObstacle != null || RaidTargetIsCurrent);
            else ApplySeparation(pos, step);
            return;
        }

        // Ana Üs hedefi: yolun sonuna kadar yol; sondaki üsse vuruş menzilinde durur.
        if (DistanceToTarget <= attackRange) { ApplySeparation(pos, step); return; }
        if (laneState == LaneState.Lock || laneState == LaneState.Fight) SetLaneState(LaneState.Rejoin);
        if (laneState == LaneState.Rejoin) RejoinMove(pos, step);
        else FollowLaneMove(pos, step);
    }

    private bool RaidTargetIsCurrent => RaidTarget != null && currentTarget == RaidTarget.transform;

    /// <summary>Hedef bırakılmalı mı (leash): hedef ya da düşman yoldan BırakmaMesafesi'nden uzak, hareketli hedef geride ya da çok ileri kovalandı.</summary>
    private bool LeashBroken(Vector2 pos)
    {
        lane.ProjectRange(pos, laneS - BackTolerance, laneS + ProgressWindow, out float selfLateral);
        if (selfLateral > LeashDistance) return true;
        float ts = lane.ProjectRange(targetPoint, laneS - 3f, laneS + ProgressWindow, out float targetLateral);
        if (targetLateral > LeashDistance) return true;
        bool moving = currentTarget != null && currentTarget.GetComponent<BattleStructure>() == null
            && currentTarget.GetComponent<MainBaseController>() == null;
        if (!moving) return false;
        if (ts < laneS - BackTolerance - 0.5f) return true;   // kahraman geride kaldı: geriye koşmaz
        bool engagedAlready = laneState == LaneState.Lock || laneState == LaneState.Fight;   // yeni kilitte lockS henüz atanmadı
        return engagedAlready && laneS - lockS > ChaseAhead;   // yol boyunca yeterince kovaladı
    }

    /// <summary>Yolu izle: şeritte ilerler; itilip şeritten çıktıysa İLERİDEKİ şerit noktasına kayar.</summary>
    private void FollowLaneMove(Vector2 pos, float step)
    {
        UpdateSeparation(pos, out float alongBlock);
        Vector2 lanePoint = LanePoint;
        float off = Vector2.Distance(pos, lanePoint);
        Vector2 next;
        if (off > 0.35f)
        {
            // Şeritten çıkmış (ayrışma/geri tepme): bulunduğu yerin ilerisindeki şerit noktasına.
            float s = LocalProgress(pos, out _);
            laneS = Mathf.Max(laneS, s);
            next = OffLaneStep(pos, LanePointAt(Mathf.Min(lane.Length, laneS + 0.6f)), step);
        }
        else
        {
            laneS = Mathf.Min(lane.Length, laneS + step * alongBlock);
            next = Vector2.MoveTowards(pos, LanePoint, Mathf.Max(step * 1.5f, 0.02f));
        }
        Commit(pos, next);
    }

    /// <summary>
    /// Yola dön: hedef nokta ilerlemenin İLERİSİNDE (yoldan uzaklıkla orantılı öncü pay) ve düşman ilerledikçe ileri kayar →
    /// düşman yola açılı, doğal bir eğriyle katılır; ilerleme penceresi geriyi görmediği için geriye yönelmez.
    /// </summary>
    private void RejoinMove(Vector2 pos, float step)
    {
        float s = LocalProgress(pos, out float lateral);
        float lead = Mathf.Clamp(lateral * 0.9f, 0.4f, 2.2f);
        rejoinS = Mathf.Min(lane.Length, Mathf.Max(rejoinS, Mathf.Max(laneS, s) + lead));
        Vector2 aim = LanePointAt(rejoinS);
        Vector2 here = LanePointAt(Mathf.Max(laneS, s));
        if (Vector2.Distance(pos, here) < 0.3f || Vector2.Distance(pos, aim) < 0.3f)
        {
            laneS = Mathf.Max(laneS, Vector2.Distance(pos, aim) < 0.3f ? rejoinS : s);
            LastJoinS = laneS;
            RejoinCount++;
            SetLaneState(LaneState.FollowLane);
            FollowLaneMove(pos, step);
            return;
        }
        StepOffLane(pos, aim, step, allowBackward: false);
    }

    /// <summary>Yol dışı adım (ortak yol bulma) + ayrışma; <paramref name="allowBackward"/> false iken yol yönünde geri bileşen atılır.</summary>
    private void StepOffLane(Vector2 pos, Vector2 aim, float step, bool allowBackward)
    {
        Vector2 next = OffLaneStep(pos, aim, step);
        next += SeparationPush(pos) * step * 0.6f;
        if (!allowBackward)
        {
            Vector2 tangent = lane.TangentAt(Mathf.Max(laneS, LocalProgress(pos, out _)));
            float back = Vector2.Dot(next - pos, tangent);
            if (back < 0f) next -= tangent * back;
        }
        if (!IsFlying && !BattleWalkGrid.Shared.Walkable(BattleWalkGrid.Shared.CellOf(next)) && BattleWalkGrid.Shared.Walkable(BattleWalkGrid.Shared.CellOf(pos)))
            next = OffLaneStep(pos, aim, step);   // ayrışma su/yapıya itiyorsa yalnız yol bulma adımı
        Commit(pos, next);
    }

    private Vector2 OffLaneStep(Vector2 pos, Vector2 aim, float step)
    {
        if (IsFlying) return Vector2.MoveTowards(pos, aim, step);
        steer = steer ?? new GridSteer(BattleWalkGrid.Shared);
        return steer.Step(pos, aim, step, out _);
    }

    private Vector2 facingVelocity;

    private void Commit(Vector2 pos, Vector2 next)
    {
        Vector2 delta = next - pos;
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        // Titreme olmasın: bakış yönü yumuşatılmış hıza göre ve histerezisle (yatay hız, yürüme hızının %35'ini aşınca) döner.
        // Dikey yol kesiminde ayrışmanın küçük yan kıpırtısı yönü çevirmez.
        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        facingVelocity = Vector2.Lerp(facingVelocity, delta / dt, Mathf.Clamp01(dt * 6f));
        float threshold = Mathf.Max(0.2f, moveSpeed * SpeedMultiplier * 0.35f);
        if (spriteRenderer != null && Mathf.Abs(facingVelocity.x) > threshold) spriteRenderer.flipX = facingVelocity.x > 0;
    }

    // ---- Ayrışma (separation): kalabalıkta iç içe girmesin ----
    private static readonly System.Collections.Generic.List<EnemyController> laneEnemies = new System.Collections.Generic.List<EnemyController>();
    private float BodyRadius => EnemyTypeInfo.IsBoss(enemyType) ? 0.75f : enemyType == EnemyType.Troll ? 0.55f : 0.36f;

    private void OnEnable() { if (!laneEnemies.Contains(this)) laneEnemies.Add(this); }
    private void OnDisable() => laneEnemies.Remove(this);

    /// <summary>Komşulardan itme vektörü (birim başına; örtüşme oranında).</summary>
    private Vector2 SeparationPush(Vector2 pos)
    {
        Vector2 push = Vector2.zero;
        for (int i = 0; i < laneEnemies.Count; i++)
        {
            var other = laneEnemies[i];
            if (other == this || other == null || other.isDead || other.lane == null || other.IsFlying != IsFlying) continue;
            Vector2 d = pos - (Vector2)other.transform.position;
            float min = BodyRadius + other.BodyRadius;
            float dist = d.magnitude;
            if (dist >= min) continue;
            if (dist < 1e-4f) d = new Vector2(GetInstanceID() > other.GetInstanceID() ? 1f : -1f, 0f);
            push += d.normalized * (1f - dist / min);
        }
        return push;
    }

    /// <summary>
    /// Yol izlerken ayrışma: yan itme şerit kaymasını yumuşakça değiştirir (sıçrama yok), öndeki komşu yavaşlatır (geri itmez).
    /// <paramref name="alongBlock"/> 0,3–1 arası ilerleme çarpanı.
    /// </summary>
    private void UpdateSeparation(Vector2 pos, out float alongBlock)
    {
        alongBlock = 1f;
        Vector2 tangent = lane.TangentAt(laneS), normal = new Vector2(-tangent.y, tangent.x);
        float lateralPush = 0f;
        for (int i = 0; i < laneEnemies.Count; i++)
        {
            var other = laneEnemies[i];
            if (other == this || other == null || other.isDead || other.lane == null || other.IsFlying != IsFlying) continue;
            Vector2 d = (Vector2)other.transform.position - pos;
            float min = BodyRadius + other.BodyRadius;
            if (d.sqrMagnitude >= min * min * 1.6f) continue;
            float ahead = Vector2.Dot(d, tangent), side = Vector2.Dot(d, normal);
            // Aynı noktada (eşit ilerleme) iki düşman: kimlikle sıra bozulur, biri öne geçer (yoksa hep iç içe yürürler).
            if (Mathf.Abs(ahead) < 0.05f) ahead = GetInstanceID() < other.GetInstanceID() ? 0.06f : -0.06f;
            float overlap = 1f - d.magnitude / (min * 1.26f);
            lateralPush -= Mathf.Sign(side == 0f ? (GetInstanceID() > other.GetInstanceID() ? 1f : -1f) : side) * overlap;
            if (ahead > 0.05f && Mathf.Abs(side) < min * 0.8f && other.moveSpeed * other.SpeedMultiplier <= moveSpeed * SpeedMultiplier + 0.01f)
                alongBlock = Mathf.Min(alongBlock, Mathf.Lerp(0.1f, 1f, Mathf.Clamp01((ahead - 0.2f * min) / (0.8f * min))));
        }
        float targetOffset = Mathf.Clamp(lateralPush * 0.5f, -0.45f - laneOffset, 0.45f - laneOffset);
        if (Mathf.Abs(lateralPush) < 0.01f) targetOffset = 0f;
        sepOffset = Mathf.MoveTowards(sepOffset, targetOffset, 0.9f * Time.deltaTime);
    }

    /// <summary>Savaşırken (yerinde): yalnız örtüşmeyi çözer, su/yapıya itmez.</summary>
    private void ApplySeparation(Vector2 pos, float step)
    {
        Vector2 push = SeparationPush(pos);
        if (push.sqrMagnitude < 1e-4f) return;
        Vector2 next = pos + Vector2.ClampMagnitude(push, 1f) * step * 0.5f;
        if (!IsFlying && !BattleWalkGrid.Shared.Walkable(BattleWalkGrid.Shared.CellOf(next))) return;
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    // ==================== SALDIRI ====================

    private void TryAttack()
    {
        if (currentTarget == null) return;
        if (DistanceToTarget <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            AttackTarget();
        }
    }

    private void AttackTarget()
    {
        if (currentTarget == null) return;
        if (route != null)
        {
            if (attackRange > 2.5f) IslandFx.EnemyShot(transform.position, targetPoint);
            IslandStats.RecordEnemyHit(this, currentTarget, damage);
            var islandArcher = currentTarget.GetComponent<IslandArcher>();
            if (islandArcher != null) { islandArcher.TakeDamage(damage); return; }
            var islandLancer = currentTarget.GetComponent<IslandLancer>();   // Faz E1: Kışla Lancer'ı
            if (islandLancer != null) { islandLancer.TakeDamage(damage); return; }
        }
        var baseCtrl = currentTarget.GetComponent<MainBaseController>();
        if (baseCtrl != null) { baseCtrl.TakeDamage(damage); return; }
        var structure = currentTarget.GetComponent<BattleStructure>();
        if (structure != null) { structure.TakeDamage(damage); return; }
        var heroCtrl = currentTarget.GetComponent<HeroController>();
        if (heroCtrl != null) { heroCtrl.TakeDamage(damage); return; }
        var apprentice = currentTarget.GetComponent<BattleApprentice>();
        if (apprentice != null) { apprentice.TakeDamage(damage); return; }
        var allyLancer = currentTarget.GetComponent<AllyLancer>();
        if (allyLancer != null) { allyLancer.TakeDamage(damage); return; }
        var wallCtrl = currentTarget.GetComponent<WallController>();
        if (wallCtrl != null) wallCtrl.TakeDamage(damage);
    }

    // ==================== HASAR ALMA ====================

    /// <summary>Can Emme gibi yetenekler için iyileşme (tavanı aşmaz).</summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (healthBar != null) healthBar.UpdateHealth(currentHealth);
    }

    public void TakeDamage(float dmg, DamageSource source = DamageSource.Hero)
    {
        if (isDead || Invulnerable) return;

        LastHitTime = Time.time;
        lastDamageSource = source;
        float sourceMult = SourceMultiplier(source);
        bool resisted = sourceMult < 1f;
        dmg *= sourceMult;
        dmg *= BattleCards.TowerHitMultiplier(source, Variant);   // Atölye kartları (Keskin Oklar/Zırh Delici/Gök Avcısı; ×1,5 tavanlı)
        dmg *= Mathf.Max(0f, DamageTakenMultiplier);
        if (IsExposed) dmg *= Progression.Counters.WardExposedDamageMult;
        float before = dmg;

        if (WardActive && source != DamageSource.Env)
        {
            if (source == DamageSource.Hero || source == DamageSource.Apprentice)
            {
                // Kahraman/Çırak önce kalkanı eritir (x1,0); taşan hasar cana geçer.
                float absorbed = Mathf.Min(wardHp, dmg);
                wardHp -= absorbed;
                dmg -= absorbed;
                if (wardBar != null) wardBar.UpdateHealth(wardHp);
                if (wardHp <= 0f) BreakWard();
            }
            else { dmg *= wardTowerMult; before = dmg; } // Kule hasarı kalkan varken azalır
        }
        if (before > 0f)
        {
            DamageBySource.TryGetValue(source, out float total);
            DamageBySource[source] = total + before;
        }

        currentHealth -= dmg;
        currentHealth = Mathf.Max(0, currentHealth);

        // Hasar flash efekti
        if (spriteRenderer != null)
            StartCoroutine(DamageFlash());

        // Hasar sayısı (yeni VFX); Zırhlı'ya kule hasarı gri "dayanıklı" sayıyla gösterilir.
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(
                transform.position + Vector3.up * 0.8f,
                before,
                damage,
                false,
                resisted && Variant == EnemyVariant.Armored ? new Color(0.62f, 0.62f, 0.66f) : (Color?)null);

        // Can barını güncelle
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            if (IsSaboteur)
                CountSaboteur(source == DamageSource.Hero || source == DamageSource.Apprentice ? "hero" : "tower");
            Die();
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.25f, 0.25f, spriteRenderer.color.a);
            yield return new WaitForSeconds(0.1f);
            if (spriteRenderer != null)
                spriteRenderer.color = new Color(BaseTint.r, BaseTint.g, BaseTint.b, spriteRenderer.color.a);
        }
    }

    private DamageSource lastDamageSource = DamageSource.Env;

    private void Die()
    {
        isDead = true;
        // Günlük görev: son vuruş kahraman ya da Çırak'tan geldiyse "kahramanla yen".
        if (lastDamageSource == DamageSource.Hero || lastDamageSource == DamageSource.Apprentice)
            DailyQuests.Report(QuestKind.HeroKills);

        Died?.Invoke(this);

        // BattleManager'a bildir
        BattleManager battleManager = BattleManager.Instance != null ? BattleManager.Instance : FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
            battleManager.OnEnemyKilled(goldReward);

        Debug.Log($"[Enemy] {enemyType} öldü! +{goldReward} altın");

        if (!SuppressDeathEffect) StartCoroutine(DeathEffect());
    }

    private System.Collections.IEnumerator DeathEffect()
    {
        // 1. Sprite kırmızıya dönsün (0.1s)
        if (spriteRenderer != null)
            spriteRenderer.DOColor(new Color(1f, 0.15f, 0.1f), 0.1f);

        yield return new WaitForSeconds(0.1f);

        // 2. Parçacık dağılsın
        Color particleColor = new Color(0.8f, 0.15f, 0.1f);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnDeathParticles(transform.position, particleColor);

        // 3. Scale 1 → 0 (0.3s, InBack)
        transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
        if (spriteRenderer != null)
            spriteRenderer.DOFade(0f, 0.3f);

        yield return new WaitForSeconds(0.35f);

        // 8. oturum (D97): boss olmayan düşmanın kabuğu havuza döner.
        var battle = BattleManager.Instance;
        if (battle != null && battle.ReleaseEnemy(this)) yield break;
        Destroy(gameObject);
    }
}

/// <summary>
/// Uçan düşman görseli: kök, mantıksal konumun üstünde süzülür. Kule/menzil hesapları Update'te
/// (ofset kaldırılmışken) yapılır; ofset yalnız LateUpdate'ten sonra, çizim için uygulanır.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class EnemyFlightVisual : MonoBehaviour
{
    private Vector3 applied;

    private void Update() => Remove();

    private void LateUpdate()
    {
        Remove();
        float bob = Mathf.Sin(Time.time * Mathf.PI * 2f * Progression.Counters.FlyingBobHz) * Progression.Counters.FlyingBobAmplitude;
        applied = new Vector3(0f, Progression.Counters.FlyingYOffset + bob, 0f);
        transform.position += applied;
    }

    private void OnDisable() => Remove();

    private void Remove()
    {
        transform.position -= applied;
        applied = Vector3.zero;
    }
}
