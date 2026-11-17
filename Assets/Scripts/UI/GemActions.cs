using System;
using UnityEngine;

/// <summary>
/// Elmas harcayan işlemlerin ortak "sor → harca → bildir" akışı. Arayüzler (bina paneli,
/// inşaatçı paneli, Elmas Dükkanı) yalnız bunları çağırır; fiyatlar <see cref="GemEconomy"/>'de.
/// </summary>
public static class GemActions
{
    public static void Toast(string message)
    {
        var setup = UnityEngine.Object.FindFirstObjectByType<BaseSceneSetup>();
        if (setup != null) setup.ShowHudToast(message);
        else Debug.Log("[GemActions] " + message);
    }

    /// <summary>Süren işi hemen bitirme teklifi.</summary>
    public static void OfferFinish(BuilderTaskSave task, Action after = null)
    {
        var gm = GameManager.Instance;
        if (gm == null || task == null) return;
        int cost = GameManager.FinishNowCost(task);
        string what = task.kind == "remove" ? L.T("Temizlik", "Clearing")
            : string.IsNullOrEmpty(task.label) ? L.T("İnşaat", "Construction") : task.label;
        // Bedava seçenek: reklamla −30 dk (günde 3; yalnız 1 dk'dan uzun işlerde).
        bool adOption = cost > 0 && AdRewards.SpeedupAdsLeft > 0;
        // Kereste Sevkiyatı: ada Odun'u (100) inşaatı 10 dk kısaltır, günde 3; Odun yetmezse düğme soluk görünür.
        bool lumberOption = cost > 0 && IslandEconomy.LumberShipmentOffered;
        string lumberLabel = !lumberOption ? null
            : L.T($"KERESTE: {GemEconomy.LumberShipmentWood} ODUN → −{GemEconomy.LumberShipmentMinutes} DK ({IslandEconomy.LumberShipmentsLeft})",
                $"LUMBER: {GemEconomy.LumberShipmentWood} WOOD → −{GemEconomy.LumberShipmentMinutes} MIN ({IslandEconomy.LumberShipmentsLeft})");
        GemConfirm.Show(L.T("HEMEN BİTİR", "FINISH NOW"),
            $"{what}\n{L.T("Kalan süre", "Time left")}: {Progression.FormatDuration(GameManager.SecondsRemaining(task))}",
            cost, L.T("BİTİR", "FINISH"), () =>
            {
                if (!gm.FinishTaskNow(task)) { Toast(L.T("Bitirilemedi: elmas yetmiyor", "Not enough gems")); return; }
                AudioManager.Instance?.PlaySfx("levelUp");
                Toast(cost > 0 ? L.T($"Bitti! −{cost} elmas", $"Done! −{cost} gems") : L.T("Bitti!", "Done!"));
                after?.Invoke();
            }, null, false,
            adOption ? L.T($"REKLAM: −{GemEconomy.AdSpeedupMinutes} DK ({AdRewards.SpeedupAdsLeft} hak)",
                $"AD: −{GemEconomy.AdSpeedupMinutes} MIN ({AdRewards.SpeedupAdsLeft} left)") : null,
            () => RewardedAds.Show("builder_speedup", ok =>
            {
                if (!ok || !AdRewards.RewardSpeedup(task)) return;
                Toast(L.T($"İnşaat {GemEconomy.AdSpeedupMinutes} dk kısaldı!",
                    $"Construction {GemEconomy.AdSpeedupMinutes} min faster!"));
                after?.Invoke();
            }),
            lumberLabel,
            () =>
            {
                if (!IslandEconomy.TryShipLumber(task))
                {
                    Toast(L.T($"Odun yetmiyor ({IslandEconomy.Wood}/{GemEconomy.LumberShipmentWood}): Martı Adası Oduncusu",
                        $"Not enough Wood ({IslandEconomy.Wood}/{GemEconomy.LumberShipmentWood}): Gull Isle Lumber Mill"));
                    return;
                }
                AudioManager.Instance?.PlaySfx("buildingPlace");
                Toast(L.T($"Kereste geldi: inşaat {GemEconomy.LumberShipmentMinutes} dk kısaldı!",
                    $"Lumber delivered: construction {GemEconomy.LumberShipmentMinutes} min faster!"));
                after?.Invoke();
            },
            IslandEconomy.CanShipLumber);
    }

