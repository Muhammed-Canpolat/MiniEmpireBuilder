using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IslandPhaseBTests;

/// <summary>UI2 (kullanıcı raporu): ada ANA ÜS düğmesi dokununca ana üsse dönmüyordu. Gerçek dokunuş benzetimi (EventSystem.RaycastAll).</summary>
public class IslandHomeButtonTests
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
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        Time.timeScale = 1f;
    }

    private static Transform Safe => GameObject.Find("IslandCanvas").transform.Find("SafeArea");

    /// <summary>Düğme merkezine UI ışını: isabet listesi (en üstten alta).</summary>
    private static List<RaycastResult> HitsAtCenter(Button b)
    {
        var rt = (RectTransform)b.transform;
        var screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
        return hits;
    }

    private static string Describe(List<RaycastResult> hits) =>
        hits.Count == 0 ? "hiçbir şey" : string.Join(" > ", hits.Take(4).Select(h => h.gameObject.name));

    private static Button Home => Safe.Find("TopBar/HomeButton").GetComponent<Button>();

    [UnityTest]
    public IEnumerator HomeButtonTouchReachesButtonAndLeavesToBase()
    {
        yield return LoadIsland(null, 3, v => { });
        var home = Home;
        Assert.IsTrue(home.interactable && home.gameObject.activeInHierarchy, "düğme etkin");
        var hits = HitsAtCenter(home);
        Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == home,
            "ANA ÜS dokunuşu düğmeye ulaşmalı; ilk isabet: " + Describe(hits));
        var rt = (RectTransform)home.transform;
        Assert.GreaterOrEqual(rt.rect.height, 44f - 0.01f, "dokunma alanı >= 44 ref birim");

        // Gerçek dokunuş zinciri: ilk isabet üzerinden pointerClick.
        ExecuteEvents.Execute(hits[0].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        float until = Time.realtimeSinceStartup + 10f;
        while (SceneManager.GetActiveScene().name != "BaseScene" && Time.realtimeSinceStartup < until) yield return null;
        Assert.AreEqual("BaseScene", SceneManager.GetActiveScene().name, "ana üse dönüldü");
    }

    [UnityTest]
    public IEnumerator HomeButtonDuringRaidNeedsConfirmThenLeaves()
    {
        yield return LoadIsland(null, 3, v => { });
        var raid = Prop(Setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        yield return new WaitForSecondsRealtime(1.8f);
        var home = Home;
        var hits = HitsAtCenter(home);
        Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == home, "akında düğmeye ulaşır; ilk isabet: " + Describe(hits));
        ExecuteEvents.Execute(hits[0].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        yield return null;
        Assert.AreEqual("BattleScene", SceneManager.GetActiveScene().name, "ilk dokunuş yalnız onay ister");
        hits = HitsAtCenter(home);
        ExecuteEvents.Execute(hits[0].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        float until = Time.realtimeSinceStartup + 10f;
        while (SceneManager.GetActiveScene().name != "BaseScene" && Time.realtimeSinceStartup < until) yield return null;
        Assert.AreEqual("BaseScene", SceneManager.GetActiveScene().name, "ikinci dokunuş ana üse döner");
    }

    [UnityTest]
    public IEnumerator HomeButtonLayoutNoOverlapAndCaptures()
    {
        yield return LoadIsland(null, 3, v => { });
        var camera = (Camera)Prop(Setup, "WorldCamera");
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)Home.transform;
        var safe = (RectTransform)Safe;
        Rect R(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y); }
        var homeRect = R(rect);
        var safeRect = R(safe);
        Assert.IsTrue(safeRect.Contains(homeRect.min) && safeRect.Contains(homeRect.max - new Vector2(0.01f, 0.01f)), "SafeArea içinde");
        foreach (var other in new[] { "TopBar/IslandNameButton", "TopBar/LevelCrest", "RaidBadge" })
        {
            var t = Safe.Find(other);
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            Assert.IsFalse(homeRect.Overlaps(R((RectTransform)t)), "ANA ÜS ile çakışıyor: " + other);
        }
        foreach (var (w, h, tag) in new[] { (1080, 1920, "9x16"), (1536, 2048, "4x3"), (1080, 2340, "9x21") })
            FontCaptureTests.Shoot(camera, w, h, FontCaptureTests.Out("ana-us-butonu-" + tag));
        var raid = Prop(Setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 });
        yield return new WaitForSecondsRealtime(1.8f);
        Canvas.ForceUpdateCanvases();
        foreach (var other in new[] { "TopBar/IslandNameButton", "TopBar/LevelCrest", "RaidBadge" })
        {
            var t = Safe.Find(other);
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            Assert.IsFalse(R((RectTransform)Home.transform).Overlaps(R((RectTransform)t)), "akında ANA ÜS ile çakışıyor: " + other);
        }
        FontCaptureTests.Shoot(camera, 1080, 1920, FontCaptureTests.Out("ana-us-butonu-akin-9x16"));
    }
}
