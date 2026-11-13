using UnityEngine;

/// <summary>
/// Altın ekonomisi profili (AŞAMA 4A, ECONOMY.md §7). İki asset: Resources/Config/Economy_Legacy.asset (eski değerler,
/// davranış birebir) ve Economy_V2.asset (öneri). Etkin profil: <see cref="FeatureConfig.economyProfile"/> (varsayılan
/// "Economy_V2"); asset bulunamazsa kod varsayılanları = LEGACY (güvenli). Erişim: <see cref="Economy.Active"/>.
/// Elmas fiyatları, GoldPerGem, 3.–5. inşaatçı elmasları ve süreler burada DEĞİL (değişmez).
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire Builder/Economy Config", fileName = "Economy_V2")]
public sealed class EconomyConfig : ScriptableObject
{
    [Header("Kimlik")]
    public string profileName = "Legacy";

    [Header("Bina bedelleri (index = hedef seviye)")]
    public int[] mainBaseCosts = { 0, 0, 150, 250, 400, 600, 850, 1150, 1500, 1900, 2400, 3000, 3700, 4500, 5400, 6500 };
    public float factorArcher = 0.5f;
    public float factorCannon = 0.7f;
    public float factorMonastery = 0.6f;
    public float factorMine = 0.55f;
    public float factorBarracks = 0.65f;
    public float factorHouse = 0.4f;
    public float factorWorkshop = 0.45f;
    /// <summary>2. inşaatçının altın bedeli (Ana Üs tablosundan ayrık sabit).</summary>
    public int builder2GoldCost = 850;

    [Header("Maden")]
    public float mineGpsBase = 1f;
    public float mineGpsPerLevel = 0.5f;
    public int mineStorageBase = 200;
    public int mineStoragePerLevel = 150;
    public float houseStorageBase = 0.05f;
    public float houseStoragePerLevel = 0.005f;
    /// <summary>Çırak maden bonusu (%), index = çırak seviyesi − 1.</summary>
    public int[] apprenticeMineBonus = { 10, 15, 20, 25, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50 };

    [Header("Cüzdan")]
    public float walletCapMult = 1.5f;
    public int walletCapMin = 500;

    [Header("Anında yükseltmeler")]
    public float heroCostBase = 60f;
    public float heroCostGrowth = 1.28f;
    public float apprenticeCostBase = 80f;
    public float apprenticeCostGrowth = 1.3f;

    [Header("Savaş ödülü")]
    /// <summary>Doğruysa Sefer zafer ödülü bölge asset'inden okunur (LEGACY); yanlışsa a + b(r−1) + c(s−1).</summary>
    public bool victoryRewardFromAsset = true;
    public int victoryRewardBase = 35;
    public int victoryRewardPerRegion = 35;
    public int victoryRewardPerStage = 18;
    /// <summary>Zaferde net savaş altınının cüzdana geçen oranı.</summary>
    public float killGoldTransferRate = 1f;
    /// <summary>Düşman altını seviye başına artışı (EnemyStats.ScaleToLevel).</summary>
    public float enemyGoldGrowthPerLevel = 0.08f;
    public float replayRewardRate = 0.3f;
    /// <summary>Zaferde 2x ödül reklamı günlük hak (0 = sınırsız).</summary>
    public int victoryDoubleAdsPerDay = 0;

    [Header("Sonsuz Sefer / İstila savaş içi kule bedeli (ECONOMY §5)")]
    /// <summary>Doğruysa formül kullanılır; yanlışsa eski hata (bölge 99 → ×20,6) döner. Her iki profilde AÇIK (hata düzeltmesi).</summary>
    public bool endlessBuildCostFormula = true;
    public float endlessBuildCostK = 1f;
    public float invasionPerWave = 14f;

    [Header("Günlük altınlar")]
    public int calendarDay1Base = 50;
    public int calendarDay1PerMb = 50;
    public int calendarDay3Base = 100;
    public int calendarDay3PerMb = 100;
    public int calendarDay7ChestPerMb = 150;
    public int questGoldBase = 80;
    public int questGoldPerMb = 40;
    public int adChestGoldBase = 100;
    public int adChestGoldPerMb = 50;

    [Header("Ada Tüccarı (altın yutağı)")]
    public bool traderEnabled = false;
    public int traderPackAmount = 50;
    public int traderBaseCost = 400;
    public int traderPerMbCost = 120;
    public int traderMbRef = 5;
    public int traderPacksPerDay = 2;

    // ---- Hesaplayıcılar (tüm çağıranlar buradan geçer) ----
    public int MainBaseCost(int targetLevel)
    {
        if (mainBaseCosts == null || mainBaseCosts.Length == 0) return 0;
        return mainBaseCosts[Mathf.Clamp(targetLevel, 0, mainBaseCosts.Length - 1)];
    }

