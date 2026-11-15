using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// 8. oturum (D98): savaş üst barı (BattleTopBar). Oyun koduna yalnız yansıma ile erişilir.
/// </summary>
public class Session8HudTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;

    private static Type T(string name) => Session6Tests.T(name);
    private static object P(object o, string p) => Session6Tests.P(o, p);

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(SaveFile) ? File.ReadAllText(SaveFile) : null;
    }

    [TearDown]
    public void RestoreRealSave()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(SaveFile, savedFile);
        else if (File.Exists(SaveFile)) File.Delete(SaveFile);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    // TMP başvurusu test asmdef'inde yok: TextMeshProUGUI yansıma ile okunur.
    private static Type TmpType => T("TMPro.TextMeshProUGUI");
    private static IEnumerable<Component> Texts(Component bar) => bar.GetComponentsInChildren(TmpType, true);
    private static Component TextNamed(Component bar, string name) => Texts(bar).FirstOrDefault(t => t.name == name);
    private static string Str(Component text) => (string)P(text, "text");
    private static float Num(Component text, string prop) => (float)P(text, prop);

    /// <summary>H1: dört hedef türünde üst bar kurulur; metinler taşmaz, bar güvenli alanda; 4 oranda görüntü.</summary>
    [UnityTest, Timeout(600000)]
    public IEnumerator H1_BattleTopBarShowsCountersObjectiveCardAndFitsSafeArea()
    {
        var cases = new (int region, int level, string kind, bool card)[]
        {
            (4, 3, "mine", true),
            (2, 4, "time", true),
            (1, 2, "base", true),
            (6, 5, "boss", false),
        };
        foreach (var c in cases)
        {
            yield return Session4Tests.StartBattle(c.region, c.level);
            yield return new WaitForSecondsRealtime(1.5f);
            string tag = $"b{c.region}_l{c.level}_{c.kind}";

            var bar = Object.FindFirstObjectByType(T("BattleTopBar")) as Component;
            Assert.NotNull(bar, $"[{tag}] BattleTopBar sahnede olmalı");
            var root = bar.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "BattleTopBarRoot");
            Assert.NotNull(root, $"[{tag}] BattleTopBarRoot bulunamadı");

            var battle = Object.FindFirstObjectByType(T("BattleManager")) as Component;
            Assert.NotNull(battle, $"[{tag}] BattleManager olmalı");
            int remaining = (int)P(battle, "EnemiesRemaining");
            int total = (int)P(battle, "EnemiesTotal");
            var enemy = TextNamed(bar, "EnemyText");
            Assert.NotNull(enemy, $"[{tag}] düşman metni yok");
            Assert.AreEqual($"{remaining} / {total}", Str(enemy), $"[{tag}] düşman sayacı 'kalan / toplam' olmalı");

            var wave = TextNamed(bar, "WaveText");
            Assert.NotNull(wave, $"[{tag}] dalga metni yok");
            StringAssert.Contains($"{(int)P(battle, "CurrentWave")}/{(int)P(battle, "WaveCount")}", Str(wave), $"[{tag}] dalga N/M");
            Assert.NotNull(TextNamed(bar, "RegionText"), $"[{tag}] bölge/seviye metni yok");
            Assert.NotNull(TextNamed(bar, "GoldText"), $"[{tag}] altın metni yok");

            var card = bar.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r => r.name == "ObjectiveCard");
            if (c.card) Assert.NotNull(card, $"[{tag}] hedef kartı olmalı");
            else Assert.IsNull(card, $"[{tag}] bu türde hedef kartı olmamalı (boss çubuğu BattleUI'da)");

            // Eski parçalar tekrar etmesin: eski üst barın çocukları kapalı.
            var oldTop = GameObject.Find("BattleCanvas")?.transform.Find("SafeArea/TopBar");
            if (oldTop != null)
                foreach (Transform child in oldTop)
                    Assert.IsFalse(child.gameObject.activeSelf, $"[{tag}] eski üst bar öğesi gizli olmalı: {child.name}");

            // Metinler taşmaz.
            foreach (var text in Texts(bar))
            {
                if (!text.gameObject.activeInHierarchy) continue;
                TmpType.GetMethod("ForceMeshUpdate", new Type[] { typeof(bool), typeof(bool) })?.Invoke(text, new object[] { false, false });
                float width = ((RectTransform)text.transform).rect.width;
                bool overflowing = (bool)P(text, "isTextOverflowing");
                float preferred = Num(text, "preferredWidth");
                Assert.IsTrue(!overflowing || preferred <= width + 1f,
                    $"[{tag}] metin taşıyor: {text.name} '{Str(text)}' (tercih {preferred:F1} > genişlik {width:F1})");
            }

            // Bar güvenli alanın içinde (Overlay tuvalde dünya = ekran pikseli).
            Rect barRect = WorldRect(root);
            Rect safe = Screen.safeArea;
            Assert.IsTrue(barRect.xMin >= safe.xMin - 0.5f && barRect.xMax <= safe.xMax + 0.5f
                          && barRect.yMin >= safe.yMin - 0.5f && barRect.yMax <= safe.yMax + 0.5f,
                $"[{tag}] bar güvenli alanın dışında: bar {barRect} güvenli {safe}");
            if (card != null)
                Assert.IsTrue(WorldRect(card).yMin >= safe.yMin && WorldRect(card).xMax <= safe.xMax + 0.5f, $"[{tag}] kart güvenli alan dışında");

            foreach (var r in Session4Tests.Ratios)
            {
                string name = $"H1_topbar_r{c.region}_l{c.level}_{r.tag}";
                Session4Tests.CaptureUi(name, r.w, r.h);
                // CaptureUi session4 klasörüne yazar: session8'e taşı.
                string from = Path.Combine(Session4Tests.Folder, name + ".png");
                if (File.Exists(from))
                {
                    string to = Path.Combine(Session8Tests.Folder, name + ".png");
                    if (File.Exists(to)) File.Delete(to);
                    File.Move(from, to);
                }
            }
        }
    }
}
