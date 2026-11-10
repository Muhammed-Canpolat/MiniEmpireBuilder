using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Small sound menu shared by the base, battle and title scenes.</summary>
public sealed class AudioSettingsUI : MonoBehaviour
{
    private GameObject panel;
    private TextMeshProUGUI musicLabel;
    private TextMeshProUGUI effectsLabel;
    private TextMeshProUGUI languageLabel;
    private TextMeshProUGUI notificationsLabel;
    private Texture2D gearTexture;
    private Sprite gearSprite;
    private GameObject blocker;
    private GameObject confirmation;
    private bool battleScene;
    private bool forfeitCommitted;
    public static bool BattleModalOpen { get; internal set; }
    public bool IsBattleModalOpen => battleScene && panel != null && panel.activeSelf;

    private void Start()
    {
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

        bool baseScene = SceneManager.GetActiveScene().name == "BaseScene";
        battleScene = SceneManager.GetActiveScene().name == "BattleScene";
        var gear = ReferenceUiVisuals.Button(safe, "AudioSettingsButton", "", false,
            new Vector2(0.89f, baseScene ? 0.935f : battleScene ? 0.93f : 0.93f),
            new Vector2(0.98f, baseScene ? 0.978f : battleScene ? 0.973f : 0.975f),
            Vector2.zero, Vector2.zero, 22);
        gear.GetComponent<Image>().sprite = BaseMapManager.MakeCircleSprite(Color.white);
        gear.GetComponent<Image>().type = Image.Type.Simple;
        gear.GetComponent<Image>().color = ReferenceUiVisuals.Panel;
        if (battleScene)
        {
            var hit = (RectTransform)gear.transform;
            // Üst bar 0.90–1.0; düşman/altın çipleri bunun %49–%91'inde → merkez ≈ 0.97.
            hit.anchorMin = hit.anchorMax = new Vector2(1f, 0.97f);
            hit.pivot = new Vector2(1f, 0.5f);
            hit.anchoredPosition = new Vector2(-8f, 0f);
            hit.sizeDelta = new Vector2(42f, 42f);
        }
        var gearLabel = gear.GetComponentInChildren<TextMeshProUGUI>();
        if (gearLabel != null) gearLabel.text = string.Empty;
        var icon = ReferenceUiVisuals.Image(gear.transform, "SettingsGearIcon",
            new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f),
            Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
        icon.sprite = CreateGearSprite();
        gear.onClick.AddListener(Open);

        if (battleScene)
        {
            blocker = ReferenceUiVisuals.Image(safe, "BattleSettingsBlocker",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.5f)).gameObject;
            blocker.GetComponent<Image>().raycastTarget = true;
            blocker.SetActive(false);
        }

