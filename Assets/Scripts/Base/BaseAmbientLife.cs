using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ana Üs'te hafif ortam hareketi (yalnız görüntü): dekor koyunları otlar ve footprint içinde yavaşça dolaşır
/// (Ada koyunuyla aynı Tiny Swords kareleri), animasyonsuz ağaç/çalılar rüzgârda ±1,5° sallanır.
/// Tek merkezî Update; ekran dışındaki nesneler atlanır; yeni nesneler birkaç saniyede bir taranır.
/// Yapı nesnelerini ve hücre/doluluk verisini değiştirmez.
/// </summary>
public sealed class BaseAmbientLife : MonoBehaviour
{
    public const float WanderRadius = 0.5f;
    private const float RescanSeconds = 3f;
    private const int MaxItems = 160;

    private enum Mode { Graze, Idle, Move }

    private sealed class Sway
    {
        public Transform t; public Renderer r; public float phase, speed, amplitude, lastAngle;
    }

    private sealed class Sheep
    {
        public Transform t; public SpriteRenderer r; public Animator animator;
        public Vector3 basePos, lastPos; public Vector2 offset, target;
        public Mode mode; public float modeEnd, clipStart;
    }

    public static BaseAmbientLife Instance { get; private set; }

    private readonly List<Sway> sways = new List<Sway>();
    private readonly List<Sheep> sheep = new List<Sheep>();
    private readonly HashSet<Transform> known = new HashSet<Transform>();
    private Sprite[] grass, idle, move;
    private float nextScan;

    public int SwayCount => sways.Count;
    public int SheepCount => sheep.Count;

    private void Awake()
    {
        Instance = this;
        var catalog = IslandVisualCatalog.Instance;
        if (catalog != null)
        {
            grass = catalog.Frames("sheep_grass");
            idle = catalog.Frames("sheep_idle");
            move = catalog.Frames("sheep_move");
        }
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (Time.unscaledTime >= nextScan) Scan();
        float time = Time.time;
        // Görünürlük: kamera görüş dikdörtgeni (renderer.isVisible kamera çizim yapmadan güncellenmez; batchmode/ilk kare)
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float h = cam.orthographicSize + 1.5f, w = cam.orthographicSize * cam.aspect + 1.5f;
            Vector3 c = cam.transform.position;
            view = Rect.MinMaxRect(c.x - w, c.y - h, c.x + w, c.y + h);
        }
        else view = new Rect(-1e5f, -1e5f, 2e5f, 2e5f);
        for (int i = 0; i < sways.Count; i++)
        {
            var s = sways[i];
            if (s.t == null) { sways.RemoveAt(i--); continue; }
            if (s.r == null || !Visible(s.r)) continue;
            float angle = Mathf.Sin(time * s.speed + s.phase) * s.amplitude;
            if (Mathf.Abs(angle - s.lastAngle) < 0.02f) continue;
            s.lastAngle = angle;
            s.t.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        for (int i = 0; i < sheep.Count; i++)
        {
            var s = sheep[i];
            if (s.t == null) { sheep.RemoveAt(i--); continue; }
            if (s.r == null || !Visible(s.r)) continue;
            UpdateSheep(s, time);
        }
    }

