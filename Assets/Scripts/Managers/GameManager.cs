using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Oyunun ana yöneticisi — Singleton, sahneler arası yaşar
/// Altın, seviye yükseltme, bina yönetimi, sahne geçişi hepsini koordine eder
/// </summary>
public class GameManager : MonoBehaviour
{
    // ==================== SINGLETON ====================
    public static GameManager Instance { get; private set; }

    // ==================== OYUN VERİLERİ ====================
    public PlayerSaveData PlayerData { get; private set; }

    // Çalışma zamanı bina verileri (statlar hesaplanmış)
    public Dictionary<BuildingType, BuildingData> Buildings { get; private set; }

    // ==================== EVENT'LER ====================
    public event Action<int> OnGoldChanged;             // altın değişince
    public event Action<BuildingType, int> OnBuildingUpgraded;  // bina seviye atınca
    public event Action<int> OnHeroLevelUp;             // kahraman seviye atınca
    public event Action<int> OnBattleLevelChanged;      // savaş level değişince
    public event Action<int, int> OnGoldMineStoredChanged; // (stored, capacity)

    // ==================== DURUMLAR ====================
    public bool IsInBattle { get; private set; } = false;
    public bool IsGameStarted { get; private set; } = false;

    // ==================== UNITY YAŞAM DÖNGÜSÜ ====================

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Buildings = new Dictionary<BuildingType, BuildingData>();
    }

    private void Start()
    {
        // Kayıtlı oyun varsa yükle
        if (SaveSystem.HasSave())
        {
            LoadGame();
        }
    }

    // ==================== YENİ OYUN ====================

    /// <summary>
    /// Yeni oyun başlat — silah seçiminden sonra çağrılır
    /// </summary>
    public void StartNewGame(WeaponType selectedWeapon)
    {
        PlayerData = PlayerSaveData.CreateNew(selectedWeapon);
        RebuildBuildingData();
        IsGameStarted = true;
        SaveGame();

        Debug.Log($"[GameManager] Yeni oyun başlatıldı! Silah: {selectedWeapon}");

        // Üs dünyasına git
        LoadBaseWorld();
    }

    // ==================== KAYDETME / YÜKLEME ====================

    public void SaveGame()
    {
        if (PlayerData != null)
        {
            SaveSystem.Save(PlayerData);
        }
    }

    public void LoadGame()
    {
        PlayerData = SaveSystem.Load();
        if (PlayerData != null)
        {
            PlayerData.hero.ApplyLevelStats();
            RebuildBuildingData();
            IsGameStarted = true;
            Debug.Log($"[GameManager] Oyun yüklendi. Altın: {PlayerData.gold}, Level: {PlayerData.currentBattleLevel}");
        }
    }

    // ==================== ALTIN YÖNETİMİ ====================

    public int Gold => PlayerData?.gold ?? 0;
    public int GoldMineStored => PlayerData?.goldMineStoredGold ?? 0;

    /// <summary>
    /// Ana Üs seviyesine göre maksimum altın kapasitesi
    /// </summary>
    public int GetMaxGoldCapacity()
    {
        int mainBaseLevel = GetMainBaseLevel();
        return 500 + (mainBaseLevel - 1) * 300; // Lv1: 500, Lv2: 800, Lv3: 1100...
    }

    /// <summary>
    /// Altın ekle (savaştan kazanılan, madenden üretilen vs.)
    /// Altın kapasitesini aşamaz
    /// </summary>
    public void AddGold(int amount)
    {
        if (PlayerData == null || amount <= 0) return;
        int maxGold = GetMaxGoldCapacity();
        PlayerData.gold = Mathf.Min(PlayerData.gold + amount, maxGold);
        OnGoldChanged?.Invoke(PlayerData.gold);
    }

    /// <summary>
    /// Altın madeninde tutulabilecek maksimum birikim (maden seviyesiyle artar)
    /// </summary>
    public int GetGoldMineStorageCapacity()
    {
        if (Buildings == null || !Buildings.ContainsKey(BuildingType.GoldMine))
            return 0;

        BuildingData mine = Buildings[BuildingType.GoldMine];
        if (!mine.isUnlocked || mine.level <= 0)
            return 0;

        return 200 + (mine.level - 1) * 150;
    }

    public void AddGoldToMineStorage(int amount, bool saveAfter = false)
    {
        if (PlayerData == null || amount <= 0)
            return;

        int capacity = GetGoldMineStorageCapacity();
        if (capacity <= 0)
            return;

        PlayerData.goldMineStoredGold = Mathf.Min(PlayerData.goldMineStoredGold + amount, capacity);
        OnGoldMineStoredChanged?.Invoke(PlayerData.goldMineStoredGold, capacity);

        if (saveAfter)
            SaveGame();
    }

    /// <summary>
    /// Madende biriken altını cüzdana taşır. Cüzdan doluysa kalan maden içinde kalır.
    /// </summary>
    public int CollectGoldFromMineStorage()
    {
        if (PlayerData == null || PlayerData.goldMineStoredGold <= 0)
            return 0;

        int maxGold = GetMaxGoldCapacity();
        int walletSpace = Mathf.Max(0, maxGold - PlayerData.gold);
        if (walletSpace <= 0)
            return 0;

        int toCollect = Mathf.Min(PlayerData.goldMineStoredGold, walletSpace);
        PlayerData.goldMineStoredGold -= toCollect;
        PlayerData.gold += toCollect;

        OnGoldChanged?.Invoke(PlayerData.gold);
        OnGoldMineStoredChanged?.Invoke(PlayerData.goldMineStoredGold, GetGoldMineStorageCapacity());
        SaveGame();

        return toCollect;
    }

    /// <summary>
    /// Oyuna dönüşte madenin offline üretimini madende biriktirir.
    /// </summary>
    public void ApplyOfflineGoldMineProduction()
    {
        if (PlayerData == null)
            return;

        long nowTicks = DateTime.UtcNow.Ticks;
        if (PlayerData.goldMineLastCollectTime <= 0)
        {
            PlayerData.goldMineLastCollectTime = nowTicks;
            return;
        }

        float gps = GetGoldPerSecond();
        if (gps <= 0f)
        {
            PlayerData.goldMineLastCollectTime = nowTicks;
            return;
        }

        double elapsedSeconds = new TimeSpan(nowTicks - PlayerData.goldMineLastCollectTime).TotalSeconds;
        if (elapsedSeconds <= 0.5d)
            return;

        int produced = Mathf.FloorToInt((float)(elapsedSeconds * gps));
        if (produced > 0)
            AddGoldToMineStorage(produced, false);

        PlayerData.goldMineLastCollectTime = nowTicks;
        SaveGame();
    }

    /// <summary>
    /// Altın harca — yeterliyse true döner
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (PlayerData == null || amount <= 0) return false;
        if (PlayerData.gold < amount)
        {
            Debug.Log($"[GameManager] Yetersiz altın! Gereken: {amount}, Mevcut: {PlayerData.gold}");
            return false;
        }

        PlayerData.gold -= amount;
        OnGoldChanged?.Invoke(PlayerData.gold);
        Debug.Log($"[GameManager] -{amount} altın harcandı. Kalan: {PlayerData.gold}");
        return true;
    }

    // ==================== BİNA YÖNETİMİ ====================

    /// <summary>
    /// Kayıtlı bina verilerinden çalışma zamanı BuildingData oluştur
    /// </summary>
    private void RebuildBuildingData()
    {
        Buildings.Clear();

        foreach (var entry in PlayerData.buildings)
        {
            BuildingData bd = new BuildingData();
            bd.buildingType = entry.buildingType;
            bd.level = entry.level;
            bd.isUnlocked = entry.isUnlocked;
            bd.ApplyLevelStats();

            Buildings[entry.buildingType] = bd;
        }

        OnGoldMineStoredChanged?.Invoke(PlayerData?.goldMineStoredGold ?? 0, GetGoldMineStorageCapacity());
    }

    /// <summary>
    /// Bina seviye yükselt
    /// </summary>
    public bool UpgradeBuilding(BuildingType type)
    {
        if (!Buildings.ContainsKey(type))
        {
            Debug.LogWarning($"[GameManager] Bina bulunamadı: {type}");
            return false;
        }

        BuildingData building = Buildings[type];

        // Kilitli mi?
        if (!building.isUnlocked)
        {
            int requiredLevel = building.GetRequiredMainBaseLevel();
            int mainBaseLevel = GetMainBaseLevel();
            Debug.Log($"[GameManager] {type} kilitli! Ana Üs Lv.{requiredLevel} gerekli (şu an Lv.{mainBaseLevel})");
            return false;
        }

        // Max seviyeye ulaşmış mı?
        int maxLvl = building.GetMaxLevel(GetMainBaseLevel());
        if (building.level >= maxLvl)
        {
            Debug.Log($"[GameManager] {type} max seviyeye ulaştı! (Lv.{building.level}/{maxLvl})");
            return false;
        }

        // Altın yeterli mi?
        int cost = building.GetUpgradeCost();
        if (!SpendGold(cost))
        {
            return false;
        }

        // Seviye yükselt
        building.level++;
        building.ApplyLevelStats();

        // Kayıt verisini güncelle
        UpdateBuildingSaveEntry(type, building.level, building.isUnlocked);

        // Ana Üs seviye atınca yeni binaları aç
        if (type == BuildingType.MainBase)
        {
            CheckUnlockBuildings();
        }

        OnBuildingUpgraded?.Invoke(type, building.level);
        SaveGame();

        Debug.Log($"[GameManager] {type} seviye yükseltildi! Lv.{building.level}");
        return true;
    }

    /// <summary>
    /// Ana Üs seviyesine göre kilitli binaları kontrol et ve aç
    /// </summary>
    private void CheckUnlockBuildings()
    {
        int mainBaseLevel = GetMainBaseLevel();

        foreach (var kvp in Buildings)
        {
            if (!kvp.Value.isUnlocked)
            {
                int required = kvp.Value.GetRequiredMainBaseLevel();
                if (mainBaseLevel >= required)
                {
                    kvp.Value.isUnlocked = true;
                    kvp.Value.level = 1;
                    kvp.Value.ApplyLevelStats();
                    UpdateBuildingSaveEntry(kvp.Key, 1, true);
                    Debug.Log($"[GameManager] {kvp.Key} açıldı! (Ana Üs Lv.{mainBaseLevel})");
                }
            }
        }
    }

    public int GetMainBaseLevel()
    {
        if (Buildings.ContainsKey(BuildingType.MainBase))
            return Buildings[BuildingType.MainBase].level;
        return 1;
    }

    private void UpdateBuildingSaveEntry(BuildingType type, int level, bool unlocked)
    {
        foreach (var entry in PlayerData.buildings)
        {
            if (entry.buildingType == type)
            {
                entry.level = level;
                entry.isUnlocked = unlocked;
                return;
            }
        }
    }

    // ==================== KAHRAMAN YÖNETİMİ ====================

    /// <summary>
    /// Ana Savaşçı seviye yükselt
    /// </summary>
    public bool UpgradeHero()
    {
        if (PlayerData == null) return false;

        int maxHeroLevel = GetMainBaseLevel() + 1; // Ana Üs seviyesi + 1
        if (PlayerData.hero.level >= maxHeroLevel)
        {
            Debug.Log($"[GameManager] Kahraman max seviyede! (Lv.{PlayerData.hero.level}/{maxHeroLevel})");
            return false;
        }

        int cost = PlayerData.hero.level * 40; // Seviye başına 40 altın
        if (!SpendGold(cost))
        {
            return false;
        }

        PlayerData.hero.level++;
        PlayerData.hero.ApplyLevelStats();

        OnHeroLevelUp?.Invoke(PlayerData.hero.level);
        SaveGame();

        Debug.Log($"[GameManager] Kahraman seviye atladı! Lv.{PlayerData.hero.level}");
        return true;
    }

    // ==================== SAVAŞ YÖNETİMİ ====================

    /// <summary>
    /// Savaşı başlat — savaş sahnesine geç
    /// </summary>
    public void StartBattle()
    {
        IsInBattle = true;
        StartCoroutine(FadeAndLoadScene("BattleScene"));
        Debug.Log($"[GameManager] Savaş başlıyor! Level {PlayerData.currentBattleLevel}");
    }

    /// <summary>
    /// Savaş kazanıldı
    /// </summary>
    public void BattleWon(int goldEarned)
    {
        IsInBattle = false;
        AddGold(goldEarned);

        if (PlayerData.currentBattleLevel >= PlayerData.highestBattleLevel)
        {
            PlayerData.highestBattleLevel = PlayerData.currentBattleLevel + 1;
        }

        PlayerData.currentBattleLevel++;
        OnBattleLevelChanged?.Invoke(PlayerData.currentBattleLevel);
        SaveGame();

        Debug.Log($"[GameManager] Savaş kazanıldı! +{goldEarned} altın. Yeni level: {PlayerData.currentBattleLevel}");
    }

    /// <summary>
    /// Savaş kaybedildi
    /// </summary>
    public void BattleLost()
    {
        IsInBattle = false;
        SaveGame();
        Debug.Log($"[GameManager] Savaş kaybedildi! Level {PlayerData.currentBattleLevel}");
    }

    // ==================== SAHNE GEÇİŞLERİ ====================

    public void LoadBaseWorld()
    {
        IsInBattle = false;
        StartCoroutine(FadeAndLoadScene("BaseScene"));
        Debug.Log("[GameManager] Üs dünyasına geçiliyor...");
    }

    public void LoadMainMenu()
    {
        StartCoroutine(FadeAndLoadScene("MainMenuScene"));
        Debug.Log("[GameManager] Ana menüye geçiliyor...");
    }

    /// <summary>
    /// Sahne geçişi sırasında fade-out efekti uygular
    /// </summary>
    private System.Collections.IEnumerator FadeAndLoadScene(string sceneName)
    {
        // Fade-out canvas oluştur
        GameObject fadeObj = new GameObject("SceneFade");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;

        GameObject panel = new GameObject("FadePanel");
        panel.transform.SetParent(fadeObj.transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        UnityEngine.UI.Image img = panel.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = true; // Fade sırasında tıklamayı engelle

        // Fade to black
        float t = 0;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            img.color = new Color(0, 0, 0, Mathf.Clamp01(t / 0.4f));
            yield return null;
        }

        // Sahneyi yükle
        SceneManager.LoadScene(sceneName);
    }

    // ==================== YARDIMCI ====================

    /// <summary>
    /// Altın Madeni'nin saniyede ürettiği altın
    /// </summary>
    public float GetGoldPerSecond()
    {
        if (Buildings.ContainsKey(BuildingType.GoldMine) && Buildings[BuildingType.GoldMine].isUnlocked)
        {
            return Buildings[BuildingType.GoldMine].goldPerSecond;
        }
        return 0f;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            if (PlayerData != null)
                PlayerData.goldMineLastCollectTime = DateTime.UtcNow.Ticks;
            SaveGame();
        }
    }

    private void OnApplicationQuit()
    {
        if (PlayerData != null)
            PlayerData.goldMineLastCollectTime = DateTime.UtcNow.Ticks;
        SaveGame();
    }
}
