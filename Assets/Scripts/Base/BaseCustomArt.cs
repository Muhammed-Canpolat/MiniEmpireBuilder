using System.Collections.Generic;
using UnityEngine;

/// <summary>Loads the authored transparent art without stretching its empty margins.</summary>
public static class BaseCustomArt
{
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        if (Sprites.TryGetValue(name, out var cached) && cached != null) return cached;
        var texture = Resources.Load<Texture2D>("CustomBaseArt/" + name);
        if (texture == null) return null;
        texture.filterMode = FilterMode.Point;
        int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
        if (texture.isReadable)
        {
            var pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a >= 24)
                {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
        }
        Rect rect = maxX >= minX && maxY >= minY
            ? new Rect(Mathf.Max(0, minX - 2), Mathf.Max(0, minY - 2),
                Mathf.Min(texture.width - Mathf.Max(0, minX - 2), maxX - minX + 5),
                Mathf.Min(texture.height - Mathf.Max(0, minY - 2), maxY - minY + 5))
            : new Rect(0, 0, texture.width, texture.height);
        var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect);
        sprite.name = name;
        Sprites[name] = sprite;
        return sprite;
    }
}
