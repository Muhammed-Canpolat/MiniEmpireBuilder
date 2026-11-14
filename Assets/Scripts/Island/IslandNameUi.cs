using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ada adı penceresi (AŞAMA 3 §3B). Üç kip: İlk (yeni oyuncu, bedava) · Teklif (eski kayıt: "Adın adı Martı Adası. Değiştirmek ister misin?") ·
/// Değiştir (üst çubuktaki ada adına dokunma; 3. değişiklikten itibaren elmasla). Kurallar <see cref="IslandNameRules"/>.
/// </summary>
public sealed class IslandNameUi : MonoBehaviour
{
    public enum Mode { First, Offer, Rename }

    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static readonly Color InkMuted = ReferenceUiVisuals.Hex(0x6B5236);

    /// <summary>Test kancası: geliştirici modunda da ad sorusu çıksın (varsayılan: dev modunda sorulmaz, diğer testleri bölmesin).</summary>
    public static bool OfferInDev;

    private static IslandNameUi open;
    public static bool IsOpen => open != null;
    public static IslandNameUi Current => open;

    private IslandSaveData save;
    private Mode mode;
    private Action onApplied;
    private TMP_InputField field;
    private TextMeshProUGUI errorText, titleText, bodyText;
    private Button okButton;
    private TextMeshProUGUI okLabel;
    private GameObject offerGroup, editGroup;
    private RectTransform panelRect;
    private Image gemIcon;

    public string FieldText { get => field != null ? field.text : ""; set { if (field != null) field.text = value; } }
    public string ErrorText => errorText != null ? errorText.text : "";
    public Button OkButton => okButton;

    /// <summary>Ada açıldığında hangi kipin sorulacağı; sorulmayacaksa null.</summary>
    public static Mode? OfferFor(IslandSaveData save)
    {
        if (save == null || !FeatureConfig.Instance.islandNaming || save.nameOffered) return null;
        return save.freshStart ? Mode.First : Mode.Offer;
    }

    public static IslandNameUi Open(IslandSaveData save, Mode mode, Action onApplied = null)
    {
        Close();
        if (save == null) return null;
        var root = new GameObject("IslandNameModal", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        var ui = root.AddComponent<IslandNameUi>();
        open = ui;
        ui.save = save;
        ui.mode = mode;
        ui.onApplied = onApplied;
        if (mode != Mode.Rename && !save.nameOffered) { save.nameOffered = true; GameManager.Instance?.SaveGame(); }   // bir kez sorulur
        ui.Build(ReferenceUiVisuals.SafeArea(canvas.transform));
        return ui;
    }

    public static void Close()
    {
        if (open == null) return;
        var go = open.gameObject;
        open = null;
        Destroy(go);
    }

    private void Build(RectTransform safe)
    {
        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.62f));
        dim.raycastTarget = true;
        var center = new Vector2(0.5f, 0.56f);
        var panel = ReferenceUiVisuals.Image(safe, "Panel", center, center, new Vector2(-165, -128), new Vector2(165, 128), Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;
        panelRect = panel.rectTransform;

        titleText = ReferenceUiVisuals.Text(panel.transform, "Title", "", 20, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(54, -58), new Vector2(-54, -18), true);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.fontSizeMin = 13f;

        // Kapat (ilk adlandırmada/tekliflerde "sonra" anlamına gelir; ad varsayılan kalır)
        var closeRect = ReferenceUiVisuals.Rect(panel.transform, "Close", Vector2.one, Vector2.one, new Vector2(-46, -46), new Vector2(-8, -8));
        var closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        closeImage.color = closeImage.sprite != null ? Color.white : ReferenceUiVisuals.Red;
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Dismiss);
        ReferenceUiVisuals.Text(closeRect, "X", "X", 16, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);

