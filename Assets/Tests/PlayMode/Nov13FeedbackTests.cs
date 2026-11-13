using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// 13 Kasım oyun testi geri bildirimi: Akın sonucunda ADAYA DÖN (bulutlu), adada radyal menü (İNŞA ET · AKIN · KADRO, Akın pulu),
/// kompakt akın sayacı, bölge–hücre eşlemesi ve bulut kapsaması (görselleştirme PNG'si), ada kadrajı (4 en-boy), Ana Üs alt barı
/// (arma, hizalı rozet, artık yazı yok), üst bar veri kaynağı ve ada → akın → adaya dön → Ana Üs → ada akışı.
/// Görüntüler <c>PlaytestOutput/feedback13/*.png</c>. Yalıtılmış test kaydı (gerçek kayıt geri yüklenir).
/// </summary>
public class Nov13FeedbackTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private const string RadialKey = "MEB_RadialOpen", IslandRadialKey = "MEB_IslandRadialOpen";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private int? savedRadial, savedIslandRadial;
    private bool devWas;

    private static readonly (string name, int w, int h)[] Aspects =
    {
        ("16x9", 1080, 1920), ("19_5x9", 1080, 2340), ("20x9", 1080, 2400), ("tablet_3x4", 1536, 2048),
    };

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
        savedRadial = PlayerPrefs.HasKey(RadialKey) ? PlayerPrefs.GetInt(RadialKey) : (int?)null;
        savedIslandRadial = PlayerPrefs.HasKey(IslandRadialKey) ? PlayerPrefs.GetInt(IslandRadialKey) : (int?)null;
        devWas = (bool)T("DevSettings").GetProperty("Enabled").GetValue(null);
    }

    [TearDown]
    public void RestoreRealSave()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        void PutInt(string key, int? value) { if (value.HasValue) PlayerPrefs.SetInt(key, value.Value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PutInt(RadialKey, savedRadial);
        PutInt(IslandRadialKey, savedIslandRadial);
        PlayerPrefs.Save();
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        Call("IslandNameUi", "Close");
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        PlayerPrefs.Save();
    }

    private static string L(string tr, string en) => (string)T("L").GetMethod("T").Invoke(null, new object[] { tr, en });
    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);
    private static Transform IslandSafe => GameObject.Find("IslandCanvas").transform.Find("SafeArea");
    private static object Hud => Prop(Setup, "Hud");
    private static object Raid => Prop(Setup, "Raid");

    private static string TextOf(Component c)
    {
        var tmp = c.GetComponent("TextMeshProUGUI");
        return (string)tmp.GetType().GetProperty("text").GetValue(tmp);
    }

    private static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    private static IEnumerable<object> RadialItems(Component menu) => ((IEnumerable)Prop(menu, "Items")).Cast<object>();
    private static object RadialItem(Component menu, string id) => RadialItems(menu).First(i => (string)Field(i, "id") == id);
    private static void ClickRadial(Component menu, string id) =>
        ((UnityEngine.UI.Button)Field(RadialItem(menu, id), "button")).onClick.Invoke();

    // ==================== 4. BÖLGE – HÜCRE EŞLEMESİ ====================

    private static int Zone(int c, int r) => (int)Call("IslandLayout", "ZoneAt", c, r);
    private static bool Land(int c, int r) => (bool)Call("IslandLayout", "IsLand", c, r);
    private const int W = 22, H = 36;

    private static List<List<Vector2Int>> LandComponents()
    {
        var seen = new bool[W, H];
        var result = new List<List<Vector2Int>>();
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
            {
                if (seen[c, r] || !Land(c, r)) continue;
                var comp = new List<Vector2Int>();
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(c, r)); seen[c, r] = true;
                while (stack.Count > 0)
                {
                    var p = stack.Pop(); comp.Add(p);
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var n = p + d;
                        if (n.x < 0 || n.y < 0 || n.x >= W || n.y >= H || seen[n.x, n.y] || !Land(n.x, n.y)) continue;
                        seen[n.x, n.y] = true; stack.Push(n);
                    }
                }
                result.Add(comp);
            }
        return result;
    }

    [Test]
    public void EveryIsletBelongsWhollyToOneZoneAndNorthwestIsletIsOpen()
    {
        var comps = LandComponents().OrderByDescending(c => c.Count).ToList();
        Assert.AreEqual(5, comps.Count, "ana ada + 4 adacık");
        foreach (var islet in comps.Skip(1))
        {
            var zones = islet.Select(p => Zone(p.x, p.y)).Distinct().ToList();
            Assert.AreEqual(1, zones.Count, $"adacık {islet[0]} tek bölgede olmalı (bulunan: {string.Join(",", zones)})");
        }
        // Kullanıcının hatası: KADRO düğmesinin altındaki NW adacığı "Altın Burnu" açılmadan kurulamıyordu
        foreach (var cell in new[] { new Vector2Int(1, 4), new Vector2Int(2, 4), new Vector2Int(1, 5), new Vector2Int(2, 5), new Vector2Int(1, 6) })
            Assert.AreEqual(0, Zone(cell.x, cell.y), $"NW adacığı {cell} baştan açık (Z0)");
        var island = NewIsland(15);
        Placed(island).Clear();
        Assert.AreNotEqual("Locked", Check("Tower", 1, 4, island, 1, null, 15), "NW adacığına yalnız Z0 açıkken kule kurulabilir");
        Assert.AreEqual("Locked", Check("Tower", 19, 10, island, 1, null, 15), "E adacığı (Z1) kilitli kalır");
        Assert.AreNotEqual("Locked", Check("Tower", 19, 10, island, 1 | 2, null, 15), "Z1 açılınca E adacığı açılır");
        Assert.AreEqual("Locked", Check("Tower", 1, 29, island, 1 | 8, null, 15), "Altın Burnu (Z3) açmak SW adacığını açmaz");
    }

    [UnityTest]
    public IEnumerator ZoneCloudsCoverExactlyTheLockedCellsOfTheirOwnZone()
    {
        yield return LoadIsland(island => Set(island, "zoneMask", 1), 1, v => { });
        var cloudType = T("IslandZoneCloud");
        var clouds = Object.FindObjectsByType(cloudType, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().ToList();
        Assert.Greater(clouds.Count, 20, "bölge bulutları pişirilmiş");
        var covers = cloudType.GetMethod("CoreCovers");
        var cellCenter = cloudType.GetMethod("CellCenter");
        int CloudZone(Component cl) => (int)cloudType.GetField("zone").GetValue(cl);
        Vector2 Anchor(Component cl) => (Vector2)cloudType.GetField("anchor").GetValue(cl);
        bool Covers(Component cl, Vector2 p) => (bool)covers.Invoke(null, new object[] { Anchor(cl), cl.transform.localScale.x, p });

        var problems = new List<string>();
        var coveredBy = new int[W, H];
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
            {
                coveredBy[c, r] = -1;
                if (!Land(c, r)) continue;
                int zone = Zone(c, r);
                var p = (Vector2)cellCenter.Invoke(null, new object[] { c, r });
                var hits = clouds.Where(cl => Covers(cl, p)).ToList();
                if (hits.Count > 0) coveredBy[c, r] = CloudZone(hits[0]);
                if (zone > 0 && !hits.Any(cl => CloudZone(cl) == zone && cl.gameObject.activeSelf))
                    problems.Add($"Z{zone} hücresi ({c},{r}) kilitli ama bulutu yok");
                foreach (var cl in hits.Where(cl => CloudZone(cl) != zone))
                    problems.Add($"Z{CloudZone(cl)} bulutu {cl.name} başka bölgenin (Z{zone}) hücresini ({c},{r}) örtüyor");
            }
        WriteZoneMap(clouds, coveredBy, Anchor, CloudZone);
        Assert.IsEmpty(problems, string.Join("\n", problems.Take(30)));
        // Hepsi kilitli: her bulut görünür; bölge açılınca yalnız kendi bulutları kalkar
        Assert.IsTrue(clouds.All(cl => cl.gameObject.activeSelf), "Z0 dışındaki bütün bulutlar görünür");
        yield return Capture("F4_zones_all_locked", 1080, 1920);
        var island0 = CurrentIsland();
        Set(island0, "zoneMask", 1 | (1 << 3));
        Setup.GetType().GetMethod("RefreshZones").Invoke(Setup, null);
        yield return null;
        foreach (var cl in clouds)
            Assert.AreEqual(CloudZone(cl) != 3, cl.gameObject.activeSelf, $"{cl.name}: yalnız Z3 bulutları kalkar");
        Set(island0, "zoneMask", 63);
        Setup.GetType().GetMethod("RefreshZones").Invoke(Setup, null);
        yield return null;
        Assert.IsTrue(clouds.All(cl => !cl.gameObject.activeSelf), "hepsi açık: bulut yok");
    }

    /// <summary>Hata ayıklama görseli: hücre başına 20 px; renk = bölge, koyu = bulut çekirdeği örtüyor, beyaz nokta = bulut merkezi.</summary>
    private static void WriteZoneMap(List<Component> clouds, int[,] coveredBy, Func<Component, Vector2> anchor, Func<Component, int> zoneOf)
    {
        const int S = 20;
        var colors = new[]
        {
            new Color(0.55f, 0.8f, 0.45f), new Color(0.95f, 0.6f, 0.3f), new Color(0.4f, 0.6f, 0.95f),
            new Color(0.95f, 0.85f, 0.3f), new Color(0.75f, 0.45f, 0.85f), new Color(0.9f, 0.4f, 0.45f),
        };
        var tex = new Texture2D(W * S, H * S, TextureFormat.RGB24, false);
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
            {
                Color col = Land(c, r) ? colors[Zone(c, r)] : new Color(0.2f, 0.45f, 0.5f);
                bool covered = coveredBy[c, r] >= 0;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        bool border = x == 0 || y == 0;
                        bool hatch = covered && ((x + y) % 6 < 2);
                        var px = border ? col * 0.8f : hatch ? col * 0.55f : col;
                        tex.SetPixel(c * S + x, (H - 1 - r) * S + (S - 1 - y), px);
                    }
            }
        foreach (var cl in clouds)
        {
            var a = anchor(cl);   // dünya: x = sütun, y = H - satır
            int px = Mathf.RoundToInt(a.x * S), py = Mathf.RoundToInt(a.y * S);
            for (int y = -3; y <= 3; y++)
                for (int x = -3; x <= 3; x++)
                    if (px + x >= 0 && px + x < W * S && py + y >= 0 && py + y < H * S)
                        tex.SetPixel(px + x, py + y, x * x + y * y <= 4 ? Color.white : colors[zoneOf(cl)] * 0.4f);
        }
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Folder, "F4_zone_map_debug.png"), tex.EncodeToPNG());
        Object.Destroy(tex);
    }

    // ==================== 2–3. ADA RADYAL MENÜSÜ + AKIN SAYACI ====================

    [UnityTest]
    public IEnumerator IslandUsesRadialMenuWithAkinBadgeAndKadroAndNoOldButtons()
    {
        PlayerPrefs.DeleteKey(IslandRadialKey);   // ilk açılış: açık
        yield return LoadIsland(island => { Set(island, "highestAkin", 6); Set(island, "lastAkin", 7); }, 7, v => { });
        var safe = IslandSafe;
        Assert.IsNull(safe.Find("StaffButton"), "sol üstteki KADRO düğmesi kalktı");
        Assert.IsNull(GameObject.Find("AkinButton"), "ayrı AKIN düğmesi yok");
        Assert.IsNull(safe.Find("Dock/BuildButton"), "ayrı İNŞA ET düğmesi yok");
        var menu = (Component)Prop(Hud, "Radial");
        Assert.NotNull(menu, "adada radyal menü");
        CollectionAssert.AreEqual(new[] { "build", "raid", "staff" }, RadialItems(menu).Select(i => (string)Field(i, "id")).ToArray());
        Assert.IsTrue((bool)Prop(menu, "IsOpen"), "ilk açılışta açık");
        menu.GetType().GetMethod("RefreshBadges").Invoke(menu, null);
        Assert.AreEqual("7", (string)Prop(RadialItem(menu, "raid"), "InfoBadgeText"), "AKIN düğmesinde Akın numarası pulu");
        // Ana Üs ile aynı bileşen ve boyutlar: ana düğme 92, vurgulu 70, diğerleri 60
        Assert.AreEqual(92f, ((RectTransform)Prop(menu, "MainButton")).sizeDelta.x, 0.1f);
        Assert.AreEqual(70f, ((RectTransform)Prop(RadialItem(menu, "raid"), "Rect")).sizeDelta.x, 0.1f);
        Assert.AreEqual(60f, ((RectTransform)Prop(RadialItem(menu, "staff"), "Rect")).sizeDelta.x, 0.1f);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Capture("F2_island_radial_open", 1080, 1920);

        ClickRadial(menu, "raid");
        yield return null;
        Assert.IsTrue((bool)Prop(Hud, "BoardVisible"), "AKIN → Akın Panosu");
        Assert.IsFalse(safe.Find("Dock").gameObject.activeSelf, "pano açıkken menü gizli");
        yield return Capture("F2_island_raid_board", 1080, 1920);
        Hud.GetType().GetMethod("CloseBuildUi").Invoke(Hud, null);
        var board = Hud.GetType().GetField("board", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Hud);
        ((Action)board.GetType().GetField("ClosePressed").GetValue(board))();
        yield return null;
        Assert.IsTrue(safe.Find("Dock").gameObject.activeSelf, "pano kapanınca menü döner");

        ClickRadial(menu, "staff");
        yield return null;
        var buildUi = Prop(Hud, "BuildUi");
        Assert.IsTrue((bool)Prop(buildUi, "Open"), "KADRO → kadro paneli");
        Hud.GetType().GetMethod("CloseBuildUi").Invoke(Hud, null);
        yield return null;

        menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { false, true, true });
        yield return null;
        Assert.AreEqual(0, PlayerPrefs.GetInt(IslandRadialKey, 1), "ada menü durumu kendi anahtarında");
        yield return Capture("F2_island_radial_closed", 1080, 1920);
        ClickRadial(menu, "build");
        yield return null;
        Assert.IsTrue((bool)Prop(buildUi, "Open"), "İNŞA ET → inşa menüsü");
        Hud.GetType().GetMethod("CloseBuildUi").Invoke(Hud, null);
    }

    [UnityTest]
    public IEnumerator RaidPillIsCompactAndExplainsItselfOnTap()
    {
        long now = DateTime.UtcNow.Ticks;
        yield return LoadIsland(island =>
        {
            Set(island, "nextRaidTicks", now + TimeSpan.FromHours(14).Ticks + TimeSpan.FromMinutes(47.5).Ticks);
            Set(island, "raidSeenTicks", now);
        }, 5, v => { });
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => now));
        yield return new WaitForSecondsRealtime(0.7f);
        var pill = (RectTransform)IslandSafe.Find("RaidBadge");
        Assert.IsTrue(pill.gameObject.activeSelf);
        Assert.NotNull(pill.Find("ClockIcon"), "kum saati ikonu");
        Assert.AreEqual(L("14s 48dk", "14h 48m"), TextOf(pill.Find("Label")), "kısa biçim");
        Assert.LessOrEqual(pill.rect.width, 110f, "hap küçük (eskiden 188)");
        Assert.LessOrEqual(pill.rect.height, 28f);
        pill.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        var banner = IslandSafe.GetComponentsInChildren<Component>(true).Where(c => c.GetType().Name == "TextMeshProUGUI")
            .Select(TextOf).FirstOrDefault(t => t.StartsWith(L("Sonraki akına kalan süre", "Time until the next raid")));
        Assert.NotNull(banner, "dokununca açıklama");
        yield return Capture("F3_raid_pill_tap", 1080, 1920);
    }

    // ==================== 1. AKIN SONUCU → ADAYA DÖN ====================

    [UnityTest]
    public IEnumerator RaidResultOffersReturnToIslandWithCloudTransition()
    {
        yield return LoadIsland(null, 3, v => { });
        var raid = Raid;
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        yield return new WaitForSecondsRealtime(0.3f);
        raid.GetType().GetMethod("Win", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(raid, null);
        yield return null;
        yield return AssertResultReturnsToIsland("F1_result_victory");
        // Yenilgide de aynı seçenek
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        yield return new WaitForSecondsRealtime(0.3f);
        var castle = (Component)Prop(Setup, "CastleStructure");
        castle.GetType().GetMethod("TakeDamage", new[] { typeof(float) })?.Invoke(castle, new object[] { 1e6f });
        float until = Time.realtimeSinceStartup + 3f;
        while (Prop(raid, "Phase").ToString() == "Running" && Time.realtimeSinceStartup < until) yield return null;
        if (Prop(raid, "Phase").ToString() == "Lost") yield return AssertResultReturnsToIsland("F1_result_defeat");
        else Debug.Log("[Nov13] Yenilgi zorlanamadı (TakeDamage imzası); yalnız zafer yolu denetlendi.");
    }

    private IEnumerator AssertResultReturnsToIsland(string shot)
    {
        var safe = IslandSafe;
        var panel = safe.Find("ResultPanel");
        Assert.IsTrue(panel.gameObject.activeSelf, "sonuç ekranı");
        var back = panel.Find("Card/ReturnIsland");
        Assert.NotNull(back, "ADAYA DÖN düğmesi");
        Assert.IsNull(panel.Find("Card/Home"), "ANA ÜS seçeneği sonuç ekranında yok");
        Assert.AreEqual(L("ADAYA DÖN", "BACK TO ISLE"), TextOf(back.GetComponentsInChildren<Component>().First(c => c.GetType().Name == "TextMeshProUGUI")));
        yield return Capture(shot, 1080, 1920);
        var transition = T("SceneCloudTransition");
        int before = (int)transition.GetProperty("CompletedCount").GetValue(null);
        back.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        Assert.NotNull(transition.GetProperty("Active").GetValue(null), "bulut örtüsü başladı");
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((int)transition.GetProperty("CompletedCount").GetValue(null) == before && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.AreEqual(before + 1, (int)transition.GetProperty("CompletedCount").GetValue(null), "geçiş tamamlandı");
        Assert.AreEqual("Idle", Prop(Raid, "Phase").ToString(), "ada görünümünde");
        Assert.IsFalse((bool)Prop(Setup, "InDefense"), "savunma kapandı");
        Assert.AreEqual("BattleScene", SceneManager.GetActiveScene().name, "sahne değişmedi (ada)");
        Assert.IsTrue((bool)T("IslandSession").GetProperty("Active").GetValue(null), "ada oturumu sürüyor");
        Assert.IsFalse(panel.gameObject.activeSelf);
        Assert.IsTrue(safe.Find("Dock").gameObject.activeSelf, "radyal menü geri geldi");
        yield return Capture(shot + "_back_on_island", 1080, 1920);
    }

    // ==================== 5. ADA KADRAJI ====================

    [UnityTest]
    public IEnumerator IslandFrameShowsNorthTreesAndSouthIsletsAtAllAspects()
    {
        PlayerPrefs.SetInt(IslandRadialKey, 0);
        yield return LoadIsland(null, 5, v => { });
        var camera = (Camera)Prop(Setup, "WorldCamera");
        var controller = Prop(Setup, "CameraController");
        float top = (float)Field(Config, "islandViewTopHudFraction"), bottom = (float)Field(Config, "islandViewBottomHudFraction");
        float treeTop = (float)T("IslandCameraController").GetField("TreeTopHeight").GetValue(null);
        var trees = (Vector2Int[])T("IslandLayout").GetField("Trees").GetValue(null);
        var report = new StringBuilder("[Nov13] Ada kadrajı:");
        foreach (var (name, w, h) in Aspects)
        {
            camera.aspect = (float)w / h;
            controller.GetType().GetMethod("SetDefaultView").Invoke(controller, null);
            yield return null;
            // Kuzey ağaçlarının tepesi üst çubuğun altında
            float highest = trees.Max(t => H - (t.y + 1f) + 0.1f + treeTop);
            var vTop = camera.WorldToViewportPoint(new Vector3(10f, highest));
            Assert.LessOrEqual(vTop.y, 1f - top + 0.005f, $"{name}: kuzey ağaçları üst çubuğun altında (y={vTop.y:0.000})");
            // Güney adacıkları (SW c1–3, SE c18–20, r29–30) ekranda ve alt-orta ana düğmenin yanında
            foreach (var cell in new[] { new Vector2(1f, 31f), new Vector2(3.9f, 30f), new Vector2(18f, 29f), new Vector2(20.9f, 31f) })
            {
                var v = camera.WorldToViewportPoint(new Vector3(cell.x, H - cell.y));
                Assert.That(v.x, Is.InRange(0f, 1f), $"{name}: adacık x {cell}");
                Assert.That(v.y, Is.InRange(0f, 1f), $"{name}: adacık y {cell}");
                Assert.IsTrue(Mathf.Abs(v.x - 0.5f) > 0.14f, $"{name}: adacık ana düğmenin altında kalmaz (x={v.x:0.00})");
            }
            report.Append($" {name} boy={camera.orthographicSize:0.00} ağaç-tepesi y={vTop.y:0.000};");
            yield return Capture("F5_frame_" + name, w, h);
        }
        Debug.Log(report);
    }

    // ==================== 6. ANA ÜS ALT BARI ====================

    [UnityTest]
    public IEnumerator BaseMainButtonHasEmblemAlignedBadgeAndNoStrayDockText()
    {
        PlayerPrefs.SetInt(RadialKey, 0);   // kapalı: ana düğmede toplu uyarı noktası görünsün
        yield return LoadBase(15, gems: 18, gold: 8693);
        var menu = (Component)T("RadialDockMenu").GetProperty("Current").GetValue(null);
        var main = (RectTransform)Prop(menu, "MainButton");
        var visual = main.Find("Visual");
        Assert.IsNull(visual.Find("Gem"), "ana düğmede elmas yok");
        Assert.NotNull(visual.Find("SwordL")); Assert.NotNull(visual.Find("SwordR")); Assert.NotNull(visual.Find("Emblem"));
        menu.GetType().GetMethod("RefreshBadges").Invoke(menu, null);
        var dot = (RectTransform)visual.Find("Badge");
        Assert.IsTrue(dot.gameObject.activeSelf, "kapalı menüde uyarı noktası (inşaat önerisi)");
        float r = main.sizeDelta.x * 0.5f, d = dot.anchoredPosition.magnitude;
        Assert.That(d, Is.InRange(r * 0.9f, r * 1.1f), $"nokta düğmenin kenarında (merkeze uzaklık {d:0.0}, yarıçap {r})");
        Assert.Greater(dot.anchoredPosition.x, 0f); Assert.Greater(dot.anchoredPosition.y, 0f);

        // Alt barda tek başına sayı (ör. elmas "18") olmamalı: ekranın alt %15'indeki etkin yazılar
        var hud = GameObject.Find("BaseCanvas").transform;
        var stray = new List<string>();
        float screenH = Screen.height;
        foreach (var c in hud.GetComponentsInChildren<Component>(false))
        {
            if (c == null || c.GetType().Name != "TextMeshProUGUI") continue;
            var rect = ScreenRect((RectTransform)c.transform);
            if (rect.center.y > screenH * 0.15f) continue;
            string text = TextOf(c);
            if (string.IsNullOrWhiteSpace(text)) continue;
            string path = c.transform.name;
            for (var p = c.transform.parent; p != null && p != hud; p = p.parent) path = p.name + "/" + path;
            Debug.Log($"[Nov13] alt yazı: '{text}' @ {path} y={rect.center.y:0}");
            if (System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), "^[0-9]+$")) stray.Add($"'{text}' @ {path}");
        }
        Assert.IsEmpty(stray, "alt barda artık sayı: " + string.Join("; ", stray));
        yield return CaptureBase("F6_base_dock_closed");
        menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { true, true, false });
        yield return null;
        yield return CaptureBase("F6_base_dock_open");
    }

    // ==================== 7. ORTAK VERİ KAYNAĞI + BAŞTAN OYNATMA ====================

    private static string BaseChip(string path) => TextOf(GameObject.Find("BaseCanvas").transform.Find("SafeArea/HudRoot/" + path));
    private static string IslandChip(string name) => TextOf(IslandSafe.Find("TopBar/" + name + "/Value"));
    private static string Short(long v) => (string)Call("NumberFormat", "Short", v, 10000L);

    [UnityTest]
    public IEnumerator JourneyIslandRaidReturnBaseIslandWithSharedTopBarValues()
    {
        PlayerPrefs.DeleteKey(IslandRadialKey);
        PlayerPrefs.SetInt(RadialKey, 1);
        yield return LoadBase(7, meat: 123, wood: 456, gold: 4321, gems: 77);
        var log = new StringBuilder("[Nov13] Baştan oynatma:\n");
        string[] BaseValues() => new[] { BaseChip("GoldPill/GoldText"), BaseChip("GemPill/GemText"), BaseChip("ResourceTopBar/MeatPill/Value"), BaseChip("ResourceTopBar/WoodPill/Value") };
        string[] IslandValues() => new[] { IslandChip("GoldPill"), IslandChip("GemPill"), IslandChip("MeatPill"), IslandChip("WoodPill") };
        var b0 = BaseValues();
        CollectionAssert.AreEqual(new[] { "4321", "77", "123", "456" }, b0, "Ana Üs üst bar");
        log.AppendLine("Ana Üs: " + string.Join(" · ", b0));

        // Ana Üs → ADA (radyal ADA düğmesi, bulutlu)
        yield return GoIslandFromBase();
        yield return Capture("J1_island_arrive", 1080, 1920);
        var i0 = IslandValues();
        CollectionAssert.AreEqual(b0, i0, "Ada üst barı Ana Üs ile aynı kaynaktan (altın, elmas, et, odun)");
        log.AppendLine("Ada: " + string.Join(" · ", i0));

        // Değer değişince iki bar da aynı sayıyı gösterir: elmas ve altın GameManager'dan, Et/Odun kayıttaki adadan
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        gm.GetType().GetMethod("AddGems").Invoke(gm, new object[] { 5, false });
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.AreEqual("82", IslandChip("GemPill"));

        // AKIN → pano → savunma (bot) → sonuç
        var menu = (Component)Prop(Hud, "Radial");
        ClickRadial(menu, "raid");
        yield return null;
        yield return Capture("J2_raid_board", 1080, 1920);
        var raid = Raid;
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        T("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, Enum.Parse(T("IslandBot+Profile"), "Good") });
        Time.timeScale = 6f;
        float deadline = Time.realtimeSinceStartup + 150f;
        while (Prop(raid, "Phase").ToString() == "Running" && Time.realtimeSinceStartup < deadline) yield return null;
        Time.timeScale = 1f;
        string phase = Prop(raid, "Phase").ToString();
        log.AppendLine("Akın 1 sonucu: " + phase);
        Assert.That(phase, Is.EqualTo("Won").Or.EqualTo("Lost"), "Akın bitti");
        yield return null;
        yield return AssertResultReturnsToIsland("J3_result");

        // Ada → ANA ÜS (üst çubuk), değerler aynı
        var i1 = IslandValues();
        IslandSafe.Find("TopBar/HomeButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return WaitScene("BaseScene");
        yield return CaptureBase("J5_base_after_island");
        var b1 = BaseValues();
        CollectionAssert.AreEqual(i1, b1, "adadan dönünce Ana Üs barı aynı değerleri gösterir");
        log.AppendLine("Ada (akından sonra): " + string.Join(" · ", i1) + " → Ana Üs: " + string.Join(" · ", b1));

        // Ana Üs → ADA yeniden
        yield return GoIslandFromBase();
        yield return Capture("J6_island_again", 1080, 1920);
        CollectionAssert.AreEqual(b1, IslandValues(), "adaya yeniden girince aynı değerler");
        Debug.Log(log.ToString());
        File.WriteAllText(Path.Combine(Folder, "journey.txt"), log.ToString());
    }

    private static IEnumerator GoIslandFromBase()
    {
        var baseMenu = (Component)T("RadialDockMenu").GetProperty("Current").GetValue(null);
        ClickRadial(baseMenu, "island");
        float deadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < deadline)
        {
            var setup = Object.FindFirstObjectByType(T("IslandSceneSetup"));
            if (setup != null && (bool)setup.GetType().GetProperty("Ready").GetValue(setup)
                && T("SceneCloudTransition").GetProperty("Active").GetValue(null) == null) break;
            yield return null;
        }
        Assert.NotNull(Setup, "ada açıldı");
        Call("IslandNameUi", "Close");
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private static IEnumerator WaitScene(string scene)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((SceneManager.GetActiveScene().name != scene || T("SceneCloudTransition").GetProperty("Active").GetValue(null) != null)
               && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
        for (int i = 0; i < 30; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
    }

    // ==================== YARDIMCILAR ====================

    private static IEnumerator LoadBase(int mainBase, int meat = 0, int wood = 0, int gold = 6345, int gems = 111)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        T("DevSettings").GetProperty("Enabled").SetValue(null, false);
        yield return null;
        var save = NewSave();
        foreach (var entry in (IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", gold);
        Set(save, "gems", gems);
        Set(save, "firstBaseGuideDone", true);
        var island = Field(save, "island");
        Set(island, "meat", meat); Set(island, "wood", wood);
        Set(island, "keepLevel", Math.Max(1, Math.Min(mainBase, 15)));
        Set(island, "tutorialStep", 4);
        Call("IslandNameRules", "Apply", island, "Kartal Adası");   // ad penceresi akışı bu testin konusu değil
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private static string Folder
    {
        get
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/feedback13"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static IEnumerator CaptureBase(string name)
    {
        yield return null;
        CaptureCamera(name, Camera.main, 1080, 1920, false);
    }

    private static IEnumerator Capture(string name, int w, int h)
    {
        yield return null;
        CaptureCamera(name, (Camera)Prop(Setup, "WorldCamera"), w, h, true);
    }

    private static void CaptureCamera(string name, Camera camera, int w, int h, bool island)
    {
        var texture = new RenderTexture(w, h, 24);
        var rect = camera.rect;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        if (island)
        {
            var controller = Prop(Setup, "CameraController");
            if (!(bool)Prop(Setup, "InDefense")) controller.GetType().GetMethod("SetDefaultView").Invoke(controller, null);
        }
        var changed = new List<(Canvas canvas, RenderMode mode, Camera cam, int order, bool over)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
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
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.rect = rect;
        camera.ResetAspect();
        texture.Release();
        Object.Destroy(texture);
        foreach (var (canvas, mode, cam, order, over) in changed)
        {
            if (canvas == null) continue;
            canvas.renderMode = mode; canvas.worldCamera = cam; canvas.sortingOrder = order; canvas.overrideSorting = over;
        }
        if (island)
        {
            var controller = Prop(Setup, "CameraController");
            if (!(bool)Prop(Setup, "InDefense")) controller.GetType().GetMethod("SetDefaultView").Invoke(controller, null);
        }
    }
}
