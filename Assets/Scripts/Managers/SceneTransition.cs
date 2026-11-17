using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 11. oturum (D114, bug 4/6/15/27): sahne geçişlerinin ORTAK güvenlik katmanı. Hem kararmalı yükleme
/// (<c>GameManager.FadeAndLoadScene</c>) hem bulut geçişi (<see cref="SceneCloudTransition"/>) bunu kullanır:
/// - <see cref="Busy"/>: aynı anda tek yükleme (çift dokunuş / bulut + kararma yarışı ikinci bir yüklemeyi başlatmaz).
/// - <see cref="PrepareForLoad"/>: eski sahne yıkılmadan hemen önce duraklatma, zaman ölçeği, ses dinleyicisi ve müzik
///   kaynakları normale döner, DOTween temizlenir. Her adım ayrı try/catch: biri hata verse de geçiş sürer.
/// - Tüm hatalar "[SceneLoad]" önekiyle günlüğe yazılır; yakalanan hata oyunu düşürmez.
/// - İz (breadcrumb): geçiş başlarken PlayerPrefs'e yazılır, bitince silinir. Uygulama geçiş sırasında kapanırsa (yerel
///   çökme / bellek) sonraki açılışta günlüğe "[SceneLoad] önceki oturum ... geçişinde kapandı" yazılır (TestFlight tanısı).
/// - Düşük bellek uyarısında kullanılmayan varlıklar boşaltılır (iOS bellek baskısı).
/// </summary>
public static class SceneTransition
{
    public const string LogTag = "[SceneLoad]";
    private const string BreadcrumbKey = "scene_load_breadcrumb";
    private const float StaleSeconds = 20f;     // takılı kalmış bir geçiş sonraki yüklemeleri sonsuza dek kilitlemesin

    private static float busySince = -1f;
    private static string busyScene;
    private static float lastLowMemoryUnload = -100f;

    /// <summary>Bir sahne yüklemesi sürüyor mu (kararma/bulut kapanışı dahil). 20 sn'den eski kilit geçersiz sayılır.</summary>
    public static bool Busy => busySince >= 0f && Time.realtimeSinceStartup - busySince < StaleSeconds;
    public static string BusyScene => Busy ? busyScene : null;

    // ---- Test/telemetri ----
    public static int ErrorCount { get; private set; }
    public static string LastError { get; private set; }
    public static int IgnoredRequests { get; private set; }
    public static int CompletedLoads { get; private set; }
    /// <summary>Önceki oturum bir geçiş ortasında kapandıysa o geçişin izi (yoksa null).</summary>
    public static string PreviousSessionBreadcrumb { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        busySince = -1f;
        busyScene = null;
        try
        {
            if (PlayerPrefs.HasKey(BreadcrumbKey))
            {
                PreviousSessionBreadcrumb = PlayerPrefs.GetString(BreadcrumbKey);
                Debug.LogWarning($"{LogTag} Önceki oturum sahne geçişi sırasında kapandı: {PreviousSessionBreadcrumb}");
                PlayerPrefs.DeleteKey(BreadcrumbKey);
                PlayerPrefs.Save();
            }
        }
        catch (Exception e) { Debug.LogWarning($"{LogTag} iz okunamadı: {e.Message}"); }
        Application.lowMemory -= OnLowMemory;
        Application.lowMemory += OnLowMemory;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Tek sahne yüklendi: istenen yükleme etkinleşti (ya da başka bir yol sahneyi değiştirdi). Kilit burada da kalkar;
    /// yükleyen coroutine'in sahibi yok edilse bile (ör. GameManager) sonraki geçişler kilitli kalmaz.
    /// </summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !Busy) return;
        busySince = -1f;
        busyScene = null;
    }

    /// <summary>
    /// Yükleme isteğini kaydeder. Başka bir yükleme sürüyorsa false (çağıran isteği bırakır: çifte yükleme yok).
    /// </summary>
    public static bool TryBegin(string sceneName)
    {
        if (Busy)
        {
            IgnoredRequests++;
            Debug.LogWarning($"{LogTag} '{sceneName}' isteği yok sayıldı: '{busyScene}' yüklemesi sürüyor.");
            return false;
        }
        busySince = Time.realtimeSinceStartup;
        busyScene = sceneName;
        Guard("iz yaz", () =>
        {
            PlayerPrefs.SetString(BreadcrumbKey, $"{SceneManager.GetActiveScene().name} -> {sceneName} @ {DateTime.UtcNow:O}");
            PlayerPrefs.Save();
        });
        Debug.Log($"{LogTag} {SceneManager.GetActiveScene().name} -> {sceneName} başladı.");
        return true;
    }

    /// <summary>Yükleme bitti (ya da iptal edildi): kilit kalkar, iz silinir.</summary>
    public static void End(string sceneName, bool loaded)
    {
        if (loaded) CompletedLoads++;
        if (Busy && busyScene != sceneName) return;   // bu arada yeni bir geçiş başladı: onun kilidi ve izi kalır
        busySince = -1f;
        busyScene = null;
        Guard("iz sil", () => { PlayerPrefs.DeleteKey(BreadcrumbKey); PlayerPrefs.Save(); });
        Debug.Log($"{LogTag} {sceneName} {(loaded ? "yüklendi" : "iptal edildi")}.");
    }

    /// <summary>
    /// Eski sahne yıkılmadan HEMEN önce: savaş duraklatması kalkar, timeScale 0 ise 1 olur, ses dinleyicisi (arka planda değilse)
    /// açılır, müzik kaynaklarının yarım kalmış solmaları sabitlenir (KillAll onları yarıda öldürmesin: bug 27), sonra DOTween temizlenir.
    /// Hiçbir adım istisna fırlatmaz.
    /// </summary>
    public static void PrepareForLoad(string sceneName)
    {
        Guard("BattlePause.ResetAll", BattlePause.ResetAll);
        Guard("timeScale", () => { if (Time.timeScale <= 0f) Time.timeScale = 1f; });
        Guard("AudioListener.pause", () =>
            AudioListener.pause = AudioManager.Instance != null && AudioManager.Instance.InBackground);
        Guard("AudioManager.PrepareSceneChange", () => AudioManager.Instance?.PrepareSceneChange());
        Guard("DOTween.KillAll", () => DG.Tweening.DOTween.KillAll());
    }

    /// <summary>Adımı çalıştırır; istisnayı "[SceneLoad]" ile günlüğe yazar ve yutar. Başarılıysa true.</summary>
    public static bool Guard(string step, Action action)
    {
        try { action?.Invoke(); return true; }
        catch (Exception e) { Report(step, e); return false; }
    }

    public static void Report(string step, Exception e)
    {
        ErrorCount++;
        LastError = step + ": " + e.Message;
        Debug.LogError($"{LogTag} {step} hata verdi (geçiş sürüyor): {e}");
    }

    private static void OnLowMemory()
    {
        // iOS/Android bellek uyarısı: sahnede kullanılmayan varlıkları boşalt (en çok 10 sn'de bir; yükleme sırasında değil).
        if (Busy || Time.realtimeSinceStartup - lastLowMemoryUnload < 10f) return;
        lastLowMemoryUnload = Time.realtimeSinceStartup;
        Debug.LogWarning("[Memory] Düşük bellek uyarısı: kullanılmayan varlıklar boşaltılıyor.");
        Guard("UnloadUnusedAssets", () => { Resources.UnloadUnusedAssets(); GC.Collect(); });
    }
}
