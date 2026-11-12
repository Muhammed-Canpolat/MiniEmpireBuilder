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
    Monastery       // Manastır — dost birimleri iyileştirir
}

/// <summary>
/// Düşman türleri
/// </summary>
public enum EnemyType
{
    Wolf,       // Kurt — hızlı, düşük can
    Zombie,     // Zombi — yavaş, orta can
    Orc,        // Ork — güçlü, yüksek can
    Troll,      // Trol — boss, çok yüksek can
    // Yeni türler hep sona eklenir: RegionData asset'leri enum'u sayı olarak saklar.
    ShadowWolf, // Gölge Kurt — sisin içinde görünmez, kahramana atılır
    ForestWitch, // Orman Cadısı — Bölge 2 boss'u
    DragonWarrior, // Ejder Savaşçısı — Bölge 6 boss'u
    CastleGuardian, // Kale Muhafızı — Bölge 1 boss'u (Kalkan Duvarı, Hücum)
    MineWarden,     // Maden Gardiyanı — Bölge 3 boss'u (Kaya Yağmuru)
    FrostLord,      // Buz Hükümdarı — Bölge 4 boss'u (Buz Nefesi)
    DarkKing,       // Karanlık Kral — Bölge 5 boss'u (Gölge Çağrısı, Can Emme)
    // Martı Adası (Docs/design/island_base.md §4): Kızıl Korsanlar.
    Deckhand,       // Tayfa — hızlı, sürü
    Raider,         // Akıncı — kaleye yürür
    ShoreArcher,    // Kıyı Okçusu — durup atış yerini / kaleyi vurur
    // Martı Adası v2 Faz C (island_base_v2.md §7.3): Kademe I türleri; Kızıl insanlar + DungeonMonsters2D.
    ShieldLancer,   // Kalkanlı Mızrakçı — Kızıl Lancer, önden gelen tek atış ×0,3
    Rat,            // Fare — DM2D, hızlı, yarı tırmanır, üretim binasına gider
    Bat,            // Yarasa — DM2D, uçan, teknesiz gelir, Okçu Kaptan'a dalar
    WoodGolem,      // Ahşap Golem — DM2D, tank, ok/kule ×0,6
    ButcherCaptain  // Kasap Kaptan — DM2D, 10. Akın boss'u
}

public static class EnemyTypeInfo
{
    public static bool IsBoss(EnemyType type) =>
        type == EnemyType.Troll || type == EnemyType.ForestWitch || type == EnemyType.DragonWarrior || IsRegionBoss(type);

    /// <summary>Trol gövdesi + renk + kendi yetenekleri (RegionBoss) olan bölge bossları.</summary>
    public static bool IsRegionBoss(EnemyType type) =>
        type == EnemyType.CastleGuardian || type == EnemyType.MineWarden
        || type == EnemyType.FrostLord || type == EnemyType.DarkKing;

