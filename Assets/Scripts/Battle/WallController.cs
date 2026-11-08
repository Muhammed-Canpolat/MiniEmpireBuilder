using UnityEngine;

/// <summary>
/// Legacy wall component retained for existing scene compatibility.
/// </summary>
public class WallController : MonoBehaviour
{
    [Header("Statlar")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private HealthBar healthBar;

    private SpriteRenderer spriteRenderer;

    public float HealthPercent => currentHealth / maxHealth;
    public bool IsDestroyed => currentHealth <= 0;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        currentHealth = maxHealth;
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 0.6f, 0));
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

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
