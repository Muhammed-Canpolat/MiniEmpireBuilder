using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Bölge 6 (Ejder İni): her 24 sn'de 4 lav bombası. Üç kuleye birer, bir tanesi kahramanın yakınına.
/// 1,2 sn kırmızı uyarı dairesinden sonra patlar (yapı/kahraman/yer düşmanı azami canın %8'i) ve
/// <see cref="LavaGround"/> bırakır: kahramana ve yer düşmanlarına saniyede %4. Uçanlar bağışık.
/// </summary>
public sealed class VolcanicEruptions : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(Progression.RegionMechanics.EruptionFirstDelay);
        while (true)
        {
            var battle = BattleManager.Instance;
            if (battle != null && battle.IsBattleActive) yield return Erupt();
            yield return new WaitForSeconds(Progression.RegionMechanics.EruptionInterval);
        }
    }

    /// <summary>Hedef noktaları seçer (en çok 3 kule + kahraman yakını).</summary>
    public static List<Vector2> PickTargets()
    {
        var targets = new List<Vector2>();
        var towers = new List<TowerController>();
        foreach (var tower in FindObjectsByType<TowerController>(FindObjectsSortMode.None))
            if (!tower.IsDestroyed) towers.Add(tower);
        for (int i = 0; i < Progression.RegionMechanics.EruptionMaxTowerBombs && towers.Count > 0; i++)
        {
            int pick = Random.Range(0, towers.Count);
            var structure = towers[pick].GetComponent<BattleStructure>();
            targets.Add(structure != null ? structure.BasePoint : (Vector2)towers[pick].transform.position);
            towers.RemoveAt(pick);
        }
        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && !hero.IsDowned)
            targets.Add((Vector2)hero.transform.position
                + Random.insideUnitCircle.normalized * Progression.RegionMechanics.HeroBombOffset);
        return targets;
    }

    private IEnumerator Erupt()
    {
        var targets = PickTargets();
        if (targets.Count == 0) yield break;
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowNoticeBanner(L.T("YANARDAĞ PATLIYOR!", "ERUPTION!"));
        var markers = new List<GameObject>();
        foreach (var target in targets) markers.Add(LavaGround.Telegraph(target));
        yield return new WaitForSeconds(Progression.RegionMechanics.EruptionTelegraphSeconds);
        foreach (var marker in markers) if (marker != null) Destroy(marker);
        foreach (var target in targets) Impact(target);
    }

    public static void Impact(Vector2 position)
    {
        float radius = Progression.RegionMechanics.LavaRadius;
        float fraction = Progression.RegionMechanics.LavaImpactFraction;
        TinyFx.Explosion(position, 2.4f);
        if (BattleVfxManager.Instance != null) BattleVfxManager.Instance.TryScreenShake(40f);

        foreach (var structure in new List<BattleStructure>(BattleStructure.All))
        {
            if (structure == null || structure.IsDestroyed) continue;
            if (Vector2.Distance(structure.BasePoint, position) > radius + 0.6f) continue;
            RegionMechanicsStats.EruptionDamageToPlayer += structure.MaxHealth * fraction;
            structure.TakeDamage(structure.MaxHealth * fraction);
        }
        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && !hero.IsDowned && Vector2.Distance(hero.transform.position, position) <= radius)
        {
            RegionMechanicsStats.EruptionDamageToPlayer += hero.MaxHealth * fraction;
            hero.TakeDamage(hero.MaxHealth * fraction);
        }
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || enemy.IsFlying) continue;
            if (Vector2.Distance(enemy.transform.position, position) > radius) continue;
            LavaGround.DamageEnemy(enemy, enemy.MaxHealth * fraction);
        }
        LavaGround.Create(position, radius, Progression.RegionMechanics.BurnSeconds);
    }
}

/// <summary>Yanan zemin: kahramana ve yer düşmanlarına (uçanlar hariç) zamanla hasar verir.</summary>
public sealed class LavaGround : MonoBehaviour
{
    private float radius;
    private float until;
    private float nextTick;

    public static GameObject Telegraph(Vector2 position)
    {
        float radius = Progression.RegionMechanics.LavaRadius;
        var go = Disc("LavaTelegraph", position, radius, new Color(1f, 0.15f, 0.05f, 0.22f), -70);
        var ring = BattleWorldMarkers.DashedCircle(go.transform, radius, new Color(1f, 0.3f, 0.1f, 0.95f), 20);
        ring.transform.position = new Vector3(position.x, position.y, 0.05f);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.DOFade(0.5f, Progression.RegionMechanics.EruptionTelegraphSeconds / 3f)
            .SetLoops(-1, LoopType.Yoyo).SetLink(go);
        return go;
    }

    public static LavaGround Create(Vector2 position, float radius, float seconds)
    {
        var go = Disc("LavaGround", position, radius, new Color(1f, 0.42f, 0.08f, 0.55f), -69);
        var ground = go.AddComponent<LavaGround>();
        ground.radius = radius;
        ground.until = Time.time + seconds;
        ground.nextTick = Time.time;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.DOFade(0.3f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetLink(go);
        return ground;
    }

    private static GameObject Disc(string name, Vector2 position, float radius, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(position.x, position.y, 0.1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        var sprite = BaseMapManager.MakeCircleSprite(Color.white);
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        go.transform.localScale = Vector3.one * (radius * 2f / sprite.bounds.size.x);
        return go;
    }

    private void Update()
    {
        if (Time.time >= until) { Destroy(gameObject); return; }
        if (Time.time < nextTick) return;
        nextTick = Time.time + Progression.RegionMechanics.BurnTick;
        Tick(Progression.RegionMechanics.BurnFractionPerSecond * Progression.RegionMechanics.BurnTick);
    }

    private void Tick(float fraction)
    {
        Vector2 center = transform.position;
        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && !hero.IsDowned && Vector2.Distance(hero.transform.position, center) <= radius)
        {
            RegionMechanicsStats.EruptionDamageToPlayer += hero.MaxHealth * fraction;
            hero.TakeDamage(hero.MaxHealth * fraction);
        }
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead || enemy.IsFlying) continue;
            if (Vector2.Distance(enemy.transform.position, center) > radius) continue;
            DamageEnemy(enemy, enemy.MaxHealth * fraction);
        }
    }

    /// <summary>Çevre hasarı (DamageSource.Env); Uçanlar çağıran tarafından hariç tutulur.</summary>
    public static void DamageEnemy(EnemyController enemy, float damage)
    {
        if (enemy == null || enemy.IsDead || enemy.IsFlying) return;
        // Boss'lar yüzdelik lav hasarından muaf: aksi hâlde boss canının %20'si zeminde eriyordu.
        if (EnemyTypeInfo.IsBoss(enemy.Type)) return;
        float before = enemy.CurrentHealth;
        enemy.TakeDamage(damage, DamageSource.Env);
        RegionMechanicsStats.EruptionDamageToEnemies += Mathf.Max(0f, before - enemy.CurrentHealth);
    }
}
