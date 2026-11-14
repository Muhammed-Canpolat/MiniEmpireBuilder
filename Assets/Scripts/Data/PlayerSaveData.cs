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
    public int decorLayoutVersion;   // 4. oturum (2.1): 1 = küçük dekorlar 1×1 (DecorPlacementController.LayoutVersion)
    public List<CampaignLevelProgress> campaignProgress = new List<CampaignLevelProgress>();

    // İnşa edilebilir alanın kayıtlı genişliği; görünüm yüksekliği buna göre hesaplanır.
    public int buildableSize = 17;
    public List<string> removedFenceIds = new List<string>();
    // Kilitli araziden kalan doğa nesneleri (kimlik: tür_x_y). natureBaselineLevel: doğa ilk spawn edildiğinde
    // açık olan Ana Üs seviyesi (0 = henüz yok; eski kayıtlarda o seviyedeki temiz alan temiz kalır).
    public List<string> removedNatureIds = new List<string>();
    public int natureBaselineLevel;


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
    // Bekleme süresi tablosunun sürümü (TimingConfig.version); değişince devam eden yükseltmeler oranlanır.
    public int timingVersion;
    // Oyunun gördüğü en ileri saat (UTC tick): saat geri alınınca günlük sınırlar yenilenmesin (GameClock).
    public long clockHighWaterTicks;
    public long buildPotionUntilTicks;
    public long tempBuilderUntilTicks;
    // Elmas kazanma (GemRewards): reklamla elmas günü/sayısı, ödüllendirilmiş en yüksek Ana Üs seviyesi.
    public int gemAdDay;
    public int gemAdCount;
    public int gemRewardedBaseLevel;
    // Ödüllü reklam günlük hakları (AdRewards; gün = yyyyMMdd) ve Kraliyet günlük elması.
    public int adSpeedupDay;
    public int adSpeedupCount;
    // Kereste Sevkiyatı: günlük sayaç (gün = GameClock.TodayId). Eski kayıtta 0 gelir.
    public int lumberShipDay;
    public int lumberShipCount;
    public int adMineDay;
    public int adChestDay;
    // Zaferde 2x ödül reklamı günlük sayacı (AŞAMA 4; Economy.victoryDoubleAdsPerDay > 0 ise sınırlı). Eski kayıtta 0.
    public int adDoubleDay;
    public int adDoubleCount;
    // Ada Tüccarı (AŞAMA 4): günlük paket sayacı. Eski kayıtta 0.
    public int traderDay;
    public int traderCount;
    public int royalGemDay;
    // Günlük görevler (DailyQuests): gün, seçilen 3 görev, ilerleme, alınanlar, bonus.
    public int questDay;
    public List<int> questKinds = new List<int>();
    public List<int> questProgress = new List<int>();
    public List<bool> questClaimed = new List<bool>();
    public bool questBonusClaimed;
    // Sonsuz Sefer en iyi kat; Ejder İstilası ilk zafer ödülünün alındığı hafta (yıl × 100 + hafta).
    public int endlessBestFloor;
    public int invasionRewardWeek;
    // v6: Martı Adası (Docs/design/island_base.md). Eski kayıtta null gelir; yüklemede oluşturulur.
    public IslandSaveData island = new IslandSaveData();
    // v8: Kart Atölyesi (Docs/design/card_workshop.md §8). Eski kayıtta null/boş gelir; yüklemede onarılır.
    public WorkshopSaveData workshop = new WorkshopSaveData();

    /// <summary>
    /// Varsayılan verilerle yeni oyuncu oluştur
    /// </summary>
    public static PlayerSaveData CreateNew(WeaponType selectedWeapon)
    {
        PlayerSaveData data = new PlayerSaveData();
        data.saveVersion = CurrentVersion;
        data.timingVersion = TimingConfig.Instance.version;
        // İlk görüşte madende biraz altın: rehberin "madene dokun" adımı boşa çıkmasın.
        data.goldMineStoredGold = 40;

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
        data.removedNatureIds = new List<string>();
        data.natureBaselineLevel = 0;
        data.terrainResourcesSeeded = false;
        data.terrainResources = new List<TerrainResourceSave>();
        data.terrainLastGrowTicks = 0;
        data.island = new IslandSaveData();
        data.workshop = new WorkshopSaveData();
        EnsureWorkshopEntry(data);
        EnsureBaseBondEntries(data);

        return data;
    }

    public const int CurrentVersion = 11;

    /// <summary>Kışla ve Köylü Evi türleri kayıtta yoksa ekler (v9; eski kayıt göçü ve yeni oyun): açılış seviyesindeyse açık, yoksa kilitli.</summary>
    public static void EnsureBaseBondEntries(PlayerSaveData data)
    {
        if (data == null) return;
        if (data.buildings == null) data.buildings = new List<BuildingSaveEntry>();
        var main = data.buildings.Find(entry => entry.buildingType == BuildingType.MainBase);
        foreach (var type in new[] { BuildingType.Barracks, BuildingType.House })
        {
            if (data.buildings.Exists(entry => entry.buildingType == type)) continue;
            bool open = main != null && main.level >= Progression.FirstUnlockLevel(type);
            data.buildings.Add(new BuildingSaveEntry { buildingType = type, level = open ? 1 : 0, isUnlocked = open });
        }
    }

    /// <summary>Kart Atölyesi türü kayıtta yoksa ekler (eski kayıt göçü ve yeni oyun): Ana Üs 5'te açık, yoksa kilitli.</summary>
    public static void EnsureWorkshopEntry(PlayerSaveData data)
    {
        if (data == null) return;
        if (data.buildings == null) data.buildings = new List<BuildingSaveEntry>();
        if (data.buildings.Exists(entry => entry.buildingType == BuildingType.CardWorkshop)) return;
        var main = data.buildings.Find(entry => entry.buildingType == BuildingType.MainBase);
        bool open = main != null && main.level >= Progression.Workshop.UnlockMainBaseLevel;
        data.buildings.Add(new BuildingSaveEntry { buildingType = BuildingType.CardWorkshop, level = open ? 1 : 0, isUnlocked = open });
    }
}