    public static string DisplayName(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Wolf: return L.T("Kurt", "Wolf");
            case EnemyType.Zombie: return L.T("Zombi", "Zombie");
            case EnemyType.Orc: return L.T("Ork", "Orc");
            case EnemyType.Troll: return L.T("Muhafız", "Guardian");
            case EnemyType.ShadowWolf: return L.T("Gölge Kurt", "Shadow Wolf");
            case EnemyType.ForestWitch: return L.T("Orman Cadısı", "Forest Witch");
            case EnemyType.DragonWarrior: return L.T("Ejder Savaşçısı", "Dragon Warrior");
            case EnemyType.CastleGuardian: return L.T("Kale Muhafızı", "Castle Guardian");
            case EnemyType.MineWarden: return L.T("Maden Gardiyanı", "Mine Warden");
            case EnemyType.FrostLord: return L.T("Buz Hükümdarı", "Frost Lord");
            case EnemyType.DarkKing: return L.T("Karanlık Kral", "Dark King");
            case EnemyType.Deckhand: return L.T("Tayfa", "Deckhand");
            case EnemyType.Raider: return L.T("Akıncı", "Raider");
            case EnemyType.ShoreArcher: return L.T("Kıyı Okçusu", "Shore Archer");
            case EnemyType.ShieldLancer: return L.T("Kalkanlı Mızrakçı", "Shield Lancer");
            case EnemyType.Rat: return L.T("Fare", "Rat");
            case EnemyType.Bat: return L.T("Yarasa", "Bat");
            case EnemyType.WoodGolem: return L.T("Ahşap Golem", "Wood Golem");
            case EnemyType.ButcherCaptain: return L.T("Kasap Kaptan", "Butcher Captain");
            default: return type.ToString();
        }
    }
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
    /// <summary>Kalkan: düşmandan gelen hasarı bu oranda azaltır (0.10 = %10).</summary>
    public float shield = 0.10f;

    /// <summary>Kalkan gücü: 1. seviyede %10, her seviyede +%2 (15. seviyede %38).</summary>
    public static float ShieldForLevel(int level) =>
        0.10f + 0.02f * (Mathf.Clamp(level, 1, Progression.MaxLevel) - 1);

    /// <summary>
    /// Silah tipine ve seviyeye göre istatistikleri hesapla
    /// </summary>
    public void ApplyLevelStats()
    {
        float levelMultiplier = 1f + (level - 1) * 0.15f;
        shield = ShieldForLevel(level);

        switch (weaponType)
        {
            case WeaponType.Axe:
                // Tek kahraman (Warrior): taban can 130'dan 200'e çıktı (çok çabuk ölüyordu).
                damage = 15f * levelMultiplier;
                attackRange = 1.5f;
                attackSpeed = 0.8f;
                maxHealth = 200f * levelMultiplier;
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
    // Index is Ana Üs level. Kept on the building data so both menus use one limit.
    public int[] maxCountByBaseLevel;
    // Presentation only: level statistics and save format remain independent.
    public BuildingLevelVisual[] levels = new BuildingLevelVisual[5];

    public BuildingLevelVisual VisualForLevel(int value)
    {
        if (levels == null || levels.Length != 5 || levels[0] == null)
            levels = BaseVisualCatalog.Instance?.GetLevels(buildingType)
                ?? new BuildingLevelVisual[5];
        int index = BaseVisualCatalog.ColorIndex(value);
        return levels[index];
    }

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
                maxHealth = 300f + (level - 1) * 100f; // balance_v1: ilk bölgeler affedici olsun
                currentHealth = maxHealth;
                break;

            case BuildingType.ArcherTower:
                maxHealth = 80f + (level - 1) * 30f;
                currentHealth = maxHealth;
                damage = Progression.TowerDamage(BuildingType.ArcherTower, level);
                unitCount = Progression.TowerUnits(BuildingType.ArcherTower, level);
                attackRange = 6f;
                attackSpeed = 1.25f;
                break;

            case BuildingType.CannonTower:
                maxHealth = 120f + (level - 1) * 40f;
                currentHealth = maxHealth;
                damage = Progression.TowerDamage(BuildingType.CannonTower, level);
                unitCount = Progression.TowerUnits(BuildingType.CannonTower, level);
                attackRange = 5f;
                attackSpeed = 0.5f;                    // Yavaş ama güçlü
                break;

            case BuildingType.GoldMine:
                maxHealth = 50f + (level - 1) * 10f;
                currentHealth = maxHealth;
                goldPerSecond = 1f + (level - 1) * 0.5f;
                break;

            case BuildingType.Monastery:
                maxHealth = 90f + (level - 1) * 25f;
                currentHealth = maxHealth;
                break;
        }
    }

    /// <summary>Bir sonraki seviye için gereken altın (Progression tablosu).</summary>
    public int GetUpgradeCost() => Progression.UpgradeCost(buildingType, level + 1);

    /// <summary>Bu türün ilk binası için gereken Ana Üs seviyesi.</summary>
    public int GetRequiredMainBaseLevel() => Progression.FirstUnlockLevel(buildingType);

    /// <summary>Bu binanın çıkabileceği en yüksek seviye (Ana Üs seviyesine bağlı).</summary>
    public int GetMaxLevel(int mainBaseLevel)
    {
        switch (buildingType)
        {
            case BuildingType.MainBase: return Progression.MaxLevel;
            case BuildingType.GoldMine: return Mathf.Min(mainBaseLevel + 1, Progression.MaxLevel);
            default: return Mathf.Clamp(mainBaseLevel, 1, Progression.MaxLevel);
        }
    }

    /// <summary>Ana Üs seviyesine göre bu türden kaç bina kurulabilir.</summary>
    public static int GetMaxBuildCount(BuildingType type, int mainBaseLevel)
    {
        switch (type)
        {
            case BuildingType.MainBase:
            case BuildingType.GoldMine:
                return 1;
            default:
                int first = Progression.FirstUnlockLevel(type);
                int second = Progression.SecondUnlockLevel(type);
                return mainBaseLevel < first ? 0 : mainBaseLevel < second ? 1 : 2;
        }
    }

    public void SetBuildLimits()
    {
        maxCountByBaseLevel = new int[Progression.MaxLevel + 1];
        for (int level = 0; level < maxCountByBaseLevel.Length; level++)
            maxCountByBaseLevel[level] = GetMaxBuildCount(buildingType, level);
    }

    public int MaxCountAtBaseLevel(int level)
    {
        if (maxCountByBaseLevel == null) SetBuildLimits();
        return maxCountByBaseLevel[Mathf.Clamp(level, 0, maxCountByBaseLevel.Length - 1)];
    }

}

/// <summary>
/// Ana Üs ve yapı ilerlemesinin tek kaynağı: seviye tavanı, açılma seviyeleri,
/// yükseltme bedeli ve süresi. Denge ayarı yalnız bu tablolardan yapılır.
/// </summary>
public static class Progression
{
    public const int MaxLevel = 15;

    // ---- Savaş dengesi (Docs/design/balance_v1.md) ----
    /// <summary>
    /// Savaş başı altını (round2 D): varsayılan yok; Ana Üs seviyesiyle açılır (7: 25, 10: 50, 13: 75).
    /// Hiçbir zaman satılmaz; kart/Kraliyet/elmas etkilemez. Zafer ödülünden düşülür.
    /// Ana Üs seçildi: yükseltmeyi ödüllendirir, üç küçük adım (tek geç sıçrama yerine), asla tam bir kule ödemez.
    /// </summary>
    public static int BattleStartGold(int mainBaseLevel) =>
        mainBaseLevel >= 13 ? 75 : mainBaseLevel >= 10 ? 50 : mainBaseLevel >= 7 ? 25 : 0;
    /// <summary>İlk dalganın en fazla düşman sayısı (başlangıç altını yokken ilk dalga kahramanla tutulabilsin).</summary>
    public static int Wave1Cap(int region) => 6 + Mathf.Max(1, region);

