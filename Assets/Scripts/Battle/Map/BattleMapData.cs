using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>7. oturum (D88): bölge atmosferi (görsel; oynanış mekanikleri ayrı bileşenlerde).</summary>
public enum BattleAtmosphere { None, Ruins, Fog, Mine, Snow, Dark, Lava }

/// <summary>Bölgenin süsleme seti: rastgele (tohumlu) dağıtılan görsel öğeler. Yoldan, yuvalardan, üs/madenden uzak durur.</summary>
[Serializable]
public class BattleDecorEntry
{
    public string name;
    public Sprite[] sprites;
    public int count = 6;
    [Tooltip("Görünen yükseklik (birim) aralığı.")]
    public Vector2 height = new Vector2(1f, 1.4f);
    [Tooltip("Kahramanı durdurur mu (ağaç gövdesi, büyük kaya). Engel alanı yol bulma verisine eklenir.")]
    public bool blocksMovement;
    [Tooltip("Engel genişliği (birim; yalnız blocksMovement).")]
    public float footprint = 0.6f;
    [Tooltip("Yol kenarından en az uzaklık (birim).")]
    public float roadClearance = 0.5f;
    public bool shadow = true;
    public bool sway;
    public Color tint = Color.white;
    [Tooltip("Adanın kenar kuşağına (yoldan uzak) yığılsın mı (ağaç kümeleri).")]
    public bool preferEdges;
}

/// <summary>Bölgeye özgü büyük yapı (yıkık kale, maden girişi, siyah kale…): sabit, yürünemez, yalnız görsel.</summary>
[Serializable]
public class BattleLandmark
{
    public string name;
    public Sprite sprite;
    [Tooltip("Aday konumlar (dünya); yol/yuva/üsle çakışmayan İLK aday kullanılır.")]
    public Vector2[] candidates;
    public float height = 3f;
    public Color tint = Color.white;
    public bool blocksMovement = true;
}

/// <summary>Bir rota: kontrol noktaları (dünya). Çatallanan yolda her kol ayrı tam rotadır (başı ve sonu ortak).</summary>
[Serializable]
public class BattleRouteData
{
    public Vector2[] points;
}

/// <summary>İsteğe bağlı dalga geçersiz kılma (boşsa bölge verisindeki CampaignLevelData dalgaları kullanılır).</summary>
[Serializable]
public class BattleWaveOverride
{
    public int[] enemyCounts;
}

/// <summary>Savaş haritası ızgarası: 1 karo = 1 birim (Tiny Swords 64 PPU). Ana Üs (0,0) yolun alt ucunda.</summary>
public static class BattleMapGeometry
{
    public const int Columns = 16, Rows = 32;
    /// <summary>Izgaranın üst kenarı (dünya y). Satır r'nin merkezi = TopY − r − 0,5.</summary>
    public const float TopY = 26f;
    public const float LeftX = -Columns * 0.5f;
    /// <summary>Düşman yolu genişliği (toprak) ve koyu kenar.</summary>
    public const float RoadWidth = 1.15f, RoadEdgeWidth = 1.45f;
    /// <summary>Yuva yarıçapı (BattleBuildSlot çemberi ≈0,95) + pay.</summary>
    public const float SlotRadius = 1.0f;
    /// <summary>Düşmanın doğduğu köprü ucu (haritanın üstünde).</summary>
    public static Vector2 Entry => new Vector2(0f, TopY + 2.5f);

    public static Vector2 CellCenter(int col, int row) => new Vector2(LeftX + col + 0.5f, TopY - row - 0.5f);
    public static Vector2Int CellOf(Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x - LeftX), Mathf.FloorToInt(TopY - world.y));
    public static Rect Bounds => new Rect(LeftX, TopY - Rows, Columns, Rows);
}
