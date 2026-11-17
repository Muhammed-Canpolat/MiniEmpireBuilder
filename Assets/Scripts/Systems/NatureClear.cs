using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (4.2): Ana Üs ve adada ORTAK süreli temizleme (kütük, çalı, kaya, ağaç).
/// - Süre öğenin büyüklüğüne göre <see cref="FeatureConfig"/>'ten: küçük çalı 3 sn, kaya 5 sn, büyük ağaç 10 sn (kütük 4 sn).
/// - İş sıraya girer; işçi (Ana Üs: inşaatçı, ada: Ada Ustası) boşalınca başlar ("SIRADA"). İşçi yürür ve keser/kırar.
/// - Öğenin üstünde ilerleme halkası; bitince toz efekti + ödül yazısı ("+12 Odun").
/// Ana Üs işleri inşaatçı görevi olduğu için oyun kapalıyken de ilerler (GameManager tamamlayınca kayda yazar).
/// </summary>
public static class NatureClear
{
    public enum Size { Bush, Stump, Rock, Tree }

    public static float SecondsFor(Size size)
    {
        var c = FeatureConfig.Instance;
        switch (size)
        {
            case Size.Bush: return c.clearSecondsBush;
            case Size.Stump: return c.clearSecondsStump;
            case Size.Rock: return c.clearSecondsRock;
            default: return c.clearSecondsTree;
        }
    }

    public static Size SizeOf(NatureKind kind) =>
        kind == NatureKind.Bush ? Size.Bush : kind == NatureKind.Stump ? Size.Stump : kind == NatureKind.Rock ? Size.Rock : Size.Tree;

    public static string TimeText(Size size) => L.T($"{SecondsFor(size):0} sn", $"{SecondsFor(size):0} s");

    // ==================== sıra ====================

    public sealed class Job
    {
        public string key;
        public Transform target;
        public Size size;
        /// <summary>İşçi uygunsa işi başlatır (bedel burada ödenir); true → başladı, false → beklemeye devam. null → iptal (yetersiz vb.).</summary>
        public Func<bool?> tryStart;
        /// <summary>Başladıktan sonra ilerleme 0..1 (Ana Üs: inşaatçı görevi; ada: gerçek zaman).</summary>
        public Func<float> progress;
        public bool started;
        public ClearRing ring;
        /// <summary>Gerçek zamanlı işlerde (ada) ilerleme 1'e varınca çağrılır; Ana Üs işleri inşaatçı görevi bitince tamamlanır.</summary>
        public Action onComplete;
    }

    private static readonly List<Job> queue = new List<Job>();
    public static IReadOnlyList<Job> Queue => queue;
    public static bool IsQueued(string key) => queue.Exists(j => j.key == key);

    public static Job Enqueue(string key, Transform target, Size size, Func<bool?> tryStart, Func<float> progress)
    {
        if (IsQueued(key)) return null;
        var job = new Job { key = key, target = target, size = size, tryStart = tryStart, progress = progress };
        job.ring = ClearRing.Attach(target, job);
        queue.Add(job);
        Runner.Ensure();
        Pump();
        return job;
    }

    /// <summary>Sırayı ilerletir: başlamamış ilk işi dener (aynı anda işçi sayısı kadar iş sürer; dener, başlamazsa bekler).</summary>
    public static void Pump()
    {
        for (int i = 0; i < queue.Count; i++)
        {
            var job = queue[i];
            if (job.target == null) { Drop(job); i--; continue; }
            if (job.started)
            {
                if (job.onComplete != null && job.progress != null && job.progress() >= 1f) { var done = job.onComplete; job.onComplete = null; done(); }
                continue;
            }
            bool? result = job.tryStart();
            if (result == null) { Drop(job); i--; continue; }
            if (result == false) break;   // sıra: öndeki başlamadan arkadakiler başlamaz
            job.started = true;
        }
    }

    public static void Finish(string key, Vector3 at, string reward)
    {
        var job = queue.Find(j => j.key == key);
        if (job != null) Drop(job);
        BaseBuildingVisual.SpawnDust(at, 140);
        if (!string.IsNullOrEmpty(reward)) FloatingReward.Spawn(at + Vector3.up * 0.8f, reward);
        // 10. oturum (D110): "coin" anahtarının sesi yoktu. Ağaç/kütük = odun kesme, kaya = maden; çalı için uygun ses yok (sessiz).
        bool wood = key.StartsWith("tree", System.StringComparison.OrdinalIgnoreCase) || key.StartsWith("stump", System.StringComparison.OrdinalIgnoreCase);
        bool rock = key.StartsWith("rock", System.StringComparison.OrdinalIgnoreCase);
        if (wood) AudioManager.Instance?.PlaySfx("chop");
        else if (rock) AudioManager.Instance?.PlaySfx("goldCollect");
    }

