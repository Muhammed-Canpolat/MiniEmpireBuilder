using System;

public enum BattleCard { HeroDamage, HeroHealth, Fortify }

/// <summary>
/// Savaş öncesi 3 kart: +%10 savaşçı hasarı, +%10 savaşçı canı, +%10 kule ve Ana Üs canı.
/// Her kart bir ödüllü reklamla (ya da kart jetonuyla) yalnız bir sonraki savaş için açılır.
/// İlk savaşta bir kart bedava. Kraliyet Üyeliği: üç kart da reklamsız ve otomatik, her biri %15.
/// Eşzamansız rekabet modlarında (ileride) kartlar kapalıdır: <see cref="Competitive"/>.
/// </summary>
public static class BattleCards
{
    public const float AdBonus = 0.10f;
    public const float RoyalBonus = 0.15f;
    public const int Count = 3;

    public static event Action OnChanged;

    private static readonly bool[] unlocked = new bool[Count];
    private static readonly bool[] inBattle = new bool[Count];
    private static bool freeChosen;
    private static float battleBonus;

    /// <summary>Eşzamansız rekabet (Düello, skor tablosu) savaşında kartlar etkisizdir.</summary>
    public static bool Competitive { get; set; }

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    public static bool IsRoyal => Data != null && Data.royalUntilTicks > DateTime.UtcNow.Ticks;
    public static float Bonus => IsRoyal ? RoyalBonus : AdBonus;
    public static int BonusPercent => (int)Math.Round(Bonus * 100f);

    /// <summary>Bir sonraki savaş için açık mı (Kraliyet'te hepsi açık).</summary>
    public static bool IsUnlocked(BattleCard card) => IsRoyal || unlocked[(int)card];

    /// <summary>İlk savaşa özel bedava kart hakkı (henüz kullanılmadı ve seçilmedi).</summary>
    public static bool FreeCardAvailable => Data != null && !Data.freeCardUsed && !freeChosen && !IsRoyal;

    public static int Tokens => Data?.cardTokens ?? 0;

    public static string Name(BattleCard card)
    {
        switch (card)
        {
            case BattleCard.HeroDamage: return "Hasar";
            case BattleCard.HeroHealth: return "Can";
            default: return "Kale";
        }
    }

    public static string Description(BattleCard card)
    {
        switch (card)
        {
            case BattleCard.HeroDamage: return "Savaşçı hasarı";
            case BattleCard.HeroHealth: return "Savaşçı canı";
            default: return "Kule ve Ana Üs canı";
        }
    }

    /// <summary>Bedava hakkı varsa onu, yoksa jetonu kullanarak açar. Reklam gerekiyorsa false döner.</summary>
    public static bool TryUnlockWithoutAd(BattleCard card)
    {
        if (IsUnlocked(card)) return true;
        if (FreeCardAvailable)
        {
            freeChosen = true;
            Set(card);
            return true;
        }
        if (Tokens > 0)
        {
            Data.cardTokens--;
            GameManager.Instance.SaveGame();
            Set(card);
            return true;
        }
        return false;
    }

    /// <summary>Ödüllü reklam izlendi: kart açılır.</summary>
    public static void UnlockFromAd(BattleCard card) => Set(card);

    private static void Set(BattleCard card)
    {
        unlocked[(int)card] = true;
        OnChanged?.Invoke();
    }

    /// <summary>Savaş başlarken açık kartlar savaşa geçer; bedava hak kullanılmışsa kaydedilir.</summary>
    public static void BeginBattle()
    {
        battleBonus = Competitive ? 0f : Bonus;
        for (int i = 0; i < Count; i++)
        {
            inBattle[i] = !Competitive && (IsRoyal || unlocked[i]);
            unlocked[i] = false;
        }
        if (freeChosen && Data != null)
        {
            Data.freeCardUsed = true;
            GameManager.Instance.SaveGame();
        }
        freeChosen = false;
        OnChanged?.Invoke();
    }

    /// <summary>Savaş bitti: kartlar tek savaşlıktır (tekrar denemede yeniden açılır).</summary>
    public static void EndBattle()
    {
        for (int i = 0; i < Count; i++) inBattle[i] = false;
        battleBonus = 0f;
    }

    public static bool ActiveInBattle(BattleCard card) => inBattle[(int)card];

    public static float Multiplier(BattleCard card) => ActiveInBattle(card) ? 1f + battleBonus : 1f;

    public static int ActiveBattlePercent => (int)Math.Round(battleBonus * 100f);

    /// <summary>Testler için: tüm durumu sıfırla.</summary>
    public static void ResetForTests()
    {
        for (int i = 0; i < Count; i++) unlocked[i] = inBattle[i] = false;
        freeChosen = false;
        battleBonus = 0f;
        Competitive = false;
    }
}
