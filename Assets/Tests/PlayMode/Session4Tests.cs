using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// 14 Kasım 2026, 4. oturum: ortak yol bulma (1.1), seçim yöneticisi (1.2), maden sprite'ı (1.3), ada doğa temizliği (1.4) ve sonraki
/// aşamaların denetimleri. Görüntüler <c>PlaytestOutput/session4/*.png</c>. Yalıtılmış test kaydı (gerçek kayıt geri yüklenir).
/// </summary>
[Timeout(180000)]
public class Session4Tests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private bool devWas;

    [SetUp]
    public void KeepRealSave()
    {
        LegacyDockFlag.Off();   // D107: bu testler radyal menüyü sınar; yeni alt bar MiniEmpireBottomBarTests'te
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
        devWas = (bool)T("DevSettings").GetProperty("Enabled").GetValue(null);
    }

    [TearDown]
    public void RestoreRealSave()
    {
        LegacyDockFlag.Restore();
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        Call("WalkGridDebug", "Show", new object[] { null });
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
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session4"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    internal static IEnumerator CaptureIsland(string name, int w = 1080, int h = 1920)
    {
        yield return null;
        yield return null;
        Nov13FeedbackTests.CaptureCamera(name, (Camera)Prop(Setup, "WorldCamera"), w, h, true, Folder);
    }

    internal static IEnumerator CaptureBase(string name, int w = 1080, int h = 1920)
    {
        yield return null;
        yield return null;
        Nov13FeedbackTests.CaptureCamera(name, Camera.main, w, h, false, Folder);
    }

    // ==================== YOL BULMA YARDIMCILARI ====================

    private static object Grid(string type) => T(type).GetField("Shared").GetValue(null);
    private static bool Walkable(object grid, Vector2Int c) => (bool)grid.GetType().GetMethod("Walkable").Invoke(grid, new object[] { c });
    private static int Version(object grid) => (int)grid.GetType().GetProperty("Version").GetValue(grid);
    private static Vector2Int CellOf(object grid, Vector2 p) => (Vector2Int)grid.GetType().GetMethod("CellOf").Invoke(grid, new object[] { p });
    private static Vector2 Center(object grid, Vector2Int c) => (Vector2)grid.GetType().GetMethod("Center").Invoke(grid, new object[] { c });

    private static List<Vector2> FindPath(object grid, Vector2 from, Vector2 to, out bool reached)
    {
        var method = T("GridPathfinder").GetMethod("FindPath");
        var args = new object[] { grid, from, to, false, null, 6000 };
        var path = (List<Vector2>)method.Invoke(null, args);
        reached = (bool)args[3];
        return path;
    }

    private static bool LineClear(object grid, Vector2 a, Vector2 b) =>
        (bool)T("GridPathfinder").GetMethod("LineClear").Invoke(null, new object[] { grid, a, b });

    /// <summary>Yolun (ilk nokta hariç: karakterin bulunduğu yer) bütün parçaları yürünebilir hücrelerden geçer.</summary>
    private static void AssertPathClear(object grid, List<Vector2> path, string label)
    {
        Assert.GreaterOrEqual(path.Count, 1, label + ": yol boş");
        for (int i = 1; i < path.Count; i++)
        {
            Assert.IsTrue(Walkable(grid, CellOf(grid, path[i])), $"{label}: {i}. nokta kapalı hücrede {path[i]}");
            if (i >= 2) Assert.IsTrue(LineClear(grid, path[i - 1], path[i]), $"{label}: {i - 1}→{i} parçası engelden geçiyor");
        }
    }

    // ==================== 1.1 YOL BULMA ====================

    [UnityTest]
    public IEnumerator IslandPathsAvoidBuildingsWaterAndCliffsAndUseStairs()
    {
        yield return LoadIsland(island => { Set(island, "zoneMask", 0xFF); }, 8, v => { });
        var grid = Grid("IslandWalkGrid");
        // Kale ve kurulu her bina kapalı
        var castle = (RectInt)T("IslandGrid").GetProperty("CastleRect").GetValue(null);
        Assert.IsFalse(Walkable(grid, new Vector2Int(castle.xMin + 1, castle.yMin + 1)), "kale hücresi kapalı");
        foreach (var entry in Placed(CurrentIsland()))
        {
            var rect = (RectInt)Call("IslandGrid", "Rect", Kind((string)Field(entry, "kind")), (int)Field(entry, "col"), (int)Field(entry, "row"));
            for (int c = rect.xMin; c < rect.xMax; c++)
                for (int r = rect.yMin; r < rect.yMax; r++)
                    Assert.IsFalse(Walkable(grid, new Vector2Int(c, r)), $"{Field(entry, "id")} hücresi ({c},{r}) kapalı");
        }
        Assert.IsFalse(Walkable(grid, new Vector2Int(0, 0)), "su kapalı");

        // Ova (r16) → zirve (kale yanı): yalnız merdivenlerden çıkılır; her adım yükseklik kuralına uyar.
        var lowland = Center(grid, new Vector2Int(8, 16));
        var plateau = Center(grid, new Vector2Int(6, 8));
        var path = FindPath(grid, lowland, plateau, out bool reached);
        Assert.IsTrue(reached, "ova → yayla yolu bulunur (merdivenden)");
        AssertPathClear(grid, path, "ova→yayla");
        bool usedStair = false;
        for (int i = 1; i < path.Count; i++)
        {
            var a = path[i - 1]; var b = path[i];
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.1f);
            for (int k = 0; k <= steps; k++)
            {
                var c = CellOf(grid, Vector2.Lerp(a, b, k / (float)Mathf.Max(1, steps)));
                if ((bool)Call("IslandLayout", "IsStair", c.x, c.y)) usedStair = true;
            }
        }
        Assert.IsTrue(usedStair, "yükseklik değişimi merdivenden");

        // Ulaşılamayan hedef (açık deniz): en yakın kara hücresinde biter, titreme yok.
        var sea = Center(grid, new Vector2Int(10, 34));
        var toSea = FindPath(grid, lowland, sea, out bool seaReached);
        Assert.IsFalse(seaReached, "deniz hedefine ulaşılmaz");
        AssertPathClear(grid, toSea, "denize");
        Assert.Less(Vector2.Distance(toSea[toSea.Count - 1], sea), Vector2.Distance(lowland, sea), "hedefe yaklaşır");

        // Piyonlar bir süre çalışsın: aldıkları her yol temiz.
        Call("WalkGridDebug", "Show", grid);
        yield return new WaitForSeconds(6f);
        var pawns = (IList)T("IslandPawn").GetField("All").GetValue(null);
        int checkedPaths = 0;
        foreach (var pawn in pawns)
        {
            var follower = pawn.GetType().GetProperty("LastPath").GetValue(pawn);
            if (follower == null) continue;
            var p = ((IReadOnlyList<Vector2>)follower.GetType().GetProperty("Path").GetValue(follower)).ToList();
            AssertPathClear(grid, p, "piyon " + pawn);
            checkedPaths++;
        }
        Assert.Greater(checkedPaths, 0, "piyonlar yürüdü");
        yield return CaptureIsland("S1_1_island_walkgrid");
    }

    [UnityTest]
    public IEnumerator PlacingABuildingOnARouteBumpsTheGridAndWalkersRepath()
    {
        yield return LoadIsland(island => { Set(island, "zoneMask", 0xFF); Set(island, "wood", 5000); Set(island, "meat", 5000); }, 8, v => { });
        var grid = Grid("IslandWalkGrid");
        var from = Center(grid, new Vector2Int(4, 18));
        var to = Center(grid, new Vector2Int(16, 18));
        var follower = Activator.CreateInstance(T("GridPathFollower"), grid, from, to, null);
        int v0 = Version(grid);
        // Yol üstüne bir ev koy (kayıt düzeyinde; IslandVillage sahnede de günceller)
        bool found = FindCellNear("House", CurrentIsland(), 0xFF, 10, 18, out int col, out int row);
        Assert.IsTrue(found, "yol yakınında boş ev yeri");
        Placed(CurrentIsland()).Add(Entry("House#path", "House", 1, col, row));
        T("IslandWalkGrid").GetMethod("Invalidate").Invoke(null, null);
        Assert.Greater(Version(grid), v0, "bina kurulunca ızgara sürümü artar");
        var tick = follower.GetType().GetMethod("Tick");
        Vector2 pos = from;
        for (int i = 0; i < 2000 && !(bool)follower.GetType().GetProperty("Done").GetValue(follower); i++)
        {
            var args = new object[] { pos, 0.05f, 0f };
            pos = (Vector2)tick.Invoke(follower, args);
            Assert.IsTrue(Walkable(grid, CellOf(grid, pos)) || i < 3, $"yürüyen karakter kapalı hücreye girdi {pos}");
        }
        Assert.Greater((int)follower.GetType().GetProperty("Repaths").GetValue(follower), 0, "yoldaki karakter rotayı yeniden hesapladı");
        Assert.Less(Vector2.Distance(pos, to), 0.6f, "hedefe ulaştı");
        yield return null;
    }

    [UnityTest]
    public IEnumerator BasePathsGoAroundBuildingsAndHeroStandsOnFreeGround()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        var grid = Grid("BaseWalkGrid");
        var bounds = (RectInt)grid.GetType().GetProperty("Bounds").GetValue(grid);
        Assert.Greater(bounds.width, 4, "Ana Üs ızgarası");
        // Bir uçtan diğerine: binaların içinden geçmez.
        Vector2Int? a = null, b = null;
        for (int x = bounds.xMin; x < bounds.xMax && a == null; x++)
            for (int y = bounds.yMin; y < bounds.yMax && a == null; y++)
                if (Walkable(grid, new Vector2Int(x, y))) a = new Vector2Int(x, y);
        for (int x = bounds.xMax - 1; x >= bounds.xMin && b == null; x--)
            for (int y = bounds.yMax - 1; y >= bounds.yMin && b == null; y--)
                if (Walkable(grid, new Vector2Int(x, y))) b = new Vector2Int(x, y);
        Assert.NotNull(a); Assert.NotNull(b);
        var path = FindPath(grid, Center(grid, a.Value), Center(grid, b.Value), out bool reached);
        AssertPathClear(grid, path, "Ana Üs köşeden köşeye");
        Call("WalkGridDebug", "Show", grid);
        yield return new WaitForSeconds(3f);
        var hero = GameObject.Find("Hero");
        Assert.NotNull(hero, "kahraman");
        var cell = CellOf(grid, hero.transform.position);
        Assert.IsTrue((bool)T("HeroMovementController").GetMethod("IsStandCell").Invoke(null, new object[] { cell, hero, null })
            || (bool)T("HeroMovementController").GetProperty("Instance").GetValue(null).GetType().GetProperty("IsMoving")
                .GetValue(T("HeroMovementController").GetProperty("Instance").GetValue(null)),
            "kahraman boş bir yerde durur (bina/dekor/bayrak görseline binmez) ya da yürüyor");
        yield return CaptureBase("S1_1_base_walkgrid");
    }

    [Test]
    public void BattleSteeringGoesAroundStructures()
    {
        var owner = new GameObject("TestObstacle");
        try
        {
            var obstacles = T("BattleObstacles");
            obstacles.GetMethod("Register").Invoke(null, new object[] { owner, new Rect(-1f, -1f, 2f, 2f) });
            var grid = Grid("BattleWalkGrid");
            Assert.IsFalse(Walkable(grid, CellOf(grid, Vector2.zero)), "yapı alanı kapalı");
            var steer = Activator.CreateInstance(T("GridSteer"), grid);
            var step = steer.GetType().GetMethod("Step");
            Vector2 pos = new Vector2(-4f, 0.1f), goal = new Vector2(4f, 0f);
            for (int i = 0; i < 600 && Vector2.Distance(pos, goal) > 0.05f; i++)
            {
                var args = new object[] { pos, goal, 0.05f, 0f };
                pos = (Vector2)step.Invoke(steer, args);
                Assert.IsFalse(new Rect(-1f, -1f, 2f, 2f).Contains(pos), $"düşman yapının içine girdi {pos}");
            }
            Assert.Less(Vector2.Distance(pos, goal), 0.1f, "hedefe dolanarak ulaştı");
        }
        finally
        {
            T("BattleObstacles").GetMethod("Unregister").Invoke(null, new object[] { owner });
            Object.DestroyImmediate(owner);
        }
    }
    // ==================== 1.2 SEÇİM YÖNETİCİSİ ====================

    [UnityTest]
    public IEnumerator DecorSelectionClosesOnEmptyTapOtherTapAndBackKey()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        var decor = T("DecorPlacementController").GetProperty("Instance").GetValue(null);
        Assert.NotNull(decor, "dekor denetçisi");
        var all = (Array)T("DecorPlacementController").GetProperty("All").GetValue(null);
        Assert.Greater(all.Length, 0, "dekor kataloğu");
        var data = all.GetValue(0);
        var spawn = decor.GetType().GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance);
        // Boş bir hücre bul
        var grid = Grid("BaseWalkGrid");
        var bounds = (RectInt)grid.GetType().GetProperty("Bounds").GetValue(grid);
        Vector2Int cell = default; bool found = false;
        for (int x = bounds.xMin + 2; x < bounds.xMax - 3 && !found; x++)
            for (int y = bounds.yMin + 2; y < bounds.yMax - 3 && !found; y++)
            {
                bool free = true;
                for (int dx = -1; dx <= 2 && free; dx++) for (int dy = -1; dy <= 2 && free; dy++)
                    free = Walkable(grid, new Vector2Int(x + dx, y + dy));
                if (free) { cell = new Vector2Int(x, y); found = true; }
            }
        Assert.IsTrue(found, "boş hücre");
        var item = (Component)spawn.Invoke(decor, new object[] { data, cell, "test-decor" });
        yield return null;
        var show = decor.GetType().GetMethod("ShowActions");
        var visible = decor.GetType().GetProperty("ActionsVisible");
        var setup = Object.FindFirstObjectByType(T("BaseSceneSetup"));
        var tap = setup.GetType().GetMethod("HandleTap", BindingFlags.NonPublic | BindingFlags.Instance);
        var selection = T("SelectionManager");

        show.Invoke(decor, new object[] { item });
        Assert.IsTrue((bool)visible.GetValue(decor), "TAŞI / SİL açıldı");
        yield return CaptureBase("S1_2_decor_selected");
        // 1) boş yere dokunuş
        Vector3 empty = new Vector3(bounds.xMin + 0.5f, bounds.yMin + 0.5f, 0f);
        for (int x = bounds.xMin; x < bounds.xMax; x++)
            if (Walkable(grid, new Vector2Int(x, bounds.yMin)) && Mathf.Abs(x - cell.x) > 3) { empty = new Vector3(x + 0.5f, bounds.yMin + 0.5f, 0f); break; }
        tap.Invoke(setup, new object[] { empty });
        yield return null;
        Assert.IsFalse((bool)visible.GetValue(decor), "boş yere dokunmak seçimi kapatır");
        // 2) aynı dekora tekrar dokunmak (aç/kapa)
        show.Invoke(decor, new object[] { item });
        Assert.IsTrue((bool)visible.GetValue(decor));
        tap.Invoke(setup, new object[] { item.transform.position });
        yield return null;
        Assert.IsFalse((bool)visible.GetValue(decor), "aynı dekora dokunmak seçimi kapatır");
        // 3) geri tuşu = SelectionManager.Clear
        show.Invoke(decor, new object[] { item });
        Assert.IsTrue((bool)visible.GetValue(decor));
        Assert.IsTrue((bool)selection.GetMethod("Clear").Invoke(null, null), "geri tuşu kapatacak seçim buldu");
        yield return null;
        Assert.IsFalse((bool)visible.GetValue(decor), "geri tuşu seçimi kapatır");
        // 4) başka bir yapıya dokunmak: bina penceresi açılır, dekor eylemleri kapanır
        show.Invoke(decor, new object[] { item });
        var ui = Object.FindFirstObjectByType(T("BaseWorldUI"));
        ui.GetType().GetMethod("ShowHeroInfo").Invoke(ui, null);
        yield return null;
        Assert.IsFalse((bool)visible.GetValue(decor), "başka seçim dekor seçimini kapatır");
        Assert.IsTrue((bool)selection.GetMethod("Clear").Invoke(null, null), "pencere de seçim yöneticisinde");
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsFalse((bool)ui.GetType().GetProperty("IsInfoPanelOpen").GetValue(ui), "geri tuşu pencereyi kapatır");
    }
    // ==================== 1.3 MADENİ KORU: MADEN SPRITE'I ====================

    internal static IEnumerator StartBattle(int region, int level, int mainBase = 5, Action<object> configure = null)
    {
        var managerType = T("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        configure?.Invoke(save);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        T("BattleSession").GetMethod("Begin").Invoke(null, new object[] { region, level });
        T("IslandSession").GetMethod("Clear").Invoke(null, null);   // önceki ada testinden kalan kip savaşı ada olarak kurmasın
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 45; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
    }

    /// <summary>Etkin, Animator'suz ve sprite'ı boş SpriteRenderer'lar (kayıp sprite referansı belirtisi).</summary>
    internal static List<string> NullSpriteRenderers()
    {
        var list = new List<string>();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (!sr.enabled || !sr.gameObject.activeInHierarchy || sr.sprite != null) continue;
            if (sr.GetComponentInParent<Animator>() != null) continue;
            if (sr.GetComponent<ParticleSystem>() != null) continue;
            list.Add(sr.gameObject.name);
        }
        return list;
    }

    [UnityTest]
    public IEnumerator DefendTheMineShowsATinySwordsMineWithHealthBar()
    {
        yield return StartBattle(1, 3);
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        Assert.NotNull(battle, "savaş");
        var mine = battle.GetType().GetProperty("DefendedMine").GetValue(battle) as Component;
        Assert.NotNull(mine, "Madeni Koru: maden kuruldu");
        var sr = mine.GetComponent<SpriteRenderer>();
        Assert.NotNull(sr.sprite, "maden sprite'ı atanmış");
        StringAssert.Contains("Gold", sr.sprite.texture.name + sr.sprite.name, "Tiny Swords altın kayası");
        Assert.NotNull(mine.transform.Find("MineHouse"), "maden evi");
        Assert.NotNull(mine.transform.Find("MineHouse").GetComponent<SpriteRenderer>().sprite, "maden evi sprite'ı");
        var bar = mine.GetType().GetField("healthBar", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(mine) as Component;
        Assert.NotNull(bar, "can barı");
        var empty = NullSpriteRenderers();
        File.WriteAllLines(Path.Combine(Folder, "S1_3_null_sprites_battle.txt"), empty);
        Assert.IsFalse(empty.Any(n => n.Contains("Mine")), "madende boş sprite yok: " + string.Join(", ", empty));
        var cam = Camera.main;
        var follow = cam.GetComponent(T("CameraFollow")) as Behaviour;
        if (follow != null) follow.enabled = false;
        cam.transform.position = new Vector3(mine.transform.position.x, mine.transform.position.y - 1.5f, cam.transform.position.z);
        yield return null;
        CardWorkshopTests.CaptureScreen("S1_3_defend_mine", "session4");
        if (follow != null) follow.enabled = true;
        battle.GetType().GetMethod("BattleLost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(battle, null);
    }
    // ==================== 1.4 ADA: ÇALI / KAYA TEMİZLİĞİ ====================

    [UnityTest]
    public IEnumerator IslandBushesAndRocksCanBeClearedForASmallCost()
    {
        yield return LoadIsland(island => { Set(island, "zoneMask", 0xFF); Set(island, "wood", 200); }, 8, v => { });
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        Set(Prop(gm, "PlayerData"), "gold", 100);   // altın tavanının altında kalsın
        var nature = T("IslandNatureClear");
        var items = ((IEnumerable)nature.GetProperty("Items").GetValue(null)).Cast<object>().ToList();
        Assert.Greater(items.Count, 10, "çalı ve kayalar bulundu");
        object bush = items.First(it => Field(it, "kind").ToString() == "Bush" && !(bool)Field(it, "Cleared"));
        object rock = items.First(it => Field(it, "kind").ToString() == "Rock" && !(bool)Field(it, "Cleared"));
        var island = CurrentIsland();
        int wood0 = (int)Field(island, "wood");
        int gold0 = (int)gm.GetType().GetProperty("Gold").GetValue(gm);

        // Dokunma: panel açılır
        var root = (Transform)Field(bush, "root");
        var village = T("IslandVillage").GetProperty("Instance").GetValue(null);
        var r = (SpriteRenderer)Field(bush, "renderer");
        Assert.IsTrue((bool)village.GetType().GetMethod("HandleTap").Invoke(village, new object[] { (Vector2)r.bounds.center }), "çalıya dokunuş işlendi");
        yield return null;
        yield return CaptureIsland("S1_4_bush_panel");

        var tryClear = nature.GetMethod("TryClear");
        var args = new object[] { Field(bush, "kind"), Field(bush, "cell"), null };
        Assert.IsTrue((bool)tryClear.Invoke(null, args), "çalı temizlendi: " + args[2]);
        args = new object[] { Field(rock, "kind"), Field(rock, "cell"), null };
        Assert.IsTrue((bool)tryClear.Invoke(null, args), "kaya kırıldı: " + args[2]);
        yield return new WaitForSeconds(0.6f);
        Assert.IsFalse(root.gameObject.activeSelf, "çalı görseli kalktı");
        Assert.IsFalse(((Transform)Field(rock, "root")).gameObject.activeSelf, "kaya görseli kalktı");
        var cleared = (IList<string>)Field(island, "clearedNature");
        Assert.AreEqual(2, cleared.Count, "kayda yazıldı");
        Assert.AreEqual(wood0 + (int)nature.GetField("BushWood").GetValue(null) - (int)nature.GetField("RockWood").GetValue(null), (int)Field(island, "wood"), "odun: +çalı −kaya");
        Assert.AreEqual(gold0 - (int)nature.GetField("BushGold").GetValue(null) + (int)nature.GetField("RockGold").GetValue(null),
            (int)gm.GetType().GetProperty("Gold").GetValue(gm), "altın: −çalı +kaya");
        // Kayıt döngüsü: JSON'a yazılıp geri okunur
        string json = JsonUtility.ToJson(island);
        StringAssert.Contains("clearedNature", json);
        yield return CaptureIsland("S1_4_after_clear");

        // Bina altında kalan çalı (eski kayıt) bedelsiz gizlenir
        object other = items.First(it => Field(it, "kind").ToString() == "Bush" && !(bool)Field(it, "Cleared"));
        var cell = (Vector2Int)Field(other, "cell");
        Placed(island).Add(Entry("House#overbush", "House", 1, cell.x, cell.y));
        nature.GetMethod("Refresh").Invoke(null, new object[] { true });
        Assert.IsTrue((bool)Field(other, "Cleared"), "bina altındaki çalı gizlendi");
        Assert.IsTrue(cleared.Contains("bush:" + cell.x + "," + cell.y), "göç: kayda yazıldı");
    }
    // ==================== AŞAMA 2 YARDIMCILARI ====================

    internal static IEnumerator LoadBaseWith(int mainBase, Action<object> configure, int gold = 6345)
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
        Set(save, "gems", 111);
        Set(save, "firstBaseGuideDone", true);
        var island = Field(save, "island");
        Set(island, "keepLevel", Math.Max(1, Math.Min(mainBase, 15)));
        Set(island, "tutorialStep", 4);
        Call("IslandNameRules", "Apply", island, "Kartal Adası");
        configure?.Invoke(save);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        managerType.GetMethod("CheckUnlockBuildings", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private static object DecorRecord(string instance, string id, int x, int y)
    {
        var rec = Activator.CreateInstance(T("PlacedDecorSave"));
        Set(rec, "instanceId", instance); Set(rec, "decorId", id); Set(rec, "x", x); Set(rec, "y", y);
        return rec;
    }

    // ==================== 2.1 / 2.2 DEKOR ====================

    private static readonly string[] TinySwordsTextures = { "Tree", "Bush", "Rock", "Stump", "Sheep", "Gold", "Wood Resource", "Meat Resource" };

    [Test]
    public void DecorCatalogIsTinySwordsOnlyAndSmallDecorIsOneCell()
    {
        var catalog = (Array)T("DecorPlacementController").GetProperty("Catalog").GetValue(null);
        Assert.GreaterOrEqual(catalog.Length, 15, "katalog zenginleşti");
        foreach (var d in catalog)
        {
            string id = (string)Field(d, "id");
            var sprite = (Sprite)d.GetType().GetProperty("Visual").GetValue(d);
            Assert.NotNull(sprite, id + " görseli");
            string tex = sprite.name;   // atlas dokusu adı paketlenince değişir; sprite adı Tiny Swords dosya adından gelir
            Assert.IsTrue(TinySwordsTextures.Any(t => tex.StartsWith(t) || tex.Contains(t)), $"{id}: Tiny Swords sprite'ı değil ({tex})");
            var fp = (Vector2Int)Field(d, "footprint");
            Assert.AreEqual(Vector2Int.one, fp, id + " 1×1");
        }
        var all = (Array)T("DecorPlacementController").GetProperty("All").GetValue(null);
        foreach (var d in all)
            if ((bool)Field(d, "retired"))
                Assert.IsFalse(((Sprite)d.GetType().GetProperty("Visual").GetValue(d))?.name.StartsWith("Tree") ?? false);
    }

    [UnityTest]
    public IEnumerator OldTwoByTwoDecorMigratesAndRetiredDecorStaysWithFullRefund()
    {
        yield return LoadBaseWith(7, save =>
        {
            var list = (IList)Field(save, "placedDecor");
            list.Add(DecorRecord("old-tree", "tree", 4, 16));
            list.Add(DecorRecord("old-bush", "bush", 8, 16));
            list.Add(DecorRecord("old-barrel", "barrel", 10, 16));
            Set(save, "decorLayoutVersion", 0);
        }, gold: 100);
        var decor = T("DecorPlacementController").GetProperty("Instance").GetValue(null);
        var placed = ((IEnumerable)decor.GetType().GetProperty("Placed").GetValue(decor)).Cast<Component>().ToList();
        Component Find(string id) => placed.FirstOrDefault(p => (string)p.GetType().GetProperty("InstanceId").GetValue(p) == id);
        var tree = Find("old-tree"); var bush = Find("old-bush"); var barrel = Find("old-barrel");
        Assert.NotNull(tree, "eski ağaç duruyor"); Assert.NotNull(bush, "eski çalı duruyor"); Assert.NotNull(barrel, "emekli fıçı silinmedi");
        Vector2Int Cell(Component c) => (Vector2Int)c.GetType().GetProperty("Cell").GetValue(c);
        Assert.IsTrue(new RectInt(4, 16, 3, 3).Contains(Cell(tree)), "3×3 ağaç eski alanının içinde kaldı (orta ya da en yakın boş hücre): " + Cell(tree));
        Assert.AreEqual(new Vector2Int(8, 16), Cell(bush), "2×2 çalı yerinde (1×1)");
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        Assert.AreEqual(1, (int)Field(Prop(gm, "PlayerData"), "decorLayoutVersion"));
        int gold0 = (int)Prop(gm, "Gold");
        var barrelData = barrel.GetType().GetProperty("Data").GetValue(barrel);
        int cost = (int)Field(barrelData, "cost");
        decor.GetType().GetMethod("Delete").Invoke(decor, new object[] { barrel });
        Assert.AreEqual(gold0 + cost, (int)Prop(gm, "Gold"), "emekli dekor silinince tam bedel iade");

        // Yan yana doğal çevre: 1×1 ağaç/çalı/kaya dizisi
        var spawn = decor.GetType().GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance);
        var all = ((Array)T("DecorPlacementController").GetProperty("Catalog").GetValue(null)).Cast<object>().ToList();
        string[] row = { "tree", "tree3", "bush2", "rock", "bush", "tree2", "rock4", "stump", "bush3", "tree4", "gold_stone" };
        var canPlace = decor.GetType().GetMethod("CanPlace", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public, null,
            new[] { T("DecorData"), typeof(Vector2Int), T("DecorInstance") }, null);
        int x = 4;
        foreach (var id in row)
        {
            var data = all.First(d => (string)Field(d, "id") == id);
            var cell = new Vector2Int(x++, 13);
            if (canPlace == null || (bool)canPlace.Invoke(decor, new object[] { data, cell, null }))
                spawn.Invoke(decor, new object[] { data, cell, "row-" + id });
        }
        yield return new WaitForSecondsRealtime(0.3f);
        var cam = Camera.main;
        cam.transform.position = new Vector3(9.5f, 14.5f, cam.transform.position.z);
        Nov13FeedbackTests.CaptureCamera("S2_1_decor_row", cam, 1080, 1920, false, Folder);
        // Menü: Dekor sekmesi
        var menu = T("BuildMenuController").GetProperty("Instance").GetValue(null);
        menu.GetType().GetMethod("Show").Invoke(menu, null);
        yield return new WaitForSecondsRealtime(0.4f);
        menu.GetType().GetMethod("SelectTab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new object[] { 1 });
        yield return new WaitForSecondsRealtime(0.4f);
        CardWorkshopTests.CaptureScreen("S2_2_decor_menu", "session4");
    }
    // ==================== 2.3 MADEN: TEK ÇIRAK ====================

    [UnityTest]
    public IEnumerator MineHasASingleDiggingApprenticeWithGoldSparks()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        yield return new WaitForSecondsRealtime(1f);
        var pawns = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)
            .Where(a => a.gameObject.name == "MinePawn" || a.gameObject.name == "BaseApprentice").ToList();
        Assert.AreEqual(1, pawns.Count, "madende tek figür: " + string.Join(",", pawns.Select(p => p.name)));
        var miner = pawns[0];
        Assert.NotNull(miner.GetComponent(T("BaseApprenticeTap")), "madenci Çırak'tır (dokununca Çırak penceresi)");
        Assert.NotNull(miner.GetComponent<BoxCollider2D>(), "dokunma alanı");
        var state = miner.GetCurrentAnimatorClipInfo(0);
        Assert.IsTrue(state.Length > 0 && state[0].clip.name.Contains("Pickaxe"), "kazma animasyonu: " + (state.Length > 0 ? state[0].clip.name : "yok"));
        bool spark = false;
        float until = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < until && !spark) { spark = GameObject.Find("MineGoldSpark") != null; yield return null; }
        Assert.IsTrue(spark, "altın kıvılcımı çıktı");
        var cam = Camera.main;
        float size = cam.orthographicSize;
        cam.orthographicSize = 3.2f;
        cam.transform.position = new Vector3(miner.transform.position.x, miner.transform.position.y + 0.4f, cam.transform.position.z);
        Nov13FeedbackTests.CaptureCamera("S2_3_mine_apprentice", cam, 1080, 1920, false, Folder);
        cam.orthographicSize = size;
    }

    /// <summary>
    /// Kaldırma kartı Tiny Swords kâğıt + kurdele; taşıma/inşa onayı nesnenin üstünde ✓/✗ (alt çubuk yok); GERİ AL yeni tasarım.
    /// </summary>
    [UnityTest]
    public IEnumerator RemoveCardConfirmButtonsAndUndoUseNewDesign()
    {
        yield return Nov13FeedbackTests.LoadBase(2);
        yield return new WaitForSecondsRealtime(1.5f);
        var setup = (Component)Object.FindFirstObjectByType(T("BaseSceneSetup"));
        var tap = setup.GetType().GetMethod("HandleTap", BindingFlags.NonPublic | BindingFlags.Instance);
        var map = T("BaseMapManager").GetProperty("Instance").GetValue(null);
        var tree = Object.FindObjectsByType(T("MapObjectTag"), FindObjectsSortMode.None).Cast<Component>()
            .FirstOrDefault(t => Field(t, "objectType").ToString() == "Tree"
                && (bool)map.GetType().GetMethod("IsBuildablePosition").Invoke(map, new object[] { t.transform.position }));
        Assert.NotNull(tree, "açık arazide ağaç");
        Camera.main.transform.position = new Vector3(tree.transform.position.x, tree.transform.position.y + 1.5f, Camera.main.transform.position.z);
        yield return null;
        // 1) Kaldırma kartı
        tap.Invoke(setup, new object[] { (Vector3)tree.GetComponent<Collider2D>().bounds.center });
        yield return new WaitForSecondsRealtime(0.4f);
        // 6. oturum (4.2): büyük kart yerine öğenin üstünde küçük eylem menüsü (KALDIR + süre).
        var card = GameObject.Find("RemoveActions") ?? GameObject.Find("NatureActions");
        Assert.NotNull(card, "kaldırma menüsü");
        Assert.NotNull(card.GetComponentInChildren<UnityEngine.UI.Button>(true), "KALDIR düğmesi");
        CardWorkshopTests.CaptureScreen("S5_remove_card", "session4");
        tap.Invoke(setup, new object[] { new Vector3(-50f, -50f) });
        yield return null;

        // 2) Bina taşıma: ✓/✗ binanın üstünde, eski alt çubuk yok
        var objs = ((IEnumerable)Prop(map, "MapObjects")).Cast<object>().ToList();
        var mine = objs.First(o => Field(o, "buildingType") != null && Field(o, "buildingType").ToString() == "GoldMine");
        var pc = T("PlacementController").GetProperty("Instance").GetValue(null);
        pc.GetType().GetMethod("BeginMove").Invoke(pc, new object[] { mine });
        var mineGo = (GameObject)Field(mine, "gameObject");
        Camera.main.transform.position = new Vector3(mineGo.transform.position.x, mineGo.transform.position.y + 1f, Camera.main.transform.position.z);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsNull(GameObject.Find("PlacementConfirmBar"), "alt bar çubuğu kalktı");
        var buttons = GameObject.Find("PlacementConfirm");
        Assert.NotNull(buttons, "✓/✗ düğmeleri");
        Assert.NotNull(buttons.transform.Find("Confirm/Icon").GetComponent<UnityEngine.UI.Image>().sprite, "✓ simgesi");
        Assert.NotNull(buttons.transform.Find("Cancel/Icon").GetComponent<UnityEngine.UI.Image>().sprite, "✗ simgesi");
        // 5. oturum (2.2): düğmeler hayaletin YANINDA (üstüne binmez; arkasındaki yapının üstünde durmaz) — ayrıntılı denetim Session5Tests
        var ghost = GameObject.Find("GhostPlacement").GetComponent<SpriteRenderer>();
        var side = (string)buttons.GetComponent(T("WorldConfirmButtons")).GetType().GetProperty("Side").GetValue(buttons.GetComponent(T("WorldConfirmButtons")));
        StringAssert.IsMatch("right|left|below|above", side, "hayaletin bir yanında");
        CaptureUi("S5_move_confirm", 780, 1688, () => buttons.GetComponent(T("WorldConfirmButtons")).GetType().GetMethod("Follow").Invoke(buttons.GetComponent(T("WorldConfirmButtons")), null));
        pc.GetType().GetMethod("CancelPlacement").Invoke(pc, null);
        yield return null;
        Assert.IsNull(GameObject.Find("PlacementConfirm"), "iptal düğmeleri kaldırır");

        // 3) Yeni dekor: bedel hapı
        var decor = T("DecorPlacementController").GetProperty("Instance").GetValue(null);
        var catalog = (IEnumerable)T("DecorPlacementController").GetProperty("Catalog").GetValue(null);
        var data = catalog.Cast<object>().First();
        decor.GetType().GetMethod("BeginPlacement").Invoke(decor, new object[] { data });
        yield return new WaitForSecondsRealtime(0.4f);
        var decorButtons = GameObject.Find("DecorConfirm");
        Assert.NotNull(decorButtons, "dekor ✓/✗");
        Assert.IsNull(GameObject.Find("DecorConfirmBar"));
        CaptureUi("S5_decor_confirm", 780, 1688, () => decorButtons.GetComponent(T("WorldConfirmButtons")).GetType().GetMethod("Follow").Invoke(decorButtons.GetComponent(T("WorldConfirmButtons")), null));
        decor.GetType().GetMethod("Cancel").Invoke(decor, null);
        yield return null;

        // 4) GERİ AL
        var before = mineGo.transform.position;
        var nearest = (Vector3)map.GetType().GetMethod("NearestFreeSpot").Invoke(map, new object[] { new Vector2Int(4, 4), before + new Vector3(0f, -5f, 0f), mine });
        Assert.IsTrue((bool)map.GetType().GetMethod("MoveObjectTo").Invoke(map, new object[] { mine, nearest }));
        pc.GetType().GetMethod("ShowUndo", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pc, new object[] { mine, before });
        yield return new WaitForSecondsRealtime(0.5f);
        var undo = T("UndoMoveButton").GetProperty("Current").GetValue(null) as Component;
        Assert.NotNull(undo);
        Assert.AreEqual("ui_icon_undo", undo.transform.Find("IconRing/Well/Icon").GetComponent<UnityEngine.UI.Image>().sprite.name, "Tiny Swords geri oku");
        var rim = (RectTransform)undo.transform;
        var ring = (RectTransform)undo.transform.Find("IconRing");
        var rc = new Vector3[4]; var ic = new Vector3[4];
        rim.GetWorldCorners(rc); ring.GetWorldCorners(ic);
        Assert.GreaterOrEqual(ic[0].x, rc[0].x, "ikon halkası hapın içinde (sol)");
        Assert.GreaterOrEqual(ic[0].y, rc[0].y - 0.5f, "ikon halkası hapın içinde (alt)");
        Assert.LessOrEqual(ic[2].y, rc[2].y + 0.5f, "ikon halkası hapın içinde (üst)");
        CardWorkshopTests.CaptureScreen("S5_undo", "session4");
    }

    /// <summary>Açık arazideki her ağaç/çalı/kaya/kütüğe dokunmak kaldırma kartını açar; Ana Üs veya kahraman penceresi açılmaz.</summary>
    [UnityTest]
    public IEnumerator EveryOpenLandNatureTapOpensClearPromptNotBuildingOrHero()
    {
        yield return Nov13FeedbackTests.LoadBase(2);
        yield return new WaitForSecondsRealtime(1.5f);
        var setup = (Component)Object.FindFirstObjectByType(T("BaseSceneSetup"));
        var tap = setup.GetType().GetMethod("HandleTap", BindingFlags.NonPublic | BindingFlags.Instance);
        var map = T("BaseMapManager").GetProperty("Instance").GetValue(null);
        var buildable = map.GetType().GetMethod("IsBuildablePosition");
        bool Open(string n) { var p = GameObject.Find(n); return p != null && p.activeInHierarchy; }
        var natures = Object.FindObjectsByType(T("NatureObstacle"), FindObjectsSortMode.None).Cast<Component>()
            .Where(n => (bool)buildable.Invoke(map, new object[] { n.transform.position })).ToList();
        // Harita ağaçları/kayaları (MapObjectTag Tree/Rock) da açık arazideki kaldırılabilir öğelerdir
        natures.AddRange(Object.FindObjectsByType(T("MapObjectTag"), FindObjectsSortMode.None).Cast<Component>()
            .Where(t => { string k = Field(t, "objectType").ToString(); return k == "Tree" || k == "Rock"; })
            .Where(t => (bool)buildable.Invoke(map, new object[] { t.transform.position })));
        Assert.GreaterOrEqual(natures.Count, 2, "açık arazide ağaç/kaya");
        var heroRect = (Rect)setup.GetType().GetMethod("HeroTapRect").Invoke(setup, null);
        // Zor durumlar: biri kalenin görsel kutusunun alt kenarına, biri kahramanın hemen yanına
        var castle = GameObject.Find("Building_MainBase_main").GetComponent<Collider2D>().bounds;
        natures[0].transform.position = new Vector3(castle.min.x + 0.4f, castle.min.y + 0.4f, natures[0].transform.position.z);
        natures[1].transform.position = new Vector3(heroRect.xMax + 0.25f, heroRect.center.y, natures[1].transform.position.z);
        Physics2D.SyncTransforms();
        int nearBig = 0;
        var failures = new List<string>();
        foreach (var n in natures)
        {
            Vector2 point = n.GetComponent<Collider2D>().bounds.center;
            if (heroRect.Contains(point) || Physics2D.OverlapPointAll(point).Length > 1) nearBig++;
            tap.Invoke(setup, new object[] { (Vector3)point });
            yield return null;
            if ((GameObject.Find("NatureActions") ?? GameObject.Find("RemoveActions") ?? GameObject.Find("RemovePrompt")) == null || Open("HeroInfo") || Open("BuildingInfo"))
                failures.Add(n.name + " hero=" + Open("HeroInfo") + " bina=" + Open("BuildingInfo"));
            tap.Invoke(setup, new object[] { new Vector3(-50f, -50f) });   // kapat
            yield return null;
        }
        Debug.Log($"[NatureTap] {natures.Count} nesne, {nearBig} tanesi büyük bir alanla çakışıyor");
        Assert.IsEmpty(failures, "kaldırma kartı açılmadı: " + string.Join("; ", failures));
        // Kahramanın gövdesine dokunmak hâlâ kahraman penceresini açar
        tap.Invoke(setup, new object[] { (Vector3)heroRect.center });
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.IsTrue(Open("HeroInfo") || GameObject.Find("RemovePrompt") != null, "kahraman gövdesi dokunuşu");
        Assert.Less(heroRect.width, 1.6f, "kahraman dokunma alanı gövde kadar");
    }

    /// <summary>Madene dokunmak madeni açar/altın toplar; yalnız çırağın gövdesi Çırak penceresini açar. Çırak kazar, yürür, altın taşır.</summary>
    [UnityTest]
    public IEnumerator MineTapCollectsGoldAndOnlyApprenticeBodyOpensApprentice()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        yield return new WaitForSecondsRealtime(1f);
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        var data = Prop(gm, "PlayerData");
        var mine = (Component)Object.FindFirstObjectByType(T("BaseMineVisual"));
        Transform miner = (Transform)Prop(mine, "Miner");
        Assert.NotNull(miner, "çırak");
        var setup = Object.FindFirstObjectByType(T("BaseSceneSetup"));
        var tap = setup.GetType().GetMethod("HandleTap", BindingFlags.NonPublic | BindingFlags.Instance);
        GameObject Panel() { var p = GameObject.Find("HeroInfo"); return p != null && p.activeInHierarchy ? p : null; }

        // 1) madenin çırak dışındaki her yeri (büyük kaya, maden evi, yığın) madendir: altın toplanır, Çırak penceresi açılmaz
        Vector3 c = mine.transform.position;
        var spots = new[] { c + new Vector3(-0.85f, 0.75f), c + new Vector3(1.05f, 0.5f), c + new Vector3(1.1f, -1.15f), c + new Vector3(-1.5f, 0.1f) };
        foreach (var spot in spots)
        {
            Set(data, "gold", 0);
            Set(data, "goldMineStoredGold", 30);
            tap.Invoke(setup, new object[] { spot });
            yield return null;
            Assert.AreEqual(0, (int)Field(data, "goldMineStoredGold"), "madene dokunuş altını toplar: " + (spot - c) + " çarpanlar=" +
                string.Join(",", Physics2D.OverlapPointAll(spot).Select(o => o.name + o.bounds)) + " mineCol=" +
                (mine.GetComponentInParent<BoxCollider2D>() != null ? mine.GetComponentInParent<BoxCollider2D>().bounds.ToString() : "yok"));
            Assert.IsNull(Panel(), "madene dokunuş Çırak penceresi açmaz: " + (spot - c));
            tap.Invoke(setup, new object[] { c + new Vector3(0f, -6f) });   // seçim/pencere kapansın
            yield return null;
        }
        // 2) çırağın gövdesi Çırak penceresini açar
        var body = miner.GetComponent<BoxCollider2D>();
        Assert.Less(body.bounds.size.x, 0.8f, "dokunma alanı dar (gövde)");
        tap.Invoke(setup, new object[] { body.bounds.center });
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.NotNull(Panel(), "çırağa dokunuş Çırak penceresini açar");
        StringAssert.Contains("Maden", Text(Panel().transform.Find("Body")));
        tap.Invoke(setup, new object[] { c + new Vector3(0f, -6f) });
        yield return null;

        // 3) çırak kazar → altını yığına taşır (yürür) → öbür kayaya yürür
        var anim = miner.GetComponent<Animator>();
        var clips = new HashSet<string>();
        Vector3 start = miner.position;
        float far = 0f;
        float until = Time.realtimeSinceStartup + 16f;
        bool walked = false;
        while (Time.realtimeSinceStartup < until)
        {
            var info = anim.GetCurrentAnimatorClipInfo(0);
            if (info.Length > 0) clips.Add(info[0].clip.name);
            far = Mathf.Max(far, (miner.position - start).magnitude);
            if ((bool)Prop(mine, "MinerWalking")) walked = true;
            if (walked && clips.Any(n => n.Contains("Run")) && clips.Any(n => n.Contains("Gold")) && far > 0.6f) break;
            yield return null;
        }
        Debug.Log("[MineApprentice] clips=" + string.Join(",", clips) + " far=" + far);
        Assert.IsTrue(walked, "çırak yürüdü");
        Assert.Greater(far, 0.6f, "çırak yerinden ayrıldı");
        Assert.IsTrue(clips.Any(n => n.Contains("Run")), "yürüme kareleri: " + string.Join(",", clips));
        Assert.IsTrue(clips.Any(n => n.Contains("Gold")), "altın taşıma kareleri: " + string.Join(",", clips));
        var cam = Camera.main;
        float size = cam.orthographicSize;
        cam.orthographicSize = 3.2f;
        cam.transform.position = new Vector3(c.x, c.y + 0.2f, cam.transform.position.z);
        Nov13FeedbackTests.CaptureCamera("S5_mine_apprentice_walk", cam, 1080, 1920, false, Folder);
        cam.orthographicSize = size;
    }
    // ==================== 2.4 KIŞLA DEVRİYESİ + ÖLÇEK TABLOSU ====================

    private static void AddBarracks(object save, int level, string slot = "barracks_1")
    {
        var placed = (IList)Field(save, "placedBuildings");
        var entry = Activator.CreateInstance(T("PlacedBuildingData"));
        Set(entry, "slotId", slot); Set(entry, "buildingType", "Barracks"); Set(entry, "level", level);
        placed.Add(entry);
        foreach (var b in (IEnumerable)Field(save, "buildings"))
            if (Field(b, "buildingType").ToString() == "Barracks") { Set(b, "isUnlocked", true); Set(b, "level", level); }
    }

    [UnityTest]
    public IEnumerator BarracksRoofLancerPatrolsAndBattleLancersMatchHeroScale()
    {
        yield return LoadBaseWith(12, save => AddBarracks(save, 10));
        var patrol = Object.FindFirstObjectByType(T("BarracksRoofPatrol")) as Component;
        Assert.NotNull(patrol, "Kışla çatı devriyesi");
        yield return new WaitForSecondsRealtime(1f);
        var lancer = (Transform)Prop(patrol, "Lancer");
        Assert.NotNull(lancer, "damdaki mızraklı");
        var body = patrol.GetComponent<SpriteRenderer>().bounds;
        Assert.IsTrue(lancer.position.y > body.center.y, "mızraklı binanın üst yarısında (damda)");
        Assert.IsTrue(lancer.position.x > body.min.x && lancer.position.x < body.max.x, "damın içinde");
        float x0 = lancer.position.x;
        bool moved = false;
        float until = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < until && !moved) { moved = Mathf.Abs(lancer.position.x - x0) > 0.2f; yield return null; }
        Assert.IsTrue(moved, "devriye: ileri geri yürür");
        var cam = Camera.main;
        float size = cam.orthographicSize;
        cam.orthographicSize = 4f;
        cam.transform.position = new Vector3(body.center.x, body.center.y, cam.transform.position.z);
        Nov13FeedbackTests.CaptureCamera("S2_4_barracks_roof", cam, 1080, 1920, false, Folder);
        cam.orthographicSize = size;

        // Savaş: mızraklılar kahramanla aynı ölçek tablosundan
        yield return StartBattle(1, 1, 12, save => AddBarracks(save, 10));
        var lancers = ((IEnumerable)T("AllyLancer").GetField("All").GetValue(null)).Cast<Component>().ToList();
        Assert.Greater(lancers.Count, 0, "savaşta mızraklı");
        var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
        float heroScale = hero.transform.lossyScale.x;
        foreach (var l in lancers)
            Assert.AreEqual(heroScale, l.transform.lossyScale.x, 0.02f, "mızraklı ölçeği = kahraman ölçeği (UnitScale.Battle)");
        Assert.AreEqual((float)T("UnitScale").GetMethod("BattleHero").Invoke(null, null), heroScale, 0.02f, "kahraman = tablo (HeroLook.WarriorScaleFor)");
        var bcam = Camera.main;
        var follow = bcam.GetComponent(T("CameraFollow")) as Behaviour;
        if (follow != null) follow.enabled = false;
        var mb = Object.FindFirstObjectByType(T("MainBaseController")) as Component;
        bcam.transform.position = new Vector3(mb.transform.position.x, mb.transform.position.y - 1.5f, bcam.transform.position.z);
        yield return null;
        CardWorkshopTests.CaptureScreen("S2_4_battle_lancers", "session4");
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        battle.GetType().GetMethod("BattleLost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(battle, null);
    }
    internal static string Text(Transform t)
    {
        foreach (var c in t.GetComponents<Component>())
        {
            var p = c.GetType().GetProperty("text");
            if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(c);
        }
        return null;
    }

    // ==================== 2.5 KAHRAMAN / ÇIRAK PENCERESİ ====================

    [UnityTest]
    public IEnumerator HeroAndApprenticeUseTheFancyUpgradePanel()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        var ui = Object.FindFirstObjectByType(T("BaseWorldUI"));
        ui.GetType().GetMethod("ShowHeroInfo").Invoke(ui, null);
        yield return new WaitForSecondsRealtime(0.5f);
        var panel = GameObject.Find("HeroInfo");
        Assert.NotNull(panel, "kahraman penceresi");
        Assert.NotNull(panel.transform.Find("Ribbon"), "süslü kurdele başlık");
        Assert.AreEqual("ui_paper", panel.GetComponent<UnityEngine.UI.Image>().sprite != null ? "ui_paper" : "", "kâğıt zemin");
        var level = Text(panel.transform.Find("Level"));
        StringAssert.Contains("/ 15", level, "Seviye N / 15");
        var body = Text(panel.transform.Find("Body"));
        StringAssert.Contains("→", body, "mevcut → sonraki değerler");
        Assert.IsTrue(panel.transform.Find("WoodIcon").gameObject.activeSelf, "altın ikonu + bedel");
        CardWorkshopTests.CaptureScreen("S2_5_hero_panel", "session4");
        ui.GetType().GetMethod("ShowApprenticeInfo").Invoke(ui, null);
        yield return new WaitForSecondsRealtime(0.4f);
        StringAssert.Contains("Maden", Text(panel.transform.Find("Body")), "Çırak değerleri");
        CardWorkshopTests.CaptureScreen("S2_5_apprentice_panel", "session4");
    }
    // ==================== 2.6 HEMEN BİTİR: BEDAVA YOK ====================

    [Test]
    public void FinishNowIsNeverFreeAndCostsOneGemUnderAMinute()
    {
        int Finish(double sec) => (int)Call("GemEconomy", "FinishCost", sec);
        Assert.AreEqual(0, Finish(0), "bitmiş iş");
        Assert.AreEqual(1, Finish(1), "1 sn kala 1 elmas");
        Assert.AreEqual(1, Finish(59));
        Assert.AreEqual(1, Finish(60), "tam 1 dk: 1 elmas");
        Assert.GreaterOrEqual(Finish(61), 2, "1 dk üstü formül");
        Assert.AreEqual(17, Finish(3600), "mevcut formül korunur");
        string label = (string)Call("GemEconomy", "FinishLabel", 1);
        StringAssert.DoesNotContain("BEDAVA", label);
        StringAssert.DoesNotContain("FREE", label);
        StringAssert.EndsWith("1", label, "fiyat: elmas ikonu + sayı");
    }
    // ==================== AŞAMA 3 + 2.7: EKRAN ORANLARI ====================

    internal static readonly (string tag, int w, int h)[] Ratios =
        { ("16x9", 1080, 1920), ("19_5x9", 1080, 2340), ("20x9", 1080, 2400), ("tablet4x3", 1536, 2048) };

    /// <summary>Arayüzlü (Overlay tuvalleri kameraya alınmış) görüntü, verilen piksel boyunda.</summary>
    internal static void CaptureUi(string name, int w, int h, Action prepare = null)
    {
        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var texture = new RenderTexture(w, h, 24);
        camera.targetTexture = texture;
        float aspect = camera.aspect;
        // 8. oturum: savaş kamerası üst/alt barların arasına kırpılır (WorldViewportBars); tuval kameraya bağlanınca o kırpık alana
        // sığdırılıyor ve görüntü cihazdakinden farklı çıkıyordu. Çekim tam ekran görüş alanıyla yapılır (cihazda tuval tam ekrandır).
        var viewport = camera.rect;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.aspect = (float)w / h;
        var changed = new List<(Canvas, RenderMode, Camera, int, bool)>();
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
        if (prepare != null) { prepare(); Canvas.ForceUpdateCanvases(); }
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
        camera.rect = viewport;
        camera.aspect = aspect;
        texture.Release();
        Object.Destroy(texture);
        foreach (var (canvas, mode, cam, order, over) in changed)
        {
            if (canvas == null) continue;
            canvas.renderMode = mode; canvas.worldCamera = cam; canvas.sortingOrder = order; canvas.overrideSorting = over;
        }
    }

    private static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    [UnityTest]
    public IEnumerator BaseDockLionStripIslandIconRadialSpacingAndShade()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        Assert.NotNull(Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "RadialOrnament"),
            "3.1 aslanlı şerit geri geldi");
        var menu = T("RadialDockMenu").GetProperty("Current").GetValue(null);
        menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { true, true, false });
        yield return new WaitForSecondsRealtime(0.6f);
        var island = GameObject.Find("Radial_island") ?? GameObject.Find("IslandBtn");
        Assert.NotNull(island, "ADA düğmesi");
        var icon = island.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(im => im.name == "Icon");
        Assert.NotNull(icon); Assert.NotNull(icon.sprite);
        StringAssert.Contains("ui_icon_island", icon.sprite.name, "3.2 Tiny Swords ada ikonu");
        // Etiketler çakışmaz (3.4)
        var labels = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
            .Where(t => t.name == "Label" && t.parent != null && t.parent.name == "Visual" && t.gameObject.activeInHierarchy).ToList();
        Assert.GreaterOrEqual(labels.Count, 4, "radyal etiketler");
        for (int a = 0; a < labels.Count; a++)
            for (int b = a + 1; b < labels.Count; b++)
            {
                var ra = ScreenRect(labels[a]); var rb = ScreenRect(labels[b]);
                Assert.IsFalse(ra.Overlaps(rb), $"etiketler çakışıyor: {labels[a].parent.parent.name} / {labels[b].parent.parent.name}");
            }
        Assert.Greater((float)menu.GetType().GetProperty("ShadeAlpha").GetValue(menu), 0.8f, "menü açıkken arka plan kararır");
        foreach (var r in Ratios) CaptureUi("S3_base_radial_" + r.tag, r.w, r.h);
        // Ayarlar (3.3)
        var settings = Object.FindFirstObjectByType(T("AudioSettingsUI")) as Component;
        Assert.NotNull(settings, "ayarlar");
        settings.GetType().GetMethod("Open").Invoke(settings, null);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsTrue(GameObject.Find("AudioSettingsPanel").activeSelf);
        foreach (var r in Ratios) CaptureUi("S3_settings_" + r.tag, r.w, r.h, () => settings.GetType().GetMethod("Refit").Invoke(settings, null));
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator BattleObjectiveSitsInTheTopBarNotOverTheWorld()
    {
        yield return StartBattle(1, 3);
        yield return null;   // D98: gösterge BattleTopBar'ın "ObjectiveCard" hedef kartı (eski ObjectivePill kalktı)
        var pill = GameObject.Find("ObjectiveCard");
        Assert.NotNull(pill, "Madeni Koru göstergesi (hedef kartı)");
        Assert.IsNotNull(pill.GetComponentInParent<Canvas>(), "3.4 gösterge üst barın tuvalinde (dünya halkalarının üstüne binmez)");
        StringAssert.Contains("MADEN", Text(pill.transform.Find("ObjectiveLabel")).ToUpperInvariant());
        foreach (var r in Ratios) CaptureUi("S3_battle_mine_hud_" + r.tag, r.w, r.h);
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        battle.GetType().GetMethod("BattleLost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(battle, null);
    }
    // ==================== 2.7 KART ATÖLYESİ ====================

    [UnityTest]
    public IEnumerator CardWorkshopFitsAllRatiosWithClearShortfallAndNoEmptySpeedUp()
    {
        yield return LoadBaseWith(7, save =>
        {
            var placed = (IList)Field(save, "placedBuildings");
            var entry = Activator.CreateInstance(T("PlacedBuildingData"));
            Set(entry, "slotId", "workshop_1"); Set(entry, "buildingType", "CardWorkshop"); Set(entry, "level", 3);
            placed.Add(entry);
            foreach (var b in (IEnumerable)Field(save, "buildings"))
                if (Field(b, "buildingType").ToString() == "CardWorkshop") { Set(b, "isUnlocked", true); Set(b, "level", 3); }
            Set(Field(save, "island"), "meat", 10);
        });
        Assert.IsTrue((bool)Call("CardWorkshopPanel", "Open", "workshop_1"), "atölye açıldı");
        yield return new WaitForSecondsRealtime(0.5f);
        var panel = T("CardWorkshopPanel").GetProperty("Current").GetValue(null) as Component;
        Assert.IsNull(GameObject.Find("SpeedUpHeader"), "boş kuyrukta HIZLANDIR başlığı yok");
        foreach (var r in Ratios)
        {
            CaptureUi("S2_7_workshop_" + r.tag, r.w, r.h, () => panel.GetType().GetMethod("RefitForPixels").Invoke(panel, new object[] { (float)r.w, (float)r.h }));
            yield return null;
        }
        var ribbon = GameObject.Find("Ribbon");
        var close = panel.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Close");
        var body = panel.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Panel");
        Assert.NotNull(close); Assert.NotNull(body);
        var pr = ScreenRect(body); var cr = ScreenRect(close);
        Assert.IsTrue(cr.yMax <= pr.yMax + 0.5f && cr.xMax <= pr.xMax + 0.5f, "X paneli taşmıyor");
        Call("CardWorkshopPanel", "Close");
    }
}
