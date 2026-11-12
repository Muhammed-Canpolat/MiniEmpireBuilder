using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ada inşa arayüzü (island_base_v2 §3, §5): İNŞA ET menüsü (bina kartları + yeni bölge), bina/kale/inşaat bilgi paneli
/// (üretim, depo, işçi −/+, YÜKSELT, TAŞI, TOPLA, elmasla BİTİR) ve bölge keşfi paneli. Tiny Swords kâğıt/kurdele/düğme/kaynak
/// sprite'larıyla çizilir (kod ilkeli yalnız metin/yerleşim). Mantık <see cref="IslandEconomy"/> / <see cref="IslandVillage"/>'dedir.
/// </summary>
public sealed class IslandBuildUi
{
    public Action<IslandLayout.BuildingKind> PlaceRequested;
    public Action<string> MoveRequested;
    public Action AkinRequested;
    public Action<string> Toast;

    private RectTransform safe;
    private GameObject menuRoot, panelRoot;
    private Mode mode = Mode.None;
    private string targetId;
    private int targetZone;
    private float nextRefresh;

    private enum Mode { None, Menu, Building, Castle, Zone }

    // Menü
    private sealed class MenuCard
    {
        public IslandLayout.BuildingKind kind;
        public Image paper, icon, woodIcon, meatIcon;
        public TextMeshProUGUI title, info, wood, meat, state;
        public Button button;
    }
    private readonly List<MenuCard> cards = new List<MenuCard>();
    private TextMeshProUGUI zoneTitle, zoneInfo, zoneState;
    private Button zoneButton;

    // Panel
    private Image panelIcon, panelWoodIcon, panelMeatIcon;
    private TextMeshProUGUI panelTitle, panelBody, panelWood, panelMeat, panelTime, panelNote, workersLabel;
    private Button primary, secondary, tertiary, workerMinus, workerPlus;
    private TextMeshProUGUI primaryLabel, secondaryLabel, tertiaryLabel;

    public bool Open => mode != Mode.None;
    public bool MenuOpen => mode == Mode.Menu;
    public string OpenTargetId => targetId;

    private static Sprite Ui(string name)
    {
        var catalog = IslandVisualCatalog.Instance;
        return catalog != null ? catalog.Single(name) : null;
    }

    private static Image NineSlice(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high, string sprite, Color tint, float ppu)
    {
        var image = ReferenceUiVisuals.Image(parent, name, min, max, low, high, tint);
        image.sprite = Ui(sprite);
        if (image.sprite != null) { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = ppu; }
        return image;
    }