/// <summary>Atölye üretim işi: kart, başlangıç/bitiş (UTC tick) ve üretim başlarken kilitlenen hak sayısı.</summary>
[Serializable]
public class WorkshopJob
{
    public int card;
    public long startTicks;
    public long endTicks;
    public int uses;
}

/// <summary>
/// Kart Atölyesi kaydı: kart başına kalan hak (dizin = <see cref="BattleCard"/>), üretim kuyruğu, ilk bedava üretim bayrağı ve
/// günlük reklam sayacı (gün = yyyyMMdd). Uzunluk eksikse yüklemede büyütülür (yeni kart eklenirse stok kaybolmaz).
/// </summary>
[Serializable]
public class WorkshopSaveData
{
    public int[] stock = new int[BattleCards.Count];
    public List<WorkshopJob> queue = new List<WorkshopJob>();
    public bool firstFreeUsed;
    public int adDay;
    public int adsToday;

    /// <summary>Boş/eksik alanları onarır ve negatif değerleri sıfırlar.</summary>
    public void Repair()
    {
        if (stock == null) stock = new int[BattleCards.Count];
        if (stock.Length < BattleCards.Count) Array.Resize(ref stock, BattleCards.Count);
        for (int i = 0; i < stock.Length; i++) stock[i] = Mathf.Max(0, stock[i]);
        if (queue == null) queue = new List<WorkshopJob>();
        queue.RemoveAll(job => job == null || job.card < 0 || job.card >= BattleCards.Count);
    }
}

