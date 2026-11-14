using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 4. oturum (1.1): oyunun TEK yol bulma servisi. Her sahne kendi yürünebilirlik ızgarasını (<see cref="IWalkGrid"/>) verir:
/// Ana Üs <see cref="BaseWalkGrid"/> (doluluk ızgarası), Ada <see cref="IslandWalkGrid"/> (arazi + yükseklik/merdiven + bina + doğa),
/// Savaş <see cref="BattleWalkGrid"/> (BattleObstacles alanları). Izgara değişince <see cref="IWalkGrid.Version"/> artar;
/// <see cref="GridPathFollower"/> yolda olan karakterin rotasını yeniden hesaplar. Hedefe ulaşılamıyorsa en yakın ulaşılabilir
/// hücreye gidilir (karakter yerinde titremez).
/// </summary>
public interface IWalkGrid
{
    /// <summary>Izgara her değiştiğinde (bina kur/taşı/sil, alan aç, dekor) artar.</summary>
    int Version { get; }
    RectInt Bounds { get; }
    bool Walkable(Vector2Int cell);
    /// <summary>a → b komşu adımı yapılabilir mi (yükseklik/merdiven kuralları; b yürünebilir olmalı).</summary>
    bool CanStep(Vector2Int a, Vector2Int b);
    Vector2Int CellOf(Vector2 world);
    Vector2 Center(Vector2Int cell);
}

public static class GridPathfinder
{
    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    /// <summary>Son hesaplanan yollar (hata ayıklama görseli için, en çok 32).</summary>
    public static readonly List<List<Vector2>> RecentPaths = new List<List<Vector2>>();
    public static int SearchCount { get; private set; }

    /// <summary>Komşu adımı: 4 yön serbest, çapraz adımda iki yan hücre de geçilebilir olmalı (köşe kesme yok).</summary>
    public static bool Step(IWalkGrid grid, Vector2Int a, Vector2Int b)
    {
        if (!grid.Bounds.Contains(b) || !grid.CanStep(a, b)) return false;
        int dx = b.x - a.x, dy = b.y - a.y;
        if (dx != 0 && dy != 0)
        {
            var sideA = new Vector2Int(a.x + dx, a.y);
            var sideB = new Vector2Int(a.x, a.y + dy);
            if (!grid.Bounds.Contains(sideA) || !grid.Bounds.Contains(sideB)) return false;
            if (!grid.CanStep(a, sideA) || !grid.CanStep(a, sideB)) return false;
        }
        return true;
    }

