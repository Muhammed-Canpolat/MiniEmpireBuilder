using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Üs dünyası sahnesini otomatik olarak kurar — tüm binalar, UI ve etkileşimler kodla oluşturulur
/// BaseScene'de boş bir GameObject'e eklenir
/// </summary>
public class BaseSceneSetup : MonoBehaviour
{
    // Bina objeleri — tıklanabilir
    private GameObject mainBaseObj;
    private GameObject goldMineObj;
    private GameObject archerTowerObj;
    private GameObject cannonTowerObj;
    private GameObject wallObj;
    private GameObject wallObj2;
    private GameObject heroObj;

    // UI referansları
    private BaseWorldUI baseUI;

    private void Awake()
    {
        // GameManager yoksa ana menüye yönlendir
        if (GameManager.Instance == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }

        // SpriteManager yoksa oluştur
        if (SpriteManager.Instance == null)
        {
            GameObject smObj = new GameObject("SpriteManager");
            smObj.AddComponent<SpriteManager>();
        }

        if (GameManager.Instance != null && GameManager.Instance.PlayerData == null)
        {
            GameManager.Instance.StartNewGame(WeaponType.Spear);
            // StartNewGame LoadBaseWorld çağırır, o da bu sahneyi tekrar yükler
            // Bu sefer PlayerData dolu olacak, Awake'e tekrar girince sorun olmaz
            return;
        }
    }

