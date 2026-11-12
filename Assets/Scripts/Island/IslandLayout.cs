using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Martı Adası v2 yerleşimi (Docs/design/island_base_v2.md §4): 22 × 36 hücre, 1 karakter = 1 hücre = 1 dünya birimi,
/// 3 yükseklik (düz, yayla, zirve), yan merdivenler (Tiny Swords karo kuralı, kit §4.3), deniz, 4 adacık.
/// Satır 0 üstte; dünya y'si yukarı artar (<see cref="World"/> çevirir). Arazi karakterlerinin anlamı ve yarların
/// türetilmesi için bkz. <see cref="IslandTerrainRules"/>. Hücre koordinatları (col, row; merkez .5) kullanılır.
/// Belge §4 haritasından sapma: merdivenler plato YANINA dikilir (SL/SR): S1 zirvenin doğu ucunda (c15),
/// S2/S3 yaylanın batı/doğu alt köşesinde (c3 / c18). Yarlar yazılmaz, türetilir.
/// </summary>
public static class IslandLayout
{
    public const int Width = Progression.Island.MapWidth, Height = Progression.Island.MapHeight;

    // ~ su · . düz zemin · 1 yayla · 2 zirve · S merdiven üst hücresi
    public static readonly string[] Rows =
    {
        "~~~~~~~~~~~~~~~~~~~~~~", // r00
        "~~~~~~~~~~~~~~~~~~~~~~", // r01
        "~~~~~~~~111~11~~~~~~~~", // r02  N kıyı: kesik
        "~~~~~~1122222221~~~~~~", // r03
        "~..~~112222222211~~~~~", // r04  NW adacığı
        "~..~11122222222111~~~~", // r05
        "~.~~11122222222S11~~~~", // r06  S1 (SR) c15 · zirve yarı r7/r8 kademeli
        "~~~~1112222111111~~~~~", // r07
        "~~~~~111111111111~~~~~", // r08
        "~~~~~~111111111111~~.~", // r09  E adacığı c19-20
        "~~~~~1111111111111~..~", // r10
        "~~~~~111111111111~~..~", // r11
        "~~~~11111..111111~~~~~", // r12
        "~~~S11111......111S~~~", // r13  S2 (SL) c3 · S3 (SR) c18 · yayla yarı kademeli, c9-10 oyuk
        "~~~................~~~", // r14
        "~~~................~~~", // r15
        "~~~~...............~~~", // r16
        "~~~~..............~~~~", // r17
        "~~..................~~", // r18  A kumsalı c2 · B kumsalı c19
        "~~..................~~", // r19
        "~~..................~~", // r20
        "~~~~...............~~~", // r21
        "~~~~~~~.....1111...~~~", // r22  küçük yayla c12-15 · S4 (SL) c11 (r23)
        "~~~~~~~....S1111~~~~~~", // r23
        "~~~~~~..........~~~~~~", // r24
        "~~~~~............~~~~~", // r25
        "~~~~~............~~~~~", // r26
        "~~~~~~....~......~~~~~", // r27  C kumsalı c6-9 · D kumsalı c14-16
        "~~~~~~~~~~~...~~~~~~~~", // r28
        "~..~~~~~~~~..~~~~~..~~", // r29  SW/SE adacıkları · yarımada c11-13
        "~...~~~~~~~~~~~~~~...~", // r30
        "~~~~~~~~~~~~~~~~~~~~~~", // r31
        "~~~~~~~~~~~~~~~~~~~~~~", // r32
        "~~~~~~~~~~~~~~~~~~~~~~", // r33
        "~~~~~~~~~~~~~~~~~~~~~~", // r34
        "~~~~~~~~~~~~~~~~~~~~~~", // r35
    };



    private static IslandTerrainRules.Result terrain;
    /// <summary>Kurallardan türetilmiş arazi (yar, merdiven, yükseklik); tembel hesaplanır.</summary>
    public static IslandTerrainRules.Result Terrain => terrain ?? (terrain = IslandTerrainRules.Build(Rows));

    public static char Cell(int col, int row) =>
        col < 0 || col >= Width || row < 0 || row >= Height ? '~' : Rows[row][col];

