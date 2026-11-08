using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CampaignSceneSetup : MonoBehaviour
{
    private RectTransform safeArea;
    private RectTransform mapRoot;
    private RectTransform regionRoot;
    private RegionData selectedRegion;
    private int selectedLevel;
    private RectTransform roadViewport;
    private RectTransform roadContent;
    private ScrollRect roadScroll;
    private RectTransform detailPanel;
    private bool detailOpen;
    private readonly Dictionary<int, RectTransform> levelNodes = new Dictionary<int, RectTransform>();
    private TextMeshProUGUI toast;
    private Image toastBackground;
    private CanvasGroup toastGroup;
    private Coroutine toastRoutine;

    private static readonly Color Locked = ReferenceUiVisuals.Hex(0x4A3A26);
    private static readonly Color Purple = ReferenceUiVisuals.Hex(0x7A6AA8);
    private static Sprite starSprite;

    private void Update()
    {
        if (regionRoot != null && regionRoot.gameObject.activeInHierarchy && detailOpen
            && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseDetail();
    }

    private void Start()
    {
        if (EventSystem.current == null)
        {
            var input = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.transform.SetParent(transform);
        }
        var canvasObject = new GameObject("CampaignCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safeArea = ReferenceUiVisuals.SafeArea(canvas.transform);
        Box(safeArea, "Backdrop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, ReferenceUiVisuals.Background);
        var texture = Resources.Load<Sprite>("Sprites/ground_battle_tile_01");
        if (texture != null)
        {
            var bg = Box(safeArea, "GroundTexture", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.45f, 0.37f, 0.27f, 0.12f));
            bg.sprite = texture;
            bg.type = Image.Type.Simple;
        }
        mapRoot = ReferenceUiVisuals.Rect(safeArea, "CampaignMap", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        regionRoot = ReferenceUiVisuals.Rect(safeArea, "RegionScreen", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        toastBackground = Box(safeArea, "ToastPill", new Vector2(0.08f, 1), new Vector2(0.92f, 1),
            new Vector2(0, -238), new Vector2(0, -190), Color.white);
        toastBackground.sprite = ReferenceUiVisuals.ToastSprite();
        toastBackground.type = Image.Type.Sliced;
        toastGroup = toastBackground.gameObject.AddComponent<CanvasGroup>();
        toast = Label(toastBackground.transform, "Text", "", 14, ReferenceUiVisuals.Gold, false,
            Vector2.zero, Vector2.one, new Vector2(12, 2), new Vector2(-12, -2));
        toastBackground.gameObject.AddComponent<PanelController>().Initialize(false);
        ShowMap();
        if (BattleSession.IsCampaign)
        {
            var lastRegion = CampaignCatalog.Get(BattleSession.RegionId);
            if (lastRegion != null) ShowRegion(lastRegion);
        }
    }

    private static Image Box(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high, Color color)
        => ReferenceUiVisuals.Image(parent, name, min, max, low, high, color);

    private static TextMeshProUGUI Label(Transform parent, string name, string value, float size,
        Color color, bool title, Vector2 min, Vector2 max, Vector2 low, Vector2 high,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        => ReferenceUiVisuals.Text(parent, name, value, size, color, alignment, min, max, low, high, title, !title);

    private static RectTransform TopRect(Transform parent, string name, float x, float top, float width, float height)
        => ReferenceUiVisuals.Rect(parent, name, Vector2.up, Vector2.up,
            new Vector2(x, -top - height), new Vector2(x + width, -top));

    private static Image TopBox(Transform parent, string name, float x, float top, float width, float height, Color color)
    {
        var rect = TopRect(parent, name, x, top, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI TopLabel(Transform parent, string name, string value, float x, float top,
        float width, float height, float size, Color color, bool title = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rect = TopRect(parent, name, x, top, width, height);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(10, size - 3);
        label.fontSizeMax = size;
        ReferenceUiVisuals.ApplyFont(label, size, color, title, !title);
        return label;
    }

    private static Button TopButton(Transform parent, string name, string value, float x, float top,
        float width, float height, bool gold, float fontSize)
    {
        var rect = TopRect(parent, name, x, top, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        ReferenceUiVisuals.ApplyButtonSkin(button, gold);
        Label(rect, "Label", value, fontSize, gold ? ReferenceUiVisuals.Panel : ReferenceUiVisuals.Light,
            true, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
        return button;
    }

    private static void Clear(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
    }

    private static void Border(Image image, Color color, float width)
    {
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
    }

    private static void Dot(Transform parent, float x, float y, float size, Color color)
    {
        var image = TopBox(parent, "RouteDot", x - size / 2f, y - size / 2f, size, size, color);
        image.sprite = BaseMapManager.MakeCircleSprite(Color.white);
    }

    public static Sprite GetStarSprite()
    {
        if (starSprite == null)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            var polygon = new Vector2[10];
            for (int i = 0; i < polygon.Length; i++)
            {
                float angle = (-90f + i * 36f) * Mathf.Deg2Rad;
                float radius = i % 2 == 0 ? 14f : 6.5f;
                polygon[i] = new Vector2(15.5f + Mathf.Cos(angle) * radius,
                    15.5f + Mathf.Sin(angle) * radius);
            }
            for (int py = 0; py < 32; py++)
            for (int px = 0; px < 32; px++)
            {
                bool inside = false;
                for (int i = 0, j = 9; i < 10; j = i++)
                {
                    var a = polygon[i]; var b = polygon[j];
                    if ((a.y > py) != (b.y > py)
                        && px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
                texture.SetPixel(px, py, inside ? Color.white : Color.clear);
            }
            texture.Apply();
            starSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }
        return starSprite;
    }

    private static void Star(Transform parent, float x, float top, float size, Color color)
    {
        var icon = TopBox(parent, "Star", x, top, size, size, color);
        icon.sprite = GetStarSprite();
    }

    private void ShowMap()
    {
        selectedRegion = null;
        regionRoot.gameObject.SetActive(false);
        mapRoot.gameObject.SetActive(true);
        Clear(mapRoot);

        TopLabel(mapRoot, "MapTitle", "SEFER HARİTASI", 15, 34, 240, 42, 24, ReferenceUiVisuals.Gold, true,
            TextAlignmentOptions.Left);
        var total = CampaignCatalog.Regions.Length * 15;
        var starsChip = TopButton(mapRoot, "StarsChip", $"{CampaignProgress.TotalStars()}/{total}",
            293, 39, 82, 30, false, 14);
        starsChip.interactable = false;
        var starsText = starsChip.transform.Find("Label") as RectTransform;
        if (starsText != null) starsText.offsetMin = new Vector2(13, 0);
        Star(starsChip.transform, 7, 8, 14, ReferenceUiVisuals.Gold);

        for (float y = 145; y <= 614; y += 13)
            Dot(mapRoot, 42, y, 3, ReferenceUiVisuals.Border);

        var endless = TopButton(mapRoot, "EndlessCard", "", 70, 96, 303, 72, false, 14);
        Border(endless.GetComponent<Image>(), Purple, 1.6f);
        endless.onClick.AddListener(() => ShowToast(CampaignProgress.Stars(5, 5) > 0
            ? "Sonsuz Sefer sonraki aşamada açılacak."
            : "Bölge 5 bossunu yenerek açılır."));
        var infinity = TopBox(mapRoot, "InfinityNode", 16, 108, 52, 52, ReferenceUiVisuals.Panel);
        infinity.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        Border(infinity, Purple, 2f);
        TopLabel(mapRoot, "Infinity", "∞", 20, 110, 44, 44, 25, Purple, true);
        TopLabel(mapRoot, "EndlessName", "SONSUZ SEFER", 82, 105, 268, 24, 16, Purple, true,
            TextAlignmentOptions.Left);
        TopLabel(mapRoot, "EndlessDetail", "Bölge 5 bossunu yen. Sınırsız seviye, artan zorluk.",
            82, 132, 267, 27, 13, ReferenceUiVisuals.Muted, false, TextAlignmentOptions.Left);

        for (int id = 5; id >= 1; id--)
        {
            var region = CampaignCatalog.Get(id);
            if (region == null) continue;
            int top = 177 + (5 - id) * 88;
            CreateRegionCard(region, top);
        }

        var back = ReferenceUiVisuals.Button(mapRoot, "ReturnToBase", "ÜSSE DÖN", false,
            new Vector2(0.04f, 0), new Vector2(0.96f, 0), new Vector2(0, 22), new Vector2(0, 78), 18);
        back.onClick.AddListener(() => GameManager.Instance.LoadBaseWorld());
    }

    private void CreateRegionCard(RegionData region, int top)
    {
        bool open = CampaignProgress.IsRegionOpen(region);
        bool complete = CampaignProgress.Stars(region.id, 5) > 0;
        int stars = CampaignProgress.RegionStars(region.id);
        var card = TopButton(mapRoot, "Region_" + region.id, "", 70, top, 303, 80, false, 14);
        Border(card.GetComponent<Image>(), open ? ReferenceUiVisuals.Gold : Locked, open ? 1.5f : 1f);
        var group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = open ? 1f : 0.55f;
        card.onClick.AddListener(() =>
        {
            if (open) ShowRegion(region);
            else ShowToast(GameManager.Instance.GetMainBaseLevel() < region.requiredBaseLevel
                ? $"Ana Üs Sev. {region.requiredBaseLevel} gerekir."
                : $"Önce Bölge {region.id - 1} bossunu yen.");
        });
        var thumb = TopBox(card.transform, "RegionTexture", 7, 7, 66, 66, Color.white);
        thumb.sprite = region.groundTile ?? Resources.Load<Sprite>("Sprites/ground_battle_tile_01");
        thumb.color = Color.Lerp(Color.white, region.colorTone, 0.8f);
        TopLabel(card.transform, "Name", UiStrings.Upper(region.displayName), 85, 12, 206, 27, 15,
            open ? ReferenceUiVisuals.Light : ReferenceUiVisuals.Muted, true, TextAlignmentOptions.Left);
        string status = complete ? $"TAMAMLANDI · {stars}/15 yıldız"
            : open ? $"AÇIK · {stars}/15 yıldız · Seviye {CampaignProgress.FirstIncomplete(region)}"
            : $"Kilitli · Ana Üs Sev. {region.requiredBaseLevel} gerekir";
        TopLabel(card.transform, "Status", status, 85, 42, 210, 24, 13,
            open ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Muted, false, TextAlignmentOptions.Left);

        var node = TopBox(mapRoot, "RegionNode_" + region.id, 16, top + 14, 52, 52,
            open ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Panel);
        node.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        Border(node, open ? ReferenceUiVisuals.Gold : Locked, 2f);
        TopLabel(mapRoot, "Number", region.id.ToString(), 20, top + 17, 44, 44, 18,
            open ? ReferenceUiVisuals.Panel : ReferenceUiVisuals.Muted, true);
    }

    private void ShowRegion(RegionData region)
    {
        selectedRegion = region;
        selectedLevel = 0;
        detailOpen = false;
        RenderRegion();
    }

    private void RenderRegion()
    {
        if (selectedRegion == null) return;
        mapRoot.gameObject.SetActive(false);
        regionRoot.gameObject.SetActive(true);
        Clear(regionRoot);
        levelNodes.Clear();

        var outside = regionRoot.gameObject.GetComponent<Image>();
        if (outside == null) outside = regionRoot.gameObject.AddComponent<Image>();
        outside.color = Color.clear;
        outside.raycastTarget = true;
        var outsideButton = regionRoot.gameObject.GetComponent<Button>();
        if (outsideButton == null) outsideButton = regionRoot.gameObject.AddComponent<Button>();
        outsideButton.transition = Selectable.Transition.None;
        outsideButton.onClick.RemoveAllListeners();
        outsideButton.onClick.AddListener(() => { if (detailOpen) CloseDetail(); });

        var back = TopButton(regionRoot, "BackToMap", "‹ Harita", 14, 36, 87, 32, false, 14);
        back.onClick.AddListener(ShowMap);
        TopLabel(regionRoot, "RegionTitle", UiStrings.Upper(selectedRegion.displayName),
            109, 35, 264, 36, 22, ReferenceUiVisuals.Gold, true, TextAlignmentOptions.Left);

        var banner = TopBox(regionRoot, "RegionBanner", 14, 88, 362, 94,
            Color.Lerp(selectedRegion.colorTone, ReferenceUiVisuals.Panel, 0.55f));
        Border(banner, ReferenceUiVisuals.Border, 1f);
        TopLabel(banner.transform, "Description", selectedRegion.description, 12, 8, 338, 54, 14,
            ReferenceUiVisuals.Light, false, TextAlignmentOptions.Left);
        TopLabel(banner.transform, "Enemies", $"Düşmanlar: Kurt, Zombi, Ork  ·  Boss: {selectedRegion.bossName}",
            12, 64, 338, 25, 12, ReferenceUiVisuals.Gold, false, TextAlignmentOptions.Left);

        roadViewport = ReferenceUiVisuals.Rect(regionRoot, "RoadViewport", Vector2.zero, Vector2.one,
            new Vector2(14, 78), new Vector2(-14, -190));
        var roadImage = roadViewport.gameObject.AddComponent<Image>();
        roadImage.color = Color.clear;
        roadViewport.gameObject.AddComponent<RectMask2D>();
        roadContent = ReferenceUiVisuals.Rect(roadViewport, "RoadContent", new Vector2(0, 1),
            Vector2.one, new Vector2(0, -540), Vector2.zero);
        roadScroll = roadViewport.gameObject.AddComponent<ScrollRect>();
        roadScroll.viewport = roadViewport;
        roadScroll.content = roadContent;
        roadScroll.horizontal = false;
        roadScroll.vertical = true;
        roadScroll.movementType = ScrollRect.MovementType.Clamped;
        roadScroll.scrollSensitivity = 20;

        for (int level = 1; level < 5; level++)
        {
            int y1 = 485 - (level - 1) * 105;
            int y2 = y1 - 105;
            int x1 = level % 2 == 1 ? 120 : 255;
            int x2 = level % 2 == 1 ? 255 : 120;
            for (int dot = 1; dot <= 8; dot++)
            {
                float t = dot / 9f;
                Dot(roadContent, Mathf.Lerp(x1, x2, t), Mathf.Lerp(y1, y2, t), 3, ReferenceUiVisuals.Border);
            }
        }
        for (int level = 5; level >= 1; level--)
            CreateLevelNode(level);
        detailPanel = Box(regionRoot, "LevelDetail", new Vector2(0.04f, 0), new Vector2(0.96f, 0),
            new Vector2(0, 22), new Vector2(0, 232), ReferenceUiVisuals.Panel).rectTransform;
        Border(detailPanel.GetComponent<Image>(), ReferenceUiVisuals.Gold, 1.5f);
        detailPanel.anchoredPosition = new Vector2(detailPanel.anchoredPosition.x, 127);
        detailPanel.gameObject.AddComponent<PanelController>().Initialize();
        UpdateNodeSelection();
    }

    private void CreateLevelNode(int level)
    {
        bool open = CampaignProgress.IsLevelOpen(selectedRegion, level);
        int stars = CampaignProgress.Stars(selectedRegion.id, level);
        int x = level % 2 == 1 ? 120 : 255;
        int y = 485 - (level - 1) * 105;
        float size = level == 5 ? 62 : 52;
        var node = TopBox(roadContent, "LevelNode_" + level, x - size / 2, y - size / 2, size, size,
            stars > 0 ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Panel);
        node.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        Border(node, level == 5 ? ReferenceUiVisuals.Red : open ? ReferenceUiVisuals.Gold : Locked, 2f);
        levelNodes[level] = node.rectTransform;
        node.raycastTarget = true;
        var button = node.gameObject.AddComponent<Button>();
        button.targetGraphic = node;
        button.onClick.AddListener(() =>
        {
            if (!open) ShowToast($"Önce {level - 1}. seviyeyi tamamla");
            else SelectLevel(level);
        });
        TopLabel(node.transform, "NodeText", stars > 0 ? "" : level == 5 ? "B" : level.ToString(),
            3, 4, size - 6, size - 8, 21,
            stars > 0 ? ReferenceUiVisuals.Panel : open ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Muted, true);
        if (stars > 0) Star(node.transform, size * 0.28f, size * 0.28f,
            size * 0.44f, ReferenceUiVisuals.Panel);
        var hint = TopLabel(node.transform, "ActiveHint", "▼", size * 0.5f - 10, -21,
            20, 18, 13, ReferenceUiVisuals.Gold, true);
        hint.gameObject.SetActive(false);
        string caption = level == 5 ? "5 · BOSS · " + selectedRegion.bossName
            : level + " · " + CampaignCatalog.KindName(selectedRegion.levels[level - 1].kind);
        int labelX = x < 190 ? 160 : 28;
        TopLabel(roadContent, "NodeCaption_" + level, caption, labelX, y - 16, 200, 34,
            12, open ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Muted, false,
            x < 190 ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
    }

    private void UpdateNodeSelection()
    {
        int active = selectedRegion == null ? 0 : CampaignProgress.FirstIncomplete(selectedRegion);
        foreach (var pair in levelNodes)
        {
            var node = pair.Value;
            if (node == null) continue;
            node.DOKill();
            node.localScale = Vector3.one * (detailOpen && selectedLevel == pair.Key ? 1.1f : 1f);
            var hint = node.Find("ActiveHint");
            if (hint != null) hint.gameObject.SetActive(!detailOpen && pair.Key == active);
            if (!detailOpen && pair.Key == active)
                node.DOScale(1.08f, 1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetLink(node.gameObject);
        }
    }

    private void SelectLevel(int level)
    {
        selectedLevel = level;
        if (!detailOpen)
        {
            detailOpen = true;
            detailPanel.GetComponent<PanelController>().Show();
            roadViewport.offsetMin = new Vector2(14, 240);
        }
        RenderLevelDetail();
        UpdateNodeSelection();
        ScrollSelectedIntoView();
    }

    private void CloseDetail()
    {
        if (!detailOpen) return;
        detailOpen = false;
        selectedLevel = 0;
        UpdateNodeSelection();
        roadViewport.offsetMin = new Vector2(14, 78);
        detailPanel.GetComponent<PanelController>().Hide();
    }

    private void ScrollSelectedIntoView()
    {
        Canvas.ForceUpdateCanvases();
        float viewportHeight = roadViewport.rect.height;
        float scrollRange = Mathf.Max(0, roadContent.rect.height - viewportHeight);
        if (scrollRange <= 0) return;
        float nodeY = 485 - (selectedLevel - 1) * 105;
        float scroll = Mathf.Clamp(nodeY - viewportHeight + 55, 0, scrollRange);
        roadScroll.verticalNormalizedPosition = 1f - scroll / scrollRange;
        Debug.Log($"[CampaignRoad] screen={Screen.width}x{Screen.height}, root={regionRoot.rect.height:F1}, "
            + $"viewport={viewportHeight:F1}, content={roadContent.rect.height:F1}, "
            + $"scroll={scroll:F1}, normalized={roadScroll.verticalNormalizedPosition:F2}, "
            + $"anchored={roadContent.anchoredPosition}");
    }

    private void RenderLevelDetail()
    {
        Clear(detailPanel);
        var level = selectedRegion.levels[selectedLevel - 1];
        TopLabel(detailPanel, "DetailTitle", $"SEVİYE {selectedLevel} · {UiStrings.Upper(CampaignCatalog.KindName(level.kind))}",
            13, 10, 280, 36, 17, ReferenceUiVisuals.Light, true, TextAlignmentOptions.Left);
        var closeRect = TopRect(detailPanel, "CloseLevelDetail", 301, 10, 44, 44);
        var closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.color = ReferenceUiVisuals.Red;
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(CloseDetail);
        TopLabel(closeRect, "X", "X", 0, 0, 44, 44, 21, Color.white, true);
        TopLabel(detailPanel, "LevelDescription", level.description,
            13, 54, 330, 43, 14, ReferenceUiVisuals.Muted, false, TextAlignmentOptions.Left);
        TopLabel(detailPanel, "Meta", $"Düşman: {level.TotalEnemies}       Ödül: {level.reward}",
            13, 103, 255, 29, 13, ReferenceUiVisuals.Gold, false, TextAlignmentOptions.Left);
        TopLabel(detailPanel, "Modifier", string.IsNullOrEmpty(level.modifier) ? "NORMAL" : level.modifier,
            270, 103, 72, 29, 12, ReferenceUiVisuals.Muted);
        for (int i = 0; i < 3; i++)
            Star(detailPanel, 281 + i * 18, 132, 15, ReferenceUiVisuals.Gold);
        var battle = ReferenceUiVisuals.Button(detailPanel, "StartCampaignBattle", "SAVAŞA BAŞLA", true,
            Vector2.zero, Vector2.zero, new Vector2(14, 9), new Vector2(345, 61), 19);
        battle.interactable = CampaignProgress.IsLevelOpen(selectedRegion, selectedLevel);
        battle.onClick.AddListener(() =>
        {
            BattleSession.Begin(selectedRegion.id, selectedLevel);
            GameManager.Instance.StartBattle();
        });
    }

    private void ShowToast(string message)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(Toast(message));
    }

    private IEnumerator Toast(string message)
    {
        toast.text = message;
        toastBackground.GetComponent<PanelController>().Show();
        yield return new WaitForSeconds(1.25f);
        toastBackground.GetComponent<PanelController>().Hide();
        toastRoutine = null;
    }
}
