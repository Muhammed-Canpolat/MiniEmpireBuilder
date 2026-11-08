using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation of the existing battle result. The return Button keeps its original listener.
public sealed class VictoryReferenceView : MonoBehaviour
{
    private RectTransform panel;
    private TextMeshProUGUI title;
    private TextMeshProUGUI[] values;
    private CanvasGroup[] rows;
    private Button returnButton;
    private Button retryButton;
    private Image[] campaignStars;
    private TextMeshProUGUI starReason;

    public void Initialize(RectTransform inner, TextMeshProUGUI existingTitle, TextMeshProUGUI existingReward, Button existingReturn)
    {
        var overlay = GetComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0.65f);
        var safe = ReferenceUiVisuals.SafeArea(transform);
        inner.SetParent(safe, false);
        panel = inner;
        inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
        inner.pivot = new Vector2(0.5f, 0.5f);
        bool campaign = BattleSession.IsCampaign;
        float panelWidth = 360f;
        float panelHeight = campaign ? 640f : 560f;
        inner.sizeDelta = new Vector2(panelWidth, panelHeight);
        inner.anchoredPosition = Vector2.zero;
        // Kök Image yalnız ışın hedefi; görünen dolgu çerçevenin içine çekilir ki
        // çerçeve köşelerinin saydam kısımlarından taşmasın.
        inner.GetComponent<Image>().color = Color.clear;
        BuildOrnateFrame(inner, panelWidth);

        var border = inner.Find("EndBorder");
        if (border != null) border.gameObject.SetActive(false);
        existingReward.gameObject.SetActive(false);
        title = existingTitle;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(300, 54);
        title.rectTransform.anchoredPosition = new Vector2(0, -182);
        title.fontSize = 38;
        title.color = ReferenceUiVisuals.Gold;
        var titleFont = Resources.Load<Font>("Fonts/Cinzel-ExtraBold");
        if (titleFont != null) title.font = TMP_FontAsset.CreateFontAsset(titleFont);

        var portrait = ReferenceUiVisuals.Image(inner, "VictoryHero", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(-65, -162), new Vector2(65, -52), Color.white);
        var gm = GameManager.Instance;
        if (gm != null && gm.PlayerData != null && SpriteManager.Instance != null)
            portrait.sprite = HeroPortraitSprite.Get(SpriteManager.Instance.GetHeroSprite(gm.PlayerData.hero.weaponType));
        portrait.preserveAspect = true;

