// [HEALTH BAR] — Mini Empire Builder
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Bir objenin üstünde küçük can barı. 6. oturum (1.3): dolgu SOL kenara sabit (sol pivotlu sprite), sağdan sola azalır;
/// hasarda gecikmeli beyaz iz (UiHealthBar ile aynı kural ve süreler). Eskiden dolgu sprite'ı 2 birim genişken kaydırma yarım
/// hesaplanıyordu: dolgu ortaya doğru küçülüyor, solda boşluk kalıyordu.
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
    private SpriteRenderer trailRenderer;
    private float shownRatio = 1f, trailRatio = 1f, trailHoldUntil;
    public float ShownRatio => shownRatio;
    public float TrailRatio => trailRatio;
    /// <summary>Dolgunun sol kenarı (yerel x): oran ne olursa olsun barın sol kenarıyla aynı.</summary>
    public float FillLeftEdge => fillRenderer != null ? fillRenderer.transform.localPosition.x : 0f;
    public float BarLeftEdge => -barSize.x * SpriteUnitWidth * 0.5f;
    private const float SpriteUnitWidth = 2f;   // 32 px / 16 ppu
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

    /// <summary>Can tavanını değiştirir ve çubuğu doldurur.</summary>
    public void SetMax(float maxHp)
    {
        maxHealth = Mathf.Max(1f, maxHp);
        currentHealth = maxHealth;
        ApplyFill(1f);
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

        var leftSprite = CreateRoundedBarSprite(new Vector2(0f, 0.5f));
        GameObject trailObj = new GameObject("BarTrail");
        trailObj.transform.SetParent(transform);
        trailRenderer = trailObj.AddComponent<SpriteRenderer>();
        trailRenderer.sprite = leftSprite;
        trailRenderer.color = new Color(1f, 0.97f, 0.9f, 0.9f);
        trailRenderer.sortingOrder = 11;

        GameObject fillObj = new GameObject("BarFill");
        fillObj.transform.SetParent(transform);
        fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = leftSprite;
        fillRenderer.color = baseColor;
        fillRenderer.sortingOrder = 12;
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

    /// <summary>Hasar: dolgu hemen iner, beyaz iz kısa bekleyip erir. İyileşme: dolgu yumuşakça artar.</summary>
    private void ApplyFillTween(float ratio)
    {
        if (fillRenderer == null)
            return;
        if (ratio < shownRatio - 1e-4f)
        {
            if (trailRatio < shownRatio) trailRatio = shownRatio;
            trailHoldUntil = Time.unscaledTime + UiHealthBar.TrailHold;
            shownRatio = ratio;
        }
        targetRatio = ratio;
        Layout();
    }

    private float targetRatio = 1f;

    private void ApplyFill(float ratio)
    {
        shownRatio = trailRatio = targetRatio = Mathf.Clamp01(ratio);
        Layout();
    }

    /// <summary>Sol pivotlu sprite'lar barın sol kenarına oturur; genişlik = oran × bar.</summary>
    private void Layout()
    {
        float left = BarLeftEdge;
        if (fillRenderer != null)
        {
            fillRenderer.transform.localPosition = new Vector3(left, 0f, 0f);
            fillRenderer.transform.localScale = new Vector3(barSize.x * shownRatio, barSize.y, 1f);
            fillRenderer.enabled = shownRatio > 0.001f;
        }
        if (trailRenderer != null)
        {
            trailRenderer.transform.localPosition = new Vector3(left, 0f, 0f);
            trailRenderer.transform.localScale = new Vector3(barSize.x * trailRatio, barSize.y, 1f);
            trailRenderer.enabled = trailRatio > shownRatio + 0.001f;
        }
    }

    private void StepTrail()
    {
        float dt = Time.unscaledDeltaTime;
        bool changed = false;
        if (targetRatio > shownRatio) { shownRatio = Mathf.MoveTowards(shownRatio, targetRatio, UiHealthBar.FillSpeed * dt); changed = true; }
        if (trailRatio < shownRatio) { trailRatio = shownRatio; changed = true; }
        else if (trailRatio > shownRatio && Time.timeScale > 0f && Time.unscaledTime >= trailHoldUntil)
        {
            trailRatio = Mathf.MoveTowards(trailRatio, shownRatio, UiHealthBar.TrailSpeed * dt);
            changed = true;
        }
        if (changed) Layout();
    }

    private Sprite CreateRoundedBarSprite() => CreateRoundedBarSprite(new Vector2(0.5f, 0.5f));

    private Sprite CreateRoundedBarSprite(Vector2 pivot)
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
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), pivot, 16f);
    }

    private void LateUpdate()
    {
        StepTrail();
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
