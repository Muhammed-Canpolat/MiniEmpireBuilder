// [HEALTH BAR] — Mini Empire Builder
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Bir objenin ustunde kucuk can bari gosterir.
/// </summary>
public class HealthBar : MonoBehaviour
{
    private const float LowHealthThreshold = 0.3f;
    private const float SmoothDuration = 0.3f;

    [SerializeField] private Vector3 offset = new Vector3(0f, 0.7f, 0f);
    // Sprite 2 x 0.5 birim: varsayılan çubuk 1.1 x 0.1 dünya birimi (eskiden 0.05 kalınlık, çizgi gibiydi).
    [SerializeField] private Vector2 barSize = new Vector2(0.55f, 0.2f);
    private Transform frameTransform;
    private Transform backgroundTransform;

    private Transform targetTransform;
    public Transform Target => targetTransform;
    public Vector3 Offset { get => offset; set => offset = value; }
    private SpriteRenderer fillRenderer;
    private float maxHealth = 1f;
    private float currentHealth = -1f;
    private Color baseColor = Color.green;
    private bool hasCustomColor = false;

    /// <summary>
    /// Creates a new health bar and binds it to target transform.
    /// </summary>
    public static HealthBar Create(Transform target, float maxHp, Vector3? customOffset = null, Color? customColor = null)
    {
        GameObject barObj = new GameObject("HealthBar");
        HealthBar hb = barObj.AddComponent<HealthBar>();
        hb.targetTransform = target;
        hb.maxHealth = maxHp;
        hb.currentHealth = maxHp;
        
        if (customColor.HasValue)
        {
            hb.baseColor = customColor.Value;
            hb.hasCustomColor = true;
        }

        if (customOffset.HasValue)
            hb.offset = customOffset.Value;

        hb.BuildBar();
        hb.ApplyFill(1f);
        return hb;
    }

    /// <summary>
    /// Updates health value with smooth fill and low-health pulse.
    /// </summary>
    public void UpdateHealth(float hp)
    {
        float ratio = Mathf.Clamp01(Mathf.Max(0f, hp) / maxHealth);
        ApplyFillTween(ratio);

        if (fillRenderer == null)
            return;
            
        // Flash effect if damaged
        if (currentHealth > 0 && hp < currentHealth)
        {
            fillRenderer.DOKill(false);
            fillRenderer.DOColor(Color.white, 0.05f).OnComplete(() => 
            {
                if (fillRenderer != null)
                    ApplyBaseColor(ratio);
            }).SetLink(gameObject);
        }
        else
        {
            ApplyBaseColor(ratio);
        }
        
        currentHealth = hp;
    }
    
    private void ApplyBaseColor(float ratio)
    {
        fillRenderer.DOKill(false);
        if (hasCustomColor)
        {
            fillRenderer.color = baseColor;
        }
        else
        {
            if (ratio <= LowHealthThreshold)
                fillRenderer.DOColor(new Color(0.75f, 0f, 0f), 0.2f).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
            else
                fillRenderer.color = ratio > 0.6f ? Color.green : Color.yellow;
        }
    }

    private void BuildBar()
    {
        // Koyu çerçeve + boş yuva: her zeminde okunur.
        GameObject frameObj = new GameObject("BarFrame");
        frameObj.transform.SetParent(transform);
        var frameRenderer = frameObj.AddComponent<SpriteRenderer>();
        frameRenderer.sprite = CreateRoundedBarSprite();
        frameRenderer.color = new Color(0.04f, 0.03f, 0.02f, 0.92f);
        frameRenderer.sortingOrder = 9;
        frameTransform = frameObj.transform;

        GameObject bgObj = new GameObject("BarBg");
        bgObj.transform.SetParent(transform);
        SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = CreateRoundedBarSprite();
        bgRenderer.color = new Color(0.24f, 0.21f, 0.18f, 0.95f);
        bgRenderer.sortingOrder = 10;
        backgroundTransform = bgObj.transform;
        ApplyFrameScale();

        GameObject fillObj = new GameObject("BarFill");
        fillObj.transform.SetParent(transform);
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = CreateRoundedBarSprite();
        fillRenderer.color = baseColor;
        fillRenderer.sortingOrder = 11;
        fillObj.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
    }

    private void ApplyFrameScale()
    {
        if (frameTransform != null)
            frameTransform.localScale = new Vector3(barSize.x + 0.04f, barSize.y + 0.16f, 1f);
        if (backgroundTransform != null)
            backgroundTransform.localScale = new Vector3(barSize.x, barSize.y, 1f);
    }

    /// <summary>Çubuk boyutunu değiştirir (sprite birimi: x 2, y 0.5 dünya birimi çarpanı).</summary>
    public void Resize(Vector2 size)
    {
        barSize = size;
        ApplyFrameScale();
        ApplyFill(maxHealth > 0f ? Mathf.Clamp01(Mathf.Max(0f, currentHealth) / maxHealth) : 1f);
    }

    private void ApplyFillTween(float ratio)
    {
        if (fillRenderer == null)
            return;

        float targetX = barSize.x * ratio;
        float xOffset = -(barSize.x * (1f - ratio)) / 2f;

        fillRenderer.transform.DOKill(false);
        fillRenderer.transform.DOScaleX(targetX, SmoothDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        fillRenderer.transform.DOLocalMoveX(xOffset, SmoothDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    private void ApplyFill(float ratio)
    {
        if (fillRenderer == null)
            return;

        fillRenderer.transform.localScale = new Vector3(barSize.x * ratio, barSize.y, 1f);
        float xOffset = -(barSize.x * (1f - ratio)) / 2f;
        fillRenderer.transform.localPosition = new Vector3(xOffset, 0f, 0f);
    }

    private Sprite CreateRoundedBarSprite()
    {
        int width = 32;
        int height = 8;
        Texture2D tex = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Simple rounded corners
                bool isCorner = (x == 0 && y == 0) || (x == width - 1 && y == 0) || 
                                (x == 0 && y == height - 1) || (x == width - 1 && y == height - 1);
                colors[y * width + x] = isCorner ? Color.clear : Color.white;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 16f);
    }

    private void LateUpdate()
    {
        if (targetTransform != null)
            transform.position = targetTransform.position + offset;
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (fillRenderer != null)
        {
            fillRenderer.DOKill(false);
            fillRenderer.transform.DOKill(false);
        }
    }
}
