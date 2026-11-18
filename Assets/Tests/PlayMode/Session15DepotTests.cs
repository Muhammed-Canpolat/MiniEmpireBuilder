using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Grup 9 (K): Depo (bug 11, D121). Kayıt göçü (v13 fikstürü → boş depo, v14), yerleşik+depo kimlik tekliği (Repair), depodaki binanın
/// savaş/üretim/kapasite etkisinin düşmesi, ret kuralları, ücretsiz/seviyesi korunan kurma, limitin yerleşik+depo sayılması, sahne yeniden
/// yüklemesinde tutarlılık; adada stok koruma, hasar/savunma reddi, Okçuluk/Kale hariç. Ekran görüntüleri Docs/playtest/bugs-g9/.
/// </summary>
public class Session15DepotTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private static readonly string OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/playtest/bugs-g9"));
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
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    // ==================== YARDIMCILAR ====================

    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object F(object target, string name) => target.GetType().GetField(name).GetValue(target);
    private static void SetF(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    private static object P(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object S(string type, string method, params object[] args) => T(type).GetMethod(method).Invoke(null, args);
    private static object GM => T("GameManager").GetProperty("Instance").GetValue(null);
    private static object Data => P(GM, "PlayerData");
    private static IList Depot => (IList)F(Data, "depot");
    private static object BaseAdapter => T("BaseDepotAdapter").GetField("Instance").GetValue(null);
    private static object IslandAdapter => T("IslandDepotAdapter").GetField("Instance").GetValue(null);
    private static int Const(string name) => (int)T("DepotService").GetField(name).GetValue(null);

    private static bool Store(object adapter, string id, int category, out string reason)
    {
        var args = new object[] { adapter, id, category, null };
        bool ok = (bool)T("DepotService").GetMethod("Store").Invoke(null, args);
        reason = (string)args[3];
        return ok;
    }

    private static bool Restore(object adapter, string id, Vector2 at, out string reason)
    {
        var args = new object[] { adapter, id, at, null };
        bool ok = (bool)T("DepotService").GetMethod("Restore").Invoke(null, args);
        reason = (string)args[3];
        return ok;
    }

    private static string Busy(int region, int category, string id) => (string)S("DepotService", "BusyReason", region, category, id);
    private static int Owned(int region, string type) => (int)S("DepotService", "OwnedCount", region, type);
    private static object BuildingType(string name) => Enum.Parse(T("BuildingType"), name);
    private static int Completed(string type) => (int)S("BuildingLevels", "CompletedCount", Data, BuildingType(type));

    private static object NewSave()
    {
        var save = T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });
        SetF(save, "landOpened", -1);
        return save;
    }

    private static object Placed(string slot, string type, int level, float x = 0f, float y = 0f, bool hasPos = false)
    {
        var p = Activator.CreateInstance(T("PlacedBuildingData"));
        SetF(p, "slotId", slot); SetF(p, "buildingType", type); SetF(p, "level", level);
        SetF(p, "hasPosition", hasPos); SetF(p, "x", x); SetF(p, "y", y);
        return p;
    }

    private static object DepotItem(string id, int region, int category, string type, int level)
    {
        var d = Activator.CreateInstance(T("DepotItem"));
        SetF(d, "id", id); SetF(d, "region", region); SetF(d, "category", category); SetF(d, "type", type); SetF(d, "level", level);
        return d;
    }

    /// <summary>Tam üs (Session5Tests.FullBase: 2'şer okçu/topçu/manastır/kışla/ev, atölye, maden; seviye 10). Okçular 10 ve 7.</summary>
    private static IEnumerator LoadDepotBase(Action<object> extra = null)
    {
        yield return Session4Tests.LoadBaseWith(15, save =>
        {
            Session5Tests.FullBase(save);
            foreach (var p in (IEnumerable)F(save, "placedBuildings"))
                if ((string)F(p, "slotId") == "archer_2") SetF(p, "level", 7);
            extra?.Invoke(save);
        }, 5000);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    private static object Map => T("BaseMapManager").GetProperty("Instance").GetValue(null);
    private static object MapObject(string slot) => Map.GetType().GetMethod("GetMapObjectBySlot").Invoke(Map, new object[] { slot });
    private static string FirstSlotOf(string type)
    {
        foreach (var o in (IEnumerable)P(Map, "MapObjects"))
            if (F(o, "buildingType")?.ToString() == type && F(o, "gameObject") as GameObject != null) return (string)F(o, "id");
        return null;
    }
    private static Vector3 PosOf(string slot) => ((GameObject)F(MapObject(slot), "gameObject")).transform.position;

    private static Component BaseSetup => (Component)Object.FindFirstObjectByType(T("BaseSceneSetup"));
    private static Component ActionMenu
    {
        get
        {
            var m = T("WorldActionMenu").GetProperty("Current").GetValue(null);
            return m as Component;
        }
    }
    private static Button Btn(object menu, string id) => (Button)menu.GetType().GetMethod("ButtonFor").Invoke(menu, new object[] { id });

    private static IEnumerator Capture(string name, Camera camera, int w = 1080, int h = 1920)
    {
        Directory.CreateDirectory(OutDir);
        var texture = new RenderTexture(w, h, 24);
        float aspect = camera.aspect;
        var viewport = camera.rect;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        var changed = new System.Collections.Generic.List<(Canvas, RenderMode, Camera, int, bool)>();
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
        yield return null;   // menü/hap yerleşimi bu kadraja göre yeniden hesaplanır
        yield return null;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), pixels.EncodeToPNG());
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

    /// <summary>Ana Üs: Session4Tests.CaptureUi (WorldViewportBars kırpması doğru ele alınır), sonra bugs-g9'a taşınır.</summary>
    private static IEnumerator CaptureBaseUi(string name)
    {
        yield return null;
        yield return null;
        Session4Tests.CaptureUi(name, 1080, 1920);
        Directory.CreateDirectory(OutDir);
        var src = Path.Combine(Session4Tests.Folder, name + ".png");
        var dst = Path.Combine(OutDir, name + ".png");
        if (File.Exists(src)) { File.Copy(src, dst, true); File.Delete(src); }
    }

    // ==================== KAYIT ====================

    [Test]
    public void V13FixtureMigratesToV14WithEmptyDepotAndOtherwiseUnchanged()
    {
        string path = Path.Combine(Application.dataPath, "Tests/PlayMode/Fixtures/editor_save_v13_2026-11-15.json");
        Assert.IsTrue(File.Exists(path), "fikstür");
        string json = File.ReadAllText(path);
        StringAssert.DoesNotContain("\"depot\"", json, "v13 kaydında depo alanı yok");
        // Doğrudan göç zinciri (dosya/PlayerPrefs'e dokunmadan): Migrate + Normalize + Repair = Load ile aynı yol.
        var data = JsonUtility.FromJson(json, T("PlayerSaveData"));
        var original = JsonUtility.FromJson(json, T("PlayerSaveData"));
        var migrate = T("SaveSystem").GetMethod("Migrate", BindingFlags.NonPublic | BindingFlags.Static);
        migrate.Invoke(null, new object[] { data, json });
        T("SaveSystem").GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { data });
        S("SaveInvariants", "Repair", data);
        Assert.AreEqual(14, (int)F(data, "saveVersion"), "sürüm 14");
        Assert.NotNull(F(data, "depot"), "depo listesi oluşturuldu");
        Assert.AreEqual(0, ((IList)F(data, "depot")).Count, "eski kayıt = boş depo");
        foreach (string field in new[] { "gold", "gems", "apprenticeLevel", "builderCount", "lives", "goldMineStoredGold" })
            Assert.AreEqual(F(original, field), F(data, field), field);
        Assert.AreEqual(((IList)F(original, "placedBuildings")).Count, ((IList)F(data, "placedBuildings")).Count, "binalar");
        Assert.AreEqual(((IList)F(original, "placedDecor")).Count, ((IList)F(data, "placedDecor")).Count, "dekorlar");
        Assert.AreEqual(((IList)F(F(original, "island"), "placed")).Count, ((IList)F(F(data, "island"), "placed")).Count, "ada binaları");
        // Normalize: alanı hiç olmayan JSON'da da boş liste (JsonUtility eksik listeyi null bırakabilir)
        var bare = JsonUtility.FromJson("{\"saveVersion\":14}", T("PlayerSaveData"));
        SetF(bare, "depot", null);
        T("SaveSystem").GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { bare });
        Assert.NotNull(F(bare, "depot"));
    }

    [Test]
    public void SameIdInPlacedAndDepotIsRepairedPlacedCopyWins()
    {
        var save = NewSave();
        var placed = (IList)F(save, "placedBuildings");
        placed.Add(Placed("archer_1", "ArcherTower", 6));
        var depot = (IList)F(save, "depot");
        depot.Add(DepotItem("archer_1", 0, 0, "ArcherTower", 6));     // yarıda kalan Store: iki listede
        depot.Add(DepotItem("cannon_9", 0, 0, "CannonTower", 99));    // seviye sınır dışı
        depot.Add(DepotItem("cannon_9", 0, 0, "CannonTower", 3));     // kopya kimlik
        depot.Add(DepotItem("archer_1", 1, 0, "Tower", 2));           // ada: farklı bölge, aynı metin kimlik → ayrı nesne
        var problems = (System.Collections.Generic.List<string>)S("SaveInvariants", "Check", save);
        Assert.IsTrue(problems.Any(p => p.Contains("hem yerleşik hem depoda")), "Check yakalar");
        Assert.IsTrue((bool)S("SaveInvariants", "Repair", save));
        Assert.AreEqual(1, placed.Count, "yerleşik kopya kalır");
        Assert.AreEqual(2, depot.Count, "depo kopyası ve kopya kimlik silindi: " + string.Join(",", depot.Cast<object>().Select(d => F(d, "id"))));
        var cannon = depot.Cast<object>().First(d => (string)F(d, "id") == "cannon_9");
        Assert.AreEqual(15, (int)F(cannon, "level"), "seviye 1..MaxLevel");
        problems = (System.Collections.Generic.List<string>)S("SaveInvariants", "Check", save);
        Assert.IsFalse(problems.Any(p => p.Contains("depo")), string.Join(" | ", problems));
    }

    [Test]
    public void RepairDepotSanitizesNumbersAndDropsUnknownTypes()
    {
        var save = NewSave();
        var depot = (IList)F(save, "depot");
        var nan = DepotItem("nan_1", 1, 0, "House", 1); SetF(nan, "stock", float.NaN); SetF(nan, "workers", -7);
        var inf = DepotItem("inf_1", 1, 0, "Lumber", 1); SetF(inf, "stock", float.PositiveInfinity);
        depot.Add(nan); depot.Add(inf);
        depot.Add(DepotItem("bad_type", 0, 0, "Nope", 1));
        depot.Add(DepotItem("bad_case", 0, 0, "archertower", 1));
        depot.Add(DepotItem("bad_kind", 1, 0, "Nope", 1));
        depot.Add(DepotItem("bad_region", 5, 0, "House", 1));
        depot.Add(DepotItem("bad_cat", 0, 7, "House", 1));
        depot.Add(DepotItem("bad_decor", 0, 1, "no_such_decor_xyz", 1));
        Assert.IsTrue((bool)S("SaveInvariants", "RepairDepot", save));
        Assert.AreEqual(2, depot.Count, string.Join(",", depot.Cast<object>().Select(d => F(d, "id"))));
        Assert.AreEqual(0f, (float)F(nan, "stock"), "NaN stok");
        Assert.AreEqual(-1, (int)F(nan, "workers"), "workers < -1");
        Assert.AreEqual(0f, (float)F(inf, "stock"), "Infinity stok");
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator RestoreRejectsOverLimitAndExcludedTypesButNotLegitItems()
    {
        yield return LoadDepotBase();
        // Düzenlenmiş kayıt: 2/2 okçu yerleşikken depoda 3. okçu
        var extra = DepotItem("archer_edit", 0, 0, "ArcherTower", 3);
        Depot.Add(extra);
        string why = (string)S("DepotService", "RestoreBlockedReason", extra);
        Assert.IsNotNull(why, "limit aşımı reddedilir");
        Assert.IsFalse(Restore(BaseAdapter, "archer_edit", Vector2.zero, out _));
        Assert.AreEqual(1, Depot.Count, "depo değişmedi");
        Depot.Clear();
        // Hariç türler
        var main = DepotItem("main_edit", 0, 0, "MainBase", 1);
        Depot.Add(main);
        Assert.IsNotNull(S("DepotService", "RestoreBlockedReason", main), "Ana Üs");
        var castle = DepotItem("castle_edit", 1, 0, "Castle", 1);
        var archery = DepotItem("archery_edit", 1, 0, "Archery", 1);
        Assert.IsNotNull(S("DepotService", "RestoreBlockedReason", castle), "Kıyı Kalesi");
        Assert.IsNotNull(S("DepotService", "RestoreBlockedReason", archery), "Okçuluk Alanı");
        Depot.Clear();
        // Meşru: depola → kurma engellenmez (limit yerleşik+depo)
        Assert.IsTrue(Store(BaseAdapter, "archer_1", 0, out why), why);
        yield return null;
        Assert.IsNull(S("DepotService", "RestoreBlockedReason", Depot[0]), "meşru kurma serbest");
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator GoldMineRestoreKeepsOtherMinesOfflineProduction()
    {
        yield return LoadDepotBase();
        var data = Data;
        SetF(data, "goldMineLastCollectTime", DateTime.UtcNow.AddHours(-2).Ticks);
        float before = Convert.ToSingle(F(data, "goldMineStoredGold"));
        var item = DepotItem("mine_extra", 0, 0, "GoldMine", 1);
        BaseAdapter.GetType().GetMethod("Spawn").Invoke(BaseAdapter, new object[] { item, Vector2.zero });
        float after = Convert.ToSingle(F(data, "goldMineStoredGold"));
        Assert.Greater(after, before, "kurmadan önce mevcut madenin birikimi yazıldı");
    }

    // ==================== ANA ÜS ====================

    [UnityTest, Timeout(120000)]
    public IEnumerator StoredTowerDropsFromBattleAndLimitCountsStoredRestoreIsFreeAndKeepsLevel()
    {
        yield return LoadDepotBase();
        Assert.AreEqual(2, Completed("ArcherTower"), "iki okçu kışlası");
        Assert.IsNotNull(MapObject("archer_1"));
        var pos = PosOf("archer_1");
        int goldBefore = (int)F(Data, "gold");
        int tasksBefore = ((IList)F(Data, "builderTasks")).Count;
        Assert.IsNull(Busy(0, 0, "archer_1"));
        Assert.IsTrue(Store(BaseAdapter, "archer_1", 0, out string why), why);
        yield return null;
        Assert.IsNull(MapObject("archer_1"), "sahneden kalktı");
        Assert.AreEqual(1, Completed("ArcherTower"), "savaşta 1 kule");
        Assert.AreEqual(7, (int)GM.GetType().GetMethod("GetBattleTowerLevel").Invoke(GM, new[] { BuildingType("ArcherTower"), (object)0 }),
            "kalan kulenin seviyesi");
        Assert.AreEqual(2, Owned(0, "ArcherTower"), "sahip olunan = yerleşik + depo");
        Assert.AreEqual(1, Depot.Count);
        // İnşa Et: limit (Ana Üs 15'te 2) doldu sayılır; etiket depoyu gösterir
        var menu = T("BuildMenuController").GetProperty("Instance").GetValue(null);
        menu.GetType().GetMethod("Show").Invoke(menu, null);
        yield return null;
        var card = GameObject.Find("Item_ArcherTower");
        Assert.NotNull(card);
        Assert.IsFalse(card.GetComponent<Button>().interactable, "limit: yerleşik 1 + depo 1 = 2/2");
        StringAssert.Contains("1/2", Session4Tests.Text(card.transform.Find("PlacedCount")));
        menu.GetType().GetMethod("Hide").Invoke(menu, null);
        // Kurma: ücretsiz, inşaatçı yok, seviye korunur; en çok 1 kez
        Assert.IsTrue(Restore(BaseAdapter, "archer_1", pos, out why), why);
        yield return null;
        Assert.IsFalse(Restore(BaseAdapter, "archer_1", pos, out _), "ikinci kurma yok");
        Assert.AreEqual(0, Depot.Count);
        Assert.AreEqual(goldBefore, (int)F(Data, "gold"), "altın harcanmaz");
        Assert.AreEqual(tasksBefore, ((IList)F(Data, "builderTasks")).Count, "inşaatçı görevi yok");
        var restored = ((IEnumerable)F(Data, "placedBuildings")).Cast<object>().First(p => (string)F(p, "slotId") == "archer_1");
        Assert.AreEqual(10, (int)F(restored, "level"), "seviye korunur");
        Assert.AreEqual(2, Completed("ArcherTower"));
        Assert.IsNotNull(MapObject("archer_1"));
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator StoredBarracksHouseAndMineLoseTheirEffects()
    {
        yield return LoadDepotBase();
        var gmType = GM.GetType();
        // Kışla depoda → Lancer yok
        Assert.AreEqual(2, ((IList)S("AllyBarracks", "BarracksLevels", Data)).Count);
        Assert.IsTrue(Store(BaseAdapter, "barracks_1", 0, out string why), why);
        Assert.IsTrue(Store(BaseAdapter, "barracks_2", 0, out why), why);
        Assert.AreEqual(0, ((IList)S("AllyBarracks", "BarracksLevels", Data)).Count, "depodaki Kışla Lancer vermez");
        // Ev depoda → maden deposu kapasitesi düşer
        int capBefore = (int)gmType.GetMethod("MineCapacityAt").Invoke(GM, new object[] { 10 });
        Assert.IsTrue(Store(BaseAdapter, "house_1", 0, out why), why);
        int capAfter = (int)gmType.GetMethod("MineCapacityAt").Invoke(GM, new object[] { 10 });
        Assert.Less(capAfter, capBefore, "Köylü Evi depodayken bonus yok");
        // Maden depoda → üretim ve kapasite 0, çevrimdışı birikim yok
        string mine = FirstSlotOf("GoldMine");
        Assert.NotNull(mine, "maden");
        Assert.Greater((float)gmType.GetMethod("GetGoldPerSecond", Type.EmptyTypes).Invoke(GM, null), 0f);
        var minePos = PosOf(mine);
        Assert.IsTrue(Store(BaseAdapter, mine, 0, out why), why);
        Assert.AreEqual(0f, (float)gmType.GetMethod("GetGoldPerSecond", Type.EmptyTypes).Invoke(GM, null), "depodaki maden üretmez");
        Assert.AreEqual(0, (int)gmType.GetMethod("GetGoldMineStorageCapacity").Invoke(GM, null), "kapasite 0");
        SetF(Data, "goldMineStoredGold", 25);
        SetF(Data, "goldMineLastCollectTime", DateTime.UtcNow.AddHours(-5).Ticks);
        gmType.GetMethod("ApplyOfflineGoldMineProduction").Invoke(GM, null);
        Assert.AreEqual(25, (int)F(Data, "goldMineStoredGold"), "çevrimdışı birikim yok");
        // Geri kurulunca üretim döner, depoda geçen süre sayılmaz
        Assert.IsTrue(Restore(BaseAdapter, mine, minePos, out why), why);
        Assert.Greater((float)gmType.GetMethod("GetGoldPerSecond", Type.EmptyTypes).Invoke(GM, null), 0f, "maden yeniden üretir");
        gmType.GetMethod("ApplyOfflineGoldMineProduction").Invoke(GM, null);
        Assert.LessOrEqual((int)F(Data, "goldMineStoredGold"), 26, "depodaki 5 saat üretim sayılmadı");
        Assert.AreEqual(3, Depot.Count, "2 Kışla + 1 Ev depoda");
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator UpgradingBuildingAndBusyWorkshopAreRejectedWithReason()
    {
        yield return LoadDepotBase(save =>
        {
            var tasks = (IList)F(save, "builderTasks");
            var task = Activator.CreateInstance(T("BuilderTaskSave"));
            SetF(task, "builderIndex", 0); SetF(task, "kind", "upgrade"); SetF(task, "slotId", "archer_1"); SetF(task, "buildingType", "ArcherTower");
            SetF(task, "startTicks", DateTime.UtcNow.Ticks); SetF(task, "endTicks", DateTime.UtcNow.AddHours(3).Ticks);
            tasks.Add(task);
            var queue = (IList)F(F(save, "workshop"), "queue");
            var job = Activator.CreateInstance(T("WorkshopJob"));
            SetF(job, "startTicks", DateTime.UtcNow.Ticks); SetF(job, "endTicks", DateTime.UtcNow.AddHours(1).Ticks);
            queue.Add(job);
        });
        string reason = Busy(0, 0, "archer_1");
        Assert.NotNull(reason, "yükseltme sürerken ret");
        StringAssert.Contains("İnşaat", reason);
        Assert.IsFalse(Store(BaseAdapter, "archer_1", 0, out _));
        Assert.IsNotNull(MapObject("archer_1"), "bina yerinde");
        Assert.AreEqual(0, Depot.Count);
        Assert.NotNull(Busy(0, 0, "workshop_1"), "atölye üretimi sürerken ret");
        // Menüde düğme var ama gri: dokunuş depolamaz, sebep söylenir
        var setup = BaseSetup;
        var menu = (Component)setup.GetType().GetMethod("ShowBuildingActions").Invoke(setup, new[] { MapObject("archer_1") });
        Assert.NotNull(Btn(menu, "depot"), "DEPOYA düğmesi");
        Btn(menu, "depot").onClick.Invoke();
        yield return null;
        Assert.IsFalse((bool)menu.GetType().GetProperty("IsConfirming").GetValue(menu), "engelli düğme onay açmaz");
        Assert.AreEqual(0, Depot.Count);
        // Ana Üs kalesi: DEPOYA yok
        var castle = MapObject(FirstSlotOf("MainBase"));
        var castleMenu = (Component)setup.GetType().GetMethod("ShowBuildingActions").Invoke(setup, new[] { castle });
        Assert.IsNull(Btn(castleMenu, "depot"), "Ana Üs depoya alınamaz");
    }

    [UnityTest, Timeout(150000)]
    public IEnumerator ThreeButtonMenuDepotTabAndSceneReloadStayConsistent()
    {
        yield return LoadDepotBase();
        var setup = BaseSetup;
        var tower = MapObject("cannon_1");
        var go = (GameObject)F(tower, "gameObject");
        Camera.main.transform.position = new Vector3(go.transform.position.x, go.transform.position.y + 1f, Camera.main.transform.position.z);
        yield return null;
        var menu = (Component)setup.GetType().GetMethod("ShowBuildingActions").Invoke(setup, new[] { tower });
        var ids = ((IEnumerable)P(menu, "ButtonIds")).Cast<string>().ToList();
        CollectionAssert.AreEqual(new[] { "info", "move", "depot" }, ids, "3 düğme: YÜKSELT, TAŞI, DEPOYA");
        yield return new WaitForSecondsRealtime(0.3f);
        yield return CaptureBaseUi("g9_ana_us_menu_3_dugme_1080x1920");
        // Onay satırı: ✓ depoya alır
        Btn(menu, "depot").onClick.Invoke();
        yield return null;
        Assert.IsTrue((bool)menu.GetType().GetProperty("IsConfirming").GetValue(menu), "onay sorulur");
        Btn(menu, "yes").onClick.Invoke();
        yield return null;
        Assert.AreEqual(1, Depot.Count, "onaydan sonra depoda");
        // Dekor: TAŞI / DEPOYA / SİL
        var dpc = T("DecorPlacementController").GetProperty("Instance").GetValue(null);
        var decor = ((IEnumerable)P(dpc, "Placed")).Cast<Component>().First(d => d != null);
        string decorId = (string)P(decor, "InstanceId");
        dpc.GetType().GetMethod("ShowActions").Invoke(dpc, new object[] { decor });
        yield return null;
        var decorMenu = ActionMenu;
        CollectionAssert.AreEqual(new[] { "move", "depot", "delete" }, ((IEnumerable)P(decorMenu, "ButtonIds")).Cast<string>().ToList());
        Assert.IsTrue(Store(BaseAdapter, decorId, 1, out string why), why);
        yield return null;
        Assert.IsFalse(((IEnumerable)F(Data, "placedDecor")).Cast<object>().Any(d => (string)F(d, "instanceId") == decorId));
        // DEPO sekmesi: iki kart, sayı rozeti
        var build = T("BuildMenuController").GetProperty("Instance").GetValue(null);
        build.GetType().GetMethod("ShowDepot").Invoke(build, null);
        yield return new WaitForSecondsRealtime(0.4f);
        var cards = (IDictionary)build.GetType().GetField("DepotButtons").GetValue(build);
        Assert.AreEqual(2, cards.Count, "kule + dekor");
        Assert.AreEqual(3, (int)P(build, "ActiveTab"));
        yield return CaptureBaseUi("g9_depo_sekmesi_1080x1920");
        // Karta dokunmak ücretsiz yerleştirmeyi açar
        ((Button)cards["cannon_1"]).onClick.Invoke();
        yield return null;
        var pc = T("PlacementController").GetProperty("Instance").GetValue(null);
        Assert.IsTrue((bool)P(pc, "PlacingFromDepot"), "depodan yerleştirme");
        pc.GetType().GetMethod("CancelPlacement").Invoke(pc, null);
        yield return null;
        Assert.AreEqual(2, Depot.Count, "iptal depoyu değiştirmez");
        // Sahne yeniden yüklenince: depodakiler sahnede yok, kayıt tutarlı
        GM.GetType().GetMethod("SaveGame").Invoke(GM, null);
        string json = JsonUtility.ToJson(Data);
        var reloaded = JsonUtility.FromJson(json, T("PlayerSaveData"));
        Assert.AreEqual(2, ((IList)F(reloaded, "depot")).Count, "depo JSON'da kalıcı");
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.IsNull(MapObject("cannon_1"), "depodaki kule sahnede yok");
        Assert.AreEqual(2, Depot.Count);
        Assert.IsFalse(((IEnumerable)F(Data, "placedBuildings")).Cast<object>().Any(p => (string)F(p, "slotId") == "cannon_1"));
        Assert.IsFalse(((IEnumerable)F(Data, "placedDecor")).Cast<object>().Any(d => (string)F(d, "instanceId") == decorId));
        var problems = (System.Collections.Generic.List<string>)S("SaveInvariants", "Check", Data);
        Assert.IsFalse(problems.Any(p => p.Contains("depo")), string.Join(" | ", problems));
        Assert.AreEqual(2, Owned(0, "CannonTower"), "limit sayımı yeniden yüklemede de yerleşik + depo");
    }

    // ==================== ADA ====================

    private static IEnumerator LoadDepotIsland(Action<bool> setDev, Action<object> extra = null)
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            SetF(island, "buildTutorial", 5);
            SetF(island, "introSeen", true);
            SetF(island, "wood", 500); SetF(island, "meat", 200);
            extra?.Invoke(island);
        }, 5, setDev);
    }

    private static object IslandSave => F(Data, "island");
    private static object IslandEntry(string kind) => ((IEnumerable)F(IslandSave, "placed")).Cast<object>().FirstOrDefault(b => (string)F(b, "kind") == kind);

    [UnityTest, Timeout(120000)]
    public IEnumerator IslandStockIsKeptAndFrozenWhileStoredLimitCountsStored()
    {
        yield return LoadDepotIsland(v => { });
        long t0 = DateTime.UtcNow.Ticks;
        IslandPhaseBTests.SetClock(t0);
        var lumber = IslandEntry("Lumber");
        Assert.NotNull(lumber, "hazır köyde Oduncu");
        string id = (string)F(lumber, "id");
        int col = (int)F(lumber, "col"), row = (int)F(lumber, "row");
        SetF(lumber, "stock", 7.5f); SetF(lumber, "lastTick", t0); SetF(lumber, "workers", 1);
        string blockedBefore = (string)S("IslandBuildUi", "KindBlockedReason", IslandPhaseBTests.Kind("Lumber"));
        var args = new object[] { id, null };
        Assert.IsTrue((bool)T("IslandEconomy").GetMethod("TryStore").Invoke(null, args), (string)args[1]);
        Assert.IsNull(IslandEntry("Lumber"), "araziden kalktı");
        var item = Depot.Cast<object>().First(d => (string)F(d, "id") == id);
        Assert.AreEqual(7.5f, (float)F(item, "stock"), 0.01f, "stok depo öğesine taşındı");
        Assert.AreEqual(1, (int)F(item, "workers"), "işçi tercihi korunur");
        // Limit: yerleşik 0 + depo 1 → adet doldu (depola → yeni kur yok)
        Assert.AreEqual(blockedBefore, S("IslandBuildUi", "KindBlockedReason", IslandPhaseBTests.Kind("Lumber")), "limit aynı sayılır");
        // 5 saat: depoda üretim yok
        long t1 = t0 + 5 * TimeSpan.TicksPerHour;
        IslandPhaseBTests.SetClock(t1);
        S("IslandEconomy", "Accumulate", IslandSave, t1);
        Assert.AreEqual(7.5f, (float)F(item, "stock"), 0.01f, "depodayken artmaz");
        // Kurma: aynı hücre, ücretsiz; stok ve seviye korunur, son tick = şimdi
        int wood = (int)F(IslandSave, "wood");
        args = new object[] { id, col, row, null };
        Assert.IsTrue((bool)T("IslandEconomy").GetMethod("TryRestore").Invoke(null, args), (string)args[3]);
        var back = IslandEntry("Lumber");
        Assert.NotNull(back);
        Assert.AreEqual(7.5f, (float)F(back, "stock"), 0.01f);
        Assert.AreEqual(t1, (long)F(back, "lastTick"));
        Assert.AreEqual(wood, (int)F(IslandSave, "wood"), "odun harcanmaz");
        Assert.AreEqual(0, Depot.Count);
        Assert.IsFalse((bool)T("IslandEconomy").GetMethod("TryRestore").Invoke(null, new object[] { id, col, row, null }), "ikinci kurma yok");
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator IslandDamagedAndDefenseRejectedArcheryAndKeepHaveNoButton()
    {
        yield return LoadDepotIsland(v => { });
        int island = Const("RegionIsland"), building = Const("CategoryBuilding");
        Assert.IsFalse((bool)S("DepotService", "ShowButton", island, building, "Archery"), "Okçuluk Alanı hariç");
        Assert.IsFalse((bool)S("DepotService", "ShowButton", island, building, "Castle"), "Kıyı Kalesi hariç");
        Assert.IsTrue((bool)S("DepotService", "ShowButton", island, building, "Tower"));
        var towers = ((IEnumerable)F(IslandSave, "placed")).Cast<object>().Where(b => (string)F(b, "kind") == "Tower").ToList();
        Assert.GreaterOrEqual(towers.Count, 2);
        string damaged = (string)F(towers[0], "id"), healthy = (string)F(towers[1], "id");
        var save = Activator.CreateInstance(T("IslandBuildingSave"));
        SetF(save, "id", damaged); SetF(save, "hpFraction", 0.4f); SetF(save, "repairEndTicks", DateTime.UtcNow.AddHours(2).Ticks);
        ((IList)F(IslandSave, "buildings")).Add(save);
        StringAssert.Contains("onar", Busy(island, building, damaged), "hasarlı bina reddedilir");
        Assert.IsFalse((bool)T("IslandEconomy").GetMethod("TryStore").Invoke(null, new object[] { damaged, null }));
        // Ada menüsü: YÜKSELT / TAŞI / DEPOYA (sağlam kule)
        var setup = IslandPhaseBTests.Setup;
        var hud = P(setup, "Hud");
        var menu = (Component)hud.GetType().GetMethod("ShowBuildingActions").Invoke(hud, new object[] { healthy });
        Assert.NotNull(menu);
        CollectionAssert.AreEqual(new[] { "info", "move", "depot" }, ((IEnumerable)P(menu, "ButtonIds")).Cast<string>().ToList());
        var live = (Component)P(setup, "Village").GetType().GetMethod("Live").Invoke(P(setup, "Village"), new object[] { healthy });
        var cam = (Camera)P(setup, "WorldCamera");
        cam.transform.position = new Vector3(live.transform.position.x, live.transform.position.y + 1f, cam.transform.position.z);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Capture("g9_ada_menu_3_dugme_1080x1920", cam);
        // Okçuluk Alanı: DEPOYA yok
        string archery = (string)F(IslandEntry("Archery"), "id");
        var archeryMenu = (Component)hud.GetType().GetMethod("ShowBuildingActions").Invoke(hud, new object[] { archery });
        Assert.IsNull(Btn(archeryMenu, "depot"), "Okçuluk Alanı depoya alınamaz");
        // Savunmada ret (alma ve kurma)
        var defense = setup.GetType().GetProperty("InDefense");
        defense.SetValue(setup, true);
        try
        {
            StringAssert.Contains("Savaş", Busy(island, building, healthy));
            var item = DepotItem("Tower#x", 1, 0, "Tower", 1);
            Assert.NotNull(S("DepotService", "RestoreBlockedReason", item), "savunmada kurulamaz");
        }
        finally { defense.SetValue(setup, false); }
        // Depoya al → İNŞA ET menüsünde DEPO satırı
        Assert.IsTrue((bool)T("IslandEconomy").GetMethod("TryStore").Invoke(null, new object[] { healthy, null }));
        var buildUi = P(hud, "BuildUi");
        hud.GetType().GetMethod("OpenBuildMenu").Invoke(hud, null);
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.IsTrue((bool)P(buildUi, "DepotRowVisible"), "DEPO satırı");
        Assert.IsTrue(((IDictionary)buildUi.GetType().GetField("DepotButtons").GetValue(buildUi)).Contains(healthy));
        yield return Capture("g9_ada_depo_bolumu_1080x1920", cam);
        // İnşa öğreticisi bitmeden düğme yok
        SetF(IslandSave, "buildTutorial", 3);
        Assert.IsFalse((bool)S("DepotService", "ShowButton", island, building, "Tower"), "öğretici sürerken DEPOYA yok");
        SetF(IslandSave, "buildTutorial", 5);
    }
}
