using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mavi piyon (Tiny Swords): yalnız görsel iş döngüleri (island_base_v2 §5). Oduncu ağaca koşar, 3 vuruş keser, kereste taşır;
/// çoban koyundan kırkım alıp et taşır; madenci altın taşını kazar ve kaleye taşır; usta çekiçle çalışır; boştakiler dolaşır.
/// Üretim UTC tick ile hesaplanır (Faz B); bu bileşen maliyetsizdir. Ayak çizgisi kökte, sprite 39 px yukarıdadır.
/// </summary>
public sealed class IslandPawn : MonoBehaviour
{
    public enum Job { Lumberjack, Shepherd, Miner, Builder, Idler }

    public static readonly List<IslandPawn> All = new List<IslandPawn>();
    private const float FeetPixels = 39f;

    public Job Kind { get; private set; }
    public string CurrentClip { get; private set; } = "";

    private IslandVisualCatalog catalog;
    private SpriteRenderer sr;
    private Vector2 home;
    private Vector2[] carryRoute;      // madenci: taş → kale yolu (ayak, dünya)
    private Vector2 stonePoint;
    private Sprite[] clip;
    private float clipStart;
    private int treeVisits;
    private bool started;

    public static IslandPawn Create(Transform parent, IslandVisualCatalog catalog, Job job, Vector2 homeFeet,
        Vector2[] carryRoute = null)
    {
        var go = new GameObject("Pawn_" + job);
        go.transform.SetParent(parent, false);
        go.transform.position = homeFeet;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, FeetPixels / 64f, 0f);
        var pawn = go.AddComponent<IslandPawn>();
        pawn.sr = visual.AddComponent<SpriteRenderer>();
        pawn.catalog = catalog;
        pawn.Kind = job;
        pawn.home = homeFeet;
        pawn.carryRoute = carryRoute;
        go.AddComponent<IslandYSort>().bias = 1;
        pawn.Play("pawn_idle");
        return pawn;
    }

    private void OnEnable()
    {
        All.Add(this);
        // Savaştan dönüşte (nesne yeniden açılınca) döngü baştan başlar.
        if (started) { transform.position = home; StartCoroutine(Loop()); }
    }

    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        started = true;
        StartCoroutine(Loop());
    }

    private void LateUpdate()
    {
        if (clip == null || clip.Length == 0 || sr == null) return;
        sr.sprite = clip[(int)((Time.time - clipStart) * 10f) % clip.Length];
    }

    private void Play(string name)
    {
        var frames = catalog != null ? catalog.Frames(name) : null;
        if (frames == null || frames.Length == 0) return;
        if (clip == frames) return;
        clip = frames;
        CurrentClip = name;
        clipStart = Time.time;
    }

    private void Face(float dx)
    {
        if (Mathf.Abs(dx) > 0.01f && sr != null) sr.flipX = dx < 0f;
    }

    private IEnumerator Walk(Vector2 target, string runClip)
    {
        Play(runClip);
        while (Vector2.Distance(transform.position, target) > 0.04f)
        {
            Vector2 position = transform.position;
            Vector2 next = Vector2.MoveTowards(position, target, Progression.Island.PawnRunSpeed * Time.deltaTime);
            Face(next.x - position.x);
            transform.position = new Vector3(next.x, next.y, 0f);
            yield return null;
        }
    }

    private IEnumerator Work(string clipName, float seconds, float faceDx)
    {
        Face(faceDx);
        Play(clipName);
        yield return new WaitForSeconds(seconds);
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.value * 1.5f);
        switch (Kind)
        {
            case Job.Lumberjack: yield return LumberLoop(); break;
            case Job.Shepherd: yield return ShepherdLoop(); break;
            case Job.Miner: yield return MinerLoop(); break;
            case Job.Builder: yield return BuilderLoop(); break;
            default: yield return IdleLoop(); break;
        }
    }

    private IEnumerator LumberLoop()
    {
        while (true)
        {
            Play("pawn_idle");
            yield return new WaitForSeconds(Random.Range(0.6f, 1.4f));
            IslandTree tree = PickTree();
            if (tree == null) { yield return new WaitForSeconds(2f); continue; }
            bool fromLeft = tree.Feet.x >= home.x;
            Vector2 spot = tree.Feet + new Vector2(fromLeft ? -0.65f : 0.65f, -0.05f);
            yield return Walk(spot, "pawn_run_axe");
            for (int hit = 0; hit < (int)Progression.Island.ChopsPerTree; hit++)
                yield return Work("pawn_interact_axe", 0.6f, tree.Feet.x - transform.position.x);
            treeVisits++;
            if (treeVisits % (int)Progression.Island.TreeStumpEveryVisit == 0) tree.Chop();
            yield return Walk(home, "pawn_run_wood");
            yield return Work("pawn_idle_wood", 0.8f, 0f);
        }
    }

    private IslandTree PickTree()
    {
        var candidates = new List<IslandTree>();
        foreach (var tree in IslandTree.All)
            if (!tree.Chopped && Vector2.Distance(tree.Feet, home) < 7f) candidates.Add(tree);
        if (candidates.Count == 0) return null;
        candidates.Sort((a, b) => Vector2.Distance(a.Feet, home).CompareTo(Vector2.Distance(b.Feet, home)));
        return candidates[Random.Range(0, Mathf.Min(3, candidates.Count))];
    }

    private IEnumerator ShepherdLoop()
    {
        while (true)
        {
            Play("pawn_idle");
            yield return new WaitForSeconds(Random.Range(1f, 2.2f));
            var sheep = IslandSheep.Nearest(home);
            if (sheep == null) { yield return new WaitForSeconds(2f); continue; }
            Vector2 spot = sheep.Feet + new Vector2(-0.75f, -0.05f);
            yield return Walk(spot, "pawn_run_knife");
            yield return Work("pawn_interact_knife", 1.2f, sheep.Feet.x - transform.position.x);
            yield return Walk(home, "pawn_run_meat");
            yield return Work("pawn_idle_meat", 0.8f, 0f);
        }
    }

    private IEnumerator MinerLoop()
    {
        while (true)
        {
            Play("pawn_idle_pickaxe");
            yield return new WaitForSeconds(0.4f);
            for (int hit = 0; hit < 3; hit++) yield return Work("pawn_interact_pickaxe", 0.6f, 1f);
            if (carryRoute != null)
            {
                foreach (var point in carryRoute) yield return Walk(point, "pawn_run_gold");
                yield return Work("pawn_idle_gold", 0.8f, 0f);
                for (int i = carryRoute.Length - 2; i >= 0; i--) yield return Walk(carryRoute[i], "pawn_run_pickaxe");
                yield return Walk(home, "pawn_run_pickaxe");
            }
        }
    }

    private IEnumerator BuilderLoop()
    {
        while (true)
        {
            yield return Work("pawn_interact_hammer", Random.Range(2.4f, 3.6f), 1f);
            yield return Work("pawn_idle_hammer", Random.Range(0.8f, 1.5f), 0f);
        }
    }

    private IEnumerator IdleLoop()
    {
        while (true)
        {
            Play("pawn_idle");
            yield return new WaitForSeconds(Random.Range(2f, 4.5f));
            Vector2 target = home + new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(-0.2f, 0.3f));
            yield return Walk(target, "pawn_run");
        }
    }
}
