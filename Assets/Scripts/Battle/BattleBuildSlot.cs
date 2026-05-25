using UnityEngine;

/// <summary>
/// Savaş alanındaki inşa slotu.
/// Kahraman slot alanına girince altın yeterliyse otomatik inşa tetikler.
/// </summary>
public class BattleBuildSlot : MonoBehaviour
{
    [SerializeField] private BuildingType buildType;
    [SerializeField] private int buildCost;

    private BattleManager battleManager;
    private bool isBuilt;
    private SpriteRenderer slotRenderer;

    private Sprite builtSprite;
    private HealthBar healthBar;

    public void Setup(BattleManager manager, BuildingType type, int cost)
    {
        battleManager = manager;
        buildType = type;
        buildCost = cost;
    }

    private void Start()
    {
        slotRenderer = GetComponent<SpriteRenderer>();

        if (SpriteManager.Instance != null)
        {
            builtSprite = SpriteManager.Instance.GetBuildingSprite(buildType);
            if (builtSprite != null)
            {
                slotRenderer.sprite = builtSprite;
                slotRenderer.color = new Color(1f, 1f, 1f, 0.4f); // Yarı saydam blueprint
            }
        }

        // Health bar'ı oluştur (sıfır dolu, çünkü henüz inşa edilmedi)
        float maxHp = 100f; 
        if (GameManager.Instance != null && GameManager.Instance.Buildings.ContainsKey(buildType))
        {
            var data = GameManager.Instance.Buildings[buildType];
            if (buildType == BuildingType.WallBuilder)
            {
                maxHp = BuildingData.GetWallHealthFromBuilder(data.level);
            }
            else
            {
                data.ApplyLevelStats();
                maxHp = data.maxHealth;
            }
        }

        healthBar = HealthBar.Create(transform, maxHp, new Vector3(0, 0.8f, 0));
        if (healthBar != null) healthBar.UpdateHealth(0f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isBuilt || battleManager == null || !battleManager.IsBattleActive)
            return;

        HeroController hero = other.GetComponent<HeroController>();
        if (hero == null)
            return;

        bool built = battleManager.TryBuildFromSlot(this, buildType, transform.position, buildCost);
        if (built)
        {
            isBuilt = true;
            if (slotRenderer != null)
                slotRenderer.enabled = false;
            
            if (healthBar != null)
                Destroy(healthBar.gameObject);
        }
    }
}
