using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Sonsuz Sefer: son bölgenin bossu yenilince açılır. Her kat ayrı bir savaş; kaldığın en yüksek
/// kattan devam edilir. Her 5 katta tema değişir (bölgeler sırayla: zemin, düşmanlar, sis/don, boss);
/// 5'in katları boss katıdır. Güç 6. bölgenin sonundan devam eder (savaş seviyesi 30 + kat).
/// Ödül: her kat altın; 5'in katlarını ilk geçişte +3 elmas.
/// </summary>
public static class EndlessMode
{
    public const int RegionId = 99;
    public const int FloorsPerTheme = 5;
    public const int MilestoneGems = 3;

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    public static bool Unlocked
    {
        get
        {
            var regions = CampaignCatalog.Regions;
            return regions.Length > 0 && CampaignProgress.Stars(regions[regions.Length - 1].id, 5) > 0;
        }
    }

    public static int BestFloor => Data?.endlessBestFloor ?? 0;
    public static int NextFloor => BestFloor + 1;
    public static int BattleLevel(int floor) => 30 + Mathf.Max(1, floor);
    public static int Reward(int floor) => 300 + 20 * Mathf.Max(1, floor);
    public static bool IsBossFloor(int floor) => floor % FloorsPerTheme == 0;

    /// <summary>Bu katın teması olan bölge (5 katta bir sıradaki bölge).</summary>
    public static RegionData Theme(int floor)
    {
        var regions = CampaignCatalog.Regions;
        if (regions.Length == 0) return null;
        return regions[((Mathf.Max(1, floor) - 1) / FloorsPerTheme) % regions.Length];
    }

    private static readonly Dictionary<int, RegionData> regionCache = new Dictionary<int, RegionData>();

    public static RegionData Region(int floor)
    {
        floor = Mathf.Max(1, floor);
        if (regionCache.TryGetValue(floor, out var cached) && cached != null) return cached;
        var theme = Theme(floor);
        var region = ScriptableObject.CreateInstance<RegionData>();
        region.id = RegionId;
        region.displayName = L.T("Sonsuz Sefer", "Endless Campaign");
        region.requiredBaseLevel = theme != null ? theme.requiredBaseLevel : 1;
        region.colorTone = theme != null ? theme.colorTone : Color.white;
        region.groundTile = theme != null ? theme.groundTile : null;
        region.enemyTypes = theme != null && theme.enemyTypes != null && theme.enemyTypes.Length > 0
            ? theme.enemyTypes : new[] { EnemyType.Wolf, EnemyType.Zombie, EnemyType.Orc };
        region.specialMechanic = theme != null ? theme.specialMechanic : "";
        region.bossType = theme != null ? theme.bossType : EnemyType.Troll;
        region.bossName = theme != null ? theme.bossName : "";
        region.description = L.T("Sınırsız katlar, artan zorluk.", "Endless floors, rising difficulty.");
        region.levels = new[] { Level(floor) };
        regionCache[floor] = region;
        return region;
    }

    private static readonly CampaignLevelKind[] KindCycle =
        { CampaignLevelKind.Survive, CampaignLevelKind.DefendBase, CampaignLevelKind.DefendMine, CampaignLevelKind.TimeTrial };

    public static CampaignLevelData Level(int floor)
    {
        floor = Mathf.Max(1, floor);
        bool boss = IsBossFloor(floor);
        var kind = boss ? CampaignLevelKind.Boss : KindCycle[(floor - 1) % FloorsPerTheme % KindCycle.Length];
        int waves = boss ? 3 : Mathf.Min(4, 2 + floor / 8);
        int perWave = Mathf.Min(28, 12 + floor / 2);
        var counts = new int[waves];
        for (int i = 0; i < waves; i++) counts[i] = perWave + i * 2;
        return new CampaignLevelData
        {
            number = floor,
            kind = kind,
            waveCount = waves,
            enemyCounts = counts,
            reward = Reward(floor),
            modifier = "",
            description = L.T($"Kat {floor}: {CampaignCatalog.KindName(kind)}", $"Floor {floor}: {CampaignCatalog.KindName(kind)}"),
            starRules = new string[0]
        };
    }

    /// <summary>Kat geçildi: en iyi kat güncellenir; 5'in katı ilk kez geçildiyse elmas. Verilen elmas.</summary>
    public static int RecordClear(int floor)
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || floor <= data.endlessBestFloor) return 0;
        data.endlessBestFloor = floor;
        int gems = IsBossFloor(floor) ? MilestoneGems : 0;
        if (gems > 0) gm.AddGems(gems, false);
        gm.SaveGame();
        return gems;
    }
}

