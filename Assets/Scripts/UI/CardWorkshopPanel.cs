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

    private RectTransform panelRt, safeRt;
    private float panelH = PanelHeight;
    private float targetScale = 1f;
    private ScrollRect gridScroll;

    private void Build(Transform canvas)
    {
        safeRt = ReferenceUiVisuals.SafeArea(canvas);
        var safe = safeRt;
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
        panelRt = panel.rectTransform;
        content = panelRt;
        ApplyLayout(AvailableSize(), false);
        panel.transform.localScale = Vector3.one * targetScale * 0.9f;
        panel.transform.DOScale(targetScale, 0.18f).SetEase(Ease.OutBack).SetLink(panel.gameObject);
        Rebuild();
    }

    /// <summary>AŞAMA 3 §3E: panel 366 geniş; 3:4 gibi kısa tuvallerde (≈497×662) ekrana sığacak kadar küçülür. Ölçü tuval boyudur (px değil). (Eski sabit-770 formülü; testler kullanır.)</summary>
    public static float FitFor(float canvasWidth, float canvasHeight)
    {
        if (!FeatureConfig.Instance.workshopPanelFit || canvasWidth <= 0f || canvasHeight <= 0f) return 1f;
        return Mathf.Min(1f, canvasHeight * 0.97f / PanelHeight, canvasWidth * 0.99f / PanelWidth);
    }

    /// <summary>4. oturum: kullanılabilir (güvenli alan) tuval boyundan panel ölçeği ve yüksekliği. Genişlik 366'ya sığacak şekilde ölçeklenir,
    /// yükseklik = kullanılabilir yükseklik / ölçek, 640..900 arası; 640 yetmiyorsa ölçek ayrıca küçülür.</summary>
    public static void Layout(float availW, float availH, out float scale, out float height)
    {
        if (!FeatureConfig.Instance.workshopPanelFit || availW <= 0f || availH <= 0f) { scale = 1f; height = PanelHeight; return; }
        float sw = Mathf.Min(1f, availW * 0.99f / PanelWidth);
        height = Mathf.Clamp(availH * 0.97f / sw, 640f, 900f);
        scale = Mathf.Min(sw, availH * 0.97f / height);
    }

    /// <summary>Güvenli alanın tuval boyu; henüz hesaplanmadıysa tüm tuval.</summary>
    private Vector2 AvailableSize()
    {
        if (safeRt != null)
        {
            var r = safeRt.rect;
            if (r.width > 1f && r.height > 1f) return r.size;
        }
        return CanvasSize();
    }

    private void ApplyLayout(Vector2 avail, bool rebuild)
    {
        lastCanvas = avail;
        // 4. oturum (2.7): üst kaynak barı (≈74 birim) ve alt radyal dock (≈40) açık kalır; pencere aradaki alana sığar,
        // başlık ve X üst barın altında kalır (dişli düğmesi X'in üstüne binmez).
        const float topReserve = 78f, bottomReserve = 36f;
        Layout(avail.x, Mathf.Max(400f, avail.y - topReserve - bottomReserve), out targetScale, out float h);
        bool heightChanged = !Mathf.Approximately(h, panelH);
        panelH = h;
        if (panelRt == null) return;
        float shift = -(topReserve - bottomReserve) * 0.5f;   // aradaki alanın ortası
        panelRt.offsetMin = new Vector2(-PanelWidth / 2f, -panelH / 2f + shift / Mathf.Max(0.01f, targetScale));
        panelRt.offsetMax = new Vector2(PanelWidth / 2f, panelH / 2f + shift / Mathf.Max(0.01f, targetScale));
        panelRt.localScale = Vector3.one * targetScale;
        if (rebuild && heightChanged) Rebuild();
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
    public void Refit() => ApplyLayout(AvailableSize(), true);

    /// <summary>Ekran/hedef doku boyu (px) verilince ölçeği ve yüksekliği CanvasScaler formülüyle (ref 390×844, match 0,5) hesaplar (testler/ekran görüntüsü).</summary>
    public void RefitForPixels(float pixelWidth, float pixelHeight)
    {
        float factor = Mathf.Sqrt(pixelWidth / 390f * (pixelHeight / 844f));
        if (content == null || factor <= 0f) return;
        ApplyLayout(new Vector2(pixelWidth / factor, pixelHeight / factor), true);
    }

    private Vector2 lastCanvas;

    private readonly List<GameObject> dynamic = new List<GameObject>();

    private void Rebuild()
    {
        if (content == null) return;
        // Eski yerleşim aynı karede görünmesin (Destroy kare sonunda işler; yeniden boyutlamada "hayalet" kalıyordu)
        foreach (var item in dynamic) if (item != null) { item.SetActive(false); Destroy(item); }
        dynamic.Clear();
        timers.Clear();
        var root = new GameObject("Dynamic", typeof(RectTransform));
        root.transform.SetParent(content, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        dynamic.Add(root);
        Fill(rt);
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, -y - h); rt.offsetMax = new Vector2(x + w, -y);
    }

    private static Sprite triangle;
    /// <summary>Krem renkli küçük ▶ (yazı tipinde ▶ olmayabilir; yeşil "mücevher" ui_icon_play yerine).</summary>
    private static Sprite PlayTriangle()
    {
        if (triangle != null) return triangle;
        const int n = 48;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        var cream = new Color(1f, 0.96f, 0.82f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = (x + 0.5f) / n, fy = (y + 0.5f) / n;
                float left = 0.22f, right = 0.88f;
                float half = 0.5f * (right - fx) / (right - left);   // sağa doğru daralır
                float edge = Mathf.Min((fx - left) * n, (half - Mathf.Abs(fy - 0.5f)) * n * 1.1f);
                float a = Mathf.Clamp01(edge + 0.5f);
                px[y * n + x] = new Color(cream.r, cream.g, cream.b, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        triangle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return triangle;
    }

    /// <summary>Atölyenin sonraki seviyelerde getirdiği ilk yenilik (Sv N: ...); en yüksek seviyede null.</summary>
    private static string NextPerk(int level, out int perkLevel)
    {
        perkLevel = level + 1;
        for (int lv = level + 1; lv <= Progression.MaxLevel; lv++)
        {
            perkLevel = lv;
            if (Progression.Workshop.QueueSlots(lv) > Progression.Workshop.QueueSlots(level))
                return L.T($"{Progression.Workshop.QueueSlots(lv)}. üretim yuvası", $"queue slot #{Progression.Workshop.QueueSlots(lv)}");
            if (lv == Progression.Workshop.RareUnlockLevel) return L.T("Nadir kartlar açılır", "unlocks Rare cards");
            if (lv == Progression.Workshop.EpicUnlockLevel) return L.T("Destansı kartlar açılır", "unlocks Epic cards");
            if (Progression.Workshop.UsesPerCraft(lv) > Progression.Workshop.UsesPerCraft(level))
                return L.T($"üretim başına {Progression.Workshop.UsesPerCraft(lv)} hak", $"{Progression.Workshop.UsesPerCraft(lv)} uses per craft");
        }
        return null;
    }

    private void Fill(Transform p)
    {
        int level = CardWorkshop.Level;
        float w = PanelWidth, H = panelH;

        // Başlık kurdelesi + kapat (ikisi de panelin içinde)
        var ribbon = At(p, "Ribbon", 48f, 10f, w - 96f, 46f).gameObject.AddComponent<Image>();
        ribbon.sprite = Ui("ui_ribbon");
        if (ribbon.sprite != null) { ribbon.type = Image.Type.Sliced; ribbon.pixelsPerUnitMultiplier = 3f; }
        ribbon.raycastTarget = false;
        Tx(ribbon.transform, "Title", L.T($"KART ATÖLYESİ · SEV. {level}", $"CARD WORKSHOP · LV. {level}"), 18, Color.white, 28f, 3f, w - 96f - 56f, 36f,
            TextAlignmentOptions.Center, false, true);
        var close = At(p, "Close", w - 44f, 14f, 32f, 32f);
        var closeImage = close.gameObject.AddComponent<Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        closeImage.color = closeImage.sprite != null ? Color.white : ReferenceUiVisuals.Red;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close);
        ReferenceUiVisuals.Text(close, "X", "X", 15, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);

        // Kaynak satırı: Et · altın · YÜKSELT (bedel + sonraki seviye notu altında)
        var gm = GameManager.Instance;
        Pic(p, "MeatIcon", IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.meatIcon : null, 22f, 66f, 28f, 28f);
        meatText = Tx(p, "Meat", IslandEconomy.Meat.ToString(), 17, Ink, 52f, 66f, 56f, 28f);
        Pic(p, "GoldIcon", CampaignSceneSetup.Art("ui_icon_coin"), 112f, 66f, 28f, 28f);
        goldText = Tx(p, "Gold", gm != null ? gm.Gold.ToString() : "0", 17, Ink, 142f, 66f, 70f, 28f);
        bool maxed = level >= Progression.MaxLevel;
        var upgrade = TsButton(p, "Upgrade", maxed ? L.T("EN YÜKSEK", "MAX LEVEL") : L.T("YÜKSELT", "UPGRADE"), false, w - 136f, 64f, 114f, 32f, 14, out _);
        upgrade.onClick.AddListener(() =>
        {
            var ui = FindFirstObjectByType<BaseWorldUI>();
            if (ui == null) return;
            var id = slotId;
            Close();
            ui.ShowBuildingInfo(BuildingType.CardWorkshop, id);
        });
        if (maxed)
        {
            Tx(p, "NextInfo", L.T("EN YÜKSEK SEVİYE", "HIGHEST LEVEL"), 12, InkMuted, 22f, 100f, w - 44f, 18f, TextAlignmentOptions.Left, true);
        }
        else
        {
            string perk = NextPerk(level, out int perkLevel);
            string next = perk != null ? L.T($"Sv {perkLevel}: {perk}", $"Lv {perkLevel}: {perk}") : L.T($"Sv {level + 1}: daha güçlü üretim", $"Lv {level + 1}: stronger crafting");
            Tx(p, "NextInfo", next, 12, InkMuted, 22f, 100f, w - 44f - 104f, 28f, TextAlignmentOptions.TopLeft, true);
            Pic(p, "UpgradeCostIcon", CampaignSceneSetup.Art("ui_icon_coin"), w - 136f + 14f, 99f, 20f, 20f);
            Tx(p, "UpgradeCost", Progression.UpgradeCost(BuildingType.CardWorkshop, level + 1).ToString(), 14, Ink, w - 136f + 38f, 99f, 76f, 20f,
                TextAlignmentOptions.Left, true);
        }
        int usesPer = CardWorkshop.UsesPerCraft;
        Tx(p, "UsesInfo", L.T($"1 üretim = {usesPer} savaş hakkı", $"1 craft = {usesPer} battle uses"), 12, InkMuted, 22f, 132f, w - 44f - 30f, 18f, TextAlignmentOptions.Left, true);
        var infoRt = At(p, "UsesInfoButton", w - 22f - 22f, 130f, 22f, 22f);
        var infoImage = infoRt.gameObject.AddComponent<Image>();
        infoImage.sprite = CampaignSceneSetup.Art("ui_icon_info");
        infoImage.preserveAspect = true;
        var infoButton = infoRt.gameObject.AddComponent<Button>();
        infoButton.targetGraphic = infoImage;
        if (infoImage.sprite == null)
        {
            infoImage.color = ReferenceUiVisuals.Blue;
            ReferenceUiVisuals.Text(infoRt, "I", "i", 14, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        }
        infoButton.onClick.AddListener(() => GemActions.Toast(L.T(
            $"Her üretim {usesPer} savaş hakkı verir. 1 savaş = 1 hak; yenilgide de düşer.",
            $"Each craft gives {usesPer} battle uses. 1 battle = 1 use; spent on defeat too.")));

        // Üretim kuyruğu
        Tx(p, "QueueTitle", L.T("ÜRETİM KUYRUĞU", "PRODUCTION QUEUE"), 13, Ink, 22f, 156f, 200f, 18f, TextAlignmentOptions.Left, true, true);
        int slots = Mathf.Max(1, CardWorkshop.QueueSlots);
        var queue = CardWorkshop.Queue;
        for (int i = 0; i < slots; i++) QueueRow(p, i, i < queue.Count ? queue[i] : null, 176f + i * 56f);

        // Seçili kart ayrıntısı altta sabit; kart ızgarası arada kaydırılabilir
        const float detailH = 204f;
        float detailTop = H - 14f - detailH;
        float gridTop = 176f + slots * 56f + 2f;
        Tx(p, "StockTitle", L.T("KARTLARIM", "MY CARDS"), 13, Ink, 22f, gridTop, 140f, 18f, TextAlignmentOptions.Left, true, true);
        float viewTop = gridTop + 20f, viewH = Mathf.Max(60f, detailTop - 6f - viewTop);
        int rows = (BattleCards.Count + 1) / 2;
        const float gap = 6f;
        float cellH = Mathf.Clamp((viewH - gap * (rows - 1)) / rows, 64f, 76f);   // 4. oturum: ≥64 (iç boşluk)
        float contentH = rows * cellH + (rows - 1) * gap;
        float cellW = (w - 44f - 8f - 8f) / 2f;   // sağda kaydırma payı (8)

        var view = At(p, "Grid", 18f, viewTop, w - 36f, viewH).gameObject;
        view.AddComponent<RectMask2D>();
        var scrollRt = (RectTransform)view.transform;
        var contentRt = ReferenceUiVisuals.Rect(scrollRt, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -contentH), Vector2.zero);
        var viewBg = view.AddComponent<Image>();   // sürükleme için ışın alıcı (görünmez)
        viewBg.color = new Color(0f, 0f, 0f, 0f);
        gridScroll = view.AddComponent<ScrollRect>();
        gridScroll.content = contentRt;
        gridScroll.viewport = scrollRt;
        gridScroll.horizontal = false;
        gridScroll.vertical = contentH > viewH + 0.5f;
        gridScroll.movementType = ScrollRect.MovementType.Clamped;
        gridScroll.scrollSensitivity = 24f;
        for (int i = 0; i < BattleCards.Count; i++)
        {
            float x = 4f + (i % 2) * (cellW + 8f);
            float y = (i / 2) * (cellH + gap);
            CardCell(contentRt, (BattleCard)i, x, y, cellW, cellH);
        }

        Detail(p, detailTop, detailH);
    }

    private void QueueRow(Transform p, int index, WorkshopJob job, float y)
    {
        float w = PanelWidth;
        var paper = Paper(p, "Queue" + index, 18f, y, w - 36f, 52f, new Color(1f, 0.97f, 0.88f, 1f), 2f);
        if (job == null)
        {
            Tx(paper.transform, "Empty", L.T("Boş yuva: aşağıdan bir kart seç ve üret", "Empty slot: pick a card below and craft it"), 13, InkMuted, 18f, 0f, w - 36f - 36f, 52f,
                TextAlignmentOptions.MidlineLeft, true);
            return;
        }
        var card = (BattleCard)job.card;
        var qIcon = Pic(paper.transform, "Icon", null, 8f, 8f, 34f, 34f);
        BattleCards.ApplyIcon(qIcon, card);
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
        ButtonIcon(ad, adLabel, PlayTriangle(), 18f);   // krem ▶ + "3/3" (yeşil mücevher yok)
        adLabel.textWrappingMode = TextWrappingModes.NoWrap;
        adLabel.fontSizeMax = 14f;
        Dim(ad, adsLeft > 0);
        ad.onClick.AddListener(() => RewardedAds.Show("workshop_finish", ok =>
        {
            if (ok && CardWorkshop.TryFinishByAd(captured)) AudioManager.Instance?.PlaySfx("levelUp");
        }));
        int gems = CardWorkshop.FinishGemCost(index);
        var gem = TsButton(paper.transform, "Gem", Mathf.Max(1, gems).ToString(), true, w - 36f - 62f, 6f, 54f, 40f, 13, out var gemLabel);
        ButtonIcon(gem, gemLabel, GemIcon.Sprite, 16f, "GemIcon");   // 4. oturum (2.6): her zaman ikon + sayı (bedava yok)
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
        float iconSize = Mathf.Clamp(h - 16f, 32f, 44f);
        var icon = Pic(paper.transform, "Icon", null, 10f, (h - iconSize) / 2f, iconSize, iconSize);
        BattleCards.ApplyIcon(icon, card);
        if (!unlocked) foreach (var img in icon.GetComponentsInChildren<Image>()) img.color = new Color(1f, 1f, 1f, 0.45f);
        float tx = 10f + iconSize + 6f, tw = w - tx - 8f;
        // 4. oturum (2.7): iç boşluk — çerçeve kalınlığı ~7 px; ad üst satırda (8 px içeride), kademe aynı satırın sağında,
        // stok alt satırda (alt kenardan 9 px yukarıda). Hiçbir yazı çerçeveye değmez.
        const float pad = 9f;
        Tx(paper.transform, "Tier", BattleCards.TierName(tier), 10, InkMuted, w - 64f - pad - 6f, pad + 2f, 64f, 13f, TextAlignmentOptions.Right, false);
        var nameText = Tx(paper.transform, "Name", BattleCards.Name(card), 14, unlocked ? Ink : InkMuted, tx, pad - 1f, tw - 66f, 20f, TextAlignmentOptions.Left, true);
        nameText.textWrappingMode = TextWrappingModes.NoWrap;   // "Keskin Oklar" / "Kalkan Kıran" tek satır, sığmazsa küçülür
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 10f; nameText.fontSizeMax = 14f;
        float y0 = h - pad - 18f - 32f;
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
        Tx(paper.transform, "Stock", line, 13, lineColor, tx, y0 + 32f, tw, 18f, TextAlignmentOptions.Left, true);
    }

    private void Detail(Transform p, float top, float height)
    {
        float w = PanelWidth;
        var card = selected;
        var tier = BattleCards.Tier(card);
        var paper = Paper(p, "Detail", 14f, top, w - 28f, height, new Color(1f, 0.98f, 0.9f), 2.2f);
        float inner = w - 28f;
        var dIcon = Pic(paper.transform, "Icon", null, 10f, 8f, 36f, 36f);
        BattleCards.ApplyIcon(dIcon, card);
        Tx(paper.transform, "Name", $"{BattleCards.Name(card)}  ·  {BattleCards.TierName(tier)}", 16, Ink, 52f, 6f, inner - 60f, 22f, TextAlignmentOptions.Left, true, true);
        Tx(paper.transform, "Effect", BattleCards.Description(card), 13, InkMuted, 52f, 28f, inner - 60f, 32f, TextAlignmentOptions.TopLeft, true);

        bool tierOpen = CardWorkshop.IsUnlocked(card);
        bool free = CardWorkshop.FirstCraftFree(card);
        int meat = CardWorkshop.MeatCost(card), gold = CardWorkshop.GoldCost(card), seconds = CardWorkshop.CraftSeconds(card);
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
        // "+N kullanım": kılıç ikonu = savaşta kullanım hakkı
        Pic(paper.transform, "UsesIcon", CampaignSceneSetup.Art("ui_icon_sword"), 236f, rowY, 20f, 24f);
        Tx(paper.transform, "Uses", L.T($"+{CardWorkshop.UsesPerCraft} kullanım", $"+{CardWorkshop.UsesPerCraft} uses"), 12, new Color(0.15f, 0.5f, 0.2f), 258f, rowY, inner - 262f, 24f, TextAlignmentOptions.Left, true);
        float hintY = rowY + 28f;
        bool gemCraft = false;
        int gemShort = 0;
        if (free) Tx(paper.transform, "Free", L.T("İLK ÜRETİM BEDAVA · 30 sn", "FIRST CRAFT FREE · 30 s"), 12, new Color(0.15f, 0.5f, 0.2f), 12f, hintY, inner - 24f, 16f, TextAlignmentOptions.Left, true);
        else if (meatShort && tierOpen)
        {
            // "165 Et eksik — Ağıl'da üret ya da [elmas] 21 ile tamamla": tek satır, sığmazsa yazı küçülür.
            gemCraft = true;
            gemShort = CardWorkshop.MeatShortfallGems(card);
            var hint = Tx(paper.transform, "MeatHint", L.T($"{meat - IslandEconomy.Meat} Et eksik — Ağıl'da üret ya da", $"{meat - IslandEconomy.Meat} Meat short — make it at the Pasture or"), 12, Warn, 12f, hintY, 200f, 16f, TextAlignmentOptions.Left, true);
            var num = Tx(paper.transform, "HintGems", gemShort.ToString(), 13, Warn, 0f, hintY, 40f, 16f, TextAlignmentOptions.Left, true);
            var tail = Tx(paper.transform, "HintTail", L.T("ile tamamla", "to top up"), 12, Warn, 0f, hintY, 80f, 16f, TextAlignmentOptions.Left, true);
            foreach (var t in new[] { hint, num, tail }) { t.textWrappingMode = TextWrappingModes.NoWrap; t.enableAutoSizing = false; }
            const float gemIconW = 16f;
            float avail = inner - 24f - gemIconW - 6f;
            float fs = 12f;
            for (int pass = 0; pass < 2; pass++)
            {
                hint.fontSize = fs; num.fontSize = fs + 1f; tail.fontSize = fs;
                float total = hint.GetPreferredValues(hint.text, 1000f, 16f).x + num.GetPreferredValues(num.text, 1000f, 16f).x + tail.GetPreferredValues(tail.text, 1000f, 16f).x;
                if (total <= avail) break;
                fs = Mathf.Max(8f, fs * avail / total);
            }
            float hw = hint.GetPreferredValues(hint.text, 1000f, 16f).x + 2f;
            float nw = num.GetPreferredValues(num.text, 1000f, 16f).x + 2f;
            float tw = tail.GetPreferredValues(tail.text, 1000f, 16f).x + 2f;
            Place(hint.rectTransform, 12f, hintY, hw, 16f);
            Pic(paper.transform, "HintGemIcon", GemIcon.Sprite, 12f + hw + 3f, hintY, gemIconW, gemIconW);
            Place(num.rectTransform, 12f + hw + 3f + gemIconW + 1f, hintY, nw, 16f);
            Place(tail.rectTransform, 12f + hw + 3f + gemIconW + 1f + nw + 2f, hintY, tw, 16f);
        }

        string block = CardWorkshop.CraftBlockReason(card);
        float buttonsY = hintY + 24f;
        float bw = inner * 0.5f - 16f;
        bool useGemCraft = gemCraft && block == null;
        var craft = TsButton(paper.transform, "Craft", block != null ? block.ToUpperInvariant() : useGemCraft ? L.T($"{gemShort} ile üret", $"{gemShort} craft") : L.T("ÜRET", "CRAFT"), useGemCraft, 12f, buttonsY + 2f, bw, 44f, 15, out var craftLabel);
        if (useGemCraft) { ButtonIcon(craft, craftLabel, GemIcon.Sprite, 20f, "GemIcon"); craftLabel.fontSizeMax = 13f; }
        craftLabel.textWrappingMode = TextWrappingModes.NoWrap;
        Dim(craft, block == null);
        craft.onClick.AddListener(() => BeginCraft(card, false));
        int adsLeft = AdRewards.WorkshopAdsLeft;
        var adCraft = TsButton(paper.transform, "CraftAd", L.T($"REKLAMLA ÜRET ({adsLeft}/{GemEconomy.DailyWorkshopAds})", $"CRAFT WITH AD ({adsLeft}/{GemEconomy.DailyWorkshopAds})"), true, inner * 0.5f + 4f, buttonsY, bw, 48f, 12, out var adCraftLabel);
        ButtonIcon(adCraft, adCraftLabel, PlayTriangle(), 14f);
        adCraftLabel.textWrappingMode = TextWrappingModes.NoWrap;
        adCraftLabel.fontSizeMax = 12f;
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
        if ((AvailableSize() - lastCanvas).sqrMagnitude > 0.25f) ApplyLayout(AvailableSize(), true);
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
