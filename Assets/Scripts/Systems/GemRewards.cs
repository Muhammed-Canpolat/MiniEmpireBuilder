using System;

/// <summary>
/// Satın alma gelene kadar elmas kazanma yolları:
///   seferde her yeni yıldız +1, bölge bossunu ilk yenişte +10, her yeni Ana Üs seviyesi +5,
///   ödüllü reklam +2 (günde 3). 7. gün sandığı DailyRewards'ta.
/// </summary>
public static class GemRewards
{
    public static event Action<int, string> OnGemsEarned; // miktar, neden

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
    private static int Today => int.Parse(DateTime.Now.ToString("yyyyMMdd"));

    /// <summary>Son zaferde kazanılan elmas (zafer ekranı için).</summary>
    public static int LastVictoryGems { get; private set; }

    /// <summary>
    /// Zafer kaydedilmeden ÖNCE çağrılır: önceki yıldızla karşılaştırıp yeni yıldızlar ve
    /// ilk boss zaferi için elmas verir. Verilen elmas sayısını döner.
    /// </summary>
    public static int ForVictory(int regionId, int levelNumber, int newStars)
    {
        LastVictoryGems = 0;
        var gm = GameManager.Instance;
        if (gm == null || gm.PlayerData == null) return 0;
        int before = CampaignProgress.Stars(regionId, levelNumber);
        int gained = Math.Max(0, Math.Min(3, newStars) - before);
        int gems = gained * GemEconomy.GemsPerNewStar;
        if (levelNumber == 5 && before == 0) gems += GemEconomy.GemsPerFirstBossWin;
        if (gems <= 0) return 0;
        gm.AddGems(gems, false);
        LastVictoryGems = gems;
        OnGemsEarned?.Invoke(gems, "victory");
        return gems;
    }

    /// <summary>Yeni Ana Üs seviyesi: daha önce ödüllendirilmemişse +5.</summary>
    public static void ForMainBaseLevel(int level)
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || level <= 1 || level <= data.gemRewardedBaseLevel) return;
        data.gemRewardedBaseLevel = level;
        gm.AddGems(GemEconomy.GemsPerMainBaseLevel, false);
        OnGemsEarned?.Invoke(GemEconomy.GemsPerMainBaseLevel, "mainbase");
    }

    public static int AdGemsToday
    {
        get
        {
            var data = Data;
            return data == null || data.gemAdDay != Today ? 0 : data.gemAdCount;
        }
    }

    public static bool CanWatchAdForGems => Data != null && AdGemsToday < GemEconomy.DailyGemAds;

    /// <summary>Ödüllü reklam izlendi: +2 elmas (günde en fazla 3).</summary>
    public static bool RewardAdGems()
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || !CanWatchAdForGems) return false;
        if (data.gemAdDay != Today)
        {
            data.gemAdDay = Today;
            data.gemAdCount = 0;
        }
        data.gemAdCount++;
        gm.AddGems(GemEconomy.GemsPerAd);
        OnGemsEarned?.Invoke(GemEconomy.GemsPerAd, "ad");
        return true;
    }
}
