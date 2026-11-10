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
        BaseMapManager.CreateReadableLabel(hut.transform, "İnşaatçı", false,
            sr.sprite.bounds.extents.y * scale + 0.34f, scale, $"{index + 1}. inşaatçı");
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
        worker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
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
            guest.workerRenderer.enabled = true;
            guest.worker.DOKill();
            guest.workerRenderer.flipX = target.x < guest.worker.position.x;
            guest.animator?.Play("Pawn_Run Hammer_" + guest.Color);
            var walker = guest;
            guest.worker.DOMove(target, 2.5f).SetSpeedBased().OnComplete(() =>
                walker.animator?.Play("Pawn_Interact Hammer_" + walker.Color));
            guest.workerRenderer.sortingOrder = 101 - Mathf.RoundToInt(target.y * 2f);
            return;
        }
        if (guest == null || !guest.busy) return;
        guest.busy = false;
        var leaving = guest;
        Vector3 home = GuestHome();
        leaving.worker.DOKill();
        leaving.workerRenderer.flipX = home.x < leaving.worker.position.x;
        leaving.animator?.Play("Pawn_Run Hammer_" + leaving.Color);
        leaving.worker.DOMove(home, 2.5f).SetSpeedBased().OnComplete(() =>
        {
            if (leaving.workerRenderer != null) leaving.workerRenderer.enabled = false;
        });
    }

    private void GoToWork(Unit unit, Vector3 target)
    {
        unit.sleeping = false;
        unit.zzz.SetActive(false);
        unit.workerRenderer.enabled = true;
        unit.worker.DOKill();
        unit.workerRenderer.flipX = target.x < unit.worker.position.x;
        unit.animator?.Play("Pawn_Run Hammer_" + unit.Color);
        unit.worker.DOMove(target, 2.5f).SetSpeedBased().OnComplete(() =>
            unit.animator?.Play("Pawn_Interact Hammer_" + unit.Color));
        unit.workerRenderer.sortingOrder = 101 - Mathf.RoundToInt(target.y * 2f);
    }

    private void GoHome(Unit unit)
    {
        Vector3 home = unit.hut.transform.position;
        unit.worker.DOKill();
        if (!unit.workerRenderer.enabled)
        {
            // Zaten kulübede: doğrudan uyu.
            unit.worker.position = home;
            unit.sleeping = true;
            unit.zzz.SetActive(true);
            return;
        }
        unit.workerRenderer.flipX = home.x < unit.worker.position.x;
        unit.animator?.Play("Pawn_Run Hammer_" + unit.Color);
        unit.worker.DOMove(home, 2.5f).SetSpeedBased().OnComplete(() =>
        {
            unit.workerRenderer.enabled = false;
            unit.zzz.SetActive(true);
            unit.sleeping = true;
        });
    }
}
