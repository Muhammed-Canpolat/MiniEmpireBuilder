using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Zamanlı baskının ada görünümündeki izi (island_base_v2 §3): T−30 dk'dan itibaren hedef kumsalın açığında 1–3 küçük Kenney teknesi
/// sallanır, kumsalda kırmızı bir Tiny Swords oku ve kumsal harfi görünür. Pencere (T0..T0+2 sa) açıkken tekneye ya da işarete
/// dokunmak canlı savunmayı başlatır. Savunmada ve Akın sırasında gizlidir. Görseller katalogdan (Kenney tekne, Tiny Swords ok).
/// </summary>
public sealed class IslandRaidVisuals : MonoBehaviour
{
    public Action StartRequested;

    private readonly List<Transform> boats = new List<Transform>();
    private readonly List<float> bobSeeds = new List<float>();
    private GameObject root, marker;
    private TextMeshPro markerText;
    private int builtBeach = -1, builtSeed;
    private IslandRaidScheduler.Phase lastPhase = IslandRaidScheduler.Phase.None;
    private float nextPoll;
    private Vector2 markerPosition;

    public bool Visible => root != null && root.activeSelf;
    public int BoatCount => boats.Count;
    public IslandRaidScheduler.Phase Phase => lastPhase;
    /// <summary>Test/ekran görüntüsü: hedef kumsalın işaret noktası (dünya).</summary>
    public Vector2 MarkerPosition => markerPosition;

