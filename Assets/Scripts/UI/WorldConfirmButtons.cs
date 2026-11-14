using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Yerleştirme/taşıma onayı: alt bardaki "YERLEŞTİR / İPTAL" çubuğunun yerine, yerleştirilen nesnenin hemen üstünde yüzen iki
/// yuvarlak düğme — ✓ (onay, yeşil Tiny Swords tarzı tik) ve ✗ (iptal, Tiny Swords Icon_09). Altın halkalı koyu yüz (HUD ailesi,
/// radyal menüyle aynı). Düğmelerin üstünde küçük bilgi hapı: bedel (simge + sayı) ya da neden konamadığı (kırmızı).
/// Hayalet sürüklendikçe izler; ekranın dışına taşmaz (üst bar/alt bar payı). Ana Üs binası, Ana Üs dekoru ve Ada aynı bileşeni kullanır.
/// </summary>
public sealed class WorldConfirmButtons : MonoBehaviour
{
    public const float ButtonSize = 58f;
    private const float Gap = 18f;

    private Func<Vector3> anchor;
    private Camera cam;
    private RectTransform rect, parentRect;
    private Button confirm;
    private Image confirmFace, confirmIcon;
    private GameObject infoPill;
    private Image infoIcon;
    private TextMeshProUGUI infoText;
    private string lastInfo;
    private float InfoWidth;

    public Button Confirm => confirm;
    public bool CanConfirm => confirm != null && confirm.interactable;
    public string Info => infoPill != null && infoPill.activeSelf && infoText != null ? infoText.text : "";

    /// <param name="safe">Tuvalin güvenli alanı.</param>
    /// <param name="worldTop">Nesnenin üst orta noktası (dünya); her kare okunur.</param>
    public static WorldConfirmButtons Create(Transform safe, Func<Vector3> worldTop, Camera camera, UnityAction onConfirm, UnityAction onCancel,
        string name = "PlacementConfirm")
    {
        if (safe == null) return null;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(safe, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(ButtonSize * 2f + Gap, ButtonSize + 40f);
        var c = go.AddComponent<WorldConfirmButtons>();
        c.anchor = worldTop;
        c.cam = camera;
        c.rect = rt;
        c.parentRect = (RectTransform)safe;
        c.confirm = c.RoundButton("Confirm", -(ButtonSize + Gap) * 0.5f, CampaignSceneSetup.Art("ui_icon_check"),
            new Color(0.17f, 0.24f, 0.13f, 0.96f), onConfirm, out c.confirmFace, out c.confirmIcon);
        c.RoundButton("Cancel", (ButtonSize + Gap) * 0.5f, CampaignSceneSetup.Art("ui_icon_cross"),
            new Color(0.25f, 0.12f, 0.10f, 0.96f), onCancel, out _, out _);
        c.BuildInfoPill();
        c.Follow();
        rt.localScale = Vector3.one * 0.6f;
        rt.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(go);
        return c;
    }

    private Button RoundButton(string name, float x, Sprite icon, Color faceColor, UnityAction action, out Image face, out Image picture)
    {
        var ring = ReferenceUiVisuals.Image(rect, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(x - ButtonSize * 0.5f, 0f), new Vector2(x + ButtonSize * 0.5f, ButtonSize), ReferenceUiVisuals.Gold);
        ring.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        ring.raycastTarget = true;
        // Alt gölge: düğme zeminden kalkık dursun
        var shadow = ReferenceUiVisuals.Image(ring.transform, "Shadow", Vector2.zero, Vector2.one, new Vector2(1f, -4f), new Vector2(-1f, -2f),
            new Color(0f, 0f, 0f, 0.35f));
        shadow.sprite = ring.sprite;
        shadow.raycastTarget = false;
        shadow.transform.SetAsFirstSibling();
        face = ReferenceUiVisuals.Image(ring.transform, "Face", Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f), faceColor);
        face.sprite = ring.sprite;
        face.raycastTarget = false;
        picture = ReferenceUiVisuals.Image(ring.transform, "Icon", Vector2.zero, Vector2.one, new Vector2(11f, 11f), new Vector2(-11f, -11f), Color.white);
        picture.sprite = icon;
        picture.preserveAspect = true;
        picture.raycastTarget = false;
        var button = ring.gameObject.AddComponent<Button>();
        button.targetGraphic = ring;
        button.transition = Selectable.Transition.None;
        ring.gameObject.AddComponent<ReferenceButtonPress>();
        button.onClick.AddListener(action);
        return button;
    }

