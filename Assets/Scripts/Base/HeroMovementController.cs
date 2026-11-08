using System.Collections.Generic;
using UnityEngine;

/// <summary>Base scene ambient hero movement around the camp. Battle movement is separate.</summary>
public class HeroMovementController : MonoBehaviour
{
    public static HeroMovementController Instance { get; private set; }

    private const float WalkSpeed = 1.2f;
    private const float CampRadius = 5f;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Animator visualAnimator;
    private bool? lastAnimatedMovement;
    private Sprite[] idleFrames;
    private Sprite[] runFrames;
    private Vector2 campCenter;
    private Vector2 target;
    private float waitUntil;
    private float animationClock;
    private bool walking;

    public void RefreshWalkable()
    {
        walking = false;
        waitUntil = Time.time;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        campCenter = transform.position;
        var weapon = GameManager.Instance?.PlayerData?.hero?.weaponType ?? WeaponType.Axe;
        int weaponIndex = weapon == WeaponType.Spear ? 1 : weapon == WeaponType.Bow ? 2 : 0;
        var controller = BaseVisualCatalog.Instance?.heroControllers != null
            && BaseVisualCatalog.Instance.heroControllers.Length > weaponIndex
            ? BaseVisualCatalog.Instance.heroControllers[weaponIndex] : null;
        if (controller != null)
        {
            visualAnimator = gameObject.AddComponent<Animator>();
            visualAnimator.runtimeAnimatorController = controller;
            visualAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            visualAnimator.Play("Idle");
        }
        string name = weapon == WeaponType.Spear ? "spear" : weapon == WeaponType.Bow ? "bow" : "axe";
        idleFrames = Resources.LoadAll<Sprite>($"Sprites/hero_{name}_idle");
        runFrames = Resources.LoadAll<Sprite>($"Sprites/hero_{name}_run");
        System.Array.Sort(idleFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        System.Array.Sort(runFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < idleFrames.Length; i++) idleFrames[i] = HeroPortraitSprite.Get(idleFrames[i]);
        for (int i = 0; i < runFrames.Length; i++) runFrames[i] = HeroPortraitSprite.Get(runFrames[i]);
        waitUntil = Time.time + Random.Range(1f, 3f);
    }

    private void Update()
    {
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = 101 - Mathf.RoundToInt(transform.position.y * 2f);
        if (BaseWorldUI.Instance != null && BaseWorldUI.Instance.IsInfoPanelOpen)
        {
            PlayFrame(false);
            return;
        }
        if (!walking && Time.time >= waitUntil)
            SelectNextTarget();
        PlayFrame(walking);
    }

    private void FixedUpdate()
    {
        if (!walking || (BaseWorldUI.Instance != null && BaseWorldUI.Instance.IsInfoPanelOpen)) return;
        Vector2 current = body != null ? body.position : (Vector2)transform.position;
        Vector2 next = Vector2.MoveTowards(current, target, WalkSpeed * Time.fixedDeltaTime);
        if (body != null) body.MovePosition(next);
        else transform.position = new Vector3(next.x, next.y, transform.position.z);
        if (Vector2.Distance(next, target) < 0.04f)
        {
            walking = false;
            waitUntil = Time.time + Random.Range(1f, 3f);
        }
    }

    private void SelectNextTarget()
    {
        RectInt area = BaseMapManager.Instance != null ? BaseMapManager.Instance.BuildableRect : new RectInt(0, 0, 0, 0);
        var candidates = new List<Vector2>();
        var occupiedFootprints = BaseWorldLayout.GetOccupiedFootprints();
        for (int x = area.xMin + 1; x < area.xMax - 1; x++)
        for (int y = area.yMin + 1; y < area.yMax - 1; y++)
        {
            Vector2 cell = new Vector2(x + 0.5f, y + 0.5f);
            if (Vector2.Distance(cell, campCenter) > CampRadius || Vector2.Distance(cell, campCenter) < 0.7f)
                continue;
            if (BaseWorldLayout.IsWithinBuildingFootprint(cell, occupiedFootprints)) continue;
            if (Physics2D.OverlapCircle(cell, 0.55f, LayerMask.GetMask("Default")) != null)
                continue;
            bool blocked = false;
            foreach (var hit in Physics2D.LinecastAll(transform.position, cell, LayerMask.GetMask("Default")))
                if (hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    blocked = true;
                    break;
                }
            if (blocked) continue;
            int steps = Mathf.CeilToInt(Vector2.Distance(transform.position, cell) * 2f);
            for (int step = 1; step <= steps; step++)
                if (BaseWorldLayout.IsWithinBuildingFootprint(Vector2.Lerp(transform.position, cell,
                    (float)step / steps), occupiedFootprints)) { blocked = true; break; }
            if (blocked) continue;
            candidates.Add(cell);
        }
        if (candidates.Count == 0)
        {
            waitUntil = Time.time + 2f;
            return;
        }
        target = candidates[Random.Range(0, candidates.Count)];
        spriteRenderer.flipX = target.x < transform.position.x;
        walking = true;
    }

    private void PlayFrame(bool moving)
    {
        if (visualAnimator != null)
        {
            if (lastAnimatedMovement != moving)
            {
                visualAnimator.Play(moving ? "Run" : "Idle", 0, 0f);
                lastAnimatedMovement = moving;
            }
            return;
        }
        Sprite[] frames = moving && runFrames.Length > 0 ? runFrames : idleFrames;
        if (frames == null || frames.Length == 0) return;
        animationClock += Time.deltaTime * (moving ? 8f : 5f);
        spriteRenderer.sprite = frames[Mathf.FloorToInt(animationClock) % frames.Length];
    }

    public void SetMoveInput(Vector2 direction) { }
    public void MoveToPosition(Vector3 worldPosition) { }
    public bool IsMoving => walking;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
