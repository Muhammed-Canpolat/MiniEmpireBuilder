using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Savaş sahnesini yönetir — dalga sistemi, düşman spawn, kazanma/kaybetme
/// BattleScene'de boş bir GameObject'e eklenir
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private Transform mainBaseTransform;
    [SerializeField] private Transform heroTransform;

    [Header("Spawn Ayarları")]
    [SerializeField] private float spawnRadius = 10f;       // Düşmanlar bu yarıçaptan gelir
    [SerializeField] private GameObject enemyPrefab;         // Düşman prefab'ı
    [SerializeField] private int archerBattleBuildCost = 50;
    [SerializeField] private int cannonBattleBuildCost = 75;
    [SerializeField] private int monasteryBattleBuildCost = 60;
    [SerializeField] private int baseWinGoldReward = 100;

    [Header("Durum")]
    private int currentWaveIndex = 0;
    private int enemiesAlive = 0;
    private int totalGoldEarned = 0;
    // Kahraman üssün sağ altında başlar: üssün ve yuvaların üstüne binmez (yuva tetik yarıçapı 1.35).
    private static readonly Vector3 HeroSpawnPosition = new Vector3(2.6f, -1.7f, 0f);
    public int GoldEarned => totalGoldEarned;
    private bool battleActive = false;
    private bool battleWon = false;
    private int lastStars;

    // Level verisi
    private LevelData currentLevel;

    // Event'ler — UI için
    public event System.Action<int, int> OnWaveChanged;          // (currentWave, totalWaves)
    public event System.Action<int> OnEnemiesCountChanged;       // kalan düşman
    public event System.Action<int> OnGoldEarnedChanged;         // kazanılan altın
    public event System.Action<bool> OnBattleEnded;              // true=kazandı, false=kaybetti
    public event System.Action<int> OnWaveIncoming;              // gelecek dalga duyurusu

    // Singleton (sadece savaş sahnesi boyunca)
    public static BattleManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // GameManager yoksa oluştur (test için)
        if (GameManager.Instance == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }

        // Level verisini oluştur
        int battleLevel = 1;
        if (GameManager.Instance.PlayerData != null)
        {
            battleLevel = GameManager.Instance.PlayerData.currentBattleLevel;
        }

        if (BattleSession.IsCampaign)
        {
            battleLevel = (BattleSession.RegionId - 1) * 5 + BattleSession.LevelNumber;
            currentLevel = CreateCampaignWaves(BattleSession.Region, BattleSession.Level);
        }
        else currentLevel = LevelData.GenerateLevel(battleLevel);

        Debug.Log($"[BattleManager] Level {battleLevel} başlıyor! {currentLevel.waves.Count} dalga");

        // Sahneyi oluştur (eğer otomatik kurulum istiyorsak)
        SetupBattleScene();
        EnsureBattleVfxManager();
        OnGoldEarnedChanged?.Invoke(totalGoldEarned);

        // İlk dalgayı başlat
        StartCoroutine(StartBattleSequence());
    }

    // ==================== SAHNE KURULUMU ====================

    /// <summary>
    /// Savaş sahnesini kodla oluştur — test için
    /// İleride prefab'larla değiştirilecek
    /// </summary>
    private void SetupBattleScene()
    {
        var sm = SpriteManager.Instance;

        // === ZEMIN ===
        Sprite ground = BattleSession.Region?.groundTile ?? sm?.GetSprite("ground_battle_tile_01")
                        ?? Resources.Load<Sprite>("Sprites/ground_battle_tile_01");
        if (ground != null)
        {
            var groundObject = new GameObject("BattleGround");
            groundObject.transform.position = new Vector3(0, 0, 1);
            var renderer = groundObject.AddComponent<SpriteRenderer>();
            renderer.sprite = ground;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(60f, 60f);
            renderer.sortingOrder = -100;
            // Zemin önceki tonlardan belirgin açık: koyu düşman sprite'ları üzerinde seçilebilsin.
            renderer.color = BattleSession.Level?.kind == CampaignLevelKind.Boss
                ? new Color(0.86f, 0.93f, 0.86f)
                : BattleSession.IsCampaign
                    ? Color.Lerp(new Color(1f, 0.96f, 0.9f), BattleSession.Region.colorTone, 0.12f)
                    : new Color(1f, 0.96f, 0.9f);
        }
        else
        {
            CreateSimpleSprite("BattleGround", new Color(0.12f, 0.11f, 0.09f),
                new Vector3(0, 0, 1), new Vector3(60f, 60f, 1f));
        }

        // === ÇEVRE DEKORASYONU (Rastgele) ===
        // Geniş bir alana (örn: -15 to 15) rastgele kaya, çalı, kuru ot yerleştirelim.
        int propCount = 60; // Etrafa yayılacak obje sayısı
        for (int i = 0; i < propCount; i++)
        {
            float rx = Random.Range(-15f, 15f);
            float ry = Random.Range(-15f, 15f);

            // Ana üsse veya başlangıç noktasına çok yakınsa koyma
            if (Vector2.Distance(new Vector2(rx, ry), Vector2.zero) < 3f) continue;

            int propType = Random.Range(0, 3); // 0: Kaya, 1: Çalı, 2: Kuru Ot
            Sprite propSprite = null;
            Color propColor = Color.white;
            float propScale = Random.Range(0.2f, 0.5f);

            if (sm != null)
            {
                if (propType == 0) propSprite = sm.Rock;
                else if (propType == 1) propSprite = sm.Tree; // Çalı/ağaç
            }

            GameObject propObj;
            if (propSprite != null)
            {
                propObj = CreateSpriteObject($"Prop_{i}", propSprite, new Vector3(rx, ry, 0.8f), new Vector3(propScale, propScale, 1f));
            }
            else
            {
                if (propType == 0) propColor = new Color(0.4f, 0.4f, 0.4f); // Kaya (gri)
                else if (propType == 1) propColor = new Color(0.15f, 0.35f, 0.15f); // Çalı (koyu yeşil)
                else propColor = new Color(0.6f, 0.55f, 0.2f); // Kuru ot (sarı/kahve)

                propObj = CreateSimpleSprite($"Prop_{i}", propColor, new Vector3(rx, ry, 0.8f), new Vector3(propScale, propScale, 1f));
                if (propType == 2) propObj.transform.localScale = new Vector3(0.05f, 0.3f, 1f); // Kuru ot ince uzun
            }

            propObj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            SpriteRenderer sr = propObj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = 1;
        }

        // Ana Üs yoksa oluştur
        if (mainBaseTransform == null)
        {
            GameObject baseObj;
            if (sm != null)
            {
                baseObj = CreateSpriteObject("AnaUs", sm.MainBase,
                    Vector3.zero, new Vector3(0.60f, 0.60f, 1f));
            }
            else
            {
                baseObj = CreateSimpleSprite("AnaUs", new Color(0.85f, 0.75f, 0.2f),
                    Vector3.zero, new Vector3(1.5f, 1.5f, 1f));
            }

            baseObj.AddComponent<MainBaseController>();
            mainBaseTransform = baseObj.transform;
        }

        // Kahraman yoksa oluştur
        if (heroTransform == null)
        {
            GameObject heroObj;

            if (sm != null && GameManager.Instance != null && GameManager.Instance.PlayerData != null)
            {
                Sprite heroSprite = sm.GetHeroSprite(GameManager.Instance.PlayerData.hero.weaponType);
                Vector3 scale = new Vector3(0.85f, 0.85f, 1f);
                if (GameManager.Instance.PlayerData.hero.weaponType == WeaponType.Spear)
                    scale = new Vector3(1.0f, 1.0f, 1f);

                heroObj = CreateSpriteObject("AnaSavasci", heroSprite,
                    HeroSpawnPosition, scale);
            }
            else
            {
                Color heroColor = Color.cyan;
                if (GameManager.Instance != null && GameManager.Instance.PlayerData != null)
                {
                    switch (GameManager.Instance.PlayerData.hero.weaponType)
                    {
                        case WeaponType.Axe:
                            heroColor = new Color(0.9f, 0.3f, 0.2f);
                            break;
                        case WeaponType.Spear:
                            heroColor = new Color(0.3f, 0.7f, 1f);
                            break;
                        case WeaponType.Bow:
                            heroColor = new Color(0.2f, 0.85f, 0.4f);
                            break;
                    }
                }
                heroObj = CreateSimpleSprite("AnaSavasci", heroColor,
                    HeroSpawnPosition, new Vector3(0.8f, 0.8f, 1f));
            }

            heroObj.AddComponent<HeroController>();

            BoxCollider2D heroCol = heroObj.AddComponent<BoxCollider2D>();
            heroCol.isTrigger = true;

            Rigidbody2D heroRb = heroObj.AddComponent<Rigidbody2D>();
            heroRb.gravityScale = 0;
            heroRb.freezeRotation = true;

            heroTransform = heroObj.transform;
        }

        // Savunma birimleri savaş sırasında slotlardan otomatik inşa edilir
        CreateBattleBuildSlots();

        // Düşman prefab yoksa basit bir tane oluştur
        if (enemyPrefab == null)
        {
            enemyPrefab = CreateEnemyPrefab();
        }
    }

    private GameObject CreateSimpleSprite(string name, Color color, Vector3 position, Vector3 scale)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = position;
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = color;

        return obj;
    }

    /// <summary>
    /// Gerçek sprite ile obje oluştur
    /// </summary>
    private GameObject CreateSpriteObject(string name, Sprite sprite, Vector3 position, Vector3 scale)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = position;
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        return obj;
    }

    /// <summary>
    /// Parent'a bağlı child sprite oluştur (silah, dekorasyon vs.)
    /// </summary>
    private GameObject CreateChildSprite(Transform parent, string name, Color color, Vector3 localPos, Vector3 scale)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = scale;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = color;
        sr.sortingOrder = 2;

        return obj;
    }

    private void CreateWallPiece(Vector3 position)
    {
        GameObject wall;
        var sm = SpriteManager.Instance;
        if (sm != null)
        {
            wall = CreateSpriteObject("Duvar", sm.WallSegment, position, new Vector3(0.5f, 0.5f, 1f));
        }
        else
        {
            wall = CreateSimpleSprite("Duvar", new Color(0.55f, 0.38f, 0.18f), position, new Vector3(1.5f, 0.4f, 1f));
        }
        wall.AddComponent<WallController>();
        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
    }

    private void CreateBattleBuildSlots()
    {
        if (GameManager.Instance == null)
            return;
        // Ana Üs görseli yaklaşık x∈[-1.9, 1.9], y∈[-1.0, 1.6] kaplar (BattleActorPresentation).
        // Yuvalar (yarıçap 0.95 + alt etiket) bu alanın dışında, üssün iki üst köşesi ve altında.
        CreateBuildSlot(new Vector3(-2.9f, 2.4f, 0f), BuildingType.ArcherTower, archerBattleBuildCost);
        CreateBuildSlot(new Vector3(2.9f, 2.4f, 0f), BuildingType.CannonTower, cannonBattleBuildCost);
        CreateBuildSlot(new Vector3(0f, -2.9f, 0f), BuildingType.Monastery, monasteryBattleBuildCost);
    }

    private void CreateBuildSlot(Vector3 position, BuildingType type, int cost)
    {
        var sm = SpriteManager.Instance;
        Sprite slotSprite = sm != null ? sm.BuildSlot : null;

        GameObject slotObj;
        if (slotSprite != null)
        {
            slotObj = CreateSpriteObject($"Slot_{type}", slotSprite, position, new Vector3(0.25f, 0.25f, 1f));
            SpriteRenderer sr = slotObj.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, 0.85f);
        }
        else
        {
            slotObj = CreateSimpleSprite($"Slot_{type}", new Color(1f, 1f, 1f, 0.35f), position, new Vector3(0.8f, 0.8f, 1f));
        }

        CircleCollider2D slotCol = slotObj.AddComponent<CircleCollider2D>();
        slotCol.isTrigger = true;
        slotCol.radius = 1.1f;

        BattleBuildSlot slot = slotObj.AddComponent<BattleBuildSlot>();
        slot.Setup(this, type, cost);
    }

    private void CreateTower(Vector3 position, BuildingType type)
    {
        var sm = SpriteManager.Instance;
        GameObject towerObj;

        if (sm != null)
        {
            Sprite towerSprite = type == BuildingType.ArcherTower ? sm.ArcherTower : sm.CannonTower;
            towerObj = CreateSpriteObject(type.ToString(), towerSprite, position, new Vector3(0.4f, 0.4f, 1f));
        }
        else
        {
            Color c = type == BuildingType.ArcherTower ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.4f, 0.1f);
            towerObj = CreateSimpleSprite(type.ToString(), c, position, new Vector3(0.7f, 1f, 1f));
        }

        TowerController tower = towerObj.AddComponent<TowerController>();
        tower.Initialize(type);
        if (GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var building))
        {
            var visual = building.VisualForLevel(building.level);
            var renderer = towerObj.GetComponent<SpriteRenderer>();
            if (renderer != null && visual != null)
            {
                if (visual.sprite != null) renderer.sprite = visual.sprite;
                renderer.color = visual.color;
            }
        }
    }

    private void CreateMonastery(Vector3 position)
    {
        var visual = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(BuildingType.Monastery, out var data)
            ? data.VisualForLevel(data.level) : null;
        var monastery = CreateSpriteObject("Monastery", visual?.sprite,
            position, new Vector3(0.4f, 0.4f, 1f));
        monastery.AddComponent<MonasteryHealer>();
    }

    private GameObject CreateEnemyPrefab()
    {
        // Geçici prefab — runtime'da oluştur
        GameObject prefab = new GameObject("EnemyPrefab");
        prefab.transform.localScale = new Vector3(0.65f, 0.65f, 1f);

        SpriteRenderer sr = prefab.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = new Color(0.8f, 0.15f, 0.15f); // Kırmızı düşman
        sr.sortingOrder = 1;

        BoxCollider2D col = prefab.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        // Enemy layer
        prefab.layer = LayerMask.NameToLayer("Default"); // İleride "Enemy" layer ekleyeceğiz

        prefab.AddComponent<EnemyController>();
        prefab.AddComponent<EnemyVisuals>();

        prefab.SetActive(false); // Prefab olarak kullanılacak, sahnede görünmeyecek

        return prefab;
    }

    private void EnsureBattleVfxManager()
    {
        if (FindFirstObjectByType<BattleVfxManager>() == null)
        {
            GameObject vfxObj = new GameObject("BattleVfxManager");
            vfxObj.AddComponent<BattleVfxManager>();
        }
    }

    /// <summary>
    /// Basit kare sprite oluştur (geçici — ileride gerçek sprite kullanacağız)
    /// </summary>
    private Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(32, 32);
        Color[] colors = new Color[32 * 32];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    // ==================== SAVAŞ AKIŞI ====================

    private IEnumerator StartBattleSequence()
    {
        yield return new WaitForSeconds(1.5f); // Başlangıç bekleme

        battleActive = true;

        for (int w = 0; w < currentLevel.waves.Count; w++)
        {
            if (!battleActive) yield break;

            currentWaveIndex = w;
            WaveData wave = currentLevel.waves[w];
            int incomingCount = 0;
            foreach (var enemy in wave.enemies) incomingCount += enemy.count;
            OnWaveIncoming?.Invoke(incomingCount);

            // Ekranda DALGA X banner'ını göster
            if (BattleVfxManager.Instance != null)
                BattleVfxManager.Instance.ShowWaveBanner(w + 1);

            NextWaveAt = Time.time + 3f;
            yield return new WaitForSeconds(3f);
            NextWaveAt = 0f;

            Debug.Log($"[BattleManager] Dalga {w + 1}/{currentLevel.waves.Count} başlıyor!");
            OnWaveChanged?.Invoke(w + 1, currentLevel.waves.Count);

            // Dalgadaki tüm düşmanları spawn et
            yield return StartCoroutine(SpawnWave(wave));

            // Tüm düşmanlar ölene kadar bekle
            while (enemiesAlive > 0 && battleActive)
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (!battleActive) yield break;

            // Dalgalar arası bekleme
            if (w < currentLevel.waves.Count - 1)
            {
                Debug.Log("[BattleManager] Sonraki dalga 5 saniye içinde...");
                NextWaveAt = Time.time + 5f;
                yield return new WaitForSeconds(5f);
                NextWaveAt = 0f;
            }
        }

        // Tüm dalgalar bitti — KAZANDIK!
        if (battleActive)
        {
            BattleWon();
        }
    }

    private IEnumerator SpawnWave(WaveData wave)
    {
        foreach (var spawnInfo in wave.enemies)
        {
            for (int i = 0; i < spawnInfo.count; i++)
            {
                if (!battleActive) yield break;

                SpawnEnemy(spawnInfo.enemyType);
                yield return new WaitForSeconds(wave.timeBetweenSpawns);
            }
        }
    }

    private void SpawnEnemy(EnemyType type)
    {
        Vector3 spawnPos = Vector3.zero;
        BattleSceneSetup setup = FindFirstObjectByType<BattleSceneSetup>();

        if (setup != null)
        {
            float halfW = setup.worldWidth / 2f;
            float halfH = setup.worldHeight / 2f;

            // Rastgele bir kenar seç (0: üst, 1: sağ, 2: alt, 3: sol)
            int edge = Random.Range(0, 4);
            switch (edge)
            {
                case 0: // Üst
                    spawnPos = new Vector3(Random.Range(-halfW, halfW), halfH + 2f, 0);
                    break;
                case 1: // Sağ
                    spawnPos = new Vector3(halfW + 2f, Random.Range(-halfH, halfH), 0);
                    break;
                case 2: // Alt
                    spawnPos = new Vector3(Random.Range(-halfW, halfW), -halfH - 2f, 0);
                    break;
                case 3: // Sol
                    spawnPos = new Vector3(-halfW - 2f, Random.Range(-halfH, halfH), 0);
                    break;
            }
        }
        else
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            spawnPos = new Vector3(Mathf.Cos(angle) * 10f, Mathf.Sin(angle) * 10f, 0);
        }

        SpawnEnemyAt(type, spawnPos);
    }

    /// <summary>Belirli bir noktada düşman doğurur (ör. Cadı'nın çağırdığı kurtlar).</summary>
    public EnemyController SpawnEnemyAt(EnemyType type, Vector3 spawnPos)
    {
        if (enemyPrefab == null) return null;
        GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        enemyObj.SetActive(true);
        enemyObj.name = $"Enemy_{type}";

        EnemyController enemy = enemyObj.GetComponent<EnemyController>();
        if (enemy != null)
        {
            int battleLevel = BattleSession.IsCampaign
                ? (BattleSession.RegionId - 1) * 5 + BattleSession.LevelNumber
                : GameManager.Instance?.PlayerData?.currentBattleLevel ?? 1;
            enemy.Initialize(type, battleLevel, mainBaseTransform);
        }

        enemiesAlive++;
        OnEnemiesCountChanged?.Invoke(enemiesAlive);
        return enemy;
    }

    // ==================== OLAY CALLBACK'LERİ ====================

    public void OnEnemyKilled(int goldReward)
    {
        if (!battleActive) return;

        enemiesAlive--;
        enemiesAlive = Mathf.Max(0, enemiesAlive);
        totalGoldEarned += goldReward;

        OnEnemiesCountChanged?.Invoke(enemiesAlive);
        OnGoldEarnedChanged?.Invoke(totalGoldEarned);
    }

    public bool TryBuildFromSlot(BattleBuildSlot slot, BuildingType type, Vector3 position, int cost)
    {
        if (!CanBuildFromSlot(type, cost))
            return false;

        totalGoldEarned -= cost;
        OnGoldEarnedChanged?.Invoke(totalGoldEarned);

        switch (type)
        {
            case BuildingType.ArcherTower:
                CreateTower(position, BuildingType.ArcherTower);
                break;
            case BuildingType.CannonTower:
                CreateTower(position, BuildingType.CannonTower);
                break;
            case BuildingType.Monastery:
                CreateMonastery(position);
                break;
            default:
                return false;
        }

        if (BattleVfxManager.Instance != null)
            BattleVfxManager.Instance.SpawnBuild(position);

        return true;
    }

    public bool CanBuildFromSlot(BuildingType type, int cost)
    {
        if (!battleActive || cost <= 0 || totalGoldEarned < cost || GameManager.Instance == null)
            return false;
        if (!GameManager.Instance.Buildings.TryGetValue(type, out var building) || !building.isUnlocked)
            return false;
        int max = GetBattleBuildAllowance(type);
        int built = 0;
        foreach (var slot in FindObjectsByType<BattleBuildSlot>(FindObjectsSortMode.None))
            if (slot.IsBuilt && slot.BuiltType == type) built++;
        return built < max;
    }

    public int GetBattleBuildAllowance(BuildingType type)
    {
        var placed = GameManager.Instance?.PlayerData?.placedBuildings;
        if (placed == null) return 0;
        int count = 0;
        foreach (var building in placed)
            if (building != null && building.buildingType == type.ToString()) count++;
        return count;
    }

    public int GetBattleBuildRemaining(BuildingType type)
    {
        int built = 0;
        foreach (var slot in FindObjectsByType<BattleBuildSlot>(FindObjectsSortMode.None))
            if (slot.IsBuilt && slot.BuiltType == type) built++;
        return Mathf.Max(0, GetBattleBuildAllowance(type) - built);
    }

    public int GetBuildCost(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.ArcherTower: return archerBattleBuildCost;
            case BuildingType.CannonTower: return cannonBattleBuildCost;
            case BuildingType.Monastery: return monasteryBattleBuildCost;
            default: return 0;
        }
    }

    public void OnHeroDied()
    {
        if (!battleActive) return;
        Debug.Log("[BattleManager] Kahraman öldü! Savaş kaybedildi!");
        BattleLost();
    }

    public void OnMainBaseDestroyed()
    {
        if (!battleActive) return;
        BattleLost();
    }

    // ==================== SAVAŞ SONU ====================

    private void BattleWon()
    {
        battleActive = false;
        battleWon = true;
        if (BattleSession.IsCampaign)
        {
            float baseHealth = mainBaseTransform != null
                ? mainBaseTransform.GetComponent<MainBaseController>()?.HealthPercent ?? 0f : 0f;
            float heroHealth = heroTransform != null
                ? heroTransform.GetComponent<HeroController>()?.HealthPercent ?? 0f : 0f;
            lastStars = 1 + (baseHealth > 0.5f ? 1 : 0) + (heroHealth > 0.5f ? 1 : 0);
            CampaignProgress.RecordVictory(BattleSession.RegionId, BattleSession.LevelNumber, lastStars);
        }
        int totalPayout = totalGoldEarned + GetVictoryBonus();

        Debug.Log($"[BattleManager] SAVAŞ KAZANILDI! Savas ici kalan altin: {totalGoldEarned}, Odul: {GetVictoryBonus()}, Toplam: {totalPayout}");

        OnBattleEnded?.Invoke(true);

        // GameManager'a bildir
        if (GameManager.Instance != null)
        {
            GameManager.Instance.BattleWon(totalPayout);
        }
    }

    private void BattleLost()
    {
        battleActive = false;
        battleWon = false;

        Debug.Log("[BattleManager] SAVAŞ KAYBEDİLDİ!");

        OnBattleEnded?.Invoke(false);

        // Tüm düşmanları durdur
        StopAllCoroutines();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.BattleLost();
        }
    }

    public bool Forfeit()
    {
        if (!battleActive) return false;
        BattleLost();
        foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            if (enemy != null) Destroy(enemy.gameObject);
        foreach (var projectile in FindObjectsByType<ProjectileController>(FindObjectsSortMode.None))
            if (projectile != null) Destroy(projectile.gameObject);
        return true;
    }

    public float NextWaveAt { get; private set; }

    // ==================== PUBLIC ERIŞIM ====================

    public int CurrentWave => currentWaveIndex + 1;
    public int TotalWaves => currentLevel?.waves.Count ?? 0;
    public int EnemiesAlive => enemiesAlive;
    public int TotalGoldEarned => totalGoldEarned;
    public int VictoryBonus => GetVictoryBonus();
    public bool IsBattleActive => battleActive;
    public bool IsBattleWon => battleWon;
    public int LastStars => lastStars;

    private int GetVictoryBonus()
    {
        if (BattleSession.IsCampaign && BattleSession.Level != null)
            return BattleSession.Level.reward;
        int battleLevel = GameManager.Instance?.PlayerData?.currentBattleLevel ?? 1;
        return baseWinGoldReward + (battleLevel - 1) * 10;
    }

    private static LevelData CreateCampaignWaves(RegionData region, CampaignLevelData campaignLevel)
    {
        if (region == null || campaignLevel == null)
            return LevelData.GenerateLevel(1);
        var battle = new LevelData { levelNumber = campaignLevel.number };
        for (int i = 0; i < campaignLevel.waveCount; i++)
        {
            var wave = new WaveData { waveNumber = i + 1,
                timeBetweenSpawns = Mathf.Max(0.65f, 1.25f - (region.id - 1) * 0.1f) };
            int count = campaignLevel.enemyCounts != null && i < campaignLevel.enemyCounts.Length
                ? campaignLevel.enemyCounts[i] : 7;
            EnemyType type = region.enemyTypes != null && region.enemyTypes.Length > 0
                ? region.enemyTypes[Mathf.Min(i, region.enemyTypes.Length - 1)] : EnemyType.Wolf;
            if (campaignLevel.kind == CampaignLevelKind.Boss && i == campaignLevel.waveCount - 1)
            {
                wave.enemies.Add(new EnemySpawnInfo { enemyType = type, count = Mathf.Max(0, count - 1) });
                wave.enemies.Add(new EnemySpawnInfo { enemyType = region.bossType, count = 1 });
            }
            else wave.enemies.Add(new EnemySpawnInfo { enemyType = type, count = count });
            battle.waves.Add(wave);
        }
        return battle;
    }
}
