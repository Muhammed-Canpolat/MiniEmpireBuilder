// [GLOBAL UI THEME APPLIER] — Mini Empire Builder
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalUiThemeApplier : MonoBehaviour
{
    [SerializeField] private ColorPalette palette;
    [SerializeField] private TMP_FontAsset cinzelFont;
    [SerializeField] private bool applyOnStart = true;

    /// <summary>
    /// Applies palette colors and optional Cinzel font to all UI elements in scene.
    /// </summary>
    public void ApplyThemeNow()
    {
        if (palette == null)
        {
            return;
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Image img = buttons[i].targetGraphic as Image;
            if (img != null)
            {
                img.color = palette.PrimaryGold;
            }

            ColorBlock cb = buttons[i].colors;
            cb.normalColor = palette.PrimaryGold;
            cb.highlightedColor = palette.PrimaryGold * 1.08f;
            cb.pressedColor = palette.PrimaryGold * 0.85f;
            cb.selectedColor = palette.PrimaryGold;
            buttons[i].colors = cb;
        }

        TextMeshProUGUI[] texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            if (cinzelFont != null)
            {
                texts[i].font = cinzelFont;
            }

            if (texts[i].color.a > 0.01f)
            {
                texts[i].color = palette.LightText;
            }
        }

        if (Camera.main != null)
        {
            Camera.main.backgroundColor = palette.DarkBg;
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyThemeNow();
        }
    }
}
