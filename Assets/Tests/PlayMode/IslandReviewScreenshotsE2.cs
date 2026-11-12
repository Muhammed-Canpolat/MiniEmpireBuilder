using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz E2 ekran görüntüleri (island_base_v2 §21): Kademe II–IV düşman dizilişleri, siyah/sarı tekneler, Akın 20 ve 30 bossu savaşta,
/// yetenek düğmeleri. Çıktı <c>PlaytestOutput/island/E2_*.png</c>.
/// </summary>
public partial class IslandReviewScreenshots
{
    private static void Lineup((string name, float x, float y, bool boss)[] specs, string capture, Vector2 center, float size, int level = 1)
    {
        var castle = (Component)Prop(Setup, "CastleStructure");
        var spawn = Type("IslandEnemy").GetMethod("Spawn");
        foreach (var (name, x, y, boss) in specs)
        {
            var enemy = spawn.Invoke(null, new object[] { Enum.Parse(Type("EnemyType"), name), level, castle.transform, new Vector2(x, y), null, 0, boss, false });
            var controller = Prop(enemy, "Controller");
            controller.GetType().GetProperty("MovementLocked").SetValue(controller, true);
        }
        var cameraController = Prop(Setup, "CameraController");
        cameraController.GetType().GetProperty("Locked").SetValue(cameraController, true);
        SetCamera(center, size);
    }

    [UnityTest]
    public IEnumerator CapturePhaseE2EnemyLineups()
    {
        yield return LoadIsland();
        Setup.GetType().GetMethod("SetKeepOverride").Invoke(Setup, new object[] { 15 });
        var nature = GameObject.Find("IslandNature");
        if (nature != null) nature.SetActive(false);
        foreach (var sheep in UnityEngine.Object.FindObjectsByType(Type("IslandSheep"), FindObjectsSortMode.None)) ((Component)sheep).gameObject.SetActive(false);
        // Kademe II: Lanetli Tayfa
        Lineup(new[]
        {
            ("SkeletonWarrior", 4.2f, 21.4f, false), ("SkeletonArcher", 6.8f, 21.4f, false), ("BoneMage", 9.4f, 21.4f, false), ("Imp", 12.0f, 21.4f, false),
            ("Spider", 4.6f, 17.8f, false), ("Zombie", 7.4f, 17.8f, false), ("StoneGolem", 10.4f, 17.8f, false), ("Ghost", 13.2f, 17.8f, false),
        }, "E2_01_tier2", new Vector2(8.6f, 19.6f), 11.0f);
        yield return new WaitForSecondsRealtime(0.7f);
        Capture("E2_01_tier2_lineup");
        foreach (var e in LiveEnemies()) UnityEngine.Object.Destroy(e.gameObject);
        yield return null;
        // Kademe III: Batık Lejyon
        Lineup(new[]
        {
            ("ZombieWarrior", 4.4f, 21.4f, false), ("Succubus", 7.2f, 21.4f, false), ("Vampire", 10.0f, 21.4f, false), ("Demon", 13.0f, 21.4f, false),
            ("CrystalGolem", 5.2f, 17.8f, false), ("MagmaGolem", 9.0f, 17.8f, false), ("Raider", 12.6f, 17.8f, false),
        }, "E2_02_tier3", new Vector2(8.8f, 19.6f), 11.0f);
        yield return new WaitForSecondsRealtime(0.7f);
        Capture("E2_02_tier3_lineup");
        foreach (var e in LiveEnemies()) UnityEngine.Object.Destroy(e.gameObject);
        yield return null;
        // Bosslar: Kasap Kaptan, Batık Şövalye (Akın 20), Kızıl Ejder (Akın 30); yanında Kızıl Warrior ölçüsü için
        Lineup(new[]
        {
            ("Raider", 4.4f, 20.0f, false), ("ButcherCaptain", 7.4f, 20.0f, true), ("SunkenKnight", 10.9f, 20.0f, true), ("RedDragon", 14.4f, 20.4f, true),
        }, "E2_03_bosses", new Vector2(9.2f, 20.4f), 10.0f, 20);
        yield return new WaitForSecondsRealtime(0.7f);
        Capture("E2_03_bosses_lineup");
    }

    private static System.Collections.Generic.List<Component> LiveEnemies() =>
        ((System.Collections.IEnumerable)Type("IslandEnemy").GetProperty("All").GetValue(null)).Cast<Component>().Where(c => c != null).ToList();

    private static Component FindEnemy(string typeName) =>
        LiveEnemies().FirstOrDefault(e => (bool)Prop(e, "Alive") && Prop(e, "Type").ToString() == typeName);

