using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Akın Panosu (island_base_v2 §3): AKIN düğmesi bu paneli açar. Merdiven kartları: N, güç, kumsal iğneleri, düşman portreleri,
/// ödül ve şart. Kart seçilir, "SAVUNMAYA BAŞLA" savunmayı başlatır. Tiny Swords kâğıt/düğme/kurdele sprite'larıyla çizilir.
/// (Zamanlı baskın kırmızı kartı Faz D'de eklenir.)
/// </summary>
public sealed class IslandRaidBoard
{
    private const int Cards = 4;

    public Action<int> Selected;
    public Action StartPressed, ClosePressed;
    /// <summary>Faz D: bekleyen zamanlı baskın kırmızı kartı.</summary>
    public struct RaidInfo { public bool active, live; public string title, detail; }
    public Action RaidStartPressed;
    private RectTransform raidStrip;
    private TextMeshProUGUI raidTitle, raidDetail;
    private Button raidButton;

    private GameObject root;
    private RectTransform cardsRoot;
    private TextMeshProUGUI footerNote, startLabel;
    private Button startButton, upButton, downButton;
    private readonly List<Card> cards = new List<Card>();
    private int selected = 1;

    private sealed class Card
    {
        public RectTransform rect;
        public Image paper;
        public Button button;
        public TextMeshProUGUI number, power, waves, rewardWood, rewardGold, rewardMeat, rewardGems, state;
        public Image iconWood, iconGold, iconMeat;
        public readonly List<Image> portraits = new List<Image>();
        public readonly List<TextMeshProUGUI> pins = new List<TextMeshProUGUI>();
        public readonly List<Image> pinBackgrounds = new List<Image>();
        public int akin;
    }

    public bool Visible => root != null && root.activeSelf;
    public int Selection => selected;

    private static Sprite Ui(string name)
    {
        var catalog = IslandVisualCatalog.Instance;
        return catalog != null ? catalog.Single(name) : null;
    }

