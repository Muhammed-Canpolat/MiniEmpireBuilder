using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Üs sahnesi kamerası: tek parmak kaydırma, iki parmak sıkıştırma (editörde fare tekerleği),
/// harita sınırlarına (0..22 × 0..38) kenetlenme. En uzak mesafe = tüm harita genişliği (mevcut
/// çerçeve), en yakın = bunun %60'ı. Yerleştirme sırasında hayalet sürüklenirken (GhostDragActive)
/// kaydırma kapanır; hayalet ekran kenarına yaklaşınca <see cref="EdgeScroll"/> kamerayı kaydırır.
/// Kamera durumu kameranın kendisidir (WorldViewportBars yeniden çerçevelerse zoom da sıfırlanır).
/// </summary>
public sealed class BaseCameraController : MonoBehaviour
{
    public static BaseCameraController Instance { get; private set; }

    /// <summary>Yerleştirme denetleyicisi hayaleti sürüklüyorsa true: kamera kaydırması durur.</summary>
    public static bool GhostDragActive;

    public const float MinZoomFraction = 0.6f;
    public const float TapSlopPixels = 10f;
    public const float EdgeFraction = 0.08f;

    private Camera cam;
    private bool panning;
    private Vector2 lastPanPosition;
    private float lastPinchDistance;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GhostDragActive = false;
    }

    /// <summary>Tüm harita genişliği (ya da yüksekliği) sığan en uzak ortografik boyut.</summary>
    public float FullSize => cam == null ? 0f
        : Mathf.Max(BaseWorldLayout.Width * 0.5f / Mathf.Max(0.01f, cam.aspect), BaseWorldLayout.Height * 0.5f);

    private void Update()
    {
        if (cam == null) return;
        HandleInput();
        ClampView();
    }

    private void HandleInput()
    {
        if (TouchCount() >= 2)
        {
            panning = false;
            HandlePinch();
            return;
        }
        lastPinchDistance = 0f;

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(mouse))
                ZoomBy(Mathf.Pow(0.9f, scroll / 120f), mouse);
        }

        if (!ReadPointer(out Vector2 position, out bool pressed, out bool down, out _))
        {
            panning = false;
            return;
        }
        if (pressed)
        {
            panning = !IsOverUi(position);
            lastPanPosition = position;
            return;
        }
        if (!down) { panning = false; return; }
        if (panning && !GhostDragActive)
        {
            Vector3 before = cam.ScreenToWorldPoint(lastPanPosition);
            Vector3 after = cam.ScreenToWorldPoint(position);
            Vector3 p = cam.transform.position + (before - after);
            p.z = cam.transform.position.z;
            cam.transform.position = p;
        }
        lastPanPosition = position;
    }

    private void HandlePinch()
    {
        var touches = new List<Vector2>(2);
        foreach (var touch in Touchscreen.current.touches)
            if (touch.press.isPressed && touches.Count < 2) touches.Add(touch.position.ReadValue());
        if (touches.Count < 2) return;
        float distance = Vector2.Distance(touches[0], touches[1]);
        if (lastPinchDistance > 1f && distance > 1f)
            ZoomBy(lastPinchDistance / distance, (touches[0] + touches[1]) * 0.5f);
        lastPinchDistance = distance;
    }

    /// <summary>Boyutu çarpanla değiştirir (&lt;1 yaklaştırır); ekran noktasının altındaki dünya noktası sabit kalır.</summary>
    public void ZoomBy(float factor, Vector2 screenPoint)
    {
        if (cam == null) return;
        float full = FullSize;
        Vector3 before = cam.ScreenToWorldPoint(screenPoint);
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, full * MinZoomFraction, full);
        Vector3 after = cam.ScreenToWorldPoint(screenPoint);
        Vector3 p = cam.transform.position + (before - after);
        p.z = cam.transform.position.z;
        cam.transform.position = p;
        ClampView();
    }

    /// <summary>Hayalet ekran kenarının %8'i içindeyse kamerayı o yöne kaydırır (kenara yakınlıkla hızlanır).</summary>
    public void EdgeScroll(Vector2 screenPoint)
    {
        if (cam == null) return;
        Rect view = cam.pixelRect;
        float marginX = view.width * EdgeFraction;
        float marginY = view.height * EdgeFraction;
        float dx = 0f, dy = 0f;
        if (screenPoint.x < view.xMin + marginX) dx = -(1f - (screenPoint.x - view.xMin) / marginX);
        else if (screenPoint.x > view.xMax - marginX) dx = 1f - (view.xMax - screenPoint.x) / marginX;
        if (screenPoint.y < view.yMin + marginY) dy = -(1f - (screenPoint.y - view.yMin) / marginY);
        else if (screenPoint.y > view.yMax - marginY) dy = 1f - (view.yMax - screenPoint.y) / marginY;
        if (dx == 0f && dy == 0f) return;
        float speed = cam.orthographicSize * 1.1f * Time.unscaledDeltaTime;
        Vector3 p = cam.transform.position;
        p.x += Mathf.Clamp(dx, -1f, 1f) * speed;
        p.y += Mathf.Clamp(dy, -1f, 1f) * speed;
        cam.transform.position = p;
        ClampView();
    }

    /// <summary>Dünya noktası görüş alanının iç %80'inde değilse kamerayı ona ortalar.</summary>
    public void EnsureVisible(Vector2 world)
    {
        if (cam == null) return;
        Vector3 viewport = cam.WorldToViewportPoint(new Vector3(world.x, world.y, 0f));
        if (viewport.x > 0.1f && viewport.x < 0.9f && viewport.y > 0.1f && viewport.y < 0.9f) return;
        cam.transform.position = new Vector3(world.x, world.y, cam.transform.position.z);
        ClampView();
    }

    /// <summary>Boyutu [en yakın, en uzak] aralığında tutar; görüş alanını harita sınırlarına kenetler.</summary>
    public void ClampView()
    {
        if (cam == null) return;
        float full = FullSize;
        if (full <= 0f) return;
        float size = Mathf.Clamp(cam.orthographicSize, full * MinZoomFraction, full);
        if (!Mathf.Approximately(size, cam.orthographicSize)) cam.orthographicSize = size;
        float halfHeight = size;
        float halfWidth = size * cam.aspect;
        HudPaddingWorld(size, out float padL, out float padR, out float padT, out float padB);
        Vector3 p = cam.transform.position;
        // Görüş kenarı dünya sınırının HUD payı kadar ötesine gidebilir: her hücre HUD'un altından çıkarılabilir.
        p.x = ClampAxis(p.x, halfWidth, -padL, BaseWorldLayout.Width + padR, BaseWorldLayout.Width * 0.5f);
        p.y = ClampAxis(p.y, halfHeight, -padB, BaseWorldLayout.Height + padT, BaseWorldLayout.Height * 0.5f);
        cam.transform.position = p;
    }

    private static float ClampAxis(float center, float half, float low, float high, float fallback)
    {
        float min = low + half, max = high - half;
        // Pay yalnızca kaydırma aralığını genişletir; varsayılan/ortalı görünüm harita ortasıdır.
        if (min > max) return fallback;
        return Mathf.Clamp(center, min, max);
    }

    // ==================== HUD PAYI ====================

    private static readonly string[] HudIconNames = { "DailyButton", "QuestsButton" };
    private RectTransform[] hudIcons;
    private RectTransform hudTop, hudBottom;
    private float nextHudLookup;

    private void FindHud()
    {
        if (Time.unscaledTime < nextHudLookup) return;
        nextHudLookup = Time.unscaledTime + 1f;
        if (hudIcons == null) hudIcons = new RectTransform[HudIconNames.Length];
        for (int i = 0; i < HudIconNames.Length; i++)
            if (hudIcons[i] == null) hudIcons[i] = GameObject.Find(HudIconNames[i])?.GetComponent<RectTransform>();
        if (hudTop == null) hudTop = GameObject.Find("TopBarBG")?.GetComponent<RectTransform>();
        if (hudBottom == null) hudBottom = GameObject.Find("BottomDock")?.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Kamera görüş alanının üstüne binen HUD parçalarının (üst bar, alt bar, sol kenar simge düğmeleri)
    /// kapladığı payı dünya biriminde verir. Üst/alt bar zaten görüş alanının dışındaysa pay 0'dır.
    /// </summary>
    public void HudPaddingWorld(float orthoSize, out float left, out float right, out float top, out float bottom)
    {
        left = right = top = bottom = 0f;
        if (cam == null) return;
        FindHud();
        Rect view = cam.pixelRect;
        if (view.height <= 1f) return;
        float perPixel = orthoSize * 2f / view.height;
        var corners = new Vector3[4];
        if (hudIcons != null)
            foreach (var icon in hudIcons)
            {
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                icon.GetWorldCorners(corners);
                left = Mathf.Max(left, (corners[2].x - view.xMin) * perPixel);
            }
        if (hudTop != null && hudTop.gameObject.activeInHierarchy)
        {
            hudTop.GetWorldCorners(corners);
            top = Mathf.Max(0f, (view.yMax - corners[0].y) * perPixel);
        }
        if (hudBottom != null && hudBottom.gameObject.activeInHierarchy)
        {
            hudBottom.GetWorldCorners(corners);
            bottom = Mathf.Max(0f, (corners[2].y - view.yMin) * perPixel);
        }
        left = Mathf.Max(0f, left);
    }

    // ==================== GİRİŞ YARDIMCILARI ====================

    /// <summary>Dokunma ya da farenin bu karedeki durumu. Giriş yoksa false.</summary>
    public static bool ReadPointer(out Vector2 position, out bool pressed, out bool down, out bool released)
    {
        position = Vector2.zero;
        pressed = down = released = false;
        var touch = Touchscreen.current?.primaryTouch;
        if (touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
        {
            position = touch.position.ReadValue();
            pressed = touch.press.wasPressedThisFrame;
            down = touch.press.isPressed;
            released = touch.press.wasReleasedThisFrame;
            return true;
        }
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
        {
            position = mouse.position.ReadValue();
            pressed = mouse.leftButton.wasPressedThisFrame;
            down = mouse.leftButton.isPressed;
            released = mouse.leftButton.wasReleasedThisFrame;
            return true;
        }
        return false;
    }

    public static int TouchCount()
    {
        if (Touchscreen.current == null) return 0;
        int count = 0;
        foreach (var touch in Touchscreen.current.touches)
            if (touch.press.isPressed) count++;
        return count;
    }

    /// <summary>Ekran noktasında bir arayüz öğesi var mı (sanal joystick sayılmaz).</summary>
    public static bool IsOverUi(Vector2 screenPoint)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = screenPoint };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        foreach (var hit in results)
            if (hit.gameObject.name != "VirtualJoystickInput" && hit.gameObject.name != "VirtualJoystick")
                return true;
        return false;
    }
}

