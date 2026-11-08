using System.Globalization;

// Shared Turkish labels for the refreshed intro, base, and battle interfaces.
public static class UiStrings
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    public static string Upper(string value) => value?.ToUpper(Turkish) ?? string.Empty;
    public const string MainBase = "Ana Üs";
    public const string Warrior = "Savaşçı";
    public const string Enemy = "Düşman";
    public const string Build = "İNŞA ET";
    public const string Battle = "SAVAŞA GİT";
    public const string Incoming = "▼ Düşman yaklaşıyor";
    public const string BattleBuildHint = "İnşa için kahramanı sahadaki yuvaya götür.";
    public const string IntroStory = "Karanlık bir çağda\nimparatorluğun yıkıldı.\nYıkıntılar arasında\ntek umut: sen.";
}
