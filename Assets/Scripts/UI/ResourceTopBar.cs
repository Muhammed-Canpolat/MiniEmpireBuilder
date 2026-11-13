using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ortak üst bar çipleri (AŞAMA 3 §3A): Altın · Elmas · Et · Odun. Ana Üs ve Ada aynı bileşeni kullanır; çerçeve
/// <see cref="BaseHudRedesign.OrnateStrip"/>, hapler <see cref="ReferenceUiVisuals.PillSprite"/>. Sayılar <see cref="NumberFormat.Short"/>.
/// Ana Üs Sv.5'ten önce Et/Odun "0" soluk + kilit simgesi; dokununca bilgi satırı çıkar.
/// Ana Üs'te Altın/Elmas hapları mevcut (GoldPill/GemPill) kalır, bu bileşen yalnız Et/Odun çiplerini ekler.
/// </summary>
public sealed class ResourceTopBar : MonoBehaviour
{
    public enum Mode { Base, Island }

    public sealed class Chip
    {
        public string id;
        public RectTransform rect;
        public Image bg, icon, lockIcon;
        public TextMeshProUGUI text;
        public Button button;
        public bool Locked => lockIcon != null && lockIcon.gameObject.activeSelf;
    }

    public Mode mode;
    public Chip Gold, Gem, Meat, Wood;
    private RectTransform toastRect;
    private TextMeshProUGUI toastText;
    private float toastUntil, nextRefresh;

