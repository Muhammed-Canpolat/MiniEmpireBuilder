using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>D120: öğreticilerin ortak zıplayan ok kodu (IslandBuildTutorial + akın tanıtımı). Ok aşağı bakar, hedefin üstüne konur.</summary>
public static class TutorialPointer
{
    /// <summary>Sprite aşağı bakmıyorsa (yedek: 45° döndürülmüş Tiny Swords oku, sağ-üste bakar) aşağı bakması için ek açı.</summary>
    private static float baseAngle;

    public static RectTransform CreateArrow(Transform parent)
    {
        baseAngle = 0f;
        var arrowImg = ReferenceUiVisuals.Image(parent, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-22, -22), new Vector2(22, 22), Color.white);
        arrowImg.sprite = CampaignSceneSetup.Art("ui_arrow_down");
        if (arrowImg.sprite == null) { arrowImg.sprite = CampaignSceneSetup.Art("ui_icon_arrow"); if (arrowImg.sprite != null) baseAngle = -135f; }
        arrowImg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
        if (arrowImg.sprite == null)
        {
            // Yedek: altın "▼"
            arrowImg.color = new Color(0, 0, 0, 0);
            ReferenceUiVisuals.Text(arrowImg.transform, "Glyph", "▼", 40, ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(-10, -10), new Vector2(10, 10), true);
        }
        arrowImg.raycastTarget = false;
        return arrowImg.rectTransform;
    }

    /// <summary>Oku hedefe çevirir: aşağı (hedef altta) ya da 180° yukarı (hedef üstte).</summary>
    public static void Rotate(RectTransform arrow, bool pointsUp) => arrow.localRotation = Quaternion.Euler(0f, 0f, baseAngle + (pointsUp ? 180f : 0f));

    /// <summary>Zıplama kayması (birim).</summary>
    public static float Bob() => Mathf.Sin(Time.unscaledTime * 6f) * 8f;
}
