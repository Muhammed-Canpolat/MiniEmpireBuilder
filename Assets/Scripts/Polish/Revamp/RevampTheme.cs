// [REVAMP THEME] — Mini Empire Builder
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "MiniEmpire/Theme/Revamp Theme", fileName = "RevampTheme")]
public class RevampTheme : ScriptableObject
{
    [Header("Palette")]
    [SerializeField] private Color primaryGold = new Color32(0xD4, 0xA0, 0x17, 0xFF);
    [SerializeField] private Color darkBg = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
    [SerializeField] private Color panelBg = new Color32(0x12, 0x14, 0x24, 0xF2);
    [SerializeField] private Color lightText = new Color32(0xF0, 0xE6, 0xD3, 0xFF);
    [SerializeField] private Color danger = new Color32(0x8B, 0x00, 0x00, 0xFF);

    [Header("Typography")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private int titleSize = 36;
    [SerializeField] private int subtitleSize = 22;
    [SerializeField] private int bodySize = 18;

    [Header("Motion")]
    [SerializeField] private float panelInDuration = 0.35f;
    [SerializeField] private float pulseDuration = 0.8f;

    public Color PrimaryGold => primaryGold;
    public Color DarkBg => darkBg;
    public Color PanelBg => panelBg;
    public Color LightText => lightText;
    public Color Danger => danger;

    public TMP_FontAsset TitleFont => titleFont;
    public TMP_FontAsset BodyFont => bodyFont;
    public int TitleSize => titleSize;
    public int SubtitleSize => subtitleSize;
    public int BodySize => bodySize;

    public float PanelInDuration => panelInDuration;
    public float PulseDuration => pulseDuration;
}
