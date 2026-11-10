using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Elmas harcamadan önce sorulan ortak pencere (her sahnede çalışır: kendi tuvali var).
/// Elmas yetmiyorsa düğme Elmas Dükkanı'nı açar. Bedel 0 ise "BEDAVA" yazar, elmas düşmez.
/// </summary>
public sealed class GemConfirm : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static readonly Color InkMuted = ReferenceUiVisuals.Hex(0x6B5236);

    private static GemConfirm open;
    private Action onCancel;

    public static bool IsOpen => open != null;

    /// <param name="cost">Elmas bedeli; 0 bedava.</param>
    /// <param name="onConfirm">Onaylanınca çağrılır. Elmas harcamayı çağıran yapar (GameManager.SpendGems vb.).</param>
    /// <param name="onCancel">Vazgeçilince ya da pencere kapanınca (savaşta oyunu sürdürmek için).</param>
    /// <param name="altAction">İsteğe bağlı ikinci seçenek (ör. "REKLAM İZLE"): elmas istemez.</param>
    public static void Show(string title, string body, int cost, string action, Action onConfirm, Action onCancel = null,
        bool unscaled = false, string altAction = null, Action onAlt = null)
    {
        Close(false);
        var root = new GameObject("GemConfirm", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        open = root.AddComponent<GemConfirm>();
        open.onCancel = onCancel;
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(() => Close(true));

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(safe, "Panel", center, center, new Vector2(-160, -118), new Vector2(160, 118),
            Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;
        panel.transform.localScale = Vector3.one * 0.85f;
        var pop = panel.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetLink(panel.gameObject);
        if (unscaled) pop.SetUpdate(true);

        ReferenceUiVisuals.Text(panel.transform, "Title", title, 20, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(44, -54), new Vector2(-44, -16), true);
        bool hasAlt = !string.IsNullOrEmpty(altAction);
        if (hasAlt)
        {
            // İkinci seçenek ana düğmenin üstünde; pencere biraz uzar.
            panel.rectTransform.offsetMin = new Vector2(-160, -146);
            panel.rectTransform.offsetMax = new Vector2(160, 146);
            var alt = ReferenceUiVisuals.Button(panel.transform, "AltButton", altAction, false,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 80), new Vector2(-22, 124), 15);
            alt.onClick.AddListener(() =>
            {
                Close(false);
                onAlt?.Invoke();
            });
        }
        ReferenceUiVisuals.Text(panel.transform, "Body", body, 14, InkMuted, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(22, hasAlt ? 130 : 84), new Vector2(-22, -58), false, true);

        var gm = GameManager.Instance;
        int gems = gm != null ? gm.Gems : 0;
        bool enough = cost <= gems;
        string label = cost <= 0 ? $"{action} · " + L.T("BEDAVA", "FREE")
            : enough ? $"{action} · {cost}" : L.T("ELMAS YETMİYOR · ", "NOT ENOUGH GEMS · ") + $"{gems}/{cost}";
        var button = ReferenceUiVisuals.Button(panel.transform, "ConfirmButton", label, enough,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 20), new Vector2(-22, 72), 17);
        if (cost > 0) AddGemIcon(button.transform);
        button.onClick.AddListener(() =>
        {
            if (!enough)
            {
                // Pencere kapanır, dükkân açılır; vazgeçme geri çağrısı dükkân kapanınca değil şimdi çalışır.
                Close(true);
                GemShopUI.Open();
                return;
            }
            Close(false);
            onConfirm?.Invoke();
        });

        var closeRect = ReferenceUiVisuals.Rect(panel.transform, "Close", Vector2.one, Vector2.one,
            new Vector2(-46, -46), new Vector2(-8, -8));
        var closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        closeImage.color = closeImage.sprite != null ? Color.white : ReferenceUiVisuals.Red;
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(() => Close(true));
        ReferenceUiVisuals.Text(closeRect, "X", "X", 16, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        AudioManager.Instance?.PlaySfx("click");
    }

    /// <summary>Düğme yazısının soluna küçük elmas simgesi.</summary>
    public static void AddGemIcon(Transform button)
    {
        var icon = ReferenceUiVisuals.Image(button, "GemIcon", new Vector2(0.04f, 0.18f), new Vector2(0.16f, 0.82f),
            Vector2.zero, Vector2.zero, Color.white);
        icon.sprite = GemIcon.Sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    public static void Close(bool cancelled)
    {
        if (open == null) return;
        var cancel = cancelled ? open.onCancel : null;
        Destroy(open.gameObject);
        open = null;
        cancel?.Invoke();
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }
}
