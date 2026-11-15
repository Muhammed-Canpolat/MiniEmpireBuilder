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
    // 4. oturum (2.1): görselin dünya genişliği (birim). >0 ise ayak izinden bağımsızdır: 1×1 ağaç doğal boyunda görünür, yalnız gövdesi
    // hücre kaplar. 0: eski davranış (genişlik ayak izine oturur).
    public float visualWidth;
    // 4. oturum (2.2): katalogdan kaldırıldı (Tiny Swords dışı). Kurulu olanlar yerinde kalır; SİL tam bedeli iade eder.
    public bool retired;

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
    /// Görsel ölçek: 5. oturum (1.3) ortak ölçek tablosundan (<see cref="DecorScale"/>), ayak izinden bağımsız.
    /// </summary>
    public float ScaleFor(Sprite visual) => DecorScale.For(this, visual);

    /// <summary>
    /// Tabloda olmayan (emekli) dekorların eski formülü: genişlik footprint'e oturur; uzun görseller footprint'ten yüksek olabilir
    /// ama makul bir tavanla sınırlanır. <see cref="visualWidth"/> doluysa o kullanılır.
    /// </summary>
    public float LegacyScale(Sprite visual)
    {
        if (visual == null) return 1f;
        Vector2 size = visual.bounds.size;
        if (visualWidth > 0f) return visualWidth / Mathf.Max(0.01f, size.x);
        float scale = footprint.x * 0.92f * Mathf.Max(0.1f, visualScale) / Mathf.Max(0.01f, size.x);
        float maxHeight = footprint.y * 1.9f + 0.6f;
        if (size.y * scale > maxHeight) scale = maxHeight / Mathf.Max(0.01f, size.y);
        return scale;
    }

    /// <summary>
    /// Görselin GÖRÜNEN alt kenarı footprint'in tabanına otursun diye merkezden dikey kayma.
    /// 8. oturum (D100, Ana Üs yerleşim hatasının kök nedeni): eskiden sprite'ın TAM ÇERÇEVESİNİN alt kenarı (<c>bounds.min.y</c>) kullanılıyordu.
    /// Tiny Swords çalı/koyun/altın taşı karelerinin altında saydam boşluk var; bu dekorlar dolu sayılan hücrenin 0,7–1,2 birim YUKARISINDA
    /// çiziliyordu → oyuncu dolu hücreyi boş görüyor (yerleşim "sığmıyor"), çizildiği hücre ise boş sayılıyordu. Artık sıkı ağın (saydam
    /// hariç) en alt noktası kullanılır; çizim ile doluluk aynı hücrededir (Session8BaseTests.O1).
    /// </summary>
    public float VerticalOffset(Sprite visual, float scale)
    {
        if (visual == null) return 0f;
        return -footprint.y * 0.5f + 0.06f - VisibleBottom(visual) * scale;
    }

    /// <summary>Sprite'ın görünen (saydam olmayan) alt kenarı, yerel birimde (sıkı ağ; yoksa çerçeve).</summary>
    public static float VisibleBottom(Sprite visual)
    {
        var vertices = visual.vertices;
        if (vertices == null || vertices.Length == 0) return visual.bounds.min.y;
        float min = float.MaxValue;
        foreach (var v in vertices) if (v.y < min) min = v.y;
        return min;
    }
}
