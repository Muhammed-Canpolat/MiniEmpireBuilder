using UnityEngine;

[CreateAssetMenu(menuName = "Mini Empire/Apprentice Progression")]
public sealed class ApprenticeProgressionData : ScriptableObject
{
    public float healthFraction = 0.5f;
    public float damageFraction = 0.25f;
    public float[] levelMultipliers = { 1f, 1.3f, 1.65f, 2.05f, 2.5f, 2.8f, 3.1f, 3.4f, 3.7f, 4f, 4.3f, 4.6f, 4.9f, 5.2f, 5.5f };
    public int[] mineBonuses = { 10, 15, 20, 25, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50 };

    private static ApprenticeProgressionData instance;
    public static ApprenticeProgressionData Instance => instance != null ? instance :
        instance = Resources.Load<ApprenticeProgressionData>("ApprenticeProgressionData");

    public int Health(WeaponType weapon, int level)
    {
        var hero = new HeroData { weaponType = weapon, level = 1 };
        hero.ApplyLevelStats();
        // D128: kahraman canı değişti; çırak eski Balta tabanından (200) hesaplanmaya devam eder.
        float baseHealth = weapon == WeaponType.Axe ? Progression.ApprenticeAxeBaseHealth : hero.maxHealth;
        return Mathf.RoundToInt(baseHealth * healthFraction * Multiplier(level));
    }

    public int Damage(WeaponType weapon, int level)
    {
        var hero = new HeroData { weaponType = weapon, level = 1 };
        hero.ApplyLevelStats();
        return Mathf.RoundToInt(hero.damage * damageFraction * Multiplier(level));
    }

    /// <summary>Çırak maden bonusu (%): AŞAMA 4'ten beri etkin ekonomi profilinden (asset'teki mineBonuses = LEGACY kopyası, değiştirilmedi).</summary>
    public int MineBonus(int level) => Economy.Active.ApprenticeMineBonusPercent(level);
    public float Multiplier(int level) => levelMultipliers[Mathf.Clamp(level - 1, 0, levelMultipliers.Length - 1)];
}
