using UnityEngine;

/// <summary>
/// Kamera pinch-to-zoom ve tek parmak pan sistemi.
/// BattleSceneSetup ve BaseSceneSetup tarafından AddComponent ile eklenir.
/// Sahne kapanınca zoom seviyesi PlayerPrefs'e kaydedilir, açılınca geri yüklenir.
/// </summary>
public class CameraController : MonoBehaviour
{
    // ==================== ZOOM AYARLARI ====================
    private const float ZoomMin        = 4f;
    private const float ZoomMax        = 8f;
    private const float ZoomSpeed      = 0.01f;
    private const float ZoomSmoothing  = 8f;   // Lerp hızı (görsel yumuşatma)
    private const string ZoomPrefKey   = "CameraZoom";

    // ==================== PAN SINIRLARI ====================
    // Setup scriptlerinden Configure() ile ayarlanır
    private float panMinX = -10f;
    private float panMaxX =  10f;
    private float panMinY =  -8f;
    private float panMaxY =   8f;

    // ==================== RUNTIME STATE ====================
    private Camera cam;
    private float  targetZoom;

    // Pan takibi
    private bool    isPanning;
    private Vector3 panStartWorldPos;   // Parmağın dünya konumu (pan başlangıcı)

    // Pinch takibi
    private float prevPinchDist;

    // ==================== INIT ====================

    private void Awake()
    {
        cam = Camera.main;

        // Kayıtlı zoom varsa geri yükle, yoksa mevcut kamera değerini kullan
        if (PlayerPrefs.HasKey(ZoomPrefKey))
            targetZoom = PlayerPrefs.GetFloat(ZoomPrefKey);
        else
            targetZoom = cam != null ? cam.orthographicSize : 6f;

        targetZoom = Mathf.Clamp(targetZoom, ZoomMin, ZoomMax);
    }

    /// <summary>
    /// Setup scriptlerinden çağrılır — pan sınırlarını sahneye göre ayarlar.
    /// </summary>
    public void Configure(float minX, float maxX, float minY, float maxY)
    {
        panMinX = minX;
        panMaxX = maxX;
        panMinY = minY;
        panMaxY = maxY;
    }

    // ==================== UPDATE ====================

    private void Update()
    {
        if (cam == null) return;

        int touchCount = Input.touchCount;

        if (touchCount == 2)
        {
            // ----- 2 PARMAK: PİNCH-TO-ZOOM -----
            isPanning = false;   // Pan'ı kes

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float currentDist = Vector2.Distance(t0.position, t1.position);

            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                // İlk kare — sadece mesafeyi kaydet
                prevPinchDist = currentDist;
            }
            else
            {
                float delta = prevPinchDist - currentDist;   // pozitif → zoom in, negatif → zoom out
                targetZoom += delta * ZoomSpeed;
                targetZoom  = Mathf.Clamp(targetZoom, ZoomMin, ZoomMax);
                prevPinchDist = currentDist;
            }
        }
        else if (touchCount == 1)
        {
            // ----- 1 PARMAK: PAN -----
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // Pan başlangıcını kaydet
                isPanning     = true;
                panStartWorldPos = ScreenToWorld(touch.position);
            }
            else if (touch.phase == TouchPhase.Moved && isPanning)
            {
                // Parmak ne kadar hareket etti → kamerayı ters yönde kaydır
                Vector3 currentWorldPos = ScreenToWorld(touch.position);
                Vector3 delta           = panStartWorldPos - currentWorldPos;

                Vector3 newPos = cam.transform.position + delta;
                newPos = ClampCameraPosition(newPos);
                cam.transform.position = newPos;

                // Başlangıç noktasını güncelle (kümülatif sürükleme için)
                panStartWorldPos = ScreenToWorld(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isPanning = false;
            }
        }
        else
        {
            isPanning = false;
        }

        // ----- ZOOM YUMUŞATMA -----
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            Time.deltaTime * ZoomSmoothing);
    }

    // ==================== YARDIMCI METODLAR ====================

    /// <summary>
    /// Ekran koordinatını dünya koordinatına çevirir (z=0 düzlemi).
    /// </summary>
    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        Vector3 pos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane));
        pos.z = 0f;
        return pos;
    }

    /// <summary>
    /// Kamera pozisyonunu belirlenen sınırlar içinde tutar.
    /// </summary>
    private Vector3 ClampCameraPosition(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, panMinX, panMaxX);
        pos.y = Mathf.Clamp(pos.y, panMinY, panMaxY);
        return pos;
    }

    // ==================== KAYDETME ====================

    private void OnDestroy()
    {
        SaveZoom();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveZoom();
    }

    private void OnApplicationQuit()
    {
        SaveZoom();
    }

    private void SaveZoom()
    {
        if (cam != null)
            PlayerPrefs.SetFloat(ZoomPrefKey, cam.orthographicSize);
        PlayerPrefs.Save();
    }
}
