using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana Üs'te günlük görevler: takvim düğmesinin altında "GÖREV" düğmesi (alınacak ödül varsa
/// kırmızı nokta) ve 3 görev + bonus penceresi. Mantık <see cref="DailyQuests"/>'ta.
/// </summary>
public sealed class DailyQuestsUI : MonoBehaviour
{
    private static readonly Color Ink = ReferenceUiVisuals.Hex(0x3B2A1A);
    private static readonly Color InkMuted = ReferenceUiVisuals.Hex(0x6B5236);

    private RectTransform safe;
    private GameObject dot;
    private GameObject popup;

    private void Start()
    {
        var canvasObject = new GameObject("DailyQuestsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 45;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safe = ReferenceUiVisuals.SafeArea(canvas.transform);
        CreateSideButton();
        DailyQuests.OnChanged += RefreshDot;
        RefreshDot();
    }

    private void OnDestroy() => DailyQuests.OnChanged -= RefreshDot;

    private void RefreshDot()
    {
        if (dot != null) dot.SetActive(DailyQuests.HasClaimable);
        if (popup != null) Open();
    }

    private void CreateSideButton()
    {
        var anchor = new Vector2(0f, 0.64f);
        var button = Paper(safe, "QuestsButton", anchor, anchor, new Vector2(8, -30), new Vector2(64, 26), 16f);
        button.raycastTarget = true;
        var icon = ReferenceUiVisuals.Image(button.transform, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-17, -15), new Vector2(17, 21), Color.white);
        icon.sprite = CampaignSceneSetup.Art("ui_scroll");
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        ReferenceUiVisuals.Text(button.transform, "Label", L.T("GÖREV", "QUESTS"), 10, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, 14), true).raycastTarget = false;
        var dotImage = ReferenceUiVisuals.Image(button.transform, "Dot", Vector2.one, Vector2.one,
            new Vector2(-14, -14), new Vector2(2, 2), ReferenceUiVisuals.Red);
        dotImage.sprite = ReferenceUiVisuals.SolidPillSprite();
        dot = dotImage.gameObject;
        dot.transform.DOScale(1.2f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetLink(dot);
        var uiButton = button.gameObject.AddComponent<Button>();
        uiButton.targetGraphic = button;
        uiButton.onClick.AddListener(() =>
        {
            Open();
            FirstTimeTips.Show("quests", L.T("Her gün 3 yeni görev gelir. Üçünü de bitirirsen bonus elmas!",
                "3 new quests every day. Finish all three for bonus gems!"));
        });
    }

    private static Image Paper(Transform parent, string name, Vector2 min, Vector2 max, Vector2 low, Vector2 high, float border)
    {
        var image = ReferenceUiVisuals.Image(parent, name, min, max, low, high, Color.white);
        image.sprite = CampaignSceneSetup.Art("ui_paper", border);
        image.type = Image.Type.Sliced;
        return image;
    }

    private void Close()
    {
        if (popup != null) Destroy(popup);
        popup = null;
    }

    private void Open()
    {
        bool fresh = popup == null;
        if (popup != null) Destroy(popup);
        var blocker = ReferenceUiVisuals.Image(safe, "DailyQuestsPopup", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.62f));
        blocker.raycastTarget = true;
        popup = blocker.gameObject;
        var dim = popup.AddComponent<Button>();
        dim.transition = Selectable.Transition.None;
        dim.onClick.AddListener(Close);

        var center = new Vector2(0.5f, 0.5f);
        var panel = Paper(popup.transform, "Panel", center, center, new Vector2(-172, -230), new Vector2(172, 230), 22f);
        panel.raycastTarget = true;
        if (fresh)
        {
            panel.transform.localScale = Vector3.one * 0.85f;
            panel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(panel.gameObject);
        }
        var p = panel.transform;
        ReferenceUiVisuals.Text(p, "Title", L.T("GÜNLÜK GÖREVLER", "DAILY QUESTS"), 21, Ink, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(44, -54), new Vector2(-44, -16), true);
        ReferenceUiVisuals.Text(p, "Subtitle", L.T("Görevler her gün yenilenir.", "Quests refresh every day."), 12, InkMuted,
            TextAlignmentOptions.Center, new Vector2(0, 1), Vector2.one, new Vector2(20, -76), new Vector2(-20, -54), false, true);

        for (int i = 0; i < DailyQuests.Count; i++) CreateRow(p, i);

