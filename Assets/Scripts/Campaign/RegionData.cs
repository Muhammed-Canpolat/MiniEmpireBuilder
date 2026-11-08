using System;
using System.Collections.Generic;
using UnityEngine;

public enum CampaignLevelKind { Survive, DefendBase, DefendMine, TimeTrial, Boss }

// Battle already owns a WaveData/LevelData type. Campaign metadata lives beside it.
[Serializable]
public class CampaignLevelData
{
    public int number;
    public CampaignLevelKind kind;
    public int waveCount;
    public int[] enemyCounts;
    public int reward;
    public string modifier;
    [TextArea(2, 3)] public string description;
    public string[] starRules;
    public int TotalEnemies
    {
        get
        {
            int total = 0;
            if (enemyCounts != null) foreach (int count in enemyCounts) total += count;
            return total;
        }
    }
}

[CreateAssetMenu(menuName = "Mini Empire Builder/Campaign Region")]
public class RegionData : ScriptableObject
{
    public int id;
    public string displayName;
    public int requiredBaseLevel;
    public Color colorTone;
    public Sprite groundTile;
    public EnemyType[] enemyTypes;
    public string specialMechanic;
    public string bossName;
    [TextArea(2, 3)] public string description;
    public CampaignLevelData[] levels = new CampaignLevelData[5];
}

public static class CampaignCatalog
{
    private static RegionData[] cached;
    public static RegionData[] Regions
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.LoadAll<RegionData>("Campaign");
                Array.Sort(cached, (a, b) => a.id.CompareTo(b.id));
            }
            return cached;
        }
    }

    public static RegionData Get(int id)
    {
        foreach (var region in Regions)
            if (region.id == id) return region;
        return null;
    }

    public static string KindName(CampaignLevelKind kind)
    {
        switch (kind)
        {
            case CampaignLevelKind.Survive: return "Hayatta kal";
            case CampaignLevelKind.DefendBase: return "Üssü koru";
            case CampaignLevelKind.DefendMine: return "Madeni koru";
            case CampaignLevelKind.TimeTrial: return "Zamana karşı";
            default: return "Boss";
        }
    }
}

[Serializable]
public class CampaignLevelProgress
{
    public int regionId;
    public int levelNumber;
    [Range(0, 3)] public int stars;
}

public static class CampaignProgress
{
    private static List<CampaignLevelProgress> Entries => GameManager.Instance?.PlayerData?.campaignProgress;

    public static int Stars(int regionId, int levelNumber)
    {
        var entries = Entries;
        if (entries == null) return 0;
        var entry = entries.Find(e => e.regionId == regionId && e.levelNumber == levelNumber);
        return entry?.stars ?? 0;
    }

    public static bool IsRegionOpen(RegionData region)
    {
        if (region == null || GameManager.Instance == null) return false;
        if (GameManager.Instance.GetMainBaseLevel() < region.requiredBaseLevel) return false;
        return region.id == 1 || Stars(region.id - 1, 5) > 0;
    }

    public static bool IsLevelOpen(RegionData region, int number)
    {
        if (!IsRegionOpen(region) || number < 1 || number > 5) return false;
        return number == 1 || Stars(region.id, number - 1) > 0;
    }

    public static int FirstIncomplete(RegionData region)
    {
        for (int level = 1; level <= 5; level++)
            if (Stars(region.id, level) == 0) return level;
        return 5;
    }

    public static int RegionStars(int regionId)
    {
        int sum = 0;
        for (int level = 1; level <= 5; level++) sum += Stars(regionId, level);
        return sum;
    }

    public static int TotalStars()
    {
        int sum = 0;
        foreach (var region in CampaignCatalog.Regions) sum += RegionStars(region.id);
        return sum;
    }

    public static void RecordVictory(int regionId, int levelNumber, int stars)
    {
        var entries = Entries;
        if (entries == null) return;
        var entry = entries.Find(e => e.regionId == regionId && e.levelNumber == levelNumber);
        if (entry == null)
        {
            entry = new CampaignLevelProgress { regionId = regionId, levelNumber = levelNumber };
            entries.Add(entry);
        }
        entry.stars = Mathf.Max(entry.stars, Mathf.Clamp(stars, 1, 3));
        GameManager.Instance.SaveGame();
    }
}
