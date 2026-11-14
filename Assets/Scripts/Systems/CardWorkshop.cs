using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kart Atölyesi mantığı (Docs/design/card_workshop.md): Et + altınla savaş kartı üretimi, üretim kuyruğu, stok (kalan hak),
/// süre / reklam / elmasla bitirme. Saf mantık: <see cref="WorkshopSaveData"/> üzerinde çalışır, sahne gerektirmez.
/// Et, ada cüzdanından (<see cref="IslandEconomy"/>) harcanır; eksik Et elmasla tamamlanabilir (<see cref="GemEconomy.MeatTopUpCost"/>).
/// Süre <see cref="GameClock"/> tabanlıdır: oyun kapalıyken biten üretimler <see cref="Tick"/> ile stoğa girer.
/// Bütün sayılar yalnız <see cref="Progression.Workshop"/> ve <see cref="GemEconomy"/>'dedir.
/// </summary>
public static class CardWorkshop
{
    public static event Action OnChanged;
    /// <summary>Bir üretim bitti (kart, eklenen hak).</summary>
    public static event Action<BattleCard, int> Completed;

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    private static WorkshopSaveData Save
    {
        get
        {
            var data = Data;
            if (data == null) return null;
            if (data.workshop == null) data.workshop = new WorkshopSaveData();
            if (data.workshop.stock == null || data.workshop.stock.Length < BattleCards.Count) data.workshop.Repair();
            return data.workshop;
        }
    }

    private static long Now => GameClock.UtcNow.Ticks;

    // ==================== BİNA / SEVİYE ====================

    /// <summary>Atölye seviyesi: sahnedeki (kayıtlı) Kart Atölyesi'nin seviyesi; kurulu değilse 0.</summary>
    public static int Level
    {
        get
        {
            return BuildingLevels.HighestCompleted(Data, BuildingType.CardWorkshop);   // 6. oturum (1.1)
        }
    }

    public static bool Built => Level >= 1;
    public static int UsesPerCraft => Progression.Workshop.UsesPerCraft(Level);
    public static int QueueSlots => Progression.Workshop.QueueSlots(Level);
    public static bool IsTierUnlocked(CardTier tier) => Built && Level >= Progression.Workshop.UnlockLevel((int)tier);
    public static bool IsUnlocked(BattleCard card) => IsTierUnlocked(BattleCards.Tier(card));

    // ==================== STOK ====================

    public static int Stock(BattleCard card)
    {
        var save = Save;
        return save == null ? 0 : save.stock[(int)card];
    }

    public static int StockCap(BattleCard card) => Progression.Workshop.StockCap((int)BattleCards.Tier(card));

    /// <summary>Kuyrukta bekleyen (henüz stoğa girmemiş) haklar.</summary>
    public static int QueuedUses(BattleCard card)
    {
        var save = Save;
        int n = 0;
        if (save != null) foreach (var job in save.queue) if (job.card == (int)card) n += job.uses;
        return n;
    }

    /// <summary>Stoğa hak ekler (tavana kadar). Eklenen miktarı döndürür.</summary>
    public static int AddStock(BattleCard card, int uses)
    {
        var save = Save;
        if (save == null || uses <= 0) return 0;
        int room = Mathf.Max(0, StockCap(card) - save.stock[(int)card]);
        int add = Mathf.Min(room, uses);
        save.stock[(int)card] += add;
        return add;
    }

    /// <summary>Bir savaş için 1 hak düşer (<see cref="BattleCards.BeginBattle"/>). Hak yoksa false.</summary>
    public static bool ConsumeUse(BattleCard card)
    {
        var save = Save;
        if (save == null || save.stock[(int)card] <= 0) return false;
        save.stock[(int)card]--;
        GameManager.Instance?.SaveGame();
        OnChanged?.Invoke();
        return true;
    }

    // ==================== BEDEL ====================

    public static bool FirstCraftFree(BattleCard card) =>
        Save != null && !Save.firstFreeUsed && BattleCards.Tier(card) == CardTier.Common;

    public static int MeatCost(BattleCard card) =>
        FirstCraftFree(card) ? 0 : Progression.Workshop.Meat((int)BattleCards.Tier(card), Mathf.Max(1, Level));

    public static int GoldCost(BattleCard card) =>
        FirstCraftFree(card) ? 0 : Progression.Workshop.Gold((int)BattleCards.Tier(card), Mathf.Max(1, Level));

    public static int CraftSeconds(BattleCard card) =>
        FirstCraftFree(card) ? Progression.Workshop.FirstCraftSeconds : Progression.Workshop.Seconds((int)BattleCards.Tier(card));

    /// <summary>Üretim başlayamazsa nedeni (kaynak eksikliği hariç); başlayabilirse null.</summary>
    public static string CraftBlockReason(BattleCard card)
    {
        var save = Save;
        if (save == null) return L.T("Kayıt yok", "No save");
        if (!Built) return L.T("Kart Atölyesi kurulu değil", "Card Workshop is not built");
        var tier = BattleCards.Tier(card);
        if (!IsTierUnlocked(tier))
            return L.T($"Atölye Sv {Progression.Workshop.UnlockLevel((int)tier)} gerekir", $"Needs Workshop Lv {Progression.Workshop.UnlockLevel((int)tier)}");
        if (save.queue.Count >= QueueSlots) return L.T("Üretim kuyruğu dolu", "Production queue is full");
        if (save.stock[(int)card] + QueuedUses(card) + UsesPerCraft > StockCap(card))
            return L.T("Depo dolu", "Storage full");
        return null;
    }

