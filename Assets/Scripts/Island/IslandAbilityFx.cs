using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 3. oturum 3.3: akın yetenekleri için kodla çizilen küçük sprite'lar (halka, disk, ağ) ve ortak yardımcılar.
/// AĞ SPRITE'I GEÇİCİ YER TUTUCUDUR (ART_NEEDS.md): gerçek ağ çizimi gelince <see cref="Net"/> değişir.
/// </summary>
public static class IslandAbilityArt
{
    private static Sprite ring, disc, net;

    /// <summary>1 birim çaplı beyaz halka (renk SpriteRenderer'dan).</summary>
    public static Sprite Ring()
    {
        if (ring != null) return ring;
        const int size = 128;
        var tex = NewTexture(size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float outer = Mathf.Clamp01(size * 0.5f - 1f - d), inner = Mathf.Clamp01(d - (size * 0.5f - 11f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Min(outer, inner)));
            }
        tex.Apply();
        ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        ring.name = "IslandAbilityRing";
        return ring;
    }

    /// <summary>1 birim çaplı yumuşak kenarlı beyaz disk.</summary>
    public static Sprite Disc()
    {
        if (disc != null) return disc;
        const int size = 128;
        var tex = NewTexture(size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((size * 0.5f - d) / 6f)));
            }
        tex.Apply();
        disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        disc.name = "IslandAbilityDisc";
        return disc;
    }

    /// <summary>1 birim çaplı yuvarlak halat ağı (kahverengi ip, elmas örgü). GEÇİCİ yer tutucu.</summary>
    public static Sprite Net()
    {
        if (net != null) return net;
        const int size = 96;
        var tex = NewTexture(size);
        var rope = new Color(0.62f, 0.43f, 0.22f, 1f);
        var knot = new Color(0.42f, 0.28f, 0.14f, 1f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                if (d > size * 0.5f - 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                bool border = d > size * 0.5f - 5f;
                int a = (x + y) % 16, b = (x - y + size * 2) % 16;
                bool line = a < 3 || b < 3;
                bool node = a < 4 && b < 4;
                tex.SetPixel(x, y, node || border ? knot : line ? rope : Color.clear);
            }
        tex.Apply();
        net = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        net.name = "IslandAbilityNet";
        return net;
    }

    private static Texture2D NewTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    /// <summary>Sesi çalar (anahtar yoksa / ses kapalıysa sessiz).</summary>
    public static void Sfx(string key) => AudioManager.Instance?.PlaySfx(key);
}

/// <summary>Hafif ekran sarsıntısı: kamera konumuna yalnız çizim sırasında ofset ekler, hemen geri alır (takip kamerasıyla savaşmaz).</summary>
public sealed class IslandCameraShake : MonoBehaviour
{
    private Camera cam;
    private float until, duration, amplitude;
    private Vector3 applied;

    public static void Shake(float amplitude = 0.08f, float seconds = 0.12f)
    {
        var setup = IslandSceneSetup.Instance;
        var camera = setup != null ? setup.WorldCamera : null;
        if (camera == null) return;
        var shake = camera.GetComponent<IslandCameraShake>();
        if (shake == null) { shake = camera.gameObject.AddComponent<IslandCameraShake>(); shake.cam = camera; }
        shake.amplitude = Mathf.Max(shake.Active ? shake.amplitude : 0f, amplitude);
        shake.duration = Mathf.Max(0.02f, seconds);
        shake.until = Time.unscaledTime + seconds;
    }

    public bool Active => Time.unscaledTime < until;

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBegin;
        RenderPipelineManager.endCameraRendering += OnEnd;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBegin;
        RenderPipelineManager.endCameraRendering -= OnEnd;
        Restore();
    }

    private void OnBegin(ScriptableRenderContext context, Camera camera)
    {
        if (camera != cam || !Active) return;
        float k = Mathf.Clamp01((until - Time.unscaledTime) / duration);
        var offset = (Vector3)(Random.insideUnitCircle * amplitude * k);
        cam.transform.position += offset;
        applied = offset;
    }

    private void OnEnd(ScriptableRenderContext context, Camera camera)
    {
        if (camera == cam) Restore();
    }

    private void Restore()
    {
        if (applied != Vector3.zero && cam != null) cam.transform.position -= applied;
        applied = Vector3.zero;
    }
}

/// <summary>Ağ Oku: hedefin üstünde oturan, onu izleyen ve kök süresi bitince sönen halat ağı.</summary>
public sealed class IslandNetOverlay : MonoBehaviour
{
    private float until, fadeFrom;
    private SpriteRenderer sr;

    public static IslandNetOverlay Attach(Transform target, Vector2 worldCenter, float diameter, float seconds)
    {
        if (target == null) return null;
        var overlay = target.GetComponentInChildren<IslandNetOverlay>(true);
        if (overlay == null)
        {
            var go = new GameObject("NetOverlay");
            go.transform.SetParent(target, false);
            overlay = go.AddComponent<IslandNetOverlay>();
            overlay.sr = go.AddComponent<SpriteRenderer>();
            overlay.sr.sprite = IslandAbilityArt.Net();
            overlay.sr.sortingOrder = IslandFx.FxSorting - 3;
        }
        overlay.transform.position = new Vector3(worldCenter.x, worldCenter.y, target.position.z);
        float parentScale = Mathf.Max(0.01f, Mathf.Abs(target.lossyScale.x));
        overlay.transform.localScale = Vector3.one * (diameter / parentScale);
        overlay.fadeFrom = 0.4f;
        overlay.until = Time.time + seconds;
        overlay.sr.color = Color.white;
        return overlay;
    }

    private void Update()
    {
        float left = until - Time.time;
        if (left <= 0f) { Destroy(gameObject); return; }
        if (sr != null) sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(left / fadeFrom));
    }
}

/// <summary>Ateşli Ok: okun arkasında kısa ömürlü alev izi (en çok 40 canlı parça).</summary>
public sealed class IslandFlameTrail : MonoBehaviour
{
    private static int alive;
    private float next;

    private void Update()
    {
        if (Time.time < next || alive >= 40) return;
        next = Time.time + 0.035f;
        var catalog = IslandVisualCatalog.Instance;
        var sprite = catalog != null ? catalog.Single("fire_1") : null;
        if (sprite == null) return;
        var go = new GameObject("FlameTrail");
        go.transform.position = transform.position + (Vector3)(Random.insideUnitCircle * 0.06f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, Random.Range(0.45f, 0.7f), 0.15f, 0.9f);
        sr.sortingOrder = IslandFx.FxSorting - 1;
        float scale = 0.5f / Mathf.Max(0.01f, sprite.bounds.size.y);
        go.transform.localScale = Vector3.one * scale;
        alive++;
        go.transform.DOScale(Vector3.one * scale * 0.3f, 0.28f).SetLink(go);
        sr.DOFade(0f, 0.28f).OnComplete(() => Destroy(go)).OnKill(() => alive--).SetLink(go);
    }

}
