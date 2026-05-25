// [COLOR PALETTE] — Mini Empire Builder
using UnityEngine;

[CreateAssetMenu(menuName = "MiniEmpire/Theme/Color Palette", fileName = "ColorPalette")]
public class ColorPalette : ScriptableObject
{
    [SerializeField] private Color primaryGold = new Color32(0xD4, 0xA0, 0x17, 0xFF);
    [SerializeField] private Color dangerRed = new Color32(0x8B, 0x00, 0x00, 0xFF);
    [SerializeField] private Color darkBg = new Color32(0x1A, 0x1A, 0x2E, 0xFF);
    [SerializeField] private Color lightText = new Color32(0xF0, 0xE6, 0xD3, 0xFF);

    public Color PrimaryGold => primaryGold;
    public Color DangerRed => dangerRed;
    public Color DarkBg => darkBg;
    public Color LightText => lightText;
}
