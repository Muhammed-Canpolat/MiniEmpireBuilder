using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3. oturum (2.4): Ada ve Ana Üs'ün ortak süslü bina/yükseltme penceresi (Tiny Swords kâğıt + kurdele başlık, simge, açıklama,
/// mevcut → sonraki değerler, ikonlu maliyet satırı, süre, not, üç düğme). Eskiden yalnız adadaydı (IslandBuildUi.BuildPanel);
/// Ana Üs düz koyu kutu kullanıyordu. Nesne adları adadaki eski adlarla aynıdır (testler ve kayıtlı alışkanlıklar bozulmaz).
/// </summary>
public static class FancyInfoPanel
{
    public sealed class View
    {
        public GameObject root;
        public Image panel, ribbon, icon, role, costIconA, costIconB, timeIcon;
        public TextMeshProUGUI title, level, body, costA, costB, time, note, workers;
        public TextMeshProUGUI primaryLabel, secondaryLabel, tertiaryLabel;
        public Button close, primary, secondary, tertiary, workerMinus, workerPlus;
    }

    public static readonly Color Ink = new Color(0.30f, 0.17f, 0.08f);
    public static readonly Color Missing = new Color(0.75f, 0.15f, 0.1f);

    public static Sprite Ui(string name)
    {
        var catalog = IslandVisualCatalog.Instance;
        return catalog != null ? catalog.Single(name) : null;
    }

    public static Image NineSlice(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high, string sprite, Color tint, float ppu)
    {
        var image = ReferenceUiVisuals.Image(parent, name, min, max, low, high, tint);
        image.sprite = Ui(sprite);
        if (image.sprite != null) { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = ppu; }
        return image;
    }

    public static Button TsButton(Transform parent, string name, string label, bool red, Vector2 min, Vector2 max, Vector2 low, Vector2 high,
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

    /// <summary>Pencereyi kurar (varsayılan konum: ekranın alt %3–40'ı, adadaki gibi).</summary>
    public static View Build(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var v = new View();
        v.panel = NineSlice(parent, name, min, max, Vector2.zero, Vector2.zero, "ui_paper", Color.white, 2.4f);
        v.panel.raycastTarget = true;
        v.root = v.panel.gameObject;
        var t = v.panel.transform;
        v.ribbon = NineSlice(t, "Ribbon", new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0, -34), new Vector2(0, 20), "ui_ribbon", Color.white, 3f);
        v.title = ReferenceUiVisuals.Text(v.ribbon.transform, "Title", "", 20, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(40, 8), new Vector2(-40, -4), true);
        v.title.enableAutoSizing = true; v.title.fontSizeMin = 12f; v.title.fontSizeMax = 20f;
        // 4. oturum (3.4): dokunma alanı 30×28 → 44×44 (≥48 dp'ye yakın; kâğıt kenarından taşmaz)
        v.close = ReferenceUiVisuals.Button(t, "Close", "X", false, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-52, -50), new Vector2(-8, -6), 16);

