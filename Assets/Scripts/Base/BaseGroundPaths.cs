using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Shortest stone routes beneath placed buildings.</summary>
public sealed class BaseGroundPaths : MonoBehaviour
{
    public static BaseGroundPaths Instance { get; private set; }
    private Tilemap roadBaseMap;
    private Tilemap roadMap;
    private Tile roadTile;
    private Tile roadBaseTile;
    private Texture2D roadTexture;
    private Sprite roadSprite;

    private void Awake()
    {
        Instance = this;
        roadBaseMap = CreateMap("StoneRoadBaseTilemap", -17);
        roadMap = CreateMap("StoneRoadTilemap", -16);
        roadTile = ScriptableObject.CreateInstance<Tile>();
        roadBaseTile = ScriptableObject.CreateInstance<Tile>();
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        roadTexture = texture;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            texture.SetPixel(x, y, new Color32(0x8A, 0x7A, 0x5A, 0xFF));
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32),
            new Vector2(0.5f, 0.5f), 32f);
        roadSprite = sprite;
        roadBaseTile.sprite = sprite;
        roadBaseTile.color = Color.white;
        roadBaseTile.flags = TileFlags.None;
        roadTile.sprite = Resources.Load<DecorData>("Decor/stone_path")?.sprite ?? sprite;
        roadTile.color = Color.white;
        roadTile.flags = TileFlags.None;
    }

    private Tilemap CreateMap(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var map = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = order;
        return map;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (roadTile != null) Destroy(roadTile);
        if (roadBaseTile != null) Destroy(roadBaseTile);
        if (roadSprite != null) Destroy(roadSprite);
        if (roadTexture != null) Destroy(roadTexture);
    }

    public void Refresh()
    {
        if (roadMap == null || roadBaseMap == null) return;
        roadMap.ClearAllTiles();
        roadBaseMap.ClearAllTiles();
        var buildings = FindObjectsByType<BuildingIdentifier>(FindObjectsSortMode.None)
            .Where(item => item != null && item.gameObject.activeInHierarchy).ToArray();
        var main = buildings.FirstOrDefault(item => item.buildingType == BuildingType.MainBase);
        if (main == null) return;
        var blocked = new HashSet<Vector2Int>();
        foreach (var building in buildings)
        {
            Vector2 size = BaseWorldLayout.Footprint(building.buildingType);
            Vector2 origin = (Vector2)building.transform.position - size * 0.5f;
            int left = Mathf.RoundToInt(origin.x);
            int bottom = Mathf.RoundToInt(origin.y);
            for (int y = 0; y < Mathf.CeilToInt(size.y); y++)
            for (int x = 0; x < Mathf.CeilToInt(size.x); x++)
                blocked.Add(new Vector2Int(left + x, bottom + y));
        }
        if (DecorPlacementController.Instance != null)
            foreach (var decor in DecorPlacementController.Instance.Placed)
            {
                if (decor == null || decor.Data == null) continue;
                for (int y = 0; y < decor.Data.footprint.y; y++)
                for (int x = 0; x < decor.Data.footprint.x; x++)
                    blocked.Add(decor.Cell + new Vector2Int(x, y));
            }
        var bounds = BaseMapManager.Instance?.BuildableRect ?? BaseWorldLayout.Buildable;
        Vector2Int start = Access(main, blocked, bounds);
        foreach (var building in buildings)
        {
            if (building == main) continue;
            foreach (var cell in AStar(start, Access(building, blocked, bounds), blocked, bounds))
            {
                roadBaseMap.SetTile(new Vector3Int(cell.x, cell.y, 0), roadBaseTile);
                roadMap.SetTile(new Vector3Int(cell.x, cell.y, 0), roadTile);
            }
        }
    }

    private static Vector2Int Access(BuildingIdentifier building, HashSet<Vector2Int> blocked,
        RectInt bounds)
    {
        Vector2 size = BaseWorldLayout.Footprint(building.buildingType);
        Vector2 center = building.transform.position;
        int left = Mathf.RoundToInt(center.x - size.x * 0.5f);
        int bottom = Mathf.RoundToInt(center.y - size.y * 0.5f);
        int right = left + Mathf.RoundToInt(size.x);
        int top = bottom + Mathf.RoundToInt(size.y);
        var options = new List<Vector2Int>();
        int middleX = left + Mathf.RoundToInt(size.x * 0.5f);
        int middleY = bottom + Mathf.RoundToInt(size.y * 0.5f);
        options.Add(new Vector2Int(middleX, bottom - 1));
        options.Add(new Vector2Int(middleX, top));
        options.Add(new Vector2Int(left - 1, middleY));
        options.Add(new Vector2Int(right, middleY));
        for (int x = left; x < right; x++)
        {
            options.Add(new Vector2Int(x, bottom - 1));
            options.Add(new Vector2Int(x, top));
        }
        for (int y = bottom; y < top; y++)
        {
            options.Add(new Vector2Int(left - 1, y));
            options.Add(new Vector2Int(right, y));
        }
        foreach (var cell in options)
            if (bounds.Contains(cell) && !blocked.Contains(cell)) return cell;
        return new Vector2Int(middleX, bottom - 1);
    }

    private static IEnumerable<Vector2Int> AStar(Vector2Int start, Vector2Int goal,
        HashSet<Vector2Int> blocked, RectInt bounds)
    {
        var open = new List<Vector2Int> { start };
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        var cost = new Dictionary<Vector2Int, int> { [start] = 0 };
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right,
            Vector2Int.down, Vector2Int.left };
        while (open.Count > 0)
        {
            int bestIndex = 0;
            int bestScore = int.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                int score = cost[open[i]] + Mathf.Abs(goal.x - open[i].x)
                    + Mathf.Abs(goal.y - open[i].y);
                if (score >= bestScore) continue;
                bestScore = score;
                bestIndex = i;
            }
            Vector2Int current = open[bestIndex];
            open.RemoveAt(bestIndex);
            if (current == goal)
            {
                var route = new List<Vector2Int>();
                while (current != start)
                {
                    route.Add(current);
                    current = previous[current];
                }
                route.Add(start);
                return route;
            }
            foreach (var direction in directions)
            {
                Vector2Int next = current + direction;
                if (!bounds.Contains(next) || blocked.Contains(next)) continue;
                int nextCost = cost[current] + 1;
                if (cost.TryGetValue(next, out int oldCost) && nextCost >= oldCost) continue;
                cost[next] = nextCost;
                previous[next] = current;
                if (!open.Contains(next)) open.Add(next);
            }
        }
        return System.Array.Empty<Vector2Int>();
    }
}
