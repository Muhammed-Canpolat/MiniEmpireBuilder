using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Tüm ekranı kaplayan görünmez yüzey: dünya dokunuşlarını (hedef kilidi, atış yeri) toplar. Düğmeler üstte kalır.</summary>
public sealed class IslandTouchSurface : MonoBehaviour, IPointerClickHandler
{
    public Action<Vector2> Tapped;
    public void OnPointerClick(PointerEventData eventData)
    {
        // Kaydırma/sıkıştırma bırakılışı dokunuş sayılmaz (Faz B: yığın toplama ve bina seçimi yanlışlıkla tetiklenmesin).
        if (Vector2.Distance(eventData.pressPosition, eventData.position) > 24f) return;
        Tapped?.Invoke(eventData.position);
    }
}

/// <summary>Basılı tutma düğmesi (ATEŞ): bas / bırak olayları okçuya gider.</summary>
public sealed class IslandHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action Pressed, Released;
    private bool down;
    public void OnPointerDown(PointerEventData eventData) { down = true; Pressed?.Invoke(); }
    public void OnPointerUp(PointerEventData eventData) { if (down) { down = false; Released?.Invoke(); } }
    public void OnPointerExit(PointerEventData eventData) { if (down) { down = false; Released?.Invoke(); } }
}

/// <summary>
/// Ada arayüzü: üst bar (ANA ÜS, Akın/dalga, Et/Odun, kale canı), Akın seçimi, ATEŞ + OTO düğmeleri, öğretici ipucu ve sonuç.
/// Tamamı kodla kurulur (Reference UI dili).
/// </summary>
public sealed class IslandHud : MonoBehaviour
{
    private IslandSceneSetup setup;
    private IslandRaidManager raid;
    private IslandArcher archer;

    private RectTransform safe;
    private TextMeshProUGUI titleText, meatText, woodText, goldText, castleText, hintText, autoLabel, homeLabel, bannerText, mapLabel;
    private Image castleFill, hintBg, chargeRing, fireFace, cooldownFill, bannerBg;
    private Button mapButton;
    private readonly System.Collections.Generic.List<Image> edgeArrows = new System.Collections.Generic.List<Image>();
    private GameObject fireRoot, resultPanel, autoRoot, dock, castleBar, staffButton;
    private IslandRaidBoard board;
    private bool prepOpen;
    private TextMeshProUGUI akinDockLabel;
    private RadialDockMenu radial;
    private Button homeButton, autoButton, nameButton;
    private ResourceTopBar resourceBar;
    private int selectedAkin = 1;
    private float homeConfirmUntil;
    private TextMeshProUGUI resultTitle, resultBody;
    private Button resultAgain, resultNext, resultHome;
    private float toastUntil;
    private IslandBuildUi buildUi;
    private IslandPlacementController placement;
    private Image touchSurfaceImage;
    private bool placing;
    private Image raidBadgeBg;
    private TextMeshProUGUI raidBadgeText;
    private float nextRaidUiPoll;

    public static readonly Color Gold = ReferenceUiVisuals.Gold;

    public IslandBuildUi BuildUi => buildUi;
    public bool Placing => placing;

    public void Build(IslandSceneSetup owner, IslandRaidManager raidManager, IslandArcher islandArcher, Camera cam)
    {
        setup = owner;
        raid = raidManager;
        archer = islandArcher;
        var canvasObject = new GameObject("IslandCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        // Dünya dokunuşları (en arkada).
        var surface = ReferenceUiVisuals.Image(safe, "TouchSurface", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0f));
        surface.raycastTarget = true;
        touchSurfaceImage = surface;
        var touch = surface.gameObject.AddComponent<IslandTouchSurface>();
        touch.Tapped = screen => setup.HandleScreenTap(screen);

        BuildTopBar();
        BuildRaidBadge();
        BuildDock();
        BuildStaffButton();
        BuildPrepPanel();
        BuildBuildUi();
        BuildFireControls();
        BuildHint();
        BuildResult();
        selectedAkin = Mathf.Max(1, IslandSession.Save?.lastAkin ?? 1);
        SnapSelection();
        raid.StateChanged += _ => Refresh();
        raid.TutorialChanged += Refresh;
        Refresh();
        if (FeatureConfig.Instance.islandNaming) StartCoroutine(OfferNameSoon());
    }

    // ==================== KURULUM ====================

