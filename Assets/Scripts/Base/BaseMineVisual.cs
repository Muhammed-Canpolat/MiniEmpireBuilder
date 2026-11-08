using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Layered 4×4 mine presentation. Production and storage remain in GameManager.</summary>
public sealed class BaseMineVisual : MonoBehaviour
{
    [Header("Mine art (Inspector overrides are optional)")]
    [SerializeField] private Sprite mainSprite;
    [SerializeField] private Sprite[] levelSprites = new Sprite[8];
    [SerializeField] private Sprite liftBoxSprite;
    [SerializeField] private Sprite tunnelSprite;
    [SerializeField] private Sprite cartSprite;
    [SerializeField] private Sprite greyRockSprite;
    [SerializeField] private Sprite goldVeinSprite;
    [SerializeField] private RuntimeAnimatorController minerController;
    [Tooltip("4×4 footprint içinde sol alt köşeden hücre koordinatı")]
    [SerializeField] private Vector2 minerAnchorCell = new Vector2(1.15f, 0.72f);
    public Vector2 MinerAnchorCell => minerAnchorCell;

    private Transform layers;
    private Transform cartGold;
    private Transform liftBox;
    private SpriteRenderer liftBoxRenderer;
    private Vector3 cartGoldRestScale;
    private GameObject fullBubble;
    private int sortingOrder;
    private int level;
    private Coroutine atmosphere;
    public Color ThemeColor { get; private set; } = Color.white;

    public void Initialize(int buildingLevel, int order)
    {
        level = buildingLevel;
        sortingOrder = order;
        Rebuild();
        atmosphere = StartCoroutine(TunnelAtmosphere());
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnBuildingUpgraded += OnBuildingUpgraded;
        GameManager.Instance.OnGoldMineStoredChanged += OnStorageChanged;
        OnStorageChanged(GameManager.Instance.GoldMineStored,
            GameManager.Instance.GetGoldMineStorageCapacity());
    }

    private void OnDestroy()
    {
        if (atmosphere != null) StopCoroutine(atmosphere);
        if (liftBox != null) liftBox.DOKill();
        if (cartGold != null) cartGold.DOKill();
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnBuildingUpgraded -= OnBuildingUpgraded;
        GameManager.Instance.OnGoldMineStoredChanged -= OnStorageChanged;
    }

    private void OnBuildingUpgraded(BuildingType type, int newLevel)
    {
        if (type != BuildingType.GoldMine) return;
        level = newLevel;
        Rebuild();
        if (GameManager.Instance != null)
            OnStorageChanged(GameManager.Instance.GoldMineStored,
                GameManager.Instance.GetGoldMineStorageCapacity());
    }

