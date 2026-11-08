using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>Editor/development-only world measurements, toggled with F1.</summary>
public class BaseDebugOverlay : MonoBehaviour
{
    public bool Visible => geometryRoot != null && geometryRoot.activeSelf;
    private GameObject geometryRoot;
    private TextMeshProUGUI readout;
    private Sprite lineSprite;
    private int fenceGaps;
    private bool patchesVisible = true;
    private bool buildingSpritesVisible = true;
    private bool shadowsVisible = true;

    private void Start()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) { enabled = false; return; }
        lineSprite = BaseMapManager.MakeSquareSprite(Color.white);
        geometryRoot = new GameObject("BaseDebugGeometry");
        var canvas = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        if (canvas != null)
        {
            var panel = ReferenceUiVisuals.Image(canvas, "BaseDebugReadout",
                new Vector2(0.015f, 0f), new Vector2(0.985f, 0f),
                new Vector2(0, 94), new Vector2(0, 265), new Color(0.08f, 0.06f, 0.04f, 0.84f));
            readout = ReferenceUiVisuals.Text(panel.transform, "Text", "", 10,
                Color.white, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, new Vector2(6, 3), new Vector2(-6, -3));
            readout.enableAutoSizing = false;
            panel.gameObject.SetActive(false);
        }
        // Diagnostic geometry must never be part of the normal player view.
        SetVisible(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            SetVisible(!Visible);
        if (Visible && Keyboard.current != null)
        {
            if (Keyboard.current.f2Key.wasPressedThisFrame) patchesVisible = !patchesVisible;
            if (Keyboard.current.f3Key.wasPressedThisFrame) buildingSpritesVisible = !buildingSpritesVisible;
            if (Keyboard.current.f4Key.wasPressedThisFrame) shadowsVisible = !shadowsVisible;
            ApplyLayerVisibility();
        }
        if (Visible) UpdateReadout();
    }

    public void SetDiagnosticLayers(bool patches, bool sprites, bool shadows)
    {
        patchesVisible = patches;
        buildingSpritesVisible = sprites;
        shadowsVisible = shadows;
        ApplyLayerVisibility();
    }

    private void ApplyLayerVisibility()
    {
        var patch = GameObject.Find("YamaTilemap")?.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
        if (patch != null) patch.enabled = patchesVisible;
        foreach (var building in FindObjectsByType<BuildingIdentifier>(FindObjectsSortMode.None))
        {
            foreach (var sprite in building.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sprite.name == "SoftGroundShadow" || sprite.name == "ZeminGolgesi")
                    sprite.enabled = shadowsVisible;
                else sprite.enabled = buildingSpritesVisible;
            }
        }
    }

    public void SetVisible(bool visible)
    {
        if (geometryRoot == null) return;
        if (visible) Rebuild();
        geometryRoot.SetActive(visible);
        if (readout != null) readout.transform.parent.gameObject.SetActive(visible);
        if (visible) UpdateReadout();
    }

    private void Rebuild()
    {
        for (int i = geometryRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(geometryRoot.transform.GetChild(i).gameObject);
        var grid = new Color(1f, 1f, 1f, 0.14f);
        for (int x = 0; x <= BaseWorldLayout.Width; x++)
            Line(new Vector2(x, 0), new Vector2(x, BaseWorldLayout.Height), grid, 0.025f);
        for (int y = 0; y <= BaseWorldLayout.Height; y++)
            Line(new Vector2(0, y), new Vector2(BaseWorldLayout.Width, y), grid, 0.025f);

        var fenceCells = new System.Collections.Generic.HashSet<Vector2Int>();
        var fenceRoot = GameObject.Find("BuildableFence");
        if (fenceRoot != null)
            foreach (Transform piece in fenceRoot.transform)
                fenceCells.Add(new Vector2Int(Mathf.FloorToInt(piece.position.x),
                    Mathf.FloorToInt(piece.position.y)));
        fenceGaps = 0;
        for (int x = 1; x <= 20; x++)
        {
            FenceCell(x, 1, fenceCells);
            FenceCell(x, 36, fenceCells);
        }
        for (int y = 2; y <= 35; y++)
        {
            FenceCell(1, y, fenceCells);
            FenceCell(20, y, fenceCells);
        }

        RectInt buildable = BaseMapManager.Instance.BuildableRect;
        Outline(new Rect(buildable.x, buildable.y, buildable.width, buildable.height), Color.yellow, 0.12f);
        var camera = Camera.main;
        if (camera != null)
        {
            Vector3 low = camera.ViewportToWorldPoint(new Vector3(0, 0, 10));
            Vector3 high = camera.ViewportToWorldPoint(new Vector3(1, 1, 10));
            Outline(new Rect(low.x, low.y, high.x - low.x, high.y - low.y), Color.red, 0.12f);
        }

        foreach (var building in FindObjectsOfType<BuildingIdentifier>())
        {
            Vector2 size = BaseWorldLayout.Footprint(building.buildingType);
            Vector2 center = building.transform.position;
            Outline(new Rect(center.x - size.x / 2f, center.y - size.y / 2f, size.x, size.y),
                Color.green, 0.11f);
            var garrison = building.GetComponentInChildren<BaseGarrisonVisual>();
            if (garrison != null && garrison.Anchors != null)
                foreach (var anchor in garrison.Anchors)
                {
                    Vector2 point = center - size * 0.5f + anchor.cell;
                    var marker = new GameObject("GarrisonAnchorDot");
                    marker.transform.SetParent(geometryRoot.transform, false);
                    marker.transform.position = point;
                    marker.transform.localScale = Vector3.one * 0.19f;
                    var renderer = marker.AddComponent<SpriteRenderer>();
                    renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
                    renderer.color = anchor.role == BaseGarrisonVisual.Role.RoofArcher
                        ? Color.cyan : new Color(1f, 0.34f, 0.8f);
                    renderer.sortingOrder = 901;
                }
            var mine = building.GetComponentInChildren<BaseMineVisual>();
            if (mine != null)
            {
                Vector2 point = center - size * 0.5f + mine.MinerAnchorCell;
                var marker = new GameObject("MineWorkerAnchorDot");
                marker.transform.SetParent(geometryRoot.transform, false);
                marker.transform.position = point;
                marker.transform.localScale = Vector3.one * 0.19f;
                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
                renderer.color = new Color(1f, 0.62f, 0.2f);
                renderer.sortingOrder = 901;
            }
        }
        var hut = GameObject.Find("BuilderHut");
        if (hut != null)
        {
            Vector2 center = hut.transform.position;
            Outline(new Rect(center.x - 1, center.y - 1, 2, 2), Color.green, 0.11f);
        }
        var camp = GameObject.Find("HeroCamp");
        if (camp != null)
        {
            Vector2 center = camp.transform.position;
            Outline(new Rect(center.x - 1, center.y - 1, 2, 2), Color.green, 0.11f);
        }
    }

    private void FenceCell(int x, int y, System.Collections.Generic.HashSet<Vector2Int> pieces)
    {
        bool covered = pieces.Contains(new Vector2Int(x, y));
        if (!covered) fenceGaps++;
        Outline(new Rect(x + 0.05f, y + 0.05f, 0.9f, 0.9f),
            covered ? new Color(0.15f, 1f, 0.2f, 0.8f) : Color.red, 0.06f);
    }

    private void UpdateReadout()
    {
        if (readout == null || Camera.main == null) return;
        SpriteRenderer main = null;
        foreach (var building in FindObjectsOfType<BuildingIdentifier>())
            if (building.buildingType == BuildingType.MainBase) { main = building.GetComponent<SpriteRenderer>(); break; }
        float viewWidth = Camera.main.orthographicSize * 2f * Camera.main.aspect;
        float spriteRatio = main != null ? main.bounds.size.x / viewWidth : 0;
        float footprintRatio = BaseWorldLayout.Footprint(BuildingType.MainBase).x / viewWidth;
        var camera = Camera.main;
        var bars = FindFirstObjectByType<WorldViewportBars>();
        var sb = new StringBuilder(850);
        sb.AppendLine($"F1 {Screen.width}×{Screen.height} safe={Screen.safeArea}");
        sb.AppendLine($"F2 Yama={(GameObject.Find("YamaTilemap") == null ? "kaldırıldı" : patchesVisible ? "açık" : "kapalı")} · F3 Bina={(buildingSpritesVisible ? "açık" : "kapalı")} · F4 Gölge={(shadowsVisible ? "açık" : "kapalı")}");
        sb.AppendLine($"Bar px: üst={PixelHeight(bars?.TopBar):F1} alt={PixelHeight(bars?.BottomBar):F1}");
        sb.AppendLine($"Cam={camera.pixelRect} aspect={camera.aspect:F3} ortho={camera.orthographicSize:F2}");
        Vector3 min = camera.WorldToScreenPoint(Vector3.zero);
        Vector3 max = camera.WorldToScreenPoint(new Vector3(BaseWorldLayout.Width, BaseWorldLayout.Height));
        sb.AppendLine($"Dünya+çit px=({min.x:F0},{min.y:F0})–({max.x:F0},{max.y:F0})");
        sb.AppendLine($"Görüş={viewWidth:F1}×{camera.orthographicSize * 2f:F1} çit boşluğu={fenceGaps}");
        sb.AppendLine($"Ana Üs sprite={spriteRatio:F3} footprint={footprintRatio:F3}");
        var apprentice = FindFirstObjectByType<BattleApprentice>();
        if (apprentice != null) sb.AppendLine($"Çırak={apprentice.transform.position}");
        sb.Append("Raycast UI: ");
        foreach (var image in FindObjectsByType<Image>(FindObjectsSortMode.None))
        {
            if (!image.isActiveAndEnabled || !image.raycastTarget) continue;
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            sb.Append($"{image.name}[{corners[0].x:F0},{corners[0].y:F0},{corners[2].x:F0},{corners[2].y:F0}] ");
        }
        readout.text = sb.ToString();
    }

    private static float PixelHeight(RectTransform rect)
    {
        if (rect == null) return 0f;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[2].y - corners[0].y;
    }

    private void Outline(Rect rect, Color color, float thickness)
    {
        Line(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), color, thickness);
        Line(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), color, thickness);
        Line(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), color, thickness);
        Line(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), color, thickness);
    }

    private void Line(Vector2 from, Vector2 to, Color color, float thickness)
    {
        var go = new GameObject("DebugLine");
        go.transform.SetParent(geometryRoot.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = lineSprite;
        renderer.color = color;
        renderer.sortingOrder = 900;
        Vector2 delta = to - from;
        go.transform.position = (from + to) / 2f;
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(delta.magnitude, thickness, 1);
    }

    private void OnDestroy()
    {
        if (geometryRoot != null) Destroy(geometryRoot);
    }
}
#endif