    private void Update()
    {
        if (Time.unscaledTime >= nextPoll) { nextPoll = Time.unscaledTime + 0.25f; Poll(); }
        for (int i = 0; i < boats.Count; i++)
        {
            if (boats[i] == null) continue;
            float t = Time.time + bobSeeds[i];
            var hull = boats[i].GetChild(0);
            hull.localPosition = new Vector3(0f, Mathf.Sin(t * 1.6f) * 0.06f, 0f);
            hull.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f) * 2.2f);
        }
    }

    public void Poll()
    {
        var save = IslandSession.Save;
        var setup = IslandSceneSetup.Instance;
        var phase = save != null ? IslandRaidScheduler.PhaseAt(save, IslandRepair.Now) : IslandRaidScheduler.Phase.None;
        if (phase != lastPhase)
        {
            if (lastPhase == IslandRaidScheduler.Phase.None && phase == IslandRaidScheduler.Phase.Warning)
                AudioManager.Instance?.PlaySfx("raidHorn");   // ses varlığı yoksa sessiz
            lastPhase = phase;
        }
        bool idle = setup != null && !setup.InDefense && setup.Raid != null && setup.Raid.Phase == IslandRaidManager.State.Idle;
        bool show = phase != IslandRaidScheduler.Phase.None && idle;
        if (!show) { if (root != null && root.activeSelf) root.SetActive(false); return; }
        if (root == null || builtBeach != save.raidBeach || builtSeed != save.raidSeed) Rebuild(save);
        if (!root.activeSelf) root.SetActive(true);
        if (markerText != null)
            markerText.text = phase == IslandRaidScheduler.Phase.Live
                ? L.T($"BASKIN! {IslandRaidScheduler.BeachName(save.raidBeach)}", $"RAID! {IslandRaidScheduler.BeachName(save.raidBeach)}")
                : L.T($"{IslandRaidScheduler.BeachName(save.raidBeach)} KUMSALI", $"BEACH {IslandRaidScheduler.BeachName(save.raidBeach)}");
    }

    /// <summary>Yoldaki noktayı sondan <paramref name="back"/> birim geriden döndürür (yön: ilerleme yönü).</summary>
    private static Vector2 PointFromEnd(Vector2[] path, float back, out Vector2 direction)
    {
        direction = Vector2.down;
        float remaining = back;
        for (int i = path.Length - 1; i > 0; i--)
        {
            float length = Vector2.Distance(path[i - 1], path[i]);
            direction = (path[i] - path[i - 1]).normalized;
            if (remaining <= length) return Vector2.Lerp(path[i], path[i - 1], length > 0.001f ? remaining / length : 0f);
            remaining -= length;
        }
        return path[0];
    }

    private void Rebuild(IslandSaveData save)
    {
        if (root != null) Destroy(root);
        boats.Clear(); bobSeeds.Clear();
        builtBeach = save.raidBeach; builtSeed = save.raidSeed;
        var beach = IslandLayout.Beaches[Mathf.Clamp(save.raidBeach, 0, IslandLayout.Beaches.Length - 1)];
        root = new GameObject("IslandRaidVisuals");
        root.transform.SetParent(transform, false);
        var catalog = IslandVisualCatalog.Instance;
        var rng = new System.Random(save.raidSeed);
        int count = 1 + rng.Next(3);                       // 1–3 tekne
        for (int i = 0; i < count; i++)
        {
            Vector2 direction;
            Vector2 at = PointFromEnd(beach.boatPath, 2.6f + 2.2f * i, out direction);
            var go = new GameObject("HorizonBoat" + i);
            go.transform.SetParent(root.transform, false);
            go.transform.position = at;
            var hull = new GameObject("Hull");
            hull.transform.SetParent(go.transform, false);
            var sr = hull.AddComponent<SpriteRenderer>();
            // İlk tekne kızıl-haç gemi (ship 3), diğerleri sandal; mavi kullanılmaz (düşman rengi değil)
            sr.sprite = catalog != null ? catalog.Single(i == 0 ? "ship_3" : "dinghy" + (1 + rng.Next(3))) : null;
            float scale = Progression.Island.HorizonBoatScale;
            hull.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
            go.AddComponent<IslandYSort>().bias = -20;
            boats.Add(go.transform);
            bobSeeds.Add((float)rng.NextDouble() * 6f);
        }
        // Kumsalda kırmızı işaret: Tiny Swords oku, kumsala bakar; nabız atar
        markerPosition = beach.landing + Vector2.up * 1.6f;
        marker = IslandFx.NewArrow(new Color(1f, 0.28f, 0.24f, 0.95f), markerPosition);
        marker.transform.SetParent(root.transform, true);
        marker.transform.localScale = Vector3.one * 2.2f;
        IslandFx.PointArrow(marker.transform, Vector2.down);
        marker.GetComponent<SpriteRenderer>().sortingOrder = IslandFx.FxSorting + 8;
        marker.transform.DOScale(Vector3.one * 2.8f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetLink(marker);
        var label = new GameObject("RaidBeachLabel");
        label.transform.SetParent(root.transform, true);
        label.transform.position = markerPosition + new Vector2(-1.5f, 1.0f);   // tekneye binmesin: işaretin solu, karanın üstü
        markerText = label.AddComponent<TextMeshPro>();
        markerText.fontSize = 3.6f;
        markerText.alignment = TextAlignmentOptions.Center;
        markerText.color = new Color(1f, 0.55f, 0.45f);
        markerText.fontStyle = FontStyles.Bold;
        IslandFx.Outline(markerText);
        markerText.sortingOrder = IslandFx.FxSorting + 9;
    }

    /// <summary>Dünya dokunuşu: pencere açıkken tekne/işaret yakınına dokunulduysa savunmayı başlatır. Tüketildiyse true.</summary>
    public bool HandleTap(Vector2 world)
    {
        if (!Visible) return false;
        bool near = Vector2.Distance(world, markerPosition) < 1.8f;
        foreach (var boat in boats) if (boat != null && Vector2.Distance(world, boat.position) < 1.8f) near = true;
        if (!near) return false;
        StartRequested?.Invoke();
        return true;
    }

    /// <summary>Kamera odağı için: kumsal ile teknelerin ortası.</summary>
    public Vector2 FocusPoint
    {
        get
        {
            Vector2 sum = markerPosition;
            foreach (var boat in boats) if (boat != null) sum += (Vector2)boat.position;
            return sum / (1 + boats.Count);
        }
    }
}
