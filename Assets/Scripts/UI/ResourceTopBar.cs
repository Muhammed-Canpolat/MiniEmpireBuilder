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
        float w = (chipsTo - chipsFrom) / 4f;
        Vector2 Lo(int i) => new Vector2(chipsFrom + i * w + 0.002f, 0.14f);
        Vector2 Hi(int i) => new Vector2(chipsFrom + (i + 1) * w - 0.002f, 0.86f);
        component.Gold = MakeChip(bar, "GoldPill", "gold", CampaignSceneSetup.Art("ui_icon_coin"), Lo(0), Hi(0));
        component.Gem = MakeChip(bar, "GemPill", "gem", GemIcon.Sprite, Lo(1), Hi(1));
        component.Gem.button.onClick.AddListener(GemShopUI.Open);
        component.Gem.button.gameObject.AddComponent<GemCounter>().Bind(component.Gem.text);
        component.Meat = MakeChip(bar, "MeatPill", "meat", catalog != null ? catalog.meatIcon : null, Lo(2), Hi(2));
        component.Wood = MakeChip(bar, "WoodPill", "wood", catalog != null ? catalog.woodIcon : null, Lo(3), Hi(3));
        component.BuildToast(bar);
        component.Refresh(true);
        return component;
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
        if (!chip.Locked) return;
        ShowToast(L.T("Ada'dan elde edilir — Ana Üs Sv.5'te açılır", "Comes from the Island — unlocks at Main Base Lv.5"));
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
