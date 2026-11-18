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
        [SerializeField] private string gameSceneName = "MainMenu"; // Build Settings'e eklenmiş olmalı

        [Header("Süreler (sn)")]
        [SerializeField] private float fadeInTime = 0.6f;
        [SerializeField] private float fadeOutTime = 0.5f;
        [SerializeField] private float minimumShowTime = 2.5f;   // yükleme çok hızlı olsa bile intro bu kadar görünsün
        [SerializeField] private float holdAtFullTime = 0.35f;   // %100'de kısa bekleme

        private void Start()
        {
            Application.targetFrameRate = 60;
            StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine()
        {
            yield return Fade(0f, 1f, fadeInTime);

            AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
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

            op.allowSceneActivation = true;            // oyun açılır
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
