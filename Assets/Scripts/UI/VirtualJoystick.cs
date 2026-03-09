using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mobil joystick girdisini kahraman hareketine çevirir.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform handle;
    [SerializeField] private float maxRadius = 90f;

    private RectTransform rectTransform;
    private Vector2 currentInput;
    private HeroController heroController;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (heroController == null)
            heroController = FindFirstObjectByType<HeroController>();

        if (heroController != null)
            heroController.SetMoveInput(currentInput);
    }

    public void Setup(RectTransform handleRect, float radius)
    {
        handle = handleRect;
        maxRadius = radius;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ProcessPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        ProcessPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        currentInput = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    private void ProcessPointer(PointerEventData eventData)
    {
        if (rectTransform == null)
            return;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return;
        }

        Vector2 clamped = Vector2.ClampMagnitude(localPoint, maxRadius);
        currentInput = clamped / maxRadius;

        if (handle != null)
            handle.anchoredPosition = clamped;
    }
}
