using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 7. oturum (D88): bölge savaş haritası + seviye yerleşimi varlıklarını üretir (Resources/BattleMaps).
/// Var olan varlığa dokunmaz (elle yapılan düzenlemeler korunur); "Yeniden Üret (üzerine yaz)" hepsini baştan kurar.
/// Komut satırı: -executeMethod BattleMapAuthoring.GenerateFromCommandLine [-force]
/// </summary>
public static class BattleMapAuthoring
{
    private const string Root = "Assets/Resources/BattleMaps";
    private const string TS = "Assets/Tiny Swords";

    [MenuItem("Tools/Mini Empire Builder/Savaş Haritaları/Eksikleri Üret")]
    public static void GenerateMissing() => Generate(false);

    [MenuItem("Tools/Mini Empire Builder/Savaş Haritaları/Yeniden Üret (üzerine yaz)")]
    public static void GenerateForce()
    {
        if (EditorUtility.DisplayDialog("Savaş haritaları", "Tüm bölge/seviye varlıkları baştan üretilecek; elle yapılan rota/yuva düzenlemeleri kaybolur.", "Üret", "Vazgeç"))
            Generate(true);
    }

    [MenuItem("Tools/Mini Empire Builder/Savaş Haritaları/Doğrula")]
    public static void ValidateMenu()
    {
        var errors = ValidateAll();
        if (errors.Count == 0) Debug.Log("[BattleMaps] 30 seviye geçerli.");
        foreach (var e in errors) Debug.LogError("[BattleMaps] " + e);
    }

    public static void GenerateFromCommandLine()
    {
        bool force = System.Environment.GetCommandLineArgs().Contains("-force");
        Generate(force);
        var errors = ValidateAll();
        foreach (var e in errors) Debug.LogError("[BattleMaps] " + e);
        Debug.Log($"[BattleMaps] üretim bitti, doğrulama hatası: {errors.Count}");
        EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
    }

