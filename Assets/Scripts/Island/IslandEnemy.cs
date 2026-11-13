using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ada düşmanı: <see cref="EnemyController"/>'ı (hedefleme, saldırı, can) yeniden kullanır. Görsel iki kaynaktan gelir (v2 Faz C):
/// Tiny Swords Kızıl birimler (Warrior/Archer/Lancer kare dizisi) ya da DungeonMonsters2D iskeletli prefabları
/// (katalogdan; kök SortingGroup, yön localScale.x ile, animasyon <c>animator.Play</c> ile; Death klibi). Ayrıca ıslaklık
/// (iniş anında ×1,25 hasar), kalkan/zırh okları, Kasap Kaptan Nara'sı ve y-sıralaması.
/// </summary>
public sealed class IslandEnemy : MonoBehaviour
{
    private static readonly List<IslandEnemy> all = new List<IslandEnemy>();
    public static IReadOnlyList<IslandEnemy> All => all;

    public EnemyController Controller { get; private set; }
    public EnemyType Type { get; private set; }
    public int BeachIndex { get; private set; }
    public bool IsBoss { get; private set; }
    public Vector2 Position => transform.position;
    public bool Alive => Controller != null && !Controller.IsDead;
    public bool IsWet => Time.time < wetUntil;
    /// <summary>DM2D iskeletli görsel mi (false: Tiny Swords kare dizisi).</summary>
    public bool IsMonster => monster != null;
    public Animator MonsterAnimator => animator;
    public string CurrentState { get; private set; } = "";
    public Transform MonsterVisual => monster;
    /// <summary>Bakış yönü sağa mı (DM2D: localScale.x işareti + prefab bakışı).</summary>
    public bool FacingRight { get; private set; } = true;
    public Vector2 MoveDirection { get; private set; } = Vector2.up;
    /// <summary>Faz E2: çağrılan düşmanlar (Batık Şövalye) ve yetenekler için doğuş bilgisi.</summary>
    public int Level { get; private set; } = 1;
    public Vector2[] Route { get; private set; }
    public Transform CastleTransform { get; private set; }
    /// <summary>Boss boy çarpanı (1: boss değil); türe ve turlara göre <see cref="Progression.Island.BossScaleFor"/>.</summary>
    public float BossFactor { get; private set; } = 1f;
    /// <summary>Hayalet "dokunulmaz" evresinde gövde saydamlığı.</summary>
    public float TintAlpha { get; set; } = 1f;

    private SpriteRenderer sr;
    private IslandSpriteAnimator anim;
    private float wetUntil;
    private float lastSeenAttack, lastSeenHit;
    private Color dryTint = Color.white;
    private static readonly Color WetTint = new Color(0.62f, 0.8f, 1f);

    // DM2D
    private Transform monster;
    private Animator animator;
    private SpriteRenderer[] monsterRenderers;
    private MonsterEntry entry;
    private string suffix = "";
    private float attackUntil, hitFlashUntil, flyBob, visualHeight;
    private Vector3 baseVisualScale, baseVisualPosition;
    private Vector3 lastPosition;
    private float nextRoar, roarUntil, specialUntil;
    private bool deathPlayed;
    private float centerX;          // prefab merkezinin x ofseti (k ölçeğinde, çevrilmemiş)
    private int spawnFrame;
    private bool flierLook;
    /// <summary>Vuruş/nişan/seçim noktası: DM2D'de gövde ortası (kök ayaktadır); Tiny Swords'ta kök zaten gövde merkezi.</summary>
    public Vector2 AimPoint => monster != null
        ? (Vector2)transform.position + Vector2.up * (visualHeight * 0.5f + (flierLook ? 0.55f : 0f))
        : (Vector2)transform.position;

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    /// <summary>Elle Akın ölçeği (AŞAMA 2 §4): Akın başlarken <see cref="IslandRaidManager"/> yazar; zamanlı baskında 1.</summary>
    public static float StatScale = 1f;

