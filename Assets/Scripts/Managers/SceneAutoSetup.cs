using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Otomatik sahne kurulumu — her sahne yüklendiğinde doğru setup script'ini ekler
/// Hiçbir sahneye elle script eklemeye gerek kalmaz!
/// Bu script proje genelinde çalışır (RuntimeInitializeOnLoadMethod)
/// </summary>
public static class SceneAutoSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoaded()
    {
        // İlk yüklemede çalışır
        SetupCurrentScene();

        // Sahne değişikliklerinde de çalışsın
        SceneManager.sceneLoaded += OnSceneLoadedEvent;
    }

    private static void OnSceneLoadedEvent(Scene scene, LoadSceneMode mode)
    {
        SetupCurrentScene();
    }

    private static void SetupCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        switch (sceneName)
        {
            case "MainMenuScene":
                EnsureComponent<IntroSceneSetup>("IntroSetup");
                break;

            case "BaseScene":
                EnsureComponent<BaseSceneSetup>("BaseSetup");
                break;

            case "BattleScene":
                EnsureComponent<BattleSceneSetup>("BattleSetup");
                break;

            default:
                // Bilinmeyen sahne — intro olarak davran (ilk açılış için)
                if (Object.FindFirstObjectByType<IntroSceneSetup>() == null &&
                    Object.FindFirstObjectByType<BaseSceneSetup>() == null &&
                    Object.FindFirstObjectByType<BattleSceneSetup>() == null)
                {
                    Debug.Log($"[SceneAutoSetup] Bilinmeyen sahne: {sceneName}, IntroSetup ekleniyor...");
                    EnsureComponent<IntroSceneSetup>("IntroSetup");
                }
                break;
        }
    }

    private static void EnsureComponent<T>(string objName) where T : MonoBehaviour
    {
        if (Object.FindFirstObjectByType<T>() != null)
        {
            Debug.Log($"[SceneAutoSetup] {typeof(T).Name} zaten mevcut.");
            return;
        }

        GameObject setupObj = new GameObject(objName);
        setupObj.AddComponent<T>();
        Debug.Log($"[SceneAutoSetup] {typeof(T).Name} oluşturuldu.");
    }
}
