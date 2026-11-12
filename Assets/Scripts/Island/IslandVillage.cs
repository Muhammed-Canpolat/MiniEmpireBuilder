using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Ada köyü (island_base_v2 §5, §6, Faz B): Kale + kayıttaki yerleştirilmiş binalar (<see cref="IslandSaveData.placed"/>) kurulur;
/// inşaat alanları (level 0), Usta görevi işareti (çekiç + süre), kaynak yığınları (gerçek Tiny Swords Et/Odun sprite'ları), piyonlar
/// ve koyunlar burada yönetilir. Üretim UTC tick ile <see cref="IslandEconomy"/>'de hesaplanır; bu sınıf yalnız görüntü ve dokunuştur.
/// </summary>
public sealed class IslandVillage : MonoBehaviour
{
    public static IslandVillage Instance { get; private set; }

    public Transform PawnsRoot { get; private set; }
    public IslandBuilding Castle { get; private set; }
    public BattleStructure CastleStructure => Castle != null ? Castle.Structure : null;

    /// <summary>Dokunuş olayları (HUD panelleri bağlanır): bina/inşaat kimliği, kale, kilitli bölge.</summary>
    public Action<string> BuildingTapped;
    public Action CastleTapped;
    public Action<int> ZoneTapped;
    public Action<string> Toast;

    private IslandSceneSetup setup;
    private Transform villageRoot, workersRoot, pilesRoot;
    private int keep;
    private readonly Dictionary<string, IslandBuilding> live = new Dictionary<string, IslandBuilding>();
    private readonly Dictionary<string, Site> sites = new Dictionary<string, Site>();
    private readonly Dictionary<string, Pile> piles = new Dictionary<string, Pile>();
    private IslandPawn builderPawn;
    private IslandRallyFlag flag;
    private Pile veinPile;
    private float nextTick;
    /// <summary>Altın Damarı yığını konumu (altın taşlarının önü; ayak, dünya).</summary>
    private static readonly Vector2 VeinPileCenter = IslandLayout.World(16.3f, 10.7f);
    private Marker marker;

    private sealed class Site { public GameObject go; public SpriteRenderer sr; public IslandLayout.BuildingSpot spot; }

    private sealed class Pile
    {
        public Transform root;
        public SpriteRenderer[] icons;
        public int shown;
        public Vector2 center;
        public bool wood;
    }

    // ==================== SPRITE TABLOSU ====================

