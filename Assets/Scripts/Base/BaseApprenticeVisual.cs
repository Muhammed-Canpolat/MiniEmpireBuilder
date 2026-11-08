using System.Collections;
using UnityEngine;

/// <summary>The apprentice works beside the mine and never wanders around the base.</summary>
public sealed class BaseApprenticeVisual : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer renderer;

    private IEnumerator Start()
    {
        var mine = FindFirstObjectByType<BaseMineVisual>();
        if (mine == null) { Destroy(gameObject); yield break; }
        // BaseMapManager animates new building scale from zero for 0.3 seconds.
        yield return new WaitForSeconds(0.35f);
        if (mine == null) { Destroy(gameObject); yield break; }
        transform.SetParent(mine.transform, false);
        float parentScale = Mathf.Max(0.01f, mine.transform.lossyScale.x);
        transform.localPosition = new Vector3(-0.52f / parentScale, -0.65f / parentScale, -0.04f);
        transform.localScale = Vector3.one * (0.92f / parentScale);
        renderer = gameObject.AddComponent<SpriteRenderer>();
        var mineBody = mine.transform.parent != null
            ? mine.transform.parent.GetComponent<SpriteRenderer>()
            : mine.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = mineBody != null ? mineBody.sortingOrder + 16 : 116;
        var collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.7f, 0.9f);
        animator = gameObject.AddComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        ApplyLevel(GameManager.Instance?.PlayerData?.apprenticeLevel ?? 1);
        if (GameManager.Instance != null)
            GameManager.Instance.OnApprenticeLevelUp += ApplyLevel;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnApprenticeLevelUp -= ApplyLevel;
    }

    private void ApplyLevel(int level)
    {
        if (animator == null) return;
        animator.runtimeAnimatorController = BaseVisualCatalog.Instance?.GetUnitController(
            BuildingType.GoldMine, level);
        string color = new[] { "Blue", "Yellow", "Red", "Purple", "Black" }
            [BaseVisualCatalog.ColorIndex(level)];
        animator.Play("Pawn_Interact Pickaxe_" + color, 0, 0f);
    }
}
