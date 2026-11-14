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
        float s = Mathf.Max(0.01f, bound.lossyScale.x);
        collider.size = new Vector2(0.8f / s, 1.0f / s);
        collider.offset = new Vector2(0f, 0.35f / s);
        var animator = bound.GetComponent<Animator>();
        if (animator == null) return;
        var controller = BaseVisualCatalog.Instance?.GetUnitController(BuildingType.GoldMine, level);
        if (controller != null) animator.runtimeAnimatorController = controller;
        string color = new[] { "Blue", "Yellow", "Red", "Purple", "Black" }[BaseVisualCatalog.ColorIndex(level)];
        animator.Play("Pawn_Interact Pickaxe_" + color, 0, 0f);
    }
}

/// <summary>Dokunma işareti: bu nesneye dokunmak Çırak penceresini açar.</summary>
public sealed class BaseApprenticeTap : MonoBehaviour { }
