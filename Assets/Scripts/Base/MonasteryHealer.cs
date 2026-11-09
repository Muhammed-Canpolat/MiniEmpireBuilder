using UnityEngine;

/// <summary>
/// Savaştaki Manastır: 4 birim içindeki kahramanı, Ana Üs'ü ve kuleleri üç saniyede bir
/// azami canlarının %3'ü kadar iyileştirir. Yıkılınca durur. Düşman davranışını değiştirmez.
/// </summary>
public sealed class MonasteryHealer : MonoBehaviour
{
    private const float Range = 4f;
    private const float Interval = 3f;
    private const float HealFraction = 0.03f;
    private float nextHeal;
    private BattleStructure structure;
    // Savaşta kurulduğu binanın seviyesi; ayarlanmazsa türün seviyesi kullanılır.
    public int Level { get; set; }

    private void Start()
    {
        structure = GetComponent<BattleStructure>();
        nextHeal = Time.time + Interval;
    }

    private Vector2 Origin => structure != null ? structure.BasePoint : (Vector2)transform.position;

    private void Update()
    {
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
            if (Vector2.Distance(Origin, other.BasePoint) > Range) continue;
            other.Heal(other.MaxHealth * HealFraction);
            healed = true;
        }
        if (!healed) return;
        var garrison = GetComponent<BattleGarrison>();
        if (garrison != null) garrison.PlayHeal();
    }
}
