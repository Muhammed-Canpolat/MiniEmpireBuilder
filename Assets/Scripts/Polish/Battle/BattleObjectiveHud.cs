using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Savaş görev göstergeleri: "Zamana karşı" süre sayacı, "Madeni koru" hatırlatması ve
/// düşen savaşçının dirilme geri sayımı. Savaş kararlarını değiştirmez, yalnız okur.
/// </summary>
public sealed class BattleObjectiveHud : MonoBehaviour
{
    private TextMeshProUGUI timerText;
    private Image timerPill;
    private TextMeshProUGUI respawnText;
    private CanvasGroup respawnGroup;
    private HeroController hero;
    private int lastWholeSecond = -1;
    private float stackBottom;

    private void Start()
    {
        var canvasObject = new GameObject("BattleObjectiveCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        var battle = BattleManager.Instance;
        bool timed = battle != null && battle.HasTimeLimit;
        bool mine = BattleSession.IsCampaign && BattleSession.Level?.kind == CampaignLevelKind.DefendMine;
        if (timed || mine)
        {
            timerPill = ReferenceUiVisuals.Image(safe, "ObjectivePill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-82f, -140f), new Vector2(82f, -110f), new Color(0.12f, 0.09f, 0.06f, 0.88f));
            timerPill.sprite = ReferenceUiVisuals.PillSprite();
            timerPill.type = Image.Type.Sliced;
            var outline = timerPill.gameObject.AddComponent<Outline>();
            outline.effectColor = timed ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Hex(0xE8B04A);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            timerText = ReferenceUiVisuals.Text(timerPill.transform, "ObjectiveText",
                timed ? FormatTime(BattleManager.TimeTrialSeconds) : L.T("MADENİ KORU", "DEFEND THE MINE"), 16, ReferenceUiVisuals.Gold,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f), true);
        }

        // Bu savaşta açık kartlar: küçük bir pul (ör. "KART +%10: Hasar · Can").
        var cards = new System.Collections.Generic.List<string>();
        for (int i = 0; i < BattleCards.Count; i++)
            if (BattleCards.ActiveInBattle((BattleCard)i)) cards.Add(BattleCards.Name((BattleCard)i));
        if (cards.Count > 0)
        {
            float top = timed || mine ? -144f : -110f;
            var cardPill = ReferenceUiVisuals.Image(safe, "ActiveCardsPill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-110f, top - 24f), new Vector2(110f, top), new Color(0.2f, 0.4f, 0.18f, 0.9f));
            cardPill.sprite = ReferenceUiVisuals.SolidPillSprite();
            cardPill.type = Image.Type.Sliced;
            ReferenceUiVisuals.Text(cardPill.transform, "ActiveCardsText",
                L.T($"KART +%{BattleCards.ActiveBattlePercent}: ", $"CARDS +{BattleCards.ActiveBattlePercent}%: ") + string.Join(" · ", cards), 12, ReferenceUiVisuals.Light,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            stackBottom = -(top - 24f);
        }
        else if (timed || mine) stackBottom = 140f;

        // Dalga duyurusu ("12 düşman yaklaşıyor") ve geri sayım bu pullarla aynı yüksekliğe denk geliyordu:
        // pul varsa ikisi de pulların altına kayar.
        if (stackBottom > 0f) StartCoroutine(ShiftWaveNotices(stackBottom + 6f));

        var respawnBox = ReferenceUiVisuals.Image(safe, "RespawnNotice", new Vector2(0.12f, 0.56f),
            new Vector2(0.88f, 0.56f), new Vector2(0f, -26f), new Vector2(0f, 26f),
            new Color(0.1f, 0.07f, 0.05f, 0.85f));
        respawnBox.sprite = ReferenceUiVisuals.PillSprite();
        respawnBox.type = Image.Type.Sliced;
        respawnGroup = respawnBox.gameObject.AddComponent<CanvasGroup>();
        respawnGroup.alpha = 0f;
        respawnText = ReferenceUiVisuals.Text(respawnBox.transform, "RespawnText", "", 17, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f), true);

        // Elmasla hemen kalk (onaysız: küçük bedel, savaş akarken hızlı olmalı).
        respawnButton = ReferenceUiVisuals.Button(respawnBox.transform, "RespawnNow", "", true,
            new Vector2(0.2f, 0f), new Vector2(0.8f, 0f), new Vector2(0f, -50f), new Vector2(0f, -8f), 15);
        GemConfirm.AddGemIcon(respawnButton.transform);
        respawnLabel = respawnButton.GetComponentInChildren<TextMeshProUGUI>();
        respawnButton.onClick.AddListener(RespawnNow);

        // Yalnız düğme dokunuş alır; pullar ve bildirim alttaki oyunu engellemez.
        foreach (var graphic in canvasObject.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        var buttonImage = respawnButton.GetComponent<Image>();
        if (buttonImage != null) buttonImage.raycastTarget = true;
        respawnGroup.blocksRaycasts = false;
    }

    private Button respawnButton;
    private TextMeshProUGUI respawnLabel;

    private void RespawnNow()
    {
        var gm = GameManager.Instance;
        if (hero == null || !hero.IsDowned || gm == null) return;
        if (!gm.SpendGems(GemEconomy.InstantRespawnCost))
        {
            GemShopUI.Open();
            return;
        }
        hero.RespawnNow();
        AudioManager.Instance?.PlaySfx("levelUp");
    }

    /// <summary>
    /// HUD kurulumu (BattleHudRedesign) bitince "IncomingChip" ve "NextWaveCountdown"ı, üst kenarları
    /// <paramref name="clearFromTop"/> biriminin altına düşecek kadar aşağı kaydırır.
    /// </summary>
    private IEnumerator ShiftWaveNotices(float clearFromTop)
    {
        yield return null;
        yield return null;
        RectTransform incoming = null, countdown = null;
        foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rt.name == "IncomingChip") incoming = rt;
            else if (rt.name == "NextWaveCountdown") countdown = rt;
        }
        if (incoming == null) yield break;
        var parent = incoming.parent as RectTransform;
        if (parent == null) yield break;
        float incomingTop = parent.rect.height * (1f - incoming.anchorMax.y) - incoming.offsetMax.y;
        float shift = clearFromTop - incomingTop;
        if (shift <= 0f) yield break;
        foreach (var rt in new[] { incoming, countdown })
        {
            if (rt == null) continue;
            rt.offsetMin -= new Vector2(0f, shift);
            rt.offsetMax -= new Vector2(0f, shift);
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return L.T("SÜRE ", "TIME ") + $"{total / 60}:{total % 60:00}";
    }

    private void Update()
    {
        var battle = BattleManager.Instance;
        if (battle == null) return;

        if (timerText != null && battle.HasTimeLimit)
        {
            float remaining = battle.TimeRemaining;
            timerText.text = FormatTime(remaining);
            bool urgent = remaining <= 30f;
            timerText.color = urgent ? ReferenceUiVisuals.Red : ReferenceUiVisuals.Gold;
            int whole = Mathf.CeilToInt(remaining);
            if (urgent && whole != lastWholeSecond && battle.IsBattleActive)
            {
                lastWholeSecond = whole;
                timerPill.transform.DOKill(true);
                timerPill.transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 4, 0.5f);
            }
        }
        else if (timerText != null && battle.DefendedMine != null)
        {
            timerText.text = L.T($"MADEN %{Mathf.CeilToInt(battle.DefendedMine.HealthPercent * 100f)}", $"MINE {Mathf.CeilToInt(battle.DefendedMine.HealthPercent * 100f)}%");
            timerText.color = battle.DefendedMine.HealthPercent < 0.35f ? ReferenceUiVisuals.Red : ReferenceUiVisuals.Gold;
        }

        if (hero == null) hero = FindFirstObjectByType<HeroController>();
        bool downed = hero != null && hero.IsDowned && battle.IsBattleActive;
        respawnGroup.alpha = Mathf.MoveTowards(respawnGroup.alpha, downed ? 1f : 0f, Time.deltaTime * 4f);
        respawnGroup.blocksRaycasts = downed;
        if (downed)
        {
            FirstTimeTips.Show("respawn", L.T(
                "Savaşçın düştü ama savaş sürüyor: birkaç saniye sonra Ana Üs'ün önünde kalkar. Yenilgi yalnız Ana Üs yıkılınca olur.",
                "Your warrior fell but the battle goes on: they rise again at the Main Base in a few seconds. You only lose if the Main Base falls."));
            respawnText.text = L.T($"Savaşçı düştü · {Mathf.CeilToInt(hero.RespawnRemaining)} sn sonra üste döner", $"Warrior down · back at base in {Mathf.CeilToInt(hero.RespawnRemaining)} s");
            bool enough = GameManager.Instance != null && GameManager.Instance.Gems >= GemEconomy.InstantRespawnCost;
            respawnLabel.text = enough ? L.T($"   HEMEN KALK · {GemEconomy.InstantRespawnCost}", $"   RISE NOW · {GemEconomy.InstantRespawnCost}") : L.T("   ELMAS YETMİYOR", "   NOT ENOUGH GEMS");
        }
    }
}
