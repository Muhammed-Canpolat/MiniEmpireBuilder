using UnityEngine;

/// <summary>
/// Okçu/Topçu Kulesi — otomatik olarak en yakın düşmana saldırır
/// </summary>
public class TowerController : MonoBehaviour
{
    [Header("Kule Tipi")]
    [SerializeField] private BuildingType towerType = BuildingType.ArcherTower;

    [Header("Statlar")]
    [SerializeField] private float maxHealth = 80f;
    [SerializeField] private float damage = 8f;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private int unitCount = 1;

    private float currentHealth;
    private float lastAttackTime;
    private SpriteRenderer spriteRenderer;
    private HealthBar healthBar;

    public float HealthPercent => currentHealth / maxHealth;
    public bool IsDestroyed => currentHealth <= 0;

    public void Initialize(BuildingType type)
    {
        towerType = type;
    }

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // GameManager'dan stat al
        if (GameManager.Instance != null && GameManager.Instance.Buildings.ContainsKey(towerType))
        {
            var towerData = GameManager.Instance.Buildings[towerType];
            if (towerData.isUnlocked && towerData.level > 0)
            {
                towerData.ApplyLevelStats();
                maxHealth = towerData.maxHealth;
                damage = towerData.damage;
                attackRange = towerData.attackRange;
                unitCount = towerData.unitCount;
                attackCooldown = 1f / towerData.attackSpeed;
            }
        }

        currentHealth = maxHealth;

        // Health bar'ı oluştur
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 0.8f, 0));

        // Kule rengini ayarla (geçici, gerçek sprite yoksa)
        if (spriteRenderer != null && (spriteRenderer.sprite == null || spriteRenderer.sprite.name == "Square"))
        {
            spriteRenderer.color = towerType == BuildingType.ArcherTower
                ? new Color(0.2f, 0.6f, 1f)   // Mavi — Okçu
                : new Color(1f, 0.4f, 0.1f);  // Turuncu — Topçu
        }
    }

    private void Update()
    {
        if (currentHealth <= 0) return;

        // Saldırı cooldown kontrolü
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            EnemyController target = FindNearestEnemy();
            if (target != null)
            {
                Attack(target);
                lastAttackTime = Time.time;
            }
        }
    }

    private EnemyController FindNearestEnemy()
    {
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        EnemyController nearest = null;
        float nearestDist = attackRange;

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private void Attack(EnemyController target)
    {
        // Kule tipine göre mermi tipi belirle
        ProjectileType pType = towerType == BuildingType.ArcherTower ? ProjectileType.Arrow : ProjectileType.Axe;

        GameObject projObj = new GameObject("TowerProjectile");
        ProjectileController proj = projObj.AddComponent<ProjectileController>();

        // Her birim ayrı saldırır (örneğin 3 okçu varsa 3 katı hasar, görsel olarak tek mermi fırlatılır)
        float totalDamage = damage * unitCount;

        proj.Launch(transform.position + Vector3.up * 0.5f, target.transform.position, pType, () =>
        {
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(totalDamage);
            }
        });
    }

    public void TakeDamage(float dmg)
    {
        currentHealth -= dmg;
        currentHealth = Mathf.Max(0, currentHealth);

        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0)
        {
            Debug.Log($"[Tower] {towerType} yıkıldı!");
            Destroy(gameObject);
        }
    }
}
