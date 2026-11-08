using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orman Cadısı (Bölge 2 boss'u). Normal düşman gibi Ana Üs'e yürür; kahraman
/// 10 birim içindeyse sırayla yetenek kullanır:
///   Zehir → Çağırma → Zehir → Sis Adımı (döngü)
/// Zehir: kahramanın bulunduğu yerde 0.9 sn işaret, sonra 4 sn hasar veren alan.
/// Çağırma: yanına 3 kurt (en fazla 5 canlı çağrı, altın düşürmezler).
/// Sis Adımı: kaybolur, hasar almaz, kahramandan 5 birim uzakta yeniden belirir.
/// Canı %50 altına inince bekleme 4.5 sn'den 3.5 sn'ye düşer.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public sealed class ForestWitchBoss : MonoBehaviour
{
    public enum Ability { Poison, Summon, FogStep }

    public const float AbilityRange = 10f;
    public const float Cooldown = 4.5f;
    public const float EnragedCooldown = 3.5f;
    public const float PoisonTelegraph = 0.9f;
    public const float PoisonDuration = 4f;
    public const float PoisonRadius = 1.3f;
    public const float PoisonTick = 0.5f;
    public const float PoisonDamageFactor = 0.35f;
    public const int SummonCount = 3;
    public const int MaxLiveSummons = 5;
    public const float FogStepDistance = 5f;
    private const float FadeTime = 0.4f;
    private const float CastTime = 0.6f;
    private const float FrameHeight = 3.4f;
    private static readonly Color Tint = new Color(0.66f, 0.95f, 0.58f);
    private static readonly Ability[] Rotation =
        { Ability.Poison, Ability.Summon, Ability.Poison, Ability.FogStep };

    private EnemyController enemy;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private readonly List<EnemyController> summons = new List<EnemyController>();
    private readonly List<GameObject> pools = new List<GameObject>();
    private int rotationIndex;
    private float nextAbility;
    private bool casting;
    private Vector3 lastPosition;
    private bool running;

    public Ability NextAbility => Rotation[rotationIndex % Rotation.Length];
    public int LiveSummons { get { summons.RemoveAll(s => s == null || s.IsDead); return summons.Count; } }
    public bool IsCasting => casting;

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        nextAbility = Time.time + 2.5f;
    }

    private IEnumerator Start()
    {
        var controllers = BaseVisualCatalog.Instance?.monkControllers;
        if (controllers != null && controllers.Length > 3 && controllers[3] != null)
        {
            animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controllers[3]; // Mor keşiş, yeşil tonlanır
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        // Animatör ilk kareyi yazsın, sonra kare boyuna göre ölçekle.
        yield return null;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float height = Mathf.Max(0.01f, spriteRenderer.sprite.bounds.size.y);
            float scale = FrameHeight / height;
            transform.localScale = new Vector3(scale, scale, 1f);
            spriteRenderer.color = new Color(Tint.r, Tint.g, Tint.b, spriteRenderer.color.a);
        }
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (enemy.IsDead) { Cleanup(); return; }
        UpdateLocomotion();
        if (casting || Time.time < nextAbility) return;
        HeroController hero = FindFirstObjectByType<HeroController>();
        if (hero == null || hero.CurrentHealth <= 0f) return;
        if (Vector2.Distance(transform.position, hero.transform.position) > AbilityRange) return;
        Use(NextAbility, hero);
    }

    /// <summary>Testler ve yetenek döngüsü için tek giriş noktası.</summary>
    public void Use(Ability ability, HeroController hero)
    {
        rotationIndex++;
        nextAbility = Time.time + (enemy.HealthPercent < 0.5f ? EnragedCooldown : Cooldown);
        switch (ability)
        {
            case Ability.Poison: StartCoroutine(CastPoison(hero.transform.position)); break;
            case Ability.Summon: StartCoroutine(CastSummon()); break;
            case Ability.FogStep: StartCoroutine(FogStep(hero.transform.position)); break;
        }
    }

    private void UpdateLocomotion()
    {
        if (animator == null || casting) return;
        bool moving = (transform.position - lastPosition).sqrMagnitude > 0.00001f;
        lastPosition = transform.position;
        if (moving == running) return;
        running = moving;
        animator.Play(moving ? "Monk_Run_Purple" : "Monk_Idle_Purple", 0, 0f);
    }

    private IEnumerator Cast()
    {
        casting = true;
        enemy.MovementLocked = true;
        if (animator != null) animator.Play("Monk_Heal_Purple", 0, 0f);
        yield return new WaitForSeconds(CastTime);
        casting = false;
        running = false;
        if (enemy != null) enemy.MovementLocked = false;
        if (animator != null) animator.Play("Monk_Idle_Purple", 0, 0f);
    }

    // ==================== ZEHİR ====================

    private IEnumerator CastPoison(Vector3 target)
    {
        StartCoroutine(Cast());
        var pool = new GameObject("WitchPoisonPool");
        pool.transform.position = new Vector3(target.x, target.y, 0f);
        pools.Add(pool);
        var renderer = pool.AddComponent<SpriteRenderer>();
        renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        renderer.sortingOrder = -40;
        float diameter = PoisonRadius * 2f;

        // İşaret: büyüyen soluk halka, oyuncuya kaçma süresi verir.
        float t = 0f;
        while (t < PoisonTelegraph)
        {
            if (pool == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / PoisonTelegraph);
            pool.transform.localScale = Vector3.one * diameter * Mathf.Lerp(0.3f, 1f, k);
            renderer.color = new Color(0.45f, 0.85f, 0.25f, Mathf.Lerp(0.12f, 0.3f, k));
            yield return null;
        }
        if (pool == null) yield break;
        pool.transform.localScale = Vector3.one * diameter;
        renderer.color = new Color(0.35f, 0.8f, 0.2f, 0.5f);

        float tickDamage = enemy.Damage * PoisonDamageFactor;
        float end = Time.time + PoisonDuration;
        while (Time.time < end)
        {
            if (pool == null) yield break;
            var hero = FindFirstObjectByType<HeroController>();
            if (hero != null && hero.CurrentHealth > 0f &&
                Vector2.Distance(hero.transform.position, pool.transform.position) <= PoisonRadius)
                hero.TakeDamage(tickDamage);
            yield return new WaitForSeconds(PoisonTick);
        }
        if (pool != null)
        {
            pools.Remove(pool);
            Destroy(pool);
        }
    }

    // ==================== ÇAĞIRMA ====================

    private IEnumerator CastSummon()
    {
        StartCoroutine(Cast());
        yield return new WaitForSeconds(CastTime * 0.5f);
        if (enemy.IsDead || BattleManager.Instance == null) yield break;
        int room = Mathf.Min(SummonCount, MaxLiveSummons - LiveSummons);
        for (int i = 0; i < room; i++)
        {
            float angle = (i / (float)SummonCount) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.3f;
            var summoned = BattleManager.Instance.SpawnEnemyAt(EnemyType.Wolf, position);
            if (summoned == null) continue;
            summoned.SuppressGoldReward();
            summons.Add(summoned);
            if (BattleVfxManager.Instance != null)
                BattleVfxManager.Instance.SpawnDeathParticles(position, new Color(0.3f, 0.7f, 0.25f));
        }
    }

    // ==================== SİS ADIMI ====================

    private IEnumerator FogStep(Vector3 heroPosition)
    {
        casting = true;
        enemy.MovementLocked = true;
        enemy.Invulnerable = true;
        enemy.FogOverride = true;
        yield return Fade(spriteRenderer != null ? spriteRenderer.color.a : 1f, 0f);
        if (enemy.IsDead) yield break;

        transform.position = PickFogStepPosition(heroPosition, transform.position);
        lastPosition = transform.position;
        yield return Fade(0f, 1f);

        enemy.FogOverride = false; // görüş dışındaysa FogOfWar tekrar soluklaştırır
        enemy.Invulnerable = false;
        enemy.MovementLocked = false;
        casting = false;
    }

    /// <summary>Kahramandan sabit uzaklıkta, savaş alanı içinde, mevcut yerin tersine yakın bir nokta.</summary>
    public static Vector3 PickFogStepPosition(Vector3 heroPosition, Vector3 current)
    {
        Vector2 away = (Vector2)(current - heroPosition);
        float baseAngle = away.sqrMagnitude > 0.01f ? Mathf.Atan2(away.y, away.x) : 0f;
        float angle = baseAngle + Mathf.PI + Random.Range(-1.2f, 1.2f);
        Vector3 p = heroPosition + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * FogStepDistance;
        var setup = FindFirstObjectByType<BattleSceneSetup>();
        if (setup != null && setup.worldWidth > 0f)
        {
            float hw = setup.worldWidth * 0.5f - 1f;
            float hh = setup.worldHeight * 0.5f - 1f;
            p.x = Mathf.Clamp(p.x, -hw, hw);
            p.y = Mathf.Clamp(p.y, -hh, hh);
        }
        p.z = 0f;
        return p;
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < FadeTime)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / FadeTime));
            yield return null;
        }
        SetAlpha(to);
    }

    // Can barını da birlikte gizler.
    private void SetAlpha(float alpha) => enemy.SetFogAlpha(alpha);

    private void Cleanup()
    {
        foreach (var pool in pools)
            if (pool != null) Destroy(pool);
        pools.Clear();
        enabled = false;
    }

    private void OnDestroy()
    {
        foreach (var pool in pools)
            if (pool != null) Destroy(pool);
    }
}
