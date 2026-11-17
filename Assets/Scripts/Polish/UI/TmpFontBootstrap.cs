using TMPro;
using UnityEngine;

/// <summary>
/// Tek noktadan TMP yazı güvenliği (iOS'ta görünmeyen yazılar, D113).
/// Font atanmadan oluşturulan TMP bileşenleri statik "LiberationSans SDF" varsayılanını kullanır; bunun Türkçe/özel glifleri
/// yoktur ve fallback listesi boştu. Burada (1) varsayılan fonta Dinamik Türkçe fallback zinciri eklenir,
/// (2) font atanmamış her TMP yazısı güvenli Dinamik fonta geçirilir, (3) çalışma anında üretilen font asset'lerinin
/// shader'ı build'de ayıklanmışsa (Shader.Find null) varsayılan fontun shader'ına bağlanır.
/// </summary>
public static class TmpFontBootstrap
{
    public const string ExtraCharacters = "ĞÜŞİÖÇğüşıöçÂâÎîÛû·→…—–•×";
    private static TMP_FontAsset bodyFont;
    private static bool hooked;

    public static TMP_FontAsset BodyFont => bodyFont != null ? bodyFont : bodyFont = CreateDynamicFont(Resources.Load<Font>("Fonts/AlegreyaSans-Medium"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (hooked) return;
        hooked = true;
        var body = BodyFont;
        var def = TMP_Settings.defaultFontAsset;
        if (body != null && def != null)
        {
            if (def.fallbackFontAssetTable == null) def.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!def.fallbackFontAssetTable.Contains(body)) def.fallbackFontAssetTable.Add(body);
        }
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private static void OnTextChanged(Object obj)
    {
        if (obj is TMP_Text text) Swap(text);
    }

    private static void Swap(TMP_Text text)
    {
        if (text == null || bodyFont == null) return;
        var def = TMP_Settings.defaultFontAsset;
        if (text.font != def || def == null) return;
        // Font değişimi malzemeyi sıfırlar: kontur/yüz/gölge değerlerini koru.
        float ow = text.outlineWidth; Color oc = text.outlineColor; Color fc = text.faceColor;
        text.font = bodyFont;
        text.fontSharedMaterial = bodyFont.material;
        text.faceColor = fc;
        if (ow > 0f) { text.outlineWidth = 0f; text.outlineWidth = ow; text.outlineColor = oc; }
    }

    /// <summary>Dinamik atlaslı, Türkçe karakterleri önceden eklenmiş font; shader build'de yoksa varsayılandan alır.</summary>
    public static TMP_FontAsset CreateDynamicFont(Font source)
    {
        if (source == null) return null;
        var asset = TMP_FontAsset.CreateFontAsset(source);
        if (asset == null) return null;
        asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        var def = TMP_Settings.defaultFontAsset;
        if (asset.material != null && def != null && def.material != null &&
            (asset.material.shader == null || asset.material.shader.name.StartsWith("Hidden/")))
            asset.material.shader = def.material.shader;
        if (!asset.TryAddCharacters(ExtraCharacters, out string missing) && !string.IsNullOrEmpty(missing))
            Debug.LogWarning($"[UI Font] {source.name} eksik karakterler: {missing}");
        return asset;
    }

    /// <summary>Test/tanı için: dinamik font Türkçe + "Dalga" glifleri ekleyebiliyor mu, fallback zinciri kurulu mu, atanmamış yazı değişiyor mu.</summary>
    public static bool SelfCheck(out string report)
    {
        report = "";
        var font = BodyFont;
        if (font == null) { report = "BodyFont yok"; return false; }
        if (!font.TryAddCharacters("Dalga 1 " + ExtraCharacters, out string missing)) { report = "eksik glif: " + missing; return false; }
        if (font.material == null || font.material.shader == null) { report = "shader yok"; return false; }
        var def = TMP_Settings.defaultFontAsset;
        if (def == null || def.fallbackFontAssetTable == null || !def.fallbackFontAssetTable.Contains(font)) { report = "fallback zinciri yok"; return false; }
        var go = new GameObject("tmpSelfCheck", typeof(RectTransform), typeof(TextMeshProUGUI));
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = "Dalga 1";
        tmp.outlineWidth = 0.2f;
        float before = tmp.outlineWidth;
        Swap(tmp);
        if (Mathf.Abs(tmp.outlineWidth - 0.2f) > 0.01f) { Object.DestroyImmediate(go); report = "kontur korunmadi: once=" + before + " sonra=" + tmp.outlineWidth; return false; }
        bool swapped = tmp.font != def;
        Object.DestroyImmediate(go);
        if (!swapped) { report = "atanmamis yazi varsayilan fontta kaldi"; return false; }
        return true;
    }
}
