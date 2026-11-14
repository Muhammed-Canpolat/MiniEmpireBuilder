using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// İnşaatçı yürüyüşü: ortak <see cref="GridPathfinder"/> + <see cref="BaseWalkGrid"/> üzerinden (4. oturum 1.1). Dolu hücrelerin
/// (bina, kulübe, dekor, kamp) etrafından dolanır; hedefe ulaşılamazsa en yakın ulaşılabilir hücrede durur.
/// </summary>
public static class BuilderPathfinder
{
    public static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

    public static List<Vector3> FindPath(Vector3 from, Vector3 to, RectInt? goalFootprint, params RectInt[] ignoreCells) =>
        FindPath(from, to, goalFootprint, out _, ignoreCells);

    /// <summary>
    /// <paramref name="goalFootprint"/> verilirse yapının bitişiğindeki (4 yönlü) boş hücrede durur (yapıya girmez);
    /// verilmezse doğrudan <paramref name="to"/> noktasına gider. <paramref name="reached"/> false: en yakın noktaya kadar.
    /// </summary>
    public static List<Vector3> FindPath(Vector3 from, Vector3 to, RectInt? goalFootprint, out bool reached,
        params RectInt[] ignoreCells)
    {
        var grid = BaseWalkGrid.Ignoring(ignoreCells);
        System.Func<Vector2Int, bool> isGoal = null;
        if (goalFootprint.HasValue)
        {
            var r = goalFootprint.Value;
            isGoal = c => grid.Walkable(c) &&
                c.x >= r.xMin - 1 && c.x <= r.xMax && c.y >= r.yMin - 1 && c.y <= r.yMax &&
                !(c.x >= r.xMin && c.x < r.xMax && c.y >= r.yMin && c.y < r.yMax) &&
                (c.x >= r.xMin && c.x < r.xMax || c.y >= r.yMin && c.y < r.yMax);
        }
        var path = GridPathfinder.FindPath(grid, from, to, out reached, isGoal);
        var result = new List<Vector3>(path.Count);
        foreach (var p in path) result.Add(new Vector3(p.x, p.y, 0f));
        return result;
    }
}
