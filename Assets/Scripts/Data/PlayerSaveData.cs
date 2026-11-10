using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Oyuncunun tüm kayıt verileri — JSON olarak kaydedilir/yüklenir
/// </summary>
[Serializable]
public class PlayerSaveData
{
    public int saveVersion = 0;
    // Genel
    public int gold = 50;                   // Başlangıç altını
    public int gems = 0;                    // Elmas (değerli para birimi)
    public int currentBattleLevel = 1;      // Şu anki savaş seviyesi
    public int highestBattleLevel = 1;      // Ulaşılan en yüksek seviye

    // Ana Savaşçı
    public HeroData hero = new HeroData();
    public int apprenticeLevel = 1;

    // Binalar
    public List<BuildingSaveEntry> buildings = new List<BuildingSaveEntry>();

    // Offline altın madeni zamanı
    public long goldMineLastCollectTime = 0; // Unix timestamp (ticks)
    public bool firstBaseGuideDone = false;
    public bool firstBattleTowerBuilt = false;
    public int goldMineStoredGold = 0;       // Madende biriken altın (cüzdana otomatik eklenmez)

    // Haritaya yerleştirilen binalar
    public List<PlacedBuildingData> placedBuildings = new List<PlacedBuildingData>();
    public List<PlacedDecorSave> placedDecor = new List<PlacedDecorSave>();
    public List<CampaignLevelProgress> campaignProgress = new List<CampaignLevelProgress>();

    // İnşa edilebilir alanın kayıtlı genişliği; görünüm yüksekliği buna göre hesaplanır.
    public int buildableSize = 17;
    public List<string> removedFenceIds = new List<string>();


    // Eski (v3) inşaatçı durumu: yalnız geçiş için okunur.
    public BuilderSaveData builderState = new BuilderSaveData();
    // v4: tek inşaatçının görevi. v5'ten itibaren yalnız geçiş için okunur.
    public BuilderTaskSave builderTask = new BuilderTaskSave();
    // v5: birden çok inşaatçı (1–5). Her görev builderIndex ile bir inşaatçıya bağlıdır.
    public int builderCount = 1;
    public List<BuilderTaskSave> builderTasks = new List<BuilderTaskSave>();
    public List<BuilderHutSave> builderHuts = new List<BuilderHutSave>();

    // Ana üsteki ağaç ve kayalar: ilk açılışta bir kez oluşturulur, sonra kayıttan okunur.
    // Temizlenenlerin yerine 18 saatte bir tek bir tane çıkar (terrainLastGrowTicks).
    public bool terrainResourcesSeeded;
    public List<TerrainResourceSave> terrainResources = new List<TerrainResourceSave>();
    public long terrainLastGrowTicks;

    // Can: en fazla 5, 30 dakikada bir yenilenir (LivesSystem). Sınırsız can süresi UTC tick.
    public int lives = 5;
    public long livesLastRegenTicks;
    public long unlimitedLivesUntilTicks;
    // Reklamla can: günde en fazla 3 (gün = yyyyMMdd).
    public int adLivesDay;
    public int adLivesCount;

    // Savaş kartları (BattleCards): ilk savaşta 1 kart bedava; jeton reklamsız kart açar.
    public bool freeCardUsed;
    public int cardTokens;
    // Kraliyet Üyeliği bitişi (UTC tick): tüm kartlar otomatik %15.
    public long royalUntilTicks;

    // 7 günlük giriş takvimi (DailyRewards): bu döngüde alınan gün sayısı ve son alma günü (yyyyMMdd).
    public int dailyStreak;
    public int dailyLastClaimDay;
    // Takvim ödülleri: 2x maden bitişi (UTC tick), sonraki yükseltmeye uygulanacak hızlandırma (dk).
    public long mineBoostUntilTicks;
    public int pendingSpeedupMinutes;

