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
/// 13 Kasım 2026, 3. oturum (oyun testi geri bildirimi 2): akın haritası güncel durum (1.1), menü rozetleri (1.2), ada hayalet engelleri
/// + hücre görseli (1.3), Ana Üs yerleşim çakışmaları (1.4) ve sonraki aşamaların denetimleri. Görüntüler <c>PlaytestOutput/session3/*.png</c>.
/// Yalıtılmış test kaydı (gerçek kayıt geri yüklenir).
/// </summary>
public class Session3Tests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private bool devWas;

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
        devWas = (bool)T("DevSettings").GetProperty("Enabled").GetValue(null);
    }

    [TearDown]
    public void RestoreRealSave()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
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

    internal static string Folder
    {
        get
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session3"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    internal static IEnumerator Capture(string name, int w = 1080, int h = 1920)
    {
        yield return null;
        Nov13FeedbackTests.CaptureCamera(name, (Camera)Prop(Setup, "WorldCamera"), w, h, true, Folder);
    }

    internal static IEnumerator CaptureBase(string name)
    {
        yield return null;
        Nov13FeedbackTests.CaptureCamera(name, Camera.main, 1080, 1920, false, Folder);
    }

    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);
    private const int W = 22, H = 36;

    private static Vector2Int FirstTreeInZone(int zone, int skip = 0)
    {
        var trees = (Vector2Int[])T("IslandLayout").GetField("Trees").GetValue(null);
        return trees.Where(t => (int)Call("IslandLayout", "ZoneAt", t.x, t.y) == zone).Skip(skip).First();
    }

    // ==================== 1.1 AKIN HARİTASI GÜNCEL DURUMU GÖSTERİR ====================

    [UnityTest]
    public IEnumerator RaidMapShowsTheCurrentIslandStateInTheBaseFrame()
    {
        var cut = FirstTreeInZone(0);
        yield return LoadIsland(island =>
        {
            Set(island, "zoneMask", 1 | 2);
            ((IList<string>)Field(island, "clearedTrees")).Add(cut.x + "," + cut.y);
        }, 5, v => { });
        Assert.IsTrue((bool)Field(Config, "islandClearableTrees"), "varsayılan: kesilebilir ağaç");
        var raid = Prop(Setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.IsTrue((bool)Prop(Setup, "InDefense"));
        var mapButton = GameObject.Find("MapButton");
        Assert.NotNull(mapButton, "HARİTA düğmesi");
        mapButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.8f);

        var camera = (Camera)Prop(Setup, "WorldCamera");
        var args = new object[] { camera.aspect, null, null };
        T("IslandCameraController").GetMethod("ComputeFixedView").Invoke(null, args);
        var center = (Vector2)args[1]; float size = (float)args[2];
        Assert.AreEqual(size, camera.orthographicSize, 0.05f, "HARİTA = üs görünümüyle aynı kadraj (boy)");
        Assert.AreEqual(center.x, camera.transform.position.x, 0.05f, "HARİTA kadraj merkezi x");
        Assert.AreEqual(center.y, camera.transform.position.y, 0.05f, "HARİTA kadraj merkezi y");

        // Bulutlar: açık bölgeler (Z0, Z1) bulutsuz, kilitli bölgeler (Z2+) savunmada da bulutlu (yarı saydam)
        var clouds = Object.FindObjectsByType(T("IslandZoneCloud"), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().ToList();
        foreach (var cl in clouds)
        {
            int zone = (int)Field(cl, "zone");
            Assert.AreEqual(zone >= 2, cl.gameObject.activeSelf, $"{cl.name} (Z{zone}) savunmada {(zone >= 2 ? "görünür" : "gizli")}");
            if (zone >= 2) Assert.Less((float)Prop(cl, "Dim"), 0.9f, "savunmada bulut yarı saydam");
        }
        // Kesilen ağaç savunmada da yok
        var tree = T("IslandTree").GetMethod("At").Invoke(null, new object[] { cut });
        Assert.IsTrue(tree == null || !((Component)tree).gameObject.activeInHierarchy, "kesilen ağaç haritada görünmez");
        yield return CaptureDefense("S1_raid_map_current");
        // Takibe dön → harita yeniden açılınca aynı kadraj
        mapButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.6f);
        mapButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.AreEqual(size, camera.orthographicSize, 0.05f, "ikinci açılışta da güncel kadraj");
    }

    /// <summary>Savunmada kamerayı oynatmadan görüntü (CaptureCamera ada modunda yalnız üs görünümünü sıfırlar).</summary>
    private static IEnumerator CaptureDefense(string name)
    {
        yield return null;
        var camera = (Camera)Prop(Setup, "WorldCamera");
        Nov13FeedbackTests.CaptureCamera(name, camera, Mathf.RoundToInt(1920 * camera.aspect), 1920, true, Folder);
    }

    // ==================== 1.2 ROZETLER ====================

    private static object Gm => T("GameManager").GetProperty("Instance").GetValue(null);

    private static IEnumerator EmptySceneWithSave(Action<object> configure)
    {
        // Sahne kapatmadan (test çalıştırıcının sahnesi kapanırsa koşu takılır): Ana Üs harita yöneticisi varsa yok edilir → sayım kayıttan
        var map = T("BaseMapManager").GetProperty("Instance").GetValue(null) as Component;
        if (map != null) Object.DestroyImmediate(map.gameObject);
        yield return null;
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        T("DevSettings").GetProperty("Enabled").SetValue(null, false);
        yield return null;
        var save = NewSave();
        configure(save);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        managerType.GetMethod("CheckUnlockBuildings", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);   // gerçek oyundaki gibi: Ana Üs seviyesine göre türler açık
        yield return null;
    }

    private static void AddPlaced(object save, string type, int n, int level)
    {
        var list = (IList)Field(save, "placedBuildings");
        for (int i = 0; i < n; i++)
        {
            var p = Activator.CreateInstance(T("PlacedBuildingData"));
            Set(p, "slotId", $"{type}_t{i}_{list.Count}"); Set(p, "buildingType", type); Set(p, "level", level);
            list.Add(p);
        }
    }

    [UnityTest]
    public IEnumerator BuildBadgeOnlyShowsWhenSomethingCanActuallyBeBuilt()
    {
        string[] menu = { "ArcherTower", "CannonTower", "Monastery", "CardWorkshop", "House", "Barracks" };
        object saveRef = null;
        yield return EmptySceneWithSave(save =>
        {
            saveRef = save;
            foreach (var entry in (IEnumerable)Field(save, "buildings"))
                if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", 15);
            Set(save, "gold", 9309);
        });
        Assert.IsNull(T("BaseMapManager").GetProperty("Instance").GetValue(null), "sahnesiz: sayım kayıttan");
        var gm = Gm;
        var buildingType = T("BuildingType");
        var placedList = (IList)Field(saveRef, "placedBuildings");
        // Her menü yapısını 15. seviyedeki sınırına kadar kur (seviye 1: eski kural "seviyesi 15 altında + altın yeter" diye rozet yakıyordu)
        foreach (var type in menu)
        {
            int have = placedList.Cast<object>().Count(p => (string)Field(p, "buildingType") == type);
            var data = ((IDictionary)Prop(gm, "Buildings"))[Enum.Parse(buildingType, type)];
            int max = (int)data.GetType().GetMethod("MaxCountAtBaseLevel").Invoke(data, new object[] { 15 });
            AddPlaced(saveRef, type, Math.Max(0, max - have), 1);
        }
        int Badge() => (int)T("RadialDockMenu").GetMethod("BuildBadge").Invoke(null, null);
        Assert.AreEqual(0, Badge(), "inşa edilebilecek her şey kurulu → İNŞA ET rozeti yok (yükseltme binaya dokunarak yapılır)");
        // Bir Okçu Kulesi kaldırılınca yeniden kurulabilir → rozet
        var archer = placedList.Cast<object>().Last(p => (string)Field(p, "buildingType") == "ArcherTower");
        placedList.Remove(archer);
        Assert.AreEqual(1, Badge(), "eksik yapı + altın + boş inşaatçı → rozet");
        // Altın yetmezse rozet anında kalkar
        Set(Prop(gm, "PlayerData"), "gold", 10);
        Assert.AreEqual(0, Badge(), "altın yetmezse rozet yok");
        Set(Prop(gm, "PlayerData"), "gold", 9309);
        Assert.AreEqual(1, Badge());
        placedList.Add(archer);
        Assert.AreEqual(0, Badge(), "koşul kalkınca rozet kalkar");
    }

    // ==================== 1.3 ADA HAYALET ENGELLERİ ====================

    [Test]
    public void MeadowIsNotAGhostObstacleWhenItIsNotDrawn()
    {
        Assert.IsFalse((bool)T("IslandGrid").GetProperty("MeadowBlocks").GetValue(null), "kesilebilir ağaç modunda çayır çizilmez → engel değil");
        var island = NewIsland(15);
        Placed(island).Clear();
        var meadow = (RectInt)T("IslandLayout").GetField("Meadow").GetValue(null);
        var problems = new List<string>();
        for (int c = meadow.xMin; c < meadow.xMax; c++)
            for (int r = meadow.yMin; r < meadow.yMax; r++)
            {
                string reason = Call("IslandGrid", "CellReason", c, r).ToString();
                bool tree = (bool)Call("IslandEconomy", "IsTreeCell", c, r);
                if (reason == "Nature" && !tree) problems.Add($"({c},{r}) çayır hücresi hâlâ Nature");
            }
        Assert.IsEmpty(problems, string.Join("; ", problems));
    }

    [Test]
    public void CleanGhostsRemovesUnknownOrphanAndDuplicateEntries()
    {
        var island = NewIsland(15);
        var placed = Placed(island);
        placed.Clear();
        placed.Add(Entry("House#1", "House", 2, 9, 12));
        placed.Add(Entry("Farm#2", "Farm", 1, 12, 12));          // tanınmayan tür: eskiden görünmez 2×2 Ev engeli
        placed.Add(Entry("House#3", "House", 0, 5, 14));          // görevi olmayan şantiye
        placed.Add(Entry("House#1", "House", 1, 14, 14));         // kopya kimlik
        placed.Add(Entry("Tower#4", "Tower", 1, 40, 40));         // harita dışı
        placed.Add(Entry("Castle#5", "Castle", 1, 10, 4));        // kale kayıtta olmaz
        placed.Add(Entry("Lumber#6", "Lumber", 0, 6, 14));        // görevi olan şantiye kalır
        var task = Field(island, "task");
        Set(task, "kind", "build"); Set(task, "buildingId", "Lumber#6");
        var cleared = (IList<string>)Field(island, "clearedTrees");
        var tree = FirstTreeInZone(0);
        cleared.Add(tree.x + "," + tree.y); cleared.Add("0,0"); cleared.Add("bozuk");
        int removed = (int)Call("IslandRepair", "CleanGhosts", island);
        Assert.AreEqual(7, removed, "5 bina + 2 ağaç anahtarı");
        CollectionAssert.AreEqual(new[] { "House#1", "Lumber#6" }, placed.Cast<object>().Select(p => (string)Field(p, "id")).ToArray());
        Assert.AreEqual(9, (int)Field(placed[0], "col"), "kopyadan ilki kalır");
        CollectionAssert.AreEqual(new[] { tree.x + "," + tree.y }, cleared.ToArray());
        Assert.AreEqual(0, (int)Call("IslandRepair", "CleanGhosts", island), "idempotent");
    }

    /// <summary>
    /// "Görsel = mantık": doğa nedeniyle kapalı her hücrede sahnede görünür bir ağaç (kesilmemiş) ya da altın taşı var; kesilen her ağacın
    /// hücresi açık. Hata ayıklama görseli <c>S1_3_cell_debug.png</c>: ada görüntüsünün üstüne hücre nedenleri (yeşil serbest, sarı doğa,
    /// mavi su, gri yar/merdiven, turuncu şerit, mor kilitli, kırmızı bina).
    /// </summary>
    [UnityTest]
    public IEnumerator EveryNatureBlockedCellHasAVisibleObjectAndDebugMapIsWritten()
    {
        var cut0 = FirstTreeInZone(0); var cut1 = FirstTreeInZone(0, 1);
        yield return LoadIsland(island =>
        {
            Set(island, "zoneMask", 63);
            var cleared = (IList<string>)Field(island, "clearedTrees");
            cleared.Add(cut0.x + "," + cut0.y); cleared.Add(cut1.x + "," + cut1.y);
        }, 6, v => { });
        var stones = (Vector3Int[])T("IslandLayout").GetField("GoldStones").GetValue(null);
        var trees = Object.FindObjectsByType(T("IslandTree"), FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().ToList();
        var problems = new List<string>();
        var reasons = new string[W, H];
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
            {
                string reason = Call("IslandGrid", "CellReason", c, r).ToString();
                reasons[c, r] = reason;
                if (reason != "Nature") continue;
                bool stone = stones.Any(s => s.x == c && s.y == r);
                bool visibleTree = trees.Any(t => (Vector2Int)Prop(t, "Cell") == new Vector2Int(c, r) && t.gameObject.activeInHierarchy && !(bool)Prop(t, "Cleared"));
                if (!stone && !visibleTree) problems.Add($"({c},{r}) Nature ama görünür nesne yok");
            }
        foreach (var cut in new[] { cut0, cut1 })
        {
            Assert.AreEqual("None", reasons[cut.x, cut.y], $"kesilen ağaç {cut} hücresi açık");
            var t = trees.First(x => (Vector2Int)Prop(x, "Cell") == cut);
            Assert.IsFalse(t.gameObject.activeInHierarchy, $"kesilen ağaç {cut} görünmez");
        }
        WriteCellDebug(reasons);
        Assert.IsEmpty(problems, string.Join("\n", problems.Take(20)));
    }

    // ==================== 2.1 RADYAL MENÜ BOYUTU + 2.2 ÜST BAR SIRASI ====================

    [UnityTest]
    public IEnumerator BaseRadialIsSmallerBarIsPlainAndTopBarFollowsConfigOrder()
    {
        PlayerPrefs.SetInt("MEB_RadialOpen", 1);
        yield return Nov13FeedbackTests.LoadBase(15, meat: 76, wood: 261, gold: 9309, gems: 20);
        var menu = (Component)T("RadialDockMenu").GetProperty("Current").GetValue(null);
        Assert.AreEqual(72f, ((RectTransform)Prop(menu, "MainButton")).sizeDelta.x, 0.1f, "ana düğme 92 → 72");
        foreach (var item in Nov13FeedbackTests.RadialItems(menu))
        {
            var rt = (RectTransform)Prop(item, "Rect");
            Assert.GreaterOrEqual(Mathf.Min(rt.rect.width, rt.rect.height), 48f, Field(item, "id") + " ≥ 48 dp");
            Assert.LessOrEqual(rt.rect.width, 56.1f, Field(item, "id") + " küçüldü");
        }
        var hud = GameObject.Find("BaseCanvas").transform.Find("SafeArea/HudRoot");
        // 4. oturum (3.1): kullanıcı aslanlı şeridi geri istedi → artık var olmalı.
        Assert.NotNull(hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RadialOrnament"), "aslanlı şerit geri geldi");
        var order = (List<string>)T("ResourceTopBar").GetMethod("BaseVisibleOrder").Invoke(null, new object[] { hud });
        CollectionAssert.AreEqual(new[] { "gold", "wood", "meat", "gem", "builder" }, order, "Ana Üs: Altın, Odun, Et, Elmas, İnşaatçı");
        yield return new WaitForSecondsRealtime(0.6f);
        yield return CaptureBase("S2_base_radial_open_topbar");
        menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { false, true, false });
        yield return new WaitForSecondsRealtime(0.3f);
        yield return CaptureBase("S2_base_radial_closed");

        // Ada: aynı bileşen, sıra Ana Üs · ad · Altın, Odun, Et, Elmas
        yield return Nov13FeedbackTests.GoIslandFromBase();
        var bar = Object.FindFirstObjectByType(T("ResourceTopBar"));
        var islandOrder = (List<string>)bar.GetType().GetMethod("VisibleOrder").Invoke(bar, null);
        CollectionAssert.AreEqual(new[] { "gold", "wood", "meat", "gem" }, islandOrder, "Ada: Altın, Odun, Et, Elmas");
        var safe = GameObject.Find("IslandCanvas").transform.Find("SafeArea");
        var home = (RectTransform)safe.Find("TopBar/HomeButton");
        var gold = (RectTransform)safe.Find("TopBar/GoldPill");
        Assert.Less(Nov13FeedbackTests.ScreenRect(home).center.x, Nov13FeedbackTests.ScreenRect(gold).center.x, "ANA ÜS en solda");
        var islandMenu = (Component)Prop(Prop(Setup, "Hud"), "Radial");
        Assert.AreEqual(72f, ((RectTransform)Prop(islandMenu, "MainButton")).sizeDelta.x, 0.1f, "adada da 72");
        yield return Capture("S2_island_radial_topbar");
    }

    // ==================== 2.4 ORTAK SÜSLÜ PENCERE + 2.5 GERİ AL ====================

    private sealed class TextRef { public string text; }
    private static TextRef Tmp(Transform t) => new TextRef { text = t != null ? Nov13FeedbackTests.TextOf(t) : "" };

    [UnityTest]
    public IEnumerator BaseBuildingPanelIsTheFancyIslandPanelAndUndoIsOrnate()
    {
        yield return Nov13FeedbackTests.LoadBase(6, gold: 9309, gems: 20);
        var ui = T("BaseWorldUI").GetProperty("Instance").GetValue(null);
        ui.GetType().GetMethod("ShowBuildingInfo", new[] { T("BuildingType"), typeof(string) })
            .Invoke(ui, new object[] { Enum.Parse(T("BuildingType"), "MainBase"), null });
        yield return new WaitForSecondsRealtime(0.5f);
        var panel = GameObject.Find("BaseCanvas").transform.Find("SafeArea/BuildingInfo");
        Assert.NotNull(panel, "Ana Üs bina penceresi");
        Assert.IsTrue(panel.gameObject.activeSelf);
        Assert.NotNull(panel.Find("Ribbon/Title"), "kurdele başlık (adadaki pencere)");
        Assert.AreEqual(L("Ana Üs", "Main Base").ToUpperInvariant().Length > 0, true);
        StringAssert.Contains(L("Seviye", "Level"), Tmp(panel.Find("Level")).text, "seviye satırı");
        Assert.IsTrue(panel.Find("WoodIcon").gameObject.activeSelf, "maliyet ikonu (altın)");
        Assert.IsTrue(panel.Find("Time").gameObject.activeSelf, "süre");
        Assert.IsTrue(panel.Find("TimeIcon").gameObject.activeSelf, "süre ikonu");
        StringAssert.Contains(L("Sv 7", "Lv 7"), Tmp(panel.Find("Primary/Label")).text, "YÜKSELT → Sv 7");
        StringAssert.Contains("→", Tmp(panel.Find("Body")).text, "mevcut → sonraki değer");
        yield return CaptureBase("S2_4_base_mainbase_panel");

        // Ana Üs dışı bir yapı: TAŞI düğmesi var; taşı → GERİ AL
        var map = T("BaseMapManager").GetProperty("Instance").GetValue(null);
        var objs = ((IEnumerable)Prop(map, "MapObjects")).Cast<object>().ToList();
        var mine = objs.FirstOrDefault(o => Field(o, "buildingType") != null && Field(o, "buildingType").ToString() == "GoldMine");
        Assert.NotNull(mine, "maden");
        ui.GetType().GetMethod("ShowBuildingInfo", new[] { T("BuildingType"), typeof(string) })
            .Invoke(ui, new object[] { Enum.Parse(T("BuildingType"), "GoldMine"), (string)Field(mine, "id") });
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsTrue(panel.Find("Secondary").gameObject.activeSelf, "TAŞI düğmesi");
        yield return CaptureBase("S2_4_base_mine_panel");
        ui.GetType().GetMethod("HideAllPanelsPublic").Invoke(ui, null);
        var go = (GameObject)Field(mine, "gameObject");
        Vector3 before = go.transform.position;
        var nearest = (Vector3)map.GetType().GetMethod("NearestFreeSpot").Invoke(map, new object[] { new Vector2Int(4, 4), before + new Vector3(0f, -6f, 0f), mine });
        Assert.IsTrue((bool)map.GetType().GetMethod("MoveObjectTo").Invoke(map, new object[] { mine, nearest }), "taşındı");
        var pc = T("PlacementController").GetProperty("Instance").GetValue(null);
        pc.GetType().GetMethod("ShowUndo", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pc, new object[] { mine, before });
        yield return new WaitForSecondsRealtime(0.3f);
        var undo = T("UndoMoveButton").GetProperty("Current").GetValue(null) as Component;
        Assert.NotNull(undo, "GERİ AL düğmesi");
        Assert.NotNull(undo.transform.Find("IconRing/Well/Icon"), "ikonlu");
        Assert.NotNull(undo.transform.Find("IconRing/Timer"), "süre halkası");
        yield return CaptureBase("S2_5_base_undo");
        undo.GetType().GetMethod("Press").Invoke(undo, null);
        yield return null;
        Assert.AreEqual(before.x, go.transform.position.x, 0.01f, "geri alındı");
        Assert.AreEqual(before.y, go.transform.position.y, 0.01f);
    }

    [UnityTest]
    public IEnumerator IslandPanelUsesSharedComponentAndMoveCanBeUndone()
    {
        yield return LoadIsland(island => Set(island, "zoneMask", 63), 6, v => { });
        var island = CurrentIsland();
        var house = FindEntry(island, "House");
        Assert.NotNull(house, "hazır köyde Ev");
        string id = (string)Field(house, "id");
        int col0 = (int)Field(house, "col"), row0 = (int)Field(house, "row");
        var buildUi = Prop(Prop(Setup, "Hud"), "BuildUi");
        buildUi.GetType().GetMethod("ShowBuilding").Invoke(buildUi, new object[] { id });
        yield return new WaitForSecondsRealtime(0.3f);
        var safe = GameObject.Find("IslandCanvas").transform.Find("SafeArea");
        var panel = safe.Find("BuildingPanel");
        Assert.NotNull(panel.Find("Ribbon/Title")); Assert.NotNull(panel.Find("Level"), "ortak bileşen (seviye satırı alanı)");
        yield return Capture("S2_4_island_panel");
        buildUi.GetType().GetMethod("Close").Invoke(buildUi, null);

        // Taşı → GERİ AL → eski yer
        int toCol = -1, toRow = -1;
        for (int r = 0; r < H && toCol < 0; r++)
            for (int c = 0; c < W; c++)
                if ((c != col0 || r != row0) && Check("House", c, r, island, 63, id, 6) == "None") { toCol = c; toRow = r; break; }
        Assert.GreaterOrEqual(toCol, 0, "boş yer bulundu");
        var host = T("IslandPlacementHost").GetMethod("ForMove").Invoke(null, new object[] { id });
        Assert.IsTrue((bool)host.GetType().GetMethod("Confirm").Invoke(host, new object[] { toCol, toRow }), "taşındı");
        Assert.AreEqual(toCol, (int)Field(FindEntry(island, "House"), "col"));
        var undo = T("UndoMoveButton").GetProperty("Current").GetValue(null) as Component;
        Assert.NotNull(undo, "adada da GERİ AL");
        Assert.AreEqual(safe, undo.transform.parent, "ada tuvalinde");
        yield return Capture("S2_5_island_undo");
        undo.GetType().GetMethod("Press").Invoke(undo, null);
        yield return null;
        var after = Placed(island).Cast<object>().First(p => (string)Field(p, "id") == id);
        Assert.AreEqual(col0, (int)Field(after, "col"), "geri alındı (sütun)");
        Assert.AreEqual(row0, (int)Field(after, "row"), "geri alındı (satır)");
    }

    private static string L(string tr, string en) => (string)T("L").GetMethod("T").Invoke(null, new object[] { tr, en });

    // ==================== AŞAMA 4: TEST KOLAYLIKLARI YAYINDA DERLEME DIŞI ====================

    /// <summary>
    /// Yayın derlemesinde (UNITY_EDITOR ve DEVELOPMENT_BUILD tanımsız) hiçbir test kolaylığı derlenmez: (1) DevSettings.Available yayın
    /// dalında false ve bütün kolaylık anahtarları ona bağlı; (2) DEV menüsü, bot, hata ayıklama katmanları dosya başında korunur;
    /// (3) Assets/Scripts'te bu sembolleri kullanan her satır ya korumalı blok içinde ya da DevSettings üzerinden geçer.
    /// AAB tarafı: Tools/check_aab.py global-metadata.dat'ta bu sınıf adlarını arar.
    /// </summary>
    [Test]
    public void CheatsAreCompiledOutOfReleaseBuilds()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        string dev = File.ReadAllText(Path.Combine(root, "Systems/DevSettings.cs"));
        StringAssert.Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD\n    public const bool Available = true;\n#else\n    public const bool Available = false;",
            dev.Replace("\r\n", "\n"), "Available yayında false");
        StringAssert.Contains("if (!Available) return false;", dev, "kolaylık okuması yayında hep false");
        foreach (var file in new[] { "UI/DevMenu.cs", "Systems/PlaytestBot.cs", "Systems/PlaytestJourney.cs", "Base/BattleDebugOverlay.cs", "Base/BaseDebugOverlay.cs" })
        {
            var first = File.ReadLines(Path.Combine(root, file)).FirstOrDefault(l => l.StartsWith("#if")) ?? "";
            StringAssert.Contains("DEVELOPMENT_BUILD", first, file + " derleme anında korunur");
        }
        // Korumasız kullanım taraması
        var problems = new List<string>();
        string[] symbols = { "DevMenu.", "PlaytestBot.", "DevAdvanceTime(" };
        foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(path);
            int depth = 0; var guard = new Stack<bool>(); bool guarded = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("#if")) { guard.Push(guarded); guarded = guarded || t.Contains("UNITY_EDITOR") || t.Contains("DEVELOPMENT_BUILD"); depth++; continue; }
                if (t.StartsWith("#else")) { guarded = guard.Count > 0 && guard.Peek(); continue; }
                if (t.StartsWith("#endif")) { guarded = guard.Count > 0 ? guard.Pop() : false; depth--; continue; }
                if (guarded || t.StartsWith("//") || t.StartsWith("///")) continue;
                foreach (var s in symbols)
                    if (t.Contains(s) && !path.Replace('\\', '/').EndsWith("UI/DevMenu.cs"))
                        problems.Add($"{Path.GetFileName(path)}:{i + 1} {t}");
            }
        }
        Assert.IsEmpty(problems, "korumasız test kolaylığı:\n" + string.Join("\n", problems));
        Assert.IsFalse((bool)T("ReleaseLogging").GetProperty("IsRelease").GetValue(null), "editörde yayın değil (yayında Debug.Log filtrelenir)");
    }

    // ==================== 2.3 AKIN SAYACI BİÇİMİ ====================

    [Test]
    public void RaidPillUsesSaDkSnUnits()
    {
        string S(double s) => (string)T("IslandHud").GetMethod("ShortSpan").Invoke(null, new object[] { s });
        bool tr = (string)Call("L", "T", "a", "b") == "a";
        Assert.AreEqual(tr ? "10sa 0dk" : "10h 0m", S(36000));
        Assert.AreEqual(tr ? "14sa 48dk" : "14h 48m", S(14 * 3600 + 47.5 * 60));
        Assert.AreEqual(tr ? "45dk" : "45m", S(45 * 60));
        Assert.AreEqual(tr ? "1dk" : "1m", S(59.6));
        Assert.AreEqual(tr ? "30sn" : "30s", S(30));
        Assert.AreEqual(tr ? "1sn" : "1s", S(0));
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(S(36000), "^[0-9]+s "), "saat 's' ile yazılmaz");
    }

    // ==================== 1.4 ANA ÜS YERLEŞİM ÇAKIŞMALARI ====================

    /// <summary>Ana Üs 15, her türden sınır kadar bina (kayıtlı konumsuz → yuvalar) ve kullanıcı görselindeki gibi bir Kışla'nın üstüne fener.</summary>
    internal static IEnumerator LoadCrowdedBase(int gold = 9309)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        T("DevSettings").GetProperty("Enabled").SetValue(null, false);
        yield return null;
        var save = NewSave();
        foreach (var entry in (IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", 15);
        Set(save, "gold", gold);
        Set(save, "gems", 20);
        Set(save, "firstBaseGuideDone", true);
        var island = Field(save, "island");
        Set(island, "keepLevel", 15); Set(island, "tutorialStep", 4);
        Call("IslandNameRules", "Apply", island, "Kartal Adası");
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        managerType.GetMethod("CheckUnlockBuildings", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);   // gerçek oyundaki gibi: Ana Üs seviyesine göre türler açık
        var buildingType = T("BuildingType");
        var placedList = (IList)Field(save, "placedBuildings");
        foreach (var type in new[] { "ArcherTower", "CannonTower", "Monastery", "CardWorkshop", "House", "Barracks" })
        {
            int have = placedList.Cast<object>().Count(p => (string)Field(p, "buildingType") == type);
            var data = ((IDictionary)Prop(gm, "Buildings"))[Enum.Parse(buildingType, type)];
            int max = (int)data.GetType().GetMethod("MaxCountAtBaseLevel").Invoke(data, new object[] { 15 });
            AddPlaced(save, type, Math.Max(0, max - have), 10);
        }
        // Fener: 2. Kışla yuvasının (3,3 sol-üst, 3×3) sol üst köşesine yakın; Bayrak: kamp yanında
        var decor = (IList)Field(save, "placedDecor");
        void Decor(string id, int x, int y)
        {
            var d = Activator.CreateInstance(T("PlacedDecorSave"));
            Set(d, "instanceId", id + "_t"); Set(d, "decorId", id); Set(d, "x", x); Set(d, "y", y);
            decor.Add(d);
        }
        var camp = (Vector2)Prop(T("BaseWorldLayout").GetMethod("Get").Invoke(null, new object[] { "camp" }), "center");
        Decor("lantern", 3, 5);
        Decor("flag", Mathf.RoundToInt(camp.x) + 1, Mathf.RoundToInt(camp.y) - 1);
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        managerType.GetMethod("CheckUnlockBuildings", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);   // gerçek oyundaki gibi: Ana Üs seviyesine göre türler açık
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
    }

    [UnityTest]
    public IEnumerator BaseHasNoVisualOverlapsAfterLoadRepair()
    {
        yield return LoadCrowdedBase();
        var overlaps = (List<string>)Call("BaseVisualBounds", "Overlaps");
        Debug.Log("[Session3] Ana Üs görsel çakışmalar (" + overlaps.Count + "):\n" + string.Join("\n", overlaps));
        yield return CaptureBase("S1_4_base_crowded");
        Assert.IsEmpty(overlaps, "görsel çakışma kalmamalı:\n" + string.Join("\n", overlaps.Take(20)));
        // Kahraman kamp bayrağının üstünde durmaz
        var hero = GameObject.Find("Hero");
        var camp = GameObject.Find("HeroCamp");
        Assert.NotNull(hero); Assert.NotNull(camp);
        Vector2 d = hero.transform.position - camp.transform.position;
        Assert.IsFalse(Mathf.Abs(d.x) < 1.8f && d.y > -0.8f && d.y < 1.9f, $"kahraman bayrakla iç içe değil (fark {d})");
        // Yeniden yükleme: onarım kalıcı (aynı konumlar, yeni taşıma yok)
        var before = ((IEnumerable)Call("BaseVisualBounds", "Collect")).Cast<object>()
            .Select(x => ((GameObject)x.GetType().GetField("Item2").GetValue(x)).transform.position).ToList();
        int movedAgain = (int)T("BaseMapManager").GetMethod("RepairVisualLayout").Invoke(T("BaseMapManager").GetProperty("Instance").GetValue(null), null);
        Assert.AreEqual(0, movedAgain, "ikinci onarım bir şey taşımaz");
    }

    private static void WriteCellDebug(string[,] reasons)
    {
        var camera = (Camera)Prop(Setup, "WorldCamera");
        const int w = 1080, h = 1920;
        var rt = new RenderTexture(w, h, 24);
        camera.targetTexture = rt; camera.aspect = (float)w / h;
        var controller = Prop(Setup, "CameraController");
        controller.GetType().GetMethod("SetDefaultView").Invoke(controller, null);
        camera.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = prev;
        var placed = Placed(CurrentIsland());
        var occupied = new bool[W, H];
        foreach (var p in placed)
        {
            var size = (Vector2Int)Call("IslandGrid", "Size", Call("IslandGrid", "ParseKind", (string)Field(p, "kind")));
            for (int c = (int)Field(p, "col"); c < (int)Field(p, "col") + size.x; c++)
                for (int r = (int)Field(p, "row"); r < (int)Field(p, "row") + size.y; r++)
                    if (c >= 0 && r >= 0 && c < W && r < H) occupied[c, r] = true;
        }
        Color ColorOf(string reason, bool occ)
        {
            if (occ) return new Color(0.95f, 0.2f, 0.2f);
            switch (reason)
            {
                case "None": return new Color(0.3f, 0.95f, 0.3f);
                case "Nature": return new Color(1f, 0.85f, 0.1f);
                case "Water": return new Color(0.2f, 0.4f, 1f);
                case "Cliff": case "Stairs": return new Color(0.5f, 0.5f, 0.5f);
                case "Lane": return new Color(1f, 0.55f, 0.1f);
                default: return new Color(0.7f, 0.3f, 0.9f);
            }
        }
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
            {
                if (reasons[c, r] == "Water" && !occupied[c, r]) continue;
                var a = camera.WorldToScreenPoint(new Vector3(c, H - r - 1f, 0f));
                var b = camera.WorldToScreenPoint(new Vector3(c + 1f, H - r, 0f));
                var col = ColorOf(reasons[c, r], occupied[c, r]);
                for (int y = Mathf.Max(0, (int)a.y + 2); y < Mathf.Min(h, (int)b.y - 2); y++)
                    for (int x = Mathf.Max(0, (int)a.x + 2); x < Mathf.Min(w, (int)b.x - 2); x++)
                        tex.SetPixel(x, y, Color.Lerp(tex.GetPixel(x, y), col, 0.42f));
            }
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Folder, "S1_3_cell_debug.png"), tex.EncodeToPNG());
        camera.targetTexture = null; camera.ResetAspect();
        Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
    }
}
