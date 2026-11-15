using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7. oturum (D88): savaş haritası varlıkları. Bölge: Resources/BattleMaps/Region_N, seviye: Resources/BattleMaps/Levels/RN_LM.
/// Sefer aşaması kendi bölge/seviyesini; Sonsuz Sefer kat temasının bölgesini (tür döngüsüne göre seviye, boss katında 5.);
/// Ejder İstilası Bölge 6 boss yerleşimini kullanır. Varlık yoksa savaş eski (merkezde üs, kenarlardan düşman) düzenle kurulur.
/// </summary>
public static class BattleMapCatalog
{
    /// <summary>Testler/eski düzen karşılaştırması: false iken harita hiç kullanılmaz.</summary>
    public static bool Enabled = true;

    private static readonly Dictionary<int, BattleRegionMap> regions = new Dictionary<int, BattleRegionMap>();
    private static readonly Dictionary<(int, int), BattleLevelLayout> levels = new Dictionary<(int, int), BattleLevelLayout>();

    public static BattleRegionMap Region(int id)
    {
        if (!regions.TryGetValue(id, out var map) || map == null)
            regions[id] = map = Resources.Load<BattleRegionMap>($"BattleMaps/Region_{id}");
        return map;
    }

    public static BattleLevelLayout Level(int regionId, int level)
    {
        if (!levels.TryGetValue((regionId, level), out var layout) || layout == null)
            levels[(regionId, level)] = layout = Resources.Load<BattleLevelLayout>($"BattleMaps/Levels/R{regionId}_L{level}");
        return layout;
    }

    public static int LevelForKind(CampaignLevelKind kind) =>
        kind == CampaignLevelKind.Survive ? 1 : kind == CampaignLevelKind.DefendBase ? 2 : kind == CampaignLevelKind.DefendMine ? 3
        : kind == CampaignLevelKind.TimeTrial ? 4 : 5;

    /// <summary>Bu savaşın haritası (bölge, seviye). Sefer dışı (eski "Level N" savaşı) ya da varlık yoksa false.</summary>
    public static bool TryGetFor(out BattleRegionMap map, out BattleLevelLayout layout)
    {
        map = null; layout = null;
        if (!Enabled || !BattleSession.IsCampaign) return false;
        int regionId, level;
        if (BattleSession.IsEndless)
        {
            var theme = EndlessMode.Theme(BattleSession.Floor);
            regionId = theme != null ? theme.id : 1;
            level = EndlessMode.IsBossFloor(BattleSession.Floor) ? 5 : LevelForKind(BattleSession.Level?.kind ?? CampaignLevelKind.Survive);
        }
        else if (BattleSession.IsInvasion) { regionId = 6; level = 5; }
        else { regionId = BattleSession.RegionId; level = BattleSession.LevelNumber; }
        map = Region(regionId);
        layout = Level(regionId, level);
        return map != null && layout != null && layout.Routes.Length > 0;
    }
}

/// <summary>
/// 7. oturum (D88): bu savaşın arazisi (çalışma anı). Kara maskesi + sabit engeller (ağaç gövdesi, bölge yapıları) yürünebilirliği belirler;
/// <see cref="BattleWalkGrid"/>, kahraman, Çırak ve Lancer hareketi buradan okur. Harita yoksa her yer yürünebilir (eski davranış).
/// </summary>
public static class BattleTerrain
{
    public static BattleRegionMap Map { get; private set; }
    public static BattleLevelLayout Layout { get; private set; }
    public static BattleRoute[] Routes { get; private set; } = new BattleRoute[0];
    public static bool Active => Map != null;
    public static int Version { get; private set; }

    public static void Begin(BattleRegionMap map, BattleLevelLayout layout)
    {
        Map = map; Layout = layout;
        Routes = layout != null ? layout.Routes : new BattleRoute[0];
        Version++;
    }

    public static void Clear() { Map = null; Layout = null; Routes = new BattleRoute[0]; Version++; }

