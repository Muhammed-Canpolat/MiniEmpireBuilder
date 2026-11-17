using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>Resources/MiniEmpireUiCatalog.asset'i paket sprite'larından üretir. Batchmode: -executeMethod MiniEmpireUiCatalogBuilder.Build</summary>
public static class MiniEmpireUiCatalogBuilder
{
    private const string Root = "Assets/MiniEmpireUI/MiniEmpireUI/Sprites/";
    private const string OutPath = "Assets/Resources/MiniEmpireUiCatalog.asset";
    private const string CastleRoot = "Assets/PixelCastle/PixelCastle/Sprites/";
    private const string CastleAtlasPath = "Assets/PixelCastle/PixelCastleWalls.spriteatlasv2";
    private static readonly string[] CastleSprites =
        { "wall_crenellated", "wall_vertical", "corner_outer_left", "corner_outer_right", "tower_small_closed", "gatehouse_doors" };

    [MenuItem("Tools/MiniEmpire UI/Build Catalog")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MiniEmpireUiCatalog>(OutPath);
        bool created = catalog == null;
        if (created) catalog = ScriptableObject.CreateInstance<MiniEmpireUiCatalog>();
        catalog.topbarBg = Load("TopBar/topbar_bg.png");
        catalog.resPill = Load("TopBar/res_pill_bg.png");
        catalog.levelShield = Load("TopBar/level_shield_empty.png");
        catalog.bannerLeft = Load("TopBar/banner_left.png");
        catalog.bannerRight = Load("TopBar/banner_right.png");
        catalog.btnSettings = Load("TopBar/btn_settings.png");
        catalog.iconGold = Load("Icons/icon_gold.png");
        catalog.iconWood = Load("Icons/icon_wood.png");
        catalog.iconMeat = Load("Icons/icon_meat.png");
        catalog.iconGem = Load("Icons/icon_gem.png");
        catalog.iconBuilder = Load("Icons/icon_builder.png");
        catalog.bottombarBg = Load("BottomBar/bottombar_bg.png");
        catalog.menuFrame = Load("BottomBar/menu_button_frame_noplate.png");
        catalog.labelPlate = Load("BottomBar/menu_label_plate.png");
        catalog.separatorBanner = Load("BottomBar/separator_banner.png");
        catalog.arrowDown = Load("BottomBar/btn_arrow_down.png");
        catalog.arrowUp = Load("BottomBar/btn_arrow_up.png");
        catalog.iconBuild = Load("Icons/icon_build.png");
        catalog.iconExpedition = Load("Icons/icon_expedition.png");
        catalog.iconHero = Load("Icons/icon_hero.png");
        catalog.iconIsland = Load("Icons/icon_island.png");
        // D109: sağ üst droplist (satır kapsülü + ok).
        catalog.resCapsule = Load("TopBar/res_capsule_bg.png");
        catalog.iconChevronDown = Load("Icons/icon_chevron_down.png");
        // D108: Ana Üs duvarları (PixelCastle; Resources dışı → katalogdan).
        catalog.wallCrenellated = LoadCastle("wall_crenellated");
        catalog.wallVertical = LoadCastle("wall_vertical");
        catalog.cornerOuterLeft = LoadCastle("corner_outer_left");
        catalog.cornerOuterRight = LoadCastle("corner_outer_right");
        catalog.towerSmallClosed = LoadCastle("tower_small_closed");
        catalog.gatehouseDoors = LoadCastle("gatehouse_doors");
        BuildCastleAtlas();
        if (created) { Directory.CreateDirectory(Path.GetDirectoryName(OutPath)); AssetDatabase.CreateAsset(catalog, OutPath); }
        else EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("MiniEmpire UI catalog built: " + OutPath);
    }

    private static Sprite LoadCastle(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CastleRoot + name + ".png");
        if (sprite == null) Debug.LogWarning("PixelCastle sprite missing: " + name);
        return sprite;
    }

    /// <summary>D108: yalnız kullanılan 6 duvar sprite'ı tek atlasta (Point, sıkıştırmasız, mipmap yok, döndürme/tight kapalı).</summary>
    private static void BuildCastleAtlas()
    {
        var atlas = new SpriteAtlasAsset();
        var textures = new Object[CastleSprites.Length];
        for (int i = 0; i < CastleSprites.Length; i++)
            textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(CastleRoot + CastleSprites[i] + ".png");
        atlas.Add(textures);
        SpriteAtlasAsset.Save(atlas, CastleAtlasPath);
        AssetDatabase.ImportAsset(CastleAtlasPath, ImportAssetOptions.ForceUpdate);
        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(CastleAtlasPath);
        importer.packingSettings = new SpriteAtlasPackingSettings
        {
            blockOffset = 1, padding = 4, enableRotation = false, enableTightPacking = false, enableAlphaDilation = false
        };
        importer.textureSettings = new SpriteAtlasTextureSettings
        {
            readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Point, anisoLevel = 0
        };
        var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
        platform.maxTextureSize = 2048;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.format = TextureImporterFormat.Automatic;
        importer.SetPlatformSettings(platform);
        importer.SaveAndReimport();
        Debug.Log("PixelCastle walls atlas built: " + CastleAtlasPath);
    }

    private static Sprite Load(string rel)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + rel);
        if (sprite == null) Debug.LogWarning("MiniEmpire UI sprite missing: " + rel);
        return sprite;
    }
}
