using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Savaşta kurulan kule/Manastır: can, can çubuğu, hasar efekti, yürünemez alan ve yıkılma.
/// Yıkılınca yuvada yıkıntı kalır; yuva yeniden kurulabilir (bedel her yıkılışta +%50).
/// Düşman hedef seçimi kayıtlı yapıları <see cref="All"/> üzerinden okur.
/// </summary>
public sealed class BattleStructure : MonoBehaviour
{
    private static readonly List<BattleStructure> registry = new List<BattleStructure>();
    public static IReadOnlyList<BattleStructure> All => registry;

    public BuildingType Type { get; private set; }
    public int Level { get; private set; }
    public BattleBuildSlot Slot { get; private set; }
    public Vector2 BasePoint { get; private set; }
    public float MaxHealth { get; private set; } = 100f;
    public float CurrentHealth { get; private set; } = 100f;
    public bool IsDestroyed => CurrentHealth <= 0f;
    public float HealthPercent => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;

    public event Action<BattleStructure> Destroyed;

    private SpriteRenderer body;
    private HealthBar healthBar;
    private Coroutine flash;

    public void Initialize(BuildingType type, int level, BattleBuildSlot slot, Vector2 basePoint, float visualHeight)
    {
        Type = type;
        Level = Mathf.Max(1, level);
        Slot = slot;
        BasePoint = basePoint;
        body = GetComponent<SpriteRenderer>();
        var data = GameManager.Instance != null ? GameManager.Instance.CreateLevelData(type, Level) : null;
        MaxHealth = data != null && data.maxHealth > 0f ? data.maxHealth : 100f;
        CurrentHealth = MaxHealth;
        float top = basePoint.y + visualHeight + 0.15f - transform.position.y;
        healthBar = HealthBar.Create(transform, MaxHealth, new Vector3(0f, top, 0f), ReferenceUiVisuals.Hex(0x7FC8F8));
        // Kahraman ve Çırak yapının içinden geçemez.
        BattleObstacles.Register(this, BattleObstacles.Footprint(basePoint, 1.25f, 0.25f, 0.75f));
        registry.Add(this);
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (healthBar != null) healthBar.UpdateHealth(CurrentHealth);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(transform.position + Vector3.up * 0.6f, amount, amount);
        if (body != null)
        {
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(Flash());
        }
        if (IsDestroyed) Collapse();
    }

    public void Heal(float amount)
    {
        if (IsDestroyed || amount <= 0f || CurrentHealth >= MaxHealth) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        if (healthBar != null) healthBar.UpdateHealth(CurrentHealth);
    }

    private IEnumerator Flash()
    {
        body.color = new Color(1f, 0.55f, 0.55f);
        yield return new WaitForSeconds(0.1f);
        if (body != null) body.color = Color.white;
        flash = null;
    }

    private void Collapse()
    {
        BattleObstacles.Unregister(this);
        registry.Remove(this);
        BaseBuildingVisual.SpawnDust((Vector3)BasePoint + Vector3.up * 0.2f, 160);
        Slot?.OnStructureDestroyed();
        Destroyed?.Invoke(this);
        if (healthBar != null) Destroy(healthBar.gameObject);
        transform.DOKill();
        transform.DOScaleY(transform.localScale.y * 0.2f, 0.25f).SetEase(Ease.InBack);
        if (body != null) body.DOFade(0f, 0.25f);
        Destroy(gameObject, 0.3f);
    }

    private void OnDestroy()
    {
        BattleObstacles.Unregister(this);
        registry.Remove(this);
        if (healthBar != null) Destroy(healthBar.gameObject);
    }
}
