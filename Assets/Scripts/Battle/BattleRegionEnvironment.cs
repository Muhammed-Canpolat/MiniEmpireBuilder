using DG.Tweening;
using System.Collections;
using UnityEngine;

/// <summary>Campaign terrain treatment. Görsel katman savaşa dokunmaz; oynanış etkisi
/// yalnız "Sis" mekaniğinde eklenen <see cref="FogOfWar"/> bileşeninden gelir.</summary>
public sealed class BattleRegionEnvironment : MonoBehaviour
{
    private GameObject fog;
    private Texture2D fogTexture;
    private Sprite fogSprite;
    private Transform hero;

    private IEnumerator Start()
    {
        yield return null;
        RegionData region = BattleSession.Region;
        if (!BattleSession.IsCampaign || region == null || region.id == 1) yield break;
        var ground = GameObject.Find("BattleGround")?.GetComponent<SpriteRenderer>();
        if (ground != null)
        {
            if (region.groundTile != null) ground.sprite = region.groundTile;
            // Keep the source grass and leaf detail readable. The region tone contributes at most 15%.
            ground.color = Color.Lerp(Color.white, region.colorTone, 0.15f);
        }
        if (region.specialMechanic == "Sis" && GetComponent<FogOfWar>() == null)
            gameObject.AddComponent<FogOfWar>();
        if (region.id == 3) { AddMineScatter(); yield break; }
        if (region.id != 2) yield break;
        hero = FindFirstObjectByType<HeroController>()?.transform;
        AddForestWash();
        AddForestFloor();
        AddEdgeShadows();
        AddFog();
    }

    private void AddForestWash()
    {
        var wash = new GameObject("ForestGroundWash");
        wash.transform.SetParent(transform, false);
        wash.transform.position = new Vector3(0f, 0f, 0.95f);
        var renderer = wash.AddComponent<SpriteRenderer>();
        renderer.sprite = BaseMapManager.MakeSquareSprite(Color.white);
        // Hafif yeşil ton; eskiden %56 örtü bütün sahneyi düz yeşile boyuyordu.
        renderer.color = new Color(0.30f, 0.45f, 0.24f, 0.26f);
        renderer.sortingOrder = -99;
        wash.transform.localScale = new Vector3(60f, 60f, 1f);
    }

