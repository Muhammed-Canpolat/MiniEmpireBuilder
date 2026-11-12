using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Martı Adası araziyi ve doğayı gerçek Tiny Swords karolarıyla pişirir (island_base_v2 §10 Faz A, kit §7).
/// Çıktılar: <c>Resources/Island/IslandTerrain.prefab</c> (su, köpük, sv0, gölge, yar, sv1, sv2 katmanları) ve
/// <c>Resources/Island/IslandNature.prefab</c> (ağaç, çalı, kaya, su kayası, altın taşı, bulutlar). Kodla çizilmiş görsel yoktur.
/// Elle düzenlenmiş prefab (içerik karması kayıtlıdan farklı) sormadan üzerine yazılmaz; <c>-force</c> ya da menü "Force".
/// Batchmode: <c>-executeMethod BakeIslandTerrain.BakeFromCommandLine</c>.
/// </summary>
public static class BakeIslandTerrain
{
    private const string TilesDir = "Assets/Tiny Swords/Terrain/Tileset/Tilemap Settings/";
    private const string OutDir = "Assets/Resources/Island";
    private const string TerrainPath = OutDir + "/IslandTerrain.prefab";
    private const string NaturePath = OutDir + "/IslandNature.prefab";
    private const string RulesVersion = "v1-phaseA";
    private const string Root = "Assets/Tiny Swords";

    // Sıralama bantları (kit §7.4)
    private const int OrderBg = -100, OrderFoam = -95, OrderWaterDecor = -93, OrderFlat = -90,
        OrderShadow1 = -80, OrderElev1 = -70, OrderShadow2 = -60, OrderElev2 = -50, OrderShadow3 = -40, OrderElev3 = -30,
        OrderEdgeClouds = 650, OrderZoneClouds = 800;

    [MenuItem("Tools/Island/Bake Island Terrain")]
    public static void BakeMenu() => Bake(false);

    [MenuItem("Tools/Island/Bake Island Terrain (force)")]
    public static void BakeForceMenu() => Bake(true);

    public static void BakeFromCommandLine()
    {
        bool force = Environment.GetCommandLineArgs().Contains("-force");
        bool ok = Bake(force);
        if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
    }

    public static bool Bake(bool force)
    {
        Directory.CreateDirectory(OutDir);
        var terrainOk = BakeTerrain(force);
        var natureOk = BakeNature(force);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return terrainOk && natureOk;
    }

    // ==================== ARAZİ ====================

    private static string Sha(string text)
    {
        using (var sha = SHA1.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
    }

    private static string TerrainInputHash() => Sha(RulesVersion + string.Join("\n", IslandLayout.Rows));

    private static TileBase Tile(int color, int index) =>
        AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesDir}Sliced Tiles/Tilemap_color{color}_{index}.asset");

