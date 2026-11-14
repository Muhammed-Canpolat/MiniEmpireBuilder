using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7. oturum (D88): bir bölgenin savaş haritası — Sefer Haritası'ndaki adanın dikey sürümü. Zemin karo seti (Tiny Swords düzeni 9×6),
/// kara maskesi (satırlar üstten alta, '#' kara, '.' su), atmosfer, süsleme seti ve bölge yapıları. Seviyeye özgü rota/yuva
/// <see cref="BattleLevelLayout"/>'ta. Varlıklar: Resources/BattleMaps/Region_N.asset (üreteç: Tools ▸ Mini Empire Builder ▸ Savaş Haritaları).
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire Builder/Battle Region Map")]
public class BattleRegionMap : ScriptableObject
{
    public int regionId;
    [Tooltip("Tiny Swords Tilemap_colorN düzeninde (576×384, 64 px karo) karo seti; bölge 4–6 için renk eşlemeli kopya (D89).")]
    public Texture2D tileset;
    [Tooltip("İsteğe bağlı hazır zemin prefab'ı (Grid + Tilemap). Boşsa zemin maskeden çalışma anında kurulur.")]
    public GameObject terrainPrefab;
    public Color waterColor = new Color32(71, 171, 169, 255);
    public Color shallowColor = new Color32(104, 196, 190, 255);
    public Color roadColor = new Color32(206, 168, 112, 255);
    public Color roadEdgeColor = new Color32(120, 86, 52, 255);
    public Color bridgeColor = new Color32(171, 120, 70, 255);
    public int columns = BattleMapGeometry.Columns;
    public int rows = BattleMapGeometry.Rows;
    [Tooltip("Kara maskesi: satırlar ÜSTTEN alta; '#' kara, diğer her şey su.")]
    public string[] mask;
    public BattleAtmosphere atmosphere;
    public BattleDecorEntry[] decor;
    public BattleLandmark[] landmarks;
    [Tooltip("Karanlık bölge: tüm sahneye bindirilen kasvet örtüsü (alfa 0 = yok).")]
    public Color gloom = new Color(0f, 0f, 0f, 0f);

    public bool IsLandCell(int col, int row)
    {
        if (mask == null || row < 0 || row >= mask.Length || col < 0) return false;
        string line = mask[row];
        return line != null && col < line.Length && line[col] == '#';
    }
}