    private void Start()
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null) return;

        // Kamerayı ayarla
        Camera.main.orthographic = true;
        Camera.main.orthographicSize = 5.5f;
        Camera.main.transform.position = new Vector3(0, -0.5f, -10f); // Biraz aşağı kaydır
        Camera.main.backgroundColor = new Color(0.06f, 0.09f, 0.06f);

        // EventSystem
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // BaseWorldController ekle (altın üretimi)
        if (FindFirstObjectByType<BaseWorldController>() == null)
        {
            gameObject.AddComponent<BaseWorldController>();
        }

        // Zemin oluştur
        SetupGround();

        // Binaları oluştur
        SetupBuildings();

        // UI oluştur
        SetupUI();

        // Altın popup yöneticisi
        if (FindFirstObjectByType<GoldPopupManager>() == null)
        {
            new GameObject("GoldPopupManager").AddComponent<GoldPopupManager>();
        }

        // Sahne fade-in efekti
        StartCoroutine(SceneFadeIn());

        Debug.Log("[BaseSceneSetup] Us dunyasi hazirlandi!");
    }

    private void SetupGround()
    {
        var sm = SpriteManager.Instance;

        // Ana zemin — koyu yeşil çim
        CreateSimpleSprite("Ground", new Color(0.1f, 0.15f, 0.06f),
            new Vector3(0, -1f, 1), new Vector3(14f, 14f, 1));

        // Hafif açık çim alanları (dekoratif — binalar arası doku farkı)
        CreateSimpleSprite("CimAlani1", new Color(0.12f, 0.17f, 0.07f),
            new Vector3(-1.2f, 1.5f, 0.9f), new Vector3(2.5f, 2f, 1));
        CreateSimpleSprite("CimAlani2", new Color(0.12f, 0.16f, 0.07f),
            new Vector3(1f, -1.5f, 0.9f), new Vector3(2f, 1.5f, 1));
        CreateSimpleSprite("CimAlani3", new Color(0.11f, 0.16f, 0.06f),
            new Vector3(0, 0, 0.85f), new Vector3(3f, 3f, 1));

        // Dekoratif ağaçlar (gerçek sprite)
        if (sm != null)
        {
            Sprite treeSpr = sm.Tree;
            CreateSpriteObject("Tree1", treeSpr, new Vector3(-3.2f, 3.5f, 0.3f), new Vector3(0.45f, 0.45f, 1f));
            CreateSpriteObject("Tree2", treeSpr, new Vector3(3.4f, 3.2f, 0.3f), new Vector3(0.4f, 0.4f, 1f));
            CreateSpriteObject("Tree3", treeSpr, new Vector3(-3.5f, -0.5f, 0.3f), new Vector3(0.38f, 0.38f, 1f));
            CreateSpriteObject("Tree4", treeSpr, new Vector3(3.3f, -2.8f, 0.3f), new Vector3(0.42f, 0.42f, 1f));
            CreateSpriteObject("Tree5", treeSpr, new Vector3(-0.3f, 3.8f, 0.3f), new Vector3(0.35f, 0.35f, 1f));
            CreateSpriteObject("Tree6", treeSpr, new Vector3(0.5f, -3.8f, 0.3f), new Vector3(0.38f, 0.38f, 1f));
            CreateSpriteObject("Tree7", treeSpr, new Vector3(-3.6f, 1.2f, 0.3f), new Vector3(0.3f, 0.3f, 1f));
            CreateSpriteObject("Tree8", treeSpr, new Vector3(3.6f, 0.5f, 0.3f), new Vector3(0.33f, 0.33f, 1f));

            // Dekoratif kayalar
            Sprite rockSpr = sm.Rock;
            CreateSpriteObject("Rock1", rockSpr, new Vector3(-2.5f, -3.5f, 0.4f), new Vector3(0.5f, 0.5f, 1f));
            CreateSpriteObject("Rock2", rockSpr, new Vector3(2.8f, -0.3f, 0.4f), new Vector3(0.45f, 0.45f, 1f));
            CreateSpriteObject("Rock3", rockSpr, new Vector3(0.3f, 3.5f, 0.4f), new Vector3(0.4f, 0.4f, 1f));
            CreateSpriteObject("Rock4", rockSpr, new Vector3(-3f, -3f, 0.4f), new Vector3(0.35f, 0.35f, 1f));
        }
    }

    private IEnumerator SceneFadeIn()
    {
        // Tam ekran siyah panel oluştur
        GameObject fadeObj = new GameObject("FadePanel");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999;

        GameObject fadePanel = new GameObject("Black");
        fadePanel.transform.SetParent(fadeObj.transform, false);
        RectTransform rt = fadePanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image fadeImg = fadePanel.AddComponent<Image>();
        fadeImg.color = Color.black;
        fadeImg.raycastTarget = false;

        // Fade out (siyahtan saydam)
        float t = 0;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            fadeImg.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(t / 0.6f));
            yield return null;
        }

        Destroy(fadeObj);
    }

    // ==================== BİNA YERLEŞİMİ ====================

    private void SetupBuildings()
    {
        var buildings = GameManager.Instance.Buildings;
        var sm = SpriteManager.Instance;
        Color lockedColor = new Color(0.3f, 0.3f, 0.35f);

        // === ANA ÜS — merkez (en büyük bina) ===
        mainBaseObj = CreateBuildingSprite("AnaUs", new Color(0.85f, 0.75f, 0.2f),
            new Vector3(0, 0.2f, 0), new Vector3(0.8f, 0.8f, 1f), BuildingType.MainBase);

        CreateWorldLabel(mainBaseObj.transform, "Ana Us",
            GetBuildingLevelText(BuildingType.MainBase), new Color(1f, 0.9f, 0.3f));

        // === ALTIN MADENİ — Topçu ile hizalı ===
        // Wall reference (temp) (çalışan sprite)
        GameObject goldMineHouse = CreateSpriteObject("AltinMadeniBina",
            sm != null ? sm.WallBuilder : null,
            new Vector3(-1.4f, 2.1f, 0), new Vector3(1.1f, 1.1f, 1f));
        // Üstüne altın yığını ekle (biraz daha küçük, bina üzerinde)
        GameObject goldPile = CreateSpriteObject("AltinYigini",
            sm != null ? sm.GoldMine : null,
            new Vector3(-1.4f, 2.6f, 0.01f), new Vector3(0.6f, 0.6f, 1f));
        goldPile.transform.SetParent(goldMineHouse.transform, true);
        goldMineObj = goldMineHouse;

        // Altin madeni de diger binalar gibi tiklanabilir olmali.
        BoxCollider2D mineCol = goldMineObj.GetComponent<BoxCollider2D>();
        if (mineCol == null)
            mineCol = goldMineObj.AddComponent<BoxCollider2D>();
        mineCol.isTrigger = true;

        BuildingIdentifier mineId = goldMineObj.GetComponent<BuildingIdentifier>();
        if (mineId == null)
            mineId = goldMineObj.AddComponent<BuildingIdentifier>();
        mineId.buildingType = BuildingType.GoldMine;

        ApplyLockedTint(goldMineObj, BuildingType.GoldMine);

        CreateWorldLabel(goldMineObj.transform, "Maden",
            GetBuildingLevelText(BuildingType.GoldMine), new Color(1f, 0.85f, 0.2f));

        // === OKÇU KULESİ — sol alt ===
        bool archerLocked = buildings.ContainsKey(BuildingType.ArcherTower) && !buildings[BuildingType.ArcherTower].isUnlocked;
        Color archerColor = archerLocked ? lockedColor : new Color(0.2f, 0.6f, 1f);

        archerTowerObj = CreateBuildingSprite("OkcuKulesi", archerColor,
            new Vector3(-1.8f, -2.0f, 0), new Vector3(1.0f, 1.0f, 1f), BuildingType.ArcherTower);
        ApplyLockedTint(archerTowerObj, BuildingType.ArcherTower);

        CreateWorldLabel(archerTowerObj.transform, "Okcu",
            GetBuildingLevelText(BuildingType.ArcherTower), archerColor);

        // === TOPÇU KULESİ — sağ üst ===
        bool cannonLocked = buildings.ContainsKey(BuildingType.CannonTower) && !buildings[BuildingType.CannonTower].isUnlocked;
        Color cannonColor = cannonLocked ? lockedColor : new Color(1f, 0.4f, 0.1f);

        cannonTowerObj = CreateBuildingSprite("TopcuKulesi", cannonColor,
            new Vector3(1.8f, 2.0f, 0), new Vector3(1.0f, 1.0f, 1f), BuildingType.CannonTower);
        ApplyLockedTint(cannonTowerObj, BuildingType.CannonTower);

        CreateWorldLabel(cannonTowerObj.transform, "Topcu",
            GetBuildingLevelText(BuildingType.CannonTower), cannonColor);

        // === DUVAR ORNEGI (dekor) — alt orta ===
        if (sm != null && sm.WallSegment != null)
        {
            wallObj = CreateSpriteObject("DuvarOrnegi", sm.WallSegment,
                new Vector3(0f, -2.5f, 0f), new Vector3(0.8f, 0.8f, 1f));
        }
        else
        {
            wallObj = CreateSimpleSprite("DuvarOrnegi", new Color(0.55f, 0.38f, 0.18f),
                new Vector3(0f, -2.5f, 0f), new Vector3(1.0f, 0.35f, 1f));
        }

        // === DUVARCI ATÖLYESİ — sağ alt ===
        bool wallLocked2 = buildings.ContainsKey(BuildingType.WallBuilder) && !buildings[BuildingType.WallBuilder].isUnlocked;
        Color wallColor2 = wallLocked2 ? lockedColor : new Color(0.6f, 0.45f, 0.2f);

        wallObj2 = CreateBuildingSprite("Duvarci", wallColor2,
            new Vector3(1.8f, -2.0f, 0), new Vector3(1.0f, 1.0f, 1f), BuildingType.WallBuilder);
        ApplyLockedTint(wallObj2, BuildingType.WallBuilder);

        CreateWorldLabel(wallObj2.transform, "Duvarci",
            GetBuildingLevelText(BuildingType.WallBuilder), wallColor2);

        // Mine indicator icon kaldirildi (UI istegi).

        // === KAHRAMAN — ana üsün solunda ===
        Color heroColor = GetHeroColor();
        if (sm != null)
        {
            Sprite heroSprite = sm.GetHeroSprite(GameManager.Instance.PlayerData.hero.weaponType);
            heroObj = CreateSpriteObject("Kahraman", heroSprite,
                new Vector3(-2.0f, 0.5f, 0), new Vector3(1.0f, 1.0f, 1f));
        }
        else
        {
            heroObj = CreateSimpleSprite("Kahraman", heroColor,
                new Vector3(-0.9f, -0.2f, 0), new Vector3(0.55f, 0.55f, 1f));
        }

        string weaponName = GetWeaponDisplayName();
        CreateWorldLabel(heroObj.transform, weaponName,
            $"Lv.{GameManager.Instance.PlayerData.hero.level}", heroColor);

        // Kahramana tıklama
        BoxCollider2D heroCol = heroObj.AddComponent<BoxCollider2D>();
        heroCol.isTrigger = true;
    }

    /// <summary>
    /// Kilitli binalara gri tint uygula
    /// </summary>
    private void ApplyLockedTint(GameObject obj, BuildingType type)
    {
        var buildings = GameManager.Instance.Buildings;
        if (buildings.ContainsKey(type) && !buildings[type].isUnlocked)
        {
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.4f, 0.4f, 0.4f, 0.7f); // Kilitli: koyu ve yarı saydam
            }
        }
    }

    private string GetBuildingLevelText(BuildingType type)
    {
        var buildings = GameManager.Instance.Buildings;
        if (!buildings.ContainsKey(type)) return "Yok";
        if (!buildings[type].isUnlocked) return "Kilitli";
        return $"Lv.{buildings[type].level}";
    }

    private Color GetHeroColor()
    {
        switch (GameManager.Instance.PlayerData.hero.weaponType)
        {
            case WeaponType.Axe: return new Color(0.9f, 0.3f, 0.2f);
            case WeaponType.Spear: return new Color(0.3f, 0.7f, 1f);
            case WeaponType.Bow: return new Color(0.2f, 0.85f, 0.4f);
            default: return Color.cyan;
        }
    }

    private string GetWeaponDisplayName()
    {
        switch (GameManager.Instance.PlayerData.hero.weaponType)
        {
            case WeaponType.Axe: return "Balta";
            case WeaponType.Spear: return "Mizrak";
            case WeaponType.Bow: return "Ok";
            default: return "";
        }
    }

    // ==================== UI KURULUMU ====================

    private void SetupUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("BaseCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // ===== ÜST BİLGİ ÇUBUĞU =====
        // Arka plan — %90 - %100 (daha geniş alan)
        GameObject topBar = CreateUIPanel(canvasObj.transform, "TopBar",
            new Vector2(0, 0.90f), Vector2.one, new Color(0.05f, 0.05f, 0.08f, 0.85f));

        // Üst çizgi (altın rengi dekoratif)
        CreateUIPanel(topBar.transform, "TopStripe",
            new Vector2(0, 0), new Vector2(1, 0.02f), new Color(1f, 0.85f, 0.2f, 0.6f));

        // Altın ikon kutusu
        CreateUIPanel(topBar.transform, "GoldIcon",
            new Vector2(0.02f, 0.55f), new Vector2(0.06f, 0.85f), new Color(1f, 0.85f, 0.2f));

        // Altın metin
        TextMeshProUGUI goldText = CreateUIText(topBar.transform, "GoldText",
            new Vector2(0.07f, 0.55f), new Vector2(0.45f, 0.9f),
            $"{GameManager.Instance.Gold}", 28, TextAlignmentOptions.Left,
            new Color(1f, 0.9f, 0.3f));

        // Altın/sn
        float gps = GameManager.Instance.GetGoldPerSecond();
        TextMeshProUGUI gpsText = CreateUIText(topBar.transform, "GpsText",
            new Vector2(0.07f, 0.15f), new Vector2(0.45f, 0.55f),
            gps > 0 ? $"+{gps:F1} altin/sn" : "", 16, TextAlignmentOptions.Left,
            new Color(0.5f, 0.85f, 0.3f));

        // Savaş Level kutusu
        GameObject lvlBox = CreateUIPanel(topBar.transform, "LvlBox",
            new Vector2(0.55f, 0.55f), new Vector2(0.98f, 0.9f), new Color(0.8f, 0.2f, 0.2f, 0.8f));
        TextMeshProUGUI levelText = CreateUIText(lvlBox.transform, "LevelText",
            Vector2.zero, Vector2.one,
            $"Savas Lv.{GameManager.Instance.PlayerData.currentBattleLevel}",
            22, TextAlignmentOptions.Center, Color.white);

        // Ana Üs seviye
        TextMeshProUGUI baseLvlText = CreateUIText(topBar.transform, "BaseLvlText",
            new Vector2(0.55f, 0.15f), new Vector2(0.98f, 0.55f),
            $"Ana Us Lv.{GameManager.Instance.GetMainBaseLevel()}",
            18, TextAlignmentOptions.Right, Color.yellow);

        // ===== SAVASA GİT BUTONU — alt orta, daha büyük =====
        GameObject battleBtn = CreateUIButton(canvasObj.transform, "BattleBtn",
            new Vector2(0.15f, 0.01f), new Vector2(0.85f, 0.07f),
            "SAVASA GIT!", new Color(0.75f, 0.15f, 0.15f));
        // Buton üst çizgi
        CreateUIPanel(battleBtn.transform, "BtnStripe",
            new Vector2(0, 0.85f), Vector2.one, new Color(1f, 0.3f, 0.2f));

        // ===== ANA MENÜ BUTONU — sol üst =====
        GameObject menuBtn = CreateUIButton(canvasObj.transform, "MenuBtn",
            new Vector2(0.02f, 0.84f), new Vector2(0.18f, 0.89f),
            "MENU", new Color(0.3f, 0.3f, 0.4f, 0.8f));
        menuBtn.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance.LoadMainMenu());

        // ===== BİNA BİLGİ PANELİ — geliştirilmiş =====
        GameObject buildingPanel = CreateUIPanel(canvasObj.transform, "BuildingPanel",
            new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.68f),
            new Color(0.08f, 0.08f, 0.12f, 0.97f));
        buildingPanel.SetActive(false);

        // Üst kenar çizgi
        CreateUIPanel(buildingPanel.transform, "PanelBorder",
            new Vector2(0, 0.97f), Vector2.one, new Color(1f, 0.85f, 0.2f, 0.8f));

        // Bina adı
        TextMeshProUGUI bNameText = CreateUIText(buildingPanel.transform, "BName",
            new Vector2(0.05f, 0.82f), new Vector2(0.65f, 0.96f),
            "Bina Adi", 26, TextAlignmentOptions.Left, Color.white);

        // Bina seviye kutusu
        GameObject bLvlBox = CreateUIPanel(buildingPanel.transform, "BLvlBox",
            new Vector2(0.7f, 0.84f), new Vector2(0.95f, 0.96f), new Color(0.2f, 0.5f, 0.8f, 0.8f));
        TextMeshProUGUI bLevelText = CreateUIText(bLvlBox.transform, "BLevel",
            Vector2.zero, Vector2.one,
            "Lv.1", 22, TextAlignmentOptions.Center, Color.white);

        // Ayirıcı çizgi
        CreateUIPanel(buildingPanel.transform, "Divider1",
            new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.80f), new Color(1, 1, 1, 0.1f));

        // Stat metni
        TextMeshProUGUI bStatsText = CreateUIText(buildingPanel.transform, "BStats",
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.78f),
            "Statlar", 18, TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.85f));

        // Maliyet kutusu
        GameObject costBox = CreateUIPanel(buildingPanel.transform, "CostBox",
            new Vector2(0.05f, 0.22f), new Vector2(0.50f, 0.38f), new Color(0.15f, 0.12f, 0.05f, 0.8f));
        TextMeshProUGUI bCostText = CreateUIText(costBox.transform, "BCost",
            Vector2.zero, Vector2.one,
            "100 Altin", 17, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.2f));

        // Yükselt butonu
        GameObject upgradeBtn = CreateUIButton(buildingPanel.transform, "UpgradeBtn",
            new Vector2(0.52f, 0.22f), new Vector2(0.95f, 0.38f),
            "YUKSELT", new Color(0.15f, 0.65f, 0.25f));

        // Kapat butonu
        GameObject closeBtn = CreateUIButton(buildingPanel.transform, "CloseBtn",
            new Vector2(0.15f, 0.03f), new Vector2(0.85f, 0.18f),
            "KAPAT", new Color(0.35f, 0.35f, 0.4f));

        // ===== KAHRAMAN BİLGİ PANELİ — geliştirilmiş =====
        GameObject heroPanel = CreateUIPanel(canvasObj.transform, "HeroPanel",
            new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.68f),
            new Color(0.08f, 0.08f, 0.12f, 0.97f));
        heroPanel.SetActive(false);

        // Üst kenar çizgi — kahraman rengi
        CreateUIPanel(heroPanel.transform, "HPanelBorder",
            new Vector2(0, 0.97f), Vector2.one, GetHeroColor());

        TextMeshProUGUI hNameText = CreateUIText(heroPanel.transform, "HName",
            new Vector2(0.05f, 0.82f), new Vector2(0.65f, 0.96f),
            "Savasci", 26, TextAlignmentOptions.Left, GetHeroColor());

        GameObject hLvlBox = CreateUIPanel(heroPanel.transform, "HLvlBox",
            new Vector2(0.7f, 0.84f), new Vector2(0.95f, 0.96f), GetHeroColor());
        TextMeshProUGUI hLevelText = CreateUIText(hLvlBox.transform, "HLevel",
            Vector2.zero, Vector2.one,
            "Lv.1", 22, TextAlignmentOptions.Center, Color.white);

        CreateUIPanel(heroPanel.transform, "HDivider",
            new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.80f), new Color(1, 1, 1, 0.1f));

        TextMeshProUGUI hStatsText = CreateUIText(heroPanel.transform, "HStats",
            new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.78f),
            "Statlar", 18, TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.85f));

        GameObject hCostBox = CreateUIPanel(heroPanel.transform, "HCostBox",
            new Vector2(0.05f, 0.22f), new Vector2(0.50f, 0.38f), new Color(0.15f, 0.12f, 0.05f, 0.8f));
        TextMeshProUGUI hCostText = CreateUIText(hCostBox.transform, "HCost",
            Vector2.zero, Vector2.one,
            "40 Altin", 17, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.2f));

        GameObject heroUpBtn = CreateUIButton(heroPanel.transform, "HeroUpBtn",
            new Vector2(0.52f, 0.22f), new Vector2(0.95f, 0.38f),
            "YUKSELT", new Color(0.15f, 0.65f, 0.25f));

        GameObject heroCloseBtn = CreateUIButton(heroPanel.transform, "HCloseBtn",
            new Vector2(0.15f, 0.03f), new Vector2(0.85f, 0.18f),
            "KAPAT", new Color(0.35f, 0.35f, 0.4f));

        // ===== BaseWorldUI bileşenini ekle =====
        baseUI = canvasObj.AddComponent<BaseWorldUI>();
        baseUI.SetReferences(
            goldText, levelText, gpsText,
            buildingPanel, bNameText, bLevelText, bStatsText, bCostText,
            upgradeBtn.GetComponent<Button>(), closeBtn.GetComponent<Button>(),
            heroPanel, hNameText, hLevelText, hStatsText, hCostText,
            heroUpBtn.GetComponent<Button>(), heroCloseBtn.GetComponent<Button>(),
            battleBtn.GetComponent<Button>()
        );

        // ===== Bina tıklama algılayıcı =====
        gameObject.AddComponent<BuildingClickHandler>().Initialize(baseUI, heroObj);
    }

    // ==================== YARDIMCI: DÜNYA OBJELERİ ====================

    private GameObject CreateBuildingSprite(string name, Color color, Vector3 pos, Vector3 scale, BuildingType type)
    {
        // SpriteManager varsa gerçek sprite kullan
        if (SpriteManager.Instance != null)
        {
            Sprite realSprite = SpriteManager.Instance.GetBuildingSprite(type);
            if (realSprite != null)
            {
                GameObject obj = CreateSpriteObject(name, realSprite, pos, scale);
                BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                BuildingIdentifier bid = obj.AddComponent<BuildingIdentifier>();
                bid.buildingType = type;
                return obj;
            }
        }

        // Fallback: renkli kare
        GameObject fallback = CreateSimpleSprite(name, color, pos, scale);
        BoxCollider2D col2 = fallback.AddComponent<BoxCollider2D>();
        col2.isTrigger = true;
        BuildingIdentifier bid2 = fallback.AddComponent<BuildingIdentifier>();
        bid2.buildingType = type;
        return fallback;
    }

    private GameObject CreateSimpleSprite(string name, Color color, Vector3 pos, Vector3 scale)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = pos;
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = color;

        return obj;
    }

    /// <summary>
    /// Gerçek sprite ile obje oluştur (renk tintlemesi yok)
    /// </summary>
    private GameObject CreateSpriteObject(string name, Sprite sprite, Vector3 pos, Vector3 scale)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = pos;
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        return obj;
    }

    private void CreateWorldLabel(Transform parent, string line1, string line2, Color color)
    {
        // World-space Canvas
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = new Vector3(0, -0.55f, 0);

        Canvas c = labelObj.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 10;

        RectTransform crt = labelObj.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(160, 50);
        crt.localScale = Vector3.one * 0.007f; // Çok daha küçük!

        // Arka plan (yarı saydam koyu kutu)
        GameObject bgObj = new GameObject("LabelBg");
        bgObj.transform.SetParent(labelObj.transform, false);
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(-0.05f, -0.1f);
        bgRt.anchorMax = new Vector2(1.05f, 1.1f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.55f);
        bgImg.raycastTarget = false;

        // Satır 1
        GameObject t1Obj = new GameObject("Line1");
        t1Obj.transform.SetParent(labelObj.transform, false);
        RectTransform t1rt = t1Obj.AddComponent<RectTransform>();
        t1rt.anchorMin = new Vector2(0, 0.5f);
        t1rt.anchorMax = new Vector2(1, 1);
        t1rt.offsetMin = Vector2.zero;
        t1rt.offsetMax = Vector2.zero;
        TextMeshProUGUI t1 = t1Obj.AddComponent<TextMeshProUGUI>();
        t1.text = line1;
        t1.fontSize = 22;
        t1.alignment = TextAlignmentOptions.Center;
        t1.color = color;
        t1.fontStyle = FontStyles.Bold;
        t1.enableAutoSizing = true;
        t1.fontSizeMin = 10;
        t1.fontSizeMax = 24;

        // Satır 2
        GameObject t2Obj = new GameObject("Line2");
        t2Obj.transform.SetParent(labelObj.transform, false);
        RectTransform t2rt = t2Obj.AddComponent<RectTransform>();
        t2rt.anchorMin = new Vector2(0, 0);
        t2rt.anchorMax = new Vector2(1, 0.5f);
        t2rt.offsetMin = Vector2.zero;
        t2rt.offsetMax = Vector2.zero;
        TextMeshProUGUI t2 = t2Obj.AddComponent<TextMeshProUGUI>();
        t2.text = line2;
        t2.fontSize = 16;
        t2.alignment = TextAlignmentOptions.Center;
        t2.color = new Color(0.85f, 0.85f, 0.9f);
        t2.enableAutoSizing = true;
        t2.fontSizeMin = 8;
        t2.fontSizeMax = 18;
    }

    private Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(32, 32);
        Color[] colors = new Color[32 * 32];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    private void CreateGoldMineCollectIndicator()
    {
        if (goldMineObj == null)
            return;

        GameObject iconObj = new GameObject("CollectIcon");
        iconObj.transform.SetParent(goldMineObj.transform, false);
        iconObj.transform.localPosition = new Vector3(0f, 0.95f, -0.02f);
        iconObj.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

        SpriteRenderer iconSr = iconObj.AddComponent<SpriteRenderer>();
        iconSr.sortingOrder = 20;
        if (SpriteManager.Instance != null)
            iconSr.sprite = SpriteManager.Instance.IconGold;
        iconSr.color = new Color(1f, 0.9f, 0.2f, 0.95f);

        GameObject amountObj = new GameObject("CollectAmount");
        amountObj.transform.SetParent(goldMineObj.transform, false);
        amountObj.transform.localPosition = new Vector3(0f, 1.25f, -0.03f);
        amountObj.transform.localScale = Vector3.one * 0.04f;

        TextMeshPro amountText = amountObj.AddComponent<TextMeshPro>();
        amountText.alignment = TextAlignmentOptions.Center;
        amountText.fontSize = 5f;
        amountText.color = new Color(1f, 0.95f, 0.45f, 1f);
        amountText.text = "";

        GoldMineCollectIndicator indicator = goldMineObj.AddComponent<GoldMineCollectIndicator>();
        indicator.SetReferences(iconSr, amountText);
    }

    // ==================== YARDIMCI: UI ====================

    private GameObject CreateUIPanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = color;
        return panel;
    }

    private TextMeshProUGUI CreateUIText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, float fontSize, TextAlignmentOptions align, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10;
        tmp.fontSizeMax = fontSize;
        return tmp;
    }

    private GameObject CreateUIButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string text, Color bgColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        // Buton yazısı
        TextMeshProUGUI btnText = CreateUIText(btnObj.transform, "Text",
            Vector2.zero, Vector2.one,
            text, 20, TextAlignmentOptions.Center, Color.white);

        return btnObj;
    }
}

