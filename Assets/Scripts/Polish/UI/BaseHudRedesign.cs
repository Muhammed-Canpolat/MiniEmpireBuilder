using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Üs HUD'ının görsel dili: süslü ahşap üst şerit, ortada seviye arması (renk kademesine
/// göre boyanan kalkan), altında saniyelik gelir; altta İnşa Et · SEFER · Kahraman.
/// Mevcut düğmelerin dinleyicileri korunur; yalnız yerleşim ve görünüm değişir.
/// </summary>
public static class BaseHudRedesign
{
    private const float DockButtonWidth = 104f;
    private const float DockButtonHeight = 70f;
    private const float CenterButtonWidth = 128f;
    private const float CenterButtonHeight = 80f;

    private static readonly Color[] TierColors =
    {
        ReferenceUiVisuals.Hex(0x3E7FB0), // 1–3 mavi
        ReferenceUiVisuals.Hex(0xC99A2E), // 4–6 sarı
        ReferenceUiVisuals.Hex(0xB0402C), // 7–9 kırmızı
        ReferenceUiVisuals.Hex(0x7A4BA6), // 10–12 mor
        ReferenceUiVisuals.Hex(0x2C2C36)  // 13–15 siyah
    };

    public static Color TierColor(int level) => TierColors[Progression.ColorTier(level)];

    public static void Apply(Transform hud)
    {
        if (hud == null) return;
        StyleTopBar(hud);
        StyleDock(hud);
    }

    // ==================== ÜST BAR ====================

    private static void StyleTopBar(Transform hud)
    {
        var top = hud.Find("TopBarBG") as RectTransform;
        if (top != null && top.Find("Ornament") == null)
        {
            // Ana menüdeki çerçevenin üst şeridi: oranı (≈6.2:1) 390×64 bara birebir uyar.
            var strip = OrnateStrip(true);
            if (strip != null)
            {
                var ornament = ReferenceUiVisuals.Image(top, "Ornament", Vector2.zero, Vector2.one,
                    new Vector2(-4f, -2f), new Vector2(4f, 2f), Color.white);
                ornament.sprite = strip;
            }
        }

        // Altın: koyu hap içinde miktar, altında saniyelik gelir.
        var gold = hud.Find("GoldPill") as RectTransform;
        if (gold != null)
        {
            SetAnchors(gold, new Vector2(0.025f, 0.931f), new Vector2(0.27f, 0.983f));
            var bg = gold.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = ReferenceUiVisuals.PillSprite();
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            SetAnchors(gold.Find("CoinIcon") as RectTransform, new Vector2(0.04f, 0.16f), new Vector2(0.30f, 0.84f));
            var amount = gold.Find("GoldText") as RectTransform;
            SetAnchors(amount, new Vector2(0.32f, 0.40f), new Vector2(0.96f, 0.98f));
            var gps = gold.Find("GpsText") as RectTransform;
            SetAnchors(gps, new Vector2(0.32f, 0.04f), new Vector2(0.96f, 0.46f));
            var gpsText = gps != null ? gps.GetComponent<TextMeshProUGUI>() : null;
            if (gpsText != null)
            {
                ReferenceUiVisuals.ApplyFont(gpsText, 10f, ReferenceUiVisuals.Hex(0x9FD37E), false, true);
                gpsText.alignment = TextAlignmentOptions.Left;
                gpsText.enableAutoSizing = false;
            }
            var amountText = amount != null ? amount.GetComponent<TextMeshProUGUI>() : null;
            if (amountText != null) amountText.alignment = TextAlignmentOptions.Left;
        }

        // Elmas: altının yanında koyu hap; dokununca nasıl kazanılacağını söyler.
        if (hud.Find("GemPill") == null)
        {
            var gemButton = ReferenceUiVisuals.Button(hud, "GemPill", "", false,
                new Vector2(0.28f, 0.936f), new Vector2(0.415f, 0.978f), Vector2.zero, Vector2.zero, 12);
            var gemBg = gemButton.GetComponent<Image>();
            gemBg.sprite = ReferenceUiVisuals.PillSprite();
            gemBg.type = Image.Type.Sliced;
            var oldLabel = gemButton.transform.Find("Label");
            if (oldLabel != null) Object.Destroy(oldLabel.gameObject);
            var gemIcon = ReferenceUiVisuals.Image(gemButton.transform, "GemIcon", new Vector2(0.05f, 0.12f),
                new Vector2(0.38f, 0.88f), Vector2.zero, Vector2.zero, Color.white);
            gemIcon.sprite = GemIcon.Sprite;
            gemIcon.preserveAspect = true;
            var gemText = ReferenceUiVisuals.Text(gemButton.transform, "GemText", "0", 15,
                ReferenceUiVisuals.Light, TextAlignmentOptions.Left, new Vector2(0.40f, 0.05f),
                new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero, bodyBold: true);
            var counter = gemButton.gameObject.AddComponent<GemCounter>();
            counter.Bind(gemText);
            gemButton.onClick.AddListener(() => Object.FindFirstObjectByType<BaseSceneSetup>()?.ShowHudToast(
                counter.RegisterTestTap() ? "[TEST] +100 elmas"
                    : "Elmas: giriş ödülleri ve mağazadan kazanılır; inşaatçı ve hızlandırmada kullanılır"));
        }

        // Eski "ANA ÜS · SEV. N" yazısı yerine arma.
        var levelPill = hud.Find("BattleLevelPill");
        if (levelPill != null) levelPill.gameObject.SetActive(false);
        if (hud.Find("LevelCrest") == null)
        {
            var crest = ReferenceUiVisuals.Rect(hud, "LevelCrest", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-27f, -66f), new Vector2(27f, -2f));
            crest.gameObject.AddComponent<LevelCrest>().Build(crest);
        }

