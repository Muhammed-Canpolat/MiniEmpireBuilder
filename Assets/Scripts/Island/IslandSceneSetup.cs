using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Martı Adası ince önyükleyici (island_base_v2 §9): ayrı .unity sahnesi yok; BattleScene ev sahibi olarak kurulur
/// (<see cref="IslandSession.Active"/> iken SceneAutoSetup bunu ekler). Arazi ve doğa editörde pişirilmiş prefablardan gelir
/// (<c>Resources/Island/IslandTerrain</c>, <c>IslandNature</c>: Tiny Swords karoları); kale, hazır köy, piyonlar ve koyunlar
/// katalog sprite'larından kurulur. Kodla çizilmiş arazi/ağaç/bulut yoktur. İki mod: Görünüm (kaydırma/zum) ve Savunma
/// (kamera savaş çerçevesine kayar, kilitli).
/// </summary>
public sealed class IslandSceneSetup : MonoBehaviour
{
    public static IslandSceneSetup Instance { get; private set; }

    public Camera WorldCamera { get; private set; }
    public IslandCameraController CameraController { get; private set; }
    public IslandArcher Archer { get; private set; }
    public IslandRaidManager Raid { get; private set; }
    public IslandHud Hud { get; private set; }
    public BattleStructure CastleStructure { get; private set; }
    /// <summary>Kurulum bitti mi (testler bekler).</summary>
    public bool Ready { get; private set; }
    /// <summary>Savunma modunda mı (kamera savaş çerçevesinde, kilitli).</summary>
    public bool InDefense { get; private set; }
    /// <summary>Test/Dev: "tüm bölgeler açık" görünümü için Kıyı Kalesi seviyesini geçersiz kılar (0: kayıttan).</summary>
    public int KeepOverride { get; private set; }

    private Transform worldRoot, villageRoot, pawnsRoot;
    private GameObject terrainObject, natureObject;
    private readonly List<SpriteRenderer> postMarkers = new List<SpriteRenderer>();
    private readonly List<IslandCloud> edgeClouds = new List<IslandCloud>();
    private readonly List<IslandZoneCloud> zoneClouds = new List<IslandZoneCloud>();

    private static readonly Color WaterColor = new Color32(0x47, 0xAB, 0xA9, 0xFF);

    private void Awake()
    {
        Instance = this;
        // BattleScene'e gömülü savaş kurulumu bir kare sonra Start ile savaşı başlatmasın.
        var hostSetup = FindFirstObjectByType<BattleSceneSetup>();
        if (hostSetup != null) hostSetup.enabled = false;
        if (GameManager.Instance == null) new GameObject("GameManager").AddComponent<GameManager>();
        if (SpriteManager.Instance == null) new GameObject("SpriteManager").AddComponent<SpriteManager>();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private IEnumerator Start()
    {
        // GameManager.Start kayıtlı oyunu yükler: bir kare bekle.
        yield return null;
        var gm = GameManager.Instance;
        if (gm.PlayerData == null)
        {
            Debug.LogWarning("[IslandSceneSetup] PlayerData yok: yeni oyunla devam ediliyor (test modu).");
            gm.StartNewGame(WeaponType.Axe);
            yield break;
        }
        if (gm.PlayerData.island == null) gm.PlayerData.island = new IslandSaveData();
        if (!IslandSession.Unlocked(gm.GetMainBaseLevel()) && !DevSettings.Enabled)
        {
            IslandSession.Clear();
            gm.LoadBaseWorld();
            yield break;
        }
        Build();
    }

    // ==================== KURULUM ====================

    /// <summary>Tüm ada nesnelerini kurar (testler doğrudan çağırabilir).</summary>
    public void Build()
    {
        if (Ready) return;
        DisableHostLeftovers();
        SetupCamera();
        worldRoot = new GameObject("IslandWorld").transform;
        terrainObject = Spawn("Island/IslandTerrain");
        natureObject = Spawn("Island/IslandNature");
        CollectClouds();

        if (BattleVfxManager.Instance == null) new GameObject("IslandVfx").AddComponent<BattleVfxManager>();
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        int keep = IslandSession.KeepLevel();
        int archerLevel = IslandSession.ArcherLevel();
        BuildVillage(keep);
        Archer = IslandArcher.Create(archerLevel, keep, 0);
        Archer.AutoFire = IslandSession.AutoFireUnlocked(keep) && (IslandSession.Save?.autoFire ?? false);
        BuildPostMarkers();

        Raid = gameObject.AddComponent<IslandRaidManager>();
        Raid.Bind(CastleStructure, Archer);
        Raid.StateChanged += OnRaidState;
        Hud = new GameObject("IslandHud").AddComponent<IslandHud>();
        Hud.Build(this, Raid, Archer, WorldCamera);
        IslandStats.Reset();
        ShowHealthBars(false);
        RefreshZones();
        CameraController.SetDefaultView();
        Ready = true;
    }

    private GameObject Spawn(string resourcePath)
    {
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[IslandSceneSetup] {resourcePath} bulunamadı: Tools/Island/Bake Island Terrain çalıştırın.");
            return null;
        }
        var instance = Instantiate(prefab, worldRoot);
        instance.name = prefab.name;
        return instance;
    }

