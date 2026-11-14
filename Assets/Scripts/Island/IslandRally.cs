using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Kışla (island_base_v2 §5, §7.5, Faz E1): savaş başlayınca 1 + ⌊L/5⌋ mavi Lancer doğurur; Lancer'lar toplanma bayrağına gider,
/// bayrak çevresindeki karadaki düşmanı tutar (her Lancer bir düşman) ve vurur. Ölen Lancer 12 sn sonra Kışla kapısında yeniden doğar.
/// Savaş dışında Lancer yoktur (ada görünümünde yalnız bayrak görünür). Yıkık Kışla yeni Lancer doğurmaz.
/// </summary>
public sealed class IslandBarracks : MonoBehaviour
{
    private sealed class Slot { public IslandLancer lancer; public float respawnLeft; }

    private IslandBuilding building;
    private readonly List<Slot> slots = new List<Slot>();
    private bool active;

    public bool Active => active;
    public int Alive { get { int n = 0; foreach (var s in slots) if (s.lancer != null && s.lancer.Alive) n++; return n; } }
    public int SlotCount => slots.Count;

    private void Update()
    {
        if (building == null) building = GetComponent<IslandBuilding>();
        if (building == null) return;
        var setup = IslandSceneSetup.Instance;
        bool defense = setup != null && setup.InDefense;
        if (defense && !active) Activate();
        else if (!defense && active) Deactivate();
        if (!active) return;
        var raid = setup.Raid;
        if (raid != null && raid.Phase != IslandRaidManager.State.Running) return;   // zafer/yenilgi sonrası yeni Lancer yok
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.lancer != null) continue;
            slot.respawnLeft -= Time.deltaTime;
            if (slot.respawnLeft <= 0f && !building.Ruined) Spawn(i);
        }
    }

    private void Activate()
    {
        active = true;
        slots.Clear();
        int count = Progression.Island.LancerCount(building.Level);
        for (int i = 0; i < count; i++) { slots.Add(new Slot()); Spawn(i); }
    }

    private void Deactivate()
    {
        active = false;
        foreach (var slot in slots) if (slot.lancer != null) Destroy(slot.lancer.gameObject);
        slots.Clear();
    }

    private void Spawn(int index)
    {
        var slot = slots[index];
        Vector2 door = IslandLayout.World(building.Spot.DoorCell) + new Vector2((index - (slots.Count - 1) * 0.5f) * 0.4f, 0f);
        var lancer = IslandLancer.Create(transform.parent, building.Level, door, index, slots.Count);
        slot.lancer = lancer;
        lancer.Died += l =>
        {
            if (slot.lancer == l) { slot.lancer = null; slot.respawnLeft = Progression.Island.LancerRespawn; }
        };
    }

    private void OnDestroy() { if (active) Deactivate(); }
}

/// <summary>
/// Mavi Lancer (Tiny Swords): bayrak çevresindeki (<see cref="Progression.Island.LancerEngageRadius"/>) karadaki düşmanı tutar. Köken ayak noktasıdır
/// (sprite kök merkezinde, <see cref="Lift"/> yukarıda). Düşman yakın dövüşçüyse 1,5 u içindeki Lancer'ı hedef alır
/// (<see cref="IslandTargeting"/>); Lancer uçanı vuramaz.
/// </summary>
public sealed class IslandLancer : MonoBehaviour
{
    public static readonly List<IslandLancer> All = new List<IslandLancer>();
    /// <summary>Lancer sprite'ının ayaktan merkeze yüksekliği (Tiny Swords Lancer 320 px kare: ayak ≈ 38 px aşağıda, düşman Lancer ile aynı).</summary>
    public const float Lift = 38f / 64f;

    public bool Alive { get; private set; } = true;
    public float Hp { get; private set; }
    public float MaxHp { get; private set; }
    public float Damage { get; private set; }
    public int Slot { get; private set; }
    public int SlotCount { get; private set; }
    public IslandEnemy Target { get; private set; }
    public Vector2 Position => transform.position;
    public int Hits { get; private set; }
    public event Action<IslandLancer> Died;

    private SpriteRenderer sr;
    private IslandSpriteAnimator anim;
    private HealthBar bar;
    private float nextHit;

