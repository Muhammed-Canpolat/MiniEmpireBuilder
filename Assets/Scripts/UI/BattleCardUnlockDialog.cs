using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (AŞAMA 5): Sefer haritasında savaş kartı açma penceresi. Tiny Swords kâğıt + kurdele; altta EŞİT boyutlu iki düğme:
/// sol "▶ REKLAM İZLE" (oynat simgesi), sağ "N İLE AÇ" (elmas simgesi + net fiyat). Günlük kart reklamı hakkı bitince sol düğme
/// pasifleşir ve yenilenmeye kalan süreyi yazar ("Yeni hak 3sa 12dk"). Elmas yetmezse fiyat kırmızı, dokununca Elmas Dükkânı.
/// </summary>
public sealed class BattleCardUnlockDialog : MonoBehaviour
{
    public static BattleCardUnlockDialog Current { get; private set; }

    private Button adButton, gemButton;
    private TextMeshProUGUI adLabel, gemLabel;
    private Action onAd, onGems;
    private int cost;
    private float nextRefresh;

    public Button AdButton => adButton;
    public Button GemButton => gemButton;
    public string AdText => adLabel != null ? adLabel.text : "";
    public string GemText => gemLabel != null ? gemLabel.text : "";

    public static BattleCardUnlockDialog Show(string title, string body, Sprite cardIcon, int gemCost, Action watchAd, Action payGems)
    {
        Close();
        var root = new GameObject("BattleCardUnlock", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        var d = root.AddComponent<BattleCardUnlockDialog>();
        Current = d;
        d.cost = gemCost; d.onAd = watchAd; d.onGems = payGems;
        d.Build(ReferenceUiVisuals.SafeArea(canvas.transform), title, body, cardIcon);
        return d;
    }

    public static void Close()
    {
        if (Current != null) Destroy(Current.gameObject);
        Current = null;
    }

    private void Build(RectTransform safe, string title, string body, Sprite cardIcon)
    {
        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var center = new Vector2(0.5f, 0.55f);
        var panel = FancyInfoPanel.NineSlice(safe, "Panel", center, center, new Vector2(-170, -120), new Vector2(170, 120), "ui_paper", Color.white, 2.4f);
        panel.raycastTarget = true;
        var ribbon = FancyInfoPanel.NineSlice(panel.transform, "Ribbon", new Vector2(0.06f, 1f), new Vector2(0.94f, 1f), new Vector2(0, -28), new Vector2(0, 20), "ui_ribbon", Color.white, 3f);
        ReferenceUiVisuals.Text(ribbon.transform, "Title", title, 18, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
            new Vector2(12, 4), new Vector2(-12, -2), true);
        var icon = ReferenceUiVisuals.Image(panel.transform, "CardIcon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22, -6), new Vector2(84, 56), Color.white);
        icon.sprite = cardIcon; icon.preserveAspect = true; icon.enabled = cardIcon != null;
        var text = ReferenceUiVisuals.Text(panel.transform, "Body", body, 15, FancyInfoPanel.Ink, TextAlignmentOptions.Left,
            new Vector2(0f, 0.38f), new Vector2(1f, 0.86f), new Vector2(96, 0), new Vector2(-18, 0), false, true);
        text.textWrappingMode = TextWrappingModes.Normal;
        var close = FancyInfoPanel.NineSlice(panel.transform, "Close", Vector2.one, Vector2.one, new Vector2(-40, -40), new Vector2(-4, -4), "ui_button_red", Color.white, 3f);
        close.raycastTarget = true;
        close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        ReferenceUiVisuals.Text(close.transform, "X", "X", 15, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);

        // Eşit boyutlu iki düğme
        adButton = FancyInfoPanel.TsButton(panel.transform, "AdButton", "", false, new Vector2(0.05f, 0f), new Vector2(0.485f, 0f),
            new Vector2(0, 16), new Vector2(0, 70), 15, out adLabel);
        gemButton = FancyInfoPanel.TsButton(panel.transform, "GemButton", "", true, new Vector2(0.515f, 0f), new Vector2(0.95f, 0f),
            new Vector2(0, 16), new Vector2(0, 70), 15, out gemLabel);
        // Aynı düğme ailesi (aynı doku kenarları → görünür boyut da eşit): reklam koyu, elmas altın.
        ReferenceUiVisuals.ApplyButtonSkin(adButton, false);
        ReferenceUiVisuals.ApplyButtonSkin(gemButton, true);
        foreach (var l in new[] { adLabel, gemLabel })
        {
            l.textWrappingMode = TextWrappingModes.NoWrap;
            l.enableAutoSizing = true; l.fontSizeMin = 10f; l.fontSizeMax = 15f;
        }
        adLabel.color = ReferenceUiVisuals.Light;
        AddIcon(adButton.transform, PlayIcon(), adLabel);
        AddIcon(gemButton.transform, GemIcon.Sprite, gemLabel);
        adButton.onClick.AddListener(() =>
        {
            if (!AdRewards.CardAdAvailable) return;
            var act = onAd; Close(); act?.Invoke();
        });
        gemButton.onClick.AddListener(() =>
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Gems < cost) { Close(); GemShopUI.Open(); return; }
            var act = onGems; Close(); act?.Invoke();
        });
        Refresh();
    }

    private static Sprite PlayIcon() =>
        CampaignSceneSetup.Art("ui_icon_play");

    private static void AddIcon(Transform button, Sprite sprite, TextMeshProUGUI label)
    {
        var icon = ReferenceUiVisuals.Image(button, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8, -13), new Vector2(34, 13), Color.white);
        icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
        if (sprite == null)
        {
            // Simge yoksa yazı tipindeki ▶ yerine çizilmiş üçgen yok: metne boşluk bırakılmaz.
            icon.enabled = false;
            return;
        }
        label.rectTransform.offsetMin = new Vector2(36, label.rectTransform.offsetMin.y);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        Refresh();
    }

    public void Refresh()
    {
        bool ad = AdRewards.CardAdAvailable;
        adButton.interactable = ad;
        adLabel.text = ad ? L.T($"REKLAM İZLE ({AdRewards.CardAdsLeft})", $"WATCH AD ({AdRewards.CardAdsLeft})")
            : L.T("Yeni hak ", "New in ") + Progression.FormatDuration(AdRewards.SecondsToDailyReset);
        var gm = GameManager.Instance;
        bool enough = gm != null && gm.Gems >= cost;
        gemLabel.text = L.T($"{cost} İLE AÇ", $"UNLOCK · {cost}");
        gemLabel.color = enough ? ReferenceUiVisuals.Hex(0x2A1A08) : ReferenceUiVisuals.Hex(0xB0301E);
    }

    private void OnDestroy() { if (Current == this) Current = null; }
}
