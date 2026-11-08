using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Yarı saydam ghost sprite ile yerleştirme (yeni bina inşa veya taşıma) modu.
/// </summary>
public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }

    private bool _isPlacing;
    private BuildingType _placingBuildingType;
    private PlacedMapObject _movingObject;
    private GameObject _ghostObj;
    private SpriteRenderer _ghostRenderer;

    private float _buildDuration;
    private int _buildCost;
    private bool _hasClickedDownSinceStart;
    private Vector3 _lastValidWorldPos;
    private Vector3 _moveOrigin;
    private Vector3 _moveScale;
    private float _moveOriginalAlpha = 1f;
    private GameObject _moveGrid;
    private GameObject _footprint;
    private GameObject _undoToast;

    private const float GoldMineMaxWorldSize = 2.2f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== API ====================

    /// <summary>Yeni bina inşası için yerleştirme başlatır</summary>
    public void BeginPlacement(BuildingType type, float buildDurationSeconds, int cost)
    {
        if (_isPlacing) CancelPlacement();

        _isPlacing = true;
        _placingBuildingType = type;
        _movingObject = null;
        _buildDuration = buildDurationSeconds;
        _buildCost = cost;
        _hasClickedDownSinceStart = false;

        CreateGhost(BaseMapManager.GetBuildingColor(type), SpriteManager.Instance?.GetBuildingSprite(type));
    }


    /// <summary>Mevcut bir objeyi taşımak için yerleştirme başlatır</summary>
    public void BeginMove(PlacedMapObject obj)
    {
        if (_isPlacing) CancelPlacement();
        if (obj?.gameObject == null || obj.buildingType == BuildingType.MainBase) return;

        _isPlacing = true;
        _placingBuildingType = obj.buildingType ?? BuildingType.MainBase; // fallback
        _movingObject = obj;
        _buildDuration = 0f;
        _hasClickedDownSinceStart = true;
        _moveOrigin = obj.gameObject.transform.position;
        _moveScale = obj.gameObject.transform.localScale;
        _lastValidWorldPos = _moveOrigin;
        obj.gameObject.transform.DOScale(_moveScale * 1.1f, 0.12f);

        // Orijinal objeyi hafif saydam yap. Maden gibi kök renderer'ı görünmez
        // (alfa 0) olan yapılarda alfa korunur; aksi halde gizli kare görünür olur.
        var sr = obj.gameObject.GetComponent<SpriteRenderer>();
        _moveOriginalAlpha = sr != null ? sr.color.a : 1f;
        if (sr != null)
        {
            Color c = sr.color; c.a = _moveOriginalAlpha * 0.5f; sr.color = c;
        }

        // Hayalet, binanın ekranda görünen asıl görselini gerçek boyutunda kullanır.
        SpriteRenderer visual = FindVisibleRenderer(obj.gameObject, sr);
        CreateGhost(BaseMapManager.GetBuildingColor(_placingBuildingType), visual?.sprite,
            visual != null ? visual.transform.lossyScale : (Vector3?)null);
        CreateMoveGrid();
        _ghostObj.transform.position = _moveOrigin;
        _footprint.transform.position = _moveOrigin;
        _footprint.GetComponent<SpriteRenderer>().color = new Color32(0x4F, 0xCF, 0x74, 0x59);
    }

    public void CancelPlacement()
    {
        if (!_isPlacing) return;

        if (_ghostObj != null) Destroy(_ghostObj);
        if (_moveGrid != null) Destroy(_moveGrid);
        if (_footprint != null) Destroy(_footprint);

        // Taşıma iptaliyse orijinal rengi geri ver
        if (_movingObject?.gameObject != null)
        {
            _movingObject.gameObject.transform.DOScale(_moveScale, 0.2f);
            var sr = _movingObject.gameObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color; c.a = _moveOriginalAlpha; sr.color = c;
            }
        }

        _isPlacing = false;
        _movingObject = null;
    }

    // ==================== UPDATE (GHOST HAREKET VE ONAY) ====================

    private void Update()
    {
        if (!_isPlacing || _ghostObj == null) return;

        Vector2 pointerPos = Vector2.zero;
        bool pressed = false;
        bool released = false;
        bool hasInput = false;

        if (Touchscreen.current != null &&
            (Touchscreen.current.primaryTouch.press.isPressed ||
             Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
        {
            var touch = Touchscreen.current.primaryTouch;
            pointerPos = touch.position.ReadValue();
            pressed = touch.press.wasPressedThisFrame;
            released = touch.press.wasReleasedThisFrame;
            hasInput = true;
        }
        else if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.delta.ReadValue().sqrMagnitude > 0))
        {
            pointerPos = Mouse.current.position.ReadValue();
            pressed = Mouse.current.leftButton.wasPressedThisFrame;
            released = Mouse.current.leftButton.wasReleasedThisFrame;
            hasInput = true;
        }

        if (pressed) _hasClickedDownSinceStart = true;

        if (hasInput)
        {
            Vector3 pointerWorld = Camera.main.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, 0f));
            if (_movingObject != null)
            {
                Vector2 size = BaseWorldLayout.Footprint(_placingBuildingType);
                _lastValidWorldPos = new Vector3(
                    Mathf.Floor(pointerWorld.x - size.x * 0.5f) + size.x * 0.5f,
                    Mathf.Floor(pointerWorld.y - size.y * 0.5f) + size.y * 0.5f, -1f);
            }
            else if (BaseMapManager.Instance != null && BaseMapManager.Instance.TryGetAvailableSlot(
                    _placingBuildingType, pointerWorld, null, out var slot))
                _lastValidWorldPos = slot.center;
            else
                _lastValidWorldPos = pointerWorld;
            _lastValidWorldPos.z = -1f; // Ghost objesi üstte
        }

        // Ghost'u fareye/parmağa takip ettir
        _ghostObj.transform.position = _lastValidWorldPos;

        if (!_hasClickedDownSinceStart) return; // Henüz yeni bir tıklama yapılmadıysa onayı bekle

        // Çakışma kontrolü
        bool canPlace = CheckCanPlace(_lastValidWorldPos);
        _ghostRenderer.color = canPlace ? new Color(0.2f, 1f, 0.2f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);
        if (_footprint != null)
        {
            _footprint.transform.position = _lastValidWorldPos;
            _footprint.GetComponent<SpriteRenderer>().color = canPlace
                ? new Color32(0x4F, 0xCF, 0x74, 0x59)
                : new Color32(0xC2, 0x45, 0x2D, 0x59);
        }


        if (released)
        {
            if (canPlace)
                ConfirmPlacement(_lastValidWorldPos);
            else
            {
                if (_movingObject != null && _ghostObj != null)
                {
                    var rejected = _ghostObj;
                    _ghostObj = null;
                    rejected.transform.DOMove(_moveOrigin, 0.2f)
                        .OnComplete(() => Destroy(rejected));
                }
                CancelPlacement();
            }
        }
    }

    private bool CheckCanPlace(Vector3 pos)
    {
        if (IsPointerOverBlockingUI()) return false;
        if (_movingObject != null)
            return BaseMapManager.Instance != null && BaseMapManager.Instance.CanPlaceBuildingAt(
                _placingBuildingType, pos, _movingObject);
        return BaseMapManager.Instance != null && BaseMapManager.Instance.TryGetAvailableSlot(
            _placingBuildingType, pos, _movingObject?.id, out var slot)
            && Vector2.Distance(pos, slot.center) < 0.1f;
    }
    private bool IsPointerOverBlockingUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            eventData.position = Touchscreen.current.touches[0].position.ReadValue();
        else if (Mouse.current != null)
            eventData.position = Mouse.current.position.ReadValue();
        else
            return false;

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

        foreach (var r in results)
        {
            if (r.gameObject.name != "VirtualJoystickInput" && r.gameObject.name != "VirtualJoystick")
            {
                return true;
            }
        }
        return false;
    }

    private void ConfirmPlacement(Vector3 pos)
    {
        if (_movingObject != null)
        {
            // Taşıma onayı
            var moved = _movingObject;
            Vector3 previous = _moveOrigin;
            bool success = BaseMapManager.Instance != null && BaseMapManager.Instance.MoveObjectTo(moved, pos);
            CancelPlacement(); // Ghost silinir, renk 1f yapılır
            if (success)
            {
                BaseBuildingVisual.SpawnDust(pos + Vector3.down * 0.4f, 110);
                AudioManager.Instance?.PlaySfx("buildingPlace");
                if (Application.isMobilePlatform) Handheld.Vibrate();
                ShowUndo(moved, previous);
            }
        }
        else
        {
            // Yeni inşaa onayı
            if (BuilderSystem.Instance != null && BuilderSystem.Instance.HasAvailableBuilder())
            {
                if (GameManager.Instance.SpendGold(_buildCost))
                {
                    // İnşaatçıyı meşgul et
                    string name = BaseMapManager.GetBuildingDisplayName(_placingBuildingType);
                    PlacedMapObject completedBuilding = null;
                    BuilderSystem.Instance.TryStartTask(name + " İnşası", _buildDuration, pos,
                        () => completedBuilding?.gameObject?.GetComponent<BaseBuildingVisual>()
                            ?.PlayConstructionComplete());

                    // Binayı oluştur
                    completedBuilding = BaseMapManager.Instance?.SpawnBuilding(_placingBuildingType, pos);
                    if (completedBuilding != null)
                        AudioManager.Instance?.PlaySfx("buildingPlace");

                    BaseMapManager.Instance?.SaveAll();
                }
                else
                {
                    GoldPopupManager.ShowGoldChange(0); // Yetersiz altın geri bildirimi
                }
            }
            else
            {
                Debug.LogWarning("[Placement] İnşaatçı müsait değil!");
            }


            Destroy(_ghostObj);
            _isPlacing = false;
        }
    }

    // ==================== YARDIMCI ====================

    /// <summary>Kök renderer görünmezse (maden) görünen en büyük alt görseli döndürür.</summary>
    private static SpriteRenderer FindVisibleRenderer(GameObject root, SpriteRenderer rootRenderer)
    {
        if (rootRenderer != null && rootRenderer.sprite != null && rootRenderer.color.a > 0.01f)
            return rootRenderer;
        SpriteRenderer best = null;
        float bestArea = 0f;
        foreach (var candidate in root.GetComponentsInChildren<SpriteRenderer>())
        {
            if (candidate == rootRenderer || candidate.sprite == null || candidate.color.a < 0.5f) continue;
            if (candidate.GetComponent<Animator>() != null) continue; // işçi/asker değil, yapının kendisi
            Vector3 size = candidate.bounds.size;
            float area = size.x * size.y;
            if (area > bestArea) { bestArea = area; best = candidate; }
        }
        return best;
    }

    private void CreateGhost(Color fallbackColor, Sprite realSprite = null, Vector3? worldScale = null)
    {
        _ghostObj = new GameObject("GhostPlacement");
        _ghostObj.transform.localScale = worldScale ?? CalculateGhostScale(_placingBuildingType, realSprite);

        _ghostRenderer = _ghostObj.AddComponent<SpriteRenderer>();
        _ghostRenderer.sortingOrder = 100;

        if (realSprite != null)
        {
            _ghostRenderer.sprite = realSprite;
        }
        else
        {
            Texture2D tex = new Texture2D(32, 32);
            Color[] cols = new Color[32 * 32];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols); tex.Apply();
            _ghostRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
        }

        _ghostRenderer.color = new Color(0.2f, 1f, 0.2f, 0.6f);
    }

    private void CreateMoveGrid()
    {
        var area = BaseMapManager.Instance.BuildableRect;
        var square = BaseMapManager.MakeSquareSprite(Color.white);
        _moveGrid = new GameObject("MoveGrid");
        void AddLine(float x, float y, float width, float height)
        {
            var line = new GameObject("GridLine");
            line.transform.SetParent(_moveGrid.transform, false);
            line.transform.position = new Vector3(x, y, 0f);
            line.transform.localScale = new Vector3(width / square.bounds.size.x,
                height / square.bounds.size.y, 1f);
            var sr = line.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.sortingOrder = 98;
            sr.color = new Color(1f, 1f, 1f, 0.12f);
        }
        for (int x = area.xMin; x <= area.xMax; x++)
            AddLine(x, area.center.y, 0.025f, area.height);
        for (int y = area.yMin; y <= area.yMax; y++)
            AddLine(area.center.x, y, area.width, 0.025f);
        _footprint = new GameObject("MoveFootprint");
        var renderer = _footprint.AddComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.sortingOrder = 99;
        var size = BaseWorldLayout.Footprint(_placingBuildingType);
        _footprint.transform.localScale = new Vector3(size.x / square.bounds.size.x,
            size.y / square.bounds.size.y, 1f);
    }

    private void ShowUndo(PlacedMapObject moved, Vector3 previous)
    {
        if (_undoToast != null) Destroy(_undoToast);
        var safe = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        if (safe == null) return;
        _undoToast = new GameObject("MoveUndoToast", typeof(RectTransform), typeof(Image), typeof(Button));
        _undoToast.transform.SetParent(safe, false);
        var rect = _undoToast.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(190f, 44f);
        rect.anchoredPosition = new Vector2(0f, 106f);
        _undoToast.GetComponent<Image>().color = ReferenceUiVisuals.Hex(0x2A2118);
        var captured = _undoToast;
        _undoToast.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (moved?.gameObject != null) BaseMapManager.Instance?.MoveObjectTo(moved, previous);
            if (captured != null) Destroy(captured);
        });
        var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(_undoToast.transform, false);
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var label = text.GetComponent<TextMeshProUGUI>();
        label.text = "Geri al";
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = ReferenceUiVisuals.Gold;
        Destroy(captured, 3f);
    }

    private Vector3 CalculateGhostScale(BuildingType type, Sprite sprite)
    {
        if (type != BuildingType.GoldMine || sprite == null)
            return new Vector3(0.9f, 0.9f, 1f);

        Vector2 size = sprite.bounds.size;
        float maxSize = Mathf.Max(size.x, size.y);
        if (maxSize <= 0.0001f)
            return new Vector3(0.9f, 0.9f, 1f);

        float scale = Mathf.Min(1f, GoldMineMaxWorldSize / maxSize);
        return new Vector3(scale, scale, 1f);
    }
}
