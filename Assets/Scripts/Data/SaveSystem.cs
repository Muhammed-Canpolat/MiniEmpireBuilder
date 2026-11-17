using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Son yüklemenin sonucu. <see cref="Failed"/>: kayıt VAR ama açılamadı — üstüne yazılmaz.</summary>
public enum SaveLoadStatus { NoSave, Loaded, Failed }

/// <summary>
/// Oyuncu verisini kaydetme/yükleme (9. oturum, TECH_AUDIT K-1…K-5).
///
/// Kopyalar: PlayerPrefs asıl + PlayerPrefs yedek (bir önceki kayıt) + <c>persistentDataPath/save.json</c>.
/// Yedek klasörü <c>persistentDataPath/SaveBackups</c>: göç öncesi kayıt, açılamayan kopyalar (karantina), silinen kayıt.
///
/// Güvenceler:
/// - Kayıt açılamazsa (<see cref="SaveLoadStatus.Failed"/>) bütün kopyalar yerinde kalır, karantinaya da kopyalanır ve
///   <see cref="Save"/> hiçbir şey yazmaz. Yalnız oyuncunun açıkça onayladığı yeni oyun (<see cref="DeleteSave"/>) bu kilidi kaldırır.
/// - Göç bellekte çalışır ve tek kayıtla biter; göçten önce orijinal JSON dosya olarak saklanır.
/// - En yeni kopya kayıt sayacına (<see cref="PlayerSaveData.saveSerial"/>) göre seçilir; cihaz saati geri alınsa da doğru kopya açılır.
///
/// YENİ SÜRÜM EKLEMEK: <see cref="PlayerSaveData.CurrentVersion"/> artır, <see cref="Migrate"/> sonuna
/// <c>if (data.saveVersion &lt; N) { …; data.saveVersion = N; changed = true; }</c> bloğu ekle (içinde Save çağırma),
/// Session9SaveTests.B1 yeni sürümü kendiliğinden kapsar. Sahneye bağlı onarım yazma: göç burada, kayıt verisi üzerinde yapılır.
/// </summary>
public static class SaveSystem
{
    private const string SAVE_KEY = "MiniEmpireBuilder_SaveData";
    // Bir önceki sağlam kayıt (asıl kayıt bozulursa buradan açılır).
    private const string BACKUP_KEY = "MiniEmpireBuilder_SaveData_Backup";
    // Hiçbir kopya açılamazsa bozuk kayıt burada da saklanır (eski sürümlerle uyum; asıl arşiv SaveBackups klasörü).
    private const string CORRUPT_KEY = "MiniEmpireBuilder_SaveData_Corrupt";
    private const string FILE_NAME = "save.json";
    private const string BACKUP_FOLDER = "SaveBackups";
    /// <summary>Yedek klasöründe tür başına tutulan en fazla dosya (eskiler silinir).</summary>
    private const int KeepPerKind = 4;

    /// <summary>Son yazılan/okunan sağlam JSON: bir sonraki kayıtta yedeğe geçer.</summary>
    private static string lastGoodJson;
    private static long lastSerial;

    public static SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.NoSave;
    /// <summary>Son yükleme hatasının kısa açıklaması (destek için; boş = hata yok).</summary>
    public static string LastLoadError { get; private set; } = "";