/// <summary>
/// Bina objesine eklenir — hangi bina olduğunu saklar
/// </summary>
public class BuildingIdentifier : MonoBehaviour
{
    public BuildingType buildingType;
}

/// <summary>
/// Binalara tıklama algılayıcı — BaseSceneSetup tarafından eklenir
/// Tap feedback animasyonu ve altın kazanma popup'u içerir
/// </summary>
public class BuildingClickHandler : MonoBehaviour
{
    private BaseWorldUI baseUI;
    private GameObject heroObj;

    public void Initialize(BaseWorldUI ui, GameObject hero)
    {
        baseUI = ui;
        heroObj = hero;
    }

    private void Update()
    {
        // Dokunma/fare tıklama kontrolü
        bool clicked = false;
        Vector2 screenPos = Vector2.zero;

        if (UnityEngine.InputSystem.Touchscreen.current != null &&
            UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            clicked = true;
            screenPos = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (UnityEngine.InputSystem.Mouse.current != null &&
                 UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            // UI üzerindeyse atla
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            clicked = true;
            screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }

        if (!clicked) return;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0));
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null)
        {
            // Tap feedback animasyonu
            StartCoroutine(TapFeedback(hit.collider.gameObject));

            // Bina mı?
            BuildingIdentifier bid = hit.collider.GetComponent<BuildingIdentifier>();
            if (bid != null && baseUI != null)
            {
                if (bid.buildingType == BuildingType.GoldMine && GameManager.Instance != null)
                {
                    int collected = GameManager.Instance.CollectGoldFromMineStorage();
                    if (collected > 0)
                    {
                        GoldPopupManager.ShowGoldChange(collected);
                    }
                }

                baseUI.ShowBuildingInfo(bid.buildingType);
                return;
            }

            // Kahraman mı?
            if (hit.collider.gameObject == heroObj && baseUI != null)
            {
                baseUI.ShowHeroInfo();
                return;
            }
        }
    }

    /// <summary>
    /// Tıklanan objeye scale bounce animasyonu uygular
    /// </summary>
    private System.Collections.IEnumerator TapFeedback(GameObject target)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.transform.localScale;
        Vector3 punchScale = originalScale * 1.15f;

        // Büyüt
        float t = 0;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            target.transform.localScale = Vector3.Lerp(originalScale, punchScale, t / 0.08f);
            yield return null;
        }

        // Geri küçült
        t = 0;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            target.transform.localScale = Vector3.Lerp(punchScale, originalScale, t / 0.12f);
            yield return null;
        }

        target.transform.localScale = originalScale;
    }
}

