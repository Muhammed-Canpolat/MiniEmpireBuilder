using UnityEngine;

/// <summary>
/// Savaştaki Manastır: 4 birim içindeki kahramanı, Ana Üs'ü ve kuleleri üç saniyede bir
/// azami canlarının %(2 + seviye)'si kadar iyileştirir (Progression.MonasteryHealFraction). Yıkılınca durur. Düşman davranışını değiştirmez.
/// </summary>
public sealed class MonasteryHealer : MonoBehaviour
{
    public const float Range = 4f;
    private RangeRing ring;
    private float ringUntil;
    private HeroController heroRef;
    private const float Interval = 3f;
    private float HealFraction => Progression.MonasteryHealFraction(Level);
    private float nextHeal;
    private BattleStructure structure;
    // Savaşta kurulduğu binanın seviyesi; ayarlanmazsa türün seviyesi kullanılır.
    public int Level { get; set; }

    private void Start()
    {
        structure = GetComponent<BattleStructure>();
        nextHeal = Time.time + Interval;
        // 6. oturum (1.2): iyileştirme menzili çemberi (yeşilimsi), kulelerle aynı kural: kurulunca 2,5 sn ve kahraman yaklaşınca.
        if (structure != null)
        {
            ring = RangeRing.Attach(gameObject, () => Origin, Range, new Color(0.55f, 0.95f, 0.6f, 0.35f), 30);
            ringUntil = Time.time + 2.5f;
        }
    }

    /// <summary>Menzil merkezi = yapının görsel/mantıksal merkezi (TowerController ile aynı kural).</summary>
    public Vector2 Origin => structure != null ? structure.RangeCenter : (Vector2)transform.position;

    private void Update()
    {
        if (ring != null)
        {
            if (heroRef == null) heroRef = FindFirstObjectByType<HeroController>();
            bool near = heroRef != null && Vector2.Distance(heroRef.transform.position, Origin) < 3.2f;
            ring.SetVisible(!(structure != null && structure.IsDestroyed) && (near || Time.time < ringUntil));
        }
        if (Time.time < nextHeal) return;
        nextHeal = Time.time + Interval;
        if (structure != null && structure.IsDestroyed) return;
        bool healed = false;

        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && hero.CurrentHealth > 0f && hero.CurrentHealth < hero.MaxHealth &&
            Vector2.Distance(Origin, hero.transform.position) <= Range)
        {
            hero.Heal(hero.MaxHealth * HealFraction);
            healed = true;
        }
        var mainBase = FindFirstObjectByType<MainBaseController>();
        if (mainBase != null && !mainBase.IsDestroyed && mainBase.CurrentHealth < mainBase.MaxHealth &&
            Vector2.Distance(Origin, mainBase.transform.position) <= Range)
        {
            mainBase.Heal(mainBase.MaxHealth * HealFraction);
            healed = true;
        }
        foreach (var other in BattleStructure.All)
        {
            if (other == null || other == structure || other.IsDestroyed || other.HealthPercent >= 1f) continue;
            if (Vector2.Distance(Origin, other.RangeCenter) > Range) continue;
            other.Heal(other.MaxHealth * HealFraction);
            healed = true;
        }
        if (!healed) return;
        var garrison = GetComponent<BattleGarrison>();
        if (garrison != null) garrison.PlayHeal();
    }
}
