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

        // Sahne değişikliklerinde de çalışsın (alan yeniden yüklemesi kapalıyken çift abonelik olmasın)
        SceneManager.sceneLoaded -= OnSceneLoadedEvent;
        SceneManager.sceneLoaded += OnSceneLoadedEvent;
    }

    private static void OnSceneLoadedEvent(Scene scene, LoadSceneMode mode)
    {
        // D114: sceneLoaded çoklu yayınında bir dinleyicinin istisnası sonrakileri çağırtmaz; burada yakalanır ve günlüğe yazılır.
        SceneTransition.Guard("SceneAutoSetup " + scene.name, SetupCurrentScene);
    }

    private static void SetupCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        switch (sceneName)
        {
            case "MainMenuScene":
                EnsureComponent<IntroSceneSetup>("IntroSetup");
                EnsureComponent<ScenePolishInstaller>("PolishInstaller");
                EnsureComponent<UiRevampBootstrap>("UiRevampBootstrap");
                break;

            case "BaseScene":
                EnsureComponent<BaseSceneSetup>("BaseSetup");
                EnsureComponent<ScenePolishInstaller>("PolishInstaller");
                EnsureComponent<UiRevampBootstrap>("UiRevampBootstrap");
                break;

            case "BattleScene":
                // Martı Adası: BattleScene ev sahibi sahne; ada oturumunda savaş kurulumu yerine ada kurulur.
                if (IslandSession.Active) EnsureComponent<IslandSceneSetup>("IslandSetup");
                else EnsureComponent<BattleSceneSetup>("BattleSetup");
                EnsureComponent<ScenePolishInstaller>("PolishInstaller");
                EnsureComponent<UiRevampBootstrap>("UiRevampBootstrap");
                break;

            case "Intro":
                // Açılış intro sahnesi (Build index 0): kendi IntroSceneController'ı var; eski giriş (IntroSceneSetup) EKLENMEZ.
                break;

            case "CampaignScene":
                EnsureComponent<CampaignSceneSetup>("CampaignSceneSetup");
                break;

            default:
                // Bilinmeyen sahne — intro olarak davran (ilk açılış için)
                if (Object.FindFirstObjectByType<IntroSceneSetup>() == null &&
                    Object.FindFirstObjectByType<BaseSceneSetup>() == null &&
                    Object.FindFirstObjectByType<BattleSceneSetup>() == null &&
                    Object.FindFirstObjectByType<IslandSceneSetup>() == null &&
                    Object.FindFirstObjectByType<CampaignSceneSetup>() == null)
                {
                    Debug.Log($"[SceneAutoSetup] Bilinmeyen sahne: {sceneName}, IntroSetup ekleniyor...");
                    EnsureComponent<IntroSceneSetup>("IntroSetup");
                    EnsureComponent<ScenePolishInstaller>("PolishInstaller");
                    EnsureComponent<UiRevampBootstrap>("UiRevampBootstrap");
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
