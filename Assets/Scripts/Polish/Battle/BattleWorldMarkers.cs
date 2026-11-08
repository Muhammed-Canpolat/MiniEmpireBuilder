using TMPro;
using UnityEngine;

// Presentation attached to the existing combat slots and towers.
public static class BattleWorldMarkers
{
    private static Material lineMaterial;

    public static GameObject DashedCircle(Transform parent, float radius, Color color, int dashes = 18)
    {
        var root = new GameObject("DashedRangeRing");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0, 0, 0.05f);
        float parentScale = Mathf.Abs(parent.lossyScale.x);
        root.transform.localScale = Vector3.one * (parentScale > 0.001f ? 1f / parentScale : 1f);
        if (lineMaterial == null)
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < dashes; i++)
        {
            var dash = new GameObject("Dash");
            dash.transform.SetParent(root.transform, false);
            var line = dash.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.material = lineMaterial;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = 0.045f;
            line.positionCount = 2;
            line.sortingOrder = 8;
            float start = i * Mathf.PI * 2f / dashes;
            float end = start + Mathf.PI * 1.2f / dashes;
            line.SetPosition(0, new Vector3(Mathf.Cos(start) * radius, Mathf.Sin(start) * radius));
            line.SetPosition(1, new Vector3(Mathf.Cos(end) * radius, Mathf.Sin(end) * radius));
        }
        return root;
    }

    public static void SlotLabel(Transform parent, BattleManager manager)
    {
        var plus = new GameObject("SlotPlus");
        plus.transform.SetParent(parent, false);
        plus.transform.localPosition = new Vector3(0, 0, -0.2f);
        var horizontal = plus.AddComponent<SpriteRenderer>();
        horizontal.sprite = BaseMapManager.MakeSquareSprite(Color.white);
        horizontal.color = ReferenceUiVisuals.Gold;
        horizontal.sortingOrder = 12;
        plus.transform.localScale = new Vector3(0.42f, 0.08f, 1);
        var vertical = new GameObject("Vertical");
        vertical.transform.SetParent(parent, false);
        vertical.transform.localPosition = new Vector3(0, 0, -0.2f);
        vertical.transform.localScale = new Vector3(0.08f, 0.42f, 1);
        var verticalSprite = vertical.AddComponent<SpriteRenderer>();
        verticalSprite.sprite = horizontal.sprite;
        verticalSprite.color = ReferenceUiVisuals.Gold;
        verticalSprite.sortingOrder = 12;

        var caption = new GameObject("SlotCaption");
        caption.transform.SetParent(parent, false);
        caption.transform.localPosition = new Vector3(0, -1.18f, -0.2f);
        var canvas = caption.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 12;
        var canvasRect = caption.GetComponent<RectTransform>();
        // Tek satır, okunur boyut: 1.5 x 0.42 dünya birimi.
        canvasRect.sizeDelta = new Vector2(150, 42);
        canvasRect.localScale = Vector3.one * 0.01f;
        var pill = caption.AddComponent<UnityEngine.UI.Image>();
        pill.sprite = ReferenceUiVisuals.PillSprite();
        pill.type = UnityEngine.UI.Image.Type.Sliced;
        pill.color = new Color32(0x14, 0x11, 0x0D, 0xD9);
        pill.raycastTarget = false;
        var rights = ReferenceUiVisuals.Text(caption.transform, "RemainingRights", "", 26,
            ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2), bodyBold: true);
        caption.AddComponent<BattleSlotRights>().Initialize(manager, rights, canvas);
    }
}

public sealed class BattleSlotRights : MonoBehaviour
{
    private BattleManager manager;
    private TextMeshProUGUI label;
    private Canvas canvas;
    private static readonly BuildingType[] Types =
        { BuildingType.ArcherTower, BuildingType.CannonTower, BuildingType.Monastery };

    public void Initialize(BattleManager source, TextMeshProUGUI target, Canvas owner)
    {
        manager = source;
        label = target;
        canvas = owner;
    }

    /// <summary>Tüm kule türlerinde kalan toplam hak; hak yoksa etiket gizlenir.</summary>
    private void Update()
    {
        if (manager == null || label == null) return;
        int remaining = 0;
        foreach (var type in Types) remaining += manager.GetBattleBuildRemaining(type);
        if (canvas != null) canvas.enabled = remaining > 0;
        label.text = UiStrings.Upper($"Kule kur · {remaining} hak");
    }
}
