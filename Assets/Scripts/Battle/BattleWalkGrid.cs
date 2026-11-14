using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 4. oturum (1.1): savaş yürünebilirlik ızgarası (0,5 birimlik hücre). Kapalı hücreler <see cref="BattleObstacles"/> alanlarıdır
/// (Ana Üs, kuleler, Manastır, maden); yapı yıkılınca/kurulunca sürüm artar ve düşmanlar/müttefikler rotayı yeniler.
/// </summary>
public sealed class BattleWalkGrid : IWalkGrid
{
    public static readonly BattleWalkGrid Shared = new BattleWalkGrid();
    public const float CellSize = 0.5f;
    private const int HalfExtent = 80;   // ±40 birim

    private int builtVersion = -1;
    private readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();

    private void Refresh()
    {
        if (builtVersion == BattleObstacles.Version) return;
        blocked.Clear();
        foreach (var r in BattleObstacles.All())
        {
            int x0 = Mathf.FloorToInt(r.xMin / CellSize), x1 = Mathf.CeilToInt(r.xMax / CellSize);
            int y0 = Mathf.FloorToInt(r.yMin / CellSize), y1 = Mathf.CeilToInt(r.yMax / CellSize);
            for (int x = x0; x < x1; x++)
            for (int y = y0; y < y1; y++) blocked.Add(new Vector2Int(x, y));
        }
        builtVersion = BattleObstacles.Version;
    }

    public int Version { get { Refresh(); return builtVersion; } }
    public RectInt Bounds => new RectInt(-HalfExtent, -HalfExtent, HalfExtent * 2, HalfExtent * 2);
    public bool Walkable(Vector2Int cell) { Refresh(); return Bounds.Contains(cell) && !blocked.Contains(cell); }
    public bool CanStep(Vector2Int a, Vector2Int b) => Walkable(b);
    public Vector2Int CellOf(Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x / CellSize), Mathf.FloorToInt(world.y / CellSize));
    public Vector2 Center(Vector2Int cell) => new Vector2((cell.x + 0.5f) * CellSize, (cell.y + 0.5f) * CellSize);
}
