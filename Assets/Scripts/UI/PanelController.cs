using DG.Tweening;
using UnityEngine;

public static class UIState
{
    public static bool IsModalOpen => PanelController.OpenModal != null;
}

/// <summary>Owns visibility and raycasts for a sliding UI panel.</summary>
public sealed class PanelController : MonoBehaviour
{
    public static PanelController OpenModal { get; private set; }

    [SerializeField] private bool modal = true;
    [SerializeField] private float duration = 0.25f;
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 openPosition;
    private Tween motion;
    private int transition;

    public bool IsOpen { get; private set; }

    public void Initialize(bool isModal = true)
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        modal = isModal;
        openPosition = rect.anchoredPosition;
        HideImmediate();
    }

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private float ClosedY()
    {
        var canvas = GetComponentInParent<Canvas>(true);
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float safeBottom = Screen.safeArea.yMin / Mathf.Max(0.01f, scale);
        float topAnchorOffset = rect.anchorMax.y > 0.5f && rect.parent is RectTransform parent
            ? parent.rect.height * rect.anchorMax.y : 0f;
        return -(rect.rect.height + safeBottom + 20f / Mathf.Max(0.01f, scale)
            + topAnchorOffset);
    }

    public void Show()
    {
        if (rect == null) Awake();
        if (modal && OpenModal != null && OpenModal != this)
            OpenModal.HideImmediate();
        motion?.Kill();
        transition++;
        if (!gameObject.activeSelf)
        {
            rect.anchoredPosition = new Vector2(openPosition.x, ClosedY());
            gameObject.SetActive(true);
        }
        if (modal) OpenModal = this;
        IsOpen = true;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        motion = rect.DOAnchorPos(openPosition, duration).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void Hide()
    {
        if (rect == null) Awake();
        if (!gameObject.activeSelf) { HideImmediate(); return; }
        motion?.Kill();
        int version = ++transition;
        IsOpen = false;
        if (OpenModal == this) OpenModal = null;
        group.interactable = false;
        group.blocksRaycasts = false;
        motion = rect.DOAnchorPosY(ClosedY(), duration).SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() =>
            {
                if (version != transition || this == null) return;
                group.alpha = 0f;
                gameObject.SetActive(false);
            });
    }

    public void HideImmediate()
    {
        if (rect == null) Awake();
        motion?.Kill();
        transition++;
        IsOpen = false;
        if (OpenModal == this) OpenModal = null;
        rect.anchoredPosition = new Vector2(openPosition.x, ClosedY());
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        motion?.Kill();
        if (OpenModal == this) OpenModal = null;
    }
}
