using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Üs haritasının tek doluluk (occupancy) sorgusu. Bina, inşaatçı kulübesi, dekor ve kahraman
/// kampı tek bir hücre ızgarasında çakışma denetimine girer; bina ve dekor yerleştirmesi aynı
/// kuralı kullanır. Ağaç/kayalar yerleştirmeyi engellemez (temizlenebilir arazi öğesi).
/// </summary>
public static class BaseOccupancyGrid
{
    private static GameObject camp;
    private static int campSearchFrame = -1;

    /// <summary>Merkez + boyuttan hücre dikdörtgeni (sol-alt hücre, genişlik, yükseklik).</summary>
    public static RectInt CellsFor(Vector2 size, Vector2 center) => new RectInt(
        Mathf.RoundToInt(center.x - size.x * 0.5f), Mathf.RoundToInt(center.y - size.y * 0.5f),
        Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));

    public static bool Overlaps(RectInt a, RectInt b) =>
        a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

    /// <summary>Hücrelerin tamamı şu an açılmış (temizlenmiş) arazinin içinde mi?</summary>
    public static bool InsideUnlockedArea(RectInt cells, bool edgeMargin)
    {
        var map = BaseMapManager.Instance;
        if (map == null) return false;
        var area = map.BuildableRect;
        if (area.width <= 0 || area.height <= 0) return false;
        if (cells.width <= 0 || cells.height <= 0) return false;
        if (cells.xMin < area.xMin || cells.xMax > area.xMax ||
            cells.yMin < area.yMin || cells.yMax > area.yMax) return false;
        if (!edgeMargin) return true;
        // Binalar sabit sınırın 1 hücre içinde kalır (yol/çit payı).
        var fixedArea = BaseWorldLayout.Buildable;
        return cells.xMin >= fixedArea.xMin + 1 && cells.xMax <= fixedArea.xMax - 1 &&
            cells.yMin >= fixedArea.yMin + 1 && cells.yMax <= fixedArea.yMax - 1;
    }

    /// <summary>
    /// Hücre dikdörtgeni boş mu? <paramref name="ignoreObject"/> / <paramref name="ignoreDecor"/> taşınan
    /// nesnenin kendisidir. <paramref name="onlyObjects"/> verilirse yalnızca o harita nesneleri engel
    /// sayılır (yükleme onarımında "önce yerleşen kazanır" sırası için).
    /// </summary>
    public static bool IsFree(RectInt cells, PlacedMapObject ignoreObject = null,
        DecorInstance ignoreDecor = null, bool edgeMargin = false, bool includeDecor = true,
        ICollection<PlacedMapObject> onlyObjects = null)
    {
        if (!InsideUnlockedArea(cells, edgeMargin)) return false;

        var campObject = FindCamp();
        if (campObject != null)
        {
            Vector3 p = campObject.transform.position;
            if (Overlaps(cells, CellsFor(new Vector2(2f, 2f), p))) return false;
        }

        var map = BaseMapManager.Instance;
        foreach (var other in map.MapObjects)
        {
            if (other == null || other == ignoreObject || other.gameObject == null || other.isBeingRemoved) continue;
            bool building = other.objectType == MapObjectType.Building && other.buildingType.HasValue;
            if (!building && other.objectType != MapObjectType.BuilderHut) continue;
            if (onlyObjects != null && !onlyObjects.Contains(other)) continue;
            if (Overlaps(cells, CellsFor((Vector2)BaseMapManager.FootprintOf(other),
                other.gameObject.transform.position))) return false;
        }

        var decor = includeDecor ? DecorPlacementController.Instance : null;
        if (decor != null)
            foreach (var item in decor.Placed)
            {
                if (item == null || item == ignoreDecor || item.Data == null) continue;
                var itemCells = new RectInt(item.Cell.x, item.Cell.y,
                    item.Data.footprint.x, item.Data.footprint.y);
                if (Overlaps(cells, itemCells)) return false;
            }
        return true;
    }

    /// <summary>
    /// Yürünemez hücreler: bina, inşaatçı kulübesi, dekor ve kahraman kampı. İnşaatçı yol bulma kullanır;
    /// <paramref name="ignoreCells"/> içindeki dikdörtgenler (başlangıç/hedef yapısı) yok sayılır.
    /// </summary>
    public static HashSet<Vector2Int> BlockedCells(params RectInt[] ignoreCells)
    {
        var blocked = new HashSet<Vector2Int>();
        void Add(RectInt r)
        {
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++) blocked.Add(new Vector2Int(x, y));
        }
        var map = BaseMapManager.Instance;
        if (map == null) return blocked;
        var campObject = FindCamp();
        if (campObject != null) Add(CellsFor(new Vector2(2f, 2f), campObject.transform.position));
        foreach (var other in map.MapObjects)
        {
            if (other == null || other.gameObject == null || other.isBeingRemoved) continue;
            bool building = other.objectType == MapObjectType.Building && other.buildingType.HasValue;
            if (!building && other.objectType != MapObjectType.BuilderHut) continue;
            Add(CellsFor((Vector2)BaseMapManager.FootprintOf(other), other.gameObject.transform.position));
        }
        var decor = DecorPlacementController.Instance;
        if (decor != null)
            foreach (var item in decor.Placed)
            {
                if (item == null || item.Data == null) continue;
                Add(new RectInt(item.Cell.x, item.Cell.y, item.Data.footprint.x, item.Data.footprint.y));
            }
        if (ignoreCells != null)
            foreach (var r in ignoreCells)
                for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++) blocked.Remove(new Vector2Int(x, y));
        return blocked;
    }

    private static GameObject FindCamp()
    {
        if (camp != null) return camp;
        if (campSearchFrame == Time.frameCount) return null;
        campSearchFrame = Time.frameCount;
        camp = GameObject.Find("HeroCamp");
        return camp;
    }
}
