using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Bölge 5 (Karanlık Kral): sahaya Lanetli Türbeler dikilir (aşama 1: 1, sonra 2). Her türbe
/// <see cref="Progression.RegionMechanics.ShrineInterval"/> sn'de bir uyarı halkasından sonra Karanlık yayar:
/// yakınındaki kuleler menzilin %60'ını görür. Yalnız kahraman türbeye zarar verir.
/// </summary>
public sealed class ShadowShrines : MonoBehaviour
{
    // Yollardan uzak, üs ile kenarlar arasında sabit noktalar.
    private static readonly Vector2[] Points = { new Vector2(-6.2f, 8.5f), new Vector2(6.2f, -8.5f) };

    private void Start()
    {
        int count = Mathf.Min(Points.Length, Progression.RegionMechanics.ShrineCount(BattleSession.LevelNumber));
        for (int i = 0; i < count; i++) CursedShrine.Create(Points[i], i * 4f);
    }
}

/// <summary>Tek Lanetli Türbe: kendi canı, yalnız Kahraman hasarı, periyodik Karanlık.</summary>
public sealed class CursedShrine : MonoBehaviour
{
    private static readonly List<CursedShrine> registry = new List<CursedShrine>();
    public static IReadOnlyList<CursedShrine> All => registry;

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsDestroyed => CurrentHealth <= 0f;
    public bool DarknessActive { get; private set; }

    private HealthBar healthBar;
    private SpriteRenderer glow;
    private GameObject darkDisc;
    private GameObject warnRing;
    private float startDelay;

    public static CursedShrine Create(Vector2 position, float extraDelay = 0f)
    {
        var go = new GameObject("CursedShrine");
        go.transform.position = new Vector3(position.x, position.y, 0f);
        var shrine = go.AddComponent<CursedShrine>();
        shrine.startDelay = Progression.RegionMechanics.ShrineFirstDelay + extraDelay;
        return shrine;
    }

    /// <summary>Türbe canı: 120 × düşman can çarpanı (savaş seviyesine göre).</summary>
    public static float HealthFor(int battleLevel)
    {
        var stats = EnemyStats.GetBaseStats(EnemyType.Wolf);
        float baseHp = Mathf.Max(1f, stats.health);
        stats.ScaleToLevel(Mathf.Max(1, battleLevel));
        return Progression.RegionMechanics.ShrineBaseHp * (stats.health / baseHp);
    }

    /// <summary>Verilen noktadaki kule menzil çarpanı (yok = 1).</summary>
    public static float RangeMultiplierAt(Vector2 position)
    {
        float multiplier = 1f;
        foreach (var shrine in registry)
            if (shrine.DarknessActive && !shrine.IsDestroyed
                && Vector2.Distance(shrine.transform.position, position) <= Progression.RegionMechanics.ShrineRadius)
                multiplier = Mathf.Min(multiplier, Progression.RegionMechanics.ShrineRangeMultiplier);
        return multiplier;
    }

    /// <summary>Kahramanın vuruş menzilindeki en yakın türbe (türbe yarıçapı ~0.5 sayılır).</summary>
    public static CursedShrine NearestInReach(Vector2 from, float reach)
    {
        CursedShrine best = null;
        float bestDist = reach + 0.5f;
        foreach (var shrine in registry)
        {
            if (shrine.IsDestroyed) continue;
            float dist = Vector2.Distance(from, shrine.transform.position);
            if (dist <= bestDist) { bestDist = dist; best = shrine; }
        }
        return best;
    }

    private void Awake()
    {
        MaxHealth = CurrentHealth = HealthFor(BattleSession.BattleLevel);
        registry.Add(this);
        BuildVisual();
        healthBar = HealthBar.Create(transform, MaxHealth, new Vector3(0f, 1.9f, 0f), new Color(0.65f, 0.3f, 0.9f));
    }

    private void Start() => StartCoroutine(Cycle());

    /// <summary>Yalnız kahraman hasarı işler; kule, çırak, çevre ve diğer kaynaklar yok sayılır.</summary>
    public void TakeDamage(float damage, DamageSource source)
    {
        if (IsDestroyed || damage <= 0f || source != DamageSource.Hero) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        if (healthBar != null) healthBar.UpdateHealth(CurrentHealth);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(transform.position + Vector3.up * 1.2f, damage, damage);
        transform.DOComplete();
        transform.DOPunchScale(new Vector3(0.1f, -0.1f, 0f), 0.15f, 6, 0.6f);
        if (IsDestroyed) Break();
    }