    public static int GoldShortfall(BattleCard card) => Mathf.Max(0, GoldCost(card) - (GameManager.Instance?.Gold ?? 0));
    public static int MeatShortfall(BattleCard card) => Mathf.Max(0, MeatCost(card) - IslandEconomy.Meat);
    public static int MeatShortfallGems(BattleCard card) => GemEconomy.MeatTopUpCost(MeatShortfall(card));

    /// <summary>
    /// Üretimi başlatır: altın Ana Üs cüzdanından, Et ada cüzdanından (useGems: eksik Et elmasla tamamlanır). Hiçbir şey eksikse
    /// kısmen harcamaz. Eksik altın bu fonksiyonda tamamlanmaz (arayüz <see cref="GemActions.OfferGoldTopUp"/> ile önce tamamlar).
    /// </summary>
    public static bool TryStartCraft(BattleCard card, bool useGems, out string reason)
    {
        reason = CraftBlockReason(card);
        if (reason != null) return false;
        var gm = GameManager.Instance;
        int meat = MeatCost(card), gold = GoldCost(card);
        if (gm.Gold < gold) { reason = L.T("Altın yetersiz", "Not enough gold"); return false; }
        bool free = FirstCraftFree(card);
        if (meat > 0 && !IslandEconomy.TrySpendWithGems(0, meat, useGems))
        { reason = L.T("Et yetersiz (Martı Adası Ağılı)", "Not enough Meat (Gull Isle Pasture)"); return false; }
        if (gold > 0) gm.SpendGold(gold);
        var save = Save;
        long now = Now;
        save.queue.Add(new WorkshopJob
        {
            card = (int)card, startTicks = now, endTicks = now + CraftSeconds(card) * TimeSpan.TicksPerSecond, uses = UsesPerCraft,
        });
        if (free) save.firstFreeUsed = true;
        gm.SaveGame();
        OnChanged?.Invoke();
        return true;
    }

    // ==================== KUYRUK / TAMAMLAMA ====================

    public static IReadOnlyList<WorkshopJob> Queue => Save != null ? (IReadOnlyList<WorkshopJob>)Save.queue : Array.Empty<WorkshopJob>();

    public static double SecondsLeft(WorkshopJob job) => job == null ? 0d : Math.Max(0d, new TimeSpan(job.endTicks - Now).TotalSeconds);

    public static float Progress01(WorkshopJob job)
    {
        if (job == null || job.endTicks <= job.startTicks) return 1f;
        return Mathf.Clamp01((float)((Now - job.startTicks) / (double)(job.endTicks - job.startTicks)));
    }

    public static void TickIfAny()
    {
        var save = Save;
        if (save != null && save.queue.Count > 0) Tick();
    }

    /// <summary>Süresi dolan üretimleri stoğa işler (çevrimdışı dahil: bitiş anı GameClock'a göre). Tamamlanan sayıyı döndürür.</summary>
    public static int Tick()
    {
        var save = Save;
        if (save == null) return 0;
        long now = Now;
        List<WorkshopJob> done = null;
        foreach (var job in save.queue) if (job.endTicks <= now) (done ??= new List<WorkshopJob>()).Add(job);
        if (done == null) return 0;
        foreach (var job in done)
        {
            save.queue.Remove(job);
            int added = AddStock((BattleCard)job.card, job.uses);
            Completed?.Invoke((BattleCard)job.card, added);
            GemActions.Toast(L.T($"Kart hazır: {BattleCards.Name((BattleCard)job.card)} ×{added}", $"Card ready: {BattleCards.Name((BattleCard)job.card)} x{added}"));
        }
        GameManager.Instance?.SaveGame();
        OnChanged?.Invoke();
        return done.Count;
    }

    /// <summary>Ödüllü reklam izlendi: üretim anında biter (kademe fark etmez; günlük atölye reklam hakkı gerekir).</summary>
    public static bool TryFinishByAd(int index)
    {
        var save = Save;
        if (save == null || index < 0 || index >= save.queue.Count) return false;
        if (!AdRewards.ConsumeWorkshopAd()) return false;
        save.queue[index].endTicks = Now;
        Tick();
        return true;
    }

    /// <summary>Kalan süre için elmas bedeli (<see cref="GemEconomy.FinishCost"/>; 1 dk altı bedava).</summary>
    public static int FinishGemCost(int index) =>
        index < 0 || Save == null || index >= Save.queue.Count ? 0 : GemEconomy.FinishCost(SecondsLeft(Save.queue[index]));

    public static bool TryFinishWithGems(int index)
    {
        var save = Save;
        if (save == null || index < 0 || index >= save.queue.Count) return false;
        int cost = FinishGemCost(index);
        if (cost > 0 && (GameManager.Instance == null || !GameManager.Instance.SpendGems(cost))) return false;
        save.queue[index].endTicks = Now;
        Tick();
        return true;
    }

    /// <summary>Geliştirici menüsü: bütün üretimleri hemen bitirir.</summary>
    public static void DevFinishAll()
    {
        var save = Save;
        if (save == null) return;
        foreach (var job in save.queue) job.endTicks = Now;
        Tick();
    }
}
