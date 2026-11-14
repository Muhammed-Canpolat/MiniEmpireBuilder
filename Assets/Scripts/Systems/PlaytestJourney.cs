#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SON aşama: yeni hesapla baştan oynatma (sınırsız elmas KAPALI, geliştirici ayarları kapalı). Gerçek akışlar:
/// StartNewGame → Ana Üs (maden topla, yükselt, kahraman/çırak, 2. inşaatçı) → SEFER (radyal düğme) → savaş (PlaytestBot,
/// oyuncunun kendi kaydıyla) → üsse dönüş … Ana Üs 5 → ADA (radyal düğme, bulut geçişi, ad penceresi, Akın 1, Ada Tüccarı)
/// → Ana Üs → SEFER. Zaman: savaşın oyun süresi + oturum araları <see cref="GameManager.DevAdvanceTime"/> ile.
/// Çıktı: PlaytestOutput/journey/journey.md + ekran görüntüleri; özet <see cref="Last"/>.
/// </summary>
public static class PlaytestJourney
{
    public sealed class Report
    {
        public readonly List<string> lines = new List<string>();
        public readonly List<string> problems = new List<string>();
        public int battles, wins, finalMainBase, finalHero, finalGold, gemsSpent, finalStars;
        public bool unlimitedGemsOff, islandVisited, namePrompted, akinWon, traderBought, campaignPanelSeen;
        public double simulatedMinutes;
        public string lastStage = "";
    }

    public static Report Last { get; private set; }
    public static string OutputDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/journey"));

    private static Report report;
    private static double simMinutes;
    private static int shot;

    private static void Note(string line)
    {
        string stamp = $"[{FormatSim(simMinutes)}] ";
        report.lines.Add(stamp + line);
        Debug.Log("[Journey] " + stamp + line);
    }

    private static string FormatSim(double minutes) =>
        minutes < 60 ? $"{minutes:0} dk" : $"{(int)(minutes / 60)} sa {(int)(minutes % 60):00} dk";

    private static string Wallet(GameManager gm) =>
        $"altın {gm.Gold}/{gm.GetMaxGoldCapacity()} · elmas {gm.PlayerData.gems} · maden deposu {gm.GoldMineStored}";

