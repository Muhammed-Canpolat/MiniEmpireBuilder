using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using DG.Tweening;

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


    [Header("Map Settings")]
    public int width = 30;
    public int height = 20;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetupSystems();

        SetupUI();
        GenerateWorld();

        // Kayıtlı binaları yükle, yoksa ana üs + maden spawnla
        if (GameManager.Instance?.PlayerData?.placedBuildings?.Count > 0)
        {
            BaseMapManager.Instance.LoadFromPlayerData();
        }
        else
        {
            // Yeni oyun
            BaseMapManager.Instance.SpawnBuilding(BuildingType.MainBase, new Vector3(width / 2f, height / 2f, 0f));
            BaseMapManager.Instance.SpawnBuilding(BuildingType.GoldMine, new Vector3(width / 2f + 3f, height / 2f, 0f));
            BaseMapManager.Instance.SaveAll();
        }

        SpawnHero();
        SetupCamera();

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
        if (BaseMapManager.Instance == null) gameObject.AddComponent<BaseMapManager>();
        if (BuilderSystem.Instance == null) gameObject.AddComponent<BuilderSystem>();
        if (PlacementController.Instance == null) gameObject.AddComponent<PlacementController>();
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

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

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
        Sprite iconBattle = SpriteManager.Instance?.GetSprite("ui_icon_battle");    // Tool_03.png
        Sprite iconBuild = SpriteManager.Instance?.GetSprite("ui_icon_build");     // Tool_04.png

        // ─── ALTIN BARI (Sol üst) ───────────────────────────────
        GameObject goldPill = new GameObject("GoldPill");
        goldPill.transform.SetParent(hudRoot.transform, false);
        RectTransform goldRt = goldPill.AddComponent<RectTransform>();
        goldRt.anchorMin = new Vector2(0.02f, 0.942f);
        goldRt.anchorMax = new Vector2(0.40f, 0.990f);
        goldRt.offsetMin = Vector2.zero;
        goldRt.offsetMax = Vector2.zero;

        Image goldBg = goldPill.AddComponent<Image>();
        if (barGold != null)
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
        CreateImage(goldPill.transform, "CoinIcon",
            new Vector2(0.0f, 0.08f), new Vector2(0.25f, 0.92f),
            goldCoin, Color.white);
        // Altın sayısı
        TextMeshProUGUI goldText = CreateText(goldPill.transform, "GoldText",
            "0",
            new Vector2(0.28f, 0.38f), new Vector2(0.96f, 0.92f),
            30, TextAlignmentOptions.Left, new Color(0.95f, 0.95f, 0.95f));
        goldText.fontStyle = FontStyles.Bold;
        // +X/sn
        TextMeshProUGUI gpsText = CreateText(goldPill.transform, "GpsText",
            "+0.0 altın/sn",
            new Vector2(0.28f, 0.04f), new Vector2(0.98f, 0.42f),
            13, TextAlignmentOptions.Left, new Color(0.85f, 0.85f, 0.85f));

        // ─── SAVAŞ SEVİYESİ (Üst Orta) ────────────────────────
        GameObject levelPill = new GameObject("BattleLevelPill");
        levelPill.transform.SetParent(hudRoot.transform, false);
        RectTransform levelRt = levelPill.AddComponent<RectTransform>();
        levelRt.anchorMin = new Vector2(0.33f, 0.935f);
        levelRt.anchorMax = new Vector2(0.67f, 1.000f);
        levelRt.offsetMin = Vector2.zero;
        levelRt.offsetMax = Vector2.zero;

        Image levelBg = levelPill.AddComponent<Image>();
        if (ribbonBattle != null)
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
            "Savaş Lv.1",
            new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.82f),
            24, TextAlignmentOptions.Center, new Color(0.95f, 0.95f, 0.95f));
        battleLvlText.fontStyle = FontStyles.Bold;

        // ─── İNŞAATÇI BARI (Sağ üst) ──────────────────────────
        GameObject builderPill = new GameObject("BuilderPill");
        builderPill.transform.SetParent(hudRoot.transform, false);
        RectTransform builderRt = builderPill.AddComponent<RectTransform>();
        builderRt.anchorMin = new Vector2(0.60f, 0.942f);
        builderRt.anchorMax = new Vector2(0.98f, 0.990f);
        builderRt.offsetMin = Vector2.zero;
        builderRt.offsetMax = Vector2.zero;

        Image builderBg = builderPill.AddComponent<Image>();
        if (barBuilder != null)
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
                          ?? SpriteManager.Instance?.WallBuilder
                          ?? BaseMapManager.MakeCircleSprite(new Color(0.3f, 0.6f, 1f));
        CreateImage(builderPill.transform, "AvatarIcon",
            new Vector2(0.50f, 0.06f), new Vector2(0.70f, 0.94f),
            builderIcon, Color.white);
        _builderStatusText = CreateText(builderPill.transform, "BuilderCount",
            "1/1",
            new Vector2(0.72f, 0.15f), new Vector2(0.98f, 0.85f),
            28, TextAlignmentOptions.Left, new Color(0.95f, 0.95f, 0.95f));
        _builderStatusText.fontStyle = FontStyles.Bold;

        // ─── SAVAŞA GİT BUTONU (Tool_03) ──────────────────────
        Button battleBtn = CreateRoundIconButton(hudRoot.transform, "BattleBtn",
            new Vector2(0.85f, 0.12f), new Vector2(140f, 140f),
            iconBattle, "Savaşa Git", new Color(0.85f, 0.25f, 0.2f), null);

        // ─── İNŞA ET BUTONU (Tool_04) ─────────────────────────
        Button buildBtn = CreateRoundIconButton(hudRoot.transform, "BuildBtn",
            new Vector2(0.85f, 0.28f), new Vector2(140f, 140f),
            iconBuild, "İnşa", new Color(0.2f, 0.55f, 0.85f), null);

        // --- Info Panels (Building + Hero) ---
        GameObject buildingInfoPanel = CreatePanel(canvasObj.transform, "BuildingInfo", new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.45f), new Color(0.1f, 0.1f, 0.1f, 0.95f));
        TextMeshProUGUI bName = CreateText(buildingInfoPanel.transform, "Name", "Bina Adı", new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.95f), 45, TextAlignmentOptions.Center, Color.white);
        TextMeshProUGUI bLvl = CreateText(buildingInfoPanel.transform, "Level", "Seviye 1", new Vector2(0.1f, 0.65f), new Vector2(0.9f, 0.8f), 35, TextAlignmentOptions.Center, Color.yellow);
        TextMeshProUGUI bStats = CreateText(buildingInfoPanel.transform, "Stats", "Can: 100", new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f), 28, TextAlignmentOptions.TopLeft, Color.white);
        Button bUpBtn = CreateButton(buildingInfoPanel.transform, "UpBtn", "Yükselt", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.3f), new Color(0.2f, 0.7f, 0.2f));
        TextMeshProUGUI bCost = bUpBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        Button bClose = CreateButton(buildingInfoPanel.transform, "CloseBtn", "X", new Vector2(0.85f, 0.85f), new Vector2(0.95f, 0.95f), Color.red);

        GameObject heroInfoPanel = CreatePanel(canvasObj.transform, "HeroInfo", new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.45f), new Color(0.1f, 0.1f, 0.1f, 0.95f));
        TextMeshProUGUI hName = CreateText(heroInfoPanel.transform, "Name", "Kahraman", new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.95f), 45, TextAlignmentOptions.Center, Color.white);
        TextMeshProUGUI hLvl = CreateText(heroInfoPanel.transform, "Level", "Seviye 1", new Vector2(0.1f, 0.65f), new Vector2(0.9f, 0.8f), 35, TextAlignmentOptions.Center, Color.yellow);
        TextMeshProUGUI hStats = CreateText(heroInfoPanel.transform, "Stats", "Can: 100", new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f), 28, TextAlignmentOptions.TopLeft, Color.white);
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

        CreateVirtualJoystick(hudRoot.transform);
    }

    private void UpdateBuilderStatus()
    {
        if (_builderStatusText != null && BuilderSystem.Instance != null)
        {
            _builderStatusText.text = BuilderSystem.Instance.HasAvailableBuilder() ? "1/1" : "0/1";
        }
    }

    private void CreateVirtualJoystick(Transform parent)
    {
        GameObject inputRoot = new GameObject("VirtualJoystickInput");
        inputRoot.transform.SetParent(parent, false);
        inputRoot.transform.SetAsFirstSibling();
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
        GameObject worldRoot = new GameObject("WorldRoot");

        Sprite grassSprite = null;
        Sprite woodSlotsSprite = SpriteManager.Instance?.GetSprite("tile_wood_slots");

        Texture2D tileset = Resources.Load<Texture2D>("Sprites/tileset_color1");
        if (tileset != null)
        {
            int cols = 9;
            int rows = 6;
            int tileWidth = tileset.width / cols;
            int tileHeight = tileset.height / rows;
            int ppu = Mathf.Max(32, Mathf.Min(tileWidth, tileHeight));

            Sprite GetTile(int col, int row)
            {
                int x = col * tileWidth;
                int y = tileset.height - (row + 1) * tileHeight;
                Rect rect = new Rect(x, y, tileWidth, tileHeight);
                return Sprite.Create(tileset, rect, new Vector2(0.5f, 0.5f), ppu);
            }

            grassSprite = GetTile(0, 0);
        }

        if (grassSprite == null)
            grassSprite = SpriteManager.Instance?.Ground;

        if (grassSprite == null)
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] cols = new Color[32 * 32];
            Color groundColor = new Color(0.35f, 0.55f, 0.3f);
            for (int i = 0; i < cols.Length; i++) cols[i] = groundColor;
            tex.SetPixels(cols); tex.Apply();
            grassSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject tile = new GameObject($"Grass_{x}_{y}");
                tile.transform.SetParent(worldRoot.transform);
                tile.transform.position = new Vector3(x, y, 2f);

                SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();

                if (woodSlotsSprite != null)
                {
                    sr.sprite = woodSlotsSprite;
                    sr.color = Color.white;
                }
                else
                {
                    sr.sprite = grassSprite;
                    sr.color = Color.white;
                }
                sr.sortingOrder = -10;
            }
        }


        // Ağaçlar — merkez bölgesi ve su kenarı dışında rastgele
        int treeCount = UnityEngine.Random.Range(6, 10);
        int placed = 0;
        int attempts = 0;
        while (placed < treeCount && attempts < 100)
        {
            attempts++;
            float rx = UnityEngine.Random.Range(3f, width - 3f);
            float ry = UnityEngine.Random.Range(3f, height - 3f);
            float dxC = Mathf.Abs(rx - width / 2f);
            float dyC = Mathf.Abs(ry - height / 2f);
            // Merkez alanı (5x5) ve bina yakını hariC tut
            if (dxC < 3.5f && dyC < 3.5f) continue;
            BaseMapManager.Instance.SpawnTree(new Vector3(rx, ry, 0f));
            placed++;
        }

        int rockCount = UnityEngine.Random.Range(4, 7);
        for (int i = 0; i < rockCount; i++)
        {
            float rx = UnityEngine.Random.Range(1f, width - 1f);
            float ry = UnityEngine.Random.Range(1f, height - 1f);
            BaseMapManager.Instance.SpawnRock(new Vector3(rx, ry, 0f));
        }
    }

    private void SpawnHero()
    {
        _heroObj = new GameObject("Hero");
        _heroObj.transform.position = new Vector3(width / 2f + 1f, height / 2f - 2f, -0.5f);
        _heroObj.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

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

        if (weapon == WeaponType.Spear)
            _heroObj.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        CircleCollider2D col = _heroObj.AddComponent<CircleCollider2D>();
        col.isTrigger = false;

        Rigidbody2D rb = _heroObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        if (sr.sprite != null)
        {
            float radius = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) * 0.4f;
            col.radius = Mathf.Max(0.25f, radius);
        }

        _heroObj.AddComponent<HeroMovementController>();
    }

    private void SetupCamera()
    {
        Camera.main.orthographicSize = 6f;
        Camera.main.transform.position = new Vector3(width / 2f, height / 2f, -10f);

        // Basit kamera takibi kahramana
        var follow = Camera.main.gameObject.AddComponent<CameraFollow>();
        follow.target = _heroObj.transform;
        follow.boundsMin = new Vector2(3f, 3f);
        follow.boundsMax = new Vector2(width - 3f, height - 3f);
    }

    // ==================== INPUT (TAP & LONG PRESS) ====================

    private void HandleLongPress(GameObject targetObj)
    {
        // Uzun basma algılandığında
        var mapObj = BaseMapManager.Instance.GetMapObject(targetObj);
        if (mapObj != null)
        {
            if (mapObj.objectType == MapObjectType.Tree || mapObj.objectType == MapObjectType.Rock)
            {
                BaseMapManager.Instance.RequestRemoveObject(mapObj);
            }
            else if (mapObj.objectType == MapObjectType.Building)
            {
                // Taşıma modu
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

    private void HandleTap(Vector3 worldPos)
    {
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null)
        {
            var mapObj = BaseMapManager.Instance.GetMapObject(hit.collider.gameObject);
            if (mapObj != null && mapObj.objectType == MapObjectType.Building)
            {
                HideRemovePrompt();
                // Binaya tıklandı
                if (mapObj.buildingType == BuildingType.GoldMine)
                {
                    // Altın madenine tıklandığında içindeki altını topla!
                    int stored = GameManager.Instance.GoldMineStored;
                    if (stored > 0)
                    {
                        GameManager.Instance.CollectGoldFromMineStorage();
                        GoldPopupManager.ShowGoldChange(stored);
                    }

                }

                var ui = GetComponent<BaseWorldUI>();
                if (ui != null && mapObj.buildingType.HasValue)
                {
                    ui.ShowBuildingInfo(mapObj.buildingType.Value);
                }
                return;
            }

            if (mapObj != null && (mapObj.objectType == MapObjectType.Tree || mapObj.objectType == MapObjectType.Rock))
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
        }

        // Boşluğa tıklandı - Panelleri kapat (artık tıklamayla yürümeyecek)
        HideRemovePrompt();
        GetComponent<BaseWorldUI>()?.HideAllPanelsPublic();
    }

    private void ShowRemovePrompt(PlacedMapObject obj)
    {
        if (obj == null || obj.gameObject == null)
            return;

        if (_removePrompt != null && _removeTarget == obj)
            return;

        HideRemovePrompt();

        _removeTarget = obj;

        _removePrompt = new GameObject("RemovePrompt");

        Canvas canvas = _removePrompt.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;
        _removePrompt.AddComponent<GraphicRaycaster>();

        RectTransform rt = _removePrompt.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(110f, 36f);
        rt.localScale = Vector3.one;

        Image bg = _removePrompt.AddComponent<Image>();
        Sprite removeBg = SpriteManager.Instance?.GetSprite("ui_btn_remove");
        if (removeBg != null)
        {
            bg.sprite = removeBg;
            bg.color = Color.white;
            bg.preserveAspect = true;
        }
        else
        {
            bg.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
        }

        Button btn = _removePrompt.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() =>
        {
            BaseMapManager.Instance?.RequestRemoveObject(obj);
            HideRemovePrompt();
        });

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(_removePrompt.transform, false);
        RectTransform txtRt = textObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "KALDIR";
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.2f);
        tmp.fontStyle = FontStyles.Bold;
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
            bg.preserveAspect = true;
        }
        else
        {
            bg.sprite = BaseMapManager.MakeCircleSprite(Color.white);
            bg.color = bgColor;
        }

        Button btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(root.transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        // Ikonlari buyutmek icin anchorlari genisletiyoruz (eski: 0.22-0.78, yeni: 0.1-0.9)
        iconRt.anchorMin = new Vector2(0.1f, 0.2f);
        iconRt.anchorMax = new Vector2(0.9f, 0.85f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        Image iconImg = iconObj.AddComponent<Image>();
        if (icon != null)
        {
            iconImg.sprite = icon;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.color = new Color(1f, 1f, 1f, 0.4f);
        }

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(root.transform, false);
        RectTransform labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, -0.35f);
        labelRt.anchorMax = new Vector2(1f, 0f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

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