        panel = ReferenceUiVisuals.Image(safe, "AudioSettingsPanel",
            new Vector2(0.1f, battleScene ? 0.29f : 0.27f),
            new Vector2(0.9f, battleScene ? 0.71f : 0.73f), Vector2.zero, Vector2.zero,
            new Color(0.09f, 0.075f, 0.06f, 0.98f)).gameObject;
        panel.GetComponent<Image>().raycastTarget = true;
        var frame = panel.AddComponent<Outline>();
        frame.effectColor = ReferenceUiVisuals.Border;
        frame.effectDistance = new Vector2(3f, -3f);
        ReferenceUiVisuals.Text(panel.transform, "Title", L.T("AYARLAR", "SETTINGS"), 21,
            ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
            new Vector2(0.08f, battleScene ? 0.76f : 0.84f), new Vector2(0.92f, 0.97f), Vector2.zero, Vector2.zero, true);
        var music = ReferenceUiVisuals.Button(panel.transform, "MusicToggle", "", false,
            new Vector2(0.12f, battleScene ? 0.55f : 0.70f),
            new Vector2(0.88f, battleScene ? 0.69f : 0.81f), Vector2.zero, Vector2.zero, 17);
        musicLabel = music.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        music.onClick.AddListener(() =>
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            audio.SetMusicEnabled(!audio.MusicEnabled);
            RefreshLabels();
        });
        var effects = ReferenceUiVisuals.Button(panel.transform, "EffectsToggle", "", false,
            new Vector2(0.12f, battleScene ? 0.37f : 0.57f),
            new Vector2(0.88f, battleScene ? 0.51f : 0.68f), Vector2.zero, Vector2.zero, 17);
        effectsLabel = effects.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        effects.onClick.AddListener(() =>
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            audio.SetSfxEnabled(!audio.SfxEnabled);
            RefreshLabels();
        });
        // Dil: savaş dışında tek dokunuşla Türkçe ⇄ İngilizce; arayüz kodla kurulduğu için sahne yeniden yüklenir.
        if (!battleScene)
        {
            var notifications = ReferenceUiVisuals.Button(panel.transform, "NotificationsToggle", "", false,
                new Vector2(0.12f, 0.44f), new Vector2(0.88f, 0.55f), Vector2.zero, Vector2.zero, 17);
            notificationsLabel = notifications.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            notifications.onClick.AddListener(() =>
            {
                GameNotifications.Enabled = !GameNotifications.Enabled;
                RefreshLabels();
            });
            var language = ReferenceUiVisuals.Button(panel.transform, "LanguageToggle", "", false,
                new Vector2(0.12f, 0.31f), new Vector2(0.88f, 0.42f), Vector2.zero, Vector2.zero, 17);
            languageLabel = language.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            language.onClick.AddListener(() =>
            {
                L.Set(L.English ? GameLanguage.Turkish : GameLanguage.English);
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });
        }
        var close = ReferenceUiVisuals.Button(panel.transform, "CloseAudioSettings",
            battleScene ? L.T("DEVAM ET", "RESUME") : L.T("KAPAT", "CLOSE"), true,
            new Vector2(battleScene ? 0.12f : 0.28f, battleScene ? 0.20f : 0.07f),
            new Vector2(battleScene ? 0.88f : 0.72f, battleScene ? 0.34f : 0.20f),
            Vector2.zero, Vector2.zero, 15);
        close.onClick.AddListener(Close);
        if (battleScene) CreateForfeitControls();
        panel.SetActive(false);
    }

    private void CreateForfeitControls()
    {
        var surrender = ReferenceUiVisuals.Button(panel.transform, "ForfeitButton", L.T("PES ET", "SURRENDER"), false,
            new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.17f),
            Vector2.zero, Vector2.zero, 17);
        AddRedFill(surrender);
        surrender.onClick.AddListener(() => confirmation.SetActive(true));

        confirmation = ReferenceUiVisuals.Image(panel.transform, "ForfeitConfirm",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            ReferenceUiVisuals.Panel).gameObject;
        ReferenceUiVisuals.Text(confirmation.transform, "ConfirmTitle", L.T("PES ET", "SURRENDER"), 21,
            ReferenceUiVisuals.Hex(0xC2452D), TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.91f), Vector2.zero, Vector2.zero, true);
        ReferenceUiVisuals.Text(confirmation.transform, "ConfirmDescription",
            L.T("Pes etmek istediğine emin misin? Bu savaşın ilerlemesi kaybolur.",
                "Are you sure you want to surrender? This battle's progress will be lost."), 17,
            ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            new Vector2(0.09f, 0.35f), new Vector2(0.91f, 0.68f), Vector2.zero, Vector2.zero);
        var cancel = ReferenceUiVisuals.Button(confirmation.transform, "CancelForfeit", L.T("VAZGEÇ", "CANCEL"), false,
            new Vector2(0.07f, 0.09f), new Vector2(0.48f, 0.26f), Vector2.zero, Vector2.zero, 15);
        cancel.onClick.AddListener(() => confirmation.SetActive(false));
        var confirm = ReferenceUiVisuals.Button(confirmation.transform, "ConfirmForfeit", L.T("PES ET", "SURRENDER"), false,
            new Vector2(0.52f, 0.09f), new Vector2(0.93f, 0.26f), Vector2.zero, Vector2.zero, 15);
        AddRedFill(confirm);
        confirm.onClick.AddListener(ConfirmForfeit);
        confirmation.SetActive(false);
    }

    private static void AddRedFill(Button button)
    {
        var fill = ReferenceUiVisuals.Image(button.transform, "RedFill",
            Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f),
            ReferenceUiVisuals.Hex(0xC2452D));
        fill.raycastTarget = false;
        fill.transform.SetAsFirstSibling();
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
        if (panel != null) panel.SetActive(false);
        if (blocker != null) blocker.SetActive(false);
        if (battleScene) Time.timeScale = 1f;
        if (battleScene) BattleModalOpen = false;
    }

    public void Open()
    {
        if (battleScene && (BattleManager.Instance == null || !BattleManager.Instance.IsBattleActive)) return;
        RefreshLabels();
        panel?.SetActive(true);
        if (blocker != null) blocker.SetActive(true);
        if (battleScene) Time.timeScale = 0f;
        if (battleScene) BattleModalOpen = true;
    }

    private void RefreshLabels()
    {
        var audio = AudioManager.Instance;
        string on = L.T("AÇIK", "ON"), off = L.T("KAPALI", "OFF");
        if (musicLabel != null) musicLabel.text = L.T("MÜZİK  ", "MUSIC  ") +
            (audio == null || audio.MusicEnabled ? on : off);
        if (effectsLabel != null) effectsLabel.text = L.T("EFEKTLER  ", "EFFECTS  ") +
            (audio == null || audio.SfxEnabled ? on : off);
        if (languageLabel != null) languageLabel.text = L.T("DİL  TÜRKÇE", "LANGUAGE  ENGLISH");
        if (notificationsLabel != null) notificationsLabel.text = L.T("BİLDİRİMLER  ", "NOTIFICATIONS  ") +
            (GameNotifications.Enabled ? on : off);
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
        if (battleScene && Time.timeScale == 0f) Time.timeScale = 1f;
        if (battleScene) BattleModalOpen = false;
        if (gearSprite != null) Destroy(gearSprite);
        if (gearTexture != null) Destroy(gearTexture);
    }
}
