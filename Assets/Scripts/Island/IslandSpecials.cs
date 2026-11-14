using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ada düşmanının tür özel yetenekleri (island_base_v2 §7.3 "Özel" sütunu), <see cref="IslandEnemy.Spawn"/> ile türe göre eklenir:
/// Kemik Büyücü şifa · Hayalet dokunulmazlık · Vampir can çalma · İmp yangın · Örümcek ağ · Sukkubus Lancer'ı durdurma ·
/// Magma Golem ölüm yangını · Batık Şövalye iskelet çağırma (%50 canda 4) · Kızıl Ejder koni ateş (10 sn'de bir).
/// </summary>
public sealed class IslandSpecials : MonoBehaviour
{
    private IslandEnemy enemy;
    private EnemyController controller;
    private float nextPulse, phaseUntil, lastSeenAttack;
    private bool summoned;
    /// <summary>Test/telemetri: şifa vuruşları, Ejder ateşleri, Hayalet dokunulmaz evreleri, Sukkubus durdurmaları, çağrılar.</summary>
    public int Pulses { get; private set; }

    /// <summary>Türün özel yeteneği var mı (Spawn yalnız bunlara bileşen ekler).</summary>
    public static bool Has(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.BoneMage: case EnemyType.Ghost: case EnemyType.Vampire: case EnemyType.Imp: case EnemyType.Spider:
            case EnemyType.Succubus: case EnemyType.MagmaGolem: case EnemyType.SunkenKnight: case EnemyType.RedDragon:
                return true;
            default: return false;
        }
    }

    public static IslandSpecials Attach(IslandEnemy owner)
    {
        var specials = owner.gameObject.AddComponent<IslandSpecials>();
        specials.enemy = owner;
        specials.controller = owner.Controller;
        specials.lastSeenAttack = owner.Controller.LastAttackTime;
        float first = owner.Type == EnemyType.RedDragon ? 4f : owner.Type == EnemyType.Ghost ? Progression.Island.GhostPhaseEvery * 0.5f
            : owner.Type == EnemyType.Succubus ? Progression.Island.SuccubusStunEvery * 0.5f : owner.Type == EnemyType.BoneMage ? 1.5f : 0f;
        specials.nextPulse = Time.time + first;
        owner.Controller.Died += specials.OnDied;
        return specials;
    }

    private void Update()
    {
        if (enemy == null || controller == null || controller.IsDead) return;
        bool attacked = controller.LastAttackTime != lastSeenAttack;
        if (attacked) lastSeenAttack = controller.LastAttackTime;
        switch (enemy.Type)
        {
            case EnemyType.BoneMage: MageHeal(); break;
            case EnemyType.Ghost: GhostPhase(); break;
            case EnemyType.Vampire: if (attacked && controller.CurrentTarget != null) Lifesteal(); break;
            case EnemyType.Imp: if (attacked) IgniteTarget(); break;
            case EnemyType.Spider: if (attacked) WebTarget(); break;
            case EnemyType.Succubus: Stun(); break;
            case EnemyType.SunkenKnight: KnightSummon(); break;
            case EnemyType.RedDragon: DragonFire(); break;
        }
    }

    // Kemik Büyücü: 3 sn'de bir 2,5 u içindeki (kendisi hariç) düşmanlara azami canın %6'sı
    private void MageHeal()
    {
        if (Time.time < nextPulse) return;
        nextPulse = Time.time + Progression.Island.MageHealEvery;
        bool any = false;
        foreach (var other in IslandEnemy.All)
        {
            if (other == null || other == enemy || !other.Alive) continue;
            if (Vector2.Distance(other.Position, enemy.Position) > Progression.Island.MageHealRadius) continue;
            var c = other.Controller;
            float before = c.CurrentHealth;
            c.Heal(c.MaxHealth * Progression.Island.MageHealFraction);
            float healed = c.CurrentHealth - before;
            if (healed <= 0f) continue;
            IslandStats.MageHealed += healed;
            any = true;
            IslandFx.Play("heal_fx", other.AimPoint, 0.9f, 16f);
        }
        if (any) Pulses++;
    }

    // Hayalet: 6 sn'de bir 1,5 sn hasar almaz (saydamlaşır)
    private void GhostPhase()
    {
        if (Time.time >= nextPulse)
        {
            nextPulse = Time.time + Progression.Island.GhostPhaseEvery;
            phaseUntil = Time.time + Progression.Island.GhostPhaseSeconds;
            Pulses++;
        }
        bool phased = Time.time < phaseUntil;
        controller.Invulnerable = phased;
        enemy.TintAlpha = phased ? 0.35f : 1f;
    }

    // Vampir: vuruş hasarının %30'u kadar can
    private void Lifesteal()
    {
        float before = controller.CurrentHealth;
        controller.Heal(controller.Damage * Progression.Island.VampireLifesteal);
        IslandStats.VampireHealed += controller.CurrentHealth - before;
    }

    // İmp: vurduğu binayı 3 sn boyunca %4/sn yakar
    private void IgniteTarget()
    {
        var target = controller.CurrentTarget;
        var building = target != null ? target.GetComponent<IslandBuilding>() : null;
        if (building != null && !building.Ruined) IslandBurn.Ignite(building, Progression.Island.ImpBurnSeconds, Progression.Island.ImpBurnFractionPerSecond);
    }

    // Örümcek: vurduğu kule/okçunun atış hızı 2 sn −%40
    private void WebTarget()
    {
        var target = controller.CurrentTarget;
        if (target == null) return;
        var tower = target.GetComponent<IslandTower>();
        if (tower != null) { tower.Web(Progression.Island.WebSeconds); IslandFx.Play("dust_2", tower.transform.position, 1.2f, 16f); return; }
        var archer = target.GetComponent<IslandArcher>();
        if (archer != null) { archer.Web(Progression.Island.WebSeconds); IslandFx.Play("dust_2", archer.Position, 1.2f, 16f); }
    }

    // Sukkubus: 8 sn'de bir menzildeki bir Lancer'ı 2 sn durdurur
    private void Stun()
    {
        if (Time.time < nextPulse) return;
        IslandLancer best = null;
        float bestDistance = controller.AttackRange + 1f;
        foreach (var lancer in IslandLancer.All)
        {
            if (lancer == null || !lancer.Alive || lancer.Stunned) continue;
            float d = Vector2.Distance(lancer.Position, enemy.Position);
            if (d < bestDistance) { bestDistance = d; best = lancer; }
        }
        if (best == null) return;
        nextPulse = Time.time + Progression.Island.SuccubusStunEvery;
        best.Stun(Progression.Island.SuccubusStunSeconds);
        IslandFx.FloatText(best.Position + Vector2.up * 0.8f, L.T("DONDU!", "STUNNED!"), new Color(0.85f, 0.6f, 1f), 4f);
        Pulses++;
    }

    // Batık Şövalye: %50 canda 4 İskelet Savaşçı çağırır
    private void KnightSummon()
    {
        if (summoned || controller.HealthPercent > Progression.Island.KnightSummonAtHealth) return;
        summoned = true;
        Pulses++;
        IslandFx.FloatText(enemy.Position + Vector2.up * 2f, L.T("KALKIN!", "RISE!"), new Color(0.7f, 0.9f, 1f), 4.5f);
        for (int i = 0; i < Progression.Island.KnightSummonCount; i++)
        {
            Vector2 at = enemy.Position + new Vector2((i - 1.5f) * 0.7f, Random.Range(-0.3f, 0.3f));
            IslandEnemy.Spawn(EnemyType.SkeletonWarrior, enemy.Level, enemy.CastleTransform, at, enemy.RouteFromHere(at), enemy.BeachIndex, false, false);
            IslandStats.CrewTotal++; IslandStats.Landed++;
            IslandFx.Play("dust_2", at, 1.3f, 16f);
        }
    }

    // Kızıl Ejder: 10 sn'de bir 3 u'luk koni ateş; kule ve okçuyu vurur (hasar = 2 × vuruş)
    private void DragonFire()
    {
        if (Time.time < nextPulse) return;
        // Hedef: 4,5 u içinde okçu ya da en yakın kule; yoksa ateş bekler
        Vector2 origin = enemy.AimPoint;
        Vector2? aim = null;
        var archer = IslandArcher.Instance;
        float reach = Progression.Island.DragonFireLength + 1.5f;
        if (archer != null && archer.IsAlive && Vector2.Distance(origin, archer.Position) <= reach) aim = archer.Position;
        float best = reach;
        if (aim == null)
            foreach (var building in IslandBuilding.All)
            {
                if (building == null || building.Ruined || !building.IsTower) continue;
                float d = Vector2.Distance(origin, building.Center);
                if (d < best) { best = d; aim = building.Center; }
            }
        if (aim == null) return;
        nextPulse = Time.time + Progression.Island.DragonFireEvery;
        Pulses++;
        Vector2 dir = (aim.Value - origin).normalized;
        float damage = controller.Damage * Progression.Island.DragonFireDamageMult;
        IslandFx.FloatText(origin + Vector2.up * 1.5f, L.T("ALEV!", "FLAMES!"), new Color(1f, 0.5f, 0.2f), 5f);
        for (int i = 1; i <= 4; i++) IslandFx.Play("fire_" + (i % 3 + 1), origin + dir * (i * 0.8f), 1.1f + 0.15f * i, 14f);
        foreach (var building in IslandBuilding.All)
        {
            if (building == null || building.Ruined || !building.IsTower) continue;
            if (!InCone(origin, dir, building.Center)) continue;
            IslandStats.RecordEnemyHit(controller, building.transform, damage);
            building.Structure.TakeDamage(damage);
        }
        if (archer != null && archer.IsAlive && InCone(origin, dir, archer.Position))
        {
            IslandStats.RecordEnemyHit(controller, archer.transform, damage);
            archer.TakeDamage(damage);
        }
    }

    private static bool InCone(Vector2 origin, Vector2 dir, Vector2 point)
    {
        Vector2 to = point - origin;
        float d = to.magnitude;
        return d <= Progression.Island.DragonFireLength + 0.8f && (d < 0.2f || Vector2.Angle(dir, to) <= 35f);
    }

    // Magma Golem: ölünce 1,5 u içindeki binalar 3 sn yanar
    private void OnDied(EnemyController dead)
    {
        if (enemy == null || enemy.Type != EnemyType.MagmaGolem) return;
        Vector2 at = enemy.Position;
        IslandFx.Play("fire_2", at, 2.2f, 12f);
        foreach (var building in new List<IslandBuilding>(IslandBuilding.All))
        {
            if (building == null || building.Ruined) continue;
            if (Vector2.Distance(at, building.Center) - building.Radius > Progression.Island.MagmaBurnRadius) continue;
            IslandBurn.Ignite(building, Progression.Island.MagmaBurnSeconds, Progression.Island.MagmaBurnFractionPerSecond);
        }
    }
}