    public static string SpriteName(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.Castle: return "building_castle";
            case IslandLayout.BuildingKind.House: return "building_house1";
            case IslandLayout.BuildingKind.Lumber: return "building_house2";
            case IslandLayout.BuildingKind.Pasture: return "building_house3";
            case IslandLayout.BuildingKind.Archery: return "building_archery";
            case IslandLayout.BuildingKind.Barracks: return "building_barracks";
            case IslandLayout.BuildingKind.Monastery: return "building_monastery";
            default: return "building_tower";
        }
    }

    public static float FeetPixels(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.Castle: return 121f;
            case IslandLayout.BuildingKind.House: return 69f;
            case IslandLayout.BuildingKind.Lumber: return 77f;
            case IslandLayout.BuildingKind.Pasture: return 70f;
            case IslandLayout.BuildingKind.Archery: return 104f;
            case IslandLayout.BuildingKind.Barracks: return 109f;     // sprite 192×256, alt kenar merkezden 117 px aşağıda
            case IslandLayout.BuildingKind.Monastery: return 142f;    // sprite 192×320, alt kenar 150 px
            default: return 94f;
        }
    }

    public static string ObjectName(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.Castle: return "CoastalKeep";
            case IslandLayout.BuildingKind.House: return "House";
            case IslandLayout.BuildingKind.Lumber: return "LumberCamp";
            case IslandLayout.BuildingKind.Pasture: return "Pasture";
            case IslandLayout.BuildingKind.Archery: return "ArcheryRange";
            case IslandLayout.BuildingKind.Barracks: return "Barracks";
            case IslandLayout.BuildingKind.Monastery: return "Monastery";
            default: return "WatchTower";
        }
    }

    public static string KindName(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.House: return L.T("Ev", "House");
            case IslandLayout.BuildingKind.Lumber: return L.T("Oduncu Kampı", "Lumber Camp");
            case IslandLayout.BuildingKind.Pasture: return L.T("Ağıl", "Pasture");
            case IslandLayout.BuildingKind.Archery: return L.T("Okçuluk Alanı", "Archery Range");
            case IslandLayout.BuildingKind.Barracks: return L.T("Kışla", "Barracks");
            case IslandLayout.BuildingKind.Monastery: return L.T("Manastır", "Monastery");
            case IslandLayout.BuildingKind.Tower: return L.T("Gözcü Kulesi", "Watch Tower");
            default: return L.T("Kıyı Kalesi", "Coastal Keep");
        }
    }

    public static IslandLayout.BuildingSpot SpotFor(IslandPlacedBuilding entry)
    {
        var kind = IslandGrid.ParseKind(entry.kind);
        var size = IslandGrid.Size(kind);
        return new IslandLayout.BuildingSpot { kind = kind, col = entry.col, row = entry.row, w = size.x, h = size.y };
    }

    // ==================== KURULUM ====================

    private void OnEnable() => IslandEconomy.TaskCompleted += OnTaskCompleted;
    private void OnDisable() => IslandEconomy.TaskCompleted -= OnTaskCompleted;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Build(IslandSceneSetup owner, Transform worldRoot, int keepLevel)
    {
        Instance = this;
        setup = owner;
        keep = keepLevel;
        var save = IslandSession.Save;
        IslandEconomy.EnsureSeeded(save, keep);
        IslandEconomy.Tick(IslandRepair.Now, true);   // çevrimdışı üretim ve biten görev (onarım kaydı henüz silinmeden)

        villageRoot = new GameObject("Village").transform;
        villageRoot.SetParent(worldRoot, false);
        PawnsRoot = new GameObject("Pawns").transform;
        PawnsRoot.SetParent(worldRoot, false);
        workersRoot = new GameObject("Workers").transform;
        workersRoot.SetParent(PawnsRoot, false);
        pilesRoot = new GameObject("Piles").transform;
        pilesRoot.SetParent(PawnsRoot, false);
        IslandBuilding.PawnsParent = PawnsRoot;

        var castleSpot = IslandLayout.Building(IslandLayout.BuildingKind.Castle);
        Castle = IslandBuilding.Attach(MakeRenderer(castleSpot, "CoastalKeep", -30), castleSpot, keep, "Castle@" + castleSpot.col + "," + castleSpot.row);
        if (save != null)
            foreach (var entry in save.placed.ToArray())
                if (entry != null) SpawnEntry(entry);

        BuildSheep();
        RebuildPawns();
        RefreshFlag();
        RefreshPiles(true);
    }

    // ==================== KIŞLA BAYRAĞI (Faz E1) ====================

    public IslandRallyFlag Flag => flag;

    /// <summary>Tamamlanmış Kışla varsa bayrak durur (kayıttaki merdivende); yoksa kaldırılır.</summary>
    public void RefreshFlag()
    {
        var save = IslandSession.Save;
        bool has = save != null && IslandEconomy.CompletedOf(IslandLayout.BuildingKind.Barracks, save.placed) != null;
        if (has && flag == null) flag = IslandRallyFlag.Create(villageRoot, save.flagStair);
        else if (!has && flag != null) { Destroy(flag.gameObject); flag = null; }
    }

    /// <summary>Bayrağı sıradaki merdivene taşır (S1 → S2 → S3 → S1). Yeni merdiven indeksini döndürür.</summary>
    public int CycleFlag()
    {
        var save = IslandSession.Save;
        if (save == null) return 0;
        return SetFlag((save.flagStair + 1) % IslandLayout.FlagPoints.Length);
    }

    public int SetFlag(int stair)
    {
        var save = IslandSession.Save;
        if (save == null) return 0;
        save.flagStair = Mathf.Clamp(stair, 0, IslandLayout.FlagPoints.Length - 1);
        GameManager.Instance?.SaveGame();
        RefreshFlag();
        if (flag != null) flag.SetStair(save.flagStair, true);
        return save.flagStair;
    }

    private SpriteRenderer MakeRenderer(IslandLayout.BuildingSpot spot, string objectName, int bias)
    {
        var catalog = IslandVisualCatalog.Instance;
        var go = new GameObject(objectName);
        go.transform.SetParent(villageRoot, false);
        Vector2 feet = spot.FeetWorld;
        go.transform.position = new Vector3(feet.x, feet.y + FeetPixels(spot.kind) / 64f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = catalog != null ? catalog.Single(SpriteName(spot.kind)) : null;
        sr.sortingOrder = IslandYSort.OrderFor(feet.y, bias);
        return sr;
    }

    private void SpawnEntry(IslandPlacedBuilding entry)
    {
        var spot = SpotFor(entry);
        if (entry.level >= 1)
        {
            var building = IslandBuilding.Attach(MakeRenderer(spot, ObjectName(spot.kind), 0), spot, keep, entry.id);
            live[entry.id] = building;
            return;
        }
        var sr = MakeRenderer(spot, ObjectName(spot.kind) + "_Site", 0);
        sr.color = new Color(0.62f, 0.66f, 0.74f, 0.7f);   // inşaat alanı: gerçek bina sprite'ı soluk
        sites[entry.id] = new Site { go = sr.gameObject, sr = sr, spot = spot };
    }

    private void BuildSheep()
    {
        var catalog = IslandVisualCatalog.Instance;
        var meadow = IslandLayout.Meadow;
        var rect = Rect.MinMaxRect(meadow.xMin + 0.5f, IslandLayout.Height - meadow.yMax + 0.3f,
            meadow.xMax - 0.5f, IslandLayout.Height - meadow.yMin - 0.3f);
        for (int i = 0; i < IslandLayout.SheepCount && catalog != null; i++)
        {
            var feet = new Vector2(UnityEngine.Random.Range(rect.xMin, rect.xMax), UnityEngine.Random.Range(rect.yMin, rect.yMax));
            IslandSheep.Create(villageRoot, rect, feet, catalog.Frames("sheep_grass"), catalog.Frames("sheep_idle"),
                catalog.Frames("sheep_move"), 20f);
        }
    }

    // ==================== PİYONLAR (işçi dağılımı + Usta + boştakiler) ====================

    /// <summary>Piyonları kayda göre yeniden kurar: işçiler üretici binalarında, Usta kalede, kalanlar kalede dolaşır (§5).</summary>
    public void RebuildPawns()
    {
        var save = IslandSession.Save;
        if (save == null || workersRoot == null) return;
        Vector3? builderAt = builderPawn != null ? builderPawn.transform.position : (Vector3?)null;
        for (int i = workersRoot.childCount - 1; i >= 0; i--) Destroy(workersRoot.GetChild(i).gameObject);
        var catalog = IslandVisualCatalog.Instance;
        var assignment = IslandEconomy.AssignWorkers(save.placed);
        int used = 0;
        foreach (var entry in save.placed)
        {
            if (entry == null || entry.level < 1 || !assignment.TryGetValue(entry.id, out int crew)) continue;
            var kind = IslandEconomy.KindOf(entry);
            var door = IslandLayout.World(SpotFor(entry).DoorCell);
            for (int i = 0; i < crew; i++, used++)
                IslandPawn.Create(workersRoot, catalog,
                    kind == IslandLayout.BuildingKind.Lumber ? IslandPawn.Job.Lumberjack : IslandPawn.Job.Shepherd,
                    door + new Vector2((i - (crew - 1) * 0.5f) * 0.45f, 0f));
        }
        var keepDoor = IslandLayout.World(IslandLayout.Building(IslandLayout.BuildingKind.Castle).DoorCell);
        builderPawn = IslandPawn.Create(workersRoot, catalog, IslandPawn.Job.Builder, keepDoor + new Vector2(-1.4f, -0.1f));
        if (builderAt.HasValue) builderPawn.transform.position = builderAt.Value;
        int idle = Mathf.Min(3, Mathf.Max(0, IslandEconomy.PawnPool(save.placed) - used));
        for (int i = 0; i < idle; i++)
            IslandPawn.Create(workersRoot, catalog, IslandPawn.Job.Idler, keepDoor + new Vector2(1.2f + i * 0.9f, -0.1f));
        if (setup != null && setup.KeepOverride > 0 || IslandEconomy.VeinOpen(save))
        {
            // Altın Damarı (Z3, Faz E1) açıkken madenci altın taşını kazar (havuzdan düşmez); "tüm bölgeler açık" görünümünde de görünür.
            var stone = IslandLayout.GoldStones[0];
            var route = new[]
            {
                IslandLayout.World(15.5f, 9.5f), IslandLayout.World(15.5f, 7.5f), IslandLayout.World(15.5f, 6.5f),
                IslandLayout.World(13.5f, 6.5f), IslandLayout.World(11.6f, 6.6f),
            };
            IslandPawn.Create(workersRoot, catalog, IslandPawn.Job.Miner, IslandLayout.World(stone.x - 0.25f, stone.y + 1.15f), route);
        }
        UpdateTaskVisuals(true);
    }

    // ==================== YIĞINLAR ====================

    private Vector2 PileCenter(IslandPlacedBuilding entry)
    {
        var spot = SpotFor(entry);
        Vector2 feet = spot.FeetWorld;
        return new Vector2(feet.x + spot.w * 0.5f + 0.2f, feet.y + 0.2f);
    }

    private Pile EnsurePile(IslandPlacedBuilding entry)
    {
        if (piles.TryGetValue(entry.id, out var pile)) return pile;
        var catalog = IslandVisualCatalog.Instance;
        bool wood = IslandEconomy.KindOf(entry) == IslandLayout.BuildingKind.Lumber;
        var sprite = catalog != null ? (wood ? catalog.woodIcon : catalog.meatIcon) : null;
        pile = new Pile { root = new GameObject("Pile_" + entry.id).transform, icons = new SpriteRenderer[6], wood = wood };
        pile.root.SetParent(pilesRoot, false);
        Vector2[] offsets = { new Vector2(-0.34f, 0f), new Vector2(0f, 0.02f), new Vector2(0.34f, 0f), new Vector2(-0.17f, 0.3f), new Vector2(0.17f, 0.3f), new Vector2(0f, 0.58f) };
        for (int i = 0; i < 6; i++)
        {
            var go = new GameObject("Icon" + i);
            go.transform.SetParent(pile.root, false);
            go.transform.localPosition = offsets[i];
            go.transform.localScale = Vector3.one * 0.8f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            pile.icons[i] = sr;
            go.SetActive(false);
        }
        piles[entry.id] = pile;
        return pile;
    }

    /// <summary>Yığın doluluğunu (0–6 simge) kayıttaki stoka göre günceller; üretici olmayan/yok olan yığınlar kapanır.</summary>
    public void RefreshPiles(bool instant = false)
    {
        var save = IslandSession.Save;
        if (save == null) return;
        RefreshVeinPile(save, instant);
        var alive = new HashSet<string>();
        foreach (var entry in save.placed)
        {
            if (entry == null || entry.level < 1 || !IslandEconomy.IsProducer(IslandEconomy.KindOf(entry))) continue;
            alive.Add(entry.id);
            var pile = EnsurePile(entry);
            pile.center = PileCenter(entry);
            pile.root.position = new Vector3(pile.center.x, pile.center.y, 0f);
            int count = IslandEconomy.PileCount(entry);
            for (int i = 0; i < pile.icons.Length; i++)
            {
                bool on = i < count;
                var go = pile.icons[i].gameObject;
                if (go.activeSelf != on)
                {
                    go.SetActive(on);
                    if (on && !instant)
                    {
                        go.transform.localScale = Vector3.one * 0.2f;
                        go.transform.DOScale(0.8f, 0.25f).SetEase(Ease.OutBack).SetLink(go);
                    }
                }
                pile.icons[i].sortingOrder = IslandYSort.OrderFor(pile.center.y, 60) + i;
            }
            pile.shown = count;
        }
        foreach (var id in new List<string>(piles.Keys))
            if (!alive.Contains(id)) { if (piles[id].root != null) Destroy(piles[id].root.gameObject); piles.Remove(id); }
    }

    /// <summary>Altın Damarı yığını (0–6 altın simgesi): Z3 açıkken depo doluluğuna göre; dokununca Ana Üs altınına eklenir.</summary>
    private void RefreshVeinPile(IslandSaveData save, bool instant)
    {
        var catalog = IslandVisualCatalog.Instance;
        if (veinPile == null)
        {
            veinPile = new Pile { root = new GameObject("Pile_GoldVein").transform, icons = new SpriteRenderer[6] };
            veinPile.root.SetParent(pilesRoot, false);
            veinPile.center = VeinPileCenter;
            veinPile.root.position = new Vector3(VeinPileCenter.x, VeinPileCenter.y, 0f);
            Vector2[] offsets = { new Vector2(-0.34f, 0f), new Vector2(0f, 0.02f), new Vector2(0.34f, 0f), new Vector2(-0.17f, 0.3f), new Vector2(0.17f, 0.3f), new Vector2(0f, 0.58f) };
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Icon" + i);
                go.transform.SetParent(veinPile.root, false);
                go.transform.localPosition = offsets[i];
                go.transform.localScale = Vector3.one * 0.8f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = catalog != null ? catalog.Single("gold_icon") : null;
                veinPile.icons[i] = sr;
                go.SetActive(false);
            }
        }
        int count = IslandEconomy.VeinPileCount(save, keep);
        for (int i = 0; i < veinPile.icons.Length; i++)
        {
            bool on = i < count;
            var go = veinPile.icons[i].gameObject;
            if (go.activeSelf != on)
            {
                go.SetActive(on);
                if (on && !instant)
                {
                    go.transform.localScale = Vector3.one * 0.2f;
                    go.transform.DOScale(0.8f, 0.25f).SetEase(Ease.OutBack).SetLink(go);
                }
            }
            veinPile.icons[i].sortingOrder = IslandYSort.OrderFor(VeinPileCenter.y, 60) + i;
        }
        veinPile.shown = count;
    }

    public int VeinPileShown => veinPile != null ? veinPile.shown : 0;

    private void CollectVein()
    {
        int amount = IslandEconomy.CollectVein();
        if (amount <= 0) { Toast?.Invoke(L.T("Damarda henüz altın yok", "No gold in the vein yet")); return; }
        Toast?.Invoke(L.T($"+{amount} altın (Ana Üs)", $"+{amount} gold (Main Base)"));
        var catalog = IslandVisualCatalog.Instance;
        FlyIcon(catalog != null ? catalog.Single("gold_icon") : null, VeinPileCenter);
        RefreshVeinPile(IslandSession.Save, false);
    }

    /// <summary>Yığın simgesi sayısı (testler/ekran görüntüsü).</summary>
    public int PileShown(string id) => piles.TryGetValue(id, out var pile) ? pile.shown : 0;

    private void Collect(string id)
    {
        var entry = IslandEconomy.Find(id);
        if (entry == null) return;
        bool wood = IslandEconomy.KindOf(entry) == IslandLayout.BuildingKind.Lumber;
        int amount = IslandEconomy.CollectFrom(entry);
        var catalog = IslandVisualCatalog.Instance;
        if (amount <= 0)
        {
            Toast?.Invoke(entry.stock >= 1f ? L.T(wood ? "Odun deposu dolu" : "Et deposu dolu", wood ? "Wood wallet is full" : "Meat wallet is full")
                : L.T("Henüz toplanacak yok", "Nothing to collect yet"));
            return;
        }
        Toast?.Invoke(wood ? L.T($"+{amount} Odun", $"+{amount} Wood") : L.T($"+{amount} Et", $"+{amount} Meat"));
        if (piles.TryGetValue(id, out var pile)) FlyIcon(wood ? catalog?.woodIcon : catalog?.meatIcon, pile.center);
        RefreshPiles();
    }

    private static void FlyIcon(Sprite sprite, Vector2 from)
    {
        if (sprite == null) return;
        var go = new GameObject("CollectFx");
        go.transform.position = from + Vector2.up * 0.4f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = IslandFx.FxSorting;
        go.transform.DOMoveY(from.y + 1.8f, 0.7f).SetEase(Ease.OutQuad).SetLink(go);
        sr.DOFade(0f, 0.7f).SetDelay(0.2f).SetLink(go).OnComplete(() => Destroy(go));
    }

    // ==================== DOKUNUŞ ====================

    /// <summary>Üs görünümü dünya dokunuşu: yığın topla → bina/inşaat → kale → bulutlu bölge. İşlendiyse true.</summary>
    public bool HandleTap(Vector2 world)
    {
        foreach (var pair in piles)
        {
            var pile = pair.Value;
            if (pile.shown <= 0 || pile.root == null) continue;
            if (Vector2.Distance(world, pile.center + Vector2.up * 0.3f) < 0.85f) { Collect(pair.Key); return true; }
        }
        if (veinPile != null && veinPile.shown > 0 && Vector2.Distance(world, VeinPileCenter + Vector2.up * 0.3f) < 0.95f) { CollectVein(); return true; }
        if (flag != null && flag.Hit(world))
        {
            int stair = CycleFlag();
            Toast?.Invoke(L.T($"Bayrak: {IslandLayout.FlagName(stair)}", $"Flag: {IslandLayout.FlagName(stair)}"));
            return true;
        }
        string best = null;
        float bestArea = float.MaxValue;
        foreach (var pair in live)
        {
            if (pair.Value == null) continue;
            var bounds = pair.Value.Bounds;
            bounds.Expand(0.2f);
            if (!bounds.Contains(new Vector3(world.x, world.y, bounds.center.z))) continue;
            float area = bounds.size.x * bounds.size.y;
            if (area < bestArea) { bestArea = area; best = pair.Key; }
        }
        foreach (var pair in sites)
        {
            if (pair.Value.sr == null) continue;
            var bounds = pair.Value.sr.bounds;
            if (!bounds.Contains(new Vector3(world.x, world.y, bounds.center.z))) continue;
            float area = bounds.size.x * bounds.size.y;
            if (area < bestArea) { bestArea = area; best = pair.Key; }
        }
        if (best != null) { BuildingTapped?.Invoke(best); return true; }
        if (Castle != null && Castle.Bounds.Contains(new Vector3(world.x, world.y, Castle.Bounds.center.z))) { CastleTapped?.Invoke(); return true; }
        int zone = setup != null ? setup.ZoneCloudAt(world) : -1;
        if (zone > 0) { ZoneTapped?.Invoke(zone); return true; }
        return false;
    }

    public IslandBuilding Live(string id) => live.TryGetValue(id, out var b) ? b : null;

    // ==================== YERLEŞTİRME ARACI (bina ekleme / taşıma) ====================

    /// <summary>Yeni bina için inşaat alanı: Usta görevi başladı, bina sprite'ı soluk görünür.</summary>
    public void OnBuildStarted(IslandPlacedBuilding entry)
    {
        SpawnEntry(entry);
        IslandFx.Play("dust_1", SpotFor(entry).FeetWorld + Vector2.up * 0.3f, 1.6f, 14f);
        UpdateTaskVisuals(true);
    }

    /// <summary>Taşıma: aynı kimlikle yeni konuma yeniden kurar (hasar/onarım durumu korunur).</summary>
    public void OnMoved(IslandPlacedBuilding entry)
    {
        IslandRepair.SaveAll();
        if (live.TryGetValue(entry.id, out var old) && old != null) { live.Remove(entry.id); Destroy(old.gameObject); }
        SpawnEntry(entry);
        IslandFx.Play("dust_1", SpotFor(entry).FeetWorld + Vector2.up * 0.3f, 1.6f, 14f);
        RebuildPawns();
        RefreshPiles(true);
    }

    /// <summary>Taşınırken kaynak binayı yarı saydam gösterir.</summary>
    public void SetMoveGhostSource(string id, bool moving)
    {
        if (!live.TryGetValue(id, out var building) || building == null) return;
        var sr = building.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        var c = sr.color; c.a = moving ? 0.35f : 1f; sr.color = c;
    }

    // ==================== USTA GÖREVİ GÖRSELLERİ ====================

    private sealed class Marker
    {
        public GameObject root;
        public TextMeshPro text;
        public string key = "";
    }

    private Vector2 TaskSite(IslandBuilderTask task, out Vector2 top)
    {
        top = Vector2.zero;
        if (task.kind == "zone")
        {
            Vector2 center = setup != null ? setup.ZoneCenter(task.zone) : IslandLayout.CastleCenter;
            top = center + Vector2.up * 1.2f;
            return center;
        }
        var entry = IslandEconomy.Find(task.buildingId);
        if (entry == null) { top = IslandLayout.CastleCenter; return IslandLayout.CastleCenter; }
        var spot = SpotFor(entry);
        float topY = spot.FeetWorld.y + FeetPixels(spot.kind) / 64f * 2f;
        if (live.TryGetValue(entry.id, out var b) && b != null) topY = b.Bounds.max.y;
        else if (sites.TryGetValue(entry.id, out var site) && site.sr != null) topY = site.sr.bounds.max.y;
        top = new Vector2(spot.FeetWorld.x, topY + 0.55f);
        return IslandLayout.World(spot.DoorCell) + new Vector2(spot.w * 0.5f + 0.1f, -0.15f);
    }

    private void UpdateTaskVisuals(bool force = false)
    {
        var save = IslandSession.Save;
        var task = save != null ? save.task : null;
        bool show = task != null && task.Active && !(setup != null && setup.InDefense);
        if (!show)
        {
            if (marker != null && marker.root != null) marker.root.SetActive(false);
            if (builderPawn != null) builderPawn.SetSite(null);
            return;
        }
        string key = task.kind + task.buildingId + task.zone + task.targetLevel;
        Vector2 siteFeet = TaskSite(task, out Vector2 top);
        if (marker == null || marker.root == null)
        {
            var catalog = IslandVisualCatalog.Instance;
            marker = new Marker { root = new GameObject("TaskMarker") };
            var icon = marker.root.AddComponent<SpriteRenderer>();
            icon.sprite = catalog != null ? catalog.Single("icon_hammer") : null;
            icon.sortingOrder = IslandFx.FxSorting;
            marker.root.transform.localScale = Vector3.one * 0.8f;
            marker.root.transform.DOLocalRotate(new Vector3(0f, 0f, 14f), 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(marker.root);
            var textObject = new GameObject("Timer");
            textObject.transform.SetParent(marker.root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            marker.text = textObject.AddComponent<TextMeshPro>();
            marker.text.fontSize = 3.2f;
            marker.text.alignment = TextAlignmentOptions.Center;
            marker.text.fontStyle = FontStyles.Bold;
            marker.text.color = new Color(1f, 0.92f, 0.65f);
            marker.text.sortingOrder = IslandFx.FxSorting + 1;
            marker.text.outlineWidth = 0.28f;   // bulut/zemin üstünde okunur
            marker.text.outlineColor = new Color32(48, 24, 8, 255);
        }
        if (!marker.root.activeSelf) marker.root.SetActive(true);
        marker.root.transform.position = new Vector3(top.x, top.y, 0f);
        int left = Mathf.CeilToInt((float)IslandEconomy.TaskSecondsLeft);
        marker.text.text = left >= 3600 ? $"{left / 3600}:{left % 3600 / 60:00}:{left % 60:00}" : $"{left / 60}:{left % 60:00}";
        if (builderPawn != null && (force || marker.key != key)) builderPawn.SetSite(siteFeet);
        marker.key = key;
    }

    public bool MarkerVisible => marker != null && marker.root != null && marker.root.activeSelf;

    private void Update()
    {
        if (setup == null || !setup.Ready) return;
        if (Time.unscaledTime >= nextTick)
        {
            nextTick = Time.unscaledTime + 1f;
            IslandEconomy.Tick(IslandRepair.Now, true);
            RefreshPiles();
        }
        UpdateTaskVisuals();
    }

    // ==================== GÖREV TAMAMLANDI ====================

    private void OnTaskCompleted(IslandBuilderTask task)
    {
        if (setup == null || !setup.Ready && Castle == null) return;
        if (task.kind == "zone")
        {
            setup.DisperseZone(task.zone);
            Toast?.Invoke(L.T($"{ZoneName(task.zone)} açıldı!", $"{ZoneName(task.zone)} revealed!"));
            if (task.zone == Progression.Island.VeinZone) RebuildPawns();   // madenci damara gelir
        }
        else
        {
            var entry = IslandEconomy.Find(task.buildingId);
            if (entry != null)
            {
                if (sites.TryGetValue(entry.id, out var site)) { if (site.go != null) Destroy(site.go); sites.Remove(entry.id); }
                IslandRepair.SaveAll();
                if (live.TryGetValue(entry.id, out var old) && old != null) { live.Remove(entry.id); Destroy(old.gameObject); }
                SpawnEntry(entry);
                var spot = SpotFor(entry);
                IslandFx.Play("dust_2", spot.FeetWorld + Vector2.up * 0.4f, 2.2f, 14f);
                if (live.TryGetValue(entry.id, out var fresh) && fresh != null)
                    fresh.transform.DOPunchScale(new Vector3(0.08f, -0.06f, 0f), 0.35f, 6, 0.6f).SetLink(fresh.gameObject);
                string name = KindName(spot.kind);
                Toast?.Invoke(task.kind == "build" ? L.T($"{name} tamamlandı", $"{name} complete")
                    : L.T($"{name} Sv {entry.level}", $"{name} Lv {entry.level}"));
            }
            RebuildPawns();
            RefreshFlag();
        }
        RefreshPiles(true);
        UpdateTaskVisuals(true);
    }

    public static string ZoneName(int zone)
    {
        string full = IslandLayout.Zones[Mathf.Clamp(zone, 0, IslandLayout.Zones.Length - 1)].name;
        int slash = full.IndexOf('/');
        if (slash < 0) return full;
        return L.T(full.Substring(0, slash).Trim(), full.Substring(slash + 1).Trim());
    }
}
