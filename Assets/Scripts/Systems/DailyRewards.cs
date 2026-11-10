using System;
using UnityEngine;

public enum DailyRewardKind { Gold, BuildSpeedup, CardToken, MineBoost, UnlimitedLives, GemsChest }

public struct DailyReward
{
    public int day;
    public DailyRewardKind kind;
    public int amount;       // altın / dakika / jeton / elmas
    public int chestGold;    // yalnız 7. gün: sandıktan çıkan altın
    public string title;     // kısa: "250 Altın"
    public string icon;      // CampaignArt görsel adı
}

/// <summary>
/// 7 günlük giriş takvimi. Her gün bir ödül alınır; bir gün kaçırılırsa takvim 1. güne döner,
/// 7. gün alınınca yeni döngü başlar. Gün yerel takvim gününe göre sayılır.
///   1) altın (Ana Üs seviyesine göre)  2) 30 dk inşaat hızlandırma  3) daha çok altın
///   4) bedava savaş kartı jetonu  5) 1 sa 2x maden  6) 2 saat sınırsız can  7) 20 elmas + küçük sandık
/// </summary>
public static class DailyRewards
{
    public const int Days = 7;
    public static event Action OnChanged;

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    /// <summary>Testler gün değiştirebilsin diye: null değilse bugünün tarihi bu.</summary>
    public static DateTime? TodayOverride { get; set; }
    private static DateTime TodayDate => (TodayOverride ?? DateTime.Now).Date;
    private static int Stamp(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;
    private static int Today => Stamp(TodayDate);
    private static int Yesterday => Stamp(TodayDate.AddDays(-1));

    public static bool ClaimedToday => Data != null && Data.dailyLastClaimDay == Today;
    public static bool CanClaim => Data != null && !ClaimedToday;

    /// <summary>Bu döngüde alınmış gün sayısı (görünüm için; kaçırılan gün sonrası 0).</summary>
    public static int ClaimedInCycle
    {
        get
        {
            var data = Data;
            if (data == null) return 0;
            if (ClaimedToday) return Mathf.Clamp(data.dailyStreak, 1, Days);
            if (data.dailyLastClaimDay == Yesterday && data.dailyStreak < Days) return data.dailyStreak;
            return 0; // gün kaçtı ya da 7 gün tamamlandı: yeni döngü
        }
    }

    /// <summary>Bugün alınacak (ya da alınmış) günün numarası, 1..7.</summary>
    public static int TodayDay => ClaimedToday ? Mathf.Clamp(Data.dailyStreak, 1, Days) : ClaimedInCycle + 1;

    /// <summary>Kaçırılan gün yüzünden takvim sıfırlandı mı (bilgi mesajı için).</summary>
    public static bool StreakBroken => Data != null && !ClaimedToday && Data.dailyStreak > 0 && Data.dailyStreak < Days
        && Data.dailyLastClaimDay != Yesterday && Data.dailyLastClaimDay != 0;

    /// <summary>Yalnız tek gün kaçtıysa seri elmasla kurtarılabilir (dünü alınmış sayar).</summary>
    public static bool CanSaveStreak => StreakBroken && Data.dailyLastClaimDay == Stamp(TodayDate.AddDays(-2));

    public static bool SaveStreak()
    {
        var gm = GameManager.Instance;
        if (!CanSaveStreak || gm == null || !gm.SpendGems(GemEconomy.StreakSaveCost)) return false;
        Data.dailyLastClaimDay = Yesterday;
        gm.SaveGame();
        OnChanged?.Invoke();
        return true;
    }

    public static DailyReward RewardFor(int day)
    {
        int level = GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.GetMainBaseLevel()) : 1;
        switch (day)
        {
            case 1: return Make(day, DailyRewardKind.Gold, 50 + 50 * level, "Altın", "ui_icon_coin");
            case 2: return Make(day, DailyRewardKind.BuildSpeedup, 30, "30 dk hızlandırma", "ui_icon_hammer");
            case 3: return Make(day, DailyRewardKind.Gold, 100 + 100 * level, "Altın", "ui_icon_coin");
            case 4: return Make(day, DailyRewardKind.CardToken, 1, "Savaş kartı jetonu", "ui_icon_sword");
            case 5: return Make(day, DailyRewardKind.MineBoost, 60, "1 sa 2x maden", "ui_icon_coin");
            case 6: return Make(day, DailyRewardKind.UnlimitedLives, 120, "2 sa sınırsız can", "ui_icon_heart");
            default:
                var chest = Make(Days, DailyRewardKind.GemsChest, 20, "20 elmas + sandık", "gem");
                chest.chestGold = 150 * level;
                return chest;
        }
    }

    private static DailyReward Make(int day, DailyRewardKind kind, int amount, string title, string icon)
    {
        if (kind == DailyRewardKind.Gold) title = $"{amount} Altın";
        return new DailyReward { day = day, kind = kind, amount = amount, title = title, icon = icon };
    }

    /// <summary>Bugünün ödülünü verir. Alınamazsa (bugün alındıysa) null.</summary>
    public static DailyReward? Claim()
    {
        var data = Data;
        var gm = GameManager.Instance;
        if (data == null || gm == null || !CanClaim) return null;
        int day = TodayDay;
        var reward = RewardFor(day);
        switch (reward.kind)
        {
            case DailyRewardKind.Gold:
                gm.AddGold(reward.amount);
                break;
            case DailyRewardKind.BuildSpeedup:
                gm.SpeedUpUpgrade(reward.amount);
                break;
            case DailyRewardKind.CardToken:
                data.cardTokens += reward.amount;
                break;
            case DailyRewardKind.MineBoost:
                gm.ApplyOfflineGoldMineProduction(); // önceki üretim normal hızla yazılsın
                long now = DateTime.UtcNow.Ticks;
                data.mineBoostUntilTicks = Math.Max(now, data.mineBoostUntilTicks) + TimeSpan.FromMinutes(reward.amount).Ticks;
                break;
            case DailyRewardKind.UnlimitedLives:
                LivesSystem.GrantUnlimited(TimeSpan.FromMinutes(reward.amount));
                break;
            case DailyRewardKind.GemsChest:
                gm.AddGems(reward.amount, false);
                gm.AddGold(reward.chestGold);
                data.cardTokens += 1;
                break;
        }
        data.dailyStreak = day;
        data.dailyLastClaimDay = Today;
        gm.SaveGame();
        OnChanged?.Invoke();
        return reward;
    }
}