    /// <summary>BattleScene'in sahneye gömülü eski öğeleri (savaş kurulumu, zemin) ada modunda kapatılır.</summary>
    private void DisableHostLeftovers()
    {
        var battleSetup = FindFirstObjectByType<BattleSceneSetup>();
        if (battleSetup != null) battleSetup.enabled = false;
        var ground = GameObject.Find("Ground");
        if (ground != null) ground.SetActive(false);
        var follow = Camera.main != null ? Camera.main.GetComponent<BattleCameraFollow>() : null;
        if (follow != null) follow.enabled = false;
    }

    private void SetupCamera()
    {
        WorldCamera = Camera.main;
        if (WorldCamera == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            WorldCamera = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        WorldCamera.orthographic = true;
        WorldCamera.backgroundColor = WaterColor;   // yedek: su karosu zaten her yeri kaplar
        WorldCamera.clearFlags = CameraClearFlags.SolidColor;
        var position = WorldCamera.transform.position;
        WorldCamera.transform.position = new Vector3(IslandLayout.Width * 0.5f, IslandLayout.Height * 0.5f, -10f);
        CameraController = IslandCameraController.Attach(WorldCamera);
    }

    private float lastAspect;

    private void Update()
    {
        if (WorldCamera != null && !Mathf.Approximately(lastAspect, WorldCamera.aspect))
        {
            lastAspect = WorldCamera.aspect;
            if (InDefense && CameraController.FullFrame) CameraController.SetBattleView();
            else if (Ready) CameraController.ClampView();
        }
        UpdatePostMarkers();
    }

    // ==================== MOD GEÇİŞİ ====================

    private void OnRaidState(IslandRaidManager.State state)
    {
        if (state == IslandRaidManager.State.Running) SetDefense(true);
        else if (state == IslandRaidManager.State.Idle) SetDefense(false);
    }

    private void SetDefense(bool defense)
    {
        if (InDefense == defense) return;
        InDefense = defense;
        if (pawnsRoot != null) pawnsRoot.gameObject.SetActive(!defense);   // piyonlar en yakın yapıya koşup saklanır
        ShowHealthBars(defense);
        foreach (var cloud in edgeClouds) if (cloud != null) cloud.SetAlphaMultiplier(defense ? Progression.Island.EdgeCloudBattleAlpha : 1f);
        RefreshZones();
        if (defense) CameraController.EnterBattle(Progression.Island.BattleCameraSeconds);
        else CameraController.ExitBattle(Progression.Island.BattleCameraSeconds);
    }

    /// <summary>Üs görünümünde can çubuğu yok (temiz ada); savunmada görünür.</summary>
    private static void ShowHealthBars(bool visible)
    {
        foreach (var bar in FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bar == null) continue;
            var owner = bar.GetComponentInParent<IslandBuilding>();
            bar.gameObject.SetActive(visible && (owner == null || !owner.Ruined));   // kalıntının çubuğu yok
        }
    }

    /// <summary>"Tüm bölgeler açık" görünümü (Dev/test): Kıyı Kalesi seviyesini geçersiz kılar.</summary>
    public void SetKeepOverride(int keepLevel)
    {
        KeepOverride = keepLevel;
        RefreshZones();
    }

    private void CollectClouds()
    {
        if (natureObject == null) return;
        edgeClouds.AddRange(natureObject.GetComponentsInChildren<IslandCloud>(true));
        zoneClouds.AddRange(natureObject.GetComponentsInChildren<IslandZoneCloud>(true));
    }

