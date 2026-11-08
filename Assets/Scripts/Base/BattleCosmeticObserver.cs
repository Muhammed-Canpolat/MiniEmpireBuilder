using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reads battle objects to drive optional visuals without changing combat decisions.</summary>
public sealed class BattleCosmeticObserver : MonoBehaviour
{
    private readonly Dictionary<TowerController, BattleGarrison> towers =
        new Dictionary<TowerController, BattleGarrison>();
    private readonly HashSet<int> seenProjectiles = new HashSet<int>();
    private HeroController hero;
    private MainBaseController mainBase;
    private float lastHeroHealth;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "BattleScene")
        {
            new GameObject("BattleCosmeticObserver").AddComponent<BattleCosmeticObserver>();
            new GameObject("BattleApprentice").AddComponent<BattleApprentice>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            new GameObject("BattleDebugOverlay").AddComponent<BattleDebugOverlay>();
#endif
        }
    }

    private void Update()
    {
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.3f;
            foreach (var tower in FindObjectsByType<TowerController>(FindObjectsSortMode.None))
            {
                if (towers.ContainsKey(tower)) continue;
                var garrison = tower.gameObject.AddComponent<BattleGarrison>();
                garrison.Initialize(tower.gameObject.name.Contains("Cannon")
                    ? BuildingType.CannonTower : BuildingType.ArcherTower);
                towers.Add(tower, garrison);
            }
            if (hero == null)
            {
                hero = FindFirstObjectByType<HeroController>();
                if (hero != null)
                {
                    lastHeroHealth = hero.CurrentHealth;
                    if (hero.GetComponent<BattleActorPresentation>() == null)
                        hero.gameObject.AddComponent<BattleActorPresentation>()
                            .Initialize(BattleActorPresentation.HeroHeight);
                }
            }
            if (mainBase == null)
            {
                mainBase = FindFirstObjectByType<MainBaseController>();
                if (mainBase != null && mainBase.GetComponent<BattleActorPresentation>() == null)
                    mainBase.gameObject.AddComponent<BattleActorPresentation>()
                        .Initialize(BattleActorPresentation.BaseHeight);
            }
            foreach (var prop in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (prop.name.StartsWith("Prop_")) prop.enabled = false;
        }

        // HeroController creates its Animator in Start, which can run after our first scan.
        if (hero != null)
        {
            var heroAnimator = hero.GetComponent<Animator>();
            if (heroAnimator != null &&
                heroAnimator.cullingMode != AnimatorCullingMode.CullUpdateTransforms)
                heroAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        foreach (var projectile in FindObjectsByType<ProjectileController>(FindObjectsSortMode.None))
        {
            if (projectile == null || projectile.gameObject.name != "TowerProjectile") continue;
            if (!seenProjectiles.Add(projectile.GetInstanceID())) continue;
            BattleGarrison nearest = null;
            float distance = 1.5f;
            foreach (var entry in towers)
            {
                if (entry.Key == null || entry.Value == null) continue;
                float value = Vector2.Distance(projectile.transform.position,
                    entry.Key.transform.position + Vector3.up * 0.5f);
                if (value < distance) { nearest = entry.Value; distance = value; }
            }
            nearest?.Fire();
        }
        if (hero != null && hero.CurrentHealth < lastHeroHealth)
        {
            var animator = hero.GetComponent<Animator>();
            if (animator != null)
                animator.Play(hero.CurrentHealth <= 0f ? "Death" : "Hit", 0, 0f);
            var sprite = hero.GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sprite.DOKill();
                if (hero.CurrentHealth <= 0f)
                    sprite.DOFade(0.2f, 0.35f);
                else
                    sprite.DOColor(new Color(1f, 0.52f, 0.52f), 0.075f)
                        .SetLoops(2, LoopType.Yoyo);
            }
        }
        if (hero != null) lastHeroHealth = hero.CurrentHealth;
    }
}

/// <summary>Enlarges only the rendered copy; combat transforms and colliders keep their size.</summary>
public sealed class BattleActorPresentation : MonoBehaviour
{
    public const float HeroHeight = 2.5f;
    public const float BaseHeight = 2.6f;

    private SpriteRenderer source;
    private SpriteRenderer visual;
    private float targetHeight;

    public void Initialize(float height)
    {
        targetHeight = height;
        source = GetComponent<SpriteRenderer>();
        if (source == null) { enabled = false; return; }
        var child = new GameObject("ReadableVisual");
        child.transform.SetParent(transform, false);
        visual = child.AddComponent<SpriteRenderer>();
        source.forceRenderingOff = true;
    }

