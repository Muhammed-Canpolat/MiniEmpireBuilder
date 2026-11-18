using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniEmpire.Intro
{
    /// <summary>
    /// Intro sahnesinin ana kontrolcüsü.
    /// Akış: Fade-in -> ana oyun sahnesini arka planda yükle (%0-%100) -> %100 olunca fade-out -> oyun açılır.
    /// </summary>
    public class IntroSceneController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private LoadingBar loadingBar;
        [SerializeField] private CanvasGroup rootGroup; // fade için (tüm intro canvas'ı)

        [Header("Sahne")]
        [SerializeField] private string gameSceneName = "MainMenuScene"; // eski ilk sahne (Build Settings index 1)

        [Header("Süreler (sn)")]
        [SerializeField] private float fadeInTime = 0.6f;
        [SerializeField] private float fadeOutTime = 0.5f;
        [SerializeField] private float minimumShowTime = 2.5f;   // yükleme çok hızlı olsa bile intro bu kadar görünsün
        [SerializeField] private float holdAtFullTime = 0.35f;   // %100'de kısa bekleme

        private void Start()
        {
            // Not: Application.targetFrameRate projede tek yerde (MobilePerformanceConfig) ayarlanır; burada ayarlanmaz.
            StartCoroutine(IntroRoutine());
        }

        /// <summary>Etkinleştirme anı (oyun sahnesi açılırken donma ölçümü için).</summary>
        private static float activationStartedAt = -1f;

        private IEnumerator IntroRoutine()
        {
            yield return Fade(0f, 1f, fadeInTime);

            // Güvenli başlatma: sahne Build Settings'te yoksa ya da async işlem başlatılamazsa doğrudan yedek yükleme.
            AsyncOperation op = null;
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
                Debug.LogError($"[Intro] '{gameSceneName}' Build Settings'te yok; yedek yükleme deneniyor.");
            else
            {
                try { op = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single); }
                catch (System.Exception e) { Debug.LogError($"[Intro] LoadSceneAsync hata: {e}"); op = null; }
            }
            if (op == null) { FallbackLoad(); yield break; }

            op.allowSceneActivation = false;           // %100 olmadan açma
            float startTime = Time.unscaledTime;

            // Unity, allowSceneActivation=false iken progress'i 0.9'da bekletir.
            while (!loadingBar.IsFull || op.progress < 0.9f || Time.unscaledTime - startTime < minimumShowTime)
            {
                float real = Mathf.Clamp01(op.progress / 0.9f);                       // gerçek yükleme
                float time = Mathf.Clamp01((Time.unscaledTime - startTime) / minimumShowTime); // min. süre
                loadingBar.SetTarget(Mathf.Min(real, time));
                yield return null;
            }

            loadingBar.SetTarget(1f);                  // %100
            yield return new WaitForSecondsRealtime(holdAtFullTime);
            yield return Fade(1f, 0f, fadeOutTime);

            activationStartedAt = Time.realtimeSinceStartup;
            SceneManager.sceneLoaded -= LogActivation;
            SceneManager.sceneLoaded += LogActivation;
            op.allowSceneActivation = true;            // oyun açılır; Single mod intro sahnesini bellekten tamamen çıkarır
        }

        /// <summary>Async yol kullanılamazsa intro'da takılı kalmamak için eşzamanlı yükleme.</summary>
        private void FallbackLoad()
        {
            try { SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single); }
            catch (System.Exception e)
            {
                Debug.LogError($"[Intro] Yedek yükleme de başarısız ({gameSceneName}): {e}. Build index 1 deneniyor.");
                if (SceneManager.sceneCountInBuildSettings > 1) SceneManager.LoadScene(1, LoadSceneMode.Single);
            }
        }

        private static void LogActivation(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= LogActivation;
            if (activationStartedAt < 0f) return;
            float ms = (Time.realtimeSinceStartup - activationStartedAt) * 1000f;
            activationStartedAt = -1f;
            Debug.Log($"[Intro] {scene.name} etkinleşme süresi: {ms:0} ms");
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (rootGroup == null || duration <= 0f) { if (rootGroup) rootGroup.alpha = to; yield break; }
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                rootGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            rootGroup.alpha = to;
        }
    }
}
