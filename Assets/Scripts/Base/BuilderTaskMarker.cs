using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Her inşaatçının üzerinde çalıştığı binanın tepesinde geri sayım ve ilerleme çubuğu.
/// Görevler GameManager'da gerçek saatle ilerler; bu bileşen yalnız gösterir.
/// </summary>
public sealed class BuilderTaskMarker : MonoBehaviour
{
    private const float WorldWidth = 2.6f;
    private const float WorldHeight = 0.62f;
    private const float CanvasScale = 0.01f;

    private sealed class Marker
    {
        public GameObject root;
        public TextMeshProUGUI label;
        public RectTransform fill;
    }

    private readonly List<Marker> markers = new List<Marker>();

    private void LateUpdate()
    {
        var gm = GameManager.Instance;
        int used = 0;
        if (gm != null)
            foreach (var task in gm.BuilderTasks)
            {
                if (task.kind == "remove") continue;
                if (used >= markers.Count) markers.Add(Build());
                var marker = markers[used++];
                if (!marker.root.activeSelf) marker.root.SetActive(true);
                marker.root.transform.position = AnchorFor(task);
                string what = task.kind == "upgrade" ? task.label : "İnşa";
                marker.label.text = $"{what} · {Progression.FormatDuration(GameManager.SecondsRemaining(task))}";
                marker.fill.anchorMax = new Vector2(Mathf.Max(0.02f, GameManager.TaskProgress(task)), 1f);
            }
        for (int i = used; i < markers.Count; i++)
            if (markers[i].root.activeSelf) markers[i].root.SetActive(false);
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

    private static Marker Build()
    {
        var marker = new Marker();
        marker.root = new GameObject("BuilderTaskMarker", typeof(RectTransform));
        var canvas = marker.root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 450;
        var rect = (RectTransform)marker.root.transform;
        rect.sizeDelta = new Vector2(WorldWidth / CanvasScale, WorldHeight / CanvasScale);
        rect.localScale = Vector3.one * CanvasScale;

        var background = marker.root.AddComponent<Image>();
        background.sprite = ReferenceUiVisuals.PillSprite();
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        marker.label = ReferenceUiVisuals.Text(rect, "Label", "", 24, ReferenceUiVisuals.Light,
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
        marker.fill = fillImage.rectTransform;
        return marker;
    }

    private void OnDestroy()
    {
        foreach (var marker in markers)
            if (marker.root != null) Destroy(marker.root);
    }
}
