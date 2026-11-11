using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// İnşaatçıların haritadaki görünümü: her inşaatçının taşınabilir bir kulübesi ve bir işçisi vardır.
/// Boştaki işçi kulübesine dönüp uyur, görevdeki işçi hedef binaya yürüyüp çekiç sallar.
/// </summary>
public class BuilderVisualController : MonoBehaviour
{
    /// <summary>İşçi ölçeği (Tiny Swords pawn). Önceki 0.5'in ~1.7 katı: yürürken de rahat seçilir.</summary>
    private const float WorkerScale = 0.85f;
    private const float WalkSpeed = 2.5f;
    private const float HammerSeconds = 3.2f;

    private static readonly string[] PawnColors = { "Blue", "Yellow", "Red", "Purple", "Black" };

    private sealed class Unit
    {
        public int index;
        public BuilderHut hut;
        public Transform worker;
        public SpriteRenderer workerRenderer;
        public Animator animator;
        public GameObject zzz;
        public bool busy;
        public bool sleeping;
        public Coroutine routine;
        public Vector3 target;
        public string Color => PawnColors[index % PawnColors.Length];
    }

    private readonly List<Unit> units = new List<Unit>();
    private bool spawned;

    private IEnumerator Start()
    {
        // Binalar ve arazi bir kare sonra hazır olur; yeni kulübe boş yer ararken onlara bakar.
        yield return null;
        int count = GameManager.Instance != null ? GameManager.Instance.BuilderCount : 1;
        for (int i = 0; i < count; i++) SpawnUnit(i, false);
        spawned = true;
        BaseMapManager.Instance?.SaveAll();
        HeroMovementController.Instance?.RefreshWalkable();

        if (BuilderSystem.Instance != null) BuilderSystem.Instance.OnStatusChanged += UpdateVisuals;
        if (GameManager.Instance != null) GameManager.Instance.OnBuilderHired += HandleHired;
        UpdateVisuals();
    }

    private void OnDestroy()
    {
        if (BuilderSystem.Instance != null) BuilderSystem.Instance.OnStatusChanged -= UpdateVisuals;
        if (GameManager.Instance != null) GameManager.Instance.OnBuilderHired -= HandleHired;
        foreach (var unit in units)
            if (unit.worker != null) unit.worker.DOKill();
        if (guest?.worker != null) guest.worker.DOKill();
    }

    private void HandleHired(int total)
    {
        if (!spawned) return;
        for (int i = units.Count; i < total; i++) SpawnUnit(i, true);
        BaseMapManager.Instance?.SaveAll();
        HeroMovementController.Instance?.RefreshWalkable();
        UpdateVisuals();
    }

    // ==================== OLUŞTURMA ====================

    private Vector3 HutPosition(int index)
    {
        var saved = GameManager.Instance?.PlayerData?.builderHuts;
        if (saved != null && index < saved.Count && saved[index] != null && saved[index].hasPosition)
            return new Vector3(saved[index].x, saved[index].y, 0f);
        Vector3 home = BaseWorldLayout.Get("builder").center;
        if (index == 0 || BaseMapManager.Instance == null) return home;
        Vector3 near = units.Count > 0 && units[0].hut != null ? units[0].hut.transform.position : home;
        return BaseMapManager.Instance.NearestFreeSpot(BuilderHut.FootprintSize, near);
    }

