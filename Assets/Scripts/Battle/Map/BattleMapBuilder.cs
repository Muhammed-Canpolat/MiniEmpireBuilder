using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 7. oturum (D92): sefer savaşının bölge haritasını kurar — Sefer Haritası'ndaki adanın dikey sürümü (generate.py land() ile aynı karo kuralı):
/// deniz + sığ su halkası, Tiny Swords karo zemin ve alt kenar uçurumları, toprak yol (koyu kenar + benekler), üst köprü, bölge süsleri ve
/// yapıları. Engel olan süsler (ağaç gövdesi, büyük kaya, lav gölü, bölge yapıları) <see cref="BattleObstacles"/>'a yazılır.
/// Yalnız görsel + engel; oynanış kuralları BattleManager/EnemyController'da.
/// </summary>
public sealed class BattleMapBuilder : MonoBehaviour
{
    // Çizim sırası: hepsi yapıların (5–6) ve birimlerin altında.
    public const int OrderWater = -130, OrderWaves = -128, OrderShallow = -125, OrderCliff = -122, OrderGround = -120,
        OrderRoad = -112, OrderBridge = -111, OrderGroundDecor = -100, OrderShadow = 1, OrderDecor = 3;

    private const int RoadPixelsPerUnit = 24;
    private static readonly Dictionary<Texture2D, Sprite[]> tileCache = new Dictionary<Texture2D, Sprite[]>();

    public BattleRegionMap Map { get; private set; }
    public BattleLevelLayout Layout { get; private set; }
    /// <summary>Yerleştirilen süsler (test/görüntü için).</summary>
    public readonly List<GameObject> Decor = new List<GameObject>();
    public readonly List<GameObject> Landmarks = new List<GameObject>();
    private readonly List<Rect> occupied = new List<Rect>();

    public static BattleMapBuilder Build(BattleRegionMap map, BattleLevelLayout layout)
    {
        var root = new GameObject("BattleMap");
        var builder = root.AddComponent<BattleMapBuilder>();
        builder.Map = map; builder.Layout = layout;
        BattleTerrain.Begin(map, layout);
        builder.BuildWater();
        builder.BuildTerrain();
        builder.BuildRoad();
        builder.BuildBridge();
        builder.ReserveGameplayAreas();
        builder.PlaceLandmarks();
        builder.PlaceDecor();
        return builder;
    }

    private void OnDestroy()
    {
        if (BattleTerrain.Map == Map) BattleTerrain.Clear();
    }

    // ==================== DENİZ ====================

    private void BuildWater()
    {
        if (Camera.main != null) Camera.main.backgroundColor = Map.waterColor;
        var bounds = BattleTerrain.CameraBounds;
        var water = Child("Water", OrderWater, BaseMapManager.MakeSquareSprite(Color.white), Map.waterColor);
        water.transform.position = new Vector3(bounds.center.x, bounds.center.y, 1f);
        water.transform.localScale = new Vector3(bounds.width + 30f, bounds.height + 30f, 1f);

        // Dalga çizgileri (generate.py water_canvas): kısa açık yaylar.
        var arc = ArcSprite();
        var rnd = new System.Random(Map.regionId * 71);
        var waveColor = Color.Lerp(Map.waterColor, Color.white, 0.25f);
        for (int i = 0; i < 70; i++)
        {
            var p = new Vector2(Mathf.Lerp(bounds.xMin - 4f, bounds.xMax + 4f, (float)rnd.NextDouble()),
                Mathf.Lerp(bounds.yMin - 4f, bounds.yMax + 4f, (float)rnd.NextDouble()));
            if (BattleTerrain.IsLand(p) || NearLand(p, 1.2f)) continue;
            var wave = Child("Wave", OrderWaves, arc, waveColor);
            wave.transform.position = p;
            wave.transform.localScale = Vector3.one * (0.8f + (float)rnd.NextDouble() * 0.5f);
        }

        // Sığ su halkası: karaya uzaklığa göre yumuşak açık turkuaz (generate.py land(shallow=True)).
        const int ppu = 4;
        var b = BattleMapGeometry.Bounds;
        int w = Mathf.CeilToInt((b.width + 4f) * ppu), h = Mathf.CeilToInt((b.height + 4f) * ppu);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        Color32 shallow = Map.shallowColor;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var world = new Vector2(b.xMin - 2f + (x + 0.5f) / ppu, b.yMin - 2f + (y + 0.5f) / ppu);
                float d = DistanceToLand(world, 1.6f);
                // Uçurum alt kenarı 1 karo aşağı uzanır: halka aşağıda biraz daha geniş.
                float a = Mathf.Clamp01(1f - (d - 0.25f) / 1.05f);
                pixels[y * w + x] = new Color32(shallow.r, shallow.g, shallow.b, (byte)(a * a * (3f - 2f * a) * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var ring = Child("ShallowRing", OrderShallow, Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, ppu), Color.white);
        ring.transform.position = new Vector3(b.xMin - 2f, b.yMin - 2f - 0.6f, 0.9f);
    }

    private float DistanceToLand(Vector2 p, float max)
    {
        var cell = BattleMapGeometry.CellOf(p);
        float best = max;
        int reach = Mathf.CeilToInt(max) + 1;
        for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int c = cell.x + dx, r = cell.y + dy;
                if (!Map.IsLandCell(c, r)) continue;
                var center = BattleMapGeometry.CellCenter(c, r);
                var rect = new Rect(center.x - 0.5f, center.y - 0.5f, 1f, 1f);
                best = Mathf.Min(best, BattleObstacles.DistanceTo(rect, p));
            }
        return best;
    }

