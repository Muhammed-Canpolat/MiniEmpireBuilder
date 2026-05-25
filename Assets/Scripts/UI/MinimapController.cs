using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Savaş sahnesinde sağ alt köşede bulunacak minimap için basit bir RawTexture çözümü.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private int mapResolution = 128;
    [SerializeField] private float updateInterval = 0.1f;

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
    [SerializeField] private Color heroColor = Color.cyan;
    [SerializeField] private Color baseColor = Color.yellow;
    [SerializeField] private Color enemyColor = Color.red;

    private Texture2D minimapTexture;
    private Color[] clearPixels;
    private float lastUpdateTime;

    private float worldW;
    private float worldH;
    private Transform heroTransform;
    private Transform baseTransform;
    private BattleManager battleManager;

    public void Setup(RawImage uiImage, float worldWidth, float worldHeight, Transform hero, Transform mainBase)
    {
        minimapImage = uiImage;
        worldW = worldWidth;
        worldH = worldHeight;
        heroTransform = hero;
        baseTransform = mainBase;
        battleManager = FindFirstObjectByType<BattleManager>();

        minimapTexture = new Texture2D(mapResolution, mapResolution, TextureFormat.RGBA32, false);
        minimapTexture.filterMode = FilterMode.Point; // Keskin pikseller
        
        clearPixels = new Color[mapResolution * mapResolution];
        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = backgroundColor;
        }

        if (minimapImage != null)
        {
            minimapImage.texture = minimapTexture;
        }

        UpdateMap();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateMap();
        }
    }

    private void UpdateMap()
    {
        if (minimapTexture == null || minimapImage == null) return;

        // Arka planı temizle
        minimapTexture.SetPixels(clearPixels);

        // Ana Üssü çiz (merkezde, büyük nokta)
        if (baseTransform != null)
        {
            DrawPoint(baseTransform.position, baseColor, 3);
        }

        // Düşmanları çiz
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead)
            {
                DrawPoint(enemy.transform.position, enemyColor, 2);
            }
        }

        // Kahramanı çiz
        if (heroTransform != null)
        {
            DrawPoint(heroTransform.position, heroColor, 3);
        }

        minimapTexture.Apply();
    }

    private void DrawPoint(Vector3 worldPos, Color color, int size = 1)
    {
        // Dünya koordinatını map resolution'a çevir
        // Dünya (0,0) map'in (resolution/2, resolution/2) noktasına denk gelir
        float xPercent = (worldPos.x + worldW / 2f) / worldW;
        float yPercent = (worldPos.y + worldH / 2f) / worldH;

        int px = Mathf.RoundToInt(xPercent * mapResolution);
        int py = Mathf.RoundToInt(yPercent * mapResolution);

        // Noktayı çiz (boyuta göre)
        int halfSize = size / 2;
        for (int x = -halfSize; x <= halfSize; x++)
        {
            for (int y = -halfSize; y <= halfSize; y++)
            {
                int drawX = px + x;
                int drawY = py + y;

                if (drawX >= 0 && drawX < mapResolution && drawY >= 0 && drawY < mapResolution)
                {
                    minimapTexture.SetPixel(drawX, drawY, color);
                }
            }
        }
    }
}
