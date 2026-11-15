using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Savaş görev göstergeleri: etkin kart pulu ve (süre/maden göstergesi BattleTopBar kartına taşındı)
/// düşen savaşçının dirilme geri sayımı; üst barı (BattleTopBar) kurar. Savaş kararlarını değiştirmez.
/// </summary>
public sealed class BattleObjectiveHud : MonoBehaviour
{
    private TextMeshProUGUI respawnText;
    private CanvasGroup respawnGroup;
    private HeroController hero;

    private void Start()
    {
        var canvasObject = new GameObject("BattleObjectiveCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        // 8. oturum (D98): süre / maden göstergesi artık BattleTopBar'ın hedef kartında; bar burada kurulur.
        if (BattleTopBar.Instance == null) new GameObject("BattleTopBar").AddComponent<BattleTopBar>();
        BattleTopBar.CardsPillStack = 0f;
        bool hasCard = BattleTopBar.KindForBattle() != BattleTopBar.ObjectiveKind.None;

        // Bu savaşta açık kartlar: küçük bir pul (ör. "KART +%10: Hasar · Can").
        var cards = new System.Collections.Generic.List<string>();
        for (int i = 0; i < BattleCards.Count; i++)
            if (BattleCards.ActiveInBattle((BattleCard)i)) cards.Add(BattleCards.Name((BattleCard)i));
        // "+%10" yalnız temel kart etkinse; özel (Atölye) kartların etkisi adında/ayrıntısındadır.
        bool basicActive = false;
        for (int i = 0; i < BattleCards.BasicCount; i++) basicActive |= BattleCards.ActiveInBattle((BattleCard)i);
        if (cards.Count > 0)
        {
            float top = -(BattleTopBar.BarHeight + BattleTopBar.Gap + (hasCard ? BattleTopBar.CardHeight + BattleTopBar.Gap : 0f));
            var cardPill = ReferenceUiVisuals.Image(safe, "ActiveCardsPill", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-140f, top - 24f), new Vector2(140f, top), new Color(0.2f, 0.4f, 0.18f, 0.9f));
            cardPill.sprite = ReferenceUiVisuals.SolidPillSprite();
            cardPill.type = Image.Type.Sliced;
            ReferenceUiVisuals.Text(cardPill.transform, "ActiveCardsText",
                (basicActive ? L.T($"KART +%{BattleCards.ActiveBattlePercent}: ", $"CARDS +{BattleCards.ActiveBattlePercent}%: ") : L.T("KART: ", "CARDS: ")) + string.Join(" · ", cards), 12, ReferenceUiVisuals.Light,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            BattleTopBar.CardsPillStack = 28f;
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

    private void Update()
    {
        var battle = BattleManager.Instance;
        if (battle == null) return;

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
