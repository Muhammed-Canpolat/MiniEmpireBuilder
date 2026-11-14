using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 4. oturum (1.1): yol bulma hata ayıklama görseli (editör / Development). F6 aç-kapa: yürünebilir hücreler yeşil, kapalı kırmızı,
/// son hesaplanan rotalar sarı çizgi. Testler <see cref="Show"/> ile açıp ekran görüntüsü alır.
/// </summary>
public sealed class WalkGridDebug : MonoBehaviour
{
    public static bool Enabled { get; private set; }
    private static WalkGridDebug instance;

    private IWalkGrid grid;
    private SpriteRenderer cells;
    private Texture2D texture;
    private int drawnVersion = -1;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private Material lineMaterial;

    /// <summary>Sahnenin ızgarasını gösterir (null: kapatır).</summary>
    public static void Show(IWalkGrid grid)
    {
        if (grid == null)
        {
            Enabled = false;
            if (instance != null) Destroy(instance.gameObject);
            return;
        }
        Enabled = true;
        if (instance == null) instance = new GameObject("WalkGridDebug").AddComponent<WalkGridDebug>();
        instance.grid = grid;
        instance.drawnVersion = -1;
    }

    /// <summary>Sahne kurulumunda çağrılır: F6 ile açılabilir olsun (oyuncu derlemesinde hiçbir şey yapmaz).</summary>
    public static void Install(IWalkGrid grid)
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        var host = new GameObject("WalkGridDebugToggle").AddComponent<WalkGridDebugToggle>();
        host.grid = grid;
    }

    private void LateUpdate()
    {
        if (grid == null) return;
        if (grid.Version != drawnVersion) Redraw();
        DrawPaths();
    }

    private void Redraw()
    {
        drawnVersion = grid.Version;
        var b = grid.Bounds;
        if (texture == null || texture.width != b.width || texture.height != b.height)
        {
            texture = new Texture2D(b.width, b.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }
        var open = new Color(0.2f, 0.9f, 0.3f, 0.28f);
        var shut = new Color(0.95f, 0.15f, 0.1f, 0.42f);
        for (int x = 0; x < b.width; x++)
        for (int y = 0; y < b.height; y++)
            texture.SetPixel(x, y, grid.Walkable(new Vector2Int(b.xMin + x, b.yMin + y)) ? open : shut);
        texture.Apply();
        if (cells == null)
        {
            cells = new GameObject("Cells").AddComponent<SpriteRenderer>();
            cells.transform.SetParent(transform, false);
            cells.sortingOrder = 30000;
        }
        cells.sprite = Sprite.Create(texture, new Rect(0, 0, b.width, b.height), Vector2.zero, 1f);
        // Hücre (xMin, yMin) köşesi ve adım yönü ızgaradan türetilir (ada satırları aşağı artar).
        Vector2 c0 = grid.Center(new Vector2Int(b.xMin, b.yMin));
        Vector2 c1 = grid.Center(new Vector2Int(b.xMin + 1, b.yMin + 1));
        Vector2 step = c1 - c0;
        cells.transform.position = new Vector3(c0.x - step.x * 0.5f, c0.y - step.y * 0.5f, 0f);
        cells.transform.localScale = new Vector3(step.x, step.y, 1f);
    }

    private void DrawPaths()
    {
        var paths = GridPathfinder.RecentPaths;
        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
        while (lines.Count < paths.Count)
        {
            var lr = new GameObject("Path").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.material = lineMaterial;
            lr.startWidth = lr.endWidth = 0.08f;
            lr.startColor = new Color(1f, 0.85f, 0.1f, 0.95f);
            lr.endColor = new Color(1f, 0.4f, 0.1f, 0.95f);
            lr.sortingOrder = 30001;
            lines.Add(lr);
        }
        for (int i = 0; i < lines.Count; i++)
        {
            bool on = i < paths.Count;
            lines[i].enabled = on;
            if (!on) continue;
            lines[i].positionCount = paths[i].Count;
            for (int k = 0; k < paths[i].Count; k++) lines[i].SetPosition(k, new Vector3(paths[i][k].x, paths[i][k].y, 0f));
        }
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; Enabled = false; }
    }
}

/// <summary>F6 tuşuyla <see cref="WalkGridDebug"/> aç/kapa.</summary>
public sealed class WalkGridDebugToggle : MonoBehaviour
{
    public IWalkGrid grid;

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            WalkGridDebug.Show(WalkGridDebug.Enabled ? null : grid);
#endif
    }
}
