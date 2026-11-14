using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Tiny Swords parçalarından kurulan 4×4 altın madeni: altın kayalar, seviye rengine göre çatılı maden evi,
/// depo doldukça büyüyen altın yığını ve kazma sallayan madenci. Üretim/depo GameManager'da kalır.
/// </summary>
public sealed class BaseMineVisual : MonoBehaviour
{
    [Header("Maden görselleri (boş bırakılırsa BaseVisualCatalog kullanılır)")]
    [SerializeField] private Sprite[] houseSprites = new Sprite[5];
    [SerializeField] private RuntimeAnimatorController minerController;
    [Tooltip("4×4 footprint içinde sol alt köşeden hücre koordinatı")]
    [SerializeField] private Vector2 minerAnchorCell = new Vector2(2.05f, 1.8f);
    public Vector2 MinerAnchorCell => minerAnchorCell;

    public const int MaxPileNuggets = 6;

    // Kaya yerleşimi (yerel birim, merkez 0,0): arkadakiler önce, en büyük kaya ortada.
    private static readonly (int sprite, Vector2 pos, float size, int minTier)[] StoneLayout =
    {
        (4, new Vector2(-1.55f, 1.30f), 1.05f, 2),
        (1, new Vector2(-0.10f, 1.35f), 0.95f, 1),
        (5, new Vector2(-0.85f, 0.70f), 1.95f, 0),
        (3, new Vector2(-1.60f, 0.05f), 1.15f, 0),
        (2, new Vector2(-0.95f, -0.55f), 0.90f, 3),
        (0, new Vector2(0.05f, 0.25f), 0.80f, 4),
    };

    private static readonly Vector2[] PileLayout =
    {
        new Vector2(0.75f, -1.30f), new Vector2(1.10f, -1.32f), new Vector2(1.45f, -1.28f),
        new Vector2(0.92f, -1.12f), new Vector2(1.27f, -1.10f), new Vector2(1.10f, -0.93f),
    };

    private Transform layers;
    private readonly List<SpriteRenderer> stones = new List<SpriteRenderer>();
    private readonly List<Transform> pile = new List<Transform>();
    private GameObject fullBubble;
    private Sequence fullGlow;
    private int sortingOrder;
    private int level;
    private int shownNuggets = -1;
    private Coroutine atmosphere;
    public Color ThemeColor { get; private set; } = Color.white;
    /// <summary>4. oturum (2.3): madendeki tek figür (Çırak). Maden yükselince yeniden kurulur; Çırak bağlayıcısı her seferinde bağlar.</summary>
    public Transform Miner { get; private set; }
    /// <summary>Çırak şu an kayayı kazıyor (kıvılcımlar yalnız bu sırada çıkar).</summary>
    public bool MinerDigging { get; private set; }
    /// <summary>Çırak şu an yürüyor (altını yığına taşıyor ya da başka kayaya gidiyor).</summary>
    public bool MinerWalking { get; private set; }

    // Çırağın iş döngüsü (yerel birim, maden merkezi 0,0): iki kazı noktası (ikisi de sola, kayaya dönük) ve yığın başı.
    private static readonly Vector2 SecondDigSpot = new Vector2(-0.72f, -0.78f);
    private static readonly Vector2 DropSpot = new Vector2(0.40f, -1.00f);
    private const float WorkerSpeed = 0.85f;
    /// <summary>Çırak piyonunun dünya ölçeği (dokunma alanı da buna göre).</summary>
    public const float WorkerWorldScale = 1.04f;
    private Coroutine work;
    private Animator workerAnimator;
    private SpriteRenderer workerRenderer;
    private Vector2 workerPos;
    private string workerState = "Interact Pickaxe";
    private string workerColor = "Blue";

    public void Initialize(int buildingLevel, int order)
    {
        level = buildingLevel;
        sortingOrder = order;
        Rebuild();
        atmosphere = StartCoroutine(MineAtmosphere());
        MineStorageGauge.Attach(transform);   // 6. oturum (2.3): depo göstergesi + DOLU uyarısı
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnBuildingUpgraded += OnBuildingUpgraded;
        GameManager.Instance.OnGoldMineStoredChanged += OnStorageChanged;
        OnStorageChanged(GameManager.Instance.GoldMineStored,
            GameManager.Instance.GetGoldMineStorageCapacity());
    }

    private void OnDestroy()
    {
        if (atmosphere != null) StopCoroutine(atmosphere);
        if (work != null) StopCoroutine(work);
        KillTweens();
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnBuildingUpgraded -= OnBuildingUpgraded;
        GameManager.Instance.OnGoldMineStoredChanged -= OnStorageChanged;
    }

