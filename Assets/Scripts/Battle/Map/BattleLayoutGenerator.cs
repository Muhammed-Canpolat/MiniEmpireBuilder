using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7. oturum (D88, D90): savaş haritası yerleşim üreteci ve doğrulayıcısı (çalışma anı derlemesinde; editör aracı ve testler kullanır).
/// <list type="bullet">
/// <item>Kara maskesi: generate.py blob_mask'ın C# karşılığı (süperelips + gürültü), dikey 14×30; rotalar ve Ana Üs çevresi kara yapılır.</item>
/// <item>Rotalar: 1. seviye = Sefer Haritası'ndaki yolun AYNISI (generate.py map_region noktaları, dikeye ölçekli); diğerleri varyasyon.</item>
/// <item>Yuvalar: dönemeçlerin yanında, yoldan ≥2 birim, sırayla sağlı sollu; her yuva menziliyle en az bir dönemeci kapsar.</item>
/// </list>
/// </summary>
public static class BattleLayoutGenerator
{
    // ---- Ölçüler ----
    /// <summary>Rotanın Ana Üs ucundaki y (üs görselinin üst kenarı ≈1,6).</summary>
    public const float RoadEndY = 1.75f;
    /// <summary>Kule menzil merkezi yuvanın bu kadar üstünde (Topçu 2,0 boy / 2); kapsama Topçu menziliyle (5) ölçülür.</summary>
    public const float RangeCenterLift = 1.0f, CoverageRange = 5f;
    public const float SlotRoadClearance = BattleMapGeometry.RoadEdgeWidth * 0.5f + BattleMapGeometry.SlotRadius + 0.25f;
    public const float SlotSpacing = 2.8f;
    /// <summary>Ana Üs engel dikdörtgeni (BattleActorPresentation: x∈[-1,9, 1,9], y∈[-1,0, 1,6]).</summary>
    public static Rect BaseRect(Vector2 basePos) => Rect.MinMaxRect(basePos.x - 1.9f, basePos.y - 1.0f, basePos.x + 1.9f, basePos.y + 1.6f);

    /// <summary>Sefer Haritası'ndaki zikzak yönü: tek bölgelerde alttan önce SAĞA (generate.py side = 1 if region % 2 else -1).</summary>
    public static int Side(int regionId) => regionId % 2 == 1 ? 1 : -1;

    private static float YOf(float v) => BattleMapGeometry.TopY - v * (BattleMapGeometry.TopY - RoadEndY);

    // ==================== ROTA ŞABLONLARI ====================

    /// <summary>
    /// generate.py map_region: pts (alttan üste) = (CX, alt), (CX, alt−70), (CX+side·300, 610), (CX−side·280, 380), (CX+side·40, 200), (CX, üst+30);
    /// kara kutusu x∈[76,1484] (yarı genişlik 704), y∈[110,750]. Normalize (u = dx/704, v = (y−110)/640) ve savaşta üstten alta sırala.
    /// Yanal ölçek 8,5 birim (dikey adada zikzak karakteri korunsun diye haritadakinden %20 geniş).
    /// </summary>
    public static readonly Vector2[] MapRouteUV =
    {
        new Vector2(0f, 0.047f), new Vector2(0.057f, 0.141f), new Vector2(-0.398f, 0.422f),
        new Vector2(0.426f, 0.781f), new Vector2(0f, 0.891f), new Vector2(0f, 1f)
    };
    public const float MapLateralScale = 8.5f;

