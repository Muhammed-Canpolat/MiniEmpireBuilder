using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5. oturum (1.3): Ana Üs dekor/hayvan görsel ölçeğinin TEK kaynağı. Değer = sprite karesinin dünyadaki genişliği (birim; 1 hücre = 1 birim).
/// Ayak izinden (footprint) tamamen bağımsızdır: dekorlar 1×1'e geçtiğinde (4. oturum 2.1) görsel genişlik ayak izine bağlı
/// <c>visualWidth</c>'ten okunuyordu ve eski <c>visualScale</c> büyütmesi yok sayılıyordu; koyunlar %37, çalılar %50 boyuta küçülmüştü.
///
/// Önceki doğal boyutlar (2×2 / 3×3 döneminin formülü: ayak izi × 0,92 × visualScale): koyun 2 × 0,92 × 1,9 = 3,5; çalı 2 × 0,92 × 1,35 = 2,48;
/// kaya 2 × 0,92 = 1,84; ağaç 3 × 0,92 = 2,76. Aynı paketten aynı kare boyundaki eşler (Bush 2–4, Rock2–4, Tree2) aynı değeri alır; daha
/// sonra eklenenler (altın taşı, kaynak yığınları, kütük) Tiny Swords doğal boyunda (64 px = 1 birim) — kütük ağaçla aynı ölçekte.
/// Tabloda olmayan (emekli Cainos) dekorlar eski ayak izi formülünü kullanır (<see cref="DecorData.LegacyScale"/>).
/// </summary>
public static class DecorScale
{
    private static readonly Dictionary<string, float> FrameWidth = new Dictionary<string, float>
    {
        // hayvan
        { "sheep", 3.5f },
        // çalılar (Bush 1–4, 128 px kare)
        { "bush", 2.48f }, { "bush2", 2.48f }, { "bush3", 2.48f }, { "bush4", 2.48f },
        // kayalar (Rock1–4, 64 px)
        { "rock", 1.84f }, { "rock2", 1.84f }, { "rock3", 1.84f }, { "rock4", 1.84f },
        // ağaçlar (Tree1–2: 192×256, Tree3–4: 192×192 — eski oranları korunur) ve kütük (ağaçla aynı paket ölçeği)
        { "tree", 2.76f }, { "tree2", 2.76f }, { "tree3", 2.52f }, { "tree4", 2.52f }, { "stump", 2.76f },
        // kaynak/altın (Tiny Swords doğal boyu)
        { "gold_stone", 2f }, { "gold_stone_big", 2f }, { "gold_pile", 2f }, { "meat_pile", 1f }, { "wood_pile", 1f },
    };

    /// <summary>Tablodaki karenin dünya genişliği.</summary>
    public static bool TryGetFrameWidth(string id, out float width)
    {
        width = 0f;
        return !string.IsNullOrEmpty(id) && FrameWidth.TryGetValue(id, out width);
    }

    /// <summary>Dekorun kök ölçeği (oranı koruyan tek çarpan): tablo varsa oradan, yoksa eski formül.</summary>
    public static float For(DecorData data, Sprite visual)
    {
        if (data == null || visual == null) return 1f;
        if (TryGetFrameWidth(data.id, out float width)) return width / Mathf.Max(0.01f, visual.bounds.size.x);
        return data.LegacyScale(visual);
    }

    /// <summary>Bug 14: kimlikten türetilen kararlı çeşitlilik çarpanı, 1 ± <see cref="VarietyFraction"/> (unit: 0..1).</summary>
    public const float VarietyFraction = 0.08f;
    public static float Variety(float unit) => 1f + (Mathf.Clamp01(unit) * 2f - 1f) * VarietyFraction;

    private static Dictionary<string, DecorData> byId;

    /// <summary>Kimliğe göre dekor verisi (Resources/Decor, önbellekli); yoksa null.</summary>
    public static DecorData Find(string id)
    {
        if (byId == null)
        {
            var map = new Dictionary<string, DecorData>();
            foreach (var d in Resources.LoadAll<DecorData>("Decor"))
                if (d != null && !string.IsNullOrEmpty(d.id) && !map.ContainsKey(d.id)) map.Add(d.id, d);
            if (map.Count == 0) return null;   // henüz yüklenemedi: önbelleğe alma
            byId = map;
        }
        return id != null && byId.TryGetValue(id, out var data) ? data : null;
    }

    /// <summary>Tablodaki kimlikler (test).</summary>
    public static IEnumerable<string> Ids => FrameWidth.Keys;
}
