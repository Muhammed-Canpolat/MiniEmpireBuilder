using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ada yerleştirme ev sahibi: yeni bina (Odun/Et bedeli + Ada Ustası) ya da taşıma (bedava, anında) için
/// <see cref="IPlacementHost"/> uygular. Geçerlilik <see cref="IslandGrid"/>, işleme <see cref="IslandEconomy"/>.
/// </summary>
public sealed class IslandPlacementHost : IPlacementHost
{
    public IslandLayout.BuildingKind Kind { get; private set; }
    public string MovingId { get; private set; }
    public Vector2Int StartCell { get; set; }
    public Vector2Int Footprint => IslandGrid.Size(Kind);
    public (int wood, int meat) Cost => MovingId != null ? (0, 0) : Progression.Island.PlaceCost(Kind);
    public Sprite GhostSprite
    {
        get
        {
            var catalog = IslandVisualCatalog.Instance;
            return catalog != null ? catalog.Single(IslandVillage.SpriteName(Kind)) : null;
        }
    }
    public int ShortfallGems => MovingId != null ? 0 : IslandEconomy.ShortfallGems(Cost.wood, Cost.meat);

    public static IslandPlacementHost ForNew(IslandLayout.BuildingKind kind) => new IslandPlacementHost { Kind = kind };

    public static IslandPlacementHost ForMove(string id)
    {
        var entry = IslandEconomy.Find(id);
        return new IslandPlacementHost
        {
            Kind = entry != null ? IslandEconomy.KindOf(entry) : IslandLayout.BuildingKind.House, MovingId = id,
            StartCell = entry != null ? new Vector2Int(entry.col, entry.row) : default,
        };
    }

    public bool CanPlace(int col, int row, out string reason)
    {
        var save = IslandSession.Save;
        var result = IslandGrid.Check(Kind, col, row, save != null ? save.placed : null, save != null ? save.zoneMask : 1, MovingId, IslandEconomy.Keep);
        reason = IslandGrid.ReasonText(result);
        return result == IslandGrid.Reason.None;
    }

    public bool CanAfford(out string reason)
    {
        reason = null;
        if (IslandEconomy.InDefense) { reason = L.T("Savaşta yerleştirilemez", "Cannot place during defense"); return false; }
        if (MovingId != null) return true;
        if (IslandEconomy.BuilderBusy) { reason = L.T("Ada Ustası meşgul", "Island Builder is busy"); return false; }
        if (IslandEconomy.CanSpend(Cost.wood, Cost.meat)) return true;
        var gm = GameManager.Instance;
        if (gm != null && gm.Gems >= ShortfallGems) return true;   // elmasla tamamlanabilir
        reason = L.T("Kaynak yetersiz", "Not enough resources");
        return false;
    }

    public bool Confirm(int col, int row)
    {
        var village = IslandVillage.Instance;
        if (village == null) return false;
        string reason;
        if (MovingId != null)
        {
            if (!IslandEconomy.TryMove(MovingId, col, row, out reason)) { village.Toast?.Invoke(reason); return false; }
            village.OnMoved(IslandEconomy.Find(MovingId));
            return true;
        }
        var entry = IslandEconomy.TryStartBuild(Kind, col, row, ShortfallGems > 0, out reason);
        if (entry == null) { village.Toast?.Invoke(reason); return false; }
        village.OnBuildStarted(entry);
        return true;
    }
}

/// <summary>
/// Ada yerleştirme arayüzü (Ana Üs <c>PlacementController</c> UX'inin adaya uyarlaması, island_base_v2 §3): hayalet geçerliyse yeşil,
/// değilse kırmızı; yürüyüş şeritleri ve merdiven önleri yalnız bu modda kırmızı kaplanır; YERLEŞTİR / İPTAL düğmeleri
/// (<see cref="DecorPlacementController.MakeButton"/>); hayalet <see cref="GhostPointerDrag"/> ile sürüklenir, kenarda kamera kayar.
/// Hayalet ve kaplamalar gerçek Tiny Swords sprite'larıdır (bina + zemin karosu, renk tonuyla).
/// </summary>
public sealed class IslandPlacementController : MonoBehaviour
{
    public static IslandPlacementController Instance { get; private set; }

