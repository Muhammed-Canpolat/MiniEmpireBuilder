using UnityEngine;

/// <summary>
/// 4. oturum (2.4): tüm birim/karakterlerin ölçeği için TEK referans. Tiny Swords karakter kareleri 64 px/birim çizilir ve gövde boyları
/// paket içinde aynıdır (Lancer karesi 320 px, Warrior 192 px olsa da gövde aynı boy); bu yüzden aynı sahnede her Tiny Swords birimi aynı
/// kök ölçekle çizilir. Referans birim kahramandır (Warrior). Yeni birim eklerken buradaki sahne ölçeğini × <see cref="Relative"/> kullanın.
/// </summary>
public static class UnitScale
{
    public enum Kind { Warrior, Lancer, Archer, Monk, Pawn, Sheep }

    /// <summary>Savaş: kahraman kökü 0,85 × Warrior 0,64 × savaş vurgusu 1,3 (HeroController.SetupAnimator).</summary>
    public const float Battle = 0.85f * HeroLook.WarriorScale * HeroLook.BattleScale;

    /// <summary>Ana Üs: çatı devriyesi, garnizon vb. süs birimleri (bina karelerine göre ölçülü; Ana Üs kahramanı ayrı görünür boydadır).</summary>
    public const float Base = 1.9f / 3f;   // garnizon: 192 px (3 u) kare 1,9 u boyda (BaseGarrisonVisual.FitVisibleHeight)

    /// <summary>Ada: ızgara 1 hücre = 1 birim, Tiny Swords doğal boyu.</summary>
    public const float Island = 1f;

    /// <summary>Birim türüne göre göreli çarpan (gerekirse tek yerden ayarlanır). Hepsi 1: Tiny Swords gövdeleri eş boy.</summary>
    public static float Relative(Kind kind)
    {
        switch (kind)
        {
            case Kind.Sheep: return 0.9f;
            default: return 1f;
        }
    }

    /// <summary>Savaşta kahramanın gerçek kök ölçeği (HeroController ile aynı tek kaynak: HeroLook.WarriorScaleFor, sefer kamerası ortho'su). Bug 34.</summary>
    public static float BattleHero()
    {
        var cam = Camera.main;
        return HeroLook.WarriorScaleFor(BattleSceneSetup.CameraSizeFor(cam != null ? cam.aspect : 0.5625f));
    }

    public static float InBattle(Kind kind) => BattleHero() * Relative(kind);
    public static float InBase(Kind kind) => Base * Relative(kind);
}
