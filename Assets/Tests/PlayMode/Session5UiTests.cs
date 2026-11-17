using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// 5. oturum (2.1): dünya eylem menüsü (<see cref="WorldActionMenu"/>) — dekor ve binaya dokunmak nesnenin üstünde süslü menü açar,
/// ekran güvenli alanında kalır, SİL onay ister ve iadesini simgeyle gösterir, seçili nesne vurgulanır. Görüntüler <c>PlaytestOutput/session5/</c>.
/// </summary>
[Timeout(300000)]
public class Session5UiTests
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
        CloseMenu();
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        PlayerPrefs.Save();
    }

    private static Component Setup => (Component)Object.FindFirstObjectByType(T("BaseSceneSetup"));
    private static void Tap(Vector3 world)
        => T("BaseSceneSetup").GetMethod("HandleTap", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Setup, new object[] { world });

    // WorldActionMenu yansımayla (test derlemesi Assembly-CSharp'a doğrudan başvurmaz)
    private static Component Menu => (Component)T("WorldActionMenu").GetProperty("Current").GetValue(null);
    private static void CloseMenu() { var m = Menu; if (m != null) m.GetType().GetMethod("Close").Invoke(m, null); }
    private static Button Btn(Component menu, string id) => (Button)menu.GetType().GetMethod("ButtonFor").Invoke(menu, new object[] { id });
    private static RectTransform MenuRect(Component menu) => (RectTransform)Prop(menu, "Rect");
    private static GameObject HighlightOf(Component menu) => menu == null ? null : (GameObject)Prop(menu, "Highlight");
    private static object DecorCtl => T("DecorPlacementController").GetProperty("Instance").GetValue(null);
    private static int Refund(Component item) => (int)T("DecorPlacementController").GetMethod("RefundFor").Invoke(null, new[] { Prop(item, "Data") });
    private static bool Placed(Component item) => ((IEnumerable)Prop(DecorCtl, "Placed")).Cast<object>().Contains(item);
    private static bool HasSelection => (bool)T("SelectionManager").GetProperty("HasSelection").GetValue(null);

    private static RectTransform SafeArea => (RectTransform)GameObject.Find("BaseCanvas").transform.Find("SafeArea");

    /// <summary>Boş (yürünebilir) bir hücrede dekor kurar; sağ kenara yakın hücrelerden başlar.</summary>
    private static Component SpawnDecor(string id, int cellOffset)
    {
        var decor = DecorCtl;
        var data = ((Array)T("DecorPlacementController").GetProperty("All").GetValue(null)).Cast<object>().First(d => (string)Field(d, "id") == id);
        var spawn = T("DecorPlacementController").GetMethod("Spawn", BindingFlags.NonPublic | BindingFlags.Instance);
        var grid = T("BaseWalkGrid").GetField("Shared").GetValue(null);
        var bounds = (RectInt)grid.GetType().GetProperty("Bounds").GetValue(grid);
        var walk = grid.GetType().GetMethod("Walkable");
        int found = 0;
        for (int x = bounds.xMax - 4; x > bounds.xMin + 2; x--)
            for (int y = bounds.yMin + 3; y < bounds.yMax - 4; y++)
            {
                bool free = true;
                for (int dx = -1; dx <= 2 && free; dx++) for (int dy = -1; dy <= 2 && free; dy++)
                    free = (bool)walk.Invoke(grid, new object[] { new Vector2Int(x + dx, y + dy) });
                if (free && found++ == cellOffset)
                    return (Component)spawn.Invoke(decor, new object[] { data, new Vector2Int(x, y), "ui-test-" + id + cellOffset });
            }
        Assert.Fail("boş hücre yok");
        return null;
    }

    private static SpriteRenderer Sr(Component c) => c.GetComponent<SpriteRenderer>();

    private static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);   // Overlay tuvalde dünya = ekran pikseli
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    private static Rect ItemScreenRect(Bounds b)
    {
        var cam = Camera.main;
        var lo = cam.WorldToScreenPoint(b.min); var hi = cam.WorldToScreenPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
    }

    private static object Placement => T("PlacementController").GetProperty("Instance").GetValue(null);

    private static Sprite IconOf(Button b) => b.transform.Find("Icon").GetComponent<Image>().sprite;

    [UnityTest]
    public IEnumerator DecorMenuStaysInsideScreenAboveItemWithIconsAndCoinRefund()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        yield return new WaitForSecondsRealtime(1f);
        var item = SpawnDecor("tree", 0);
        yield return null;
        // Dekoru ekranın sağ kenarına yaklaştır
        var cam = Camera.main;
        var p = item.transform.position;
        float halfW = cam.orthographicSize * cam.aspect;
        cam.transform.position = new Vector3(p.x - halfW * 0.9f, p.y - 0.5f, cam.transform.position.z);
        yield return null;
        Tap(Sr(item).bounds.center);
        yield return new WaitForSecondsRealtime(0.35f);
        var menu = Menu;
        Assert.NotNull(menu, "dekora dokunmak eylem menüsünü açar");
        Assert.AreEqual("DecorActions", menu.name);
        Assert.IsTrue((bool)Prop(DecorCtl, "ActionsVisible"));
        var safe = ScreenRect(SafeArea);
        var mr = ScreenRect(MenuRect(menu));
        var itemRect = ItemScreenRect(Sr(item).bounds);
        Debug.Log($"[UI] menü {mr} güvenli {safe} nesne {itemRect} yan={(string)Prop(menu, "Side")}");
        Assert.GreaterOrEqual(mr.xMin, safe.xMin - 0.5f, "sol kenar");
        Assert.LessOrEqual(mr.xMax, safe.xMax + 0.5f, "sağ kenar taşmaz");
        Assert.LessOrEqual(mr.yMax, safe.yMax + 0.5f);
        Assert.GreaterOrEqual(mr.yMin, safe.yMin - 0.5f);
        if ((string)Prop(menu, "Side") == "above") Assert.GreaterOrEqual(mr.yMin, itemRect.yMax - 2f, "menü nesnenin üstünde");
        else Assert.LessOrEqual(mr.yMax, itemRect.yMin + 2f, "menü nesnenin altında");
        // Simgeler ve iade satırı
        var move = Btn(menu, "move"); var del = Btn(menu, "delete");
        Assert.NotNull(move); Assert.NotNull(del);
        Assert.NotNull(IconOf(move), "TAŞI simgesi"); Assert.NotNull(IconOf(del), "SİL simgesi");
        Assert.AreEqual("ui_icon_move", IconOf(move).texture.name);
        Assert.AreEqual("ui_icon_trash", IconOf(del).texture.name);
        var cost = menu.transform.Find("delete_Cost");
        Assert.NotNull(cost, "iade satırı");
        Assert.AreEqual("ui_icon_coin", cost.Find("Icon").GetComponent<Image>().sprite.texture.name, "altın simgesi");
        int refund = Refund(item);
        StringAssert.Contains("+" + refund, Session4Tests.Text(cost.Find("Text")));
        foreach (var r in Session4Tests.Ratios)
            yield return CaptureRatio("S5_action_menu_decor_" + r.tag, r.w, r.h);
    }

    [UnityTest]
    public IEnumerator DeleteAsksForConfirmationThenRefundsAndHighlightFollowsSelection()
    {
        yield return Nov13FeedbackTests.LoadBase(7);
        yield return new WaitForSecondsRealtime(1f);
        var item = SpawnDecor("bush", 1);
        yield return null;
        var cam = Camera.main;
        cam.transform.position = new Vector3(item.transform.position.x, item.transform.position.y - 0.5f, cam.transform.position.z);
        yield return null;
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        var data = Prop(gm, "PlayerData");
        int refund = Refund(item);
        Set(data, "gold", 100);
        Tap(Sr(item).bounds.center);
        yield return new WaitForSecondsRealtime(0.3f);
        var menu = Menu;
        Assert.NotNull(menu);
        // Vurgu var
        Assert.NotNull(HighlightOf(menu), "seçili dekor vurgulanır");
        Assert.NotNull(item.transform.Find("SelectHighlight"));
        // Silmeye ilk dokunuş yalnız onay satırını açar
        Btn(menu, "delete").onClick.Invoke();
        yield return null;
        Assert.IsTrue((bool)Prop(menu, "IsConfirming"), "onay satırı");
        Assert.NotNull(Btn(menu, "yes")); Assert.NotNull(Btn(menu, "no"));
        Assert.AreEqual("ui_icon_check", IconOf(Btn(menu, "yes")).texture.name);
        Assert.AreEqual("ui_icon_cross", IconOf(Btn(menu, "no")).texture.name);
        Assert.IsTrue(item != null && Placed(item), "ilk dokunuşta dekor silinmedi");
        Assert.AreEqual(100, (int)Field(data, "gold"));
        yield return Session5Tests.CaptureBase("S5_action_menu_confirm");
        // ✗ asıl menüye döner
        Btn(menu, "no").onClick.Invoke();
        yield return null;
        Assert.IsFalse((bool)Prop(menu, "IsConfirming"));
        Assert.NotNull(Btn(menu, "delete"));
        // ✓ siler ve iade eder
        Btn(menu, "delete").onClick.Invoke();
        yield return null;
        Btn(menu, "yes").onClick.Invoke();
        yield return null;
        Assert.IsFalse(Placed(item), "onaydan sonra silindi");
        Assert.AreEqual(100 + refund, (int)Field(data, "gold"), "altın iadesi");
        Assert.IsNull(Menu, "menü kapandı");
        yield return null;
        // Boş yere dokunmak seçimi iptal eder, vurgu kalkar
        var item2 = SpawnDecor("bush", 2);
        yield return null;
        // Dokunuş yerine doğrudan seç: gezen kahraman o anda dekorun önünde olabiliyor (dokunuş önceliği ilk testte denetleniyor)
        var dpc = T("DecorPlacementController").GetProperty("Instance").GetValue(null);
        dpc.GetType().GetMethod("ShowActions").Invoke(dpc, new object[] { item2 });
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.NotNull(HighlightOf(Menu), "ikinci dekor vurgulu");
        Assert.NotNull(item2.transform.Find("SelectHighlight"));
        Tap(new Vector3(-50f, -50f));
        yield return null;
        Assert.IsNull(Menu, "boş yere dokunuş menüyü kapatır");
        Assert.IsNull(item2.transform.Find("SelectHighlight"), "vurgu kalktı");
        Assert.IsFalse(HasSelection);
    }

    [UnityTest]
    public IEnumerator BuildingTapShowsActionMenuCastleHasNoMoveAction()
    {
        yield return Session4Tests.LoadBaseWith(15, save => Session5Tests.FullBase(save));
        yield return new WaitForSecondsRealtime(1.2f);
        var map = T("BaseMapManager").GetProperty("Instance").GetValue(null);
        var objs = ((IEnumerable)Prop(map, "MapObjects")).Cast<object>().ToList();
        var tower = objs.FirstOrDefault(o => Field(o, "objectType").ToString() == "Building" && Field(o, "buildingType")?.ToString() == "ArcherTower");
        Assert.NotNull(tower, "okçu kışlası");
        var cam = Camera.main;
        var col = ((GameObject)Field(tower, "gameObject")).GetComponent<Collider2D>();
        cam.transform.position = new Vector3(col.bounds.center.x, col.bounds.center.y, cam.transform.position.z);
        yield return null;
        Tap(col.bounds.center);
        yield return new WaitForSecondsRealtime(0.35f);
        var menu = Menu;
        Assert.NotNull(menu, "binaya dokunmak aynı eylem menüsünü açar");
        Assert.NotNull(Btn(menu, "move"), "TAŞI");
        Assert.NotNull(Btn(menu, "info"), "YÜKSELT");
        var ui = Object.FindFirstObjectByType(T("BaseWorldUI"));
        Assert.IsFalse((bool)Prop(ui, "IsInfoPanelOpen"), "pencere düğmeden açılır");
        var safe = ScreenRect(SafeArea); var mr = ScreenRect(MenuRect(menu));
        Assert.GreaterOrEqual(mr.xMin, safe.xMin - 0.5f); Assert.LessOrEqual(mr.xMax, safe.xMax + 0.5f);
        foreach (var r in Session4Tests.Ratios) yield return CaptureRatio("S5_action_menu_building_" + r.tag, r.w, r.h);
        // TAŞI taşıma modunu başlatır
        Btn(menu, "move").onClick.Invoke();
        yield return null;
        Assert.IsTrue((bool)Prop(Placement, "IsPlacing"), "taşıma başladı");
        Placement.GetType().GetMethod("CancelPlacement").Invoke(Placement, null);
        yield return null;
        // Kale: yalnız yükselt/bilgi
        Tap(new Vector3(-50f, -50f));
        yield return null;
        var castle = objs.First(o => Field(o, "buildingType")?.ToString() == "MainBase");
        var castleMenu = (Component)T("BaseSceneSetup").GetMethod("ShowBuildingActions").Invoke(Setup, new[] { castle });
        Assert.NotNull(castleMenu);
        Assert.NotNull(Btn(castleMenu, "info"));
        Assert.IsNull(Btn(castleMenu, "move"), "kale taşınamaz");
        // YÜKSELT bina penceresini açar
        Btn(castleMenu, "info").onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsTrue((bool)Prop(ui, "IsInfoPanelOpen"), "YÜKSELT pencereyi açar");
        Assert.IsNull(Menu);
    }

    private static IEnumerator CaptureRatio(string name, int w, int h)
    {
        yield return null;
        yield return null;
        Session4Tests.CaptureUi(name, w, h);
        var src = Path.Combine(Session4Tests.Folder, name + ".png");
        var dst = Path.Combine(Session5Tests.Folder, name + ".png");
        if (File.Exists(src)) { File.Copy(src, dst, true); File.Delete(src); }
    }
}
