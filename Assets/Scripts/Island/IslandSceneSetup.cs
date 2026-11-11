using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

/// <summary>
/// Martı Adası sahne kurulumu (island_base.md §10): ayrı .unity sahnesi yok; BattleScene ev sahibi olarak kodla kurulur
/// (<see cref="IslandSession.Active"/> iken SceneAutoSetup bunu ekler). Tilemap (zemin, yar, merdiven, köpük, bulut), kale,
/// Okçu Kaptan, atış yeri işaretleri, Akın yöneticisi ve HUD burada birleşir. Katalog yoksa yedek görseller çizilir.
/// </summary>
public sealed class IslandSceneSetup : MonoBehaviour
{
    public static IslandSceneSetup Instance { get; private set; }

    public Camera WorldCamera { get; private set; }
    public IslandArcher Archer { get; private set; }
    public IslandRaidManager Raid { get; private set; }
    public IslandHud Hud { get; private set; }
    public BattleStructure CastleStructure { get; private set; }
    /// <summary>Kurulum bitti mi (testler bekler).</summary>
    public bool Ready { get; private set; }

    private Transform worldRoot;
    private readonly List<SpriteRenderer> foamRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> postMarkers = new List<SpriteRenderer>();
    private readonly List<Transform> clouds = new List<Transform>();
    private float foamClock;

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
        BuildTerrain();
        BuildFoam();
        BuildDecor();
        BuildClouds();

        if (BattleVfxManager.Instance == null) new GameObject("IslandVfx").AddComponent<BattleVfxManager>();
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        int keep = IslandSession.KeepLevel();
        int archerLevel = IslandSession.ArcherLevel();
        BuildCastle(keep);
        Archer = IslandArcher.Create(archerLevel, keep, 0);
        Archer.AutoFire = IslandSession.AutoFireUnlocked(keep) && (IslandSession.Save?.autoFire ?? false);
        BuildPostMarkers(keep);

