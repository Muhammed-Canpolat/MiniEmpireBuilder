using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 6. oturum (4.1): açılacak arazinin TAMAMINI haritada gösterir — hafif parlak dolgu + belirgin altın kenar (yalnız dış kenarlar) + nabız.
/// Ana Üs (hücre = 1×1 birim) ve ada (IslandLayout hücresi) aynı bileşeni kullanır; hücre → dünya dikdörtgeni dönüşümünü çağıran verir.
/// <see cref="Disperse"/>: alan açılırken bulut dağılma animasyonu (Tiny Swords bulutları dışa doğru kayıp söner).
/// </summary>
public sealed class AreaHighlight : MonoBehaviour
{
    private static Material lineMaterial;
    private static Sprite square;
    private readonly List<SpriteRenderer> fills = new List<SpriteRenderer>();
    private readonly List<LineRenderer> edges = new List<LineRenderer>();
    public int CellCount { get; private set; }
    public Rect WorldBounds { get; private set; }

    public static AreaHighlight Show(IReadOnlyCollection<Vector2Int> cells, Func<Vector2Int, Rect> cellRect, int sortingOrder = 40, string name = "AreaHighlight")
    {
        var go = new GameObject(name);
        var h = go.AddComponent<AreaHighlight>();
        h.Build(cells, cellRect, sortingOrder);
        return h;
    }

    /// <summary>Shader.Find build'de (iOS/URP, strip) null dönebilir; sırayla güvenli yedekler dener.</summary>
    public static Material SafeSpriteMaterial()
    {
        Shader sh = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
            ?? Shader.Find("Unlit/Color");
        if (sh != null) return new Material(sh);
        var tmp = new GameObject("MatProbe") { hideFlags = HideFlags.HideAndDontSave };
        var sr = tmp.AddComponent<SpriteRenderer>();
        var m = sr.sharedMaterial != null ? new Material(sr.sharedMaterial) : null;
        if (Application.isPlaying) UnityEngine.Object.Destroy(tmp); else UnityEngine.Object.DestroyImmediate(tmp);
        return m;
    }

    private void Build(IReadOnlyCollection<Vector2Int> cells, Func<Vector2Int, Rect> cellRect, int order)
    {
        if (lineMaterial == null) lineMaterial = SafeSpriteMaterial();
        if (square == null)
        {
            var tex = Texture2D.whiteTexture;
            square = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
        var set = new HashSet<Vector2Int>(cells);
        CellCount = set.Count;
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        foreach (var c in set)
        {
            var r = cellRect(c);
            xMin = Mathf.Min(xMin, r.xMin); yMin = Mathf.Min(yMin, r.yMin); xMax = Mathf.Max(xMax, r.xMax); yMax = Mathf.Max(yMax, r.yMax);
            var fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(transform, false);
            fill.sprite = square;
            fill.color = new Color(1f, 0.86f, 0.35f, 0.22f);
            fill.sortingOrder = order;
            fill.transform.position = new Vector3(r.center.x, r.center.y, 0f);
            fill.transform.localScale = new Vector3(r.width, r.height, 1f);
            fills.Add(fill);
            // Dış kenarlar: komşu hücre kümede değilse o kenar çizilir.
            if (!set.Contains(c + Vector2Int.left)) Edge(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), order);
            if (!set.Contains(c + Vector2Int.right)) Edge(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), order);
            bool upIsPlus = cellRect(c + Vector2Int.up).center.y > r.center.y;   // ada: satır aşağı doğru artar
            Vector2Int above = upIsPlus ? c + Vector2Int.up : c + Vector2Int.down, below = upIsPlus ? c + Vector2Int.down : c + Vector2Int.up;
            if (!set.Contains(above)) Edge(new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), order);
            if (!set.Contains(below)) Edge(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), order);
        }
        WorldBounds = set.Count > 0 ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : new Rect();
    }

    private void Edge(Vector2 a, Vector2 b, int order)
    {
        var line = new GameObject("Edge").AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = true;
        line.material = lineMaterial;
        line.positionCount = 2;
        line.SetPosition(0, a); line.SetPosition(1, b);
        line.startWidth = line.endWidth = 0.09f;
        line.startColor = line.endColor = new Color(1f, 0.82f, 0.25f, 0.95f);
        line.sortingOrder = order + 1;
        edges.Add(line);
    }

    private void Update()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (var f in fills) if (f != null) f.color = new Color(1f, 0.86f, 0.35f, Mathf.Lerp(0.14f, 0.30f, pulse));
        foreach (var e in edges) if (e != null) { var c = new Color(1f, 0.82f, 0.25f, Mathf.Lerp(0.7f, 1f, pulse)); e.startColor = e.endColor = c; }
    }

    public void Hide() { if (this != null) Destroy(gameObject); }

    /// <summary>Alanın üstünde bulutlar belirip dışa doğru dağılır (açılış animasyonu); bitince <paramref name="done"/>.</summary>
    public static Coroutine Disperse(MonoBehaviour host, Rect area, Action done, float seconds = 1.2f)
    {
        return host.StartCoroutine(DisperseRoutine(area, done, seconds));
    }

    private static IEnumerator DisperseRoutine(Rect area, Action done, float seconds)
    {
        var sprites = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.clouds : null;
        var root = new GameObject("LandCloudDisperse");
        var list = new List<(Transform t, SpriteRenderer sr, Vector3 from, Vector3 to)>();
        int n = Mathf.Clamp(Mathf.RoundToInt(area.width * area.height / 6f), 6, 28);
        var rng = new System.Random(4127);
        for (int i = 0; i < n; i++)
        {
            Sprite sprite = sprites != null && sprites.Length > 0 ? sprites[i % sprites.Length] : null;
            if (sprite == null) continue;
            var sr = new GameObject("Cloud").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root.transform, false);
            sr.sprite = sprite;
            sr.sortingOrder = 300;
            var p = new Vector3(area.xMin + (float)rng.NextDouble() * area.width, area.yMin + (float)rng.NextDouble() * area.height, 0f);
            float s = 2.2f / Mathf.Max(0.1f, sprite.bounds.size.x);
            sr.transform.position = p;
            sr.transform.localScale = Vector3.one * s;
            Vector3 dir = ((Vector2)p - area.center).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            list.Add((sr.transform, sr, p, p + dir * 4f));
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
            foreach (var c in list)
            {
                if (c.t == null) continue;
                c.t.position = Vector3.Lerp(c.from, c.to, k);
                c.sr.color = new Color(1f, 1f, 1f, 1f - k);
            }
            yield return null;
        }
        UnityEngine.Object.Destroy(root);
        done?.Invoke();
    }
}