    /// <summary>Kıyıya inen bir düşman doğurur ve rotasını verir.</summary>
    public static IslandEnemy Spawn(EnemyType type, int battleLevel, Transform castle, Vector2 position,
        Vector2[] route, int beachIndex, bool boss = false, bool wet = true)
    {
        var catalog = IslandVisualCatalog.Instance;
        var entry = catalog != null ? catalog.Monster(type.ToString()) : null;
        bool monsterLook = entry != null && entry.prefab != null;
        // Konum ve rota ayak noktalarıdır. Tiny Swords: kök sprite merkezinde (merkez yüksekliği eklenir); DM2D: kök ayaktadır.
        float lift = monsterLook ? 0f : CenterLift(type, boss, battleLevel);
        position += Vector2.up * lift;
        if (route != null)
        {
            route = (Vector2[])route.Clone();
            for (int i = 0; i < route.Length; i++) route[i] += Vector2.up * lift;
        }
        var go = new GameObject("Island_" + type);
        go.transform.position = position;
        SpriteRenderer sr = monsterLook ? null : go.AddComponent<SpriteRenderer>();
        var controller = go.AddComponent<EnemyController>();
        controller.SuppressDeathEffect = true;
        controller.Initialize(type, battleLevel, castle);
        controller.ScaleStats(StatScale, StatScale);
        controller.SetRoute(route);
        // Genel savaştan gelen kuruntular (kurt sprite'ı, gölge, hareket animasyonu) ada görselinin yerini almasın.
        var motion = go.GetComponent<EnemyMotion>();
        if (motion != null) DestroyImmediate(motion);
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            if (go.transform.GetChild(i).name == "GroundShadow") DestroyImmediate(go.transform.GetChild(i).gameObject);

        var enemy = go.AddComponent<IslandEnemy>();
        enemy.Controller = controller;
        enemy.Type = type;
        enemy.sr = sr;
        enemy.BeachIndex = beachIndex;
        enemy.IsBoss = boss;
        enemy.Level = battleLevel;
        enemy.Route = route;
        enemy.CastleTransform = castle;
        enemy.BossFactor = boss ? Progression.Island.BossScaleFor(type, Progression.Island.AkinOfLevel(battleLevel)) : 1f;
        if (monsterLook) enemy.ApplyMonsterLook(entry, boss);
        else enemy.ApplyLook(type, boss);
        if (wet) enemy.wetUntil = Time.time + Progression.Island.WetSeconds;
        enemy.ApplyWet(true);
        if (monsterLook) go.AddComponent<SortingGroup>();   // YSort Awake'ten önce: grup varsa onu sürer
        go.AddComponent<IslandYSort>().footOffset = lift;
        controller.Died += enemy.OnDied;
        if (IslandSpecials.Has(type)) IslandSpecials.Attach(enemy);
        return enemy;
    }

    /// <summary>Tiny Swords Kızıl birimlerde ayak çizgisi merkezden aşağı (px): Warrior 41, Archer 40, Lancer 38.</summary>
    private static float FeetPixels(EnemyType type) =>
        type == EnemyType.ShoreArcher ? 40f : type == EnemyType.ShieldLancer ? 38f : 41f;

    /// <summary>Birim ayakta durduğu noktadan merkeze kaç dünya birimi yukarıdadır (kök = sprite merkezi).</summary>
    public static float CenterLift(EnemyType type, bool boss, int battleLevel = 1) =>
        FeetPixels(type) / 64f * Progression.Island.UnitScale * (boss ? Progression.Island.BossScaleFor(type, Progression.Island.AkinOfLevel(battleLevel)) : 1f);

    // ==================== TINY SWORDS GÖRSELİ ====================

