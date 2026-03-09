using UnityEngine;

/// <summary>
/// Düşman davranışı — Ana Üs'e doğru yürür, yolda engel varsa saldırır
/// Öncelik: Duvar → Kule → Ana Üs
/// Eğer kahraman çok yakınsa ve saldırıyorsa ona da döner (opsiyonel)
/// </summary>
public class EnemyController : MonoBehaviour
{
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
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => currentHealth / maxHealth;

    // Hedef referansları (BattleManager tarafından atanır)
    private Transform mainBaseTransform;
    private EnemyType enemyType;

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
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 0.6f, 0));

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

            switch (type)
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
    }

    private void Update()
    {
        if (isDead) return;

        FindBestTarget();
        MoveToTarget();
        TryAttack();
    }

    // ==================== HEDEF BULMA ====================

    /// <summary>
    /// Hedef önceliği: Yolda karşılaşılan Kahraman → Duvar → Ana Üs
    /// Kahraman yakındaysa ona saldır, değilse üsse yürü
    /// </summary>
    private void FindBestTarget()
    {
        // 1) Kahraman yakında mı? (Ana üs'ten uzakta karşılaşma)
        HeroController hero = FindFirstObjectByType<HeroController>();
        if (hero != null && hero.CurrentHealth > 0)
        {
            float distToHero = Vector2.Distance(transform.position, hero.transform.position);
            float heroEngageRange = attackRange + 1.5f; // Algılama mesafesi

            if (distToHero <= heroEngageRange)
            {
                currentTarget = hero.transform;
                return;
            }
        }

        // 2) Duvar var mı kontrol et
        WallController wall = FindNearestAlive<WallController>();
        if (wall != null)
        {
            float dist = Vector2.Distance(transform.position, wall.transform.position);
            if (dist < attackRange + 2f)
            {
                currentTarget = wall.transform;
                return;
            }
        }

        // 3) Ana Üs her zaman son hedef
        currentTarget = mainBaseTransform;
    }

    private T FindNearestAlive<T>() where T : MonoBehaviour
    {
        T[] objects = FindObjectsByType<T>(FindObjectsSortMode.None);
        T nearest = null;
        float nearestDist = Mathf.Infinity;

        foreach (var obj in objects)
        {
            float dist = Vector2.Distance(transform.position, obj.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = obj;
            }
        }

        return nearest;
    }

    // ==================== HAREKET ====================

    private void MoveToTarget()
    {
        if (currentTarget == null) return;

        float distance = Vector2.Distance(transform.position, currentTarget.position);

        if (distance > attackRange)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            // Sprite yönü
            if (spriteRenderer != null && direction.x != 0)
            {
                spriteRenderer.flipX = direction.x > 0; // Düşmanlar sağdan gelirse sola baksın
            }
        }
    }

    // ==================== SALDIRI ====================

    private void TryAttack()
    {
        if (currentTarget == null) return;

        float distance = Vector2.Distance(transform.position, currentTarget.position);

        if (distance <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            AttackTarget();
        }
    }

    private void AttackTarget()
    {
        // Ana Üs'e saldırı
        MainBaseController baseCtrl = currentTarget.GetComponent<MainBaseController>();
        if (baseCtrl != null)
        {
            baseCtrl.TakeDamage(damage);
            return;
        }

        // Duvara saldırı
        WallController wallCtrl = currentTarget.GetComponent<WallController>();
        if (wallCtrl != null)
        {
            wallCtrl.TakeDamage(damage);
            return;
        }

        // Kahramana saldırı
        HeroController heroCtrl = currentTarget.GetComponent<HeroController>();
        if (heroCtrl != null)
        {
            heroCtrl.TakeDamage(damage);
            return;
        }

        // Kule'ye saldırı (ileride eklenecek)
    }

    // ==================== HASAR ALMA ====================

    public void TakeDamage(float dmg)
    {
        if (isDead) return;

        currentHealth -= dmg;
        currentHealth = Mathf.Max(0, currentHealth);

        // Hasar efekti
        if (spriteRenderer != null)
        {
            StartCoroutine(DamageFlash());
        }

        // Can barını güncelle
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
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

        // BattleManager'a bildir (savaş içi altın havuzuna eklenir)
        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
        {
            battleManager.OnEnemyKilled(goldReward);
        }

        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnDeath(transform.position, 0.8f);

        Debug.Log($"[Enemy] {enemyType} öldü! +{goldReward} altın");

        // Ölüm efekti: hemen küçült ve soluklaştır
        StartCoroutine(DeathEffect());
    }

    private System.Collections.IEnumerator DeathEffect()
    {
        // Hemen sprite'ı kırmızıya çevir
        if (spriteRenderer != null)
            spriteRenderer.color = new Color(1f, 0.2f, 0.1f, 0.8f);

        Vector3 startScale = transform.localScale;
        float t = 0;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float progress = t / 0.25f;
            transform.localScale = startScale * (1f - progress * 0.8f);
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f - progress;
                spriteRenderer.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