    /// <summary><paramref name="cell"/>'e en yakın yürünebilir hücre (halka halka, en çok <paramref name="radius"/>).</summary>
    public static Vector2Int? NearestWalkable(IWalkGrid grid, Vector2Int cell, int radius = 6)
    {
        if (grid.Bounds.Contains(cell) && grid.Walkable(cell)) return cell;
        for (int r = 1; r <= radius; r++)
        {
            Vector2Int? best = null;
            float bestD = float.MaxValue;
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                var c = new Vector2Int(cell.x + dx, cell.y + dy);
                if (!grid.Bounds.Contains(c) || !grid.Walkable(c)) continue;
                float d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best.HasValue) return best;
        }
        return null;
    }

    /// <summary>
    /// <paramref name="from"/> → <paramref name="to"/> dünya noktaları listesi (ilk nokta = from). <paramref name="isGoal"/> verilirse
    /// hedef koşulu odur (ör. yapının bitişiği). Ulaşılamazsa en yakın ulaşılabilir hücreye giden yol döner ve
    /// <paramref name="reached"/> false olur. Başlangıç hücresi kapalıysa (karakter bina içinde kalmışsa) önce en yakın açık hücreye çıkılır.
    /// </summary>
    public static List<Vector2> FindPath(IWalkGrid grid, Vector2 from, Vector2 to, out bool reached,
        Func<Vector2Int, bool> isGoal = null, int maxNodes = 6000)
    {
        SearchCount++;
        reached = false;
        var bounds = grid.Bounds;
        Vector2Int startCell = grid.CellOf(from), goalCell = grid.CellOf(to);
        var points = new List<Vector2> { from };
        var start = NearestWalkable(grid, startCell, 8);
        if (!start.HasValue) return points;
        bool exact = isGoal == null;
        bool goalWalkable = bounds.Contains(goalCell) && grid.Walkable(goalCell);
        if (exact && !goalWalkable)
        {
            // Hedef kapalı: hedefe en yakın açık hücre hedef olur (ulaşılamazsa yine en yakına gidilir).
            var near = NearestWalkable(grid, goalCell, 4);
            if (near.HasValue) { goalCell = near.Value; goalWalkable = true; }
        }
        Func<Vector2Int, bool> goalTest = isGoal ?? (c => c == goalCell);

        float H(Vector2Int c)
        {
            float dx = Mathf.Abs(c.x - goalCell.x), dy = Mathf.Abs(c.y - goalCell.y);
            return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
        }

        var g = new Dictionary<Vector2Int, float> { [start.Value] = 0f };
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();
        var open = new MinHeap();
        open.Push(start.Value, H(start.Value));
        Vector2Int bestCell = start.Value;
        float bestH = H(start.Value);
        Vector2Int end = start.Value;
        int expanded = 0;
        while (open.Count > 0 && expanded < maxNodes)
        {
            var cur = open.Pop();
            if (!closed.Add(cur)) continue;
            expanded++;
            if (goalTest(cur)) { end = cur; reached = true; break; }
            float h = H(cur);
            if (h < bestH) { bestH = h; bestCell = cur; }
            foreach (var d in Dirs)
            {
                var n = cur + d;
                if (closed.Contains(n) || !Step(grid, cur, n)) continue;
                float cost = g[cur] + (d.x != 0 && d.y != 0 ? 1.414f : 1f);
                if (g.TryGetValue(n, out float old) && old <= cost) continue;
                g[n] = cost;
                parent[n] = cur;
                open.Push(n, cost + H(n));
            }
        }
        if (!reached) end = bestCell;

        var cells = new List<Vector2Int> { end };
        while (cells[cells.Count - 1] != start.Value && parent.TryGetValue(cells[cells.Count - 1], out var p)) cells.Add(p);
        cells.Reverse();
        // Başlangıç hücresi kapalıysa önce açık hücrenin merkezine çık.
        bool startBlocked = start.Value != startCell;
        for (int i = startBlocked ? 0 : 1; i < cells.Count; i++) points.Add(grid.Center(cells[i]));
        if (reached && exact && goalWalkable && grid.CellOf(to) == goalCell) points[points.Count - 1] = to;
        var result = Smooth(grid, points, startBlocked ? 2 : 1);
        Remember(result);
        return result;
    }

    /// <summary>Doğrudan görüşü açık ara noktaları atar (ızgara adımları boyunca örnekleyerek).</summary>
    private static List<Vector2> Smooth(IWalkGrid grid, List<Vector2> pts, int keepFirst)
    {
        if (pts.Count <= 2) return pts;
        var result = new List<Vector2>();
        for (int i = 0; i < keepFirst && i < pts.Count; i++) result.Add(pts[i]);
        int anchor = keepFirst - 1;
        while (anchor < pts.Count - 1)
        {
            int next = anchor + 1;
            for (int j = pts.Count - 1; j > anchor + 1; j--)
                if (LineClear(grid, pts[anchor], pts[j])) { next = j; break; }
            result.Add(pts[next]);
            anchor = next;
        }
        return result;
    }

    /// <summary>İki nokta arası düz çizgi yürünebilir mi (çizgi boyunca hücre geçişleri <see cref="Step"/> kuralına uyar).</summary>
    public static bool LineClear(IWalkGrid grid, Vector2 a, Vector2 b)
    {
        float dist = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.15f));
        var prev = grid.CellOf(a);
        for (int i = 1; i <= steps; i++)
        {
            var c = grid.CellOf(Vector2.Lerp(a, b, i / (float)steps));
            if (c == prev) continue;
            if (!Step(grid, prev, c)) return false;
            prev = c;
        }
        return true;
    }

    private static void Remember(List<Vector2> path)
    {
        if (!WalkGridDebug.Enabled) return;
        RecentPaths.Add(new List<Vector2>(path));
        if (RecentPaths.Count > 32) RecentPaths.RemoveAt(0);
    }

    /// <summary>Basit ikili yığın (öncelik kuyruğu).</summary>
    private sealed class MinHeap
    {
        private readonly List<(Vector2Int cell, float f)> items = new List<(Vector2Int, float)>();
        public int Count => items.Count;

        public void Push(Vector2Int cell, float f)
        {
            items.Add((cell, f));
            int i = items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (items[p].f <= items[i].f) break;
                (items[p], items[i]) = (items[i], items[p]);
                i = p;
            }
        }

        public Vector2Int Pop()
        {
            var top = items[0].cell;
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < items.Count && items[l].f < items[m].f) m = l;
                if (r < items.Count && items[r].f < items[m].f) m = r;
                if (m == i) break;
                (items[m], items[i]) = (items[i], items[m]);
                i = m;
            }
            return top;
        }
    }
}

