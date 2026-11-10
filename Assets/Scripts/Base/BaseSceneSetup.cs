using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using DG.Tweening;
using System.Linq;
using UnityEngine.Tilemaps;

/// <summary>
/// Yeni Scrollable 30x20 Harita + Tiled Zemin + Sistemlerin Başlatılması
/// </summary>
public class BaseSceneSetup : MonoBehaviour
{
    public static BaseSceneSetup Instance { get; private set; }

    private Canvas _mainCanvas;
    private GameObject _heroObj;
    private TextMeshProUGUI _builderStatusText;
    private GameObject _removePrompt;
    private PlacedMapObject _removeTarget;
    private Coroutine _lockedLandToastRoutine;
    private readonly System.Collections.Generic.Dictionary<Vector2Int, GameObject> _lockedDecor =
        new System.Collections.Generic.Dictionary<Vector2Int, GameObject>();
    private GameObject _lockedShadeRoot;
    private GameObject _naturalBoundaryRoot;
    private Sprite _lockedFogSprite;
    private Texture2D _lockedFogTexture;


    [Header("Map Settings")]
    public int width = BaseWorldLayout.Width;
    public int height = BaseWorldLayout.Height;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        width = BaseWorldLayout.Width;
        height = BaseWorldLayout.Height;
        SetupSystems();

        SetupUI();
        gameObject.AddComponent<BaseLevelBanner>();
        gameObject.AddComponent<DailyRewardsUI>();
        gameObject.AddComponent<DailyQuestsUI>();
        gameObject.AddComponent<AwayReportUI>();
        gameObject.AddComponent<FirstBaseGuide>();
        GenerateWorld();

        // Kayıtlı binaları yükle, yoksa ana üs + maden spawnla
        if (GameManager.Instance?.PlayerData?.placedBuildings?.Count > 0)
        {
            BaseMapManager.Instance.LoadFromPlayerData();
        }
        else
        {
            // Yeni oyun
            BaseMapManager.Instance.SpawnBuilding(BuildingType.MainBase, BaseWorldLayout.Get("main").center, "main");
            BaseMapManager.Instance.SpawnBuilding(BuildingType.GoldMine, BaseWorldLayout.Get("mine_1").center, "mine_1");
            BaseMapManager.Instance.SaveAll();
        }

        SpawnHero();
        new GameObject("BaseApprentice").AddComponent<BaseApprenticeVisual>();
        CreateHeroCamp();
        gameObject.AddComponent<BaseAmbientMotion>();
        BaseMapManager.Instance.LoadOrSeedTerrainResources(() =>
        {
            SpawnTerrainResources(8, true);
            SpawnTerrainResources(4, false);
        });
        SetupCamera();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        gameObject.AddComponent<BaseDebugOverlay>();
#endif
        StartCoroutine(LogBaseWidthAfterSpawn());

