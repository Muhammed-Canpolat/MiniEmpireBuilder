using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Martı Adası'nın tek, elle tasarlanmış yerleşimi (Docs/design/island_base.md §2).
/// 1 karakter = 1 hücre = 1 dünya birimi. Izgara satır 0 üstte; dünya y'si yukarı artar
/// (<see cref="World"/> çevirir). Tüm koordinatlar "hücre" cinsinden (x = sütun, y = satır, merkez .5).
/// </summary>
public static class IslandLayout
{
    public const int Width = 14, Height = 25;

    // ~ deniz · : sığ · s kumsal · . ova · 1 yayla · 2 zirve · # yar · H merdiven · K kale · P atış yeri
    // t kule parseli · b bina parseli · o ağıl · T orman · A B C D kumsal adı (tekne suyu)
    private static readonly string[] Rows =
    {
        "~~~~~~~~~~~~~~", // r00
        "~~~~:TTTTT:~~~", // r01
        "~~~:T22222T:~~", // r02
        "~~~:2P2KKK2P:~", // r03
        "~~~:222KKK22:~", // r04
        "~~~:2b2KKK22:~", // r05
        "~~~:###HH###:~", // r06
        "~~:111111111:~", // r07
        "~~:1t1b1b1t1:~", // r08
        "~~:P1111111P:~", // r09
        "~~:111b1b111:~", // r10
        "~~:#H#####H#:~", // r11
        "~:...........:", // r12
        "As..oo..TT..sB", // r13
        "As..oo..TT..sB", // r14
        "~:...o..t.TT.:", // r15
        "~:.....P.....:", // r16
        "~:s.........s:", // r17
        "~~ss.......ss~", // r18
        "~~~C:::::::D~~", // r19
        "~~~~~~~~~~~~~~", // r20
        "~~~~~~~~~~~~~~", // r21
        "~~~~~~~~~~~~~~", // r22
        "~~~~~~~~~~~~~~", // r23
        "~~~~~~~~~~~~~~", // r24
    };

    public static char Cell(int col, int row) =>
        col < 0 || col >= Width || row < 0 || row >= Height ? '~' : Rows[row][col];

    /// <summary>Izgara (sütun, satır; merkez .5) → dünya konumu.</summary>
    public static Vector2 World(float col, float row) => new Vector2(col, Height - row);
    public static Vector2 World(Vector2 cell) => World(cell.x, cell.y);

    public static bool IsWaterChar(char c) => c == '~' || c == ':' || c == 'A' || c == 'B' || c == 'C' || c == 'D';
    public static bool IsLand(int col, int row) => !IsWaterChar(Cell(col, row));

    /// <summary>Hücrenin yüksekliği: 2 zirve (kale dahil), 1 yayla, 0 ova/kumsal. Yar ve merdiven alt seviye sayılır.</summary>
    public static int HeightAt(int col, int row)
    {
        char c = Cell(col, row);
        switch (c)
        {
            case '2': case 'K': return 2;
            case '1': case '#': case 'H': return 1;
            case 'P': case 't': case 'b': case 'T': return row <= 5 ? 2 : row <= 10 ? 1 : 0;
            default: return 0;
        }
    }

    // ==================== KALE ====================

    public static readonly Vector2 CastleCenterCell = new Vector2(8.5f, 4.5f);   // 3×3: sütun 7–9, satır 3–5
    public static Vector2 CastleCenter => World(CastleCenterCell);
    public static Vector2 CastleBottomCenter => World(8.5f, 6f);
    public const float CastleRadius = 1.6f;

    // ==================== ATIŞ YERLERİ ====================

    public struct Post
    {
        public int index;          // 0..4 → P1..P5
        public Vector2 cell;       // hücre cinsinden
        public int height;         // 0 ova, 1 yayla, 2 zirve
        public Vector2 World => IslandLayout.World(cell);
        public string Name => "P" + (index + 1);
        /// <summary>Yüksek zemin menzil bonusu (yükseklik 1–2).</summary>
        public float Range => Progression.Island.ArcherRange + (height >= 1 ? Progression.Island.HighGroundRangeBonus : 0f);
    }

