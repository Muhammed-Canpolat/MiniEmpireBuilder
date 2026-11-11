using System;

/// <summary>
/// Oyunun "şimdi"si: telefon saati geri alınınca geri gitmez (kayıtta en son görülen saat tutulur).
/// Günlük sınırlar (reklam hakları, günlük sandık, takvim, görevler, İstila haftası) bunu kullanır;
/// böylece "saati ileri al → topla → geri al → yine topla" döngüsü kapanır.
/// Saat 7 günden fazla geride kalırsa (telefon saati yanlış ayarlanıp düzeltildi) sınır sıfırlanır,
/// oyuncu kilitlenmez. Bildirim zamanlaması gerçek saati kullanmaya devam eder.
/// </summary>
public static class GameClock
{
    public const int MaxRollbackDays = 7;

    public static DateTime UtcNow
    {
        get
        {
            var now = DateTime.UtcNow;
            var gm = GameManager.Instance;
            var data = gm != null ? gm.PlayerData : null;
            if (data == null) return now;
            long seen = data.clockHighWaterTicks;
            if (now.Ticks >= seen || seen - now.Ticks > TimeSpan.FromDays(MaxRollbackDays).Ticks)
            {
                data.clockHighWaterTicks = now.Ticks;
                return now;
            }
            return new DateTime(seen, DateTimeKind.Utc);
        }
    }

    /// <summary>Yerel saat (gün sınırları yerel gece yarısında döner).</summary>
    public static DateTime LocalNow => UtcNow.ToLocalTime();

    /// <summary>yyyyMMdd biçiminde bugünün kimliği.</summary>
    public static int TodayId
    {
        get
        {
            var d = LocalNow;
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }
    }
}