    // Elmas güçlendirmeleri (GemEconomy): İnşaat İksiri ve geçici inşaatçı bitişi (UTC tick).
    // Son kayıt anı (UTC tick): kopyalar arasından en yenisini seçmek için.
    public long lastSavedTicks;
    public long buildPotionUntilTicks;
    public long tempBuilderUntilTicks;
    // Elmas kazanma (GemRewards): reklamla elmas günü/sayısı, ödüllendirilmiş en yüksek Ana Üs seviyesi.
    public int gemAdDay;
    public int gemAdCount;
    public int gemRewardedBaseLevel;
    // Ödüllü reklam günlük hakları (AdRewards; gün = yyyyMMdd) ve Kraliyet günlük elması.
    public int adSpeedupDay;
    public int adSpeedupCount;
    public int adMineDay;
    public int adChestDay;
    public int royalGemDay;

    /// <summary>
    /// Varsayılan verilerle yeni oyuncu oluştur
    /// </summary>
    public static PlayerSaveData CreateNew(WeaponType selectedWeapon)
    {
        PlayerSaveData data = new PlayerSaveData();
        data.saveVersion = CurrentVersion;

        // Ana Savaşçı
        data.hero = new HeroData();
        // Tek kahraman: Warrior (eski silah seçimi yok sayılır).
        data.hero.weaponType = WeaponType.Axe;
        data.hero.level = 1;
        data.hero.ApplyLevelStats();
        data.apprenticeLevel = 1;

        // Başlangıç binaları
        // Ana Üs — hep açık, seviye 1
        data.buildings.Add(new BuildingSaveEntry
        {
            buildingType = BuildingType.MainBase,
            level = 1,
            isUnlocked = true
        });

        // Altın Madeni — baştan açık, seviye 1
        data.buildings.Add(new BuildingSaveEntry
        {
            buildingType = BuildingType.GoldMine,
            level = 1,
            isUnlocked = true
        });

        // Okçu Kulesi — kilitli
        data.buildings.Add(new BuildingSaveEntry
        {
            buildingType = BuildingType.ArcherTower,
            level = 0,
            isUnlocked = false
        });

        // Topçu Kulesi — kilitli
        data.buildings.Add(new BuildingSaveEntry
        {
            buildingType = BuildingType.CannonTower,
            level = 0,
            isUnlocked = false
        });

        // Manastır — Ana Üs seviye 4'te açılır
        data.buildings.Add(new BuildingSaveEntry
        {
            buildingType = BuildingType.Monastery,
            level = 0,
            isUnlocked = false
        });

        data.gold = 50;
        data.currentBattleLevel = 1;
        data.highestBattleLevel = 1;
        data.goldMineLastCollectTime = System.DateTime.UtcNow.Ticks;
        data.goldMineStoredGold = 0;
        data.placedBuildings = new List<PlacedBuildingData>();
        data.placedDecor = new List<PlacedDecorSave>();
        data.campaignProgress = new List<CampaignLevelProgress>();
        data.builderState = new BuilderSaveData();
        data.builderTask = new BuilderTaskSave();
        data.builderCount = 1;
        data.builderTasks = new List<BuilderTaskSave>();
        data.builderHuts = new List<BuilderHutSave>();
        data.gems = 0;
        data.buildableSize = 17;
        data.removedFenceIds = new List<string>();
        data.terrainResourcesSeeded = false;
        data.terrainResources = new List<TerrainResourceSave>();
        data.terrainLastGrowTicks = 0;

        return data;
    }

    public const int CurrentVersion = 5;
}

/// <summary>İnşaatçı kulübesinin konumu (index = inşaatçı sırası). Konum yoksa varsayılan yere konur.</summary>
[Serializable]
public class BuilderHutSave
{
    public bool hasPosition;
    public float x;
    public float y;
}

/// <summary>Ana üsteki bir ağaç ya da kaya (type: "Tree" / "Rock"; size: dünya birimi yükseklik).</summary>
[Serializable]
public class TerrainResourceSave
{
    public string id;
    public string type;
    public float x;
    public float y;
    public float size;
}

