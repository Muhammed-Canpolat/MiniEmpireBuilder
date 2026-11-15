using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// 9. oturum (TECH_AUDIT E-1 / Y-6): Android geri tuşu yöneticisi (BackKeyRouter). Testler tuşa basmak yerine
/// <c>BackKeyRouter.HandleBack()</c> çağırır (Escape → HandleBack bağlantısı Update'tedir). Gerçek kayıt saklanır ve geri konur;
/// zaman ölçeği, duraklatma, açık pencereler ve seçim TearDown'da temizlenir.
/// </summary>
[Timeout(180000)]
public class Session9BackKeyTests : Session9SaveFixture
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [TearDown]
    public void CleanUpWindows()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Invoke("QuitConfirm", "Close");
        Invoke("GemConfirm", "Close", false);
        Invoke("BattlePause", "ResetAll");
        Invoke("SelectionManager", "Reset");
        Invoke("IslandSession", "Clear");
        Time.timeScale = 1f;
    }

    private static object Invoke(string type, string method, params object[] args) =>
        T(type).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

    private static bool Back() => (bool)Invoke("BackKeyRouter", "HandleBack");
    private static bool QuitOpen() => (bool)T("QuitConfirm").GetProperty("IsOpen").GetValue(null);
    private static bool GemConfirmOpen() => (bool)T("GemConfirm").GetProperty("IsOpen").GetValue(null);
    private static bool HasSelection() => (bool)T("SelectionManager").GetProperty("HasSelection").GetValue(null);
    private static bool Paused() => (bool)T("BattlePause").GetProperty("IsPaused").GetValue(null);

    /// <summary>Test oyuncu verisi (Ana Üs 5, ilk rehber bitmiş) ile GameManager hazırlar.</summary>
    private static IEnumerator PrepareGame()
    {
        var managerType = T("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (IEnumerable)Get(save, "buildings"))
            if (Get(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", 5);
        Set(save, "firstBaseGuideDone", true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        Time.timeScale = 1f;
    }

    private static IEnumerator LoadBase()
    {
        yield return PrepareGame();
        Invoke("IslandSession", "Clear");
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 45; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        // Açılışta kendiliğinden çıkmış olabilecek pencereler (Sen yokken vb.) testin konusu değil.
        Invoke("AwayReportUI", "CloseIfOpen");
        Invoke("DailyRewardsUI", "CloseIfOpen");
        Invoke("DailyQuestsUI", "CloseIfOpen");
    }

    private static void SelectDummy(out bool[] closed)
    {
        var flag = new bool[1];
        closed = flag;
        Action close = () => flag[0] = true;
        T("SelectionManager").GetMethod("Select").Invoke(null, new object[] { new object(), close });
    }

    private static void ShowGemConfirm()
    {
        var args = new object[] { "Test", "Test", 5, "TAMAM", (Action)(() => { }), Type.Missing, Type.Missing, Type.Missing,
            Type.Missing, Type.Missing, Type.Missing, Type.Missing };
        T("GemConfirm").GetMethod("Show").Invoke(null, BindingFlags.OptionalParamBinding | BindingFlags.InvokeMethod,
            null, args, null);
    }

    // T1
    [UnityTest]
    public IEnumerator BackInBaseClearsSelectionAndDoesNotAskToQuit()
    {
        yield return LoadBase();
        SelectDummy(out var closed);
        Assert.IsTrue(HasSelection(), "seçim açık");

        Assert.IsTrue(Back(), "geri tuşu işlendi");
        Assert.IsTrue(closed[0], "seçimin kapatma işlevi çağrıldı");
        Assert.IsFalse(HasSelection(), "seçim temizlendi");
        Assert.IsFalse(QuitOpen(), "seçim varken çıkış onayı açılmaz");
    }

    // T2
    [UnityTest]
    public IEnumerator BackInBaseWithNothingOpenTogglesQuitConfirm()
    {
        yield return LoadBase();
        Assert.IsFalse(QuitOpen());

        Assert.IsTrue(Back());
        yield return null;
        // Açık kalmış radyal menü (durumu PlayerPrefs'te saklanır; tam koşuda önceki testlerden açık gelir) ya da geç beliren bir pencere varsa
        // ilk basış onu kapatır: kural gereği doğru davranış. Çıkış onayı ancak kapatılacak bir şey kalmayınca açılır.
        for (int extra = 0; extra < 3 && !QuitOpen() && (string)T("BackKeyRouter").GetProperty("LastStep").GetValue(null) != "scene"; extra++)
        {
            Assert.IsTrue(Back());
            yield return null;
        }
        Assert.IsTrue(QuitOpen(), "hiçbir şey açık değilken çıkış onayı açılır; işleyen basamak: " + T("BackKeyRouter").GetProperty("LastStep").GetValue(null));
        var canvas = GameObject.Find("QuitConfirm").GetComponent<Canvas>();
        Assert.GreaterOrEqual(canvas.sortingOrder, 1300, "çıkış onayı her pencerenin üstünde");
        Assert.NotNull(GameObject.Find("StayButton"), "KAL düğmesi");
        Assert.NotNull(GameObject.Find("QuitButton"), "ÇIK düğmesi");

        Assert.IsTrue(Back());
        yield return null;
        Assert.IsFalse(QuitOpen(), "ikinci geri tuşu çıkış onayını kapatır");
    }

    // T3
    [UnityTest]
    public IEnumerator BackClosesOnlyTheTopWindow()
    {
        yield return LoadBase();
        SelectDummy(out var closed);
        ShowGemConfirm();
        yield return null;
        Assert.IsTrue(GemConfirmOpen(), "elmas onayı açık");

        Assert.IsTrue(Back());
        yield return null;
        Assert.IsFalse(GemConfirmOpen(), "geri tuşu açık pencereyi kapatır");
        Assert.IsTrue(HasSelection(), "alttaki seçime dokunmaz");
        Assert.IsFalse(closed[0]);
        Assert.IsFalse(QuitOpen(), "çıkış onayı açılmaz");

        Assert.IsTrue(Back());
        Assert.IsFalse(HasSelection(), "ikinci geri tuşu seçimi temizler");
        Assert.IsFalse(QuitOpen());
    }

    // T4
    [UnityTest]
    public IEnumerator BackInBattlePausesAndResumes()
    {
        yield return Session4Tests.StartBattle(1, 2);
        yield return new WaitForSeconds(1f);
        Assert.IsFalse(Paused());

        Assert.IsTrue(Back());
        yield return null;
        Assert.IsTrue(Paused(), "savaşta geri tuşu ayarlar/duraklatma penceresini açar");
        Assert.AreEqual(0f, Time.timeScale, "zaman durur");
        Assert.IsFalse(QuitOpen(), "savaşta çıkış onayı açılmaz");

        Assert.IsTrue(Back());
        yield return null;
        Assert.IsFalse(Paused(), "ikinci geri tuşu oyunu sürdürür");
        Assert.AreEqual(1f, Time.timeScale, 1e-4);
    }

    // T5
    [UnityTest]
    public IEnumerator BackInCampaignClosesTheOpenRegionPanel()
    {
        yield return PrepareGame();
        Invoke("IslandSession", "Clear");
        SceneManager.LoadScene("CampaignScene");
        for (int frame = 0; frame < 30; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        var setup = Object.FindFirstObjectByType(T("CampaignSceneSetup")) as Component;
        Assert.NotNull(setup, "sefer haritası");
        var region = T("CampaignCatalog").GetMethod("Get").Invoke(null, new object[] { 1 });
        setup.GetType().GetMethod("ShowRegion", Any).Invoke(setup, new[] { region });
        yield return null;
        setup.GetType().GetMethod("SelectLevel", Any).Invoke(setup, new object[] { 1 });
        yield return null;
        var detail = setup.GetType().GetProperty("DetailIsOpen");
        Assert.IsTrue((bool)detail.GetValue(setup), "bölge paneli açık");

        Assert.IsTrue(Back());
        yield return null;
        Assert.IsFalse((bool)detail.GetValue(setup), "geri tuşu bölge panelini kapatır");
        Assert.AreEqual("CampaignScene", SceneManager.GetActiveScene().name, "sahne değişmedi");
        Assert.IsFalse(QuitOpen());

        // Panel kapalıyken bölge ekranından haritaya dönülür (‹ Harita); sahne yine değişmez.
        Assert.IsTrue(Back());
        yield return null;
        var regionRoot = (Component)setup.GetType().GetField("regionRoot", Any).GetValue(setup);
        Assert.IsFalse(regionRoot.gameObject.activeSelf, "bölge ekranı kapandı, harita açık");
        Assert.AreEqual("CampaignScene", SceneManager.GetActiveScene().name);
    }
}
