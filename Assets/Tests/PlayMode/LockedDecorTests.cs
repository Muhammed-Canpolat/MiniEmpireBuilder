using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// D123: kilitli arazideki rastgele doğa engelleri (ağaç/çalı/kaya/kütük) birbirine binmez, hücre merkezine oturur ve
/// yerleştirilen dekorla aynı sprite/ölçeği kullanır (kaya/kütük artık kırpılmış sprite ile büyütülmez).
/// </summary>
[Timeout(120000)]
public class LockedDecorTests
{
    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");

    [UnityTest]
    public IEnumerator LockedDecor_DoesNotOverlap_AndMatchesPlacedDecorScale()
    {
        SceneManager.LoadScene("BaseScene");
        yield return new WaitForSecondsRealtime(2.5f);
        var natureType = T("NatureObstacle");
        var kindField = natureType.GetField("kind");
        var cellProp = natureType.GetProperty("cell");
        var radius = T("BaseSceneSetup").GetMethod("LockedDecorRadius");
        var decorScale = T("DecorScale");
        var find = decorScale.GetMethod("Find");
        var forMethod = decorScale.GetMethod("For");

        var items = new List<(Vector2 pos, float r, string kind)>();
        foreach (var c in Object.FindObjectsByType(natureType, FindObjectsSortMode.None))
        {
            var n = (Component)c;
            if (!n.name.StartsWith("LockedObstacle_")) continue;
            var kind = kindField.GetValue(n);
            var cell = (Vector2Int)cellProp.GetValue(n);
            float r = (float)radius.Invoke(null, new[] { kind });
            Assert.AreEqual(cell.x + 0.5f, n.transform.position.x, 0.001f, n.name + " hücre merkezinde değil");
            var sr = n.GetComponent<SpriteRenderer>();
            string k = kind.ToString();
            string baseId = k == "Tree" ? "tree" : k == "Bush" ? "bush" : k == "Rock" ? "rock" : "stump";
            var data = find.Invoke(null, new object[] { baseId });
            Assert.IsNotNull(data, baseId + " dekor verisi yok");
            float expected = (float)forMethod.Invoke(null, new object[] { data, sr.sprite });
            float scale = n.transform.localScale.x;
            Assert.LessOrEqual(Mathf.Abs(scale / expected - 1f), 0.081f, $"{n.name} ölçeği dekordan farklı ({scale} / {expected})");
            if (k == "Rock" || k == "Stump")
                Assert.LessOrEqual(sr.bounds.size.x * VisibleFraction(k), 1.25f, n.name + " görünen genişliği 1 hücreden büyük");
            items.Add((new Vector2(cell.x + 0.5f, cell.y + 0.5f), r, k));
        }
        Assert.Greater(items.Count, 20, "Kilitli arazide doğa engeli oluşmadı");
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
        {
            float min = items[i].r + items[j].r;
            Assert.GreaterOrEqual((items[i].pos - items[j].pos).magnitude + 1e-4f, min,
                $"{items[i].kind}@{items[i].pos} ile {items[j].kind}@{items[j].pos} üst üste");
        }
        Debug.Log($"[LockedDecorTest] {items.Count} engel, çakışma yok");
        CardWorkshopTests.CaptureScreen("base_locked_decor", "intro");
        CaptureWholeMap("base_locked_decor_map");
    }

    /// <summary>Tüm Ana Üs haritası (yalnız dünya, UI yok) — kilitli arazi doğa dokusunu görmek için.</summary>
    private static void CaptureWholeMap(string name)
    {
        var cam = Camera.main;
        var oldPos = cam.transform.position; float oldSize = cam.orthographicSize;
        const int W = 900, H = 1600;
        var layout = T("BaseWorldLayout");
        float w = Convert.ToSingle(layout.GetProperty("Width")?.GetValue(null) ?? layout.GetField("Width").GetValue(null));
        float h = Convert.ToSingle(layout.GetProperty("Height")?.GetValue(null) ?? layout.GetField("Height").GetValue(null));
        cam.transform.position = new Vector3(w * 0.5f, h * 0.5f, oldPos.z);
        cam.orthographicSize = Mathf.Max(h * 0.5f + 2f, (w * 0.5f + 2f) * H / W);
        var rt = new RenderTexture(W, H, 24);
        cam.targetTexture = rt; cam.aspect = (float)W / H;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../PlaytestOutput/intro"));
        System.IO.Directory.CreateDirectory(folder);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; cam.ResetAspect();
        cam.transform.position = oldPos; cam.orthographicSize = oldSize;
        rt.Release(); Object.Destroy(rt); Object.Destroy(tex);
    }

    /// <summary>Tiny Swords karesinde görünen gövdenin kare genişliğine oranı (en geniş kullanılan: Rock3 39/64, Stump 1 48/192).</summary>
    private static float VisibleFraction(string kind) => kind == "Rock" ? 39f / 64f : 48f / 192f;
}
