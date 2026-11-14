using System.Collections;
using UnityEngine;

/// <summary>
/// 4. oturum (2.4): Ana Üs Kışlası'nın damında devriye gezen mızraklı (Tiny Swords mavi Lancer). Damın iki ucu arasında yürür (koşu
/// kareleri yavaş oynatılır), arada durup sağa sola bakar (bekleme kareleri). Ölçek ortak tablodan (<see cref="UnitScale.Base"/>).
/// Yalnız görüntü: dokunma/çakışma yok, bina yükselince/taşınınca bina nesnesiyle birlikte gider.
/// </summary>
public sealed class BarracksRoofPatrol : MonoBehaviour
{
    private const float WalkSpeed = 0.32f;
    private SpriteRenderer body;
    private SpriteRenderer lancer;
    private Sprite[] idle, run;
    private Sprite[] clip;
    private float clipStart;
    private float clipFps = 8f;

    public Transform Lancer => lancer != null ? lancer.transform : null;
    public bool Walking { get; private set; }

    private IEnumerator Start()
    {
        body = GetComponent<SpriteRenderer>();
        // Bina açılış ölçek animasyonu (0,3 sn) bitsin
        yield return new WaitForSeconds(0.45f);
        var catalog = IslandVisualCatalog.Instance;
        idle = catalog != null ? catalog.Frames("blue_lancer_idle") : null;
        run = catalog != null ? catalog.Frames("blue_lancer_run") : null;
        if (body == null || idle == null || idle.Length == 0) yield break;
        var go = new GameObject("RoofLancer");
        go.transform.SetParent(transform, false);
        float parent = Mathf.Max(0.01f, transform.lossyScale.x);
        go.transform.localScale = Vector3.one * (UnitScale.InBase(UnitScale.Kind.Lancer) / parent);
        lancer = go.AddComponent<SpriteRenderer>();
        lancer.sortingOrder = body.sortingOrder + 6;
        Play(idle, 8f);
        StartCoroutine(Patrol());
    }

    /// <summary>Dam hattı (dünya): sol/sağ uç ve ayak yüksekliği; bina görselinin üst kısmından türetilir.</summary>
    public (float left, float right, float y) RoofLine()
    {
        var b = body.bounds;
        float feetLift = 38f / 64f * UnitScale.InBase(UnitScale.Kind.Lancer);   // Lancer karesinde ayak merkezden 38 px aşağıda
        return (b.min.x + b.size.x * 0.28f, b.max.x - b.size.x * 0.28f, b.min.y + b.size.y * 0.66f + feetLift);
    }

    private IEnumerator Patrol()
    {
        var line = RoofLine();
        float x = Mathf.Lerp(line.left, line.right, 0.5f);
        SetPos(x, line.y);
        while (true)
        {
            // Dur ve etrafa bak
            Walking = false;
            Play(idle, 8f);
            int looks = Random.Range(1, 4);
            for (int i = 0; i < looks; i++)
            {
                yield return new WaitForSeconds(Random.Range(0.7f, 1.4f));
                lancer.flipX = !lancer.flipX;
            }
            yield return new WaitForSeconds(Random.Range(0.4f, 1.2f));
            // Damın öbür yanına yürü
            line = RoofLine();
            float target = x < (line.left + line.right) * 0.5f ? Random.Range(Mathf.Lerp(line.left, line.right, 0.6f), line.right)
                : Random.Range(line.left, Mathf.Lerp(line.left, line.right, 0.4f));
            lancer.flipX = target < x;
            Walking = true;
            Play(run != null && run.Length > 0 ? run : idle, 6f);
            while (Mathf.Abs(target - x) > 0.01f)
            {
                line = RoofLine();
                x = Mathf.MoveTowards(x, target, WalkSpeed * Time.deltaTime);
                SetPos(x, line.y);
                yield return null;
            }
        }
    }

    private void SetPos(float x, float y)
    {
        if (lancer != null) lancer.transform.position = new Vector3(x, y, transform.position.z - 0.02f);
    }

    private void Play(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0 || clip == frames) return;
        clip = frames;
        clipFps = fps;
        clipStart = Time.time;
    }

    private void LateUpdate()
    {
        if (lancer == null || clip == null || clip.Length == 0) return;
        lancer.sprite = clip[(int)((Time.time - clipStart) * clipFps) % clip.Length];
        if (body != null) lancer.sortingOrder = body.sortingOrder + 6;
    }
}
