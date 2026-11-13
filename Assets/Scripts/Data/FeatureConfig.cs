using UnityEngine;

/// <summary>
/// Gece çalışması özellik bayrakları ve sayıları (Resources/Config/FeatureConfig.asset; asset yoksa kod varsayılanları).
/// Varsayılan = yeni davranış AÇIK. Bayrağı kapatmak eski davranışa döndürür.
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire Builder/Feature Config", fileName = "FeatureConfig")]
public sealed class FeatureConfig : ScriptableObject
{
    [Header("Ada müziği")]
    /// <summary>Adada sakınken sakin müzik (Ana Üs listesi + rüzgâr/kuş); akında savaş müziği.</summary>
    public bool islandCalmMusic = true;
    /// <summary>Sakin ↔ savaş müziği çapraz geçiş süresi (sn).</summary>
    public float islandMusicCrossfadeSeconds = 1.5f;

    [Header("Ada sabit kamera")]
    public bool islandFixedCamera = true;
    /// <summary>Adanın iki yanındaki deniz payı (adanın genişliğinin oranı, her yan).</summary>
    public float islandViewSeaMargin = 0.05f;
    /// <summary>Üst çubuğa ayrılan ekran yüksekliği oranı.</summary>
    public float islandViewTopHudFraction = 0.07f;
    /// <summary>Alt düğmelere ayrılan ekran yüksekliği oranı.</summary>
    public float islandViewBottomHudFraction = 0.11f;

    [Header("Bulut geçişi (Ana Üs ↔ Ada)")]
    public bool cloudTransition = true;
    /// <summary>Bulutların ekranı kapatma süresi (sn).</summary>
    public float cloudCoverSeconds = 0.7f;
    /// <summary>Bulutların açılma süresi (sn).</summary>
    public float cloudRevealSeconds = 0.9f;
    /// <summary>Yeni sahne yüklendikten sonra kurulum için beklenen kare sayısı.</summary>
    public int cloudPostLoadFrames = 3;
    /// <summary>Güvenlik: bu süreden uzun yüklemede bulutlar yine de açılır (sn).</summary>
    public float cloudMaxWaitSeconds = 8f;

    private static FeatureConfig cached;

    public static FeatureConfig Instance
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<FeatureConfig>("Config/FeatureConfig");
            if (cached == null) cached = CreateInstance<FeatureConfig>();
            return cached;
        }
    }
}