    public float UpgradeFactor(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.MainBase: return 1f;
            case BuildingType.CannonTower: return factorCannon;
            case BuildingType.Monastery: return factorMonastery;
            case BuildingType.GoldMine: return factorMine;
            case BuildingType.CardWorkshop: return factorWorkshop;
            case BuildingType.Barracks: return factorBarracks;
            case BuildingType.House: return factorHouse;
            default: return factorArcher;
        }
    }

    public float MineGps(int level) => mineGpsBase + (level - 1) * mineGpsPerLevel;
    public int MineStorage(int level) => mineStorageBase + (level - 1) * mineStoragePerLevel;
    public float HouseStorageBonus(int level) => houseStorageBase + houseStoragePerLevel * (Mathf.Max(1, level) - 1);

    public int ApprenticeMineBonusPercent(int level)
    {
        if (apprenticeMineBonus == null || apprenticeMineBonus.Length == 0) return 10;
        return apprenticeMineBonus[Mathf.Clamp(level - 1, 0, apprenticeMineBonus.Length - 1)];
    }

    /// <summary>Cüzdan tavanı: sıradaki Ana Üs bedelinin çarpanı (en az walletCapMin), 100'e yukarı yuvarlı.</summary>
    public int WalletCap(int nextMainBaseCost) =>
        Mathf.Max(walletCapMin, Mathf.CeilToInt(nextMainBaseCost * walletCapMult / 100f) * 100);

    public int HeroCost(int currentLevel) =>
        Mathf.RoundToInt(heroCostBase * Mathf.Pow(heroCostGrowth, currentLevel - 1) / 10f) * 10;

    public int ApprenticeCost(int currentLevel) =>
        Mathf.RoundToInt(apprenticeCostBase * Mathf.Pow(apprenticeCostGrowth, currentLevel - 1) / 10f) * 10;

    /// <summary>Formül ödülü: a + b(r−1) + c(s−1) (asset ödülü kullanılmıyorsa).</summary>
    public int VictoryRewardFormula(int region, int stage) =>
        victoryRewardBase + victoryRewardPerRegion * (Mathf.Max(1, region) - 1) + victoryRewardPerStage * (Mathf.Max(1, stage) - 1);

    /// <summary>Ada Tüccarı paket bedeli (altın): taban + adım × (Ana Üs − referans).</summary>
    public int TraderPackCost(int mainBaseLevel) =>
        traderBaseCost + traderPerMbCost * Mathf.Max(0, mainBaseLevel - traderMbRef);

    /// <summary>Sonsuz/İstila kule bedeli: round5(taban × K × (1 + büyüme(sev−1)) × dalgaBaşı / 12).</summary>
    public int EndlessBuildCost(int baseCost, int battleLevel, float perWave) =>
        Mathf.RoundToInt(baseCost * endlessBuildCostK * (1f + enemyGoldGrowthPerLevel * (Mathf.Max(1, battleLevel) - 1)) * perWave / 12f / 5f) * 5;

    // ---- Kod içi profiller (asset yoksa LEGACY; asset dosyaları bunlarla birebir olmalı: EconomyProfileTests) ----
    public static EconomyConfig CreateLegacy()
    {
        var c = CreateInstance<EconomyConfig>();
        c.name = "Economy_Legacy";
        c.profileName = "Legacy";
        return c;
    }

    public static EconomyConfig CreateV2()
    {
        var c = CreateInstance<EconomyConfig>();
        c.name = "Economy_V2";
        c.profileName = "V2";
        c.mainBaseCosts = new[] { 0, 0, 150, 250, 400, 600, 850, 1250, 1800, 2500, 3500, 4800, 6400, 8400, 10800, 13800 };
        c.mineGpsBase = 0.6f;
        c.mineGpsPerLevel = 0.2f;
        c.apprenticeMineBonus = new[] { 5, 8, 10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 };
        c.heroCostBase = 75f;
        c.heroCostGrowth = 1.30f;
        c.apprenticeCostBase = 100f;
        c.apprenticeCostGrowth = 1.32f;
        c.victoryRewardFromAsset = false;
        c.victoryRewardBase = 40;
        c.victoryRewardPerRegion = 30;
        c.victoryRewardPerStage = 12;
        c.killGoldTransferRate = 0.75f;
        c.victoryDoubleAdsPerDay = 0;   // 13 Kasım kararı (D17 kaldırıldı): 2x zafer reklamı sınırsız
        c.traderEnabled = true;
        return c;
    }
}

/// <summary>Etkin ekonomi profiline tek erişim noktası.</summary>
public static class Economy
{
    public const string LegacyName = "Economy_Legacy";
    public const string V2Name = "Economy_V2";

    private static EconomyConfig cached;
    private static EconomyConfig overridden;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cached = null; overridden = null; }

    public static EconomyConfig Active
    {
        get
        {
            if (overridden != null) return overridden;
            if (cached != null) return cached;
            cached = Load(FeatureConfig.Instance.economyProfile);
            return cached;
        }
    }

    /// <summary>Profili adıyla yükler ("Economy_V2", "Economy_Legacy" ya da kısa "V2"/"Legacy"); yoksa LEGACY kod varsayılanları.</summary>
    public static EconomyConfig Load(string profile)
    {
        string name = string.IsNullOrEmpty(profile) ? LegacyName : profile;
        if (name == "V2") name = V2Name;
        else if (name == "Legacy") name = LegacyName;
        var asset = Resources.Load<EconomyConfig>("Config/" + name);
        if (asset != null) return asset;
        return EconomyConfig.CreateLegacy();   // asset yoksa güvenli varsayılan: LEGACY
    }

    /// <summary>Test kancası: profili zorlar. <see cref="ClearOverride"/> ile geri alınır.</summary>
    public static void OverrideForTests(string profile) => overridden = Load(profile);
    public static void ClearOverride() { overridden = null; cached = null; }
}
