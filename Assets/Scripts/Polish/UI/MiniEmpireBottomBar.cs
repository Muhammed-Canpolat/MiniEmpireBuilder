using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ana Üs yeni alt bar görünümü (D107, UI_DEGISIKLIK §2). Yalnız görünüm: mevcut BuildBtn/BattleBtn/HeroBtn/IslandBtn nesneleri
/// ve dinleyicileri aynen kalır (HUD kökünün doğrudan çocuğu; adlar korunur), yalnız giydirilir. Kırmızı ok barı aşağı kaydırıp
/// gizler/geri açar; açık/kapalı durumu PlayerPrefs <see cref="PrefKey"/> (oyuncu kaydına girmez).
/// KAMERA DEĞİŞMEZ: "BottomDock" (kamera çerçevesinin okuduğu dikdörtgen) hep ≈78 birimde sabit kalır, kayan şey yalnız
/// görseller ve düğmelerdir; bar kapanınca görüş alanı/zoom oynamaz (bug 13). Tüm yerleşim anchor + ref. tuval birimidir.
/// </summary>
public sealed class MiniEmpireBottomBar : MonoBehaviour
{
    public const string PrefKey = "MEB_BaseBarOpen";
    public const float BarHeight = 84f, ButtonHeight = 75f, ButtonY = 3f, ExtraBottom = 120f, SlideSeconds = 0.25f;
    private const float TabOpenY = BarHeight - 9f, TabClosedY = 6f;
    private static readonly string[] ButtonNames = { "BuildBtn", "BattleBtn", "HeroBtn", "IslandBtn" };
    private static readonly float[] ButtonX = { 0.125f, 0.375f, 0.625f, 0.875f };

    public static MiniEmpireBottomBar Current { get; private set; }

    public bool IsOpen { get; private set; } = true;
    /// <summary>Modal panel (inşaat/yerleştirme) açıkken false: bar topluca gizlenir.</summary>
    public bool DockShown { get; private set; } = true;
    public bool TabBadgeShown => tabDot != null && tabDot.activeSelf;
    public RectTransform Tab => tab;

    private struct Slider { public RectTransform rt; public float baseY; public CanvasGroup group; }

    private readonly System.Collections.Generic.List<Slider> sliders = new System.Collections.Generic.List<Slider>();
    private RectTransform slide, tab;
    private Image tabImage;
    private GameObject tabDot;
    private CanvasGroup tabGroup;
    private RectTransform buildDotHost, islandDotHost;
    private float t, target;   // 0 = açık, 1 = kapalı
    private float nextRefresh;
    private static Sprite circle;

    private static Sprite Circle => circle != null ? circle : circle = BaseMapManager.MakeCircleSprite(Color.white);

    /// <summary>Bar görünümünü kurar (bayrak/katalog yoksa null; eski dock/radyal menü çalışır).</summary>
    public static MiniEmpireBottomBar Apply(Transform hud)
    {
        if (hud == null || !MiniEmpireUiCatalog.BottomBarReady) return null;
        var dock = hud.Find("BottomDock") as RectTransform;
        if (dock == null) return null;
        var existing = dock.GetComponent<MiniEmpireBottomBar>();
        if (existing != null) return existing;
        var bar = dock.gameObject.AddComponent<MiniEmpireBottomBar>();
        bar.Build(hud, dock, MiniEmpireUiCatalog.Instance);
        Current = bar;
        return bar;
    }

    private void OnDestroy() { if (Current == this) Current = null; }

    // ==================== KURULUM ====================

    private void Build(Transform hud, RectTransform dock, MiniEmpireUiCatalog c)
    {
        // BottomDock: kamera çerçevesi için sabit, görünmez dikdörtgen (ad ve alt-yapışık düzen korunur).
        dock.anchorMin = new Vector2(0f, 0f);
        dock.anchorMax = new Vector2(1f, 0f);
        dock.pivot = new Vector2(0.5f, 0f);
        dock.sizeDelta = new Vector2(0f, BarHeight);
        dock.anchoredPosition = Vector2.zero;
        var dockImage = dock.GetComponent<Image>();
        if (dockImage != null) { dockImage.sprite = null; dockImage.color = Color.clear; dockImage.raycastTarget = false; }
        foreach (Transform child in dock) child.gameObject.SetActive(false);   // eski tahta/ray/perçin katmanları

        // Kayan görseller: gövde + ayıraçlar.
        slide = Child(dock, "BarSlide");
        Stretch(slide);
        var slideGroup = slide.gameObject.AddComponent<CanvasGroup>();

        var fill = Child(slide, "BodyFill");
        fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(1f, 0f);
        fill.pivot = new Vector2(0.5f, 1f);
        fill.sizeDelta = new Vector2(0f, ExtraBottom);   // ev çubuğu bölgesi: gövdenin altı ekran dibine kadar uzar
        fill.anchoredPosition = new Vector2(0f, 1f);
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.23f, 0.094f, 0.031f, 1f);
        fillImage.raycastTarget = true;