/// <summary>
/// Altın değişikliklerinde "+X" popup metni gösterir
/// BaseWorldUI veya BaseSceneSetup tarafından tetiklenir
/// </summary>
public class GoldPopupManager : MonoBehaviour
{
    private static GoldPopupManager instance;
    private Canvas popupCanvas;

    private void Awake()
    {
        instance = this;

        // Popup için özel canvas
        GameObject canvasObj = new GameObject("GoldPopupCanvas");
        popupCanvas = canvasObj.AddComponent<Canvas>();
        popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        popupCanvas.sortingOrder = 100;

        UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;

        DontDestroyOnLoad(canvasObj);
        DontDestroyOnLoad(gameObject);
    }

    public static void ShowGoldChange(int amount)
    {
        if (instance == null || instance.popupCanvas == null) return;
        instance.StartCoroutine(instance.AnimateGoldPopup(amount));
    }

    private System.Collections.IEnumerator AnimateGoldPopup(int amount)
    {
        // Metin oluştur
        GameObject textObj = new GameObject("GoldPopup");
        textObj.transform.SetParent(popupCanvas.transform, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.3f, 0.87f);
        rt.anchorMax = new Vector2(0.7f, 0.92f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = amount > 0 ? $"+{amount}" : $"{amount}";
        tmp.fontSize = 36;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = amount > 0 ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.3f, 0.2f);
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.enableAutoSizing = false;

        // Yukarı kayarak kaybol
        float duration = 1.2f;
        float t = 0;
        Vector2 startAnchor = rt.anchorMin;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;

            // Yukarı hareket
            float yOffset = progress * 0.04f;
            rt.anchorMin = new Vector2(startAnchor.x, startAnchor.y + yOffset);
            rt.anchorMax = new Vector2(0.7f, 0.92f + yOffset);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Fade out (son %40'ta)
            if (progress > 0.6f)
            {
                float fadeProgress = (progress - 0.6f) / 0.4f;
                Color c = tmp.color;
                c.a = 1f - fadeProgress;
                tmp.color = c;
            }

            yield return null;
        }

        Destroy(textObj);
    }
}