    public static void OfferPotion(Action after = null)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.BuildPotionActive)
        {
            Toast(L.T("İnşaat İksiri zaten etkin: ", "Construction Potion already active: ") + GemEconomy.FormatRemaining(gm.PlayerData.buildPotionUntilTicks));
            return;
        }
        GemConfirm.Show(L.T("İNŞAAT İKSİRİ", "CONSTRUCTION POTION"),
            L.T($"{GemEconomy.PotionHours} saat boyunca bütün inşaatçılar {GemEconomy.PotionSpeed} kat hızlı çalışır. Süren işler de hızlanır, oyun kapalıyken de işler.",
                $"For {GemEconomy.PotionHours} hours all builders work {GemEconomy.PotionSpeed}x faster. Running jobs speed up too, even while offline."),
            GemEconomy.PotionCost, L.T("İÇ", "DRINK"), () =>
            {
                if (!gm.ActivateBuildPotion()) return;
                AudioManager.Instance?.PlaySfx("levelUp");
                Toast(L.T($"İnşaat İksiri etkin: {GemEconomy.PotionHours} saat {GemEconomy.PotionSpeed} kat hız!", $"Construction Potion active: {GemEconomy.PotionSpeed}x speed for {GemEconomy.PotionHours} hours!"));
                after?.Invoke();
            });
    }

    public static void OfferTempBuilder(Action after = null)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.TempBuilderActive)
        {
            Toast(L.T("Misafir inşaatçı zaten burada: ", "Guest builder already here: ") + GemEconomy.FormatRemaining(gm.PlayerData.tempBuilderUntilTicks));
            return;
        }
        if (!gm.CanRentTempBuilder)
        {
            Toast(L.T("Bütün inşaatçılar kiralandı: misafire gerek yok", "All builders hired: no guest needed"));
            return;
        }
        GemConfirm.Show(L.T("MİSAFİR İNŞAATÇI", "GUEST BUILDER"),
            L.T($"{GemEconomy.TempBuilderHours} saat boyunca bir inşaatçı daha. Süre biterken elindeki işi yine de bitirir.", $"One more builder for {GemEconomy.TempBuilderHours} hours. They still finish their current job when time runs out."),
            GemEconomy.TempBuilderCost, L.T("KİRALA", "HIRE"), () =>
            {
                if (!gm.RentTempBuilder()) return;
                AudioManager.Instance?.PlaySfx("levelUp");
                Toast(L.T($"Misafir inşaatçı geldi: {GemEconomy.TempBuilderHours} saat yanında", $"Guest builder arrived for {GemEconomy.TempBuilderHours} hours"));
                after?.Invoke();
            });
    }

    /// <summary>
    /// Ada (Odun/Et) için ORTAK eksik-kaynak yolu (BUGLAR 22): kaynak yetiyorsa <paramref name="start"/>(false) doğrudan çalışır;
    /// eksikse elmas bedeli (GemEconomy) onay penceresinde gösterilir ve elmas YALNIZ onaydan sonra düşer (start(true)).
    /// </summary>
    public static void OfferIslandTopUp(int wood, int meat, string what, Func<bool, bool> start, Action after = null)
    {
        if (IslandEconomy.CanSpend(wood, meat)) { if (start(false)) after?.Invoke(); return; }
        int missingWood = Mathf.Max(0, wood - IslandEconomy.Wood), missingMeat = Mathf.Max(0, meat - IslandEconomy.Meat);
        int cost = IslandEconomy.ShortfallGems(wood, meat);
        string tr = missingWood > 0 && missingMeat > 0 ? $"{missingWood} Odun, {missingMeat} Et" : missingWood > 0 ? $"{missingWood} Odun" : $"{missingMeat} Et";
        string en = missingWood > 0 && missingMeat > 0 ? $"{missingWood} Wood, {missingMeat} Meat" : missingWood > 0 ? $"{missingWood} Wood" : $"{missingMeat} Meat";
        GemConfirm.Show(L.T("KAYNAK EKSİK", "NOT ENOUGH RESOURCES"),
            L.T($"{what} için {tr} eksik.\nElmasla tamamlayıp hemen başlatalım mı?", $"{what} needs {en} more.\nTop up with gems and start now?"),
            cost, L.T("TAMAMLA", "TOP UP"), () => { if (start(true)) after?.Invoke(); });
    }

    /// <summary>Altın yetmeyen bir işi elmasla tamamlayıp başlatma teklifi.</summary>
    public static void OfferGoldTopUp(int neededGold, string what, Func<bool> start, Action after = null)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int missing = neededGold - gm.Gold;
        if (missing <= 0) { if (start()) after?.Invoke(); return; }
        int cost = GemEconomy.GoldTopUpCost(missing);
        GemConfirm.Show(L.T("ALTIN EKSİK", "NOT ENOUGH GOLD"),
            L.T($"{what} için {missing} altın eksik.\nElmasla tamamlayıp hemen başlatalım mı?", $"{what} needs {missing} more gold.\nTop up with gems and start now?"),
            cost, L.T("TAMAMLA", "TOP UP"), () =>
            {
                if (!gm.TopUpGold(neededGold)) return;
                if (start()) after?.Invoke();
            });
    }
}
