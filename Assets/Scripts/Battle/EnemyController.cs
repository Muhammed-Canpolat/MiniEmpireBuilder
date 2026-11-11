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
        wardMax = maxHealth * Progression.Counters.WardFraction(region);
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
        exposedUntil = Time.time + Progression.Counters.WardExposedSeconds;
        staggerUntil = Time.time + Progression.Counters.WardStaggerSeconds;
        SetWardVisible(false);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnSpark(transform.position + Vector3.up * 0.8f);
    }

    // Özel davranışlar (Gölge Kurt atılması, Cadı ışınlanması) için
    public bool MovementLocked { get; set; }
    /// <summary>Alınan hasar çarpanı (ör. Kale Muhafızı'nın Kalkan Duvarı 0.3).</summary>
    public float DamageTakenMultiplier { get; set; } = 1f;
    /// <summary>Asıl renk: vurulma parlaması bitince buna döner (üst üste darbede kırmızı kalmasın).</summary>
    public Color BaseTint { get; set; } = Color.white;
    public bool Invulnerable { get; set; }
    // true iken FogOfWar alfa değerine dokunmaz (davranış kendi soluklaşmasını yönetir).
    public bool FogOverride { get; set; }

    /// <summary>Çağrılmış birimler altın düşürmez; boss ile altın kasılmasını önler.</summary>
    public void SuppressGoldReward() => goldReward = 0;

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
        var structure = currentTarget.GetComponent<BattleStructure>();
        if (structure != null) return !structure.IsDestroyed;
        var heroTarget = currentTarget.GetComponent<HeroController>();
        if (heroTarget != null) return heroTarget.CurrentHealth > 0f;
        var apprentice = currentTarget.GetComponent<BattleApprentice>();
        if (apprentice != null) return apprentice.CurrentHealth > 0f;
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
        if (IsSaboteur)
        {
            // Bombacı: kışkırtmayı yok sayar; kuleyi (her mesafeden) ya da kule yoksa Ana Üs'ü hedefler.
            if (fuseEndsAt >= 0f) return;
            var tower = NearestTower(position);
            if (tower != null) SetTarget(tower.transform, tower.BasePoint, StructureRadius);
            else if (mainBaseTransform != null) SetTarget(mainBaseTransform, mainBaseTransform.position, BaseRadius);
            return;
        }
        HeroController hero = FindFirstObjectByType<HeroController>();
        bool heroAlive = hero != null && hero.CurrentHealth > 0f;
        float heroDistance = heroAlive ? Vector2.Distance(position, hero.transform.position) : float.MaxValue;

        // Ortak kurallar: kışkırtılmışsa kahraman; kahraman dibindeyse (vuruş menzili) kahraman.
        if (heroAlive && (Time.time < provokedUntil || heroDistance <= attackRange + 0.3f))
        {
            SetTarget(hero.transform, hero.transform.position, 0f);
            return;
        }

        switch (EnemyRole)
        {
            case Role.Hunter:
            {
                Transform prey = null;
                float best = HunterAggro;
                if (heroAlive && heroDistance <= best) { prey = hero.transform; best = heroDistance; }
                var apprentice = FindFirstObjectByType<BattleApprentice>();
                if (apprentice != null && apprentice.CurrentHealth > 0f)
                {
                    float d = Vector2.Distance(position, apprentice.transform.position);
                    if (d < best) { prey = apprentice.transform; best = d; }
                }
                if (prey != null) { SetTarget(prey, prey.position, 0f); return; }
                break;
            }
            case Role.Besieger:
            case Role.Breaker:
            {
                float reach = EnemyRole == Role.Breaker ? BreakerReach : BesiegerReach;
                var structure = NearestStructure(position, reach);
                if (structure != null) { SetTarget(structure.transform, structure.BasePoint, StructureRadius); return; }
                break;
            }
            case Role.Siege:
            {
                // Boss üsse yürür; yolunun üstündeki kuleyi ezer.
                var structure = NearestStructure(position, attackRange + 1f);
                if (structure != null) { SetTarget(structure.transform, structure.BasePoint, StructureRadius); return; }
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

    /// <summary>"Madeni koru" aşamasında bu düşmanın asıl hedefi olan maden (yoksa null: Ana Üs).</summary>
    public BattleStructure RaidTarget { get; set; }

    private void SetTarget(Transform target, Vector2 point, float radius)
    {
        currentTarget = target;
        targetPoint = point;
        targetRadius = radius;
    }

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

    private float DistanceToTarget => Vector2.Distance(transform.position, targetPoint) - targetRadius;

    // ==================== HAREKET ====================

    private void MoveToTarget()
    {
        if (currentTarget == null) return;
        if (DistanceToTarget <= attackRange) return;
        Vector3 direction = ((Vector3)targetPoint - transform.position);
        direction.z = 0f;
        direction.Normalize();
        transform.position += direction * moveSpeed * Time.deltaTime;
        if (spriteRenderer != null && direction.x != 0)
            spriteRenderer.flipX = direction.x > 0; // Düşmanlar sağdan gelirse sola baksın
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
        var baseCtrl = currentTarget.GetComponent<MainBaseController>();
        if (baseCtrl != null) { baseCtrl.TakeDamage(damage); return; }
        var structure = currentTarget.GetComponent<BattleStructure>();
        if (structure != null) { structure.TakeDamage(damage); return; }
        var heroCtrl = currentTarget.GetComponent<HeroController>();
        if (heroCtrl != null) { heroCtrl.TakeDamage(damage); return; }
        var apprentice = currentTarget.GetComponent<BattleApprentice>();
        if (apprentice != null) { apprentice.TakeDamage(damage); return; }
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

        // BattleManager'a bildir
        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
            battleManager.OnEnemyKilled(goldReward);

        Debug.Log($"[Enemy] {enemyType} öldü! +{goldReward} altın");

        StartCoroutine(DeathEffect());
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
