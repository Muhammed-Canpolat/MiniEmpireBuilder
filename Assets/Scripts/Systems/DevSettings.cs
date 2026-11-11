using UnityEngine;

/// <summary>
/// Bütün test kolaylıklarının tek anahtarı. Yalnız editörde ve Development derlemesinde var;
/// yayın derlemesinde <see cref="Available"/> derleme anında false olur ve hiçbir kolaylık çalışmaz.
/// Ayarlar cihazda PlayerPrefs'te tutulur (kayıt dosyasına girmez). DEV menüsünden açılıp kapatılır.
/// Playtest/bot ölçümleri her zaman normal ekonomiyle yapılır (sınırsız elmas kapalı).
/// </summary>
public static class DevSettings
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const bool Available = true;
#else
    public const bool Available = false;
#endif

    private const string Prefix = "MiniEmpireBuilder_Dev_";

    private static bool Get(string key, bool fallback)
    {
        if (!Available) return false;
        return PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;
    }

    private static void Set(string key, bool value)
    {
        if (!Available) return;
        PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Ana anahtar: kapalıysa hiçbir test kolaylığı çalışmaz (DEV düğmesi yine görünür, geri açmak için).</summary>
    public static bool Enabled { get => Get("Enabled", true); set => Set("Enabled", value); }

    /// <summary>Elmas harcanırken düşmez (sayı değişmez); fiyat akışları test edilebilir.</summary>
    public static bool UnlimitedGems { get => Enabled && Get("UnlimitedGems", false); set => Set("UnlimitedGems", value); }

    /// <summary>Elmas sayacına / dükkândaki elmas hapına 5 hızlı dokunuş = +100 elmas.</summary>
    public static bool GemTapShortcut { get => Enabled && Get("GemTap", true); set => Set("GemTap", value); }

    /// <summary>Ana Üs hata ayıklama katmanı (ızgara, sınırlar).</summary>
    public static bool DebugOverlay { get => Enabled && Get("DebugOverlay", true); set => Set("DebugOverlay", value); }
}
