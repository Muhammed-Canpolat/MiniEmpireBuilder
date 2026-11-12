using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Tüm ekranı kaplayan görünmez yüzey: dünya dokunuşlarını (hedef kilidi, atış yeri) toplar. Düğmeler üstte kalır.</summary>
public sealed class IslandTouchSurface : MonoBehaviour, IPointerClickHandler
{
    public Action<Vector2> Tapped;
    public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(eventData.position);
}

/// <summary>Basılı tutma düğmesi (ATEŞ): bas / bırak olayları okçuya gider.</summary>
public sealed class IslandHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action Pressed, Released;
    private bool down;
    public void OnPointerDown(PointerEventData eventData) { down = true; Pressed?.Invoke(); }
    public void OnPointerUp(PointerEventData eventData) { if (down) { down = false; Released?.Invoke(); } }
    public void OnPointerExit(PointerEventData eventData) { if (down) { down = false; Released?.Invoke(); } }
}

/// <summary>
/// Ada arayüzü: üst bar (ANA ÜS, Akın/dalga, Et/Odun, kale canı), Akın seçimi, ATEŞ + OTO düğmeleri, öğretici ipucu ve sonuç.
/// Tamamı kodla kurulur (Reference UI dili).
/// </summary>
public sealed class IslandHud : MonoBehaviour
{
    private IslandSceneSetup setup;
    private IslandRaidManager raid;
    private IslandArcher archer;

    private RectTransform safe;
    private TextMeshProUGUI titleText, meatText, woodText, goldText, castleText, hintText, autoLabel, homeLabel, bannerText, mapLabel;
    private Image castleFill, hintBg, chargeRing, fireFace, cooldownFill, bannerBg;
    private Button mapButton;
    private readonly System.Collections.Generic.List<Image> edgeArrows = new System.Collections.Generic.List<Image>();
    private GameObject fireRoot, resultPanel, autoRoot, dock, castleBar;
    private IslandRaidBoard board;
    private bool prepOpen;
    private TextMeshProUGUI akinDockLabel;
    private Button homeButton, autoButton;
    private int selectedAkin = 1;
    private float homeConfirmUntil;
    private TextMeshProUGUI resultTitle, resultBody;
    private Button resultAgain, resultNext, resultHome;
    private float toastUntil;

    public static readonly Color Gold = ReferenceUiVisuals.Gold;

    public void Build(IslandSceneSetup owner, IslandRaidManager raidManager, IslandArcher islandArcher, Camera cam)
    {
        setup = owner;
        raid = raidManager;
        archer = islandArcher;
        var canvasObject = new GameObject("IslandCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        ReferenceUiVisuals.ConfigureCanvas(canvasObject.GetComponent<CanvasScaler>());
        safe = ReferenceUiVisuals.SafeArea(canvas.transform);

        // Dünya dokunuşları (en arkada).
        var surface = ReferenceUiVisuals.Image(safe, "TouchSurface", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0f));
        surface.raycastTarget = true;
        var touch = surface.gameObject.AddComponent<IslandTouchSurface>();
        touch.Tapped = screen => setup.HandleScreenTap(screen);

        BuildTopBar();
        BuildDock();
        BuildPrepPanel();
        BuildFireControls();
        BuildHint();
        BuildResult();
        selectedAkin = Mathf.Max(1, IslandSession.Save?.lastAkin ?? 1);
        SnapSelection();
        raid.StateChanged += _ => Refresh();
        raid.TutorialChanged += Refresh;
        Refresh();
    }

    // ==================== KURULUM ====================

