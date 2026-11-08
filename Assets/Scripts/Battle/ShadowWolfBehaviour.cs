using System.Collections;
using UnityEngine;

/// <summary>
/// Gölge Kurt: kahramana 1.4–3.6 birim mesafede kısa bir hazırlanma sonrası
/// atılır. Atılma sonunda kahramana yakınsa 1.6x hasar verir. 4 sn bekleme.
/// Atılma menzili kahramanın görüş yarıçapının içinde kaldığı için hazırlanma
/// her zaman görünür; kırmızı parıltı oyuncuya kaçma şansı tanır.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public sealed class ShadowWolfBehaviour : MonoBehaviour
{
    public const float MinPounceDistance = 1.4f;
    public const float MaxPounceDistance = 3.6f;
    public const float Cooldown = 4f;
    public const float Windup = 0.35f;
    public const float DashDuration = 0.22f;
    public const float HitRadius = 1.1f;
    public const float DamageMultiplier = 1.6f;

    private EnemyController enemy;
    private SpriteRenderer spriteRenderer;
    private float nextPounce;

    public bool IsPouncing { get; private set; }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        nextPounce = Time.time + 0.3f;
    }

    private void Update()
    {
        if (IsPouncing || enemy.IsDead || Time.time < nextPounce) return;
        // Hedefi beklemeden kahramana bakar: normal düşman 2.7 birimde hedef alır,
        // Gölge Kurt 3.6 birimden atılabilir.
        HeroController hero = FindFirstObjectByType<HeroController>();
        if (hero == null || hero.CurrentHealth <= 0f) return;
        float distance = Vector2.Distance(transform.position, hero.transform.position);
        if (distance < MinPounceDistance || distance > MaxPounceDistance) return;
        StartCoroutine(Pounce(hero));
    }

    private IEnumerator Pounce(HeroController hero)
    {
        IsPouncing = true;
        enemy.MovementLocked = true;
        nextPounce = Time.time + Cooldown;

        // Hazırlanma: sinmiş poz + kırmızımsı parıltı.
        Vector3 baseScale = transform.localScale;
        transform.localScale = new Vector3(baseScale.x * 1.12f, baseScale.y * 0.85f, 1f);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            spriteRenderer.color = new Color(0.85f, 0.35f, 0.45f, 1f);
            yield return new WaitForSeconds(Windup);
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.42f, 0.36f, 0.58f, c.a);
        }
        else yield return new WaitForSeconds(Windup);
        transform.localScale = baseScale;

        if (enemy.IsDead || hero == null) { Finish(); yield break; }

        Vector3 start = transform.position;
        Vector3 toHero = hero.transform.position - start;
        // Kahramanın biraz ötesine atıl; kaçan oyuncu ıskalatabilir.
        Vector3 end = start + toHero.normalized * (toHero.magnitude + 0.6f);
        if (spriteRenderer != null && toHero.x != 0) spriteRenderer.flipX = toHero.x > 0;

        bool hit = false;
        float t = 0f;
        while (t < 1f)
        {
            if (enemy.IsDead) { Finish(); yield break; }
            t += Time.deltaTime / DashDuration;
            transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            if (!hit && hero != null && hero.CurrentHealth > 0f &&
                Vector2.Distance(transform.position, hero.transform.position) <= HitRadius)
            {
                hit = true;
                hero.TakeDamage(enemy.Damage * DamageMultiplier);
            }
            yield return null;
        }
        Finish();
    }

    private void Finish()
    {
        IsPouncing = false;
        if (enemy != null) enemy.MovementLocked = false;
    }
}