    /// <summary>Izgara (sütun, satır; merkez .5) → dünya konumu.</summary>
    public static Vector2 World(float col, float row) => new Vector2(col, Height - row);
    public static Vector2 World(Vector2 cell) => World(cell.x, cell.y);
    /// <summary>Hücre (sütun, satır) → karo koordinatı.</summary>
    public static Vector3Int TileCell(int col, int row) => new Vector3Int(col, Height - 1 - row, 0);

    public static bool IsWaterChar(char c) => c == '~';
    public static bool IsLand(int col, int row) => HeightAt(col, row) >= 0;

    /// <summary>Hücrenin yüksekliği: -1 su, 0 düz, 1 yayla, 2 zirve (merdiven hücresi alt seviye).</summary>
    public static int HeightAt(int col, int row)
    {
        if (col < 0 || col >= Width || row < 0 || row >= Height) return -1;
        return Terrain.h[col, Height - 1 - row];
    }

    public static bool IsCliff(int col, int row) =>
        col >= 0 && col < Width && row >= 0 && row < Height && Terrain.cliff[col, Height - 1 - row];
    public static bool IsStair(int col, int row) =>
        col >= 0 && col < Width && row >= 0 && row < Height && Terrain.stairCell[col, Height - 1 - row];
    public static bool Walkable(int col, int row) => IslandTerrainRules.Walkable(Terrain, col, Height - 1 - row);

    // ==================== KALE VE BİNALAR (hazır köy, §14) ====================

    public enum BuildingKind { Castle, House, Lumber, Pasture, Archery, Tower }

    /// <summary>Bina ayak izi: sol-üst hücre (col, row) ve boyut; ayak çizgisi alt kenar.</summary>
    public struct BuildingSpot
    {
        public BuildingKind kind;
        public int col, row, w, h;
        public Vector2 FeetWorld => World(col + w * 0.5f, row + h);
        public Vector2 DoorCell => new Vector2(col + w * 0.5f, row + h + 0.15f);
    }

    public static readonly BuildingSpot[] Buildings =
    {
        new BuildingSpot { kind = BuildingKind.Castle, col = 8, row = 3, w = 5, h = 3 },
        new BuildingSpot { kind = BuildingKind.House, col = 9, row = 10, w = 2, h = 2 },
        new BuildingSpot { kind = BuildingKind.Lumber, col = 6, row = 11, w = 2, h = 2 },
        new BuildingSpot { kind = BuildingKind.Archery, col = 13, row = 10, w = 3, h = 2 },
        new BuildingSpot { kind = BuildingKind.Pasture, col = 12, row = 18, w = 2, h = 2 },
        new BuildingSpot { kind = BuildingKind.Tower, col = 19, row = 10, w = 2, h = 2 },   // E adacığı (c20 r9 çıkıntı)
        new BuildingSpot { kind = BuildingKind.Tower, col = 1, row = 4, w = 2, h = 2 },     // NW adacığı
    };

    public static BuildingSpot Building(BuildingKind kind)
    {
        foreach (var spot in Buildings) if (spot.kind == kind) return spot;
        return default;
    }

    public static readonly Vector2 CastleCenterCell = new Vector2(10.5f, 4.5f);   // 5×3: sütun 8–12, satır 3–5
    public static Vector2 CastleCenter => World(CastleCenterCell);
    public static Vector2 CastleBottomCenter => World(10.5f, 6f);
    public const float CastleRadius = 2.3f;

    // ==================== DOĞA (kit §5, §8) ====================

    public static readonly Vector2Int[] Trees =
    {
        new Vector2Int(8, 2), new Vector2Int(13, 2), new Vector2Int(6, 3), new Vector2Int(7, 3), new Vector2Int(15, 3), new Vector2Int(16, 4),
        new Vector2Int(17, 5), new Vector2Int(16, 6), new Vector2Int(5, 4), new Vector2Int(4, 5), new Vector2Int(4, 6), new Vector2Int(5, 7),
        new Vector2Int(5, 8), new Vector2Int(6, 10), new Vector2Int(5, 11), new Vector2Int(4, 12), new Vector2Int(5, 12), new Vector2Int(6, 17),
        new Vector2Int(5, 19), new Vector2Int(15, 17), new Vector2Int(16, 19), new Vector2Int(13, 21), new Vector2Int(6, 21), new Vector2Int(7, 22),
        new Vector2Int(8, 23), new Vector2Int(7, 24), new Vector2Int(6, 24), new Vector2Int(5, 25), new Vector2Int(6, 26), new Vector2Int(7, 27),
        new Vector2Int(17, 22), new Vector2Int(16, 22), new Vector2Int(16, 25), new Vector2Int(15, 26), new Vector2Int(16, 26), new Vector2Int(14, 22),
        new Vector2Int(8, 21), new Vector2Int(14, 20), new Vector2Int(13, 25), new Vector2Int(11, 26), new Vector2Int(12, 16), new Vector2Int(13, 16),
        new Vector2Int(9, 22), new Vector2Int(11, 21),
    };

