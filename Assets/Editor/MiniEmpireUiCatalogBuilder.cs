using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Resources/MiniEmpireUiCatalog.asset'i paket sprite'larından üretir. Batchmode: -executeMethod MiniEmpireUiCatalogBuilder.Build</summary>
public static class MiniEmpireUiCatalogBuilder
{
    private const string Root = "Assets/MiniEmpireUI/MiniEmpireUI/Sprites/";
    private const string OutPath = "Assets/Resources/MiniEmpireUiCatalog.asset";

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
        if (created) { Directory.CreateDirectory(Path.GetDirectoryName(OutPath)); AssetDatabase.CreateAsset(catalog, OutPath); }
        else EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("MiniEmpire UI catalog built: " + OutPath);
    }

    private static Sprite Load(string rel)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + rel);
        if (sprite == null) Debug.LogWarning("MiniEmpire UI sprite missing: " + rel);
        return sprite;
    }
}