    private void Break()
    {
        DarknessActive = false;
        registry.Remove(this);
        RegionMechanicsStats.ShrinesDestroyed++;
        BattleManager.Instance?.AddBattleGold(Progression.RegionMechanics.ShrineGold);
        if (BattleVfxManager.Instance != null)
        {
            BattleVfxManager.Instance.ShowNoticeBanner(L.T("Lanetli Türbe yıkıldı! +" + Progression.RegionMechanics.ShrineGold + " altın",
                "Cursed Shrine destroyed! +" + Progression.RegionMechanics.ShrineGold + " gold"));
            BattleVfxManager.Instance.SpawnSmallDust(transform.position);
        }
        TinyFx.Explosion(transform.position, 2f);
        if (healthBar != null) Destroy(healthBar.gameObject);
        if (darkDisc != null) Destroy(darkDisc);
        if (warnRing != null) Destroy(warnRing);
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>()) renderer.DOFade(0f, 0.4f);
        Destroy(gameObject, 0.5f);
    }

    private IEnumerator Cycle()
    {
        yield return new WaitForSeconds(startDelay);
        while (!IsDestroyed)
        {
            var battle = BattleManager.Instance;
            if (battle != null && battle.IsBattleActive)
            {
                // Uyarı: büyüyen mor halka.
                warnRing = BattleWorldMarkers.DashedCircle(transform, Progression.RegionMechanics.ShrineRadius,
                    new Color(0.7f, 0.3f, 1f, 0.9f), 30);
                warnRing.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                float warn = Progression.RegionMechanics.ShrineWarnSeconds;
                glow.DOFade(1f, warn).From(0.3f).SetLink(gameObject);
                yield return new WaitForSeconds(warn);
                if (warnRing != null) Destroy(warnRing);
                if (IsDestroyed) yield break;

                // Karanlık: koyu mor disk + menzil düşüşü.
                DarknessActive = true;
                ShowDarkDisc(true);
                yield return new WaitForSeconds(Progression.RegionMechanics.ShrineDarkSeconds);
                DarknessActive = false;
                ShowDarkDisc(false);
            }
            yield return new WaitForSeconds(Progression.RegionMechanics.ShrineInterval
                - Progression.RegionMechanics.ShrineWarnSeconds - Progression.RegionMechanics.ShrineDarkSeconds);
        }
    }

    private void ShowDarkDisc(bool on)
    {
        if (darkDisc == null) return;
        darkDisc.SetActive(on);
        if (!on) return;
        var renderer = darkDisc.GetComponent<SpriteRenderer>();
        renderer.DOKill();
        renderer.color = new Color(0.18f, 0.02f, 0.3f, 0f);
        renderer.DOFade(0.38f, 0.4f).SetLink(darkDisc);
        // Nabız: karanlık yayılırken disk hafifçe titreşir.
        renderer.DOFade(0.24f, 0.9f).SetDelay(0.4f).SetLoops(-1, LoopType.Yoyo).SetLink(darkDisc);
    }

    private void BuildVisual()
    {
        var circle = BaseMapManager.MakeCircleSprite(Color.white);
        var square = BaseMapManager.MakeSquareSprite(Color.white);
        float unit = circle.bounds.size.x;

        var shadow = Child("Base", circle, new Color(0.12f, 0.02f, 0.2f, 0.85f), 9);
        shadow.transform.localScale = new Vector3(1.5f / unit, 0.6f / unit, 1f);
        var pillar = Child("Pillar", square, new Color(0.3f, 0.12f, 0.4f), 10);
        pillar.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        pillar.transform.localScale = new Vector3(0.5f / square.bounds.size.x, 1.4f / square.bounds.size.y, 1f);
        glow = Child("Glow", circle, new Color(0.8f, 0.4f, 1f, 0.9f), 11);
        glow.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        glow.transform.localScale = Vector3.one * (0.6f / unit);
        glow.DOFade(0.45f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);

        var disc = Child("DarkDisc", circle, new Color(0.18f, 0.02f, 0.3f, 0f), -90);
        disc.transform.localScale = Vector3.one * (Progression.RegionMechanics.ShrineRadius * 2f / unit);
        darkDisc = disc.gameObject;
        darkDisc.SetActive(false);
    }

    private SpriteRenderer Child(string name, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    private void OnDestroy() => registry.Remove(this);
}