    public static readonly Vector2Int[] Bushes =
    {
        new Vector2Int(8, 9), new Vector2Int(12, 8), new Vector2Int(14, 8), new Vector2Int(6, 4), new Vector2Int(7, 4), new Vector2Int(14, 4),
        new Vector2Int(14, 6), new Vector2Int(15, 5), new Vector2Int(6, 15), new Vector2Int(8, 15), new Vector2Int(14, 15), new Vector2Int(17, 15),
        new Vector2Int(5, 16), new Vector2Int(16, 17), new Vector2Int(7, 18), new Vector2Int(12, 20), new Vector2Int(9, 21), new Vector2Int(15, 21),
        new Vector2Int(13, 23), new Vector2Int(10, 26), new Vector2Int(7, 27), new Vector2Int(8, 24), new Vector2Int(14, 25), new Vector2Int(10, 13),
        new Vector2Int(1, 6), new Vector2Int(20, 9), new Vector2Int(3, 30), new Vector2Int(20, 30), new Vector2Int(12, 29),
    };

    public static readonly Vector2Int[] Rocks =
    {
        new Vector2Int(9, 13), new Vector2Int(12, 11), new Vector2Int(7, 5), new Vector2Int(10, 9), new Vector2Int(6, 9), new Vector2Int(14, 16),
        new Vector2Int(4, 20), new Vector2Int(17, 20), new Vector2Int(6, 25), new Vector2Int(13, 26), new Vector2Int(8, 22), new Vector2Int(15, 25),
        new Vector2Int(7, 19), new Vector2Int(10, 25), new Vector2Int(12, 28),
    };

    /// <summary>Altın taşları: hücre ve Gold Stone numarası (1–6).</summary>
    public static readonly Vector3Int[] GoldStones =
    {
        new Vector3Int(16, 8, 4), new Vector3Int(17, 9, 1), new Vector3Int(17, 10, 2),
    };

    /// <summary>Suda kayalar (kıyıdan 1–3 hücre açıkta; tekne kanallarından uzak).</summary>
    public static readonly Vector2Int[] WaterRocks =
    {
        new Vector2Int(10, 1), new Vector2Int(1, 8), new Vector2Int(21, 3), new Vector2Int(20, 6), new Vector2Int(21, 12), new Vector2Int(4, 23),
        new Vector2Int(18, 24), new Vector2Int(20, 26), new Vector2Int(1, 24), new Vector2Int(2, 27), new Vector2Int(10, 29), new Vector2Int(7, 31),
        new Vector2Int(14, 30), new Vector2Int(4, 29), new Vector2Int(17, 31), new Vector2Int(21, 28),
    };

    public static readonly Vector2Int RubberDuck = new Vector2Int(5, 28);

