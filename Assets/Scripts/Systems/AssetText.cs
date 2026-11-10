using System.Collections.Generic;

/// <summary>
/// Oyun verisindeki (bölge, aşama, dekor asset'leri) Türkçe metinlerin İngilizcesi.
/// Asset'ler tek dilde kalır; gösterirken <see cref="L.Asset"/> ile çevrilir. Sözlükte olmayan metin
/// olduğu gibi gösterilir (yeni asset eklenince buraya da eklenmeli).
/// </summary>
public static class AssetText
{
    public static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        // Bölgeler
        { "Yıkık Kale Yolu", "Ruined Castle Road" },
        { "Sisli Orman", "Misty Forest" },
        { "Terk Edilmiş Maden", "Abandoned Mine" },
        { "Donmuş Geçit", "Frozen Pass" },
        { "Karanlık Kale", "Dark Castle" },
        { "Ejderin İni", "Dragon's Lair" },
        { "Sonsuz Sefer", "Endless Campaign" },
        // Bosslar
        { "Kale Muhafızı", "Castle Guardian" },
        { "Orman Cadısı", "Forest Witch" },
        { "Maden Gardiyanı", "Mine Warden" },
        { "Buz Hükümdarı", "Frost Lord" },
        { "Karanlık Kral", "Dark King" },
        { "Ejder Savaşçısı", "Dragon Warrior" },
        // Bölge açıklamaları
        { "Yıkılmış kalenin yolunu temizle. Düşman akınlarını durdurarak üsse güvenli bir geçit aç.",
          "Clear the road to the fallen castle. Stop the enemy raids and open a safe passage to your base." },
        { "Sis görüş alanını daraltır; yaratıklar ağaçların arasından yaklaşır.",
          "The fog narrows your vision; creatures creep in between the trees." },
        { "Terk edilmiş madendeki kaynakları düşman baskınlarından koru.",
          "Protect the abandoned mine's resources from enemy raids." },
        { "Donmuş geçitte soğuk ve dar yollar savunmayı zorlaştırır.",
          "In the frozen pass, cold and narrow roads make defending harder." },
        { "Karanlık kalenin ordusunu yen ve son bossla yüzleş.",
          "Defeat the dark castle's army and face the final boss." },
        { "Volkanın eteklerindeki ejder inine gir; Ejder Savaşçısı'nı ve ordusunu yen.",
          "Enter the dragon's lair at the foot of the volcano; defeat the Dragon Warrior and his army." },
        // Aşama açıklamaları
        { "Dalgalar bitene kadar hayatta kal ve Ana Üs'ü koru.", "Survive until the waves end and protect the Main Base." },
        { "Düşmanları Ana Üs'e ulaşmadan durdur.", "Stop the enemies before they reach the Main Base." },
        { "Madene yönelen düşman akınını püskürt.", "Repel the enemy raid on the mine." },
        { "Süre dolmadan bütün düşmanları temizle.", "Clear all enemies before time runs out." },
        { "Bölgenin bossunu ve korumalarını yen.", "Defeat the region boss and its guards." },
        { "Ejder Savaşçısı inden çıktı: onu ve korumalarını yen.", "The Dragon Warrior has left his lair: defeat him and his guards." },
        // Yıldız kuralları ve değiştiriciler
        { "Zafer kazan", "Win the battle" },
        { "Ana Üs canı %50 üzerinde kalsın", "Keep the Main Base above 50% health" },
        { "Kahraman canı %50 üzerinde kalsın", "Keep the hero above 50% health" },
        { "SİS", "FOG" },
        { "DON", "FROST" },
        // Dekorlar
        { "Örs", "Anvil" },
        { "Fıçı", "Barrel" },
        { "Çalı", "Bush" },
        { "Kamp ateşi", "Campfire" },
        { "Sandık", "Chest" },
        { "Sancak", "Banner" },
        { "Ayçiçeği", "Sunflower" },
        { "Kuyu", "Well" },
        { "Saman balyası", "Hay bale" },
        { "Fener", "Lantern" },
        { "Kaya", "Rock" },
        { "Korkuluk", "Scarecrow" },
        { "Koyun", "Sheep" },
        { "Heykel", "Statue" },
        { "Patika", "Path" },
        { "Ağaç", "Tree" },
        { "Odun yığını", "Log pile" },
    };
}
