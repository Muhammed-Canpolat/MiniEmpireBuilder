using UnityEngine;

/// <summary>
/// Savaş sahnesinde kameranın kahramanı takip etmesini sağlar.
/// Dünya sınırları içerisinde kalacak şekilde clamp işlemi uygular.
/// </summary>
public class BattleCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f);

    private float minX, maxX, minY, maxY;
    private bool hasBounds = false;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    /// <summary>
    /// Takip edilecek hedefi ve dünya sınırlarını ayarlar.
    /// Dünya sınırları, kameranın merkezinin gidebileceği alan olmalıdır.
    /// </summary>
    public void Setup(Transform newTarget, float worldWidth, float worldHeight)
    {
        target = newTarget;

        float camHeight = cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        // Dünya sınırlarının yarısı kadar alana gidebiliriz, ancak kameranın boyutu kadar içeri çekmeliyiz.
        float halfWorldW = worldWidth / 2f;
        float halfWorldH = worldHeight / 2f;

        // Clamp için limitler
        minX = -halfWorldW + camWidth;
        maxX = halfWorldW - camWidth;
        minY = -halfWorldH + camHeight;
        maxY = halfWorldH - camHeight;

        // Eğer dünya boyutu ekrandan küçükse clamp yapamayız, ortalayalım
        if (minX > maxX) minX = maxX = 0;
        if (minY > maxY) minY = maxY = 0;

        hasBounds = true;

        // Başlangıçta direkt hedefe ışınlan
        if (target != null)
        {
            Vector3 startPos = target.position + offset;
            startPos.x = Mathf.Clamp(startPos.x, minX, maxX);
            startPos.y = Mathf.Clamp(startPos.y, minY, maxY);
            transform.position = startPos;
        }
    }

    private void LateUpdate()
    {
        if (target == null || !hasBounds) return;

        Vector3 desiredPosition = target.position + offset;

        // Sınırlandırma
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);

        // Pürüzsüz takip
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}