    private void ApplyLook(EnemyType type, bool boss)
    {
        // Kızıl Warrior (Akıncı/Tayfa), Kızıl Lancer (Kalkanlı Mızrakçı), Kızıl Archer (Kıyı Okçusu); hepsi Tiny Swords 1:1.
        var catalog = IslandVisualCatalog.Instance;
        var frames = new IslandUnitFrames();
        if (catalog != null)
        {
            switch (type)
            {
                case EnemyType.ShoreArcher:
                    frames.idle = catalog.shoreArcherIdle; frames.run = catalog.shoreArcherRun; frames.attack = catalog.shoreArcherShoot; break;
                case EnemyType.ShieldLancer:
                    frames.idle = catalog.Frames("lancer_defence") ?? catalog.Frames("lancer_idle");
                    frames.run = catalog.Frames("lancer_run"); frames.attack = catalog.Frames("lancer_attack"); break;
                default:
                    frames.idle = catalog.raiderIdle; frames.run = catalog.raiderRun; frames.attack = catalog.raiderAttack; break;
            }
        }
        float scale = Progression.Island.UnitScale * BossFactor;
        if (frames.HasArt) sr.sprite = IslandVisualCatalog.Has(frames.idle) ? frames.idle[0] : frames.run[0];
        transform.localScale = new Vector3(scale, scale, 1f);
        sr.color = Color.white;
        dryTint = Color.white;
        Controller.BaseTint = Color.white;
        anim = gameObject.AddComponent<IslandSpriteAnimator>();
        anim.Setup(sr, frames);
        lastSeenAttack = Controller.LastAttackTime;
    }

    // ==================== DM2D GÖRSELİ ====================

