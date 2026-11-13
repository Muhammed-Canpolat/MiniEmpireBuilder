using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Radyal menüdeki tek bir yuvarlak düğme (AŞAMA 3 §3F). Yeni menü eklemek = yeni bir RadialItem kaydetmek.</summary>
public sealed class RadialItem
{
    public string id;
    public Func<string> label;
    public Func<Sprite> iconProvider;
    /// <summary>İkonun yanına küçük eklenen ikinci görsel (ör. ada için ağaç).</summary>
    public Sprite iconAccent;
    public Color faceColor = new Color(0.11f, 0.09f, 0.07f, 1f);
    public Action onClick;
    /// <summary>0 = rozet yok, 1 = kırmızı nokta, N&gt;1 = sayı.</summary>
    public Func<int> badgeProvider;
    public Func<bool> visibleProvider;
    public Func<bool> lockedProvider;
    public bool emphasised;
    /// <summary>Ana düğme merkezine göre açık konum (kanvas birimi).</summary>
    public Vector2 offset;
    /// <summary>Hazır düğme (BuildBtn/BattleBtn/HeroBtn/IslandBtn); null ise oluşturulur.</summary>
    public Button button;

    internal RectTransform rt, visual;
    internal CanvasGroup group;
    internal GameObject badge, lockIcon;
    internal TextMeshProUGUI badgeText;
    internal float t, startAt;
    internal bool target;
    internal int order;

    public bool Visible => button != null && button.gameObject.activeSelf;
    public bool IsLocked => lockedProvider != null && lockedProvider();
    public int BadgeValue => badgeProvider != null ? badgeProvider() : 0;
    public bool BadgeShown => badge != null && badge.activeSelf;
    public RectTransform Rect => rt;
}

/// <summary>
/// Ana Üs alt menüsü: kapalıyken koyu şerit + ortada büyük yuvarlak düğme; dokununca altın patlama ve
/// 4 yuvarlak düğme yay şeklinde açılır (İNŞA ET, SEFER, KAHRAMAN, ADA). Son durum PlayerPrefs'te tutulur,
/// ilk açılışta menü açıktır. Animasyon kendi güncellemesiyle yapılır (DOTween yok): düğmelerin dönüşümüne
/// dokunan rehber vurgusu / basma animasyonuyla çakışmaz.
/// </summary>
public sealed class RadialDockMenu : MonoBehaviour
{
    public const string PrefKey = "MEB_RadialOpen";
    private const float MainSize = 92f, MainCenterY = 56f;

    public static RadialDockMenu Current { get; private set; }

    private readonly List<RadialItem> items = new List<RadialItem>();
    public IReadOnlyList<RadialItem> Items => items;
    public bool IsOpen { get; private set; }
    public RectTransform MainButton => main;

    private RectTransform hud, main, mainVisual, glow;
    private Image mainGlowImage;
    private GameObject mainDot;
    private Image[] sparks;
    private float burstStart = -10f, nextRefresh;
    private bool dockShown = true;
    private static Sprite circle;

    private static Sprite Circle => circle != null ? circle : circle = BaseMapManager.MakeCircleSprite(Color.white);

    // ==================== KURULUM ====================