    // ---- Kuleler (round2 A): kahraman kadar doğrusal büyür, birim sayısı yavaş artar ----
    /// <summary>Birim başına kule hasarı. Okçu: 8 + 2 × (sev − 1) (kullanıcı kararı, round 3: kahramansız zafer biraz daha
    /// mümkün olsun). Topçu: 18 taban, seviye başına +%15 (kahramanla aynı eğim).</summary>
    public static float TowerDamage(BuildingType type, int level) =>
        type == BuildingType.CannonTower
            ? 18f * (1f + 0.15f * (Mathf.Max(1, level) - 1))
            : 8f + 2f * (Mathf.Max(1, level) - 1);
    /// <summary>Manastır iyileştirmesi (3 sn'de bir, azami canın oranı): 1. sev %3, her seviye +%1 (kullanıcı kararı).</summary>
    public static float MonasteryHealFraction(int level) => 0.03f + 0.01f * (Mathf.Max(1, level) - 1);
    /// <summary>Kule birim sayısı: Okçu 1 + sev/5, Topçu 1 + sev/6.</summary>
    public static int TowerUnits(BuildingType type, int level) =>
        1 + Mathf.Max(1, level) / (type == BuildingType.CannonTower ? 6 : 5);
    /// <summary>Savaş içi yapı bedeli bölgeyle artar: taban × (1 + 0,2 × (bölge − 1)).</summary>
    public static float BattleBuildCostScale(int region) => 1f + 0.2f * (Mathf.Max(1, region) - 1);
    /// <summary>Bir dalganın kaç taraftan geldiği: Bölge 1–2 tek taraf, sonrası iki taraf.</summary>
    public static int WaveSpawnSides(int region) => region <= 2 ? 1 : 2;
    /// <summary>Boss aşamasında her dalgaya eklenen düşman (bot ölçümü: boss aşamaları fazla kolaydı).</summary>
    public static int BossStageExtraEnemies(int region) => region <= 1 ? 1 : 3; // Bölge 1 öğretici: hafif
    /// <summary>Dalga içinde iki düşman arası saniye.</summary>
    public static float SpawnInterval(int region) => Mathf.Max(0.9f, 1.25f - 0.07f * (Mathf.Max(1, region) - 1));
    public const int LevelsPerColorTier = 3;

    /// <summary>Ana Üs'te kalan doğa nesnesini kaldırma bedeli (altın): çalı 5, kütük 8, kaya 10, ağaç 15.</summary>
    public static int NatureRemoveCost(NatureKind kind) =>
        kind == NatureKind.Bush ? 5 : kind == NatureKind.Stump ? 8 : kind == NatureKind.Rock ? 10 : 15;

    // ---- Bölge kimliği (round2 E): Bölge 4 Don, 5 Lanetli Türbeler, 6 Yanardağ ----
    public static class RegionMechanics
    {
        // Bölge 4: fırtına kuleleri dondurur; kahraman yanında durarak çözer.
        public const float StormTowerCooldown = 1.25f;          // eskiden 1.4
        public const float FreezeSeconds = 5f;
        public const float ThawRadius = 1.6f, ThawSeconds = 1f;
        public static int FreezeCount(int stage) => stage >= 3 ? 2 : 1;

        // Bölge 5: Lanetli Türbeler (yalnız kahraman hasar verir).
        public static int ShrineCount(int stage) => stage <= 1 ? 1 : 2;
        public const float ShrineBaseHp = 120f;                  // x düşman can çarpanı
        public const float ShrineInterval = 18f, ShrineWarnSeconds = 2f, ShrineDarkSeconds = 6f;
        public const float ShrineFirstDelay = 10f;
        public const float ShrineRadius = 4.5f, ShrineRangeMultiplier = 0.6f;
        public const int ShrineGold = 30;

        // Bölge 6: Yanardağ patlamaları.
        public const float EruptionInterval = 24f, EruptionFirstDelay = 14f, EruptionTelegraphSeconds = 1.2f;
        public const int EruptionMaxTowerBombs = 3;              // + 1 bomba kahramanın yakınına
        public const float LavaRadius = 1.1f;
        public const float LavaImpactFraction = 0.08f;           // yapı/kahraman/yer düşmanı azami canı
        public const float BurnSeconds = 5f, BurnFractionPerSecond = 0.04f, BurnTick = 0.5f;
        public const float HeroBombOffset = 1.5f;
    }

    // ---- Martı Adası (Docs/design/island_base.md §8): ada sayıları yalnız burada ----
    public static class Island
    {
        public const int UnlockMainBaseLevel = 5;
        /// <summary>Oto-ateş: Kıyı Kalesi seviyesi bu değere ulaşınca açılır (DevSettings açıkken test için hep açık).</summary>
        public const int AutoFireUnlockKeepLevel = 5;
        public const float AutoFireRateMult = 0.75f;          // elle en hızlı atışın 0,75'i
        /// <summary>MVP: ada yükseltmesi henüz yok; Kale ve Okçu seviyesi Ana Üs seviyesini izler (Faz 2'de kalkar).</summary>
        public const bool KeepFollowsMainBase = true;

        // Kale / Okçu
        public static float CastleHp(int k) => 300f + 80f * (Mathf.Max(1, k) - 1);
        public static float ArcherDamage(int l) => 12f * (1f + 0.15f * (Mathf.Max(1, l) - 1));
        public static float ArcherHp(int l) => 150f * (1f + 0.15f * (Mathf.Max(1, l) - 1));
        public const float FireCooldown = 0.5f;
        public const int TapBuffer = 1;
        public const float CritChance = 0.10f, CritMult = 2f;
        public const float ChargeStart = 0.35f, ChargeFull = 1.2f, ChargedMult = 2.5f, ChargedHullMult = 1.5f;
        public const int ChargedPierce = 3;
        public const float ArcherRange = 7f, HighGroundRangeBonus = 1.5f, ArrowSpeed = 14f;
        public const float ArcherRunSpeed = 9f, MinPostSwitch = 0.6f, ArcherRespawn = 8f; // v2: 22x36 harita, koşu 9 u/sn
        public const float CastleDangerRadius = 3f;

        // Ada görünümü ve harita (island_base_v2 §3, §4, §10 Faz A): sayılar yalnız burada
        public const int MapWidth = 22, MapHeight = 36;
        public const float CameraDefaultZoom = 0.70f, CameraMinZoom = 0.45f, BattleFrameWidth = 22f, BattleCameraSeconds = 0.8f;
        /// <summary>Tiny Swords 1:1 (64 px/birim): kale 5, ev 2 hücre; birimler ölçeklenmez.</summary>
        public const float UnitScale = 1f;
        public const float EdgeCloudBattleAlpha = 0.4f, EdgeCloudCount = 7f;
        public const float PawnRunSpeed = 2.4f, TreeRegrowSeconds = 180f, ChopsPerTree = 3f, TreeStumpEveryVisit = 3f;
        /// <summary>Kenney gemileri 66x113 / 20x38 px (64 PPU); son boy: Sandal 1,48 · Akıncı 2,47 · Mavna 3,53 u.</summary>
        public const float RowboatScale = 2.5f, LongboatScale = 1.4f, BargeScale = 2.0f;
        public static readonly float[] BoatDamageAt = { 0.66f, 0.33f };

