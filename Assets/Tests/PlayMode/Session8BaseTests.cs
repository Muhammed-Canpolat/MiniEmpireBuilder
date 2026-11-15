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
/// 8. oturum (4): Ana Üs yerleşimi — kaldırılan doğa nesnelerinin hücreleri görsel ve mantıksal olarak tamamen boşalır mı (D100).
/// </summary>
public class Session8BaseTests
{
    // Kullanıcının kaydı korunur (testler kendi kayıtlarını yazar).
    private static readonly string[] SaveKeys =
        { "MiniEmpireBuilder_SaveData", "MiniEmpireBuilder_SaveData_Backup", "MiniEmpireBuilder_SaveData_Corrupt" };
    private string[] savedPrefs;
    private string savedFile;
    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

    [NUnit.Framework.OneTimeSetUp]
    public void BackupSave()
    {
        savedPrefs = SaveKeys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
        savedFile = File.Exists(SaveFilePath) ? File.ReadAllText(SaveFilePath) : null;
    }

    [NUnit.Framework.OneTimeTearDown]
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
    }

    private static Type T(string name) => Session6Tests.T(name);
    private static object S(string type, string method, params object[] args) => Session6Tests.S(type, method, args);
    private static object SP(string type, string member) => Session6Tests.SP(type, member);
    private static object F(object o, string f) => Session6Tests.F(o, f);
    private static object P(object o, string p) => Session6Tests.P(o, p);
    private static object M(object o, string m, params object[] a) => Session6Tests.M(o, m, a);
    private static void SetF(object o, string f, object v) => Session6Tests.SetF(o, f, v);

    private static string Folder
    {
        get
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session8"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static object Map => SP("BaseMapManager", "Instance");
    private static IDictionary ByCell => (IDictionary)SP("NatureObstacle", "ByCell");

    /// <summary>Yerleşim kuralından BAĞIMSIZ: hücre dikdörtgenine ekranda görünen (zemin dışı) bir sprite değiyor mu; değenlerin adları.</summary>
    internal static List<string> VisibleSpritesOn(RectInt cells, float inset = 0.2f)
    {
        var rect = new Rect(cells.x + inset, cells.y + inset, cells.width - inset * 2f, cells.height - inset * 2f);
        var hits = new List<string>();
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (sr == null || !sr.enabled || sr.sprite == null || !sr.gameObject.activeInHierarchy || sr.color.a < 0.2f) continue;
            if (sr.sortingOrder <= 0) continue;   // zemin / çim / yol katmanları
            string path = sr.name;
            for (var t = sr.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            if (path.Contains("Cloud") || path.Contains("GhostCell") || path.Contains("Grid") || path.Contains("Highlight")) continue;
            var r = (Rect)S("BaseVisualBounds", "RendererRect", sr);
            if (r.Overlaps(rect)) hits.Add($"{path}(a{sr.color.a:0.00},o{sr.sortingOrder})");
        }
        return hits;
    }

    private static readonly string[] PlaceTypes = { "ArcherTower", "CannonTower", "Monastery", "Barracks", "House" };

    /// <summary>Hücreyi kapsayan her yerleşim için kural kararı; reddedilenlerin gerekçesi + görsel boşluk.</summary>
    private static void ProbeCell(Vector2Int cell, List<string> lines, List<string> hidden)
    {
        var validate = T("PlacementController").GetMethod("Validate");
        var bt = T("BuildingType");
        foreach (var name in PlaceTypes)
        {
            var type = Enum.Parse(bt, name);
            var size = (Vector2Int)S("BaseWorldLayout", "Footprint", type);
            var typical = S("BaseOccupancyGrid", "TypicalVisual", type);
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                {
                    var cells = new RectInt(cell.x - dx, cell.y - dy, size.x, size.y);
                    var center = new Vector2(cells.x + size.x * 0.5f, cells.y + size.y * 0.5f);
                    bool ok = (bool)validate.Invoke(null, new object[] { type, center, null, null });
                    if (ok) continue;
                    var reasons = new List<string>();
                    S("BaseOccupancyGrid", "ConflictCells", cells, null, null, true, true, null, typical, null, reasons);
                    var sprites = VisibleSpritesOn(cells);
                    bool onlyArea = reasons.All(r => r.StartsWith("alan") || r.StartsWith("görsel alan"));
                    string line = $"  {name} {cells}: RED — {string.Join("; ", reasons)} | görünen: {(sprites.Count == 0 ? "YOK" : string.Join(", ", sprites.Take(4)))}";
                    lines.Add(line);
                    if (sprites.Count == 0 && !onlyArea) hidden.Add($"{cell} {line}");
                }
        }
    }

    // ==================== O2: KALICI REGRESYON ====================

    private static IList Occupants => (IList)T("BaseOccupancyGrid").GetProperty("Occupants").GetValue(null);

    /// <summary>"Mevcut kayıt": eski sürüm (v12), çalı/koyun ağılı (eski hizalamayla yerleşmiş dekor), kayıtta hayalet doğa kimlikleri ve sahnede
    /// karşılığı olmayan dekor kaydı; arazinin tamamı açık, doğa tabanı Ana Üs 1 (açık alanda doğa nesneleri).</summary>
    /// <summary>Taşınacak 4 bina türü (eski düzen yuvalarında) + çalı/koyun ağılı; diğer arazi boş.</summary>
    private static void Partial(object save)
    {
        var placed = (IList)F(save, "placedBuildings");
        var wanted = new[] { "main", "mine_1", "archer_1", "cannon_1", "monastery_1", "house_1" };
        foreach (var slot in (Array)T("BaseWorldLayout").GetField("Slots").GetValue(null))
        {
            var type = slot.GetType().GetField("buildingType").GetValue(slot);
            string id = (string)slot.GetType().GetField("id").GetValue(slot);
            if (type == null || !wanted.Contains(id)) continue;
            if (placed.Cast<object>().Any(p => (string)F(p, "slotId") == id)) continue;
            var entry = Activator.CreateInstance(T("PlacedBuildingData"));
            SetF(entry, "slotId", id); SetF(entry, "buildingType", type.ToString()); SetF(entry, "level", id == "main" ? 15 : 10);
            placed.Add(entry);
        }
        foreach (var b in (IEnumerable)F(save, "buildings")) SetF(b, "isUnlocked", true);
        var decor = (IList)F(save, "placedDecor");
        int n = 0;
        foreach (var (id, x, y) in new[] { ("bush", 16, 27), ("bush", 17, 27), ("bush", 18, 27), ("sheep", 16, 29), ("sheep", 18, 29) })
        {
            var rec = Activator.CreateInstance(T("PlacedDecorSave"));
            SetF(rec, "instanceId", "pen_" + n++); SetF(rec, "decorId", id); SetF(rec, "x", x); SetF(rec, "y", y);
            decor.Add(rec);
        }
        SetF(save, "decorLayoutVersion", 1);
    }

    private static void ExistingSave(object save)
    {
        Partial(save);
        SetF(save, "saveVersion", 12);
        SetF(save, "landOpened", 4);
        SetF(save, "natureBaselineLevel", 1);
        var removed = (IList)F(save, "removedNatureIds");
        removed.Add("Tree_0_0"); removed.Add("Rock_99_99"); removed.Add("Bush_3_40");   // eski/hayalet kimlikler
        var decor = (IList)F(save, "placedDecor");
        var rec = Activator.CreateInstance(T("PlacedDecorSave"));
        SetF(rec, "instanceId", "ghost_decor"); SetF(rec, "decorId", "bush"); SetF(rec, "x", 40); SetF(rec, "y", 40);
        decor.Add(rec);
    }

    private static void NewSave(object save)
    {
        Partial(save);
        SetF(save, "landOpened", 4);
        SetF(save, "natureBaselineLevel", 1);
    }

    private static readonly string[] MoveTypes = { "ArcherTower", "CannonTower", "Monastery", "House" };

    /// <summary>
    /// Kaldırılabilir öğelerin bulunduğu, çevresi (sağ/sol/üst 1, alt 2 hücre — kule görselleri ayak izinin altına sarkar) yapıdan/kamptan boş
    /// 4×4 alanlar. Her alanda en az bir doğa ya da dekor öğesi bulunur.
    /// </summary>
    private static List<RectInt> FindClearings(int want)
    {
        var area = (RectInt)P(Map, "BuildableRect");
        var result = new List<RectInt>();
        var occ = Occupants.Cast<object>().ToList();
        for (int y = area.yMin + 4; y <= area.yMax - 6 && result.Count < want; y++)
            for (int x = area.xMin + 1; x <= area.xMax - 5 && result.Count < want; x++)
            {
                var r = new RectInt(x, y, 4, 4);
                var e = new RectInt(x - 1, y - 2, 6, 7);
                if (result.Any(o => o.Overlaps(new RectInt(e.x - 1, e.y - 1, e.width + 2, e.height + 2)))) continue;
                bool blocked = false; int removable = 0;
                foreach (var o in occ)
                {
                    var cells = (RectInt)F(o, "cells");
                    string kind = (string)F(o, "kind");
                    if ((kind == "structure" || kind == "camp") && cells.Overlaps(e)) { blocked = true; break; }
                    if ((kind == "nature" || kind == "decor") && cells.Overlaps(r))
                    {
                        if (cells.xMin < r.xMin || cells.yMin < r.yMin || cells.xMax > r.xMax || cells.yMax > r.yMax) { blocked = true; break; }
                        removable++;
                    }
                }
                if (!blocked && (bool)S("BaseOccupancyGrid", "InsideUnlockedArea", r, true)) result.Add(r);
            }
        return result;
    }

    /// <summary>
    /// Alana oyunun kendi bileşenleriyle kaldırılabilir öğeler koyar: 2 doğa nesnesi (ağaç + kaya, BaseSceneSetup'ın kurduğu gibi
    /// SpriteRenderer + BoxCollider2D + NatureObstacle) ve 2 dekor (çalı + kütük, DecorPlacementController.Spawn).
    /// </summary>
    private static IEnumerator SeedArea(RectInt r)
    {
        var all = ((Array)SP("DecorPlacementController", "All")).Cast<object>().ToList();
        object Data(string id) => all.First(d => (string)F(d, "id") == id);
        var kinds = T("NatureKind");
        foreach (var (kind, cell, art) in new[] { ("Tree", new Vector2Int(r.x, r.y), "tree"), ("Rock", new Vector2Int(r.x + 2, r.y + 1), "rock") })
        {
            if (Occupants.Cast<object>().Any(o => ((RectInt)F(o, "cells")).Contains(cell))) continue;
            var go = new GameObject($"LockedObstacle_{cell.x}_{cell.y}");
            go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = (Sprite)P(Data(art), "Visual");
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * (1f / Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y));
            go.AddComponent<BoxCollider2D>();
            var n = go.AddComponent(T("NatureObstacle"));
            T("NatureObstacle").GetField("kind").SetValue(n, Enum.Parse(kinds, kind));
            T("NatureObstacle").GetProperty("cell").SetValue(n, cell);
        }
        var controller = SP("DecorPlacementController", "Instance");
        var spawn = controller.GetType().GetMethod("Spawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var (id, cell) in new[] { ("bush", new Vector2Int(r.x + 1, r.y + 3)), ("stump", new Vector2Int(r.x + 3, r.y + 2)) })
            if (!Occupants.Cast<object>().Any(o => ((RectInt)F(o, "cells")).Contains(cell)))
                spawn.Invoke(controller, new object[] { Data(id), cell, $"seed_{id}_{cell.x}_{cell.y}" });
        yield return null;
    }

    /// <summary>Alandaki doğa (altınla anında / süreli temizleme) ve dekor (sil) öğelerini kaldırır; kaldırılan adları döner.</summary>
    private static IEnumerator ClearArea(RectInt r, List<string> removedNames, bool timed)
    {
        var controller = SP("DecorPlacementController", "Instance");
        var gm = SP("GameManager", "Instance");
        foreach (var o in Occupants.Cast<object>().ToList())
        {
            var cells = (RectInt)F(o, "cells");
            if (!cells.Overlaps(r)) continue;
            string kind = (string)F(o, "kind");
            if (kind == "nature")
            {
                var n = F(o, "nature");
                if (timed)
                {
                    M(n, "RequestClear");
                    S("NatureClear", "Pump");
                    var task = M(gm, "TaskForSlot", (string)P(n, "TaskSlot"));
                    if (task != null) M(gm, "FinishTaskNow", task);
                }
                else M(n, "TryRemove");
                removedNames.Add("doğa " + P(n, "Id"));
            }
            else if (kind == "decor")
            {
                var d = F(o, "decor");
                removedNames.Add("dekor " + F(P(d, "Data"), "id") + " " + P(d, "Cell"));
                M(controller, "Delete", d);
            }
            yield return null;
        }
        yield return null;
    }

    private static IEnumerator Journey(string tag, Action<object> fixture, List<string> lines, List<string> failures)
    {
        yield return Session4Tests.LoadBaseWith(15, fixture, 50000);
        yield return new WaitForSecondsRealtime(0.5f);
        lines.Add($"== {tag} == yükleme: " + (string)M(Map, "ValidateOccupancy"));
        var clearings = FindClearings(4);
        lines.Add($"boş alanlar: {string.Join(", ", clearings)}");
        if (clearings.Count < 2) { failures.Add($"{tag}: test için yeterli alan bulunamadı ({clearings.Count})"); yield break; }
        var removed = new List<string>();
        foreach (var r in clearings) yield return SeedArea(r);
        for (int i = 0; i < clearings.Count; i++) yield return ClearArea(clearings[i], removed, timed: i % 2 == 1);
        lines.Add($"kaldırılan {removed.Count}: {string.Join(", ", removed)}");
        if (removed.Count < 10) failures.Add($"{tag}: en az 10 öğe kaldırılmalıydı ({removed.Count})");
        yield return new WaitForSeconds(0.3f);
        var validate = T("PlacementController").GetMethod("Validate");
        var bt = T("BuildingType");
        foreach (var r in clearings)
        {
            // 1) Kural: farklı boyutlarda yeni bina aynı hücrelere
            foreach (var name in MoveTypes)
            {
                var type = Enum.Parse(bt, name);
                var size = (Vector2Int)S("BaseWorldLayout", "Footprint", type);
                var center = new Vector2(r.x + size.x * 0.5f, r.y + size.y * 0.5f);
                bool ok = (bool)validate.Invoke(null, new object[] { type, center, null, null });
                if (!ok)
                {
                    var reasons = new List<string>();
                    S("BaseOccupancyGrid", "ConflictCells", new RectInt(r.x, r.y, size.x, size.y), null, null, true, true, null,
                        S("BaseOccupancyGrid", "TypicalVisual", type), null, reasons);
                    failures.Add($"{tag} {r}: yeni {name} reddedildi — {string.Join("; ", reasons)}");
                }
            }
            // 2) Taşıma: her boyuttan mevcut bina alana taşınır, sonra geri
            foreach (var name in MoveTypes)
            {
                var obj = Session5Tests.MapObjects().FirstOrDefault(o => F(o, "buildingType")?.ToString() == name);
                if (obj == null) { lines.Add($"{name} yok, taşıma atlandı"); continue; }
                var go = (GameObject)F(obj, "gameObject");
                var size = (Vector2Int)S("BaseWorldLayout", "Footprint", Enum.Parse(bt, name));
                Vector2 home = go.transform.position;
                var homeCell = new Vector2Int(Mathf.RoundToInt(home.x - size.x * 0.5f), Mathf.RoundToInt(home.y - size.y * 0.5f));
                yield return Session5Tests.MoveGhostTo(obj, new Vector2Int(r.x, r.y));
                bool valid = Session5Tests.GhostValid;
                if (!valid)
                {
                    var conflicts = (HashSet<Vector2Int>)P(Session5Tests.Placement, "CurrentConflicts");
                    failures.Add($"{tag} {r}: {name} taşıması reddedildi, kırmızı hücreler {string.Join(" ", conflicts)}");
                    M(Session5Tests.Placement, "CancelPlacement");
                    yield return null;
                    continue;
                }
                Session5Tests.ConfirmGhost();
                yield return null; yield return null;
                var placedAt = (Vector2)go.transform.position;
                if (!r.Contains(Vector2Int.FloorToInt(placedAt))) failures.Add($"{tag} {r}: {name} onaylandı ama yerinde değil ({placedAt})");
                yield return Session5Tests.MoveGhostTo(obj, homeCell);
                if (Session5Tests.GhostValid) Session5Tests.ConfirmGhost();
                else { failures.Add($"{tag}: {name} eski yerine geri taşınamadı"); M(Session5Tests.Placement, "CancelPlacement"); }
                yield return null; yield return null;
            }
        }
        // 3) Yeni bina kurulur (ilk alana Köylü Evi) — oyunun kurma yolu (SpawnBuilding: yer uygun değilse binayı başka yere kaydırır,
        //    yani konum kuralın kararını gösterir) — ve kayıt → yeniden yükleme
        var first = clearings[0];
        var houseSize = (Vector2Int)S("BaseWorldLayout", "Footprint", Enum.Parse(bt, "House"));
        var houseCenter = new Vector3(first.x + houseSize.x * 0.5f, first.y + houseSize.y * 0.5f, 0f);
        bool houseValid = (bool)validate.Invoke(null, new object[] { Enum.Parse(bt, "House"), (Vector2)houseCenter, null, null });
        var built = M(Map, "SpawnBuilding", Enum.Parse(bt, "House"), houseCenter, null);
        var builtPos = built != null ? (Vector2)((GameObject)F(built, "gameObject")).transform.position : Vector2.zero;
        if (built == null || Vector2.Distance(builtPos, houseCenter) > 0.05f)
            failures.Add($"{tag} {first}: yeni Köylü Evi istenen yere kurulmadı ({builtPos})");
        int housesAfterConfirm = Session5Tests.MapObjects().Count(o => F(o, "buildingType")?.ToString() == "House");
        yield return new WaitForSeconds(0.3f);
        M(Map, "SaveAll");
        UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        lines.Add("yeniden yükleme: " + (string)M(Map, "ValidateOccupancy"));
        foreach (var r in clearings.Skip(1))
        {
            var natureLeft = Occupants.Cast<object>().Where(o => (string)F(o, "kind") == "nature" && ((RectInt)F(o, "cells")).Overlaps(r)).ToList();
            if (natureLeft.Count > 0) failures.Add($"{tag} {r}: yeniden yüklemede kaldırılan doğa geri geldi ({natureLeft.Count})");
            var type = Enum.Parse(bt, "Monastery");
            bool ok = (bool)validate.Invoke(null, new object[] { type, new Vector2(r.x + 1.5f, r.y + 1.5f), null, null });
            if (!ok) failures.Add($"{tag} {r}: yeniden yüklemeden sonra Manastır reddedildi");
        }
        bool houseThere = Session5Tests.MapObjects().Any(o => F(o, "buildingType")?.ToString() == "House"
            && first.Contains(Vector2Int.FloorToInt((Vector2)((GameObject)F(o, "gameObject")).transform.position)));
        var houses = Session5Tests.MapObjects().Where(o => F(o, "buildingType")?.ToString() == "House")
            .Select(o => ((GameObject)F(o, "gameObject")).transform.position.ToString()).ToList();
        lines.Add($"Köylü Evleri (yeniden yükleme): {string.Join(" ", houses)}; beklenen alan {first}; onay öncesi geçerli {houseValid}, onay sonrası ev sayısı {housesAfterConfirm}");
        if (!houseThere) failures.Add($"{tag}: yeni Köylü Evi yeniden yüklemede yerinde değil ({string.Join(" ", houses)})");
        yield return null; yield return null; Session4Tests.CaptureUi($"../session8/O2_{tag}_after_reload", 1080, 1920);
    }

    /// <summary>
    /// O2 (kalıcı regresyon, D100): ağaç/kaya/çalı/kütük (doğa nesnesi: anında ve süreli temizleme; dekor: sil) kaldırılır → aynı hücrelere
    /// 4 farklı boyutta bina (Okçu 4×4, Topçu 3×2, Manastır 3×3, Köylü Evi 2×3) kuralı kabul eder, mevcut binalar oraya taşınıp geri alınır,
    /// yeni bina kurulur; kayıt → yeniden yükleme sonrası kaldırılanlar geri gelmez, yerler hâlâ boştur. Mevcut (eski) ve yeni kayıtla ayrı ayrı.
    /// </summary>
    [UnityTest, Timeout(900000)]
    public IEnumerator O2_RemovedNatureCellsAcceptBuildingsAndMoves_ExistingAndNewSave()
    {
        _ = Folder;
        var lines = new List<string>();
        var failures = new List<string>();
        yield return Journey("mevcut_kayit", ExistingSave, lines, failures);
        yield return Journey("yeni_kayit", NewSave, lines, failures);
        File.WriteAllLines(Path.Combine(Folder, "O2_regression.txt"), lines.Concat(new[] { "", "HATALAR:" }).Concat(failures));
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }

    /// <summary>O3: geliştirici "Occupancy göster" — dolu her hücre için bir kare, her dolduran nesne için adıyla etiket; kapatınca kalkar.</summary>
    [UnityTest]
    public IEnumerator O3_OccupancyDebugViewShowsCellsAndOwnerNames()
    {
        _ = Folder;
        yield return Session4Tests.LoadBaseWith(15, NewSave, 50000);
        var view = T("OccupancyDebugView");
        view.GetProperty("Enabled").SetValue(null, true);
        yield return new WaitForSecondsRealtime(0.6f);
        var root = GameObject.Find("OccupancyDebugView");
        Assert.NotNull(root, "görünüm açıldı");
        var occ = Occupants.Cast<object>().ToList();
        int cells = occ.Sum(o => { var r = (RectInt)F(o, "cells"); return r.width * r.height; });
        int tiles = root.GetComponentsInChildren<SpriteRenderer>(false).Length;
        var labelTexts = root.GetComponentsInChildren<Component>(false).Where(c => c.GetType().Name == "TextMeshPro")
            .Select(c => (string)c.GetType().GetProperty("text").GetValue(c)).ToList();
        File.WriteAllLines(Path.Combine(Folder, "O3_debug_view.txt"), new[] { $"dolu hücre {cells}, kare {tiles}, etiket {labelTexts.Count}" }.Concat(labelTexts));
        Assert.AreEqual(cells, tiles, "her dolu hücre için bir kare");
        Assert.AreEqual(occ.Count, labelTexts.Count, "her nesne için bir etiket");
        Assert.IsTrue(labelTexts.Any(t => t.Contains("MainBase")), "Ana Üs etiketi");
        yield return null; yield return null; Session4Tests.CaptureUi("../session8/O3_occupancy_debug", 1080, 1920);
        view.GetProperty("Enabled").SetValue(null, false);
        yield return null;
        Assert.IsNull(GameObject.Find("OccupancyDebugView"), "kapatınca kalktı");
    }

    /// <summary>
    /// O1: her dekor türü (katalog + emekli) ve her doğa nesnesi için DOLU SAYILAN hücreler ile ÇİZİLEN görsel aynı yerde mi.
    /// Ölçüt: görselin sıkı (saydam hariç) alt kenarı, dolu sayılan alanın alt kenarına 0,45 birimden yakın; görselin yatay merkezi alanın
    /// içinde. Uyuşmazlıkta oyuncu dolu hücreyi boş görür (ve çizilen hücre boş sayılır).
    /// </summary>
    [UnityTest, Timeout(600000)]
    public IEnumerator O1_DrawnVisualSitsOnTheOccupiedCells()
    {
        _ = Folder;
        yield return Session5Tests.LoadFullBase();
        var controller = SP("DecorPlacementController", "Instance");
        var spawn = controller.GetType().GetMethod("Spawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var all = (Array)SP("DecorPlacementController", "All");
        var lines = new List<string> { "öğe | dolu hücreler | sıkı görsel [xMin,yMin–xMax,yMax] | alt kenar farkı | durum" };
        var bad = new List<string>();
        int col = 2;
        foreach (var data in all)
        {
            var cell = new Vector2Int(col, 3);
            col += 3;
            if (col > 18) col = 2;
            var item = spawn.Invoke(controller, new object[] { data, cell, "probe_" + F(data, "id") }) as Component;
            yield return null;
            var fp = (Vector2Int)F(data, "footprint");
            var cells = new RectInt(cell.x, cell.y, fp.x, fp.y);
            var v = (Rect)S("BaseVisualBounds", "Of", item.gameObject);
            Check($"dekor:{F(data, "id")}{((bool)F(data, "retired") ? " (emekli)" : "")}", cells, v, lines, bad);
            M(controller, "Delete", item);
            yield return null;
        }
        foreach (DictionaryEntry pair in ByCell)
        {
            var n = pair.Value as Component;
            var c = (Vector2Int)pair.Key;
            Check("doğa:" + P(n, "Id"), new RectInt(c.x, c.y, 1, 1), (Rect)S("BaseVisualBounds", "Of", n.gameObject), lines, bad, nature: true);
        }
        File.WriteAllLines(Path.Combine(Folder, "O1_alignment.txt"), lines.Concat(new[] { "", "UYUŞMAZ:" }).Concat(bad));
        Assert.IsEmpty(bad, string.Join("\n", bad));
    }

    private static void Check(string name, RectInt cells, Rect v, List<string> lines, List<string> bad, bool nature = false)
    {
        if (v.width <= 0f) { lines.Add($"{name} | {cells} | görsel yok | - | ?"); return; }
        float bottomGap = v.yMin - cells.yMin;
        float cx = v.center.x;
        // Doğa nesnesi hücre merkezinde (±0,2 rastgele) çizilir; dekor tabanı hücre tabanına oturur.
        bool ok = nature
            ? v.center.x >= cells.xMin - 0.05f && v.center.x <= cells.xMax + 0.05f && v.center.y >= cells.yMin - 0.3f && v.center.y <= cells.yMax + 0.5f
            : Mathf.Abs(bottomGap) <= 0.45f && cx >= cells.xMin && cx <= cells.xMax;
        string line = $"{name} | ({cells.x},{cells.y},{cells.width},{cells.height}) | [{v.xMin:0.00},{v.yMin:0.00}–{v.xMax:0.00},{v.yMax:0.00}] | {bottomGap:+0.00;-0.00} | {(ok ? "tamam" : "UYUŞMAZ")}";
        lines.Add(line);
        if (!ok) bad.Add(line);
    }

    /// <summary>
    /// O0 (keşif): Ana Üs 13, tüm arazi açık, doğa tabanı Ana Üs 1 (açık alanda çok doğa nesnesi). 10 doğa nesnesi kaldırılır (5 anında,
    /// 5 süreli temizleme + elmasla bitir); her hücrede doğa kaydı / nesne / collider / görünen sprite ve hücreyi kapsayan her bina
    /// yerleşiminin kural kararı (red gerekçesi: hangi nesne, hangi hücre) kaydedilir. Sonra kayıt → sahne yeniden yüklenir, tekrarlanır.
    /// "Gizli ret" = görünen sprite yok ama kural reddediyor.
    /// </summary>
    [UnityTest, Timeout(900000)]
    public IEnumerator O0_ExploreRemovedNatureCells()
    {
        _ = Folder;
        yield return Session4Tests.LoadBaseWith(13, save =>
        {
            SetF(save, "landOpened", 4);
            SetF(save, "natureBaselineLevel", 1);
            SetF(save, "gems", 5000);
        }, 50000);
        var lines = new List<string>();
        var hidden = new List<string>();
        var area = (RectInt)P(Map, "BuildableRect");
        lines.Add($"Açık alan {area}, doğa {ByCell.Count}");
        var candidates = ByCell.Keys.Cast<Vector2Int>()
            .Where(c => area.Contains(c) && !(bool)S("BaseOccupancyGrid", "InEdgeMargin", c))
            .OrderBy(c => Mathf.Abs(c.x - area.center.x) + Mathf.Abs(c.y - area.center.y)).ToList();
        var picked = new List<Vector2Int>();
        foreach (var kind in new[] { "Tree", "Rock", "Bush", "Stump", "Tree", "Rock", "Bush", "Stump", "Tree", "Rock" })
        {
            var c = candidates.FirstOrDefault(k => !picked.Contains(k) && F(ByCell[k], "kind").ToString() == kind);
            if (c == default) c = candidates.First(k => !picked.Contains(k));
            picked.Add(c);
        }
        var gm = SP("GameManager", "Instance");
        for (int i = 0; i < picked.Count; i++)
        {
            var n = ByCell[picked[i]] as Component;
            string id = (string)P(n, "Id");
            if (i < 5) lines.Add($"anında kaldır {id}: {M(n, "TryRemove")}");
            else
            {
                bool queued = (bool)M(n, "RequestClear");
                S("NatureClear", "Pump");
                var task = M(gm, "TaskForSlot", (string)P(n, "TaskSlot"));
                bool done = task != null && (bool)M(gm, "FinishTaskNow", task);
                lines.Add($"süreli temizle {id}: sıra {queued}, görev {(task != null)}, bitir {done}");
            }
            for (int f = 0; f < 4; f++) yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        foreach (var phase in new[] { "kaldırma sonrası", "yeniden yükleme sonrası" })
        {
            if (phase.StartsWith("yeniden"))
            {
                M(Map, "SaveAll");
                var save = P(gm, "PlayerData");
                lines.Add("kayıt removedNatureIds: " + string.Join(",", ((IEnumerable)F(save, "removedNatureIds")).Cast<string>()));
                UnityEngine.SceneManagement.SceneManager.LoadScene("BaseScene");
                for (int i = 0; i < 45; i++) yield return null;
                yield return new WaitForSecondsRealtime(0.8f);
            }
            lines.Add("== " + phase + " ==");
            foreach (var cell in picked)
            {
                bool inDict = ByCell.Contains(cell);
                bool goAlive = GameObject.Find($"LockedObstacle_{cell.x}_{cell.y}") != null;
                var colliders = Physics2D.OverlapPointAll(new Vector2(cell.x + 0.5f, cell.y + 0.5f)).Select(c => c.name).ToList();
                var display = (IEnumerable)S("BaseOccupancyGrid", "OccupiedForDisplay", null, null, true, true);
                bool red = display.Cast<Vector2Int>().Contains(cell);
                lines.Add($"{cell}: doğa kaydı {inDict}, nesne {goAlive}, collider [{string.Join(",", colliders)}], ızgarada kırmızı {red}, " +
                          $"görünen [{string.Join(", ", VisibleSpritesOn(new RectInt(cell.x, cell.y, 1, 1)))}]");
                ProbeCell(cell, lines, hidden);
            }
        }
        File.WriteAllLines(Path.Combine(Folder, "O0_explore.txt"), lines.Concat(new[] { "", "GİZLİ RET:" }).Concat(hidden));
        Assert.Pass($"keşif: {hidden.Count} gizli ret");
    }
}
