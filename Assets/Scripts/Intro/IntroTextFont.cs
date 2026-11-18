using TMPro;
using UnityEngine;

/// <summary>
/// Intro sahnesi yazılarına (yüzde + ipucu) projenin iOS-güvenli dinamik Türkçe fontunu atar
/// (ReferenceUiVisuals.ApplyFont → AlegreyaSans-Bold, TmpFontBootstrap zinciri; D113). Yeni font asset'i eklenmez,
/// yazılar sprite'a pişirilmez. Siyah kontur font atandıktan sonra uygulanır.
/// </summary>
public sealed class IntroTextFont : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] texts;
    [SerializeField] private float outlineWidth = 0.25f;
    [SerializeField] private Color outlineColor = Color.black;

    private void Awake()
    {
        if (texts == null) return;
        foreach (var t in texts)
        {
            if (t == null) continue;
            ReferenceUiVisuals.ApplyFont(t, t.fontSize, t.color, false, true);
            if (outlineWidth > 0f) { t.outlineWidth = outlineWidth; t.outlineColor = outlineColor; }
        }
    }
}
