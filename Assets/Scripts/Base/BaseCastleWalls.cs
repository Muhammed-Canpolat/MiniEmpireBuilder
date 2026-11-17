using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D108: Ana Üs arazisini PixelCastle duvar/köşe/kule/kapı parçalarıyla çeviren YALNIZ GÖRSEL katman.
/// Çit çizgisinden (<see cref="FenceRect"/>, <see cref="BaseWorldLayout.Buildable"/> ±0,5) türetilir; parçalar
/// dışa taşar, collider/Rigidbody/MapObjectTag yoktur (dokunuş yutmaz, kayda/ızgaraya girmez). Çarpışma eski
/// <c>FenceSegment</c> engelleriyle aynen sürer. <see cref="BaseMapManager.RefreshBuildableArea"/> her çağrıda yeniden kurar.
/// Ayrıca kadraj sınırı (<see cref="FrameBounds"/>) verir: zoom kapalı, kamera duvarları + biraz ormanı gösterir.
/// </summary>
public sealed class BaseCastleWalls : MonoBehaviour
{
    // Kaynak: Assets/PixelCastle (PPU 84, alt-orta pivot). Hepsine tek ölçek → ≈105 px/birim.
    public const float Scale = 0.8f;
    public const float SourcePpu = 84f;
    public const float WorldPerPx = Scale / SourcePpu;

    // Arka grup (üst + yan duvar, üst köşeler) içerideki her şeyin ARKASINDA: dekor 20−y (en düşük −15), ağaç 3,
    // bina 100−2y; zemin −20, çim lekesi −19. Ön grup (alt duvar, kapı, alt kuleler) birimlerin ÖNÜNDE (≤ 96).
    public const int BackWallOrder = -18, BackCornerOrder = -17, BackBannerOrder = -16;
    public const int FrontWallOrder = 110, FrontTowerOrder = 111, FrontBannerOrder = 112;

    // Parça ölçüleri (kaynak piksel). Tekrarlanan iki parça çalışma anında kenar payından kırpılır (kaynağın açık renkli
    // kenar çizgisi + yarı saydam kenar dikiş gibi görünüyordu): duvar x 9..175 (167 px), dikey duvar üstten 37..476 (440 px).
    // Adım = kırpılmış boy − 2 px bindirme (piksel yuvarlaması dahil) → boşluk/ince çizgi yok.
    private static readonly RectInt WallCrop = new RectInt(9, 0, 167, 144);
    private static readonly RectInt VertCrop = new RectInt(0, 16, 137, 440);   // doku koordinatı (alttan)
    private const float WallW = 167f, WallH = 144f, WallPitch = 165f;
    private const float VertW = 137f, VertH = 440f, VertPitch = 438f;
    private const float CornerLW = 322f, CornerLH = 396f, CornerLInnerX = 141f, CornerLArm = 183f;
    private const float CornerRW = 345f, CornerRH = 394f, CornerRInnerX = 205f, CornerRArm = 182f;
    private const float TowerW = 202f, TowerH = 233f, TowerSink = 20f;
    private const float GateW = 781f, GateH = 451f;
    /// <summary>Kapının çit çizgisinden içeri taşma payı (birim).</summary>
    public const float GateInward = 1f;
    private const float BannerH = 1.3f, BannerAspect = 48f / 128f, BannerSpacing = 3f;
    /// <summary>Kadraj payı: duvar dış sınırının ötesinde görünen orman (birim).</summary>
    public const float FrameSide = 1.2f, FrameTop = 1.3f, FrameBottom = 0.8f;

    public enum Part { Wall, WallVertical, CornerLeft, CornerRight, Tower, Gate, Banner }

    public struct Piece
    {
        public Part part;
        public Rect rect;      // dünya dikdörtgeni (görselin tam kaynak sınırı)
        public bool flipX;
        public int order;
        public bool front;     // alt duvar grubu
    }

    public static BaseCastleWalls Current { get; private set; }

    public Rect Fence { get; private set; }
    public Rect OuterBounds { get; private set; }
    public Rect FrameBounds { get; private set; }
    public int PieceCount { get; private set; }

    /// <summary>Bayrak açık ve katalogda duvar sprite'ları var.</summary>
    public static bool Enabled => FeatureConfig.Instance != null && FeatureConfig.Instance.pixelCastleWalls
        && MiniEmpireUiCatalog.WallsReady;

