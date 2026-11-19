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

    private static bool DevToggleVisible()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "DevToggle" && t.gameObject.activeInHierarchy) return true;
        return false;
    }

    /// <summary>Kullanıcı isteği: açılış (Intro) ekranında "DEV" düğmesi görünmez; diğer sahnelerde DevMenu davranışı aynı.</summary>
    [UnityTest]
    public IEnumerator Intro_HasNoDevButton_ButBaseSceneKeepsIt()
    {
        SceneManager.LoadScene("Intro");
        for (int i = 0; i < 4; i++) yield return null;
        Assert.AreEqual("Intro", SceneManager.GetActiveScene().name);
        Assert.IsFalse(DevToggleVisible(), "Intro sahnesinde DEV düğmesi görünmemeli");
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 20; i++) yield return null;
        Assert.AreEqual("BaseScene", SceneManager.GetActiveScene().name);
        Assert.IsTrue(DevToggleVisible(), "Ana Üs'te geliştirici menüsü düğmesi aynen duruyor (Development/Editor)");
    }

    [UnityTest]
    public IEnumerator Intro_FillsBar_RotatesTips_ThenOpensGame()
    {
        StringAssert.EndsWith("Intro.unity", SceneUtility.GetScenePathByBuildIndex(0), "Build index 0 Intro olmalı");
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
            Assert.AreEqual("BaseScene", SceneManager.GetActiveScene().name, "Intro bitince doğrudan Ana Üs açılmalı");
            Assert.IsTrue(sawFull, "Çubuk %100'e ulaşmadı");
            Assert.IsTrue(tipChanged, "İpucu değişmedi");
            yield return null;
            Assert.IsFalse(SceneManager.GetSceneByName("Intro").isLoaded, "Intro sahnesi bellekten çıkmalı");
            Assert.IsNull(Object.FindFirstObjectByType(T("MiniEmpire.Intro.IntroSceneController")), "Intro nesnesi kalmamalı");
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[IntroTest] " + log.Activation);

            Assert.IsNull(Object.FindFirstObjectByType(T("IntroSceneSetup")), "Eski giriş ekranı (MainMenuScene) gösterilmemeli");

            // Ayarlar → YENİ OYUN → onay: ilerleme silinir, Ana Üs sıfırdan açılır (eski sahneye/intro'ya düşmez)
            var gmType = T("GameManager");
            var gm = gmType.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(gm, "GameManager yok");
            var data = gmType.GetProperty("PlayerData").GetValue(gm);
            Assert.IsNotNull(data, "Oyun verisi hazır değil");
            var goldField = data.GetType().GetField("gold");
            goldField.SetValue(data, 987654);
            yield return new WaitForSecondsRealtime(1.5f);
            var settings = Object.FindFirstObjectByType(T("AudioSettingsUI")) as Component;
            Assert.IsNotNull(settings, "Ayarlar paneli yok");
            settings.GetType().GetMethod("Open").Invoke(settings, null);
            yield return new WaitForSecondsRealtime(0.4f);
            var newGameBtn = GameObject.Find("NewGameButton");
            Assert.IsNotNull(newGameBtn, "Ayarlarda YENİ OYUN düğmesi yok");
            CaptureGame("settings_new_game");
            newGameBtn.GetComponent<Button>().onClick.Invoke();
            yield return null;
            var dialog = GameObject.Find("NewGameConfirm");
            Assert.IsNotNull(dialog, "Onay penceresi açılmadı");
            StringAssert.Contains("ilerlemesi kaybedilecek", dialog.GetComponentsInChildren<Component>().Length > 0 ? AllText(dialog) : "");
            CaptureGame("new_game_confirm");
            dialog.transform.Find("SafeArea/Panel/ConfirmButton").GetComponent<Button>().onClick.Invoke();
            string seen = "";
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 8f)
            {
                string n = SceneManager.GetActiveScene().name;
                if (n == "Intro" || n == "MainMenuScene") seen = n;
                yield return null;
            }
            Assert.AreEqual("", seen, "Yeni oyun eski sahneye/intro'ya düşmemeli");
            Assert.AreEqual("BaseScene", SceneManager.GetActiveScene().name);
            var fresh = gmType.GetProperty("PlayerData").GetValue(gm);
            Assert.AreNotEqual(987654, (int)goldField.GetValue(fresh), "İlerleme sıfırlanmadı");
            CaptureGame("base_after_new_game");
        }
        finally { Application.logMessageReceived -= log.On; }
    }

    private static string AllText(GameObject root)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in root.GetComponentsInChildren<Component>(true))
            if (c != null && c.GetType().Name == "TextMeshProUGUI") sb.Append((string)c.GetType().GetProperty("text").GetValue(c)).Append(' ');
        return sb.ToString();
    }

    /// <summary>Oyun ekranı (tüm Overlay tuvalleri dahil) 780x1688: PlaytestOutput/intro/.</summary>
    private static void CaptureGame(string name) => CardWorkshopTests.CaptureScreen(name, "intro");

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
