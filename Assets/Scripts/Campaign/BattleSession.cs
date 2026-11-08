public static class BattleSession
{
    public static int RegionId { get; private set; }
    public static int LevelNumber { get; private set; }
    public static bool IsCampaign => RegionId > 0 && LevelNumber > 0;

    public static RegionData Region => CampaignCatalog.Get(RegionId);
    public static CampaignLevelData Level => Region != null && LevelNumber >= 1 && LevelNumber <= 5
        ? Region.levels[LevelNumber - 1] : null;

    public static void Begin(int regionId, int levelNumber)
    {
        RegionId = regionId;
        LevelNumber = levelNumber;
    }

    public static void Clear()
    {
        RegionId = 0;
        LevelNumber = 0;
    }
}
