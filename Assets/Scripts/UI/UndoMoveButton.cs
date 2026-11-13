using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3. oturum (2.5): taşımadan sonra çıkan "GERİ AL" düğmesi — Ana Üs ve Ada aynı bileşeni kullanır. Oyunun stil ailesinde:
/// altın kenarlı koyu hap, solda altın halkalı yuvarlak ikon (geri oku), sağda büyük harf etiket, altta kalan süreyi gösteren
/// altın dolum çizgisi. Süre (varsayılan 4 sn) bitince ya da dokununca kapanır. Eski düz koyu dikdörtgenin yerini alır.
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
        var rim = ReferenceUiVisuals.Image(safe, "MoveUndoToast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-82f, 214f), new Vector2(82f, 260f), ReferenceUiVisuals.Gold);
        rim.sprite = ReferenceUiVisuals.SolidPillSprite();
        rim.type = Image.Type.Sliced;
        rim.raycastTarget = true;
        var face = ReferenceUiVisuals.Image(rim.transform, "Face", Vector2.zero, Vector2.one, new Vector2(2.5f, 2.5f), new Vector2(-2.5f, -2.5f),
            ReferenceUiVisuals.Hex(0x2A1A0E));
        face.sprite = ReferenceUiVisuals.SolidPillSprite();
        face.type = Image.Type.Sliced;
        face.raycastTarget = false;
        // Sol: altın halkalı yuvarlak ikon yuvası
        var ring = ReferenceUiVisuals.Image(rim.transform, "IconRing", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(-6f, -25f), new Vector2(44f, 25f), ReferenceUiVisuals.Gold);
        ring.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        ring.raycastTarget = false;
        var well = ReferenceUiVisuals.Image(ring.transform, "Well", Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f),
            ReferenceUiVisuals.Hex(0x3B2A16));
        well.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        well.raycastTarget = false;
        var icon = ReferenceUiVisuals.Image(well.transform, "Icon", Vector2.zero, Vector2.one, new Vector2(9f, 9f), new Vector2(-9f, -9f), Color.white);
        icon.sprite = CampaignSceneSetup.Art("ui_icon_play");   // oynat oku 180° döner: "geri" (özel geri al simgesi ART_NEEDS'te)
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        var label = ReferenceUiVisuals.Text(rim.transform, "Label", L.T("GERİ AL", "UNDO"), 17, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(46f, 4f), new Vector2(-10f, -2f), true);
        label.raycastTarget = false;
        // Kalan süre çizgisi
        var track = ReferenceUiVisuals.Image(rim.transform, "TimerTrack", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(26f, 5f), new Vector2(-14f, 8f), new Color(0f, 0f, 0f, 0.45f));
        track.raycastTarget = false;
        var fill = ReferenceUiVisuals.Image(track.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
        fill.sprite = ReferenceUiVisuals.SolidPillSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.raycastTarget = false;

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
