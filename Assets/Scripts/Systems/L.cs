using System;
using System.Globalization;
using UnityEngine;

public enum GameLanguage { Turkish, English }

/// <summary>
/// Dil desteği (Türkçe / İngilizce). Metinler kodda iki dilli yazılır: <c>L.T("Altın", "Gold")</c>.
/// Ayrı anahtar dosyası yok: çeviri metnin hemen yanında durur, unutulamaz ya da kayamaz.
/// İlk açılışta telefonun dili seçilir (Türkçe değilse İngilizce); Ayarlar'dan değiştirilir ve
/// arayüz kodla kurulduğundan sahne baştan yüklenir.
/// </summary>
public static class L
{
    private const string PrefKey = "MiniEmpireBuilder_Language";
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static GameLanguage? current;

    public static event Action OnChanged;

    public static GameLanguage Current
    {
        get
        {
            if (current == null)
            {
                int saved = PlayerPrefs.GetInt(PrefKey, -1);
                current = saved >= 0 ? (GameLanguage)saved
                    : Application.systemLanguage == SystemLanguage.Turkish ? GameLanguage.Turkish : GameLanguage.English;
            }
            return current.Value;
        }
    }

    public static bool English => Current == GameLanguage.English;

    /// <summary>Geçerli dildeki metin. İngilizce boş bırakılmışsa Türkçesi gösterilir.</summary>
    public static string T(string turkish, string english) =>
        English && !string.IsNullOrEmpty(english) ? english : turkish;

    /// <summary>
    /// Oyun verisinden (asset) gelen Türkçe metin: İngilizcede <see cref="AssetText"/> sözlüğünden çevrilir,
    /// sözlükte yoksa olduğu gibi kalır.
    /// </summary>
    public static string Asset(string turkish)
    {
        if (!English || string.IsNullOrEmpty(turkish)) return turkish;
        return AssetText.English.TryGetValue(turkish.Trim(), out var english) ? english : turkish;
    }

    /// <summary>Dile uygun büyük harf (Türkçe: i → İ; İngilizce: i → I).</summary>
    public static string Upper(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return English ? value.ToUpperInvariant() : value.ToUpper(TurkishCulture);
    }

    public static void Set(GameLanguage language)
    {
        if (current == language) return;
        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>Testler için: kayıtlı tercihi yok sayıp dili geçici olarak ayarlar.</summary>
    public static void SetForTests(GameLanguage? language) => current = language;
}
