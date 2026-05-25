using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tüm sprite'ları merkezi olarak yöneten Singleton
/// Resources/Sprites klasöründen sprite'ları yükler
/// Eğer sprite bulamazsa otomatik placeholder (geçici) sprite üretir
/// </summary>
public class SpriteManager : MonoBehaviour
{
    public static SpriteManager Instance { get; private set; }

    // Cache — her sprite sadece bir kez yüklenir
    private Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private Dictionary<string, RuntimeAnimatorController> controllerCache = new Dictionary<string, RuntimeAnimatorController>();

    // ==================== SPRITE TANIMLARI ====================

    // Binalar — Resources/Sprites altındaki dosyalar
    public Sprite MainBase    => GetSprite("building_mainbase");    // Black Buildings/Castle.png
    public Sprite ArcherTower => GetSprite("building_archertower"); // Blue Buildings/Archery.png
    public Sprite CannonTower => GetSprite("building_cannontower"); // Blue Buildings/Tower.png
    public Sprite GoldMine    => GetSprite("building_goldmine");    // Yellow Buildings/House3.png
    
    // Kahramanlar (Base sahnesi için — tek kare idle)
    public Sprite HeroAxe   => GetSprite("hero_axe");   // Warrior idle
    public Sprite HeroSpear => GetSprite("hero_spear"); // Lancer idle
    public Sprite HeroBow   => GetSprite("hero_bow");   // Archer idle

    // Düşmanlar
    public Sprite EnemyWolf   => GetSprite("enemy_wolf");
    public Sprite EnemyZombie => GetSprite("enemy_zombie");
    public Sprite EnemyOrc    => GetSprite("enemy_orc");
    public Sprite EnemyTroll  => GetSprite("enemy_troll");

    // Savunma
    public Sprite WallBuilder  => GetSprite("building_carpenter"); // Blue Buildings/House1.png
    public Sprite WallSegment  => GetSprite("defense_fence");

    // Ortam
    public Sprite Ground     => GetSprite("env_ground");
    public Sprite GroundSnow => GetSprite("env_ground_snow");
    public Sprite Tree       => GetSprite("env_tree");
    public Sprite TreeSnow   => GetSprite("env_tree_snow");
    public Sprite Rock       => GetSprite("env_rock");

    // UI İkonları
    public Sprite IconGold    => GetSprite("icon_gold");
    public Sprite IconSpear   => GetSprite("icon_spear");
    public Sprite IconShield  => GetSprite("icon_shield");
    public Sprite IconHeart   => GetSprite("icon_heart");
    public Sprite BuildSlot   => GetSprite("ui_buildslot");

    // Efektler
    public Sprite ParticleStar   => GetSprite("fx_star");
    public Sprite ParticleCircle => GetSprite("fx_circle");

