using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kart Atölyesi üretim ekranı (Docs/design/card_workshop.md §9): üstte Et/altın, üretim kuyruğu (ilerleme, kalan süre, reklam ve elmasla
/// bitir), 7 kartlık stok ızgarası ve seçili kartın ayrıntısı + ÜRET. Kendi tuvali vardır (her sahnede açılır). Görseller Tiny Swords
/// kâğıt/kurdele/düğme/simge sprite'larıdır (<see cref="IslandVisualCatalog"/> ve Resources/CampaignArt); mantık <see cref="CardWorkshop"/>'tadır.
/// </summary>
public sealed class CardWorkshopPanel : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.23f, 0.165f, 0.10f);
    private static readonly Color InkMuted = new Color(0.42f, 0.32f, 0.21f);
    private static readonly Color Warn = new Color(0.72f, 0.16f, 0.10f);

    public const float PanelWidth = 366f, PanelHeight = 770f;

    public static CardWorkshopPanel Current { get; private set; }
    public static bool IsOpen => Current != null;

    private string slotId;
    private BattleCard selected = BattleCard.HeroDamage;
    private RectTransform content;
    private readonly List<(WorkshopJob job, Image fill, TextMeshProUGUI time)> timers = new List<(WorkshopJob, Image, TextMeshProUGUI)>();
    private TextMeshProUGUI meatText, goldText;
    private float nextTick;

    // ==================== AÇ / KAPAT ====================

    /// <summary>Üretim ekranını açar. Atölye kurulu değilse (kayıtta bina yoksa) false.</summary>
    public static bool Open(string slotId = null)
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null) return false;
        Close();
        var root = new GameObject("CardWorkshopPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        ReferenceUiVisuals.ConfigureCanvas(root.GetComponent<CanvasScaler>());
        var panel = root.AddComponent<CardWorkshopPanel>();
        panel.slotId = slotId;
        Current = panel;
        panel.Build(canvas.transform);
        CardWorkshop.OnChanged += panel.Rebuild;
        BattleCards.OnChanged += panel.Rebuild;
        AudioManager.Instance?.PlaySfx("click");
        return true;
    }

    public static void Close()
    {
        if (Current == null) return;
        var panel = Current;
        Current = null;
        CardWorkshop.OnChanged -= panel.Rebuild;
        BattleCards.OnChanged -= panel.Rebuild;
        Destroy(panel.gameObject);
    }

    /// <summary>Testler için: seçili kart.</summary>
    public void Select(BattleCard card) { selected = card; Rebuild(); }

    // ==================== YAPI ====================

    private static Sprite Ui(string name)
    {
        var catalog = IslandVisualCatalog.Instance;
        return catalog != null ? catalog.Single(name) : null;
    }

    private static RectTransform At(Transform parent, string name, float x, float y, float w, float h)
    {
        var rt = ReferenceUiVisuals.Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y - h), new Vector2(x + w, -y));
        return rt;
    }

    private static Image Pic(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        var image = At(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    private static TextMeshProUGUI Tx(Transform parent, string name, string value, float size, Color color, float x, float y, float w, float h,
        TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = true, bool title = false)
    {
        var tmp = ReferenceUiVisuals.Text(parent, name, value, size, color, align, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x, -y - h), new Vector2(x + w, -y), title, bold);
        tmp.fontSizeMin = Mathf.Max(8f, size - 5f);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static Image Paper(Transform parent, string name, float x, float y, float w, float h, Color tint, float ppu = 2.4f)
    {
        var image = At(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = Ui("ui_paper");
        image.color = tint;
        if (image.sprite != null) { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = ppu; }
        return image;
    }

    private static Button TsButton(Transform parent, string name, string label, bool red, float x, float y, float w, float h, float size, out TextMeshProUGUI text)
    {
        var rt = At(parent, name, x, y, w, h);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = Ui(red ? "ui_button_red" : "ui_button_blue");
        if (image.sprite != null) { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 3f; }
        else image.color = red ? ReferenceUiVisuals.Red : ReferenceUiVisuals.Blue;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        text = ReferenceUiVisuals.Text(rt, "Label", label, size, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
            new Vector2(6, 3), new Vector2(-6, -2), true);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;   // uzun etiketler düğmeden taşmasın
        text.fontSizeMin = 8f;
        text.fontSizeMax = size;
        return button;
    }

    /// <summary>AŞAMA 3 §3E: düğmenin soluna ikon koyar, yazıyı sağa iter (sayı/etiket çıplak kalmaz).</summary>
    private static void ButtonIcon(Button button, TextMeshProUGUI label, Sprite sprite, float size = 18f, string name = "Icon")
    {
        var icon = ReferenceUiVisuals.Image(button.transform, name, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(6f, -size / 2f), new Vector2(6f + size, size / 2f), Color.white);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.enabled = sprite != null;
        if (label == null) return;
        label.rectTransform.offsetMin = new Vector2(size + 8f, label.rectTransform.offsetMin.y);
        label.rectTransform.offsetMax = new Vector2(-10f, label.rectTransform.offsetMax.y);   // yazı düğme kenarına değmesin
        label.fontSizeMax = Mathf.Min(label.fontSizeMax, 11f);
    }

    private static void Header(Transform parent, string name, string text, float x, float y, float w, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var tx = Tx(parent, name, text, 11, InkMuted, x, y, w, 15f, align, true, true);
        tx.characterSpacing = 1.2f;
        tx.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void Dim(Button button, bool enabled)
    {
        button.interactable = enabled;
        var image = button.GetComponent<Image>();
        if (image != null) image.color = enabled ? Color.white : new Color(0.62f, 0.62f, 0.62f, 0.85f);
    }

    private void Build(Transform canvas)
    {
        var safe = ReferenceUiVisuals.SafeArea(canvas);
        var dim = ReferenceUiVisuals.Image(safe, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.62f));
        dim.raycastTarget = true;
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);

        var center = new Vector2(0.5f, 0.5f);
        var panel = ReferenceUiVisuals.Image(safe, "Panel", center, center, new Vector2(-PanelWidth / 2f, -PanelHeight / 2f),
            new Vector2(PanelWidth / 2f, PanelHeight / 2f), Color.white);
        panel.sprite = Ui("ui_paper");
        if (panel.sprite != null) { panel.type = Image.Type.Sliced; panel.pixelsPerUnitMultiplier = 2.4f; }
        else panel.color = ReferenceUiVisuals.Panel;
        panel.raycastTarget = true;
        panel.transform.localScale = Vector3.one * 0.9f;
        panel.transform.DOScale(FitFor(CanvasSize().x, CanvasSize().y), 0.18f).SetEase(Ease.OutBack).SetLink(panel.gameObject);
        content = panel.rectTransform;
        FitScale();
        Rebuild();
    }

    /// <summary>AŞAMA 3 §3E: panel 366×770 sabit; 3:4 gibi kısa tuvallerde (≈497×662) ekrana sığacak kadar küçülür. Ölçü tuval boyudur (px değil).</summary>
    public static float FitFor(float canvasWidth, float canvasHeight)
    {
        if (!FeatureConfig.Instance.workshopPanelFit || canvasWidth <= 0f || canvasHeight <= 0f) return 1f;
        return Mathf.Min(1f, canvasHeight * 0.97f / PanelHeight, canvasWidth * 0.99f / PanelWidth);
    }

    /// <summary>Tuval boyu: kök tuvalin rect'i; henüz hesaplanmadıysa CanvasScaler formülüyle (ref 390×844, match 0,5).</summary>
    private Vector2 CanvasSize()
    {
        var canvas = GetComponentInParent<Canvas>();
        var rect = canvas != null ? canvas.rootCanvas.GetComponent<RectTransform>().rect : default;
        if (rect.width > 1f && rect.height > 1f) return rect.size;
        float factor = Mathf.Sqrt(Screen.width / 390f * (Screen.height / 844f));
        return factor > 0f ? new Vector2(Screen.width / factor, Screen.height / factor) : new Vector2(390f, 844f);
    }

    /// <summary>Tuval boyu değişti (döndürme/test): paneli yeniden sığdırır.</summary>
    public void Refit() => FitScale();

    /// <summary>Ekran/hedef doku boyu (px) verilince ölçeği CanvasScaler formülüyle (ref 390×844, match 0,5) hesaplar (testler/ekran görüntüsü).</summary>
    public void RefitForPixels(float pixelWidth, float pixelHeight)
    {
        float factor = Mathf.Sqrt(pixelWidth / 390f * (pixelHeight / 844f));
        if (content == null || factor <= 0f) return;
        content.localScale = Vector3.one * FitFor(pixelWidth / factor, pixelHeight / factor);
    }

    private Vector2 lastCanvas;
    private void FitScale()
    {
        if (content == null) return;
        lastCanvas = CanvasSize();
        content.localScale = Vector3.one * FitFor(lastCanvas.x, lastCanvas.y);
    }

    private readonly List<GameObject> dynamic = new List<GameObject>();

    private void Rebuild()
    {
        if (content == null) return;
        foreach (var item in dynamic) if (item != null) Destroy(item);
        dynamic.Clear();
        timers.Clear();
        var root = new GameObject("Dynamic", typeof(RectTransform));
        root.transform.SetParent(content, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        dynamic.Add(root);
        Fill(rt);
    }

    private void Fill(Transform p)
    {
        int level = CardWorkshop.Level;
        float w = PanelWidth;

        // Başlık kurdelesi + kapat
        var ribbon = At(p, "Ribbon", 36f, -6f, w - 72f, 50f).gameObject.AddComponent<Image>();
        ribbon.sprite = Ui("ui_ribbon");
        if (ribbon.sprite != null) { ribbon.type = Image.Type.Sliced; ribbon.pixelsPerUnitMultiplier = 3f; }
        ribbon.raycastTarget = false;
        Tx(ribbon.transform, "Title", L.T($"KART ATÖLYESİ · SEV. {level}", $"CARD WORKSHOP · LV. {level}"), 19, Color.white, 40f, 4f, w - 72f - 80f, 40f,
            TextAlignmentOptions.Center, false, true);
        var close = At(p, "Close", w - 48f, 6f, 36f, 36f);
        var closeImage = close.gameObject.AddComponent<Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        closeImage.color = closeImage.sprite != null ? Color.white : ReferenceUiVisuals.Red;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close);
        ReferenceUiVisuals.Text(close, "X", "X", 15, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);

        // Kaynak satırı: Et · altın · hak/üretim · YÜKSELT
        var gm = GameManager.Instance;
        Pic(p, "MeatIcon", IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.meatIcon : null, 22f, 60f, 28f, 28f);
        meatText = Tx(p, "Meat", IslandEconomy.Meat.ToString(), 17, Ink, 52f, 60f, 56f, 28f);
        Pic(p, "GoldIcon", CampaignSceneSetup.Art("ui_icon_coin"), 112f, 60f, 28f, 28f);
        goldText = Tx(p, "Gold", gm != null ? gm.Gold.ToString() : "0", 17, Ink, 142f, 60f, 70f, 28f);
        var upgrade = TsButton(p, "Upgrade", L.T("YÜKSELT", "UPGRADE"), false, w - 128f, 58f, 106f, 32f, 14, out _);
        upgrade.onClick.AddListener(() =>
        {
            var ui = FindFirstObjectByType<BaseWorldUI>();
            if (ui == null) return;
            var id = slotId;
            Close();
            ui.ShowBuildingInfo(BuildingType.CardWorkshop, id);
        });
        Tx(p, "UsesInfo", L.T($"Üretim başına ×{CardWorkshop.UsesPerCraft} hak · 1 savaş = 1 hak · yenilgide de düşer",
            $"x{CardWorkshop.UsesPerCraft} uses per craft · 1 battle = 1 use · spent on defeat too"), 12, InkMuted, 22f, 92f, w - 44f, 18f, TextAlignmentOptions.Left, true);

        // Üretim kuyruğu
        Tx(p, "QueueTitle", L.T("ÜRETİM KUYRUĞU", "PRODUCTION QUEUE"), 13, Ink, 22f, 114f, 170f, 18f, TextAlignmentOptions.Left, true, true);
        Header(p, "SpeedUpHeader", L.T("HIZLANDIR", "SPEED UP"), w - 196f, 117f, 160f, TextAlignmentOptions.Center);
        int slots = Mathf.Max(1, CardWorkshop.QueueSlots);
        var queue = CardWorkshop.Queue;
        for (int i = 0; i < slots; i++) QueueRow(p, i, i < queue.Count ? queue[i] : null, 134f + i * 58f);

        // Kart ızgarası
        float gridTop = 134f + slots * 58f + 6f;
        Tx(p, "StockTitle", L.T("KARTLARIM", "MY CARDS"), 13, Ink, 22f, gridTop, 140f, 18f, TextAlignmentOptions.Left, true, true);
        float cellW = (w - 44f - 8f) / 2f, cellH = 62f;
        for (int i = 0; i < BattleCards.Count; i++)
        {
            float x = 22f + (i % 2) * (cellW + 8f);
            float y = gridTop + 20f + (i / 2) * (cellH + 6f);
            CardCell(p, (BattleCard)i, x, y, cellW, cellH);
        }

        // Seçili kart ayrıntısı
        float detailTop = gridTop + 20f + 4 * (cellH + 6f) + 2f;
        Detail(p, detailTop);
    }

    private void QueueRow(Transform p, int index, WorkshopJob job, float y)
    {
        float w = PanelWidth;
        var paper = Paper(p, "Queue" + index, 18f, y, w - 36f, 52f, new Color(1f, 0.97f, 0.88f, 1f), 2f);
        if (job == null)
        {
            Tx(paper.transform, "Empty", L.T("Boş yuva: aşağıdan bir kart seç ve üret", "Empty slot: pick a card below and craft it"), 13, InkMuted, 12f, 12f, w - 60f, 28f,
                TextAlignmentOptions.Left, true);
            return;
        }
        var card = (BattleCard)job.card;
        Pic(paper.transform, "Icon", CampaignSceneSetup.Art(BattleCards.IconName(card)), 8f, 8f, 34f, 34f);
        Tx(paper.transform, "Name", $"{BattleCards.Name(card)} ×{job.uses}", 14, Ink, 48f, 2f, 120f, 18f, TextAlignmentOptions.Left, true);
        Pic(paper.transform, "ClockIcon", CampaignSceneSetup.Art("ui_icon_clock"), 48f, 19f, 15f, 15f);
        var time = Tx(paper.transform, "Time", Progression.FormatDuration(CardWorkshop.SecondsLeft(job)), 13, InkMuted, 66f, 19f, 102f, 16f, TextAlignmentOptions.Left, true);
        var bar = ReferenceUiVisuals.Image(paper.transform, "BarBg", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -47f), new Vector2(168f, -37f), new Color(0.25f, 0.17f, 0.1f, 0.85f));
        var fill = ReferenceUiVisuals.Image(bar.transform, "Fill", Vector2.zero, new Vector2(CardWorkshop.Progress01(job), 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Color(0.35f, 0.72f, 0.3f));
        timers.Add((job, fill, time));
        int adsLeft = AdRewards.WorkshopAdsLeft;
        int captured = index;
        var ad = TsButton(paper.transform, "Ad", $"{adsLeft}/{GemEconomy.DailyWorkshopAds}", false,
            w - 36f - 160f, 4f, 92f, 44f, 14, out var adLabel);
        ButtonIcon(ad, adLabel, CampaignSceneSetup.Art("ui_icon_play"), 22f);   // ikon + "3/3"
        adLabel.textWrappingMode = TextWrappingModes.NoWrap;
        adLabel.fontSizeMax = 14f;
        Dim(ad, adsLeft > 0);
        ad.onClick.AddListener(() => RewardedAds.Show("workshop_finish", ok =>
        {
            if (ok && CardWorkshop.TryFinishByAd(captured)) AudioManager.Instance?.PlaySfx("levelUp");
        }));
        int gems = CardWorkshop.FinishGemCost(index);
        var gem = TsButton(paper.transform, "Gem", gems > 0 ? gems.ToString() : L.T("BEDAVA", "FREE"), true, w - 36f - 62f, 6f, 54f, 40f, 13, out var gemLabel);
        if (gems > 0) ButtonIcon(gem, gemLabel, GemIcon.Sprite, 16f, "GemIcon");   // ikon + sayı (çıplak sayı yok)
        else gemLabel.rectTransform.offsetMin = new Vector2(4f, 3f);
        gem.onClick.AddListener(() =>
        {
            int cost = CardWorkshop.FinishGemCost(captured);
            GemConfirm.Show(L.T("ÜRETİMİ BİTİR", "FINISH CRAFT"),
                $"{BattleCards.Name(card)} ×{job.uses}\n{L.T("Kalan süre", "Time left")}: {Progression.FormatDuration(CardWorkshop.SecondsLeft(job))}",
                cost, L.T("BİTİR", "FINISH"), () => { if (!CardWorkshop.TryFinishWithGems(captured)) GemActions.Toast(L.T("Elmas yetmiyor", "Not enough gems")); });
        });
    }

    private void CardCell(Transform p, BattleCard card, float x, float y, float w, float h)
    {
        var tier = BattleCards.Tier(card);
        bool unlocked = CardWorkshop.IsUnlocked(card);
        Color tint = tier == CardTier.Common ? new Color(1f, 0.97f, 0.88f) : tier == CardTier.Rare ? new Color(0.82f, 0.9f, 1f) : new Color(0.93f, 0.83f, 1f);
        if (!unlocked) tint = new Color(0.72f, 0.68f, 0.62f, 0.95f);
        var paper = Paper(p, "Card_" + card, x, y, w, h, tint, 2f);
        paper.raycastTarget = true;
        var button = paper.gameObject.AddComponent<Button>();
        button.targetGraphic = paper;
        button.transition = Selectable.Transition.None;
        var capture = card;
        button.onClick.AddListener(() => { selected = capture; Rebuild(); });
        if (selected == card)
        {
            var outline = paper.gameObject.AddComponent<Outline>();
            outline.effectColor = ReferenceUiVisuals.Gold;
            outline.effectDistance = new Vector2(2.5f, -2.5f);
        }
        var icon = Pic(paper.transform, "Icon", CampaignSceneSetup.Art(BattleCards.IconName(card)), 8f, 10f, 40f, 40f);
        if (!unlocked) icon.color = new Color(1f, 1f, 1f, 0.45f);
        Tx(paper.transform, "Name", BattleCards.Name(card), 14, unlocked ? Ink : InkMuted, 54f, 8f, w - 60f, 20f, TextAlignmentOptions.Left, true);
        int stock = CardWorkshop.Stock(card);
        string line;
        Color lineColor = InkMuted;
        if (!unlocked)
            line = L.T($"Atölye Sv {Progression.Workshop.UnlockLevel((int)tier)}", $"Workshop Lv {Progression.Workshop.UnlockLevel((int)tier)}");
        else
        {
            int queued = CardWorkshop.QueuedUses(card);
            line = stock > 0 ? L.T($"Stok ×{stock}", $"Stock x{stock}") : L.T("Stok yok", "No stock");
            if (queued > 0) line += L.T($" (+{queued})", $" (+{queued})");
            if (stock > 0) lineColor = new Color(0.15f, 0.5f, 0.2f);
        }
        // Kademe ayrı satır değil: kâğıdın alt kenarına taşıyordu; stok satırının sağında küçük etiket.
        Tx(paper.transform, "Stock", line, 13, lineColor, 54f, 30f, w - 60f - 62f, 18f, TextAlignmentOptions.Left, true);
        Tx(paper.transform, "Tier", BattleCards.TierName(tier), 10, InkMuted, w - 76f, 32f, 62f, 16f, TextAlignmentOptions.Right, false);
    }

    private void Detail(Transform p, float top)
    {
        float w = PanelWidth;
        var card = selected;
        var tier = BattleCards.Tier(card);
        var paper = Paper(p, "Detail", 14f, top, w - 28f, PanelHeight - top - 14f, new Color(1f, 0.98f, 0.9f), 2.2f);
        float inner = w - 28f;
        Pic(paper.transform, "Icon", CampaignSceneSetup.Art(BattleCards.IconName(card)), 10f, 8f, 36f, 36f);
        Tx(paper.transform, "Name", $"{BattleCards.Name(card)}  ·  {BattleCards.TierName(tier)}", 16, Ink, 52f, 6f, inner - 60f, 22f, TextAlignmentOptions.Left, true, true);
        Tx(paper.transform, "Effect", BattleCards.Description(card), 13, InkMuted, 52f, 28f, inner - 60f, 32f, TextAlignmentOptions.TopLeft, true);

        bool tierOpen = CardWorkshop.IsUnlocked(card);
        bool free = CardWorkshop.FirstCraftFree(card);
        int meat = CardWorkshop.MeatCost(card), gold = CardWorkshop.GoldCost(card), seconds = CardWorkshop.CraftSeconds(card);
        // AŞAMA 3 §3E: başlıklı bölümler (MALİYET · SÜRE) + her fiyat ikonlu
        Header(paper.transform, "CostHeader", L.T("MALİYET", "COST"), 12f, 62f, 120f);
        Header(paper.transform, "TimeHeader", L.T("SÜRE", "TIME"), 152f, 62f, 80f);
        float rowY = 78f;
        Pic(paper.transform, "MeatIcon", IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.meatIcon : null, 12f, rowY, 24f, 24f);
        bool meatShort = IslandEconomy.Meat < meat;
        Tx(paper.transform, "MeatCost", meat.ToString(), 15, meatShort ? Warn : Ink, 38f, rowY, 40f, 24f, TextAlignmentOptions.Left, true);
        Pic(paper.transform, "GoldIcon", CampaignSceneSetup.Art("ui_icon_coin"), 80f, rowY, 24f, 24f);
        bool goldShort = GameManager.Instance != null && GameManager.Instance.Gold < gold;
        Tx(paper.transform, "GoldCost", gold.ToString(), 15, goldShort ? Warn : Ink, 106f, rowY, 44f, 24f, TextAlignmentOptions.Left, true);
        Pic(paper.transform, "ClockIcon", CampaignSceneSetup.Art("ui_icon_clock"), 152f, rowY, 22f, 24f);
        Tx(paper.transform, "Time", Progression.FormatDuration(seconds), 14, Ink, 176f, rowY, 56f, 24f, TextAlignmentOptions.Left, true);
        // "+N kullanım": kart/parşömen ikonu + metin (çıplak sayı yok)
        Pic(paper.transform, "UsesIcon", CampaignSceneSetup.Art("ui_scroll"), 236f, rowY, 20f, 24f);
        Tx(paper.transform, "Uses", L.T($"+{CardWorkshop.UsesPerCraft} kullanım", $"+{CardWorkshop.UsesPerCraft} uses"), 12, new Color(0.15f, 0.5f, 0.2f), 258f, rowY, inner - 262f, 24f, TextAlignmentOptions.Left, true);
        float hintY = rowY + 26f;
        if (free) Tx(paper.transform, "Free", L.T("İLK ÜRETİM BEDAVA · 30 sn", "FIRST CRAFT FREE · 30 s"), 12, new Color(0.15f, 0.5f, 0.2f), 12f, hintY, inner - 24f, 16f, TextAlignmentOptions.Left, true);
        else if (meatShort && tierOpen)
        {
            // Tek satir: "Et eksik (160): Agil ya da [elmas] 20" - elmas simgesi + sayi yazinin hemen ardinda, sigmazsa yazi kuculur.
            var hint = Tx(paper.transform, "MeatHint", L.T($"Et eksik ({meat - IslandEconomy.Meat}): Ağıl ya da", $"Meat short ({meat - IslandEconomy.Meat}): Pasture or"), 12, Warn, 12f, hintY, inner - 24f, 16f, TextAlignmentOptions.Left, true);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.enableAutoSizing = false;
            string gemsText = CardWorkshop.MeatShortfallGems(card).ToString();
            const float gemIconW = 18f, gemTextW = 34f;
            float avail = inner - 24f - gemIconW - gemTextW - 6f;
            float textW = hint.GetPreferredValues(hint.text, 1000f, 16f).x;
            hint.fontSize = textW > avail ? 12f * avail / textW : 12f;
            textW = hint.GetPreferredValues(hint.text, 1000f, 16f).x;
            hint.rectTransform.offsetMax = new Vector2(12f + textW + 2f, hint.rectTransform.offsetMax.y);
            float gemX = 12f + textW + 6f;
            Pic(paper.transform, "HintGemIcon", GemIcon.Sprite, gemX, hintY - 1f, gemIconW, gemIconW);
            Tx(paper.transform, "HintGems", gemsText, 13, Warn, gemX + gemIconW + 2f, hintY, gemTextW, 16f, TextAlignmentOptions.Left, true).textWrappingMode = TextWrappingModes.NoWrap;
        }

        string block = CardWorkshop.CraftBlockReason(card);
        float buttonsY = hintY + 22f;
        var craft = TsButton(paper.transform, "Craft", block != null ? block.ToUpperInvariant() : L.T("ÜRET", "CRAFT"), false, 12f, buttonsY + 2f, inner * 0.5f - 16f, 44f, 15, out _);
        Dim(craft, block == null);
        craft.onClick.AddListener(() => BeginCraft(card, false));
        int adsLeft = AdRewards.WorkshopAdsLeft;
        var adCraft = TsButton(paper.transform, "CraftAd", L.T($"REKLAMLA ÜRET ({adsLeft}/{GemEconomy.DailyWorkshopAds})", $"CRAFT WITH AD ({adsLeft}/{GemEconomy.DailyWorkshopAds})"), true, inner * 0.5f + 4f, buttonsY, inner * 0.5f - 16f, 48f, 12, out var adCraftLabel);
        ButtonIcon(adCraft, adCraftLabel, CampaignSceneSetup.Art("ui_icon_play"), 22f);
        Dim(adCraft, block == null && adsLeft > 0);
        adCraft.onClick.AddListener(() => BeginCraft(card, true));
    }

    // ==================== ÜRETİM AKIŞI ====================

    /// <summary>Altın eksikse önce elmasla tamamlama, sonra Et (eksikse elmasla), sonra üretim. adAfter: üretimden sonra reklamla anında bitir.</summary>
    private void BeginCraft(BattleCard card, bool adAfter)
    {
        string block = CardWorkshop.CraftBlockReason(card);
        if (block != null) { GemActions.Toast(block); return; }
        var gm = GameManager.Instance;
        int gold = CardWorkshop.GoldCost(card);
        if (gm.Gold < gold)
        {
            GemActions.OfferGoldTopUp(gold, L.T("Bu üretim", "This craft"), () => { ContinueMeat(card, adAfter); return true; });
            return;
        }
        ContinueMeat(card, adAfter);
    }

    private void ContinueMeat(BattleCard card, bool adAfter)
    {
        int short1 = CardWorkshop.MeatShortfall(card);
        if (short1 <= 0) { StartCraft(card, false, adAfter); return; }
        int gems = CardWorkshop.MeatShortfallGems(card);
        GemConfirm.Show(L.T("ET EKSİK", "NOT ENOUGH MEAT"),
            L.T($"{short1} Et eksik (1 elmas = {GemEconomy.MeatPerGem} Et).\nElmasla tamamlayıp üretelim mi?", $"{short1} Meat short (1 gem = {GemEconomy.MeatPerGem} Meat).\nTop up with gems and craft?"),
            gems, L.T("TAMAMLA", "TOP UP"), () => StartCraft(card, true, adAfter));
    }

    private void StartCraft(BattleCard card, bool useGems, bool adAfter)
    {
        if (!CardWorkshop.TryStartCraft(card, useGems, out string reason)) { GemActions.Toast(reason ?? L.T("Başlatılamadı", "Could not start")); return; }
        AudioManager.Instance?.PlaySfx("buildingPlace");
        if (!adAfter) return;
        int index = CardWorkshop.Queue.Count - 1;
        RewardedAds.Show("workshop_craft", ok =>
        {
            if (ok && CardWorkshop.TryFinishByAd(index)) AudioManager.Instance?.PlaySfx("levelUp");
        });
    }

    // ==================== ZAMAN ====================

    private void Update()
    {
        if ((CanvasSize() - lastCanvas).sqrMagnitude > 0.25f) FitScale();
        if (Time.unscaledTime < nextTick) return;
        nextTick = Time.unscaledTime + 0.25f;
        foreach (var (job, fill, time) in timers)
        {
            if (fill == null || time == null) continue;
            fill.rectTransform.anchorMax = new Vector2(CardWorkshop.Progress01(job), 1f);
            time.text = Progression.FormatDuration(CardWorkshop.SecondsLeft(job));
        }
        var gm = GameManager.Instance;
        if (meatText != null) meatText.text = IslandEconomy.Meat.ToString();
        if (goldText != null && gm != null) goldText.text = gm.Gold.ToString();
    }

    private void OnDestroy()
    {
        CardWorkshop.OnChanged -= Rebuild;
        BattleCards.OnChanged -= Rebuild;
        if (Current == this) Current = null;
    }
}
