using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Grid decoration placement and persistence, separate from building construction.</summary>
public sealed class DecorPlacementController : MonoBehaviour
{
    public static DecorPlacementController Instance { get; private set; }
    private readonly List<DecorInstance> placed = new List<DecorInstance>();
    private DecorData selected;
    private DecorInstance moving;
    private GameObject ghost;
    private SpriteRenderer ghostRenderer;
    private readonly List<SpriteRenderer> cells = new List<SpriteRenderer>();
    private Vector2Int currentCell;
    private bool valid;
    private bool moveArmed;
    private Vector2Int moveOrigin;
    private GameObject undoToast;
    private GameObject moveGrid;
    private GameObject confirmBar;
    private Button confirmButton;
    private GameObject actionPanel;
    private static Sprite square;

    public static DecorData[] All => Resources.LoadAll<DecorData>("Decor");
    public IReadOnlyList<DecorInstance> Placed => placed;
    public bool IsPlacing => selected != null;

    public void AddRecommendedClusters()
    {
        var decor = All;
        var clusters = new[]
        {
            ("tree", new Vector2Int(3, 17)), ("bush", new Vector2Int(5, 18)),
            ("rock", new Vector2Int(4, 19)),
            ("tree", new Vector2Int(17, 20)), ("bush", new Vector2Int(16, 22)),
            ("rock", new Vector2Int(18, 22)),
            ("tree", new Vector2Int(5, 28)), ("bush", new Vector2Int(7, 28)),
            ("rock", new Vector2Int(8, 29)),
            ("tree", new Vector2Int(14, 6)), ("bush", new Vector2Int(16, 7)),
            ("rock", new Vector2Int(13, 8))
        };
        for (int i = 0; i < clusters.Length; i++)
        {
            string id = "recommended_" + i;
            if (placed.Exists(item => item != null && item.InstanceId == id)) continue;
            var data = Array.Find(decor, item => item.id == clusters[i].Item1);
            if (data == null) continue;
            bool placedClusterItem = false;
            for (int radius = 0; radius <= 2 && !placedClusterItem; radius++)
            for (int dx = -radius; dx <= radius && !placedClusterItem; dx++)
            for (int dy = -radius; dy <= radius && !placedClusterItem; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius) continue;
                var cell = clusters[i].Item2 + new Vector2Int(dx, dy);
                if (!CanPlace(data, cell)) continue;
                Spawn(data, cell, id);
                placedClusterItem = true;
            }
        }
        Save();
    }

    private void Awake() => Instance = this;

    private void Start()
    {
        var records = GameManager.Instance?.PlayerData?.placedDecor;
        if (records == null) return;
        foreach (var record in records)
        {
            var data = Array.Find(All, item => item.id == record.decorId);
            if (data != null && CanPlace(data, new Vector2Int(record.x, record.y)))
                Spawn(data, new Vector2Int(record.x, record.y), record.instanceId);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanPlace(DecorData data, Vector2Int cell, DecorInstance except = null)
    {
        if (data == null || BaseMapManager.Instance == null) return false;
        var clean = BaseMapManager.Instance.BuildableRect;
        for (int y = cell.y; y < cell.y + data.footprint.y; y++)
        for (int x = cell.x; x < cell.x + data.footprint.x; x++)
        {
            var point = new Vector2Int(x, y);
            if (!clean.Contains(point)) return false;
            foreach (var building in UnityEngine.Object.FindObjectsByType<BuildingIdentifier>(FindObjectsSortMode.None))
            {
                var size = BaseWorldLayout.Footprint(building.buildingType);
                var center = building.transform.position;
                if (new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f,
                    size.x, size.y).Contains(new Vector2(x + 0.5f, y + 0.5f))) return false;
            }
            if (BuilderHut.IsInsideAny(new Vector2(x + 0.5f, y + 0.5f))) return false;
            foreach (var item in placed)
                if (item != null && item != except && item.Occupies(point)) return false;
        }
        return true;
    }

    public void BeginPlacement(DecorData data)
    {
        if (data == null) return;
        Cancel();
        selected = data;
        BaseWorldUI.Instance?.ToggleBottomButtons(false);
        CreateGhost();
        CreateConfirmBar();
    }

    public void BeginMove(DecorInstance item)
    {
        if (item == null) return;
        Cancel();
        selected = item.Data;
        BaseWorldUI.Instance?.ToggleBottomButtons(false);
        moving = item;
        moveArmed = true;
        moveOrigin = item.Cell;
        item.gameObject.SetActive(false);
        currentCell = item.Cell;
        CreateGhost();
        CreateMoveGrid();
        HideActions();
    }

    public void Cancel()
    {
        if (moving != null) moving.gameObject.SetActive(true);
        moving = null;
        moveArmed = false;
        selected = null;
        if (ghost != null) Destroy(ghost);
        if (moveGrid != null) Destroy(moveGrid);
        moveGrid = null;
        ghost = null;
        cells.Clear();
        if (confirmBar != null) Destroy(confirmBar);
        confirmBar = null;
        BaseWorldUI.Instance?.ToggleBottomButtons(true);
    }

    private void Update()
    {
        if (selected == null || ghost == null || Camera.main == null) return;
        Vector2 screen = Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed
            ? Touchscreen.current.primaryTouch.position.ReadValue()
            : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        if (screen != Vector2.zero && (confirmBar == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(
                confirmBar.GetComponent<RectTransform>(), screen)))
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(screen);
            currentCell = new Vector2Int(Mathf.FloorToInt(world.x - selected.footprint.x * 0.5f),
                Mathf.FloorToInt(world.y - selected.footprint.y * 0.5f));
        }
        valid = CanPlace(selected, currentCell, moving) &&
            (moving != null || GameManager.Instance?.Gold >= selected.cost);
        var center = new Vector3(currentCell.x + selected.footprint.x * 0.5f,
            currentCell.y + selected.footprint.y * 0.5f, -1f);
        ghost.transform.position = center;
        ghostRenderer.color = valid ? new Color(0.45f, 1f, 0.5f, 0.72f) :
            new Color(1f, 0.35f, 0.35f, 0.72f);
        foreach (var tile in cells)
            tile.color = valid ? new Color32(0x4F, 0xCF, 0x74, 0x59) :
                new Color32(0xC2, 0x45, 0x2D, 0x59);
        if (confirmButton != null) confirmButton.interactable = valid;
        if (moveArmed && (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasReleasedThisFrame ||
            Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame))
        {
            if (valid) Confirm();
            else Cancel();
        }
    }

    private static bool PointerOverUi(Vector2 screen)
    {
        if (EventSystem.current == null) return false;
        var eventData = new PointerEventData(EventSystem.current) { position = screen };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);
        foreach (var hit in hits)
            if (hit.gameObject.GetComponentInParent<Canvas>() != null) return true;
        return false;
    }

    private void Confirm()
    {
        if (!valid || selected == null) return;
        if (moving != null)
        {
            var moved = moving;
            var previous = moveOrigin;
            moved.SetCell(currentCell);
            moved.gameObject.SetActive(true);
            BaseBuildingVisual.SpawnDust(moved.transform.position, 130);
            if (Application.isMobilePlatform) Handheld.Vibrate();
            Save();
            Cancel();
            ShowUndo(moved, previous);
            return;
        }
        else
        {
            if (GameManager.Instance == null || !GameManager.Instance.SpendGold(selected.cost)) return;
            Spawn(selected, currentCell, Guid.NewGuid().ToString("N"));
        }
        Save();
        Cancel();
    }

    private void ShowUndo(DecorInstance moved, Vector2Int previous)
    {
        if (undoToast != null) Destroy(undoToast);
        var safe = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        if (safe == null) return;
        undoToast = new GameObject("DecorUndoToast", typeof(RectTransform), typeof(Image));
        undoToast.transform.SetParent(safe, false);
        var rect = undoToast.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 106f);
        rect.sizeDelta = new Vector2(150f, 48f);
        undoToast.GetComponent<Image>().color = Color.clear;
        var toast = undoToast;
        MakeButton(rect, "GERİ AL", Vector2.zero, () =>
        {
            if (moved != null && CanPlace(moved.Data, previous, moved))
            {
                moved.SetCell(previous);
                Save();
            }
            if (toast != null) Destroy(toast);
        });
        Destroy(toast, 3f);
    }

    public void Delete(DecorInstance item)
    {
        if (item == null || !placed.Remove(item)) return;
        int refund = item.Data.cost / 2;
        Destroy(item.gameObject);
        GameManager.Instance?.AddGold(refund);
        Save();
        HideActions();
    }

    public void ShowActions(DecorInstance item)
    {
        if (item == null || selected != null) return;
        HideActions();
        var parent = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        if (parent == null) return;
        actionPanel = new GameObject("DecorActions", typeof(RectTransform), typeof(Image));
        actionPanel.transform.SetParent(parent, false);
        var rect = actionPanel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(214f, 58f);
        rect.position = Camera.main.WorldToScreenPoint(item.transform.position + Vector3.up);
        var panelImage = actionPanel.GetComponent<Image>();
        panelImage.sprite = ReferenceUiVisuals.ToastSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = Color.white;
        MakeButton(rect, "TAŞI", new Vector2(-50f, 0f), () => BeginMove(item), false, 92f);
        MakeButton(rect, $"SİL +{item.Data.cost / 2}", new Vector2(50f, 0f), () => Delete(item), false, 92f);
    }

    private void HideActions()
    {
        if (actionPanel != null) Destroy(actionPanel);
        actionPanel = null;
    }

    private DecorInstance Spawn(DecorData data, Vector2Int cell, string id)
    {
        var go = new GameObject("Decor_" + data.id);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = data.Visual ?? Square;
        renderer.sortingOrder = 20 - cell.y;
        // Oranı koruyan tek ölçek; eskiden en ve boy ayrı ölçeklenip görsel yamuluyordu.
        go.transform.localScale = Vector3.one * data.ScaleFor(renderer.sprite);
        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = renderer.sprite.bounds.size;
        var item = go.AddComponent<DecorInstance>();
        item.Initialize(data, cell, id);
        if (data.animated && data.animatorController != null)
        {
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = data.animatorController;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        placed.Add(item);
        return item;
    }

    private void Save()
    {
        var player = GameManager.Instance?.PlayerData;
        if (player == null) return;
        player.placedDecor = new List<PlacedDecorSave>();
        foreach (var item in placed)
            if (item != null) player.placedDecor.Add(new PlacedDecorSave
            {
                instanceId = item.InstanceId, decorId = item.Data.id,
                x = item.Cell.x, y = item.Cell.y
            });
        GameManager.Instance.SaveGame();
    }

    private static Sprite Square => square != null ? square :
        square = BaseMapManager.MakeSquareSprite(Color.white);

    private void CreateMoveGrid()
    {
        var area = BaseMapManager.Instance.BuildableRect;
        moveGrid = new GameObject("DecorMoveGrid");
        void Line(float x, float y, float width, float height)
        {
            var line = new GameObject("GridLine");
            line.transform.SetParent(moveGrid.transform, false);
            line.transform.position = new Vector3(x, y, 0f);
            line.transform.localScale = new Vector3(width / Square.bounds.size.x,
                height / Square.bounds.size.y, 1f);
            var sr = line.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.sortingOrder = 98;
            sr.color = new Color(1f, 1f, 1f, 0.12f);
        }
        for (int x = area.xMin; x <= area.xMax; x++)
            Line(x, area.center.y, 0.025f, area.height);
        for (int y = area.yMin; y <= area.yMax; y++)
            Line(area.center.x, y, area.width, 0.025f);
    }

    private void CreateGhost()
    {
        ghost = new GameObject("DecorGhost");
        var visual = new GameObject("Visual");
        visual.transform.SetParent(ghost.transform, false);
        ghostRenderer = visual.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = selected.Visual ?? Square;
        ghostRenderer.sortingOrder = 130;
        float scale = selected.ScaleFor(ghostRenderer.sprite);
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = new Vector3(0f, selected.VerticalOffset(ghostRenderer.sprite, scale), 0f);
        for (int y = 0; y < selected.footprint.y; y++)
        for (int x = 0; x < selected.footprint.x; x++)
        {
            var tile = new GameObject("FootprintCell");
            tile.transform.SetParent(ghost.transform, false);
            tile.transform.localPosition = new Vector3(
                x + 0.5f - selected.footprint.x * 0.5f, y + 0.5f - selected.footprint.y * 0.5f, 0f);
            tile.transform.localScale = new Vector3(0.94f / Square.bounds.size.x, 0.94f / Square.bounds.size.y, 1f);
            var renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.sortingOrder = 129;
            cells.Add(renderer);
        }
    }

    private void CreateConfirmBar()
    {
        var parent = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        if (parent == null) return;
        confirmBar = new GameObject("DecorConfirmBar", typeof(RectTransform), typeof(Image));
        confirmBar.transform.SetParent(parent, false);
        var rect = confirmBar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0f);
        rect.anchorMax = new Vector2(0.85f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 14f);
        rect.sizeDelta = new Vector2(0f, 66f);
        var barImage = confirmBar.GetComponent<Image>();
        barImage.sprite = ReferenceUiVisuals.ToastSprite();
        barImage.type = Image.Type.Sliced;
        barImage.color = Color.white;
        // ✓/✗ simgeleri fontta yoktu ve boş kare görünüyordu: yazılı düğmeler.
        string confirmLabel = moving != null ? "YERLEŞTİR" : $"YERLEŞTİR · {selected.cost}";
        confirmButton = MakeButton(rect, confirmLabel, new Vector2(-62f, 0f), Confirm, true, 140f);
        MakeButton(rect, "İPTAL", new Vector2(80f, 0f), Cancel, false, 96f);
    }

    /// <summary>Ortak düğme görünümü (kabartmalı kenar, oyunun başlık fontu).</summary>
    private static Button MakeButton(Transform parent, string label, Vector2 offset,
        UnityEngine.Events.UnityAction action, bool gold = false, float width = 90f)
    {
        var button = ReferenceUiVisuals.Button(parent, label, label, gold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 14);
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(width, 44f);
        var text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.characterSpacing = 0.5f;
            text.fontSizeMin = 9f;
        }
        button.onClick.AddListener(action);
        return button;
    }
}

