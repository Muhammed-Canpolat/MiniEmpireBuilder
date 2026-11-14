using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7. oturum (D88): sefer savaşı rotası. Kontrol noktalarından (Catmull-Rom, sefer haritası çizimiyle aynı eğri) sık örneklenmiş
/// çoklu çizgi; uzunluk boyunca konum (<see cref="PointAt"/>), en yakın nokta izdüşümü (<see cref="Project"/>), dönemeçler.
/// Düşmanlar bu çizgiyi izler; yuva üreteci ve doğrulama testi aynı sınıfı kullanır.
/// </summary>
public sealed class BattleRoute
{
    public const float SampleStep = 0.2f;

    public readonly Vector2[] Points;
    private readonly float[] cumulative;
    public float Length => cumulative[cumulative.Length - 1];
    public Vector2 Start => Points[0];
    public Vector2 End => Points[Points.Length - 1];

    public BattleRoute(IList<Vector2> controlPoints)
    {
        var dense = CatmullRom(controlPoints, 24);
        Points = Resample(dense, SampleStep).ToArray();
        cumulative = new float[Points.Length];
        for (int i = 1; i < Points.Length; i++) cumulative[i] = cumulative[i - 1] + Vector2.Distance(Points[i - 1], Points[i]);
    }

    /// <summary>generate.py'deki catmull() ile aynı: uç noktalar tekrarlanır, her parça <paramref name="steps"/> adım.</summary>
    public static List<Vector2> CatmullRom(IList<Vector2> points, int steps)
    {
        var output = new List<Vector2>();
        if (points == null || points.Count == 0) return output;
        if (points.Count < 3) { output.AddRange(points); return output; }
        var pts = new List<Vector2> { points[0] };
        pts.AddRange(points);
        pts.Add(points[points.Count - 1]);
        for (int i = 1; i < pts.Count - 2; i++)
        {
            Vector2 p0 = pts[i - 1], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2];
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                output.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        output.Add(points[points.Count - 1]);
        return output;
    }

    private static List<Vector2> Resample(List<Vector2> dense, float step)
    {
        var output = new List<Vector2>();
        if (dense.Count == 0) return output;
        output.Add(dense[0]);
        float carry = 0f;
        for (int i = 1; i < dense.Count; i++)
        {
            Vector2 a = dense[i - 1], b = dense[i];
            float segment = Vector2.Distance(a, b);
            float pos = step - carry;
            while (pos <= segment)
            {
                output.Add(Vector2.Lerp(a, b, pos / Mathf.Max(1e-5f, segment)));
                pos += step;
            }
            carry = segment - (pos - step);
        }
        if (Vector2.Distance(output[output.Count - 1], dense[dense.Count - 1]) > 0.02f) output.Add(dense[dense.Count - 1]);
        return output;
    }

    /// <summary>Rota başından <paramref name="s"/> birim ilerideki nokta (uçlarda kenetlenir).</summary>
    public Vector2 PointAt(float s)
    {
        if (s <= 0f) return Points[0];
        if (s >= Length) return End;
        int lo = 0, hi = cumulative.Length - 1;
        while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (cumulative[mid] <= s) lo = mid; else hi = mid; }
        float k = (s - cumulative[lo]) / Mathf.Max(1e-5f, cumulative[hi] - cumulative[lo]);
        return Vector2.Lerp(Points[lo], Points[hi], k);
    }

    /// <summary>Gidiş yönü (birim vektör).</summary>
    public Vector2 TangentAt(float s)
    {
        Vector2 a = PointAt(s - 0.3f), b = PointAt(s + 0.3f);
        Vector2 d = b - a;
        return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.down;
    }

    /// <summary>Gidiş yönünün SOLU (rota üstten alta inerken ekranda sağ taraf negatif).</summary>
    public Vector2 NormalAt(float s) { var t = TangentAt(s); return new Vector2(-t.y, t.x); }

    /// <summary>Noktanın rotaya en yakın izdüşümü: uzunluk konumu ve uzaklık.</summary>
    public float Project(Vector2 p, out float distance)
    {
        float best = float.MaxValue, bestS = 0f;
        for (int i = 1; i < Points.Length; i++)
        {
            Vector2 a = Points[i - 1], b = Points[i], ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            float d = (a + ab * t - p).sqrMagnitude;
            if (d < best) { best = d; bestS = cumulative[i - 1] + Mathf.Sqrt(len2) * t; }
        }
        distance = Mathf.Sqrt(best);
        return bestS;
    }

    public float DistanceTo(Vector2 p) { Project(p, out float d); return d; }

    /// <summary>Dönemeç tepeleri: ±1,5 birimlik pencerede yön değişimi en az <paramref name="minDegrees"/> olan yerel en büyükler.</summary>
    public List<float> Bends(float minDegrees = 30f)
    {
        var bends = new List<float>();
        const float window = 1.5f;
        float prevAngle = 0f, prevS = 0f; bool rising = false;
        for (float s = window; s <= Length - window; s += SampleStep)
        {
            float angle = Vector2.Angle(TangentAt(s - window), TangentAt(s + window));
            if (angle < prevAngle && rising && prevAngle >= minDegrees)
            {
                if (bends.Count == 0 || prevS - bends[bends.Count - 1] > 2.5f) bends.Add(prevS);
                rising = false;
            }
            else if (angle > prevAngle) rising = true;
            prevAngle = angle; prevS = s;
        }
        return bends;
    }
}