    private void LateUpdate()
    {
        if (source == null || visual == null || source.sprite == null) return;
        source.forceRenderingOff = true;
        visual.sprite = source.sprite;
        visual.color = source.color;
        visual.flipX = source.flipX;
        visual.flipY = source.flipY;
        visual.sortingLayerID = source.sortingLayerID;
        visual.sortingOrder = source.sortingOrder;
        float originalHeight = Mathf.Max(0.01f,
            source.sprite.bounds.size.y * Mathf.Abs(transform.lossyScale.y));
        float factor = Mathf.Max(1f, targetHeight / originalHeight);
        visual.transform.localScale = Vector3.one * factor;
        visual.transform.localPosition = Vector3.up *
            (targetHeight - originalHeight) * 0.5f /
            Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
    }

    private void OnDestroy()
    {
        if (source != null) source.forceRenderingOff = false;
    }
}

public sealed class BattleGarrison : MonoBehaviour
{
    private BuildingType type;
    private Animator animator;
    private SpriteRenderer unitRenderer;
    private float idleAt;

    public void Initialize(BuildingType buildingType)
    {
        type = buildingType;
        var prefab = BaseVisualCatalog.Instance?.garrisonPrefab;
        var anchor = prefab != null ? Instantiate(prefab, transform) : new GameObject("GarrisonAnchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = new Vector3(0f, 0.72f, -0.01f);
        anchor.transform.localScale = Vector3.one * 0.65f;
        unitRenderer = anchor.GetComponent<SpriteRenderer>() ?? anchor.AddComponent<SpriteRenderer>();
        unitRenderer.sortingOrder = GetComponent<SpriteRenderer>()?.sortingOrder + 2 ?? 22;
        animator = anchor.GetComponent<Animator>() ?? anchor.AddComponent<Animator>();
        int level = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var data) ? data.level : 1;
        var levelSprite = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var building)
            ? building.VisualForLevel(level)?.sprite : null;
        var towerRenderer = GetComponent<SpriteRenderer>();
        if (levelSprite != null && towerRenderer != null)
        {
            towerRenderer.sprite = levelSprite;
            towerRenderer.color = Color.white;
        }
        animator.runtimeAnimatorController = BaseVisualCatalog.Instance?.GetUnitController(type, level);
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    public void Fire()
    {
        if (animator == null) return;
        EnemyController nearest = null;
        float distance = float.MaxValue;
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead) continue;
            float value = Vector2.Distance(enemy.transform.position, transform.position);
            if (value < distance) { nearest = enemy; distance = value; }
        }
        if (nearest != null) unitRenderer.flipX = nearest.transform.position.x < transform.position.x;
        int level = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var data) ? data.level : 1;
        string color = new[] { "Blue", "Yellow", "Red", "Purple", "Black" }
            [BaseVisualCatalog.ColorIndex(level)];
        animator.Play(type == BuildingType.ArcherTower
            ? $"Archer_Shoot_{color}" : $"Warrior_Attack1_{color}", 0, 0f);
        idleAt = Time.time + 0.4f;
        if (type == BuildingType.CannonTower) SpawnSmoke();
    }

    private void Update()
    {
        if (idleAt <= 0f || Time.time < idleAt || animator == null) return;
        idleAt = 0f;
        int level = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var data) ? data.level : 1;
        string color = new[] { "Blue", "Yellow", "Red", "Purple", "Black" }
            [BaseVisualCatalog.ColorIndex(level)];
        animator.Play(type == BuildingType.ArcherTower
            ? $"Archer_Idle_{color}" : $"Warrior_Idle_{color}");
    }

    private void SpawnSmoke()
    {
        var controller = BaseVisualCatalog.Instance?.dustController;
        if (controller == null) return;
        var smoke = new GameObject("CannonSmoke");
        smoke.transform.position = transform.position + Vector3.up * 0.85f;
        smoke.transform.localScale = Vector3.one * 0.42f;
        var sr = smoke.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.65f, 0.65f, 0.63f, 0.62f);
        sr.sortingOrder = unitRenderer.sortingOrder + 1;
        var effect = smoke.AddComponent<Animator>();
        effect.runtimeAnimatorController = controller;
        effect.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        Destroy(smoke, 0.55f);
    }
}
