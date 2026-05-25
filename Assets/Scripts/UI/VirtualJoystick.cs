using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Mobil joystick girdisini kahraman hareketine çevirir.
/// Dokunulmadığında görünmez (alpha 0), dokunulduğunda görünür (alpha 1).
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform handle;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private float maxRadius = 90f;

    private RectTransform rectTransform;
    private Vector2 currentInput;
    private HeroController heroController;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (visualRoot == null)
            visualRoot = rectTransform;

        canvasGroup = visualRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = visualRoot.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        if (visualRoot != rectTransform)
            visualRoot.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (heroController == null)
            heroController = FindFirstObjectByType<HeroController>();

        if (heroController != null)
        {
            heroController.SetMoveInput(currentInput);
        }
        else
        {
            // Ana menü (Base) için HeroMovementController ara
            HeroMovementController hmc = FindFirstObjectByType<HeroMovementController>();
            if (hmc != null)
            {
                hmc.SetMoveInput(currentInput);
            }
        }
    }

    public void Setup(RectTransform visualRect, RectTransform handleRect, float radius)
    {
        visualRoot = visualRect;
        handle = handleRect;
        maxRadius = radius;

        if (visualRoot != null)
        {
            canvasGroup = visualRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = visualRoot.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            if (visualRoot != rectTransform)
                visualRoot.gameObject.SetActive(false);
        }
    }

    private bool _isIgnoredTouch = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isIgnoredTouch = false;

        if (visualRoot == null || rectTransform == null)
            return;

        // Base sahnesinde miyiz ve altında bina var mı kontrolü
        HeroMovementController hmc = FindFirstObjectByType<HeroMovementController>();
        if (hmc != null && eventData.pressEventCamera != null)
        {
            Vector3 worldPos = eventData.pressEventCamera.ScreenToWorldPoint(eventData.position);
            worldPos.z = 0;
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
            if (hit.collider != null)
            {
                var baseMapManager = FindFirstObjectByType<BaseMapManager>();
                if (baseMapManager != null)
                {
                    var mapObj = baseMapManager.GetMapObject(hit.collider.gameObject);
                    if (mapObj != null && (mapObj.objectType == MapObjectType.Building || mapObj.objectType == MapObjectType.Tree || mapObj.objectType == MapObjectType.Rock))
                    {
                        _isIgnoredTouch = true;
                        return; // Joystick'i çalıştırma, altındaki objeye bırak
                    }
                }
            }
        }

        RectTransform parentRt = visualRoot.parent as RectTransform;
        if (parentRt == null)
            parentRt = rectTransform;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            visualRoot.anchoredPosition = localPoint;
        }

        visualRoot.gameObject.SetActive(true);
        canvasGroup.DOKill();
        canvasGroup.DOFade(1f, 0.1f);
        ProcessPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_isIgnoredTouch) return;
        ProcessPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isIgnoredTouch) return;
        
        currentInput = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
            {
                if (visualRoot != null)
                    visualRoot.gameObject.SetActive(false);
            });
        }
    }

    private void ProcessPointer(PointerEventData eventData)
    {
        if (visualRoot == null)
            return;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                visualRoot, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return;
        }

        Vector2 clamped = Vector2.ClampMagnitude(localPoint, maxRadius);
        currentInput = clamped / maxRadius;

        if (handle != null)
            handle.anchoredPosition = clamped;
    }
}
