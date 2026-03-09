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

    public void Setup(BattleManager manager, BuildingType type, int cost)
    {
        battleManager = manager;
        buildType = type;
        buildCost = cost;
    }

    private void Awake()
    {
        slotRenderer = GetComponent<SpriteRenderer>();
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
        }
    }
}
