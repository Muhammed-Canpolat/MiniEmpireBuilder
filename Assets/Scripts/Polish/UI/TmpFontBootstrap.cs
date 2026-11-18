using TMPro;
using UnityEngine;

/// <summary>
/// Oyunun TMP font seti (D124; D113'ün yerini alır). Fontlar EDİTÖRDE önceden doldurulmuş STATİK asset'lerdir
/// (`Assets/Resources/Fonts/TMP`, üretim: Tools/Mini Empire Builder/Yazı/Font Asset'lerini Üret = FontPipeline.Build):
///   Gövde  "MEB Body SDF"  (Alegreya Sans Medium) = TMP Settings varsayılanı → font atanmadan kurulan yazılar zaten Türkçe-tam,
///   Kalın  "MEB Bold SDF"  (Alegreya Sans Bold), Başlık "MEB Title SDF" (Cinzel ExtraBold; eksik glif → Kalın → Gövde),
///   güvenlik ağı "MEB Safety Dynamic SDF" (boş dinamik, yalnız statik sette olmayan karakter gelirse dolar).
/// D113'teki çalışma anı kancası (TEXT_CHANGED'da font değiştirme) KALDIRILDI: TMP o olayı grafik yeniden kurulumu içinde
/// tetikler, oradaki font/malzeme değişimi ağı yeniden üretmeden yeni atlası bağlıyordu → görünmeyen/parça/kutu yazılar (D124 kanıtı).
/// Çalışma anında font üretimi yalnız asset'ler bulunamazsa (bozuk kurulum) son çare olarak yapılır.
/// </summary>
public static class TmpFontBootstrap
{
    public const string BodyPath = "Fonts/TMP/MEB Body SDF";
    public const string BoldPath = "Fonts/TMP/MEB Bold SDF";
    public const string TitlePath = "Fonts/TMP/MEB Title SDF";
    public const string ExtraCharacters = "ĞÜŞİÖÇğüşıöçÂâÎîÛû·→…—–•×";
    private static TMP_FontAsset body, bold, title;

    public static TMP_FontAsset BodyFont => body != null ? body : (body = Load(BodyPath, "Fonts/AlegreyaSans-Medium"));
    public static TMP_FontAsset BoldFont => bold != null ? bold : (bold = Load(BoldPath, "Fonts/AlegreyaSans-Bold"));
    public static TMP_FontAsset TitleFont => title != null ? title : (title = Load(TitlePath, "Fonts/Cinzel-ExtraBold"));

    private static TMP_FontAsset Load(string path, string ttf)
    {
        var asset = Resources.Load<TMP_FontAsset>(path);
        if (asset != null) return asset;
        Debug.LogError($"[UI Font] Statik font asset'i yok: Resources/{path} — dinamik yedek üretiliyor (FontPipeline.Build çalıştırılmalı).");
        return CreateDynamicFont(Resources.Load<Font>(ttf));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // Yalnız denetim + son çare: varsayılan font Türkçe-tam değilse (TMP Settings bozulmuşsa) gövde fontunu fallback'e bağla.
        var def = TMP_Settings.defaultFontAsset;
        var b = BodyFont;
        if (def == null || b == null || def == b) return;
        Debug.LogWarning($"[UI Font] TMP varsayılan fontu '{def.name}', beklenen '{b.name}' — gövde fontu fallback olarak bağlandı.");
        if (def.fallbackFontAssetTable == null) def.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        if (!def.fallbackFontAssetTable.Contains(b)) def.fallbackFontAssetTable.Insert(0, b);
    }

