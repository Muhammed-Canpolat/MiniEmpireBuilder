using System;
using UnityEngine;

public enum ShopResource { Gold, Wood, Meat }

/// <summary>
/// 6. oturum (2.4): elmasla kaynak paketleri. Fiyat/miktar hesabı burada, sayılar <see cref="ShopConfig"/>'ten.
/// </summary>
public static class ShopService
{
    public const int PackCount = 3;

    public static int MainBaseLevel => GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.GetMainBaseLevel()) : 1;

    /// <summary>Altın kuru (elmas başına): Ana Üs seviyesindeki madenin saatlik üretimi / gemsPerMineHour (en az minGoldPerGem).</summary>
    public static int GoldPerGemAt(int mainBaseLevel)
    {
        var cfg = ShopConfig.Instance;
        float perHour = Economy.Active.MineGps(Mathf.Clamp(mainBaseLevel, 1, Progression.MaxLevel)) * 3600f;
        return Mathf.Max(cfg.minGoldPerGem, Mathf.RoundToInt(perHour / Mathf.Max(0.1f, cfg.gemsPerMineHour)));
    }

    public static int GoldPerGem => GoldPerGemAt(MainBaseLevel);

    public static int RatePerGem(ShopResource r) =>
        r == ShopResource.Gold ? GoldPerGem : r == ShopResource.Meat ? GemEconomy.MeatPerGem : GemEconomy.WoodPerGem;

    /// <summary>Paketin hedef miktarı: altın = sıradaki Ana Üs bedelinin payı; Et/Odun = ada cüzdan tavanının payı.</summary>
    public static int PackAmount(ShopResource r, int tier)
    {
        var cfg = ShopConfig.Instance;
        tier = Mathf.Clamp(tier, 0, PackCount - 1);
        float share;
        int reference;
        if (r == ShopResource.Gold)
        {
            share = cfg.goldPackShareOfNextMainBase[tier];
            int next = Mathf.Min(MainBaseLevel + 1, Progression.MaxLevel);
            reference = Progression.UpgradeCost(BuildingType.MainBase, next);
            if (MainBaseLevel >= Progression.MaxLevel) reference = Progression.UpgradeCost(BuildingType.MainBase, Progression.MaxLevel);
        }
        else
        {
            share = cfg.islandPackShareOfWallet[tier];
            reference = r == ShopResource.Meat ? IslandEconomy.MeatCap : IslandEconomy.WoodCap;
        }
        int amount = Mathf.Max(1, Mathf.RoundToInt(reference * share));
        int step = amount >= 1000 ? 50 : amount >= 100 ? 10 : 1;
        return Mathf.Max(step, Mathf.RoundToInt(amount / (float)step) * step);
    }

    /// <summary>Paketin elmas bedeli: miktar / kur / (1 + bonus), yukarı yuvarlı, en az minPackGems.</summary>
    public static int PackGems(ShopResource r, int tier)
    {
        var cfg = ShopConfig.Instance;
        tier = Mathf.Clamp(tier, 0, PackCount - 1);
        float gems = PackAmount(r, tier) / (float)RatePerGem(r) / (1f + cfg.packBonus[tier]);
        return Mathf.Max(cfg.minPackGems, Mathf.CeilToInt(gems));
    }

    /// <summary>Ada kaynakları ada açılınca (Ana Üs 5) satılır.</summary>
    public static bool Available(ShopResource r) =>
        r == ShopResource.Gold || MainBaseLevel >= Progression.Island.UnlockMainBaseLevel;

    public enum BuyResult { Ok, NoGems, Locked, WalletFull }

    /// <summary>Altın paketi cüzdan tavanını aşıyorsa satılmaz (fazlası silinirdi).</summary>
    public static BuyResult CanBuy(ShopResource r, int tier)
    {
        var gm = GameManager.Instance;
        if (gm == null) return BuyResult.Locked;
        if (!Available(r)) return BuyResult.Locked;
        if (r == ShopResource.Gold && gm.Gold + PackAmount(r, tier) > gm.GetMaxGoldCapacity()) return BuyResult.WalletFull;
        if (gm.Gems < PackGems(r, tier)) return BuyResult.NoGems;
        return BuyResult.Ok;
    }

    public static event Action<ShopResource, int> Bought;

    public static BuyResult Buy(ShopResource r, int tier)
    {
        var result = CanBuy(r, tier);
        if (result != BuyResult.Ok) return result;
        var gm = GameManager.Instance;
        int amount = PackAmount(r, tier);
        if (!gm.SpendGems(PackGems(r, tier))) return BuyResult.NoGems;
        if (r == ShopResource.Gold) gm.AddGold(amount);
        else if (r == ShopResource.Meat) IslandEconomy.AddMeat(amount);
        else IslandEconomy.AddWood(amount);
        gm.SaveGame();
        Bought?.Invoke(r, amount);
        return BuyResult.Ok;
    }
}
