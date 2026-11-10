using UnityEngine;

/// <summary>
/// Tek kareli düşman görsellerine (Kurt, Zombi, Ork, Muhafız, Gölge Kurt) kodla canlılık verir:
/// yürürken zıplayıp sallanma, beklerken nefes alma, saldırıda hedefe atılma, vurulunca geri tepme.
/// Yalnız çizilen kopya hareket eder; düşmanın konumu, ölçeği ve dövüş mantığı değişmez.
/// </summary>
public sealed class EnemyMotion : MonoBehaviour
{
    private SpriteRenderer source;
    private SpriteRenderer visual;
    private EnemyController enemy;
    private Vector3 lastPosition;
    private float phase;
    private float lunge;
    private float recoil;
    private float lastSeenAttack;
    private float lastSeenHit;
    private Vector2 lungeDirection = Vector2.left;
    private float smoothedSpeed;

    private void Start()
    {
        source = GetComponent<SpriteRenderer>();
        enemy = GetComponent<EnemyController>();
        if (source == null || enemy == null) { enabled = false; return; }
        var child = new GameObject("MotionVisual");
        child.transform.SetParent(transform, false);
        visual = child.AddComponent<SpriteRenderer>();
        source.forceRenderingOff = true;
        phase = Random.value * 10f;
        lastPosition = transform.position;
        lastSeenAttack = enemy.LastAttackTime;
        lastSeenHit = enemy.LastHitTime;
    }

    private void LateUpdate()
    {
        if (visual == null || source == null) return;
        source.forceRenderingOff = true;
        visual.sprite = source.sprite;
        visual.color = source.color;
        visual.flipX = source.flipX;
        visual.flipY = source.flipY;
        visual.sortingLayerID = source.sortingLayerID;
        visual.sortingOrder = source.sortingOrder;
        visual.enabled = source.enabled;

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        float speed = (transform.position - lastPosition).magnitude / dt;
        lastPosition = transform.position;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-dt * 10f));
        bool moving = smoothedSpeed > 0.08f && !enemy.IsDead;

        if (enemy.LastAttackTime > lastSeenAttack)
        {
            lastSeenAttack = enemy.LastAttackTime;
            lunge = 1f;
            Vector2 toTarget = enemy.AttackPoint - (Vector2)transform.position;
            lungeDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.left;
        }
        if (enemy.LastHitTime > lastSeenHit)
        {
            lastSeenHit = enemy.LastHitTime;
            recoil = 1f;
        }
        lunge = Mathf.MoveTowards(lunge, 0f, dt * 4.5f);
        recoil = Mathf.MoveTowards(recoil, 0f, dt * 6f);

        // Adım sıklığı hıza bağlı: hızlı kurt sık, yavaş zombi ağır adım atar.
        phase += dt * (moving ? Mathf.Clamp(smoothedSpeed * 5.5f, 6f, 14f) : 2.2f);
        float bob = moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.09f : Mathf.Sin(phase) * 0.015f;
        float stretch = moving ? 1f + Mathf.Sin(phase * 2f) * 0.045f : 1f + Mathf.Sin(phase) * 0.025f;
        float tilt = moving ? Mathf.Sin(phase) * 5f : 0f;

        // Atılma: önce hızla ileri, sonra geri (tek kambur eğri). Geri tepme: ezilip geri kayma.
        float lungeCurve = Mathf.Sin(lunge * Mathf.PI) * 0.28f;
        Vector2 offset = lungeDirection * lungeCurve - lungeDirection * recoil * 0.12f;
        float squash = 1f - recoil * 0.12f;

        Vector3 lossy = transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(lossy.x)), sy = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
        // Ayaklar yerde kalsın: ölçek ayak hizasından (sprite alt kenarı) uygulanır.
        float bottom = source.sprite != null ? source.sprite.bounds.min.y : 0f;
        float scaleY = stretch * squash, scaleX = (2f - stretch) * (2f - squash);
        visual.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        visual.transform.localPosition = new Vector3(offset.x / sx, (bob + offset.y) / sy + bottom * (1f - scaleY), 0f);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, source.flipX ? -tilt : tilt);
    }

    private void OnDestroy()
    {
        if (source != null) source.forceRenderingOff = false;
    }
}
