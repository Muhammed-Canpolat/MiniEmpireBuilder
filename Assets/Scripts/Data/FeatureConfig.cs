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
    public float islandViewSeaMargin = 0.02f;
    /// <summary>Kadraj dünya (22×36) yerine kara hücrelerinin sınırına göre (adacıklar dahil) hesaplanır.</summary>
    public bool islandViewFitLand = true;
    /// <summary>Üst çubuğa ayrılan ekran yüksekliği oranı.</summary>
    public float islandViewTopHudFraction = 0.07f;
    /// <summary>Alt düğmelere ayrılan ekran yüksekliği oranı.</summary>
    public float islandViewBottomHudFraction = 0.08f;

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

    [Header("Ada yapısı: boş başlangıç + ilk yapım bedava (AŞAMA 2 §1)")]
    /// <summary>Yeni ada yalnız kaleyle başlar; her türün ilk yapımı bedava; Okçuluk Alanı menüden kurulur.</summary>
    public bool islandStartFresh = true;
    /// <summary>Yeni adanın başlangıç Odun/Et stoku (eski v6 başlangıcıyla büyük olanı alınır).</summary>
    public int islandStartWood = 60;
    public int islandStartMeat = 0;
    /// <summary>Okçuluk Alanı kurulum bedeli (ilk yapım bedava; adet 1).</summary>
    public int archeryPlaceWood = 80;
    public int archeryPlaceMeat = 0;

    [Header("Ada kadrosu (AŞAMA 2 §2)")]
    /// <summary>Üretim binası kendi işçisiyle gelir; ek yuvalar Ev kapasitesinden dolar; görünen işçi = atanmış işçi.</summary>
    public bool islandStaffV2 = true;
    /// <summary>Her üretim binasının kendi işçi sayısı (yuvaya sayılır).</summary>
    public int staffOwnWorkers = 1;

    [Header("Ada ağaçları ve koyunlar (AŞAMA 2 §3)")]
    /// <summary>Üretim ağacı olmayan ağaçlar altınla kesilir (yer açar, odun verir); koyunlar yalnız Ağıl çevresinde.</summary>
    public bool islandClearableTrees = true;
    /// <summary>Ağaç kesme bedeli (Ana Üs altını) ve verdiği Odun.</summary>
    public int clearTreeGold = 10;
    public int clearTreeWood = 8;
    /// <summary>Tamamlanmış her Ağıl çevresindeki koyun sayısı ve yarıçapı (hücre).</summary>
    public int sheepPerPasture = 3;
    public float sheepRadius = 2.5f;

    [Header("Gözcü kuleleri ve Akın ölçeği (AŞAMA 2 §4)")]
    /// <summary>Elle Akın düşman can/hasar çarpanı = clamp(taban + kule sv toplamı × k + (KK − KKtaban) × m, min, max).</summary>
    public bool islandTowerScaling = true;
    public float akinScaleBase = 0.8f;
    public float akinScalePerTowerLevel = 0.015f;
    public float akinScalePerKeep = 0.02f;
    public int akinScaleKeepBase = 5;
    public float akinScaleMin = 0.7f;
    public float akinScaleMax = 1.15f;

    [Header("AŞAMA 3 · arayüz")]
    /// <summary>Ana Üs ve Ada aynı ResourceTopBar bileşenini (Altın · Elmas · Et · Odun) kullanır.</summary>
    public bool sharedTopBar = true;
    /// <summary>Ada adı: ilk açılışta sorulur, üst çubuktan değiştirilir (3. değişiklikten itibaren elmasla).</summary>
    public bool islandNaming = true;
    /// <summary>Ana Üs radyal alt menüsü (AŞAMA 3 §3F; henüz uygulanmadı).</summary>
    public bool radialDockMenu = true;
    /// <summary>Ada alt düğmeleri küçük, ikonlu, köşelerde (kapalıysa eski 340×66 dock).</summary>
    public bool islandCompactDock = true;
    /// <summary>Ada adı uzunluk sınırları ve ücretsiz değişiklik sayısı.</summary>
    public int islandNameMin = 3;
    public int islandNameMax = 16;
    public int islandNameFreeChanges = 2;
    /// <summary>Ada adının sonraki değişiklik bedeli: (değişiklik sayısı - ücretsiz + 1) × bu adım (elmas).</summary>
    public int islandRenameGemStep = 50;
    /// <summary>Üst çubuk çip sayıları: bu değerden büyük sayılar kısaltılır (12.4K).</summary>
    public int topBarShortFrom = 10000;
    /// <summary>Baskın hapı: saat ikonu + "Sonraki akın: 8 sa 54 dk" (kapalıysa "Sakin · …").</summary>
    public bool raidPillClock = true;
    /// <summary>Kart Atölyesi paneli kısa tuvalde (3:4) ekrana sığacak kadar ölçeklenir.</summary>
    public bool workshopPanelFit = true;
    /// <summary>Ada kompakt dock düğme yüksekliği (referans px, 390×844).</summary>
    public float compactDockHeight = 54f;
    public float compactDockWidth = 124f;

    [Header("Ekonomi (AŞAMA 4)")]
    /// <summary>Etkin altın ekonomisi profili: Resources/Config/ altındaki EconomyConfig asset adı ("Economy_V2" ya da "Economy_Legacy").</summary>
    public string economyProfile = "Economy_V2";

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