    public static IEnumerator Run(int maxBattlesBeforeIsland = 16, int battlesAfterIsland = 3)
    {
        report = new Report();
        Last = report;
        IslandNameUi.OfferInDev = true;   // 6. oturum: ilk giriş akışı (tanıtım + ad + öğretici) gerçek oyuncudaki gibi
        FakeStoreBackend.ClearPending();
        simMinutes = 0;
        shot = 0;
        Directory.CreateDirectory(OutputDir);
        bool devWas = DevSettings.Enabled, gemsWas = DevSettings.UnlimitedGems;
        DevSettings.UnlimitedGems = false;
        DevSettings.Enabled = false;   // gerçek oyuncu: geliştirici kısayolları ve sınırsız elmas yok
        report.unlimitedGemsOff = !DevSettings.UnlimitedGems;
        try
        {
            var gm = GameManager.Instance;
            if (gm == null) gm = new GameObject("JourneyGameManager").AddComponent<GameManager>();
            yield return null;
            BattleCards.ResetForTests();
            gm.StartNewGame(WeaponType.Axe);
            yield return WaitScene("BaseScene");
            Note($"Yeni hesap: Ana Üs {gm.GetMainBaseLevel()} · {Wallet(gm)} · ekonomi profili {Economy.Active.profileName}");
            int gemsStart = gm.PlayerData.gems;
            yield return Capture("01_base_fresh");

            // ---- 1. bölüm: Ana Üs ↔ Sefer döngüsü, ada açılana kadar ----
            int seed = 0;
            while (gm.GetMainBaseLevel() < Progression.Island.UnlockMainBaseLevel && report.battles < maxBattlesBeforeIsland)
            {
                BaseTurn(gm);
                yield return Battle(gm, ++seed);
                if (report.battles % 4 == 0) Break(gm, 120, "oturum arası");
            }
            BaseTurn(gm);
            // Son yükseltme sürüyorsa bitene kadar bekle (oyuncu ertesi oturumda döner).
            while (gm.GetMainBaseLevel() < Progression.Island.UnlockMainBaseLevel && gm.BuilderTasks.Count > 0)
            {
                Break(gm, Math.Max(1, (int)Math.Ceiling(gm.BuilderSecondsRemaining / 60d)), "Ana Üs inşaatı bekleniyor");
                BaseTurn(gm);
            }
            if (SceneManager.GetActiveScene().name != "BaseScene") { gm.LoadBaseWorld(); yield return WaitScene("BaseScene"); }
            yield return Capture("02_base_mb" + gm.GetMainBaseLevel());

            // ---- 2. bölüm: Ada (radyal ADA düğmesi) ----
            if (gm.GetMainBaseLevel() >= Progression.Island.UnlockMainBaseLevel)
                yield return IslandVisit(gm);
            else report.problems.Add($"{maxBattlesBeforeIsland} savaşta Ana Üs 5'e ulaşılamadı (Ana Üs {gm.GetMainBaseLevel()})");

            // ---- 3. bölüm: SEFER düğmesiyle haritaya, aşama paneli, birkaç savaş ----
            if (SceneManager.GetActiveScene().name != "BaseScene") { gm.LoadBaseWorld(); yield return WaitScene("BaseScene"); }
            BaseTurn(gm);
            if (!ClickHud("BattleBtn")) { report.problems.Add("SEFER düğmesi bulunamadı"); gm.LoadCampaignMap(); }
            yield return WaitScene("CampaignScene");
            yield return Capture("05_campaign_map");
            yield return OpenStagePanel();
            for (int i = 0; i < battlesAfterIsland; i++)
            {
                yield return Battle(gm, ++seed);
                BaseTurn(gm);
            }
            if (SceneManager.GetActiveScene().name != "BaseScene") { gm.LoadBaseWorld(); yield return WaitScene("BaseScene"); }
            yield return Capture("07_base_end");

            report.finalMainBase = gm.GetMainBaseLevel();
            report.finalHero = gm.PlayerData.hero.level;
            report.finalGold = gm.Gold;
            report.finalStars = CampaignProgress.TotalStars();
            report.gemsSpent = gemsStart - gm.PlayerData.gems;   // negatif: kazanılan elmas harcanandan fazla
            report.simulatedMinutes = simMinutes;
            Note($"BİTİŞ: Ana Üs {report.finalMainBase} · kahraman {report.finalHero} · çırak {gm.PlayerData.apprenticeLevel} · " +
                 $"{report.wins}/{report.battles} zafer · yıldız {report.finalStars} · {Wallet(gm)}");
        }
        finally
        {
            Time.timeScale = 1f;
            DevSettings.Enabled = devWas;
            DevSettings.UnlimitedGems = gemsWas;
            WriteReport();
        }
    }

    // ==================== ANA ÜS ====================

    private static readonly BuildingType[] UpgradeOrder =
        { BuildingType.MainBase, BuildingType.GoldMine, BuildingType.ArcherTower, BuildingType.CannonTower, BuildingType.Monastery };

