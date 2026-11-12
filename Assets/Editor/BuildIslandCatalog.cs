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

        string sheep = $"{Root}/Pawn and Resources/Meat/Sheep";
        Add("sheep_idle", Frames($"{sheep}/Sheep_Idle.png", missing));
        Add("sheep_move", Frames($"{sheep}/Sheep_Move.png", missing));
        Add("sheep_grass", Frames($"{sheep}/Sheep_Grass.png", missing));

        Add("cursor_brackets", new[] { First($"{Root}/UI Elements/Cursors/Cursor_04.png", missing) });
        Add("arrow", new[] { First($"{Root}/Units/Extra/Arrow/Arrow.png", missing) });

        // Kenney Pirate Pack gemileri (CC0): 64 PPU, Bilinear (vektör stil), sıkıştırmasız, mipmap yok.
        for (int i = 1; i <= 3; i++) Add("dinghy" + i, new[] { BoatSprite($"{Kenney}/dinghyLarge{i}.png", missing) });
        for (int i = 1; i <= 24; i++) Add("ship_" + i, new[] { BoatSprite($"{Kenney}/ship ({i}).png", missing) });
        catalog.named = list;
    }

    private static Sprite BoatSprite(string path, List<string> missing)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { missing.Add(path); return null; }
        bool dirty = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 64f) || importer.filterMode != FilterMode.Bilinear
            || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
            || !importer.alphaIsTransparency;
        if (dirty)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Bilinear;
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

    /// <summary>"Ad_12" gibi son sayıya göre doğal sıra (Ad_2 &lt; Ad_10).</summary>
    private static int Order(Sprite sprite)
    {
        var match = Regex.Match(sprite.name, @"(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
