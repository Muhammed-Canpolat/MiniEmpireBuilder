using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6. oturum (4.1): Ana Üs arazi genişlemesi şeffaf ve elle. Genişleme kademeleri (t = 1..4) Ana Üs 4/7/10/13'te AÇILABİLİR olur ama
/// kendiliğinden açılmaz: kilitli araziye dokununca o kademenin tüm alanı vurgulanır (<see cref="AreaHighlight"/>), kartta koşul,
/// alanın büyüklüğü (+N inşa hücresi) ve içindeki kaynaklar (ağaç/kaya/çalı) yazar; koşul sağlanınca "AÇ" → bulut dağılma animasyonu → alan açılır.
/// Durum: <see cref="PlayerSaveData.landOpened"/> (kaç kademe açıldı; -1 = eski kayıt → Ana Üs kademesi kadar açık sayılır).
/// Ada bölgeleri aynı vurgu ve kart bilgisini kullanır (IslandBuildUi bölge paneli).
/// </summary>
public sealed class LandExpansion : MonoBehaviour
{
    public static LandExpansion Current { get; private set; }

    private int tier;
    private AreaHighlight highlight;
    private FancyInfoPanel.View view;

    public int Tier => tier;
    public int CellCount => highlight != null ? highlight.CellCount : 0;
    public string BodyText => view?.body != null ? view.body.text : "";
    public Button OpenButton => view?.primary;

    // ==================== kurallar (tek kaynak) ====================

    /// <summary>Açık kademe: kayıt (-1 ise Ana Üs kademesi; eski kayıt kıpırdamaz), Ana Üs kademesini aşamaz.</summary>
    public static int OpenedTier
    {
        get
        {
            var gm = GameManager.Instance;
            int mbTier = gm != null ? Progression.ColorTier(gm.GetMainBaseLevel()) : 0;
            var data = gm?.PlayerData;
            if (data == null) return mbTier;
            if (data.landOpened < 0) data.landOpened = mbTier;   // göç: eski kayıtta açılmış arazi açık kalır
            return Mathf.Clamp(data.landOpened, 0, 4);
        }
    }

    /// <summary>Kademe <paramref name="t"/> için gereken Ana Üs seviyesi (1 → 4, 2 → 7, 3 → 10, 4 → 13).</summary>
    public static int RequiredMainBase(int t) => t * Progression.LevelsPerColorTier + 1;

    /// <summary>Hücrenin ait olduğu genişleme kademesi (zaten açık bölge 0; sınır dışı -1).</summary>
    public static int TierOfCell(Vector2Int cell)
    {
        if (!BaseWorldLayout.Buildable.Contains(cell)) return -1;
        for (int t = 0; t <= 4; t++) if (BaseWorldLayout.CleanForTier(t).Contains(cell)) return t;
        return -1;
    }

    /// <summary>Kademe <paramref name="t"/>'nin açtığı hücreler (bir önceki kademeye göre fark).</summary>
    public static List<Vector2Int> RegionCells(int t)
    {
        var cells = new List<Vector2Int>();
        var now = BaseWorldLayout.CleanForTier(t);
        var before = BaseWorldLayout.CleanForTier(t - 1);
        for (int y = now.yMin; y < now.yMax; y++)
            for (int x = now.xMin; x < now.xMax; x++)
            {
                var c = new Vector2Int(x, y);
                if (!before.Contains(c)) cells.Add(c);
            }
        return cells;
    }

    public static (int trees, int rocks, int bushes) Resources(List<Vector2Int> cells)
    {
        int trees = 0, rocks = 0, bushes = 0;
        foreach (var c in cells)
            if (NatureObstacle.ByCell.TryGetValue(c, out var n) && n != null)
            {
                if (n.kind == NatureKind.Tree || n.kind == NatureKind.Stump) trees++;
                else if (n.kind == NatureKind.Rock) rocks++;
                else bushes++;
            }
        return (trees, rocks, bushes);
    }

    /// <summary>Açılabilir mi: sıradaki kademe ve Ana Üs koşulu sağlanmış.</summary>
    public static string BlockedReason(int t)
    {
        int opened = OpenedTier;
        if (t <= opened) return L.T("Bu alan zaten açık", "Already open");
        if (t > opened + 1) return L.T("Önce yakındaki alanı aç", "Open the nearer area first");
        int need = RequiredMainBase(t);
        int mb = GameManager.Instance != null ? GameManager.Instance.GetMainBaseLevel() : 1;
        return mb < need ? L.T($"Ana Üs Sv.{need} gerekir", $"Requires Main Base Lv.{need}") : null;
    }

