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
/// 11. oturum, D grubu (D115): konum kalıcılığı. Madde 31 (dekor/arazi her girişte aynı yerde ve görünümde), 33 (yeni inşaatçı kulübesi
/// öncekinin dibine kurulmaz), 19/29 (okçu kaptan akın dışında Okçuluk Alanı çevresinde gezinir, akında eskisi gibi P1'de),
/// 18 (dokunulan dekor seçilir ve eylem menüsü onun görünür sınırına bağlanır). Yalıtılmış test kaydı.
/// </summary>
public class Session11PositionTests
{
    private static readonly string[] SaveKeys =
        { "MiniEmpireBuilder_SaveData", "MiniEmpireBuilder_SaveData_Backup", "MiniEmpireBuilder_SaveData_Corrupt" };
    private string[] savedPrefs;
    private string savedFile;
    private bool devWas;
    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

    [SetUp]
    public void BackupSave()
    {
        savedPrefs = SaveKeys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
        savedFile = File.Exists(SaveFilePath) ? File.ReadAllText(SaveFilePath) : null;
        devWas = (bool)T("DevSettings").GetProperty("Enabled").GetValue(null);
    }

    [TearDown]
    public void RestoreSave()
    {
        Time.timeScale = 1f;
        for (int i = 0; i < SaveKeys.Length; i++)
        {
            if (savedPrefs[i] == null) PlayerPrefs.DeleteKey(SaveKeys[i]);
            else PlayerPrefs.SetString(SaveKeys[i], savedPrefs[i]);
        }
        PlayerPrefs.Save();
        if (savedFile != null) File.WriteAllText(SaveFilePath, savedFile);
        else if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
    }

    private static Type T(string name) => Session6Tests.T(name);
    private static object SP(string type, string member) => Session6Tests.SP(type, member);
    private static object P(object o, string p) => Session6Tests.P(o, p);
    private static object F(object o, string f) => Session6Tests.F(o, f);
    private static object M(object o, string m, params object[] a) => Session6Tests.M(o, m, a);
    private static object PlayerData => P(SP("GameManager", "Instance"), "PlayerData");
    private static object Decor => SP("DecorPlacementController", "Instance");
    private static object DecorData(string id) => ((Array)SP("DecorPlacementController", "All")).Cast<object>()
        .First(d => (string)F(d, "id") == id);
    private static List<Component> PlacedDecor => ((IEnumerable)P(Decor, "Placed")).Cast<Component>().Where(c => c != null).ToList();