    public static IslandLancer Create(Transform parent, int level, Vector2 feet, int slot, int slotCount)
    {
        var go = new GameObject("IslandLancer");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = feet + Vector2.up * Lift;
        var lancer = go.AddComponent<IslandLancer>();
        lancer.Slot = slot; lancer.SlotCount = Mathf.Max(1, slotCount);
        lancer.MaxHp = Progression.Island.LancerHp(level);
        lancer.Hp = lancer.MaxHp;
        lancer.Damage = Progression.Island.LancerDamage(level);
        lancer.sr = go.AddComponent<SpriteRenderer>();
        var catalog = IslandVisualCatalog.Instance;
        var frames = new IslandUnitFrames();
        if (catalog != null)
        {
            frames.idle = catalog.Frames("blue_lancer_idle");
            frames.run = catalog.Frames("blue_lancer_run");
            frames.attack = catalog.Frames("blue_lancer_attack");
        }
        if (frames.HasArt) lancer.sr.sprite = IslandVisualCatalog.Has(frames.idle) ? frames.idle[0] : frames.run[0];
        lancer.anim = go.AddComponent<IslandSpriteAnimator>();
        lancer.anim.Setup(lancer.sr, frames);
        go.AddComponent<IslandYSort>().footOffset = Lift;
        lancer.bar = HealthBar.Create(go.transform, lancer.MaxHp, new Vector3(0f, 0.85f, 0f), new Color(0.35f, 0.65f, 1f));
        return lancer;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    /// <summary>Bu Lancer'ın bekleme noktası (bayrak çevresinde yan yana, kök koordinatı).</summary>
    public Vector2 HomePoint()
    {
        var save = IslandSession.Save;
        int stair = Mathf.Clamp(save != null ? save.flagStair : 0, 0, IslandLayout.FlagPoints.Length - 1);
        Vector2 flag = IslandLayout.FlagPoints[stair];
        return flag + new Vector2((Slot - (SlotCount - 1) * 0.5f) * 0.6f, (Slot % 2) * 0.25f - 0.1f) + Vector2.up * Lift;
    }

    private static bool CanHold(IslandEnemy e) =>
        e != null && e.Alive && IslandTargeting.RoleOf(e.Type) != IslandTargeting.Role.Flyer;

    private int ClaimsOn(IslandEnemy e)
    {
        int n = 0;
        foreach (var other in All) if (other != this && other.Alive && other.Target == e) n++;
        return n;
    }

    private IslandEnemy PickTarget(Vector2 flagRoot)
    {
        IslandEnemy best = null;
        float bestScore = float.MaxValue;
        foreach (var e in IslandEnemy.All)
        {
            if (!CanHold(e)) continue;
            float d = Vector2.Distance(flagRoot, e.AimPoint);
            if (d > Progression.Island.LancerEngageRadius) continue;
            float score = d + ClaimsOn(e) * 100f;   // her Lancer bir düşman: boşta olan önce
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    private float stunUntil;
    public bool Stunned => Time.time < stunUntil;
    /// <summary>Sukkubus büyüsü: Lancer <paramref name="seconds"/> sn boyunca durur, vuramaz.</summary>
    public void Stun(float seconds)
    {
        if (!Alive) return;
        stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
        sr.color = new Color(0.8f, 0.6f, 1f);
        sr.DOColor(Color.white, seconds).SetLink(sr.gameObject);
    }

    private void Update()
    {
        if (!Alive || Time.time < stunUntil) return;
        var setup = IslandSceneSetup.Instance;
        bool fighting = setup == null || setup.Raid == null || setup.Raid.Phase == IslandRaidManager.State.Running;
        Vector2 home = HomePoint();
        if (Target != null && (!CanHold(Target) || Vector2.Distance(home, Target.AimPoint) > Progression.Island.LancerLeash)) Target = null;
        if (Target == null && fighting) Target = PickTarget(home);
        Vector2 goal = home;
        if (Target != null)
        {
            Vector2 aim = Target.AimPoint;
            float d = Vector2.Distance(Position, aim);
            if (d <= Progression.Island.LancerReach)
            {
                goal = Position;
                sr.flipX = aim.x < Position.x;
                if (Time.time >= nextHit)
                {
                    nextHit = Time.time + Progression.Island.LancerHitInterval;
                    Hits++;
                    IslandStats.LancerDamageDealt += Damage;
                    anim.Attack();
                    Target.Controller.TakeDamage(Damage, DamageSource.IslandLancer);
                }
            }
            else goal = aim;
        }
        Vector2 to = goal - Position;
        if (to.sqrMagnitude > 0.04f)
        {
            // 4. oturum (1.1): binaların/suyun içinden geçmez (ortak yol bulma)
            steer = steer ?? new GridSteer(IslandWalkGrid.Shared);
            transform.position = steer.Step(Position, goal, Progression.Island.LancerSpeed * Time.deltaTime, out _);
        }
    }
    private GridSteer steer;

    public void TakeDamage(float amount)
    {
        if (!Alive || amount <= 0f) return;
        Hp = Mathf.Max(0f, Hp - amount);
        if (bar != null) bar.UpdateHealth(Hp);
        sr.DOKill();
        sr.color = new Color(1f, 0.5f, 0.5f);
        sr.DOColor(Color.white, 0.15f).SetLink(sr.gameObject);
        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.ShowDamageNumber(transform.position + Vector3.up * 0.6f, amount, amount);
        if (Hp <= 0f) Die();
    }

    /// <summary>Manastır şifası: tavanı aşmaz. Gerçekten iyileştiyse true.</summary>
    public bool Heal(float amount)
    {
        if (!Alive || amount <= 0f || Hp >= MaxHp) return false;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        if (bar != null) bar.UpdateHealth(Hp);
        return true;
    }

    private void Die()
    {
        Alive = false;
        Target = null;
        IslandFx.Play("dust_1", transform.position - Vector3.up * Lift, 1.3f, 14f);
        Died?.Invoke(this);
        Destroy(gameObject);
    }
}

/// <summary>
/// Manastır (island_base_v2 §5, §7.5, Faz E1): mavi Monk kapıda durur (ada görünümünde de). Savaşta 4 sn'de bir 3 u içindeki binalara,
/// Lancer'lara ve okçuya %8 can verir (Tiny Swords Heal efekti). Onarım süresi ×0,5 ayrıca <see cref="IslandBuilding.CommitAfterBattle"/>'da.
/// </summary>
public sealed class IslandMonastery : MonoBehaviour
{
    private IslandBuilding building;
    private SpriteRenderer monk;
    private Sprite[] idle, heal;
    private float nextHeal, healStart = -10f;
    private float phase;
    /// <summary>Son şifa vuruşunda iyileşen hedef sayısı (test/ekran görüntüsü).</summary>
    public int LastHealed { get; private set; }
    public int Pulses { get; private set; }

    private void Start()
    {
        building = GetComponent<IslandBuilding>();
        var catalog = IslandVisualCatalog.Instance;
        if (catalog == null || building == null) return;
        idle = catalog.Frames("monk_idle");
        heal = catalog.Frames("monk_heal");
        var go = new GameObject("Monk");
        go.transform.SetParent(transform.parent, false);
        Vector2 feet = IslandLayout.World(building.Spot.DoorCell) + new Vector2(1.1f, -0.1f);
        go.transform.position = new Vector3(feet.x, feet.y + 30f / 64f, 0f);   // 192 px kare: ayak merkezden ≈30 px aşağıda
        monk = go.AddComponent<SpriteRenderer>();
        if (IslandVisualCatalog.Has(idle)) monk.sprite = idle[0];
        go.AddComponent<IslandYSort>().footOffset = 30f / 64f;
        phase = UnityEngine.Random.value * 2f;
        nextHeal = Time.time + Progression.Island.MonkHealInterval;
    }

    private void OnDestroy() { if (monk != null) Destroy(monk.gameObject); }

    private void Update()
    {
        if (building == null || monk == null) return;
        bool ruined = building.Ruined;
        if (monk.gameObject.activeSelf == ruined) monk.gameObject.SetActive(!ruined);
        if (ruined) return;
        var setup = IslandSceneSetup.Instance;
        bool fighting = setup != null && setup.InDefense && setup.Raid != null && setup.Raid.Phase == IslandRaidManager.State.Running;
        if (fighting && Time.time >= nextHeal)
        {
            nextHeal = Time.time + Progression.Island.MonkHealInterval;
            Pulse();
        }
        bool healing = Time.time - healStart < 0.9f && IslandVisualCatalog.Has(heal);
        var frames = healing ? heal : idle;
        if (!IslandVisualCatalog.Has(frames)) return;
        float t = healing ? (Time.time - healStart) * 12f : (Time.time + phase) * 8f;
        monk.sprite = frames[Mathf.Abs((int)t) % frames.Length];
    }

    /// <summary>Bir şifa vuruşu (savaşta 4 sn'de bir; testler doğrudan çağırabilir). İyileşen hedef sayısını döndürür.</summary>
    public int Pulse()
    {
        healStart = Time.time;
        Pulses++;
        int healed = 0;
        float radius = Progression.Island.MonkHealRadius, fraction = Progression.Island.MonkHealFraction;
        Vector2 origin = building.Center;
        foreach (var other in IslandBuilding.All)
        {
            if (other == null || other.Structure == null || other.Ruined) continue;
            if (Vector2.Distance(origin, other.Center) > radius + other.Radius) continue;
            if (other.Structure.CurrentHealth >= other.Structure.MaxHealth) continue;
            other.Structure.Heal(other.Structure.MaxHealth * fraction);
            IslandFx.Play("heal_fx", other.Center + Vector2.up * 0.4f, 1.4f, 14f);
            healed++;
        }
        foreach (var lancer in new List<IslandLancer>(IslandLancer.All))
        {
            if (lancer == null || Vector2.Distance(origin, lancer.Position) > radius) continue;
            if (!lancer.Heal(lancer.MaxHp * fraction)) continue;
            IslandFx.Play("heal_fx", lancer.Position + Vector2.up * 0.2f, 1.1f, 14f);
            healed++;
        }
        var archer = IslandArcher.Instance;
        if (archer != null && archer.IsAlive && Vector2.Distance(origin, archer.Position) <= radius && archer.Heal(archer.MaxHp * fraction))
        {
            IslandFx.Play("heal_fx", archer.Position + Vector2.up * 0.2f, 1.1f, 14f);
            healed++;
        }
        LastHealed = healed;
        return healed;
    }
}

/// <summary>
/// Kışla toplanma bayrağı (§7.5): mavi yelken sancağı + zemin köşeleri. Üç merdivenden birinde durur (S1/S2/S3); ada görünümünde
/// dokununca sıradaki merdivene geçer (sürükleme yerine; §20). Konum kayıtta (<see cref="IslandSaveData.flagStair"/>) kalır.
/// </summary>
public sealed class IslandRallyFlag : MonoBehaviour
{
    public int Stair { get; private set; }
    public Vector2 Point => IslandLayout.FlagPoints[Mathf.Clamp(Stair, 0, IslandLayout.FlagPoints.Length - 1)];

    private Transform banner;
    private SpriteRenderer bannerRenderer, brackets, pole;

    public static IslandRallyFlag Create(Transform parent, int stair)
    {
        var go = new GameObject("RallyFlag");
        go.transform.SetParent(parent, false);
        var flag = go.AddComponent<IslandRallyFlag>();
        flag.Build(stair);
        return flag;
    }

    private void Build(int stair)
    {
        Stair = Mathf.Clamp(stair, 0, IslandLayout.FlagPoints.Length - 1);
        var catalog = IslandVisualCatalog.Instance;
        var groundGo = new GameObject("Brackets");
        groundGo.transform.SetParent(transform, false);
        brackets = groundGo.AddComponent<SpriteRenderer>();
        brackets.sprite = catalog != null ? catalog.Single("cursor_brackets") : null;
        brackets.sortingOrder = -4;
        groundGo.transform.localScale = Vector3.one * 0.6f;
        // Cainos Village Props sancağı: direk + üstten asılı bayrak (pivot üstte: dalgalanma üst kenardan)
        var poleGo = new GameObject("Pole");
        poleGo.transform.SetParent(transform, false);
        pole = poleGo.AddComponent<SpriteRenderer>();
        pole.sprite = catalog != null ? catalog.Single("rally_pole") : null;
        poleGo.transform.localScale = Vector3.one * 0.55f;
        var bannerGo = new GameObject("Banner");
        bannerGo.transform.SetParent(transform, false);
        banner = bannerGo.transform;
        bannerRenderer = bannerGo.AddComponent<SpriteRenderer>();
        bannerRenderer.sprite = catalog != null ? catalog.Single("rally_banner") : null;
        banner.localScale = Vector3.one * 0.55f;
        banner.DOLocalRotate(new Vector3(0f, 0f, 4f), 0.9f).From(new Vector3(0f, 0f, -4f)).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(bannerGo);
        Place();
    }

    private void Place()
    {
        Vector2 p = Point;
        transform.position = new Vector3(p.x, p.y, 0f);
        brackets.transform.localPosition = Vector3.zero;
        pole.transform.localPosition = new Vector3(0f, 0.76f, 0f);   // pivot ortada: alt kenar yerde
        banner.localPosition = new Vector3(0f, 1.32f, 0f);           // üst kenar çubuğun altında
        pole.sortingOrder = IslandYSort.OrderFor(p.y, 30);
        bannerRenderer.sortingOrder = IslandYSort.OrderFor(p.y, 31);
    }

    public void SetStair(int stair, bool animate)
    {
        Stair = Mathf.Clamp(stair, 0, IslandLayout.FlagPoints.Length - 1);
        if (!animate) { Place(); return; }
        Vector2 p = Point;
        transform.DOKill();
        transform.DOMove(new Vector3(p.x, p.y, 0f), 0.35f).SetEase(Ease.OutBack).SetLink(gameObject).OnComplete(Place);
    }

    /// <summary>Dünya dokunuşu bayrağa mı (sancak + zemin işareti).</summary>
    public bool Hit(Vector2 world) => Vector2.Distance(world, Point + Vector2.up * 0.9f) < 1.0f;

    private void Update()
    {
        if (brackets == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        brackets.color = new Color(1f, 0.95f, 0.7f, 0.5f + 0.3f * pulse);
    }
}
