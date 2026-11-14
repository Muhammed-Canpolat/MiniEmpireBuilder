using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 7. oturum (D94): bölge atmosferi — yalnız görsel, oynanışa etkisi yok. Partikül/örnek sayıları <see cref="BattleFxQuality.Budget"/> ile
/// sınırlı (düşük kalitede yarısı). Ayrıntı: Ruins (toz), Fog (süzülen sis bulutları), Mine (altın parıltısı), Snow (kar yağışı),
/// Dark (kasvet örtüsü + kenar kararması), Lava (lav gölü parlaması/kabarcık + kül).
/// Her şey çalışma anında üretilen küçük dokularla kurulur; Update içinde yeni nesne ayrılmaz.
/// </summary>
public sealed class BattleAtmosphereFx : MonoBehaviour
{
    public BattleAtmosphere Kind { get; private set; }
    /// <summary>Etkin partikül/sprite sayısı (performans testi).</summary>
    public int ActiveParticles { get; private set; }

    private const int OrderDust = 450, OrderFog = 820, OrderGloom = 840, OrderVignette = 845;
    private const int MaxSprites = 60;

    // Kamera görüş dikdörtgeni boyunca sarılan partiküller (toz, kar, kül, köz)
    private SpriteRenderer[] pr; private Transform[] pt;
    private Vector2[] ppos, pvel; private float[] pphase, pfreq, pamp, palpha; private Color[] pcol; private bool[] pflicker;
    private int pCount;

    // Sis bulutları (dünya uzayı)
    private SpriteRenderer[] fr; private Transform[] ft; private float[] fx, fy, fspeed, fhalf;
    private int fCount;

    // Altın parıltılar
    private SpriteRenderer[] sr; private Transform[] st; private float[] sLife, sT, sWait, sSize;
    private int sCount;

    // Lav gölü kabarcıkları
    private SpriteRenderer[] br; private Transform[] bt; private Vector2[] bCenter; private float[] bLife, bT, bWait;
    private int bCount;

    // Dark örtüleri
    private Transform gloomT, vignT;

    private Camera cam;
    private Sprite circle, star, softEllipse, glow, vignette;
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private readonly List<Sprite> sprites = new List<Sprite>();
    private Transform root;

    public void Setup(BattleRegionMap map)
    {
        Kind = map != null ? map.atmosphere : BattleAtmosphere.None;
        ActiveParticles = 0;
        if (Kind == BattleAtmosphere.None) return;
        cam = Camera.main;
        var rootGo = new GameObject("AtmosferFx");
        root = rootGo.transform; root.SetParent(transform, false);
        circle = BaseMapManager.MakeCircleSprite(Color.white);
        sprites.Add(circle); textures.Add(circle.texture);

        switch (Kind)
        {
            case BattleAtmosphere.Ruins: BuildWrap(BattleFxQuality.Budget(14), 0); break;
            case BattleAtmosphere.Fog: BuildFog(); break;
            case BattleAtmosphere.Mine: BuildSparkles(BattleFxQuality.Budget(10)); break;
            case BattleAtmosphere.Snow: BuildWrap(BattleFxQuality.Budget(40), 1); break;
            case BattleAtmosphere.Dark: BuildDark(map); break;
            case BattleAtmosphere.Lava: BuildLava(); break;
        }
    }

    // ==================== KURULUM ====================

    private SpriteRenderer NewSr(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite; r.sortingOrder = order;
        ActiveParticles++;
        return r;
    }

    private static void SetSize(Transform t, Sprite s, float w, float h)
    {
        var b = s.bounds.size;
        t.localScale = new Vector3(w / b.x, h / b.y, 1f);
    }

    private Sprite AddSprite(Texture2D tex, float ppu)
    {
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
        var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
        textures.Add(tex); sprites.Add(sp);
        return sp;
    }

