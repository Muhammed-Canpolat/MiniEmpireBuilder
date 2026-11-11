using UnityEngine;

/// <summary>
/// Martı Adası oturumu: ada ayrı bir .unity sahnesi değil, BattleScene'i "ev sahibi" olarak kullanan bir moddur
/// (Docs/design/island_base.md §10). <see cref="Active"/> iken SceneAutoSetup savaş kurulumu yerine
/// <see cref="IslandSceneSetup"/> ekler. Etkin değilken hiçbir paylaşılan sınıfın davranışı değişmez.
/// </summary>
public static class IslandSession
{
    /// <summary>Ada modu açık mı (BattleScene ada olarak kurulur).</summary>
    public static bool Active { get; private set; }

    public static void Begin() => Active = true;
    public static void Clear() => Active = false;

    public static IslandSaveData Save => GameManager.Instance?.PlayerData?.island;

    public static int MainBaseLevel => GameManager.Instance != null ? GameManager.Instance.GetMainBaseLevel() : 1;

    /// <summary>Ada açık mı (Ana Üs 5). Geliştirici ayarlarında hep açık değil: kuralı bozmaz.</summary>
    public static bool Unlocked(int mainBaseLevel) => mainBaseLevel >= Progression.Island.UnlockMainBaseLevel;
    public static bool Unlocked() => Unlocked(MainBaseLevel);

    public static int KeepLevel() => Progression.Island.EffectiveKeepLevel(Save?.keepLevel ?? 1, MainBaseLevel);
    public static int ArcherLevel() => Progression.Island.EffectiveArcherLevel(Save?.archerLevel ?? 1, KeepLevel());

    /// <summary>
    /// Oto-ateş açılmış mı: Kıyı Kalesi seviyesi eşiği geçmeli. MVP'de kalenin yükseltmesi yok;
    /// DevSettings açıkken (editör/Development) test için de açılır. Eşik sabiti değişmez.
    /// </summary>
    public static bool AutoFireUnlocked(int keepLevel) =>
        keepLevel >= Progression.Island.AutoFireUnlockKeepLevel || DevSettings.Enabled;

    /// <summary>Akın ödülü (ilk zafer tam, tekrar %30; elmas yalnız ilk zaferde).</summary>
    public struct Reward
    {
        public int meat, wood, gold, gems;
        public bool firstWin;
    }

    public static Reward RewardFor(int akin, int highestAkin)
    {
        bool first = akin > highestAkin;
        float rate = first ? 1f : Progression.Island.ReplayRewardRate;
        return new Reward
        {
            firstWin = first,
            meat = Mathf.RoundToInt(Progression.Island.AkinMeat(akin) * rate),
            wood = Mathf.RoundToInt(Progression.Island.AkinWood(akin) * rate),
            gold = Mathf.RoundToInt(Progression.Island.AkinGold(akin) * rate),
            gems = first ? Progression.Island.AkinGems(akin) : 0,
        };
    }

    /// <summary>
    /// Zaferi kaydeder: Et/Odun ada cüzdanına, altın ve elmas Ana Üs cüzdanına. Aynı Akın'ı tekrar saymaz
    /// (en yüksek Akın yalnız artar).
    /// </summary>
    public static Reward GrantVictory(int akin)
    {
        var save = Save;
        int highest = save != null ? save.highestAkin : 0;
        var reward = RewardFor(akin, highest);
        if (save == null) return reward;
        save.meat += reward.meat;
        save.wood += reward.wood;
        save.highestAkin = Mathf.Max(save.highestAkin, akin);
        save.lastAkin = akin;
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.AddGold(reward.gold);
            if (reward.gems > 0) gm.AddGems(reward.gems, false);
            gm.SaveGame();
        }
        return reward;
    }
}
