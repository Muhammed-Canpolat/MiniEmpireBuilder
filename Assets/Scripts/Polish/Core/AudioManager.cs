using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Müzik ve efektler. Klipler Resources/AudioLibrary.asset'ten gelir (bu bileşen çalışma
/// anında oluşturulduğu için Inspector'dan atanamaz). Sahneye göre müzik:
/// Menü → menü teması, Ana Üs → çalma listesi (sırayla), Sefer → sefer teması,
/// Savaş → rastgele bir döngü (+ sisli bölgede ambiyans katmanı), boss çıkınca boss müziği.
/// Parçalar arası geçiş 1 sn; her parça kendi ses çarpanıyla çalar.
/// 10. oturum (D110): ortam sesi (Ana Üs/Ada) müzik gibi tek hedef klipli çapraz geçişle çalar ve Müzik ayarına bağlıdır;
/// tüm solmalar GERÇEK zamanlıdır (bug 27: Time.timeScale=0 iken solma donuyor, eski müzik kısık kalıyordu);
/// efektler 8'li havuzda, ses başına asgari aralık ve klip başına en çok 3 eşzamanlı ile sınırlıdır; uygulama arka plana
/// geçince (veya odak kaybolunca) ses AudioListener.pause ile susar.
/// </summary>
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicEnabledKey = "audio_music_enabled";
    private const string SfxEnabledKey = "audio_sfx_enabled";
    private const float FadeSeconds = 1f;
    private const float FogLayerVolume = 0.35f;
    private const float AmbienceLevel = 0.35f;     // ortam sesi: müzik seviyesinin bu kadarı
    private const float StormLevel = 0.5f;         // ada tanıtımı fırtınası
    private const float SceneFadeSeconds = 0.6f;   // sahne değişince eski kaynakların solma süresi (gerçek zaman)
    private const int EffectPoolSize = 8;
    private const int MaxSameClip = 3;

    private AudioSource musicA;
    private AudioSource musicB;
    private AudioSource windSource;
    private AudioSource birdSource;
    private AudioSource layerSource;
    private AudioSource ambA;
    private AudioSource ambB;
    private AudioSource[] effects;
    private AudioClip[] effectClip;       // havuz kaynağında son çalan klip
    private float[] effectEndAt;          // Time.unscaledTime cinsinden bitiş (kendi sayacımız: ses aygıtından bağımsız)
    private float[] effectStartAt;
    private float[] sfxLastAt;            // anahtar başına son çalma (asgari aralık)
    private bool isAmbAActive = true;
    private MusicTrack currentAmbience;
    private float ambienceScale = 1f;
    private float windBase, birdBase, layerBase;   // katman tabanları (kaydırıcı çarpılmadan)
    private int waveStartIndex;
    private bool appPaused, appUnfocused;

    private readonly HashSet<int> wiredButtons = new HashSet<int>();
    private bool isMusicAActive = true;
    private MusicTrack currentTrack;
    private MusicTrack[] playlist;      // Ana Üs: sırayla
    private int playlistIndex;
    private AudioListener fallbackListener;
    private Coroutine buttonScan;
    private float musicVolume = 0.6f;
    private float sfxVolume = 0.85f;

    public static AudioManager Instance { get; private set; }
    /// <summary>Ayarlar ekranındaki ses kaydırıcıları (0–1); kitaplık ses düzeyiyle çarpılır.</summary>
    public float MusicVolume01 => Mathf.Clamp01(PlayerPrefs.GetFloat("audio_music_volume", 1f));
    public float SfxVolume01 => Mathf.Clamp01(PlayerPrefs.GetFloat("audio_sfx_volume", 1f));
    public bool MusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
    public bool SfxEnabled => PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;
    private static AudioLibrary Library => AudioLibrary.Instance;

    // ---- Test/telemetri görünümü ----
    public bool InBackground => appPaused || appUnfocused;
    public int MusicPlayingCount => (musicA != null && musicA.isPlaying ? 1 : 0) + (musicB != null && musicB.isPlaying ? 1 : 0);
    public int AmbiencePlayingCount => (ambA != null && ambA.isPlaying ? 1 : 0) + (ambB != null && ambB.isPlaying ? 1 : 0);
    public AudioClip CurrentMusicClip => currentTrack != null ? currentTrack.clip : null;
    public AudioClip CurrentAmbienceClip => currentAmbience != null ? currentAmbience.clip : null;
    public float AmbienceVolumeNow => ActiveAmbience != null ? ActiveAmbience.volume : 0f;
    public AudioClip LastSfxClip { get; private set; }
    private AudioSource ActiveAmbience => isAmbAActive ? ambA : ambB;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        fallbackListener = gameObject.AddComponent<AudioListener>();
        UpdateListener();
        musicA = MakeSource("MusicA", true);
        musicB = MakeSource("MusicB", true);
        windSource = MakeSource("BaseWind", true);
        birdSource = MakeSource("BaseBirds", true);
        layerSource = MakeSource("MusicLayer", true);
        ambA = MakeSource("AmbienceA", true);
        ambB = MakeSource("AmbienceB", true);
        effects = new AudioSource[EffectPoolSize];
        effectClip = new AudioClip[EffectPoolSize];
        effectEndAt = new float[EffectPoolSize];
        effectStartAt = new float[EffectPoolSize];
        sfxLastAt = new float[SfxKeyCount];
        for (int i = 0; i < effects.Length; i++) effects[i] = MakeSource("Effect" + i, false);
        if (Library != null)
        {
            musicVolume = Library.musicVolume;
            sfxVolume = Library.sfxVolume;
        }
        SceneManager.activeSceneChanged += OnSceneChanged;
        buttonScan = StartCoroutine(WireUiButtons());
        OnSceneChanged(default, SceneManager.GetActiveScene());
    }

    private AudioSource MakeSource(string name, bool loop)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        if (buttonScan != null) StopCoroutine(buttonScan);
        foreach (var source in new[] { musicA, musicB, ambA, ambB, layerSource }) if (source != null) source.DOKill();
        Instance = null;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        // D114: bu olayda bir hata sahnenin müziğini değiştirmeden bırakmasın (eski parça çalmaya devam ederdi) ve
        // activeSceneChanged'e bağlı diğer dinleyicileri düşürmesin.
        try
        {
            UpdateListener();
            wiredButtons.Clear();
            SettleStaleSources();
            switch (newScene.name)
            {
                case "BaseScene": PlayBaseBgm(); break;
                case "BattleScene": PlaySceneBattleOrIsland(); break;
                case "MainMenuScene": PlayIntroBgm(); break;
                default: PlayWorldBgm(); break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{SceneTransition.LogTag} AudioManager sahne müziği ({newScene.name}) hata verdi: {e}");
            SceneTransition.Guard("müzik yedek durdurma", () => { StopIdle(musicA, musicB, isMusicAActive); StopIdle(ambA, ambB, isAmbAActive); StopLayers(); });
        }
    }

    /// <summary>
    /// D114 (bug 27): sahne yüklemesinden HEMEN önce (DOTween.KillAll'dan önce) çağrılır. KillAll yarıda kalan çapraz geçişi
    /// öldürünce eski parça kısık sesle sonsuza dek çalıyordu (OnComplete(Stop) hiç gelmez). Burada solmalar sabitlenir:
    /// solan (etkin olmayan) müzik/ortam kaynağı kesin DURUR, etkin kaynak hedef sesine oturur; katmanlar durur.
    /// Yeni sahnenin müziği OnSceneChanged'de başlar.
    /// </summary>
    public void PrepareSceneChange()
    {
        StopIdle(musicA, musicB, isMusicAActive);
        StopIdle(ambA, ambB, isAmbAActive);
        SnapActive(ActiveMusic, currentTrack, currentTrack != null ? TrackVolume(currentTrack) : 0f);
        SnapActive(ActiveAmbience, currentAmbience, currentAmbience != null ? AmbienceVolume(currentAmbience) : 0f);
        if (layerSource != null) { layerSource.DOKill(); if (layerBase <= 0f) layerSource.Stop(); }
        PreparedSceneChanges++;
    }

    /// <summary>Test/telemetri: sahne öncesi hazırlık ve güvenlik ağının durdurduğu sahipsiz kaynak sayısı.</summary>
    public int PreparedSceneChanges { get; private set; }
    public int OrphanStops { get; private set; }
    /// <summary>Etkin olmayan müzik kaynağında çalan (solmayan) bir parça var mı (test).</summary>
    public bool IdleMusicAudible
    {
        get
        {
            var idle = isMusicAActive ? musicB : musicA;
            return idle != null && idle.isPlaying && idle.volume > 0.0001f;
        }
    }
    public float MusicVolumeNow => ActiveMusic != null ? ActiveMusic.volume : 0f;

    private static void StopIdle(AudioSource a, AudioSource b, bool aActive)
    {
        var idle = aActive ? b : a;
        if (idle == null) return;
        idle.DOKill();
        idle.Stop();
        idle.volume = 0f;
    }

    private void SnapActive(AudioSource source, MusicTrack track, float target)
    {
        if (source == null) return;
        source.DOKill();
        if (track == null || source.clip != track.clip) { source.Stop(); source.volume = 0f; return; }
        if (MusicEnabled && source.isPlaying) source.volume = target;
    }

    private float nextInvariantCheck;

    /// <summary>
    /// D114 (bug 27) güvenlik ağı, 0,5 sn'de bir (gerçek zaman): solma tween'i olmadan çalan etkin-olmayan müzik/ortam kaynağı
    /// durdurulur; etkin kaynak hedef klipten farklıysa durur, hedef klipse ve solma yoksa sesi hedefe oturur (kısık takılı kalmaz).
    /// Böylece hangi yoldan gelinirse gelinsin aynı anda tek müzik çalar.
    /// </summary>
    private void EnforceSingleMusic()
    {
        if (Time.unscaledTime < nextInvariantCheck) return;
        nextInvariantCheck = Time.unscaledTime + 0.5f;
        StopOrphan(isMusicAActive ? musicB : musicA);
        StopOrphan(isAmbAActive ? ambB : ambA);
        FixActive(ActiveMusic, currentTrack, currentTrack != null ? TrackVolume(currentTrack) : 0f);
        FixActive(ActiveAmbience, currentAmbience, currentAmbience != null ? AmbienceVolume(currentAmbience) : 0f);
    }

    private void StopOrphan(AudioSource source)
    {
        if (source == null || !source.isPlaying || DOTween.IsTweening(source)) return;
        source.Stop();
        source.volume = 0f;
        OrphanStops++;
    }

    private void FixActive(AudioSource source, MusicTrack track, float target)
    {
        if (source == null || !source.isPlaying || DOTween.IsTweening(source)) return;
        if (track == null || source.clip != track.clip) { StopOrphan(source); return; }
        if (MusicEnabled && Mathf.Abs(source.volume - target) > 0.01f) source.volume = target;
    }

    /// <summary>
    /// Bug 27: sahne değişince önceki sahnenin kaynakları temizlenir. Kullanılmayan (solan) müzik/ortam kaynağı ve katmanlar
    /// gerçek zamanlı kısa solmayla (0,6 sn) durur; solma eskiden ölçekli zamandaydı (timeScale=0 iken donuyor, OnComplete(Stop)
    /// hiç çalışmıyordu → eski parça kısık çalmaya devam ediyor, yenisiyle üst üste biniyordu).
    /// </summary>
    private void SettleStaleSources()
    {
        FadeOut(isMusicAActive ? musicB : musicA, SceneFadeSeconds);
        FadeOut(isAmbAActive ? ambB : ambA, SceneFadeSeconds);
        StopLayers();
    }

    private static void FadeOut(AudioSource source, float seconds)
    {
        if (source == null) return;
        source.DOKill();
        if (!source.isPlaying && source.volume <= 0.0001f) { source.Stop(); return; }
        source.DOFade(0f, seconds).SetUpdate(true).OnComplete(source.Stop);
    }

    public void SetAppPaused(bool paused) { appPaused = paused; ApplyBackground(); }
    public void SetAppFocused(bool focused) { appUnfocused = !focused; ApplyBackground(); }

    private void ApplyBackground()
    {
        if (InBackground) AudioListener.pause = true;
        else if (!BattlePause.IsPaused) AudioListener.pause = false;   // savaş DEVAM penceresi sesi kendisi açar
    }

    // Editörde odak/duraklatma (başka pencereye geçmek) testleri ve çalışmayı bozmasın; cihazda gerçek arka plan.
    private void OnApplicationPause(bool paused) { if (!Application.isEditor) SetAppPaused(paused); }
    private void OnApplicationFocus(bool focus) { if (!Application.isEditor) SetAppFocused(focus); }

    private void UpdateListener()
    {
        if (fallbackListener == null) return;
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None))
            if (listener != fallbackListener && listener.enabled)
            { fallbackListener.enabled = false; return; }
        fallbackListener.enabled = true;
    }

    private IEnumerator WireUiButtons()
    {
        while (true)
        {
            UpdateListener();
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (button == null || !wiredButtons.Add(button.GetInstanceID())) continue;
                button.onClick.AddListener(() => PlaySfx("buttonClick"));
            }
            yield return new WaitForSecondsRealtime(0.8f);
        }
    }

    // ==================== MÜZİK ====================

    private AudioSource ActiveMusic => isMusicAActive ? musicA : musicB;
    private float TrackVolume(MusicTrack track) => musicVolume * MusicVolume01 * Mathf.Clamp01(track?.volume ?? 1f);

    /// <summary>Parçaya 1 sn'lik çapraz geçiş. loop=false ise parça bitince çalma listesi ilerler.</summary>
    private void PlayTrack(MusicTrack track, bool loop, float fade = FadeSeconds)
    {
        if (track != null && track.clip == null) track = null;
        if (track != null && currentTrack != null && track.clip == currentTrack.clip) return;
        currentTrack = track;
        if (track == null)
        {
            FadeOut(musicA, fade);
            FadeOut(musicB, fade);
            return;
        }
        AudioSource from = ActiveMusic;
        AudioSource to = isMusicAActive ? musicB : musicA;
        from.DOKill();
        to.DOKill();
        to.clip = track.clip;
        to.loop = loop;
        to.volume = 0f;
        if (MusicEnabled) to.Play(); else to.Stop();
        from.DOFade(0f, fade).SetUpdate(true).OnComplete(from.Stop);
        to.DOFade(MusicEnabled ? TrackVolume(track) : 0f, fade).SetUpdate(true);
        isMusicAActive = !isMusicAActive;
    }

    private static MusicTrack Random(MusicTrack[] tracks)
    {
        if (tracks == null) return null;
        var usable = System.Array.FindAll(tracks, t => t != null && t.clip != null);
        return usable.Length == 0 ? null : usable[UnityEngine.Random.Range(0, usable.Length)];
    }

    public void PlayIntroBgm()
    {
        StopAmbience();
        playlist = null;
        PlayTrack(Random(Library?.menuMusic), true);
    }

    public void PlayWorldBgm()
    {
        StopAmbience();
        playlist = null;
        PlayTrack(Random(Library?.campaignMusic), true);
    }

    /// <summary>Her savaşta rastgele bir döngü; sisli bölgede altta ambiyans katmanı.</summary>
    public void PlayBattleBgm()
    {
        lastMusicCategory = "battle";
        StopAmbience();
        playlist = null;
        PlayTrack(Random(Library?.battleMusic), true);
        var fog = Library?.fogAmbience;
        bool foggy = BattleSession.IsCampaign && BattleSession.Region != null
            && BattleSession.Region.specialMechanic == "Sis";
        if (foggy && fog != null && fog.clip != null)
            PlayLayer(layerSource, fog.clip, musicVolume * Mathf.Clamp01(fog.volume) * FogLayerVolume, ref layerBase);
    }

    // ---- Ada müziği (FeatureConfig.islandCalmMusic) ----
    private bool islandRaidRunning;
    private string lastMusicCategory = "";

    /// <summary>Son seçilen müzik kategorisi: "island-calm", "island-battle", "battle", "base"... (test/telemetri).</summary>
    public string MusicCategory => lastMusicCategory;

    private static bool IslandCalmMusicActive => IslandSession.Active && FeatureConfig.Instance.islandCalmMusic;

    private void PlaySceneBattleOrIsland()
    {
        if (IslandCalmMusicActive) { islandRaidRunning = false; PlayIslandBgm(false); }
        else PlayBattleBgm();
    }

    /// <summary>Ada: sakinken Ana Üs listesi + rüzgâr/kuş katmanı; akın sürerken savaş müziği. Geçiş config süresinde çapraz.</summary>
    public void PlayIslandBgm(bool raidRunning)
    {
        islandRaidRunning = raidRunning;
        float fade = Mathf.Max(0.05f, FeatureConfig.Instance.islandMusicCrossfadeSeconds);
        if (raidRunning)
        {
            StopLayers();
            PlayAmbience(Library?.islandAmbience, fade, 0.5f);   // akında %50'ye iner, kesilmez
            playlist = null;
            lastMusicCategory = "island-battle";
            var battle = Random(Library?.battleMusic);
            if (battle != null && currentTrack != null && battle.clip == currentTrack.clip) return;
            PlayTrack(battle, true, fade);
            return;
        }
        lastMusicCategory = "island-calm";
        var tracks = Library?.baseMusic;
        playlist = tracks != null ? System.Array.FindAll(tracks, t => t != null && t.clip != null) : null;
        if (playlist != null && playlist.Length > 0)
        {
            bool alreadyInList = currentTrack != null && System.Array.Exists(playlist, t => t.clip == currentTrack.clip);
            if (!alreadyInList)
            {
                playlistIndex = UnityEngine.Random.Range(0, playlist.Length);
                PlayTrack(playlist[playlistIndex], playlist.Length == 1, fade);
            }
        }
        else PlayTrack(null, true, fade);
        PlayLayer(windSource, Library?.baseWindLoop, 0.22f, ref windBase);
        PlayLayer(birdSource, Library?.baseBirdLoop, 0.14f, ref birdBase);
        PlayAmbience(Library?.islandAmbience, fade);
    }

    /// <summary>Boss sahaya çıkınca boss müziği (atanmışsa).</summary>
    public void PlayBossBgm()
    {
        var boss = Random(Library?.bossMusic);
        if (boss != null) PlayTrack(boss, true);
    }

    /// <summary>Ana Üs: uzun parçalar sırayla çalar, liste bitince başa döner.</summary>
    public void PlayBaseBgm()
    {
        lastMusicCategory = "base";
        var tracks = Library?.baseMusic;
        playlist = tracks != null ? System.Array.FindAll(tracks, t => t != null && t.clip != null) : null;
        if (playlist != null && playlist.Length > 0)
        {
            bool alreadyInList = currentTrack != null && System.Array.Exists(playlist, t => t.clip == currentTrack.clip);
            if (!alreadyInList)
            {
                playlistIndex = UnityEngine.Random.Range(0, playlist.Length);
                PlayTrack(playlist[playlistIndex], playlist.Length == 1);
            }
        }
        else PlayTrack(null, true);
        PlayLayer(windSource, Library?.baseWindLoop, 0.22f, ref windBase);
        PlayLayer(birdSource, Library?.baseBirdLoop, 0.14f, ref birdBase);
        PlayAmbience(Library?.baseAmbience, FadeSeconds);
    }

    private void Update()
    {
        EnforceSingleMusic();
        // Çalma listesi: parça bitince sıradakine geç (döngü kapalıyken kaynak durur).
        if (playlist == null || playlist.Length < 2 || !MusicEnabled || currentTrack == null) return;
        var active = ActiveMusic;
        if (active.clip == currentTrack.clip && !active.loop && !active.isPlaying
            && active.time == 0f && active.timeSamples == 0)
        {
            playlistIndex = (playlistIndex + 1) % playlist.Length;
            currentTrack = null;
            PlayTrack(playlist[playlistIndex], false);
        }
    }

    private void PlayLayer(AudioSource source, AudioClip clip, float baseVolume, ref float store)
    {
        if (source == null || clip == null) return;
        source.DOKill();
        store = baseVolume;
        source.clip = clip;
        source.volume = MusicEnabled ? baseVolume * MusicVolume01 : 0f;
        if (MusicEnabled && !source.isPlaying) source.Play();
    }

    private void StopLayers()
    {
        if (windSource == null) return;
        foreach (var source in new[] { windSource, birdSource, layerSource })
            if (source != null) { source.DOKill(); source.Stop(); }
        windBase = birdBase = layerBase = 0f;
    }

    /// <summary>Katmanları keser, ortam sesini yumuşakça söndürür (Menü/Sefer/Savaş).</summary>
    private void StopAmbience()
    {
        StopLayers();
        PlayAmbience(null, SceneFadeSeconds);
    }

    private float AmbienceVolume(MusicTrack track) =>
        musicVolume * MusicVolume01 * AmbienceLevel * Mathf.Clamp01(track?.volume ?? 1f) * ambienceScale;

    /// <summary>
    /// Ortam sesi: müzik gibi tek hedef klip + A/B çapraz geçiş (gerçek zaman). Aynı klip zaten çalıyorsa yalnız seviye ayarlanır
    /// (yeniden başlatılmaz). Müzik ayarı kapalıysa sessiz.
    /// </summary>
    private void PlayAmbience(MusicTrack track, float fade, float scale = 1f)
    {
        if (ambA == null) return;
        if (track != null && track.clip == null) track = null;
        ambienceScale = scale;
        fade = Mathf.Max(0.05f, fade);
        AudioSource active = ActiveAmbience;
        if (track == null)
        {
            currentAmbience = null;
            FadeOut(ambA, fade);
            FadeOut(ambB, fade);
            return;
        }
        if (currentAmbience != null && currentAmbience.clip == track.clip && active.clip == track.clip)
        {
            currentAmbience = track;
            active.DOKill();
            if (MusicEnabled)
            {
                active.UnPause();
                if (!active.isPlaying) active.Play();
            }
            active.DOFade(MusicEnabled ? AmbienceVolume(track) : 0f, fade).SetUpdate(true);
            return;
        }
        AudioSource to = isAmbAActive ? ambB : ambA;
        active.DOKill();
        to.DOKill();
        to.clip = track.clip;
        to.loop = true;
        to.volume = 0f;
        if (MusicEnabled) to.Play(); else to.Stop();
        FadeOut(active, fade);
        to.DOFade(MusicEnabled ? AmbienceVolume(track) : 0f, fade).SetUpdate(true);
        isAmbAActive = !isAmbAActive;
        currentAmbience = track;
    }

    /// <summary>Ada tanıtımı (IslandIntro): Sea Storm katmanı yumuşakça girer / çıkar. Müzik ayarına bağlı.</summary>
    public void SetStormAmbience(bool on)
    {
        var storm = Library?.stormAmbience;
        if (layerSource == null) return;
        if (!on || storm == null || storm.clip == null)
        {
            if (layerBase <= 0f && !layerSource.isPlaying) return;
            layerBase = 0f;
            layerSource.DOKill();
            layerSource.DOFade(0f, 1f).SetUpdate(true).OnComplete(layerSource.Stop);
            return;
        }
        layerSource.DOKill();
        layerBase = musicVolume * Mathf.Clamp01(storm.volume) * StormLevel;
        layerSource.clip = storm.clip;
        layerSource.loop = true;
        layerSource.volume = 0f;
        if (MusicEnabled) layerSource.Play();
        layerSource.DOFade(MusicEnabled ? layerBase * MusicVolume01 : 0f, 1f).SetUpdate(true);
    }

    /// <summary>Kaydırıcı/anahtar değişince çalan müzik, ortam sesi ve katman seviyelerini günceller.</summary>
    private void RefreshMusicVolumes()
    {
        bool on = MusicEnabled;
        var music = ActiveMusic;
        if (music != null && currentTrack != null && music.clip == currentTrack.clip)
        { music.DOKill(); music.volume = on ? TrackVolume(currentTrack) : 0f; }
        var amb = ActiveAmbience;
        if (amb != null && currentAmbience != null && amb.clip == currentAmbience.clip)
        { amb.DOKill(); amb.volume = on ? AmbienceVolume(currentAmbience) : 0f; }
        if (windSource != null && windSource.isPlaying) windSource.volume = on ? windBase * MusicVolume01 : 0f;
        if (birdSource != null && birdSource.isPlaying) birdSource.volume = on ? birdBase * MusicVolume01 : 0f;
        if (layerSource != null && layerSource.isPlaying) { layerSource.DOKill(); layerSource.volume = on ? layerBase * MusicVolume01 : 0f; }
    }

    // ==================== EFEKTLER ====================

    // Anahtar sırası = SfxMinInterval sırası.
    private const int SfxKeyCount = 14;
    // buttonClick, buildingPlace, goldCollect, levelUp, swordHit, enemyDeath, victory, arrowShoot, defeat, raidHorn,
    // arrowImpact, chop, fireball, waveStart
    private static readonly float[] SfxMinInterval =
        { 0.05f, 0.05f, 0.10f, 0.10f, 0.05f, 0.05f, 0.5f, 0.05f, 0.5f, 1f, 0.05f, 0.10f, 0.20f, 1f };

    private static int SfxIndex(string key)
    {
        switch (key)
        {
            case "buttonClick": return 0;
            case "buildingPlace": return 1;
            case "goldCollect": return 2;
            case "levelUp": return 3;
            case "swordHit": return 4;
            case "enemyDeath": return 5;
            case "victory": return 6;
            case "arrowShoot": return 7;
            case "defeat": return 8;
            case "raidHorn": return 9;
            case "arrowImpact": return 10;
            case "chop": return 11;
            case "fireball": return 12;
            case "waveStart": return 13;
            default: return -1;   // "coin", "click": karşılığı olan ses yok (alakasız sesle doldurulmaz)
        }
    }

    /// <summary>Efekt çal. Aynı efekt asgari aralıktan sık çalmaz, aynı klip en çok 3 eşzamanlı çalar, havuz doluysa en eski kesilir.</summary>
    public void PlaySfx(string key)
    {
        if (!SfxEnabled || effects == null) return;
        int index = SfxIndex(key);
        if (index < 0) return;
        AudioClip clip = ClipFor(index);
        if (clip == null) return;
        float volume = sfxVolume * SfxVolume01;
        if (volume <= 0.0001f) return;
        float now = Time.unscaledTime;
        if (sfxLastAt[index] > 0f && now - sfxLastAt[index] < SfxMinInterval[index]) return;
        int same = 0, free = -1, oldest = 0;
        float oldestAt = float.MaxValue;
        for (int i = 0; i < effects.Length; i++)
        {
            if (now < effectEndAt[i])
            {
                if (effectClip[i] == clip) same++;
                if (effectStartAt[i] < oldestAt) { oldestAt = effectStartAt[i]; oldest = i; }
            }
            else if (free < 0) free = i;
        }
        if (same >= MaxSameClip) return;
        int slot = free >= 0 ? free : oldest;
        var source = effects[slot];
        if (source == null) return;
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.Play();
        effectClip[slot] = clip;
        effectStartAt[slot] = now;
        effectEndAt[slot] = now + clip.length;
        sfxLastAt[index] = now;
        if (index == 13) waveStartIndex++;
        LastSfxClip = clip;
    }

    /// <summary>Şu an çalan (kendi sayacımıza göre) efekt kaynağı sayısı; clip null ise hepsi.</summary>
    public int PlayingSfxCount(AudioClip clip)
    {
        if (effects == null) return 0;
        int count = 0;
        float now = Time.unscaledTime;
        for (int i = 0; i < effects.Length; i++)
            if (now < effectEndAt[i] && (clip == null || effectClip[i] == clip)) count++;
        return count;
    }

    /// <summary>Anahtarın bağlı olduğu klip (waveStart: sıradaki). Kütüphanede yoksa/boşsa null.</summary>
    public static AudioClip ResolveSfx(string key)
    {
        int index = SfxIndex(key);
        return index < 0 ? null : ClipFor(index);
    }

    private static AudioClip ClipFor(int index)
    {
        var library = Library;
        if (library == null) return null;
        switch (index)
        {
            case 0: return library.buttonClick;
            case 1: return library.buildingPlace;
            case 2: return library.goldCollect;
            case 3: return library.levelUp;
            case 4: return library.swordHit;
            case 5: return library.enemyDeath;
            case 6: return library.victory;
            case 7: return library.arrowShoot;
            case 8: return library.defeat;
            case 9: return library.raidHorn;
            case 10: return library.arrowImpact;
            case 11: return library.chop;
            case 12: return library.fireball;
            default:
                var waves = library.waveStart;
                if (waves == null || waves.Length == 0) return null;
                int at = Instance != null ? Instance.waveStartIndex : 0;
                return waves[at % waves.Length];
        }
    }

    // ==================== AYARLAR ====================

    public void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (!enabled)
        {
            musicA.Pause(); musicB.Pause(); ambA.Pause(); ambB.Pause(); StopLayers();
            return;
        }
        var amb = ActiveAmbience;
        if (currentAmbience != null && amb.clip == currentAmbience.clip)
        {
            amb.volume = AmbienceVolume(currentAmbience);
            amb.UnPause();
            if (!amb.isPlaying) amb.Play();
        }
        var active = ActiveMusic;
        if (currentTrack != null && active.clip == currentTrack.clip)
        {
            active.volume = TrackVolume(currentTrack);
            active.UnPause();
            if (!active.isPlaying) active.Play();
        }
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "BaseScene") PlayBaseBgm();
        else if (scene == "BattleScene" && IslandCalmMusicActive) PlayIslandBgm(islandRaidRunning);
        else if (scene == "BattleScene" && currentTrack == null) PlayBattleBgm();
    }

    public void SetSfxEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume01(float value)
    {
        PlayerPrefs.SetFloat("audio_music_volume", Mathf.Clamp01(value));
        RefreshMusicVolumes();
    }

    public void SetSfxVolume01(float value)
    {
        PlayerPrefs.SetFloat("audio_sfx_volume", Mathf.Clamp01(value));
    }
}
