using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kodla çizilen yalnız ARAYÜZ ilkelleri (düğme dairesi, ilerleme halkası, dolgu karesi). v2: ada sahnesinde (ağaç, tekne, kale,
/// yar, merdiven, su, köpük, bulut, insan) kodla çizilmiş sprite YOKTUR; bunlar Tiny Swords / Kenney varlıklarından gelir.
/// Bu sınıf SpriteRenderer için kullanılmaz (ada sahnesinde isl_ dokusu sıfır olmalı).
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
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = key == "isl_square" ? FilterMode.Point : FilterMode.Bilinear, name = key };
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
    public static Sprite Circle => Make("isl_circle", 128, 128, 128f, (px, w, h) => Disc(px, w, h, 64f, 64f, 63.5f, Color.white));

    /// <summary>1 birim çaplı halka (yaklaşık 0,12 kalın).</summary>
    public static Sprite Ring => Make("isl_ring", 128, 128, 128f, (px, w, h) =>
    {
        Disc(px, w, h, 64f, 64f, 63.5f, Color.white);
        Disc(px, w, h, 64f, 64f, 48f, Color.clear);
    });
}
