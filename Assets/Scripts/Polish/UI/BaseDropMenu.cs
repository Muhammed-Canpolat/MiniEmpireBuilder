using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Açılır menü maddesi: yeni madde = <see cref="BaseDropMenu.Items"/>'a tek satır. <c>icon</c> null/boş dönerse satır ikonsuzdur.</summary>
public sealed class DropMenuItem
{
    public readonly string id, tr, en;
    public readonly Func<Sprite> icon;
    public readonly Action open;
    public readonly Func<bool> badge;

    public DropMenuItem(string id, Func<Sprite> icon, string tr, string en, Action open, Func<bool> badge)
    {
        this.id = id; this.icon = icon; this.tr = tr; this.en = en; this.open = open; this.badge = badge;
    }

    public string Label => L.T(tr, en);
}

/// <summary>
/// Ana Üs sağ üst açılır menü (D109, UI_DEGISIKLIK §4/§6): üst barın altında sağda ok düğmesi; açılınca Günlük / Görev / Etkinlik listesi.
/// Yalnız arayüz: ödül/görev mantığı DailyRewards/DailyQuests'te kalır, rozetler onların mevcut koşullarından (0,5 sn'de bir) okunur.
/// Açıkken listenin arkasında şeffaf kalkan dokunuşu yutar ve yalnız menüyü kapatır; kapalıyken yalnız ok düğmesi dokunuş yutar.
/// Başka seçim/panel (SelectionManager) ya da geri tuşu menüyü kapatır. Durum kalıcı değildir (her girişte kapalı).
/// Tüm yerleşim anchor + ref. tuval birimidir.
/// </summary>
public sealed class BaseDropMenu : MonoBehaviour
{
    public const float ToggleSize = 36f, RowHeight = 32f, RowWidth = 70f, Gap = 2f, AnimSeconds = 0.15f;
    private const float EdgeMargin = 6f, TopBarFallback = 64f;
    private const float ClosedAngle = 90f, OpenAngle = -90f;   // aşağı bakan ok: 90° = ">", -90° = "<"

    /// <summary>Maddeler (yukarıdan aşağıya). Etkinlik: ikonsuz (kupa EKSİK ASSET, ART_NEEDS #25), rozet hep kapalı.</summary>
    public static readonly List<DropMenuItem> Items = new List<DropMenuItem>
    {
        new DropMenuItem("daily", () => CampaignSceneSetup.Art("ui_icon_calendar", 0f), "Günlük", "Daily",
            DailyRewardsUI.OpenFromMenu, () => DailyRewards.CanClaim),
        new DropMenuItem("quests", () => CampaignSceneSetup.Art("ui_scroll", 0f), "Görev", "Quests",
            DailyQuestsUI.OpenFromMenu, () => DailyQuests.HasClaimable),
        new DropMenuItem("events", null, "Etkinlik", "Events", () => Current?.ShowComingSoon(), () => false),
    };

    public static BaseDropMenu Current { get; private set; }

    public bool IsOpen { get; private set; }
    public bool ComingSoonOpen => comingSoon != null;
    public int RowCount => rows.Count;
    public bool ToggleBadgeShown => toggleBadge != null && toggleBadge.activeSelf;
    public RectTransform Toggle => toggle;
    public RectTransform List => list;
    public RectTransform Shield => shield;

    private sealed class Row { public DropMenuItem item; public RectTransform rt; public GameObject badge; }

    private readonly List<Row> rows = new List<Row>();
    private RectTransform hud, toggle, list, shield, chevron;
    private CanvasGroup listGroup;
    private GameObject toggleBadge, comingSoon;
    private float nextRefresh, angle = ClosedAngle;
    private static Sprite circle;
    private static Sprite Circle => circle != null ? circle : circle = BaseMapManager.MakeCircleSprite(Color.white);
    private static float SliceMultiplier => MiniEmpireTopBarSkin.SliceMultiplier;