        // --- Teklif (eski kayıt) ---
        offerGroup = ReferenceUiVisuals.Rect(panel.transform, "OfferGroup", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var offerBody = ReferenceUiVisuals.Text(offerGroup.transform, "Body", "", 16, InkMuted, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(24, 96), new Vector2(-24, -64), false, true);
        offerBody.text = L.T($"Adanın adı: {IslandNameRules.Display(save)}.\nDeğiştirmek ister misin?", $"Your island is called {IslandNameRules.Display(save)}.\nWould you like to rename it?");
        var change = ReferenceUiVisuals.Button(offerGroup.transform, "ChangeButton", L.T("DEĞİŞTİR", "RENAME"), true,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 74), new Vector2(-22, 124), 16);
        change.onClick.AddListener(() => SwitchToEdit());
        var keep = ReferenceUiVisuals.Button(offerGroup.transform, "KeepButton", L.T("BÖYLE KALSIN", "KEEP IT"), false,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 18), new Vector2(-22, 66), 16);
        keep.onClick.AddListener(Dismiss);

        // --- Düzenleme ---
        editGroup = ReferenceUiVisuals.Rect(panel.transform, "EditGroup", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        bodyText = ReferenceUiVisuals.Text(editGroup.transform, "Body", "", 13, InkMuted, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(22, -98), new Vector2(-22, -62), false, true);
        bodyText.textWrappingMode = TextWrappingModes.NoWrap;
        bodyText.fontSizeMin = 9f;
        BuildField(editGroup.transform);
        errorText = ReferenceUiVisuals.Text(editGroup.transform, "Error", "", 12, ReferenceUiVisuals.Red, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 78), new Vector2(-18, 100), false, true);
        errorText.textWrappingMode = TextWrappingModes.NoWrap;
        errorText.fontSizeMin = 8f;
        okButton = ReferenceUiVisuals.Button(editGroup.transform, "OkButton", "", true,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 20), new Vector2(-22, 72), 17);
        okLabel = okButton.GetComponentInChildren<TextMeshProUGUI>();
        okButton.onClick.AddListener(() => { TryApply(); });
        gemIcon = ReferenceUiVisuals.Image(okButton.transform, "GemIcon", new Vector2(0.04f, 0.18f), new Vector2(0.16f, 0.82f), Vector2.zero, Vector2.zero, Color.white);
        gemIcon.sprite = GemIcon.Sprite;
        gemIcon.preserveAspect = true;

        if (mode == Mode.Offer) { offerGroup.SetActive(true); editGroup.SetActive(false); titleText.text = L.T("ADA ADI", "ISLAND NAME"); }
        else SwitchToEdit();
    }

    private void SwitchToEdit()
    {
        offerGroup.SetActive(false);
        editGroup.SetActive(true);
        int changes = IslandNameRules.EffectiveChanges(save);
        int cost = IslandNameRules.NextCost(save);
        titleText.text = mode == Mode.First ? L.T("Adanızın adı ne olsun?", "What shall your island be called?") : L.T("Adanın adını değiştir", "Rename your island");
        int freeLeft = Mathf.Max(0, FeatureConfig.Instance.islandNameFreeChanges - changes);
        bodyText.text = cost <= 0
            ? L.T($"Ücretsiz değişiklik hakkı: {freeLeft}", $"Free renames left: {freeLeft}")
            : L.T("Bu değişiklik elmasla yapılır.", "Renaming costs gems.");
        field.text = save.islandName ?? "";
        field.placeholder.GetComponent<TextMeshProUGUI>().text = IslandNameRules.DefaultName;
        RefreshOk();
        errorText.text = "";
        if (field.gameObject.activeInHierarchy) field.ActivateInputField();
    }

    private void RefreshOk()
    {
        int cost = IslandNameRules.NextCost(save);
        var gm = GameManager.Instance;
        bool enough = cost <= 0 || (gm != null && gm.Gems >= cost);
        okLabel.text = cost <= 0 ? L.T("TAMAM", "OK") : enough ? $"{L.T("TAMAM", "OK")} · {cost}" : L.T("ELMAS YETMİYOR · ", "NOT ENOUGH GEMS · ") + $"{(gm != null ? gm.Gems : 0)}/{cost}";
        gemIcon.gameObject.SetActive(cost > 0);
    }

    private void BuildField(Transform parent)
    {
        var bg = ReferenceUiVisuals.Image(parent, "NameField", new Vector2(0, 1), Vector2.one, new Vector2(22, -150), new Vector2(-22, -108),
            ReferenceUiVisuals.Hex(0xF4EAD2));
        bg.sprite = ReferenceUiVisuals.SolidPillSprite();
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;
        var outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = ReferenceUiVisuals.Border;
        outline.effectDistance = new Vector2(2, -2);
        var viewport = ReferenceUiVisuals.Rect(bg.transform, "TextArea", Vector2.zero, Vector2.one, new Vector2(14, 4), new Vector2(-14, -4));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = ReferenceUiVisuals.Text(viewport, "Text", "", 18, Ink, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, true);
        text.enableAutoSizing = false;
        text.fontSize = 18f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        var placeholder = ReferenceUiVisuals.Text(viewport, "Placeholder", "", 18, new Color(Ink.r, Ink.g, Ink.b, 0.4f), TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, true);
        placeholder.enableAutoSizing = false;
        placeholder.fontSize = 18f;
        placeholder.fontStyle = FontStyles.Italic;
        field = bg.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.targetGraphic = bg;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = FeatureConfig.Instance.islandNameMax + 6;   // sınır doğrulamada açıklanır (yapıştırma payı)
        field.onValueChanged.AddListener(_ => { if (errorText != null) errorText.text = ""; });
        field.onSubmit.AddListener(_ => { TryApply(); });
    }

    /// <summary>TAMAM: doğrula → bedel (elmas) → yaz + kaydet.</summary>
    public bool TryApply()
    {
        if (!IslandNameRules.Validate(field.text, out string clean, out string error)) { errorText.text = error; return false; }
        int cost = mode == Mode.First ? 0 : IslandNameRules.NextCost(save);
        var gm = GameManager.Instance;
        if (cost > 0)
        {
            if (gm == null || gm.Gems < cost) { Close(); GemShopUI.Open(); return false; }
            if (!gm.SpendGems(cost)) { errorText.text = L.T("Elmas yetmiyor.", "Not enough gems."); return false; }
        }
        IslandNameRules.ApplyNaming(save, clean, mode == Mode.First);
        gm?.SaveGame();
        var done = onApplied;
        Close();
        done?.Invoke();
        return true;
    }

    /// <summary>Kapat/BÖYLE KALSIN: ad değişmez; teklif yapıldı sayılır.</summary>
    private void Dismiss()
    {
        if (save != null && !save.nameOffered) { save.nameOffered = true; GameManager.Instance?.SaveGame(); }
        var done = onApplied;
        Close();
        done?.Invoke();
    }

    private void OnDestroy() { if (open == this) open = null; }
}
