using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

/// <summary>
/// 6. oturum (2.4): gerçek parayla elmas. Tek giriş noktası; arka uç Unity IAP v5 (Google Play Billing) ya da test için sahte mağaza.
/// Kurallar:
/// - Fiyat koda yazılmaz: mağazanın yerel para birimindeki fiyatı (<see cref="PriceLabel"/>) gösterilir.
/// - Elmas YALNIZ mağaza "bekleyen sipariş" bildirince ve makbuz doğrulanınca verilir; önce kayda yazılır, sonra sipariş onaylanır
///   (ConfirmPurchase). Uygulama arada kapanırsa sipariş onaylanmamış kalır, sonraki açılışta mağaza yeniden bildirir ve işlenir.
/// - Çift verme yok: işlem kimlikleri kayıttaki deftere (<see cref="PlayerSaveData.iapLedger"/>) yazılır; aynı işlem ikinci kez gelirse
///   elmas verilmeden yalnız onaylanır.
/// - Makbuz: Android'de Unity IAP yerel doğrulaması (CrossPlatformValidator + GooglePlayTangle). Tangle üretilmemişse yayın derlemesinde
///   elmas VERİLMEZ (PLAY_RELEASE.md adımı), Development derlemesinde uyarıyla verilir.
/// - Editör/testler: sahte mağaza (<see cref="UseFakeStore"/>).
/// </summary>
public sealed class IapService : MonoBehaviour
{
    public interface IBackend
    {
        bool Ready { get; }
        string PriceLabel(string productId);
        void Initialize(IapService owner, IReadOnlyList<string> productIds);
        void Purchase(string productId);
        void Confirm(object token);
        /// <summary>Makbuz yerel doğrulaması; doğrulanamıyorsa false.</summary>
        bool Validate(string productId, string receipt, out string error);
    }

    public static bool UseFakeStore => Application.isEditor || ForceFake;
    /// <summary>Testler / Development: cihazda da sahte mağaza.</summary>
    public static bool ForceFake;

    private static IapService instance;
    public static IapService Instance
    {
        get
        {
            if (instance != null) return instance;
            var go = new GameObject("IapService");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<IapService>();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        // Açılışta bağlan: önceki oturumda yarım kalan (onaylanmamış) satın almalar hemen işlenir.
        if (!Application.isBatchMode) _ = Instance;
    }

    public IBackend Backend { get; private set; }
    public bool Ready => Backend != null && Backend.Ready;
    public string LastMessage { get; private set; } = "";
    public string Busy { get; private set; }

    public event Action Changed;
    /// <summary>(ürün, başarı, mesaj)</summary>
    public event Action<string, bool, string> PurchaseFinished;

    private readonly Queue<(string id, string tx, string receipt, object token)> waiting = new Queue<(string, string, string, object)>();

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Backend = UseFakeStore ? new FakeStoreBackend() : (IBackend)new UnityIapBackend();
        Backend.Initialize(this, ShopConfig.Instance.gemProductIds);
    }

