using System.Collections.Generic;
using System.Linq;
using MiniEmpire.Intro;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Açılış intro sahnesini (Assets/Scenes/Intro.unity) hazır paketten kurar, sprite import ayarlarını (mobil) uygular ve
/// Build Settings'te Intro'yu index 0 yapar (diğer sahnelerin sırası korunur). Tekrar çalıştırılabilir (sahneyi baştan yazar).
/// Ölçüler referans görselden (Docs/UIReference/intro-referans.jpeg, 941x1672) alınmıştır.
/// Komut satırı: -executeMethod IntroSceneBuilder.BuildCli
/// </summary>
public static class IntroSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Intro.unity";
    public const string GameSceneName = "MainMenuScene";
    private const string SpriteDir = "Assets/MiniEmpire_Intro/Assets/MiniEmpire/Sprites/Intro/";
    private const string Background = SpriteDir + "intro_background.png";
    private const string Frame = SpriteDir + "loading_bar_frame.png";
    private const string Fill = SpriteDir + "loading_bar_fill.png";
    private const string Bulb = SpriteDir + "icon_tip_bulb.png";
    private const string Logo = SpriteDir + "logo_mini_empire_builder.png";   // kullanılmıyor (logo arka plana pişmiş; çift logo yok)
    private static readonly Vector2 RefSize = new Vector2(941f, 1672f);
    private static readonly Color DecorGold = new Color32(0xE8, 0xAE, 0x3C, 0xFF);

    [MenuItem("Tools/Intro/Intro Sahnesini Kur")]
    public static void Build()
    {
        ConfigureImports();
        CreateScene();
        RegisterBuildScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[IntroSceneBuilder] Intro sahnesi kuruldu: " + ScenePath);
    }

    public static void BuildCli()
    {
        try { Build(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError("[IntroSceneBuilder] " + e); EditorApplication.Exit(1); }
    }

    // ── Import ayarları (madde 9) ───────────────────────────────────────────
    private static void ConfigureImports()
    {
        Configure(Background, true);
        Configure(Frame, false);
        Configure(Fill, false);
        Configure(Bulb, false);
        Configure(Logo, false);
    }

    private static void Configure(string path, bool illustration)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new System.Exception("Sprite bulunamadı: " + path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spritePixelsPerUnit = 100f;
        ti.SetTextureSettings(s);
        ti.mipmapEnabled = false;
        ti.isReadable = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;
        ti.textureCompression = illustration ? TextureImporterCompression.Compressed : TextureImporterCompression.CompressedHQ;
        var format = illustration ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
        foreach (var platform in new[] { "iPhone", "Android" })
        {
            var ps = ti.GetPlatformTextureSettings(platform);
            ps.overridden = true;
            ps.maxTextureSize = 2048;
            ps.format = format;
            ps.compressionQuality = illustration ? 50 : 100;
            ti.SetPlatformTextureSettings(ps);
        }
        ti.SaveAndReimport();
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) throw new System.Exception("Sprite yüklenemedi: " + path);
        return sprite;
    }

    // ── Sahne ───────────────────────────────────────────────────────────────
    private static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;   // fade-out sonrası ve etkinleşme anında siyah
        camGo.AddComponent<AudioListener>();

        var canvasGo = new GameObject("IntroCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = RefSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // Arka plan (madde 10): dar/uzun ekranda Envelope (yanlar kırpılır), tablette tam sığdırma + karartılmış kaplayan dolgu.
        var bgSprite = LoadSprite(Background);
        var bgFill = NewImage("BackgroundFill", canvasGo.transform, bgSprite);
        Stretch(bgFill.rectTransform);
        bgFill.color = new Color(0.45f, 0.45f, 0.5f, 1f);
        var fillFitter = bgFill.gameObject.AddComponent<AspectRatioFitter>();
        fillFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fillFitter.aspectRatio = RefSize.x / RefSize.y;
        var bg = NewImage("Background", canvasGo.transform, bgSprite);
        Stretch(bg.rectTransform);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = RefSize.x / RefSize.y;
        bg.gameObject.AddComponent<IntroBackgroundFit>();

        var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(canvasGo.transform, false);
        Stretch(safe);
        safe.gameObject.AddComponent<ReferenceSafeArea>();

        // Yükleme çubuğu (referans: 605x64, merkez alttan 177)
        var barRt = NewRect("LoadingBar", safe, new Vector2(0.5f, 0f), new Vector2(605f, 64f), new Vector2(0f, 177f));
        var frame = NewImage("Frame", barRt, LoadSprite(Frame));
        Stretch(frame.rectTransform);
        var fill = NewImage("Fill", barRt, LoadSprite(Fill));
        var fillRt = fill.rectTransform;
        fillRt.anchorMin = fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 1f);
        fillRt.sizeDelta = new Vector2(584f, 42f);
        fillRt.anchoredPosition = new Vector2(10f, -10f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        var percent = NewText("PercentText", barRt, 40f, "%0");
        Stretch(percent.rectTransform);
        percent.rectTransform.offsetMin = new Vector2(0f, 2f);
        var bar = barRt.gameObject.AddComponent<LoadingBar>();
        SetRef(bar, "fillImage", fill);
        SetRef(bar, "percentText", percent);

        // İpucu alanı (referans: merkez alttan 93; ampul x -180, yazı x +10, yanlarda ince altın çizgiler)
        var tipRt = NewRect("TipArea", safe, new Vector2(0.5f, 0f), new Vector2(760f, 84f), new Vector2(0f, 93f));
        Decor(tipRt, "LineLeft", new Vector2(-285f, 0f), new Vector2(130f, 3f), 0f);
        Decor(tipRt, "DotLeft", new Vector2(-220f, 0f), new Vector2(8f, 8f), 45f);
        Decor(tipRt, "LineRight", new Vector2(258f, 0f), new Vector2(144f, 3f), 0f);
        Decor(tipRt, "DotRight", new Vector2(186f, 0f), new Vector2(8f, 8f), 45f);
        var icon = NewImage("Icon", tipRt, LoadSprite(Bulb));
        icon.rectTransform.sizeDelta = new Vector2(50f, 60f);
        icon.rectTransform.anchoredPosition = new Vector2(-180f, 0f);
        var tip = NewText("TipText", tipRt, 28f, "");
        tip.rectTransform.sizeDelta = new Vector2(330f, 84f);
        tip.rectTransform.anchoredPosition = new Vector2(10f, 0f);
        tip.textWrappingMode = TextWrappingModes.Normal;
        tip.enableAutoSizing = true;
        tip.fontSizeMin = 20f;
        tip.fontSizeMax = 28f;
        tip.lineSpacing = -8f;
        var rotator = tipRt.gameObject.AddComponent<TipRotator>();
        SetRef(rotator, "tipText", tip);

        var fonts = canvasGo.AddComponent<IntroTextFont>();
        SetArray(fonts, "texts", new Object[] { percent, tip });

        var controllerGo = new GameObject("IntroSceneController");
        var controller = controllerGo.AddComponent<IntroSceneController>();
        SetRef(controller, "loadingBar", bar);
        SetRef(controller, "rootGroup", group);
        var so = new SerializedObject(controller);
        so.FindProperty("gameSceneName").stringValue = GameSceneName;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void RegisterBuildScene()
    {
        var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────────
    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void Decor(Transform parent, string name, Vector2 pos, Vector2 size, float rot)
    {
        var img = NewImage(name, parent, null);   // sprite yok: düz renkli çizgi/nokta (yeni sanat üretilmez)
        img.color = DecorGold;
        img.rectTransform.sizeDelta = size;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, rot);
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, float size, string text)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.text = text;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;   // kontur çalışma anında IntroTextFont ile (sahneye örnek malzeme kaydedilmez)
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) throw new System.Exception($"{target.GetType().Name}.{field} bulunamadı");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string field, IList<Object> values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
