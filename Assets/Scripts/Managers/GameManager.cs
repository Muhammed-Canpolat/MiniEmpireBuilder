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
    public event Action<int> OnGoldChanged;                         // altın değişince
    public event Action<int> OnGemsChanged;                         // elmas değişince
    public event Action<int> OnBuilderHired;                        // yeni inşaatçı (toplam sayı)
    public event Action<BuildingType, int> OnBuildingUpgraded;      // bina seviye atınca
    public event Action<int> OnHeroLevelUp;                         // kahraman seviye atınca
    public event Action<int> OnApprenticeLevelUp;
    public event Action<int> OnBattleLevelChanged;                  // savaş level değişince
    public event Action<int, int> OnGoldMineStoredChanged;          // (stored, capacity)

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
            // Oyun kapalıyken süresi dolan yükseltme hemen uygulanır.
            CheckBuilderTask();
            if (PlayerData.buildableSize <= 0)
            {
                PlayerData.buildableSize = 10;
            }
            if (PlayerData.removedFenceIds == null)
            {
                PlayerData.removedFenceIds = new List<string>();
            }
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
        // Bir sonraki Ana Üs yükseltmesinin 1.5 katı (en az 500): biriktirilebilir hedef.
        int mainBaseLevel = GetMainBaseLevel();
        int next = Progression.UpgradeCost(BuildingType.MainBase,
            Mathf.Min(mainBaseLevel + 1, Progression.MaxLevel));
        return Mathf.Max(500, Mathf.CeilToInt(next * 1.5f / 100f) * 100);
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

        float gps = GetGoldPerSecond(false);
        if (gps <= 0f)
        {
            PlayerData.goldMineLastCollectTime = nowTicks;
            return;
        }

        double elapsedSeconds = new TimeSpan(nowTicks - PlayerData.goldMineLastCollectTime).TotalSeconds;
        if (elapsedSeconds <= 0.5d)
            return;

        // 2x maden yalnız süresiyle örtüşen kısma uygulanır (takvim 5. gün).
        long boostEnd = Math.Min(nowTicks, PlayerData.mineBoostUntilTicks);
        double boostedSeconds = Math.Max(0d, new TimeSpan(boostEnd - PlayerData.goldMineLastCollectTime).TotalSeconds);
        int produced = Mathf.FloorToInt((float)((elapsedSeconds + boostedSeconds) * gps));
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

    // ==================== ELMAS ====================

    public int Gems => PlayerData?.gems ?? 0;

    public void AddGems(int amount, bool saveAfter = true)
    {
        if (PlayerData == null || amount <= 0) return;
        PlayerData.gems += amount;
        OnGemsChanged?.Invoke(PlayerData.gems);
        if (saveAfter) SaveGame();
    }

    public bool SpendGems(int amount)
    {
        if (PlayerData == null || amount <= 0 || PlayerData.gems < amount) return false;
        PlayerData.gems -= amount;
        OnGemsChanged?.Invoke(PlayerData.gems);
        SaveGame();
        return true;
    }

    // ==================== BİNA YÖNETİMİ ====================

    /// <summary>
    /// Kayıtlı bina verilerinden çalışma zamanı BuildingData oluştur.
    /// Türün seviyesi = o türdeki en yüksek binanın seviyesi (her bina kendi seviyesini taşır).
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
            bd.SetBuildLimits();
            bd.ApplyLevelStats();

            Buildings[entry.buildingType] = bd;
        }
        foreach (var type in new List<BuildingType>(Buildings.Keys)) RecomputeTypeLevel(type);

        OnGoldMineStoredChanged?.Invoke(PlayerData?.goldMineStoredGold ?? 0, GetGoldMineStorageCapacity());
    }

    private void RecomputeTypeLevel(BuildingType type)
    {
        if (!Buildings.TryGetValue(type, out var data)) return;
        int best = 0;
        if (PlayerData?.placedBuildings != null)
            foreach (var placed in PlayerData.placedBuildings)
                if (placed != null && placed.buildingType == type.ToString())
                    best = Mathf.Max(best, placed.level);
        if (best > 0) data.level = best;
        data.ApplyLevelStats();
        var entry = PlayerData?.buildings?.Find(e => e.buildingType == type);
        if (entry != null) entry.level = data.level;
    }

    public PlacedBuildingData FindPlaced(string slotId) =>
        string.IsNullOrEmpty(slotId) ? null
            : PlayerData?.placedBuildings?.Find(p => p != null && p.slotId == slotId);

    /// <summary>Belirli bir binanın seviyesi; kayıt yoksa türün seviyesi.</summary>
    public int GetInstanceLevel(string slotId, BuildingType type)
    {
        var placed = FindPlaced(slotId);
        if (placed != null && placed.level > 0) return placed.level;
        return Buildings.TryGetValue(type, out var data) ? Mathf.Max(1, data.level) : 1;
    }

    /// <summary>Bir binanın seviyesine göre hesaplanmış istatistikler (tür verisinin kopyası).</summary>
    public BuildingData GetInstanceData(string slotId, BuildingType type) =>
        CreateLevelData(type, GetInstanceLevel(slotId, type));

    public BuildingData CreateLevelData(BuildingType type, int level)
    {
        Buildings.TryGetValue(type, out var source);
        var copy = new BuildingData
        {
            buildingType = type,
            isUnlocked = source == null || source.isUnlocked,
            level = Mathf.Max(1, level)
        };
        copy.SetBuildLimits();
        copy.ApplyLevelStats();
        return copy;
    }

    /// <summary>Savaşta bu türden kurulan index'inci kule: binalar seviyeye göre azalan sırada.</summary>
    public int GetBattleTowerLevel(BuildingType type, int index)
    {
        var levels = new List<int>();
        if (PlayerData?.placedBuildings != null)
            foreach (var placed in PlayerData.placedBuildings)
                if (placed != null && placed.buildingType == type.ToString())
                    levels.Add(Mathf.Max(1, placed.level));
        if (levels.Count == 0)
            return Buildings.TryGetValue(type, out var data) ? Mathf.Max(1, data.level) : 1;
        levels.Sort((a, b) => b.CompareTo(a));
        return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
    }

    public enum UpgradeBlock { None, Missing, Locked, MaxLevel, BuilderBusy, NoGold, Upgrading }

    public UpgradeBlock CanUpgrade(string slotId, BuildingType type)
    {
        if (!Buildings.TryGetValue(type, out var data)) return UpgradeBlock.Missing;
        if (!data.isUnlocked) return UpgradeBlock.Locked;
        if (TaskForSlot(slotId) != null) return UpgradeBlock.Upgrading;
        int level = GetInstanceLevel(slotId, type);
        if (level >= data.GetMaxLevel(GetMainBaseLevel())) return UpgradeBlock.MaxLevel;
        if (IsBuilderBusy) return UpgradeBlock.BuilderBusy;
        if (Gold < Progression.UpgradeCost(type, level + 1)) return UpgradeBlock.NoGold;
        return UpgradeBlock.None;
    }

    /// <summary>Altını öder ve inşaatçıyı süreli yükseltmeye gönderir. Seviye süre bitince artar.</summary>
    public bool StartUpgrade(string slotId, BuildingType type)
    {
        if (CanUpgrade(slotId, type) != UpgradeBlock.None) return false;
        int level = GetInstanceLevel(slotId, type);
        if (!SpendGold(Progression.UpgradeCost(type, level + 1))) return false;
        var placed = FindPlaced(slotId);
        Vector3 position = placed != null && placed.hasPosition ? new Vector3(placed.x, placed.y, 0f) : Vector3.zero;
        StartBuilderTask("upgrade", $"{BaseMapManager.GetBuildingDisplayName(type)} Sev. {level + 1}",
            Progression.UpgradeSecondsTo(level + 1), position, slotId, type.ToString());
        return true;
    }

    /// <summary>Geriye uyum: türün en düşük seviyeli binasını yükseltmeye başlatır.</summary>
    public bool UpgradeBuilding(BuildingType type)
    {
        string slotId = null;
        int lowest = int.MaxValue;
        if (PlayerData?.placedBuildings != null)
            foreach (var placed in PlayerData.placedBuildings)
                if (placed != null && placed.buildingType == type.ToString() && placed.level < lowest)
                {
                    lowest = placed.level;
                    slotId = placed.slotId;
                }
        return StartUpgrade(slotId, type);
    }

    private void ApplyInstanceUpgrade(string slotId, BuildingType type)
    {
        if (!Buildings.TryGetValue(type, out var data)) return;
        var placed = FindPlaced(slotId);
        int newLevel;
        if (placed != null)
        {
            placed.level = Mathf.Max(1, placed.level) + 1;
            newLevel = placed.level;
        }
        else
        {
            newLevel = data.level + 1;
            data.level = newLevel;
            UpdateBuildingSaveEntry(type, newLevel, data.isUnlocked);
        }
        RecomputeTypeLevel(type);
        if (type == BuildingType.MainBase)
        {
            CheckUnlockBuildings();
            GemRewards.ForMainBaseLevel(newLevel);
        }
        OnBuildingUpgraded?.Invoke(type, newLevel);
        AudioManager.Instance?.PlaySfx("levelUp");
        Debug.Log($"[GameManager] {type} ({slotId}) Sev. {newLevel} tamamlandı.");
    }

    // ==================== İNŞAATÇI (gerçek saat) ====================

    public event Action OnBuilderTaskStarted;
    public event Action<BuilderTaskSave> OnBuilderTaskCompleted;

    private static readonly List<BuilderTaskSave> NoTasks = new List<BuilderTaskSave>();

    /// <summary>Etkin görevler (her inşaatçının en fazla bir görevi).</summary>
    public IReadOnlyList<BuilderTaskSave> BuilderTasks => PlayerData?.builderTasks ?? NoTasks;
    public int BuilderCount => Mathf.Clamp(PlayerData?.builderCount ?? 1, 1, Progression.MaxBuilders);
    public int BusyBuilderCount => BuilderTasks.Count;
    public int FreeBuilderCount => Mathf.Max(0, WorkingBuilderCount - BusyBuilderCount);

    /// <summary>
    /// Çalışan inşaatçı sayısı: kalıcılar + süresi süren (ya da işi henüz bitmemiş) misafir inşaatçı.
    /// Kiralama bedeli yalnız kalıcı sayıya (<see cref="BuilderCount"/>) bakar.
    /// </summary>
    public int WorkingBuilderCount
    {
        get
        {
            int count = BuilderCount;
            if (count >= Progression.MaxBuilders) return count;
            bool guestWorking = false;
            foreach (var task in BuilderTasks)
                if (task.builderIndex >= count) { guestWorking = true; break; }
            return TempBuilderActive || guestWorking ? count + 1 : count;
        }
    }

    public bool TempBuilderActive => PlayerData != null && PlayerData.tempBuilderUntilTicks > DateTime.UtcNow.Ticks;
    public bool BuildPotionActive => PlayerData != null && PlayerData.buildPotionUntilTicks > DateTime.UtcNow.Ticks;
    public bool HasFreeBuilder => FreeBuilderCount > 0;

    /// <summary>Tüm inşaatçılar meşgul (yeni iş başlatılamaz).</summary>
    public bool IsBuilderBusy => PlayerData != null && !HasFreeBuilder;

    /// <summary>En erken bitecek görev; görev yoksa null. (Tek inşaatçı dönemiyle uyumlu.)</summary>
    public BuilderTaskSave BuilderTask
    {
        get
        {
            BuilderTaskSave soonest = null;
            foreach (var task in BuilderTasks)
                if (soonest == null || task.endTicks < soonest.endTicks) soonest = task;
            return soonest;
        }
    }

    public double BuilderSecondsRemaining => SecondsRemaining(BuilderTask);
    public float BuilderProgress => TaskProgress(BuilderTask);

    public static double SecondsRemaining(BuilderTaskSave task) => task == null || !task.IsActive ? 0d
        : Math.Max(0d, new TimeSpan(task.endTicks - DateTime.UtcNow.Ticks).TotalSeconds);

    public static float TaskProgress(BuilderTaskSave task)
    {
        if (task == null || !task.IsActive) return 0f;
        double total = task.endTicks - task.startTicks;
        return total <= 0 ? 1f : Mathf.Clamp01((float)((DateTime.UtcNow.Ticks - task.startTicks) / total));
    }

    public BuilderTaskSave TaskForBuilder(int builderIndex)
    {
        foreach (var task in BuilderTasks)
            if (task.builderIndex == builderIndex) return task;
        return null;
    }

    /// <summary>Bu binada süren yükseltme/inşa görevi (yoksa null).</summary>
    public BuilderTaskSave TaskForSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return null;
        foreach (var task in BuilderTasks)
            if (task.slotId == slotId) return task;
        return null;
    }

    private int FirstFreeBuilderIndex()
    {
        for (int i = 0; i < WorkingBuilderCount; i++)
            if (TaskForBuilder(i) == null) return i;
        return -1;
    }

    /// <summary>Boştaki ilk inşaatçıya görev verir. Başlayan görev <see cref="LastStartedTask"/>.</summary>
    public bool StartBuilderTask(string kind, string label, float seconds, Vector3 position,
        string slotId = "", string buildingType = "")
    {
        if (PlayerData == null) return false;
        int index = FirstFreeBuilderIndex();
        if (index < 0) return false;
        long now = DateTime.UtcNow.Ticks;
        var task = new BuilderTaskSave
        {
            builderIndex = index,
            kind = kind,
            label = label,
            slotId = slotId ?? "",
            buildingType = buildingType ?? "",
            startTicks = now,
            endTicks = now + TimeSpan.FromSeconds(Mathf.Max(0.1f, seconds)).Ticks,
            x = position.x,
            y = position.y
        };
        // Giriş takviminden bekleyen hızlandırma, sonraki yükseltmeye uygulanır.
        if (kind == "upgrade" && PlayerData.pendingSpeedupMinutes > 0)
        {
            task.endTicks = Math.Max(now + TimeSpan.FromSeconds(1).Ticks,
                task.endTicks - TimeSpan.FromMinutes(PlayerData.pendingSpeedupMinutes).Ticks);
            PlayerData.pendingSpeedupMinutes = 0;
        }
        // İnşaat İksiri sürüyorsa yeni iş de hızlı ilerler.
        if (BuildPotionActive)
            task.endTicks = now + GemEconomy.Boosted(task.endTicks - now, PlayerData.buildPotionUntilTicks - now);
        PlayerData.builderTasks.Add(task);
        LastStartedTask = task;
        SaveGame();
        OnBuilderTaskStarted?.Invoke();
        return true;
    }

    public BuilderTaskSave LastStartedTask { get; private set; }

    /// <summary>
    /// Süren en uzun yükseltmeyi verilen dakika kadar kısaltır; yükseltme yoksa hızlandırma
    /// bir sonraki yükseltmeye saklanır. Yalnız "upgrade" görevleri (geri çağrısı yok) kısalır.
    /// </summary>
    public bool SpeedUpUpgrade(int minutes)
    {
        if (PlayerData == null || minutes <= 0) return false;
        long now = DateTime.UtcNow.Ticks;
        BuilderTaskSave longest = null;
        foreach (var task in PlayerData.builderTasks)
            if (task.kind == "upgrade" && task.endTicks > now && (longest == null || task.endTicks > longest.endTicks))
                longest = task;
        if (longest == null)
        {
            PlayerData.pendingSpeedupMinutes += minutes;
            SaveGame();
            return false;
        }
        longest.endTicks = Math.Max(now, longest.endTicks - TimeSpan.FromMinutes(minutes).Ticks);
        SaveGame();
        OnBuilderTaskStarted?.Invoke();
        return true;
    }

    // ==================== ELMAS: İNŞAAT ====================

    /// <summary>Bu işi şimdi bitirmenin elmas bedeli (1 dk altı 0).</summary>
    public static int FinishNowCost(BuilderTaskSave task) => GemEconomy.FinishCost(SecondsRemaining(task));

    /// <summary>Süren işi elmasla hemen bitirir (yükseltme, inşa, kaldırma).</summary>
    public bool FinishTaskNow(BuilderTaskSave task)
    {
        if (PlayerData == null || task == null || !PlayerData.builderTasks.Contains(task)) return false;
        int cost = FinishNowCost(task);
        if (cost > 0 && !SpendGems(cost)) return false;
        task.endTicks = DateTime.UtcNow.Ticks;
        CheckBuilderTask();
        return true;
    }

    /// <summary>İnşaat İksiri: 2 saat bütün inşaatçılar 4 kat hızlı. Sürerken yeniden alınamaz.</summary>
    public bool ActivateBuildPotion()
    {
        if (PlayerData == null || BuildPotionActive) return false;
        if (!SpendGems(GemEconomy.PotionCost)) return false;
        long now = DateTime.UtcNow.Ticks;
        long window = TimeSpan.FromHours(GemEconomy.PotionHours).Ticks;
        PlayerData.buildPotionUntilTicks = now + window;
        foreach (var task in PlayerData.builderTasks)
            if (task.endTicks > now)
                task.endTicks = now + GemEconomy.Boosted(task.endTicks - now, window);
        SaveGame();
        OnBuilderTaskStarted?.Invoke();
        return true;
    }

    /// <summary>Misafir inşaatçı: 24 saat bir inşaatçı daha. 5 kalıcı inşaatçı varken alınamaz.</summary>
    public bool CanRentTempBuilder => PlayerData != null && !TempBuilderActive && BuilderCount < Progression.MaxBuilders;

    public bool RentTempBuilder()
    {
        if (!CanRentTempBuilder || !SpendGems(GemEconomy.TempBuilderCost)) return false;
        PlayerData.tempBuilderUntilTicks = DateTime.UtcNow.Ticks + TimeSpan.FromHours(GemEconomy.TempBuilderHours).Ticks;
        SaveGame();
        OnBuilderHired?.Invoke(BuilderCount);
        return true;
    }

    /// <summary>Elmasla 2x maden (4 saat; sürüyorsa üstüne eklenir).</summary>
    public bool BuyMineBoost()
    {
        if (PlayerData == null || !SpendGems(GemEconomy.MineBoostCost)) return false;
        ApplyOfflineGoldMineProduction(); // önceki üretim normal hızla yazılsın
        long now = DateTime.UtcNow.Ticks;
        PlayerData.mineBoostUntilTicks = Math.Max(now, PlayerData.mineBoostUntilTicks)
            + TimeSpan.FromHours(GemEconomy.MineBoostHours).Ticks;
        SaveGame();
        return true;
    }

    /// <summary>Eksik altını elmasla tamamlar: <paramref name="needed"/> altına ulaşacak kadar ekler.</summary>
    public bool TopUpGold(int needed)
    {
        if (PlayerData == null) return false;
        int missing = needed - Gold;
        if (missing <= 0) return true;
        if (!SpendGems(GemEconomy.GoldTopUpCost(missing))) return false;
        AddGold(missing);
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ==================== GELİŞTİRİCİ TEST MENÜSÜ (yalnız editör / Development APK) ====================

    /// <summary>Ana Üs seviyesini doğrudan ayarlar (binalar açılır, kahraman tavanı değişir).</summary>
    public void DevSetMainBaseLevel(int level)
    {
        if (PlayerData == null) return;
        level = Mathf.Clamp(level, 1, Progression.MaxLevel);
        foreach (var placed in PlayerData.placedBuildings)
            if (placed != null && placed.buildingType == BuildingType.MainBase.ToString()) placed.level = level;
        var entry = PlayerData.buildings.Find(e => e.buildingType == BuildingType.MainBase);
        if (entry != null) entry.level = level;
        if (Buildings.TryGetValue(BuildingType.MainBase, out var data)) data.level = level;
        RecomputeTypeLevel(BuildingType.MainBase);
        CheckUnlockBuildings();
        SaveGame();
        OnBuildingUpgraded?.Invoke(BuildingType.MainBase, level);
    }

    /// <summary>
    /// Zamanı ileri alır: kayıttaki bütün süreler geçmişe kaydırılır (inşaat, can, maden, güçlendirmeler,
    /// ağaç/kaya büyümesi). Gerçek saate dokunmaz.
    /// </summary>
    public void DevAdvanceTime(TimeSpan span)
    {
        if (PlayerData == null) return;
        long t = span.Ticks;
        long Shift(long value) => value > 0 ? value - t : value;
        foreach (var task in PlayerData.builderTasks)
        {
            task.startTicks = Shift(task.startTicks);
            task.endTicks = Shift(task.endTicks);
        }
        PlayerData.livesLastRegenTicks = Shift(PlayerData.livesLastRegenTicks);
        PlayerData.goldMineLastCollectTime = Shift(PlayerData.goldMineLastCollectTime);
        PlayerData.buildPotionUntilTicks = Shift(PlayerData.buildPotionUntilTicks);
        PlayerData.tempBuilderUntilTicks = Shift(PlayerData.tempBuilderUntilTicks);
        PlayerData.mineBoostUntilTicks = Shift(PlayerData.mineBoostUntilTicks);
        PlayerData.unlimitedLivesUntilTicks = Shift(PlayerData.unlimitedLivesUntilTicks);
        PlayerData.terrainLastGrowTicks = Shift(PlayerData.terrainLastGrowTicks);
        ApplyOfflineGoldMineProduction();
        CheckBuilderTask();
        SaveGame();
    }
#endif

    private void Update() => CheckBuilderTask();

    private void CheckBuilderTask()
    {
        if (PlayerData?.builderTasks == null || PlayerData.builderTasks.Count == 0) return;
        long now = DateTime.UtcNow.Ticks;
        List<BuilderTaskSave> finished = null;
        foreach (var task in PlayerData.builderTasks)
            if (task.endTicks <= now) (finished ??= new List<BuilderTaskSave>()).Add(task);
        if (finished == null) return;
        foreach (var task in finished)
        {
            PlayerData.builderTasks.Remove(task);
            if (task.kind == "upgrade" && Enum.TryParse(task.buildingType, out BuildingType type))
                ApplyInstanceUpgrade(task.slotId, type);
        }
        SaveGame();
        foreach (var task in finished) OnBuilderTaskCompleted?.Invoke(task);
    }

    // ==================== İNŞAATÇI KİRALAMA ====================

    public enum HireBlock { None, MaxReached, MainBaseLevel, NoGold, NoGems }

    /// <summary>Sıradaki inşaatçının bedeli (en fazlaya ulaşıldıysa 0,0).</summary>
    public (int gold, int gems) NextBuilderCost => Progression.BuilderCost(BuilderCount + 1);

    public HireBlock CanHireBuilder()
    {
        if (PlayerData == null || BuilderCount >= Progression.MaxBuilders) return HireBlock.MaxReached;
        if (GetMainBaseLevel() < Progression.SecondBuilderMainBaseLevel) return HireBlock.MainBaseLevel;
        var cost = NextBuilderCost;
        if (cost.gold > Gold) return HireBlock.NoGold;
        if (cost.gems > Gems) return HireBlock.NoGems;
        return HireBlock.None;
    }

    public bool HireBuilder()
    {
        if (CanHireBuilder() != HireBlock.None) return false;
        var cost = NextBuilderCost;
        if (cost.gold > 0 && !SpendGold(cost.gold)) return false;
        if (cost.gems > 0 && !SpendGems(cost.gems)) return false;
        PlayerData.builderCount = BuilderCount + 1;
        SaveGame();
        OnBuilderHired?.Invoke(PlayerData.builderCount);
        AudioManager.Instance?.PlaySfx("levelUp");
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
                    Debug.Log($"[GameManager] {kvp.Key} açıldı! (Ana Üs Sev. {mainBaseLevel})");
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

    /// <summary>Tür kaydını günceller. Binaların kendi seviyelerine dokunmaz.</summary>
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

    /// <summary>Kahraman ve Çırak en fazla Ana Üs seviyesine çıkabilir.</summary>
    public int GetHeroMaxLevel() => Mathf.Clamp(GetMainBaseLevel(), 1, Progression.MaxLevel);
    public int GetHeroUpgradeCost() => Progression.HeroUpgradeCost(PlayerData?.hero?.level ?? 1);
    public int GetApprenticeMaxLevel() => GetHeroMaxLevel();

    /// <summary>
    /// Ana Savaşçı seviye yükselt (anında)
    /// </summary>
    public bool UpgradeHero()
    {
        if (PlayerData == null) return false;
        if (PlayerData.hero.level >= GetHeroMaxLevel()) return false;
        if (!SpendGold(GetHeroUpgradeCost())) return false;

        PlayerData.hero.level++;
        PlayerData.hero.ApplyLevelStats();

        OnHeroLevelUp?.Invoke(PlayerData.hero.level);
        SaveGame();

        Debug.Log($"[GameManager] Kahraman seviye atladı! Sev. {PlayerData.hero.level}");
        return true;
    }

    public int GetApprenticeUpgradeCost()
    {
        int level = PlayerData != null ? Mathf.Clamp(PlayerData.apprenticeLevel, 1, Progression.MaxLevel) : 1;
        return level >= Progression.MaxLevel ? 0 : Progression.ApprenticeUpgradeCost(level);
    }

    public bool UpgradeApprentice()
    {
        if (PlayerData == null || PlayerData.apprenticeLevel >= GetApprenticeMaxLevel()) return false;
        int cost = GetApprenticeUpgradeCost();
        if (!SpendGold(cost)) return false;
        PlayerData.apprenticeLevel++;
        OnApprenticeLevelUp?.Invoke(PlayerData.apprenticeLevel);
        SaveGame();
        return true;
    }

    // ==================== SAVAŞ YÖNETİMİ ====================

    /// <summary>
    /// Savaşı başlat — savaş sahnesine geç
    /// </summary>
    /// <summary>
    /// Sefer savaşı: can varsa açık kartları savaşa geçirip başlatır; can yoksa false.
    /// </summary>
    public bool TryStartCampaignBattle()
    {
        if (!LivesSystem.CanPlay) return false;
        BattleCards.BeginBattle();
        StartBattle();
        return true;
    }

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
        BattleCards.EndBattle();
        AddGold(goldEarned);

        if (BattleSession.IsCampaign)
        {
            SaveGame();
            Debug.Log($"[GameManager] Sefer zaferi: Bölge {BattleSession.RegionId}, Seviye {BattleSession.LevelNumber}, +{goldEarned} altın.");
            return;
        }

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
        BattleCards.EndBattle();
        // Sefer yenilgisi ve pes etme 1 can düşürür (sınırsız can süresinde düşmez).
        if (BattleSession.IsCampaign) LivesSystem.LoseLife();
        SaveGame();
        Debug.Log($"[GameManager] Savaş kaybedildi! Level {PlayerData.currentBattleLevel}");
    }

    // ==================== SAHNE GEÇİŞLERİ ====================

    public void LoadBaseWorld()
    {
        IsInBattle = false;
        BattleSession.Clear();
        StartCoroutine(FadeAndLoadScene("BaseScene"));
        Debug.Log("[GameManager] Üs dünyasına geçiliyor...");
    }

    public void LoadCampaignMap()
    {
        IsInBattle = false;
        BattleSession.Clear();
        StartCoroutine(FadeAndLoadScene("CampaignScene"));
    }

    public void ReturnToCampaign()
    {
        IsInBattle = false;
        StartCoroutine(FadeAndLoadScene("CampaignScene"));
    }

    public void LoadMainMenu()
    {
        StartCoroutine(FadeAndLoadScene("MainMenuScene"));
        Debug.Log("[GameManager] Ana menüye geçiliyor...");
    }

    public void LoadHeroSelect()
    {
        StartCoroutine(FadeAndLoadScene("HeroSelectScene"));
        Debug.Log("[GameManager] Kahraman seçimine geçiliyor...");
    }

    /// <summary>
    /// Sahne geçişi sırasında fade-out efekti uygular
    /// </summary>
    private IEnumerator FadeAndLoadScene(string sceneName)
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
        DG.Tweening.DOTween.KillAll();
        SceneManager.LoadScene(sceneName);
    }

    // ==================== YARDIMCI ====================

    /// <summary>
    /// Altın Madeni'nin saniyede ürettiği altın
    /// </summary>
    /// <summary>2x maden (giriş takvimi ödülü) süresi devam ediyor mu.</summary>
    public bool MineBoostActive => PlayerData != null && PlayerData.mineBoostUntilTicks > DateTime.UtcNow.Ticks;

    public float GetGoldPerSecond() => GetGoldPerSecond(true);

    public float GetGoldPerSecond(bool includeBoost)
    {
        float boost = includeBoost && MineBoostActive ? 2f : 1f;
        if (Buildings.ContainsKey(BuildingType.GoldMine) && Buildings[BuildingType.GoldMine].isUnlocked)
        {
            int level = PlayerData != null ? Mathf.Clamp(PlayerData.apprenticeLevel, 1, Progression.MaxLevel) : 1;
            var progression = ApprenticeProgressionData.Instance;
            return Buildings[BuildingType.GoldMine].goldPerSecond * boost *
                (1f + (progression != null ? progression.MineBonus(level) : 10) / 100f);
        }
        return 0f;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Önce o ana kadarki üretim madene yazılır, sonra zaman damgası ilerler. Eskiden damga
        // doğrudan "şimdi"ye çekiliyordu: savaş/sefer ekranındaki ve arka plandaki üretim siliniyordu.
        if (PlayerData == null) return;
        if (pauseStatus)
        {
            ApplyOfflineGoldMineProduction();
            SaveGame();
        }
        else if (FindFirstObjectByType<BaseWorldController>() != null)
        {
            // Üs açıkken geri dönüldü: arka planda geçen süre şimdi eklenir
            // (savaştaysa üsse dönüşte BaseWorldController.Start ekler).
            ApplyOfflineGoldMineProduction();
        }
    }

    private void OnApplicationQuit()
    {
        if (PlayerData != null)
            ApplyOfflineGoldMineProduction();
        SaveGame();
    }
}
