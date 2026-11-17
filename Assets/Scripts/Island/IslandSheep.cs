using System.Collections.Generic;
using UnityEngine;

/// <summary>Koyun: çayırda otlar, ara sıra kısa yürür (yalnız görüntü). Tiny Swords Sheep_Grass/Idle/Move kareleri.</summary>
public sealed class IslandSheep : MonoBehaviour
{
    public static readonly List<IslandSheep> All = new List<IslandSheep>();

    private enum Mode { Graze, Idle, Move }
    private SpriteRenderer sr;
    private Sprite[] grass, idle, move;
    private Rect meadow;
    private Mode mode = Mode.Graze;
    private float modeEnd, clipStart;
    private Vector2 target;
    private int height;
    private const float WalkSpeed = 0.45f;

    public Vector2 Feet => transform.position;

    public static IslandSheep Create(Transform parent, Rect meadowWorld, Vector2 feet, Sprite[] grassFrames,
        Sprite[] idleFrames, Sprite[] moveFrames, float footPixels)
    {
        var go = new GameObject("Sheep");
        go.transform.SetParent(parent, false);
        go.transform.position = feet;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, footPixels / 64f, 0f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        var sheep = go.AddComponent<IslandSheep>();
        sheep.sr = renderer;
        sheep.grass = grassFrames; sheep.idle = idleFrames; sheep.move = moveFrames;
        sheep.meadow = meadowWorld;
        sheep.height = HeightOf(feet);
        var sort = go.AddComponent<IslandYSort>();
        sort.bias = 0;
        sheep.Pick(Mode.Graze);
        return sheep;
    }

    /// <summary>Hücre yürünebilir mi ve verilen katta mı (Ağıl ile aynı yükseklik).</summary>
    public static bool SpotOk(Vector2 world, int height)
    {
        var cell = IslandWalkGrid.Shared.CellOf(world);
        return IslandWalkGrid.Shared.Walkable(cell) && IslandLayout.HeightAt(cell.x, cell.y) == height;
    }

    public static int HeightOf(Vector2 world)
    {
        var cell = IslandWalkGrid.Shared.CellOf(world);
        return IslandLayout.HeightAt(cell.x, cell.y);
    }

    private bool PathOk(Vector2 from, Vector2 to)
    {
        if (!GridPathfinder.LineClear(IslandWalkGrid.Shared, from, to)) return false;
        float length = Vector2.Distance(from, to);
        int steps = Mathf.CeilToInt(length / 0.4f);
        for (int i = 1; i <= steps; i++)
            if (!SpotOk(Vector2.Lerp(from, to, i / (float)steps), height)) return false;
        return true;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Pick(Mode next)
    {
        mode = next;
        clipStart = Time.time - Random.value;
        switch (next)
        {
            case Mode.Graze: modeEnd = Time.time + Random.Range(2.5f, 6f); break;
            case Mode.Idle: modeEnd = Time.time + Random.Range(1f, 2.5f); break;
            default:
                // 4. oturum (1.1) + bug 23: koyun bina/su/ağaç içine yürümez, kat değiştirmez; uygun hedef bulunamazsa yerinde kalır.
                target = transform.position;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    var candidate = new Vector2(Random.Range(meadow.xMin, meadow.xMax), Random.Range(meadow.yMin, meadow.yMax));
                    if (PathOk(transform.position, candidate)) { target = candidate; break; }
                }
                modeEnd = Time.time + 8f;
                break;
        }
    }

    private void Update()
    {
        if (mode == Mode.Move)
        {
            Vector2 position = transform.position;
            Vector2 next = Vector2.MoveTowards(position, target, WalkSpeed * Time.deltaTime);
            if (Mathf.Abs(next.x - position.x) > 0.0001f) sr.flipX = next.x < position.x;
            transform.position = new Vector3(next.x, next.y, 0f);
            if (Vector2.Distance(next, target) < 0.05f || Time.time > modeEnd) Pick(Random.value < 0.7f ? Mode.Graze : Mode.Idle);
        }
        else if (Time.time > modeEnd)
        {
            Pick(Random.value < 0.55f ? Mode.Move : mode == Mode.Graze ? Mode.Idle : Mode.Graze);
        }
        var clip = mode == Mode.Graze ? grass : mode == Mode.Move ? move : idle;
        if (clip != null && clip.Length > 0)
            sr.sprite = clip[(int)((Time.time - clipStart) * 10f) % clip.Length];
    }

    /// <summary>Kırkım için piyonun yanına en yakın koyun.</summary>
    public static IslandSheep Nearest(Vector2 point)
    {
        IslandSheep best = null;
        float bestDistance = float.MaxValue;
        foreach (var sheep in All)
        {
            float d = Vector2.Distance(sheep.Feet, point);
            if (d < bestDistance) { bestDistance = d; best = sheep; }
        }
        return best;
    }
}