    /// <summary>Yumuşak kenarlı elips/daire (alfa: 1 - r^2 düzgün düşüş).</summary>
    private Sprite SoftSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        var cols = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                cols[y * w + x] = new Color(1f, 1f, 1f, d * d * (3f - 2f * d));
            }
        tex.SetPixels(cols); tex.Apply();
        return AddSprite(tex, w);
    }

    private Sprite StarSprite()
    {
        const int s = 16;
        var tex = new Texture2D(s, s, TextureFormat.ARGB32, false);
        var cols = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / s * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / s * 2f - 1f);
                float a = Mathf.Clamp01(1f - (Mathf.Sqrt(dx) + Mathf.Sqrt(dy)));
                a = Mathf.Max(a * 1.6f, Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3.2f));
                cols[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        tex.SetPixels(cols); tex.Apply();
        return AddSprite(tex, s);
    }

    private Sprite VignetteSprite()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.ARGB32, false);
        var cols = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01((d - 0.55f) / 0.87f);
                cols[y * s + x] = new Color(0f, 0f, 0f, 0.45f * t * t * (3f - 2f * t));
            }
        tex.SetPixels(cols); tex.Apply();
        return AddSprite(tex, s);
    }

    private void AllocWrap(int n)
    {
        pr = new SpriteRenderer[n]; pt = new Transform[n];
        ppos = new Vector2[n]; pvel = new Vector2[n];
        pphase = new float[n]; pfreq = new float[n]; pamp = new float[n]; palpha = new float[n];
        pcol = new Color[n]; pflicker = new bool[n];
    }

    private void ViewRect(out float l, out float b, out float w, out float h)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) { var r = BattleMapGeometry.Bounds; l = r.xMin; b = r.yMin; w = r.width; h = 18f; return; }
        h = cam.orthographicSize * 2f; w = h * cam.aspect;
        var p = cam.transform.position;
        l = p.x - w * 0.5f; b = p.y - h * 0.5f;
    }

    /// <summary>mode 0 = toz (yavaş süzülen, sıcak kahve), 1 = kar.</summary>
    private void BuildWrap(int n, int mode)
    {
        n = Mathf.Min(n, MaxSprites);
        AllocWrap(n); pCount = n;
        ViewRect(out float l, out float b, out float w, out float h);
        for (int i = 0; i < n; i++)
        {
            var r = NewSr(mode == 0 ? "Toz" : "Kar", circle, OrderDust);
            pr[i] = r; pt[i] = r.transform;
            ppos[i] = new Vector2(l + Random.value * w, b + Random.value * h);
            pphase[i] = Random.value * 6.28f;
            if (mode == 0)
            {
                float s = Random.Range(0.05f, 0.1f);
                SetSize(pt[i], circle, s, s);
                pvel[i] = new Vector2(Random.Range(-0.15f, 0.2f), Random.Range(-0.05f, 0.12f));
                pamp[i] = 0.15f; pfreq[i] = Random.Range(0.4f, 0.9f);
                palpha[i] = Random.Range(0.25f, 0.4f);
                pcol[i] = new Color(0.78f, 0.62f, 0.42f, palpha[i]);
            }
            else
            {
                float s = Random.Range(0.06f, 0.14f);
                SetSize(pt[i], circle, s, s);
                pvel[i] = new Vector2(Random.Range(-0.5f, -0.2f), Random.Range(-1.4f, -0.8f));
                pamp[i] = Random.Range(0.2f, 0.5f); pfreq[i] = Random.Range(0.8f, 1.8f);
                palpha[i] = Random.Range(0.5f, 0.85f);
                pcol[i] = new Color(1f, 1f, 1f, palpha[i]);
            }
            r.color = pcol[i];
        }
    }

    private void BuildFog()
    {
        int n = Mathf.Min(BattleFxQuality.Budget(8), MaxSprites);
        n = Mathf.Max(n, BattleFxQuality.Low ? 3 : 5);
        softEllipse = SoftSprite(128, 64);
        fr = new SpriteRenderer[n]; ft = new Transform[n];
        fx = new float[n]; fy = new float[n]; fspeed = new float[n]; fhalf = new float[n];
        fCount = n;
        var rect = BattleMapGeometry.Bounds;
        for (int i = 0; i < n; i++)
        {
            var r = NewSr("Sis", softEllipse, OrderFog);
            fr[i] = r; ft[i] = r.transform;
            float w = Random.Range(5f, 8f), h = Random.Range(1.6f, 2.6f);
            SetSize(ft[i], softEllipse, w, h);
            fhalf[i] = w * 0.5f;
            // Yükseklikler haritaya eşit dağılır (her bulut kendi şeridinde)
            fy[i] = Mathf.Lerp(rect.yMin + 1f, rect.yMax - 1f, (i + Random.Range(0.1f, 0.9f)) / n);
            fx[i] = Random.Range(-8f, 8f);
            fspeed[i] = Random.Range(0.15f, 0.35f) * (Random.value < 0.35f ? -1f : 1f);
            r.color = new Color(0.92f, 0.97f, 0.94f, Random.Range(0.18f, 0.32f));
            ft[i].position = new Vector3(fx[i], fy[i], 0f);
        }
    }

    private void BuildSparkles(int n)
    {
        n = Mathf.Min(n, MaxSprites);
        star = StarSprite();
        sr = new SpriteRenderer[n]; st = new Transform[n];
        sLife = new float[n]; sT = new float[n]; sWait = new float[n]; sSize = new float[n];
        sCount = n;
        for (int i = 0; i < n; i++)
        {
            var r = NewSr("Parilti", star, OrderDust);
            r.color = new Color(1f, 0.85f, 0.35f, 0f);
            r.enabled = false;
            sr[i] = r; st[i] = r.transform;
            sWait[i] = Random.Range(0f, 2.5f); sT[i] = -1f;
        }
    }

    private void BuildDark(BattleRegionMap map)
    {
        var gloom = map != null ? map.gloom : Color.clear;
        if (gloom.a > 0.001f)
        {
            var g = NewSr("Kasvet", BaseMapManager.MakeSquareSprite(Color.white), OrderGloom);
            sprites.Add(g.sprite); textures.Add(g.sprite.texture);
            g.color = gloom; gloomT = g.transform;
        }
        vignette = VignetteSprite();
        var v = NewSr("Kenar", vignette, OrderVignette);
        vignT = v.transform;
        UpdateOverlays();
        BuildAsh(BattleFxQuality.Budget(16), false, 0f);
    }

    private void UpdateOverlays()
    {
        ViewRect(out float l, out float b, out float w, out float h);
        var c = new Vector3(l + w * 0.5f, b + h * 0.5f, 0f);
        if (gloomT != null)
        {
            gloomT.position = c;
            SetSize(gloomT, gloomT.GetComponent<SpriteRenderer>().sprite, w + 2f, h + 2f);
        }
        if (vignT != null)
        {
            vignT.position = c;
            SetSize(vignT, vignette, w + 0.2f, h + 0.2f);
        }
    }

    /// <summary>Gri-mor kül (Dark) ya da gri kül + turuncu köz (Lava).</summary>
    private void BuildAsh(int n, bool withEmbers, float emberRatio)
    {
        n = Mathf.Min(n, MaxSprites - ActiveParticles);
        if (n <= 0) return;
        AllocWrap(n); pCount = n;
        ViewRect(out float l, out float b, out float w, out float h);
        for (int i = 0; i < n; i++)
        {
            bool ember = withEmbers && Random.value < emberRatio;
            var r = NewSr(ember ? "Koz" : "Kul", circle, OrderDust);
            pr[i] = r; pt[i] = r.transform;
            ppos[i] = new Vector2(l + Random.value * w, b + Random.value * h);
            pphase[i] = Random.value * 6.28f;
            pamp[i] = Random.Range(0.1f, 0.35f); pfreq[i] = Random.Range(0.5f, 1.4f);
            float s;
            if (ember)
            {
                s = Random.Range(0.05f, 0.09f);
                pvel[i] = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.4f, 0.9f));
                palpha[i] = Random.Range(0.6f, 0.95f);
                pcol[i] = new Color(1f, Random.Range(0.4f, 0.6f), 0.1f, palpha[i]);
                pflicker[i] = true;
            }
            else if (withEmbers)
            {
                s = Random.Range(0.05f, 0.11f);
                pvel[i] = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(-0.6f, -0.3f));
                palpha[i] = Random.Range(0.3f, 0.55f);
                pcol[i] = new Color(0.55f, 0.53f, 0.52f, palpha[i]);
            }
            else
            {
                // Dark: büyük, silik gri-mor dumanlar
                s = Random.Range(0.18f, 0.4f);
                pvel[i] = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.12f, 0.2f));
                palpha[i] = Random.Range(0.08f, 0.2f);
                pcol[i] = new Color(0.55f, 0.5f, 0.65f, palpha[i]);
            }
            SetSize(pt[i], circle, s, s);
            r.color = pcol[i];
        }
    }

    private void BuildLava()
    {
        int ash = BattleFxQuality.Budget(30);
        var builder = FindFirstObjectByType<BattleMapBuilder>();
        var pools = new List<Transform>();
        if (builder != null)
            foreach (var go in builder.Decor)
                if (go != null && go.name == "Lav gölü") pools.Add(go.transform);
        bool bubbles = !BattleFxQuality.Low;
        int per = bubbles ? 3 : 1;
        int maxPools = Mathf.Max(0, (MaxSprites - ash) / per);
        if (pools.Count > maxPools) pools.RemoveRange(maxPools, pools.Count - maxPools);

        glow = SoftSprite(64, 64);
        int nb = bubbles ? pools.Count * 2 : 0;
        br = new SpriteRenderer[nb]; bt = new Transform[nb]; bCenter = new Vector2[nb];
        bLife = new float[nb]; bT = new float[nb]; bWait = new float[nb];
        bCount = nb;
        for (int i = 0; i < pools.Count; i++)
        {
            Vector3 p = pools[i].position;
            var g = NewSr("LavParlama", glow, BattleMapBuilder.OrderGroundDecor + 1);
            g.transform.position = new Vector3(p.x, p.y + 0.5f, 0f);
            SetSize(g.transform, glow, 2.4f, 1.4f);
            g.color = new Color(1f, 0.45f, 0.1f, 0.25f);
            g.DOFade(0.55f, Random.Range(1.2f, 2f)).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(g.gameObject);
            if (!bubbles) continue;
            for (int k = 0; k < 2; k++)
            {
                int idx = i * 2 + k;
                var r = NewSr("LavKabarcik", circle, BattleMapBuilder.OrderGroundDecor + 3);
                r.color = new Color(1f, 0.75f, 0.35f, 0f); r.enabled = false;
                br[idx] = r; bt[idx] = r.transform;
                bCenter[idx] = new Vector2(p.x, p.y + 0.5f);
                bWait[idx] = Random.Range(0.2f, 2f); bT[idx] = -1f;
            }
        }
        BuildAsh(ash, true, 0.2f);
    }

    // ==================== GÜNCELLEME ====================

    private void Update()
    {
        if (Kind == BattleAtmosphere.None || root == null) return;
        float dt = Time.deltaTime, time = Time.time;
        if (pCount > 0) UpdateWrap(dt, time);
        if (fCount > 0) UpdateFog(dt);
        if (sCount > 0) UpdateSparkles(dt);
        if (bCount > 0) UpdateBubbles(dt);
        if (gloomT != null || vignT != null) UpdateOverlays();
    }

    private void UpdateWrap(float dt, float time)
    {
        ViewRect(out float l, out float b, out float w, out float h);
        const float m = 0.8f;
        float x0 = l - m, x1 = l + w + m, y0 = b - m, y1 = b + h + m;
        float ww = x1 - x0, hh = y1 - y0;
        for (int i = 0; i < pCount; i++)
        {
            var p = ppos[i];
            p += pvel[i] * dt;
            if (p.x < x0) p.x += ww; else if (p.x > x1) p.x -= ww;
            if (p.y < y0) p.y += hh; else if (p.y > y1) p.y -= hh;
            ppos[i] = p;
            float sway = Mathf.Sin(time * pfreq[i] + pphase[i]) * pamp[i];
            pt[i].position = new Vector3(p.x + sway, p.y, 0f);
            if (pflicker[i])
            {
                var c = pcol[i];
                c.a = palpha[i] * (0.55f + 0.45f * Mathf.Sin(time * 7f + pphase[i]));
                pr[i].color = c;
            }
        }
    }

    private void UpdateFog(float dt)
    {
        const float lo = -8f, hi = 8f;
        for (int i = 0; i < fCount; i++)
        {
            float x = fx[i] + fspeed[i] * dt;
            if (fspeed[i] > 0f && x - fhalf[i] > hi) x = lo - fhalf[i];
            else if (fspeed[i] < 0f && x + fhalf[i] < lo) x = hi + fhalf[i];
            fx[i] = x;
            ft[i].position = new Vector3(x, fy[i], 0f);
        }
    }

    private void UpdateSparkles(float dt)
    {
        for (int i = 0; i < sCount; i++)
        {
            if (sT[i] < 0f)
            {
                sWait[i] -= dt;
                if (sWait[i] > 0f) continue;
                if (!PickLandPoint(out Vector2 p)) { sWait[i] = 0.5f; continue; }
                st[i].position = new Vector3(p.x, p.y, 0f);
                sLife[i] = Random.Range(0.7f, 1.3f); sT[i] = 0f;
                sSize[i] = Random.Range(0.22f, 0.4f);
                st[i].rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));
                sr[i].enabled = true;
                continue;
            }
            sT[i] += dt;
            float k = sT[i] / sLife[i];
            if (k >= 1f) { sT[i] = -1f; sr[i].enabled = false; sWait[i] = Random.Range(0.4f, 2.2f); continue; }
            float s = Mathf.Sin(k * Mathf.PI);
            SetSize(st[i], star, sSize[i] * s, sSize[i] * s);
            var c = sr[i].color; c.a = s; sr[i].color = c;
        }
    }

    private bool PickLandPoint(out Vector2 p)
    {
        ViewRect(out float l, out float b, out float w, out float h);
        var bounds = BattleMapGeometry.Bounds;
        float x0 = Mathf.Max(l, bounds.xMin), x1 = Mathf.Min(l + w, bounds.xMax);
        float y0 = Mathf.Max(b, bounds.yMin), y1 = Mathf.Min(b + h, bounds.yMax);
        p = default;
        if (x1 <= x0 || y1 <= y0) return false;
        for (int t = 0; t < 8; t++)
        {
            p = new Vector2(Random.Range(x0, x1), Random.Range(y0, y1));
            if (BattleTerrain.IsLand(p)) return true;
        }
        return false;
    }

    private void UpdateBubbles(float dt)
    {
        for (int i = 0; i < bCount; i++)
        {
            if (bT[i] < 0f)
            {
                bWait[i] -= dt;
                if (bWait[i] > 0f) continue;
                var c = bCenter[i];
                bt[i].position = new Vector3(c.x + Random.Range(-0.4f, 0.4f), c.y + Random.Range(-0.15f, 0.15f), 0f);
                bLife[i] = Random.Range(0.7f, 1.2f); bT[i] = 0f;
                br[i].enabled = true;
                continue;
            }
            bT[i] += dt;
            float k = bT[i] / bLife[i];
            if (k >= 1f) { bT[i] = -1f; br[i].enabled = false; bWait[i] = Random.Range(0.6f, 2.2f); continue; }
            // Büyür; son çeyrekte patlayıp solar
            float s = 0.04f + 0.14f * Mathf.Min(1f, k / 0.75f);
            if (k > 0.75f) s *= 1f + (k - 0.75f) * 1.2f;
            SetSize(bt[i], circle, s, s);
            var col = br[i].color; col.a = k > 0.75f ? (1f - k) / 0.25f * 0.8f : 0.8f; br[i].color = col;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < sprites.Count; i++) if (sprites[i] != null) Destroy(sprites[i]);
        for (int i = 0; i < textures.Count; i++) if (textures[i] != null) Destroy(textures[i]);
        sprites.Clear(); textures.Clear();
    }
}
