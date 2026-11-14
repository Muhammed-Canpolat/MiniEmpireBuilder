using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Savaşta yürünemeyen alanlar (Ana Üs, kuleler, Manastır). Kahraman ve Çırak fizik
/// kullanmadan konumla hareket ettiği için engelleri bu kayıttan okuyup dışarı itilir.
/// Sahibi yok edilince alan kendiliğinden temizlenir.
/// </summary>
public static class BattleObstacles
{
    private static readonly Dictionary<Object, Rect> Areas = new Dictionary<Object, Rect>();
    private static readonly List<Object> Stale = new List<Object>();

    /// <summary>4. oturum (1.1): alan eklenince/kalkınca artar (BattleWalkGrid rotaları yeniler).</summary>
    public static int Version { get; private set; }

    public static void Register(Object owner, Rect area)
    {
        if (owner == null) return;
        if (!Areas.TryGetValue(owner, out var old) || old != area) Version++;
        Areas[owner] = area;
    }

    public static void Unregister(Object owner)
    {
        if (owner != null && Areas.Remove(owner)) Version++;
    }

    /// <summary>Canlı engel alanları (sahibi yok edilenler atlanır).</summary>
    public static IEnumerable<Rect> All()
    {
        foreach (var pair in Areas) if (pair.Key != null) yield return pair.Value;
    }

    /// <summary>Sahibin kayıtlı alanı (düşmanın vuruş mesafesi bu alanın kenarından ölçülür).</summary>
    public static bool TryGet(Object owner, out Rect area)
    {
        area = default;
        return owner != null && Areas.TryGetValue(owner, out area);
    }

    /// <summary>Noktanın dikdörtgene en kısa uzaklığı (içindeyse 0).</summary>
    public static float DistanceTo(Rect r, Vector2 p)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
        float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static readonly string[] SkipNames = { "shadow", "health", "bar", "label", "ring", "glow", "marker", "ghost", "smoke", "fire",
        "spark", "fx", "arrow", "projectile", "rubble", "ruin", "crossfade", "dust", "zone", "range", "indicator", "badge" };

    /// <summary>Yapının görünür (sıkı) dikdörtgeni: kök + alt parçalar; can barı, gölge, efekt ve garnizon karakterleri hariç.</summary>
    public static Rect VisualRect(GameObject go)
    {
        if (go == null) return Rect.zero;
        bool any = false;
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (!sr.enabled || sr.sprite == null || sr.color.a < 0.35f) continue;
            if (sr.transform != go.transform)
            {
                var anim = sr.GetComponentInParent<Animator>();
                if (anim != null && anim.transform != go.transform) continue;   // garnizon askeri vb.
            }
            bool skip = false;
            for (var t = sr.transform; t != null && t != go.transform && !skip; t = t.parent)   // kökün adı denetlenmez
            {
                string n = t.name.ToLowerInvariant();
                foreach (var k in SkipNames) if (n.Contains(k)) { skip = true; break; }
            }
            if (skip) continue;
            var m = sr.transform.localToWorldMatrix;
            foreach (var v in sr.sprite.vertices)
            {
                Vector3 w = m.MultiplyPoint3x4(new Vector3(sr.flipX ? -v.x : v.x, sr.flipY ? -v.y : v.y, 0f));
                xMin = Mathf.Min(xMin, w.x); xMax = Mathf.Max(xMax, w.x);
                yMin = Mathf.Min(yMin, w.y); yMax = Mathf.Max(yMax, w.y);
            }
            any = true;
        }
        return any ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : Rect.zero;
    }

    /// <summary>
    /// 5. oturum (1.2): yapının yürünemez alanı GÖRSELİNDEN türetilir (eskiden her yapı görseli ne olursa olsun sabit 1,25×1 birimdi;
    /// "Madeni Koru" madeni ve büyük kuleler bunun çok dışına taşıyor, kahraman/çırak görselin içinden geçiyordu). Karakterler yapıların
    /// üstünde çizildiğinden görselin tamamı kapatılır: yanlardan %8, tepeden %10 pay; taban, eski alanın tabanıyla birleşir.
    /// </summary>
    public static Rect FromVisual(GameObject go, Rect fallback)
    {
        var v = VisualRect(go);
        if (v.width <= 0.05f || v.height <= 0.05f) return fallback;
        var shrunk = Rect.MinMaxRect(v.xMin + v.width * 0.08f, v.yMin, v.xMax - v.width * 0.08f, v.yMax - v.height * 0.1f);
        return Rect.MinMaxRect(Mathf.Min(shrunk.xMin, fallback.xMin), Mathf.Min(shrunk.yMin, fallback.yMin),
            Mathf.Max(shrunk.xMax, fallback.xMax), Mathf.Max(shrunk.yMax, fallback.yMax));
    }

    /// <summary>Merkezi verilen, tabanı pivotun biraz altında başlayan bina alanı.</summary>
    public static Rect Footprint(Vector2 center, float width, float below, float above) =>
        new Rect(center.x - width * 0.5f, center.y - below, width, below + above);

    /// <summary>Yarıçaplı bir noktayı tüm engellerin dışına iter (en kısa yönde).</summary>
    public static Vector2 Resolve(Vector2 point, float radius)
    {
        Stale.Clear();
        foreach (var pair in Areas)
        {
            if (pair.Key == null) { Stale.Add(pair.Key); continue; }
            Rect r = pair.Value;
            float cx = Mathf.Clamp(point.x, r.xMin, r.xMax);
            float cy = Mathf.Clamp(point.y, r.yMin, r.yMax);
            Vector2 delta = point - new Vector2(cx, cy);
            float distance = delta.magnitude;
            if (distance >= radius) continue;
            if (distance > 0.0001f)
            {
                point = new Vector2(cx, cy) + delta / distance * radius;
                continue;
            }
            // Nokta alanın içinde: en yakın kenardan dışarı çıkar.
            float toLeft = point.x - r.xMin, toRight = r.xMax - point.x;
            float toBottom = point.y - r.yMin, toTop = r.yMax - point.y;
            float min = Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toBottom, toTop));
            if (min == toLeft) point.x = r.xMin - radius;
            else if (min == toRight) point.x = r.xMax + radius;
            else if (min == toBottom) point.y = r.yMin - radius;
            else point.y = r.yMax + radius;
        }
        foreach (var key in Stale) Areas.Remove(key);
        if (Stale.Count > 0) Version++;
        return point;
    }

    public static bool Blocked(Vector2 point, float radius) => (Resolve(point, radius) - point).sqrMagnitude > 0.0001f;
}
