// [RESOURCE HUD] — Mini Empire Builder
using System.Collections;
using TMPro;
using UnityEngine;

public class ResourceHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI rateText;
    [SerializeField] private TextMeshProUGUI popupText;
    [SerializeField] private Color increaseColor = new Color32(0x4C, 0xAF, 0x50, 0xFF);
    [SerializeField] private Color decreaseColor = new Color32(0x8B, 0x00, 0x00, 0xFF);
    [SerializeField] private Color normalColor = Color.white;

    private int previousValue;

    /// <summary>
    /// Updates amount and production rate labels with change feedback.
    /// </summary>
    public void SetResource(int value, float productionPerSec)
    {
        int delta = value - previousValue;
        previousValue = value;

        if (amountText != null)
        {
            amountText.text = value.ToString();
            if (delta > 0)
            {
                amountText.color = increaseColor;
            }
            else if (delta < 0)
            {
                amountText.color = decreaseColor;
            }
            else
            {
                amountText.color = normalColor;
            }
        }

        if (rateText != null)
        {
            rateText.text = $"+{productionPerSec:0.0}/sn";
        }

        if (delta != 0)
        {
            StartCoroutine(ShowPopup(delta));
        }
    }

    private IEnumerator ShowPopup(int delta)
    {
        if (popupText == null)
        {
            yield break;
        }

        popupText.gameObject.SetActive(true);
        popupText.text = delta > 0 ? $"+{delta}" : delta.ToString();
        popupText.color = delta > 0 ? increaseColor : decreaseColor;

        float t = 0f;
        Color start = popupText.color;
        Vector3 origin = popupText.rectTransform.localPosition;

        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.5f);
            popupText.rectTransform.localPosition = origin + Vector3.up * (20f * p);
            Color c = start;
            c.a = 1f - p;
            popupText.color = c;
            yield return null;
        }

        popupText.rectTransform.localPosition = origin;
        popupText.gameObject.SetActive(false);
    }
}
