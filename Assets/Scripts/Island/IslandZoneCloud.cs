using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kilitli bölge bulutu: bölge açılınca 1,5 sn'de kayıp dağılır (dışa süzülür, solar, hafifçe büyür), sonra gizlenir;
/// savaşta da gizlenir. Tiny Swords bulut sprite'ı (gölgesi baked).
/// </summary>
public sealed class IslandZoneCloud : MonoBehaviour
{
    public int zone;
    public Vector2 anchor;
    public float phase;

    private SpriteRenderer sr;
    private float dissolveStart = -1f, dissolveSeconds;
    private Vector2 drift;
    private Color baseColor = Color.white;
    private Vector3 baseScale = Vector3.one;
    private bool captured;
    private float dim = 1f;

    /// <summary>3. oturum (1.1): savunmada bulut kapanmaz, yarı saydam kalır (kilitli bölge haritada açık görünmesin; düşman seçilebilir).</summary>
    public void SetDim(float alpha)
    {
        Capture();
        dim = Mathf.Clamp01(alpha);
        if (dissolveStart < 0f && sr != null) { var c = baseColor; c.a = baseColor.a * dim; sr.color = c; }
    }
    public float Dim => dim;

    // ==================== KAPSAMA MODELİ (pişirme + IslandZoneMapTests aynı kuralı kullanır) ====================

    /// <summary>Bölge bulutlarının ölçeği (sprite 576×256 px, 64 ppu → opak alan ≈7,7×2,5 u). Eski 1,25–1,75 bölge sınırını aşıyordu.</summary>
    public const float BakedScale = 0.78f;
    /// <summary>Ölçek 1'de bulutun "kesin örttüğü" çekirdek elipsin yarı eksenleri (opak alanın içi).</summary>
    public const float CoreHalfWidth = 3.2f, CoreHalfHeight = 0.95f;
    /// <summary>Update'teki salınım genliği (çekirdekten düşülür: bulut salınırken de hücreyi örter).</summary>
    public const float SwayX = 0.35f, SwayY = 0.2f;

    /// <summary>Bulut (dinlenme konumu <paramref name="anchor"/>, ölçek <paramref name="scale"/>) dünya noktasını salınım boyunca örter mi.</summary>
    public static bool CoreCovers(Vector2 anchor, float scale, Vector2 point)
    {
        float a = CoreHalfWidth * scale - SwayX, b = CoreHalfHeight * scale - SwayY;
        if (a <= 0f || b <= 0f) return false;
        float dx = (point.x - anchor.x) / a, dy = (point.y - anchor.y) / b;
        return dx * dx + dy * dy <= 1f;
    }

    /// <summary>Hücre merkezi (dünya).</summary>
    public static Vector2 CellCenter(int col, int row) => IslandLayout.World(col + 0.5f, row + 0.5f);

    /// <summary>Dağılma sürüyor mu.</summary>
    public bool Dissolving => dissolveStart >= 0f;

    private void Capture()
    {
        if (captured) return;
        captured = true;
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        baseScale = transform.localScale;
    }

    /// <summary>Bulutu <paramref name="direction"/> yönünde süzüp soldurur; bitince nesne kapanır.</summary>
    public void Dissolve(Vector2 direction, float seconds)
    {
        Capture();
        drift = direction.normalized * 4.5f;
        dissolveSeconds = Mathf.Max(0.05f, seconds);
        dissolveStart = Time.time;
    }

    /// <summary>Bölge yeniden kilitlenirse (testler): bulutu ilk haline getirir.</summary>
    public void ResetDissolve()
    {
        Capture();
        dissolveStart = -1f;
        if (sr != null) { var c = baseColor; c.a = baseColor.a * dim; sr.color = c; }
        transform.localScale = baseScale;
    }

    private void Update()
    {
        Capture();
        Vector2 sway = new Vector2(Mathf.Sin(Time.time * 0.15f + phase) * 0.35f, Mathf.Sin(Time.time * 0.11f + phase * 1.7f) * 0.2f);
        if (dissolveStart < 0f)
        {
            transform.position = new Vector3(anchor.x + sway.x, anchor.y + sway.y, 0f);
            return;
        }
        float t = Mathf.Clamp01((Time.time - dissolveStart) / dissolveSeconds);
        float eased = t * t * (3f - 2f * t);
        transform.position = new Vector3(anchor.x + sway.x + drift.x * eased, anchor.y + sway.y + drift.y * eased, 0f);
        transform.localScale = baseScale * (1f + 0.25f * eased);
        if (sr != null) { var c = baseColor; c.a = baseColor.a * dim * (1f - eased); sr.color = c; }
        if (t >= 1f) { dissolveStart = -1f; gameObject.SetActive(false); ResetDissolve(); }
    }
}
