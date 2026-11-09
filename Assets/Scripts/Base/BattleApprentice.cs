using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Friendly pawn follower. Enemy and wave controllers remain unchanged.</summary>
public sealed class BattleApprentice : MonoBehaviour
{
    private HeroController hero;
    private BattleManager battle;
    private Animator animator;
    private SpriteRenderer renderer;
    private Image healthFill;
    private GameObject hud;
    private float maxHealth;
    private float currentHealth;
    private float damage;
    private float nextAttack;
    private float nextContactDamage;
    private float returnToIdle;
    private string color;
    private string animationState;
    private Vector2 followVelocity;
    private TextMeshProUGUI healthLabel;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private IEnumerator Start()
    {
        for (int i = 0; i < 120 && hero == null; i++)
        {
            hero = FindFirstObjectByType<HeroController>();
            if (hero == null) yield return null;
        }
        if (hero == null || GameManager.Instance?.PlayerData == null)
        {
            Destroy(gameObject);
            yield break;
        }
        yield return null;
        battle = FindFirstObjectByType<BattleManager>();
        int level = Mathf.Clamp(GameManager.Instance.PlayerData.apprenticeLevel, 1, Progression.MaxLevel);
        var progression = ApprenticeProgressionData.Instance;
        // 15 seviye, 5 renk kademesi: seviye doğrudan indeks değil (6+ seviyede çöküyordu).
        color = new[] { "Blue", "Yellow", "Red", "Purple", "Black" }[BaseVisualCatalog.ColorIndex(level)];
        var weapon = GameManager.Instance.PlayerData.hero.weaponType;
        maxHealth = progression != null ? progression.Health(weapon, level) : hero.MaxHealth * 0.5f;
        currentHealth = maxHealth;
        damage = progression != null ? progression.Damage(weapon, level)
            : GameManager.Instance.PlayerData.hero.damage * 0.25f;
        transform.position = hero.transform.position + (Vector3)FollowOffset;
        renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 34;
        animator = gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = BaseVisualCatalog.Instance?.GetUnitController(
            BuildingType.GoldMine, level);
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (animator.runtimeAnimatorController != null) animator.Rebind();
        Play("Pawn_Idle Axe_" + color);
        if (animator.runtimeAnimatorController != null) animator.Update(0f);
        if (renderer.sprite == null && animator.runtimeAnimatorController != null)
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == "Pawn_Idle Axe_" + color)
                {
                    clip.SampleAnimation(gameObject, 0f);
                    break;
                }
        var heroRenderer = hero.GetComponent<SpriteRenderer>();
        for (int frame = 0; frame < 12 &&
            (renderer.sprite == null || heroRenderer == null || heroRenderer.sprite == null); frame++)
            yield return null;
        if (renderer.sprite != null && heroRenderer != null && heroRenderer.sprite != null)
        {
            // Pawn frames contain more transparent padding than hero frames.
            // The larger renderer bounds make the painted pawn about 75% of the hero.
            // Pawn kareleri boşluklu: 1.1 çarpanıyla çizili figür kahramanın ~%70'i.
            float targetHeight = BattleActorPresentation.HeroHeight * 1.1f;
            transform.localScale = Vector3.one * (targetHeight /
                Mathf.Max(0.01f, renderer.sprite.bounds.size.y));
        }
        // Kahramanın biraz önünde durduğu için onun üstünde çizilir.
        renderer.sortingOrder = heroRenderer != null ? heroRenderer.sortingOrder + 1 : 34;
        CreateHud();
    }

    // Kahramanın solunda, hafif önünde: kahramanın gövdesi (≈0.8 yarı genişlik) onu örtmez.
    private static readonly Vector2 FollowOffset = new Vector2(-1.1f, -0.15f);

    private void Update()
    {
        if (hero == null || currentHealth <= 0f || battle == null || !battle.IsBattleActive) return;
        var heroRenderer = hero.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = heroRenderer != null ? heroRenderer.sortingOrder + 1 : 34;
        Vector2 desired = (Vector2)hero.transform.position + FollowOffset;
        Vector2 before = transform.position;
        Vector2 followed = Vector2.SmoothDamp(before, desired, ref followVelocity, 0.12f);
        Vector2 fromHero = followed - (Vector2)hero.transform.position;
        if (fromHero.magnitude > 3f)
            followed = (Vector2)hero.transform.position + fromHero.normalized * 3f;
        followed = BattleObstacles.Resolve(followed + HeroController.FeetOffset, 0.3f) - HeroController.FeetOffset;
        transform.position = followed;
        bool moving = Vector2.Distance(before, followed) > 0.01f;
        if (moving) renderer.flipX = desired.x < before.x;
        if (Time.time >= nextAttack)
        {
            EnemyController nearest = null;
            float distance = 1.2f;
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.IsDead || !FogOfWar.IsVisible(enemy.transform.position)) continue;
                float candidate = Vector2.Distance(enemy.transform.position, transform.position);
                if (candidate < distance) { distance = candidate; nearest = enemy; }
            }
            if (nearest != null)
            {
                renderer.flipX = nearest.transform.position.x < transform.position.x;
                Play("Pawn_Interact Axe_" + color);
                nearest.TakeDamage(damage);
                if (!nearest.IsDead && distance <= 0.85f && Time.time >= nextContactDamage)
                {
                    var stats = EnemyStats.GetBaseStats(nearest.Type);
                    // Seferde düşmanla aynı seviye (bölge×5 + aşama); eskiden hep 1 alınıyordu.
                    stats.ScaleToLevel(BattleSession.IsCampaign
                        ? (BattleSession.RegionId - 1) * 5 + BattleSession.LevelNumber
                        : GameManager.Instance.PlayerData.currentBattleLevel);
                    TakeDamage(stats.damage);
                    nextContactDamage = Time.time + 1f;
                }
                nextAttack = Time.time + 1f;
                returnToIdle = Time.time + 0.42f;
                return;
            }
        }
        if (Time.time >= returnToIdle)
            Play(moving ? "Pawn_Run Axe_" + color : "Pawn_Idle Axe_" + color);
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (healthFill != null) healthFill.fillAmount = currentHealth / maxHealth;
        if (healthLabel != null)
            healthLabel.text = $"{Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}";
        if (currentHealth <= 0f)
        {
            if (renderer != null) renderer.enabled = false;
            if (animator != null) animator.enabled = false;
        }
    }

    private void Play(string state)
    {
        if (animator == null || animator.runtimeAnimatorController == null || animationState == state)
            return;
        animationState = state;
        animator.Play(state, 0, 0f);
    }

    private void CreateHud()
    {
        var safe = GameObject.Find("BattleCanvas")?.transform.Find("SafeArea");
        if (safe == null) return;
        var group = new GameObject("ApprenticeHealth", typeof(RectTransform));
        group.transform.SetParent(safe, false);
        hud = group;
        var rect = group.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.68f, 0.075f);
        rect.anchorMax = new Vector2(0.98f, 0.15f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var box = group.AddComponent<Image>();
        box.color = ReferenceUiVisuals.Background;
        box.raycastTarget = false;
        ReferenceUiVisuals.Text(rect, "Name", "Çırak", 28,
            ReferenceUiVisuals.Light, TextAlignmentOptions.Left,
            new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.98f),
            Vector2.zero, Vector2.zero);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(rect, false);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.04f, 0.37f);
        labelRect.anchorMax = new Vector2(0.96f, 0.68f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var text = label.GetComponent<TextMeshProUGUI>();
        text.text = $"{Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}";
        healthLabel = text;
        ReferenceUiVisuals.ApplyFont(text, 26f, ReferenceUiVisuals.Light);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = ReferenceUiVisuals.Light;
        text.raycastTarget = false;
        var background = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(rect, false);
        var barRect = background.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.04f, 0.08f);
        barRect.anchorMax = new Vector2(0.96f, 0.34f);
        barRect.offsetMin = barRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.85f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(barRect, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        healthFill = fill.GetComponent<Image>();
        healthFill.sprite = BaseMapManager.MakeSquareSprite(Color.white);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.color = new Color(0.37f, 0.75f, 0.47f);
        healthFill.fillAmount = 1f;
        // Pawn karesi atlas içinde ve boşluklu (kırpılamıyor): yuvada maskelenip büyütülür.
        BattleHudRedesign.StyleCard(rect, 2, renderer != null ? HeroPortraitSprite.Get(renderer.sprite) : null,
            ReferenceUiVisuals.Hex(0x6FB8E8), 2.4f);
    }

    private void OnDestroy()
    {
        if (hud != null) Destroy(hud);
    }
}
