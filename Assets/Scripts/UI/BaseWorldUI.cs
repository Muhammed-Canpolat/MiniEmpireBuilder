using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Üs dünyasındaki UI — bina bilgileri, altın, seviye yükseltme butonları
/// </summary>
public class BaseWorldUI : MonoBehaviour
{
    public static BaseWorldUI Instance { get; private set; }

    [Header("Üst Bilgi Çubuğu")]

    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI battleLevelText;
    [SerializeField] private TextMeshProUGUI goldPerSecText;
    [SerializeField] private TextMeshProUGUI mineStoredText;

    [Header("Bina Bilgi Paneli")]
    [SerializeField] private GameObject buildingInfoPanel;
    [SerializeField] private TextMeshProUGUI buildingNameText;
    [SerializeField] private TextMeshProUGUI buildingLevelText;
    [SerializeField] private TextMeshProUGUI buildingStatsText;
    [SerializeField] private TextMeshProUGUI upgradeCostText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button closePanelButton;

    [Header("İnşa Sistemi")]
    [SerializeField] private Button buildMenuButton;

    [Header("Kahraman Bilgi Paneli")]
    [SerializeField] private GameObject heroInfoPanel;
    [SerializeField] private TextMeshProUGUI heroNameText;
    [SerializeField] private TextMeshProUGUI heroLevelText;
    [SerializeField] private TextMeshProUGUI heroStatsText;
    [SerializeField] private TextMeshProUGUI heroUpgradeCostText;
    [SerializeField] private Button heroUpgradeButton;
    [SerializeField] private Button heroCloseButton;

    [Header("Alt Butonlar")]
    [SerializeField] private Button battleButton;
    public Button BattleButton => battleButton;