    /// <summary>Ana Üs: HudRoot'a Et ve Odun çipleri (mevcut Altın/Elmas hapları yerinde).</summary>
    public static ResourceTopBar AttachBase(Transform hud)
    {
        if (hud == null || !FeatureConfig.Instance.sharedTopBar) return null;
        var existing = hud.GetComponentInChildren<ResourceTopBar>(true);
        if (existing != null) return existing;
        var root = ReferenceUiVisuals.Rect(hud, "ResourceTopBar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bar = root.gameObject.AddComponent<ResourceTopBar>();
        bar.mode = Mode.Base;
        var catalog = IslandVisualCatalog.Instance;
        bar.Meat = MakeChip(root, "MeatPill", "meat", catalog != null ? catalog.meatIcon : null,
            new Vector2(0.585f, 0.934f), new Vector2(0.72f, 0.980f));
        bar.Wood = MakeChip(root, "WoodPill", "wood", catalog != null ? catalog.woodIcon : null,
            new Vector2(0.725f, 0.934f), new Vector2(0.86f, 0.980f));
        bar.BuildToast(root);
        bar.Refresh(true);
        return bar;
    }

    /// <summary>Ada: üst çubuğa (bar) dört çip; konum bar anchor oranlarıyla (ANA ÜS · ad · çipler · dişli yanında).</summary>
    public static ResourceTopBar BuildIsland(RectTransform bar, float chipsFrom, float chipsTo)
    {
        var component = bar.gameObject.AddComponent<ResourceTopBar>();
        component.mode = Mode.Island;
        var catalog = IslandVisualCatalog.Instance;
        component.Gold = MakeChip(bar, "GoldPill", "gold", CampaignSceneSetup.Art("ui_icon_coin"), Vector2.zero, Vector2.zero);
        component.Gem = MakeChip(bar, "GemPill", "gem", GemIcon.Sprite, Vector2.zero, Vector2.zero);
        component.Gem.button.onClick.AddListener(GemShopUI.Open);
        component.Gem.button.gameObject.AddComponent<GemCounter>().Bind(component.Gem.text);
        component.Meat = MakeChip(bar, "MeatPill", "meat", catalog != null ? catalog.meatIcon : null, Vector2.zero, Vector2.zero);
        component.Wood = MakeChip(bar, "WoodPill", "wood", catalog != null ? catalog.woodIcon : null, Vector2.zero, Vector2.zero);
        // 3. oturum (2.2): sıra FeatureConfig.islandTopBarOrder (varsayılan Altın · Odun · Et · Elmas)
        var rects = new System.Collections.Generic.Dictionary<string, RectTransform>
        {
            { "gold", component.Gold.rect }, { "gem", component.Gem.rect }, { "meat", component.Meat.rect }, { "wood", component.Wood.rect },
        };
        var order = Order(FeatureConfig.Instance.islandTopBarOrder, DefaultIslandOrder);
        Place(rects, order, chipsFrom, chipsTo, 0.14f, 0.86f);
        component.BuildToast(bar);
        component.Refresh(true);
        return component;
    }

    // ==================== 3. oturum (2.2): config'ten sıra ====================

    public static readonly string[] DefaultBaseOrder = { "gold", "wood", "meat", "gem", "builder" };
    public static readonly string[] DefaultIslandOrder = { "gold", "wood", "meat", "gem" };

    /// <summary>Geçerli (bilinen, tekrarsız) kimlik sırası; config boşsa varsayılan.</summary>
    public static System.Collections.Generic.List<string> Order(string[] configured, string[] fallback)
    {
        var known = new System.Collections.Generic.HashSet<string> { "gold", "wood", "meat", "gem", "builder" };
        var result = new System.Collections.Generic.List<string>();
        foreach (var id in configured != null && configured.Length > 0 ? configured : fallback)
            if (!string.IsNullOrEmpty(id) && known.Contains(id) && !result.Contains(id)) result.Add(id);
        return result.Count > 0 ? result : new System.Collections.Generic.List<string>(fallback);
    }

    /// <summary>Öğeleri [from, to] yatay aralığına eşit genişlikte sırayla yerleştirir; sırada olmayanlar gizlenir.</summary>
    private static void Place(System.Collections.Generic.Dictionary<string, RectTransform> rects, System.Collections.Generic.List<string> order,
        float from, float to, float yMin, float yMax)
    {
        foreach (var pair in rects) if (pair.Value != null) pair.Value.gameObject.SetActive(order.Contains(pair.Key));
        var shown = order.FindAll(id => rects.ContainsKey(id) && rects[id] != null);
        if (shown.Count == 0) return;
        float w = (to - from) / shown.Count;
        for (int i = 0; i < shown.Count; i++)
        {
            var rt = rects[shown[i]];
            rt.anchorMin = new Vector2(from + i * w + 0.002f, yMin);
            rt.anchorMax = new Vector2(from + (i + 1) * w - 0.002f, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Ada üst barında görünen sıra (test/rapor).</summary>
    public System.Collections.Generic.List<string> VisibleOrder()
    {
        var chips = new System.Collections.Generic.List<Chip> { Gold, Gem, Meat, Wood };
        chips.RemoveAll(c => c == null || !c.rect.gameObject.activeSelf);
        chips.Sort((a, b) => a.rect.anchorMin.x.CompareTo(b.rect.anchorMin.x));
        return chips.ConvertAll(c => c.id);
    }

    /// <summary>
    /// Ana Üs üst barı: Altın/Elmas/İnşaatçı hapları (BaseHudRedesign) ve Et/Odun çipleri FeatureConfig.baseTopBarOrder sırasıyla;
    /// ilk yarı (yukarı yuvarlanır) armanın solunda [0,02–0,415], kalanı sağında [0,585–0,86] (en sağda ayar dişlisi).
    /// </summary>
    public static void LayoutBase(Transform hud)
    {
        if (hud == null) return;
        var bar = hud.GetComponentInChildren<ResourceTopBar>(true);
        var rects = new System.Collections.Generic.Dictionary<string, RectTransform>
        {
            { "gold", hud.Find("GoldPill") as RectTransform }, { "gem", hud.Find("GemPill") as RectTransform },
            { "builder", hud.Find("BuilderPill") as RectTransform },
            { "meat", bar != null && bar.Meat != null ? bar.Meat.rect : null }, { "wood", bar != null && bar.Wood != null ? bar.Wood.rect : null },
        };
        var order = Order(FeatureConfig.Instance.baseTopBarOrder, DefaultBaseOrder);
        int left = (order.Count + 1) / 2;
        Place(rects, order.GetRange(0, left), 0.02f, 0.415f, 0.934f, 0.980f);
        var right = order.GetRange(left, order.Count - left);
        var rightRects = new System.Collections.Generic.Dictionary<string, RectTransform>();
        foreach (var id in right) if (rects.TryGetValue(id, out var r)) rightRects[id] = r;
        Place(rightRects, right, 0.585f, 0.86f, 0.934f, 0.980f);
        foreach (var id in right) if (rects[id] != null) rects[id].gameObject.SetActive(true);
    }

    /// <summary>Ana Üs üst barında soldan sağa görünen sıra (test/rapor).</summary>
    public static System.Collections.Generic.List<string> BaseVisibleOrder(Transform hud)
    {
        var bar = hud.GetComponentInChildren<ResourceTopBar>(true);
        var items = new System.Collections.Generic.List<(string id, RectTransform rt)>
        {
            ("gold", hud.Find("GoldPill") as RectTransform), ("gem", hud.Find("GemPill") as RectTransform), ("builder", hud.Find("BuilderPill") as RectTransform),
            ("meat", bar != null && bar.Meat != null ? bar.Meat.rect : null), ("wood", bar != null && bar.Wood != null ? bar.Wood.rect : null),
        };
        items.RemoveAll(x => x.rt == null || !x.rt.gameObject.activeSelf);
        items.Sort((a, b) => a.rt.anchorMin.x.CompareTo(b.rt.anchorMin.x));
        return items.ConvertAll(x => x.id);
    }

    private static Chip MakeChip(Transform parent, string name, string id, Sprite iconSprite, Vector2 min, Vector2 max)
    {
        var chip = new Chip { id = id };
        chip.bg = ReferenceUiVisuals.Image(parent, name, min, max, Vector2.zero, Vector2.zero, Color.white);
        chip.rect = chip.bg.rectTransform;
        chip.bg.sprite = ReferenceUiVisuals.PillSprite();
        chip.bg.type = Image.Type.Sliced;
        chip.bg.raycastTarget = true;
        chip.button = chip.bg.gameObject.AddComponent<Button>();
        chip.button.targetGraphic = chip.bg;
        chip.button.transition = Selectable.Transition.None;
        chip.icon = ReferenceUiVisuals.Image(chip.rect, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(5, -8), new Vector2(21, 8), Color.white);
        chip.icon.sprite = iconSprite;
        chip.icon.preserveAspect = true;
        chip.icon.enabled = iconSprite != null;
        chip.lockIcon = ReferenceUiVisuals.Image(chip.rect, "Lock", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(1, -11), new Vector2(11, -1), Color.white);
        chip.lockIcon.sprite = CampaignSceneSetup.Art("ui_icon_lock");
        chip.lockIcon.preserveAspect = true;
        chip.lockIcon.gameObject.SetActive(false);
        chip.text = ReferenceUiVisuals.Text(chip.rect, "Value", "0", 12, ReferenceUiVisuals.Light, TextAlignmentOptions.Left,
            Vector2.zero, Vector2.one, new Vector2(23, 0), new Vector2(-4, 0), false, true);
        chip.text.textWrappingMode = TextWrappingModes.NoWrap;
        chip.text.enableAutoSizing = true;
        chip.text.fontSizeMin = 7f;
        chip.text.fontSizeMax = 12f;
        return chip;
    }

    private void BuildToast(RectTransform parent)
    {
        bool island = mode == Mode.Island;
        // Ada: çubuğun hemen altı; Ana Üs: üst çubuğun altında (arma altına inmez, tam genişlik).
        toastRect = island
            ? ReferenceUiVisuals.Rect(parent, "ResourceToast", new Vector2(0.04f, 0f), new Vector2(0.96f, 0f), new Vector2(0, -34), new Vector2(0, -8))
            : ReferenceUiVisuals.Rect(parent, "ResourceToast", new Vector2(0.05f, 0.885f), new Vector2(0.95f, 0.92f), Vector2.zero, Vector2.zero);
        var bg = toastRect.gameObject.AddComponent<Image>();
        bg.sprite = ReferenceUiVisuals.PillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.08f, 0.06f, 0.04f, 0.94f);
        bg.raycastTarget = false;
        toastText = ReferenceUiVisuals.Text(toastRect, "Text", "", 12, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0), false, true);
        toastText.textWrappingMode = TextWrappingModes.NoWrap;
        toastText.fontSizeMin = 8f;
        toastRect.gameObject.SetActive(false);
        foreach (var chip in new[] { Meat, Wood })
        {
            if (chip == null) continue;
            var captured = chip;
            chip.button.onClick.AddListener(() => OnChipTapped(captured));
        }
    }

    private void OnChipTapped(Chip chip)
    {
        if (!chip.Locked)
        {
            // AŞAMA 4: adada Et/Odun çipi Ada Tüccarı'nı açar (V2 profili; LEGACY'de kapalı).
            if (mode == Mode.Island && IslandEconomy.TraderEnabled) ShowTrader(this);
            return;
        }
        ShowToast(L.T("Ada'dan elde edilir — Ana Üs Sv.5'te açılır", "Comes from the Island — unlocks at Main Base Lv.5"));
    }

    /// <summary>Ada Tüccarı penceresi: Ana Üs altınıyla 50 Et ya da 50 Odun (günde 2 paket).</summary>
    public static void ShowTrader(ResourceTopBar bar = null)
    {
        int amount = IslandEconomy.TraderPackAmount, cost = IslandEconomy.TraderPackCost, left = IslandEconomy.TraderPacksLeft;
        int perDay = Economy.Active.traderPacksPerDay;
        string body = L.T(
            $"Ada'ya uğrayan gezgin tüccar Ana Üs altını karşılığında erzak getirir.\n\n<b>Paket:</b> {amount} Et ya da {amount} Odun\n<b>Bedel:</b> {cost} altın\n<b>Bugün kalan:</b> {left}/{perDay}\n\nAltının: {NumberFormat.Short(GameManager.Instance != null ? GameManager.Instance.Gold : 0, 10000)}",
            $"A travelling trader brings supplies for Main Base gold.\n\n<b>Pack:</b> {amount} Meat or {amount} Wood\n<b>Cost:</b> {cost} gold\n<b>Left today:</b> {left}/{perDay}\n\nYour gold: {NumberFormat.Short(GameManager.Instance != null ? GameManager.Instance.Gold : 0, 10000)}");
        ModeLaunchPanel.Show(L.T("TÜCCAR", "TRADER"), body, L.T("KAPAT", "CLOSE"), true, null, "ui_ribbon_purple", 60f,
            container =>
            {
                bool can = left > 0 && GameManager.Instance != null && GameManager.Instance.Gold >= cost;
                void Buy(bool meat)
                {
                    if (IslandEconomy.TryBuyTraderPack(meat, out string reason))
                    {
                        AudioManager.Instance?.PlaySfx("levelUp");
                        ModeLaunchPanel.Close();
                        if (bar != null) { bar.Refresh(true); bar.ShowToast(meat ? L.T($"+{amount} Et", $"+{amount} Meat") : L.T($"+{amount} Odun", $"+{amount} Wood")); }
                    }
                    else ModeLaunchPanel.Hint(reason);
                }
                var meatButton = ReferenceUiVisuals.Button(container, "TraderMeat", L.T($"{amount} ET", $"{amount} MEAT"), can,
                    new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(22, 6), new Vector2(-5, -6), 16);
                meatButton.onClick.AddListener(() => Buy(true));
                var woodButton = ReferenceUiVisuals.Button(container, "TraderWood", L.T($"{amount} ODUN", $"{amount} WOOD"), can,
                    new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(5, 6), new Vector2(-22, -6), 16);
                woodButton.onClick.AddListener(() => Buy(false));
            });
    }

    public void ShowToast(string message)
    {
        if (toastRect == null) return;
        toastText.text = message;
        toastRect.SetAsLastSibling();
        toastRect.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + 3f;
    }

    public bool ToastVisible => toastRect != null && toastRect.gameObject.activeSelf;

    private void Update()
    {
        if (toastRect != null && toastRect.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastRect.gameObject.SetActive(false);
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.2f; Refresh(false); }
    }

    private static readonly Color Dim = new Color(1f, 1f, 1f, 0.75f);

    public void Refresh(bool force)
    {
        var gm = GameManager.Instance;
        long shortFrom = FeatureConfig.Instance.topBarShortFrom;
        if (mode == Mode.Island && Gold != null) Gold.text.text = NumberFormat.Short(gm != null ? gm.Gold : 0, shortFrom);
        var save = IslandSession.Save;
        bool locked = mode == Mode.Base && !IslandSession.Unlocked();   // adada kilit yok (oraya girilmiş demektir)
        SetResource(Meat, locked, save != null ? save.meat : 0, save != null && !locked && save.meat >= IslandEconomy.MeatCap, shortFrom);
        SetResource(Wood, locked, save != null ? save.wood : 0, save != null && !locked && save.wood >= IslandEconomy.WoodCap, shortFrom);
    }

    private static void SetResource(Chip chip, bool locked, int value, bool capped, long shortFrom)
    {
        if (chip == null) return;
        chip.text.text = locked ? "0" : NumberFormat.Short(value, shortFrom);
        chip.text.color = locked ? ReferenceUiVisuals.Muted : capped ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Light;
        chip.bg.color = locked ? Dim : Color.white;
        chip.icon.color = locked ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : Color.white;
        if (chip.lockIcon.gameObject.activeSelf != locked) chip.lockIcon.gameObject.SetActive(locked);
    }
}