    /// <summary>
    /// 8. oturum (D99): yalnız YUVALARI geçersiz olan seviyelerin yuvalarını yeni kurala (hak etiketi de yola binmez) göre yeniden üretir;
    /// rota, maden, kahraman ve Ana Üs aynen kalır. -executeMethod BattleMapAuthoring.RegenerateInvalidSlotsFromCommandLine
    /// </summary>
    public static void RegenerateInvalidSlotsFromCommandLine()
    {
        int changed = 0;
        for (int region = 1; region <= 6; region++)
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleRegionMap>($"{Root}/Region_{region}.asset");
            for (int level = 1; level <= 5; level++)
            {
                var layout = AssetDatabase.LoadAssetAtPath<BattleLevelLayout>($"{Root}/Levels/R{region}_L{level}.asset");
                if (map == null || layout == null) continue;
                bool slotProblem = BattleLayoutGenerator.Validate(map, layout).Any(e => e.Contains(" yuva ") || e.Contains("yuva sayısı"));
                if (!slotProblem) continue;
                var before = string.Join(" ", layout.slots ?? new Vector2[0]);
                layout.slots = new Vector2[0];
                layout.slots = BattleLayoutGenerator.GenerateSlots(map, layout, BattleLayoutGenerator.SlotCountFor(region, level, layout.Routes.Length));
                EditorUtility.SetDirty(layout);
                changed++;
                Debug.Log($"[BattleMaps] B{region}-S{level} yuvalar: {before} → {string.Join(" ", layout.slots)}");
            }
        }
        AssetDatabase.SaveAssets();
        var errors = ValidateAll();
        foreach (var e in errors) Debug.LogError("[BattleMaps] " + e);
        Debug.Log($"[BattleMaps] yuvası yenilenen seviye {changed}, doğrulama hatası {errors.Count}");
        EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
    }

    public static List<string> ValidateAll()
    {
        var errors = new List<string>();
        for (int region = 1; region <= 6; region++)
        {
            var map = AssetDatabase.LoadAssetAtPath<BattleRegionMap>($"{Root}/Region_{region}.asset");
            for (int level = 1; level <= 5; level++)
            {
                var layout = AssetDatabase.LoadAssetAtPath<BattleLevelLayout>($"{Root}/Levels/R{region}_L{level}.asset");
                if (map == null || layout == null) { errors.Add($"B{region}-S{level}: varlık yok"); continue; }
                errors.AddRange(BattleLayoutGenerator.Validate(map, layout));
            }
        }
        return errors;
    }

    public static void Generate(bool force)
    {
        Directory.CreateDirectory(Root + "/Levels");
        AssetDatabase.Refresh();
        for (int region = 1; region <= 6; region++)
        {
            string regionPath = $"{Root}/Region_{region}.asset";
            var map = AssetDatabase.LoadAssetAtPath<BattleRegionMap>(regionPath);
            var templates = new List<List<Vector2[]>>();
            for (int level = 1; level <= 5; level++) templates.Add(BattleLayoutGenerator.RouteTemplate(region, level));
            if (map == null || force)
            {
                bool created = map == null;
                if (created) map = ScriptableObject.CreateInstance<BattleRegionMap>();
                FillRegion(map, region);
                var allRoutes = templates.SelectMany(t => t).Select(points => new BattleRoute(points));
                map.mask = BattleLayoutGenerator.GenerateMask(region, allRoutes);
                if (created) AssetDatabase.CreateAsset(map, regionPath);
                EditorUtility.SetDirty(map);
            }
            for (int level = 1; level <= 5; level++)
            {
                string levelPath = $"{Root}/Levels/R{region}_L{level}.asset";
                var layout = AssetDatabase.LoadAssetAtPath<BattleLevelLayout>(levelPath);
                if (layout != null && !force) continue;
                bool created = layout == null;
                if (created) layout = ScriptableObject.CreateInstance<BattleLevelLayout>();
                FillLevel(map, layout, region, level, templates[level - 1]);
                if (created) AssetDatabase.CreateAsset(layout, levelPath);
                EditorUtility.SetDirty(layout);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void FillLevel(BattleRegionMap map, BattleLevelLayout layout, int region, int level, List<Vector2[]> routes)
    {
        layout.regionId = region;
        layout.levelNumber = level;
        var regionData = AssetDatabase.LoadAssetAtPath<RegionData>($"Assets/Resources/Campaign/region_0{region}.asset");
        layout.kind = regionData != null && regionData.levels != null && regionData.levels.Length >= level && regionData.levels[level - 1] != null
            ? regionData.levels[level - 1].kind : (CampaignLevelKind)(level - 1);
        layout.routes = routes.Select(p => new BattleRouteData { points = p }).ToList();
        layout.basePosition = Vector2.zero;
        layout.slots = new Vector2[0];
        layout.heroSpawn = BattleLayoutGenerator.GenerateHeroSpawn(map, layout);
        if (layout.HasMine) layout.minePosition = BattleLayoutGenerator.GenerateMine(map, layout);
        layout.slots = BattleLayoutGenerator.GenerateSlots(map, layout, BattleLayoutGenerator.SlotCountFor(region, level, routes.Count));
    }

    // ==================== BÖLGE İÇERİĞİ ====================

    private static Sprite Sprite(string path, int frame = 0)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        if (sprites.Count == 0) { Debug.LogWarning("[BattleMaps] sprite yok: " + path); return null; }
        if (sprites.Count == 1) return sprites[0];
        return sprites.FirstOrDefault(s => s.name.EndsWith("_" + frame)) ?? sprites[0];
    }

    private static Sprite[] Sprites(params string[] paths) => paths.Select(p => Sprite(p)).Where(s => s != null).ToArray();
    private static string Tree(int k) => $"{TS}/Pawn and Resources/Wood/Trees/Tree{k}.png";
    private static string Bush(int k) => $"{TS}/Terrain/Decorations/Bushes/Bush {k}.png";
    private static string Rock(int k) => $"{TS}/Terrain/Decorations/Rocks/Rock{k}.png";
    private static string Stump(int k) => $"{TS}/Pawn and Resources/Wood/Trees/Stump {k}.png";
    private static string Gold(int k) => $"{TS}/Pawn and Resources/Gold/Gold Stones/Gold Stone {k}.png";
    private static string Building(string color, string name) => $"{TS}/Buildings/{color} Buildings/{name}.png";
    private static string Prop(string name) => $"{Root}/Props/{name}.png";

    private static BattleDecorEntry Decor(string name, Sprite[] sprites, int count, float hMin, float hMax, bool blocks = false,
        float footprint = 0.6f, float clearance = 0.5f, bool sway = false, bool edges = false, Color? tint = null, bool shadow = true) =>
        new BattleDecorEntry { name = name, sprites = sprites, count = count, height = new Vector2(hMin, hMax), blocksMovement = blocks,
            footprint = footprint, roadClearance = clearance, sway = sway, preferEdges = edges, tint = tint ?? Color.white, shadow = shadow };

    private static BattleLandmark Landmark(string name, Sprite sprite, float height, params Vector2[] candidates) =>
        new BattleLandmark { name = name, sprite = sprite, height = height, candidates = candidates, tint = Color.white, blocksMovement = true };

    /// <summary>
    /// Sefer Haritası'ndaki adayla aynı kimlik (generate.py TILESET_OF / region_flavor / LANDMARKS): karo seti, süsler ve bölge yapıları.
    /// Yapı adayları harita konumlarının dikey karşılığı (x = u·8,5; y = 26 − 24,25·v), sonra yedekler.
    /// </summary>
    public static void FillRegion(BattleRegionMap map, int region)
    {
        map.regionId = region;
        map.columns = BattleMapGeometry.Columns;
        map.rows = BattleMapGeometry.Rows;
        string tileset = region switch
        {
            1 => $"{TS}/Terrain/Tileset/Tilemap_color1.png",
            2 => $"{TS}/Terrain/Tileset/Tilemap_color3.png",
            3 => $"{TS}/Terrain/Tileset/Tilemap_color4.png",
            4 => "Assets/Art/BattleMaps/Tileset_snow.png",
            5 => "Assets/Art/BattleMaps/Tileset_dark.png",
            _ => "Assets/Art/BattleMaps/Tileset_ash.png",
        };
        map.tileset = AssetDatabase.LoadAssetAtPath<Texture2D>(tileset);
        map.waterColor = new Color32(71, 171, 169, 255);
        map.shallowColor = new Color32(104, 196, 190, 255);
        map.roadColor = new Color32(206, 168, 112, 255);
        map.roadEdgeColor = new Color32(120, 86, 52, 255);
        map.gloom = new Color(0f, 0f, 0f, 0f);
        var pines = Sprites(Tree(1), Tree(2));
        var autumn = Sprites(Tree(3), Tree(4));
        var bushes = Sprites(Bush(1), Bush(2), Bush(3), Bush(4));
        var rocks = Sprites(Rock(1), Rock(2), Rock(3), Rock(4));
        var stumps = Sprites(Stump(1), Stump(2), Stump(3), Stump(4));
        int side = BattleLayoutGenerator.Side(region);
        switch (region)
        {
            case 1:
                map.atmosphere = BattleAtmosphere.Ruins;
                map.decor = new[]
                {
                    Decor("Çam", pines, 14, 2.5f, 3.0f, true, 0.5f, 0.9f, true, true),
                    Decor("Çalı", bushes, 8, 0.7f, 0.95f),
                    Decor("Kaya", rocks, 6, 0.55f, 0.8f),
                    Decor("Yıkık duvar", Sprites(Prop("ruin_wall")), 3, 1.1f, 1.3f, true, 1.5f, 0.8f),
                };
                map.landmarks = new[]
                {
                    Landmark("Yıkık Kale", Sprite(Prop("ruin_castle")), 2.7f, new Vector2(side * 4.9f, 12.4f), new Vector2(side * 4.9f, 17.5f), new Vector2(-side * 4.9f, 19f)),
                    Landmark("Yıkık Kule", Sprite(Prop("ruin_tower")), 1.9f, new Vector2(-side * 5.6f, 8.6f), new Vector2(-side * 5.6f, 21f), new Vector2(side * 5.6f, 22f)),
                    Landmark("Kırmızı Ev", Sprite(Building("Red", "House1")), 2.0f, new Vector2(-side * 5.6f, 3.6f), new Vector2(-side * 5.4f, -2.4f), new Vector2(side * 5.4f, -2.4f)),
                };
                break;
            case 2:
                map.atmosphere = BattleAtmosphere.Fog;
                map.decor = new[]
                {
                    Decor("Çam", pines, 24, 2.5f, 3.1f, true, 0.5f, 0.7f, true, true),
                    Decor("Güz ağacı", autumn, 18, 2.0f, 2.5f, true, 0.5f, 0.7f, true, true),
                    Decor("Çalı", bushes, 10, 0.7f, 0.95f),
                };
                map.landmarks = new[]
                {
                    Landmark("Mor Ev", Sprite(Building("Purple", "House2")), 2.1f, new Vector2(side * 5.6f, 13.9f), new Vector2(side * 5.4f, 20f), new Vector2(-side * 5.4f, 6f)),
                };
                break;
            case 3:
                map.atmosphere = BattleAtmosphere.Mine;
                map.decor = new[]
                {
                    Decor("Altın kaya", Sprites(Gold(1), Gold(2), Gold(3), Gold(4), Gold(5), Gold(6)), 9, 0.9f, 1.25f, true, 0.8f, 0.8f),
                    Decor("Kaya", rocks, 12, 0.6f, 0.95f),
                    Decor("Kütük", stumps, 3, 0.7f, 0.9f),
                };
                map.landmarks = new[]
                {
                    Landmark("Maden girişi", Sprite(Prop("mine_entrance")), 2.6f, new Vector2(side * 5.2f, 18.5f), new Vector2(-side * 5.2f, 18.5f), new Vector2(side * 5.2f, 10f)),
                    Landmark("Sarı Ev", Sprite(Building("Yellow", "House3")), 2.1f, new Vector2(-side * 5.6f, 13.5f), new Vector2(-side * 5.6f, 7f), new Vector2(side * 5.6f, 7f)),
                    Landmark("Sarı Kule", Sprite(Building("Yellow", "Tower")), 2.4f, new Vector2(side * 5.8f, 4.0f), new Vector2(-side * 5.8f, -2.4f), new Vector2(side * 5.8f, -2.4f)),
                };
                break;
            case 4:
                map.atmosphere = BattleAtmosphere.Snow;
                var frost = new Color(0.86f, 0.93f, 1f, 1f);
                map.decor = new[]
                {
                    Decor("Buzlu kaya", Sprites(Prop("ice_rock")), 7, 0.9f, 1.2f, true, 0.9f, 0.8f),
                    Decor("Karlı ağaç", autumn, 9, 2.0f, 2.4f, true, 0.5f, 0.9f, true, true, frost),
                    Decor("Kaya", rocks, 7, 0.6f, 0.9f, false, 0.6f, 0.5f, false, false, frost),
                };
                map.landmarks = new[]
                {
                    Landmark("Mavi Kule", Sprite(Building("Blue", "Tower")), 2.4f, new Vector2(-side * 5.8f, 13.1f), new Vector2(-side * 5.8f, 19f), new Vector2(side * 5.8f, 19f)),
                    Landmark("Mavi Kışla", Sprite(Building("Blue", "Barracks")), 2.2f, new Vector2(side * 5.4f, 3.7f), new Vector2(side * 5.2f, -2.4f), new Vector2(-side * 5.2f, -2.4f)),
                };
                break;
            case 5:
                map.atmosphere = BattleAtmosphere.Dark;
                map.gloom = new Color(0.05f, 0.02f, 0.12f, 0.28f);
                var dim = new Color(0.62f, 0.6f, 0.72f, 1f);
                map.decor = new[]
                {
                    Decor("Kemik", Sprites(Prop("bones_1"), Prop("bones_2"), Prop("bones_3")), 12, 0.45f, 0.65f, false, 0.6f, 0.35f, false, false, null, false),
                    Decor("Kütük", stumps, 6, 0.7f, 0.9f, false, 0.6f, 0.5f, false, false, dim),
                    Decor("Kaya", rocks, 8, 0.6f, 0.9f, false, 0.6f, 0.5f, false, false, dim),
                };
                map.landmarks = new[]
                {
                    Landmark("Kara Kale", Sprite(Building("Black", "Castle")), 3.0f, new Vector2(side * 4.8f, 12.4f), new Vector2(side * 4.8f, 18f), new Vector2(-side * 4.8f, 18f)),
                    Landmark("Kara Kule", Sprite(Building("Black", "Tower")), 2.4f, new Vector2(-side * 5.9f, 13.1f), new Vector2(-side * 5.9f, 7f), new Vector2(side * 5.9f, 7f)),
                };
                break;
            default:
                map.atmosphere = BattleAtmosphere.Lava;
                map.decor = new[]
                {
                    Decor("Lav gölü", Sprites(Prop("lava_pool")), 5, 1.0f, 1.3f, true, 1.7f, 0.9f, false, false, null, false),
                    Decor("Kaya", rocks, 10, 0.6f, 0.95f, false, 0.6f, 0.5f, false, false, new Color(0.8f, 0.7f, 0.66f, 1f)),
                    Decor("Kütük", stumps, 4, 0.7f, 0.9f, false, 0.6f, 0.5f, false, false, new Color(0.55f, 0.45f, 0.42f, 1f)),
                };
                map.landmarks = new[]
                {
                    Landmark("Kızıl Kule", Sprite(Building("Red", "Tower")), 2.4f, new Vector2(side * 5.6f, 13.5f), new Vector2(side * 5.6f, 19f), new Vector2(-side * 5.6f, 19f)),
                    Landmark("Kara Kışla", Sprite(Building("Black", "Barracks")), 2.2f, new Vector2(-side * 5.6f, 4.0f), new Vector2(-side * 5.4f, -2.4f), new Vector2(side * 5.4f, -2.4f)),
                };
                break;
        }
    }
}
