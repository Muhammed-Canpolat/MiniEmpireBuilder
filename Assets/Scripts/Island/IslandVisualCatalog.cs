using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Martı Adası görselleri (Tiny Swords). Tiny Swords <c>Resources</c> altında olmadığı için editörde
/// "Tools/Island/Build Island Catalog" menüsü bu varlığı <c>Resources/IslandVisualCatalog.asset</c> olarak üretir.
/// Varlık yoksa (<see cref="Instance"/> null) ada kodla çizilen yedek görsellerle (renkli hücreler, daireler,
/// <see cref="IslandArt"/>) yine çalışır.
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire/Island Visual Catalog")]
public class IslandVisualCatalog : ScriptableObject
{
    public const int TilesetColumns = 9, TilesetRows = 6;

    [Tooltip("Tilemap_color1: 9 sütun × 6 satır = 54 yuva (satır 0 üstte); boş yuva null")]
    public Sprite[] tileset = new Sprite[TilesetColumns * TilesetRows];
    public Sprite[] foam;
    public Sprite[] clouds;
    public Sprite[] splash;

    [Header("Mavi Okçu")]
    public Sprite[] archerIdle, archerRun, archerShoot;
    [Header("Kızıl Korsanlar")]
    public Sprite[] deckhandIdle, deckhandRun, deckhandAttack;   // Kırmızı Pawn bıçak
    public Sprite[] raiderIdle, raiderRun, raiderAttack;         // Kırmızı Warrior
    public Sprite[] shoreArcherIdle, shoreArcherRun, shoreArcherShoot; // Kırmızı Archer

    [Header("v2: adlandırılmış kare dizileri (piyonlar, binalar, Lancer, Kenney tekneleri, koyun, arayüz)")]
    public List<NamedFrames> named = new List<NamedFrames>();

    [Header("Faz C: DungeonMonsters2D (PSB + 2D Animation) prefabları; yol dizgesiyle Resources.Load yok, katalog referansı")]
    public List<MonsterEntry> monsters = new List<MonsterEntry>();

    [Header("Yapılar ve simgeler")]
    public Sprite castle;
    public Sprite tower;
    public Sprite meatIcon, woodIcon;
    public Sprite[] sheepIdle;

    private static IslandVisualCatalog instance;
    private static bool loaded;

    /// <summary>Katalog (yoksa null; çağıranlar yedek görsele düşer).</summary>
    public static IslandVisualCatalog Instance
    {
        get
        {
            if (!loaded) { instance = Resources.Load<IslandVisualCatalog>("IslandVisualCatalog"); loaded = true; }
            return instance;
        }
    }

    /// <summary>Testler için: kataloğu yok say / yeniden yükle.</summary>
    public static void ResetForTests() { instance = null; loaded = false; }

    public Sprite Tile(int column, int row)
    {
        if (tileset == null || column < 0 || column >= TilesetColumns || row < 0 || row >= TilesetRows) return null;
        int index = row * TilesetColumns + column;
        return index < tileset.Length ? tileset[index] : null;
    }

    private Dictionary<string, Sprite[]> namedLookup;

    /// <summary>Adlandırılmış kare dizisi (yoksa null). Adlar BuildIslandCatalog'da tanımlıdır.</summary>
    public Sprite[] Frames(string name)
    {
        if (namedLookup == null)
        {
            namedLookup = new Dictionary<string, Sprite[]>();
            foreach (var entry in named) if (entry != null && !string.IsNullOrEmpty(entry.name)) namedLookup[entry.name] = entry.frames;
        }
        return namedLookup.TryGetValue(name, out var frames) ? frames : null;
    }

    /// <summary>DM2D canavar kaydı (anahtar = EnemyType adı; yoksa null).</summary>
    public MonsterEntry Monster(string key)
    {
        if (monsters == null) return null;
        foreach (var entry in monsters) if (entry != null && entry.key == key) return entry;
        return null;
    }

    /// <summary>Tek kareli adlandırılmış sprite (yoksa null).</summary>
    public Sprite Single(string name)
    {
        var frames = Frames(name);
        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    public static bool Has(Sprite[] frames) => frames != null && frames.Length > 0 && frames[0] != null;
}

/// <summary>
/// DM2D canavar kaydı. <see cref="bodyHeight"/> prefabın kendi ölçeğindeki gövde yüksekliğidir (editörde ölçülür);
/// çalışma anında ölçek = <see cref="targetHeight"/> / bodyHeight (Kızıl Warrior gövdesi ≈ 1,0 u; boss hariç ≤1,6×).
/// </summary>
[Serializable]
public class MonsterEntry
{
    public string key;
    public GameObject prefab;
    public float bodyHeight = 1f;
    public float targetHeight = 1f;
    [Tooltip("Prefab sağa mı bakıyor (false: sola bakar; yön localScale.x ile çevrilir)")]
    public bool facesRight = true;
    public Sprite portrait;
    [Tooltip("Prefab kökünden gövdenin en alt noktasına y farkı (prefab ölçeğinde; negatif = kök gövdenin üstünde)")]
    public float feetOffsetY;
}

[Serializable]
public class NamedFrames
{
    public string name;
    public Sprite[] frames;
}

/// <summary>Bir birimin kare kümesi (boşta / koşu / saldırı).</summary>
public sealed class IslandUnitFrames
{
    public Sprite[] idle, run, attack;
    public bool HasArt => IslandVisualCatalog.Has(idle) || IslandVisualCatalog.Has(run);
}
