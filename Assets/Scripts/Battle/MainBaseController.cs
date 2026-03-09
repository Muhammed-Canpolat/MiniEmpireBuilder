using UnityEngine;

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

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // GameManager'dan stat al
        if (GameManager.Instance != null && GameManager.Instance.Buildings.ContainsKey(BuildingType.MainBase))
        {
            var baseData = GameManager.Instance.Buildings[BuildingType.MainBase];
            baseData.ApplyLevelStats();
            maxHealth = baseData.maxHealth;
        }

        currentHealth = maxHealth;

        // Can barı oluştur
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 1.2f, 0));
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        // Can barını güncelle
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (spriteRenderer != null)
        {
            StartCoroutine(DamageFlash());
        }

        if (currentHealth <= 0)
        {
            OnDestroyed();
        }
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

        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
        {
            battleManager.OnMainBaseDestroyed();
        }
    }
}
