using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kodla çizilen yedek/özel görseller (katalog yokken ve tekneler için; ART_NEEDS planı: gövde + yelken).
/// Her sprite bir kez üretilir ve önbelleğe alınır. Hepsi 1 dünya birimi = N piksel olacak şekilde
/// ölçeklenmiştir (yorumlarda boyut belirtilir).
/// </summary>
public static class IslandArt
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    private static Sprite Make(string key, int w, int h, float ppu, System.Action<Color[], int, int> paint,
        Vector2? pivot = null)
    {
        if (cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
        var pixels = new Color[w * h];
        paint(pixels, w, h);
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = key };
        texture.SetPixels(pixels);
        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }

    private static void Fill(Color[] px, int w, int x0, int y0, int rw, int rh, Color c)
    {
        for (int y = y0; y < y0 + rh; y++)
        for (int x = x0; x < x0 + rw; x++)
            if (x >= 0 && x < w && y >= 0 && y * w + x < px.Length) px[y * w + x] = c;
    }

    private static void Disc(Color[] px, int w, int h, float cx, float cy, float r, Color c)
    {
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= r * r) px[y * w + x] = c;
    }

    /// <summary>1×1 birim beyaz kare.</summary>
    public static Sprite Square => Make("isl_square", 4, 4, 4f, (px, w, h) => Fill(px, w, 0, 0, w, h, Color.white));

    /// <summary>1 birim çaplı dolu daire.</summary>
    public static Sprite Circle => Make("isl_circle", 32, 32, 32f, (px, w, h) => Disc(px, w, h, 16f, 16f, 15.5f, Color.white));

    /// <summary>1 birim çaplı halka (yaklaşık 0,12 kalın).</summary>
    public static Sprite Ring => Make("isl_ring", 64, 64, 64f, (px, w, h) =>
    {
        Disc(px, w, h, 32f, 32f, 31.5f, Color.white);
        Disc(px, w, h, 32f, 32f, 24f, Color.clear);
    });

    /// <summary>Nişangâh: halka + artı (kilitli hedef).</summary>
    public static Sprite Crosshair => Make("isl_crosshair", 64, 64, 64f, (px, w, h) =>
    {
        Disc(px, w, h, 32f, 32f, 31.5f, Color.white);
        Disc(px, w, h, 32f, 32f, 26f, Color.clear);
        Fill(px, w, 30, 4, 4, 56, Color.white);
        Fill(px, w, 4, 30, 56, 4, Color.white);
        Fill(px, w, 22, 22, 20, 20, Color.clear);
    });

    /// <summary>Merdiven: 1×1 birim, yatay basamaklar.</summary>
    public static Sprite Stairs => Make("isl_stairs", 16, 16, 16f, (px, w, h) =>
    {
        Fill(px, w, 0, 0, w, h, new Color(0.62f, 0.52f, 0.38f));
        for (int step = 0; step < 4; step++)
        {
            Fill(px, w, 1, step * 4 + 2, w - 2, 2, new Color(0.84f, 0.74f, 0.55f));
            Fill(px, w, 1, step * 4, w - 2, 1, new Color(0.38f, 0.30f, 0.22f));
        }
    });

    /// <summary>Tekne: gövde (kahverengi) + direk + yelken. 1,5 × 1 birim (48×32 piksel); sola bakar.</summary>
    public static Sprite Boat(Progression.Island.BoatKind kind)
    {
        Color sail = kind == Progression.Island.BoatKind.Barge ? new Color(0.9f, 0.86f, 0.74f) : Color.white;
        Color hullColor = kind == Progression.Island.BoatKind.Barge ? new Color(0.36f, 0.24f, 0.16f)
            : new Color(0.55f, 0.36f, 0.2f);
        return Make("isl_boat_" + kind, 48, 32, 32f, (px, w, h) =>
        {
            Color dark = new Color(0.22f, 0.13f, 0.08f);
            // Gövde: alttan daralan yamuk
            for (int y = 2; y < 12; y++)
            {
                int inset = (12 - y) / 2;
                Fill(px, w, 4 + inset, y, w - 8 - inset * 2, 1, y == 11 ? dark : hullColor);
            }
            Fill(px, w, 6, 11, w - 12, 1, dark);
            Fill(px, w, 8, 4, w - 16, 1, new Color(0.7f, 0.5f, 0.3f));
            // Direk ve yelken
            Fill(px, w, 23, 12, 2, 17, dark);
            for (int y = 14; y < 29; y++)
            {
                int width = Mathf.Clamp((29 - y) + 4, 4, 17);
                Fill(px, w, 25, y, width, 1, sail);
            }
            Fill(px, w, 25, 14, 17, 1, new Color(0.75f, 0.75f, 0.75f));
            // Korsan bayrağı (küçük kırmızı)
            Fill(px, w, 23, 29, 5, 2, new Color(0.8f, 0.15f, 0.12f));
        }, new Vector2(0.5f, 0.3f));
    }

    /// <summary>Yedek birim: yuvarlak gövde + kafa, koyu kontur. 0,75 × 1 birim.</summary>
    public static Sprite Person => Make("isl_person", 24, 32, 32f, (px, w, h) =>
    {
        Color outline = new Color(0.1f, 0.08f, 0.1f);
        Disc(px, w, h, 12f, 11f, 10f, outline);
        Disc(px, w, h, 12f, 11f, 8.5f, Color.white);
        Disc(px, w, h, 12f, 24f, 6.5f, outline);
        Disc(px, w, h, 12f, 24f, 5.2f, new Color(0.96f, 0.82f, 0.66f));
    }, new Vector2(0.5f, 0.15f));

    /// <summary>Yedek ağaç / orman: koyu yeşil taç + gövde. 1×1,25 birim.</summary>
    public static Sprite Tree => Make("isl_tree", 32, 40, 32f, (px, w, h) =>
    {
        Fill(px, w, 14, 0, 4, 12, new Color(0.38f, 0.24f, 0.13f));
        Disc(px, w, h, 16f, 24f, 14f, new Color(0.12f, 0.33f, 0.17f));
        Disc(px, w, h, 13f, 27f, 9f, new Color(0.2f, 0.45f, 0.22f));
    }, new Vector2(0.5f, 0.1f));

    /// <summary>Yedek kale: gri-mavi duvar + kuleler + kapı. 3 × 2,6 birim (96×84).</summary>
    public static Sprite Castle => Make("isl_castle", 96, 84, 32f, (px, w, h) =>
    {
        Color wall = new Color(0.55f, 0.62f, 0.74f), dark = new Color(0.2f, 0.24f, 0.33f), roof = new Color(0.25f, 0.4f, 0.72f);
        Fill(px, w, 8, 0, 80, 46, wall);
        Fill(px, w, 8, 0, 80, 2, dark);
        for (int x = 8; x < 88; x += 10) Fill(px, w, x, 46, 6, 6, wall);
        Fill(px, w, 0, 0, 22, 70, wall);
        Fill(px, w, 74, 0, 22, 70, wall);
        Fill(px, w, 0, 70, 22, 4, roof);
        Fill(px, w, 74, 70, 22, 4, roof);
        for (int x = 0; x < 22; x += 8) { Fill(px, w, x, 74, 5, 6, wall); Fill(px, w, 74 + x, 74, 5, 6, wall); }
        Fill(px, w, 38, 0, 20, 26, dark);
        Fill(px, w, 40, 24, 16, 4, dark);
        Fill(px, w, 42, 52, 12, 20, roof);
        Fill(px, w, 46, 72, 2, 10, dark);
    }, new Vector2(0.5f, 0f));

    /// <summary>Ok: 0,6 × 0,08 birim, sağa bakar (kırmızıya boyanabilir).</summary>
    public static Sprite Arrow => Make("isl_arrow", 20, 3, 32f, (px, w, h) =>
    {
        Fill(px, w, 0, 1, w - 4, 1, Color.white);
        Fill(px, w, w - 5, 0, 5, 3, Color.white);
        Fill(px, w, 0, 0, 3, 3, new Color(0.9f, 0.9f, 0.9f));
    });
}
