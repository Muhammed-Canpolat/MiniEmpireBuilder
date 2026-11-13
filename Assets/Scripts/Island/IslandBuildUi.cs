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
    private GameObject menuRoot, panelRoot, staffRoot;
    private Mode mode = Mode.None;
    private string targetId;
    private int targetZone;
    private Vector2Int targetTree;
    private float nextRefresh;

    private enum Mode { None, Menu, Building, Castle, Zone, Tree, Staff }

    // Menü
    private sealed class MenuCard
    {
        public IslandLayout.BuildingKind kind;
        public Image paper, icon, woodIcon, meatIcon, role;
        public TextMeshProUGUI title, info, wood, meat, state, free;
        public Button button;
    }
    private readonly List<MenuCard> cards = new List<MenuCard>();
    private TextMeshProUGUI zoneTitle, zoneInfo, zoneState;
    private Button zoneButton;

    // Kadro (AŞAMA 2 §2)
    private TextMeshProUGUI staffTotals, staffList;

    // Panel
    private Image panelIcon, panelWoodIcon, panelMeatIcon, panelRole;
    private TextMeshProUGUI panelTitle, panelBody, panelWood, panelMeat, panelTime, panelNote, workersLabel;
    private Button primary, secondary, tertiary, workerMinus, workerPlus;
    private TextMeshProUGUI primaryLabel, secondaryLabel, tertiaryLabel;

    public bool Open => mode != Mode.None;
    public bool MenuOpen => mode == Mode.Menu;
    public bool StaffOpen => mode == Mode.Staff;
    public bool TreeOpen => mode == Mode.Tree;
    public string OpenTargetId => targetId;
    /// <summary>Testler okur: kadro listesinin satırları ve toplam satırı; panel gövde metni.</summary>
    public string StaffText => staffList != null ? staffList.text : "";
    public string StaffTotalsText => staffTotals != null ? staffTotals.text : "";
    public string PanelBodyText => panelBody != null ? panelBody.text : "";
    public string PrimaryText => primaryLabel != null && primary.gameObject.activeSelf ? primaryLabel.text : null;
    public string MenuCardCostText(IslandLayout.BuildingKind kind)
    {
        var card = cards.Find(c => c.kind == kind);
        if (card == null) return null;
        return card.free.gameObject.activeSelf ? card.free.text.Replace("\n", " ") : card.wood.text + "/" + card.meat.text;
    }
    public string MenuCardInfoText(IslandLayout.BuildingKind kind) => cards.Find(c => c.kind == kind)?.info.text;

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
        BuildStaff();
        menuRoot.SetActive(false);
        panelRoot.SetActive(false);
        staffRoot.SetActive(false);
    }

    // ==================== İNŞA ET MENÜSÜ ====================

    private static readonly IslandLayout.BuildingKind[] MenuKinds =
    {
        IslandLayout.BuildingKind.House, IslandLayout.BuildingKind.Lumber, IslandLayout.BuildingKind.Pasture,
        IslandLayout.BuildingKind.Tower, IslandLayout.BuildingKind.Barracks, IslandLayout.BuildingKind.Monastery,
        IslandLayout.BuildingKind.Archery,
    };

    private void BuildMenu()
    {
        var panel = NineSlice(safe, "BuildMenu", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.80f), Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
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
            // AŞAMA 2 §1: rol simgesi (ürettiği kaynak / savunma / şifa) + tek cümle ne işe yaradığı
            card.role = ReferenceUiVisuals.Image(rect, "Role", new Vector2(0, 0.28f), new Vector2(0, 0.28f), new Vector2(92, -11), new Vector2(114, 11), Color.white);
            card.role.sprite = RoleIcon(card.kind);
            card.role.preserveAspect = true;
            card.info = ReferenceUiVisuals.Text(rect, "Info", "", 11, light, TextAlignmentOptions.Left,
                new Vector2(0, 0), new Vector2(0.64f, 0.56f), new Vector2(118, 12), new Vector2(0, 0), false, true);   // AŞAMA 3 §3G: 2. satır kartın alt kenarlığına taşmaz
            card.info.textWrappingMode = TextWrappingModes.Normal;
            card.info.overflowMode = TextOverflowModes.Truncate;
            card.info.fontSizeMin = 7f;
            card.info.fontSizeMax = 11f;
            card.info.lineSpacing = -10f;
            card.free = ReferenceUiVisuals.Text(rect, "Free", L.T("İLK YAPIM\nBEDAVA", "FIRST BUILD\nFREE"), 11, new Color(0.55f, 1f, 0.55f), TextAlignmentOptions.Center,
                new Vector2(0.64f, 0.12f), new Vector2(0.84f, 0.88f), new Vector2(0, 0), new Vector2(0, 0), true);
            card.free.fontSizeMin = 7f;
            card.free.gameObject.SetActive(false);
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

    /// <summary>Bölgenin getirdiği (§4): oyuncuya "neden açayım" cevabı.</summary>
    public static string ZoneBenefit(int zone)
    {
        switch (zone)
        {
            case 1: return L.T("Doğu kıyısında yeni ağaç, çayır ve yer; B kumsalı da buradan çıkar.", "New trees, meadow and room on the east shore; beach B lands here.");
            case 2: return L.T("Güneybatı koyunda ağaç ve geniş yer; C kumsalı burası.", "Trees and room in the southwest cove; beach C lands here.");
            case 3: return L.T($"Altın Damarı: Ana Üs'e saatte {Progression.Island.GoldVeinPerHour(IslandEconomy.Keep):0} altın (8 saat depo). Dokunup topla.",
                $"Gold Vein: {Progression.Island.GoldVeinPerHour(IslandEconomy.Keep):0} Main Base gold per hour (8 h storage). Tap to collect.");
            case 4: return L.T("Güneydoğu koyunda yeni ağaç ve yer; D kumsalı burası.", "New trees and room in the southeast cove; beach D lands here.");
            case 5: return L.T("Orta çayır: Ağıl üretimi +%15 ve güney çayırında yeni yer.", "Middle meadow: Pasture output +15% and room in the south meadow.");
            default: return "";
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
        if (kind == IslandLayout.BuildingKind.Archery && !IslandEconomy.Buildable(kind)) return L.T("Okçuluk Alanı hazır kurulu; taşınabilir", "The archery range is ready; it can be moved");
        if (kind == IslandLayout.BuildingKind.Barracks && IslandEconomy.Keep < Progression.Island.BarracksKeep)
            return L.T($"Kışla Kıyı Kalesi {Progression.Island.BarracksKeep}'te açılır", $"Barracks unlock at Keep {Progression.Island.BarracksKeep}");
        if (kind == IslandLayout.BuildingKind.Monastery && IslandEconomy.Keep < Progression.Island.MonasteryKeep)
            return L.T($"Manastır Kıyı Kalesi {Progression.Island.MonasteryKeep}'te açılır", $"Monastery unlocks at Keep {Progression.Island.MonasteryKeep}");
        int have = IslandEconomy.CountOf(kind), max = Progression.Island.MaxCount(kind, IslandEconomy.Keep);
        if (have >= max) return L.T($"Adet doldu ({have}/{max})", $"Limit reached ({have}/{max})");
        if (IslandEconomy.InDefense) return L.T("Savaşta inşa yapılamaz", "Cannot build during defense");
        if (IslandEconomy.BuilderBusy) return L.T("Ada Ustası meşgul", "Island Builder is busy");
        return null;
    }

    /// <summary>Türün açıldığı en düşük Kıyı Kalesi seviyesi (adet 0'dan büyük olur); yoksa 0.</summary>
    public static int UnlockKeep(IslandLayout.BuildingKind kind)
    {
        for (int k = 1; k <= Progression.MaxLevel; k++) if (Progression.Island.MaxCount(kind, k) > 0) return k;
        return 0;
    }

    private static string TurkishLocative(int n)
    {
        if (n % 10 == 0) return n % 20 == 0 ? "de" : "da";   // 10'da, 20'de, 30'da
        switch (n % 10) { case 1: case 2: case 7: case 8: return "de"; case 3: case 4: case 5: return "te"; default: return "da"; }
    }

    /// <summary>Kartta kilit nedeni: "Kıyı Kalesi 6'da açılır".</summary>
    public static string LockedText(IslandLayout.BuildingKind kind)
    {
        int k = UnlockKeep(kind);
        return L.T($"Kıyı Kalesi\n{k}'{TurkishLocative(k)} açılır", $"Unlocks at\nKeep {k}");
    }

    private void RefreshMenu()
    {
        var save = IslandSession.Save;
        foreach (var card in cards)
        {
            var cost = IslandEconomy.BuildCost(card.kind);
            int have = IslandEconomy.CountOf(card.kind), max = Progression.Island.MaxCount(card.kind, IslandEconomy.Keep);
            bool buildable = IslandEconomy.Buildable(card.kind);
            // AŞAMA 3 §3G: adedi 0 olan tür (ör. Manastır KK5) kilit nedenini gösterir; "İLK YAPIM BEDAVA" yalnız şimdi kurulabilirse.
            bool lockedByKeep = max <= 0;
            bool free = buildable && !lockedByKeep && IslandEconomy.IsFirstBuildFree(card.kind);
            card.free.gameObject.SetActive(free || lockedByKeep);   // AŞAMA 2 §1: bedel yerine "İLK YAPIM BEDAVA"
            if (lockedByKeep)
            {
                card.free.text = LockedText(card.kind);
                card.free.color = new Color(1f, 0.72f, 0.45f);
            }
            else
            {
                card.free.text = L.T("İLK YAPIM\nBEDAVA", "FIRST BUILD\nFREE");
                card.free.color = new Color(0.55f, 1f, 0.55f);
            }
            bool showCost = !free && !lockedByKeep;
            card.wood.gameObject.SetActive(showCost); card.meat.gameObject.SetActive(showCost);
            card.woodIcon.gameObject.SetActive(showCost); card.meatIcon.gameObject.SetActive(showCost);
            card.wood.text = !buildable ? "-" : cost.wood.ToString();
            card.meat.text = !buildable ? "-" : cost.meat.ToString();
            card.wood.color = save != null && save.wood < cost.wood ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.95f, 0.78f);
            card.meat.color = save != null && save.meat < cost.meat ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.95f, 0.78f);
            card.info.text = KindPurpose(card.kind);
            string blocked = KindBlockedReason(card.kind);
            card.state.text = $"{have}/{max}";
            card.paper.color = blocked == null ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            card.icon.color = blocked == null ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        }
        int zone = NextZone();
        if (zone <= 0)
        {
            zoneTitle.text = L.T("Yeni bölge", "New zone");
            zoneInfo.text = L.T("Tüm bölgeler keşfedildi.", "All zones explored.");
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

    /// <summary>AŞAMA 2 §1: türün ne ürettiği / ne işe yaradığı (tek cümle; menü kartı ve bina paneli).</summary>
    public static string KindPurpose(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.House:
                return L.T($"İşçi kapasitesini artırır (+{Progression.Island.HousePawns(1)} işçi).", $"Raises worker capacity (+{Progression.Island.HousePawns(1)} workers).");
            case IslandLayout.BuildingKind.Lumber: return L.T("Odun üretir: işçisi yakındaki ağaçları keser.", "Produces Wood: its worker chops nearby trees.");
            case IslandLayout.BuildingKind.Pasture: return L.T("Et üretir: işçisi koyunları güder.", "Produces Meat: its worker herds the sheep.");
            case IslandLayout.BuildingKind.Tower: return L.T("Akınlarda yaklaşan düşmanlara ok atar.", "Shoots arrows at approaching raiders.");
            case IslandLayout.BuildingKind.Archery: return L.T("Okçu Kaptan'ı yükseltir; atış yeridir.", "Trains the Archer Captain; a shooting post.");
            case IslandLayout.BuildingKind.Barracks: return L.T("Lancer'lar bayrağı korur.", "Lancers guard the flag.");
            case IslandLayout.BuildingKind.Monastery: return L.T("Binaları iyileştirir, onarımı hızlandırır.", "Heals buildings and speeds up repairs.");
            default: return L.T("Adanın kalbi: düşerse Akın kaybedilir.", "The heart of the island: if it falls, the raid is lost.");
        }
    }

    /// <summary>Rol simgesi: üretilen kaynak (Odun/Et) ya da işlev (kalkan, kılıç, kalp, çekiç).</summary>
    public static Sprite RoleIcon(IslandLayout.BuildingKind kind)
    {
        var catalog = IslandVisualCatalog.Instance;
        switch (kind)
        {
            case IslandLayout.BuildingKind.Lumber: return catalog != null ? catalog.woodIcon : null;
            case IslandLayout.BuildingKind.Pasture: return catalog != null ? catalog.meatIcon : null;
            case IslandLayout.BuildingKind.Tower: return CampaignSceneSetup.Art("ui_icon_shield");
            case IslandLayout.BuildingKind.Castle: return CampaignSceneSetup.Art("ui_icon_shield");
            case IslandLayout.BuildingKind.Archery:
            case IslandLayout.BuildingKind.Barracks: return CampaignSceneSetup.Art("ui_icon_sword");
            case IslandLayout.BuildingKind.Monastery: return CampaignSceneSetup.Art("ui_icon_heart");
            default: return CampaignSceneSetup.Art("ui_icon_hammer");
        }
    }

    // ==================== BİLGİ PANELİ ====================

    private void BuildPanel()
    {
        // 3. oturum (2.4): pencere ortak bileşenden (FancyInfoPanel); Ana Üs de aynısını kullanır. Ad ve yerleşim değişmedi.
        var v = FancyInfoPanel.Build(safe, "BuildingPanel", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.40f));
        panelRoot = v.root;
        panelTitle = v.title;
        v.close.onClick.AddListener(Close);
        panelIcon = v.icon;
        panelRole = v.role;
        panelBody = v.body;
        panelWoodIcon = v.costIconA;
        panelWoodIcon.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.woodIcon : null;
        panelWood = v.costA;
        panelMeatIcon = v.costIconB;
        panelMeatIcon.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.meatIcon : null;
        panelMeat = v.costB;
        panelTime = v.time;
        panelNote = v.note;
        workersLabel = v.workers;
        workerMinus = v.workerMinus;
        workerPlus = v.workerPlus;
        workerMinus.onClick.AddListener(() => ChangeWorkers(-1));
        workerPlus.onClick.AddListener(() => ChangeWorkers(+1));
        primary = v.primary; primaryLabel = v.primaryLabel;
        secondary = v.secondary; secondaryLabel = v.secondaryLabel;
        tertiary = v.tertiary; tertiaryLabel = v.tertiaryLabel;
        primary.onClick.AddListener(OnPrimary);
        secondary.onClick.AddListener(OnSecondary);
        tertiary.onClick.AddListener(OnTertiary);
    }

    // ==================== AÇ / KAPAT ====================

    public void ShowMenu()
    {
        mode = Mode.Menu; targetId = null;
        menuRoot.SetActive(true); panelRoot.SetActive(false); staffRoot.SetActive(false);
        RefreshMenu();
    }

    public void Close()
    {
        mode = Mode.None; targetId = null;
        if (menuRoot != null) menuRoot.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(false);
        if (staffRoot != null) staffRoot.SetActive(false);
    }

    private void ShowPanel()
    {
        menuRoot.SetActive(false); staffRoot.SetActive(false); panelRoot.SetActive(true);
        RefreshPanel();
    }

    public void ShowBuilding(string id) { mode = Mode.Building; targetId = id; ShowPanel(); }

    public void ShowCastle() { mode = Mode.Castle; targetId = null; ShowPanel(); }

    public void ShowZone(int zone) { mode = Mode.Zone; targetZone = zone; targetId = null; ShowPanel(); }

    /// <summary>AŞAMA 2 §3: ağaç paneli (kes onayı ya da "üretim ağacı" bilgisi).</summary>
    public void ShowTree(Vector2Int cell) { mode = Mode.Tree; targetTree = cell; targetId = null; ShowPanel(); }

    /// <summary>AŞAMA 2 §2: KADRO paneli (her işçi bir satır + çalışan/kapasite).</summary>
    public void ShowStaff()
    {
        mode = Mode.Staff; targetId = null;
        menuRoot.SetActive(false); panelRoot.SetActive(false); staffRoot.SetActive(true);
        RefreshStaff();
    }

    public void Tick()
    {
        if (!Open || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        if (mode == Mode.Menu) RefreshMenu(); else if (mode == Mode.Staff) RefreshStaff(); else RefreshPanel();
    }

    // ==================== KADRO (AŞAMA 2 §2) ====================

    private void BuildStaff()
    {
        var panel = NineSlice(safe, "StaffPanel", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.40f), Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
        panel.raycastTarget = true;
        staffRoot = panel.gameObject;
        var ribbon = NineSlice(panel.transform, "Ribbon", new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0, -34), new Vector2(0, 20), "ui_ribbon", Color.white, 3f);
        ReferenceUiVisuals.Text(ribbon.transform, "Title", L.T("KADRO", "STAFF"), 20, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(40, 8), new Vector2(-40, -4), true);
        var close = ReferenceUiVisuals.Button(panel.transform, "Close", "X", false, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-42, -40), new Vector2(-12, -12), 14);
        close.onClick.AddListener(Close);
        staffTotals = ReferenceUiVisuals.Text(panel.transform, "Totals", "", 17, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -80), new Vector2(-18, -46), true);
        staffList = ReferenceUiVisuals.Text(panel.transform, "List", "", 15, Ink, TextAlignmentOptions.TopLeft,
            Vector2.zero, Vector2.one, new Vector2(22, 16), new Vector2(-18, -84), false, true);
        staffList.textWrappingMode = TextWrappingModes.Normal;
        staffList.fontSizeMin = 9f;
    }

    /// <summary>Kadro satırı metni (testler de kullanır): "İşçi 1 · Oduncu Kampı · kendi işçisi" / "İşçi 3 · Ev'den · Ağıl'da çalışıyor" / "İşçi 5 · Boşta (Ev)".</summary>
    public static string StaffRowText(IslandEconomy.StaffRow row)
    {
        var home = IslandEconomy.Find(row.homeId);
        string homeName = home != null ? IslandVillage.KindName(IslandEconomy.KindOf(home)) : "?";
        if (row.own) return L.T($"İşçi {row.number} · {homeName} · kendi işçisi", $"Worker {row.number} · {homeName} · own worker");
        if (row.workId == null) return L.T($"İşçi {row.number} · Boşta ({homeName})", $"Worker {row.number} · Idle ({homeName})");
        var work = IslandEconomy.Find(row.workId);
        var workKind = work != null ? IslandEconomy.KindOf(work) : IslandLayout.BuildingKind.Lumber;
        string at = workKind == IslandLayout.BuildingKind.Pasture ? "Ağıl'da" : "Oduncu Kampı'nda";
        return L.T($"İşçi {row.number} · {homeName}'den · {at} çalışıyor", $"Worker {row.number} · from {homeName} · works at {IslandVillage.KindName(workKind)}");
    }

    private void RefreshStaff()
    {
        var save = IslandSession.Save;
        if (save == null) { Close(); return; }
        var rows = IslandEconomy.StaffRoster(save.placed);
        int working = 0;
        var text = new System.Text.StringBuilder();
        foreach (var row in rows)
        {
            if (row.workId != null) working++;
            text.AppendLine(StaffRowText(row));
        }
        if (rows.Count == 0)
            text.AppendLine(L.T("Henüz işçi yok. Oduncu Kampı ya da Ağıl kendi işçisiyle gelir; Ev ek işçi verir.",
                "No workers yet. A Lumber Camp or Pasture comes with its own worker; a House adds more."));
        staffTotals.text = L.T($"Çalışan {working} / Kapasite {rows.Count}", $"Working {working} / Capacity {rows.Count}");
        staffList.text = text.ToString();
    }

    // ==================== PANEL İÇERİĞİ ====================

    private enum PrimaryAction { None, Upgrade, Explore, Akin, ClearTree }
    private PrimaryAction primaryAction;
    private enum TertiaryAction { None, Collect, FinishNow, Flag }
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
        panelRole.gameObject.SetActive(false);
        if (mode == Mode.Zone) { RefreshZonePanel(); return; }
        if (mode == Mode.Castle) { RefreshCastlePanel(); return; }
        if (mode == Mode.Tree) { RefreshTreePanel(); return; }
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

        panelRole.sprite = RoleIcon(kind);
        panelRole.gameObject.SetActive(panelRole.sprite != null);
        body.AppendLine("<i>" + KindPurpose(kind) + "</i>");   // AŞAMA 2 §1: ne işe yaradığı
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

        if (!taskHere && !building && kind == IslandLayout.BuildingKind.Barracks && !IslandEconomy.InDefense)
        {
            int next = ((save != null ? save.flagStair : 0) + 1) % IslandLayout.FlagPoints.Length;
            tertiaryAction = TertiaryAction.Flag;
            tertiaryText = L.T($"BAYRAK → {IslandLayout.FlagShortName(next)}", $"FLAG → {IslandLayout.FlagShortName(next)}");
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
            case IslandLayout.BuildingKind.Barracks:
            {
                int stair = save != null ? save.flagStair : 0;
                body.AppendLine(L.T($"{Progression.Island.LancerCount(level)} Lancer · can {Progression.Island.LancerHp(level):0} · hasar {Progression.Island.LancerDamage(level):0.#}/sn",
                    $"{Progression.Island.LancerCount(level)} Lancer(s) · HP {Progression.Island.LancerHp(level):0} · damage {Progression.Island.LancerDamage(level):0.#}/s"));
                body.AppendLine(L.T($"Bayrak: {IslandLayout.FlagName(stair)}", $"Flag: {IslandLayout.FlagName(stair)}"));
                body.AppendLine(L.T($"Bina canı {Progression.Island.StructureHp("Barracks", level):0} · Lancer {Progression.Island.LancerRespawn:0} sn'de döner",
                    $"Building HP {Progression.Island.StructureHp("Barracks", level):0} · Lancer back in {Progression.Island.LancerRespawn:0} s"));
                break;
            }
            case IslandLayout.BuildingKind.Monastery:
                body.AppendLine(L.T($"Her {Progression.Island.MonkHealInterval:0} sn'de {Progression.Island.MonkHealRadius:0} u içine %{Progression.Island.MonkHealFraction * 100f:0} can",
                    $"Heals {Progression.Island.MonkHealFraction * 100f:0}% every {Progression.Island.MonkHealInterval:0} s within {Progression.Island.MonkHealRadius:0} u"));
                body.AppendLine(L.T("Binaların onarım süresi ×0,5", "Building repair time x0.5"));
                body.AppendLine(L.T($"Savaş canı {Progression.Island.StructureHp("Monastery", level):0}", $"Battle HP {Progression.Island.StructureHp("Monastery", level):0}"));
                break;
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
        workerMinus.interactable = crew > IslandEconomy.OwnWorkers(entry);   // v2: kendi işçisi hep çalışır
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

    /// <summary>AŞAMA 2 §3: ağaç paneli. Üretim ağacı kesilemez; diğerleri altınla kesilir (Odun verir, yer açar, bir daha büyümez).</summary>
    private void RefreshTreePanel()
    {
        var save = IslandSession.Save;
        var tree = IslandTree.At(targetTree);
        if (save == null || tree == null || tree.Cleared) { Close(); return; }
        panelIcon.sprite = tree.Renderer != null ? tree.Renderer.sprite : null;
        panelRole.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.woodIcon : null;
        panelRole.gameObject.SetActive(panelRole.sprite != null);
        panelTitle.text = L.T("Ağaç", "Tree");
        workerMinus.gameObject.SetActive(false); workerPlus.gameObject.SetActive(false); workersLabel.text = "";
        SetCost(0, 0, null, false);
        primaryAction = PrimaryAction.None; tertiaryAction = TertiaryAction.None; secondaryIsMove = false;
        var cfg = IslandEconomy.Config;
        if (IslandEconomy.IsProductionTree(save, targetTree.x, targetTree.y))
        {
            panelBody.text = L.T("Oduncu Kampı'nın üretim ağacı.\nKesilemez: Odun üretimini besler.", "A production tree of the Lumber Camp.\nCannot be cleared: it feeds Wood production.");
            panelNote.text = "";
            SetButtons(null, false, null, false, null, false);
            return;
        }
        panelBody.text = L.T($"Ağacı kes: {cfg.clearTreeGold} altın → +{cfg.clearTreeWood} odun\nYer açılır; kesilen ağaç bir daha büyümez.",
            $"Clear the tree: {cfg.clearTreeGold} gold → +{cfg.clearTreeWood} wood\nFrees the spot; a cleared tree never grows back.");
        string blocked = IslandEconomy.ClearTreeBlockedReason(targetTree.x, targetTree.y);
        panelNote.text = blocked ?? "";
        primaryAction = PrimaryAction.ClearTree;
        SetButtons(L.T($"KES · {cfg.clearTreeGold} altın", $"CLEAR · {cfg.clearTreeGold} gold"), blocked == null, null, false, null, false);
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
        panelBody.text = open ? L.T("Bölge açık. ", "Zone is open. ") + ZoneBenefit(zone)
            : L.T("Bulutların altında: ", "Hidden under clouds: ") + ZoneBenefit(zone);
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
            case PrimaryAction.ClearTree:
            {
                var village = IslandVillage.Instance;
                if (village == null) return;
                if (!village.ClearTree(targetTree, out reason)) { Toast?.Invoke(reason); RefreshPanel(); return; }
                Close();
                break;
            }
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
            case TertiaryAction.Flag:
            {
                var village = IslandVillage.Instance;
                if (village == null) return;
                int stair = village.CycleFlag();
                Toast?.Invoke(L.T($"Bayrak: {IslandLayout.FlagName(stair)}", $"Flag: {IslandLayout.FlagName(stair)}"));
                RefreshPanel();
                break;
            }
        }
    }
}