/// <summary>Yanan bina (İmp, Magma Golem): süre boyunca azami canın oranı kadar saniyede hasar.</summary>
public sealed class IslandBurn : MonoBehaviour
{
    private IslandBuilding building;
    private float until, fraction, nextTick;
    private GameObject flame;
    public static int Ignitions;

    public static void Ignite(IslandBuilding target, float seconds, float fractionPerSecond)
    {
        if (target == null || target.Structure == null) return;
        var burn = target.GetComponent<IslandBurn>();
        if (burn == null) burn = target.gameObject.AddComponent<IslandBurn>();   // Unity sahte-null: ?? kullanılmaz
        burn.building = target;
        burn.until = Time.time + seconds;
        burn.fraction = Mathf.Max(burn.fraction, fractionPerSecond);
        burn.nextTick = Time.time + 0.5f;
        Ignitions++;
        if (burn.flame == null) burn.flame = IslandFx.Play("fire_1", target.Center, 1.3f, 12f, true, target.transform);
    }

    private void Update()
    {
        if (building == null || building.Ruined || Time.time >= until) { Stop(); return; }
        if (Time.time < nextTick) return;
        nextTick = Time.time + 0.5f;
        building.Structure.TakeDamage(building.Structure.MaxHealth * fraction * 0.5f);
    }

    private void Stop()
    {
        if (flame != null) Destroy(flame);
        Destroy(this);
    }
}
