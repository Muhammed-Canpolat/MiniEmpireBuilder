using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3. oturum (1.4): Ana Üs nesnelerinin GÖRÜNÜR alanı (sprite ağının sıkı sınırı; gölge, işaret, yazı ve uyku simgeleri hariç).
/// Doluluk ızgarası (BaseOccupancyGrid) hücre ayak izine bakar; çatılar ve büyük sprite'lar ayak izinden taşar. Yükleme onarımı ve
/// yerleştirme bu dikdörtgenlerin birbirine girmemesini de denetler: "birbirine değiyor/üst üste" görüntüsü kalmaz.
/// </summary>
public static class BaseVisualBounds
{
    /// <summary>Sıkı örtüşme payı (kahramanın durma yeri vb.).</summary>
    public const float Tolerance = 0.12f;
    /// <summary>
    /// 5. oturum (D62): iki YAPI görselinin izin verilen örtüşmesi = hücre kuralının payının iki katı (her biri komşu hücreye en fazla
    /// MarginX/MarginY kadar taşabilir). Yerleşim yalnız hücrelere bakar; bu denetim aynı kuralın görsel karşılığıdır.
    /// </summary>
    public const float StructureToleranceX = BaseOccupancyGrid.MarginX * 2f, StructureToleranceY = BaseOccupancyGrid.MarginY * 2f;

    private static bool Skip(SpriteRenderer sr, Transform root)
    {
        if (sr == null || !sr.enabled || sr.sprite == null || !sr.gameObject.activeInHierarchy) return true;
        // Yapının içindeki/çevresindeki karakterler (garnizon askeri, madenci, işçi: Animator'lı alt nesneler) yapının görseli değildir;
        // dolaştıkları için ölçümü oynatırlar. Kök nesnenin kendi animasyonu (dekor feneri, bayrak) sayılır.
        if (sr.transform != root && sr.GetComponentInParent<Animator>() != null && sr.GetComponentInParent<Animator>().transform != root) return true;
        if (sr.color.a < 0.35f) return true;
        for (var t = sr.transform; t != null && t != root.parent; t = t.parent)
        {
            string n = t.name;
            if (n.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("marker", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("zzz", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("sleep", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("badge", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("ring", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("pile", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("ghost", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (t.GetComponent<BuilderTaskMarker>() != null) return true;
        }
        return false;
    }

    /// <summary>Nesnenin görünür dikdörtgeni (dünya); görünür sprite yoksa boş (size 0).</summary>
    public static Rect Of(GameObject go)
    {
        if (go == null) return Rect.zero;
        bool any = false;
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (Skip(sr, go.transform)) continue;
            var verts = sr.sprite.vertices;   // sıkı ağ (saydam kenarlar hariç)
            var m = sr.transform.localToWorldMatrix;
            foreach (var v in verts)
            {
                Vector3 local = new Vector3(sr.flipX ? -v.x : v.x, sr.flipY ? -v.y : v.y, 0f);
                Vector3 w = m.MultiplyPoint3x4(local);
                xMin = Mathf.Min(xMin, w.x); xMax = Mathf.Max(xMax, w.x);
                yMin = Mathf.Min(yMin, w.y); yMax = Mathf.Max(yMax, w.y);
            }
            any = true;
        }
        return any ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : Rect.zero;
    }

    /// <summary>Tek renderer'ın sıkı dikdörtgeni (ad süzgeci yok: yerleştirme hayaleti için).</summary>
    public static Rect RendererRect(SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null) return Rect.zero;
        float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
        var m = sr.transform.localToWorldMatrix;
        foreach (var v in sr.sprite.vertices)
        {
            Vector3 w = m.MultiplyPoint3x4(new Vector3(sr.flipX ? -v.x : v.x, sr.flipY ? -v.y : v.y, 0f));
            xMin = Mathf.Min(xMin, w.x); xMax = Mathf.Max(xMax, w.x);
            yMin = Mathf.Min(yMin, w.y); yMax = Mathf.Max(yMax, w.y);
        }
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    /// <summary>Dikdörtgen, <paramref name="self"/> dışındaki herhangi bir Ana Üs nesnesinin görseline giriyor mu.</summary>
    public static bool OverlapsAny(Rect rect, GameObject self, GameObject alsoIgnore = null)
    {
        foreach (var (_, go) in CollectStructures())
        {
            if (go == null || go == self || go == alsoIgnore) continue;
            if (Overlap(rect, Of(go))) return true;
        }
        return false;
    }

    /// <summary>İki yapı görseli, hücre kuralının izin verdiğinden fazla iç içe mi (her iki eksende de).</summary>
    public static bool Overlap(Rect a, Rect b) => Overlap(a, b, StructureToleranceX, StructureToleranceY);

    /// <summary>İki görsel, verilen paylardan fazla iç içe mi (her iki eksende de).</summary>
    public static bool Overlap(Rect a, Rect b, float tolX, float tolY)
    {
        if (a.width <= 0f || b.width <= 0f) return false;
        float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return ox > tolX && oy > tolY;
    }

    /// <summary>
    /// Ana Üs'ün görsel olarak denetlenen nesneleri: binalar, inşaatçı kulübeleri, kahraman kampı ve (istenirse) dekorlar (ad, nesne).
    /// 5. oturum (D62): dekor görselleri birbirine ve çatılara binebilir (ağıl, çalı kümesi); yapı çakışma onarımına girmez.
    /// </summary>
    public static List<(string name, GameObject go)> Collect() => Collect(true);

    /// <summary>Yalnız yapılar (bina, kulübe, kamp): çakışma onarımı ve yerleşim denetimi.</summary>
    public static List<(string name, GameObject go)> CollectStructures() => Collect(false);

    private static List<(string name, GameObject go)> Collect(bool includeDecor)
    {
        var list = new List<(string, GameObject)>();
        var map = BaseMapManager.Instance;
        if (map != null)
            foreach (var obj in map.MapObjects)
            {
                if (obj == null || obj.gameObject == null || obj.isBeingRemoved) continue;
                bool building = obj.objectType == MapObjectType.Building && obj.buildingType.HasValue;
                if (!building && obj.objectType != MapObjectType.BuilderHut) continue;
                list.Add((building ? obj.buildingType.Value + ":" + obj.id : "hut:" + obj.id, obj.gameObject));
            }
        if (includeDecor && DecorPlacementController.Instance != null)
            foreach (var item in DecorPlacementController.Instance.Placed)
                if (item != null && item.Data != null) list.Add(("decor:" + item.Data.id + ":" + item.InstanceId, item.gameObject));
        var camp = GameObject.Find("HeroCamp");
        if (camp != null) list.Add(("camp", camp));
        return list;
    }

    /// <summary>Görsel olarak iç içe geçmiş çiftler (test ve hata ayıklama).</summary>
    public static List<string> Overlaps()
    {
        var all = CollectStructures();
        var rects = new List<Rect>();
        foreach (var (_, go) in all) rects.Add(Of(go));
        var result = new List<string>();
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                if (Overlap(rects[i], rects[j])) result.Add($"{all[i].name} {Fmt(rects[i])} × {all[j].name} {Fmt(rects[j])}");
        return result;
    }

    private static string Fmt(Rect r) => $"[{r.xMin:0.0},{r.yMin:0.0}–{r.xMax:0.0},{r.yMax:0.0}]";
}
