using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Üs haritasının tek doluluk (occupancy) sorgusu. Bina, inşaatçı kulübesi, dekor ve kahraman
/// kampı tek bir hücre ızgarasında çakışma denetimine girer; bina ve dekor yerleştirmesi aynı
/// kuralı kullanır. Kilitli araziden kalan doğa nesneleri (NatureObstacle) engeldir; ağaç/kaya kaynakları (PlacedMapObject) değildir.
///
/// 5. oturum (1.1, D62): yapıların (bina, kulübe, kamp bayrağı) ENGEL ALANI = ayak izi ∪ görselinin gerçekten kapladığı hücreler
/// (<see cref="BlockOf"/>). Eskiden ayrıca görünmeyen bir sprite-örtüşme denetimi vardı; yeşil görünen hücrelerde "Buraya sığmıyor"
/// çıkıyordu. Artık tek kural hücrelerdir ve yerleşim ızgarası bu hücreleri kırmızı gösterir (<see cref="ConflictCells"/>).
/// Dekor ve doğa nesneleri yalnız kendi hücrelerini kaplar (çalı/koyun görselleri birbirine ve çatılara binebilir).
/// </summary>
public static class BaseOccupancyGrid
{
    /// <summary>Görselin bir hücreyi "kaplaması" için o hücreye en az bu kadar girmesi gerekir (yan saçak / alt-üst taşma).</summary>
    public const float MarginX = 0.35f, MarginY = 0.3f;

    private static GameObject camp;
    private static int campSearchFrame = -1;

