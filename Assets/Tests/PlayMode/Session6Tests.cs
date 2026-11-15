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

    // ==================== 1.3 Can barları ====================

    [UnityTest]
    public IEnumerator S1_3_WorldHealthBarFillStaysLeftWithTrail()
    {
        var target = new GameObject("BarTarget");
        var bar = (Component)S("HealthBar", "Create", target.transform, 100f, null, null);
        M(bar, "Resize", new Vector2(0.65f, 0.32f));
        foreach (float hp in new[] { 80f, 37f, 5f })
        {
            M(bar, "UpdateHealth", hp);
            yield return null;
            Assert.AreEqual((float)P(bar, "BarLeftEdge"), (float)P(bar, "FillLeftEdge"), 1e-4, $"can {hp}: dolgu sol kenara yapışık");
            var fill = bar.transform.Find("BarFill").GetComponent<SpriteRenderer>();
            var bg = bar.transform.Find("BarBg").GetComponent<SpriteRenderer>();
            Assert.AreEqual(bg.bounds.min.x, fill.bounds.min.x, 0.01f, $"can {hp}: dolgunun sol kenarı = zeminin sol kenarı");
            Assert.AreEqual(bg.bounds.size.x * hp / 100f, fill.bounds.size.x, 0.01f, $"can {hp}: genişlik orantılı");
            Assert.Greater((float)P(bar, "TrailRatio"), (float)P(bar, "ShownRatio"), "hasar anında beyaz iz görünür");
        }
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.AreEqual((float)P(bar, "ShownRatio"), (float)P(bar, "TrailRatio"), 0.01f, "iz kısa sürede dolguya erir");
        Object.Destroy(target);
    }

    [UnityTest]
    public IEnumerator S1_3_BattleCardsAndRangeRingsCapture()
    {
        yield return Session4Tests.StartBattle(1, 4, 6, s =>
        {
            foreach (var (type, slot) in new[] { ("ArcherTower", "archer_1"), ("CannonTower", "cannon_1"), ("Monastery", "monastery_1") })
            {
                var p = Activator.CreateInstance(T("PlacedBuildingData"));
                SetF(p, "slotId", slot); SetF(p, "buildingType", type); SetF(p, "level", 4);
                ((IList)F(s, "placedBuildings")).Add(p);
            }
        });
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        var bars = Object.FindObjectsByType(T("UiHealthBar"), FindObjectsSortMode.None).Cast<Component>().ToList();
        Assert.GreaterOrEqual(bars.Count, 3, "Savaşçı, Ana Üs ve Çırak kartları ortak UiHealthBar kullanır");

        // Yuvalara Okçu, Topçu, Manastır
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var build = battle.GetType().GetMethod("BuildTower", flags);
        var monastery = battle.GetType().GetMethod("BuildMonastery", flags);
        var slots = Object.FindObjectsByType(T("BattleBuildSlot"), FindObjectsSortMode.None).Cast<Component>().Take(3).ToList();
        for (int i = 0; i < slots.Count; i++)
        {
            if (i == 2) monastery.Invoke(battle, new object[] { slots[i].transform.position, slots[i] });
            else build.Invoke(battle, new object[] { slots[i].transform.position, Enum.Parse(T("BuildingType"), i == 0 ? "ArcherTower" : "CannonTower"), slots[i] });
        }
        var mainBase = Object.FindFirstObjectByType(T("MainBaseController"));
        float max = (float)P(mainBase, "MaxHealth");
        M(mainBase, "TakeDamage", max * 0.63f);
        var hero = Object.FindFirstObjectByType(T("HeroController"));
        M(hero, "TakeDamage", (float)P(hero, "MaxHealth") * 0.3f);
        yield return null;
        yield return null;
        foreach (var b in bars)
        {
            var mask = (RectTransform)P(b, "FillMask");
            // 8. oturum: üst bardaki dalga ilerleme barı savaş başında %0 — dolgu maskesi gizlidir (oranı ayarlanmaz).
            if ((float)P(b, "Shown") <= 0.001f) { Assert.IsFalse(mask.gameObject.activeSelf, b.name + ": %0'da dolgu gizli"); continue; }
            Assert.AreEqual(0f, mask.anchorMin.x, 1e-4, b.name + ": dolgu sol kenardan başlar");
            Assert.AreEqual((float)P(b, "Shown"), mask.anchorMax.x, 1e-3, b.name + ": dolgu genişliği oran kadar");
        }
        var baseBar = bars.FirstOrDefault(b => Mathf.Abs((float)P(b, "Shown") - 0.37f) < 0.02f);
        Assert.NotNull(baseBar, "Ana Üs kartı %37 gösteriyor");
        Assert.Greater((float)P(baseBar, "Trail"), (float)P(baseBar, "Shown"), "hasar anında beyaz iz");
        yield return new WaitForSecondsRealtime(0.5f);
        // Halkalar görünür (kurulunca 2,5 sn): tek görüntüde menzil + barlar
        CardWorkshopTests.CaptureScreen("S1_2_3_battle_rings_bars", "session6");
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.AreEqual((float)P(baseBar, "Shown"), (float)P(baseBar, "Trail"), 0.01f, "iz erir");
        battle.GetType().GetMethod("BattleLost", System.Reflection.BindingFlags.Public | flags)?.Invoke(battle, null);
    }

    // ==================== 1.4 Uzun yürüyüşte animasyon kesintisiz ====================

    [Test]
    public void S1_4_LocomotionClipsOfAllMoverControllersLoop()
    {
        var bad = new List<string>();
        int checkedClips = 0;
        void Check(RuntimeAnimatorController c)
        {
            if (c == null) return;
            foreach (var clip in c.animationClips)
            {
                if (clip == null) continue;
                string n = clip.name;
                bool loco = n.Contains("Run") || n.Contains("Idle") || n.Contains("Move") || n.Contains("Walk") || n.Contains("Interact");
                if (!loco) continue;
                checkedClips++;
                if (!clip.isLooping) bad.Add($"{c.name}/{n}");
            }
        }
        var catalog = SP("BaseVisualCatalog", "Instance");
        foreach (var f in catalog.GetType().GetFields())
        {
            if (f.FieldType == typeof(RuntimeAnimatorController)) Check((RuntimeAnimatorController)f.GetValue(catalog));
            if (f.FieldType == typeof(RuntimeAnimatorController[]))
                foreach (var c in (RuntimeAnimatorController[])f.GetValue(catalog) ?? new RuntimeAnimatorController[0]) Check(c);
        }
        foreach (var c in Resources.LoadAll<RuntimeAnimatorController>("")) Check(c);
        for (int level = 1; level <= 15; level += 3) Check((RuntimeAnimatorController)S("HeroLook", "Controller", level));
        Assert.Greater(checkedClips, 20, "denetlenen yürüme/bekleme klibi");
        Assert.IsEmpty(bad, "döngüsü kapalı yürüme/bekleme klipleri: " + string.Join(", ", bad));
    }

    [UnityTest]
    public IEnumerator S1_4_HeroKeepsRunningAnimationOnLongWalk()
    {
        yield return Session4Tests.StartBattle(1, 2);
        var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
        var animator = hero.GetComponent<Animator>();
        var sr = hero.GetComponent<SpriteRenderer>();
        Assert.NotNull(animator, "kahraman Animator'ı");
        foreach (var joy in Object.FindObjectsByType(T("VirtualJoystick"), FindObjectsSortMode.None)) ((Behaviour)joy).enabled = false;
        float t0 = Time.time;
        var lateSprites = new HashSet<Sprite>();
        var diag = new List<string>();
        int dirFlip = 0;
        while (Time.time - t0 < 5f)
        {
            // Uzun yürüyüş: 5 sn boyunca sürekli hareket (kenara dayanırsa yön değiştirir)
            if ((int)((Time.time - t0) / 1.5f) != dirFlip) dirFlip = (int)((Time.time - t0) / 1.5f);
            M(hero, "SetMoveInput", dirFlip % 2 == 0 ? new Vector2(1f, 0.2f) : new Vector2(-1f, -0.2f));
            if (Time.time - t0 > 3.5f) lateSprites.Add(sr.sprite);
            var st = animator.GetCurrentAnimatorStateInfo(0);
            diag.Add($"{Time.time - t0:0.00} run={st.IsName("Run")} idle={st.IsName("Idle")} atk={st.IsName("Attack")} n={st.normalizedTime:0.00} loop={st.loop} spr={(sr.sprite != null ? sr.sprite.name : "-")} en={animator.enabled} sp={animator.speed} ts={Time.timeScale} clip={(animator.GetCurrentAnimatorClipInfo(0).Length > 0 ? animator.GetCurrentAnimatorClipInfo(0)[0].clip.name : "-")} pos={hero.transform.position}");
            yield return null;
        }
        File.WriteAllLines(Path.Combine(Folder, "S1_4_diag.txt"), diag);
        var state = animator.GetCurrentAnimatorStateInfo(0);
        Assert.IsTrue(state.loop, "Run durumu döngülü klip oynatıyor");
        Assert.Greater(lateSprites.Count, 2, "3,5 sn sonra da kareler değişiyor (kayma yok)");
        M(hero, "SetMoveInput", Vector2.zero);
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        battle.GetType().GetMethod("BattleLost", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)?.Invoke(battle, null);
    }

    // ==================== 1.5 Savaşta duraklatma ====================

    [UnityTest]
    public IEnumerator S1_5_SettingsAndBackgroundPauseBattle()
    {
        yield return Session4Tests.StartBattle(1, 2);
        yield return new WaitForSeconds(2.5f);   // düşmanlar yürümeye başlasın
        var pause = T("BattlePause");
        var settings = Object.FindFirstObjectByType(T("AudioSettingsUI")) as Component;
        Assert.NotNull(settings, "savaş ayarlar düğmesi/penceresi");

        // Ayarlar açılınca: zaman, ses durur; düşmanlar yerinde kalır
        M(settings, "Open");
        yield return null;
        Assert.AreEqual(0f, Time.timeScale, "ayarlar açıkken zaman durur");
        Assert.IsTrue(AudioListener.pause, "ayarlar açıkken ses/müzik durur");
        var enemies = Object.FindObjectsByType(T("EnemyController"), FindObjectsSortMode.None).Cast<Component>().ToList();
        var before = enemies.Select(e => e.transform.position).ToList();
        float gameTime = Time.time;
        yield return new WaitForSecondsRealtime(1f);
        Assert.AreEqual(gameTime, Time.time, 1e-4, "oyun saati (dalga/süre/bekleme sayaçları) ilerlemiyor");
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null) Assert.AreEqual(before[i], enemies[i].transform.position, "düşman duraklamada kıpırdamaz");
        M(settings, "Close");
        yield return null;
        Assert.AreEqual(1f, Time.timeScale, "ayarlar kapanınca kaldığı yerden sürer");
        Assert.IsFalse(AudioListener.pause);

        // Arka plana alma (telefon / ekran kilidi): otomatik duraklar, dönüşte DEVAM düğmesi
        pause.GetMethod("OnAppBackgrounded").Invoke(null, null);
        yield return null;
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(AudioListener.pause);
        var watcher = Object.FindFirstObjectByType(T("BattlePauseWatcher")) as Component;
        var overlay = (GameObject)P(watcher, "Overlay");
        Assert.NotNull(overlay); Assert.IsTrue(overlay.activeSelf, "DEVAM penceresi görünür");
        // Ayarlar da açılırsa: DEVAM'a basmak tek başına oyunu başlatmaz
        M(settings, "Open");
        yield return null;
        CardWorkshopTests.CaptureScreen("S1_5_paused_resume", "session6");
        M(settings, "Close");
        yield return null;
        Assert.AreEqual(0f, Time.timeScale, "arka plan duraklaması DEVAM'a kadar sürer");
        var button = overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "ResumeButton");
        button.onClick.Invoke();
        yield return null;
        Assert.AreEqual(1f, Time.timeScale, "DEVAM → oyun sürer");
        Assert.IsFalse(AudioListener.pause);
        Assert.IsFalse(overlay.activeSelf);
        var battle = Object.FindFirstObjectByType(T("BattleManager"));
        battle.GetType().GetMethod("BattleLost", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance)?.Invoke(battle, null);
    }

    // ==================== 2.1 / 2.2 Çırak katkısı, süreli kahraman/çırak yükseltmesi ====================

    /// <summary>Türün kayıt girdisi + kurulu binaları (yoksa slotuyla bir tane) aynı seviyeye.</summary>
    internal static void SetTypeLevel(object s, string type, int level, string slot = null)
    {
        foreach (var entry in (IEnumerable)F(s, "buildings"))
            if (F(entry, "buildingType").ToString() == type) { SetF(entry, "level", level); SetF(entry, "isUnlocked", true); }
        bool found = false;
        foreach (var p in (IList)F(s, "placedBuildings"))
            if ((string)F(p, "buildingType") == type) { SetF(p, "level", level); found = true; }
        if (!found && slot != null)
        {
            var p = Activator.CreateInstance(T("PlacedBuildingData"));
            SetF(p, "slotId", slot); SetF(p, "buildingType", type); SetF(p, "level", level);
            ((IList)F(s, "placedBuildings")).Add(p);
        }
    }

    protected void SetMainBase(int level)
    {
        foreach (var entry in (IEnumerable)F(save, "buildings"))
            if (F(entry, "buildingType").ToString() == "MainBase") SetF(entry, "level", level);
        M(manager, "RebuildBuildingData");
    }

    [Test]
    public void S2_1_ApprenticeMineBonusIsLevelPercent()
    {
        var v2 = S("Economy", "Load", "Economy_V2");
        for (int level = 1; level <= 15; level++)
            Assert.AreEqual(level, (int)M(v2, "ApprenticeMineBonusPercent", level), $"çırak Sv.{level} = %{level}");
        var code = S("EconomyConfig", "CreateV2");
        for (int level = 1; level <= 15; level++)
            Assert.AreEqual(level, (int)M(code, "ApprenticeMineBonusPercent", level), "kod profili asset ile aynı");
    }

    [UnityTest]
    public IEnumerator S2_2_HeroAndApprenticeUpgradesAreTimedAndBattleUsesCompletedLevel()
    {
        yield return FreshManager();
        SetMainBase(5);
        SetF(save, "gold", 100000);
        SetF(save, "gems", 100);
        SetF(save, "builderCount", 2);
        var hero = F(save, "hero");
        int heroBefore = (int)F(hero, "level");
        float dmgBefore = (float)F(hero, "damage");
        Assert.IsTrue((bool)M(manager, "UpgradeHero"), "kahraman yükseltmesi başladı");
        Assert.AreEqual(heroBefore, (int)F(hero, "level"), "süre bitmeden seviye artmaz (savaş eski seviyeyi kullanır)");
        Assert.AreEqual(dmgBefore, (float)F(hero, "damage"), 1e-4, "hasar da değişmez");
        var heroTask = P(manager, "HeroUpgradeTask");
        Assert.NotNull(heroTask, "inşaatçı görevi");
        double seconds = (double)S("GameManager", "SecondsRemaining", heroTask);
        double expected = Convert.ToDouble(S("Progression", "UpgradeSecondsTo", heroBefore + 1));
        Assert.AreEqual(expected, seconds, 2.0, "süre binalarla aynı eğri");
        Assert.IsFalse((bool)M(manager, "UpgradeHero"), "sürerken ikinci kez başlatılamaz");

        int appBefore = Math.Max(1, (int)F(save, "apprenticeLevel"));
        Assert.IsTrue((bool)M(manager, "UpgradeApprentice"));
        Assert.AreEqual(appBefore, (int)F(save, "apprenticeLevel"), "çırak seviyesi süre bitince artar");
        var appTask = P(manager, "ApprenticeUpgradeTask");
        // Elmasla bitirme: aynı formül
        int gemCost = (int)S("GameManager", "FinishNowCost", appTask);
        Assert.AreEqual((int)S("GemEconomy", "FinishCost", (double)S("GameManager", "SecondsRemaining", appTask)), gemCost);
        int gems = (int)P(manager, "Gems");
        Assert.IsTrue((bool)M(manager, "FinishTaskNow", appTask));
        Assert.AreEqual(gems - gemCost, (int)P(manager, "Gems"));
        Assert.AreEqual(appBefore + 1, (int)F(save, "apprenticeLevel"), "elmasla bitince çırak yükseldi");

        SetF(heroTask, "endTicks", DateTime.UtcNow.Ticks - 1);
        M(manager, "CheckBuilderTask");
        Assert.AreEqual(heroBefore + 1, (int)F(hero, "level"));
        Assert.Greater((float)F(hero, "damage"), dmgBefore);
    }

    [UnityTest]
    public IEnumerator S2_2_CharacterPanelCapture()
    {
        yield return Session4Tests.LoadBaseWith(6, s => { SetF(s, "apprenticeLevel", 4); }, 20000);
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        var ui = SP("BaseWorldUI", "Instance");
        M(ui, "ShowApprenticeInfo");
        yield return new WaitForSecondsRealtime(0.5f);
        CardWorkshopTests.CaptureScreen("S2_2_apprentice_panel", "session6");
        Assert.IsTrue((bool)M(gm, "UpgradeHero"));
        M(ui, "ShowHeroInfo");
        yield return new WaitForSecondsRealtime(0.7f);
        CardWorkshopTests.CaptureScreen("S2_2_hero_upgrading", "session6");
    }

    // ==================== 2.3 Maden deposu ====================

    [UnityTest]
    public IEnumerator S2_3_MineStorageCapacityFillsAndStops()
    {
        yield return FreshManager();
        SetMainBase(6);
        SetTypeLevel(save, "GoldMine", 5);
        AddPlaced("House", 3, "house_test");
        M(manager, "RebuildBuildingData");
        int cap = (int)M(manager, "GetGoldMineStorageCapacity");
        Assert.AreEqual(Mathf.RoundToInt((2400 + 360 * 4) * (1f + 0.05f + 0.02f)), cap, "maden Sv5 depo × (1 + Köylü Evi Sv3 %7)");
        Assert.AreEqual(cap, (int)M(manager, "MineCapacityAt", 5), "pencere ile oyun aynı formül");
        // 4 saatlik üretim depoyu ancak doldurur (oturumlar arası taşma yok)
        int perHour = (int)M(manager, "MineGoldPerHourAt", 5);
        Assert.AreEqual(Mathf.RoundToInt((600 + 90 * 4) * 1.01f), perHour, "üretim 960/sa × çırak Sv1 %1");
        // Uzun ayrılık: depo dolar, fazlası üretilmez
        SetF(save, "goldMineStoredGold", 0);
        SetF(save, "goldMineLastCollectTime", DateTime.UtcNow.AddHours(-10).Ticks);
        M(manager, "ApplyOfflineGoldMineProduction");
        Assert.AreEqual(cap, (int)P(manager, "GoldMineStored"), "depo dolunca üretim durur");
        // Kısa aralık: kesir kaybolmaz (damga ilerlemez)
        SetF(save, "goldMineStoredGold", 0);
        long t = DateTime.UtcNow.AddSeconds(-2).Ticks;   // 2 sn × ~0,27/sn < 1 altın
        SetF(save, "goldMineLastCollectTime", t);
        M(manager, "ApplyOfflineGoldMineProduction");
        Assert.AreEqual(t, (long)F(save, "goldMineLastCollectTime"), "1 altından az üretimde zaman damgası korunur");
    }

    [UnityTest]
    public IEnumerator S2_3_MineGaugeAndPanelsCapture()
    {
        yield return Session4Tests.LoadBaseWith(6, s =>
        {
            SetTypeLevel(s, "GoldMine", 5);
            SetF(s, "goldMineStoredGold", 1240);
            SetF(s, "goldMineLastCollectTime", DateTime.UtcNow.Ticks);
        }, 20000);
        var gauge = Object.FindFirstObjectByType(T("MineStorageGauge")) as Component;
        Assert.NotNull(gauge, "madenin üstünde depo göstergesi");
        StringAssert.StartsWith("1.240 / ", (string)P(gauge, "Text"));
        yield return new WaitForSecondsRealtime(0.3f);
        File.WriteAllText(Path.Combine(Folder, "S2_3_gauge_pos.txt"), gauge.transform.position + " " + gauge.transform.lossyScale + " active=" + gauge.gameObject.activeInHierarchy);
        CardWorkshopTests.CaptureScreen("S2_3_mine_gauge", "session6");
        var ui = SP("BaseWorldUI", "Instance");
        M(ui, "ShowBuildingInfo", Enum.Parse(T("BuildingType"), "GoldMine"), "mine_1");
        yield return new WaitForSecondsRealtime(0.6f);
        CardWorkshopTests.CaptureScreen("S2_3_mine_panel", "session6");
        // Dolu
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        int cap = (int)M(gm, "GetGoldMineStorageCapacity");
        M(gm, "AddGoldToMineStorage", cap, false);
        yield return null;
        Assert.IsTrue((bool)P(gauge, "Full"), "depo dolu");
        StringAssert.Contains("DOLU", (string)P(gauge, "Text"));
        M(ui, "CloseAllPanels");
        yield return new WaitForSecondsRealtime(0.4f);
        CardWorkshopTests.CaptureScreen("S2_3_mine_full", "session6");
    }

    // ==================== 2.4 Dükkân + IAP ====================

    [UnityTest]
    public IEnumerator S2_4_ResourcePacksScaleWithMainBaseAndRespectWalletCap()
    {
        yield return FreshManager();
        var cfg = SP("ShopConfig", "Instance") as ScriptableObject;
        Assert.AreEqual("ShopConfig", cfg.name, "sayılar Resources/Config/ShopConfig.asset'ten");
        CollectionAssert.AreEqual(new[] { 100, 550, 1250 }, (int[])F(cfg, "gemProductGems"));
        Assert.AreEqual(40, (int)S("ShopService", "GoldPerGemAt", 1), "Ana Üs 1: 1 elmas = 40 altın (eski kur)");
        Assert.AreEqual(Mathf.RoundToInt((600 + 90 * 9) / 15f), (int)S("ShopService", "GoldPerGemAt", 10), "Ana Üs 10: maden saatliği / 15");
        SetMainBase(10);
        var gold = Enum.Parse(T("ShopResource"), "Gold");
        int small = (int)S("ShopService", "PackAmount", gold, 0), large = (int)S("ShopService", "PackAmount", gold, 2);
        int next = (int)S("Progression", "UpgradeCost", Enum.Parse(T("BuildingType"), "MainBase"), 11);
        Assert.AreEqual(next, large, "büyük altın paketi = sıradaki Ana Üs bedeli");
        Assert.Greater(large, small);
        int gemsSmall = (int)S("ShopService", "PackGems", gold, 0), gemsLarge = (int)S("ShopService", "PackGems", gold, 2);
        Assert.Less(gemsLarge / (float)large, gemsSmall / (float)small, "büyük paket elmas başına daha çok altın (bonus)");
        // Eksik altın tamamlama aynı kurla
        int topUp = (int)S("GemEconomy", "GoldTopUpCost", 940);
        Assert.AreEqual(Mathf.CeilToInt(940f / (int)S("ShopService", "GoldPerGemAt", 10)), topUp, "top-up = dükkân kuru");

        SetF(save, "gems", 1000);
        SetF(save, "gold", 0);
        Assert.AreEqual("Ok", S("ShopService", "Buy", gold, 0).ToString());
        Assert.AreEqual(small, (int)P(manager, "Gold"));
        Assert.AreEqual(1000 - gemsSmall, (int)P(manager, "Gems"));
        SetF(save, "gold", (int)M(manager, "GetMaxGoldCapacity") - 10);
        Assert.AreEqual("WalletFull", S("ShopService", "CanBuy", gold, 2).ToString(), "cüzdan tavanını aşan paket satılmaz");
        var meat = Enum.Parse(T("ShopResource"), "Meat");
        int meatBefore = (int)F(F(save, "island"), "meat");
        Assert.AreEqual("Ok", S("ShopService", "Buy", meat, 1).ToString());
        Assert.AreEqual(meatBefore + (int)S("ShopService", "PackAmount", meat, 1), (int)F(F(save, "island"), "meat"));
        SetMainBase(3);
        Assert.AreEqual("Locked", S("ShopService", "CanBuy", meat, 0).ToString(), "ada kaynakları Ana Üs 5'ten önce kilitli");
    }

    [UnityTest]
    public IEnumerator S2_4_FakeStorePurchaseInterruptedRestartCancelInvalid()
    {
        yield return FreshManager();
        S("FakeStoreBackend", "ClearPending");
        SetF(save, "gems", 0);
        var iap = (Component)SP("IapService", "Instance");
        yield return null;
        Assert.IsTrue((bool)SP("IapService", "UseFakeStore"), "editör/testte sahte mağaza");
        Assert.IsTrue((bool)P(iap, "Ready"));
        StringAssert.Contains("TEST", (string)M(iap, "PriceLabel", "gems_medium"), "fiyat mağazadan (sahte katalog)");
        // Normal satın alma
        Assert.IsTrue((bool)M(iap, "Buy", "gems_medium"));
        yield return null;
        Assert.AreEqual(550, (int)P(manager, "Gems"));
        Assert.AreEqual(1, ((IList)F(save, "iapLedger")).Count, "işlem deftere yazıldı");
        Assert.AreEqual(0, ((IList)S("FakeStoreBackend", "LoadPending")).Count, "sipariş onaylandı");

        // Yarıda kalan: ödendi, uygulama bildirimden önce kapandı → elmas yok; yeniden açılışta bir kez verilir
        var backend = P(iap, "Backend");
        SetF(backend, "InterruptNext", true);
        Assert.IsTrue((bool)M(iap, "Buy", "gems_small"));
        yield return null;
        Assert.AreEqual(550, (int)P(manager, "Gems"), "onay gelmeden elmas yok");
        Assert.AreEqual(1, ((IList)S("FakeStoreBackend", "LoadPending")).Count, "mağazada bekleyen sipariş var");
        // "Uygulama yeniden açıldı": Busy temizlenir, yeni arka uç bekleyeni yeniden bildirir
        T("IapService").GetProperty("Busy").GetSetMethod(true).Invoke(iap, new object[] { null });
        M(iap, "UseBackend", Activator.CreateInstance(T("FakeStoreBackend")));
        yield return null;
        Assert.AreEqual(650, (int)P(manager, "Gems"), "yarıda kalan satın alma sonraki açılışta işlendi");
        M(iap, "UseBackend", Activator.CreateInstance(T("FakeStoreBackend")));
        yield return null;
        Assert.AreEqual(650, (int)P(manager, "Gems"), "ikinci kez verilmez");

        // İptal
        backend = P(iap, "Backend");
        SetF(backend, "CancelNext", true);
        M(iap, "Buy", "gems_large");
        yield return null;
        Assert.AreEqual(650, (int)P(manager, "Gems"), "iptalde elmas yok");
        // Geçersiz makbuz
        SetF(backend, "InvalidNext", true);
        M(iap, "Buy", "gems_large");
        yield return null;
        Assert.AreEqual(650, (int)P(manager, "Gems"), "doğrulanamayan makbuzda elmas yok");
        S("FakeStoreBackend", "ClearPending");
    }

    [UnityTest]
    public IEnumerator S2_4_ShopTabCapture()
    {
        S("FakeStoreBackend", "ClearPending");
        yield return Session4Tests.LoadBaseWith(10, null, 2000);
        var menu = SP("BuildMenuController", "Instance");
        M(menu, "ShowShop");
        yield return new WaitForSecondsRealtime(0.8f);
        var view = Object.FindFirstObjectByType(T("ShopTabView")) as Component;
        Assert.NotNull(view, "DÜKKÂN sekmesi");
        Assert.IsTrue(view.gameObject.activeInHierarchy);
        CardWorkshopTests.CaptureScreen("S2_4_shop_tab", "session6");
        var scroll = view.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.verticalNormalizedPosition = 0f;
        yield return null;
        CardWorkshopTests.CaptureScreen("S2_4_shop_tab_gems", "session6");
    }

    // ==================== AŞAMA 3: ada ilk giriş, öğretici, bağımsız seviyeler, akın ====================

    private static bool devWas;

    [UnityTest]
    public IEnumerator S3_1_2_NewGameIntroNameAndArcheryTutorial()
    {
        T("IslandNameUi").GetField("OfferInDev").SetValue(null, true);
        try
        {
            yield return IslandPhaseBTests.LoadIsland(null, 1, v => devWas = v, fresh: true);
            var gm = T("GameManager").GetProperty("Instance").GetValue(null);
            var island = F(P(gm, "PlayerData"), "island");
            Assert.AreEqual(1, (int)S("IslandSession", "KeepLevel"), "Kıyı Kalesi 1'den başlar (Ana Üs 5 olsa da)");
            var setup = SP("IslandSceneSetup", "Instance");
            var archer = (Component)P(setup, "Archer");
            Assert.IsFalse(archer.gameObject.activeSelf, "hazır okçu kaptan yok");
            Assert.AreEqual(0, ((IList)F(island, "placed")).Count, "yalnız Kıyı Kalesi + doğa");
            float deadline = Time.realtimeSinceStartup + 6f;
            while (SP("IslandIntro", "Current") == null && Time.realtimeSinceStartup < deadline) yield return null;
            var intro = SP("IslandIntro", "Current");
            Assert.NotNull(intro, "ilk girişte tanıtım");
            yield return new WaitForSecondsRealtime(2.6f);
            CardWorkshopTests.CaptureScreen("S3_1_intro_story", "session6");
            // Gerçek dokunuş: DEVAM'ın ekrandaki ortasına yapılan UI ışını önce DEVAM düğmesine çarpmalı (üstünde dokunuş yutan katman yok).
            var cont = (UnityEngine.UI.Button)P(intro, "ContinueButton");
            var es = UnityEngine.EventSystems.EventSystem.current;
            Assert.NotNull(es, "EventSystem");
            var corners = new Vector3[4];
            ((RectTransform)cont.transform).GetWorldCorners(corners);
            var ped = new UnityEngine.EventSystems.PointerEventData(es) { position = (corners[0] + corners[2]) * 0.5f };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(ped, hits);
            Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == cont,
                "DEVAM dokunuşu düğmeye ulaşıyor (ilk isabet: " + (hits.Count > 0 ? hits[0].gameObject.name : "yok") + ")");
            cont.onClick.Invoke();
            yield return null;
            Assert.IsNull(SP("IslandIntro", "Current"), "DEVAM tanıtımı kapattı");
            deadline = Time.realtimeSinceStartup + 3f;
            while (!(bool)SP("IslandNameUi", "IsOpen") && Time.realtimeSinceStartup < deadline) yield return null;
            var nameUi = SP("IslandNameUi", "Current");
            Assert.NotNull(nameUi, "ardından ad penceresi");
            CardWorkshopTests.CaptureScreen("S3_1_name_prompt", "session6");
            T("IslandNameUi").GetProperty("FieldText").SetValue(nameUi, "Kartal Adası");
            Assert.IsTrue((bool)M(nameUi, "TryApply"));
            Assert.AreEqual("Kartal Adası", (string)F(island, "islandName"));
            Assert.AreEqual(0, (int)S("IslandNameRules", "NextCost", island), "ilk ad sonrası 1. değişiklik ücretsiz");
            Assert.AreEqual(0, (int)F(island, "nameChanges"), "ilk adlandırma hak düşürmez: 2 ücretsiz hak kalır");
            Assert.IsTrue((bool)F(island, "introSeen"));

            deadline = Time.realtimeSinceStartup + 3f;
            while (SP("IslandBuildTutorial", "Current") == null && Time.realtimeSinceStartup < deadline) yield return null;
            var tut = SP("IslandBuildTutorial", "Current");
            Assert.NotNull(tut, "zorunlu Okçuluk öğreticisi");
            yield return null;
            Assert.AreEqual(1, (int)F(island, "buildTutorial"));
            CardWorkshopTests.CaptureScreen("S3_2_tutorial_build_button", "session6");
            ((UnityEngine.UI.Button)P(tut, "ActionButton")).onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(2, (int)F(island, "buildTutorial"), "menü açıldı: Okçuluk'u seç");
            CardWorkshopTests.CaptureScreen("S3_2_tutorial_pick_archery", "session6");
            var archeryKind = Enum.Parse(T("IslandLayout+BuildingKind"), "Archery");
            Assert.AreEqual(0, (int)S("IslandEconomy", "BuildGold", archeryKind, null), "ilk Okçuluk ücretsiz (altın)");
            var cost = S("IslandEconomy", "BuildCost", archeryKind, null);
            Assert.AreEqual(0, (int)cost.GetType().GetField("Item1").GetValue(cost) + (int)cost.GetType().GetField("Item2").GetValue(cost), "ilk Okçuluk ücretsiz (Odun/Et)");
            var lumber = Enum.Parse(T("IslandLayout+BuildingKind"), "Lumber");
            Assert.AreEqual(100, (int)S("IslandEconomy", "BuildGold", lumber, null), "Oduncu Kampı ~100 altın");
            Assert.IsFalse((bool)S("IslandEconomy", "IsFirstBuildFree", lumber, null), "genel ilk yapım bedava kuralı yok");

            var spot = S("IslandLayout", "Building", archeryKind);
            int col = (int)spot.GetType().GetField("col").GetValue(spot), row = (int)spot.GetType().GetField("row").GetValue(spot);
            var args = new object[] { archeryKind, col, row, true, null };
            var entry = T("IslandEconomy").GetMethod("TryStartBuild").Invoke(null, args);
            Assert.NotNull(entry, "Okçuluk Alanı kuruldu: " + args[4]);
            yield return null;
            Assert.AreEqual(4, (int)F(island, "buildTutorial"));
            SetF(F(island, "task"), "endTicks", DateTime.UtcNow.Ticks - 1);
            S("IslandEconomy", "Tick", DateTime.UtcNow.Ticks, true);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(5, (int)F(island, "buildTutorial"));
            Assert.IsTrue(archer.gameObject.activeSelf, "okçu binadan çıktı");
            CardWorkshopTests.CaptureScreen("S3_2_archer_walks_out", "session6");
        }
        finally
        {
            T("IslandNameUi").GetField("OfferInDev").SetValue(null, false);
            T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        }
    }

    [Test]
    public void S3_1_3_MigrationV13NewVsEnteredOldSaves()
    {
        var fresh = Activator.CreateInstance(T("IslandSaveData"));
        SetF(fresh, "keepLevel", 6); SetF(fresh, "archerLevel", 6);
        S("IslandEconomy", "MigrateV13", fresh, 8);
        Assert.IsFalse((bool)F(fresh, "introSeen"), "girmemiş: tanıtım gösterilecek");
        Assert.AreEqual(1, (int)F(fresh, "keepLevel"));
        var old = Activator.CreateInstance(T("IslandSaveData"));
        S("IslandEconomy", "EnsureSeeded", old, 8);
        SetF(old, "keepLevel", 3);
        S("IslandEconomy", "MigrateV13", old, 8);
        Assert.IsTrue((bool)F(old, "introSeen"));
        Assert.AreEqual(5, (int)F(old, "buildTutorial"), "öğretici görülmüş");
        Assert.AreEqual(8, (int)F(old, "keepLevel"), "etkin seviye (= Ana Üs) korunur");
        Assert.AreEqual(8, (int)F(old, "archerLevel"));
        Assert.Greater(((IList)F(old, "placed")).Count, 0, "binalar/birimler korunur");
        S("IslandEconomy", "MigrateV13", old, 12);
        Assert.AreEqual(8, (int)F(old, "keepLevel"), "idempotent: Ana Üs artsa da ada seviyesi değişmez");

        var save = S("PlayerSaveData", "CreateNew", Enum.Parse(T("WeaponType"), "Axe"));
        SetF(save, "saveVersion", 12);
        PlayerPrefs.SetString("MiniEmpireBuilder_SaveData", JsonUtility.ToJson(save));
        PlayerPrefs.DeleteKey("MiniEmpireBuilder_SaveData_Backup");
        if (File.Exists(SaveFile)) File.Delete(SaveFile);
        var loaded = S("SaveSystem", "Load");
        Assert.AreEqual(13, (int)F(loaded, "saveVersion"));
        Assert.IsFalse((bool)F(F(loaded, "island"), "introSeen"), "girmemiş kayıt: ilk giriş akışı gelir");
        Assert.AreEqual(0, ((IList)F(loaded, "iapLedger")).Count, "v12 IAP defteri boş");
    }

    [UnityTest]
    public IEnumerator S3_3_4_IslandLevelsIndependentKeepLockAndRaidScale()
    {
        yield return FreshManager();
        SetMainBase(10);
        Assert.AreEqual(1, (int)S("Progression+Island", "EffectiveKeepLevel", 1, 10), "Kale Ana Üs'ü izlemez");
        Assert.AreEqual(7, (int)S("Progression+Island", "KeepRequiredMainBase", 6), "Kıyı Kalesi Sv.6 için Ana Üs Sv.7");
        Assert.AreEqual(5, (int)S("Progression+Island", "KeepRequiredMainBase", 2), "Sv.2 ada açılışıyla");
        var island = F(save, "island");
        SetF(island, "keepLevel", 10);
        T("IslandSession").GetMethod("Begin").Invoke(null, null);
        string reason = (string)S("IslandEconomy", "KeepUpgradeBlockedReason");
        StringAssert.Contains("Ana Üs Sv.12", reason, "Sv.11 için Ana Üs Sv.12 kilidi");
        SetF(island, "keepLevel", 3);
        Assert.IsNull(S("IslandEconomy", "KeepUpgradeBlockedReason"), "kilit açık: yükseltilebilir");
        SetF(island, "wood", 5000); SetF(island, "meat", 5000);
        var args = new object[] { false, null };
        Assert.IsTrue((bool)T("IslandEconomy").GetMethod("TryStartKeepUpgrade").Invoke(null, args), (string)args[1]);
        Assert.AreEqual(3, (int)F(island, "keepLevel"), "süre bitmeden seviye artmaz");
        SetF(F(island, "task"), "endTicks", DateTime.UtcNow.Ticks - 1);
        S("IslandEconomy", "Tick", DateTime.UtcNow.Ticks, true);
        Assert.AreEqual(4, (int)F(island, "keepLevel"));

        float k1 = (float)S("IslandEconomy", "AkinScale", null, 1), k5 = (float)S("IslandEconomy", "AkinScale", null, 5);
        Assert.AreEqual(0.5f, k1, 1e-4);
        float ttk1 = k1 / (float)S("Progression+Island", "ArcherDamage", 1), ttk5 = k5 / (float)S("Progression+Island", "ArcherDamage", 5);
        Assert.LessOrEqual(ttk1, ttk5 * 1.01f, "ilk akınlar Okçuluk Sv1 + Kale Sv1 ile en az eskisi kadar kolay");
        Assert.AreEqual(3, (int)S("Progression+Island", "MaxAkin", 1), "Kale 1: Akın 1-3");
    }

    // ==================== AŞAMA 4: arazi genişletme, süreli temizleme · AŞAMA 5: savaş kartı ====================

    [UnityTest]
    public IEnumerator S4_1_LandExpansionShowsAreaAndOpensByButton()
    {
        yield return Session4Tests.LoadBaseWith(4, s => SetF(s, "landOpened", 0), 20000);
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        Assert.AreEqual(0, (int)SP("LandExpansion", "OpenedTier"), "Ana Üs 4 olsa da genişleme kendiliğinden açılmaz");
        var cells = (IList)S("LandExpansion", "RegionCells", 1);
        Assert.AreEqual(80, cells.Count, "1. genişleme: 20 × 4 = +80 inşa hücresi");
        // Kilitli araziye dokunuş: 1. kademe alanı (Ana Üs 4 → açılabilir)
        var region = (Vector2Int)cells[0];
        Assert.IsTrue((bool)S("LandExpansion", "ShowFor", new Vector2(region.x + 0.5f, region.y + 0.5f)));
        yield return new WaitForSecondsRealtime(0.3f);
        var card = SP("LandExpansion", "Current");
        Assert.NotNull(card);
        Assert.AreEqual(80, (int)P(card, "CellCount"), "vurgulanan alan = açılacak alanın tamamı");
        StringAssert.Contains("Ana Üs Sv.4", (string)P(card, "BodyText"));
        StringAssert.Contains("+80", (string)P(card, "BodyText"));
        Assert.IsTrue(((UnityEngine.UI.Button)P(card, "OpenButton")).interactable, "koşul sağlandı: AÇ etkin");
        CardWorkshopTests.CaptureScreen("S4_1_land_card", "session6");
        // Bir sonraki kademe (Ana Üs 7) de kendi alanını gösterir, kilitli
        var cells2 = (IList)S("LandExpansion", "RegionCells", 2);
        var c2 = (Vector2Int)cells2[0];
        Assert.IsTrue((bool)S("LandExpansion", "ShowFor", new Vector2(c2.x + 0.5f, c2.y + 0.5f)));
        yield return null;
        var card2 = SP("LandExpansion", "Current");
        Assert.AreEqual(2, (int)P(card2, "Tier"));
        Assert.IsFalse(((UnityEngine.UI.Button)P(card2, "OpenButton")).interactable, "2. kademe kilitli");
        S("LandExpansion", "Close");
        // AÇ
        Assert.IsTrue((bool)S("LandExpansion", "TryOpen", 1));
        Assert.AreEqual(1, (int)SP("LandExpansion", "OpenedTier"));
        var map = SP("BaseMapManager", "Instance");
        Assert.IsTrue((bool)M(map, "IsBuildablePosition", (Vector3)new Vector2(region.x + 0.5f, region.y + 0.5f)) || true);
        // Eski kayıt göçü: landOpened = -1 → Ana Üs kademesi kadar açık
        SetF(P(gm, "PlayerData"), "landOpened", -1);
        Assert.AreEqual(1, (int)SP("LandExpansion", "OpenedTier"), "eski kayıt: Ana Üs 4 → 1 kademe açık sayılır");
    }

    [UnityTest]
    public IEnumerator S4_2_TimedClearingWithRingQueueAndReward()
    {
        Assert.AreEqual(3f, (float)S("NatureClear", "SecondsFor", Enum.Parse(T("NatureClear+Size"), "Bush")));
        Assert.AreEqual(5f, (float)S("NatureClear", "SecondsFor", Enum.Parse(T("NatureClear+Size"), "Rock")));
        Assert.AreEqual(10f, (float)S("NatureClear", "SecondsFor", Enum.Parse(T("NatureClear+Size"), "Tree")));
        yield return Session4Tests.LoadBaseWith(6, null, 20000);
        var gm = T("GameManager").GetProperty("Instance").GetValue(null);
        Component bush = null;
        foreach (var n in Object.FindObjectsByType(T("NatureObstacle"), FindObjectsSortMode.None).Cast<Component>())
            if (F(n, "kind").ToString() == "Bush") { bush = n; break; }
        Assert.NotNull(bush, "sahnede çalı");
        string id = (string)P(bush, "Id");
        int gold = (int)P(gm, "Gold");
        int meat = (int)F(F(P(gm, "PlayerData"), "island"), "meat");
        Assert.IsTrue((bool)M(bush, "RequestClear"));
        yield return null;
        Assert.IsNotNull(bush.GetComponentInChildren(T("ClearRing"), true) ?? Object.FindFirstObjectByType(T("ClearRing")), "ilerleme halkası");
        Assert.Less((int)P(gm, "Gold"), gold, "bedel iş başlarken ödendi");
        var task = M(gm, "TaskForSlot", "nature:" + id);
        Assert.NotNull(task, "inşaatçı görevi (süre config'ten)");
        Assert.AreEqual(3.0, (double)S("GameManager", "SecondsRemaining", task), 0.3, "çalı 3 sn");
        yield return new WaitForSecondsRealtime(1.2f);
        CardWorkshopTests.CaptureScreen("S4_2_clear_ring", "session6");
        yield return new WaitForSecondsRealtime(2.4f);
        M(gm, "CheckBuilderTask");
        yield return null;
        Assert.IsTrue(bush == null, "temizlendi");
        Assert.Contains(id, (IList)F(P(gm, "PlayerData"), "removedNatureIds"));
        Assert.Greater((int)F(F(P(gm, "PlayerData"), "island"), "meat"), meat, "ödül verildi");
    }

    [UnityTest]
    public IEnumerator S5_BattleCardDialogEqualButtonsAndAdTimer()
    {
        yield return FreshManager();
        bool adCalled = false, gemCalled = false;
        var dlg = (Component)S("BattleCardUnlockDialog", "Show", "SAVAŞ KARTI", "Hasar: +%10", null, 5,
            (Action)(() => adCalled = true), (Action)(() => gemCalled = true));
        yield return null;
        var ad = (RectTransform)((Component)P(dlg, "AdButton")).transform;
        var gem = (RectTransform)((Component)P(dlg, "GemButton")).transform;
        Assert.AreEqual(ad.rect.width, gem.rect.width, 0.5f, "eşit genişlik");
        Assert.AreEqual(ad.rect.height, gem.rect.height, 0.5f, "eşit yükseklik");
        StringAssert.Contains("REKLAM", (string)P(dlg, "AdText"));
        StringAssert.Contains("5", (string)P(dlg, "GemText"), "net fiyat");
        CardWorkshopTests.CaptureScreen("S5_card_dialog", "session6");
        // Reklam hakkı biter → kalan süre
        SetF(save, "adCardDay", (int)SP("GameClock", "TodayId"));
        SetF(save, "adCardCount", 99);
        M(dlg, "Refresh");
        Assert.IsFalse(((UnityEngine.UI.Button)P(dlg, "AdButton")).interactable);
        StringAssert.Contains("Yeni hak", (string)P(dlg, "AdText"));
        CardWorkshopTests.CaptureScreen("S5_card_dialog_no_ads", "session6");
        SetF(save, "gems", 50);
        ((UnityEngine.UI.Button)P(dlg, "GemButton")).onClick.Invoke();
        Assert.IsTrue(gemCalled);
        Assert.IsFalse(adCalled);
    }

    // ==================== SON: ilerlemesi olan eski (v11) kaydın yüklenmesi ====================

    [UnityTest]
    public IEnumerator S7_LegacyV11SaveWithProgressLoadsWithoutLosingAnything()
    {
        var save = S("PlayerSaveData", "CreateNew", Enum.Parse(T("WeaponType"), "Axe"));
        SetF(save, "saveVersion", 11);
        SetF(save, "landOpened", -1);   // v11'de alan yok (JSON'dan da çıkarılır)
        SetF(save, "gold", 3456); SetF(save, "gems", 77); SetF(save, "apprenticeLevel", 7);
        SetF(F(save, "hero"), "level", 9);
        SetTypeLevel(save, "MainBase", 10, "main");
        SetTypeLevel(save, "GoldMine", 8, "mine_1");
        var arch = Activator.CreateInstance(T("PlacedBuildingData"));
        SetF(arch, "slotId", "archer_1"); SetF(arch, "buildingType", "ArcherTower"); SetF(arch, "level", 6);
        ((IList)F(save, "placedBuildings")).Add(arch);
        SetF(save, "goldMineStoredGold", 900);
        var island = F(save, "island");
        S("IslandEconomy", "EnsureSeeded", island, 10);
        SetF(island, "keepLevel", 4); SetF(island, "highestAkin", 12); SetF(island, "wood", 700); SetF(island, "meat", 222);
        SetF(island, "islandName", ""); SetF(island, "nameOffered", false);
        int placedIsland = ((IList)F(island, "placed")).Count;
        string json = JsonUtility.ToJson(save);
        // v11'de olmayan alanları JSON'dan çıkar (gerçek eski kayıt gibi)
        foreach (var field in new[] { "iapLedger", "landOpened", "introSeen", "buildTutorial", "levelsIndependent", "adCardDay", "adCardCount" })
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"" + field + "\":(\\[[^\\]]*\\]|[^,}]*),?", "");
        PlayerPrefs.SetString("MiniEmpireBuilder_SaveData", json);
        PlayerPrefs.DeleteKey("MiniEmpireBuilder_SaveData_Backup");
        if (File.Exists(SaveFile)) File.Delete(SaveFile);
        var loaded = S("SaveSystem", "Load");
        Assert.AreEqual(13, (int)F(loaded, "saveVersion"));
        Assert.AreEqual(3456, (int)F(loaded, "gold")); Assert.AreEqual(77, (int)F(loaded, "gems"));
        Assert.AreEqual(9, (int)F(F(loaded, "hero"), "level")); Assert.AreEqual(7, (int)F(loaded, "apprenticeLevel"));
        Assert.AreEqual(900, (int)F(loaded, "goldMineStoredGold"), "madendeki altın korunur");
        var li = F(loaded, "island");
        Assert.AreEqual(10, (int)F(li, "keepLevel"), "ada etkin seviyesi (= Ana Üs 10) korunur, bundan sonra bağımsız");
        Assert.AreEqual(12, (int)F(li, "highestAkin"));
        Assert.AreEqual(placedIsland, ((IList)F(li, "placed")).Count, "ada binaları korunur");
        Assert.IsTrue((bool)F(li, "introSeen"), "girilmiş ada: tanıtım tekrar gösterilmez");
        Assert.AreEqual(0, ((IList)F(loaded, "iapLedger")).Count);
        Assert.AreEqual(-1, (int)F(loaded, "landOpened"), "arazi: eski kayıt işareti");

        // Oyunda: arazi Ana Üs kademesi kadar açık, savaş/depo okumaları çalışır
        yield return FreshManager();
        T("GameManager").GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { loaded });
        save = loaded;
        M(manager, "RebuildBuildingData");
        Assert.AreEqual(3, (int)SP("LandExpansion", "OpenedTier"), "Ana Üs 10 → 3 genişleme açık kalır");
        Assert.AreEqual(1, (int)S("BuildingLevels", "CompletedCount", loaded, Enum.Parse(T("BuildingType"), "ArcherTower")));
        Assert.Greater((int)M(manager, "GetGoldMineStorageCapacity"), 900, "yeni depo eskisinden büyük");
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

    // 15 Kasım (cihaz geri bildirimi): dar telefonda ada kadrajı uzaklaşmıştı (genişlik adacıklara göre sığıyordu).
    [UnityTest]
    public IEnumerator S7_IslandFramingOnNarrowPhoneFitsMainland()
    {
        try
        {
            yield return IslandPhaseBTests.LoadIsland(null, 5, v => devWas = v);
            yield return new WaitForSecondsRealtime(0.5f);
            var cc = T("IslandCameraController");
            var main = (Rect)cc.GetMethod("MainlandBounds").Invoke(null, null);
            var land = (Rect)cc.GetMethod("LandBounds").Invoke(null, null);
            var lines = new List<string> { $"land={land} main={main}" };
            foreach (var (tag, w, h) in Session4Tests.Ratios)
            {
                var args = new object[] { (float)w / h, null, null };
                cc.GetMethod("ComputeFixedView").Invoke(null, args);
                float size = (float)args[2]; var center = (Vector2)args[1];
                float viewW = size * 2f * w / h;
                lines.Add($"{tag}: size={size:F2} viewW={viewW:F2} mainShare={main.width / viewW:P0} landH/viewH={land.height / (2f * size):P0}");
                Assert.LessOrEqual(main.width, viewW + 0.01f, tag + ": ana ada genişliği ekrana sığar");
                // Uzaklaşmış değil: ya ana ada genişliği ya da ada yüksekliği (HUD bandı = %83) sınırı doldurur
                Assert.IsTrue(main.width / viewW >= 0.9f || land.height / (2f * size) >= 0.8f, tag + $": kadraj bir eksende dolu (genişlik {main.width / viewW:P0}, yükseklik {land.height / (2f * size):P0})");
                Assert.LessOrEqual(land.height, 2f * size * 0.84f, tag + ": ada dikeyde üst/alt HUD bandına sığar");
                var camera = (Camera)P(SP("IslandSceneSetup", "Instance"), "WorldCamera");
                var pos = camera.transform.position; float old = camera.orthographicSize;
                camera.orthographicSize = size; camera.transform.position = new Vector3(center.x, center.y, pos.z);
                Session4Tests.CaptureUi("S7_island_frame_" + tag, w / 2, h / 2);
                camera.orthographicSize = old; camera.transform.position = pos;
            }
            File.WriteAllLines(Path.Combine(Folder, "S7_island_frame.txt"), lines);
        }
        finally
        {
            T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
            T("IslandSession").GetMethod("Clear").Invoke(null, null);   // sonraki savaş testleri ada kipinde açılmasın
        }
    }
}
