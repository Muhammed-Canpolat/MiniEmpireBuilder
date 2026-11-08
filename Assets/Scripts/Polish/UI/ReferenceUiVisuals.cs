using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Visual-only helpers shared by the three reference screens.
public static class ReferenceUiVisuals
{
    public static readonly Color Background = Hex(0x14110D);
    public static readonly Color Panel = Hex(0x2A2118);
    public static readonly Color Border = Hex(0x6B4A22);
    public static readonly Color Light = Hex(0xF2E6C9);
    public static readonly Color Muted = Hex(0xC9B88F);
    public static readonly Color Gold = Hex(0xF0C24A);
    public static readonly Color Red = Hex(0xC2452D);
    public static readonly Color Blue = Hex(0x3E7FB0);
    public static readonly Color Green = Hex(0x2F8F4E);

    private static Sprite goldSprite;
    private static Sprite darkSprite;
    private static TMP_FontAsset titleFont;
    private static TMP_FontAsset bodyFont;
    private static TMP_FontAsset bodyBoldFont;
    private static Sprite toastSprite;
    private static Sprite pillSprite;
    private const string TurkishCharacters = "ĞÜŞİÖÇğüşıöç";

    public static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    public static void ConfigureCanvas(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390, 844);
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static RectTransform SafeArea(Transform parent)
    {
        var obj = new GameObject("SafeArea", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rt = (RectTransform)obj.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        obj.AddComponent<ReferenceSafeArea>();
        return rt;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rt = (RectTransform)obj.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = low;
        rt.offsetMax = high;
        return rt;
    }

    public static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high, Color color)
    {
        var rt = Rect(parent, name, min, max, low, high);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max, Vector2 low, Vector2 high, bool title = false, bool bodyBold = false)
    {
        var rt = Rect(parent, name, min, max, low, high);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = value;
        tmp.alignment = alignment;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = Mathf.Max(10, size - 4);
        tmp.fontSizeMax = size;
        tmp.raycastTarget = false;
        ApplyFont(tmp, size, color, title, bodyBold);
        return tmp;
    }

    public static void ApplyFont(TextMeshProUGUI tmp, float size, Color color, bool title = false, bool bodyBold = false)
    {
        if (tmp == null) return;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Normal;
        // Runtime font assets are generated from the bundled OFL font files.
        if (title && titleFont == null)
        {
            var font = Resources.Load<Font>("Fonts/Cinzel-ExtraBold");
            if (font != null) titleFont = CreateTurkishFont(font);
        }
        if (!title && bodyFont == null)
        {
            var font = Resources.Load<Font>("Fonts/AlegreyaSans-Medium");
            if (font != null) bodyFont = CreateTurkishFont(font);
        }
        if (bodyBold && bodyBoldFont == null)
        {
            var font = Resources.Load<Font>("Fonts/AlegreyaSans-Bold");
            if (font != null) bodyBoldFont = CreateTurkishFont(font);
        }
        var chosen = title ? titleFont : bodyBold ? bodyBoldFont : bodyFont;
        if (chosen != null) tmp.font = chosen;
    }

    private static TMP_FontAsset CreateTurkishFont(Font source)
    {
        var asset = TMP_FontAsset.CreateFontAsset(source);
        asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        if (!asset.TryAddCharacters(TurkishCharacters, out string missing) && !string.IsNullOrEmpty(missing))
            Debug.LogWarning($"[UI Font] {source.name} eksik Türkçe karakterler: {missing}");
        return asset;
    }

