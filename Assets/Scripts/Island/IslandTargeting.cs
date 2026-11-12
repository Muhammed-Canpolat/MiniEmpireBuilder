using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ada düşmanının hedef kuralları (island_base_v2 §7.5): rolüne göre kule/ev/okçu/kale. Yerleşimi okunur kılar:
/// yakın dövüşçü şeritteki binayı (1,5 u), menzili ilk kuleyi (5 u), Fare en yakın üretim binasını (yar tırmanarak),
/// Yarasa okçuyu, Tank yalnız kaleyi seçer. Hiç eşleşme yoksa (target == null) v1 kuralı (okçu/kale) sürer.
/// </summary>
public static class IslandTargeting
{
    public enum Role { Melee, Ranged, Fast, Flyer, Tank }

    public struct Result
    {
        public Transform target;
        public Vector2 point;
        public float radius;
    }

    public static Role RoleOf(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.ShoreArcher: return Role.Ranged;
            case EnemyType.Rat: return Role.Fast;
            case EnemyType.Bat: return Role.Flyer;
            case EnemyType.WoodGolem: return Role.Tank;
            default: return Role.Melee;
        }
    }

    private static Result Of(IslandBuilding building) =>
        new Result { target = building.transform, point = building.Center, radius = building.Radius };

    /// <summary>Faz E1: yakın dövüşçüyü tutan en yakın Kışla Lancer'ı (yoksa boş sonuç).</summary>
    private static Result NearLancer(Vector2 position, float radius)
    {
        var none = new Result();
        float best = radius;
        foreach (var lancer in IslandLancer.All)
        {
            if (lancer == null || !lancer.Alive) continue;
            float d = Vector2.Distance(position, lancer.Position);
            if (d >= best) continue;
            best = d;
            none = new Result { target = lancer.transform, point = lancer.Position, radius = 0.4f };
        }
        return none;
    }

    public static Result Pick(EnemyType type, Vector2 position, Transform castle, float attackRange)
    {
        var none = new Result();
        switch (RoleOf(type))
        {
            case Role.Flyer:
            {
                var archer = IslandArcher.Instance;
                if (archer != null && archer.IsAlive)
                    return new Result { target = archer.transform, point = archer.Position, radius = 0.3f };
                return none;   // kaleye düz çizgi (v1 varsayılanı)
            }
            case Role.Tank:
                return NearLancer(position, Progression.Island.LancerBlockAggro);   // yalnız kale; Lancer yolunu keserse onu vurur (§7.5)
            case Role.Ranged:
            {
                // 5 u içine giren ilk kule ya da okçu (adacık kulesi dahil)
                Result best = none;
                float bestDistance = Progression.Island.RangedAggro;
                foreach (var building in IslandBuilding.All)
                {
                    if (building == null || building.Ruined || !building.IsTower) continue;
                    float d = Vector2.Distance(position, building.Center);
                    if (d < bestDistance) { bestDistance = d; best = Of(building); }
                }
                var archer = IslandArcher.Instance;
                if (archer != null && archer.IsAlive)
                {
                    float d = Vector2.Distance(position, archer.Position);
                    if (d < bestDistance) best = new Result { target = archer.transform, point = archer.Position, radius = 0.3f };
                }
                return best;
            }
            case Role.Fast:
            {
                // En yakın üretim binası (Ev/Oduncu/Ağıl): yar tırmanarak kısa yoldan gider
                Result best = none;
                float bestDistance = Progression.Island.RatClimbReach;
                foreach (var building in IslandBuilding.All)
                {
                    if (building == null || building.Ruined || !building.IsProduction) continue;
                    float d = Vector2.Distance(position, building.Center);
                    if (d < bestDistance) { bestDistance = d; best = Of(building); }
                }
                return best;
            }
            default:
            {
                // Yakın: şeritteki 1,5 u içindeki Lancer (önce) ya da bina
                var lancerHit = NearLancer(position, Progression.Island.LancerAggro);
                if (lancerHit.target != null) return lancerHit;
                Result best = none;
                float bestDistance = Progression.Island.LaneAggroRadius;
                foreach (var building in IslandBuilding.All)
                {
                    if (building == null || building.Ruined || building.IsCastle) continue;
                    float d = Vector2.Distance(position, building.Center) - building.Radius;
                    if (d < bestDistance) { bestDistance = d; best = Of(building); }
                }
                return best;
            }
        }
    }
}

/// <summary>Yenilgi etiketi (§7.6). <see cref="tag"/> telemetri anahtarı; metin oyuncuya "ne yapmalıyım" der.</summary>
public struct IslandDefeatLabel
{
    public string tag;
    public string text;
    /// <summary>true: gerçek bir neden bulundu. false: yalnız "kale düştü" (etiketsiz yenilgi).</summary>
    public bool explained;
}

