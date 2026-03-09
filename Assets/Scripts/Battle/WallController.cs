using UnityEngine;

/// <summary>
/// Savaş sahnesindeki Duvar segmenti — düşmanlar önce bunu kırmaya çalışır
/// Duvar HP'si Duvarcı (WallBuilder) seviyesine göre belirlenir
/// </summary>
public class WallController : MonoBehaviour
{
    [Header("Statlar")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    private SpriteRenderer spriteRenderer;

    public float HealthPercent => currentHealth / maxHealth;
    public bool IsDestroyed => currentHealth <= 0;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Duvarcı (WallBuilder) seviyesinden HP al
        if (GameManager.Instance != null && GameManager.Instance.Buildings.ContainsKey(BuildingType.WallBuilder))
        {
            var wallBuilderData = GameManager.Instance.Buildings[BuildingType.WallBuilder];
            if (wallBuilderData.isUnlocked && wallBuilderData.level > 0)
            {
                maxHealth = BuildingData.GetWallHealthFromBuilder(wallBuilderData.level);
            }
        }

        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

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
            spriteRenderer.color = new Color(1f, 0.5f, 0f); // Turuncu
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = new Color(0.6f, 0.4f, 0.2f); // Kahverengi
        }
    }

    private void OnDestroyed()
    {
        Debug.Log("[Wall] Duvar yıkıldı!");
        Destroy(gameObject);
    }
}
