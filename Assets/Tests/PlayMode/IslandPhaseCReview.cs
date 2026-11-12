using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz C ekran görüntüleri (island_base_v2 §10 C): düşman ölçek sırası (DM2D + Kızıl + Warrior), AKIN panosu,
/// savaş çerçevesi, iniş, okçuyla çatışma, yarasa sürüsü, golem, Akın 10 Kasap Kaptan ve zafer/yenilgi panelleri.
/// Çıktı <c>PlaytestOutput/island/</c>. Oyuncu yerine <c>IslandBot</c> oynar.
/// </summary>
public partial class IslandReviewScreenshots
{
    private static List<Component> Enemies() =>
        ((System.Collections.IEnumerable)Type("IslandEnemy").GetProperty("All").GetValue(null)).Cast<Component>().Where(c => c != null).ToList();

    private static string EnemyTypeName(Component enemy) => Prop(enemy, "Type").ToString();

    private static IEnumerator WaitUntilReal(Func<bool> condition, float timeoutReal)
    {
        float deadline = Time.realtimeSinceStartup + timeoutReal;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
    }

    private static string RaidPhase() => Prop(Prop(Setup, "Raid"), "Phase").ToString();

    private static int StaticInt(string type, string field) => (int)Type(type).GetField(field).GetValue(null);

    private static void BattleCamera()
    {
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        View("SetBattleView");
    }

    private static object BotProfile(string name) => Enum.Parse(Type("IslandBot+Profile"), name);

    [UnityTest]
    public IEnumerator CaptureEnemyScaleLineup()
    {
        yield return LoadIsland();
        Setup.GetType().GetMethod("SetKeepOverride").Invoke(Setup, new object[] { 15 });
        var castle = (Component)Prop(Setup, "CastleStructure");
        var spawn = Type("IslandEnemy").GetMethod("Spawn");
        // Ölçek karşılaştırması temiz zeminde: doğa nesneleri ve koyunlar gizlenir
        var nature = GameObject.Find("IslandNature");
        if (nature != null) nature.SetActive(false);
        foreach (var sheep in UnityEngine.Object.FindObjectsByType(Type("IslandSheep"), FindObjectsSortMode.None)) ((Component)sheep).gameObject.SetActive(false);
        var specs = new[]
        {
            ("Raider", 5.4f, 21.4f, false), ("ShoreArcher", 7.2f, 21.4f, false), ("ShieldLancer", 9.0f, 21.4f, false), ("Rat", 10.8f, 21.4f, false),
            ("Bat", 5.8f, 18.4f, false), ("WoodGolem", 8.0f, 18.4f, false), ("ButcherCaptain", 10.6f, 18.4f, true),
        };
        foreach (var (name, x, y, boss) in specs)
        {
            var enemy = spawn.Invoke(null, new object[] { Enum.Parse(Type("EnemyType"), name), 1, castle.transform, new Vector2(x, y), null, 0, boss, false });
            var controller = Prop(enemy, "Controller");
            controller.GetType().GetProperty("MovementLocked").SetValue(controller, true);
        }
        var cameraController = Prop(Setup, "CameraController");
        cameraController.GetType().GetProperty("Locked").SetValue(cameraController, true);
        SetCamera(new Vector2(8.1f, 19.7f), 6.0f);
        yield return new WaitForSecondsRealtime(0.6f);
        Capture("13_enemy_scale_lineup");
    }

    private static List<Component> Ground() => Enemies().Where(e => EnemyTypeName(e) != "Bat").ToList();

    private static bool Visible(Component e)
    {
        Vector2 p = e.transform.position;
        return p.x > 1.5f && p.x < IslandLayoutWidth - 1.5f && p.y > 3f && p.y < 32f;
    }

    private static float IslandLayoutWidth => (int)Type("IslandLayout").GetField("Width").GetValue(null);

    private static long clockOffsetTicks;

