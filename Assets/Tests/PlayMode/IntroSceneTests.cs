using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Açılış intro sahnesi: Intro → MainMenuScene geçişi, çubuk %0→%100, ipucu döngüsü, Türkçe font, intro'nun bellekten çıkması
/// ve 9:16 / 9:21 / 4:3 / Safe Area ekran görüntüleri (PlaytestOutput/intro/). Gerçek kayıt saklanıp geri konur.
/// </summary>
[Timeout(120000)]
public class IntroSceneTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private string savedKey, savedBackup, savedFile;
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");

    [SetUp]
    public void Keep()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
    }

    [TearDown]
    public void Restore()
    {
        if (savedKey != null) PlayerPrefs.SetString(Key, savedKey); else PlayerPrefs.DeleteKey(Key);
        if (savedBackup != null) PlayerPrefs.SetString(BackupKey, savedBackup); else PlayerPrefs.DeleteKey(BackupKey);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile); else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
    }

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");

    [UnityTest]
    public IEnumerator Intro_FillsBar_RotatesTips_ThenOpensGame()
    {
        StringAssert.EndsWith("Intro.unity", SceneUtility.GetScenePathByBuildIndex(0), "Build index 0 Intro olmalı");
        StringAssert.EndsWith("MainMenuScene.unity", SceneUtility.GetScenePathByBuildIndex(1), "Eski ilk sahne index 1 olmalı");
        StringWriterLog log = new StringWriterLog();
        Application.logMessageReceived += log.On;
        try
        {
            SceneManager.LoadScene("Intro");
            yield return null;
            yield return null;
            Assert.AreEqual("Intro", SceneManager.GetActiveScene().name);
            Assert.IsNull(Object.FindFirstObjectByType(T("IntroSceneSetup")), "Eski giriş (IntroSceneSetup) intro'ya eklenmemeli");
            Assert.IsNull(GameObject.Find("Logo"), "Ayrı logo katmanı olmamalı (logo arka planda)");

            var barType = T("MiniEmpire.Intro.LoadingBar");
            var bar = Object.FindFirstObjectByType(barType) as Component;
            Assert.IsNotNull(bar, "LoadingBar yok");
            var progress = barType.GetProperty("DisplayedProgress");
            var fill = bar.transform.Find("Fill").GetComponent<Image>();
            var percent = new Txt(bar.transform.Find("PercentText"));
            var tip = new Txt(GameObject.Find("TipText").transform);
            string firstTip = tip.text;
            Assert.AreEqual("Ordunu güçlendir, imparatorluğunu büyüt!", firstTip);

            // Font: projenin dinamik Türkçe fontu (varsayılan LiberationSans değil) ve Türkçe glifler
            Assert.IsFalse(tip.UsesDefaultFont, "İpucu varsayılan (Türkçesiz) fontta kaldı");
            Assert.IsTrue(tip.FontHas("çğıöşüİÇĞÖŞÜ%0123456789"), "Türkçe glif eksik");
            Assert.IsFalse(percent.UsesDefaultFont, "Yüzde yazısı varsayılan fontta kaldı");

            float Prog() => (float)progress.GetValue(bar);
            float last = 0f;
            float t0 = Time.realtimeSinceStartup;
            while (Prog() < 0.74f && Time.realtimeSinceStartup - t0 < 15f)
            {
                Assert.GreaterOrEqual(Prog() + 1e-4f, last, "Çubuk geri gitmemeli");
                last = Prog();
                yield return null;
            }
            Assert.GreaterOrEqual(Prog(), 0.74f, "Çubuk ilerlemedi");
            Assert.AreEqual("%" + Mathf.RoundToInt(Prog() * 100f), percent.text);
            Assert.AreEqual(Prog(), fill.fillAmount, 0.001f);

            Capture("intro_9x16", 941, 1672, false);
            Capture("intro_9x21", 720, 1680, false);
            Capture("intro_4x3", 1536, 2048, false);
            Capture("intro_safe_notch", 780, 1688, true);

            bool tipChanged = false, sawFull = false;
            t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name == "Intro" && Time.realtimeSinceStartup - t0 < 20f)
            {
                if (tip.Alive && tip.text != firstTip) tipChanged = true;
                if (bar != null && Prog() >= 0.999f) { sawFull = true; Assert.AreEqual("%100", percent.text); }
                yield return null;
            }
            Assert.AreEqual("MainMenuScene", SceneManager.GetActiveScene().name, "Intro bitince oyun açılmalı");
            Assert.IsTrue(sawFull, "Çubuk %100'e ulaşmadı");
            Assert.IsTrue(tipChanged, "İpucu değişmedi");
            yield return null;
            Assert.IsFalse(SceneManager.GetSceneByName("Intro").isLoaded, "Intro sahnesi bellekten çıkmalı");
            Assert.IsNull(Object.FindFirstObjectByType(T("MiniEmpire.Intro.IntroSceneController")), "Intro nesnesi kalmamalı");
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[IntroTest] " + log.Activation);

            // "Başa dönme" (GameManager.LoadMainMenu) intro'ya değil eski ilk sahneye gider
            var gmType = T("GameManager");
            var gm = gmType.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(gm, "GameManager yok");
            yield return new WaitForSecondsRealtime(1f);
            gmType.GetMethod("LoadMainMenu").Invoke(gm, null);
            string seen = "";
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 6f)
            {
                string n = SceneManager.GetActiveScene().name;
                if (n == "Intro") seen = n;
                yield return null;
            }
            Assert.AreEqual("", seen, "Başa dönme intro'ya düşmemeli");
            Assert.AreEqual("MainMenuScene", SceneManager.GetActiveScene().name);
        }
        finally { Application.logMessageReceived -= log.On; }
    }

    /// <summary>TextMeshProUGUI'ye yansıma ile erişim (test derlemesi TMP'ye bağlı değil).</summary>
    private sealed class Txt
    {
        private readonly Component c;
        public Txt(Transform t)
        {
            foreach (var comp in t.GetComponents<Component>()) if (comp.GetType().Name == "TextMeshProUGUI") c = comp;
            Assert.IsNotNull(c, t.name + " TMP yok");
        }
        public bool Alive => c != null;
        public string text => (string)c.GetType().GetProperty("text").GetValue(c);
        private object Font => c.GetType().GetProperty("font").GetValue(c);
        public bool UsesDefaultFont
        {
            get
            {
                var settings = c.GetType().Assembly.GetType("TMPro.TMP_Settings");
                var def = settings.GetProperty("defaultFontAsset").GetValue(null);
                return ReferenceEquals(def, Font) || (Font as Object) == null;
            }
        }
        public bool FontHas(string chars)
        {
            var f = Font;
            var m = f.GetType().GetMethod("HasCharacters", new[] { typeof(string) });
            return (bool)m.Invoke(f, new object[] { chars });
        }
    }

    private sealed class StringWriterLog
    {
        public string Activation = "etkinleşme günlüğü yok";
        public void On(string msg, string stack, LogType type) { if (msg.StartsWith("[Intro]") && msg.Contains("etkinleşme")) Activation = msg; }
    }

    /// <summary>Overlay tuvali geçici olarak kameraya bağlayıp W×H görüntü yazar. notch: iPhone çentik/ev çubuğu Safe Area benzetimi.</summary>
    private static void Capture(string name, int w, int h, bool notch)
    {
        var camera = Camera.main;
        var canvas = GameObject.Find("IntroCanvas").GetComponent<Canvas>();
        // Yalnız intro tuvali (DevMenu gibi geliştirici tuvalleri yayında yok) karşılaştırmaya girer.
        var others = new System.Collections.Generic.List<Canvas>();
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != canvas && c.isRootCanvas && c.enabled) { c.enabled = false; others.Add(c); }
        var safe = GameObject.Find("SafeArea").GetComponent<RectTransform>();
        var safeComp = safe.GetComponent(T("ReferenceSafeArea")) as Behaviour;
        Vector2 aMin = safe.anchorMin, aMax = safe.anchorMax;
        if (notch)
        {
            safeComp.enabled = false;
            safe.anchorMin = new Vector2(0f, 34f / 844f);   // iPhone ev çubuğu
            safe.anchorMax = new Vector2(1f, 1f - 47f / 844f); // çentik
        }
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/intro"));
        Directory.CreateDirectory(folder);
        var rt = new RenderTexture(w, h, 24);
        camera.targetTexture = rt;
        camera.aspect = (float)w / h;
        var mode = canvas.renderMode;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 5f;
        Canvas.ForceUpdateCanvases();
        foreach (var b in canvas.GetComponentsInChildren<Behaviour>())
            if (b.GetType().Name == "IntroBackgroundFit")
                b.GetType().GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(b, null);
        foreach (var f in canvas.GetComponentsInChildren<AspectRatioFitter>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)f.transform);
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = prev;
        camera.targetTexture = null;
        camera.ResetAspect();
        canvas.renderMode = mode;
        rt.Release();
        Object.Destroy(rt);
        Object.Destroy(tex);
        foreach (var c in others) if (c != null) c.enabled = true;
        if (notch) { safe.anchorMin = aMin; safe.anchorMax = aMax; safeComp.enabled = true; }
    }
}
