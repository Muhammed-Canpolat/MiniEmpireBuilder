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

    public static void Register(Object owner, Rect area)
    {
        if (owner != null) Areas[owner] = area;
    }

    public static void Unregister(Object owner)
    {
        if (owner != null) Areas.Remove(owner);
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
        return point;
    }

    public static bool Blocked(Vector2 point, float radius) => (Resolve(point, radius) - point).sqrMagnitude > 0.0001f;
}
