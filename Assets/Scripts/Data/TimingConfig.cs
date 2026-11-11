using UnityEngine;

/// <summary>
/// Bekleme süreleri tek yerden (Resources/Config/TimingConfig.asset; Inspector'dan değiştirilebilir).
/// Bina yükseltme süreleri (index = hedef seviye, saniye) ve can yenilenme süresi.
/// Asset bulunamazsa koddaki varsayılanlar kullanılır. Süre tablosu değişince <see cref="version"/> artırılır:
/// kayıt yüklenirken devam eden yükseltmelerin kalan süresi yeni/eski oranıyla ölçeklenir (SaveSystem).
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire Builder/Timing Config", fileName = "TimingConfig")]
public sealed class TimingConfig : ScriptableObject
{
    /// <summary>Hedef seviyeye göre yükseltme süresi (saniye). 0 ve 1 kullanılmaz.</summary>
    public int[] upgradeSeconds = { 0, 0, 10, 60, 180, 360, 600, 900, 1800, 3600, 7200, 10800, 14400, 21600, 28800, 43200 };

    /// <summary>Bir canın yenilenme süresi (dakika).</summary>
    public int livesRegenMinutes = 20;

    /// <summary>Süre tablosunun sürümü (kayıtlardaki devam eden işlerin geçişi için).</summary>
    public int version = 2;

    /// <summary>Sürüm 1 (11 Kasım 2026 öncesi) tablosu: eski kayıtların kalan süresini oranlamak için.</summary>
    public static readonly int[] LegacyUpgradeSecondsV1 = { 0, 0, 15, 60, 1800, 3600, 10800, 21600, 43200, 64800, 86400, 108000, 129600, 151200, 172800, 172800 };

    private static TimingConfig cached;

    public static TimingConfig Instance
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<TimingConfig>("Config/TimingConfig");
            if (cached == null) cached = CreateInstance<TimingConfig>();
            return cached;
        }
    }

    public int UpgradeSecondsTo(int targetLevel)
    {
        if (upgradeSeconds == null || upgradeSeconds.Length == 0) return 0;
        return Mathf.Max(0, upgradeSeconds[Mathf.Clamp(targetLevel, 0, upgradeSeconds.Length - 1)]);
    }
}