    /// <summary>Kaydı ilerletir (animasyonsuz; testler ve AÇ düğmesi).</summary>
    public static bool TryOpen(int t)
    {
        if (BlockedReason(t) != null) return false;
        GameManager.Instance.PlayerData.landOpened = t;
        GameManager.Instance.SaveGame();
        return true;
    }

    // ==================== kart ====================

    /// <summary>Kilitli araziye dokunuş: o hücrenin kademesini gösterir. Kalıcı sınırda (kademe yok) false.</summary>
    public static bool ShowFor(Vector2 world)
    {
        var cell = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        int t = TierOfCell(cell);
        if (t <= 0 || t <= OpenedTier) return false;
        Close();
        var go = new GameObject("LandExpansionCard", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        ReferenceUiVisuals.ConfigureCanvas(go.GetComponent<CanvasScaler>());
        var le = go.AddComponent<LandExpansion>();
        Current = le;
        le.tier = t;
        le.Build(ReferenceUiVisuals.SafeArea(canvas.transform));
        SelectionManager.Select(le, () => { if (le != null) Destroy(le.gameObject); });   // yalnız bu kartı kapatır
        return true;
    }

    public static void Close()
    {
        if (Current != null) Destroy(Current.gameObject);
        Current = null;
    }

    private void Build(RectTransform safe)
    {
        var cells = RegionCells(tier);
        highlight = AreaHighlight.Show(cells, c => new Rect(c.x, c.y, 1f, 1f), 45, "LandHighlight");
        view = FancyInfoPanel.Build(safe, "LandCard", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.34f));
        view.title.text = L.T("YENİ ARAZİ", "NEW LAND");
        view.icon.sprite = CampaignSceneSetup.Art("ui_icon_map") ?? CampaignSceneSetup.Art("ui_icon_hammer");
        view.icon.enabled = view.icon.sprite != null;
        int need = RequiredMainBase(tier);
        int mb = GameManager.Instance != null ? GameManager.Instance.GetMainBaseLevel() : 1;
        var res = Resources(cells);
        view.level.text = L.T($"Genişleme {tier} / 4", $"Expansion {tier} / 4");
        string cond = "";
        view.body.text =
            L.T("Koşul: ", "Requires: ") + cond + L.T($"Ana Üs Sv.{need}", $"Main Base Lv.{need}")
            + (mb >= need ? L.T(" <color=#2E7D32>(karşılandı)</color>", " <color=#2E7D32>(met)</color>") : "") + "\n" +
            L.T($"Alan: +{cells.Count} inşa hücresi", $"Area: +{cells.Count} build cells") + "\n" +
            L.T($"Kaynak: {res.trees} ağaç, {res.rocks} kaya, {res.bushes} çalı", $"Resources: {res.trees} trees, {res.rocks} rocks, {res.bushes} bushes") + "\n" +
            L.T("Açılınca bina ve dekor kurabilirsin; ağaç ve kayalar kaldırılınca ödül verir.", "Once open you can build here; clearing trees and rocks gives rewards.");
        view.body.fontSizeMax = 15f;
        view.body.rectTransform.offsetMax = new Vector2(-18f, -70f);   // seviye satırının altından başlar (bina penceresiyle aynı)
        string blocked = BlockedReason(tier);
        view.note.text = blocked ?? "";
        FancyInfoPanel.SetCost(view, null, 0, true, null, 0, true, null, false);
        view.timeIcon.gameObject.SetActive(false);
        FancyInfoPanel.SetButtons(view, blocked == null ? L.T("AÇ", "OPEN") : L.T("KİLİTLİ", "LOCKED"), blocked == null, null, false, null, false);
        view.primary.onClick.AddListener(OpenWithAnimation);
        view.close.onClick.AddListener(Close);
        var prt = view.primary.GetComponent<RectTransform>();
        prt.anchorMax = new Vector2(0.96f, 0f);
    }

    private void OpenWithAnimation()
    {
        int t = tier;
        if (!TryOpen(t)) return;
        var area = highlight != null ? highlight.WorldBounds : new Rect();
        var host = BaseSceneSetup.Instance;
        if (view?.root != null) view.root.SetActive(false);
        if (highlight != null) highlight.Hide();
        AudioManager.Instance?.PlaySfx("levelUp");
        if (host == null) { Close(); return; }
        AreaHighlight.Disperse(host, area, () =>
        {
            BaseMapManager.Instance?.RefreshBuildableArea(host.width, host.height);
            host.ClearUnlockedLand(GameManager.Instance.GetMainBaseLevel());
            host.RefreshCameraFraming();
        });
        Close();
    }

    private void OnDestroy()
    {
        if (highlight != null) highlight.Hide();
        if (Current == this) Current = null;
    }
}
