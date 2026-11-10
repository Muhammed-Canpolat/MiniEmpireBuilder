using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestKind { KillEnemies, WinCampaign, StartUpgrade, CollectMine, SpendGold, EarnStars }

/// <summary>
/// Günlük görevler: her gün tarihe göre (gün içinde sabit) 3 görev seçilir. Görev bitince ödülü
/// "AL" ile alınır; üçü de alınınca +5 elmas bonus. Oyun olayları <see cref="Report"/> ile bildirilir.
/// </summary>
public static class DailyQuests
{
    public const int Count = 3;
    public const int BonusGems = 5;

    public struct Def
    {
        public QuestKind kind;
        public int target;
        public bool gemReward;   // false: altın (Ana Üs seviyesine göre)
        public int gems;
    }

    private static readonly Def[] Pool =
    {
        new Def { kind = QuestKind.KillEnemies, target = 20 },
        new Def { kind = QuestKind.WinCampaign, target = 2, gemReward = true, gems = 2 },
        new Def { kind = QuestKind.StartUpgrade, target = 1 },
        new Def { kind = QuestKind.CollectMine, target = 2 },
        new Def { kind = QuestKind.SpendGold, target = 300, gemReward = true, gems = 2 },
        new Def { kind = QuestKind.EarnStars, target = 2, gemReward = true, gems = 2 },
    };

    public static event Action OnChanged;

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    /// <summary>Testler gün değiştirebilsin diye.</summary>
    public static DateTime? TodayOverride { get; set; }
    private static int Today
    {
        get
        {
            var date = (TodayOverride ?? DateTime.Now).Date;
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }
    }

    /// <summary>Gün değiştiyse yeni 3 görev seçilir (tarih tohumlu: aynı gün hep aynı görevler).</summary>
    private static PlayerSaveData Ensure()
    {
        var data = Data;
        if (data == null) return null;
        if (data.questDay == Today && data.questKinds != null && data.questKinds.Count == Count) return data;
        data.questDay = Today;
        data.questKinds = new List<int>();
        data.questProgress = new List<int>();
        data.questClaimed = new List<bool>();
        data.questBonusClaimed = false;
        var random = new System.Random(Today);
        var indices = new List<int>();
        for (int i = 0; i < Pool.Length; i++) indices.Add(i);
        for (int i = 0; i < Count; i++)
        {
            int pick = random.Next(indices.Count);
            data.questKinds.Add(indices[pick]);
            indices.RemoveAt(pick);
            data.questProgress.Add(0);
            data.questClaimed.Add(false);
        }
        return data;
    }

    public static Def Quest(int index)
    {
        var data = Ensure();
        return data == null ? Pool[0] : Pool[Mathf.Clamp(data.questKinds[index], 0, Pool.Length - 1)];
    }

    public static int Progress(int index)
    {
        var data = Ensure();
        return data == null ? 0 : Mathf.Min(data.questProgress[index], Quest(index).target);
    }

    public static bool IsDone(int index) => Progress(index) >= Quest(index).target;
    public static bool IsClaimed(int index) => Ensure()?.questClaimed[index] ?? false;
    public static bool BonusClaimed => Ensure()?.questBonusClaimed ?? false;

    public static bool AllClaimed
    {
        get
        {
            for (int i = 0; i < Count; i++) if (!IsClaimed(i)) return false;
            return true;
        }
    }

    /// <summary>Alınacak bir ödül var mı (kırmızı nokta için).</summary>
    public static bool HasClaimable
    {
        get
        {
            if (Data == null) return false;
            for (int i = 0; i < Count; i++) if (IsDone(i) && !IsClaimed(i)) return true;
            return AllClaimed && !BonusClaimed;
        }
    }

    public static int GoldReward => 80 + 40 * Mathf.Max(1, GameManager.Instance != null ? GameManager.Instance.GetMainBaseLevel() : 1);

    public static string RewardText(int index)
    {
        var quest = Quest(index);
        return quest.gemReward ? L.T($"{quest.gems} elmas", $"{quest.gems} gems") : L.T($"{GoldReward} altın", $"{GoldReward} gold");
    }

    public static string Title(int index)
    {
        var quest = Quest(index);
        int n = quest.target;
        switch (quest.kind)
        {
            case QuestKind.KillEnemies: return L.T($"{n} düşman yen", $"Defeat {n} enemies");
            case QuestKind.WinCampaign: return L.T($"{n} sefer savaşı kazan", $"Win {n} campaign battles");
            case QuestKind.StartUpgrade: return L.T("Bir bina yükseltmesi başlat", "Start a building upgrade");
            case QuestKind.CollectMine: return L.T($"Madenden {n} kez altın topla", $"Collect gold from the mine {n} times");
            case QuestKind.SpendGold: return L.T($"{n} altın harca", $"Spend {n} gold");
            default: return L.T($"Seferde {n} yeni yıldız kazan", $"Earn {n} new campaign stars");
        }
    }

    /// <summary>Oyun olayı: ilgili görevlerin ilerlemesi artar.</summary>
    public static void Report(QuestKind kind, int amount = 1)
    {
        var data = Ensure();
        if (data == null || amount <= 0) return;
        bool changed = false;
        for (int i = 0; i < Count; i++)
        {
            if (Pool[data.questKinds[i]].kind != kind || data.questClaimed[i]) continue;
            int before = data.questProgress[i];
            data.questProgress[i] = Mathf.Min(Pool[data.questKinds[i]].target, before + amount);
            changed |= data.questProgress[i] != before;
        }
        if (changed) OnChanged?.Invoke();
    }

    /// <summary>Biten görevin ödülünü verir. Verilen ödülün metni; alınamazsa null.</summary>
    public static string Claim(int index)
    {
        var data = Ensure();
        var gm = GameManager.Instance;
        if (data == null || gm == null || !IsDone(index) || IsClaimed(index)) return null;
        var quest = Quest(index);
        string reward = RewardText(index);
        data.questClaimed[index] = true;
        if (quest.gemReward) gm.AddGems(quest.gems, false);
        else gm.AddGold(GoldReward);
        gm.SaveGame();
        OnChanged?.Invoke();
        return reward;
    }

    public static bool ClaimBonus()
    {
        var data = Ensure();
        var gm = GameManager.Instance;
        if (data == null || gm == null || !AllClaimed || data.questBonusClaimed) return false;
        data.questBonusClaimed = true;
        gm.AddGems(BonusGems);
        OnChanged?.Invoke();
        return true;
    }
}
