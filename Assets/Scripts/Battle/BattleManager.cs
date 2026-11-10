using DG.Tweening;
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
    public int GoldEarned => totalGoldEarned;
    private bool battleActive = false;
    private bool battleWon = false;
    private int lastStars;

    /// <summary>Daha önce kazanılmış bir sefer aşaması tekrar oynanınca toplam ödülün bu oranı verilir.</summary>
    public const float ReplayRewardRate = 0.3f;
    /// <summary>Bu savaş, önceden en az 1 yıldızla geçilmiş bir aşamanın tekrarı mı.</summary>
    public bool IsReplay { get; private set; }
    /// <summary>Zaferde cüzdana aktarılan son tutar (tekrar oynama kesintisi dahil).</summary>
    public int LastPayout { get; private set; }
    /// <summary>Bu zaferde kazanılan elmas (yeni yıldız, ilk boss, Sonsuz Sefer dönüm katı, İstila ilk zaferi).</summary>
    public int LastVictoryGems { get; private set; }

    // ==================== GÖREV TÜRLERİ ====================
    /// <summary>"Zamana karşı" aşamalarında süre (sn): dolmadan tüm düşmanlar temizlenmezse yenilgi.</summary>
    public const float TimeTrialSeconds = 180f;
    /// <summary>"Madeni koru" aşamalarında madenin canı, Ana Üs canının bu oranı.</summary>
    public const float DefendedMineHealthRatio = 0.6f;
    /// <summary>Süreli aşamada mı (zamana karşı).</summary>
    public bool HasTimeLimit => BattleSession.IsCampaign && BattleSession.Level?.kind == CampaignLevelKind.TimeTrial;
    /// <summary>Süreli aşamada kalan süre (sn); süre başlamadıysa tam süre.</summary>
    public float TimeRemaining => !HasTimeLimit ? 0f
        : timeLimitEndsAt <= 0f ? TimeTrialSeconds : Mathf.Max(0f, timeLimitEndsAt - Time.time);
    private float timeLimitEndsAt;
    /// <summary>"Madeni koru" aşamasında korunacak maden (yoksa null).</summary>
    public BattleStructure DefendedMine { get; private set; }
    /// <summary>Son yenilginin sebebi (sonuç ekranı için): "base", "time", "mine".</summary>
    public string DefeatReason { get; private set; } = "";
    public event System.Action OnHeroDowned;
    public event System.Action OnHeroBack;
    private int spawnedCount;

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
            battleLevel = BattleSession.BattleLevel;
            currentLevel = CreateCampaignWaves(BattleSession.Region, BattleSession.Level);
            // Tekrar: sefer aşaması yıldızlıysa; Sonsuz Sefer'de geçilmiş kat; İstila'da haftanın ilk zaferi alındıysa.
            IsReplay = BattleSession.IsEndless ? BattleSession.Floor <= EndlessMode.BestFloor
                : BattleSession.IsInvasion ? !DragonInvasion.FirstWinAvailable
                : CampaignProgress.Stars(BattleSession.RegionId, BattleSession.LevelNumber) > 0;
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
        if (BattleSession.IsCampaign && BattleSession.Level?.kind == CampaignLevelKind.DefendMine)
            CreateDefendedMine();

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
        // Yuvalar (yarıçap 0.95 + alt etiket) bu alanın dışında. Sayı bölgeye göre 3–5.
        foreach (var position in SlotLayout(SlotCount))
            CreateBuildSlot(position, BuildingType.ArcherTower, archerBattleBuildCost);
    }

    private static int SlotCount =>
        CampaignCatalog.BattleSlotCount(BattleSession.IsCampaign ? BattleSession.RegionId : 1);

    // Ana Üs merkezli dikdörtgenin köşeleri: her köşe üsse eşit uzaklıkta.
    private const float SlotX = 3.3f, SlotTop = 2.7f, SlotBottom = -2.5f;

    /// <summary>3: üst köşeler + alt orta · 4: dört çapraz köşe · 5: dört köşe + üst orta.</summary>
    public static Vector3[] SlotLayout(int count)
    {
        var upperLeft = new Vector3(-SlotX, SlotTop, 0f);
        var upperRight = new Vector3(SlotX, SlotTop, 0f);
        var lowerLeft = new Vector3(-SlotX, SlotBottom, 0f);
        var lowerRight = new Vector3(SlotX, SlotBottom, 0f);
        if (count <= 3) return new[] { upperLeft, upperRight, new Vector3(0f, -3.4f, 0f) };
        if (count == 4) return new[] { upperLeft, upperRight, lowerLeft, lowerRight };
        return new[] { upperLeft, upperRight, lowerLeft, lowerRight, new Vector3(0f, 4.3f, 0f) };
    }

    /// <summary>Kahraman üssün önünde, hiçbir yuvanın tetik alanında olmadan başlar.</summary>
    private static Vector3 HeroSpawnPosition => SlotCount <= 3
        ? new Vector3(2.2f, -2.2f, 0f) : new Vector3(0f, -2.4f, 0f);

    /// <summary>Bu türden şu ana kadar kaç kule kuruldu (savaştaki sıra = binanın seviye sırası).</summary>
    private int BuiltCount(BuildingType type)
    {
        int built = 0;
        foreach (var slot in FindObjectsByType<BattleBuildSlot>(FindObjectsSortMode.None))
            if (slot.IsBuilt && slot.BuiltType == type) built++;
        return built;
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

    // Yapıların görünen boyları (Ana Üs 2.6, kahraman 1.75 ile orantılı).
    private const float ArcherTowerHeight = 2.3f;
    private const float CannonTowerHeight = 2.0f;
    private const float MonasteryHeight = 2.2f;

    private void CreateTower(Vector3 position, BuildingType type) => BuildTower(position, type, null);

    private void BuildTower(Vector3 position, BuildingType type, BattleBuildSlot slot)
    {
        // İlk kurulan kule en yüksek seviyeli binanın, ikincisi sonrakinin seviyesini alır.
        int level = GameManager.Instance != null ? GameManager.Instance.GetBattleTowerLevel(type, BuiltCount(type)) : 1;
        float height = type == BuildingType.ArcherTower ? ArcherTowerHeight : CannonTowerHeight;
        var towerObj = CreateStructureVisual(type.ToString(), type, level, position, height);
        TowerController tower = towerObj.AddComponent<TowerController>();
        tower.Initialize(type, level);
        FinishStructure(towerObj, type, level, slot, position, height);
    }

    private void CreateMonastery(Vector3 position) => BuildMonastery(position, null);

    private void BuildMonastery(Vector3 position, BattleBuildSlot slot)
    {
        int level = GameManager.Instance != null
            ? GameManager.Instance.GetBattleTowerLevel(BuildingType.Monastery, BuiltCount(BuildingType.Monastery)) : 1;
        var monastery = CreateStructureVisual("Monastery", BuildingType.Monastery, level, position, MonasteryHeight);
        monastery.AddComponent<MonasteryHealer>().Level = level;
        FinishStructure(monastery, BuildingType.Monastery, level, slot, position, MonasteryHeight);
    }

    /// <summary>
    /// Seviye görseliyle, tabanı yuvanın biraz altına oturan, verilen boyda bina.
    /// Kök nesne görseldir (testler ve garnizon kökteki SpriteRenderer'ı kullanır).
    /// </summary>
    private GameObject CreateStructureVisual(string name, BuildingType type, int level, Vector3 slotPosition, float height)
    {
        Sprite sprite = null;
        if (GameManager.Instance?.Buildings != null && GameManager.Instance.Buildings.TryGetValue(type, out var data))
            sprite = data.VisualForLevel(level)?.sprite;
        if (sprite == null && SpriteManager.Instance != null)
            sprite = type == BuildingType.ArcherTower ? SpriteManager.Instance.ArcherTower
                : type == BuildingType.CannonTower ? SpriteManager.Instance.CannonTower : null;
        GameObject obj = sprite != null
            ? CreateSpriteObject(name, sprite, slotPosition, Vector3.one)
            : CreateSimpleSprite(name, new Color(0.5f, 0.6f, 0.8f), slotPosition, Vector3.one);
        var renderer = obj.GetComponent<SpriteRenderer>();
        Bounds local = renderer.sprite.bounds;
        float scale = height / Mathf.Max(0.01f, local.size.y);
        obj.transform.localScale = new Vector3(scale, scale, 1f);
        // Görselin alt kenarı yuva merkezinin 0.45 altında: yapı yuvanın üstünde "durur".
        float bottom = slotPosition.y - 0.45f;
        obj.transform.position = new Vector3(slotPosition.x, bottom - local.min.y * scale, 0f);
        renderer.sortingOrder = 6;
        renderer.color = Color.white;
        // Zemin gölgesi
        var shadow = new GameObject("StructureShadow");
        shadow.transform.SetParent(obj.transform, false);
        var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = BaseMapManager.MakeCircleSprite(Color.white);
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.32f);
        shadowRenderer.sortingOrder = 5;
        Vector2 circle = shadowRenderer.sprite.bounds.size;
        shadow.transform.localScale = new Vector3(1.6f / (circle.x * scale), 0.45f / (circle.y * scale), 1f);
        shadow.transform.position = new Vector3(slotPosition.x, bottom + 0.08f, 0f);
        return obj;
    }

    private void FinishStructure(GameObject obj, BuildingType type, int level, BattleBuildSlot slot,
        Vector3 slotPosition, float height)
    {
        var structure = obj.AddComponent<BattleStructure>();
        Vector2 basePoint = new Vector2(slotPosition.x, slotPosition.y - 0.45f);
        structure.Initialize(type, level, slot, basePoint, height);
        obj.AddComponent<BattleGarrison>().Initialize(type, level, height);
        obj.transform.localScale = new Vector3(obj.transform.localScale.x, obj.transform.localScale.y * 0.2f, 1f);
        float targetY = obj.transform.localScale.y / 0.2f;
        obj.transform.DOScaleY(targetY, 0.35f).SetEase(Ease.OutBack);
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
        if (HasTimeLimit) timeLimitEndsAt = Time.time + TimeTrialSeconds;

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
                ? BattleSession.BattleLevel
                : GameManager.Instance?.PlayerData?.currentBattleLevel ?? 1;
            enemy.Initialize(type, battleLevel, mainBaseTransform);
            // "Madeni koru": her iki düşmandan biri (boss hariç) madene yürür.
            if (DefendedMine != null && !EnemyTypeInfo.IsBoss(type))
                enemy.RaidTarget = spawnedCount % 2 == 0 ? DefendedMine : null;
            spawnedCount++;
        }

        enemiesAlive++;
        OnEnemiesCountChanged?.Invoke(enemiesAlive);
        return enemy;
    }

    // ==================== OLAY CALLBACK'LERİ ====================

    public void OnEnemyKilled(int goldReward)
    {
        DailyQuests.Report(QuestKind.KillEnemies);
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
                BuildTower(position, BuildingType.ArcherTower, slot);
                break;
            case BuildingType.CannonTower:
                BuildTower(position, BuildingType.CannonTower, slot);
                break;
            case BuildingType.Monastery:
                BuildMonastery(position, slot);
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

    /// <summary>
    /// Savaşçı düştü: savaş sürer, savaşçı HeroController.RespawnSeconds sonra Ana Üs önünde kalkar.
    /// Yenilgi yalnız Ana Üs (ya da "Madeni koru"da maden) yıkılınca veya süre bitince olur.
    /// </summary>
    public void OnHeroDied()
    {
        if (!battleActive) return;
        Debug.Log("[BattleManager] Savaşçı düştü, birazdan dirilecek.");
        OnHeroDowned?.Invoke();
    }

    public void OnHeroRespawned() => OnHeroBack?.Invoke();

    private void Update()
    {
        if (battleActive && HasTimeLimit && timeLimitEndsAt > 0f && Time.time >= timeLimitEndsAt)
        {
            DefeatReason = "time";
            BattleLost();
        }
    }

    /// <summary>"Madeni koru": düşmanların bir kısmının saldırdığı maden; yıkılırsa savaş kaybedilir.</summary>
    private void CreateDefendedMine()
    {
        int mineLevel = 1;
        float baseHealth = 200f;
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.Buildings.TryGetValue(BuildingType.GoldMine, out var mineData))
                mineLevel = Mathf.Max(1, mineData.level);
            baseHealth = GameManager.Instance.CreateLevelData(BuildingType.MainBase,
                Mathf.Max(1, GameManager.Instance.GetMainBaseLevel()))?.maxHealth ?? baseHealth;
        }
        // Yuvaların boş kaldığı yer: 5 yuvalı düzende üst orta dolu olduğundan alta konur.
        Vector3 position = SlotCount >= 5 ? new Vector3(0f, -4.4f, 0f) : new Vector3(0f, 4.3f, 0f);
        var mine = CreateStructureVisual("DefendedMine", BuildingType.GoldMine, mineLevel, position, 1.9f);
        var structure = mine.AddComponent<BattleStructure>();
        structure.Initialize(BuildingType.GoldMine, mineLevel, null, position, 1.9f);
        structure.SetMaxHealth(baseHealth * DefendedMineHealthRatio);
        structure.Destroyed += _ =>
        {
            if (!battleActive) return;
            DefeatReason = "mine";
            BattleLost();
        };
        DefendedMine = structure;
    }

    /// <summary>Bu savaşta elmasla "devam et" kullanıldı mı (savaş başına 1 kez).</summary>
    public bool ContinueUsed { get; private set; }
    private bool awaitingContinue;

    public void OnMainBaseDestroyed()
    {
        if (!battleActive || awaitingContinue) return;
        DefeatReason = "base";
        if (!ContinueUsed && BattleSession.IsCampaign && GameManager.Instance != null)
        {
            OfferContinue();
            return;
        }
        BattleLost();
    }

    /// <summary>
    /// Ana Üs yıkıldı: oyun durur, elmasla devam teklif edilir. Kabul edilirse üs %50 canla ve
    /// 3 sn korumayla kalkar (can harcanmaz); reddedilirse yenilgi.
    /// </summary>
    private void OfferContinue()
    {
        awaitingContinue = true;
        float previousScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioSettingsUI.BattleModalOpen = true;
        int cost = GemEconomy.ContinueCost;
        int percent = Mathf.RoundToInt(GemEconomy.ContinueBaseHealth * 100f);

        void Resume()
        {
            Time.timeScale = previousScale;
            AudioSettingsUI.BattleModalOpen = false;
            awaitingContinue = false;
        }
        void Revive()
        {
            ContinueUsed = true;
            var baseController = mainBaseTransform != null ? mainBaseTransform.GetComponent<MainBaseController>() : null;
            if (baseController != null) baseController.Revive(GemEconomy.ContinueBaseHealth, 3f);
            AudioManager.Instance?.PlaySfx("levelUp");
        }

        GemConfirm.Show(L.T("ANA ÜS YIKILDI", "MAIN BASE DESTROYED"),
            L.T($"Devam et: Ana Üs %{percent} canla ayağa kalkar, can kaybetmezsin. Her savaşta 1 kez.",
                $"Continue: your Main Base rises with {percent}% health and you keep your life. Once per battle."),
            cost, L.T("DEVAM ET", "CONTINUE"), () =>
            {
                Resume();
                if (!battleActive) return;
                if (GameManager.Instance == null || !GameManager.Instance.SpendGems(cost)) { BattleLost(); return; }
                Revive();
            },
            () =>
            {
                Resume();
                if (battleActive) BattleLost();
            }, true,
            L.T("▶ REKLAM İZLE, DEVAM ET", "▶ WATCH AD TO CONTINUE"),
            // Reklam oyun dururken oynar (gerçek zamanla sayar); bitince savaş sürer.
            () => RewardedAds.Show("battle_continue", ok =>
            {
                Resume();
                if (!battleActive) return;
                if (ok) Revive();
                else BattleLost();
            }));
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
            var heroController = heroTransform != null ? heroTransform.GetComponent<HeroController>() : null;
            float heroHealth = heroController != null ? heroController.HealthPercent : 0f;
            // Savaşçı yıldızı: hiç düşmeden ve canı %50'nin üstünde bitirmek.
            bool heroStar = heroHealth > 0.5f && (heroController == null || heroController.TimesDowned == 0);
            lastStars = 1 + (baseHealth > 0.5f ? 1 : 0) + (heroStar ? 1 : 0);
            DailyQuests.Report(QuestKind.WinCampaign);
            if (BattleSession.IsEndless) LastVictoryGems = EndlessMode.RecordClear(BattleSession.Floor);
            else if (BattleSession.IsInvasion) LastVictoryGems = DragonInvasion.RecordWin();
            else
            {
                // Yeni yıldız ve ilk boss zaferi elmas getirir (kayıttan önce: eski yıldızla karşılaştırılır).
                int starsBefore = CampaignProgress.Stars(BattleSession.RegionId, BattleSession.LevelNumber);
                DailyQuests.Report(QuestKind.EarnStars, Mathf.Max(0, lastStars - starsBefore));
                LastVictoryGems = GemRewards.ForVictory(BattleSession.RegionId, BattleSession.LevelNumber, lastStars);
                CampaignProgress.RecordVictory(BattleSession.RegionId, BattleSession.LevelNumber, lastStars);
            }
        }
        int totalPayout = totalGoldEarned + GetVictoryBonus();
        // Tamamlanmış aşamayı tekrar oynamak altın kasma yolu olmasın: toplamın %30'u.
        if (IsReplay) totalPayout = Mathf.RoundToInt(totalPayout * ReplayRewardRate);
        LastPayout = totalPayout;

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