    /// <summary>HUD köküne radyal menüyü kurar; dört hazır düğmeyi (varsa) yuvarlak öğeye çevirir.</summary>
    public static RadialDockMenu Attach(Transform hudRoot)
    {
        if (hudRoot == null) return null;
        var existing = hudRoot.Find("RadialMainBtn");
        if (existing != null) return existing.GetComponent<RadialDockMenu>();

        var buttonImage = ReferenceUiVisuals.Image(hudRoot, "RadialMainBtn", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, Vector2.zero, Color.clear);
        var rt = buttonImage.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(MainSize, MainSize);
        rt.anchoredPosition = new Vector2(0f, MainCenterY);
        buttonImage.raycastTarget = true;
        var menu = rt.gameObject.AddComponent<RadialDockMenu>();
        menu.hud = hudRoot as RectTransform;
        menu.main = rt;
        menu.BuildMain(buttonImage);
        Current = menu;

        var b = hudRoot.Find("BuildBtn")?.GetComponent<Button>();
        var s = hudRoot.Find("BattleBtn")?.GetComponent<Button>();
        var h = hudRoot.Find("HeroBtn")?.GetComponent<Button>();
        var a = hudRoot.Find("IslandBtn")?.GetComponent<Button>();
        menu.Register(new RadialItem
        {
            id = "build", button = b, label = () => L.T("İNŞA ET", "BUILD"), offset = new Vector2(-127f, 46f),
            badgeProvider = BuildBadge
        });
        menu.Register(new RadialItem
        {
            id = "campaign", button = s, label = () => L.T("SEFER", "CAMPAIGN"), offset = new Vector2(-57f, 124f),
            emphasised = true
        });
        menu.Register(new RadialItem
        {
            id = "hero", button = h, label = () => L.T("KAHRAMAN", "HERO"), offset = new Vector2(57f, 124f)
        });
        var islandCatalog = IslandVisualCatalog.Instance;
        menu.Register(new RadialItem
        {
            id = "island", button = a, label = () => IslandSession.Unlocked() ? L.T("ADA", "ISLE") : L.T("ADA · 5", "ISLE · 5"),
            offset = new Vector2(127f, 46f),
            iconProvider = () => islandCatalog != null && islandCatalog.castle != null ? islandCatalog.castle : SpriteManager.Instance?.MainBase,
            iconAccent = SpriteManager.Instance?.Tree,
            faceColor = new Color(0.12f, 0.30f, 0.36f, 1f),   // deniz
            lockedProvider = () => !IslandSession.Unlocked(),
            badgeProvider = IslandBadge
        });
        menu.hud.Find("RadialMainBtn")?.SetAsLastSibling();

        bool open = !PlayerPrefs.HasKey(PrefKey) || PlayerPrefs.GetInt(PrefKey, 1) == 1;
        menu.SetOpen(open, true, false);
        return menu;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    /// <summary>Yeni bir öğe ekler ve görünümünü kurar (sıra = kayıt sırası).</summary>
    public RadialItem Register(RadialItem item)
    {
        item.order = items.Count;
        items.Add(item);
        BuildItem(item);
        if (!dockShown) item.button.gameObject.SetActive(false);
        return item;
    }

    private void BuildMain(Image rootImage)
    {
        // Parıltı (dokununca patlayan altın halka) ve kıvılcımlar: düğmenin arkasında.
        glow = ReferenceUiVisuals.Rect(main, "Glow", new Vector2(-0.35f, -0.35f), new Vector2(1.35f, 1.35f), Vector2.zero, Vector2.zero);
        mainGlowImage = glow.gameObject.AddComponent<Image>();
        mainGlowImage.sprite = Circle;
        mainGlowImage.color = new Color(1f, 0.82f, 0.30f, 0f);
        mainGlowImage.raycastTarget = false;
        sparks = new Image[10];
        for (int i = 0; i < sparks.Length; i++)
        {
            var spark = ReferenceUiVisuals.Image(main, "Spark" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-3f, -3f), new Vector2(3f, 3f), new Color(1f, 0.9f, 0.45f, 0f));
            spark.sprite = Circle;
            sparks[i] = spark;
        }

        mainVisual = ReferenceUiVisuals.Rect(main, "Visual", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Ring(mainVisual, ReferenceUiVisuals.Hex(0x120D08), 0f);                    // dış hat
        Ring(mainVisual, ReferenceUiVisuals.Hex(0xE2B34A), 2f);                    // altın çerçeve
        Ring(mainVisual, ReferenceUiVisuals.Hex(0x7A4E16), 7f);                    // koyu bronz iç halka
        Ring(mainVisual, ReferenceUiVisuals.Hex(0xF3D27A), 9f);                    // ince altın çizgi
        Ring(mainVisual, ReferenceUiVisuals.Hex(0x1A1410), 11f);                   // koyu yüz
        var shield = ReferenceUiVisuals.Image(mainVisual, "Emblem", Vector2.zero, Vector2.one,
            new Vector2(22f, 20f), new Vector2(-22f, -22f), Color.white);
        shield.sprite = CampaignSceneSetup.Art("ui_icon_shield") ?? SpriteManager.Instance?.IconShield;
        shield.preserveAspect = true;
        var gem = ReferenceUiVisuals.Image(mainVisual, "Gem", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-9f, -9f), new Vector2(9f, 9f), Color.white);
        gem.sprite = GemIcon.Sprite;
        gem.preserveAspect = true;

        mainDot = BuildDot(mainVisual, new Vector2(-16f, -16f), new Vector2(0f, 0f), out _);
        mainDot.SetActive(false);

        var button = rootImage.gameObject.AddComponent<Button>();
        button.targetGraphic = rootImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Toggle);
    }

    private static Image Ring(RectTransform parent, Color color, float inset)
    {
        var image = ReferenceUiVisuals.Image(parent, "Ring", Vector2.zero, Vector2.one,
            new Vector2(inset, inset), new Vector2(-inset, -inset), color);
        image.sprite = Circle;
        return image;
    }

    private static GameObject BuildDot(RectTransform parent, Vector2 low, Vector2 high, out TextMeshProUGUI text)
    {
        // Krem kenarlı kırmızı nokta: kök krem daire, içindeki "Dot" kırmızı (alt öğe üstte çizilir).
        var rim = ReferenceUiVisuals.Image(parent, "Badge", Vector2.one, Vector2.one, low, high, ReferenceUiVisuals.Light);
        rim.sprite = Circle;
        rim.rectTransform.pivot = new Vector2(1f, 1f);
        rim.rectTransform.sizeDelta = new Vector2(20f, 20f);
        rim.rectTransform.anchoredPosition = new Vector2(3f, 3f);
        var dot = ReferenceUiVisuals.Image(rim.rectTransform, "Dot", Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f), ReferenceUiVisuals.Red);
        dot.sprite = Circle;
        text = ReferenceUiVisuals.Text(rim.rectTransform, "Count", "", 11, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, bodyBold: true);
        text.enableAutoSizing = false;
        return rim.gameObject;
    }

    private void BuildItem(RadialItem item)
    {
        float size = item.emphasised ? 70f : 60f;
        if (item.button == null)
        {
            item.button = ReferenceUiVisuals.Button(hud, "Radial_" + item.id, item.label(), false,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero, 11);
            if (item.onClick != null) item.button.onClick.AddListener(() => item.onClick());
        }
        var rt = (RectTransform)item.button.transform;
        item.rt = rt;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = main.anchoredPosition;

        // Eski düğme yüzeyi görünmez dokunma alanı olur; ikon + etiket "Visual" altına taşınır.
        var iconOld = rt.Find("Icon");
        var labelOld = rt.Find("Label");
        var root = item.button.GetComponent<Image>();
        root.sprite = null;
        root.type = Image.Type.Simple;
        root.color = Color.clear;
        root.raycastTarget = true;
        item.button.transition = Selectable.Transition.None;

        var visual = ReferenceUiVisuals.Rect(rt, "Visual", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        item.visual = visual;
        item.group = visual.gameObject.AddComponent<CanvasGroup>();
        item.group.blocksRaycasts = false;
        Color gold = item.emphasised ? ReferenceUiVisuals.Hex(0xF6D463) : ReferenceUiVisuals.Hex(0xD9A93C);
        Ring(visual, ReferenceUiVisuals.Hex(0x120D08), 0f);
        Ring(visual, gold, 1.5f);
        Ring(visual, ReferenceUiVisuals.Hex(0x5A3A12), 4.5f);
        Ring(visual, item.faceColor, 6f);

        Image icon = iconOld != null ? iconOld.GetComponent<Image>() : null;
        if (icon == null)
            icon = ReferenceUiVisuals.Image(rt, "Icon", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
        icon.transform.SetParent(visual, false);
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        float pad = item.iconAccent != null ? 12f : 9f;
        iconRt.offsetMin = new Vector2(pad, pad); iconRt.offsetMax = new Vector2(-pad, -pad);
        iconRt.localScale = Vector3.one;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.color = Color.white;
        if (item.iconProvider != null)
        {
            var sprite = item.iconProvider();
            if (sprite != null) icon.sprite = sprite;
        }
        if (item.iconAccent != null)
        {
            var accent = ReferenceUiVisuals.Image(visual, "IconAccent", new Vector2(0.55f, 0.1f), new Vector2(1f, 0.6f),
                Vector2.zero, Vector2.zero, Color.white);
            accent.sprite = item.iconAccent;
            accent.preserveAspect = true;
            iconRt.offsetMax = new Vector2(-pad - 6f, -pad);
        }
        if (item.lockedProvider != null)
        {
            var lockImage = ReferenceUiVisuals.Image(visual, "LockIcon", new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f),
                Vector2.zero, Vector2.zero, Color.white);
            lockImage.sprite = CampaignSceneSetup.Art("ui_icon_lock");
            lockImage.preserveAspect = true;
            item.lockIcon = lockImage.gameObject;
            item.lockIcon.SetActive(item.IsLocked);
        }

        // Etiket kurdelesi: düğmenin altında koyu pul + altın ince hat.
        var ribbonGold = ReferenceUiVisuals.Image(visual, "RibbonEdge", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-41f, -23f), new Vector2(41f, -4f), ReferenceUiVisuals.Hex(0xC99A2E));
        var ribbon = ReferenceUiVisuals.Image(visual, "Ribbon", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-40f, -22f), new Vector2(40f, -5f), ReferenceUiVisuals.Hex(0x17110B));
        foreach (var r in new[] { ribbonGold, ribbon })
        {
            r.sprite = ReferenceUiVisuals.SolidPillSprite();
            r.type = Image.Type.Sliced;
            r.pixelsPerUnitMultiplier = 3f;
        }
        TextMeshProUGUI label = labelOld != null ? labelOld.GetComponent<TextMeshProUGUI>() : null;
        if (label == null)
            label = ReferenceUiVisuals.Text(rt, "Label", "", 11, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, bodyBold: true);
        label.transform.SetParent(visual, false);
        var labelRt = label.rectTransform;
        labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(78f, 17f);
        labelRt.anchoredPosition = new Vector2(0f, -13.5f);
        labelRt.localScale = Vector3.one;
        label.text = item.label();
        ReferenceUiVisuals.ApplyFont(label, 11f, ReferenceUiVisuals.Light, true);
        label.enableAutoSizing = true;
        label.fontSizeMin = 7f; label.fontSizeMax = 11f;
        label.characterSpacing = 0.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        item.badge = BuildDot(visual, Vector2.zero, Vector2.zero, out item.badgeText);
        item.badge.SetActive(false);

        var press = item.button.GetComponent<ReferenceButtonPress>();
        if (press == null) item.button.gameObject.AddComponent<ReferenceButtonPress>();
    }

    // ==================== DURUM ====================

    public void Toggle() => SetOpen(!IsOpen, false, true);

    /// <summary>Menüyü açar/kapatır. instant: animasyonsuz; save: PlayerPrefs'e yazar.</summary>
    public void SetOpen(bool open, bool instant = false, bool save = true)
    {
        IsOpen = open;
        if (save)
        {
            PlayerPrefs.SetInt(PrefKey, open ? 1 : 0);
            PlayerPrefs.Save();
        }
        float now = Time.unscaledTime;
        foreach (var item in items)
        {
            item.target = open;
            bool visible = open && dockShown && (item.visibleProvider == null || item.visibleProvider());
            item.target = visible;
            // Açılırken soldan sağa, kapanırken tersi ~40 ms gecikmeyle.
            item.startAt = now + (instant ? 0f : (open ? item.order : items.Count - 1 - item.order) * 0.04f);
            if (instant)
            {
                item.t = visible ? 1f : 0f;
                Apply(item);
                item.button.gameObject.SetActive(visible);
            }
            else if (visible) item.button.gameObject.SetActive(true);
        }
        if (!instant && open) burstStart = now;
        else if (instant) { burstStart = -10f; ApplyBurst(1f, false); }
        if (instant) return;
    }

    /// <summary>Modal paneller açılırken (BaseWorldUI.ToggleBottomButtons) menüyü topluca gizler/gösterir.</summary>
    public void SetDockVisible(bool show)
    {
        dockShown = show;
        main.gameObject.SetActive(show);
        SetOpen(IsOpen, true, false);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        foreach (var item in items)
        {
            if (now < item.startAt) continue;
            float goal = item.target ? 1f : 0f;
            if (!Mathf.Approximately(item.t, goal))
            {
                item.t = Mathf.MoveTowards(item.t, goal, Time.unscaledDeltaTime / 0.30f);
                Apply(item);
                if (!item.target && item.t <= 0f) item.button.gameObject.SetActive(false);
            }
        }

        float age = now - burstStart;
        if (age >= 0f && age < 0.6f) ApplyBurst(age / 0.6f, true);
        else if (age >= 0.6f && age < 0.7f) ApplyBurst(1f, false);

        if (now >= nextRefresh)
        {
            nextRefresh = now + 0.5f;
            RefreshBadges();
        }
    }

    private void Apply(RadialItem item)
    {
        float t = item.t;
        float e = EaseOutBack(Mathf.Clamp01(t));
        item.rt.anchoredPosition = Vector2.LerpUnclamped(main.anchoredPosition, main.anchoredPosition + item.offset, e);
        item.visual.localScale = Vector3.one * Mathf.Max(0.001f, e);
        item.group.alpha = Mathf.Clamp01(t * 2.2f);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private void ApplyBurst(float x, bool active)
    {
        if (mainGlowImage == null) return;
        if (!active)
        {
            mainGlowImage.color = new Color(1f, 0.82f, 0.30f, 0f);
            glow.localScale = Vector3.one * (IsOpen ? 1.0f : 0.9f);
            mainVisual.localScale = Vector3.one;
            foreach (var s in sparks) s.color = new Color(1f, 0.9f, 0.45f, 0f);
            return;
        }
        float fade = 1f - x;
        mainGlowImage.color = new Color(1f, 0.82f, 0.30f, 0.65f * fade * fade);
        glow.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.9f, 1f - fade * fade);
        mainVisual.localScale = Vector3.one * (1f + 0.14f * Mathf.Sin(x * Mathf.PI));
        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = (i / (float)sparks.Length) * Mathf.PI * 2f + 0.3f;
            float radius = Mathf.Lerp(MainSize * 0.45f, MainSize * 1.15f, 1f - fade * fade);
            sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            sparks[i].rectTransform.localScale = Vector3.one * (1.2f + (i % 3) * 0.5f);
            sparks[i].color = new Color(1f, 0.9f, 0.45f, fade);
        }
    }

    // ==================== ROZETLER ====================

    public void RefreshBadges()
    {
        bool any = false;
        foreach (var item in items)
        {
            int value = item.BadgeValue;
            if (item.badge != null)
            {
                item.badge.SetActive(value > 0);
                if (item.badgeText != null) item.badgeText.text = value > 1 ? Mathf.Min(value, 99).ToString() : "";
            }
            if (item.lockIcon != null) item.lockIcon.SetActive(item.IsLocked);
            if (value > 0) any = true;
        }
        // Menü kapalıyken ana düğmede toplu uyarı noktası.
        if (mainDot != null) mainDot.SetActive(any && !IsOpen);
    }

    /// <summary>Boşta inşaatçı varken bütçeye uyan bir yükseltme var mı (ucuz kontrol).</summary>
    private static int BuildBadge()
    {
        var gm = GameManager.Instance;
        var data = gm?.PlayerData;
        if (data == null || !gm.HasFreeBuilder) return 0;
        foreach (var placed in data.placedBuildings)
        {
            if (placed == null || placed.level >= Progression.MaxLevel) continue;
            if (!Enum.TryParse(placed.buildingType, out BuildingType type)) continue;
            if (gm.Gold >= Progression.UpgradeCost(type, placed.level + 1)) return 1;
        }
        return 0;
    }

    /// <summary>Ada baskını uyarı/canlı evresindeyse rozet.</summary>
    private static int IslandBadge()
    {
        if (!IslandSession.Unlocked()) return 0;
        var save = IslandSession.Save;
        return save != null && IslandRaidScheduler.PhaseAt(save, IslandRepair.Now) != IslandRaidScheduler.Phase.None ? 1 : 0;
    }
}
