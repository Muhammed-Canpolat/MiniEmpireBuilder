using UnityEngine;

[CreateAssetMenu(menuName = "Mini Empire/Apprentice Progression")]
public sealed class ApprenticeProgressionData : ScriptableObject
{
    public float healthFraction = 0.5f;
    public float damageFraction = 0.25f;
    public float[] levelMultipliers = { 1f, 1.3f, 1.65f, 2.05f, 2.5f };
    public int[] mineBonuses = { 10, 15, 20, 25, 30 };

    private static ApprenticeProgressionData instance;
    public static ApprenticeProgressionData Instance => instance != null ? instance :
        instance = Resources.Load<ApprenticeProgressionData>("ApprenticeProgressionData");

    public int Health(WeaponType weapon, int level)
    {
        var hero = new HeroData { weaponType = weapon, level = 1 };
        hero.ApplyLevelStats();
        return Mathf.RoundToInt(hero.maxHealth * healthFraction * Multiplier(level));
    }

    public int Damage(WeaponType weapon, int level)
    {
        var hero = new HeroData { weaponType = weapon, level = 1 };
        hero.ApplyLevelStats();
        return Mathf.RoundToInt(hero.damage * damageFraction * Multiplier(level));
    }

    public int MineBonus(int level) => mineBonuses[Mathf.Clamp(level - 1, 0, mineBonuses.Length - 1)];
    public float Multiplier(int level) => levelMultipliers[Mathf.Clamp(level - 1, 0, levelMultipliers.Length - 1)];
}
