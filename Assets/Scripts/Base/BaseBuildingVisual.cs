using DG.Tweening;
using UnityEngine;

/// <summary>Cosmetic level and touch feedback for a placed base building.</summary>
public sealed class BaseBuildingVisual : MonoBehaviour
{
    private BuildingType type;
    private SpriteRenderer body;
    private Animator animator;
    private Vector3 restScale;
    private int currentLevel;

    public void Initialize(BuildingType buildingType, SpriteRenderer renderer, Vector3 scale)
    {
        type = buildingType;
        body = renderer;
        restScale = scale;
        currentLevel = BuildingInstance.LevelOf(gameObject, type);
        ApplyLevel(currentLevel);
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded += OnUpgraded;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded -= OnUpgraded;
    }

    private void ApplyLevel(int level)
    {
        if (type == BuildingType.GoldMine) return;
        currentLevel = level;
        if (GameManager.Instance?.Buildings == null ||
            !GameManager.Instance.Buildings.TryGetValue(type, out var data)) return;
        var visual = data.VisualForLevel(level);
        if (visual == null) return;
        if (visual.sprite != null) body.sprite = visual.sprite;
        body.color = Color.white;
        if (visual.animatorOverride != null)
        {
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = visual.animatorOverride;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.Rebind();
            animator.Play("Idle", 0, 0f);
        }
    }

    private void LateUpdate()
    {
        if (type == BuildingType.GoldMine || body == null || GameManager.Instance?.Buildings == null ||
            !GameManager.Instance.Buildings.TryGetValue(type, out var data)) return;
        // Some imported idle clips write their own first sprite after an override swap.
        var selected = data.VisualForLevel(currentLevel)?.sprite;
        if (selected != null && body.sprite != selected) body.sprite = selected;
    }

    private void OnUpgraded(BuildingType upgradedType, int eventLevel)
    {
        // Aynı türden iki bina olabilir: yalnız bu binanın seviyesi değiştiyse tepki ver.
        if (type != upgradedType) return;
        int level = BuildingInstance.LevelOf(gameObject, type);
        if (level == currentLevel) return;
        currentLevel = level;
        if (type != BuildingType.GoldMine && body != null)
        {
            var previous = new GameObject("LevelCrossfade");
            previous.transform.SetParent(transform, false);
            previous.transform.localPosition = Vector3.zero;
            var old = previous.AddComponent<SpriteRenderer>();
            old.sprite = body.sprite;
            old.color = body.color;
            old.sortingOrder = body.sortingOrder + 1;
            ApplyLevel(level);
            old.DOFade(0f, 0.4f).OnComplete(() => Destroy(previous));
        }
        else ApplyLevel(level);

        transform.DOKill();
        transform.localScale = restScale;
        transform.DOScale(restScale * 1.15f, 0.15f).SetEase(Ease.OutBack)
            .OnComplete(() => transform.DOScale(restScale, 0.15f).SetEase(Ease.OutBack));
        SpawnDust(transform.position + Vector3.down * 0.6f, body != null ? body.sortingOrder + 2 : 110);
        if (Camera.main != null)
            Camera.main.transform.DOShakePosition(0.15f, 0.055f, 8, 90f);
    }

    public void Tap()
    {
        transform.DOKill();
        transform.localScale = restScale;
        transform.DOScale(new Vector3(restScale.x * 1.08f, restScale.y * 0.92f, 1f), 0.075f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => transform.DOScale(restScale, 0.075f).SetEase(Ease.OutBack));
    }

    public void PlayConstructionComplete()
    {
        transform.DOKill();
        Vector3 landing = transform.position;
        transform.position = landing + Vector3.up * 2f;
        transform.localScale = restScale * 0.9f;
        transform.DOMove(landing, 0.3f).SetEase(Ease.InQuad)
            .OnComplete(() => SpawnDust(landing + Vector3.down * 0.5f,
                body != null ? body.sortingOrder + 2 : 110));
        transform.DOScale(restScale * 1.15f, 0.15f).SetEase(Ease.OutBack)
            .OnComplete(() => transform.DOScale(restScale, 0.15f).SetEase(Ease.OutBack));
    }

    public static void SpawnDust(Vector3 position, int order)
    {
        var controller = BaseVisualCatalog.Instance?.dustController;
        if (controller == null) return;
        var dust = new GameObject("TinySwordsDust");
        dust.transform.position = position;
        dust.transform.localScale = Vector3.one * 0.65f;
        var renderer = dust.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        var animation = dust.AddComponent<Animator>();
        animation.runtimeAnimatorController = controller;
        animation.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        Destroy(dust, 0.55f);
    }
}
