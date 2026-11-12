using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz E1 ekran görüntüleri (island_base_v2 §20): Kışla + bayrak (+ panel, bayrak S2), Manastır + Monk (+ panel), Altın Damarı yığını,
/// Z3–Z5 bulutları (kapalı, dağılırken, açık) ve savaşta Lancer'lar / Manastır şifası. Çıktı <c>PlaytestOutput/island/E1*.png</c>.
/// </summary>
public partial class IslandReviewScreenshots
{
    private static (int col, int row) PlaceNear(object island, string kind, int level, int nearCol, int nearRow, string id, int mask = 1)
    {
        IslandPhaseBTests.Call("IslandEconomy", "EnsureSeeded", island, 6);
        Assert.IsTrue(IslandPhaseBTests.FindCellNear(kind, island, mask, nearCol, nearRow, out int c, out int r, 6), kind);
        var entry = IslandPhaseBTests.Entry(id, kind, level, c, r);
        IslandPhaseBTests.Set(entry, "lastTick", DateTime.UtcNow.Ticks);
        IslandPhaseBTests.Placed(island).Add(entry);
        return (c, r);
    }

    private static Vector2[] FlagPoints => (Vector2[])Type("IslandLayout").GetField("FlagPoints").GetValue(null);

    [UnityTest]
    public IEnumerator CapturePhaseE1BarracksAndFlag()
    {
        (int col, int row) at = default;
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "wood", 900); IslandPhaseBTests.Set(island, "meat", 300); IslandPhaseBTests.Set(island, "highestAkin", 3);
            IslandPhaseBTests.Set(island, "zoneMask", 63);   // tüm bölgeler açık: bayrağın çevresinde bulut yok
            at = PlaceNear(island, "Barracks", 5, 12, 10, "Barracks#1");
        }, 6, v => devWas = v);
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        var center = WorldOf(at.col, at.row, 3, 2);
        View("SetDefaultView");
        yield return new WaitForSecondsRealtime(0.5f);
        SetCamera(center + new Vector2(0f, -0.3f), 5.0f);
        yield return new WaitForSecondsRealtime(0.6f);
        Capture("E1_01_barracks");
        SetCamera(FlagPoints[0] + new Vector2(-0.8f, 0.2f), 4.6f);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("E1_01b_flag_S1");

        // Kışla paneli: Lancer sayısı, bayrak, BAYRAK düğmesi
        BuildUi.GetType().GetMethod("ShowBuilding").Invoke(BuildUi, new object[] { "Barracks#1" });
        Hud.GetType().GetMethod("FocusBuilding").Invoke(Hud, new object[] { "Barracks#1" });
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("E1_02_barracks_panel");
        BuildUi.GetType().GetMethod("Close").Invoke(BuildUi, null);

        // Bayrak S2'ye (batı merdiveni): A/C yolu
        var village = Village;
        village.GetType().GetMethod("SetFlag").Invoke(village, new object[] { 1 });
        yield return new WaitForSecondsRealtime(0.7f);
        SetCamera(FlagPoints[1] + new Vector2(1.5f, 0f), 6f);
        yield return new WaitForSecondsRealtime(0.3f);
        Capture("E1_03_flag_moved_S2");
        village.GetType().GetMethod("SetFlag").Invoke(village, new object[] { 2 });
        yield return new WaitForSecondsRealtime(0.7f);
        SetCamera(FlagPoints[2] + new Vector2(-1.5f, 0f), 6f);
        yield return new WaitForSecondsRealtime(0.3f);
        Capture("E1_03b_flag_moved_S3");

        // İNŞA ET menüsü (Kışla, Manastır satırları)
        View("SetDefaultView");
        Hud.GetType().GetMethod("OpenBuildMenu").Invoke(Hud, null);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("E1_04_build_menu");
    }

    [UnityTest]
    public IEnumerator CapturePhaseE1Monastery()
    {
        (int col, int row) at = default;
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "wood", 900); IslandPhaseBTests.Set(island, "meat", 300); IslandPhaseBTests.Set(island, "highestAkin", 3);
            at = PlaceNear(island, "Monastery", 3, 11, 12, "Monastery#1");
        }, 6, v => devWas = v);
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        SetCamera(WorldOf(at.col, at.row, 3, 3) + new Vector2(0f, -0.4f), 5.2f);
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("E1_05_monastery_monk");
        View("SetDefaultView");
        BuildUi.GetType().GetMethod("ShowBuilding").Invoke(BuildUi, new object[] { "Monastery#1" });
        Hud.GetType().GetMethod("FocusBuilding").Invoke(Hud, new object[] { "Monastery#1" });
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("E1_06_monastery_panel");
    }

    [UnityTest]
    public IEnumerator CapturePhaseE1GoldVeinAndZones()
    {
        long t0 = DateTime.UtcNow.Ticks;
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "wood", 6000); IslandPhaseBTests.Set(island, "meat", 1000); IslandPhaseBTests.Set(island, "highestAkin", 3);
        }, 6, v => devWas = v);
        IslandPhaseBTests.SetClock(t0);
        var island0 = CurrentSave();
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        var economy = Type("IslandEconomy");

        // Z3–Z5 kapalı: tam görünümde bulutlar
        View("SetFullView");
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("E1_07_zones_closed_full");
        View("SetDefaultView");
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("E1_07b_zones_closed_default");

        // Z3 bulut paneli (bedel, süre, getirdiği)
        BuildUi.GetType().GetMethod("ShowZone").Invoke(BuildUi, new object[] { 3 });
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("E1_08_zone3_panel");
        BuildUi.GetType().GetMethod("Close").Invoke(BuildUi, null);

        // Z3'ü Usta ile aç; dağılırken ve açıkken görüntü; damar stoku 7 sa birikmiş
        var args = new object[] { 3, false, null };
        Assert.IsTrue((bool)economy.GetMethod("TryStartZone").Invoke(null, args), (string)args[2]);
        yield return new WaitForSecondsRealtime(0.4f);
        SetCamera(new Vector2(16.5f, 29f), 7.5f);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("E1_09_zone3_exploring");
        long done = t0 + 302 * TimeSpan.TicksPerSecond;
        IslandPhaseBTests.SetClock(done);
        economy.GetMethod("Tick").Invoke(null, new object[] { done, true });
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("E1_10_zone3_dispersing");
        yield return new WaitForSecondsRealtime(1.6f);
        long later = done + 7 * TimeSpan.TicksPerHour;
        IslandPhaseBTests.SetClock(later);
        economy.GetMethod("Tick").Invoke(null, new object[] { later, true });
        Village.GetType().GetMethod("RefreshPiles").Invoke(Village, new object[] { true });
        SetCamera(new Vector2(16.0f, 27.0f), 5.2f);
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.Greater((int)Prop(Village, "VeinPileShown"), 0);
        Capture("E1_11_gold_vein_pile");
        int gold = (int)economy.GetMethod("CollectVein").Invoke(null, null);
        Village.GetType().GetMethod("RefreshPiles").Invoke(Village, new object[] { true });
        UnityEngine.Debug.Log($"[E1] damar toplandı: {gold} altın");

        // Z4 ve Z5 de açılır (Usta sırayla); sonra tam görünüm
        long now = later;
        foreach (int zone in new[] { 4, 5 })
        {
            var a = new object[] { zone, false, null };
            Assert.IsTrue((bool)economy.GetMethod("TryStartZone").Invoke(null, a), (string)a[2]);
            now += (zone == 4 ? 905L : 3605L) * TimeSpan.TicksPerSecond;
            IslandPhaseBTests.SetClock(now);
            economy.GetMethod("Tick").Invoke(null, new object[] { now, true });
            yield return new WaitForSecondsRealtime(0.5f);
            if (zone == 5) Capture("E1_12_zone5_dispersing_full");
        }
        View("SetFullView");
        yield return new WaitForSecondsRealtime(2.0f);
        Capture("E1_13_zones_open_full");
    }

    [UnityTest]
    public IEnumerator CapturePhaseE1BattleLancersAndMonasteryHeal()
    {
        (int col, int row) bar = default, mon = default;
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "highestAkin", 5);
            IslandPhaseBTests.Set(island, "zoneMask", 7);   // Z2 açık: Kışla'ya yer (Manastır'ın tek Z0 hücresi doluyken)
            mon = PlaceNear(island, "Monastery", 4, 8, 11, "Monastery#1", 7);
            bar = PlaceNear(island, "Barracks", 10, 6, 18, "Barracks#1", 7);
        }, 8, v => devWas = v);
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        var raid = Prop(Setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 5 });
        var boats = (System.Collections.IEnumerable)Type("RaidBoat").GetProperty("All").GetValue(null);
        foreach (var boat in boats.Cast<Component>().ToList()) if (boat != null) UnityEngine.Object.Destroy(boat.gameObject);
        yield return new WaitForSecondsRealtime(5.5f);   // Lancer'lar bayrağa koşar (Kışla Z2'de, bayrak S1'de)
        var flag = FlagPoints[0];
        var castle = (Component)Prop(Setup, "CastleStructure");
        var spawn = Type("IslandEnemy").GetMethod("Spawn");
        var castleCenter = (Vector2)Type("IslandLayout").GetProperty("CastleCenter").GetValue(null);
        for (int i = 0; i < 3; i++)
        {
            var at = flag + new Vector2(2.6f + i * 0.7f, 0.7f - i * 0.6f);
            spawn.Invoke(null, new object[] { Enum.Parse(Type("EnemyType"), i == 1 ? "ShieldLancer" : "Raider"), 6, castle.transform, at, new[] { at, castleCenter }, 0, false, false });
        }
        // Savaş görünümü kilitliyken yakın kare: kamera Lancer'lara
        yield return new WaitForSecondsRealtime(0.2f);
        SetCamera(flag + new Vector2(0.6f, 0.4f), 4.6f);
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("E1_14_battle_lancers_hold_the_flag");
        yield return new WaitForSecondsRealtime(0.7f);
        Capture("E1_15_battle_lancers_fighting");
        // Manastır şifası: hasarlı bina + şifa efekti
        var monCenter = WorldOf(mon.col, mon.row, 3, 3);
        foreach (var b in ((System.Collections.IEnumerable)Type("IslandBuilding").GetProperty("All").GetValue(null)).Cast<Component>())
        {
            var center = (Vector2)Prop(b, "Center");
            if ((bool)Prop(b, "IsCastle") || Vector2.Distance(center, monCenter) > 3.5f) continue;
            var structure = (Component)Prop(b, "Structure");
            structure.GetType().GetMethod("TakeDamage").Invoke(structure, new object[] { (float)Prop(structure, "MaxHealth") * 0.5f });
        }
        var monastery = UnityEngine.Object.FindFirstObjectByType(Type("IslandMonastery"));
        SetCamera(monCenter + new Vector2(0.4f, -0.2f), 4.2f);
        yield return new WaitForSecondsRealtime(0.4f);
        monastery.GetType().GetMethod("Pulse").Invoke(monastery, null);
        yield return new WaitForSecondsRealtime(0.35f);
        Capture("E1_16_monastery_heal");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }
}