    /// <summary>Testler: arka ucu değiştirir (ör. yeni bir sahte mağaza = "uygulama yeniden açıldı").</summary>
    public void UseBackend(IBackend backend)
    {
        Backend = backend;
        Backend.Initialize(this, ShopConfig.Instance.gemProductIds);
        Changed?.Invoke();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    public static int GemsFor(string productId)
    {
        var cfg = ShopConfig.Instance;
        int i = Array.IndexOf(cfg.gemProductIds, productId);
        return i >= 0 && i < cfg.gemProductGems.Length ? cfg.gemProductGems[i] : 0;
    }

    public string PriceLabel(string productId) => Ready ? Backend.PriceLabel(productId) : "…";

    public bool Buy(string productId)
    {
        if (!Ready || !string.IsNullOrEmpty(Busy) || GemsFor(productId) <= 0) return false;
        Busy = productId;
        Changed?.Invoke();
        Backend.Purchase(productId);
        return true;
    }

    // ==================== arka uçtan gelenler ====================

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>Mağaza bekleyen (ödenmiş, onaylanmamış) bir sipariş bildirdi.</summary>
    public void HandlePending(string productId, string transactionId, string receipt, object token)
    {
        waiting.Enqueue((productId, transactionId, receipt, token));
        ProcessWaiting();
    }

    public void HandleFailure(string productId, string reason)
    {
        if (Busy == productId || string.IsNullOrEmpty(productId)) Busy = null;
        LastMessage = reason;
        PurchaseFinished?.Invoke(productId, false, reason);
        Changed?.Invoke();
    }

    private void Update() { if (waiting.Count > 0) ProcessWaiting(); }

    /// <summary>Kayıt yüklenmeden gelen siparişler kuyrukta bekler (açılışta mağaza kayıttan önce bağlanabilir).</summary>
    private void ProcessWaiting()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.PlayerData == null) return;
        while (waiting.Count > 0)
        {
            var (id, tx, receipt, token) = waiting.Dequeue();
            Grant(gm, id, tx, receipt, token);
        }
    }

    private void Grant(GameManager gm, string productId, string transactionId, string receipt, object token)
    {
        var data = gm.PlayerData;
        if (data.iapLedger == null) data.iapLedger = new List<string>();
        string key = string.IsNullOrEmpty(transactionId) ? null : productId + "|" + transactionId;
        if (key != null && data.iapLedger.Contains(key))
        {
            // Daha önce verildi (onay yarıda kalmış): yalnız onayla.
            Backend.Confirm(token);
            if (Busy == productId) Busy = null;
            Changed?.Invoke();
            return;
        }
        int gems = GemsFor(productId);
        if (gems <= 0 || key == null)
        {
            HandleFailure(productId, L.T("Bilinmeyen ürün", "Unknown product"));
            return;
        }
        if (!Backend.Validate(productId, receipt, out string error))
        {
            // Doğrulanamayan makbuz: elmas verilmez, sipariş onaylanmaz (mağaza 3 günde iade eder).
            Debug.LogWarning("[IAP] Makbuz doğrulanamadı: " + error);
            HandleFailure(productId, L.T("Satın alma doğrulanamadı", "Purchase could not be verified"));
            return;
        }
        // Önce kayda (elmas + defter), sonra mağazaya onay: arada kapanırsa yeniden bildirimde defter çift vermeyi önler.
        gm.AddGems(gems, false);
        data.iapLedger.Add(key);
        if (data.iapLedger.Count > 300) data.iapLedger.RemoveRange(0, data.iapLedger.Count - 300);
        gm.SaveGame();
        Backend.Confirm(token);
        if (Busy == productId) Busy = null;
        LastMessage = L.T($"+{gems} elmas", $"+{gems} gems");
        PurchaseFinished?.Invoke(productId, true, LastMessage);
        Changed?.Invoke();
    }
}

/// <summary>
/// Editör ve testler için sahte mağaza: anlık "ödeme", sabit test fiyat etiketleri. Onaylanmayan siparişleri PlayerPrefs'te tutar ve
/// yeniden başlatmada (yeni örnek) tekrar bildirir — gerçek mağazanın "yarıda kalan satın alma" davranışı.
/// </summary>
public sealed class FakeStoreBackend : IapService.IBackend
{
    private const string PendingKey = "MEB_FakeIap_Pending";
    private IapService owner;
    private bool ready;
    /// <summary>Test: sıradaki satın alma "ödenir" ama uygulama bildirimden önce kapanmış gibi işlenmez.</summary>
    public bool InterruptNext;
    /// <summary>Test: sıradaki satın alma iptal edilir.</summary>
    public bool CancelNext;
    /// <summary>Test: sıradaki makbuz doğrulamadan geçmez.</summary>
    public bool InvalidNext;
    public int Confirmed { get; private set; }

    public bool Ready => ready;

    public string PriceLabel(string productId)
    {
        // Sahte mağaza kataloğu (yalnız test etiketi; gerçek fiyat Play Console'dan gelir).
        int gems = IapService.GemsFor(productId);
        return gems <= 0 ? "-" : L.T("TEST ", "TEST ") + (gems <= 100 ? "₺34,99" : gems <= 550 ? "₺149,99" : "₺299,99");
    }

    public void Initialize(IapService service, IReadOnlyList<string> productIds)
    {
        owner = service;
        ready = true;
        // Yarıda kalanları yeniden bildir.
        foreach (var p in LoadPending()) owner.HandlePending(p.Split('|')[0], p.Split('|')[1], "fake-receipt", p);
        owner.NotifyChanged();
    }

    public void Purchase(string productId)
    {
        if (CancelNext) { CancelNext = false; owner.HandleFailure(productId, L.T("Satın alma iptal edildi", "Purchase cancelled")); return; }
        string tx = "fake-" + Guid.NewGuid().ToString("N").Substring(0, 12);
        string token = productId + "|" + tx;
        var pending = LoadPending();
        pending.Add(token);
        SavePending(pending);
        if (InterruptNext) { InterruptNext = false; return; }   // ödendi ama uygulama kapandı
        owner.HandlePending(productId, tx, InvalidNext ? "bad" : "fake-receipt", token);
        InvalidNext = false;
    }

    public void Confirm(object token)
    {
        var pending = LoadPending();
        if (pending.Remove(token as string)) Confirmed++;
        SavePending(pending);
    }

    public bool Validate(string productId, string receipt, out string error)
    {
        error = receipt == "fake-receipt" ? "" : "sahte makbuz geçersiz";
        return receipt == "fake-receipt";
    }

