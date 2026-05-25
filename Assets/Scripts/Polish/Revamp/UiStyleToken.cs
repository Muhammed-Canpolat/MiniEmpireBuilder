// [UI STYLE TOKEN] — Mini Empire Builder
using UnityEngine;

public enum UiStyleKind
{
    None,
    ScreenBackground,
    TopBar,
    Panel,
    PrimaryButton,
    DangerButton,
    TitleText,
    SubtitleText,
    BodyText,
    AccentText
}

public class UiStyleToken : MonoBehaviour
{
    [SerializeField] private UiStyleKind styleKind = UiStyleKind.None;

    public UiStyleKind StyleKind => styleKind;

    /// <summary>
    /// Sets style kind at runtime.
    /// </summary>
    public void SetKind(UiStyleKind kind)
    {
        styleKind = kind;
    }
}
