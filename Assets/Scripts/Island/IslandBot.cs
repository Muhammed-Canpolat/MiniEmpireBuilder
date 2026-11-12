using UnityEngine;

/// <summary>
/// Ada savunma botu (island_base_v2 §11 telemetri): test ve ekran görüntüleri için oyuncu yerine atış yeri seçer ve ateş eder.
/// Profiller: <see cref="Profile.Good"/> (en çok hedefi kapsayan atış yerine koşar, oto-ateş + sık dokunuş),
/// <see cref="Profile.Casual"/> (yalnız oto-ateş, yer değiştirmez), <see cref="Profile.None"/> (hiçbir şey yapmaz; savunmasız ölçüm).
/// Oyunda hiçbir yerde kendiliğinden eklenmez.
/// </summary>
public sealed class IslandBot : MonoBehaviour
{
    public enum Profile { None, Casual, Good }

    public Profile Mode = Profile.Good;
    public IslandArcher Archer;
    public int KeepLevel = 1;
    private float nextTap, nextPlan, chargeStart;
    private bool charging;

    /// <summary>Sahnedeki ada kurulumuna bir bot ekler (testler yansımayla çağırır).</summary>
    public static IslandBot Attach(IslandSceneSetup setup, Profile profile)
    {
        var bot = setup.gameObject.AddComponent<IslandBot>();
        bot.Mode = profile;
        bot.Archer = setup.Archer;
        bot.KeepLevel = IslandSession.KeepLevel();
        return bot;
    }

    /// <summary>Savunmasız ölçüm: kuleler ateş etmez (okçuyu bot susturur: Profile.None).</summary>
    public static void StripDefenses()
    {
        foreach (var tower in FindObjectsByType<IslandTower>(FindObjectsSortMode.None)) tower.enabled = false;
    }

    private void Update()
    {
        if (Archer == null || !Archer.IsAlive || Mode == Profile.None) return;
        Archer.AutoFire = true;
        if (Mode != Profile.Good || Archer.Running) return;
        if (Time.time >= nextPlan)
        {
            nextPlan = Time.time + 0.35f;
            Plan();
        }
        var target = Archer.Resolve();
        bool armored = target != null && target.enemy != null &&
            (target.enemy.Type == EnemyType.ShieldLancer || IslandTargeting.IsGolem(target.enemy.Type) || Progression.Island.IsBossType(target.enemy.Type)
             || target.enemy.Type == EnemyType.Demon || target.enemy.Type == EnemyType.ZombieWarrior);
        UseAbilities(target, armored);
        if (armored)
        {
            // Zırhlı/kalkanlı/boss: dolu atış (basılı tut, ≥1,2 sn sonra bırak); oto-ateş basılıyken susar.
            if (!charging) { charging = true; chargeStart = Time.time; Archer.PressFire(); }
            else if (Time.time - chargeStart >= Progression.Island.ChargeFull + 0.05f)
            {
                Archer.ReleaseFire();
                charging = false;
                nextTap = Time.time + 0.2f;
            }
            return;
        }
        if (charging) { Archer.ReleaseFire(); charging = false; }
        if (Time.time >= nextTap)
        {
            nextTap = Time.time + 0.5f;
            Archer.PressFire();
            Archer.ReleaseFire();
        }
    }

    /// <summary>İyi bot yetenekleri: Ok Yağmuru ≥3 hedef kümesinde, Ateşli Ok zırhlı/boss hedefte, Ağ Oku boss/golem/elit hedefte.</summary>
    private void UseAbilities(IslandTarget target, bool armored)
    {
        var abilities = IslandAbilities.Instance;
        if (abilities == null || target == null) return;
        if (abilities.CanUse(IslandAbilities.Kind.Volley) && IslandAbilities.InCircle(target.Position, Progression.Island.VolleyRadius).Count >= 3)
            abilities.Use(IslandAbilities.Kind.Volley);
        if (armored && abilities.CanUse(IslandAbilities.Kind.FireArrow)) abilities.Use(IslandAbilities.Kind.FireArrow);
        if (armored && target.enemy != null && !target.enemy.Rooted && abilities.CanUse(IslandAbilities.Kind.Net)) abilities.Use(IslandAbilities.Kind.Net);
    }

    /// <summary>Hedef sayısı (kaleye yakın olanlar 3×) en yüksek atış yerine geçer; mevcut yerde hedef varsa kalır.</summary>
    private void Plan()
    {
        var targets = IslandTarget.Collect();
        if (targets.Count == 0) return;
        int best = Archer.CurrentPost;
        float here = Score(Archer.CurrentPost, targets);
        float bestScore = here > 0f ? here + 1.5f : 0f;   // menzilde hedef varsa yer değiştirme eşiği; yoksa hedefi olan en iyi yere git
        for (int i = 0; i < IslandLayout.Posts.Length; i++)
        {
            if (i == Archer.CurrentPost || !IslandLayout.PostUnlocked(i, KeepLevel)) continue;
            float score = Score(i, targets);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        if (best != Archer.CurrentPost) Archer.MoveToPost(best);
    }

    private static float Score(int post, System.Collections.Generic.List<IslandTarget> targets)
    {
        var info = IslandLayout.Posts[post];
        float score = 0f;
        foreach (var target in targets)
        {
            if (Vector2.Distance(info.World, target.Position) > info.Range) continue;
            score += Vector2.Distance(target.Position, IslandLayout.CastleCenter) < 6f ? 3f : 1f;
        }
        return score;
    }
}
