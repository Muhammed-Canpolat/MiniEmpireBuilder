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
        GemConfirm.Show(L.T("HEMEN BİTİR", "FINISH NOW"),
            $"{what}\n{L.T("Kalan süre", "Time left")}: {Progression.FormatDuration(GameManager.SecondsRemaining(task))}",
            cost, L.T("BİTİR", "FINISH"), () =>
            {
                if (!gm.FinishTaskNow(task)) { Toast(L.T("Bitirilemedi: elmas yetmiyor", "Not enough gems")); return; }
                AudioManager.Instance?.PlaySfx("levelUp");
                Toast(cost > 0 ? L.T($"Bitti! −{cost} elmas", $"Done! −{cost} gems") : L.T("Bitti!", "Done!"));
                after?.Invoke();
            }, null, false,
            adOption ? L.T($"▶ REKLAM: −{GemEconomy.AdSpeedupMinutes} DK ({AdRewards.SpeedupAdsLeft} hak)",
                $"▶ AD: −{GemEconomy.AdSpeedupMinutes} MIN ({AdRewards.SpeedupAdsLeft} left)") : null,
            () => RewardedAds.Show("builder_speedup", ok =>
            {
                if (!ok || !AdRewards.RewardSpeedup(task)) return;
                Toast(L.T($"İnşaat {GemEconomy.AdSpeedupMinutes} dk kısaldı!",
                    $"Construction {GemEconomy.AdSpeedupMinutes} min faster!"));
                after?.Invoke();
            }));
    }

    public static void OfferPotion(Action after = null)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.BuildPotionActive)
        {
            Toast($"İnşaat İksiri zaten etkin: {GemEconomy.FormatRemaining(gm.PlayerData.buildPotionUntilTicks)} kaldı");
            return;
        }
        GemConfirm.Show("İNŞAAT İKSİRİ",
            $"{GemEconomy.PotionHours} saat boyunca bütün inşaatçılar {GemEconomy.PotionSpeed} kat hızlı çalışır. " +
            "Süren işler de hızlanır, oyun kapalıyken de işler.",
            GemEconomy.PotionCost, "İÇ", () =>
            {
                if (!gm.ActivateBuildPotion()) return;
                AudioManager.Instance?.PlaySfx("levelUp");
                Toast($"İnşaat İksiri etkin: {GemEconomy.PotionHours} saat {GemEconomy.PotionSpeed} kat hız!");
                after?.Invoke();
            });
    }

    public static void OfferTempBuilder(Action after = null)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.TempBuilderActive)
        {
            Toast($"Misafir inşaatçı zaten burada: {GemEconomy.FormatRemaining(gm.PlayerData.tempBuilderUntilTicks)} kaldı");
            return;
        }
        if (!gm.CanRentTempBuilder)
        {
            Toast("Bütün inşaatçılar kiralandı: misafire gerek yok");
            return;
        }
        GemConfirm.Show("MİSAFİR İNŞAATÇI",
            $"{GemEconomy.TempBuilderHours} saat boyunca bir inşaatçı daha. Süre biterken elindeki işi yine de bitirir.",
            GemEconomy.TempBuilderCost, "KİRALA", () =>
            {
                if (!gm.RentTempBuilder()) return;
                AudioManager.Instance?.PlaySfx("levelUp");
                Toast($"Misafir inşaatçı geldi: {GemEconomy.TempBuilderHours} saat yanında");
                after?.Invoke();
            });
    }

    /// <summary>Altın yetmeyen bir işi elmasla tamamlayıp başlatma teklifi.</summary>
    public static void OfferGoldTopUp(int neededGold, string what, Func<bool> start, Action after = null)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int missing = neededGold - gm.Gold;
        if (missing <= 0) { if (start()) after?.Invoke(); return; }
        int cost = GemEconomy.GoldTopUpCost(missing);
        GemConfirm.Show("ALTIN EKSİK",
            $"{what} için {missing} altın eksik.\nElmasla tamamlayıp hemen başlatalım mı?",
            cost, "TAMAMLA", () =>
            {
                if (!gm.TopUpGold(neededGold)) return;
                if (start()) after?.Invoke();
            });
    }
}