        // Tekne / iniş
        public const float WetSeconds = 1.5f, WetDamageTaken = 1.25f, DisembarkInterval = 0.4f;
        public const float BoatStagger = 2.5f, WaveBreak = 8f, BoatSinkSeconds = 0.8f;
        public enum BoatKind { Rowboat, Longboat, Barge }
        public static float BoatBaseHp(BoatKind k) => k == BoatKind.Barge ? 300f : k == BoatKind.Longboat ? 150f : 60f;
        public static int BoatCrew(BoatKind k) => k == BoatKind.Barge ? 6 : k == BoatKind.Longboat ? 4 : 2;
        public static float BoatSpeed(BoatKind k) => k == BoatKind.Barge ? 0.6f : k == BoatKind.Longboat ? 0.8f : 1.0f;
        /// <summary>Kalkanlı Mavna: tek tık okları bu çarpanla işler (dolu atış kalkanı yok sayar).</summary>
        public const float BargeTapTaken = 0.6f;
        public const int LongboatFromAkin = 5, BargeFromAkin = 15;
        public const float TutorialBoatSpeedMult = 0.6f;
        /// <summary>Boss Akın'ında son dalgadaki Kızıl Kaptan: etkin can çarpanı ve boyut.</summary>
        public const float BossHpMult = 5f, BossScale = 1.3f;   // Faz C: Kasap Kaptan x1,3 (BossHpMult artık kullanılmaz: boss canı EnemyStats tablosundan)
        /// <summary>Düşman can çarpanı (EnemyStats.ScaleToLevel ile aynı eğri).</summary>
        public static float EnemyHealthMult(int level) => 1f + (Mathf.Max(1, level) - 1) * 0.085f;

        // Akın merdiveni (§5)
        public static int AkinBattleLevel(int n) => 1 + Mathf.RoundToInt(0.8f * Mathf.Max(1, n));
        public static int AkinWaves(int n) => n <= 2 ? 1 : n <= 9 ? 2 : n <= 24 ? 3 : 4;
        public static int AkinBoats(int n) => Mathf.Min(1 + Mathf.CeilToInt(Mathf.Max(1, n) / 3f), 7);
        /// <summary>Kumsal açılışı (A, B, C, D sırasıyla): A 1, B 4, C 9, D 15.</summary>
        public static readonly int[] AkinBeachUnlock = { 1, 4, 9, 15 };
        public static int AkinRequiredCastle(int n) => Mathf.CeilToInt(Mathf.Max(1, n) / 3f);
        public const int BossEvery = 10;
        public static bool IsBossAkin(int n) => n > 0 && n % BossEvery == 0;
        /// <summary>v2 §14 karar 5: Et ödülü DÜŞÜK (5 + 2N), yalnız ilk zaferde; tekrarda 0. Akın Et çeşmesi olmamalı.</summary>
        public static int AkinMeat(int n) => 5 + 2 * n;
        public const int ReplayRewardsPerDay = 5;
        public static int AkinWood(int n) => 25 + 8 * n;
        public static int AkinGold(int n) => 20 + 5 * n;
        public const int GemsEvery = 5, GemsEveryAmount = 3, BossGems = 5;
        public static int AkinGems(int n) => (n % GemsEvery == 0 ? GemsEveryAmount : 0) + (IsBossAkin(n) ? BossGems : 0);
        public const float ReplayRewardRate = 0.3f;   // Odun/altın tekrarda %30; Et 0