        // Input Manager (Long press + tap)
        LongPressHandler.Instance.OnLongPress += HandleLongPress;
    }

    private void OnDestroy()
    {
        if (LongPressHandler.Instance != null)
            LongPressHandler.Instance.OnLongPress -= HandleLongPress;
    }

    private void Update()
    {
        if (_removePrompt != null && _removeTarget != null && _removeTarget.gameObject != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(_removeTarget.gameObject.transform.position + Vector3.up * 0.8f);
            _removePrompt.transform.position = screenPos;
        }
        if (_removeTarget != null && _removeTarget.gameObject == null)
        {
            HideRemovePrompt();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            _wasPointerOverUIOnPress = IsPointerOverBlockingUI();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            _wasPointerOverUIOnPress = IsPointerOverBlockingUI();
        }

        HandleInput();
        UpdateBuilderStatus();
    }

    // ==================== SİSTEM KURULUMU ====================

    private void SetupSystems()
    {
        // Managerlar eksikse ekle
        if (SpriteManager.Instance == null)
        {
            GameObject spriteManagerGo = new GameObject("SpriteManager");
            spriteManagerGo.AddComponent<SpriteManager>();
        }

        if (BaseMapManager.Instance == null) gameObject.AddComponent<BaseMapManager>();
        if (BuilderSystem.Instance == null) gameObject.AddComponent<BuilderSystem>();
        if (GetComponent<BuilderTaskMarker>() == null) gameObject.AddComponent<BuilderTaskMarker>();
        if (PlacementController.Instance == null) gameObject.AddComponent<PlacementController>();
        if (DecorPlacementController.Instance == null) gameObject.AddComponent<DecorPlacementController>();
        if (LongPressHandler.Instance == null) gameObject.AddComponent<LongPressHandler>();
        if (BuildMenuController.Instance == null) gameObject.AddComponent<BuildMenuController>();

        // Altın Madeni online üretimi için
        if (GetComponent<BaseWorldController>() == null) gameObject.AddComponent<BaseWorldController>();

        // İnşaatçı Görsel Kontrolcüsü
        if (FindObjectOfType<BuilderVisualController>() == null)
        {
            GameObject builderGo = new GameObject("BuilderCharacter");
            builderGo.AddComponent<BuilderVisualController>();
        }
    }



    // ==================== UI KURULUMU ====================

    private void SetupUI()
    {
        DisableLegacySceneCanvas();

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Yeni Input System UI modulü
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject canvasObj = new GameObject("BaseCanvas");


        _mainCanvas = canvasObj.AddComponent<Canvas>();
        _mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _mainCanvas.overrideSorting = true;
        _mainCanvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        ReferenceUiVisuals.ConfigureCanvas(scaler);

        canvasObj.AddComponent<GraphicRaycaster>();

        // BuildMenu başlat (canvas'a ekle)
        BuildMenuController.Instance.Initialize(canvasObj.transform);

        // --- HUD Root ---
        GameObject hudRoot = new GameObject("HudRoot");
        hudRoot.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRt = hudRoot.AddComponent<RectTransform>();
        hudRt.anchorMin = Vector2.zero;
        hudRt.anchorMax = Vector2.one;
        hudRt.offsetMin = Vector2.zero;
        hudRt.offsetMax = Vector2.zero;

        Sprite circleSprite = BaseMapManager.MakeCircleSprite(new Color(1f, 1f, 1f, 1f));

        // Doğru UI sprite'ları
        Sprite barGold = SpriteManager.Instance?.GetSprite("ui_bar_gold");
        Sprite barBuilder = SpriteManager.Instance?.GetSprite("ui_bar_builder");
        Sprite ribbonBattle = SpriteManager.Instance?.GetSprite("ui_ribbon_battle");  // BigRibbons 5.png
        Sprite avatarBuilder = SpriteManager.Instance?.GetSprite("ui_builder_avatar"); // Avatars_04.png
        Sprite iconBattle = SpriteManager.Instance?.IconBattleNew
                 ?? Resources.Load<Sprite>("Sprites/ui_icon_battle_new")
                 ?? SpriteManager.Instance?.GetSprite("ui_icon_battle");
        Sprite iconBuild = SpriteManager.Instance?.IconBuildNew
                ?? Resources.Load<Sprite>("Sprites/ui_icon_build_new")
                ?? SpriteManager.Instance?.GetSprite("ui_icon_build");

        Sprite topBarBg = Resources.Load<Sprite>("Sprites/ui_topbar_bg_16x9")
                          ?? SpriteManager.Instance?.GetSprite("ui_topbar_bg_16x9");
        Sprite buttonBg = Resources.Load<Sprite>("Sprites/ui_button_bg_01")
                          ?? SpriteManager.Instance?.GetSprite("ui_button_bg_01");

        if (topBarBg != null)
        {
            GameObject topBar = new GameObject("TopBarBG");
            topBar.transform.SetParent(hudRoot.transform, false);
            RectTransform topBarRt = topBar.AddComponent<RectTransform>();
            topBarRt.anchorMin = new Vector2(0f, 1f);
            topBarRt.anchorMax = new Vector2(1f, 1f);
            topBarRt.pivot = new Vector2(0.5f, 1f);
            topBarRt.anchoredPosition = Vector2.zero;
            topBarRt.sizeDelta = new Vector2(0f, 130f);

            Image topBarImg = topBar.AddComponent<Image>();
            topBarImg.sprite = topBarBg;
            topBarImg.color = Color.white;
            topBarImg.raycastTarget = false;
        }

        // ─── ALTIN BARI (Sol üst) ───────────────────────────────
        GameObject goldPill = new GameObject("GoldPill");
        goldPill.transform.SetParent(hudRoot.transform, false);
        RectTransform goldRt = goldPill.AddComponent<RectTransform>();
        goldRt.anchorMin = new Vector2(0.02f, 0.93f);
        goldRt.anchorMax = new Vector2(0.40f, 0.98f);
        goldRt.offsetMin = Vector2.zero;
        goldRt.offsetMax = Vector2.zero;

        Image goldBg = goldPill.AddComponent<Image>();
        if (topBarBg != null)
        {
            goldBg.color = new Color(1f, 1f, 1f, 0f);
            goldBg.raycastTarget = false;
        }
        else if (barGold != null)
        {
            goldBg.sprite = barGold;
            goldBg.color = Color.white;
        }
        else
        {
            goldBg.sprite = circleSprite;
            goldBg.color = new Color(0.1f, 0.12f, 0.15f, 0.9f);
        }

        Sprite goldCoin = SpriteManager.Instance?.IconGold
                       ?? BaseMapManager.MakeCircleSprite(new Color(1f, 0.85f, 0.2f));
        // Coin ikonu barın sol kısmında
        Image coinIcon = CreateImage(goldPill.transform, "CoinIcon",
            new Vector2(0.02f, 0.18f), new Vector2(0.24f, 0.82f),
            goldCoin, Color.white);
        RectTransform coinRt = coinIcon.rectTransform;
        coinRt.offsetMin = new Vector2(0f, -15f);   // Left, Bottom
        coinRt.offsetMax = new Vector2(-5f, 15f);   // -Right, -Top
        // Altın sayısı
        TextMeshProUGUI goldText = CreateText(goldPill.transform, "GoldText",
            "0",
            new Vector2(0.28f, 0.36f), new Vector2(0.96f, 0.92f),
            36, TextAlignmentOptions.Left, new Color(0.95f, 0.95f, 0.95f));
        goldText.fontStyle = FontStyles.Bold;
        goldText.fontSize = 34f;
        RectTransform goldTextRt = goldText.rectTransform;
        goldTextRt.offsetMin = new Vector2(-15f, -20f); // Left, Bottom
        goldTextRt.offsetMax = new Vector2(0f, 0f);     // -Right, -Top
        // +X/sn
        TextMeshProUGUI gpsText = CreateText(goldPill.transform, "GpsText",
            "+0.0 altın/sn",
            new Vector2(0.28f, 0.04f), new Vector2(0.98f, 0.36f),
            14, TextAlignmentOptions.Left, new Color(0.85f, 0.85f, 0.85f));
        gpsText.gameObject.SetActive(false);

        // ─── SAVAŞ SEVİYESİ (Üst Orta) ────────────────────────
        GameObject levelPill = new GameObject("BattleLevelPill");
        levelPill.transform.SetParent(hudRoot.transform, false);
        RectTransform levelRt = levelPill.AddComponent<RectTransform>();
        levelRt.anchorMin = new Vector2(0.33f, 0.935f);
        levelRt.anchorMax = new Vector2(0.67f, 0.985f);
        levelRt.offsetMin = new Vector2(0f, 0f);   // Left, Bottom
        levelRt.offsetMax = new Vector2(0f, -30f); // -Right, -Top

        Image levelBg = levelPill.AddComponent<Image>();
        if (topBarBg != null)
        {
            levelBg.color = new Color(1f, 1f, 1f, 0f);
            levelBg.raycastTarget = false;
        }
        else if (ribbonBattle != null)
        {
            levelBg.sprite = ribbonBattle;
            levelBg.color = Color.white;
        }
        else
        {
            levelBg.sprite = circleSprite;
            levelBg.color = new Color(0.1f, 0.12f, 0.15f, 0.9f);
        }

        TextMeshProUGUI battleLvlText = CreateText(levelPill.transform, "LevelText",
            "SAVAŞ LVL. 10",
            new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.82f),
            28, TextAlignmentOptions.Center, new Color(0.95f, 0.95f, 0.95f));
        battleLvlText.fontStyle = FontStyles.Bold;
        battleLvlText.fontSize = 36f;
        RectTransform levelTextRt = battleLvlText.rectTransform;
        levelTextRt.offsetMin = new Vector2(0f, 0f);
        levelTextRt.offsetMax = new Vector2(0f, 0f);

        // ─── İNŞAATÇI BARI (Sağ üst) ──────────────────────────
        GameObject builderPill = new GameObject("BuilderPill");
        builderPill.transform.SetParent(hudRoot.transform, false);
        RectTransform builderRt = builderPill.AddComponent<RectTransform>();
        builderRt.anchorMin = new Vector2(0.60f, 0.935f);
        builderRt.anchorMax = new Vector2(0.98f, 0.985f);
        builderRt.offsetMin = new Vector2(0f, -20f);  // Left, Bottom
        builderRt.offsetMax = new Vector2(-10f, 0f);  // -Right, -Top

        Image builderBg = builderPill.AddComponent<Image>();
        if (topBarBg != null)
        {
            builderBg.color = new Color(1f, 1f, 1f, 0f);
            builderBg.raycastTarget = false;
        }
        else if (barBuilder != null)
        {
            builderBg.sprite = barBuilder;
            builderBg.color = Color.white;
        }
        else
        {
            builderBg.sprite = circleSprite;
            builderBg.color = new Color(0.1f, 0.12f, 0.15f, 0.9f);
        }

        Sprite builderIcon = avatarBuilder
                          ?? SpriteManager.Instance?.BuilderHut
                          ?? BaseMapManager.MakeCircleSprite(new Color(0.3f, 0.6f, 1f));
        Image avatarIcon = CreateImage(builderPill.transform, "AvatarIcon",
            new Vector2(0.52f, 0.18f), new Vector2(0.72f, 0.82f),
            builderIcon, Color.white);
        RectTransform avatarRt = avatarIcon.rectTransform;
        avatarRt.offsetMin = new Vector2(0f, -5f);   // Left, Bottom
        avatarRt.offsetMax = new Vector2(-18f, 0f);  // -Right, -Top
        _builderStatusText = CreateText(builderPill.transform, "BuilderCount",
            "1/1",
            new Vector2(0.74f, 0.18f), new Vector2(0.98f, 0.82f),
            30, TextAlignmentOptions.Left, new Color(0.95f, 0.95f, 0.95f));
        _builderStatusText.fontStyle = FontStyles.Bold;
        RectTransform builderCountRt = _builderStatusText.rectTransform;
        builderCountRt.offsetMin = new Vector2(-18f, 0f); // Left, Bottom
        builderCountRt.offsetMax = new Vector2(0f, -6f);  // -Right, -Top

        // ─── SAVAŞA GİT BUTONU (Tool_03) ──────────────────────
        Button battleBtn = CreateRoundIconButton(hudRoot.transform, "BattleBtn",
            new Vector2(0.86f, 0.10f), new Vector2(320f, 140f),
            iconBattle, "Savaşa Git", new Color(0.85f, 0.25f, 0.2f), buttonBg);

        // ─── İNŞA ET BUTONU (Tool_04) ─────────────────────────
        Button buildBtn = CreateRoundIconButton(hudRoot.transform, "BuildBtn",
            new Vector2(0.86f, 0.22f), new Vector2(320f, 140f),
            iconBuild, "İnşa Et", new Color(0.2f, 0.55f, 0.85f), buttonBg);

        // --- Info Panels (Building + Hero) ---
        GameObject buildingInfoPanel = CreatePanel(canvasObj.transform, "BuildingInfo", new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.45f), new Color(0.1f, 0.1f, 0.1f, 0.95f));
        TextMeshProUGUI bName = CreateText(buildingInfoPanel.transform, "Name", "Bina Adı", new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.95f), 45, TextAlignmentOptions.Center, Color.white);
        TextMeshProUGUI bLvl = CreateText(buildingInfoPanel.transform, "Level", "Seviye 1", new Vector2(0.1f, 0.65f), new Vector2(0.9f, 0.8f), 35, TextAlignmentOptions.Center, Color.yellow);
        TextMeshProUGUI bStats = CreateText(buildingInfoPanel.transform, "Stats", "Can: 100", new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f), 28, TextAlignmentOptions.TopLeft, Color.white);
        Button bUpBtn = CreateButton(buildingInfoPanel.transform, "UpBtn", "Yükselt", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.3f), new Color(0.2f, 0.7f, 0.2f));
        TextMeshProUGUI bCost = bUpBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        Button bClose = CreateButton(buildingInfoPanel.transform, "CloseBtn", "X", new Vector2(0.85f, 0.85f), new Vector2(0.95f, 0.95f), Color.red);

        GameObject heroInfoPanel = CreatePanel(canvasObj.transform, "HeroInfo", new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.45f), new Color(0.1f, 0.1f, 0.1f, 0.95f));
        TextMeshProUGUI hName = CreateText(heroInfoPanel.transform, "Name", "Kahraman", new Vector2(0.1f, 0.85f), new Vector2(0.84f, 0.96f), 37, TextAlignmentOptions.Center, Color.white);
        TextMeshProUGUI hLvl = CreateText(heroInfoPanel.transform, "Level", "Seviye 1", new Vector2(0.1f, 0.60f), new Vector2(0.9f, 0.72f), 31, TextAlignmentOptions.Center, Color.yellow);
        TextMeshProUGUI hStats = CreateText(heroInfoPanel.transform, "Stats", "Can: 100", new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.60f), 25, TextAlignmentOptions.TopLeft, Color.white);
        Button hUpBtn = CreateButton(heroInfoPanel.transform, "UpBtn", "Yükselt", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.3f), new Color(0.2f, 0.7f, 0.2f));
        TextMeshProUGUI hCost = hUpBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        Button hClose = CreateButton(heroInfoPanel.transform, "CloseBtn", "X", new Vector2(0.85f, 0.85f), new Vector2(0.95f, 0.95f), Color.red);

        // --- BaseWorldUI Bağlama ---
        BaseWorldUI uiScript = gameObject.AddComponent<BaseWorldUI>();
        uiScript.SetReferences(
            goldText, battleLvlText, gpsText,
            buildingInfoPanel, bName, bLvl, bStats, bCost, bUpBtn, bClose,
            heroInfoPanel, hName, hLvl, hStats, hCost, hUpBtn, hClose,
            battleBtn, buildBtn
        );

        DarkFantasyHudPolish.PolishBase(_mainCanvas, hudRoot.transform);
    }

    private void DisableLegacySceneCanvas()
    {
        // Eski sahne UI'si (Canvas) runtime UI ile cakisiyor, kapat.
        var canvases = FindObjectsOfType<Canvas>(true);
        foreach (var canvas in canvases)
        {
            if (canvas == null || canvas.gameObject == null) continue;
            if (canvas.gameObject.name == "BaseCanvas") continue;

            canvas.gameObject.SetActive(false);
        }
    }

    private void UpdateBuilderStatus()
    {
        if (_builderStatusText != null && BuilderSystem.Instance != null)
        {
            _builderStatusText.text = $"{BuilderSystem.Instance.FreeCount}/{BuilderSystem.Instance.TotalCount}";
        }
    }

    private void CreateVirtualJoystick(Transform parent)
    {
        // Keep the full-screen movement raycast behind every HUD and modal control.
        var inputCanvasObject = new GameObject("BaseJoystickCanvas");
        var inputCanvas = inputCanvasObject.AddComponent<Canvas>();
        inputCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        inputCanvas.overrideSorting = true;
        inputCanvas.sortingOrder = 0;
        var inputScaler = inputCanvasObject.AddComponent<CanvasScaler>();
        ReferenceUiVisuals.ConfigureCanvas(inputScaler);
        inputCanvasObject.AddComponent<GraphicRaycaster>();

        GameObject inputRoot = new GameObject("VirtualJoystickInput");
        inputRoot.transform.SetParent(inputCanvasObject.transform, false);
        RectTransform inputRt = inputRoot.AddComponent<RectTransform>();
        inputRt.anchorMin = Vector2.zero;
        inputRt.anchorMax = Vector2.one;
        inputRt.offsetMin = Vector2.zero;
        inputRt.offsetMax = Vector2.zero;

        Image inputImg = inputRoot.AddComponent<Image>();
        inputImg.color = new Color(0f, 0f, 0f, 0f);
        inputImg.raycastTarget = true;

        GameObject visualRoot = new GameObject("VirtualJoystick");
        visualRoot.transform.SetParent(parent, false);
        RectTransform rootRt = visualRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(220f, 220f);
        rootRt.anchoredPosition = new Vector2(180f, 180f);

        Image bg = visualRoot.AddComponent<Image>();
        bg.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.5f);
        bg.raycastTarget = false;

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(visualRoot.transform, false);
        RectTransform handleRt = handleObj.AddComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0.5f, 0.5f);
        handleRt.anchorMax = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(90f, 90f);
        handleRt.anchoredPosition = Vector2.zero;

        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        handleImg.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        handleImg.raycastTarget = false;

        VirtualJoystick joystick = inputRoot.AddComponent<VirtualJoystick>();
        joystick.Setup(rootRt, handleRt, 100f);
    }

    // ==================== HARİTA OLUŞTURMA ====================

    private void GenerateWorld()
    {
        var legacyGround = GameObject.Find("Ground")?.GetComponent<SpriteRenderer>();
        if (legacyGround != null) legacyGround.enabled = false;

        var root = new GameObject("WorldRoot");
        var grid = root.AddComponent<Grid>();
        grid.cellSize = Vector3.one;
        root.AddComponent<BaseGroundPaths>();
        var ground = new GameObject("BaseGroundTilemap");
        ground.transform.SetParent(root.transform, false);
        var tilemap = ground.AddComponent<Tilemap>();
        var renderer = ground.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = -20;

        var grassTexture = Resources.Load<Texture2D>("BaseDecor/grass_tileset");
        Sprite buildableSprite = grassTexture != null
            ? Sprite.Create(grassTexture, new Rect(64, 256, 64, 64),
                new Vector2(0.5f, 0.5f), 64f)
            : SpriteManager.Instance?.GetSquareTileSprite("ground_buildable_01");
        Sprite lockedSprite = buildableSprite;

        var buildableTile = ScriptableObject.CreateInstance<Tile>();
        buildableTile.sprite = buildableSprite;
        buildableTile.color = new Color(0.94f, 1f, 0.87f);
        buildableTile.flags = TileFlags.None;
        var alternateTile = ScriptableObject.CreateInstance<Tile>();
        alternateTile.sprite = buildableSprite;
        alternateTile.color = new Color(0.89f, 0.98f, 0.82f);
        alternateTile.flags = TileFlags.None;
        var thirdTile = ScriptableObject.CreateInstance<Tile>();
        thirdTile.sprite = buildableSprite;
        thirdTile.color = new Color(0.98f, 1f, 0.91f);
        thirdTile.flags = TileFlags.None;
        var lockedTile = ScriptableObject.CreateInstance<Tile>();
        lockedTile.sprite = lockedSprite;
        lockedTile.color = new Color(0.76f, 0.91f, 0.70f);
        lockedTile.flags = TileFlags.None;

        // The camera is wider vertically than the 22×36 playable grid on tall phones.
        // Textured scenery behind the grid fills that view without adding playable cells.
        var backdrop = new GameObject("BaseBackdropTilemap");
        backdrop.transform.SetParent(root.transform, false);
        var backdropTiles = backdrop.AddComponent<Tilemap>();
        backdrop.AddComponent<TilemapRenderer>().sortingOrder = -30;
        var backdropTile = ScriptableObject.CreateInstance<Tile>();
        backdropTile.sprite = buildableSprite;
        backdropTile.color = new Color(0.88f, 0.98f, 0.81f);
        backdropTile.flags = TileFlags.None;
        // Tablet gibi geniş ekranlarda kamera yüksekliğe sığar ve 22 hücreden çok daha
        // geniş görür; zemin yanlara taşar ki kenarlarda siyah şerit kalmasın.
        for (int y = -12; y < BaseWorldLayout.Height + 12; y++)
        for (int x = -14; x < BaseWorldLayout.Width + 14; x++)
            backdropTiles.SetTile(new Vector3Int(x, y, 0), backdropTile);
        backdropTiles.CompressBounds();

        var stainRoot = new GameObject("GrassDetails");
        stainRoot.transform.SetParent(root.transform, false);
        Sprite grassTuft = Resources.Load<Sprite>("BaseDecor/bush");
        var grassRandom = new System.Random(194712);

        for (int y = 0; y < BaseWorldLayout.Height; y++)
        for (int x = 0; x < BaseWorldLayout.Width; x++)
        {
            Vector3Int cell = new Vector3Int(x, y, 0);
            bool buildable = BaseWorldLayout.Buildable.Contains(new Vector2Int(x, y));
            int variation = grassRandom.Next();
            tilemap.SetTile(cell, !buildable ? lockedTile
                : variation % 3 == 0 ? buildableTile
                : variation % 3 == 1 ? alternateTile : thirdTile);
            if (buildable)
            {
                var rotation = Quaternion.Euler(0f, 0f, (variation % 4) * 90f);
                tilemap.SetTransformMatrix(cell, Matrix4x4.TRS(Vector3.zero, rotation,
                    new Vector3((variation & 4) == 0 ? 1f : -1f, 1f, 1f)));
                if (grassTuft != null && grassRandom.NextDouble() < 0.06)
                {
                    var stain = new GameObject("GrassFlowerDetail");
                    stain.transform.SetParent(stainRoot.transform, false);
                    stain.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    var sprite = stain.AddComponent<SpriteRenderer>();
                    sprite.sprite = grassTuft;
                    sprite.sortingOrder = -19;
                    sprite.color = new Color(0.58f, 0.79f, 0.49f, 0.52f);
                    float scale = 0.28f / Mathf.Max(0.1f, grassTuft.bounds.size.x);
                    stain.transform.localScale = Vector3.one * scale;
                }
            }
        }
        tilemap.CompressBounds();
        CreateExteriorScenery(root.transform);
        CreateForestMargin(root.transform);
        BaseMapManager.Instance.RefreshBuildableArea(width, height);
        CreateLockedDecor();
    }

    private static void CreateExteriorScenery(Transform parent)
    {
        Sprite tree = System.Array.Find(DecorPlacementController.All,
            item => item.id == "tree")?.sprite;
        Sprite bush = Resources.Load<Sprite>("BaseDecor/bush");
        Sprite rock = Resources.Load<Sprite>("BaseDecor/rock");
        if (tree == null && bush == null && rock == null) return;

        var root = new GameObject("ExteriorScenery");
        root.transform.SetParent(parent, false);
        var random = new System.Random(40172);
        for (int side = 0; side < 4; side++)
        {
            int count = side < 2 ? 10 : 5;
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = i % 5 == 0 ? rock : i % 3 == 0 ? bush : tree;
                if (sprite == null) sprite = tree != null ? tree : rock != null ? rock : bush;
                if (sprite == null) continue;
                float x = side == 0 ? 0.2f + (float)random.NextDouble() * 0.3f
                    : side == 1 ? 21.5f + (float)random.NextDouble() * 0.3f
                    : 2.1f + (float)random.NextDouble() * 17.8f;
                float y = side < 2 ? 0.5f + (i + 0.3f +
                    (float)random.NextDouble() * 0.4f) / count * 37f
                    : side == 2 ? 0.15f + (float)random.NextDouble() * 0.25f
                    : 37.6f + (float)random.NextDouble() * 0.25f;
                var go = new GameObject("Exterior_" + side + "_" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(x, y, 0f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 6;
                float targetHeight = sprite == rock ? 0.65f : sprite == bush ? 0.85f
                    : side < 2 ? 2.4f + (float)random.NextDouble() * 0.5f : 0.9f;
                go.transform.localScale = Vector3.one *
                    (targetHeight / Mathf.Max(0.01f, sprite.bounds.size.y));
                // Keep the full sprite bounds outside the fence; edge trees may crop at the screen.
                Vector3 position = go.transform.position;
                if (side == 0) position.x = Mathf.Min(position.x,
                    1.0f - renderer.bounds.extents.x);
                else if (side == 1) position.x = Mathf.Max(position.x,
                    21.0f + renderer.bounds.extents.x);
                else if (side == 2) position.y = Mathf.Min(position.y,
                    1.0f - renderer.bounds.extents.y);
                else position.y = Mathf.Max(position.y,
                    37.0f + renderer.bounds.extents.y);
                go.transform.position = position;
            }
        }
    }

    /// <summary>
    /// Çitin dışındaki geniş alanda seyrek, koyulaştırılmış ağaçlar: geniş ekranlarda
    /// dünyayı bir orman çerçevesi sarar. Oynanışa dahil değildir (çarpışma yok).
    /// </summary>
    private static void CreateForestMargin(Transform parent)
    {
        Sprite tree = System.Array.Find(DecorPlacementController.All, item => item.id == "tree")?.sprite;
        Sprite bush = Resources.Load<Sprite>("BaseDecor/bush");
        if (tree == null) return;
        var root = new GameObject("ForestMargin");
        root.transform.SetParent(parent, false);
        var random = new System.Random(90211);
        for (int side = 0; side < 2; side++)
        for (int i = 0; i < 46; i++)
        {
            // Çite yakın sıralar daha sık, dışa doğru seyrekleşir.
            float depth = 1.6f + (float)(random.NextDouble() * random.NextDouble()) * 11.5f;
            float x = side == 0 ? -depth : BaseWorldLayout.Width + depth;
            float y = -6f + (float)random.NextDouble() * (BaseWorldLayout.Height + 12f);
            bool isBush = bush != null && random.NextDouble() < 0.28;
            var go = new GameObject("Margin_" + side + "_" + i);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(x, y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = isBush ? bush : tree;
            // Uzaktakiler hafif koyu: derinlik hissi, oyun alanına dikkat.
            float shade = Mathf.Lerp(0.92f, 0.62f, Mathf.Clamp01((depth - 1.6f) / 10f));
            renderer.color = new Color(shade, shade * 1.02f, shade * 0.95f);
            renderer.sortingOrder = 6 - Mathf.RoundToInt(y * 0.1f);
            float height = isBush ? 0.9f : 2.2f + (float)random.NextDouble() * 0.9f;
            go.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, renderer.sprite.bounds.size.y));
        }
    }

    private void SpawnTerrainResources(int count, bool trees)
    {
        int created = 0;
        for (int attempt = 0; attempt < 500 && created < count; attempt++)
        {
            int quadrant = created % 4;
            float minX = quadrant % 2 == 0 ? 2.7f : width * 0.5f;
            float maxX = quadrant % 2 == 0 ? width * 0.5f : width - 2.7f;
            float minY = quadrant < 2 ? 2.7f : height * 0.5f;
            float maxY = quadrant < 2 ? height * 0.5f : height - 2.7f;
            Vector3 position = new Vector3(Random.Range(minX, maxX),
                Random.Range(minY, maxY), 0f);
            if (!BaseMapManager.Instance.CanSpawnResourceAt(position)) continue;
            if (trees) BaseMapManager.Instance.SpawnTree(position);
            else BaseMapManager.Instance.SpawnRock(position);
            created++;
        }
    }

    private void CreateLockedDecor()
    {
        Sprite tree = System.Array.Find(DecorPlacementController.All, item => item.id == "tree")?.sprite;
        Sprite[] sprites = { tree, Resources.Load<Sprite>("BaseDecor/bush"),
            Resources.Load<Sprite>("BaseDecor/rock"), Resources.Load<Sprite>("BaseDecor/stump") };
        var clean = BaseWorldLayout.CleanAtLevel(GameManager.Instance != null
            ? GameManager.Instance.GetMainBaseLevel() : 1);
        var random = new System.Random(76143);
        var centers = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i < 18; i++)
            centers.Add(new Vector2(random.Next(2, 20), random.Next(2, 36)));
        foreach (Vector2Int cell in EnumerateInteriorCells())
        {
            if (clean.Contains(cell)) continue;
            float nearest = 20f;
            foreach (var center in centers)
                nearest = Mathf.Min(nearest, Vector2.Distance(center, cell));
            // Seyrek doku + birkaç belirgin küme; önceki eşit dağılımlı yoğun görünüm yerine.
            float density = Mathf.Lerp(0.05f, 0.62f, Mathf.Clamp01((3.6f - nearest) / 3.2f));
            if (cell.y < 4 || cell.y >= 34) density = Mathf.Max(density, 0.42f);
            if (random.NextDouble() > density) continue;
            Sprite sprite = sprites[random.Next(sprites.Length)];
            if (sprite == null) continue;
            var go = new GameObject("LockedObstacle_" + cell.x + "_" + cell.y);
            go.transform.position = new Vector3(cell.x + 0.5f + (float)(random.NextDouble() - 0.5) * 0.4f,
                cell.y + 0.5f + (float)(random.NextDouble() - 0.5) * 0.4f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;
            float size = (0.65f + (float)random.NextDouble() * 0.65f)
                / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            go.transform.localScale = Vector3.one * size;
            _lockedDecor[cell] = go;
        }
        RefreshLockedShade(clean);
    }

    private void RefreshLockedShade(RectInt clean)
    {
        if (_lockedShadeRoot != null) Destroy(_lockedShadeRoot);
        if (_lockedFogSprite != null) Destroy(_lockedFogSprite);
        if (_lockedFogTexture != null) Destroy(_lockedFogTexture);
        _lockedShadeRoot = new GameObject("LockedLandShade");
        Sprite square = BaseMapManager.MakeSquareSprite(Color.white);
        void Band(string name, int start, int end)
        {
            if (end <= start) return;
            var go = new GameObject(name);
            go.transform.SetParent(_lockedShadeRoot.transform, false);
            go.transform.position = new Vector3(11f, (start + end) * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0f, 0f, 0f, 0.25f);
            sr.sortingOrder = 4;
            go.transform.localScale = new Vector3(18f / square.bounds.size.x,
                (end - start) / square.bounds.size.y, 1f);
        }
        Band("SouthClosed", BaseWorldLayout.Buildable.yMin, clean.yMin);
        Band("NorthClosed", clean.yMax, BaseWorldLayout.Buildable.yMax);
        var fogTexture = new Texture2D(1, 32, TextureFormat.RGBA32, false);
        _lockedFogTexture = fogTexture;
        for (int y = 0; y < 32; y++)
            fogTexture.SetPixel(0, y, new Color(0.73f, 0.72f, 0.65f,
                (1f - Mathf.Abs(y - 16f) / 16f) * 0.22f));
        fogTexture.Apply();
        var fogSprite = Sprite.Create(fogTexture, new Rect(0, 0, 1, 32),
            new Vector2(0.5f, 0.5f), 32f);
        _lockedFogSprite = fogSprite;
        var fog = new GameObject("NorthernBoundaryFog");
        fog.transform.SetParent(_lockedShadeRoot.transform, false);
        fog.transform.position = new Vector3(11f, clean.yMax, 0f);
        fog.transform.localScale = new Vector3(18f, 1.2f, 1f);
        var fogRenderer = fog.AddComponent<SpriteRenderer>();
        fogRenderer.sprite = fogSprite;
        fogRenderer.sortingOrder = 6;
    }

    private static System.Collections.Generic.IEnumerable<Vector2Int> EnumerateInteriorCells()
    {
        for (int y = BaseWorldLayout.Buildable.yMin; y < BaseWorldLayout.Buildable.yMax; y++)
        for (int x = BaseWorldLayout.Buildable.xMin; x < BaseWorldLayout.Buildable.xMax; x++)
            yield return new Vector2Int(x, y);
    }

    public void ClearUnlockedLand(int level)
    {
        var clean = BaseWorldLayout.CleanAtLevel(level);
        RefreshLockedShade(clean);
        StartCoroutine(AnimateLandClearing(clean));
        if (_naturalBoundaryRoot != null)
        {
            Destroy(_naturalBoundaryRoot);
            _naturalBoundaryRoot = null;
        }
    }

    private void CreateNaturalBoundaryBand()
    {
        if (_naturalBoundaryRoot != null) return;
        _naturalBoundaryRoot = new GameObject("NaturalBoundaryBand");
        Sprite tree = System.Array.Find(DecorPlacementController.All,
            item => item.id == "tree")?.sprite;
        Sprite bush = Resources.Load<Sprite>("BaseDecor/bush");
        var random = new System.Random(5519);
        for (int y = 2; y <= 35; y++)
        {
            if (y != 2 && y != 3 && y != 34 && y != 35) continue;
            for (int x = 2; x < 20; x++)
            {
                if (random.NextDouble() > 0.78) continue;
                Sprite sprite = random.NextDouble() < 0.45 ? tree : bush;
                if (sprite == null) continue;
                var item = new GameObject("NaturalBoundaryPlant");
                item.transform.SetParent(_naturalBoundaryRoot.transform, false);
                item.transform.position = new Vector3(x + 0.5f,
                    y + 0.5f + (float)(random.NextDouble() - 0.5) * 0.24f);
                var renderer = item.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 5;
                float size = (0.75f + (float)random.NextDouble() * 0.45f) /
                    Mathf.Max(0.01f, sprite.bounds.size.y);
                item.transform.localScale = Vector3.one * size;
            }
        }
    }

    private System.Collections.IEnumerator AnimateLandClearing(RectInt clean)
    {
        var pawn = GameObject.Find("BuilderCharacter");
        Vector3 pawnHome = pawn != null ? pawn.transform.position : Vector3.zero;
        GameObject helper = null;
        var pawnSprite = pawn != null ? pawn.GetComponentInChildren<SpriteRenderer>()?.sprite : null;
        if (pawnSprite != null)
        {
            helper = new GameObject("ClearingPawn");
            helper.transform.position = pawnHome + Vector3.right * 0.6f;
            helper.transform.localScale = Vector3.one *
                (0.7f / Mathf.Max(0.01f, pawnSprite.bounds.size.y));
            var helperRenderer = helper.AddComponent<SpriteRenderer>();
            helperRenderer.sprite = pawnSprite;
            helperRenderer.sortingOrder = 112;
        }
        var cleared = new System.Collections.Generic.List<Vector2Int>();
        foreach (var pair in _lockedDecor)
            if (clean.Contains(pair.Key)) cleared.Add(pair.Key);
        for (int index = 0; index < cleared.Count; index++)
        {
            var cell = cleared[index];
            GameObject obstacle = _lockedDecor[cell];
            _lockedDecor.Remove(cell);
            if (obstacle == null) continue;
            var worker = index % 2 == 0 || helper == null ? pawn : helper;
            if (worker != null) worker.transform.DOMove(obstacle.transform.position, 0.18f);
            obstacle.transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack)
                .OnComplete(() => { if (obstacle != null) Destroy(obstacle); });
            var dust = new GameObject("ClearingDust");
            dust.transform.position = obstacle.transform.position;
            var dustRenderer = dust.AddComponent<SpriteRenderer>();
            dustRenderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
            dustRenderer.color = new Color(0.75f, 0.65f, 0.45f, 0.45f);
            dustRenderer.sortingOrder = 110;
            dust.transform.localScale = Vector3.one * 0.2f;
            dust.transform.DOScale(Vector3.one * 0.8f, 0.3f).OnComplete(() => Destroy(dust));
            dustRenderer.DOFade(0f, 0.3f);
            yield return new WaitForSeconds(0.05f);
        }
        if (pawn != null) pawn.transform.DOMove(pawnHome, 0.3f);
        if (helper != null) Destroy(helper, 0.4f);
        if (cleared.Count > 0 && Camera.main != null)
            Camera.main.transform.DOShakePosition(0.16f, 0.06f, 8, 90f);
    }

    private static void AddGroundVisual(Transform parent, string objectName, string resource,
        Vector2 center, Vector2 size, int order, Color tint)
    {
        Texture2D texture = Resources.Load<Texture2D>("Sprites/" + resource);
        if (texture == null) return;
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(center.x, center.y, 2f);
        // The buildable texture has dark baked edges. Tile only its clean central field.
        Rect source = resource == "ground_buildable_01"
            ? new Rect(texture.width * 0.36f, texture.height * 0.17f,
                texture.width * 0.28f, texture.height * 0.66f)
            : new Rect(0, 0, texture.width, texture.height);
        var sprite = Sprite.Create(texture, source,
            new Vector2(0.5f, 0.5f), source.width, 0, SpriteMeshType.FullRect);
        var visual = go.AddComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.drawMode = SpriteDrawMode.Tiled;
        visual.size = size;
        visual.color = tint;
        visual.sortingOrder = order;
    }
    private void SpawnHero()
    {
        _heroObj = new GameObject("Hero");
        Vector2 camp = BaseWorldLayout.Get("camp").center;
        _heroObj.transform.position = new Vector3(camp.x, camp.y, -0.5f);
        _heroObj.transform.localScale = Vector3.one;

        SpriteRenderer sr = _heroObj.AddComponent<SpriteRenderer>();
        WeaponType weapon = GameManager.Instance?.PlayerData?.hero?.weaponType ?? WeaponType.Axe;
        sr.sprite = SpriteManager.Instance?.GetHeroSprite(weapon);
        if (sr.sprite == null)
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] cols = new Color[32 * 32];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.cyan;
            tex.SetPixels(cols); tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }
        sr.sortingOrder = 15;

        string heroName = weapon == WeaponType.Spear ? "spear" : weapon == WeaponType.Bow ? "bow" : "axe";
        Sprite[] idleFrames = Resources.LoadAll<Sprite>($"Sprites/hero_{heroName}_idle");
        if (idleFrames.Length > 0)
        {
            System.Array.Sort(idleFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            Sprite trimmed = HeroPortraitSprite.Get(idleFrames[0]);
            if (trimmed != null) sr.sprite = trimmed;
        }
        float visibleWidth = HeroPortraitSprite.Get(sr.sprite).bounds.size.x;
        float heroScale = 2.5f / Mathf.Max(0.01f, visibleWidth);
        _heroObj.transform.localScale = new Vector3(heroScale, heroScale, 1f);

        CircleCollider2D col = _heroObj.AddComponent<CircleCollider2D>();
        col.isTrigger = false;

        Rigidbody2D rb = _heroObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (sr.sprite != null)
        {
            float radius = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) * 0.4f;
            float previousScale = weapon == WeaponType.Spear ? 0.8f : 0.7f;
            float visualScale = _heroObj.transform.localScale.x;
            col.radius = Mathf.Max(0.25f, radius) * previousScale / visualScale;
        }

        _heroObj.AddComponent<HeroMovementController>();
    }

    private void CreateHeroCamp()
    {
        Vector2 campPosition = BaseWorldLayout.Get("camp").center;
        Vector3 center = new Vector3(campPosition.x, campPosition.y, 0f);
        var camp = new GameObject("HeroCamp");
        camp.transform.position = center;
        var renderer = camp.AddComponent<SpriteRenderer>();
        renderer.sprite = MakeCampFlagSprite();
        renderer.sortingOrder = 78;
        camp.transform.localScale = Vector3.one * 2f;
        BaseMapManager.CreateBuildingShadow(camp.transform, new Vector2(2f, 2f),
            camp.transform.localScale, renderer.sortingOrder - 1);
    }

    private static Sprite MakeCampFlagSprite()
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        var pixels = new Color32[32 * 32];
        Color32 wood = new Color32(184, 132, 67, 255);
        Color32 edge = new Color32(96, 56, 28, 255);
        Color32 gold = new Color32(240, 194, 74, 255);
        Color32 shade = new Color32(166, 102, 31, 255);
        for (int y = 3; y < 29; y++)
        for (int x = 7; x < 10; x++) pixels[y * 32 + x] = x == 7 ? edge : wood;
        for (int y = 17; y < 28; y++)
        for (int x = 10; x < 27 - (27 - y) / 2; x++)
            pixels[y * 32 + x] = y < 19 || x > 23 ? shade : gold;
        for (int x = 3; x < 15; x++)
        for (int y = 2; y < 5; y++)
            if (Mathf.Abs(x - 9) + Mathf.Abs(y - 3) < 7) pixels[y * 32 + x] = edge;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.2f), 32f);
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;
        RefreshCameraFraming();
        var follow = camera.GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;
        var panZoom = camera.GetComponent<CameraController>();
        if (panZoom != null) panZoom.enabled = false;
    }

    public void RefreshCameraFraming()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.20f, 0.31f, 0.15f); // zemin bitse de siyah değil orman yeşili
        camera.ResetAspect();
        camera.orthographicSize = 22f / (2f * camera.aspect);
        camera.transform.position = new Vector3(11f, 19f, -10f);
        var bars = FindFirstObjectByType<WorldViewportBars>();
        if (bars != null) bars.UpdateLayout();
    }

    private System.Collections.IEnumerator LogBaseWidthAfterSpawn()
    {
        yield return new WaitForSeconds(0.4f);
        var mainBase = FindObjectsOfType<BuildingIdentifier>()
            .FirstOrDefault(building => building.buildingType == BuildingType.MainBase);
        var renderer = mainBase != null ? mainBase.GetComponent<SpriteRenderer>() : null;
        var camera = Camera.main;
        if (renderer != null && camera != null)
        {
            float ratio = renderer.bounds.size.x / (camera.orthographicSize * 2f * camera.aspect);
            Debug.Log($"[BaseVisual] Ana Üs ekran genişliği oranı: {ratio:F3} (hedef 0.50–0.55)");
        }
    }

    // ==================== INPUT (TAP & LONG PRESS) ====================

    private void HandleLongPress(GameObject targetObj)
    {
        var decor = targetObj != null ? targetObj.GetComponentInParent<DecorInstance>() : null;
        if (decor != null)
        {
            DecorPlacementController.Instance?.BeginMove(decor);
            return;
        }
        // Uzun basma algılandığında
        var mapObj = BaseMapManager.Instance.GetMapObject(targetObj);
        if (mapObj != null)
        {
            if (mapObj.objectType == MapObjectType.Tree || mapObj.objectType == MapObjectType.Rock || mapObj.objectType == MapObjectType.Fence)
            {
                BaseMapManager.Instance.RequestRemoveObject(mapObj);
            }
            else if (mapObj.objectType == MapObjectType.Building ||
                     mapObj.objectType == MapObjectType.BuilderHut)
            {
                // Taşıma modu (inşaatçı kulübeleri de taşınır)
                if (mapObj.buildingType != BuildingType.MainBase)
                    PlacementController.Instance.BeginMove(mapObj);
            }
        }
    }

    private bool _wasPointerOverUIOnPress = false;

    private bool IsPointerOverBlockingUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            eventData.position = Touchscreen.current.touches[0].position.ReadValue();
        else if (Mouse.current != null)
            eventData.position = Mouse.current.position.ReadValue();
        else
            return false;

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

        foreach (var r in results)
        {
            if (r.gameObject.name != "VirtualJoystickInput" && r.gameObject.name != "VirtualJoystick")
            {
                return true; // Gerçek bir UI elemanına (buton, panel vb.) tıklandı
            }
        }
        return false;
    }

    private void HandleInput()
    {
        if (DecorPlacementController.Instance != null && DecorPlacementController.Instance.IsPlacing)
            return;
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (_wasPointerOverUIOnPress) return;
            if (IsPointerOverBlockingUI()) return;

            if (LongPressHandler.Instance.WasFiredThisPress) return; // Uzun basma işlemi gerçekleşmişse tap'i yoksay

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            HandleTap(worldPos);
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            if (_wasPointerOverUIOnPress) return;
            if (IsPointerOverBlockingUI()) return;

            if (LongPressHandler.Instance.WasFiredThisPress) return;

            Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, 0f));
            HandleTap(worldPos);
        }
    }

    /// <summary>
    /// Kahramanın çarpışma dairesi yürüme için küçük tutulur; dokunma ise görünen gövdeye
    /// (biraz pay ile) ve sarı kamp bayrağına göre algılanır.
    /// </summary>
    private bool IsHeroTap(Vector3 worldPos)
    {
        Vector2 point = worldPos;
        if (_heroObj != null)
        {
            foreach (var renderer in _heroObj.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!renderer.enabled || renderer.sprite == null) continue;
                var bounds = renderer.bounds;
                bounds.Expand(new Vector3(0.3f, 0.3f, 10f));
                if (bounds.Contains(new Vector3(point.x, point.y, bounds.center.z))) return true;
            }
        }
        var camp = GameObject.Find("HeroCamp")?.GetComponent<SpriteRenderer>();
        if (camp != null)
        {
            var bounds = camp.bounds;
            bounds.Expand(new Vector3(0.2f, 0.2f, 10f));
            if (bounds.Contains(new Vector3(point.x, point.y, bounds.center.z))) return true;
        }
        return false;
    }

    private void HandleTap(Vector3 worldPos)
    {
        if (IsHeroTap(worldPos))
        {
            HideRemovePrompt();
            GetComponent<BaseWorldUI>()?.ShowHeroInfo();
            return;
        }
        if (BaseWorldLayout.Buildable.Contains(new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y)))
            && BaseMapManager.Instance != null && !BaseMapManager.Instance.IsBuildablePosition(worldPos))
        {
            if (_lockedLandToastRoutine != null) StopCoroutine(_lockedLandToastRoutine);
            _lockedLandToastRoutine = StartCoroutine(ShowLockedLandToast());
            return;
        }
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null)
        {
            var touchedDecor = hit.collider.GetComponentInParent<DecorInstance>();
            if (touchedDecor != null)
            {
                DecorPlacementController.Instance?.ShowActions(touchedDecor);
                return;
            }
            var touchedHut = hit.collider.GetComponent<BuilderHut>();
            if (touchedHut != null)
            {
                HideRemovePrompt();
                FindObjectOfType<BuilderVisualController>()?.ShowHutLabel(touchedHut);
                BuilderPanel.Show();
                return;
            }
            var mapObj = BaseMapManager.Instance.GetMapObject(hit.collider.gameObject);
            if (mapObj != null && mapObj.objectType == MapObjectType.Building)
            {
                HideRemovePrompt();
                BaseMapManager.Instance.ShowBuildingLabel(mapObj);
                mapObj.gameObject.GetComponent<BaseBuildingVisual>()?.Tap();
                // Binaya tıklandı
                if (mapObj.buildingType == BuildingType.GoldMine)
                {
                    FirstBaseGuide.Instance?.OnMineTapped();
                    // Altın madenine tıklandığında içindeki altını topla!
                    if (GameManager.Instance.GoldMineStored > 0)
                    {
                        int collected = GameManager.Instance.CollectGoldFromMineStorage();
                        if (collected > 0)
                        {
                            AudioManager.Instance?.PlaySfx("goldCollect");
                            GetComponent<BaseWorldUI>()?.AnimateMineCollection(
                                mapObj.gameObject.transform.position, collected);
                        }
                    }

                }

                var ui = GetComponent<BaseWorldUI>();
                if (ui != null && mapObj.buildingType.HasValue)
                {
                    ui.ShowBuildingInfo(mapObj.buildingType.Value, mapObj.id);
                }
                return;
            }

            if (mapObj != null && (mapObj.objectType == MapObjectType.Tree || mapObj.objectType == MapObjectType.Rock || mapObj.objectType == MapObjectType.Fence))
            {
                ShowRemovePrompt(mapObj);
                return;
            }

            else if (hit.collider.gameObject == _heroObj)
            {
                HideRemovePrompt();
                // Kahramana tıklandı
                GetComponent<BaseWorldUI>()?.ShowHeroInfo();
                return;
            }
            else if (hit.collider.GetComponentInParent<BaseApprenticeVisual>() != null)
            {
                HideRemovePrompt();
                GetComponent<BaseWorldUI>()?.ShowApprenticeInfo();
                return;
            }
        }

        if (BaseMapManager.Instance != null && !BaseMapManager.Instance.IsBuildablePosition(worldPos))
        {
            if (_lockedLandToastRoutine != null) StopCoroutine(_lockedLandToastRoutine);
            _lockedLandToastRoutine = StartCoroutine(ShowLockedLandToast());
            return;
        }

        // Boşluğa tıklandı - Panelleri kapat (artık tıklamayla yürümeyecek)
        HideRemovePrompt();
        GetComponent<BaseWorldUI>()?.HideAllPanelsPublic();
    }

    /// <summary>Üst barın altındaki kısa bilgi balonu (kilitli arazi, inşaatçı vb.).</summary>
    public void ShowHudToast(string message)
    {
        if (_lockedLandToastRoutine != null) StopCoroutine(_lockedLandToastRoutine);
        _lockedLandToastRoutine = StartCoroutine(ShowToastRoutine(message));
    }

    private System.Collections.IEnumerator ShowLockedLandToast()
    {
        int level = GameManager.Instance != null ? GameManager.Instance.GetMainBaseLevel() : 1;
        int next = BaseWorldLayout.NextLandLevel(level);
        return ShowToastRoutine(next == 0 ? "Kalıcı doğal sınır"
            : $"Ana Üs Sev. {next} ile temizlenir");
    }

    private System.Collections.IEnumerator ShowToastRoutine(string message)
    {
        var pill = _mainCanvas?.transform.Find("SafeArea/HudRoot/LockedLandToast");
        var label = pill?.Find("Text")?.GetComponent<TextMeshProUGUI>();
        var group = pill?.GetComponent<CanvasGroup>();
        if (label == null || group == null) yield break;
        label.text = message;
        var panel = pill.GetComponent<PanelController>();
        panel.Show();
        yield return new WaitForSeconds(1.25f);
        if (panel != null) panel.Hide();
        _lockedLandToastRoutine = null;
    }

    /// <summary>
    /// Ağaç/kaya/çit kaldırma kartı: nesnenin adı, inşaatçı süresi, KALDIR ve VAZGEÇ.
    /// Kart nesnenin üstünde durur, alttaki küçük ok nesneyi gösterir; ekran dışına taşmaz.
    /// </summary>
    private void ShowRemovePrompt(PlacedMapObject obj)
    {
        if (obj == null || obj.gameObject == null)
            return;

        if (_removePrompt != null && _removeTarget == obj)
            return;

        HideRemovePrompt();
        _removeTarget = obj;

        var canvasRect = _mainCanvas.GetComponent<RectTransform>();
        _removePrompt = new GameObject("RemovePrompt", typeof(RectTransform), typeof(Image));
        _removePrompt.transform.SetParent(_mainCanvas.transform, false);
        var rt = (RectTransform)_removePrompt.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(220f, 104f);
        var card = _removePrompt.GetComponent<Image>();
        card.sprite = ReferenceUiVisuals.ToastSprite();
        card.type = Image.Type.Sliced;
        card.color = Color.white;
        var outline = _removePrompt.AddComponent<Outline>();
        outline.effectColor = new Color(0.94f, 0.76f, 0.29f, 0.7f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        string title = obj.objectType == MapObjectType.Tree ? "Ağaç"
            : obj.objectType == MapObjectType.Rock ? "Kaya"
            : obj.objectType == MapObjectType.Fence ? "Çit" : "Nesne";
        bool builderFree = BuilderSystem.Instance == null || BuilderSystem.Instance.HasAvailableBuilder();
        ReferenceUiVisuals.Text(rt, "Title", UiStrings.Upper(title), 16, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, new Vector2(0f, 0.70f), new Vector2(1f, 0.95f),
            new Vector2(10f, 0f), new Vector2(-10f, 0f), title: true);
        ReferenceUiVisuals.Text(rt, "Info", builderFree
                ? "İnşaatçı temizler · 5 sn"
                : "<color=#E07A5F>İnşaatçı şu an meşgul</color>", 12, ReferenceUiVisuals.Muted,
            TextAlignmentOptions.Center, new Vector2(0f, 0.50f), new Vector2(1f, 0.70f),
            new Vector2(10f, 0f), new Vector2(-10f, 0f), bodyBold: true);

        var remove = ReferenceUiVisuals.Button(rt, "RemoveConfirm", "KALDIR", true,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-100f, 10f), new Vector2(-4f, 48f), 14);
        // Yıkıcı eylem: altın düğme kırmızıya çalar.
        remove.GetComponent<Image>().color = new Color(1f, 0.62f, 0.55f);
        remove.interactable = builderFree;
        remove.onClick.AddListener(() =>
        {
            BaseMapManager.Instance?.RequestRemoveObject(obj);
            HideRemovePrompt();
        });
        var cancel = ReferenceUiVisuals.Button(rt, "RemoveCancel", "VAZGEÇ", false,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(4f, 10f), new Vector2(100f, 48f), 14);
        cancel.onClick.AddListener(HideRemovePrompt);

        // Nesneyi gösteren küçük ok.
        var pointer = ReferenceUiVisuals.Image(rt, "Pointer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-9f, -12f), new Vector2(9f, 1f), ReferenceUiVisuals.Background);
        pointer.sprite = PointerSprite();
        pointer.transform.SetAsFirstSibling();

        // Konum: nesnenin üstü; kart ekran içinde kalır, ok nesneyi göstermeye devam eder.
        Vector3 screenPos = Camera.main.WorldToScreenPoint(obj.gameObject.transform.position + Vector3.up * 0.9f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPos);
        float halfW = rt.sizeDelta.x * 0.5f + 8f;
        float x = Mathf.Clamp(localPos.x, canvasRect.rect.xMin + halfW, canvasRect.rect.xMax - halfW);
        float y = Mathf.Clamp(localPos.y + 12f, canvasRect.rect.yMin + 110f, canvasRect.rect.yMax - rt.sizeDelta.y - 90f);
        rt.anchoredPosition = new Vector2(x, y);
        ((RectTransform)pointer.transform).anchoredPosition = new Vector2(Mathf.Clamp(localPos.x - x, -90f, 90f), -5.5f);

        rt.localScale = Vector3.one * 0.7f;
        rt.DOScale(1f, 0.18f).SetEase(Ease.OutBack);
    }

    private static Sprite removePointer;

    private static Sprite PointerSprite()
    {
        if (removePointer != null) return removePointer;
        const int w = 24, h = 16;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float half = (y + 0.5f) / h * (w * 0.5f);
            float alpha = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - w * 0.5f) + 0.5f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        removePointer = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return removePointer;
    }

    private void HideRemovePrompt()
    {
        if (_removePrompt != null)
        {
            Destroy(_removePrompt);
            _removePrompt = null;
        }

        _removeTarget = null;
    }

    // ==================== YARDIMCI UI METOTLARI ====================

    private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private Button CreateButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject go = CreatePanel(parent, name, anchorMin, anchorMax, color);
        Button btn = go.AddComponent<Button>();

        GameObject txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        RectTransform txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 40;

        return btn;
    }

    private Button CreateRoundIconButton(Transform parent, string name, Vector2 anchorPos, Vector2 size, Sprite icon, string label, Color bgColor, Sprite bgSprite = null)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);

        RectTransform rt = root.AddComponent<RectTransform>();
        rt.anchorMin = anchorPos;
        rt.anchorMax = anchorPos;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        Image bg = root.AddComponent<Image>();
        if (bgSprite != null)
        {
            bg.sprite = bgSprite;
            bg.color = Color.white;
            bg.preserveAspect = false;
        }
        else
        {
            bg.sprite = BaseMapManager.MakeCircleSprite(Color.white);
            bg.color = bgColor;
        }

        Button btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;
        if (bgSprite != null)
        {
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            btn.colors = colors;
        }

        if (icon != null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(root.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            // Ikonlari buyutmek icin anchorlari genisletiyoruz (eski: 0.22-0.78, yeni: 0.1-0.9)
            iconRt.anchorMin = new Vector2(0.1f, 0.2f);
            iconRt.anchorMax = new Vector2(0.9f, 0.85f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
        }

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(root.transform, false);
        RectTransform labelRt = labelObj.AddComponent<RectTransform>();
        if (bgSprite != null)
        {
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
        }
        else
        {
            labelRt.anchorMin = new Vector2(0f, -0.35f);
            labelRt.anchorMax = new Vector2(1f, 0f);
        }
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = (bgSprite != null) ? 34f : 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (bgSprite != null)
            tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontSize = fontSize;
        return tmp;
    }

    private Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Sprite sprite, Color fallbackColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = fallbackColor;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        return img;
    }
}

// Basit Kamera Takip Scripti
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector2 boundsMin;
    public Vector2 boundsMax;
    public float smoothSpeed = 5f;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = new Vector3(target.position.x, target.position.y, transform.position.z);

        // Sınırla
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, boundsMin.x, boundsMax.x);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, boundsMin.y, boundsMax.y);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}
