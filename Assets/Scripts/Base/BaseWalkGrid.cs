using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 4. oturum (1.1): Ana Üs yürünebilirlik ızgarası = <see cref="BaseOccupancyGrid.BlockedCells"/> (bina, inşaatçı kulübesi, dekor,
/// kahraman kampı, kilitli arazi doğası) + açılmış arazi sınırı. Engel kümesi önbellekli; <see cref="Invalidate"/> (bina kur/taşı/sil,
/// dekor, alan açma) ya da 0,25 sn'lik içerik denetimi değişiklik bulursa <see cref="Version"/> artar.
/// </summary>
public sealed class BaseWalkGrid : IWalkGrid
{
    public static readonly BaseWalkGrid Shared = new BaseWalkGrid(null);

    private static HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
    private static RectInt bounds;
    private static int version;
    private static long signature;
    private static float nextCheck = -1f;
    private static bool dirty = true;

    private readonly HashSet<Vector2Int> ignore;

    private BaseWalkGrid(HashSet<Vector2Int> ignore) { this.ignore = ignore; }

    /// <summary>Başlangıç/hedef yapısının hücrelerini yok sayan görünüm (inşaatçı kulübeden çıkar, yapıya yanaşır).</summary>
    public static BaseWalkGrid Ignoring(params RectInt[] rects)
    {
        if (rects == null || rects.Length == 0) return Shared;
        var set = new HashSet<Vector2Int>();
        foreach (var r in rects)
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++) set.Add(new Vector2Int(x, y));
        return new BaseWalkGrid(set);
    }

    /// <summary>Harita değişti: bir sonraki sorguda engeller yeniden okunur, yoldaki karakterler rotasını yeniler.</summary>
    public static void Invalidate() => dirty = true;

    private static void Refresh()
    {
        if (!dirty && Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.25f;
        var map = BaseMapManager.Instance;
        var area = map != null && map.BuildableRect.width > 0 ? map.BuildableRect : BaseWorldLayout.Buildable;
        var fresh = BaseOccupancyGrid.BlockedCells();
        long sig = area.GetHashCode() * 31L + fresh.Count;
        foreach (var c in fresh) sig += (c.x * 73856093L) ^ (c.y * 19349663L);
        if (dirty || sig != signature || area.Equals(bounds) == false)
        {
            if (sig != signature || !area.Equals(bounds)) version++;
            signature = sig;
            blocked = fresh;
            bounds = area;
        }
        dirty = false;
    }

    public int Version { get { Refresh(); return version; } }
    public RectInt Bounds { get { Refresh(); return bounds; } }

    public bool Walkable(Vector2Int cell)
    {
        Refresh();
        if (!bounds.Contains(cell)) return false;
        return !blocked.Contains(cell) || (ignore != null && ignore.Contains(cell));
    }

    public bool CanStep(Vector2Int a, Vector2Int b) => Walkable(b);
    public Vector2Int CellOf(Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
    public Vector2 Center(Vector2Int cell) => new Vector2(cell.x + 0.5f, cell.y + 0.5f);
}