        Raid = gameObject.AddComponent<IslandRaidManager>();
        Raid.Bind(CastleStructure, Archer);
        Hud = new GameObject("IslandHud").AddComponent<IslandHud>();
        Hud.Build(this, Raid, Archer, WorldCamera);
        IslandStats.Reset();
        Ready = true;
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
        WorldCamera.backgroundColor = WaterColor;
        WorldCamera.clearFlags = CameraClearFlags.SolidColor;
        FitCamera();
    }

    /// <summary>Savaş kamerası haritanın tamamını gösterir (kaydırma yok): 9:16'da 12,5 birim; dar ekranda genişliğe sığdırır.</summary>
    public static float CameraSizeFor(float aspect) =>
        Mathf.Max(IslandLayout.Height / 2f, IslandLayout.Width / 2f / Mathf.Max(0.3f, aspect));

    private void FitCamera()
    {
        WorldCamera.orthographicSize = CameraSizeFor(WorldCamera.aspect);
        WorldCamera.transform.position = new Vector3(IslandLayout.Width / 2f, IslandLayout.Height / 2f, -10f);
    }

    private float lastAspect;

    private void Update()
    {
        if (WorldCamera != null && !Mathf.Approximately(lastAspect, WorldCamera.aspect))
        {
            lastAspect = WorldCamera.aspect;
            FitCamera();
        }
        AnimateFoamAndClouds();
        UpdatePostMarkers();
    }

    // ==================== ZEMİN ====================

    private static readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();

    private static Tile MakeTile(Sprite sprite, Color color)
    {
        string key = sprite.GetInstanceID() + "_" + color;
        if (tileCache.TryGetValue(key, out var tile) && tile != null) return tile;
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = color;
        tile.flags = TileFlags.None;
        tileCache[key] = tile;
        return tile;
    }

    private void BuildTerrain()
    {
        var grid = worldRoot.gameObject.AddComponent<Grid>();
        grid.cellSize = Vector3.one;
        var groundObject = new GameObject("IslandGround");
        groundObject.transform.SetParent(worldRoot, false);
        var tilemap = groundObject.AddComponent<Tilemap>();
        groundObject.AddComponent<TilemapRenderer>().sortingOrder = -20;
        var catalog = IslandVisualCatalog.Instance;
        bool art = catalog != null && catalog.Tile(1, 1) != null;

        for (int row = 0; row < IslandLayout.Height; row++)
        for (int col = 0; col < IslandLayout.Width; col++)
        {
            char c = IslandLayout.Cell(col, row);
            var pos = new Vector3Int(col, IslandLayout.Height - 1 - row, 0);
            Sprite sprite;
            Color color;
            switch (c)
            {
                case '~': case 'A': case 'B': case 'C': case 'D': continue;
                case ':':
                    sprite = IslandArt.Square;
                    color = new Color(0.62f, 0.9f, 0.88f, 0.55f);
                    break;
                case '#':
                    sprite = art ? catalog.Tile(6, 4) ?? catalog.Tile(1, 1) : IslandArt.Square;
                    color = art ? Color.white : new Color(0.42f, 0.4f, 0.36f);
                    break;
                case 'H':
                    sprite = IslandArt.Stairs;
                    color = Color.white;
                    break;
                case 's':
                    sprite = art ? catalog.Tile(1, 1) : IslandArt.Square;
                    color = art ? new Color(1f, 0.95f, 0.72f) : new Color(0.9f, 0.84f, 0.56f);
                    break;
                default:
                {
                    int h = IslandLayout.HeightAt(col, row);
                    if (art)
                    {
                        sprite = h >= 1 ? catalog.Tile(6, 1) ?? catalog.Tile(1, 1) : catalog.Tile(1, 1);
                        color = h == 2 ? new Color(1f, 1f, 0.9f) : h == 1 ? new Color(0.95f, 1f, 0.92f) : Color.white;
                    }
                    else
                    {
                        sprite = IslandArt.Square;
                        color = h == 2 ? new Color(0.62f, 0.82f, 0.4f) : h == 1 ? new Color(0.5f, 0.76f, 0.34f) : new Color(0.4f, 0.68f, 0.3f);
                    }
                    // Hafif tekdüzelik kırıcı: hücre başına küçük ton farkı.
                    float variation = ((col * 7 + row * 13) % 5) * 0.012f;
                    color = new Color(color.r - variation, color.g - variation * 0.5f, color.b - variation, color.a);
                    break;
                }
            }
            tilemap.SetTile(pos, MakeTile(sprite, color));
        }
        // Basamak yönü çizgisi: Merdiven hücreleri üzerine yalnız görsel ipucu (zaten Stairs sprite'ı).
        tilemap.CompressBounds();
    }

    private bool IsCoast(int col, int row)
    {
        if (!IslandLayout.IsLand(col, row)) return false;
        return !IslandLayout.IsLand(col - 1, row) || !IslandLayout.IsLand(col + 1, row)
            || !IslandLayout.IsLand(col, row - 1) || !IslandLayout.IsLand(col, row + 1);
    }

    private void BuildFoam()
    {
        var catalog = IslandVisualCatalog.Instance;
        bool art = catalog != null && IslandVisualCatalog.Has(catalog.foam);
        for (int row = 0; row < IslandLayout.Height; row++)
        for (int col = 0; col < IslandLayout.Width; col++)
        {
            if (!IsCoast(col, row)) continue;
            var go = new GameObject("Foam");
            go.transform.SetParent(worldRoot, false);
            go.transform.position = IslandLayout.World(col + 0.5f, row + 0.5f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -25;
            if (art)
            {
                sr.sprite = catalog.foam[0];
                go.transform.localScale = Vector3.one;
            }
            else
            {
                sr.sprite = IslandArt.Circle;
                sr.color = new Color(1f, 1f, 1f, 0.4f);
                go.transform.localScale = Vector3.one * 1.7f;
            }
            foamRenderers.Add(sr);
        }
    }

    private void BuildDecor()
    {
        var catalog = IslandVisualCatalog.Instance;
        for (int row = 0; row < IslandLayout.Height; row++)
        for (int col = 0; col < IslandLayout.Width; col++)
        {
            char c = IslandLayout.Cell(col, row);
            Vector2 world = IslandLayout.World(col + 0.5f, row + 0.5f);
            switch (c)
            {
                case 'T':
                    MakeDecor("Tree", IslandArt.Tree, world + new Vector2(0f, -0.35f), 1.15f, Color.white);
                    break;
                case 'o':
                    if (catalog != null && IslandVisualCatalog.Has(catalog.sheepIdle))
                    {
                        var sheep = MakeDecor("Sheep", catalog.sheepIdle[0], world, 0.5f, Color.white);
                        sheep.gameObject.AddComponent<IslandSpriteAnimator>().Setup(sheep,
                            new IslandUnitFrames { idle = catalog.sheepIdle });
                    }
                    else MakeDecor("Sheep", IslandArt.Circle, world, 0.45f, new Color(0.96f, 0.96f, 0.92f));
                    break;
                case 't':
                    MakeDecor("TowerPlot", IslandArt.Ring, world, 0.9f, new Color(0.95f, 0.9f, 0.7f, 0.55f), false).sortingOrder = -15;
                    break;
                case 'b':
                    MakeDecor("BuildPlot", IslandArt.Square, world, 0.8f, new Color(0.85f, 0.78f, 0.55f, 0.35f), false).sortingOrder = -15;
                    break;
            }
        }
    }

    private SpriteRenderer MakeDecor(string name, Sprite sprite, Vector2 position, float scale, Color color, bool ySort = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(worldRoot, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        if (ySort) go.AddComponent<IslandYSort>();
        return sr;
    }

    private void BuildClouds()
    {
        var catalog = IslandVisualCatalog.Instance;
        bool art = catalog != null && IslandVisualCatalog.Has(catalog.clouds);
        // Süzülen bulutlar (denizin üstünde)
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Cloud");
            go.transform.SetParent(worldRoot, false);
            go.transform.position = new Vector3(Random.Range(0f, IslandLayout.Width), 3f + i * 6f + Random.Range(-1f, 1f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 700;
            if (art)
            {
                sr.sprite = catalog.clouds[i % catalog.clouds.Length];
                go.transform.localScale = Vector3.one * 0.9f;
                sr.color = new Color(1f, 1f, 1f, 0.55f);
            }
            else
            {
                sr.sprite = IslandArt.Circle;
                go.transform.localScale = new Vector3(4f, 1.4f, 1f);
                sr.color = new Color(1f, 1f, 1f, 0.2f);
            }
            clouds.Add(go.transform);
        }
        // Kilitli alanların üstündeki bulutlar: Kıyı Kalesi seviyesi açınca yoktur.
        int keep = IslandSession.KeepLevel();
        foreach (var area in IslandLayout.Areas)
        {
            if (keep >= area.unlockKeep) continue;
            var go = new GameObject("LockCloud_" + area.name);
            go.transform.SetParent(worldRoot, false);
            go.transform.position = IslandLayout.World((area.x0 + area.x1 + 1) / 2f, (area.y0 + area.y1 + 1) / 2f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 800;
            sr.sprite = art ? catalog.clouds[0] : IslandArt.Circle;
            float w = (area.x1 - area.x0 + 1), h = (area.y1 - area.y0 + 1);
            go.transform.localScale = art ? Vector3.one * Mathf.Max(w, h) / 3f : new Vector3(w, h, 1f);
            sr.color = new Color(1f, 1f, 1f, 0.92f);
        }
    }

    private void AnimateFoamAndClouds()
    {
        var catalog = IslandVisualCatalog.Instance;
        foamClock += Time.deltaTime;
        if (catalog != null && IslandVisualCatalog.Has(catalog.foam))
        {
            var frame = catalog.foam[(int)(foamClock * 8f) % catalog.foam.Length];
            foreach (var sr in foamRenderers) if (sr != null) sr.sprite = frame;
        }
        else
        {
            float alpha = 0.3f + 0.15f * Mathf.Sin(foamClock * 2.5f);
            foreach (var sr in foamRenderers) if (sr != null) sr.color = new Color(1f, 1f, 1f, alpha);
        }
        foreach (var cloud in clouds)
        {
            if (cloud == null) continue;
            cloud.position += Vector3.right * 0.25f * Time.deltaTime;
            if (cloud.position.x > IslandLayout.Width + 4f) cloud.position = new Vector3(-4f, cloud.position.y, 0f);
        }
    }

    // ==================== KALE ====================

    private void BuildCastle(int keepLevel)
    {
        var go = new GameObject("CoastalKeep");
        var sr = go.AddComponent<SpriteRenderer>();
        var catalog = IslandVisualCatalog.Instance;
        sr.sprite = catalog != null && catalog.castle != null ? catalog.castle : IslandArt.Castle;
        // Genişlik ≈ 3,3 birim; alt kenar 6. satırın üstüne oturur.
        float width = sr.sprite.bounds.size.x;
        float scale = 3.3f / Mathf.Max(0.01f, width);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        Vector2 bottom = IslandLayout.CastleBottomCenter;
        go.transform.position = new Vector3(bottom.x, bottom.y - sr.sprite.bounds.min.y * scale + 0.05f, 0f);
        float visualHeight = sr.sprite.bounds.size.y * scale;
        go.AddComponent<IslandYSort>().bias = -30;
        CastleStructure = go.AddComponent<BattleStructure>();
        CastleStructure.Initialize(BuildingType.MainBase, keepLevel, null, IslandLayout.CastleCenter, visualHeight * 0.5f);
        CastleStructure.SetMaxHealth(Progression.Island.CastleHp(keepLevel));
    }

    // ==================== ATIŞ YERİ İŞARETLERİ ====================

    private void BuildPostMarkers(int keep)
    {
        for (int i = 0; i < IslandLayout.Posts.Length; i++)
        {
            var go = new GameObject("PostMarker_" + IslandLayout.Posts[i].Name);
            go.transform.SetParent(worldRoot, false);
            go.transform.position = IslandLayout.Posts[i].World;
            go.transform.localScale = Vector3.one * 1.0f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = IslandArt.Ring;
            sr.sortingOrder = -5;
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
            sr.enabled = unlocked && !current;
            bool tutorialPulse = Raid != null && Raid.TutorialStep == 2 && Raid.Phase == IslandRaidManager.State.Running && i == 3;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (tutorialPulse ? 7f : 3f));
            sr.color = tutorialPulse ? new Color(1f, 0.85f, 0.25f, 0.5f + 0.5f * pulse)
                : new Color(1f, 0.9f, 0.5f, 0.35f + 0.25f * pulse);
            sr.transform.localScale = Vector3.one * (tutorialPulse ? 1.2f + 0.2f * pulse : 1f);
        }
    }

    // ==================== DOKUNUŞ ====================

    public void HandleScreenTap(Vector2 screen)
    {
        if (WorldCamera == null) return;
        Vector3 world = WorldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -WorldCamera.transform.position.z));
        HandleWorldTap(new Vector2(world.x, world.y));
    }

    /// <summary>Dünya dokunuşu: hedef kilidi → atış yeri → boş su (kilidi kaldırır).</summary>
    public void HandleWorldTap(Vector2 world)
    {
        if (Archer == null || !Archer.IsAlive) return;
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