        v.icon = ReferenceUiVisuals.Image(t, "Icon", new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -150), new Vector2(128, -42), Color.white);
        v.icon.preserveAspect = true;
        v.role = ReferenceUiVisuals.Image(t, "Role", new Vector2(0, 1), new Vector2(0, 1), new Vector2(98, -152), new Vector2(132, -118), Color.white);
        v.role.preserveAspect = true;
        v.role.gameObject.SetActive(false);
        // Seviye satırı (Ana Üs: "Seviye 3 / 15"); adada başlıkta yazdığı için boş kalır
        v.level = ReferenceUiVisuals.Text(t, "Level", "", 15, new Color(0.55f, 0.32f, 0.06f), TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(138, -66), new Vector2(-48, -44), true);
        v.body = ReferenceUiVisuals.Text(t, "Body", "", 19, Ink, TextAlignmentOptions.TopLeft,
            new Vector2(0, 0.38f), new Vector2(1, 1), new Vector2(138, 0), new Vector2(-18, -48), false, true);
        v.body.textWrappingMode = TextWrappingModes.Normal;
        v.body.enableAutoSizing = true;
        v.body.fontSizeMin = 12f;

        // Maliyet satırı: A (adada Odun, Ana Üs'te Altın) / B (adada Et) / süre
        v.costIconA = ReferenceUiVisuals.Image(t, "WoodIcon", new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(18, -6), new Vector2(50, 26), Color.white);
        v.costIconA.preserveAspect = true;
        v.costA = ReferenceUiVisuals.Text(t, "Wood", "", 16, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(52, -4), new Vector2(120, 24), false, true);
        v.costIconB = ReferenceUiVisuals.Image(t, "MeatIcon", new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(124, -6), new Vector2(156, 26), Color.white);
        v.costIconB.preserveAspect = true;
        v.costB = ReferenceUiVisuals.Text(t, "Meat", "", 16, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.38f), new Vector2(0, 0.38f), new Vector2(158, -4), new Vector2(226, 24), false, true);
        v.time = ReferenceUiVisuals.Text(t, "Time", "", 15, Ink, TextAlignmentOptions.Right,
            new Vector2(0.5f, 0.38f), new Vector2(1, 0.38f), new Vector2(0, -4), new Vector2(-18, 24), false, true);
        v.timeIcon = ReferenceUiVisuals.Image(t, "TimeIcon", new Vector2(1, 0.38f), new Vector2(1, 0.38f), new Vector2(-120, -4), new Vector2(-100, 18), Color.white);
        v.timeIcon.sprite = CampaignSceneSetup.Art("ui_icon_clock");
        v.timeIcon.preserveAspect = true;
        v.timeIcon.raycastTarget = false;
        v.timeIcon.gameObject.SetActive(false);   // Ana Üs açar (adada süre yalnız metin)
        v.note = ReferenceUiVisuals.Text(t, "Note", "", 12, new Color(0.55f, 0.2f, 0.1f), TextAlignmentOptions.Center,
            new Vector2(0, 0.24f), new Vector2(1, 0.38f), new Vector2(18, 0), new Vector2(-18, -2), false, true);

        // İşçi satırı (adada üretici binalar)
        v.workers = ReferenceUiVisuals.Text(t, "Workers", "", 14, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.24f), new Vector2(0.5f, 0.38f), new Vector2(18, 0), new Vector2(0, -2), false, true);
        v.workerMinus = ReferenceUiVisuals.Button(t, "WorkerMinus", "-", false, new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), new Vector2(0, 0), new Vector2(40, 34), 16);
        v.workerPlus = ReferenceUiVisuals.Button(t, "WorkerPlus", "+", false, new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), new Vector2(46, 0), new Vector2(86, 34), 16);
        v.workerMinus.gameObject.SetActive(false); v.workerPlus.gameObject.SetActive(false);

        v.primary = TsButton(t, "Primary", "", false, new Vector2(0.04f, 0), new Vector2(0.52f, 0), new Vector2(0, 12), new Vector2(0, 62), 16, out v.primaryLabel);
        v.secondary = TsButton(t, "Secondary", "", false, new Vector2(0.54f, 0), new Vector2(0.96f, 0), new Vector2(0, 12), new Vector2(0, 62), 16, out v.secondaryLabel);
        v.tertiary = TsButton(t, "Tertiary", "", true, new Vector2(0.04f, 0), new Vector2(0.96f, 0), new Vector2(0, 68), new Vector2(0, 112), 15, out v.tertiaryLabel);
        return v;
    }

    /// <summary>Maliyet satırı: A ve B kaynak (simge + miktar; yetmiyorsa kırmızı), süre. Miktar 0 ise o kaynak gizlenir.</summary>
    public static void SetCost(View v, Sprite iconA, int amountA, bool enoughA, Sprite iconB, int amountB, bool enoughB, string time, bool show)
    {
        if (iconA != null) v.costIconA.sprite = iconA;
        if (iconB != null) v.costIconB.sprite = iconB;
        v.costIconA.gameObject.SetActive(show && amountA > 0); v.costA.gameObject.SetActive(show && amountA > 0);
        v.costIconB.gameObject.SetActive(show && amountB > 0); v.costB.gameObject.SetActive(show && amountB > 0);
        v.time.gameObject.SetActive(show);
        v.costA.text = NumberFormat.Short(amountA, 100000);
        v.costB.text = NumberFormat.Short(amountB, 100000);
        v.costA.color = enoughA ? Ink : Missing;
        v.costB.color = enoughB ? Ink : Missing;
        v.time.text = time ?? "";
    }

    /// <summary>Düğmeler: metin null ise gizli.</summary>
    public static void SetButtons(View v, string primaryText, bool primaryOn, string secondaryText, bool secondaryOn, string tertiaryText, bool tertiaryOn)
    {
        v.primary.gameObject.SetActive(primaryText != null);
        if (primaryText != null) { v.primaryLabel.text = primaryText; v.primary.interactable = primaryOn; }
        v.secondary.gameObject.SetActive(secondaryText != null);
        if (secondaryText != null) { v.secondaryLabel.text = secondaryText; v.secondary.interactable = secondaryOn; }
        v.tertiary.gameObject.SetActive(tertiaryText != null);
        if (tertiaryText != null) { v.tertiaryLabel.text = tertiaryText; v.tertiary.interactable = tertiaryOn; }
    }

    /// <summary>
    /// Mevcut → sonraki değerler: iki "Ad: değer" metnini satır satır karşılaştırır; değişen satıra yeşil "→ yeni" eklenir.
    /// </summary>
    public static string CompareStats(string current, string next)
    {
        if (string.IsNullOrEmpty(next) || next == current) return current ?? "";
        var a = (current ?? "").Split('\n');
        var b = next.Split('\n');
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < a.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(a[i]);
            if (i >= b.Length || b[i] == a[i]) continue;
            int colon = b[i].IndexOf(':');
            string tail = colon >= 0 && a[i].IndexOf(':') == colon ? b[i].Substring(colon + 1).Trim() : b[i];
            sb.Append("  <color=#2E7D32>→ ").Append(tail).Append("</color>");
        }
        return sb.ToString();
    }
}