    /// <summary>Koyun çayırı (hücre dikdörtgeni, dahil).</summary>
    public static readonly RectInt Meadow = new RectInt(8, 17, 4, 3);
    public const int SheepCount = 5;

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
        new Post { index = 0, cell = new Vector2(7.5f, 6.5f), height = 2 },    // P1 zirve batı
        new Post { index = 1, cell = new Vector2(13.5f, 6.5f), height = 2 },   // P2 zirve doğu
        new Post { index = 2, cell = new Vector2(5.5f, 13.5f), height = 1 },   // P3 batı yar başı
        new Post { index = 3, cell = new Vector2(16.5f, 13.5f), height = 1 },  // P4 doğu yar başı
        new Post { index = 4, cell = new Vector2(9.5f, 24.5f), height = 0 },   // P5 güney çayırı
    };

    /// <summary>Bu Kıyı Kalesi seviyesinde açık mı.</summary>
    public static bool PostUnlocked(int index, int keepLevel) =>
        index >= 0 && index < Posts.Length && keepLevel >= Progression.Island.PostUnlock[index];

    // ==================== KUMSALLAR (tekne yanaşma + yürüyüş yolu) ====================

    public struct Beach
    {
        public int index;               // 0..3 → A..D
        public char name;
        public Vector2[] boatPath;      // dünya: doğuş → yanaşma noktası (denizde)
        public Vector2 landing;         // dünya: tayfanın indiği nokta (ayak)
        public Vector2[] walkPath;      // dünya: iniş → kale önü ara noktaları (ayak)
    }

    private static Vector2[] W(params float[] xy)
    {
        var result = new Vector2[xy.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = World(xy[2 * i], xy[2 * i + 1]);
        return result;
    }

    private static Vector2[] Join(Vector2[] head, Vector2[] tail)
    {
        var result = new Vector2[head.Length + tail.Length];
        head.CopyTo(result, 0);
        tail.CopyTo(result, head.Length);
        return result;
    }

    // Ortak son kısım: yayla şeritleri → S1 (c15) → kale önü. Yayla güney yarı kademeli olduğundan şeritler c8 ve c16 boyunca kuzeye çıkar.
    private static readonly Vector2[] FromWestStairTop = W(8.5f, 13.5f, 8.5f, 9.5f, 15.5f, 9.5f, 15.5f, 7.5f, 15.5f, 6.5f, 13.5f, 6.5f, 10.5f, 6.6f);
    private static readonly Vector2[] FromEastStairTop = W(16.5f, 13.5f, 16.5f, 9.5f, 15.5f, 9.5f, 15.5f, 7.5f, 15.5f, 6.5f, 13.5f, 6.5f, 10.5f, 6.6f);

    public static readonly Beach[] Beaches =
    {
        // A batı: kuzeybatıdan batı kanalıyla iner (NW adacığının doğusundan), r19'da kumsala yanaşır
        new Beach { index = 0, name = 'A',
            boatPath = W(3.5f, -1f, 3.5f, 11.5f, 1.2f, 12.8f, 1.2f, 19.5f), landing = World(2.5f, 19.5f),
            walkPath = Join(W(2.5f, 19.5f, 4.5f, 17.5f, 3.5f, 15.5f, 3.5f, 14.5f, 3.5f, 13.5f), FromWestStairTop) },
        // B doğu: kuzeydoğudan doğu kanalıyla iner (E adacığının batısından)
        new Beach { index = 1, name = 'B',
            boatPath = W(18.5f, -1f, 18.5f, 12.8f, 20.5f, 14.2f, 20.5f, 19.5f), landing = World(19.5f, 19.5f),
            walkPath = Join(W(19.5f, 19.5f, 17.5f, 17.5f, 18.5f, 15.5f, 18.5f, 14.5f, 18.5f, 13.5f), FromEastStairTop) },
        // C güneybatı: güney denizinden kuzeye çıkar (kısa görünür süre)
        new Beach { index = 2, name = 'C',
            boatPath = W(8f, 34f, 8f, 28.6f), landing = World(8f, 27.5f),
            walkPath = Join(W(8f, 27.5f, 7.5f, 24f, 7.5f, 21f, 5.5f, 17.5f, 3.5f, 15.5f, 3.5f, 14.5f, 3.5f, 13.5f), FromWestStairTop) },
        // D güneydoğu: batı lagününü dolanır (doğu girinti r23-24 geçilmez)
        new Beach { index = 3, name = 'D',
            boatPath = W(15f, 34f, 15f, 28.6f), landing = World(15f, 27.5f),
            walkPath = Join(W(15f, 27.5f, 12f, 26f, 9.5f, 23.5f, 10.5f, 20.5f, 14.5f, 20.5f, 15.5f, 17.5f, 18.5f, 15.5f, 18.5f, 14.5f, 18.5f, 13.5f), FromEastStairTop) },   // Faz B: Ağıl (12-13 × 18-19) şeritte kalmasın diye r20 boyunca
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
        new Vector2(7.5f, 6.5f),   // 0 P1
        new Vector2(13.5f, 6.5f),  // 1 P2
        new Vector2(5.5f, 13.5f),  // 2 P3
        new Vector2(16.5f, 13.5f), // 3 P4
        new Vector2(9.5f, 24.5f),  // 4 P5
        new Vector2(10.5f, 6.8f),  // 5 kale önü
        new Vector2(15.5f, 6.5f),  // 6 S1 üst
        new Vector2(15.5f, 7.5f),  // 7 S1 alt
        new Vector2(15.5f, 9.5f),  // 8 yayla kavşağı doğu
        new Vector2(8.5f, 9.5f),   // 9 yayla kavşağı batı
        new Vector2(8.5f, 13.5f),  // 10 yayla güney şeridi (batı)
        new Vector2(3.5f, 13.5f),  // 11 S2 üst
        new Vector2(3.5f, 14.5f),  // 12 S2 alt
        new Vector2(18.5f, 13.5f), // 13 S3 üst
        new Vector2(18.5f, 14.5f), // 14 S3 alt
        new Vector2(3.5f, 15.5f),  // 15 S2 inişi
        new Vector2(18.5f, 15.5f), // 16 S3 inişi
        new Vector2(13.5f, 19.5f), // 17 ova ara noktası (küçük yaylayı dolanır)
    };

    private static readonly int[,] Edges =
    {
        { 0, 5 }, { 5, 1 }, { 1, 6 }, { 6, 7 }, { 7, 8 }, { 8, 9 }, { 9, 10 }, { 10, 2 }, { 8, 3 },
        { 2, 11 }, { 11, 12 }, { 12, 15 }, { 3, 13 }, { 13, 14 }, { 14, 16 }, { 15, 4 }, { 16, 17 }, { 17, 4 },
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

    // ==================== BÖLGELER (bulut altında başlar; §4 tablosu) ====================

    /// <summary>Kilitli bölge: ad ve açan Kıyı Kalesi seviyesi (Z0 baştan açık).</summary>
    public struct Zone { public string name; public int unlockKeep; }

    public static readonly Zone[] Zones =
    {
        new Zone { name = "Kale Yaylası / Castle Plateau", unlockKeep = 1 },
        new Zone { name = "Doğu Kıyısı / East Shore", unlockKeep = 2 },
        new Zone { name = "Güneybatı Koyu / Southwest Cove", unlockKeep = 3 },
        new Zone { name = "Altın Burnu / Gold Point", unlockKeep = 4 },
        new Zone { name = "Güneydoğu Koyu / Southeast Cove", unlockKeep = 5 },
        new Zone { name = "Orta Çayır / Middle Meadow", unlockKeep = 6 },
    };

    /// <summary>Orta çayırın bölünme sütunları: c ≤ batı → Z2, c ≥ doğu → Z1 (r15–22).</summary>
    public const int MeadowSplitWest = 8, MeadowSplitEast = 14;

    /// <summary>Hücrenin bölgesi (0..5); belge §4 tablosu. Adacıklar ilgili bölgeye bağlıdır.</summary>
    public static int ZoneAt(int c, int r)
    {
        if (c >= 1 && c <= 2 && r >= 4 && r <= 5) return 3;        // NW adacığı → Z3
        if (c >= 19 && c <= 20 && r >= 10 && r <= 11) return 1;     // E adacığı → Z1
        if (c >= 1 && c <= 2 && r >= 29 && r <= 30) return 2;       // SW adacığı → Z2
        if (c >= 18 && c <= 19 && r >= 29 && r <= 30) return 4;     // SE adacığı → Z4
        if (c >= 15 && c <= 17 && r >= 2 && r <= 8) return 3;
        // Hazır köy (Kale, evler, Okçuluk, Ağıl) Z0'da kalır: açılışta bulut yalnız kenar kuşağını örter.
        // Orta çayır (r15–22) bölünür: batısı Z2'ye, doğusu Z1'e (12 Kasım 2026 kararı: Z1/Z2 yalnız şerit/yardan ibaretti,
        // birer ev sığıyordu). Ağıl (c12–13) ve çayır ortası Z0'da kalır.
        if (r >= 15 && r <= 22 && c <= MeadowSplitWest) return 2;
        if (r >= 15 && r <= 22 && c >= MeadowSplitEast) return 1;
        if (r <= 22) return c >= 16 ? 1 : 0;
        if (c >= 9 && c <= 12) return 5;
        return c <= 8 ? 2 : 4;
    }

    public static bool ZoneUnlocked(int zone, int keepLevel) =>
        zone <= 0 || (zone < Zones.Length && keepLevel >= Zones[zone].unlockKeep);

    /// <summary>Hücre kilitli bir bölgede mi (bu Kıyı Kalesi seviyesinde).</summary>
    public static bool IsLocked(int col, int row, int keepLevel) => !ZoneUnlocked(ZoneAt(col, row), keepLevel);
}
