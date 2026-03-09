using UnityEngine;

/// <summary>
/// Gerçek sprite dosyası bulunamadığında otomatik placeholder sprite üretir
/// Her bina/karakter/düşman için tanınabilir piksel-art oluşturur
/// Gerçek sprite'lar eklendiğinde bu sınıf devre dışı kalır
/// </summary>
public static class PlaceholderSpriteGenerator
{
    private const int SIZE = 64; // 64x64 piksel

    /// <summary>
    /// İsme göre uygun placeholder sprite üret
    /// </summary>
    public static Sprite Generate(string spriteName)
    {
        Texture2D tex;

        switch (spriteName)
        {
            // ===== BİNALAR =====
            case "building_mainbase":
                tex = GenerateMainBase();
                break;
            case "building_archertower":
                tex = GenerateArcherTower();
                break;
            case "building_cannontower":
                tex = GenerateCannonTower();
                break;
            case "building_goldmine":
                tex = GenerateGoldMine();
                break;
            case "building_carpenter":
                tex = GenerateWall_Placeholder();
                break;

            // ===== KAHRAMANLAR =====
            case "hero_axe":
                tex = GenerateHero(new Color(0.85f, 0.3f, 0.2f)); // Kırmızı
                break;
            case "hero_spear":
                tex = GenerateHero(new Color(0.3f, 0.6f, 1f)); // Mavi
                break;
            case "hero_bow":
                tex = GenerateHero(new Color(0.2f, 0.8f, 0.3f)); // Yeşil
                break;

            // ===== DÜŞMANLAR =====
            case "enemy_wolf":
                tex = GenerateWolf();
                break;
            case "enemy_zombie":
                tex = GenerateZombie();
                break;
            case "enemy_orc":
                tex = GenerateOrc();
                break;
            case "enemy_troll":
                tex = GenerateTroll();
                break;

            // ===== SAVUNMA =====
            case "defense_fence":
                tex = GenerateWall();
                break;

            // ===== ORTAM =====
            case "env_ground":
                tex = GenerateSolidColor(new Color(0.18f, 0.25f, 0.12f)); // Çim
                break;
            case "env_ground_snow":
                tex = GenerateSolidColor(new Color(0.85f, 0.88f, 0.92f)); // Kar
                break;
            case "env_tree":
                tex = GenerateTree();
                break;
            case "env_tree_snow":
                tex = GenerateTreeSnow();
                break;
            case "env_rock":
                tex = GenerateRock();
                break;

            // ===== FX =====
            case "fx_star":
                tex = GenerateStar();
                break;
            case "fx_circle":
                tex = GenerateCircle(Color.white);
                break;

            // ===== İKONLAR =====
            case "icon_gold":
                tex = GenerateCircle(new Color(1f, 0.85f, 0.2f));
                break;
            case "icon_spear":
                tex = GenerateSolidColor(new Color(0.7f, 0.7f, 0.8f));
                break;
            case "icon_shield":
                tex = GenerateSolidColor(new Color(0.4f, 0.5f, 0.7f));
                break;
            case "icon_heart":
                tex = GenerateCircle(new Color(0.9f, 0.2f, 0.2f));
                break;
            case "ui_buildslot":
                tex = GenerateBuildSlot();
                break;

            default:
                tex = GenerateSolidColor(Color.magenta); // Bilinmeyen
                break;
        }

        tex.filterMode = FilterMode.Point; // Piksel-art keskin görünsün
        tex.wrapMode = TextureWrapMode.Clamp;

        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), SIZE);
    }

    // ==================== BİNA ÜRETİCİLERİ ====================

    private static Texture2D GenerateMainBase()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color stone = new Color(0.55f, 0.50f, 0.42f);
        Color stoneDark = new Color(0.40f, 0.36f, 0.30f);
        Color roof = new Color(0.25f, 0.35f, 0.55f);
        Color door = new Color(0.35f, 0.22f, 0.12f);
        Color flag = new Color(0.85f, 0.15f, 0.1f);
        Color torch = new Color(1f, 0.7f, 0.2f);

        // Kale gövdesi
        FillRect(tex, 12, 4, 40, 36, stone);
        // Kale üst kenar (parlak)
        FillRect(tex, 12, 38, 40, 2, stoneDark);
        // Burçlar (üstte 5 diş)
        for (int i = 0; i < 5; i++)
        {
            FillRect(tex, 14 + i * 8, 40, 5, 6, stone);
            FillRect(tex, 14 + i * 8, 44, 5, 2, stoneDark);
        }
        // Çatı/kulecik
        FillRect(tex, 24, 46, 16, 10, roof);
        FillRect(tex, 28, 56, 8, 4, roof);
        // Bayrak
        FillRect(tex, 31, 60, 2, 4, stoneDark);
        FillRect(tex, 33, 61, 6, 3, flag);
        // Kapı
        FillRect(tex, 26, 4, 12, 16, door);
        FillRect(tex, 28, 4, 8, 14, new Color(0.28f, 0.18f, 0.1f));
        // Kapı kemeri
        FillRect(tex, 27, 18, 10, 2, stoneDark);
        // Pencereler
        FillRect(tex, 16, 24, 4, 5, new Color(0.9f, 0.8f, 0.3f, 0.7f));
        FillRect(tex, 44, 24, 4, 5, new Color(0.9f, 0.8f, 0.3f, 0.7f));
        // Meşaleler
        FillRect(tex, 13, 28, 2, 4, torch);
        FillRect(tex, 49, 28, 2, 4, torch);
        // Taş doku (rastgele çizgiler)
        for (int y = 6; y < 38; y += 4)
        {
            for (int x = 14; x < 50; x += 8)
            {
                FillRect(tex, x, y, 1, 1, stoneDark);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateArcherTower()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color wood = new Color(0.45f, 0.32f, 0.18f);
        Color woodDark = new Color(0.35f, 0.25f, 0.14f);
        Color stoneBase = new Color(0.50f, 0.48f, 0.42f);
        Color roofBlue = new Color(0.2f, 0.35f, 0.65f);
        Color flagBlue = new Color(0.2f, 0.4f, 0.85f);

        // Taş taban
        FillRect(tex, 16, 0, 32, 10, stoneBase);
        // Ahşap gövde (alta doğru geniş, üste doğru dar)
        FillRect(tex, 20, 10, 24, 30, wood);
        FillRect(tex, 22, 8, 20, 4, wood);
        // Dikey ahşap çizgiler
        for (int x = 22; x < 44; x += 4)
        {
            FillRect(tex, x, 10, 1, 30, woodDark);
        }
        // Platform (üstte geniş)
        FillRect(tex, 14, 40, 36, 4, wood);
        FillRect(tex, 12, 42, 40, 2, woodDark);
        // Korkuluk
        for (int i = 0; i < 6; i++)
        {
            FillRect(tex, 14 + i * 7, 44, 2, 8, wood);
        }
        FillRect(tex, 14, 50, 36, 2, wood);
        // Çatı
        FillRect(tex, 20, 52, 24, 6, roofBlue);
        FillRect(tex, 24, 58, 16, 4, roofBlue);
        // Bayrak
        FillRect(tex, 31, 62, 2, 2, woodDark);
        FillRect(tex, 33, 62, 5, 2, flagBlue);
        // Merdiven
        FillRect(tex, 8, 2, 4, 38, woodDark);
        for (int y = 4; y < 38; y += 5)
        {
            FillRect(tex, 6, y, 8, 2, wood);
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateCannonTower()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color stone = new Color(0.48f, 0.44f, 0.38f);
        Color woodDark = new Color(0.38f, 0.28f, 0.16f);
        Color metal = new Color(0.3f, 0.3f, 0.32f);
        Color cannonColor = new Color(0.2f, 0.2f, 0.22f);
        Color flagRed = new Color(0.85f, 0.2f, 0.15f);

        // Taş taban (dairesel efekt)
        FillRect(tex, 14, 0, 36, 8, stone);
        FillRect(tex, 12, 2, 40, 4, stone);
        // Kule gövdesi
        FillRect(tex, 16, 8, 32, 34, stone);
        // Ahşap destekler
        FillRect(tex, 14, 8, 4, 34, woodDark);
        FillRect(tex, 46, 8, 4, 34, woodDark);
        // Üst platform
        FillRect(tex, 12, 42, 40, 4, woodDark);
        // Korkuluk
        FillRect(tex, 12, 46, 40, 2, woodDark);
        for (int i = 0; i < 5; i++)
        {
            FillRect(tex, 14 + i * 8, 46, 3, 6, stone);
        }
        // Top namlusu (sağa bakıyor)
        FillRect(tex, 40, 50, 18, 4, cannonColor);
        FillRect(tex, 56, 49, 4, 6, cannonColor);
        // Top gövdesi
        FillRect(tex, 30, 48, 12, 8, metal);
        // Mühimmat kutusu
        FillRect(tex, 18, 48, 8, 6, woodDark);
        FillRect(tex, 19, 49, 2, 2, cannonColor);
        FillRect(tex, 22, 49, 2, 2, cannonColor);
        // Bayrak
        FillRect(tex, 26, 52, 2, 10, woodDark);
        FillRect(tex, 28, 58, 5, 3, flagRed);

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateGoldMine()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color dirt = new Color(0.35f, 0.28f, 0.18f);
        Color rock = new Color(0.42f, 0.40f, 0.36f);
        Color rockDark = new Color(0.30f, 0.28f, 0.24f);
        Color wood = new Color(0.48f, 0.35f, 0.2f);
        Color gold = new Color(1f, 0.82f, 0.15f);
        Color goldDark = new Color(0.85f, 0.65f, 0.1f);
        Color rail = new Color(0.4f, 0.38f, 0.35f);

        // Toprak zemin
        FillRect(tex, 0, 0, s, 16, dirt);
        // Kaya dağ
        FillRect(tex, 8, 16, 48, 36, rock);
        FillRect(tex, 14, 48, 36, 12, rock);
        FillRect(tex, 20, 56, 24, 8, rockDark);
        // Mağara girişi
        FillRect(tex, 18, 16, 28, 24, rockDark);
        FillRect(tex, 20, 18, 24, 20, new Color(0.12f, 0.1f, 0.08f));
        // Mağara kemeri
        FillRect(tex, 18, 36, 28, 4, rock);
        // Ahşap destek (mağara girişi)
        FillRect(tex, 18, 16, 3, 24, wood);
        FillRect(tex, 43, 16, 3, 24, wood);
        FillRect(tex, 18, 38, 28, 3, wood);
        // Raylar
        FillRect(tex, 6, 6, 30, 2, rail);
        FillRect(tex, 6, 10, 30, 2, rail);
        // Ray traversleri
        for (int x = 8; x < 34; x += 5)
        {
            FillRect(tex, x, 5, 2, 8, wood);
        }
        // Altın parçaları (mağara içi + dış)
        FillRect(tex, 24, 20, 4, 3, gold);
        FillRect(tex, 34, 22, 3, 3, gold);
        FillRect(tex, 28, 16, 3, 2, goldDark);
        FillRect(tex, 10, 2, 5, 4, gold);
        FillRect(tex, 12, 4, 3, 2, goldDark);
        // Tabela
        FillRect(tex, 46, 28, 12, 10, wood);
        FillRect(tex, 50, 18, 3, 12, wood);
        // Meşale
        FillRect(tex, 16, 34, 2, 4, new Color(1f, 0.6f, 0.1f));
        FillRect(tex, 46, 34, 2, 4, new Color(1f, 0.6f, 0.1f));

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateWall_Placeholder()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color wood = new Color(0.50f, 0.36f, 0.2f);
        Color woodLight = new Color(0.6f, 0.45f, 0.25f);
        Color woodDark = new Color(0.35f, 0.25f, 0.14f);
        Color cloth = new Color(0.55f, 0.45f, 0.35f);
        Color skin = new Color(0.75f, 0.6f, 0.45f);
        Color tool = new Color(0.5f, 0.5f, 0.52f);

        // Zemin
        FillRect(tex, 0, 0, s, 8, new Color(0.3f, 0.25f, 0.18f));
        // Tezgah ayakları
        FillRect(tex, 10, 8, 4, 14, woodDark);
        FillRect(tex, 50, 8, 4, 14, woodDark);
        // Tezgah üstü
        FillRect(tex, 6, 22, 52, 6, wood);
        FillRect(tex, 6, 26, 52, 2, woodDark);
        // Tezgah üstündeki ahşap malzemeler
        FillRect(tex, 10, 28, 14, 4, woodLight);
        FillRect(tex, 12, 32, 3, 8, woodLight);
        FillRect(tex, 18, 32, 3, 6, woodLight);
        // Testere/çekiç
        FillRect(tex, 40, 28, 2, 12, tool);
        FillRect(tex, 36, 38, 10, 3, tool);
        // Çatı (basit gölgelik)
        FillRect(tex, 2, 48, 60, 4, cloth);
        FillRect(tex, 0, 46, 4, 24, woodDark);
        FillRect(tex, 60, 46, 4, 24, woodDark);
        // Çatı direkleri
        FillRect(tex, 0, 8, 4, 42, woodDark);
        FillRect(tex, 60, 8, 4, 42, woodDark);
        // Çatı örtüsü (kumaş)
        FillRect(tex, 2, 48, 60, 6, cloth);
        FillRect(tex, 6, 52, 52, 4, new Color(0.50f, 0.40f, 0.30f));
        // Duvarcı (küçük figür)
        FillRect(tex, 28, 28, 8, 10, cloth); // gövde
        FillRect(tex, 30, 38, 4, 4, skin);   // kafa
        // Hazır çitler (yerde)
        for (int i = 0; i < 3; i++)
        {
            FillRect(tex, 8 + i * 10, 10, 6, 10, woodLight);
            FillRect(tex, 9 + i * 10, 18, 1, 4, woodDark); // çiviler
        }
        // Fener
        FillRect(tex, 56, 36, 4, 6, new Color(1f, 0.7f, 0.2f));

        tex.Apply();
        return tex;
    }

    // ==================== KAHRAMAN ====================

    private static Texture2D GenerateHero(Color armorColor)
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color skin = new Color(0.75f, 0.6f, 0.48f);
        Color hair = new Color(0.3f, 0.22f, 0.15f);
        Color boots = new Color(0.35f, 0.25f, 0.15f);
        Color belt = new Color(0.45f, 0.3f, 0.15f);

        // Botlar
        FillRect(tex, 20, 0, 10, 8, boots);
        FillRect(tex, 36, 0, 10, 8, boots);
        // Bacaklar
        FillRect(tex, 22, 8, 8, 14, armorColor * 0.8f);
        FillRect(tex, 36, 8, 8, 14, armorColor * 0.8f);
        // Gövde
        FillRect(tex, 18, 22, 28, 20, armorColor);
        // Kemer
        FillRect(tex, 18, 24, 28, 3, belt);
        // Omuzluklar
        FillRect(tex, 14, 36, 8, 6, armorColor * 1.1f);
        FillRect(tex, 42, 36, 8, 6, armorColor * 1.1f);
        // Kollar
        FillRect(tex, 12, 26, 6, 12, armorColor * 0.9f);
        FillRect(tex, 46, 26, 6, 12, armorColor * 0.9f);
        // Eller
        FillRect(tex, 12, 24, 6, 4, skin);
        FillRect(tex, 46, 24, 6, 4, skin);
        // Boyun
        FillRect(tex, 26, 42, 12, 4, skin);
        // Kafa
        FillRect(tex, 22, 46, 20, 14, skin);
        // Saç
        FillRect(tex, 22, 56, 20, 6, hair);
        FillRect(tex, 20, 52, 4, 10, hair);
        FillRect(tex, 40, 52, 4, 10, hair);
        // Gözler
        FillRect(tex, 26, 52, 3, 2, new Color(0.15f, 0.15f, 0.2f));
        FillRect(tex, 35, 52, 3, 2, new Color(0.15f, 0.15f, 0.2f));
        // Ağız
        FillRect(tex, 29, 48, 6, 1, new Color(0.6f, 0.35f, 0.3f));

        tex.Apply();
        return tex;
    }

    // ==================== DÜŞMANLAR ====================

    private static Texture2D GenerateWolf()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color fur = new Color(0.45f, 0.38f, 0.3f);
        Color furDark = new Color(0.3f, 0.25f, 0.2f);

        // Gövde (yatay, yassı)
        FillRect(tex, 8, 14, 40, 18, fur);
        FillRect(tex, 8, 12, 40, 4, furDark);
        // Kafa
        FillRect(tex, 48, 16, 14, 14, fur);
        // Burun
        FillRect(tex, 58, 18, 6, 6, furDark);
        // Kulaklar
        FillRect(tex, 50, 30, 4, 8, fur);
        FillRect(tex, 56, 30, 4, 8, fur);
        // Göz
        FillRect(tex, 52, 24, 2, 2, new Color(0.9f, 0.3f, 0.1f));
        // Kuyruk
        FillRect(tex, 2, 22, 8, 4, furDark);
        FillRect(tex, 0, 26, 6, 4, fur);
        // Bacaklar
        FillRect(tex, 14, 4, 5, 12, furDark);
        FillRect(tex, 24, 4, 5, 12, furDark);
        FillRect(tex, 34, 4, 5, 12, furDark);
        FillRect(tex, 42, 4, 5, 12, furDark);
        // Pençeler
        FillRect(tex, 13, 2, 7, 3, new Color(0.25f, 0.2f, 0.15f));
        FillRect(tex, 23, 2, 7, 3, new Color(0.25f, 0.2f, 0.15f));
        FillRect(tex, 33, 2, 7, 3, new Color(0.25f, 0.2f, 0.15f));
        FillRect(tex, 41, 2, 7, 3, new Color(0.25f, 0.2f, 0.15f));
        // Dişler
        FillRect(tex, 58, 16, 2, 3, Color.white);
        FillRect(tex, 62, 16, 2, 3, Color.white);

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateZombie()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color skinGreen = new Color(0.4f, 0.55f, 0.3f);
        Color cloth = new Color(0.3f, 0.28f, 0.25f);
        Color blood = new Color(0.5f, 0.15f, 0.1f);

        // Ayaklar
        FillRect(tex, 22, 0, 8, 6, cloth);
        FillRect(tex, 36, 0, 8, 6, cloth);
        // Bacaklar
        FillRect(tex, 22, 6, 8, 12, cloth);
        FillRect(tex, 36, 6, 8, 12, cloth);
        // Gövde
        FillRect(tex, 18, 18, 28, 22, cloth);
        // Yırtıklar
        FillRect(tex, 22, 20, 4, 3, skinGreen);
        FillRect(tex, 38, 28, 5, 4, skinGreen);
        // Kan lekeleri
        FillRect(tex, 30, 22, 3, 3, blood);
        // Kollar (biri uzanmış)
        FillRect(tex, 10, 28, 8, 6, skinGreen);
        FillRect(tex, 4, 30, 8, 4, skinGreen);
        FillRect(tex, 46, 28, 8, 6, skinGreen);
        // Boyun
        FillRect(tex, 26, 40, 12, 4, skinGreen);
        // Kafa
        FillRect(tex, 22, 44, 20, 16, skinGreen);
        // Gözler (parlak)
        FillRect(tex, 26, 52, 3, 3, new Color(0.8f, 0.9f, 0.2f));
        FillRect(tex, 35, 52, 3, 3, new Color(0.8f, 0.9f, 0.2f));
        // Ağız
        FillRect(tex, 28, 46, 8, 2, blood);

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateOrc()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color skin = new Color(0.3f, 0.5f, 0.2f);
        Color armor = new Color(0.4f, 0.35f, 0.25f);
        Color metal = new Color(0.5f, 0.48f, 0.45f);

        // Botlar
        FillRect(tex, 18, 0, 12, 8, new Color(0.3f, 0.22f, 0.14f));
        FillRect(tex, 34, 0, 12, 8, new Color(0.3f, 0.22f, 0.14f));
        // Bacaklar
        FillRect(tex, 20, 8, 10, 14, armor);
        FillRect(tex, 34, 8, 10, 14, armor);
        // Gövde (geniş)
        FillRect(tex, 14, 22, 36, 22, armor);
        // Göğüs zırhı
        FillRect(tex, 20, 28, 24, 12, metal);
        // Kollar (kalın)
        FillRect(tex, 6, 26, 10, 14, skin);
        FillRect(tex, 48, 26, 10, 14, skin);
        // Eller
        FillRect(tex, 4, 24, 10, 5, skin);
        // Boyun
        FillRect(tex, 24, 44, 16, 4, skin);
        // Kafa (büyük)
        FillRect(tex, 18, 48, 28, 14, skin);
        // Gözler
        FillRect(tex, 24, 54, 4, 3, new Color(0.9f, 0.2f, 0.1f));
        FillRect(tex, 36, 54, 4, 3, new Color(0.9f, 0.2f, 0.1f));
        // Dişler
        FillRect(tex, 26, 50, 3, 3, Color.white);
        FillRect(tex, 35, 50, 3, 3, Color.white);
        // Kulaklar (sivri)
        FillRect(tex, 14, 52, 5, 6, skin);
        FillRect(tex, 45, 52, 5, 6, skin);

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateTroll()
    {
        // Trol — daha büyük texture
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color skin = new Color(0.45f, 0.25f, 0.45f);
        Color skinDark = new Color(0.35f, 0.18f, 0.35f);
        Color cloth = new Color(0.3f, 0.25f, 0.2f);
        Color club = new Color(0.4f, 0.3f, 0.2f);

        // Ayaklar
        FillRect(tex, 16, 0, 14, 8, skinDark);
        FillRect(tex, 34, 0, 14, 8, skinDark);
        // Bacaklar
        FillRect(tex, 18, 8, 12, 14, cloth);
        FillRect(tex, 34, 8, 12, 14, cloth);
        // Gövde (devasa)
        FillRect(tex, 10, 22, 44, 24, skin);
        FillRect(tex, 14, 24, 36, 20, cloth);
        // Kollar
        FillRect(tex, 2, 26, 10, 16, skin);
        FillRect(tex, 52, 26, 10, 16, skin);
        // Sopa (solda)
        FillRect(tex, 0, 10, 6, 34, club);
        FillRect(tex, -2, 8, 10, 6, new Color(0.35f, 0.35f, 0.35f)); // Çivili baş
        // Boyun
        FillRect(tex, 22, 46, 20, 4, skin);
        // Kafa
        FillRect(tex, 16, 50, 32, 14, skin);
        // Gözler (kırmızı)
        FillRect(tex, 22, 56, 5, 4, new Color(1f, 0.15f, 0.1f));
        FillRect(tex, 37, 56, 5, 4, new Color(1f, 0.15f, 0.1f));
        // Ağız
        FillRect(tex, 26, 52, 12, 3, skinDark);
        // Dişler
        FillRect(tex, 28, 52, 3, 4, Color.white);
        FillRect(tex, 35, 52, 3, 4, Color.white);
        // Boynuzlar
        FillRect(tex, 18, 60, 4, 4, new Color(0.7f, 0.65f, 0.55f));
        FillRect(tex, 42, 60, 4, 4, new Color(0.7f, 0.65f, 0.55f));

        tex.Apply();
        return tex;
    }

    // ==================== SAVUNMA ====================

    private static Texture2D GenerateWall()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color wood = new Color(0.55f, 0.4f, 0.22f);
        Color woodDark = new Color(0.4f, 0.3f, 0.16f);

        // Yatay tahtalar
        FillRect(tex, 0, 8, s, 6, wood);
        FillRect(tex, 0, 24, s, 6, wood);
        FillRect(tex, 0, 40, s, 6, wood);
        // Dikey direkler
        for (int i = 0; i < 5; i++)
        {
            int x = 4 + i * 14;
            FillRect(tex, x, 0, 5, 54, woodDark);
            // Sivri uç
            FillRect(tex, x + 1, 54, 3, 4, woodDark);
            FillRect(tex, x + 2, 58, 1, 3, woodDark);
        }
        // Çivi detayları
        for (int i = 0; i < 5; i++)
        {
            FillRect(tex, 5 + i * 14, 10, 2, 2, new Color(0.3f, 0.3f, 0.32f));
            FillRect(tex, 5 + i * 14, 26, 2, 2, new Color(0.3f, 0.3f, 0.32f));
        }

        tex.Apply();
        return tex;
    }

    // ==================== ORTAM ====================

    private static Texture2D GenerateTree()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color trunk = new Color(0.4f, 0.28f, 0.15f);
        Color leaf = new Color(0.15f, 0.45f, 0.15f);
        Color leafLight = new Color(0.2f, 0.55f, 0.2f);

        // Gövde
        FillRect(tex, 26, 0, 12, 30, trunk);
        // Yapraklar (katmanlar)
        FillRect(tex, 10, 26, 44, 12, leaf);
        FillRect(tex, 14, 36, 36, 10, leafLight);
        FillRect(tex, 18, 44, 28, 10, leaf);
        FillRect(tex, 22, 52, 20, 8, leafLight);
        FillRect(tex, 26, 58, 12, 6, leaf);

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateTreeSnow()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color trunk = new Color(0.35f, 0.25f, 0.15f);
        Color leaf = new Color(0.12f, 0.30f, 0.15f);
        Color snow = new Color(0.9f, 0.92f, 0.95f);

        FillRect(tex, 26, 0, 12, 30, trunk);
        FillRect(tex, 10, 26, 44, 12, leaf);
        FillRect(tex, 10, 34, 44, 4, snow);
        FillRect(tex, 14, 36, 36, 10, leaf);
        FillRect(tex, 14, 44, 36, 3, snow);
        FillRect(tex, 18, 44, 28, 10, leaf);
        FillRect(tex, 18, 52, 28, 3, snow);
        FillRect(tex, 22, 52, 20, 8, leaf);
        FillRect(tex, 22, 58, 20, 3, snow);
        FillRect(tex, 26, 58, 12, 6, leaf);
        FillRect(tex, 26, 62, 12, 2, snow);

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateRock()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Color rock = new Color(0.45f, 0.43f, 0.40f);
        Color rockDark = new Color(0.35f, 0.33f, 0.30f);
        Color rockLight = new Color(0.55f, 0.53f, 0.50f);

        FillRect(tex, 10, 0, 44, 24, rock);
        FillRect(tex, 6, 8, 52, 18, rock);
        FillRect(tex, 14, 24, 36, 12, rockDark);
        FillRect(tex, 20, 12, 14, 8, rockLight);
        FillRect(tex, 38, 6, 8, 6, rockLight);

        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateStar()
    {
        int s = 16;
        Texture2D tex = CreateTexture(s, s);
        Color c = Color.white;
        FillRect(tex, 6, 0, 4, s, c);
        FillRect(tex, 0, 6, s, 4, c);
        FillRect(tex, 4, 4, 8, 8, c);
        tex.Apply();
        return tex;
    }

    // ==================== TEMEL ÇİZİM METODLARİ ====================

    private static Texture2D CreateTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] clear = new Color[w * h];
        for (int i = 0; i < clear.Length; i++)
            clear[i] = Color.clear;
        tex.SetPixels(clear);
        return tex;
    }

    private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color color)
    {
        for (int px = Mathf.Max(0, x); px < Mathf.Min(tex.width, x + w); px++)
        {
            for (int py = Mathf.Max(0, y); py < Mathf.Min(tex.height, y + h); py++)
            {
                if (color.a < 1f)
                {
                    // Alpha blending
                    Color existing = tex.GetPixel(px, py);
                    Color blended = Color.Lerp(existing, color, color.a);
                    blended.a = Mathf.Max(existing.a, color.a);
                    tex.SetPixel(px, py, blended);
                }
                else
                {
                    tex.SetPixel(px, py, color);
                }
            }
        }
    }

    private static Texture2D GenerateSolidColor(Color color)
    {
        Texture2D tex = CreateTexture(SIZE, SIZE);
        for (int x = 0; x < SIZE; x++)
            for (int y = 0; y < SIZE; y++)
                tex.SetPixel(x, y, color);
        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateCircle(Color color)
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        float center = s / 2f;
        float radius = s / 2f - 2;

        for (int x = 0; x < s; x++)
        {
            for (int y = 0; y < s; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius)
                {
                    tex.SetPixel(x, y, color);
                }
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateBuildSlot()
    {
        int s = SIZE;
        Texture2D tex = CreateTexture(s, s);
        Vector2 center = new Vector2(s / 2f, s / 2f);
        float outer = s * 0.42f;
        float inner = s * 0.30f;

        Color ring = new Color(1f, 1f, 1f, 0.55f);
        Color fill = new Color(0.95f, 0.9f, 0.7f, 0.18f);

        for (int x = 0; x < s; x++)
        {
            for (int y = 0; y < s; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= outer && dist >= inner)
                    tex.SetPixel(x, y, ring);
                else if (dist < inner)
                    tex.SetPixel(x, y, fill);
            }
        }

        tex.Apply();
        return tex;
    }
}
