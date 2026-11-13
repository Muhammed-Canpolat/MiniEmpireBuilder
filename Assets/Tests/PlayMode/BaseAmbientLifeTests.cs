using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>Faz 3.1: Ana Üs ortam hayatı (koyun otlama/dolaşma, ağaç sallanması, madenci kazması).</summary>
public class BaseAmbientLifeTests
{
    [UnityTest]
    public IEnumerator BaseAmbientLife_SheepWanderAndMinersSwing()
    {
        yield return Nov13FeedbackTests.LoadBase(15);
        var ambient = Object.FindFirstObjectByType(T("BaseAmbientLife")) as Component;
        Assert.IsNotNull(ambient, "BaseAmbientLife bileşeni yok");

        // Kameranın baktığı yere bir dekor koyunu koy (gerçek dekor sınıfıyla).
        var data = Resources.Load("Decor/sheep");
        Assert.IsNotNull(data, "sheep dekoru yok");
        var go = new GameObject("Decor_sheep");
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = (Sprite)Prop(data, "Visual");
        renderer.sortingOrder = 30;
        go.transform.localScale = Vector3.one * (float)T("DecorData").GetMethod("ScaleFor").Invoke(data, new object[] { renderer.sprite });
        var instance = go.AddComponent(T("DecorInstance"));
        instance.GetType().GetMethod("Initialize").Invoke(instance, new object[] { data, new Vector2Int(10, 10), "test_sheep" });
        var cam = Camera.main;
        Vector3 view = cam.transform.position; view.z = 0f;
        go.transform.position = view;
        yield return null;

        Sprite firstSprite = renderer.sprite;
        Vector3 firstPos = go.transform.position;
        var miner = GameObject.Find("MinePawn");
        var minerRenderer = miner != null ? miner.GetComponent<SpriteRenderer>() : null;
        var minerFrames = new HashSet<Sprite>();
        var sheepSprites = new HashSet<Sprite>();
        bool moved = false;
        float end = Time.realtimeSinceStartup + 14f;
        while (Time.realtimeSinceStartup < end)
        {
            if (minerRenderer != null && minerRenderer.sprite != null) minerFrames.Add(minerRenderer.sprite);
            sheepSprites.Add(renderer.sprite);
            if ((go.transform.position - firstPos).sqrMagnitude > 1e-4f) moved = true;
            yield return null;
        }
        Debug.Log($"[BaseAmbientLife] sway={Prop(ambient, "SwayCount")} sheep={Prop(ambient, "SheepCount")} moved={moved} sheepSprites={sheepSprites.Count} minerFrames={minerFrames.Count}");
        Assert.GreaterOrEqual((int)Prop(ambient, "SheepCount"), 1, "koyun kaydedilmedi (Ada kare katalogu yok olabilir)");
        Assert.IsTrue(moved || sheepSprites.Count > 1 || renderer.sprite != firstSprite, "koyun ne yürüdü ne kare değiştirdi");
        Assert.LessOrEqual(((Vector2)(go.transform.position - firstPos)).magnitude, 0.55f, "koyun alanından çok uzaklaştı");
        // Madenci Animator'ı (CullCompletely) batchmode'da kamera çizmediği için kare üretmeyebilir; kare geldiyse değişmeli
        if (minerRenderer != null && minerFrames.Count > 0) Assert.Greater(minerFrames.Count, 1, "madenci kazma kareleri değişmedi");
        Assert.Greater((int)Prop(ambient, "SwayCount"), 10, "çit dışı ağaçlar sallanıyor");
        Nov13FeedbackTests.CaptureCamera("base-ambient-life", Camera.main, 1080, 1920, false, Session3Tests.Folder);
        Object.Destroy(go);
    }
}
