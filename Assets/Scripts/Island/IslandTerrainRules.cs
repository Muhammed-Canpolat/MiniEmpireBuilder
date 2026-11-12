using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiny Swords arazi kuralları (Docs/art/tiny_swords_island_kit.md §4 ve §7.3): yükseklik haritasından karo indeksleri.
/// Saf mantık (editör ve test paylaşır): 4 komşulu maske (iç köşe yok), 1 satırlık yar, yan merdiven (SL/SR), gölge, köpük.
/// Koordinatlar karo ızgarasıdır (x sütun, y yukarı artar); harita satırı r → y = Height-1-r.
/// Harita karakterleri: ~ su · . düz zemin (sv0) · 1 · 2 yayla/zirve üst yüzeyi · S merdiven ÜST hücresi.
/// Yar satırı yazılmaz: plato altındaki satır "arkasındaki zemin" olarak yazılır (kit §7.2).
/// </summary>
public static class IslandTerrainRules
{
    public static readonly int[] FlatByMask = { 27, 19, 24, 16, 3, 11, 0, 8, 26, 18, 25, 17, 2, 10, 1, 9 };
    public static readonly int[] ElevByMask = { 31, 23, 28, 20, 7, 15, 4, 12, 30, 22, 29, 21, 6, 14, 5, 13 };
    public static readonly int[] CliffGround = { 37, 36, 34, 35 };
    public static readonly int[] CliffWater = { 43, 42, 40, 41 };
    public const int StairLeftTop = 32, StairLeftBottom = 38, StairRightTop = 33, StairRightBottom = 39;
    /// <summary>Seviye → Tilemap_color numarası: düz 2, sv1 1, sv2 3, sv3 4 (demo ile aynı).</summary>
    public static readonly int[] LevelColor = { 2, 1, 3, 4 };

    public struct Stair
    {
        public Vector2Int top;
        public int level;     // yükselen platonun seviyesi (merdiven bu seviyenin tilemap'inde)
        public bool left;     // SL: plato merdivenin DOĞUSUNDA (rampa doğuya yükselir)
        public Vector2Int Bottom => new Vector2Int(top.x, top.y - 1);
    }

    public sealed class Result
    {
        public int width, height, maxLevel;
        public int[,] h;                                   // [x,y]: -1 su, 0.. seviye (merdiven hücresi L-1)
        public bool[,] cliff;                              // yar karosu olan hücre
        public int[,] cliffLevel;                          // yarı çizen seviye (0: yok)
        public bool[,] stairCell;                          // merdiven üst/alt hücresi
        public Dictionary<Vector2Int, int> flat = new Dictionary<Vector2Int, int>();
        public List<Dictionary<Vector2Int, int>> elev = new List<Dictionary<Vector2Int, int>>();   // [L] (0 kullanılmaz)
        public List<HashSet<Vector2Int>> shadow = new List<HashSet<Vector2Int>>();                  // [L]
        public HashSet<Vector2Int> foam = new HashSet<Vector2Int>();
        public List<Stair> stairs = new List<Stair>();
        public List<string> warnings = new List<string>();
    }

    private static readonly Vector2Int N = new Vector2Int(0, 1), E = new Vector2Int(1, 0),
        S = new Vector2Int(0, -1), W = new Vector2Int(-1, 0);

