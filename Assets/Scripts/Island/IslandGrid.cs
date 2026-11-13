using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yerleştirme ev sahibi (island_base_v2 §9): hayalet/onay/iptal arayüzü bu arayüzle konuşur; ev sahibi ayak izini, geçerliliği,
/// bedeli ve işlemeyi bilir. Ana Üs <c>PlacementController</c>'ı kendi (BaseMapManager) ev sahibiyle kalır; ada <see cref="IslandPlacementHost"/> kullanır.
/// </summary>
public interface IPlacementHost
{
    IslandLayout.BuildingKind Kind { get; }
    /// <summary>Ayak izi (genişlik, yükseklik) hücre.</summary>
    Vector2Int Footprint { get; }
    /// <summary>Taşınan binanın kimliği (kendisiyle çakışmaz); yeni binada null.</summary>
    string MovingId { get; }
    /// <summary>Başlangıç hücresi (sol-üst).</summary>
    Vector2Int StartCell { get; }
    /// <summary>(Odun, Et) bedeli; taşımada (0, 0).</summary>
    (int wood, int meat) Cost { get; }
    Sprite GhostSprite { get; }
    /// <summary>Hücre (sol-üst) geçerli mi; değilse nedenini verir.</summary>
    bool CanPlace(int col, int row, out string reason);
    /// <summary>Eksik Et/Odun için elmas bedeli (0: eksik yok ya da taşıma).</summary>
    int ShortfallGems { get; }
    /// <summary>Bedel/Usta uygun mu (yeni binada; eksik kaynak elmasla tamamlanabiliyorsa true); değilse nedenini verir.</summary>
    bool CanAfford(out string reason);
    /// <summary>Onay: işler ve başarıyı döndürür.</summary>
    bool Confirm(int col, int row);
}

/// <summary>
/// Ada yerleştirme ızgarası (island_base_v2 §5 "Yerleşim"): serbest 1 hücre ızgara. Geçersiz hücreler: su, yar yüzü, merdiven ve çevresi,
/// yürüyüş şeritleri, kumsal, doğa (ağaç, çalı, kaya, altın taşı, çayır), kilitli bölge, başka bina. Adacığa yalnız kule konur.
/// Saf mantık: <see cref="IslandLayout"/> + bina listesi + bölge maskesi (test edilebilir, sahne gerektirmez).
/// </summary>
public static class IslandGrid
{
    public enum Reason { None, OutOfMap, Water, Cliff, Stairs, Lane, Nature, Height, Locked, Overlap, TowerOnly, NeedTrees, NeedMeadow, Limit, Fixed }

    public static Vector2Int Size(IslandLayout.BuildingKind kind)
    {
        foreach (var spot in IslandLayout.Buildings) if (spot.kind == kind) return new Vector2Int(spot.w, spot.h);
        if (kind == IslandLayout.BuildingKind.Barracks) return new Vector2Int(3, 2);     // Faz E1 (§5)
        if (kind == IslandLayout.BuildingKind.Monastery) return new Vector2Int(3, 3);
        return new Vector2Int(2, 2);
    }

    public static RectInt Rect(IslandLayout.BuildingKind kind, int col, int row)
    {
        var size = Size(kind);
        return new RectInt(col, row, size.x, size.y);
    }

    private static Reason[,] staticReason;
    private static bool[,] mainland;

