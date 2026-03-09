using UnityEngine;

/// <summary>
/// Us dunyasinda genel runtime akisi.
/// Altin madeni online/offline uretimi burada yonetilir.
/// </summary>
public class BaseWorldController : MonoBehaviour
{
    private float mineAccumulator;
    private float periodicSaveTimer;

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[BaseWorld] GameManager bulunamadi!");
            return;
        }

        GameManager.Instance.ApplyOfflineGoldMineProduction();

        Debug.Log("[BaseWorld] Us dunyasi yuklendi.");
        Debug.Log($"  Cuzdan altin: {GameManager.Instance.Gold}");
        Debug.Log($"  Maden altin: {GameManager.Instance.GoldMineStored}/{GameManager.Instance.GetGoldMineStorageCapacity()}");
        Debug.Log($"  Ana Us Lv: {GameManager.Instance.GetMainBaseLevel()}");
        Debug.Log($"  Savas Level: {GameManager.Instance.PlayerData.currentBattleLevel}");
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.PlayerData == null)
            return;

        ProduceMineGoldOnline();
        periodicSaveTimer += Time.deltaTime;
        if (periodicSaveTimer >= 10f)
        {
            periodicSaveTimer = 0f;
            GameManager.Instance.PlayerData.goldMineLastCollectTime = System.DateTime.UtcNow.Ticks;
            GameManager.Instance.SaveGame();
        }
    }

    private void ProduceMineGoldOnline()
    {
        float goldPerSec = GameManager.Instance.GetGoldPerSecond();
        if (goldPerSec <= 0f)
            return;

        mineAccumulator += goldPerSec * Time.deltaTime;
        if (mineAccumulator < 1f)
            return;

        int produced = Mathf.FloorToInt(mineAccumulator);
        mineAccumulator -= produced;

        GameManager.Instance.AddGoldToMineStorage(produced, false);
        GameManager.Instance.PlayerData.goldMineLastCollectTime = System.DateTime.UtcNow.Ticks;
    }
}
