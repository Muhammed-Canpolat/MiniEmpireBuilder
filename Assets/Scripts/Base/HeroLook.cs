using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tek kahraman (Tiny Swords Warrior) görünümü: seviyenin renk kademesine göre (mavi, sarı,
/// kırmızı, mor, siyah) Warrior animasyonları. Eski Hero_axe denetleyicisi temel alınır, yalnız
/// klipleri değiştirilir; durum adları (Idle, Run, Attack, Hit, Death) ve parametreler aynı kalır.
/// "Hit" ve "Death" kalkan kaldırma (Guard) klibini oynatır: darbe alınca savaşçı kalkanla korunur.
/// </summary>
public static class HeroLook
{
    public static readonly string[] Colors = { "Blue", "Yellow", "Red", "Purple", "Black" };

    /// <summary>
    /// Tiny Swords Warrior kareleri 64 px/birim, eski kahraman kareleri 100 px/birim: aynı boyda
    /// görünmesi için Warrior kullanan kahramanın ölçeği bu oranla çarpılır.
    /// </summary>
    public const float WarriorScale = 0.64f;

    private static readonly Dictionary<int, RuntimeAnimatorController> cache =
        new Dictionary<int, RuntimeAnimatorController>();

    public static int Tier(int heroLevel) => BaseVisualCatalog.ColorIndex(heroLevel);

    /// <summary>Kahraman seviyesine göre renkli Warrior denetleyicisi; kurulamazsa null.</summary>
    public static RuntimeAnimatorController Controller(int heroLevel)
    {
        int tier = Tier(heroLevel);
        if (cache.TryGetValue(tier, out var cached) && cached != null) return cached;
        var catalog = BaseVisualCatalog.Instance;
        var baseController = catalog?.heroControllers != null && catalog.heroControllers.Length > 0
            ? catalog.heroControllers[0] : null;
        var warrior = catalog?.warriorControllers != null && tier < catalog.warriorControllers.Length
            ? catalog.warriorControllers[tier] : null;
        if (baseController == null || warrior == null) return null;

        AnimationClip idle = Find(warrior, "_Idle"), run = Find(warrior, "_Run"),
            attack = Find(warrior, "_Attack1") ?? Find(warrior, "_Attack"), guard = Find(warrior, "_Guard");
        if (idle == null || run == null) return null;

        var result = new AnimatorOverrideController(baseController) { name = "Hero_Warrior_" + Colors[tier] };
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        result.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            var original = overrides[i].Key;
            if (original == null) continue;
            string name = original.name;
            AnimationClip replacement = name.EndsWith("Idle") ? idle
                : name.EndsWith("Run") ? run
                : name.EndsWith("Attack") ? attack
                : name.EndsWith("Hit") || name.EndsWith("Death") ? guard ?? idle
                : null;
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
        }
        result.ApplyOverrides(overrides);
        cache[tier] = result;
        return result;
    }

    private static AnimationClip Find(RuntimeAnimatorController controller, string key)
    {
        foreach (var clip in controller.animationClips)
            if (clip != null && clip.name.Contains(key)) return clip;
        return null;
    }
}
