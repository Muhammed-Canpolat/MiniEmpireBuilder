// [UI REVAMP APPLIER] — Mini Empire Builder
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiRevampApplier : MonoBehaviour
{
    [SerializeField] private RevampTheme theme;

    /// <summary>
    /// Assigns theme data used by the revamp pass.
    /// </summary>
    public void SetTheme(RevampTheme revampTheme)
    {
        theme = revampTheme;
    }

    /// <summary>
    /// Applies active revamp theme across tagged UI elements.
    /// </summary>
    public void ApplyNow()
    {
        if (theme == null)
            theme = ScriptableObject.CreateInstance<RevampTheme>();

        UiStyleToken[] tokens = FindObjectsByType<UiStyleToken>(FindObjectsSortMode.None);
        for (int i = 0; i < tokens.Length; i++)
        {
            ApplyToken(tokens[i]);
        }
    }

    private void ApplyToken(UiStyleToken token)
    {
        Image image = token.GetComponent<Image>();
        Button button = token.GetComponent<Button>();
        TextMeshProUGUI text = token.GetComponent<TextMeshProUGUI>();

        switch (token.StyleKind)
        {
            case UiStyleKind.ScreenBackground:
                if (image != null) image.color = theme.DarkBg;
                break;

            case UiStyleKind.TopBar:
                if (image != null) image.color = new Color(theme.DarkBg.r, theme.DarkBg.g, theme.DarkBg.b, 0.92f);
                EnsureDepth(token.transform, theme.PrimaryGold, 0.85f);
                AnimateEntrance(token.transform);
                break;

            case UiStyleKind.Panel:
                if (image != null) image.color = theme.PanelBg;
                EnsureDepth(token.transform, theme.PrimaryGold, 0.65f);
                AnimateEntrance(token.transform);
                break;

            case UiStyleKind.PrimaryButton:
                if (image != null) image.color = theme.PrimaryGold;
                if (button != null)
                {
                    ColorBlock cb = button.colors;
                    cb.normalColor = theme.PrimaryGold;
                    cb.highlightedColor = theme.PrimaryGold * 1.08f;
                    cb.pressedColor = theme.PrimaryGold * 0.85f;
                    cb.selectedColor = theme.PrimaryGold;
                    button.colors = cb;
                }
                ApplyChildTextColor(token.transform, theme.DarkBg, FontStyles.Bold);
                AnimatePulse(token.transform);
                break;

            case UiStyleKind.DangerButton:
                if (image != null) image.color = theme.Danger;
                ApplyChildTextColor(token.transform, theme.LightText, FontStyles.Bold);
                break;

            case UiStyleKind.TitleText:
                if (text != null)
                {
                    if (theme.TitleFont != null) text.font = theme.TitleFont;
                    text.fontSize = theme.TitleSize;
                    text.color = theme.PrimaryGold;
                    text.fontStyle = FontStyles.Bold;
                }
                break;

            case UiStyleKind.SubtitleText:
                if (text != null)
                {
                    if (theme.BodyFont != null) text.font = theme.BodyFont;
                    text.fontSize = theme.SubtitleSize;
                    text.color = theme.LightText;
                }
                break;

            case UiStyleKind.BodyText:
                if (text != null)
                {
                    if (theme.BodyFont != null) text.font = theme.BodyFont;
                    text.fontSize = theme.BodySize;
                    text.color = theme.LightText;
                }
                break;

            case UiStyleKind.AccentText:
                if (text != null)
                {
                    if (theme.BodyFont != null) text.font = theme.BodyFont;
                    text.fontSize = theme.BodySize;
                    text.color = theme.PrimaryGold;
                    text.fontStyle = FontStyles.Bold;
                }
                break;
        }
    }

    private void AnimateEntrance(Transform target)
    {
        RectTransform rt = target as RectTransform;
        if (rt == null)
            return;

        Vector2 pos = rt.anchoredPosition;
        rt.anchoredPosition = pos + new Vector2(0f, 80f);
        rt.DOAnchorPos(pos, theme.PanelInDuration).SetEase(Ease.OutCubic);
    }

    private void AnimatePulse(Transform target)
    {
        target.DOKill();
        target.DOScale(1.04f, theme.PulseDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    private static void ApplyChildTextColor(Transform root, Color color, FontStyles fontStyle)
    {
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].color = color;
            texts[i].fontStyle = fontStyle;
        }
    }

    private static void EnsureDepth(Transform target, Color accentColor, float accentAlpha)
    {
        RectTransform targetRt = target as RectTransform;
        if (targetRt == null || target.Find("RevampShadow") != null)
            return;

        GameObject shadow = new GameObject("RevampShadow");
        shadow.transform.SetParent(target, false);
        shadow.transform.SetAsFirstSibling();
        RectTransform shadowRt = shadow.AddComponent<RectTransform>();
        shadowRt.anchorMin = Vector2.zero;
        shadowRt.anchorMax = Vector2.one;
        shadowRt.offsetMin = new Vector2(5f, -5f);
        shadowRt.offsetMax = new Vector2(5f, -5f);
        Image shadowImg = shadow.AddComponent<Image>();
        shadowImg.color = new Color(0f, 0f, 0f, 0.28f);
        shadowImg.raycastTarget = false;

        GameObject accent = new GameObject("RevampAccent");
        accent.transform.SetParent(target, false);
        RectTransform accentRt = accent.AddComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 0f);
        accentRt.anchorMax = new Vector2(1f, 0.035f);
        accentRt.offsetMin = Vector2.zero;
        accentRt.offsetMax = Vector2.zero;
        Image accentImg = accent.AddComponent<Image>();
        accentColor.a = accentAlpha;
        accentImg.color = accentColor;
        accentImg.raycastTarget = false;
    }
}
