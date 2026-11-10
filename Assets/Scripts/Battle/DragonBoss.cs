using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Ejder Savaşçısı (Bölge 6 boss'u). Kare animasyonları Resources/Dragon sayfalarından
/// (Tools/dragon/make_sheets.py). Normal düşman gibi Ana Üs'e yürür; sırayla iki yetenek:
///   Uçan Tekme: 0.6 sn hazırlanır, savaşçıya atılır; iniş noktasında alan hasarı.
///   Yer Sarsıntısı: 0.8 sn işaret, çevresindeki kulelere ve savaşçıya hasar.
/// Canı %50 altına inince bekleme 6 sn'den 4.5 sn'ye iner.
/// </summary>
[RequireComponent(typeof(EnemyController))]
public sealed class DragonBoss : MonoBehaviour
{
    public enum Ability { FlyKick, Quake }

    public const float AbilityRange = 9f;
    public const float Cooldown = 6f;
    public const float EnragedCooldown = 4.5f;
    public const float KickRadius = 1.4f;
    public const float KickDamageFactor = 1.6f;
    public const float QuakeRadius = 3f;
    public const float QuakeDamageFactor = 1.2f;
    private const float FrameHeight = 3.1f;
    private const int Columns = 6;
    private const float Fps = 10f;

    private static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();

    private EnemyController enemy;
    private SpriteRenderer spriteRenderer;
    private Sprite[] current;
    private string currentName;
    private float frameClock;
    private bool loop = true;
    private float lockedUntil;
    private float nextAbility;
    private int rotation;
    private Vector3 lastPosition;
    private bool facingLeft = true;
    private float lastSeenAttack;
    private float lastSeenHit;
    private bool busy;

    public bool IsBusy => busy;
    public Ability NextAbility => rotation % 2 == 0 ? Ability.FlyKick : Ability.Quake;

    /// <summary>Animasyon sayfasını karelere böler (6 sütun, satır satır).</summary>
    public static Sprite[] Frames(string anim)
    {
        if (sheets.TryGetValue(anim, out var cached) && cached != null) return cached;
        var texture = Resources.Load<Texture2D>("Dragon/dragon_" + anim);
        if (texture == null) return sheets[anim] = new Sprite[0];
        int cellW = texture.width / Columns;
        int cellH = cellW * 114 / 172; // make_sheets.py hücre oranı
        int rows = Mathf.Max(1, Mathf.RoundToInt((float)texture.height / cellH));
        cellH = texture.height / rows;
        var list = new List<Sprite>();
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < Columns; c++)
        {
            var rect = new Rect(c * cellW, texture.height - (r + 1) * cellH, cellW, cellH);
            if (IsEmpty(texture, rect)) continue;
            // Pivot ayak hizasında (hücrenin altından biraz yukarı).
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.06f), 100f);
            sprite.name = $"dragon_{anim}_{list.Count}";
            list.Add(sprite);
        }
        return sheets[anim] = list.ToArray();
    }

    /// <summary>Son satırda boş hücre olabilir; doku okunamıyorsa kare sayısı bilinmez, dolu sayılır.</summary>
    private static bool IsEmpty(Texture2D texture, Rect rect)
    {
        if (!texture.isReadable) return false;
        var pixels = texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        foreach (var p in pixels) if (p.a > 0.05f) return false;
        return true;
    }

    /// <summary>Okunamayan dokuda kare sayıları (make_sheets.py çıktısı).</summary>
    private static int KnownCount(string anim)
    {
        switch (anim)
        {
            case "idle": case "walk": case "flyKick": return 6;
            case "attack": return 3;
            case "strike": return 5;
            case "hurt": return 2;
            case "die": return 10;
            default: return 6;
        }
    }

    private void Awake()
    {
        enemy = GetComponent<EnemyController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        nextAbility = Time.time + 3f;
    }

    private IEnumerator Start()
    {
        Play("idle", true);
        yield return null;
        // Boss katmanı (EnemyVisuals) görselsiz düşmanı büyütüp kızartır: ejder kendi boyunu ve rengini kullanır.
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float scale = FrameHeight / Mathf.Max(0.01f, spriteRenderer.sprite.bounds.size.y);
            transform.localScale = new Vector3(scale, scale, 1f);
            spriteRenderer.color = new Color(1f, 1f, 1f, spriteRenderer.color.a);
        }
        lastPosition = transform.position;
        lastSeenAttack = enemy.LastAttackTime;
        lastSeenHit = enemy.LastHitTime;
    }

    private void Play(string anim, bool looping)
    {
        if (currentName == anim && loop == looping) return;
        var frames = Frames(anim);
        if (frames.Length == 0) return;
        int count = Mathf.Min(frames.Length, KnownCount(anim));
        if (count < frames.Length)
        {
            var trimmed = new Sprite[count];
            System.Array.Copy(frames, trimmed, count);
            frames = trimmed;
        }
        current = frames;
        currentName = anim;
        loop = looping;
        frameClock = 0f;
    }

    private void Update()
    {
        if (enemy == null) return;
        if (enemy.IsDead)
        {
            Play("die", false);
            return;
        }
        if (busy || Time.time < nextAbility) return;
        var hero = FindFirstObjectByType<HeroController>();
        if (hero == null || hero.CurrentHealth <= 0f) return;
        if (Vector2.Distance(transform.position, hero.transform.position) > AbilityRange) return;
        var ability = NextAbility;
        rotation++;
        nextAbility = Time.time + (enemy.HealthPercent < 0.5f ? EnragedCooldown : Cooldown);
        StartCoroutine(ability == Ability.FlyKick ? FlyKick(hero) : Quake());
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || current == null || current.Length == 0) return;
        if (!enemy.IsDead && !busy && Time.time >= lockedUntil)
        {
            // Saldırı/vurulma tek seferlik, sonra yürüme ya da bekleme.
            if (enemy.LastAttackTime > lastSeenAttack)
            {
                lastSeenAttack = enemy.LastAttackTime;
                Play("attack", false);
                lockedUntil = Time.time + KnownCount("attack") / Fps;
            }
            else if (enemy.LastHitTime > lastSeenHit && enemy.LastHitTime > Time.time - 0.05f && currentName != "attack")
            {
                lastSeenHit = enemy.LastHitTime;
                Play("hurt", false);
                lockedUntil = Time.time + KnownCount("hurt") / Fps;
            }
            else
            {
                bool moving = (transform.position - lastPosition).sqrMagnitude > 0.00002f;
                Play(moving ? "walk" : "idle", true);
            }
        }
        lastSeenHit = Mathf.Max(lastSeenHit, enemy.LastHitTime);
        // Yön: ejder görseli sağa bakar; sola giderken çevrilir.
        float dx = transform.position.x - lastPosition.x;
        if (Mathf.Abs(dx) > 0.002f) facingLeft = dx < 0f;
        lastPosition = transform.position;
        spriteRenderer.flipX = facingLeft;

        frameClock += Time.deltaTime * Fps;
        int index = Mathf.FloorToInt(frameClock);
        index = loop ? index % current.Length : Mathf.Min(index, current.Length - 1);
        spriteRenderer.sprite = current[index];
    }

    // ==================== YETENEKLER ====================

    private IEnumerator FlyKick(HeroController hero)
    {
        busy = true;
        enemy.MovementLocked = true;
        Play("strike", false);
        yield return new WaitForSeconds(0.6f);
        if (enemy.IsDead || hero == null) { Finish(); yield break; }
        Vector3 landing = hero.transform.position;
        var marker = Telegraph(landing, KickRadius, new Color(1f, 0.45f, 0.1f, 0.4f));
        Play("flyKick", false);
        facingLeft = landing.x < transform.position.x;
        yield return transform.DOMove(landing, 0.45f).SetEase(Ease.InQuad).WaitForCompletion();
        if (marker != null) Destroy(marker);
        if (!enemy.IsDead)
        {
            DealArea(landing, KickRadius, KickDamageFactor, false);
            BattleVfxManager.Instance?.TryScreenShake(40f);
            TinyFx.Explosion(landing, 1.6f);
        }
        yield return new WaitForSeconds(0.3f);
        Finish();
    }

    private IEnumerator Quake()
    {
        busy = true;
        enemy.MovementLocked = true;
        Play("attack", false);
        var marker = Telegraph(transform.position, QuakeRadius, new Color(0.9f, 0.2f, 0.1f, 0.3f));
        yield return new WaitForSeconds(0.8f);
        if (marker != null) Destroy(marker);
        if (!enemy.IsDead)
        {
            DealArea(transform.position, QuakeRadius, QuakeDamageFactor, true);
            BattleVfxManager.Instance?.TryScreenShake(50f);
            BaseBuildingVisual.SpawnDust(transform.position, 60);
        }
        yield return new WaitForSeconds(0.3f);
        Finish();
    }

    private void Finish()
    {
        busy = false;
        if (enemy != null) enemy.MovementLocked = false;
        lockedUntil = 0f;
    }

    /// <summary>Alan hasarı: savaşçı ve Çırak; structures true ise kuleler de.</summary>
    private void DealArea(Vector3 center, float radius, float factor, bool structures)
    {
        float damage = enemy.Damage * factor;
        var hero = FindFirstObjectByType<HeroController>();
        if (hero != null && Vector2.Distance(hero.transform.position, center) <= radius) hero.TakeDamage(damage);
        var apprentice = FindFirstObjectByType<BattleApprentice>();
        if (apprentice != null && Vector2.Distance(apprentice.transform.position, center) <= radius)
            apprentice.TakeDamage(damage);
        if (!structures) return;
        foreach (var structure in new List<BattleStructure>(BattleStructure.All))
            if (structure != null && !structure.IsDestroyed && Vector2.Distance(structure.BasePoint, center) <= radius)
                structure.TakeDamage(damage);
    }

    private static GameObject Telegraph(Vector3 center, float radius, Color color)
    {
        var go = new GameObject("DragonTelegraph");
        go.transform.position = center;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        renderer.color = color;
        renderer.sortingOrder = 3;
        Vector2 size = renderer.sprite.bounds.size;
        go.transform.localScale = new Vector3(radius * 2f / size.x, radius * 1.2f / size.y, 1f);
        renderer.DOFade(color.a * 1.8f, 0.3f).SetLoops(-1, LoopType.Yoyo).SetLink(go);
        return go;
    }
}
