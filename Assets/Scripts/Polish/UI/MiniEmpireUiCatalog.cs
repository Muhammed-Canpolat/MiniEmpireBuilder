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

    public static void ResetForTests() { instance = null; loaded = false; }
}
