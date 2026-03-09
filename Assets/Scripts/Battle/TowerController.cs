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

        // Kule rengini ayarla (geçici)
        if (spriteRenderer != null)
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
        // Her birim ayrı saldırır
        float totalDamage = damage * unitCount;
        target.TakeDamage(totalDamage);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnHit(target.transform.position, 0.6f);

        Debug.Log($"[Tower] {towerType} → {totalDamage} hasar! ({unitCount} birim)");
    }

    public void TakeDamage(float dmg)
    {
        currentHealth -= dmg;
        currentHealth = Mathf.Max(0, currentHealth);

        if (currentHealth <= 0)
        {
            Debug.Log($"[Tower] {towerType} yıkıldı!");
            Destroy(gameObject);
        }
    }
}
