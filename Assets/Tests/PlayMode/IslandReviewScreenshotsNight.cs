using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Gece çalışması: ada müziği kategorisi (sakin ↔ savaş) ve sabit ada kamerası (4 en-boy oranında tüm ada görünür,
/// kaydırma/zum kamerayı oynatmaz). Ekran görüntüleri <c>PlaytestOutput/island/N*.png</c>.
/// </summary>
public partial class IslandReviewScreenshots
{
    private static readonly float[] NightAspects = { 9f / 16f, 9f / 19.5f, 9f / 20f, 3f / 4f };

    [UnityTest]
    public IEnumerator IslandMusicIsCalmUntilRaidRunsThenReturnsToCalm()
    {
        var audioType = Type("AudioManager");
        var existing = (Component)UnityEngine.Object.FindFirstObjectByType(audioType);
        GameObject created = null;
        if (existing == null) { created = new GameObject("TestAudioManager"); created.AddComponent(audioType); }
        yield return null;
        yield return LoadIsland(keep: 5, highest: 0);
        var audio = audioType.GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(audio);
        string Category() => (string)audioType.GetProperty("MusicCategory").GetValue(audio);
        Assert.AreEqual("island-calm", Category(), "adada sakinken sakin müzik");

        var raid = Prop(Setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 });
        yield return null;
        Assert.AreEqual("island-battle", Category(), "akın başlayınca savaş müziği");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
        yield return null;
        Assert.AreEqual("island-calm", Category(), "akın bitince sakin müzik");
        if (created != null) UnityEngine.Object.Destroy(created);
    }

    [UnityTest]
    public IEnumerator FixedIslandCameraShowsWholeIslandAtAllAspectsAndIgnoresDrag()
    {
        yield return LoadIsland(keep: 5, highest: 0);
        var camera = (Camera)Prop(Setup, "WorldCamera");
        var controller = Prop(Setup, "CameraController");
        var controllerType = controller.GetType();
        Assert.IsTrue((bool)controllerType.GetProperty("FixedView").GetValue(null), "sabit kamera bayrağı açık");
        foreach (float aspect in NightAspects)
        {
            camera.aspect = aspect;
            yield return null;   // IslandSceneSetup.Update en-boy değişimini yakalar
            yield return null;
            // Kadraj kara sınırına (adacıklar dahil) oturur: kara dikdörtgeninin dört köşesi görünmeli
            var land = (Rect)controllerType.GetMethod("LandBounds").Invoke(null, null);
            Assert.Greater(land.width, 15f, "kara sınırı makul");
            foreach (var corner in new[] { new Vector3(land.xMin, land.yMin), new Vector3(land.xMax, land.yMin), new Vector3(land.xMin, land.yMax), new Vector3(land.xMax, land.yMax) })
            {
                var v = camera.WorldToViewportPoint(corner);
                Assert.That(v.x, Is.InRange(-0.001f, 1.001f), $"aspect {aspect:0.000} köşe {corner} x={v.x}");
                Assert.That(v.y, Is.InRange(-0.001f, 1.001f), $"aspect {aspect:0.000} köşe {corner} y={v.y}");
            }
            Vector3 pos = camera.transform.position; float size = camera.orthographicSize;
            // Sürükleme/zum/kenar kaydırma/kayma denemesi: kamera yerinde kalır
            controllerType.GetMethod("ZoomBy").Invoke(controller, new object[] { 0.5f, new Vector2(100f, 100f) });
            controllerType.GetMethod("EdgeScroll").Invoke(controller, new object[] { new Vector2(1f, 1f) });
            camera.transform.position += new Vector3(5f, -4f, 0f);   // sürükleme kayması
            camera.orthographicSize *= 0.6f;
            yield return null;
            Assert.Less(Vector3.Distance(pos, camera.transform.position), 0.01f, "kamera konumu sabit");
            Assert.AreEqual(size, camera.orthographicSize, 0.01f, "kamera boyu sabit");
        }
    }

    [UnityTest]
    public IEnumerator CaptureFixedIslandViewsPortraitAndTall()
    {
        yield return LoadIsland(keep: 5, highest: 0);
        var camera = (Camera)Prop(Setup, "WorldCamera");
        CaptureSized("N01_fixed_9x16", 1080, 1920, camera);
        CaptureSized("N02_fixed_9x20", 1080, 2400, camera);
    }

    [UnityTest]
    public IEnumerator CloudTransitionCoversLoadsBaseAndDestroysItself()
    {
        yield return LoadIsland(keep: 5, highest: 0);
        var transitionType = Type("SceneCloudTransition");
        int before = (int)transitionType.GetProperty("CompletedCount").GetValue(null);
        var gm = Type("GameManager").GetProperty("Instance").GetValue(null);
        gm.GetType().GetMethod("LoadBaseWorld").Invoke(gm, null);
        yield return null;
        yield return null;
        Assert.IsNotNull(GameObject.Find("SceneCloudTransition"), "bulut örtüsü oluştu");
        float deadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < deadline && (int)transitionType.GetProperty("CompletedCount").GetValue(null) == before)
            yield return null;
        Assert.AreEqual(before + 1, (int)transitionType.GetProperty("CompletedCount").GetValue(null), "geçiş tamamlandı");
        yield return null;
        Assert.IsNull(GameObject.Find("SceneCloudTransition"), "örtü kendini yok etti");
        Assert.AreEqual("BaseScene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private static void CaptureSized(string name, int w, int h, Camera camera)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/island"));
        Directory.CreateDirectory(folder);
        var texture = new RenderTexture(w, h, 24);
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        View("SetDefaultView");
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
        var pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        UnityEngine.Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        texture.Release();
        UnityEngine.Object.Destroy(texture);
    }
}
