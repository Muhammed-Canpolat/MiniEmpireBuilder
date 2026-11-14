using System;

/// <summary>
/// Savaş kartları. Sıradan 3 (temel) kart: +%10 savaşçı hasarı, +%10 savaşçı canı, +%10 kule ve Ana Üs canı. Yeni dizin sırası hep
/// sona eklenir (kayıt stoğu dizindir). Nadir 3 + Destansı 1 "özel" kart yalnız Kart Atölyesi stoğuyla açılır.
/// Temel kart kaynakları (Docs/design/card_workshop.md §6): Kraliyet (bedava, harcamaz) → ilk savaş bedava → atölye stoğu → kart jetonu →
/// reklam / 5 elmas. 1 savaş = 1 hak: atölye hakkı <see cref="BeginBattle"/> anında, sonuçtan bağımsız (yenilgide de) düşer.
/// Eşzamansız rekabet modlarında kartlar kapalıdır: <see cref="Competitive"/>.
/// </summary>
public enum BattleCard { HeroDamage, HeroHealth, Fortify, SharpVolleys, ArmorBreaker, SkyHunter, WardBreaker }

public enum CardTier { Common, Rare, Epic }

public static class BattleCards
{
    public const float AdBonus = 0.10f;
    public const float RoyalBonus = 0.15f;
    public const int Count = 7;
    /// <summary>Sıradan temel kartlar (UI döngüleri bunu kullanır; özel kartlar atölye satırındadır).</summary>
    public const int BasicCount = 3;

    // Özel kart etkileri (denge sayıları kart tasarımıdır; üretim bedeli/süre/hak yalnız Progression.Workshop'tadır).
    public const float SharpVolleysBonus = 0.15f;     // tüm kule hasarı
    public const float ArmorBreakerMult = 1.4f;       // kulelerin Zırhlı'ya hasarı
    public const float SkyHunterBonus = 0.5f;         // Okçu kulelerinin Uçan'a hasarı
    public const float WardPoolCut = 0.30f;           // Boss Kalkanı havuzu −%30
    public const float WardExposedExtraSeconds = 2f;  // açık kalma süresi 6 → 8 sn

    public static event Action OnChanged;

    private static readonly bool[] unlocked = new bool[Count];
    private static readonly bool[] inBattle = new bool[Count];
    private static readonly bool[] fromStock = new bool[Count];
    private static bool freeChosen;
    private static float battleBonus;

    /// <summary>Eşzamansız rekabet (Düello, skor tablosu) savaşında kartlar etkisizdir.</summary>
    public static bool Competitive { get; set; }

    private static PlayerSaveData Data => GameManager.Instance != null ? GameManager.Instance.PlayerData : null;

    public static bool IsRoyal => Data != null && Data.royalUntilTicks > DateTime.UtcNow.Ticks;
    public static float Bonus => IsRoyal ? RoyalBonus : AdBonus;
    public static int BonusPercent => (int)Math.Round(Bonus * 100f);

    public static bool IsBasic(BattleCard card) => (int)card < BasicCount;

    public static CardTier Tier(BattleCard card) =>
        IsBasic(card) ? CardTier.Common : card == BattleCard.WardBreaker ? CardTier.Epic : CardTier.Rare;

    public static string TierName(CardTier tier) =>
        tier == CardTier.Common ? L.T("Sıradan", "Common") : tier == CardTier.Rare ? L.T("Nadir", "Rare") : L.T("Destansı", "Epic");

    /// <summary>Bir sonraki savaş için açık mı (Kraliyet'te yalnız 3 temel kart açıktır).</summary>
    public static bool IsUnlocked(BattleCard card) => (IsRoyal && IsBasic(card)) || unlocked[(int)card];

    /// <summary>Bu kart seçimi atölye stoğundan mı (savaş başlayınca bir hak düşer).</summary>
    public static bool ChosenFromStock(BattleCard card) => unlocked[(int)card] && fromStock[(int)card];

