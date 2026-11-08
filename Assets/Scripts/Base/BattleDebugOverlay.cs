#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Small battle viewport diagnostic and apprentice position marker.</summary>
public sealed class BattleDebugOverlay : MonoBehaviour
{
    private GameObject panel;
    private TextMeshProUGUI label;
    private SpriteRenderer dot;

    private System.Collections.IEnumerator Start()
    {
        Transform canvas = null;
        for (int i = 0; i < 120 && canvas == null; i++)
        {
            canvas = GameObject.Find("BattleCanvas")?.transform.Find("SafeArea");
            if (canvas == null) yield return null;
        }
        if (canvas == null) { Destroy(gameObject); yield break; }
        var background = ReferenceUiVisuals.Image(canvas, "BattleDebugReadout",
            new Vector2(0.02f, 0.17f), new Vector2(0.98f, 0.17f),
            Vector2.zero, new Vector2(0, 145), new Color(0.08f, 0.06f, 0.04f, 0.84f));
        panel = background.gameObject;
        label = ReferenceUiVisuals.Text(panel.transform, "Text", "", 10, Color.white,
            TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one,
            new Vector2(5, 3), new Vector2(-5, -3));
        label.enableAutoSizing = false;
        var marker = new GameObject("ApprenticeDebugDot");
        marker.transform.SetParent(transform, false);
        dot = marker.AddComponent<SpriteRenderer>();
        dot.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        dot.color = Color.cyan;
        dot.sortingOrder = 950;
        marker.transform.localScale = Vector3.one * 0.25f;
        SetVisible(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            SetVisible(!panel.activeSelf);
        if (panel == null || !panel.activeSelf) return;
        var apprentice = FindFirstObjectByType<BattleApprentice>();
        dot.enabled = apprentice != null;
        if (apprentice != null) dot.transform.position = apprentice.transform.position + Vector3.up * 0.8f;
        var bars = FindFirstObjectByType<WorldViewportBars>();
        var camera = Camera.main;
        var sb = new StringBuilder(500);
        sb.AppendLine($"F1 {Screen.width}×{Screen.height} safe={Screen.safeArea}");
        sb.AppendLine($"Bar üst={Height(bars?.TopBar):F0}px alt={Height(bars?.BottomBar):F0}px");
        sb.AppendLine($"Cam={camera.pixelRect} aspect={camera.aspect:F2} ortho={camera.orthographicSize:F2}");
        sb.AppendLine($"Çırak={(apprentice != null ? apprentice.transform.position.ToString() : "yok")}");
        sb.Append("Raycast UI: ");
        foreach (var image in FindObjectsByType<Image>(FindObjectsSortMode.None))
        {
            if (!image.isActiveAndEnabled || !image.raycastTarget) continue;
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            sb.Append($"{image.name}[{corners[0].x:F0},{corners[0].y:F0},{corners[2].x:F0},{corners[2].y:F0}] ");
        }
        label.text = sb.ToString();
    }

    private void SetVisible(bool visible)
    {
        if (panel != null) panel.SetActive(visible);
        if (dot != null) dot.enabled = visible;
    }

    private static float Height(RectTransform rect)
    {
        if (rect == null) return 0f;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[2].y - corners[0].y;
    }
}
#endif
