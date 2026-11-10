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

    private void Start()
    {
        var canvasObject = new GameObject("BattleObjectiveCanvas", typeof(Canvas), typeof(CanvasScaler));
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
                new Vector2(-82f, -132f), new Vector2(82f, -102f), new Color(0.12f, 0.09f, 0.06f, 0.88f));
            timerPill.sprite = ReferenceUiVisuals.PillSprite();
            timerPill.type = Image.Type.Sliced;
            var outline = timerPill.gameObject.AddComponent<Outline>();
            outline.effectColor = timed ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Hex(0xE8B04A);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            timerText = ReferenceUiVisuals.Text(timerPill.transform, "ObjectiveText",
                timed ? FormatTime(BattleManager.TimeTrialSeconds) : "MADENİ KORU", 16, ReferenceUiVisuals.Gold,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f), true);
        }

        // Bu savaşta açık kartlar: küçük bir pul (ör. "KART +%10: Hasar · Can").
        var cards = new System.Collections.Generic.List<string>();
        for (int i = 0; i < BattleCards.Count; i++)
            if (BattleCards.ActiveInBattle((BattleCard)i)) cards.Add(BattleCards.Name((BattleCard)i));
        if (cards.Count > 0)
        {
            float top = timed || mine ? -136f : -102f;
            var cardPill = ReferenceUiVisuals.Image(safe, "ActiveCardsPill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-110f, top - 24f), new Vector2(110f, top), new Color(0.16f, 0.32f, 0.16f, 0.88f));
            cardPill.sprite = ReferenceUiVisuals.PillSprite();
            cardPill.type = Image.Type.Sliced;
            ReferenceUiVisuals.Text(cardPill.transform, "ActiveCardsText",
                $"KART +%{BattleCards.ActiveBattlePercent}: {string.Join(" · ", cards)}", 12, ReferenceUiVisuals.Light,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        }

        var respawnBox = ReferenceUiVisuals.Image(safe, "RespawnNotice", new Vector2(0.12f, 0.56f),
            new Vector2(0.88f, 0.56f), new Vector2(0f, -26f), new Vector2(0f, 26f),
            new Color(0.1f, 0.07f, 0.05f, 0.85f));
        respawnBox.sprite = ReferenceUiVisuals.PillSprite();
        respawnBox.type = Image.Type.Sliced;
        respawnGroup = respawnBox.gameObject.AddComponent<CanvasGroup>();
        respawnGroup.alpha = 0f;
        respawnText = ReferenceUiVisuals.Text(respawnBox.transform, "RespawnText", "", 17, ReferenceUiVisuals.Light,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f), true);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"SÜRE {total / 60}:{total % 60:00}";
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
            timerText.text = $"MADEN %{Mathf.CeilToInt(battle.DefendedMine.HealthPercent * 100f)}";
            timerText.color = battle.DefendedMine.HealthPercent < 0.35f ? ReferenceUiVisuals.Red : ReferenceUiVisuals.Gold;
        }

        if (hero == null) hero = FindFirstObjectByType<HeroController>();
        bool downed = hero != null && hero.IsDowned && battle.IsBattleActive;
        respawnGroup.alpha = Mathf.MoveTowards(respawnGroup.alpha, downed ? 1f : 0f, Time.deltaTime * 4f);
        if (downed)
            respawnText.text = $"Savaşçı düştü · {Mathf.CeilToInt(hero.RespawnRemaining)} sn sonra üste döner";
    }
}
