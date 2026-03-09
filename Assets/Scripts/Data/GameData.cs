using UnityEngine;
using System;
using System.Collections.Generic;

// ==================== ENUM'LAR ====================

/// <summary>
/// Ana Savaşçının silah tipi — oyun başında oyuncu seçer
/// </summary>
public enum WeaponType
{
    Axe,    // Balta — Yüksek hasar, kısa menzil
    Spear,  // Mızrak — Savunmacı, yüksek dayanıklılık
    Bow     // Ok — Orta hasar, uzun menzil
}

/// <summary>
/// Bina türleri
/// </summary>
public enum BuildingType
{
    MainBase,       // Ana Üs
    ArcherTower,    // Okçu Kulesi
    CannonTower,    // Topçu Kulesi
    GoldMine,       // Altın Madeni
    WallBuilder     // Duvarcı (duvar inşa eden bina)
}

/// <summary>
/// Düşman türleri
/// </summary>
public enum EnemyType
{
    Wolf,       // Kurt — hızlı, düşük can
    Zombie,     // Zombi — yavaş, orta can
    Orc,        // Ork — güçlü, yüksek can
    Troll       // Trol — boss, çok yüksek can
}

// ==================== VERİ SINIFLARI ====================

/// <summary>
/// Ana Savaşçı verileri
/// </summary>
[Serializable]
public class HeroData
{
    public WeaponType weaponType;
    public int level = 1;

    // Hesaplanan istatistikler
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float damage = 10f;
    public float attackRange = 1.5f;
    public float attackSpeed = 1f;      // saldırı / saniye
    public float moveSpeed = 3f;

    /// <summary>
    /// Silah tipine ve seviyeye göre istatistikleri hesapla
    /// </summary>
    public void ApplyLevelStats()
    {
        float levelMultiplier = 1f + (level - 1) * 0.15f;

        switch (weaponType)
        {
            case WeaponType.Axe:
                damage = 15f * levelMultiplier;
                attackRange = 1.5f;
                attackSpeed = 0.8f;
                maxHealth = 130f * levelMultiplier;
                moveSpeed = 2.5f;
                break;

            case WeaponType.Spear:
                damage = 9f * levelMultiplier;
                attackRange = 2.8f;
                attackSpeed = 1.0f;
                maxHealth = 125f * levelMultiplier;
                moveSpeed = 2.8f;
                break;

            case WeaponType.Bow:
                damage = 7f * levelMultiplier;
                attackRange = 6f;
                attackSpeed = 1.2f;
                maxHealth = 80f * levelMultiplier;
                moveSpeed = 3.2f;
                break;
        }

        currentHealth = maxHealth;
    }
}

/// <summary>
/// Bina verileri
/// </summary>
[Serializable]
public class BuildingData
{
    public BuildingType buildingType;
    public int level = 0;
    public bool isUnlocked = false;

    // Hesaplanan istatistikler
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float damage = 0f;
    public float attackRange = 0f;
    public float attackSpeed = 0f;
    public int unitCount = 0;
    public float goldPerSecond = 0f;

    /// <summary>
    /// Bina tipine ve seviyeye göre istatistikleri hesapla
    /// </summary>
    public void ApplyLevelStats()
    {
        if (level <= 0) return;

        switch (buildingType)
        {
            case BuildingType.MainBase:
                maxHealth = 200f + (level - 1) * 100f;
                currentHealth = maxHealth;
                break;

            case BuildingType.ArcherTower:
                maxHealth = 80f + (level - 1) * 30f;
                currentHealth = maxHealth;
                damage = 8f + (level - 1) * 3f;
                unitCount = 1 + (level / 3);          // her 3 seviyede +1 birim
                attackRange = 6f;
                attackSpeed = 1.5f;
                break;

            case BuildingType.CannonTower:
                maxHealth = 120f + (level - 1) * 40f;
                currentHealth = maxHealth;
                damage = 20f + (level - 1) * 8f;      // Okçudan fazla hasar
                unitCount = 1 + (level / 4);           // her 4 seviyede +1 birim
                attackRange = 5f;
                attackSpeed = 0.5f;                    // Yavaş ama güçlü
                break;

            case BuildingType.GoldMine:
                maxHealth = 50f + (level - 1) * 10f;
                currentHealth = maxHealth;
                goldPerSecond = 1f + (level - 1) * 0.5f;
                break;

            case BuildingType.WallBuilder:
                // Duvarcı: Duvar inşa eder, seviyesi duvar HP'sini belirler
                maxHealth = 60f + (level - 1) * 20f;
                currentHealth = maxHealth;
                break;
        }
    }

