using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 7. oturum (D88): sefer seviyesinin yerleşimi — rota(lar), kule yuvaları, Ana Üs / maden / kahraman konumları, seviye türü, dalgalar.
/// Seçiliyken Scene görünümünde tutamaçlarla düzenlenir (Editor/BattleLevelLayoutEditor). Doğrulama: BattleLayoutGenerator.Validate.
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire Builder/Battle Level Layout")]
public class BattleLevelLayout : ScriptableObject
{
    public int regionId;
    public int levelNumber;
    public CampaignLevelKind kind;
    [Tooltip("Rotalar; [0] ana yol. Çatallı seviyede iki kol (düşmanlar sırayla seçer).")]
    public List<BattleRouteData> routes = new List<BattleRouteData>();
    public Vector2[] slots;
    public Vector2 basePosition = Vector2.zero;
    public Vector2 minePosition = new Vector2(3f, 9f);
    public Vector2 heroSpawn = new Vector2(2.4f, 2.6f);
    [Tooltip("Boşsa bölge verisindeki dalgalar.")]
    public BattleWaveOverride[] waves;

    [NonSerialized] private BattleRoute[] cached;
    [NonSerialized] private int cachedHash;

    /// <summary>Örneklenmiş rotalar (kontrol noktaları değişince yeniden kurulur).</summary>
    public BattleRoute[] Routes
    {
        get
        {
            int hash = 17;
            foreach (var r in routes)
                if (r?.points != null) foreach (var p in r.points) hash = hash * 31 + p.GetHashCode();
            if (cached == null || hash != cachedHash)
            {
                var list = new List<BattleRoute>();
                foreach (var r in routes) if (r?.points != null && r.points.Length >= 2) list.Add(new BattleRoute(r.points));
                cached = list.ToArray();
                cachedHash = hash;
            }
            return cached;
        }
    }

    public bool HasMine => kind == CampaignLevelKind.DefendMine;

    /// <summary>Noktanın tüm rotalara en kısa uzaklığı.</summary>
    public float DistanceToRoad(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var r in Routes) best = Mathf.Min(best, r.DistanceTo(p));
        return best;
    }
}