    /// <summary>Kilitli bölge bulutları: bölge açıksa ya da savunmadaysak gizli.</summary>
    public void RefreshZones()
    {
        int keep = KeepOverride > 0 ? KeepOverride : IslandSession.KeepLevel();
        foreach (var cloud in zoneClouds)
            if (cloud != null) cloud.gameObject.SetActive(!InDefense && !IslandLayout.ZoneUnlocked(cloud.zone, keep));
    }

    // ==================== KALE VE HAZIR KÖY ====================

    private SpriteRenderer MakeBuilding(string spriteName, Vector2 feet, float feetPixels, int bias, string objectName)
    {
        var catalog = IslandVisualCatalog.Instance;
        var sprite = catalog != null ? catalog.Single(spriteName) : null;
        var go = new GameObject(objectName);
        go.transform.SetParent(villageRoot, false);
        go.transform.position = new Vector3(feet.x, feet.y + feetPixels / 64f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = IslandYSort.OrderFor(feet.y, bias);
        return sr;
    }

    private void BuildVillage(int keep)
    {
        villageRoot = new GameObject("Village").transform;
        villageRoot.SetParent(worldRoot, false);
        pawnsRoot = new GameObject("Pawns").transform;
        pawnsRoot.SetParent(worldRoot, false);
        var catalog = IslandVisualCatalog.Instance;

        IslandBuilding.PawnsParent = pawnsRoot;
        foreach (var spot in IslandLayout.Buildings)
        {
            Vector2 feet = spot.FeetWorld;
            SpriteRenderer renderer = null;
            switch (spot.kind)
            {
                case IslandLayout.BuildingKind.Castle: renderer = MakeBuilding("building_castle", feet, 121f, -30, "CoastalKeep"); break;
                case IslandLayout.BuildingKind.House: renderer = MakeBuilding("building_house1", feet, 69f, 0, "House"); break;
                case IslandLayout.BuildingKind.Lumber: renderer = MakeBuilding("building_house2", feet, 77f, 0, "LumberCamp"); break;
                case IslandLayout.BuildingKind.Pasture: renderer = MakeBuilding("building_house3", feet, 70f, 0, "Pasture"); break;
                case IslandLayout.BuildingKind.Archery: renderer = MakeBuilding("building_archery", feet, 104f, 0, "ArcheryRange"); break;
                case IslandLayout.BuildingKind.Tower: renderer = MakeBuilding("building_tower", feet, 94f, 0, "WatchTower"); break;
            }
            if (renderer == null) continue;
            // Her ada binası savaşta bir BattleStructure olur (§7.5); hazır köy sabit seviyede (Faz B bağlayana kadar).
            var building = IslandBuilding.Attach(renderer, spot, keep);
            if (spot.kind == IslandLayout.BuildingKind.Castle) CastleStructure = building.Structure;
        }

        // Piyonlar: oduncu, çoban, madenci, usta, 2 boşta (en az 3 farklı iş).
        var lumber = IslandLayout.Building(IslandLayout.BuildingKind.Lumber);
        var pasture = IslandLayout.Building(IslandLayout.BuildingKind.Pasture);
        var house = IslandLayout.Building(IslandLayout.BuildingKind.House);
        IslandPawn.Create(pawnsRoot, catalog, IslandPawn.Job.Lumberjack, IslandLayout.World(lumber.DoorCell));
        IslandPawn.Create(pawnsRoot, catalog, IslandPawn.Job.Shepherd, IslandLayout.World(pasture.DoorCell));
        IslandPawn.Create(pawnsRoot, catalog, IslandPawn.Job.Builder, IslandLayout.World(house.col - 0.55f, house.row + house.h + 0.1f));
        var stone = IslandLayout.GoldStones[0];
        var route = new[]
        {
            IslandLayout.World(15.5f, 9.5f), IslandLayout.World(15.5f, 7.5f), IslandLayout.World(15.5f, 6.5f),
            IslandLayout.World(13.5f, 6.5f), IslandLayout.World(11.6f, 6.6f),
        };
        IslandPawn.Create(pawnsRoot, catalog, IslandPawn.Job.Miner, IslandLayout.World(stone.x - 0.25f, stone.y + 1.15f), route);
        IslandPawn.Create(pawnsRoot, catalog, IslandPawn.Job.Idler, IslandLayout.World(9.4f, 6.7f));
        IslandPawn.Create(pawnsRoot, catalog, IslandPawn.Job.Idler, IslandLayout.World(12.2f, 6.7f));

        // Koyunlar çayırda otlar.
        var meadow = IslandLayout.Meadow;
        var rect = Rect.MinMaxRect(meadow.xMin + 0.5f, IslandLayout.Height - meadow.yMax + 0.3f,
            meadow.xMax - 0.5f, IslandLayout.Height - meadow.yMin - 0.3f);
        for (int i = 0; i < IslandLayout.SheepCount && catalog != null; i++)
        {
            var feet = new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(rect.yMin, rect.yMax));
            IslandSheep.Create(villageRoot, rect, feet, catalog.Frames("sheep_grass"), catalog.Frames("sheep_idle"),
                catalog.Frames("sheep_move"), 20f);
        }
    }

