using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Ada efektleri: ok, su sıçraması, yüzen yazı (hepsi Tiny Swords varlıkları; katalog yoksa efekt çizilmez).</summary>
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
        var catalog = IslandVisualCatalog.Instance;
        renderer.sprite = catalog != null ? catalog.Single("arrow") : null;   // Tiny Swords Arrow.png (sağa bakar)
        renderer.color = color;
        renderer.sortingOrder = FxSorting;
        return go;
    }

    public static void PointArrow(Transform arrow, Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    /// <summary>Tekne batışı / ok düşüşü: Tiny Swords su sıçraması.</summary>
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
        Object.Destroy(go);   // katalog yoksa efekt yok (kodla çizilmiş yedek kaldırıldı)
    }

    /// <summary>Katalogdaki adlandırılmış Tiny Swords efekt karesi dizisini bir kez oynatır (Dust, Explosion, Fire). Yoksa efekt çizilmez.</summary>
    public static GameObject Play(string frameName, Vector2 position, float size, float fps = 14f, bool loop = false,
        Transform parent = null, int order = FxSorting - 2)
    {
        var catalog = IslandVisualCatalog.Instance;
        var frames = catalog != null ? catalog.Frames(frameName) : null;
        if (!IslandVisualCatalog.Has(frames)) return null;
        var go = new GameObject("IslandFx_" + frameName);
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        float frameSize = Mathf.Max(0.01f, frames[0].bounds.size.y);
        go.transform.localScale = Vector3.one * (size / frameSize);
        var animator = go.AddComponent<IslandSpriteAnimator>();
        animator.PlayOnce(renderer, frames, fps, loop);
        if (!loop) Object.Destroy(go, frames.Length / fps + 0.1f);
        return go;
    }

    /// <summary>Tek karelik Kenney sprite'ını kısa süre gösterip söndürür (patlama/ateş parıltısı).</summary>
    public static GameObject Pulse(string spriteName, Vector2 position, float height, float seconds, int order = FxSorting - 1)
    {
        var catalog = IslandVisualCatalog.Instance;
        var sprite = catalog != null ? catalog.Single(spriteName) : null;
        if (sprite == null) return null;
        var go = new GameObject("IslandPulse_" + spriteName);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        float scale = height / Mathf.Max(0.01f, sprite.bounds.size.y);
        go.transform.localScale = Vector3.one * scale * 0.5f;
        go.transform.DOScale(Vector3.one * scale, seconds * 0.4f).SetEase(Ease.OutBack).SetLink(go);
        renderer.DOFade(0f, seconds * 0.6f).SetDelay(seconds * 0.4f).OnComplete(() => Object.Destroy(go)).SetLink(go);
        return go;
    }

    /// <summary>Uçan sürü uyarısı: kenarda içeri bakan, nabız atan Tiny Swords oku (2 sn) ve "!" yazısı.</summary>
    public static void Telegraph(Vector2 edgePoint, Vector2 inward, float seconds)
    {
        // Görünür kenar: harita sınırı içinde, dar bir pay
        Vector2 at = new Vector2(Mathf.Clamp(edgePoint.x, 1.2f, IslandLayout.Width - 1.2f), Mathf.Clamp(edgePoint.y, 1.6f, IslandLayout.Height - 1.6f));
        var arrow = NewArrow(new Color(1f, 0.35f, 0.3f, 0.95f), at);
        arrow.transform.localScale = Vector3.one * 2.2f;
        PointArrow(arrow.transform, inward);
        arrow.GetComponent<SpriteRenderer>().sortingOrder = FxSorting + 8;
        arrow.transform.DOScale(Vector3.one * 2.8f, 0.35f).SetLoops(-1, LoopType.Yoyo).SetLink(arrow);
        FloatText(at + Vector2.up * 0.9f, L.T("ÜSTÜNDEN!", "FROM THE AIR!"), new Color(1f, 0.55f, 0.4f), 3.6f);
        Object.Destroy(arrow, seconds);
    }

    /// <summary>Dünya yazısına koyu kontur (okunurluk: zemin/tekne üstünde kaybolmasın).</summary>
    public static void Outline(TextMeshPro tmp)
    {
        tmp.outlineWidth = 0.28f;
        tmp.outlineColor = new Color32(24, 16, 10, 255);
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
        Outline(tmp);
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

    private bool onceLoop;

    public void PlayOnce(SpriteRenderer renderer, Sprite[] sequence, float fps, bool loop = false)
    {
        sr = renderer;
        once = sequence;
        onceFps = fps;
        onceLoop = loop;
        onceStart = Time.time;
        if (sr != null && once.Length > 0) sr.sprite = once[0];
    }

    public void Attack() => attackUntil = Time.time + AttackSeconds;

    private void LateUpdate()
    {
        if (sr == null) return;
        if (once != null)
        {
            int step = (int)((Time.time - onceStart) * onceFps);
            int index = onceLoop ? step % once.Length : Mathf.Min(once.Length - 1, step);
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

/// <summary>
/// Sprite sıralaması ayak y'sine göre (aşağıdaki öndedir): <c>400 - round(feetY*10) + bias</c>; arazi katmanları (kit §7.4)
/// bunun altındadır. Ayak, bileşenin bulunduğu nesnenin konumudur (görsel çocukta ise ayakta durur); sprite kök merkezliyse
/// <see cref="footOffset"/> ayağın merkezden aşağı uzaklığıdır.
/// </summary>
public sealed class IslandYSort : MonoBehaviour
{
    public const int Base = 400;
    public int bias;
    public float footOffset;
    private SpriteRenderer[] renderers;
    private UnityEngine.Rendering.SortingGroup group;

    public static int OrderFor(float feetY, int bias = 0) => Base - Mathf.RoundToInt(feetY * 10f) + bias;

    private void Awake()
    {
        // DM2D iskeletli canavarlar: parçaların kendi iç sırası korunur, bütün kök SortingGroup ile sıralanır.
        group = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (group == null) renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void LateUpdate()
    {
        int order = OrderFor(transform.position.y - footOffset, bias);
        if (group != null) { group.sortingOrder = order; return; }
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sortingOrder = order;
    }
}