    private Rect view;
    private bool Visible(Renderer r) { var b = r.bounds; return view.Overlaps(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y)); }

    private void Scan()
    {
        nextScan = Time.unscaledTime + RescanSeconds;
        if (sways.Count + sheep.Count >= MaxItems) return;
        foreach (var decor in FindObjectsByType<DecorInstance>(FindObjectsSortMode.None))
        {
            if (decor == null || decor.Data == null || !known.Add(decor.transform)) continue;
            var renderer = decor.GetComponent<SpriteRenderer>();
            if (renderer == null) continue;
            string id = decor.Data.id;
            if (id == "sheep") AddSheep(decor.transform, renderer);
            else if ((id == "tree" || id == "bush") && decor.GetComponent<Animator>() == null)
                AddSway(decor.transform, renderer, id == "tree" ? 1.5f : 1f);
        }
        // Çit dışı orman (ExteriorScenery, ForestMargin): ağaç/çalı sprite'ları da sallanır (kaya hariç)
        foreach (var rootName in new[] { "ExteriorScenery", "ForestMargin" })
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sways.Count + sheep.Count >= MaxItems) break;
                if (sr == null || sr.sprite == null || !known.Add(sr.transform)) continue;
                string n = sr.sprite.name.ToLowerInvariant();
                if (n.Contains("rock") || n.Contains("stone") || n.Contains("shadow")) continue;
                AddSway(sr.transform, sr, n.Contains("bush") ? 1f : 1.2f);
            }
        }
        foreach (var nature in FindObjectsByType<NatureObstacle>(FindObjectsSortMode.None))
        {
            if (nature == null || !known.Add(nature.transform)) continue;
            if (nature.kind != NatureKind.Tree && nature.kind != NatureKind.Bush) continue;
            var renderer = nature.GetComponent<SpriteRenderer>();
            if (renderer != null && nature.GetComponent<Animator>() == null)
                AddSway(nature.transform, renderer, nature.kind == NatureKind.Tree ? 1.5f : 1f);
        }
    }

    private void AddSway(Transform t, Renderer r, float amplitude)
    {
        sways.Add(new Sway { t = t, r = r, amplitude = amplitude, phase = Random.value * 6.28f,
            speed = Random.Range(0.8f, 1.4f) });
    }

    private void AddSheep(Transform t, SpriteRenderer r)
    {
        if (!IslandVisualCatalog.Has(grass) || !IslandVisualCatalog.Has(move)) return;   // kare yoksa Animator otlamaya devam eder
        var animator = t.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        var s = new Sheep { t = t, r = r, animator = animator, basePos = t.position, lastPos = t.position };
        Pick(s, Random.value < 0.5f ? Mode.Graze : Mode.Idle);
        sheep.Add(s);
    }

    private void Pick(Sheep s, Mode next)
    {
        s.mode = next;
        s.clipStart = Time.time - Random.value;
        switch (next)
        {
            case Mode.Graze: s.modeEnd = Time.time + Random.Range(3f, 7f); break;
            case Mode.Idle: s.modeEnd = Time.time + Random.Range(1f, 2.5f); break;
            default:
                s.target = Random.insideUnitCircle * WanderRadius;
                s.modeEnd = Time.time + 8f;
                break;
        }
    }

    private void UpdateSheep(Sheep s, float time)
    {
        // Dışarıdan taşındıysa (yerleştirme/sürükleme) yeni konum taban olur.
        if ((s.t.position - s.lastPos).sqrMagnitude > 1e-4f)
        {
            s.basePos = s.t.position;
            s.offset = Vector2.zero;
        }
        if (s.mode == Mode.Move)
        {
            Vector2 next = Vector2.MoveTowards(s.offset, s.target, 0.35f * Time.deltaTime);
            if (Mathf.Abs(next.x - s.offset.x) > 0.0001f) s.r.flipX = next.x < s.offset.x;
            s.offset = next;
            if (Vector2.Distance(next, s.target) < 0.04f || time > s.modeEnd)
                Pick(s, Random.value < 0.7f ? Mode.Graze : Mode.Idle);
        }
        else if (time > s.modeEnd)
        {
            Pick(s, Random.value < 0.5f ? Mode.Move : s.mode == Mode.Graze ? Mode.Idle : Mode.Graze);
        }
        s.t.position = s.basePos + new Vector3(s.offset.x, s.offset.y, 0f);
        s.lastPos = s.t.position;
        var clip = s.mode == Mode.Graze ? grass : s.mode == Mode.Move ? move : (IslandVisualCatalog.Has(idle) ? idle : grass);
        s.r.sprite = clip[(int)((time - s.clipStart) * 10f) % clip.Length];
    }
}
