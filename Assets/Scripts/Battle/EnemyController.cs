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
    public EnemyType Type => enemyType;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => currentHealth / maxHealth;
    public float Damage => damage;
    public float MoveSpeed => moveSpeed;
    public Transform CurrentTarget => currentTarget;

    // Özel davranışlar (Gölge Kurt atılması, Cadı ışınlanması) için
    public bool MovementLocked { get; set; }
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
                float targetWidth = type == EnemyType.Troll ? 2.5f :
                    type == EnemyType.ShadowWolf ? 1.6f :
                    type == EnemyType.Orc ? 1.85f :
                    type == EnemyType.Wolf ? 1.75f : 1.6f;
                float scale = targetWidth / Mathf.Max(0.01f, spriteRenderer.sprite.bounds.size.x);
                transform.localScale = new Vector3(scale, scale, 1f);
                if (type == EnemyType.ShadowWolf)
                    spriteRenderer.color = new Color(0.42f, 0.36f, 0.58f);
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

        FindBestTarget();
        if (MovementLocked) return;
        MoveToTarget();
        TryAttack();
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
        if (mainBaseTransform != null)
            SetTarget(mainBaseTransform, mainBaseTransform.position, BaseRadius);
    }

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

    public void TakeDamage(float dmg)
    {
        if (isDead || Invulnerable) return;

        currentHealth -= dmg;
        currentHealth = Mathf.Max(0, currentHealth);

        // Hasar flash efekti
        if (spriteRenderer != null)
            StartCoroutine(DamageFlash());

        // Hasar sayısı (yeni VFX)
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(
                transform.position + Vector3.up * 0.8f,
                dmg,
                damage);

        // Can barını güncelle
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
            Die();
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
        }
    }

    private void Die()
    {
        isDead = true;

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
