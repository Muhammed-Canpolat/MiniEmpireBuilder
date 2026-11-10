using System;
using UnityEngine;

/// <summary>
/// Günlük hakları olan ödüllü reklam ödülleri: inşaatta −30 dk (günde 3), madende 2 sa 2x (günde 1),
/// dükkânda günlük sandık (günde 1). Reklamın kendisi <see cref="RewardedAds"/>; burada yalnız hak sayımı
/// ve ödül verilir. (Zaferde 2x altın ve yenilgide devam savaş başına bir kez olduğundan günlük hakları yok.)
/// </summary>
public static class AdRewards
{
    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
    private static int Today => int.Parse(DateTime.Now.ToString("yyyyMMdd"));

    // ==================== İNŞAAT −30 DK ====================

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
            int gold = 100 + 50 * Mathf.Max(1, gm.GetMainBaseLevel());
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
