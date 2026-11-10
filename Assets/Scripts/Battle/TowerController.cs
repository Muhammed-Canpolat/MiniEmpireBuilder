using UnityEngine;

/// <summary>
/// Okçu/Topçu kulesi saldırısı. Can ve yıkılma <see cref="BattleStructure"/>'da.
/// Menzil halkası sürekli görünmez: kurulduktan sonra kısa süre ve kahraman yaklaşınca belirir.
/// </summary>
public class TowerController : MonoBehaviour
{
    private const float RingRevealDistance = 3.2f;
    private const float RingAfterBuildSeconds = 2.5f;

    [SerializeField] private BuildingType towerType = BuildingType.ArcherTower;
    [SerializeField] private float damage = 8f;
    [SerializeField] private float attackRange = 6f;
    [SerializeField] private float attackCooldown = 0.7f;
    [SerializeField] private int unitCount = 1;

    private float lastAttackTime;
    private int level = -1;
    private BattleStructure structure;
    private GameObject rangeRing;
    private float ringUntil;
    private HeroController hero;

    public float HealthPercent => structure != null ? structure.HealthPercent : 1f;
    public bool IsDestroyed => structure != null && structure.IsDestroyed;
    public float AttackRange => attackRange;
    public BuildingType Type => towerType;

    /// <summary>Kule, onu sağlayan Ana Üs binasının seviyesiyle kurulur.</summary>
    public void Initialize(BuildingType type, int towerLevel)
    {
        level = towerLevel;
        Initialize(type);
    }

    public void Initialize(BuildingType type)
    {
        towerType = type;
    }

    private void Start()
    {
        structure = GetComponent<BattleStructure>();
        if (GameManager.Instance != null && GameManager.Instance.Buildings.ContainsKey(towerType))
        {
            var towerData = level > 0 ? GameManager.Instance.CreateLevelData(towerType, level)
                : GameManager.Instance.Buildings[towerType];
            if (towerData.isUnlocked && towerData.level > 0)
            {
                towerData.ApplyLevelStats();
                damage = towerData.damage;
                attackRange = towerData.attackRange;
                unitCount = towerData.unitCount;
                attackCooldown = 1f / Mathf.Max(0.1f, towerData.attackSpeed);
            }
        }
        rangeRing = BattleWorldMarkers.DashedCircle(transform, attackRange,
            new Color(0.94f, 0.76f, 0.29f, 0.35f), 36);
        // Halka yapının tabanında olsun (kök görselin ortasında).
        if (structure != null)
            rangeRing.transform.position = new Vector3(structure.BasePoint.x, structure.BasePoint.y, 0.05f);
        ringUntil = Time.time + RingAfterBuildSeconds;
    }

    private void Update()
    {
        if (IsDestroyed) return;
        UpdateRing();
        if (Time.time - lastAttackTime < attackCooldown) return;
        EnemyController target = FindNearestEnemy();
        if (target == null) return;
        Attack(target);
        lastAttackTime = Time.time;
    }

    private void UpdateRing()
    {
        if (rangeRing == null) return;
        if (hero == null) hero = FindFirstObjectByType<HeroController>();
        bool near = hero != null && Vector2.Distance(hero.transform.position, transform.position) < RingRevealDistance;
        bool show = near || Time.time < ringUntil;
        if (rangeRing.activeSelf != show) rangeRing.SetActive(show);
    }

    private EnemyController FindNearestEnemy()
    {
        EnemyController nearest = null;
        float nearestDist = attackRange;
        Vector2 origin = structure != null ? structure.BasePoint : (Vector2)transform.position;
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || !FogOfWar.IsVisible(enemy.transform.position)) continue;
            float dist = Vector2.Distance(origin, enemy.transform.position);
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
        ProjectileType pType = towerType == BuildingType.ArcherTower ? ProjectileType.Arrow : ProjectileType.Axe;
        GameObject projObj = new GameObject("TowerProjectile");
        ProjectileController proj = projObj.AddComponent<ProjectileController>();
        float totalDamage = damage * unitCount;
        Vector3 impact = target.transform.position;
        bool cannon = towerType == BuildingType.CannonTower;
        proj.Launch(transform.position + Vector3.up * 0.5f, impact, pType, () =>
        {
            // Top mermisi isabet ettiği yerde patlar (Tiny Swords Explosion).
            if (cannon) TinyFx.Explosion(target != null ? target.transform.position : impact);
            if (target != null && !target.IsDead) target.TakeDamage(totalDamage);
        });
        var garrison = GetComponent<BattleGarrison>();
        if (garrison != null) garrison.Fire();
    }

    /// <summary>Geriye uyum: hasar yapıya iletilir.</summary>
    public void TakeDamage(float dmg)
    {
        if (structure != null) structure.TakeDamage(dmg);
    }
}
