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
    private readonly List<Tile> roadTiles = new List<Tile>();
    private readonly List<Texture2D> roadTextures = new List<Texture2D>();
    private readonly List<Sprite> roadSprites = new List<Sprite>();

    // Yol lekesi hücreden taşar ki komşu hücreler kesintisiz, yuvarlak kenarlı bir patika oluştursun.
    private const int RoadTexture = 40;
    private const float RoadWorldSize = 1.42f;
    private static readonly Color32 DirtBase = new Color32(0x9C, 0x7B, 0x50, 0xFF);
    private static readonly Color32 DirtDark = new Color32(0x7C, 0x5E, 0x3C, 0xFF);
    private static readonly Color32 DirtLight = new Color32(0xB6, 0x95, 0x63, 0xFF);
    private static readonly Color32 Pebble = new Color32(0x93, 0x8D, 0x80, 0xFF);

    private void Awake()
    {
        Instance = this;
        roadBaseMap = CreateMap("StoneRoadBaseTilemap", -17);
        roadMap = CreateMap("StoneRoadTilemap", -16);
        for (int variant = 0; variant < 3; variant++)
        {
            var texture = MakeDirtTexture(1337 + variant * 101);
            var sprite = Sprite.Create(texture, new Rect(0, 0, RoadTexture, RoadTexture),
                new Vector2(0.5f, 0.5f), RoadTexture / RoadWorldSize);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.None;
            roadTextures.Add(texture);
            roadSprites.Add(sprite);
            roadTiles.Add(tile);
        }
    }

    /// <summary>Kenarı gürültülü, ortası opak, çakıllı toprak lekesi.</summary>
    private static Texture2D MakeDirtTexture(int seed)
    {
        var random = new System.Random(seed);
        var texture = new Texture2D(RoadTexture, RoadTexture, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        float half = RoadTexture * 0.5f;
        float phase = (float)random.NextDouble() * 6.28f;
        var pixels = new Color32[RoadTexture * RoadTexture];
        for (int y = 0; y < RoadTexture; y++)
        for (int x = 0; x < RoadTexture; x++)
        {
            float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx);
            // Organik kenar: açıya bağlı hafif dalga.
            float edge = 0.86f + 0.06f * Mathf.Sin(angle * 3f + phase) + 0.04f * Mathf.Sin(angle * 7f - phase);
            float alpha = Mathf.Clamp01((edge - radius) / 0.16f);
            float noise = (float)random.NextDouble();
            Color color = noise < 0.18f ? (Color)DirtDark : noise > 0.9f ? (Color)DirtLight : (Color)DirtBase;
            color = Color.Lerp(color, DirtDark, Mathf.Clamp01((radius - 0.55f) / 0.35f) * 0.45f);
            color.a = alpha;
            pixels[y * RoadTexture + x] = color;
        }
        // Birkaç çakıl taşı.
        int pebbles = 3 + random.Next(3);
        for (int i = 0; i < pebbles; i++)
        {
            int cx = 10 + random.Next(RoadTexture - 20), cy = 10 + random.Next(RoadTexture - 20);
            int r = 1 + random.Next(2);
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > r * r + 1) continue;
                int index = (cy + y) * RoadTexture + cx + x;
                Color32 shade = y > 0 ? new Color32(0xAE, 0xA9, 0x9C, 0xFF) : Pebble;
                shade.a = pixels[index].a;
                pixels[index] = shade;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
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
        foreach (var tile in roadTiles) if (tile != null) Destroy(tile);
        foreach (var sprite in roadSprites) if (sprite != null) Destroy(sprite);
        foreach (var texture in roadTextures) if (texture != null) Destroy(texture);
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
                // Hücreye göre sabit varyant: yol her yenilemede aynı görünür.
                int variant = Mathf.Abs(cell.x * 73856093 ^ cell.y * 19349663) % roadTiles.Count;
                roadMap.SetTile(new Vector3Int(cell.x, cell.y, 0), roadTiles[variant]);
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
