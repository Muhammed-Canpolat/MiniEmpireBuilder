using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 4. oturum (1.1): Ada yürünebilirlik ızgarası (hücre = (sütun, satır), satır aşağı artar). Kapalı: su, yar yüzü, kale ve kurulu
/// binaların ayak izi, kesilmemiş ağaç ve altın taşı. Yükseklik değişimi yalnız merdivenden (4 yönlü adım) yapılır; plato kenarından
/// atlanmaz. Bina kur/taşı/sil, ağaç kes ya da bölge açılınca <see cref="Version"/> artar (0,25 sn'lik imza denetimi + <see cref="Invalidate"/>).
/// </summary>
public sealed class IslandWalkGrid : IWalkGrid
{
    public static readonly IslandWalkGrid Shared = new IslandWalkGrid();

    private static bool[,] blocked;
    private static int version;
    private static long signature = long.MinValue;
    private static float nextCheck = -1f;
    private static bool dirty = true;

    public static void Invalidate() => dirty = true;

    private static long Signature(IslandSaveData save)
    {
        long sig = 17;
        if (save == null) return sig;
        if (save.placed != null)
            foreach (var b in save.placed)
                if (b != null) sig = sig * 31 + (b.id?.GetHashCode() ?? 0) * 7 + b.col * 131 + b.row * 977 + (b.kind?.GetHashCode() ?? 0);
        sig = sig * 31 + (save.clearedTrees?.Count ?? 0);
        if (save.clearedTrees != null) foreach (var t in save.clearedTrees) sig += t.GetHashCode();
        if (save.clearedNature != null) foreach (var t in save.clearedNature) sig += t.GetHashCode() * 3;
        return sig;
    }

    private static void Refresh()
    {
        if (!dirty && blocked != null && Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.25f;
        var save = IslandSession.Save;
        long sig = Signature(save);
        if (!dirty && blocked != null && sig == signature) return;
        dirty = false;
        if (sig != signature || blocked == null) version++;
        signature = sig;
        int w = IslandLayout.Width, h = IslandLayout.Height;
        var b = new bool[w, h];
        for (int c = 0; c < w; c++)
        for (int r = 0; r < h; r++)
            b[c, r] = !IslandLayout.Walkable(c, r);
        void Rect(RectInt rect)
        {
            for (int c = rect.xMin; c < rect.xMax; c++)
            for (int r = rect.yMin; r < rect.yMax; r++)
                if (c >= 0 && r >= 0 && c < w && r < h) b[c, r] = true;
        }
        Rect(IslandGrid.CastleRect);
        if (save?.placed != null)
            foreach (var p in save.placed)
                if (p != null) Rect(IslandGrid.Rect(IslandGrid.ParseKind(p.kind), p.col, p.row));
        foreach (var t in IslandLayout.Trees)
            if (t.x >= 0 && t.y >= 0 && t.x < w && t.y < h && !IslandGrid.ClearedTreeAt(t.x, t.y)) b[t.x, t.y] = true;
        foreach (var s in IslandLayout.GoldStones)
            if (s.x >= 0 && s.y >= 0 && s.x < w && s.y < h) b[s.x, s.y] = true;
        blocked = b;
    }

    public int Version { get { Refresh(); return version; } }
    public RectInt Bounds => new RectInt(0, 0, IslandLayout.Width, IslandLayout.Height);

    public bool Walkable(Vector2Int cell)
    {
        Refresh();
        return cell.x >= 0 && cell.y >= 0 && cell.x < IslandLayout.Width && cell.y < IslandLayout.Height && !blocked[cell.x, cell.y];
    }

    /// <summary>Aynı yükseklik serbest; bir basamak fark yalnız merdiven hücresiyle yatay/dikey (4 yönlü) komşulukta.</summary>
    public bool CanStep(Vector2Int a, Vector2Int b)
    {
        if (!Walkable(b)) return false;
        int ha = IslandLayout.HeightAt(a.x, a.y), hb = IslandLayout.HeightAt(b.x, b.y);
        if (ha == hb) return true;
        if (Mathf.Abs(ha - hb) != 1) return false;
        bool orthogonal = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        return orthogonal && (IslandLayout.IsStair(a.x, a.y) || IslandLayout.IsStair(b.x, b.y));
    }

    public Vector2Int CellOf(Vector2 world) =>
        new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(IslandLayout.Height - world.y));

    public Vector2 Center(Vector2Int cell) => IslandLayout.World(cell.x + 0.5f, cell.y + 0.5f);
}
