using UnityEngine;

/// <summary>
/// Savas sahnesindeki Ana Savasci kontrolu.
/// Hareket girdisi VirtualJoystick tarafindan SetMoveInput ile verilir.
/// </summary>
public class HeroController : MonoBehaviour
{
    [Header("Hareket")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Saldiri")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Can")]
    [SerializeField] private float maxHealth = 100f;

    private float currentHealth;
    private float lastAttackTime;
    private bool isDead;
    private Vector2 moveInput;

    private SpriteRenderer spriteRenderer;
    private HealthBar healthBar;

    public float HealthPercent => currentHealth / maxHealth;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (GameManager.Instance != null && GameManager.Instance.PlayerData != null)
        {
            HeroData hero = GameManager.Instance.PlayerData.hero;
            hero.ApplyLevelStats();

            moveSpeed = hero.moveSpeed;
            attackRange = hero.attackRange;
            attackDamage = hero.damage;
            attackCooldown = 1f / hero.attackSpeed;
            maxHealth = hero.maxHealth;
        }

        currentHealth = maxHealth;
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 0.8f, 0));
    }

    private void Update()
    {
        if (isDead)
            return;

        HandleMovement();
        HandleAutoAttack();
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    private void HandleMovement()
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
            return;

        Vector3 direction = new Vector3(moveInput.x, moveInput.y, 0f).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
            spriteRenderer.flipX = direction.x < 0;
    }

    private void HandleAutoAttack()
    {
        EnemyController target = FindNearestEnemyInRange();
        if (target == null)
            return;

        if (Time.time - lastAttackTime < attackCooldown)
            return;

        lastAttackTime = Time.time;
        target.TakeDamage(attackDamage);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnHit(target.transform.position, 0.55f);
        StartCoroutine(AttackPunch(target.transform.position));

        if (spriteRenderer != null)
        {
            float dirX = target.transform.position.x - transform.position.x;
            if (Mathf.Abs(dirX) > 0.01f)
                spriteRenderer.flipX = dirX < 0;
        }
    }

    private System.Collections.IEnumerator AttackPunch(Vector3 targetPos)
    {
        Vector3 originalPos = transform.position;
        Vector3 dir = (targetPos - originalPos).normalized;
        Vector3 punchPos = originalPos + dir * 0.15f;

        float t = 0f;
        while (t < 0.06f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(originalPos, punchPos, t / 0.06f);
            yield return null;
        }

        t = 0f;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(punchPos, originalPos, t / 0.08f);
            yield return null;
        }

        transform.position = originalPos;
    }

    private EnemyController FindNearestEnemyInRange()
    {
        EnemyController nearest = null;
        float nearestDist = attackRange;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead)
                continue;

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist <= nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        if (spriteRenderer != null)
            StartCoroutine(DamageFlash());

        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0f)
            Die();
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (spriteRenderer == null)
            yield break;

        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.08f);

        if (spriteRenderer != null && !isDead)
            spriteRenderer.color = originalColor;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        StartCoroutine(HeroDeathEffect());

        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
            battleManager.OnHeroDied();
    }

    private System.Collections.IEnumerator HeroDeathEffect()
    {
        if (spriteRenderer == null)
            yield break;

        spriteRenderer.color = new Color(1f, 0.3f, 0.2f);

        float t = 0f;
        Vector3 startScale = transform.localScale;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float progress = t / 0.4f;

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f - progress;
                spriteRenderer.color = c;
            }

            transform.localScale = startScale * (1f - progress * 0.5f);
            yield return null;
        }
    }
}