    /// <summary>Yalnız testler: göçten sonra çağrılır (göç hatasını canlandırmak için). Oyunda hep null.</summary>
    internal static Action MigrationFaultForTests;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);
    private static string TempPath => FilePath + ".tmp";
    public static string BackupDirectory => Path.Combine(Application.persistentDataPath, BACKUP_FOLDER);

    internal static void ResetForTests()
    {
        lastGoodJson = null;
        lastSerial = 0;
        LastLoadStatus = SaveLoadStatus.NoSave;
        LastLoadError = "";
        archivedThisSession.Clear();
    }

    // ==================== KAYDET ====================

    /// <summary>
    /// Oyuncu verisini kaydet. Üç kopya: PlayerPrefs asıl + bir önceki kaydın yedeği + dosya (önce geçici dosyaya yazılır,
    /// sonra yerine konur). Açılamayan bir kayıt varken (<see cref="SaveLoadStatus.Failed"/>) hiçbir şey yazmaz.
    /// </summary>
    public static void Save(PlayerSaveData data)
    {
        if (data == null) return;
        if (LastLoadStatus == SaveLoadStatus.Failed)
        {
            Debug.LogWarning("[SaveSystem] Açılamayan bir kayıt var; üstüne yazılmadı.");
            return;
        }
        data.lastSavedTicks = DateTime.UtcNow.Ticks;
        data.saveSerial = Math.Max(data.saveSerial, lastSerial) + 1;
        lastSerial = data.saveSerial;
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
            string temp = TempPath;
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                // Üzerine yazarak değiştir: arada "dosya yok" anı kalmaz. Desteklenmeyen dosya sisteminde sil + taşı
                // (o an kesilirse sağlam geçici dosya yüklemede aday sayılır).
                try { File.Replace(temp, path, null); }
                catch (Exception)
                {
                    File.Delete(path);
                    File.Move(temp, path);
                }
            }
            else File.Move(temp, path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] Kayıt dosyası yazılamadı: " + e.Message);
        }
    }

    private static string ReadFile(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] Kayıt dosyası okunamadı: " + e.Message);
            return null;
        }
    }

    // ==================== KOPYA SEÇİMİ ====================

    /// <summary>JSON sağlam mı: tam (yarım kesilmemiş), çözülebiliyor ve gerçek bir kayıt (en az bir bina girdisi). Sağlamsa kayıt anını verir.</summary>
    public static bool TryParse(string json, out long savedTicks) => TryParse(json, out savedTicks, out _);

    private static bool TryParse(string json, out long savedTicks, out long serial)
    {
        savedTicks = -1;
        serial = 0;
        if (string.IsNullOrEmpty(json)) return false;
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) return false;
        try
        {
            var data = JsonUtility.FromJson<PlayerSaveData>(trimmed);
            if (data == null || data.saveVersion < 0) return false;
            // "{}" gibi boş nesne de çözülür ama kayıt değildir: her kayıtta en az Ana Üs girdisi bulunur.
            if (data.buildings == null || data.buildings.Count == 0) return false;
            savedTicks = data.lastSavedTicks;
            serial = data.saveSerial;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private struct Candidate { public string source; public string json; }

    private static List<Candidate> Candidates()
    {
        var list = new List<Candidate>();
        void Add(string source, string json) { if (!string.IsNullOrEmpty(json)) list.Add(new Candidate { source = source, json = json }); }
        Add("prefs", PlayerPrefs.HasKey(SAVE_KEY) ? PlayerPrefs.GetString(SAVE_KEY) : null);
        Add("file", ReadFile(FilePath));
        Add("backup", PlayerPrefs.HasKey(BACKUP_KEY) ? PlayerPrefs.GetString(BACKUP_KEY) : null);
        Add("temp", ReadFile(TempPath));   // yerine taşınamadan kalmış kayıt
        return list;
    }

    /// <summary>
    /// Asıl kayıt, dosya, yedek ve yarım kalmış geçici dosyadan sağlam olanların en yenisi (önce kayıt sayacı, eşitse kayıt anı).
    /// Hiçbiri sağlam değilse null.
    /// </summary>
    public static string PickBestJson() => PickBestJson(out _);

    private static string PickBestJson(out bool anyExisted)
    {
        var candidates = Candidates();
        anyExisted = candidates.Count > 0;
        string best = null;
        long bestTicks = long.MinValue, bestSerial = long.MinValue;
        foreach (var c in candidates)
        {
            if (!TryParse(c.json, out long ticks, out long serial)) continue;
            if (best == null || serial > bestSerial || (serial == bestSerial && ticks > bestTicks))
            {
                best = c.json;
                bestTicks = ticks;
                bestSerial = serial;
            }
        }
        if (best != null && candidates[0].json != best)
            Debug.LogWarning("[SaveSystem] Asıl kayıt bozuk ya da eski: başka kopyadan açıldı.");
        return best?.Trim();
    }

    // ==================== YEDEK KLASÖRÜ ====================

    /// <summary>İçeriği yedek klasörüne yazar: <c>save.&lt;tür&gt;.&lt;an&gt;[.kaynak].json</c>. Tür başına en yeni birkaç dosya tutulur.</summary>
    private static void Archive(string kind, string json, string source = null)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string name = "save." + kind + "." + stamp + (source != null ? "." + source : "") + ".json";
            File.WriteAllText(Path.Combine(BackupDirectory, name), json);
            // Sürüm yedekleri ("v6.premigration", "v16.newer") tek grup; karantina ve silinenler ayrı gruplar.
            Prune(kind.StartsWith("v") ? "save.v" : "save." + kind);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] Yedek dosyası yazılamadı: " + e.Message);
        }
    }

    private static void Prune(string prefix)
    {
        var files = new List<string>(Directory.GetFiles(BackupDirectory, prefix + "*.json"));
        // Karantina ve silinen kayıtta bir olay birden çok kopya üretir (asıl, dosya, yedek, geçici).
        int keep = KeepPerKind * 4;
        if (files.Count <= keep) return;
        files.Sort((a, b) => File.GetLastWriteTimeUtc(a).CompareTo(File.GetLastWriteTimeUtc(b)));
        for (int i = 0; i < files.Count - keep; i++)
            try { File.Delete(files[i]); } catch (Exception) { /* silinemeyen eski yedek zararsız */ }
    }

    /// <summary>Bu oturumda arşivlenmiş içerikler: aynı açılamayan kayıt her denemede yeniden kopyalanmasın.</summary>
    private static readonly HashSet<string> archivedThisSession = new HashSet<string>();

    /// <summary>Bütün kopyaları (birbirinden farklı olanları) yedek klasörüne kopyalar; asıllara dokunmaz.</summary>
    private static void ArchiveAllCopies(string kind)
    {
        foreach (var c in Candidates())
            if (archivedThisSession.Add(kind + "|" + c.json)) Archive(kind, c.json, c.source);
    }

    // ==================== YÜKLE ====================

    /// <summary>
    /// Oyuncu verisini yükle. Kayıt yoksa ya da açılamadıysa null; hangisi olduğu <see cref="LastLoadStatus"/>.
    /// Açılamadıysa kopyalar yerinde ve karantinada durur, <see cref="Save"/> kilitlenir.
    /// </summary>
    public static PlayerSaveData Load()
    {
        LastLoadError = "";
        string json = PickBestJson(out bool anyExisted);
        if (json == null)
        {
            if (!anyExisted)
            {
                LastLoadStatus = SaveLoadStatus.NoSave;
                Debug.Log("[SaveSystem] Kayıt bulunamadı.");
                return null;
            }
            return Fail("hiçbir kayıt kopyası çözülemedi", null);
        }

        try
        {
            var data = JsonUtility.FromJson<PlayerSaveData>(json);
            int fromVersion = data.saveVersion;
            if (fromVersion > PlayerSaveData.CurrentVersion)
            {
                // Daha yeni bir sürümün kaydı (eski sürüme dönüş, yedekten geri yükleme): bilinmeyen alanlar ilk kayıtta düşer;
                // orijinali saklanır ve sürüm numarası düşürülmez.
                Debug.LogWarning($"[SaveSystem] Kayıt daha yeni bir sürümden (v{fromVersion} > v{PlayerSaveData.CurrentVersion}); orijinali saklandı.");
                Archive("v" + fromVersion + ".newer", json);
            }
            else if (fromVersion < PlayerSaveData.CurrentVersion)
                Archive("v" + fromVersion + ".premigration", json);

            bool changed = Migrate(data, json);
            MigrationFaultForTests?.Invoke();
            Normalize(data);
            changed |= SaveInvariants.Repair(data);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (string problem in SaveInvariants.Check(data))
                Debug.LogWarning("[SaveSystem] Kayıt tutarsızlığı: " + problem);
#endif
            LastLoadStatus = SaveLoadStatus.Loaded;
            lastGoodJson = json;                       // göç kaydı orijinali PlayerPrefs yedeğine taşır
            lastSerial = Math.Max(lastSerial, data.saveSerial);
            if (changed) Save(data);
            Debug.Log("[SaveSystem] Kayıt yüklendi.");
            return data;
        }
        catch (Exception e)
        {
            return Fail("kayıt işlenirken hata: " + e.GetType().Name + ": " + e.Message, json);
        }
    }

    private static PlayerSaveData Fail(string reason, string parsedJson)
    {
        LastLoadStatus = SaveLoadStatus.Failed;
        LastLoadError = reason;
        ArchiveAllCopies("quarantine");
        try
        {
            var all = Candidates();
            string keep = parsedJson ?? (all.Count > 0 ? all[0].json : null);
            if (keep != null) { PlayerPrefs.SetString(CORRUPT_KEY, keep); PlayerPrefs.Save(); }
        }
        catch (Exception) { /* PlayerPrefs dolu olabilir: dosya arşivi yeterli */ }
        Debug.LogError("[SaveSystem] Kayıt açılamadı (" + reason + "). Kopyalar korunuyor, üstüne yazılmayacak: " + BackupDirectory);
        return null;
    }

    /// <summary>
    /// Göç zinciri: BELLEKTE çalışır, kaydetmez. Dönüş: veri değişti mi (çağıran tek kayıt yapar).
    /// <paramref name="json"/> yalnız v1 → v2 için (eski yerleşim alanlarını okur).
    /// </summary>
    private static bool Migrate(PlayerSaveData data, string json)
    {
        bool changed = false;
        int timingBefore = data.timingVersion;

        if (data.campaignProgress == null) data.campaignProgress = new List<CampaignLevelProgress>();
        if (data.placedDecor == null) data.placedDecor = new List<PlacedDecorSave>();
        if (data.builderTask == null) data.builderTask = new BuilderTaskSave();
        // The old enum occupied the same numeric value; migrate string slot ids too.
        if (data.placedBuildings != null)
            foreach (var building in data.placedBuildings)
                if (building != null && building.buildingType == "WallBuilder")
                {
                    building.buildingType = "Monastery";
                    building.slotId = "monastery_1";
                    changed = true;
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
            changed = true;
            Debug.Log($"[SaveSystem] Üs kaydı slotlara taşındı: {migrated.Count} bina.");
        }
        if (data.saveVersion < 3)
        {
            data.firstBaseGuideDone = true;
            data.saveVersion = 3;
            changed = true;
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
            changed = true;
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
            changed = true;
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
            changed = true;
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
            changed = true;
        }
        // v8: Kart Atölyesi. Eski kayıtta workshop boştur; bina türü kaydı Ana Üs seviyesine göre eklenir. Mevcut veriye dokunulmaz.
        if (data.workshop == null) data.workshop = new WorkshopSaveData();
        data.workshop.Repair();
        PlayerSaveData.EnsureWorkshopEntry(data);
        if (data.saveVersion < 8)
        {
            data.saveVersion = 8;
            changed = true;
        }
        // v9: Kışla + Köylü Evi. Eski kayıtta tür girdisi yoktur; Ana Üs seviyesine göre eklenir, mevcut veriye dokunulmaz.
        PlayerSaveData.EnsureBaseBondEntries(data);
        if (data.saveVersion < 9)
        {
            data.saveVersion = 9;
            changed = true;
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
            changed = true;
        }
        // v11 (AŞAMA 3): ada adı. Eski kayıtta ad boş ("Martı Adası" gösterilir), teklif henüz yapılmadı.
        if (data.saveVersion < 11)
        {
            data.island.islandName = "";
            data.island.nameChanges = 0;
            data.island.nameOffered = false;
            data.island.nameSeal = IslandNameRules.Seal("", 0);
            data.saveVersion = 11;
            changed = true;
        }
        // v12 (6. oturum 2.4): IAP defteri. Eski kayıtta satın alma yok → boş defter.
        if (data.iapLedger == null) data.iapLedger = new List<string>();
        if (data.saveVersion < 12)
        {
            data.saveVersion = 12;
            changed = true;
        }
        // v13 (6. oturum 3.1–3.3): adaya daha önce girmiş oyuncu tanıtımı/öğreticiyi görmez; ada seviyeleri bugünkü (Ana Üs'ü izleyen)
        // etkin değerde sabitlenir ve bundan sonra bağımsız ilerler (kimse seviye kaybetmez). Adaya hiç girmemiş eski kayıt: ilk giriş akışı.
        if (data.saveVersion < 13)
        {
            IslandEconomy.MigrateV13(data.island, data.buildings?.Find(e => e.buildingType == BuildingType.MainBase)?.level ?? 1);
            data.saveVersion = 13;
            changed = true;
        }
        // v14 (D121, grup 9): Depo. Dönüşüm yok: eski kayıt = boş depo (JsonUtility eksik listeyi null bırakabilir). Sürüm artışı geri dönüş
        // güvencesi içindir: eski sürüm bilinmeyen depo alanını ilk kayıtta düşürür ama v14 kaydı önce "v14.newer" olarak arşivlenir.
        if (data.depot == null) data.depot = new List<DepotItem>();
        if (data.saveVersion < 14)
        {
            data.saveVersion = 14;
            changed = true;
        }
        MigrateTimings(data);
        changed |= data.timingVersion != timingBefore;
        return changed;
    }

    /// <summary>Her yüklemede uygulanan sınırlama ve boş alan onarımı (sürümden bağımsız, idempotent).</summary>
    private static void Normalize(PlayerSaveData data)
    {
        data.apprenticeLevel = Mathf.Clamp(data.apprenticeLevel, 1, Progression.MaxLevel);
        // Tek kahraman: Okçu/Mızrakçı seçmiş eski oyuncular da Warrior olur.
        if (data.hero == null) data.hero = new HeroData();
        data.hero.weaponType = WeaponType.Axe;
        data.hero.level = Mathf.Clamp(data.hero.level, 1, Progression.MaxLevel);
        data.hero.ApplyLevelStats();
        data.builderCount = Mathf.Clamp(data.builderCount, 1, Progression.MaxBuilders);
        data.gems = Mathf.Max(0, data.gems);
        data.builderTasks.RemoveAll(task => task == null || !task.IsActive);
        if (data.depot == null) data.depot = new List<DepotItem>();   // D121: eksik alan (eski/yarım kayıt) → boş depo
    }

    /// <summary>
    /// Süre tablosu değiştiyse (TimingConfig.version) devam eden yükseltmelerin kalan süresi yeni/eski oranıyla
    /// ölçeklenir: yarısı geçmiş 6 saatlik iş, yeni tabloda 1 saatse 30 dk kalır. Yeni kayıt doğrudan güncel sürümle başlar.
    /// </summary>
    public static void MigrateTimings(PlayerSaveData data)
    {
        var config = TimingConfig.Instance;
        if (data == null || data.timingVersion >= config.version) return;
        long now = DateTime.UtcNow.Ticks;
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
                task.startTicks = task.endTicks - TimeSpan.FromSeconds(newSeconds).Ticks;
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

    // ==================== VAR MI / SİL ====================

    /// <summary>Kayıt var mı (açılamayan kayıt da "var" sayılır: menü DEVAM'ı gösterir, oyuncu durumu görür).</summary>
    public static bool HasSave()
    {
        return PlayerPrefs.HasKey(SAVE_KEY) || PlayerPrefs.HasKey(BACKUP_KEY) || File.Exists(FilePath);
    }

    /// <summary>
    /// Kaydı sil: oyuncunun onayladığı yeni oyun (ve testler). Silinen kopyalar önce yedek klasörüne arşivlenir
    /// (yanlışlıkla silmede destekle geri getirilebilir); açılamayan kayıt kilidi kalkar.
    /// </summary>
    public static void DeleteSave()
    {
        ArchiveAllCopies("deleted");
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.DeleteKey(BACKUP_KEY);
        PlayerPrefs.Save();
        lastGoodJson = null;
        lastSerial = 0;
        LastLoadStatus = SaveLoadStatus.NoSave;
        LastLoadError = "";
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
        catch (Exception e) { Debug.LogWarning("[SaveSystem] Kayıt dosyası silinemedi: " + e.Message); }
        Debug.Log("[SaveSystem] Kayıt silindi.");
    }
}