    public static readonly Post[] Posts =
    {
        new Post { index = 0, cell = new Vector2(5.5f, 3.5f), height = 2 },   // P1 zirve batı
        new Post { index = 1, cell = new Vector2(11.5f, 3.5f), height = 2 },  // P2 zirve doğu
        new Post { index = 2, cell = new Vector2(3.5f, 9.5f), height = 1 },   // P3 batı yar başı
        new Post { index = 3, cell = new Vector2(11.5f, 9.5f), height = 1 },  // P4 doğu yar başı
        new Post { index = 4, cell = new Vector2(7.5f, 16.5f), height = 0 },  // P5 güney burnu
    };

    /// <summary>Bu Kıyı Kalesi seviyesinde açık mı.</summary>
    public static bool PostUnlocked(int index, int keepLevel) =>
        index >= 0 && index < Posts.Length && keepLevel >= Progression.Island.PostUnlock[index];

    // ==================== KUMSALLAR (tekne yanaşma + yürüyüş yolu) ====================

    public struct Beach
    {
        public int index;               // 0..3 → A..D
        public char name;
        public Vector2[] boatPath;      // dünya: doğuş → yanaşma noktası
        public Vector2 landing;         // dünya: tayfanın indiği nokta
        public Vector2[] walkPath;      // dünya: iniş → kale önü ara noktaları
    }

