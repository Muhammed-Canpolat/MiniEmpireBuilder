using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// 10. oturum (D110): SFX_Pack sesleri + ortam sesi + sahne geçişi (bug 27). AudioManager'a yansımayla erişir.
/// Gerçek kayıt saklanıp geri konur; ses ayarları (PlayerPrefs) ve zaman ölçeği TearDown'da eski haline döner.
/// </summary>
[Timeout(240000)]
public class AudioSfxPackTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static readonly string[] PrefKeys = { "audio_music_enabled", "audio_sfx_enabled" };
    private static readonly string[] PrefFloatKeys = { "audio_music_volume", "audio_sfx_volume" };
    private string savedKey, savedBackup, savedFile;
    private readonly float?[] savedFloats = new float?[2];
    private readonly int?[] savedInts = new int?[2];

    private static string SaveFile => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    [SetUp]
    public void Keep()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = System.IO.File.Exists(SaveFile) ? System.IO.File.ReadAllText(SaveFile) : null;
        for (int i = 0; i < 2; i++)
        {
            savedInts[i] = PlayerPrefs.HasKey(PrefKeys[i]) ? PlayerPrefs.GetInt(PrefKeys[i]) : (int?)null;
            savedFloats[i] = PlayerPrefs.HasKey(PrefFloatKeys[i]) ? PlayerPrefs.GetFloat(PrefFloatKeys[i]) : (float?)null;
            PlayerPrefs.DeleteKey(PrefKeys[i]);
            PlayerPrefs.DeleteKey(PrefFloatKeys[i]);
        }
    }

    [TearDown]
    public void Restore()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) System.IO.File.WriteAllText(SaveFile, savedFile);
        else if (System.IO.File.Exists(SaveFile)) System.IO.File.Delete(SaveFile);
        for (int i = 0; i < 2; i++)
        {
            if (savedInts[i] != null) PlayerPrefs.SetInt(PrefKeys[i], savedInts[i].Value); else PlayerPrefs.DeleteKey(PrefKeys[i]);
            if (savedFloats[i] != null) PlayerPrefs.SetFloat(PrefFloatKeys[i], savedFloats[i].Value); else PlayerPrefs.DeleteKey(PrefFloatKeys[i]);
        }
        PlayerPrefs.Save();
        T("BattlePause").GetMethod("ResetAll").Invoke(null, null);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("BattleSession").GetMethod("Clear").Invoke(null, null);
        var audio = Audio;
        if (audio != null)
        {
            Call(audio, "SetAppPaused", false);
            Call(audio, "SetAppFocused", true);
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
    }

    // ---------- yardımcılar ----------

    private static Component Audio => T("AudioManager").GetProperty("Instance").GetValue(null) as Component;
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static object Prop(string name) => Audio.GetType().GetProperty(name).GetValue(Audio);
    private static int MusicPlaying => (int)Prop("MusicPlayingCount");
    private static int AmbiencePlaying => (int)Prop("AmbiencePlayingCount");
    private static string MusicName => (Prop("CurrentMusicClip") as AudioClip)?.name;
    private static string AmbienceName => (Prop("CurrentAmbienceClip") as AudioClip)?.name;
    private static AudioClip Resolve(string key) => (AudioClip)T("AudioManager").GetMethod("ResolveSfx").Invoke(null, new object[] { key });
    private static int Playing(AudioClip clip) => (int)Call(Audio, "PlayingSfxCount", clip);

    private static ScriptableObject Library => Resources.Load<ScriptableObject>("AudioLibrary");
    private static string TrackClip(string field)
    {
        var track = LibField(field);
        return ((AudioClip)track.GetType().GetField("clip").GetValue(track)).name;
    }
    private static object LibField(string name) => Library.GetType().GetField(name).GetValue(Library);

    private static IEnumerator EnsureGameManager(int mainBase = 7)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", 5000);
        Set(save, "firstBaseGuideDone", true);
        Set(Field(save, "island"), "announced", true);
        Set(Field(save, "island"), "introSeen", true);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
    }

    private static IEnumerator LoadBase()
    {
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 40; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.6f);
    }

    private static IEnumerator LoadBattle()
    {
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("BattleSession").GetMethod("Begin").Invoke(null, new object[] { 1, 1 });
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 40; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.6f);
    }

    private static IEnumerator LoadIsland()
    {
        T("IslandSession").GetMethod("Begin").Invoke(null, null);
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 40; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.8f);
    }

    private static void AssertBaseAudio(string where)
    {
        Assert.AreEqual(1, MusicPlaying, where + ": tek müzik");
        Assert.AreEqual(1, AmbiencePlaying, where + ": tek ortam sesi");
        Assert.AreEqual("Inside Day", AmbienceName, where);
        StringAssert.DoesNotContain("Beach", AmbienceName ?? "");
    }

    // ---------- kütüphane ve eşleme ----------

    [Test]
    public void LibraryMapsEveryEventToTheRightClip()
    {
        // Ses yöneticisi sahnesiz de kütüphaneyi okur (statik çözümleme).
        Assert.NotNull(Library, "Resources/AudioLibrary.asset");
        void Is(string key, string clip)
        {
            var found = Resolve(key);
            Assert.NotNull(found, key);
            Assert.AreEqual(clip, found.name, key);
        }
        Is("arrowImpact", "Bow Impact Hit 1");
        Is("swordHit", "Sword Impact Hit 2");
        Is("chop", "chop 2");
        Is("goldCollect", "mine 3");
        Is("fireball", "Fireball 1");
        Is("raidHorn", "Wave Attack 1");
        Is("waveStart", "Wave Attack 1");   // sıradaki; ikincisi çalınınca Wave Attack 2
        var waves = (AudioClip[])LibField("waveStart");
        Assert.AreEqual(new[] { "Wave Attack 1", "Wave Attack 2" }, waves.Select(c => c.name).ToArray());
        Assert.AreEqual("Inside Day", TrackClip("baseAmbience"));
        Assert.AreEqual("Beach", TrackClip("islandAmbience"));
        Assert.AreEqual("Sea Storm", TrackClip("stormAmbience"));
        // Karşılığı olmayan anahtarlar alakasız sesle doldurulmaz.
        Assert.IsNull(Resolve("coin"));
        Assert.IsNull(Resolve("click"));
        Assert.IsNull(Resolve("bilinmeyen"));
    }

    [Test]
    public void ImportSettingsAreMobileFriendly()
    {
        // .meta metninden okunur (editör API'sine bağımlı olmadan): kısa WAV = ADPCM + mono, uzun OGG = Streaming + Vorbis %60.
        string dir = System.IO.Path.Combine(Application.dataPath, "Audio/Music/SFX_Pack");
        var metas = System.IO.Directory.GetFiles(dir, "*.meta");
        Assert.AreEqual(16, metas.Length);
        string Value(string text, string key) =>
            System.Text.RegularExpressions.Regex.Match(text, @"\s+" + key + @": (\S*)").Groups[1].Value.Trim();
        foreach (var meta in metas)
        {
            string text = System.IO.File.ReadAllText(meta);
            string name = System.IO.Path.GetFileName(meta);
            if (name.EndsWith(".wav.meta"))
            {
                Assert.AreEqual("2", Value(text, "compressionFormat"), name + " ADPCM");
                Assert.AreEqual("1", Value(text, "forceToMono"), name + " mono");
                Assert.AreNotEqual("2", Value(text, "loadType"), name + " akış değil");
            }
            else
            {
                Assert.AreEqual("2", Value(text, "loadType"), name + " Streaming");
                Assert.AreEqual("1", Value(text, "compressionFormat"), name + " Vorbis");
                Assert.AreEqual("0.6", Value(text, "quality"), name);
            }
        }
    }

    // ---------- efekt sınırları ----------

    [UnityTest]
    public IEnumerator SfxRateLimitAndConcurrencyCap()
    {
        yield return EnsureGameManager();
        yield return LoadBase();
        var impact = Resolve("arrowImpact");
        yield return new WaitForSecondsRealtime(0.3f);
        for (int i = 0; i < 10; i++) Call(Audio, "PlaySfx", "arrowImpact");
        Assert.AreEqual(1, Playing(impact), "aynı karede 10 çağrı → en fazla 1 (asgari aralık)");
        Assert.AreEqual(impact, Prop("LastSfxClip"));

        // Klip başına en çok 3 eşzamanlı: raidHorn (Wave Attack 1, asgari aralık 1 sn, klip ~3,9 sn).
        var horn = Resolve("raidHorn");
        Assert.Greater(horn.length, 3.5f, "test varsayımı: klip 3 aralıktan uzun");
        int peak = 0;
        for (int i = 0; i < 4; i++)
        {
            Call(Audio, "PlaySfx", "raidHorn");
            peak = Mathf.Max(peak, Playing(horn));
            yield return new WaitForSecondsRealtime(1.1f);
        }
        Assert.AreEqual(3, peak, "aynı klip 4. kez istenince 3 eşzamanlıda kalır");
        Assert.LessOrEqual(Playing(null), 8, "havuz 8");
    }

    [UnityTest]
    public IEnumerator WaveStartAlternatesBetweenBothClips()
    {
        yield return EnsureGameManager();
        yield return LoadBase();
        var first = Resolve("waveStart");
        Call(Audio, "PlaySfx", "waveStart");
        Assert.AreEqual(first, Prop("LastSfxClip"));
        yield return new WaitForSecondsRealtime(1.1f);
        Call(Audio, "PlaySfx", "waveStart");
        var second = (AudioClip)Prop("LastSfxClip");
        Assert.AreNotEqual(first, second);
        Assert.AreEqual("Wave Attack 2", second.name);
    }

    [UnityTest]
    public IEnumerator EventsAskForTheRightClip()
    {
        yield return EnsureGameManager();
        yield return LoadBase();
        // Doğa temizleme sonu: ağaç/kütük = chop, kaya = mine, çalı = sessiz.
        var natureClear = T("NatureClear").GetMethod("Finish");
        natureClear.Invoke(null, new object[] { "Tree_3_4", Vector3.zero, null });
        Assert.AreEqual("chop 2", ((AudioClip)Prop("LastSfxClip")).name);
        yield return new WaitForSecondsRealtime(0.3f);
        natureClear.Invoke(null, new object[] { "rock:2,2", Vector3.zero, null });
        Assert.AreEqual("mine 3", ((AudioClip)Prop("LastSfxClip")).name);
        yield return new WaitForSecondsRealtime(0.3f);
        var before = Prop("LastSfxClip");
        natureClear.Invoke(null, new object[] { "bush:1,1", Vector3.zero, null });
        Assert.AreEqual(before, Prop("LastSfxClip"), "çalı temizliği ses çıkarmaz");
    }

    [UnityTest]
    public IEnumerator SettingsSilenceEffectsAndAmbience()
    {
        yield return EnsureGameManager();
        yield return LoadBase();
        var sword = Resolve("swordHit");
        // Efekt kapalı / kaydırıcı 0 → çalmaz.
        Call(Audio, "SetSfxEnabled", false);
        Call(Audio, "PlaySfx", "swordHit");
        Assert.AreEqual(0, Playing(sword));
        Call(Audio, "SetSfxEnabled", true);
        Call(Audio, "SetSfxVolume01", 0f);
        Call(Audio, "PlaySfx", "swordHit");
        Assert.AreEqual(0, Playing(sword));
        Call(Audio, "SetSfxVolume01", 1f);
        Call(Audio, "PlaySfx", "swordHit");
        Assert.AreEqual(1, Playing(sword));

        // Ortam sesi Müzik ayarına bağlı: kaydırıcı ölçekler, kapatınca susar, açınca geri gelir.
        Assert.AreEqual(1, AmbiencePlaying);
        float full = (float)Prop("AmbienceVolumeNow");
        Assert.Greater(full, 0.01f);
        Call(Audio, "SetMusicVolume01", 0.5f);
        Assert.AreEqual(full * 0.5f, (float)Prop("AmbienceVolumeNow"), 0.002f);
        Call(Audio, "SetMusicVolume01", 0f);
        Assert.AreEqual(0f, (float)Prop("AmbienceVolumeNow"), 0.0001f);
        Call(Audio, "SetMusicVolume01", 1f);
        Call(Audio, "SetMusicEnabled", false);
        yield return null;
        Assert.AreEqual(0, AmbiencePlaying, "Müzik kapalı → ortam sesi susar");
        Assert.AreEqual(0, MusicPlaying);
        Call(Audio, "SetMusicEnabled", true);
        yield return new WaitForSecondsRealtime(1.3f);
        Assert.AreEqual(1, AmbiencePlaying, "Müzik açık → ortam sesi geri gelir");
        Assert.AreEqual(1, MusicPlaying);
    }

    // ---------- sahne geçişleri (bug 27) ----------

    [UnityTest]
    public IEnumerator BaseBattleBaseKeepsOneMusicAndOneAmbience()
    {
        yield return EnsureGameManager();
        yield return LoadBase();
        AssertBaseAudio("Ana Üs");
        var baseMusic = MusicName;
        yield return LoadBattle();
        Assert.AreEqual(1, MusicPlaying, "Savaş: tek müzik");
        Assert.AreEqual(0, AmbiencePlaying, "Savaş: Ana Üs ortam sesi susmuş");
        Assert.IsNull(AmbienceName);
        var battleMusic = MusicName;
        Assert.AreNotEqual(baseMusic, battleMusic);
        yield return LoadBase();
        AssertBaseAudio("Ana Üs'e dönüş");
        Assert.AreNotEqual(battleMusic, MusicName, "savaş parçası bırakılmış olmalı");
    }

    [UnityTest]
    public IEnumerator BaseIslandBaseKeepsOneMusicAndOneAmbience()
    {
        yield return EnsureGameManager();
        yield return LoadBase();
        AssertBaseAudio("Ana Üs");
        yield return LoadIsland();
        Assert.AreEqual(1, MusicPlaying, "Ada: tek müzik");
        Assert.AreEqual(1, AmbiencePlaying, "Ada: tek ortam sesi");
        Assert.AreEqual("Beach", AmbienceName);
        yield return LoadBase();
        AssertBaseAudio("Ana Üs'e dönüş");
    }

    [UnityTest]
    public IEnumerator SceneExitFadeCompletesWhileTimeScaleIsZero()
    {
        yield return EnsureGameManager();
        yield return LoadBattle();
        var battleMusic = MusicName;
        Assert.AreEqual(1, MusicPlaying);
        // Savaş/ayarlar duraklatması timeScale=0 bırakır; sahne çıkışındaki solma yine de bitmeli.
        Time.timeScale = 0f;
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 40; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.8f);
        Assert.AreEqual(1, MusicPlaying, "eski savaş müziği durmuş olmalı");
        Assert.AreEqual(1, AmbiencePlaying);
        Assert.AreNotEqual(battleMusic, MusicName);
        Assert.AreEqual("Inside Day", AmbienceName);
        // Yeni parçanın sesi gerçekten yükselmiş (kısık takılı kalmamış).
        Assert.Greater((float)Prop("AmbienceVolumeNow"), 0.01f);
        Time.timeScale = 1f;
    }

    // ---------- arka plan ----------

    [UnityTest]
    public IEnumerator BackgroundAndFocusLossSilenceAudio()
    {
        yield return EnsureGameManager();
        yield return LoadBase();
        AudioListener.pause = false;
        Call(Audio, "SetAppPaused", true);
        Assert.IsTrue(AudioListener.pause, "arka plana geçince sessiz");
        Call(Audio, "SetAppPaused", false);
        Assert.IsFalse(AudioListener.pause, "dönünce geri gelir");
        Call(Audio, "SetAppFocused", false);
        Assert.IsTrue(AudioListener.pause, "odak kaybında sessiz");
        Call(Audio, "SetAppFocused", true);
        Assert.IsFalse(AudioListener.pause);

        // Savaş duraklatması sürerken uygulama dönünce ses AÇILMAZ (DEVAM penceresi açar).
        T("BattlePause").GetMethod("Pause").Invoke(null, new object[] { "settings" });
        Call(Audio, "SetAppPaused", true);
        Call(Audio, "SetAppPaused", false);
        Assert.IsTrue(AudioListener.pause);
        T("BattlePause").GetMethod("Resume").Invoke(null, new object[] { "settings" });
        Assert.IsFalse(AudioListener.pause);
        // Arka plandayken duraklatma kalksa da ses açılmaz.
        Call(Audio, "SetAppFocused", false);
        T("BattlePause").GetMethod("Pause").Invoke(null, new object[] { "settings" });
        T("BattlePause").GetMethod("Resume").Invoke(null, new object[] { "settings" });
        Assert.IsTrue(AudioListener.pause);
        Call(Audio, "SetAppFocused", true);
        Assert.IsFalse(AudioListener.pause);
    }
}
