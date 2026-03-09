using UnityEngine;

/// <summary>
/// Bir objenin üstünde küçük can barı gösterir
/// Düşmanlar, kuleler, çit ve Ana Üs için kullanılır
/// </summary>
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0, 0.7f, 0);
    [SerializeField] private Vector2 barSize = new Vector2(0.8f, 0.1f);

    private Transform targetTransform;
    private SpriteRenderer bgRenderer;
    private SpriteRenderer fillRenderer;
    private float maxHealth = 1f;
    private float currentHealth = 1f;

    /// <summary>
    /// Can barını oluştur ve hedefe bağla
    /// </summary>
    public static HealthBar Create(Transform target, float maxHp, Vector3? customOffset = null)
    {
        GameObject barObj = new GameObject("HealthBar");
        HealthBar hb = barObj.AddComponent<HealthBar>();
        hb.targetTransform = target;
        hb.maxHealth = maxHp;
        hb.currentHealth = maxHp;

        if (customOffset.HasValue)
            hb.offset = customOffset.Value;

        hb.BuildBar();
        return hb;
    }

    private void BuildBar()
    {
        // Arka plan (koyu)
        GameObject bgObj = new GameObject("BarBg");
        bgObj.transform.SetParent(transform);
        bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = CreateBarSprite();
        bgRenderer.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        bgRenderer.sortingOrder = 10;
        bgObj.transform.localScale = new Vector3(barSize.x, barSize.y, 1);

        // Dolgu (yeşil)
        GameObject fillObj = new GameObject("BarFill");
        fillObj.transform.SetParent(transform);
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = CreateBarSprite();
        fillRenderer.color = Color.green;
        fillRenderer.sortingOrder = 11;
        fillObj.transform.localScale = new Vector3(barSize.x, barSize.y, 1);
    }

    private Sprite CreateBarSprite()
    {
        Texture2D tex = new Texture2D(16, 4);
        Color[] colors = new Color[16 * 4];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 16, 4), new Vector2(0.5f, 0.5f), 16f);
    }

    /// <summary>
    /// Canı güncelle
    /// </summary>
    public void UpdateHealth(float hp)
    {
        currentHealth = Mathf.Max(0, hp);
        float ratio = currentHealth / maxHealth;

        if (fillRenderer != null)
        {
            // Dolgu genişliğini ayarla
            fillRenderer.transform.localScale = new Vector3(barSize.x * ratio, barSize.y, 1);

            // Dolgu pozisyonunu ayarla (soldan dolsun)
            float xOffset = -(barSize.x * (1f - ratio)) / 2f;
            fillRenderer.transform.localPosition = new Vector3(xOffset, 0, 0);

            // Renge göre değiştir
            if (ratio > 0.6f)
                fillRenderer.color = Color.green;
            else if (ratio > 0.3f)
                fillRenderer.color = Color.yellow;
            else
                fillRenderer.color = Color.red;
        }
    }

    private void LateUpdate()
    {
        if (targetTransform != null)
        {
            transform.position = targetTransform.position + offset;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Temizlik
    }
}
