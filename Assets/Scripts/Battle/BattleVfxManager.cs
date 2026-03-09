using UnityEngine;

/// <summary>
/// Savas sahnesi VFX yoneticisi.
/// Resources/VFX altindaki prefab'lari yukler ve kullanir.
/// </summary>
public class BattleVfxManager : MonoBehaviour
{
    public static BattleVfxManager Instance { get; private set; }

    private GameObject hitPrefab;
    private GameObject deathPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        hitPrefab = Resources.Load<GameObject>("VFX/Green hit");
        deathPrefab = Resources.Load<GameObject>("VFX/Explosion");
    }

    public void SpawnHit(Vector3 position, float scale = 0.7f)
    {
        Spawn(hitPrefab, position, scale, 2f);
    }

    public void SpawnDeath(Vector3 position, float scale = 0.9f)
    {
        Spawn(deathPrefab, position, scale, 2.2f);
    }

    public void SpawnBuild(Vector3 position, float scale = 0.6f)
    {
        Spawn(hitPrefab, position, scale, 1.8f);
    }

    private void Spawn(GameObject prefab, Vector3 position, float scale, float lifeTime)
    {
        if (prefab == null)
            return;

        GameObject fx = Instantiate(prefab, position, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;
        Destroy(fx, lifeTime);
    }
}