    private void BuildInfoPill()
    {
        var rim = ReferenceUiVisuals.Image(rect, "Info", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-90f, ButtonSize + 8f), new Vector2(90f, ButtonSize + 36f), new Color(ReferenceUiVisuals.Gold.r, ReferenceUiVisuals.Gold.g, ReferenceUiVisuals.Gold.b, 0.85f));
        rim.sprite = ReferenceUiVisuals.SolidPillSprite();
        rim.type = Image.Type.Sliced;
        rim.raycastTarget = false;
        var face = ReferenceUiVisuals.Image(rim.transform, "Face", Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f),
            new Color(0.10f, 0.07f, 0.04f, 0.92f));
        face.sprite = ReferenceUiVisuals.SolidPillSprite();
        face.type = Image.Type.Sliced;
        face.raycastTarget = false;
        infoIcon = ReferenceUiVisuals.Image(rim.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -10f), new Vector2(28f, 10f), Color.white);
        infoIcon.preserveAspect = true;
        infoIcon.raycastTarget = false;
        infoText = ReferenceUiVisuals.Text(rim.transform, "Text", "", 13, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(10f, 1f), new Vector2(-10f, -1f), bodyBold: true);
        infoText.enableAutoSizing = true;
        infoText.fontSizeMin = 9f;
        infoText.fontSizeMax = 13f;
        infoText.textWrappingMode = TextWrappingModes.NoWrap;
        infoText.raycastTarget = false;
        infoPill = rim.gameObject;
        infoPill.SetActive(false);
    }

    /// <summary>Onay durumu ve bilgi hapı. <paramref name="info"/> boşsa hap gizlenir; <paramref name="icon"/> doluysa metnin solunda durur.</summary>
    public void SetState(bool canConfirm, string info, Sprite icon = null, bool warning = false)
    {
        if (confirm == null) return;
        confirm.interactable = canConfirm;
        confirmFace.color = canConfirm ? new Color(0.17f, 0.24f, 0.13f, 0.96f) : new Color(0.16f, 0.15f, 0.14f, 0.9f);
        confirmIcon.color = canConfirm ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.8f);
        string key = (info ?? "") + "|" + (icon != null ? icon.name : "") + "|" + warning;
        if (key == lastInfo) return;
        lastInfo = key;
        bool show = !string.IsNullOrEmpty(info);
        infoPill.SetActive(show);
        if (!show) return;
        infoText.text = info;
        infoText.color = warning ? new Color(1f, 0.55f, 0.45f) : ReferenceUiVisuals.Light;
        infoIcon.gameObject.SetActive(icon != null);
        infoIcon.sprite = icon;
        var textRect = infoText.rectTransform;
        textRect.offsetMin = new Vector2(icon != null ? 30f : 10f, textRect.offsetMin.y);
        // Hap genişliği metne göre (84–220)
        float width = Mathf.Clamp(infoText.GetPreferredValues(info, 400f, 30f).x + (icon != null ? 44f : 24f), 84f, 220f);
        InfoWidth = width;
        var pill = (RectTransform)infoPill.transform;
        pill.offsetMin = new Vector2(-width * 0.5f, pill.offsetMin.y);
        pill.offsetMax = new Vector2(width * 0.5f, pill.offsetMax.y);
    }

    private void LateUpdate() => Follow();

    public void Follow()
    {
        if (anchor == null || parentRect == null) return;
        var camera = cam != null ? cam : Camera.main;
        if (camera == null) return;
        Vector3 screen = camera.WorldToScreenPoint(anchor());
        var canvas = parentRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCamera, out Vector2 local)) return;
        var area = parentRect.rect;
        float halfW = Mathf.Max(rect.sizeDelta.x * 0.5f, InfoWidth * 0.5f) + 12f;
        // Üstte üst bar (~150), altta alt bar (~150) payı
        float x = Mathf.Clamp(local.x, area.xMin + halfW, area.xMax - halfW);
        float y = Mathf.Clamp(local.y + 10f, area.yMin + 150f, area.yMax - rect.sizeDelta.y - 150f);
        rect.anchoredPosition = new Vector2(x - area.center.x, y - area.center.y);
    }
}