    private void KillTweens()
    {
        fullGlow?.Kill();
        fullGlow = null;
        foreach (var nugget in pile) if (nugget != null) nugget.DOKill();
        foreach (var stone in stones) if (stone != null) stone.DOKill();
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

    /// <summary>Seviyeye göre görünen altın kaya sayısı: kademe başına bir kaya daha (2–6).</summary>
    public static int StoneCountForLevel(int buildingLevel)
    {
        int tier = BaseVisualCatalog.ColorIndex(Mathf.Clamp(buildingLevel, 1, Progression.MaxLevel));
        int count = 0;
        foreach (var stone in StoneLayout) if (stone.minTier <= tier) count++;
        return count;
    }

    /// <summary>Depo doluluğuna göre yığındaki külçe sayısı (0–6).</summary>
    public static int NuggetsForFill(int stored, int capacity)
    {
        if (stored <= 0 || capacity <= 0) return 0;
        float fraction = Mathf.Clamp01((float)stored / capacity);
        return Mathf.Clamp(Mathf.CeilToInt(fraction * MaxPileNuggets), 1, MaxPileNuggets);
    }

    private void Rebuild()
    {
        KillTweens();
        stones.Clear();
        pile.Clear();
        shownNuggets = -1;
        if (layers != null) Destroy(layers.gameObject);
        layers = new GameObject("MineLayers").transform;
        layers.SetParent(transform, false);
        var catalog = BaseVisualCatalog.Instance;
        int tier = BaseVisualCatalog.ColorIndex(Mathf.Clamp(level, 1, Progression.MaxLevel));
        ThemeColor = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(BuildingType.GoldMine, out var mineData)
            ? mineData.VisualForLevel(level)?.color ?? Color.white : Color.white;

        MakePart("MineGroundShadow", BaseMapManager.MakeCircleSprite(Color.white),
            new Vector2(-0.35f, -0.05f), new Vector2(3.9f, 2.9f), sortingOrder,
            new Color(0.18f, 0.14f, 0.08f, 0.22f), false);

        var stoneSprites = catalog?.goldStones;
        for (int i = 0; i < StoneLayout.Length; i++)
        {
            var layout = StoneLayout[i];
            if (layout.minTier > tier) continue;
            Sprite sprite = stoneSprites != null && stoneSprites.Length > 0
                ? stoneSprites[Mathf.Min(layout.sprite, stoneSprites.Length - 1)]
                : BaseMapManager.MakeCircleSprite(new Color(0.95f, 0.78f, 0.25f));
            stones.Add(MakePart("MineGoldStone_" + i, sprite, layout.pos,
                Vector2.one * layout.size, sortingOrder + 2 + DepthOrder(layout.pos.y), Color.white, true));
        }

        Sprite house = houseSprites != null && tier < houseSprites.Length && houseSprites[tier] != null
            ? houseSprites[tier]
            : catalog?.mineHouses != null && tier < catalog.mineHouses.Length ? catalog.mineHouses[tier] : null;
        if (house != null)
            MakePart("MineHouse", house, new Vector2(1.05f, 0.40f), new Vector2(1.55f, 2.32f),
                sortingOrder + 2 + DepthOrder(-0.75f), Color.white, true);

        CreateWorker(catalog, tier);
    }

    /// <summary>Aşağıdaki (öndeki) parça üstte çizilir.</summary>
    private static int DepthOrder(float localY) => Mathf.RoundToInt((2f - localY) * 4f);

    private void CreateWorker(BaseVisualCatalog catalog, int colorIndex)
    {
        float unit = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
        var worker = new GameObject("MinePawn");
        Miner = worker.transform;
        worker.transform.SetParent(layers, false);
        worker.transform.localScale = Vector3.one * WorkerWorldScale * unit;
        workerRenderer = worker.AddComponent<SpriteRenderer>();
        workerRenderer.flipX = true; // kayaya (sola) dönük kazar
        workerAnimator = worker.AddComponent<Animator>();
        workerAnimator.runtimeAnimatorController = minerController ??
            (catalog?.pawnControllers != null && colorIndex < catalog.pawnControllers.Length
                ? catalog.pawnControllers[colorIndex] : null);
        workerAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        workerColor = ColorName(colorIndex);
        workerState = null;
        PlaceWorker(FirstDigSpot);
        PlayWorker("Interact Pickaxe");
        if (work != null) StopCoroutine(work);
        work = StartCoroutine(WorkLoop(worker.transform));
    }

    private static string ColorName(int index) => new[] { "Blue", "Yellow", "Red", "Purple", "Black" }[Mathf.Clamp(index, 0, 4)];
    private Vector2 FirstDigSpot => new Vector2(minerAnchorCell.x - 2f, minerAnchorCell.y - 2f);

    /// <summary>Çırak bağlayıcısı (seviye rengi) burayı çağırır: denetleyici değişir, o anki iş animasyonu yeni renkle sürer.</summary>
    public void SetWorkerLook(RuntimeAnimatorController controller, int colorIndex)
    {
        if (workerAnimator == null) return;
        if (controller != null) workerAnimator.runtimeAnimatorController = controller;
        workerColor = ColorName(colorIndex);
        string state = workerState ?? "Interact Pickaxe";
        workerState = null;
        PlayWorker(state);
    }

    private void PlayWorker(string state)
    {
        if (workerAnimator == null || state == workerState) return;
        workerState = state;
        if (workerAnimator.runtimeAnimatorController != null)
            workerAnimator.Play("Pawn_" + state + "_" + workerColor, 0, 0f);
    }

    private void PlaceWorker(Vector2 local)
    {
        workerPos = local;
        if (Miner == null) return;
        float unit = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
        Miner.localPosition = new Vector3(local.x * unit, local.y * unit, -0.02f);
        if (workerRenderer != null) workerRenderer.sortingOrder = sortingOrder + 2 + DepthOrder(local.y) + 1;
    }

    /// <summary>
    /// Çırak yerinde durmaz: kayayı kazar (kıvılcımlar), altını sırtlayıp yığına yürür ve bırakır, kazmasını alıp sırayla
    /// öbür kayaya yürür. Hepsi Tiny Swords piyon kareleri: Interact/Run/Idle Pickaxe, Run/Idle Gold.
    /// </summary>
    private IEnumerator WorkLoop(Transform figure)
    {
        int spot = 0;
        bool first = true;
        while (figure != null)
        {
            if (workerRenderer != null) workerRenderer.flipX = true;
            MinerDigging = true;
            PlayWorker("Interact Pickaxe");
            yield return new WaitForSeconds(first ? Random.Range(4.5f, 6f) : Random.Range(3.5f, 5.5f));
            first = false;
            MinerDigging = false;
            yield return WalkWorker(DropSpot, "Run Gold");
            if (workerRenderer != null) workerRenderer.flipX = false;
            PlayWorker("Idle Gold");
            yield return new WaitForSeconds(0.45f);
            DropGoldOnPile();
            PlayWorker("Idle Pickaxe");
            yield return new WaitForSeconds(Random.Range(0.5f, 1f));
            spot = 1 - spot;
            yield return WalkWorker(spot == 0 ? FirstDigSpot : SecondDigSpot, "Run Pickaxe");
        }
    }

    private IEnumerator WalkWorker(Vector2 target, string state)
    {
        MinerWalking = true;
        PlayWorker(state);
        if (workerRenderer != null && Mathf.Abs(target.x - workerPos.x) > 0.02f) workerRenderer.flipX = target.x < workerPos.x;
        while (Miner != null && (target - workerPos).sqrMagnitude > 0.0004f)
        {
            PlaceWorker(Vector2.MoveTowards(workerPos, target, WorkerSpeed * Time.deltaTime));
            yield return null;
        }
        PlaceWorker(target);
        MinerWalking = false;
    }

    /// <summary>Taşınan altın yığına düşer: küçük külçe sekip söner, yığının üstü hafifçe zıplar.</summary>
    private void DropGoldOnPile()
    {
        if (Miner == null) return;
        var drop = new GameObject("MineGoldDrop");
        drop.transform.position = Miner.position + new Vector3(0.35f, 0.15f, -0.03f);
        drop.transform.localScale = Vector3.one * 0.2f;
        var renderer = drop.AddComponent<SpriteRenderer>();
        renderer.sprite = BaseVisualCatalog.Instance?.goldIcon ?? BaseMapManager.MakeCircleSprite(new Color(1f, 0.82f, 0.2f));
        renderer.sortingOrder = sortingOrder + 44;
        Vector3 land = transform.position + new Vector3(1.05f, -1.05f, -0.03f);
        drop.transform.DOJump(land, 0.3f, 1, 0.4f).SetEase(Ease.OutQuad).SetLink(drop);
        renderer.DOFade(0f, 0.25f).SetDelay(0.4f).SetLink(drop).OnComplete(() => Destroy(drop));
        if (pile.Count > 0 && pile[pile.Count - 1] != null)
        {
            var top = pile[pile.Count - 1];
            top.DOComplete();
            top.DOPunchScale(top.localScale * 0.25f, 0.3f, 6, 0.5f).SetDelay(0.38f);
        }
    }

    private SpriteRenderer MakePart(string name, Sprite sprite, Vector2 offset,
        Vector2 size, int order, Color color, bool keepAspect)
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
        Vector2 bounds = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        float sx = size.x / Mathf.Max(0.01f, bounds.x * parentScale);
        float sy = size.y / Mathf.Max(0.01f, bounds.y * parentScale);
        if (keepAspect) sx = sy = Mathf.Min(sx, sy);
        part.transform.localScale = new Vector3(sx, sy, 1f);
        return renderer;
    }