    private static void AdvanceClock(double seconds)
    {
        clockOffsetTicks += (long)(seconds * TimeSpan.TicksPerSecond);
        long offset = clockOffsetTicks;
        Type("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks + offset));
    }

    /// <summary>Zamanı dondurup bir düşmana yakın plan çeker (DM2D + Kızıl birimlerin yan yana okunurluğu), sonra savaş çerçevesine döner.</summary>
    private static IEnumerator CaptureClose(string name, Component target, float size)
    {
        if (target == null) yield break;
        float scale = Time.timeScale;
        Time.timeScale = 0f;
        SetCamera(target.transform.position, size);
        yield return new WaitForSecondsRealtime(0.25f);
        Capture(name);
        BattleCamera();
        yield return new WaitForSecondsRealtime(0.1f);
        Time.timeScale = scale;
    }

    [UnityTest]
    public IEnumerator CaptureRaidBoardAndDefenseMoments()
    {
        clockOffsetTicks = 0;
        yield return LoadIsland(6, 9);   // ada KK ≥5'te açılır: gerçek oyun durumu
        var raid = Prop(Setup, "Raid");
        var hud = Prop(Setup, "Hud");
        View("SetDefaultView");
        hud.GetType().GetMethod("OpenBoard").Invoke(hud, new object[] { 10 });
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsTrue((bool)Prop(hud, "BoardVisible"), "AKIN panosu açık olmalı");
        Capture("14_raid_board");

        // Akın 10 (Kasap Kaptan): yakın + menzilli + Fare + Kalkanlı Mızrakçı + Ahşap Golem + Yarasa sürüleri
        var bot = (Component)Type("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, BotProfile("Good") });
        Time.timeScale = 4f;
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 10 });
        yield return new WaitForSecondsRealtime(1.0f);
        BattleCamera();
        yield return new WaitForSecondsRealtime(1.5f);
        Time.timeScale = 1f;
        Capture("15_battle_frame");
        var archer = Prop(Setup, "Archer");

        Time.timeScale = 3f;
        yield return WaitUntilReal(() => Ground().Count >= 3 || RaidPhase() != "Running", 90f);
        Capture("16_landing");
        yield return CaptureClose("16b_landing_closeup", Ground().FirstOrDefault(), 5.5f);

        yield return WaitUntilReal(() => RaidPhase() != "Running" || (StaticInt("IslandStats", "Shots") >= 6
            && Ground().Any(e => Vector2.Distance(e.transform.position, ((Component)archer).transform.position) < 7f)
            && UnityEngine.Object.FindObjectsByType(Type("IslandArrow"), FindObjectsSortMode.None).Length > 0), 90f);
        Capture("17_fight_archer");
        yield return CaptureClose("17b_fight_closeup", Ground().FirstOrDefault(e => Vector2.Distance(e.transform.position, ((Component)archer).transform.position) < 7f), 5.5f);

        Time.timeScale = 4f;
        yield return WaitUntilReal(() => RaidPhase() != "Running" || UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .Any(r => r.name == "IslandArrow" && r.transform.localScale.x >= 2f), 120f);
        Capture("18a_flyer_telegraph");
        yield return WaitUntilReal(() => RaidPhase() != "Running" || Enemies().Any(e => EnemyTypeName(e) == "Bat" && Visible(e)), 120f);
        Capture("18_bats");
        yield return CaptureClose("18b_bats_closeup", Enemies().FirstOrDefault(e => EnemyTypeName(e) == "Bat"), 5f);
        yield return WaitUntilReal(() => RaidPhase() != "Running" || Enemies().Any(e => EnemyTypeName(e) == "WoodGolem" && Visible(e)), 120f);
        Capture("19_golem");
        yield return CaptureClose("19b_golem_closeup", Enemies().FirstOrDefault(e => EnemyTypeName(e) == "WoodGolem"), 5f);
        yield return WaitUntilReal(() => RaidPhase() != "Running" || Enemies().Any(e => EnemyTypeName(e) == "ButcherCaptain" && Visible(e)), 240f);
        Capture("22_boss_butcher");
        yield return CaptureClose("22b_boss_closeup", Enemies().FirstOrDefault(e => EnemyTypeName(e) == "ButcherCaptain"), 4.5f);
        Time.timeScale = 6f;
        yield return WaitUntilReal(() => RaidPhase() != "Running", 240f);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("24_boss_result_" + RaidPhase().ToLowerInvariant());
        raid.GetType().GetMethod("ReturnToIdle").Invoke(raid, null);
        yield return new WaitForSecondsRealtime(1.0f);
        // Hasarlı binalar ada görünümünde: çekiç simgesi + onarım sayacı + Usta piyonlar
        SetCamera(new Vector2(11f, 22f), 9.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("23_repair_view");
        UnityEngine.Object.Destroy(bot);
    }

    [UnityTest]
    public IEnumerator CaptureDefeatPanel()
    {
        yield return LoadIsland(1, 0);
        var raid = Prop(Setup, "Raid");
        Type("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, BotProfile("None") });
        Type("IslandBot").GetMethod("StripDefenses").Invoke(null, null);
        Time.timeScale = 8f;
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 });
        yield return new WaitForSecondsRealtime(0.8f);
        BattleCamera();
        yield return WaitUntilReal(() => RaidPhase() != "Running", 120f);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual("Lost", RaidPhase(), "savunmasız Akın 1 kaybedilmeli");
        Capture("25_defeat_panel");
    }
}
