using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Serbest ızgarada yerleştirme (yeni bina inşası veya taşıma). Hayalet 1 hücrelik ızgaraya oturur,
/// geçerliyse yeşil / çakışıyorsa kırmızıdır; ONAY ve İPTAL düğmeleriyle tamamlanır.
/// Hayalete basılıp sürüklenir, başka yerde sürükleme kamerayı kaydırır (BaseCameraController).
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
    private Vector2Int _cell;
    private Vector3 _lastValidWorldPos;
    private bool _valid;
    private Vector3 _moveOrigin;
    private Vector3 _moveScale;
    private float _moveOriginalAlpha = 1f;
    private GameObject _moveGrid;
    private GameObject _footprint;
    private GameObject _undoToast;
    private GameObject _confirmBar;
    private Button _confirmButton;
    private TextMeshProUGUI _confirmText;
    private string _confirmLabel;
    private readonly GhostPointerDrag _drag = new GhostPointerDrag();

    private const float GoldMineMaxWorldSize = 2.2f;

    public bool IsPlacing => _isPlacing;

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
        _drag.Reset();

        // Başlangıç: eski düzenin boş slotu (güzel varsayılan), yoksa kamera merkezine en yakın boş hücre.
        Vector2Int size = BaseWorldLayout.Footprint(type);
        Vector2 view = Camera.main != null ? (Vector2)Camera.main.transform.position : new Vector2(11f, 19f);
        Vector2 center;
        if (BaseMapManager.Instance == null ||
            !BaseMapManager.Instance.TryGetDefaultPlacementCenter(size, view, out center, type))
            center = CenterOf(CellUnderPointer(view, size), size);
        _cell = CellOf(center, size);
        _lastValidWorldPos = CenterOf(_cell, size);

        CreateGhost(BaseMapManager.GetBuildingColor(type), SpriteManager.Instance?.GetBuildingSprite(type));
        CreateGrid();
        CreateConfirmBar();
        _ghostObj.transform.position = _lastValidWorldPos;
        _footprint.transform.position = _lastValidWorldPos;
        BaseWorldUI.Instance?.ToggleBottomButtons(false);
        BaseCameraController.Instance?.EnsureVisible(center);
    }

    /// <summary>Mevcut bir objeyi taşımak için yerleştirme başlatır</summary>
    public void BeginMove(PlacedMapObject obj)
    {
        if (_isPlacing) CancelPlacement();
        if (obj?.gameObject == null || obj.buildingType == BuildingType.MainBase) return;

        _isPlacing = true;
        _placingBuildingType = obj.buildingType ?? BuildingType.MainBase; // fallback (kulübe)
        _movingObject = obj;
        _buildDuration = 0f;
        _moveOrigin = obj.gameObject.transform.position;
        _moveScale = obj.gameObject.transform.localScale;
        _lastValidWorldPos = _moveOrigin;
        _cell = CellOf(_moveOrigin, BaseMapManager.FootprintOf(obj));
        obj.gameObject.transform.DOScale(_moveScale * 1.1f, 0.12f);
        _drag.BeginHeld(); // uzun basma parmağı hâlâ basılıysa hayalet hemen onu izler

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
        CreateGrid();
        CreateConfirmBar();
        _ghostObj.transform.position = _moveOrigin;
        _footprint.transform.position = _moveOrigin;
        _footprint.GetComponent<SpriteRenderer>().color = new Color32(0x4F, 0xCF, 0x74, 0x59);
        BaseWorldUI.Instance?.ToggleBottomButtons(false);
    }

    public void CancelPlacement()
    {
        if (!_isPlacing) return;

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
        FinishPlacement();
    }

    private void FinishPlacement()
    {
        if (_ghostObj != null) Destroy(_ghostObj);
        if (_moveGrid != null) Destroy(_moveGrid);
        if (_footprint != null) Destroy(_footprint);
        if (_confirmBar != null) Destroy(_confirmBar);
        _ghostObj = _moveGrid = _footprint = _confirmBar = null;
        _confirmButton = null;
        _confirmText = null;
        _drag.Reset();
        bool wasPlacing = _isPlacing;
        _isPlacing = false;
        _movingObject = null;
        if (wasPlacing) BaseWorldUI.Instance?.ToggleBottomButtons(true);
    }

    // ==================== UPDATE (GHOST HAREKET VE DURUM) ====================

    private void Update()
    {
        if (!_isPlacing || _ghostObj == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector2Int size = CurrentFootprint();
        if (_drag.Poll(cam, world => new Rect(_cell.x - 1f, _cell.y - 1f, size.x + 2f, size.y + 2f)
                .Contains(world), out Vector3 pointer))
            _cell = CellUnderPointer(pointer, size);

        _lastValidWorldPos = CenterOf(_cell, size);
        _ghostObj.transform.position = _lastValidWorldPos;
        _valid = IsPlacementValid(_lastValidWorldPos);

        _ghostRenderer.color = _valid ? new Color(0.2f, 1f, 0.2f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);
        if (_footprint != null)
        {
            _footprint.transform.position = _lastValidWorldPos;
            _footprint.GetComponent<SpriteRenderer>().color = _valid
                ? new Color32(0x4F, 0xCF, 0x74, 0x59)
                : new Color32(0xC2, 0x45, 0x2D, 0x59);
        }
        UpdateConfirmButton();
    }

    private bool IsPlacementValid(Vector3 center)
    {
        var map = BaseMapManager.Instance;
        if (map == null) return false;
        bool cellsOk = _movingObject != null
            ? map.CanPlaceObjectAt(_movingObject, center)
            : map.CanPlaceBuildingAt(_placingBuildingType, center);
        if (!cellsOk) return false;
        // 3. oturum (1.4): çatılar/büyük sprite'lar başka yapının görseline girmesin
        Rect visual;
        GameObject self = null;
        if (_movingObject != null && _movingObject.gameObject != null)
        {
            self = _movingObject.gameObject;
            visual = BaseVisualBounds.Of(self);
            visual.position += (Vector2)(center - self.transform.position);
        }
        else if (_ghostRenderer != null && _ghostObj != null)
        {
            visual = BaseVisualBounds.RendererRect(_ghostRenderer);
            visual.position += (Vector2)(center - _ghostObj.transform.position);
        }
        else return true;
        return !BaseVisualBounds.OverlapsAny(visual, self, _ghostObj);
    }

    /// <summary>Taşınan nesnenin (bina ya da kulübe) ya da yeni binanın kapladığı alan.</summary>
    private Vector2Int CurrentFootprint() => _movingObject != null
        ? BaseMapManager.FootprintOf(_movingObject)
        : BaseWorldLayout.Footprint(_placingBuildingType);

    private static Vector3 CenterOf(Vector2Int cell, Vector2Int size) =>
        new Vector3(cell.x + size.x * 0.5f, cell.y + size.y * 0.5f, -1f);

    private static Vector2Int CellOf(Vector2 center, Vector2Int size) => new Vector2Int(
        Mathf.RoundToInt(center.x - size.x * 0.5f), Mathf.RoundToInt(center.y - size.y * 0.5f));

    /// <summary>İşaretçinin altındaki sol-alt hücre (Floor(işaretçi - boyut/2)), harita sınırları içinde.</summary>
    private static Vector2Int CellUnderPointer(Vector2 world, Vector2Int size) => new Vector2Int(
        Mathf.Clamp(Mathf.FloorToInt(world.x - size.x * 0.5f), 0, BaseWorldLayout.Width - size.x),
        Mathf.Clamp(Mathf.FloorToInt(world.y - size.y * 0.5f), 0, BaseWorldLayout.Height - size.y));

    // ==================== ONAY ====================

    private bool CanAffordNewBuilding(out string reason)
    {
        reason = null;
        if (BuilderSystem.Instance == null || !BuilderSystem.Instance.HasAvailableBuilder())
        {
            reason = L.T("İnşaatçı meşgul", "Builder busy");
            return false;
        }
        if (GameManager.Instance == null || GameManager.Instance.Gold < _buildCost)
        {
            reason = L.T("Altın yetersiz", "Not enough gold");
            return false;
        }
        return true;
    }

    private void UpdateConfirmButton()
    {
        if (_confirmButton == null) return;
        string label = L.T("YERLEŞTİR", "PLACE");
        bool affordable = true;
        if (_movingObject == null)
        {
            affordable = CanAffordNewBuilding(out string reason);
            label = affordable ? L.T("YERLEŞTİR · ", "PLACE · ") + _buildCost : reason;
        }
        _confirmButton.interactable = _valid && affordable;
        if (_confirmText != null && label != _confirmLabel)
        {
            _confirmLabel = label;
            _confirmText.text = label;
        }
    }

    private void OnConfirmClicked()
    {
        if (!_isPlacing || !_valid) return;
        ConfirmPlacement(_lastValidWorldPos);
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
                if (Application.isMobilePlatform && AudioSettingsUI.VibrationEnabled) Handheld.Vibrate();
                ShowUndo(moved, previous);
            }
            return;
        }

        // Yeni inşa onayı: altın/inşaatçı yetmezse yerleştirme açık kalır, düğme zaten pasiftir.
        if (!CanAffordNewBuilding(out _))
        {
            GoldPopupManager.ShowGoldChange(0); // Yetersiz altın geri bildirimi
            return;
        }
        if (!GameManager.Instance.SpendGold(_buildCost)) return;

        string name = BaseMapManager.GetBuildingDisplayName(_placingBuildingType);
        // Bina benzersiz kimlikle oluşturulur; görev bu kimliğe bağlanır (inşa bitmeden yükseltilemez).
        PlacedMapObject completedBuilding = BaseMapManager.Instance?.SpawnBuilding(_placingBuildingType, pos);
        if (completedBuilding == null)
        {
            GameManager.Instance.AddGold(_buildCost); // beklenmedik: yer yok, bedel iade
            return;
        }
        BuilderSystem.Instance.TryStartTask(L.T(name + " İnşası", name + " Construction"), _buildDuration, pos,
            () => completedBuilding?.gameObject?.GetComponent<BaseBuildingVisual>()
                ?.PlayConstructionComplete(), completedBuilding.id ?? "");
        AudioManager.Instance?.PlaySfx("buildingPlace");
        BaseMapManager.Instance?.SaveAll();
        FinishPlacement();
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

    /// <summary>Açılmış arazide ızgara çizgileri + ghost'un kapladığı alan (yeşil/kırmızı).</summary>
    private void CreateGrid()
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
        var size = CurrentFootprint();
        _footprint.transform.localScale = new Vector3(size.x / square.bounds.size.x,
            size.y / square.bounds.size.y, 1f);
    }

    private void CreateConfirmBar()
    {
        var parent = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        if (parent == null) return;
        _confirmBar = new GameObject("PlacementConfirmBar", typeof(RectTransform), typeof(Image));
        _confirmBar.transform.SetParent(parent, false);
        var rect = _confirmBar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0f);
        rect.anchorMax = new Vector2(0.85f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 14f);
        rect.sizeDelta = new Vector2(0f, 66f);
        var barImage = _confirmBar.GetComponent<Image>();
        barImage.sprite = ReferenceUiVisuals.ToastSprite();
        barImage.type = Image.Type.Sliced;
        barImage.color = Color.white;
        _confirmLabel = _movingObject != null ? L.T("YERLEŞTİR", "PLACE")
            : L.T("YERLEŞTİR · ", "PLACE · ") + _buildCost;
        _confirmButton = DecorPlacementController.MakeButton(rect, _confirmLabel, new Vector2(-62f, 0f),
            OnConfirmClicked, true, 140f);
        _confirmText = _confirmButton.GetComponentInChildren<TextMeshProUGUI>();
        DecorPlacementController.MakeButton(rect, L.T("İPTAL", "CANCEL"), new Vector2(80f, 0f),
            CancelPlacement, false, 96f);
    }

    /// <summary>3. oturum (2.5): ortak süslü GERİ AL düğmesi (Ada da aynısını kullanır).</summary>
    private void ShowUndo(PlacedMapObject moved, Vector3 previous)
    {
        var safe = GameObject.Find("BaseCanvas")?.transform.Find("SafeArea");
        var button = UndoMoveButton.Show(safe, () =>
        {
            if (moved?.gameObject != null && BaseMapManager.Instance != null && BaseMapManager.Instance.MoveObjectTo(moved, previous))
                BaseBuildingVisual.SpawnDust(previous + Vector3.down * 0.4f, 110);
        });
        _undoToast = button != null ? button.gameObject : null;
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
