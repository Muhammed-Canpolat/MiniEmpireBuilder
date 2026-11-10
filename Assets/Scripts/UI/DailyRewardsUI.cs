using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Üs'te 7 günlük giriş takvimi: solda takvim düğmesi (alınacak ödül varsa kırmızı nokta)
/// ve günde bir kez kendiliğinden açılan pencere. Mantık <see cref="DailyRewards"/>'ta.
/// </summary>
public sealed class DailyRewardsUI : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static readonly Color InkMuted = ReferenceUiVisuals.Hex(0x6B5236);

    private RectTransform safe;
    private GameObject dot;
    private GameObject popup;
    private TextMeshProUGUI resultText;

    private IEnumerator Start()
    {
        var canvasObject = new GameObject("DailyRewardsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safe = ReferenceUiVisuals.SafeArea(canvas.transform);
        CreateSideButton();
        DailyRewards.OnChanged += RefreshDot;
        RefreshDot();
        if (GemRewards.ClaimRoyalDaily())
            GemActions.Toast(L.T($"Kraliyet Üyeliği: +{GemEconomy.RoyalDailyGems} elmas",
                $"Royal Membership: +{GemEconomy.RoyalDailyGems} gems"));

        // İlk açılış rehberi bittiyse ve bugün alınmadıysa pencere kendiliğinden açılır.
        yield return new WaitForSeconds(1.2f);
        var data = GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
        // "Sen yokken" penceresi açıksa önce o kapanır.
        while (AwayReportUI.IsOpen) yield return null;
        if (data != null && data.firstBaseGuideDone && DailyRewards.CanClaim) Open();
    }

    private void OnDestroy() => DailyRewards.OnChanged -= RefreshDot;

    private void RefreshDot()
    {
        if (dot != null) dot.SetActive(DailyRewards.CanClaim);
    }

    private void CreateSideButton()
    {
        var anchor = new Vector2(0f, 0.72f);
        var button = Image(safe, "DailyButton", anchor, anchor, new Vector2(8, -30), new Vector2(64, 26), "ui_paper", 16f);
        button.raycastTarget = true;
        Image(button.transform, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-19, -17), new Vector2(19, 21), "ui_icon_calendar", 0f).preserveAspect = true;
        var label = ReferenceUiVisuals.Text(button.transform, "Label", L.T("GÜNLÜK", "DAILY"), 10, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, 14), true);
        label.raycastTarget = false;
        var dotImage = ReferenceUiVisuals.Image(button.transform, "Dot", Vector2.one, Vector2.one,
            new Vector2(-14, -14), new Vector2(2, 2), ReferenceUiVisuals.Red);
        dotImage.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        dot = dotImage.gameObject;
        dot.transform.DOScale(1.2f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetLink(dot);
        var uiButton = button.gameObject.AddComponent<Button>();
        uiButton.targetGraphic = button;
        uiButton.onClick.AddListener(Open);
    }

    private static Image Image(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high,
        string art, float border)
    {
        var image = ReferenceUiVisuals.Image(parent, name, min, max, low, high, Color.white);
        image.sprite = art == "gem" ? GemIcon.Sprite : CampaignSceneSetup.Art(art, border);
        image.type = border > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        return image;
    }

    // ==================== PENCERE ====================

    public void Open()
    {
        if (popup != null) Destroy(popup);
        var blocker = ReferenceUiVisuals.Image(safe, "DailyRewardsPopup", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.62f));
        blocker.raycastTarget = true;
        popup = blocker.gameObject;
        var close = popup.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(Close);

        var center = new Vector2(0.5f, 0.5f);
        var panel = Image(popup.transform, "Panel", center, center, new Vector2(-172, -250), new Vector2(172, 250),
            "ui_paper", 22f);
        panel.raycastTarget = true; // panele dokunmak pencereyi kapatmasın
        panel.transform.localScale = Vector3.one * 0.85f;
        panel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(panel.gameObject);

        Text(panel.transform, "Title", L.T("GÜNLÜK ÖDÜL", "DAILY REWARD"), 22, Ink, true, new Vector2(0, -52), new Vector2(0, -16));
        string sub = DailyRewards.StreakBroken
            ? L.T("Bir gün kaçtı: takvim 1. güne döndü.", "You missed a day: the calendar restarted.")
            : L.T("Her gün gir, 7. günde elmas ve sandık seni bekliyor. Bir gün kaçarsa takvim başa döner.", "Log in every day: gems and a chest await on day 7. Miss a day and it restarts.");
        Text(panel.transform, "Subtitle", sub, 12, InkMuted, false, new Vector2(0, -92), new Vector2(0, -52));

        int claimed = DailyRewards.ClaimedInCycle;
        int today = DailyRewards.TodayDay;
        bool canClaim = DailyRewards.CanClaim;
        for (int day = 1; day <= DailyRewards.Days; day++)
        {
            bool done = day <= claimed;
            bool isToday = day == today && canClaim;
            CreateTile(panel.transform, day, done, isToday);
        }

        resultText = Text(panel.transform, "Result", "", 13, ReferenceUiVisuals.Hex(0x2F7A3A), true,
            new Vector2(0, -400), new Vector2(0, -376));

        // Tek gün kaçtıysa seri elmasla kurtarılabilir (almadan önce sorulur).
        if (DailyRewards.CanSaveStreak)
        {
            var save = ReferenceUiVisuals.Button(panel.transform, "SaveStreak",
                L.T($"   SERİYİ KURTAR · {GemEconomy.StreakSaveCost}", $"   SAVE STREAK · {GemEconomy.StreakSaveCost}"), false,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-120, -400), new Vector2(120, -356), 15);
            GemConfirm.AddGemIcon(save.transform);
            int keep = GameManager.Instance.PlayerData.dailyStreak;
            save.onClick.AddListener(() => GemConfirm.Show(L.T("SERİYİ KURTAR", "SAVE STREAK"),
                L.T($"Dün giriş yapmadın. Seriyi korursan bugün {keep + 1}. günün ödülünü alırsın.", $"You missed yesterday. Save your streak to claim day {keep + 1} today."),
                GemEconomy.StreakSaveCost, L.T("KURTAR", "SAVE"), () =>
                {
                    if (!DailyRewards.SaveStreak()) return;
                    AudioManager.Instance?.PlaySfx("levelUp");
                    Open();
                }));
        }

        var claim = ReferenceUiVisuals.Button(panel.transform, "ClaimDaily",
            canClaim ? L.T($"{today}. GÜNÜ AL", $"CLAIM DAY {today}") : L.T($"YARIN: {(today % DailyRewards.Days) + 1}. GÜN", $"TOMORROW: DAY {(today % DailyRewards.Days) + 1}"), canClaim,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 18), new Vector2(-22, 70), 18);
        claim.interactable = canClaim;
        claim.onClick.AddListener(() => Claim(panel.transform));

        var closeRect = ReferenceUiVisuals.Rect(panel.transform, "Close", Vector2.one, Vector2.one,
            new Vector2(-46, -46), new Vector2(-8, -8));
        var closeImage = closeRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close);
        Text(closeRect, "X", "X", 16, Color.white, true, Vector2.zero, Vector2.zero, true);
    }

    private void Close()
    {
        if (popup != null) Destroy(popup);
        popup = null;
    }

    /// <summary>1-4. günler üst sırada, 5-7 alt sırada (7. gün geniş).</summary>
    private void CreateTile(Transform panel, int day, bool done, bool isToday)
    {
        var reward = DailyRewards.RewardFor(day);
        bool top = day <= 4;
        float width = 70f, height = 112f, gap = 8f;
        float rowWidth = top ? 4 * width + 3 * gap : 2 * width + (width * 2 + gap) + 2 * gap;
        float x = -rowWidth / 2f;
        if (top) x += (day - 1) * (width + gap);
        else x += (day - 5) * (width + gap);
        float w = day == 7 ? width * 2 + gap : width;
        float y = top ? -104f : -232f;
        var rect = ReferenceUiVisuals.Rect(panel, "Day" + day, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(x, y - height), new Vector2(x + w, y));
        var bg = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        // Beyaz hap: renk doğrudan Image.color'dan gelir (PillSprite koyu zemin taşır, hepsi siyah görünürdü).
        bg.sprite = ReferenceUiVisuals.SolidPillSprite();
        bg.type = UnityEngine.UI.Image.Type.Sliced;
        bg.color = done ? ReferenceUiVisuals.Hex(0x4E7A3A) : isToday ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Hex(0x5A4127);
        var edge = rect.gameObject.AddComponent<Outline>();
        edge.effectColor = isToday ? ReferenceUiVisuals.Hex(0xFFF2B0) : ReferenceUiVisuals.Hex(0x2A1C10);
        edge.effectDistance = new Vector2(isToday ? 2.5f : 1.5f, isToday ? -2.5f : -1.5f);
        Color text = isToday ? Ink : ReferenceUiVisuals.Light;
        Text(rect, "Day", L.T($"GÜN {day}", $"DAY {day}"), 12, text, true, new Vector2(0, -24), new Vector2(0, -4));
        if (reward.kind == DailyRewardKind.GemsChest)
        {
            Image(rect, "Gem", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-52, -14), new Vector2(-14, 24),
                "gem", 0f).preserveAspect = true;
            Image(rect, "Coin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, -14), new Vector2(26, 18),
                "ui_icon_coin", 0f).preserveAspect = true;
            Image(rect, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26, -12), new Vector2(56, 18),
                "ui_icon_sword", 0f).preserveAspect = true;
        }
        else
        {
            Image(rect, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-19, -16), new Vector2(19, 22),
                reward.icon, 0f).preserveAspect = true;
        }
        string amount = reward.kind == DailyRewardKind.GemsChest
            ? L.T($"20 elmas · {reward.chestGold} altın · 1 kart", $"20 gems · {reward.chestGold} gold · 1 card") : reward.title;
        Text(rect, "Amount", amount, 10.5f, text, false, new Vector2(0, 4), new Vector2(0, 34), false, true);
        if (done)
        {
            // Alınan gün: simge soluklaşır, üstüne "ALINDI" damgası (✓ yazı tipinde olmayabilir).
            foreach (var icon in rect.GetComponentsInChildren<UnityEngine.UI.Image>())
                if (icon != bg) icon.color = new Color(1f, 1f, 1f, 0.45f);
            var stamp = ReferenceUiVisuals.Image(rect, "ClaimedStamp", new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.5f),
                new Vector2(0, -8), new Vector2(0, 14), ReferenceUiVisuals.Hex(0x2F5A24));
            stamp.sprite = ReferenceUiVisuals.SolidPillSprite();
            stamp.type = UnityEngine.UI.Image.Type.Sliced;
            Text(stamp.transform, "Check", L.T("ALINDI", "CLAIMED"), 12, ReferenceUiVisuals.Hex(0xE8F5C8), true,
                Vector2.zero, Vector2.zero, true);
        }
        if (isToday)
            rect.DOScale(1.06f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(rect.gameObject);
    }

    private void Claim(Transform panel)
    {
        var reward = DailyRewards.Claim();
        if (reward == null) return;
        var r = reward.Value;
        string message;
        switch (r.kind)
        {
            case DailyRewardKind.Gold: message = L.T($"+{r.amount} altın!", $"+{r.amount} gold!"); break;
            case DailyRewardKind.BuildSpeedup:
                message = GameManager.Instance.PlayerData.pendingSpeedupMinutes > 0
                    ? L.T("30 dk hızlandırma sonraki yükseltmene uygulanacak", "The 30 min speed-up will apply to your next upgrade")
                    : L.T("Süren yükseltme 30 dk kısaldı!", "Your upgrade is 30 min shorter!");
                break;
            case DailyRewardKind.CardToken: message = L.T("+1 savaş kartı jetonu!", "+1 battle card token!"); break;
            case DailyRewardKind.MineBoost: message = L.T("Maden 1 saat 2 kat üretiyor!", "Mine produces 2x for 1 hour!"); break;
            case DailyRewardKind.UnlimitedLives: message = L.T("2 saat sınırsız can!", "2 hours of unlimited lives!"); break;
            default: message = L.T($"+20 elmas, +{r.chestGold} altın, +1 kart jetonu!", $"+20 gems, +{r.chestGold} gold, +1 card token!"); break;
        }
        AudioManager.Instance?.PlaySfx("buildingPlace");
        Open();
        if (resultText != null)
        {
            resultText.text = message;
            resultText.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f).SetLink(resultText.gameObject);
        }
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color, bool title,
        Vector2 low, Vector2 high, bool full = false, bool bottom = false)
    {
        Vector2 min = full ? Vector2.zero : bottom ? Vector2.zero : new Vector2(0, 1);
        Vector2 max = full ? Vector2.one : bottom ? new Vector2(1, 0) : Vector2.one;
        var label = ReferenceUiVisuals.Text(parent, name, value, size, color, TextAlignmentOptions.Center,
            min, max, new Vector2(6 + low.x, low.y), new Vector2(-6 + high.x, high.y), title, !title);
        label.raycastTarget = false;
        return label;
    }
}
