using UnityEngine;
using DG.Tweening;

/// <summary>
/// Savaş sahnesindeki Ana Üs
/// Düşmanlar bunu yok etmeye çalışır
/// </summary>
public class MainBaseController : MonoBehaviour
{
    [Header("Statlar")]
    [SerializeField] private float maxHealth = 200f;
    private float currentHealth;

    private SpriteRenderer spriteRenderer;
    private HealthBar healthBar;

    public float HealthPercent => currentHealth / maxHealth;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed => currentHealth <= 0;

    public void Heal(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (healthBar != null) healthBar.UpdateHealth(currentHealth);
    }

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // GameManager'dan stat al
        if (GameManager.Instance != null && GameManager.Instance.Buildings.ContainsKey(BuildingType.MainBase))
        {
            var baseData = GameManager.Instance.Buildings[BuildingType.MainBase];
            baseData.ApplyLevelStats();
            maxHealth = baseData.maxHealth * BattleCards.Multiplier(BattleCard.Fortify);
        }

        currentHealth = maxHealth;

        // Can barı oluştur (üssün görünen tepesinin üstünde)
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 1.95f, 0), ReferenceUiVisuals.Gold);
        // Kahraman ve Çırak üssün içinden geçemez.
        BattleObstacles.Register(this, BattleObstacles.Footprint(transform.position, 3.4f, 1.05f, 1.6f));
    }

    private void OnDestroy() => BattleObstacles.Unregister(this);

    private float invulnerableUntil;

    /// <summary>"Savaşta devam et": yıkılan üs canın bir kısmıyla kalkar, kısa süre hasar almaz.</summary>
    public void Revive(float healthFraction, float graceSeconds)
    {
        currentHealth = Mathf.Max(1f, maxHealth * Mathf.Clamp01(healthFraction));
        invulnerableUntil = Time.time + graceSeconds;
        if (healthBar != null) healthBar.UpdateHealth(currentHealth);
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    public void TakeDamage(float damage)
    {
        if (Time.time < invulnerableUntil) return;
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        // Can barını güncelle
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (spriteRenderer != null)
        {
            StartCoroutine(DamageFlash());
        }
        PlayHitFeedback();

        if (currentHealth <= 0)
        {
            OnDestroyed();
        }
    }

    private float nextHitFeedback, nextSmoke;

    /// <summary>Darbe hissi: küçük sarsıntı + toz (en fazla 0,25 sn'de bir); canı %30 altındaysa duman.</summary>
    private void PlayHitFeedback()
    {
        var vfx = BattleVfxManager.Instance;
        if (Time.time >= nextHitFeedback)
        {
            nextHitFeedback = Time.time + 0.25f;
            transform.DOComplete();
            transform.DOShakePosition(0.15f, new Vector3(0.07f, 0.04f, 0f), 18, 90f);
            if (vfx != null) vfx.SpawnSmallDust(transform.position + new Vector3(Random.Range(-1.2f, 1.2f), -0.6f, 0f));
        }
    }

    private void Update()
    {
        // Ağır hasarlı üsten düzenli duman yükselir: tehlike uzaktan okunur.
        if (IsDestroyed || maxHealth <= 0f || currentHealth / maxHealth > 0.3f || Time.time < nextSmoke) return;
        nextSmoke = Time.time + 0.45f;
        var vfx = BattleVfxManager.Instance;
        if (vfx != null) vfx.SpawnSmallDust(transform.position + new Vector3(Random.Range(-1f, 1f), Random.Range(0.4f, 1.4f), 0f));
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = Color.white;
        }
    }

    private void OnDestroyed()
    {
        Debug.Log("[MainBase] Ana Üs yıkıldı!");
        // Yıkılış: büyük toz bulutu ve güçlü sarsıntı (yenilgi ani değil, okunur olsun).
        var vfx = BattleVfxManager.Instance;
        if (vfx != null)
            for (int i = 0; i < 6; i++)
                vfx.SpawnSmallDust(transform.position + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.6f, 1.2f), 0f));
        if (Camera.main != null) Camera.main.transform.DOShakePosition(0.45f, 0.35f, 14, 90f);

        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
        {
            battleManager.OnMainBaseDestroyed();
        }
    }
}
