using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Grup 6 (D118): bug 1 (taşıma hayaleti ayak izine yakın), 23 (koyun yürünebilir hücrede), 17 (bilgi kutusunda gövde/maliyet çakışmaz),
/// 20 (ada ayarlar düğmesi paneli açar), 21 (yükseltmede kalan süre etiketi). Ekran görüntüleri Docs/playtest/bugs-g6/.
/// </summary>
public class Session12DrawIslandUiTests
{
    private bool devWas;
    private static Type T(string n) => IslandPhaseBTests.T(n);
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    [TearDown]
    public void Cleanup()
    {
        Time.timeScale = 1f;
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
    }

    [Test]
    public void GhostIsLimitedNearTheFootprint()
    {
        var fit = T("PlacementController").GetMethod("FitGhostScale", Any);
        object[] a = { new Vector2(1f, 1f), new Vector3(1f, 1f, 1f), new Vector2Int(2, 2), null };
        Assert.IsTrue((bool)fit.Invoke(null, a));
        Assert.AreEqual(new Vector3(1f, 1f, 1f), (Vector3)a[3], "küçük hayalet değişmez");
        a = new object[] { new Vector2(1f, 1f), new Vector3(4f, 4f, 1f), new Vector2Int(2, 2), null };
        Assert.IsTrue((bool)fit.Invoke(null, a));
        Assert.LessOrEqual(((Vector3)a[3]).x, 3.01f, "ayak izinin 1,5 katına sınırlanır");
        a = new object[] { new Vector2(1f, 1f), new Vector3(60f, 60f, 1f), new Vector2Int(2, 2), null };
        Assert.IsFalse((bool)fit.Invoke(null, a), "ekranı kaplayan hayalet: yalnız ayak izi karesi");
    }

    [Test]
    public void InfoPanelBodyAndCostRowsDoNotOverlap()
    {
        var canvasObject = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390, 844);
        try
        {
            var type = T("FancyInfoPanel");
            var view = type.GetMethod("Build").Invoke(null, new object[] { canvasObject.transform, "Info", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.40f) });
            Canvas.ForceUpdateCanvases();
            Rect R(string f) => WorldRect(((Component)view.GetType().GetField(f).GetValue(view)).transform as RectTransform);
            var body = R("body");
            foreach (var f in new[] { "costA", "costB", "time" })
                Assert.GreaterOrEqual(body.yMin + 0.5f, R(f).yMax, $"gövde alt sınırı {f} satırının üstünde");
            Assert.LessOrEqual(R("note").yMax, R("workers").yMin + 0.5f, "not ve işçi satırı ayrı bantlarda");
            Assert.LessOrEqual(R("costB").xMax, R("time").xMin + 0.5f, "maliyet ve süre yatayda çakışmaz");
        }
        finally { UnityEngine.Object.DestroyImmediate(canvasObject); }
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    [UnityTest]
    public IEnumerator IslandSettingsButtonOpensTimeLabelShownAndSheepWalkable()
    {
        yield return IslandPhaseBTests.LoadIsland(null, 5, v => devWas = v);
        try
        {
            // 23: tüm koyunlar yürünebilir hücrede
            var sheepType = T("IslandSheep");
            var spotOk = sheepType.GetMethod("SpotOk");
            var heightOf = sheepType.GetMethod("HeightOf");
            foreach (var sheep in (System.Collections.IEnumerable)sheepType.GetField("All").GetValue(null))
            {
                Vector2 feet = ((Component)sheep).transform.position;
                Assert.IsTrue((bool)spotOk.Invoke(null, new object[] { feet, heightOf.Invoke(null, new object[] { feet }) }), "koyun yürünebilir hücrede doğar");
            }

            // 20: ada ayarlar düğmesi paneli açar
            var settings = UnityEngine.Object.FindFirstObjectByType(T("AudioSettingsUI"));
            Assert.IsNotNull(settings, "ada sahnesinde AudioSettingsUI var");
            yield return null;
            settings.GetType().GetMethod("Open").Invoke(settings, null);
            yield return null;
            Assert.IsTrue((bool)T("AudioSettingsUI").GetProperty("IsSettingsOpen").GetValue(null), "ada üssünde ayarlar açılır");
            // Kapanış gerçek zamanlı biter (timeScale 0 olsa bile): panel ve engelleyici kapanır, dokunuşu engellemez.
            float scaleWas = Time.timeScale;
            Time.timeScale = 0f;
            T("AudioSettingsUI").GetMethod("CloseIfOpen").Invoke(null, null);
            yield return new WaitForSecondsRealtime(0.5f);
            Time.timeScale = scaleWas;
            Assert.IsFalse((bool)T("AudioSettingsUI").GetProperty("IsSettingsOpen").GetValue(null));
            var panelGo = (GameObject)settings.GetType().GetField("panel", Any).GetValue(settings);
            var blockerGo = (GameObject)settings.GetType().GetField("blocker", Any).GetValue(settings);
            Assert.IsFalse(panelGo.activeInHierarchy, "ayarlar kapanınca panel görünmez");
            Assert.IsTrue(blockerGo == null || !blockerGo.activeInHierarchy, "ayarlar kapanınca dokunuş engellenmez");

            // 21: kale yükseltilirken kalan süre etiketi görünür
            long clock = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc).Ticks;
            IslandPhaseBTests.SetClock(clock);
            var island = IslandPhaseBTests.CurrentIsland();
            var task = IslandPhaseBTests.Field(island, "task");
            IslandPhaseBTests.Set(task, "kind", "upgrade");
            IslandPhaseBTests.Set(task, "buildingId", "Castle");
            IslandPhaseBTests.Set(task, "targetLevel", 6);
            IslandPhaseBTests.Set(task, "startTicks", clock);
            IslandPhaseBTests.Set(task, "endTicks", clock + 600L * TimeSpan.TicksPerSecond);
            var hud = IslandPhaseBTests.Prop(IslandPhaseBTests.Setup, "Hud");
            var ui = IslandPhaseBTests.Prop(hud, "BuildUi");
            ui.GetType().GetMethod("ShowCastle").Invoke(ui, null);
            yield return null;
            var time = (Component)ui.GetType().GetField("panelTime", Any).GetValue(ui);
            Assert.IsTrue(time.gameObject.activeInHierarchy, "yükseltme sürerken süre etiketi görünür");
            string expected = T("Progression").GetMethod("FormatDuration").Invoke(null, new object[] { 600d }).ToString();
            StringAssert.Contains(expected, time.GetType().GetProperty("text").GetValue(time).ToString());
            Capture("island_castle_upgrade_time");

            // 17: ada inşa menüsü kartları
            ui.GetType().GetMethod("Close").Invoke(ui, null);
            hud.GetType().GetMethod("OpenBuildMenu").Invoke(hud, null);
            yield return null;
            yield return null;
            Capture("island_build_cards");
        }
        finally
        {
            T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        }
    }

    private static void Capture(string name)
    {
        const int W = 1080, H = 1920;
        var camera = (Camera)IslandPhaseBTests.Prop(IslandPhaseBTests.Setup, "WorldCamera");
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/playtest/bugs-g6"));
        Directory.CreateDirectory(folder);
        var texture = new RenderTexture(W, H, 24);
        camera.targetTexture = texture;
        camera.aspect = (float)W / H;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(W, H, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        UnityEngine.Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        texture.Release();
        UnityEngine.Object.Destroy(texture);
    }
}
