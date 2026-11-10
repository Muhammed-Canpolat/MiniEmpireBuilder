using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Elmas Dükkanı: güçlendirmeler (İnşaat İksiri, misafir inşaatçı, 2x maden, canları doldur),
/// reklamla bedava elmas ve (6. adımda bağlanacak) elmas paketleri. Kendi tuvali var; her sahnede açılır.
/// Üst bardaki elmas sayacına dokununca açılır.
/// </summary>
public sealed class GemShopUI : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static readonly Color InkMuted = ReferenceUiVisuals.Hex(0x6B5236);

    private static GemShopUI open;

    private sealed class Row
    {
        public TextMeshProUGUI status;
        public TextMeshProUGUI price;
        public Func<string> statusText;
        public Func<string> priceText;
    }

    private readonly List<Row> rows = new List<Row>();
    private TextMeshProUGUI gemLabel;
    private float refreshTimer;
    private int testTaps;
    private float lastTestTap;

    public static bool IsOpen => open != null;

    public static void Open()
    {
        if (open != null) return;
        var root = new GameObject("GemShop", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250; // GemConfirm (300) bunun üstünde açılır
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        open = root.AddComponent<GemShopUI>();
        open.Build(ReferenceUiVisuals.SafeArea(canvas.transform));
        open.Refresh();
        AudioManager.Instance?.PlaySfx("click");
    }

    public static void Close()
    {
        if (open != null) Destroy(open.gameObject);
        open = null;
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }

    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.5f;
        Refresh();
    }

    // ==================== KURULUM ====================

    private void Build(RectTransform safe)
    {
        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.62f));
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(safe, "Panel", center, center, new Vector2(-178, -346), new Vector2(178, 346),
            Color.white);
        panel.sprite = CampaignSceneSetup.Art("ui_paper", 22f);
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;
        panel.transform.localScale = Vector3.one * 0.85f;
        panel.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(panel.gameObject);
        var p = panel.transform;

        TopText(p, "Title", "ELMAS DÜKKANI", 16, 22, 22, Ink, true);
        // Elmas sayısı (Development APK'da 5 hızlı dokunuş = +100 test elması).
        var gemPill = TopRect(p, "GemPill", 118, 50, 120, 30);
        var pillImage = gemPill.gameObject.AddComponent<Image>();
        pillImage.sprite = ReferenceUiVisuals.SolidPillSprite();
        pillImage.type = Image.Type.Sliced;
        pillImage.color = ReferenceUiVisuals.Hex(0x3B2A1A);
        var pillButton = gemPill.gameObject.AddComponent<Button>();
        pillButton.targetGraphic = pillImage;
        pillButton.onClick.AddListener(TestTap);
        var pillIcon = ReferenceUiVisuals.Image(gemPill, "GemIcon", new Vector2(0.06f, 0.12f), new Vector2(0.3f, 0.88f),
            Vector2.zero, Vector2.zero, Color.white);
        pillIcon.sprite = GemIcon.Sprite;
        pillIcon.preserveAspect = true;
        pillIcon.raycastTarget = false;
        gemLabel = ReferenceUiVisuals.Text(gemPill, "Gems", "0", 16, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            new Vector2(0.3f, 0f), Vector2.one, Vector2.zero, new Vector2(-6, 0), false, true);

        var gm = GameManager.Instance;
        float y = 92f;
        TopText(p, "BoostsTitle", "GÜÇLENDİRMELER", 18, y, 16, Ink, true, TextAlignmentOptions.Left);
        y += 22f;
        AddRow(p, ref y, "ui_icon_hammer", "İnşaat İksiri",
            () => gm != null && gm.BuildPotionActive
                ? $"Etkin · {GemEconomy.FormatRemaining(gm.PlayerData.buildPotionUntilTicks)}"
                : $"{GemEconomy.PotionHours} sa bütün inşaatçılar {GemEconomy.PotionSpeed}x hızlı",
            () => gm != null && gm.BuildPotionActive ? "ETKİN" : GemEconomy.PotionCost.ToString(),
            () => GemActions.OfferPotion(Refresh));
        AddRow(p, ref y, "ui_hero_marker", "Misafir İnşaatçı",
            () => gm != null && gm.TempBuilderActive
                ? $"Burada · {GemEconomy.FormatRemaining(gm.PlayerData.tempBuilderUntilTicks)}"
                : gm != null && !gm.CanRentTempBuilder ? "5 inşaatçın var, gerek yok"
                : $"{GemEconomy.TempBuilderHours} sa bir inşaatçı daha",
            () => gm != null && gm.TempBuilderActive ? "ETKİN" : GemEconomy.TempBuilderCost.ToString(),
            () => GemActions.OfferTempBuilder(Refresh));
        AddRow(p, ref y, "ui_icon_coin", "Maden 2x",
            () => gm != null && gm.MineBoostActive
                ? $"Etkin · {GemEconomy.FormatRemaining(gm.PlayerData.mineBoostUntilTicks)} (+{GemEconomy.MineBoostHours} sa eklenir)"
                : $"{GemEconomy.MineBoostHours} sa altın madeni 2 kat üretir",
            () => GemEconomy.MineBoostCost.ToString(),
            OfferMineBoost);
        AddRow(p, ref y, "ui_icon_heart", "Canları Doldur",
            () => LivesSystem.Unlimited ? "Sınırsız can sürüyor"
                : $"Can {LivesSystem.Lives}/{LivesSystem.MaxLives} → {LivesSystem.MaxLives}/{LivesSystem.MaxLives}",
            () => LivesSystem.CanRefillWithGems ? GemEconomy.RefillLivesCost.ToString() : "DOLU",
            OfferRefillLives);

        y += 6f;
        TopText(p, "FreeTitle", L.T("BEDAVA", "FREE"), 18, y, 16, Ink, true, TextAlignmentOptions.Left);
        y += 22f;
        AddRow(p, ref y, "gem", L.T("Reklam İzle", "Watch Ad"),
            () => L.T($"+{GemEconomy.GemsPerAd} elmas · bugün {GemRewards.AdGemsToday}/{GemEconomy.DailyGemAds}",
                $"+{GemEconomy.GemsPerAd} gems · today {GemRewards.AdGemsToday}/{GemEconomy.DailyGemAds}"),
            () => GemRewards.CanWatchAdForGems ? L.T("İZLE", "WATCH") : L.T("YARIN", "TOMORROW"),
            WatchAdForGems, false);
        AddRow(p, ref y, "ui_scroll", L.T("Günlük Sandık", "Daily Chest"),
            () => AdRewards.ChestAvailable ? L.T("Reklam izle: altın, elmas ya da kart jetonu", "Watch an ad: gold, gems or a card token")
                : L.T("Bugün açıldı, yarın yine gel", "Opened today, come back tomorrow"),
            () => AdRewards.ChestAvailable ? L.T("AÇ", "OPEN") : L.T("YARIN", "TOMORROW"),
            OpenChest, false);

        y += 6f;
        TopText(p, "PacksTitle", "ELMAS PAKETLERİ", 18, y, 16, Ink, true, TextAlignmentOptions.Left);
        y += 22f;
        string[] packs = { "80", "500", "1200" };
        for (int i = 0; i < packs.Length; i++)
        {
            var tile = TopRect(p, "Pack" + i, 18 + i * 108, y, 100, 62);
            var image = tile.gameObject.AddComponent<Image>();
            image.sprite = ReferenceUiVisuals.SolidPillSprite();
            image.type = Image.Type.Sliced;
            image.color = ReferenceUiVisuals.Hex(0x5A4127);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => GemActions.Toast("Elmas paketleri mağaza bağlantısıyla gelecek"));
            var icon = ReferenceUiVisuals.Image(tile, "Gem", new Vector2(0.06f, 0.3f), new Vector2(0.36f, 0.9f),
                Vector2.zero, Vector2.zero, Color.white);
            icon.sprite = GemIcon.Sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            ReferenceUiVisuals.Text(tile, "Amount", packs[i], 16, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
                new Vector2(0.36f, 0.3f), new Vector2(1f, 0.9f), Vector2.zero, new Vector2(-4, 0), true);
            ReferenceUiVisuals.Text(tile, "Soon", "YAKINDA", 10, ReferenceUiVisuals.Muted, TextAlignmentOptions.Center,
                Vector2.zero, new Vector2(1f, 0.32f), new Vector2(4, 4), new Vector2(-4, 0), false, true);
        }
        y += 72f;

        ReferenceUiVisuals.Text(p, "EarnInfo",
            $"Elmas kazan: seferde yeni yıldız +{GemEconomy.GemsPerNewStar} · bölge bossu ilk kez +{GemEconomy.GemsPerFirstBossWin} · " +
            $"yeni Ana Üs seviyesi +{GemEconomy.GemsPerMainBaseLevel} · giriş takviminin 7. günü 20",
            11, InkMuted, TextAlignmentOptions.Center, new Vector2(0, 1), Vector2.one,
            new Vector2(18, -y - 40), new Vector2(-18, -y), false, true);

        var closeRect = ReferenceUiVisuals.Rect(p, "Close", Vector2.one, Vector2.one, new Vector2(-46, -46), new Vector2(-8, -8));
        var closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        closeImage.color = closeImage.sprite != null ? Color.white : ReferenceUiVisuals.Red;
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close);
        ReferenceUiVisuals.Text(closeRect, "X", "X", 16, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
    }

    private void AddRow(Transform panel, ref float y, string icon, string title, Func<string> status, Func<string> price,
        Action onClick, bool gemPrice = true)
    {
        var row = TopRect(panel, "Row_" + title, 14, y, 328, 56);
        var bg = row.gameObject.AddComponent<Image>();
        bg.sprite = ReferenceUiVisuals.SolidPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.42f, 0.32f, 0.2f, 0.22f);
        bg.raycastTarget = false;

        var iconImage = ReferenceUiVisuals.Image(row, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(8, -18), new Vector2(44, 18), Color.white);
        iconImage.sprite = icon == "gem" ? GemIcon.Sprite : CampaignSceneSetup.Art(icon);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        ReferenceUiVisuals.Text(row, "Title", title, 15, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.5f), new Vector2(0.7f, 1f), new Vector2(52, -2), new Vector2(0, -4), true);
        var statusLabel = ReferenceUiVisuals.Text(row, "Status", "", 11.5f, InkMuted, TextAlignmentOptions.Left,
            Vector2.zero, new Vector2(0.7f, 0.5f), new Vector2(52, 4), new Vector2(0, 2), false, true);

        var button = ReferenceUiVisuals.Button(row, "Buy", "", true, new Vector2(0.71f, 0.12f), new Vector2(0.98f, 0.88f),
            Vector2.zero, Vector2.zero, 15);
        if (gemPrice) GemConfirm.AddGemIcon(button.transform);
        var priceLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(() => onClick?.Invoke());
        rows.Add(new Row { status = statusLabel, price = priceLabel, statusText = status, priceText = price });
        y += 62f;
    }

    private void Refresh()
    {
        if (gemLabel != null) gemLabel.text = (GameManager.Instance != null ? GameManager.Instance.Gems : 0).ToString();
        foreach (var row in rows)
        {
            row.status.text = row.statusText();
            string price = row.priceText();
            // Elmas simgesi solda: sayı biraz sağa kayar.
            row.price.text = int.TryParse(price, out _) ? "   " + price : price;
        }
    }

    // ==================== İŞLEMLER ====================

    private void OfferMineBoost()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        // Bedava seçenek: günde bir reklamla 2 saat.
        bool adOption = AdRewards.MineAdAvailable;
        GemConfirm.Show(L.T("MADEN 2X", "MINE 2X"),
            L.T($"Altın madeni {GemEconomy.MineBoostHours} saat boyunca 2 kat üretir. Sürüyorsa süreye eklenir; oyun kapalıyken de işler.",
                $"The gold mine produces 2x for {GemEconomy.MineBoostHours} hours. Stacks if active; works while offline."),
            GemEconomy.MineBoostCost, L.T("AL", "BUY"), () =>
            {
                if (!gm.BuyMineBoost()) return;
                AudioManager.Instance?.PlaySfx("levelUp");
                GemActions.Toast(L.T("Maden 2x: ", "Mine 2x: ") + GemEconomy.FormatRemaining(gm.PlayerData.mineBoostUntilTicks));
                Refresh();
            }, null, false,
            adOption ? L.T($"▶ REKLAM: {GemEconomy.AdMineBoostHours} SA BEDAVA", $"▶ AD: {GemEconomy.AdMineBoostHours} H FREE") : null,
            () => RewardedAds.Show("mine_boost", ok =>
            {
                if (!ok || !AdRewards.RewardMineBoost()) return;
                GemActions.Toast(L.T("Maden 2x: ", "Mine 2x: ") + GemEconomy.FormatRemaining(gm.PlayerData.mineBoostUntilTicks));
                Refresh();
            }));
    }

    private void OpenChest()
    {
        if (!AdRewards.ChestAvailable)
        {
            GemActions.Toast(L.T("Sandık bugün açıldı, yarın yine gel", "Chest already opened today"));
            return;
        }
        RewardedAds.Show("daily_chest", ok =>
        {
            if (!ok) return;
            string reward = AdRewards.OpenChest();
            if (reward == null) return;
            AudioManager.Instance?.PlaySfx("levelUp");
            GemActions.Toast(L.T("Sandıktan: ", "Chest: ") + reward);
            Refresh();
        });
    }

    private void OfferRefillLives()
    {
        if (!LivesSystem.CanRefillWithGems)
        {
            GemActions.Toast(LivesSystem.Unlimited ? "Sınırsız can sürüyor" : "Canların zaten dolu");
            return;
        }
        GemConfirm.Show("CANLARI DOLDUR", $"Canların {LivesSystem.MaxLives}/{LivesSystem.MaxLives} olur.",
            GemEconomy.RefillLivesCost, "DOLDUR", () =>
            {
                if (!LivesSystem.RefillWithGems()) return;
                AudioManager.Instance?.PlaySfx("levelUp");
                GemActions.Toast("Canlar doldu!");
                Refresh();
            });
    }

    private void WatchAdForGems()
    {
        if (!GemRewards.CanWatchAdForGems)
        {
            GemActions.Toast("Bugünkü reklam hakların bitti, yarın yine gel");
            return;
        }
        RewardedAds.Show("gems", ok =>
        {
            if (!ok || !GemRewards.RewardAdGems()) return;
            GemActions.Toast($"+{GemEconomy.GemsPerAd} elmas!");
            Refresh();
        });
    }

    private void TestTap()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        testTaps = Time.unscaledTime - lastTestTap < 0.4f ? testTaps + 1 : 1;
        lastTestTap = Time.unscaledTime;
        if (testTaps < 5) return;
        testTaps = 0;
        GameManager.Instance?.AddGems(100);
        GemActions.Toast("[TEST] +100 elmas");
        Refresh();
#endif
    }

    // ==================== YARDIMCILAR ====================

    private static RectTransform TopRect(Transform parent, string name, float x, float top, float width, float height)
        => ReferenceUiVisuals.Rect(parent, name, Vector2.up, Vector2.up,
            new Vector2(x, -top - height), new Vector2(x + width, -top));

    private static TextMeshProUGUI TopText(Transform parent, string name, string value, float x, float top, float size,
        Color color, bool title, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rect = TopRect(parent, name, x, top, 356 - 2 * x, size + 8);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(10, size - 4);
        label.fontSizeMax = size;
        ReferenceUiVisuals.ApplyFont(label, size, color, title, !title);
        return label;
    }
}