/// <summary>
/// Martı Adası kaydı. MVP'de kullanılanlar: duyuru, en yüksek Akın, Et/Odun cüzdanı, öğretici adımı, oto-ateş tercihi.
/// Kale/Okçu seviyeleri Faz 2'de (ada yükseltmeleri) gerçek anlam kazanır; şimdiden kayıtta durur.
/// </summary>
[Serializable]
public class IslandSaveData
{
    public bool announced;          // "Ufukta bir ada!" afişi gösterildi
    public int keepLevel = 1;       // Kıyı Kalesi (etkin seviye: Progression.Island.EffectiveKeepLevel)
    public int archerLevel = 1;     // Okçu Kaptan
    public int meat;                // Et (Faz 2'de harcanır)
    public int wood;                // Odun (Faz 2'de harcanır)
    public int highestAkin;         // kazanılan en yüksek Akın
    public int lastAkin = 1;        // son seçilen Akın
    public int tutorialStep;        // 0 başlamadı · 1 ATEŞ · 2 atış yeri · 3 ödül · 4 bitti
    public bool autoFire;           // oto-ateş tercihi
    public long nextRaidTicks;      // Faz 3: sonraki baskın (UTC tick)
    public string lastReport = "";  // Faz 3: "Sen yokken" tek satırı
    // Faz D (island_base_v2 §7.8): zamanlı baskın. nextRaidTicks (üstte) = bekleyen baskının varış anı T0 (0: planlı baskın yok).
    // Eski kayıtta hepsi 0/boş: ilk Tick'te (Akın >= 5 ise) planlanır. Kayıt sürümü değişmedi (yalnız yeni alanlar). Konum: `task` hâlâ island'ın son alanı (eski sürüm testleri regex'le keser).
    public int raidBeach;           // hedef kumsal indeksi (0..3 = A..D)
    public int raidSeed;            // baskının rastgelelik tohumu
    public int raidSerial;          // kaç baskın planlandı (tohum kaynağı)
    public int raidsResolved;       // çözülen (çevrimdışı ya da canlı) baskın sayısı
    public long raidSeenTicks;      // zamanlayıcının en son çalıştığı an (24 sa+ kapalı kalırsa saat durur)
    public string raidReportKind = "";   // "" okunmamış rapor yok · "offline" · "won" · "lost"
    public int raidReportBeach, raidReportTowers, raidReportWood, raidReportMeat;
    // Faz E1 (island_base_v2 §5–§7.5, §20): Kışla bayrağı ve Altın Damarı. Eski kayıtta hepsi 0 (bayrak S1, damar boş): güvenli; kayıt sürümü değişmedi.
    public int flagStair;           // Kışla toplanma bayrağı: 0 = S1 zirve · 1 = S2 batı · 2 = S3 doğu
    public float veinStock;         // Altın Damarı: toplanmamış Ana Üs altını (Z3 açıkken birikir)
    public long veinTick;           // damar stokunun son güncellendiği an (UTC tick; 0 = başlamadı)
    // Faz C (island_base_v2 §7.7, §8): bina savaş durumu/onarım ve günlük tekrar ödülü sayacı. Eski kayıtta boş/0: güvenli.
    public List<IslandBuildingSave> buildings = new List<IslandBuildingSave>();
    public string replayDay = "";   // yyyy-MM-dd (UTC)
    public int replaysToday;        // bugün verilen tekrar ödülü sayısı (Progression.Island.ReplayRewardsPerDay tavanı)
    // v10 (AŞAMA 2): boş başlangıç, ilk yapım bedava, kesilen ağaçlar. Konum: villageSeeded'den ÖNCE (eski sürüm testleri
    // villageSeeded…task bloğunu regex'le keser). Eski kayıtta göç: sahip olunan türlerin biti yazılır, freshStart=false.
    public bool freshStart;         // ada boş başlangıçla (yalnız kale) kuruldu
    public int freeBuildMask;       // bit (int)BuildingKind = o türün ilk yapımı kullanıldı (bedava hakkı bitti)
    public List<string> clearedTrees = new List<string>();   // kesilen ağaçlar "sütun,satır" (bir daha büyümez)
    public List<string> clearedNature = new List<string>();  // 4. oturum (1.4): temizlenen çalı/kaya "bush:c,r" / "rock:c,r"
    public bool legacyVillage;      // v10 öncesi ilerlemesi olan ama köyü tohumlanmamış kayıt: ilk açılışta hazır köy kurulur
    // v11 (AŞAMA 3): ada adı. Boş ad = "Martı Adası". nameSeal = ad+değişiklik sayısının basit özeti (elle düzenleme tespiti).
    public string islandName = "";
    public int nameChanges;         // yapılan ad değişikliği sayısı (ilk adlandırma yeni oyuncuda 1. değişiklik)
    public int nameSeal;            // IslandNameRules.Seal(islandName, nameChanges)
    public bool nameOffered;        // ada adı sorusu/teklifi gösterildi
    // v7 / Faz B (island_base_v2 §9, §17): yerleştirilmiş binalar, bölge maskesi, Usta görevi. Eski kayıtta boş/varsayılan: hazır köy tohumlanır.
    public bool villageSeeded;      // hazır köy placed listesine yazıldı mı (v6'dan geçişte ilk açılışta yapılır)
    public List<IslandPlacedBuilding> placed = new List<IslandPlacedBuilding>();
    public int nextBuildingSerial = 1;
    public int zoneMask = 1;        // bit i = bölge Zi açık (Z0 hep açık)
    public IslandBuilderTask task = new IslandBuilderTask();
}

