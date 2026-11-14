using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Taşımadan sonra çıkan "GERİ AL" düğmesi — Ana Üs binası, Ana Üs dekoru ve Ada aynı bileşeni kullanır.
/// 5. oturum (2.3): eski 188×58'lik hap ekranın ortasında kahramanı ve binaları kapatıyordu. Artık küçük yuvarlak düğme (52 çap,
/// alanı eskisinin ~%40'ı): Tiny Swords turuncu geri oku, çevresinde kalan süreyle AZALAN altın halka, altında kısa etiket.
/// Sol alt köşede, alt menünün (radyal yay dahil) üstünde, çit dışındaki ağaç şeridinde durur; oyun alanını kapatmaz.
/// 3 sn kalır, sonra yumuşakça söner (0,35 sn); dokununca hemen geri alır.
/// </summary>
public sealed class UndoMoveButton : MonoBehaviour
{
    public static UndoMoveButton Current { get; private set; }
    public const float DefaultSeconds = 3f;
    public const float Diameter = 52f;
    /// <summary>Güvenli alanın sol alt köşesine göre merkez (radyal menü yayının ve etiketlerinin üstünde).</summary>
    public static readonly Vector2 Anchor = new Vector2(44f, 238f);
    private const float FadeSeconds = 0.35f;

    private Action undo;
    private float until, seconds;
    private Image timer;
    private CanvasGroup group;
    private bool fading;

    public bool Visible => this != null && gameObject.activeInHierarchy;
    /// <summary>Kalan süre oranı (1 → 0); halka bununla boşalır.</summary>
    public float Remaining => timer != null ? timer.fillAmount : 0f;

    public static UndoMoveButton Show(Transform safe, Action onUndo, float seconds = DefaultSeconds)
    {
        if (Current != null) Destroy(Current.gameObject);
        if (safe == null) return null;
        const float labelH = 16f;
        // Kök: saydam kap (dokunma alanı = halka + etiket); sol-alt köşeye sabit
        var root = new GameObject("MoveUndoToast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(safe, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Diameter + 14f, Diameter + labelH + 6f);
        rt.anchoredPosition = Anchor + new Vector2(0f, -labelH * 0.5f);
        var hit = root.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        // Halka: gölge + koyu zemin + azalan altın süre halkası + iç yuva + ikon
        var iconRing = ReferenceUiVisuals.Image(rt, "IconRing", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-Diameter * 0.5f, -Diameter), new Vector2(Diameter * 0.5f, 0f), new Color(0.06f, 0.04f, 0.02f, 0.92f));
        iconRing.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        iconRing.raycastTarget = false;
        var shadow = ReferenceUiVisuals.Image(iconRing.transform, "Shadow", Vector2.zero, Vector2.one, new Vector2(1f, -4f), new Vector2(-1f, -2f),
            new Color(0f, 0f, 0f, 0.35f));
        shadow.sprite = iconRing.sprite;
        shadow.raycastTarget = false;
        shadow.transform.SetAsFirstSibling();
        var fill = ReferenceUiVisuals.Image(iconRing.transform, "Timer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
        fill.sprite = iconRing.sprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        fill.raycastTarget = false;
        var well = ReferenceUiVisuals.Image(iconRing.transform, "Well", Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f),
            ReferenceUiVisuals.Hex(0x2A1A0E));
        well.sprite = iconRing.sprite;
        well.raycastTarget = false;
        var icon = ReferenceUiVisuals.Image(well.transform, "Icon", Vector2.zero, Vector2.one, new Vector2(8f, 9f), new Vector2(-8f, -7f), Color.white);
        icon.sprite = CampaignSceneSetup.Art("ui_icon_undo");   // Tiny Swords Icon_08: turuncu geri oku
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        // Etiket: halkanın altında küçük koyu hap
        var pill = ReferenceUiVisuals.Image(rt, "LabelPill", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-31f, 0f), new Vector2(31f, labelH), new Color(0.10f, 0.07f, 0.04f, 0.9f));
        pill.sprite = ReferenceUiVisuals.SolidPillSprite();
        pill.type = Image.Type.Sliced;
        pill.raycastTarget = false;
        var label = ReferenceUiVisuals.Text(pill.transform, "Label", L.T("GERİ AL", "UNDO"), 11, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(3f, 0f), new Vector2(-3f, 0f), true);
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 11f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        var button = root.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        root.AddComponent<ReferenceButtonPress>();
        var component = root.AddComponent<UndoMoveButton>();
        component.undo = onUndo;
        component.seconds = Mathf.Max(0.5f, seconds);
        component.until = Time.unscaledTime + component.seconds;
        component.timer = fill;
        component.group = root.GetComponent<CanvasGroup>();
        button.onClick.AddListener(component.Press);
        root.transform.SetAsLastSibling();
        root.transform.localScale = Vector3.one * 0.6f;
        root.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(root);
        Current = component;
        return component;
    }

    /// <summary>Geri al: işlemi uygular ve kapanır.</summary>
    public void Press()
    {
        if (fading) return;
        var action = undo;
        undo = null;
        Close();
        action?.Invoke();
    }

    public void Close()
    {
        if (Current == this) Current = null;
        if (this != null) Destroy(gameObject);
    }

    private void Update()
    {
        float left = until - Time.unscaledTime;
        if (timer != null) timer.fillAmount = Mathf.Clamp01(left / seconds);
        if (left > 0f || fading) return;
        // Süre bitti: yumuşakça söner, sonra kapanır (sönerken dokunuş geri almaz)
        fading = true;
        if (Current == this) Current = null;
        if (group != null) group.interactable = group.blocksRaycasts = false;
        transform.DOScale(0.85f, FadeSeconds).SetUpdate(true).SetLink(gameObject);
        if (group != null) group.DOFade(0f, FadeSeconds).SetUpdate(true).SetLink(gameObject).OnComplete(Close);
        else Close();
    }

    private void OnDestroy() { if (Current == this) Current = null; }
}
