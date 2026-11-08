using System.Collections.Generic;
using UnityEngine;

// Crops transparent padding from the first idle sprite for character cards only.
public static class HeroPortraitSprite
{
    private static readonly Dictionary<Sprite, Sprite> cache = new Dictionary<Sprite, Sprite>();

    public static Sprite Get(Sprite source)
    {
        if (source == null) return null;
        if (cache.TryGetValue(source, out var cropped)) return cropped;
        try
        {
            var texture = source.texture;
            var pixels = texture.GetPixels32();
            var rect = source.rect;
            int left = Mathf.RoundToInt(rect.xMin);
            int bottom = Mathf.RoundToInt(rect.yMin);
            int right = Mathf.RoundToInt(rect.xMax);
            int top = Mathf.RoundToInt(rect.yMax);
            int minX = right, minY = top, maxX = left - 1, maxY = bottom - 1;
            for (int y = bottom; y < top; y++)
            for (int x = left; x < right; x++)
            {
                if (pixels[y * texture.width + x].a <= 10) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY) return cache[source] = source;
            minX = Mathf.Max(left, minX - 2); minY = Mathf.Max(bottom, minY - 2);
            maxX = Mathf.Min(right - 1, maxX + 2); maxY = Mathf.Min(top - 1, maxY + 2);
            var crop = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            cropped = Sprite.Create(texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            cropped.name = source.name + "_portrait";
            return cache[source] = cropped;
        }
        catch (UnityException)
        {
            return cache[source] = source;
        }
    }
}
