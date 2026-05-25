// [DAMAGE NUMBER POOL] — Mini Empire Builder
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamageNumberPool : MonoBehaviour
{
    private const float CriticalMultiplier = 1.5f;

    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private int poolSize = 20;

    private readonly Queue<DamageNumber> pool = new Queue<DamageNumber>();

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            DamageNumber instance = Instantiate(damageNumberPrefab, transform);
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
        }
    }

    /// <summary>
    /// Spawns and animates damage number at world position.
    /// </summary>
    public void ShowDamage(Vector3 position, float damage, float baselineDamage)
    {
        DamageNumber number = GetFromPool();
        number.transform.position = position;
        number.gameObject.SetActive(true);
        bool isCritical = damage > baselineDamage * CriticalMultiplier;
        number.Play(damage, isCritical);
    }

    private DamageNumber GetFromPool()
    {
        if (pool.Count > 0)
        {
            DamageNumber n = pool.Dequeue();
            pool.Enqueue(n);
            return n;
        }

        DamageNumber created = Instantiate(damageNumberPrefab, transform);
        pool.Enqueue(created);
        return created;
    }
}