    /// <summary>Nokta kara üstünde mi (harita yoksa hep true). Köprü (haritanın üstü) yürünebilir sayılır.</summary>
    public static bool IsLand(Vector2 p)
    {
        if (Map == null) return true;
        if (p.y >= BattleMapGeometry.TopY - 0.05f) return Mathf.Abs(p.x) <= 1.1f && p.y <= BattleMapGeometry.TopY + 4f;
        var cell = BattleMapGeometry.CellOf(p);
        return Map.IsLandCell(cell.x, cell.y);
    }

    /// <summary>Yarıçaplı nokta (ayak) tamamen kara üstünde mi.</summary>
    public static bool Walkable(Vector2 p, float radius = 0.25f)
    {
        if (Map == null) return true;
        if (!IsLand(p)) return false;
        return IsLand(p + new Vector2(radius, 0f)) && IsLand(p - new Vector2(radius, 0f))
            && IsLand(p + new Vector2(0f, radius)) && IsLand(p - new Vector2(0f, radius));
    }

    /// <summary>
    /// Hareketi kara içinde tut: hedef nokta suya taşıyorsa eksen eksen kaydırır (kıyı boyunca süzülür), olmazsa önceki noktada kalır.
    /// </summary>
    public static Vector2 KeepOnLand(Vector2 from, Vector2 to, float radius = 0.25f)
    {
        if (Map == null || Walkable(to, radius)) return to;
        var slideX = new Vector2(to.x, from.y);
        if (Walkable(slideX, radius)) return slideX;
        var slideY = new Vector2(from.x, to.y);
        if (Walkable(slideY, radius)) return slideY;
        return Walkable(from, radius) ? from : NearestLand(from);
    }

    /// <summary>Su içinde kalmış noktaya en yakın kara (ışınlanma/itilme sonrası).</summary>
    public static Vector2 NearestLand(Vector2 p)
    {
        if (Map == null || Walkable(p)) return p;
        for (float r = 0.5f; r <= 8f; r += 0.5f)
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (Walkable(q)) return q;
            }
        return Layout != null ? Layout.basePosition + Vector2.down * 2.5f : p;
    }

    /// <summary>Noktanın en yakın yola uzaklığı (harita yoksa sonsuz).</summary>
    public static float DistanceToRoad(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var r in Routes) best = Mathf.Min(best, r.DistanceTo(p));
        return best;
    }

    /// <summary>Kara üstünde, yoldan en az <paramref name="roadClearance"/> uzak rastgele nokta (mekanikler: türbe, patlama…).</summary>
    public static bool RandomLandPoint(System.Func<float, float, float> range, float roadClearance, out Vector2 point, Rect? within = null)
    {
        var area = within ?? Rect.MinMaxRect(-6.5f, -4f, 6.5f, BattleMapGeometry.TopY - 2f);
        for (int i = 0; i < 60; i++)
        {
            var q = new Vector2(range(area.xMin, area.xMax), range(area.yMin, area.yMax));
            if (!Walkable(q, 0.6f)) continue;
            if (Routes.Length > 0 && DistanceToRoad(q) < roadClearance) continue;
            point = q;
            return true;
        }
        point = Vector2.zero;
        return false;
    }

    /// <summary>Kamera sınırı: harita + 1 birim deniz payı (köprü dahil).</summary>
    public static Rect CameraBounds
    {
        get
        {
            var b = BattleMapGeometry.Bounds;
            // 8. oturum (D99): yatayda KARANIN kenarı (eskiden ızgara + 0,5 → kahramanı izleyen kamera bir yanda 1–2 birim deniz şeridi ve
            // kıyıdaki yapının yarısını gösteriyordu). Ekran adadan genişse CameraFollow kamerayı adanın ortasına sabitler (iki yan eşit).
            float landMin = float.MaxValue, landMax = float.MinValue;
            if (Map != null)
                for (float y = b.yMin + 0.5f; y < b.yMax; y += 1f)
                    for (float x = b.xMin; x <= b.xMax; x += 0.25f)
                        if (IsLand(new Vector2(x, y))) { landMin = Mathf.Min(landMin, x); landMax = Mathf.Max(landMax, x); }
            if (landMin > landMax) { landMin = b.xMin - 0.5f; landMax = b.xMax + 0.5f; }
            return Rect.MinMaxRect(landMin, b.yMin - 0.5f, landMax, b.yMax + 2f);
        }
    }
}
