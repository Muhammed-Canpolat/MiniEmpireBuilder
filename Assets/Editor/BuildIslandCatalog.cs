using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Martı Adası görsel kataloğunu üretir: Tiny Swords dilimlerini <c>Resources/IslandVisualCatalog.asset</c> içine koyar
/// (Tiny Swords Resources altında olmadığı için çalışma anında doğrudan yüklenemez). Kalıp: BuildBaseVisualCatalog.
/// Menü: Tools/Island/Build Island Catalog. Editörde bir kez çalıştırılır; katalog yokken ada yedek görsellerle çalışır.
/// </summary>
public static class BuildIslandCatalog
{
    private const string Root = "Assets/Tiny Swords";
    private const string Output = "Assets/Resources/IslandVisualCatalog.asset";

    /// <summary>Batchmode girişi: <c>-executeMethod BuildIslandCatalog.BuildFromCommandLine</c>.</summary>
    public static void BuildFromCommandLine() => Build();

    private const string Kenney = "Assets/kenney_piratePack/PNG/Retina/Ships";

    [MenuItem("Tools/Island/Build Island Catalog")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<IslandVisualCatalog>(Output);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<IslandVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, Output);
        }
        var missing = new List<string>();

        // Zemin: Tilemap_color1 (9 × 6 ızgara; sprite dikdörtgeninden sütun/satır bulunur).
        const string tilesetPath = Root + "/Terrain/Tileset/Tilemap_color1.png";
        catalog.tileset = new Sprite[IslandVisualCatalog.TilesetColumns * IslandVisualCatalog.TilesetRows];
        var tilesetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tilesetPath);
        foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(tilesetPath).OfType<Sprite>())
        {
            int column = Mathf.FloorToInt(sprite.rect.center.x / 64f);
            int rowFromTop = Mathf.FloorToInt((tilesetTexture.height - sprite.rect.center.y) / 64f);
            if (column < 0 || column >= IslandVisualCatalog.TilesetColumns || rowFromTop < 0 ||
                rowFromTop >= IslandVisualCatalog.TilesetRows) continue;
            catalog.tileset[rowFromTop * IslandVisualCatalog.TilesetColumns + column] = sprite;
        }
        if (catalog.tileset[1 * IslandVisualCatalog.TilesetColumns + 1] == null) missing.Add(tilesetPath);

        catalog.foam = Frames($"{Root}/Terrain/Tileset/Water Foam.png", missing);
        catalog.clouds = Enumerable.Range(1, 8)
            .Select(i => First($"{Root}/Terrain/Decorations/Clouds/Clouds_{i:00}.png", missing)).Where(s => s != null).ToArray();
        catalog.splash = Frames($"{Root}/Particle FX/Water Splash.png", missing);

        string blueArcher = $"{Root}/Units/Blue Units/Archer";
        catalog.archerIdle = Frames($"{blueArcher}/Archer_Idle.png", missing);
        catalog.archerRun = Frames($"{blueArcher}/Archer_Run.png", missing);
        catalog.archerShoot = Frames($"{blueArcher}/Archer_Shoot.png", missing);

        string redPawn = $"{Root}/Pawn and Resources/Pawn/Red Pawn";
        catalog.deckhandIdle = Frames($"{redPawn}/Pawn_Idle Knife.png", missing);
        catalog.deckhandRun = Frames($"{redPawn}/Pawn_Run Knife.png", missing);
        catalog.deckhandAttack = Frames($"{redPawn}/Pawn_Interact Knife.png", missing);

        string redWarrior = $"{Root}/Units/Red Units/Warrior";
        catalog.raiderIdle = Frames($"{redWarrior}/Warrior_Idle.png", missing);
        catalog.raiderRun = Frames($"{redWarrior}/Warrior_Run.png", missing);
        catalog.raiderAttack = Frames($"{redWarrior}/Warrior_Attack1.png", missing);

        string redArcher = $"{Root}/Units/Red Units/Archer";
        catalog.shoreArcherIdle = Frames($"{redArcher}/Archer_Idle.png", missing);
        catalog.shoreArcherRun = Frames($"{redArcher}/Archer_Run.png", missing);
        catalog.shoreArcherShoot = Frames($"{redArcher}/Archer_Shoot.png", missing);

        catalog.castle = First($"{Root}/Buildings/Blue Buildings/Castle.png", missing);
        catalog.tower = First($"{Root}/Buildings/Blue Buildings/Tower.png", missing);
        catalog.meatIcon = First($"{Root}/Pawn and Resources/Meat/Meat Resource/Meat Resource.png", missing);
        catalog.woodIcon = First($"{Root}/Pawn and Resources/Wood/Wood Resource/Wood Resource.png", missing);
        catalog.sheepIdle = Frames($"{Root}/Pawn and Resources/Meat/Sheep/Sheep_Idle.png", missing);

        BuildNamed(catalog, missing);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        IslandVisualCatalog.ResetForTests();
        Debug.Log(missing.Count == 0
            ? "[IslandCatalog] Katalog üretildi: " + Output
            : "[IslandCatalog] Katalog üretildi; eksik varlıklar (yedek görsel kullanılır): " + string.Join(", ", missing));
    }

    // ---- v2: adlandırılmış kümeler (piyonlar, binalar, Lancer, koyun, Kenney tekneleri, arayüz) ----
    private static void BuildNamed(IslandVisualCatalog catalog, List<string> missing)
    {
        var list = new List<NamedFrames>();
        void Add(string name, Sprite[] frames) { if (frames != null && frames.Length > 0) list.Add(new NamedFrames { name = name, frames = frames }); }

        string pawn = $"{Root}/Pawn and Resources/Pawn/Blue Pawn";
        Add("pawn_idle", Frames($"{pawn}/Pawn_Idle.png", missing));
        Add("pawn_run", Frames($"{pawn}/Pawn_Run.png", missing));
        foreach (var tool in new[] { "Axe", "Wood", "Knife", "Meat", "Pickaxe", "Gold", "Hammer" })
        {
            Add("pawn_idle_" + tool.ToLowerInvariant(), Frames($"{pawn}/Pawn_Idle {tool}.png", missing));
            Add("pawn_run_" + tool.ToLowerInvariant(), Frames($"{pawn}/Pawn_Run {tool}.png", missing));
        }
        foreach (var tool in new[] { "Axe", "Hammer", "Knife", "Pickaxe" })
            Add("pawn_interact_" + tool.ToLowerInvariant(), Frames($"{pawn}/Pawn_Interact {tool}.png", missing));

        foreach (var building in new[] { "Castle", "House1", "House2", "House3", "Tower", "Archery", "Barracks", "Monastery" })
            Add("building_" + building.ToLowerInvariant(), new[] { First($"{Root}/Buildings/Blue Buildings/{building}.png", missing) });

        string lancer = $"{Root}/Units/Red Units/Lancer";
        Add("lancer_idle", Frames($"{lancer}/Lancer_Idle.png", missing));
        Add("lancer_run", Frames($"{lancer}/Lancer_Run.png", missing));
        Add("lancer_attack", Frames($"{lancer}/Lancer_Right_Attack.png", missing));
        Add("lancer_defence", Frames($"{lancer}/Lancer_Right_Defence.png", missing));
        // Faz E1: Kışla'nın mavi Lancer'ı, Manastır'ın Monk'u + şifa efekti, Kışla toplanma bayrağı (Kenney mavi yelken)
        string blueLancer = $"{Root}/Units/Blue Units/Lancer";
        Add("blue_lancer_idle", Frames($"{blueLancer}/Lancer_Idle.png", missing));
        Add("blue_lancer_run", Frames($"{blueLancer}/Lancer_Run.png", missing));
        Add("blue_lancer_attack", Frames($"{blueLancer}/Lancer_Right_Attack.png", missing));
        string monk = $"{Root}/Units/Blue Units/Monk";
        Add("monk_idle", Frames($"{monk}/Idle.png", missing));
        Add("monk_heal", Frames($"{monk}/Heal.png", missing));
        Add("heal_fx", Frames($"{Root}/Units/Extra/Heal Effect/Heal_Effect.png", missing));
        const string cainosProps = "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";
        Add("rally_pole", new[] { NamedSprite(cainosProps, "TX Village Props - Banner Pillar", missing) });
        Add("rally_banner", new[] { NamedSprite(cainosProps, "TX Village Props - Banner", missing) });
        Add("unit_shadow", new[] { First($"{Root}/Terrain/Tileset/Shadow.png", missing) });

        string sheep = $"{Root}/Pawn and Resources/Meat/Sheep";
        Add("sheep_idle", Frames($"{sheep}/Sheep_Idle.png", missing));
        Add("sheep_move", Frames($"{sheep}/Sheep_Move.png", missing));
        Add("sheep_grass", Frames($"{sheep}/Sheep_Grass.png", missing));

        Add("gold_icon", new[] { First($"{Root}/Pawn and Resources/Gold/Gold Resource/Gold_Resource.png", missing) });
        Add("cursor_brackets", new[] { First($"{Root}/UI Elements/Cursors/Cursor_04.png", missing) });
        Add("arrow", new[] { First($"{Root}/Units/Extra/Arrow/Arrow.png", missing) });

        // Kenney Pirate Pack gemileri (CC0): 64 PPU, Bilinear (vektör stil), sıkıştırmasız, mipmap yok.
        for (int i = 1; i <= 3; i++) Add("dinghy" + i, new[] { BoatSprite($"{Kenney}/dinghyLarge{i}.png", missing) });
        for (int i = 1; i <= 24; i++) Add("ship_" + i, new[] { BoatSprite($"{Kenney}/ship ({i}).png", missing) });
        // Faz C: efektler (Tiny Swords Dust/Explosion/Fire), arayüz simgeleri ve kâğıt/kurdele
        Add("dust_1", Frames($"{Root}/Particle FX/Dust_01.png", missing));
        Add("dust_2", Frames($"{Root}/Particle FX/Dust_02.png", missing));
        Add("explosion_1", Frames($"{Root}/Particle FX/Explosion_01.png", missing));
        Add("explosion_2", Frames($"{Root}/Particle FX/Explosion_02.png", missing));
        for (int i = 1; i <= 3; i++) Add("fire_" + i, Frames($"{Root}/Particle FX/Fire_0{i}.png", missing));
        Add("icon_hammer", new[] { First($"{Root}/UI Elements/Icons/Icon_01.png", missing) });
        Add("icon_sword", new[] { First($"{Root}/UI Elements/Icons/Icon_05.png", missing) });
        Add("ui_paper", new[] { First($"{Root}/UI Elements/Papers/RegularPaper.png", missing) });
        Add("ui_ribbon", new[] { First($"{Root}/UI Elements/Ribbons/BigRibbons 1.png", missing) });
        Add("ui_paper_special", new[] { First($"{Root}/UI Elements/Papers/SpecialPaper.png", missing) });
        Add("ui_button_blue", new[] { First($"{Root}/UI Elements/Buttons/BigBlueButton_Regular.png", missing) });
        Add("ui_round_red", new[] { First($"{Root}/UI Elements/Buttons/SmallRedRoundButton_Regular.png", missing) });
        Add("ui_round_blue", new[] { First($"{Root}/UI Elements/Buttons/SmallBlueRoundButton_Regular.png", missing) });
        Add("ui_square_blue", new[] { First($"{Root}/UI Elements/Buttons/SmallBlueSquareButton_Regular.png", missing) });
        Add("ui_button_red", new[] { First($"{Root}/UI Elements/Buttons/BigRedButton_Regular.png", missing) });
        // Kenney Pirate Pack efektleri (CC0): batan teknede patlama, hasarlı teknede ateş, top mermisi
        const string kenneyFx = "Assets/kenney_piratePack/PNG/Retina/Effects";
        for (int i = 1; i <= 3; i++) Add("kenney_explosion" + i, new[] { BoatSprite($"{kenneyFx}/explosion{i}.png", missing) });
        for (int i = 1; i <= 2; i++) Add("kenney_fire" + i, new[] { BoatSprite($"{kenneyFx}/fire{i}.png", missing) });
        Add("kenney_cannonball", new[] { BoatSprite("Assets/kenney_piratePack/PNG/Retina/Ship parts/cannonBall.png", missing) });
        catalog.named = list;
        BuildMonsters(catalog, missing);
    }

    // ---- Faz C: DungeonMonsters2D prefabları (PSB + 2D Animation, PPU 100). Anahtar = EnemyType adı ----
    private const string Dm2d = "Assets/DungeonMonsters2D/Characters";

    private static void BuildMonsters(IslandVisualCatalog catalog, List<string> missing)
    {
        // (anahtar, prefab, hedef gövde yüksekliği u). Kızıl Warrior gövdesi ≈ 1,0 u; canavarlar ≤1,6× (boss ×BossScale ayrıca).
        var specs = new (string key, string prefab, float height)[]
        {
            ("Rat", "Rat", 0.5f), ("Bat", "Bat", 0.75f), ("WoodGolem", "WoodGolem", 1.5f), ("ButcherCaptain", "Butcher", 1.3f),
        };
        var old = catalog.monsters ?? new List<MonsterEntry>();
        var list = new List<MonsterEntry>();
        foreach (var spec in specs)
        {
            string path = $"{Dm2d}/{spec.prefab}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { missing.Add(path); continue; }
            var previous = old.FirstOrDefault(e => e != null && e.key == spec.key);
            var entry = new MonsterEntry
            {
                key = spec.key, prefab = prefab, targetHeight = spec.height,
                facesRight = false,   // DM2D prefabları varsayılan olarak SOLA bakar (ekran görüntüsüyle doğrulandı)
            };
            Measure(prefab, out entry.bodyHeight, out entry.feetOffsetY, out entry.centerOffsetX);
            entry.portrait = Portrait(prefab);
            list.Add(entry);
            Debug.Log($"[IslandCatalog] DM2D {spec.key}: gövde {entry.bodyHeight:0.00} u (prefab ölçeğinde), ayak ofseti {entry.feetOffsetY:0.00}, merkez x {entry.centerOffsetX:0.00}");
        }
        catalog.monsters = list;
    }

    /// <summary>Prefabın etkin SpriteRenderer'larının birleşik gövde yüksekliği ve en alt noktası (kök yerel uzayı, prefab ölçeğinde).</summary>
    private static void Measure(GameObject prefab, out float height, out float feetOffset, out float centerX)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        Bounds bounds = default;
        bool any = false;
        foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (!renderer.enabled || renderer.sprite == null || renderer.name.Contains("Effect")) continue;
            if (!any) { bounds = renderer.bounds; any = true; } else bounds.Encapsulate(renderer.bounds);
        }
        height = any ? Mathf.Max(0.05f, bounds.size.y) : 1f;
        feetOffset = any ? bounds.min.y : 0f;
        centerX = any ? bounds.center.x : 0f;
        UnityEngine.Object.DestroyImmediate(instance);
    }

    /// <summary>Pano portresi: "Head" parçası, yoksa en büyük parça sprite'ı.</summary>
    private static Sprite Portrait(GameObject prefab)
    {
        Sprite best = null; float area = 0f;
        foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || renderer.name.Contains("Effect")) continue;
            if (renderer.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0) return renderer.sprite;
            float a = renderer.sprite.rect.width * renderer.sprite.rect.height;
            if (a > area) { area = a; best = renderer.sprite; }
        }
        return best;
    }

    private static Sprite BoatSprite(string path, List<string> missing)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { missing.Add(path); return null; }
        bool dirty = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 64f) || importer.filterMode != FilterMode.Point
            || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
            || !importer.alphaIsTransparency;
        if (dirty)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;   // piksel sanatın yanında net (Retina en büyük Kenney boyu; 2–2,5× büyütülür)
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return First(path, missing);
    }

    private static Sprite First(string path, List<string> missing)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(Order).FirstOrDefault();
        if (sprite == null) missing.Add(path);
        return sprite;
    }

    private static Sprite[] Frames(string path, List<string> missing)
    {
        var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(Order).ToArray();
        if (frames.Length == 0) missing.Add(path);
        return frames;
    }

    /// <summary>Çok-dilimli bir dokudan adıyla tek sprite (Cainos Village Props).</summary>
    private static Sprite NamedSprite(string path, string spriteName, List<string> missing)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s => s.name == spriteName);
        if (sprite == null) missing.Add(path + "#" + spriteName);
        return sprite;
    }

    /// <summary>"Ad_12" gibi son sayıya göre doğal sıra (Ad_2 &lt; Ad_10).</summary>
    private static int Order(Sprite sprite)
    {
        var match = Regex.Match(sprite.name, @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
