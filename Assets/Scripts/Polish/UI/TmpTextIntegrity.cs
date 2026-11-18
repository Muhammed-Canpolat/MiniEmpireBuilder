using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP yazı bütünlüğü denetimi (D124). İki şeyi ölçer:
/// (1) Eksik glif: metindeki her karakter yazının fontunda ya da fallback zincirinde var mı (statik tabloya bakar, dinamik ekleme yapmaz).
/// (2) Ağ/malzeme uyumu: çizilen ağın UV'leri hangi font atlasından üretildiyse CanvasRenderer'daki malzeme de o atlası göstermeli.
///     D113'teki TEXT_CHANGED font değişimi bu uyumu bozuyordu (yazı görünmez / parça parça / kutu).
/// Ayrıca geliştirme build'inde ve editörde TMP'nin "eksik karakter" olayını günlüğe yazar (her karakter+font için bir kez).
/// </summary>
public static class TmpTextIntegrity
{
    private static readonly HashSet<long> logged = new HashSet<long>();
    private static readonly List<string> missingLog = new List<string>();
    public static IReadOnlyList<string> MissingLog => missingLog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        logged.Clear();
        missingLog.Clear();
        if (!Debug.isDebugBuild && !Application.isEditor) return;
        TMP_Text.OnMissingCharacter -= OnMissing;
        TMP_Text.OnMissingCharacter += OnMissing;
    }

    private static void OnMissing(int unicode, int stringIndex, string text, TMP_FontAsset fontAsset, TMP_Text textComponent)
    {
        long key = ((long)unicode << 32) | (uint)(fontAsset != null ? fontAsset.GetInstanceID() : 0);
        if (!logged.Add(key)) return;
        string line = $"U+{unicode:X4} '{(char)unicode}' font={(fontAsset != null ? fontAsset.name : "-")} nesne={(textComponent != null ? textComponent.name : "-")} metin=\"{Short(text)}\"";
        missingLog.Add(line);
        Debug.LogWarning("[UI Font] Eksik karakter " + line);
    }

    private static string Short(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 40 ? s.Substring(0, 40) + "…" : s);

    /// <summary>Karakter fontta veya fallback zincirinde (yazının fontu → font fallback'leri → TMP Settings fallback'leri) var mı? Dinamik ekleme denemez.</summary>
    public static bool Covered(TMP_FontAsset font, uint unicode, out TMP_FontAsset owner)
    {
        owner = null;
        var seen = new HashSet<int>();
        if (Search(font, unicode, seen, ref owner)) return true;
        var global = TMP_Settings.fallbackFontAssets;
        if (global != null) foreach (var f in global) if (Search(f, unicode, seen, ref owner)) return true;
        return false;
    }

    private static bool Search(TMP_FontAsset font, uint unicode, HashSet<int> seen, ref TMP_FontAsset owner)
    {
        if (font == null || !seen.Add(font.GetInstanceID())) return false;
        var table = font.characterLookupTable;
        if (table != null && table.ContainsKey(unicode)) { owner = font; return true; }
        if (font.fallbackFontAssetTable != null)
            foreach (var f in font.fallbackFontAssetTable) if (Search(f, unicode, seen, ref owner)) return true;
        return false;
    }

    public static bool Ignorable(uint c) => c < 32 || c == 0xA0 || c == 0x200B || c == 0xAD || (c >= 0xFE00 && c <= 0xFE0F) || c == 0x200D;

    /// <summary>Tek yazıyı denetler; sorun yoksa null.</summary>
    public static string Inspect(TMP_Text t)
    {
        if (t == null || !t.isActiveAndEnabled || string.IsNullOrEmpty(t.text)) return null;
        var sb = new StringBuilder();
        // (1) eksik glif (yazının o anki fontu ve zinciri)
        string parsed = t.GetParsedText();
        var missing = new StringBuilder();
        foreach (char ch in parsed)
        {
            if (char.IsWhiteSpace(ch) || Ignorable(ch)) continue;
            if (!Covered(t.font, ch, out _) && missing.ToString().IndexOf(ch) < 0) missing.Append(ch);
        }
        if (missing.Length > 0) sb.Append($"eksik glif '{missing}' (font {(t.font != null ? t.font.name : "yok")}); ");
        // (2) ağ / malzeme uyumu (ana ağ = materialReferenceIndex 0)
        var info = t.textInfo;
        if (info != null)
        {
            for (int i = 0; i < info.characterCount && i < info.characterInfo.Length; i++)
            {
                var ci = info.characterInfo[i];
                if (!ci.isVisible || ci.elementType != TMP_TextElementType.Character || ci.materialReferenceIndex != 0) continue;
                var fa = ci.fontAsset;
                int atlas = ci.textElement != null && ci.textElement.glyph != null ? ci.textElement.glyph.atlasIndex : 0;
                Texture expected = fa != null && fa.atlasTextures != null && atlas < fa.atlasTextures.Length ? fa.atlasTextures[atlas] : null;
                Material rendered = RenderedMaterial(t);
                Texture actual = rendered != null && rendered.HasProperty(ShaderUtilities.ID_MainTex) ? rendered.GetTexture(ShaderUtilities.ID_MainTex) : null;
                if (expected == null || actual == null || expected != actual)
                    sb.Append($"ağ/malzeme uyumsuz: ağ '{(fa != null ? fa.name : "-")}' atlasından, çizim '{(actual != null ? actual.name : "null")}' ile; ");
                if (fa != t.font && atlas == 0)
                    sb.Append($"bayat ağ: yazı fontu '{(t.font != null ? t.font.name : "-")}' ama ağ '{(fa != null ? fa.name : "-")}' ile üretilmiş; ");
                break;
            }
        }
        return sb.Length == 0 ? null : $"{Path(t.transform)} \"{Short(t.text)}\": {sb}";
    }

    private static Material RenderedMaterial(TMP_Text t)
    {
        if (t is TextMeshProUGUI ugui)
            return ugui.canvasRenderer != null && ugui.canvasRenderer.materialCount > 0 ? ugui.canvasRenderer.GetMaterial(0) : null;
        var r = t.GetComponent<Renderer>();
        return r != null ? r.sharedMaterial : null;
    }

    /// <summary>Sahnedeki tüm etkin TMP yazılarını denetler; sorunlu yazı sayısını döndürür.</summary>
    public static int ScanLoaded(out string report)
    {
        var sb = new StringBuilder();
        int bad = 0, total = 0;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            total++;
            string issue = Inspect(t);
            if (issue == null) continue;
            bad++;
            sb.AppendLine(issue);
        }
        report = $"{total} yazı, {bad} sorunlu\n{sb}";
        return bad;
    }

    /// <summary>
    /// Taşma ölçümü: etkin yazılardan metin sınırı kendi kutusunu (yatayda) aşanlar. <paramref name="filter"/> boş değilse yalnız yolunda
    /// bu parçalardan biri geçen yazılar denetlenir. Sorunlu yazı sayısını döndürür.
    /// </summary>
    public static int ScanOverflow(string[] filter, out string report)
    {
        var sb = new StringBuilder();
        int bad = 0;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(t.text)) continue;
            string path = Path(t.transform);
            if (filter != null && filter.Length > 0 && System.Array.TrueForAll(filter, f => path.IndexOf(f, System.StringComparison.Ordinal) < 0)) continue;
            t.ForceMeshUpdate();
            var rect = t.rectTransform.rect;
            var b = t.textBounds;
            bool over = b.size.x > rect.width + 0.5f || b.min.x < rect.xMin - 0.5f || b.max.x > rect.xMax + 0.5f || t.isTextOverflowing;
            if (!over) continue;
            bad++;
            sb.AppendLine($"{path} \"{Short(t.text)}\": metin {b.size.x:0.0}x{b.size.y:0.0} kutu {rect.width:0.0}x{rect.height:0.0} punto {t.fontSize:0.0} (oto={t.enableAutoSizing} {t.fontSizeMin}-{t.fontSizeMax}, sarma={t.textWrappingMode}, satır={t.textInfo.lineCount})");
        }
        report = sb.ToString();
        return bad;
    }

    /// <summary>Tanı: yolunda <paramref name="part"/> geçen etkin yazıların ölçüleri (punto, kutu, metin sınırı, ölçek, satır).</summary>
    public static string Describe(string part)
    {
        var sb = new StringBuilder();
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string path = Path(t.transform);
            if (path.IndexOf(part, System.StringComparison.Ordinal) < 0) continue;
            var rect = t.rectTransform.rect; var b = t.textBounds;
            var parent = t.transform.parent as RectTransform;
            sb.AppendLine($"{path} \"{t.text}\" font={(t.font != null ? t.font.name : "-")} punto={t.fontSize:0.00} oto={t.enableAutoSizing} {t.fontSizeMin}-{t.fontSizeMax} " +
                          $"kutu={rect.width:0.0}x{rect.height:0.0} metin={b.size.x:0.0}x{b.size.y:0.0} (x {b.min.x:0.0}..{b.max.x:0.0}) satır={t.textInfo.lineCount} " +
                          $"ebeveyn={(parent != null ? parent.rect.width.ToString("0.0") : "-")} ölçek={t.transform.lossyScale.x:0.000} kalın={t.fontStyle} boşluk={t.characterSpacing}");
        }
        return sb.ToString();
    }

    /// <summary>Test/tanı: font ATANMADAN (BattleVfxManager dalga kutusu ve sekmeler gibi) yazı içeren bir overlay canvas kurar.</summary>
    public static GameObject SpawnUnassignedProbe(string text, bool bold)
    {
        var canvasObj = new GameObject("FontProbeCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var go = new GameObject("FontProbeText", typeof(RectTransform));
        go.transform.SetParent(canvasObj.transform, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(900f, 120f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 40f;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return canvasObj;
    }

    /// <summary>Probe (veya herhangi bir kök) altındaki yazıların denetimi; sorun yoksa null.</summary>
    public static string InspectUnder(GameObject root)
    {
        var sb = new StringBuilder();
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
        {
            string issue = Inspect(t);
            if (issue != null) sb.AppendLine(issue);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        for (var x = t.parent; x != null && p.Length < 120; x = x.parent) p = x.name + "/" + p;
        return p;
    }
}
