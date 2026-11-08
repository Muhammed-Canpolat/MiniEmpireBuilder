using UnityEngine;

/// <summary>Friendly support built on a battle socket; does not alter enemy AI.</summary>
public sealed class MonasteryHealer : MonoBehaviour
{
    private const float Range = 4f;
    private const float Interval = 3f;
    private Animator monk;
    private float nextHeal;

    private void Start()
    {
        int level = 1;
        if (GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(BuildingType.Monastery, out var data))
            level = Mathf.Max(1, data.level);
        var controllers = BaseVisualCatalog.Instance?.monkControllers;
        int index = BaseVisualCatalog.ColorIndex(level);
        if (controllers != null && index < controllers.Length && controllers[index] != null)
        {
            var unit = new GameObject("MonkGarrison");
            unit.transform.SetParent(transform, false);
            unit.transform.localPosition = new Vector3(0f, 1.8f, -0.01f);
            unit.transform.localScale = Vector3.one * 1.5f;
            var renderer = unit.AddComponent<SpriteRenderer>();
            var buildingRenderer = GetComponent<SpriteRenderer>();
            renderer.sortingOrder = buildingRenderer != null ? buildingRenderer.sortingOrder + 2 : 22;
            monk = unit.AddComponent<Animator>();
            monk.runtimeAnimatorController = controllers[index];
            monk.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        nextHeal = Time.time + Interval;
    }

    private void Update()
    {
        if (Time.time < nextHeal) return;
        nextHeal = Time.time + Interval;
        bool healed = false;
        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && hero.CurrentHealth > 0f &&
            Vector2.Distance(transform.position, hero.transform.position) <= Range &&
            hero.CurrentHealth < hero.MaxHealth)
        {
            hero.Heal(hero.MaxHealth * 0.03f);
            healed = true;
        }
        var mainBase = FindFirstObjectByType<MainBaseController>();
        if (mainBase != null && !mainBase.IsDestroyed &&
            Vector2.Distance(transform.position, mainBase.transform.position) <= Range &&
            mainBase.CurrentHealth < mainBase.MaxHealth)
        {
            mainBase.Heal(mainBase.MaxHealth * 0.03f);
            healed = true;
        }
        if (healed && monk != null)
        {
            int level = GameManager.Instance?.Buildings != null &&
                GameManager.Instance.Buildings.TryGetValue(BuildingType.Monastery, out var data)
                ? data.level : 1;
            string color = new[] { "Blue", "Yellow", "Red", "Purple", "Black" }
                [BaseVisualCatalog.ColorIndex(level)];
            monk.Play("Monk_Heal_" + color, 0, 0f);
        }
    }
}