/// <summary>Savaş sırasında toplanan hasar dağılımı (kimin kime ne kadar vurduğu) ve etiket kuralları.</summary>
public static class IslandDefeat
{
    public const string Unexplained = "castle_fell";

    public static IslandDefeatLabel Classify(IReadOnlyDictionary<EnemyType, float> damageByType, float[] castleDamageByBeach,
        int shieldBlocks, System.Func<int, bool> beachWatched)
    {
        float total = 0f;
        foreach (var pair in damageByType) total += pair.Value;
        if (total <= 0f)
            return new IslandDefeatLabel { tag = Unexplained, text = L.T("Kale düştü.", "The keep fell."), explained = false };
        float Share(EnemyType t) => damageByType.TryGetValue(t, out float v) ? v / total : 0f;

        if (Share(EnemyType.Bat) >= 0.30f)
            return Label("flyer_unanswered", L.T("Yarasaları yalnız kuleler ve okçu vurabilir.", "Only towers and the archer can hit bats."));
        if (Share(EnemyType.WoodGolem) >= 0.30f)
            return Label("golem_armor", L.T("Golemlere dolu atış kullan.", "Use charged shots on golems."));
        if (Share(EnemyType.ButcherCaptain) >= 0.40f)
            return Label("boss_unbroken", L.T("Kasap Kaptan kaleyi ezdi: önce onu dolu atışla düşür.", "The Butcher crushed the keep: take him down first with charged shots."));
        if (Share(EnemyType.Rat) >= 0.25f)
            return Label("climber_shortcut", L.T("Fareler yardan tırmandı: yar başına kule koy.", "Rats climbed the cliffs: put a tower at the cliff top."));
        if (Share(EnemyType.ShieldLancer) >= 0.30f || (shieldBlocks >= 6 && Share(EnemyType.ShieldLancer) >= 0.15f))
            return Label("shield_front", L.T("Kalkanlı Mızrakçılar önden gelen oku sektirdi: dolu atış kullan.", "Shield Lancers deflected frontal arrows: use charged shots."));

        // Kumsal temelli: kaleye en çok hasar veren çıkış noktası
        int top = 0;
        float beachTotal = 0f;
        for (int i = 0; i < castleDamageByBeach.Length; i++)
        {
            beachTotal += castleDamageByBeach[i];
            if (castleDamageByBeach[i] > castleDamageByBeach[top]) top = i;
        }
        if (beachTotal > 0f && castleDamageByBeach[top] / beachTotal >= 0.5f)
        {
            char name = IslandLayout.Beaches[top].name;
            if (!beachWatched(top))
            {
                // Doğu (B, D) ve güney yürüyüşleri S3 merdivenine çıkar
                bool eastStair = top == 1 || top == 3;
                return eastStair
                    ? Label("stair_undefended", L.T($"{name} kumsalının S3 merdiveni boştu: oraya kule ya da atış yeri koy.", $"The S3 stair above beach {name} was undefended: add a tower or a post."))
                    : Label("beach_unwatched", L.T($"{name} kumsalı izlenmedi.", $"Beach {name} was not watched."));
            }
            return Label("beach_overrun", L.T($"{name} kumsalı bastırıldı: dolu atışla öncüleri düşür.", $"Beach {name} was overrun: pick off the vanguard with charged shots."));
        }
        return Label("multi_beach", L.T("Birkaç kumsaldan birden baskı geldi: kulesiz kumsala kule ya da atış yeri ekle.", "Pressure from several beaches: cover the open beach with a tower or a post."));
    }

    private static IslandDefeatLabel Label(string tag, string text) => new IslandDefeatLabel { tag = tag, text = text, explained = true };

    /// <summary>Kumsal izleniyor mu: okçunun atış yeri iniş noktasını kapsıyor ya da bir kule tekne yolunu/inişi menzilinde tutuyor.</summary>
    public static bool BeachWatched(int beach, IslandArcher archer)
    {
        var info = IslandLayout.Beaches[beach];
        if (archer != null && archer.IsAlive && Vector2.Distance(archer.Position, info.landing) <= archer.Range + 1f) return true;
        foreach (var building in IslandBuilding.All)
        {
            if (building == null || building.Ruined || !building.IsTower) continue;
            var tower = building.GetComponent<IslandTower>();
            float range = tower != null && tower.Range > 0f ? tower.Range : Progression.Island.TowerRange;
            if (Vector2.Distance(building.Center, info.landing) <= range) return true;
            foreach (var point in info.boatPath) if (Vector2.Distance(building.Center, point) <= range) return true;
        }
        return false;
    }
}