    /// <summary>Oyuncu üste: madeni toplar, boş inşaatçıya iş verir (Ana Üs önce), kalanla kahraman/çırak; 2. inşaatçı.</summary>
    private static void BaseTurn(GameManager gm)
    {
        int collected = gm.CollectGoldFromMineStorage();
        var did = new List<string>();
        if (collected > 0) did.Add($"maden +{collected}");
        if (gm.CanHireBuilder() == GameManager.HireBlock.None && gm.HireBuilder()) did.Add("2. inşaatçı alındı");
        int nextMb = Progression.UpgradeCost(BuildingType.MainBase, Mathf.Min(gm.GetMainBaseLevel() + 1, Progression.MaxLevel));
        for (int guard = 0; guard < 4 && gm.HasFreeBuilder; guard++)
        {
            bool started = false;
            foreach (var type in UpgradeOrder)
            {
                if (!gm.Buildings.TryGetValue(type, out var data) || !data.isUnlocked) continue;
                // Ana Üs için yedek: başka işe Ana Üs bedelinin yarısı kalacak kadar harcamaz (sim politikası).
                int cost = Progression.UpgradeCost(type, data.level + 1);
                if (type != BuildingType.MainBase && gm.Gold - cost < nextMb / 2 && gm.GetMainBaseLevel() < Progression.MaxLevel
                    && gm.CanUpgrade(null, BuildingType.MainBase) != GameManager.UpgradeBlock.MaxLevel) continue;
                if (gm.UpgradeBuilding(type)) { did.Add($"{type} → {data.level + 1} ({cost})"); started = true; break; }
            }
            if (!started) break;
        }
        for (int guard = 0; guard < 5; guard++)
        {
            var hero = gm.PlayerData.hero;
            if (hero.level < gm.GetHeroMaxLevel() && gm.Gold - gm.GetHeroUpgradeCost() >= nextMb / 2)
            { int c = gm.GetHeroUpgradeCost(); if (gm.UpgradeHero()) { did.Add($"kahraman → {hero.level} ({c})"); continue; } }
            if (gm.PlayerData.apprenticeLevel < gm.GetApprenticeMaxLevel() && gm.Gold - gm.GetApprenticeUpgradeCost() >= nextMb / 2)
            { int c = gm.GetApprenticeUpgradeCost(); if (gm.UpgradeApprentice()) { did.Add($"çırak → {gm.PlayerData.apprenticeLevel} ({c})"); continue; } }
            break;
        }
        simMinutes += 0.5;
        Note($"Üs: {(did.Count > 0 ? string.Join(", ", did) : "harcanacak bir şey yok")} · Ana Üs {gm.GetMainBaseLevel()} · {Wallet(gm)}" +
             (gm.BuilderTasks.Count > 0 ? $" · inşaat {Progression.FormatDuration(gm.BuilderSecondsRemaining)}" : ""));
    }

    /// <summary>Oturum arası / bekleme: kayıttaki tüm süreler ileri alınır (çevrimdışı maden, inşaat, can).</summary>
    private static void Break(GameManager gm, int minutes, string why)
    {
        gm.DevAdvanceTime(TimeSpan.FromMinutes(minutes));
        simMinutes += minutes;
        Note($"— {minutes} dk ara ({why}) · {Wallet(gm)}");
    }

    // ==================== SAVAŞ ====================

    private static (int region, int level) NextStage()
    {
        (int, int) best = (1, 1);
        foreach (var region in CampaignCatalog.Regions)
        {
            if (region == null || !CampaignProgress.IsRegionOpen(region)) continue;
            int level = CampaignProgress.FirstIncomplete(region);
            best = (region.id, level);
            if (CampaignProgress.Stars(region.id, level) == 0) return best;
        }
        return best;   // hepsi bitti ya da sıradaki bölge kilitli: son açık bölgenin 5. aşaması (tekrar)
    }

    private static IEnumerator Battle(GameManager gm, int seed)
    {
        while (!LivesSystem.CanPlay) Break(gm, LivesSystem.RegenMinutes, "can yok");
        var (region, level) = NextStage();
        int goldBefore = gm.Gold, livesBefore = LivesSystem.Lives;
        bool replay = CampaignProgress.Stars(region, level) > 0;
        yield return PlaytestBot.PlayBattle(region, level, 4f, 600f, false, 9000 + seed, false, "combined", true);
        var t = PlaytestBot.Last;
        if (GemConfirm.IsOpen) GemConfirm.Close(true);   // elmasla devam teklifini oyuncu reddeder → yenilgi
        for (int i = 0; i < 10; i++) yield return null;
        var bm = BattleManager.Instance;
        int payout = bm != null && t.result == "win" ? bm.LastPayout : 0;
        report.battles++;
        if (t.result == "win") report.wins++;
        simMinutes += t.durationSec / 60d + 1d;   // savaş + menüler
        report.lastStage = $"B{region}-{level}";
        Note($"Savaş B{region}-{level}{(replay ? " (tekrar)" : "")}: {t.result} · {t.durationSec:0} sn · dalga {t.wavesReached} · " +
             $"kale %{t.baseHealthEnd * 100f:0} · savaş altını {t.goldEarned} · ödeme +{payout} · cüzdan {goldBefore}→{gm.Gold} · can {livesBefore}→{LivesSystem.Lives}");
        if (t.result == "setup_failed" || t.result == "no_lives") report.problems.Add($"B{region}-{level}: {t.result}");
        if (t.errors.Count > 0) report.problems.Add($"B{region}-{level} hata günlüğü: {t.errors[0]}");
        if (report.battles == 1) yield return Capture("03_first_battle_end");
        gm.LoadBaseWorld();
        yield return WaitScene("BaseScene");
    }

