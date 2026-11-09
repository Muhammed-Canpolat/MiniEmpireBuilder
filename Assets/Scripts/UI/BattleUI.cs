using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Savas sahnesi UI yonetimi.
/// </summary>
public class BattleUI : MonoBehaviour
{
    private TextMeshProUGUI waveText;
    private TextMeshProUGUI enemyCountText;
    private TextMeshProUGUI goldText;
    private TextMeshProUGUI levelText;

    private Slider heroHealthBar;
    private TextMeshProUGUI heroHealthText;
    private Slider baseHealthBar;
    private TextMeshProUGUI baseHealthText;

    private GameObject battleEndPanel;
    private TextMeshProUGUI resultText;
    private TextMeshProUGUI rewardText;
    private Button returnButton;

    private HeroController heroController;
    private MainBaseController mainBaseController;
    private bool refsReady;
    private bool subscribed;
    
    private int displayedGold = 0;
    private int lastReportedGold;
    private int grossBattleGold;
    private int battleBuildCost;
    private VictoryReferenceView victoryView;
    private Image waveProgressFill;
    private TextMeshProUGUI netGoldText;
    private Image[] waveSegments;
    private GameObject incomingChip;
    private GameObject bossTrack;
    private Image bossFill;
    private TextMeshProUGUI nextWaveCountdown;

    public void SetReferences(
        TextMeshProUGUI wave, TextMeshProUGUI enemy, TextMeshProUGUI gold, TextMeshProUGUI level,
        Slider heroBar, TextMeshProUGUI heroHp,
        Slider baseBar, TextMeshProUGUI baseHp,
        GameObject endPanel, TextMeshProUGUI result, TextMeshProUGUI reward,
        Button retBtn)
    {
        waveText = wave;
        enemyCountText = enemy;
        goldText = gold;
        levelText = level;
        heroHealthBar = heroBar;
        heroHealthText = heroHp;
        baseHealthBar = baseBar;
        baseHealthText = baseHp;
        battleEndPanel = endPanel;
        resultText = result;
        rewardText = reward;
        returnButton = retBtn;
        victoryView = endPanel != null ? endPanel.GetComponent<VictoryReferenceView>() : null;
        waveProgressFill = wave != null ? wave.transform.parent.Find("WaveTrack/WaveFill")?.GetComponent<Image>() : null;
        netGoldText = wave != null ? wave.transform.parent.parent.Find("BattleNetText")?.GetComponent<TextMeshProUGUI>() : null;
        incomingChip = wave != null ? wave.transform.parent.parent.Find("IncomingChip")?.gameObject : null;
        nextWaveCountdown = wave != null ? wave.transform.parent.parent.Find("NextWaveCountdown")?.GetComponent<TextMeshProUGUI>() : null;
        waveSegments = System.Array.Empty<Image>();
        if (goldText != null) goldText.text = "0";

        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnClicked);

