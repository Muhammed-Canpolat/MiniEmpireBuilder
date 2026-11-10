/// <summary>Özel savaş modları: sefer aşaması dışındaki savaşlar (sefer akışını aynen kullanır).</summary>
public enum SpecialBattle { None, Endless, Invasion }

/// <summary>
/// Başlatılan savaşın bağlamı. Sefer aşaması (bölge + seviye) ya da özel mod (Sonsuz Sefer katı,
/// Ejder İstilası). Özel modlar sefer akışını (can, kart, devam, zafer ekranı) aynen kullanır:
/// <see cref="IsCampaign"/> true, bölge ve seviye verisi çalışma anında üretilir.
/// </summary>
public static class BattleSession
{
    public static int RegionId { get; private set; }
    public static int LevelNumber { get; private set; }
    public static SpecialBattle Special { get; private set; }
    /// <summary>Sonsuz Sefer katı (yalnız Endless modunda).</summary>
    public static int Floor { get; private set; }

    public static bool IsCampaign => RegionId > 0 && LevelNumber > 0;
    public static bool IsEndless => Special == SpecialBattle.Endless;
    public static bool IsInvasion => Special == SpecialBattle.Invasion;
    public static bool IsSpecial => Special != SpecialBattle.None;

    public static RegionData Region => IsEndless ? EndlessMode.Region(Floor)
        : IsInvasion ? DragonInvasion.Region
        : CampaignCatalog.Get(RegionId);

    public static CampaignLevelData Level => IsEndless ? EndlessMode.Level(Floor)
        : IsInvasion ? DragonInvasion.Level
        : Region != null && LevelNumber >= 1 && LevelNumber <= 5 ? Region.levels[LevelNumber - 1] : null;

    /// <summary>Düşman gücü için savaş seviyesi (sefer: bölge × 5 + aşama; özel modlar kendi hesabı).</summary>
    public static int BattleLevel => IsEndless ? EndlessMode.BattleLevel(Floor)
        : IsInvasion ? DragonInvasion.BattleLevel
        : (RegionId - 1) * 5 + LevelNumber;

    public static void Begin(int regionId, int levelNumber)
    {
        RegionId = regionId;
        LevelNumber = levelNumber;
        Special = SpecialBattle.None;
        Floor = 0;
    }

    public static void BeginEndless(int floor)
    {
        Special = SpecialBattle.Endless;
        Floor = UnityEngine.Mathf.Max(1, floor);
        RegionId = EndlessMode.RegionId;
        LevelNumber = 1;
    }

    public static void BeginInvasion()
    {
        Special = SpecialBattle.Invasion;
        Floor = 0;
        RegionId = DragonInvasion.RegionId;
        LevelNumber = 1;
    }

    public static void Clear()
    {
        RegionId = 0;
        LevelNumber = 0;
        Special = SpecialBattle.None;
        Floor = 0;
    }
}
