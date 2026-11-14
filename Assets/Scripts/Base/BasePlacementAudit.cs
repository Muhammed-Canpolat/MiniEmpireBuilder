using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 5. oturum (1.1): Ana Üs yerleşim denetimi. Her nesnenin hücre alanını (ayak izi) görünür sprite sınırıyla karşılaştırır ve tüm açık
/// araziyi her bina boyutu için tarar: "görünüşte boş" bir yer reddediliyorsa (oyuncunun göremediği bir engel) bunu <see cref="Scan"/>
/// raporlar. Test ve hata ayıklama görüntüsü (<see cref="Render"/>) bunu kullanır.
/// </summary>
public static class BasePlacementAudit
{
    public struct Item
    {
        public string name;
        public string kind;     // building / hut / decor / camp / nature
        public RectInt cells;
        public Rect visual;
        public GameObject go;
    }

    /// <summary>Doluluğa giren her nesne: hücre alanı + görünür dikdörtgen.</summary>
    public static List<Item> Items()
    {
        var list = new List<Item>();
        var map = BaseMapManager.Instance;
        if (map != null)
            foreach (var obj in map.MapObjects)
            {
                if (obj == null || obj.gameObject == null || obj.isBeingRemoved) continue;
                bool building = obj.objectType == MapObjectType.Building && obj.buildingType.HasValue;
                if (!building && obj.objectType != MapObjectType.BuilderHut) continue;
                list.Add(new Item
                {
                    name = building ? obj.buildingType.Value + ":" + obj.id : "hut:" + obj.id,
                    kind = building ? "building" : "hut",
                    cells = BaseOccupancyGrid.CellsFor((Vector2)BaseMapManager.FootprintOf(obj), obj.gameObject.transform.position),
                    visual = BaseVisualBounds.Of(obj.gameObject),
                    go = obj.gameObject
                });
            }
        if (DecorPlacementController.Instance != null)
            foreach (var d in DecorPlacementController.Instance.Placed)
                if (d != null && d.Data != null)
                    list.Add(new Item
                    {
                        name = "decor:" + d.Data.id, kind = "decor",
                        cells = new RectInt(d.Cell.x, d.Cell.y, d.Data.footprint.x, d.Data.footprint.y),
                        visual = BaseVisualBounds.Of(d.gameObject), go = d.gameObject
                    });
        foreach (var camp in BaseOccupancyGrid.CampCells())
            list.Add(new Item { name = "camp", kind = "camp", cells = BaseOccupancyGrid.CellsFor(new Vector2(2f, 2f), camp.go.transform.position), visual = BaseVisualBounds.Of(camp.go), go = camp.go });
        foreach (var pair in NatureObstacle.ByCell)
            if (pair.Value != null)
                list.Add(new Item
                {
                    name = "nature:" + pair.Value.Id, kind = "nature", cells = new RectInt(pair.Key.x, pair.Key.y, 1, 1),
                    visual = BaseVisualBounds.Of(pair.Value.gameObject), go = pair.Value.gameObject
                });
        return list;
    }