/// <summary>Yerleştirilmiş ada binası (Kale hariç; sabit). level 0 = inşaatta (yeni bina). Üretim: stok + son tick (UTC).</summary>
[Serializable]
public class IslandPlacedBuilding
{
    public string id;               // kararlı kimlik (hazır köy: "Tower@19,10"; yeni: "House#3")
    public string kind;             // IslandLayout.BuildingKind adı
    public int level = 1;
    public int col, row;            // sol-üst hücre
    public float stock;             // toplanmamış üretim (Odun/Et)
    public long lastTick;           // stok en son bu an güncellendi (UTC tick)
    public int workers = -1;        // elle işçi sayısı; -1 = otomatik
}

/// <summary>Ada Ustası'nın tek görevi (UTC tick). kind: "" yok · "build" yeni bina · "upgrade" seviye · "zone" bölge keşfi.</summary>
[Serializable]
public class IslandBuilderTask
{
    public string kind = "";
    public string buildingId = "";
    public int zone;
    public int targetLevel;
    public long startTicks, endTicks;
    public bool Active => !string.IsNullOrEmpty(kind);
}

/// <summary>Ada binasının kalıcı savaş durumu: can oranı ve onarımın biteceği an (UTC tick; 0: onarım yok).</summary>
[Serializable]
public class IslandBuildingSave
{
    public string id;               // "Tower@19,10" gibi: tür + sol-üst hücre
    public float hpFraction = 1f;   // 0 = yıkık
    public long repairEndTicks;
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
    /// <summary>
    /// Süre tablosu değiştiyse (TimingConfig.version) devam eden yükseltmelerin kalan süresi yeni/eski oranıyla
    /// ölçeklenir: yarısı geçmiş 6 saatlik iş, yeni tabloda 1 saatse 30 dk kalır. Yeni kayıt doğrudan güncel sürümle başlar.
    /// </summary>
    public static void MigrateTimings(PlayerSaveData data)
    {
        var config = TimingConfig.Instance;
        if (data == null || data.timingVersion >= config.version) return;
        long now = System.DateTime.UtcNow.Ticks;
        if (data.timingVersion < 2 && data.builderTasks != null)
            foreach (var task in data.builderTasks)
            {
                if (task == null || !task.IsActive || task.endTicks <= now || task.kind != "upgrade") continue;
                int target = TargetLevelOf(data, task);
                int oldSeconds = TimingConfig.LegacyUpgradeSecondsV1[Mathf.Clamp(target, 0, TimingConfig.LegacyUpgradeSecondsV1.Length - 1)];
                int newSeconds = config.UpgradeSecondsTo(target);
                if (oldSeconds <= 0 || newSeconds >= oldSeconds) continue;
                double ratio = newSeconds / (double)oldSeconds;
                task.endTicks = now + (long)((task.endTicks - now) * ratio);
                task.startTicks = task.endTicks - System.TimeSpan.FromSeconds(newSeconds).Ticks;
            }
        data.timingVersion = config.version;
    }

