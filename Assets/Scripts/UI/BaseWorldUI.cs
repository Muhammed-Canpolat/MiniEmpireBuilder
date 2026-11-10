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
        // Dock'a sonradan eklenen Kahraman düğmesi de aynı anda gizlenir/görünür.
        var hero = buildMenuButton != null ? buildMenuButton.transform.parent?.Find("HeroBtn") : null;
        if (hero != null) hero.gameObject.SetActive(show);
    }

    // ==================== UI GÜNCELLEME ====================


    private void UpdateAllUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null) return;

        UpdateGoldDisplay(GameManager.Instance.Gold);

        if (battleLevelText != null)
            battleLevelText.text = L.T("ANA ÜS · SEV. ", "MAIN BASE · LV. ") + GameManager.Instance.GetMainBaseLevel();

        if (goldPerSecText != null)
        {
            float gps = GameManager.Instance.GetGoldPerSecond();
            goldPerSecText.text = gps > 0 ? $"+{gps:F1}/sn" : "";
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
            bool maxed = mainBase.level >= mainBase.GetMaxLevel(mainBase.level);
            levelFill.anchorMax = new Vector2(maxed || cost <= 0 ? 1f : Mathf.Clamp01((float)gold / cost), 1f);
            var caption = levelFill.parent.parent.Find("BaseLevelCaption")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (caption != null)
                caption.text = maxed ? L.T("En yüksek seviye", "Max level")
                    : gold >= cost ? L.T($"<color=#F0C24A>Sev. {mainBase.level + 1} yükseltmesine hazır!</color>", $"<color=#F0C24A>Ready to upgrade to Lv. {mainBase.level + 1}!</color>")
                    : L.T($"Sev. {mainBase.level + 1} için {gold}/{cost} altın", $"Lv. {mainBase.level + 1}: {gold}/{cost} gold");
        }
        if (buildingInfoPanel != null && buildingInfoPanel.activeSelf)
            ShowBuildingInfo(selectedBuilding, selectedSlotId);

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
            // Tek satır: saniyelik gelir. Depo doluluğu madenin üstündeki balonda gösterilir.
            float gps = GameManager.Instance != null ? GameManager.Instance.GetGoldPerSecond() : 0f;
            goldPerSecText.text = gps > 0 ? $"+{gps:F1}/sn" : "";
            return;
        }

        mineStoredText.text = L.T("Maden: ", "Mine: ") + $"{stored}/{capacity}";
    }

    // ==================== BİNA PANELİ ====================

    /// <summary>
    /// Bir binaya tıklayınca çağır — bina bilgilerini gösterir
    /// </summary>
    public void ShowBuildingInfo(BuildingType type) => ShowBuildingInfo(type, null);

    /// <summary>
    /// Bir binanın bilgisini göster. slotId verilmezse türün en yüksek seviyeli binası seçilir.
    /// Seviye, istatistik ve yükseltme bu binaya aittir (aynı türden iki bina ayrı seviyelenir).
    /// </summary>
    public void ShowBuildingInfo(BuildingType type, string slotId)
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.Buildings.ContainsKey(type)) return;

        if (string.IsNullOrEmpty(slotId)) slotId = HighestSlotOf(type);
        bool reopen = buildingInfoPanel == null || !buildingInfoPanel.activeSelf
            || selectedBuilding != type || selectedSlotId != slotId;
        selectedBuilding = type;
        selectedSlotId = slotId;
        BuildingData typeData = gm.Buildings[type];
        BuildingData building = gm.GetInstanceData(slotId, type);
        building.isUnlocked = typeData.isUnlocked;

        if (reopen)
        {
            BuildMenuController.Instance?.Hide();
            if (heroInfoPanel != null) heroInfoPanel.GetComponent<PanelController>()?.HideImmediate();
            if (buildingInfoPanel != null) buildingInfoPanel.GetComponent<PanelController>()?.Show();
            SetModalBlocker(true);
        }

        if (buildingNameText != null)
            buildingNameText.text = GetBuildingDisplayName(type);

        if (buildingLevelText != null)
            buildingLevelText.text = !typeData.isUnlocked ? L.T("KİLİTLİ", "LOCKED")
                : L.T("Seviye ", "Level ") + $"{building.level} / {Progression.MaxLevel}";

        if (buildingStatsText != null)
            buildingStatsText.text = GetBuildingStatsText(building);

        var task = gm.TaskForSlot(slotId);
        int cost = building.GetUpgradeCost();
        string duration = Progression.FormatDuration(Progression.UpgradeSecondsTo(building.level + 1));
        var block = gm.CanUpgrade(slotId, type);

        if (upgradeCostText != null)
        {
            if (!typeData.isUnlocked)
                upgradeCostText.text = L.T($"Ana Üs Sev. {typeData.GetRequiredMainBaseLevel()} gerekli", $"Requires Main Base Lv. {typeData.GetRequiredMainBaseLevel()}");
            else if (task != null)
            {
                // Süren iş: düğme "hemen bitir" olur (1 dk altı bedava).
                int finish = GameManager.FinishNowCost(task);
                string left = Progression.FormatDuration(GameManager.SecondsRemaining(task));
                upgradeCostText.text = finish > 0 ? L.T($"HEMEN BİTİR · {finish} ELMAS · {left}", $"FINISH NOW · {finish} GEMS · {left}") : L.T($"BEDAVA BİTİR · {left}", $"FINISH FREE · {left}");
            }
            else if (building.level >= Progression.MaxLevel)
                upgradeCostText.text = L.T("EN YÜKSEK SEVİYE", "MAX LEVEL");
            else if (block == GameManager.UpgradeBlock.MaxLevel)
                upgradeCostText.text = L.T("Önce Ana Üs Sev. ", "First Main Base Lv. ") + (building.level + (type == BuildingType.GoldMine ? 0 : 1));
            else if (block == GameManager.UpgradeBlock.BuilderBusy)
                upgradeCostText.text = gm.BuilderCount > 1
                    ? L.T("TÜM İNŞAATÇILAR MEŞGUL · ", "ALL BUILDERS BUSY · ") + Progression.FormatDuration(gm.BuilderSecondsRemaining)
                    : L.T("İNŞAATÇI MEŞGUL · ", "BUILDER BUSY · ") + Progression.FormatDuration(gm.BuilderSecondsRemaining);
            else if (block == GameManager.UpgradeBlock.NoGold)
                upgradeCostText.text = L.T("YÜKSELT · ", "UPGRADE · ") + $"<color=#C2452D>{cost}</color> · {duration}";
            else
                upgradeCostText.text = L.T("YÜKSELT · ", "UPGRADE · ") + $"{cost} · {duration}";
        }

        if (upgradeButton != null)
        {
            // Altın eksikse elmasla tamamlanabilir; süren iş elmasla bitirilebilir.
            bool canUpgrade = block == GameManager.UpgradeBlock.None || block == GameManager.UpgradeBlock.NoGold
                || (task != null && typeData.isUnlocked);
            upgradeButton.interactable = canUpgrade;
            var buttonImage = upgradeButton.GetComponent<Image>();
            if (buttonImage != null) buttonImage.color = canUpgrade ? Color.white : new Color(0.65f, 0.65f, 0.65f, 0.75f);
        }

        var next = buildingInfoPanel?.transform.Find("NextUnlock")?.GetComponent<TextMeshProUGUI>();
        if (next != null)
            next.text = type == BuildingType.MainBase ? GetNextMainBaseUnlock(building.level) : "";
    }

    private string selectedSlotId;
    private float nextInfoRefresh;

    /// <summary>Panel açıkken geri sayım ve inşaatçı durumu saniyede iki kez yenilenir.</summary>
    private void Update()
    {
        if (Time.unscaledTime < nextInfoRefresh) return;
        nextInfoRefresh = Time.unscaledTime + 0.5f;
        if (buildingInfoPanel != null && buildingInfoPanel.activeSelf)
            ShowBuildingInfo(selectedBuilding, selectedSlotId);
    }

    private static string HighestSlotOf(BuildingType type)
    {
        string best = null;
        int bestLevel = -1;
        var placed = GameManager.Instance?.PlayerData?.placedBuildings;
        if (placed != null)
            foreach (var entry in placed)
                if (entry != null && entry.buildingType == type.ToString() && entry.level > bestLevel)
                {
                    bestLevel = entry.level;
                    best = entry.slotId;
                }
        return best;
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
            if (heroNameText != null) heroNameText.text = L.T("ÇIRAK", "APPRENTICE");
            if (heroLevelText != null) heroLevelText.text = L.T("Seviye ", "Level ") + apprenticeLevel;
            if (heroStatsText != null)
            {
                var progression = ApprenticeProgressionData.Instance;
                if (progression != null)
                {
                    int next = Mathf.Min(GameManager.Instance.GetApprenticeMaxLevel(), apprenticeLevel + 1);
                    string Arrow(int from, int to) => next == apprenticeLevel ? from.ToString()
                        : $"{from} → {to} ▲";
                    heroStatsText.text = L.T("Can ", "Health ") + $"{Arrow(progression.Health(hero.weaponType, apprenticeLevel), progression.Health(hero.weaponType, next))}\n" +
                        L.T("Hasar ", "Damage ") + $"{Arrow(progression.Damage(hero.weaponType, apprenticeLevel), progression.Damage(hero.weaponType, next))}\n" +
                        L.T($"Maden +%{progression.MineBonus(apprenticeLevel)}", $"Mine +{progression.MineBonus(apprenticeLevel)}%") +
                        (next == apprenticeLevel ? "" : $" → +%{progression.MineBonus(next)} ▲");
                }
            }
            if (heroUpgradeCostText != null)
                heroUpgradeCostText.text = apprenticeLevel >= Progression.MaxLevel ? L.T("EN YÜKSEK SEVİYE", "MAX LEVEL")
                    : apprenticeLevel >= GameManager.Instance.GetApprenticeMaxLevel()
                        ? L.T("Ana Üs seviyesini yükselt!", "Upgrade your Main Base!") : L.T("YÜKSELT · ", "UPGRADE · ") + apprenticeCost;
            if (heroUpgradeButton != null)
                heroUpgradeButton.interactable = apprenticeLevel < GameManager.Instance.GetApprenticeMaxLevel() &&
                    GameManager.Instance.Gold >= apprenticeCost;
            return;
        }

        if (heroNameText != null)
            heroNameText.text = L.T("Ana Savaşçı", "Main Warrior");

        if (heroLevelText != null)
            heroLevelText.text = L.T("Seviye ", "Level ") + hero.level;

        if (heroStatsText != null)
        {
            // Bir sonraki seviyenin değerleri ok ile gösterilir (kalkan: %10 + seviye başına %2).
            var next = new HeroData { weaponType = hero.weaponType,
                level = Mathf.Min(hero.level + 1, GameManager.Instance.GetHeroMaxLevel()) };
            next.ApplyLevelStats();
            bool more = next.level > hero.level;
            string Up(string now, string then) => more && now != then ? $"{now} → {then} ▲" : now;
            heroStatsText.text = $"XP: —\n" +
                                 L.T("Can: ", "Health: ") + $"{Up(hero.maxHealth.ToString("F0"), next.maxHealth.ToString("F0"))}\n" +
                                 L.T("Hasar: ", "Damage: ") + $"{Up(hero.damage.ToString("F0"), next.damage.ToString("F0"))}\n" +
                                 L.T("Kalkan: ", "Shield: ") + $"{Up("%" + Mathf.RoundToInt(hero.shield * 100f), "%" + Mathf.RoundToInt(next.shield * 100f))}\n" +
                                 L.T("Menzil: ", "Range: ") + $"{hero.attackRange:F1}\n" +
                                 L.T($"Saldırı Hızı: {hero.attackSpeed:F1}/sn", $"Attack Speed: {hero.attackSpeed:F1}/s");
        }

        int maxHeroLvl = GameManager.Instance.GetHeroMaxLevel();
        int cost = GameManager.Instance.GetHeroUpgradeCost();
        if (heroUpgradeCostText != null)
        {
            if (hero.level >= Progression.MaxLevel)
                heroUpgradeCostText.text = L.T("EN YÜKSEK SEVİYE", "MAX LEVEL");
            else if (hero.level >= maxHeroLvl)
                heroUpgradeCostText.text = L.T("Ana Üs seviyesini yükselt!", "Upgrade your Main Base!");
            else
                heroUpgradeCostText.text = L.T("YÜKSELT · ", "UPGRADE · ") + cost;
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
        var gm = GameManager.Instance;
        string slot = selectedSlotId;
        var type = selectedBuilding;
        var task = gm.TaskForSlot(slot);
        if (task != null)
        {
            GemActions.OfferFinish(task, () => ShowBuildingInfo(type, slot));
            return;
        }
        if (gm.CanUpgrade(slot, type) == GameManager.UpgradeBlock.NoGold)
        {
            int cost = Progression.UpgradeCost(type, gm.GetInstanceLevel(slot, type) + 1);
            GemActions.OfferGoldTopUp(cost, L.T("Bu yükseltme", "This upgrade"), () => gm.StartUpgrade(slot, type), () =>
            {
                AudioManager.Instance?.PlaySfx("buildingPlace");
                ShowBuildingInfo(type, slot);
            });
            return;
        }
        if (gm.StartUpgrade(selectedSlotId, selectedBuilding))
        {
            AudioManager.Instance?.PlaySfx("buildingPlace");
            ShowBuildingInfo(selectedBuilding, selectedSlotId); // UI'ı güncelle
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
        string[] labels = { L.T("KAHRAMAN", "HERO"), L.T("ÇIRAK", "APPRENTICE") };
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
            case BuildingType.MainBase: return L.T("Ana Üs", "Main Base");
            case BuildingType.ArcherTower: return L.T("Okçu Kulesi", "Archer Tower");
            case BuildingType.CannonTower: return L.T("Topçu Kulesi", "Cannon Tower");
            case BuildingType.GoldMine: return L.T("Altın Madeni", "Gold Mine");
            case BuildingType.Monastery: return L.T("Manastır", "Monastery");
            default: return type.ToString();
        }
    }

    private string GetNextMainBaseUnlock(int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        if (nextLevel > Progression.MaxLevel) return "";
        var unlocked = new List<string>();
        foreach (var type in new[] { BuildingType.ArcherTower, BuildingType.CannonTower, BuildingType.Monastery })
        {
            if (Progression.FirstUnlockLevel(type) == nextLevel) unlocked.Add(GetBuildingDisplayName(type));
            else if (Progression.SecondUnlockLevel(type) == nextLevel) unlocked.Add("2. " + GetBuildingDisplayName(type));
        }
        if (BaseWorldLayout.NextLandLevel(currentLevel) == nextLevel) unlocked.Add(L.T("yeni arazi", "new land"));
        foreach (var region in CampaignCatalog.Regions)
            if (region.requiredBaseLevel == nextLevel) unlocked.Add(L.Asset(region.displayName));
        return unlocked.Count > 0 ? L.T("Sev. ", "Lv. ") + $"{nextLevel}: {string.Join(" + ", unlocked)}" : "";
    }

    private string GetBuildingStatsText(BuildingData building)
    {
        if (!building.isUnlocked || building.level <= 0)
            return L.T("Henüz inşa edilmedi.", "Not built yet.");

        switch (building.buildingType)
        {
            case BuildingType.MainBase:
                return L.T("Can: ", "Health: ") + $"{building.maxHealth:F0}";

            case BuildingType.ArcherTower:
            case BuildingType.CannonTower:
                return L.T("Can: ", "Health: ") + $"{building.maxHealth:F0}\n" +
                       L.T("Hasar: ", "Damage: ") + $"{building.damage:F0}\n" +
                       L.T("Birim: ", "Units: ") + $"{building.unitCount}\n" +
                       L.T("Menzil: ", "Range: ") + $"{building.attackRange:F1}";

            case BuildingType.GoldMine:
                return L.T("Can: ", "Health: ") + $"{building.maxHealth:F0}\n" +
                       L.T($"Üretim: {building.goldPerSecond:F1} altın/sn", $"Production: {building.goldPerSecond:F1} gold/s");

            case BuildingType.Monastery:
                return L.T(L.T("Can: ", "Health: ") + $"{building.maxHealth:F0}\nİyileştirme: 3 sn'de %3\nMenzil: 4", $"Health: {building.maxHealth:F0}\nHealing: 3% every 3 s\nRange: 4");


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
