using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Üs yeni üst bar görünümü (D106, UI_DEGISIKLIK §1). Yalnız görünüm: mevcut nesneler (TopBarBG, GoldPill, GemPill,
/// BuilderPill, MeatPill/WoodPill, LevelCrest) ve bağları aynen kalır; sprite, anchor ve yazı yerleşimi değişir.
/// Tüm yerleşim anchor + ref. tuval (390x844, Canvas Scaler) birimidir; sabit piksel yok. Ada/Sefer/Savaş barlarına dokunmaz.
/// </summary>
public static class MiniEmpireTopBarSkin
{
    /// <summary>Paket 886 px genişlik için çizildi; ref. tuval 390 → 9-slice kenarlar bu oranda küçülür.</summary>
    public const float SliceMultiplier = 886f / 390f;
    public const float BarHeight = 47f, PillHeight = 33f, ShieldWidth = 50f, ShieldHeight = 62f, GearSize = 40f;

    public static void Apply(Transform hud)
    {
        if (hud == null || !MiniEmpireUiCatalog.TopBarReady) return;
        var c = MiniEmpireUiCatalog.Instance;

        // Eski süslü şerit gizlenir; TopBarBG adı/üst sınırı korunur (kamera çerçevesi okur).
        var top = hud.Find("TopBarBG") as RectTransform;
        if (top != null)
        {
            var ornament = top.Find("Ornament");
            if (ornament != null) ornament.gameObject.SetActive(false);
            var bg = top.GetComponent<Image>();
            if (bg != null) { bg.sprite = c.topbarBg; Slice(bg); bg.color = Color.white; bg.raycastTarget = false; }
            TopRect(top, 0f, 1f, 0f, BarHeight);
            top.SetAsFirstSibling();
        }

        BuildBanner(hud, "BannerLeft", c.bannerLeft, -30f);
        BuildBanner(hud, "BannerRight", c.bannerRight, 30f);

        // Sol/sağ yuvalar: sıra FeatureConfig.baseTopBarOrder (varsayılan gold,wood,meat | gem,builder).
        var bar = hud.GetComponentInChildren<ResourceTopBar>(true);
        var order = ResourceTopBar.Order(FeatureConfig.Instance.baseTopBarOrder, ResourceTopBar.DefaultBaseOrder);
        int left = (order.Count + 1) / 2;
        float[] cursor = { 0.02f, 0.60f };   // sol grup 0,02–0,40 · sağ grup 0,60–0,855 (referans)
        float[] total = new float[2];
        for (int i = 0; i < order.Count; i++) total[i < left ? 0 : 1] += SlotWeight(order[i]);
        for (int i = 0; i < order.Count; i++)
        {
            int group = i < left ? 0 : 1;
            float span = (group == 0 ? 0.38f : 0.255f) * SlotWeight(order[i]) / total[group];
            float x0 = cursor[group] + 0.003f, x1 = cursor[group] + span - 0.003f;
            cursor[group] += span;
            string id = order[i];
            RectTransform pill = id == "gold" ? hud.Find("GoldPill") as RectTransform
                : id == "gem" ? hud.Find("GemPill") as RectTransform
                : id == "builder" ? hud.Find("BuilderPill") as RectTransform
                : id == "meat" ? (bar != null && bar.Meat != null ? bar.Meat.rect : null)
                : (bar != null && bar.Wood != null ? bar.Wood.rect : null);
            if (pill == null) continue;
            pill.gameObject.SetActive(true);
            TopRect(pill, x0, x1, (BarHeight - PillHeight) * 0.5f, PillHeight);
            var image = pill.GetComponent<Image>();
            if (image != null) { image.sprite = c.resPill; Slice(image); image.color = Color.white; }
            switch (id)
            {
                case "gold": StylePill(pill, "CoinIcon", "GoldText", c.iconGold); break;
                case "gem": StylePill(pill, "GemIcon", "GemText", c.iconGem); break;
                case "builder": StylePill(pill, "AvatarIcon", "BuilderCount", c.iconBuilder); break;
                case "meat": StylePill(pill, "Icon", "Value", c.iconMeat); break;
                case "wood": StylePill(pill, "Icon", "Value", c.iconWood); break;
            }
        }

        // GpsText (altın/sn) yeni barda gösterilmez (yalnız görünüm; veri ResourceHUD'da güncellenmeye devam eder).
        var gps = hud.Find("GoldPill/GpsText");
        if (gps != null) gps.gameObject.SetActive(false);

        // Kalkan bayrakların önünde: seviye numarası LevelCrest içinde dinamik.
        var crest = hud.Find("LevelCrest") as RectTransform;
        if (crest != null)
        {
            TopRect(crest, 0.5f, 0.5f, 1.5f, ShieldHeight);
            crest.sizeDelta = new Vector2(ShieldWidth, ShieldHeight);
            crest.SetAsLastSibling();
        }
        // Et/Odun bilgi satırı üst sırada kalsın.
        var toast = bar != null ? bar.transform.Find("ResourceToast") : null;
        if (toast != null) toast.SetAsLastSibling();
    }

    /// <summary>Hap genişlik payı: altın (4-5 hane) daha geniş, et dar (referans oranları).</summary>
    private static float SlotWeight(string id) => id == "gold" ? 1.3f : id == "wood" ? 1.15f : id == "meat" ? 1.1f : id == "builder" ? 1.1f : 1f;

    private static void BuildBanner(Transform hud, string name, Sprite sprite, float centerX)
    {
        if (sprite == null) return;
        var existing = hud.Find(name);
        var rt = existing != null ? (RectTransform)existing : ReferenceUiVisuals.Rect(hud, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var image = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        // 48x128 px paket boyutu / 2,27 → ≈21x56 birim; barın ortasında, kalkanın iki yanında asılı.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(sprite.rect.width / SliceMultiplier, sprite.rect.height / SliceMultiplier);
        rt.anchoredPosition = new Vector2(centerX, 0f);
        rt.SetSiblingIndex(1);
    }

    private static void StylePill(RectTransform pill, string iconName, string textName, Sprite icon)
    {
        var iconRect = pill.Find(iconName) as RectTransform;
        if (iconRect != null)
        {
            var image = iconRect.GetComponent<Image>();
            if (image != null)
            {
                if (icon != null) { image.sprite = icon; image.enabled = true; }
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            // Sol yuvarlak yuva (res_pill_bg sol 63 px ≈ 28 birim) içinde, hap yüksekliğinden biraz taşan simge.
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(24f, 24f);
            iconRect.anchoredPosition = new Vector2(14f, 0f);
        }
        var lockRect = pill.Find("Lock") as RectTransform;
        if (lockRect != null)
        {
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0f, 0.5f);
            lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.sizeDelta = new Vector2(12f, 12f);
            lockRect.anchoredPosition = new Vector2(20f, -6f);
        }
        var textRect = pill.Find(textName) as RectTransform;
        if (textRect != null)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(26f, 0f);
            textRect.offsetMax = new Vector2(-3f, 0f);
            var tmp = textRect.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                // Büyük sayı taşmasın: tek satır + oto boyut (NumberFormat.Short zaten 10K+ kısaltır).
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 5f;
                tmp.fontSizeMax = 16f;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.raycastTarget = false;
            }
        }
    }

    /// <summary>Üste yapışık, x anchor oranlarında, sabit yükseklik (birim); pivot üst-orta.</summary>
    private static void TopRect(RectTransform rt, float xMin, float xMax, float topOffset, float height)
    {
        rt.anchorMin = new Vector2(xMin, 1f);
        rt.anchorMax = new Vector2(xMax, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -(topOffset + height));
        rt.offsetMax = new Vector2(0f, -topOffset);
    }

    private static void Slice(Image image)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SliceMultiplier;
    }
}
