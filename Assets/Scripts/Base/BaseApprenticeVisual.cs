using UnityEngine;

/// <summary>
/// 4. oturum (2.3): madende TEK figür kalır. Çırak ayrı bir piyon olarak çizilmez; maden görselinin kazma sallayan piyonu (MinePawn)
/// Çırak'tır: rengi Çırak seviyesinin renk kademesi, dokununca Çırak penceresi açılır (dokunma alanı + <see cref="BaseApprenticeTap"/>).
/// Maden yükselince MinePawn yeniden kurulur; bu bağlayıcı yeni piyonu bağlar. Kazma: Tiny Swords "Interact Pickaxe" (adadaki madenciyle
/// aynı kareler), sürekli döngü; altın kıvılcımları <see cref="BaseMineVisual"/>.
/// </summary>
public sealed class BaseApprenticeVisual : MonoBehaviour
{
    private BaseMineVisual mine;
    private Transform bound;
    private int boundLevel = -1;
    private float nextCheck;
    /// <summary>Piyonun gövdesi (dünya birimi; 192 px'lik Tiny Swords karesinde ~40×55 px gövde, kazma salınımı hariç).</summary>
    public static readonly Vector2 BodySize = new Vector2(0.62f, 0.86f);
    public const float BodyOffsetY = -0.08f;

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnApprenticeLevelUp += OnLevel;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnApprenticeLevelUp -= OnLevel;
    }

    private void OnLevel(int level) => boundLevel = -1;

    /// <summary>Şu an Çırak'ı temsil eden piyon (testler ve dokunma).</summary>
    public Transform Figure => bound;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.25f;
        if (mine == null) mine = FindFirstObjectByType<BaseMineVisual>();
        if (mine == null || mine.Miner == null) return;
        int level = GameManager.Instance?.PlayerData?.apprenticeLevel ?? 1;
        if (bound == mine.Miner && boundLevel == level) return;
        bound = mine.Miner;
        boundLevel = level;
        if (bound.GetComponent<BaseApprenticeTap>() == null) bound.gameObject.AddComponent<BaseApprenticeTap>();
        var collider = bound.GetComponent<BoxCollider2D>();
        if (collider == null) collider = bound.gameObject.AddComponent<BoxCollider2D>();
        // Dokunma alanı yalnız piyonun gövdesi: madenin geri kalanına dokunmak madeni açar (altın toplar).
        // Ölçek o anki lossyScale'den değil piyonun hedef dünya ölçeğinden: bina açılış animasyonu sırasında (ölçek ~0) bağlanınca
        // eski kod alanı ~15 kat büyütüyor, madenin tamamı Çırak'a dokunuş sayılıyordu.
        float s = BaseMineVisual.WorkerWorldScale;
        collider.size = new Vector2(BodySize.x / s, BodySize.y / s);
        collider.offset = new Vector2(0f, BodyOffsetY / s);
        var controller = BaseVisualCatalog.Instance?.GetUnitController(BuildingType.GoldMine, level);
        mine.SetWorkerLook(controller, BaseVisualCatalog.ColorIndex(level));
    }
}

/// <summary>Dokunma işareti: bu nesneye dokunmak Çırak penceresini açar.</summary>
public sealed class BaseApprenticeTap : MonoBehaviour { }
