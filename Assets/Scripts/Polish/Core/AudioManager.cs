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
/// </summary>
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicEnabledKey = "audio_music_enabled";
    private const string SfxEnabledKey = "audio_sfx_enabled";
    private const float FadeSeconds = 1f;
    private const float FogLayerVolume = 0.35f;

    private AudioSource musicA;
    private AudioSource musicB;
    private AudioSource windSource;
    private AudioSource birdSource;
    private AudioSource layerSource;
    private AudioSource[] effects;

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
    public bool MusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
    public bool SfxEnabled => PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;
    private static AudioLibrary Library => AudioLibrary.Instance;

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
        effects = new AudioSource[4];
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
        Instance = null;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        UpdateListener();
        wiredButtons.Clear();
        switch (newScene.name)
        {
            case "BaseScene": PlayBaseBgm(); break;
            case "BattleScene": PlayBattleBgm(); break;
            case "MainMenuScene": PlayIntroBgm(); break;
            default: PlayWorldBgm(); break;
        }
    }

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
    private float TrackVolume(MusicTrack track) => musicVolume * Mathf.Clamp01(track?.volume ?? 1f);

    /// <summary>Parçaya 1 sn'lik çapraz geçiş. loop=false ise parça bitince çalma listesi ilerler.</summary>
    private void PlayTrack(MusicTrack track, bool loop)
    {
        if (track != null && track.clip == null) track = null;
        if (track != null && currentTrack != null && track.clip == currentTrack.clip) return;
        currentTrack = track;
        if (track == null)
        {
            foreach (var source in new[] { musicA, musicB })
            {
                source.DOKill();
                source.DOFade(0f, FadeSeconds).OnComplete(source.Stop);
            }
            return;
        }
        AudioSource from = ActiveMusic;
        AudioSource to = isMusicAActive ? musicB : musicA;
        from.DOKill();
        to.DOKill();
        to.clip = track.clip;
        to.loop = loop;
        to.volume = 0f;
        if (MusicEnabled) to.Play();
        from.DOFade(0f, FadeSeconds).OnComplete(from.Stop);
        to.DOFade(MusicEnabled ? TrackVolume(track) : 0f, FadeSeconds);
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
        StopAmbience();
        playlist = null;
        PlayTrack(Random(Library?.battleMusic), true);
        var fog = Library?.fogAmbience;
        bool foggy = BattleSession.IsCampaign && BattleSession.Region != null
            && BattleSession.Region.specialMechanic == "Sis";
        if (foggy && fog != null && fog.clip != null)
            PlayLayer(layerSource, fog.clip, TrackVolume(fog) * FogLayerVolume);
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
        PlayLayer(windSource, Library?.baseWindLoop, 0.22f);
        PlayLayer(birdSource, Library?.baseBirdLoop, 0.14f);
    }

    private void Update()
    {
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

    private void PlayLayer(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null) return;
        source.clip = clip;
        source.volume = MusicEnabled ? volume : 0f;
        if (MusicEnabled && !source.isPlaying) source.Play();
    }

    private void StopAmbience()
    {
        if (windSource != null) windSource.Stop();
        if (birdSource != null) birdSource.Stop();
        if (layerSource != null) layerSource.Stop();
    }

    // ==================== EFEKTLER ====================

    public void PlaySfx(string key)
    {
        if (!SfxEnabled) return;
        AudioClip clip = ResolveSfx(key);
        if (clip == null || effects == null || effects.Length == 0) return;
        foreach (var source in effects)
            if (source != null && !source.isPlaying) { source.PlayOneShot(clip, sfxVolume); return; }
        effects[0]?.PlayOneShot(clip, sfxVolume);
    }

    private static AudioClip ResolveSfx(string key)
    {
        var library = Library;
        if (library == null) return null;
        switch (key)
        {
            case "buttonClick": return library.buttonClick;
            case "buildingPlace": return library.buildingPlace;
            case "goldCollect": return library.goldCollect;
            case "levelUp": return library.levelUp;
            case "swordHit": return library.swordHit;
            case "enemyDeath": return library.enemyDeath;
            case "victory": return library.victory;
            case "arrowShoot": return library.arrowShoot;
            case "defeat": return library.defeat;
            default: return null;
        }
    }

    // ==================== AYARLAR ====================

    public void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (!enabled)
        {
            musicA.Pause(); musicB.Pause(); StopAmbience();
            return;
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
        else if (scene == "BattleScene" && currentTrack == null) PlayBattleBgm();
    }

    public void SetSfxEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
