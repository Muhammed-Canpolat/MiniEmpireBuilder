// Ortak arayüz metinleri (iki dilli: L.T). Büyük harf dile göre (L.Upper).
public static class UiStrings
{
    public static string Upper(string value) => L.Upper(value);
    public static string MainBase => L.T("Ana Üs", "Main Base");
    public static string Warrior => L.T("Savaşçı", "Warrior");
    public static string Enemy => L.T("Düşman", "Enemy");
    public static string Build => L.T("İNŞA ET", "BUILD");
    public static string Battle => L.T("SAVAŞA GİT", "TO BATTLE");
    public static string Incoming => L.T("▼ Düşman yaklaşıyor", "▼ Enemies approaching");
    public static string BattleBuildHint => L.T("İnşa için kahramanı sahadaki yuvaya götür.",
        "Walk your hero to a slot on the field to build.");
    public static string IntroStory => L.T(
        "Karanlık bir çağda\nimparatorluğun yıkıldı.\nYıkıntılar arasında\ntek umut: sen.",
        "In a dark age\nyour empire fell.\nAmong the ruins\nthe only hope is you.");
}
