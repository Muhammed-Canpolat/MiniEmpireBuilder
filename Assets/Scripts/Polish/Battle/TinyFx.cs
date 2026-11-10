using UnityEngine;

/// <summary>
/// Tiny Swords efekt animasyonlarını (patlama, ateş) dünyada bir kez ya da süreli oynatır.
/// Denetleyiciler BaseVisualCatalog'dadır (explosionControllers, fireControllers).
/// </summary>
public static class TinyFx
{
    /// <summary>Animasyonlu efekt; <paramref name="height"/> dünya birimi yükseklik, life sn sonra silinir.</summary>
    public static GameObject Play(RuntimeAnimatorController controller, Vector3 position, float height,
        float life, int sortingOrder, Transform parent = null)
    {
        if (controller == null) return null;
        var go = new GameObject("TinyFx_" + controller.name);
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.Update(0f);
        // İlk karenin boyuna göre ölçek (Tiny Swords efekt kareleri 192 px).
        float frameHeight = renderer.sprite != null ? renderer.sprite.bounds.size.y : 3f;
        float scale = height / Mathf.Max(0.01f, frameHeight);
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            scale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
        if (life > 0f) Object.Destroy(go, life);
        return go;
    }

    public static RuntimeAnimatorController Pick(RuntimeAnimatorController[] set)
        => set != null && set.Length > 0 ? set[Random.Range(0, set.Length)] : null;

    /// <summary>Top mermisi isabeti: kısa patlama.</summary>
    public static void Explosion(Vector3 position, float height = 1.5f)
        => Play(Pick(BaseVisualCatalog.Instance?.explosionControllers), position, height, 0.9f, 60);

    /// <summary>Yıkılan yapının enkazında yanan ateş (enkazla birlikte silinir).</summary>
    public static void Fire(Vector3 position, Transform parent, float height = 1.1f)
        => Play(Pick(BaseVisualCatalog.Instance?.fireControllers), position, height, 0f, 8, parent);
}