    public static Sprite ToastSprite()
    {
        if (toastSprite != null) return toastSprite;
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = Mathf.Max(0, 10 - x, x - 21);
            float dy = Mathf.Max(0, 10 - y, y - 21);
            bool inside = dx * dx + dy * dy <= 100;
            bool edge = inside && (x < 2 || x >= 30 || y < 2 || y >= 30
                || dx * dx + dy * dy > 64);
            tex.SetPixel(x, y, !inside ? Color.clear : edge ? Border :
                new Color(Background.r, Background.g, Background.b, 0.90f));
        }
        tex.Apply();
        toastSprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        return toastSprite;
    }

    public static Sprite PillSprite()
    {
        if (pillSprite != null) return pillSprite;
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = x - 15.5f, dy = y - 15.5f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            texture.SetPixel(x, y, radius > 15.5f ? Color.clear :
                radius > 13.5f ? Border : new Color(Background.r, Background.g, Background.b, 0.94f));
        }
        texture.Apply();
        pillSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32),
            new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
            new Vector4(15, 15, 15, 15));
        return pillSprite;
    }

    public static Button Button(Transform parent, string name, string label, bool gold,
        Vector2 min, Vector2 max, Vector2 low, Vector2 high, float fontSize)
    {
        var rt = Rect(parent, name, min, max, low, high);
        var image = rt.gameObject.AddComponent<Image>();
        var button = rt.gameObject.AddComponent<Button>();
        ApplyButtonSkin(button, gold);
        var txt = Text(rt, "Label", label, fontSize, gold ? Hex(0x2A1A08) : Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0), true);
        txt.characterSpacing = 1.6f;
        return button;
    }

    public static void ApplyButtonSkin(Button button, bool gold)
    {
        var image = button.GetComponent<Image>();
        image.sprite = gold ? goldSprite ??= MakeButtonSprite(true) : darkSprite ??= MakeButtonSprite(false);
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.color = Color.white;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        if (button.GetComponent<ReferenceButtonPress>() == null)
            button.gameObject.AddComponent<ReferenceButtonPress>();
    }

    private static Sprite MakeButtonSprite(bool gold)
    {
        var tex = new Texture2D(32, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 32; x++)
        {
            bool inside = x >= 2 && x < 30 && y >= 2 && y < 62;
            if ((x < 10 || x >= 22) && (y < 10 || y >= 54))
            {
                float dx = x < 10 ? 10 - x : x - 21;
                float dy = y < 10 ? 10 - y : y - 53;
                inside = dx * dx + dy * dy <= 100;
            }
            Color fill = gold ? Color.Lerp(Hex(0xB97A1C), Gold, y / 63f) : Panel;
            bool edge = inside && (x < 4 || x >= 28 || y < 4 || y >= 60);
            tex.SetPixel(x, y, !inside ? Color.clear : edge ? (gold ? Hex(0x8A5A1E) : Border) : fill);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
    }
}

public sealed class ReferenceSafeArea : MonoBehaviour
{
    private RectTransform rt;
    private Rect last;
    private void Awake() { rt = (RectTransform)transform; Apply(); }
    private void Update() { if (last != Screen.safeArea) Apply(); }
    private void Apply()
    {
        last = Screen.safeArea;
        rt.anchorMin = new Vector2(last.xMin / Screen.width, last.yMin / Screen.height);
        rt.anchorMax = new Vector2(last.xMax / Screen.width, last.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

/// <summary>Fills device edges and keeps the world camera between the HUD bars.</summary>
public sealed class WorldViewportBars : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform top;
    private RectTransform bottom;
    private float topHeight;
    private float bottomHeight;
    private float worldWidth;
    private Rect lastSafe;
    private Vector2Int lastSize;
    private bool pendingRefresh;
    public RectTransform TopBar => top;
    public RectTransform BottomBar => bottom;

    public void Initialize(Canvas owner, float topPixels, float bottomPixels, float width)
    {
        canvas = owner;
        topHeight = topPixels;
        bottomHeight = bottomPixels;
        worldWidth = width;
        top = MakeBar("ScreenTopFill", true);
        bottom = MakeBar("ScreenBottomFill", false);
        UpdateLayout();
        StartCoroutine(RefreshAfterLayout());
    }

    private System.Collections.IEnumerator RefreshAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        UpdateLayout();
        pendingRefresh = false;
    }

    private RectTransform MakeBar(string name, bool atTop)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, atTop ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = ReferenceUiVisuals.Hex(0x14110D);
        image.raycastTarget = false;
        return rt;
    }

