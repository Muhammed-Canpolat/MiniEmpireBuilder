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
    private WaveAnnouncer waveAnnouncer;
    private bool refsReady;
    private bool subscribed;
    
    private int displayedGold = 0;

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

        if (levelText != null && GameManager.Instance?.PlayerData != null)
            levelText.text = $"Level {GameManager.Instance.PlayerData.currentBattleLevel}";
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
        if (waveText != null)
            waveText.text = $"Dalga {current}/{total}";
    }

    private void UpdateEnemyCount(int count)
    {
        if (enemyCountText != null)
            enemyCountText.text = $"Dusman: {count}";
    }

    private void UpdateGoldDisplay(int gold)
    {
        if (goldText != null)
        {
            DOTween.To(() => displayedGold, x => 
            {
                displayedGold = x;
                goldText.text = $"Altin: {displayedGold}";
            }, gold, 0.5f).SetEase(Ease.OutQuad);
            
            // Text scale animasyonu
            goldText.transform.DOKill(true);
            goldText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 1f);
        }
    }

    private void ShowBattleResult(bool won)
    {
        if (battleEndPanel != null)
            battleEndPanel.SetActive(true);

        if (resultText != null)
        {
            resultText.text = won ? "ZAFER!" : "YENILGI!";
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
                    rewardText.text = $"Kazanilan Altin: {total} (Savas: {bm.TotalGoldEarned} + Odul: {bm.VictoryBonus})";
                }
                else
                {
                    rewardText.text = "Ussunu guclendir ve tekrar dene!";
                }
            }
        }
    }

    private void OnReturnClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadBaseWorld();
    }

    private void ShowIncomingWave(int waveNumber)
    {
        if (waveAnnouncer == null)
            waveAnnouncer = FindFirstObjectByType<WaveAnnouncer>();

        if (waveAnnouncer != null)
            waveAnnouncer.AnnounceIncomingWave(waveNumber);
    }
}
