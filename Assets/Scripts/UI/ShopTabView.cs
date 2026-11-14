using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (2.4): İnşa Et menüsünün DÜKKÂN sekmesi. İki bölüm, kaydırılabilir:
/// 1) KAYNAK PAKETLERİ — Altın / Odun / Et × küçük-orta-büyük, elmasla (ShopService; miktarlar Ana Üs seviyesine göre).
/// 2) ELMAS PAKETLERİ — gerçek para (IapService; fiyat mağazadan, yerel para birimi). "En iyi değer" / "Popüler" kurdeleleri.
/// Görsel dil: koyu ahşap kart + altın çerçeve, Tiny Swords kurdele ve simgeleri (diğer pencerelerle aynı aile).
/// </summary>
public sealed class ShopTabView : MonoBehaviour
{
    private sealed class PackCard
    {
        public ShopResource resource;
        public int tier;
        public TextMeshProUGUI amount, price, note;
        public Button button;
        public Image face;
    }

    private sealed class GemCard
    {
        public string productId;
        public TextMeshProUGUI price;
        public Button button;
    }

    private readonly List<PackCard> packs = new List<PackCard>();
    private readonly List<GemCard> gemCards = new List<GemCard>();
    private TextMeshProUGUI status;
    private float nextRefresh;

    public static readonly Color Wood = ReferenceUiVisuals.Hex(0x3B2A1A);
    public static readonly Color WoodLight = ReferenceUiVisuals.Hex(0x5A4127);

    public static ShopTabView Build(Transform panel, Vector2 min, Vector2 max)
    {
        var scrollGo = new GameObject("ShopScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        scrollGo.transform.SetParent(panel, false);
        var rt = (RectTransform)scrollGo.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(rt, false);
        var crt = (RectTransform)content.transform;
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = crt; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        var view = scrollGo.AddComponent<ShopTabView>();
        view.Layout(crt);
        return view;
    }

    private void Layout(RectTransform content)
    {
        float y = 4f;
        Section(content, ref y, L.T("KAYNAK PAKETLERİ", "RESOURCE PACKS"),
            L.T("Miktarlar Ana Üs seviyene göre büyür", "Amounts grow with your Main Base level"));
        foreach (ShopResource r in new[] { ShopResource.Gold, ShopResource.Wood, ShopResource.Meat })
            ResourceRow(content, ref y, r);
        y += 6f;
        Section(content, ref y, L.T("ELMAS PAKETLERİ", "GEM PACKS"),
            L.T("Google Play ile güvenli ödeme", "Secure payment via Google Play"));
        GemRow(content, ref y);
        status = ReferenceUiVisuals.Text(content, "ShopStatus", "", 12, ReferenceUiVisuals.Muted, TextAlignmentOptions.Center,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -y - 34), new Vector2(-8, -y), false, true);
        status.textWrappingMode = TextWrappingModes.Normal;
        y += 40f;
        content.sizeDelta = new Vector2(0f, y);

        IapService.Instance.Changed += Refresh;
        IapService.Instance.PurchaseFinished += OnPurchaseFinished;
        Refresh();
    }

    private void OnDestroy()
    {
        var iap = FindFirstObjectByType<IapService>();
        if (iap == null) return;
        iap.Changed -= Refresh;
        iap.PurchaseFinished -= OnPurchaseFinished;
    }