    private void Rebuild()
    {
        if (liftBox != null) liftBox.DOKill();
        if (cartGold != null) cartGold.DOKill();
        if (layers != null) Destroy(layers.gameObject);
        layers = new GameObject("MineLayers").transform;
        layers.SetParent(transform, false);
        float unit = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
        var catalog = BaseVisualCatalog.Instance;
        ThemeColor = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(BuildingType.GoldMine, out var mineData)
            ? mineData.VisualForLevel(level)?.color ?? Color.white : Color.white;
        Sprite grey = greyRockSprite ?? (catalog?.goldStones != null && catalog.goldStones.Length > 0
            ? catalog.goldStones[0] : BaseMapManager.MakeCircleSprite(Color.white));
        Sprite gold = goldVeinSprite ?? (catalog?.goldStones != null && catalog.goldStones.Length > 2
            ? catalog.goldStones[2] : BaseMapManager.MakeCircleSprite(Color.white));

        int artLevel = Mathf.Clamp(level, 1, 8);
        Sprite authored = mainSprite != null ? mainSprite
            : levelSprites != null && artLevel - 1 < levelSprites.Length &&
                levelSprites[artLevel - 1] != null ? levelSprites[artLevel - 1]
            : BaseCustomArt.Get("mine_stage_" + artLevel)
                ?? BaseCustomArt.Get("mine_gold_arch");
        if (authored != null)
        {
            MakePart("MineAuthoredArt", authored, new Vector2(0f, 0.02f),
                new Vector2(4.65f, 4.15f), sortingOrder + 1,
                Color.Lerp(Color.white, ThemeColor, 0.08f));
            Sprite boxSprite = liftBoxSprite != null ? liftBoxSprite
                : BaseCustomArt.Get("mine_lift_box");
            if (boxSprite != null)
            {
                liftBoxRenderer = MakePart("MineLiftBox", boxSprite,
                    new Vector2(1.60f, -1.15f), new Vector2(0.75f, 0.90f),
                    sortingOrder + 22, Color.white);
                liftBox = liftBoxRenderer.transform;
                float unitScale = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
                liftBox.DOLocalMoveY(liftBox.localPosition.y + 0.62f * unitScale, 1.35f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
                cartGold = MakePart("CartGoldFill", gold,
                    new Vector2(1.60f, -0.98f), new Vector2(0.24f, 0.17f),
                    sortingOrder + 23, new Color(1f, 0.82f, 0.2f, 0.85f)).transform;
                cartGold.SetParent(liftBox, true);
            }
            else
                cartGold = MakePart("CartGoldFill", gold,
                    new Vector2(1.38f, -1.16f), new Vector2(0.57f, 0.31f),
                    sortingOrder + 22, new Color(1f, 0.82f, 0.2f, 0.88f)).transform;
            cartGoldRestScale = cartGold.localScale;
            CreateWorker(unit, catalog);
            return;
        }

        if (greyRockSprite == null)
            MakePart("MinePlaceholderBody", BaseMapManager.MakeSquareSprite(Color.white),
                new Vector2(0f, 0.05f), new Vector2(3.6f, 2.75f), sortingOrder,
                Color.Lerp(new Color(0.31f, 0.33f, 0.34f), ThemeColor, 0.24f));

        // Stone bank behind the entrance; the gold is embedded in its face.
        for (int i = 0; i < 7; i++)
        {
            float x = -1.55f + (i % 4) * 1.02f;
            float y = 0.3f + (i / 4) * 0.65f;
            MakePart("GreyRockMass", BaseMapManager.MakeCircleSprite(Color.white),
                new Vector2(x, y), new Vector2(1.15f, 1.06f), sortingOrder + i,
                Color.Lerp(new Color(0.29f + i % 3 * 0.035f, 0.31f, 0.32f),
                    ThemeColor, 0.2f));
            MakePart("BackGreyRock", grey, new Vector2(x, y),
                new Vector2(1.85f, 1.45f), sortingOrder + 1 + i,
                Color.Lerp(new Color(0.48f + i % 3 * 0.04f, 0.49f, 0.5f),
                    ThemeColor, 0.18f));
        }
        var entrance = MakePart("TunnelEntrance", tunnelSprite ?? BaseMapManager.MakeCircleSprite(Color.white),
            new Vector2(0f, -0.05f), new Vector2(2.15f, 1.75f), sortingOrder + 10,
            tunnelSprite == null ? new Color(0.045f, 0.04f, 0.035f) : Color.white);
        if (tunnelSprite == null)
            MakePart("TunnelRim", BaseMapManager.MakeCircleSprite(Color.white),
                new Vector2(0f, -0.08f), new Vector2(2.45f, 1.98f), sortingOrder + 9,
                new Color(0.22f, 0.22f, 0.22f));
        entrance.transform.SetAsLastSibling();
        for (int i = 0; i < 5; i++)
            MakePart("EmbeddedGoldVein", gold,
                new Vector2(-1.38f + (i % 3) * 1.25f, 0.52f + (i / 3) * 0.53f),
                new Vector2(0.48f, 0.38f), sortingOrder + 12,
                new Color(0.94f, 0.72f, 0.22f, 0.82f));

        MakePart("MineCart", cartSprite ?? BaseMapManager.MakeSquareSprite(Color.white),
            new Vector2(1.05f, -1.25f), new Vector2(1.35f, 0.72f), sortingOrder + 20,
            cartSprite == null ? new Color(0.28f, 0.22f, 0.16f) : Color.white);
        cartGold = MakePart("CartGold", gold, new Vector2(1.05f, -0.94f),
            new Vector2(0.78f, 0.42f), sortingOrder + 21, new Color(1f, 0.78f, 0.16f))
            .transform;
        cartGoldRestScale = cartGold.localScale;
        MakePart("CartWheelLeft", BaseMapManager.MakeCircleSprite(Color.white),
            new Vector2(0.56f, -1.61f), new Vector2(0.34f, 0.34f), sortingOrder + 22,
            new Color(0.11f, 0.1f, 0.08f));
        MakePart("CartWheelRight", BaseMapManager.MakeCircleSprite(Color.white),
            new Vector2(1.53f, -1.61f), new Vector2(0.34f, 0.34f), sortingOrder + 22,
            new Color(0.11f, 0.1f, 0.08f));

        CreateWorker(unit, catalog);
    }

    private void CreateWorker(float unit, BaseVisualCatalog catalog)
    {
        var worker = new GameObject("MinePawn");
        worker.transform.SetParent(layers, false);
        worker.transform.localPosition = new Vector3((minerAnchorCell.x - 2f) * unit,
            (minerAnchorCell.y - 2f) * unit, -0.02f);
        worker.transform.localScale = Vector3.one * 1.04f * unit;
        worker.AddComponent<SpriteRenderer>().sortingOrder = sortingOrder + 25;
        var animator = worker.AddComponent<Animator>();
        int colorIndex = BaseVisualCatalog.ColorIndex(level);
        animator.runtimeAnimatorController = minerController ??
            (catalog?.pawnControllers != null && colorIndex < catalog.pawnControllers.Length
                ? catalog.pawnControllers[colorIndex] : null);
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (animator.runtimeAnimatorController != null)
            animator.Play("Pawn_Interact Pickaxe_" +
                new[] { "Blue", "Yellow", "Red", "Purple", "Black" }[colorIndex]);
    }

    private SpriteRenderer MakePart(string name, Sprite sprite, Vector2 offset,
        Vector2 size, int order, Color color)
    {
        var part = new GameObject(name);
        part.transform.SetParent(layers, false);
        float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);
        part.transform.localPosition = new Vector3(offset.x / parentScale,
            offset.y / parentScale, -0.01f);
        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        Vector2 bounds = sprite != null ? sprite.bounds.size : Vector2.one;
        part.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, bounds.x * parentScale),
            size.y / Mathf.Max(0.01f, bounds.y * parentScale), 1f);
        return renderer;
    }

    private void OnStorageChanged(int stored, int capacity)
    {
        float fraction = capacity > 0 ? Mathf.Clamp01((float)stored / capacity) : 0f;
        if (liftBoxRenderer != null)
            liftBoxRenderer.color = Color.Lerp(new Color(0.75f, 0.75f, 0.75f),
                Color.white, fraction);
        if (cartGold != null)
        {
            cartGold.gameObject.SetActive(stored > 0);
            cartGold.DOKill();
            cartGold.DOScale(cartGoldRestScale * (0.5f + fraction * 0.5f), 0.25f);
        }
        if (fullBubble == null)
        {
            fullBubble = new GameObject("MineFullBubble");
            fullBubble.transform.SetParent(transform, false);
            float unit = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
            fullBubble.transform.localPosition = new Vector3(1.3f * unit, 1.65f * unit, -0.02f);
            fullBubble.transform.localScale = Vector3.one * 0.14f * unit;
            var text = fullBubble.AddComponent<TextMeshPro>();
            text.text = "!";
            text.fontSize = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.85f, 0.24f);
            text.sortingOrder = sortingOrder + 30;
            fullBubble.transform.DOLocalMoveY(fullBubble.transform.localPosition.y + 0.08f * unit, 0.8f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
        fullBubble.SetActive(capacity > 0 && stored >= capacity);
    }

    private IEnumerator TunnelAtmosphere()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(4f, 6f));
            Vector3 origin = transform.position + new Vector3(0f, -0.1f);
            BaseBuildingVisual.SpawnDust(origin, sortingOrder + 24);
            var spark = new GameObject("MineGoldSpark");
            spark.transform.position = origin + new Vector3(Random.Range(-0.4f, 0.4f), 0.25f);
            spark.transform.localScale = Vector3.one * 0.16f;
            var renderer = spark.AddComponent<SpriteRenderer>();
            renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
            renderer.color = new Color(1f, 0.85f, 0.2f);
            renderer.sortingOrder = sortingOrder + 26;
            spark.transform.DOMoveY(spark.transform.position.y + 0.4f, 0.4f);
            renderer.DOFade(0f, 0.4f).OnComplete(() => Destroy(spark));
        }
    }

    public void FlyGoldToTopBar()
    {
        if (Camera.main == null) return;
        Vector3 target = Camera.main.ScreenToWorldPoint(
            new Vector3(Screen.width * 0.08f, Screen.height * 0.96f, 10f));
        for (int i = 0; i < 5; i++)
        {
            var icon = new GameObject("CollectedGoldFlight");
            icon.transform.position = transform.position + new Vector3(0.8f, -0.8f);
            icon.transform.localScale = Vector3.one * 0.23f;
            var renderer = icon.AddComponent<SpriteRenderer>();
            renderer.sprite = BaseVisualCatalog.Instance?.goldIcon
                ?? BaseMapManager.MakeCircleSprite(Color.white);
            renderer.color = new Color(1f, 0.81f, 0.2f);
            renderer.sortingOrder = 999;
            icon.transform.DOMove(target, 0.55f).SetDelay(i * 0.07f)
                .SetEase(Ease.InQuad).OnComplete(() => Destroy(icon));
        }
    }
}
