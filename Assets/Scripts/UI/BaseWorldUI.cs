using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Üs dünyasındaki UI — bina bilgileri, altın, seviye yükseltme butonları
/// </summary>
public class BaseWorldUI : MonoBehaviour
{
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

    private BuildingType selectedBuilding;

    /// <summary>
    /// Programatik kurulum — BaseSceneSetup tarafından çağrılır
    /// </summary>
    public void SetReferences(
        TextMeshProUGUI gold, TextMeshProUGUI level, TextMeshProUGUI gps,
        GameObject bPanel, TextMeshProUGUI bName, TextMeshProUGUI bLevel, TextMeshProUGUI bStats, TextMeshProUGUI bCost,
        Button upgradeBtn, Button closeBtn,
        GameObject hPanel, TextMeshProUGUI hName, TextMeshProUGUI hLevel, TextMeshProUGUI hStats, TextMeshProUGUI hCost,
        Button heroUpBtn, Button heroCloseBtn,
        Button battleBtn)
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
        mineStoredText = null;
    }

    private void Start()
    {
        // Panel'leri gizle
        if (buildingInfoPanel != null) buildingInfoPanel.SetActive(false);
        if (heroInfoPanel != null) heroInfoPanel.SetActive(false);

        // Buton listener'ları
        if (battleButton != null)
            battleButton.onClick.AddListener(OnBattleClicked);

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
        // Event'lerden çık
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged -= UpdateGoldDisplay;
            GameManager.Instance.OnBuildingUpgraded -= OnBuildingUpgradedEvent;
            GameManager.Instance.OnGoldMineStoredChanged -= OnGoldMineStoredChanged;
        }
    }

    // ==================== UI GÜNCELLEME ====================

    private void UpdateAllUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null) return;

        UpdateGoldDisplay(GameManager.Instance.Gold);

        if (battleLevelText != null)
            battleLevelText.text = $"Savas Lv.{GameManager.Instance.PlayerData.currentBattleLevel}";

        if (goldPerSecText != null)
        {
            float gps = GameManager.Instance.GetGoldPerSecond();
            goldPerSecText.text = gps > 0 ? $"+{gps:F1} altin/sn" : "";
        }

        OnGoldMineStoredChanged(GameManager.Instance.GoldMineStored, GameManager.Instance.GetGoldMineStorageCapacity());
    }

    private int lastKnownGold = -1;

    private void UpdateGoldDisplay(int gold)
    {
        if (goldText != null)
            goldText.text = $"{gold}";

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
        BuildingData building = GameManager.Instance.Buildings[type];

        if (heroInfoPanel != null) heroInfoPanel.SetActive(false);
        if (buildingInfoPanel != null) buildingInfoPanel.SetActive(true);

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
                upgradeCostText.text = $"Yükselt: {building.GetUpgradeCost()} Altın";
            }
        }

        // Buton aktiflik
        if (upgradeButton != null)
        {
            upgradeButton.interactable = building.isUnlocked
                && building.level < maxLvl
                && GameManager.Instance.Gold >= building.GetUpgradeCost();
        }
    }

    /// <summary>
    /// Kahraman bilgilerini göster
    /// </summary>
    public void ShowHeroInfo()
    {
        if (GameManager.Instance == null) return;

        if (buildingInfoPanel != null) buildingInfoPanel.SetActive(false);
        if (heroInfoPanel != null) heroInfoPanel.SetActive(true);

        HeroData hero = GameManager.Instance.PlayerData.hero;

        if (heroNameText != null)
            heroNameText.text = $"Ana Savaşçı ({GetWeaponName(hero.weaponType)})";

        if (heroLevelText != null)
            heroLevelText.text = $"Seviye {hero.level}";

        if (heroStatsText != null)
        {
            heroStatsText.text = $"Can: {hero.maxHealth:F0}\n" +
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
        GameManager.Instance.StartBattle();
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
        if (GameManager.Instance.UpgradeHero())
        {
            ShowHeroInfo(); // UI'ı güncelle
        }
    }

    private void OnBuildingUpgradedEvent(BuildingType type, int level)
    {
        UpdateAllUI();
    }

    private void CloseAllPanels()
    {
        if (buildingInfoPanel != null) buildingInfoPanel.SetActive(false);
        if (heroInfoPanel != null) heroInfoPanel.SetActive(false);
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
            case BuildingType.WallBuilder: return "Duvarcı";
            default: return type.ToString();
        }
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

            case BuildingType.WallBuilder:
                return $"Can: {building.maxHealth:F0}";


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
