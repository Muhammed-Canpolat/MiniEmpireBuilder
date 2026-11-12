using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Ada görünümü kamerası (island_base_v2 §3): tek parmak kaydırma, iki parmak sıkıştırma (editörde fare tekerleği),
/// harita sınırlarına kenetlenme. Varsayılan yakınlık tam görünümün %70'i, en yakın %45. Savunmada kilitlenir ve
/// savaş çerçevesine (genişlik 22 u) kayar; sonuçtan sonra önceki görünüme döner. Ana Üs kamerasından (BaseWorldLayout'a
/// bağlı) ayrı tutuldu: Ana Üs davranışı değişmez.
/// </summary>
public sealed class IslandCameraController : MonoBehaviour
{
    public const float BoundsPad = 2.5f;

    private Camera cam;
    private bool panning, moving;
    private Vector2 lastPan;
    private float lastPinch;
    private Vector3 savedPosition;
    private float savedSize;

    /// <summary>Kilitliyken kaydırma/zum yok (savunma).</summary>
    public bool Locked { get; set; }

    public static IslandCameraController Attach(Camera camera)
    {
        var controller = camera.GetComponent<IslandCameraController>();
        if (controller == null) controller = camera.gameObject.AddComponent<IslandCameraController>();
        controller.cam = camera;
        return controller;
    }

    /// <summary>Tüm harita genişliği (ya da yüksekliği) sığan en uzak ortografik boyut.</summary>
    public float FullSize => cam == null ? 0f
        : Mathf.Max(IslandLayout.Width * 0.5f / Mathf.Max(0.01f, cam.aspect), IslandLayout.Height * 0.5f);

    /// <summary>Savaş çerçevesi: genişlik 22 u sığar (9:16'da ortho ≈19,6).</summary>
    public float BattleSize => cam == null ? 0f
        : Progression.Island.BattleFrameWidth * 0.5f / Mathf.Max(0.3f, cam.aspect);

    public Vector2 BattleCenter => new Vector2(IslandLayout.Width * 0.5f, IslandLayout.Height * 0.5f + 1.2f);   // üst HUD ≈3 u pay

    public void SetDefaultView()
    {
        cam.orthographicSize = Mathf.Max(0.1f, FullSize * Progression.Island.CameraDefaultZoom);
        // Kale görünümde üstte: açılış kaleden (üst-orta) başlar.
        var center = IslandLayout.CastleCenter + new Vector2(0f, -5.5f);
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
        ClampView();
    }

    public void SetFullView()
    {
        cam.orthographicSize = FullSize;
        cam.transform.position = new Vector3(IslandLayout.Width * 0.5f, IslandLayout.Height * 0.5f, cam.transform.position.z);
        ClampView();
    }

    public void SetBattleView()
    {
        cam.orthographicSize = BattleSize;
        cam.transform.position = new Vector3(BattleCenter.x, BattleCenter.y, cam.transform.position.z);
    }

    /// <summary>Savaşa girerken: görünümü sakla ve savaş çerçevesine kay.</summary>
    public void EnterBattle(float seconds)
    {
        savedPosition = cam.transform.position;
        savedSize = cam.orthographicSize;
        Locked = true;
        StopAllCoroutines();
        StartCoroutine(Glide(new Vector3(BattleCenter.x, BattleCenter.y, savedPosition.z), BattleSize, seconds, false));
    }

    /// <summary>Sonuçtan dönerken: önceki görünüme kay ve kilidi aç.</summary>
    public void ExitBattle(float seconds)
    {
        if (savedSize <= 0f) { Locked = false; return; }
        StopAllCoroutines();
        StartCoroutine(Glide(savedPosition, savedSize, seconds, true));
    }

    private IEnumerator Glide(Vector3 position, float size, float seconds, bool unlockAfter)
    {
        moving = true;
        Vector3 from = cam.transform.position;
        float fromSize = cam.orthographicSize;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, seconds));
            cam.transform.position = Vector3.Lerp(from, position, k);
            cam.orthographicSize = Mathf.Lerp(fromSize, size, k);
            yield return null;
        }
        cam.transform.position = position;
        cam.orthographicSize = size;
        moving = false;
        if (unlockAfter) { Locked = false; ClampView(); }
    }

    private void Update()
    {
        if (cam == null || Locked || moving) return;
        HandleInput();
        ClampView();
    }

    private void HandleInput()
    {
        if (BaseCameraController.TouchCount() >= 2)
        {
            panning = false;
            var touches = new List<Vector2>(2);
            foreach (var touch in Touchscreen.current.touches)
                if (touch.press.isPressed && touches.Count < 2) touches.Add(touch.position.ReadValue());
            if (touches.Count < 2) return;
            float distance = Vector2.Distance(touches[0], touches[1]);
            if (lastPinch > 1f && distance > 1f) ZoomBy(lastPinch / distance, (touches[0] + touches[1]) * 0.5f);
            lastPinch = distance;
            return;
        }
        lastPinch = 0f;

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(mouse)) ZoomBy(Mathf.Pow(0.9f, scroll / 120f), mouse);
        }

        if (!BaseCameraController.ReadPointer(out Vector2 position, out bool pressed, out bool down, out _)) { panning = false; return; }
        if (pressed) { panning = !IsOverUi(position); lastPan = position; return; }
        if (!down) { panning = false; return; }
        if (panning)
        {
            Vector3 before = cam.ScreenToWorldPoint(lastPan);
            Vector3 after = cam.ScreenToWorldPoint(position);
            var p = cam.transform.position + (before - after);
            p.z = cam.transform.position.z;
            cam.transform.position = p;
        }
        lastPan = position;
    }

    public void ZoomBy(float factor, Vector2 screenPoint)
    {
        Vector3 before = cam.ScreenToWorldPoint(screenPoint);
        float full = FullSize;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, full * Progression.Island.CameraMinZoom, full);
        Vector3 after = cam.ScreenToWorldPoint(screenPoint);
        var p = cam.transform.position + (before - after);
        p.z = cam.transform.position.z;
        cam.transform.position = p;
        ClampView();
    }

    /// <summary>Boyutu [en yakın, en uzak] aralığında tutar; görüş alanını harita (+pay) sınırlarına kenetler.</summary>
    public void ClampView()
    {
        float full = FullSize;
        if (full <= 0f) return;
        if (!Locked && !moving)
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, full * Progression.Island.CameraMinZoom, full);
        float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;
        var p = cam.transform.position;
        p.x = ClampAxis(p.x, halfWidth, -BoundsPad, IslandLayout.Width + BoundsPad, IslandLayout.Width * 0.5f);
        p.y = ClampAxis(p.y, halfHeight, -BoundsPad, IslandLayout.Height + BoundsPad, IslandLayout.Height * 0.5f);
        cam.transform.position = p;
    }

    private static float ClampAxis(float center, float half, float low, float high, float fallback)
    {
        float min = low + half, max = high - half;
        return min > max ? fallback : Mathf.Clamp(center, min, max);
    }

    private static bool IsOverUi(Vector2 screenPoint)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = screenPoint };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        foreach (var hit in results)
            if (hit.gameObject.name != "TouchSurface") return true;
        return false;
    }
}