        // ---- Faz C: düşman hedefleme, tırmanma, uçanlar, zırh (island_base_v2 §7.3, §7.5, §8) ----
        public const float LaneAggroRadius = 1.5f, RangedAggro = 5f, RatClimbReach = 9f, ClimbSeconds = 1.2f;
        public const float ShieldFrontTaken = 0.3f, ShieldFrontArc = 60f, GolemArrowTaken = 0.6f;
        public const int FlyerSwarmMin = 3, FlyerSwarmMax = 5, FlyerFromAkin = 5;
        public const float FlyerTelegraph = 2f;
        public const float ButcherRoarEvery = 15f, ButcherRoarSpeed = 1.3f, ButcherRoarSeconds = 4f;
        public const int GolemFromAkin = 8, ShieldFromAkin = 6, RatFromAkin = 3, ArcherFromAkin = 2;
        public const float TowerRange = 6f;
        /// <summary>Ada binasının savaş canı (§5): Kale CastleHp; Ev/Oduncu/Ağıl 80+20(L-1); Kule 80+30(L-1); Okçuluk 100+25(L-1).</summary>
        public static float StructureHp(string kind, int level)
        {
            int l = Mathf.Max(1, level) - 1;
            switch (kind)
            {
                case "Tower": return 80f + 30f * l;
                case "Archery": return 100f + 25f * l;
                default: return 80f + 20f * l;
            }
        }
        // Onarım (§7.7): ücretsiz, yalnız bekleme; onarım sırasında üretim yavaş (üretim Faz B'de; burada yalnız çarpan).
        public const float RepairBase = 20f, RepairPerLost = 100f, RepairDestroyed = 180f, RepairMonasteryMult = 0.5f, RepairProductionMult = 0.5f;
        public static float RepairSeconds(float lostFraction, bool destroyed, bool monastery)
        {
            float seconds = destroyed ? RepairDestroyed : RepairBase + RepairPerLost * Mathf.Clamp01(lostFraction);
            return seconds * (monastery ? RepairMonasteryMult : 1f);
        }
        /// <summary>
        /// Hazır köyün sabit bina seviyesi (Faz B gerçek seviyeleri bağlayana kadar): adacık kuleleri yavaş yükselir
        /// (1 + KK/2: iki adacık kulesi tekne yollarını tek başına kapatıp inişi ortadan kaldırmasın), Okçuluk Kıyı Kalesi'ni izler, diğerleri 1.
        /// </summary>
        public static int PrebuiltLevel(string kind, int keepLevel) =>
            kind == "Tower" ? 1 + Mathf.Max(1, keepLevel) / 2 : kind == "Archery" ? Mathf.Max(1, keepLevel) : 1;
        // ---- Faz B: inşa ve ekonomi (island_base_v2 §5, §6, §8): bina bedelleri, süreler, üretim, tavanlar, bölge keşfi ----
        /// <summary>Yükseltme bedeli tabanı W (index = HEDEF seviye 2..15; §6).</summary>
        public static readonly int[] UpgradeBaseCost = { 0, 0, 80, 150, 250, 380, 550, 760, 1000, 1300, 1650, 2050, 2500, 3000, 3600, 4300 };
        /// <summary>Yükseltme çarpanı (Odun, Et) bina türüne göre (§5).</summary>
        public static (float wood, float meat) UpgradeFactor(IslandLayout.BuildingKind kind)
        {
            switch (kind)
            {
                case IslandLayout.BuildingKind.House: return (0.25f, 0f);
                case IslandLayout.BuildingKind.Lumber: return (0.15f, 0.2f);
                case IslandLayout.BuildingKind.Pasture: return (0.45f, 0f);
                case IslandLayout.BuildingKind.Tower: return (0.5f, 0.1f);
                default: return (1f, 0.3f);   // Kale (KK Ana Üs'ü izlediği sürece kullanılmaz)
            }
        }
        private static int Round10(float value) => Mathf.Max(0, Mathf.RoundToInt(value / 10f) * 10);
        public static int UpgradeWood(IslandLayout.BuildingKind kind, int targetLevel)
        {
            var factor = UpgradeFactor(kind);
            return factor.wood <= 0f ? 0 : Mathf.Max(10, Round10(UpgradeBaseCost[Mathf.Clamp(targetLevel, 2, MaxLevel)] * factor.wood));
        }
        public static int UpgradeMeat(IslandLayout.BuildingKind kind, int targetLevel)
        {
            var factor = UpgradeFactor(kind);
            return factor.meat <= 0f ? 0 : Mathf.Max(10, Round10(UpgradeBaseCost[Mathf.Clamp(targetLevel, 2, MaxLevel)] * factor.meat));
        }
        /// <summary>Ada Ustası süresi: Ana Üs süre tablosu (TimingConfig) × <see cref="TimeFactor"/>; yeni bina (sv1) <see cref="PlaceSeconds"/>.</summary>
        public const float TimeFactor = 0.5f;
        public const int PlaceSeconds = 5;
        public static int BuildSeconds(int targetLevel) => targetLevel <= 1 ? PlaceSeconds
            : Mathf.Max(1, Mathf.RoundToInt(UpgradeSecondsTo(targetLevel) * TimeFactor));
        /// <summary>Yeni bina kurulum bedeli (Odun, Et): Ev 40/0, Kule 60/10, 2. Oduncu 300/60, 2. Ağıl 600/0. Okçuluk yerleştirilemez.</summary>
        public static (int wood, int meat) PlaceCost(IslandLayout.BuildingKind kind)
        {
            switch (kind)
            {
                case IslandLayout.BuildingKind.House: return (40, 0);
                case IslandLayout.BuildingKind.Tower: return (60, 10);
                case IslandLayout.BuildingKind.Lumber: return (300, 60);
                case IslandLayout.BuildingKind.Pasture: return (600, 0);
                default: return (0, 0);
            }
        }
        /// <summary>Kıyı Kalesi seviyesine göre bina adedi tavanı (§8: Ev {1:1,3:2,6:3,9:4}, Kule {1:1,2:2,4:3,6:4,9:5}, Oduncu 7'de 2, Ağıl 10'da 2).</summary>
        public static int MaxCount(IslandLayout.BuildingKind kind, int keep)
        {
            switch (kind)
            {
                case IslandLayout.BuildingKind.House: return keep >= 9 ? 4 : keep >= 6 ? 3 : keep >= 3 ? 2 : 1;
                case IslandLayout.BuildingKind.Tower: return keep >= 9 ? 5 : keep >= 6 ? 4 : keep >= 4 ? 3 : keep >= 2 ? 2 : 1;
                case IslandLayout.BuildingKind.Lumber: return keep >= SecondLumberKeep ? 2 : 1;
                case IslandLayout.BuildingKind.Pasture: return keep >= SecondPastureKeep ? 2 : 1;
                case IslandLayout.BuildingKind.Archery: return 1;
                default: return 1;
            }
        }
        public const int SecondLumberKeep = 7, SecondPastureKeep = 10;
        public static int HousePawns(int level) => 2 + (level >= 5 ? 1 : 0) + (level >= 10 ? 1 : 0);
        public static int LumberSlots(int level) => 2 + (level >= 5 ? 1 : 0) + (level >= 10 ? 1 : 0);
        public static int PastureSlots(int level) => 1 + (level >= 5 ? 1 : 0) + (level >= 10 ? 1 : 0);
        public const int CastlePawns = 2;
        public static int Slots(IslandLayout.BuildingKind kind, int level) =>
            kind == IslandLayout.BuildingKind.Lumber ? LumberSlots(level) : kind == IslandLayout.BuildingKind.Pasture ? PastureSlots(level) : 0;
        /// <summary>Tam kadro saatlik üretim: Odun 30 + 15(L−1); Et 15 + 8(L−1).</summary>
        public static float WoodPerHour(int level) => 30f + 15f * (Mathf.Max(1, level) - 1);
        public static float MeatPerHour(int level) => 15f + 8f * (Mathf.Max(1, level) - 1);
        public const float ProducerStorageHours = 8f, SecondPastureMult = 1f, MeadowBonus = 0.15f;
        /// <summary>Doğa kısıtı (§5): ağaç yarıçapı/yuva başına ağaç, çayır hücresi sayısı/yarıçapı, kısa çayır çarpanı.</summary>
        public const int TreesPerSlot = 2, MeadowCells = 6;
        public const float TreeRadius = 4f, MeadowRadius = 3f, MeadowShortMult = 0.5f;
        /// <summary>Cüzdan tavanı: Odun 600 + 400(k−1), Et 300 + 200(k−1) (kale taşır).</summary>
        public static int WalletWood(int keep) => 600 + 400 * (Mathf.Max(1, keep) - 1);
        public static int WalletMeat(int keep) => 300 + 200 * (Mathf.Max(1, keep) - 1);
        /// <summary>Okçu yükseltmesi (Et, anında; L → L+1): 35 × 1,28^(L−1), 5'e yuvarlanır.</summary>
        public static int ArcherUpgradeMeat(int level) => Mathf.RoundToInt(35f * Mathf.Pow(1.28f, Mathf.Max(1, level) - 1) / 5f) * 5;
        /// <summary>Bina saldırıya/üretime hazır tamamlanan en yüksek seviye = Kıyı Kalesi.</summary>
        public static int MaxBuildingLevel(int keep) => Mathf.Clamp(keep, 1, MaxLevel);