    private static Button TsButton(Transform parent, string name, string label, bool red, Vector2 min, Vector2 max, Vector2 low, Vector2 high,
        float size, out TextMeshProUGUI text)
    {
        var bg = NineSlice(parent, name, min, max, low, high, red ? "ui_button_red" : "ui_button_blue", Color.white, 3f);
        bg.raycastTarget = true;
        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;
        text = ReferenceUiVisuals.Text(bg.transform, "Label", label, size, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -2), true);
        text.textWrappingMode = TextWrappingModes.Normal;
        return button;
    }

    private static readonly Color Ink = new Color(0.30f, 0.17f, 0.08f);

    public void Build(RectTransform safeArea)
    {
        safe = safeArea;
        BuildMenu();
        BuildPanel();
        menuRoot.SetActive(false);
        panelRoot.SetActive(false);
    }

    // ==================== İNŞA ET MENÜSÜ ====================

    private static readonly IslandLayout.BuildingKind[] MenuKinds =
    {
        IslandLayout.BuildingKind.House, IslandLayout.BuildingKind.Lumber, IslandLayout.BuildingKind.Pasture,
        IslandLayout.BuildingKind.Tower, IslandLayout.BuildingKind.Archery,
    };

    private void BuildMenu()
    {
        var panel = NineSlice(safe, "BuildMenu", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.66f), Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
        panel.raycastTarget = true;
        menuRoot = panel.gameObject;
        var ribbon = NineSlice(panel.transform, "Ribbon", new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0, -34), new Vector2(0, 20), "ui_ribbon", Color.white, 3f);
        ReferenceUiVisuals.Text(ribbon.transform, "Title", L.T("İNŞA ET", "BUILD"), 20, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(40, 8), new Vector2(-40, -4), true);
        var close = ReferenceUiVisuals.Button(panel.transform, "Close", "X", false, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-42, -40), new Vector2(-12, -12), 14);
        close.onClick.AddListener(Close);

        int rows = MenuKinds.Length + 1;
        var list = ReferenceUiVisuals.Rect(panel.transform, "Cards", Vector2.zero, Vector2.one, new Vector2(16, 14), new Vector2(-16, -48));
        for (int i = 0; i < MenuKinds.Length; i++)
        {
            float top = 1f - i / (float)rows, bottom = 1f - (i + 1) / (float)rows;
            var card = new MenuCard { kind = MenuKinds[i] };
            var rect = ReferenceUiVisuals.Rect(list, "Card" + i, new Vector2(0, bottom), new Vector2(1, top), new Vector2(0, 3), new Vector2(0, -3));
            card.paper = NineSlice(rect, "Paper", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "ui_paper_special", Color.white, 4f);
            card.paper.raycastTarget = true;
            card.button = card.paper.gameObject.AddComponent<Button>();
            card.button.targetGraphic = card.paper;
            card.button.transition = Selectable.Transition.None;
            var captured = card;
            card.button.onClick.AddListener(() => OnCard(captured));
            card.icon = ReferenceUiVisuals.Image(rect, "Icon", new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 6), new Vector2(86, -6), Color.white);
            card.icon.sprite = Ui(IslandVillage.SpriteName(card.kind));
            card.icon.preserveAspect = true;
            var light = new Color(1f, 0.95f, 0.78f);
            card.title = ReferenceUiVisuals.Text(rect, "Title", IslandVillage.KindName(card.kind), 16, light, TextAlignmentOptions.Left,
                new Vector2(0, 0.5f), new Vector2(0.72f, 1), new Vector2(92, 0), new Vector2(0, -4), true);
            card.info = ReferenceUiVisuals.Text(rect, "Info", "", 11, light, TextAlignmentOptions.Left,
                new Vector2(0, 0), new Vector2(0.62f, 0.52f), new Vector2(92, 4), new Vector2(0, 0), false, true);
            card.woodIcon = ReferenceUiVisuals.Image(rect, "WoodIcon", new Vector2(0.62f, 0.5f), new Vector2(0.62f, 0.5f), new Vector2(0, 2), new Vector2(26, 28), Color.white);
            card.woodIcon.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.woodIcon : null;
            card.woodIcon.preserveAspect = true;
            card.wood = ReferenceUiVisuals.Text(rect, "Wood", "", 14, light, TextAlignmentOptions.Left,
                new Vector2(0.62f, 0.5f), new Vector2(0.62f, 0.5f), new Vector2(28, 2), new Vector2(76, 28), false, true);
            card.meatIcon = ReferenceUiVisuals.Image(rect, "MeatIcon", new Vector2(0.62f, 0.5f), new Vector2(0.62f, 0.5f), new Vector2(0, -28), new Vector2(26, -2), Color.white);
            card.meatIcon.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.meatIcon : null;
            card.meatIcon.preserveAspect = true;
            card.meat = ReferenceUiVisuals.Text(rect, "Meat", "", 14, light, TextAlignmentOptions.Left,
                new Vector2(0.62f, 0.5f), new Vector2(0.62f, 0.5f), new Vector2(28, -28), new Vector2(76, -2), false, true);
            card.state = ReferenceUiVisuals.Text(rect, "State", "", 12, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Right,
                new Vector2(0.78f, 0), new Vector2(1, 1), new Vector2(0, 4), new Vector2(-10, -4), true);
            cards.Add(card);
        }
        // Yeni bölge satırı
        {
            int i = MenuKinds.Length;
            float top = 1f - i / (float)rows, bottom = 1f - (i + 1) / (float)rows;
            var rect = ReferenceUiVisuals.Rect(list, "ZoneCard", new Vector2(0, bottom), new Vector2(1, top), new Vector2(0, 3), new Vector2(0, -3));
            var paper = NineSlice(rect, "Paper", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "ui_paper_special", new Color(0.85f, 0.95f, 1f), 4f);
            paper.raycastTarget = true;
            zoneButton = paper.gameObject.AddComponent<Button>();
            zoneButton.targetGraphic = paper;
            zoneButton.transition = Selectable.Transition.None;
            zoneButton.onClick.AddListener(() => { int z = NextZone(); if (z > 0) ShowZone(z); });
            var light = new Color(1f, 0.95f, 0.78f);
            zoneTitle = ReferenceUiVisuals.Text(rect, "Title", "", 16, light, TextAlignmentOptions.Left,
                new Vector2(0, 0.5f), new Vector2(0.8f, 1), new Vector2(14, 0), new Vector2(0, -12), true);
            zoneInfo = ReferenceUiVisuals.Text(rect, "Info", "", 11, light, TextAlignmentOptions.Left,
                new Vector2(0, 0), new Vector2(0.8f, 0.52f), new Vector2(14, 6), new Vector2(0, 0), false, true);
            zoneState = ReferenceUiVisuals.Text(rect, "State", "", 12, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Right,
                new Vector2(0.78f, 0), new Vector2(1, 1), new Vector2(0, 4), new Vector2(-10, -4), true);
        }
    }

    private static int NextZone()
    {
        var save = IslandSession.Save;
        for (int z = 1; z <= Progression.Island.LastRevealableZone; z++)
            if (!IslandGrid.ZoneOpen(z, save?.zoneMask ?? 1)) return z;
        return 0;
    }

    private void OnCard(MenuCard card)
    {
        string reason = KindBlockedReason(card.kind);
        if (reason != null) { Toast?.Invoke(reason); return; }
        Close();
        PlaceRequested?.Invoke(card.kind);
    }

    /// <summary>Bu tür şu an kurulabilir mi (adet, Usta, savaş); değilse neden.</summary>
    public static string KindBlockedReason(IslandLayout.BuildingKind kind)
    {
        var save = IslandSession.Save;
        if (save == null) return L.T("Kayıt yok", "No save");
        if (kind == IslandLayout.BuildingKind.Archery) return L.T("Okçuluk Alanı hazır kurulu; taşınabilir", "The archery range is ready; it can be moved");
        int have = IslandEconomy.CountOf(kind), max = Progression.Island.MaxCount(kind, IslandEconomy.Keep);
        if (have >= max) return L.T($"Adet doldu ({have}/{max})", $"Limit reached ({have}/{max})");
        if (IslandEconomy.InDefense) return L.T("Savaşta inşa yapılamaz", "Cannot build during defense");
        if (IslandEconomy.BuilderBusy) return L.T("Ada Ustası meşgul", "Island Builder is busy");
        return null;
    }

    private void RefreshMenu()
    {
        var save = IslandSession.Save;
        foreach (var card in cards)
        {
            var cost = Progression.Island.PlaceCost(card.kind);
            int have = IslandEconomy.CountOf(card.kind), max = Progression.Island.MaxCount(card.kind, IslandEconomy.Keep);
            card.wood.text = card.kind == IslandLayout.BuildingKind.Archery ? "-" : cost.wood.ToString();
            card.meat.text = card.kind == IslandLayout.BuildingKind.Archery ? "-" : cost.meat.ToString();
            card.wood.color = save != null && save.wood < cost.wood ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.95f, 0.78f);
            card.meat.color = save != null && save.meat < cost.meat ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.95f, 0.78f);
            card.info.text = KindInfo(card.kind);
            string blocked = KindBlockedReason(card.kind);
            card.state.text = $"{have}/{max}";
            card.paper.color = blocked == null ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            card.icon.color = blocked == null ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        }
        int zone = NextZone();
        if (zone <= 0)
        {
            zoneTitle.text = L.T("Yeni bölge", "New zone");
            zoneInfo.text = L.T("Sıradaki bölgeler sonraki güncellemede açılır.", "More zones open in a later update.");
            zoneState.text = "";
        }
        else
        {
            zoneTitle.text = L.T($"Keşfet: {IslandVillage.ZoneName(zone)}", $"Explore: {IslandVillage.ZoneName(zone)}");
            zoneInfo.text = L.T($"{Progression.Island.ZoneWood[zone]} Odun" + (Progression.Island.ZoneMeat[zone] > 0 ? $" · {Progression.Island.ZoneMeat[zone]} Et" : "")
                + $" · {Progression.FormatDuration(Progression.Island.ZoneSeconds[zone])}",
                $"{Progression.Island.ZoneWood[zone]} Wood" + (Progression.Island.ZoneMeat[zone] > 0 ? $" · {Progression.Island.ZoneMeat[zone]} Meat" : "")
                + $" · {Progression.FormatDuration(Progression.Island.ZoneSeconds[zone])}");
            zoneState.text = IslandEconomy.ZoneBlockedReason(zone) == null ? L.T("AÇ", "OPEN") : "";
        }
    }

    private static string KindInfo(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.House: return L.T("+2 piyon (Sv5'te 3, Sv10'da 4)", "+2 pawns (3 at L5, 4 at L10)");
            case IslandLayout.BuildingKind.Lumber: return L.T("Odun üretir; yakında ağaç ister", "Makes Wood; needs trees nearby");
            case IslandLayout.BuildingKind.Pasture: return L.T("Et üretir (kıt); çayır ister", "Makes Meat (scarce); needs meadow");
            case IslandLayout.BuildingKind.Tower: return L.T("Savunma; adacığa da kurulur", "Defense; also fits on islets");
            default: return L.T("Hazır kurulu", "Ready-made");
        }
    }

    // ==================== BİLGİ PANELİ ====================

    private void BuildPanel()
    {
        var panel = NineSlice(safe, "BuildingPanel", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.40f), Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
        panel.raycastTarget = true;
        panelRoot = panel.gameObject;
        var ribbon = NineSlice(panel.transform, "Ribbon", new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0, -34), new Vector2(0, 20), "ui_ribbon", Color.white, 3f);
        panelTitle = ReferenceUiVisuals.Text(ribbon.transform, "Title", "", 20, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(40, 8), new Vector2(-40, -4), true);
        var close = ReferenceUiVisuals.Button(panel.transform, "Close", "X", false, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-42, -40), new Vector2(-12, -12), 14);
        close.onClick.AddListener(Close);

        panelIcon = ReferenceUiVisuals.Image(panel.transform, "Icon", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -150), new Vector2(128, -42), Color.white);
        panelIcon.preserveAspect = true;
        panelBody = ReferenceUiVisuals.Text(panel.transform, "Body", "", 19, Ink, TextAlignmentOptions.TopLeft,
            new Vector2(0, 0.38f), new Vector2(1, 1), new Vector2(138, 0), new Vector2(-18, -48), false, true);
        panelBody.textWrappingMode = TextWrappingModes.Normal;
        panelBody.enableAutoSizing = true;
        panelBody.fontSizeMin = 12f;

        // Maliyet satırı: Odun / Et / süre
        panelWoodIcon = ReferenceUiVisuals.Image(panel.transform, "WoodIcon", new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(18, -6), new Vector2(50, 26), Color.white);
        panelWoodIcon.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.woodIcon : null;
        panelWoodIcon.preserveAspect = true;
        panelWood = ReferenceUiVisuals.Text(panel.transform, "Wood", "", 16, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(52, -4), new Vector2(120, 24), false, true);
        panelMeatIcon = ReferenceUiVisuals.Image(panel.transform, "MeatIcon", new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(124, -6), new Vector2(156, 26), Color.white);
        panelMeatIcon.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.meatIcon : null;
        panelMeatIcon.preserveAspect = true;
        panelMeat = ReferenceUiVisuals.Text(panel.transform, "Meat", "", 16, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(158, -4), new Vector2(226, 24), false, true);
        panelTime = ReferenceUiVisuals.Text(panel.transform, "Time", "", 15, Ink, TextAlignmentOptions.Right,
            new Vector2(0.5f, 0.38f), new Vector2(1, 0.38f), new Vector2(0, -4), new Vector2(-18, 24), false, true);
        panelNote = ReferenceUiVisuals.Text(panel.transform, "Note", "", 12, new Color(0.55f, 0.2f, 0.1f), TextAlignmentOptions.Center,
            new Vector2(0, 0.24f), new Vector2(1, 0.38f), new Vector2(18, 0), new Vector2(-18, -2), false, true);

        // İşçi satırı
        workersLabel = ReferenceUiVisuals.Text(panel.transform, "Workers", "", 14, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.24f), new Vector2(0.5f, 0.38f), new Vector2(18, 0), new Vector2(0, -2), false, true);
        workerMinus = ReferenceUiVisuals.Button(panel.transform, "WorkerMinus", "-", false, new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), new Vector2(0, 0), new Vector2(40, 34), 16);
        workerPlus = ReferenceUiVisuals.Button(panel.transform, "WorkerPlus", "+", false, new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), new Vector2(46, 0), new Vector2(86, 34), 16);
        workerMinus.onClick.AddListener(() => ChangeWorkers(-1));
        workerPlus.onClick.AddListener(() => ChangeWorkers(+1));

        primary = TsButton(panel.transform, "Primary", "", false, new Vector2(0.04f, 0), new Vector2(0.52f, 0), new Vector2(0, 12), new Vector2(0, 62), 16, out primaryLabel);
        secondary = TsButton(panel.transform, "Secondary", "", false, new Vector2(0.54f, 0), new Vector2(0.96f, 0), new Vector2(0, 12), new Vector2(0, 62), 16, out secondaryLabel);
        tertiary = TsButton(panel.transform, "Tertiary", "", true, new Vector2(0.04f, 0), new Vector2(0.96f, 0), new Vector2(0, 68), new Vector2(0, 112), 15, out tertiaryLabel);
        primary.onClick.AddListener(OnPrimary);
        secondary.onClick.AddListener(OnSecondary);
        tertiary.onClick.AddListener(OnTertiary);
    }

    // ==================== AÇ / KAPAT ====================

    public void ShowMenu()
    {
        mode = Mode.Menu; targetId = null;
        menuRoot.SetActive(true); panelRoot.SetActive(false);
        RefreshMenu();
    }

    public void Close()
    {
        mode = Mode.None; targetId = null;
        if (menuRoot != null) menuRoot.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void ShowBuilding(string id)
    {
        mode = Mode.Building; targetId = id;
        menuRoot.SetActive(false); panelRoot.SetActive(true);
        RefreshPanel();
    }

    public void ShowCastle()
    {
        mode = Mode.Castle; targetId = null;
        menuRoot.SetActive(false); panelRoot.SetActive(true);
        RefreshPanel();
    }

    public void ShowZone(int zone)
    {
        mode = Mode.Zone; targetZone = zone; targetId = null;
        menuRoot.SetActive(false); panelRoot.SetActive(true);
        RefreshPanel();
    }

    public void Tick()
    {
        if (!Open || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        if (mode == Mode.Menu) RefreshMenu(); else RefreshPanel();
    }

    // ==================== PANEL İÇERİĞİ ====================

    private enum PrimaryAction { None, Upgrade, Explore, Akin }
    private PrimaryAction primaryAction;
    private enum TertiaryAction { None, Collect, FinishNow }
    private TertiaryAction tertiaryAction;
    private bool secondaryIsMove;

    private void SetButtons(string primaryText, bool primaryOn, string secondaryText, bool secondaryOn, string tertiaryText, bool tertiaryOn)
    {
        primary.gameObject.SetActive(primaryText != null);
        if (primaryText != null) { primaryLabel.text = primaryText; primary.interactable = primaryOn; }
        secondary.gameObject.SetActive(secondaryText != null);
        if (secondaryText != null) { secondaryLabel.text = secondaryText; secondary.interactable = secondaryOn; }
        tertiary.gameObject.SetActive(tertiaryText != null);
        if (tertiaryText != null) { tertiaryLabel.text = tertiaryText; tertiary.interactable = tertiaryOn; }
    }

    private void SetCost(int wood, int meat, string time, bool show)
    {
        panelWoodIcon.gameObject.SetActive(show && wood > 0); panelWood.gameObject.SetActive(show && wood > 0);
        panelMeatIcon.gameObject.SetActive(show && meat > 0); panelMeat.gameObject.SetActive(show && meat > 0);
        panelTime.gameObject.SetActive(show);
        var save = IslandSession.Save;
        panelWood.text = wood.ToString();
        panelMeat.text = meat.ToString();
        panelWood.color = save != null && save.wood < wood ? new Color(0.75f, 0.15f, 0.1f) : Ink;
        panelMeat.color = save != null && save.meat < meat ? new Color(0.75f, 0.15f, 0.1f) : Ink;
        panelTime.text = time ?? "";
    }

    private void RefreshPanel()
    {
        if (mode == Mode.Zone) { RefreshZonePanel(); return; }
        if (mode == Mode.Castle) { RefreshCastlePanel(); return; }
        var entry = IslandEconomy.Find(targetId);
        if (entry == null) { Close(); return; }
        var kind = IslandEconomy.KindOf(entry);
        var save = IslandSession.Save;
        panelIcon.sprite = Ui(IslandVillage.SpriteName(kind));
        bool building = entry.level < 1;
        int level = kind == IslandLayout.BuildingKind.Archery ? Mathf.Max(1, IslandSession.ArcherLevel()) : entry.level;
        panelTitle.text = building ? IslandVillage.KindName(kind) : L.T($"{IslandVillage.KindName(kind)} · Sv {level}", $"{IslandVillage.KindName(kind)} · Lv {level}");
        var body = new System.Text.StringBuilder();
        workerMinus.gameObject.SetActive(false); workerPlus.gameObject.SetActive(false); workersLabel.text = "";
        panelNote.text = "";
        var task = save != null ? save.task : null;
        bool taskHere = task != null && task.Active && task.buildingId == entry.id;

        if (building)
        {
            body.AppendLine(L.T("İnşaat sürüyor. Ada Ustası çekiçle çalışıyor.", "Under construction. The Island Builder is hammering."));
        }
        else
        {
            AppendStats(body, entry, kind, level);
            if (IslandEconomy.IsProducer(kind)) SetupWorkers(entry, kind);
        }
        panelBody.text = body.ToString();

        // Düğmeler
        tertiaryAction = TertiaryAction.None;
        primaryAction = PrimaryAction.None;
        string tertiaryText = null; bool tertiaryOn = false;
        if (taskHere)
        {
            int gems = IslandEconomy.FinishGemCost();
            tertiaryAction = TertiaryAction.FinishNow;
            tertiaryText = gems <= 0 ? L.T("HEMEN BİTİR · BEDAVA", "FINISH NOW · FREE") : L.T($"HEMEN BİTİR · {gems} elmas", $"FINISH NOW · {gems} gems");
            tertiaryOn = true;
            SetCost(0, 0, Progression.FormatDuration(IslandEconomy.TaskSecondsLeft), true);
        }
        else if (IslandEconomy.IsProducer(kind) && !building && entry.stock >= 1f)
        {
            tertiaryAction = TertiaryAction.Collect;
            tertiaryText = L.T($"TOPLA · {Mathf.FloorToInt(entry.stock)} " + (kind == IslandLayout.BuildingKind.Lumber ? "Odun" : "Et"),
                $"COLLECT · {Mathf.FloorToInt(entry.stock)} " + (kind == IslandLayout.BuildingKind.Lumber ? "Wood" : "Meat"));
            tertiaryOn = true;
        }

        if (building) { SetCost(0, 0, null, false); SetButtons(null, false, null, false, tertiaryText, tertiaryOn); return; }

        string blocked = IslandEconomy.UpgradeBlockedReason(entry);
        string primaryText = null; bool primaryOn = false;
        if (kind != IslandLayout.BuildingKind.Archery)
        {
            var cost = IslandEconomy.UpgradeCost(entry);
            if (!taskHere) SetCost(cost.wood, cost.meat, Progression.FormatDuration(cost.seconds), blocked == null || IslandEconomy.BuilderBusy || IslandEconomy.InDefense || entry.level < Progression.Island.MaxBuildingLevel(IslandEconomy.Keep));
            bool canPay = IslandEconomy.CanSpend(cost.wood, cost.meat);
            int gems = IslandEconomy.ShortfallGems(cost.wood, cost.meat);
            if (blocked != null) { primaryText = blocked.Length > 26 ? L.T("YÜKSELT", "UPGRADE") : blocked; panelNote.text = blocked; }
            else if (canPay) { primaryText = L.T($"YÜKSELT → Sv {entry.level + 1}", $"UPGRADE → Lv {entry.level + 1}"); primaryOn = true; }
            else { primaryText = L.T($"TAMAMLA · {gems} elmas", $"TOP UP · {gems} gems"); primaryOn = GameManager.Instance != null && GameManager.Instance.Gems >= gems; }
            primaryAction = PrimaryAction.Upgrade;
            if (blocked != null) primaryOn = false;
        }
        else
        {
            SetCost(0, 0, null, false);
            panelNote.text = L.T("Okçu Kaptan'ın seviyesi Kıyı Kalesi ile yükselir.", "The Archer Captain levels up with the keep.");
        }
        bool canMove = !IslandEconomy.InDefense;
        SetButtons(primaryText, primaryOn, L.T("TAŞI", "MOVE"), canMove, tertiaryText, tertiaryOn);
        secondaryIsMove = true;
    }

    private void AppendStats(System.Text.StringBuilder body, IslandPlacedBuilding entry, IslandLayout.BuildingKind kind, int level)
    {
        var save = IslandSession.Save;
        switch (kind)
        {
            case IslandLayout.BuildingKind.Lumber:
            case IslandLayout.BuildingKind.Pasture:
            {
                bool wood = kind == IslandLayout.BuildingKind.Lumber;
                var assign = IslandEconomy.AssignWorkers(save.placed);
                assign.TryGetValue(entry.id, out int crew);
                float rate = IslandEconomy.Rate(entry, crew, save.zoneMask, save.placed) * IslandRepair.ProductionMultiplier();
                string unit = wood ? L.T("Odun", "Wood") : L.T("Et", "Meat");
                body.AppendLine(L.T($"Üretim: {rate:0.#} {unit}/sa  (tam kadro {IslandEconomy.FullRate(entry):0})", $"Output: {rate:0.#} {unit}/h  (full crew {IslandEconomy.FullRate(entry):0})"));
                body.AppendLine(L.T($"Depo: {Mathf.FloorToInt(entry.stock)}/{Mathf.FloorToInt(IslandEconomy.Capacity(entry))} (8 sa)", $"Storage: {Mathf.FloorToInt(entry.stock)}/{Mathf.FloorToInt(IslandEconomy.Capacity(entry))} (8 h)"));
                var center = IslandGrid.CenterCell(kind, entry.col, entry.row);
                if (wood)
                {
                    int trees = IslandGrid.TreesNear(center, save.zoneMask), need = Progression.Island.TreesPerSlot * Progression.Island.LumberSlots(level);
                    body.AppendLine(L.T($"Yakın ağaç {trees}/{need}", $"Nearby trees {trees}/{need}") + (trees < need ? L.T("  (üretim düşer)", "  (output drops)") : ""));
                }
                else
                {
                    int cells = IslandGrid.MeadowNear(center);
                    body.AppendLine(L.T($"Yakın çayır {cells}/{Progression.Island.MeadowCells}", $"Nearby meadow {cells}/{Progression.Island.MeadowCells}") + (cells < Progression.Island.MeadowCells ? L.T("  (×0,5)", "  (x0.5)") : ""));
                }
                if (IslandRepair.ProductionMultiplier() < 1f) body.AppendLine(L.T("Onarım sürerken üretim yavaş (×0,5)", "Output halved while repairing (x0.5)"));
                break;
            }
            case IslandLayout.BuildingKind.House:
                body.AppendLine(L.T($"+{Progression.Island.HousePawns(level)} piyon", $"+{Progression.Island.HousePawns(level)} pawns"));
                body.AppendLine(L.T($"Toplam piyon {IslandEconomy.PawnPool(save.placed)}", $"Total pawns {IslandEconomy.PawnPool(save.placed)}"));
                break;
            case IslandLayout.BuildingKind.Tower:
            {
                float damage = Progression.TowerDamage(BuildingType.ArcherTower, level) * Progression.TowerUnits(BuildingType.ArcherTower, level);
                body.AppendLine(L.T($"Hasar {damage:0} · Menzil {Progression.Island.TowerRange:0.#}", $"Damage {damage:0} · Range {Progression.Island.TowerRange:0.#}"));
                body.AppendLine(L.T($"Savaş canı {Progression.Island.StructureHp("Tower", level):0}", $"Battle HP {Progression.Island.StructureHp("Tower", level):0}"));
                break;
            }
            default:
                body.AppendLine(L.T($"Okçu Kaptan Sv {IslandSession.ArcherLevel()}", $"Archer Captain Lv {IslandSession.ArcherLevel()}"));
                body.AppendLine(L.T($"Savaş canı {Progression.Island.StructureHp("Archery", level):0}", $"Battle HP {Progression.Island.StructureHp("Archery", level):0}"));
                break;
        }
    }

    private void SetupWorkers(IslandPlacedBuilding entry, IslandLayout.BuildingKind kind)
    {
        var save = IslandSession.Save;
        var assign = IslandEconomy.AssignWorkers(save.placed);
        assign.TryGetValue(entry.id, out int crew);
        int slots = Progression.Island.Slots(kind, entry.level);
        workersLabel.text = L.T($"İşçi {crew}/{slots}" + (entry.workers >= 0 ? " (elle)" : ""), $"Workers {crew}/{slots}" + (entry.workers >= 0 ? " (manual)" : ""));
        workerMinus.gameObject.SetActive(true); workerPlus.gameObject.SetActive(true);
        workerMinus.interactable = crew > 0;
        workerPlus.interactable = crew < slots && IslandEconomy.PawnPool(save.placed) > TotalAssigned(assign);
    }

    private static int TotalAssigned(Dictionary<string, int> assign)
    {
        int total = 0;
        foreach (var pair in assign) total += pair.Value;
        return total;
    }

    private void ChangeWorkers(int delta)
    {
        var entry = IslandEconomy.Find(targetId);
        if (entry == null) return;
        var assign = IslandEconomy.AssignWorkers(IslandSession.Save.placed);
        assign.TryGetValue(entry.id, out int crew);
        // Elle ayar: önce üretimi bu ana kadar işle, sonra yeni dağılımı yaz
        IslandEconomy.Accumulate(IslandSession.Save, IslandEconomy.Now);
        IslandEconomy.SetWorkers(entry.id, Mathf.Clamp(crew + delta, 0, Progression.Island.Slots(IslandEconomy.KindOf(entry), entry.level)));
        IslandVillage.Instance?.RebuildPawns();
        RefreshPanel();
    }

    private void RefreshCastlePanel()
    {
        var save = IslandSession.Save;
        panelIcon.sprite = Ui("building_castle");
        int keep = IslandEconomy.Keep;
        panelTitle.text = L.T($"Kıyı Kalesi · Sv {keep}", $"Coastal Keep · Lv {keep}");
        panelBody.text = L.T($"Kale canı {Progression.Island.CastleHp(keep):0}\nCüzdan: Odun {save.wood}/{IslandEconomy.WoodCap}, Et {save.meat}/{IslandEconomy.MeatCap}\nPiyon {IslandEconomy.PawnPool(save.placed)} · Ev başına +2",
            $"Keep HP {Progression.Island.CastleHp(keep):0}\nWallet: Wood {save.wood}/{IslandEconomy.WoodCap}, Meat {save.meat}/{IslandEconomy.MeatCap}\nPawns {IslandEconomy.PawnPool(save.placed)} · +2 per house");
        workerMinus.gameObject.SetActive(false); workerPlus.gameObject.SetActive(false); workersLabel.text = "";
        panelNote.text = L.T("Kıyı Kalesi, Ana Üs seviyesini izler.", "The keep follows the Main Base level.");
        SetCost(0, 0, null, false);
        primaryAction = PrimaryAction.Akin; tertiaryAction = TertiaryAction.None;
        SetButtons(L.T("AKIN PANOSU", "RAID BOARD"), true, null, false, null, false);
        secondaryIsMove = false;
    }

    private void RefreshZonePanel()
    {
        int zone = targetZone;
        var save = IslandSession.Save;
        panelIcon.sprite = IslandVisualCatalog.Instance != null && IslandVisualCatalog.Instance.clouds != null && IslandVisualCatalog.Instance.clouds.Length > 0
            ? IslandVisualCatalog.Instance.clouds[0] : null;
        panelTitle.text = IslandVillage.ZoneName(zone);
        workerMinus.gameObject.SetActive(false); workerPlus.gameObject.SetActive(false); workersLabel.text = "";
        var task = save.task;
        bool here = task != null && task.Active && task.kind == "zone" && task.zone == zone;
        bool open = IslandGrid.ZoneOpen(zone, save.zoneMask);
        panelBody.text = open ? L.T("Bölge açık.", "Zone is open.")
            : L.T("Bulutların altında: yeni ağaç, çayır ve alan getirir; yeni bir çıkarma kumsalı da demektir.", "Hidden under clouds: brings trees, meadow and space, and possibly a new landing beach.");
        panelNote.text = "";
        primaryAction = PrimaryAction.None; tertiaryAction = TertiaryAction.None;
        secondaryIsMove = false;
        if (here)
        {
            int gems = IslandEconomy.FinishGemCost();
            tertiaryAction = TertiaryAction.FinishNow;
            SetCost(0, 0, Progression.FormatDuration(IslandEconomy.TaskSecondsLeft), true);
            SetButtons(null, false, null, false, gems <= 0 ? L.T("HEMEN BİTİR · BEDAVA", "FINISH NOW · FREE") : L.T($"HEMEN BİTİR · {gems} elmas", $"FINISH NOW · {gems} gems"), true);
            return;
        }
        if (open || !Progression.Island.ZoneRevealable(zone)) { SetCost(0, 0, null, false); panelNote.text = open ? "" : IslandEconomy.ZoneBlockedReason(zone); SetButtons(null, false, null, false, null, false); return; }
        int wood = Progression.Island.ZoneWood[zone], meat = Progression.Island.ZoneMeat[zone];
        SetCost(wood, meat, Progression.FormatDuration(Progression.Island.ZoneSeconds[zone]), true);
        string blocked = IslandEconomy.ZoneBlockedReason(zone);
        bool canPay = IslandEconomy.CanSpend(wood, meat);
        int shortfall = IslandEconomy.ShortfallGems(wood, meat);
        primaryAction = PrimaryAction.Explore;
        string text = canPay ? L.T("KEŞFET", "EXPLORE") : L.T($"TAMAMLA · {shortfall} elmas", $"TOP UP · {shortfall} gems");
        bool on = blocked == null && (canPay || GameManager.Instance != null && GameManager.Instance.Gems >= shortfall);
        if (blocked != null) panelNote.text = blocked;
        SetButtons(text, on, null, false, null, false);
    }

    // ==================== EYLEMLER ====================

    private void OnPrimary()
    {
        string reason;
        switch (primaryAction)
        {
            case PrimaryAction.Upgrade:
            {
                var entry = IslandEconomy.Find(targetId);
                if (entry == null) return;
                var cost = IslandEconomy.UpgradeCost(entry);
                bool useGems = !IslandEconomy.CanSpend(cost.wood, cost.meat);
                if (!IslandEconomy.TryStartUpgrade(entry.id, useGems, out reason)) { Toast?.Invoke(reason); return; }
                IslandVillage.Instance?.RebuildPawns();
                RefreshPanel();
                break;
            }
            case PrimaryAction.Explore:
            {
                bool useGems = !IslandEconomy.CanSpend(Progression.Island.ZoneWood[targetZone], Progression.Island.ZoneMeat[targetZone]);
                if (!IslandEconomy.TryStartZone(targetZone, useGems, out reason)) { Toast?.Invoke(reason); return; }
                RefreshPanel();
                break;
            }
            case PrimaryAction.Akin:
                Close();
                AkinRequested?.Invoke();
                break;
        }
    }

    private void OnSecondary()
    {
        if (!secondaryIsMove || mode != Mode.Building) return;
        string id = targetId;
        Close();
        MoveRequested?.Invoke(id);
    }

    private void OnTertiary()
    {
        switch (tertiaryAction)
        {
            case TertiaryAction.Collect:
            {
                var entry = IslandEconomy.Find(targetId);
                if (entry == null) return;
                int amount = IslandEconomy.CollectFrom(entry);
                Toast?.Invoke(amount > 0 ? L.T($"+{amount} " + (IslandEconomy.KindOf(entry) == IslandLayout.BuildingKind.Lumber ? "Odun" : "Et"),
                    $"+{amount} " + (IslandEconomy.KindOf(entry) == IslandLayout.BuildingKind.Lumber ? "Wood" : "Meat")) : L.T("Cüzdan dolu", "Wallet is full"));
                IslandVillage.Instance?.RefreshPiles();
                RefreshPanel();
                break;
            }
            case TertiaryAction.FinishNow:
            {
                if (!IslandEconomy.TryFinishNow(out string reason)) { Toast?.Invoke(reason); return; }
                Close();
                break;
            }
        }
    }
}
