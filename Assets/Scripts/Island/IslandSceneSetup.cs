using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    /// <summary>Faz D: ufuk tekneleri + kırmızı kumsal işareti (zamanlı baskın).</summary>
    public IslandRaidVisuals RaidVisuals { get; private set; }
    public BattleStructure CastleStructure { get; private set; }
    /// <summary>Kurulum bitti mi (testler bekler).</summary>
    public bool Ready { get; private set; }
    /// <summary>Savunma modunda mı (kamera savaş çerçevesinde, kilitli).</summary>
    public bool InDefense { get; private set; }
    /// <summary>Test/Dev: "tüm bölgeler açık" görünümü için Kıyı Kalesi seviyesini geçersiz kılar (0: kayıttan).</summary>
    public int KeepOverride { get; private set; }

    private Transform worldRoot, pawnsRoot;
    private GameObject terrainObject, natureObject;
    private readonly List<PostMarker> postMarkers = new List<PostMarker>();
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
        WalkGridDebug.Install(IslandWalkGrid.Shared);
        SelectionManager.InstallBackKey();   // 4. oturum (1.2)   // 4. oturum (1.1): F6 yürünebilirlik + rotalar
        SetupCamera();
        worldRoot = new GameObject("IslandWorld").transform;
        terrainObject = Spawn("Island/IslandTerrain");
        natureObject = Spawn("Island/IslandNature");
        IslandNatureClear.Bind(natureObject);   // 4. oturum (1.4): temizlenebilir çalı/kaya
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
        IslandAbilities.Attach(Archer);
        // 6. oturum (3.2): Okçu Kaptan ancak Okçuluk Alanı tamamlanınca adada (öğreticiyle kurulur, binadan çıkıp yürür).
        if (!IslandEconomy.HasArcheryReady(IslandSession.Save)) Archer.gameObject.SetActive(false);
        BuildPostMarkers();

        Raid = gameObject.AddComponent<IslandRaidManager>();
        Raid.Bind(CastleStructure, Archer);
        Raid.StateChanged += OnRaidState;
        Hud = new GameObject("IslandHud").AddComponent<IslandHud>();
        Hud.Build(this, Raid, Archer, WorldCamera);
        RaidVisuals = new GameObject("IslandRaidVisuals").AddComponent<IslandRaidVisuals>();
        RaidVisuals.StartRequested = Hud.StartRaidDefense;
        IslandStats.Reset();
        ShowHealthBars(false);
        RefreshZones();
        CameraController.SetDefaultView();
        Ready = true;
    }

    /// <summary>6. oturum (3.2): Okçuluk Alanı bitti → okçu binanın kapısında belirir ve ilk atış yerine koşar.</summary>
    public void RevealArcher()
    {
        if (Archer == null || Archer.gameObject.activeSelf) return;
        var save = IslandSession.Save;
        Vector2 door = Archer.transform.position;
        if (save?.placed != null)
            foreach (var b in save.placed)
                if (b != null && b.level >= 1 && b.kind == nameof(IslandLayout.BuildingKind.Archery))
                {
                    var spot = new IslandLayout.BuildingSpot { kind = IslandLayout.BuildingKind.Archery, col = b.col, row = b.row,
                        w = IslandLayout.Building(IslandLayout.BuildingKind.Archery).w, h = IslandLayout.Building(IslandLayout.BuildingKind.Archery).h };
                    door = IslandLayout.World(spot.DoorCell);
                    break;
                }
        Archer.transform.position = new Vector3(door.x, door.y, Archer.transform.position.z);
        Archer.gameObject.SetActive(true);
        Archer.MoveToPost(0);
        BattleVfxManager.Instance?.SpawnSpark(door);
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
        // Akın sürerken savaş müziği; Idle/Won/Lost sakin müzik (FeatureConfig.islandCalmMusic)
        if (FeatureConfig.Instance.islandCalmMusic)
            AudioManager.Instance?.PlayIslandBgm(state == IslandRaidManager.State.Running);
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
        Village?.RebuildPawns();
    }

    private void CollectClouds()
    {
        if (natureObject == null) return;
        edgeClouds.AddRange(natureObject.GetComponentsInChildren<IslandCloud>(true));
        zoneClouds.AddRange(natureObject.GetComponentsInChildren<IslandZoneCloud>(true));
    }

    /// <summary>Kilitli bölge bulutları: bölge açıksa (kayıttaki maske) ya da savunmadaysak gizli. Dev "hepsi açık" görünümü KeepOverride ile.</summary>
    public void RefreshZones()
    {
        int mask = IslandSession.Save != null ? IslandSession.Save.zoneMask : 1;
        foreach (var cloud in zoneClouds)
        {
            if (cloud == null) continue;
            bool open = KeepOverride > 0 || IslandGrid.ZoneOpen(cloud.zone, mask);
            // Dağılma animasyonu sürerken kapatma (animasyon kendi bitirir)
            if (cloud.Dissolving) continue;
            // 3. oturum (1.1): savunmada da kilitli bölge bulutlu kalır (yarı saydam): HARİTA adanın gerçek durumunu gösterir
            cloud.SetDim(InDefense ? Progression.Island.ZoneCloudBattleAlpha : 1f);
            cloud.gameObject.SetActive(!open);
        }
    }

    // ==================== KALE VE KÖY (Faz B: kayıttan) ====================

    public IslandVillage Village { get; private set; }

    private void BuildVillage(int keep)
    {
        Village = gameObject.GetComponent<IslandVillage>();
        if (Village == null) Village = gameObject.AddComponent<IslandVillage>();   // Unity sahte-null: ?? kullanılmaz
        Village.Build(this, worldRoot, keep);
        pawnsRoot = Village.PawnsRoot;
        CastleStructure = Village.CastleStructure;
    }

    /// <summary>Bölge açılınca bulutlar kayıp dağılır (1,5 sn); Kale ve işaretçiler değişmez.</summary>
    public void DisperseZone(int zone)
    {
        Vector2 center = ZoneCenter(zone);
        foreach (var cloud in zoneClouds)
        {
            if (cloud == null || cloud.zone != zone || !cloud.gameObject.activeSelf) continue;
            Vector2 away = (Vector2)cloud.transform.position - center;
            if (away.sqrMagnitude < 0.01f) away = Vector2.up;
            cloud.Dissolve(away, Progression.Island.ZoneDispersalSeconds);
        }
    }

    /// <summary>Bölgenin bulut merkezi (görev işareti ve dağılma yönü).</summary>
    public Vector2 ZoneCenter(int zone)
    {
        Vector2 sum = Vector2.zero; int count = 0;
        foreach (var cloud in zoneClouds)
            if (cloud != null && cloud.zone == zone) { sum += cloud.anchor; count++; }
        return count > 0 ? sum / count : IslandLayout.CastleCenter;
    }

    /// <summary>Dokunulan noktadaki (görünür) bölge bulutunun bölgesi; yoksa -1.</summary>
    public int ZoneCloudAt(Vector2 world)
    {
        int best = -1; float bestDistance = 2.6f;
        foreach (var cloud in zoneClouds)
        {
            if (cloud == null || !cloud.gameObject.activeSelf || cloud.Dissolving) continue;
            float d = Vector2.Distance(world, cloud.transform.position);
            if (d < bestDistance) { bestDistance = d; best = cloud.zone; }
        }
        return best;
    }

    // ==================== ATIŞ YERİ İŞARETLERİ ====================
    // 3. oturum 3.2: yuvarlak işaretler. Altın dolu = okçunun bulunduğu yer · gidilen yer = turuncu hızlı nabız ·
    // ulaşılabilir = açık halka (nabız) · kilitli = soluk gri + kilit simgesi (dokunulmaz, dokununca kilit bilgisi).

    public sealed class PostMarker
    {
        public GameObject root;
        public SpriteRenderer ring, fill, lockIcon;
        public string State = "";
    }

    public const string PostHintKey = "MEB_PostHintShown";
    private float postHintUntil;
    private bool postHintChecked;
    private GameObject postHintLabel;

    public IReadOnlyList<PostMarker> PostMarkerList => postMarkers;

    private void BuildPostMarkers()
    {
        var lockSprite = CampaignSceneSetup.Art("ui_icon_lock");
        for (int i = 0; i < IslandLayout.Posts.Length; i++)
        {
            var m = new PostMarker { root = new GameObject("PostMarker_" + IslandLayout.Posts[i].Name) };
            m.root.transform.SetParent(worldRoot, false);
            m.root.transform.position = IslandLayout.Posts[i].World;
            SpriteRenderer Part(string name, Sprite sprite, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(m.root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;   // zemin kaplamaları bandı (-20…-1): arazinin üstünde, nesnelerin altında
                sr.enabled = false;
                return sr;
            }
            m.fill = Part("Fill", IslandAbilityArt.Disc(), -7);
            m.ring = Part("Ring", IslandAbilityArt.Ring(), -6);
            m.lockIcon = Part("Lock", lockSprite, -5);
            if (lockSprite != null) m.lockIcon.transform.localScale = Vector3.one * (0.45f / Mathf.Max(0.01f, lockSprite.bounds.size.y));
            postMarkers.Add(m);
        }
    }

    private void UpdatePostMarkers()
    {
        if (Archer == null) return;
        int keep = IslandSession.KeepLevel();
        int runTo = Archer.TargetPost;
        for (int i = 0; i < postMarkers.Count; i++)
        {
            var m = postMarkers[i];
            if (m == null || m.ring == null) continue;
            bool unlocked = IslandLayout.PostUnlocked(i, keep);
            bool current = !Archer.Running && Archer.CurrentPost == i;
            bool target = runTo == i;
            bool visible = InDefense;
            m.ring.enabled = m.fill.enabled = visible;
            m.lockIcon.enabled = visible && !unlocked;
            if (!visible) continue;
            bool tutorialPulse = Raid != null && Raid.TutorialStep == 2 && Raid.Phase == IslandRaidManager.State.Running && i == 3;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (target || tutorialPulse ? 8f : 3f));
            float diameter;
            if (current)
            {
                m.State = "current"; diameter = 1.15f;
                m.ring.color = new Color(1f, 0.82f, 0.2f, 1f);
                m.fill.color = new Color(1f, 0.82f, 0.2f, 0.45f);
            }
            else if (target)
            {
                m.State = "target"; diameter = 1.1f + 0.12f * pulse;
                m.ring.color = new Color(1f, 0.55f, 0.15f, 1f);
                m.fill.color = new Color(1f, 0.55f, 0.15f, 0.3f);
            }
            else if (unlocked)
            {
                m.State = "reachable"; diameter = tutorialPulse ? 1.05f + 0.1f * pulse : 0.95f + 0.05f * pulse;
                m.ring.color = tutorialPulse ? new Color(1f, 0.85f, 0.25f, 1f) : new Color(0.95f, 0.97f, 1f, 0.5f + 0.4f * pulse);
                m.fill.color = new Color(0.95f, 0.97f, 1f, 0.08f + 0.1f * pulse);
            }
            else
            {
                m.State = "locked"; diameter = 0.7f;
                m.ring.color = new Color(0.5f, 0.5f, 0.55f, 0.55f);
                m.fill.color = new Color(0.25f, 0.25f, 0.3f, 0.35f);
            }
            m.ring.transform.localScale = m.fill.transform.localScale = Vector3.one * diameter;
        }
        UpdatePostHint(keep);
    }

    /// <summary>İlk akında (kayıt başına bir kez) işaret ipucu: bildirim şeridi + en yakın ulaşılabilir işaretin üstünde küçük yazı.</summary>
    private void UpdatePostHint(int keep)
    {
        if (postHintLabel != null && (Time.time > postHintUntil || !InDefense)) { Destroy(postHintLabel); postHintLabel = null; }
        if (postHintChecked || !InDefense || Raid == null || Raid.Phase != IslandRaidManager.State.Running || !Archer.IsAlive) return;
        if (PlayerPrefs.GetInt(PostHintKey, 0) == 1) { postHintChecked = true; return; }
        if (!string.IsNullOrEmpty(Raid.TutorialHint())) return;   // öğretici ipucu bitince göster
        int nearest = -1; float best = float.MaxValue;
        for (int i = 0; i < IslandLayout.Posts.Length; i++)
        {
            if (i == Archer.CurrentPost || !IslandLayout.PostUnlocked(i, keep)) continue;
            float d = Vector2.Distance(Archer.Position, IslandLayout.Posts[i].World);
            if (d < best) { best = d; nearest = i; }
        }
        postHintChecked = true;
        PlayerPrefs.SetInt(PostHintKey, 1);
        PlayerPrefs.Save();
        Hud?.Toast(L.T("Okçu buraya gidip buradan savaşabilir — işarete dokun", "The archer can move here and fight from here — tap a marker"), 6f);
        if (nearest < 0) return;
        postHintUntil = Time.time + 6f;
        postHintLabel = new GameObject("PostHintLabel");
        postHintLabel.transform.position = IslandLayout.Posts[nearest].World + Vector2.up * 0.9f;
        var tmp = postHintLabel.AddComponent<TextMeshPro>();
        tmp.text = L.T("Dokun: buraya git", "Tap: go here");
        tmp.fontSize = 3.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.9f, 0.45f);
        tmp.fontStyle = FontStyles.Bold;
        IslandFx.Outline(tmp);
        tmp.sortingOrder = IslandFx.FxSorting + 3;
    }

    public bool PostHintVisible => postHintLabel != null;

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
        if (!InDefense && Raid != null && Raid.Phase == IslandRaidManager.State.Idle)
        {
            // Üs görünümü (Faz B): yığın topla, bina/kale/bulut seç; boş yere dokunuş açık paneli kapatır
            if (IslandPlacementController.Instance != null && IslandPlacementController.Instance.IsPlacing) return;
            if (RaidVisuals != null && RaidVisuals.HandleTap(world)) return;   // Faz D: tekneye/işarete dokunmak baskını savunur
            if (Village != null && Village.HandleTap(world)) return;
            Hud?.CloseBuildUi();
            return;
        }
        if (Archer == null || !Archer.IsAlive || Raid == null || Raid.Phase != IslandRaidManager.State.Running) return;
        // Ok Yağmuru nişan kipi: dokunuş halkayı taşır (menzil sınırlı)
        var abilities = IslandAbilities.Instance;
        if (abilities != null && abilities.Aiming) { abilities.SetAim(world); return; }
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