        // Bölge keşfi (§4, §8): Z1–Z2 Faz B'de açılır (Z3–Z5 Faz E). Bedel Odun/Et, süre sn (Usta).
        public const int LastRevealableZone = 2;
        public static readonly int[] ZoneKeep = { 1, 2, 3, 4, 5, 6 };
        public static readonly int[] ZoneWood = { 0, 150, 300, 500, 800, 1500 };
        public static readonly int[] ZoneMeat = { 0, 0, 30, 50, 80, 150 };
        public static readonly int[] ZoneSeconds = { 0, 30, 180, 300, 900, 3600 };
        public static bool ZoneRevealable(int zone) => zone >= 1 && zone <= LastRevealableZone;
        public const float ZoneDispersalSeconds = 1.5f;

        /// <summary>Ada Ana Üs ilerlemesine bağlı: en yüksek Akın = 3 × Kıyı Kalesi.</summary>
        public static int MaxAkin(int keepLevel) => 3 * Mathf.Max(1, keepLevel);

        // Atış yerleri (§2): açılış Kıyı Kalesi seviyesi (P1, P2, P3, P4, P5)
        public static readonly int[] PostUnlock = { 1, 4, 1, 2, 5 };
        public static int BeachesOpen(int n)
        {
            int open = 0;
            for (int i = 0; i < AkinBeachUnlock.Length; i++) if (n >= AkinBeachUnlock[i]) open++;
            return Mathf.Max(1, open);
        }
        /// <summary>Etkin Kıyı Kalesi seviyesi: kayıtlı seviye ile (MVP'de) Ana Üs seviyesinin büyüğü.</summary>
        public static int EffectiveKeepLevel(int savedKeep, int mainBaseLevel) =>
            Mathf.Clamp(KeepFollowsMainBase ? Mathf.Max(savedKeep, mainBaseLevel) : savedKeep, 1, MaxLevel);
        public static int EffectiveArcherLevel(int savedArcher, int keepLevel) =>
            Mathf.Clamp(KeepFollowsMainBase ? keepLevel : Mathf.Min(savedArcher, keepLevel), 1, MaxLevel);
        /// <summary>Başlatılabilir mi: sıralı (en çok en yüksek + 1) ve Kıyı Kalesi kapısı.</summary>
        public static bool CanStartAkin(int n, int keepLevel, int highestAkin) =>
            n >= 1 && n <= highestAkin + 1 && keepLevel >= AkinRequiredCastle(n);
    }

    // ---- Karşı düşmanlar ve Boss Kalkanı (round2 C) ----
    public static class Counters
    {
        // Zırhlı (Ork gövdesi)
        public const float ArmoredHp = 1.4f, ArmoredDamage = 1f, ArmoredSpeed = 0.8f, ArmoredGold = 1.5f, ArmoredScale = 1.15f;
        public const float ArmoredArcherTaken = 0.35f, ArmoredCannonTaken = 0.6f;
        public static readonly Color ArmoredTint = new Color(0.62f, 0.68f, 0.80f);
        // Uçan (Gölge Kurt gövdesi)
        public const float FlyingHp = 1.2f, FlyingDamage = 1.3f, FlyingSpeed = 0.85f, FlyingGold = 1.3f;
        public const float FlyingYOffset = 0.45f, FlyingBobAmplitude = 0.08f, FlyingBobHz = 1.6f, FlyingShadowAlpha = 0.35f, FlyingTintAlpha = 0.85f;
        public static readonly Color FlyingTint = new Color(0.62f, 0.5f, 0.9f);
        // Bombacı (Kurt gövdesi)
        public const float SaboteurHp = 0.4f, SaboteurSpeed = 1.3f, SaboteurGold = 0.5f, SaboteurScale = 0.85f;
        public const float SaboteurTowerTaken = 0.7f, SaboteurFuseSeconds = 1.0f; // 0,6 sn: sıradan oyuncu tepki veremiyordu (bot ölçümü)
        public const float SaboteurTowerDamageFraction = 0.30f, SaboteurBaseDamageFraction = 0.05f;
        public const float SaboteurBurstInterval = 0.35f;
        public static readonly Color SaboteurTint = new Color(1f, 0.6f, 0.25f);

