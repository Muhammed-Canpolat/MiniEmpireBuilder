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

    public void Initialize(RectTransform inner, TextMeshProUGUI existingTitle, TextMeshProUGUI existingReward, Button existingReturn)
    {
        var overlay = GetComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0.65f);
        var safe = ReferenceUiVisuals.SafeArea(transform);
        inner.SetParent(safe, false);
        panel = inner;
        inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
        inner.pivot = new Vector2(0.5f, 0.5f);
        inner.sizeDelta = new Vector2(350, BattleSession.IsCampaign ? 550 : 500);
        inner.anchoredPosition = Vector2.zero;
        inner.GetComponent<Image>().color = ReferenceUiVisuals.Panel;
        inner.gameObject.AddComponent<Outline>().effectColor = ReferenceUiVisuals.Border;
        var frameSprites = Resources.LoadAll<Sprite>("Sprites/Intro/intro_button");
        if (frameSprites.Length > 0)
        {
            Sprite source = frameSprites[0];
            var rect = source.rect;
            float strip = rect.height * 0.29f;
            var top = ReferenceUiVisuals.Image(inner, "OrnateHeader", new Vector2(0.5f, 1),
                new Vector2(0.5f, 1), new Vector2(-165, -62), new Vector2(165, 0), Color.white);
            top.sprite = Sprite.Create(source.texture,
                new Rect(rect.x, rect.yMax - strip, rect.width, strip), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            var bottom = ReferenceUiVisuals.Image(inner, "OrnateFooter", new Vector2(0.5f, 0),
                new Vector2(0.5f, 0), new Vector2(-165, 0), new Vector2(165, 62), Color.white);
            bottom.sprite = Sprite.Create(source.texture,
                new Rect(rect.x, rect.y, rect.width, strip), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            float sideWidth = rect.width * 0.16f;
            float sideHeight = rect.height - strip * 2f;
            var left = ReferenceUiVisuals.Image(inner, "OrnateLeft", new Vector2(0, 0),
                new Vector2(0, 1), new Vector2(0, 62), new Vector2(34, -62), Color.white);
            left.sprite = Sprite.Create(source.texture,
                new Rect(rect.x, rect.y + strip, sideWidth, sideHeight),
                new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            var right = ReferenceUiVisuals.Image(inner, "OrnateRight", new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(-34, 62), new Vector2(0, -62), Color.white);
            right.sprite = Sprite.Create(source.texture,
                new Rect(rect.xMax - sideWidth, rect.y + strip, sideWidth, sideHeight),
                new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        }
        ReferenceUiVisuals.Image(inner, "BottomRule", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-165, 5), new Vector2(165, 8), ReferenceUiVisuals.Border);

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
            float y = BattleSession.IsCampaign
                ? (i == 3 ? -366 : -260 - i * 38)
                : (i == 3 ? -330 : -224 - i * 38);
            var row = ReferenceUiVisuals.Rect(inner, "ResultRow" + i, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-135, y - 34), new Vector2(135, y));
            rows[i] = row.gameObject.AddComponent<CanvasGroup>();
            ReferenceUiVisuals.Text(row, "Label", labels[i], i == 3 ? 22 : 19,
                i == 3 ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Light, TextAlignmentOptions.Left,
                new Vector2(0, 0), new Vector2(0.72f, 1), Vector2.zero, Vector2.zero, bodyBold: true);
            values[i] = ReferenceUiVisuals.Text(row, "Value", "0", i == 3 ? 24 : 19,
                i == 3 ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Light, TextAlignmentOptions.Right,
                new Vector2(0.72f, 0), Vector2.one, Vector2.zero, Vector2.zero, bodyBold: true);
            if (i == 3)
                ReferenceUiVisuals.Image(inner, "ResultDivider", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-135, y + 3), new Vector2(135, y + 5), ReferenceUiVisuals.Border);
        }

        returnButton = existingReturn;
        var buttonRt = (RectTransform)existingReturn.transform;
        buttonRt.anchorMin = buttonRt.anchorMax = new Vector2(0.5f, 0f);
        buttonRt.pivot = new Vector2(0.5f, 0f);
        buttonRt.sizeDelta = new Vector2(270, 56);
        buttonRt.anchoredPosition = new Vector2(0, BattleSession.IsCampaign ? 78 : 70);
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
                    new Vector2(-42 + i * 30, -231), new Vector2(-18 + i * 30, -207),
                    ReferenceUiVisuals.Border);
                campaignStars[i].sprite = CampaignSceneSetup.GetStarSprite();
            }
            retryButton = ReferenceUiVisuals.Button(inner, "RetryCampaignBattle", "TEKRAR OYNA", false,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-135, 13), new Vector2(135, 66), 17);
            retryButton.onClick.AddListener(() => GameManager.Instance.StartBattle());
        }
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
                        && i < BattleManager.Instance.LastStars ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Border;
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
