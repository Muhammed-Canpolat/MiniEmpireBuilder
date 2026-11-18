using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// D124 (UI2 Madde 1+5) yazı altyapısı: oyunun kullandığı TÜM karakterleri tarar, MEB font setini (Alegreya Sans Medium/Bold +
/// Cinzel ExtraBold) EDİTÖRDE önceden doldurulmuş STATİK TMP font asset'i olarak diske yazar (atlas + malzeme alt-asset),
/// TMP Settings varsayılanı/fallback zincirini ve TMP shader'ının build'e girmesini (Always Included) ayarlar; "Font Denetimi" raporu üretir.
/// Menü: Tools/Mini Empire Builder/Yazı/...  · CLI: -executeMethod FontPipeline.BuildCli | FontPipeline.AuditCli
/// </summary>
public static class FontPipeline
{
    public const string OutDir = "Assets/Resources/Fonts/TMP";
    public const string BodyPath = OutDir + "/MEB Body SDF.asset";
    public const string BoldPath = OutDir + "/MEB Bold SDF.asset";
    public const string TitlePath = OutDir + "/MEB Title SDF.asset";
    public const string SafetyPath = OutDir + "/MEB Safety Dynamic SDF.asset";
    public const string ReportPath = "Docs/playtest/ui2/font-denetimi.txt";
    private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string LegacyPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string MobileSdfShaderGuid = "fe393ace9b354375a9cb14cdbbc28be4";   // TextMeshPro/Mobile/Distance Field
    private const string SdfShaderGuid = "68e6db2ebdc24f95958faec2be5558d6";         // TextMeshPro/Distance Field
    private const int SamplingSize = 72, Padding = 7, AtlasSize = 1024;
    public const float MinSpaceEm = 0.26f;   // TmpFontBootstrap.MinSpaceEm ile aynı (D113 kelime aralığı)

    /// <summary>Kod/sahne taramasından bağımsız, her zaman atlasta olacak karakterler.</summary>
    public const string BaseCharacters =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        " ÇĞİÖŞÜçğıöşüÂâÎîÛûÀÁÄÉÈÊËÍÌÏÑÓÒÔÕÚÙàáäéèêëíìïñóòôõúù" +
        "·•…—–‘’“”«»×÷±°%‰€£$¥₺→←↑↓▲▼▶©®™¹²³½¼¾§¶";

    private static readonly string[] SkipFolders = { "Assets/Dragon Warrior Files", "Assets/TextMesh Pro", "Assets/Tests", "Assets/Editor" };
    private static readonly string[] DataRoots = { "Assets/Scenes", "Assets/Prefabs", "Assets/Resources", "Assets/MiniEmpireUI", "Assets/MiniEmpire_Intro" };

    // ==================== KARAKTER TARAMASI ====================

