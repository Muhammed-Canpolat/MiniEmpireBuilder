using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Taşımadan sonra çıkan "GERİ AL" düğmesi — Ana Üs binası, Ana Üs dekoru ve Ada aynı bileşeni kullanır. Altın kenarlı koyu hap
/// (alt gölgeli); solda Tiny Swords turuncu geri oku, çevresinde kalan süreyle boşalan altın halka; sağda etiket. Süre (varsayılan 4 sn)
/// bitince ya da dokununca kapanır. (Eski hâl: döndürülmüş yeşil oynat oku, hapın dışına taşan halka, alttan taşan süre çizgisi.)
/// </summary>
public sealed class UndoMoveButton : MonoBehaviour
{
    public static UndoMoveButton Current { get; private set; }
    public const float DefaultSeconds = 4f;

    private Action undo;
    private float until, seconds;
    private Image timer;

    public bool Visible => this != null && gameObject.activeInHierarchy;

    /// <summary>Düğmeyi <paramref name="safe"/> alanının altına, açık radyal menü yayının ve etiketlerinin üstüne açar; öncekini kapatır.</summary>
    public static UndoMoveButton Show(Transform safe, Action onUndo, float seconds = DefaultSeconds)
    {
        if (Current != null) Destroy(Current.gameObject);
        if (safe == null) return null;
        // Hap: alt gölge + altın kenar + koyu yüz; içinde solda süre halkalı ikon yuvası, sağda etiket (taşma yok)
        const float width = 188f, height = 58f;
        var rim = ReferenceUiVisuals.Image(safe, "MoveUndoToast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-width * 0.5f, 214f), new Vector2(width * 0.5f, 214f + height), ReferenceUiVisuals.Gold);
        rim.sprite = ReferenceUiVisuals.SolidPillSprite();
        rim.type = Image.Type.Sliced;
        rim.raycastTarget = true;
        var shadow = ReferenceUiVisuals.Image(rim.transform, "Shadow", Vector2.zero, Vector2.one, new Vector2(2f, -5f), new Vector2(-2f, -3f),
            new Color(0f, 0f, 0f, 0.35f));
        shadow.sprite = ReferenceUiVisuals.SolidPillSprite();
        shadow.type = Image.Type.Sliced;
        shadow.raycastTarget = false;
        shadow.transform.SetAsFirstSibling();
        var face = ReferenceUiVisuals.Image(rim.transform, "Face", Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f),
            ReferenceUiVisuals.Hex(0x2A1A0E));
        face.sprite = ReferenceUiVisuals.SolidPillSprite();
        face.type = Image.Type.Sliced;
        face.raycastTarget = false;
        // Sol: ikon yuvası — sönük halka üstünde kalan süreyi gösteren dolan altın halka (Radial360)
        const float ring = 46f;
        var iconRing = ReferenceUiVisuals.Image(rim.transform, "IconRing", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(6f, -ring * 0.5f), new Vector2(6f + ring, ring * 0.5f), new Color(0f, 0f, 0f, 0.5f));
        iconRing.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        iconRing.raycastTarget = false;
        var fill = ReferenceUiVisuals.Image(iconRing.transform, "Timer", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
        fill.sprite = iconRing.sprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;
        fill.raycastTarget = false;
        var well = ReferenceUiVisuals.Image(iconRing.transform, "Well", Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f),
            ReferenceUiVisuals.Hex(0x3B2A16));
        well.sprite = iconRing.sprite;
        well.raycastTarget = false;
        var icon = ReferenceUiVisuals.Image(well.transform, "Icon", Vector2.zero, Vector2.one, new Vector2(5f, 6f), new Vector2(-5f, -4f), Color.white);
        icon.sprite = CampaignSceneSetup.Art("ui_icon_undo");   // Tiny Swords Icon_08: turuncu geri oku
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var label = ReferenceUiVisuals.Text(rim.transform, "Label", L.T("GERİ AL", "UNDO"), 19, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(ring + 12f, 3f), new Vector2(-12f, -1f), true);
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 19f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        var button = rim.gameObject.AddComponent<Button>();
        button.targetGraphic = rim;
        button.transition = Selectable.Transition.None;
        rim.gameObject.AddComponent<ReferenceButtonPress>();
        var component = rim.gameObject.AddComponent<UndoMoveButton>();
        component.undo = onUndo;
        component.seconds = Mathf.Max(0.5f, seconds);
        component.until = Time.unscaledTime + component.seconds;
        component.timer = fill;
        button.onClick.AddListener(component.Press);
        rim.transform.SetAsLastSibling();
        rim.transform.localScale = Vector3.one * 0.6f;
        rim.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(rim.gameObject);
        Current = component;
        return component;
    }

    /// <summary>Geri al: işlemi uygular ve kapanır.</summary>
    public void Press()
    {
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
        if (left <= 0f) Close();
    }

    private void OnDestroy() { if (Current == this) Current = null; }
}