    private void LateUpdate()
    {
        if (lastSafe != Screen.safeArea || lastSize.x != Screen.width || lastSize.y != Screen.height)
        {
            UpdateLayout();
            if (!pendingRefresh) { pendingRefresh = true; StartCoroutine(RefreshAfterLayout()); }
        }
        else if (NeedsHudAlignment()) UpdateLayout();
    }

    private bool NeedsHudAlignment()
    {
        var camera = Camera.main;
        if (camera == null) return false;
        var hudTop = canvas.transform.Find("SafeArea/HudRoot/TopBarBG") as RectTransform
            ?? canvas.transform.Find("SafeArea/TopBar") as RectTransform;
        var hudBottom = canvas.transform.Find("SafeArea/HudRoot/BottomDock") as RectTransform
            ?? canvas.transform.Find("SafeArea/BattleBottomDock") as RectTransform;
        if (hudTop == null || hudBottom == null) return false;
        var corners = new Vector3[4];
        hudTop.GetWorldCorners(corners);
        float upper = corners[0].y;
        hudBottom.GetWorldCorners(corners);
        return Mathf.Abs(camera.pixelRect.yMax - upper) > 0.75f ||
            Mathf.Abs(camera.pixelRect.yMin - corners[2].y) > 0.75f;
    }

    public void UpdateLayout()
    {
        if (canvas == null || Screen.width <= 0 || Screen.height <= 0) return;
        lastSafe = Screen.safeArea;
        lastSize = new Vector2Int(Screen.width, Screen.height);
        float scale = Mathf.Max(0.01f, canvas.scaleFactor);
        var hudTop = canvas.transform.Find("SafeArea/HudRoot/TopBarBG") as RectTransform
            ?? canvas.transform.Find("SafeArea/TopBar") as RectTransform;
        var hudBottom = canvas.transform.Find("SafeArea/HudRoot/BottomDock") as RectTransform
            ?? canvas.transform.Find("SafeArea/BattleBottomDock") as RectTransform;
        float lower = lastSafe.yMin + bottomHeight * scale;
        float upper = lastSafe.yMax - topHeight * scale;
        Canvas.ForceUpdateCanvases();
        if (hudTop != null)
        {
            var corners = new Vector3[4];
            hudTop.GetWorldCorners(corners);
            upper = corners[0].y;
        }
        if (hudBottom != null)
        {
            var corners = new Vector3[4];
            hudBottom.GetWorldCorners(corners);
            lower = corners[2].y;
        }
        top.sizeDelta = new Vector2(0f, (Screen.height - upper) / scale);
        bottom.sizeDelta = new Vector2(0f, lower / scale);
        var camera = Camera.main;
        if (camera == null) return;
        lower = Mathf.Clamp(lower, 0f, Screen.height - 1f);
        upper = Mathf.Clamp(upper, lower + 1f, Screen.height);
        camera.pixelRect = new Rect(0f, lower, Screen.width, upper - lower);
        camera.aspect = camera.pixelRect.width / Mathf.Max(1f, camera.pixelRect.height);
        if (worldWidth > 0f)
        {
            float widthSize = worldWidth / (2f * camera.aspect);
            camera.orthographicSize = Mathf.Max(widthSize, BaseWorldLayout.Height * 0.5f);
            camera.transform.position = new Vector3(worldWidth * 0.5f,
                BaseWorldLayout.Height * 0.5f, camera.transform.position.z);
        }
    }
}

public sealed class ReferenceButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public void OnPointerDown(PointerEventData e) { transform.DOKill(); transform.DOScale(0.92f, 0.08f); }
    public void OnPointerUp(PointerEventData e) { transform.DOKill(); transform.DOScale(1f, 0.12f); }
    public void OnPointerExit(PointerEventData e) { transform.DOKill(); transform.DOScale(1f, 0.12f); }
}