    private void ApplyMonsterLook(MonsterEntry monsterEntry, bool boss)
    {
        entry = monsterEntry;
        suffix = IslandTargeting.IsGolem(Type) ? "Golem" : "";   // Golem denetleyicilerinde durum adları "IdleGolem/MoveGolem..." (Magma'da yalnız "Idle"): Play ikisini de dener
        var instance = Instantiate(entry.prefab, transform);
        instance.name = "Monster_" + entry.key;
        monster = instance.transform;
        // Boyut: gövde yüksekliği hedefe normalize edilir (Kızıl Warrior ≈ 1 u); boss ayrıca BossScale.
        float k = entry.targetHeight / Mathf.Max(0.05f, entry.bodyHeight) * BossFactor;
        baseVisualScale = monster.localScale * k;
        visualHeight = entry.targetHeight * BossFactor;
        bool flier = IslandTargeting.RoleOf(Type) == IslandTargeting.Role.Flyer;
        flierLook = flier;
        centerX = entry.centerOffsetX * k;
        baseVisualPosition = new Vector3(0f, -entry.feetOffsetY * k + (flier ? 0.55f : 0f), 0f);
        monster.localScale = baseVisualScale;
        monster.localPosition = baseVisualPosition;
        monsterRenderers = monster.GetComponentsInChildren<SpriteRenderer>(true);
        // Prefab kökündeki Cleave vb. efektler kapalı kalır; yalnız gövde parçaları sıralama grubunda.
        animator = monster.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (!animator.HasState(0, Animator.StringToHash("Idle" + suffix))) suffix = "";
            Play("Idle", true);
        }
        // Gölge: Tiny Swords Shadow.png (gerçek asset), ayağın altında
        var catalog = IslandVisualCatalog.Instance;
        var shadowSprite = catalog != null ? catalog.Single("unit_shadow") : null;
        if (shadowSprite != null && !flier)   // uçanın (suyun üstünde de) yer gölgesi yok
        {
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            float width = Mathf.Max(0.8f, visualHeight * 0.9f);
            shadow.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, shadowSprite.bounds.size.x));
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = shadowSprite;
            shadowRenderer.color = new Color(1f, 1f, 1f, 0.55f);
            shadowRenderer.sortingOrder = -50;
        }
        // Can çubuğu gövdenin üstünde
        var bar = Controller != null ? Controller.Bar : null;
        if (bar != null) bar.Offset = new Vector3(0f, visualHeight + 0.3f + (flier ? 0.55f : 0f), 0f);   // HealthBar kendi konumunu LateUpdate'te Offset'ten sürer
        lastSeenAttack = Controller.LastAttackTime;
        lastSeenHit = Controller.LastHitTime;
        lastPosition = transform.position;
        dryTint = Color.white;
        if (Type == EnemyType.ButcherCaptain) nextRoar = Time.time + Progression.Island.ButcherRoarEvery * 0.5f;
        FacingRight = true;
        ApplyFacing(true);
    }

    private readonly Dictionary<string, string> resolvedStates = new Dictionary<string, string>();
    private string StateName(string baseName) => baseName + suffix;

    /// <summary>Parametresiz animasyon: doğrudan durum adıyla (Idle/Move/Attack/SpecialAttack/Stuned/Death/Climb).</summary>
    public void Play(string baseName, bool force = false)
    {
        if (animator == null) return;
        if (!resolvedStates.TryGetValue(baseName, out string state))
        {
            foreach (var candidate in new[] { StateName(baseName), baseName, baseName + "Golem" })
                if (animator.HasState(0, Animator.StringToHash(candidate))) { state = candidate; break; }
            resolvedStates[baseName] = state;   // yoksa null
        }
        if (state == null) return;
        if (!force && CurrentState == state) return;
        int hash = Animator.StringToHash(state);
        CurrentState = state;
        animator.Play(hash, 0, 0f);
    }

    private void ApplyFacing(bool right)
    {
        FacingRight = right;
        if (monster == null) return;
        // Yön localScale.x ile çevrilir; prefab sola bakıyorsa işaret ters. Gövde merkezi köke hizalanır (çevrilince ofset işaret değiştirir).
        bool positive = entry.facesRight ? right : !right;
        var scale = baseVisualScale;
        scale.x = Mathf.Abs(scale.x) * (positive ? 1f : -1f);
        monster.localScale = scale;
        baseVisualPosition.x = -centerX * (positive ? 1f : -1f);
        monster.localPosition = baseVisualPosition;
    }

    /// <summary>Üçüncü karede (Idle pozu oturunca) canlı SpriteRenderer sınırlarına göre gövdeyi köke hizalar: görsel, can çubuğu, nişan noktası tek yerde.</summary>
    private void Calibrate()
    {
        Bounds bounds = default; bool any = false;
        foreach (var renderer in monsterRenderers)
        {
            if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
            if (!any) { bounds = renderer.bounds; any = true; } else bounds.Encapsulate(renderer.bounds);
        }
        if (!any) return;
        float dx = bounds.center.x - transform.position.x;
        bool positive = monster.localScale.x > 0f;
        baseVisualPosition.x -= dx;
        centerX = -baseVisualPosition.x * (positive ? 1f : -1f);
        if (!flierLook) baseVisualPosition.y -= bounds.min.y - transform.position.y;
        monster.localPosition = baseVisualPosition;
    }

    // ==================== ISLAKLIK / HASAR ====================

    private void ApplyWet(bool wet)
    {
        Controller.DamageTakenMultiplier = wet ? Progression.Island.WetDamageTaken : 1f;
        Color tint = wet ? new Color(dryTint.r * WetTint.r, dryTint.g * WetTint.g, dryTint.b * WetTint.b) : dryTint;
        Controller.BaseTint = tint;
        if (sr != null) sr.color = tint;
        if (monsterRenderers != null)
            foreach (var renderer in monsterRenderers) if (renderer != null) renderer.color = tint;
    }

    /// <summary>Okun gerçek hasarı: Kalkanlı Mızrakçı önden gelen tek atışı ×0,3, Ahşap Golem dolu olmayan oku ×0,6 alır (§7.3).</summary>
    public float ArrowDamage(float damage, bool charged, Vector2? from)
    {
        if (Type == EnemyType.CrystalGolem && charged)
        {
            IslandStats.CrystalWeakHits++;
            return damage * Progression.Island.CrystalGolemChargedTaken;   // zayıf nokta
        }
        if (charged) return damage;
        if (Type == EnemyType.StoneGolem || Type == EnemyType.CrystalGolem)
        {
            IslandStats.GolemArmorHits++;
            return damage * (Type == EnemyType.StoneGolem ? Progression.Island.StoneGolemArrowTaken : Progression.Island.CrystalGolemArrowTaken);
        }
        if (Type == EnemyType.WoodGolem)
        {
            IslandStats.GolemArmorHits++;
            return damage * Progression.Island.GolemArrowTaken;
        }
        if (Type == EnemyType.ShieldLancer && from.HasValue)
        {
            Vector2 toShooter = from.Value - (Vector2)transform.position;
            if (toShooter.sqrMagnitude > 0.01f && MoveDirection.sqrMagnitude > 0.0001f &&
                Vector2.Angle(MoveDirection, toShooter) <= Progression.Island.ShieldFrontArc)
            {
                IslandStats.ShieldBlocks++;
                return damage * Progression.Island.ShieldFrontTaken;
            }
        }
        return damage;
    }

    private bool wasWet = true;
    private float roarMult = 1f, rootUntil, slowUntil, slowMult = 1f, burnUntil, burnDps, burnTick;

    // ==================== YETENEK ETKİLERİ (Faz E2: Ağ Oku, Ateşli Ok) ====================

    /// <summary>Ağ Oku: <paramref name="rootSeconds"/> sn kökleşir, sonra <paramref name="slowSeconds"/> sn hızı ×<paramref name="mult"/>.</summary>
    public void Net(float rootSeconds, float slowSeconds, float mult)
    {
        if (!Alive) return;
        rootUntil = Time.time + rootSeconds;
        slowUntil = rootUntil + slowSeconds;
        slowMult = mult;
        Play("Stuned", true);
        specialUntil = Time.time + rootSeconds;
    }

    /// <summary>Yürüyüş rotasının <paramref name="at"/> noktasına en yakın dalından sonrası (çağrılan birlik geriye yürümesin).</summary>
    public Vector2[] RouteFromHere(Vector2 at)
    {
        if (Route == null || Route.Length == 0) return Route;
        int best = 0;
        for (int i = 1; i < Route.Length; i++)
            if (Vector2.Distance(Route[i], at) < Vector2.Distance(Route[best], at)) best = i;
        var rest = new Vector2[Route.Length - best];
        System.Array.Copy(Route, best, rest, 0, rest.Length);
        return rest;
    }

    public bool Rooted => Time.time < rootUntil;
    public bool Slowed => Time.time < slowUntil;
    public bool Burning => Alive && Time.time < burnUntil;

    /// <summary>Ateşli Ok: <paramref name="seconds"/> sn boyunca saniyede <paramref name="dps"/> hasar.</summary>
    public void Ignite(float seconds, float dps)
    {
        if (!Alive) return;
        burnUntil = Time.time + seconds;
        burnDps = dps;
        burnTick = Time.time + 0.5f;
        IslandFx.Play("fire_1", AimPoint + Vector2.up * 0.2f, 0.9f, 14f, false, transform);
    }

    private void TickEffects()
    {
        Controller.SpeedMultiplier = roarMult * (Time.time < rootUntil ? 0f : Time.time < slowUntil ? slowMult : 1f);
        if (Alive && Time.time < burnUntil && Time.time >= burnTick)
        {
            burnTick = Time.time + 0.5f;
            Controller.TakeDamage(burnDps * 0.5f, DamageSource.IslandArcher);
        }
    }

    private void Update()
    {
        if (Controller == null) return;
        Vector2 delta = transform.position - lastPosition;
        if (delta.sqrMagnitude > 0.000001f && Alive) MoveDirection = delta.normalized;
        TickEffects();
        bool wet = IsWet;
        if (wet != wasWet) { wasWet = wet; ApplyWet(wet); }
        if (Controller.LastAttackTime != lastSeenAttack)
        {
            lastSeenAttack = Controller.LastAttackTime;
            if (anim != null) anim.Attack();
            attackUntil = Time.time + 0.55f;
        }
        if (monster != null && ++spawnFrame == 3) Calibrate();
        if (monster != null && Alive) UpdateMonster(delta);
        lastPosition = transform.position;
    }

    private void UpdateMonster(Vector2 delta)
    {
        bool moving = delta.sqrMagnitude > 0.000004f;
        if (Mathf.Abs(delta.x) > 0.0005f) ApplyFacing(delta.x > 0f);
        else if (!moving && Controller.CurrentTarget != null)
        {
            float dx = Controller.CurrentTarget.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.3f) ApplyFacing(dx > 0f);
        }
        // Kasap Kaptan Nara'sı: 15 sn'de bir +%30 hız, 4 sn (§7.3)
        if (Type == EnemyType.ButcherCaptain)
        {
            if (Time.time >= nextRoar && Time.time > 4f)
            {
                nextRoar = Time.time + Progression.Island.ButcherRoarEvery;
                roarUntil = Time.time + Progression.Island.ButcherRoarSeconds;
                IslandFx.FloatText(transform.position + Vector3.up * (visualHeight + 0.9f), L.T("NARA!", "ROAR!"), new Color(1f, 0.55f, 0.3f), 5f);
                specialUntil = Time.time + 0.6f;
            }
            roarMult = Time.time < roarUntil ? Progression.Island.ButcherRoarSpeed : 1f;
        }
        if (Controller.LastHitTime != lastSeenHit)
        {
            lastSeenHit = Controller.LastHitTime;
            hitFlashUntil = Time.time + 0.1f;
        }
        bool flash = Time.time < hitFlashUntil;
        if (flash) TintMonster(new Color(1f, 0.45f, 0.4f));
        else if (monsterRenderers != null && monsterRenderers.Length > 0 && monsterRenderers[0] != null && monsterRenderers[0].color != CurrentTint()) TintMonster(CurrentTint());

        if (Rooted) Play("Stuned");
        else if (Time.time < specialUntil) Play("SpecialAttack");
        else if (Time.time < attackUntil) Play("Attack");
        else if (moving) Play(Type == EnemyType.Rat && IslandLayout.IsCliff(Mathf.FloorToInt(transform.position.x),
            IslandLayout.Height - 1 - Mathf.FloorToInt(transform.position.y)) ? "Climb" : "Move");
        else Play("Idle");
        if (IslandTargeting.RoleOf(Type) == IslandTargeting.Role.Flyer)
            monster.localPosition = baseVisualPosition + Vector3.up * (Mathf.Sin(Time.time * 6f + transform.position.x) * 0.08f);
    }

    private Color CurrentTint()
    {
        Color c = IsWet ? new Color(dryTint.r * WetTint.r, dryTint.g * WetTint.g, dryTint.b * WetTint.b) : dryTint;
        c.a *= TintAlpha;
        return c;
    }

    private void TintMonster(Color color)
    {
        foreach (var renderer in monsterRenderers) if (renderer != null) renderer.color = color;
    }

    // ==================== ÖLÜM ====================

    private void OnDied(EnemyController dead)
    {
        if (deathPlayed) return;
        deathPlayed = true;
        var bar = Controller != null ? Controller.Bar : null;
        if (bar != null) bar.gameObject.SetActive(false);
        Vector2 feet = transform.position - Vector3.up * (monster != null ? 0f : FeetPixels(Type) / 64f * Progression.Island.UnitScale);
        if (monster != null)
        {
            // DM2D: Death klibi; sonra solar (Tiny Swords tozu ayağın altında)
            Play("Death", true);
            float length = ClipLength("Death");
            TintMonster(dryTint);
            IslandFx.Play("dust_2", feet, 1.2f, 14f);
            StartCoroutine(FadeAndDestroy(Mathf.Clamp(length, 0.5f, 1.4f), 0.45f));
        }
        else
        {
            // Tiny Swords ölüm karesi yok: toz + solma (mevcut Dust FX)
            IslandFx.Play("dust_1", feet, 1.3f, 14f);
            if (sr != null) sr.DOFade(0f, 0.4f).SetLink(gameObject);
            Destroy(gameObject, 0.5f);
        }
    }

    private float ClipLength(string baseName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 0.8f;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip != null && clip.name.StartsWith(baseName)) return clip.length;
        return 0.8f;
    }

    private System.Collections.IEnumerator FadeAndDestroy(float wait, float fade)
    {
        yield return new WaitForSeconds(wait);
        float t = 0f;
        while (t < fade)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / fade);
            foreach (var renderer in monsterRenderers)
                if (renderer != null) { var c = renderer.color; c.a = a; renderer.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }
}
