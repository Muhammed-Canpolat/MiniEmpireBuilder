using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ayarlar ekranı (Ana Üs, Ada, Sefer, Savaş ortak). 3.3: Tiny Swords kâğıt + kurdele, bölümlü ve kaydırılabilir;
/// SES (müzik/efekt kaydırıcı), TİTREŞİM, DİL, BİLDİRİMLER, GİZLİLİK, DESTEK, sürüm. Savaşta ayrıca DEVAM ET / PES ET.
/// </summary>
public sealed class AudioSettingsUI : MonoBehaviour
{
    // TODO: gerçek gizlilik politikası adresi yayın öncesi buraya yazılacak.
    public const string PrivacyPolicyUrl = "https://example.com/privacy";
    /// <summary>Destek e-postası: tek kaynak (D120).</summary>
    public const string SupportEmail = "miniempirebuilder@gmail.com";

    /// <summary>
    /// mailto bağlantısı (iOS ve Android): adres düz, konu ve gövde RFC 6068'e göre yüzde-kodlanır (boşluk %20; "+" kullanılmaz).
    /// </summary>
    public static string BuildMailtoUrl(string subject, string body)
    {
        var url = "mailto:" + SupportEmail;
        char sep = '?';
        if (!string.IsNullOrEmpty(subject)) { url += sep + "subject=" + Uri.EscapeDataString(subject); sep = '&'; }
        if (!string.IsNullOrEmpty(body)) url += sep + "body=" + Uri.EscapeDataString(body);
        return url;
    }
    private const string VibrationKey = "MEB_Vibration";

