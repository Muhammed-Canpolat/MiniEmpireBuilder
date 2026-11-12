using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ada düşmanı: mevcut <see cref="EnemyController"/>'ı (hedefleme, saldırı, can, ölüm) yeniden kullanır; bu bileşen
/// yalnız ada görselini (katalog ya da yedek), ıslaklığı (iniş anında ×1,25 hasar) ve y-sıralamasını ekler.
/// </summary>
public sealed class IslandEnemy : MonoBehaviour
{
    private static readonly List<IslandEnemy> all = new List<IslandEnemy>();
    public static IReadOnlyList<IslandEnemy> All => all;

    public EnemyController Controller { get; private set; }
    public int BeachIndex { get; private set; }
    public bool IsBoss { get; private set; }
    public Vector2 Position => transform.position;
    public bool Alive => Controller != null && !Controller.IsDead;
    public bool IsWet => Time.time < wetUntil;

    private SpriteRenderer sr;
    private IslandSpriteAnimator anim;
    private float wetUntil;
    private float baseTakenMultiplier = 1f;
    private float lastSeenAttack;
    private Color dryTint = Color.white;
    private static readonly Color WetTint = new Color(0.62f, 0.8f, 1f);

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    /// <summary>Kıyıya inen bir düşman doğurur ve rotasını verir.</summary>
    public static IslandEnemy Spawn(EnemyType type, int battleLevel, Transform castle, Vector2 position,
        Vector2[] route, int beachIndex, bool boss = false, bool wet = true)
    {
        // Konum ve rota ayak noktalarıdır; kök sprite merkezinde durduğu için merkez yüksekliği eklenir.
        float lift = CenterLift(type, boss);
        position += Vector2.up * lift;
        if (route != null)
        {
            route = (Vector2[])route.Clone();
            for (int i = 0; i < route.Length; i++) route[i] += Vector2.up * lift;
        }
        var go = new GameObject("Island_" + type);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        var controller = go.AddComponent<EnemyController>();
        controller.Initialize(type, battleLevel, castle);
        controller.SetRoute(route);
        // Genel savaştan gelen kuruntular (kurt sprite'ı, gölge, hareket animasyonu) ada görselinin yerini almasın.
        var motion = go.GetComponent<EnemyMotion>();
        if (motion != null) DestroyImmediate(motion);
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            if (go.transform.GetChild(i).name == "GroundShadow") DestroyImmediate(go.transform.GetChild(i).gameObject);

        var enemy = go.AddComponent<IslandEnemy>();
        enemy.Controller = controller;
        enemy.sr = sr;
        enemy.BeachIndex = beachIndex;
        enemy.IsBoss = boss;
        enemy.ApplyLook(type, boss);
        if (boss) enemy.baseTakenMultiplier = 1f / Progression.Island.BossHpMult;
        if (wet) enemy.wetUntil = Time.time + Progression.Island.WetSeconds;
        enemy.ApplyWet(true);
        go.AddComponent<IslandYSort>().footOffset = lift;
        return enemy;
    }

    /// <summary>Tiny Swords Kızıl birimlerde ayak çizgisi merkezden aşağı (px): Warrior 41, Archer 40, Lancer 38.</summary>
    private static float FeetPixels(EnemyType type) =>
        type == EnemyType.ShoreArcher ? 40f : type == EnemyType.Raider ? 38f : 41f;

    /// <summary>Birim ayakta durduğu noktadan merkeze kaç dünya birimi yukarıdadır (kök = sprite merkezi).</summary>
    public static float CenterLift(EnemyType type, bool boss) =>
        FeetPixels(type) / 64f * Progression.Island.UnitScale * (boss ? Progression.Island.BossScale : 1f);

    private void ApplyLook(EnemyType type, bool boss)
    {
        // v2 Faz A: Kızıl Warrior (Tayfa/Akıncı), Kızıl Lancer (Akıncı), Kızıl Archer (Kıyı Okçusu); hepsi Tiny Swords 1:1.
        var catalog = IslandVisualCatalog.Instance;
        var frames = new IslandUnitFrames();
        if (catalog != null)
        {
            switch (type)
            {
                case EnemyType.ShoreArcher:
                    frames.idle = catalog.shoreArcherIdle; frames.run = catalog.shoreArcherRun; frames.attack = catalog.shoreArcherShoot; break;
                case EnemyType.Raider:
                    frames.idle = catalog.Frames("lancer_idle"); frames.run = catalog.Frames("lancer_run"); frames.attack = catalog.Frames("lancer_attack"); break;
                default:
                    frames.idle = catalog.raiderIdle; frames.run = catalog.raiderRun; frames.attack = catalog.raiderAttack; break;
            }
        }
        float scale = Progression.Island.UnitScale * (boss ? Progression.Island.BossScale : 1f);
        if (frames.HasArt) sr.sprite = IslandVisualCatalog.Has(frames.idle) ? frames.idle[0] : frames.run[0];
        transform.localScale = new Vector3(scale, scale, 1f);
        sr.color = Color.white;
        dryTint = Color.white;
        Controller.BaseTint = Color.white;
        anim = gameObject.AddComponent<IslandSpriteAnimator>();
        anim.Setup(sr, frames);
        lastSeenAttack = Controller.LastAttackTime;
    }

    private void ApplyWet(bool wet)
    {
        Controller.DamageTakenMultiplier = baseTakenMultiplier * (wet ? Progression.Island.WetDamageTaken : 1f);
        Color tint = wet ? new Color(dryTint.r * WetTint.r, dryTint.g * WetTint.g, dryTint.b * WetTint.b) : dryTint;
        Controller.BaseTint = tint;
        if (sr != null) sr.color = tint;
    }

    private bool wasWet = true;

    private void Update()
    {
        if (Controller == null) return;
        bool wet = IsWet;
        if (wet != wasWet) { wasWet = wet; ApplyWet(wet); }
        if (Controller.LastAttackTime != lastSeenAttack)
        {
            lastSeenAttack = Controller.LastAttackTime;
            if (anim != null) anim.Attack();
        }
    }
}