    private static Image NineSlice(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high,
        string sprite, Color tint, float ppuMultiplier)
    {
        var image = ReferenceUiVisuals.Image(parent, name, min, max, low, high, tint);
        image.sprite = Ui(sprite);
        if (image.sprite != null)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ppuMultiplier;
        }
        return image;
    }

    public void Build(RectTransform safe)
    {
        var panel = NineSlice(safe, "RaidBoard", new Vector2(0.03f, 0.11f), new Vector2(0.97f, 0.83f), Vector2.zero, Vector2.zero,
            "ui_paper", Color.white, 2.4f);
        panel.raycastTarget = true;   // arkadaki dünya dokunuşunu yutar
        root = panel.gameObject;

        var ribbon = NineSlice(panel.transform, "Ribbon", new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0, -34), new Vector2(0, 20),
            "ui_ribbon", Color.white, 3f);
        ReferenceUiVisuals.Text(ribbon.transform, "Title", L.T("AKIN PANOSU", "RAID BOARD"), 20, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(40, 8), new Vector2(-40, -4), true);

        upButton = ReferenceUiVisuals.Button(panel.transform, "Prev", "<", false, new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(14, 8), new Vector2(58, 52), 20);
        upButton.onClick.AddListener(() => Selected?.Invoke(Mathf.Max(1, selected - 1)));
        downButton = ReferenceUiVisuals.Button(panel.transform, "Next", ">", false, new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-58, 8), new Vector2(-14, 52), 20);
        downButton.onClick.AddListener(() => Selected?.Invoke(selected + 1));
        var close = ReferenceUiVisuals.Button(panel.transform, "Close", "X", false, new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-42, -40), new Vector2(-12, -12), 14);
        close.onClick.AddListener(() => ClosePressed?.Invoke());

        cardsRoot = ReferenceUiVisuals.Rect(panel.transform, "Cards", Vector2.zero, Vector2.one, new Vector2(18, 84), new Vector2(-18, -44));
        for (int i = 0; i < Cards; i++) cards.Add(BuildCard(i));
        BuildRaidStrip(panel.transform);

        footerNote = ReferenceUiVisuals.Text(panel.transform, "Note", "", 12, new Color(0.35f, 0.2f, 0.1f), TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(70, 56), new Vector2(-70, 80), false, true);
        var start = NineSlice(panel.transform, "StartButtonBg", new Vector2(0.2f, 0), new Vector2(0.8f, 0), new Vector2(0, 8), new Vector2(0, 52),
            "ui_button_blue", Color.white, 3f);
        start.raycastTarget = true;
        startButton = start.gameObject.AddComponent<Button>();
        startButton.targetGraphic = start;
        startButton.transition = Selectable.Transition.ColorTint;
        startLabel = ReferenceUiVisuals.Text(start.transform, "Label", L.T("SAVUNMAYA BAŞLA", "START DEFENSE"), 17, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -2), true);
        startButton.onClick.AddListener(() => StartPressed?.Invoke());
        root.SetActive(false);
    }

    /// <summary>Üstte kırmızı kart (bekleyen baskın varsa): başlık, kısa ayrıntı, pencere açıkken SAVUN düğmesi.</summary>
    private void BuildRaidStrip(Transform panel)
    {
        raidStrip = ReferenceUiVisuals.Rect(panel, "RaidStrip", new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -102), new Vector2(-18, -42));
        var paper = NineSlice(raidStrip, "Paper", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "ui_paper_special", new Color(1f, 0.5f, 0.45f), 4f);
        paper.raycastTarget = true;
        raidTitle = ReferenceUiVisuals.Text(raidStrip, "Title", "", 15, new Color(1f, 0.95f, 0.8f), TextAlignmentOptions.Left,
            new Vector2(0, 0.45f), Vector2.one, new Vector2(16, 0), new Vector2(-112, -10), true);
        raidDetail = ReferenceUiVisuals.Text(raidStrip, "Detail", "", 10, new Color(1f, 0.92f, 0.78f), TextAlignmentOptions.Left,
            Vector2.zero, new Vector2(1, 0.5f), new Vector2(16, 4), new Vector2(-112, 0), false, true);
        var go = NineSlice(raidStrip, "RaidDefend", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-104, 8), new Vector2(-10, -8), "ui_button_red", Color.white, 3f);
        go.raycastTarget = true;
        raidButton = go.gameObject.AddComponent<Button>();
        raidButton.targetGraphic = go;
        raidButton.transition = Selectable.Transition.ColorTint;
        ReferenceUiVisuals.Text(go.transform, "Label", L.T("SAVUN", "DEFEND"), 14, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-4, -2), true);
        raidButton.onClick.AddListener(() => RaidStartPressed?.Invoke());
        raidStrip.gameObject.SetActive(false);
    }

    /// <summary>Bekleyen baskın kartını göster/gizle; kartlar alanı kırmızı kart kadar aşağı kayar.</summary>
    public void SetRaid(RaidInfo info)
    {
        if (raidStrip == null) return;
        if (raidStrip.gameObject.activeSelf != info.active)
        {
            raidStrip.gameObject.SetActive(info.active);
            cardsRoot.offsetMax = new Vector2(-18, info.active ? -108 : -44);
        }
        if (!info.active) return;
        raidTitle.text = info.title;
        raidDetail.text = info.detail;
        raidButton.gameObject.SetActive(info.live);
    }

    private Card BuildCard(int index)
    {
        var card = new Card();
        float top = 1f - index / (float)Cards, bottom = 1f - (index + 1) / (float)Cards;
        card.rect = ReferenceUiVisuals.Rect(cardsRoot, "Card" + index, new Vector2(0, bottom), new Vector2(1, top), new Vector2(0, 3), new Vector2(0, -3));
        card.paper = NineSlice(card.rect, "Paper", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "ui_paper_special", Color.white, 4f);
        card.paper.raycastTarget = true;
        card.button = card.paper.gameObject.AddComponent<Button>();
        card.button.targetGraphic = card.paper;
        card.button.transition = Selectable.Transition.None;
        card.button.onClick.AddListener(() => { if (card.akin > 0) Selected?.Invoke(card.akin); });

        var dark = new Color(1f, 0.95f, 0.78f);
        card.number = ReferenceUiVisuals.Text(card.rect, "N", "1", 30, dark, TextAlignmentOptions.Center,
            new Vector2(0, 0.28f), new Vector2(0, 1), new Vector2(14, 0), new Vector2(66, -8), true);
        card.power = ReferenceUiVisuals.Text(card.rect, "Power", "", 11, dark, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(0, 0.34f), new Vector2(8, 8), new Vector2(72, 0), false, true);
        card.waves = ReferenceUiVisuals.Text(card.rect, "Waves", "", 11, dark, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0.64f, 1), new Vector2(76, -26), new Vector2(0, -8), false, true);
        // Düşman portreleri (yeni türler sağda)
        for (int p = 0; p < 6; p++)
        {
            var portrait = ReferenceUiVisuals.Image(card.rect, "Portrait" + p, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(76 + p * 34, -22), new Vector2(76 + p * 34 + 32, 10), Color.white);
            portrait.preserveAspect = true;
            card.portraits.Add(portrait);
        }
        // Kumsal iğneleri A–D
        for (int b = 0; b < 4; b++)
        {
            var pin = ReferenceUiVisuals.Image(card.rect, "Pin" + b, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(76 + b * 26, 8), new Vector2(76 + b * 26 + 22, 30), new Color(0.25f, 0.5f, 0.7f));
            pin.sprite = ReferenceUiVisuals.PillSprite();
            pin.type = Image.Type.Sliced;
            var letter = ReferenceUiVisuals.Text(pin.transform, "L", IslandLayout.Beaches[b].name.ToString(), 11, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            card.pins.Add(letter);
            card.pinBackgrounds.Add(pin);
        }
        // Ödül sütunu
        var catalog = IslandVisualCatalog.Instance;
        card.iconWood = RewardIcon(card.rect, 0, catalog != null ? catalog.woodIcon : null, out card.rewardWood);
        card.iconGold = RewardIcon(card.rect, 1, catalog != null ? catalog.Single("gold_icon") : null, out card.rewardGold);
        card.iconMeat = RewardIcon(card.rect, 2, catalog != null ? catalog.meatIcon : null, out card.rewardMeat);
        card.rewardGems = ReferenceUiVisuals.Text(card.rect, "Gems", "", 11, new Color(0.65f, 0.88f, 1f), TextAlignmentOptions.Right,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-96, 6), new Vector2(-10, 24), false, true);
        card.state = ReferenceUiVisuals.Text(card.rect, "State", "", 11, new Color(1f, 0.82f, 0.4f), TextAlignmentOptions.Right,
            new Vector2(0.4f, 1), new Vector2(1, 1), new Vector2(0, -26), new Vector2(-12, -8), false, true);
        return card;
    }

    private static Image RewardIcon(RectTransform card, int row, Sprite sprite, out TextMeshProUGUI value)
    {
        float y = -36 - row * 20;
        var icon = ReferenceUiVisuals.Image(card, "RewardIcon" + row, new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-96, y - 10), new Vector2(-76, y + 10), Color.white);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        value = ReferenceUiVisuals.Text(card, "RewardValue" + row, "", 13, Color.white, TextAlignmentOptions.Left,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-72, y - 10), new Vector2(-8, y + 10), false, true);
        return icon;
    }

    public void SetVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible) root.SetActive(visible);
    }

    private static Sprite PortraitOf(EnemyType type)
    {
        var catalog = IslandVisualCatalog.Instance;
        if (catalog == null) return null;
        switch (type)
        {
            case EnemyType.Raider: return IslandVisualCatalog.Has(catalog.raiderIdle) ? catalog.raiderIdle[0] : null;
            case EnemyType.ShoreArcher: return IslandVisualCatalog.Has(catalog.shoreArcherIdle) ? catalog.shoreArcherIdle[0] : null;
            case EnemyType.ShieldLancer: return catalog.Single("lancer_idle");
            default:
                var entry = catalog.Monster(type.ToString());
                return entry != null ? entry.portrait : null;
        }
    }

    /// <summary>Portre boyu normalizasyonu: Tiny Swords kareleri (192 px, içerik ~%40) büyütülür; DM2D parçaları kutuya sığdırılır.</summary>
    private static float PortraitScale(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Raider: case EnemyType.ShoreArcher: case EnemyType.ShieldLancer: return 2.3f;
            case EnemyType.Bat: return 0.7f;
            case EnemyType.Rat: return 0.85f;
            default: return 1f;
        }
    }

    /// <summary>Panoyu yeniler. <paramref name="maxSelectable"/>: oynanabilir en yüksek Akın; sonrası kilitli kartlar olarak görünür.</summary>
    public void Refresh(int selectedAkin, int highestAkin, int keepLevel, int maxSelectable, bool canStart, string gateText)
    {
        selected = Mathf.Max(1, selectedAkin);
        int first = Mathf.Max(1, selected - 1);
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            int n = first + i;
            card.akin = n;
            bool isSelected = n == selected;
            bool open = n <= maxSelectable && Progression.Island.CanStartAkin(n, keepLevel, highestAkin);
            card.paper.color = !open ? new Color(0.5f, 0.5f, 0.56f) : isSelected ? Color.white : new Color(0.74f, 0.8f, 0.92f);
            card.rect.localScale = Vector3.one * (isSelected ? 1f : 0.97f);
            card.number.text = n.ToString();
            card.power.text = L.T($"Güç {Progression.Island.AkinBattleLevel(n)}", $"Power {Progression.Island.AkinBattleLevel(n)}");
            string boss = Progression.Island.IsBossAkin(n) ? L.T(" · BOSS", " · BOSS") : "";
            card.waves.text = L.T($"{Progression.Island.AkinWaves(n)} dalga · {Progression.Island.AkinBoats(n)} tekne{boss}",
                $"{Progression.Island.AkinWaves(n)} waves · {Progression.Island.AkinBoats(n)} boats{boss}");
            var roster = IslandWaves.RosterFor(n);
            for (int p = 0; p < card.portraits.Count; p++)
            {
                bool has = p < roster.Count;
                card.portraits[p].gameObject.SetActive(has);
                if (!has) continue;
                card.portraits[p].sprite = PortraitOf(roster[p]);
                card.portraits[p].rectTransform.localScale = Vector3.one * PortraitScale(roster[p]);
                card.portraits[p].color = n > highestAkin + 1 ? new Color(0.45f, 0.45f, 0.45f) : Color.white;
            }
            int beaches = Mathf.Min(Progression.Island.BeachesOpen(n), IslandLayout.Beaches.Length);
            for (int b = 0; b < card.pins.Count; b++)
                card.pinBackgrounds[b].color = b < beaches ? new Color(0.78f, 0.25f, 0.18f) : new Color(0.5f, 0.5f, 0.5f, 0.55f);
            var reward = IslandSession.RewardFor(n, highestAkin);
            card.rewardWood.text = reward.wood.ToString();
            card.rewardGold.text = reward.gold.ToString();
            card.rewardMeat.text = reward.meat.ToString();
            card.rewardGems.text = reward.gems > 0 ? L.T($"+{reward.gems} elmas", $"+{reward.gems} gems") : "";
            card.state.text = !open ? L.T("KİLİTLİ", "LOCKED") : n <= highestAkin ? L.T("TEKRAR (%30, Et 0)", "REPLAY (30%, no Meat)") : isSelected ? L.T("SIRADAKİ", "NEXT") : "";
        }
        upButton.interactable = selected > 1;
        downButton.interactable = selected < maxSelectable;
        startButton.interactable = canStart;
        footerNote.text = canStart ? L.T("Kırmızı iğneler: tekneler bu kumsallara çıkar.", "Red pins: boats land on these beaches.") : gateText;
    }
}