    // ==================== ADA ====================

    private static IEnumerator IslandVisit(GameManager gm)
    {
        if (!ClickHud("IslandBtn")) { report.problems.Add("ADA düğmesi bulunamadı"); gm.LoadIsland(); }
        float start = Time.realtimeSinceStartup;
        while ((IslandSceneSetup.Instance == null || SceneManager.GetActiveScene().name != "BattleScene") && Time.realtimeSinceStartup - start < 15f)
            yield return null;
        for (float t = 0; t < 3f; t += Time.unscaledDeltaTime) yield return null;   // bulut geçişi açılsın
        var setup = IslandSceneSetup.Instance;
        if (setup == null) { report.problems.Add("Ada sahnesi kurulamadı"); yield break; }
        report.islandVisited = true;
        simMinutes += 1;
        var island = IslandSession.Save;
        // 6. oturum (3.1): ilk giriş tanıtımı → ad penceresi
        for (float t = 0; t < 4f && IslandIntro.Current == null && !IslandNameUi.IsOpen; t += Time.unscaledDeltaTime) yield return null;
        if (IslandIntro.Current != null)
        {
            report.problems.Remove("tanıtım yok");
            for (float t = 0; t < 2.6f; t += Time.unscaledDeltaTime) yield return null;
            yield return Capture("04_island_intro");
            Note("ADA: ilk giriş tanıtımı gösterildi (bulutlar açıldı, hikâye kartı)");
            IslandIntro.Current.Finish();
            for (float t = 0; t < 1.5f && !IslandNameUi.IsOpen; t += Time.unscaledDeltaTime) yield return null;
        }
        else report.problems.Add("ADA: ilk giriş tanıtımı görünmedi");
        Note($"ADA: bina {island.placed?.Count ?? 0} · Odun {island.wood} · Et {island.meat} · okçu {(setup.Archer != null && setup.Archer.gameObject.activeSelf ? "VAR" : "yok")} · ad penceresi {(IslandNameUi.IsOpen ? "AÇIK" : "yok")}");
        if (IslandNameUi.IsOpen)
        {
            report.namePrompted = true;
            yield return Capture("04a_island_name_prompt");
            var ui = IslandNameUi.Current;
            var fieldInfo = typeof(IslandNameUi).GetField("field", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var input = fieldInfo?.GetValue(ui) as TMPro.TMP_InputField;
            if (input != null) input.text = "Kartal Adası";
            bool applied = ui.TryApply();
            Note($"Ada adı 'Kartal Adası': {(applied ? "kaydedildi (bedava)" : "REDDEDİLDİ")} · elmas {gm.PlayerData.gems}");
            if (!applied) report.problems.Add("Ada adı kaydedilemedi");
            yield return null;
        }
        yield return Capture("04b_island_fresh");

        // 6. oturum (2.4): dükkân — sahte mağazayla elmas paketi + elmasla kaynak paketi (gerçek akış: kayıt + defter)
        {
            int gems0 = gm.Gems;
            bool started = IapService.Instance.Buy("gems_small");
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime) yield return null;
            Note($"DÜKKÂN: sahte mağazayla 'gems_small' → elmas {gems0} → {gm.Gems} ({(started && gm.Gems == gems0 + IapService.GemsFor("gems_small") ? "verildi" : "SORUN")})");
            if (!(started && gm.Gems == gems0 + IapService.GemsFor("gems_small"))) report.problems.Add("IAP elmas verilmedi");
            int wood0 = IslandEconomy.Wood;
            var res = ShopService.Buy(ShopResource.Wood, 0);
            Note($"DÜKKÂN: Odun Kesesi ({ShopService.PackGems(ShopResource.Wood, 0)} elmas) → {res} · Odun {wood0} → {IslandEconomy.Wood}");
        }

        // 6. oturum (3.2): zorunlu Okçuluk öğreticisi — İNŞA ET → Okçuluk Alanı → yerleştir → inşa → okçu çıkar
        for (float t = 0; t < 3f && IslandBuildTutorial.Current == null; t += Time.unscaledDeltaTime) yield return null;
        if (IslandBuildTutorial.Current != null)
        {
            yield return Capture("04b2_tutorial");
            IslandBuildTutorial.Current.ActionButton.onClick.Invoke();
            yield return null;
            var spot = IslandLayout.Building(IslandLayout.BuildingKind.Archery);
            var entry = IslandEconomy.TryStartBuild(IslandLayout.BuildingKind.Archery, spot.col, spot.row, false, out string why);
            Note($"Öğretici: Okçuluk Alanı {(entry != null ? "ücretsiz kuruldu" : "KURULAMADI: " + why)}");
            if (entry == null) report.problems.Add("Okçuluk Alanı kurulamadı: " + why);
            for (float t = 0; t < Progression.Island.PlaceSeconds + 1.5f; t += Time.unscaledDeltaTime) { IslandEconomy.Tick(IslandEconomy.Now); yield return null; }
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime) yield return null;
            bool archer = setup.Archer != null && setup.Archer.gameObject.activeSelf;
            Note($"Öğretici bitti: okçu {(archer ? "binadan çıktı" : "YOK")} · Kıyı Kalesi Sv{IslandSession.KeepLevel()} · Okçu Sv{IslandSession.ArcherLevel()}");
            if (!archer) report.problems.Add("Okçu öğreticiden sonra görünmedi");
            yield return Capture("04b3_archer_out");
        }
        else Note("ADA: öğretici yok (Okçuluk zaten var)");

