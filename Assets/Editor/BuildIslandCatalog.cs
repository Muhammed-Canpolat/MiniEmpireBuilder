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

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        IslandVisualCatalog.ResetForTests();
        Debug.Log(missing.Count == 0
            ? "[IslandCatalog] Katalog üretildi: " + Output
            : "[IslandCatalog] Katalog üretildi; eksik varlıklar (yedek görsel kullanılır): " + string.Join(", ", missing));
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
