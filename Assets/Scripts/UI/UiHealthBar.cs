using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (1.3): tüm UI can barlarının ortak bileşeni (savaş alt kartları: Savaşçı, Ana Üs, Çırak; ada kale barı).
/// - Dolgu SOL kenara sabittir, sağdan sola azalır: maske kabı (RectTransform, RectMask2D) sol çapalı, genişliği oran kadar;
///   içindeki hap görseli her zaman tam bar genişliğinde (sliced kenarlar hiçbir oranda bozulmaz, solda boşluk kalmaz).
/// - Hasarda "gecikmeli beyaz iz": iz kısa bekler (<see cref="TrailHold"/>) sonra dolguya doğru erir; azalma okunur.
/// - Değer kaynağı bir fonksiyondur (Slider.normalizedValue, Image.fillAmount ya da doğrudan oran).
/// Dünya üstündeki barlar (<see cref="HealthBar"/>) aynı kuralı SpriteRenderer ile uygular.
/// </summary>
public sealed class UiHealthBar : MonoBehaviour
{
    public const float TrailHold = 0.35f;
    public const float TrailSpeed = 1.6f;   // saniyede oran
    public const float FillSpeed = 6f;

    private RectTransform fillMask, fillPill, trailMask, trailPill;
    private Image fillImage;
    private Func<float> source;
    private float shown = 1f, trail = 1f, holdUntil;

    public Image Fill => fillImage;
    public float Shown => shown;
    public float Trail => trail;
    public RectTransform FillMask => fillMask;

    /// <summary>Var olan bir izin (track) içine dolgu + iz kurar. Track'in kendi Image'ı zemin olur.</summary>
    public static UiHealthBar Build(RectTransform track, Color color, Func<float> source, Sprite pill = null)
    {
        var existing = track.GetComponent<UiHealthBar>();
        if (existing != null) { existing.source = source; existing.fillImage.color = color; return existing; }
        var bar = track.gameObject.AddComponent<UiHealthBar>();
        bar.source = source;
        pill = pill != null ? pill : ReferenceUiVisuals.SolidPillSprite();
        bar.trailMask = Layer(track, "HpTrail", pill, new Color(1f, 0.97f, 0.9f, 0.9f), out bar.trailPill, out _);
        bar.fillMask = Layer(track, "HpFill", pill, color, out bar.fillPill, out bar.fillImage);
        var shine = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        shine.transform.SetParent(bar.fillPill, false);
        var srt = (RectTransform)shine.transform;
        srt.anchorMin = new Vector2(0f, 0.55f); srt.anchorMax = Vector2.one;
        srt.offsetMin = new Vector2(2f, 0f); srt.offsetMax = new Vector2(-2f, -1f);
        var shineImage = shine.GetComponent<Image>();
        shineImage.sprite = pill; shineImage.type = Image.Type.Sliced; shineImage.color = new Color(1f, 1f, 1f, 0.22f);
        shineImage.raycastTarget = false;
        float start = Mathf.Clamp01(source != null ? source() : 1f);
        bar.shown = bar.trail = start;
        bar.Apply();
        return bar;
    }

    private static RectTransform Layer(RectTransform track, string name, Sprite pill, Color color, out RectTransform inner, out Image image)
    {
        var mask = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        mask.transform.SetParent(track, false);
        var mrt = (RectTransform)mask.transform;
        mrt.pivot = new Vector2(0f, 0.5f);
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = mrt.offsetMax = Vector2.zero;
        var go = new GameObject(name + "Pill", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(mrt, false);
        inner = (RectTransform)go.transform;
        inner.pivot = new Vector2(0f, 0.5f);
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = inner.offsetMax = Vector2.zero;
        image = go.GetComponent<Image>();
        image.sprite = pill; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false;
        return mrt;
    }

    public void SetSource(Func<float> value) => source = value;

    /// <summary>Anında ayarla (iz dahil), örn. bar ilk kurulduğunda ya da can tamamen dolduğunda.</summary>
    public void Snap(float ratio)
    {
        shown = trail = Mathf.Clamp01(ratio);
        Apply();
    }

    private void Update()
    {
        if (source == null) return;
        float target = Mathf.Clamp01(source());
        Step(target, Time.unscaledDeltaTime, Time.timeScale > 0f);
    }

    /// <summary>Bir adım ilerletir (testler de çağırır). <paramref name="running"/> false iken (duraklatma) iz donar.</summary>
    public void Step(float target, float dt, bool running = true)
    {
        if (target < shown - 1e-4f)
        {
            if (trail < shown) trail = shown;       // iz önceki dolgudan başlar
            holdUntil = Time.unscaledTime + TrailHold;
            shown = target;                          // dolgu hasarı hemen gösterir
        }
        else if (target > shown) shown = Mathf.MoveTowards(shown, target, FillSpeed * dt);
        if (trail < shown) trail = shown;
        else if (running && Time.unscaledTime >= holdUntil) trail = Mathf.MoveTowards(trail, shown, TrailSpeed * dt);
        Apply();
    }

    private void Apply()
    {
        Set(fillMask, fillPill, shown);
        Set(trailMask, trailPill, trail);
    }

    private static void Set(RectTransform mask, RectTransform pill, float ratio)
    {
        if (mask == null) return;
        bool visible = ratio > 0.001f;
        if (mask.gameObject.activeSelf != visible) mask.gameObject.SetActive(visible);
        if (!visible) return;
        mask.anchorMax = new Vector2(ratio, 1f);                  // sol kenar 0'da sabit
        pill.anchorMax = new Vector2(1f / ratio, 1f);             // hap her zaman tam bar genişliği
    }
}
