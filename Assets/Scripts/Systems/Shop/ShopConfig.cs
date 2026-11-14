using UnityEngine;

/// <summary>
/// 6. oturum (2.4): Dükkân sayıları tek yerde (Resources/Config/ShopConfig.asset; yoksa bu varsayılanlar). Gerekçe ECONOMY.md §12.
/// - Altın kuru Ana Üs seviyesine göre: 1 elmas = (Ana Üs seviyesindeki madenin saatlik üretimi) / <see cref="gemsPerMineHour"/>.
///   Eksik altın tamamlama (GemEconomy.GoldTopUpCost) da aynı kuru kullanır.
/// - Kaynak paketleri: miktar = hedefin yüzdesi (altın: sıradaki Ana Üs bedeli; Et/Odun: ada cüzdan tavanı), elmas = miktar / kur / (1 + bonus).
/// - Gerçek para paketleri: ürün kimlikleri + verilen elmas; fiyat mağazadan (yerel para birimi) gelir, burada fiyat YOK.
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire Builder/Shop Config", fileName = "ShopConfig")]
public sealed class ShopConfig : ScriptableObject
{
    [Header("Kur")]
    [Tooltip("1 saatlik maden üretimi kaç elmas (büyüdükçe altın pahalılaşır). 15 → Ana Üs 1'de 1 elmas = 40 altın.")]
    public float gemsPerMineHour = 15f;
    [Tooltip("En düşük altın kuru (elmas başına).")]
    public int minGoldPerGem = 40;

    [Header("Kaynak paketleri (küçük, orta, büyük)")]
    public float[] goldPackShareOfNextMainBase = { 0.15f, 0.40f, 1.00f };
    public float[] islandPackShareOfWallet = { 0.15f, 0.40f, 1.00f };
    public float[] packBonus = { 0f, 0.10f, 0.25f };
    public int minPackGems = 3;

    [Header("Gerçek para: elmas paketleri (fiyatlar mağazadan)")]
    public string[] gemProductIds = { "gems_small", "gems_medium", "gems_large" };
    public int[] gemProductGems = { 100, 550, 1250 };
    /// <summary>Etikette gösterilen bonus yüzdesi (100 elmaslık taban orana göre).</summary>
    public int[] gemProductBonusPercent = { 0, 10, 25 };
    /// <summary>"En iyi değer" etiketi alan paket (indeks), "Popüler" alan paket.</summary>
    public int bestValueIndex = 2;
    public int popularIndex = 1;

    private static ShopConfig instance;
    public static ShopConfig Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = Resources.Load<ShopConfig>("Config/ShopConfig");
            if (instance == null) { instance = CreateInstance<ShopConfig>(); instance.name = "ShopConfig (varsayılan)"; }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => instance = null;
}