    private void BuildTopBar()
    {
        var bar = ReferenceUiVisuals.Image(safe, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), Vector2.zero,
            new Color(0.08f, 0.06f, 0.04f, 0.72f));
        if (FeatureConfig.Instance.sharedTopBar) BuildSharedTopBar(bar);
        else
        {
        homeButton = ReferenceUiVisuals.Button(bar.transform, "HomeButton", L.T("ANA ÜS", "BASE"), false,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -16), new Vector2(96, 16), 12);
        homeLabel = homeButton.GetComponentInChildren<TextMeshProUGUI>();
        homeButton.onClick.AddListener(OnHome);
        titleText = ReferenceUiVisuals.Text(bar.transform, "Title", "", 13, Gold, TextAlignmentOptions.Center,
            new Vector2(0.21f, 0), new Vector2(0.40f, 1), Vector2.zero, Vector2.zero, true);
        // Kaynaklar: Tiny Swords simgeleri (Et, Odun, Ana Üs altını) + sayı
        var catalog = IslandVisualCatalog.Instance;
        meatText = ResourceChip(bar.transform, 0, catalog != null ? catalog.meatIcon : null);
        woodText = ResourceChip(bar.transform, 1, catalog != null ? catalog.woodIcon : null);
        goldText = ResourceChip(bar.transform, 2, catalog != null ? catalog.Single("gold_icon") : null);
        }

        var barBg = ReferenceUiVisuals.Image(safe, "CastleBar", new Vector2(0.18f, 1), new Vector2(0.82f, 1),
            new Vector2(0, -74), new Vector2(0, -58), new Color(0.05f, 0.04f, 0.03f, 0.85f));
        castleBar = barBg.gameObject;
        castleFill = ReferenceUiVisuals.Image(barBg.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2),
            new Color(0.35f, 0.7f, 0.95f));
        castleFill.sprite = IslandArt.Square;
        castleFill.type = Image.Type.Filled;
        castleFill.fillMethod = Image.FillMethod.Horizontal;
        castleText = ReferenceUiVisuals.Text(barBg.transform, "Text", "", 11, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, true);
    }

    /// <summary>AŞAMA 3 §3A: Ana Üs'le aynı süslü çerçeve + ResourceTopBar çipleri; solda ANA ÜS, yanında ada adı (tek satır, dokununca değişir).</summary>
    private void BuildSharedTopBar(Image bar)
    {
        bar.color = new Color(0.07f, 0.05f, 0.04f, 0.97f);
        var strip = BaseHudRedesign.OrnateStrip(true);
        if (strip != null)
        {
            var ornament = ReferenceUiVisuals.Image(bar.transform, "Ornament", Vector2.zero, Vector2.one, new Vector2(-4f, -2f), new Vector2(4f, 2f), Color.white);
            ornament.sprite = strip;
        }
        homeButton = ReferenceUiVisuals.Button(bar.transform, "HomeButton", L.T("ANA ÜS", "BASE"), false,
            new Vector2(0.015f, 0.14f), new Vector2(0.165f, 0.86f), Vector2.zero, Vector2.zero, 11);
        homeLabel = homeButton.GetComponentInChildren<TextMeshProUGUI>();
        homeLabel.characterSpacing = 0.4f;
        homeLabel.fontSizeMin = 7f;
        homeLabel.rectTransform.offsetMin = new Vector2(3, 0);
        homeLabel.rectTransform.offsetMax = new Vector2(-3, 0);
        homeButton.onClick.AddListener(OnHome);
        // Ada adı: tek satır, otomatik küçülür, sığmazsa üç nokta; dokununca yeniden adlandırma penceresi.
        var nameRect = ReferenceUiVisuals.Rect(bar.transform, "IslandNameButton", new Vector2(0.17f, 0.1f), new Vector2(0.36f, 0.9f), Vector2.zero, Vector2.zero);
        var nameImage = nameRect.gameObject.AddComponent<Image>();
        nameImage.sprite = ReferenceUiVisuals.PillSprite();   // zifiri/süs üstünde okunur: çiplerle aynı koyu hap
        nameImage.type = Image.Type.Sliced;
        nameImage.color = Color.white;
        nameImage.raycastTarget = true;
        nameButton = nameRect.gameObject.AddComponent<Button>();
        nameButton.targetGraphic = nameImage;
        nameButton.transition = Selectable.Transition.None;
        nameButton.onClick.AddListener(OpenRename);
        titleText = ReferenceUiVisuals.Text(nameRect, "Title", "", 13, Gold, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 7f;
        titleText.fontSizeMax = 13f;
        resourceBar = ResourceTopBar.BuildIsland(bar.rectTransform, 0.365f, 0.875f);
    }

    /// <summary>Üst çubuktaki ada adına dokunma: yeniden adlandırma (bedel gösterilir). Akın/yerleştirme sırasında kapalı.</summary>
    public void OpenRename()
    {
        if (!FeatureConfig.Instance.islandNaming || raid.Phase != IslandRaidManager.State.Idle || placing) return;
        buildUi?.Close();
        prepOpen = false;
        IslandNameUi.Open(IslandSession.Save, IslandNameUi.Mode.Rename, () => Refresh());
    }

    /// <summary>Ada ilk açıldığında (ve eski kayıtta bir kez) ad sorusu/teklifi.</summary>
    private System.Collections.IEnumerator OfferNameSoon()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        while (raid != null && (raid.Phase != IslandRaidManager.State.Idle || placing)) yield return null;
        if (DevSettings.Enabled && !IslandNameUi.OfferInDev) yield break;
        var mode = IslandNameUi.OfferFor(IslandSession.Save);
        if (mode.HasValue && !IslandNameUi.IsOpen) IslandNameUi.Open(IslandSession.Save, mode.Value, () => Refresh());
    }

    // ==================== ZAMANLI BASKIN (Faz D) ====================

    /// <summary>Sağ üstte baskın rozeti: "Sakin · 3 sa 12 dk" / "Baskın geliyor · 24 dk" / "BASKIN! · savun". Dokununca (pencere açıksa) savunma başlar.</summary>
    private void BuildRaidBadge()
    {
        // 13 Kasım: kompakt hap (kum saati + "14sa 48dk", 104×26); eski uzun hap 188×32 idi ve adanın sağ üstünü örtüyordu.
        bool compact = FeatureConfig.Instance.raidPillCompact;
        raidBadgeBg = ReferenceUiVisuals.Image(safe, "RaidBadge", new Vector2(1, 1), new Vector2(1, 1),
            compact ? new Vector2(-CompactPillWidth - 6, -86) : new Vector2(-196, -90), compact ? new Vector2(-6, -60) : new Vector2(-8, -58),
            new Color(0.12f, 0.3f, 0.36f, 0.92f));
        raidBadgeBg.sprite = ReferenceUiVisuals.PillSprite();
        raidBadgeBg.type = Image.Type.Sliced;
        raidBadgeBg.raycastTarget = true;
        var button = raidBadgeBg.gameObject.AddComponent<Button>();
        button.targetGraphic = raidBadgeBg;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(OnRaidBadge);
        bool clock = FeatureConfig.Instance.raidPillClock || compact;
        if (clock)
        {
            var icon = compact
                ? ReferenceUiVisuals.Image(raidBadgeBg.transform, "ClockIcon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, -8), new Vector2(22, 8), Color.white)
                : ReferenceUiVisuals.Image(raidBadgeBg.transform, "ClockIcon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -9), new Vector2(26, 9), Color.white);
            icon.sprite = CampaignSceneSetup.Art("ui_icon_clock");
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            icon.raycastTarget = false;
        }
        raidBadgeText = ReferenceUiVisuals.Text(raidBadgeBg.transform, "Label", "", compact ? 11 : 12, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(compact ? 22 : clock ? 28 : 8, 0), new Vector2(compact ? -5 : -8, 0), true);
        raidBadgeText.textWrappingMode = TextWrappingModes.NoWrap;
        raidBadgeText.enableAutoSizing = true;
        raidBadgeText.fontSizeMin = 8f;
        raidBadgeText.fontSizeMax = compact ? 11f : 12f;
        raidBadgeText.raycastTarget = false;
        raidBadgeBg.gameObject.SetActive(false);
    }

    private static string Span(double seconds)
    {
        seconds = Math.Max(0d, seconds);
        long secs = Math.Max(1L, (long)Math.Ceiling(seconds)); if (secs < 60) return L.T($"{secs} sn", $"{secs}s");
        long minutes = (long)Math.Ceiling(seconds / 60d);
        long h = minutes / 60, m = minutes % 60;
        return h > 0 ? L.T($"{h} sa {m} dk", $"{h}h {m}m") : L.T($"{m} dk", $"{m}m");
    }

    private const float CompactPillWidth = 104f;

    /// <summary>
    /// Kompakt süre (3. oturum 2.3): 1 saat ve üstü "10sa 0dk", 1 saatin altında "45dk", 1 dakikanın altında "30sn"
    /// (EN "10h 0m" / "45m" / "30s"). Eskiden saat "s" ile yazılıyordu ("10s 0dk": saniye gibi okunuyordu).
    /// </summary>
    public static string ShortSpan(double seconds)
    {
        seconds = Math.Max(0d, seconds);
        long secs = Math.Max(1L, (long)Math.Ceiling(seconds)); if (secs < 60) return L.T($"{secs}sn", $"{secs}s");
        long minutes = (long)Math.Ceiling(seconds / 60d);
        long h = minutes / 60, m = minutes % 60;
        return h > 0 ? L.T($"{h}sa {m}dk", $"{h}h {m}m") : L.T($"{m}dk", $"{m}m");
    }

    /// <summary>Kompakt hap metni (FeatureConfig.raidPillCompact): sakin "14sa 48dk", uyarı "Geliyor 24dk", canlı "BASKIN 12dk".</summary>
    public static string RaidPillText(IslandSaveData save, long now)
    {
        if (!FeatureConfig.Instance.raidPillCompact) return RaidBadgeText(save, now);
        if (save == null || save.nextRaidTicks <= 0) return "";
        string span = ShortSpan(IslandRaidScheduler.SecondsLeft(save, now));
        switch (IslandRaidScheduler.PhaseAt(save, now))
        {
            case IslandRaidScheduler.Phase.Live: return L.T($"BASKIN {span}", $"RAID {span}");
            case IslandRaidScheduler.Phase.Warning: return L.T($"Geliyor {span}", $"Soon {span}");
            default: return span;
        }
    }

    /// <summary>Rozet metni (uzun biçim; testler ve dokunma açıklaması): baskın yoksa boş.</summary>
    public static string RaidBadgeText(IslandSaveData save, long now)
    {
        if (save == null || save.nextRaidTicks <= 0) return "";
        double left = IslandRaidScheduler.SecondsLeft(save, now);
        switch (IslandRaidScheduler.PhaseAt(save, now))
        {
            case IslandRaidScheduler.Phase.Live: return L.T($"BASKIN! · {Span(left)}", $"RAID! · {Span(left)}");
            case IslandRaidScheduler.Phase.Warning: return L.T($"Baskın geliyor · {Span(left)}", $"Raid incoming · {Span(left)}");
            default:
                return FeatureConfig.Instance.raidPillClock ? L.T($"Sonraki akın: {Span(left)}", $"Next raid: {Span(left)}")
                    : L.T($"Sakin · {Span(left)}", $"Calm · {Span(left)}");
        }
    }

    private void RefreshRaidUi()
    {
        var save = IslandSession.Save;
        long now = IslandRepair.Now;
        string text = RaidPillText(save, now);
        bool idle = raid.Phase == IslandRaidManager.State.Idle;
        bool show = text.Length > 0 && idle && !placing;
        if (raidBadgeBg.gameObject.activeSelf != show) raidBadgeBg.gameObject.SetActive(show);
        if (show)
        {
            raidBadgeText.text = text;
            var phase = IslandRaidScheduler.PhaseAt(save, now);
            if (FeatureConfig.Instance.raidPillCompact)
            {
                // Uyarı/canlı metni daha uzun: hap sola doğru genişler (sağ kenar sabit)
                float width = phase == IslandRaidScheduler.Phase.None ? CompactPillWidth : CompactPillWidth + 30f;
                raidBadgeBg.rectTransform.offsetMin = new Vector2(-width - 6f, raidBadgeBg.rectTransform.offsetMin.y);
            }
            raidBadgeBg.color = phase == IslandRaidScheduler.Phase.Live ? new Color(0.78f, 0.2f, 0.16f, 0.95f)
                : phase == IslandRaidScheduler.Phase.Warning ? new Color(0.72f, 0.42f, 0.12f, 0.95f) : new Color(0.12f, 0.3f, 0.36f, 0.92f);
        }
        if (board != null && board.Visible) board.SetRaid(RaidBoardInfo(save, now));
        if (idle && save != null && !string.IsNullOrEmpty(save.raidReportKind) && !prepOpen)
        {
            // Pencere ada açıkken ya da çevrimdışı bitti: rapor satırı bir kez gösterilir
            string line = IslandRaidScheduler.TakeReport(save);
            if (line.Length > 0) { Toast(line); toastUntil = Time.unscaledTime + 6f; GameManager.Instance?.SaveGame(); }
        }
    }

    private IslandRaidBoard.RaidInfo RaidBoardInfo(IslandSaveData save, long now)
    {
        var phase = IslandRaidScheduler.PhaseAt(save, now);
        if (save == null || phase == IslandRaidScheduler.Phase.None) return default;
        double left = IslandRaidScheduler.SecondsLeft(save, now);
        string beach = IslandRaidScheduler.BeachName(save.raidBeach);
        int power = Progression.Island.RaidPower(save.highestAkin);
        bool live = phase == IslandRaidScheduler.Phase.Live;
        return new IslandRaidBoard.RaidInfo
        {
            active = true,
            live = live,
            title = live ? L.T($"BASKIN! {beach} kumsalı", $"RAID! Beach {beach}") : L.T($"Baskın geliyor · {beach} kumsalı", $"Raid incoming · Beach {beach}"),
            detail = live ? L.T($"Güç {power} · 2 dalga · {Span(left)} içinde savun, yoksa kuleler çözer", $"Power {power} · 2 waves · defend within {Span(left)} or the towers decide")
                : L.T($"Güç {power} · {Span(left)} sonra çıkar · kuleyi {beach} kumsalına bakan yara taşı", $"Power {power} · lands in {Span(left)} · move a tower to cover beach {beach}"),
        };
    }

    private void OnRaidBadge()
    {
        var save = IslandSession.Save;
        long now = IslandRepair.Now;
        var phase = IslandRaidScheduler.PhaseAt(save, now);
        if (phase == IslandRaidScheduler.Phase.Live) { StartRaidDefense(); return; }
        if (phase == IslandRaidScheduler.Phase.Warning)
            Toast(L.T($"Korsanlar {Span(IslandRaidScheduler.SecondsLeft(save, now))} sonra {IslandRaidScheduler.BeachName(save.raidBeach)} kumsalına çıkar.",
                $"Pirates land on beach {IslandRaidScheduler.BeachName(save.raidBeach)} in {Span(IslandRaidScheduler.SecondsLeft(save, now))}."));
        else if (save != null && save.nextRaidTicks > 0)
        {
            // 13 Kasım: kompakt hapın açıklaması dokununca
            string left = Span(IslandRaidScheduler.SecondsLeft(save, now));
            Toast(L.T($"Sonraki akına kalan süre: {left}. Korsanlar 30 dk önce görünür.", $"Time until the next raid: {left}. Pirates show up 30 minutes early."));
            toastUntil = Time.unscaledTime + 3.5f;
        }
        else Toast(L.T("Ada sakin. Baskın 8 saatte bir gelir; 30 dk önce haber verilir.", "The isle is calm. Raids come every ~8 hours with 30 minutes' warning."));
    }

    /// <summary>Canlı baskın savunmasını başlatır (rozet, tekne ya da Akın Panosu kırmızı kartı). Pencere kapalıysa bilgi verir.</summary>
    public void StartRaidDefense()
    {
        if (raid.Phase != IslandRaidManager.State.Idle || placing) return;
        if (IslandRaidScheduler.PhaseAt(IslandSession.Save, IslandRepair.Now) != IslandRaidScheduler.Phase.Live) { OnRaidBadge(); return; }
        buildUi?.Close();
        prepOpen = false;
        if (raid.StartRaid()) { Toast(L.T("BASKIN! Kumsalı koru.", "RAID! Hold the beach.")); Refresh(); }
    }

    /// <summary>Alt dock (görünüm modu): AKIN düğmesi. Akın yalnız buradan başlar; ada girişte üs görünümü açılır (v2 §3).</summary>
    private TextMeshProUGUI ResourceChip(Transform bar, int index, Sprite icon)
    {
        float x0 = 0.40f + index * 0.15f;   // 3 çip × 0,15: dört basamaklı sayı bir sonraki simgeye binmez
        var group = ReferenceUiVisuals.Image(bar, "Res" + index, new Vector2(x0, 0), new Vector2(x0 + 0.15f, 1),
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
        group.raycastTarget = false;
        var image = ReferenceUiVisuals.Image(group.transform, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(0, -12), new Vector2(24, 12), Color.white);
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var value = ReferenceUiVisuals.Text(group.transform, "Value", "0", 14, ReferenceUiVisuals.Light, TextAlignmentOptions.Left,
            new Vector2(0, 0), Vector2.one, new Vector2(26, 0), new Vector2(-2, 0), true);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.enableAutoSizing = true;   // 5 basamakta küçülür, taşmaz
        value.fontSizeMin = 9f;
        value.fontSizeMax = 14f;
        return value;
    }

    private void BuildDock()
    {
        var cfg = FeatureConfig.Instance;
        var root = new GameObject("Dock", typeof(RectTransform));
        root.transform.SetParent(safe, false);
        var rt = (RectTransform)root.transform;
        dock = root;
        if (cfg.islandRadialMenu)
        {
            // 13 Kasım: Ana Üs'ün radyal menü bileşeni, adaya özgü öğelerle (İNŞA ET · AKIN · KADRO). Kap tüm güvenli alanı
            // kaplar (ana düğme alt-ortaya bağlanır); kap gizlenince menü topluca gizlenir. Kap dokunuş yakalamaz.
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            BuildRadialMenu(rt);
            return;
        }
        if (cfg.islandCompactDock)
        {
            // AŞAMA 3 §3D: iki küçük ikonlu düğme alt köşelerde (Ana Üs dock ailesi: koyu İNŞA ET, altın AKIN); ortası boş, ada görünür.
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
            rt.offsetMin = new Vector2(0, 12); rt.offsetMax = new Vector2(0, 12 + cfg.compactDockHeight);
            var build = CompactDockButton(rt, "BuildButton", L.T("İNŞA ET", "BUILD"), false, "ui_icon_hammer", true, cfg);
            build.onClick.AddListener(OpenBuildMenu);
            var akin = CompactDockButton(rt, "AkinButton", L.T("AKIN", "RAID"), true, "ui_icon_sword", false, cfg);
            akinDockLabel = akin.GetComponentInChildren<TextMeshProUGUI>();
            akin.onClick.AddListener(() => { buildUi?.Close(); prepOpen = true; Refresh(); });
            return;
        }
        rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(-170, 26); rt.offsetMax = new Vector2(170, 92);
        // Alt dock (v2 §3): İNŞA ET · AKIN (KAPTAN düğmesi Faz E'de gelir)
        var buildOld = ReferenceUiVisuals.Button(rt, "BuildButton", L.T("İNŞA ET", "BUILD"), false, Vector2.zero, new Vector2(0.48f, 1),
            Vector2.zero, Vector2.zero, 22);
        buildOld.onClick.AddListener(OpenBuildMenu);
        var button = ReferenceUiVisuals.Button(rt, "AkinButton", L.T("AKIN", "RAID"), true, new Vector2(0.52f, 0), Vector2.one,
            Vector2.zero, Vector2.zero, 24);
        akinDockLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(() => { buildUi?.Close(); prepOpen = true; Refresh(); });
    }

    /// <summary>Ada radyal menüsü (Ana Üs bileşeni): sol İNŞA ET, ortada vurgulu AKIN (Akın numarası pulu), sağda KADRO.</summary>
    private void BuildRadialMenu(RectTransform container)
    {
        radial = RadialDockMenu.Create(container, RadialDockMenu.IslandPrefKey);
        radial.Register(new RadialItem
        {
            id = "build", label = () => L.T("İNŞA ET", "BUILD"), offset = new Vector2(-96f, 54f),
            iconProvider = () => CampaignSceneSetup.Art("ui_icon_hammer"),
            onClick = OpenBuildMenu,
        });
        radial.Register(new RadialItem
        {
            id = "raid", label = () => L.T("AKIN", "RAID"), offset = new Vector2(0f, 122f), emphasised = true,
            iconProvider = () => CampaignSceneSetup.Art("ui_icon_sword"),
            infoBadgeProvider = () => DockAkin().ToString(),
            onClick = OpenRaidBoard,
        });
        radial.Register(new RadialItem
        {
            id = "staff", label = () => L.T("KADRO", "STAFF"), offset = new Vector2(96f, 54f),
            iconProvider = () => SpriteManager.Instance != null ? SpriteManager.Instance.GetSprite("ui_builder_avatar") : null,
            faceColor = new Color(0.12f, 0.30f, 0.36f, 1f),   // deniz (ada ailesi)
            onClick = OpenStaff,
        });
        radial.FinishSetup();
    }

    /// <summary>Alt menüde gösterilen Akın numarası (seçili, kapıdan geçebilen en yüksek).</summary>
    private int DockAkin() => Mathf.Min(MaxSelectable(), Mathf.Max(1, selectedAkin));

    private void OpenRaidBoard()
    {
        if (raid.Phase != IslandRaidManager.State.Idle || placing) return;
        buildUi?.Close();
        prepOpen = true;
        Refresh();
    }

    /// <summary>Ada radyal menüsü (testler).</summary>
    public RadialDockMenu Radial => radial;

    private static Button CompactDockButton(RectTransform parent, string name, string label, bool gold, string art, bool left, FeatureConfig cfg)
    {
        float w = cfg.compactDockWidth;
        var button = ReferenceUiVisuals.Button(parent, name, label, gold,
            left ? new Vector2(0, 0) : new Vector2(1, 0), left ? new Vector2(0, 1) : new Vector2(1, 1),
            left ? new Vector2(10, 0) : new Vector2(-10 - w, 0), left ? new Vector2(10 + w, 0) : new Vector2(-10, 0), 15);
        var text = button.GetComponentInChildren<TextMeshProUGUI>();
        text.rectTransform.offsetMin = new Vector2(36, 0);
        text.rectTransform.offsetMax = new Vector2(-8, 0);
        text.characterSpacing = 0.6f;
        text.fontSizeMin = 9f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        var icon = ReferenceUiVisuals.Image(button.transform, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -15), new Vector2(38, 15), Color.white);
        icon.sprite = CampaignSceneSetup.Art(art);
        icon.preserveAspect = true;
        icon.enabled = icon.sprite != null;
        return button;
    }

    /// <summary>AŞAMA 2 §2: sol kenarda, üst çubuğun altında küçük yuvarlak KADRO düğmesi (adayı örtmez); kadro panelini açar.</summary>
    private void BuildStaffButton()
    {
        if (FeatureConfig.Instance.islandRadialMenu) return;   // KADRO radyal menüde (sol üstteki düğme adacığın üstüne biniyordu)
        var bg = ReferenceUiVisuals.Image(safe, "StaffButton", new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -134), new Vector2(68, -74),
            new Color(0.12f, 0.3f, 0.36f, 0.92f));
        bg.sprite = ReferenceUiVisuals.SolidPillSprite();
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;
        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(OpenStaff);
        var icon = ReferenceUiVisuals.Image(bg.transform, "Icon", Vector2.zero, Vector2.one, new Vector2(8, 14), new Vector2(-8, -4), Color.white);
        icon.sprite = SpriteManager.Instance != null ? SpriteManager.Instance.GetSprite("ui_builder_avatar") : null;
        icon.preserveAspect = true;
        icon.enabled = icon.sprite != null;
        var label = ReferenceUiVisuals.Text(bg.transform, "Label", L.T("KADRO", "STAFF"), 10, Color.white, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 16), true);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        staffButton = bg.gameObject;
    }

    public void OpenStaff()
    {
        if (raid.Phase != IslandRaidManager.State.Idle || placing) return;
        prepOpen = false;
        board.SetVisible(false);
        buildUi.ShowStaff();
    }

    // ==================== İNŞA (Faz B) ====================

    private void BuildBuildUi()
    {
        buildUi = new IslandBuildUi();
        buildUi.Build(safe);
        buildUi.Toast = Toast;
        buildUi.PlaceRequested = kind => StartPlacement(IslandPlacementHost.ForNew(kind));
        buildUi.MoveRequested = id => StartPlacement(IslandPlacementHost.ForMove(id));
        buildUi.AkinRequested = () => { prepOpen = true; Refresh(); };
        placement = setup.gameObject.GetComponent<IslandPlacementController>();
        if (placement == null) placement = setup.gameObject.AddComponent<IslandPlacementController>();   // Unity sahte-null: ?? kullanılmaz
        var village = IslandVillage.Instance;
        if (village != null)
        {
            village.Toast = Toast;
            village.BuildingTapped = id => { buildUi.ShowBuilding(id); FocusBuilding(id); };
            village.CastleTapped = () => { buildUi.ShowCastle(); setup.CameraController.FocusUpper(IslandLayout.CastleCenter); };
            village.ZoneTapped = zone => { buildUi.ShowZone(zone); setup.CameraController.FocusUpper(setup.ZoneCenter(zone)); };
            village.TreeTapped = cell => buildUi.ShowTree(cell);   // AŞAMA 2 §3
            village.NatureTapped = item => buildUi.ShowNature(item.kind, item.cell);   // 4. oturum (1.4)
        }
    }

    public void OpenBuildMenu()
    {
        if (raid.Phase != IslandRaidManager.State.Idle || placing) return;
        prepOpen = false;
        board.SetVisible(false);
        buildUi.ShowMenu();
    }

    public void CloseBuildUi() => buildUi?.Close();

    /// <summary>Bina paneli alttan açılır: seçilen bina panelin üstünde görünsün diye kamera kayar.</summary>
    public void FocusBuilding(string id)
    {
        var village = IslandVillage.Instance;
        var entry = IslandEconomy.Find(id);
        if (village == null || entry == null || setup.CameraController == null) return;
        var spot = IslandVillage.SpotFor(entry);
        setup.CameraController.FocusUpper(spot.FeetWorld + Vector2.up * 1f);
    }

    /// <summary>Yerleştirmeyi başlatır (yeni bina ya da taşıma); hayalet, YERLEŞTİR / İPTAL çubuğu açılır.</summary>
    public void StartPlacement(IPlacementHost host)
    {
        if (raid.Phase != IslandRaidManager.State.Idle) return;
        buildUi.Close();
        placement.Begin(host, setup.WorldCamera, setup.CameraController, safe, SetPlacementUi, Toast);
    }

    private void SetPlacementUi(bool on)
    {
        placing = on;
        if (touchSurfaceImage != null) touchSurfaceImage.raycastTarget = !on;   // hayalet sürüklemesi dünya dokunuşuna karışmasın
        if (dock != null) dock.SetActive(!on && raid.Phase == IslandRaidManager.State.Idle && !prepOpen);
    }

    /// <summary>Akın Panosu (§3): AKIN düğmesi açar; kartlar merdiveni gösterir, SAVUNMAYA BAŞLA savunmayı başlatır.</summary>
    private void BuildPrepPanel()
    {
        board = new IslandRaidBoard();
        board.Build(safe);
        board.Selected = n => { selectedAkin = Mathf.Clamp(n, 1, Mathf.Max(1, MaxSelectable() + 3)); Refresh(); };
        board.StartPressed = OnStart;
        board.RaidStartPressed = StartRaidDefense;
        board.ClosePressed = () => { prepOpen = false; Refresh(); };
    }

    /// <summary>Test/ekran görüntüsü: panoyu açar ve N'yi seçer.</summary>
    public void OpenBoard(int akin) { selectedAkin = Mathf.Max(1, akin); prepOpen = true; Refresh(); }
    public bool BoardVisible => board != null && board.Visible;

    private void BuildFireControls()
    {
        fireRoot = new GameObject("FireControls", typeof(RectTransform));
        fireRoot.transform.SetParent(safe, false);
        var rootRt = (RectTransform)fireRoot.transform;
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one; rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        // ATEŞ / OTO / HARİTA: Tiny Swords UI düğme sprite'ları (kodla çizilmiş daire yok); dolum/şarj sprite üstünde dolgu
        var round = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("ui_round_red") : null;
        var fire = ReferenceUiVisuals.Image(rootRt, "FireButton", new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-176, 30), new Vector2(-22, 184), Color.white);
        fire.sprite = round;
        fire.raycastTarget = true;
        fireFace = fire;
        var hold = fire.gameObject.AddComponent<IslandHoldButton>();
        hold.Pressed = () => { if (archer != null) archer.PressFire(); fire.rectTransform.localScale = Vector3.one * 0.94f; };
        hold.Released = () => { if (archer != null) archer.ReleaseFire(); fire.rectTransform.localScale = Vector3.one; };
        cooldownFill = RadialOverlay(fire.transform, "Cooldown", round, new Color(0f, 0f, 0f, 0.5f));
        chargeRing = RadialOverlay(fire.transform, "Charge", round, new Color(1f, 0.85f, 0.3f, 0.65f));
        ReferenceUiVisuals.Text(fire.transform, "Label", L.T("ATEŞ", "FIRE"), 26, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);

        BuildAbilityButtons(rootRt);
        autoButton = SquareButton(rootRt, "AutoButton", L.T("OTO", "AUTO"), new Vector2(-76, 200), out autoLabel);
        autoRoot = autoButton.gameObject;
        autoButton.onClick.AddListener(OnAutoToggle);
        mapButton = SquareButton(rootRt, "MapButton", L.T("HARİTA", "MAP"), new Vector2(-76, 262), out mapLabel);
        mapButton.onClick.AddListener(() => { setup.CameraController.ToggleFullFrame(); });

        // Kısa bildirimler (okçu düştü, atış yeri kilidi...): Tiny Swords kurdele üstünde, otomatik kapanır
        bannerBg = ReferenceUiVisuals.Image(safe, "Banner", new Vector2(0.1f, 1), new Vector2(0.9f, 1),
            new Vector2(0, -150), new Vector2(0, -102), Color.white);
        var ribbon = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("ui_ribbon") : null;
        bannerBg.sprite = ribbon;
        if (ribbon != null) { bannerBg.type = Image.Type.Sliced; bannerBg.pixelsPerUnitMultiplier = 3f; }
        bannerText = ReferenceUiVisuals.Text(bannerBg.transform, "Text", "", 15, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(44, 6), new Vector2(-44, -6), true);
        bannerBg.gameObject.SetActive(false);
    }

    // ---- Faz E2 + 3. oturum 3.3: Okçu yetenekleri (Ok Yağmuru Sv 2 · Ateşli Ok Sv 4 · Ağ Oku Sv 6) ----
    // ATEŞ sağ-altta çapadır (merkez -99,107); yetenekler onun sol-üstünde, başparmağın yetiştiği 112 birimlik yayda (56 birim, altın halkalı yuvarlak düğme).
    // Yardımcı düğmeler (OTO, HARİTA) sağ kenarda, yayın dışında ayrı küçük bir sütundadır.
    private readonly Image[] abilityFace = new Image[3], abilityFill = new Image[3];
    private readonly TextMeshProUGUI[] abilityLabel = new TextMeshProUGUI[3], abilityTime = new TextMeshProUGUI[3];
    private GameObject abilityRoot;
    public const float AbilityButtonSize = 56f, AbilityArcRadius = 112f, UtilityButtonSize = 52f;
    private static readonly Vector2 FireCenter = new Vector2(-99f, 107f);
    private static readonly float[] AbilityAngles = { 170f, 135f, 100f };

    private void BuildAbilityButtons(RectTransform rootRt)
    {
        abilityRoot = new GameObject("AbilityButtons", typeof(RectTransform));
        abilityRoot.transform.SetParent(rootRt, false);
        var rt = (RectTransform)abilityRoot.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var catalog = IslandVisualCatalog.Instance;
        var round = catalog != null ? catalog.Single("ui_round_blue") : null;
        var ringSprite = IslandAbilityArt.Ring();
        for (int i = 0; i < 3; i++)
        {
            var kind = (IslandAbilities.Kind)i;
            float rad = AbilityAngles[i] * Mathf.Deg2Rad;
            Vector2 c = FireCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * AbilityArcRadius;
            float h = AbilityButtonSize * 0.5f;
            var face = ReferenceUiVisuals.Image(rt, "Ability" + kind, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(c.x - h, c.y - h), new Vector2(c.x + h, c.y + h), Color.white);
            face.sprite = round;
            face.raycastTarget = true;
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            face.gameObject.AddComponent<ReferenceButtonPress>();
            button.onClick.AddListener(() => OnAbilityPressed(kind));
            abilityFace[i] = face;
            // Simge (üst), kısa ad (alt)
            var icon = ReferenceUiVisuals.Image(face.transform, "Icon", new Vector2(0.2f, 0.36f), new Vector2(0.8f, 0.9f), Vector2.zero, Vector2.zero, Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            if (kind == IslandAbilities.Kind.Volley)
            {
                icon.sprite = catalog != null ? catalog.Single("arrow") : null;
                icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);   // yağmur: aşağı inen ok
            }
            else if (kind == IslandAbilities.Kind.FireArrow)
            {
                icon.sprite = catalog != null ? catalog.Single("fire_1") : null;
                icon.color = new Color(1f, 0.7f, 0.35f);
            }
            else icon.sprite = IslandAbilityArt.Net();
            abilityFill[i] = RadialOverlay(face.transform, "Cooldown", round, new Color(0f, 0f, 0f, 0.6f));
            abilityLabel[i] = ReferenceUiVisuals.Text(face.transform, "Label", AbilityShortName(i), 9, Color.white, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 0.38f), new Vector2(6, 3), new Vector2(-6, 0), true);   // dairenin içine sığsın
            abilityLabel[i].enableAutoSizing = true;
            abilityLabel[i].fontSizeMin = 5; abilityLabel[i].fontSizeMax = 9;
            abilityLabel[i].raycastTarget = false;
            // Kalan saniye: dolum sürerken dairenin ortasında iri sayı
            abilityTime[i] = ReferenceUiVisuals.Text(face.transform, "Seconds", "", 20, Color.white, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            abilityTime[i].raycastTarget = false;
            // Altın halka (ATEŞ ile aynı düğme ailesi)
            var gold = ReferenceUiVisuals.Image(face.transform, "GoldRing", Vector2.zero, Vector2.one, new Vector2(-1, -1), new Vector2(1, 1), new Color(0.98f, 0.8f, 0.3f, 0.95f));
            gold.sprite = ringSprite;
            gold.raycastTarget = false;
        }
    }

    private void OnAbilityPressed(IslandAbilities.Kind kind)
    {
        var abilities = IslandAbilities.Instance;
        if (abilities == null) return;
        if (kind == IslandAbilities.Kind.Volley)
        {
            // Yağmur: ilk basış nişan halkasını açar, ikinci basış ateşler
            if (abilities.Aiming) abilities.ConfirmAim();
            else if (!abilities.BeginAim()) { if (abilities.Unlocked(kind)) return; ToastLocked(kind); }
            else Toast(L.T("Haritaya dokunup yeri seç, sonra YAĞMUR'a tekrar bas", "Tap the map to pick the spot, then press RAIN again"), 3f);
            return;
        }
        if (!abilities.Unlocked(kind)) { ToastLocked(kind); return; }
        if (kind == IslandAbilities.Kind.FireArrow) { abilities.Use(kind); return; }
        // Ağ: şu anki hedefe atılır; hedef yoksa bekleme harcanmaz
        if (!abilities.Use(kind) && abilities.CanReady(kind)) Toast(L.T("Menzilde hedef yok.", "No target in range."));
    }

    private void ToastLocked(IslandAbilities.Kind kind) =>
        Toast(L.T($"{IslandAbilities.NameOf(kind)}: Okçu Sv {IslandAbilities.UnlockLevel(kind)}'de açılır.", $"{IslandAbilities.NameOf(kind)}: unlocks at Archer Lv {IslandAbilities.UnlockLevel(kind)}."));

    private static string AbilityShortName(int i) =>
        i == 0 ? L.T("YAĞMUR", "RAIN") : i == 1 ? L.T("ATEŞ OK", "FLAME") : L.T("AĞ", "NET");

    private void RefreshAbilities()
    {
        var abilities = IslandAbilities.Instance;
        for (int i = 0; i < 3; i++)
        {
            if (abilityFace[i] == null) continue;
            var kind = (IslandAbilities.Kind)i;
            bool unlocked = abilities != null && abilities.Unlocked(kind);
            abilityFill[i].fillAmount = unlocked ? abilities.Remaining01(kind) : 1f;
            bool aiming = unlocked && kind == IslandAbilities.Kind.Volley && abilities.Aiming;
            abilityFace[i].color = !unlocked ? new Color(0.55f, 0.55f, 0.6f)
                : aiming ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f))
                : kind == IslandAbilities.Kind.FireArrow && abilities.FireShotsLeft > 0 ? new Color(1f, 0.7f, 0.4f) : Color.white;
            string name = AbilityShortName(i);
            abilityLabel[i].text = !unlocked ? L.T("Sv", "Lv") + IslandAbilities.UnlockLevel(kind)
                : aiming ? L.T("ATEŞ!", "GO!")
                : kind == IslandAbilities.Kind.FireArrow && abilities.FireShotsLeft > 0 ? "×" + abilities.FireShotsLeft : name;
            float left = unlocked ? abilities.RemainingSeconds(kind) : 0f;
            abilityTime[i].text = left > 0.05f ? Mathf.CeilToInt(left).ToString() : "";
        }
    }

    private static Image RadialOverlay(Transform parent, string name, Sprite sprite, Color color)
    {
        var overlay = ReferenceUiVisuals.Image(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
        overlay.sprite = sprite;
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;
        return overlay;
    }

    private static Button SquareButton(RectTransform parent, string name, string label, Vector2 corner, out TextMeshProUGUI text)
    {
        var image = ReferenceUiVisuals.Image(parent, name, new Vector2(1, 0), new Vector2(1, 0), corner, corner + new Vector2(UtilityButtonSize, UtilityButtonSize), Color.white);
        image.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("ui_square_blue") : null;
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        image.gameObject.AddComponent<ReferenceButtonPress>();
        text = ReferenceUiVisuals.Text(image.transform, "Label", label, 12, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(5, 6), new Vector2(-5, -8), true);
        text.textWrappingMode = TextWrappingModes.Normal;
        // Düğme kenarına taşmasın: uzun etiket ("OTO KAPALI", "HARİTA") küçülür.
        text.enableAutoSizing = true;
        text.fontSizeMin = 6f;
        text.fontSizeMax = 11f;
        return button;
    }

    /// <summary>Ekran dışındaki tekne/düşmanlar için kenar okları (takip kamerası).</summary>
    private void UpdateEdgeArrows()
    {
        var camera = setup.WorldCamera;
        bool show = raid.Phase == IslandRaidManager.State.Running && setup.CameraController != null && !setup.CameraController.FullFrame && camera != null;
        var targets = show ? IslandTarget.Collect() : new System.Collections.Generic.List<IslandTarget>();
        var offscreen = new System.Collections.Generic.List<(float d, Vector2 vp, bool boat)>();
        foreach (var target in targets)
        {
            Vector3 vp = camera.WorldToViewportPoint(target.Position);
            if (vp.x >= 0.03f && vp.x <= 0.97f && vp.y >= 0.1f && vp.y <= 0.93f) continue;
            offscreen.Add((Vector2.Distance(target.Position, camera.transform.position), new Vector2(vp.x, vp.y), target.IsBoat));
        }
        offscreen.Sort((x, y) => x.d.CompareTo(y.d));
        int count = Mathf.Min(offscreen.Count, 6);
        var sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("arrow") : null;
        while (edgeArrows.Count < count)
        {
            var arrow = ReferenceUiVisuals.Image(safe, "EdgeArrow", Vector2.zero, Vector2.zero, new Vector2(-22, -22), new Vector2(22, 22), Color.white);
            arrow.sprite = sprite;
            arrow.preserveAspect = true;
            edgeArrows.Add(arrow);
        }
        for (int i = 0; i < edgeArrows.Count; i++)
        {
            var arrow = edgeArrows[i];
            if (i >= count) { arrow.gameObject.SetActive(false); continue; }
            arrow.gameObject.SetActive(true);
            Vector2 vp = offscreen[i].vp;
            Vector2 dir = (vp - new Vector2(0.5f, 0.5f)).normalized;
            Vector2 pos = new Vector2(Mathf.Clamp(vp.x, 0.06f, 0.94f), Mathf.Clamp(vp.y, 0.14f, 0.9f));
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = pos;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            arrow.color = offscreen[i].boat ? new Color(1f, 0.75f, 0.3f, 0.95f) : new Color(1f, 0.3f, 0.25f, 0.95f);
        }
    }

    private void BuildHint()
    {
        hintBg = ReferenceUiVisuals.Image(safe, "HintBg", new Vector2(0.05f, 1), new Vector2(0.95f, 1),
            new Vector2(0, -122), new Vector2(0, -84), new Color(0.08f, 0.06f, 0.04f, 0.82f));
        hintBg.sprite = ReferenceUiVisuals.PillSprite();
        hintBg.type = Image.Type.Sliced;
        hintText = ReferenceUiVisuals.Text(hintBg.transform, "Hint", "", 15, Gold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0), false, true);
    }

    private void BuildResult()
    {
        var dim = ReferenceUiVisuals.Image(safe, "ResultPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        resultPanel = dim.gameObject;
        var center = new Vector2(0.5f, 0.5f);
        var card = ReferenceUiVisuals.Image(dim.transform, "Card", center, center, new Vector2(-165, -200), new Vector2(165, 200),
            new Color(0.12f, 0.09f, 0.06f, 0.97f));
        card.sprite = ReferenceUiVisuals.PillSprite();
        card.type = Image.Type.Sliced;
        resultTitle = ReferenceUiVisuals.Text(card.transform, "Title", "", 28, Gold, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(10, -64), new Vector2(-10, -14), true);
        resultBody = ReferenceUiVisuals.Text(card.transform, "Body", "", 15, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(18, 130), new Vector2(-18, -74), false, true);
        resultNext = ReferenceUiVisuals.Button(card.transform, "Next", L.T("SONRAKİ AKIN", "NEXT RAID"), true,
            new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0, 76), new Vector2(0, 118), 16);
        resultAgain = ReferenceUiVisuals.Button(card.transform, "Again", L.T("TEKRAR", "AGAIN"), false,
            new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0, 42), new Vector2(0, 72), 14);
        // 13 Kasım: Akın adadan başlar → sonuç ekranı adaya dönmeyi teklif eder (Ana Üs'e üst çubuktaki ANA ÜS ile gidilir).
        bool toIsland = FeatureConfig.Instance.raidResultReturnToIsland;
        resultHome = ReferenceUiVisuals.Button(card.transform, toIsland ? "ReturnIsland" : "Home", toIsland ? L.T("ADAYA DÖN", "BACK TO ISLE") : L.T("ANA ÜS", "BASE"), false,
            new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0, 8), new Vector2(0, 38), 14);
        resultNext.onClick.AddListener(() => { raid.ReturnToIdle(); prepOpen = true; selectedAkin = Mathf.Min(MaxSelectable(), raid.Akin + 1); Refresh(); });
        resultAgain.onClick.AddListener(() => { raid.ReturnToIdle(); prepOpen = true; selectedAkin = raid.Akin; Refresh(); });
        if (toIsland) resultHome.onClick.AddListener(ReturnToIsland);
        else resultHome.onClick.AddListener(GoHome);
        resultPanel.SetActive(false);
    }

    // ==================== DAVRANIŞ ====================

    private int MaxSelectable()
    {
        var save = IslandSession.Save;
        int highest = save != null ? save.highestAkin : 0;
        return Mathf.Min(highest + 1, Progression.Island.MaxAkin(IslandSession.KeepLevel()));
    }

    private void SnapSelection()
    {
        // Kapıdan geçilmeyen Akın seçili kalmasın.
        selectedAkin = Mathf.Clamp(selectedAkin, 1, Mathf.Max(1, MaxSelectable()));
    }

    private void OnStart()
    {
        if (!raid.CanStart(selectedAkin))
        {
            Toast(GateText(selectedAkin));
            return;
        }
        prepOpen = false;
        raid.StartAkin(selectedAkin);
        Refresh();
    }

    private void OnAutoToggle()
    {
        int keep = IslandSession.KeepLevel();
        if (!IslandSession.AutoFireUnlocked(keep))
        {
            Toast(L.T($"Oto-ateş: Kıyı Kalesi {Progression.Island.AutoFireUnlockKeepLevel}'te açılır.",
                $"Auto-fire unlocks at Keep {Progression.Island.AutoFireUnlockKeepLevel}."));
            return;
        }
        archer.AutoFire = !archer.AutoFire;
        if (IslandSession.Save != null) IslandSession.Save.autoFire = archer.AutoFire;
        GameManager.Instance?.SaveGame();
        Refresh();
    }

    private void OnHome()
    {
        if (raid.Phase != IslandRaidManager.State.Running) { GoHome(); return; }
        if (Time.unscaledTime < homeConfirmUntil) { raid.Abandon(); GoHome(); return; }
        homeConfirmUntil = Time.unscaledTime + 3f;
        Toast(L.T("Akın yarıda kalır. Çıkmak için tekrar dokun.", "The raid will be abandoned. Tap again to leave."));
    }

    /// <summary>
    /// Sonuç ekranından ada (üs) görünümüne dönüş: bulutlar ekranı örter, savaş temizlenir ve kamera üs kadrajına döner,
    /// sonra bulutlar açılır. Bulut geçişi kapalıysa ya da başka bir geçiş sürüyorsa doğrudan döner.
    /// </summary>
    public void ReturnToIsland()
    {
        if (raid.Phase != IslandRaidManager.State.Won && raid.Phase != IslandRaidManager.State.Lost) return;
        void Back()
        {
            raid.ReturnToIdle();
            prepOpen = false;
            Refresh();
        }
        if (!SceneCloudTransition.BeginInPlace(Back, Progression.Island.BattleCameraSeconds + 0.15f)) Back();
    }

    private void GoHome()
    {
        IslandSession.Clear();
        GameManager.Instance?.LoadBaseWorld();
    }

    public void Toast(string text) => Toast(text, 2.4f);

    public void Toast(string text, float seconds)
    {
        bannerText.text = text;
        bannerBg.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + seconds;
    }

    private string GateText(int akin)
    {
        int need = Progression.Island.AkinRequiredCastle(akin);
        var save = IslandSession.Save;
        if (akin > (save?.highestAkin ?? 0) + 1)
            return L.T("Önce önceki Akın'ı kazan.", "Win the previous raid first.");
        return L.T($"Gerekli: Kıyı Kalesi {need} (Ana Üs {need})", $"Requires Keep {need} (Main Base {need})");
    }

    // ==================== YENİLEME ====================

    private void Refresh()
    {
        if (raid == null) return;
        var state = raid.Phase;
        bool running = state == IslandRaidManager.State.Running;
        bool idle = state == IslandRaidManager.State.Idle;
        if (!idle) prepOpen = false;
        board.SetVisible(idle && prepOpen);
        dock.SetActive(idle && !prepOpen && !placing && !(buildUi != null && buildUi.Open));
        if (!idle) { buildUi?.Close(); }
        castleBar.SetActive(!idle);
        fireRoot.SetActive(running);
        resultPanel.SetActive(state == IslandRaidManager.State.Won || state == IslandRaidManager.State.Lost);
        SnapSelection();

        if (idle)
        {
            int n = selectedAkin;
            if (akinDockLabel != null) akinDockLabel.text = L.T($"AKIN {DockAkin()}", $"RAID {DockAkin()}");
            radial?.RefreshBadges();
            if (prepOpen)
            {
                bool can = raid.CanStart(n);
                board.Refresh(n, IslandSession.Save?.highestAkin ?? 0, IslandSession.KeepLevel(), MaxSelectable(), can, GateText(n));
                board.SetRaid(RaidBoardInfo(IslandSession.Save, IslandRepair.Now));
            }
        }

        if (raid.IsRaid && (state == IslandRaidManager.State.Won || state == IslandRaidManager.State.Lost))
        {
            ShowRaidResult(state == IslandRaidManager.State.Won);
        }
        else if (state == IslandRaidManager.State.Won)
        {
            resultAgain.gameObject.SetActive(true);
            var r = raid.LastReward;
            resultTitle.text = L.T("ZAFER!", "VICTORY!");
            string gems = r.gems > 0 ? L.T($"\n+{r.gems} elmas", $"\n+{r.gems} gems") : "";
            resultBody.text = L.T(
                $"Akın {raid.Akin} temizlendi.\n+{r.gold} altın (Ana Üs) · +{r.meat} Et · +{r.wood} Odun{gems}",
                $"Raid {raid.Akin} cleared.\n+{r.gold} gold (Base) · +{r.meat} Meat · +{r.wood} Wood{gems}");
            if (raid.TutorialFinishedThisRun)
            {
                resultBody.text += "\n" + L.T("Odun ödülünü İNŞA ET menüsünde harca. Et yavaş gelir: Ağıl'ı yükselt.",
                    "Spend the Wood in the BUILD menu. Meat comes slowly: upgrade the Pasture.");
            }
            resultBody.text += DamagedBuildings();
            bool next = raid.CanStart(raid.Akin + 1);
            resultNext.gameObject.SetActive(next);
        }
        else if (state == IslandRaidManager.State.Lost)
        {
            resultAgain.gameObject.SetActive(true);
            resultTitle.text = L.T("YENİLGİ", "DEFEAT");
            resultBody.text = L.T($"Kıyı Kalesi düştü.\n{raid.DefeatReason}", $"The keep has fallen.\n{raid.DefeatReason}") + DamagedBuildings();
            resultNext.gameObject.SetActive(false);
        }
        RefreshAuto();
    }

    /// <summary>Canlı baskın sonucu: ödül ya da çalıntı (Akın ödülü/ilerlemesi yok).</summary>
    private void ShowRaidResult(bool won)
    {
        var o = raid.LastRaidOutcome;
        resultNext.gameObject.SetActive(false);
        resultAgain.gameObject.SetActive(false);
        resultTitle.text = won ? L.T("BASKIN PÜSKÜRTÜLDÜ!", "RAID REPELLED!") : L.T("KORSANLAR KAÇTI", "PIRATES GOT AWAY");
        string loot = o.wood > 0 || o.meat > 0
            ? L.T($"Çalınan: {o.wood} Odun · {o.meat} Et", $"Stolen: {o.wood} Wood · {o.meat} Meat")
            : L.T("Hiçbir şey çalınmadı.", "Nothing was stolen.");
        string body = won
            ? L.T($"Beklemedeki üretime +%{Mathf.RoundToInt(Progression.Island.RaidWinBonus * 100)}, +{o.gold} altın (Ana Üs).\n{loot}", $"+{Mathf.RoundToInt(Progression.Island.RaidWinBonus * 100)}% on pending production, +{o.gold} gold (Base).\n{loot}")
            : L.T($"Kıyı Kalesi düştü; korsanlar kumsaldan döndü.\n{loot}\n{raid.DefeatReason}", $"The keep fell; the pirates sailed off.\n{loot}\n{raid.DefeatReason}");
        resultBody.text = body + DamagedBuildings();
    }

    /// <summary>Sonuç ekranı: hasarlı binalar ve onarım süresi (§7.7; ücretsiz, yalnız bekleme).</summary>
    private static string DamagedBuildings()
    {
        var names = new System.Text.StringBuilder();
        int count = 0;
        foreach (var building in IslandBuilding.All)
        {
            if (building == null || !building.UnderRepair) continue;
            if (count++ > 0) names.Append(", ");
            int left = Mathf.CeilToInt(building.RepairSecondsLeft);
            names.Append($"{BuildingName(building.Kind)} {left / 60}:{left % 60:00}");
        }
        return count == 0 ? "" : "\n" + L.T("Onarımda (ücretsiz): ", "Repairing (free): ") + names;
    }

    private static string BuildingName(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.House: return L.T("Ev", "House");
            case IslandLayout.BuildingKind.Lumber: return L.T("Oduncu", "Lumber");
            case IslandLayout.BuildingKind.Pasture: return L.T("Ağıl", "Pasture");
            case IslandLayout.BuildingKind.Archery: return L.T("Okçuluk", "Archery");
            case IslandLayout.BuildingKind.Tower: return L.T("Kule", "Tower");
            case IslandLayout.BuildingKind.Barracks: return L.T("Kışla", "Barracks");
            case IslandLayout.BuildingKind.Monastery: return L.T("Manastır", "Monastery");
            default: return L.T("Kale", "Keep");
        }
    }

    private void RefreshAuto()
    {
        if (autoLabel == null || archer == null) return;
        bool unlocked = IslandSession.AutoFireUnlocked(IslandSession.KeepLevel());
        autoLabel.text = !unlocked ? L.T("OTO ", "AUTO ") + "KK" + Progression.Island.AutoFireUnlockKeepLevel
            : archer.AutoFire ? L.T("OTO\nAÇIK", "AUTO\nON") : L.T("OTO\nKAPALI", "AUTO\nOFF");
        var image = autoButton.GetComponent<Image>();
        if (image != null) image.color = archer.AutoFire ? new Color(0.6f, 1f, 0.6f) : Color.white;
        if (mapLabel != null && setup.CameraController != null)
            mapLabel.text = setup.CameraController.FullFrame ? L.T("TAKİP", "FOLLOW") : L.T("HARİTA", "MAP");
    }

    private void Update()
    {
        if (raid == null) return;
        // Kale canı
        if (setup.CastleStructure != null)
        {
            var c = setup.CastleStructure;
            castleFill.fillAmount = c.HealthPercent;
            castleFill.color = c.HealthPercent > 0.5f ? new Color(0.35f, 0.7f, 0.95f)
                : c.HealthPercent > 0.25f ? new Color(0.95f, 0.75f, 0.25f) : new Color(0.9f, 0.3f, 0.25f);
            castleText.text = L.T($"Kıyı Kalesi {Mathf.CeilToInt(c.CurrentHealth)}/{Mathf.CeilToInt(c.MaxHealth)}",
                $"Coastal Keep {Mathf.CeilToInt(c.CurrentHealth)}/{Mathf.CeilToInt(c.MaxHealth)}");
        }
        var save = IslandSession.Save;
        if (meatText != null) meatText.text = save != null ? save.meat.ToString() : "0";
        if (meatText != null && save != null)
        {
            // Cüzdan tavanına ulaşan sayaç sarıya döner (üretim durur)
            meatText.color = save.meat >= IslandEconomy.MeatCap ? Gold : ReferenceUiVisuals.Light;
            woodText.color = save.wood >= IslandEconomy.WoodCap ? Gold : ReferenceUiVisuals.Light;
        }
        buildUi?.Tick();
        if (Time.unscaledTime >= nextRaidUiPoll) { nextRaidUiPoll = Time.unscaledTime + 0.5f; RefreshRaidUi(); }
        if (dock != null && raid.Phase == IslandRaidManager.State.Idle)
        {
            bool dockOn = !prepOpen && !placing && !(buildUi != null && buildUi.Open);
            if (dock.activeSelf != dockOn) dock.SetActive(dockOn);
        }
        if (staffButton != null)
        {
            bool staffOn = raid.Phase == IslandRaidManager.State.Idle && !placing && !prepOpen;
            if (staffButton.activeSelf != staffOn) staffButton.SetActive(staffOn);
        }
        if (woodText != null) woodText.text = save != null ? save.wood.ToString() : "0";
        var player = GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
        if (goldText != null) goldText.text = player != null ? player.gold.ToString() : "0";
        titleText.text = raid.Phase == IslandRaidManager.State.Running && raid.IsRaid
            ? L.T($"BASKIN · DALGA {raid.Wave}/{raid.TotalWaves}", $"RAID · WAVE {raid.Wave}/{raid.TotalWaves}")
            : raid.Phase == IslandRaidManager.State.Running
            ? L.T($"AKIN {raid.Akin} · DALGA {raid.Wave}/{raid.TotalWaves}", $"RAID {raid.Akin} · WAVE {raid.Wave}/{raid.TotalWaves}")
            : resourceBar != null ? IslandNameRules.Display(save) : L.T("MARTI ADASI", "GULL ISLE");
        if (homeLabel != null)
            homeLabel.text = Time.unscaledTime < homeConfirmUntil ? L.T("EMİN MİSİN?", "SURE?") : L.T("ANA ÜS", "BASE");

        // Öğretici ipucu
        string hint = raid.TutorialHint();
        hintBg.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        hintText.text = hint;

        // ATEŞ: dolum halkası + yanıp sönen vurgu (öğretici adım 1)
        if (archer != null)
        {
            chargeRing.fillAmount = archer.Charging ? archer.Charge01 : 0f;
            cooldownFill.fillAmount = archer.IsAlive ? archer.CooldownFraction : 1f;
            bool pulse = raid.TutorialStep == 1 && raid.Phase == IslandRaidManager.State.Running;
            fireFace.color = pulse ? Color.Lerp(Color.white, new Color(1f, 0.75f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 6f)) : Color.white;
            if (!archer.IsAlive)
            {
                bannerText.text = L.T($"OKÇU DÜŞTÜ · {Mathf.CeilToInt(archer.RespawnLeft)} sn", $"ARCHER DOWN · {Mathf.CeilToInt(archer.RespawnLeft)}s");
                bannerBg.gameObject.SetActive(true);
                toastUntil = Time.unscaledTime + 0.4f;
            }
        }
        UpdateEdgeArrows();
        RefreshAuto();
        RefreshAbilities();
        if (bannerBg != null && bannerBg.gameObject.activeSelf && Time.unscaledTime > toastUntil) bannerBg.gameObject.SetActive(false);
    }
}
