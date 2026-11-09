using UnityEngine;

/// <summary>
/// Elmas ikonu. Resources/UI/icon_gem varsa o kullanılır; yoksa kodla çizilmiş yüzeyli bir mücevher.
/// </summary>
public static class GemIcon
{
    private static Sprite sprite;

    public static Sprite Sprite
    {
        get
        {
            if (sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("UI/icon_gem");
            if (sprite == null) sprite = Draw();
            return sprite;
        }
    }

    private static Sprite Draw()
    {
        const int size = 48;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var outline = new Color32(0x1E, 0x3A, 0x5C, 255);
        var dark = new Color32(0x2E, 0x8F, 0xC9, 255);
        var mid = new Color32(0x56, 0xC7, 0xF0, 255);
        var light = new Color32(0xB8, 0xF0, 0xFF, 255);
        // Üstte düz taç (y 30–38), altta sivri uç (y 6).
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = x + 0.5f - size * 0.5f;
            float py = y + 0.5f;
            float halfWidth;
            if (py >= 30f && py <= 38f) halfWidth = 18f - (py - 30f) * 0.75f;  // taç
            else if (py >= 6f && py < 30f) halfWidth = (py - 6f) * 0.75f;      // gövde
            else halfWidth = -1f;
            Color32 c = new Color32(0, 0, 0, 0);
            if (halfWidth > 0f && Mathf.Abs(cx) <= halfWidth)
            {
                bool edge = Mathf.Abs(cx) > halfWidth - 1.6f || py > 36.6f || py < 7.4f;
                if (edge) c = outline;
                else if (py >= 30f) c = cx < -4f ? mid : cx > 4f ? dark : light;   // taç yüzeyleri
                else c = cx < -2f ? mid : cx > 5f ? dark : (cx < 2f ? light : mid);
                if (!edge && Mathf.Abs(py - 30f) < 0.8f) c = outline;             // taç çizgisi
            }
            tex.SetPixel(x, y, c);
        }
        // Parıltı
        for (int i = -2; i <= 2; i++)
        {
            tex.SetPixel(17 + i, 34, Color.white);
            tex.SetPixel(17, 34 + i, Color.white);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