    /// <summary>Savaş ekranı: Akın başlar, hızlı oyunla bekler; koşul sağlanınca yavaşlatıp görüntü alır.</summary>
    private IEnumerator RunBattleAndCapture(int akin, int keep, Func<bool> moment, float maxRealSeconds, Action<string> capture, string profile = "Good")
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "highestAkin", akin - 1);
            IslandPhaseE2Tests.BuildDefense(island, keep, akin);   // ölçümle aynı yerleşim: KK−1 kule, Kışla, Manastır
        }, keep, v => devWas = v);
        var raid = Prop(Setup, "Raid");
        Type("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, BotProfile(profile) });
        BattleCamera();
        Time.timeScale = 1f;
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { akin });
        yield return new WaitForSecondsRealtime(0.9f);
        Time.timeScale = 6f;
        float deadline = Time.realtimeSinceStartup + maxRealSeconds;
        while (!moment() && Time.realtimeSinceStartup < deadline && RaidPhase() == "Running") yield return null;
        Time.timeScale = 1f;
        capture("moment");
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator CapturePhaseE2BlackBoatsAkin12AndAbilityButtons()
    {
        // Akın 12: Kademe II siyah-kafatası tekneler; KK 6'da üç yetenek düğmesi de açık
        yield return RunBattleAndCapture(12, 6, ShipInView, 60f, _ => { });
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("E2_04_black_boats_akin12_battle_hud");
        // Kademe II karışımı: iskelet/İmp/Örümcek vb. kıyıda
        var raid = Prop(Setup, "Raid");
        Time.timeScale = 6f;
        yield return WaitUntilReal(() => LiveEnemies().Count(e => (bool)Prop(e, "Alive")) >= 5, 60f);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.3f);
        Capture("E2_05_tier2_wave_on_the_beach");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    /// <summary>Sandal olmayan (Kenney gemisi: siyah/sarı kademe rengi) bir tekne kamera görüşünde mi.</summary>
    private static bool ShipInView()
    {
        var camera = (Camera)Prop(Setup, "WorldCamera");
        foreach (var boat in ((System.Collections.IEnumerable)Type("RaidBoat").GetProperty("All").GetValue(null)).Cast<Component>())
        {
            if (boat == null || !(bool)Prop(boat, "Alive") || Prop(boat, "Kind").ToString() == "Rowboat") continue;
            var view = camera.WorldToViewportPoint(boat.transform.position);
            if (view.x > 0.1f && view.x < 0.9f && view.y > 0.12f && view.y < 0.8f) return true;
        }
        return false;
    }

    private static int RaidBoatCount() =>
        ((System.Collections.IEnumerable)Type("RaidBoat").GetProperty("All").GetValue(null)).Cast<Component>().Count(b => b != null && (bool)Prop(b, "Alive"));

    [UnityTest, Timeout(900000)]
    public IEnumerator CapturePhaseE2SunkenKnightAkin20()
    {
        // Akın 20 bossu: Batık Şövalye (son dalgada, siyah Mavna ile); bot yetenek kullanır
        yield return RunBattleAndCapture(20, 7, () => FindEnemy("SunkenKnight") != null, 300f, _ => { });
        var knight = FindEnemy("SunkenKnight");
        if (knight == null) { Assert.Inconclusive("Akın 20: bot boss'a gelmeden yenildi (görüntü alınamadı)"); yield break; }
        var cameraController = Prop(Setup, "CameraController");
        cameraController.GetType().GetProperty("Locked").SetValue(cameraController, true);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("E2_06_akin20_boss_battle_view");
        SetCamera((Vector2)Prop(knight, "Position") + new Vector2(0f, 1.0f), 4.6f);
        yield return new WaitForSecondsRealtime(0.3f);
        Capture("E2_07_akin20_sunken_knight_close");
        // %50 can altına inince 4 iskelet çağırır
        var controller = Prop(knight, "Controller");
        controller.GetType().GetMethod("TakeDamage").Invoke(controller, new object[] { (float)Prop(controller, "CurrentHealth") * 0.55f, Enum.Parse(Type("DamageSource"), "Env") });
        yield return new WaitForSecondsRealtime(0.8f);
        SetCamera((Vector2)Prop(knight, "Position") + new Vector2(0f, 0.8f), 5.0f);
        Capture("E2_08_akin20_knight_summons_skeletons");
        Prop(Setup, "Raid").GetType().GetMethod("Abandon").Invoke(Prop(Setup, "Raid"), null);
    }

    [UnityTest, Timeout(900000)]
    public IEnumerator CapturePhaseE2YellowBoatsAndRedDragonAkin30()
    {
        // Akın 30: Kademe IV sarı tekneler ve Kızıl Ejder (uçan boss, son dalgada kenardan)
        yield return RunBattleAndCapture(30, 10, ShipInView, 60f, _ => { });
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("E2_09_yellow_boats_akin30");
        var raid = Prop(Setup, "Raid");
        Time.timeScale = 6f;
        yield return WaitUntilReal(() => FindEnemy("RedDragon") != null || RaidPhase() != "Running", 420f);
        var dragon = FindEnemy("RedDragon");
        Time.timeScale = 1f;
        if (dragon == null) { Assert.Inconclusive("Akın 30: bot Ejder'e gelmeden yenildi"); yield break; }
        yield return new WaitForSecondsRealtime(0.3f);
        Capture("E2_10_akin30_dragon_battle_view");
        var cameraController = Prop(Setup, "CameraController");
        cameraController.GetType().GetProperty("Locked").SetValue(cameraController, true);
        SetCamera((Vector2)Prop(dragon, "Position") + new Vector2(0f, 0.4f), 5.0f);
        yield return new WaitForSecondsRealtime(0.3f);
        Capture("E2_11_akin30_red_dragon_close");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator CapturePhaseE2RaidBoardTierTwoToFour()
    {
        yield return LoadIsland(10, 29);
        var hud = Prop(Setup, "Hud");
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        hud.GetType().GetMethod("OpenBoard").Invoke(hud, new object[] { 30 });
        yield return new WaitForSecondsRealtime(0.7f);
        Capture("E2_12_raid_board_akin30");
        hud.GetType().GetMethod("OpenBoard").Invoke(hud, new object[] { 20 });
        yield return new WaitForSecondsRealtime(0.7f);
        Capture("E2_13_raid_board_akin20");
    }
}
