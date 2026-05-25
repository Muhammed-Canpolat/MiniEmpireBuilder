using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Ekranın altından kayarak açılan inşa menüsü.
/// "Topçu Kulesi", "Okçu Kulesi", "Duvarcı", vs. gösterir.
/// </summary>
public class BuildMenuController : MonoBehaviour
{
    public static BuildMenuController Instance { get; private set; }

    private GameObject _panelRoot;
    private RectTransform _panelRt;
    private bool _isOpen;

    private List<Button> _itemButtons = new List<Button>();

    // ==================== KURULUM ====================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Initialize(Transform canvasTransform)
    {
        CreateUI(canvasTransform);
        HideImmediate();
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
        // Topçu Kulesi, Okçu Kulesi, Duvarcı, Ana Üs, Altın Madeni
        // Gerçekte Ana Üs ve Altın Madeni ilk başta sahneye yerleşmeli, ancak menüden de yerleştirilebilir.
        // Maliyetleri BuildingData GetUpgradeCost üzerinden ilk seviye için hesaplayabiliriz. Veya sabit tutabiliriz.

        CreateItem(BuildingType.ArcherTower, 0.05f, 0.35f, 50);
        CreateItem(BuildingType.CannonTower, 0.38f, 0.68f, 80);
        CreateItem(BuildingType.WallBuilder, 0.71f, 1.01f, 45); // Biraz sağa taşıyor, layoutGroup kullanmak daha iyi olabilirdi ama hızlıca yerleştiriyorum
    }

    private void CreateItem(BuildingType type, float minX, float maxX, int cost)
    {
        GameObject itemGO = new GameObject($"Item_{type}");
        itemGO.transform.SetParent(_panelRoot.transform, false);
        RectTransform rt = itemGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(minX, 0.1f);
        rt.anchorMax = new Vector2(maxX, 0.8f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image bg = itemGO.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f);

        Button btn = itemGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        _itemButtons.Add(btn);

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
        _panelRoot.SetActive(true);
        _panelRoot.transform.SetAsLastSibling(); // Menüyü en öne al (joystick ve diğer arayüzlerin üstüne)

        _panelRt.DOKill();
        _panelRt.anchoredPosition = new Vector2(0f, -_panelRt.rect.height);
        _panelRt.DOAnchorPosY(0f, 0.35f).SetEase(Ease.OutCubic);

        BaseWorldUI.Instance?.ToggleBottomButtons(false);

        RefreshButtons();

    }

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;

        _panelRt.DOKill();
        _panelRt.DOAnchorPosY(-_panelRt.rect.height, 0.3f).SetEase(Ease.InCubic)
            .OnComplete(() => {
                _panelRoot.SetActive(false);
                BaseWorldUI.Instance?.ToggleBottomButtons(true);
            });
    }

    private void HideImmediate()
    {
        _isOpen = false;
        _panelRoot.SetActive(false);
        BaseWorldUI.Instance?.ToggleBottomButtons(true);
    }


    private void RefreshButtons()
    {
        bool builderAvailable = BuilderSystem.Instance != null && BuilderSystem.Instance.HasAvailableBuilder();

        foreach (var btn in _itemButtons)
        {
            btn.interactable = builderAvailable;
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
