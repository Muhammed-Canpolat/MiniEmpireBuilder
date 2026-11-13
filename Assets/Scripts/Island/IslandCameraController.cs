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

    /// <summary>Savaş dışında sabit kadraj (FeatureConfig.islandFixedCamera): kaydırma/zum yok.</summary>
    public static bool FixedView => FeatureConfig.Instance.islandFixedCamera;

    /// <summary>
    /// Sabit kadraj boyu: ada iki yanda deniz payıyla genişliğe sığar, yükseklik üst çubuk/alt düğme payları düşülmüş
    /// bandına sığar; ikisinin büyüğü. Merkez adanın ortası, dikey olarak bant ortasına oturtulur.
    /// </summary>
    /// <summary>Kara hücrelerinin dünya sınırı (adacıklar dahil; deniz hariç). Kadraj buna göre hesaplanır.</summary>
    public static Rect LandBounds()
    {
        if (landBounds.HasValue) return landBounds.Value;
        int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
        for (int r = 0; r < IslandLayout.Height; r++)
            for (int c = 0; c < IslandLayout.Width; c++)
                if (IslandLayout.IsLand(c, r)) { minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c); minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r); }
        if (minC > maxC) { landBounds = new Rect(0f, 0f, IslandLayout.Width, IslandLayout.Height); return landBounds.Value; }
        // World(col,row) = (col, Height-row): satır büyüdükçe y küçülür; hücre 1 u. Kıyı köpüğü için yarım hücre pay.
        float xMin = minC - 0.5f, xMax = maxC + 1.5f;
        float yMin = IslandLayout.Height - maxR - 1.5f, yMax = IslandLayout.Height - minR + 0.5f;
        landBounds = Rect.MinMaxRect(Mathf.Max(0f, xMin), Mathf.Max(0f, yMin), Mathf.Min(IslandLayout.Width, xMax), Mathf.Min(IslandLayout.Height, yMax));
        return landBounds.Value;
    }
    private static Rect? landBounds;

    public static void ComputeFixedView(float aspect, out Vector2 center, out float size)
    {
        var config = FeatureConfig.Instance;
        aspect = Mathf.Max(0.3f, aspect);
        var land = config.islandViewFitLand ? LandBounds() : new Rect(0f, 0f, IslandLayout.Width, IslandLayout.Height);
        float usable = Mathf.Clamp(1f - config.islandViewTopHudFraction - config.islandViewBottomHudFraction, 0.3f, 1f);
        float widthFit = land.width * 0.5f * (1f + 2f * config.islandViewSeaMargin) / aspect;
        float heightFit = land.height * 0.5f / usable;
        size = Mathf.Max(widthFit, heightFit);
        // Bandın ortası (alt pay + usable/2) viewport'ta 0.5 değilse dünya merkezini ona göre kaydır
        float bandCenter = config.islandViewBottomHudFraction + usable * 0.5f;
        center = new Vector2(land.center.x, land.center.y - (bandCenter - 0.5f) * 2f * size);
    }

    public void ApplyFixedView()
    {
        ComputeFixedView(cam.aspect, out var center, out float size);
        cam.orthographicSize = size;
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
    }

    private bool IsFixed => FixedView && !InBattle && !FullFrame;

    public void SetDefaultView()
    {
        if (FixedView) { ApplyFixedView(); return; }
        cam.orthographicSize = Mathf.Max(0.1f, FullSize * Progression.Island.CameraDefaultZoom);
        // Kale görünümde üstte: açılış kaleden (üst-orta) başlar.
        var center = IslandLayout.CastleCenter + new Vector2(0f, -5.5f);
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
        ClampView();
    }

    public void SetFullView()
    {
        if (FixedView) { ApplyFixedView(); return; }
        cam.orthographicSize = FullSize;
        cam.transform.position = new Vector3(IslandLayout.Width * 0.5f, IslandLayout.Height * 0.5f, cam.transform.position.z);
        ClampView();
    }

    /// <summary>Tam savaş çerçevesi (HARİTA düğmesi, bot, testler): takip kapanır, tüm harita görünür.</summary>
    public void SetBattleView()
    {
        FullFrame = true;
        cam.orthographicSize = BattleSize;
        cam.transform.position = new Vector3(BattleCenter.x, BattleCenter.y, cam.transform.position.z);
    }

    /// <summary>Savaş takibi: HARİTA düğmesi tam çerçeve ↔ takip arasında geçirir.</summary>
    public void ToggleFullFrame()
    {
        FullFrame = !FullFrame;
        if (!InBattle) return;
        StopAllCoroutines();
        if (FullFrame) StartCoroutine(Glide(new Vector3(BattleCenter.x, BattleCenter.y, cam.transform.position.z), BattleSize, 0.5f, false));
        else { manualUntil = 0f; moving = false; }
    }

    /// <summary>Takip çerçevesi: harita tam boyunun %60'ı; okçu + en yakın tehdit sığar (en çok %85).</summary>
    public const float FollowZoom = 0.6f, FollowMaxZoom = 0.85f;
    public bool InBattle { get; private set; }
    public bool FullFrame { get; private set; }
    private float manualUntil;
    private Vector3 velocity;
    private float sizeVelocity;

    /// <summary>Testler/ekran görüntüsü: takibi geçici durdurur / sürdürür.</summary>
    public void HoldFollow(float seconds) { manualUntil = Time.unscaledTime + seconds; }
    public void ResumeFollow() { FullFrame = false; manualUntil = 0f; }

    /// <summary>Takip hedefi: okçu ile en yakın etkin tehdidin (kıyıdaki düşman, yoksa tekne) orta noktası.</summary>
    public void ComputeFollowTarget(out Vector2 center, out float size)
    {
        Vector2 archer = IslandArcher.Instance != null ? IslandArcher.Instance.Position : IslandLayout.CastleCenter;
        Vector2 castle = IslandLayout.CastleCenter;
        Vector2? threat = null;
        float best = float.MaxValue;
        foreach (var target in IslandTarget.Collect())
        {
            Vector2 p = target.Position;
            // Karadaki düşman kaleye yakınlığıyla; tekne kumsala (inişe) yakınlığıyla önceliklenir
            float d = target.IsBoat ? Vector2.Distance(p, IslandLayout.Beaches[Mathf.Clamp(target.boat.BeachIndex, 0, 3)].landing) + 6f
                : Vector2.Distance(p, castle);
            if (d < best) { best = d; threat = p; }
        }
        float baseSize = Mathf.Max(0.1f, FullSize * FollowZoom);
        if (!threat.HasValue) { center = (archer + castle) * 0.5f; size = baseSize; return; }
        center = (archer + threat.Value) * 0.5f;
        float needH = Mathf.Abs(archer.y - threat.Value.y) * 0.5f + 3.5f;
        float needW = (Mathf.Abs(archer.x - threat.Value.x) * 0.5f + 3f) / Mathf.Max(0.3f, cam.aspect);
        size = Mathf.Clamp(Mathf.Max(baseSize, needH, needW), baseSize, FullSize * FollowMaxZoom);
    }

    private void UpdateBattleFollow()
    {
        if (FullFrame) return;
        // Oyuncu kaydırır/sıkıştırırsa 4 sn serbest bırakılır, sonra takip süzülerek döner
        Vector3 before = cam.transform.position; float beforeSize = cam.orthographicSize;
        HandleInput();
        if ((cam.transform.position - before).sqrMagnitude > 1e-6f || !Mathf.Approximately(beforeSize, cam.orthographicSize)) manualUntil = Time.unscaledTime + 4f;
        if (Time.unscaledTime < manualUntil) { ClampView(); return; }
        ComputeFollowTarget(out var center, out float size);
        float halfHeight = size, halfWidth = size * cam.aspect;
        center.x = ClampAxis(center.x, halfWidth, -BoundsPad, IslandLayout.Width + BoundsPad, IslandLayout.Width * 0.5f);
        center.y = ClampAxis(center.y - 0.8f, halfHeight, -BoundsPad, IslandLayout.Height + BoundsPad, IslandLayout.Height * 0.5f);
        var goal = new Vector3(center.x, center.y, cam.transform.position.z);
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (dt <= 0f) return;
        cam.transform.position = Vector3.SmoothDamp(cam.transform.position, goal, ref velocity, 0.6f, 30f, dt);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, size, ref sizeVelocity, 0.8f, 30f, dt);
    }

    /// <summary>Savaşa girerken: görünümü sakla ve takip çerçevesine kay (kilit yok: kaydırma/sıkıştırma çalışır).</summary>
    public void EnterBattle(float seconds)
    {
        savedPosition = cam.transform.position;
        savedSize = cam.orthographicSize;
        InBattle = true; FullFrame = false; Locked = false;
        manualUntil = 0f; velocity = Vector3.zero; sizeVelocity = 0f;
        StopAllCoroutines();
        ComputeFollowTarget(out var center, out float size);
        StartCoroutine(Glide(new Vector3(center.x, center.y - 0.8f, savedPosition.z), size, seconds, false));
    }

    /// <summary>Sonuçtan dönerken: önceki görünüme kay ve kilidi aç.</summary>
    public void ExitBattle(float seconds)
    {
        InBattle = false; FullFrame = false;
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
        if (cam == null || moving) return;
        if (InBattle) { UpdateBattleFollow(); return; }
        if (Locked) return;
        if (IsFixed) { ClampView(); return; }   // sabit kadraj: giriş okunmaz
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

        if (BaseCameraController.GhostDragActive) { panning = false; return; }   // yerleştirmede hayalet sürükleniyor
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

    /// <summary>Yerleştirmede hayalet ekran kenarına yaklaşınca kamerayı kaydırır (Ana Üs EdgeScroll ile aynı kural).</summary>
    public void EdgeScroll(Vector2 screenPoint)
    {
        if (cam == null || IsFixed) return;   // sabit kadrajda kenar kaydırması yok
        Rect view = cam.pixelRect;
        float marginX = view.width * BaseCameraController.EdgeFraction, marginY = view.height * BaseCameraController.EdgeFraction;
        float dx = 0f, dy = 0f;
        if (screenPoint.x < view.xMin + marginX) dx = -(1f - (screenPoint.x - view.xMin) / marginX);
        else if (screenPoint.x > view.xMax - marginX) dx = 1f - (view.xMax - screenPoint.x) / marginX;
        if (screenPoint.y < view.yMin + marginY) dy = -(1f - (screenPoint.y - view.yMin) / marginY);
        else if (screenPoint.y > view.yMax - marginY) dy = 1f - (view.yMax - screenPoint.y) / marginY;
        if (dx == 0f && dy == 0f) return;
        float speed = cam.orthographicSize * 1.1f * Time.unscaledDeltaTime;
        var p = cam.transform.position;
        p.x += Mathf.Clamp(dx, -1f, 1f) * speed;
        p.y += Mathf.Clamp(dy, -1f, 1f) * speed;
        cam.transform.position = p;
        ClampView();
    }

    /// <summary>Alt panel açılınca seçilen yapı panelin üstünde kalsın: dünya noktasını ekranın üst yarısına (viewportY) süzer.</summary>
    public void FocusUpper(Vector2 world, float viewportY = 0.74f, float seconds = 0.3f)
    {
        if (cam == null || InBattle || Locked || IsFixed) return;
        float size = cam.orthographicSize;
        float halfWidth = size * cam.aspect;
        float x = ClampAxis(world.x, halfWidth, -BoundsPad, IslandLayout.Width + BoundsPad, IslandLayout.Width * 0.5f);
        float y = ClampAxis(world.y - (viewportY - 0.5f) * 2f * size, size, -BoundsPad, IslandLayout.Height + BoundsPad, IslandLayout.Height * 0.5f);
        StopAllCoroutines(); moving = false;
        StartCoroutine(Glide(new Vector3(x, y, cam.transform.position.z), size, seconds, false));
    }

    /// <summary>Dünya noktası görüş alanının iç %80'inde değilse kamerayı ona ortalar.</summary>
    public void EnsureVisible(Vector2 world)
    {
        if (cam == null || IsFixed) return;
        Vector3 viewport = cam.WorldToViewportPoint(new Vector3(world.x, world.y, 0f));
        if (viewport.x > 0.1f && viewport.x < 0.9f && viewport.y > 0.1f && viewport.y < 0.9f) return;
        cam.transform.position = new Vector3(world.x, world.y, cam.transform.position.z);
        ClampView();
    }

    public void ZoomBy(float factor, Vector2 screenPoint)
    {
        if (IsFixed) return;
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
        if (IsFixed && !moving) { ApplyFixedView(); return; }
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

    public static bool IsOverUi(Vector2 screenPoint)
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