    private static IEnumerator ReloadBase()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(1.6f);   // yükleme onarımı (4 kare) + görsel onarım (0,5 sn) bitsin
    }

    private static object DecorRecord(string instance, string id, Vector2Int cell)
    {
        var rec = Activator.CreateInstance(T("PlacedDecorSave"));
        rec.GetType().GetField("instanceId").SetValue(rec, instance);
        rec.GetType().GetField("decorId").SetValue(rec, id);
        rec.GetType().GetField("x").SetValue(rec, cell.x);
        rec.GetType().GetField("y").SetValue(rec, cell.y);
        return rec;
    }

    private static Dictionary<string, Vector2Int> DecorCells() => PlacedDecor.ToDictionary(
        c => (string)P(c, "InstanceId"), c => (Vector2Int)P(c, "Cell"));

    private static List<string> TerrainSignature()
    {
        var list = new List<string>();
        foreach (var o in (IEnumerable)P(SP("BaseMapManager", "Instance"), "MapObjects"))
        {
            string type = F(o, "objectType").ToString();
            if (type != "Tree" && type != "Rock") continue;
            var go = (GameObject)F(o, "gameObject");
            if (go == null) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            list.Add($"{F(o, "id")}:{type}@{go.transform.position.x:F3},{go.transform.position.y:F3} " +
                     $"{(sr != null && sr.sprite != null ? sr.sprite.name : "-")} x{go.transform.localScale.x:F3}");
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    // ==================== MADDE 31 ====================

    [UnityTest]
    public IEnumerator D31_DecorAndTerrainStayIdenticalAcrossBaseReloads()
    {
        yield return Session4Tests.LoadBaseWith(9, null, 5000);
        var bush = DecorData("bush");
        var area = (RectInt)P(SP("BaseMapManager", "Instance"), "BuildableRect");
        // Boş 3 hücre (aralarında boşluk) + varsa "yapının görseline değen ama ayak izi boş" bir hücre (eskiden her girişte itilirdi).
        var chosen = new List<Vector2Int>();
        Vector2Int? nearVisual = null;
        for (int y = area.yMin + 1; y < area.yMax - 1; y++)
            for (int x = area.xMin + 1; x < area.xMax - 1; x++)
            {
                var cell = new Vector2Int(x, y);
                bool strict = (bool)M(Decor, "CanPlace", bush, cell, null);
                bool soft = (bool)M(Decor, "SavedCellValid", bush, cell, null);
                if (strict && chosen.Count < 3 && chosen.All(c => Mathf.Abs(c.x - x) + Mathf.Abs(c.y - y) > 3)) chosen.Add(cell);
                if (!strict && soft && nearVisual == null) nearVisual = cell;
            }
        Assert.AreEqual(3, chosen.Count, "test için boş hücre bulunamadı");
        var records = (IList)F(PlayerData, "placedDecor");
        records.Clear();
        for (int i = 0; i < chosen.Count; i++) records.Add(DecorRecord("d31_" + i, "bush", chosen[i]));
        if (nearVisual.HasValue) records.Add(DecorRecord("d31_visual", "bush", nearVisual.Value));
        Debug.Log($"[D31] seçilen {string.Join(", ", chosen)}; görsele değen {(nearVisual.HasValue ? nearVisual.Value.ToString() : "yok")}");

        yield return ReloadBase();
        var first = DecorCells();
        var terrainFirst = TerrainSignature();
        for (int i = 0; i < chosen.Count; i++)
            Assert.AreEqual(chosen[i], first["d31_" + i], $"dekor {i} kayıtlı hücresinde kalmalı (ilk giriş)");
        if (nearVisual.HasValue) Assert.AreEqual(nearVisual.Value, first["d31_visual"], "görsele değen dekor yerinden oynamamalı");
        Assert.Greater(terrainFirst.Count, 0, "arazi ağaç/kayaları var");

        yield return ReloadBase();
        yield return ReloadBase();
        var third = DecorCells();
        CollectionAssert.AreEquivalent(first, third, "dekor hücreleri girişler arasında aynı");
        CollectionAssert.AreEqual(terrainFirst, TerrainSignature(), "arazi ağaç/kayalarının yeri, sprite'ı ve ölçeği aynı");
        var saved = ((IEnumerable)F(PlayerData, "placedDecor")).Cast<object>()
            .ToDictionary(r => (string)F(r, "instanceId"), r => new Vector2Int((int)F(r, "x"), (int)F(r, "y")));
        foreach (var pair in first) Assert.AreEqual(pair.Value, saved[pair.Key], "kayıt da aynı: " + pair.Key);
    }

    // ==================== MADDE 33 ====================

    [UnityTest]
    public IEnumerator D33_NewBuilderHutsKeepAGapFromEachOther()
    {
        yield return Session4Tests.LoadBaseWith(9, save =>
        {
            save.GetType().GetField("builderCount").SetValue(save, 3);
            ((IList)save.GetType().GetField("builderHuts").GetValue(save)).Clear();
        }, 5000);
        yield return new WaitForSecondsRealtime(0.5f);
        var huts = ((IEnumerable)SP("BuilderHut", "All")).Cast<Component>().Where(h => h != null).ToList();
        Assert.AreEqual(3, huts.Count, "üç kulübe");
        var map = SP("BaseMapManager", "Instance");
        for (int i = 0; i < huts.Count; i++)
            for (int j = i + 1; j < huts.Count; j++)
            {
                var a = (Rect)P(huts[i], "Footprint");
                var b = (Rect)P(huts[j], "Footprint");
                float gapX = Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax);
                float gapY = Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax);
                Assert.GreaterOrEqual(Mathf.Max(gapX, gapY), 0.99f,
                    $"kulübe {i} ve {j} dip dibe: {a} / {b}");
                var va = (Rect)T("BaseVisualBounds").GetMethod("Of").Invoke(null, new object[] { huts[i].gameObject });
                var vb = (Rect)T("BaseVisualBounds").GetMethod("Of").Invoke(null, new object[] { huts[j].gameObject });
                Assert.IsFalse(va.Overlaps(vb), $"kulübe {i} ve {j} görselleri iç içe: {va} / {vb}");
            }
        // Kayıtlı konum korunur: yeniden girişte kulübeler aynı yerde.
        var before = huts.Select(h => (Vector2)h.transform.position).ToList();
        yield return ReloadBase();
        var after = ((IEnumerable)SP("BuilderHut", "All")).Cast<Component>().Where(h => h != null)
            .OrderBy(h => (int)P(h, "Index")).Select(h => (Vector2)h.transform.position).ToList();
        CollectionAssert.AreEqual(before, after, "kayıtlı kulübe konumları değişmedi");
        Assert.NotNull(map);
    }

    // ==================== MADDE 19 / 29 ====================

    [UnityTest]
    public IEnumerator D19_ArcherWandersAtArcheryRangeOutsideRaids()
    {
        yield return IslandPhaseBTests.LoadIsland(null, 6, v => { });
        var setup = IslandPhaseBTests.Setup;
        var archer = (Component)SP("IslandArcher", "Instance");
        Assert.NotNull(archer, "okçu var");
        Assert.IsTrue(archer.gameObject.activeSelf, "Okçuluk Alanı hazır: okçu adada");
        var args = new object[] { Vector2.zero };
        Assert.IsTrue((bool)T("IslandSceneSetup").GetMethod("TryArcheryDoor").Invoke(null, args), "Okçuluk Alanı kurulu");
        var door = (Vector2)args[0];
        var post0 = (Vector2)P(((Array)SP("IslandLayout", "Posts")).GetValue(0), "World");
        float radius = (float)T("IslandArcher").GetField("WanderRadius").GetValue(null);

        // Sahne girişi: kapıda, kale yanındaki P1'de değil.
        Assert.IsTrue((bool)P(archer, "Wandering"), "giriş: gezinme düzeni");
        Vector2 at = (Vector2)P(archer, "Position");
        Assert.LessOrEqual(Vector2.Distance(at, door), radius + 0.3f, $"giriş: Okçuluk Alanı yakınında ({at} / kapı {door})");
        Assert.Greater(Vector2.Distance(at, post0), 1.5f, "giriş: kıyı kalesi yanındaki P1'de değil");

        // Gezinir (yürür/durur) ama alanın dışına çıkmaz.
        var seen = new List<Vector2>();
        float until = Time.realtimeSinceStartup + 9f;
        while (Time.realtimeSinceStartup < until)
        {
            seen.Add((Vector2)P(archer, "Position"));
            yield return new WaitForSeconds(0.25f);
        }
        Assert.Greater(seen.Max(p => Vector2.Distance(p, door)), 0.3f, "okçu gezindi");
        Assert.LessOrEqual(seen.Max(p => Vector2.Distance(p, door)), radius + 0.3f, "gezinme Okçuluk Alanı çevresinde kaldı");

        // Akın: eskisi gibi P1'den başlar (savaş davranışı aynı).
        var raid = P(setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 1 }), "akın başladı");
        yield return null;
        Assert.IsFalse((bool)P(archer, "Wandering"), "akında gezinme yok");
        Assert.AreEqual(0, (int)P(archer, "CurrentPost"));
        Assert.Less(Vector2.Distance((Vector2)P(archer, "Position"), post0), 0.05f, "akın başında P1'de");

        // Akın sonrası (madde 29): Okçuluk Alanı'na döner.
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
        yield return null;
        Assert.IsTrue((bool)P(archer, "Wandering"), "akın sonrası gezinme");
        Assert.LessOrEqual(Vector2.Distance((Vector2)P(archer, "Position"), door), radius + 0.3f, "akın sonrası Okçuluk Alanı yakınında");
    }

    // ==================== MADDE 18 ====================

    [UnityTest]
    public IEnumerator D18_TapSelectsTheTouchedDecorAndMenuSitsOnIt()
    {
        yield return Session4Tests.LoadBaseWith(9, save => ((IList)save.GetType().GetField("placedDecor").GetValue(save)).Clear(), 5000);
        var tree = DecorData("tree");
        var bush = DecorData("bush");
        var area = (RectInt)P(SP("BaseMapManager", "Instance"), "BuildableRect");
        // Ağacın hemen altına (önüne) çalı: ağaç karesinin saydam payı çalının üstüne taşar; çalıya dokunmak çalıyı seçmeli.
        Vector2Int? treeCell = null;
        for (int y = area.yMin + 2; y < area.yMax - 2 && treeCell == null; y++)
            for (int x = area.xMin + 2; x < area.xMax - 2 && treeCell == null; x++)
            {
                var c = new Vector2Int(x, y);
                if ((bool)M(Decor, "CanPlace", tree, c, null) && (bool)M(Decor, "CanPlace", bush, c + Vector2Int.down, null)
                    && (bool)M(Decor, "CanPlace", bush, c + Vector2Int.right, null)) treeCell = c;
            }
        Assert.IsTrue(treeCell.HasValue, "boş hücre yok");
        var spawn = Decor.GetType().GetMethod("Spawn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var treeItem = (Component)spawn.Invoke(Decor, new object[] { tree, treeCell.Value, "d18_tree" });
        var bushItem = (Component)spawn.Invoke(Decor, new object[] { bush, treeCell.Value + Vector2Int.down, "d18_bush" });
        var bush2 = (Component)spawn.Invoke(Decor, new object[] { bush, treeCell.Value + Vector2Int.right, "d18_bush2" });
        Physics2D.SyncTransforms();
        yield return null;

        var setup = SP("BaseSceneSetup", "Instance");
        var pick = setup.GetType().GetMethod("SmallTargetAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var visible = T("BaseVisualBounds").GetMethod("VisibleBounds");
        foreach (var item in new[] { bushItem, bush2, treeItem })
        {
            var b = (Bounds)visible.Invoke(null, new object[] { item.GetComponent<SpriteRenderer>() });
            var hit = (Collider2D)pick.Invoke(setup, new object[] { (Vector2)b.center });
            Assert.NotNull(hit, "dokunuşta hedef bulundu: " + item.name);
            Assert.AreSame(item, hit.GetComponentInParent(T("DecorInstance")), $"{P(item, "InstanceId")} görünür merkezine dokunuş onu seçmeli");
        }

        // Çalıya dokun: menü çalıya bağlı ve çalının görünür sınırına göre konumlanır.
        var bushBounds = (Bounds)visible.Invoke(null, new object[] { bushItem.GetComponent<SpriteRenderer>() });
        setup.GetType().GetMethod("HandleTap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(setup, new object[] { new Vector3(bushBounds.center.x, bushBounds.center.y, 0f) });
        yield return null;
        Assert.AreSame(bushItem, SP("SelectionManager", "Current"), "seçili nesne dokunulan çalı");
        var menu = SP("WorldActionMenu", "Current");
        Assert.NotNull(menu, "eylem menüsü açık");
        var boundsFn = (Func<Bounds>)menu.GetType().GetField("bounds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu);
        var anchored = boundsFn();
        Assert.Less(Vector2.Distance(anchored.center, bushBounds.center), 0.01f, "menü çalının görünür sınırına bağlı");
        Assert.Less(anchored.size.y, bushItem.GetComponent<SpriteRenderer>().bounds.size.y + 0.001f, "görünür sınır tam sprite karesinden büyük değil");
    }
}
