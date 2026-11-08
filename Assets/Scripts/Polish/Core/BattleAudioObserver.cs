using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>Reads combat state for sound cues; never writes combat state.</summary>
public sealed class BattleAudioObserver : MonoBehaviour
{
    private static readonly FieldInfo LastAttack = typeof(HeroController)
        .GetField("lastAttackTime", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly HashSet<int> reportedDeaths = new HashSet<int>();
    private HeroController hero;
    private float lastAttackTime;

    private void Update()
    {
        if (hero == null) hero = FindFirstObjectByType<HeroController>();
        if (hero != null && LastAttack != null)
        {
            float attackAt = (float)LastAttack.GetValue(hero);
            if (attackAt > lastAttackTime && attackAt > 0f)
                AudioManager.Instance?.PlaySfx("swordHit");
            lastAttackTime = attackAt;
        }
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (enemy != null && enemy.IsDead && reportedDeaths.Add(enemy.GetInstanceID()))
                AudioManager.Instance?.PlaySfx("enemyDeath");
    }
}
