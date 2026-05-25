// [MOBILE PERFORMANCE CONFIG] — Mini Empire Builder
using UnityEngine;

public class MobilePerformanceConfig : MonoBehaviour
{
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private int mediumQualityIndex = 1;

    private void Awake()
    {
        Apply();
    }

    /// <summary>
    /// Applies runtime-friendly mobile performance settings.
    /// </summary>
    public void Apply()
    {
        QualitySettings.SetQualityLevel(Mathf.Clamp(mediumQualityIndex, 0, QualitySettings.names.Length - 1), true);
        QualitySettings.shadowDistance = 0f;
        QualitySettings.antiAliasing = 0;
        Application.targetFrameRate = targetFrameRate;

        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
        }

        ParticleSystem[] particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            main.maxParticles = 30;
        }
    }
}