    public bool IsPlacing => host != null;
    public IPlacementHost Host => host;
    public Vector2Int Cell => cell;
    public bool Valid { get; private set; }
    public string InvalidReason { get; private set; } = "";

    private IPlacementHost host;
    private Camera cam;
    private IslandCameraController cameraController;
    private RectTransform safe;
    private Action<bool> uiMode;
    private Action<string> toast;
    private Vector2Int cell;
    private GameObject ghost, overlayRoot, bar;
    private SpriteRenderer ghostRenderer;
    private readonly List<SpriteRenderer> footprintCells = new List<SpriteRenderer>();
    private Button confirmButton;
    private TextMeshProUGUI confirmText;
    private string lastLabel;
    private readonly GhostPointerDrag drag = new GhostPointerDrag();

    private static readonly Color Green = new Color(0.25f, 1f, 0.3f), Red = new Color(1f, 0.12f, 0.18f);
    /// <summary>Yerleştirmede yasak şerit/merdiven hücresi rengi ve boyu (kırmızımsı, yarı opak).</summary>
    /// (Doğrusal renk uzayında yarı saydam kırmızı, alttaki yeşil çimle turuncu/kahverengiye kayar: opaklık yüksek tutulur.)
    public static readonly Color LaneColor = new Color(0.9f, 0.06f, 0.14f, 0.9f);
    public const float LaneCellScale = 0.86f;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Yerleştirmeyi başlatır. <paramref name="setUiMode"/>: dock/menü gizle-göster; touch yüzeyi kapat-aç.</summary>
    public void Begin(IPlacementHost placementHost, Camera camera, IslandCameraController controller, RectTransform safeArea,
        Action<bool> setUiMode, Action<string> showToast)
    {
        if (IsPlacing) Cancel();
        host = placementHost;
        cam = camera;
        cameraController = controller;
        safe = safeArea;
        uiMode = setUiMode;
        toast = showToast;
        drag.Reset();
        drag.OverUi = IslandCameraController.IsOverUi;
        drag.EdgeScroller = controller != null ? (Action<Vector2>)controller.EdgeScroll : null;
        cell = FindStartCell();
        BuildVisuals();
        if (host.MovingId != null) IslandVillage.Instance?.SetMoveGhostSource(host.MovingId, true);
        uiMode?.Invoke(true);
        Vector2 center = CenterWorld(cell);
        cameraController?.EnsureVisible(center);
        UpdateGhost();
    }

    public void Cancel()
    {
        if (!IsPlacing) return;
        Finish();
    }

    private void Finish()
    {
        if (host != null && host.MovingId != null) IslandVillage.Instance?.SetMoveGhostSource(host.MovingId, false);
        if (ghost != null) Destroy(ghost);
        if (overlayRoot != null) Destroy(overlayRoot);
        if (bar != null) Destroy(bar);
        ghost = overlayRoot = bar = null;
        footprintCells.Clear();
        confirmButton = null; confirmText = null; lastLabel = null;
        drag.Reset();
        host = null;
        uiMode?.Invoke(false);
    }

    /// <summary>Testler: hayaleti doğrudan hücreye koyar (sol-üst hücre) ve durumu günceller.</summary>
    public void MoveGhostTo(int col, int row)
    {
        if (!IsPlacing) return;
        cell = new Vector2Int(col, row);
        UpdateGhost();
    }

    /// <summary>Testler/arayüz: onay düğmesinin yaptığı iş. Başarıda true ve mod kapanır.</summary>
    public bool Confirm()
    {
        if (!IsPlacing || !Valid) return false;
        if (!host.CanAfford(out string reason)) { toast?.Invoke(reason); return false; }
        if (!host.Confirm(cell.x, cell.y)) return false;
        Finish();
        return true;
    }