    /// <summary>İlk savaşa özel bedava kart hakkı (henüz kullanılmadı ve seçilmedi).</summary>
    public static bool FreeCardAvailable => Data != null && !Data.freeCardUsed && !freeChosen && !IsRoyal;

    public static int Tokens => Data?.cardTokens ?? 0;

    public static string Name(BattleCard card)
    {
        switch (card)
        {
            case BattleCard.HeroDamage: return L.T("Hasar", "Damage");
            case BattleCard.HeroHealth: return L.T("Can", "Health");
            case BattleCard.Fortify: return L.T("Kale", "Fort");
            case BattleCard.SharpVolleys: return L.T("Keskin Oklar", "Sharp Volleys");
            case BattleCard.ArmorBreaker: return L.T("Zırh Delici", "Armor Breaker");
            case BattleCard.SkyHunter: return L.T("Gök Avcısı", "Sky Hunter");
            default: return L.T("Kalkan Kıran", "Ward Breaker");
        }
    }

    public static string Description(BattleCard card)
    {
        switch (card)
        {
            case BattleCard.HeroDamage: return L.T("Savaşçı hasarı", "Warrior damage");
            case BattleCard.HeroHealth: return L.T("Savaşçı canı", "Warrior health");
            case BattleCard.Fortify: return L.T("Kule ve Ana Üs canı", "Tower and Main Base health");
            case BattleCard.SharpVolleys: return L.T("Tüm kule hasarı +%15", "All tower damage +15%");
            case BattleCard.ArmorBreaker: return L.T("Kuleler Zırhlı'ya ×1,4 hasar verir", "Towers deal x1.4 damage to Armored");
            case BattleCard.SkyHunter: return L.T("Okçu kuleleri Uçan'a +%50 hasar verir", "Archer towers deal +50% damage to Flying");
            default: return L.T("Boss Kalkanı −%30, açık kalma 6 → 8 sn", "Boss Ward pool -30%, exposed 6 -> 8 s");
        }
    }

    /// <summary>Kart simgesi (Resources/CampaignArt içindeki Tiny Swords arayüz simgeleri).</summary>
    public static string IconName(BattleCard card)
    {
        switch (card)
        {
            case BattleCard.HeroDamage: return "ui_icon_sword";
            case BattleCard.HeroHealth: return "ui_icon_heart";
            case BattleCard.Fortify: return "ui_icon_shield";
            case BattleCard.SharpVolleys: return "ui_icon_arrow";   // Tiny Swords Arrow (Units/Extra/Arrow), 45° döndürülmüş
            case BattleCard.ArmorBreaker: return "ui_icon_hammer";
            case BattleCard.SkyHunter: return "ui_icon_archer";     // Tiny Swords Human Avatars_03 (başlıklı okçu/avcı)
            default: return "ui_icon_cross";   // Kalkan Kıran: tek başına çarpı; ApplyIcon kırık kalkan olarak birleştirir
        }
    }