    private void SpawnUnit(int index, bool celebrate)
    {
        var unit = new Unit { index = index, target = new Vector3(float.MaxValue, 0f, 0f) };
        var hut = new GameObject(index == 0 ? "BuilderHut" : "BuilderHut_" + (index + 1));
        hut.transform.position = HutPosition(index);

        var sr = hut.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteManager.Instance?.BuilderHut;
        if (sr.sprite == null) sr.sprite = BaseMapManager.MakeSquareSprite(new Color(0.25f, 0.45f, 0.75f));
        float scale = 2.6f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
        hut.transform.localScale = new Vector3(scale, scale, 1f);
        BaseMapManager.CreateBuildingShadow(hut.transform, BuilderHut.FootprintSize,
            hut.transform.localScale, sr.sortingOrder - 1);
        var hutCollider = hut.AddComponent<BoxCollider2D>();
        hutCollider.size = sr.sprite.bounds.size;
        BaseMapManager.CreateReadableLabel(hut.transform, L.T("İnşaatçı", "Builder"), false,
            sr.sprite.bounds.extents.y * scale + 0.34f, scale, L.T($"{index + 1}. inşaatçı", $"Builder {index + 1}"));
        hut.transform.Find("ReadableLabel")?.gameObject.SetActive(false);

        unit.hut = hut.AddComponent<BuilderHut>();
        unit.hut.Initialize(index, sr, hut.transform.Find("ZeminGolgesi"));
        unit.hut.SavePosition();
        BaseMapManager.Instance?.RegisterBuilderHut(hut, index);
        unit.zzz = CreateZzZ(hut.transform);

        CreateWorker(unit, hut.transform.position);
        unit.sleeping = true;
        unit.zzz.SetActive(true);
        units.Add(unit);

        if (celebrate)
        {
            hut.transform.localScale = Vector3.zero;
            hut.transform.DOScale(new Vector3(scale, scale, 1f), 0.35f).SetEase(Ease.OutBack);
            BaseBuildingVisual.SpawnDust(hut.transform.position + Vector3.down * 0.5f, sr.sortingOrder + 1);
        }
    }

    /// <summary>İşçi: her inşaatçının kendi renkte işçisi (mavi, sarı, kırmızı, mor, siyah).</summary>
    private static void CreateWorker(Unit unit, Vector3 position)
    {
        var worker = new GameObject("BuilderWorker_" + (unit.index + 1));
        worker.transform.position = position;
        worker.transform.localScale = new Vector3(WorkerScale, WorkerScale, 1f);
        unit.worker = worker.transform;
        unit.workerRenderer = worker.AddComponent<SpriteRenderer>();
        unit.workerRenderer.sprite = SpriteManager.Instance?.GetSprite("ui_builder_avatar")
            ?? BaseMapManager.MakeCircleSprite(new Color(0.3f, 0.6f, 1f));
        unit.workerRenderer.enabled = false;
        var controllers = BaseVisualCatalog.Instance?.pawnControllers;
        int colorIndex = unit.index % PawnColors.Length;
        if (controllers != null && colorIndex < controllers.Length && controllers[colorIndex] != null)
        {
            unit.animator = worker.AddComponent<Animator>();
            unit.animator.runtimeAnimatorController = controllers[colorIndex];
            unit.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            unit.animator.Play("Pawn_Idle Hammer_" + unit.Color);
        }
    }

