using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bölge 1/3/4/5 bossları: Trol gövdesi üstünde kendi rengi, ayak altında aura ve iki yeteneği.
///   Kale Muhafızı (B1): Kalkan Duvarı (3,5 sn %70 az hasar) · Hücum (savaşçıya atılır, sersemletir)
///   Maden Gardiyanı (B3): Kaya Yağmuru (uyarı halkası → kaya; savaşçı ve yapılar) · Yer Sarsıntısı
///   Buz Hükümdarı (B4): Buz Nefesi (yakındaki kuleler 3 sn donar, savaşçı yavaşlar) · Buz Halkası
///   Karanlık Kral (B5): Gölge Çağrısı (Gölge Kurtlar) · Can Emme (savaşçıdan çalıp kendini iyileştirir)
/// Her yetenek önce görünür bir uyarı verir: oyuncunun kaçma şansı olur. Can %50 altında daha sık.
/// </summary>
public sealed class RegionBoss : MonoBehaviour
{
    public const float Cooldown = 8.5f;
    public const float EnragedCooldown = 6f;
    public const float AbilityRange = 8f;

    private static Sprite circle, square;
    private static Sprite Circle => circle != null ? circle : circle = BaseMapManager.MakeCircleSprite(Color.white);
    private static Sprite Square => square != null ? square : square = BaseMapManager.MakeSquareSprite(Color.white);

    private EnemyController enemy;
    private EnemyType type;
    private SpriteRenderer aura;
    private float nextAbility;
    private int rotation;
    private bool busy;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<EnemyController> summons = new List<EnemyController>();