    /// <summary>Merkez + boyuttan hücre dikdörtgeni (sol-alt hücre, genişlik, yükseklik).</summary>
    public static RectInt CellsFor(Vector2 size, Vector2 center) => new RectInt(
        Mathf.RoundToInt(center.x - size.x * 0.5f), Mathf.RoundToInt(center.y - size.y * 0.5f),
        Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));

    public static bool Overlaps(RectInt a, RectInt b) =>
        a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0 &&
        a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

    /// <summary>Görsel dikdörtgenin kapladığı hücreler (kenarlardan <see cref="MarginX"/>/<see cref="MarginY"/> içeri).</summary>
    public static RectInt VisualCells(Rect visual)
    {
        if (visual.width <= 0f || visual.height <= 0f) return new RectInt(0, 0, 0, 0);
        const float eps = 0.01f;   // 11,8 + 0,2 = 11,9999… → 11 olmasın (kayan nokta)
        int x0 = Mathf.FloorToInt(visual.xMin + MarginX + eps), x1 = Mathf.CeilToInt(visual.xMax - MarginX - eps);
        int y0 = Mathf.FloorToInt(visual.yMin + MarginY + eps), y1 = Mathf.CeilToInt(visual.yMax - MarginY - eps);
        return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
    }

    public static RectInt Union(RectInt a, RectInt b)
    {
        if (b.width <= 0 || b.height <= 0) return a;
        if (a.width <= 0 || a.height <= 0) return b;
        int x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin);
        int x1 = Mathf.Max(a.xMax, b.xMax), y1 = Mathf.Max(a.yMax, b.yMax);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    // ==================== GÖRSEL (dinlenme ölçeğinde, konuma göre) ====================

    private static readonly Dictionary<GameObject, (Rect rel, int frame)> restCache = new Dictionary<GameObject, (Rect, int)>();
    private static readonly Dictionary<GameObject, Rect> lastGood = new Dictionary<GameObject, Rect>();

    /// <summary>
    /// Nesnenin görünür dikdörtgeni, kendi konumuna göre (sol-alt köşe = görsel.min − konum) ve DİNLENME ölçeğinde: açılış (0→1),
    /// dokunma zıplaması ya da taşıma büyütmesi (×1,1) hücreleri oynatmaz. Kare başına önbellekli.
    /// </summary>
    public static Rect RestVisualRelative(GameObject go)
    {
        if (go == null) return Rect.zero;
        if (restCache.TryGetValue(go, out var c) && c.frame == Time.frameCount) return c.rel;
        if (restCache.Count > 256) restCache.Clear();
        var t = go.transform;
        Vector3 scale = t.localScale;
        var visual = go.GetComponent<BaseBuildingVisual>();
        bool swap = visual != null && visual.RestScale.sqrMagnitude > 0.0001f && (scale - visual.RestScale).sqrMagnitude > 0.000001f;
        if (swap) t.localScale = visual.RestScale;
        Rect world = BaseVisualBounds.Of(go);
        if (swap) t.localScale = scale;
        Rect rel;
        if (world.width > 0f) { rel = new Rect(world.position - (Vector2)t.position, world.size); lastGood[go] = rel; }
        else rel = lastGood.TryGetValue(go, out var good) ? good : Rect.zero;   // animasyon karesi/gizli: son geçerli ölçüm
        restCache[go] = (rel, Time.frameCount);
        return rel;
    }

    /// <summary>Önbelleği boşalt (bina yükseltildi/görseli değişti).</summary>
    public static void InvalidateVisuals() { restCache.Clear(); }

    /// <summary>Bu türden kurulu bir binanın göreli görseli (yeni bina hayaleti yoksa tarama/yerleştirme için örnek).</summary>
    public static Rect? TypicalVisual(BuildingType type)
    {
        var map = BaseMapManager.Instance;
        if (map == null) return null;
        foreach (var o in map.MapObjects)
            if (o != null && o.gameObject != null && !o.isBeingRemoved && o.objectType == MapObjectType.Building && o.buildingType == type)
            {
                var rel = RestVisualRelative(o.gameObject);
                if (rel.width > 0f) return rel;
            }
        // Bu türden ilk bina: kurulacak görselin kendisi (1. seviye sprite'ı × bina ölçeği); maden gibi parçalı görselde ayak izi yeter.
        if (type == BuildingType.GoldMine) return null;
        var sprite = BaseMapManager.PreviewSprite(type);
        if (sprite == null) return null;
        Vector3 scale = BaseMapManager.BuildingVisualScale(type, sprite);
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        foreach (var v in sprite.vertices)
        {
            xMin = Mathf.Min(xMin, v.x * scale.x); xMax = Mathf.Max(xMax, v.x * scale.x);
            yMin = Mathf.Min(yMin, v.y * scale.y); yMax = Mathf.Max(yMax, v.y * scale.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // ==================== ENGEL ALANLARI ====================

    private static bool IsStructure(PlacedMapObject o) =>
        o != null && o.gameObject != null && !o.isBeingRemoved &&
        (o.objectType == MapObjectType.Building && o.buildingType.HasValue || o.objectType == MapObjectType.BuilderHut);

    /// <summary>Yapının engel alanı: ayak izi ∪ görsel hücreleri, verilen merkezde (varsayılan: şu anki konumu).</summary>
    public static RectInt BlockAt(Vector2Int footprint, Vector2 center, Rect? visualRelative)
    {
        var cells = CellsFor((Vector2)footprint, center);
        if (!visualRelative.HasValue || visualRelative.Value.width <= 0f) return cells;
        var r = visualRelative.Value;
        return Union(cells, VisualCells(new Rect(center + r.position, r.size)));
    }

    public static RectInt BlockOf(PlacedMapObject obj) =>
        BlockAt(BaseMapManager.FootprintOf(obj), obj.gameObject.transform.position, RestVisualRelative(obj.gameObject));

    /// <summary>
    /// Kahraman kampının (sarı bayrak) kapladığı hücreler: bayrağın görseli (5. oturum: eskiden görselden büyük sabit 2×2 kareydi,
    /// bayrağın altındaki boş görünen satır yerleşimi engelliyordu). Görsel ölçülemezse 2×2.
    /// </summary>
    public static List<(RectInt cells, GameObject go)> CampCells()
    {
        var list = new List<(RectInt, GameObject)>();
        var campObject = FindCamp();
        if (campObject != null)
        {
            Vector2 p = campObject.transform.position;
            var rel = RestVisualRelative(campObject);
            var cells = rel.width > 0f ? VisualCells(new Rect(p + rel.position, rel.size)) : new RectInt(0, 0, 0, 0);
            if (cells.width <= 0 || cells.height <= 0) cells = CellsFor(new Vector2(2f, 2f), p);
            list.Add((cells, campObject));
        }
        return list;
    }

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
        // Binalar sabit sınırın 1 hücre içinde kalır (yol/çit payı). Yerleşim ızgarası bu şeridi kırmızı gösterir.
        var fixedArea = BaseWorldLayout.Buildable;
        return cells.xMin >= fixedArea.xMin + 1 && cells.xMax <= fixedArea.xMax - 1 &&
            cells.yMin >= fixedArea.yMin + 1 && cells.yMax <= fixedArea.yMax - 1;
    }

    /// <summary>Çit payı şeridindeki hücre mi (yapı ayak izi buraya giremez).</summary>
    public static bool InEdgeMargin(Vector2Int cell)
    {
        var f = BaseWorldLayout.Buildable;
        return cell.x < f.xMin + 1 || cell.x >= f.xMax - 1 || cell.y < f.yMin + 1 || cell.y >= f.yMax - 1;
    }

    /// <summary>
    /// Hücre dikdörtgeni boş mu? <paramref name="ignoreObject"/> / <paramref name="ignoreDecor"/> taşınan
    /// nesnenin kendisidir. <paramref name="onlyObjects"/> verilirse yalnızca o harita nesneleri engel
    /// sayılır (yükleme onarımında "önce yerleşen kazanır" sırası için). <paramref name="visualRelative"/> yerleştirilen yapının
    /// göreli görseli: verilirse yapının engel alanı (ayak izi ∪ görsel hücreleri) diğer yapıların engel alanlarıyla karşılaştırılır.
    /// </summary>
    public static bool IsFree(RectInt cells, PlacedMapObject ignoreObject = null,
        DecorInstance ignoreDecor = null, bool edgeMargin = false, bool includeDecor = true,
        ICollection<PlacedMapObject> onlyObjects = null, Rect? visualRelative = null)
        => ConflictCells(cells, ignoreObject, ignoreDecor, edgeMargin, includeDecor, onlyObjects, visualRelative, null);

    /// <summary>
    /// <see cref="IsFree"/> ile aynı kural; <paramref name="conflicts"/> verilirse çakışan hücreleri (aday engel alanındaki) toplar.
    /// Dönüş: boş mu. Yerleşim ızgarası kırmızı hücreleri buradan çizer.
    /// </summary>
    public static bool ConflictCells(RectInt cells, PlacedMapObject ignoreObject, DecorInstance ignoreDecor, bool edgeMargin,
        bool includeDecor, ICollection<PlacedMapObject> onlyObjects, Rect? visualRelative, HashSet<Vector2Int> conflicts,
        List<string> reasons = null)
    {
        bool free = true;
        Vector2 center = new Vector2(cells.x + cells.width * 0.5f, cells.y + cells.height * 0.5f);
        RectInt block = BlockAt(new Vector2Int(cells.width, cells.height), center, visualRelative);
        void Mark(RectInt a, RectInt b)
        {
            free = false;
            if (conflicts == null) return;
            int x0 = Mathf.Max(a.xMin, b.xMin), x1 = Mathf.Min(a.xMax, b.xMax), y0 = Mathf.Max(a.yMin, b.yMin), y1 = Mathf.Min(a.yMax, b.yMax);
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) conflicts.Add(new Vector2Int(x, y));
        }
        bool Bail() => !free && conflicts == null;

        if (!InsideUnlockedArea(cells, edgeMargin) || !InsideUnlockedArea(block, false))
        {
            free = false;
            reasons?.Add(!InsideUnlockedArea(cells, edgeMargin) ? "alan/çit payı" : "görsel alan dışına taşıyor " + block);
            if (conflicts == null) return false;
            var map0 = BaseMapManager.Instance;
            var area = map0 != null ? map0.BuildableRect : BaseWorldLayout.Buildable;
            for (int y = block.yMin; y < block.yMax; y++)
                for (int x = block.xMin; x < block.xMax; x++)
                {
                    var c = new Vector2Int(x, y);
                    bool inFootprint = cells.Contains(c);
                    if (!area.Contains(c) || inFootprint && edgeMargin && InEdgeMargin(c)) conflicts.Add(c);
                }
        }

        foreach (var (campCells, _) in CampCells())
            if (Overlaps(block, campCells)) { Mark(block, campCells); reasons?.Add("kamp " + campCells + " / aday " + block); if (Bail()) return false; }

        var map = BaseMapManager.Instance;
        if (map != null)
            foreach (var other in map.MapObjects)
            {
                if (other == ignoreObject || !IsStructure(other)) continue;
                if (onlyObjects != null && !onlyObjects.Contains(other)) continue;
                var otherBlock = BlockOf(other);
                if (Overlaps(block, otherBlock)) { Mark(block, otherBlock); reasons?.Add(other.gameObject.name + " " + otherBlock + " / aday " + block); if (Bail()) return false; }
            }

        for (int y = cells.yMin; y < cells.yMax; y++)
            for (int x = cells.xMin; x < cells.xMax; x++)
                if (NatureObstacle.ByCell.ContainsKey(new Vector2Int(x, y)))
                {
                    Mark(cells, new RectInt(x, y, 1, 1));
                    reasons?.Add("doğa " + x + "," + y);
                    if (Bail()) return false;
                }

        var decor = includeDecor ? DecorPlacementController.Instance : null;
        if (decor != null)
            foreach (var item in decor.Placed)
            {
                if (item == null || item == ignoreDecor || item.Data == null) continue;
                var itemCells = new RectInt(item.Cell.x, item.Cell.y, item.Data.footprint.x, item.Data.footprint.y);
                if (Overlaps(cells, itemCells)) { Mark(cells, itemCells); reasons?.Add("dekor " + item.Data.id + " " + itemCells); if (Bail()) return false; }
            }
        return free;
    }

    /// <summary>
    /// Yüklemedeki onarım için YUMUŞAK denetim (5. oturum, D62): mevcut nesne yalnız AYAK İZİ başka bir yapının ayak izine, kampa, doğaya
    /// (ve istenirse dekora) giriyorsa ya da alan dışındaysa "bozuk" sayılır. Hücre yuvarlaması (iki görsel aynı hücreye karşı yönden taşıyor)
    /// yüzünden oyuncunun eski düzeni yerinden oynamasın; katı kural (<see cref="IsFree"/>) yeni yerleştirme ve taşımada geçerlidir.
    /// </summary>
    public static bool FootprintFree(RectInt cells, PlacedMapObject ignoreObject, DecorInstance ignoreDecor, bool edgeMargin,
        bool includeDecor, ICollection<PlacedMapObject> onlyObjects)
    {
        if (!InsideUnlockedArea(cells, edgeMargin)) return false;
        foreach (var (campCells, _) in CampCells()) if (Overlaps(cells, campCells)) return false;
        var map = BaseMapManager.Instance;
        if (map != null)
            foreach (var other in map.MapObjects)
            {
                if (other == ignoreObject || !IsStructure(other)) continue;
                if (onlyObjects != null && !onlyObjects.Contains(other)) continue;
                if (Overlaps(cells, CellsFor((Vector2)BaseMapManager.FootprintOf(other), other.gameObject.transform.position))) return false;
            }
        if (NatureObstacle.AnyIn(cells)) return false;
        var decor = includeDecor ? DecorPlacementController.Instance : null;
        if (decor != null)
            foreach (var item in decor.Placed)
            {
                if (item == null || item == ignoreDecor || item.Data == null) continue;
                if (Overlaps(cells, new RectInt(item.Cell.x, item.Cell.y, item.Data.footprint.x, item.Data.footprint.y))) return false;
            }
        return true;
    }

    /// <summary>İki yapının dinlenme görselleri payından fazla iç içe mi (yükleme onarımı).</summary>
    public static bool VisualClash(PlacedMapObject a, PlacedMapObject b)
    {
        if (!IsStructure(a) || !IsStructure(b)) return false;
        var ra = RestVisualRelative(a.gameObject); ra.position += (Vector2)a.gameObject.transform.position;
        var rb = RestVisualRelative(b.gameObject); rb.position += (Vector2)b.gameObject.transform.position;
        return BaseVisualBounds.Overlap(ra, rb);
    }

    /// <summary>
    /// Yerleşim ızgarasında kırmızı gösterilecek hücreler: yapıların engel alanları, kamp, dekor, doğa ve (yapı için) çit payı şeridi.
    /// <paramref name="ignoreObject"/>/<paramref name="ignoreDecor"/> taşınan nesnedir.
    /// </summary>
    public static HashSet<Vector2Int> OccupiedForDisplay(PlacedMapObject ignoreObject, DecorInstance ignoreDecor, bool structure,
        bool includeSmall = true)
    {
        var set = new HashSet<Vector2Int>();
        void Add(RectInt r) { for (int y = r.yMin; y < r.yMax; y++) for (int x = r.xMin; x < r.xMax; x++) set.Add(new Vector2Int(x, y)); }
        foreach (var (campCells, _) in CampCells()) Add(campCells);
        var map = BaseMapManager.Instance;
        if (map != null)
            foreach (var other in map.MapObjects)
                if (other != ignoreObject && IsStructure(other)) Add(BlockOf(other));
        if (includeSmall) foreach (var cell in NatureObstacle.ByCell.Keys) set.Add(cell);
        var decor = includeSmall ? DecorPlacementController.Instance : null;
        if (decor != null)
            foreach (var item in decor.Placed)
                if (item != null && item != ignoreDecor && item.Data != null && item.gameObject.activeSelf)
                    Add(new RectInt(item.Cell.x, item.Cell.y, item.Data.footprint.x, item.Data.footprint.y));
        if (structure && map != null)
        {
            var area = map.BuildableRect;
            for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                    if (InEdgeMargin(new Vector2Int(x, y))) set.Add(new Vector2Int(x, y));
        }
        return set;
    }

    /// <summary>
    /// Yürünemez hücreler: bina, inşaatçı kulübesi (engel alanı: görseli dahil), dekor ve kahraman kampı. İnşaatçı yol bulma kullanır;
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
        foreach (var (campCells, _) in CampCells()) Add(campCells);
        foreach (var other in map.MapObjects)
            if (IsStructure(other)) Add(BlockOf(other));
        var decor = DecorPlacementController.Instance;
        if (decor != null)
            foreach (var item in decor.Placed)
            {
                if (item == null || item.Data == null) continue;
                Add(new RectInt(item.Cell.x, item.Cell.y, item.Data.footprint.x, item.Data.footprint.y));
            }
        foreach (var cell in NatureObstacle.ByCell.Keys) blocked.Add(cell);
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
