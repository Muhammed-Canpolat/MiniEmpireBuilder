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
    public float islandViewBottomHudFraction = 0.10f;   // 13 Kasım: radyal ana düğme (alt-orta ~%12) için 0,08 → 0,10

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
    /// <summary>
    /// 6. oturum (3.2): ada binalarının ALTIN kurulum bedeli (index = IslandLayout.BuildingKind: Kale, Ev, Oduncu, Ağıl, Okçuluk, Kule,
    /// Kışla, Manastır). Oduncu ve Ağıl ucuz (ilk üretim zinciri; Odun/Et henüz yok), diğerleri pahalı. Gerekçe ECONOMY.md §13.
    /// "İlk yapım bedava" kuralı kalktı; yalnız öğreticideki ilk Okçuluk Alanı ücretsiz.
    /// </summary>
    public int[] islandBuildGold = { 0, 250, 100, 100, 0, 350, 600, 900 };

    /// <summary>6. oturum (AŞAMA 5): savaş kartı açma reklamı günlük hakkı (savaş başına en çok 3 kart × 3 savaş).</summary>
    public int dailyBattleCardAds = 9;

    [Header("Temizleme (6. oturum 4.2): süre öğenin büyüklüğüne göre, ödüller")]
    public float clearSecondsBush = 3f;
    public float clearSecondsStump = 4f;
    public float clearSecondsRock = 5f;
    public float clearSecondsTree = 10f;
    /// <summary>Ana Üs doğa temizliği ödülleri (ada temizliği kendi değerlerini kullanır).</summary>
    public int clearRewardTreeWood = 12;
    public int clearRewardStumpWood = 6;
    public int clearRewardBushMeat = 2;
    public int clearRewardRockGold = 8;
    /// <summary>Aynı türün ikinci ve sonraki kurulumlarında altına ek olarak Odun/Et liste bedeli (Progression.Island.PlaceCost) istenir.</summary>
    public bool islandRepeatNeedsResources = true;

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
    /// <summary>
    /// 6. oturum (3.4): ada seviyeleri 1'den başlıyor. Kıyı Kalesi akinScaleKeepBase'in ALTINDA her seviye için düşman gücü bu kadar daha düşer
    /// (Kale 1: 0,8 − 4×0,075 = 0,5). Gerekçe: Okçu Sv1 hasarı Sv5'in 0,625'i → düşman canı da aynı oranda (0,5/12 = 0,8/19,2): ilk akınlar,
    /// eski "Kale 5'le açılan" ada kadar zor; kale canı düşük olduğu için tekne sayısı/dalga aynı kalır.
    /// </summary>
    public float akinScalePerKeepBelow = 0.075f;
    public float akinScaleMin = 0.5f;
    public float akinScaleMax = 1.15f;

    [Header("AŞAMA 3 · arayüz")]
    /// <summary>Ana Üs ve Ada aynı ResourceTopBar bileşenini (Altın · Elmas · Et · Odun) kullanır.</summary>
    public bool sharedTopBar = true;
    /// <summary>Ana Üs üst barı MiniEmpireUI paketiyle (D106; yalnız görünüm). Kapalıysa eski koyu hap bar.</summary>
    public bool miniEmpireTopBar = true;
    /// <summary>Ana Üs alt barı MiniEmpireUI paketiyle (D107; yalnız görünüm). Kapalıysa radyal menü (radialDockMenu) / eski dock.</summary>
    public bool miniEmpireBottomBar = true;
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
    /// <summary>
    /// 3. oturum (2.2): üst bar öğe sırası (soldan sağa; kimlikler: gold, wood, meat, gem, builder). Ana Üs'te ilk yarı armanın
    /// solunda, kalanı sağında; Ada'da ANA ÜS düğmesi ve ada adından sonra. Listede olmayan öğe gizlenir. Tek bileşen:
    /// <see cref="ResourceTopBar"/> (aynı veri kaynağı); sahneler yalnız bu listeyle ayrışır.
    /// </summary>
    public string[] baseTopBarOrder = { "gold", "wood", "meat", "gem", "builder" };
    public string[] islandTopBarOrder = { "gold", "wood", "meat", "gem" };
    /// <summary>Baskın hapı: saat ikonu + "Sonraki akın: 8 sa 54 dk" (kapalıysa "Sakin · …").</summary>
    public bool raidPillClock = true;
    /// <summary>Kart Atölyesi paneli kısa tuvalde (3:4) ekrana sığacak kadar ölçeklenir.</summary>
    public bool workshopPanelFit = true;
    /// <summary>Ada kompakt dock düğme yüksekliği (referans px, 390×844).</summary>
    public float compactDockHeight = 54f;
    public float compactDockWidth = 124f;

    [Header("13 Kasım geri bildirimi")]
    /// <summary>Adada Ana Üs'ün radyal menüsü (İNŞA ET · AKIN · KADRO); kapalıysa kompakt dock + sol üst KADRO.</summary>
    public bool islandRadialMenu = true;
    /// <summary>Akın sayacı küçük hap: kum saati + "14sa 48dk"; dokununca açıklama (kapalıysa uzun "Sonraki akın: …").</summary>
    public bool raidPillCompact = true;
    /// <summary>Adadan başlatılan Akın'ın sonuç ekranında "ANA ÜS" yerine "ADAYA DÖN" (bulutlu geçişle ada görünümüne).</summary>
    public bool raidResultReturnToIsland = true;

    [Header("Ekonomi (AŞAMA 4)")]
    /// <summary>Etkin altın ekonomisi profili: Resources/Config/ altındaki EconomyConfig asset adı ("Economy_V2" ya da "Economy_Legacy").</summary>
    public string economyProfile = "Economy_V2";

    [Header("Ana Üs (5. oturum)")]
    /// <summary>
    /// İnşa Et menüsündeki "ÖNERİLEN DÜZEN" düğmesi ve ona bağlı otomatik yerleşim (BaseMapManager.ApplyRecommendedLayout). 5. oturum (2.4):
    /// kullanıcı kararıyla KAPALI — kod silinmedi; true yapılırsa düğme geri gelir. (Varsayılanı false: asset'te alan yoksa da kapalı.)
    /// </summary>
    public bool recommendedLayout = false;

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
