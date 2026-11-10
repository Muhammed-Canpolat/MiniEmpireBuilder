using System;

/// <summary>
/// Can sistemi: en fazla 5 can, 30 dakikada bir 1 can yenilenir (oyun kapalıyken de, UTC saatle).
/// Sefer yenilgisi ya da pes etme 1 can düşürür; can yoksa sefer savaşı başlamaz.
/// Sınırsız can süresi (giriş takvimi 6. gün, paketler) bu sürede can düşmesini durdurur.
/// </summary>
public static class LivesSystem
{
    public const int MaxLives = 5;
    public const int RegenMinutes = 30;
    public const int DailyAdLives = 3;

    public static event Action OnChanged;

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
    private static long Now => DateTime.UtcNow.Ticks;
    private static long Period => TimeSpan.FromMinutes(RegenMinutes).Ticks;
    private static int Today => int.Parse(DateTime.Now.ToString("yyyyMMdd"));

    public static bool Unlimited => Data != null && Data.unlimitedLivesUntilTicks > Now;

    public static TimeSpan UnlimitedRemaining => Unlimited
        ? TimeSpan.FromTicks(Data.unlimitedLivesUntilTicks - Now) : TimeSpan.Zero;

    public static int Lives
    {
        get
        {
            Refresh();
            return Data?.lives ?? MaxLives;
        }
    }

    public static bool CanPlay => Unlimited || Lives > 0;

    /// <summary>Bir sonraki canın gelmesine kalan süre (can doluysa sıfır).</summary>
    public static TimeSpan NextLifeIn
    {
        get
        {
            Refresh();
            var data = Data;
            if (data == null || data.lives >= MaxLives) return TimeSpan.Zero;
            return TimeSpan.FromTicks(Math.Max(0L, data.livesLastRegenTicks + Period - Now));
        }
    }

    /// <summary>Geçen süreye göre canları yeniler. Can doluyken sayaç durur.</summary>
    public static void Refresh()
    {
        var data = Data;
        if (data == null) return;
        long now = Now;
        // Can doluyken ya da saat bozuksa (ileri/geri alınmış) sayaç şimdiden başlar.
        if (data.lives >= MaxLives || data.livesLastRegenTicks <= 0 || data.livesLastRegenTicks > now)
        {
            data.livesLastRegenTicks = now;
            return;
        }
        long gained = (now - data.livesLastRegenTicks) / Period;
        if (gained <= 0) return;
        data.lives = Math.Min(MaxLives, data.lives + (int)gained);
        data.livesLastRegenTicks = data.lives >= MaxLives ? now : data.livesLastRegenTicks + gained * Period;
        OnChanged?.Invoke();
    }

    /// <summary>Sefer yenilgisi / pes etme. Sınırsız can süresinde düşmez.</summary>
    public static void LoseLife()
    {
        var data = Data;
        if (data == null || Unlimited) return;
        Refresh();
        if (data.lives >= MaxLives) data.livesLastRegenTicks = Now;
        data.lives = Math.Max(0, data.lives - 1);
        GameManager.Instance.SaveGame();
        OnChanged?.Invoke();
    }

    public static void AddLives(int amount, bool capAtMax = true)
    {
        var data = Data;
        if (data == null || amount <= 0) return;
        Refresh();
        data.lives = capAtMax ? Math.Min(MaxLives, data.lives + amount) : data.lives + amount;
        if (data.lives >= MaxLives) data.livesLastRegenTicks = Now;
        GameManager.Instance.SaveGame();
        OnChanged?.Invoke();
    }

    /// <summary>Sınırsız can süresi ekler (üst üste eklenir).</summary>
    public static void GrantUnlimited(TimeSpan duration)
    {
        var data = Data;
        if (data == null || duration <= TimeSpan.Zero) return;
        long start = Math.Max(Now, data.unlimitedLivesUntilTicks);
        data.unlimitedLivesUntilTicks = start + duration.Ticks;
        GameManager.Instance.SaveGame();
        OnChanged?.Invoke();
    }

    /// <summary>Elmasla canları doldurma: can eksikse ve sınırsız can sürmüyorsa.</summary>
    public static bool CanRefillWithGems => Data != null && !Unlimited && Lives < MaxLives;

    public static bool RefillWithGems()
    {
        var gm = GameManager.Instance;
        if (!CanRefillWithGems || gm == null || !gm.SpendGems(GemEconomy.RefillLivesCost)) return false;
        AddLives(MaxLives - Lives);
        return true;
    }

    public static int AdLivesToday
    {
        get
        {
            var data = Data;
            return data == null || data.adLivesDay != Today ? 0 : data.adLivesCount;
        }
    }

    public static bool CanWatchAdForLife => Data != null && !Unlimited && Lives < MaxLives && AdLivesToday < DailyAdLives;

    /// <summary>Ödüllü reklam izlendi: +1 can (günde en fazla 3).</summary>
    public static void RewardAdLife()
    {
        var data = Data;
        if (data == null) return;
        if (data.adLivesDay != Today)
        {
            data.adLivesDay = Today;
            data.adLivesCount = 0;
        }
        data.adLivesCount++;
        AddLives(1);
    }

    /// <summary>"12:34" ya da "1 sa 5 dk" biçiminde kısa süre.</summary>
    public static string Format(TimeSpan time)
    {
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours} sa {time.Minutes} dk";
        return $"{time.Minutes:00}:{time.Seconds:00}";
    }
}
