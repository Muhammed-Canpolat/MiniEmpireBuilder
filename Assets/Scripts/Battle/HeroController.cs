using UnityEngine;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Savas sahnesindeki Ana Savasci kontrolu.
/// Hareket girdisi VirtualJoystick tarafindan SetMoveInput ile verilir.
/// Kahraman tipine gore farkli saldiri animasyonu ve efekti oynatilir.
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
    private Animator heroAnimator;
    private HealthBar healthBar;
    private WeaponType weaponType = WeaponType.Axe;
    private Sprite idleSprite;
    private Sprite runSprite;
    private Sprite attackSpriteA;
    private Sprite attackSpriteB;
    private float attackSpriteTimer;
    private bool attackToggle;
    private bool useAnimator = false;

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
            weaponType = hero.weaponType;
        }

        currentHealth = maxHealth;
        healthBar = HealthBar.Create(transform, maxHealth, new Vector3(0, 0.8f, 0));

        // Unity Animator entegrasyonu
        SetupAnimator();

        // Animator yoksa sprite-swap ile devam et
        if (!useAnimator)
            LoadCombatSprites();
    }

    /// <summary>
    /// Silah tipine göre AnimatorController yükle ve Animator bileşenini bağla.
    /// Controller Resources/Animators/ altında bulunmalıdır.
    /// </summary>
    private void SetupAnimator()
    {
        // AnimatorController bul
        RuntimeAnimatorController ctrl = null;
        if (SpriteManager.Instance != null)
            ctrl = SpriteManager.Instance.GetHeroAnimatorController(weaponType);

        if (ctrl == null)
        {
            Debug.LogWarning("[HeroController] AnimatorController bulunamadı — sprite-swap modunda devam.");
            useAnimator = false;
            return;
        }

        // Animator bileşenini ekle
        heroAnimator = gameObject.AddComponent<Animator>();
        heroAnimator.runtimeAnimatorController = ctrl;
        heroAnimator.updateMode = AnimatorUpdateMode.Normal;
        useAnimator = true;
        Debug.Log($"[HeroController] Animator kuruldu: {ctrl.name}");
    }

    private void Update()
    {
        if (isDead) return;
        HandleMovement();
        HandleAutoAttack();
        UpdateCombatSprite();
    }

    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    // ══════════════════════════════════════════════════════════════
    // HAREKET
    // ══════════════════════════════════════════════════════════════

    private void HandleMovement()
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
        {
            if (useAnimator && heroAnimator != null)
                heroAnimator.SetBool("IsMoving", false);
            return;
        }

        Vector3 direction = new Vector3(moveInput.x, moveInput.y, 0f).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
            spriteRenderer.flipX = direction.x < 0;

        if (useAnimator && heroAnimator != null)
            heroAnimator.SetBool("IsMoving", true);
    }

    private void UpdateCombatSprite()
    {
        // Animator kullanılıyorsa sprite-swap'a gerek yok
        if (useAnimator) return;

        if (spriteRenderer == null)
            return;

        if (attackSpriteTimer > 0f)
        {
            attackSpriteTimer -= Time.deltaTime;
            return;
        }

        if (moveInput.sqrMagnitude > 0.01f && runSprite != null)
        {
            if (spriteRenderer.sprite != runSprite)
                spriteRenderer.sprite = runSprite;
        }
        else if (idleSprite != null && spriteRenderer.sprite != idleSprite)
        {
            spriteRenderer.sprite = idleSprite;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // SALDIRI
    // ══════════════════════════════════════════════════════════════

    private void HandleAutoAttack()
    {
        EnemyController target = FindNearestEnemyInRange();
        if (target == null) return;
        if (Time.time - lastAttackTime < attackCooldown) return;

        lastAttackTime = Time.time;

        // Yüz hedefe
        if (spriteRenderer != null)
        {
            float dirX = target.transform.position.x - transform.position.x;
            if (Mathf.Abs(dirX) > 0.01f)
                spriteRenderer.flipX = dirX < 0;
        }

        // Hasar ver
        target.TakeDamage(attackDamage);

        // Screen shake (güçlü hasarda)
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.TryScreenShake(attackDamage);

        // Kahraman tipine göre animasyon + efekt
        if (useAnimator && heroAnimator != null)
        {
            // Animator üzerinden attack trigger
            if (heroAnimator.parameters.Length > 0)
            {
                // "Attack" trigger veya "IsAttacking" bool varsa tetikle
                foreach (var param in heroAnimator.parameters)
                {
                    if (param.name == "Attack" && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        heroAnimator.SetTrigger("Attack");
                        break;
                    }
                    if (param.name == "IsAttacking" && param.type == AnimatorControllerParameterType.Bool)
                    {
                        heroAnimator.SetBool("IsAttacking", true);
                        StartCoroutine(ResetAttackingBool(attackCooldown * 0.6f));
                        break;
                    }
                }
            }
        }

        switch (weaponType)
        {
            case WeaponType.Axe:
                PlayAttackSprite(attackSpriteA, attackSpriteB);
                StartCoroutine(AxeAttack(target.transform.position));
                break;
            case WeaponType.Bow:
                PlayAttackSprite(attackSpriteA, null);
                StartCoroutine(BowAttack(target));
                break;
            case WeaponType.Spear:
                PlayAttackSprite(attackSpriteA, null);
                StartCoroutine(SpearAttack(target));
                break;
            default:
                PlayAttackSprite(attackSpriteA, null);
                StartCoroutine(BasicPunch(target.transform.position));
                break;
        }
    }

    private void PlayAttackSprite(Sprite primary, Sprite secondary)
    {
        // Animator kullanıyorsa sprite-swap'a gerek yok
        if (useAnimator) return;
        if (spriteRenderer == null) return;

        Sprite chosen = primary;
        if (secondary != null)
        {
            attackToggle = !attackToggle;
            chosen = attackToggle ? primary : secondary;
        }

        if (chosen != null)
            spriteRenderer.sprite = chosen;

        attackSpriteTimer = 0.18f;
    }

    private IEnumerator ResetAttackingBool(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (heroAnimator != null)
            heroAnimator.SetBool("IsAttacking", false);
    }

    private void LoadCombatSprites()
    {
        switch (weaponType)
        {
            case WeaponType.Axe:
                idleSprite = Resources.Load<Sprite>("Sprites/hero_axe_idle");
                runSprite = Resources.Load<Sprite>("Sprites/hero_axe_run");
                attackSpriteA = Resources.Load<Sprite>("Sprites/hero_axe_attack1");
                attackSpriteB = Resources.Load<Sprite>("Sprites/hero_axe_attack2");
                break;
            case WeaponType.Spear:
                idleSprite = Resources.Load<Sprite>("Sprites/hero_spear_idle");
                runSprite = Resources.Load<Sprite>("Sprites/hero_spear_run");
                attackSpriteA = Resources.Load<Sprite>("Sprites/hero_spear_attack");
                break;
            case WeaponType.Bow:
                idleSprite = Resources.Load<Sprite>("Sprites/hero_bow_idle");
                runSprite = Resources.Load<Sprite>("Sprites/hero_bow_run");
                attackSpriteA = Resources.Load<Sprite>("Sprites/hero_bow_shoot");
                break;
        }

        if (spriteRenderer != null && idleSprite != null)
            spriteRenderer.sprite = idleSprite;
    }

    // ── BALTACI (YAKIN MESAFE) ─────────────────────────────────────

    private IEnumerator AxeAttack(Vector3 targetPos)
    {
        Vector3 origScale = transform.localScale;

        // Kahramanın ufak zıplama/vurma animasyonu
        transform.DOScaleX(origScale.x * -1f, 0.08f).SetEase(Ease.OutQuad);
        transform.DORotate(new Vector3(0, 0, -15f), 0.08f);

        yield return new WaitForSeconds(0.08f);

        transform.DOScaleX(origScale.x, 0.12f).SetEase(Ease.OutBack);
        transform.DORotate(new Vector3(0, 0, 15f), 0.06f);

        // Beyaz arc çizgisi (swing efekti)
        if (BattleVfxManager.Instance != null)
        {
            GameObject arc = new GameObject("AxeSwingArc");
            arc.transform.position = transform.position + (targetPos - transform.position).normalized * 0.8f;

            // Yöne göre rotasyon
            Vector2 dir = (targetPos - transform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arc.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            SpriteRenderer arcSr = arc.AddComponent<SpriteRenderer>();

            // İnce kavisli bir çizgi oluşturamayacağımızdan, ince uzun bir dikdörtgeni hızla döndürelim
            Texture2D tex = new Texture2D(8, 32);
            Color[] px = new Color[8 * 32];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            arcSr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 32), new Vector2(0.5f, 0.5f), 32f);

            arcSr.color = new Color(1f, 1f, 1f, 0.8f);
            arc.transform.localScale = new Vector3(0.1f, 0.6f, 1f);

            arc.transform.DORotate(new Vector3(0, 0, angle - 180f), 0.15f, RotateMode.FastBeyond360).SetEase(Ease.OutQuad);
            arcSr.DOFade(0f, 0.15f);
            Destroy(arc, 0.2f);

            BattleVfxManager.Instance.SpawnSpark(targetPos);
        }

        yield return new WaitForSeconds(0.06f);
        transform.DORotate(Vector3.zero, 0.06f);
        yield return new WaitForSeconds(0.06f);
        transform.localScale = origScale;
    }

    // ── OKÇU (MENZİLLİ) ────────────────────────────────────────────

    private IEnumerator BowAttack(EnemyController target)
    {
        if (target == null || target.IsDead) yield break;

        Vector3 targetPos = target.transform.position;

        // Hafif geri çekilme
        transform.DOScaleY(transform.localScale.y * 0.92f, 0.1f).SetEase(Ease.OutQuad);
        yield return new WaitForSeconds(0.1f);
        transform.DOScaleY(transform.localScale.y, 0.1f);

        // Ok fırlat
        GameObject projObj = new GameObject("Projectile");
        ProjectileController proj = projObj.AddComponent<ProjectileController>();
        proj.Launch(transform.position, targetPos, ProjectileType.Arrow, () =>
        {
            if (target != null && !target.IsDead)
            {
                // Hasar zaten ana metodda verildi ama efekti burada yapıyoruz
                // Not: Mantıksal olarak hasarı hedefe ulaşınca vermek daha doğrudur.
                // Ancak mevcut yapıyı (önceden hasar verme) bozmamak için sadece efekti buraya bırakıyoruz.
                // Eğer hasar onHit'te verilecekse, HandleAutoAttack'taki target.TakeDamage'ı buraya taşıyabiliriz.
            }
        });
    }

    // ── MIZRAKÇI (HİBRİT) ──────────────────────────────────────────

    private IEnumerator SpearAttack(EnemyController target)
    {
        if (target == null || target.IsDead) yield break;

        float dist = Vector2.Distance(transform.position, target.transform.position);
        Vector3 targetPos = target.transform.position;
        bool isClose = dist <= attackRange * 0.7f;

        if (isClose)
        {
            // Yakın: thrust animasyonu
            Vector3 originalPos = transform.position;
            Vector3 dir = (targetPos - originalPos).normalized;
            Vector3 thrustPos = originalPos + dir * 0.3f;

            transform.DOMove(thrustPos, 0.08f).SetEase(Ease.OutQuad);
            yield return new WaitForSeconds(0.08f);
            transform.DOMove(originalPos, 0.1f).SetEase(Ease.InQuad);

            if (BattleVfxManager.Instance != null)
                BattleVfxManager.Instance.SpawnSpark(targetPos);
        }
        else
        {
            // Uzak: mızrak fırlatma
            GameObject projObj = new GameObject("Projectile");
            ProjectileController proj = projObj.AddComponent<ProjectileController>();
            proj.Launch(transform.position, targetPos, ProjectileType.Spear, () => { });
        }
    }

    // ── TEMEL (fallback) ───────────────────────────────────────────

    private IEnumerator BasicPunch(Vector3 targetPos)
    {
        Vector3 originalPos = transform.position;
        Vector3 dir = (targetPos - originalPos).normalized;
        Vector3 punchPos = originalPos + dir * 0.15f;

        float t = 0f;
        while (t < 0.06f) { t += Time.deltaTime; transform.position = Vector3.Lerp(originalPos, punchPos, t / 0.06f); yield return null; }
        t = 0f;
        while (t < 0.08f) { t += Time.deltaTime; transform.position = Vector3.Lerp(punchPos, originalPos, t / 0.08f); yield return null; }
        transform.position = originalPos;

        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnSpark(targetPos);
    }

    // ══════════════════════════════════════════════════════════════
    // HEDEF BULMA
    // ══════════════════════════════════════════════════════════════

    private EnemyController FindNearestEnemyInRange()
    {
        EnemyController nearest = null;
        float nearestDist = attackRange;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist <= nearestDist) { nearestDist = dist; nearest = enemy; }
        }

        return nearest;
    }

    // ══════════════════════════════════════════════════════════════
    // HASAR ALMA
    // ══════════════════════════════════════════════════════════════

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        if (spriteRenderer != null)
            StartCoroutine(DamageFlash());

        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth);

        if (currentHealth <= 0f)
            Die();
    }

    private IEnumerator DamageFlash()
    {
        if (spriteRenderer == null) yield break;
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        if (spriteRenderer != null && !isDead)
            spriteRenderer.color = originalColor;
    }

    // ══════════════════════════════════════════════════════════════
    // ÖLÜM
    // ══════════════════════════════════════════════════════════════

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        StartCoroutine(HeroDeathEffect());

        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager != null)
            battleManager.OnHeroDied();
    }

    private IEnumerator HeroDeathEffect()
    {
        if (spriteRenderer == null) yield break;

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
