using NUnit.Framework;
using static IslandPhaseBTests;

/// <summary>D113: TMP dinamik Türkçe font + fallback zinciri + font atanmamış yazının güvenli fonta geçişi.</summary>
public class TmpFontBootstrapTests
{
    [Test]
    public void TmpFonts_AreSafeForTurkishAndWaveText()
    {
        var args = new object[] { null };
        bool ok = (bool)T("TmpFontBootstrap").GetMethod("SelfCheck").Invoke(null, args);
        Assert.IsTrue(ok, args[0] as string);
    }
}
