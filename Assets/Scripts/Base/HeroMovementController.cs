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

    /// <summary>
    /// Harita değişti (bina kuruldu/taşındı, dekor): yeni hedef seçilir. 5. oturum (1.1): kahraman yeni yapının alanında kaldıysa en yakın
    /// boş durma yerine YÜRÜYEREK çekilir (bina onun üstüne kurulmaz gibi görünmez; ışınlanmaz).
    /// </summary>
    public void RefreshWalkable()
    {
        walking = false;
        follower = null;
        waitUntil = Time.time;
        evacuate = true;
    }

    private bool evacuate;
    /// <summary>Son çekilmede yürüyerek mi çıktı (test).</summary>
    public bool LastEvacuationWalked { get; private set; }

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
        // Tek kahraman: seviyenin renk kademesindeki Warrior (64 px/birim kareler: ölçek dengelenir).
        var warrior = weapon == WeaponType.Axe
            ? HeroLook.Controller(GameManager.Instance?.PlayerData?.hero?.level ?? 1) : null;
        if (warrior != null)
        {
            controller = warrior;
            // Bug 34: ölçek HeroLook'taki ekran oranından (kamera ortho'suna göre); kadraj oturunca yeniden uygulanır.
            fitWarrior = true;
            var circle = GetComponent<CircleCollider2D>();
            colliderWorldRadius = circle != null ? circle.radius * transform.localScale.x * HeroLook.BaseSceneScale : 0f;   // çarpışma görselle birlikte küçülür
            FitWarriorScale();
            StartCoroutine(RefitWarrior());
        }
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

    private bool fitWarrior;
    private float colliderWorldRadius;
    private float fittedOrtho;

    private void FitWarriorScale()
    {
        var cam = Camera.main;
        if (!fitWarrior || cam == null) return;
        fittedOrtho = cam.orthographicSize;
        float scale = HeroLook.WarriorScaleFor(fittedOrtho) * HeroLook.BaseSceneScale;
        transform.localScale = new Vector3(scale, scale, 1f);
        var circle = GetComponent<CircleCollider2D>();
        if (circle != null && colliderWorldRadius > 0f) circle.radius = colliderWorldRadius / scale;
    }

    private System.Collections.IEnumerator RefitWarrior()
    {
        for (int frame = 0; frame < 6; frame++) yield return null;
        FitWarriorScale();   // kamera kadrajı (duvar/çubuk) oturduktan sonra
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
        if (!placedOnce || (!walking && Time.time >= waitUntil))
            SelectNextTarget();
        PlayFrame(walking);
    }

    private void FixedUpdate()
    {
        if (!walking || (BaseWorldUI.Instance != null && BaseWorldUI.Instance.IsInfoPanelOpen)) return;
        Vector2 current = body != null ? body.position : (Vector2)transform.position;
        if (follower == null) { walking = false; return; }
        Vector2 next = follower.Tick(current, WalkSpeed * Time.fixedDeltaTime, out float facing);
        if (Mathf.Abs(facing) > 0.001f && spriteRenderer != null) spriteRenderer.flipX = facing < 0f;
        if (body != null) body.MovePosition(next);
        else transform.position = new Vector3(next.x, next.y, transform.position.z);
        if (follower.Done)
        {
            walking = false;
            follower = null;
            waitUntil = Time.time + Random.Range(1.5f, 4f);
        }
    }

    private GridPathFollower follower;
    private bool placedOnce;

    /// <summary>
    /// 4. oturum (1.1 + 3.4): kahramanın durabileceği hücre — kendisi ve çevresi (sol/sağ ve üst sıra) yürünebilir ve gövdesi hiçbir
    /// bina/dekor/kamp görseline binmiyor. Kahraman doluluk ızgarasının parçasıdır: kamp ve dekorların içinde durmaz.
    /// </summary>
    public static bool IsStandCell(Vector2Int c, GameObject self, List<Rect> visuals = null)
    {
        var grid = BaseWalkGrid.Shared;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = 0; dy <= 1; dy++)
                if (!grid.Walkable(new Vector2Int(c.x + dx, c.y + dy))) return false;
        var body = BodyRectAt(new Vector2(c.x + 0.5f, c.y + 0.5f), self);
        visuals = visuals ?? VisualRects(self);
        foreach (var r in visuals) if (BaseVisualBounds.Overlap(body, r, BaseVisualBounds.Tolerance, BaseVisualBounds.Tolerance)) return false;
        return true;
    }

    /// <summary>
    /// Kahramanın <paramref name="at"/> noktasında dururken kapladığı görsel dikdörtgen: gerçek sprite sınırı (kenar saydamlığı için %20
    /// daraltılmış), dönüşüm noktasına göre aynı göreli konumda. Sprite yoksa 1,5×1,6'lık varsayılan.
    /// </summary>
    public static Rect BodyRectAt(Vector2 at, GameObject self)
    {
        var sr = self != null ? self.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null) return new Rect(at.x - 0.75f, at.y - 0.45f, 1.5f, 1.6f);
        var b = sr.bounds;
        Vector2 offset = (Vector2)b.center - (Vector2)self.transform.position;
        Vector2 size = new Vector2(b.size.x * 0.6f, b.size.y * 0.8f);
        Vector2 center = at + offset;
        return new Rect(center - size * 0.5f, size);
    }

    /// <summary>Ana Üs nesnelerinin (bina, kulübe, dekor, kamp) görsel dikdörtgenleri; bir seçim turunda bir kez toplanır.</summary>
    public static List<Rect> VisualRects(GameObject self)
    {
        var list = new List<Rect>();
        foreach (var (_, go) in BaseVisualBounds.Collect())
            if (go != null && go != self) list.Add(BaseVisualBounds.Of(go));
        return list;
    }

    private void SelectNextTarget()
    {
        var grid = BaseWalkGrid.Shared;
        RectInt area = grid.Bounds;
        Vector2 here = body != null ? body.position : (Vector2)transform.position;
        var candidates = new List<Vector2Int>();
        var visuals = VisualRects(gameObject);
        for (int x = area.xMin + 1; x < area.xMax - 1; x++)
        for (int y = area.yMin + 1; y < area.yMax - 1; y++)
        {
            Vector2 cell = new Vector2(x + 0.5f, y + 0.5f);
            float d = Vector2.Distance(cell, campCenter);
            if (d > CampRadius || Vector2.Distance(cell, here) < 0.7f) continue;
            if (!IsStandCell(new Vector2Int(x, y), gameObject, visuals)) continue;
            candidates.Add(new Vector2Int(x, y));
        }
        if (evacuate && placedOnce)
        {
            evacuate = false;
            if (!IsStandCell(grid.CellOf(here), gameObject, visuals) && candidates.Count > 0)
            {
                candidates.Sort((p, q) => Vector2.Distance(grid.Center(p), here).CompareTo(Vector2.Distance(grid.Center(q), here)));
                target = grid.Center(candidates[0]);
                follower = new GridPathFollower(grid, here, target);
                LastEvacuationWalked = !follower.Done;
                if (follower.Done)
                {
                    follower = null;
                    if (body != null) body.position = target;
                    transform.position = new Vector3(target.x, target.y, transform.position.z);
                    waitUntil = Time.time + 1f;
                    return;
                }
                walking = true;
                return;
            }
        }
        if (!placedOnce)
        {
            // Başlangıçta bir binanın/bayrağın/dekorun üstündeyse en yakın boş durma yerine geçer.
            placedOnce = true;
            if (!IsStandCell(grid.CellOf(here), gameObject, visuals) && candidates.Count > 0)
            {
                candidates.Sort((p, q) => Vector2.Distance(grid.Center(p), here).CompareTo(Vector2.Distance(grid.Center(q), here)));
                Vector2 spot = grid.Center(candidates[0]);
                if (body != null) body.position = spot;
                transform.position = new Vector3(spot.x, spot.y, transform.position.z);
                waitUntil = Time.time + Random.Range(1f, 3f);
                return;
            }
        }
        if (candidates.Count == 0)
        {
            waitUntil = Time.time + 2f;
            return;
        }
        target = grid.Center(candidates[Random.Range(0, candidates.Count)]);
        follower = new GridPathFollower(grid, here, target);
        if (follower.Done) { follower = null; waitUntil = Time.time + 2f; return; }
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