    private bool NearLand(Vector2 p, float distance) => DistanceToLand(p, distance + 0.1f) < distance;

    private static Sprite arcSprite;
    private static Sprite ArcSprite()
    {
        if (arcSprite != null) return arcSprite;
        const int w = 36, h = 10;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            float t = (x - w / 2f) / (w / 2f);
            int y = Mathf.RoundToInt((1f - t * t) * (h - 4)) + 1;
            for (int k = 0; k < 2; k++) if (y + k < h) px[(y + k) * w + x] = new Color32(255, 255, 255, 255);
        }
        tex.SetPixels32(px); tex.Apply();
        return arcSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
    }

    // ==================== KARO ZEMİN ====================

    /// <summary>Karo setinden (9×6, 64 px) çalışma anında dilimler (Tiny Swords düzeni; satır 0 = görselin üstü).</summary>
    public static Sprite TileSprite(Texture2D tileset, int col, int row)
    {
        if (tileset == null) return null;
        if (!tileCache.TryGetValue(tileset, out var sprites) || sprites == null)
            tileCache[tileset] = sprites = new Sprite[54];
        int index = row * 9 + col;
        if (sprites[index] == null)
        {
            var rect = new Rect(col * 64, tileset.height - (row + 1) * 64, 64, 64);
            sprites[index] = Sprite.Create(tileset, rect, new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
            sprites[index].name = $"{tileset.name}_{col}_{row}";
        }
        return sprites[index];
    }

    private static readonly Dictionary<Sprite, Tile> tiles = new Dictionary<Sprite, Tile>();
    private static Tile TileFor(Sprite sprite)
    {
        if (sprite == null) return null;
        if (!tiles.TryGetValue(sprite, out var tile) || tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tiles[sprite] = tile;
        }
        return tile;
    }

    /// <summary>generate.py land(): önce yamaç (altı boş kara hücresinin altına satır 5), sonra çim (komşuluğa göre sütun 5–8, satır 0–3).</summary>
    private void BuildTerrain()
    {
        if (Map.terrainPrefab != null)
        {
            Instantiate(Map.terrainPrefab, transform);
            return;
        }
        var grid = new GameObject("Grid").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        grid.cellSize = Vector3.one;
        grid.transform.position = new Vector3(BattleMapGeometry.LeftX, BattleMapGeometry.TopY - BattleMapGeometry.Rows, 0.8f);
        var cliffs = NewTilemap(grid.transform, "Cliffs", OrderCliff);
        var ground = NewTilemap(grid.transform, "Ground", OrderGround);
        int rows = BattleMapGeometry.Rows, cols = BattleMapGeometry.Columns;
        bool At(int c, int r) => Map.IsLandCell(c, r);
        int ColFor(bool left, bool right) => left && right ? 6 : right ? 5 : left ? 7 : 8;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!At(c, r) || At(c, r + 1)) continue;
                bool left = At(c - 1, r) && !At(c - 1, r + 1), right = At(c + 1, r) && !At(c + 1, r + 1);
                cliffs.SetTile(new Vector3Int(c, rows - 2 - r, 0), TileFor(TileSprite(Map.tileset, ColFor(left, right), 5)));
            }
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!At(c, r)) continue;
                int col = ColFor(At(c - 1, r), At(c + 1, r));
                bool up = At(c, r - 1), down = At(c, r + 1);
                int row = up && down ? 1 : down ? 0 : up ? 2 : 3;
                ground.SetTile(new Vector3Int(c, rows - 1 - r, 0), TileFor(TileSprite(Map.tileset, col, row)));
            }
    }

    private static Tilemap NewTilemap(Transform parent, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var map = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = order;
        renderer.mode = TilemapRenderer.Mode.Chunk;
        return map;
    }

    // ==================== YOL ====================

    /// <summary>Toprak yol dokusu: rotalar boyunca disk damgası (koyu kenar, açık toprak, taş benekleri) — generate.py path() karşılığı.</summary>
    private void BuildRoad()
    {
        var b = BattleMapGeometry.Bounds;
        float x0 = b.xMin, y0 = b.yMin, top = BattleMapGeometry.TopY - 0.2f;
        int w = Mathf.CeilToInt(b.width * RoadPixelsPerUnit), h = Mathf.CeilToInt((top - y0) * RoadPixelsPerUnit);
        var edge = new float[w * h]; var fill = new float[w * h];
        float edgeR = BattleMapGeometry.RoadEdgeWidth * 0.5f, fillR = BattleMapGeometry.RoadWidth * 0.5f;
        void Stamp(float[] buffer, Vector2 p, float radius)
        {
            float cx = (p.x - x0) * RoadPixelsPerUnit, cy = (p.y - y0) * RoadPixelsPerUnit, rp = radius * RoadPixelsPerUnit;
            int xmin = Mathf.Max(0, Mathf.FloorToInt(cx - rp - 1)), xmax = Mathf.Min(w - 1, Mathf.CeilToInt(cx + rp + 1));
            int ymin = Mathf.Max(0, Mathf.FloorToInt(cy - rp - 1)), ymax = Mathf.Min(h - 1, Mathf.CeilToInt(cy + rp + 1));
            for (int y = ymin; y <= ymax; y++)
                for (int x = xmin; x <= xmax; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(rp - d + 0.5f);
                    int i = y * w + x;
                    if (a > buffer[i]) buffer[i] = a;
                }
        }
        foreach (var route in Layout.Routes)
            for (float s = 0f; s <= route.Length; s += 0.08f)
            {
                var p = route.PointAt(s);
                if (p.y > top + edgeR) continue;
                Stamp(edge, p, edgeR);
                Stamp(fill, p, fillR);
            }
        Color32 edgeColor = Map.roadEdgeColor, dirt = Map.roadColor;
        var speckle = (Color32)Color.Lerp(Map.roadColor, Map.roadEdgeColor, 0.3f);
        var light = (Color32)Color.Lerp(Map.roadColor, Color.white, 0.18f);
        var rnd = new System.Random(Map.regionId * 977 + Layout.levelNumber);
        var pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            float e = edge[i], f = fill[i];
            if (e <= 0f) continue;
            var c = Color32.Lerp(edgeColor, dirt, f);
            if (f > 0.95f)
            {
                double roll = rnd.NextDouble();
                if (roll < 0.035) c = speckle; else if (roll < 0.05) c = light;
            }
            c.a = (byte)(e * 255f);
            pixels[i] = c;
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var road = Child("Road", OrderRoad, Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, RoadPixelsPerUnit), Color.white);
        road.transform.position = new Vector3(x0, y0, 0.7f);
    }

    /// <summary>Üst köprü (generate.py bridge): kalaslar + iki yanda koyu direk; haritanın üst kenarından denize uzanır (düşman girişi).</summary>
    private void BuildBridge()
    {
        const int ppu = 32;
        float width = 1.7f, bottom = BattleMapGeometry.TopY - 0.7f, top = BattleMapGeometry.TopY + 5f;
        int w = Mathf.CeilToInt((width + 0.6f) * ppu), h = Mathf.CeilToInt((top - bottom) * ppu);
        var px = new Color32[w * h];
        Color32 plank = Map.bridgeColor, plankDark = new Color32(92, 58, 30, 255), plankLight = new Color32(196, 146, 92, 255),
            post = new Color32(110, 70, 36, 255), postDark = new Color32(70, 42, 20, 255);
        int postW = Mathf.RoundToInt(0.18f * ppu), plankH = Mathf.RoundToInt(0.34f * ppu);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c = default;
                bool leftPost = x < postW + 2, rightPost = x >= w - postW - 2;
                if (leftPost || rightPost) c = (x == 0 || x == w - 1 || x == postW + 1 || x == w - postW - 2) ? postDark : post;
                else
                {
                    int k = y % plankH;
                    c = k == 0 || k == plankH - 1 ? plankDark : k == 2 ? plankLight : plank;
                    if (x == postW + 2 || x == w - postW - 3) c = plankDark;
                }
                px[y * w + x] = c;
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixels32(px); tex.Apply(false, true);
        var bridge = Child("Bridge", OrderBridge, Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), ppu), Color.white);
        bridge.transform.position = new Vector3(0f, bottom, 0.65f);
        var shadow = Child("BridgeShadow", OrderShallow + 1, BaseMapManager.MakeSquareSprite(Color.white), new Color(0f, 0f, 0f, 0.22f));
        shadow.transform.position = new Vector3(0.15f, (bottom + top) * 0.5f - 0.15f, 0.66f);
        shadow.transform.localScale = new Vector3(width + 0.5f, top - bottom, 1f);
    }

    // ==================== SÜSLER ====================

    /// <summary>Oynanış alanları süslerden boş kalır: yuvalar (+ kule görseli), Ana Üs, maden, kahraman doğuşu.</summary>
    private void ReserveGameplayAreas()
    {
        occupied.Clear();
        if (Layout.slots != null)
            foreach (var s in Layout.slots) occupied.Add(Rect.MinMaxRect(s.x - 1.5f, s.y - 1.4f, s.x + 1.5f, s.y + 2.8f));
        var baseRect = BattleLayoutGenerator.BaseRect(Layout.basePosition);
        occupied.Add(Rect.MinMaxRect(baseRect.xMin - 1.4f, baseRect.yMin - 1.6f, baseRect.xMax + 1.4f, baseRect.yMax + 1.2f));
        if (Layout.HasMine) occupied.Add(new Rect(Layout.minePosition.x - 2f, Layout.minePosition.y - 1.6f, 4f, 4.2f));
        occupied.Add(new Rect(Layout.heroSpawn.x - 1f, Layout.heroSpawn.y - 1f, 2f, 2.4f));
        occupied.Add(Rect.MinMaxRect(-2f, BattleMapGeometry.TopY - 3f, 2f, BattleMapGeometry.TopY + 6f));   // köprü girişi
    }

    private bool Overlaps(Rect r)
    {
        foreach (var o in occupied) if (o.Overlaps(r)) return true;
        return false;
    }

    private bool RectOnLand(Rect r)
    {
        for (float x = r.xMin; x <= r.xMax + 0.01f; x += Mathf.Max(0.3f, r.width / 3f))
            for (float y = r.yMin; y <= r.yMax + 0.01f; y += Mathf.Max(0.3f, r.height / 3f))
                if (!BattleTerrain.IsLand(new Vector2(x, y))) return false;
        return true;
    }

    private float RoadDistance(Rect r)
    {
        float best = float.MaxValue;
        for (float x = r.xMin; x <= r.xMax + 0.01f; x += Mathf.Max(0.25f, r.width / 4f))
            for (float y = r.yMin; y <= r.yMax + 0.01f; y += Mathf.Max(0.25f, r.height / 4f))
                best = Mathf.Min(best, Layout.DistanceToRoad(new Vector2(x, y)));
        return best;
    }

    private void PlaceLandmarks()
    {
        if (Map.landmarks == null) return;
        foreach (var lm in Map.landmarks)
        {
            if (lm?.sprite == null || lm.candidates == null) continue;
            float aspect = lm.sprite.bounds.size.x / Mathf.Max(0.01f, lm.sprite.bounds.size.y);
            // Bölge yapısı ada kenarına sığsın: genişlik en çok 4,2 birim (boy orantılı küçülür).
            float height = Mathf.Min(lm.height, 4.2f / Mathf.Max(0.1f, aspect));
            float width = height * aspect;
            bool Fits(Vector2 c, out Rect rect, out Rect foot)
            {
                rect = new Rect(c.x - width * 0.5f, c.y, width, height);
                foot = Rect.MinMaxRect(rect.xMin + width * 0.1f, rect.yMin, rect.xMax - width * 0.1f, rect.yMin + height * 0.45f);
                return RectOnLand(foot) && !Overlaps(rect) && RoadDistance(rect) >= BattleMapGeometry.RoadEdgeWidth * 0.5f + 0.3f;
            }
            // Önce tasarımdaki adaylar (haritadaki yer); tutmazsa ilk adayın tarafında, ona en yakın uygun yer (ızgara taraması).
            Vector2? spot = null;
            foreach (var c in lm.candidates) if (Fits(c, out _, out _)) { spot = c; break; }
            if (spot == null && lm.candidates.Length > 0)
            {
                var want = lm.candidates[0];
                float best = float.MaxValue;
                for (float y = -4.5f; y <= BattleMapGeometry.TopY - 4f; y += 0.5f)
                    for (float x = -6.5f; x <= 6.5f; x += 0.5f)
                    {
                        var c = new Vector2(x, y);
                        if (Mathf.Sign(x) != Mathf.Sign(want.x) && Mathf.Abs(want.x) > 1f) continue;
                        float d = Vector2.Distance(c, want);
                        if (d < best && Fits(c, out _, out _)) { best = d; spot = c; }
                    }
            }
            if (spot == null) continue;
            Fits(spot.Value, out var placedRect, out var placedFoot);
            var go = SpriteAt(lm.name, lm.sprite, spot.Value, height, lm.tint, OrderDecor, true);
            if (lm.blocksMovement) BattleObstacles.Register(go, Rect.MinMaxRect(placedFoot.xMin, placedFoot.yMin, placedFoot.xMax, placedRect.yMin + height * 0.7f));
            occupied.Add(placedRect);
            Landmarks.Add(go);
        }
    }

    private void PlaceDecor()
    {
        if (Map.decor == null) return;
        var rnd = new System.Random(Map.regionId * 1000 + Layout.levelNumber * 17);
        float R() => (float)rnd.NextDouble();
        bool lowQuality = BattleFxQuality.Low;
        foreach (var entry in Map.decor)
        {
            if (entry?.sprites == null || entry.sprites.Length == 0) continue;
            int placed = 0;
            for (int attempt = 0; attempt < entry.count * 40 && placed < entry.count; attempt++)
            {
                var sprite = entry.sprites[rnd.Next(entry.sprites.Length)];
                if (sprite == null) continue;
                float height = Mathf.Lerp(entry.height.x, entry.height.y, R());
                float width = height * sprite.bounds.size.x / Mathf.Max(0.01f, sprite.bounds.size.y);
                var p = new Vector2(Mathf.Lerp(-7f, 7f, R()), Mathf.Lerp(-5.5f, BattleMapGeometry.TopY - 1f, R()));
                if (entry.preferEdges && Layout.DistanceToRoad(p) < 3.2f && R() < 0.75f) continue;
                float foot = Mathf.Max(entry.footprint, width * 0.45f);
                var footRect = new Rect(p.x - foot * 0.5f, p.y - 0.15f, foot, 0.5f);
                var visual = new Rect(p.x - width * 0.5f, p.y, width, height);
                if (!RectOnLand(footRect)) continue;
                if (RoadDistance(footRect) < BattleMapGeometry.RoadEdgeWidth * 0.5f + entry.roadClearance) continue;
                // Uzun süs (ağaç) yolu görsel olarak kapatmasın: gövdenin üst yarısı da yoldan uzak.
                if (height > 1.6f && RoadDistance(Rect.MinMaxRect(visual.xMin + width * 0.25f, visual.yMin, visual.xMax - width * 0.25f, visual.yMax - height * 0.3f)) < 0.6f) continue;
                if (Overlaps(entry.blocksMovement || height > 1.6f ? visual : footRect)) continue;
                var go = SpriteAt(entry.name, sprite, p, height, entry.tint, height > 1.6f ? OrderDecor : OrderGroundDecor + 2, entry.shadow);
                if (R() < 0.5f) go.GetComponentInChildren<SpriteRenderer>().flipX = true;
                if (entry.blocksMovement) BattleObstacles.Register(go, Rect.MinMaxRect(p.x - entry.footprint * 0.5f, p.y - 0.1f, p.x + entry.footprint * 0.5f, p.y + Mathf.Min(0.6f, height * 0.4f)));
                if (entry.sway && !lowQuality)
                    go.transform.DORotate(new Vector3(0f, 0f, 1.4f), 2.2f + R()).From(new Vector3(0f, 0f, -1.4f)).SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo).SetDelay(R()).SetLink(go);
                occupied.Add(entry.blocksMovement || height > 1.6f ? visual : footRect);
                Decor.Add(go);
                placed++;
            }
        }
    }

    /// <summary>Sprite'ı alt-orta noktası <paramref name="bottom"/>'da, görünen yüksekliği <paramref name="height"/> olacak şekilde kurar
    /// (kök alt-ortada: salınım gövde dibinden döner).</summary>
    private GameObject SpriteAt(string name, Sprite sprite, Vector2 bottom, float height, Color tint, int order, bool shadow)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(bottom.x, bottom.y, 0.5f);
        var body = new GameObject("Sprite");
        body.transform.SetParent(root.transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = order;
        float scale = height / Mathf.Max(0.01f, sprite.bounds.size.y);
        body.transform.localScale = Vector3.one * scale;
        // Pivot nerede olursa olsun alt kenar köke otursun.
        body.transform.localPosition = new Vector3(0f, -sprite.bounds.min.y * scale, 0f);
        if (shadow)
        {
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(root.transform, false);
            var ss = sh.AddComponent<SpriteRenderer>();
            ss.sprite = BaseMapManager.MakeCircleSprite(Color.white);
            ss.color = new Color(0f, 0f, 0f, 0.22f);
            ss.sortingOrder = OrderGroundDecor;
            float w = sprite.bounds.size.x * scale * 0.7f;
            sh.transform.localScale = new Vector3(w, Mathf.Max(0.18f, w * 0.28f), 1f);
            sh.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        }
        return root;
    }

    private GameObject Child(string name, int order, Sprite sprite, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }
}

/// <summary>
/// 7. oturum (D94): atmosfer efektleri grafik kalitesine bağlı. Unity kalite seviyesi (Ayarlar/QualitySettings) en düşükse ya da
/// oyuncu "Düşük efekt" seçtiyse partikül sayısı yarıya iner, salınım/kabarcık gibi süs animasyonları kapanır.
/// </summary>
public static class BattleFxQuality
{
    public const string PrefKey = "MEB_LowFx";

    /// <summary>Düşük kalite: QualitySettings en alt seviye ya da ayarlardaki düşük efekt anahtarı.</summary>
    public static bool Low
    {
        get
        {
            if (forced.HasValue) return forced.Value;
            return QualitySettings.GetQualityLevel() == 0 || PlayerPrefs.GetInt(PrefKey, 0) == 1;
        }
    }

    private static bool? forced;
    /// <summary>Testler: kaliteyi zorla (null = gerçek ayar).</summary>
    public static void Force(bool? low) => forced = low;

    /// <summary>Partikül/örnek sayısı tavanı: düşük kalitede yarısı.</summary>
    public static int Budget(int normal) => Low ? Mathf.Max(1, normal / 2) : normal;
}