    /// <summary>Nesne başına: hücreler, görsel, görselin hücre alanından taşma/eksik payı (dünya birimi).</summary>
    public static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ad | tür | hücreler (x,y,g,y) | görsel [xMin,yMin–xMax,yMax] | taşma sol/sağ/alt/üst (+ dışarı, − içeri)");
        foreach (var it in Items())
        {
            var c = it.cells; var v = it.visual;
            string over = v.width <= 0f ? "görsel yok"
                : $"{c.xMin - v.xMin:+0.0;-0.0}/{v.xMax - c.xMax:+0.0;-0.0}/{c.yMin - v.yMin:+0.0;-0.0}/{v.yMax - c.yMax:+0.0;-0.0}";
            sb.AppendLine($"{it.name} | {it.kind} | ({c.x},{c.y},{c.width},{c.height}) | [{v.xMin:0.0},{v.yMin:0.0}–{v.xMax:0.0},{v.yMax:0.0}] | {over}");
        }
        return sb.ToString();
    }

    public sealed class ScanResult
    {
        public readonly List<string> Lines = new List<string>();
        public int Candidates, Accepted, HiddenRejects, VisibleRejects, UglyAccepts;
        /// <summary>Kural ile ızgarada gösterilen kırmızı hücrelerin uyuşmadığı aday sayısı (0 olmalı).</summary>
        public int DisplayMismatches;
        /// <summary>Görünüşte boş ama reddedilen dikdörtgenlerin hücreleri (kırmızı çizilir).</summary>
        public readonly HashSet<Vector2Int> HiddenCells = new HashSet<Vector2Int>();
        /// <summary>Kabul edilip görseli başka yapıya binen yerlerin hücreleri (turuncu çizilir).</summary>
        public readonly HashSet<Vector2Int> UglyCells = new HashSet<Vector2Int>();
    }

    private static bool IsStructureKind(string kind) => kind == "building" || kind == "hut" || kind == "camp";

    /// <summary>
    /// Oyuncunun gözüyle "boş" mu (yerleşim kuralından BAĞIMSIZ ölçüt): ayak izi hiçbir nesnenin hücresine girmiyor, hiçbir yapının
    /// görseli ayak izinin üstünde durmuyor ve yerleştirilen yapının kendi görseli (<paramref name="candidateVisual"/>, dünya) başka bir
    /// yapının görseline payından fazla binmiyor. Dekorun yalnız hücresi sayılır (görseli çatılara binebilir).
    /// </summary>
    public static bool LooksEmpty(RectInt cells, List<Item> items, GameObject ignore = null, Rect? candidateVisual = null)
    {
        var rect = new Rect(cells.x, cells.y, cells.width, cells.height);
        foreach (var it in items)
        {
            if (ignore != null && it.go == ignore) continue;
            if (BaseOccupancyGrid.Overlaps(cells, it.cells)) return false;
            if (!IsStructureKind(it.kind)) continue;
            if (BaseVisualBounds.Overlap(rect, it.visual)) return false;
            if (candidateVisual.HasValue && BaseVisualBounds.Overlap(candidateVisual.Value, it.visual)) return false;
        }
        return true;
    }

    /// <summary>
    /// Her yerleştirilebilir bina türü için açık arazinin her hücresini dener. <paramref name="subject"/> türün göreli görselini ve
    /// (taşıma taramasında) yok sayılacak kendi nesnesini verir; <paramref name="validator"/> oyunun gerçek kuralıdır.
    /// Gizli ret = görünüşte boş ama reddedildi; çirkin kabul = kabul edildi ama görseli başka yapıya biniyor. İkisi de 0 olmalı.
    /// </summary>
    public static ScanResult Scan(System.Func<BuildingType, Vector2, bool> validator = null,
        System.Func<BuildingType, (Rect? visual, GameObject ignore)> subject = null)
    {
        var result = new ScanResult();
        var map = BaseMapManager.Instance;
        if (map == null) { result.Lines.Add("BaseMapManager yok"); return result; }
        var items = Items();
        var area = map.BuildableRect;
        var types = new List<BuildingType>();
        foreach (var slot in BaseWorldLayout.Slots)
            if (slot.buildingType.HasValue && slot.buildingType != BuildingType.MainBase && !types.Contains(slot.buildingType.Value))
                types.Add(slot.buildingType.Value);
        var summary = new List<string>();
        var details = new List<string>();
        foreach (var type in types)
        {
            var size = BaseWorldLayout.Footprint(type);
            var (rel, ignore) = subject != null ? subject(type) : (BaseOccupancyGrid.TypicalVisual(type), null);
            int candidates = 0, accepted = 0, hidden = 0, visible = 0, ugly = 0, mismatch = 0;
            PlacedMapObject ignoreObj = null;
            foreach (var o in map.MapObjects) if (o != null && ignore != null && o.gameObject == ignore) ignoreObj = o;
            var shown = BaseOccupancyGrid.OccupiedForDisplay(ignoreObj, null, true);
            var shownStructures = BaseOccupancyGrid.OccupiedForDisplay(ignoreObj, null, false, false);   // çatı/görsel hücreleri yalnız yapılarla çakışır
            for (int y = area.yMin; y <= area.yMax - size.y; y++)
                for (int x = area.xMin; x <= area.xMax - size.x; x++)
                {
                    var cells = new RectInt(x, y, size.x, size.y);
                    if (!BaseOccupancyGrid.InsideUnlockedArea(cells, true)) continue;   // çit payı: ızgarada kırmızı şerit
                    candidates++;
                    Vector2 center = new Vector2(x + size.x * 0.5f, y + size.y * 0.5f);
                    Rect? world = rel.HasValue ? new Rect(center + rel.Value.position, rel.Value.size) : (Rect?)null;
                    bool inside = !world.HasValue || BaseOccupancyGrid.InsideUnlockedArea(BaseOccupancyGrid.VisualCells(world.Value), false)
                        || BaseOccupancyGrid.VisualCells(world.Value).width == 0;
                    bool empty = inside && LooksEmpty(cells, items, ignore, world);
                    bool ok = validator != null ? validator(type, center) : map.CanPlaceBuildingWithVisual(type, center, null, rel);
                    // Izgara tutarlılığı: ret ⇔ hayaletin engel alanında kırmızı gösterilen (ya da alan dışı) bir hücre var
                    var block = BaseOccupancyGrid.BlockAt(size, center, rel);
                    bool shownConflict = false;
                    for (int by = block.yMin; by < block.yMax && !shownConflict; by++)
                        for (int bx = block.xMin; bx < block.xMax && !shownConflict; bx++)
                        {
                            var bc = new Vector2Int(bx, by);
                            bool inFootprint = cells.Contains(bc);
                            if (!area.Contains(bc) || (inFootprint ? shown.Contains(bc) : shownStructures.Contains(bc)))
                                shownConflict = true;
                        }
                    if (ok == shownConflict)
                    {
                        mismatch++;
                        if (mismatch <= 4) details.Add($"  IZGARA UYUŞMAZLIĞI {type} @ ({x},{y}): kural {(ok ? "kabul" : "ret")}, ızgara {(shownConflict ? "kırmızı" : "boş")}");
                    }
                    if (ok)
                    {
                        accepted++;
                        if (!empty)
                        {
                            ugly++;
                            for (int cy = cells.yMin; cy < cells.yMax; cy++)
                                for (int cx = cells.xMin; cx < cells.xMax; cx++) result.UglyCells.Add(new Vector2Int(cx, cy));
                            if (ugly <= 4) details.Add($"  ÇİRKİN KABUL {type} {size.x}×{size.y} @ ({x},{y})");
                        }
                        continue;
                    }
                    if (empty)
                    {
                        hidden++;
                        for (int cy = cells.yMin; cy < cells.yMax; cy++)
                            for (int cx = cells.xMin; cx < cells.xMax; cx++) result.HiddenCells.Add(new Vector2Int(cx, cy));
                        if (hidden <= 6)
                        {
                            var why = new List<string>();
                            BaseOccupancyGrid.ConflictCells(cells, ignoreObj, null, true, true, null, rel, new HashSet<Vector2Int>(), why);
                            details.Add($"  GİZLİ RET {type} {size.x}×{size.y} @ ({x},{y}): {string.Join("; ", why)}");
                        }
                    }
                    else visible++;
                }
            result.Candidates += candidates; result.Accepted += accepted; result.HiddenRejects += hidden;
            result.VisibleRejects += visible; result.UglyAccepts += ugly; result.DisplayMismatches += mismatch;
            summary.Add($"{type} {size.x}×{size.y}: aday {candidates}, kabul {accepted}, görünür engel {visible}, sprite-boşluk reddi {hidden}, " +
                $"çirkin kabul {ugly}, ızgara uyuşmazlığı {mismatch}");
        }
        result.Lines.AddRange(summary);
        result.Lines.AddRange(details);
        return result;
    }

    /// <summary>Hata ayıklama görüntüsü: hücre alanları (tür rengi), görsel sınırlar (sarı çerçeve), gizli ret hücreleri (kırmızı).</summary>
    public static Texture2D Render(ScanResult scan = null, int ppu = 24)
    {
        int w = BaseWorldLayout.Width * ppu, h = BaseWorldLayout.Height * ppu;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        var map = BaseMapManager.Instance;
        var area = map != null ? map.BuildableRect : BaseWorldLayout.Buildable;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int cx = x / ppu, cy = y / ppu;
                bool inside = area.Contains(new Vector2Int(cx, cy));
                bool line = x % ppu == 0 || y % ppu == 0;
                px[y * w + x] = inside ? (line ? new Color32(70, 110, 60, 255) : new Color32(96, 150, 80, 255)) : new Color32(40, 40, 40, 255);
            }
        void FillCells(RectInt r, Color32 c)
        {
            for (int y = r.yMin * ppu; y < r.yMax * ppu; y++)
                for (int x = r.xMin * ppu; x < r.xMax * ppu; x++)
                    if (x >= 0 && y >= 0 && x < w && y < h && (x % ppu != 0 && y % ppu != 0)) px[y * w + x] = c;
        }
        void Frame(Rect r, Color32 c, int t)
        {
            int x0 = Mathf.RoundToInt(r.xMin * ppu), x1 = Mathf.RoundToInt(r.xMax * ppu), y0 = Mathf.RoundToInt(r.yMin * ppu), y1 = Mathf.RoundToInt(r.yMax * ppu);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    if (x - x0 < t || x1 - x < t || y - y0 < t || y1 - y < t) px[y * w + x] = c;
                }
        }
        var items = Items();
        foreach (var it in items)
        {
            Color32 c = it.kind == "building" ? new Color32(60, 90, 200, 255) : it.kind == "hut" ? new Color32(150, 90, 40, 255)
                : it.kind == "decor" ? new Color32(230, 150, 40, 255) : it.kind == "camp" ? new Color32(160, 60, 200, 255) : new Color32(20, 70, 20, 255);
            FillCells(it.cells, c);
        }
        if (scan != null)
        {
            foreach (var cell in scan.UglyCells) FillCells(new RectInt(cell.x, cell.y, 1, 1), new Color32(250, 120, 20, 255));
            foreach (var cell in scan.HiddenCells) FillCells(new RectInt(cell.x, cell.y, 1, 1), new Color32(230, 40, 40, 255));
        }
        foreach (var it in items)
            if (IsStructureKind(it.kind))
            {
                var b = BaseOccupancyGrid.Union(it.cells, BaseOccupancyGrid.VisualCells(it.visual));
                Frame(new Rect(b.x, b.y, b.width, b.height), new Color32(40, 220, 230, 255), 1);
            }
        foreach (var it in items) if (it.visual.width > 0f) Frame(it.visual, new Color32(255, 230, 40, 255), 2);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
