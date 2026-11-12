using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz D ekran görüntüleri (island_base_v2 §10): T−30 ufuk tekneleri + kırmızı kumsal işareti + rozet, Akın Panosu
/// bekleyen baskın satırı, "Sen yokken" raporu ve ada rapor bildirimi. Çıktı <c>PlaytestOutput/island/D*.png</c>.
/// </summary>
public partial class IslandReviewScreenshots
{
    private static readonly long RaidT0 = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc).Ticks;

    private IEnumerator LoadRaidIsland()
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "highestAkin", 9);
            IslandPhaseBTests.Set(island, "zoneMask", 7);
            IslandPhaseBTests.Set(island, "wood", 320); IslandPhaseBTests.Set(island, "meat", 110);
        }, 8, v => devWas = v);
        IslandPhaseBTests.SetClock(RaidT0);
        var island = CurrentSave();
        foreach (var entry in IslandPhaseBTests.Placed(island))
        {
            string kind = (string)IslandPhaseBTests.Field(entry, "kind");
            IslandPhaseBTests.Set(entry, "lastTick", RaidT0);
            if (kind == "Lumber" || kind == "Pasture")
                IslandPhaseBTests.Set(entry, "stock", (float)IslandPhaseBTests.Call("IslandEconomy", "Capacity", entry) * 0.7f);
        }
        IslandPhaseBTests.Set(island, "raidBeach", 1);
        IslandPhaseBTests.Set(island, "raidSeed", 3);
        IslandPhaseBTests.Set(island, "raidSeenTicks", RaidT0);
    }

    [UnityTest]
    public IEnumerator CaptureRaidWarningBoatsMarkerAndBadge()
    {
        yield return LoadRaidIsland();
        var island = CurrentSave();
        IslandPhaseBTests.Set(island, "nextRaidTicks", RaidT0 + 24 * TimeSpan.TicksPerMinute);   // T−24 dk
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        View("SetDefaultView");
        yield return new WaitForSecondsRealtime(1.2f);
        var visuals = Prop(Setup, "RaidVisuals");
        Assert.IsTrue((bool)Prop(visuals, "Visible"), "T−30'da ufuk tekneleri ve işaret görünür");
        Assert.Greater((int)Prop(visuals, "BoatCount"), 0);
        Capture("D01_warning_default_view");

        // Tam görünüm: tüm ada, hedef kumsal açığında tekneler
        View("SetFullView");
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("D02_warning_full_view");

        // Yakın: B kumsalı, tekneler ve kırmızı işaret
        var focus = (Vector2)Prop(visuals, "FocusPoint");
        SetCamera(focus, camera.orthographicSize * 0.42f);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("D03_warning_beach_zoom");

        // Savunma / Akın sırasında gizlenir
        var raid = Prop(Setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 });
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.IsFalse((bool)Prop(visuals, "Visible"), "savunmada ufuk tekneleri gizli");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    [UnityTest]
    public IEnumerator CaptureRaidBoardRowLiveWindowAndIslandReport()
    {
        yield return LoadRaidIsland();
        var island = CurrentSave();
        IslandPhaseBTests.Set(island, "nextRaidTicks", RaidT0 - 12 * TimeSpan.TicksPerMinute);   // T0 geçti: pencere açık
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        View("SetDefaultView");
        yield return new WaitForSecondsRealtime(1.0f);
        Capture("D04_live_window_badge");

        Hud.GetType().GetMethod("OpenBoard").Invoke(Hud, new object[] { 10 });
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("D05_raid_board_pending_row");
        Hud.GetType().GetMethod("OpenBoard").Invoke(Hud, new object[] { 10 });

        // Pencere ada açıkken bitti: çevrimdışı çözüm + ada bildirimi (HUD rapor satırını gösterir)
        var prepField = Hud.GetType().GetField("prepOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prepField.SetValue(Hud, false);
        Hud.GetType().GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(Hud, null);
        IslandPhaseBTests.SetClock(RaidT0 + 3 * TimeSpan.TicksPerHour);
        IslandPhaseBTests.Set(island, "raidSeenTicks", RaidT0 + 3 * TimeSpan.TicksPerHour - TimeSpan.TicksPerMinute);
        yield return new WaitForSecondsRealtime(3f);   // GameManager.Update çözer, HUD toast gösterir
        Assert.AreEqual(1, (int)IslandPhaseBTests.Field(island, "raidsResolved"), "pencere bitince çözüldü");
        Capture("D06_island_report_toast");
    }

    [UnityTest]
    public IEnumerator CaptureAwayReportRaidLine()
    {
        yield return LoadRaidIsland();
        var island = CurrentSave();
        var gm = Type("GameManager").GetProperty("Instance").GetValue(null);
        var player = Prop(gm, "PlayerData");
        // Pencere kapalıyken bitmiş baskın; açık pano HUD'un raporu almasını engeller
        Hud.GetType().GetMethod("OpenBoard").Invoke(Hud, new object[] { 10 });
        IslandPhaseBTests.Set(island, "nextRaidTicks", RaidT0 - 5 * TimeSpan.TicksPerHour);
        IslandPhaseBTests.Set(island, "raidSeenTicks", RaidT0);
        var board = Hud.GetType().GetField("board", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Hud);
        board.GetType().GetMethod("SetVisible").Invoke(board, new object[] { false });   // pano açık sayılır (HUD raporu almaz) ama görünmez
        var uiObject = new GameObject("TestAwayReportUI");
        uiObject.AddComponent(Type("AwayReportUI"));
        yield return new WaitForSecondsRealtime(1.0f);
        Type("AwayReport").GetMethod("Begin").Invoke(null, new object[] { player, 6 * 3600d });
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.AreEqual(1, (int)IslandPhaseBTests.Field(island, "raidsResolved"));
        Assert.IsTrue((bool)Type("AwayReportUI").GetProperty("IsOpen").GetValue(null), "Sen yokken penceresi açıldı");
        string line = (string)Type("IslandRaidScheduler").GetMethod("ReportText").Invoke(null, new object[] { island });
        UnityEngine.Debug.Log("[IslandRaid] rapor satırı (alındıktan sonra boş olabilir): " + line);
        Capture("D07_away_report_raid_line");
        UnityEngine.Object.Destroy(uiObject);
    }
}