/// <summary>
/// Hayalet sürükleme kuralı (bina ve dekor yerleştirme ortak): hayalete (1 hücre paylı) basılıp
/// sürüklenirse hayalet taşınır ve kenara yaklaşınca kamera kayar; başka yere basılıp sürüklenirse
/// kamera kaydırılır; kısa dokunma boş zemine hayaleti ışınlar. Arayüze basılan dokunuş sayılmaz.
/// </summary>
public sealed class GhostPointerDrag
{
    public bool Dragging { get; private set; }
    private Vector2 pressStart;
    private bool tracking;
    private bool moved;

    /// <summary>Hayalet için yeni bir dünya noktası varsa true (hücre bundan hesaplanır).</summary>
    public bool Poll(Camera camera, Func<Vector2, bool> hitsGhost, out Vector3 world)
    {
        world = default;
        if (camera == null) return false;
        if (BaseCameraController.TouchCount() >= 2) { Reset(); return false; }
        bool has = BaseCameraController.ReadPointer(out Vector2 position, out bool pressed,
            out bool down, out bool released);
        if (!has)
        {
            if (Dragging || tracking) Reset();
            return false;
        }
        bool take = false;
        if (pressed)
        {
            pressStart = position;
            moved = false;
            tracking = !BaseCameraController.IsOverUi(position);
            Dragging = tracking && hitsGhost(camera.ScreenToWorldPoint(position));
        }
        if (down && tracking)
        {
            if (!moved && Vector2.Distance(position, pressStart) > BaseCameraController.TapSlopPixels) moved = true;
            if (Dragging)
            {
                world = camera.ScreenToWorldPoint(position);
                take = true;
                BaseCameraController.Instance?.EdgeScroll(position);
            }
        }
        if (released)
        {
            if (tracking && !Dragging && !moved)
            {
                world = camera.ScreenToWorldPoint(position);
                take = true;
            }
            Dragging = false;
            tracking = false;
        }
        BaseCameraController.GhostDragActive = Dragging;
        return take;
    }

    /// <summary>Parmak zaten basılıyken başlayan taşıma (uzun basma): hayalet hemen parmağı izler.</summary>
    public void BeginHeld()
    {
        if (!BaseCameraController.ReadPointer(out Vector2 position, out _, out bool down, out _) || !down)
        {
            Reset();
            return;
        }
        pressStart = position;
        tracking = true;
        moved = true;
        Dragging = true;
        BaseCameraController.GhostDragActive = true;
    }

    public void Reset()
    {
        Dragging = false;
        tracking = false;
        moved = false;
        BaseCameraController.GhostDragActive = false;
    }
}