    /// <summary>
    /// Bir sonraki seviye için gereken altın
    /// </summary>
    public int GetUpgradeCost()
    {
        int nextLevel = level + 1;

        switch (buildingType)
        {
            case BuildingType.MainBase:
                return nextLevel * 100;

            case BuildingType.ArcherTower:
                return nextLevel * 50;

            case BuildingType.CannonTower:
                return nextLevel * 80;    // Okçudan pahalı

            case BuildingType.GoldMine:
                return nextLevel * 60;

            case BuildingType.WallBuilder:
                return nextLevel * 45;

            default:
                return nextLevel * 50;
        }
    }

    /// <summary>
    /// Bu bina için gereken minimum Ana Üs seviyesi
    /// </summary>
    public int GetRequiredMainBaseLevel()
    {
        switch (buildingType)
        {
            case BuildingType.MainBase:
                return 0;   // Her zaman var

            case BuildingType.ArcherTower:
                return 2;   // Ana Üs Lv.2'de açılır

            case BuildingType.CannonTower:
                return 4;   // Ana Üs Lv.4'te açılır

            case BuildingType.GoldMine:
                return 1;   // Ana Üs Lv.1'de açılır (baştan)

            case BuildingType.WallBuilder:
                return 2;   // Ana Üs Lv.2'de açılır

            default:
                return 1;
        }
    }

    /// <summary>
    /// Bu binanın maksimum seviyesi (Ana Üs seviyesine bağlı)
    /// </summary>
    public int GetMaxLevel(int mainBaseLevel)
    {
        switch (buildingType)
        {
            case BuildingType.MainBase:
                return 10;  // Ana Üs max 10

            case BuildingType.ArcherTower:
                return Mathf.Min(mainBaseLevel, 8);

            case BuildingType.CannonTower:
                return Mathf.Min(mainBaseLevel - 1, 6);

            case BuildingType.GoldMine:
                return Mathf.Min(mainBaseLevel + 1, 8);

            case BuildingType.WallBuilder:
                return Mathf.Min(mainBaseLevel, 5);

            default:
                return mainBaseLevel;
        }
    }

    /// <summary>
    /// Ana üs seviyesine göre bu binadan kaç tane inşa edilebilir
    /// </summary>
    public static int GetMaxBuildCount(BuildingType type, int mainBaseLevel)
    {
        switch (type)
        {
            case BuildingType.MainBase:
                return 1;   // Sadece 1 ana üs

            case BuildingType.GoldMine:
                return Mathf.Min(1 + mainBaseLevel / 3, 4); // Lv1:1, Lv3:2, Lv6:3, Lv9:4

            case BuildingType.ArcherTower:
                return Mathf.Min(1 + (mainBaseLevel - 2) / 2, 3); // Lv2:1, Lv4:2, Lv6:3

            case BuildingType.CannonTower:
                return Mathf.Min(1 + (mainBaseLevel - 4) / 3, 2); // Lv4:1, Lv7:2

            case BuildingType.WallBuilder:
                return 1;   // Sadece 1 duvarcı

            default:
                return 1;
        }
    }

    /// <summary>
    /// Duvarcı seviyesine göre duvar HP'si hesapla
    /// </summary>
    public static float GetWallHealthFromBuilder(int wallBuilderLevel)
    {
        if (wallBuilderLevel <= 0) return 100f;
        return 100f + (wallBuilderLevel - 1) * 50f;
    }
}

/// <summary>
/// Düşman istatistikleri — seviye çarpanıyla ölçeklenir
/// </summary>
[Serializable]
public class EnemyStats
{
    public EnemyType enemyType;
    public float health;
    public float damage;
    public float moveSpeed;
    public float attackRange = 1.5f;
    public float attackSpeed = 1f;
    public int goldDrop;
    public int goldReward { get => goldDrop; set => goldDrop = value; }

