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
        if (sr != null) sr.color = baseColor;
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
        if (sr != null) { var c = baseColor; c.a = baseColor.a * (1f - eased); sr.color = c; }
        if (t >= 1f) { dissolveStart = -1f; gameObject.SetActive(false); ResetDissolve(); }
    }
}
