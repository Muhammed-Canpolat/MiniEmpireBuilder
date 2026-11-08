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


    // İnşaatçı durumu (sahne kapanınca kaydedilir)
    public BuilderSaveData builderState = new BuilderSaveData();

    /// <summary>
    /// Varsayılan verilerle yeni oyuncu oluştur
    /// </summary>
    public static PlayerSaveData CreateNew(WeaponType selectedWeapon)
    {
        PlayerSaveData data = new PlayerSaveData();
        data.saveVersion = 3;

        // Ana Savaşçı
        data.hero = new HeroData();
        data.hero.weaponType = selectedWeapon;
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
        data.buildableSize = 17;
        data.removedFenceIds = new List<string>();

        return data;
    }
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

    /// <summary>
    /// Oyuncu verisini kaydet
    /// </summary>
    public static void Save(PlayerSaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
        Debug.Log("[SaveSystem] Oyun kaydedildi.");
    }

    /// <summary>
    /// Oyuncu verisini yükle
    /// </summary>
    public static PlayerSaveData Load()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            if (data.campaignProgress == null)
                data.campaignProgress = new List<CampaignLevelProgress>();
            if (data.placedDecor == null)
                data.placedDecor = new List<PlacedDecorSave>();
            data.apprenticeLevel = Mathf.Clamp(data.apprenticeLevel, 1, 5);
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
        return PlayerPrefs.HasKey(SAVE_KEY);
    }

    /// <summary>
    /// Kaydı sil (test için)
    /// </summary>
    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        Debug.Log("[SaveSystem] Kayıt silindi.");
    }
}
