using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Ekranın altından kayarak açılan inşa menüsü.
/// Yapı ve dekor kartlarını gösterir.
/// </summary>
public class BuildMenuController : MonoBehaviour
{
    public static BuildMenuController Instance { get; private set; }

    private GameObject _panelRoot;
    private RectTransform _panelRt;
    private PanelController _panelController;
    private bool _isOpen;
    public bool IsOpen => _isOpen;

    private List<Button> _itemButtons = new List<Button>();
    private readonly List<Button> _decorButtons = new List<Button>();
    private readonly List<GameObject> _structureCards = new List<GameObject>();
    private readonly List<GameObject> _decorCards = new List<GameObject>();
    private readonly GameObject[] _tabUnderlines = new GameObject[2];
    private int _activeTab;
    private GameObject _recommendedButton;
    private GameObject _recommendedConfirm;

    // ==================== KURULUM ====================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Initialize(Transform canvasTransform)
    {
        CreateUI(canvasTransform);
        DarkFantasyHudPolish.PolishBuildMenu(_panelRoot.transform);
        _panelController = _panelRoot.AddComponent<PanelController>();
        _panelController.Initialize();
    }


    private void CreateUI(Transform canvasTransform)
    {
        // Panel Root
        _panelRoot = new GameObject("BuildMenuPanel");
        _panelRoot.transform.SetParent(canvasTransform, false);

        _panelRt = _panelRoot.AddComponent<RectTransform>();
        _panelRt.anchorMin = new Vector2(0f, 0f);
        _panelRt.anchorMax = new Vector2(1f, 0.35f);
        _panelRt.offsetMin = _panelRt.offsetMax = Vector2.zero;
        
        Image bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        // Üst kenarlık (altın)
        GameObject border = new GameObject("Border");
        border.transform.SetParent(_panelRoot.transform, false);
        RectTransform borderRt = border.AddComponent<RectTransform>();
        borderRt.anchorMin = new Vector2(0f, 0.98f);
        borderRt.anchorMax = new Vector2(1f, 1f);
        borderRt.offsetMin = borderRt.offsetMax = Vector2.zero;
        border.AddComponent<Image>().color = new Color(1f, 0.85f, 0.2f, 0.9f);

        // Kapat Butonu (Sağ üst küçük x)
        GameObject closeBtnGO = new GameObject("CloseBtn");
        closeBtnGO.transform.SetParent(_panelRoot.transform, false);
        RectTransform closeRt = closeBtnGO.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.9f, 0.8f);
        closeRt.anchorMax = new Vector2(0.98f, 0.95f);
        closeRt.offsetMin = closeRt.offsetMax = Vector2.zero;
        Image closeImg = closeBtnGO.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f);
        Button closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.onClick.AddListener(Hide);
        
        GameObject closeTextGO = new GameObject("Text");
        closeTextGO.transform.SetParent(closeBtnGO.transform, false);
        RectTransform closeTextRt = closeTextGO.AddComponent<RectTransform>();
        closeTextRt.anchorMin = Vector2.zero; closeTextRt.anchorMax = Vector2.one;
        closeTextRt.offsetMin = closeTextRt.offsetMax = Vector2.zero;
        TextMeshProUGUI closeTmp = closeTextGO.AddComponent<TextMeshProUGUI>();
        closeTmp.text = "X";
        closeTmp.color = Color.white;
        closeTmp.alignment = TextAlignmentOptions.Center;
        closeTmp.fontSize = 24f;

        // Grid (Horizontal Layout Group için basitleştirilmiş yatay dizilim)
        // Yapı kartları sekmenin altında gösterilir.
        // Gerçekte Ana Üs ve Altın Madeni ilk başta sahneye yerleşmeli, ancak menüden de yerleştirilebilir.
        // Maliyetleri BuildingData GetUpgradeCost üzerinden ilk seviye için hesaplayabiliriz. Veya sabit tutabiliriz.

        CreateTab("YAPI", 0, 0.05f, 0.43f);
        CreateTab("DEKOR", 1, 0.48f, 0.86f);
        CreateItem(BuildingType.ArcherTower, 0.03f, 0.32f, 50);
        CreateItem(BuildingType.CannonTower, 0.35f, 0.64f, 80);
        CreateItem(BuildingType.Monastery, 0.67f, 0.96f, 60);
        var decor = DecorPlacementController.All;
        string[] decorOrder = { "tree", "bush", "rock", "flowers", "lantern",
            "stone_path", "fountain", "flag" };
        System.Array.Sort(decor, (a, b) =>
            System.Array.IndexOf(decorOrder, a.id).CompareTo(System.Array.IndexOf(decorOrder, b.id)));
        for (int i = 0; i < decor.Length; i++) CreateDecorItem(decor[i], i);
        SelectTab(0);
        CreateRecommendedLayoutControl();
    }

    private void CreateRecommendedLayoutControl()
    {
        var button = ReferenceUiVisuals.Button(_panelRoot.transform, "RecommendedLayout",
            "ÖNERİLEN DÜZEN", false, new Vector2(0.26f, 0.015f),
            new Vector2(0.74f, 0.10f), Vector2.zero, Vector2.zero, 13f);
        _recommendedButton = button.gameObject;
        button.onClick.AddListener(() => _recommendedConfirm?.SetActive(true));
        var confirm = ReferenceUiVisuals.Image(_panelRoot.transform, "RecommendedConfirm",
            new Vector2(0.07f, 0.24f), new Vector2(0.93f, 0.77f),
            Vector2.zero, Vector2.zero, ReferenceUiVisuals.Panel);
        _recommendedConfirm = confirm.gameObject;
        ReferenceUiVisuals.Text(confirm.transform, "Question",
            "Binalar önerilen konumlara taşınsın mı? Dekorlar korunur.", 16,
            ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            new Vector2(0.06f, 0.44f), new Vector2(0.94f, 0.91f),
            Vector2.zero, Vector2.zero);
        var yes = ReferenceUiVisuals.Button(confirm.transform, "Confirm", "UYGULA", true,
            new Vector2(0.08f, 0.09f), new Vector2(0.48f, 0.39f),
            Vector2.zero, Vector2.zero, 15f);
        yes.onClick.AddListener(() =>
        {
            BaseMapManager.Instance?.ApplyRecommendedLayout();
            _recommendedConfirm.SetActive(false);
            Hide();
        });
        var no = ReferenceUiVisuals.Button(confirm.transform, "Cancel", "VAZGEÇ", false,
            new Vector2(0.52f, 0.09f), new Vector2(0.92f, 0.39f),
            Vector2.zero, Vector2.zero, 15f);
        no.onClick.AddListener(() => _recommendedConfirm.SetActive(false));
        _recommendedConfirm.SetActive(false);
    }

    private void CreateTab(string title, int index, float left, float right)
    {
        var go = new GameObject("Tab_" + title, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_panelRoot.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(left, 0.79f);
        rect.anchorMax = new Vector2(right, 0.95f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.15f, 0.11f, 0.07f, 0.9f);
        go.GetComponent<Button>().onClick.AddListener(() => SelectTab(index));
        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = title;
        text.fontSize = 21f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.87f, 0.56f);
        text.raycastTarget = false;
        var underline = new GameObject("GoldUnderline", typeof(RectTransform), typeof(Image));
        underline.transform.SetParent(go.transform, false);
        var line = underline.GetComponent<RectTransform>();
        line.anchorMin = new Vector2(0.08f, 0f);
        line.anchorMax = new Vector2(0.92f, 0f);
        line.pivot = new Vector2(0.5f, 0f);
        line.sizeDelta = new Vector2(0f, 3f);
        line.anchoredPosition = Vector2.zero;
        underline.GetComponent<Image>().color = new Color(1f, 0.76f, 0.25f);
        _tabUnderlines[index] = underline;
    }

    private void SelectTab(int index)
    {
        _activeTab = index;
        if (_recommendedButton != null) _recommendedButton.SetActive(index == 0);
        foreach (var item in _structureCards) item.SetActive(index == 0);
        foreach (var item in _decorCards) item.SetActive(index == 1);
        for (int i = 0; i < _tabUnderlines.Length; i++)
        {
            if (_tabUnderlines[i] == null) continue;
            _tabUnderlines[i].SetActive(i == index);
            var image = _tabUnderlines[i].transform.parent.GetComponent<Image>();
            if (image != null) image.color = i == index
                ? ReferenceUiVisuals.Hex(0x2A2118) : ReferenceUiVisuals.Hex(0x14110D);
        }
        var hint = _panelRoot?.transform.Find("BuildMenuHint")?.GetComponent<TextMeshProUGUI>();
        if (hint != null)
        {
            bool hasLocked = GameManager.Instance != null &&
                (GameManager.Instance.Buildings.TryGetValue(BuildingType.ArcherTower, out var archer) && !archer.isUnlocked ||
                 GameManager.Instance.Buildings.TryGetValue(BuildingType.CannonTower, out var cannon) && !cannon.isUnlocked ||
                 GameManager.Instance.Buildings.TryGetValue(BuildingType.Monastery, out var monastery) && !monastery.isUnlocked);
            hint.gameObject.SetActive(index == 1 || hasLocked);
            hint.text = index == 0 ? "Kilitli yapılar üs geliştikçe açılır." :
                "Dekora uzun bas: taşı veya sil (%50 iade).";
        }
        RefreshButtons();
    }

    private void CreateDecorItem(DecorData data, int index)
    {
        if (data == null) return;
        int column = index % 4;
        int row = index / 4;
        var go = new GameObject("Decor_" + data.id, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_panelRoot.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.02f + column * 0.245f, row == 0 ? 0.44f : 0.12f);
        rect.anchorMax = new Vector2(0.245f + column * 0.245f, row == 0 ? 0.72f : 0.40f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.14f, 0.1f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            if (GameManager.Instance == null ||
                GameManager.Instance.GetMainBaseLevel() < data.unlockMainBaseLevel ||
                GameManager.Instance.Gold < data.cost) return;
            Hide();
            DecorPlacementController.Instance?.BeginPlacement(data);
        });
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(go.transform, false);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.25f, 0.38f);
        iconRect.anchorMax = new Vector2(0.75f, 0.95f);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
        icon.GetComponent<Image>().sprite = data.sprite;
        icon.GetComponent<Image>().preserveAspect = true;
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.19f);
        labelRect.anchorMax = new Vector2(1f, 0.43f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var text = label.GetComponent<TextMeshProUGUI>();
        text.text = data.displayName;
        text.fontSize = 12f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var price = new GameObject("Cost", typeof(RectTransform), typeof(TextMeshProUGUI));
        price.transform.SetParent(go.transform, false);
        var priceRect = price.GetComponent<RectTransform>();
        priceRect.anchorMin = Vector2.zero;
        priceRect.anchorMax = new Vector2(1f, 0.2f);
        priceRect.offsetMin = priceRect.offsetMax = Vector2.zero;
        var priceText = price.GetComponent<TextMeshProUGUI>();
        priceText.text = data.cost.ToString();
        priceText.fontSize = 13f;
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.color = new Color(1f, 0.83f, 0.35f);
        _decorCards.Add(go);
        _decorButtons.Add(button);
    }

    private void CreateItem(BuildingType type, float minX, float maxX, int cost)
    {
        GameObject itemGO = new GameObject($"Item_{type}");
        itemGO.transform.SetParent(_panelRoot.transform, false);
        RectTransform rt = itemGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(minX, 0.08f);
        rt.anchorMax = new Vector2(maxX, 0.70f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image bg = itemGO.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f);

        Button btn = itemGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        _itemButtons.Add(btn);
        _structureCards.Add(itemGO);

        // İkon (Geçici)
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(itemGO.transform, false);
        RectTransform iconRt = iconGO.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.2f, 0.4f);
        iconRt.anchorMax = new Vector2(0.8f, 0.9f);
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = BaseMapManager.GetBuildingColor(type);
        if (SpriteManager.Instance != null)
        {
            Sprite s = SpriteManager.Instance.GetBuildingSprite(type);
            if (s != null) iconImg.sprite = s;
        }

        // İsim
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(itemGO.transform, false);
        RectTransform nameRt = nameGO.AddComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.2f);
        nameRt.anchorMax = new Vector2(1f, 0.4f);
        nameRt.offsetMin = nameRt.offsetMax = Vector2.zero;
        TextMeshProUGUI nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text = BaseMapManager.GetBuildingDisplayName(type);
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.fontSize = 18f;

        // Cost
        GameObject costGO = new GameObject("Cost");
        costGO.transform.SetParent(itemGO.transform, false);
        RectTransform costRt = costGO.AddComponent<RectTransform>();
        costRt.anchorMin = new Vector2(0f, 0f);
        costRt.anchorMax = new Vector2(1f, 0.2f);
        costRt.offsetMin = costRt.offsetMax = Vector2.zero;
        TextMeshProUGUI costTmp = costGO.AddComponent<TextMeshProUGUI>();
        costTmp.text = $"{cost} Altın";
        costTmp.color = new Color(1f, 0.85f, 0.2f);
        costTmp.alignment = TextAlignmentOptions.Center;
        costTmp.fontSize = 16f;

        var countGo = new GameObject("PlacedCount", typeof(RectTransform), typeof(TextMeshProUGUI));
        countGo.transform.SetParent(itemGO.transform, false);
        var countRect = countGo.GetComponent<RectTransform>();
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = new Vector2(1f, 0.12f);
        countRect.offsetMin = countRect.offsetMax = Vector2.zero;
        var countText = countGo.GetComponent<TextMeshProUGUI>();
        countText.fontSize = 12f;
        countText.color = ReferenceUiVisuals.Muted;
        countText.alignment = TextAlignmentOptions.Center;

        btn.onClick.AddListener(() =>
        {
            if (GameManager.Instance != null)
            {
                // Unlocked kontrolü
                BuildingData bd = null;
                if (GameManager.Instance.Buildings.TryGetValue(type, out bd))
                {
                    if (!bd.isUnlocked)
                    {
                        Debug.Log($"[BuildMenu] {type} kilitli! (Ana Üs Lv.{bd.GetRequiredMainBaseLevel()} gerekli)");
                        return; // Kilitli
                    }
                }

                // Max sayı kontrolü
                int currentCount = BaseMapManager.Instance?.GetPlacedCount(type) ?? 0;
                int maxCount = BuildingData.GetMaxBuildCount(type, GameManager.Instance.GetMainBaseLevel());
                
                if (currentCount >= maxCount)
                {
                    Debug.Log($"[BuildMenu] {type} maksimum limite ulaşıldı! ({currentCount}/{maxCount})");
                    return;
                }

                if (GameManager.Instance.Gold >= cost)
                {
                    Hide();
                    PlacementController.Instance?.BeginPlacement(type, 60f, cost); // Varsayılan 60s inşa süresi
                }
                else
                {
                    Debug.Log("[BuildMenu] Yetersiz altın.");
                }

            }
        });
    }

    // ==================== API ====================

    public void Show()
    {
        if (_isOpen) return;
        _isOpen = true;
        _panelRoot.transform.SetAsLastSibling(); // Menüyü en öne al (joystick ve diğer arayüzlerin üstüne)
        _panelController.Show();
        BaseWorldUI.Instance?.OnBuildMenuOpened();

        BaseWorldUI.Instance?.ToggleBottomButtons(false);

        RefreshButtons();

    }

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;

        _panelController.Hide();
        BaseWorldUI.Instance?.ToggleBottomButtons(true);
        BaseWorldUI.Instance?.OnBuildMenuClosed();
    }

    private void HideImmediate()
    {
        _isOpen = false;
        _panelController.HideImmediate();
        BaseWorldUI.Instance?.ToggleBottomButtons(true);
    }


    private void RefreshButtons()
    {
        bool builderAvailable = BuilderSystem.Instance != null && BuilderSystem.Instance.HasAvailableBuilder();

        foreach (var btn in _itemButtons)
        {
            var typeName = btn.name.StartsWith("Item_") ? btn.name.Substring(5) : "";
            if (!System.Enum.TryParse(typeName, out BuildingType type)) continue;
            BuildingData data = null;
            GameManager.Instance?.Buildings.TryGetValue(type, out data);
            bool unlocked = data != null && data.isUnlocked;
            btn.interactable = builderAvailable && unlocked;
            var icon = btn.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null) icon.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.5f);
            var name = btn.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (name != null) name.color = unlocked ? ReferenceUiVisuals.Light : ReferenceUiVisuals.Muted;
            var cost = btn.transform.Find("Cost")?.GetComponent<TextMeshProUGUI>();
            int placed = BaseMapManager.Instance?.GetPlacedCount(type) ?? 0;
            int limit = data != null && GameManager.Instance != null
                ? data.MaxCountAtBaseLevel(GameManager.Instance.GetMainBaseLevel()) : 0;
            var count = btn.transform.Find("PlacedCount")?.GetComponent<TextMeshProUGUI>();
            if (count != null) count.text = $"Kurulu {placed}/{limit}";
            btn.interactable = builderAvailable && unlocked && placed < limit;
            if (cost != null)
            {
                cost.text = unlocked ? (type == BuildingType.ArcherTower ? "50 Altın"
                    : type == BuildingType.CannonTower ? "80 Altın" : "60 Altın")
                    : $"Ana Üs Sev. {data?.GetRequiredMainBaseLevel() ?? 1}";
                cost.color = unlocked ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Muted;
            }
        }
        var hint = _panelRoot?.transform.Find("BuildMenuHint");
        if (hint != null && _activeTab == 0 && GameManager.Instance != null)
            hint.gameObject.SetActive(System.Array.Exists(new[] { BuildingType.ArcherTower,
                BuildingType.CannonTower, BuildingType.Monastery }, type =>
                GameManager.Instance.Buildings.TryGetValue(type, out var item) && !item.isUnlocked));
        foreach (var btn in _decorButtons)
        {
            string id = btn.name.Substring("Decor_".Length);
            var data = System.Array.Find(DecorPlacementController.All, item => item.id == id);
            if (data == null || GameManager.Instance == null) continue;
            bool unlocked = GameManager.Instance.GetMainBaseLevel() >= data.unlockMainBaseLevel;
            btn.interactable = unlocked && GameManager.Instance.Gold >= data.cost;
            var label = btn.transform.Find("Cost")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = unlocked ? $"{data.cost} ALTIN" :
                $"SEV. {data.unlockMainBaseLevel}";
            var icon = btn.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null) icon.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }
    }

    private void Update()
    {
        if (_isOpen && BuilderSystem.Instance != null)
        {
            // İnşaatçı durumu değişirse anlık güncelle
            RefreshButtons();
        }
    }
}
