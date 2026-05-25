// [TOOLTIP SYSTEM] — Mini Empire Builder
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TooltipSystem : MonoBehaviour
{
    private const float HoldDuration = 0.5f;
    private const string DefaultMessage = "Acmak icin: Savas Lv.3 gerekli";

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;

    private float holdTimer;
    private bool isHolding;

    private void Update()
    {
        if (IsPressStarted())
        {
            isHolding = true;
            holdTimer = 0f;
        }

        if (isHolding && IsPressing())
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= HoldDuration)
            {
                ShowTooltip(DefaultMessage);
                isHolding = false;
            }
        }

        if (IsPressReleased())
        {
            isHolding = false;
            holdTimer = 0f;

            if (tooltipPanel != null && tooltipPanel.activeSelf)
            {
                HideTooltip();
            }
        }
    }

    /// <summary>
    /// Opens tooltip panel with provided message.
    /// </summary>
    public void ShowTooltip(string message)
    {
        if (tooltipPanel == null)
            return;

        tooltipPanel.SetActive(true);
        if (tooltipText != null)
            tooltipText.text = message;
    }

    /// <summary>
    /// Hides currently open tooltip panel.
    /// </summary>
    public void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private static bool IsPressStarted()
    {
        bool mouseStarted = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool touchStarted = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return mouseStarted || touchStarted;
    }

    private static bool IsPressing()
    {
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
        return mousePressed || touchPressed;
    }

    private static bool IsPressReleased()
    {
        bool mouseReleased = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        bool touchReleased = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
        return mouseReleased || touchReleased;
    }
}