    private static bool EditedByHand(string path)
    {
        if (!File.Exists(path)) return false;
        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var info = contents.GetComponent<IslandBakeInfo>();
            if (info == null) return true;
            return ContentHash(contents) != info.contentHash;
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static bool BakeTerrain(bool force)
    {
        string inputHash = TerrainInputHash();
        if (File.Exists(TerrainPath) && EditedByHand(TerrainPath) && !force)
        {
            Debug.LogWarning("[IslandBake] IslandTerrain.prefab elle düzenlenmiş görünüyor; üzerine yazılmadı (-force ile zorla).");
            return false;
        }
        var rules = IslandTerrainRules.Build(IslandLayout.Rows);
        foreach (var warning in rules.warnings) Debug.LogWarning("[IslandBake] arazi uyarısı: " + warning);

        var water = AssetDatabase.LoadAssetAtPath<TileBase>(TilesDir + "Water Background color.asset");
        var shadow = AssetDatabase.LoadAssetAtPath<TileBase>(TilesDir + "Shadow.asset");
        var foamTiles = MakeFoamVariants();
        if (water == null || shadow == null || foamTiles.Length == 0)
        {
            Debug.LogError("[IslandBake] Tiny Swords su/gölge/köpük karoları bulunamadı.");
            return false;
        }

        var root = new GameObject("IslandTerrain");
        try
        {
            root.AddComponent<Grid>().cellSize = Vector3.one;
            var bg = NewLayer(root.transform, "L0 Water BG", OrderBg);
            var foam = NewLayer(root.transform, "L1 Water Foam", OrderFoam);
            var flat = NewLayer(root.transform, "L2 Flat Ground", OrderFlat);
            foam.animationFrameRate = 1f;

            // Su: kameradan çok daha geniş (portre ±12 sütun, ±20 satır; kaydırma payıyla)
            var waterBlock = new BoundsInt(-14, -18, 0, IslandLayout.Width + 28, IslandLayout.Height + 36, 1);
            var waterTiles = new TileBase[waterBlock.size.x * waterBlock.size.y];
            for (int i = 0; i < waterTiles.Length; i++) waterTiles[i] = water;
            bg.SetTilesBlock(waterBlock, waterTiles);

            var rng = new System.Random(7);
            foreach (var cell in rules.foam.OrderBy(c => c.y).ThenBy(c => c.x))
                foam.SetTile(new Vector3Int(cell.x, cell.y, 0), foamTiles[rng.Next(foamTiles.Length)]);

            foreach (var pair in rules.flat)
                flat.SetTile(new Vector3Int(pair.Key.x, pair.Key.y, 0), Tile(IslandTerrainRules.LevelColor[0], pair.Value));

            int[] shadowOrder = { 0, OrderShadow1, OrderShadow2, OrderShadow3 };
            int[] elevOrder = { 0, OrderElev1, OrderElev2, OrderElev3 };
            for (int level = 1; level <= rules.maxLevel; level++)
            {
                var shadowMap = NewLayer(root.transform, $"L{2 * level + 1} Shadow (L{level})", shadowOrder[level]);
                var elevMap = NewLayer(root.transform, $"L{2 * level + 2} Elevated (L{level})", elevOrder[level]);
                foreach (var cell in rules.shadow[level].OrderBy(c => c.y).ThenBy(c => c.x))
                    shadowMap.SetTile(new Vector3Int(cell.x, cell.y, 0), shadow);
                int color = IslandTerrainRules.LevelColor[level];
                foreach (var pair in rules.elev[level])
                    elevMap.SetTile(new Vector3Int(pair.Key.x, pair.Key.y, 0), Tile(color, pair.Value));
            }
            foreach (var tilemap in root.GetComponentsInChildren<Tilemap>()) tilemap.CompressBounds();

            var info = root.AddComponent<IslandBakeInfo>();
            info.inputHash = inputHash;
            info.contentHash = ContentHash(root);
            PrefabUtility.SaveAsPrefabAsset(root, TerrainPath);
            Debug.Log($"[IslandBake] IslandTerrain.prefab yazıldı: zemin {rules.flat.Count}, köpük {rules.foam.Count}, merdiven {rules.stairs.Count}, " +
                $"seviye {rules.maxLevel}, uyarı {rules.warnings.Count}.");
            return true;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Tilemap NewLayer(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = order;
        renderer.mode = TilemapRenderer.Mode.Chunk;
        renderer.detectChunkCullingBounds = TilemapRenderer.DetectChunkCullingBounds.Auto;
        return go.GetComponent<Tilemap>();
    }

    /// <summary>Köpük 16 kare; fazları dağıtmak için 4 başlangıç karesiyle kopya (kit §4.5). Çıktı Resources/Island/Tiles.</summary>
    private static TileBase[] MakeFoamVariants()
    {
        var original = AssetDatabase.LoadAssetAtPath<AnimatedTile>(TilesDir + "Water Tile animated.asset");
        if (original == null) return new TileBase[0];
        Directory.CreateDirectory(OutDir + "/Tiles");
        var result = new List<TileBase>();
        int[] starts = { 1, 5, 9, 13 };   // 1 tabanlı başlangıç karesi
        for (int i = 0; i < starts.Length; i++)
        {
            string path = $"{OutDir}/Tiles/IslandFoam{i}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<AnimatedTile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.m_AnimatedSprites = original.m_AnimatedSprites;
            tile.m_MinSpeed = original.m_MinSpeed;
            tile.m_MaxSpeed = original.m_MaxSpeed;
            tile.m_AnimationStartTime = 0f;
            tile.m_AnimationStartFrame = starts[i];
            tile.m_TileColliderType = original.m_TileColliderType;
            EditorUtility.SetDirty(tile);
            result.Add(tile);
        }
        AssetDatabase.SaveAssets();
        return result.ToArray();
    }

    // ==================== İÇERİK KARMASI (elle düzenleme algısı) ====================

    private static string ContentHash(GameObject root)
    {
        var sb = new StringBuilder();
        foreach (var tilemap in root.GetComponentsInChildren<Tilemap>(true))
        {
            var renderer = tilemap.GetComponent<TilemapRenderer>();
            sb.Append(tilemap.name).Append('|').Append(renderer != null ? renderer.sortingOrder : 0).Append('|');
            var cells = new List<string>();
            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(pos);
                if (tile != null) cells.Add($"{pos.x},{pos.y}:{tile.name}");
            }
            cells.Sort(StringComparer.Ordinal);
            sb.Append(string.Join(";", cells)).Append('\n');
        }
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var p = sr.transform.position;
            sb.Append(sr.name).Append('|').Append(sr.sprite != null ? sr.sprite.name : "-").Append('|')
                .Append(p.x.ToString("F2")).Append(',').Append(p.y.ToString("F2")).Append('|').Append(sr.sortingOrder).Append('|')
                .Append(sr.gameObject.activeSelf).Append('\n');
        }
        return Sha(sb.ToString());
    }

    // ==================== DOĞA ====================

    private static int Order(Sprite sprite)
    {
        var match = Regex.Match(sprite.name, @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static Sprite[] Frames(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(Order).ToArray();

    private static Sprite One(string path) => Frames(path).FirstOrDefault();

    // Ayak çizgisi: merkezden aşağı piksel (kit §8.3)
    private static readonly float[] TreeFeet = { 113f, 121f, 74f, 72f };
    private static readonly float[] StumpFeet = { 112f, 112f, 72f, 72f };
    private static readonly float[] BushFeet = { 15f, 17f, 20f, 18f };
    private static readonly float[] RockFeet = { 19f, 21f, 22f, 24f };
    private static readonly float[] GoldFeet = { 15f, 19f, 23f, 26f, 30f, 34f };

    private static bool BakeNature(bool force)
    {
        string inputHash = Sha(RulesVersion + "nature" + string.Join(";", IslandLayout.Trees) + string.Join(";", IslandLayout.Bushes) +
            string.Join(";", IslandLayout.Rocks) + string.Join(";", IslandLayout.GoldStones) + string.Join(";", IslandLayout.WaterRocks));
        if (File.Exists(NaturePath) && EditedByHand(NaturePath) && !force)
        {
            Debug.LogWarning("[IslandBake] IslandNature.prefab elle düzenlenmiş görünüyor; üzerine yazılmadı (-force ile zorla).");
            return false;
        }
        var root = new GameObject("IslandNature");
        try
        {
            var rng = new System.Random(11);
            var terrain = IslandLayout.Terrain;

            // Ağaçlar: Tree1/2 (büyük) ağırlıklı, Tree3/4 küçük; kesilince kütük
            var treesGroup = Group(root.transform, "Trees");
            foreach (var cell in IslandLayout.Trees)
            {
                int variant = rng.Next(100) < 55 ? 1 + rng.Next(2) : 3 + rng.Next(2);
                var frames = Frames($"{Root}/Pawn and Resources/Wood/Trees/Tree{variant}.png");
                var stump = One($"{Root}/Pawn and Resources/Wood/Trees/Stump {variant}.png");
                Vector2 feet = FeetOf(cell, 0.12f);
                var holder = Prop(treesGroup, $"Tree{variant}_{cell.x}_{cell.y}", feet, frames, TreeFeet[variant - 1], 0);
                var tree = holder.gameObject.AddComponent<IslandTree>();
                tree.idle = frames; tree.stump = stump;
                Animate(holder.GetChild(0).gameObject, frames, rng);
            }

            var bushesGroup = Group(root.transform, "Bushes");
            foreach (var cell in IslandLayout.Bushes)
            {
                int variant = 1 + rng.Next(4);
                var frames = Frames($"{Root}/Terrain/Decorations/Bushes/Bush {variant}.png");
                var holder = Prop(bushesGroup, $"Bush{variant}_{cell.x}_{cell.y}", FeetOf(cell, 0.1f), frames, BushFeet[variant - 1], 0);
                Animate(holder.GetChild(0).gameObject, frames, rng);
            }

            var rocksGroup = Group(root.transform, "Rocks");
            foreach (var cell in IslandLayout.Rocks)
            {
                int variant = 1 + rng.Next(4);
                var sprite = One($"{Root}/Terrain/Decorations/Rocks/Rock{variant}.png");
                Prop(rocksGroup, $"Rock{variant}_{cell.x}_{cell.y}", FeetOf(cell, 0.1f), new[] { sprite }, RockFeet[variant - 1], 0);
            }

            var goldGroup = Group(root.transform, "GoldStones");
            foreach (var cell in IslandLayout.GoldStones)
            {
                int variant = Mathf.Clamp(cell.z, 1, 6);
                var sprite = One($"{Root}/Pawn and Resources/Gold/Gold Stones/Gold Stone {variant}.png");
                Prop(goldGroup, $"GoldStone{variant}_{cell.x}_{cell.y}", FeetOf(new Vector2Int(cell.x, cell.y), 0.1f),
                    new[] { sprite }, GoldFeet[variant - 1], 0);
            }

            var waterGroup = Group(root.transform, "WaterDecor");
            foreach (var cell in IslandLayout.WaterRocks)
            {
                int variant = 1 + rng.Next(4);
                var frames = Frames($"{Root}/Terrain/Decorations/Rocks in the Water/Water Rocks_0{variant}.png");
                var go = new GameObject($"WaterRock{variant}_{cell.x}_{cell.y}");
                go.transform.SetParent(waterGroup, false);
                go.transform.position = new Vector3(cell.x + 0.5f, IslandLayout.Height - cell.y - 0.5f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = OrderWaterDecor;
                Animate(go, frames, rng);
            }
            {
                var duckFrames = Frames($"{Root}/Terrain/Decorations/Rubber Duck/Rubber duck.png");
                var go = new GameObject("RubberDuck");
                go.transform.SetParent(waterGroup, false);
                go.transform.position = new Vector3(IslandLayout.RubberDuck.x + 0.5f, IslandLayout.Height - IslandLayout.RubberDuck.y - 0.5f, 0f);
                go.transform.localScale = Vector3.one * 1.3f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = duckFrames[0];
                sr.sortingOrder = OrderWaterDecor + 1;
                Animate(go, duckFrames, rng);
            }

            BakeClouds(root.transform, rng);

            var info = root.AddComponent<IslandBakeInfo>();
            info.inputHash = inputHash;
            info.contentHash = ContentHash(root);
            PrefabUtility.SaveAsPrefabAsset(root, NaturePath);
            Debug.Log($"[IslandBake] IslandNature.prefab yazıldı: ağaç {IslandLayout.Trees.Length}, çalı {IslandLayout.Bushes.Length}, " +
                $"kaya {IslandLayout.Rocks.Length}, su kayası {IslandLayout.WaterRocks.Length}, altın {IslandLayout.GoldStones.Length}.");
            return true;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    /// <summary>Hücrenin alt-orta noktasına yakın ayak konumu (dünya).</summary>
    private static Vector2 FeetOf(Vector2Int cell, float lift) =>
        new Vector2(cell.x + 0.5f, IslandLayout.Height - cell.y - 1f + lift);

    /// <summary>Ayakta duran nesne: kök ayakta, sprite çocukta +ayak/64 yukarıda; sıralama ayak y'sinden (statik).</summary>
    private static Transform Prop(Transform parent, string name, Vector2 feet, Sprite[] frames, float feetPixels, int bias)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.position = feet;
        var visual = new GameObject("Sprite");
        visual.transform.SetParent(holder, false);
        visual.transform.localPosition = new Vector3(0f, feetPixels / 64f, 0f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = IslandYSort.OrderFor(feet.y, bias);
        return holder;
    }

    private static void Animate(GameObject go, Sprite[] frames, System.Random rng)
    {
        var animator = go.AddComponent<IslandLoopAnimator>();
        animator.frames = frames;
        animator.fps = 10f;
        animator.phase = 0.2f + (float)rng.NextDouble() * 5f;
    }

    // ==================== BULUTLAR ====================

    private static void BakeClouds(Transform root, System.Random rng)
    {
        var sprites = Enumerable.Range(1, 8).Select(i => One($"{Root}/Terrain/Decorations/Clouds/Clouds_{i:00}.png")).ToArray();

        // Kenar bulutları: ekran kenarlarına sabit (kamera ile birlikte), yavaş süzülür.
        var edge = Group(root, "EdgeClouds");
        // Köşelerde ve kenarlarda kümelenmiş iri bulutlar (referanstaki kabarık bulutlar) + 3 yavaş sürüklenen
        var anchors = new[]
        {
            new Vector3(0.00f, 0.97f, 2.0f), new Vector3(0.14f, 1.00f, 1.6f), new Vector3(1.00f, 0.94f, 1.9f),
            new Vector3(-0.07f, 0.70f, 1.7f), new Vector3(1.08f, 0.58f, 1.8f), new Vector3(-0.07f, 0.40f, 1.6f),
            new Vector3(1.08f, 0.30f, 1.7f), new Vector3(0.00f, 0.08f, 2.1f), new Vector3(0.16f, 0.00f, 1.7f),
            new Vector3(1.00f, 0.04f, 2.2f), new Vector3(0.86f, -0.02f, 1.6f),
            // Sürüklenenler de kenarda: ekrana sabit oldukları için ortada durursa kaleyi/araziyi örterler.
            new Vector3(-0.07f, 0.55f, 1.6f), new Vector3(1.08f, 0.78f, 1.6f), new Vector3(0.55f, -0.03f, 1.6f),
        };
        for (int i = 0; i < anchors.Length; i++)
        {
            var go = new GameObject("EdgeCloud_" + i);
            go.transform.SetParent(edge, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[(i * 3) % sprites.Length];
            sr.sortingOrder = OrderEdgeClouds + i;
            var cloud = go.AddComponent<IslandCloud>();
            cloud.viewport = new Vector2(anchors[i].x, anchors[i].y);
            cloud.baseScale = anchors[i].z;
            cloud.phase = (float)rng.NextDouble() * 6f;
            cloud.baseAlpha = 1f;
            if (i >= anchors.Length - 3) { cloud.swayX = 0.07f; cloud.swayY = 0.012f; cloud.speed = 0.06f; }   // yavaş süzülenler
        }

        // Kilitli bölge bulutları: bölgeyi örter; açılınca kaybolur.
        var zones = Group(root, "ZoneClouds");
        for (int zone = 1; zone < IslandLayout.Zones.Length; zone++)
        {
            var group = Group(zones, "Zone" + zone);
            int count = 0;
            for (float y = 0f; y < IslandLayout.Height; y += 2.2f)
            for (float x = -1f; x < IslandLayout.Width + 1; x += 3.2f)
            {
                int col = Mathf.Clamp(Mathf.FloorToInt(x + 1.6f), 0, IslandLayout.Width - 1);
                int row = Mathf.Clamp(Mathf.FloorToInt(IslandLayout.Height - y - 1.1f), 0, IslandLayout.Height - 1);
                if (IslandLayout.ZoneAt(col, row) != zone || !NearLand(col, row, 2)) continue;
                if (NearOpenBuilding(new Vector2(x + 1.6f, y + 1.1f), 4.3f)) continue;   // hazır köy binasını örtmesin
                var go = new GameObject($"ZoneCloud_{zone}_{count++}");
                go.transform.SetParent(group, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[rng.Next(sprites.Length)];
                sr.sortingOrder = OrderZoneClouds + count;
                float scale = 1.25f + 0.5f * (float)rng.NextDouble();
                go.transform.localScale = Vector3.one * scale;
                var cloud = go.AddComponent<IslandZoneCloud>();
                cloud.zone = zone;
                cloud.anchor = new Vector2(x + 1.6f + (float)(rng.NextDouble() - 0.5) * 1.0f, y + 1.1f + (float)(rng.NextDouble() - 0.5) * 0.7f);
                cloud.phase = (float)rng.NextDouble() * 6f;
                go.transform.position = cloud.anchor;
            }
        }
    }

    private static bool NearOpenBuilding(Vector2 world, float radius)
    {
        foreach (var spot in IslandLayout.Buildings)
        {
            var center = IslandLayout.World(spot.col + spot.w * 0.5f, spot.row + spot.h * 0.5f);
            if (IslandLayout.ZoneAt(spot.col, spot.row) == 0 && Vector2.Distance(world, center) < radius) return true;
        }
        return false;
    }

    private static bool NearLand(int col, int row, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
            if (IslandLayout.IsLand(col + dx, row + dy)) return true;
        return false;
    }
}
