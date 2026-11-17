using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>Ana Üs sağ üst açılır menü (D109): kurulum, aç/kapa, rozetler, satır eylemleri, dokunuş yutma, geri tuşu, bayrak kapalı.</summary>
public class BaseDropMenuTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private string savedKey, savedBackup, savedFile;
    private bool? savedFlag;

    private static string SaveFile => System.IO.Path.Combine(Application.persistentDataPath, "save.json");
    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);

    [SetUp]
    public void Keep()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = System.IO.File.Exists(SaveFile) ? System.IO.File.ReadAllText(SaveFile) : null;
    }

    [TearDown]
    public void Restore()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) System.IO.File.WriteAllText(SaveFile, savedFile);
        else if (System.IO.File.Exists(SaveFile)) System.IO.File.Delete(SaveFile);
        PlayerPrefs.Save();
        if (savedFlag != null) { Config.GetType().GetField("miniEmpireDropMenu").SetValue(Config, savedFlag.Value); savedFlag = null; }
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
    }

    private static Transform Hud => GameObject.Find("BaseCanvas").transform.Find("SafeArea/HudRoot");
    private static Component Menu => (Component)Hud.GetComponentInChildren(T("BaseDropMenu"), true);
    private static R Call<R>(Component c, string method, params object[] args) => (R)c.GetType().GetMethod(method).Invoke(c, args);
    private static bool IsOpen(Component m) => (bool)m.GetType().GetProperty("IsOpen").GetValue(m);
    private static IList Items => (IList)T("BaseDropMenu").GetField("Items").GetValue(null);
    private static bool Back() => (bool)T("BackKeyRouter").GetMethod("HandleBack").Invoke(null, null);
    private static string LastStep => (string)T("BackKeyRouter").GetProperty("LastStep").GetValue(null);
    private static bool AnyNamed(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(t => t.name == name);

    private static IEnumerator LoadBase(int mainBase = 7, Action<object> edit = null)
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
        Set(Field(save, "island"), "announced", true);
        edit?.Invoke(save);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        T("AwayReportUI").GetMethod("CloseIfOpen").Invoke(null, null);
        T("DailyRewardsUI").GetMethod("CloseIfOpen").Invoke(null, null);
        yield return null;
    }

    private static List<RaycastResult> RaycastAt(Vector2 screen)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, results);
        results.RemoveAll(r => r.gameObject.name == "VirtualJoystickInput" || r.gameObject.name == "VirtualJoystick");
        return results;
    }

    private static Vector2 CenterOf(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));

    [UnityTest]
    public IEnumerator FlagOnBuildsMenuWithoutOldSideButtons()
    {
        yield return LoadBase();
        Assert.IsFalse(AnyNamed("DailyButton"), "eski GÜNLÜK düğmesi yok");
        Assert.IsFalse(AnyNamed("QuestsButton"), "eski GÖREV düğmesi yok");
        var menu = Menu;
        Assert.NotNull(menu);
        Assert.NotNull(Hud.Find("DropMenu/DropMenuToggle"));
        Assert.IsFalse(IsOpen(menu), "her girişte kapalı");
        Assert.AreEqual(Items.Count, Call<int>(menu, "get_RowCount"), "satır sayısı = madde sayısı");
        Assert.AreEqual(3, Items.Count);
    }

    [UnityTest]
    public IEnumerator OpenCloseTogglesListShieldAndChevron()
    {
        yield return LoadBase();
        var menu = Menu;
        var toggle = (RectTransform)Hud.Find("DropMenu/DropMenuToggle");
        var list = Hud.Find("DropMenu/DropMenuList");
        var shield = Hud.Find("DropMenu/DropMenuShield");
        Assert.IsFalse(list.gameObject.activeSelf);
        Assert.IsFalse(shield.gameObject.activeSelf);
        float closedZ = Mathf.DeltaAngle(0f, toggle.Find("Chevron").localEulerAngles.z);
        toggle.GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsTrue(IsOpen(menu));
        Assert.IsTrue(list.gameObject.activeSelf);
        Assert.IsTrue(shield.gameObject.activeSelf);
        Assert.AreEqual(1f, list.localScale.y, 0.01f);
        Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(closedZ, toggle.Find("Chevron").localEulerAngles.z)), 90f, "ok açılınca döner");
        toggle.GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.IsFalse(IsOpen(menu));
        Assert.IsFalse(list.gameObject.activeSelf);
        Assert.IsFalse(shield.gameObject.activeSelf);
        Assert.AreEqual(closedZ, Mathf.DeltaAngle(0f, toggle.Find("Chevron").localEulerAngles.z), 1f);
    }

    [UnityTest]
    public IEnumerator AddingItemIsOneLineAndGrowsList()
    {
        yield return LoadBase();
        var menu = Menu;
        var list = (RectTransform)Hud.Find("DropMenu/DropMenuList");
        Call<object>(menu, "Open");
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        float before = list.rect.height;
        var itemType = T("DropMenuItem");
        var extra = Activator.CreateInstance(itemType, "test", null, "Deneme", "Test", (Action)(() => { }), (Func<bool>)(() => false));
        Items.Add(extra);
        try
        {
            Call<object>(menu, "RebuildRows");
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            Assert.AreEqual(4, Call<int>(menu, "get_RowCount"));
            Assert.Greater(list.rect.height, before + 20f, "gövde kendiliğinden uzar");
        }
        finally { Items.Remove(extra); }
    }

    [UnityTest]
    public IEnumerator ClosedMenuLetsTerrainTapsThroughOnlyToggleBlocks()
    {
        yield return LoadBase();
        var menu = Menu;
        var toggle = (RectTransform)Hud.Find("DropMenu/DropMenuToggle");
        var hits = RaycastAt(CenterOf(toggle));
        Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(toggle), "kapalıyken yalnız ok düğmesi dokunuş yutar");
        // Listenin açıkken olacağı bölge (düğmenin altı) ve arazi ortası boş geçer.
        var below = CenterOf(toggle) + new Vector2(0f, -toggle.rect.height * 2.2f * toggle.lossyScale.y);
        Assert.IsFalse(RaycastAt(below).Any(r => r.gameObject.transform.IsChildOf(Hud.Find("DropMenu"))), "kapalı liste/kalkan yutmaz");
        Assert.IsFalse(RaycastAt(new Vector2(Screen.width * 0.4f, Screen.height * 0.5f)).Any(r => r.gameObject.transform.IsChildOf(Hud.Find("DropMenu"))), "arazi dokunuşu geçer");
        Assert.IsFalse(IsOpen(menu));
    }

    [UnityTest]
    public IEnumerator TapOutsideClosesOnlyAndSwallowsTouch()
    {
        yield return LoadBase();
        var menu = Menu;
        Call<object>(menu, "Open");
        yield return new WaitForSecondsRealtime(0.3f);
        var outside = new Vector2(Screen.width * 0.4f, Screen.height * 0.5f);
        var hits = RaycastAt(outside);
        Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.name == "DropMenuShield", "açıkken liste dışı dokunuş kalkana gider (arazi görmez)");
        // Üst bar bölgesi de kalkanın altında kalır.
        var topHits = RaycastAt(new Vector2(Screen.width * 0.2f, Screen.height - 5f));
        Assert.IsTrue(topHits.Count > 0 && topHits[0].gameObject.name == "DropMenuShield", "barlar da yalnız menüyü kapatır");
        hits[0].gameObject.GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(IsOpen(menu));
        Assert.IsFalse((bool)T("SelectionManager").GetProperty("HasSelection").GetValue(null), "kapanınca seçim kaydı düşer");
        Assert.IsFalse(AnyNamed("DailyRewardsPopup") || AnyNamed("DailyQuestsPopup"), "dışarı dokunuş panel açmaz");
    }

    [UnityTest]
    public IEnumerator BadgesFollowDailyAndQuestSourcesWithoutNewLogic()
    {
        yield return LoadBase();
        var menu = Menu;
        bool daily = (bool)T("DailyRewards").GetProperty("CanClaim").GetValue(null);
        bool quests = (bool)T("DailyQuests").GetProperty("HasClaimable").GetValue(null);
        Call<object>(menu, "Refresh");
        Call<object>(menu, "Open");
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.AreEqual(daily, Call<bool>(menu, "RowBadgeShown", "daily"), "Günlük satır rozeti = DailyRewards.CanClaim");
        Assert.AreEqual(quests, Call<bool>(menu, "RowBadgeShown", "quests"), "Görev satır rozeti = DailyQuests.HasClaimable");
        Assert.IsFalse(Call<bool>(menu, "RowBadgeShown", "events"), "Etkinlik rozeti hep kapalı");
        Assert.AreEqual(daily || quests, Call<bool>(menu, "get_ToggleBadgeShown"), "ok düğmesinde toplu rozet");
        if (!daily) Assert.Inconclusive("bu kayıtta günlük ödül hazır değil");
        T("DailyRewards").GetMethod("Claim").Invoke(null, null);
        yield return new WaitForSecondsRealtime(1.0f);   // D32: 0,5 sn'lik yoklama ≤1 sn'de söndürür
        Assert.IsFalse(Call<bool>(menu, "RowBadgeShown", "daily"), "alındıktan sonra satır rozeti söner");
        Assert.AreEqual((bool)T("DailyQuests").GetProperty("HasClaimable").GetValue(null), Call<bool>(menu, "get_ToggleBadgeShown"));
    }

    [UnityTest]
    public IEnumerator DailyAndQuestRowsOpenTheirPanelsAndCloseMenu()
    {
        yield return LoadBase();
        var menu = Menu;
        Call<object>(menu, "Open");
        yield return new WaitForSecondsRealtime(0.3f);
        Call<RectTransform>(menu, "RowRect", "daily").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.IsFalse(IsOpen(menu), "satır eylemi menüyü kapatır");
        Assert.IsTrue(AnyNamed("DailyRewardsPopup"), "Günlük penceresi açıldı");
        T("DailyRewardsUI").GetMethod("CloseIfOpen").Invoke(null, null);
        yield return null;
        Call<object>(menu, "Open");
        yield return new WaitForSecondsRealtime(0.3f);
        Call<RectTransform>(menu, "RowRect", "quests").GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.IsFalse(IsOpen(menu));
        Assert.IsTrue(AnyNamed("DailyQuestsPopup"), "Görev penceresi açıldı");
        T("DailyQuestsUI").GetMethod("CloseIfOpen").Invoke(null, null);
    }

    [UnityTest]
    public IEnumerator EventsRowIsIconlessAndShowsComingSoonBox()
    {
        yield return LoadBase();
        var menu = Menu;
        Call<object>(menu, "Open");
        yield return new WaitForSecondsRealtime(0.3f);
        var row = Call<RectTransform>(menu, "RowRect", "events");
        Assert.IsNull(row.Find("Icon"), "Etkinlik satırı ikonsuz (kupa eksik asset)");
        Assert.NotNull(Call<RectTransform>(menu, "RowRect", "daily").Find("Icon"));
        row.GetComponent<Button>().onClick.Invoke();
        yield return null;
        Assert.IsFalse(IsOpen(menu));
        Assert.IsTrue(Call<bool>(menu, "get_ComingSoonOpen"), "Yakında kutusu");
        Assert.IsTrue(Back(), "geri tuşu kutuyu kapatır");
        Assert.AreEqual("window", LastStep);
        Assert.IsFalse(Call<bool>(menu, "get_ComingSoonOpen"));
    }

    [UnityTest]
    public IEnumerator BackKeyClosesOpenMenuViaSelection()
    {
        yield return LoadBase();
        var menu = Menu;
        Call<object>(menu, "Open");
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.IsTrue((bool)T("SelectionManager").GetProperty("HasSelection").GetValue(null));
        Assert.IsTrue(Back());
        Assert.AreEqual("selection", LastStep);
        Assert.IsFalse(IsOpen(menu));
    }

    [UnityTest]
    public IEnumerator OtherSelectionClosesMenu()
    {
        yield return LoadBase();
        var menu = Menu;
        Call<object>(menu, "Open");
        yield return null;
        var other = new object();
        T("SelectionManager").GetMethod("Select").Invoke(null, new object[] { other, (Action)(() => { }) });
        Assert.IsFalse(IsOpen(menu), "başka seçim menüyü kapatır");
        T("SelectionManager").GetMethod("Clear").Invoke(null, null);
    }

    [UnityTest]
    public IEnumerator FlagOffKeepsOldSideButtonsAndNoMenu()
    {
        savedFlag = (bool)Config.GetType().GetField("miniEmpireDropMenu").GetValue(Config);
        Config.GetType().GetField("miniEmpireDropMenu").SetValue(Config, false);
        yield return LoadBase();
        Assert.IsNull(Hud.Find("DropMenu"), "bayrak kapalı: menü yok");
        Assert.IsTrue(AnyNamed("DailyButton"), "eski GÜNLÜK düğmesi");
        Assert.IsTrue(AnyNamed("QuestsButton"), "eski GÖREV düğmesi");
    }
}