/// <summary>
/// Yolda yürüyen karakterin rotası: ızgara sürümü değişirse (bina kuruldu/taşındı/silindi, alan açıldı) bulunduğu yerden yeniden
/// hesaplar. Ulaşılamayan hedefte en yakın noktada durur ve <see cref="Done"/> olur (titreme yok).
/// </summary>
public sealed class GridPathFollower
{
    private readonly IWalkGrid grid;
    private readonly Func<Vector2Int, bool> isGoal;
    private List<Vector2> path;
    private int index;
    private int version;
    public Vector2 Destination { get; private set; }
    public bool Reached { get; private set; }
    public bool Done => path == null || index >= path.Count;
    public IReadOnlyList<Vector2> Path => path;
    public int Repaths { get; private set; }

    public GridPathFollower(IWalkGrid grid, Vector2 from, Vector2 to, Func<Vector2Int, bool> isGoal = null)
    {
        this.grid = grid;
        this.isGoal = isGoal;
        Destination = to;
        Plan(from);
    }

    private void Plan(Vector2 from)
    {
        version = grid.Version;
        path = GridPathfinder.FindPath(grid, from, Destination, out bool reached, isGoal);
        Reached = reached;
        index = path.Count > 1 ? 1 : path.Count;
    }

    /// <summary>Bir kare ilerler; yeni konumu döndürür. <paramref name="facing"/> yatay yön (0: değişmedi).</summary>
    public Vector2 Tick(Vector2 position, float distance, out float facing)
    {
        facing = 0f;
        if (grid.Version != version && !Done) { Repaths++; Plan(position); }
        for (int guard = 0; guard < 16 && !Done && distance > 1e-5f; guard++)
        {
            Vector2 target = path[index];
            float toTarget = Vector2.Distance(position, target);
            if (toTarget <= distance || toTarget < 0.02f)
            {
                // Ara noktaya varıldı: kalan mesafe bir sonraki parçaya taşınır.
                if (Mathf.Abs(target.x - position.x) > 0.001f) facing = target.x - position.x;
                distance -= toTarget;
                position = target;
                index++;
                continue;
            }
            Vector2 next = position + (target - position) / toTarget * distance;
            if (Mathf.Abs(next.x - position.x) > 0.001f) facing = next.x - position.x;
            position = next;
            break;
        }
        return position;
    }
}

/// <summary>
/// Hareketli hedef kovalayan birimler için (mızraklı, düşman): düz çizgi açıksa doğrudan yürür; kapalıysa A* rotası kullanır ve
/// hedef bir hücreden fazla kayınca / ızgara değişince yeniden hesaplar (en çok 0,3 sn'de bir).
/// </summary>
public sealed class GridSteer
{
    private readonly IWalkGrid grid;
    private GridPathFollower follower;
    private Vector2Int goalCell;
    private float nextPlan;

    public GridSteer(IWalkGrid grid) { this.grid = grid; }

    public Vector2 Step(Vector2 position, Vector2 goal, float distance, out float facing)
    {
        facing = 0f;
        if (grid == null) return Vector2.MoveTowards(position, goal, distance);
        if (GridPathfinder.LineClear(grid, position, goal) && grid.Walkable(grid.CellOf(position)))
        {
            follower = null;
            Vector2 next = Vector2.MoveTowards(position, goal, distance);
            facing = next.x - position.x;
            return next;
        }
        var cell = grid.CellOf(goal);
        if (follower == null || (follower.Done && cell != goalCell) || (cell != goalCell && Time.time >= nextPlan))
        {
            follower = new GridPathFollower(grid, position, goal);
            goalCell = cell;
            nextPlan = Time.time + 0.3f;
        }
        if (follower.Done) return position;
        return follower.Tick(position, distance, out facing);
    }
}
