using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// ==================== VERİ TİPLERİ ====================

public enum MapObjectType { Building, Tree, Rock, Fence, BuilderHut }

/// <summary>Haritadaki canlı bir objeyi temsil eder</summary>
public class PlacedMapObject
{
    public GameObject gameObject;
    public MapObjectType objectType;
    public BuildingType? buildingType;
    public string id;
    public bool isBeingRemoved;
    // Ağaç/kaya için görsel yükseklik (kayıtta aynı boyda geri gelsin diye).
    public float size;
}

/// <summary>
/// Sahnedeki bir binanın kayıt satırı (slot) ve seviyesi. Kayıtta varsa seviye oradan
/// okunur; henüz kaydedilmemiş yeni binada başlangıç seviyesi kullanılır.
/// </summary>
public class BuildingInstance : MonoBehaviour
{
    public string SlotId;
    public BuildingType Type;
    public int InitialLevel = 1;

    public int Level
    {
        get
        {
            var placed = GameManager.Instance?.FindPlaced(SlotId);
            return placed != null && placed.level > 0 ? placed.level : Mathf.Max(1, InitialLevel);
        }
    }

    public static int LevelOf(GameObject building, BuildingType type)
    {
        var instance = building != null ? building.GetComponentInParent<BuildingInstance>() : null;
        if (instance != null) return instance.Level;
        return GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var data) ? Mathf.Max(1, data.level) : 1;
    }
}

/// <summary>Ağaç veya kayaya eklenir — tipini işaretler</summary>
public class MapObjectTag : MonoBehaviour
{
    public MapObjectType objectType;
}

/// <summary>Binaya eklenir — tipini işaretler</summary>
public class BuildingIdentifier : MonoBehaviour
{
    public BuildingType buildingType;
}

// ==================== ANA SINIF ====================


/// <summary>
/// Üs haritasındaki tüm objeleri (bina, ağaç, kaya) yönetir.
/// Yerleştirme, kaldırma, taşıma ve save/load entegrasyonu.
/// </summary>
public class BaseMapManager : MonoBehaviour
{
    public static BaseMapManager Instance { get; private set; }

    private readonly List<PlacedMapObject> _mapObjects = new List<PlacedMapObject>();

    private const float FenceSegmentWorldLength = 4f;
    private const float FenceHeight = 1.2f;
    private const float FenceCornerWorldSize = 1.2f;

    private GameObject _buildableGroundRoot;
    private GameObject _buildableFenceRoot;
    private RectInt _buildableRect;
    public Vector2 BuildableCenter => _buildableRect.center;
    public RectInt BuildableRect => _buildableRect;


