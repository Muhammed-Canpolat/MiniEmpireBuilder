using UnityEngine;
using DG.Tweening;

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
    private RangeRing rangeRing;
    private float ringUntil;
    private HeroController hero;

    public float HealthPercent => structure != null ? structure.HealthPercent : 1f;
    public bool IsDestroyed => structure != null && structure.IsDestroyed;
    public float AttackRange => attackRange;
    public BuildingType Type => towerType;
    /// <summary>Menzil merkezi: hedefleme, çember ve sis görüşü aynı noktayı kullanır.</summary>
    public Vector2 RangeOrigin => structure != null ? structure.RangeCenter : (Vector2)transform.position;
    public RangeRing Ring => rangeRing;

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
        // 6. oturum (1.2): halka hedeflemeyle aynı merkez ve yarıçap (RangeCenter, attackRange); ölçekli kuleden bağımsız.
        rangeRing = RangeRing.Attach(gameObject, () => RangeOrigin, attackRange, new Color(0.94f, 0.76f, 0.29f, 0.35f), 36);
        ringUntil = Time.time + RingAfterBuildSeconds;
    }

    private float frozenUntil;
    private SpriteRenderer[] tintTargets;
    private Color[] tintOriginals;

    public bool IsFrozen => Time.time < frozenUntil;

    /// <summary>Buz Nefesi: kule süre boyunca ateş etmez, buz mavisine boyanır.</summary>
    public void Freeze(float seconds)
    {
        if (IsDestroyed || seconds <= 0f) return;
        if (!IsFrozen)
        {
            tintTargets = GetComponentsInChildren<SpriteRenderer>();
            tintOriginals = new Color[tintTargets.Length];
            for (int i = 0; i < tintTargets.Length; i++)
            {
                tintOriginals[i] = tintTargets[i].color;
                tintTargets[i].color = tintOriginals[i] * new Color(0.6f, 0.85f, 1.25f, 1f);
            }
        }
        frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
    }

    private float thawProgress;
    private GameObject thawRing;

    /// <summary>Kahraman yarıçap içinde ThawSeconds boyunca durursa kule çözülür (Bölge 4, round2 E).</summary>
    private void UpdateThaw()
    {
        if (hero == null) hero = FindFirstObjectByType<HeroController>();
        Vector2 origin = RangeOrigin;
        bool near = hero != null && !hero.IsDowned
            && Vector2.Distance(hero.transform.position, origin) <= Progression.RegionMechanics.ThawRadius;
        if (thawRing == null)
            thawRing = BattleWorldMarkers.DashedCircle(transform, Progression.RegionMechanics.ThawRadius,
                new Color(0.55f, 0.85f, 1f, 0.8f), 24);
        thawRing.transform.position = new Vector3(origin.x, origin.y, 0.05f);
        thawProgress = near ? thawProgress + Time.deltaTime : Mathf.Max(0f, thawProgress - Time.deltaTime);
        float progress = Mathf.Clamp01(thawProgress / Progression.RegionMechanics.ThawSeconds);
        // Çözülme nabzı: halka kahraman yaklaştıkça kuleye doğru büzülür.
        thawRing.transform.localScale = Vector3.one * (Mathf.Lerp(1f, 0.35f, progress)
            / Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x)));
        if (progress < 1f) return;
        frozenUntil = 0f;
        RegionMechanicsStats.Thaws++;
        Thaw();
        if (BattleVfxManager.Instance != null) BattleVfxManager.Instance.SpawnSpark(transform.position + Vector3.up * 0.6f);
    }

    private void Thaw()
    {
        thawProgress = 0f;
        if (thawRing != null) { Destroy(thawRing); thawRing = null; }
        if (tintTargets == null) return;
        for (int i = 0; i < tintTargets.Length; i++)
            if (tintTargets[i] != null) tintTargets[i].color = tintOriginals[i];
        tintTargets = null;
    }

    private void Update()
    {
        if (IsDestroyed) { if (rangeRing != null) rangeRing.SetVisible(false); return; }
        UpdateRing();
        if (IsFrozen) { UpdateThaw(); return; }
        if (tintTargets != null) Thaw();
        // "Don" fırtınasında kuleler daha yavaş ateş eder.
        if (Time.time - lastAttackTime < attackCooldown * BattleWeather.TowerCooldownMultiplier) return;
        EnemyController target = FindNearestEnemy();
        if (target == null) return;
        Attack(target);
        lastAttackTime = Time.time;
    }

    private void UpdateRing()
    {
        if (rangeRing == null) return;
        if (hero == null) hero = FindFirstObjectByType<HeroController>();
        bool near = hero != null && Vector2.Distance(hero.transform.position, RangeOrigin) < RingRevealDistance;
        rangeRing.SetVisible(near || Time.time < ringUntil);
    }

    /// <summary>Menzil kuralı (tek yer): merkezden öklid uzaklığı ≤ yarıçap; dört yönde eşit.</summary>
    public static bool InRange(Vector2 origin, float range, Vector2 point) => Vector2.Distance(origin, point) <= range + 1e-4f;

    /// <summary>Topçu uçan düşmanı hedefleyemez (Okçu ve kahraman hedefleyebilir).</summary>
    public static bool CanTarget(BuildingType tower, EnemyController enemy) =>
        enemy != null && !(tower == BuildingType.CannonTower && enemy.IsFlying);

    private EnemyController FindNearestEnemy()
    {
        EnemyController nearest = null;
        Vector2 origin = RangeOrigin;
        // Lanetli Türbe karanlığı (Bölge 5) menzili geçici daraltır.
        float nearestDist = attackRange * CursedShrine.RangeMultiplierAt(origin);
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || !CanTarget(towerType, enemy) || !FogOfWar.IsVisible(enemy.transform.position)) continue;
            float dist = Vector2.Distance(origin, enemy.transform.position);
            if (dist <= nearestDist && InRange(origin, attackRange * CursedShrine.RangeMultiplierAt(origin), enemy.transform.position))
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
            if (target != null && !target.IsDead) target.TakeDamage(totalDamage, cannon ? DamageSource.Cannon : DamageSource.Archer);
        });
        var garrison = GetComponent<BattleGarrison>();
        if (garrison != null) garrison.Fire();
        PlayFireFeedback(cannon);
    }

    /// <summary>Atış hissi: yapı kısa bir ezilip esner; topçuda namlu kıvılcımı.</summary>
    private void PlayFireFeedback(bool cannon)
    {
        if (structure != null && structure.IsDestroyed) return;
        transform.DOComplete();
        transform.DOPunchScale(cannon ? new Vector3(0.08f, -0.1f, 0f) : new Vector3(0.03f, -0.05f, 0f),
            cannon ? 0.22f : 0.15f, 6, 0.6f);
        if (cannon && BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnSpark(transform.position + Vector3.up * 0.9f);
    }

    /// <summary>Geriye uyum: hasar yapıya iletilir.</summary>
    public void TakeDamage(float dmg)
    {
        if (structure != null) structure.TakeDamage(dmg);
    }
}
