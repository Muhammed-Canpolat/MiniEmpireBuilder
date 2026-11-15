#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 8. oturum (D100): geliştirici ayarı "Occupancy göster" (yalnız Editor / Development build; DevMenu'den). Ana Üs'te dolu her hücre türüne göre
/// renklenir (yapı kırmızı, kamp turuncu, dekor mavi, doğa yeşil) ve hücreleri dolduran nesnenin adı alanın ortasına yazılır. Veri doğrudan
/// <see cref="BaseOccupancyGrid.Occupants"/> anlık görüntüsünden okunur: ekranda görülen, yerleşim kuralının gördüğüyle birebir aynıdır.
/// </summary>
public sealed class OccupancyDebugView : MonoBehaviour
{
    private const string PrefKey = "MEB_OccupancyDebug";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Sync();
        }
    }

    private static OccupancyDebugView instance;
    private readonly List<GameObject> pool = new List<GameObject>();
    private readonly List<TextMeshPro> labels = new List<TextMeshPro>();
    private int shownRebuild = -1;
    private float nextRefresh;
    private Sprite square;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded += (_, __) => Sync();
        Sync();
    }

    /// <summary>Ayar açıksa ve Ana Üs sahnesindeysek görünümü kurar; değilse kaldırır.</summary>
    public static void Sync()
    {
        bool want = Enabled && BaseMapManager.Instance != null;
        if (want && instance == null) instance = new GameObject("OccupancyDebugView").AddComponent<OccupancyDebugView>();
        else if (!want && instance != null) Destroy(instance.gameObject);
    }

    private void Update()
    {
        if (BaseMapManager.Instance == null) { Destroy(gameObject); return; }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        var occupants = BaseOccupancyGrid.Occupants;
        if (shownRebuild == BaseOccupancyGrid.RebuildCount && pool.Count > 0) return;
        shownRebuild = BaseOccupancyGrid.RebuildCount;
        Draw(occupants);
    }

    private static Color ColorOf(string kind) => kind == "structure" ? new Color(0.95f, 0.2f, 0.15f, 0.38f)
        : kind == "camp" ? new Color(1f, 0.6f, 0.1f, 0.4f)
        : kind == "decor" ? new Color(0.25f, 0.55f, 1f, 0.42f)
        : new Color(0.25f, 0.85f, 0.3f, 0.45f);

    private void Draw(IReadOnlyList<BaseOccupancyGrid.Occupant> occupants)
    {
        if (square == null) square = BaseMapManager.MakeSquareSprite(Color.white);
        int tile = 0, label = 0;
        foreach (var occ in occupants)
        {
            var c = occ.cells;
            for (int y = c.yMin; y < c.yMax; y++)
                for (int x = c.xMin; x < c.xMax; x++)
                {
                    var go = Tile(tile++);
                    go.transform.position = new Vector3(x + 0.5f, y + 0.5f, -2f);
                    go.GetComponent<SpriteRenderer>().color = ColorOf(occ.kind);
                }
            var text = Label(label++);
            text.transform.position = new Vector3(c.center.x, c.center.y, -2.1f);
            text.text = occ.name;
            text.fontSize = occ.kind == "structure" ? 2.2f : 1.5f;
        }
        for (int i = tile; i < pool.Count; i++) pool[i].SetActive(false);
        for (int i = label; i < labels.Count; i++) labels[i].gameObject.SetActive(false);
    }

    private GameObject Tile(int i)
    {
        if (i < pool.Count) { pool[i].SetActive(true); return pool[i]; }
        var go = new GameObject("OccupiedCell");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.sortingOrder = 300;
        go.transform.localScale = new Vector3(0.9f / square.bounds.size.x, 0.9f / square.bounds.size.y, 1f);
        pool.Add(go);
        return go;
    }

    private TextMeshPro Label(int i)
    {
        if (i < labels.Count) { labels[i].gameObject.SetActive(true); return labels[i]; }
        var go = new GameObject("OccupantName");
        go.transform.SetParent(transform, false);
        var t = go.AddComponent<TextMeshPro>();
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.outlineWidth = 0.25f;
        t.outlineColor = Color.black;
        t.enableWordWrapping = true;
        t.rectTransform.sizeDelta = new Vector2(4f, 2f);
        t.sortingOrder = 301;
        labels.Add(t);
        return t;
    }
}
#endif
