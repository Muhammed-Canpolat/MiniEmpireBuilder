using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Görünmez joystick için ilk savaş ipucu: "Sürükleyerek hareket et".
/// Oyuncu joystick'i bir kez sürükleyince kaybolur ve bir daha gösterilmez.
/// </summary>
public sealed class MoveHint : MonoBehaviour
{
    private const string SeenKey = "MiniEmpire_MoveHintSeen";
    private CanvasGroup group;
    private RectTransform finger;
    private bool hiding;

    public static void TryShow(Transform safeArea)
    {
        if (safeArea == null || PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
        var root = ReferenceUiVisuals.Rect(safeArea, "MoveHint", new Vector2(0.5f, 0.30f),
            new Vector2(0.5f, 0.30f), new Vector2(-130f, -34f), new Vector2(130f, 34f));
        root.gameObject.AddComponent<MoveHint>().Build(root);
    }

    private void Build(RectTransform root)
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var background = gameObject.AddComponent<Image>();
        background.sprite = ReferenceUiVisuals.ToastSprite();
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        ReferenceUiVisuals.Text(root, "Text", "Ekrana dokunup <color=#F0C24A>sürükleyerek</color> hareket et",
            15, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            new Vector2(0.18f, 0f), Vector2.one, new Vector2(0f, 4f), new Vector2(-12f, -4f), bodyBold: true);

        // Sağa sola kayan "parmak" noktası: sürükleme hareketini anlatır.
        var dot = ReferenceUiVisuals.Image(root, "Finger", new Vector2(0.10f, 0.5f), new Vector2(0.10f, 0.5f),
            new Vector2(-11f, -11f), new Vector2(11f, 11f), ReferenceUiVisuals.Gold);
        dot.sprite = ReferenceUiVisuals.SolidPillSprite();
        finger = dot.rectTransform;
        finger.DOAnchorPosX(14f, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

        group.alpha = 0f;
        group.DOFade(1f, 0.4f).SetDelay(1.2f);
        VirtualJoystick.Dragged += OnDragged;
    }

    private void OnDragged()
    {
        if (hiding) return;
        hiding = true;
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        group.DOKill();
        group.DOFade(0f, 0.35f).OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        VirtualJoystick.Dragged -= OnDragged;
        if (finger != null) finger.DOKill();
        if (group != null) group.DOKill();
    }
}
