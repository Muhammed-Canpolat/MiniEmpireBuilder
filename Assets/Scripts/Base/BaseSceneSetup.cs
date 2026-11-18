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
        gameObject.AddComponent<BaseAmbientLife>();
        BaseMapManager.Instance.LoadOrSeedTerrainResources(() =>
        {
            SpawnTerrainResources(8, true);
            SpawnTerrainResources(4, false);
        });
        SetupCamera();
        SelectionManager.Reset();   // geri tuşunu BackKeyRouter yönetir; burada yalnız önceki sahneden kalan seçim kaydı sıfırlanır
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevSettings.DebugOverlay) gameObject.AddComponent<BaseDebugOverlay>();
        WalkGridDebug.Install(BaseWalkGrid.Shared);   // 4. oturum (1.1): F6 yürünebilirlik + rotalar
#endif
        StartCoroutine(LogBaseWidthAfterSpawn());
        StartCoroutine(RepairLayoutAfterLoad());

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
            _pressScreenPosition = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            _wasPointerOverUIOnPress = IsPointerOverBlockingUI();
            _pressScreenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
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
            L.T("+0.0 altın/sn", "+0.0 gold/s"),
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
            iconBattle, L.T("Savaşa Git", "To Battle"), new Color(0.85f, 0.25f, 0.2f), buttonBg);

        // ─── İNŞA ET BUTONU (Tool_04) ─────────────────────────
        Button buildBtn = CreateRoundIconButton(hudRoot.transform, "BuildBtn",
            new Vector2(0.86f, 0.22f), new Vector2(320f, 140f),
            iconBuild, L.T("İnşa Et", "Build"), new Color(0.2f, 0.55f, 0.85f), buttonBg);

        // --- Info Panels (Building + Hero) ---
        // 3. oturum (2.4): bina penceresi adadakiyle aynı süslü bileşen (FancyInfoPanel); eski düz koyu kutu kalktı.
        var fancyBuilding = FancyInfoPanel.Build(canvasObj.transform, "BuildingInfo", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.40f));
        GameObject buildingInfoPanel = fancyBuilding.root;
        TextMeshProUGUI bName = fancyBuilding.title;
        TextMeshProUGUI bLvl = fancyBuilding.level;
        TextMeshProUGUI bStats = fancyBuilding.body;
        Button bUpBtn = fancyBuilding.primary;
        TextMeshProUGUI bCost = fancyBuilding.primaryLabel;
        Button bClose = fancyBuilding.close;

        // 4. oturum (2.5): Kahraman/Çırak penceresi de aynı süslü bileşen (eski düz koyu kutu kalktı).
        var fancyHero = FancyInfoPanel.Build(canvasObj.transform, "HeroInfo", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.40f));
        GameObject heroInfoPanel = fancyHero.root;
        TextMeshProUGUI hName = fancyHero.title;
        TextMeshProUGUI hLvl = fancyHero.level;
        TextMeshProUGUI hStats = fancyHero.body;
        Button hUpBtn = fancyHero.primary;
        TextMeshProUGUI hCost = fancyHero.primaryLabel;
        Button hClose = fancyHero.close;

        // --- BaseWorldUI Bağlama ---
        BaseWorldUI uiScript = gameObject.AddComponent<BaseWorldUI>();
        uiScript.SetReferences(
            goldText, battleLvlText, gpsText,
            buildingInfoPanel, bName, bLvl, bStats, bCost, bUpBtn, bClose,
            heroInfoPanel, hName, hLvl, hStats, hCost, hUpBtn, hClose,
            battleBtn, buildBtn
        );
        uiScript.UseFancyBuildingPanel(fancyBuilding);
        uiScript.UseFancyCharacterPanel(fancyHero);

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
            if (canvas.gameObject.name == "SceneCloudTransition") continue;   // bulut geçişi örtüsü kendini bitirir

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
        // Kahverengi toprak yollar kaldırıldı: BaseGroundPaths bilerek eklenmiyor (Instance null; çağrılar '?.' ile güvenli).
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
        // D125 (İş 2): Ana Üs kamerası artık tam ekran çizer (bar kapanınca alt bölge de orman); zemin duvar kadrajının
        // ±16 (alt) / +8 (üst) ötesine uzar. Yalnız görünüm: BaseWorldLayout.Buildable değişmez.
        int bx0 = -14, bx1 = BaseWorldLayout.Width + 14, by0 = -12, by1 = BaseWorldLayout.Height + 12;
        if (BaseCastleWalls.Enabled)
        {
            Rect fr = BaseCastleWalls.FrameOf(BaseCastleWalls.PlannedOuter);
            bx0 = Mathf.Min(bx0, Mathf.FloorToInt(fr.xMin - 16f));
            bx1 = Mathf.Max(bx1, Mathf.CeilToInt(fr.xMax + 16f));
            by0 = Mathf.Min(by0, Mathf.FloorToInt(fr.yMin - 16f));
            by1 = Mathf.Max(by1, Mathf.CeilToInt(fr.yMax + 8f));
        }
        for (int y = by0; y < by1; y++)
        for (int x = bx0; x < bx1; x++)
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
        // D108: PixelCastle duvarları çitin dışına taşar → dış dekor duvarın dış sınırına itilir (RNG sırası aynı),
        // sırası zeminin/duvarın arkasına iner (duvarı örtmesin; dış dekor zemin karolarının dışında kalır).
        bool castle = BaseCastleWalls.Enabled;
        Rect outer = castle ? BaseCastleWalls.PlannedOuter : default;
        for (int side = 0; side < 4; side++)
        {
            int count = side < 2 ? 10 : 5;
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = i % 5 == 0 ? rock : i % 3 == 0 ? bush : tree;
                if (sprite == null) sprite = tree != null ? tree : rock != null ? rock : bush;
                if (sprite == null) continue;
                float x = side == 0 ? -0.3f + (float)random.NextDouble() * 0.3f
                    : side == 1 ? 22.0f + (float)random.NextDouble() * 0.3f
                    : 1.1f + (float)random.NextDouble() * 19.8f;
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
                float leftEdge = castle ? outer.xMin - 0.05f : 0.0f, rightEdge = castle ? outer.xMax + 0.05f : 22.0f;
                float bottomEdge = castle ? outer.yMin - 0.05f : 1.0f, topEdge = castle ? outer.yMax + 0.05f : 37.0f;
                if (side == 0) position.x = Mathf.Min(position.x,
                    leftEdge - renderer.bounds.extents.x);
                else if (side == 1) position.x = Mathf.Max(position.x,
                    rightEdge + renderer.bounds.extents.x);
                else if (side == 2) position.y = Mathf.Min(position.y,
                    bottomEdge - renderer.bounds.extents.y);
                else position.y = Mathf.Max(position.y,
                    topEdge + renderer.bounds.extents.y);
                go.transform.position = position;
                if (castle) renderer.sortingOrder = OutsideOrder(position.y);
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
        bool castle = BaseCastleWalls.Enabled;
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
            renderer.sortingOrder = castle ? OutsideOrder(y) : 6 - Mathf.RoundToInt(y * 0.1f);
            float height = isBush ? 0.9f : 2.2f + (float)random.NextDouble() * 0.9f;
            go.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, renderer.sprite.bounds.size.y));
        }
        if (castle) { CreateForestBelts(root.transform, tree, bush); CreateForestFarBelt(root.transform, tree, bush); }
    }

    /// <summary>D108: duvarın dışındaki dekorun sırası: arka plan karosunun (−30) üstünde, zemin (−20) ve duvarların arkasında.</summary>
    private static int OutsideOrder(float y) => Mathf.Clamp(-25 - Mathf.RoundToInt(y * 0.1f), -29, -21);

    /// <summary>
    /// D108: duvarın üstünde ve altında orman kuşağı (referanstaki çevre). Ayrı sabit tohum → sol/sağ kenarın dizilimi değişmez.
    /// Ağaçlar duvarın dış sınırına girmez; oynanışa dahil değildir (çarpışma yok).
    /// </summary>
    private static void CreateForestBelts(Transform root, Sprite tree, Sprite bush)
    {
        Rect outer = BaseCastleWalls.PlannedOuter;
        var random = new System.Random(52817);
        for (int side = 0; side < 2; side++)
        for (int i = 0; i < 44; i++)
        {
            bool top = side == 0;
            float depth = 0.2f + (float)(random.NextDouble() * random.NextDouble()) * 9f;
            float x = -12f + (float)random.NextDouble() * (BaseWorldLayout.Width + 24f);
            bool isBush = bush != null && random.NextDouble() < 0.3;
            float height = isBush ? 0.9f : 2.0f + (float)random.NextDouble() * 0.9f;
            float shade = Mathf.Lerp(0.92f, 0.62f, Mathf.Clamp01(depth / 8f));
            var go = new GameObject((top ? "MarginTop_" : "MarginBottom_") + i);
            go.transform.SetParent(root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = isBush ? bush : tree;
            renderer.color = new Color(shade, shade * 1.02f, shade * 0.95f);
            go.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, renderer.sprite.bounds.size.y));
            // Sprite sınırı duvarın dış sınırının tamamen dışında kalır (pivot ne olursa olsun).
            go.transform.position = new Vector3(x, 0f, 0f);
            Bounds b = renderer.bounds;
            float y = top ? outer.yMax + 0.1f + depth + (0f - b.min.y)
                : outer.yMin - 0.1f - depth - (b.max.y - 0f);
            go.transform.position = new Vector3(x, y, 0f);
            renderer.sortingOrder = OutsideOrder(y);
        }
    }

    /// <summary>D125 (İş 2): alt/üst dış zemini dolduran ek orman kuşağı. Ayrı sabit tohum (61337): mevcut dizilimler değişmez, her açılışta aynı yer.
    /// ≤32 SpriteRenderer; çarpışma/MapObjectTag yok, kayda girmez.</summary>
    public const int ForestFarBeltSeed = 61337;
    private static void CreateForestFarBelt(Transform parent, Sprite tree, Sprite bush)
    {
        Sprite rock = Resources.Load<Sprite>("BaseDecor/rock");
        Rect outer = BaseCastleWalls.PlannedOuter;
        var root = new GameObject("ForestFarBelt");
        root.transform.SetParent(parent, false);
        var random = new System.Random(ForestFarBeltSeed);
        for (int band = 0; band < 2; band++)
        {
            bool top = band == 1;
            int count = top ? 10 : 22;
            for (int i = 0; i < count; i++)
            {
                double roll = random.NextDouble();
                Sprite sprite = roll < 0.70 ? tree : roll < 0.90 ? bush : rock;
                if (sprite == null) sprite = tree;
                float x = outer.xMin - 6f + (float)random.NextDouble() * (outer.width + 12f);
                float y = top ? outer.yMax + 3f + (float)random.NextDouble() * 4f
                    : outer.yMin - 14f + (float)random.NextDouble() * 8f;
                float height = sprite == rock ? 0.65f : sprite == bush ? 0.9f : 2.0f + (float)random.NextDouble() * 0.9f;
                float depth = Mathf.Clamp01(Mathf.Abs(y - (top ? outer.yMax : outer.yMin)) / 14f);
                float shade = Mathf.Lerp(0.85f, 0.62f, depth);
                var go = new GameObject("FarBelt_" + (top ? "T" : "B") + i);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(x, y, 0f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(shade, shade * 1.02f, shade * 0.95f);
                renderer.sortingOrder = OutsideOrder(y);
                go.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, sprite.bounds.size.y));
            }
        }
    }

    private void SpawnTerrainResources(int count, bool trees)
    {
        int created = 0;
        for (int attempt = 0; attempt < 500 && created < count; attempt++)
        {
            int quadrant = created % 4;
            float minX = quadrant % 2 == 0 ? 1.7f : width * 0.5f;
            float maxX = quadrant % 2 == 0 ? width * 0.5f : width - 1.7f;
            float minY = quadrant < 2 ? 2.7f : height * 0.5f;
            float maxY = quadrant < 2 ? height * 0.5f : height - 2.7f;
            Vector3 position = new Vector3(Random.Range(minX, maxX),
                Random.Range(minY, maxY), 0f);
            if (!BaseMapManager.Instance.CanSpawnResourceAt(position, trees ? BaseMapManager.TreeClearRadius : 0.8f)) continue;
            if (trees) BaseMapManager.Instance.SpawnTree(position);
            else BaseMapManager.Instance.SpawnRock(position);
            created++;
        }
    }

    /// <summary>
    /// Kilitli (açılmamış) arazideki doğa engelleri. 12. oturum (D123): görseller ve ölçek YERLEŞTİRİLEN dekorla aynı
    /// (DecorData.Visual + DecorScale; eskiden Resources/BaseDecor'daki KIRPILMIŞ kaya/kütük/çalı sprite'ları tam kare ölçeğiyle
    /// büyütülüyordu: kütük ~4×, kaya/çalı ~2×). Her nesne hücre merkezine ve dekorla aynı taban hizasına (VerticalOffset) oturur;
    /// türe göre yarıçap kuralıyla komşusuna binen nesne çizilmez. Rastgele akış (tohum, çekim sırası) değişmedi: hangi hücrede hangi
    /// türün olduğu ve kayıtlı removedNatureIds kimlikleri aynı kalır.
    /// </summary>
    private void CreateLockedDecor()
    {
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.GetMainBaseLevel() : 1;
        var saveData = GameManager.Instance?.PlayerData;
        // Doğa genişlemeyle silinmez: dizilim, ilk spawn anındaki (taban) temiz alana göre sabittir.
        // Eski kayıtlarda taban = mevcut seviye; zaten temizlenmiş alan temiz kalır.
        if (saveData != null && saveData.natureBaselineLevel <= 0) saveData.natureBaselineLevel = currentLevel;
        var baseline = BaseWorldLayout.CleanAtLevel(saveData != null ? saveData.natureBaselineLevel : currentLevel);
        var clean = BaseWorldLayout.CleanForTier(LandExpansion.OpenedTier);
        var random = new System.Random(76143);
        var centers = new System.Collections.Generic.List<Vector2>();
        var placed = new System.Collections.Generic.List<(Vector2 pos, float radius)>();
        for (int i = 0; i < 18; i++)
            centers.Add(new Vector2(random.Next(BaseWorldLayout.Buildable.xMin, BaseWorldLayout.Buildable.xMax), random.Next(2, 36)));
        foreach (Vector2Int cell in EnumerateInteriorCells())
        {
            if (baseline.Contains(cell)) continue;
            float nearest = 20f;
            foreach (var center in centers)
                nearest = Mathf.Min(nearest, Vector2.Distance(center, cell));
            // Seyrek doku + birkaç belirgin küme; önceki eşit dağılımlı yoğun görünüm yerine.
            float density = Mathf.Lerp(0.05f, 0.62f, Mathf.Clamp01((3.6f - nearest) / 3.2f));
            if (cell.y < 4 || cell.y >= 34) density = Mathf.Max(density, 0.42f);
            if (random.NextDouble() > density) continue;
            int spriteIndex = random.Next(4);
            var kind = spriteIndex == 0 ? NatureKind.Tree : spriteIndex == 1 ? NatureKind.Bush
                : spriteIndex == 2 ? NatureKind.Rock : NatureKind.Stump;
            bool removed = saveData?.removedNatureIds != null
                && saveData.removedNatureIds.Contains(NatureObstacle.MakeId(kind, cell));
            random.NextDouble(); random.NextDouble();   // eski konum titremesi: akış korunur, değer kullanılmaz (ızgara hizası)
            float variety = DecorScale.Variety((float)random.NextDouble());
            if (removed) continue;
            var decorData = DecorScale.Find(LockedDecorId(kind, cell));
            Sprite sprite = decorData != null ? decorData.Visual : null;
            if (sprite == null) continue;
            var pos = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            float radius = LockedDecorRadius(kind);
            bool crowded = false;
            foreach (var other in placed)
                if ((other.pos - pos).sqrMagnitude < (other.radius + radius) * (other.radius + radius)) { crowded = true; break; }
            if (crowded) continue;
            placed.Add((pos, radius));

            var go = new GameObject("LockedObstacle_" + cell.x + "_" + cell.y);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;
            float size = DecorScale.For(decorData, sprite) * variety;
            go.transform.localScale = Vector3.one * size;
            // Dekorla aynı yerleşim kuralı: görünen alt kenar hücre tabanına oturur (DecorInstance.SetCell, footprint 1x1).
            float offset = decorData.VerticalOffset(sprite, size);
            go.transform.position = new Vector3(pos.x, cell.y + 0.5f + offset, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one * (0.9f / size);
            box.offset = new Vector2(0f, -offset / size);   // tıklama/doluluk hücrenin kendisinde kalır
            var nature = go.AddComponent<NatureObstacle>();
            nature.kind = kind;
            nature.cell = cell;
            _lockedDecor[cell] = go;
        }
        RefreshLockedShade(clean);
    }

    /// <summary>
    /// Kilitli arazi dekorunun katalog kimliği; varyant hücreden kararlı seçilir (rastgele akışı tüketmez). Yalnız bir hücreye
    /// sığan küçük varyantlar: kaya 1/3 (0,9–1,1 u), çalı 1/2/4 (0,9–1,3 u); kaya4 (1,7 u) ve çalı3 (1,5 u) kullanılmaz.
    /// </summary>
    private static readonly string[] LockedRocks = { "rock", "rock3" };
    private static readonly string[] LockedBushes = { "bush", "bush2", "bush4" };
    private static string LockedDecorId(NatureKind kind, Vector2Int cell)
    {
        float unit = BaseMapManager.StableUnit(cell.x + "_" + cell.y, 7);
        switch (kind)
        {
            case NatureKind.Tree: return "tree";
            case NatureKind.Bush: return LockedBushes[Mathf.Min(LockedBushes.Length - 1, (int)(unit * LockedBushes.Length))];
            case NatureKind.Rock: return LockedRocks[Mathf.Min(LockedRocks.Length - 1, (int)(unit * LockedRocks.Length))];
            default: return "stump";
        }
    }

    /// <summary>Çakışma yarıçapı (dünya birimi, görünen gövdenin yarısı kadar): iki nesne arası en az rA + rB.</summary>
    public static float LockedDecorRadius(NatureKind kind)
    {
        switch (kind)
        {
            case NatureKind.Tree: return 1.1f;   // eski ağaç-ağaç kuralı (2,2 u) korunur
            case NatureKind.Bush: return 0.62f;
            case NatureKind.Rock: return 0.55f;
            default: return 0.36f;
        }
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
            go.transform.position = new Vector3(BaseWorldLayout.Buildable.center.x, (start + end) * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0f, 0f, 0f, 0.25f);
            sr.sortingOrder = 4;
            go.transform.localScale = new Vector3(BaseWorldLayout.Buildable.width / square.bounds.size.x,
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
        fog.transform.position = new Vector3(BaseWorldLayout.Buildable.center.x, clean.yMax, 0f);
        fog.transform.localScale = new Vector3(BaseWorldLayout.Buildable.width, 1.2f, 1f);
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
        var clean = BaseWorldLayout.CleanForTier(LandExpansion.OpenedTier);   // 6. oturum (4.1)
        RefreshLockedShade(clean);
        // Doğa nesneleri genişlemede silinmez: yerleştirmeyi engeller, altın karşılığı kaldırılır (NatureObstacle).
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
    /// <summary>Kahramanın kamp bayrağına göre başlangıç yeri (bayrak 2×2 hücrenin ortasında; kahraman solunda durur).</summary>
    public static readonly Vector2 HeroCampOffset = new Vector2(-2.3f, -0.4f);

    private void SpawnHero()
    {
        _heroObj = new GameObject("Hero");
        Vector2 camp = BaseWorldLayout.Get("camp").center + HeroCampOffset;   // 3. oturum (1.4): bayrağın yanında, üstünde değil
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
        // Üs kamerası: tek parmak kaydırma, sıkıştırarak yakınlaştırma (harita sınırlarına kenetli).
        if (camera.GetComponent<BaseCameraController>() == null) camera.gameObject.AddComponent<BaseCameraController>();
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
        else BaseCastleWalls.ApplyFraming(camera);   // D108: duvar + orman kadrajı (bar yoksa tam ekran)
    }

    /// <summary>
    /// Bina, kulübe ve dekor yüklendikten (birkaç kare sonra) çakışan/açık arazi dışındaki nesneleri
    /// en yakın boş geçerli yere taşır; sığmayan dekorun bedelini iade eder.
    /// </summary>
    private System.Collections.IEnumerator RepairLayoutAfterLoad()
    {
        for (int frame = 0; frame < 4; frame++) yield return null;
        if (BaseMapManager.Instance != null) BaseMapManager.Instance.ValidateOccupancy();   // 5. oturum (1.1): hayalet kayıtlar
        if (BaseMapManager.Instance != null) BaseMapManager.Instance.RepairLayout();
        // 3. oturum (1.4): sprite'lar (animasyon ilk kare, gölge) otursun; sonra görsel çakışma onarımı
        yield return new WaitForSecondsRealtime(0.5f);   // açılış ölçek animasyonları bitsin
        if (BaseMapManager.Instance != null) BaseMapManager.Instance.RepairVisualLayout();
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
        SelectionManager.Clear();   // 5. oturum (2.1): açık eylem menüsü/vurgu taşıma başlarken kalkar
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
    private Vector2 _pressScreenPosition;

    /// <summary>Basıldığı yerden 10 pikselden fazla kaydıysa (kamera kaydırma/sürükleme) bu dokunuş tap sayılmaz.</summary>
    private bool DraggedSincePress(Vector2 releasePosition)
        => (releasePosition - _pressScreenPosition).sqrMagnitude > BaseCameraController.TapSlopPixels * BaseCameraController.TapSlopPixels;

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
        if (PlacementController.Instance != null && PlacementController.Instance.IsPlacing)
            return; // yerleştirme sırasında dokunuşlar hayaleti/kamerayı yönetir, bina seçmez
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (_wasPointerOverUIOnPress || DraggedSincePress(Mouse.current.position.ReadValue())) return;
            if (IsPointerOverBlockingUI()) return;

            if (LongPressHandler.Instance.WasFiredThisPress) return; // Uzun basma işlemi gerçekleşmişse tap'i yoksay

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            HandleTap(worldPos);
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            if (_wasPointerOverUIOnPress || DraggedSincePress(Touchscreen.current.primaryTouch.position.ReadValue())) return;
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
    /// <summary>
    /// Kahramanın dokunma dikdörtgeni: animasyon karesinin tamamı değil gövdesi (Tiny Swords 192 px karede figür ortadaki ~%40×%55),
    /// en az 0,8×1,1 birim. Eskiden kare sınırı +0,3 paydı (~4×4 birim); çevresindeki ağaç/kayalar dokunulamıyordu.
    /// </summary>
    public Rect HeroTapRect()
    {
        var sr = _heroObj != null ? _heroObj.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null)
        {
            Vector2 p = _heroObj != null ? (Vector2)_heroObj.transform.position : Vector2.zero;
            return new Rect(p.x - 0.4f, p.y - 0.45f, 0.8f, 1.1f);
        }
        var b = sr.bounds;
        Vector2 size = new Vector2(Mathf.Max(0.8f, b.size.x * 0.4f), Mathf.Max(1.1f, b.size.y * 0.55f));
        Vector2 center = (Vector2)b.center + new Vector2(0f, -b.size.y * 0.04f);
        return new Rect(center - size * 0.5f, size);
    }

    /// <summary>Dokunulan noktadaki küçük hedef (dekor, kilitsiz arazideki ağaç/çalı/kaya/kütük, harita ağacı/kayası); birden çoksa merkezi en yakın.</summary>
    private bool TrySmallTargetTap(Vector2 point)
    {
        Collider2D best = SmallTargetAt(point);
        if (best == null) return false;
        var decor = best.GetComponentInParent<DecorInstance>();
        if (decor != null) { DecorPlacementController.Instance?.ShowActions(decor); return true; }
        var nature = best.GetComponent<NatureObstacle>();
        if (nature != null) { ShowNaturePrompt(nature); return true; }
        ShowRemovePrompt(BaseMapManager.Instance.GetMapObject(best.gameObject));
        return true;
    }

    /// <summary>
    /// D115 (madde 18): dokunulan küçük hedef. Dekor çarpıştırıcısı tüm sprite karesidir (saydam pay dahil); eskiden noktayı içeren
    /// çarpıştırıcılardan MERKEZİ en yakın olan seçiliyordu ve dokunulan dekor yerine komşusu (ya da arazi ağacı) seçilip menü onun
    /// üstünde açılıyordu. Şimdi önce noktayı GÖRÜNÜR sınırı içinde tutanlar aday olur; birden çoksa ekranda en öndeki (çizim sırası),
    /// eşitse görünür merkezi en yakın. Görünür sınırda aday yoksa eski kural (merkezi en yakın çarpıştırıcı) geçerlidir.
    /// </summary>
    internal Collider2D SmallTargetAt(Vector2 point)
    {
        Collider2D best = null, fallback = null;
        float bestDistance = float.MaxValue, fallbackDistance = float.MaxValue;
        int bestLayer = int.MinValue, bestOrder = int.MinValue;
        foreach (var c in Physics2D.OverlapPointAll(point))
        {
            bool small = c.GetComponentInParent<DecorInstance>() != null || c.GetComponent<NatureObstacle>() != null;
            if (!small && BaseMapManager.Instance != null)
            {
                var obj = BaseMapManager.Instance.GetMapObject(c.gameObject);
                small = obj != null && (obj.objectType == MapObjectType.Tree || obj.objectType == MapObjectType.Rock);
            }
            if (!small) continue;
            float fd = ((Vector2)c.bounds.center - point).sqrMagnitude;
            if (fd < fallbackDistance) { fallbackDistance = fd; fallback = c; }
            var sr = c.GetComponentInParent<SpriteRenderer>();   // önce kendisi, yoksa üst nesne
            Bounds visible = sr != null ? BaseVisualBounds.VisibleBounds(sr) : c.bounds;
            visible.Expand(new Vector3(0.12f, 0.12f, 0f));
            // Doğa/arazi engellerinin çarpıştırıcısı zaten görselden küçük: o çarpıştırıcıya düşen dokunuş görünür sayılır.
            bool inside = c.GetComponentInParent<DecorInstance>() == null
                || visible.Contains(new Vector3(point.x, point.y, visible.center.z));
            if (!inside) continue;
            int layer = sr != null ? SortingLayer.GetLayerValueFromID(sr.sortingLayerID) : 0;
            int order = sr != null ? sr.sortingOrder : 0;
            float d = ((Vector2)visible.center - point).sqrMagnitude;
            bool front = layer > bestLayer || layer == bestLayer && (order > bestOrder || order == bestOrder && d < bestDistance);
            if (!front) continue;
            best = c; bestLayer = layer; bestOrder = order; bestDistance = d;
        }
        return best != null ? best : fallback;
    }

    private bool IsHeroTap(Vector3 worldPos)
    {
        Vector2 point = worldPos;
        if (_heroObj != null && HeroTapRect().Contains(point)) return true;
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
        // 4. oturum (1.2): her dünya dokunuşu önce açık seçimi (dekor eylemleri, kaldırma istemi, bina penceresi) kapatır;
        // aynı dekora dokunmak seçimi kapatıp bırakır (aç/kapa).
        var previous = SelectionManager.Current;
        SelectionManager.Clear();
        // 5. oturum (2.1): açık bina menüsü varken aynı binaya (maden hariç: altın toplar) tekrar dokunmak menüyü kapatıp bırakır.
        if (previous is PlacedMapObject prevBuilding && prevBuilding.gameObject != null && prevBuilding.buildingType != BuildingType.GoldMine)
        {
            var hitAgain = Physics2D.Raycast(worldPos, Vector2.zero).collider;
            if (hitAgain != null && BaseMapManager.Instance != null && BaseMapManager.Instance.GetMapObject(hitAgain.gameObject) == prevBuilding) return;
        }
        if (previous is DecorInstance prevDecor && prevDecor != null)
        {
            var again = Physics2D.Raycast(worldPos, Vector2.zero).collider?.GetComponentInParent<DecorInstance>();
            if (again == prevDecor) return;
        }
        if (BaseWorldLayout.Buildable.Contains(new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y)))
            && BaseMapManager.Instance != null && !BaseMapManager.Instance.IsBuildablePosition(worldPos))
        {
            if (LandExpansion.ShowFor(worldPos)) return;   // 6. oturum (4.1): alanın tamamı + koşul + büyüklük + AÇ
            if (_lockedLandToastRoutine != null) StopCoroutine(_lockedLandToastRoutine);
            _lockedLandToastRoutine = StartCoroutine(ShowLockedLandToast());
            return;
        }
        // Küçük hedefler (ağaç, çalı, kaya, kütük, dekor) büyük alanlardan (bina görsel kutusu, kahraman) önce gelir: bina ve kahramanın
        // dokunma alanı saydam kenarlarıyla geniştir, yanlarındaki ağaç/kayaya dokunmak onların penceresini açıyordu.
        if (TrySmallTargetTap(worldPos)) return;
        if (IsHeroTap(worldPos))
        {
            HideRemovePrompt();
            GetComponent<BaseWorldUI>()?.ShowHeroInfo();
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
            // Çırak madenin içinde durur ve yürür: dokunuş gövdesine (dar dokunma alanı) denk geliyorsa Çırak penceresi,
            // madenin geri kalanı maden (altın toplama). Çakışan iki alandan hangisinin önce döneceği belli olmadığından açıkça bakılır.
            foreach (var overlap in Physics2D.OverlapPointAll(worldPos))
                if (overlap.GetComponent<BaseApprenticeTap>() != null)
                {
                    HideRemovePrompt();
                    GetComponent<BaseWorldUI>()?.ShowApprenticeInfo();
                    return;
                }
            var touchedNature = hit.collider.GetComponent<NatureObstacle>();
            if (touchedNature != null)
            {
                ShowNaturePrompt(touchedNature);
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

                // 5. oturum (2.1): binaya dokunmak artık binanın üstünde eylem menüsünü açar (YÜKSELT/BİLGİ + TAŞI); pencere düğmeden açılır.
                ShowBuildingActions(mapObj);
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
            else if (hit.collider.GetComponentInParent<BaseApprenticeVisual>() != null || hit.collider.GetComponent<BaseApprenticeTap>() != null)
            {
                HideRemovePrompt();
                GetComponent<BaseWorldUI>()?.ShowApprenticeInfo();
                return;
            }
        }

        if (BaseMapManager.Instance != null && !BaseMapManager.Instance.IsBuildablePosition(worldPos))
        {
            if (LandExpansion.ShowFor(worldPos)) return;
            if (_lockedLandToastRoutine != null) StopCoroutine(_lockedLandToastRoutine);
            _lockedLandToastRoutine = StartCoroutine(ShowLockedLandToast());
            return;
        }

        // Boşluğa tıklandı - Panelleri kapat (artık tıklamayla yürümeyecek)
        HideRemovePrompt();
        GetComponent<BaseWorldUI>()?.HideAllPanelsPublic();
    }

    /// <summary>
    /// Bina eylem menüsü: YÜKSELT (bina/yükseltme penceresi; Kart Atölyesi'nde üretim ekranı) ve TAŞI (Ana Üs kalesi taşınamaz → yalnız ilki).
    /// Menü binanın üstünde durur, bina altın parlar; boş yere dokunmak/geri tuşu kapatır.
    /// </summary>
    public WorldActionMenu ShowBuildingActions(PlacedMapObject mapObj)
    {
        if (mapObj == null || mapObj.gameObject == null || !mapObj.buildingType.HasValue) return null;
        var type = mapObj.buildingType.Value;
        var safe = _mainCanvas != null ? _mainCanvas.transform.Find("SafeArea") : null;
        if (safe == null) return null;
        var ui = GetComponent<BaseWorldUI>();
        var items = new System.Collections.Generic.List<WorldActionMenu.Item>
        {
            new WorldActionMenu.Item
            {
                id = "info", icon = CampaignSceneSetup.Art("ui_icon_hammer"), label = L.T("YÜKSELT", "UPGRADE"),
                action = () =>
                {
                    if (type == BuildingType.CardWorkshop && CardWorkshopPanel.Open(mapObj.id)) return;
                    ui?.ShowBuildingInfo(type, mapObj.id);
                }
            }
        };
        if (type != BuildingType.MainBase)
            items.Add(new WorldActionMenu.Item
            {
                id = "move", icon = CampaignSceneSetup.Art("ui_icon_move"), label = L.T("TAŞI", "MOVE"),
                action = () => PlacementController.Instance?.BeginMove(mapObj)
            });
        // D121 (bug 11): 3. düğme DEPOYA (Ana Üs kalesi ve ilk rehber bitmeden yok; inşa/yükseltme sürerken gri + sebep).
        var depot = DepotMenuItem(DepotService.RegionBase, DepotService.CategoryBuilding, type.ToString(), mapObj.id, BaseDepotAdapter.Instance,
            type == BuildingType.GoldMine ? L.T("Depoya kaldırılsın mı? Depodayken altın üretmez.", "Move to storage? It won't produce gold while stored.") : null,
            ShowHudToast);
        if (depot != null) items.Add(depot);
        var go = mapObj.gameObject;
        var sr = go.GetComponent<SpriteRenderer>();
        return WorldActionMenu.Show(safe, () => BuildingBounds(go), Camera.main, items, "BuildingActions", mapObj, sr);
    }

    /// <summary>
    /// D121: ortak DEPOYA menü düğmesi (Ana Üs binası/dekoru ve Ada binası aynı tanımı kullanır). Hariç türde null. Onay sorulur; reddedilen
    /// durumda düğme gri, dokununca sebep <paramref name="toast"/> ile tek satır gösterilir.
    /// </summary>
    public static WorldActionMenu.Item DepotMenuItem(int region, int category, string type, string id, IDepotRegion adapter, string question,
        System.Action<string> toast)
    {
        if (!DepotService.ShowButton(region, category, type)) return null;
        string blocked = DepotService.BusyReason(region, category, id);
        return new WorldActionMenu.Item
        {
            id = "depot", icon = DepotIcon, label = L.T("DEPOYA", "STORE"),
            confirm = blocked == null, question = question ?? L.T("Depoya kaldırılsın mı?", "Move to storage?"),
            blockedReason = blocked, onBlocked = reason => toast?.Invoke(reason),
            action = () =>
            {
                if (DepotService.Store(adapter, id, category, out string why))
                    toast?.Invoke(L.T("Depoya kaldırıldı · İnşa Et > DEPO", "Stored · Build > STORAGE"));
                else toast?.Invoke(why);
            }
        };
    }

    /// <summary>D121: depo simgesi (Cainos tahta sandık karesi, Tools/campaign_art/depot_icon.py); yoksa çekiç.</summary>
    public static Sprite DepotIcon => CampaignSceneSetup.Art("ui_icon_depot") ?? CampaignSceneSetup.Art("ui_icon_hammer");

    /// <summary>Binanın ekranda görünen dünya sınırı (kök görsel; yoksa çarpıştırıcı).</summary>
    private static Bounds BuildingBounds(GameObject go)
    {
        if (go == null) return new Bounds();
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null && sr.color.a > 0.1f) return sr.bounds;
        var col = go.GetComponent<Collider2D>();
        return col != null ? col.bounds : new Bounds(go.transform.position, Vector3.one);
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
        return ShowToastRoutine(L.T("Kalıcı doğal sınır", "Permanent natural border"));
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
        SelectionManager.Select(RemovePromptToken, HideRemovePrompt);   // 4. oturum (1.2)

        string title = obj.objectType == MapObjectType.Tree ? L.T("Ağaç", "Tree")
            : obj.objectType == MapObjectType.Rock ? L.T("Kaya", "Rock")
            : obj.objectType == MapObjectType.Fence ? L.T("Çit", "Fence") : L.T("Nesne", "Object");
        bool builderFree = BuilderSystem.Instance == null || BuilderSystem.Instance.HasAvailableBuilder();
        var subject = obj.gameObject.GetComponent<SpriteRenderer>();
        // 6. oturum (4.2): büyük kart yerine küçük eylem menüsü; süre öğenin büyüklüğüne göre (config).
        var size = obj.objectType == MapObjectType.Tree ? NatureClear.Size.Tree : NatureClear.Size.Rock;
        var items = new System.Collections.Generic.List<WorldActionMenu.Item>
        {
            new WorldActionMenu.Item
            {
                id = "clear", icon = CampaignSceneSetup.Art("ui_icon_hammer"), label = L.T("KALDIR", "CLEAR"),
                costIcon = CampaignSceneSetup.Art("ui_icon_clock"), costText = NatureClear.TimeText(size),
                action = () =>
                {
                    if (!builderFree) { ShowHudToast(L.T("İnşaatçı şu an meşgul", "Builder is busy")); return; }
                    BaseMapManager.Instance?.RequestRemoveObject(obj);
                    HideRemovePrompt();
                },
            },
        };
        var safe = _mainCanvas != null ? _mainCanvas.transform.Find("SafeArea") : null;
        WorldActionMenu.Show(safe, () => subject != null ? BaseVisualBounds.VisibleBounds(subject) : new Bounds(obj.gameObject.transform.position, Vector3.one),   // D115
            Camera.main, items, "RemoveActions", obj, subject);
    }

    /// <summary>Doğa nesnesi kartı: "Kaldır (X altın)" ve vazgeç; altın yetmezse düğme kapalı.</summary>
    private void ShowNaturePrompt(NatureObstacle nature)
    {
        // 6. oturum (4.2): büyük kart yerine öğenin hemen üstünde açılan küçük eylem menüsü (dekor/bina menüsüyle aynı aile).
        if (nature == null) return;
        HideRemovePrompt();
        _removeTarget = null;
        int cost = nature.RemoveCost;
        var size = NatureClear.SizeOf(nature.kind);
        var items = new System.Collections.Generic.List<WorldActionMenu.Item>
        {
            new WorldActionMenu.Item
            {
                id = "clear", icon = CampaignSceneSetup.Art("ui_icon_hammer"), label = L.T("KALDIR", "CLEAR"),
                costIcon = CampaignSceneSetup.Art("ui_icon_coin"), costText = $"{cost} · {NatureClear.TimeText(size)}",
                action = () =>
                {
                    if (GameManager.Instance != null && GameManager.Instance.Gold < cost) { ShowHudToast(L.T("Yeterli altın yok", "Not enough gold")); return; }
                    if (!nature.RequestClear()) ShowHudToast(L.T("Zaten sırada", "Already queued"));
                    else if (GameManager.Instance != null && !GameManager.Instance.HasFreeBuilder) ShowHudToast(L.T("İnşaatçı boşalınca başlar", "Starts when a builder is free"));
                },
            },
        };
        var safe = _mainCanvas != null ? _mainCanvas.transform.Find("SafeArea") : null;
        var sr = nature.GetComponent<SpriteRenderer>();
        WorldActionMenu.Show(safe, () => sr != null ? BaseVisualBounds.VisibleBounds(sr) : new Bounds(nature.transform.position, Vector3.one),
            Camera.main, items, "NatureActions", nature, sr);   // D115
    }

    /// <summary>
    /// Kaldırma kartı (ağaç, kaya, çalı, kütük, çit): bina pencereleriyle aynı Tiny Swords kâğıt + kurdele başlık; solda nesnenin kendi
    /// görseli, sağda açıklama ve bedel/süre satırı (simgeli; yetmiyorsa kırmızı), altta Tiny Swords kırmızı KALDIR ve mavi VAZGEÇ.
    /// Kâğıt renkli ok nesneyi gösterir; kart ekranda kalır. (Eski hâl: koyu kutu, iki satıra kırılan "KALDIR (15 ALTIN)" düğmesi.)
    /// </summary>
    private void BuildRemoveCard(string title, Sprite subject, string info, bool warn, Sprite costIcon, string costText, bool costOk,
        bool canConfirm, UnityEngine.Events.UnityAction onConfirm, Vector3 worldPos)
    {
        var canvasRect = _mainCanvas.GetComponent<RectTransform>();
        var paper = FancyInfoPanel.NineSlice(_mainCanvas.transform, "RemovePrompt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
        paper.raycastTarget = true;
        _removePrompt = paper.gameObject;
        var rt = (RectTransform)_removePrompt.transform;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(292f, 172f);

        var ribbon = FancyInfoPanel.NineSlice(rt, "Ribbon", new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0f, -30f), new Vector2(0f, 18f),
            "ui_ribbon", Color.white, 3f);
        var titleText = ReferenceUiVisuals.Text(ribbon.transform, "Title", UiStrings.Upper(title), 18, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(36f, 7f), new Vector2(-36f, -3f), title: true);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 11f;
        titleText.fontSizeMax = 18f;

        // Nesnenin kendi görseli (hafif gölgeli yuvada)
        var slot = ReferenceUiVisuals.Image(rt, "SubjectSlot", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -112f), new Vector2(84f, -46f),
            new Color(0.55f, 0.42f, 0.25f, 0.18f));
        slot.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        slot.raycastTarget = false;
        var picture = ReferenceUiVisuals.Image(slot.transform, "Subject", Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f), Color.white);
        picture.sprite = subject;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        picture.gameObject.SetActive(subject != null);

        var infoText = ReferenceUiVisuals.Text(rt, "Info", info, 15, warn ? FancyInfoPanel.Missing : FancyInfoPanel.Ink, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(96f, -80f), new Vector2(-16f, -40f), false, true);
        infoText.textWrappingMode = TextWrappingModes.Normal;
        infoText.enableAutoSizing = true;
        infoText.fontSizeMin = 11f;
        infoText.fontSizeMax = 15f;
        var icon = ReferenceUiVisuals.Image(rt, "CostIcon", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -110f), new Vector2(122f, -84f), Color.white);
        icon.sprite = costIcon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.gameObject.SetActive(costIcon != null);
        ReferenceUiVisuals.Text(rt, "Cost", costText, 19, costOk ? FancyInfoPanel.Ink : FancyInfoPanel.Missing, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(costIcon != null ? 128f : 96f, -112f), new Vector2(-16f, -82f), true).raycastTarget = false;

        var remove = FancyInfoPanel.TsButton(rt, "RemoveConfirm", L.T("KALDIR", "REMOVE"), true, new Vector2(0.05f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 12f), new Vector2(-3f, 56f), 17, out _);
        remove.interactable = canConfirm;
        remove.onClick.AddListener(onConfirm);
        var cancel = FancyInfoPanel.TsButton(rt, "RemoveCancel", L.T("VAZGEÇ", "CANCEL"), false, new Vector2(0.5f, 0f), new Vector2(0.95f, 0f),
            new Vector2(3f, 12f), new Vector2(0f, 56f), 17, out _);
        cancel.onClick.AddListener(HideRemovePrompt);

        // Nesneyi gösteren kâğıt renkli ok
        var pointer = ReferenceUiVisuals.Image(rt, "Pointer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-11f, -14f), new Vector2(11f, 3f), new Color32(0xEE, 0xE1, 0xC6, 0xFF));
        pointer.sprite = PointerSprite();
        pointer.raycastTarget = false;
        pointer.transform.SetAsFirstSibling();

        // Konum: nesnenin üstü; kart ekran içinde kalır, ok nesneyi göstermeye devam eder.
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 0.9f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out Vector2 localPos);
        float halfW = rt.sizeDelta.x * 0.5f + 8f;
        float x = Mathf.Clamp(localPos.x, canvasRect.rect.xMin + halfW, canvasRect.rect.xMax - halfW);
        float y = Mathf.Clamp(localPos.y + 14f, canvasRect.rect.yMin + 110f, canvasRect.rect.yMax - rt.sizeDelta.y - 110f);
        rt.anchoredPosition = new Vector2(x, y);
        ((RectTransform)pointer.transform).anchoredPosition = new Vector2(Mathf.Clamp(localPos.x - x, -110f, 110f), -5.5f);
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

    private static readonly object RemovePromptToken = new object();

    private void HideRemovePrompt()
    {
        SelectionManager.Release(RemovePromptToken);
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