    /// <summary>Çit çizgisi: inşa edilebilir alanın yarım hücre dışı (bugünkü çitle aynı: 0,5…21,5 × 1,5…36,5).</summary>
    public static Rect FenceRect
    {
        get
        {
            var b = BaseWorldLayout.Buildable;
            return Rect.MinMaxRect(b.xMin - 0.5f, b.yMin - 0.5f, b.xMax + 0.5f, b.yMax + 0.5f);
        }
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // ==================== YERLEŞİM (saf hesap) ====================

    private static float Px(float px) => px * WorldPerPx;
    private static float Snap(float v) => Mathf.Round(v / WorldPerPx) * WorldPerPx;

    /// <summary>Verilen çit dikdörtgeni için tüm parçalar. Saf fonksiyon: aynı girdi → aynı çıktı.</summary>
    public static List<Piece> Layout(Rect fence)
    {
        var pieces = new List<Piece>(64);
        float L = fence.xMin, R = fence.xMax, B = fence.yMin, T = fence.yMax;

        // Üst köşeler: L parçanın iç köşesi çit köşesine oturur (dikey kolun iç kenarı, yatay kolun alt kenarı).
        var cornerL = new Rect(L - Px(CornerLInnerX), T + Px(CornerLArm) - Px(CornerLH), Px(CornerLW), Px(CornerLH));
        var cornerR = new Rect(R - Px(CornerRInnerX), T + Px(CornerRArm) - Px(CornerRH), Px(CornerRW), Px(CornerRH));

        // Alt köşe kuleleri: yan duvarın orta çizgisinde, alt duvarın biraz altında.
        float sideHalf = Px(VertW) * 0.5f;
        float wallBottom = B - Px(WallH);
        float towerBottom = wallBottom - Px(TowerSink);
        var towerL = new Rect(L - sideHalf - Px(TowerW) * 0.5f, towerBottom, Px(TowerW), Px(TowerH));
        var towerR = new Rect(R + sideHalf - Px(TowerW) * 0.5f, towerBottom, Px(TowerW), Px(TowerH));

        // Kapı: alt kenarın ortasında, içeri en çok GateInward taşar.
        var gate = new Rect(fence.center.x - Px(GateW) * 0.5f, B + GateInward - Px(GateH), Px(GateW), Px(GateH));

        // Üst duvar: köşe kollarının uç kulelerinin altından başlar/biter.
        HorizontalRun(pieces, cornerL.xMax - 0.55f, cornerL.xMax - 0.25f, cornerR.xMin + 0.25f, cornerR.xMin + 0.55f,
            T, BackWallOrder, false);
        // Yan duvarlar: alttaki kulenin içinden üst köşenin dikey kolunun içine.
        VerticalRun(pieces, L - Px(VertW), towerL.yMin + 0.6f, towerL.yMin + 1.2f, cornerL.yMin + 0.35f, cornerL.yMin + 0.9f, false);
        VerticalRun(pieces, R, towerR.yMin + 0.6f, towerR.yMin + 1.2f, cornerR.yMin + 0.35f, cornerR.yMin + 0.9f, true);
        // Alt duvar: kapının iki yanında iki ayrı açıklık (kule merkezinden kapı kulesinin içine).
        HorizontalRun(pieces, towerL.center.x - 0.2f, towerL.center.x + 0.45f, gate.xMin + 0.35f, gate.xMin + 0.8f,
            wallBottom, FrontWallOrder, true);
        HorizontalRun(pieces, gate.xMax - 0.8f, gate.xMax - 0.35f, towerR.center.x - 0.45f, towerR.center.x + 0.2f,
            wallBottom, FrontWallOrder, true);

        pieces.Add(new Piece { part = Part.CornerLeft, rect = SnapRect(cornerL), order = BackCornerOrder });
        pieces.Add(new Piece { part = Part.CornerRight, rect = SnapRect(cornerR), order = BackCornerOrder });
        pieces.Add(new Piece { part = Part.Tower, rect = SnapRect(towerL), order = FrontTowerOrder, front = true });
        pieces.Add(new Piece { part = Part.Tower, rect = SnapRect(towerR), order = FrontTowerOrder, front = true });
        pieces.Add(new Piece { part = Part.Gate, rect = SnapRect(gate), order = FrontTowerOrder, front = true });

        // Sancaklar: üst duvarda eşit aralıklı (köşe kulelerinden uzak), altta kapının iki yanındaki açıklığın ortasında.
        float bw = BannerH * BannerAspect;
        float a = cornerL.xMax + 0.4f, b = cornerR.xMin - 0.4f;
        int count = Mathf.Max(1, Mathf.RoundToInt((b - a) / BannerSpacing));
        for (int i = 0; i < count; i++)
        {
            float x = a + (b - a) * (i + 0.5f) / count;
            pieces.Add(Banner(x, T + Px(WallH) - 0.05f - BannerH * 0.5f, bw, BackBannerOrder, false));
        }
        pieces.Add(Banner((towerL.xMax + gate.xMin) * 0.5f, B - 0.05f - BannerH * 0.5f, bw, FrontBannerOrder, true));
        pieces.Add(Banner((gate.xMax + towerR.xMin) * 0.5f, B - 0.05f - BannerH * 0.5f, bw, FrontBannerOrder, true));
        return pieces;
    }

    private static Piece Banner(float cx, float cy, float w, int order, bool front)
        => new Piece { part = Part.Banner, rect = SnapRect(new Rect(cx - w * 0.5f, cy - BannerH * 0.5f, w, BannerH)), order = order, front = front };

    private static Rect SnapRect(Rect r) => new Rect(Snap(r.xMin), Snap(r.yMin), r.width, r.height);

    /// <summary>
    /// Tekrarlı açıklık: başlangıç [sMin,sMax], bitiş [eMin,eMax] penceresinde (uçlar komşu kule/köşenin altında gizli).
    /// Adımın tam katı pencereye sığıyorsa ölçek 1 (germe yok); sığmıyorsa en yakın parça sayısıyla hafif germe.
    /// </summary>
    internal static void Run(float sMin, float sMax, float eMin, float eMax, float pitch,
        out int n, out float start, out float scale)
    {
        float lmin = eMin - sMax, lmax = eMax - sMin;
        int lo = Mathf.Max(1, Mathf.CeilToInt(lmin / pitch - 1e-4f));
        if (lo * pitch <= lmax + 1e-4f)
        {
            n = lo;
            float length = n * pitch;
            float from = Mathf.Max(sMin, eMin - length), to = Mathf.Min(sMax, eMax - length);
            start = (from + to) * 0.5f;
            scale = 1f;
            return;
        }
        float mid = (lmin + lmax) * 0.5f;
        n = Mathf.Max(1, Mathf.RoundToInt(mid / pitch));
        start = (sMin + sMax) * 0.5f;
        scale = mid / (n * pitch);
    }

    private static void HorizontalRun(List<Piece> pieces, float sMin, float sMax, float eMin, float eMax,
        float bottom, int order, bool front)
    {
        float pitch = Px(WallPitch), width = Px(WallW);
        Run(sMin, sMax, eMin, eMax, pitch, out int n, out float start, out float scale);
        float margin = (width - pitch) * 0.5f * scale;
        for (int i = 0; i < n; i++)
        {
            float x = start + i * pitch * scale - margin;
            pieces.Add(new Piece { part = Part.Wall, rect = SnapRect(new Rect(x, bottom, width * scale, Px(WallH))), order = order, front = front });
        }
    }

    private static void VerticalRun(List<Piece> pieces, float xMin, float sMin, float sMax, float eMin, float eMax, bool flip)
    {
        float pitch = Px(VertPitch), height = Px(VertH);
        Run(sMin, sMax, eMin, eMax, pitch, out int n, out float start, out float scale);
        float margin = (height - pitch) * 0.5f * scale;
        for (int i = 0; i < n; i++)
        {
            float y = start + i * pitch * scale - margin;
            pieces.Add(new Piece { part = Part.WallVertical, rect = SnapRect(new Rect(xMin, y, Px(VertW), height * scale)), flipX = flip, order = BackWallOrder });
        }
    }

    /// <summary>Sancaklar hariç tüm parçaların dış sınırı.</summary>
    public static Rect OuterOf(List<Piece> pieces)
    {
        bool any = false;
        Rect outer = default;
        foreach (var p in pieces)
        {
            if (p.part == Part.Banner) continue;
            outer = any ? Rect.MinMaxRect(Mathf.Min(outer.xMin, p.rect.xMin), Mathf.Min(outer.yMin, p.rect.yMin),
                Mathf.Max(outer.xMax, p.rect.xMax), Mathf.Max(outer.yMax, p.rect.yMax)) : p.rect;
            any = true;
        }
        return outer;
    }

    /// <summary>Kadraj: duvar dış sınırı + orman payı.</summary>
    public static Rect FrameOf(Rect outer)
        => Rect.MinMaxRect(outer.xMin - FrameSide, outer.yMin - FrameBottom, outer.xMax + FrameSide, outer.yMax + FrameTop);

    /// <summary>Bugünkü çit için planlanan duvar dış sınırı (sahne kurulmadan; dış dekoru itmek için).</summary>
    public static Rect PlannedOuter => OuterOf(Layout(FenceRect));

    // ==================== KURULUM ====================

    public static BaseCastleWalls Build(Transform parent, Rect fence)
    {
        var catalog = MiniEmpireUiCatalog.Instance;
        if (catalog == null) return null;
        var go = new GameObject("CastleWalls");
        go.transform.SetParent(parent, false);
        var walls = go.AddComponent<BaseCastleWalls>();
        var pieces = Layout(fence);
        int index = 0;
        foreach (var piece in pieces)
        {
            Sprite sprite = SpriteFor(catalog, piece.part);
            if (piece.part == Part.Wall) sprite = Trimmed(sprite, WallCrop, ref trimmedWall);
            else if (piece.part == Part.WallVertical) sprite = Trimmed(sprite, VertCrop, ref trimmedVertical);
            if (sprite == null) continue;
            var child = new GameObject(piece.part + "_" + index++);
            child.transform.SetParent(go.transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = piece.flipX;
            renderer.sortingOrder = piece.order;
            Bounds b = sprite.bounds;
            float sx = piece.rect.width / Mathf.Max(0.0001f, b.size.x);
            float sy = piece.rect.height / Mathf.Max(0.0001f, b.size.y);
            float cx = piece.flipX ? -b.center.x : b.center.x;
            // Aynı sıradaki bindiren parçalar: aşağıdaki (ve sağdaki) önde → kararlı örtüşme.
            float z = (piece.rect.yMin + piece.rect.xMin * 0.01f) * 0.001f;
            child.transform.position = new Vector3(piece.rect.center.x - cx * sx, piece.rect.center.y - b.center.y * sy, z);
            child.transform.localScale = new Vector3(sx, sy, 1f);
        }
        walls.Fence = fence;
        walls.PieceCount = index;
        walls.OuterBounds = OuterOf(pieces);
        walls.FrameBounds = FrameOf(walls.OuterBounds);
        Current = walls;
        return walls;
    }

    private static Sprite trimmedWall, trimmedVertical;

    /// <summary>Kaynak sprite'ın (atlasta da) kırpılmış kopyası; oturum boyunca bir kez üretilir. Başarısızsa kaynak.</summary>
    private static Sprite Trimmed(Sprite source, RectInt crop, ref Sprite cache)
    {
        if (source == null) return null;
        if (cache != null && cache.texture == source.texture) return cache;
        try
        {
            Rect tr = source.textureRect;
            Vector2 offset = source.textureRectOffset;
            var rect = new Rect(tr.x + crop.x - offset.x, tr.y + crop.y - offset.y, crop.width, crop.height);
            if (rect.xMin < tr.xMin - 0.5f || rect.yMin < tr.yMin - 0.5f || rect.xMax > tr.xMax + 0.5f || rect.yMax > tr.yMax + 0.5f)
                return source;
            cache = Sprite.Create(source.texture, rect, new Vector2(0.5f, 0f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cache.name = source.name + "_trim";
            return cache;
        }
        catch (System.Exception)
        {
            return source;   // sıkı paketlenmiş atlas vb.: tam sprite kırpılmış dikdörtgene oturur
        }
    }

    private static Sprite SpriteFor(MiniEmpireUiCatalog c, Part part)
    {
        switch (part)
        {
            case Part.Wall: return c.wallCrenellated;
            case Part.WallVertical: return c.wallVertical;
            case Part.CornerLeft: return c.cornerOuterLeft;
            case Part.CornerRight: return c.cornerOuterRight;
            case Part.Tower: return c.towerSmallClosed;
            case Part.Gate: return c.gatehouseDoors;
            default: return c.bannerLeft;
        }
    }

    // ==================== KADRAJ (bug 13: Ana Üs'te zoom yok) ====================

    /// <summary>Duvarlar kuruluysa sabit kadraj sınırı.</summary>
    public static bool TryGetFrame(out Rect frame)
    {
        var walls = Current;
        if (walls != null && walls.isActiveAndEnabled) { frame = walls.FrameBounds; return true; }
        frame = default;
        return false;
    }

    /// <summary>Sınırın tamamını gösteren en küçük ortografik boy.</summary>
    public static float FitOrtho(Rect frame, float aspect)
        => Mathf.Max(frame.width / (2f * Mathf.Max(0.01f, aspect)), frame.height * 0.5f);

    /// <summary>Kamerayı sabit kadraja oturtur (değişiklik yoksa dokunmaz). Duvar yoksa false.</summary>
    public static bool ApplyFraming(Camera camera)
    {
        if (camera == null || !TryGetFrame(out Rect frame)) return false;
        float size = FitOrtho(frame, camera.aspect);
        if (!Mathf.Approximately(camera.orthographicSize, size)) camera.orthographicSize = size;
        Vector3 p = camera.transform.position;
        if (!Mathf.Approximately(p.x, frame.center.x) || !Mathf.Approximately(p.y, frame.center.y))
            camera.transform.position = new Vector3(frame.center.x, frame.center.y, p.z);
        return true;
    }
}