    private void AddForestFloor()
    {
        var sprite = BaseMapManager.MakeCircleSprite(Color.white);
        var random = new System.Random(2042);
        for (int i = 0; i < 90; i++)
        {
            float x = (float)random.NextDouble() * 30f - 15f;
            float y = (float)random.NextDouble() * 30f - 15f;
            var leaf = new GameObject("ForestLeafShadow");
            leaf.transform.SetParent(transform, false);
            leaf.transform.position = new Vector3(x, y, 0f);
            leaf.transform.localScale = new Vector3(0.22f, 0.09f, 1f);
            leaf.transform.rotation = Quaternion.Euler(0, 0, random.Next(0, 180));
            var renderer = leaf.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.05f, 0.11f, 0.055f, 0.34f);
            renderer.sortingOrder = -95;
        }
    }

    private void AddEdgeShadows()
    {
        Sprite tree = SpriteManager.Instance?.Tree ?? BaseMapManager.MakeCircleSprite(Color.white);
        for (int side = -1; side <= 1; side += 2)
        for (int i = -3; i <= 3; i++)
        {
            var shadow = new GameObject("ForestEdgeTreeShadow");
            shadow.transform.SetParent(transform, false);
            shadow.transform.position = new Vector3(side * 6.3f, i * 4.2f, 0f);
            shadow.transform.localScale = Vector3.one * (2.2f + (i & 1) * 0.5f);
            var renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sprite = tree;
            renderer.color = new Color(0.015f, 0.055f, 0.02f, 0.5f);
            renderer.sortingOrder = -80;
        }
    }

    private void AddFog()
    {
        const int width = 128;
        const int height = 224;
        fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        fogTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float worldX = (x + 0.5f - width * 0.5f) * 22f / width;
            float worldY = (y + 0.5f - height * 0.5f) * 38.5f / height;
            float radius = Mathf.Sqrt(worldX * worldX + worldY * worldY);
            // Merkez (≈5.5 birim) tamamen net, sis kenarlara doğru koyulaşır; renk gri-yeşil.
            float opacity = Mathf.SmoothStep(0f, 0.45f, Mathf.Clamp01((radius - 5.5f) / 4.5f));
            fogTexture.SetPixel(x, y, new Color(0.40f, 0.47f, 0.43f, opacity));
        }
        fogTexture.Apply();
        fogSprite = Sprite.Create(fogTexture, new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f), width / 22f);
        fog = new GameObject("ForestRadialFog");
        fog.transform.SetParent(transform, false);
        var renderer = fog.AddComponent<SpriteRenderer>();
        renderer.sprite = fogSprite;
        renderer.sortingOrder = 850;
    }

    private void LateUpdate()
    {
        if (fog != null)
        {
            Vector3 center = hero != null ? hero.position : Camera.main.transform.position;
            fog.transform.position = new Vector3(center.x, center.y, 0f);
        }
    }

    // ==================== TERK EDİLMİŞ MADEN (Bölge 3) ====================

    /// <summary>
    /// Kayalı toprak zemin üstünde: kenarlarda iki terk edilmiş maden girişi, parıldayan altın
    /// kümeleri, kayalar ve maden eşyaları. Ana Üs ve yuva çevresi (≈5 birim) boş kalır.
    /// Yalnız görsel: çarpışma yok, savaşa etkisi yok.
    /// </summary>
    private void AddMineScatter()
    {
        var setup = FindFirstObjectByType<BattleSceneSetup>();
        float halfW = setup != null && setup.worldWidth > 0f ? setup.worldWidth * 0.5f - 1.5f : 15f;
        float halfH = setup != null && setup.worldHeight > 0f ? setup.worldHeight * 0.5f - 1.5f : 22f;
        var random = new System.Random(3031);
        var used = new System.Collections.Generic.List<Vector2>();

        bool Free(Vector2 p, float spacing)
        {
            if (Mathf.Abs(p.x) < 5.4f && Mathf.Abs(p.y) < 5.4f) return false; // üs + yuvalar
            foreach (var other in used) if ((other - p).sqrMagnitude < spacing * spacing) return false;
            return true;
        }
        Vector2? Pick(float spacing)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var p = new Vector2((float)(random.NextDouble() * 2 - 1) * halfW, (float)(random.NextDouble() * 2 - 1) * halfH);
                if (!Free(p, spacing)) continue;
                used.Add(p);
                return p;
            }
            return null;
        }

        // Terk edilmiş maden girişleri: çapraz köşelere yakın.
        var entrance = BaseCustomArt.Get("mine_stage_2") ?? BaseCustomArt.Get("mine_gold_arch");
        if (entrance != null)
            foreach (var p in new[] { new Vector2(-halfW * 0.62f, halfH * 0.55f), new Vector2(halfW * 0.6f, -halfH * 0.5f) })
            {
                used.Add(p);
                Prop("AbandonedMine", entrance, p, 3.4f, new Color(0.82f, 0.78f, 0.74f), -60);
            }

        var catalog = BaseVisualCatalog.Instance;
        var gold = catalog?.goldStones;
        var rock = Resources.Load<Sprite>("BaseDecor/rock");
        var props = new System.Collections.Generic.List<Sprite>();
        foreach (var id in new[] { "crate", "barrel", "wood_logs" })
        {
            var data = System.Array.Find(DecorPlacementController.All, item => item.id == id);
            if (data?.Visual != null) props.Add(data.Visual);
        }
        var liftBox = BaseCustomArt.Get("mine_lift_box");
        if (liftBox != null) props.Add(liftBox);

        // Altın kümeleri: 2–3 taş, hafif parıltı.
        if (gold != null && gold.Length > 0)
            for (int i = 0; i < 14; i++)
            {
                var center = Pick(2.2f);
                if (center == null) break;
                int stones = 2 + random.Next(2);
                for (int s = 0; s < stones; s++)
                {
                    var sprite = gold[random.Next(gold.Length)];
                    var offset = new Vector2((float)(random.NextDouble() - 0.5) * 1.1f, (float)(random.NextDouble() - 0.5) * 0.7f);
                    var stone = Prop("GoldStone", sprite, center.Value + offset, 0.55f + (float)random.NextDouble() * 0.35f,
                        Color.white, -50);
                    if (s == 0) AddGlint(stone.transform, (float)random.NextDouble() * 2f);
                }
            }

        if (rock != null)
            for (int i = 0; i < 16; i++)
            {
                var p = Pick(1.6f);
                if (p == null) break;
                Prop("MineRock", rock, p.Value, 0.6f + (float)random.NextDouble() * 0.6f,
                    new Color(0.78f, 0.74f, 0.7f), -52);
            }

        for (int i = 0; i < 9 && props.Count > 0; i++)
        {
            var p = Pick(2f);
            if (p == null) break;
            Prop("MineProp", props[random.Next(props.Count)], p.Value, 0.9f + (float)random.NextDouble() * 0.4f,
                new Color(0.9f, 0.86f, 0.82f), -51);
        }
    }

    /// <summary>Genişliği verilen boyda, tabanı noktaya oturan dekor; altında yumuşak gölge.</summary>
    private GameObject Prop(string name, Sprite sprite, Vector2 basePoint, float width, Color tint, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = order;
        float scale = width / Mathf.Max(0.01f, sprite.bounds.size.x);
        go.transform.localScale = Vector3.one * scale;
        go.transform.position = new Vector3(basePoint.x, basePoint.y - sprite.bounds.min.y * scale, 0.5f);
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(go.transform, false);
        var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.25f);
        shadowRenderer.sortingOrder = order - 1;
        Vector2 circle = shadowRenderer.sprite.bounds.size;
        shadow.transform.localScale = new Vector3(width * 0.9f / (circle.x * scale), width * 0.28f / (circle.y * scale), 1f);
        shadow.transform.position = new Vector3(basePoint.x, basePoint.y + 0.05f, 0.5f);
        return go;
    }

    /// <summary>Altın taşının üstünde ara sıra beliren küçük parıltı.</summary>
    private static void AddGlint(Transform stone, float delay)
    {
        var glint = new GameObject("GoldGlint");
        glint.transform.SetParent(stone, false);
        glint.transform.localPosition = new Vector3(0.1f, 0.25f, 0f);
        var renderer = glint.AddComponent<SpriteRenderer>();
        renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        renderer.color = new Color(1f, 0.95f, 0.7f, 0f);
        renderer.sortingOrder = -40;
        float parent = Mathf.Max(0.01f, stone.lossyScale.x);
        glint.transform.localScale = Vector3.one * (0.16f / (renderer.sprite.bounds.size.x * parent));
        renderer.DOFade(0.85f, 0.35f).SetDelay(delay).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
            .SetLink(glint);
    }

    private void OnDestroy()
    {
        if (fogSprite != null) Destroy(fogSprite);
        if (fogTexture != null) Destroy(fogTexture);
    }
}