        // Boss Kalkanı
        public const float WardExposedSeconds = 6f, WardExposedDamageMult = 1.5f, WardStaggerSeconds = 1.5f;
        /// <summary>Kalkan havuzu / boss canı: Bölge 1 %15, 2 %20, 3+ %25.</summary>
        public static float WardFraction(int region) => region <= 1 ? 0.15f : region == 2 ? 0.20f : 0.25f;
        /// <summary>Kalkan ayaktayken kule hasarı çarpanı: 0,5 / 0,35 / 0,15.</summary>
        public static float WardTowerMult(int region) => region <= 1 ? 0.5f : region == 2 ? 0.35f : 0.25f; // 0,15: sıradan oyuncu boss'u hiç geçemiyordu
        /// <summary>Boss türünün bölgesi (kalkanı olmayan tür için 0).</summary>
        public static int WardRegion(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.CastleGuardian: return 1;
                case EnemyType.ForestWitch: return 2;
                case EnemyType.MineWarden: return 3;
                case EnemyType.FrostLord: return 4;
                case EnemyType.DarkKing: return 5;
                case EnemyType.DragonWarrior: return 6;
                default: return 0;
            }
        }
    }

    /// <summary>
    /// Bir dalganın karşı düşman karışımı (round2 C tablosu). stage 1–5, wave 0 tabanlı (0 = ilk dalga: hiç yok).
    /// Varyantlar temel düşmanların yerini alır; toplam en fazla baseCount − 2.
    /// </summary>
    public static (int armored, int flying, int saboteur) CounterMix(int region, int stage, int wave, int baseCount = int.MaxValue)
    {
        int arm = 0, fly = 0, sab = 0;
        if (wave >= 1 && wave <= 2 && stage >= 2 && stage <= 5 && region >= 2 && region <= 6)
        {
            bool s5 = stage == 5;
            if (wave == 1)
            {
                switch (region)
                {
                    case 2: sab = s5 ? 2 : stage >= 3 ? 2 : 0; break; // S5 3 -> 2 (Bölge 2 boss sıçraması)
                    case 3: sab = s5 ? 3 : 2; arm = s5 ? 2 : stage >= 3 ? 2 : 0; break;
                    case 4:
                        fly = 3;
                        arm = s5 ? 0 : stage >= 3 ? 2 : 0;
                        sab = s5 ? 3 : stage == 4 ? 2 : 0;
                        break;
                    case 5: if (s5) { fly = 4; sab = 4; } else { fly = 3; arm = 2; sab = 3; } break;
                    case 6: if (s5) { fly = 5; sab = 4; } else { fly = 4; arm = 3; sab = 3; } break;
                }
            }
            else if (s5) // 3. dalga yalnız 5. aşamada
            {
                switch (region)
                {
                    case 3: arm = 3; break;
                    case 4: arm = 3; fly = 3; break;
                    case 5: arm = 4; fly = 3; break;
                    case 6: arm = 5; fly = 4; break;
                }
            }
        }
        int cap = Mathf.Max(0, baseCount - 2);
        while (arm + fly + sab > cap)
        {
            if (sab >= fly && sab >= arm) sab--;
            else if (fly >= arm) fly--;
            else arm--;
        }
        return (arm, fly, sab);
    }

    // ==================== İNŞAATÇILAR ====================
    public const int MaxBuilders = 5;
    /// <summary>2. inşaatçının (altınla) açıldığı Ana Üs seviyesi.</summary>
    public const int SecondBuilderMainBaseLevel = 5;

    /// <summary>
    /// n. inşaatçının bedeli. 2.: Ana Üs 5→6 yükseltmesi kadar altın; 3./4./5.: 100/200/300 elmas.
    /// </summary>
    public static (int gold, int gems) BuilderCost(int builderNumber)
    {
        if (builderNumber <= 1 || builderNumber > MaxBuilders) return (0, 0);
        if (builderNumber == 2)
            return (UpgradeCost(BuildingType.MainBase, SecondBuilderMainBaseLevel + 1), 0);
        return (0, (builderNumber - 2) * 100);
    }

    // Yükseltme süreleri artık Resources/Config/TimingConfig.asset içinde (TimingConfig).

    // Ana Üs bedeli (index = hedef seviye); diğer yapılar bunun oranıyla ölçeklenir.
    private static readonly int[] MainBaseCosts =
    {
        0, 0, 150, 250, 400, 600, 850, 1150, 1500, 1900, 2400, 3000, 3700, 4500, 5400, 6500
    };

    public static int ColorTier(int level) => Mathf.Clamp((level - 1) / LevelsPerColorTier, 0, 4);

    public static int UpgradeSecondsTo(int targetLevel) => TimingConfig.Instance.UpgradeSecondsTo(targetLevel);

    public static int UpgradeCost(BuildingType type, int targetLevel)
    {
        int baseCost = MainBaseCosts[Mathf.Clamp(targetLevel, 0, MainBaseCosts.Length - 1)];
        float factor = type == BuildingType.MainBase ? 1f
            : type == BuildingType.CannonTower ? 0.7f
            : type == BuildingType.Monastery ? 0.6f
            : type == BuildingType.GoldMine ? 0.55f
            : 0.5f; // Okçu
        return Mathf.Max(10, Mathf.RoundToInt(baseCost * factor / 10f) * 10);
    }

    public static int FirstUnlockLevel(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.ArcherTower: return 2;
            case BuildingType.CannonTower: return 4;
            case BuildingType.Monastery: return 6;
            default: return 1;
        }
    }

    public static int SecondUnlockLevel(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.ArcherTower: return 5;
            case BuildingType.CannonTower: return 8;
            case BuildingType.Monastery: return 10;
            default: return int.MaxValue;
        }
    }

    /// <summary>Kahraman yükseltme bedeli (anında; seviye tavanı Ana Üs seviyesi).</summary>
    public static int HeroUpgradeCost(int currentLevel) =>
        Mathf.RoundToInt(60f * Mathf.Pow(1.28f, currentLevel - 1) / 10f) * 10;

    /// <summary>Çırak yükseltme bedeli (anında; seviye tavanı Ana Üs seviyesi).</summary>
    public static int ApprenticeUpgradeCost(int currentLevel) =>
        Mathf.RoundToInt(80f * Mathf.Pow(1.3f, currentLevel - 1) / 10f) * 10;

    /// <summary>Kısa Türkçe süre: "15 sn", "30 dk", "2 sa 15 dk", "1 g 6 sa".</summary>
    public static string FormatDuration(double seconds)
    {
        long s = (long)System.Math.Ceiling(System.Math.Max(0d, seconds));
        // Birimler dile göre: sn/dk/sa/g ya da s/m/h/d.
        string S = L.T(" sn", "s"), M = L.T(" dk", "m"), H = L.T(" sa", "h"), D = L.T(" g", "d");
        if (s < 60) return s + S;
        long m = s / 60, sec = s % 60;
        if (m < 60) return sec > 0 && m < 10 ? m + M + " " + sec + S : m + M;
        long h = m / 60, min = m % 60;
        if (h < 24) return min > 0 ? h + H + " " + min + M : h + H;
        long d = h / 24, hr = h % 24;
        return hr > 0 ? d + D + " " + hr + H : d + D;
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

            // Bölge bossları: bölge ilerledikçe güçlenir (aşama ölçeği ayrıca uygulanır).
            case EnemyType.CastleGuardian:
                stats.health = 220f; stats.damage = 16f; stats.moveSpeed = 0.85f;
                stats.attackRange = 2.0f; stats.attackSpeed = 0.55f; stats.goldDrop = 100;
                break;
            case EnemyType.MineWarden:
                stats.health = 260f; stats.damage = 18f; stats.moveSpeed = 0.8f;
                stats.attackRange = 2.0f; stats.attackSpeed = 0.5f; stats.goldDrop = 120;
                break;
            case EnemyType.FrostLord:
                stats.health = 280f; stats.damage = 17f; stats.moveSpeed = 0.85f;
                stats.attackRange = 2.0f; stats.attackSpeed = 0.55f; stats.goldDrop = 130;
                break;
            case EnemyType.DarkKing:
                stats.health = 300f; stats.damage = 19f; stats.moveSpeed = 0.9f;
                stats.attackRange = 2.0f; stats.attackSpeed = 0.6f; stats.goldDrop = 140;
                break;

            case EnemyType.Troll:
                stats.health = 200f;
                stats.damage = 20f;
                stats.moveSpeed = 0.8f;
                stats.attackRange = 2.0f;
                stats.attackSpeed = 0.5f;
                stats.goldDrop = 100;
                break;

            case EnemyType.ShadowWolf:
                stats.health = 26f;
                stats.damage = 6f;
                stats.moveSpeed = 2.5f;
                stats.attackRange = 1.2f;
                stats.attackSpeed = 1.1f;
                stats.goldDrop = 14;
                break;

            case EnemyType.DragonWarrior:
                stats.health = 320f;
                stats.damage = 14f;
                stats.moveSpeed = 1.0f;
                stats.attackRange = 1.8f;
                stats.attackSpeed = 0.7f;
                stats.goldDrop = 150;
                break;

            // Martı Adası: Kızıl Korsanlar (island_base.md §4)
            case EnemyType.Deckhand:
                stats.health = 26f; stats.damage = 4f; stats.moveSpeed = 2.2f;
                stats.attackRange = 1.2f; stats.attackSpeed = 1.2f; stats.goldDrop = 6;
                break;
            case EnemyType.Raider:
                stats.health = 55f; stats.damage = 8f; stats.moveSpeed = 1.3f;
                stats.attackRange = 1.4f; stats.attackSpeed = 0.9f; stats.goldDrop = 14;
                break;
            case EnemyType.ShoreArcher:
                stats.health = 30f; stats.damage = 6f; stats.moveSpeed = 1.4f;
                stats.attackRange = 5f; stats.attackSpeed = 0.8f; stats.goldDrop = 14;
                break;

            // Martı Adası v2 Faz C: Kademe I (island_base_v2.md §7.3, sv 1 tabanı)
            case EnemyType.ShieldLancer:
                stats.health = 80f; stats.damage = 10f; stats.moveSpeed = 1.1f;
                stats.attackRange = 1.6f; stats.attackSpeed = 0.8f; stats.goldDrop = 22;
                break;
            case EnemyType.Rat:
                stats.health = 22f; stats.damage = 4f; stats.moveSpeed = 2.6f;
                stats.attackRange = 1.0f; stats.attackSpeed = 1.2f; stats.goldDrop = 6;
                break;
            case EnemyType.Bat:
                stats.health = 20f; stats.damage = 4f; stats.moveSpeed = 2.0f;
                stats.attackRange = 1.0f; stats.attackSpeed = 1.1f; stats.goldDrop = 8;
                break;
            case EnemyType.WoodGolem:
                stats.health = 160f; stats.damage = 12f; stats.moveSpeed = 0.7f;
                stats.attackRange = 1.6f; stats.attackSpeed = 0.6f; stats.goldDrop = 30;
                break;
            case EnemyType.ButcherCaptain:
                stats.health = 450f; stats.damage = 20f; stats.moveSpeed = 0.9f;
                stats.attackRange = 1.8f; stats.attackSpeed = 0.7f; stats.goldDrop = 120;
                break;

            case EnemyType.ForestWitch:
                stats.health = 240f;
                stats.damage = 10f;
                stats.moveSpeed = 0.9f;
                stats.attackRange = 1.6f;
                stats.attackSpeed = 0.6f;
                stats.goldDrop = 120;
                break;
        }

        return stats;
    }

    /// <summary>
    /// Seviyeye göre statları ölçekle
    /// </summary>
    public const float DamageGrowthPerLevel = 0.06f;

    public void ScaleToLevel(int level)
    {
        // Can aşama başına %8,5, hasar %6 artar (hasar eskiden %8,5'ti: kahraman çok çabuk ölüyordu).
        health *= 1f + (level - 1) * 0.085f;
        damage *= 1f + (level - 1) * DamageGrowthPerLevel;
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
    public EnemyVariant variant;
}

/// <summary>Hasarın kaynağı (karşı düşmanların hasar çarpanları ve telemetri için).</summary>
public enum DamageSource { Hero, Apprentice, Archer, Cannon, Env, IslandArcher }

/// <summary>Mevcut gövdeler üzerinde karşı düşman çeşitleri (round2 C).</summary>
public enum EnemyVariant { None, Armored, Flying, Saboteur }

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
