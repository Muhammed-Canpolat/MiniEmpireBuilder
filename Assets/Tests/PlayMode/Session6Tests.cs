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
/// 14 Kasım 2026, 6. oturum: savaş hataları (AŞAMA 1), kahraman/çırak/ekonomi (AŞAMA 2), ada (AŞAMA 3), arazi (AŞAMA 4).
/// Yalıtılmış test kaydı (gerçek kayıt geri yüklenir). Çıktılar <c>PlaytestOutput/session6/</c>.
/// </summary>
[Timeout(300000)]
public class Session6Tests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;

    internal static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).FirstOrDefault(t => t != null)
        ?? throw new Exception("Tür yok: " + name);

    internal static object S(string type, string method, params object[] args)
    {
        var methods = T(type).GetMethods().Where(m => m.Name == method && m.GetParameters().Length == args.Length).ToArray();
        if (methods.Length == 0) throw new Exception($"{type}.{method}/{args.Length} yok");
        return methods[0].Invoke(null, args);
    }

    internal static object SP(string type, string member)
    {
        var t = T(type);
        var p = t.GetProperty(member);
        return p != null ? p.GetValue(null) : t.GetField(member).GetValue(null);
    }

    internal static object F(object o, string field) => o.GetType().GetField(field).GetValue(o);
    internal static void SetF(object o, string field, object value) => o.GetType().GetField(field).SetValue(o, value);
    internal static object P(object o, string prop) => o.GetType().GetProperty(prop).GetValue(o);
    internal static object M(object o, string method, params object[] args)
    {
        var mi = o.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .First(m => m.Name == method && m.GetParameters().Length == args.Length);
        return mi.Invoke(o, args);
    }

    internal static string Folder
    {
        get
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/session6"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    protected Component manager;
    protected object save;

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

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var gm = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (gm != null) Object.DestroyImmediate(gm.gameObject);
    }

    /// <summary>Temiz kayıtla GameManager kurar.</summary>
    protected IEnumerator FreshManager()
    {
        var type = T("GameManager");
        manager = type.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(type);
        yield return null;
        save = S("PlayerSaveData", "CreateNew", Enum.Parse(T("WeaponType"), "Axe"));
        type.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        M(manager, "RebuildBuildingData");
    }

    protected IList PlacedList => (IList)F(save, "placedBuildings");
    protected IList Tasks => (IList)F(save, "builderTasks");

    protected object AddPlaced(string type, int level, string slot)
    {
        var p = Activator.CreateInstance(T("PlacedBuildingData"));
        SetF(p, "slotId", slot);
        SetF(p, "buildingType", type);
        SetF(p, "level", level);
        PlacedList.Add(p);
        return p;
    }

    protected object AddTask(string kind, string slot, string buildingType, double seconds)
    {
        var t = Activator.CreateInstance(T("BuilderTaskSave"));
        long now = DateTime.UtcNow.Ticks;
        SetF(t, "builderIndex", Tasks.Count);
        SetF(t, "kind", kind);
        SetF(t, "slotId", slot);
        SetF(t, "buildingType", buildingType);
        SetF(t, "startTicks", now);
        SetF(t, "endTicks", now + TimeSpan.FromSeconds(seconds).Ticks);
        Tasks.Add(t);
        return t;
    }

    // ==================== 1.1 Tamamlanmamış inşaat savaşa yansımaz ====================

    [UnityTest]
    public IEnumerator S1_1_UnfinishedConstructionIsNotUsedAnywhere()
    {
        yield return FreshManager();
        SetF(save, "builderCount", 3);
        // Tamamlanmış Okçu Sv.3; yeni kurulan Topçu (1 dk'lık inşa sürüyor); Sv.2 → 3 yükseltilen Kışla; inşası süren Köylü Evi ve Atölye.
        AddPlaced("ArcherTower", 3, "archer_a");
        AddPlaced("CannonTower", 1, "cannon_new");
        AddTask("build", "cannon_new", "", 60);
        AddPlaced("Barracks", 2, "barracks_a");
        AddTask("upgrade", "barracks_a", "Barracks", 600);
        AddPlaced("House", 1, "house_new");
        AddTask("build", "house_new", "", 60);
        AddPlaced("CardWorkshop", 1, "ws_new");
        AddTask("build", "ws_new", "", 60);

        int Count(string type) => (int)S("BuildingLevels", "CompletedCount", save, Enum.Parse(T("BuildingType"), type));
        Assert.AreEqual(1, Count("ArcherTower"), "tamamlanmış Okçu savaşta");
        Assert.AreEqual(0, Count("CannonTower"), "inşası süren Topçu savaşta YOK");
        Assert.AreEqual(0, Count("House"));
        Assert.AreEqual(0, Count("CardWorkshop"));

        var lancers = (IList)T("AllyBarracks").GetMethod("BarracksLevels").Invoke(null, new[] { save });
        Assert.AreEqual(1, lancers.Count);
        Assert.AreEqual(2, lancers[0], "yükseltilen Kışla savaşta TAMAMLANMIŞ seviyesiyle (2)");
        Assert.AreEqual(0f, (float)M(manager, "HouseStorageBonus"), 1e-4, "inşası süren ev depoyu büyütmez");
        Assert.AreEqual(0, (int)SP("CardWorkshop", "Level"), "inşası süren atölye kullanılamaz");

        // Süre dolunca hepsi devreye girer; yükseltme tamamlanınca 3.
        foreach (var t in Tasks) SetF(t, "endTicks", DateTime.UtcNow.Ticks - 1);
        M(manager, "CheckBuilderTask");
        Assert.AreEqual(1, Count("CannonTower"));
        Assert.Greater((float)M(manager, "HouseStorageBonus"), 0f);
        Assert.AreEqual(1, (int)SP("CardWorkshop", "Level"));
        lancers = (IList)T("AllyBarracks").GetMethod("BarracksLevels").Invoke(null, new[] { save });
        Assert.AreEqual(3, lancers[0]);
    }

    // ==================== 1.2 Menzil çemberi kulenin merkezinde, dört yönde eşit ====================

    [UnityTest]
    public IEnumerator S1_2_RangeRingCenteredAndEqualInFourDirections()
    {
        yield return FreshManager();
        foreach (string kind in new[] { "ArcherTower", "CannonTower", "Monastery" })
        {
            // Savaştaki gibi: taban noktası yuvanın 0,45 altında, görsel 2,4 birim yüksek, açılışta Y ölçeği 0,2 → 1.
            var go = new GameObject("TestTower_" + kind);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f / 1.6f);
            go.transform.position = new Vector3(2f, 1.2f, 0f);
            go.transform.localScale = new Vector3(1.6f, 1.6f * 0.2f, 1f);
            var structure = go.AddComponent(T("BattleStructure"));
            M(structure, "Initialize", Enum.Parse(T("BuildingType"), kind), 3, null, new Vector2(2f, 0f), 2.4f);
            Component comp = kind == "Monastery" ? go.AddComponent(T("MonasteryHealer")) : go.AddComponent(T("TowerController"));
            yield return null;                       // Start: halka kurulur (yapı hâlâ ezik)
            go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);   // açılış animasyonu biter
            yield return null;
            yield return null;

            Vector2 center = (Vector2)P(structure, "RangeCenter");
            Assert.AreEqual(new Vector2(2f, 1.2f), center, $"{kind}: merkez = taban + görsel yüksekliğin yarısı (kulenin ortası)");
            var ringComp = go.GetComponent(T("RangeRing"));
            Assert.NotNull(ringComp, kind + ": menzil çemberi yok");
            var ring = (GameObject)P(ringComp, "Ring");
            float radius = (float)P(ringComp, "Radius");
            Assert.AreEqual(center.x, ring.transform.position.x, 1e-3, kind + ": çember merkezi X");
            Assert.AreEqual(center.y, ring.transform.position.y, 1e-3, kind + ": çember merkezi Y (eskiden kulenin çok altında)");
            // Çizilen her kesik çizgi uç noktası merkezden aynı uzaklıkta (kuzey = güney = doğu = batı).
            float min = float.MaxValue, max = 0f, north = 0f, south = 0f;
            foreach (var line in ring.GetComponentsInChildren<LineRenderer>())
                for (int i = 0; i < line.positionCount; i++)
                {
                    Vector2 w = line.transform.TransformPoint(line.GetPosition(i));
                    float d = Vector2.Distance(w, center);
                    min = Mathf.Min(min, d); max = Mathf.Max(max, d);
                    north = Mathf.Max(north, w.y - center.y); south = Mathf.Max(south, center.y - w.y);
                }
            Assert.AreEqual(radius, min, 0.01f, kind + ": çember yarıçapı (en yakın nokta)");
            Assert.AreEqual(radius, max, 0.01f, kind + ": çember yarıçapı (en uzak nokta)");
            Assert.AreEqual(north, south, 0.05f, kind + ": kuzey ve güney kenarı eşit");

            if (kind != "Monastery")
            {
                Assert.AreEqual(center, (Vector2)P(comp, "RangeOrigin"), "hedefleme merkezi = çember merkezi");
                Assert.AreEqual(radius, (float)P(comp, "AttackRange"), 1e-4, "hedefleme yarıçapı = çember yarıçapı");
                foreach (var dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                {
                    Assert.IsTrue((bool)S("TowerController", "InRange", center, radius, center + dir * (radius - 0.02f)), $"{kind} {dir}: içeride");
                    Assert.IsFalse((bool)S("TowerController", "InRange", center, radius, center + dir * (radius + 0.02f)), $"{kind} {dir}: dışarıda");
                }
            }
            else Assert.AreEqual(center, (Vector2)P(comp, "Origin"), "iyileştirme merkezi = çember merkezi");
            Object.Destroy(go);
            yield return null;
        }
    }
}