    /// <summary>Seviye şablonu: (x birim × side, v) noktaları; çatallı seviyede iki kol.</summary>
    public static List<Vector2[]> RouteTemplate(int regionId, int level)
    {
        int side = Side(regionId);
        var list = new List<Vector2[]>();
        Vector2[] Build(params float[] xv)
        {
            var pts = new List<Vector2> { BattleMapGeometry.Entry };
            for (int i = 0; i < xv.Length; i += 2) pts.Add(new Vector2(xv[i] * side, YOf(xv[i + 1])));
            return pts.ToArray();
        }
        switch (level)
        {
            case 1:
            {
                var pts = new List<Vector2> { BattleMapGeometry.Entry };
                foreach (var uv in MapRouteUV) pts.Add(new Vector2(uv.x * side * MapLateralScale, YOf(uv.y)));
                list.Add(pts.ToArray());
                break;
            }
            case 2:   // farklı dönemeçler: üç büyük kıvrım
                list.Add(Build(0f, 0.05f, 2.4f, 0.17f, -3.5f, 0.36f, 3.5f, 0.57f, -3.2f, 0.76f, 0f, 0.9f, 0f, 1f));
                break;
            case 3:   // ters başlangıç, sıkı S (maden orta kıvrımın dışında)
                list.Add(Build(0f, 0.05f, -2.8f, 0.2f, 3.6f, 0.43f, -3.5f, 0.68f, 1.4f, 0.86f, 0f, 0.95f, 0f, 1f));
                break;
            case 4:   // ikiye ayrılıp birleşen yol
                list.Add(Build(0f, 0.06f, 0f, 0.15f, -3.7f, 0.33f, -3.5f, 0.5f, 0f, 0.66f, 2.6f, 0.8f, 0f, 0.93f, 0f, 1f));
                list.Add(Build(0f, 0.06f, 0f, 0.15f, 3.7f, 0.33f, 3.5f, 0.5f, 0f, 0.66f, 2.6f, 0.8f, 0f, 0.93f, 0f, 1f));
                break;
            default:  // boss: en uzun, ek dönemeç
                list.Add(Build(0f, 0.04f, 2.8f, 0.12f, -3.6f, 0.25f, 3.6f, 0.4f, -3.6f, 0.55f, 3.6f, 0.7f, -2.8f, 0.84f, 0f, 0.94f, 0f, 1f));
                break;
        }
        return list;
    }

    // ==================== KARA MASKESİ ====================

    /// <summary>generate.py blob_mask: süperelips + satır/sütun gürültüsü, tek karoluk çıkıntılar temizlenir.</summary>
    public static bool[,] BlobMask(int cols, int rows, int seed, float power = 3.2f, float jitter = 0.18f)
    {
        var rnd = new System.Random(seed);
        float rx = cols / 2f - 0.5f, ry = rows / 2f - 0.5f, cx = (cols - 1) / 2f, cy = (rows - 1) / 2f;
        var noiseR = new float[rows]; var noiseC = new float[cols];
        for (int r = 0; r < rows; r++) noiseR[r] = (float)(rnd.NextDouble() * 2 - 1) * jitter;
        for (int c = 0; c < cols; c++) noiseC[c] = (float)(rnd.NextDouble() * 2 - 1) * jitter;
        var mask = new bool[cols, rows];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                mask[c, r] = Mathf.Pow(Mathf.Abs((c - cx) / rx), power) + Mathf.Pow(Mathf.Abs((r - cy) / ry), power) <= 1 + noiseR[r] + noiseC[c];
        Clean(mask);
        return mask;
    }