        values = new TextMeshProUGUI[4];
        rows = new CanvasGroup[4];
        string[] labels = { "Düşman altını", "Zafer ödülü", "İnşa maliyeti", "Üsse aktarılan" };
        for (int i = 0; i < 4; i++)
        {
            // Satırlar 40 birim aralıklı; toplam satırı ayraçla birlikte 48 birim aşağıda.
            // Seferde yıldız ve yıldız açıklaması için satırlar biraz aşağıda başlar.
            float first = campaign ? -282f : -226f;
            float step = campaign ? 38f : 40f;
            float y = i == 3 ? first - 2 * step - 46f : first - i * step;
            var row = ReferenceUiVisuals.Rect(inner, "ResultRow" + i, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-112, y - 34), new Vector2(112, y));
            rows[i] = row.gameObject.AddComponent<CanvasGroup>();
            ReferenceUiVisuals.Text(row, "Label", labels[i], i == 3 ? 22 : 19,
                i == 3 ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Light, TextAlignmentOptions.Left,
                new Vector2(0, 0), new Vector2(0.72f, 1), Vector2.zero, Vector2.zero, bodyBold: true);
            values[i] = ReferenceUiVisuals.Text(row, "Value", "0", i == 3 ? 24 : 19,
                i == 3 ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Light, TextAlignmentOptions.Right,
                new Vector2(0.72f, 0), Vector2.one, Vector2.zero, Vector2.zero, bodyBold: true);
            if (i == 3)
                ReferenceUiVisuals.Image(inner, "ResultDivider", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-112, y + 6), new Vector2(112, y + 8), ReferenceUiVisuals.Border);
        }

        returnButton = existingReturn;
        var buttonRt = (RectTransform)existingReturn.transform;
        buttonRt.anchorMin = buttonRt.anchorMax = new Vector2(0.5f, 0f);
        buttonRt.pivot = new Vector2(0.5f, 0f);
        buttonRt.sizeDelta = new Vector2(236, 56);
        buttonRt.anchoredPosition = new Vector2(0, campaign ? 126 : 70);
        ReferenceUiVisuals.ApplyButtonSkin(existingReturn, true);
        var text = existingReturn.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = BattleSession.IsCampaign ? "BÖLGEYE DÖN" : "ÜSSE DÖN";
            text.fontSize = 18;
            text.color = ReferenceUiVisuals.Hex(0x2A1A08);
            if (titleFont != null) text.font = TMP_FontAsset.CreateFontAsset(titleFont);
        }
        if (BattleSession.IsCampaign)
        {
            campaignStars = new Image[3];
            for (int i = 0; i < campaignStars.Length; i++)
            {
                campaignStars[i] = ReferenceUiVisuals.Image(inner, "VictoryStar" + i,
                    new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-51 + i * 36, -238), new Vector2(-21 + i * 36, -208),
                    ReferenceUiVisuals.Border);
                campaignStars[i].sprite = CampaignSceneSetup.GetStarSprite();
            }
            starReason = ReferenceUiVisuals.Text(inner, "VictoryStarReason", "", 13,
                ReferenceUiVisuals.Muted, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-120, -276), new Vector2(120, -241),
                bodyBold: true);
            retryButton = ReferenceUiVisuals.Button(inner, "RetryCampaignBattle", "TEKRAR OYNA", false,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-118, 66), new Vector2(118, 118), 17);
            retryButton.onClick.AddListener(() => GameManager.Instance.StartBattle());
        }
    }

    /// <summary>
    /// Kaçan yıldızların sebebi. Kural BattleManager ile aynı: can oranı %50'nin üstündeyse +1.
    /// </summary>
    private static string StarReasonText()
    {
        float baseHealth = FindFirstObjectByType<MainBaseController>()?.HealthPercent ?? 0f;
        float heroHealth = FindFirstObjectByType<HeroController>()?.HealthPercent ?? 0f;
        bool baseStar = baseHealth > 0.5f, heroStar = heroHealth > 0.5f;
        if (baseStar && heroStar) return "<color=#F0C24A>Kusursuz zafer!</color>";
        string Part(string name, float value, bool ok) => ok
            ? $"<color=#7BC67B>{name} %{Mathf.RoundToInt(value * 100f)}</color>"
            : $"<color=#E07A5F>{name} %{Mathf.RoundToInt(value * 100f)}</color>";
        return $"{Part("Ana Üs", baseHealth, baseStar)}  ·  {Part("Savaşçı", heroHealth, heroStar)}\n" +
               "Her biri %50'nin üstünde kalırsa +1 yıldız";
    }

    private const float FrameFillInset = 0.55f;

    /// <summary>
    /// Süslü çerçeve: üst/alt şeritler köşeleriyle birlikte panel genişliğine oranlı
    /// oturur, yan sütunlar uzatılmak yerine döşenir. Koyu dolgu çerçevenin içinde kalır.
    /// </summary>
    private static void BuildOrnateFrame(RectTransform inner, float panelWidth)
    {
        var frameSprites = Resources.LoadAll<Sprite>("Sprites/Intro/intro_button");
        if (frameSprites.Length == 0)
        {
            ReferenceUiVisuals.Image(inner, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                ReferenceUiVisuals.Panel).transform.SetAsFirstSibling();
            inner.gameObject.AddComponent<Outline>().effectColor = ReferenceUiVisuals.Border;
            return;
        }
        Sprite source = frameSprites[0];
        Rect rect = source.rect;
        float scale = panelWidth / rect.width;              // kaynak piksel → UI birimi
        float strip = rect.height * 0.29f;
        float sideWidth = rect.width * 0.16f;
        float stripUi = strip * scale;
        float sideUi = sideWidth * scale;

        var fill = ReferenceUiVisuals.Image(inner, "Fill", Vector2.zero, Vector2.one,
            new Vector2(sideUi * FrameFillInset, stripUi * FrameFillInset),
            new Vector2(-sideUi * FrameFillInset, -stripUi * FrameFillInset), ReferenceUiVisuals.Panel);
        fill.transform.SetAsFirstSibling();

        Image Piece(string name, Rect crop, Vector2 min, Vector2 max, Vector2 low, Vector2 high, bool tiled)
        {
            var image = ReferenceUiVisuals.Image(inner, name, min, max, low, high, Color.white);
            image.sprite = Sprite.Create(source.texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            if (tiled)
            {
                image.type = Image.Type.Tiled;
                // Döşeme, üst/alt şeritle aynı ölçekte tekrar etsin.
                image.pixelsPerUnitMultiplier = 1f / scale *
                    (100f / Mathf.Max(1f, source.pixelsPerUnit));
            }
            return image;
        }

        float sideHeight = rect.height - strip * 2f;
        Piece("OrnateLeft", new Rect(rect.x, rect.y + strip, sideWidth, sideHeight),
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, stripUi - 1), new Vector2(sideUi, -stripUi + 1), true);
        Piece("OrnateRight", new Rect(rect.xMax - sideWidth, rect.y + strip, sideWidth, sideHeight),
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(-sideUi, stripUi - 1), new Vector2(0, -stripUi + 1), true);
        Piece("OrnateHeader", new Rect(rect.x, rect.yMax - strip, rect.width, strip),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -stripUi), Vector2.zero, false);
        Piece("OrnateFooter", new Rect(rect.x, rect.y, rect.width, strip),
            new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, stripUi), false);
    }

    public void Present(bool won, int enemyGold, int bonus, int buildCost)
    {
        title.text = won ? "ZAFER!" : "YENİLGİ!";
        title.color = won ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Red;
        if (BattleSession.IsCampaign)
        {
            if (returnButton != null)
            {
                var label = returnButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = "BÖLGEYE DÖN";
            }
            if (retryButton != null)
            {
                var label = retryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = won ? "TEKRAR OYNA" : "TEKRAR DENE";
            }
            if (campaignStars != null)
                for (int i = 0; i < campaignStars.Length; i++)
                {
                    campaignStars[i].gameObject.SetActive(won);
                    campaignStars[i].color = won && BattleManager.Instance != null
                        && i < BattleManager.Instance.LastStars ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Hex(0x4A3C28);
                }
            if (starReason != null)
            {
                starReason.gameObject.SetActive(won);
                if (won) starReason.text = StarReasonText();
            }
            if (returnButton != null)
            {
                ReferenceUiVisuals.ApplyButtonSkin(returnButton, won);
                var label = returnButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.color = won ? ReferenceUiVisuals.Hex(0x2A1A08) : ReferenceUiVisuals.Light;
            }
            if (retryButton != null) ReferenceUiVisuals.ApplyButtonSkin(retryButton, !won);
        }
        if (rows.Length > 1) rows[1].gameObject.SetActive(won);
        values[0].text = enemyGold.ToString();
        values[1].text = "+" + (won ? bonus : 0);
        values[2].text = "−" + buildCost;
        int transfer = won ? enemyGold + bonus - buildCost : 0;
        values[3].text = "0";
        panel.localScale = Vector3.one * 0.8f;
        panel.DOScale(1, 0.25f).SetEase(Ease.OutBack);
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].alpha = 0;
            rows[i].DOFade(1, 0.2f).SetDelay(0.25f + i * 0.4f);
        }
        int shown = 0;
        DOTween.To(() => shown, value => { shown = value; values[3].text = shown.ToString(); }, transfer, 0.5f)
            .SetDelay(1.45f).SetEase(Ease.OutQuad);
    }
}
