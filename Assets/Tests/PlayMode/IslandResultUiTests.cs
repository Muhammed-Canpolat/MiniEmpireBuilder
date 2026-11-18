using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using static IslandPhaseBTests;

/// <summary>UI2 İş 4 (D127): ada Zafer/Yenilgi sonuç ekranı. Hiyerarşi, ödül metni = LastReward, safe içinde kart, taşma/glif, animasyon sonu ölçeği, görüntüler.</summary>
public class IslandResultUiTests
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

    private static string Txt(Transform t)
    {
        var tmp = t.GetComponent("TextMeshProUGUI");
        return (string)tmp.GetType().GetProperty("text").GetValue(tmp);
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
    }

    private static Transform Deep(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    private static void CheckCommon(List<string> problems, string where)
    {
        var panel = Safe.Find("ResultPanel");
        Assert.IsTrue(panel.gameObject.activeSelf, "sonuç ekranı açık");
        Canvas.ForceUpdateCanvases();
        var card = (RectTransform)panel.Find("Card");
        var safeRect = WorldRect((RectTransform)Safe);
        var cardRect = WorldRect(card);
        if (!safeRect.Contains(cardRect.min) || !safeRect.Contains(cardRect.max - new Vector2(0.001f, 0.001f)))
            problems.Add($"{where}: kart SafeArea dışında {cardRect} / {safeRect}");
        foreach (var b in card.GetComponentsInChildren<Button>(false))
        {
            var r = WorldRect((RectTransform)b.transform);
            if (r.yMin < cardRect.yMin - 0.01f || r.yMax > cardRect.yMax + 0.01f) problems.Add($"{where}: {b.name} kart dışında");
        }
        FontCaptureTests.Scan(where, problems);
        FontCaptureTests.Overflow(where, new[] { "ResultPanel" }, problems);
    }

    private static IEnumerator Victory()
    {
        yield return LoadIsland(null, 3, v => { });
        var raid = Prop(Setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        yield return new WaitForSecondsRealtime(1.8f);   // geçici DALGA banner'ı (1.4 sn) bitsin
        raid.GetType().GetMethod("Win", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(raid, null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator VictoryScreenHierarchyRewardsScaleAndCaptures()
    {
        var problems = new List<string>();
        yield return Victory();
        var raid = Prop(Setup, "Raid");
        var camera = (Camera)Prop(Setup, "WorldCamera");
        var panel = Safe.Find("ResultPanel");
        Assert.NotNull(panel, "SafeArea/ResultPanel");
        Assert.NotNull(panel.Find("Card/Next"), "Card/Next");
        Assert.NotNull(panel.Find("Card/Again"), "Card/Again");
        Assert.NotNull(panel.Find("Card/ReturnIsland"), "Card/ReturnIsland");
        Assert.NotNull(Deep(panel, "Title"), "Title");
        Assert.NotNull(Deep(panel, "Body"), "Body");
        Assert.NotNull(panel.Find("Card/Ribbon"), "kurdele");

        // Ödül metni = LastReward (altın, odun, et sırası)
        var reward = Prop(raid, "LastReward");
        int Field(string n) => (int)reward.GetType().GetField(n).GetValue(reward);
        Assert.AreEqual("+" + Field("gold"), Txt(panel.Find("Card/Rewards/Reward0/Amount")), "altın = LastReward");
        Assert.AreEqual("+" + Field("wood"), Txt(panel.Find("Card/Rewards/Reward1/Amount")), "odun = LastReward");
        Assert.AreEqual("+" + Field("meat"), Txt(panel.Find("Card/Rewards/Reward2/Amount")), "et = LastReward");
        StringAssert.Contains(Prop(raid, "Akin").ToString(), Txt(panel.Find("Card/Body")), "akın no");
        Assert.AreEqual(Field("gems") > 0, panel.Find("Card/Rewards/Reward3").gameObject.activeSelf, "elmas yalnız >0");

        // Animasyon bitince ölçek 1 (unscaled)
        Time.timeScale = 0f;
        float until = Time.realtimeSinceStartup + 0.7f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Time.timeScale = 1f;
        var scale = panel.Find("Card").localScale;
        Assert.That(scale.x, Is.EqualTo(1f).Within(0.01f), "kart ölçeği 1");
        Assert.That(panel.Find("Card/Ribbon").localScale.x, Is.EqualTo(1f).Within(0.01f), "kurdele ölçeği 1");
        Assert.That(panel.GetComponent<Image>().color.a, Is.EqualTo(0.72f).Within(0.01f), "zemin alfa");

        CheckCommon(problems, "zafer");
        foreach (var (w, h, tag) in new[] { (1080, 1920, "9x16"), (1536, 2048, "4x3"), (1080, 2340, "9x21") })
            FontCaptureTests.Shoot(camera, w, h, FontCaptureTests.Out("is4-zafer-" + tag));
        Assert.IsEmpty(problems, string.Join("; ", problems));
    }

    [UnityTest]
    public IEnumerator DefeatScreenAgainFirstNextHiddenAndCaptures()
    {
        var problems = new List<string>();
        yield return LoadIsland(null, 3, v => { });
        var raid = Prop(Setup, "Raid");
        var camera = (Camera)Prop(Setup, "WorldCamera");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        yield return new WaitForSecondsRealtime(1.8f);   // geçici DALGA banner'ı (1.4 sn) bitsin
        var castle = (Component)Prop(Setup, "CastleStructure");
        castle.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(castle, new object[] { 1e6f });
        float until = Time.realtimeSinceStartup + 3f;
        while (Prop(raid, "Phase").ToString() == "Running" && Time.realtimeSinceStartup < until) yield return null;
        Assert.AreEqual("Lost", Prop(raid, "Phase").ToString());
        yield return new WaitForSecondsRealtime(0.6f);

        var panel = Safe.Find("ResultPanel");
        var again = panel.Find("Card/Again");
        var back = panel.Find("Card/ReturnIsland");
        Assert.NotNull(again); Assert.NotNull(back);
        Assert.IsFalse(panel.Find("Card/Next").gameObject.activeSelf, "yenilgide Next gizli");
        Assert.IsTrue(again.gameObject.activeSelf);
        Assert.Less(again.GetSiblingIndex(), back.GetSiblingIndex(), "Tekrar en üstte");
        string reason = (string)Prop(raid, "DefeatReason");
        if (!string.IsNullOrEmpty(reason)) StringAssert.Contains(reason, Txt(panel.Find("Card/Body")), "yenilgi nedeni");
        Assert.IsFalse(panel.Find("Card/Rewards").gameObject.activeSelf, "yenilgide ödül satırı yok");
        Assert.That(panel.Find("Card").localScale.x, Is.EqualTo(1f).Within(0.01f), "kart ölçeği 1");

        CheckCommon(problems, "yenilgi");
        foreach (var (w, h, tag) in new[] { (1080, 1920, "9x16"), (1536, 2048, "4x3") })
            FontCaptureTests.Shoot(camera, w, h, FontCaptureTests.Out("is4-yenilgi-" + tag));
        Assert.IsEmpty(problems, string.Join("; ", problems));
    }
}