    public static List<string> LoadPending()
    {
        string raw = PlayerPrefs.GetString(PendingKey, "");
        return raw.Length == 0 ? new List<string>() : raw.Split(';').Where(s => s.Length > 0).ToList();
    }

    private static void SavePending(List<string> list)
    {
        PlayerPrefs.SetString(PendingKey, string.Join(";", list));
        PlayerPrefs.Save();
    }

    public static void ClearPending() { PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); }
}

/// <summary>Unity IAP v5 (StoreController) arka ucu — Google Play Billing.</summary>
public sealed class UnityIapBackend : IapService.IBackend
{
    private StoreController store;
    private IapService owner;
    private bool ready;

    public bool Ready => ready;

    public string PriceLabel(string productId)
    {
        var product = store?.GetProductById(productId);
        return product?.metadata != null && !string.IsNullOrEmpty(product.metadata.localizedPriceString)
            ? product.metadata.localizedPriceString : "…";
    }

    public async void Initialize(IapService service, IReadOnlyList<string> productIds)
    {
        owner = service;
        try
        {
            store = UnityIAPServices.StoreController();
            // Tüm olaylara Connect'ten ÖNCE abone ol: önceki oturumdan kalan siparişler hemen gelebilir.
            store.OnPurchasePending += order =>
                owner.HandlePending(ProductOf(order), order.Info.TransactionID, order.Info.Receipt, order);
            store.OnPurchaseConfirmed += order =>
            {
                if (order is FailedOrder failed) owner.HandleFailure(ProductOf(order), failed.FailureReason + " " + failed.Details);
            };
            store.OnPurchaseFailed += failed => owner.HandleFailure(ProductOf(failed), FailText(failed.FailureReason));
            store.OnPurchaseDeferred += deferred =>
                owner.HandleFailure(ProductOf(deferred), L.T("Ödeme onay bekliyor; onaylanınca elmaslar gelir", "Payment pending; gems arrive once approved"));
            store.OnPurchasesFetched += _ => { };
            store.OnPurchasesFetchFailed += f => Debug.LogWarning("[IAP] Satın almalar alınamadı: " + f.Message);
            store.OnProductsFetched += _ =>
            {
                ready = true;
                owner.NotifyChanged();
                store.FetchPurchases();   // onaylanmamış siparişler OnPurchasePending ile yeniden gelir
            };
            store.OnProductsFetchFailed += f => Debug.LogWarning("[IAP] Ürünler alınamadı: " + f.FailureReason);
            store.OnStoreDisconnected += d => { ready = false; owner.NotifyChanged(); Debug.LogWarning("[IAP] Mağaza bağlantısı yok: " + d.Message); };
            store.OnStoreConnected += () =>
                store.FetchProducts(productIds.Select(id => new ProductDefinition(id, ProductType.Consumable)).ToList());
            await store.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[IAP] Başlatılamadı: " + e.Message);
            ready = false;
            owner.NotifyChanged();
        }
    }

    private static string FailText(PurchaseFailureReason reason) =>
        reason == PurchaseFailureReason.UserCancelled ? L.T("Satın alma iptal edildi", "Purchase cancelled")
        : L.T("Satın alma tamamlanamadı", "Purchase failed") + " (" + reason + ")";

    private static string ProductOf(Order order)
    {
        var item = order?.CartOrdered?.Items()?.FirstOrDefault();
        return item?.Product?.definition?.id ?? "";
    }

    public void Purchase(string productId)
    {
        var product = store?.GetProductById(productId);
        if (product == null || !product.availableToPurchase)
        {
            owner.HandleFailure(productId, L.T("Ürün şu an satın alınamıyor", "Product unavailable"));
            return;
        }
        store.PurchaseProduct(product);
    }

    public void Confirm(object token)
    {
        if (token is PendingOrder pending) store?.ConfirmPurchase(pending);
    }

    public bool Validate(string productId, string receipt, out string error)
    {
        error = "";
        if (Application.platform != RuntimePlatform.Android) return true;   // Apple StoreKit 2: yerel doğrulama yok (sunucu tarafı)
        var tangle = Type.GetType("UnityEngine.Purchasing.Security.GooglePlayTangle, Assembly-CSharp");
        if (tangle == null)
        {
            error = "GooglePlayTangle yok (Services > In-App Purchasing > Receipt Validation Obfuscator)";
            return Debug.isDebugBuild;   // yayında doğrulamasız elmas verilmez
        }
        try
        {
            var key = (byte[])tangle.GetMethod("Data").Invoke(null, null);
            var validator = new CrossPlatformValidator(key, Application.identifier);
            var result = validator.Validate(receipt);
            foreach (var r in result)
                if (r.productID == productId) return true;
            error = "makbuzda ürün yok";
            return false;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
