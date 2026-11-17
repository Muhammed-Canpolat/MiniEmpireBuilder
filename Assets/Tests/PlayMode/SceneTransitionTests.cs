using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// 11. oturum (D114): sahne geçişi sağlamlığı (bug 4, 6, 15) ve geçişte müzik (bug 27). Gerçek GameManager geçiş yolları
/// (kararma + bulut) kullanılır; "[SceneLoad]" hata günlüğü düşerse Unity Test Framework testi kendiliğinden düşürür.
/// Gerçek kayıt ve ses ayarları saklanıp geri konur.
/// </summary>
[Timeout(240000)]
public class SceneTransitionTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static readonly string[] AudioKeys = { "audio_music_enabled", "audio_sfx_enabled", "audio_music_volume", "audio_sfx_volume" };
    private string savedKey, savedBackup, savedFile;
    private readonly string[] savedAudio = new string[4];
    private bool? devWas;

    private static string SaveFile => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    [SetUp]
    public void Keep()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = System.IO.File.Exists(SaveFile) ? System.IO.File.ReadAllText(SaveFile) : null;
        for (int i = 0; i < AudioKeys.Length; i++)
        {
            savedAudio[i] = !PlayerPrefs.HasKey(AudioKeys[i]) ? null
                : i == 0 || i == 1 ? "i" + PlayerPrefs.GetInt(AudioKeys[i]) : "f" + PlayerPrefs.GetFloat(AudioKeys[i]).ToString("R");
            PlayerPrefs.DeleteKey(AudioKeys[i]);   // müzik/efekt açık, tam ses
        }
        devWas = null;
    }

    [TearDown]
    public void Restore()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) System.IO.File.WriteAllText(SaveFile, savedFile);
        else if (System.IO.File.Exists(SaveFile)) System.IO.File.Delete(SaveFile);
        for (int i = 0; i < AudioKeys.Length; i++)
        {
            var v = savedAudio[i];
            if (v == null) PlayerPrefs.DeleteKey(AudioKeys[i]);
            else if (v[0] == 'i') PlayerPrefs.SetInt(AudioKeys[i], int.Parse(v.Substring(1)));
            else PlayerPrefs.SetFloat(AudioKeys[i], float.Parse(v.Substring(1)));
        }
        PlayerPrefs.Save();
        if (devWas.HasValue) T("DevSettings").GetProperty("Enabled").SetValue(null, devWas.Value);
        T("GemConfirm").GetMethod("Close").Invoke(null, new object[] { false });
        T("BattlePause").GetMethod("ResetAll").Invoke(null, null);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("BattleSession").GetMethod("Clear").Invoke(null, null);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = GM;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
    }

    // ---------- yardımcılar ----------

    private static Type Transition => T("SceneTransition");
    private static object TS(string name) => Transition.GetProperty(name).GetValue(null);
    private static Component GM => T("GameManager").GetProperty("Instance").GetValue(null) as Component;
    private static void GmCall(string method) => GM.GetType().GetMethod(method).Invoke(GM, null);
    private static Component Audio => T("AudioManager").GetProperty("Instance").GetValue(null) as Component;
    private static object AP(string name) => Audio.GetType().GetProperty(name).GetValue(Audio);
    private static Component Battle => T("BattleManager").GetProperty("Instance").GetValue(null) as Component;
    private static bool CloudActive => T("SceneCloudTransition").GetProperty("Active").GetValue(null) != null;
    private static int CloudCompleted => (int)T("SceneCloudTransition").GetProperty("CompletedCount").GetValue(null);
    private static void KillAllTweens() => T("DG.Tweening.DOTween").GetMethods()
        .First(m => m.Name == "KillAll" && m.GetParameters().Length == 1).Invoke(null, new object[] { false });
    private static bool BattleActive => Battle != null && (bool)Battle.GetType().GetProperty("IsBattleActive").GetValue(Battle);

    private static IEnumerator EnsureGameManager(int mainBase = 7)
    {
        var managerType = T("GameManager");
        var gm = GM;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", 5000);
        Set(save, "firstBaseGuideDone", true);
        Set(Field(save, "island"), "announced", true);
        Set(Field(save, "island"), "introSeen", true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
    }

    private static IEnumerator LoadDirect(string scene)
    {
        SceneManager.LoadScene(scene);
        for (int i = 0; i < 30; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.2f);
    }

    private static IEnumerator LoadCampaignBattle()
    {
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        yield return LoadDirect("BattleScene");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!BattleActive && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(BattleActive, "savaş başlamış olmalı");
    }

    /// <summary>Sahne <paramref name="scene"/> olana, geçiş kilidi ve bulut bitene dek (gerçek zaman) bekler.</summary>
    private static IEnumerator WaitLoaded(string scene, float seconds = 15f)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < deadline &&
               (SceneManager.GetActiveScene().name != scene || (bool)TS("Busy") || CloudActive))
            yield return null;
        Assert.AreEqual(scene, SceneManager.GetActiveScene().name, scene + " yüklenmeli");
        Assert.IsFalse((bool)TS("Busy"), "geçiş kilidi kalkmalı");
        for (int i = 0; i < 10; i++) yield return null;
    }

    private static void AssertRuntimeReset(string where)
    {
        Assert.AreEqual(1f, Time.timeScale, where + ": timeScale");
        Assert.IsFalse((bool)T("BattlePause").GetProperty("IsPaused").GetValue(null), where + ": duraklatma kalkmış");
        Assert.IsFalse(AudioListener.pause, where + ": ses dinleyicisi açık");
    }

    // ---------- bug 4 / 6: geçiş yolları ----------

    [UnityTest]
    public IEnumerator FadeTransitionCompletesWhileTimeScaleIsZero()
    {
        yield return EnsureGameManager();
        yield return LoadCampaignBattle();
        int errors = (int)TS("ErrorCount");
        // Duraklatılmış savaştan (ayarlar: timeScale=0 + AudioListener.pause) sefere dön: eski kararma Time.deltaTime ile sonsuza dek beklerdi.
        T("BattlePause").GetMethod("Pause").Invoke(null, new object[] { "settings" });
        Assert.AreEqual(0f, Time.timeScale);
        GmCall("ReturnToCampaign");
        yield return WaitLoaded("CampaignScene", 10f);
        AssertRuntimeReset("sefer haritası");
        Assert.AreEqual(errors, (int)TS("ErrorCount"), "geçişte yakalanan hata olmamalı");
    }

    [UnityTest]
    public IEnumerator ForfeitDuringContinueOfferThenReturnLoadsCleanly()
    {
        yield return EnsureGameManager();
        yield return LoadCampaignBattle();
        // Ana Üs yıkıldı → "devam et" teklifi (timeScale=0, GemConfirm açık); oyuncu bu sırada pes ediyor.
        Battle.GetType().GetMethod("OnMainBaseDestroyed").Invoke(Battle, null);
        Assert.IsTrue((bool)Battle.GetType().GetProperty("AwaitingContinue").GetValue(Battle));
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue((bool)Battle.GetType().GetMethod("Forfeit").Invoke(Battle, null));
        yield return null;
        Assert.IsFalse(BattleActive);
        Assert.IsFalse((bool)Battle.GetType().GetProperty("AwaitingContinue").GetValue(Battle), "teklif kapanmış");
        Assert.IsFalse((bool)T("GemConfirm").GetProperty("IsOpen").GetValue(null), "teklif penceresi kapanmış");
        Assert.AreEqual(1f, Time.timeScale, "pes etmeden sonra zaman akar");
        Assert.IsFalse((bool)Battle.GetType().GetMethod("Forfeit").Invoke(Battle, null), "ikinci pes etme yok sayılır");
        yield return new WaitForSecondsRealtime(0.5f);
        var back = GameObject.Find("VictoryReturnButton");
        Assert.IsNotNull(back, "sonuç ekranı dönüş düğmesi");
        back.GetComponent<Button>().onClick.Invoke();
        back.GetComponent<Button>().onClick.Invoke();   // çift dokunuş: ikinci yükleme başlamaz
        yield return WaitLoaded("CampaignScene", 10f);
        AssertRuntimeReset("pes et → sefer");
    }

    [UnityTest]
    public IEnumerator DuplicateRequestsLoadOnlyOnce()
    {
        yield return EnsureGameManager();
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        yield return LoadDirect("BaseScene");
        int ignored = (int)TS("IgnoredRequests"), completed = (int)TS("CompletedLoads");
        // Kararmalı yol: aynı karede üç istek → tek yükleme
        GmCall("LoadCampaignMap");
        GmCall("LoadCampaignMap");
        GmCall("ReturnToCampaign");
        yield return WaitLoaded("CampaignScene");
        Assert.AreEqual(completed + 1, (int)TS("CompletedLoads"), "tek yükleme");
        Assert.GreaterOrEqual((int)TS("IgnoredRequests") - ignored, 0);
        // Bulut yolu + kararma yedeği yarışmaz: ikinci Ana Üs isteği yok sayılır.
        int clouds = CloudCompleted;
        completed = (int)TS("CompletedLoads");
        GmCall("LoadBaseWorld");
        GmCall("LoadBaseWorld");
        GmCall("LoadCampaignMap");
        yield return WaitLoaded("BaseScene");
        Assert.AreEqual(completed + 1, (int)TS("CompletedLoads"), "bulut geçişinde tek yükleme");
        Assert.AreEqual(clouds + 1, CloudCompleted);
        Assert.IsNull(GameObject.Find("SceneFade"), "yedek kararma örtüsü açılmamış");
    }

    // ---------- bug 27: geçişte müzik ----------

    [UnityTest]
    public IEnumerator AbandoningIslandRaidLeavesOnlyBaseMusic()
    {
        yield return LoadIsland(null, 5, v => devWas = v);
        Assert.IsNotNull(Audio, "AudioManager");
        var raid = Prop(Setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }));
        yield return new WaitForSecondsRealtime(0.3f);   // çapraz geçiş (1,5 sn) tam ortasında çık
        Assert.AreEqual("island-battle", (string)AP("MusicCategory"));
        var battleClip = AP("CurrentMusicClip") as AudioClip;
        // IslandHud.GoHome ile aynı yol: akın bırakılır, bulut geçişiyle Ana Üs.
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        GmCall("LoadBaseWorld");
        yield return WaitLoaded("BaseScene");
        yield return new WaitForSecondsRealtime(2.2f);
        Assert.AreEqual("base", (string)AP("MusicCategory"));
        Assert.AreEqual(1, (int)AP("MusicPlayingCount"), "tek müzik");
        Assert.IsFalse((bool)AP("IdleMusicAudible"), "eski (savaş) parçası kısık çalmıyor");
        Assert.AreNotEqual(battleClip, AP("CurrentMusicClip") as AudioClip);
        Assert.Greater((float)AP("MusicVolumeNow"), 0.01f, "Ana Üs müziği duyuluyor");
        Assert.Greater((int)AP("PreparedSceneChanges"), 0, "yükleme öncesi müzik hazırlığı çalıştı");
        AssertRuntimeReset("ada → Ana Üs");
    }

    [UnityTest]
    public IEnumerator KilledCrossfadeIsCleanedBySafetyNet()
    {
        yield return EnsureGameManager();
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        yield return LoadDirect("BaseScene");
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.AreEqual(1, (int)AP("MusicPlayingCount"));
        int orphans = (int)AP("OrphanStops");
        // Çapraz geçişi başlat ve yarıda öldür (eski hata: DOTween.KillAll → OnComplete(Stop) gelmez, iki parça üst üste/kısık kalır).
        Audio.GetType().GetMethod("PlayIslandBgm").Invoke(Audio, new object[] { true });
        yield return new WaitForSecondsRealtime(0.25f);
        KillAllTweens();
        Assert.AreEqual(2, (int)AP("MusicPlayingCount"), "öldürülen geçiş: iki kaynak çalıyor (hata durumu kuruldu)");
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.AreEqual(1, (int)AP("MusicPlayingCount"), "güvenlik ağı solan parçayı durdurdu");
        Assert.IsFalse((bool)AP("IdleMusicAudible"));
        Assert.Greater((int)AP("OrphanStops"), orphans);
        Assert.Greater((float)AP("MusicVolumeNow"), 0.01f, "yeni parça hedef sesine oturdu (kısık takılmadı)");
        yield return LoadDirect("BaseScene");   // sonraki testler için Ana Üs müziği
    }
}
