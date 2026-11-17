using UnityEngine;

/// <summary>
/// MiniEmpireUI paketi Resources dışında olduğu için sprite'ları burada toplanır
/// (editörde "Tools/MiniEmpire UI/Build Catalog" -> Resources/MiniEmpireUiCatalog.asset; IslandVisualCatalog deseni).
/// Katalog yoksa (<see cref="Instance"/> null) Ana Üs eski koyu hap barıyla çalışır.
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire/MiniEmpireUI Catalog")]
public class MiniEmpireUiCatalog : ScriptableObject
{
    [Header("Üst bar")]
    public Sprite topbarBg, resPill, levelShield, bannerLeft, bannerRight, btnSettings;
    [Header("Simgeler")]
    public Sprite iconGold, iconWood, iconMeat, iconGem, iconBuilder;

    [Header("Alt bar")]
    public Sprite bottombarBg, menuFrame, labelPlate, separatorBanner, arrowDown, arrowUp;
    public Sprite iconBuild, iconExpedition, iconHero, iconIsland;

    [Header("Ana Üs duvarları (PixelCastle, D108)")]
    public Sprite wallCrenellated, wallVertical, cornerOuterLeft, cornerOuterRight, towerSmallClosed, gatehouseDoors;

    private static MiniEmpireUiCatalog instance;
    private static bool loaded;

    public static MiniEmpireUiCatalog Instance
    {
        get
        {
            if (!loaded) { instance = Resources.Load<MiniEmpireUiCatalog>("MiniEmpireUiCatalog"); loaded = true; }
            return instance;
        }
    }

    /// <summary>Üst bar görünümü açık ve gerekli sprite'lar var mı.</summary>
    public static bool TopBarReady
    {
        get
        {
            var c = Instance;
            return FeatureConfig.Instance.miniEmpireTopBar && c != null && c.topbarBg != null && c.resPill != null
                && c.levelShield != null && c.btnSettings != null;
        }
    }

    /// <summary>Alt bar görünümü (D107) açık ve gerekli sprite'lar var mı.</summary>
    public static bool BottomBarReady
    {
        get
        {
            var c = Instance;
            return FeatureConfig.Instance.miniEmpireBottomBar && c != null && c.bottombarBg != null && c.menuFrame != null
                && c.labelPlate != null && c.separatorBanner != null && c.arrowDown != null && c.arrowUp != null
                && c.iconBuild != null && c.iconExpedition != null && c.iconHero != null && c.iconIsland != null;
        }
    }

    /// <summary>Ana Üs PixelCastle duvarları (D108) için sprite'lar var mı (sancak: <see cref="bannerLeft"/>).</summary>
    public static bool WallsReady
    {
        get
        {
            var c = Instance;
            return c != null && c.wallCrenellated != null && c.wallVertical != null && c.cornerOuterLeft != null
                && c.cornerOuterRight != null && c.towerSmallClosed != null && c.gatehouseDoors != null && c.bannerLeft != null;
        }
    }

    public static void ResetForTests() { instance = null; loaded = false; }
}