    /// <summary>Kart simgesini verilen Image'a koyar. Kalkan Kıran: kalkan simgesi + üstüne kırmızı çarpı (çocuk Image "BrokenCross"), dosya gerekmez.</summary>
    public static void ApplyIcon(UnityEngine.UI.Image image, BattleCard card)
    {
        if (image == null) return;
        bool broken = card == BattleCard.WardBreaker;
        image.sprite = CampaignSceneSetup.Art(broken ? "ui_icon_shield" : IconName(card));
        image.preserveAspect = true;
        image.enabled = image.sprite != null;
        var old = image.transform.Find("BrokenCross");
        if (old != null) UnityEngine.Object.Destroy(old.gameObject);
        if (!broken) return;
        var cross = new UnityEngine.GameObject("BrokenCross", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
        var rt = (UnityEngine.RectTransform)cross.transform;
        rt.SetParent(image.transform, false);
        rt.anchorMin = new UnityEngine.Vector2(0.28f, 0.0f); rt.anchorMax = new UnityEngine.Vector2(1.0f, 0.72f);
        rt.offsetMin = rt.offsetMax = UnityEngine.Vector2.zero;
        var ci = cross.GetComponent<UnityEngine.UI.Image>();
        ci.sprite = CampaignSceneSetup.Art("ui_icon_cross");
        ci.preserveAspect = true;
        ci.raycastTarget = false;
        ci.enabled = ci.sprite != null;
    }

    // ==================== SEÇİM (savaş öncesi) ====================

    /// <summary>Kraliyet/ilk savaş bedava/atölye stoğu/jeton ile reklamsız açar (sırayla). Hiçbiri yoksa false (reklam gerekir).</summary>
    public static bool TryUnlockWithoutAd(BattleCard card)
    {
        if (!IsBasic(card)) return false;
        if (IsUnlocked(card)) return true;
        if (FreeCardAvailable)
        {
            freeChosen = true;
            Set(card, false);
            return true;
        }
        if (CardWorkshop.Stock(card) > 0)
        {
            Set(card, true);
            return true;
        }
        if (Tokens > 0)
        {
            Data.cardTokens--;
            GameManager.Instance.SaveGame();
            Set(card, false);
            return true;
        }
        return false;
    }

    /// <summary>Ödüllü reklam izlendi: temel kart açılır.</summary>
    public static void UnlockFromAd(BattleCard card) => Set(card, false);

    /// <summary>Reklamsız: temel kart elmasla açılır (<see cref="GemEconomy.BattleCardCost"/>).</summary>
    public static bool UnlockWithGems(BattleCard card)
    {
        if (IsUnlocked(card)) return true;
        var gm = GameManager.Instance;
        if (gm == null || !gm.SpendGems(GemEconomy.BattleCardCost)) return false;
        Set(card, false);
        return true;
    }

    /// <summary>Özel (Nadir/Destansı) kart seçimi: atölye stoğu, kademe açılışı, yuva sayısı ve savaş başına 1 Destansı sınırı.</summary>
    public static bool TrySelectSpecial(BattleCard card, out string reason)
    {
        reason = null;
        if (IsBasic(card)) { reason = L.T("Temel kart", "Basic card"); return false; }
        if (unlocked[(int)card]) return true;
        int level = CardWorkshop.Level;
        if (level < Progression.Workshop.UnlockLevel((int)Tier(card)))
        { reason = L.T($"Atölye Sv {Progression.Workshop.UnlockLevel((int)Tier(card))} gerekir", $"Needs Workshop Lv {Progression.Workshop.UnlockLevel((int)Tier(card))}"); return false; }
        if (CardWorkshop.Stock(card) <= 0) { reason = L.T("Stokta hak yok: atölyede üret", "No uses left: craft it in the workshop"); return false; }
        int chosen = 0, epics = 0;
        for (int i = BasicCount; i < Count; i++)
            if (unlocked[i]) { chosen++; if (Tier((BattleCard)i) == CardTier.Epic) epics++; }
        if (chosen >= Progression.Workshop.SpecialSlots(level)) { reason = L.T("Özel kart yuvaları dolu", "Special card slots are full"); return false; }
        if (Tier(card) == CardTier.Epic && epics >= Progression.Workshop.MaxEpicPerBattle) { reason = L.T("Savaşta en çok 1 Destansı", "Max 1 Epic per battle"); return false; }
        Set(card, true);
        return true;
    }

    /// <summary>Atölye stoğundan seçilmiş kartı bırakır (hak henüz düşmedi, savaş başlayınca düşer).</summary>
    public static bool Deselect(BattleCard card)
    {
        if (!unlocked[(int)card] || !fromStock[(int)card]) return false;
        unlocked[(int)card] = false;
        fromStock[(int)card] = false;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Atölye özel yuvası sayacı (seçilen özel kart sayısı).</summary>
    public static int SpecialsChosen
    {
        get { int n = 0; for (int i = BasicCount; i < Count; i++) if (unlocked[i]) n++; return n; }
    }

    private static void Set(BattleCard card, bool stock)
    {
        unlocked[(int)card] = true;
        fromStock[(int)card] = stock;
        OnChanged?.Invoke();
    }

    // ==================== SAVAŞ ====================

    /// <summary>Bu aşamada kalkanlı boss var mı (Kalkan Kıran yalnız o zaman işe yarar ve hak harcar).</summary>
    public static bool StageHasWard
    {
        get
        {
            var level = BattleSession.Level;
            return level != null && level.kind == CampaignLevelKind.Boss;
        }
    }

    /// <summary>
    /// Savaş başlarken açık kartlar savaşa geçer. Atölye hakkı burada düşer (zafer/yenilgiden bağımsız); Kraliyet'in kapsadığı temel
    /// kartlar, rekabet modu ve kalkansız aşamadaki Kalkan Kıran hak harcamaz. Bedava hak kullanılmışsa kaydedilir.
    /// </summary>
    public static void BeginBattle()
    {
        battleBonus = Competitive ? 0f : Bonus;
        bool royal = IsRoyal;
        for (int i = 0; i < Count; i++)
        {
            var card = (BattleCard)i;
            bool basic = IsBasic(card);
            bool active = !Competitive && ((royal && basic) || unlocked[i]);
            if (active && !basic && card == BattleCard.WardBreaker && !StageHasWard) active = false;
            // Hak: yalnız atölyeden seçilen kart etkinse; Kraliyet açıkken temel kart harcanmaz.
            if (active && unlocked[i] && fromStock[i] && !(royal && basic))
                if (!CardWorkshop.ConsumeUse(card)) active = false;
            inBattle[i] = active;
            unlocked[i] = false;
            fromStock[i] = false;
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

    /// <summary>Temel kartın çarpanı (hasar/can/kale). Özel kartlar için 1.</summary>
    public static float Multiplier(BattleCard card) => IsBasic(card) && ActiveInBattle(card) ? 1f + battleBonus : 1f;

    public static int ActiveBattlePercent => (int)Math.Round(battleBonus * 100f);

    private static float Cap(float multiplier) => Math.Min(multiplier, 1f + Progression.Workshop.CardStackCap);

    /// <summary>
    /// Kulenin (Okçu/Topçu) düşmana vuruş çarpanı: Keskin Oklar ×1,15 · Zırh Delici ×1,4 (Zırhlı) · Gök Avcısı ×1,5 (Okçu→Uçan).
    /// Toplam çarpan ×1,5'i aşamaz (<see cref="Progression.Workshop.CardStackCap"/>). Diğer kaynaklarda (kahraman, ada) 1.
    /// </summary>
    public static float TowerHitMultiplier(DamageSource source, EnemyVariant variant)
    {
        if (source != DamageSource.Archer && source != DamageSource.Cannon) return 1f;
        float m = 1f;
        if (ActiveInBattle(BattleCard.SharpVolleys)) m *= 1f + SharpVolleysBonus;
        if (variant == EnemyVariant.Armored && ActiveInBattle(BattleCard.ArmorBreaker)) m *= ArmorBreakerMult;
        if (variant == EnemyVariant.Flying && source == DamageSource.Archer && ActiveInBattle(BattleCard.SkyHunter)) m *= 1f + SkyHunterBonus;
        return Cap(m);
    }

    /// <summary>Boss Kalkanı havuz çarpanı (Kalkan Kıran: 0,7).</summary>
    public static float WardPoolMultiplier => ActiveInBattle(BattleCard.WardBreaker) ? 1f - WardPoolCut : 1f;

    /// <summary>Kalkan kırılınca açık kalma süresine eklenen saniye (Kalkan Kıran: +2).</summary>
    public static float WardExposedBonus => ActiveInBattle(BattleCard.WardBreaker) ? WardExposedExtraSeconds : 0f;

    /// <summary>Testler için: tüm durumu sıfırla.</summary>
    public static void ResetForTests()
    {
        for (int i = 0; i < Count; i++) unlocked[i] = inBattle[i] = fromStock[i] = false;
        freeChosen = false;
        battleBonus = 0f;
        Competitive = false;
    }
}
