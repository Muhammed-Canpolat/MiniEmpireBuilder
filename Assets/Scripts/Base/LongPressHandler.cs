using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Uzun basma algılayıcısı — 0.8 saniye basılı tutulunca OnLongPress tetiklenir.
/// Pointer önemli ölçüde hareket ederse iptal edilir.
/// </summary>
public class LongPressHandler : MonoBehaviour
{
    public static LongPressHandler Instance { get; private set; }

    [Header("Ayarlar")]
    public float HoldThreshold       = 0.8f;  // saniye
    public float MoveCancelThreshold = 22f;   // piksel

    /// <summary>Uzun basma algılanınca — hedef GameObject verilir</summary>
    public event Action<GameObject> OnLongPress;

    private bool       _pressing;
    private float      _pressTimer;
    private Vector2    _pressStartPos;
    private GameObject _pressTarget;

    /// <summary>Bu press cycle'ında uzun basma ateşlendi mi?</summary>
    public bool WasFiredThisPress { get; private set; }

    // ==================== UNITY YAŞAM DÖNGÜSÜ ====================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        Vector2 pos           = Vector2.zero;
        bool    pressedFrame  = false;
        bool    releasedFrame = false;

        if (Mouse.current != null)
        {
            pressedFrame  = Mouse.current.leftButton.wasPressedThisFrame;
            releasedFrame = Mouse.current.leftButton.wasReleasedThisFrame;
            pos           = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null)
        {
            pressedFrame  = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            releasedFrame = Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
            pos           = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (pressedFrame)
        {
            _pressing        = true;
            _pressTimer      = 0f;
            _pressStartPos   = pos;
            WasFiredThisPress = false;

            // Hedef objeyi bul
            Vector3 wp  = Camera.main.ScreenToWorldPoint(new Vector3(pos.x, pos.y, 0f));
            RaycastHit2D hit = Physics2D.Raycast(wp, Vector2.zero);
            _pressTarget = hit.collider != null ? hit.collider.gameObject : null;
        }

        if (_pressing)
        {
            // Hareket kontrolü
            if (Vector2.Distance(pos, _pressStartPos) > MoveCancelThreshold)
            {
                _pressing = false;
                return;
            }

            _pressTimer += Time.deltaTime;

            if (!WasFiredThisPress && _pressTimer >= HoldThreshold && _pressTarget != null)
            {
                WasFiredThisPress = true;
                _pressing         = false;
                OnLongPress?.Invoke(_pressTarget);
            }
        }

        if (releasedFrame)
            _pressing = false;
    }

    /// <summary>Dışarıdan fired bayrağını sıfırla</summary>
    public void ResetFiredFlag() => WasFiredThisPress = false;
}