    // ==================== UNITY YAŞAM DÖNGÜSÜ ====================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[SpriteManager] Başlatıldı.");
    }

    // ==================== SPRİTE ERİŞİM ====================

    /// <summary>
    /// İsme göre sprite getir — önce cache'e, sonra Resources'a bakar
    /// Spritesheet ise ilk kareyi otomatik çıkarır
    /// Bulamazsa otomatik placeholder üretir
    /// </summary>
    public Sprite GetSprite(string spriteName)
    {
        if (spriteCache.ContainsKey(spriteName) && spriteCache[spriteName] != null)
            return spriteCache[spriteName];

        // Resources/Sprites klasöründen yükle
        Sprite[] allLoaded = Resources.LoadAll<Sprite>($"Sprites/{spriteName}");
        Sprite loaded = (allLoaded != null && allLoaded.Length > 0) ? allLoaded[0] : null;
        if (loaded != null)
        {
            // Spritesheet kontrolü — yatay şerit ise ilk kareyi çıkar
            Texture2D tex = loaded.texture;
            if (tex.width > tex.height * 2)
            {
                int frameSize = tex.height;
                loaded = Sprite.Create(tex,
                    new Rect(0, 0, frameSize, frameSize),
                    new Vector2(0.5f, 0.5f), 100f);
                Debug.Log($"[SpriteManager] Spritesheet → ilk kare çıkarıldı: {spriteName} ({frameSize}x{frameSize})");
            }
            else
            {
                Debug.Log($"[SpriteManager] Sprite yüklendi: {spriteName}");
            }

            spriteCache[spriteName] = loaded;
            return loaded;
        }

        // Bulamadı — placeholder üret
        Sprite placeholder = PlaceholderSpriteGenerator.Generate(spriteName);
        spriteCache[spriteName] = placeholder;
        Debug.LogWarning($"[SpriteManager] Sprite bulunamadı: '{spriteName}' → Placeholder kullanılıyor");
        return placeholder;
    }

    /// <summary>
    /// Bina tipine göre doğru sprite'ı getir
    /// </summary>
    public Sprite GetBuildingSprite(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.MainBase:    return MainBase;
            case BuildingType.ArcherTower: return ArcherTower;
            case BuildingType.CannonTower: return CannonTower;
            case BuildingType.GoldMine:    return GoldMine;
            case BuildingType.WallBuilder: return WallBuilder;
            default: return MainBase;
        }
    }

    /// <summary>
    /// Silah tipine göre kahraman idle sprite'ı getir (Base sahnesi için)
    /// </summary>
    public Sprite GetHeroSprite(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Axe:   return HeroAxe;    // Warrior
            case WeaponType.Spear: return HeroSpear;  // Lancer
            case WeaponType.Bow:   return HeroBow;    // Archer
            default: return HeroSpear;
        }
    }

    /// <summary>
    /// Silah tipine göre Unity AnimatorController yükle.
    /// Dosya Resources/ altında değil, direkt Assets/ altında. AssetDatabase yerine
    /// runtime'da yükleyebilmek için bu controller'lar Resources altına kopyalanmalı;
    /// alternatif olarak BattleSceneSetup Animator'ı kod üzerinden atar.
    /// </summary>
    public RuntimeAnimatorController GetHeroAnimatorController(WeaponType weapon)
    {
        string cacheKey = $"animator_{weapon}";
        if (controllerCache.ContainsKey(cacheKey) && controllerCache[cacheKey] != null)
            return controllerCache[cacheKey];

        // Resources altına kopyalanmış ise yükle
        string resourcePath;
        switch (weapon)
        {
            case WeaponType.Axe:   resourcePath = "Animators/Warrior_Black"; break;
            case WeaponType.Bow:   resourcePath = "Animators/Archer_Black";  break;
            case WeaponType.Spear: resourcePath = "Animators/Lancer_Black";  break;
            default:               resourcePath = "Animators/Warrior_Black"; break;
        }

        var controller = Resources.Load<RuntimeAnimatorController>(resourcePath);
        if (controller != null)
        {
            controllerCache[cacheKey] = controller;
            Debug.Log($"[SpriteManager] Animator yüklendi: {resourcePath}");
        }
        else
        {
            Debug.LogWarning($"[SpriteManager] Animator bulunamadı: {resourcePath}. AnimatorOverrideController veya doğrudan atama gerekiyor.");
        }

        return controller;
    }

    /// <summary>
    /// Düşman tipine göre sprite getir
    /// </summary>
    public Sprite GetEnemySprite(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Wolf:   return EnemyWolf;
            case EnemyType.Zombie: return EnemyZombie;
            case EnemyType.Orc:    return EnemyOrc;
            case EnemyType.Troll:  return EnemyTroll;
            default: return EnemyWolf;
        }
    }

    /// <summary>
    /// Cache'i temizle (sahne değişiminde gerekirse)
    /// </summary>
    public void ClearCache()
    {
        spriteCache.Clear();
        controllerCache.Clear();
        Debug.Log("[SpriteManager] Cache temizlendi.");
    }
}