        refsReady = true;
    }

    private void Update()
    {
        if (!refsReady)
            return;

        if (heroController == null)
            heroController = FindFirstObjectByType<HeroController>();
        if (mainBaseController == null)
            mainBaseController = FindFirstObjectByType<MainBaseController>();

        SubscribeToBattleManager();
        UpdateHealthBars();
        UpdateBossBar();
        if (nextWaveCountdown != null && BattleManager.Instance != null)
        {
            float remaining = BattleManager.Instance.NextWaveAt - Time.time;
            bool show = BattleManager.Instance.IsBattleActive && remaining > 0f;
            nextWaveCountdown.gameObject.SetActive(show);
            if (show) nextWaveCountdown.text = $"Sonraki dalga {Mathf.CeilToInt(remaining)}";
        }
    }

    private void SubscribeToBattleManager()
    {
        if (subscribed)
            return;

        BattleManager bm = BattleManager.Instance;
        if (bm == null)
            return;

        bm.OnWaveChanged += UpdateWaveDisplay;
        bm.OnEnemiesCountChanged += UpdateEnemyCount;
        bm.OnGoldEarnedChanged += UpdateGoldDisplay;
        bm.OnBattleEnded += ShowBattleResult;
        bm.OnWaveIncoming += ShowIncomingWave;
        subscribed = true;
        UpdateWaveDisplay(Mathf.Max(1, bm.CurrentWave), Mathf.Max(1, bm.TotalWaves));
        UpdateGoldDisplay(bm.TotalGoldEarned);

        if (levelText != null)
            levelText.text = BattleSession.IsCampaign && BattleSession.Region != null
                ? BattleSession.Level?.kind == CampaignLevelKind.Boss
                    ? $"BOSS · {UiStrings.Upper(BattleSession.Region.bossName)}"
                    : $"{BattleSession.Region.displayName} · Seviye {BattleSession.LevelNumber}"
                : $"SEVİYE {GameManager.Instance?.PlayerData?.currentBattleLevel ?? 1}";
        if (BattleSession.Level?.kind == CampaignLevelKind.Boss && levelText != null)
        {
            levelText.color = ReferenceUiVisuals.Red;
            var safe = levelText.transform.parent.parent;
            var track = ReferenceUiVisuals.Image(safe, "BossHealthTrack", new Vector2(0.07f, 0.875f),
                new Vector2(0.93f, 0.885f), Vector2.zero, Vector2.zero, ReferenceUiVisuals.Background);
            bossTrack = track.gameObject;
            bossFill = ReferenceUiVisuals.Image(track.transform, "BossHealthFill", Vector2.zero,
                Vector2.one, Vector2.zero, Vector2.zero, ReferenceUiVisuals.Red);
            bossTrack.SetActive(false);
        }
    }

    private void UpdateBossBar()
    {
        if (bossTrack == null) return;
        EnemyController boss = null;
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (EnemyTypeInfo.IsBoss(enemy.Type) && !enemy.IsDead) { boss = enemy; break; }
        bossTrack.SetActive(boss != null);
        if (bossFill != null && boss != null)
            bossFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(boss.HealthPercent), 1f);
    }

    private void UpdateHealthBars()
    {
        if (heroController != null && heroHealthBar != null)
        {
            heroHealthBar.value = heroController.HealthPercent;
            if (heroHealthText != null)
                heroHealthText.text = $"{heroController.CurrentHealth:F0}/{heroController.MaxHealth:F0}";
        }

        if (mainBaseController != null && baseHealthBar != null)
        {
            baseHealthBar.value = mainBaseController.HealthPercent;
            if (baseHealthText != null)
                baseHealthText.text = $"{mainBaseController.CurrentHealth:F0}/{mainBaseController.MaxHealth:F0}";
        }
    }

    private void UpdateWaveDisplay(int current, int total)
    {
        EnsureWaveSegments(total);
        if (waveText != null)
            waveText.text = $"DALGA {current}/{total}";
        if (waveProgressFill != null && total > 0)
            waveProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)current / total), 1f);
        if (waveSegments != null)
            for (int i = 0; i < waveSegments.Length; i++)
                if (waveSegments[i] != null)
                    waveSegments[i].color = i < current ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Hex(0x3A3020);
    }

    private void EnsureWaveSegments(int total)
    {
        total = Mathf.Max(1, total);
        if (waveSegments != null && waveSegments.Length == total) return;
        var track = waveText != null ? waveText.transform.parent.Find("WaveTrack") : null;
        if (track == null) return;
        for (int i = track.childCount - 1; i >= 0; i--)
            if (track.GetChild(i).name.StartsWith("Segment")) Destroy(track.GetChild(i).gameObject);

        waveSegments = new Image[total];
        for (int i = 0; i < total; i++)
        {
            var segment = new GameObject("Segment" + i, typeof(RectTransform), typeof(Image));
            segment.transform.SetParent(track, false);
            var rect = (RectTransform)segment.transform;
            rect.anchorMin = new Vector2((float)i / total, 0f);
            rect.anchorMax = new Vector2((float)(i + 1) / total, 1f);
            rect.offsetMin = new Vector2(i == 0 ? 0f : 2f, 0f);
            rect.offsetMax = new Vector2(i == total - 1 ? 0f : -2f, 0f);
            waveSegments[i] = segment.GetComponent<Image>();
            waveSegments[i].sprite = ReferenceUiVisuals.SolidPillSprite();
            waveSegments[i].type = Image.Type.Sliced;
        }
    }

    private void UpdateEnemyCount(int count)
    {
        if (enemyCountText != null)
            enemyCountText.text = $"{UiStrings.Enemy} {count}";
    }

    private void UpdateGoldDisplay(int gold)
    {
        int change = gold - lastReportedGold;
        if (change > 0) grossBattleGold += change;
        else if (change < 0) battleBuildCost -= change;
        lastReportedGold = gold;
        if (netGoldText != null)
            netGoldText.text = $"Şu anki net kazanç: {grossBattleGold} − {battleBuildCost} = {gold}";
        if (goldText != null)
        {
            DOTween.To(() => displayedGold, x => 
            {
                displayedGold = x;
                goldText.text = displayedGold.ToString();
            }, gold, 0.5f).SetEase(Ease.OutQuad);
            
            // Text scale animasyonu
            goldText.transform.DOKill(true);
            goldText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 1f);
        }
    }

    private void ShowBattleResult(bool won)
    {
        AudioManager.Instance?.PlaySfx(won ? "victory" : "defeat");
        if (battleEndPanel != null)
            battleEndPanel.SetActive(true);

        if (victoryView != null && BattleManager.Instance != null)
        {
            victoryView.Present(won, grossBattleGold, BattleManager.Instance.VictoryBonus, battleBuildCost);
            return;
        }

        if (resultText != null)
        {
            resultText.text = won ? "ZAFER!" : "YENİLGİ!";
            resultText.color = won ? Color.yellow : Color.red;
        }

        if (rewardText != null)
        {
            BattleManager bm = BattleManager.Instance;
            if (bm != null)
            {
                if (won)
                {
                    int total = bm.TotalGoldEarned + bm.VictoryBonus;
                    rewardText.text = $"Kazanılan Altın: {total} (Savaş: {bm.TotalGoldEarned} + Ödül: {bm.VictoryBonus})";
                }
                else
                {
                    rewardText.text = "Üssünü güçlendir ve tekrar dene!";
                }
            }
        }
    }

    private void OnReturnClicked()
    {
        if (GameManager.Instance != null)
        {
            if (BattleSession.IsCampaign) GameManager.Instance.ReturnToCampaign();
            else GameManager.Instance.LoadBaseWorld();
        }
    }

    private void ShowIncomingWave(int enemyCount)
    {
        if (incomingChip != null)
        {
            incomingChip.SetActive(true);
            var label = incomingChip.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = $"▼ {enemyCount} düşman yaklaşıyor";
            var group = incomingChip.GetComponent<CanvasGroup>();
            if (group == null) group = incomingChip.AddComponent<CanvasGroup>();
            group.DOKill();
            group.alpha = 1;
            group.DOFade(0, 0.3f).SetDelay(2.7f).OnComplete(() => incomingChip.SetActive(false));
        }
        // BattleVfxManager presents the wave banner; the HUD chip handles the warning.
    }
}