    // ==================== AĞAÇ / KAYA BÜYÜMESİ ====================
    /// <summary>Temizlenen ağaç/kayaların yerine bu kadar saatte bir yalnız tek bir tane çıkar.</summary>
    public const double ResourceGrowHours = 18d;
    /// <summary>Kendiliğinden büyüme bu sayıların altındayken olur (ilk açılışta 8 ağaç + 4 kaya konur).</summary>
    public const int GrowTreeCap = 6;
    public const int GrowRockCap = 3;
    private float _growCheckTimer;
    private bool _terrainLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (BaseSceneSetup.Instance == null) return;
        _growCheckTimer += Time.deltaTime;
        if (_growCheckTimer < 30f) return;
        _growCheckTimer = 0f;
        GrowTerrainResources();
    }

    /// <summary>
    /// Kayıtlı ağaç/kayaları yükler. Kayıtta hiç yoksa (yeni oyun ya da eski kayıt) seed ile
    /// bir kez oluşturur ve kaydeder; böylece temizlenenler savaştan dönünce geri gelmez.
    /// </summary>
    public void LoadOrSeedTerrainResources(Action seed)
    {
        var data = GameManager.Instance?.PlayerData;
        _terrainLoaded = true;
        if (data == null) { seed?.Invoke(); return; }
        if (!data.terrainResourcesSeeded)
        {
            seed?.Invoke();
            data.terrainResourcesSeeded = true;
            data.terrainLastGrowTicks = DateTime.UtcNow.Ticks;
            SaveAll();
            return;
        }
        foreach (var saved in data.terrainResources)
        {
            if (saved == null) continue;
            var position = new Vector3(saved.x, saved.y, 0f);
            if (saved.type == nameof(MapObjectType.Rock)) SpawnRock(position, saved.id, saved.size);
            else SpawnTree(position, saved.id, saved.size);
        }
        GrowTerrainResources();
    }

    /// <summary>
    /// Sayı tavanın altındaysa ve son büyümeden 18 saat geçtiyse tek bir ağaç ya da kaya çıkarır.
    /// Tavan doluyken sayaç işlemez: süre, oyuncu bir şey temizledikten sonra başlar.
    /// </summary>
    public bool GrowTerrainResources()
    {
        var data = GameManager.Instance?.PlayerData;
        if (data == null || !data.terrainResourcesSeeded || BaseSceneSetup.Instance == null) return false;
        long now = DateTime.UtcNow.Ticks;
        bool roomForTree = CountObjects(MapObjectType.Tree) < GrowTreeCap;
        bool roomForRock = CountObjects(MapObjectType.Rock) < GrowRockCap;
        if (data.terrainLastGrowTicks <= 0 || data.terrainLastGrowTicks > now || (!roomForTree && !roomForRock))
        {
            data.terrainLastGrowTicks = now;
            return false;
        }
        if (now - data.terrainLastGrowTicks < TimeSpan.FromHours(ResourceGrowHours).Ticks) return false;

        MapObjectType type = !roomForTree ? MapObjectType.Rock
            : !roomForRock ? MapObjectType.Tree
            : CountObjects(MapObjectType.Rock) * 2 < CountObjects(MapObjectType.Tree)
                ? MapObjectType.Rock : MapObjectType.Tree;
        if (!TrySpawnRandomResource(type)) return false;
        data.terrainLastGrowTicks = now;
        SaveAll();
        return true;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded += OnBuildingUpgraded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBuildingUpgraded -= OnBuildingUpgraded;
    }

    // ==================== BUILDABLE AREA ====================

    public void RefreshBuildableArea(int mapWidth, int mapHeight)
    {
        ClearBuildableVisualRoots();
        _buildableRect = BaseWorldLayout.CleanAtLevel(GameManager.Instance != null
            ? GameManager.Instance.GetMainBaseLevel() : 1);

        BuildBuildableGround();
        BuildBuildableFence();
    }

    public bool IsBuildablePosition(Vector3 worldPos)
    {
        if (_buildableRect.width <= 0 || _buildableRect.height <= 0) return true;
        Vector2Int cell = new Vector2Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y));
        return _buildableRect.Contains(cell);
    }

    public bool IsBuildableArea(Vector3 worldPos, float radius)
    {
        if (_buildableRect.width <= 0 || _buildableRect.height <= 0) return true;

        return worldPos.x - radius >= _buildableRect.xMin
            && worldPos.x + radius <= _buildableRect.xMax
            && worldPos.y - radius >= _buildableRect.yMin
            && worldPos.y + radius <= _buildableRect.yMax;
    }

    private void OnBuildingUpgraded(BuildingType type, int newLevel)
    {
        foreach (var building in _mapObjects)
        {
            if (building.buildingType != type || building.gameObject == null) continue;
            int level = BuildingInstance.LevelOf(building.gameObject, type);
            var label = building.gameObject.transform.Find("ReadableLabel/Text")?.GetComponent<TextMeshProUGUI>();
            if (type == BuildingType.MainBase && label != null) label.text = $"{level}";
            var badge = building.gameObject.transform.Find("ReadableLabel/LevelBadge/Text")
                ?.GetComponent<TextMeshProUGUI>();
            if (badge != null) badge.text = $"Sev. {level}";
        }
        if (type != BuildingType.MainBase) return;
        if (BaseSceneSetup.Instance == null) return;
        RefreshBuildableArea(BaseSceneSetup.Instance.width, BaseSceneSetup.Instance.height);
        BaseSceneSetup.Instance.ClearUnlockedLand(newLevel);
        BaseSceneSetup.Instance.RefreshCameraFraming();
    }

    private void BuildBuildableGround()
    {
        _buildableGroundRoot = new GameObject("BuildableGround");
    }

    /// <summary>
    /// Belirtilen konuma 1x1 dünya birimi büyüklüğünde bir zemin sprite'ı yerleştirir.
    /// Sprite'ın pivot veya import ayarından tamamen bağımsız çalışır.
    /// </summary>
    private void PlaceGroundTile(Sprite sprite, Vector3 worldPos, int sortingOrder, Color color, Vector2 size)
    {
        if (sprite == null) return;

        GameObject go = new GameObject($"GroundTile_{Mathf.FloorToInt(worldPos.x)}_{Mathf.FloorToInt(worldPos.y)}");
        go.transform.SetParent(_buildableGroundRoot.transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        sr.color = color;

        // Sprite boyutunu 1x1 dünya birimine ölçekle.
        float sprW = sprite.bounds.size.x > 0.001f ? sprite.bounds.size.x : 1f;
        float sprH = sprite.bounds.size.y > 0.001f ? sprite.bounds.size.y : 1f;

        go.transform.position = worldPos;
        go.transform.localScale = new Vector3(size.x / sprW, size.y / sprH, 1f);
    }

    /// <summary>
    /// Ana Üs çit halkası: kenar boyunca kesintisiz döşenmiş ahşap kirişler, yaklaşık 3 birimde
    /// bir dik taş direk, köşelerde büyük direk, seçili direklerde fener. Çarpışma hücre başına
    /// görünmez parçalarla sağlanır (kahraman geçemez). Çit kaldırılamaz: sınırda boşluk kalmaz.
    /// </summary>
    private void BuildBuildableFence()
    {
        ClearFenceObjects();
        _buildableFenceRoot = new GameObject("BuildableFence");

        const float left = 1.5f, right = 20.5f, bottom = 1.5f, top = 36.5f;
        Sprite beams = FenceBeamSprite();
        Sprite pillar = BaseCustomArt.Get("fence_stone_pillar") ?? LoadFullFenceSprite("fence_wood_corner");
        Sprite lantern = BaseCustomArt.Get("fence_lantern");

        // Kirişler (yatay kenarlar düz, dikey kenarlar 90° dönük döşeme).
        if (beams != null)
        {
            FenceBeam(beams, new Vector3((left + right) * 0.5f, bottom, 0.2f), right - left, false, 300);
            FenceBeam(beams, new Vector3((left + right) * 0.5f, top, 0.2f), right - left, false, 300);
            FenceBeam(beams, new Vector3(left, (bottom + top) * 0.5f, 0.2f), top - bottom, true, 299);
            FenceBeam(beams, new Vector3(right, (bottom + top) * 0.5f, 0.2f), top - bottom, true, 299);
        }

        // Direkler: kenarı eşit aralıklara böl (≈3 birim), köşeler daha büyük.
        if (pillar != null)
        {
            int horizontalSpans = Mathf.Max(1, Mathf.RoundToInt((right - left) / 3f));
            int verticalSpans = Mathf.Max(1, Mathf.RoundToInt((top - bottom) / 3f));
            for (int i = 0; i <= horizontalSpans; i++)
            {
                float x = Mathf.Lerp(left, right, i / (float)horizontalSpans);
                bool corner = i == 0 || i == horizontalSpans;
                FencePost(pillar, new Vector3(x, bottom, 0f), corner, lantern, i == 2 || i == horizontalSpans - 2);
                FencePost(pillar, new Vector3(x, top, 0f), corner, lantern, i == 2 || i == horizontalSpans - 2);
            }
            for (int i = 1; i < verticalSpans; i++)
            {
                float y = Mathf.Lerp(bottom, top, i / (float)verticalSpans);
                bool lamp = i == verticalSpans / 3 || i == verticalSpans * 2 / 3;
                FencePost(pillar, new Vector3(left, y, 0f), false, lantern, lamp);
                FencePost(pillar, new Vector3(right, y, 0f), false, lantern, lamp);
            }
        }

        // Görünmez çarpışma: her sınır hücresine bir parça (tanı katmanı da bunları sayar).
        for (int x = 1; x <= 20; x++)
        {
            FenceBlocker(new Vector3(x + 0.5f, bottom, 0f));
            FenceBlocker(new Vector3(x + 0.5f, top, 0f));
        }
        for (int y = 2; y <= 35; y++)
        {
            FenceBlocker(new Vector3(left, y + 0.5f, 0f));
            FenceBlocker(new Vector3(right, y + 0.5f, 0f));
        }
    }

    private static Sprite fenceBeamSprite;

    /// <summary>Meşe ray görselinden yalnız iki kiriş (metal bağlantısız bölüm): döşenebilir.</summary>
    private static Sprite FenceBeamSprite()
    {
        if (fenceBeamSprite != null) return fenceBeamSprite;
        var texture = Resources.Load<Texture2D>("CustomBaseArt/fence_oak_rail");
        if (texture == null) return null;
        texture.filterMode = FilterMode.Point;
        // Kaynak 2164x727: kirişler x 200–1000, y (yukarıdan) 250–465 → normalize.
        var rect = new Rect(texture.width * 0.0925f, texture.height * 0.36f,
            texture.width * 0.37f, texture.height * 0.296f);
        float pixelsPerUnit = rect.height / 0.46f; // iki kiriş birlikte 0.46 birim kalınlık
        fenceBeamSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit,
            0, SpriteMeshType.FullRect);
        fenceBeamSprite.name = "fence_beams";
        return fenceBeamSprite;
    }

    private void FenceBeam(Sprite sprite, Vector3 center, float length, bool vertical, int order)
    {
        var go = new GameObject(vertical ? "FenceBeamVertical" : "FenceBeamHorizontal");
        go.transform.SetParent(_buildableFenceRoot.transform, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, 0f, vertical ? 90f : 0f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(length, sprite.bounds.size.y);
        renderer.sortingOrder = order;
    }

    private void FencePost(Sprite pillar, Vector3 basePosition, bool corner, Sprite lantern, bool lamp)
    {
        Vector2 size = corner ? new Vector2(0.62f, 1.5f) : new Vector2(0.46f, 1.15f);
        var post = new GameObject(corner ? "FenceCorner" : "StoneFencePost");
        post.transform.SetParent(_buildableFenceRoot.transform, false);
        // Direğin tabanı çit çizgisinin biraz altında: kirişler direğe oturuyormuş gibi.
        post.transform.position = new Vector3(basePosition.x, basePosition.y - 0.22f + size.y * 0.5f, 0.15f);
        var renderer = post.AddComponent<SpriteRenderer>();
        renderer.sprite = pillar;
        renderer.sortingOrder = 301;
        post.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, pillar.bounds.size.x),
            size.y / Mathf.Max(0.01f, pillar.bounds.size.y), 1f);
        if (!lamp || lantern == null) return;
        Vector3 top = new Vector3(basePosition.x, basePosition.y - 0.22f + size.y + 0.12f, 0f);
        AddFenceAdornment(post.transform, BaseMapManager.MakeCircleSprite(Color.white), "FenceLanternGlow",
            top + new Vector3(0f, 0.06f, 0f), new Vector2(0.7f, 0.7f), 302, new Color(1f, 0.59f, 0.15f, 0.20f));
        AddFenceAdornment(post.transform, lantern, "FenceLantern", top, new Vector2(0.26f, 0.40f), 303);
    }

    private void FenceBlocker(Vector3 position)
    {
        var go = new GameObject("FenceSegment");
        go.transform.SetParent(_buildableFenceRoot.transform, false);
        go.transform.position = position;
        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 0.7f);
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
    }

    private static Sprite LoadFullFenceSprite(string name)
    {
        var texture = Resources.Load<Texture2D>("Sprites/" + name);
        if (texture == null) return null;
        Rect region = new Rect(0, 0, texture.width, texture.height);
        return Sprite.Create(texture, region,
            new Vector2(0.5f, 0.5f), 100f);
    }

    private void CreateFencePiece(Sprite sprite, Vector3 worldPos, float zRotation, bool isCorner, string id,
        float segmentLength = FenceSegmentWorldLength, bool flipX = false)
    {
        if (IsFenceRemoved(id))
            return;

        GameObject go = new GameObject(isCorner ? "FenceCorner" : "FenceSegment");
        go.transform.SetParent(_buildableFenceRoot.transform, false);
        go.transform.position = worldPos;
        bool authoredPillar = sprite.name == "fence_stone_pillar";
        bool authoredRail = sprite.name == "fence_oak_rail";
        go.transform.rotation = Quaternion.Euler(0f, 0f, authoredPillar ? 0f : zRotation);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.flipX = flipX;
        sr.sortingOrder = 300;

        go.transform.localScale = GetFenceScale(sprite, isCorner, segmentLength);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        float worldScale = go.transform.localScale.x;
        col.size = new Vector2((isCorner ? 0.8f : segmentLength - 0.05f) / worldScale,
            (isCorner ? 0.8f : authoredRail ? 0.70f : 0.25f)
                / go.transform.localScale.y);
        col.isTrigger = false;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        MapObjectTag tag = go.AddComponent<MapObjectTag>();
        tag.objectType = MapObjectType.Fence;

        _mapObjects.Add(new PlacedMapObject
        {
            gameObject = go,
            objectType = MapObjectType.Fence,
            id = id
        });

        if (!authoredRail && !authoredPillar) return;
        Sprite pillar = BaseCustomArt.Get("fence_stone_pillar");
        Sprite lantern = BaseCustomArt.Get("fence_lantern");
        int coordinate = zRotation == 90f ? Mathf.FloorToInt(worldPos.y) :
            Mathf.FloorToInt(worldPos.x);
        if (authoredRail && pillar != null && (coordinate - 2) % 6 == 0)
            AddFenceAdornment(go.transform, pillar, "StoneFencePost",
                worldPos + new Vector3(0f, 0.25f, 0f), new Vector2(0.48f, 1.22f), 301);
        bool horizontal = zRotation == 0f;
        bool lampHere = authoredRail &&
            (horizontal ? coordinate == 5 || coordinate == 16
                : coordinate == 11 || coordinate == 26);
        if (lampHere && lantern != null)
        {
            AddFenceAdornment(go.transform, BaseMapManager.MakeCircleSprite(Color.white),
                "FenceLanternGlow", worldPos + new Vector3(0f, 0.94f, 0f),
                new Vector2(0.6f, 0.6f), 302,
                new Color(1f, 0.59f, 0.15f, 0.20f));
            AddFenceAdornment(go.transform, lantern, "FenceLantern",
                worldPos + new Vector3(0f, 0.89f, 0f), new Vector2(0.24f, 0.38f), 303);
        }
    }

    private static void AddFenceAdornment(Transform parent, Sprite sprite, string name,
        Vector3 worldPosition, Vector2 worldSize, int sortingOrder,
        Color? tint = null)
    {
        var ornament = new GameObject(name);
        ornament.transform.SetParent(parent, false);
        ornament.transform.position = worldPosition;
        ornament.transform.rotation = Quaternion.identity;
        var renderer = ornament.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint ?? Color.white;
        renderer.sortingOrder = sortingOrder;
        ornament.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.01f, sprite.bounds.size.x * parent.lossyScale.x),
            worldSize.y / Mathf.Max(0.01f, sprite.bounds.size.y * parent.lossyScale.y), 1f);
    }

    private string GetFenceId(string side, int x, int y)
    {
        var fixedArea = BaseWorldLayout.Buildable;
        return $"fence_{fixedArea.xMin}_{fixedArea.yMin}_{fixedArea.width}_{fixedArea.height}_{side}_{x}_{y}";
    }

    private void MigrateRemovedFenceIds()
    {
        var data = GameManager.Instance?.PlayerData;
        if (data?.removedFenceIds == null || data.removedFenceIds.Count == 0) return;
        var mapped = new HashSet<string>();
        bool changed = false;
        foreach (string id in data.removedFenceIds)
        {
            string[] parts = id.Split('_');
            if (parts.Length != 8 || parts[0] != "fence"
                || !int.TryParse(parts[3], out int oldWidth)
                || !int.TryParse(parts[4], out int oldHeight)
                || !int.TryParse(parts[6], out int oldX)
                || !int.TryParse(parts[7], out int oldY)
                || oldWidth <= 0 || oldHeight <= 0)
            {
                mapped.Add(id);
                continue;
            }
            int newX = Mathf.RoundToInt((float)oldX / oldWidth * BaseWorldLayout.Buildable.width);
            int newY = Mathf.RoundToInt((float)oldY / oldHeight * BaseWorldLayout.Buildable.height);
            string replacement = GetFenceId(parts[5], newX, newY);
            mapped.Add(replacement);
            if (replacement != id) changed = true;
        }
        if (changed)
        {
            data.removedFenceIds = new List<string>(mapped);
            GameManager.Instance.SaveGame();
        }
    }

    private bool IsFenceRemoved(string id)
    {
        return GameManager.Instance?.PlayerData?.removedFenceIds != null
            && GameManager.Instance.PlayerData.removedFenceIds.Contains(id);
    }

    private Vector3 GetFenceScale(Sprite sprite, bool isCorner, float segmentLength)
    {
        if (sprite == null) return Vector3.one;
        Vector2 size = sprite.bounds.size;
        if (size.x <= 0.0001f || size.y <= 0.0001f) return Vector3.one;
        if (sprite.name == "fence_stone_pillar")
            return new Vector3(0.86f / size.x, 1.62f / size.y, 1f);
        if (sprite.name == "fence_oak_rail")
            return new Vector3(segmentLength / size.x, FenceHeight / size.y, 1f);
        return isCorner
            ? new Vector3(FenceCornerWorldSize / size.x, FenceHeight / size.y, 1f)
            : new Vector3(segmentLength / size.x, FenceHeight / size.y, 1f);
    }

    private void ClearFenceObjects()
    {
        for (int i = _mapObjects.Count - 1; i >= 0; i--)
        {
            if (_mapObjects[i].objectType != MapObjectType.Fence) continue;
            if (_mapObjects[i].gameObject != null)
                Destroy(_mapObjects[i].gameObject);
            _mapObjects.RemoveAt(i);
        }
    }

    private void ClearBuildableVisualRoots()
    {
        ClearFenceObjects();

        // Alan köklerini doğrudan field'lardan yok et (FindObjectsOfType'tan daha güvenilir)
        if (_buildableGroundRoot != null)
        {
            Destroy(_buildableGroundRoot);
            _buildableGroundRoot = null;
        }
        if (_buildableFenceRoot != null)
        {
            Destroy(_buildableFenceRoot);
            _buildableFenceRoot = null;
        }

        // Güvenlik: aynı isimli artık kalmış objeleri de temizle
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject go in allObjects)
        {
            if (go == null) continue;
            if (go.name == "BuildableGround" || go.name == "BuildableFence")
                Destroy(go);
        }
    }

    // ==================== SPAWN ====================

    /// <summary>Haritaya bina spawn et (kayıt sisteminden veya placement'tan)</summary>
    public PlacedMapObject SpawnBuilding(BuildingType type, Vector3 pos, string id = null)
        => SpawnBuildingInternal(type, pos, id, false, 0);

    private PlacedMapObject SpawnSavedBuilding(BuildingType type, Vector3 pos, string id, int level)
        => SpawnBuildingInternal(type, pos, id, true, level);

    private PlacedMapObject SpawnBuildingInternal(BuildingType type, Vector3 pos, string id,
        bool savedPosition, int savedLevel)
    {
        BaseWorldLayout.Slot slot;
        if (string.IsNullOrEmpty(id) || !BaseWorldLayout.TryGet(id, out slot)
            || slot.buildingType != type || IsSlotOccupied(id))
        {
            if (!TryGetAvailableSlot(type, pos, null, out slot)) return null;
        }
        id = slot.id;
        if (!savedPosition) pos = slot.center;
        else if (type != BuildingType.MainBase && !CanPlaceBuildingAt(type, pos))
            pos = NearestValidBuildingPosition(type, pos);

        GameObject go = new GameObject($"Building_{type}_{id}");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        // Yeni kule/manastır 1. seviyeden başlar; Ana Üs ve maden tek kopya: türün seviyesi.
        int typeLevel = GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var typeData) ? Mathf.Max(1, typeData.level) : 1;
        var instance = go.AddComponent<BuildingInstance>();
        instance.SlotId = id;
        instance.Type = type;
        instance.InitialLevel = savedLevel > 0 ? savedLevel
            : type == BuildingType.MainBase || type == BuildingType.GoldMine ? typeLevel : 1;
        int instanceLevel = instance.Level;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite s = type == BuildingType.GoldMine
            ? MakeSquareSprite(Color.white) : SpriteManager.Instance?.GetBuildingSprite(type);

        if (s == null)
        {
            if (type == BuildingType.GoldMine)
            {
                s = MakeSquareSprite(GetBuildingColor(type));

                // Altın madeni olduğunu belli etmek için büyük sarı bir ikon ekle
                GameObject iconGo = new GameObject("MineIcon");
                iconGo.transform.SetParent(go.transform, false);
                SpriteRenderer isr = iconGo.AddComponent<SpriteRenderer>();
                isr.sprite = SpriteManager.Instance?.IconGold ?? MakeCircleSprite(Color.yellow);
                isr.sortingOrder = 101 - Mathf.RoundToInt(pos.y * 2f);
                iconGo.transform.localScale = Vector3.one * 1.5f;
            }
            else
            {
                s = MakeSquareSprite(GetBuildingColor(type));
            }
        }

        if (type != BuildingType.GoldMine && GameManager.Instance?.Buildings != null &&
            GameManager.Instance.Buildings.TryGetValue(type, out var placedBuilding))
        {
            var levelSprite = placedBuilding.VisualForLevel(instanceLevel)?.sprite;
            if (levelSprite != null) s = levelSprite;
        }

        sr.sprite = s;
        sr.sortingOrder = 100 - Mathf.RoundToInt(pos.y * 2f);

        float visualMultiplier = 1f;
        Vector3 targetScale = CalculateBuildingVisualScale(type, sr.sprite) * visualMultiplier;

        if (type == BuildingType.GoldMine)
        {
            go.transform.localScale = targetScale;
            sr.color = Color.clear;
        }
        else if (type == BuildingType.MainBase)
        {
            go.transform.localScale = targetScale;
        }
        else if (type == BuildingType.Monastery)
        {
            go.transform.localScale = targetScale;
        }
        else
        {
            go.transform.localScale = targetScale;
        }

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        // Sprite boyutuna göre collider ayarla (Geniş binalara tıklamayı kolaylaştırır)
        Vector2 sSize = sr.sprite != null ? new Vector2(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) : new Vector2(1.5f, 1.5f);
        if (sSize.x < 1f || sSize.y < 1f) sSize = new Vector2(Mathf.Max(1.5f, sSize.x), Mathf.Max(1.5f, sSize.y));
        col.size = sSize / visualMultiplier;

        BuildingIdentifier bid = go.AddComponent<BuildingIdentifier>();

        bid.buildingType = type;

        var levelVisual = go.AddComponent<BaseBuildingVisual>();
        levelVisual.Initialize(type, sr, targetScale);
        BaseGarrisonVisual garrisonVisual = null;
        if (type == BuildingType.MainBase || type == BuildingType.ArcherTower ||
            type == BuildingType.CannonTower || type == BuildingType.Monastery)
        {
            var template = Resources.Load<BaseGarrisonVisual>("Garrison_" + type);
            garrisonVisual = template != null ? Instantiate(template, go.transform)
                : go.AddComponent<BaseGarrisonVisual>();
            garrisonVisual.transform.localPosition = Vector3.zero;
        }
        if (type == BuildingType.GoldMine)
        {
            int mineLevel = instanceLevel;
            var template = Resources.Load<BaseMineVisual>("BaseMineVisual");
            var mineVisual = template != null ? Instantiate(template, go.transform)
                : go.AddComponent<BaseMineVisual>();
            mineVisual.transform.localPosition = Vector3.zero;
            mineVisual.Initialize(mineLevel, sr.sortingOrder);
        }

        // Spawn animasyonu
        go.transform.localScale = Vector3.zero;
        go.transform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                if (garrisonVisual != null) garrisonVisual.Initialize(type, sr.sortingOrder);
            });
        CreateBuildingShadow(go.transform, slot.footprint, targetScale, sr.sortingOrder - 1);

        // Dünya etiketi
        CreateReadableLabel(go.transform, type == BuildingType.MainBase
            ? $"{GameManager.Instance?.GetMainBaseLevel() ?? 1}"
            : GetBuildingDisplayName(type), type == BuildingType.MainBase,
            sr.sprite.bounds.extents.y * targetScale.y + 0.34f, targetScale.x,
            type == BuildingType.MainBase ? null : $"Sev. {instanceLevel}");
        // Etiketler dokununca kısa süre görünür; Ana Üs seviyesi üst bardaki armada gösterilir.
        go.transform.Find("ReadableLabel")?.gameObject.SetActive(false);

        var obj = new PlacedMapObject
        {
            gameObject = go,
            objectType = MapObjectType.Building,
            buildingType = type,
            id = id,
            isBeingRemoved = false
        };

        _mapObjects.Add(obj);

        BaseGroundPaths.Instance?.Refresh();

        if (type == BuildingType.MainBase && BaseSceneSetup.Instance != null)
            RefreshBuildableArea(BaseSceneSetup.Instance.width, BaseSceneSetup.Instance.height);

        return obj;
    }

    private Vector3 CalculateBuildingVisualScale(BuildingType type, Sprite sprite)
    {
        float spriteWidth = sprite != null ? sprite.bounds.size.x : 0f;
        if (spriteWidth <= 0.0001f) return Vector3.one;
        float footprintWidth = BaseWorldLayout.Footprint(type).x;
        float width = Mathf.Min(footprintWidth * 1.15f, footprintWidth + 0.6f);
        float scale = width / spriteWidth;
        return new Vector3(scale, scale, 1f);
    }

    public static void CreateBuildingShadow(Transform parent, Vector2 footprint, Vector3 parentScale, int sortingOrder)
    {
        var template = Resources.Load<GameObject>("BaseGroundShadow");
        if (template == null) return;
        var shadow = Instantiate(template, parent);
        shadow.name = "ZeminGolgesi";
        shadow.transform.localPosition = new Vector3(0f, -0.25f / parentScale.y, 0f);
        var renderer = shadow.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;
        renderer.color = new Color(0f, 0f, 0f, 0.30f);
        renderer.sortingOrder = sortingOrder;
        shadow.transform.localScale = new Vector3(footprint.x * 1.25f /
            (renderer.sprite.bounds.size.x * parentScale.x), footprint.y * 1.25f /
            (renderer.sprite.bounds.size.y * parentScale.y), 1f);
    }

    private Vector3 CalculateMaxSizeScale(Sprite sprite, float maxWorldSize, float fallbackScale)
    {
        if (sprite == null)
            return new Vector3(fallbackScale, fallbackScale, 1f);

        Vector2 size = sprite.bounds.size;
        float maxSize = Mathf.Max(size.x, size.y);
        if (maxSize <= 0.0001f)
            return new Vector3(fallbackScale, fallbackScale, 1f);

        float scale = Mathf.Min(fallbackScale, maxWorldSize / maxSize);
        return new Vector3(scale, scale, 1f);
    }

    /// <summary>Haritaya ağaç spawn et</summary>
    public PlacedMapObject SpawnTree(Vector3 pos, string id = null, float size = 0f)
    {
        id ??= Guid.NewGuid().ToString("N").Substring(0, 8);
        if (size <= 0f) size = UnityEngine.Random.Range(1.5f, 2.1f);

        GameObject go = new GameObject($"Tree_{id}");
        go.transform.position = new Vector3(pos.x, pos.y, 0.3f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetRandomTreeSprite() ?? SpriteManager.Instance?.Tree ?? MakeCircleSprite(new Color(0.15f, 0.5f, 0.15f));
        sr.sortingOrder = 3;
        float treeScale = size / Mathf.Max(0.01f, sr.sprite.bounds.size.y);
        go.transform.localScale = new Vector3(treeScale, treeScale, 1f);
        // Rüzgârda sallanma (Tiny Swords Tree 1); her ağaç farklı karede başlar.
        var treeControllers = BaseVisualCatalog.Instance?.treeControllers;
        if (treeControllers != null && treeControllers.Length > 0 && treeControllers[0] != null)
        {
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = treeControllers[0];
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            animator.Play("Tree 1_Idle", 0, UnityEngine.Random.value);
        }

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
        col.size = new Vector2(0.9f, 0.9f) / treeScale;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        MapObjectTag tag = go.AddComponent<MapObjectTag>();
        tag.objectType = MapObjectType.Tree;

        var obj = new PlacedMapObject
        {
            gameObject = go,
            objectType = MapObjectType.Tree,
            id = id,
            isBeingRemoved = false,
            size = size
        };

        _mapObjects.Add(obj);
        return obj;
    }

    /// <summary>Haritaya kaya spawn et</summary>
    public PlacedMapObject SpawnRock(Vector3 pos, string id = null, float size = 0f)
    {
        id ??= Guid.NewGuid().ToString("N").Substring(0, 8);
        if (size <= 0f) size = UnityEngine.Random.Range(0.9f, 1.35f);

        GameObject go = new GameObject($"Rock_{id}");
        go.transform.position = new Vector3(pos.x, pos.y, 0.4f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteManager.Instance?.Rock ?? MakeCircleSprite(new Color(0.52f, 0.48f, 0.42f));
        sr.sortingOrder = 4;
        float rockScale = size / Mathf.Max(0.01f, sr.sprite.bounds.size.y);
        go.transform.localScale = new Vector3(rockScale, rockScale, 1f);

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
        col.size = new Vector2(0.9f, 0.7f) / rockScale;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        MapObjectTag tag = go.AddComponent<MapObjectTag>();
        tag.objectType = MapObjectType.Rock;

        var obj = new PlacedMapObject
        {
            gameObject = go,
            objectType = MapObjectType.Rock,
            id = id,
            isBeingRemoved = false,
            size = size
        };

        _mapObjects.Add(obj);
        return obj;
    }

    private Sprite GetRandomTreeSprite()
    {
        var fullTree = Array.Find(DecorPlacementController.All,
            item => item.id == "tree")?.sprite;
        if (fullTree != null) return fullTree;
        Sprite[] trees = new Sprite[]
        {
            SpriteManager.Instance?.GetSprite("tree1"),
            SpriteManager.Instance?.GetSprite("tree2"),
            SpriteManager.Instance?.GetSprite("tree3"),
            SpriteManager.Instance?.GetSprite("tree4")
        };

        List<Sprite> available = new List<Sprite>();
        foreach (var t in trees)
        {
            if (t != null)
                available.Add(t);
        }

        if (available.Count == 0)
            return null;

        return available[UnityEngine.Random.Range(0, available.Count)];
    }

    // ==================== KALDIRMA ====================

    /// <summary>
    /// Ağaç veya kayayı kaldır.
    /// İnşaatçı meşgulse reddeder; değilse 5 saniyelik progress bar + animasyon başlatır.
    /// </summary>
    public void RequestRemoveObject(PlacedMapObject obj)
    {
        if (obj == null || obj.isBeingRemoved) return;

        if (BuilderSystem.Instance != null && !BuilderSystem.Instance.HasAvailableBuilder())
        {
            SpawnFloatingText("İnşaatçı meşgul!", obj.gameObject.transform.position, new Color(1f, 0.4f, 0.2f));
            return;
        }

        obj.isBeingRemoved = true;
        const float duration = 5f;

        // İnşaatçıya görev at — callback: nesneyi yok et + altın ver
        bool started = BuilderSystem.Instance == null ||
            BuilderSystem.Instance.TryStartTask("Kaldırma", duration, obj.gameObject.transform.position,
                () => StartCoroutine(FinishRemoval(obj)));


        if (!started)
        {
            obj.isBeingRemoved = false;
            SpawnFloatingText("İnşaatçı meşgul!", obj.gameObject.transform.position, new Color(1f, 0.4f, 0.2f));
            return;
        }

        StartCoroutine(ShowProgressBar(obj, duration));
    }

    private IEnumerator ShowProgressBar(PlacedMapObject obj, float duration)
    {
        if (obj.gameObject == null) yield break;

        // World-space canvas — objeye bağlı
        GameObject barRoot = new GameObject("RemovalBar");
        barRoot.transform.SetParent(obj.gameObject.transform, false);
        barRoot.transform.localPosition = new Vector3(0f, 1.1f, -0.1f);

        Canvas canvas = barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;

        RectTransform crt = barRoot.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(120f, 14f);
        crt.localScale = Vector3.one * 0.011f;

        // Arka plan
        GameObject bgGO = new GameObject("BG");
        bgGO.transform.SetParent(barRoot.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        // Dolum
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(barRoot.transform, false);
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.92f, 0.35f, 0.15f);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.pivot = new Vector2(0f, 0.5f);
        fillRT.anchorMin = new Vector2(0.01f, 0.1f);
        fillRT.anchorMax = new Vector2(0.01f, 0.9f);
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

        float elapsed = 0f;

        while (elapsed < duration && obj.gameObject != null && obj.isBeingRemoved)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fillRT.anchorMax = new Vector2(t, 0.9f);
            yield return null;
        }

        if (barRoot != null) Destroy(barRoot);
    }

    private IEnumerator FinishRemoval(PlacedMapObject obj)
    {
        if (obj.gameObject == null) yield break;

        Vector3 pos = obj.gameObject.transform.position;
        bool removedFence = obj.objectType == MapObjectType.Fence;
        string removedFenceId = obj.id;

        // Coin uçuş efekti
        SpawnCoinEffect(pos);

        // Scale → 0 animasyonu
        obj.gameObject.transform.DOKill();
        yield return obj.gameObject.transform
            .DOScale(0f, 0.4f)
            .SetEase(Ease.InBack)
            .WaitForCompletion();

        if (obj.gameObject != null) Destroy(obj.gameObject);
        _mapObjects.Remove(obj);
        BaseGroundPaths.Instance?.Refresh();

        if (removedFence)
            MarkFenceRemoved(removedFenceId);

        // Altın kazan
        GameManager.Instance?.AddGold(10);
        GoldPopupManager.ShowGoldChange(10);

        SaveAll();
    }

    // ==================== TAŞIMA ====================

    /// <summary>Mevcut objeyi yeni pozisyona taşı</summary>
    private void MarkFenceRemoved(string fenceId)
    {
        if (string.IsNullOrEmpty(fenceId) || GameManager.Instance?.PlayerData == null)
            return;

        if (GameManager.Instance.PlayerData.removedFenceIds == null)
            GameManager.Instance.PlayerData.removedFenceIds = new List<string>();

        if (!GameManager.Instance.PlayerData.removedFenceIds.Contains(fenceId))
            GameManager.Instance.PlayerData.removedFenceIds.Add(fenceId);
    }

    public bool CanPlaceBuildingAt(BuildingType type, Vector2 center, PlacedMapObject moving = null)
    {
        if (type == BuildingType.MainBase) return false;
        return CanPlaceFootprintAt(BaseWorldLayout.Footprint(type), center, moving);
    }

    /// <summary>Haritadaki bir nesnenin kapladığı alan (bina türüne göre; kulübe 2×2).</summary>
    public static Vector2Int FootprintOf(PlacedMapObject obj) =>
        obj != null && obj.objectType == MapObjectType.Building && obj.buildingType.HasValue
            ? BaseWorldLayout.Footprint(obj.buildingType.Value)
            : BuilderHut.FootprintSize;

    public bool CanPlaceObjectAt(PlacedMapObject obj, Vector2 center)
    {
        if (obj == null) return false;
        if (obj.objectType == MapObjectType.Building)
            return obj.buildingType.HasValue && CanPlaceBuildingAt(obj.buildingType.Value, center, obj);
        return obj.objectType == MapObjectType.BuilderHut && CanPlaceFootprintAt(FootprintOf(obj), center, obj);
    }

    // ==================== İNŞAATÇI KULÜBELERİ ====================

    public PlacedMapObject RegisterBuilderHut(GameObject hut, int index)
    {
        var existing = _mapObjects.Find(o => o.gameObject == hut);
        if (existing != null) return existing;
        var placed = new PlacedMapObject
        {
            gameObject = hut,
            objectType = MapObjectType.BuilderHut,
            id = "builder_hut_" + index
        };
        _mapObjects.Add(placed);
        return placed;
    }

    /// <summary>Verilen boyutta, <paramref name="near"/>'a en yakın boş yer (yoksa near).</summary>
    public Vector3 NearestFreeSpot(Vector2Int size, Vector3 near, PlacedMapObject moving = null)
    {
        Vector3 best = near;
        float bestDistance = float.MaxValue;
        var area = _buildableRect.width > 0 ? _buildableRect : BaseWorldLayout.Buildable;
        for (int y = area.yMin; y <= area.yMax - size.y; y++)
        for (int x = area.xMin; x <= area.xMax - size.x; x++)
        {
            var candidate = new Vector3(x + size.x * 0.5f, y + size.y * 0.5f, near.z);
            float distance = (candidate - near).sqrMagnitude;
            if (distance >= bestDistance || !CanPlaceFootprintAt(size, candidate, moving)) continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }

    public bool CanPlaceFootprintAt(Vector2 size, Vector2 center, PlacedMapObject moving = null)
    {
        int left = Mathf.RoundToInt(center.x - size.x * 0.5f);
        int bottom = Mathf.RoundToInt(center.y - size.y * 0.5f);
        var footprint = new RectInt(left, bottom, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
        if (
            (footprint.xMin < BaseWorldLayout.Buildable.xMin + 1 ||
             footprint.xMax > BaseWorldLayout.Buildable.xMax - 1 ||
             footprint.yMin < BaseWorldLayout.Buildable.yMin + 1 ||
             footprint.yMax > BaseWorldLayout.Buildable.yMax - 1)) return false;
        for (int y = footprint.yMin; y < footprint.yMax; y++)
        for (int x = footprint.xMin; x < footprint.xMax; x++)
        {
            if (!_buildableRect.Contains(new Vector2Int(x, y))) return false;
            var camp = GameObject.Find("HeroCamp");
            if (camp != null && new Rect(camp.transform.position.x - 1f,
                camp.transform.position.y - 1f, 2f, 2f)
                .Contains(new Vector2(x + 0.5f, y + 0.5f))) return false;
            foreach (var other in _mapObjects)
            {
                if (other == moving || other.gameObject == null || other.isBeingRemoved) continue;
                bool building = other.objectType == MapObjectType.Building && other.buildingType.HasValue;
                if (!building && other.objectType != MapObjectType.BuilderHut) continue;
                Vector2 otherSize = FootprintOf(other);
                var otherRect = new Rect(other.gameObject.transform.position.x - otherSize.x * 0.5f,
                    other.gameObject.transform.position.y - otherSize.y * 0.5f, otherSize.x, otherSize.y);
                if (otherRect.Contains(new Vector2(x + 0.5f, y + 0.5f))) return false;
            }
            var decor = DecorPlacementController.Instance;
            if (decor != null)
                foreach (var item in decor.Placed)
                    if (item != null && item.Occupies(new Vector2Int(x, y))) return false;
        }
        return true;
    }

    private Vector3 NearestValidBuildingPosition(BuildingType type, Vector3 saved)
    {
        Vector2 footprint = BaseWorldLayout.Footprint(type);
        Vector3 best = saved;
        float bestDistance = float.MaxValue;
        for (int y = BaseWorldLayout.Buildable.yMin + 1;
             y <= BaseWorldLayout.Buildable.yMax - 1 - footprint.y; y++)
        for (int x = BaseWorldLayout.Buildable.xMin + 1;
             x <= BaseWorldLayout.Buildable.xMax - 1 - footprint.x; x++)
        {
            var candidate = new Vector3(x + footprint.x * 0.5f,
                y + footprint.y * 0.5f, saved.z);
            float distance = (candidate - saved).sqrMagnitude;
            if (distance >= bestDistance || !CanPlaceBuildingAt(type, candidate)) continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }

    public bool MoveObjectTo(PlacedMapObject obj, Vector3 newPos)
    {
        if (obj?.gameObject == null || !CanPlaceObjectAt(obj, newPos)) return false;

        Vector3 target = new Vector3(newPos.x, newPos.y, obj.gameObject.transform.position.z);
        obj.gameObject.transform.DOKill();
        obj.gameObject.transform.position = target;
        obj.gameObject.transform.DOPunchScale(Vector3.one * 0.08f, 0.2f);
        BaseGroundPaths.Instance?.Refresh();
        var hut = obj.gameObject.GetComponent<BuilderHut>();
        if (hut != null) hut.OnMoved();
        SaveAll();
        HeroMovementController.Instance?.RefreshWalkable();
        return true;
    }

    // ==================== KAYIT / YÜKLEME ====================

    /// <summary>Tüm binaları PlayerData.placedBuildings'e yaz ve kaydet</summary>
    public void SaveAll()
    {
        if (GameManager.Instance?.PlayerData == null) return;

        var list = new List<PlacedBuildingData>();
        foreach (var obj in _mapObjects)
        {
            if (obj.gameObject == null || obj.isBeingRemoved) continue;
            if (obj.objectType != MapObjectType.Building) continue;

            list.Add(new PlacedBuildingData
            {
                slotId = obj.id,
                buildingType = obj.buildingType?.ToString() ?? "",
                hasPosition = true,
                x = obj.gameObject.transform.position.x,
                y = obj.gameObject.transform.position.y,
                level = obj.gameObject.GetComponent<BuildingInstance>()?.Level ?? 1
            });
        }

        GameManager.Instance.PlayerData.placedBuildings = list;

        // Ağaç/kayalar sahneye yüklendikten sonra yazılır; bina yüklemesi sırasındaki SaveAll
        // henüz sahnede olmayan ağaçları silmesin.
        if (_terrainLoaded && GameManager.Instance.PlayerData.terrainResourcesSeeded)
        {
            var resources = new List<TerrainResourceSave>();
            foreach (var obj in _mapObjects)
            {
                if (obj.gameObject == null || obj.isBeingRemoved) continue;
                if (obj.objectType != MapObjectType.Tree && obj.objectType != MapObjectType.Rock) continue;
                Vector3 p = obj.gameObject.transform.position;
                resources.Add(new TerrainResourceSave
                {
                    id = obj.id, type = obj.objectType.ToString(), x = p.x, y = p.y, size = obj.size
                });
            }
            GameManager.Instance.PlayerData.terrainResources = resources;
        }
        GameManager.Instance.SaveGame();
    }

    /// <summary>PlayerData.placedBuildings'den kayıtlı binaları sahneye yükle</summary>
    public void LoadFromPlayerData()
    {
        if (GameManager.Instance?.PlayerData?.placedBuildings == null) return;
        var saved = GameManager.Instance.PlayerData.placedBuildings;
        foreach (var data in saved)
        {
            if (Enum.TryParse<BuildingType>(data.buildingType, out BuildingType type))
                SpawnSavedBuilding(type, data.hasPosition ? new Vector2(data.x, data.y)
                    : BaseWorldLayout.TryGet(data.slotId, out var slot)
                        ? slot.center : Vector2.zero, data.slotId, data.level);
        }
        SaveAll();
    }

    /// <summary>Apply the recommended positions only after the player confirms in the edit menu.</summary>
    public void ApplyRecommendedLayout()
    {
        foreach (var item in _mapObjects)
        {
            if (item.gameObject == null || item.objectType != MapObjectType.Building ||
                !BaseWorldLayout.TryGet(item.id, out var slot)) continue;
            item.gameObject.transform.position = new Vector3(slot.center.x, slot.center.y,
                item.gameObject.transform.position.z);
        }
        var camp = GameObject.Find("HeroCamp");
        if (camp != null)
        {
            var slot = BaseWorldLayout.Get("camp");
            camp.transform.position = new Vector3(slot.center.x, slot.center.y, camp.transform.position.z);
        }
        // İlk kulübe önerilen yerine, diğerleri ona en yakın boş yere.
        var huts = new List<BuilderHut>(BuilderHut.All);
        huts.Sort((a, b) => a.Index.CompareTo(b.Index));
        foreach (var hut in huts)
        {
            var placed = GetMapObject(hut.gameObject);
            Vector3 home = BaseWorldLayout.Get("builder").center;
            Vector3 spot = hut.Index == 0 && CanPlaceFootprintAt(BuilderHut.FootprintSize, home, placed)
                ? home : NearestFreeSpot(BuilderHut.FootprintSize, home, placed);
            hut.transform.position = new Vector3(spot.x, spot.y, hut.transform.position.z);
            hut.OnMoved();
        }
        BaseGroundPaths.Instance?.Refresh();
        HeroMovementController.Instance?.RefreshWalkable();
        DecorPlacementController.Instance?.AddRecommendedClusters();
        BaseGroundPaths.Instance?.Refresh();
        SaveAll();
    }

    public bool IsSlotOccupied(string slotId)
        => _mapObjects.Any(o => o.objectType == MapObjectType.Building && o.id == slotId
            && !o.isBeingRemoved && o.gameObject != null);

    public bool TryGetAvailableSlot(BuildingType type, Vector2 pointer, string movingSlotId,
        out BaseWorldLayout.Slot selected)
    {
        selected = default;
        float best = float.PositiveInfinity;
        foreach (var slot in BaseWorldLayout.ForBuilding(type))
        {
            if (slot.id != movingSlotId && IsSlotOccupied(slot.id)) continue;
            if (!SlotInsideClean(slot)) continue;
            float distance = Vector2.SqrMagnitude(slot.center - pointer);
            if (distance >= best) continue;
            best = distance;
            selected = slot;
        }
        return !string.IsNullOrEmpty(selected.id);
    }

    /// <summary>Slotun tüm hücreleri açılmış arazide mi (kilitli araziye bina kurulmaz).</summary>
    private bool SlotInsideClean(BaseWorldLayout.Slot slot)
    {
        if (_buildableRect.width <= 0 || _buildableRect.height <= 0) return true;
        Vector2 min = slot.center - (Vector2)slot.footprint * 0.5f;
        return min.x >= _buildableRect.xMin - 0.01f && min.y >= _buildableRect.yMin - 0.01f
            && min.x + slot.footprint.x <= _buildableRect.xMax + 0.01f
            && min.y + slot.footprint.y <= _buildableRect.yMax + 0.01f;
    }

    // ==================== SORGULAR ====================

    public PlacedMapObject GetMapObjectBySlot(string slotId)
        => _mapObjects.Find(o => o.objectType == MapObjectType.Building && o.id == slotId
            && o.gameObject != null && !o.isBeingRemoved);

    public PlacedMapObject GetMapObject(GameObject go)
        => _mapObjects.Find(o => o.gameObject == go);

    public int GetPlacedCount(BuildingType type)
        => _mapObjects.Count(o => o.buildingType == type && !o.isBeingRemoved);

    public bool IsBuildingPlaced(BuildingType type)
        => _mapObjects.Any(o => o.buildingType == type && !o.isBeingRemoved);

    public void ShowBuildingLabel(PlacedMapObject building)
    {
        if (building?.gameObject == null || building.buildingType == BuildingType.MainBase) return;
        var label = building.gameObject.transform.Find("ReadableLabel");
        if (label == null) return;
        var badge = label.Find("LevelBadge/Text")?.GetComponent<TextMeshProUGUI>();
        if (badge != null && building.buildingType.HasValue)
            badge.text = $"Sev. {BuildingInstance.LevelOf(building.gameObject, building.buildingType.Value)}";
        label.gameObject.SetActive(true);
        StartCoroutine(HideBuildingLabelAfterDelay(label.gameObject));
    }

    private IEnumerator HideBuildingLabelAfterDelay(GameObject label)
    {
        yield return new WaitForSeconds(2f);
        if (label != null) label.SetActive(false);
    }

    private int CountObjects(MapObjectType type)
        => _mapObjects.Count(o => o.objectType == type && !o.isBeingRemoved);

    public bool CanSpawnResourceAt(Vector2 position)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        if (!BaseWorldLayout.Buildable.Contains(cell)) return false;
        foreach (var hit in Physics2D.OverlapCircleAll(position, 0.8f))
        {
            // Building sprite colliders can extend far beyond their footprint.
            // The precise footprint check below handles them instead.
            if (hit.GetComponentInParent<BuildingIdentifier>() != null) continue;
            return false;
        }
        foreach (var footprint in BaseWorldLayout.GetOccupiedFootprints())
        {
            var padded = new Rect(footprint.xMin - 0.8f, footprint.yMin - 0.8f,
                footprint.width + 1.6f, footprint.height + 1.6f);
            if (padded.Contains(position)) return false;
        }
        return true;
    }

    private bool TrySpawnRandomResource(MapObjectType type)
    {
        if (BaseSceneSetup.Instance == null)
            return false;

        float width = BaseSceneSetup.Instance.width;
        float height = BaseSceneSetup.Instance.height;

        for (int i = 0; i < 40; i++)
        {
            float rx = UnityEngine.Random.Range(2.7f, width - 2.7f);
            float ry = UnityEngine.Random.Range(2.7f, height - 2.7f);
            Vector3 pos = new Vector3(rx, ry, 0f);
            if (!CanSpawnResourceAt(pos)) continue;

            if (type == MapObjectType.Tree)
                SpawnTree(pos);
            else if (type == MapObjectType.Rock)
                SpawnRock(pos);

            return true;
        }
        return false;
    }

    // ==================== EFEKTLER ====================

    private void SpawnCoinEffect(Vector3 worldPos)
    {
        GameObject coin = new GameObject("CoinFX");
        coin.transform.position = worldPos;
        coin.transform.localScale = Vector3.one * 0.18f;

        SpriteRenderer sr = coin.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteManager.Instance?.IconGold ?? MakeCircleSprite(new Color(1f, 0.85f, 0f));
        sr.sortingOrder = 60;

        coin.transform.DOMoveY(worldPos.y + 1.8f, 0.9f).SetEase(Ease.OutCubic);
        sr.DOFade(0f, 0.9f).SetDelay(0.25f).OnComplete(() => { if (coin) Destroy(coin); });
    }

    private void SpawnFloatingText(string text, Vector3 worldPos, Color color)
    {
        GameObject go = new GameObject("FloatText");
        go.transform.position = worldPos + Vector3.up * 0.6f;

        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 80;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 40f);
        rt.localScale = Vector3.one * 0.013f;

        GameObject tgo = new GameObject("T");
        tgo.transform.SetParent(go.transform, false);
        RectTransform trt = tgo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        go.transform.DOMoveY(worldPos.y + 1.8f, 1.1f).SetEase(Ease.OutCubic);
        tmp.DOFade(0f, 1.1f).SetDelay(0.3f).OnComplete(() => { if (go) Destroy(go); });
    }

    // ==================== DÜNYA ETİKETİ ====================

    public static void CreateReadableLabel(Transform parent, string caption, bool gold,
        float worldOffsetBelowCenter, float parentScale, string levelText = null)
    {
        var root = new GameObject("ReadableLabel");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, -worldOffsetBelowCenter / Mathf.Max(0.01f, parentScale), -0.1f);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(gold ? 36f : 180f, 33f);
        rect.localScale = Vector3.one * (0.017f / Mathf.Max(0.01f, parentScale));
        var bg = new GameObject("Background");
        bg.transform.SetParent(root.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var image = bg.AddComponent<Image>();
        image.sprite = gold ? ReferenceUiVisuals.SolidPillSprite() : ReferenceUiVisuals.PillSprite();
        image.type = Image.Type.Sliced;
        image.color = gold ? ReferenceUiVisuals.Gold : Color.white;
        image.raycastTarget = false;
        var words = new GameObject("Text");
        words.transform.SetParent(root.transform, false);
        var textRect = words.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = gold ? Vector2.one : new Vector2(0.75f, 1f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = words.AddComponent<TextMeshProUGUI>();
        text.text = caption;
        text.fontSize = gold ? 24f : 30f;
        text.enableWordWrapping = false;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        ReferenceUiVisuals.ApplyFont(text, gold ? 24f : 28f,
            gold ? ReferenceUiVisuals.Background : ReferenceUiVisuals.Light, bodyBold: true);
        text.raycastTarget = false;
        if (!gold)
        {
            var badge = new GameObject("LevelBadge");
            badge.transform.SetParent(root.transform, false);
            var badgeRect = badge.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.77f, 0.05f);
            badgeRect.anchorMax = new Vector2(0.99f, 0.95f);
            badgeRect.offsetMin = badgeRect.offsetMax = Vector2.zero;
            var badgeImage = badge.AddComponent<Image>();
            badgeImage.sprite = ReferenceUiVisuals.SolidPillSprite();
            badgeImage.type = Image.Type.Sliced;
            badgeImage.color = ReferenceUiVisuals.Gold;
            badgeImage.raycastTarget = false;
            var badgeText = new GameObject("Text");
            badgeText.transform.SetParent(badge.transform, false);
            var badgeTextRect = badgeText.AddComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.offsetMin = badgeTextRect.offsetMax = Vector2.zero;
            var badgeTmp = badgeText.AddComponent<TextMeshProUGUI>();
            badgeTmp.text = levelText ?? "Sev. 1";
            badgeTmp.fontSize = 16f;
            badgeTmp.enableAutoSizing = true;
            badgeTmp.fontSizeMin = 9f;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            ReferenceUiVisuals.ApplyFont(badgeTmp, 16f, ReferenceUiVisuals.Background, bodyBold: true);
            badgeTmp.raycastTarget = false;
        }
    }

    private void CreateMineTitleLabel(Transform parent, Vector3 parentTargetScale)
    {
        float parentScale = Mathf.Max(0.1f, parentTargetScale.x);
        GameObject labelObj = new GameObject("MineTitleLabel");
        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = new Vector3(0f, 0.95f / parentScale, -0.08f);

        Canvas canvas = labelObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 18;

        RectTransform rootRt = labelObj.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(150f, 48f);
        rootRt.localScale = Vector3.one * (0.009f / parentScale);

        GameObject bgGo = new GameObject("BG");
        bgGo.transform.SetParent(labelObj.transform, false);
        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.08f, 0.18f);
        bgRt.anchorMax = new Vector2(0.92f, 0.88f);
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        Image bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.035f, 0.02f, 0.72f);
        bg.raycastTarget = false;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(labelObj.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "Maden";
        text.fontSize = 28f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.82f, 0.18f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 28f;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
    }

    private void CreateWorldLabel(Transform parent, string line1, string line2, Color color)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = new Vector3(0f, -0.72f, 0f);

        Canvas c = labelObj.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 10;

        RectTransform crt = labelObj.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(160f, 50f);
        crt.localScale = Vector3.one * 0.008f;

        // Arka plan
        GameObject bgGO = new GameObject("BG");
        bgGO.transform.SetParent(labelObj.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(-0.05f, -0.1f);
        bgRT.anchorMax = new Vector2(1.05f, 1.1f);
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        bgImg.raycastTarget = false;

        // Satır 1
        CreateLabelLine(labelObj.transform, "L1", line1, color,
            new Vector2(0f, 0.5f), Vector2.one, 22f, FontStyles.Bold);

        // Satır 2
        CreateLabelLine(labelObj.transform, "L2", line2, new Color(0.85f, 0.85f, 0.9f),
            Vector2.zero, new Vector2(1f, 0.5f), 16f, FontStyles.Normal);
    }

    private void CreateLabelLine(Transform parent, string name, string text, Color color,
        Vector2 ancMin, Vector2 ancMax, float size, FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = size;
    }

    // ==================== İSİM / RENK YARDIMCILARI ====================

    public static string GetBuildingDisplayName(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.MainBase: return "Ana Üs";
            case BuildingType.GoldMine: return "Maden";
            case BuildingType.ArcherTower: return "Okçu";
            case BuildingType.CannonTower: return "Topçu";
            case BuildingType.Monastery: return "Manastır";
            default: return type.ToString();
        }
    }

    private static string GetBuildingLevelText(BuildingType type)
    {
        if (GameManager.Instance?.Buildings == null) return "";
        if (!GameManager.Instance.Buildings.ContainsKey(type)) return "";
        var bd = GameManager.Instance.Buildings[type];
        return bd.isUnlocked ? $"Sev. {bd.level}" : "Kilitli";
    }

    public static Color GetBuildingColor(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.MainBase: return new Color(1f, 0.9f, 0.3f);
            case BuildingType.GoldMine: return new Color(1f, 0.85f, 0.2f);
            case BuildingType.ArcherTower: return new Color(0.2f, 0.65f, 1f);
            case BuildingType.CannonTower: return new Color(1f, 0.42f, 0.12f);
            case BuildingType.Monastery: return new Color(0.75f, 0.64f, 0.43f);
            default: return Color.white;
        }
    }

    // ==================== SPRITE YARDIMCILARI ====================

    public static Sprite MakeSquareSprite(Color color)
    {
        Texture2D tex = new Texture2D(32, 32);
        Color[] cols = new Color[32 * 32];
        for (int i = 0; i < cols.Length; i++) cols[i] = color;
        tex.SetPixels(cols); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    public static Sprite MakeCircleSprite(Color color)
    {
        const int s = 32;
        float r = s / 2f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.ARGB32, false);
        Color[] cols = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - r, dy = y - r;
                cols[y * s + x] = (dx * dx + dy * dy <= r * r) ? color : Color.clear;
            }
        tex.SetPixels(cols); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }
}