    // ==================== HÜCRE / DÜNYA ====================

    private Vector2 CenterWorld(Vector2Int c)
    {
        var size = host.Footprint;
        return IslandLayout.World(c.x + size.x * 0.5f, c.y + size.y * 0.5f);
    }

    private Vector2Int CellUnderPointer(Vector2 world)
    {
        var size = host.Footprint;
        int col = Mathf.Clamp(Mathf.RoundToInt(world.x - size.x * 0.5f), 0, IslandLayout.Width - size.x);
        int bottomWorld = Mathf.RoundToInt(world.y - size.y * 0.5f);
        int row = Mathf.Clamp(IslandLayout.Height - (bottomWorld + size.y), 0, IslandLayout.Height - size.y);
        return new Vector2Int(col, row);
    }

    /// <summary>Başlangıç hücresi: taşımada binanın yeri; yenide kamera merkezine en yakın geçerli hücre (yoksa merkez).</summary>
    private Vector2Int FindStartCell()
    {
        if (host.MovingId != null) return host.StartCell;
        Vector2 view = cam != null ? (Vector2)cam.transform.position : IslandLayout.CastleCenter;
        var center = CellUnderPointer(view);
        Vector2Int best = center;
        float bestDistance = float.MaxValue;
        for (int c = 0; c <= IslandLayout.Width - host.Footprint.x; c++)
            for (int r = 0; r <= IslandLayout.Height - host.Footprint.y; r++)
            {
                if (!host.CanPlace(c, r, out _)) continue;
                float d = Vector2.Distance(new Vector2(c, r), center);
                if (d < bestDistance) { bestDistance = d; best = new Vector2Int(c, r); }
            }
        return best;
    }

    // ==================== GÖRSELLER ====================

