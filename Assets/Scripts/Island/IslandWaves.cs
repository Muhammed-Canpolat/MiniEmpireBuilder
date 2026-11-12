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
        // Tank (Akın 8+): ilk dalgada yok, tekne başına en çok 1 (Sandalda yok); iki teknede birine, dalgaya göre dönerek
        if (akin >= Progression.Island.GolemFromAkin && wave > 1 && kind != Progression.Island.BoatKind.Rowboat
            && (boatIndex / 2 + wave) % 2 == 0 && crew.Count > 0 && !hasTank)
            crew[crew.Count - 1] = EnemyType.WoodGolem;
        if (bossBoat && crew.Count > 0) crew[0] = EnemyType.ButcherCaptain;
        return crew;
    }

    /// <summary>Bu dalgada gelen yarasa sürüsü büyüklüğü (3–5); Akın 5'ten önce ve boss son dalgasında 0 değil, ilk dalgada 0.</summary>
    public static int FlyerSwarmSize(int akin, int wave)
    {
        if (akin < Progression.Island.FlyerFromAkin || wave < 2) return 0;
        return Mathf.Clamp(Progression.Island.FlyerSwarmMin + (akin - Progression.Island.FlyerFromAkin) / 2,
            Progression.Island.FlyerSwarmMin, Progression.Island.FlyerSwarmMax);
    }

    /// <summary>Bu Akın'da görünebilecek türler (panoda portre olarak gösterilir), ilk görünüş sırasıyla.</summary>
    public static List<EnemyType> RosterFor(int akin)
    {
        var roster = new List<EnemyType> { EnemyType.Raider };
        if (akin >= Progression.Island.ArcherFromAkin) roster.Add(EnemyType.ShoreArcher);
        if (akin >= Progression.Island.RatFromAkin) roster.Add(EnemyType.Rat);
        if (akin >= Progression.Island.FlyerFromAkin) roster.Add(EnemyType.Bat);
        if (akin >= Progression.Island.ShieldFromAkin) roster.Add(EnemyType.ShieldLancer);
        if (akin >= Progression.Island.GolemFromAkin) roster.Add(EnemyType.WoodGolem);
        if (Progression.Island.IsBossAkin(akin)) roster.Add(EnemyType.ButcherCaptain);
        return roster;
    }

    /// <summary>Bu Akın'da ilk kez gelen tür (yoksa null): "YENİ DÜŞMAN" afişi.</summary>
    public static EnemyType? NewTypeAt(int akin)
    {
        if (akin == Progression.Island.ArcherFromAkin) return EnemyType.ShoreArcher;
        if (akin == Progression.Island.RatFromAkin) return EnemyType.Rat;
        if (akin == Progression.Island.FlyerFromAkin) return EnemyType.Bat;
        if (akin == Progression.Island.ShieldFromAkin) return EnemyType.ShieldLancer;
        if (akin == Progression.Island.GolemFromAkin) return EnemyType.WoodGolem;
        if (Progression.Island.IsBossAkin(akin) && akin == Progression.Island.BossEvery) return EnemyType.ButcherCaptain;
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