    private void BuildTopBar()
    {
        var bar = ReferenceUiVisuals.Image(safe, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), Vector2.zero,
            new Color(0.08f, 0.06f, 0.04f, 0.72f));
        homeButton = ReferenceUiVisuals.Button(bar.transform, "HomeButton", L.T("ANA ÜS", "BASE"), false,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -16), new Vector2(96, 16), 12);
        homeLabel = homeButton.GetComponentInChildren<TextMeshProUGUI>();
        homeButton.onClick.AddListener(OnHome);
        titleText = ReferenceUiVisuals.Text(bar.transform, "Title", "", 13, Gold, TextAlignmentOptions.Center,
            new Vector2(0.21f, 0), new Vector2(0.45f, 1), Vector2.zero, Vector2.zero, true);
        // Kaynaklar: Tiny Swords simgeleri (Et, Odun, Ana Üs altını) + sayı
        var catalog = IslandVisualCatalog.Instance;
        meatText = ResourceChip(bar.transform, 0, catalog != null ? catalog.meatIcon : null);
        woodText = ResourceChip(bar.transform, 1, catalog != null ? catalog.woodIcon : null);
        goldText = ResourceChip(bar.transform, 2, catalog != null ? catalog.Single("gold_icon") : null);

        var barBg = ReferenceUiVisuals.Image(safe, "CastleBar", new Vector2(0.18f, 1), new Vector2(0.82f, 1),
            new Vector2(0, -74), new Vector2(0, -58), new Color(0.05f, 0.04f, 0.03f, 0.85f));
        castleBar = barBg.gameObject;
        castleFill = ReferenceUiVisuals.Image(barBg.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2),
            new Color(0.35f, 0.7f, 0.95f));
        castleFill.sprite = IslandArt.Square;
        castleFill.type = Image.Type.Filled;
        castleFill.fillMethod = Image.FillMethod.Horizontal;
        castleText = ReferenceUiVisuals.Text(barBg.transform, "Text", "", 11, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, true);
    }

    /// <summary>Alt dock (görünüm modu): AKIN düğmesi. Akın yalnız buradan başlar; ada girişte üs görünümü açılır (v2 §3).</summary>
    private TextMeshProUGUI ResourceChip(Transform bar, int index, Sprite icon)
    {
        float x0 = 0.445f + index * 0.135f;
        var group = ReferenceUiVisuals.Image(bar, "Res" + index, new Vector2(x0, 0), new Vector2(x0 + 0.135f, 1),
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
        group.raycastTarget = false;
        var image = ReferenceUiVisuals.Image(group.transform, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(0, -15), new Vector2(30, 15), Color.white);
        image.sprite = icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var value = ReferenceUiVisuals.Text(group.transform, "Value", "0", 14, ReferenceUiVisuals.Light, TextAlignmentOptions.Left,
            new Vector2(0, 0), Vector2.one, new Vector2(33, 0), new Vector2(40, 0), true);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.overflowMode = TextOverflowModes.Overflow;
        return value;
    }

    private void BuildDock()
    {
        var root = new GameObject("Dock", typeof(RectTransform));
        root.transform.SetParent(safe, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(-120, 26); rt.offsetMax = new Vector2(120, 92);
        dock = root;
        var button = ReferenceUiVisuals.Button(rt, "AkinButton", L.T("AKIN", "RAID"), true, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, 24);
        akinDockLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(() => { prepOpen = true; Refresh(); });
    }

    /// <summary>Akın Panosu (§3): AKIN düğmesi açar; kartlar merdiveni gösterir, SAVUNMAYA BAŞLA savunmayı başlatır.</summary>
    private void BuildPrepPanel()
    {
        board = new IslandRaidBoard();
        board.Build(safe);
        board.Selected = n => { selectedAkin = Mathf.Clamp(n, 1, Mathf.Max(1, MaxSelectable() + 3)); Refresh(); };
        board.StartPressed = OnStart;
        board.ClosePressed = () => { prepOpen = false; Refresh(); };
    }

    /// <summary>Test/ekran görüntüsü: panoyu açar ve N'yi seçer.</summary>
    public void OpenBoard(int akin) { selectedAkin = Mathf.Max(1, akin); prepOpen = true; Refresh(); }
    public bool BoardVisible => board != null && board.Visible;

    private void BuildFireControls()
    {
        fireRoot = new GameObject("FireControls", typeof(RectTransform));
        fireRoot.transform.SetParent(safe, false);
        var rootRt = (RectTransform)fireRoot.transform;
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one; rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        // ATEŞ / OTO / HARİTA: Tiny Swords UI düğme sprite'ları (kodla çizilmiş daire yok); dolum/şarj sprite üstünde dolgu
        var round = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("ui_round_red") : null;
        var fire = ReferenceUiVisuals.Image(rootRt, "FireButton", new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-176, 30), new Vector2(-22, 184), Color.white);
        fire.sprite = round;
        fire.raycastTarget = true;
        fireFace = fire;
        var hold = fire.gameObject.AddComponent<IslandHoldButton>();
        hold.Pressed = () => { if (archer != null) archer.PressFire(); fire.rectTransform.localScale = Vector3.one * 0.94f; };
        hold.Released = () => { if (archer != null) archer.ReleaseFire(); fire.rectTransform.localScale = Vector3.one; };
        cooldownFill = RadialOverlay(fire.transform, "Cooldown", round, new Color(0f, 0f, 0f, 0.5f));
        chargeRing = RadialOverlay(fire.transform, "Charge", round, new Color(1f, 0.85f, 0.3f, 0.65f));
        ReferenceUiVisuals.Text(fire.transform, "Label", L.T("ATEŞ", "FIRE"), 26, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);

        autoButton = SquareButton(rootRt, "AutoButton", L.T("OTO", "AUTO"), new Vector2(-266, 40), out autoLabel);
        autoRoot = autoButton.gameObject;
        autoButton.onClick.AddListener(OnAutoToggle);
        mapButton = SquareButton(rootRt, "MapButton", L.T("HARİTA", "MAP"), new Vector2(-266, 112), out mapLabel);
        mapButton.onClick.AddListener(() => { setup.CameraController.ToggleFullFrame(); });

        // Kısa bildirimler (okçu düştü, atış yeri kilidi...): Tiny Swords kurdele üstünde, otomatik kapanır
        bannerBg = ReferenceUiVisuals.Image(safe, "Banner", new Vector2(0.1f, 1), new Vector2(0.9f, 1),
            new Vector2(0, -150), new Vector2(0, -102), Color.white);
        var ribbon = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("ui_ribbon") : null;
        bannerBg.sprite = ribbon;
        if (ribbon != null) { bannerBg.type = Image.Type.Sliced; bannerBg.pixelsPerUnitMultiplier = 3f; }
        bannerText = ReferenceUiVisuals.Text(bannerBg.transform, "Text", "", 15, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(44, 6), new Vector2(-44, -6), true);
        bannerBg.gameObject.SetActive(false);
    }

    private static Image RadialOverlay(Transform parent, string name, Sprite sprite, Color color)
    {
        var overlay = ReferenceUiVisuals.Image(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
        overlay.sprite = sprite;
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;
        overlay.fillAmount = 0f;
        return overlay;
    }

    private static Button SquareButton(RectTransform parent, string name, string label, Vector2 corner, out TextMeshProUGUI text)
    {
        var image = ReferenceUiVisuals.Image(parent, name, new Vector2(1, 0), new Vector2(1, 0), corner, corner + new Vector2(84, 68), Color.white);
        image.sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("ui_square_blue") : null;
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        image.gameObject.AddComponent<ReferenceButtonPress>();
        text = ReferenceUiVisuals.Text(image.transform, "Label", label, 12, Color.white, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -10), true);
        text.textWrappingMode = TextWrappingModes.Normal;
        // Düğme kenarına taşmasın: uzun etiket ("OTO KAPALI", "HARİTA") küçülür.
        text.enableAutoSizing = true;
        text.fontSizeMin = 7f;
        text.fontSizeMax = 12f;
        return button;
    }

    /// <summary>Ekran dışındaki tekne/düşmanlar için kenar okları (takip kamerası).</summary>
    private void UpdateEdgeArrows()
    {
        var camera = setup.WorldCamera;
        bool show = raid.Phase == IslandRaidManager.State.Running && setup.CameraController != null && !setup.CameraController.FullFrame && camera != null;
        var targets = show ? IslandTarget.Collect() : new System.Collections.Generic.List<IslandTarget>();
        var offscreen = new System.Collections.Generic.List<(float d, Vector2 vp, bool boat)>();
        foreach (var target in targets)
        {
            Vector3 vp = camera.WorldToViewportPoint(target.Position);
            if (vp.x >= 0.03f && vp.x <= 0.97f && vp.y >= 0.1f && vp.y <= 0.93f) continue;
            offscreen.Add((Vector2.Distance(target.Position, camera.transform.position), new Vector2(vp.x, vp.y), target.IsBoat));
        }
        offscreen.Sort((x, y) => x.d.CompareTo(y.d));
        int count = Mathf.Min(offscreen.Count, 6);
        var sprite = IslandVisualCatalog.Instance != null ? IslandVisualCatalog.Instance.Single("arrow") : null;
        while (edgeArrows.Count < count)
        {
            var arrow = ReferenceUiVisuals.Image(safe, "EdgeArrow", Vector2.zero, Vector2.zero, new Vector2(-22, -22), new Vector2(22, 22), Color.white);
            arrow.sprite = sprite;
            arrow.preserveAspect = true;
            edgeArrows.Add(arrow);
        }
        for (int i = 0; i < edgeArrows.Count; i++)
        {
            var arrow = edgeArrows[i];
            if (i >= count) { arrow.gameObject.SetActive(false); continue; }
            arrow.gameObject.SetActive(true);
            Vector2 vp = offscreen[i].vp;
            Vector2 dir = (vp - new Vector2(0.5f, 0.5f)).normalized;
            Vector2 pos = new Vector2(Mathf.Clamp(vp.x, 0.06f, 0.94f), Mathf.Clamp(vp.y, 0.14f, 0.9f));
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = pos;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            arrow.color = offscreen[i].boat ? new Color(1f, 0.75f, 0.3f, 0.95f) : new Color(1f, 0.3f, 0.25f, 0.95f);
        }
    }

    private void BuildHint()
    {
        hintBg = ReferenceUiVisuals.Image(safe, "HintBg", new Vector2(0.05f, 1), new Vector2(0.95f, 1),
            new Vector2(0, -122), new Vector2(0, -84), new Color(0.08f, 0.06f, 0.04f, 0.82f));
        hintBg.sprite = ReferenceUiVisuals.PillSprite();
        hintBg.type = Image.Type.Sliced;
        hintText = ReferenceUiVisuals.Text(hintBg.transform, "Hint", "", 15, Gold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0), false, true);
    }

    private void BuildResult()
    {
        var dim = ReferenceUiVisuals.Image(safe, "ResultPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.6f));
        dim.raycastTarget = true;
        resultPanel = dim.gameObject;
        var center = new Vector2(0.5f, 0.5f);
        var card = ReferenceUiVisuals.Image(dim.transform, "Card", center, center, new Vector2(-165, -200), new Vector2(165, 200),
            new Color(0.12f, 0.09f, 0.06f, 0.97f));
        card.sprite = ReferenceUiVisuals.PillSprite();
        card.type = Image.Type.Sliced;
        resultTitle = ReferenceUiVisuals.Text(card.transform, "Title", "", 28, Gold, TextAlignmentOptions.Center,
            new Vector2(0, 1), Vector2.one, new Vector2(10, -64), new Vector2(-10, -14), true);
        resultBody = ReferenceUiVisuals.Text(card.transform, "Body", "", 15, ReferenceUiVisuals.Light, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(18, 130), new Vector2(-18, -74), false, true);
        resultNext = ReferenceUiVisuals.Button(card.transform, "Next", L.T("SONRAKİ AKIN", "NEXT RAID"), true,
            new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0, 76), new Vector2(0, 118), 16);
        resultAgain = ReferenceUiVisuals.Button(card.transform, "Again", L.T("TEKRAR", "AGAIN"), false,
            new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0, 42), new Vector2(0, 72), 14);
        resultHome = ReferenceUiVisuals.Button(card.transform, "Home", L.T("ANA ÜS", "BASE"), false,
            new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0, 8), new Vector2(0, 38), 14);
        resultNext.onClick.AddListener(() => { raid.ReturnToIdle(); prepOpen = true; selectedAkin = Mathf.Min(MaxSelectable(), raid.Akin + 1); Refresh(); });
        resultAgain.onClick.AddListener(() => { raid.ReturnToIdle(); prepOpen = true; selectedAkin = raid.Akin; Refresh(); });
        resultHome.onClick.AddListener(GoHome);
        resultPanel.SetActive(false);
    }

    // ==================== DAVRANIŞ ====================

    private int MaxSelectable()
    {
        var save = IslandSession.Save;
        int highest = save != null ? save.highestAkin : 0;
        return Mathf.Min(highest + 1, Progression.Island.MaxAkin(IslandSession.KeepLevel()));
    }

    private void SnapSelection()
    {
        // Kapıdan geçilmeyen Akın seçili kalmasın.
        selectedAkin = Mathf.Clamp(selectedAkin, 1, Mathf.Max(1, MaxSelectable()));
    }

    private void OnStart()
    {
        if (!raid.CanStart(selectedAkin))
        {
            Toast(GateText(selectedAkin));
            return;
        }
        prepOpen = false;
        raid.StartAkin(selectedAkin);
        Refresh();
    }

    private void OnAutoToggle()
    {
        int keep = IslandSession.KeepLevel();
        if (!IslandSession.AutoFireUnlocked(keep))
        {
            Toast(L.T($"Oto-ateş: Kıyı Kalesi {Progression.Island.AutoFireUnlockKeepLevel}'te açılır.",
                $"Auto-fire unlocks at Keep {Progression.Island.AutoFireUnlockKeepLevel}."));
            return;
        }
        archer.AutoFire = !archer.AutoFire;
        if (IslandSession.Save != null) IslandSession.Save.autoFire = archer.AutoFire;
        GameManager.Instance?.SaveGame();
        Refresh();
    }

    private void OnHome()
    {
        if (raid.Phase != IslandRaidManager.State.Running) { GoHome(); return; }
        if (Time.unscaledTime < homeConfirmUntil) { raid.Abandon(); GoHome(); return; }
        homeConfirmUntil = Time.unscaledTime + 3f;
        Toast(L.T("Akın yarıda kalır. Çıkmak için tekrar dokun.", "The raid will be abandoned. Tap again to leave."));
    }

    private void GoHome()
    {
        IslandSession.Clear();
        GameManager.Instance?.LoadBaseWorld();
    }

    public void Toast(string text)
    {
        bannerText.text = text;
        bannerBg.gameObject.SetActive(true);
        toastUntil = Time.unscaledTime + 2.4f;
    }

    private string GateText(int akin)
    {
        int need = Progression.Island.AkinRequiredCastle(akin);
        var save = IslandSession.Save;
        if (akin > (save?.highestAkin ?? 0) + 1)
            return L.T("Önce önceki Akın'ı kazan.", "Win the previous raid first.");
        return L.T($"Gerekli: Kıyı Kalesi {need} (Ana Üs {need})", $"Requires Keep {need} (Main Base {need})");
    }

    // ==================== YENİLEME ====================

    private void Refresh()
    {
        if (raid == null) return;
        var state = raid.Phase;
        bool running = state == IslandRaidManager.State.Running;
        bool idle = state == IslandRaidManager.State.Idle;
        if (!idle) prepOpen = false;
        board.SetVisible(idle && prepOpen);
        dock.SetActive(idle && !prepOpen);
        castleBar.SetActive(!idle);
        fireRoot.SetActive(running);
        resultPanel.SetActive(state == IslandRaidManager.State.Won || state == IslandRaidManager.State.Lost);
        SnapSelection();

        if (idle)
        {
            int n = selectedAkin;
            akinDockLabel.text = L.T($"AKIN {Mathf.Min(MaxSelectable(), Mathf.Max(1, selectedAkin))}", $"RAID {Mathf.Min(MaxSelectable(), Mathf.Max(1, selectedAkin))}");
            if (prepOpen)
            {
                bool can = raid.CanStart(n);
                board.Refresh(n, IslandSession.Save?.highestAkin ?? 0, IslandSession.KeepLevel(), MaxSelectable(), can, GateText(n));
            }
        }

        if (state == IslandRaidManager.State.Won)
        {
            var r = raid.LastReward;
            resultTitle.text = L.T("ZAFER!", "VICTORY!");
            string gems = r.gems > 0 ? L.T($"\n+{r.gems} elmas", $"\n+{r.gems} gems") : "";
            resultBody.text = L.T(
                $"Akın {raid.Akin} temizlendi.\n+{r.gold} altın (Ana Üs) · +{r.meat} Et · +{r.wood} Odun{gems}",
                $"Raid {raid.Akin} cleared.\n+{r.gold} gold (Base) · +{r.meat} Meat · +{r.wood} Wood{gems}");
            if (raid.TutorialFinishedThisRun)
            {
                resultBody.text += "\n" + L.T("Ağıl ve Oduncu yakında: Et ve Odun orada harcanacak.",
                    "Pasture and Lumber Camp are coming: Meat and Wood will be spent there.");
            }
            resultBody.text += DamagedBuildings();
            bool next = raid.CanStart(raid.Akin + 1);
            resultNext.gameObject.SetActive(next);
        }
        else if (state == IslandRaidManager.State.Lost)
        {
            resultTitle.text = L.T("YENİLGİ", "DEFEAT");
            resultBody.text = L.T($"Kıyı Kalesi düştü.\n{raid.DefeatReason}", $"The keep has fallen.\n{raid.DefeatReason}") + DamagedBuildings();
            resultNext.gameObject.SetActive(false);
        }
        RefreshAuto();
    }

    /// <summary>Sonuç ekranı: hasarlı binalar ve onarım süresi (§7.7; ücretsiz, yalnız bekleme).</summary>
    private static string DamagedBuildings()
    {
        var names = new System.Text.StringBuilder();
        int count = 0;
        foreach (var building in IslandBuilding.All)
        {
            if (building == null || !building.UnderRepair) continue;
            if (count++ > 0) names.Append(", ");
            int left = Mathf.CeilToInt(building.RepairSecondsLeft);
            names.Append($"{BuildingName(building.Kind)} {left / 60}:{left % 60:00}");
        }
        return count == 0 ? "" : "\n" + L.T("Onarımda (ücretsiz): ", "Repairing (free): ") + names;
    }

    private static string BuildingName(IslandLayout.BuildingKind kind)
    {
        switch (kind)
        {
            case IslandLayout.BuildingKind.House: return L.T("Ev", "House");
            case IslandLayout.BuildingKind.Lumber: return L.T("Oduncu", "Lumber");
            case IslandLayout.BuildingKind.Pasture: return L.T("Ağıl", "Pasture");
            case IslandLayout.BuildingKind.Archery: return L.T("Okçuluk", "Archery");
            case IslandLayout.BuildingKind.Tower: return L.T("Kule", "Tower");
            default: return L.T("Kale", "Keep");
        }
    }

    private void RefreshAuto()
    {
        if (autoLabel == null || archer == null) return;
        bool unlocked = IslandSession.AutoFireUnlocked(IslandSession.KeepLevel());
        autoLabel.text = !unlocked ? L.T("OTO ", "AUTO ") + "KK" + Progression.Island.AutoFireUnlockKeepLevel
            : archer.AutoFire ? L.T("OTO\nAÇIK", "AUTO\nON") : L.T("OTO\nKAPALI", "AUTO\nOFF");
        var image = autoButton.GetComponent<Image>();
        if (image != null) image.color = archer.AutoFire ? new Color(0.6f, 1f, 0.6f) : Color.white;
        if (mapLabel != null && setup.CameraController != null)
            mapLabel.text = setup.CameraController.FullFrame ? L.T("TAKİP", "FOLLOW") : L.T("HARİTA", "MAP");
    }

    private void Update()
    {
        if (raid == null) return;
        // Kale canı
        if (setup.CastleStructure != null)
        {
            var c = setup.CastleStructure;
            castleFill.fillAmount = c.HealthPercent;
            castleFill.color = c.HealthPercent > 0.5f ? new Color(0.35f, 0.7f, 0.95f)
                : c.HealthPercent > 0.25f ? new Color(0.95f, 0.75f, 0.25f) : new Color(0.9f, 0.3f, 0.25f);
            castleText.text = L.T($"Kıyı Kalesi {Mathf.CeilToInt(c.CurrentHealth)}/{Mathf.CeilToInt(c.MaxHealth)}",
                $"Coastal Keep {Mathf.CeilToInt(c.CurrentHealth)}/{Mathf.CeilToInt(c.MaxHealth)}");
        }
        var save = IslandSession.Save;
        meatText.text = save != null ? save.meat.ToString() : "0";
        woodText.text = save != null ? save.wood.ToString() : "0";
        var player = GameManager.Instance != null ? GameManager.Instance.PlayerData : null;
        goldText.text = player != null ? player.gold.ToString() : "0";
        titleText.text = raid.Phase == IslandRaidManager.State.Running
            ? L.T($"AKIN {raid.Akin} · DALGA {raid.Wave}/{raid.TotalWaves}", $"RAID {raid.Akin} · WAVE {raid.Wave}/{raid.TotalWaves}")
            : L.T("MARTI ADASI", "GULL ISLE");
        if (homeLabel != null)
            homeLabel.text = Time.unscaledTime < homeConfirmUntil ? L.T("EMİN MİSİN?", "SURE?") : L.T("ANA ÜS", "BASE");

        // Öğretici ipucu
        string hint = raid.TutorialHint();
        hintBg.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        hintText.text = hint;

        // ATEŞ: dolum halkası + yanıp sönen vurgu (öğretici adım 1)
        if (archer != null)
        {
            chargeRing.fillAmount = archer.Charging ? archer.Charge01 : 0f;
            cooldownFill.fillAmount = archer.IsAlive ? archer.CooldownFraction : 1f;
            bool pulse = raid.TutorialStep == 1 && raid.Phase == IslandRaidManager.State.Running;
            fireFace.color = pulse ? Color.Lerp(Color.white, new Color(1f, 0.75f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 6f)) : Color.white;
            if (!archer.IsAlive)
            {
                bannerText.text = L.T($"OKÇU DÜŞTÜ · {Mathf.CeilToInt(archer.RespawnLeft)} sn", $"ARCHER DOWN · {Mathf.CeilToInt(archer.RespawnLeft)}s");
                bannerBg.gameObject.SetActive(true);
                toastUntil = Time.unscaledTime + 0.4f;
            }
        }
        UpdateEdgeArrows();
        RefreshAuto();
        if (bannerBg != null && bannerBg.gameObject.activeSelf && Time.unscaledTime > toastUntil) bannerBg.gameObject.SetActive(false);
    }
}
