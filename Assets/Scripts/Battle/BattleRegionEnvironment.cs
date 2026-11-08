using System.Collections;
using UnityEngine;

/// <summary>Campaign terrain treatment only; it never changes combat or enemy state.</summary>
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
        renderer.color = new Color(0.37f, 0.51f, 0.29f, 0.56f);
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
            float opacity = Mathf.SmoothStep(0f, 0.45f, Mathf.Clamp01((radius - 5f) / 6f));
            fogTexture.SetPixel(x, y, new Color(0.08f, 0.12f, 0.1f, opacity));
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

    private void OnDestroy()
    {
        if (fogSprite != null) Destroy(fogSprite);
        if (fogTexture != null) Destroy(fogTexture);
    }
}
