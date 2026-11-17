using System.Text;

namespace MiniEmpire.UI
{
    public enum NumberStyle
    {
        Full,     // 12.345
        Compact,  // 12,3B  (TR)  / 12.3K (EN)
        Auto      // Full when it fits within maxChars, otherwise Compact
    }

    /// <summary>
    /// Formats resource numbers so that 1-digit and 9-digit values fit the same pill.
    /// Compact values are truncated (never rounded up), so the player is never shown more than they have.
    /// Culture-free on purpose (no CultureInfo), so it behaves the same on every device / IL2CPP build.
    /// </summary>
    public static class NumberFormatter
    {
        static readonly string[] SuffixTR = { "", "B", "M", "Mr", "T" };   // bin, milyon, milyar, trilyon
        static readonly string[] SuffixEN = { "", "K", "M", "B", "T" };

        public static string Format(long value, NumberStyle style = NumberStyle.Auto, int maxChars = 5, bool turkish = true)
        {
            string full = Grouped(value, turkish);
            if (style == NumberStyle.Full) return full;
            if (style == NumberStyle.Auto && full.Length <= maxChars) return full;
            return Compact(value, turkish);
        }

        /// <summary>12345 -> "12.345" (TR) / "12,345" (EN)</summary>
        public static string Grouped(long value, bool turkish = true)
        {
            char sep = turkish ? '.' : ',';
            bool neg = value < 0;
            string digits = (neg ? -value : value).ToString();
            var sb = new StringBuilder(digits.Length + digits.Length / 3 + 1);
            if (neg) sb.Append('-');
            int first = digits.Length % 3;
            if (first == 0) first = 3;
            sb.Append(digits, 0, first);
            for (int i = first; i < digits.Length; i += 3) { sb.Append(sep); sb.Append(digits, i, 3); }
            return sb.ToString();
        }

        /// <summary>1250 -> "1,2B" (TR) / "1.2K" (EN); 125000 -> "125B" / "125K"</summary>
        public static string Compact(long value, bool turkish = true)
        {
            var suffix = turkish ? SuffixTR : SuffixEN;
            char dec = turkish ? ',' : '.';
            bool neg = value < 0;
            long v = neg ? -value : value;
            if (v < 1000) return (neg ? "-" : "") + v;

            int tier = 0;
            long div = 1;
            while (v / div >= 1000 && tier < suffix.Length - 1) { div *= 1000; tier++; }
            long whole = v / div;
            long tenth = (v % div) * 10 / div;          // truncated first decimal
            string body = whole >= 100 || tenth == 0 ? whole.ToString() : whole + dec.ToString() + tenth;
            return (neg ? "-" : "") + body + suffix[tier];
        }
    }
}
