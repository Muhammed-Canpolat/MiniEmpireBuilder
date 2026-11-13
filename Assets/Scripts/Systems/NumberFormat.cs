using System.Globalization;

/// <summary>Sayı biçimleri (üst çubuk çipleri): "6345", "12.4K", "1.2M" (AŞAMA 3 §3A).</summary>
public static class NumberFormat
{
    /// <summary>
    /// Kısa biçim. Eşik altı (varsayılan 10 000) olduğu gibi; ≥ eşik "12.4K"; ≥ 1 000 000 "1.2M" (tek ondalık, ".0" atılır).
    /// Eşik FeatureConfig.topBarShortFrom; testte açıkça verilebilir.
    /// </summary>
    public static string Short(long value, long shortFrom = 10000)
    {
        if (value < 0) return "-" + Short(-value, shortFrom);
        if (value < shortFrom && value < 1000000) return value.ToString(CultureInfo.InvariantCulture);
        if (value >= 1000000) return Trim(value / 1000000d) + "M";
        return Trim(value / 1000d) + "K";
    }

    private static string Trim(double v)
    {
        // Aşağı yuvarla (12 999 → "12.9K"; "13.0K" göstermek yanıltır).
        double floor = System.Math.Floor(v * 10d + 1e-9) / 10d;
        string s = floor.ToString("0.0", CultureInfo.InvariantCulture);
        return s.EndsWith(".0") ? s.Substring(0, s.Length - 2) : s;
    }
}
