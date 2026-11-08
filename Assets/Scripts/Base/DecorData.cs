using UnityEngine;

[CreateAssetMenu(menuName = "Mini Empire/Decor")]
public sealed class DecorData : ScriptableObject
{
    public string id;
    public string displayName;
    public Vector2Int footprint = Vector2Int.one;
    public int cost;
    public int unlockMainBaseLevel = 1;
    public Sprite sprite;
    public bool animated;
    public RuntimeAnimatorController animatorController;
    // Resources/CustomBaseArt altındaki doku adı (çalışma anında sprite'a çevrilir); doluysa sprite yerine kullanılır.
    public string customArt;
    // Animasyon karelerinde boşluk çok olan görseller (Tiny Swords koyun, çalı) için büyütme.
    public float visualScale = 1f;

    /// <summary>Oyunda ve menüde gösterilecek görsel.</summary>
    public Sprite Visual
    {
        get
        {
            if (!string.IsNullOrEmpty(customArt))
            {
                var custom = BaseCustomArt.Get(customArt);
                if (custom != null) return custom;
            }
            return sprite;
        }
    }

    /// <summary>
    /// Oranı koruyan tek ölçek: genişlik footprint'e oturur; uzun görseller (ağaç, heykel)
    /// footprint'ten yüksek olabilir ama makul bir tavanla sınırlanır.
    /// </summary>
    public float ScaleFor(Sprite visual)
    {
        if (visual == null) return 1f;
        Vector2 size = visual.bounds.size;
        float scale = footprint.x * 0.92f * Mathf.Max(0.1f, visualScale) / Mathf.Max(0.01f, size.x);
        float maxHeight = footprint.y * 1.9f + 0.6f;
        if (size.y * scale > maxHeight) scale = maxHeight / Mathf.Max(0.01f, size.y);
        return scale;
    }

    /// <summary>Görselin alt kenarı footprint'in tabanına otursun diye merkezden dikey kayma.</summary>
    public float VerticalOffset(Sprite visual, float scale)
    {
        if (visual == null) return 0f;
        return -footprint.y * 0.5f + 0.06f - visual.bounds.min.y * scale;
    }
}
