using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Faz F "Ana Üs bağları" (island_base.md §7-8, island_base_v2 §22): Kışla + Köylü Evi açılış seviyeleri, bedel çarpanları,
/// Lancer sayı/can/hasar formülleri, koruma yarıçapı, yeniden doğma, ev deposu bonusu, kayıt v9 göçü ve sahnede Kışla + Lancer.
/// Ekran görüntüleri <c>PlaytestOutput/phaseF/</c>.
/// </summary>
public class BaseBarracksTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");
    private string savedKey, savedBackup, savedFile;

    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Call(string type, string method, params object[] args) => T(type).GetMethod(method).Invoke(null, args);
    private static object Get(string type, string member)
    {
        var t = T(type);
        var property = t.GetProperty(member);
        return property != null ? property.GetValue(null) : t.GetField(member).GetValue(null);
    }
    private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
    private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object BType(string name) => Enum.Parse(T("BuildingType"), name);
    private static Component Manager => T("GameManager").GetProperty("Instance").GetValue(null) as Component;
    private static object Save => Prop(Manager, "PlayerData");
    private static float P(string method, params object[] args) => Convert.ToSingle(T("Progression").GetMethod(method).Invoke(null, args));
    private static int PI(string method, params object[] args) => Convert.ToInt32(T("Progression").GetMethod(method).Invoke(null, args));

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
    }

    [TearDown]
    public void RestoreRealSave()
    {
        Time.timeScale = 1f;
        void Put(string key, string value)
        {
            if (value != null) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        }
        Put(Key, savedKey);
        Put(BackupKey, savedBackup);
        if (savedFile != null) File.WriteAllText(FilePath, savedFile);
        else if (File.Exists(FilePath)) File.Delete(FilePath);
        PlayerPrefs.Save();
        T("BattleCards").GetMethod("ResetForTests").Invoke(null, null);
    }

    /// <summary>
    /// GameManager sahne geçişlerinde yaşar ve <c>OnApplicationQuit</c>'te son oyuncu verisini kaydeder: test Ana Üs 11-12 + Kışla kayıtını
    /// gerçek kayda yazıp sonraki koşuları (ör. IslandEconomySimTests: etkin Kıyı Kalesi = Ana Üs) bozardı. Sınıf bitince yok edilir.
    /// </summary>
    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = Manager;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        if (savedFile != null) File.WriteAllText(FilePath, savedFile);
        PlayerPrefs.Save();
    }

    /// <summary>Yeni kayıt: Ana Üs <paramref name="mainBase"/>, verilen (slot, tür, seviye) yerleşik binalar, altın.</summary>
    private static IEnumerator Setup(int mainBase, int gold = 5000, params (string slot, string type, int level)[] placedBuildings)
    {
        var managerType = T("GameManager");
        var manager = Manager;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", gold);
        Set(save, "freeCardUsed", true);
        var placed = (System.Collections.IList)Field(save, "placedBuildings");
        placed.Clear();
        foreach (var (slot, type, level) in placedBuildings)
        {
            var entry = Activator.CreateInstance(T("PlacedBuildingData"));
            Set(entry, "slotId", slot);
            Set(entry, "buildingType", type);
            Set(entry, "level", level);
            placed.Add(entry);
        }
        // Tür kayıtları Ana Üs seviyesine göre açılır (CheckUnlockBuildings ile aynı kural).
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
        {
            string type = Field(entry, "buildingType").ToString();
            if (type != "MainBase" && type != "GoldMine")
            {
                bool open = mainBase >= (int)Call("Progression", "FirstUnlockLevel", BType(type));
                Set(entry, "isUnlocked", open);
                Set(entry, "level", open ? 1 : 0);
            }
        }
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        Call("BattleCards", "ResetForTests");
    }

    // ==================== SABİTLER VE FORMÜLLER ====================

    [Test]
    public void UnlockLevelsAndCostFactors()
    {
        var barracks = BType("Barracks"); var house = BType("House");
        Assert.AreEqual(new[] { 7, 11 }, (int[])Get("Progression", "BarracksUnlock"));
        Assert.AreEqual(new[] { 3, 9 }, (int[])Get("Progression", "HouseUnlock"));
        Assert.AreEqual(7, PI("FirstUnlockLevel", barracks)); Assert.AreEqual(11, PI("SecondUnlockLevel", barracks));
        Assert.AreEqual(3, PI("FirstUnlockLevel", house)); Assert.AreEqual(9, PI("SecondUnlockLevel", house));
        var buildingData = T("BuildingData");
        int Max(object type, int mb) => (int)buildingData.GetMethod("GetMaxBuildCount").Invoke(null, new[] { type, mb });
        Assert.AreEqual(0, Max(barracks, 6)); Assert.AreEqual(1, Max(barracks, 7)); Assert.AreEqual(1, Max(barracks, 10)); Assert.AreEqual(2, Max(barracks, 11));
        Assert.AreEqual(0, Max(house, 2)); Assert.AreEqual(1, Max(house, 3)); Assert.AreEqual(1, Max(house, 8)); Assert.AreEqual(2, Max(house, 9));

        Assert.AreEqual(0.65f, (float)Get("Progression", "BarracksCostFactor"), 1e-5f);
        Assert.AreEqual(0.4f, (float)Get("Progression", "HouseCostFactor"), 1e-5f);
        for (int level = 2; level <= 15; level++)
        {
            int main = PI("UpgradeCost", BType("MainBase"), level);
            Assert.AreEqual(Mathf.Max(10, Mathf.RoundToInt(main * 0.65f / 10f) * 10), PI("UpgradeCost", barracks, level), $"Kışla bedeli L{level}");
            Assert.AreEqual(Mathf.Max(10, Mathf.RoundToInt(main * 0.4f / 10f) * 10), PI("UpgradeCost", house, level), $"Ev bedeli L{level}");
        }
        Assert.AreEqual(PI("UpgradeCost", barracks, 2), PI("BuildCost", barracks), "kurulum = 1. seviyeye yükseltme bedeli");
        Assert.Less(PI("BuildCost", house), PI("BuildCost", barracks));
        // Enum yalnız sona eklendi: eski türlerin sayısal değerleri değişmedi (kayıt uyumu).
        Assert.AreEqual(5, (int)BType("CardWorkshop"));
        Assert.AreEqual(6, (int)BType("Barracks"));
        Assert.AreEqual(7, (int)BType("House"));
        Assert.AreEqual(7, (int)Enum.Parse(T("DamageSource"), "Ally"), "enum sonuna eklendi (IslandLancer = 6)");
    }

    [Test]
    public void LancerCountAndStatFormulas()
    {
        for (int level = 1; level <= 15; level++)
        {
            Assert.AreEqual(1 + level / 5, PI("AllyLancerCount", level), $"sayı L{level}");
            Assert.AreEqual(70f * (1f + 0.15f * (level - 1)), P("AllyLancerHp", level), 1e-3f, $"can L{level}");
            Assert.AreEqual(6f * (1f + 0.15f * (level - 1)), P("AllyLancerDamage", level), 1e-3f, $"hasar L{level}");
        }
        Assert.AreEqual(1, PI("AllyLancerCount", 4)); Assert.AreEqual(2, PI("AllyLancerCount", 5)); Assert.AreEqual(4, PI("AllyLancerCount", 15));
        Assert.AreEqual(3.5f, (float)Get("Progression", "AllyGuardRadius"));
        Assert.AreEqual(15f, (float)Get("Progression", "AllyRespawn"));
        Assert.AreEqual(1f, (float)Get("Progression", "AllyHitInterval"));
        // Bina verisi aynı formülü gösterir.
        var data = Activator.CreateInstance(T("BuildingData"));
        Set(data, "buildingType", BType("Barracks")); Set(data, "level", 10);
        data.GetType().GetMethod("ApplyLevelStats").Invoke(data, null);
        Assert.AreEqual(3, (int)Field(data, "unitCount"));
        Assert.AreEqual(P("AllyLancerDamage", 10), (float)Field(data, "damage"), 1e-3f);
    }

    [Test]
    public void HouseStorageBonusFormula()
    {
        for (int level = 1; level <= 15; level++)
            Assert.AreEqual(0.05f + 0.005f * (level - 1), P("HouseStorageBonus", level), 1e-5f);
        Assert.AreEqual(0.12f, P("HouseStorageBonus", 15), 1e-5f, "L15 %12");
        Assert.AreEqual(0.24f, 2f * P("HouseStorageBonus", 15), 1e-5f, "iki ev en çok %24");
    }

    // ==================== OYUN İÇİ DAVRANIŞ ====================

    [UnityTest]
    public IEnumerator HousesRaiseMineStorageButNotProduction()
    {
        yield return Setup(9, 5000, ("mine_1", "GoldMine", 5));
        var gm = Manager;
        int Capacity() => (int)gm.GetType().GetMethod("GetGoldMineStorageCapacity").Invoke(gm, null);
        float Rate() => (float)Field(((System.Collections.IDictionary)Prop(gm, "Buildings"))[BType("GoldMine")], "goldPerSecond");
        Assert.AreEqual(800, Capacity(), "ev yok: 200 + 4 × 150");
        float rate = Rate();

        yield return Setup(9, 5000, ("mine_1", "GoldMine", 5), ("house_1", "House", 10), ("house_2", "House", 4));
        gm = Manager;
        float bonus = P("HouseStorageBonus", 10) + P("HouseStorageBonus", 4);
        Assert.AreEqual(Mathf.RoundToInt(800f * (1f + bonus)), Capacity(), "iki ev: deposu seviyeye göre büyütür");
        Assert.Greater(Capacity(), 800);
        Assert.AreEqual(rate, Rate(), 1e-5f, "üretim hızı değişmez");
        Assert.AreEqual(bonus, (float)gm.GetType().GetMethod("HouseStorageBonus").Invoke(gm, null), 1e-5f);
    }

    [Test]
    public void SaveV8MigratesAndRoundTrips()
    {
        var save = T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", 8);
        var placed = (System.Collections.IList)Field(save, "placedBuildings");
        var barracks = Activator.CreateInstance(T("PlacedBuildingData"));
        Set(barracks, "slotId", "barracks_1"); Set(barracks, "buildingType", "Barracks"); Set(barracks, "level", 6);
        placed.Add(barracks);
        string json = JsonUtility.ToJson(save);
        // v8 kaydı: tür girdileri yok.
        json = Regex.Replace(json, "\\{[^{}]*\"buildingType\":(6|7)[^{}]*\\},?", "");
        json = json.Replace(",]", "]");
        json = Regex.Replace(json, "\"saveVersion\":\\d+", "\"saveVersion\":8");
        if (File.Exists(FilePath)) File.Delete(FilePath);   // Load en iyi kaydı seçer: gerçek dosya kayıt testini gölgelemesin (TearDown geri yükler)
        PlayerPrefs.DeleteKey(BackupKey);
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
        var loaded = Call("SaveSystem", "Load");
        Assert.NotNull(loaded, "v8 kaydı açılmalı");
        Assert.AreEqual(Get("PlayerSaveData", "CurrentVersion"), Field(loaded, "saveVersion"));
        Assert.GreaterOrEqual((int)Field(loaded, "saveVersion"), 9);
        var open = (System.Collections.IEnumerable)Field(loaded, "buildings");
        foreach (var type in new[] { "Barracks", "House" })
        {
            bool found = false;
            foreach (var entry in open)
                if (Field(entry, "buildingType").ToString() == type) { found = true; Assert.IsTrue((bool)Field(entry, "isUnlocked"), type + ": Ana Üs 8 ≥ açılış"); }
            Assert.IsTrue(found, type + " türü kaydı eklendi");
        }
        // Yerleşik Kışla seviyesiyle gidip gelir.
        var back = JsonUtility.FromJson(JsonUtility.ToJson(loaded), T("PlayerSaveData"));
        var levels = (System.Collections.IList)T("AllyBarracks").GetMethod("BarracksLevels").Invoke(null, new[] { back });
        Assert.AreEqual(1, levels.Count);
        Assert.AreEqual(6, (int)levels[0]);

        // Ana Üs 2'de ikisi de kilitli.
        var fresh = T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });
        foreach (var entry in (System.Collections.IEnumerable)Field(fresh, "buildings"))
        {
            string type = Field(entry, "buildingType").ToString();
            if (type == "Barracks") Assert.IsFalse((bool)Field(entry, "isUnlocked"));
            if (type == "House") Assert.IsFalse((bool)Field(entry, "isUnlocked"));
        }
    }

    private static IEnumerator EnterBattle(int mainBase, params (string slot, string type, int level)[] placed)
    {
        Screen.SetResolution(390, 844, false);
        yield return Setup(mainBase, 5000, placed);
        Call("BattleSession", "Begin", 1, 1);
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 45; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.4f);
    }

    private static Component Barracks => T("AllyBarracks").GetProperty("Instance").GetValue(null) as Component;
    private static System.Collections.IList Lancers => (System.Collections.IList)T("AllyLancer").GetField("All").GetValue(null);

    [UnityTest]
    public IEnumerator BattleSpawnsLancersAroundMainBase()
    {
        yield return EnterBattle(12, ("barracks_1", "Barracks", 10));
        Assert.NotNull(Barracks, "Kışla garnizonu kuruldu");
        Assert.AreEqual(3, (int)Prop(Barracks, "SlotCount"), "1 + 10/5");
        Assert.AreEqual(3, (int)Prop(Barracks, "Alive"));
        var mainBase = Object.FindFirstObjectByType(T("MainBaseController")) as Component;
        foreach (Component lancer in Lancers)
        {
            Assert.AreEqual(P("AllyLancerHp", 10), (float)Prop(lancer, "MaxHp"), 1e-3f);
            Assert.AreEqual(P("AllyLancerDamage", 10), (float)Prop(lancer, "Damage"), 1e-3f);
            Assert.LessOrEqual(Vector2.Distance(lancer.transform.position, mainBase.transform.position), 3.5f, "bekleme noktası koruma yarıçapı içinde");
        }
        CardWorkshopTests.CaptureScreen("phaseF_lancers_idle", "phaseF");
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);
    }

    [UnityTest]
    public IEnumerator NoBarracksMeansNoLancers()
    {
        yield return EnterBattle(12);
        Assert.IsNull(Barracks);
        Assert.AreEqual(0, Lancers.Count);
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);
    }

    [UnityTest]
    public IEnumerator TwoBarracksSumTheirLancers()
    {
        yield return EnterBattle(11, ("barracks_1", "Barracks", 9), ("barracks_2", "Barracks", 4));
        Assert.AreEqual(2 + 1, (int)Prop(Barracks, "SlotCount"));
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);
    }

    private static Component SpawnEnemy(Component battle, string type, Vector3 position) =>
        battle.GetType().GetMethod("SpawnVariantAt").Invoke(battle,
            new[] { Enum.Parse(T("EnemyType"), type), position, Enum.Parse(T("EnemyVariant"), "None") }) as Component;

    [UnityTest]
    public IEnumerator LancersGuardOnlyTheBaseRadiusAndStrikeAsAlly()
    {
        yield return EnterBattle(12, ("barracks_1", "Barracks", 10));
        var battle = Object.FindFirstObjectByType(T("BattleManager")) as Component;
        var mainBase = Object.FindFirstObjectByType(T("MainBaseController")) as Component;
        var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
        // Kahraman savaş dışında dursun; düşman bölgenin dışında (8 u): Lancer'lar avlanmaz.
        hero.transform.position = mainBase.transform.position + new Vector3(0f, -6f);
        Vector3 farPoint = mainBase.transform.position + new Vector3(8f, 0f);
        var far = SpawnEnemy(battle, "Orc", farPoint);
        far.GetType().GetProperty("DamageTakenMultiplier").SetValue(far, 1f);
        yield return new WaitForSecondsRealtime(0.8f);
        foreach (Component lancer in Lancers)
        {
            Assert.IsNull(Prop(lancer, "Target"), "bölge dışındaki düşman hedef alınmaz");
            Assert.LessOrEqual(Vector2.Distance(lancer.transform.position, mainBase.transform.position), 3.5f, "Lancer koruma yarıçapının dışına çıkmadı");
        }

        // Aynı düşman Ana Üs'ün 3,0 u yakınına gelince Lancer'lar vurur; hasar kaynağı Ally.
        float before = (float)Prop(far, "CurrentHealth");
        far.transform.position = mainBase.transform.position + new Vector3(3f, 0f);
        far.GetType().GetField("nextRetarget", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(far, 0f);
        yield return new WaitForSecondsRealtime(2.5f);
        var bySource = (System.Collections.IDictionary)T("EnemyController").GetField("DamageBySource").GetValue(null);
        var ally = Enum.Parse(T("DamageSource"), "Ally");
        Assert.IsTrue(bySource.Contains(ally) && (float)bySource[ally] > 0f, "Lancer hasarı DamageSource.Ally olarak sayılır");
        Assert.Less((float)Prop(far, "CurrentHealth"), before, "bölgedeki düşman Lancer'lardan hasar aldı");
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);
    }

    [UnityTest]
    public IEnumerator FallenLancerRespawnsAfterFifteenSeconds()
    {
        yield return EnterBattle(7, ("barracks_1", "Barracks", 1));
        Assert.AreEqual(1, (int)Prop(Barracks, "SlotCount"));
        var lancer = Lancers[0] as Component;
        lancer.GetType().GetMethod("TakeDamage").Invoke(lancer, new object[] { 99999f });
        yield return null;
        Assert.AreEqual(0, (int)Prop(Barracks, "Alive"));
        Assert.AreEqual(1, (int)T("AllyBarracks").GetField("Deaths").GetValue(null));
        var advance = Barracks.GetType().GetMethod("Advance");
        advance.Invoke(Barracks, new object[] { 14.5f });
        Assert.AreEqual(0, (int)Prop(Barracks, "Alive"), "15 sn dolmadan yeniden doğmaz");
        advance.Invoke(Barracks, new object[] { 0.6f });
        Assert.AreEqual(1, (int)Prop(Barracks, "Alive"), "15 sn sonra yeniden doğar");
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);
    }

    [UnityTest]
    public IEnumerator MeleeEnemyStrikesAdjacentLancer()
    {
        yield return EnterBattle(7, ("barracks_1", "Barracks", 1));
        var battle = Object.FindFirstObjectByType(T("BattleManager")) as Component;
        var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
        hero.transform.position += new Vector3(0f, -9f);
        var lancer = Lancers[0] as Component;
        float maxHp = (float)Prop(lancer, "MaxHp");
        var orc = SpawnEnemy(battle, "Orc", lancer.transform.position + new Vector3(0.5f, 0f));
        orc.GetType().GetProperty("DamageTakenMultiplier").SetValue(orc, 0.05f);   // Lancer ölmeden önce orc ölmesin
        yield return new WaitForSecondsRealtime(2.5f);
        Assert.Less((float)Prop(lancer, "Hp"), maxHp, "yakın dövüşçü yanındaki Lancer'a vurdu");
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);
    }

    // ==================== EKRAN GÖRÜNTÜLERİ ====================

    [UnityTest]
    public IEnumerator CaptureBarracksFightAndBase()
    {
        yield return EnterBattle(12, ("barracks_1", "Barracks", 10));
        var battle = Object.FindFirstObjectByType(T("BattleManager")) as Component;
        var mainBase = Object.FindFirstObjectByType(T("MainBaseController")) as Component;
        var hero = Object.FindFirstObjectByType(T("HeroController")) as Component;
        hero.transform.position = mainBase.transform.position + new Vector3(-0.2f, -3.6f);
        yield return new WaitForSecondsRealtime(2.2f);   // DALGA afişi kaybolsun
        SpawnEnemy(battle, "Orc", mainBase.transform.position + new Vector3(2.6f, -0.6f));
        SpawnEnemy(battle, "Orc", mainBase.transform.position + new Vector3(-2.4f, 0.2f));
        yield return new WaitForSecondsRealtime(0.6f);
        CardWorkshopTests.CaptureScreen("phaseF_lancers_fight", "phaseF");
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);

        // Ana Üs: Kışla (L10) + iki Köylü Evi, inşa menüsü.
        yield return Setup(12, 5000, ("mine_1", "GoldMine", 8), ("barracks_1", "Barracks", 10), ("house_1", "House", 10), ("house_2", "House", 4));
        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 30; frame++) yield return null;
        yield return new WaitForSecondsRealtime(1f);
        // Ana Üs 12: Kışla (L10, mor kademe) + iki Köylü Evi (L10 mor, L4 sarı) sahnede.
        var focus = Object.FindObjectsByType(T("BuildingIdentifier"), FindObjectsSortMode.None).Cast<Component>()
            .Where(c => { var type = Field(c, "buildingType").ToString(); return type == "Barracks" || type == "House"; }).ToList();
        Assert.AreEqual(3, focus.Count, "Kışla + 2 Ev sahnede");
        CardWorkshopTests.CaptureScreen("phaseF_base", "phaseF");
        var menu = T("BuildMenuController").GetProperty("Instance").GetValue(null);
        if (menu != null)
        {
            menu.GetType().GetMethod("Show").Invoke(menu, null);
            yield return new WaitForSecondsRealtime(0.6f);
            CardWorkshopTests.CaptureScreen("phaseF_buildmenu", "phaseF");
        }
    }
}
