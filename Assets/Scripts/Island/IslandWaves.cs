using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Akın dalga kadrosu (island_base_v2 §7.1, §7.3): Kademe I (Akın 1–9) Kızıl Korsanlar + DM2D karışımı; Akın 10 Kasap Kaptan.
/// Yeni tür ilk görünüşleri: Akıncı 1 · Kıyı Okçusu 2 · Fare 3 · Yarasa 5 · Kalkanlı Mızrakçı 6 · Ahşap Golem 8.
/// Oran: yakın ~%50 · menzilli ~%20 · hızlı ~%20 · tank ~%10 (Akın 8+, tekne başına en çok 1, ilk dalgada yok). Deterministiktir.
/// Yarasalar (uçanlar) teknesiz gelir: <see cref="FlyerSwarmSize"/>.
/// </summary>
public static class IslandWaves
{
    // Slot deseni (M yakın, R menzilli, F hızlı, T tank): 5 M · 2 R · 2 F · 1 T
    private const string Pattern = "MMRMFMRFMT";

    public static List<EnemyType> CrewFor(Progression.Island.BoatKind kind, int boatIndex, bool bossBoat, int akin, int wave = 1)
    {
        var crew = new List<EnemyType>();
        int count = Progression.Island.BoatCrew(kind);
        bool hasTank = false;
        for (int i = 0; i < count; i++)
        {
            char slot = Pattern[(i * 3 + boatIndex * 2 + wave) % Pattern.Length];
            EnemyType type = EnemyType.Raider;
            switch (slot)
            {
                case 'R': type = akin >= Progression.Island.ArcherFromAkin ? EnemyType.ShoreArcher : EnemyType.Raider; break;
                case 'F': type = akin >= Progression.Island.RatFromAkin ? EnemyType.Rat : EnemyType.Raider; break;
                case 'T': break;   // tank slotu: aşağıda tekne başına tek Golem kuralıyla yerleşir
                default:
                    if (akin >= Progression.Island.ShieldFromAkin && (i + boatIndex) % 3 == 0) type = EnemyType.ShieldLancer;
                    break;
            }
            crew.Add(type);
        }
        if (akin >= Progression.Island.FirstAkinOf(EnemyType.SkeletonWarrior)) UpgradeForTiers(crew, kind, boatIndex, akin, wave);
        // Tank (Akın 8+): ilk dalgada yok, tekne başına en çok 1 (Sandalda yok); iki teknede birine, dalgaya göre dönerek
        if (akin >= Progression.Island.GolemFromAkin && wave > 1 && kind != Progression.Island.BoatKind.Rowboat
            && (boatIndex / 2 + wave) % 2 == 0 && crew.Count > 0 && !hasTank)
            crew[crew.Count - 1] = PickWeighted(Pool(akin, EnemyType.WoodGolem, EnemyType.StoneGolem, EnemyType.CrystalGolem, EnemyType.MagmaGolem), boatIndex * 2 + wave);
        if (bossBoat && crew.Count > 0 && Progression.Island.BossFor(akin) != EnemyType.RedDragon)
            crew[0] = Progression.Island.BossFor(akin);   // Kızıl Ejder uçar: teknesiz, son dalgada ayrıca gelir (IslandRaidManager)
        return crew;
    }

    // ---- Faz E2: Kademe II–IV karışımı (§7.3). Kademe I (Akın ≤ 10) yukarıdaki eski kuralla aynen kalır. ----

    /// <summary>Türler ilk görünüş sırasıyla; "kademe içinde son açılan türler daha sık gelir": en yeni ×3, bir önceki ×2, diğerleri ×1.</summary>
    private static EnemyType PickWeighted(List<EnemyType> pool, int seed)
    {
        var expanded = new List<EnemyType>();
        for (int j = 0; j < pool.Count; j++)
        {
            int weight = j == pool.Count - 1 ? 3 : j == pool.Count - 2 ? 2 : 1;
            for (int w = 0; w < weight; w++) expanded.Add(pool[j]);
        }
        return expanded[Mathf.Abs(seed) % expanded.Count];
    }

    private static List<EnemyType> Pool(int akin, params EnemyType[] ordered)
    {
        var list = new List<EnemyType>();
        foreach (var type in ordered) if (akin >= Progression.Island.FirstAkinOf(type)) list.Add(type);
        return list;
    }

