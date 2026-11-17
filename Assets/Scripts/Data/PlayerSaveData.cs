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
    /// <summary>6. oturum (2.4, v12): işlenmiş gerçek para satın almaları ("ürün|işlemKimliği"); aynı işlem iki kez elmas vermez.</summary>
    public List<string> iapLedger = new List<string>();
    /// <summary>6. oturum (4.1): açılmış Ana Üs arazi genişlemesi (0..4). -1 = eski kayıt: Ana Üs kademesi kadar açık sayılır (LandExpansion.OpenedTier).</summary>
    public int landOpened = -1;
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
    // 9. oturum: her kayıtta artan sayaç. En yeni kopya önce buna göre seçilir (cihaz saati geri alınsa da doğru kopya açılır);
    // eski kayıtta 0 gelir ve kayıt anına bakılır.
    public long saveSerial;
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
    // 6. oturum (AŞAMA 5): savaş kartı reklamı günlük hakkı (yeni alan; eski kayıtta 0 = bugün hiç izlenmedi, göç gerekmez).
    public int adCardDay;
    public int adCardCount;
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
        data.landOpened = 0;   // 6. oturum (4.1): yeni oyunda genişlemeler elle açılır
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

        // Okçu Kışlası — kilitli
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

        // Manastır — kilitli (açılış seviyesi: Progression.FirstUnlockLevel)
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
        // (9. oturum, O-1) Burada depo yeniden 0'lanıyordu: yukarıdaki 40 altın eziliyor, rehberin ilk adımı boşa çıkıyordu.
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

    public const int CurrentVersion = 13;

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
    public int raidIntroSeen;       // D120: akın tanıtımı bit maskesi (1 ATEŞ, 2 dolu atış, 4 atış yeri, 8 HARİTA, 16 TAKİP, 32/64/128 yetenek); eksik alan → 0
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
    // v13 (6. oturum 3.1–3.3): ilk giriş tanıtımı, zorunlu Okçuluk öğreticisi, ada seviyeleri Ana Üs'ten bağımsız.
    public bool introSeen;          // ilk giriş tanıtımı izlendi (eski kayıtta adaya girmişse göçte true)
    public int buildTutorial;       // 0 başlamadı · 1 İNŞA ET'e bas · 2 Okçuluk Alanı'nı seç · 3 yerleştir · 4 inşa sürüyor · 5 bitti
    public bool levelsIndependent;  // v13: keepLevel/archerLevel artık Ana Üs'ü izlemez (göçte bir kez sabitlendi)
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