        // İnşaatçı göstergesi de koyu hap.
        var builder = hud.Find("BuilderPill") as RectTransform;
        if (builder != null)
        {
            SetAnchors(builder, new Vector2(0.70f, 0.936f), new Vector2(0.87f, 0.978f));
            var bg = builder.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = ReferenceUiVisuals.PillSprite();
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
        }

        // Seviye yalnız armada: altındaki çubuk ve "Sev. N için…" yazısı gösterilmez.
        foreach (string name in new[] { "BaseLevelTrack", "BaseLevelCaption" })
        {
            var item = hud.Find(name);
            if (item != null) item.gameObject.SetActive(false);
        }
    }

    // ==================== ALT BAR ====================

    private static void StyleDock(Transform hud)
    {
        SkinDock(hud.Find("BottomDock") as RectTransform);
        var build = hud.Find("BuildBtn") as RectTransform;
        var battle = hud.Find("BattleBtn") as RectTransform;
        StackedButton(build, new Vector2(0.17f, 0f), DockButtonWidth, DockButtonHeight, 46f, false, "İNŞA ET");
        StackedButton(battle, new Vector2(0.5f, 0f), CenterButtonWidth, CenterButtonHeight, 52f, true, "SEFER");

        if (hud.Find("HeroBtn") == null)
        {
            var hero = ReferenceUiVisuals.Button(hud, "HeroBtn", "", false,
                new Vector2(0.83f, 0f), new Vector2(0.83f, 0f), Vector2.zero, Vector2.zero, 13);
            var icon = ReferenceUiVisuals.Image(hero.transform, "Icon", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Color.white);
            icon.preserveAspect = true;
            var gm = GameManager.Instance;
            if (gm?.PlayerData != null && SpriteManager.Instance != null)
                icon.sprite = HeroPortraitSprite.Get(SpriteManager.Instance.GetHeroSprite(gm.PlayerData.hero.weaponType));
            var label = hero.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.name = "Label";
            hero.onClick.AddListener(() => BaseWorldUI.Instance?.ShowHeroInfo());
            StackedButton((RectTransform)hero.transform, new Vector2(0.83f, 0f), DockButtonWidth,
                DockButtonHeight, 46f, false, "KAHRAMAN");
        }
    }

    /// <summary>Simge üstte, yazı altta; orta düğme daha büyük ve dock'tan hafif taşar.</summary>
    private static void StackedButton(RectTransform rt, Vector2 anchor, float width, float height,
        float centerY, bool gold, string text)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, centerY);
        var button = rt.GetComponent<Button>();
        if (button != null) ReferenceUiVisuals.ApplyButtonSkin(button, gold);
        var icon = rt.Find("Icon") as RectTransform;
        if (icon != null)
        {
            SetAnchors(icon, new Vector2(0.27f, 0.38f), new Vector2(0.73f, 0.92f));
            var image = icon.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                image.preserveAspect = true;
            }
        }
        var label = rt.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = text;
            SetAnchors(label.rectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.38f));
            ReferenceUiVisuals.ApplyFont(label, gold ? 14f : 11f,
                gold ? ReferenceUiVisuals.Hex(0x2A1A08) : ReferenceUiVisuals.Light, true);
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = gold ? 14f : 11f;
            label.characterSpacing = 1f;
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    /// <summary>
    /// Dock: koyu meşe tahta dokusu, altın üst kenarlık ve altın perçinler. Eski gri çelik
    /// raylar ve iç çerçeve gizlenir (ekranın sağında/solunda görünen grilik onlardı).
    /// </summary>
    public static void SkinDock(RectTransform dock)
    {
        if (dock == null || dock.Find("WoodGrainShade") != null) return;
        var image = dock.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = WoodPlankSprite();
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 0.5f; // tahta başına ~16 birim yükseklik
            image.color = Color.white;
        }
        foreach (string name in new[] { "LeftRail", "RightRail", "BottomRule", "DockInset", "TopShadow" })
        {
            var part = dock.Find(name);
            if (part != null) part.gameObject.SetActive(false);
        }
        // Üstten aşağı koyulaşan gölge: düğmeler tahta üstünde okunur kalsın.
        var shade = ReferenceUiVisuals.Image(dock, "WoodGrainShade", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, Color.white);
        shade.sprite = VerticalShade();
        shade.transform.SetSiblingIndex(0);
        var rule = dock.Find("TopRule")?.GetComponent<Image>();
        if (rule != null)
        {
            rule.color = ReferenceUiVisuals.Gold;
            SetAnchors(rule.rectTransform, new Vector2(0f, 1f), Vector2.one);
            rule.rectTransform.offsetMin = new Vector2(0f, -3f);
            rule.transform.SetAsLastSibling();
        }
        var line = ReferenceUiVisuals.Image(dock, "TopRuleShadow", new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -6f), new Vector2(0f, -3f), ReferenceUiVisuals.Hex(0x1A100A));
        line.transform.SetAsLastSibling();
        foreach (Transform child in dock)
        {
            if (!child.name.StartsWith("DockRivet_")) continue;
            var rivet = child.GetComponent<Image>();
            if (rivet == null) continue;
            rivet.sprite = ReferenceUiVisuals.SolidPillSprite();
            rivet.color = ReferenceUiVisuals.Hex(0xD9A93C);
            child.SetAsLastSibling();
        }
    }

    private static Sprite woodPlanks;
    private static Sprite verticalShade;

    /// <summary>Yatay meşe tahtalar: koyu kahve, damar çizgileri, tahta aralarında derz.</summary>
    private static Sprite WoodPlankSprite()
    {
        if (woodPlanks != null) return woodPlanks;
        const int w = 64, h = 32, plank = 8;
        var random = new System.Random(511);
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        Color baseColor = ReferenceUiVisuals.Hex(0x3A2616);
        Color dark = ReferenceUiVisuals.Hex(0x24170D);
        Color light = ReferenceUiVisuals.Hex(0x4B3220);
        for (int y = 0; y < h; y++)
        {
            int row = y / plank;
            float rowTone = (float)random.NextDouble() * 0.12f - 0.06f;
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(baseColor, row % 2 == 0 ? light : dark, 0.25f + rowTone);
                if (y % plank == 0) c = dark;                                   // derz
                else if (y % plank == plank - 1) c = Color.Lerp(c, light, 0.5f); // üst kenar ışığı
                else if (random.NextDouble() < 0.08) c = Color.Lerp(c, dark, 0.6f); // damar
                int seam = (row * 23) % w;                                       // tahta uç birleşimi
                if (x == seam && y % plank != 0) c = dark;
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();
        woodPlanks = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect);
        return woodPlanks;
    }

    private static Sprite VerticalShade()
    {
        if (verticalShade != null) return verticalShade;
        var texture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 32; y++)
            texture.SetPixel(0, y, new Color(0f, 0f, 0f, Mathf.Lerp(0.55f, 0.05f, y / 31f)));
        texture.Apply();
        verticalShade = Sprite.Create(texture, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0.5f), 100f);
        return verticalShade;
    }

    // ==================== YARDIMCI ====================

    private static Sprite topStrip;
    private static Sprite bottomStrip;

    /// <summary>Ana menü çerçevesinin üst (veya alt) şeridi, köşe süsleriyle birlikte.</summary>
    public static Sprite OrnateStrip(bool upper)
    {
        if (upper && topStrip != null) return topStrip;
        if (!upper && bottomStrip != null) return bottomStrip;
        var frames = Resources.LoadAll<Sprite>("Sprites/Intro/intro_button");
        if (frames.Length == 0) return null;
        Rect rect = frames[0].rect;
        float strip = rect.height * 0.29f;
        var crop = upper ? new Rect(rect.x, rect.yMax - strip, rect.width, strip)
            : new Rect(rect.x, rect.y, rect.width, strip);
        var sprite = Sprite.Create(frames[0].texture, crop, new Vector2(0.5f, 0.5f), frames[0].pixelsPerUnit);
        if (upper) topStrip = sprite; else bottomStrip = sprite;
        return sprite;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        if (rt == null) return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}

