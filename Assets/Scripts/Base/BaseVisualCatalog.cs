using System;
using UnityEngine;

[Serializable]
public class BuildingLevelVisual
{
    public Color color = Color.white;
    public Sprite sprite;
    public RuntimeAnimatorController animatorOverride;
}

[CreateAssetMenu(menuName = "Mini Empire/Base Visual Catalog")]
public class BaseVisualCatalog : ScriptableObject
{
    [Serializable]
    public class BuildingSet
    {
        public BuildingType type;
        public BuildingLevelVisual[] levels = new BuildingLevelVisual[5];
    }

    public BuildingSet[] buildings;
    public RuntimeAnimatorController[] archerControllers;
    public RuntimeAnimatorController[] warriorControllers;
    public RuntimeAnimatorController[] monkControllers;
    public RuntimeAnimatorController[] pawnControllers;
    public RuntimeAnimatorController dustController;
    public RuntimeAnimatorController[] heroControllers;
    public Sprite[] goldStones;
    public Sprite goldIcon;
    [Tooltip("Altın madeni evi; renk kademesine göre (mavi, sarı, kırmızı, mor, siyah)")]
    public Sprite[] mineHouses;
    public Sprite[] cloudShadows;
    public Sprite[] bushes;
    public RuntimeAnimatorController[] bushControllers;
    public GameObject garrisonPrefab;
    [Tooltip("Tiny Swords ağaç sallanma animasyonları (Tree 1-4); Ana Üs ağaçları")]
    public RuntimeAnimatorController[] treeControllers;
    [Tooltip("Top mermisi isabeti (Explosion 1-2)")]
    public RuntimeAnimatorController[] explosionControllers;
    [Tooltip("Yıkılan yapının enkazında yanan ateş (Fire 1-3)")]
    public RuntimeAnimatorController[] fireControllers;

    private static BaseVisualCatalog instance;
    public static BaseVisualCatalog Instance => instance != null
        ? instance : instance = Resources.Load<BaseVisualCatalog>("BaseVisualCatalog");

    public BuildingLevelVisual[] GetLevels(BuildingType type)
    {
        if (buildings == null) return null;
        foreach (var set in buildings)
            if (set != null && set.type == type) return set.levels;
        return null;
    }

    // Renk her 3 seviyede değişir: 1-3 mavi, 4-6 sarı, 7-9 kırmızı, 10-12 mor, 13-15 siyah.
    public static int ColorIndex(int level) => Progression.ColorTier(level);

    public RuntimeAnimatorController GetUnitController(BuildingType type, int level)
    {
        var array = type == BuildingType.ArcherTower ? archerControllers
            : type == BuildingType.CannonTower ? warriorControllers : pawnControllers;
        int index = ColorIndex(level);
        return array != null && index < array.Length ? array[index] : null;
    }
}
