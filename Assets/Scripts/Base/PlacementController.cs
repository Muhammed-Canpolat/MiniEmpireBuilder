using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Yarı saydam ghost sprite ile yerleştirme (yeni bina inşa veya taşıma) modu.
/// </summary>
public class PlacementController : MonoBehaviour
{
    public static PlacementController Instance { get; private set; }

    private bool             _isPlacing;
    private BuildingType     _placingBuildingType;
    private PlacedMapObject  _movingObject;
    private GameObject       _ghostObj;
    private SpriteRenderer   _ghostRenderer;

    private float _buildDuration;
    private int   _buildCost;
    private bool  _hasClickedDownSinceStart;
    private Vector3 _lastValidWorldPos;


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

        _isPlacing           = true;
        _placingBuildingType = type;
        _movingObject        = null;
        _buildDuration       = buildDurationSeconds;
        _buildCost           = cost;
        _hasClickedDownSinceStart = false;

        CreateGhost(BaseMapManager.GetBuildingColor(type), SpriteManager.Instance?.GetBuildingSprite(type));
    }


    /// <summary>Mevcut bir objeyi taşımak için yerleştirme başlatır</summary>
    public void BeginMove(PlacedMapObject obj)
    {
        if (_isPlacing) CancelPlacement();
        if (obj?.gameObject == null) return;

        _isPlacing           = true;
        _placingBuildingType = obj.buildingType ?? BuildingType.MainBase; // fallback
        _movingObject        = obj;
        _buildDuration       = 0f;

        // Orijinal objeyi hafif saydam yap
        var sr = obj.gameObject.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color; c.a = 0.5f; sr.color = c;
        }

        CreateGhost(BaseMapManager.GetBuildingColor(_placingBuildingType), sr?.sprite);
    }

    public void CancelPlacement()
    {
        if (!_isPlacing) return;

        if (_ghostObj != null) Destroy(_ghostObj);

        // Taşıma iptaliyse orijinal rengi geri ver
        if (_movingObject?.gameObject != null)
        {
            var sr = _movingObject.gameObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color; c.a = 1f; sr.color = c;
            }
        }

        _isPlacing    = false;
        _movingObject = null;
    }

    // ==================== UPDATE (GHOST HAREKET VE ONAY) ====================

    private void Update()
    {
        if (!_isPlacing || _ghostObj == null) return;

        Vector2 pointerPos = Vector2.zero;
        bool    pressed    = false;
        bool    released   = false;
        bool    hasInput   = false;

        if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.delta.ReadValue().sqrMagnitude > 0))
        {
            pointerPos = Mouse.current.position.ReadValue();
            pressed    = Mouse.current.leftButton.wasPressedThisFrame;
            released   = Mouse.current.leftButton.wasReleasedThisFrame;
            hasInput = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            pointerPos = touch.position.ReadValue();
            pressed    = touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began;
            released   = touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled;
            hasInput = true;
        }

        if (pressed) _hasClickedDownSinceStart = true;

        if (hasInput)
        {
            _lastValidWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, 0f));
            _lastValidWorldPos.z = -1f; // Ghost objesi üstte
        }

        // Ghost'u fareye/parmağa takip ettir
        _ghostObj.transform.position = _lastValidWorldPos;

        if (!_hasClickedDownSinceStart) return; // Henüz yeni bir tıklama yapılmadıysa onayı bekle

        // Çakışma kontrolü
        bool canPlace = CheckCanPlace(_lastValidWorldPos);
        _ghostRenderer.color = canPlace ? new Color(0.2f, 1f, 0.2f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);


        if (released)
        {
            if (canPlace)
                ConfirmPlacement(_lastValidWorldPos);
            else
                CancelPlacement();
        }
    }

    private bool CheckCanPlace(Vector3 pos)
    {
        // UI üzerindeysek iptal (Basit mesafe/box kontrolü)
        if (IsPointerOverBlockingUI())
            return false;

        // Çakışma (Radius 1f, ghost boyutuna göre ayarlanabilir)
        Collider2D hit = Physics2D.OverlapCircle(pos, 0.7f);

        // Kendi objemizse sorun yok (Taşıma durumu)
        if (hit != null && _movingObject != null && hit.gameObject == _movingObject.gameObject)
            return true;

        return hit == null; // Çakışma yoksa true
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
            BaseMapManager.Instance?.MoveObjectTo(_movingObject, pos);
            CancelPlacement(); // Ghost silinir, renk 1f yapılır
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
                    BuilderSystem.Instance.TryStartTask(name + " İnşası", _buildDuration, pos);

                    // Binayı oluştur
                    BaseMapManager.Instance?.SpawnBuilding(_placingBuildingType, pos);

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

    private void CreateGhost(Color fallbackColor, Sprite realSprite = null)
    {
        _ghostObj = new GameObject("GhostPlacement");
        _ghostObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

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
}