public sealed class DecorInstance : MonoBehaviour
{
    public DecorData Data { get; private set; }
    public Vector2Int Cell { get; private set; }
    public string InstanceId { get; private set; }
    private SpriteRenderer glow;
    private float nextClockCheck;
    private bool lit;
    private static Sprite glowSprite;

    public void Initialize(DecorData data, Vector2Int cell, string id)
    {
        Data = data;
        InstanceId = id;
        SetCell(cell);
        if (data.id == "lantern") CreateGlow();
    }

    public void SetCell(Vector2Int cell)
    {
        Cell = cell;
        var renderer = GetComponent<SpriteRenderer>();
        float offset = renderer != null && renderer.sprite != null
            ? Data.VerticalOffset(renderer.sprite, transform.localScale.y) : 0f;
        transform.position = new Vector3(cell.x + Data.footprint.x * 0.5f,
            cell.y + Data.footprint.y * 0.5f + offset, 0f);
    }

    public bool Occupies(Vector2Int point) => point.x >= Cell.x && point.y >= Cell.y &&
        point.x < Cell.x + Data.footprint.x && point.y < Cell.y + Data.footprint.y;

    private void Update()
    {
        if (glow == null) return;
        if (Time.unscaledTime >= nextClockCheck)
        {
            nextClockCheck = Time.unscaledTime + 10f;
            int hour = DateTime.Now.Hour;
            lit = hour >= 19 || hour < 6;
            glow.gameObject.SetActive(lit);
        }
        if (lit)
            glow.color = new Color(1f, 0.78f, 0.32f,
                0.24f + Mathf.Sin(Time.time * 3.8f) * 0.035f);
    }

    private void CreateGlow()
    {
        if (glowSprite == null)
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f,
                    Mathf.Pow(Mathf.Clamp01(1f - distance / 15f), 1.5f)));
            }
            texture.Apply();
            glowSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32),
                new Vector2(0.5f, 0.5f), 32f);
        }
        var light = new GameObject("LanternGlow");
        light.transform.SetParent(transform, false);
        light.transform.localPosition = new Vector3(0f, 0.35f / transform.localScale.y, 0f);
        light.transform.localScale = new Vector3(2f / transform.localScale.x,
            1.25f / transform.localScale.y, 1f);
        glow = light.AddComponent<SpriteRenderer>();
        glow.sprite = glowSprite;
        glow.sortingOrder = 8;
        glow.gameObject.SetActive(false);
    }
}
