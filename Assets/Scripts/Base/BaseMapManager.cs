using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// ==================== VERİ TİPLERİ ====================

public enum MapObjectType { Building, Tree, Rock }

/// <summary>Haritadaki canlı bir objeyi temsil eder</summary>
public class PlacedMapObject
{
    public GameObject gameObject;
    public MapObjectType objectType;
    public BuildingType? buildingType;
    public string id;
    public bool isBeingRemoved;
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

    [Header("Respawn")]
    [SerializeField] private int minTreeCount = 8;
    [SerializeField] private int minRockCount = 4;
    [SerializeField] private float respawnIntervalMin = 10f;
    [SerializeField] private float respawnIntervalMax = 18f;

    private float _respawnTimer;
    private float _respawnTargetTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _respawnTargetTime = UnityEngine.Random.Range(respawnIntervalMin, respawnIntervalMax);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        _respawnTimer += Time.deltaTime;
        if (_respawnTimer < _respawnTargetTime)
            return;

        _respawnTimer = 0f;
        _respawnTargetTime = UnityEngine.Random.Range(respawnIntervalMin, respawnIntervalMax);

        TryRespawn(MapObjectType.Tree, minTreeCount);
        TryRespawn(MapObjectType.Rock, minRockCount);
    }

    // ==================== SPAWN ====================

    /// <summary>Haritaya bina spawn et (kayıt sisteminden veya placement'tan)</summary>
    public PlacedMapObject SpawnBuilding(BuildingType type, Vector3 pos, string id = null)
    {
        id ??= Guid.NewGuid().ToString("N").Substring(0, 8);

        GameObject go = new GameObject($"Building_{type}_{id}");
        go.transform.position = new Vector3(pos.x, pos.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite s = SpriteManager.Instance?.GetBuildingSprite(type);

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
                isr.sortingOrder = 6;
                iconGo.transform.localScale = Vector3.one * 1.5f;
            }
            else
            {
                s = MakeSquareSprite(GetBuildingColor(type));
            }
        }

        sr.sprite = s;
        sr.sortingOrder = 5;

        if (type == BuildingType.GoldMine)
        {
            go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            CreateGoldMineDecor(go.transform);
        }
        else if (type == BuildingType.MainBase)
        {
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        }
        else if (type == BuildingType.WallBuilder)
        {
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        }

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = false;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        // Sprite boyutuna göre collider ayarla (Geniş binalara tıklamayı kolaylaştırır)
        Vector2 sSize = sr.sprite != null ? new Vector2(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) : new Vector2(1.5f, 1.5f);
        if (sSize.x < 1f || sSize.y < 1f) sSize = new Vector2(Mathf.Max(1.5f, sSize.x), Mathf.Max(1.5f, sSize.y));
        col.size = sSize;

        BuildingIdentifier bid = go.AddComponent<BuildingIdentifier>();

        bid.buildingType = type;

        // Spawn animasyonu
        go.transform.localScale = Vector3.zero;
        go.transform.DOScale(new Vector3(0.9f, 0.9f, 1f), 0.3f).SetEase(Ease.OutBack);

        // Dünya etiketi
        CreateWorldLabel(go.transform,
            GetBuildingDisplayName(type),
            GetBuildingLevelText(type),
            GetBuildingColor(type));

        var obj = new PlacedMapObject
        {
            gameObject = go,
            objectType = MapObjectType.Building,
            buildingType = type,
            id = id,
            isBeingRemoved = false
        };

        _mapObjects.Add(obj);
        return obj;
    }

    /// <summary>Haritaya ağaç spawn et</summary>
    public PlacedMapObject SpawnTree(Vector3 pos, string id = null)
    {
        id ??= Guid.NewGuid().ToString("N").Substring(0, 8);

        float scale = UnityEngine.Random.Range(0.40f, 0.58f);

        GameObject go = new GameObject($"Tree_{id}");
        go.transform.position = new Vector3(pos.x, pos.y, 0.3f);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetRandomTreeSprite() ?? SpriteManager.Instance?.Tree ?? MakeCircleSprite(new Color(0.15f, 0.5f, 0.15f));
        sr.sortingOrder = 3;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.5f, 1.5f);

        MapObjectTag tag = go.AddComponent<MapObjectTag>();
        tag.objectType = MapObjectType.Tree;

        var obj = new PlacedMapObject
        {
            gameObject = go,
            objectType = MapObjectType.Tree,
            id = id,
            isBeingRemoved = false
        };

        _mapObjects.Add(obj);
        return obj;
    }

    /// <summary>Haritaya kaya spawn et</summary>
    public PlacedMapObject SpawnRock(Vector3 pos, string id = null)
    {
        id ??= Guid.NewGuid().ToString("N").Substring(0, 8);

        float scale = UnityEngine.Random.Range(0.38f, 0.55f);

        GameObject go = new GameObject($"Rock_{id}");
        go.transform.position = new Vector3(pos.x, pos.y, 0.4f);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteManager.Instance?.Rock ?? MakeCircleSprite(new Color(0.52f, 0.48f, 0.42f));
        sr.sortingOrder = 4;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.2f, 0.9f);

        MapObjectTag tag = go.AddComponent<MapObjectTag>();
        tag.objectType = MapObjectType.Rock;

        var obj = new PlacedMapObject
        {
            gameObject = go,
            objectType = MapObjectType.Rock,
            id = id,
            isBeingRemoved = false
        };

        _mapObjects.Add(obj);
        return obj;
    }

    private Sprite GetRandomTreeSprite()
    {
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

    private void CreateGoldMineDecor(Transform parent)
    {
        if (parent == null) return;

        // --- Kazıcı İşçi (pawn_miner) ---
        Sprite minerSprite = SpriteManager.Instance?.GetSprite("pawn_miner");
        if (minerSprite != null)
        {
            GameObject miner = new GameObject("MineWorker");
            miner.transform.SetParent(parent, false);
            Vector3 minerBasePos = new Vector3(-0.62f, -0.28f, -0.05f);
            miner.transform.localPosition = minerBasePos;
            SpriteRenderer msr = miner.AddComponent<SpriteRenderer>();
            msr.sprite = minerSprite;
            msr.sortingOrder = 7;
            miner.transform.localScale = Vector3.one * 0.55f;

            // Kazma animasyonu: yukarı-aşağı + yatay eğilme
            // Yukarı-aşağı hareketi (kazma vuruşu hissi)
            miner.transform.DOLocalMoveY(minerBasePos.y + 0.12f, 0.28f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutQuad);
            // Hafif yatay sallanma (vurma sırasında eğilme)
            miner.transform.DOLocalRotate(new Vector3(0f, 0f, -18f), 0.28f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        // --- Altın Yığını Kümeleri (gold_stone_6) ---
        Sprite goldPile = SpriteManager.Instance?.GetSprite("gold_stone_6");
        if (goldPile != null)
        {
            // Ana büyük yığın
            CreateGoldPile(parent, goldPile, new Vector3(0.52f, -0.22f, -0.06f), 0.60f, 0);
            // Küçük yan yığın
            CreateGoldPile(parent, goldPile, new Vector3(0.75f, -0.30f, -0.06f), 0.38f, 1);
            // Diğer küçük yığın
            CreateGoldPile(parent, goldPile, new Vector3(0.35f, -0.35f, -0.06f), 0.32f, 2);
        }
    }

    private void CreateGoldPile(Transform parent, Sprite sprite, Vector3 localPos, float scale, int animOffset)
    {
        GameObject pile = new GameObject("GoldPile");
        pile.transform.SetParent(parent, false);
        pile.transform.localPosition = localPos;
        SpriteRenderer psr = pile.AddComponent<SpriteRenderer>();
        psr.sprite = sprite;
        psr.sortingOrder = 6;
        pile.transform.localScale = Vector3.one * scale;

        // Nefes alan büyüme/küçülme animasyonu (her yığın farklı hızda)
        float duration = 0.9f + animOffset * 0.35f;
        pile.transform.DOScale(scale * 1.08f, duration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
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

        // Altın kazan
        GameManager.Instance?.AddGold(10);
        GoldPopupManager.ShowGoldChange(10);

        SaveAll();
    }

    // ==================== TAŞIMA ====================

    /// <summary>Mevcut objeyi yeni pozisyona taşı</summary>
    public void MoveObjectTo(PlacedMapObject obj, Vector3 newPos)
    {
        if (obj?.gameObject == null) return;

        Vector3 target = new Vector3(newPos.x, newPos.y, obj.gameObject.transform.position.z);
        obj.gameObject.transform.DOKill();
        obj.gameObject.transform.DOMove(target, 0.25f).SetEase(Ease.OutBack);

        SaveAll();
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
                id = obj.id,
                buildingType = obj.buildingType?.ToString() ?? "",
                x = obj.gameObject.transform.position.x,
                y = obj.gameObject.transform.position.y
            });
        }

        GameManager.Instance.PlayerData.placedBuildings = list;
        GameManager.Instance.SaveGame();
    }

    /// <summary>PlayerData.placedBuildings'den kayıtlı binaları sahneye yükle</summary>
    public void LoadFromPlayerData()
    {
        if (GameManager.Instance?.PlayerData?.placedBuildings == null) return;

        foreach (var data in GameManager.Instance.PlayerData.placedBuildings)
        {
            if (Enum.TryParse<BuildingType>(data.buildingType, out BuildingType type))
                SpawnBuilding(type, new Vector3(data.x, data.y, 0f), data.id);
        }
    }

    // ==================== SORGULAR ====================

    public PlacedMapObject GetMapObject(GameObject go)
        => _mapObjects.Find(o => o.gameObject == go);

    public int GetPlacedCount(BuildingType type)
        => _mapObjects.Count(o => o.buildingType == type && !o.isBeingRemoved);

    public bool IsBuildingPlaced(BuildingType type)
        => _mapObjects.Any(o => o.buildingType == type && !o.isBeingRemoved);

    private int CountObjects(MapObjectType type)
        => _mapObjects.Count(o => o.objectType == type && !o.isBeingRemoved);

    private void TryRespawn(MapObjectType type, int minCount)
    {
        if (BaseSceneSetup.Instance == null)
            return;

        int current = CountObjects(type);
        if (current >= minCount)
            return;

        float width = BaseSceneSetup.Instance.width;
        float height = BaseSceneSetup.Instance.height;

        for (int i = 0; i < 12; i++)
        {
            float rx = UnityEngine.Random.Range(1f, width - 1f);
            float ry = UnityEngine.Random.Range(1f, height - 1f);
            Vector3 pos = new Vector3(rx, ry, 0f);

            Collider2D hit = Physics2D.OverlapCircle(pos, 0.7f);
            if (hit != null)
                continue;

            if (type == MapObjectType.Tree)
                SpawnTree(pos);
            else if (type == MapObjectType.Rock)
                SpawnRock(pos);

            return;
        }
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
            case BuildingType.WallBuilder: return "Duvarcı";
            default: return type.ToString();
        }
    }

    private static string GetBuildingLevelText(BuildingType type)
    {
        if (GameManager.Instance?.Buildings == null) return "";
        if (!GameManager.Instance.Buildings.ContainsKey(type)) return "";
        var bd = GameManager.Instance.Buildings[type];
        return bd.isUnlocked ? $"Lv.{bd.level}" : "Kilitli";
    }

    public static Color GetBuildingColor(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.MainBase: return new Color(1f, 0.9f, 0.3f);
            case BuildingType.GoldMine: return new Color(1f, 0.85f, 0.2f);
            case BuildingType.ArcherTower: return new Color(0.2f, 0.65f, 1f);
            case BuildingType.CannonTower: return new Color(1f, 0.42f, 0.12f);
            case BuildingType.WallBuilder: return new Color(0.65f, 0.48f, 0.22f);
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
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
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
