using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Footprint-relative defender anchors. Positions can be overridden in the Inspector.</summary>
public sealed class BaseGarrisonVisual : MonoBehaviour
{
    public enum Role { DoorGuard, RoofArcher, PracticeArcher, CannonCrew, Monk }

    [Serializable]
    public struct Anchor
    {
        public Role role;
        [Tooltip("Cells from the bottom-left corner of the building footprint")]
        public Vector2 cell;
        public Anchor(Role kind, float x, float y) { role = kind; cell = new Vector2(x, y); }
    }

    [SerializeField] private Anchor[] anchors;
    [SerializeField] private Sprite arrowSprite;
    private readonly List<Animator> actors = new List<Animator>();
    private readonly List<Role> actorRoles = new List<Role>();
    private BuildingType type;
    private int sortingOrder;
    public int ActionCount { get; private set; }

    public IReadOnlyList<Anchor> Anchors => anchors;

    public void Initialize(BuildingType buildingType, int order)
    {
        type = buildingType;
        sortingOrder = order;
        if (anchors == null || anchors.Length == 0) anchors = Defaults(type);
        Build();
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded += OnBuildingUpgraded;
        StartCoroutine(AmbientActions());
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded -= OnBuildingUpgraded;
    }

    private static Anchor[] Defaults(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.MainBase:
                return new[] { new Anchor(Role.DoorGuard, 3.5f, -0.7f),
                    new Anchor(Role.DoorGuard, 5.5f, -0.7f),
                    new Anchor(Role.DoorGuard, 4.5f, -0.9f),
                    new Anchor(Role.RoofArcher, 1.6f, 3.25f),
                    new Anchor(Role.RoofArcher, 7.4f, 3.25f) };
            case BuildingType.ArcherTower:
                return new[] { new Anchor(Role.PracticeArcher, 1.1f, -0.2f),
                    new Anchor(Role.PracticeArcher, 2.8f, -0.2f) };
            case BuildingType.CannonTower:
                return new[] { new Anchor(Role.CannonCrew, 1.5f, 1.9f) };
            case BuildingType.Monastery:
                return new[] { new Anchor(Role.Monk, 1f, -0.35f) };
            default: return Array.Empty<Anchor>();
        }
    }

    private void OnBuildingUpgraded(BuildingType upgraded, int level)
    {
        if (upgraded == type) Build();
    }

    private void Build()
    {
        foreach (var actor in actors) if (actor != null) Destroy(actor.gameObject);
        actors.Clear();
        actorRoles.Clear();
        int level = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var building)
            ? building.level : 1;
        Vector2 footprint = BaseWorldLayout.Footprint(type);
        float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);
        var catalog = BaseVisualCatalog.Instance;
        for (int i = 0; i < anchors.Length; i++)
        {
            Anchor point = anchors[i];
            if (type == BuildingType.MainBase && point.role == Role.DoorGuard && i == 2 && level < 3)
                continue;
            var prefab = catalog?.garrisonPrefab;
            var actor = prefab != null ? Instantiate(prefab, transform) : new GameObject("GarrisonActor");
            actor.name = "Garrison_" + point.role;
            actor.transform.SetParent(transform, false);
            actor.transform.localPosition = new Vector3(
                (point.cell.x - footprint.x * 0.5f) / parentScale,
                (point.cell.y - footprint.y * 0.5f) / parentScale, -0.03f);
            actor.transform.localScale = Vector3.one * 0.72f / parentScale;
            var renderer = actor.GetComponent<SpriteRenderer>() ?? actor.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder + 30 + i;
            renderer.color = Color.white;
            var animator = actor.GetComponent<Animator>() ?? actor.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.runtimeAnimatorController = Controller(point.role, catalog, level);
            Play(animator, point.role == Role.Monk ? "Monk_Idle" :
                point.role == Role.DoorGuard || point.role == Role.CannonCrew
                    ? "Warrior_Idle" : "Archer_Idle", level);
            StartCoroutine(FitVisibleHeight(renderer, parentScale));
            actors.Add(animator);
            actorRoles.Add(point.role);
        }
    }

    private static IEnumerator FitVisibleHeight(SpriteRenderer renderer, float parentScale)
    {
        for (int frame = 0; frame < 8 && renderer != null && renderer.sprite == null; frame++)
            yield return null;
        if (renderer == null || renderer.sprite == null) yield break;
        float height = Mathf.Max(0.01f, renderer.sprite.bounds.size.y);
        renderer.transform.localScale = Vector3.one * (1.9f / (height * parentScale));
    }

    private static RuntimeAnimatorController Controller(Role role, BaseVisualCatalog catalog, int level)
    {
        if (catalog == null) return null;
        int index = BaseVisualCatalog.ColorIndex(level);
        RuntimeAnimatorController[] set = role == Role.Monk ? catalog.monkControllers
            : role == Role.DoorGuard || role == Role.CannonCrew ? catalog.warriorControllers
            : catalog.archerControllers;
        return set != null && index < set.Length ? set[index] : null;
    }

    private static void Play(Animator animator, string prefix, int level)
    {
        string[] colors = { "Blue", "Yellow", "Red", "Purple", "Black" };
        string state = prefix + "_" + colors[BaseVisualCatalog.ColorIndex(level)];
        if (animator != null && animator.runtimeAnimatorController != null &&
            animator.HasState(0, Animator.StringToHash(state)))
            animator.Play(state, 0, 0f);
    }

    private IEnumerator AmbientActions()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(4f, 6f));
            int level = GameManager.Instance?.Buildings != null &&
                GameManager.Instance.Buildings.TryGetValue(type, out var building)
                ? building.level : 1;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] == null) continue;
                Role role = actorRoles[i];
                if (role == Role.PracticeArcher)
                {
                    ActionCount++;
                    Play(actors[i], "Archer_Shoot", level);
                    FirePracticeArrow(actors[i].transform.position);
                }
                else if (role == Role.CannonCrew)
                {
                    ActionCount++;
                    Play(actors[i], "Warrior_Guard", level);
                    actors[i].transform.DOPunchPosition(Vector3.down * 0.12f, 0.55f);
                }
                else if (role == Role.Monk)
                {
                    ActionCount++;
                    Play(actors[i], "Monk_Run", level);
                    Vector3 home = actors[i].transform.localPosition;
                    actors[i].transform.DOLocalMoveX(home.x + 0.6f / Mathf.Max(0.01f, transform.lossyScale.x), 1.2f)
                        .SetLoops(2, LoopType.Yoyo);
                    yield return new WaitForSeconds(2.45f);
                    Play(actors[i], "Monk_Heal", level);
                    HealingSpark(actors[i].transform.position);
                }
                else if (role == Role.RoofArcher)
                {
                    ActionCount++;
                    actors[i].GetComponent<SpriteRenderer>().flipX =
                        !actors[i].GetComponent<SpriteRenderer>().flipX;
                }
            }
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] == null) continue;
                Role role = actorRoles[i];
                Play(actors[i], role == Role.Monk ? "Monk_Idle" :
                    role == Role.DoorGuard || role == Role.CannonCrew
                        ? "Warrior_Idle" : "Archer_Idle", level);
            }
        }
    }

    private void FirePracticeArrow(Vector3 origin)
    {
        AudioManager.Instance?.PlaySfx("arrowShoot");
        var arrow = new GameObject("PracticeArrow");
        arrow.transform.position = origin + Vector3.up * 0.25f;
        arrow.transform.localScale = Vector3.one * 0.35f;
        var renderer = arrow.AddComponent<SpriteRenderer>();
        renderer.sprite = arrowSprite ?? Resources.Load<Sprite>("Heroes/arrow")
            ?? BaseMapManager.MakeSquareSprite(Color.white);
        renderer.sortingOrder = sortingOrder + 40;
        Vector3 target = transform.position + new Vector3(-0.4f, -1.25f);
        arrow.transform.DOMove(target, 0.45f).SetEase(Ease.InQuad).OnComplete(() => Destroy(arrow));
    }

    private void HealingSpark(Vector3 origin)
    {
        var spark = new GameObject("MonkHealingSpark");
        spark.transform.position = origin + Vector3.up * 0.35f;
        spark.transform.localScale = Vector3.one * 0.27f;
        var renderer = spark.AddComponent<SpriteRenderer>();
        renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        renderer.color = new Color(0.65f, 1f, 0.8f);
        renderer.sortingOrder = sortingOrder + 40;
        spark.transform.DOMoveY(spark.transform.position.y + 0.55f, 0.55f);
        renderer.DOFade(0f, 0.55f).OnComplete(() => Destroy(spark));
    }
}
