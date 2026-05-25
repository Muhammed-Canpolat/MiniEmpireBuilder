// [AUDIO MANAGER] — Mini Empire Builder
using DG.Tweening;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const string MusicEnabledKey = "audio_music_enabled";
    private const string SfxEnabledKey = "audio_sfx_enabled";

    [SerializeField] private AudioSource musicA;
    [SerializeField] private AudioSource musicB;
    [SerializeField] private AudioSource[] sfxPool = new AudioSource[4];

    [Header("BGM")]
    [SerializeField] private AudioClip introBGM;
    [SerializeField] private AudioClip worldBGM;
    [SerializeField] private AudioClip battleBGM;

    [Header("SFX")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField] private AudioClip buildingPlace;
    [SerializeField] private AudioClip swordHit;
    [SerializeField] private AudioClip arrowShoot;

    private bool isMusicAActive = true;
    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Cross-fades to selected BGM clip.
    /// </summary>
    public void PlayBgm(AudioClip clip)
    {
        if (!IsMusicEnabled() || clip == null || musicA == null || musicB == null)
            return;

        AudioSource from = isMusicAActive ? musicA : musicB;
        AudioSource to = isMusicAActive ? musicB : musicA;

        to.clip = clip;
        to.volume = 0f;
        to.Play();

        from.DOKill();
        to.DOKill();
        from.DOFade(0f, 1f).OnComplete(from.Stop);
        to.DOFade(1f, 1f);

        isMusicAActive = !isMusicAActive;
    }

    /// <summary>
    /// Plays a one-shot SFX by logical key.
    /// </summary>
    public void PlaySfx(string key)
    {
        if (!IsSfxEnabled())
            return;

        AudioClip clip = ResolveSfx(key);
        if (clip == null)
            return;

        for (int i = 0; i < sfxPool.Length; i++)
        {
            if (sfxPool[i] != null && !sfxPool[i].isPlaying)
            {
                sfxPool[i].PlayOneShot(clip);
                return;
            }
        }

        if (sfxPool.Length > 0 && sfxPool[0] != null)
            sfxPool[0].PlayOneShot(clip);
    }

    /// <summary>
    /// Persists music enabled preference.
    /// </summary>
    public void SetMusicEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(MusicEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Persists SFX enabled preference.
    /// </summary>
    public void SetSfxEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Plays intro music if assigned.
    /// </summary>
    public void PlayIntroBgm() => PlayBgm(introBGM);

    /// <summary>
    /// Plays world map music if assigned.
    /// </summary>
    public void PlayWorldBgm() => PlayBgm(worldBGM);

    /// <summary>
    /// Plays battle music if assigned.
    /// </summary>
    public void PlayBattleBgm() => PlayBgm(battleBGM);

    private bool IsMusicEnabled() => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
    private bool IsSfxEnabled() => PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;

    private AudioClip ResolveSfx(string key)
    {
        switch (key)
        {
            case "buttonClick": return buttonClick;
            case "buildingPlace": return buildingPlace;
            case "swordHit": return swordHit;
            case "arrowShoot": return arrowShoot;
            default: return null;
        }
    }
}
