using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static IslandPhaseBTests;

/// <summary>
/// AŞAMA 2 (ada yapısı ve kadro): boş başlangıç + ilk yapım bedava, v10 göçü (eski köy korunur), kadro (kendi işçisi + Ev kapasitesi,
/// görünen piyon = atanmış işçi, KADRO paneli), ağaç kesme (altın → odun, yer açılır, kayıtta kalır), koyunlar yalnız Ağıl çevresinde,
/// Akın ölçeği (kule seviyesiyle artar) ve boş adada Akın 1. Yalıtılmış test kaydı; sınıf sonunda test GameManager'ı yok edilir.
/// Ekran görüntüleri <c>PlaytestOutput/island/N1*.png</c>; "ilk 30 dakika" özeti <c>PlaytestOutput/first30_island.txt</c>.
/// </summary>
public class IslandFreshStartTests
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
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    /// <summary>GameManager sahneler arası yaşar ve çıkışta son veriyi kaydeder: test adası gerçek kayda sızmasın.</summary>
    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
        PlayerPrefs.Save();
    }

    // ==================== YARDIMCILAR ====================

    private static string L(string tr, string en) => (string)T("L").GetMethod("T").Invoke(null, new object[] { tr, en });
    private static (int wood, int meat) Cost(string kind, object island) =>
        ((int, int))Call("IslandEconomy", "BuildCost", Kind(kind), island);
    private static int Bit(string kind) => 1 << (int)Kind(kind);
    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);
    private static int CfgInt(string name) => (int)Field(Config, name);
    private static Dictionary<string, int> Assign(object island) =>
        (Dictionary<string, int>)Call("IslandEconomy", "AssignWorkers", Placed(island));
    private static int AssignedTotal(object island) => Assign(island).Values.Sum();
    private static object Gm => T("GameManager").GetProperty("Instance").GetValue(null);
    private static object Player => Prop(Gm, "PlayerData");
    private static object Hud => Prop(Setup, "Hud");
    private static object BuildUi => Prop(Hud, "BuildUi");
    private static object Village => Prop(Setup, "Village");

    private static int PawnCount(params string[] jobs)
    {
        var all = (System.Collections.IEnumerable)T("IslandPawn").GetField("All").GetValue(null);
        int n = 0;
        foreach (var pawn in all) if (pawn != null && jobs.Contains(Prop(pawn, "Kind").ToString())) n++;
        return n;
    }

    private static int SheepCount() => ((System.Collections.ICollection)T("IslandSheep").GetField("All").GetValue(null)).Count;

    /// <summary>Gerçek akış: yerleştirme ev sahibi + onay; sonra saati 6 sn ileri alıp görevi bitirir. Bina kaydını döndürür.</summary>
    private static object BuildAndFinish(string kind, int nearCol, int nearRow, ref long clock)
    {
        var island = CurrentIsland();
        int mask = (int)Field(island, "zoneMask");
        int keep = (int)T("IslandSession").GetMethod("KeepLevel").Invoke(null, null);
        Assert.IsTrue(FindCellNear(kind, island, mask, nearCol, nearRow, out int col, out int row, keep), $"{kind} için geçerli hücre");
        // 6. oturum (3.2): ada binalarının ilk kurulumu altınla: test cüzdanı yeterli olsun.
        var gmType = T("GameManager");
        var gmObj = gmType.GetProperty("Instance").GetValue(null);
        var pd = gmType.GetProperty("PlayerData").GetValue(gmObj);
        if ((int)Field(pd, "gold") < 5000) Set(pd, "gold", 5000);
        var host = Call("IslandPlacementHost", "ForNew", Kind(kind));
        Assert.IsTrue((bool)host.GetType().GetMethod("Confirm").Invoke(host, new object[] { col, row }), $"{kind} inşaatı başladı");
        object entry = null;
        foreach (var e in Placed(island)) if ((int)Field(e, "col") == col && (int)Field(e, "row") == row && (string)Field(e, "kind") == kind) entry = e;
        clock += 6 * TimeSpan.TicksPerSecond;
        SetClock(clock);
        Call("IslandEconomy", "Tick", clock, true);
        Assert.AreEqual(1, (int)Field(entry, "level"), $"{kind} tamamlandı");
        return entry;
    }

    private static void Capture(string name)
    {
        const int W = 1080, H = 1920;
        var camera = (Camera)Prop(Setup, "WorldCamera");
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/island"));
        Directory.CreateDirectory(folder);
        var texture = new RenderTexture(W, H, 24);
        camera.targetTexture = texture;
        camera.aspect = (float)W / H;
        var controller = Prop(Setup, "CameraController");
        controller.GetType().GetMethod("SetDefaultView").Invoke(controller, null);   // sabit kadraj 9:16'ya oturur
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(W, H, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        UnityEngine.Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        texture.Release();
        UnityEngine.Object.Destroy(texture);
    }

    // ==================== §1 BOŞ BAŞLANGIÇ + İLK YAPIM BEDAVA ====================

    [Test]
    public void FreshIslandHasOnlyTheCastleAndEachKindsFirstBuildIsFree()
    {
        var island = NewIsland(5);
        Call("IslandEconomy", "EnsureStarted", island, 5);
        Assert.AreEqual(0, Placed(island).Count, "yalnız kale (placed boş)");
        Assert.IsTrue((bool)Field(island, "villageSeeded"));
        Assert.IsTrue((bool)Field(island, "freshStart"));
        Assert.AreEqual(0, (int)Field(island, "wood"), "başlangıç Odun (BUGLAR 16)");
        Assert.AreEqual(0, (int)Field(island, "meat"), "başlangıç Et (BUGLAR 16)");
        foreach (var kind in new[] { "House", "Lumber", "Pasture", "Tower", "Archery", "Barracks", "Monastery" })
            Assert.AreEqual((0, 0), Cost(kind, island), $"{kind} ilk yapım bedava");
        Assert.IsTrue((bool)Call("IslandEconomy", "Buildable", Kind("Archery")), "Okçuluk menüden kurulur");
        Set(island, "freeBuildMask", (int)Field(island, "freeBuildMask") | Bit("Lumber") | Bit("Archery"));
        // 6. oturum (3.2): "ilk yapım bedava" kalktı; türün ilk kurulumu yalnız ALTIN (Oduncu 100), Odun/Et ikinciden itibaren.
        Assert.AreEqual((0, 0), Cost("Lumber", island), "ilk Oduncu: Odun/Et yok, yalnız altın");
        Assert.AreEqual(100, (int)Call("IslandEconomy", "BuildGold", Kind("Lumber"), island));
        Assert.AreEqual((CfgInt("archeryPlaceWood"), CfgInt("archeryPlaceMeat")), Cost("Archery", island));
        Assert.AreEqual((0, 0), Cost("House", island), "başka türlerin hakkı sürer");
        // İdempotent: yeniden çağırmak hiçbir şeyi değiştirmez
        Call("IslandEconomy", "EnsureStarted", island, 5);
        Assert.AreEqual(0, Placed(island).Count);
    }

    [Test]
    public void OldV9SaveKeepsItsVillageAndLevelsAndMigrationMarksOwnedKinds()
    {
        T("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = NewSave();
        var island = Field(save, "island");
        Set(island, "keepLevel", 6);
        Set(island, "highestAkin", 7);
        Call("IslandEconomy", "EnsureSeeded", island, 6);
        Assert.AreEqual(6, Placed(island).Count);
        var levels = new Dictionary<string, int>();
        int bump = 0;
        foreach (var e in Placed(island)) { Set(e, "level", (int)Field(e, "level") + (bump++ % 3)); levels[(string)Field(e, "id")] = (int)Field(e, "level"); }
        string json = JsonUtility.ToJson(save);
        // v9 kaydında v10 alanları yoktur
        json = Regex.Replace(json, @"""freshStart"":(true|false),""freeBuildMask"":-?\d+,""clearedTrees"":\[[^\]]*\],(""clearedNature"":\[[^\]]*\],)?""legacyVillage"":(true|false),", "");
        StringAssert.DoesNotContain("freeBuildMask", json);
        json = Regex.Replace(json, "\"saveVersion\":\\d+", "\"saveVersion\":9");
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();

        var loaded = T("SaveSystem").GetMethod("Load").Invoke(null, null);
        Assert.AreEqual((int)T("PlayerSaveData").GetField("CurrentVersion").GetValue(null), (int)Field(loaded, "saveVersion"));
        var back = Field(loaded, "island");
        Assert.AreEqual(6, Placed(back).Count, "hazır köy yerinde");
        foreach (var e in Placed(back)) Assert.AreEqual(levels[(string)Field(e, "id")], (int)Field(e, "level"), "seviye korunur");
        int mask = (int)Field(back, "freeBuildMask");
        foreach (var kind in new[] { "Castle", "House", "Lumber", "Archery", "Pasture", "Tower" })
            Assert.AreNotEqual(0, mask & Bit(kind), $"{kind} biti yazıldı");
        Assert.AreEqual(0, mask & Bit("Barracks"), "sahip olunmayan tür bedava kalır");
        Assert.IsFalse((bool)Field(back, "freshStart"));
        Assert.AreEqual((40, 0), Cost("House", back), "ikinci Ev normal fiyat");
        Assert.AreEqual((0, 0), Cost("Barracks", back));
        // Açılış: köy zaten tohumlu, boş başlangıç olmaz
        Call("IslandEconomy", "EnsureStarted", back, 6);
        Assert.AreEqual(6, Placed(back).Count);
    }

    [Test]
    public void OldSaveWithProgressButUnseededVillageGetsThePrebuiltVillage()
    {
        T("SaveSystem").GetMethod("DeleteSave").Invoke(null, null);
        var save = NewSave();
        var island = Field(save, "island");
        Set(island, "keepLevel", 5);
        Set(island, "highestAkin", 3);
        string json = JsonUtility.ToJson(save);
        json = Regex.Replace(json, "\"saveVersion\":\\d+", "\"saveVersion\":9");
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
        var back = Field(T("SaveSystem").GetMethod("Load").Invoke(null, null), "island");
        Assert.IsTrue((bool)Field(back, "legacyVillage"), "ilerlemesi olan eski kayıt");
        Call("IslandEconomy", "EnsureStarted", back, 5);
        Assert.AreEqual(6, Placed(back).Count, "eski oyuncu hazır köyünü alır");
        Assert.IsFalse((bool)Field(back, "freshStart"));
    }

    // ==================== §2 KADRO ====================

    [Test]
    public void ProducerComesWithOwnWorkerAndHousesFillExtraSlots()
    {
        var island = NewIsland(5);
        Call("IslandEconomy", "EnsureStarted", island, 5);
        Placed(island).Add(Entry("Lumber#1", "Lumber", 1, 8, 10));
        Assert.AreEqual(1, Assign(island)["Lumber#1"], "kendi işçisi");
        Assert.AreEqual(1, (int)Call("IslandEconomy", "PawnPool", Placed(island)), "kale işçisi üretime sayılmaz");
        Placed(island).Add(Entry("House#2", "House", 1, 12, 12));
        Assert.AreEqual(2, Assign(island)["Lumber#1"], "Ev ek yuvayı doldurur (Oduncu Sv1: 2 yuva)");
        Assert.AreEqual(3, (int)Call("IslandEconomy", "PawnPool", Placed(island)));
        Placed(island).Add(Entry("Pasture#3", "Pasture", 1, 10, 18));
        Assert.AreEqual(1, Assign(island)["Pasture#3"], "Ağıl kendi işçisiyle");
        var roster = (System.Collections.IList)Call("IslandEconomy", "StaffRoster", Placed(island));
        Assert.AreEqual(4, roster.Count, "Kapasite = 2 kendi + 2 Ev");
        int own = 0, working = 0, idle = 0;
        foreach (var row in roster)
        {
            if ((bool)Field(row, "own")) own++;
            if (Field(row, "workId") != null) working++; else idle++;
        }
        Assert.AreEqual(2, own); Assert.AreEqual(3, working); Assert.AreEqual(1, idle);
        Assert.AreEqual(AssignedTotal(island), working, "çalışan = atanmış");
        // İnşaattaki üretici işçi almaz
        Placed(island).Add(Entry("Lumber#4", "Lumber", 0, 14, 22));
        Assert.IsFalse(Assign(island).ContainsKey("Lumber#4"));
    }

    // ==================== §4 AKIN ÖLÇEĞİ ====================

    [Test]
    public void AkinScaleGrowsWithTowerLevelsAndIsClamped()
    {
        float Scale(object island, int keep) => (float)Call("IslandEconomy", "AkinScale", Placed(island), keep);
        var island = NewIsland(5);
        Assert.AreEqual(0.8f, Scale(island, 5), 1e-4f, "kulesiz KK5 = taban");
        float last = Scale(island, 5);
        for (int i = 1; i <= 6; i++)
        {
            Placed(island).Add(Entry("Tower#" + i, "Tower", 2, 3 + i, 3));
            float now = Scale(island, 5);
            Assert.GreaterOrEqual(now, last, "kule seviyesiyle artar");
            last = now;
        }
        Assert.AreEqual(0.8f + 0.015f * 12, Scale(island, 5), 1e-4f);
        Placed(island).Add(Entry("Tower#9", "Tower", 0, 20, 20));
        Assert.AreEqual(0.8f + 0.015f * 12, Scale(island, 5), 1e-4f, "inşaattaki kule sayılmaz");
        for (int i = 10; i < 20; i++) Placed(island).Add(Entry("Tower#" + i, "Tower", 15, 1, i));
        Assert.AreEqual(1.15f, Scale(island, 15), 1e-4f, "tavan");
        Assert.AreEqual(0.5f, Scale(NewIsland(1), 1), 1e-4f, "KK1 kulesiz (6. oturum 3.4: Kale 5 altı seviye başına −0,075)");
        Assert.Greater(Scale(NewIsland(9), 9), Scale(NewIsland(5), 5), "Kıyı Kalesi ile artar");
    }

    // ==================== SAHNEDE: BOŞ ADA, İNŞA, KADRO, AĞAÇ, KOYUN ====================

    [UnityTest, Timeout(300000)]
    public IEnumerator FreshIslandBuildsStaffClearsTreesAndHerdsSheepInTheScene()
    {
        yield return LoadIsland(null, 5, v => devWas = v, fresh: true);
        var island = CurrentIsland();
        long clock = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc).Ticks;
        SetClock(clock);
        Assert.AreEqual(0, Placed(island).Count, "yeni ada: yalnız kale");
        Assert.IsTrue((bool)Field(island, "freshStart"));
        Assert.AreEqual(1, ((System.Collections.ICollection)T("IslandBuilding").GetProperty("All").GetValue(null)).Count, "yalnız kale yapısı");
        Assert.AreEqual(0, SheepCount(), "Ağıl yoksa koyun yok");
        Assert.AreEqual(0, PawnCount("Lumberjack", "Shepherd", "Idler"), "atanmış işçi yok: piyon yok");
        Capture("N1a_fresh_island");

        // İnşa menüsü: bedel yerine "İLK YAPIM BEDAVA", her kartta tek cümle
        Hud.GetType().GetMethod("OpenBuildMenu").Invoke(Hud, null);
        yield return null;
        var ui = BuildUi;
        string MenuCost(string kind) => (string)ui.GetType().GetMethod("MenuCardCostText").Invoke(ui, new[] { Kind(kind) });
        string MenuInfo(string kind) => (string)ui.GetType().GetMethod("MenuCardInfoText").Invoke(ui, new[] { Kind(kind) });
        Assert.AreEqual(L("100 ALTIN", "100 GOLD"), MenuCost("Lumber"), "6. oturum (3.2): Oduncu ~100 altın");
        Assert.AreEqual(L("ÜCRETSİZ", "FREE"), MenuCost("Archery"), "ilk Okçuluk ücretsiz (öğretici)");
        StringAssert.StartsWith(L("Odun üretir", "Produces Wood"), MenuInfo("Lumber"));
        StringAssert.StartsWith(L("Et üretir", "Produces Meat"), MenuInfo("Pasture"));
        yield return null;
        Capture("N1b_build_menu_free");
        ui.GetType().GetMethod("Close").Invoke(ui, null);

        // Oduncu (bedava) → 1 kendi işçisi, 1 oduncu piyonu
        int wood0 = (int)Field(island, "wood");
        var lumber = BuildAndFinish("Lumber", 6, 8, ref clock);
        Assert.AreEqual(wood0, (int)Field(island, "wood"), "ilk Oduncu bedava");
        Assert.AreNotEqual(0, (int)Field(island, "freeBuildMask") & Bit("Lumber"));
        Assert.AreEqual((300, 60), Cost("Lumber", island), "ikinci Oduncu liste bedeli");
        yield return null;
        Assert.AreEqual(1, Assign(island)[(string)Field(lumber, "id")]);
        Assert.AreEqual(1, PawnCount("Lumberjack"), "görünen oduncu = atanmış işçi");

        // Ev (bedava) → kapasite +2: Oduncu 2/2, bir işçi boşta (Ev önünde)
        BuildAndFinish("House", 11, 9, ref clock);
        yield return null;
        Assert.AreEqual(2, Assign(island)[(string)Field(lumber, "id")]);
        Assert.AreEqual(AssignedTotal(island), PawnCount("Lumberjack", "Shepherd"));
        Assert.AreEqual(1, PawnCount("Idler"), "boştaki Ev işçisi");

        // Ağıl (bedava) → koyunlar yalnız çevresinde
        var pasture = BuildAndFinish("Pasture", 10, 18, ref clock);
        yield return null;
        int perPasture = CfgInt("sheepPerPasture");
        Assert.AreEqual(perPasture, SheepCount(), "Ağıl başına koyun");
        var center = (Vector2)T("IslandLayout").GetMethod("World", new[] { typeof(Vector2) }).Invoke(null,
            new object[] { Call("IslandGrid", "CenterCell", Kind("Pasture"), Field(pasture, "col"), Field(pasture, "row")) });
        float radius = (float)Field(Config, "sheepRadius");
        foreach (var sheep in (System.Collections.IEnumerable)T("IslandSheep").GetField("All").GetValue(null))
            Assert.LessOrEqual(Vector2.Distance((Vector2)Prop(sheep, "Feet"), center), radius + 0.6f, "koyun Ağıl yakınında");
        Assert.AreEqual(AssignedTotal(island), PawnCount("Lumberjack", "Shepherd"), "görünen işçi = atanmış işçi");

        // KADRO paneli
        Hud.GetType().GetMethod("OpenStaff").Invoke(Hud, null);
        yield return null;
        Assert.IsTrue((bool)Prop(ui, "StaffOpen"));
        string staff = (string)Prop(ui, "StaffText");
        StringAssert.Contains(L("İşçi 1 · Oduncu Kampı · kendi işçisi", "Worker 1 · Lumber Camp · own worker"), staff);
        StringAssert.Contains(L("Ev'den", "from House"), staff);
        int pool = (int)Call("IslandEconomy", "PawnPool", Placed(island));
        Assert.AreEqual(L($"Çalışan {AssignedTotal(island)} / Kapasite {pool}", $"Working {AssignedTotal(island)} / Capacity {pool}"), (string)Prop(ui, "StaffTotalsText"));
        Assert.AreEqual(pool, staff.Split('\n').Count(line => line.Trim().Length > 0), "her işçi bir satır");
        Capture("N1c_kadro_panel");
        ui.GetType().GetMethod("Close").Invoke(ui, null);

        // Ağaç kesme: üretim ağacı kesilemez; diğerleri 10 altın → +8 odun, yer açılır, kayıtta kalır
        Set(Player, "gold", 500);
        var trees = (Vector2Int[])T("IslandLayout").GetField("Trees").GetValue(null);
        var save = island;
        Vector2Int production = trees.First(t => (bool)Call("IslandEconomy", "IsProductionTree", save, t.x, t.y));
        Assert.IsNotNull(Call("IslandEconomy", "ClearTreeBlockedReason", production.x, production.y), "üretim ağacı kesilemez");
        ui.GetType().GetMethod("ShowTree").Invoke(ui, new object[] { production });
        StringAssert.Contains(L("üretim ağacı", "production tree"), (string)Prop(ui, "PanelBodyText"));

        int mask = (int)Field(island, "zoneMask");
        Vector2Int target = default;
        var blockedBefore = new List<Vector2Int>();
        foreach (var t in trees)
        {
            if ((int)T("IslandLayout").GetMethod("ZoneAt").Invoke(null, new object[] { t.x, t.y }) != 0) continue;
            if (Call("IslandEconomy", "ClearTreeBlockedReason", t.x, t.y) != null) continue;
            blockedBefore.Clear();
            // Ev ayak izi (2×2) yalnız bu ağaç yüzünden engelli: diğer üç hücre kalıcı olarak serbest
            for (int c = t.x - 1; c <= t.x; c++)
                for (int r = t.y - 1; r <= t.y; r++)
                {
                    if (Check("House", c, r, island, mask, null, 5) != "Nature") continue;
                    bool onlyThisTree = true;
                    for (int dc = 0; dc < 2; dc++)
                        for (int dr = 0; dr < 2; dr++)
                            if ((c + dc != t.x || r + dr != t.y) && StaticReason(c + dc, r + dr) != "None") onlyThisTree = false;
                    if (onlyThisTree) blockedBefore.Add(new Vector2Int(c, r));
                }
            if (blockedBefore.Count > 0) { target = t; break; }
        }
        Assert.IsTrue(blockedBefore.Count > 0, "kesilebilir ağaç bulundu");
        ui.GetType().GetMethod("ShowTree").Invoke(ui, new object[] { target });
        yield return null;
        StringAssert.Contains(L("Ağacı kes: 10 altın → +8 odun", "Clear the tree: 10 gold → +8 wood"), (string)Prop(ui, "PanelBodyText"));
        Assert.AreEqual(L("KES · 10 altın", "CLEAR · 10 gold"), (string)Prop(ui, "PrimaryText"));
        Capture("N1d_tree_clear_prompt");
        int gold0 = (int)Field(Player, "gold"), woodBefore = (int)Field(island, "wood");
        var args = new object[] { target, null };
        Assert.IsTrue((bool)Village.GetType().GetMethod("ClearTree").Invoke(Village, args), (string)args[1]);
        Assert.AreEqual(gold0 - 10, (int)Field(Player, "gold"), "10 altın");
        Assert.AreEqual(woodBefore + 8, (int)Field(island, "wood"), "+8 odun");
        Assert.IsTrue((bool)Call("IslandGrid", "ClearedTreeAt", target.x, target.y));
        Assert.IsTrue(blockedBefore.Any(p => Check("House", p.x, p.y, island, mask, null, 5) != "Nature"), "ağaç hücresi yerleşime açıldı");
        yield return new WaitForSeconds(1.6f);
        var treeObj = T("IslandTree").GetMethod("At").Invoke(null, new object[] { target }) as Component;
        Assert.IsTrue(treeObj == null || !treeObj.gameObject.activeInHierarchy, "ağaç görseli kalktı");
        // Kayıt/yükleme sonrası kalıcı
        T("SaveSystem").GetMethod("Save").Invoke(null, new[] { Player });
        var back = Field(T("SaveSystem").GetMethod("Load").Invoke(null, null), "island");
        CollectionAssert.Contains((System.Collections.IList)Field(back, "clearedTrees"), $"{target.x},{target.y}");
    }

    // ==================== §4 BOŞ ADADA AKIN 1 ====================

    [UnityTest, Timeout(300000)]
    public IEnumerator EmptyIslandWinsAkinOneWithArcherAndCastle()
    {
        yield return LoadIsland(null, 5, v => devWas = v, fresh: true);
        Assert.AreEqual(0, Placed(CurrentIsland()).Count);
        var raid = Prop(Setup, "Raid");
        T("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, Enum.Parse(T("IslandBot+Profile"), "Good") });
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        Assert.AreEqual(0.8f, (float)T("IslandEnemy").GetField("StatScale").GetValue(null), 1e-4f, "kulesiz KK5 ölçeği");
        Time.timeScale = 6f;
        float start = Time.realtimeSinceStartup;
        while (Prop(raid, "Phase").ToString() == "Running" && Time.realtimeSinceStartup - start < 150f) yield return null;
        Time.timeScale = 1f;
        Assert.AreEqual("Won", Prop(raid, "Phase").ToString(), $"Akın 1 boş adada kazanılır: {Prop(raid, "DefeatTag")}");
    }

    // ==================== YENİ OYUNCU: İLK 30 DAKİKA ====================

    [UnityTest, Timeout(900000)]
    public IEnumerator FirstThirtyMinutesOfANewPlayerOnTheIsland()
    {
        yield return LoadIsland(null, 5, v => devWas = v, fresh: true);
        var island = CurrentIsland();
        var log = new StringBuilder();
        long clock = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc).Ticks;
        long t0 = clock;
        SetClock(clock);
        void Note(string line) { log.AppendLine(line); UnityEngine.Debug.Log("[First30] " + line); }
        string Wallet() => $"Odun {Field(island, "wood")} · Et {Field(island, "meat")} · Altın {Field(Player, "gold")} · Elmas {Field(Player, "gems")}";
        Note($"t=0 dk · başlangıç: {Wallet()} · bina {Placed(island).Count}");

        var raid = Prop(Setup, "Raid");
        T("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, Enum.Parse(T("IslandBot+Profile"), "Good") });
        IEnumerator Play(int akin)
        {
            bool can = (bool)raid.GetType().GetMethod("CanStart").Invoke(raid, new object[] { akin });
            if (!can) { Note($"Akın {akin}: BAŞLATILAMADI (kapı)"); yield break; }
            float scale = (float)Call("IslandEconomy", "AkinScale", Placed(island), (int)T("IslandSession").GetMethod("KeepLevel").Invoke(null, null));
            raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { akin });
            Time.timeScale = 6f;
            float start = Time.time;
            while (Prop(raid, "Phase").ToString() == "Running" && Time.time - start < 600f) yield return null;
            Time.timeScale = 1f;
            string phase = Prop(raid, "Phase").ToString();
            float castle = (float)Prop(raid, "CastleHealthPercent");
            string reward = "";
            if (phase == "Won")
            {
                var r = Prop(raid, "LastReward");
                reward = $" · ödül +{Field(r, "wood")} Odun, +{Field(r, "meat")} Et, +{Field(r, "gold")} altın, +{Field(r, "gems")} elmas";
            }
            Note($"Akın {akin} (ölçek ×{scale:0.00}): {phase} · kale %{castle * 100f:0} · oyun süresi {Time.time - start:0} sn{reward}");
            raid.GetType().GetMethod("ReturnToIdle").Invoke(raid, null);
            yield return null;
        }

        string TryBuild(string kind, int nearCol, int nearRow)
        {
            int mask = (int)Field(island, "zoneMask");
            if (!FindCellNear(kind, island, mask, nearCol, nearRow, out int col, out int row, 5)) return $"{kind}: geçerli hücre YOK";
            var cost = Cost(kind, island);
            var host = Call("IslandPlacementHost", "ForNew", Kind(kind));
            var args = new object[] { null };
            bool afford = (bool)host.GetType().GetMethod("CanAfford").Invoke(host, args);
            if (!afford) return $"{kind}: kurulamadı ({args[0]}) · bedel {cost.wood}/{cost.meat}";
            if (!(bool)host.GetType().GetMethod("Confirm").Invoke(host, new object[] { col, row })) return $"{kind}: onay reddedildi";
            clock += 6 * TimeSpan.TicksPerSecond;
            SetClock(clock);
            Call("IslandEconomy", "Tick", clock, true);
            return $"{kind}: kuruldu ({col},{row}) · bedel {cost.wood} Odun/{cost.meat} Et";
        }

        Note(TryBuild("Lumber", 6, 8));
        Note(TryBuild("House", 11, 9));
        Note(TryBuild("Pasture", 10, 18));
        Note(TryBuild("Archery", 13, 10));
        Note(TryBuild("Tower", 15, 12));
        Note($"t≈1 dk · inşaatlar sonrası: {Wallet()}");
        yield return null;

        for (int akin = 1; akin <= 3; akin++) yield return Play(akin);
        Note($"Akınlar sonrası: {Wallet()} · en yüksek Akın {Field(island, "highestAkin")}");

        // Kalan süre: 30 dakikaya kadar üretim (toplamalar 10 dk'da bir)
        for (int minute = 10; minute <= 30; minute += 10)
        {
            clock = t0 + minute * TimeSpan.TicksPerMinute;
            SetClock(clock);
            Call("IslandEconomy", "Tick", clock, true);
            int got = 0;
            foreach (var e in Placed(island).Cast<object>().ToList())
            {
                string k = (string)Field(e, "kind");
                if (k == "Lumber" || k == "Pasture") got += (int)Call("IslandEconomy", "CollectFrom", e);
            }
            Note($"t={minute} dk · toplanan {got} · {Wallet()}");
        }
        var assign = Assign(island);
        var lines = new List<string>();
        foreach (var e in Placed(island))
        {
            string id = (string)Field(e, "id");
            assign.TryGetValue(id, out int crew);
            lines.Add($"{Field(e, "kind")} Sv{Field(e, "level")}" + (assign.ContainsKey(id) ? $" (işçi {crew})" : ""));
        }
        Note("Binalar: " + string.Join(", ", lines));
        Note($"Kadro: çalışan {AssignedTotal(island)} / kapasite {Call("IslandEconomy", "PawnPool", Placed(island))}");

        // Takılma noktaları: 30. dakikada ne alınabilir?
        int wood = (int)Field(island, "wood"), meat = (int)Field(island, "meat");
        void Can(string what, int w, int m) => Note($"{what}: {w} Odun/{m} Et → " + (wood >= w && meat >= m ? "alınabilir" : $"EKSİK (Odun {Math.Max(0, w - wood)}, Et {Math.Max(0, m - meat)})"));
        var lumberEntry = Placed(island).Cast<object>().FirstOrDefault(e => (string)Field(e, "kind") == "Lumber");
        if (lumberEntry != null)
        {
            var up = ((int, int, int))Call("IslandEconomy", "UpgradeCost", lumberEntry);
            Can("Oduncu Sv2", up.Item1, up.Item2);
        }
        var c2 = Cost("Tower", island); Can("2. Gözcü Kulesi", c2.wood, c2.meat);
        var c3 = Cost("House", island); Can("2. Ev", c3.wood, c3.meat);
        Can("Bölge Z1 keşfi", (int)((int[])T("Progression+Island").GetField("ZoneWood").GetValue(null))[1], (int)((int[])T("Progression+Island").GetField("ZoneMeat").GetValue(null))[1]);

        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "first30_island.txt"), log.ToString());
        Assert.Greater(Placed(island).Count, 0);
    }
}
