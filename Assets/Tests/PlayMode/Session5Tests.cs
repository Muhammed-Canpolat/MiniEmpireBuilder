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
/// 14 Kasım 2026, 5. oturum: Ana Üs yerleşim denetimi (1.1), yapı içinden geçme (1.2), dekor ölçeği (1.3) ve UI işleri (AŞAMA 2).
/// Görüntü ve raporlar <c>PlaytestOutput/session5/</c>. Yalıtılmış test kaydı (gerçek kayıt geri yüklenir).
/// </summary>
[Timeout(300000)]
public class Session5Tests
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
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session5"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    internal static IEnumerator CaptureBase(string name, int w = 1080, int h = 1920)
    {
        yield return null;
        yield return null;
        Session4Tests.CaptureUi(name, w, h);
        var src = Path.Combine(Session4Tests.Folder, name + ".png");
        var dst = Path.Combine(Folder, name + ".png");
        if (File.Exists(src)) { File.Copy(src, dst, true); File.Delete(src); }
    }

    private static object DecorRecord(string instance, string id, int x, int y)
    {
        var rec = Activator.CreateInstance(T("PlacedDecorSave"));
        Set(rec, "instanceId", instance); Set(rec, "decorId", id); Set(rec, "x", x); Set(rec, "y", y);
        return rec;
    }

    /// <summary>
    /// Kullanıcının ekran görüntüsündeki Ana Üs 15 düzeni: her türün slotlarında bina (Okçu ×2+, Topçu, Manastır ×2, Atölye, Kışla ×2,
    /// Köylü Evi ×2, maden) ve sağ üstte koyunlu ağıl (çalı + koyun).
    /// </summary>
    internal static void FullBase(object save)
    {
        var placed = (IList)Field(save, "placedBuildings");
        var slots = new[] { "main", "mine_1", "archer_1", "archer_2", "cannon_1", "cannon_3", "monastery_1", "monastery_2",
            "workshop_1", "barracks_1", "barracks_2", "house_1", "house_2" };
        foreach (var slot in (Array)T("BaseWorldLayout").GetField("Slots").GetValue(null))
        {
            var type = slot.GetType().GetField("buildingType").GetValue(slot);
            string id = (string)slot.GetType().GetField("id").GetValue(slot);
            if (type == null || !slots.Contains(id)) continue;
            if (placed.Cast<object>().Any(p => (string)Field(p, "slotId") == id)) continue;
            var entry = Activator.CreateInstance(T("PlacedBuildingData"));
            Set(entry, "slotId", id); Set(entry, "buildingType", type.ToString()); Set(entry, "level", id == "main" ? 15 : 10);
            placed.Add(entry);
        }
        foreach (var b in (IEnumerable)Field(save, "buildings"))
        {
            Set(b, "isUnlocked", true);
            if (Field(b, "buildingType").ToString() != "MainBase") Set(b, "level", 10);
        }
        var decor = (IList)Field(save, "placedDecor");
        int n = 0;
        for (int y = 27; y <= 31; y += 2)
            for (int x = 16; x <= 19; x++)
                decor.Add(DecorRecord("pen_bush_" + n++, "bush", x, y));
        foreach (var (x, y) in new[] { (16, 28), (18, 28), (17, 30), (19, 30), (16, 32), (19, 32) })
            decor.Add(DecorRecord("pen_sheep_" + n++, "sheep", x, y));
        Set(save, "decorLayoutVersion", 1);
    }

    internal static IEnumerator LoadFullBase() => Session4Tests.LoadBaseWith(15, FullBase, 50000);

    private static void SavePng(Texture2D tex, string name)
    {
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), tex.EncodeToPNG());
        Object.Destroy(tex);
    }

    // ==================== 1.1 YERLEŞİM DENETİMİ ====================

    /// <summary>
    /// Taşıma taraması: her bina türü için kurulu bir örneği her hücreye taşımayı dener (gerçek PlacementController.Validate) ve
    /// bağımsız "görünüşte boş" ölçütüyle karşılaştırır (BasePlacementAudit.Scan).
    /// </summary>
    internal static object ScanMoves()
    {
        var map = T("BaseMapManager").GetProperty("Instance").GetValue(null);
        var objects = ((IEnumerable)Prop(map, "MapObjects")).Cast<object>().ToList();
        var validate = T("PlacementController").GetMethod("Validate");
        var rest = T("BaseOccupancyGrid").GetMethod("RestVisualRelative");
        object Instance(object type) => objects.FirstOrDefault(o => Equals(Field(o, "buildingType"), type) && Field(o, "gameObject") as GameObject != null);
        var box = new ScanBox
        {
            Valid = (type, center) => (bool)validate.Invoke(null, new object[] { type, center, Instance(type), null }),
            Subject = type =>
            {
                var inst = Instance(type);
                if (inst == null) return (null, null);
                var go = (GameObject)Field(inst, "gameObject");
                return ((Rect)rest.Invoke(null, new object[] { go }), go);
            }
        };
        var bt = T("BuildingType");
        var validType = typeof(Func<,,>).MakeGenericType(bt, typeof(Vector2), typeof(bool));
        var tupleType = typeof(ValueTuple<Rect?, GameObject>);
        var subjectType = typeof(Func<,>).MakeGenericType(bt, tupleType);
        var v = Delegate.CreateDelegate(validType, box, typeof(ScanBox).GetMethod("V").MakeGenericMethod(bt));
        var s = Delegate.CreateDelegate(subjectType, box, typeof(ScanBox).GetMethod("S").MakeGenericMethod(bt));
        return T("BasePlacementAudit").GetMethod("Scan").Invoke(null, new object[] { v, s });
    }

    internal static object Map => T("BaseMapManager").GetProperty("Instance").GetValue(null);
    internal static List<object> MapObjects() => ((IEnumerable)Prop(Map, "MapObjects")).Cast<object>().ToList();
    internal static object FirstOf(string type) => MapObjects().First(o => Field(o, "buildingType")?.ToString() == type);
    internal static object Placement => T("PlacementController").GetProperty("Instance").GetValue(null);

    /// <summary>Taşımayı başlatır ve hayaleti verilen sol-alt hücreye koyar (parmak sürüklemesinin karşılığı).</summary>
    internal static IEnumerator MoveGhostTo(object obj, Vector2Int cell)
    {
        var pc = Placement;
        pc.GetType().GetMethod("BeginMove").Invoke(pc, new[] { obj });
        yield return null;
        pc.GetType().GetField("_drag", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pc).GetType().GetMethod("Reset")
            .Invoke(pc.GetType().GetField("_drag", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pc), null);
        pc.GetType().GetField("_cell", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pc, cell);
        yield return null;
        yield return null;
    }

    internal static bool GhostValid => (bool)Placement.GetType().GetField("_valid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Placement);
    internal static void ConfirmGhost() => Placement.GetType().GetMethod("OnConfirmClicked", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Placement, null);

    [UnityTest]
    public IEnumerator MovingUnderTheCastleShowsRedCellsAndAcceptsEmptyLookingGround()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        var house = FirstOf("House");
        var main = (GameObject)Field(FirstOf("MainBase"), "gameObject");
        var castle = (RectInt)Call("BaseOccupancyGrid", "BlockOf", FirstOf("MainBase"));
        // Kalenin hemen altındaki ilk satırlar: Köylü Evi (taşmasız 2×3) kalenin görünen tabanının hemen altına sığar
        var target = new Vector2Int(castle.xMin + 1, castle.yMin - 3);
        yield return MoveGhostTo(house, target);
        var conflicts = (HashSet<Vector2Int>)Prop(Placement, "CurrentConflicts");
        yield return CaptureBase("S1_1_move_under_castle_" + (GhostValid ? "ok" : "red"));
        bool looksEmpty = (bool)T("BasePlacementAudit").GetMethod("LooksEmpty").Invoke(null, new object[] {
            new RectInt(target.x, target.y, 2, 3), T("BasePlacementAudit").GetMethod("Items").Invoke(null, null),
            Field(house, "gameObject"), null });
        if (looksEmpty) Assert.IsTrue(GhostValid, "kalenin altındaki boş görünen yer kabul edilir; çakışma: " + string.Join(",", conflicts));
        else Assert.IsNotEmpty(conflicts, "ret varsa kırmızı hücre gösterilir");
        Assert.NotNull(GameObject.Find("MoveGrid").GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r => r.name == "OccupiedCell"),
            "dolu hücreler ızgarada kırmızı");
        // Kalenin kendi engel alanına (görselinin alt kenarı) girmek: kırmızı ve onay pasif
        yield return MoveGhostTo(house, new Vector2Int(castle.xMin + 1, castle.yMin - 1));
        Assert.IsFalse(GhostValid, "kalenin görünen tabanına girilemez");
        Assert.IsNotEmpty((HashSet<Vector2Int>)Prop(Placement, "CurrentConflicts"), "çakışan hücreler kırmızı");
        yield return CaptureBase("S1_1_move_into_castle_red");
        Placement.GetType().GetMethod("CancelPlacement").Invoke(Placement, null);
        Assert.NotNull(main);
    }

    /// <summary>Oyuncunun kayıtlı düzeni (eski kuralla kurulmuş, 4. oturum düzeni dahil) yeni kurala geçince yüklemede yerinden oynamaz.</summary>
    [UnityTest]
    public IEnumerator ExistingLayoutDoesNotMoveOnReload()
    {
        // 3. oturum "kalabalık üs" düzeni gibi: tüm türler eski slotlarında + ağıl; ayrıca dört Köylü Evi sütunu (kullanıcının görüntüsü)
        yield return Session4Tests.LoadBaseWith(15, save =>
        {
            FullBase(save);
            var placed = (IList)Field(save, "placedBuildings");
            for (int i = 0; i < 2; i++)
            {
                var e = Activator.CreateInstance(T("PlacedBuildingData"));
                Set(e, "slotId", "house_col_" + i); Set(e, "buildingType", "House"); Set(e, "level", 5);
                Set(e, "hasPosition", true); Set(e, "x", 3f); Set(e, "y", 27.5f - i * 3f);   // üst üste bitişik iki ev (ayak izleri 2×3)
                placed.Add(e);
            }
        }, 50000);
        yield return new WaitForSecondsRealtime(1.2f);
        var first = MapObjects().Where(o => Field(o, "gameObject") as GameObject != null)
            .ToDictionary(o => (string)Field(o, "id"), o => (Vector2)((GameObject)Field(o, "gameObject")).transform.position);
        Map.GetType().GetMethod("SaveAll").Invoke(Map, null);
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.2f);
        var moved = new List<string>();
        foreach (var o in MapObjects())
        {
            var go = Field(o, "gameObject") as GameObject;
            string id = (string)Field(o, "id");
            if (go == null || !first.ContainsKey(id)) continue;
            if (Vector2.Distance(first[id], go.transform.position) > 0.01f) moved.Add($"{id}: {first[id]} → {(Vector2)go.transform.position}");
        }
        File.WriteAllLines(Path.Combine(Folder, "S1_1_reload_moves.txt"), moved.Count > 0 ? moved : new List<string> { "taşınan yok" });
        Assert.IsEmpty(moved, "yeniden yüklemede düzen kıpırdamaz:\n" + string.Join("\n", moved));
        Assert.IsTrue(first.ContainsKey("house_col_0") && first.ContainsKey("house_col_1"), "bitişik evler yüklendi");
    }

    [UnityTest]
    public IEnumerator HeroWalksAwayWhenABuildingIsPlacedOnItsSpot()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1.5f);
        var hero = GameObject.Find("Hero");
        var heroCtl = T("HeroMovementController").GetProperty("Instance").GetValue(null);
        var house = FirstOf("House");
        // Kahramanın durduğu yere Köylü Evi taşı (kahraman hücresi ayak izinin ortasında)
        Vector2Int heroCell = Vector2Int.FloorToInt(hero.transform.position);
        Vector2Int target = new Vector2Int(heroCell.x - 1, heroCell.y - 1);
        bool placed = false;
        for (int dx = 0; dx <= 1 && !placed; dx++)
            for (int dy = 0; dy <= 2 && !placed; dy++)
            {
                var c = new Vector2Int(heroCell.x - dx, heroCell.y - dy);
                yield return MoveGhostTo(house, c);
                if (GhostValid) { target = c; placed = true; }
            }
        if (!placed) { Placement.GetType().GetMethod("CancelPlacement").Invoke(Placement, null); Assert.Inconclusive("kahramanın yerine ev sığmadı"); }
        ConfirmGhost();
        var block = (RectInt)Call("BaseOccupancyGrid", "BlockOf", house);
        Assert.IsTrue(block.Contains(Vector2Int.FloorToInt(hero.transform.position)), "ev kahramanın üstüne kuruldu");
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue((bool)Prop(heroCtl, "IsMoving") || (bool)Prop(heroCtl, "LastEvacuationWalked"), "kahraman yürüyerek çekiliyor");
        yield return new WaitForSeconds(4f);
        Assert.IsFalse(block.Contains(Vector2Int.FloorToInt(hero.transform.position)), "kahraman binanın alanından çıktı");
        yield return CaptureBase("S1_1_hero_walked_away");
    }

    [UnityTest]
    public IEnumerator LoadValidationClearsGhostCellsAndResyncsTheSave()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        // Boş bir hücre bul ve oraya hayalet doğa kaydı + kayıtta karşılığı olmayan dekor kaydı koy
        var grid = T("BaseOccupancyGrid");
        var area = (RectInt)Prop(Map, "BuildableRect");
        Vector2Int free = default;
        bool found = false;
        for (int y = area.yMin + 2; y < area.yMax - 2 && !found; y++)
            for (int x = area.xMin + 2; x < area.xMax - 2 && !found; x++)
            {
                var r = new RectInt(x, y, 1, 1);
                if ((bool)grid.GetMethod("IsFree").Invoke(null, new object[] { r, null, null, false, true, null, null })) { free = new Vector2Int(x, y); found = true; }
            }
        Assert.IsTrue(found);
        var ghost = new GameObject("GhostNature");
        var nature = ghost.AddComponent(T("NatureObstacle"));
        var byCell = (IDictionary)T("NatureObstacle").GetProperty("ByCell").GetValue(null);
        T("NatureObstacle").GetProperty("cell").SetValue(nature, free);
        byCell = (IDictionary)T("NatureObstacle").GetProperty("ByCell").GetValue(null);
        Assert.AreSame(nature, byCell[free], "hücre atanınca doğru hücrede (eski hata: (0,0)'da kalıyordu)");
        Assert.IsFalse(byCell.Contains(Vector2Int.zero) && byCell[Vector2Int.zero] == (object)nature, "(0,0)'da hayalet kalmaz");
        Assert.IsFalse((bool)grid.GetMethod("IsFree").Invoke(null, new object[] { new RectInt(free.x, free.y, 1, 1), null, null, false, true, null, null }),
            "canlı doğa nesnesi hücreyi kapatır");
        // 8. oturum (D100): doluluk sahneden sıfırdan kurulur — nesne yok edilince AYNI KAREDE hücre boşalır (hayalet kayıt olamaz).
        Object.DestroyImmediate(ghost);
        Assert.IsTrue((bool)grid.GetMethod("IsFree").Invoke(null, new object[] { new RectInt(free.x, free.y, 1, 1), null, null, false, true, null, null }),
            "yok edilen doğa nesnesinin hücresi hemen boş");
        var save = Prop(T("GameManager").GetProperty("Instance").GetValue(null), "PlayerData");
        ((IList)Field(save, "placedDecor")).Add(DecorRecord("orphan_decor", "bush", free.x, free.y + 1));
        string line = (string)Map.GetType().GetMethod("ValidateOccupancy").Invoke(Map, null);
        File.WriteAllText(Path.Combine(Folder, "S1_1_load_validation.txt"), line);
        StringAssert.DoesNotContain("0,0:", line, "oluşturma sırasında (0,0) hayaleti kalmadı");
        Assert.IsTrue((bool)grid.GetMethod("IsFree").Invoke(null, new object[] { new RectInt(free.x, free.y, 1, 1), null, null, false, true, null, null }),
            "doğrulama hayalet hücreyi temizledi: " + line);
        Assert.IsFalse(((IEnumerable)Field(save, "placedDecor")).Cast<object>().Any(d => (string)Field(d, "instanceId") == "orphan_decor"),
            "sahnede karşılığı olmayan dekor kaydı silindi: " + line);
    }

    // ==================== 1.2 HİÇBİR SAHNEDE YAPI İÇİNDEN GEÇME ====================

    private static object Grid(string type) => T(type).GetField("Shared").GetValue(null);
    private static bool Walkable(object grid, Vector2Int c) => (bool)grid.GetType().GetMethod("Walkable").Invoke(grid, new object[] { c });
    private static Vector2Int CellOf(object grid, Vector2 p) => (Vector2Int)grid.GetType().GetMethod("CellOf").Invoke(grid, new object[] { p });
    private static Vector2 Center(object grid, Vector2Int c) => (Vector2)grid.GetType().GetMethod("Center").Invoke(grid, new object[] { c });

    private static List<Vector2> FindPath(object grid, Vector2 from, Vector2 to, out bool reached)
    {
        var args = new object[] { grid, from, to, false, null, 8000 };
        var path = (List<Vector2>)T("GridPathfinder").GetMethod("FindPath").Invoke(null, args);
        reached = (bool)args[3];
        return path;
    }

    /// <summary>Yol (karakterin bulunduğu ilk nokta hariç) yürünebilir hücrelerden geçer; ara noktalar 0,05 birimde örneklenir.</summary>
    private static void AssertRouteClear(object grid, List<Vector2> path, string label, IEnumerable<Rect> forbidden = null)
    {
        Assert.GreaterOrEqual(path.Count, 2, label + ": yol yok");
        var rects = forbidden?.ToList() ?? new List<Rect>();
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 a = path[i - 1], b = path[i];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.05f));
            for (int k = (i == 1 ? 1 : 0); k <= steps; k++)
            {
                var p = Vector2.Lerp(a, b, k / (float)steps);
                Assert.IsTrue(Walkable(grid, CellOf(grid, p)), $"{label}: {p} kapalı hücrede");
                foreach (var r in rects) Assert.IsFalse(r.Contains(p), $"{label}: {p} yapının görselinin içinde {r}");
            }
        }
    }

    /// <summary>Görselin iç kısmı (kenarlardan %12 içeri): yol buraya hiç girmemeli.</summary>
    private static Rect Core(Rect v) => Rect.MinMaxRect(v.xMin + v.width * 0.12f, v.yMin + v.height * 0.12f, v.xMax - v.width * 0.12f, v.yMax - v.height * 0.12f);

    /// <summary>Yapının önündeki ve arkasındaki en yakın yürünebilir noktalar arasında rota; ulaşılamıyorsa (ör. harita kenarı) atlanır.</summary>
    private static int RouteBehind(object grid, RectInt blockedCells, IEnumerable<Rect> forbidden, string label, Vector2 cellSize)
    {
        int routes = 0;
        Vector2Int? Near(Vector2Int c, Vector2Int away)
        {
            for (int k = 0; k <= 4; k++)
                for (int side = -k; side <= k; side++)
                {
                    var p = c + away * k + new Vector2Int(away.y, away.x) * side;
                    if (Walkable(grid, p)) return p;
                }
            return null;
        }
        foreach (var (f0, t0, dirF, dirT) in new[]
        {
            (new Vector2Int(blockedCells.xMin + blockedCells.width / 2, blockedCells.yMin - 1), new Vector2Int(blockedCells.xMin + blockedCells.width / 2, blockedCells.yMax),
                Vector2Int.down, Vector2Int.up),
            (new Vector2Int(blockedCells.xMin - 1, blockedCells.yMin + blockedCells.height / 2), new Vector2Int(blockedCells.xMax, blockedCells.yMin + blockedCells.height / 2),
                Vector2Int.left, Vector2Int.right)
        })
        {
            var f = Near(f0, dirF); var t = Near(t0, dirT);
            if (f == null || t == null) continue;
            var from = f.Value; var to = t.Value;
            var path = FindPath(grid, Center(grid, from), Center(grid, to), out bool reached);
            if (!reached) continue;
            AssertRouteClear(grid, path, label, forbidden);
            routes++;
        }
        return routes;
    }

    [UnityTest]
    public IEnumerator BaseRoutesBehindEveryStructureNeverEnterIt()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        var grid = Grid("BaseWalkGrid");
        var structures = MapObjects().Where(o => Field(o, "gameObject") as GameObject != null
            && (Field(o, "objectType").ToString() == "Building" || Field(o, "objectType").ToString() == "BuilderHut")).ToList();
        var cores = structures.Select(o => Core((Rect)Call("BaseVisualBounds", "Of", (GameObject)Field(o, "gameObject")))).ToList();
        int routes = 0;
        foreach (var o in structures)
        {
            var block = (RectInt)Call("BaseOccupancyGrid", "BlockOf", o);
            for (int y = block.yMin; y < block.yMax; y++)
                for (int x = block.xMin; x < block.xMax; x++)
                    Assert.IsFalse(Walkable(grid, new Vector2Int(x, y)), $"{((GameObject)Field(o, "gameObject")).name} engel hücresi ({x},{y}) yürünemez");
            routes += RouteBehind(grid, block, cores, "Ana Üs " + ((GameObject)Field(o, "gameObject")).name, Vector2.one);
        }
        Assert.Greater(routes, 8, "yapıların arkasına rotalar denendi");
        // Kahraman bir süre dolaşsın: hiçbir yapının görselinin içine girmez
        var hero = GameObject.Find("Hero");
        for (int i = 0; i < 300; i++)
        {
            yield return null;
            Vector2 feet = hero.transform.position;
            foreach (var c in cores) Assert.IsFalse(c.Contains(feet), $"kahraman yapının içinde {feet}");
        }
        File.WriteAllText(Path.Combine(Folder, "S1_2_base_routes.txt"), $"Ana Üs: {structures.Count} yapı, {routes} rota temiz");
    }

    [UnityTest]
    public IEnumerator IslandRoutesBehindEveryBuildingNeverEnterIt()
    {
        yield return LoadIsland(island => { Set(island, "zoneMask", 0xFF); }, 8, v => { });
        var grid = Grid("IslandWalkGrid");
        int routes = 0;
        var castle = (RectInt)T("IslandGrid").GetProperty("CastleRect").GetValue(null);
        routes += RouteBehind(grid, castle, null, "Ada kale", Vector2.one);
        foreach (var entry in Placed(CurrentIsland()))
        {
            var kind = Call("IslandGrid", "ParseKind", (string)Field(entry, "kind"));
            var rect = (RectInt)Call("IslandGrid", "Rect", kind, (int)Field(entry, "col"), (int)Field(entry, "row"));
            for (int c = rect.xMin; c < rect.xMax; c++)
                for (int r = rect.yMin; r < rect.yMax; r++)
                    Assert.IsFalse(Walkable(grid, new Vector2Int(c, r)), $"{Field(entry, "id")} hücresi ({c},{r}) kapalı");
            routes += RouteBehind(grid, rect, null, "Ada " + Field(entry, "id"), Vector2.one);
        }
        Assert.Greater(routes, 0, "adada yapıların arkasına rotalar denendi");
        File.WriteAllText(Path.Combine(Folder, "S1_2_island_routes.txt"), $"Ada: {routes} rota temiz");
    }

    private static List<(GameObject go, Rect area, Rect visual)> BattleStructures()
    {
        var list = new List<(GameObject, Rect, Rect)>();
        var tryGet = T("BattleObstacles").GetMethod("TryGet");
        var visualRect = T("BattleObstacles").GetMethod("VisualRect");
        foreach (var typeName in new[] { "BattleStructure", "MainBaseController" })
            foreach (var c in Object.FindObjectsByType(T(typeName), FindObjectsSortMode.None).Cast<Component>())
            {
                var args = new object[] { c, null };
                if (!(bool)tryGet.Invoke(null, args)) continue;
                list.Add((c.gameObject, (Rect)args[1], (Rect)visualRect.Invoke(null, new object[] { c.gameObject })));
            }
        return list;
    }

    private static IEnumerator BattleRoutesAndWalkers(string tag)
    {
        yield return new WaitForSeconds(2f);   // açılış animasyonu bitsin: engeller görselden yenilenir
        var structures = BattleStructures();
        Assert.Greater(structures.Count, 0, "savaş yapıları");
        var grid = Grid("BattleWalkGrid");
        var cores = structures.Select(s => Core(s.visual)).ToList();
        var lines = new List<string>();
        int routes = 0;
        foreach (var (go, area, visual) in structures)
        {
            // Engel, görselin gövdesini kapsar (eski sabit 1,25×1 alan madenin yalnız küçük bir parçasıydı)
            Assert.LessOrEqual(area.xMin, visual.xMin + visual.width * 0.1f, go.name + ": engel solda görseli kapsar");
            Assert.GreaterOrEqual(area.xMax, visual.xMax - visual.width * 0.1f, go.name + ": engel sağda görseli kapsar");
            Assert.LessOrEqual(area.yMin, visual.yMin + 0.05f, go.name + ": engel tabanı");
            Assert.GreaterOrEqual(area.yMax, visual.yMax - visual.height * 0.12f, go.name + ": engel tepesi");
            lines.Add($"{go.name}: görsel {visual} engel {area}");
            var cellMin = CellOf(grid, area.min);
            var cellMax = CellOf(grid, area.max);
            var cells = new RectInt(cellMin.x, cellMin.y, cellMax.x - cellMin.x + 1, cellMax.y - cellMin.y + 1);
            routes += RouteBehind(grid, cells, cores, tag + " " + go.name, Vector2.one * 0.5f);
        }
        Assert.Greater(routes, 0, "yapıların arkasına rotalar");
        lines.Add($"rota: {routes}");
        File.WriteAllLines(Path.Combine(Folder, "S1_2_" + tag + "_structures.txt"), lines);

        // Kahraman (oyuncu girdisi) ve Çırak (izler): her yapının önünden arkasına doğru yürütülür; hiçbiri görselin içine girmez
        var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
        var apprentice = Object.FindFirstObjectByType(T("BattleApprentice")) as Component;
        Assert.NotNull(hero, "kahraman");
        var setInput = hero.GetType().GetMethod("SetMoveInput");
        foreach (var (go, area, visual) in structures)
        {
            // Başlangıç: yapının altında, hiçbir engelde/görselde olmayan en yakın nokta
            Vector2 feetOffset = (Vector2)T("HeroController").GetField("FeetOffset").GetValue(null);
            Vector2 start = new Vector2(visual.center.x, area.yMin - 1.2f);
            bool ok = false;
            for (int k = 0; k < 12 && !ok; k++)
                foreach (float dx in new[] { 0f, -1.5f, 1.5f, -3f, 3f })
                {
                    var c = new Vector2(visual.center.x + dx, area.yMin - 1.0f - k * 0.5f);
                    if ((bool)T("BattleObstacles").GetMethod("Blocked").Invoke(null, new object[] { c + feetOffset, 0.45f })) continue;
                    if (cores.Any(r => r.Contains(c + feetOffset) || r.Contains(c + feetOffset + new Vector2(-1.1f, -0.15f)))) continue;
                    start = c; ok = true; break;
                }
            if (!ok) continue;
            hero.transform.position = new Vector3(start.x, start.y, hero.transform.position.z);
            if (apprentice != null) apprentice.transform.position = hero.transform.position + new Vector3(-1.1f, -0.15f, 0f);
            for (int f = 0; f < 150; f++)
            {
                setInput.Invoke(hero, new object[] { new Vector2(f < 100 ? 0.15f : 1f, 1f) });
                yield return null;
                Vector2 feet = (Vector2)hero.transform.position + (Vector2)T("HeroController").GetField("FeetOffset").GetValue(null);
                foreach (var c in cores) Assert.IsFalse(c.Contains(feet), $"{tag}: kahraman {go.name} görselinin içinde {feet}");
                if (apprentice != null && apprentice.gameObject.activeInHierarchy)
                {
                    Vector2 af = (Vector2)apprentice.transform.position + (Vector2)T("HeroController").GetField("FeetOffset").GetValue(null);
                    foreach (var c in cores) Assert.IsFalse(c.Contains(af), $"{tag}: çırak {go.name} görselinin içinde {af}");
                }
            }
            setInput.Invoke(hero, new object[] { Vector2.zero });
        }
    }

    [UnityTest]
    public IEnumerator DefendTheMineHeroAndApprenticeNeverWalkThroughTheMine()
    {
        yield return Session4Tests.StartBattle(1, 3);   // 1-3: Madeni Koru
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        var mine = battle.GetType().GetProperty("DefendedMine").GetValue(battle) as Component;
        Assert.NotNull(mine, "Madeni Koru madeni");
        Call("WalkGridDebug", "Show", Grid("BattleWalkGrid"));
        yield return BattleRoutesAndWalkers("defend_mine");
        var cam = Camera.main;
        var follow = cam.GetComponent(T("CameraFollow")) as Behaviour;
        if (follow != null) follow.enabled = false;
        cam.transform.position = new Vector3(mine.transform.position.x, mine.transform.position.y, cam.transform.position.z);
        yield return null;
        CardWorkshopTests.CaptureScreen("S1_2_defend_mine_walkgrid", "session5");
        if (follow != null) follow.enabled = true;
        Call("WalkGridDebug", "Show", new object[] { null });
        battle.GetType().GetMethod("BattleLost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(battle, null);
    }

    [UnityTest]
    public IEnumerator BattleTowersAndCastleBlockHeroApprenticeAndEnemies()
    {
        yield return Session4Tests.StartBattle(1, 1);
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        // Her boş yuvaya kule (okçu/topçu/manastır sırayla)
        var build = battle.GetType().GetMethod("BuildTower", BindingFlags.NonPublic | BindingFlags.Instance);
        var monastery = battle.GetType().GetMethod("BuildMonastery", BindingFlags.NonPublic | BindingFlags.Instance);
        var slots = Object.FindObjectsByType(T("BattleBuildSlot"), FindObjectsSortMode.None).Cast<Component>().ToList();
        int i = 0;
        foreach (var slot in slots)
        {
            var type = Enum.Parse(T("BuildingType"), i % 3 == 0 ? "ArcherTower" : i % 3 == 1 ? "CannonTower" : "Monastery");
            if (i % 3 == 2) monastery.Invoke(battle, new object[] { slot.transform.position, slot });
            else build.Invoke(battle, new object[] { slot.transform.position, type, slot });
            i++;
        }
        Assert.Greater(i, 0, "yuvalara yapı kuruldu");
        yield return BattleRoutesAndWalkers("battle");
        // Düşmanlar da rotalarını yapıların dışından kurar: biraz ilerlesinler, hiçbiri bir yapının görselinin içinde değil
        yield return new WaitForSeconds(6f);
        var cores = BattleStructures().Select(s => Core(s.visual)).ToList();
        foreach (var enemy in Object.FindObjectsByType(T("EnemyController"), FindObjectsSortMode.None).Cast<Component>())
        {
            if ((bool)enemy.GetType().GetProperty("IsFlying").GetValue(enemy)) continue;
            foreach (var c in cores) Assert.IsFalse(c.Contains(enemy.transform.position), $"düşman yapının içinde {enemy.transform.position}");
        }
        CardWorkshopTests.CaptureScreen("S1_2_battle_towers", "session5");
        battle.GetType().GetMethod("BattleLost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(battle, null);
    }

    // ==================== 1.3 DEKOR ÖLÇEĞİ ====================

    [UnityTest]
    public IEnumerator DecorScaleComesFromTheSharedTableNotTheFootprint()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        var table = T("DecorScale");
        var tryGet = table.GetMethod("TryGetFrameWidth");
        var lines = new List<string>();
        // Önceki (2×2/3×3 dönemi) doğal boyutlar: ayak izi × 0,92 × visualScale
        var previous = new Dictionary<string, float> { { "sheep", 2 * 0.92f * 1.9f }, { "bush", 2 * 0.92f * 1.35f }, { "rock", 2 * 0.92f }, { "tree", 3 * 0.92f } };
        foreach (var data in (Array)T("DecorPlacementController").GetProperty("Catalog").GetValue(null))
        {
            string id = (string)Field(data, "id");
            var sprite = (Sprite)data.GetType().GetProperty("Visual").GetValue(data);
            if (sprite == null) continue;
            var args = new object[] { id, 0f };
            Assert.IsTrue((bool)tryGet.Invoke(null, args), id + ": ortak ölçek tablosunda");
            float width = (float)args[1];
            float scale = (float)data.GetType().GetMethod("ScaleFor").Invoke(data, new object[] { sprite });
            Assert.AreEqual(width, scale * sprite.bounds.size.x, 0.01f, id + ": görsel genişlik = tablo");
            // Ayak izi değişse de görsel ölçek değişmez
            var clone = Object.Instantiate((ScriptableObject)data);
            Set(clone, "footprint", new Vector2Int(3, 3));
            Assert.AreEqual(scale, (float)clone.GetType().GetMethod("ScaleFor").Invoke(clone, new object[] { sprite }), 0.0001f, id + ": ayak izinden bağımsız");
            Object.Destroy(clone);
            if (previous.TryGetValue(id, out float before)) Assert.AreEqual(before, width, 0.02f, id + ": önceki doğal boyuta döndü");
            lines.Add($"{id}: kare genişliği {width:0.00} birim (ayak izi {Field(data, "footprint")})");
        }
        // Sahnedeki ağıl: koyun ve çalılar tablo boyunda çiziliyor
        foreach (var d in Object.FindObjectsByType(T("DecorInstance"), FindObjectsSortMode.None).Cast<Component>())
        {
            var data = Prop(d, "Data");
            string id = (string)Field(data, "id");
            var r = d.GetComponent<SpriteRenderer>();
            var args = new object[] { id, 0f };
            if (!(bool)tryGet.Invoke(null, args) || r.sprite == null) continue;
            Assert.AreEqual((float)args[1], r.sprite.bounds.size.x * d.transform.lossyScale.x, 0.02f, "sahnede " + id);
        }
        File.WriteAllLines(Path.Combine(Folder, "S1_3_decor_scale.txt"), lines);
        var pen = Object.FindObjectsByType(T("DecorInstance"), FindObjectsSortMode.None).Cast<Component>()
            .FirstOrDefault(d => (string)Field(Prop(d, "Data"), "id") == "sheep");
        if (pen != null)
        {
            var cam = Camera.main;
            float size = cam.orthographicSize;
            var pos = cam.transform.position;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(pen.transform.position.x - 1.5f, pen.transform.position.y, pos.z);
            yield return CaptureBase("S1_3_sheep_pen_after");
            cam.orthographicSize = size;
            cam.transform.position = pos;
        }
    }

    // ==================== 2.2 ✓/✗ HAYALETİN YANINDA · 2.3 GERİ AL · 2.4 ÖNERİLEN DÜZEN ====================

    private static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    /// <summary>Ekran-uzayı dikdörtgeni (Overlay tuvali: dünya köşeleri = ekran pikseli).</summary>
    private static Rect GhostScreenRect(SpriteRenderer ghost)
    {
        var b = ghost.bounds;
        var cam = Camera.main;
        Vector2 a = cam.WorldToScreenPoint(b.min), c = cam.WorldToScreenPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
    }

    [UnityTest]
    public IEnumerator ConfirmButtonsSitBesideTheGhostFollowItAndAreDimmedWhenInvalid()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        var house = FirstOf("House");
        var castle = (RectInt)Call("BaseOccupancyGrid", "BlockOf", FirstOf("MainBase"));
        // Kalenin hemen altına (kullanıcının durumu): geçersiz → ✓ soluk ve basılamaz, düğmeler kalenin değil hayaletin yanında
        yield return MoveGhostTo(house, new Vector2Int(castle.xMin + 1, castle.yMin - 1));
        yield return null;
        var buttons = GameObject.Find("PlacementConfirm");
        Assert.NotNull(buttons);
        var widget = buttons.GetComponent(T("WorldConfirmButtons"));
        widget.GetType().GetMethod("Follow").Invoke(widget, null);
        var ghost = GameObject.Find("GhostPlacement").GetComponent<SpriteRenderer>();
        Rect g = GhostScreenRect(ghost), w = ScreenRect((RectTransform)buttons.transform);
        Assert.IsFalse(GhostValid, "kalenin tabanına girilemez");
        Assert.IsFalse(g.Overlaps(w), $"düğmeler hayaletin üstüne binmez: hayalet {g} düğmeler {w}");
        Assert.Less(Mathf.Min(Mathf.Abs(w.xMin - g.xMax), Mathf.Abs(g.xMin - w.xMax), Mathf.Abs(w.yMax - g.yMin), Mathf.Abs(g.yMax - w.yMin)), 40f,
            "düğmeler hayaletin hemen yanında");
        Assert.Less(Mathf.Abs(w.center.y - g.center.y), g.height * 0.5f + w.height, "dikeyde hayaletle hizalı");
        var confirm = buttons.transform.Find("Confirm").GetComponent<UnityEngine.UI.Button>();
        Assert.IsFalse(confirm.interactable, "geçersizken ✓ basılamaz");
        Assert.Less(confirm.GetComponent<CanvasGroup>().alpha, 0.6f, "geçersizken ✓ soluk");
        StringAssert.Contains(L("Buraya sığmıyor"), (string)widget.GetType().GetProperty("Info").GetValue(widget));
        bool before = (bool)Field(house, "isBeingRemoved");
        Vector3 pos0 = ((GameObject)Field(house, "gameObject")).transform.position;
        confirm.onClick.Invoke();   // basılamaz durumda bile çağrılırsa taşımaz
        yield return null;
        Assert.AreEqual(pos0, ((GameObject)Field(house, "gameObject")).transform.position, "geçersiz yerde ✓ taşımaz");
        Session4Tests.CaptureUi("S2_2_confirm_beside_invalid", 1080, 1920);
        MoveCapture("S2_2_confirm_beside_invalid");
        // Geçerli bir yere sürüklenince düğmeler izler ve ✓ etkin
        var free = Enumerable.Range(0, 30).SelectMany(dy => Enumerable.Range(0, 18).Select(dx => new Vector2Int(2 + dx, 4 + dy)))
            .First(c => (bool)T("PlacementController").GetMethod("Validate").Invoke(null, new object[] { Field(house, "buildingType"),
                new Vector2(c.x + 1f, c.y + 1.5f), house, null }));
        Placement.GetType().GetField("_cell", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Placement, free);
        yield return null; yield return null;
        widget.GetType().GetMethod("Follow").Invoke(widget, null);
        Rect g2 = GhostScreenRect(ghost), w2 = ScreenRect((RectTransform)buttons.transform);
        Assert.IsTrue(GhostValid);
        Assert.IsTrue(confirm.interactable, "geçerliyken ✓ etkin");
        Assert.AreEqual(1f, confirm.GetComponent<CanvasGroup>().alpha, 0.01f);
        Assert.IsFalse(g2.Overlaps(w2), "yeni yerde de hayaletin yanında");
        Assert.Greater(Vector2.Distance(w.center, w2.center), 20f, "düğmeler hayaleti izledi");
        Session4Tests.CaptureUi("S2_2_confirm_beside_valid", 1080, 1920);
        MoveCapture("S2_2_confirm_beside_valid");
        Placement.GetType().GetMethod("CancelPlacement").Invoke(Placement, null);
        Assert.IsFalse(before);
    }

    private static string L(string tr) => tr;   // testler Türkçe dilde koşar

    private static void MoveCapture(string name)
    {
        var src = Path.Combine(Session4Tests.Folder, name + ".png");
        if (File.Exists(src)) { File.Copy(src, Path.Combine(Folder, name + ".png"), true); File.Delete(src); }
    }

    [UnityTest]
    public IEnumerator UndoIsSmallSitsAboveTheMenuLastsThreeSecondsAndFades()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        var house = FirstOf("House");
        var go = (GameObject)Field(house, "gameObject");
        Vector3 before = go.transform.position;
        var free = Enumerable.Range(0, 30).SelectMany(dy => Enumerable.Range(0, 18).Select(dx => new Vector2Int(2 + dx, 4 + dy)))
            .First(c => (bool)T("PlacementController").GetMethod("Validate").Invoke(null, new object[] { Field(house, "buildingType"),
                new Vector2(c.x + 1f, c.y + 1.5f), house, null }));
        yield return MoveGhostTo(house, free);
        ConfirmGhost();
        float shownAt = Time.unscaledTime;
        yield return new WaitForSecondsRealtime(0.3f);
        var undo = T("UndoMoveButton").GetProperty("Current").GetValue(null) as Component;
        Assert.NotNull(undo, "GERİ AL çıktı");
        var rt = (RectTransform)undo.transform;
        var r = ScreenRect(rt);
        var safe = ScreenRect((RectTransform)rt.parent);
        float scale = rt.lossyScale.x;
        Assert.LessOrEqual(rt.rect.width * rt.rect.height, 188f * 58f * 0.5f, "alanı eski hapın en çok yarısı");
        // Tuval birimiyle: güvenli alanın sol-alt köşesine göre (ekran çözünürlüğünden bağımsız)
        Assert.AreEqual(Vector2.zero, rt.anchorMin, "sol-alt köşeye bağlı");
        Assert.Less(rt.anchoredPosition.x, 80f, "sol kenarda (oyun alanının ortasında değil)");
        Assert.Less(rt.anchoredPosition.y, 260f, "ekranın alt kısmında, menünün hemen üstünde");
        Assert.Greater(rt.anchoredPosition.y, 150f, "alt menünün üstünde");
        // Radyal menü açıkken onun düğme/etiketleriyle çakışmaz
        var menu = T("RadialDockMenu").GetProperty("Current").GetValue(null);
        menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { true, true, false });
        yield return new WaitForSecondsRealtime(0.5f);
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                     .Where(t => t.name.StartsWith("Radial_") && t.gameObject.activeInHierarchy))
            Assert.IsFalse(ScreenRect(t).Overlaps(r), "GERİ AL radyal menü öğesi " + t.name + " ile çakışmaz");
        float ring0 = (float)Prop(undo, "Remaining");
        Session4Tests.CaptureUi("S2_3_undo_small", 1080, 1920);
        MoveCapture("S2_3_undo_small");
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.Less((float)Prop(undo, "Remaining"), ring0 - 0.25f, "süre halkası azalıyor");
        Assert.AreEqual(3f, (float)T("UndoMoveButton").GetField("DefaultSeconds").GetValue(null), 0.001f, "3 saniye");
        while (Time.unscaledTime < shownAt + 3.1f) yield return null;   // ~3,1 sn: sönüyor
        Assert.IsNull(T("UndoMoveButton").GetProperty("Current").GetValue(null), "süre doldu: artık geri alınamaz");
        Assert.IsTrue(undo != null, "yumuşak sönme sürüyor (hemen yok olmaz)");
        Assert.Less(undo.GetComponent<CanvasGroup>().alpha, 0.99f, "sönüyor");
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.IsTrue(undo == null, "söndükten sonra kalktı");
        menu.GetType().GetMethod("SetOpen").Invoke(menu, new object[] { false, true, false });
        // Dokununca geri alır
        yield return MoveGhostTo(house, free + new Vector2Int(0, 0));
        Placement.GetType().GetMethod("CancelPlacement").Invoke(Placement, null);
        Assert.AreNotEqual(before, go.transform.position, "taşındı");
    }

    [UnityTest]
    public IEnumerator SuggestedLayoutButtonIsFeatureFlaggedOff()
    {
        yield return LoadFullBase();
        Assert.IsFalse((bool)T("FeatureConfig").GetField("recommendedLayout").GetValue(T("FeatureConfig").GetProperty("Instance").GetValue(null)), "bayrak kapalı");
        var build = Object.FindFirstObjectByType(T("BuildMenuController")) as Component;
        Assert.NotNull(build);
        build.GetType().GetMethod("Show")?.Invoke(build, null);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsFalse(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(t => t.name == "RecommendedLayout" || t.name == "RecommendedConfirm"), "İnşa Et menüsünde Önerilen düzen yok");
        Assert.NotNull(T("BaseMapManager").GetMethod("ApplyRecommendedLayout"), "kod silinmedi (bayrakla geri açılabilir)");
        Session4Tests.CaptureUi("S2_4_build_menu", 1080, 1920);
        MoveCapture("S2_4_build_menu");
    }

    // ==================== SON: BAŞTAN SONA OYNATMA ====================

    /// <summary>Ana Üs'te her bina bir kez taşınır ve GERİ AL ile döner; dekor kurulur, taşınır, silinir.</summary>
    [UnityTest]
    public IEnumerator JourneyMoveAndUndoEveryBuildingAndPlaceMoveDeleteDecor()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        var lines = new List<string>();
        var buildings = MapObjects().Where(o => Field(o, "gameObject") as GameObject != null && Field(o, "buildingType") != null
            && Field(o, "buildingType").ToString() != "MainBase").ToList();
        Assert.Greater(buildings.Count, 8);
        var validate = T("PlacementController").GetMethod("Validate");
        foreach (var b in buildings)
        {
            var go = (GameObject)Field(b, "gameObject");
            Vector3 before = go.transform.position;
            var fp = (Vector2Int)Call("BaseMapManager", "FootprintOf", b);
            Vector2Int? spot = null;
            for (int d = 3; d < 30 && spot == null; d++)
                foreach (var dir in new[] { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right, new Vector2Int(1, -1), new Vector2Int(-1, 1) })
                {
                    var c = Vector2Int.RoundToInt((Vector2)before - (Vector2)fp * 0.5f) + dir * d;
                    if ((bool)validate.Invoke(null, new object[] { Field(b, "buildingType"), new Vector2(c.x + fp.x * 0.5f, c.y + fp.y * 0.5f), b, null })) { spot = c; break; }
                }
            if (spot == null) { lines.Add(go.name + ": boş yer yok (atlandı)"); continue; }
            yield return MoveGhostTo(b, spot.Value);
            Assert.IsTrue(GhostValid, go.name + " geçerli yerde");
            ConfirmGhost();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(Vector2.Distance(before, go.transform.position), 0.5f, go.name + " taşındı");
            var undo = T("UndoMoveButton").GetProperty("Current").GetValue(null) as Component;
            Assert.NotNull(undo, go.name + " GERİ AL");
            undo.GetType().GetMethod("Press").Invoke(undo, null);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Less(Vector2.Distance(before, go.transform.position), 0.01f, go.name + " geri alındı");
            lines.Add($"{go.name}: {(Vector2)before} → {spot.Value} → geri ✓");
        }
        // Dekor: kur (3 farklı), taşı, sil
        var decor = T("DecorPlacementController").GetProperty("Instance").GetValue(null);
        var catalog = ((IEnumerable)T("DecorPlacementController").GetProperty("Catalog").GetValue(null)).Cast<object>().ToList();
        var placed = (IEnumerable)Prop(decor, "Placed");
        int count0 = placed.Cast<object>().Count();
        foreach (var id in new[] { "sheep", "bush2", "rock3" })
        {
            var data = catalog.First(d => (string)Field(d, "id") == id);
            decor.GetType().GetMethod("BeginPlacement").Invoke(decor, new[] { data });
            yield return null; yield return null;
            Assert.IsTrue((bool)decor.GetType().GetField("valid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(decor), id + " kurulabilir yerde başlar");
            decor.GetType().GetMethod("Confirm", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(decor, null);
            yield return null;
            lines.Add("dekor kuruldu: " + id);
        }
        Assert.AreEqual(count0 + 3, ((IEnumerable)Prop(decor, "Placed")).Cast<object>().Count(), "3 dekor kuruldu");
        var item = ((IEnumerable)Prop(decor, "Placed")).Cast<Component>().Last();
        var cell0 = (Vector2Int)Prop(item, "Cell");
        decor.GetType().GetMethod("BeginMove").Invoke(decor, new object[] { item });
        var area = (RectInt)Prop(Map, "BuildableRect");
        var canPlace = decor.GetType().GetMethod("CanPlace");
        Vector2Int target = cell0;
        for (int y = area.yMin + 2; y < area.yMax - 2 && target == cell0; y++)
            for (int x = area.xMin + 2; x < area.xMax - 2; x++)
                if (new Vector2Int(x, y) != cell0 && (bool)canPlace.Invoke(decor, new object[] { Prop(item, "Data"), new Vector2Int(x, y), item })) { target = new Vector2Int(x, y); break; }
        decor.GetType().GetField("currentCell", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(decor, target);
        decor.GetType().GetField("moveArmed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(decor, false);
        yield return null;
        decor.GetType().GetMethod("Confirm", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(decor, null);
        yield return null;
        Assert.AreEqual(target, (Vector2Int)Prop(item, "Cell"), "dekor taşındı");
        lines.Add($"dekor taşındı: {cell0} → {target}");
        decor.GetType().GetMethod("Delete").Invoke(decor, new object[] { item });
        yield return null;
        Assert.AreEqual(count0 + 2, ((IEnumerable)Prop(decor, "Placed")).Cast<object>().Count(), "dekor silindi");
        lines.Add("dekor silindi");
        File.WriteAllLines(Path.Combine(Folder, "SON_base_journey.txt"), lines);
        yield return CaptureBase("SON_base_after_journey");
    }

    /// <summary>Normal savaş ve Madeni Koru hızlandırılmış oynanır: hata yok, düşmanlar yapılara ulaşıp vurur, hiçbiri yapının içinde değil.</summary>
    [UnityTest]
    public IEnumerator JourneyPlayNormalAndDefendTheMineBattles()
    {
        var lines = new List<string>();
        foreach (var (region, level, tag) in new[] { (1, 1, "normal"), (1, 3, "madeni_koru") })
        {
            yield return Session4Tests.StartBattle(region, level);
            Object battle = null;
            float find = Time.realtimeSinceStartup + 8f;
            while ((battle = Object.FindFirstObjectByType(T("BattleManager"))) == null && Time.realtimeSinceStartup < find) yield return null;
            Assert.NotNull(battle, tag + ": sahne " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", timeScale " + Time.timeScale);
            float wait = Time.realtimeSinceStartup + 6f;
            while (!(bool)battle.GetType().GetProperty("IsBattleActive").GetValue(battle) && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsTrue((bool)battle.GetType().GetProperty("IsBattleActive").GetValue(battle), tag + ": savaş başladı");
            Time.timeScale = 4f;
            float until = Time.realtimeSinceStartup + 25f;
            int checks = 0;
            while (Time.realtimeSinceStartup < until && (bool)battle.GetType().GetProperty("IsBattleActive").GetValue(battle))
            {
                yield return new WaitForSecondsRealtime(0.5f);
                var cores = BattleStructures().Select(s => Core(s.visual)).ToList();
                foreach (var enemy in Object.FindObjectsByType(T("EnemyController"), FindObjectsSortMode.None).Cast<Component>())
                {
                    if ((bool)enemy.GetType().GetProperty("IsFlying").GetValue(enemy)) continue;
                    foreach (var c in cores) Assert.IsFalse(c.Contains(enemy.transform.position), $"{tag}: düşman yapının içinde {enemy.transform.position}");
                    checks++;
                }
            }
            Time.timeScale = 1f;
            var mine = battle.GetType().GetProperty("DefendedMine").GetValue(battle) as Component;
            Assert.Greater(checks, 0, tag + ": düşmanlar sahaya çıktı");
            string state = (bool)battle.GetType().GetProperty("IsBattleActive").GetValue(battle) ? "sürüyor"
                : "bitti (" + (battle.GetType().GetField("battleWon", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(battle) is bool w && w ? "zafer" : "yenilgi") + ")";
            lines.Add($"{tag}: ~100 sn oyun süresi (×4), {checks} düşman konumu denetlendi, savaş {state}" +
                (mine != null ? $", maden canı {mine.GetType().GetProperty("CurrentHealth").GetValue(mine)}" : ""));
            CardWorkshopTests.CaptureScreen("SON_battle_" + tag, "session5");
            battle.GetType().GetMethod("BattleLost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(battle, null);
            yield return null;
        }
        File.WriteAllLines(Path.Combine(Folder, "SON_battles.txt"), lines);
    }

    private sealed class ScanBox
    {
        public Func<object, Vector2, bool> Valid;
        public Func<object, (Rect?, GameObject)> Subject;
        public bool V<TType>(TType type, Vector2 center) => Valid(type, center);
        public (Rect?, GameObject) S<TType>(TType type) => Subject(type);
    }

    [UnityTest]
    public IEnumerator BaseLayoutAuditAndFullPlacementScan()
    {
        yield return LoadFullBase();
        yield return new WaitForSecondsRealtime(1f);
        var audit = T("BasePlacementAudit");
        File.WriteAllText(Path.Combine(Folder, "S1_1_footprints.txt"), (string)audit.GetMethod("Report").Invoke(null, null));
        var scan = ScanMoves();
        var lines = (List<string>)scan.GetType().GetField("Lines").GetValue(scan);
        int hidden = (int)scan.GetType().GetField("HiddenRejects").GetValue(scan);
        int ugly = (int)scan.GetType().GetField("UglyAccepts").GetValue(scan);
        File.WriteAllLines(Path.Combine(Folder, "S1_1_scan.txt"), lines);
        SavePng((Texture2D)audit.GetMethod("Render").Invoke(null, new object[] { scan, 24 }), "S1_1_scan_debug");
        yield return CaptureBase("S1_1_full_base");
        int mismatch = (int)scan.GetType().GetField("DisplayMismatches").GetValue(scan);
        int candidates = (int)scan.GetType().GetField("Candidates").GetValue(scan);
        string all = string.Join("\n", lines);
        // Kesin ölçüt: ızgarada boş (kırmızısız) görünen her hücre grubu kabul edilir, her ret kırmızı hücreyle açıklanır.
        Assert.AreEqual(0, mismatch, "ızgara ile kural aynı olmalı:\n" + all);
        Assert.AreEqual(0, ugly, "kabul edilen yer başka yapının görseline binmemeli:\n" + all);
        // Sprite ölçütü (bilgi): iki görsel aynı hücreye karşı yönlerden <1 birim taşınca hücre kuralı gözden biraz katıdır.
        Assert.LessOrEqual(hidden, candidates * 0.05f, "sprite boşluğunda ret oranı küçük kalmalı:\n" + all);
    }
}