/// <summary>
/// Ejder İstilası: her hafta sonu (Cumartesi–Pazar, yerel saat) açılan etkinlik savaşı. Ana Üs 5 gerekir.
/// Ejder Savaşçısı ve ordusu oyuncunun sefer ilerlemesinden biraz güçlü gelir. Hafta sonunun ilk zaferi
/// +15 elmas ve bol altın; sonraki zaferler yalnız altının %30'u.
/// </summary>
public static class DragonInvasion
{
    public const int RegionId = 98;
    public const int RequiredBaseLevel = 5;
    public const int FirstWinGems = 15;

    /// <summary>Test menüsü: hafta içi de açık say.</summary>
    public static bool ForceActive { get; set; }

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    public static bool IsWeekend(DateTime now) => now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
    public static bool Active => ForceActive || IsWeekend(DateTime.Now);

    public static bool Unlocked => GameManager.Instance != null && GameManager.Instance.GetMainBaseLevel() >= RequiredBaseLevel;

    /// <summary>Bu hafta sonunun kimliği (yıl × 100 + hafta): ilk zafer ödülü haftada bir.</summary>
    public static int WeekId(DateTime now)
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;
        return now.Year * 100 + calendar.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    public static bool FirstWinAvailable => Data != null && Data.invasionRewardWeek != WeekId(DateTime.Now);

    /// <summary>Etkinliğin bitmesine (Pazar gece yarısı) ya da başlamasına (Cumartesi) kalan süre.</summary>
    public static TimeSpan TimeLeft(DateTime now)
    {
        if (IsWeekend(now))
        {
            int days = now.DayOfWeek == DayOfWeek.Saturday ? 2 : 1;
            return now.Date.AddDays(days) - now;
        }
        int untilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
        return now.Date.AddDays(untilSaturday) - now;
    }

    /// <summary>Oyuncunun ulaştığı en yüksek sefer aşaması + 3 (en az 8).</summary>
    public static int BattleLevel
    {
        get
        {
            int best = 5;
            foreach (var region in CampaignCatalog.Regions)
                for (int level = 1; level <= 5; level++)
                    if (CampaignProgress.Stars(region.id, level) > 0) best = Mathf.Max(best, (region.id - 1) * 5 + level);
            return Mathf.Max(8, best + 3);
        }
    }

    public static int Gold(int battleLevel) => 400 + 40 * battleLevel;

    private static RegionData region;
    private static int regionLevel = -1;

    public static RegionData Region
    {
        get
        {
            int level = BattleLevel;
            if (region != null && regionLevel == level) return region;
            regionLevel = level;
            var lair = CampaignCatalog.Get(6);
            region = ScriptableObject.CreateInstance<RegionData>();
            region.id = RegionId;
            region.displayName = L.T("Ejder İstilası", "Dragon Invasion");
            region.requiredBaseLevel = RequiredBaseLevel;
            region.colorTone = lair != null ? lair.colorTone : new Color(0.62f, 0.27f, 0.12f);
            region.groundTile = lair != null ? lair.groundTile : null;
            region.enemyTypes = lair != null ? lair.enemyTypes : new[] { EnemyType.Zombie, EnemyType.Orc, EnemyType.Wolf };
            region.specialMechanic = "";
            region.bossType = EnemyType.DragonWarrior;
            region.bossName = EnemyTypeInfo.DisplayName(EnemyType.DragonWarrior);
            region.description = L.T("Ejder Savaşçısı ordusuyla krallığa saldırıyor!", "The Dragon Warrior attacks with his army!");
            region.levels = new[]
            {
                new CampaignLevelData
                {
                    number = 1,
                    kind = CampaignLevelKind.Boss,
                    waveCount = 3,
                    enemyCounts = new[] { 14, 16, 12 },
                    reward = Gold(level),
                    modifier = "",
                    description = region.description,
                    starRules = new string[0]
                }
            };
            return region;
        }
    }

    public static CampaignLevelData Level => Region.levels[0];

    /// <summary>Zafer: hafta sonunun ilk zaferiyse +15 elmas. Verilen elmas.</summary>
    public static int RecordWin()
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || !FirstWinAvailable) return 0;
        data.invasionRewardWeek = WeekId(DateTime.Now);
        gm.AddGems(FirstWinGems, false);
        gm.SaveGame();
        return FirstWinGems;
    }
}