    private void OnStorageChanged(int stored, int capacity)
    {
        UpdatePile(NuggetsForFill(stored, capacity));
        bool full = capacity > 0 && stored >= capacity;
        UpdateFullState(full);
    }

    private void UpdatePile(int count)
    {
        if (layers == null || count == shownNuggets) return;
        Sprite nuggetSprite = BaseVisualCatalog.Instance?.goldIcon
            ?? BaseMapManager.MakeCircleSprite(new Color(1f, 0.82f, 0.2f));
        while (pile.Count < count)
        {
            int index = pile.Count;
            var nugget = MakePart("MineGoldPile_" + index, nuggetSprite, PileLayout[index],
                Vector2.one * 1.1f, sortingOrder + 2 + DepthOrder(PileLayout[index].y) + index,
                Color.white, true).transform;
            Vector3 rest = nugget.localScale;
            if (shownNuggets >= 0)
            {
                nugget.localScale = Vector3.zero;
                nugget.DOScale(rest, 0.3f).SetEase(Ease.OutBack);
            }
            pile.Add(nugget);
        }
        while (pile.Count > count)
        {
            var last = pile[pile.Count - 1];
            pile.RemoveAt(pile.Count - 1);
            if (last != null) { last.DOKill(); Destroy(last.gameObject); }
        }
        shownNuggets = count;
    }

