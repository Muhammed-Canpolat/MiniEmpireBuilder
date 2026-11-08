using UnityEngine;

/// <summary>
/// Tüm müzik ve efektlerin tek atama noktası: Assets/Resources/AudioLibrary.asset.
/// AudioManager çalışma anında oluşturulduğu için klipler Inspector'dan ona değil bu varlığa atanır.
/// Boş bırakılan alanlar sessiz kalır; oyun hata vermez.
/// </summary>
[CreateAssetMenu(menuName = "Mini Empire/Audio Library")]
public sealed class AudioLibrary : ScriptableObject
{
    [Header("Müzik (döngülü)")]
    public AudioClip menuMusic;
    public AudioClip baseMusic;
    public AudioClip campaignMusic;
    public AudioClip battleMusic;
    public AudioClip bossMusic;

    [Header("Ana Üs ambiyansı (döngülü)")]
    public AudioClip baseWindLoop;
    public AudioClip baseBirdLoop;

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

    [Header("Ses seviyeleri")]
    [Range(0f, 1f)] public float musicVolume = 0.55f;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;

    private static AudioLibrary instance;
    public static AudioLibrary Instance => instance != null ? instance
        : instance = Resources.Load<AudioLibrary>("AudioLibrary");
}
