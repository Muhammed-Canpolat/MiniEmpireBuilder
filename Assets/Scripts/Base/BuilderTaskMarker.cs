using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İnşaatçının üzerinde çalıştığı binanın tepesinde geri sayım ve ilerleme çubuğu.
/// Görev GameManager'da gerçek saatle ilerler; bu bileşen yalnız gösterir.
/// </summary>
public sealed class BuilderTaskMarker : MonoBehaviour
{
    private const float WorldWidth = 2.6f;
    private const float WorldHeight = 0.62f;
    private const float CanvasScale = 0.01f;

    private GameObject root;
    private TextMeshProUGUI label;
    private RectTransform fill;

    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsBuilderBusy || gm.BuilderTask.kind == "remove")
        {
            if (root != null && root.activeSelf) root.SetActive(false);
            return;
        }
        if (root == null) Build();
        if (!root.activeSelf) root.SetActive(true);

        var task = gm.BuilderTask;
        root.transform.position = AnchorFor(task);
        string what = task.kind == "upgrade" ? task.label : "İnşa";
        label.text = $"{what} · {Progression.FormatDuration(gm.BuilderSecondsRemaining)}";
        fill.anchorMax = new Vector2(Mathf.Max(0.02f, gm.BuilderProgress), 1f);
    }

    /// <summary>Hedef binanın görünen tepesinin biraz üstü.</summary>
    private static Vector3 AnchorFor(BuilderTaskSave task)
    {
        GameObject building = null;
        if (!string.IsNullOrEmpty(task.slotId) && BaseMapManager.Instance != null)
            building = BaseMapManager.Instance.GetMapObjectBySlot(task.slotId)?.gameObject;
        if (building == null)
            return new Vector3(task.x, task.y + 2.2f, 0f);
        float top = building.transform.position.y + 1.5f;
        foreach (var renderer in building.GetComponentsInChildren<SpriteRenderer>())
            if (renderer.enabled && renderer.sprite != null && renderer.color.a > 0.1f)
                top = Mathf.Max(top, renderer.bounds.max.y);
        return new Vector3(building.transform.position.x, Mathf.Min(top + 0.45f,
            building.transform.position.y + 4.5f), 0f);
    }

    private void Build()
    {
        root = new GameObject("BuilderTaskMarker", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 450;
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(WorldWidth / CanvasScale, WorldHeight / CanvasScale);
        rect.localScale = Vector3.one * CanvasScale;

        var background = root.AddComponent<Image>();
        background.sprite = ReferenceUiVisuals.PillSprite();
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        label = ReferenceUiVisuals.Text(rect, "Label", "", 24, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, new Vector2(0f, 0.36f), Vector2.one,
            new Vector2(14f, 0f), new Vector2(-14f, -4f), bodyBold: true);

        var track = ReferenceUiVisuals.Image(rect, "Track", new Vector2(0f, 0f), new Vector2(1f, 0.36f),
            new Vector2(16f, 9f), new Vector2(-16f, -2f), ReferenceUiVisuals.Hex(0x3A3020));
        track.sprite = ReferenceUiVisuals.SolidPillSprite();
        track.type = Image.Type.Sliced;
        var fillImage = ReferenceUiVisuals.Image(track.rectTransform, "Fill", Vector2.zero, new Vector2(0.02f, 1f),
            Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
        fillImage.sprite = ReferenceUiVisuals.SolidPillSprite();
        fillImage.type = Image.Type.Sliced;
        fill = fillImage.rectTransform;
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root);
    }
}
