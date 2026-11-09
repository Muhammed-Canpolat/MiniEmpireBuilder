using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Sefer haritası ve bölge ekranı. Arka planlar Tiny Swords parçalarından üretilmiş resimlerdir
/// (Resources/CampaignArt, üreteç: Tools/campaign_art/generate.py). Harita aşağıdan yukarı
/// adalardan oluşur; bölge ekranında 5 aşama kıvrımlı bir yolun üstündeki meydanlardadır.
/// </summary>
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

    // Harita kaydırma alanı (adalar) ve bölge yolu: genişlik değişince yeniden boyutlanır.
    private RectTransform mapViewport;
    private RectTransform mapContent;
    private ScrollRect mapScroll;
    private readonly List<RectTransform> mapTiles = new List<RectTransform>();
    private int focusTileIndex = -1;
    private float laidOutMapWidth = -1f;
    private float laidOutRoadWidth = -1f;

    private static readonly Color Locked = ReferenceUiVisuals.Hex(0x4A3A26);
    private static readonly Color Purple = ReferenceUiVisuals.Hex(0x7A6AA8);
    private static readonly Color Water = ReferenceUiVisuals.Hex(0x47ABA9);
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static readonly Color InkMuted = ReferenceUiVisuals.Hex(0x6B5236);
    private static readonly Color InkGold = ReferenceUiVisuals.Hex(0x9A6410);
    private static Sprite starSprite;

    // Görsel boyutları (piksel) ve UI birimi: 2 piksel = 1 birim, temel genişlik 390.
    private const float MapTileAspect = 440f / 780f;
    private const float RoadAspect = 1080f / 780f;
    private const float RoadBaseWidth = 390f;
    /// <summary>Aşama meydanları (UI birimi, yol içeriğinin sol üstünden). generate.py NODES_UI ile aynı.</summary>
    public static readonly Vector2[] RoadNodes =
    {
        new Vector2(120, 470), new Vector2(262, 372), new Vector2(118, 274),
        new Vector2(262, 176), new Vector2(190, 76)
    };

    private void Update()
    {
        if (regionRoot != null && regionRoot.gameObject.activeInHierarchy && detailOpen
            && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseDetail();
    }

    private void LateUpdate()
    {
        if (mapRoot != null && mapRoot.gameObject.activeInHierarchy && mapViewport != null)
        {
            float width = mapViewport.rect.width;
            if (width > 1f && Mathf.Abs(width - laidOutMapWidth) > 0.5f) LayoutMap(width);
        }
        if (regionRoot != null && regionRoot.gameObject.activeInHierarchy && roadViewport != null)
        {
            float width = roadViewport.rect.width;
            if (width > 1f && Mathf.Abs(width - laidOutRoadWidth) > 0.5f) LayoutRoad(width);
        }
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
        // Su rengi tüm ekranı kaplar (çentik/kenar boşlukları dahil): adalar suda yüzer.
        Box(canvas.transform, "Water", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Water);
        safeArea = ReferenceUiVisuals.SafeArea(canvas.transform);
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

    // ==================== GÖRSEL YARDIMCILARI ====================

    private static readonly Dictionary<string, Sprite> artCache = new Dictionary<string, Sprite>();

    /// <summary>Resources/CampaignArt altındaki görsel; border verilirse 9 dilimli kopya üretilir.</summary>
    private static Sprite Art(string name, float border = 0f)
    {
        string key = name + "#" + border;
        if (artCache.TryGetValue(key, out var cached) && cached != null) return cached;
        var sprite = Resources.Load<Sprite>("CampaignArt/" + name);
        if (sprite != null && border > 0f)
            sprite = Sprite.Create(sprite.texture, sprite.rect, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        artCache[key] = sprite;
        return sprite;
    }

    private static Image ArtImage(Transform parent, string name, string art, Vector2 min, Vector2 max,
        Vector2 low, Vector2 high, float border = 0f, bool preserve = false)
    {
        var image = Box(parent, name, min, max, low, high, Color.white);
        image.sprite = Art(art, border);
        if (image.sprite == null) image.color = new Color(0, 0, 0, 0);
        image.type = border > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = preserve;
        return image;
    }

    /// <summary>Merkezi (anchor noktası) etrafında verilen boyda resim.</summary>
    private static Image ArtAt(Transform parent, string name, string art, Vector2 anchor, Vector2 offset,
        Vector2 size, bool preserve = true)
        => ArtImage(parent, name, art, anchor, anchor, offset - size * 0.5f, offset + size * 0.5f, 0f, preserve);

    private static TextMeshProUGUI LabelAt(Transform parent, string name, string value, Vector2 anchor,
        Vector2 offset, Vector2 size, float fontSize, Color color, bool title)
    {
        var label = ReferenceUiVisuals.Text(parent, name, value, fontSize, color, TextAlignmentOptions.Center,
            anchor, anchor, offset - size * 0.5f, offset + size * 0.5f, title, !title);
        return label;
    }

    private static void Shadowed(TextMeshProUGUI label, float distance = 1.4f)
    {
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.05f, 0.02f, 0.85f);
        shadow.effectDistance = new Vector2(distance, -distance);
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
        rect.gameObject.AddComponent<Image>();
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

    public static readonly Color EmptyStar = ReferenceUiVisuals.Hex(0x4A3C28);

    /// <summary>3 yıldız yuvası (ortadaki biraz yukarıda): ilk <paramref name="earned"/> tanesi altın.</summary>
    private static RectTransform StarRow(Transform parent, string name, Vector2 anchor, Vector2 offset, float size,
        float step, int earned)
    {
        var row = ReferenceUiVisuals.Rect(parent, name, anchor, anchor,
            offset - new Vector2(step + size * 0.5f, size * 0.5f), offset + new Vector2(step + size * 0.5f, size * 0.5f));
        for (int i = 0; i < 3; i++)
        {
            var star = Box(row, "Star" + (i + 1), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2((i - 1) * step - size * 0.5f, (i == 1 ? 3f : 0f) - size * 0.5f),
                new Vector2((i - 1) * step + size * 0.5f, (i == 1 ? 3f : 0f) + size * 0.5f),
                i < earned ? ReferenceUiVisuals.Gold : EmptyStar);
            star.sprite = GetStarSprite();
            var outline = star.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.06f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }
        return row;
    }

    /// <summary>Üst şerit: koyu ahşap, altında altın çizgi; başlığı haritanın üstünde okunur kılar.</summary>
    private static void HeaderStrip(Transform parent, float height)
    {
        var strip = Box(parent, "HeaderStrip", new Vector2(0, 1), Vector2.one, new Vector2(0, -height), Vector2.zero,
            new Color(0.13f, 0.09f, 0.06f, 0.93f));
        strip.raycastTarget = true;
        Box(strip.transform, "HeaderEdge", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2.5f),
            ReferenceUiVisuals.Hex(0xB98A3A));
    }

    private static void FooterStrip(Transform parent, float height)
    {
        var strip = Box(parent, "FooterStrip", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, height),
            new Color(0.13f, 0.09f, 0.06f, 0.93f));
        strip.raycastTarget = true;
        Box(strip.transform, "FooterEdge", new Vector2(0, 1), Vector2.one, new Vector2(0, -2.5f), Vector2.zero,
            ReferenceUiVisuals.Hex(0xB98A3A));
    }

    // ==================== SEFER HARİTASI ====================

    private const float MapHeader = 84f, MapFooter = 96f;

    /// <summary>Kahramanın durduğu bölge: bossu henüz yenilmemiş en yüksek açık bölge.</summary>
    private static int CurrentRegionId()
    {
        int current = 1;
        foreach (var region in CampaignCatalog.Regions)
            if (CampaignProgress.IsRegionOpen(region)) current = region.id;
        var last = CampaignCatalog.Get(current);
        return last != null ? current : 1;
    }

    private void ShowMap()
    {
        selectedRegion = null;
        regionRoot.gameObject.SetActive(false);
        mapRoot.gameObject.SetActive(true);
        Clear(mapRoot);
        mapTiles.Clear();
        laidOutMapWidth = -1f;

        mapViewport = ReferenceUiVisuals.Rect(mapRoot, "MapViewport", Vector2.zero, Vector2.one,
            new Vector2(0, MapFooter - 6), new Vector2(0, -MapHeader + 6));
        var viewportImage = mapViewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0);
        mapViewport.gameObject.AddComponent<RectMask2D>();
        mapContent = ReferenceUiVisuals.Rect(mapViewport, "MapContent", new Vector2(0, 1), Vector2.one,
            new Vector2(0, -1320), Vector2.zero);
        mapScroll = mapViewport.gameObject.AddComponent<ScrollRect>();
        mapScroll.viewport = mapViewport;
        mapScroll.content = mapContent;
        mapScroll.horizontal = false;
        mapScroll.vertical = true;
        mapScroll.movementType = ScrollRect.MovementType.Elastic;
        mapScroll.scrollSensitivity = 24;

        // Yukarıdan aşağı: Sonsuz Sefer, Bölge 5 ... Bölge 1 (yolculuk aşağıdan yukarı).
        int current = CurrentRegionId();
        focusTileIndex = 0;
        mapTiles.Add(CreateEndlessTile());
        for (int id = 5; id >= 1; id--)
        {
            var region = CampaignCatalog.Get(id);
            if (region == null) continue;
            if (id == current) focusTileIndex = mapTiles.Count;
            mapTiles.Add(CreateRegionTile(region, id == current));
        }

        HeaderStrip(mapRoot, MapHeader);
        TopLabel(mapRoot, "MapTitle", "SEFER HARİTASI", 16, 24, 240, 42, 24, ReferenceUiVisuals.Gold, true,
            TextAlignmentOptions.Left);
        int total = CampaignCatalog.Regions.Length * 15;
        var chip = ArtImage(mapRoot, "StarsChip", "ui_paper", Vector2.one, Vector2.one,
            new Vector2(-104, -66), new Vector2(-14, -30), 18f);
        Box(chip.transform, "Star", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(9, -9), new Vector2(27, 9),
            ReferenceUiVisuals.Gold).sprite = GetStarSprite();
        Label(chip.transform, "Count", $"{CampaignProgress.TotalStars()}/{total}", 15, Ink, true,
            Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-6, 0));

        FooterStrip(mapRoot, MapFooter);
        var back = ReferenceUiVisuals.Button(mapRoot, "ReturnToBase", "ÜSSE DÖN", false,
            new Vector2(0.06f, 0), new Vector2(0.94f, 0), new Vector2(0, 20), new Vector2(0, 74), 18);
        back.onClick.AddListener(() => GameManager.Instance.LoadBaseWorld());
    }

    private void LayoutMap(float width)
    {
        laidOutMapWidth = width;
        float tileHeight = width * MapTileAspect;
        for (int i = 0; i < mapTiles.Count; i++)
        {
            var tile = mapTiles[i];
            if (tile == null) continue;
            tile.anchorMin = new Vector2(0, 1);
            tile.anchorMax = Vector2.one;
            tile.offsetMin = new Vector2(0, -(i + 1) * tileHeight);
            tile.offsetMax = new Vector2(0, -i * tileHeight);
        }
        float contentHeight = tileHeight * mapTiles.Count;
        mapContent.offsetMin = new Vector2(0, -contentHeight);
        mapContent.offsetMax = Vector2.zero;
        // Şu anki bölgeyi ekranın ortasına getir.
        float viewport = mapViewport.rect.height;
        float range = Mathf.Max(0f, contentHeight - viewport);
        if (range > 0f)
        {
            float center = (focusTileIndex + 0.5f) * tileHeight;
            float scroll = Mathf.Clamp(center - viewport * 0.5f, 0f, range);
            mapScroll.verticalNormalizedPosition = 1f - scroll / range;
        }
    }

    private RectTransform CreateRegionTile(RegionData region, bool current)
    {
        bool open = CampaignProgress.IsRegionOpen(region);
        bool complete = CampaignProgress.Stars(region.id, 5) > 0;
        int stars = CampaignProgress.RegionStars(region.id);

        var tileImage = ArtImage(mapContent, "Region_" + region.id, "map_region_" + region.id,
            new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
        tileImage.raycastTarget = true;
        // Kilitli ada solgun ve koyu: açık olanlar parlak kalır.
        tileImage.color = open ? Color.white : new Color(0.55f, 0.6f, 0.66f, 1f);
        var tile = tileImage.rectTransform;
        var button = tile.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            if (open) ShowRegion(region);
            else ShowToast(GameManager.Instance.GetMainBaseLevel() < region.requiredBaseLevel
                ? $"Ana Üs Sev. {region.requiredBaseLevel} gerekir."
                : $"Önce Bölge {region.id - 1} bossunu yen.");
        });

        // Bölge adı şeridi (adanın üst kenarında, köprünün üstünde tabela gibi).
        string ribbon = !open ? "ui_ribbon_dark" : complete ? "ui_ribbon_blue" : "ui_ribbon_gold";
        var top = new Vector2(0.5f, 1f);
        ArtAt(tile, "Ribbon", ribbon, top, new Vector2(0, -26), new Vector2(214, 56), false);
        bool goldRibbon = ribbon == "ui_ribbon_gold";
        var name = LabelAt(tile, "Name", $"{region.id} · {UiStrings.Upper(region.displayName)}", top,
            new Vector2(0, -24), new Vector2(146, 24), 14,
            goldRibbon ? Ink : open ? ReferenceUiVisuals.Light : ReferenceUiVisuals.Muted, true);
        if (!goldRibbon) Shadowed(name);

        // Durum fişi: yıldızlar ya da kilit şartı.
        var chip = ArtImage(tile, "StatusChip", "ui_paper", top, top, new Vector2(-68, -80), new Vector2(68, -56), 16f);
        if (open)
        {
            Box(chip.transform, "Star", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, -8), new Vector2(26, 8),
                ReferenceUiVisuals.Gold).sprite = GetStarSprite();
            Label(chip.transform, "Status", complete ? $"{stars}/15 · TAMAM" : $"{stars}/15 · Seviye {CampaignProgress.FirstIncomplete(region)}",
                12, Ink, false, Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-8, 0));
        }
        else
        {
            ArtAt(chip.transform, "Lock", "ui_icon_lock", new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(18, 18));
            bool baseLocked = GameManager.Instance != null && GameManager.Instance.GetMainBaseLevel() < region.requiredBaseLevel;
            Label(chip.transform, "Status", baseLocked ? $"Ana Üs Sev. {region.requiredBaseLevel}" : $"Bölge {region.id - 1} bossu",
                12, InkMuted, false, Vector2.zero, Vector2.one, new Vector2(30, 0), new Vector2(-8, 0));
            ArtAt(tile, "BigLock", "ui_icon_lock", new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(46, 46));
        }

        if (current && open)
        {
            // Kahraman bu adada: yolun ortasında zıplar.
            var hero = ArtAt(tile, "HeroMarker", "ui_hero_marker", new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(46, 52));
            hero.rectTransform.DOAnchorPosY(hero.rectTransform.anchoredPosition.y + 6f, 0.6f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(hero.gameObject);
            var you = LabelAt(tile, "HereLabel", "BURADASIN", new Vector2(0.5f, 0.42f), new Vector2(0, -34),
                new Vector2(110, 18), 11, ReferenceUiVisuals.Light, true);
            Shadowed(you);
        }
        return tile;
    }

    private RectTransform CreateEndlessTile()
    {
        bool unlocked = CampaignProgress.Stars(5, 5) > 0;
        var tileImage = ArtImage(mapContent, "EndlessCard", "map_endless", new Vector2(0, 1), Vector2.one,
            Vector2.zero, Vector2.zero);
        tileImage.raycastTarget = true;
        tileImage.color = unlocked ? Color.white : new Color(0.7f, 0.72f, 0.8f, 1f);
        var tile = tileImage.rectTransform;
        var button = tile.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => ShowToast(unlocked
            ? "Sonsuz Sefer sonraki aşamada açılacak."
            : "Bölge 5 bossunu yenerek açılır."));
        var top = new Vector2(0.5f, 1f);
        ArtAt(tile, "Ribbon", "ui_ribbon_purple", top, new Vector2(0, -26), new Vector2(214, 56), false);
        var name = LabelAt(tile, "EndlessName", "SONSUZ SEFER", top, new Vector2(0, -24), new Vector2(146, 24), 14,
            ReferenceUiVisuals.Light, true);
        Shadowed(name);
        var chip = ArtImage(tile, "StatusChip", "ui_paper", top, top, new Vector2(-92, -80), new Vector2(92, -56), 16f);
        Label(chip.transform, "EndlessDetail", unlocked ? "Sınırsız seviye, artan zorluk" : "Bölge 5 bossunu yen",
            12, InkMuted, false, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
        return tile;
    }

    // ==================== BÖLGE EKRANI ====================

    private void ShowRegion(RegionData region)
    {
        selectedRegion = region;
        selectedLevel = 0;
        detailOpen = false;
        RenderRegion();
    }

    private const float RegionTop = 182f;

    private void RenderRegion()
    {
        if (selectedRegion == null) return;
        mapRoot.gameObject.SetActive(false);
        regionRoot.gameObject.SetActive(true);
        Clear(regionRoot);
        levelNodes.Clear();
        laidOutRoadWidth = -1f;

        var outside = regionRoot.gameObject.GetComponent<Image>();
        if (outside == null) outside = regionRoot.gameObject.AddComponent<Image>();
        outside.color = Color.clear;
        outside.raycastTarget = true;
        var outsideButton = regionRoot.gameObject.GetComponent<Button>();
        if (outsideButton == null) outsideButton = regionRoot.gameObject.AddComponent<Button>();
        outsideButton.transition = Selectable.Transition.None;
        outsideButton.onClick.RemoveAllListeners();
        outsideButton.onClick.AddListener(() => { if (detailOpen) CloseDetail(); });

        // Yol: bölgenin resmi üstünde 5 aşama meydanı.
        roadViewport = ReferenceUiVisuals.Rect(regionRoot, "RoadViewport", Vector2.zero, Vector2.one,
            new Vector2(0, 0), new Vector2(0, -RegionTop + 8));
        var roadImage = roadViewport.gameObject.AddComponent<Image>();
        roadImage.color = Color.clear;
        roadViewport.gameObject.AddComponent<RectMask2D>();
        roadContent = ReferenceUiVisuals.Rect(roadViewport, "RoadContent", new Vector2(0, 1),
            Vector2.one, new Vector2(0, -540), Vector2.zero);
        var art = ArtImage(roadContent, "RoadArt", "road_region_" + selectedRegion.id, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        art.raycastTarget = false;
        roadScroll = roadViewport.gameObject.AddComponent<ScrollRect>();
        roadScroll.viewport = roadViewport;
        roadScroll.content = roadContent;
        roadScroll.horizontal = false;
        roadScroll.vertical = true;
        roadScroll.movementType = ScrollRect.MovementType.Clamped;
        roadScroll.scrollSensitivity = 20;
        for (int level = 5; level >= 1; level--)
            CreateLevelNode(level);

        // Üst kısım: başlık şeridi + parşömen üstünde bölge açıklaması.
        HeaderStrip(regionRoot, RegionTop);
        var back = TopButton(regionRoot, "BackToMap", "‹ Harita", 14, 30, 87, 34, false, 14);
        back.onClick.AddListener(ShowMap);
        TopLabel(regionRoot, "RegionTitle", UiStrings.Upper(selectedRegion.displayName),
            110, 28, 266, 38, 22, ReferenceUiVisuals.Gold, true, TextAlignmentOptions.Left);
        var banner = ArtImage(regionRoot, "RegionBanner", "ui_paper", new Vector2(0, 1), Vector2.one,
            new Vector2(12, -RegionTop + 10), new Vector2(-12, -76), 20f);
        Label(banner.transform, "Description", selectedRegion.description, 13, Ink, false,
            Vector2.zero, Vector2.one, new Vector2(14, 28), new Vector2(-14, -8), TextAlignmentOptions.TopLeft);
        Label(banner.transform, "Enemies", $"Düşmanlar: {EnemyList(selectedRegion)}  ·  Boss: {selectedRegion.bossName}",
            11.5f, InkGold, false, new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 6), new Vector2(-14, 28),
            TextAlignmentOptions.Left);

        detailPanel = ArtImage(regionRoot, "LevelDetail", "ui_paper", new Vector2(0.03f, 0), new Vector2(0.97f, 0),
            new Vector2(0, 16), new Vector2(0, 236), 22f).rectTransform;
        detailPanel.GetComponent<Image>().raycastTarget = true;
        detailPanel.anchoredPosition = new Vector2(detailPanel.anchoredPosition.x, 126);
        detailPanel.gameObject.AddComponent<PanelController>().Initialize();
        UpdateNodeSelection();
    }

    private void LayoutRoad(float width)
    {
        laidOutRoadWidth = width;
        float height = width * RoadAspect;
        roadContent.offsetMin = new Vector2(0, -height);
        roadContent.offsetMax = Vector2.zero;
        float scale = width / RoadBaseWidth;
        foreach (var pair in levelNodes)
        {
            var anchor = pair.Value.parent as RectTransform;
            if (anchor == null) continue;
            Vector2 p = RoadNodes[pair.Key - 1] * scale;
            anchor.anchoredPosition = new Vector2(p.x, -p.y);
        }
        // Açılışta sıradaki aşama görünsün.
        if (detailOpen) ScrollSelectedIntoView();
        else ScrollToLevel(CampaignProgress.FirstIncomplete(selectedRegion));
    }

    private static string KindIcon(CampaignLevelKind kind)
    {
        switch (kind)
        {
            case CampaignLevelKind.DefendBase: return "ui_icon_shield";
            case CampaignLevelKind.DefendMine: return "ui_icon_coin";
            case CampaignLevelKind.TimeTrial: return "ui_icon_clock";
            case CampaignLevelKind.Boss: return "ui_sword_red";
            default: return "ui_icon_sword";
        }
    }

    private void CreateLevelNode(int level)
    {
        bool open = CampaignProgress.IsLevelOpen(selectedRegion, level);
        int stars = CampaignProgress.Stars(selectedRegion.id, level);
        bool boss = level == 5;
        var kind = selectedRegion.levels[level - 1].kind;
        float size = boss ? 68f : 58f;

        // Düğüm kökü: meydanın merkezi (LayoutRoad konumlar). Görseller köke göre yerleşir.
        var anchor = ReferenceUiVisuals.Rect(roadContent, "LevelAnchor_" + level, new Vector2(0, 1), new Vector2(0, 1),
            Vector2.zero, Vector2.zero);
        anchor.anchoredPosition = new Vector2(RoadNodes[level - 1].x, -RoadNodes[level - 1].y);

        var node = ArtAt(anchor, "LevelNode_" + level, boss ? "ui_node_red" : "ui_node_blue", new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(size, size), false);
        node.raycastTarget = true;
        node.color = open ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
        levelNodes[level] = node.rectTransform;
        var button = node.gameObject.AddComponent<Button>();
        button.targetGraphic = node;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            if (!open) ShowToast($"Önce {level - 1}. seviyeyi tamamla");
            else SelectLevel(level);
        });
        var center = new Vector2(0.5f, 0.5f);
        if (open)
        {
            var icon = ArtAt(node.rectTransform, "KindIcon", KindIcon(kind), center, new Vector2(0, 2),
                boss ? new Vector2(46, 24) : new Vector2(30, 30));
            if (boss) icon.rectTransform.localEulerAngles = new Vector3(0, 0, 35f);
        }
        else ArtAt(node.rectTransform, "LockIcon", "ui_icon_lock", center, new Vector2(0, 1), new Vector2(26, 26));

        // Aşama numarası rozeti (sağ altta).
        var badge = Box(node.rectTransform, "NumberBadge", center, center,
            new Vector2(size * 0.5f - 18, -size * 0.5f - 2), new Vector2(size * 0.5f + 2, -size * 0.5f + 18),
            ReferenceUiVisuals.Hex(0x2A1C10));
        badge.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        var badgeEdge = badge.gameObject.AddComponent<Outline>();
        badgeEdge.effectColor = ReferenceUiVisuals.Gold;
        badgeEdge.effectDistance = new Vector2(1.2f, -1.2f);
        Label(badge.transform, "NodeText", boss ? "B" : level.ToString(), 12, ReferenceUiVisuals.Gold, true,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Kazanılan yıldızlar düğmenin üstünde.
        if (open) StarRow(node.rectTransform, "NodeStars", new Vector2(0.5f, 1f), new Vector2(0, 12), 17f, 19f, stars);

        // Altında tür adı: koyu, okunur bir pul.
        string caption = boss ? "BOSS · " + selectedRegion.bossName : CampaignCatalog.KindName(kind);
        var captionBox = Box(anchor, "NodeCaption_" + level, center, center,
            new Vector2(-58, -size * 0.5f - 24), new Vector2(58, -size * 0.5f - 6), new Color(0.12f, 0.08f, 0.05f, 0.72f));
        captionBox.sprite = ReferenceUiVisuals.PillSprite();
        captionBox.type = Image.Type.Sliced;
        Label(captionBox.transform, "Text", caption, 11.5f, open ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Muted,
            false, Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));
        if (boss)
        {
            // Boss adı uzun olabilir: pul biraz daha geniş.
            captionBox.rectTransform.offsetMin = new Vector2(-80, captionBox.rectTransform.offsetMin.y);
            captionBox.rectTransform.offsetMax = new Vector2(80, captionBox.rectTransform.offsetMax.y);
        }

        var hint = LabelAt(node.rectTransform, "ActiveHint", "▼", new Vector2(0.5f, 1f), new Vector2(0, 34),
            new Vector2(20, 18), 13, ReferenceUiVisuals.Gold, true);
        hint.gameObject.SetActive(false);

        // Sıradaki aşamada kahraman bekler.
        if (open && level == CampaignProgress.FirstIncomplete(selectedRegion) && stars == 0)
        {
            var hero = ArtAt(anchor, "HeroMarker", "ui_hero_marker", center,
                new Vector2(RoadNodes[level - 1].x < 190 ? 52 : -52, 8), new Vector2(42, 48));
            if (RoadNodes[level - 1].x >= 190) hero.rectTransform.localScale = new Vector3(-1, 1, 1);
            hero.rectTransform.DOAnchorPosY(hero.rectTransform.anchoredPosition.y + 5f, 0.55f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(hero.gameObject);
        }
    }

    private void UpdateNodeSelection()
    {
        int active = selectedRegion == null ? 0 : CampaignProgress.FirstIncomplete(selectedRegion);
        foreach (var pair in levelNodes)
        {
            var node = pair.Value;
            if (node == null) continue;
            node.DOKill();
            node.localScale = Vector3.one * (detailOpen && selectedLevel == pair.Key ? 1.12f : 1f);
            var hint = node.Find("ActiveHint");
            if (hint != null) hint.gameObject.SetActive(!detailOpen && pair.Key == active);
            if (!detailOpen && pair.Key == active)
                node.DOScale(1.08f, 0.9f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
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
            roadViewport.offsetMin = new Vector2(0, 244);
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
        roadViewport.offsetMin = Vector2.zero;
        detailPanel.GetComponent<PanelController>().Hide();
    }

    private void ScrollSelectedIntoView() => ScrollToLevel(selectedLevel);

    private void ScrollToLevel(int level)
    {
        if (level < 1 || level > 5 || roadViewport == null) return;
        Canvas.ForceUpdateCanvases();
        float viewportHeight = roadViewport.rect.height;
        float scrollRange = Mathf.Max(0, roadContent.rect.height - viewportHeight);
        if (scrollRange <= 0) return;
        float scale = roadViewport.rect.width / RoadBaseWidth;
        float nodeY = RoadNodes[level - 1].y * scale;
        float scroll = Mathf.Clamp(nodeY - viewportHeight * 0.5f, 0, scrollRange);
        roadScroll.verticalNormalizedPosition = 1f - scroll / scrollRange;
    }

    private void RenderLevelDetail()
    {
        Clear(detailPanel);
        var level = selectedRegion.levels[selectedLevel - 1];
        TopLabel(detailPanel, "DetailTitle", $"SEVİYE {selectedLevel} · {UiStrings.Upper(CampaignCatalog.KindName(level.kind))}",
            46, 14, 250, 32, 17, Ink, true, TextAlignmentOptions.Left);
        ArtImage(detailPanel, "KindIcon", KindIcon(level.kind), Vector2.up, Vector2.up,
            new Vector2(16, -42), new Vector2(40, -18), 0f, true).raycastTarget = false;
        var closeRect = TopRect(detailPanel, "CloseLevelDetail", 306, 10, 40, 40);
        var closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.sprite = Art("ui_node_red");
        closeImage.color = closeImage.sprite != null ? Color.white : ReferenceUiVisuals.Red;
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(CloseDetail);
        TopLabel(closeRect, "X", "X", 0, 0, 40, 38, 18, Color.white, true);
        TopLabel(detailPanel, "LevelDescription", level.description,
            18, 50, 322, 42, 14, InkMuted, false, TextAlignmentOptions.Left);
        bool replay = CampaignProgress.Stars(selectedRegion.id, selectedLevel) > 0;
        string reward = replay
            ? $"Ödül: %{Mathf.RoundToInt(BattleManager.ReplayRewardRate * 100f)} (tekrar)"
            : $"Ödül: {level.reward}";
        string extra = level.kind == CampaignLevelKind.TimeTrial
            ? $"   ·   Süre: {Mathf.RoundToInt(BattleManager.TimeTrialSeconds / 60f)} dk" : "";
        TopLabel(detailPanel, "Meta", $"Düşman: {level.TotalEnemies}    {reward}{extra}",
            18, 96, 236, 26, 13, InkGold, false, TextAlignmentOptions.Left);
        if (!string.IsNullOrEmpty(level.modifier))
            TopLabel(detailPanel, "Modifier", level.modifier, 262, 96, 80, 26, 12, ReferenceUiVisuals.Red, true);
        int best = CampaignProgress.Stars(selectedRegion.id, selectedLevel);
        TopLabel(detailPanel, "BestLabel", best > 0 ? "EN İYİ" : "HENÜZ OYNANMADI", 18, 124, 140, 22, 11,
            InkMuted, false, TextAlignmentOptions.Left);
        StarRow(detailPanel, "BestStars", Vector2.up, new Vector2(best > 0 ? 100 : 176, -135), 17f, 20f, best);
        var battle = ReferenceUiVisuals.Button(detailPanel, "StartCampaignBattle", "SAVAŞA BAŞLA", true,
            Vector2.zero, new Vector2(1, 0), new Vector2(16, 14), new Vector2(-16, 66), 19);
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
        toastBackground.transform.SetAsLastSibling();
        toastBackground.GetComponent<PanelController>().Show();
        yield return new WaitForSeconds(1.25f);
        toastBackground.GetComponent<PanelController>().Hide();
        toastRoutine = null;
    }

    private static string EnemyList(RegionData region)
    {
        if (region?.enemyTypes == null || region.enemyTypes.Length == 0) return "—";
        var names = new List<string>();
        foreach (var type in region.enemyTypes)
        {
            if (EnemyTypeInfo.IsBoss(type)) continue;
            string name = EnemyTypeInfo.DisplayName(type);
            if (!names.Contains(name)) names.Add(name);
        }
        return string.Join(", ", names);
    }
}