    private static int TargetLevelOf(PlayerSaveData data, BuilderTaskSave task)
    {
        if (data.placedBuildings != null)
            foreach (var placed in data.placedBuildings)
                if (placed != null && placed.slotId == task.slotId) return placed.level + 1;
        return 2;
    }

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
            if (data.removedNatureIds == null) data.removedNatureIds = new List<string>();
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
            // v6: ada kaydı (eski kayıtta JsonUtility alanı oluşturmaz/null bırakabilir).
            if (data.island == null) data.island = new IslandSaveData();
            data.island.keepLevel = Mathf.Max(1, data.island.keepLevel);
            data.island.archerLevel = Mathf.Max(1, data.island.archerLevel);
            data.island.lastAkin = Mathf.Max(1, data.island.lastAkin);
            data.island.meat = Mathf.Max(0, data.island.meat);
            data.island.wood = Mathf.Max(0, data.island.wood);
            data.island.highestAkin = Mathf.Max(0, data.island.highestAkin);
            if (data.saveVersion < 6)
            {
                data.saveVersion = 6;
                Save(data);
            }
            // v7 (Faz B): yerleştirilmiş binalar/bölge maskesi/Usta görevi. v5/v6'dan gelen kayıtta alanlar boştur;
            // hazır köy ilk açılışta placed listesine tohumlanır (seviyeler Faz C ile aynı: kayıp yok).
            if (data.island.placed == null) data.island.placed = new List<IslandPlacedBuilding>();
            if (data.island.task == null) data.island.task = new IslandBuilderTask();
            data.island.zoneMask |= 1;
            IslandEconomy.OpenZonesUnderBuildings(data.island);   // bölge sınırı değişikliği: binası olan bölge açık kalır
            data.island.nextBuildingSerial = Mathf.Max(1, data.island.nextBuildingSerial);
            if (data.saveVersion < 7)
            {
                data.saveVersion = 7;
                Save(data);
            }
            // v8: Kart Atölyesi. Eski kayıtta workshop boştur; bina türü kaydı Ana Üs seviyesine göre eklenir. Mevcut veriye dokunulmaz.
            if (data.workshop == null) data.workshop = new WorkshopSaveData();
            data.workshop.Repair();
            PlayerSaveData.EnsureWorkshopEntry(data);
            if (data.saveVersion < 8)
            {
                data.saveVersion = 8;
                Save(data);
            }
            // v9: Kışla + Köylü Evi. Eski kayıtta tür girdisi yoktur; Ana Üs seviyesine göre eklenir, mevcut veriye dokunulmaz.
            PlayerSaveData.EnsureBaseBondEntries(data);
            if (data.saveVersion < 9)
            {
                data.saveVersion = 9;
                Save(data);
            }
            // v10 (AŞAMA 2): boş başlangıç + ilk yapım bedava + kesilen ağaçlar. Eski kayıtta hiçbir şey alınmaz: sahip olunan
            // türlerin (ve Kale'nin) bedava hakkı kullanılmış sayılır; ilerlemesi olup köyü henüz tohumlanmamış kayıt hazır köyü alır.
            if (data.island.clearedTrees == null) data.island.clearedTrees = new List<string>();
            if (data.saveVersion < 10)
            {
                var isl = data.island;
                isl.freshStart = false;
                isl.clearedTrees.Clear();
                isl.freeBuildMask = isl.villageSeeded ? IslandEconomy.OwnedKindsMask(isl.placed) : 0;
                isl.legacyVillage = !isl.villageSeeded && (isl.highestAkin > 0 || isl.tutorialStep > 0 || (isl.buildings != null && isl.buildings.Count > 0));
                data.saveVersion = 10;
                Save(data);
            }
            // v11 (AŞAMA 3): ada adı. Eski kayıtta ad boş ("Martı Adası" gösterilir), teklif henüz yapılmadı.
            if (data.saveVersion < 11)
            {
                data.island.islandName = "";
                data.island.nameChanges = 0;
                data.island.nameOffered = false;
                data.island.nameSeal = IslandNameRules.Seal("", 0);
                data.saveVersion = 11;
                Save(data);
            }
            MigrateTimings(data);
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
