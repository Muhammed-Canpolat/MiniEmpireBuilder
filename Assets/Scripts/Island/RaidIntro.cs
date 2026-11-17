using System;

/// <summary>
/// D120 (bug 26): ada akın tanıtımı. Saf mantık (sahne yok): sırayla tek baloncuk; her adım bir kez gösterilir ve
/// <see cref="IslandSaveData.raidIntroSeen"/> bit maskesine yazılır. Oyun donmaz; okçu ölüyken baloncuk gizlenir ve süre durur.
/// Bitler: 1 ATEŞ · 2 dolu atış · 4 atış yeri · 8 HARİTA · 16 TAKİP · 32/64/128 yetenek 0/1/2 (Ok Yağmuru/Ateşli Ok/Ağ).
/// </summary>
public sealed class RaidIntro
{
    public const int Fire = 1, Charge = 2, Post = 4, Map = 8, Follow = 16, Ability0 = 32, Ability1 = 64, Ability2 = 128;
    public const int BasicMask = Fire | Charge | Post | Map | Follow;
    public const float ShowSeconds = 7f, GapSeconds = 3f, StartGapSeconds = 2f;
    private static readonly int[] Order = { Fire, Charge, Post, Map, Follow, Ability0, Ability1, Ability2 };

    public struct Context
    {
        public bool Running, ArcherAlive, BoatSeen, TargetInRange, PostStage, FullFrame;
        public int UnlockedAbilities;   // bit i: yetenek i açık
    }

    private IslandSaveData save;
    private float shownFor, gap;
    private bool alive = true;

    public Action Persist;
    /// <summary>Şu an gösterilen adım biti (0 yok).</summary>
    public int Current { get; private set; }
    public int SkipCount { get; private set; }
    public int TimeoutCount { get; private set; }
    public int Seen => save != null ? save.raidIntroSeen : 0;

    /// <summary>Baloncuk metni; okçu ölüyken ya da adım yokken boş.</summary>
    public string Message => Current == 0 || !alive ? "" : TextOf(Current);

    /// <summary>Akın başında: eski kayıt çıkarımı (highestAkin>0 ya da tutorialStep>=3 → ATEŞ + atış yeri; PostHint tercihi → atış yeri).</summary>
    public void Begin(IslandSaveData island, bool postHintShown)
    {
        save = island;
        Current = 0; shownFor = 0f; gap = StartGapSeconds; alive = true;
        if (save == null) return;
        int legacy = 0;
        if (save.highestAkin > 0 || save.tutorialStep >= 3) legacy |= Fire | Post;
        if (postHintShown) legacy |= Post;
        if ((save.raidIntroSeen | legacy) != save.raidIntroSeen) { save.raidIntroSeen |= legacy; Persist?.Invoke(); }
    }

    public void End() { Current = 0; }

    public static string TextOf(int bit)
    {
        switch (bit)
        {
            case Fire: return L.T("Tekne geliyor! ATEŞ'e dokun.", "A boat is coming! Tap FIRE.");
            case Charge: return L.T("Güçlü atış: ATEŞ'i basılı tut, halka dolunca bırak.", "Power shot: hold FIRE, release when the ring fills.");
            case Post: return L.T("Halkalar atış yeri: dokun, okçu yürüyüp oradan savaşır.", "Rings are firing posts: tap one, the archer walks there and fights.");
            case Map: return L.T("HARİTA: tüm adayı gör, gelen tekneleri yakala.", "MAP: see the whole isle, spot incoming boats.");
            case Follow: return L.T("TAKİP: kamera yine okçuyu izler.", "FOLLOW: the camera tracks your archer again.");
            case Ability0: case Ability1: case Ability2:
                int i = bit == Ability0 ? 0 : bit == Ability1 ? 1 : 2;
                var kind = (IslandAbilities.Kind)i;
                return L.T($"Yeni vuruş: {IslandAbilities.NameOf(kind)}! Düğmesine dokun.", $"New shot: {IslandAbilities.NameOf(kind)}! Tap its button.");
            default: return "";
        }
    }

    public void Tick(float dt, Context c)
    {
        alive = c.ArcherAlive;
        if (save == null) return;
        if (!c.Running) { Current = 0; return; }
        if (Current != 0)
        {
            if (!c.ArcherAlive) return;   // süre durur, baloncuk gizli
            shownFor += dt;
            if (Current == Map && c.FullFrame) { Close(true); return; }
            if (Current == Follow && !c.FullFrame) { Close(true); return; }
            if (shownFor >= ShowSeconds) { TimeoutCount++; Close(false); }
            return;
        }
        if (!c.ArcherAlive) return;
        gap -= dt;
        if (gap > 0f) return;
        foreach (int bit in Order)
        {
            if ((save.raidIntroSeen & bit) != 0) continue;
            bool basic = (bit & BasicMask) != 0;
            if (Eligible(bit, c)) { Current = bit; shownFor = 0f; return; }
            if (basic) return;   // temel adımlar sırayla bekler
        }
    }

    private static bool Eligible(int bit, Context c)
    {
        switch (bit)
        {
            case Fire: return c.BoatSeen;
            case Charge: return c.TargetInRange;
            case Post: return c.PostStage;
            case Map: return !c.FullFrame;
            case Follow: return c.FullFrame;
            case Ability0: return (c.UnlockedAbilities & 1) != 0;
            case Ability1: return (c.UnlockedAbilities & 2) != 0;
            case Ability2: return (c.UnlockedAbilities & 4) != 0;
            default: return false;
        }
    }

    private void Close(bool byAction)
    {
        int bit = Current;
        Current = 0; shownFor = 0f;
        gap = bit == Map && byAction ? 0f : GapSeconds;
        if (save == null || bit == 0) return;
        save.raidIntroSeen |= bit;
        if (bit == Map && !byAction) save.raidIntroSeen |= Follow;   // HARİTA'ya basılmadıysa TAKİP anlamsız
        Persist?.Invoke();
    }

    // ---- Oyuncu eylemleri ----
    public void OnShot(bool charged)
    {
        if (Current == Fire || (Current == Charge && charged)) Close(true);
    }
    public void OnPostChanged() { if (Current == Post) Close(true); }
    public void OnAbilityUsed(int index) { if (Current == (Ability0 << index)) Close(true); }

    /// <summary>ATLA: 1–16 bitleri (temel adımlar) işaretlenir; yetenek bitleri kendi açılışında yine bir kez gösterilir.</summary>
    public void Skip()
    {
        if (save == null) return;
        Current = 0; shownFor = 0f; gap = GapSeconds;
        save.raidIntroSeen |= BasicMask;
        SkipCount++;
        Persist?.Invoke();
    }

    /// <summary>Şu an gösterilen adımın bayrağı yetenek mi? (HUD oku yetenek düğmesine çevirir.)</summary>
    public int AbilityIndex => Current == Ability0 ? 0 : Current == Ability1 ? 1 : Current == Ability2 ? 2 : -1;
}
