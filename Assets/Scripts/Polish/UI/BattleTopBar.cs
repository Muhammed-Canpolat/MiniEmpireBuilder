using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 8. oturum (D98): savaş üst barı. Ana Üs üst barının süslü çerçeve diliyle (OrnateStrip, altın pul, Kelt düğümü) yeniden kuruldu:
/// solda düşman sayacı "kalan / toplam", ortada "Dalga N/M" + dalga noktaları, sağda altın + ayarlar düğmesi;
/// altında bölge · seviye adı ve belirgin dalga ilerleme çubuğu. Hedef (Madeni Koru / Zamana Karşı / Ana Üs'ü Koru)
/// barın altında ayrı bir kartta (kendi simgesi + çubuğu). Veri yalnız okunur; BattleManager olaylarına abone olur.
/// Eski savaş üst barı (SafeArea/TopBar) yalnız kamera kırpması için boş yer tutucu olarak kalır (WorldViewportBars onu izler).
/// </summary>
public sealed class BattleTopBar : MonoBehaviour
{
    public enum ObjectiveKind { None, Mine, Time, Base }

    /// <summary>Başvuru piksel (390×844 tuval) cinsinden ölçüler.</summary>
    public const float StripHeight = 64f;
    public const float BarHeight = 100f;
    public const float CardHeight = 34f;
    public const float Gap = 4f;

    /// <summary>"Aktif kart" pulu (BattleObjectiveHud) varsa kapladığı yükseklik; sonraki öğeler onun altına dizilir.</summary>
    public static float CardsPillStack;

    public static BattleTopBar Instance { get; private set; }

    /// <summary>Bu savaşta hedef kartı gerekir mi (sefer türüne göre)? Boss/Hayatta kal/eski savaşlarda None.</summary>
    public static ObjectiveKind KindForBattle()
    {
        var battle = BattleManager.Instance;
        if (battle != null && battle.HasTimeLimit) return ObjectiveKind.Time;
        if (!BattleSession.IsCampaign) return ObjectiveKind.None;
        switch (BattleSession.Level?.kind)
        {
            case CampaignLevelKind.DefendMine: return ObjectiveKind.Mine;
            case CampaignLevelKind.TimeTrial: return ObjectiveKind.Time;
            case CampaignLevelKind.DefendBase: return ObjectiveKind.Base;
            default: return ObjectiveKind.None;
        }
    }

    /// <summary>Hedef kartının üst kenarı dahil, barın altında ilk boş yer (başvuru pikseli).</summary>
    public static float StackStart()
    {
        float cursor = BarHeight + Gap;
        if (KindForBattle() != ObjectiveKind.None) cursor += CardHeight + Gap;
        return cursor + CardsPillStack;
    }

    // ---- Yapı ----
    private Canvas canvas;
    private RectTransform safe, root;
    private TextMeshProUGUI enemyText, waveText, goldText, regionText, progressText;
    private RectTransform dotsRow;
    private readonly List<Image> dots = new List<Image>();
    private UiHealthBar progressBar;
    private float progress;

    // Hedef kartı
    private ObjectiveKind kind;
    private RectTransform card;
    private TextMeshProUGUI cardValue;
    private UiHealthBar cardBar;
    private Image cardBarFill;
    private float cardRatio = 1f;
    private MainBaseController mainBase;
    private Image cardIcon;
    private bool cardIconSet;

    // Dış nesneler
    private RectTransform oldTop, gear, bossTrack, incoming, countdown;
    private Canvas oldCanvas;
    private float nextScan, nextLayout;
    private Vector2Int lastScreen;
    private float lastScale;

    // Abonelik ve önbellek
    private BattleManager subscribed;
    private int shownWave = -1, shownWaveTotal = -1, shownRemaining = -1, shownTotal = -1, shownGold = int.MinValue;

    private void Awake() { Instance = this; }