    /// <summary>Menüyü kurar (bayrak/katalog yoksa null; eski sol düğmeler çalışır).</summary>
    public static BaseDropMenu Apply(Transform hudRoot)
    {
        if (hudRoot == null || !MiniEmpireUiCatalog.DropMenuReady) return null;
        var existing = hudRoot.GetComponentInChildren<BaseDropMenu>(true);
        if (existing != null) return existing;
        var host = ReferenceUiVisuals.Rect(hudRoot, "DropMenu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var menu = host.gameObject.AddComponent<BaseDropMenu>();
        menu.Build(host, MiniEmpireUiCatalog.Instance);
        Current = menu;
        return menu;
    }

    private void OnDestroy()
    {
        SelectionManager.Release(this);
        if (Current == this) Current = null;
    }

    // ==================== KURULUM ====================

    private void Build(RectTransform host, MiniEmpireUiCatalog c)
    {
        hud = host;
        float barHeight = MiniEmpireUiCatalog.TopBarReady ? MiniEmpireTopBarSkin.BarHeight : TopBarFallback;
        float top = -(barHeight + EdgeMargin);

        // Kalkan: açıkken tam ekran şeffaf; dokunuş yalnız menüyü kapatır (arazi/bar tıklaması geçmez).
        shield = ReferenceUiVisuals.Rect(host, "DropMenuShield", Vector2.zero, Vector2.one, new Vector2(-400f, -400f), new Vector2(400f, 400f));
        var shieldImage = shield.gameObject.AddComponent<Image>();
        shieldImage.color = new Color(0f, 0f, 0f, 0f);
        shieldImage.raycastTarget = true;
        var shieldButton = shield.gameObject.AddComponent<Button>();
        shieldButton.transition = Selectable.Transition.None;
        shieldButton.targetGraphic = shieldImage;
        shieldButton.onClick.AddListener(Close);
        shield.gameObject.SetActive(false);

        // Liste gövdesi: çerçeve + dikey düzen; madde eklenince kendiliğinden büyür.
        list = Corner(host, "DropMenuList", new Vector2(-EdgeMargin, top - ToggleSize - Gap));
        var frame = list.gameObject.AddComponent<Image>();
        frame.sprite = c.menuFrame;
        Slice(frame);
        frame.raycastTarget = true;   // çerçeve boşluğuna dokunuş menüyü kapatmaz
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(7, 7, 11, 7);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        listGroup = list.gameObject.AddComponent<CanvasGroup>();
        list.gameObject.SetActive(false);

        // Aç/kapa düğmesi (referans: yuvarlak köşeli ahşap çerçeve + ok).
        toggle = Corner(host, "DropMenuToggle", new Vector2(-EdgeMargin, top));
        toggle.sizeDelta = new Vector2(ToggleSize, ToggleSize);
        var face = toggle.gameObject.AddComponent<Image>();
        face.sprite = c.menuFrame;
        Slice(face);
        face.raycastTarget = true;
        var button = toggle.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = face;
        button.onClick.AddListener(ToggleMenu);
        toggle.gameObject.AddComponent<MiniBarPress>().visual = toggle;

        chevron = ReferenceUiVisuals.Rect(toggle, "Chevron", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        chevron.sizeDelta = new Vector2(c.iconChevronDown.rect.width / SliceMultiplier * 1.15f, c.iconChevronDown.rect.height / SliceMultiplier * 1.15f);
        chevron.anchoredPosition = new Vector2(0f, -2f);
        var chevronImage = chevron.gameObject.AddComponent<Image>();
        chevronImage.sprite = c.iconChevronDown;
        chevronImage.preserveAspect = true;
        chevronImage.raycastTarget = false;
        SetAngle(ClosedAngle);

        toggleBadge = MakeBadge(toggle, 16f, new Vector2(-10f, -10f), true);
        toggleBadge.SetActive(false);

        RebuildRows();
        Refresh();
    }

    private RectTransform Corner(Transform parent, string name, Vector2 position)
    {
        var rt = ReferenceUiVisuals.Rect(parent, name, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero);
        rt.pivot = Vector2.one;
        rt.anchoredPosition = position;
        return rt;
    }

    /// <summary>Satırları <see cref="Items"/>'tan yeniden kurar (her açılışta çağrılır; madde ekleyince başka kod gerekmez).</summary>
    public void RebuildRows()
    {
        foreach (var r in rows)
            if (r.rt != null) { r.rt.SetParent(null, false); Destroy(r.rt.gameObject); }
        rows.Clear();
        var c = MiniEmpireUiCatalog.Instance;
        foreach (var item in Items) rows.Add(BuildRow(item, c));
        Refresh();
        if (list.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(list);
    }

    private Row BuildRow(DropMenuItem item, MiniEmpireUiCatalog c)
    {
        var rt = ReferenceUiVisuals.Rect(list, "Row_" + item.id, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = c.resCapsule;
        Slice(bg);
        bg.raycastTarget = true;
        var element = rt.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = RowHeight;
        element.minWidth = element.preferredWidth = RowWidth;
        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = bg;
        var captured = item;
        button.onClick.AddListener(() => { Close(); captured.open?.Invoke(); });
        rt.gameObject.AddComponent<MiniBarPress>().visual = rt;

        Sprite icon = item.icon != null ? item.icon() : null;
        float left = 6f;
        if (icon != null)
        {
            var iconRt = ReferenceUiVisuals.Rect(rt, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(22f, 22f);
            iconRt.anchoredPosition = new Vector2(15f, 0f);
            var iconImage = iconRt.gameObject.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            left = 28f;
        }
        var label = ReferenceUiVisuals.Text(rt, "Label", item.Label, 13f, Color.white,
            icon != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(left, 1f), new Vector2(-8f, -1f), false, true);
        label.fontSizeMin = 9f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        var row = new Row { item = item, rt = rt };
        row.badge = MakeBadge(rt, 12f, new Vector2(-7f, -4f), false);
        row.badge.SetActive(false);
        return row;
    }

    /// <summary>Kırmızı daire + beyaz kenar + "!" (MiniEmpireUI'da rozet sprite'ı yok; kodla çizilir).</summary>
    private static GameObject MakeBadge(Transform host, float size, Vector2 inset, bool pulse)
    {
        var rt = ReferenceUiVisuals.Rect(host, "Badge", Vector2.one, Vector2.one, Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = inset;
        var rim = rt.gameObject.AddComponent<Image>();
        rim.sprite = Circle;
        rim.color = ReferenceUiVisuals.Light;
        rim.raycastTarget = false;
        var inner = ReferenceUiVisuals.Rect(rt, "Dot", Vector2.zero, Vector2.one, new Vector2(1.2f, 1.2f), new Vector2(-1.2f, -1.2f));
        var innerImage = inner.gameObject.AddComponent<Image>();
        innerImage.sprite = Circle;
        innerImage.color = ReferenceUiVisuals.Red;
        innerImage.raycastTarget = false;
        var mark = ReferenceUiVisuals.Text(inner, "Mark", "!", size * 0.8f, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, true);
        mark.enableAutoSizing = false;
        mark.fontSize = size * 0.8f;
        mark.raycastTarget = false;
        if (pulse) rt.DOScale(1.15f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetLink(rt.gameObject);
        return rt.gameObject;
    }

    // ==================== DURUM ====================

    public void ToggleMenu() { if (IsOpen) Close(); else Open(); }

    public void Open()
    {
        if (IsOpen || list == null) return;
        IsOpen = true;
        SelectionManager.Select(this, () => CloseCore(false));   // bina/eylem seçimi ya da geri tuşu bunu kapatır
        RebuildRows();
        shield.gameObject.SetActive(true);
        list.gameObject.SetActive(true);
        listGroup.blocksRaycasts = listGroup.interactable = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        list.DOKill(); listGroup.DOKill();
        list.localScale = new Vector3(1f, 0f, 1f);
        listGroup.alpha = 0f;
        list.DOScaleY(1f, AnimSeconds).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(list.gameObject);
        listGroup.DOFade(1f, AnimSeconds).SetUpdate(true).SetLink(list.gameObject);
        TweenAngle(OpenAngle);
        Refresh();
    }

    public void Close() => CloseCore(true);

    private void CloseCore(bool release)
    {
        if (this == null || !IsOpen) return;
        IsOpen = false;
        if (release) SelectionManager.Release(this);
        shield.gameObject.SetActive(false);   // dokunuş hemen serbest
        listGroup.blocksRaycasts = listGroup.interactable = false;
        list.DOKill(); listGroup.DOKill();
        list.DOScaleY(0f, AnimSeconds).SetEase(Ease.InQuad).SetUpdate(true).SetLink(list.gameObject)
            .OnComplete(() => { if (!IsOpen) list.gameObject.SetActive(false); });
        listGroup.DOFade(0f, AnimSeconds).SetUpdate(true).SetLink(list.gameObject);
        TweenAngle(ClosedAngle);
    }

    private void TweenAngle(float target)
    {
        DOTween.Kill(chevron);
        DOTween.To(() => angle, SetAngle, target, AnimSeconds).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(chevron).SetLink(chevron.gameObject);
    }

    private void SetAngle(float value)
    {
        angle = value;
        if (chevron != null) chevron.localEulerAngles = new Vector3(0f, 0f, value);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        Refresh();
    }

    /// <summary>Rozetleri koşullardan yeniden hesaplar (D32 deseni: olay beklemez, gün dönümünde de söner).</summary>
    public void Refresh()
    {
        bool any = false;
        foreach (var r in rows)
        {
            bool on = r.item.badge != null && r.item.badge();
            any |= on;
            if (r.badge != null && r.badge.activeSelf != on) r.badge.SetActive(on);
        }
        if (toggleBadge != null && toggleBadge.activeSelf != any) toggleBadge.SetActive(any);
    }

    public bool RowBadgeShown(string id)
    {
        foreach (var r in rows) if (r.item.id == id) return r.badge != null && r.badge.activeSelf;
        return false;
    }

    public RectTransform RowRect(string id)
    {
        foreach (var r in rows) if (r.item.id == id) return r.rt;
        return null;
    }

    // ==================== ETKİNLİK "YAKINDA" ====================

    public void ShowComingSoon()
    {
        if (comingSoon != null) return;
        var dim = ReferenceUiVisuals.Image(hud, "EventsComingSoon", Vector2.zero, Vector2.one, new Vector2(-400f, -400f), new Vector2(400f, 400f),
            new Color(0f, 0f, 0f, 0.62f));
        dim.raycastTarget = true;
        comingSoon = dim.gameObject;
        var dismiss = comingSoon.AddComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(CloseComingSoon);

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(comingSoon.transform, "Panel", center, center, new Vector2(-130f, -80f), new Vector2(130f, 80f), Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;   // panele dokunmak kutuyu kapatmasın
        var ink = ReferenceUiVisuals.Hex(0x3B2A1A);
        ReferenceUiVisuals.Text(panel.transform, "Title", L.T("ETKİNLİK", "EVENTS"), 20, ink, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -44f), new Vector2(-16f, -14f), true);
        ReferenceUiVisuals.Text(panel.transform, "Message", L.T("Etkinlikler yakında geliyor!", "Events are coming soon!"), 15,
            ReferenceUiVisuals.Hex(0x6B5236), TextAlignmentOptions.Center, new Vector2(0f, 0f), Vector2.one,
            new Vector2(16f, 58f), new Vector2(-16f, -46f), false, true);
        var close = ReferenceUiVisuals.Button(panel.transform, "Close", L.T("KAPAT", "CLOSE"), true,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-50f, 12f), new Vector2(50f, 48f), 14);
        close.onClick.AddListener(CloseComingSoon);
        comingSoon.transform.SetAsLastSibling();
    }

    public void CloseComingSoon()
    {
        if (comingSoon != null) Destroy(comingSoon);
        comingSoon = null;
    }

    /// <summary>Geri tuşu (BackKeyRouter): "Yakında" kutusu açıksa kapatır.</summary>
    public static bool CloseComingSoonIfOpen()
    {
        if (Current == null || Current.comingSoon == null) return false;
        Current.CloseComingSoon();
        return true;
    }

    private static void Slice(Image image)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SliceMultiplier;
    }
}
