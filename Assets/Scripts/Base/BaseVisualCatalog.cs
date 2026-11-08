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
    public Sprite[] cloudShadows;
    public Sprite[] bushes;
    public RuntimeAnimatorController[] bushControllers;
    public GameObject garrisonPrefab;

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

    public static int ColorIndex(int level) => Mathf.Clamp(level - 1, 0, 4);

    public RuntimeAnimatorController GetUnitController(BuildingType type, int level)
    {
        var array = type == BuildingType.ArcherTower ? archerControllers
            : type == BuildingType.CannonTower ? warriorControllers : pawnControllers;
        int index = ColorIndex(level);
        return array != null && index < array.Length ? array[index] : null;
    }
}