/// <summary>Ana Üs seviye arması: kalkan rengi kademeye göre, içinde seviye numarası.</summary>
public sealed class LevelCrest : MonoBehaviour
{
    private Image body;
    private TextMeshProUGUI number;
    private int shownLevel = -1;

    public void Build(RectTransform root)
    {
        var rim = ReferenceUiVisuals.Image(root, "Rim", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
        rim.sprite = ShieldSprite();
        body = ReferenceUiVisuals.Image(root, "Body", Vector2.zero, Vector2.one,
            new Vector2(4f, 5f), new Vector2(-4f, -4f), Color.white);
        body.sprite = ShieldSprite();
        var shine = ReferenceUiVisuals.Image(body.rectTransform, "Shine", new Vector2(0f, 0.55f), Vector2.one,
            Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.16f));
        shine.sprite = ShieldSprite();
        number = ReferenceUiVisuals.Text(root, "Number", "1", 26, Color.white,
            TextAlignmentOptions.Center, new Vector2(0f, 0.22f), new Vector2(1f, 0.92f),
            Vector2.zero, Vector2.zero, title: true);
        number.enableAutoSizing = false;
        var caption = ReferenceUiVisuals.Text(root, "Caption", "ANA ÜS", 7, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, new Vector2(0f, 0.08f), new Vector2(1f, 0.3f),
            Vector2.zero, Vector2.zero, title: true);
        caption.enableAutoSizing = false;
        Refresh(true);
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnBuildingUpgraded += OnUpgraded;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnBuildingUpgraded -= OnUpgraded;
    }