    private static Vector2[] W(params float[] xy)
    {
        var result = new Vector2[xy.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = World(xy[2 * i], xy[2 * i + 1]);
        return result;
    }

    public static readonly Beach[] Beaches =
    {
        // A batı: kıyı boyunca kuzeybatıdan iner (≈9 u)
        new Beach { index = 0, name = 'A',
            boatPath = W(-0.6f, 5f,  0.5f, 9f,  0.5f, 14f), landing = World(1.5f, 14f),
            walkPath = W(1.5f, 14f,  3.5f, 12.5f,  4.5f, 12f,  4.5f, 10.5f,  7f, 8f,  7.5f, 6.6f) },
        // B doğu
        new Beach { index = 1, name = 'B',
            boatPath = W(14.6f, 5f,  13.5f, 9f,  13.5f, 14f), landing = World(12.5f, 14f),
            walkPath = W(12.5f, 14f,  10.5f, 12.5f,  10.5f, 12f,  10.5f, 10.5f,  8.5f, 8f,  8.5f, 6.6f) },
        // C güneybatı: güney denizinden
        new Beach { index = 2, name = 'C',
            boatPath = W(-0.6f, 22f,  3.5f, 22.5f,  3.5f, 19.5f), landing = World(3.5f, 18.5f),
            walkPath = W(3.5f, 18.5f,  4f, 15.5f,  4.5f, 12f,  4.5f, 10.5f,  7f, 8f,  7.5f, 6.6f) },
        // D güneydoğu
        new Beach { index = 3, name = 'D',
            boatPath = W(14.6f, 22f,  11.5f, 22.5f,  11.5f, 19.5f), landing = World(11.5f, 18.5f),
            walkPath = W(11.5f, 18.5f,  10.8f, 15.5f,  10.5f, 12f,  10.5f, 10.5f,  8.5f, 8f,  8.5f, 6.6f) },
    };

    public static float PathLength(Vector2[] path)
    {
        float length = 0f;
        for (int i = 1; i < path.Length; i++) length += Vector2.Distance(path[i - 1], path[i]);
        return length;
    }

    // ==================== OKÇU KOŞU YOLLARI (düğüm grafiği) ====================

    // Düğümler (hücre koordinatı): 0–4 P1..P5, sonrası ara noktalar.
    private static readonly Vector2[] NodeCells =
    {
        new Vector2(5.5f, 3.5f),   // 0 P1
        new Vector2(11.5f, 3.5f),  // 1 P2
        new Vector2(3.5f, 9.5f),   // 2 P3
        new Vector2(11.5f, 9.5f),  // 3 P4
        new Vector2(7.5f, 16.5f),  // 4 P5
        new Vector2(5.5f, 2.5f),   // 5 zirve kuzey batı
        new Vector2(10.5f, 2.5f),  // 6 zirve kuzey doğu
        new Vector2(6.0f, 5.6f),   // 7 kale önü batı
        new Vector2(8.0f, 5.9f),   // 8 S1 üst
        new Vector2(10.5f, 5.6f),  // 9 kale önü doğu
        new Vector2(8.0f, 7.2f),   // 10 S1 alt
        new Vector2(4.5f, 10.5f),  // 11 S2 üst
        new Vector2(4.5f, 12.0f),  // 12 S2 alt
        new Vector2(10.5f, 10.5f), // 13 S3 üst
        new Vector2(10.5f, 12.0f), // 14 S3 alt
    };

    private static readonly int[,] Edges =
    {
        { 0, 5 }, { 5, 6 }, { 6, 1 }, { 0, 7 }, { 7, 8 }, { 1, 9 }, { 9, 8 }, { 8, 10 },
        { 10, 2 }, { 10, 3 }, { 2, 11 }, { 3, 13 }, { 10, 11 }, { 10, 13 },
        { 11, 12 }, { 13, 14 }, { 12, 4 }, { 14, 4 },
    };

    /// <summary>İki atış yeri arasındaki en kısa koşu yolu (dünya noktaları; başlangıç ve bitiş dahil).</summary>
    public static List<Vector2> PathBetween(int fromPost, int toPost)
    {
        int n = NodeCells.Length;
        var dist = new float[n];
        var prev = new int[n];
        var done = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
        dist[fromPost] = 0f;
        for (int step = 0; step < n; step++)
        {
            int u = -1;
            for (int i = 0; i < n; i++) if (!done[i] && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0 || dist[u] == float.MaxValue) break;
            done[u] = true;
            for (int e = 0; e < Edges.GetLength(0); e++)
            {
                int a = Edges[e, 0], b = Edges[e, 1];
                int v = a == u ? b : b == u ? a : -1;
                if (v < 0 || done[v]) continue;
                float d = dist[u] + Vector2.Distance(NodeCells[u], NodeCells[v]);
                if (d < dist[v]) { dist[v] = d; prev[v] = u; }
            }
        }
        var result = new List<Vector2>();
        for (int at = toPost; at >= 0; at = prev[at])
        {
            result.Add(World(NodeCells[at]));
            if (at == fromPost) break;
        }
        result.Reverse();
        return result;
    }

    public static float PathSeconds(int fromPost, int toPost)
    {
        float length = 0f;
        var path = PathBetween(fromPost, toPost);
        for (int i = 1; i < path.Count; i++) length += Vector2.Distance(path[i - 1], path[i]);
        return Mathf.Max(Progression.Island.MinPostSwitch, length / Progression.Island.ArcherRunSpeed);
    }

    // ==================== BULUT / KİLİTLİ ALANLAR ====================

    /// <summary>Kilitli alan: hücre dikdörtgeni (x0, y0 satır, x1, y1 satır; dahil) ve açan Kıyı Kalesi seviyesi.</summary>
    public struct Area { public string name; public int x0, y0, x1, y1, unlockKeep; }

    public static readonly Area[] Areas =
    {
        new Area { name = "east", x0 = 7, y0 = 12, x1 = 13, y1 = 15, unlockKeep = 2 },
        new Area { name = "southwest", x0 = 0, y0 = 16, x1 = 6, y1 = 19, unlockKeep = 3 },
        new Area { name = "southeast", x0 = 7, y0 = 16, x1 = 13, y1 = 19, unlockKeep = 5 },
    };

    public static bool PointInAreaCells(Area area, Vector2 cell) =>
        cell.x >= area.x0 && cell.x <= area.x1 + 1 && cell.y >= area.y0 && cell.y <= area.y1 + 1;
}