/// <summary>
/// Haritaya yerleştirilen bir binanın konum + tip kaydı
/// </summary>
[Serializable]
public class PlacedBuildingData
{
    public string slotId;
    public string buildingType; // BuildingType enum string'i
    public int level;
    public bool hasPosition;
    public float x;
    public float y;
}

[Serializable]
public class PlacedDecorSave
{
    public string instanceId;
    public string decorId;
    public int x;
    public int y;
}

[Serializable]
internal class LegacyPlacedBuildingData
{
    public string id;
    public string buildingType;
    public float x;
    public float y;
}

[Serializable]
internal class LegacyBasePositions
{
    public List<LegacyPlacedBuildingData> placedBuildings = new List<LegacyPlacedBuildingData>();
}

/// <summary>
/// İnşaatçı durumu kaydı
/// </summary>
[Serializable]
public class BuilderSaveData
{
    public bool isAvailable = true;
    public string currentTask = "";
    public float timeRemaining = 0f;
    public float totalTime = 0f;
}

/// <summary>
/// Bir inşaatçının görevi. kind: "build" (yeni yapı), "upgrade" (slotId'deki yapıyı
/// bir seviye yükselt), "remove" (kaldırma). Süre UTC tick ile tutulur.
/// </summary>
[Serializable]
public class BuilderTaskSave
{
    public int builderIndex;
    public string kind = "";
    public string label = "";
    public string slotId = "";
    public string buildingType = "";
    public long startTicks;
    public long endTicks;
    public float x;
    public float y;

    public bool IsActive => endTicks > 0;
}

/// <summary>
/// Bina kayıt girişi
/// </summary>
[Serializable]
public class BuildingSaveEntry
{
    public BuildingType buildingType;
    public int level;
    public bool isUnlocked;
}

/// <summary>
/// Oyuncu verisini kaydetme/yükleme yöneticisi
/// </summary>
public static class SaveSystem
{
    private const string SAVE_KEY = "MiniEmpireBuilder_SaveData";
    // Bir önceki sağlam kayıt (asıl kayıt bozulursa buradan açılır).
    private const string BACKUP_KEY = "MiniEmpireBuilder_SaveData_Backup";
    // Hiçbir kopya açılamazsa, yeni oyun üstüne yazmadan önce bozuk kayıt burada saklanır (destek için).
    private const string CORRUPT_KEY = "MiniEmpireBuilder_SaveData_Corrupt";
    private const string FILE_NAME = "save.json";

    /// <summary>Son yazılan/okunan sağlam JSON: bir sonraki kayıtta yedeğe geçer.</summary>
    private static string lastGoodJson;

