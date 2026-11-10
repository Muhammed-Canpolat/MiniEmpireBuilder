using System;
using UnityEngine;

/// <summary>
/// Elmas fiyatları ve süre hesapları için tek kaynak. Denge ayarı yalnız buradan yapılır;
/// harcama işlemleri GameManager / LivesSystem / DailyRewards / BattleManager üzerindedir.
/// </summary>
public static class GemEconomy
{
    // ==================== İNŞAAT ====================
    /// <summary>İnşaat İksiri: süre boyunca bütün inşaatçılar bu kat hızlı çalışır.</summary>
    public const int PotionCost = 40;
    public const int PotionHours = 2;
    public const int PotionSpeed = 4;

    /// <summary>Geçici (misafir) inşaatçı: kalıcı inşaatçıların yanına süreli bir tane daha.</summary>
    public const int TempBuilderCost = 50;
    public const int TempBuilderHours = 24;

    /// <summary>Eksik altını tamamlama kuru: 1 elmas = bu kadar altın.</summary>
    public const int GoldPerGem = 40;

    /// <summary>Bu süreden kısa kalan işler bedava bitirilir.</summary>
    public const int FreeFinishSeconds = 60;

    // ==================== SAVAŞ ====================
    public const int ContinueCost = 25;       // Ana Üs yıkılınca devam (savaş başına 1 kez)
    public const float ContinueBaseHealth = 0.5f;
    public const int InstantRespawnCost = 3;   // düşen savaşçı beklemeden kalkar
    public const int BattleCardCost = 5;       // reklamsız kart

    // ==================== DİĞER ====================
    public const int RefillLivesCost = 15;
    public const int MineBoostCost = 20;
    public const int MineBoostHours = 4;
    public const int StreakSaveCost = 10;

    // ==================== ÖDÜLLÜ REKLAM (AdRewards) ====================
    public const int AdSpeedupMinutes = 30;   // inşaatta −30 dk
    public const int DailySpeedupAds = 3;
    public const int AdMineBoostHours = 2;    // madende 2 sa 2x, günde 1
    public const int DailyMineAds = 1;
    public const int DailyChestAds = 1;       // dükkânda günlük sandık

    // ==================== KRALİYET ÜYELİĞİ ====================
    public const int RoyalDailyGems = 10;
    public const float RoyalMineBonus = 0.2f; // +%20 maden; sınırsız can ve %15 kartlar ayrıca

    // ==================== KAZANÇ ====================
    public const int GemsPerNewStar = 1;
    public const int GemsPerFirstBossWin = 10;
    public const int GemsPerMainBaseLevel = 5;
    public const int GemsPerAd = 2;
    public const int DailyGemAds = 3;

    /// <summary>
    /// Kalan süreyi hemen bitirme bedeli: 1 dk altı bedava, sonra 2,2 × √dakika
    /// (30 dk ≈ 12, 1 sa ≈ 17, 6 sa ≈ 42, 24 sa ≈ 83, 48 sa ≈ 118).
    /// </summary>
    public static int FinishCost(double secondsRemaining)
    {
        if (secondsRemaining < FreeFinishSeconds) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(2.2f * Mathf.Sqrt((float)(secondsRemaining / 60d))));
    }

    /// <summary>Eksik altını tamamlamanın elmas bedeli (yukarı yuvarlanır).</summary>
    public static int GoldTopUpCost(int missingGold) =>
        missingGold <= 0 ? 0 : Mathf.CeilToInt(missingGold / (float)GoldPerGem);

    /// <summary>
    /// İksir penceresi içinde kalan iş: pencere boyunca <see cref="PotionSpeed"/> kat hızlı ilerler.
    /// İş pencere içinde biterse kalan/hız, bitmezse kalan − (hız−1) × pencere.
    /// </summary>
    public static long Boosted(long remainingTicks, long windowTicks)
    {
        if (remainingTicks <= 0 || windowTicks <= 0) return Math.Max(0, remainingTicks);
        if (remainingTicks <= windowTicks * PotionSpeed) return remainingTicks / PotionSpeed;
        return remainingTicks - windowTicks * (PotionSpeed - 1);
    }

    public static long Now => DateTime.UtcNow.Ticks;

    public static string FormatRemaining(long untilTicks) =>
        Progression.FormatDuration(Math.Max(0d, new TimeSpan(untilTicks - Now).TotalSeconds));
}