    private static GameObject CreateZzZ(Transform hut)
    {
        var zzz = new GameObject("ZzzText");
        zzz.transform.SetParent(hut, false);
        zzz.transform.localPosition = new Vector3(0.3f, 0.55f, 0f);

        var canvas = zzz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        var crt = zzz.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(1.5f, 0.6f);
        crt.localScale = Vector3.one * 0.09f;

        var txt = zzz.AddComponent<TextMeshProUGUI>();
        txt.text = "zzZ";
        ReferenceUiVisuals.ApplyFont(txt, 5f, new Color(0.85f, 0.9f, 1f, 0.9f), bodyBold: true);
        txt.alignment = TextAlignmentOptions.Center;
        zzz.transform.DOLocalMoveY(0.75f, 1.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        txt.DOFade(0.25f, 1.8f).SetLoops(-1, LoopType.Yoyo);
        return zzz;
    }

    // ==================== DURUM ====================

    public void ShowHutLabel(BuilderHut hut)
    {
        if (hut == null) return;
        var label = hut.transform.Find("ReadableLabel");
        if (label == null) return;
        label.gameObject.SetActive(true);
        StartCoroutine(HideHutLabel(label.gameObject));
    }

    private IEnumerator HideHutLabel(GameObject label)
    {
        yield return new WaitForSeconds(2f);
        if (label != null) label.SetActive(false);
    }

    private void UpdateVisuals()
    {
        var builders = BuilderSystem.Instance?.Builders;
        foreach (var unit in units)
        {
            if (unit.hut == null || unit.worker == null) continue;
            bool busy = builders != null && unit.index < builders.Length && !builders[unit.index].isAvailable;
            Vector3 target = busy
                ? (builders[unit.index].targetPosition != Vector3.zero
                    ? builders[unit.index].targetPosition
                    : unit.hut.transform.position + Vector3.down)
                : unit.hut.transform.position;
            // Görev ve hedef aynıysa yürüyüşü her karede yeniden başlatma.
            if (busy == unit.busy && (target - unit.target).sqrMagnitude < 0.01f) continue;
            unit.busy = busy;
            unit.target = target;
            if (busy) GoToWork(unit, target);
            else GoHome(unit);
        }
        UpdateGuest(builders);
    }

    // ==================== MİSAFİR İNŞAATÇI (elmasla, süreli) ====================

    private Unit guest;

    private Vector3 GuestHome() => units.Count > 0 && units[0].hut != null
        ? units[0].hut.transform.position + new Vector3(0.9f, -0.7f, 0f)
        : BaseWorldLayout.Get("builder").center;

    /// <summary>
    /// Kulübesiz misafir işçi: iş varken birinci kulübenin yanından çıkıp işe yürür,
    /// iş bitince geri dönüp kaybolur.
    /// </summary>
    private void UpdateGuest(BuilderSystem.Builder[] builders)
    {
        int index = units.Count;
        bool busy = builders != null && index < builders.Length && !builders[index].isAvailable;
        if (busy)
        {
            if (guest == null)
            {
                guest = new Unit { index = index, target = new Vector3(float.MaxValue, 0f, 0f) };
                CreateWorker(guest, GuestHome());
                guest.worker.name = "GuestBuilderWorker";
            }
            Vector3 target = builders[index].targetPosition != Vector3.zero ? builders[index].targetPosition : GuestHome();
            if (guest.busy && (target - guest.target).sqrMagnitude < 0.01f) return;
            guest.busy = true;
            guest.target = target;
            StartWork(guest, target);
            return;
        }
        if (guest == null || !guest.busy) return;
        guest.busy = false;
        StartHome(guest, GuestHome());
    }

    private void GoToWork(Unit unit, Vector3 target)
    {
        unit.sleeping = false;
        unit.zzz.SetActive(false);
        StartWork(unit, target);
    }

    private void GoHome(Unit unit)
    {
        Vector3 home = unit.hut.transform.position;
        if (unit.routine != null) StopCoroutine(unit.routine);
        unit.routine = null;
        unit.worker.DOKill();
        if (!unit.workerRenderer.enabled)
        {
            // Zaten kulübede: doğrudan uyu.
            unit.worker.position = home;
            unit.sleeping = true;
            unit.zzz.SetActive(true);
            return;
        }
        StartHome(unit, home);
    }

    // ==================== YÜRÜYÜŞ VE ÇALIŞMA ====================

    private static RectInt HutCells(Unit unit) => unit.hut != null
        ? BaseOccupancyGrid.CellsFor((Vector2)BuilderHut.FootprintSize, unit.hut.transform.position)
        : new RectInt(0, 0, 0, 0);

    /// <summary>Hedef konumdaki bina/yapının hücre dikdörtgeni (bulunamazsa 1x1).</summary>
    private static RectInt TargetCells(Vector3 target)
    {
        var map = BaseMapManager.Instance;
        if (map != null)
            foreach (var obj in map.MapObjects)
            {
                if (obj?.gameObject == null || obj.isBeingRemoved) continue;
                if (obj.objectType != MapObjectType.Building && obj.objectType != MapObjectType.BuilderHut) continue;
                if (((Vector2)obj.gameObject.transform.position - (Vector2)target).sqrMagnitude > 0.04f) continue;
                return BaseOccupancyGrid.CellsFor((Vector2)BaseMapManager.FootprintOf(obj), target);
            }
        return new RectInt(Mathf.FloorToInt(target.x), Mathf.FloorToInt(target.y), 1, 1);
    }

    private void StartWork(Unit unit, Vector3 target)
    {
        if (unit.routine != null) StopCoroutine(unit.routine);
        unit.worker.DOKill();
        unit.workerRenderer.enabled = true;
        unit.routine = StartCoroutine(WorkRoutine(unit, target));
    }

    private void StartHome(Unit unit, Vector3 home)
    {
        if (unit.routine != null) StopCoroutine(unit.routine);
        unit.worker.DOKill();
        unit.routine = StartCoroutine(HomeRoutine(unit, home));
    }

    private IEnumerator HomeRoutine(Unit unit, Vector3 home)
    {
        bool hasHut = unit.hut != null;
        RectInt? goal = hasHut ? HutCells(unit) : (RectInt?)null;
        yield return Walk(unit, home, goal, new RectInt[0]);
        if (unit.workerRenderer != null) unit.workerRenderer.enabled = false;
        if (unit.zzz != null && hasHut) unit.zzz.SetActive(true);
        unit.sleeping = true;
        unit.routine = null;
    }

    /// <summary>Hedef yapının çevresinde 3-4 noktada durup çekiç sallar; iş bitene kadar döner.</summary>
    private IEnumerator WorkRoutine(Unit unit, Vector3 target)
    {
        RectInt site = TargetCells(target);
        RectInt[] startIgnore = unit.hut != null ? new[] { HutCells(unit) } : new RectInt[0];
        bool first = true;
        while (true)
        {
            var points = WorkPoints(site);
            if (points.Count == 0)
            {
                yield return Walk(unit, target, site, first ? startIgnore : new RectInt[0]);
                Face(unit, target);
                unit.animator?.Play("Pawn_Interact Hammer_" + unit.Color);
                yield break;
            }
            foreach (var point in points)
            {
                yield return Walk(unit, point, null, first ? startIgnore : new RectInt[0]);
                first = false;
                Face(unit, target);
                unit.animator?.Play("Pawn_Interact Hammer_" + unit.Color);
                BaseBuildingVisual.SpawnDust(unit.worker.position + new Vector3(Mathf.Sign(target.x - unit.worker.position.x + 0.001f) * 0.35f, 0.1f, 0f),
                    unit.workerRenderer.sortingOrder + 1);
                yield return new WaitForSeconds(HammerSeconds);
            }
        }
    }

    /// <summary>Yapı çevresindeki serbest durak noktaları: alt, sağ, üst, sol (boş olanlar).</summary>
    private static List<Vector3> WorkPoints(RectInt site)
    {
        var blocked = BaseOccupancyGrid.BlockedCells();
        var area = BaseWorldLayout.Buildable;
        var candidates = new[]
        {
            new Vector2Int(site.xMin + site.width / 2, site.yMin - 1),
            new Vector2Int(site.xMax, site.yMin + site.height / 2),
            new Vector2Int(site.xMin + site.width / 2, site.yMax),
            new Vector2Int(site.xMin - 1, site.yMin + site.height / 2)
        };
        var result = new List<Vector3>();
        foreach (var c in candidates)
            if (area.Contains(c) && !blocked.Contains(c))
                result.Add(new Vector3(c.x + 0.5f, c.y + 0.5f, 0f));
        return result;
    }

    private static void Face(Unit unit, Vector3 toward)
    {
        if (unit.workerRenderer != null && Mathf.Abs(toward.x - unit.worker.position.x) > 0.05f)
            unit.workerRenderer.flipX = toward.x < unit.worker.position.x;
    }

    /// <summary>Dolu hücrelerin etrafından dolanarak yürür; yol yoksa düz çizgi.</summary>
    private IEnumerator Walk(Unit unit, Vector3 dest, RectInt? goalFootprint, RectInt[] ignore)
    {
        Vector3 start = unit.worker.position;
        var path = BuilderPathfinder.FindPath(start, dest, goalFootprint, ignore);
        if (path == null)
            path = new List<Vector3> { start, dest };
        else if (goalFootprint.HasValue)
            path.Add(dest); // son adım: kulübeye / yapıya doğru
        unit.animator?.Play("Pawn_Run Hammer_" + unit.Color);
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 next = path[i];
            Face(unit, next);
            while ((unit.worker.position - next).sqrMagnitude > 0.0004f)
            {
                unit.worker.position = Vector3.MoveTowards(unit.worker.position, next, WalkSpeed * Time.deltaTime);
                unit.workerRenderer.sortingOrder = 101 - Mathf.RoundToInt(unit.worker.position.y * 2f);
                yield return null;
            }
        }
    }
}