    private void OnUpgraded(BuildingType type, int level)
    {
        if (type == BuildingType.MainBase) Refresh(false);
    }

    private void Refresh(bool silent)
    {
        if (body == null || number == null) return;
        int level = GameManager.Instance != null ? GameManager.Instance.GetMainBaseLevel() : 1;
        if (level == shownLevel) return;
        shownLevel = level;
        number.text = level.ToString();
        body.color = BaseHudRedesign.TierColor(level);
        if (silent) return;
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.25f, 0.45f, 6, 0.8f);
    }

    private static Sprite shield;

    /// <summary>Üstü düz, alt yarısı sivri uca daralan kalkan (beyaz; Image.color ile boyanır).</summary>
    private static Sprite ShieldSprite()
    {
        if (shield != null) return shield;
        const int w = 64, h = 74;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float fromTop = (h - 1 - y) / (float)(h - 1);          // 0 üst, 1 uç
            float halfWidth = fromTop < 0.5f ? 0.5f
                : 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - (fromTop - 0.5f) / 0.5f));
            float dx = Mathf.Abs((x + 0.5f) / w - 0.5f);
            float edge = (halfWidth - dx) * w;                       // piksel cinsinden içerilik
            float topEdge = (h - 1 - y) + 0.5f;
            float alpha = Mathf.Clamp01(Mathf.Min(edge, topEdge));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        shield = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return shield;
    }
}
