using System.Collections;
using UnityEngine;

/// <summary>
/// Okçu Kaptan yetenekleri (island_base.md §3 "Yetenekler", island_base_v2 §7.4): Ok Yağmuru (Sv 2), Ateşli Ok (Sv 4), Ağ Oku (Sv 6).
/// Sayılar <c>Progression.Island</c> içindedir. Ok Yağmuru ve Ağ Oku okçunun şu anki hedefine (kilit / otomatik nişan) uygulanır;
/// hedef yoksa kullanılamaz (bekleme harcanmaz). Ateşli Ok sonraki 3 atışı tutuşturur. HUD düğmeleri <see cref="IslandHud"/>'dadır.
/// </summary>
public sealed class IslandAbilities : MonoBehaviour
{
    public enum Kind { Volley = 0, FireArrow = 1, Net = 2 }

    public static IslandAbilities Instance { get; private set; }
    private static int fireShots;
    private IslandArcher archer;
    private readonly float[] readyAt = new float[3];

    public static IslandAbilities Attach(IslandArcher owner)
    {
        var abilities = owner.gameObject.AddComponent<IslandAbilities>();
        abilities.archer = owner;
        Instance = abilities;
        fireShots = 0;
        return abilities;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public static int UnlockLevel(Kind kind) =>
        kind == Kind.Volley ? Progression.Island.VolleyLevel : kind == Kind.FireArrow ? Progression.Island.FireArrowLevel : Progression.Island.NetLevel;

    public static float CooldownOf(Kind kind) =>
        kind == Kind.Volley ? Progression.Island.VolleyCooldown : kind == Kind.FireArrow ? Progression.Island.FireArrowCooldown : Progression.Island.NetCooldown;

    public static string NameOf(Kind kind) =>
        kind == Kind.Volley ? L.T("Ok Yağmuru", "Volley") : kind == Kind.FireArrow ? L.T("Ateşli Ok", "Fire Arrow") : L.T("Ağ Oku", "Net");

    public bool Unlocked(Kind kind) => archer != null && archer.Level >= UnlockLevel(kind);
    public int FireShotsLeft => fireShots;

    /// <summary>Kalan bekleme oranı (0 hazır, 1 yeni kullanıldı).</summary>
    public float Remaining01(Kind kind) => Mathf.Clamp01((readyAt[(int)kind] - Time.time) / CooldownOf(kind));

    /// <summary>Savaş sırasında, okçu canlı ve koşmuyorken, bekleme dolduysa ve (Ateşli Ok dışında) menzilde hedef varsa.</summary>
    public bool CanUse(Kind kind)
    {
        if (archer == null || !archer.IsAlive || archer.Running || !Unlocked(kind)) return false;
        if (Time.time < readyAt[(int)kind]) return false;
        var setup = IslandSceneSetup.Instance;
        if (setup != null && !setup.InDefense) return false;
        return kind == Kind.FireArrow || archer.Resolve() != null;
    }

    public bool Use(Kind kind)
    {
        if (!CanUse(kind)) return false;
        readyAt[(int)kind] = Time.time + CooldownOf(kind);
        IslandStats.AbilityUses++;
        switch (kind)
        {
            case Kind.FireArrow:
                fireShots = Progression.Island.FireArrowShots;
                IslandFx.FloatText(archer.Position + Vector2.up * 1.3f, L.T("ATEŞLİ OK!", "FIRE ARROW!"), new Color(1f, 0.55f, 0.2f), 4.5f);
                break;
            case Kind.Volley:
            {
                Vector2 center = archer.Resolve().Position;
                IslandFx.FloatText(center + Vector2.up * 1f, L.T("OK YAĞMURU!", "VOLLEY!"), new Color(1f, 0.9f, 0.5f), 4.5f);
                StartCoroutine(VolleyRoutine(center));
                break;
            }
            default:
            {
                Vector2 center = archer.Resolve().Position;
                IslandFx.FloatText(center + Vector2.up * 1f, L.T("AĞ OKU!", "NET!"), new Color(0.7f, 0.9f, 1f), 4.5f);
                IslandFx.Play("dust_2", center, Progression.Island.NetRadius * 2.4f, 14f);
                NetAt(center);
                break;
            }
        }
        return true;
    }

    /// <summary>Ateşli Ok: bir atış tutuşturucu sayılır (okçu atarken çağırır; kalan sayı azalır).</summary>
    public static bool TakeFireShot()
    {
        if (fireShots <= 0) return false;
        fireShots--;
        return true;
    }

    /// <summary>Daire içindeki canlı hedefler (düşman + tekne).</summary>
    public static System.Collections.Generic.List<IslandTarget> InCircle(Vector2 center, float radius)
    {
        var list = new System.Collections.Generic.List<IslandTarget>();
        foreach (var target in IslandTarget.Collect())
            if (Vector2.Distance(target.Position, center) <= radius) list.Add(target);
        return list;
    }

    private IEnumerator VolleyRoutine(Vector2 center)
    {
        float damage = Progression.Island.ArcherDamage(archer.Level);
        float interval = Progression.Island.VolleySeconds / Progression.Island.VolleyHits;
        for (int i = 0; i < Progression.Island.VolleyHits; i++)
        {
            IslandFx.Play("dust_1", center, Progression.Island.VolleyRadius * 1.6f, 16f);
            for (int k = 0; k < 3; k++)
            {
                var spark = IslandFx.NewArrow(Color.white, center + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.6f, 1.2f)));
                IslandFx.PointArrow(spark.transform, Vector2.down);
                Destroy(spark, 0.18f);
            }
            foreach (var target in InCircle(center, Progression.Island.VolleyRadius)) target.Hit(damage, false, archer.Position);
            yield return new WaitForSeconds(interval);
        }
    }

    private void NetAt(Vector2 center)
    {
        foreach (var target in InCircle(center, Progression.Island.NetRadius))
        {
            if (target.enemy != null)
                target.enemy.Net(Progression.Island.NetRootSeconds, Progression.Island.NetSlowSeconds, Progression.Island.NetSlowMult);
            else if (target.boat != null)
                target.boat.Slow(Progression.Island.NetBoatSlowMult, Progression.Island.NetBoatSeconds);
        }
    }
}
