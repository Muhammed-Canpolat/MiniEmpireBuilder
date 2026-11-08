using System.Collections.Generic;
using UnityEngine;

public static class BaseWorldLayout
{
    public const int Width = 22;
    public const int Height = 38;
    public static readonly RectInt Buildable = new RectInt(2, 2, 18, 34);
    public static readonly RectInt InitialClean = new RectInt(2, 12, 18, 14);

    public static RectInt CleanAtLevel(int level)
    {
        if (level >= 5) return Buildable;
        int expansions = Mathf.Clamp(level - 1, 0, 4);
        return new RectInt(2, 12 - expansions * 2, 18, 14 + expansions * 4);
    }

    public struct Slot
    {
        public string id;
        public BuildingType? buildingType;
        public Vector2Int topLeft;
        public Vector2Int footprint;
        public Vector2 center => new Vector2(
            Buildable.xMin + topLeft.x + footprint.x * 0.5f,
            Buildable.yMax - topLeft.y - footprint.y * 0.5f);
        public Slot(string id, BuildingType? type, int x, int y, int w, int h)
        {
            this.id = id;
            buildingType = type;
            topLeft = new Vector2Int(x, y);
            footprint = new Vector2Int(w, h);
        }
    }

    // Extra fixed slots preserve the game's existing per-type build counts.
    public static readonly Slot[] Slots =
    {
        new Slot("main", BuildingType.MainBase, 4, 13, 9, 4),
        new Slot("builder", null, 1, 13, 2, 2),
        new Slot("mine_1", BuildingType.GoldMine, 13, 19, 4, 4),
        new Slot("cannon_1", BuildingType.CannonTower, 7, 7, 3, 2),
        new Slot("archer_1", BuildingType.ArcherTower, 2, 19, 4, 4),
        new Slot("camp", null, 8, 24, 2, 2),
        new Slot("mine_2", BuildingType.GoldMine, 13, 3, 4, 4),
        new Slot("mine_3", BuildingType.GoldMine, 1, 23, 4, 4),
        new Slot("mine_4", BuildingType.GoldMine, 13, 26, 4, 4),
        new Slot("archer_2", BuildingType.ArcherTower, 1, 7, 4, 4),
        new Slot("archer_3", BuildingType.ArcherTower, 3, 27, 4, 4),
        new Slot("cannon_2", BuildingType.CannonTower, 8, 3, 3, 2),
        new Slot("monastery_1", BuildingType.Monastery, 13, 8, 3, 3)
    };

    public static Slot Get(string id)
    {
        foreach (var slot in Slots)
            if (slot.id == id) return slot;
        return default;
    }

    public static bool TryGet(string id, out Slot value)
    {
        foreach (var slot in Slots)
            if (slot.id == id) { value = slot; return true; }
        value = default;
        return false;
    }

    public static IEnumerable<Slot> ForBuilding(BuildingType type)
    {
        foreach (var slot in Slots)
            if (slot.buildingType == type) yield return slot;
    }

    public static Vector2Int Footprint(BuildingType type)
    {
        foreach (var slot in Slots)
            if (slot.buildingType == type) return slot.footprint;
        return new Vector2Int(2, 2);
    }

    public static List<Rect> GetOccupiedFootprints()
    {
        var footprints = new List<Rect>();
        foreach (var building in Object.FindObjectsOfType<BuildingIdentifier>())
        {
            Vector2 size = Footprint(building.buildingType);
            Vector2 min = (Vector2)building.transform.position - size * 0.5f;
            footprints.Add(new Rect(min, size));
        }
        var hut = GameObject.Find("BuilderHut");
        if (hut != null)
            footprints.Add(new Rect((Vector2)hut.transform.position - Vector2.one, Vector2.one * 2f));
        return footprints;
    }

    public static bool IsWithinBuildingFootprint(Vector2 point, List<Rect> footprints)
    {
        foreach (var footprint in footprints)
            if (footprint.Contains(point)) return true;
        return false;
    }
}