    private static void Drop(Job job)
    {
        queue.Remove(job);
        if (job.ring != null) UnityEngine.Object.Destroy(job.ring.gameObject);
    }

    public static void ClearAll() { foreach (var j in queue.ToArray()) Drop(j); }

    private sealed class Runner : MonoBehaviour
    {
        private static Runner instance;
        private float next;
        public static void Ensure()
        {
            if (instance != null) return;
            instance = new GameObject("NatureClearRunner").AddComponent<Runner>();
        }
        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.25f;
            Pump();
        }
        private void OnDestroy() { if (instance == this) instance = null; queue.Clear(); }
    }
}

/// <summary>Temizlenen öğenin üstünde ilerleme halkası (dünya tuvali, radyal dolgu); sırada beklerken gri "SIRADA".</summary>
public sealed class ClearRing : MonoBehaviour
{
    private NatureClear.Job job;
    private Transform target;
    private Image fill;
    private TextMeshProUGUI label;
    public float Shown => fill != null ? fill.fillAmount : 0f;

    public static ClearRing Attach(Transform target, NatureClear.Job job)
    {
        var go = new GameObject("ClearRing", typeof(RectTransform));
        var ring = go.AddComponent<ClearRing>();
        ring.job = job;
        ring.target = target;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 950;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(64, 64);
        rt.localScale = Vector3.one * 0.018f;
        var bg = ReferenceUiVisuals.Image(rt, "Bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.08f, 0.06f, 0.04f, 0.8f));
        bg.sprite = ReferenceUiVisuals.SolidPillSprite(); bg.type = Image.Type.Sliced;
        ring.fill = ReferenceUiVisuals.Image(rt, "Fill", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4), ReferenceUiVisuals.Gold);
        ring.fill.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        ring.fill.type = Image.Type.Filled;
        ring.fill.fillMethod = Image.FillMethod.Radial360;
        ring.fill.fillOrigin = (int)Image.Origin360.Top;
        ring.fill.fillClockwise = true;
        ring.fill.fillAmount = 0f;
        var hole = ReferenceUiVisuals.Image(rt, "Hole", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero, new Color(0.08f, 0.06f, 0.04f, 0.95f));
        hole.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        ring.label = ReferenceUiVisuals.Text(rt, "Label", "", 13, Color.white, TextAlignmentOptions.Center,
            new Vector2(-0.6f, -0.5f), new Vector2(1.6f, -0.05f), Vector2.zero, Vector2.zero, true);
        ring.label.textWrappingMode = TextWrappingModes.NoWrap;
        ring.Follow();
        return ring;
    }

    private void Follow()
    {
        if (target == null) return;
        var sr = target.GetComponentInChildren<SpriteRenderer>();
        float top = sr != null ? sr.bounds.max.y : target.position.y + 1f;
        transform.position = new Vector3(target.position.x, Mathf.Min(top + 0.45f, target.position.y + 3f), 0f);
    }

    private void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }
        Follow();
        if (job == null) return;
        if (!job.started)
        {
            fill.fillAmount = 1f; fill.color = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            label.text = L.T("SIRADA", "QUEUED");
            return;
        }
        float p = Mathf.Clamp01(job.progress != null ? job.progress() : 0f);
        fill.color = ReferenceUiVisuals.Gold;
        fill.fillAmount = p;
        label.text = $"{Mathf.CeilToInt(NatureClear.SecondsFor(job.size) * (1f - p))}";
    }
}

/// <summary>Yükselip sönen ödül yazısı ("+12 Odun").</summary>
public sealed class FloatingReward : MonoBehaviour
{
    private float t;
    private TextMeshPro text;
    public static void Spawn(Vector3 at, string message)
    {
        var go = new GameObject("FloatingReward");
        go.transform.position = at;
        var f = go.AddComponent<FloatingReward>();
        f.text = go.AddComponent<TextMeshPro>();
        f.text.text = message;
        f.text.fontSize = 5f;
        f.text.alignment = TextAlignmentOptions.Center;
        f.text.color = new Color(1f, 0.9f, 0.45f);
        f.text.outlineWidth = 0.25f;
        f.text.outlineColor = new Color32(40, 20, 6, 255);
        f.text.sortingOrder = 960;
    }
    private void Update()
    {
        t += Time.unscaledDeltaTime;
        transform.position += Vector3.up * Time.unscaledDeltaTime * 0.9f;
        if (text != null) text.alpha = Mathf.Clamp01(1.6f - t);
        if (t > 1.6f) Destroy(gameObject);
    }
}