        var body = Child(slide, "Body");
        Stretch(body);
        var bodyImage = body.gameObject.AddComponent<Image>();
        bodyImage.sprite = c.bottombarBg;
        Slice(bodyImage);
        bodyImage.raycastTarget = true;   // barın boşluklarından arazi tıklanmasın (açıkken)

        // 5 mavi bayrak ayıracı (kenardakiler içe yaslı).
        for (int i = 0; i < 5; i++)
        {
            var sep = Child(slide, "Sep" + i);
            float x = i * 0.25f;
            sep.anchorMin = sep.anchorMax = new Vector2(x, 0.5f);
            sep.pivot = new Vector2(i == 0 ? 0f : i == 4 ? 1f : 0.5f, 0.5f);
            sep.sizeDelta = new Vector2(c.separatorBanner.rect.width / SliceMultiplier, c.separatorBanner.rect.height / SliceMultiplier);
            sep.anchoredPosition = new Vector2(i == 0 ? 2f : i == 4 ? -2f : 0f, -1f);
            var sepImage = sep.gameObject.AddComponent<Image>();
            sepImage.sprite = c.separatorBanner;
            sepImage.type = Image.Type.Sliced;
            sepImage.pixelsPerUnitMultiplier = SliceMultiplier;
            sepImage.raycastTarget = false;
        }
        sliders.Add(new Slider { rt = slide, baseY = 0f, group = slideGroup });

        // Dört düğme: mevcut nesneler giydirilir.
        Sprite[] icons = { c.iconBuild, c.iconExpedition, c.iconHero, c.iconIsland };
        string[] labels =
        {
            L.T("İNŞA ET", "BUILD"), L.T("SEFER", "CAMPAIGN"), L.T("KAHRAMAN", "HERO"), L.T("ADA", "ISLE")
        };
        for (int i = 0; i < ButtonNames.Length; i++)
        {
            var rt = hud.Find(ButtonNames[i]) as RectTransform;
            if (rt == null) continue;
            SkinButton(rt, ButtonX[i], icons[i], labels[i], c, i >= 2);
            var group = GetOrAdd<CanvasGroup>(rt);
            sliders.Add(new Slider { rt = rt, baseY = ButtonY, group = group });
            if (i == 0) buildDotHost = rt;
            if (i == 3) islandDotHost = rt;
        }
        if (buildDotHost != null) AddDot(buildDotHost, new Vector2(-10f, -10f));
        if (islandDotHost != null) AddDot(islandDotHost, new Vector2(-10f, -10f));

        BuildTab(hud, c);