    private static void Section(RectTransform content, ref float y, string title, string sub)
    {
        var bar = FancyInfoPanel.NineSlice(content, "Section_" + title, new Vector2(0.02f, 1f), new Vector2(0.98f, 1f),
            new Vector2(0, -y - 26), new Vector2(0, -y), "ui_ribbon", Color.white, 3f);
        ReferenceUiVisuals.Text(bar.transform, "Title", title, 14, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(18, 2), new Vector2(-18, -2), true);
        y += 28f;
        ReferenceUiVisuals.Text(content, "Sub_" + title, sub, 11, new Color(0.94f, 0.82f, 0.55f), TextAlignmentOptions.Center,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -y - 16), new Vector2(-8, -y), false, false);
        y += 30f;   // kartların üstündeki kurdele rozetleri alt başlığa binmesin
    }

    private static Sprite ResourceIcon(ShopResource r)
    {
        var island = IslandVisualCatalog.Instance;
        switch (r)
        {
            case ShopResource.Gold: return CampaignSceneSetup.Art("ui_icon_coin");
            case ShopResource.Meat: return island != null ? island.meatIcon : null;
            default: return island != null ? island.woodIcon : null;
        }
    }

    private static string ResourceName(ShopResource r) =>
        r == ShopResource.Gold ? L.T("Altın", "Gold") : r == ShopResource.Meat ? L.T("Et", "Meat") : L.T("Odun", "Wood");

    private static string[] TierNames => new[] { L.T("Kese", "Pouch"), L.T("Sandık", "Chest"), L.T("Hazine", "Hoard") };

    private void ResourceRow(RectTransform content, ref float y, ShopResource r)
    {
        const float h = 92f;
        for (int tier = 0; tier < ShopService.PackCount; tier++)
        {
            float l = 0.02f + tier * 0.325f, rr = l + 0.31f;
            var face = ReferenceUiVisuals.Image(content, $"Pack_{r}_{tier}", new Vector2(l, 1f), new Vector2(rr, 1f),
                new Vector2(0, -y - h), new Vector2(0, -y), WoodLight);
            face.sprite = ReferenceUiVisuals.SolidPillSprite(); face.type = Image.Type.Sliced; face.raycastTarget = true;
            var outline = face.gameObject.AddComponent<Outline>();
            outline.effectColor = tier == 2 ? ReferenceUiVisuals.Gold : new Color(0.83f, 0.66f, 0.3f, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var icon = ReferenceUiVisuals.Image(face.transform, "Icon", new Vector2(0.06f, 0.52f), new Vector2(0.36f, 0.94f),
                Vector2.zero, Vector2.zero, Color.white);
            icon.sprite = ResourceIcon(r); icon.preserveAspect = true; icon.enabled = icon.sprite != null;
            var name = ReferenceUiVisuals.Text(face.transform, "Tier", $"{ResourceName(r)} {TierNames[tier]}", 10,
                new Color(0.94f, 0.82f, 0.55f), TextAlignmentOptions.Left, new Vector2(0.38f, 0.74f), new Vector2(0.98f, 0.96f),
                Vector2.zero, Vector2.zero, false, false);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            var amount = ReferenceUiVisuals.Text(face.transform, "Amount", "", 17, ReferenceUiVisuals.Light, TextAlignmentOptions.Left,
                new Vector2(0.38f, 0.50f), new Vector2(0.98f, 0.76f), Vector2.zero, Vector2.zero, true);
            var button = ReferenceUiVisuals.Button(face.transform, "Buy", "", true, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.44f),
                Vector2.zero, Vector2.zero, 14f);
            GemConfirm.AddGemIcon(button.transform);
            var price = button.GetComponentInChildren<TextMeshProUGUI>();
            int t = tier;
            button.onClick.AddListener(() => OnBuyPack(r, t));
            TextMeshProUGUI note = null;
            if (tier == 2) note = Badge(face.transform, L.T("EN İYİ DEĞER", "BEST VALUE"), new Color(0.75f, 0.2f, 0.15f));
            else if (tier == 1) note = Badge(face.transform, L.T("+%10", "+10%"), new Color(0.2f, 0.45f, 0.7f));
            packs.Add(new PackCard { resource = r, tier = tier, amount = amount, price = price, button = button, face = face, note = note });
        }
        y += h + 8f;
    }

    private static TextMeshProUGUI Badge(Transform parent, string text, Color color)
    {
        var badge = ReferenceUiVisuals.Image(parent, "Badge", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-46, -6), new Vector2(46, 12), color);
        badge.sprite = ReferenceUiVisuals.SolidPillSprite(); badge.type = Image.Type.Sliced;
        var o = badge.gameObject.AddComponent<Outline>();
        o.effectColor = ReferenceUiVisuals.Gold; o.effectDistance = new Vector2(1f, -1f);
        var label = ReferenceUiVisuals.Text(badge.transform, "Label", text, 10, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0), true);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private void GemRow(RectTransform content, ref float y)
    {
        var cfg = ShopConfig.Instance;
        const float h = 116f;
        for (int i = 0; i < cfg.gemProductIds.Length && i < 3; i++)
        {
            float l = 0.02f + i * 0.325f, rr = l + 0.31f;
            bool best = i == cfg.bestValueIndex;
            var face = ReferenceUiVisuals.Image(content, "Gems_" + cfg.gemProductIds[i], new Vector2(l, 1f), new Vector2(rr, 1f),
                new Vector2(0, -y - h), new Vector2(0, -y), best ? ReferenceUiVisuals.Hex(0x4A2E5C) : WoodLight);
            face.sprite = ReferenceUiVisuals.SolidPillSprite(); face.type = Image.Type.Sliced; face.raycastTarget = true;
            var outline = face.gameObject.AddComponent<Outline>();
            outline.effectColor = best ? ReferenceUiVisuals.Gold : new Color(0.83f, 0.66f, 0.3f, 0.55f);
            outline.effectDistance = new Vector2(best ? 2f : 1.5f, best ? -2f : -1.5f);
            var icon = ReferenceUiVisuals.Image(face.transform, "Gem", new Vector2(0.25f, 0.52f), new Vector2(0.75f, 0.88f),
                Vector2.zero, Vector2.zero, Color.white);
            icon.sprite = GemIcon.Sprite; icon.preserveAspect = true;
            ReferenceUiVisuals.Text(face.transform, "Amount", cfg.gemProductGems[i].ToString(), 18, ReferenceUiVisuals.Light,
                TextAlignmentOptions.Center, new Vector2(0f, 0.36f), new Vector2(1f, 0.54f), Vector2.zero, Vector2.zero, true);
            int bonus = i < cfg.gemProductBonusPercent.Length ? cfg.gemProductBonusPercent[i] : 0;
            if (bonus > 0)
                ReferenceUiVisuals.Text(face.transform, "Bonus", L.T($"+%{bonus} bonus", $"+{bonus}% bonus"), 10,
                    ReferenceUiVisuals.Hex(0x8FE07A), TextAlignmentOptions.Center, new Vector2(0f, 0.26f), new Vector2(1f, 0.37f),
                    Vector2.zero, Vector2.zero, false, true);
            var button = ReferenceUiVisuals.Button(face.transform, "Buy", "…", true, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.25f),
                Vector2.zero, Vector2.zero, 13f);
            string id = cfg.gemProductIds[i];
            button.onClick.AddListener(() => OnBuyGems(id));
            if (best) Badge(face.transform, L.T("EN İYİ DEĞER", "BEST VALUE"), new Color(0.75f, 0.2f, 0.15f));
            else if (i == cfg.popularIndex) Badge(face.transform, L.T("POPÜLER", "POPULAR"), new Color(0.2f, 0.45f, 0.7f));
            gemCards.Add(new GemCard { productId = id, price = button.GetComponentInChildren<TextMeshProUGUI>(), button = button });
        }
        y += h + 8f;
    }

    private void OnBuyPack(ShopResource r, int tier)
    {
        int gems = ShopService.PackGems(r, tier);
        int amount = ShopService.PackAmount(r, tier);
        var check = ShopService.CanBuy(r, tier);
        if (check == ShopService.BuyResult.WalletFull)
        {
            GemActions.Toast(L.T("Cüzdan tavanı aşılır; önce altın harca", "Wallet cap would overflow; spend some gold first"));
            return;
        }
        if (check == ShopService.BuyResult.Locked)
        {
            GemActions.Toast(L.T($"Ada Ana Üs {Progression.Island.UnlockMainBaseLevel}'te açılır", $"The island opens at Main Base {Progression.Island.UnlockMainBaseLevel}"));
            return;
        }
        GemConfirm.Show(L.T("SATIN AL", "BUY"),
            L.T($"{NumberFormat.Short(amount)} {ResourceName(r)} alınsın mı?", $"Buy {NumberFormat.Short(amount)} {ResourceName(r)}?"),
            gems, L.T("SATIN AL", "BUY"), () =>
            {
                if (ShopService.Buy(r, tier) == ShopService.BuyResult.Ok)
                {
                    AudioManager.Instance?.PlaySfx("coin");
                    GemActions.Toast(L.T($"+{NumberFormat.Short(amount)} {ResourceName(r)}", $"+{NumberFormat.Short(amount)} {ResourceName(r)}"));
                }
                Refresh();
            });
    }

    private void OnBuyGems(string productId)
    {
        if (!IapService.Instance.Ready) { GemActions.Toast(L.T("Mağazaya bağlanılıyor…", "Connecting to the store…")); return; }
        if (!IapService.Instance.Buy(productId)) GemActions.Toast(L.T("Önceki satın alma sürüyor", "A purchase is in progress"));
        Refresh();
    }

    private void OnPurchaseFinished(string productId, bool ok, string message)
    {
        if (this == null) return;
        GemActions.Toast(message);
        if (ok) AudioManager.Instance?.PlaySfx("levelUp");
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;
        Refresh();
    }

    /// <summary>
    /// Mağazanın yerel fiyat metni yazı tipinde olmayan bir simge içerebilir (ör. ₺): o simge ISO koduna çevrilir, kare görünmez.
    /// </summary>
    public static string SafePrice(string price, TMP_Text text)
    {
        if (string.IsNullOrEmpty(price) || text == null || text.font == null) return price;
        var sb = new System.Text.StringBuilder();
        foreach (char c in price)
        {
            if (char.IsWhiteSpace(c) || text.font.HasCharacter(c, true, false)) { sb.Append(c); continue; }
            string code = c == '₺' ? "TL" : c == '€' ? "EUR" : c == '£' ? "GBP" : c == '₹' ? "INR" : c == '₽' ? "RUB"
                : c == '₩' ? "KRW" : c == '¥' ? "JPY" : c == '₴' ? "UAH" : c == '₫' ? "VND" : c == '₱' ? "PHP" : "";
            if (code.Length > 0) sb.Append(code).Append(' ');
        }
        return sb.ToString().Trim();
    }

    public void Refresh()
    {
        if (this == null)
        {
            // Sahne kapandı: kalıcı IapService'teki abonelik temizlenir (yok edilmiş metne dokunulmaz).
            var iapSvc = FindFirstObjectByType<IapService>();
            if (iapSvc != null) { iapSvc.Changed -= Refresh; iapSvc.PurchaseFinished -= OnPurchaseFinished; }
            return;
        }
        var gm = GameManager.Instance;
        foreach (var p in packs)
        {
            bool available = ShopService.Available(p.resource);
            p.amount.text = available ? "+" + NumberFormat.Short(ShopService.PackAmount(p.resource, p.tier)) : L.T("Kilitli", "Locked");
            int gems = ShopService.PackGems(p.resource, p.tier);
            var check = ShopService.CanBuy(p.resource, p.tier);
            p.price.text = !available ? L.T($"ANA ÜS {Progression.Island.UnlockMainBaseLevel}", $"MAIN BASE {Progression.Island.UnlockMainBaseLevel}")
                : check == ShopService.BuyResult.WalletFull ? L.T("CÜZDAN DOLU", "WALLET FULL") : "   " + gems;
            if (p.price.transform.parent.Find("GemIcon") is Transform gi) gi.gameObject.SetActive(available && check != ShopService.BuyResult.WalletFull);
            p.price.color = check == ShopService.BuyResult.NoGems ? ReferenceUiVisuals.Hex(0xC2452D) : ReferenceUiVisuals.Hex(0x2A1A08);
            p.button.interactable = available;
            p.face.color = available ? WoodLight : new Color(0.25f, 0.2f, 0.16f);
        }
        var iap = IapService.Instance;
        foreach (var g in gemCards)
        {
            g.price.text = iap.Busy == g.productId ? "…" : SafePrice(iap.PriceLabel(g.productId), g.price);
            g.button.interactable = iap.Ready && string.IsNullOrEmpty(iap.Busy);
        }
        if (status != null)
            status.text = !iap.Ready ? L.T("Mağazaya bağlanılıyor… Fiyatlar Google Play'den yerel para biriminde gelir.",
                    "Connecting to the store… Prices come from Google Play in your currency.")
                : (IapService.UseFakeStore ? L.T("TEST MAĞAZASI: gerçek ödeme alınmaz. ", "TEST STORE: no real payment. ") : "")
                  + L.T($"1 elmas ≈ {ShopService.GoldPerGem} altın (Ana Üs {ShopService.MainBaseLevel}). Elmaslar ödeme onaylanınca eklenir.",
                      $"1 gem ≈ {ShopService.GoldPerGem} gold (Main Base {ShopService.MainBaseLevel}). Gems arrive once payment is confirmed.");
    }
}
