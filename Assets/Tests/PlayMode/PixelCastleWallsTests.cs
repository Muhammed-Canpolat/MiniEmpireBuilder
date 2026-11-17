using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// D108: Ana Üs PixelCastle duvarları. Yerleşim her çit boyutunda sınırı kapatır; duvar yalnız görseldir (collider yok,
/// kayda/ızgaraya girmez); bayrak açık/kapalı çarpışma, inşa edilebilir alan ve yol bulma aynı; sıralama ve sabit kadraj.
/// </summary>
public class PixelCastleWallsTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => System.IO.Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;
    private bool savedFlag;

    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);
    private static FieldInfo Flag => Config.GetType().GetField("pixelCastleWalls");
    private static Type Walls => T("BaseCastleWalls");
    private static object Map => T("BaseMapManager").GetProperty("Instance").GetValue(null);

    [SetUp]
    public void Keep()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = System.IO.File.Exists(SaveFile) ? System.IO.File.ReadAllText(SaveFile) : null;
        savedFlag = (bool)Flag.GetValue(Config);
    }

    [TearDown]
    public void Restore()
    {
        Flag.SetValue(Config, savedFlag);
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) System.IO.File.WriteAllText(SaveFile, savedFile);
        else if (System.IO.File.Exists(SaveFile)) System.IO.File.Delete(SaveFile);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
    }

    private static IEnumerator LoadBase(int mainBase, int landTier)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", 5000);
        Set(save, "firstBaseGuideDone", true);
        Set(save, "landOpened", landTier);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        T("AwayReportUI").GetMethod("CloseIfOpen").Invoke(null, null);
        yield return null;
    }

    private static void SetTier(int tier)
    {
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        Set(gm.GetType().GetProperty("PlayerData").GetValue(gm), "landOpened", tier);
    }

    private static void Refresh() => Map.GetType().GetMethod("RefreshBuildableArea").Invoke(Map, new object[] { 22, 38 });

    private static IList Layout(Rect fence) => (IList)Walls.GetMethod("Layout").Invoke(null, new object[] { fence });
    private static Rect RectOf(object piece) => (Rect)Field(piece, "rect");
    private static string PartOf(object piece) => Field(piece, "part").ToString();
    private static float Const(string name) => Convert.ToSingle(Walls.GetField(name).GetValue(null));

    // ==================== YERLEŞİM ====================

    [Test]
    public void LayoutClosesFenceAtAnySize()
    {
        var current = (Rect)Walls.GetProperty("FenceRect").GetValue(null);
        Assert.AreEqual(Rect.MinMaxRect(0.5f, 1.5f, 21.5f, 36.5f), current, "çit çizgisi Buildable ±0,5 (bugünkü çit)");
        float px = Const("WorldPerPx");
        foreach (var fence in new[] { current, Rect.MinMaxRect(0.5f, 1.5f, 13.5f, 16.5f), Rect.MinMaxRect(-4.5f, -2.5f, 33.5f, 58.5f),
                     Rect.MinMaxRect(0.5f, 1.5f, 21.5f, 24.5f) })
        {
            var pieces = Layout(fence).Cast<object>().ToList();
            var solid = pieces.Where(p => PartOf(p) != "Banner").ToList();
            bool Covered(Vector2 point) => solid.Any(p => RectOf(p).Contains(point));
            for (float x = fence.xMin; x <= fence.xMax + 1e-3f; x += 0.1f)
            {
                Assert.IsTrue(Covered(new Vector2(x, fence.yMax + 0.3f)), $"{fence}: üst kenar açık x={x:0.0}");
                Assert.IsTrue(Covered(new Vector2(x, fence.yMin - 0.3f)), $"{fence}: alt kenar açık x={x:0.0}");
            }
            for (float y = fence.yMin; y <= fence.yMax + 1e-3f; y += 0.1f)
            {
                Assert.IsTrue(Covered(new Vector2(fence.xMin - 0.3f, y)), $"{fence}: sol kenar açık y={y:0.0}");
                Assert.IsTrue(Covered(new Vector2(fence.xMax + 0.3f, y)), $"{fence}: sağ kenar açık y={y:0.0}");
            }
            // Tekrarlı (kırpılmış) parçalar arasında boşluk yok: komşu dikdörtgenler en az ~1 kaynak px bindirir.
            foreach (var group in solid.Where(p => PartOf(p) == "Wall").GroupBy(p => RectOf(p).yMin))
            {
                var row = group.Select(RectOf).OrderBy(r => r.xMin).ToList();
                for (int i = 1; i < row.Count; i++)
                    if (row[i].xMin - row[i - 1].xMax < 1f)   // aynı açıklık (kapı iki açıklığı ayırır)
                        Assert.LessOrEqual(row[i].xMin, row[i - 1].xMax - 0.9f * px, $"{fence}: yatay dikiş");
            }
            foreach (var group in solid.Where(p => PartOf(p) == "WallVertical").GroupBy(p => RectOf(p).xMin))
            {
                var column = group.Select(RectOf).OrderBy(r => r.yMin).ToList();
                for (int i = 1; i < column.Count; i++)
                    Assert.LessOrEqual(column[i].yMin, column[i - 1].yMax - 0.9f * px, $"{fence}: dikey dikiş");
            }
            // Duvar içeriye girmez: yalnız kapı en çok GateInward taşar.
            var interior = Rect.MinMaxRect(fence.xMin + 0.02f, fence.yMin + Const("GateInward") + 0.02f, fence.xMax - 0.02f, fence.yMax - 0.02f);
            foreach (var p in solid)
            {
                var r = RectOf(p);
                if (PartOf(p) == "CornerLeft")   // L parça: sınır kutusu değil iç köşe çit köşesine oturur (kol kalınlığı 141 / 183 px)
                {
                    Assert.AreEqual(fence.xMin, r.xMin + 141f * px, 0.012f, "sol köşe dikey kolu çit çizgisinde");
                    Assert.AreEqual(fence.yMax, r.yMax - 183f * px, 0.012f, "sol köşe yatay kolu çit çizgisinde");
                    continue;
                }
                if (PartOf(p) == "CornerRight")
                {
                    Assert.AreEqual(fence.xMax, r.xMin + 205f * px, 0.012f, "sağ köşe dikey kolu çit çizgisinde");
                    Assert.AreEqual(fence.yMax, r.yMax - 182f * px, 0.012f, "sağ köşe yatay kolu çit çizgisinde");
                    continue;
                }
                Assert.IsFalse(r.Overlaps(interior), $"{fence}: {PartOf(p)} {r} iç alana taşıyor");
            }
            Assert.AreEqual(1, solid.Count(p => PartOf(p) == "Gate"), "tek kapı");
            Assert.AreEqual(fence.center.x, RectOf(solid.First(p => PartOf(p) == "Gate")).center.x, 0.02f, "kapı alt kenarın ortasında");
        }
    }

    // ==================== SAHNE ====================

    private struct Snapshot
    {
        public List<Vector3> blockers;
        public List<bool> buildable;
        public List<string> paths;
        public List<int> hits;
    }

    private static Snapshot Take()
    {
        var snap = new Snapshot { blockers = new List<Vector3>(), buildable = new List<bool>(), paths = new List<string>(), hits = new List<int>() };
        var fence = GameObject.Find("BuildableFence");
        foreach (var box in fence.GetComponentsInChildren<BoxCollider2D>(true))
        {
            Assert.AreEqual("FenceSegment", box.name, "çit kökünde yalnız FenceSegment çarpışması");
            snap.blockers.Add(box.transform.position);
        }
        var isBuildable = Map.GetType().GetMethod("IsBuildablePosition");
        for (int y = 0; y < 38; y++)
        for (int x = 0; x < 22; x++)
            snap.buildable.Add((bool)isBuildable.Invoke(Map, new object[] { new Vector3(x + 0.5f, y + 0.5f, 0f) }));
        var find = T("BuilderPathfinder").GetMethods().First(m => m.Name == "FindPath" && m.GetParameters().Length == 4);
        foreach (var (a, b) in new[] { (new Vector3(3f, 3f), new Vector3(19f, 34f)), (new Vector3(11f, 2.2f), new Vector3(2f, 30f)),
                     (new Vector3(20f, 20f), new Vector3(1.5f, 2.5f)) })
        {
            var path = (IEnumerable)find.Invoke(null, new object[] { a, b, null, new RectInt[0] });
            snap.paths.Add(path == null ? "null" : string.Join(";", path.Cast<Vector3>().Select(v => v.ToString("F2"))));
        }
        Physics2D.SyncTransforms();
        foreach (var point in new[] { new Vector2(0.2f, 20f), new Vector2(11f, 1.0f), new Vector2(11f, 37f), new Vector2(21.8f, 10f),
                     new Vector2(-0.4f, 30f), new Vector2(11f, -0.5f), new Vector2(3f, 36.2f) })
            snap.hits.Add(Physics2D.OverlapPointAll(point).Length);
        return snap;
    }

    [UnityTest]
    public IEnumerator WallsAreVisualOnlyAtEveryTierAndFlagDoesNotChangeLogic()
    {
        Flag.SetValue(Config, true);
        yield return LoadBase(13, 0);
        int expected = Layout((Rect)Walls.GetProperty("FenceRect").GetValue(null)).Count;
        Rect firstOuter = default;
        for (int tier = 0; tier <= 4; tier++)
        {
            SetTier(tier);
            Refresh();
            yield return null;
            var walls = GameObject.Find("CastleWalls");
            Assert.NotNull(walls, "duvar kuruldu, kademe " + tier);
            Assert.AreEqual(1, Object.FindObjectsByType(Walls, FindObjectsSortMode.None).Length, "eski duvar kökü kaldı, kademe " + tier);
            Assert.AreEqual(expected, walls.transform.childCount, "parça sayısı, kademe " + tier);
            Assert.AreEqual(0, walls.GetComponentsInChildren<Collider2D>(true).Length, "duvarda collider yok");
            Assert.IsNull(walls.GetComponentInChildren<Rigidbody2D>(true), "duvarda Rigidbody yok");
            Assert.IsNull(walls.GetComponentInChildren(T("MapObjectTag"), true), "duvar harita nesnesi değil");
            foreach (var o in (IEnumerable)Map.GetType().GetProperty("MapObjects").GetValue(Map))
            {
                var go = (GameObject)Field(o, "gameObject");
                Assert.IsFalse(go != null && go.transform.IsChildOf(walls.transform), "duvar _mapObjects'e girmez");
            }
            Assert.AreEqual(112, GameObject.Find("BuildableFence").GetComponentsInChildren<BoxCollider2D>(true).Length, "FenceSegment sayısı");
            Assert.IsNull(GameObject.Find("FenceBeamHorizontal"), "eski kiriş bayrak açıkken gizli");
            Assert.IsNull(GameObject.Find("StoneFencePost"), "eski direk bayrak açıkken gizli");
            var current = Walls.GetProperty("Current").GetValue(null);
            var outer = (Rect)current.GetType().GetProperty("OuterBounds").GetValue(current);
            if (tier == 0) firstOuter = outer; else Assert.AreEqual(firstOuter, outer, "halka sabit: duvar sınırı kademeyle değişmez");
        }

        // Bayrak açık / kapalı: çarpışma, inşa edilebilir hücreler, yol bulma ve dokunuş isabetleri aynı.
        foreach (int tier in new[] { 0, 4 })
        {
            SetTier(tier);
            Flag.SetValue(Config, true); Refresh(); yield return null;
            var on = Take();
            Flag.SetValue(Config, false); Refresh(); yield return null;
            Assert.IsNull(GameObject.Find("CastleWalls"), "bayrak kapalıyken duvar yok");
            Assert.NotNull(GameObject.Find("FenceBeamHorizontal"), "bayrak kapalıyken eski çit");
            var off = Take();
            CollectionAssert.AreEqual(off.blockers, on.blockers, "FenceSegment konumları");
            CollectionAssert.AreEqual(off.buildable, on.buildable, "inşa edilebilir hücreler");
            CollectionAssert.AreEqual(off.paths, on.paths, "yol bulma");
            CollectionAssert.AreEqual(off.hits, on.hits, "dokunuş/çarpışma isabetleri");
        }
        Flag.SetValue(Config, true); Refresh(); yield return null;
    }

    [UnityTest]
    public IEnumerator SortingAndFixedFraming()
    {
        Flag.SetValue(Config, true);
        yield return LoadBase(7, 4);
        Assert.NotNull(GameObject.Find("CastleWalls"));
        int backMax = Math.Max(Math.Max((int)Const("BackWallOrder"), (int)Const("BackCornerOrder")), (int)Const("BackBannerOrder"));
        int front = (int)Const("FrontWallOrder");
        // Arka duvar: bütün bina/dekor/doğa/birim çizimlerinin arkasında.
        foreach (var tag in Object.FindObjectsByType(T("MapObjectTag"), FindObjectsSortMode.None).Cast<Component>())
            foreach (var renderer in tag.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.enabled && renderer.sprite != null && renderer.color.a > 0.01f)
                    Assert.Greater(renderer.sortingOrder, backMax, $"{renderer.name} arka duvarın önünde olmalı");
        foreach (var decor in Object.FindObjectsByType(T("DecorInstance"), FindObjectsSortMode.None).Cast<Component>())
            foreach (var renderer in decor.GetComponentsInChildren<SpriteRenderer>())
                Assert.Greater(renderer.sortingOrder, backMax, $"{renderer.name} (dekor) arka duvarın önünde olmalı");
        // Ön duvar: birimlerin önünde; birimler çit engeli yüzünden y ≥ 1,85'te kalır (sıra 100 − 2y ≤ 96).
        var hero = Object.FindFirstObjectByType(T("HeroMovementController")) as Component;
        if (hero != null)
            foreach (var renderer in hero.GetComponentsInChildren<SpriteRenderer>())
                Assert.Less(renderer.sortingOrder, front, "kahraman alt duvarın arkasında");
        Assert.Less(100 - Mathf.RoundToInt(2f * 1.85f), front);
        // Sabit kadraj: zoom / kaydırma etkisiz; duvarlar görüş alanında.
        var cam = Camera.main;
        var controller = cam.GetComponent(T("BaseCameraController"));
        Assert.IsTrue((bool)controller.GetType().GetProperty("FixedFraming").GetValue(null), "Ana Üs kadrajı sabit");
        float ortho = cam.orthographicSize; Vector3 pos = cam.transform.position;
        controller.GetType().GetMethod("ZoomBy").Invoke(controller, new object[] { 0.6f, new Vector2(50f, 50f) });
        controller.GetType().GetMethod("EdgeScroll").Invoke(controller, new object[] { new Vector2(1f, 1f) });
        yield return null;
        Assert.AreEqual(ortho, cam.orthographicSize, 1e-4f, "pinch/tekerlek sonrası ortho aynı");
        Assert.AreEqual(pos, cam.transform.position, "kamera kaymaz");
        var current = Walls.GetProperty("Current").GetValue(null);
        var outer = (Rect)current.GetType().GetProperty("OuterBounds").GetValue(current);
        Vector3 lo = cam.WorldToViewportPoint(new Vector3(outer.xMin, outer.yMin));
        Vector3 hi = cam.WorldToViewportPoint(new Vector3(outer.xMax, outer.yMax));
        Assert.IsTrue(lo.x >= -0.001f && lo.y >= -0.001f && hi.x <= 1.001f && hi.y <= 1.001f, $"duvarın tamamı görünür {lo} {hi}");
        Assert.AreEqual((float)controller.GetType().GetProperty("FullSize").GetValue(controller), cam.orthographicSize, 1e-3f);
    }
}