    private static void EnsureStatic()
    {
        if (staticReason != null) return;
        int w = IslandLayout.Width, h = IslandLayout.Height;
        var reason = new Reason[w, h];
        for (int c = 0; c < w; c++)
            for (int r = 0; r < h; r++)
            {
                if (IslandLayout.HeightAt(c, r) < 0) reason[c, r] = Reason.Water;
                else if (IslandLayout.IsCliff(c, r) || IslandLayout.IsStair(c, r)) reason[c, r] = Reason.Cliff;
            }
        // Merdivenin çevresindeki 1 hücre (iniş/çıkış ağzı)
        for (int c = 0; c < w; c++)
            for (int r = 0; r < h; r++)
                if (IslandLayout.IsStair(c, r))
                    for (int dc = -1; dc <= 1; dc++)
                        for (int dr = -1; dr <= 1; dr++)
                            Mark(reason, c + dc, r + dr, Reason.Stairs);
        // Yürüyüş şeritleri (1 hücre geniş) + kumsal inişi çevresi
        foreach (var beach in IslandLayout.Beaches)
        {
            var path = beach.walkPath;
            for (int i = 1; i < path.Length; i++) MarkSegment(reason, path[i - 1], path[i]);
            Vector2 landing = new Vector2(beach.landing.x, IslandLayout.Height - beach.landing.y);
            for (int dc = -1; dc <= 1; dc++)
                for (int dr = -1; dr <= 1; dr++) Mark(reason, Mathf.FloorToInt(landing.x) + dc, Mathf.FloorToInt(landing.y) + dr, Reason.Lane);
        }
        // Doğa (§5): ağaç, altın taşı, koyun çayırı yerleşimi engeller; çalı ve kaya süstür (bina üstlerine biner, engel değil)
        foreach (var t in IslandLayout.Trees) Mark(reason, t.x, t.y, Reason.Nature);
        foreach (var t in IslandLayout.GoldStones) Mark(reason, t.x, t.y, Reason.Nature);
        var meadow = IslandLayout.Meadow;
        for (int c = meadow.xMin; c < meadow.xMax; c++)
            for (int r = meadow.yMin; r < meadow.yMax; r++) Mark(reason, c, r, Reason.Nature);
        staticReason = reason;

        // Ana kara: kaleden doldurma; kalanlar adacık (yalnız kule)
        mainland = new bool[w, h];
        var stack = new Stack<Vector2Int>();
        var seed = new Vector2Int(10, 7);
        stack.Push(seed);
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (p.x < 0 || p.x >= w || p.y < 0 || p.y >= h || mainland[p.x, p.y] || IslandLayout.HeightAt(p.x, p.y) < 0) continue;
            mainland[p.x, p.y] = true;
            stack.Push(new Vector2Int(p.x + 1, p.y)); stack.Push(new Vector2Int(p.x - 1, p.y));
            stack.Push(new Vector2Int(p.x, p.y + 1)); stack.Push(new Vector2Int(p.x, p.y - 1));
        }
    }

    private static void Mark(Reason[,] reason, int c, int r, Reason value)
    {
        if (c < 0 || c >= reason.GetLength(0) || r < 0 || r >= reason.GetLength(1)) return;
        if (reason[c, r] == Reason.None) reason[c, r] = value;
    }

    /// <summary>Hücre koordinatlarındaki doğru parçasını (dünya noktaları) 1 hücre genişliğinde işaretler.</summary>
    private static void MarkSegment(Reason[,] reason, Vector2 worldA, Vector2 worldB)
    {
        Vector2 a = new Vector2(worldA.x, IslandLayout.Height - worldA.y), b = new Vector2(worldB.x, IslandLayout.Height - worldB.y);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.2f));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            Mark(reason, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Reason.Lane);
        }
    }

    /// <summary>Hücre kalıcı olarak yerleşime kapalı mı (arazi, şerit, doğa); None = serbest.</summary>
    public static Reason StaticReason(int c, int r)
    {
        EnsureStatic();
        if (c < 0 || c >= IslandLayout.Width || r < 0 || r >= IslandLayout.Height) return Reason.OutOfMap;
        return staticReason[c, r];
    }

    public static bool IsMainland(int c, int r)
    {
        EnsureStatic();
        return c >= 0 && c < IslandLayout.Width && r >= 0 && r < IslandLayout.Height && mainland[c, r];
    }

    public static bool ZoneOpen(int zone, int zoneMask) => zone <= 0 || ((zoneMask >> zone) & 1) == 1;

    /// <summary>Kale ayak izi (sabit).</summary>
    public static RectInt CastleRect
    {
        get
        {
            var castle = IslandLayout.Building(IslandLayout.BuildingKind.Castle);
            return new RectInt(castle.col, castle.row, castle.w, castle.h);
        }
    }

    /// <summary>Alandaki ağaç (bölge açık) sayısı: merkezden <see cref="Progression.Island.TreeRadius"/> içinde.</summary>
    public static int TreesNear(Vector2 centerCell, int zoneMask)
    {
        int count = 0;
        foreach (var t in IslandLayout.Trees)
            if (Vector2.Distance(centerCell, new Vector2(t.x + 0.5f, t.y + 0.5f)) <= Progression.Island.TreeRadius
                && ZoneOpen(IslandLayout.ZoneAt(t.x, t.y), zoneMask) && !ClearedTreeAt(t.x, t.y)) count++;
        return count;
    }

    /// <summary>Kesilmiş ağaç hücresi (AŞAMA 2 §3; etkin ada kaydından): doğa engeli kalkar, üretime sayılmaz.</summary>
    public static bool ClearedTreeAt(int c, int r) => IslandEconomy.IsTreeCleared(IslandSession.Save, c, r);

    /// <summary>Merkezden <see cref="Progression.Island.MeadowRadius"/> içindeki çayır hücresi sayısı.</summary>
    public static int MeadowNear(Vector2 centerCell)
    {
        int count = 0;
        var meadow = IslandLayout.Meadow;
        for (int c = meadow.xMin; c < meadow.xMax; c++)
            for (int r = meadow.yMin; r < meadow.yMax; r++)
                if (Vector2.Distance(centerCell, new Vector2(c + 0.5f, r + 0.5f)) <= Progression.Island.MeadowRadius) count++;
        return count;
    }

    public static Vector2 CenterCell(IslandLayout.BuildingKind kind, int col, int row)
    {
        var size = Size(kind);
        return new Vector2(col + size.x * 0.5f, row + size.y * 0.5f);
    }

    /// <summary>
    /// Yeni/taşınan bina için hücre denetimi (sol-üst hücre). <paramref name="movingId"/> taşınan binanın kimliği (çakışma saymaz).
    /// </summary>
    public static Reason Check(IslandLayout.BuildingKind kind, int col, int row, IList<IslandPlacedBuilding> placed, int zoneMask,
        string movingId, int keepLevel)
    {
        EnsureStatic();
        if (kind == IslandLayout.BuildingKind.Castle) return Reason.Fixed;
        var size = Size(kind);
        if (col < 0 || row < 0 || col + size.x > IslandLayout.Width || row + size.y > IslandLayout.Height) return Reason.OutOfMap;
        int height = IslandLayout.HeightAt(col, row);
        bool onMainland = true;
        for (int c = col; c < col + size.x; c++)
            for (int r = row; r < row + size.y; r++)
            {
                var s = staticReason[c, r];
                if (s == Reason.Water) return Reason.Water;
                if (s == Reason.Cliff) return Reason.Cliff;
                if (IslandLayout.HeightAt(c, r) != height) return Reason.Height;
                if (!ZoneOpen(IslandLayout.ZoneAt(c, r), zoneMask)) return Reason.Locked;
                if (s == Reason.Stairs) return Reason.Stairs;
                if (s == Reason.Lane) return Reason.Lane;
                if (s == Reason.Nature && !ClearedTreeAt(c, r)) return Reason.Nature;
                if (!mainland[c, r]) onMainland = false;
            }
        if (!onMainland && kind != IslandLayout.BuildingKind.Tower) return Reason.TowerOnly;
        var rect = new RectInt(col, row, size.x, size.y);
        if (Overlaps(rect, CastleRect)) return Reason.Overlap;
        int sameKind = 0;
        if (placed != null)
            foreach (var other in placed)
            {
                if (other == null || other.id == movingId) continue;
                if (other.kind == kind.ToString()) sameKind++;
                var otherKind = ParseKind(other.kind);
                if (Overlaps(rect, Rect(otherKind, other.col, other.row))) return Reason.Overlap;
            }
        if (movingId == null && sameKind >= Progression.Island.MaxCount(kind, keepLevel)) return Reason.Limit;
        var center = CenterCell(kind, col, row);
        if (kind == IslandLayout.BuildingKind.Lumber && TreesNear(center, zoneMask) < 1) return Reason.NeedTrees;
        if (kind == IslandLayout.BuildingKind.Pasture && (height > 1 || MeadowNear(center) < 1)) return height > 1 ? Reason.Height : Reason.NeedMeadow;
        if (kind == IslandLayout.BuildingKind.Barracks && height > 1) return Reason.Height;   // Kışla: yayla/ova (§5), zirve değil
        return Reason.None;
    }

    private static bool Overlaps(RectInt a, RectInt b) => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

    public static IslandLayout.BuildingKind ParseKind(string name) =>
        System.Enum.TryParse(name, out IslandLayout.BuildingKind kind) ? kind : IslandLayout.BuildingKind.House;

    /// <summary>Oyuncuya gösterilen neden metni.</summary>
    public static string ReasonText(Reason reason)
    {
        switch (reason)
        {
            case Reason.OutOfMap: return L.T("Harita dışı", "Out of the map");
            case Reason.Water: return L.T("Suya kurulmaz", "Cannot build on water");
            case Reason.Cliff: return L.T("Yar ve merdivene kurulmaz", "Not on cliffs or stairs");
            case Reason.Stairs: return L.T("Merdivenin önü açık kalmalı", "Keep the stairs clear");
            case Reason.Lane: return L.T("Yürüyüş şeridi: düşman yolu", "Enemy walking lane");
            case Reason.Nature: return L.T("Ağaç, altın taşı ya da çayır var", "Trees, gold stones or meadow in the way");
            case Reason.Height: return L.T("Zemin düz değil", "Ground is uneven");
            case Reason.Locked: return L.T("Bölge bulutların altında", "Zone is hidden under clouds");
            case Reason.Overlap: return L.T("Başka bina var", "Another building is there");
            case Reason.TowerOnly: return L.T("Adacığa yalnız kule kurulur", "Only towers on islets");
            case Reason.NeedTrees: return L.T("Yakında ağaç yok (4 birim)", "No trees nearby (4 u)");
            case Reason.NeedMeadow: return L.T("Yakında çayır yok (3 birim)", "No meadow nearby (3 u)");
            case Reason.Limit: return L.T("Bu bina için adet doldu", "Building limit reached");
            case Reason.Fixed: return L.T("Kale sabittir", "The keep is fixed");
            default: return "";
        }
    }
}
