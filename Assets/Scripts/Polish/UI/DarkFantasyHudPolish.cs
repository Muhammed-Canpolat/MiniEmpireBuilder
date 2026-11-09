using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Visual layout for the existing base and build-menu controls. No gameplay listeners are replaced.
public static class DarkFantasyHudPolish
{
    private static readonly Color SoftGold = ReferenceUiVisuals.Hex(0xC9B88F);

    public static void PolishBase(Canvas canvas, Transform hud)
    {
        if (canvas == null || hud == null) return;
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);
        canvas.gameObject.AddComponent<WorldViewportBars>().Initialize(canvas, 64f, 92f, 22f);
        hud.SetParent(safe, false);
        var menu = canvas.transform.Find("BuildMenuPanel");
        if (menu != null) menu.SetParent(safe, false);
        var blocker = ReferenceUiVisuals.Image(safe, "ModalBlocker", Vector2.zero,
            Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.50f));
        blocker.raycastTarget = true;
        blocker.gameObject.AddComponent<Button>().onClick.AddListener(() => BaseWorldUI.Instance?.HideAllPanelsPublic());
        blocker.transform.SetSiblingIndex(1);
        blocker.gameObject.SetActive(false);
        foreach (string panelName in new[] { "BuildingInfo", "HeroInfo" })
        {
            var panel = canvas.transform.Find(panelName);
            if (panel != null) panel.SetParent(safe, false);
        }

        var top = hud.Find("TopBarBG") as RectTransform;
        if (top != null)
        {
            top.sizeDelta = new Vector2(0, 64);
            var image = top.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = ReferenceUiVisuals.Hex(0x14110D);
            }
        }

        var dock = ReferenceUiVisuals.Image(hud, "BottomDock", Vector2.zero,
            new Vector2(1, 0), Vector2.zero, new Vector2(0, 92), ReferenceUiVisuals.Panel);
        dock.transform.SetAsFirstSibling();
        var steelLight = ReferenceUiVisuals.Hex(0x747580);
        var steelDark = ReferenceUiVisuals.Hex(0x111319);
        ReferenceUiVisuals.Image(dock.transform, "DockInset", new Vector2(0, 0), Vector2.one,
            new Vector2(8, 8), new Vector2(-8, -8), ReferenceUiVisuals.Hex(0x161318));
        ReferenceUiVisuals.Image(dock.transform, "TopRule", new Vector2(0, 1), Vector2.one,
            new Vector2(0, -4), Vector2.zero, ReferenceUiVisuals.Hex(0x6B4A22));
        ReferenceUiVisuals.Image(dock.transform, "TopShadow", new Vector2(0, 1), Vector2.one,
            new Vector2(0, -10), new Vector2(0, -7), ReferenceUiVisuals.Hex(0x1A140E));
        ReferenceUiVisuals.Image(dock.transform, "BottomRule", Vector2.zero,
            new Vector2(1, 0), Vector2.zero, new Vector2(0, 6), steelDark);
        ReferenceUiVisuals.Image(dock.transform, "LeftRail", Vector2.zero,
            new Vector2(0, 1), Vector2.zero, new Vector2(7, 0), steelLight);
        ReferenceUiVisuals.Image(dock.transform, "RightRail", new Vector2(1, 0),
            Vector2.one, new Vector2(-7, 0), Vector2.zero, steelLight);
        for (int side = 0; side < 2; side++)
        for (int end = 0; end < 2; end++)
        {
            var rivet = ReferenceUiVisuals.Image(dock.transform,
                "DockRivet_" + side + "_" + end, new Vector2(side, end),
                new Vector2(side, end),
                new Vector2(side == 0 ? 10 : -17, end == 0 ? 10 : -17),
                new Vector2(side == 0 ? 17 : -10, end == 0 ? 17 : -10),
                ReferenceUiVisuals.Hex(0xB69A61));
            rivet.sprite = ReferenceUiVisuals.PillSprite();
        }

        SetAnchors(hud.Find("GoldPill") as RectTransform, new Vector2(0.02f, 0.925f), new Vector2(0.30f, 0.985f));
        SetAnchors(hud.Find("BattleLevelPill") as RectTransform, new Vector2(0.30f, 0.925f), new Vector2(0.72f, 0.985f));
        SetAnchors(hud.Find("BuilderPill") as RectTransform, new Vector2(0.71f, 0.925f), new Vector2(0.88f, 0.985f));
        SetAnchors(hud.Find("GoldPill/CoinIcon") as RectTransform, new Vector2(0.03f, 0.25f), new Vector2(0.23f, 0.75f));
        SetAnchors(hud.Find("GoldPill/GoldText") as RectTransform, new Vector2(0.29f, 0.12f), new Vector2(0.98f, 0.88f));
        SetAnchors(hud.Find("BuilderPill/AvatarIcon") as RectTransform, new Vector2(0.16f, 0.25f), new Vector2(0.46f, 0.75f));
        SetAnchors(hud.Find("BuilderPill/BuilderCount") as RectTransform, new Vector2(0.53f, 0.12f), new Vector2(0.98f, 0.88f));
        var levelTrack = ReferenceUiVisuals.Image(hud, "BaseLevelTrack",
            new Vector2(0.37f, 0.919f), new Vector2(0.63f, 0.925f),
            Vector2.zero, Vector2.zero, ReferenceUiVisuals.Hex(0x3A3020));
        ReferenceUiVisuals.Image(levelTrack.transform, "Fill", Vector2.zero,
            new Vector2(0, 1), Vector2.zero, Vector2.zero, ReferenceUiVisuals.Gold);
        // Çubuğun anlamı: altın / sonraki Ana Üs yükseltmesi. Metni BaseWorldUI günceller.
        ReferenceUiVisuals.Text(hud, "BaseLevelCaption", "", 11, SoftGold,
            TextAlignmentOptions.Center, new Vector2(0.30f, 0.897f), new Vector2(0.70f, 0.918f),
            Vector2.zero, Vector2.zero, bodyBold: true);

        // İnşaatçı göstergesi dokununca ne olduğunu söyler.
        var builderPill = hud.Find("BuilderPill");
        if (builderPill != null && builderPill.GetComponent<Button>() == null)
        {
            var pillImage = builderPill.GetComponent<Image>();
            if (pillImage != null) pillImage.raycastTarget = true;
            builderPill.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                bool free = BuilderSystem.Instance == null || BuilderSystem.Instance.HasAvailableBuilder();
                Object.FindFirstObjectByType<BaseSceneSetup>()?.ShowHudToast(free
                    ? "İnşaatçın boşta: yeni yapı kurabilir ya da yükseltebilirsin"
                    : "İnşaatçın çalışıyor: iş bitince yeni yapı başlatabilirsin");
            });
        }
        var lockedToast = ReferenceUiVisuals.Image(hud, "LockedLandToast",
            new Vector2(0.08f, 1), new Vector2(0.92f, 1),
            new Vector2(0, -238), new Vector2(0, -190), Color.white);
        lockedToast.sprite = ReferenceUiVisuals.ToastSprite();
        lockedToast.type = Image.Type.Sliced;
        lockedToast.gameObject.AddComponent<CanvasGroup>();
        ReferenceUiVisuals.Text(lockedToast.transform, "Text", "", 14,
            ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(12, 2), new Vector2(-12, -2),
            bodyBold: true);
        lockedToast.gameObject.AddComponent<PanelController>().Initialize(false);

        StyleText(hud, "GoldText", 16, ReferenceUiVisuals.Light, false);
        StyleText(hud, "LevelText", 15, ReferenceUiVisuals.Gold, true);
        StyleText(hud, "BuilderCount", 15, ReferenceUiVisuals.Light, false);
        StyleText(hud, "GpsText", 11, SoftGold, false);

        StyleDockButton(hud.Find("BuildBtn") as RectTransform, new Vector2(0.25f, 0f), false, UiStrings.Build);
        StyleDockButton(hud.Find("BattleBtn") as RectTransform, new Vector2(0.75f, 0f), true, UiStrings.Battle);
        foreach (string name in new[] { "BuildBtn", "BattleBtn" })
        {
            var button = hud.Find(name) as RectTransform;
            if (button == null) continue;
            button.anchorMin = button.anchorMax = new Vector2(button.anchorMin.x, 0f);
            button.anchoredPosition = new Vector2(0f, 46f);
        }

        // The movement control remains available, but stays quiet until the player uses it.
        var joystick = hud.Find("VirtualJoystick") as RectTransform;
        if (joystick != null)
        {
            joystick.anchorMin = joystick.anchorMax = new Vector2(0.5f, 0.5f);
            joystick.sizeDelta = new Vector2(150, 150);
            joystick.anchoredPosition = Vector2.zero;
            var image = joystick.GetComponent<Image>();
            if (image != null) image.color = new Color(0.08f, 0.07f, 0.05f, 0.20f);
        }

        StyleInfoPanel(safe.Find("BuildingInfo"));
        var heroPanel = safe.Find("HeroInfo");
        StyleInfoPanel(heroPanel);
        SetAnchors(heroPanel?.Find("Level") as RectTransform,
            new Vector2(0.07f, 0.59f), new Vector2(0.93f, 0.69f));
        SetAnchors(heroPanel?.Find("Stats") as RectTransform,
            new Vector2(0.07f, 0.33f), new Vector2(0.93f, 0.58f));
        BaseHudRedesign.Apply(hud);
    }

    public static void PolishBuildMenu(Transform panel)
    {
        if (panel == null) return;
        var rt = panel as RectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(1, 0.39f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var bg = panel.GetComponent<Image>();
        if (bg != null) bg.color = ReferenceUiVisuals.Panel;
        ReferenceUiVisuals.Text(panel, "Header", "İNŞA ET", 24,
            ReferenceUiVisuals.Gold, TextAlignmentOptions.Left,
            new Vector2(0.05f, 0.84f), new Vector2(0.78f, 0.98f),
            Vector2.zero, Vector2.zero, bodyBold: true);

        var border = panel.Find("Border")?.GetComponent<Image>();
        if (border != null) border.color = ReferenceUiVisuals.Gold;
        var close = panel.Find("CloseBtn");
        if (close != null)
        {
            var closeRt = close as RectTransform;
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = Vector2.one;
            closeRt.anchoredPosition = new Vector2(-14f, -12f);
            closeRt.sizeDelta = new Vector2(44f, 44f);
            var image = close.GetComponent<Image>();
            if (image != null) image.color = ReferenceUiVisuals.Red;
            StyleText(close, "Text", 18, Color.white, true);
        }

        for (int i = 0; i < 2; i++)
        {
            var tab = panel.Find(i == 0 ? "Tab_YAPI" : "Tab_DEKOR") as RectTransform;
            if (tab == null) continue;
            tab.anchorMin = new Vector2(i == 0 ? 0.035f : 0.51f, 1f);
            tab.anchorMax = new Vector2(i == 0 ? 0.49f : 0.965f, 1f);
            tab.pivot = new Vector2(0.5f, 1f);
            tab.offsetMin = new Vector2(0f, -106f);
            tab.offsetMax = new Vector2(0f, -62f);
            var label = tab.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.color = ReferenceUiVisuals.Hex(0xC9B88F);
            var line = tab.Find("GoldUnderline") as RectTransform;
            if (line != null)
            {
                line.anchorMin = new Vector2(0f, 0f);
                line.anchorMax = new Vector2(1f, 0f);
                line.sizeDelta = new Vector2(0f, 3f);
                line.GetComponent<Image>().color = ReferenceUiVisuals.Gold;
            }
        }

        BuildingType[] types = { BuildingType.ArcherTower, BuildingType.CannonTower, BuildingType.Monastery };
        for (int i = 0; i < types.Length; i++)
        {
            var item = panel.Find("Item_" + types[i]);
            if (item == null) continue;
            float left = 0.04f + i * 0.32f;
            SetAnchors(item as RectTransform, new Vector2(left, 0.14f), new Vector2(left + 0.28f, 0.65f));
            var itemBg = item.GetComponent<Image>();
            if (itemBg != null) itemBg.color = ReferenceUiVisuals.Background;
            var outline = item.GetComponent<Outline>();
            if (outline == null) outline = item.gameObject.AddComponent<Outline>();
            outline.effectColor = i == 0 ? ReferenceUiVisuals.Gold : ReferenceUiVisuals.Border;
            outline.effectDistance = new Vector2(2, -2);

            var icon = item.Find("Icon")?.GetComponent<Image>();
            if (icon != null) { icon.color = Color.white; icon.preserveAspect = true; }
            StyleText(item, "Name", 15, ReferenceUiVisuals.Light, true);
            StyleText(item, "Cost", 13, ReferenceUiVisuals.Gold, false);
            SetAnchors(item.Find("Cost") as RectTransform, new Vector2(0, 0.12f), new Vector2(1, 0.27f));
            StyleText(item, "PlacedCount", 11, ReferenceUiVisuals.Muted, false);
            SetAnchors(item.Find("PlacedCount") as RectTransform, new Vector2(0, 0.01f), new Vector2(1, 0.14f));
        }
        // Dekor kartları kaydırılabilir ızgarada; alan sekmelerin altından ipucunun üstüne.
        SetAnchors(panel.Find("DecorScroll") as RectTransform, new Vector2(0.03f, 0.09f), new Vector2(0.97f, 0.64f));
        ReferenceUiVisuals.Text(panel, "BuildMenuHint", "Kilitli yapılar üs geliştikçe açılır.", 12,
            SoftGold, TextAlignmentOptions.Center, new Vector2(0, 0.005f),
            new Vector2(1, 0.085f), new Vector2(12, 0), new Vector2(-12, 0));
    }

    public static void PolishBattle(Canvas canvas)
    {
        if (canvas == null) return;
        var safe = ReferenceUiVisuals.SafeArea(canvas.transform);
        canvas.gameObject.AddComponent<WorldViewportBars>().Initialize(canvas, 84f, BattleHudRedesign.DockHeight, 0f);
        string[] safeChildren = { "TopBar", "HeroBarBg", "BaseBarBg", "VirtualJoystick", "Minimap" };
        foreach (string name in safeChildren)
        {
            var child = canvas.transform.Find(name);
            if (child != null) child.SetParent(safe, false);
        }

        var dock = ReferenceUiVisuals.Image(safe, "BattleBottomDock", Vector2.zero,
            new Vector2(1, 0.165f), Vector2.zero, Vector2.zero, ReferenceUiVisuals.Panel);
        dock.transform.SetAsFirstSibling();
        var vignette = ReferenceUiVisuals.Image(safe, "BattleVignette", Vector2.zero,
            Vector2.one, Vector2.zero, Vector2.zero, Color.white);
        vignette.sprite = MakeVignette();
        vignette.transform.SetSiblingIndex(1);
        ReferenceUiVisuals.Image(dock.transform, "TopRule", new Vector2(0, 1), Vector2.one,
            Vector2.zero, new Vector2(0, 4), ReferenceUiVisuals.Border);

        var top = safe.Find("TopBar") as RectTransform;
        if (top != null)
        {
            SetAnchors(top, new Vector2(0, 0.90f), Vector2.one);
            var image = top.GetComponent<Image>();
            if (image != null) image.color = ReferenceUiVisuals.Panel;
            var gradient = top.GetComponent<UIGradient>();
            if (gradient != null)
            {
                gradient.colorTop = ReferenceUiVisuals.Panel;
                gradient.colorBottom = ReferenceUiVisuals.Background;
            }
            var rule = top.Find("BarLine")?.GetComponent<Image>();
            if (rule != null) rule.color = ReferenceUiVisuals.Border;
            SetAnchors(top.Find("EnemyText") as RectTransform, new Vector2(0.04f, 0.52f), new Vector2(0.31f, 0.90f));
            SetAnchors(top.Find("WaveText") as RectTransform, new Vector2(0.31f, 0.52f), new Vector2(0.69f, 0.90f));
            SetAnchors(top.Find("GoldText") as RectTransform, new Vector2(0.69f, 0.52f), new Vector2(0.96f, 0.90f));
            SetAnchors(top.Find("LevelText") as RectTransform, new Vector2(0.27f, 0.12f), new Vector2(0.73f, 0.47f));
            StyleText(top, "EnemyText", 39, ReferenceUiVisuals.Light, false);
            StyleText(top, "WaveText", 46, ReferenceUiVisuals.Gold, true);
            StyleText(top, "GoldText", 39, ReferenceUiVisuals.Gold, false);
            StyleText(top, "LevelText", 32, SoftGold, false);
            var enemyChip = ReferenceUiVisuals.Image(top, "EnemyChip", new Vector2(0.035f, 0.49f),
                new Vector2(0.315f, 0.91f), Vector2.zero, Vector2.zero, Color.white);
            enemyChip.sprite = ReferenceUiVisuals.PillSprite();
            enemyChip.type = Image.Type.Sliced;
            enemyChip.transform.SetAsFirstSibling();
            var goldChip = ReferenceUiVisuals.Image(top, "GoldChip", new Vector2(0.70f, 0.49f),
                new Vector2(0.87f, 0.91f), Vector2.zero, Vector2.zero, Color.white);
            goldChip.sprite = ReferenceUiVisuals.PillSprite();
            goldChip.type = Image.Type.Sliced;
            goldChip.transform.SetAsFirstSibling();
            SetAnchors(top.Find("EnemyText") as RectTransform, new Vector2(0.108f, 0.52f), new Vector2(0.31f, 0.90f));
            var enemyIcon = ReferenceUiVisuals.Image(top, "EnemyChipIcon",
                new Vector2(0.047f, 0.59f), new Vector2(0.102f, 0.84f),
                Vector2.zero, Vector2.zero, ReferenceUiVisuals.Hex(0xFF6A5C)); // koyu zeminde okunur
            enemyIcon.sprite = SpriteManager.Instance?.IconBattleNew;
            enemyIcon.preserveAspect = true;
            SetAnchors(top.Find("GoldText") as RectTransform, new Vector2(0.75f, 0.52f), new Vector2(0.86f, 0.90f));
            var coin = ReferenceUiVisuals.Image(top, "GoldCoin", new Vector2(0.70f, 0.58f),
                new Vector2(0.76f, 0.83f), Vector2.zero, Vector2.zero, Color.white);
            coin.sprite = SpriteManager.Instance?.IconGold;
            coin.preserveAspect = true;
            coin.transform.SetAsLastSibling();
            var progress = ReferenceUiVisuals.Image(top, "WaveTrack", new Vector2(0.04f, 0.06f),
                new Vector2(0.96f, 0.105f), Vector2.zero, Vector2.zero, ReferenceUiVisuals.Background);
        }

        var incoming = ReferenceUiVisuals.Image(safe, "IncomingChip", new Vector2(0.28f, 0.85f),
            new Vector2(0.72f, 0.88f), Vector2.zero, Vector2.zero, ReferenceUiVisuals.Red);
        ReferenceUiVisuals.Text(incoming.transform, "Label", "", 30, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            bodyBold: true);
        incoming.gameObject.SetActive(false);
        var countdown = ReferenceUiVisuals.Text(safe, "NextWaveCountdown", "", 22,
            ReferenceUiVisuals.Gold, TextAlignmentOptions.Center,
            new Vector2(0.24f, 0.81f), new Vector2(0.76f, 0.84f),
            Vector2.zero, Vector2.zero, bodyBold: true);
        countdown.gameObject.SetActive(false);

        StyleBattleHealth(safe.Find("HeroBarBg") as RectTransform, new Vector2(0.02f, 0.075f),
            new Vector2(0.32f, 0.15f), "HeroLabel", "HeroHpText", "HeroHealthBar", ReferenceUiVisuals.Hex(0x4FCF74));
        StyleBattleHealth(safe.Find("BaseBarBg") as RectTransform, new Vector2(0.35f, 0.075f),
            new Vector2(0.65f, 0.15f), "BaseLabel", "BaseHpText", "BaseHealthBar", ReferenceUiVisuals.Gold);

        var joystick = safe.Find("VirtualJoystick") as RectTransform;
        if (joystick != null)
        {
            joystick.anchorMin = joystick.anchorMax = new Vector2(0.5f, 0.5f);
            joystick.anchoredPosition = Vector2.zero;
            joystick.sizeDelta = new Vector2(185, 185);
            var image = joystick.GetComponent<Image>();
            if (image != null) image.color = new Color(0.165f, 0.13f, 0.094f, 0.86f);
        }

        var minimap = safe.Find("Minimap") as RectTransform;
        if (minimap != null)
        {
            SetAnchors(minimap, new Vector2(0.78f, 0.80f), new Vector2(0.97f, 0.89f));
            var outline = minimap.GetComponent<Outline>();
            if (outline == null) outline = minimap.gameObject.AddComponent<Outline>();
            outline.effectColor = ReferenceUiVisuals.Gold;
            outline.effectDistance = new Vector2(2, -2);
        }

        // Formül biçimli net kazanç metni gizli tutulur: güncel altın üst çipte,
        // döküm zafer ekranında. BattleUI nesneyi adıyla bulduğu için oluşturulur.
        var netText = ReferenceUiVisuals.Text(safe, "BattleNetText", "", 34,
            ReferenceUiVisuals.Gold, TextAlignmentOptions.Center, new Vector2(0, 0.025f),
            new Vector2(1, 0.07f), Vector2.zero, Vector2.zero, bodyBold: true);
        netText.gameObject.SetActive(false);
        var buildHint = ReferenceUiVisuals.Text(safe, "BattleBuildHint", UiStrings.BattleBuildHint, 30,
            SoftGold, TextAlignmentOptions.Center, new Vector2(0, 0.02f), new Vector2(1, 0.065f),
            Vector2.zero, Vector2.zero, bodyBold: true);
        // Kule hakkı olmayan oyuncuya yapamayacağı bir şeyi önermeyiz.
        var player = GameManager.Instance?.PlayerData;
        bool ownsTower = player?.placedBuildings != null && player.placedBuildings.Exists(b =>
            b != null && (b.buildingType == "ArcherTower" || b.buildingType == "CannonTower"
                || b.buildingType == "Monastery"));
        if (player?.firstBattleTowerBuilt == true || !ownsTower)
            buildHint.gameObject.SetActive(false);
        MoveHint.TryShow(safe);
        BattleHudRedesign.Apply(safe as RectTransform);
    }

    private static void StyleBattleHealth(RectTransform root, Vector2 min, Vector2 max,
        string labelName, string valueName, string sliderName, Color fillColor)
    {
        if (root == null) return;
        SetAnchors(root, min, max);
        var bg = root.GetComponent<Image>();
        if (bg != null) bg.color = ReferenceUiVisuals.Background;
        StyleText(root, labelName, 28, ReferenceUiVisuals.Light, false);
        StyleText(root, valueName, 26, ReferenceUiVisuals.Light, false);
        SetAnchors(root.Find(labelName) as RectTransform, new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.98f));
        SetAnchors(root.Find(valueName) as RectTransform, new Vector2(0.04f, 0.37f), new Vector2(0.96f, 0.68f));
        var hpValue = root.Find(valueName)?.GetComponent<TextMeshProUGUI>();
        if (hpValue != null) hpValue.enableWordWrapping = false;
        var slider = root.Find(sliderName) as RectTransform;
        SetAnchors(slider, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.34f));
        if (slider != null)
        {
            var fill = slider.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fill != null) fill.color = fillColor;
            var track = slider.Find("Background")?.GetComponent<Image>();
            if (track != null) track.color = ReferenceUiVisuals.Panel;
        }
    }

    private static void StyleInfoPanel(Transform panel)
    {
        if (panel == null) return;
        var panelRect = panel as RectTransform;
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0);
        panelRect.pivot = new Vector2(0.5f, 0);
        panelRect.anchoredPosition = new Vector2(0, 92);
        panelRect.sizeDelta = new Vector2(0, 340);
        var image = panel.GetComponent<Image>();
        if (image != null) { image.color = ReferenceUiVisuals.Panel; image.raycastTarget = true; }
        var outline = panel.GetComponent<Outline>();
        if (outline == null) outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = ReferenceUiVisuals.Border;
        outline.effectDistance = new Vector2(3, -3);
        var rule = ReferenceUiVisuals.Image(panel, "TopRule", new Vector2(0, 1), Vector2.one,
            Vector2.zero, new Vector2(0, 4), ReferenceUiVisuals.Gold);
        rule.transform.SetAsFirstSibling();
        SetAnchors(panel.Find("Name") as RectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.78f, 0.96f));
        SetAnchors(panel.Find("Level") as RectTransform, new Vector2(0.05f, 0.70f), new Vector2(0.94f, 0.81f));
        SetAnchors(panel.Find("Stats") as RectTransform, new Vector2(0.07f, 0.35f), new Vector2(0.93f, 0.69f));
        StyleText(panel, "Name", 22, ReferenceUiVisuals.Gold, true);
        StyleText(panel, "Level", 16, SoftGold, false);
        StyleText(panel, "Stats", 17, ReferenceUiVisuals.Light, false);
        var name = panel.Find("Name")?.GetComponent<TextMeshProUGUI>();
        if (name != null) name.alignment = TextAlignmentOptions.Left;
        var level = panel.Find("Level")?.GetComponent<TextMeshProUGUI>();
        if (level != null) level.alignment = TextAlignmentOptions.Left;
        ReferenceUiVisuals.Text(panel, "NextUnlock", "", 14, SoftGold,
            TextAlignmentOptions.Left, new Vector2(0.07f, 0.25f), new Vector2(0.93f, 0.34f),
            Vector2.zero, Vector2.zero, bodyBold: true);
        var close = panel.Find("CloseBtn") as RectTransform;
        if (close != null)
        {
            close.anchorMin = close.anchorMax = new Vector2(1, 1);
            close.pivot = Vector2.one;
            close.anchoredPosition = new Vector2(-16, -16);
            close.sizeDelta = new Vector2(44, 44);
            var closeImage = close.GetComponent<Image>();
            if (closeImage != null) { closeImage.color = ReferenceUiVisuals.Red; closeImage.raycastTarget = true; }
            var closeLabel = close.GetComponentInChildren<TextMeshProUGUI>();
            ReferenceUiVisuals.ApplyFont(closeLabel, 23, Color.white, true);
            if (closeLabel != null) closeLabel.raycastTarget = false;
        }
        var upgrade = panel.Find("UpBtn")?.GetComponent<Button>();
        if (upgrade != null)
        {
            SetAnchors(upgrade.transform as RectTransform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.23f));
            ReferenceUiVisuals.ApplyButtonSkin(upgrade, true);
            var label = upgrade.GetComponentInChildren<TextMeshProUGUI>();
            ReferenceUiVisuals.ApplyFont(label, 20, ReferenceUiVisuals.Hex(0x2A1A08), true);
            if (label != null) label.raycastTarget = false;
        }
    }

    private static void StyleDockButton(RectTransform rt, Vector2 anchor, bool gold, string label)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(160, 58);
        var button = rt.GetComponent<Button>();
        if (button != null) ReferenceUiVisuals.ApplyButtonSkin(button, gold);
        var icon = rt.Find("Icon") as RectTransform;
        if (icon != null)
        {
            SetAnchors(icon, new Vector2(0.07f, 0.13f), new Vector2(0.35f, 0.87f));
            var image = icon.GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
        }
        var text = rt.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = label;
            SetAnchors(text.rectTransform, new Vector2(0.34f, 0), new Vector2(0.96f, 1));
            ReferenceUiVisuals.ApplyFont(text, 15,
                gold ? ReferenceUiVisuals.Hex(0x2A1A08) : ReferenceUiVisuals.Light, true);
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = 15;
        }
    }

    private static void StyleText(Transform parent, string childName, float size, Color color, bool title)
    {
        if (parent == null) return;
        foreach (var text in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name != childName) continue;
            ReferenceUiVisuals.ApplyFont(text, size, color, title, !title);
        }
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        if (rt == null) return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Sprite MakeVignette()
    {
        const int width = 128;
        const int height = 256;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
            float ny = Mathf.Abs((y + 0.5f) / height * 2f - 1f);
            float edge = Mathf.Max(nx, ny * 0.72f);
            float alpha = Mathf.SmoothStep(0f, 0.35f, Mathf.InverseLerp(0.65f, 1f, edge));
            texture.SetPixel(x, y, new Color(0, 0, 0, alpha));
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }
}
