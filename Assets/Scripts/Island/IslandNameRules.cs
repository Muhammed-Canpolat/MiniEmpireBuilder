using System.Globalization;
using System.Text;

/// <summary>
/// Ada adı kuralları (AŞAMA 3 §3B): kırp + boşlukları sıkıştır, 3–16 karakter, Latin harf (Türkçe dahil) / rakam / boşluk / ' / -,
/// küçük TR+EN küfür listesi, elle düzenleme mührü. Saf statik: testlenir.
/// </summary>
public static class IslandNameRules
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // Alt dizgi eşleşir (normalleştirilmiş: küçük harf, TR harfleri sadeleşmiş, rakam→harf, ayraçsız).
    private static readonly string[] Blocked =
    {
        "orospu", "yarrak", "amcik", "siktir", "pezevenk", "gotveren", "kahpe", "sikis", "yavsak", "gavat",
        "fuck", "shit", "bitch", "nigg", "whore", "bastard", "asshole", "porn", "hitler",
    };
    // Kısa kelimeler yalnız tam sözcük olarak engellenir ("Klasik" ya da "Kamkale" geçer).
    private static readonly string[] BlockedWords = { "amk", "aq", "sik", "got", "pic", "oc", "mk", "sg", "sex", "ass", "tit", "gay", "ibne", "cunt", "dick", "slut", "nazi", "rape" };

    public static string DefaultName => L.T("Martı Adası", "Gull Isle");

    /// <summary>Kırp + ardışık boşlukları teke indir.</summary>
    public static string Clean(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        bool space = false;
        foreach (char c in raw.Trim())
        {
            if (char.IsWhiteSpace(c)) { space = true; continue; }
            if (space && sb.Length > 0) sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static bool AllowedChar(char c) =>
        c == ' ' || c == '\'' || c == '-' || (c >= '0' && c <= '9') || (char.IsLetter(c) && c < 0x250);

    /// <summary>Geçerliyse true (temiz ad <paramref name="clean"/>); değilse error kullanıcıya gösterilecek metin.</summary>
    public static bool Validate(string raw, out string clean, out string error)
    {
        var cfg = FeatureConfig.Instance;
        clean = Clean(raw);
        error = "";
        if (clean.Length == 0) { error = L.T("Ad boş olamaz.", "The name can't be empty."); return false; }
        if (clean.Length < cfg.islandNameMin)
        { error = L.T($"En az {cfg.islandNameMin} karakter olmalı.", $"At least {cfg.islandNameMin} characters."); return false; }
        if (clean.Length > cfg.islandNameMax)
        { error = L.T($"En çok {cfg.islandNameMax} karakter olabilir.", $"At most {cfg.islandNameMax} characters."); return false; }
        foreach (char c in clean)
            if (!AllowedChar(c))
            { error = L.T("Yalnız harf, rakam, boşluk, ' ve - kullanılabilir.", "Only letters, digits, spaces, ' and - are allowed."); return false; }
        if (IsProfane(clean)) { error = L.T("Bu ad uygun değil. Başka bir ad dene.", "That name isn't allowed. Try another."); return false; }
        return true;
    }

    private static char Fold(char c)
    {
        switch (c)
        {
            case 'ı': return 'i'; case 'ç': return 'c'; case 'ğ': return 'g'; case 'ö': return 'o'; case 'ş': return 's'; case 'ü': return 'u';
            case '0': return 'o'; case '1': return 'i'; case '3': return 'e'; case '4': return 'a'; case '5': return 's';
            default: return c;
        }
    }

    public static bool IsProfane(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string lower = name.ToLower(Turkish);   // I→ı, İ→i
        var all = new StringBuilder();
        var word = new StringBuilder();
        var words = new System.Collections.Generic.List<string>();
        foreach (char raw in lower)
        {
            char c = Fold(raw);
            if (char.IsLetterOrDigit(c)) { all.Append(c); word.Append(c); }
            else { if (word.Length > 0) { words.Add(word.ToString()); word.Clear(); } }
        }
        if (word.Length > 0) words.Add(word.ToString());
        string joined = all.ToString();
        foreach (var b in Blocked) if (joined.Contains(b)) return true;
        foreach (var w in words) foreach (var b in BlockedWords) if (w == b) return true;
        return false;
    }

    // ==================== MÜHÜR ====================

    public static int Seal(string name, int changes)
    {
        unchecked
        {
            int h = 17 + 31 * changes;
            h = h * 31 + 0x5EA1;
            foreach (char c in name ?? "") h = h * 131 + c;
            return h ^ 0x2B7E1516;
        }
    }

    /// <summary>Boş varsayılan kayıt (ad yok, 0 değişiklik) geçerlidir; diğer durumlarda mühür eşleşmeli.</summary>
    public static bool SealValid(IslandSaveData save)
    {
        if (save == null) return true;
        if (string.IsNullOrEmpty(save.islandName) && save.nameChanges == 0) return true;
        return save.nameSeal == Seal(save.islandName, save.nameChanges);
    }

    /// <summary>Mühür bozuksa (elle düzenleme) ücretsiz hak kalmaz: değişiklik sayısı en az 2 sayılır. Çökmez.</summary>
    public static int EffectiveChanges(IslandSaveData save)
    {
        if (save == null) return 0;
        int n = save.nameChanges < 0 ? 0 : save.nameChanges;
        return SealValid(save) ? n : System.Math.Max(n, FeatureConfig.Instance.islandNameFreeChanges);
    }

    /// <summary>Görünen ad (boşsa varsayılan).</summary>
    public static string Display(IslandSaveData save) =>
        save == null || string.IsNullOrWhiteSpace(save.islandName) ? DefaultName : save.islandName;

    /// <summary>Sonraki değişikliğin elmas bedeli.</summary>
    public static int NextCost(IslandSaveData save) => GemEconomy.IslandRenameCost(EffectiveChanges(save));

    /// <summary>Adı yazar, sayacı artırır, mührü yeniler, teklifi işaretler. Doğrulama çağıranda yapılır.</summary>
    public static void Apply(IslandSaveData save, string cleanName)
    {
        int changes = EffectiveChanges(save) + 1;
        save.islandName = cleanName;
        save.nameChanges = changes;
        save.nameOffered = true;
        save.nameSeal = Seal(cleanName, changes);
    }
}
