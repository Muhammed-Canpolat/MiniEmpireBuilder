using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Genel iki düğmeli onay penceresi (QuitConfirm ile aynı kâğıt pencere stili; yeni sanat yok). Kendi tuvalinde, ayarların
/// (1200) üstünde. Kullanım: Ayarlar → YENİ OYUN onayı, açılışta açılamayan kayıt uyarısı.
/// </summary>
public sealed class ConfirmDialog : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static ConfirmDialog open;

    public static bool IsOpen => open != null;

    /// <param name="dimCancels">Karartmaya dokunmak "vazgeç" sayılır (açılış kayıt uyarısında false).</param>
    public static void Show(string name, string title, string body, string cancelLabel, string confirmLabel,
        Action onConfirm, Action onCancel = null, bool dimCancels = true)
    {
        if (open != null) Close();
        EnsureEventSystem();
        var root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1300;
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        open = root.AddComponent<ConfirmDialog>();
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        if (dimCancels)
        {
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => { Close(); onCancel?.Invoke(); });
        }

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(safe, "Panel", center, center, new Vector2(-165, -125), new Vector2(165, 125), Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;

        ReferenceUiVisuals.Text(panel.transform, "Title", title, 21, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 0.74f), Vector2.one, new Vector2(24, 0), new Vector2(-24, -20), true);
        var text = ReferenceUiVisuals.Text(panel.transform, "Body", body, 16, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 0.36f), new Vector2(1, 0.76f), new Vector2(26, 0), new Vector2(-26, 0), false, true);
        text.textWrappingMode = TextWrappingModes.Normal;

        var cancel = FancyInfoPanel.TsButton(panel.transform, "CancelButton", cancelLabel, false,
            new Vector2(0.05f, 0), new Vector2(0.49f, 0), new Vector2(0, 20), new Vector2(0, 74), 16, out _);
        cancel.onClick.AddListener(() => { Close(); onCancel?.Invoke(); });
        var confirm = FancyInfoPanel.TsButton(panel.transform, "ConfirmButton", confirmLabel, true,
            new Vector2(0.51f, 0), new Vector2(0.95f, 0), new Vector2(0, 20), new Vector2(0, 74), 16, out _);
        confirm.onClick.AddListener(() => { Close(); onConfirm?.Invoke(); });
    }

    public static void Close()
    {
        if (open == null) return;
        var go = open.gameObject;
        open = null;
        Destroy(go);
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }
}