    public sealed class CharUse
    {
        public readonly SortedDictionary<int, string> FirstSeen = new SortedDictionary<int, string>();
        public void Add(string text, string where)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (int cp in CodePoints(text))
                if (!FirstSeen.ContainsKey(cp)) FirstSeen[cp] = where;
        }
        public string AsString() => string.Concat(FirstSeen.Keys.Select(char.ConvertFromUtf32));
    }

    private static IEnumerable<int> CodePoints(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            int cp = char.IsSurrogatePair(s, i) ? char.ConvertToUtf32(s, i++) : s[i];
            if (cp < 32 || cp == 0x200B || cp == 0x200D || cp == 0xAD || (cp >= 0xFE00 && cp <= 0xFE0F)) continue;
            yield return cp;
        }
    }

    private static bool Skipped(string path) => SkipFolders.Any(f => path.Replace('\\', '/').StartsWith(f + "/"));

    /// <summary>Oyun kodundaki (Assets/Scripts) dize sabitleri; günlük/istisna satırları ayrı tutulur (ekranda görünmez).</summary>
    public static void ScanCode(CharUse shown, CharUse logOnly)
    {
        foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            string src = File.ReadAllText(file, Encoding.UTF8);
            foreach (var (literal, line, lineText) in StringLiterals(src))
            {
                bool log = Regex.IsMatch(lineText, @"Debug\.Log|LogWarning|LogError|throw new|Exception\(|\[Conditional|Profiler|PlayerPrefs\.|Resources\.Load|GameObject\.Find|\.Find\(|Shader\.Find");
                (log ? logOnly : shown).Add(literal, $"{path}:{line}");
            }
        }
    }

    /// <summary>Basit C# sözcük çözücü: yorumları atlar, "..", @"..", $"..", $@"..", '.' sabitlerinin içeriğini verir.</summary>
    private static IEnumerable<(string text, int line, string lineText)> StringLiterals(string s)
    {
        var lines = s.Split('\n');
        int lineNo = 1, i = 0;
        string LineAt(int n) => n - 1 < lines.Length ? lines[n - 1] : "";
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\n') { lineNo++; i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) { if (s[i] == '\n') lineNo++; i++; }
                i += 2; continue;
            }
            bool verbatim = false;
            int start = i;
            if (c == '$' || c == '@')
            {
                int j = i;
                while (j < s.Length && (s[j] == '$' || s[j] == '@')) { if (s[j] == '@') verbatim = true; j++; }
                if (j < s.Length && s[j] == '"') { i = j; c = '"'; } else { i++; continue; }
            }
            if (c == '\'')
            {
                int end = i + 1; var sb = new StringBuilder();
                while (end < s.Length && s[end] != '\'' && s[end] != '\n') { if (s[end] == '\\' && end + 1 < s.Length) { sb.Append(Unescape(s, ref end)); continue; } sb.Append(s[end]); end++; }
                yield return (sb.ToString(), lineNo, LineAt(lineNo));
                i = end + 1; continue;
            }
            if (c == '"')
            {
                int startLine = lineNo;
                var sb = new StringBuilder();
                i++;
                while (i < s.Length)
                {
                    char d = s[i];
                    if (d == '\n') lineNo++;
                    if (verbatim) { if (d == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { sb.Append('"'); i += 2; continue; } break; } sb.Append(d); i++; continue; }
                    if (d == '\\') { sb.Append(Unescape(s, ref i)); continue; }
                    if (d == '"' || d == '\n') break;
                    sb.Append(d); i++;
                }
                i++;
                yield return (sb.ToString(), startLine, LineAt(startLine));
                continue;
            }
            i++;
        }
    }

    private static string Unescape(string s, ref int i, bool yaml = false)
    {
        // s[i] == '\\'
        char n = i + 1 < s.Length ? s[i + 1] : ' ';
        i += 2;
        switch (n)
        {
            case 'n': case 'r': case 't': case '0': return " ";
            case 'u':
                if (i + 4 <= s.Length && int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, null, out int u)) { i += 4; return ((char)u).ToString(); }
                return "u";
            case 'x':
                int len = 0; while (len < (yaml ? 2 : 4) && i + len < s.Length && Uri.IsHexDigit(s[i + len])) len++;
                if (len > 0 && int.TryParse(s.Substring(i, len), NumberStyles.HexNumber, null, out int x)) { i += len; return ((char)x).ToString(); }
                return "x";
            default: return n.ToString();
        }
    }

    /// <summary>Sahne/prefab/veri asset YAML'larındaki dize değerleri (m_text, displayName...). \uXXXX ve \xXX kaçışları çözülür.</summary>
    public static void ScanYaml(CharUse uses)
    {
        foreach (var root in DataRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".unity" && ext != ".prefab" && ext != ".asset") continue;
                string path = file.Replace('\\', '/');
                if (Skipped(path) || path.StartsWith(OutDir)) continue;
                if (new FileInfo(file).Length > 8_000_000) continue;
                int n = 0;
                foreach (var raw in File.ReadLines(file, Encoding.UTF8))
                {
                    n++;
                    var m = Regex.Match(raw, @"^\s*-?\s*(m_text|m_Text|displayName|description|title|label|text|[A-Za-z]*[Nn]ame|[A-Za-z]*[Tt]ext|[A-Za-z]*[Tt]itle|[A-Za-z]*[Dd]esc[A-Za-z]*|tr|en): (.+)$");
                    if (!m.Success) continue;
                    string v = m.Groups[2].Value.Trim();
                    if (v.StartsWith("{") || v.StartsWith("[")) continue;
                    if (v.StartsWith("\"") && v.EndsWith("\"") && v.Length >= 2)
                    {
                        v = v.Substring(1, v.Length - 2);
                        var sb = new StringBuilder();
                        for (int k = 0; k < v.Length;)
                            if (v[k] == '\\' && k + 1 < v.Length) sb.Append(Unescape(v, ref k, true)); else sb.Append(v[k++]);
                        v = sb.ToString();
                    }
                    else if (v.StartsWith("'") && v.EndsWith("'") && v.Length >= 2) v = v.Substring(1, v.Length - 2).Replace("''", "'");
                    uses.Add(v, $"{path}:{n}");
                }
            }
        }
    }

    public static string CollectCharset(out CharUse shown, out CharUse logOnly, out CharUse data)
    {
        shown = new CharUse(); logOnly = new CharUse(); data = new CharUse();
        ScanCode(shown, logOnly);
        ScanYaml(data);
        var all = new CharUse();
        all.Add(BaseCharacters, "temel");
        all.Add(shown.AsString(), "kod");
        all.Add(data.AsString(), "veri");
        all.Add(logOnly.AsString(), "günlük");
        return all.AsString();
    }

    // ==================== ÜRETİM ====================

    [MenuItem("Tools/Mini Empire Builder/Yazı/Font Asset'lerini Üret (statik)")]
    public static void Build()
    {
        string charset = CollectCharset(out _, out _, out _);
        Directory.CreateDirectory(OutDir);
        var body = BuildStatic(BodyPath, "Fonts/AlegreyaSans-Medium", "MEB Body SDF", charset);
        var bold = BuildStatic(BoldPath, "Fonts/AlegreyaSans-Bold", "MEB Bold SDF", charset);
        var title = BuildStatic(TitlePath, "Fonts/Cinzel-ExtraBold", "MEB Title SDF", charset);
        var safety = BuildSafety(SafetyPath, "Fonts/AlegreyaSans-Medium", "MEB Safety Dynamic SDF");

        SetFallbacks(body, safety);
        SetFallbacks(bold, body, safety);
        SetFallbacks(title, bold, body, safety);

        // TMP Settings: varsayılan = Türkçe-tam statik gövde fontu; genel fallback = dinamik güvenlik ağı; build'de dinamik veri temizlenmez.
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath);
        var so = new SerializedObject(settings);
        so.FindProperty("m_defaultFontAsset").objectReferenceValue = body;
        so.FindProperty("m_defaultFontAssetPath").stringValue = "Fonts/TMP/";
        var fb = so.FindProperty("m_fallbackFontAssets");
        fb.ClearArray(); fb.InsertArrayElementAtIndex(0); fb.GetArrayElementAtIndex(0).objectReferenceValue = safety;
        so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        so.FindProperty("m_warningsDisabled").boolValue = false;   // eksik karakter uyarıları açık
        so.ApplyModifiedPropertiesWithoutUndo();

        // Eski referanslar (LiberationSans) için güvenlik: Türkçe karakterler statik gövde fontundan gelir (çalışma anı kancası yok).
        var legacy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyPath);
        if (legacy != null)
        {
            var list = legacy.fallbackFontAssetTable ?? new List<TMP_FontAsset>();
            list.RemoveAll(f => f == null || f == body);
            list.Insert(0, body);
            legacy.fallbackFontAssetTable = list;
            EditorUtility.SetDirty(legacy);
        }

        EnsureAlwaysIncludedShaders();
        int migrated = MigrateLegacyReferences(body);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FontPipeline] Üretildi: {charset.Length} karakter; gövde {Pages(body)} atlas, kalın {Pages(bold)}, başlık {Pages(title)}; eski font referansı taşınan dosya: {migrated}");
    }

    private static int Pages(TMP_FontAsset f) => f != null && f.atlasTextures != null ? f.atlasTextures.Count(t => t != null && t.width > 1) : 0;

    private static TMP_FontAsset BuildStatic(string path, string ttf, string name, string charset)
    {
        var font = Resources.Load<Font>(ttf);
        if (font == null) throw new Exception("Kaynak font yok: " + ttf);
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        }
        // Yeniden üretim: GUID ve alt-asset (malzeme, atlas 0) kimlikleri korunur → sahne/prefab bağlantıları bozulmaz.
        asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        var so = new SerializedObject(asset);
        so.FindProperty("m_SourceFontFile").objectReferenceValue = font;
        var guidProp = so.FindProperty("m_SourceFontFileGUID");
        if (guidProp != null) guidProp.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(font));
        so.ApplyModifiedPropertiesWithoutUndo();
        asset.isMultiAtlasTexturesEnabled = true;
        asset.ClearFontAssetData(true);
        bool ok = asset.TryAddCharacters(charset, out string missing);
        if (!ok && !string.IsNullOrEmpty(missing)) Debug.Log($"[FontPipeline] {name}: kaynak fontta olmayan karakterler (fallback'ten gelir): {missing}");
        FixSpace(asset);
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        SetClearOnBuild(asset, false);
        for (int i = 0; i < asset.atlasTextures.Length; i++)
        {
            var tex = asset.atlasTextures[i];
            if (tex == null) continue;
            tex.name = i == 0 ? name + " Atlas" : $"{name} Atlas {i}";
            if (!AssetDatabase.Contains(tex)) AssetDatabase.AddObjectToAsset(tex, asset);
        }
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    /// <summary>Dinamik güvenlik ağı: boş başlar (build'de sıfır maliyet), yalnız statik setlerde olmayan bir karakter gelirse doldurulur.</summary>
    private static TMP_FontAsset BuildSafety(string path, string ttf, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null) { SetClearOnBuild(asset, false); EditorUtility.SetDirty(asset); return asset; }
        var font = Resources.Load<Font>(ttf);
        asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
        asset.name = name;
        AssetDatabase.CreateAsset(asset, path);
        asset.material.name = name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        asset.atlasTextures[0].name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.ClearFontAssetData(true);
        SetClearOnBuild(asset, false);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void FixSpace(TMP_FontAsset asset)
    {
        float min = asset.faceInfo.pointSize * MinSpaceEm;
        foreach (uint code in new uint[] { 32, 160 })
        {
            if (!asset.characterLookupTable.TryGetValue(code, out var ch) || ch.glyph == null) continue;
            var m = ch.glyph.metrics;
            if (m.horizontalAdvance >= min) continue;
            ch.glyph.metrics = new UnityEngine.TextCore.GlyphMetrics(m.width, m.height, m.horizontalBearingX, m.horizontalBearingY, min);
        }
    }

    private static void SetClearOnBuild(TMP_FontAsset asset, bool value)
    {
        var so = new SerializedObject(asset);
        so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool GetClearOnBuild(TMP_FontAsset asset) => new SerializedObject(asset).FindProperty("m_ClearDynamicDataOnBuild").boolValue;

    private static void SetFallbacks(TMP_FontAsset asset, params TMP_FontAsset[] chain)
    {
        asset.fallbackFontAssetTable = chain.Where(f => f != null && f != asset).ToList();
        EditorUtility.SetDirty(asset);
    }

    /// <summary>TMP SDF shader'ları Always Included: Shader.Find (çalışma anı güvenlik ağı) ve tüm anahtar sözcük varyantları (kontur/gölge) her platformda build'e girer.</summary>
    public static bool EnsureAlwaysIncludedShaders()
    {
        var gs = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var arr = gs.FindProperty("m_AlwaysIncludedShaders");
        bool changed = false;
        foreach (var guid in new[] { MobileSdfShaderGuid, SdfShaderGuid })
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader == null) continue;
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) has = true;
            if (has) continue;
            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            changed = true;
        }
        if (changed) gs.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    /// <summary>Oyun sahne/prefab'larında LiberationSans SDF (Türkçe eksik) referanslarını MEB Body'ye taşır (YAML GUID değişimi).</summary>
    private static int MigrateLegacyReferences(TMP_FontAsset body)
    {
        string legacyGuid = AssetDatabase.AssetPathToGUID(LegacyPath);
        string bodyGuid = AssetDatabase.AssetPathToGUID(BodyPath);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(body.material, out string _, out long matId)) return 0;
        var legacy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyPath);
        long legacyMatId = 0;
        if (legacy != null) AssetDatabase.TryGetGUIDAndLocalFileIdentifier(legacy.material, out string _, out legacyMatId);
        int count = 0;
        foreach (var root in new[] { "Assets/Scenes", "Assets/Prefabs", "Assets/Resources", "Assets/MiniEmpireUI", "Assets/MiniEmpire_Intro" })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".unity" && ext != ".prefab") continue;
                string text = File.ReadAllText(file);
                if (!text.Contains(legacyGuid)) continue;
                string next = text
                    .Replace($"{{fileID: 11400000, guid: {legacyGuid}, type: 2}}", $"{{fileID: 11400000, guid: {bodyGuid}, type: 2}}")
                    .Replace($"{{fileID: {legacyMatId}, guid: {legacyGuid}, type: 2}}", $"{{fileID: {matId}, guid: {bodyGuid}, type: 2}}");
                if (next == text) continue;
                File.WriteAllText(file, next);
                count++;
            }
        }
        return count;
    }

    public static void BuildCli()
    {
        try { Build(); Audit(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogError("[FontPipeline] " + e); EditorApplication.Exit(1); }
    }

    // ==================== FONT DENETİMİ ====================

    [MenuItem("Tools/Mini Empire Builder/Yazı/Font Denetimi")]
    public static void AuditMenu()
    {
        int missing = Audit();
        EditorUtility.DisplayDialog("Font Denetimi", $"Eksik karakter: {missing}\nRapor: {ReportPath}", "Tamam");
    }

    public static void AuditCli()
    {
        int missing = -1;
        try { missing = Audit(); }
        catch (Exception e) { Debug.LogError("[FontAudit] " + e); }
        EditorApplication.Exit(missing == 0 ? 0 : 1);
    }

    private enum Cover { Primary, Fallback, DynamicOnly, Missing }

    private static Cover Check(TMP_FontAsset font, int cp, out TMP_FontAsset owner)
    {
        owner = null;
        if (font == null) return Cover.Missing;
        var seen = new HashSet<TMP_FontAsset>();
        var queue = new List<TMP_FontAsset> { font };
        bool globalAdded = false;
        TMP_FontAsset dynamicOwner = null;
        for (int qi = 0; qi < queue.Count; qi++)
        {
            var f = queue[qi];
            if (f != null && seen.Add(f))
            {
                if (f.characterLookupTable != null && f.characterLookupTable.ContainsKey((uint)cp)) { owner = f; return qi == 0 ? Cover.Primary : Cover.Fallback; }
                if (f.atlasPopulationMode == AtlasPopulationMode.Dynamic && dynamicOwner == null && f.sourceFontFile != null && HasInSource(f, cp)) dynamicOwner = f;
                if (f.fallbackFontAssetTable != null) queue.AddRange(f.fallbackFontAssetTable);
            }
            if (qi == queue.Count - 1 && !globalAdded && TMP_Settings.fallbackFontAssets != null) { globalAdded = true; queue.AddRange(TMP_Settings.fallbackFontAssets); }
        }
        if (dynamicOwner != null) { owner = dynamicOwner; return Cover.DynamicOnly; }
        return Cover.Missing;
    }

    private static readonly Dictionary<Font, HashSet<int>> sourceGlyphs = new Dictionary<Font, HashSet<int>>();
    private static bool HasInSource(TMP_FontAsset f, int cp)
    {
        var font = f.sourceFontFile;
        if (!sourceGlyphs.TryGetValue(font, out var set))
        {
            set = new HashSet<int>();
            if (FontEngine.LoadFontFace(font, 72) == FontEngineError.Success)
                for (int c = 32; c < 0x2700; c++) if (FontEngine.TryGetGlyphIndex((uint)c, out uint gi) && gi != 0) set.Add(c);
            sourceGlyphs[font] = set;
        }
        return set.Contains(cp);
    }

    private static readonly Regex RichTag = new Regex(@"<[^<>]{1,64}>");

    /// <summary>Raporu yazar; ekranda görünecek metinlerde eksik karakter sayısını döndürür (0 = temiz).</summary>
    public static int Audit()
    {
        var sb = new StringBuilder();
        int missingTotal = 0, fallbackTotal = 0, dynamicTotal = 0, legacyUse = 0;
        var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyPath);
        var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
        var title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitlePath);
        var def = TMP_Settings.defaultFontAsset;
        sb.AppendLine($"# Font Denetimi — {DateTime.Now:yyyy-MM-dd HH:mm} (FontPipeline.Audit, D124)");
        sb.AppendLine();

        // 1) Envanter
        sb.AppendLine("## 1. Font asset envanteri");
        var usage = new Dictionary<TMP_FontAsset, int>();
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Skipped(p) && !p.StartsWith("Assets/TextMesh Pro")) continue;
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f == null) continue;
            var mat = f.material;
            sb.AppendLine($"- {p}: mod={f.atlasPopulationMode}, atlas={f.atlasWidth}x{f.atlasHeight} x{Pages(f)} sayfa, çok-atlas={f.isMultiAtlasTexturesEnabled}, " +
                          $"karakter={f.characterTable?.Count ?? 0}, ClearDynamicDataOnBuild={GetClearOnBuild(f)}, kaynak={(f.sourceFontFile != null ? f.sourceFontFile.name : "-")}, " +
                          $"shader={(mat != null && mat.shader != null ? mat.shader.name : "YOK")}, fallback=[{string.Join(", ", (f.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Where(x => x != null).Select(x => x.name))}]");
        }
        sb.AppendLine($"- TMP Settings: varsayılan={(def != null ? def.name : "YOK")}, genel fallback=[{string.Join(", ", (TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>()).Where(x => x != null).Select(x => x.name))}], " +
                      $"ClearDynamicDataOnBuild={TMP_Settings.clearDynamicDataOnBuild}, uyarılar kapalı={TMP_Settings.warningsDisabled}");
        sb.AppendLine();

        // 2) Sahne + prefab yazıları
        sb.AppendLine("## 2. Sahne ve prefab TMP yazıları (eksik = zincirde yok; fallback = birincil fontta yok, zincirden gelir)");
        var issues = new List<string>();
        int textCount = 0;
        void Inspect(TMP_Text t, string where)
        {
            textCount++;
            var font = t.font != null ? t.font : def;
            usage[font] = usage.TryGetValue(font, out int u) ? u + 1 : 1;
            if (font != null && AssetDatabase.GetAssetPath(font) == LegacyPath) { legacyUse++; issues.Add($"ESKİ FONT (LiberationSans) {where}"); }
            string plain = RichTag.Replace(t.text ?? "", "");
            var miss = new StringBuilder(); var fall = new StringBuilder(); var dyn = new StringBuilder();
            foreach (int cp in CodePoints(plain).Distinct())
            {
                if (cp == ' ' || cp == 0xA0) continue;
                switch (Check(font, cp, out var owner))
                {
                    case Cover.Missing: miss.Append(char.ConvertFromUtf32(cp)); missingTotal++; break;
                    case Cover.DynamicOnly: dyn.Append(char.ConvertFromUtf32(cp)); dynamicTotal++; break;
                    case Cover.Fallback: fall.Append(char.ConvertFromUtf32(cp)); fallbackTotal++; break;
                }
            }
            if (miss.Length > 0) issues.Add($"EKSİK '{miss}' {where} font={font?.name}");
            if (dyn.Length > 0) issues.Add($"DİNAMİK '{dyn}' {where} font={font?.name}");
            if (fall.Length > 0) issues.Add($"fallback '{fall}' {where} font={font?.name}");
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Skipped(p) || !DataRoots.Any(r => p.StartsWith(r + "/"))) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) continue;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) Inspect(t, $"{p}:{t.name}");
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var sc = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                foreach (var rootGo in sc.GetRootGameObjects())
                    foreach (var t in rootGo.GetComponentsInChildren<TMP_Text>(true)) Inspect(t, $"{scene.path}:{t.name}");
            }
        }
        finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        sb.AppendLine($"Taranan yazı: {textCount}");
        foreach (var kv in usage.OrderByDescending(k => k.Value)) sb.AppendLine($"- kullanım: {(kv.Key != null ? kv.Key.name : "null")} × {kv.Value}");
        foreach (var line in issues) sb.AppendLine("- " + line);
        sb.AppendLine();

        // 3) Kod içi metinler (L.T, AssetText, sabitler) → kodla kurulan yazılar MEB Body (varsayılan) / Bold / Title kullanır
        sb.AppendLine("## 3. Kod ve veri metinleri (Assets/Scripts dize sabitleri + sahne/prefab/veri YAML dizeleri)");
        CollectCharset(out var shown, out var logOnly, out var data);
        var combined = new CharUse();
        foreach (var kv in shown.FirstSeen) combined.FirstSeen[kv.Key] = kv.Value;
        foreach (var kv in data.FirstSeen) if (!combined.FirstSeen.ContainsKey(kv.Key)) combined.FirstSeen[kv.Key] = kv.Value;
        sb.AppendLine($"Ekranda görünebilecek farklı karakter: {combined.FirstSeen.Count} (yalnız günlük/istisna satırlarında: {logOnly.FirstSeen.Keys.Except(combined.FirstSeen.Keys).Count()})");
        foreach (var (font, label) in new[] { (body, "Gövde (MEB Body, varsayılan)"), (bold, "Kalın (MEB Bold)"), (title, "Başlık (MEB Title)") })
        {
            var miss = new List<string>(); var fall = new List<string>(); var dyn = new List<string>();
            foreach (var kv in combined.FirstSeen)
            {
                if (kv.Key == ' ' || kv.Key == 0xA0) continue;
                string ch = char.ConvertFromUtf32(kv.Key);
                switch (Check(font, kv.Key, out var owner))
                {
                    case Cover.Missing: miss.Add($"'{ch}' U+{kv.Key:X4} ({kv.Value})"); break;
                    case Cover.DynamicOnly: dyn.Add($"'{ch}' U+{kv.Key:X4} ({kv.Value})"); break;
                    case Cover.Fallback: fall.Add($"'{ch}'→{owner.name}"); break;
                }
            }
            missingTotal += miss.Count; dynamicTotal += dyn.Count;
            sb.AppendLine($"- {label}: eksik {miss.Count}, yalnız dinamik güvenlik ağında {dyn.Count}, fallback'e bağımlı {fall.Count}");
            foreach (var m in miss) sb.AppendLine("  - EKSİK " + m);
            foreach (var m in dyn) sb.AppendLine("  - DİNAMİK " + m);
            if (fall.Count > 0) sb.AppendLine("  - fallback: " + string.Join(" ", fall));
        }
        var logMissing = logOnly.FirstSeen.Where(kv => !combined.FirstSeen.ContainsKey(kv.Key) && Check(body, kv.Key, out _) == Cover.Missing).ToList();
        if (logMissing.Count > 0) sb.AppendLine("- Yalnız günlük/istisna metninde (ekranda görünmez, sayılmaz): " + string.Join(" ", logMissing.Select(kv => $"'{char.ConvertFromUtf32(kv.Key)}' ({kv.Value})")));
        sb.AppendLine();

        // 4) Shader ve platformlar
        sb.AppendLine("## 4. Shader / platform (Android, iOS)");
        AppendShaderReport(sb);
        sb.AppendLine();
        sb.AppendLine($"## SONUÇ: eksik={missingTotal}, yalnız-dinamik={dynamicTotal}, fallback'e bağımlı (yazı başına)={fallbackTotal}, eski font kullanımı={legacyUse}");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[FontAudit] eksik={missingTotal} dinamik={dynamicTotal} fallback={fallbackTotal} eski={legacyUse} → {ReportPath}");
        return missingTotal + dynamicTotal + legacyUse;
    }

    private static void AppendShaderReport(StringBuilder sb)
    {
        var gs = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        var arr = gs.FindProperty("m_AlwaysIncludedShaders");
        var included = new List<string>();
        for (int i = 0; i < arr.arraySize; i++) { var s = arr.GetArrayElementAtIndex(i).objectReferenceValue as Shader; if (s != null) included.Add(s.name); }
        sb.AppendLine($"- Always Included: {string.Join(", ", included)}");
        foreach (var target in new[] { BuildTarget.Android, BuildTarget.iOS })
        {
            bool auto = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            var apis = PlayerSettings.GetGraphicsAPIs(target);
            sb.AppendLine($"- {target}: grafik API {(auto ? "otomatik" : "elle")} = {string.Join(", ", apis)}; modül kurulu={BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target)}");
        }
        var platforms = new (ShaderCompilerPlatform p, BuildTarget t)[]
        {
            (ShaderCompilerPlatform.GLES3x, BuildTarget.Android), (ShaderCompilerPlatform.Vulkan, BuildTarget.Android), (ShaderCompilerPlatform.Metal, BuildTarget.iOS)
        };
        var keywordSets = new[] { new string[0], new[] { "OUTLINE_ON" }, new[] { "UNDERLAY_ON" }, new[] { "UNITY_UI_CLIP_RECT" }, new[] { "OUTLINE_ON", "UNITY_UI_CLIP_RECT", "UNITY_UI_ALPHACLIP" } };
        foreach (var guid in new[] { MobileSdfShaderGuid, SdfShaderGuid })
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader == null) { sb.AppendLine($"- shader {guid} YOK"); continue; }
            var data = ShaderUtil.GetShaderData(shader);
            var pass = data.GetSubshader(0).GetPass(0);
            foreach (var (p, t) in platforms)
            {
                int ok = 0, fail = 0; string err = "";
                foreach (var kw in keywordSets)
                    foreach (var type in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        try
                        {
                            var r = pass.CompileVariant(type, kw, p, t);
                            if (r.Success) ok++; else { fail++; if (err.Length == 0 && r.Messages.Length > 0) err = r.Messages[0].message; }
                        }
                        catch (Exception e) { fail++; if (err.Length == 0) err = e.Message; }
                    }
                sb.AppendLine($"- {shader.name} [{p}/{t}]: derlenen varyant {ok}, hata {fail}{(err.Length > 0 ? " — " + err : "")}");
            }
        }
    }
}