    public void AnimateMineCollection(Vector3 worldPosition, int amount)
    {
        if (amount <= 0 || goldText == null || Camera.main == null) return;
        var canvas = goldText.canvas;
        if (canvas == null) return;
        var root = canvas.transform as RectTransform;
        Vector2 start = Camera.main.WorldToScreenPoint(worldPosition);
        Vector2 end = goldText.rectTransform.position;
        var sprite = BaseVisualCatalog.Instance?.goldIcon ?? SpriteManager.Instance?.IconGold;
        int count = Mathf.Clamp(amount / 8, 5, 8);
        for (int i = 0; i < count; i++)
        {
            var coin = new GameObject("FlyingMineGold", typeof(RectTransform), typeof(Image));
            coin.transform.SetParent(root, false);
            var image = coin.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var rect = coin.GetComponent<RectTransform>();
            rect.sizeDelta = Vector2.one * 28f;
            float phase = (float)i / count;
            int coinIndex = i;
            Vector2 from = start + new Vector2(Mathf.Cos(phase * 6.28f) * 25f,
                Mathf.Sin(phase * 6.28f) * 18f);
            float t = 0f;
            DOTween.To(() => t, value =>
            {
                t = value;
                if (rect == null) return;
                Vector2 control = (from + end) * 0.5f +
                    new Vector2(45f + coinIndex * 4f, 95f);
                Vector2 screen = (1f - t) * (1f - t) * from +
                    2f * (1f - t) * t * control + t * t * end;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    out var local);
                rect.anchoredPosition = local;
            }, 1f, 0.6f).SetDelay(i * 0.035f)
                .OnComplete(() => { if (coin != null) Destroy(coin); });
        }
        int displayed = GameManager.Instance.Gold - amount;
        DOTween.To(() => displayed, value =>
        {
            displayed = value;
            if (goldText != null) goldText.text = value.ToString();
        }, GameManager.Instance.Gold, 0.6f);
    }

    private BuildingType selectedBuilding;
    private GameObject modalBlocker;
    public bool IsInfoPanelOpen => (buildingInfoPanel != null && buildingInfoPanel.activeSelf)
        || (heroInfoPanel != null && heroInfoPanel.activeSelf);

    private void SetModalBlocker(bool visible)
    {
        if (modalBlocker == null)
            modalBlocker = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea/ModalBlocker")?.gameObject;
        if (modalBlocker != null) modalBlocker.SetActive(visible);
    }

    /// <summary>
    /// Programatik kurulum — BaseSceneSetup tarafından çağrılır
    /// </summary>
    public void SetReferences(
        TextMeshProUGUI gold, TextMeshProUGUI level, TextMeshProUGUI gps,
        GameObject bPanel, TextMeshProUGUI bName, TextMeshProUGUI bLevel, TextMeshProUGUI bStats, TextMeshProUGUI bCost,
        Button upgradeBtn, Button closeBtn,
        GameObject hPanel, TextMeshProUGUI hName, TextMeshProUGUI hLevel, TextMeshProUGUI hStats, TextMeshProUGUI hCost,
        Button heroUpBtn, Button heroCloseBtn,
        Button battleBtn, Button buildBtn)
    {
        goldText = gold;
        battleLevelText = level;
        goldPerSecText = gps;
        buildingInfoPanel = bPanel;
        buildingNameText = bName;
        buildingLevelText = bLevel;
        buildingStatsText = bStats;
        upgradeCostText = bCost;
        upgradeButton = upgradeBtn;
        closePanelButton = closeBtn;
        heroInfoPanel = hPanel;
        heroNameText = hName;
        heroLevelText = hLevel;
        heroStatsText = hStats;
        heroUpgradeCostText = hCost;
        heroUpgradeButton = heroUpBtn;
        heroCloseButton = heroCloseBtn;
        battleButton = battleBtn;
        buildMenuButton = buildBtn;
        mineStoredText = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CreateCharacterTabs();
        // Panel'leri gizle
        if (buildingInfoPanel != null) buildingInfoPanel.AddComponent<PanelController>().Initialize();
        if (heroInfoPanel != null) heroInfoPanel.AddComponent<PanelController>().Initialize();


        // Buton listener'ları
        if (battleButton != null)
            battleButton.onClick.AddListener(OnBattleClicked);

        if (buildMenuButton != null)
            buildMenuButton.onClick.AddListener(OnBuildMenuClicked);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);

        if (closePanelButton != null)
            closePanelButton.onClick.AddListener(CloseAllPanels);

        if (heroUpgradeButton != null)
            heroUpgradeButton.onClick.AddListener(OnHeroUpgradeClicked);

        if (heroCloseButton != null)
            heroCloseButton.onClick.AddListener(CloseAllPanels);

        // Event'lere abone ol
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged += UpdateGoldDisplay;
            GameManager.Instance.OnBuildingUpgraded += OnBuildingUpgradedEvent;
            GameManager.Instance.OnGoldMineStoredChanged += OnGoldMineStoredChanged;
        }

        UpdateAllUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Event'lerden çık
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged -= UpdateGoldDisplay;
            GameManager.Instance.OnBuildingUpgraded -= OnBuildingUpgradedEvent;
            GameManager.Instance.OnGoldMineStoredChanged -= OnGoldMineStoredChanged;
        }
    }

    public void ToggleBottomButtons(bool show)
    {
        if (battleButton != null) battleButton.gameObject.SetActive(show);
        if (buildMenuButton != null) buildMenuButton.gameObject.SetActive(show);
    }

    // ==================== UI GÜNCELLEME ====================


    private void UpdateAllUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null) return;

        UpdateGoldDisplay(GameManager.Instance.Gold);

        if (battleLevelText != null)
            battleLevelText.text = $"ANA ÜS · SEV. {GameManager.Instance.GetMainBaseLevel()}";

        if (goldPerSecText != null)
        {
            float gps = GameManager.Instance.GetGoldPerSecond();
            goldPerSecText.text = gps > 0 ? $"+{gps:F1} altin/sn" : "";
        }

        OnGoldMineStoredChanged(GameManager.Instance.GoldMineStored, GameManager.Instance.GetGoldMineStorageCapacity());
    }

    private int lastKnownGold = -1;
    private bool showingApprentice;
    private readonly GameObject[] characterUnderlines = new GameObject[2];

    private void UpdateGoldDisplay(int gold)
    {
        if (goldText != null)
            goldText.text = $"{gold}";
        var levelFill = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea/HudRoot/BaseLevelTrack/Fill") as RectTransform;
        if (levelFill != null && GameManager.Instance?.Buildings != null
            && GameManager.Instance.Buildings.TryGetValue(BuildingType.MainBase, out var mainBase))
        {
            int cost = mainBase.GetUpgradeCost();
            levelFill.anchorMax = new Vector2(cost > 0 ? Mathf.Clamp01((float)gold / cost) : 1f, 1f);
        }
        if (buildingInfoPanel != null && buildingInfoPanel.activeSelf)
            ShowBuildingInfo(selectedBuilding);

        // Altın değişikliğini popup olarak göster
        if (lastKnownGold >= 0 && gold != lastKnownGold)
        {
            int diff = gold - lastKnownGold;
            GoldPopupManager.ShowGoldChange(diff);
        }
        lastKnownGold = gold;
    }

    private void OnGoldMineStoredChanged(int stored, int capacity)
    {
        if (goldPerSecText == null)
            return;

        if (capacity <= 0)
            return;

        if (mineStoredText == null)
        {
            // Ayrı referans bağlanmadıysa mevcut gps satırına ikinci satır ekle.
            float gps = GameManager.Instance != null ? GameManager.Instance.GetGoldPerSecond() : 0f;
            string gpsText = gps > 0 ? $"+{gps:F1} altin/sn" : "";
            goldPerSecText.text = $"{gpsText}\nMaden: {stored}/{capacity}";
            return;
        }

        mineStoredText.text = $"Maden: {stored}/{capacity}";
    }

    // ==================== BİNA PANELİ ====================

    /// <summary>
    /// Bir binaya tıklayınca çağır — bina bilgilerini gösterir
    /// </summary>
    public void ShowBuildingInfo(BuildingType type)
    {
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.Buildings.ContainsKey(type)) return;

        selectedBuilding = type;
        BuildMenuController.Instance?.Hide();
        BuildingData building = GameManager.Instance.Buildings[type];

        if (heroInfoPanel != null) heroInfoPanel.GetComponent<PanelController>()?.HideImmediate();
        if (buildingInfoPanel != null) buildingInfoPanel.GetComponent<PanelController>()?.Show();
        SetModalBlocker(true);

        // Bina adı
        if (buildingNameText != null)
        {
            buildingNameText.text = GetBuildingDisplayName(type);
        }

        // Seviye
        if (buildingLevelText != null)
        {
            if (!building.isUnlocked)
                buildingLevelText.text = "KİLİTLİ";
            else
                buildingLevelText.text = $"Seviye {building.level}";
        }

        // Statlar
        if (buildingStatsText != null)
        {
            buildingStatsText.text = GetBuildingStatsText(building);
        }

        // Yükseltme maliyeti
        int maxLvl = building.GetMaxLevel(GameManager.Instance.GetMainBaseLevel());
        if (upgradeCostText != null)
        {
            if (!building.isUnlocked)
            {
                int reqLvl = building.GetRequiredMainBaseLevel();
                upgradeCostText.text = $"Ana Üs Lv.{reqLvl} gerekli";
            }
            else if (building.level >= maxLvl)
            {
                upgradeCostText.text = "MAX SEVİYE";
            }
            else
            {
                upgradeCostText.text = $"YÜKSELT · {building.GetUpgradeCost()}";
            }
        }

        // Buton aktiflik
        if (upgradeButton != null)
        {
            bool canUpgrade = building.isUnlocked
                && building.level < maxLvl
                && GameManager.Instance.Gold >= building.GetUpgradeCost();
            upgradeButton.interactable = canUpgrade;
            var buttonImage = upgradeButton.GetComponent<Image>();
            if (buttonImage != null) buttonImage.color = canUpgrade ? Color.white : new Color(0.65f, 0.65f, 0.65f, 0.75f);
            if (upgradeCostText != null && !canUpgrade && building.isUnlocked && building.level < maxLvl)
                upgradeCostText.text = $"YÜKSELT · <color=#C2452D>{building.GetUpgradeCost()}</color>";
        }

        var next = buildingInfoPanel?.transform.Find("NextUnlock")?.GetComponent<TextMeshProUGUI>();
        if (next != null)
        {
            next.text = type == BuildingType.MainBase ? GetNextMainBaseUnlock(building.level) : "";
        }
    }

    /// <summary>
    /// Kahraman bilgilerini göster
    /// </summary>
    public void ShowHeroInfo() => ShowCharacterInfo(false);

    public void ShowApprenticeInfo() => ShowCharacterInfo(true);

    private void ShowCharacterInfo(bool apprentice)
    {
        if (GameManager.Instance == null) return;
        showingApprentice = apprentice;
        for (int i = 0; i < characterUnderlines.Length; i++)
            if (characterUnderlines[i] != null)
                characterUnderlines[i].SetActive(apprentice == (i == 1));
        BuildMenuController.Instance?.Hide();

        if (buildingInfoPanel != null) buildingInfoPanel.GetComponent<PanelController>()?.HideImmediate();
        if (heroInfoPanel != null) heroInfoPanel.GetComponent<PanelController>()?.Show();
        SetModalBlocker(true);

        HeroData hero = GameManager.Instance.PlayerData.hero;

        if (apprentice)
        {
            int apprenticeLevel = GameManager.Instance.PlayerData.apprenticeLevel;
            int apprenticeCost = GameManager.Instance.GetApprenticeUpgradeCost();
            if (heroNameText != null) heroNameText.text = "ÇIRAK";
            if (heroLevelText != null) heroLevelText.text = $"Seviye {apprenticeLevel}";
            if (heroStatsText != null)
            {
                var progression = ApprenticeProgressionData.Instance;
                if (progression != null)
                {
                    int next = Mathf.Min(5, apprenticeLevel + 1);
                    string Arrow(int from, int to) => next == apprenticeLevel ? from.ToString()
                        : $"{from} → {to} ▲";
                    heroStatsText.text = $"Can {Arrow(progression.Health(hero.weaponType, apprenticeLevel), progression.Health(hero.weaponType, next))}\n" +
                        $"Hasar {Arrow(progression.Damage(hero.weaponType, apprenticeLevel), progression.Damage(hero.weaponType, next))}\n" +
                        $"Maden +%{progression.MineBonus(apprenticeLevel)}" +
                        (next == apprenticeLevel ? "" : $" → +%{progression.MineBonus(next)} ▲");
                }
            }
            if (heroUpgradeCostText != null)
                heroUpgradeCostText.text = apprenticeLevel >= 5 ? "MAKSİMUM SEVİYE" :
                    $"YÜKSELT · {apprenticeCost}";
            if (heroUpgradeButton != null)
                heroUpgradeButton.interactable = apprenticeLevel < 5 &&
                    GameManager.Instance.Gold >= apprenticeCost;
            return;
        }

        if (heroNameText != null)
            heroNameText.text = $"Ana Savaşçı ({GetWeaponName(hero.weaponType)})";

        if (heroLevelText != null)
            heroLevelText.text = $"Seviye {hero.level}";

        if (heroStatsText != null)
        {
            heroStatsText.text = $"XP: —\nCan: {hero.maxHealth:F0}\n" +
                                 $"Hasar: {hero.damage:F0}\n" +
                                 $"Menzil: {hero.attackRange:F1}\n" +
                                 $"Saldırı Hızı: {hero.attackSpeed:F1}/sn";
        }

        int maxHeroLvl = GameManager.Instance.GetMainBaseLevel() + 1;
        int cost = hero.level * 40;
        if (heroUpgradeCostText != null)
        {
            if (hero.level >= maxHeroLvl)
                heroUpgradeCostText.text = "Ana Üs seviyesini yükselt!";
            else
                heroUpgradeCostText.text = $"Yükselt: {cost} Altın";
        }

        if (heroUpgradeButton != null)
        {
            heroUpgradeButton.interactable = hero.level < maxHeroLvl
                && GameManager.Instance.Gold >= cost;
        }
    }

    // ==================== BUTON OLAYLARI ====================

    private void OnBattleClicked()
    {
        FirstBaseGuide.Instance?.OnBattleTapped();
        GameManager.Instance.LoadCampaignMap();
    }

    private void OnUpgradeClicked()
    {
        if (GameManager.Instance.UpgradeBuilding(selectedBuilding))
        {
            ShowBuildingInfo(selectedBuilding); // UI'ı güncelle
        }
    }

    private void OnHeroUpgradeClicked()
    {
        if (showingApprentice)
        {
            if (GameManager.Instance.UpgradeApprentice()) ShowApprenticeInfo();
        }
        else if (GameManager.Instance.UpgradeHero())
        {
            ShowHeroInfo();
        }
    }

    private void CreateCharacterTabs()
    {
        if (heroInfoPanel == null) return;
        string[] labels = { "KAHRAMAN", "ÇIRAK" };
        for (int i = 0; i < 2; i++)
        {
            int selected = i;
            var go = new GameObject("CharacterTab_" + labels[i], typeof(RectTransform),
                typeof(Image), typeof(Button));
            go.transform.SetParent(heroInfoPanel.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(i == 0 ? 0.08f : 0.51f, 0.72f);
            rect.anchorMax = new Vector2(i == 0 ? 0.49f : 0.91f, 0.83f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.17f, 0.12f, 0.08f);
            go.GetComponent<Button>().onClick.AddListener(() => ShowCharacterInfo(selected == 1));
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = labels[i];
            text.fontSize = 19f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.84f, 0.49f);
            text.raycastTarget = false;
            var underline = new GameObject("GoldUnderline", typeof(RectTransform), typeof(Image));
            underline.transform.SetParent(go.transform, false);
            var line = underline.GetComponent<RectTransform>();
            line.anchorMin = new Vector2(0.08f, 0f);
            line.anchorMax = new Vector2(0.92f, 0f);
            line.sizeDelta = new Vector2(0f, 3f);
            line.anchoredPosition = Vector2.zero;
            underline.GetComponent<Image>().color = new Color(1f, 0.75f, 0.24f);
            characterUnderlines[i] = underline;
            underline.SetActive(i == 0);
        }
        if (heroUpgradeButton != null)
        {
            var icon = new GameObject("GoldIcon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(heroUpgradeButton.transform, false);
            var rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.07f, 0.2f);
            rect.anchorMax = new Vector2(0.17f, 0.8f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = icon.GetComponent<Image>();
            image.sprite = BaseVisualCatalog.Instance?.goldIcon ?? SpriteManager.Instance?.IconGold;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }

    private void OnBuildingUpgradedEvent(BuildingType type, int level)
    {
        UpdateAllUI();
    }

    private void CloseAllPanels()
    {
        if (buildingInfoPanel != null) buildingInfoPanel.GetComponent<PanelController>()?.Hide();
        if (heroInfoPanel != null) heroInfoPanel.GetComponent<PanelController>()?.Hide();
        BuildMenuController.Instance?.Hide();
        SetModalBlocker(false);
    }
    
    public void HideAllPanelsPublic()
    {
        CloseAllPanels();
    }

    public void OnBuildMenuClosed()
    {
        if (!IsInfoPanelOpen) SetModalBlocker(false);
    }

    public void OnBuildMenuOpened()
    {
        SetModalBlocker(true);
    }

    private void OnBuildMenuClicked()
    {
        if (buildingInfoPanel != null) buildingInfoPanel.GetComponent<PanelController>()?.HideImmediate();
        if (heroInfoPanel != null) heroInfoPanel.GetComponent<PanelController>()?.HideImmediate();
        SetModalBlocker(true);
        BuildMenuController.Instance?.Show();
    }

    // ==================== YARDIMCI ====================

    private string GetBuildingDisplayName(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.MainBase: return "Ana Üs";
            case BuildingType.ArcherTower: return "Okçu Kulesi";
            case BuildingType.CannonTower: return "Topçu Kulesi";
            case BuildingType.GoldMine: return "Altın Madeni";
            case BuildingType.Monastery: return "Manastır";
            default: return type.ToString();
        }
    }

    private string GetNextMainBaseUnlock(int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        var unlocked = new List<string>();
        foreach (var entry in GameManager.Instance.Buildings)
            if (entry.Key != BuildingType.MainBase && entry.Value.GetRequiredMainBaseLevel() == nextLevel)
                unlocked.Add(GetBuildingDisplayName(entry.Key));
        if (nextLevel >= 3 && nextLevel % 2 == 1)
            unlocked.Add("yeni arazi");
        return unlocked.Count > 0 ? $"Sev. {nextLevel}: {string.Join(" + ", unlocked)}" : "";
    }

    private string GetBuildingStatsText(BuildingData building)
    {
        if (!building.isUnlocked || building.level <= 0)
            return "Henüz inşa edilmedi.";

        switch (building.buildingType)
        {
            case BuildingType.MainBase:
                return $"Can: {building.maxHealth:F0}";

            case BuildingType.ArcherTower:
            case BuildingType.CannonTower:
                return $"Can: {building.maxHealth:F0}\n" +
                       $"Hasar: {building.damage:F0}\n" +
                       $"Birim: {building.unitCount}\n" +
                       $"Menzil: {building.attackRange:F1}";

            case BuildingType.GoldMine:
                return $"Can: {building.maxHealth:F0}\n" +
                       $"Üretim: {building.goldPerSecond:F1} altın/sn";

            case BuildingType.Monastery:
                return $"Can: {building.maxHealth:F0}\nİyileştirme: 3 sn'de %3\nMenzil: 4";


            default:
                return "";
        }
    }

    private string GetWeaponName(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Axe: return "Balta";
            case WeaponType.Spear: return "Mızrak";
            case WeaponType.Bow: return "Ok";
            default: return type.ToString();
        }
    }
}
