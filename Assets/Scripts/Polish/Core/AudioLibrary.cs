using System;
using UnityEngine;

/// <summary>Bir müzik parçası ve onu diğerleriyle aynı algılanan seviyeye getiren ses çarpanı.</summary>
[Serializable]
public sealed class MusicTrack
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

/// <summary>
/// Tüm müzik ve efektlerin tek atama noktası: Assets/Resources/AudioLibrary.asset.
/// AudioManager çalışma anında oluşturulduğu için klipler Inspector'dan ona değil bu varlığa atanır.
/// Boş bırakılan alanlar sessiz kalır; oyun hata vermez.
/// Parça başına "volume", parçalar arasındaki ses yüksekliği farkını dengeler (ölçülmüş RMS'e göre).
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire/Audio Library")]
public sealed class AudioLibrary : ScriptableObject
{
    [Header("Müzik")]
    public MusicTrack[] menuMusic;
    [Tooltip("Sırayla çalınır; biri bitince diğerine geçer (uzun parçalar için).")]
    public MusicTrack[] baseMusic;
    public MusicTrack[] campaignMusic;
    [Tooltip("Her savaşta rastgele biri döngüde çalar.")]
    public MusicTrack[] battleMusic;
    [Tooltip("Boss sahaya çıkınca rastgele biri.")]
    public MusicTrack[] bossMusic;
    [Tooltip("'Sis' mekaniği olan bölgede savaş müziğinin altında hafif katman.")]
    public MusicTrack fogAmbience;

    [Header("Ana Üs ambiyansı (döngülü)")]
    public AudioClip baseWindLoop;
    public AudioClip baseBirdLoop;
    [Tooltip("Ana Üs ortam sesi (Inside Day). Müzik ayarına bağlı, müziğin altında çalar.")]
    public MusicTrack baseAmbience;
    [Tooltip("Ada ortam sesi (Beach). Akın sürerken %50'ye iner, kesilmez.")]
    public MusicTrack islandAmbience;
    [Tooltip("Yalnız ada tanıtımında (Sea Storm).")]
    public MusicTrack stormAmbience;

    [Header("Efektler")]
    public AudioClip buttonClick;
    public AudioClip buildingPlace;
    public AudioClip goldCollect;
    public AudioClip levelUp;
    public AudioClip swordHit;
    public AudioClip arrowShoot;
    public AudioClip enemyDeath;
    public AudioClip victory;
    public AudioClip defeat;
    [Tooltip("Ada baskını boru sesi (Faz D). Boşsa sessiz.")]
    public AudioClip raidHorn;
    [Tooltip("Ok/mızrak hedefe isabet edince (Bow Impact Hit 1).")]
    public AudioClip arrowImpact;
    [Tooltip("Ağaç/kütük kesme (chop 2).")]
    public AudioClip chop;
    [Tooltip("Ateşli Ok yeteneği (Fireball 1).")]
    public AudioClip fireball;
    [Tooltip("Düşman dalgası başlangıcı; sırayla biri çalınır (Wave Attack 1/2).")]
    public AudioClip[] waveStart;

    [Header("Genel ses seviyeleri")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;

    private static AudioLibrary instance;
    public static AudioLibrary Instance => instance != null ? instance
        : instance = Resources.Load<AudioLibrary>("AudioLibrary");
}
