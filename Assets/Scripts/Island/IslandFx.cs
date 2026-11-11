using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Ada efektleri: ok, su sıçraması, yüzen yazı. Katalog yoksa kodla çizilen yedekler.</summary>
public static class IslandFx
{
    public const int FxSorting = 900;

    /// <summary>Hedefe giden ok (yalnız görsel; hasar çağıranda). Süre = mesafe / hız.</summary>
    public static void EnemyShot(Vector2 from, Vector2 to)
    {
        var arrow = NewArrow(new Color(0.95f, 0.45f, 0.4f), from);
        float distance = Vector2.Distance(from, to);
        PointArrow(arrow.transform, to - from);
        arrow.transform.DOMove(to, Mathf.Max(0.1f, distance / 9f)).SetEase(Ease.Linear)
            .OnComplete(() => Object.Destroy(arrow)).SetLink(arrow);
    }

    public static GameObject NewArrow(Color color, Vector2 position)
    {
        var go = new GameObject("IslandArrow");
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = IslandArt.Arrow;
        renderer.color = color;
        renderer.sortingOrder = FxSorting;
        return go;
    }

    public static void PointArrow(Transform arrow, Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    /// <summary>Tekne batışı / ok düşüşü: su sıçraması. Katalogda kare yoksa genişleyen beyaz halka.</summary>
    public static void Splash(Vector2 position, float size = 1.6f)
    {
        var catalog = IslandVisualCatalog.Instance;
        var go = new GameObject("IslandSplash");
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = FxSorting - 1;
        if (catalog != null && IslandVisualCatalog.Has(catalog.splash))
        {
            float frameSize = Mathf.Max(0.01f, catalog.splash[0].bounds.size.y);
            go.transform.localScale = Vector3.one * (size / frameSize);
            var animator = go.AddComponent<IslandSpriteAnimator>();
            animator.PlayOnce(renderer, catalog.splash, 14f);
            Object.Destroy(go, catalog.splash.Length / 14f + 0.1f);
            return;
        }
        renderer.sprite = IslandArt.Ring;
        renderer.color = new Color(1f, 1f, 1f, 0.9f);
        go.transform.localScale = Vector3.one * size * 0.3f;
        go.transform.DOScale(size, 0.5f).SetEase(Ease.OutQuad).SetLink(go);
        renderer.DOFade(0f, 0.5f).OnComplete(() => Object.Destroy(go)).SetLink(go);
    }

    /// <summary>Yükselerek sönen dünya yazısı ("+3 boğuldu").</summary>
    public static void FloatText(Vector2 position, string text, Color color, float size = 4f)
    {
        var go = new GameObject("IslandFloatText");
        go.transform.position = position;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.sortingOrder = FxSorting + 5;
        go.transform.DOMoveY(position.y + 1.2f, 1.1f).SetEase(Ease.OutQuad).SetLink(go);
        DOTween.To(() => tmp.color, c => tmp.color = c, new Color(color.r, color.g, color.b, 0f), 0.5f)
            .SetDelay(0.7f).OnComplete(() => Object.Destroy(go)).SetLink(go);
    }
}

/// <summary>Kare dizisi oynatıcı: boşta / koşu / saldırı. Birim hareketinden koşuyu kendisi anlar.</summary>
public sealed class IslandSpriteAnimator : MonoBehaviour
{
    private const float DefaultFps = 8f;
    private SpriteRenderer sr;
    private IslandUnitFrames frames;
    private Vector3 lastPosition;
    private float attackUntil;
    private float clockOffset;
    // Tek seferlik oynatma (efekt).
    private Sprite[] once;
    private float onceFps, onceStart;
    /// <summary>Doluysa hareketten çıkarılan koşu yerine bu değer kullanılır (okçunun koşusu açıkça verilir).</summary>
    public bool? ForceRunning;
    public bool FaceRightArt = true;
    public float AttackSeconds = 0.5f;

    public void Setup(SpriteRenderer renderer, IslandUnitFrames unitFrames)
    {
        sr = renderer;
        frames = unitFrames;
        lastPosition = transform.position;
        clockOffset = Random.value * 2f;
    }

    public void PlayOnce(SpriteRenderer renderer, Sprite[] sequence, float fps)
    {
        sr = renderer;
        once = sequence;
        onceFps = fps;
        onceStart = Time.time;
        if (sr != null && once.Length > 0) sr.sprite = once[0];
    }

    public void Attack() => attackUntil = Time.time + AttackSeconds;

    private void LateUpdate()
    {
        if (sr == null) return;
        if (once != null)
        {
            int index = Mathf.Min(once.Length - 1, (int)((Time.time - onceStart) * onceFps));
            if (once.Length > 0) sr.sprite = once[index];
            return;
        }
        if (frames == null) return;
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        bool moving = ForceRunning ?? (delta.sqrMagnitude > 0.000004f);
        if (Mathf.Abs(delta.x) > 0.0005f) sr.flipX = FaceRightArt ? delta.x < 0f : delta.x > 0f;
        Sprite[] clip = Time.time < attackUntil && IslandVisualCatalog.Has(frames.attack) ? frames.attack
            : moving && IslandVisualCatalog.Has(frames.run) ? frames.run : frames.idle;
        if (!IslandVisualCatalog.Has(clip)) return;
        float t = Time.time < attackUntil ? (Time.time - (attackUntil - AttackSeconds)) * clip.Length / AttackSeconds
            : (Time.time + clockOffset) * DefaultFps;
        sr.sprite = clip[Mathf.Abs((int)t) % clip.Length];
    }
}

/// <summary>Sprite sıralaması y'ye göre (aşağıdaki öndedir).</summary>
public sealed class IslandYSort : MonoBehaviour
{
    public int bias;
    private SpriteRenderer sr;
    private void Awake() => sr = GetComponent<SpriteRenderer>();
    private void LateUpdate()
    {
        if (sr != null) sr.sortingOrder = 400 - Mathf.RoundToInt(transform.position.y * 10f) + bias;
    }
}