    private void Start()
    {
        var canvasObject = new GameObject("BattleTopBarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 38;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safe = ReferenceUiVisuals.SafeArea(canvas.transform);
        kind = KindForBattle();
        Build();
        // Bar yalnız gösterir; dokunuşu yalnız ayar düğmesi (başka tuvalde) alır.
        foreach (var graphic in canvasObject.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    // ==================== KURULUM ====================

    private static RectTransform Make(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = ReferenceUiVisuals.Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    private static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        var rt = Make(parent, name, anchor, pivot, pos, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Taşmasın: otomatik boyut, tek satır, sığmazsa üç nokta.</summary>
    private static TextMeshProUGUI Label(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions align,
        Vector2 min, Vector2 max, Vector2 low, Vector2 high, bool title = false, bool bold = true, float minSize = 8f)
    {
        var text = ReferenceUiVisuals.Text(parent, name, value, size, color, align, min, max, low, high, title, bold);
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Image Pill(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var image = MakeImage(parent, name, anchor, pivot, pos, size, Color.white);
        image.sprite = ReferenceUiVisuals.PillSprite();
        image.type = Image.Type.Sliced;
        return image;
    }

    private void Build()
    {
        root = ReferenceUiVisuals.Rect(safe, "BattleTopBarRoot", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -BarHeight), Vector2.zero);
        var top = new Vector2(0f, 1f);
        var topStretchMin = new Vector2(0f, 1f);
        var topStretchMax = new Vector2(1f, 1f);

        // --- Çerçeve: koyu zemin + ahşap kuşak (bölge satırı) + süslü şerit (köşe süsleri, güneş levhası) ---
        ReferenceUiVisuals.Image(root, "StripFill", topStretchMin, topStretchMax, new Vector2(0f, -StripHeight), Vector2.zero,
            ReferenceUiVisuals.Background);
        ReferenceUiVisuals.Image(root, "Plank", topStretchMin, topStretchMax, new Vector2(0f, -BarHeight), new Vector2(0f, -StripHeight + 3f),
            ReferenceUiVisuals.Hex(0x241A10));
        ReferenceUiVisuals.Image(root, "PlankRule", topStretchMin, topStretchMax, new Vector2(0f, -BarHeight), new Vector2(0f, -BarHeight + 2f),
            ReferenceUiVisuals.Hex(0xB8892F));
        var strip = BaseHudRedesign.OrnateStrip(true);
        var ornament = ReferenceUiVisuals.Image(root, "Ornament", topStretchMin, topStretchMax, new Vector2(0f, -StripHeight), Vector2.zero,
            strip != null ? Color.white : ReferenceUiVisuals.Panel);
        if (strip != null) ornament.sprite = strip;

        // Kelt düğümü süsleri: çerçevenin gümüş düğüm levhaları (bölge satırının iki yanı).
        var knotLeft = KnotSprite(true);
        var knotRight = KnotSprite(false);
        if (knotLeft != null)
            MakeImage(root, "KnotLeft", top, top, new Vector2(10f, -66f), new Vector2(48f, 18f), Color.white).sprite = knotLeft;
        if (knotRight != null)
            MakeImage(root, "KnotRight", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -66f), new Vector2(48f, 18f), Color.white).sprite = knotRight;

        // --- Sol: düşman sayacı ---
        var enemyPill = Pill(root, "EnemyPill", top, top, new Vector2(34f, -14f), new Vector2(90f, 36f));
        var enemyIcon = ReferenceUiVisuals.Image(enemyPill.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(6f, -9f), new Vector2(24f, 9f), ReferenceUiVisuals.Hex(0xFF6A5C));
        enemyIcon.sprite = SpriteManager.Instance != null ? SpriteManager.Instance.IconBattleNew : null;
        enemyIcon.preserveAspect = true;
        enemyIcon.enabled = enemyIcon.sprite != null;
        enemyText = Label(enemyPill.transform, "EnemyText", "0 / 0", 15f, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(27f, 2f), new Vector2(-6f, -2f));

        // --- Orta: dalga ---
        var wavePill = Pill(root, "WavePill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(128f, 48f));
        waveText = Label(wavePill.transform, "WaveText", L.T("Dalga", "Wave") + " 1/1", 15f, ReferenceUiVisuals.Gold,
            TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -28f), new Vector2(-8f, -5f), title: true);
        dotsRow = ReferenceUiVisuals.Rect(wavePill.transform, "WaveDots", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(8f, -43f), new Vector2(-8f, -30f));

        // --- Sağ: altın + ayarlar ---
        var goldPill = Pill(root, "GoldPill", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -14f), new Vector2(68f, 36f));
        var coin = ReferenceUiVisuals.Image(goldPill.transform, "Coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(5f, -8f), new Vector2(21f, 8f), Color.white);
        coin.sprite = SpriteManager.Instance != null ? SpriteManager.Instance.IconGold : null;
        coin.preserveAspect = true;
        coin.enabled = coin.sprite != null;
        goldText = Label(goldPill.transform, "GoldText", "0", 14f, ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(23f, 2f), new Vector2(-5f, -2f));

        var ring = MakeImage(root, "GearRing", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-17f, -15f), new Vector2(34f, 34f),
            ReferenceUiVisuals.Gold);
        ring.sprite = BaseMapManager.MakeCircleSprite(Color.white);

        // --- Bölge · seviye adı ---
        regionText = Label(root, "RegionText", RegionLine(), 12f, ReferenceUiVisuals.Hex(0xE8D8A8), TextAlignmentOptions.Center,
            topStretchMin, topStretchMax, new Vector2(66f, -84f), new Vector2(-66f, -66f));
        if (BattleSession.Level?.kind == CampaignLevelKind.Boss) regionText.color = ReferenceUiVisuals.Red;

        // --- Dalga ilerleme çubuğu (belirgin: 12 px, altın çerçeve) ---
        var track = ReferenceUiVisuals.Image(root, "WaveProgress", topStretchMin, topStretchMax, new Vector2(16f, -97f), new Vector2(-16f, -86f),
            ReferenceUiVisuals.Hex(0x120D08));
        track.sprite = ReferenceUiVisuals.SolidPillSprite();
        track.type = Image.Type.Sliced;
        var frame = track.gameObject.AddComponent<Outline>();
        frame.effectColor = ReferenceUiVisuals.Gold;
        frame.effectDistance = new Vector2(1.2f, -1.2f);
        progressBar = UiHealthBar.Build(track.rectTransform, ReferenceUiVisuals.Hex(0xE0892E), () => progress);
        progressText = Label(track.transform, "ProgressPercent", "0%", 9f, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, minSize: 6f);

        if (kind != ObjectiveKind.None) BuildCard();
    }

    private void BuildCard()
    {
        card = Make(root, "ObjectiveCard", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(BarHeight + Gap)),
            new Vector2(236f, CardHeight));
        var bg = card.gameObject.AddComponent<Image>();
        bg.sprite = ReferenceUiVisuals.PillSprite();
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;
        var frame = card.gameObject.AddComponent<Outline>();
        frame.effectColor = new Color(ReferenceUiVisuals.Gold.r, ReferenceUiVisuals.Gold.g, ReferenceUiVisuals.Gold.b, 0.8f);
        frame.effectDistance = new Vector2(1f, -1f);

        cardIcon = ReferenceUiVisuals.Image(card, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(6f, -12f), new Vector2(30f, 12f), Color.white);
        cardIcon.preserveAspect = true;
        cardIcon.enabled = false;

        string label = kind == ObjectiveKind.Mine ? L.T("Madeni Koru", "Defend the Mine")
            : kind == ObjectiveKind.Time ? L.T("Kalan Süre", "Time Left")
            : L.T("Ana Üs'ü Koru", "Defend the Base");
        Label(card, "ObjectiveLabel", label, 12f, ReferenceUiVisuals.Muted, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -19f), new Vector2(-66f, -4f));
        cardValue = Label(card, "ObjectiveValue", "", 13f, ReferenceUiVisuals.Gold, TextAlignmentOptions.Right,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-64f, -19f), new Vector2(-12f, -4f));

        var track = ReferenceUiVisuals.Image(card, "ObjectiveBar", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(36f, 6f), new Vector2(-14f, 14f), ReferenceUiVisuals.Hex(0x120D08));
        track.sprite = ReferenceUiVisuals.SolidPillSprite();
        track.type = Image.Type.Sliced;
        cardBar = UiHealthBar.Build(track.rectTransform, ReferenceUiVisuals.Gold, () => cardRatio);
        cardBarFill = cardBar.Fill;
        foreach (var graphic in card.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    // ==================== YARDIMCI İÇERİK ====================

    private static string RegionLine()
    {
        var region = BattleSession.Region;
        if (BattleSession.IsCampaign && region != null)
        {
            if (BattleSession.Level?.kind == CampaignLevelKind.Boss)
                return L.T("Boss", "Boss") + " · " + L.Asset(region.bossName);
            if (BattleSession.IsEndless)
                return L.T($"Sonsuz Sefer · Kat {BattleSession.Floor}", $"Endless · Floor {BattleSession.Floor}");
            if (BattleSession.IsInvasion) return L.Asset(region.displayName);
            return $"{L.Asset(region.displayName)} · " + L.T("Seviye ", "Level ") + BattleSession.LevelNumber;
        }
        return L.T("Seviye ", "Level ") + (GameManager.Instance?.PlayerData?.currentBattleLevel ?? 1);
    }

    private static Sprite knotL, knotR, clock;

    /// <summary>Çerçeve görselinin gümüş Kelt düğümü levhası (üst kuşaktan kesit); yoksa null.</summary>
    private static Sprite KnotSprite(bool left)
    {
        if (left ? knotL != null : knotR != null) return left ? knotL : knotR;
        var frames = Resources.LoadAll<Sprite>("Sprites/Intro/intro_button");
        if (frames.Length == 0) return null;
        Rect r = frames[0].rect;
        float x0 = left ? 0.225f : 0.645f, w = left ? 0.144f : 0.135f;
        var crop = new Rect(r.x + r.width * x0, r.y + r.height * (1f - 0.225f), r.width * w, r.height * 0.10f);
        var sprite = Sprite.Create(frames[0].texture, crop, new Vector2(0.5f, 0.5f), frames[0].pixelsPerUnit);
        if (left) knotL = sprite; else knotR = sprite;
        return sprite;
    }

    /// <summary>Zamana Karşı kartının simgesi: beyaz halka + akrep/yelkovan (Image.color ile boyanır).</summary>
    private static Sprite ClockSprite()
    {
        if (clock != null) return clock;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = x - 15.5f, dy = y - 15.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(15.5f - d) * Mathf.Clamp01(d - 12.5f);                  // halka
            bool minute = Mathf.Abs(dx) < 1.3f && dy > 0f && dy < 11f;                       // yelkovan (yukarı)
            bool hour = Mathf.Abs(dy) < 1.3f && dx > 0f && dx < 8f;                          // akrep (sağa)
            if (minute || hour || d < 1.8f) a = 1f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        clock = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return clock;
    }

    // ==================== VERİ ====================

    private void Subscribe(BattleManager battle)
    {
        if (battle == null || subscribed == battle) return;
        Unsubscribe();
        subscribed = battle;
        battle.OnEnemyTallyChanged += SetEnemies;
        battle.OnWaveChanged += SetWave;
        battle.OnGoldEarnedChanged += SetGold;
    }

    private void Unsubscribe()
    {
        if (subscribed == null) return;
        subscribed.OnEnemyTallyChanged -= SetEnemies;
        subscribed.OnWaveChanged -= SetWave;
        subscribed.OnGoldEarnedChanged -= SetGold;
        subscribed = null;
    }

    private void SetEnemies(int remaining, int total)
    {
        if (enemyText == null || (remaining == shownRemaining && total == shownTotal)) return;
        shownRemaining = remaining;
        shownTotal = total;
        enemyText.text = $"{remaining} / {total}";
    }

    private void SetWave(int current, int total)
    {
        total = Mathf.Max(1, total);
        current = Mathf.Clamp(current, 1, total);
        if (current == shownWave && total == shownWaveTotal) return;
        bool rebuild = total != shownWaveTotal;
        shownWave = current;
        shownWaveTotal = total;
        if (waveText != null) waveText.text = $"{L.T("Dalga", "Wave")} {current}/{total}";
        if (rebuild) RebuildDots(total);
        PaintDots();
    }

    private void SetGold(int gold)
    {
        if (goldText == null || gold == shownGold) return;
        shownGold = gold;
        goldText.text = gold <= 99999 ? gold.ToString() : NumberFormat.Short(gold);
    }

    private void RebuildDots(int total)
    {
        foreach (var dot in dots) if (dot != null) Destroy(dot.gameObject);
        dots.Clear();
        if (dotsRow == null || total > 12) return;   // çok dalgada (Sonsuz) yalnız metin
        float spacing = Mathf.Min(16f, 110f / total);
        for (int i = 0; i < total; i++)
        {
            var dot = MakeImage(dotsRow, "Dot" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2((i - (total - 1) * 0.5f) * spacing, 0f), new Vector2(9f, 9f), Color.white);
            dot.sprite = ReferenceUiVisuals.SolidPillSprite();
            var outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = ReferenceUiVisuals.Hex(0x6B4A22);
            outline.effectDistance = new Vector2(0.8f, -0.8f);
            dots.Add(dot);
        }
    }

    /// <summary>Tamamlanan dalgalar dolu altın, şimdiki dalga parlak ve büyük, gelecek dalgalar koyu.</summary>
    private void PaintDots()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] == null) continue;
            int number = i + 1;
            dots[i].color = number < shownWave ? ReferenceUiVisuals.Gold
                : number == shownWave ? ReferenceUiVisuals.Hex(0xFFF1B8) : ReferenceUiVisuals.Hex(0x3A3020);
            dots[i].rectTransform.sizeDelta = number == shownWave ? new Vector2(12f, 12f) : new Vector2(9f, 9f);
        }
    }

    private void Update()
    {
        var battle = BattleManager.Instance;
        if (battle != null)
        {
            Subscribe(battle);
            SetEnemies(battle.EnemiesRemaining, battle.EnemiesTotal);
            SetWave(Mathf.Max(1, battle.CurrentWave), Mathf.Max(1, battle.WaveCount > 0 ? battle.WaveCount : battle.TotalWaves));
            SetGold(battle.TotalGoldEarned);
            // İlerleme: genel (öldürülen / toplam düşman). Dalga başına boyut BattleManager'da açık değil.
            progress = battle.EnemiesTotal > 0 ? Mathf.Clamp01((float)battle.EnemiesDefeated / battle.EnemiesTotal) : 0f;
            if (progressText != null) progressText.text = Mathf.RoundToInt(progress * 100f) + "%";
            UpdateCard(battle);
        }

        // Çalışma anı pulse: şimdiki dalga noktası.
        if (shownWave >= 1 && shownWave <= dots.Count && dots[shownWave - 1] != null)
            dots[shownWave - 1].transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f));

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            FindExternals();
        }
        LayoutExternals();
    }

    private void UpdateCard(BattleManager battle)
    {
        if (card == null || cardValue == null) return;
        if (!cardIconSet) SetCardIcon();
        Color fill = ReferenceUiVisuals.Gold;
        switch (kind)
        {
            case ObjectiveKind.Mine:
                cardRatio = battle.DefendedMine != null ? Mathf.Clamp01(battle.DefendedMine.HealthPercent) : 1f;
                cardValue.text = Percent(cardRatio);
                fill = cardRatio < 0.35f ? ReferenceUiVisuals.Red : ReferenceUiVisuals.Gold;
                break;
            case ObjectiveKind.Time:
            {
                float remaining = battle.TimeRemaining;
                cardRatio = Mathf.Clamp01(remaining / BattleManager.TimeTrialSeconds);
                int total = Mathf.CeilToInt(Mathf.Max(0f, remaining));
                cardValue.text = $"{total / 60}:{total % 60:00}";
                fill = remaining <= 30f ? ReferenceUiVisuals.Red : ReferenceUiVisuals.Blue;
                break;
            }
            case ObjectiveKind.Base:
                if (mainBase == null) mainBase = FindFirstObjectByType<MainBaseController>();
                cardRatio = mainBase != null ? Mathf.Clamp01(mainBase.HealthPercent) : 1f;
                cardValue.text = Percent(cardRatio);
                fill = cardRatio < 0.3f ? ReferenceUiVisuals.Red : ReferenceUiVisuals.Hex(0x4FCF74);
                break;
        }
        cardValue.color = fill == ReferenceUiVisuals.Blue ? ReferenceUiVisuals.Light : fill;
        if (cardBarFill != null) cardBarFill.color = fill;
    }

    private static string Percent(float ratio)
    {
        int value = Mathf.CeilToInt(ratio * 100f);
        return L.English ? value + "%" : "%" + value;
    }

    private void SetCardIcon()
    {
        if (cardIcon == null) return;
        Sprite sprite = null;
        Color tint = Color.white;
        switch (kind)
        {
            case ObjectiveKind.Mine: sprite = SpriteManager.Instance != null ? SpriteManager.Instance.GoldMine : null; break;
            case ObjectiveKind.Time: sprite = ClockSprite(); tint = ReferenceUiVisuals.Gold; break;
            case ObjectiveKind.Base:
                var gm = GameManager.Instance;
                if (gm?.Buildings != null && gm.Buildings.TryGetValue(BuildingType.MainBase, out var data))
                    sprite = data.VisualForLevel(data.level)?.sprite;
                if (sprite == null && SpriteManager.Instance != null) sprite = SpriteManager.Instance.MainBase;
                break;
        }
        if (sprite == null) return;
        cardIconSet = true;
        cardIcon.sprite = sprite;
        cardIcon.color = tint;
        cardIcon.enabled = true;
    }

    // ==================== DIŞ NESNELER (eski bar, ayar düğmesi, uyarı çipleri) ====================

    private void FindExternals()
    {
        if (oldTop == null)
        {
            var battleCanvas = GameObject.Find("BattleCanvas");
            var found = battleCanvas != null ? battleCanvas.transform.Find("SafeArea/TopBar") as RectTransform : null;
            if (found != null) AdoptOldTopBar(found, battleCanvas.GetComponent<Canvas>());
        }
        if (gear == null)
        {
            var button = GameObject.Find("AudioSettingsButton");
            if (button != null) gear = button.transform as RectTransform;
        }
        if (incoming == null || countdown == null || (bossTrack == null && BattleSession.Level?.kind == CampaignLevelKind.Boss))
        {
            foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rt.name == "IncomingChip") incoming = rt;
                else if (rt.name == "NextWaveCountdown") countdown = rt;
                else if (rt.name == "BossHealthTrack") bossTrack = rt;
            }
        }
    }

    /// <summary>Eski çubuğun görünümü kapanır; yalnız kamerayı kırpan yer tutucu olarak yeni barın yüksekliğine ayarlanır.</summary>
    private void AdoptOldTopBar(RectTransform top, Canvas owner)
    {
        oldTop = top;
        oldCanvas = owner;
        var image = top.GetComponent<Image>();
        if (image != null) image.enabled = false;
        var gradient = top.GetComponent<UIGradient>();
        if (gradient != null) gradient.enabled = false;
        for (int i = 0; i < top.childCount; i++) top.GetChild(i).gameObject.SetActive(false);   // eski düşman/dalga/altın/bölge/ince çubuk
        lastScale = -1f;
    }

    private void LayoutExternals()
    {
        // Ayar düğmesi: altın halkanın içine (başka tuvalde; davranışı AudioSettingsUI.Open aynen kalır).
        if (gear != null)
        {
            gear.anchorMin = gear.anchorMax = new Vector2(1f, 1f);
            gear.pivot = new Vector2(0.5f, 0.5f);
            gear.sizeDelta = new Vector2(28f, 28f);
            gear.anchoredPosition = new Vector2(-34f, -32f);
        }

        float scale = canvas != null ? canvas.scaleFactor : 0f;
        var screen = new Vector2Int(Screen.width, Screen.height);
        bool changed = screen != lastScreen || !Mathf.Approximately(scale, lastScale);
        if (changed)
        {
            lastScreen = screen;
            lastScale = scale;
            if (oldTop != null && oldCanvas != null && oldCanvas.scaleFactor > 0.0001f)
            {
                oldTop.anchorMin = new Vector2(0f, 1f);
                oldTop.anchorMax = new Vector2(1f, 1f);
                oldTop.pivot = new Vector2(0.5f, 1f);
                oldTop.sizeDelta = new Vector2(0f, BarHeight * scale / oldCanvas.scaleFactor);
            }
        }

        if (Time.unscaledTime < nextLayout && !changed) return;
        nextLayout = Time.unscaledTime + 0.25f;
        float height = safe != null ? safe.rect.height : 0f;
        if (height <= 1f) return;
        float cursor = StackStart();
        if (bossTrack != null) { Band(bossTrack, cursor, 22f, height); cursor += 26f; }
        if (incoming != null) { Band(incoming, cursor, 30f, height); cursor += 32f; }
        if (countdown != null) Band(countdown, cursor, 22f, height);
    }

    /// <summary>Çubuğu üstten <paramref name="top"/> pikselde, <paramref name="size"/> yüksekliğinde yerleştirir (yatay ayar korunur).</summary>
    private static void Band(RectTransform rt, float top, float size, float safeHeight)
    {
        rt.anchorMin = new Vector2(rt.anchorMin.x, 1f - (top + size) / safeHeight);
        rt.anchorMax = new Vector2(rt.anchorMax.x, 1f - top / safeHeight);
        rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
        rt.offsetMax = new Vector2(rt.offsetMax.x, 0f);
    }
}