    /// <summary>Son çare: dinamik atlaslı font (asset eksikse). Shader build'de yoksa varsayılan fontun shader'ına bağlanır.</summary>
    public static TMP_FontAsset CreateDynamicFont(Font source)
    {
        if (source == null) return null;
        var asset = TMP_FontAsset.CreateFontAsset(source, 72, 7, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        if (asset == null) return null;
        var def = TMP_Settings.defaultFontAsset;
        if (asset.material != null && def != null && def.material != null &&
            (asset.material.shader == null || asset.material.shader.name.StartsWith("Hidden/")))
            asset.material.shader = def.material.shader;
        asset.TryAddCharacters("  .,;:!?'\"()+-/%0123456789" + ExtraCharacters, out _);
        float min = asset.faceInfo.pointSize * MinSpaceEm;
        foreach (uint code in new uint[] { 32, 160 })
        {
            if (!asset.characterLookupTable.TryGetValue(code, out var ch) || ch.glyph == null) continue;
            var m = ch.glyph.metrics;
            if (m.horizontalAdvance < min) ch.glyph.metrics = new UnityEngine.TextCore.GlyphMetrics(m.width, m.height, m.horizontalBearingX, m.horizontalBearingY, min);
        }
        return asset;
    }

    /// <summary>Alegreya Sans'ın boşluk glifi ~0,15 em; üretimde (FontPipeline.FixSpace) en az bu kadar yapılır (D113 kelime aralığı).</summary>
    public const float MinSpaceEm = 0.26f;

    /// <summary>Test/tanı: font seti statik ve Türkçe-tam mı, varsayılan gövde fontu mu, malzeme/atlas sağlam mı.</summary>
    public static bool SelfCheck(out string report)
    {
        report = "";
        var def = TMP_Settings.defaultFontAsset;
        foreach (var f in new[] { BodyFont, BoldFont, TitleFont })
        {
            if (f == null) { report = "font yok"; return false; }
            if (f.atlasPopulationMode != AtlasPopulationMode.Static) { report = f.name + " statik değil"; return false; }
            if (f.material == null || f.material.shader == null || !f.material.shader.isSupported) { report = f.name + " shader yok/desteklenmiyor"; return false; }
            if (f.atlasTexture == null || f.atlasTexture.width <= 1) { report = f.name + " atlas boş"; return false; }
            if (f.material.mainTexture != f.atlasTexture) { report = f.name + " malzeme başka atlası gösteriyor"; return false; }
            foreach (char c in "Dalga 1 DALGA YAPI DEKOR DÜKKAN DEPO KAHRAMAN ÇIRAK" + ExtraCharacters)
                if (c != ' ' && !TmpTextIntegrity.Covered(f, c, out _)) { report = $"{f.name}: '{c}' yok"; return false; }
        }
        foreach (char c in "abcçdefgğhıijklmnoöprsştuüvyzABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ0123456789")
            if (!BodyFont.characterLookupTable.ContainsKey(c)) { report = $"gövde fontunda (birincil) '{c}' yok"; return false; }
        if (def != BodyFont) { report = $"TMP varsayılanı {(def != null ? def.name : "null")}, beklenen {BodyFont.name}"; return false; }
        return true;
    }

    /// <summary>Regresyon (kelime aralıkları kayboldu): her fontta boşluk glifi var ve "a b" metni "ab"den belirgin geniş.</summary>
    public static bool SpaceCheck(out string report)
    {
        report = "";
        var fonts = new[] { BodyFont, TitleFont, BoldFont };
        var go = new GameObject("tmpSpaceCheck", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 20; tmp.textWrappingMode = TextWrappingModes.NoWrap;
            foreach (var f in fonts)
            {
                if (f == null) { report = "font yok"; return false; }
                tmp.font = f;
                tmp.text = "ab"; float ab = tmp.GetPreferredValues("ab").x;
                float spaced = tmp.GetPreferredValues("a b").x;
                float sentence = tmp.GetPreferredValues("yakındaki ağaçları +250 altın").x;
                float joined = tmp.GetPreferredValues("yakındakiağaçları+250altın").x;
                bool has = f.characterLookupTable.TryGetValue(32, out var ch) && ch.glyph != null;
                float adv = has ? ch.glyph.metrics.horizontalAdvance : -1f;
                if (!has || adv < f.faceInfo.pointSize * MinSpaceEm - 0.01f || spaced < ab + 20f * (MinSpaceEm - 0.02f) || sentence < joined + 20f * 3f * (MinSpaceEm - 0.01f))
                { report = $"{f.name}: boşluk glifi={has} adv={adv} ab={ab} a_b={spaced} cümle={sentence} bitişik={joined}"; return false; }
            }
            return true;
        }
        finally { Object.DestroyImmediate(go); }
    }
}
