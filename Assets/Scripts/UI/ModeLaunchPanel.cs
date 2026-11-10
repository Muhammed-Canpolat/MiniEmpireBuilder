using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Özel mod başlatma penceresi (Sonsuz Sefer, Ejder İstilası): başlık, açıklama satırları,
/// başlat düğmesi ve kapatma. Kendi tuvali var (sefer haritasının üstünde).
/// </summary>
public sealed class ModeLaunchPanel : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static ModeLaunchPanel open;

    public static void Show(string title, string body, string startLabel, bool canStart, Action onStart,
        string ribbon = "ui_ribbon_purple")
    {
        Close();
        var root = new GameObject("ModeLaunchPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        open = root.AddComponent<ModeLaunchPanel>();
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(safe, "Panel", center, center, new Vector2(-165, -175), new Vector2(165, 175),
            Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;
        panel.transform.localScale = Vector3.one * 0.85f;
        panel.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetLink(panel.gameObject);

        var ribbonImage = ReferenceUiVisuals.Image(panel.transform, "Ribbon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-125, -44), new Vector2(125, 14), Color.white);
        ribbonImage.sprite = CampaignSceneSetup.Art(ribbon);
        ribbonImage.preserveAspect = true;
        var titleLabel = ReferenceUiVisuals.Text(ribbonImage.transform, "Title", title, 17, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.9f), Vector2.zero, Vector2.zero, true);
        var shadow = titleLabel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.05f, 0.02f, 0.85f);
        shadow.effectDistance = new Vector2(1.6f, -1.6f);

        var bodyText = ReferenceUiVisuals.Text(panel.transform, "Body", body, 14, Ink, TextAlignmentOptions.TopLeft,
            Vector2.zero, Vector2.one, new Vector2(24, 86), new Vector2(-24, -58), false, true);
        bodyText.richText = true;

        var start = ReferenceUiVisuals.Button(panel.transform, "Start", startLabel, canStart,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 20), new Vector2(-22, 72), 18);
        start.interactable = canStart;
        start.onClick.AddListener(() =>
        {
            Close();
            onStart?.Invoke();
        });

        var closeRect = ReferenceUiVisuals.Rect(panel.transform, "Close", Vector2.one, Vector2.one,
            new Vector2(-44, -44), new Vector2(-6, -6));
        var closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close);
        ReferenceUiVisuals.Text(closeRect, "X", "X", 16, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
    }

    public static void Close()
    {
        if (open != null) Destroy(open.gameObject);
        open = null;
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }
}
