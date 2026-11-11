using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// İnşaatçı yürüyüşü için küçük ızgara A*: dolu hücrelerin (bina, kulübe, dekor, kamp) etrafından dolanır.
/// Yol bulunamazsa çağıran düz çizgiye düşer. Yolculuk başına bir kez hesaplanır (en çok ~700 hücre).
/// </summary>
public static class BuilderPathfinder
{
    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    public static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
    private static Vector3 Center(Vector2Int c) => new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);

    /// <summary>
    /// <paramref name="goalFootprint"/> verilirse yapının bitişiğindeki boş hücrede durur (yapıya girmez);
    /// verilmezse doğrudan <paramref name="to"/> noktasına gider. Yol yoksa null döner.
    /// </summary>
    public static List<Vector3> FindPath(Vector3 from, Vector3 to, RectInt? goalFootprint,
        params RectInt[] ignoreCells)
    {
        var blocked = BaseOccupancyGrid.BlockedCells(ignoreCells);
        var area = BaseWorldLayout.Buildable;
        Vector2Int start = CellOf(from), goal = CellOf(to);
        bool exact = !goalFootprint.HasValue && !blocked.Contains(goal) && area.Contains(goal);
        RectInt goalRect = goalFootprint ?? new RectInt(goal.x, goal.y, 1, 1);

        bool IsGoal(Vector2Int c)
        {
            if (blocked.Contains(c)) return false;
            if (exact) return c == goal;
            return c.x >= goalRect.xMin - 1 && c.x <= goalRect.xMax && c.y >= goalRect.yMin - 1 && c.y <= goalRect.yMax
                && !(c.x >= goalRect.xMin && c.x < goalRect.xMax && c.y >= goalRect.yMin && c.y < goalRect.yMax)
                && (c.x >= goalRect.xMin && c.x < goalRect.xMax || c.y >= goalRect.yMin && c.y < goalRect.yMax);
        }

        var g = new Dictionary<Vector2Int, float> { [start] = 0f };
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var open = new List<Vector2Int> { start };
        var closed = new HashSet<Vector2Int>();
        float H(Vector2Int c)
        {
            float dx = Mathf.Abs(c.x - goal.x), dy = Mathf.Abs(c.y - goal.y);
            return Mathf.Max(dx, dy) + 0.41f * Mathf.Min(dx, dy);
        }
        Vector2Int end = default;
        bool found = false;
        while (open.Count > 0)
        {
            int best = 0;
            float bestF = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                float f = g[open[i]] + H(open[i]);
                if (f < bestF) { bestF = f; best = i; }
            }
            var cur = open[best];
            open.RemoveAt(best);
            if (!closed.Add(cur)) continue;
            if (IsGoal(cur)) { end = cur; found = true; break; }
            foreach (var d in Dirs)
            {
                var n = cur + d;
                if (!area.Contains(n) || blocked.Contains(n) || closed.Contains(n)) continue;
                bool diagonal = d.x != 0 && d.y != 0;
                if (diagonal && (blocked.Contains(new Vector2Int(cur.x + d.x, cur.y)) ||
                    blocked.Contains(new Vector2Int(cur.x, cur.y + d.y)))) continue;
                float cost = g[cur] + (diagonal ? 1.414f : 1f);
                if (g.TryGetValue(n, out float old) && old <= cost) continue;
                g[n] = cost;
                parent[n] = cur;
                open.Add(n);
            }
        }
        if (!found) return null;

        var cells = new List<Vector2Int> { end };
        while (cells[cells.Count - 1] != start && parent.TryGetValue(cells[cells.Count - 1], out var p))
            cells.Add(p);
        cells.Reverse();

        var points = new List<Vector3> { from };
        for (int i = 1; i < cells.Count; i++) points.Add(Center(cells[i]));
        if (exact) points[points.Count - 1] = to;
        return Smooth(points, blocked);
    }

    /// <summary>Doğrudan görüş çizgisi açıksa aradaki ara noktaları atar.</summary>
    private static List<Vector3> Smooth(List<Vector3> pts, HashSet<Vector2Int> blocked)
    {
        if (pts.Count <= 2) return pts;
        var result = new List<Vector3> { pts[0] };
        int anchor = 0;
        while (anchor < pts.Count - 1)
        {
            int next = anchor + 1;
            for (int j = pts.Count - 1; j > anchor + 1; j--)
                if (LineClear(pts[anchor], pts[j], blocked)) { next = j; break; }
            result.Add(pts[next]);
            anchor = next;
        }
        return result;
    }

    private static bool LineClear(Vector3 a, Vector3 b, HashSet<Vector2Int> blocked)
    {
        float dist = Vector3.Distance(a, b);
        int steps = Mathf.CeilToInt(dist / 0.2f);
        for (int i = 1; i < steps; i++)
            if (blocked.Contains(CellOf(Vector3.Lerp(a, b, i / (float)steps)))) return false;
        return true;
    }
}
