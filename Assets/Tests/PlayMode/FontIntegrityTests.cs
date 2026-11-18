using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static IslandPhaseBTests;

/// <summary>
/// D124 (UI2 Madde 1+5): TMP yazı kök nedeni regresyonları.
/// - Font atanmamış yazı (dalga kutusu, İnşa Et / Kahraman / Çırak sekmeleri) ilk karede doğru atlasla çizilmeli (ağ/malzeme uyumu),
///   aç-kapa sonrası da öyle kalmalı ("bazen görünüyor" senaryosu).
/// - Gövde fontu her erişimde aynı asset olmalı (D113 sağlık denetimi her erişimde yeni asset üretiyordu).
/// </summary>
public class FontIntegrityTests
{
    private static string Inspect(GameObject root) => (string)T("TmpTextIntegrity").GetMethod("InspectUnder").Invoke(null, new object[] { root });

    [UnityTest]
    public IEnumerator UnassignedText_MeshMatchesMaterial_AcrossToggles()
    {
        var root = (GameObject)T("TmpTextIntegrity").GetMethod("SpawnUnassignedProbe")
            .Invoke(null, new object[] { "DALGA 1 · YAPI DEKOR DÜKKAN DEPO · KAHRAMAN ÇIRAK · İŞÇİ ağaç ğüşıöç", true });
        var text = root.transform.GetChild(0).gameObject;
        string log = "";
        try
        {
            for (int round = 0; round < 4; round++)
            {
                for (int f = 0; f < 3; f++) yield return null;
                string issue = Inspect(root);
                log += $"tur {round}: {(issue ?? "temiz")}\n";
                text.SetActive(false);
                yield return null;
                text.SetActive(true);
            }
            for (int f = 0; f < 3; f++) yield return null;
            string last = Inspect(root);
            log += "son: " + (last ?? "temiz");
            Debug.Log("[FontIntegrity] " + log);
            StringAssert.DoesNotContain("uyumsuz", log, log);
            StringAssert.DoesNotContain("bayat", log, log);
            StringAssert.DoesNotContain("eksik", log, log);
        }
        finally { Object.Destroy(root); }
    }

    [Test]
    public void BodyFont_IsStableAcrossAccesses()
    {
        var prop = T("TmpFontBootstrap").GetProperty("BodyFont");
        var a = prop.GetValue(null) as Object;
        var b = prop.GetValue(null) as Object;
        var c = prop.GetValue(null) as Object;
        Assert.NotNull(a);
        Assert.AreSame(a, b, $"BodyFont ikinci erişimde değişti: {a.GetInstanceID()} → {b.GetInstanceID()}");
        Assert.AreSame(b, c, "BodyFont üçüncü erişimde değişti");
    }
}