    public static Color Tint(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.CastleGuardian: return new Color(0.78f, 0.84f, 1f);
            case EnemyType.MineWarden: return new Color(1f, 0.8f, 0.48f);
            case EnemyType.FrostLord: return new Color(0.62f, 0.88f, 1f);
            default: return new Color(0.62f, 0.42f, 0.78f);
        }
    }

    private static Color AuraColor(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.CastleGuardian: return new Color(0.55f, 0.7f, 1f, 0.35f);
            case EnemyType.MineWarden: return new Color(1f, 0.72f, 0.2f, 0.35f);
            case EnemyType.FrostLord: return new Color(0.7f, 0.95f, 1f, 0.4f);
            default: return new Color(0.55f, 0.15f, 0.75f, 0.4f);
        }
    }

    private void Start()
    {
        enemy = GetComponent<EnemyController>();
        type = enemy != null ? enemy.Type : EnemyType.CastleGuardian;
        nextAbility = Time.time + 4f;
        var auraObject = new GameObject("BossAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = new Vector3(0f, -0.35f / Mathf.Max(0.01f, transform.localScale.y), 0f);
        aura = auraObject.AddComponent<SpriteRenderer>();
        aura.sprite = Circle;
        aura.color = AuraColor(type);
        aura.sortingOrder = -45;
        float s = 2.4f / Mathf.Max(0.01f, transform.localScale.x);
        auraObject.transform.localScale = new Vector3(s, s * 0.45f, 1f);
    }

    private void Update()
    {
        if (enemy == null) return;
        if (enemy.IsDead) { Cleanup(); return; }
        if (aura != null)
        {
            var c = AuraColor(type);
            aura.color = new Color(c.r, c.g, c.b, c.a * (0.7f + 0.3f * Mathf.Sin(Time.time * 3f)));
        }
        if (busy || Time.time < nextAbility) return;
        var hero = FindFirstObjectByType<HeroController>();
        if (hero == null || hero.IsDowned) return;
        if (Vector2.Distance(transform.position, hero.transform.position) > AbilityRange) return;
        nextAbility = Time.time + (enemy.HealthPercent < 0.5f ? EnragedCooldown : Cooldown);
        bool first = rotation++ % 2 == 0;
        switch (type)
        {
            case EnemyType.CastleGuardian: StartCoroutine(first ? ShieldWall() : Charge(hero)); break;
            case EnemyType.MineWarden: StartCoroutine(first ? RockRain(hero) : GroundSlam()); break;
            case EnemyType.FrostLord: StartCoroutine(first ? FrostBreath() : IceRing(hero)); break;
            default: StartCoroutine(first ? ShadowCall() : LifeDrain(hero)); break;
        }
    }

    private void OnDestroy() => Cleanup();

    private void Cleanup()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
        if (enemy != null)
        {
            enemy.DamageTakenMultiplier = 1f;
            enemy.MovementLocked = false;
        }
        if (aura != null) aura.enabled = false;
    }

    // ==================== YARDIMCILAR ====================

    /// <summary>Yerde büyüyen uyarı halkası; süre sonunda kaybolur.</summary>
    private GameObject Warning(Vector3 at, float radius, Color color, float seconds)
    {
        var go = new GameObject("BossWarning");
        go.transform.position = new Vector3(at.x, at.y, 0f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle;
        renderer.sortingOrder = -40;
        spawned.Add(go);
        StartCoroutine(Grow(go, renderer, radius * 2f, color, seconds));
        return go;
    }

    private static IEnumerator Grow(GameObject go, SpriteRenderer renderer, float diameter, Color color, float seconds)
    {
        float t = 0f;
        while (t < seconds && go != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            go.transform.localScale = Vector3.one * diameter * Mathf.Lerp(0.25f, 1f, k);
            renderer.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.12f, color.a, k));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private IEnumerator WindUp(float seconds)
    {
        busy = true;
        enemy.MovementLocked = true;
        var sr = GetComponent<SpriteRenderer>();
        float end = Time.time + seconds;
        while (Time.time < end && !enemy.IsDead)
        {
            if (sr != null)
            {
                float k = Mathf.PingPong(Time.time * 6f, 1f);
                sr.color = Color.Lerp(enemy.BaseTint, Color.white, k * 0.6f);
            }
            yield return null;
        }
        if (sr != null) sr.color = enemy.BaseTint;
    }

    private void Done()
    {
        busy = false;
        if (enemy != null) enemy.MovementLocked = false;
    }

    private void HitArea(Vector3 center, float radius, float heroFactor, float structureFactor)
    {
        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && !hero.IsDowned && Vector2.Distance(hero.transform.position, center) <= radius)
            hero.TakeDamage(enemy.Damage * heroFactor);
        if (structureFactor <= 0f) return;
        foreach (var structure in FindObjectsByType<BattleStructure>(FindObjectsSortMode.None))
            if (structure != null && !structure.IsDestroyed && Vector2.Distance(structure.BasePoint, center) <= radius + 0.6f)
                structure.TakeDamage(enemy.Damage * structureFactor);
        var mainBase = FindFirstObjectByType<MainBaseController>();
        if (mainBase != null && Vector2.Distance(mainBase.transform.position, center) <= radius + 1.2f)
            mainBase.TakeDamage(enemy.Damage * structureFactor);
    }

    // ==================== KALE MUHAFIZI ====================

    private IEnumerator ShieldWall()
    {
        busy = true;
        var bubble = new GameObject("ShieldWall");
        bubble.transform.SetParent(transform, false);
        var renderer = bubble.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle;
        renderer.sortingOrder = 30;
        float s = 2.6f / Mathf.Max(0.01f, transform.localScale.x);
        bubble.transform.localScale = new Vector3(s, s, 1f);
        spawned.Add(bubble);
        enemy.DamageTakenMultiplier = 0.3f;
        float end = Time.time + 3.5f;
        while (Time.time < end && !enemy.IsDead)
        {
            renderer.color = new Color(0.55f, 0.75f, 1f, 0.22f + 0.1f * Mathf.Sin(Time.time * 8f));
            yield return null;
        }
        enemy.DamageTakenMultiplier = 1f;
        if (bubble != null) Destroy(bubble);
        busy = false;
    }

    private IEnumerator Charge(HeroController hero)
    {
        Vector3 target = hero.transform.position;
        var warning = Warning(target, 1.3f, new Color(1f, 0.3f, 0.2f, 0.4f), 0.8f);
        yield return WindUp(0.8f);
        if (enemy.IsDead) { Done(); yield break; }
        Vector3 start = transform.position;
        float t = 0f;
        while (t < 0.35f && !enemy.IsDead)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / 0.35f));
            yield return null;
        }
        if (warning != null) Destroy(warning);
        HitArea(target, 1.3f, 1.5f, 0f);
        if (hero != null && Vector2.Distance(hero.transform.position, target) <= 1.3f) hero.Slow(0.35f, 1.5f);
        Done();
    }

    // ==================== MADEN GARDİYANI ====================

    private IEnumerator RockRain(HeroController hero)
    {
        busy = true;
        var targets = new List<Vector3> { hero.transform.position };
        foreach (var structure in FindObjectsByType<BattleStructure>(FindObjectsSortMode.None))
            if (structure != null && !structure.IsDestroyed && targets.Count < 3) targets.Add(structure.BasePoint);
        while (targets.Count < 3)
            targets.Add(hero.transform.position + (Vector3)(Random.insideUnitCircle * 2.5f));
        foreach (var at in targets) Warning(at, 1.1f, new Color(1f, 0.55f, 0.15f, 0.45f), 1.2f);
        yield return new WaitForSeconds(1.2f);
        if (!enemy.IsDead)
            foreach (var at in targets)
            {
                TinyFx.Explosion(at, 1.4f);
                HitArea(at, 1.1f, 1.2f, 1.5f);
            }
        busy = false;
    }

    private IEnumerator GroundSlam()
    {
        Warning(transform.position, 2.5f, new Color(0.9f, 0.6f, 0.2f, 0.4f), 0.9f);
        yield return WindUp(0.9f);
        if (!enemy.IsDead)
        {
            TinyFx.Explosion(transform.position, 2f);
            HitArea(transform.position, 2.5f, 1.0f, 0.8f);
            var hero = FindFirstObjectByType<HeroController>();
            if (hero != null && Vector2.Distance(hero.transform.position, transform.position) <= 2.5f) hero.Slow(0.5f, 2f);
        }
        Done();
    }

    // ==================== BUZ HÜKÜMDARI ====================

    private IEnumerator FrostBreath()
    {
        const float radius = 5f;
        Warning(transform.position, radius, new Color(0.7f, 0.95f, 1f, 0.3f), 0.9f);
        yield return WindUp(0.9f);
        if (!enemy.IsDead)
        {
            foreach (var tower in FindObjectsByType<TowerController>(FindObjectsSortMode.None))
                if (tower != null && Vector2.Distance(tower.transform.position, transform.position) <= radius + 0.8f)
                    tower.Freeze(3f);
            var hero = FindFirstObjectByType<HeroController>();
            if (hero != null && !hero.IsDowned && Vector2.Distance(hero.transform.position, transform.position) <= radius)
            {
                hero.TakeDamage(enemy.Damage * 0.8f);
                hero.Slow(0.5f, 3f);
            }
        }
        Done();
    }

    private IEnumerator IceRing(HeroController hero)
    {
        busy = true;
        Vector3 at = hero.transform.position;
        Warning(at, 1.6f, new Color(0.6f, 0.9f, 1f, 0.45f), 1f);
        yield return new WaitForSeconds(1f);
        if (!enemy.IsDead)
        {
            HitArea(at, 1.6f, 1.1f, 0f);
            var current = FindFirstObjectByType<HeroController>();
            if (current != null && Vector2.Distance(current.transform.position, at) <= 1.6f) current.Slow(0.3f, 2f);
        }
        busy = false;
    }

    // ==================== KARANLIK KRAL ====================

    public const int MaxLiveSummons = 4;

    private IEnumerator ShadowCall()
    {
        yield return WindUp(0.7f);
        summons.RemoveAll(s => s == null || s.IsDead);
        int room = Mathf.Min(2, MaxLiveSummons - summons.Count);
        for (int i = 0; i < room && !enemy.IsDead && BattleManager.Instance != null; i++)
        {
            float angle = i * Mathf.PI + Random.Range(-0.4f, 0.4f);
            Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.4f;
            var summoned = BattleManager.Instance.SpawnEnemyAt(EnemyType.ShadowWolf, position);
            if (summoned == null) continue;
            summoned.SuppressGoldReward();
            summons.Add(summoned);
            if (BattleVfxManager.Instance != null)
                BattleVfxManager.Instance.SpawnDeathParticles(position, new Color(0.5f, 0.2f, 0.7f));
        }
        Done();
    }

    private IEnumerator LifeDrain(HeroController hero)
    {
        busy = true;
        var beam = new GameObject("LifeDrainBeam");
        var renderer = beam.AddComponent<SpriteRenderer>();
        renderer.sprite = Square;
        renderer.sortingOrder = 35;
        spawned.Add(beam);
        float end = Time.time + 1.2f;
        while (Time.time < end && hero != null && !enemy.IsDead)
        {
            Vector3 a = transform.position, b = hero.transform.position;
            Vector3 mid = (a + b) * 0.5f;
            beam.transform.position = new Vector3(mid.x, mid.y, 0f);
            beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            beam.transform.localScale = new Vector3(Vector2.Distance(a, b), 0.12f, 1f);
            renderer.color = new Color(0.65f, 0.2f, 0.85f, 0.45f + 0.25f * Mathf.Sin(Time.time * 20f));
            yield return null;
        }
        if (beam != null) Destroy(beam);
        if (hero != null && !hero.IsDowned && !enemy.IsDead &&
            Vector2.Distance(hero.transform.position, transform.position) <= AbilityRange)
        {
            float amount = enemy.Damage * 1.2f;
            hero.TakeDamage(amount);
            enemy.Heal(amount);
        }
        busy = false;
    }
}
