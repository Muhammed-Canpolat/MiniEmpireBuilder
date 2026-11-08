using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Sis" mekaniği olan bölgelerde oynanışa etki eden görüş sistemi.
/// Kahraman, kurulu kuleler ve Manastır çevrelerini aydınlatır. Görüş dışındaki
/// düşmanlar soluk siluet olur (Gölge Kurt tamamen kaybolur) ve dost birimler
/// onları hedef alamaz. Sis yoksa <see cref="IsVisible"/> her zaman true döner.
/// </summary>
public sealed class FogOfWar : MonoBehaviour
{
    public const float HeroRadius = 6.5f;
    public const float MonasteryRadius = 4.5f;
    public const float TowerExtraRadius = 0.5f;
    public const float HiddenAlpha = 0.16f;
    public const float ShadowHiddenAlpha = 0f;
    private const float FadeSpeed = 6f;
    private const float RefreshInterval = 0.25f;

    public static FogOfWar Active { get; private set; }

    private readonly List<Vector3> revealerPositions = new List<Vector3>();
    private readonly List<float> revealerRadii = new List<float>();
    private readonly Dictionary<EnemyController, float> alphas = new Dictionary<EnemyController, float>();
    private readonly List<EnemyController> stale = new List<EnemyController>();
    private EnemyController[] enemies = new EnemyController[0];
    private HeroController hero;
    private TowerController[] towers = new TowerController[0];
    private MonasteryHealer[] monasteries = new MonasteryHealer[0];
    private float nextRefresh;

    /// <summary>Sis aktif değilse veya nokta herhangi bir aydınlatıcının içindeyse true.</summary>
    public static bool IsVisible(Vector3 position) => Active == null || Active.Contains(position);

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private void Update()
    {
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + RefreshInterval;
            hero = FindFirstObjectByType<HeroController>();
            towers = FindObjectsByType<TowerController>(FindObjectsSortMode.None);
            monasteries = FindObjectsByType<MonasteryHealer>(FindObjectsSortMode.None);
            enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        }

        RebuildRevealers();
        UpdateEnemyVisibility();
    }

    private void RebuildRevealers()
    {
        revealerPositions.Clear();
        revealerRadii.Clear();
        if (hero != null && hero.CurrentHealth > 0f) Add(hero.transform.position, HeroRadius);
        foreach (var tower in towers)
            if (tower != null && !tower.IsDestroyed)
                Add(tower.transform.position, tower.AttackRange + TowerExtraRadius);
        foreach (var monastery in monasteries)
            if (monastery != null) Add(monastery.transform.position, MonasteryRadius);
    }

    private void Add(Vector3 position, float radius)
    {
        revealerPositions.Add(position);
        revealerRadii.Add(radius);
    }

    private bool Contains(Vector3 position)
    {
        for (int i = 0; i < revealerPositions.Count; i++)
        {
            float r = revealerRadii[i];
            if (((Vector2)(position - revealerPositions[i])).sqrMagnitude <= r * r) return true;
        }
        return false;
    }

    private void UpdateEnemyVisibility()
    {
        float step = FadeSpeed * Time.deltaTime;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || enemy.FogOverride) continue;
            bool visible = Contains(enemy.transform.position);
            float hidden = enemy.Type == EnemyType.ShadowWolf ? ShadowHiddenAlpha : HiddenAlpha;
            float target = visible ? 1f : hidden;
            float current = alphas.TryGetValue(enemy, out float a) ? a : hidden;
            current = Mathf.MoveTowards(current, target, step);
            alphas[enemy] = current;
            enemy.SetFogAlpha(current);
        }

        stale.Clear();
        foreach (var pair in alphas)
            if (pair.Key == null || pair.Key.IsDead) stale.Add(pair.Key);
        foreach (var key in stale) alphas.Remove(key);
    }
}
