using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyun başladığında ilk çalışan script — GameManager'ı oluşturur
/// MainMenuScene'de boş bir GameObject'e eklenir
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    private void Awake()
    {
        // GameManager zaten varsa bir şey yapma
        if (GameManager.Instance != null)
        {
            Debug.Log("[Bootstrapper] GameManager zaten mevcut.");
        }
        else
        {
            // GameManager oluştur
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
            Debug.Log("[Bootstrapper] GameManager oluşturuldu.");
        }

        // SpriteManager oluştur
        if (SpriteManager.Instance == null)
        {
            GameObject smObj = new GameObject("SpriteManager");
            smObj.AddComponent<SpriteManager>();
            Debug.Log("[Bootstrapper] SpriteManager oluşturuldu.");
        }
    }
}