        // Bonus: üçü de alınınca +5 elmas.
        bool bonusReady = DailyQuests.AllClaimed && !DailyQuests.BonusClaimed;
        string bonusLabel = DailyQuests.BonusClaimed ? L.T("BONUS ALINDI", "BONUS CLAIMED")
            : L.T($"   ÜÇÜNÜ BİTİR: +{DailyQuests.BonusGems} ELMAS", $"   FINISH ALL: +{DailyQuests.BonusGems} GEMS");
        var bonus = ReferenceUiVisuals.Button(p, "Bonus", bonusLabel, bonusReady,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(22, 20), new Vector2(-22, 70), 16);
        if (!DailyQuests.BonusClaimed) GemConfirm.AddGemIcon(bonus.transform);
        bonus.interactable = bonusReady;
        bonus.onClick.AddListener(() =>
        {
            if (!DailyQuests.ClaimBonus()) return;
            AudioManager.Instance?.PlaySfx("levelUp");
            GemActions.Toast(L.T($"Görev bonusu: +{DailyQuests.BonusGems} elmas!", $"Quest bonus: +{DailyQuests.BonusGems} gems!"));
        });

        var closeRect = ReferenceUiVisuals.Rect(p, "Close", Vector2.one, Vector2.one, new Vector2(-46, -46), new Vector2(-8, -8));
        var closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.sprite = CampaignSceneSetup.Art("ui_node_red");
        var closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close);
        ReferenceUiVisuals.Text(closeRect, "X", "X", 16, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
    }

    private void CreateRow(Transform panel, int index)
    {
        var quest = DailyQuests.Quest(index);
        int progress = DailyQuests.Progress(index);
        bool done = DailyQuests.IsDone(index), claimed = DailyQuests.IsClaimed(index);
        float top = 86f + index * 92f;
        var row = ReferenceUiVisuals.Rect(panel, "Quest" + index, new Vector2(0, 1), Vector2.one,
            new Vector2(16, -top - 84), new Vector2(-16, -top));
        var bg = row.gameObject.AddComponent<Image>();
        bg.sprite = ReferenceUiVisuals.SolidPillSprite();
        bg.type = Image.Type.Sliced;
        bg.color = claimed ? new Color(0.31f, 0.48f, 0.23f, 0.35f) : new Color(0.42f, 0.32f, 0.2f, 0.22f);

        ReferenceUiVisuals.Text(row, "Title", DailyQuests.Title(index), 14, Ink, TextAlignmentOptions.Left,
            new Vector2(0, 0.55f), new Vector2(0.68f, 1f), new Vector2(12, 0), new Vector2(0, -6), false, true);
        ReferenceUiVisuals.Text(row, "Reward", L.T("Ödül: ", "Reward: ") + DailyQuests.RewardText(index), 12, InkMuted,
            TextAlignmentOptions.Left, new Vector2(0, 0.32f), new Vector2(0.68f, 0.55f), new Vector2(12, 0), Vector2.zero, false, true);

        // İlerleme çubuğu.
        var track = ReferenceUiVisuals.Image(row, "Track", new Vector2(0, 0.1f), new Vector2(0.68f, 0.28f),
            new Vector2(12, 0), Vector2.zero, ReferenceUiVisuals.Hex(0x3B2A1A));
        track.sprite = ReferenceUiVisuals.SolidPillSprite();
        track.type = Image.Type.Sliced;
        float ratio = quest.target > 0 ? Mathf.Clamp01(progress / (float)quest.target) : 1f;
        var fill = ReferenceUiVisuals.Image(track.rectTransform, "Fill", Vector2.zero, new Vector2(Mathf.Max(0.06f, ratio), 1f),
            new Vector2(2, 2), new Vector2(-2, -2), done ? ReferenceUiVisuals.Hex(0x7BC67B) : ReferenceUiVisuals.Gold);
        fill.sprite = ReferenceUiVisuals.SolidPillSprite();
        fill.type = Image.Type.Sliced;
        ReferenceUiVisuals.Text(track.rectTransform, "Count", $"{progress}/{quest.target}", 10, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, true);

        string label = claimed ? L.T("ALINDI", "CLAIMED") : done ? L.T("AL", "CLAIM") : L.T("DEVAM", "IN PROGRESS");
        var button = ReferenceUiVisuals.Button(row, "Claim", label, done && !claimed,
            new Vector2(0.7f, 0.18f), new Vector2(0.98f, 0.82f), Vector2.zero, Vector2.zero, 13);
        button.interactable = done && !claimed;
        int captured = index;
        button.onClick.AddListener(() =>
        {
            string reward = DailyQuests.Claim(captured);
            if (reward == null) return;
            AudioManager.Instance?.PlaySfx("buildingPlace");
            GemActions.Toast("+" + reward);
        });
    }
}