    /// <summary>Titreşim ayarı (PlayerPrefs MEB_Vibration, varsayılan açık).</summary>
    public static bool VibrationEnabled
    {
        get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        set { PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private readonly List<Action> refreshers = new List<Action>();
    private RectTransform safeRect, panelRect;
    private CanvasGroup panelGroup, dimGroup;
    private ScrollRect scroll;
    private bool isOpen;
    private Sequence anim;
    private GameObject panel;
    private Texture2D gearTexture;
    private Sprite gearSprite;
    private GameObject blocker;
    private GameObject confirmation;
    private bool battleScene;
    private bool forfeitCommitted;
    public static bool BattleModalOpen { get; internal set; }
    public bool IsBattleModalOpen => battleScene && isOpen;

    private static AudioSettingsUI current;
    /// <summary>Ayarlar penceresi açık mı (BackKeyRouter / testler).</summary>
    public static bool IsSettingsOpen => current != null && current.isOpen;

    /// <summary>
    /// Geri tuşu (BackKeyRouter): PES ET onayı açıksa yalnız onu kapatır (VAZGEÇ), değilse ayarlar penceresini mevcut Close yoluyla
    /// kapatır (savaşta devam ettirir). Açık bir şey yoksa false.
    /// </summary>
    public static bool CloseIfOpen()
    {
        if (current == null || !current.isOpen) return false;
        if (current.confirmation != null && current.confirmation.activeSelf) current.confirmation.SetActive(false);
        else current.Close();
        return true;
    }

    /// <summary>Geri tuşu: ayar düğmesinin yaptığını yapar (savaşta oyunu duraklatır). Açılırsa true.</summary>
    public static bool OpenIfPossible()
    {
        if (current == null || current.isOpen) return false;
        current.Open();
        return current.isOpen;
    }

    private void Start()
    {
        current = this;
        var canvasObject = new GameObject("AudioSettingsCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1200;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390, 844);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        var safe = ReferenceUiVisuals.SafeArea(canvasObject.transform);
        safeRect = safe;

        bool baseScene = SceneManager.GetActiveScene().name == "BaseScene";
        bool battleLayout = SceneManager.GetActiveScene().name == "BattleScene";
        // Bug 20: ada üssü de BattleScene'de çalışır ama BattleManager yoktur; ayarlar savaş kipi değil, normal menü olarak açılır.
        battleScene = battleLayout && !IslandSession.Active;
        var gear = ReferenceUiVisuals.Button(safe, "AudioSettingsButton", "", false,
            new Vector2(0.89f, baseScene ? 0.935f : battleLayout ? 0.93f : 0.93f),
            new Vector2(0.98f, baseScene ? 0.978f : battleLayout ? 0.973f : 0.975f),
            Vector2.zero, Vector2.zero, 22);
        gear.GetComponent<Image>().sprite = BaseMapManager.MakeCircleSprite(Color.white);
        gear.GetComponent<Image>().type = Image.Type.Simple;
        gear.GetComponent<Image>().color = ReferenceUiVisuals.Panel;
        if (battleLayout)
        {
            var hit = (RectTransform)gear.transform;
            // Üst bar 0.90–1.0; düşman/altın çipleri bunun %49–%91'inde → merkez ≈ 0.97.
            hit.anchorMin = hit.anchorMax = new Vector2(1f, 0.97f);
            hit.pivot = new Vector2(1f, 0.5f);
            hit.anchoredPosition = new Vector2(-8f, 0f);
            hit.sizeDelta = new Vector2(42f, 42f);
        }
        bool miniGear = baseScene && MiniEmpireUiCatalog.TopBarReady;
        if (miniGear)
        {
            // D106: Ana Üs'te paket dişlisi (btn_settings, tek parça), üst barın sağ ucunda; anchor + ref. birim.
            var gearImage = gear.GetComponent<Image>();
            gearImage.sprite = MiniEmpireUiCatalog.Instance.btnSettings;
            gearImage.type = Image.Type.Simple;
            gearImage.preserveAspect = true;
            gearImage.color = Color.white;
            var gearRect = (RectTransform)gear.transform;
            gearRect.anchorMin = gearRect.anchorMax = new Vector2(1f, 1f);
            gearRect.pivot = new Vector2(1f, 1f);
            gearRect.sizeDelta = new Vector2(MiniEmpireTopBarSkin.GearSize, MiniEmpireTopBarSkin.GearSize);
            gearRect.anchoredPosition = new Vector2(-10f, -(MiniEmpireTopBarSkin.BarHeight - MiniEmpireTopBarSkin.GearSize) * 0.5f);
        }
        var gearLabel = gear.GetComponentInChildren<TextMeshProUGUI>();
        if (gearLabel != null) gearLabel.text = string.Empty;
if (!miniGear)
        {
                var icon = ReferenceUiVisuals.Image(gear.transform, "SettingsGearIcon",
                    new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f),
                    Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
                icon.sprite = CreateGearSprite();
        }
        gear.onClick.AddListener(Open);

        BuildPanel(safe);
    }

    // ---------- Pencere ----------

    private static Button Btn(Transform parent, string name, string label, bool red, float size, out TextMeshProUGUI text)
    {
        var b = FancyInfoPanel.TsButton(parent, name, label, red, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, size, out text);
        var img = b.GetComponent<Image>();
        if (img.sprite == null)   // katalog yoksa düz renkli yedek
        {
            img.type = Image.Type.Sliced;
            ReferenceUiVisuals.ApplyButtonSkin(b, !red);
            if (red) img.color = ReferenceUiVisuals.Red;
        }
        return b;
    }

    private static void SetBtnLook(Button b, bool red)
    {
        var img = b.GetComponent<Image>();
        var sprite = FancyInfoPanel.Ui(red ? "ui_button_red" : "ui_button_blue");
        if (sprite != null) img.sprite = sprite; else img.color = red ? ReferenceUiVisuals.Red : Color.white;
    }

    private static RectTransform Row(Transform parent, string name, float height, bool horizontal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height; le.preferredHeight = height;
        if (horizontal)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        }
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI RowText(Transform parent, string name, string value, float size, Color color,
        TextAlignmentOptions align, float flexible)
    {
        var t = ReferenceUiVisuals.Text(parent, name, value, size, color, align, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, true);
        t.textWrappingMode = TextWrappingModes.Normal;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = flexible;
        return t;
    }

    private static void FixedSize(Component c, float width, float height)
    {
        var le = c.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = width;
        le.preferredHeight = le.minHeight = height;
    }

    private void Section(Transform content, string icon, string title)
    {
        var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divider.transform.SetParent(content, false);
        var dImg = divider.GetComponent<Image>();
        dImg.color = new Color(FancyInfoPanel.Ink.r, FancyInfoPanel.Ink.g, FancyInfoPanel.Ink.b, 0.28f);
        dImg.raycastTarget = false;
        divider.GetComponent<LayoutElement>().preferredHeight = 2f;
        var row = Row(content, "Header_" + icon, 36f, true);
        var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(row, false);
        var img = iconObj.GetComponent<Image>();
        img.sprite = CampaignSceneSetup.Art(icon);
        img.preserveAspect = true; img.raycastTarget = false;
        FixedSize(img, 30f, 30f);
        RowText(row, "Title", title, 17, new Color(0.55f, 0.32f, 0.06f), TextAlignmentOptions.Left, 1f);
    }

    private void ToggleRow(Transform content, string name, string label, Func<bool> get, Action<bool> set)
    {
        var row = Row(content, name + "Row", 52f, true);
        RowText(row, "Label", label, 16, FancyInfoPanel.Ink, TextAlignmentOptions.Left, 1f);
        var button = Btn(row, name, "", false, 14, out var text);
        FixedSize(button, 104f, 48f);
        button.onClick.AddListener(() => { set(!get()); RefreshLabels(); });
        refreshers.Add(() =>
        {
            bool on = get();
            text.text = on ? L.T("AÇIK", "ON") : L.T("KAPALI", "OFF");
            SetBtnLook(button, !on);
        });
    }

    private void SliderRow(Transform content, string name, string label, Func<bool> getOn, Action<bool> setOn,
        Func<float> getValue, Action<float> setValue)
    {
        var title = Row(content, name + "Row", 26f, true);
        RowText(title, "Label", label, 16, FancyInfoPanel.Ink, TextAlignmentOptions.Left, 1f);
        var row = Row(content, name + "Controls", 52f, true);

        var sliderObj = new GameObject(name + "Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sliderObj.transform.SetParent(row, false);
        var sle = sliderObj.GetComponent<LayoutElement>();
        sle.flexibleWidth = 1f; sle.minHeight = 48f; sle.preferredHeight = 48f;
        var pill = ReferenceUiVisuals.PillSprite();
        var bg = ReferenceUiVisuals.Image(sliderObj.transform, "Track", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -8), new Vector2(0, 8),
            new Color(FancyInfoPanel.Ink.r, FancyInfoPanel.Ink.g, FancyInfoPanel.Ink.b, 0.35f));
        bg.sprite = pill; bg.type = Image.Type.Sliced; bg.raycastTarget = false;
        var fillArea = ReferenceUiVisuals.Rect(sliderObj.transform, "FillArea", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -8), new Vector2(0, 8));
        var fill = ReferenceUiVisuals.Image(fillArea, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, ReferenceUiVisuals.Blue);
        fill.sprite = pill; fill.type = Image.Type.Sliced; fill.raycastTarget = false;
        var slideArea = ReferenceUiVisuals.Rect(sliderObj.transform, "HandleArea", Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
        var handle = ReferenceUiVisuals.Image(slideArea, "Handle", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-16, -16), new Vector2(16, 16), Color.white);
        handle.sprite = CampaignSceneSetup.Art("ui_node_blue");
        if (handle.sprite == null) handle.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        handle.preserveAspect = true;
        var slider = sliderObj.GetComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.SetValueWithoutNotify(getValue());
        slider.onValueChanged.AddListener(v => setValue(v));

        var button = Btn(row, name, "", false, 14, out var text);
        FixedSize(button, 92f, 48f);
        button.onClick.AddListener(() => { setOn(!getOn()); RefreshLabels(); });
        refreshers.Add(() =>
        {
            bool on = getOn();
            text.text = on ? L.T("AÇIK", "ON") : L.T("KAPALI", "OFF");
            SetBtnLook(button, !on);
            slider.SetValueWithoutNotify(getValue());
            fill.color = on ? ReferenceUiVisuals.Blue : new Color(0.5f, 0.5f, 0.5f, 0.8f);
        });
    }

    private Button WideButton(Transform content, string name, string label, bool red, out TextMeshProUGUI text)
    {
        var row = Row(content, name + "Row", 52f, false);
        var b = Btn(row, name, label, red, 16, out text);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return b;
    }

    private void BuildPanel(RectTransform safe)
    {
        // Karartma: savaşta "BattleSettingsBlocker" (testler), diğer sahnelerde "SettingsDim"; dokununca kapatır.
        var dim = ReferenceUiVisuals.Image(safe, battleScene ? "BattleSettingsBlocker" : "SettingsDim",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);
        blocker = dim.gameObject;
        blocker.SetActive(false);

        var paper = FancyInfoPanel.NineSlice(safe, "AudioSettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-180, -300), new Vector2(180, 300), "ui_paper", Color.white, 2.4f);
        if (paper.sprite == null) paper.color = new Color(0.93f, 0.85f, 0.66f, 1f);
        paper.raycastTarget = true;
        panel = paper.gameObject;
        panelRect = (RectTransform)panel.transform;
        panelGroup = panel.AddComponent<CanvasGroup>();

        var ribbon = FancyInfoPanel.NineSlice(panel.transform, "Ribbon", new Vector2(0.08f, 1f), new Vector2(0.92f, 1f),
            new Vector2(0, -34), new Vector2(0, 20), "ui_ribbon", Color.white, 3f);
        var title = ReferenceUiVisuals.Text(ribbon.transform, "Title", L.T("AYARLAR", "SETTINGS"), 20, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(40, 8), new Vector2(-40, -4), true);
        title.enableAutoSizing = true; title.fontSizeMin = 12f; title.fontSizeMax = 20f;

        // Kaydırma alanı
        var viewport = ReferenceUiVisuals.Rect(panel.transform, "Scroll", Vector2.zero, Vector2.one, new Vector2(16, 82), new Vector2(-16, -44));
        var vpImage = viewport.gameObject.AddComponent<Image>();
        vpImage.color = new Color(0, 0, 0, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        var contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(viewport, false);
        var content = (RectTransform)contentObj.transform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var vlg = contentObj.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6; vlg.padding = new RectOffset(4, 4, 4, 8);
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        contentObj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        // SES
        Section(content, "ui_icon_music", L.T("SES", "SOUND"));
        SliderRow(content, "MusicToggle", L.T("Müzik", "Music"),
            () => AudioManager.Instance == null || AudioManager.Instance.MusicEnabled,
            on => AudioManager.Instance?.SetMusicEnabled(on),
            () => AudioManager.Instance != null ? AudioManager.Instance.MusicVolume01 : 1f,
            v => AudioManager.Instance?.SetMusicVolume01(v));
        SliderRow(content, "EffectsToggle", L.T("Efektler", "Effects"),
            () => AudioManager.Instance == null || AudioManager.Instance.SfxEnabled,
            on => AudioManager.Instance?.SetSfxEnabled(on),
            () => AudioManager.Instance != null ? AudioManager.Instance.SfxVolume01 : 1f,
            v => AudioManager.Instance?.SetSfxVolume01(v));

        // TİTREŞİM
        Section(content, "ui_icon_heart", L.T("TİTREŞİM", "VIBRATION"));
        ToggleRow(content, "VibrationToggle", L.T("Dokunsal geri bildirim", "Haptic feedback"),
            () => VibrationEnabled, v => { VibrationEnabled = v; if (v && Application.isMobilePlatform) Handheld.Vibrate(); });

        // DİL (savaşta kapalı: sahne yeniden yüklenir)
        Section(content, "ui_icon_info", L.T("DİL", "LANGUAGE"));
        var language = WideButton(content, "LanguageToggle", "", false, out var languageLabel);
        language.interactable = !battleScene;
        if (battleScene) language.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 0.85f);
        language.onClick.AddListener(() =>
        {
            if (battleScene) return;
            L.Set(L.English ? GameLanguage.Turkish : GameLanguage.English);
            Time.timeScale = 1f;
            SceneTransition.LoadDirect(SceneManager.GetActiveScene().name);
        });
        refreshers.Add(() => languageLabel.text = (L.English ? "LANGUAGE  ENGLISH" : "DİL  TÜRKÇE") +
            (battleScene ? L.T("  (savaşta kapalı)", "  (locked in battle)") : ""));

        // BİLDİRİMLER
        Section(content, "ui_icon_clock", L.T("BİLDİRİMLER", "NOTIFICATIONS"));
        ToggleRow(content, "NotificationsToggle", L.T("Bildirimleri al", "Receive notifications"),
            () => GameNotifications.Enabled, v => GameNotifications.Enabled = v);

        // GİZLİLİK POLİTİKASI
        Section(content, "ui_icon_shield", L.T("GİZLİLİK POLİTİKASI", "PRIVACY POLICY"));
        var privacy = WideButton(content, "PrivacyPolicyButton", L.T("POLİTİKAYI AÇ", "OPEN POLICY"), false, out _);
        privacy.onClick.AddListener(() => Application.OpenURL(PrivacyPolicyUrl));

        // DESTEK / İLETİŞİM
        Section(content, "ui_icon_gear", L.T("DESTEK / İLETİŞİM", "SUPPORT / CONTACT"));
        var support = WideButton(content, "SupportButton", L.T("E-POSTA GÖNDER", "SEND E-MAIL"), false, out _);
        support.onClick.AddListener(() => Application.OpenURL(BuildMailtoUrl(
            "Mini Empire Builder " + Application.version,
            L.T("Merhaba,\n\n", "Hello,\n\n") + "(" + Application.platform + " - " + SystemInfo.deviceModel + ")")));

        if (battleScene)
        {
            Section(content, "ui_icon_sword", L.T("SAVAŞ", "BATTLE"));
            var surrender = WideButton(content, "ForfeitButton", L.T("PES ET", "SURRENDER"), true, out _);
            surrender.onClick.AddListener(() => confirmation.SetActive(true));
        }

        // Sürüm
        var versionRow = Row(content, "VersionRow", 26f, true);
        RowText(versionRow, "Version", L.T("Sürüm ", "Version ") + Application.version, 12,
            new Color(FancyInfoPanel.Ink.r, FancyInfoPanel.Ink.g, FancyInfoPanel.Ink.b, 0.7f), TextAlignmentOptions.Center, 1f);

        // Alt sabit düğme: Kapat / Devam et
        var close = Btn(panel.transform, "CloseAudioSettings", battleScene ? L.T("DEVAM ET", "RESUME") : L.T("KAPAT", "CLOSE"), false, 17, out _);
        var crt = (RectTransform)close.transform;
        crt.anchorMin = new Vector2(0.12f, 0f); crt.anchorMax = new Vector2(0.88f, 0f);
        crt.offsetMin = new Vector2(0, 16); crt.offsetMax = new Vector2(0, 68);
        close.onClick.AddListener(Close);
        if (battleScene) CreateForfeitConfirm();
        panel.SetActive(false);
    }

    private void CreateForfeitConfirm()
    {
        var overlay = FancyInfoPanel.NineSlice(panel.transform, "ForfeitConfirm", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            "ui_paper", new Color(0.92f, 0.88f, 0.8f, 1f), 2.4f);
        if (overlay.sprite == null) overlay.color = new Color(0.93f, 0.85f, 0.66f, 1f);
        overlay.raycastTarget = true;
        confirmation = overlay.gameObject;
        ReferenceUiVisuals.Text(confirmation.transform, "ConfirmTitle", L.T("PES ET", "SURRENDER"), 22,
            FancyInfoPanel.Missing, TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero, true);
        var body = ReferenceUiVisuals.Text(confirmation.transform, "ConfirmDescription",
            L.T("Pes etmek istediğine emin misin? Bu savaşın ilerlemesi kaybolur.",
                "Are you sure you want to surrender? This battle's progress will be lost."), 17,
            FancyInfoPanel.Ink, TextAlignmentOptions.Center,
            new Vector2(0.09f, 0.32f), new Vector2(0.91f, 0.68f), Vector2.zero, Vector2.zero, false, true);
        body.textWrappingMode = TextWrappingModes.Normal;
        var cancel = Btn(confirmation.transform, "CancelForfeit", L.T("VAZGEÇ", "CANCEL"), false, 15, out _);
        var cr = (RectTransform)cancel.transform;
        cr.anchorMin = new Vector2(0.06f, 0.07f); cr.anchorMax = new Vector2(0.48f, 0.07f);
        cr.offsetMin = Vector2.zero; cr.offsetMax = new Vector2(0, 56);
        cancel.onClick.AddListener(() => confirmation.SetActive(false));
        var confirm = Btn(confirmation.transform, "ConfirmForfeit", L.T("PES ET", "SURRENDER"), true, 15, out _);
        var kr = (RectTransform)confirm.transform;
        kr.anchorMin = new Vector2(0.52f, 0.07f); kr.anchorMax = new Vector2(0.94f, 0.07f);
        kr.offsetMin = Vector2.zero; kr.offsetMax = new Vector2(0, 56);
        confirm.onClick.AddListener(ConfirmForfeit);
        confirmation.SetActive(false);
    }

    private void ConfirmForfeit()
    {
        if (forfeitCommitted) return;
        var battle = BattleManager.Instance;
        if (battle == null || !battle.IsBattleActive) return;
        forfeitCommitted = true;
        Close();
        battle.Forfeit();
    }

    private void Close()
    {
        if (confirmation != null) confirmation.SetActive(false);
        if (!isOpen) return;
        isOpen = false;
        PlayerPrefs.Save();
        // Oyun durumu hemen geri verilir; görsel kapanış kısa bir animasyonla biter.
        if (battleScene) BattlePause.Resume(BattlePause.Settings);   // 6. oturum (1.5): önceki zaman ölçeği + ses geri gelir
        if (battleScene) BattleModalOpen = false;
        anim?.Kill();
        if (panel == null) return;
        anim = DOTween.Sequence().SetUpdate(true).SetLink(panel);
        anim.Append(panelRect.DOScale(0.9f, 0.15f).SetEase(Ease.InQuad));
        anim.Join(panelGroup.DOFade(0f, 0.15f));
        anim.Join(dimGroup.DOFade(0f, 0.15f));
        anim.OnComplete(() =>
        {
            if (isOpen) return;
            panel.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
        });
    }

    public void Open()
    {
        if (battleScene && (BattleManager.Instance == null || !BattleManager.Instance.IsBattleActive)) return;
        if (panel == null) return;
        RefreshLabels();
        anim?.Kill();
        isOpen = true;
        if (blocker != null) blocker.SetActive(true);
        panel.SetActive(true);
        FitPanel();
        if (confirmation != null) confirmation.SetActive(false);
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        panelRect.localScale = Vector3.one * 0.85f;
        panelGroup.alpha = 0f; dimGroup.alpha = 0f;
        anim = DOTween.Sequence().SetUpdate(true).SetLink(panel);
        anim.Append(panelRect.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
        anim.Join(panelGroup.DOFade(1f, 0.18f));
        anim.Join(dimGroup.DOFade(1f, 0.18f));
        if (battleScene) BattlePause.Pause(BattlePause.Settings);    // zaman, zamanlayıcılar, ses ve müzik durur
        if (battleScene) BattleModalOpen = true;
    }

    /// <summary>
    /// Pencere boyu: güvenli alanın %86'sı (en çok 400×900); içerik uzunsa kaydırılır. Güvenli alan değişince (döndürme, çentik,
    /// farklı oran) <see cref="Refit"/> yeniden sığdırır.
    /// </summary>
    private void FitPanel()
    {
        Canvas.ForceUpdateCanvases();
        if (safeRect == null || panelRect == null) return;
        var r = safeRect.rect;
        lastSafe = r.size;
        float w = Mathf.Clamp(r.width - 24f, 280f, 400f);
        float h = Mathf.Clamp(r.height * 0.86f - 40f, 320f, 900f);
        panelRect.anchoredPosition = new Vector2(0f, -8f);
        panelRect.sizeDelta = new Vector2(w, h);
    }

    private Vector2 lastSafe;

    /// <summary>Güvenli alan boyu değiştiyse pencereyi yeniden sığdırır (testler/ekran görüntüsü de çağırır).</summary>
    public void Refit()
    {
        if (safeRect == null || panelRect == null) return;
        if ((safeRect.rect.size - lastSafe).sqrMagnitude > 1f) FitPanel();
    }

    private void LateUpdate() => Refit();

    private void RefreshLabels()
    {
        foreach (var refresh in refreshers) refresh();
    }

    private Sprite CreateGearSprite()
    {
        const int size = 64;
        gearTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        gearTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float phase = Mathf.Repeat(Mathf.Atan2(dy, dx) * 8f / (Mathf.PI * 2f), 1f);
            bool tooth = phase < 0.24f || phase > 0.76f;
            bool filled = radius >= 0.25f &&
                (radius <= 0.60f || (radius <= 0.80f && tooth));
            gearTexture.SetPixel(x, y, filled ? Color.white : Color.clear);
        }
        gearTexture.Apply();
        gearSprite = Sprite.Create(gearTexture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), size);
        return gearSprite;
    }

    private void OnDestroy()
    {
        anim?.Kill();
        if (current == this) current = null;
        if (battleScene) BattlePause.Resume(BattlePause.Settings);
        if (battleScene) BattleModalOpen = false;
        if (gearSprite != null) Destroy(gearSprite);
        if (gearTexture != null) Destroy(gearTexture);
    }
}
