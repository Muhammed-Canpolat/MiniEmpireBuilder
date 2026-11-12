using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz B ekran görüntüleri (island_base_v2 §10): yerleştirme hayaleti (yeşil/kırmızı + şerit kaplaması), İNŞA ET menüsü,
/// yükseltme paneli, dolu kaynak yığınları, bölge keşfi (bulut dağılması ortası) ve gelişmiş ada. Çıktı <c>PlaytestOutput/island/B*.png</c>.
/// </summary>
public partial class IslandReviewScreenshots
{
    private static object Hud => Prop(Setup, "Hud");
    private static object Village => Prop(Setup, "Village");
    private static object BuildUi => Prop(Hud, "BuildUi");

    private static object CurrentSave()
    {
        var gm = Type("GameManager").GetProperty("Instance").GetValue(null);
        return IslandPhaseBTests.Field(Prop(gm, "PlayerData"), "island");
    }

    private static void AimAt(Vector2 center, float size)
    {
        SetCamera(center, size);
    }

    private static Vector2 WorldOf(int col, int row, int w, int h) => new Vector2(col + w * 0.5f, 36 - (row + h * 0.5f));

    [UnityTest]
    public IEnumerator CapturePhaseBPlacementAndMenus()
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "wood", 900); IslandPhaseBTests.Set(island, "meat", 200);
        }, 5, v => devWas = v);
        var island0 = CurrentSave();
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        View("SetDefaultView");
        yield return new WaitForSecondsRealtime(0.5f);
        Capture("B00_dock_default");

        // İNŞA ET menüsü
        Hud.GetType().GetMethod("OpenBuildMenu").Invoke(Hud, null);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("B03_build_menu");
        BuildUi.GetType().GetMethod("Close").Invoke(BuildUi, null);

        // Hayalet: geçerli (yeşil) hücre, kale yakını
        Hud.GetType().GetMethod("StartPlacement").Invoke(Hud, new[] { IslandPhaseBTests.Call("IslandPlacementHost", "ForNew", IslandPhaseBTests.Kind("House")) });
        var controller = (Component)UnityEngine.Object.FindFirstObjectByType(Type("IslandPlacementController"));
        Assert.IsTrue(IslandPhaseBTests.FindCellNear("House", island0, 1, 10, 8, out int gc, out int gr, 5));
        controller.GetType().GetMethod("MoveGhostTo").Invoke(controller, new object[] { gc, gr });
        yield return null;
        var center = WorldOf(gc, gr, 2, 2);
        AimAt(center + new Vector2(0f, -1.5f), camera.orthographicSize * 0.62f);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("B01_ghost_green");

        // Kırmızı: yürüyüş şeridi hücresi (şerit kaplaması görünür)
        Assert.IsTrue(FindLaneNear(island0, gc, gr, out int lc, out int lr));
        controller.GetType().GetMethod("MoveGhostTo").Invoke(controller, new object[] { lc, lr });
        yield return null;
        AimAt(WorldOf(lc, lr, 2, 2) + new Vector2(0f, -1.5f), camera.orthographicSize);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("B02_ghost_red");
        controller.GetType().GetMethod("Cancel").Invoke(controller, null);

        // Yükseltme paneli: Ağıl
        var pasture = IslandPhaseBTests.FindEntry(island0, "Pasture");
        View("SetDefaultView");
        BuildUi.GetType().GetMethod("ShowBuilding").Invoke(BuildUi, new object[] { (string)IslandPhaseBTests.Field(pasture, "id") });
        Hud.GetType().GetMethod("FocusBuilding").Invoke(Hud, new object[] { (string)IslandPhaseBTests.Field(pasture, "id") });
        yield return new WaitForSecondsRealtime(0.7f);
        Capture("B04_upgrade_panel");
        var lumber = IslandPhaseBTests.FindEntry(island0, "Lumber");
        BuildUi.GetType().GetMethod("ShowBuilding").Invoke(BuildUi, new object[] { (string)IslandPhaseBTests.Field(lumber, "id") });
        yield return new WaitForSecondsRealtime(0.3f);
        Capture("B04b_upgrade_panel_lumber");
    }

    /// <summary>Yeşil hücreye yakın bir yürüyüş şeridi hücresi (kırmızı hayalet için).</summary>
    private static bool FindLaneNear(object island, int nearCol, int nearRow, out int col, out int row)
    {
        float best = float.MaxValue; col = row = -1;
        for (int c = 0; c < 21; c++)
            for (int r = 0; r < 35; r++)
            {
                if (IslandPhaseBTests.StaticReason(c, r) != "Lane" || IslandPhaseBTests.StaticReason(c + 1, r + 1) != "Lane") continue;
                if (IslandPhaseBTests.Check("House", c, r, island, 1, null, 5) != "Lane") continue;
                float d = Vector2.Distance(new Vector2(c, r), new Vector2(nearCol, nearRow));
                if (d < best) { best = d; col = c; row = r; }
            }
        return col >= 0;
    }

    [UnityTest]
    public IEnumerator CapturePhaseBPilesAndZoneReveal()
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            IslandPhaseBTests.Set(island, "wood", 900); IslandPhaseBTests.Set(island, "meat", 200);
        }, 5, v => devWas = v);
        var island0 = CurrentSave();
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;

        // Dolu yığınlar: depo tavanında Oduncu ve Ağıl
        foreach (var kind in new[] { "Lumber", "Pasture" })
        {
            var entry = IslandPhaseBTests.FindEntry(island0, kind);
            IslandPhaseBTests.Set(entry, "stock", (float)IslandPhaseBTests.Call("IslandEconomy", "Capacity", entry));
            IslandPhaseBTests.Set(entry, "lastTick", Type("IslandRepair").GetProperty("Now").GetValue(null));
        }
        Village.GetType().GetMethod("RefreshPiles").Invoke(Village, new object[] { true });
        AimAt(new Vector2(10.6f, 20.5f), 8.8f);
        yield return new WaitForSecondsRealtime(0.6f);
        Capture("B05_piles_full");

        // Bölge keşfi (Z1): görev başlar, süre dolar, bulutlar dağılırken kare alınır
        View("SetDefaultView");
        var args = new object[] { 1, false, null };
        Assert.IsTrue((bool)Type("IslandEconomy").GetMethod("TryStartZone").Invoke(null, args), (string)args[2]);
        yield return new WaitForSecondsRealtime(0.5f);
        AimAt(new Vector2(15.5f, 22f), 10.5f);
        yield return new WaitForSecondsRealtime(0.4f);
        Capture("B06a_zone_exploring");
        long done = DateTime.UtcNow.Ticks + 40 * TimeSpan.TicksPerSecond;
        IslandPhaseBTests.SetClock(done);
        Type("IslandEconomy").GetMethod("Tick").Invoke(null, new object[] { done, true });
        yield return new WaitForSecondsRealtime(0.45f);   // 1,5 sn'lik dağılmanın ortası
        Capture("B06_zone_reveal_mid");
        yield return new WaitForSecondsRealtime(1.5f);
        Capture("B06b_zone_revealed");
    }

    [UnityTest]
    public IEnumerator CaptureDevelopedIsland()
    {
        yield return IslandPhaseBTests.LoadIsland(island =>
        {
            const int keep = 8;
            IslandPhaseBTests.Set(island, "wood", 1200); IslandPhaseBTests.Set(island, "meat", 260);
            IslandPhaseBTests.Set(island, "zoneMask", 7);
            IslandPhaseBTests.Set(island, "highestAkin", 9);
            IslandPhaseBTests.Call("IslandEconomy", "EnsureSeeded", island, keep);
            var placed = IslandPhaseBTests.Placed(island);
            foreach (var entry in placed)
            {
                string kind = (string)IslandPhaseBTests.Field(entry, "kind");
                if (kind == "Lumber") IslandPhaseBTests.Set(entry, "level", 7);
                if (kind == "Pasture") IslandPhaseBTests.Set(entry, "level", 6);
                if (kind == "House") IslandPhaseBTests.Set(entry, "level", 4);
            }
            // Ek binalar: ikinci/üçüncü Ev, anakara kulesi (KK8: Ev 3, Kule 4)
            int serial = 1;
            foreach (var spec in new[] { ("House", 3, 11, 8), ("House", 2, 8, 12), ("Tower", 4, 15, 12) })
            {
                if (!IslandPhaseBTests.FindCellNear(spec.Item1, island, 7, spec.Item3, spec.Item4, out int c, out int r, keep)) continue;
                var entry = IslandPhaseBTests.Entry($"{spec.Item1}#{serial++}", spec.Item1, spec.Item2, c, r);
                IslandPhaseBTests.Set(entry, "lastTick", DateTime.UtcNow.Ticks);
                placed.Add(entry);
            }
            IslandPhaseBTests.Set(island, "nextBuildingSerial", serial);
            foreach (var entry in placed)
            {
                string kind = (string)IslandPhaseBTests.Field(entry, "kind");
                if (kind == "Lumber" || kind == "Pasture") IslandPhaseBTests.Set(entry, "stock", (float)IslandPhaseBTests.Call("IslandEconomy", "Capacity", entry) * (kind == "Lumber" ? 0.55f : 0.8f));
            }
        }, 8, v => devWas = v);
        var camera = (Camera)Prop(Setup, "WorldCamera");
        camera.aspect = (float)W / H;
        View("SetDefaultView");
        yield return new WaitForSecondsRealtime(1.2f);
        Capture("B07_developed_default");
        View("SetFullView");
        yield return new WaitForSecondsRealtime(0.8f);
        Capture("B08_developed_full");
        AimAt(new Vector2(10.5f, 20f), 9.5f);
        yield return new WaitForSecondsRealtime(0.6f);
        Capture("B09_developed_village");
    }
}