    private void UpdateFullState(bool full)
    {
        if (fullBubble == null)
        {
            fullBubble = new GameObject("MineFullBubble");
            fullBubble.transform.SetParent(transform, false);
            float unit = 1f / Mathf.Max(0.01f, transform.lossyScale.x);
            fullBubble.transform.localPosition = new Vector3(1.1f * unit, 1.95f * unit, -0.02f);
            fullBubble.transform.localScale = Vector3.one * 0.14f * unit;
            var text = fullBubble.AddComponent<TextMeshPro>();
            text.text = "!";
            text.fontSize = 8f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.85f, 0.24f);
            text.sortingOrder = sortingOrder + 40;
            fullBubble.transform.DOLocalMoveY(fullBubble.transform.localPosition.y + 0.08f * unit, 0.8f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
        fullBubble.SetActive(full);

        if (full && fullGlow == null && stones.Count > 0)
        {
            // Depo dolu: kayalar parlayarak "topla" çağrısı yapar.
            fullGlow = DOTween.Sequence();
            foreach (var stone in stones)
                fullGlow.Join(stone.DOColor(new Color(1f, 1f, 0.72f), 0.7f).SetEase(Ease.InOutSine));
            fullGlow.SetLoops(-1, LoopType.Yoyo);
        }
        else if (!full && fullGlow != null)
        {
            fullGlow.Kill();
            fullGlow = null;
            foreach (var stone in stones) if (stone != null) stone.color = Color.white;
        }
    }

    private IEnumerator MineAtmosphere()
    {
        while (true)
        {
            // 4. oturum (2.3): kazma darbeleriyle eş aralıkta (1,2–2 sn) çırağın önündeki kayadan 2–3 altın kıvılcımı sıçrar.
            yield return new WaitForSeconds(Random.Range(1.2f, 2f));
            if (stones.Count == 0 || !MinerDigging) continue;
            Vector3 origin = Miner != null ? Miner.position + new Vector3(-0.42f, 0.32f) : transform.position + new Vector3(-0.35f, 0.15f);
            int count = Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                var spark = new GameObject("MineGoldSpark");
                spark.transform.position = origin + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.05f, 0.08f));
                spark.transform.localScale = Vector3.one * Random.Range(0.07f, 0.12f);
                var renderer = spark.AddComponent<SpriteRenderer>();
                renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
                renderer.color = i == 0 ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 0.82f, 0.22f);
                renderer.sortingOrder = sortingOrder + 45;
                spark.transform.DOJump(spark.transform.position + new Vector3(Random.Range(-0.45f, 0.1f), Random.Range(-0.2f, 0f)),
                    Random.Range(0.25f, 0.4f), 1, 0.5f).SetLink(spark);
                renderer.DOFade(0f, 0.5f).SetLink(spark).OnComplete(() => Destroy(spark));
            }
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
            icon.transform.position = transform.position + new Vector3(1.1f, -1.1f);
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