        var raid = setup.Raid;
        for (int akin = 1; akin <= 3 && raid != null && raid.CanStart(akin); akin++)
        {
            IslandBot.Attach(setup, IslandBot.Profile.Good);
            raid.StartAkin(akin);
            Time.timeScale = 6f;
            float s0 = Time.time;
            while (raid.Phase == IslandRaidManager.State.Running && Time.time - s0 < 600f) yield return null;
            Time.timeScale = 1f;
            simMinutes += (Time.time - s0) / 60d;
            Note($"Akın {akin} (Kale Sv{IslandSession.KeepLevel()}, Okçu Sv{IslandSession.ArcherLevel()}): {raid.Phase} · kale %{raid.CastleHealthPercent * 100f:0}");
            if (akin == 1) report.akinWon = raid.Phase == IslandRaidManager.State.Won;
            if (raid.Phase != IslandRaidManager.State.Won) { raid.ReturnToIdle(); yield return null; break; }
            raid.ReturnToIdle();
            yield return null;
        }
        if (raid == null || !report.akinWon) report.problems.Add("Akın 1 kazanılamadı/başlatılamadı");

        // Ada Tüccarı (AŞAMA 4, V2): Et paketi.
        if (IslandEconomy.TraderEnabled)
        {
            int meat0 = IslandEconomy.Meat, gold0 = gm.Gold, cost = IslandEconomy.TraderPackCost;
            ResourceTopBar.ShowTrader();
            yield return null;
            yield return Capture("04d_island_trader");
            bool bought = IslandEconomy.TryBuyTraderPack(true, out string reason);
            ModeLaunchPanel.Close();
            report.traderBought = bought;
            Note($"Ada Tüccarı: {(bought ? $"50 Et alındı ({cost} altın) · Et {meat0}→{IslandEconomy.Meat} · altın {gold0}→{gm.Gold}" : "alınamadı: " + reason)}");
        }
        gm.LoadBaseWorld();
        yield return WaitScene("BaseScene");
        for (float t = 0; t < 2f; t += Time.unscaledDeltaTime) yield return null;
    }

    // ==================== SEFER PANELİ ====================

    private static IEnumerator OpenStagePanel()
    {
        var setup = UnityEngine.Object.FindFirstObjectByType<CampaignSceneSetup>();
        var (region, level) = NextStage();
        var regionData = CampaignCatalog.Get(region);
        if (setup == null || regionData == null) { report.problems.Add("Sefer haritası bulunamadı"); yield break; }
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(CampaignSceneSetup).GetMethod("ShowRegion", flags)?.Invoke(setup, new object[] { regionData });
        yield return null;
        typeof(CampaignSceneSetup).GetMethod("SelectLevel", flags)?.Invoke(setup, new object[] { level });
        for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) yield return null;
        var meta = GameObject.Find("Meta");
        var cards = GameObject.Find("Card_" + (BattleCard)0);
        report.campaignPanelSeen = meta != null && cards != null;
        Note($"Sefer paneli B{region}-{level}: {(meta != null ? meta.GetComponent<TMPro.TextMeshProUGUI>()?.text : "Meta yok")} · kartlar {(cards != null ? "var" : "YOK")}");
        yield return Capture("06_campaign_stage_panel");
    }

    // ==================== YARDIMCILAR ====================

    private static bool ClickHud(string name)
    {
        var go = GameObject.Find(name);
        var button = go != null ? go.GetComponent<Button>() : null;
        if (button == null)
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.name == name) { button = b; break; }
        if (button == null) return false;
        button.onClick.Invoke();
        return true;
    }

    private static IEnumerator WaitScene(string scene)
    {
        float start = Time.realtimeSinceStartup;
        // Bulut geçişi asenkron yükler: önceki sahne hâlâ etkinken "yüklendi" sanılmasın (geçiş bitene kadar bekle).
        while ((SceneManager.GetActiveScene().name != scene || SceneCloudTransition.Active != null) &&
               Time.realtimeSinceStartup - start < 20f) yield return null;
        for (int i = 0; i < 30; i++) yield return null;
        if (SceneManager.GetActiveScene().name != scene) report.problems.Add($"{scene} yüklenmedi (etkin: {SceneManager.GetActiveScene().name})");
    }

    private static IEnumerator Capture(string name)
    {
        yield return null;
        var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (camera == null) yield break;
        const int w = 540, h = 1170;
        var texture = new RenderTexture(w, h, 24);
        var rect = camera.rect;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        var changed = new List<(Canvas canvas, RenderMode mode, Camera cam, int order, bool over)>();
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            changed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.sortingOrder, canvas.overrideSorting));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000 + canvas.sortingOrder;
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(OutputDir, $"SON_{name}.png"), pixels.EncodeToPNG());
        UnityEngine.Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.rect = rect;
        camera.ResetAspect();
        texture.Release();
        UnityEngine.Object.Destroy(texture);
        foreach (var (canvas, mode, cam, order, over) in changed)
        {
            if (canvas == null) continue;
            canvas.renderMode = mode; canvas.worldCamera = cam; canvas.sortingOrder = order; canvas.overrideSorting = over;
        }
        shot++;
    }

    private static void WriteReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SON — yeni hesapla baştan oynatma (otomatik)");
        sb.AppendLine();
        sb.AppendLine($"- Sınırsız elmas kapalı: {report.unlimitedGemsOff} · ekonomi profili: {Economy.Active.profileName}");
        sb.AppendLine($"- Savaş: {report.wins}/{report.battles} zafer · son aşama {report.lastStage} · yıldız {report.finalStars}");
        sb.AppendLine($"- Bitiş: Ana Üs {report.finalMainBase} · kahraman {report.finalHero} · altın {report.finalGold} · simüle süre {FormatSim(report.simulatedMinutes)}");
        sb.AppendLine($"- Ada: ziyaret {report.islandVisited} · ad penceresi {report.namePrompted} · Akın 1 {report.akinWon} · Tüccar {report.traderBought} · sefer paneli {report.campaignPanelSeen}");
        sb.AppendLine($"- Sorunlar: {(report.problems.Count == 0 ? "yok" : string.Join(" | ", report.problems))}");
        sb.AppendLine();
        sb.AppendLine("## Günlük");
        foreach (var line in report.lines) sb.AppendLine("- " + line);
        File.WriteAllText(Path.Combine(OutputDir, "journey.md"), sb.ToString());
    }
}
#endif