    private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, FILE_NAME);

    /// <summary>
    /// Oyuncu verisini kaydet. Üç kopya: PlayerPrefs asıl + bir önceki kaydın yedeği +
    /// persistentDataPath'te dosya (önce geçici dosyaya yazılır, sonra yerine konur: yarım yazma kalmaz).
    /// </summary>
    public static void Save(PlayerSaveData data)
    {
        if (data == null) return;
        data.lastSavedTicks = DateTime.UtcNow.Ticks;
        string json = JsonUtility.ToJson(data);
        if (!string.IsNullOrEmpty(lastGoodJson) && lastGoodJson != json)
            PlayerPrefs.SetString(BACKUP_KEY, lastGoodJson);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
        WriteFileAtomic(json);
        lastGoodJson = json;
#if UNITY_EDITOR
        Debug.Log("[SaveSystem] Oyun kaydedildi.");
#endif
    }

    private static void WriteFileAtomic(string json)
    {
        try
        {
            string path = FilePath;
            string temp = path + ".tmp";
            System.IO.File.WriteAllText(temp, json);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            System.IO.File.Move(temp, path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] Kayıt dosyası yazılamadı: " + e.Message);
        }
    }

    private static string ReadFile()
    {
        try { return System.IO.File.Exists(FilePath) ? System.IO.File.ReadAllText(FilePath) : null; }
        catch { return null; }
    }

    /// <summary>JSON sağlam mı: tam (yarım kesilmemiş) ve çözülebiliyor. Sağlamsa kayıt anını verir.</summary>
    public static bool TryParse(string json, out long savedTicks)
    {
        savedTicks = -1;
        if (string.IsNullOrEmpty(json)) return false;
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) return false;
        try
        {
            var data = JsonUtility.FromJson<PlayerSaveData>(trimmed);
            if (data == null || data.saveVersion < 0) return false;
            savedTicks = data.lastSavedTicks;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Asıl kayıt, dosya ve yedekten sağlam olanların en yenisi. Hiçbiri sağlam değilse null
    /// (bozuk asıl kayıt CORRUPT_KEY'e saklanır).
    /// </summary>
    public static string PickBestJson()
    {
        string[] candidates =
        {
            PlayerPrefs.HasKey(SAVE_KEY) ? PlayerPrefs.GetString(SAVE_KEY) : null,
            ReadFile(),
            PlayerPrefs.HasKey(BACKUP_KEY) ? PlayerPrefs.GetString(BACKUP_KEY) : null
        };
        string best = null;
        long bestTicks = long.MinValue;
        bool anyExisted = false;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (string.IsNullOrEmpty(candidates[i])) continue;
            anyExisted = true;
            if (!TryParse(candidates[i], out long ticks)) continue;
            if (best == null || ticks > bestTicks)
            {
                best = candidates[i];
                bestTicks = ticks;
            }
        }
        if (best == null && anyExisted)
        {
            Debug.LogError("[SaveSystem] Hiçbir kayıt kopyası açılamadı; bozuk kayıt saklandı.");
            PlayerPrefs.SetString(CORRUPT_KEY, candidates[0] ?? candidates[1] ?? candidates[2]);
            PlayerPrefs.Save();
        }
        else if (best != null && best != candidates[0])
            Debug.LogWarning("[SaveSystem] Asıl kayıt bozuk ya da eski: yedekten açıldı.");
        return best?.Trim();
    }

    /// <summary>
    /// Oyuncu verisini yükle
    /// </summary>
    public static PlayerSaveData Load()
    {
        string json = PickBestJson();
        if (json != null)
        {
            lastGoodJson = json;
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            if (data.campaignProgress == null)
                data.campaignProgress = new List<CampaignLevelProgress>();
            if (data.placedDecor == null)
                data.placedDecor = new List<PlacedDecorSave>();
            data.apprenticeLevel = Mathf.Clamp(data.apprenticeLevel, 1, Progression.MaxLevel);
            if (data.builderTask == null) data.builderTask = new BuilderTaskSave();
            // The old enum occupied the same numeric value; migrate string slot ids too.
            if (data.placedBuildings != null)
                foreach (var building in data.placedBuildings)
                    if (building.buildingType == "WallBuilder")
                    {
                        building.buildingType = "Monastery";
                        building.slotId = "monastery_1";
                    }
            if (data.saveVersion < 2)
            {
                var legacy = JsonUtility.FromJson<LegacyBasePositions>(json);
                var migrated = new List<PlacedBuildingData>();
                var usedSlots = new HashSet<string>();
                if (legacy?.placedBuildings != null)
                foreach (var old in legacy.placedBuildings)
                {
                    if (!Enum.TryParse(old.buildingType, out BuildingType type)) continue;
                    BaseWorldLayout.Slot chosen = default;
                    foreach (var slot in BaseWorldLayout.ForBuilding(type))
                    {
                        if (usedSlots.Contains(slot.id)) continue;
                        chosen = slot;
                        break;
                    }
                    if (string.IsNullOrEmpty(chosen.id)) continue;
                    usedSlots.Add(chosen.id);
                    var state = data.buildings?.Find(entry => entry.buildingType == type);
                    migrated.Add(new PlacedBuildingData
                    {
                        slotId = chosen.id,
                        buildingType = type.ToString(),
                        level = state != null ? state.level : 1
                    });
                }
                data.placedBuildings = migrated;
                data.buildableSize = 17;
                data.removedFenceIds = new List<string>();
                data.saveVersion = 2;
                Save(data);
                Debug.Log($"[SaveSystem] Üs kaydı slotlara taşındı: {migrated.Count} bina.");
            }
            Debug.Log("[SaveSystem] Kayıt yüklendi.");
            if (data.saveVersion < 3)
            {
                data.firstBaseGuideDone = true;
                data.saveVersion = 3;
                Save(data);
            }
            if (data.saveVersion < 4)
            {
                // Eski inşaatçı geri sayımı oyun kapalıyken durmuştu: kalan süre şimdiden başlar.
                var old = data.builderState;
                if (old != null && !old.isAvailable && old.timeRemaining > 0f)
                {
                    long now = DateTime.UtcNow.Ticks;
                    data.builderTask = new BuilderTaskSave
                    {
                        kind = "build",
                        label = old.currentTask,
                        startTicks = now,
                        endTicks = now + TimeSpan.FromSeconds(old.timeRemaining).Ticks
                    };
                }
                data.builderState = new BuilderSaveData();
                // Her binanın kendi seviyesi: eski kayıtta türün seviyesi tüm kopyalara yazılmıştı.
                if (data.placedBuildings != null)
                    foreach (var placed in data.placedBuildings)
                    {
                        var state = data.buildings?.Find(entry => entry.buildingType.ToString() == placed.buildingType);
                        if (placed.level <= 0) placed.level = state != null ? Mathf.Max(1, state.level) : 1;
                    }
                data.saveVersion = 4;
                Save(data);
            }
            if (data.builderTasks == null) data.builderTasks = new List<BuilderTaskSave>();
            if (data.builderHuts == null) data.builderHuts = new List<BuilderHutSave>();
            if (data.terrainResources == null) data.terrainResources = new List<TerrainResourceSave>();
            if (data.saveVersion < 5)
            {
                // Tek görev listeye taşınır; 1 inşaatçı ve 0 elmasla devam edilir.
                data.builderTasks.Clear();
                if (data.builderTask != null && data.builderTask.IsActive)
                {
                    data.builderTask.builderIndex = 0;
                    data.builderTasks.Add(data.builderTask);
                }
                data.builderTask = new BuilderTaskSave();
                data.builderCount = 1;
                data.saveVersion = 5;
                Save(data);
            }
            // Tek kahraman: Okçu/Mızrakçı seçmiş eski oyuncular da Warrior olur.
            if (data.hero == null) data.hero = new HeroData();
            data.hero.weaponType = WeaponType.Axe;
            data.hero.level = Mathf.Clamp(data.hero.level, 1, Progression.MaxLevel);
            data.hero.ApplyLevelStats();
            data.builderCount = Mathf.Clamp(data.builderCount, 1, Progression.MaxBuilders);
            data.gems = Mathf.Max(0, data.gems);
            data.builderTasks.RemoveAll(task => task == null || !task.IsActive);
            return data;
        }

        Debug.Log("[SaveSystem] Kayıt bulunamadı.");
        return null;
    }

    /// <summary>
    /// Kayıt var mı kontrol et
    /// </summary>
    public static bool HasSave()
    {
        return PlayerPrefs.HasKey(SAVE_KEY) || PlayerPrefs.HasKey(BACKUP_KEY) || System.IO.File.Exists(FilePath);
    }

    /// <summary>
    /// Kaydı sil (test için)
    /// </summary>
    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.DeleteKey(BACKUP_KEY);
        lastGoodJson = null;
        try { if (System.IO.File.Exists(FilePath)) System.IO.File.Delete(FilePath); }
        catch (Exception e) { Debug.LogWarning("[SaveSystem] Kayıt dosyası silinemedi: " + e.Message); }
        Debug.Log("[SaveSystem] Kayıt silindi.");
    }
}