    private void BuildVisuals()
    {
        var catalog = IslandVisualCatalog.Instance;
        var tile = catalog != null ? catalog.Single("ui_paper") : null;   // Tiny Swords kâğıt dokusu: hücre kaplaması (renk tonuyla; yeşil/kırmızı net okunur)
        Vector3 tileScale = tile != null ? new Vector3(1f / Mathf.Max(0.01f, tile.bounds.size.x), 1f / Mathf.Max(0.01f, tile.bounds.size.y), 1f) : Vector3.one;
        overlayRoot = new GameObject("IslandPlacementOverlay");
        // Yürüyüş şeritleri + merdiven önleri: kırmızı (yalnız yerleştirme modunda)
        for (int c = 0; c < IslandLayout.Width; c++)
            for (int r = 0; r < IslandLayout.Height; r++)
            {
                var reason = IslandGrid.StaticReason(c, r);
                if (reason != IslandGrid.Reason.Lane && reason != IslandGrid.Reason.Stairs) continue;
                var go = new GameObject("Lane");
                go.transform.SetParent(overlayRoot.transform, false);
                go.transform.position = IslandLayout.World(c + 0.5f, r + 0.5f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tile;
                // Hücreden biraz küçük (aralarda çim görünür) ve daha opak kırmızı: yarı saydam kırmızı yeşil çimle karışıp kahverengi görünüyordu.
                go.transform.localScale = tileScale * LaneCellScale;
                sr.color = LaneColor;
                sr.sortingOrder = -4;
            }
        ghost = new GameObject("IslandPlacementGhost");
        ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = host.GhostSprite;
        ghostRenderer.sortingOrder = 900;
        var size = host.Footprint;
        for (int i = 0; i < size.x * size.y; i++)
        {
            var go = new GameObject("FootprintCell");
            go.transform.SetParent(overlayRoot.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = tile;
            go.transform.localScale = tileScale;
            sr.sortingOrder = -3;
            footprintCells.Add(sr);
        }
        CreateBar();
    }

    private void CreateBar()
    {
        if (safe == null) return;
        bar = new GameObject("IslandPlacementBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(safe, false);
        var rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0f);
        rect.anchorMax = new Vector2(0.92f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 22f);
        rect.sizeDelta = new Vector2(0f, 66f);
        var image = bar.GetComponent<Image>();
        image.sprite = ReferenceUiVisuals.ToastSprite();
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        confirmButton = DecorPlacementController.MakeButton(rect, L.T("YERLEŞTİR", "PLACE"), new Vector2(-70f, 0f), () => Confirm(), true, 190f);
        confirmText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
        confirmText.enableAutoSizing = true;
        confirmText.fontSizeMax = 14f;
        DecorPlacementController.MakeButton(rect, L.T("İPTAL", "CANCEL"), new Vector2(130f, 0f), Cancel, false, 96f);
    }

    // ==================== GÜNCELLEME ====================

    private void Update()
    {
        if (!IsPlacing || cam == null) return;
        var size = host.Footprint;
        if (drag.Poll(cam, world => new Rect(CenterWorld(cell).x - size.x * 0.5f - 1f, CenterWorld(cell).y - size.y * 0.5f - 1f, size.x + 2f, size.y + 2f).Contains(world),
            out Vector3 pointer))
            cell = CellUnderPointer(pointer);
        UpdateGhost();
    }

    private void UpdateGhost()
    {
        if (!IsPlacing) return;
        var size = host.Footprint;
        Valid = host.CanPlace(cell.x, cell.y, out string reason);
        InvalidReason = Valid ? "" : reason;
        Vector2 feet = IslandLayout.World(cell.x + size.x * 0.5f, cell.y + size.y);
        if (ghost != null)
        {
            ghost.transform.position = new Vector3(feet.x, feet.y + IslandVillage.FeetPixels(host.Kind) / 64f, 0f);
            ghostRenderer.color = Valid ? new Color(Green.r, Green.g, Green.b, 0.7f) : new Color(Red.r, Red.g, Red.b, 0.7f);
        }
        int index = 0;
        for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++, index++)
            {
                if (index >= footprintCells.Count) break;
                var sr = footprintCells[index];
                bool cellOk = IslandGrid.StaticReason(cell.x + dx, cell.y + dy) == IslandGrid.Reason.None;
                sr.transform.position = IslandLayout.World(cell.x + dx + 0.5f, cell.y + dy + 0.5f);
                var tint = Valid ? Green : Red;
                sr.color = new Color(tint.r, tint.g, tint.b, Valid ? 0.6f : !cellOk ? 0.8f : 0.55f);
            }
        UpdateConfirm();
    }

    private void UpdateConfirm()
    {
        if (confirmButton == null) return;
        bool affordable = host.CanAfford(out string affordReason);
        string label;
        if (!Valid) label = InvalidReason;
        else if (!affordable) label = affordReason;
        else
        {
            var cost = host.Cost;
            if (host.MovingId != null) label = L.T("YERLEŞTİR", "PLACE");
            else if (host.ShortfallGems > 0) label = L.T($"TAMAMLA · {host.ShortfallGems} elmas", $"TOP UP · {host.ShortfallGems} gems");
            else label = L.T($"YERLEŞTİR · {cost.wood} Odun" + (cost.meat > 0 ? $" {cost.meat} Et" : ""),
                $"PLACE · {cost.wood} Wood" + (cost.meat > 0 ? $" {cost.meat} Meat" : ""));
        }
        confirmButton.interactable = Valid && affordable;
        var face = confirmButton.GetComponent<Image>();
        if (face != null) face.color = Valid && affordable ? Color.white : new Color(0.62f, 0.5f, 0.5f, 0.95f);
        if (confirmText != null && label != lastLabel) { lastLabel = label; confirmText.text = label; }
    }
}