    /// <summary>Kademe II–IV: yakın slotlara İskelet/Zombi/elit, menzililere İskelet Okçu, hızlılara İmp, tekne başına ≤1 özel (büyücü/örümcek/sukkubus, ilk dalgada yok).</summary>
    private static void UpgradeForTiers(List<EnemyType> crew, Progression.Island.BoatKind kind, int boatIndex, int akin, int wave)
    {
        var melee = Pool(akin, EnemyType.Raider, EnemyType.ShieldLancer, EnemyType.SkeletonWarrior, EnemyType.Zombie);
        var ranged = Pool(akin, EnemyType.ShoreArcher, EnemyType.SkeletonArcher);
        var fast = Pool(akin, EnemyType.Rat, EnemyType.Imp);
        var special = Pool(akin, EnemyType.BoneMage, EnemyType.Spider, EnemyType.Succubus);
        var elite = Pool(akin, EnemyType.ZombieWarrior, EnemyType.Vampire, EnemyType.Demon);
        bool specialUsed = false, eliteUsed = false;
        for (int i = 0; i < crew.Count; i++)
        {
            int seed = i * 3 + boatIndex * 2 + wave;
            switch (crew[i])
            {
                case EnemyType.Raider: case EnemyType.ShieldLancer:
                    if (elite.Count > 0 && wave > 1 && !eliteUsed && i == 1 && crew.Count >= 4 && (boatIndex + wave) % 2 == 1)   // ölçüm turu 2: her uygun teknede değil, ikisinden birinde
                    { crew[i] = PickWeighted(elite, seed + boatIndex); eliteUsed = true; }
                    else crew[i] = PickWeighted(melee, seed);
                    break;
                case EnemyType.ShoreArcher:
                    if (special.Count > 0 && wave > 1 && !specialUsed && crew.Count >= 4 && (boatIndex + wave) % 3 == 0)   // özel birim sıklığı (ölçüm turu 1: %50 → %33)
                    { crew[i] = PickWeighted(special, seed + wave); specialUsed = true; }
                    else crew[i] = PickWeighted(ranged, seed);
                    break;
                case EnemyType.Rat:
                    crew[i] = PickWeighted(fast, seed);
                    break;
            }
        }
    }

    /// <summary>Teknesiz uçan sürü üyesi: Yarasa; Akın 18'den itibaren her ikinci üye Hayalet.</summary>
    public static EnemyType FlyerTypeAt(int akin, int index) =>
        akin >= Progression.Island.FirstAkinOf(EnemyType.Ghost) && index % 2 == 1 ? EnemyType.Ghost : EnemyType.Bat;

    /// <summary>Bu dalgada gelen yarasa sürüsü büyüklüğü (3–5); Akın 5'ten önce ve boss son dalgasında 0 değil, ilk dalgada 0.</summary>
    public static int FlyerSwarmSize(int akin, int wave)
    {
        if (akin < Progression.Island.FlyerFromAkin || wave < 2) return 0;
        return Mathf.Clamp(Progression.Island.FlyerSwarmMin + (akin - Progression.Island.FlyerFromAkin) / 2,
            Progression.Island.FlyerSwarmMin, Progression.Island.FlyerSwarmMax);
    }

    /// <summary>Bütün (boss olmayan) türler ilk görünüş Akın'ına göre; ilk görünüş sırası = panodaki portre sırası.</summary>
    private static readonly EnemyType[] Introduced =
    {
        EnemyType.Raider, EnemyType.ShoreArcher, EnemyType.Rat, EnemyType.Bat, EnemyType.ShieldLancer, EnemyType.WoodGolem,
        EnemyType.SkeletonWarrior, EnemyType.SkeletonArcher, EnemyType.Imp, EnemyType.BoneMage, EnemyType.Spider, EnemyType.Zombie,
        EnemyType.StoneGolem, EnemyType.Ghost, EnemyType.ZombieWarrior, EnemyType.Succubus, EnemyType.CrystalGolem, EnemyType.Vampire,
        EnemyType.Demon, EnemyType.MagmaGolem,
    };

    /// <summary>Bu Akın'da görünebilecek türler (panoda portre olarak gösterilir), ilk görünüş sırasıyla; boss Akın'ında boss sonda.</summary>
    public static List<EnemyType> RosterFor(int akin)
    {
        var roster = new List<EnemyType>();
        foreach (var type in Introduced) if (akin >= Progression.Island.FirstAkinOf(type)) roster.Add(type);
        if (Progression.Island.IsBossAkin(akin)) roster.Add(Progression.Island.BossFor(akin));
        return roster;
    }

    /// <summary>Bu Akın'da ilk kez gelen tür (yoksa null): "YENİ DÜŞMAN" afişi. Boss Akın'ının ilk turunda boss (10/20/30).</summary>
    public static EnemyType? NewTypeAt(int akin)
    {
        foreach (var type in Introduced) if (akin == Progression.Island.FirstAkinOf(type) && type != EnemyType.Raider) return type;
        if (Progression.Island.IsBossAkin(akin) && akin <= 3 * Progression.Island.BossEvery) return Progression.Island.BossFor(akin);
        return null;
    }

    /// <summary>Bir kenar noktası ve o kenardan haritaya bakan yön okunun yeri (yarasa sürüsü doğuşu): kamera kenarı = harita kenarı.</summary>
    public static Vector2 FlyerEdgePoint(int swarmIndex)
    {
        // Kuzey-batı / kuzey-doğu / güney kenarlarından sırayla
        switch (swarmIndex % 3)
        {
            case 0: return new Vector2(-1.5f, IslandLayout.Height * 0.62f);
            case 1: return new Vector2(IslandLayout.Width + 1.5f, IslandLayout.Height * 0.55f);
            default: return new Vector2(IslandLayout.Width * 0.5f, -1.5f);
        }
    }
}