    // ==================== ATIŞ YERİ İŞARETLERİ ====================

    private void BuildPostMarkers()
    {
        var catalog = IslandVisualCatalog.Instance;
        var sprite = catalog != null ? catalog.Single("cursor_brackets") : null;
        for (int i = 0; i < IslandLayout.Posts.Length; i++)
        {
            var go = new GameObject("PostMarker_" + IslandLayout.Posts[i].Name);
            go.transform.SetParent(worldRoot, false);
            go.transform.position = IslandLayout.Posts[i].World;
            go.transform.localScale = Vector3.one * 0.55f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -5;   // zemin kaplamaları bandı (-20…-1): arazinin üstünde, nesnelerin altında
            sr.enabled = false;
            postMarkers.Add(sr);
        }
    }

    private void UpdatePostMarkers()
    {
        if (Archer == null) return;
        int keep = IslandSession.KeepLevel();
        for (int i = 0; i < postMarkers.Count; i++)
        {
            var sr = postMarkers[i];
            if (sr == null) continue;
            bool unlocked = IslandLayout.PostUnlocked(i, keep);
            bool current = !Archer.Running && Archer.CurrentPost == i;
            sr.enabled = InDefense && unlocked && !current;
            bool tutorialPulse = Raid != null && Raid.TutorialStep == 2 && Raid.Phase == IslandRaidManager.State.Running && i == 3;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (tutorialPulse ? 7f : 3f));
            sr.color = tutorialPulse ? new Color(1f, 0.85f, 0.25f, 0.5f + 0.5f * pulse)
                : new Color(1f, 0.95f, 0.7f, 0.45f + 0.3f * pulse);
            sr.transform.localScale = Vector3.one * (tutorialPulse ? 0.65f + 0.1f * pulse : 0.55f);
        }
    }

    // ==================== DOKUNUŞ ====================

    public void HandleScreenTap(Vector2 screen)
    {
        if (WorldCamera == null) return;
        Vector3 world = WorldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -WorldCamera.transform.position.z));
        HandleWorldTap(new Vector2(world.x, world.y));
    }

    /// <summary>Dünya dokunuşu (yalnız savunmada): hedef kilidi → atış yeri → boş su (kilidi kaldırır).</summary>
    public void HandleWorldTap(Vector2 world)
    {
        if (Archer == null || !Archer.IsAlive || Raid == null || Raid.Phase != IslandRaidManager.State.Running) return;
        IslandTarget best = null;
        float bestDistance = 1.0f;
        foreach (var target in IslandTarget.Collect())
        {
            float d = Vector2.Distance(world, target.Position);
            if (d < bestDistance) { bestDistance = d; best = target; }
        }
        if (best != null)
        {
            if (Vector2.Distance(Archer.Position, best.Position) <= Archer.Range) Archer.LockTarget(best);
            else Hud?.Toast(L.T("Hedef menzil dışında.", "Target out of range."));
            return;
        }
        int keep = IslandSession.KeepLevel();
        int nearest = -1;
        float nearestDistance = 1.1f;
        for (int i = 0; i < IslandLayout.Posts.Length; i++)
        {
            float d = Vector2.Distance(world, IslandLayout.Posts[i].World);
            if (d < nearestDistance) { nearestDistance = d; nearest = i; }
        }
        if (nearest >= 0)
        {
            if (!IslandLayout.PostUnlocked(nearest, keep))
                Hud?.Toast(L.T($"{IslandLayout.Posts[nearest].Name}: Kıyı Kalesi {Progression.Island.PostUnlock[nearest]}'te açılır.",
                    $"{IslandLayout.Posts[nearest].Name}: unlocks at Keep {Progression.Island.PostUnlock[nearest]}."));
            else Archer.MoveToPost(nearest);
            return;
        }
        Archer.LockTarget(null);
    }
}
