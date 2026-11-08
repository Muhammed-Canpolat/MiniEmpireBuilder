using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Optional audio slots. Empty clips are silent until art is supplied.</summary>
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicEnabledKey = "audio_music_enabled";
    private const string SfxEnabledKey = "audio_sfx_enabled";

    [Header("Music - assign clips in Inspector")]
    [SerializeField] private AudioClip introBGM;
    [SerializeField] private AudioClip baseBGM;
    [SerializeField] private AudioClip worldBGM;
    [SerializeField] private AudioClip battleBGM;
    [SerializeField] private AudioClip baseWindLoop;
    [SerializeField] private AudioClip baseBirdLoop;

    [Header("Effects - assign clips in Inspector")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField] private AudioClip buildingPlace;
    [SerializeField] private AudioClip goldCollect;
    [SerializeField] private AudioClip levelUp;
    [SerializeField] private AudioClip swordHit;
    [SerializeField] private AudioClip enemyDeath;
    [SerializeField] private AudioClip victory;
    [SerializeField] private AudioClip arrowShoot;
    [SerializeField] private AudioClip bossBGM;
    [SerializeField] private AudioClip defeat;
    private float musicVolume = 0.55f;
    private float sfxVolume = 0.85f;

    [SerializeField] private AudioSource musicA;
    [SerializeField] private AudioSource musicB;
    [SerializeField] private AudioSource windSource;
    [SerializeField] private AudioSource birdSource;
    [SerializeField] private AudioSource[] sfxPool;

    private readonly HashSet<int> wiredButtons = new HashSet<int>();
    private bool isMusicAActive = true;
    private AudioClip currentMusic;
    private AudioListener fallbackListener;
    private AudioSource[] effects;
    private Coroutine buttonScan;
    public static AudioManager Instance { get; private set; }
    public bool MusicEnabled => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
    public bool SfxEnabled => PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        fallbackListener = gameObject.AddComponent<AudioListener>();
        UpdateListener();
        musicA = EnsureSource(musicA, "MusicA", true);
        musicB = EnsureSource(musicB, "MusicB", true);
        windSource = EnsureSource(windSource, "BaseWind", true);
        birdSource = EnsureSource(birdSource, "BaseBirds", true);
        if (sfxPool == null || sfxPool.Length == 0) sfxPool = new AudioSource[4];
        effects = new AudioSource[sfxPool.Length];
        for (int i = 0; i < sfxPool.Length; i++)
            effects[i] = sfxPool[i] = EnsureSource(sfxPool[i], "Effect" + i, false);
        ApplyLibrary();
        SceneManager.activeSceneChanged += OnSceneChanged;
        buttonScan = StartCoroutine(WireUiButtons());
        OnSceneChanged(default, SceneManager.GetActiveScene());
    }

    /// <summary>Resources/AudioLibrary.asset'teki klipleri boş alanlara doldurur.</summary>
    private void ApplyLibrary()
    {
        var library = AudioLibrary.Instance;
        if (library == null) return;
        introBGM = Pick(introBGM, library.menuMusic);
        baseBGM = Pick(baseBGM, library.baseMusic);
        worldBGM = Pick(worldBGM, library.campaignMusic);
        battleBGM = Pick(battleBGM, library.battleMusic);
        bossBGM = Pick(bossBGM, library.bossMusic);
        baseWindLoop = Pick(baseWindLoop, library.baseWindLoop);
        baseBirdLoop = Pick(baseBirdLoop, library.baseBirdLoop);
        buttonClick = Pick(buttonClick, library.buttonClick);
        buildingPlace = Pick(buildingPlace, library.buildingPlace);
        goldCollect = Pick(goldCollect, library.goldCollect);
        levelUp = Pick(levelUp, library.levelUp);
        swordHit = Pick(swordHit, library.swordHit);
        arrowShoot = Pick(arrowShoot, library.arrowShoot);
        enemyDeath = Pick(enemyDeath, library.enemyDeath);
        victory = Pick(victory, library.victory);
        defeat = Pick(defeat, library.defeat);
        musicVolume = library.musicVolume;
        sfxVolume = library.sfxVolume;
        foreach (var source in effects) if (source != null) source.volume = sfxVolume;
    }

    // UnityEngine.Object için ?? güvenilmez (sahte null); açık karşılaştırma.
    private static AudioClip Pick(AudioClip own, AudioClip fromLibrary) => own != null ? own : fromLibrary;

    private AudioSource EnsureSource(AudioSource source, string name, bool loop)
    {
        if (source == null)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
        }
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

    public void PlayBgm(AudioClip clip)
    {
        if (clip == currentMusic) return;
        currentMusic = clip;
        if (clip == null)
        {
            musicA.DOKill();
            musicB.DOKill();
            musicA.DOFade(0f, 1f).OnComplete(musicA.Stop);
            musicB.DOFade(0f, 1f).OnComplete(musicB.Stop);
            return;
        }
        AudioSource from = isMusicAActive ? musicA : musicB;
        AudioSource to = isMusicAActive ? musicB : musicA;
        from.DOKill();
        to.DOKill();
        to.clip = clip;
        to.volume = 0f;
        if (MusicEnabled) to.Play();
        from.DOFade(0f, 1f).OnComplete(from.Stop);
        to.DOFade(MusicEnabled ? musicVolume : 0f, 1f);
        isMusicAActive = !isMusicAActive;
    }

    public void PlayIntroBgm() { StopBaseAmbience(); PlayBgm(introBGM); }
    public void PlayWorldBgm() { StopBaseAmbience(); PlayBgm(worldBGM); }
    public void PlayBattleBgm() { StopBaseAmbience(); PlayBgm(battleBGM); }
    /// <summary>Boss sahaya çıkınca savaş müziği boss müziğine geçer (atanmışsa).</summary>
    public void PlayBossBgm() { if (bossBGM != null) PlayBgm(bossBGM); }
    public void PlayBaseBgm()
    {
        PlayBgm(baseBGM);
        PlayAmbient(windSource, baseWindLoop, 0.22f);
        PlayAmbient(birdSource, baseBirdLoop, 0.14f);
    }

    private void PlayAmbient(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null) return;
        source.clip = clip;
        source.volume = MusicEnabled ? volume : 0f;
        if (MusicEnabled && !source.isPlaying) source.Play();
    }

    private void StopBaseAmbience()
    {
        if (windSource != null) windSource.Stop();
        if (birdSource != null) birdSource.Stop();
    }

    public void PlaySfx(string key)
    {
        if (!SfxEnabled) return;
        AudioClip clip = ResolveSfx(key);
        if (clip == null || effects == null || effects.Length == 0) return;
        foreach (var source in effects)
            if (source != null && !source.isPlaying) { source.PlayOneShot(clip); return; }
        effects[0]?.PlayOneShot(clip);
    }

    public void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        if (!enabled)
        {
            musicA.Pause(); musicB.Pause(); StopBaseAmbience();
        }
        else
        {
            AudioSource active = isMusicAActive ? musicA : musicB;
            if (active.clip == currentMusic && currentMusic != null)
            { active.volume = musicVolume; active.UnPause(); if (!active.isPlaying) active.Play(); }
            if (SceneManager.GetActiveScene().name == "BaseScene") PlayBaseBgm();
        }
    }

    public void SetSfxEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private AudioClip ResolveSfx(string key)
    {
        switch (key)
        {
            case "buttonClick": return buttonClick;
            case "buildingPlace": return buildingPlace;
            case "goldCollect": return goldCollect;
            case "levelUp": return levelUp;
            case "swordHit": return swordHit;
            case "enemyDeath": return enemyDeath;
            case "victory": return victory;
            case "arrowShoot": return arrowShoot;
            case "defeat": return defeat;
            default: return null;
        }
    }
}