    private static void Clean(bool[,] mask)
    {
        int cols = mask.GetLength(0), rows = mask.GetLength(1);
        for (int pass = 0; pass < 2; pass++)
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int n = 0;
                    if (r + 1 < rows && mask[c, r + 1]) n++;
                    if (r - 1 >= 0 && mask[c, r - 1]) n++;
                    if (c + 1 < cols && mask[c + 1, r]) n++;
                    if (c - 1 >= 0 && mask[c - 1, r]) n++;
                    if (mask[c, r] && n <= 1) mask[c, r] = false;
                    else if (!mask[c, r] && n >= 3) mask[c, r] = true;
                }
    }

    /// <summary>Bölge maskesi: 14×30 blob (sütun 1–14, satır 0–29), tüm seviyelerin yolları + Ana Üs/köprü çevresi kara.</summary>
    public static string[] GenerateMask(int regionId, IEnumerable<BattleRoute> allRoutes)
    {
        int cols = BattleMapGeometry.Columns, rows = BattleMapGeometry.Rows;
        var blob = BlobMask(cols - 2, rows - 2, regionId * 13, 3.2f, 0.2f);
        var land = new bool[cols, rows];
        for (int r = 0; r < rows - 2; r++)
            for (int c = 0; c < cols - 2; c++) land[c + 1, r] = blob[c, r];
        void Force(Vector2 world, float radius)
        {
            for (int r = 0; r < rows - 2; r++)
                for (int c = 1; c < cols - 1; c++)
                    if (Vector2.Distance(BattleMapGeometry.CellCenter(c, r), world) <= radius) land[c, r] = true;
        }
        foreach (var route in allRoutes)
            for (float s = 0f; s <= route.Length; s += 0.4f) Force(route.PointAt(s), 1.6f);
        for (float x = -5f; x <= 5f; x += 0.5f)
            for (float y = -4.5f; y <= 4f; y += 0.5f) Force(new Vector2(x, y), 0.6f);
        // Köprü inişi (üst orta)
        for (int r = 0; r < 3; r++) { land[cols / 2 - 1, r] = true; land[cols / 2, r] = true; }
        Clean(land);
        var lines = new string[rows];
        for (int r = 0; r < rows; r++)
        {
            var chars = new char[cols];
            for (int c = 0; c < cols; c++) chars[c] = land[c, r] ? '#' : '.';
            lines[r] = new string(chars);
        }
        return lines;
    }

    // ==================== ARAZİ SORGULARI ====================

    public static bool IsLand(BattleRegionMap map, Vector2 world)
    {
        var cell = BattleMapGeometry.CellOf(world);
        return map.IsLandCell(cell.x, cell.y);
    }

    /// <summary>Daire tamamen kara üstünde ve kara kenarından en az <paramref name="margin"/> içeride mi.</summary>
    public static bool DiscOnLand(BattleRegionMap map, Vector2 c, float radius, float margin = 0.3f)
    {
        float r = radius + margin;
        if (!IsLand(map, c)) return false;
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6f;
            if (!IsLand(map, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r)) return false;
        }
        return true;
    }

    // ==================== YUVA / MADEN / KAHRAMAN ====================

    /// <summary>
    /// Yuva yeri geçerli mi: kara üstünde (uçurum kenarından içeride), yol kenarına ve kule GÖRSELİNE (yuvadan 2,3 birim yukarı uzanır)
    /// yol değmiyor, Ana Üs / maden / kahraman / başka yuvayla çakışmıyor. Hata yoksa null.
    /// </summary>
    public static string SlotProblem(BattleRegionMap map, BattleLevelLayout layout, Vector2 c, IList<Vector2> others = null, int selfIndex = -1)
    {
        if (!DiscOnLand(map, c, BattleMapGeometry.SlotRadius)) return "suya/kıyıya taşıyor";
        if (c.y > BattleMapGeometry.TopY - 2.5f) return "köprü girişine çok yakın";
        float road = layout.DistanceToRoad(c);
        if (road < SlotRoadClearance) return $"yola çok yakın ({road:0.00} < {SlotRoadClearance:0.00})";
        // Kule görseli yuvanın üstüne doğru uzanır (≈0,9 yarı genişlik, 2,3 boy): görsel yola binmesin.
        for (float dy = -0.3f; dy <= 2.2f; dy += 0.5f)
            for (float dx = -0.8f; dx <= 0.81f; dx += 0.4f)
                if (layout.DistanceToRoad(c + new Vector2(dx, dy)) < BattleMapGeometry.RoadEdgeWidth * 0.5f + 0.05f) return "kule görseli yola biniyor";
        var baseRect = BaseRect(layout.basePosition);
        if (BattleObstacles.DistanceTo(baseRect, c) < BattleMapGeometry.SlotRadius + 0.6f) return "Ana Üs'e çok yakın";
        if (layout.HasMine && Vector2.Distance(c, layout.minePosition) < 2.6f) return "madene çok yakın";
        if (Vector2.Distance(c, layout.heroSpawn) < 1.4f) return "kahraman doğuş noktasına çok yakın";
        if (others != null)
            for (int i = 0; i < others.Count; i++)
                if (i != selfIndex && Vector2.Distance(others[i], c) < SlotSpacing - 0.01f) return $"yuva {i + 1}'e çok yakın";
        return null;
    }

    /// <summary>Yuva menzil merkezinden (Topçu) en az bir dönemeç tepesi menzilde mi; menzildeki yol uzunluğu.</summary>
    public static float Coverage(BattleLevelLayout layout, Vector2 slot, out bool coversBend)
    {
        Vector2 center = slot + new Vector2(0f, RangeCenterLift);
        coversBend = false;
        float length = 0f;
        foreach (var route in layout.Routes)
        {
            for (int i = 0; i < route.Points.Length; i++)
                if (Vector2.Distance(route.Points[i], center) <= CoverageRange) length += BattleRoute.SampleStep;
            foreach (float s in route.Bends())
                if (Vector2.Distance(route.PointAt(s), center) <= CoverageRange - 0.3f) coversBend = true;
        }
        return length;
    }

    public static int SlotCountFor(int regionId, int level, int routeCount) =>
        CampaignCatalog.BattleSlotCount(regionId) + (routeCount > 1 ? 1 : 0);

    /// <summary>
    /// Yuvaları yerleştir: ana rota ilerleyişi pencerelere bölünür (üsse yakın pencereler sık), her pencerede bir önceki yuvanın
    /// TERS tarafında (gidiş yönüne göre sağ/sol) kapsaması en yüksek geçerli aday seçilir → zikzağı izleyen sağlı sollu dizi.
    /// </summary>
    public static Vector2[] GenerateSlots(BattleRegionMap map, BattleLevelLayout layout, int count)
    {
        var routes = layout.Routes;
        var main = routes[0];
        var candidates = new List<(Vector2 p, float progress, int side, float score)>();
        for (int ri = 0; ri < routes.Length; ri++)
        {
            var route = routes[ri];
            for (float s = 3f; s <= route.Length - 2.5f; s += 0.5f)
            {
                Vector2 p = route.PointAt(s), n = route.NormalAt(s);
                foreach (int side in new[] { 1, -1 })
                    foreach (float d in new[] { 2.15f, 2.5f, 2.9f, 3.3f })
                    {
                        Vector2 c = Round(p + n * side * d);   // kaydedilecek (yuvarlanmış) konum denetlenir
                        if (SlotProblem(map, layout, c) != null) continue;
                        float cover = Coverage(layout, c, out bool bend);
                        if (!bend) continue;
                        float progress = main.Project(c, out _) / main.Length;
                        candidates.Add((c, progress, side, cover - d * 0.8f));
                    }
            }
        }
        var chosen = new List<Vector2>();
        int lastSide = 0;
        // Pencereler: ilerleyiş 0,12 → 0,98; üsse yakın taraf biraz daha sık (kare kök).
        for (int w = 0; w < count; w++)
        {
            float a = Mathf.Lerp(0.12f, 0.98f, Mathf.Sqrt(w / (float)count));
            float b = Mathf.Lerp(0.12f, 0.98f, Mathf.Sqrt((w + 1) / (float)count));
            (Vector2 p, float progress, int side, float score)? best = null;
            float bestScore = float.MinValue;
            foreach (var cand in candidates)
            {
                if (cand.progress < a || cand.progress > b) continue;
                if (SlotProblem(map, layout, cand.p, chosen) != null) continue;
                float score = cand.score + (lastSide != 0 && cand.side != lastSide ? 6f : 0f);
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            if (best == null)
                foreach (var cand in candidates)
                {
                    if (SlotProblem(map, layout, cand.p, chosen) != null) continue;
                    if (cand.score > bestScore) { bestScore = cand.score; best = cand; }
                }
            if (best == null) break;
            chosen.Add(best.Value.p);
            lastSide = best.Value.side;
        }
        return chosen.ToArray();
    }

    /// <summary>Madeni Koru: maden ana yolun orta bölümünde, bir dönemecin yanında, yoldan 3–4 birim (düşman yoldan sapıp gider).</summary>
    public static Vector2 GenerateMine(BattleRegionMap map, BattleLevelLayout layout)
    {
        var main = layout.Routes[0];
        Vector2 best = layout.minePosition; float bestScore = float.MinValue;
        // 7. oturum denge (D95): maden yolun üsse yakın yarısında (ilerleyiş %55–80): köprüye yakın maden ilk düşmanlarla, kahraman ve
        // kuleler daha kurulmadan yıkılıyordu (B1-S3 23 sn'de kayıp).
        for (float s = main.Length * 0.55f; s <= main.Length * 0.8f; s += 0.5f)
        {
            Vector2 p = main.PointAt(s), n = main.NormalAt(s);
            foreach (int side in new[] { 1, -1 })
                foreach (float d in new[] { 3f, 3.4f, 3.8f })
                {
                    Vector2 c = p + n * side * d;
                    if (!DiscOnLand(map, c, 1.2f)) continue;
                    if (layout.DistanceToRoad(c) < 2.6f) continue;
                    if (BattleObstacles.DistanceTo(BaseRect(layout.basePosition), c) < 3f) continue;
                    float score = -Mathf.Abs(s / main.Length - 0.66f) * 10f - Mathf.Abs(c.x) * 0.2f;
                    if (score > bestScore) { bestScore = score; best = c; }
                }
        }
        return Round(best);
    }

    public static Vector2 GenerateHeroSpawn(BattleRegionMap map, BattleLevelLayout layout)
    {
        foreach (var c in new[] { new Vector2(2.6f, 2.8f), new Vector2(-2.6f, 2.8f), new Vector2(3f, -2.2f), new Vector2(-3f, -2.2f), new Vector2(0f, -2.6f) })
        {
            if (!DiscOnLand(map, c, 0.5f)) continue;
            if (layout.DistanceToRoad(c) < 1.3f) continue;
            if (BattleObstacles.DistanceTo(BaseRect(layout.basePosition), c) < 0.6f) continue;
            return c;
        }
        return new Vector2(0f, -2.6f);
    }

    private static Vector2 Round(Vector2 v) => new Vector2(Mathf.Round(v.x * 10f) / 10f, Mathf.Round(v.y * 10f) / 10f);

    // ==================== DOĞRULAMA ====================

    /// <summary>
    /// Seviye doğrulaması: (1) her rota köprüden başlar, Ana Üs'e ulaşır, kesintisiz ve hep kara üstünde; (2) yuva sayısı yeterli,
    /// hiçbir yuva yol/su/üs/maden/başka yuvayla çakışmaz, her yuva menziliyle en az bir dönemeci kapsar; (3) maden yolda değil ve
    /// yoldan yürünerek ulaşılır; (4) kahraman doğuş noktası karada. Boş liste = geçerli.
    /// </summary>
    public static List<string> Validate(BattleRegionMap map, BattleLevelLayout layout)
    {
        var errors = new List<string>();
        string tag = $"B{layout.regionId}-S{layout.levelNumber}";
        if (map == null || map.mask == null || map.mask.Length != BattleMapGeometry.Rows) { errors.Add(tag + ": bölge maskesi yok/yanlış"); return errors; }
        var routes = layout.Routes;
        if (routes.Length == 0) { errors.Add(tag + ": rota yok"); return errors; }
        var baseRect = BaseRect(layout.basePosition);
        for (int ri = 0; ri < routes.Length; ri++)
        {
            var route = routes[ri];
            if (route.Start.y < BattleMapGeometry.TopY) errors.Add($"{tag} rota {ri}: başlangıç haritanın üst köprüsünde değil ({route.Start})");
            if (BattleObstacles.DistanceTo(baseRect, route.End) > 0.4f) errors.Add($"{tag} rota {ri}: Ana Üs'e ulaşmıyor (son {route.End})");
            for (int i = 1; i < route.Points.Length; i++)
                if (Vector2.Distance(route.Points[i - 1], route.Points[i]) > BattleRoute.SampleStep * 1.6f) { errors.Add($"{tag} rota {ri}: kesinti ({route.Points[i]})"); break; }
            foreach (var p in route.Points)
                if (p.y < BattleMapGeometry.TopY - 0.1f && !IsLand(map, p)) { errors.Add($"{tag} rota {ri}: su üstünden geçiyor ({p})"); break; }
        }
        int need = SlotCountFor(layout.regionId, layout.levelNumber, routes.Length);
        if (layout.slots == null || layout.slots.Length < need) errors.Add($"{tag}: yuva sayısı {layout.slots?.Length ?? 0} < {need}");
        if (layout.slots != null)
            for (int i = 0; i < layout.slots.Length; i++)
            {
                string problem = SlotProblem(map, layout, layout.slots[i], layout.slots, i);
                if (problem != null) errors.Add($"{tag} yuva {i + 1} {layout.slots[i]}: {problem}");
                Coverage(layout, layout.slots[i], out bool bend);
                if (!bend) errors.Add($"{tag} yuva {i + 1}: menzilinde dönemeç yok");
            }
        if (layout.HasMine)
        {
            var m = layout.minePosition;
            if (!DiscOnLand(map, m, 1.0f)) errors.Add($"{tag}: maden suya taşıyor");
            float road = layout.DistanceToRoad(m);
            if (road < 2.2f) errors.Add($"{tag}: maden yolun üstünde ({road:0.00})");
            if (road > 4.6f) errors.Add($"{tag}: maden yoldan çok uzak ({road:0.00})");
            // Sapma rotası yürünebilir karadan: yoldaki en yakın noktadan madene kara üstünde düz/kırık yol (BFS).
            routes[0].Project(m, out _);
            Vector2 from = routes[0].PointAt(routes[0].Project(m, out _));
            if (!LandPathExists(map, from, m)) errors.Add($"{tag}: madene yoldan kara üstünden ulaşılamıyor");
        }
        if (!DiscOnLand(map, layout.heroSpawn, 0.4f, 0f)) errors.Add($"{tag}: kahraman doğuş noktası karada değil");
        return errors;
    }

    /// <summary>Kara hücreleri üstünde 4-komşu BFS (kahraman/düşman sapma rotası için kaba ulaşılabilirlik).</summary>
    public static bool LandPathExists(BattleRegionMap map, Vector2 a, Vector2 b)
    {
        var start = BattleMapGeometry.CellOf(a); var goal = BattleMapGeometry.CellOf(b);
        if (!map.IsLandCell(start.x, start.y) || !map.IsLandCell(goal.x, goal.y)) return false;
        var seen = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>(); queue.Enqueue(start);
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == goal) return true;
            foreach (var d in dirs)
            {
                var n = c + d;
                if (seen.Contains(n) || !map.IsLandCell(n.x, n.y)) continue;
                seen.Add(n); queue.Enqueue(n);
            }
        }
        return false;
    }
}