    public static EnemyStats GetBaseStats(EnemyType type)
    {
        EnemyStats stats = new EnemyStats();
        stats.enemyType = type;

        switch (type)
        {
            case EnemyType.Wolf:
                stats.health = 30f;
                stats.damage = 5f;
                stats.moveSpeed = 2.2f;
                stats.attackRange = 1.2f;
                stats.attackSpeed = 1.2f;
                stats.goldDrop = 10;
                break;

            case EnemyType.Zombie:
                stats.health = 50f;
                stats.damage = 8f;
                stats.moveSpeed = 1.1f;
                stats.attackRange = 1.5f;
                stats.attackSpeed = 0.8f;
                stats.goldDrop = 15;
                break;

            case EnemyType.Orc:
                stats.health = 80f;
                stats.damage = 12f;
                stats.moveSpeed = 1.5f;
                stats.attackRange = 1.5f;
                stats.attackSpeed = 0.7f;
                stats.goldDrop = 25;
                break;

            case EnemyType.Troll:
                stats.health = 200f;
                stats.damage = 20f;
                stats.moveSpeed = 0.8f;
                stats.attackRange = 2.0f;
                stats.attackSpeed = 0.5f;
                stats.goldDrop = 100;
                break;
        }

        return stats;
    }

    /// <summary>
    /// Seviyeye göre statları ölçekle
    /// </summary>
    public void ScaleToLevel(int level)
    {
        float multiplier = 1f + (level - 1) * 0.085f;
        health *= multiplier;
        damage *= multiplier;
        // goldDrop da hafif artar
        goldDrop = Mathf.RoundToInt(goldDrop * (1f + (level - 1) * 0.08f));
    }
}

// ==================== LEVEL / DALGA SİSTEMİ ====================

[Serializable]
public class EnemySpawnInfo
{
    public EnemyType enemyType;
    public int count;
}

[Serializable]
public class WaveData
{
    public int waveNumber;
    public List<EnemySpawnInfo> enemies = new List<EnemySpawnInfo>();
    public float timeBetweenSpawns = 1.5f;
}

[Serializable]
public class LevelData
{
    public int levelNumber;
    public List<WaveData> waves = new List<WaveData>();

    /// <summary>
    /// Level numarasına göre düşman dalgaları oluştur
    /// GDD'ye uygun: Lv1-2: 1 dalga (7-8 düşman), Lv3-5: 2 dalga, artan zorluk
    /// </summary>
    public static LevelData GenerateLevel(int levelNum)
    {
        LevelData level = new LevelData();
        level.levelNumber = levelNum;

        // GDD: Level 1 = 1 dalga, sonra yavaşça artar, max 5 dalga
        int waveCount;
        if (levelNum <= 2)
            waveCount = 1;
        else if (levelNum <= 5)
            waveCount = 2;
        else if (levelNum <= 10)
            waveCount = 3;
        else if (levelNum <= 15)
            waveCount = 4;
        else
            waveCount = 5;

        for (int w = 0; w < waveCount; w++)
        {
            WaveData wave = new WaveData();
            wave.waveNumber = w + 1;
            wave.timeBetweenSpawns = Mathf.Max(0.5f, 1.5f - levelNum * 0.03f);

            // Her dalgada temel düşman sayısı: 7-8 civarı + level bonusu
            int baseEnemyCount = 7 + Mathf.FloorToInt(levelNum * 0.5f);

            // Kurtlar — her seviyede var (temel düşman)
            int wolfCount = Mathf.Max(3, baseEnemyCount - (levelNum >= 3 ? 2 : 0) - (levelNum >= 6 ? 2 : 0));
            wave.enemies.Add(new EnemySpawnInfo
            {
                enemyType = EnemyType.Wolf,
                count = wolfCount
            });

            // Level 3'ten sonra zombiler eklenir
            if (levelNum >= 3)
            {
                int zombieCount = 2 + (levelNum - 3) / 2;
                wave.enemies.Add(new EnemySpawnInfo
                {
                    enemyType = EnemyType.Zombie,
                    count = zombieCount
                });
            }

            // Level 6'dan sonra orklar eklenir
            if (levelNum >= 6)
            {
                int orcCount = 1 + (levelNum - 6) / 3;
                wave.enemies.Add(new EnemySpawnInfo
                {
                    enemyType = EnemyType.Orc,
                    count = orcCount
                });
            }

            // Son dalgada mini-boss (Level 2'den itibaren)
            if (w == waveCount - 1 && levelNum >= 2)
            {
                EnemyType bossType;
                int bossCount;

                if (levelNum < 6)
                {
                    bossType = EnemyType.Orc;   // Erken levellerde Ork mini-boss
                    bossCount = 1;
                }
                else
                {
                    bossType = EnemyType.Troll;  // Geç levellerde Trol boss
                    bossCount = 1 + (levelNum - 6) / 5;
                }

                wave.enemies.Add(new EnemySpawnInfo
                {
                    enemyType = bossType,
                    count = bossCount
                });
            }

            level.waves.Add(wave);
        }

        return level;
    }
}
