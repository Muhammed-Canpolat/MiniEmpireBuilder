using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reads battle objects to drive optional visuals without changing combat decisions.</summary>
public sealed class BattleCosmeticObserver : MonoBehaviour
{
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
            new GameObject("BattleObjectiveHud").AddComponent<BattleObjectiveHud>();
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

        // Kule askerleri: garnizon BattleManager'da kurulur, ateşi TowerController tetikler.
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
    public const float HeroHeight = 1.75f;
    public const float BaseHeight = 2.6f;

    private SpriteRenderer source;
    private SpriteRenderer visual;
    private float targetHeight;
    private bool healthBarPlaced;

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
        if (!healthBarPlaced)
        {
            // Can çubuğu büyütülmüş görselin tepesinin hemen üstünde (başın ortasından geçmesin).
            healthBarPlaced = true;
            float top = targetHeight - originalHeight * 0.5f + 0.18f;
            foreach (var bar in FindObjectsByType<HealthBar>(FindObjectsSortMode.None))
                if (bar.Target == transform) bar.Offset = new Vector3(0f, top, 0f);
        }
    }

    private void OnDestroy()
    {
        if (source != null) source.forceRenderingOff = false;
    }
}

/// <summary>
/// Savaş yapısındaki asker: Okçu kulesinin tepesinde okçu, Topçu'da topçu, Manastır'ın
/// önünde keşiş. Yapının kendi seviyesinin rengini kullanır; ateş/iyileştirme animasyonu oynar.
/// </summary>
public sealed class BattleGarrison : MonoBehaviour
{
    private static readonly string[] Colors = { "Blue", "Yellow", "Red", "Purple", "Black" };
    private const float FrameHeight = 1.9f; // Tiny Swords karesi; çizili figür ≈ yarısı

    private BuildingType type;
    private int level = 1;
    private Animator animator;
    private SpriteRenderer unitRenderer;
    private float idleAt;

    private string Color => Colors[BaseVisualCatalog.ColorIndex(level)];
    private string IdleState => type == BuildingType.Monastery ? $"Monk_Idle_{Color}"
        : type == BuildingType.ArcherTower ? $"Archer_Idle_{Color}" : $"Warrior_Idle_{Color}";

    /// <param name="visualHeight">Yapının dünya birimindeki görünen boyu (asker konumu buna göre).</param>
    public void Initialize(BuildingType buildingType, int buildingLevel, float visualHeight)
    {
        type = buildingType;
        level = Mathf.Max(1, buildingLevel);
        var catalog = BaseVisualCatalog.Instance;
        var anchor = new GameObject("GarrisonUnit");
        anchor.transform.SetParent(transform, false);
        float parentScale = Mathf.Max(0.01f, transform.lossyScale.y);
        // Okçu kulenin tepesinde, topçu platformda, keşiş kapının önünde.
        Vector2 worldOffset = type == BuildingType.Monastery ? new Vector2(0.55f, -visualHeight * 0.30f)
            : type == BuildingType.ArcherTower ? new Vector2(0f, visualHeight * 0.20f)
            : new Vector2(0f, visualHeight * 0.12f);
        anchor.transform.localPosition = new Vector3(worldOffset.x / parentScale, worldOffset.y / parentScale, -0.01f);
        unitRenderer = anchor.AddComponent<SpriteRenderer>();
        var body = GetComponent<SpriteRenderer>();
        unitRenderer.sortingOrder = body != null ? body.sortingOrder + 2 : 22;
        animator = anchor.AddComponent<Animator>();
        RuntimeAnimatorController[] set = type == BuildingType.Monastery ? catalog?.monkControllers
            : type == BuildingType.ArcherTower ? catalog?.archerControllers : catalog?.warriorControllers;
        int index = BaseVisualCatalog.ColorIndex(level);
        animator.runtimeAnimatorController = set != null && index < set.Length ? set[index] : null;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (animator.runtimeAnimatorController != null) animator.Play(IdleState, 0, 0f);
        StartCoroutine(FitHeight(parentScale));
    }

    private System.Collections.IEnumerator FitHeight(float parentScale)
    {
        for (int frame = 0; frame < 8 && unitRenderer != null && unitRenderer.sprite == null; frame++)
            yield return null;
        if (unitRenderer == null || unitRenderer.sprite == null) yield break;
        float height = Mathf.Max(0.01f, unitRenderer.sprite.bounds.size.y);
        unitRenderer.transform.localScale = Vector3.one * (FrameHeight / (height * parentScale));
    }

    /// <summary>Kule ateş edince: en yakın düşmana dön, saldırı animasyonu, topçuda duman.</summary>
    public void Fire()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        EnemyController nearest = null;
        float distance = float.MaxValue;
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead) continue;
            float value = Vector2.Distance(enemy.transform.position, transform.position);
            if (value < distance) { nearest = enemy; distance = value; }
        }
        if (nearest != null && unitRenderer != null)
            unitRenderer.flipX = nearest.transform.position.x < transform.position.x;
        animator.Play(type == BuildingType.ArcherTower ? $"Archer_Shoot_{Color}" : $"Warrior_Attack1_{Color}", 0, 0f);
        idleAt = Time.time + 0.4f;
        if (type == BuildingType.CannonTower) SpawnSmoke();
    }

    /// <summary>Manastır iyileştirince keşişin iyileştirme animasyonu.</summary>
    public void PlayHeal()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.Play($"Monk_Heal_{Color}", 0, 0f);
        idleAt = Time.time + 0.8f;
    }

    private void Update()
    {
        if (idleAt <= 0f || Time.time < idleAt || animator == null) return;
        idleAt = 0f;
        animator.Play(IdleState);
    }

    private void SpawnSmoke()
    {
        var controller = BaseVisualCatalog.Instance?.dustController;
        if (controller == null) return;
        var smoke = new GameObject("CannonSmoke");
        smoke.transform.position = unitRenderer != null ? unitRenderer.transform.position + Vector3.up * 0.3f
            : transform.position + Vector3.up * 0.85f;
        smoke.transform.localScale = Vector3.one * 0.42f;
        var sr = smoke.AddComponent<SpriteRenderer>();
        sr.color = new UnityEngine.Color(0.65f, 0.65f, 0.63f, 0.62f);
        sr.sortingOrder = (unitRenderer != null ? unitRenderer.sortingOrder : 20) + 1;
        var effect = smoke.AddComponent<Animator>();
        effect.runtimeAnimatorController = controller;
        effect.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        Destroy(smoke, 0.55f);
    }
}
