using System;
using UnityEngine;

/// <summary>
/// Günlük hakları olan ödüllü reklam ödülleri: inşaatta −30 dk (günde 3), madende 2 sa 2x (günde 1),
/// dükkânda günlük sandık (günde 1). Reklamın kendisi <see cref="RewardedAds"/>; burada yalnız hak sayımı
/// ve ödül verilir. Zaferde 2x altın savaş başına bir kez ve (V2 profilinde) günde 5 hak; yenilgide devamın günlük hakkı yok.
/// </summary>
public static class AdRewards
{
    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
    private static int Today => GameClock.TodayId;

    // ==================== İNŞAAT −30 DK ====================

    // 6. oturum (AŞAMA 5): savaş kartı reklamı (günlük hak; yerel gece yarısı yenilenir)
    public static int CardAdsToday => Data == null || Data.adCardDay != Today ? 0 : Data.adCardCount;
    public static int CardAdsLeft => Mathf.Max(0, FeatureConfig.Instance.dailyBattleCardAds - CardAdsToday);
    public static bool CardAdAvailable => CardAdsLeft > 0;
    public static void ConsumeCardAd()
    {
        var data = Data;
        if (data == null) return;
        if (data.adCardDay != Today) { data.adCardDay = Today; data.adCardCount = 0; }
        data.adCardCount++;
        GameManager.Instance?.SaveGame();
    }
    /// <summary>Günlük hakların yenilenmesine kalan saniye (yerel gece yarısı).</summary>
    public static double SecondsToDailyReset => (GameClock.LocalNow.Date.AddDays(1) - GameClock.LocalNow).TotalSeconds;

    public static int SpeedupAdsToday => Data == null || Data.adSpeedupDay != Today ? 0 : Data.adSpeedupCount;
    public static int SpeedupAdsLeft => Mathf.Max(0, GemEconomy.DailySpeedupAds - SpeedupAdsToday);

    /// <summary>Reklam izlendi: süren işi 30 dk kısaltır (günlük hak varsa).</summary>
    public static bool RewardSpeedup(BuilderTaskSave task)
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || SpeedupAdsLeft <= 0) return false;
        if (!gm.SpeedUpTask(task, GemEconomy.AdSpeedupMinutes)) return false;
        if (data.adSpeedupDay != Today) { data.adSpeedupDay = Today; data.adSpeedupCount = 0; }
        data.adSpeedupCount++;
        gm.SaveGame();
        return true;
    }

    // ==================== KART ATÖLYESİ: ÜRETİMİ ANINDA BİTİR ====================

    public static int WorkshopAdsToday => Data?.workshop == null || Data.workshop.adDay != Today ? 0 : Data.workshop.adsToday;
    public static int WorkshopAdsLeft => Mathf.Max(0, GemEconomy.DailyWorkshopAds - WorkshopAdsToday);

    /// <summary>Reklam izlendi: günlük atölye hakkından 1 düşer (inşaatın −30 dk hakkından ayrıdır). Hak yoksa false.</summary>
    public static bool ConsumeWorkshopAd()
    {
        var data = Data;
        if (data == null || WorkshopAdsLeft <= 0) return false;
        if (data.workshop == null) data.workshop = new WorkshopSaveData();
        if (data.workshop.adDay != Today) { data.workshop.adDay = Today; data.workshop.adsToday = 0; }
        data.workshop.adsToday++;
        GameManager.Instance?.SaveGame();
        return true;
    }

    // ==================== ZAFER 2X ÖDÜL ====================

    /// <summary>Günlük 2x zafer hakkı; 0 = sınırsız (LEGACY). V2: günde 5 (ECONOMY §4).</summary>
    public static int VictoryDoubleLimit => Economy.Active.victoryDoubleAdsPerDay;
    public static int VictoryDoubleToday => Data == null || Data.adDoubleDay != Today ? 0 : Data.adDoubleCount;
    public static bool VictoryDoubleUnlimited => VictoryDoubleLimit <= 0;
    public static int VictoryDoubleLeft => VictoryDoubleUnlimited ? int.MaxValue : Mathf.Max(0, VictoryDoubleLimit - VictoryDoubleToday);
    public static bool VictoryDoubleAvailable => VictoryDoubleLeft > 0;

    /// <summary>2x zafer reklamı izlendi: günlük haktan 1 düşer (sınırsızsa yalnız sayılır).</summary>
    public static void ConsumeVictoryDouble()
    {
        var data = Data;
        if (data == null) return;
        if (data.adDoubleDay != Today) { data.adDoubleDay = Today; data.adDoubleCount = 0; }
        data.adDoubleCount++;
    }

    // ==================== MADEN 2X ====================

    public static bool MineAdAvailable => Data != null && Data.adMineDay != Today;

    public static bool RewardMineBoost()
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || !MineAdAvailable) return false;
        data.adMineDay = Today;
        gm.AddMineBoost(TimeSpan.FromHours(GemEconomy.AdMineBoostHours));
        return true;
    }

    // ==================== GÜNLÜK SANDIK ====================

    public static bool ChestAvailable => Data != null && Data.adChestDay != Today;

    /// <summary>
    /// Günlük sandık: %60 altın (Ana Üs seviyesine göre), %25 3 elmas, %15 savaş kartı jetonu.
    /// Verilen ödülün kısa açıklamasını döner; hak yoksa null.
    /// </summary>
    public static string OpenChest(float roll = -1f)
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || !ChestAvailable) return null;
        data.adChestDay = Today;
        if (roll < 0f) roll = UnityEngine.Random.value;
        string result;
        if (roll < 0.6f)
        {
            int gold = Economy.Active.adChestGoldBase + Economy.Active.adChestGoldPerMb * Mathf.Max(1, gm.GetMainBaseLevel());
            gm.AddGold(gold);
            result = L.T($"+{gold} altın", $"+{gold} gold");
        }
        else if (roll < 0.85f)
        {
            gm.AddGems(3, false);
            result = L.T("+3 elmas", "+3 gems");
        }
        else
        {
            data.cardTokens++;
            result = L.T("+1 savaş kartı jetonu", "+1 battle card token");
        }
        gm.SaveGame();
        return result;
    }
}
