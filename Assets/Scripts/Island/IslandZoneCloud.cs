using System.Collections.Generic;
using UnityEngine;

/// <summary>Kilitli bölge bulutu: bölge açılınca kaybolur (savaşta da gizlenir).</summary>
public sealed class IslandZoneCloud : MonoBehaviour
{
    public int zone;
    public Vector2 anchor;
    public float phase;
    private void Update() =>
        transform.position = new Vector3(anchor.x + Mathf.Sin(Time.time * 0.15f + phase) * 0.35f, anchor.y + Mathf.Sin(Time.time * 0.11f + phase * 1.7f) * 0.2f, 0f);
}
