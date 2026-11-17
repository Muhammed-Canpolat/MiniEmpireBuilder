using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (2.3): madenin üstünde depo göstergesi — "1.240 / 3.000", doluluk barı (ortak UiHealthBar, altın rengi) ve depo dolunca
/// kırmızı "DOLU" uyarısı. Değerler GameManager'dan (tek kaynak: <see cref="GameManager.GetGoldMineStorageCapacity"/>).
/// </summary>
public sealed class MineStorageGauge : MonoBehaviour
{
    private static readonly NumberFormatInfo Dots = new NumberFormatInfo { NumberGroupSeparator = ".", NumberDecimalDigits = 0 };
    public static string Thousands(int value) => value.ToString("#,0", Dots);

    private Canvas canvas;
    private Image pill;
    private TextMeshProUGUI label;
    private UiHealthBar bar;
    private float fill;
    private bool full;
    private Transform mine;

    public string Text => label != null ? label.text : "";
    public bool Full => full;
    public float Fill => fill;

    public static MineStorageGauge Attach(Transform mineRoot)
    {
        var existing = mineRoot.GetComponentInChildren<MineStorageGauge>();
        if (existing != null) return existing;
        var go = new GameObject("MineStorageGauge", typeof(RectTransform));
        go.transform.SetParent(mineRoot, false);
        var g = go.AddComponent<MineStorageGauge>();
        g.mine = mineRoot;
        g.Build();
        return g;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(170, 46);
        pill = gameObject.AddComponent<Image>();
        pill.sprite = ReferenceUiVisuals.PillSprite();
        pill.type = Image.Type.Sliced;
        pill.color = new Color32(0x14, 0x11, 0x0D, 0xE0);
        pill.raycastTarget = false;
        label = ReferenceUiVisuals.Text(transform, "StorageLabel", "", 19, ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
            new Vector2(0f, 0.42f), Vector2.one, new Vector2(8, 0), new Vector2(-8, -2), false, true);
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var track = ReferenceUiVisuals.Image(transform, "StorageTrack", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.40f),
            Vector2.zero, Vector2.zero, ReferenceUiVisuals.Hex(0x120D08));
        track.sprite = ReferenceUiVisuals.SolidPillSprite();
        track.type = Image.Type.Sliced;
        bar = UiHealthBar.Build(track.rectTransform, ReferenceUiVisuals.Gold, () => fill);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldMineStoredChanged += Refresh;
            Refresh(GameManager.Instance.GoldMineStored, GameManager.Instance.GetGoldMineStorageCapacity());
        }
        lastMineScale = mine != null ? mine.lossyScale : Vector3.one;
        Place();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGoldMineStoredChanged -= Refresh;
    }

    public void Refresh(int stored, int capacity)
    {
        if (label == null) return;
        fill = capacity > 0 ? Mathf.Clamp01((float)stored / capacity) : 0f;
        full = capacity > 0 && stored >= capacity;
        label.text = full
            ? L.T($"DOLU · {Thousands(capacity)}", $"FULL · {Thousands(capacity)}")
            : $"{Thousands(stored)} / {Thousands(capacity)}";
        label.color = full ? new Color(1f, 0.45f, 0.35f) : ReferenceUiVisuals.Gold;
        if (bar != null && bar.Fill != null) bar.Fill.color = full ? new Color(0.85f, 0.3f, 0.2f) : ReferenceUiVisuals.Gold;
    }

    private float nextPlace;
    private Vector3 lastMineScale;

    /// <summary>Madenin görünen tepesinin hemen üstü (çırak ve bu gösterge hariç).</summary>
    private void Place()
    {
        if (mine == null) return;
        float top = mine.position.y + 1.2f;
        float cx = mine.position.x;
        foreach (var r in mine.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!r.enabled || r.sprite == null || r.GetComponent<Animator>() != null) continue;
            if (r.bounds.size.y > 6f) continue;
            top = Mathf.Max(top, r.bounds.max.y);
        }
        top = Mathf.Min(top, mine.position.y + 3.2f);
        transform.position = new Vector3(cx, top + 0.3f, 0f);
        float parent = Mathf.Abs(mine.lossyScale.x) > 0.001f ? Mathf.Abs(mine.lossyScale.x) : 1f;
        transform.localScale = Vector3.one * (0.021f / parent);   // ≈3,6 × 1 birim: telefonda okunur
    }

    private void LateUpdate()
    {
        if (full && pill != null)
            pill.color = Color.Lerp(new Color32(0x14, 0x11, 0x0D, 0xE0), new Color32(0x5A, 0x14, 0x0E, 0xE6),
                0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
        else if (pill != null) pill.color = new Color32(0x14, 0x11, 0x0D, 0xE0);
        // Bug 7: bina ölçek tween'iyle büyürken (0'dan) her karede yeniden yerleştir; durunca 1 sn aralık.
        Vector3 s = mine != null ? mine.lossyScale : Vector3.one;
        if (s != lastMineScale) { lastMineScale = s; nextPlace = Time.unscaledTime + 1f; Place(); }
        else if (Time.unscaledTime >= nextPlace) { nextPlace = Time.unscaledTime + 1f; Place(); }
    }
}
