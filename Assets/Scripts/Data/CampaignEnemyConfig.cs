using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 8. oturum (D97): sefer savaşlarının düşman sayıları ve canı tek yerden. Resources/Config/CampaignEnemyConfig.asset (Inspector'dan düzenlenir;
/// varlık yoksa aynı varsayılanlar kullanılır).
/// - <see cref="totals"/>: bölge × seviye türü başına savaşta GERÇEKTEN doğan düşman sayısı (boss dahil; çağrılan kurtlar hariç). Üst bardaki
///   "kalan / toplam" bu sayıdır. Eski Wave1Cap / boss ek düşmanı bu toplamların içine katıldı (sefer savaşlarında artık uygulanmaz).
/// - Düşmanlar dalgalara artan ağırlıkla dağıtılır (ilk dalga en küçük); dalga başına <see cref="maxPerWave"/> aşılırsa dalga eklenir.
/// - Öldürme altını: savaşın toplam öldürme altını, bölge varlığındaki ESKİ sayılarla (eski kurallarla) aynı kalacak şekilde düşman başına ölçeklenir.
/// Sonsuz Sefer ve Ejder İstilası etkilenmez.
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire Builder/Campaign Enemy Config")]
public sealed class CampaignEnemyConfig : ScriptableObject
{
    [System.Serializable]
    public struct RegionRow
    {
        public string name;
        [Tooltip("Hayatta Kal, Üssü Koru, Madeni Koru, Zamana Karşı, Boss")]
        public int survive, defendBase, defendMine, timeTrial, boss;
        public int For(CampaignLevelKind kind) => kind switch
        {
            CampaignLevelKind.Survive => survive,
            CampaignLevelKind.DefendBase => defendBase,
            CampaignLevelKind.DefendMine => defendMine,
            CampaignLevelKind.TimeTrial => timeTrial,
            _ => boss,
        };
    }

    [Header("Düşman sayıları (bölge 1–6, savaşta doğan toplam)")]
    public RegionRow[] totals = DefaultTotals();

    [Header("Can")]
    [Tooltip("Tüm sefer düşmanlarının canı (boss dahil); bölge haritası çarpanının üstüne.")]
    public float healthMultiplier = 1.1f;

    [Header("Dalgalar")]
    [Tooltip("Bir dalgada en çok bu kadar düşman; aşılırsa dalga sayısı artar (tek dalgaya yığılmasın).")]
    public int maxPerWave = 12;
    [Tooltip("Dalga ağırlığı artışı: dalga i'nin payı 1 + i × bu değer (ilk dalga en küçük).")]
    public float waveRamp = 0.2f;
    [Tooltip("Dalga temizlendikten sonra sonraki dalga duyurusuna kadar nefes payı (sn; duyuru 3 sn ayrıca).")]
    public float interWaveSeconds = 8f;

    [Header("Ekonomi")]
    [Tooltip("Açıkken savaşın toplam öldürme altını eski düşman sayısıyla aynı kalır (düşman başına altın ölçeklenir).")]
    public bool keepKillGoldTotal = true;

    public static RegionRow[] DefaultTotals() => new[]
    {
        new RegionRow { name = "1 Yıkık Kale Yolu",       survive = 8,  defendBase = 14, defendMine = 17, timeTrial = 19, boss = 20 },
        new RegionRow { name = "2 Sisli Orman",           survive = 16, defendBase = 20, defendMine = 21, timeTrial = 24, boss = 26 },
        new RegionRow { name = "3 Terk Edilmiş Maden",    survive = 18, defendBase = 25, defendMine = 27, timeTrial = 30, boss = 32 },
        new RegionRow { name = "4 Donmuş Geçit",          survive = 20, defendBase = 27, defendMine = 30, timeTrial = 33, boss = 38 },
        new RegionRow { name = "5 Karanlık Kale",         survive = 22, defendBase = 32, defendMine = 35, timeTrial = 38, boss = 50 },
        new RegionRow { name = "6 Ejderin İni",           survive = 24, defendBase = 35, defendMine = 38, timeTrial = 40, boss = 55 },
    };

    private static CampaignEnemyConfig active;
    public static CampaignEnemyConfig Active
    {
        get
        {
            if (active != null) return active;
            active = Resources.Load<CampaignEnemyConfig>("Config/CampaignEnemyConfig");
            if (active == null) { active = CreateInstance<CampaignEnemyConfig>(); active.name = "CampaignEnemyConfig (varsayılan)"; }
            return active;
        }
    }

    /// <summary>Bölge × tür toplamı (tablo dışı bölgede 0 = eski kural).</summary>
    public int Total(int regionId, CampaignLevelKind kind) =>
        totals != null && regionId >= 1 && regionId <= totals.Length ? totals[regionId - 1].For(kind) : 0;

    /// <summary>
    /// 8. oturum (D97, ölçüm sonrası): toplam, ESKİ dalga büyüklükleri oranında dağıtılır (her dalga aynı oranda büyür; boss dalgası küçük kalır,
    /// eski tasarımın ritmi korunur). <see cref="maxPerWave"/> aşılırsa en büyük dalga ikiye bölünür (tek dalgaya yığılmaz). Toplam korunur.
    /// </summary>
    public List<int> SplitLike(int total, IList<int> legacyWaves) => SplitLike(total, legacyWaves, out _);

    /// <summary><see cref="SplitLike(int, IList{int})"/>; <paramref name="sources"/>: her yeni dalganın geldiği eski dalga (tür karışımı için).</summary>
    public List<int> SplitLike(int total, IList<int> legacyWaves, out List<int> sources)
    {
        sources = new List<int>();
        if (legacyWaves == null || legacyWaves.Count == 0) { var plain = Split(total, 1); foreach (var _ in plain) sources.Add(0); return plain; }
        for (int i = 0; i < legacyWaves.Count; i++) sources.Add(i);
        float sum = 0f; foreach (var w in legacyWaves) sum += Mathf.Max(1, w);
        var counts = new List<int>(); var rems = new List<(float r, int i)>(); int used = 0;
        for (int i = 0; i < legacyWaves.Count; i++)
        {
            float exact = total * Mathf.Max(1, legacyWaves[i]) / sum;
            int c = Mathf.Max(1, Mathf.FloorToInt(exact));
            counts.Add(c); used += c; rems.Add((exact - Mathf.Floor(exact), i));
        }
        rems.Sort((x, y) => y.r != x.r ? y.r.CompareTo(x.r) : x.i.CompareTo(y.i));
        for (int k = 0; used < total; k = (k + 1) % counts.Count) { counts[rems[k].i]++; used++; }
        for (int i = counts.Count - 1; used > total && i >= 0; i--) while (counts[i] > 1 && used > total) { counts[i]--; used--; }
        int cap = Mathf.Max(1, maxPerWave);
        for (int guard = 0; guard < 20; guard++)
        {
            int big = 0;
            for (int i = 1; i < counts.Count; i++) if (counts[i] > counts[big]) big = i;
            if (counts[big] <= cap) break;
            int half = counts[big] / 2;
            counts[big] -= half;
            counts.Insert(big, half);   // bölünen dalganın küçük yarısı önce gelir
            sources.Insert(big, sources[big]);
        }
        return counts;
    }

    /// <summary>
    /// Dalga dağılımı: en az <paramref name="minWaves"/> dalga, dalga başına ≤ maxPerWave; ağırlık 1 + i·ramp (artan), en büyük kalanla yuvarlama.
    /// Her dalga en az 1 düşman. Toplam korunur.
    /// </summary>
    public List<int> Split(int total, int minWaves)
    {
        int waves = Mathf.Max(1, Mathf.Max(minWaves, Mathf.CeilToInt(total / (float)Mathf.Max(1, maxPerWave))));
        waves = Mathf.Min(waves, Mathf.Max(1, total));
        // Artan ağırlık son dalgayı ortalamanın üstüne çıkarır: sınır aşılıyorsa dalga eklenir.
        for (int guard = 0; guard < 20; guard++)
        {
            var trial = SplitInto(total, waves);
            if (trial[trial.Count - 1] <= Mathf.Max(1, maxPerWave) || waves >= total) return trial;
            waves++;
        }
        return SplitInto(total, waves);
    }

    private List<int> SplitInto(int total, int waves)
    {
        var weights = new float[waves];
        float sum = 0f;
        for (int i = 0; i < waves; i++) { weights[i] = 1f + i * waveRamp; sum += weights[i]; }
        var counts = new List<int>();
        var remainders = new List<(float r, int i)>();
        int used = 0;
        for (int i = 0; i < waves; i++)
        {
            float exact = total * weights[i] / sum;
            int c = Mathf.Max(1, Mathf.FloorToInt(exact));
            counts.Add(c); used += c;
            remainders.Add((exact - Mathf.Floor(exact), i));
        }
        remainders.Sort((a, b) => b.r != a.r ? b.r.CompareTo(a.r) : b.i.CompareTo(a.i));
        for (int k = 0; used < total; k = (k + 1) % waves) { counts[remainders[k].i]++; used++; }
        // Taşma (her dalga en az 1 kuralından): baştaki dalgalardan düş.
        for (int i = 0; used > total && i < waves; i++) while (counts[i] > 1 && used > total) { counts[i]--; used--; }
        return counts;
    }
}
