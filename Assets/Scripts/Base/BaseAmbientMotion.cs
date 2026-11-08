using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>Small, culled ambient motions for the base scene.</summary>
public sealed class BaseAmbientMotion : MonoBehaviour
{
    private static Sprite birdSprite;

    private void Start()
    {
        var catalog = BaseVisualCatalog.Instance;
        if (catalog == null) return;
        for (int i = 0; i < 3; i++)
        {
            Sprite sprite = catalog.cloudShadows != null && i < catalog.cloudShadows.Length
                ? catalog.cloudShadows[i] : null;
            if (sprite == null) continue;
            var cloud = new GameObject("CloudShadow");
            cloud.transform.SetParent(transform, false);
            cloud.transform.position = new Vector3(-4f - i * 8f, 9f + i * 8f, 0f);
            var renderer = cloud.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0f, 0f, 0f, 0.12f);
            renderer.sortingOrder = -7;
            float width = 5f + i * 0.8f;
            cloud.transform.localScale = Vector3.one *
                (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            cloud.transform.DOMoveX(26f, 45f + i * 13f).SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }
        for (int i = 0; i < 8; i++)
        {
            int kind = i % 4;
            Sprite sprite = catalog.bushes != null && kind < catalog.bushes.Length
                ? catalog.bushes[kind] : null;
            if (sprite == null) continue;
            var bush = new GameObject("AnimatedFenceBush");
            bush.transform.SetParent(transform, false);
            bush.transform.position = new Vector3(i % 2 == 0 ? 1.7f : 20.3f,
                4f + i * 4f, 0f);
            var renderer = bush.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 12;
            bush.transform.localScale = Vector3.one *
                (0.68f / Mathf.Max(0.01f, sprite.bounds.size.x));
            var controller = catalog.bushControllers != null && kind < catalog.bushControllers.Length
                ? catalog.bushControllers[kind] : null;
            if (controller != null)
            {
                var animator = bush.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }
        var flag = GameObject.Find("HeroCamp");
        if (flag != null)
            flag.transform.DORotate(new Vector3(0f, 0f, 3f), 1.2f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        StartCoroutine(BirdFlights());
    }

    private IEnumerator BirdFlights()
    {
        yield return new WaitForSeconds(5f);
        while (true)
        {
            for (int i = 0; i < 4; i++)
            {
                SpawnBird(i);
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(Random.Range(14f, 22f));
        }
    }

    private void SpawnBird(int index)
    {
        if (birdSprite == null) birdSprite = MakeBirdSprite();
        var bird = new GameObject("PassingBird");
        bird.transform.SetParent(transform, false);
        bird.transform.position = new Vector3(-2f, 29f - index * 0.7f, 0f);
        bird.transform.localScale = Vector3.one * (index % 2 == 0 ? 0.55f : 0.42f);
        var renderer = bird.AddComponent<SpriteRenderer>();
        renderer.sprite = birdSprite;
        renderer.color = new Color(0.12f, 0.12f, 0.11f, 0.58f);
        renderer.sortingOrder = 130;
        bird.transform.DOMoveX(24f, 6f + index * 0.3f).SetEase(Ease.Linear)
            .OnComplete(() => Destroy(bird));
    }

    private static Sprite MakeBirdSprite()
    {
        var texture = new Texture2D(16, 8, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        var pixels = new Color32[16 * 8];
        for (int x = 1; x < 8; x++) pixels[(7 - x) * 16 + x] = Color.black;
        for (int x = 8; x < 15; x++) pixels[(x - 8) * 16 + x] = Color.black;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 16, 8),
            new Vector2(0.5f, 0.5f), 16f);
    }
}
