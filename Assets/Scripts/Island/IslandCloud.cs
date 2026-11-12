using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kenar bulutu: ekran kenarına sabit (kamera yakınlaştırınca boyu ekranla orantılı kalır), yavaşça süzülür;
/// savaşta %40 saydamlığa iner. Tiny Swords Clouds_0N (gölgesi baked) gökyüzü katmanında durur.
/// </summary>
public sealed class IslandCloud : MonoBehaviour
{
    public Vector2 viewport;
    public float baseScale = 0.7f, phase, baseAlpha = 1f;
    private SpriteRenderer sr;
    private float alphaMultiplier = 1f;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    public void SetAlphaMultiplier(float multiplier) => alphaMultiplier = multiplier;

    private void LateUpdate()
    {
        var cam = IslandSceneSetup.Instance != null && IslandSceneSetup.Instance.WorldCamera != null
            ? IslandSceneSetup.Instance.WorldCamera : Camera.main;
        if (cam == null) return;
        float t = Time.time * 0.12f + phase;
        var v = new Vector3(viewport.x + Mathf.Sin(t) * 0.018f, viewport.y + Mathf.Sin(t * 0.7f + 1.3f) * 0.007f, -cam.transform.position.z);
        var p = cam.ViewportToWorldPoint(v);
        transform.position = new Vector3(p.x, p.y, 0f);
        transform.localScale = Vector3.one * (baseScale * cam.orthographicSize / 19.5f);
        if (sr != null)
        {
            var c = sr.color;
            c.a = Mathf.MoveTowards(c.a, baseAlpha * alphaMultiplier, Time.deltaTime * 2f);
            sr.color = c;
        }
    }
}