    public static Result Build(string[] rows)
    {
        var r = new Result { height = rows.Length, width = rows[0].Length };
        int width = r.width, height = r.height;
        r.h = new int[width, height];
        r.cliff = new bool[width, height];
        r.cliffLevel = new int[width, height];
        r.stairCell = new bool[width, height];
        var stairTops = new List<Vector2Int>();
        for (int row = 0; row < height; row++)
        {
            if (rows[row].Length != width) r.warnings.Add($"satır {row} uzunluğu {rows[row].Length} ≠ {width}");
            for (int x = 0; x < width && x < rows[row].Length; x++)
            {
                int y = height - 1 - row;
                char c = rows[row][x];
                switch (c)
                {
                    case '.': r.h[x, y] = 0; break;
                    case '1': r.h[x, y] = 1; break;
                    case '2': r.h[x, y] = 2; break;
                    case '3': r.h[x, y] = 3; break;
                    case 'S': r.h[x, y] = -2; stairTops.Add(new Vector2Int(x, y)); break;
                    default: r.h[x, y] = -1; break;
                }
                if (r.h[x, y] > r.maxLevel) r.maxLevel = r.h[x, y];
            }
        }

        // Merdivenler: yüksek taraftaki komşu seviyeyi ve yönü belirler; merdiven hücresi L-1 zemindir.
        foreach (var top in stairTops)
        {
            int hw = Get(r, top + W), he = Get(r, top + E);
            var stair = new Stair { top = top };
            if (hw > he) { stair.level = hw; stair.left = false; }
            else if (he > hw) { stair.level = he; stair.left = true; }
            else { r.warnings.Add($"merdiven {top}: iki yan eşit"); r.h[top.x, top.y] = 0; continue; }
            r.h[top.x, top.y] = stair.level - 1;
            r.stairs.Add(stair);
        }
        foreach (var st in r.stairs)
        {
            if (Get(r, st.Bottom) != st.level - 1) r.warnings.Add($"merdiven {st.top}: alt hücre L-1 değil");
            if (Get(r, st.top + S + S) != st.level - 1) r.warnings.Add($"merdiven {st.top}: iniş hücresi L-1 değil");
            if (Get(r, st.top + N) >= st.level) r.warnings.Add($"merdiven {st.top}: üstünde sv{st.level} üst yüzeyi var (yar üstüne biner)");
            if (Get(r, st.top + (st.left ? E : W)) != st.level) r.warnings.Add($"merdiven {st.top}: yüksek komşu sv{st.level} değil");
            r.stairCell[st.top.x, st.top.y] = true;
            r.stairCell[st.Bottom.x, st.Bottom.y] = true;
        }

        // Katman 2: düz zemin (daha yüksek seviyeler altta saklı kara sayılır).
        for (int level = 0; level <= r.maxLevel; level++)
        {
            r.elev.Add(new Dictionary<Vector2Int, int>());
            r.shadow.Add(new HashSet<Vector2Int>());
        }
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (r.h[x, y] >= 0)
                r.flat[new Vector2Int(x, y)] = FlatByMask[Mask(r, new Vector2Int(x, y), 0)];

        for (int level = 1; level <= r.maxLevel; level++)
        {
            var elev = r.elev[level];
            var stairsOfLevel = r.stairs.FindAll(s => s.level == level);
            var tops = new HashSet<Vector2Int>();
            var cliffs = new HashSet<Vector2Int>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (r.h[x, y] >= level) tops.Add(new Vector2Int(x, y));

            bool SrAt(Vector2Int cell) => stairsOfLevel.Exists(s => !s.left && s.top == cell);
            bool SlAt(Vector2Int cell) => stairsOfLevel.Exists(s => s.left && s.top == cell);
            bool BottomOf(Vector2Int cell, bool left) => stairsOfLevel.Exists(s => s.left == left && s.Bottom == cell);

            foreach (var c in tops)
            {
                int mask = Mask(r, c, level);
                if (SrAt(c + E)) mask |= 2;   // SR plato'nun doğusunda durur
                if (SlAt(c + W)) mask |= 8;   // SL batısında
                elev[c] = ElevByMask[mask];
            }
            foreach (var c in tops) if (!tops.Contains(c + S)) cliffs.Add(c + S);
            foreach (var k in cliffs)
            {
                if (k.y < 0) { r.warnings.Add($"yar harita dışında {k}"); continue; }
                bool wo = tops.Contains(k + W) || cliffs.Contains(k + W) || BottomOf(k + W, true);
                bool eo = tops.Contains(k + E) || cliffs.Contains(k + E) || BottomOf(k + E, false);
                int index = (wo ? 1 : 0) | (eo ? 2 : 0);
                int hk = r.h[k.x, k.y];
                elev[k] = hk < 0 ? CliffWater[index] : CliffGround[index];
                r.cliff[k.x, k.y] = true;
                r.cliffLevel[k.x, k.y] = level;
                if (hk >= 0 && hk < level - 1) r.warnings.Add($"2 basamaklı düşüş {k} (sv{level} yarı sv{hk} üstüne)");
            }
            foreach (var st in stairsOfLevel)
            {
                elev[st.top] = st.left ? StairLeftTop : StairRightTop;
                elev[st.Bottom] = st.left ? StairLeftBottom : StairRightBottom;
            }
            foreach (var c in tops) r.shadow[level].Add(c + S);
            foreach (var st in stairsOfLevel) r.shadow[level].Add(st.top + S);
        }

        // Köpük: düz kara ve suya bakan yar hücreleri; 8 komşusundan biri "katı" değilse.
        var solid = new HashSet<Vector2Int>(r.flat.Keys);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (r.cliff[x, y] && r.h[x, y] < 0) solid.Add(new Vector2Int(x, y));
        foreach (var c in solid)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (!solid.Contains(new Vector2Int(c.x + dx, c.y + dy))) { r.foam.Add(c); goto nextCell; }
            }
            nextCell:;
        }
        return r;
    }

    private static int Get(Result r, Vector2Int cell) =>
        cell.x < 0 || cell.y < 0 || cell.x >= r.width || cell.y >= r.height ? -1 : r.h[cell.x, cell.y];

    private static int Mask(Result r, Vector2Int c, int level) =>
        (Get(r, c + N) >= level ? 1 : 0) | (Get(r, c + E) >= level ? 2 : 0) | (Get(r, c + S) >= level ? 4 : 0) |
        (Get(r, c + W) >= level ? 8 : 0);

    /// <summary>Yürünebilir mi: kara olan ve yar karosu olmayan hücre (merdiven geçittir).</summary>
    public static bool Walkable(Result r, int x, int y) =>
        x >= 0 && y >= 0 && x < r.width && y < r.height && r.h[x, y] >= 0 && !r.cliff[x, y];
}