        IsOpen = PlayerPrefs.GetInt(PrefKey, 1) != 0;
        target = t = IsOpen ? 0f : 1f;
        Layout();
        RefreshBadges();
    }

    private static void SkinButton(RectTransform rt, float cx, Sprite iconSprite, string label, MiniEmpireUiCatalog c, bool isHeroOrIsland)
    {
        rt.anchorMin = new Vector2(cx - 0.1075f, 0f);
        rt.anchorMax = new Vector2(cx + 0.1075f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, ButtonY);
        rt.offsetMax = new Vector2(0f, ButtonY + ButtonHeight);
        rt.localScale = Vector3.one;

        var face = rt.GetComponent<Image>();
        face.sprite = null;
        face.color = new Color(1f, 1f, 1f, 0f);   // görünmez dokunma alanı
        face.raycastTarget = true;
        var button = rt.GetComponent<Button>();
        if (button != null) { button.transition = Selectable.Transition.None; button.targetGraphic = face; }
        var oldPress = rt.GetComponent<ReferenceButtonPress>();
        if (oldPress != null) Destroy(oldPress);   // basma ölçeği iç "Visual"a taşınır (rehber nabzıyla çakışmasın)

        var icon = rt.Find("Icon");
        var labelTf = rt.Find("Label");
        foreach (Transform child in rt)
            if (child != icon && child != labelTf && child.name != "Visual" && child.name != "Badge") child.gameObject.SetActive(false);

        var visual = rt.Find("Visual") as RectTransform;
        if (visual == null) visual = Child(rt, "Visual");
        Stretch(visual);
        visual.SetAsFirstSibling();

        var frame = Child(visual, "Frame");
        Stretch(frame);
        var frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.sprite = c.menuFrame;
        Slice(frameImage);
        frameImage.raycastTarget = false;

        var plate = Child(visual, "Plate");
        plate.anchorMin = new Vector2(0.09f, 0.07f); plate.anchorMax = new Vector2(0.91f, 0.29f);
        plate.offsetMin = plate.offsetMax = Vector2.zero;
        var plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.sprite = c.labelPlate;
        plateImage.type = Image.Type.Sliced;
        plateImage.pixelsPerUnitMultiplier = SliceMultiplier;
        plateImage.raycastTarget = false;

        var iconRt = icon as RectTransform;
        if (iconRt == null) iconRt = Child(rt, "Icon");
        iconRt.SetParent(visual, false);
        iconRt.anchorMin = new Vector2(0.17f, 0.31f); iconRt.anchorMax = new Vector2(0.83f, 0.93f);
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        var iconImage = GetOrAdd<Image>(iconRt);
        iconImage.sprite = iconSprite;
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = true;

        var labelRt = labelTf as RectTransform;
        if (labelRt != null)
        {
            labelRt.SetParent(plate, false);
            labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(6f, 0f); labelRt.offsetMax = new Vector2(-6f, 0f);
            var text = labelRt.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
                ReferenceUiVisuals.ApplyFont(text, 14f, Color.white, false, true);
                text.enableAutoSizing = true;
                text.fontSizeMin = 6f;
                text.fontSizeMax = 13f;
                text.characterSpacing = 0f;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
            }
        }

        var press = rt.gameObject.AddComponent<MiniBarPress>();
        press.visual = visual;
    }

    private void BuildTab(Transform hud, MiniEmpireUiCatalog c)
    {
        tab = Child(hud, "BarArrowTab");
        tab.anchorMin = tab.anchorMax = new Vector2(0.5f, 0f);
        tab.pivot = new Vector2(0.5f, 0f);
        tab.sizeDelta = new Vector2(64f, 36f);   // dokunma alanı görselden büyük (≥44 dp)
        tab.anchoredPosition = new Vector2(0f, TabOpenY);
        var hit = tab.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;
        var button = tab.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        button.onClick.AddListener(Toggle);
        tabGroup = tab.gameObject.AddComponent<CanvasGroup>();

        var visual = Child(tab, "Visual");
        visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
        visual.sizeDelta = new Vector2(c.arrowDown.rect.width / SliceMultiplier, c.arrowDown.rect.height / SliceMultiplier);
        visual.anchoredPosition = Vector2.zero;
        tabImage = visual.gameObject.AddComponent<Image>();
        tabImage.sprite = c.arrowDown;
        tabImage.preserveAspect = true;
        tabImage.raycastTarget = false;
        AddDot(tab, new Vector2(-14f, -8f));
        tabDot = tab.Find("Badge").gameObject;
        tabDot.SetActive(false);
        tab.SetAsLastSibling();
    }

    private static void AddDot(RectTransform host, Vector2 inset)
    {
        var dot = Child(host, "Badge");
        dot.anchorMin = dot.anchorMax = new Vector2(1f, 1f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(14f, 14f);
        dot.anchoredPosition = inset;
        var rim = dot.gameObject.AddComponent<Image>();
        rim.sprite = Circle;
        rim.color = ReferenceUiVisuals.Light;
        rim.raycastTarget = false;
        var inner = Child(dot, "Dot");
        Stretch(inner);
        inner.offsetMin = new Vector2(1.5f, 1.5f); inner.offsetMax = new Vector2(-1.5f, -1.5f);
        var innerImage = inner.gameObject.AddComponent<Image>();
        innerImage.sprite = Circle;
        innerImage.color = ReferenceUiVisuals.Red;
        innerImage.raycastTarget = false;
        dot.gameObject.SetActive(false);
    }

    // ==================== DURUM ====================

    public void Toggle() => SetOpen(!IsOpen, true, true);

    /// <summary>Barı açar/kapatır. <paramref name="persist"/> false: rehber gibi geçici zorlamalar tercihi bozmaz.</summary>
    public void SetOpen(bool open, bool animate, bool persist)
    {
        IsOpen = open;
        target = open ? 0f : 1f;
        if (persist) { PlayerPrefs.SetInt(PrefKey, open ? 1 : 0); PlayerPrefs.Save(); }
        if (!animate) t = target;
        Layout();
        RefreshBadges();
    }

    /// <summary>Modal paneller açılırken (BaseWorldUI.ToggleBottomButtons) barı topluca gizler/gösterir.</summary>
    public void SetDockVisible(bool show)
    {
        DockShown = show;
        Layout();
    }

    private void Update()
    {
        if (!Mathf.Approximately(t, target))
        {
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / SlideSeconds);
            Layout();
        }
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.5f; RefreshBadges(); }
    }

    private void Layout()
    {
        float e = t * t * (3f - 2f * t);
        float distance = BarHeight + 12f;
        bool live = DockShown && IsOpen;
        foreach (var s in sliders)
        {
            if (s.rt == null) continue;
            var p = s.rt.anchoredPosition;
            s.rt.anchoredPosition = new Vector2(p.x, s.baseY - e * distance);
            if (s.group != null)
            {
                s.group.alpha = DockShown ? 1f - e : 0f;
                s.group.blocksRaycasts = live;   // kapalıyken düğmeler dokunuş yutmaz, arazi tıklanır
                s.group.interactable = live;
            }
        }
        if (tab != null)
        {
            tab.anchoredPosition = new Vector2(0f, Mathf.Lerp(TabOpenY, TabClosedY, e));
            if (tabImage != null)
            {
                var c = MiniEmpireUiCatalog.Instance;
                if (c != null) tabImage.sprite = t > 0.5f ? c.arrowUp : c.arrowDown;
            }
            if (tabGroup != null) { tabGroup.alpha = DockShown ? 1f : 0f; tabGroup.blocksRaycasts = DockShown; }
        }
    }

    /// <summary>Rozetler radyal menüyle aynı sağlayıcılardan: İNŞA ET (D32 düzeltmeli) ve ADA; kapalıyken ok sekmesinde toplu nokta.</summary>
    public void RefreshBadges()
    {
        bool build = BaseBadges.CanBuildNew();
        bool island = RadialDockMenu.IslandBadge() > 0;
        SetDot(buildDotHost, build);
        SetDot(islandDotHost, island);
        if (tabDot != null) tabDot.SetActive((build || island) && !IsOpen);
    }

    public bool BuildDotShown => buildDotHost != null && buildDotHost.Find("Badge").gameObject.activeSelf;
    public bool IslandDotShown => islandDotHost != null && islandDotHost.Find("Badge").gameObject.activeSelf;

    private static void SetDot(RectTransform host, bool on)
    {
        var dot = host != null ? host.Find("Badge") : null;
        if (dot != null && dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
    }

    // ==================== YARDIMCILAR ====================

    private static float SliceMultiplier => MiniEmpireTopBarSkin.SliceMultiplier;

    /// <summary>Unity nesnelerinde `??` sahte-null'ı yakalamaz; açık denetim.</summary>
    private static T GetOrAdd<T>(Component host) where T : Component
    {
        var found = host.GetComponent<T>();
        return found != null ? found : host.gameObject.AddComponent<T>();
    }

    private static RectTransform Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Slice(Image image)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SliceMultiplier;
    }
}

/// <summary>Basma efekti iç "Visual"a uygulanır: düğmenin kendi ölçeğine (rehber nabzı) dokunmaz.</summary>
public sealed class MiniBarPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Transform visual;
    public void OnPointerDown(PointerEventData e) { if (visual == null) return; visual.DOKill(); visual.DOScale(0.93f, 0.08f).SetUpdate(true); }
    public void OnPointerUp(PointerEventData e) { Release(); }
    public void OnPointerExit(PointerEventData e) { Release(); }
    private void Release() { if (visual == null) return; visual.DOKill(); visual.DOScale(1f, 0.12f).SetUpdate(true); }
}
