using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Okçu Kaptan yetenekleri (island_base.md §3 "Yetenekler", island_base_v2 §7.4): Ok Yağmuru (Sv 2), Ateşli Ok (Sv 4), Ağ Oku (Sv 6).
/// Sayılar <c>Progression.Island</c> içindedir. <see cref="Use"/> (bot) okçunun şu anki hedefine uygular, hedef yoksa kullanılamaz
/// (bekleme harcanmaz); <see cref="UseAt"/> oyuncunun seçtiği noktaya uygular (Ok Yağmuru için nişan kipi: BeginAim → SetAim → ConfirmAim).
/// Ateşli Ok sonraki 3 atışı tutuşturur. HUD düğmeleri <see cref="IslandHud"/>'dadır.
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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (aimRing != null) Destroy(aimRing);
    }

    private void OnDisable() { CancelAim(); }

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

    /// <summary>Kalan bekleme (sn).</summary>
    public float RemainingSeconds(Kind kind) => Mathf.Max(0f, readyAt[(int)kind] - Time.time);

    /// <summary>Okçu canlı, koşmuyor, yetenek açık, bekleme doldu ve savunmadayız (hedef şartı yok).</summary>
    public bool CanReady(Kind kind)
    {
        if (archer == null || !archer.IsAlive || archer.Running || !Unlocked(kind)) return false;
        if (Time.time < readyAt[(int)kind]) return false;
        var setup = IslandSceneSetup.Instance;
        return setup == null || setup.InDefense;
    }

    /// <summary>Savaş sırasında, okçu canlı ve koşmuyorken, bekleme dolduysa ve (Ateşli Ok dışında) menzilde hedef varsa.</summary>
    public bool CanUse(Kind kind) => CanReady(kind) && (kind == Kind.FireArrow || archer.Resolve() != null);

    /// <summary>Anlık kullanım (bot): Ok Yağmuru / Ağ okçunun şu anki hedefine uygulanır.</summary>
    public bool Use(Kind kind)
    {
        if (!CanUse(kind)) return false;
        Vector2 center = kind == Kind.FireArrow ? archer.Position : archer.Resolve().Position;
        Activate(kind, center);
        return true;
    }

    /// <summary>Oyuncu seçimi: yeteneği <paramref name="center"/> noktasına uygular (hedef şartı yok; nokta okçu menziline + 2 u ile sınırlanır).</summary>
    public bool UseAt(Kind kind, Vector2 center)
    {
        if (!CanReady(kind)) return false;
        Activate(kind, kind == Kind.FireArrow ? archer.Position : ClampToRange(center));
        return true;
    }

    private void Activate(Kind kind, Vector2 center)
    {
        readyAt[(int)kind] = Time.time + CooldownOf(kind);
        IslandStats.AbilityUses++;
        switch (kind)
        {
            case Kind.FireArrow:
                fireShots = Progression.Island.FireArrowShots;
                IslandFx.FloatText(archer.Position + Vector2.up * 1.3f, L.T("ATEŞLİ OK!", "FIRE ARROW!"), new Color(1f, 0.55f, 0.2f), 4.5f);
                IslandFx.Play("fire_2", archer.Position + Vector2.up * 0.5f, 1.1f, 14f);
                IslandAbilityArt.Sfx("arrowShoot");
                IslandAbilityArt.Sfx("fireball");   // 10. oturum (D110): Fireball 1 = Ateşli Ok fırlatışı
                break;
            case Kind.Volley:
                IslandFx.FloatText(center + Vector2.up * 1f, L.T("OK YAĞMURU!", "VOLLEY!"), new Color(1f, 0.9f, 0.5f), 4.5f);
                StartCoroutine(VolleyRoutine(center));
                break;
            default:
                IslandFx.FloatText(center + Vector2.up * 1f, L.T("AĞ OKU!", "NET!"), new Color(0.7f, 0.9f, 1f), 4.5f);
                IslandAbilityArt.Sfx("arrowShoot");
                StartCoroutine(NetRoutine(center));
                break;
        }
    }

    // ---- Ok Yağmuru nişan kipi: düğme → halka önizleme, haritaya dokun → halka taşınır, düğme tekrar → ateş (4 sn sonra kendiliğinden) ----
    public const float AimSeconds = 4f;
    public bool Aiming { get; private set; }
    public Vector2 AimCenter { get; private set; }
    private float aimUntil;
    private GameObject aimRing;

    public Vector2 ClampToRange(Vector2 point)
    {
        Vector2 from = archer.Position;
        float max = archer.Range + 2f;
        Vector2 delta = point - from;
        return delta.magnitude > max ? from + delta.normalized * max : point;
    }

    public bool BeginAim()
    {
        if (Aiming) return true;
        if (!CanReady(Kind.Volley)) return false;
        var target = archer.Resolve();
        AimCenter = ClampToRange(target != null ? target.Position : archer.Position + Vector2.up * 3f);
        Aiming = true;
        aimUntil = Time.time + AimSeconds;
        if (aimRing == null) aimRing = BuildAimRing();
        aimRing.transform.position = AimCenter;
        aimRing.SetActive(true);
        return true;
    }

    public void SetAim(Vector2 world)
    {
        if (!Aiming) return;
        AimCenter = ClampToRange(world);
        aimUntil = Time.time + AimSeconds;
    }

    public bool ConfirmAim()
    {
        if (!Aiming) return false;
        CancelAim();
        return UseAt(Kind.Volley, AimCenter);
    }

    public void CancelAim()
    {
        Aiming = false;
        if (aimRing != null) aimRing.SetActive(false);
    }

    private static GameObject BuildAimRing()
    {
        var root = new GameObject("VolleyAimRing");
        float diameter = Progression.Island.VolleyRadius * 2f;
        var ring = new GameObject("Ring");
        ring.transform.SetParent(root.transform, false);
        var rs = ring.AddComponent<SpriteRenderer>();
        rs.sprite = IslandAbilityArt.Ring();
        rs.color = new Color(1f, 0.85f, 0.3f, 0.95f);
        rs.sortingOrder = IslandFx.FxSorting - 6;
        ring.transform.localScale = Vector3.one * diameter;
        var fill = new GameObject("Fill");
        fill.transform.SetParent(root.transform, false);
        var fs = fill.AddComponent<SpriteRenderer>();
        fs.sprite = IslandAbilityArt.Disc();
        fs.color = new Color(1f, 0.8f, 0.25f, 0.18f);
        fs.sortingOrder = IslandFx.FxSorting - 7;
        fill.transform.localScale = Vector3.one * diameter;
        return root;
    }

    private void Update()
    {
        if (!Aiming) return;
        var setup = IslandSceneSetup.Instance;
        if (archer == null || !archer.IsAlive || archer.Running || (setup != null && !setup.InDefense)) { CancelAim(); return; }
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
        aimRing.transform.position = AimCenter;
        aimRing.transform.localScale = Vector3.one * (1f + 0.04f * pulse);
        if (Time.time >= aimUntil) ConfirmAim();
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

    private const int VolleyArrows = 15;

    private IEnumerator VolleyRoutine(Vector2 center)
    {
        float damage = Progression.Island.ArcherDamage(archer.Level);
        float radius = Progression.Island.VolleyRadius;
        int hits = Mathf.Max(1, Progression.Island.VolleyHits);
        float step = Progression.Island.VolleySeconds / VolleyArrows;
        int perHit = Mathf.Max(1, VolleyArrows / hits);
        for (int n = 0; n < VolleyArrows; n++)
        {
            // Gökten düşen ok: dairenin içinde rastgele nokta, yukarıdan çapraz iniş
            Vector2 land = center + Random.insideUnitCircle * radius * 0.95f;
            Vector2 from = land + new Vector2(Random.Range(-0.6f, 0.6f), 5.5f);
            var arrow = IslandFx.NewArrow(new Color(1f, 0.97f, 0.85f), from);
            IslandFx.PointArrow(arrow.transform, land - from);
            bool dust = n % 2 == 0;
            arrow.transform.DOMove(land, 0.3f).SetEase(Ease.InQuad).SetLink(arrow).OnComplete(() =>
            {
                if (dust) IslandFx.Play("dust_1", land, 0.7f, 18f);
                Destroy(arrow);
            });
            if (n % perHit == 0 && n / perHit < hits)
            {
                // Toplam 3 hasar vuruşu (3 × ok hasarı), her biri dalganın üçte birinin başında
                foreach (var target in InCircle(center, radius)) target.Hit(damage, false, archer.Position);
                IslandAbilityArt.Sfx("swordHit");
                IslandCameraShake.Shake(0.08f, 0.12f);
            }
            yield return new WaitForSeconds(step);
        }
    }

    private IEnumerator NetRoutine(Vector2 center)
    {
        // Ağ atılışı: okçudan hedefe 0,35 sn yay çizerek giden, dönen halat ağı (GEÇİCİ kodla çizilmiş sprite)
        var go = new GameObject("NetProjectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = IslandAbilityArt.Net();
        sr.sortingOrder = IslandFx.FxSorting + 1;
        Vector2 from = archer.Position + Vector2.up * 0.8f;
        float full = Progression.Island.NetRadius * 2f;
        const float flight = 0.35f;
        for (float t = 0f; t < flight; t += Time.deltaTime)
        {
            float k = t / flight;
            go.transform.position = Vector2.Lerp(from, center, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, full, k);
            go.transform.rotation = Quaternion.Euler(0f, 0f, k * 420f);
            yield return null;
        }
        Destroy(go);
        IslandFx.Play("dust_2", center, full * 1.2f, 14f);
        IslandAbilityArt.Sfx("swordHit");
        IslandCameraShake.Shake(0.06f, 0.1f);
        NetAt(center);
    }

    private void NetAt(Vector2 center)
    {
        foreach (var target in InCircle(center, Progression.Island.NetRadius))
        {
            if (target.enemy != null)
            {
                target.enemy.Net(Progression.Island.NetRootSeconds, Progression.Island.NetSlowSeconds, Progression.Island.NetSlowMult);
                IslandNetOverlay.Attach(target.enemy.transform, target.Position, 1.4f, Progression.Island.NetRootSeconds);
            }
            else if (target.boat != null)
            {
                target.boat.Slow(Progression.Island.NetBoatSlowMult, Progression.Island.NetBoatSeconds);
                IslandNetOverlay.Attach(target.boat.transform, target.Position, 2.4f, Progression.Island.NetBoatSeconds);
            }
        }
    }
}
