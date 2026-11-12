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
/// Kart Atölyesi (Docs/design/card_workshop.md + 12 Kasım kullanıcı kararları): bedel/hak formülü, süreyle ve reklamla üretim,
/// Et harcama + elmasla tamamlama, zafer ve yenilgide hak düşmesi, seviyeye göre hak, kayıt v8 göçü ve gerçek savaşta kart etkisi.
/// Ekran görüntüleri <c>PlaytestOutput/workshop/</c>.
/// </summary>
public class CardWorkshopTests
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
    private static object Card(string name) => Enum.Parse(T("BattleCard"), name);
    private static int W(string method, params object[] args) => (int)T("Progression+Workshop").GetMethod(method).Invoke(null, args);
    private static int Stock(string card) => (int)Call("CardWorkshop", "Stock", Card(card));
    private static int Gold => (int)Prop(Manager, "Gold");
    private static int Gems => (int)Prop(Manager, "Gems");
    private static int Meat => (int)Get("IslandEconomy", "Meat");
    private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static Component Manager => T("GameManager").GetProperty("Instance").GetValue(null) as Component;
    private static object Save => Prop(Manager, "PlayerData");
    private static object Workshop => Field(Save, "workshop");
    private static System.Collections.IList Queue => (System.Collections.IList)Field(Workshop, "queue");

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
        var cards = T("BattleCards");
        cards.GetMethod("ResetForTests").Invoke(null, null);
        T("DevSettings").GetProperty("UnlimitedGems").SetValue(null, false);
    }

    /// <summary>Yeni kayıt: Ana Üs <paramref name="mainBase"/>, Kart Atölyesi <paramref name="workshopLevel"/> (0 = kurulu değil), ada Et'i, altın, elmas.</summary>
    private static IEnumerator Setup(int workshopLevel, int meat = 0, int gold = 5000, int gems = 0, int mainBase = 7)
    {
        var managerType = T("GameManager");
        var manager = Manager;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        T("DevSettings").GetProperty("UnlimitedGems").SetValue(null, false);
        var save = T("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(T("WeaponType"), "Axe") });
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", gold);
        Set(save, "gems", gems);
        Set(save, "freeCardUsed", true);   // ilk savaş bedava kartı atölye stoğunun önüne geçmesin
        Set(Field(save, "island"), "meat", meat);
        var placed = (System.Collections.IList)Field(save, "placedBuildings");
        if (workshopLevel > 0)
        {
            var entry = Activator.CreateInstance(T("PlacedBuildingData"));
            Set(entry, "slotId", "workshop_1");
            Set(entry, "buildingType", "CardWorkshop");
            Set(entry, "level", workshopLevel);
            placed.Add(entry);
        }
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        Call("BattleCards", "ResetForTests");
    }

    private static bool Craft(string card, bool useGems = false)
    {
        var args = new object[] { Card(card), useGems, null };
        return (bool)T("CardWorkshop").GetMethod("TryStartCraft").Invoke(null, args);
    }

    /// <summary>Kuyruktaki işi geçmişe çeker (oyun kapalıyken süre doldu) ve Tick'ler.</summary>
    private static int ExpireAndTick()
    {
        foreach (var job in Queue)
        {
            long length = (long)Field(job, "endTicks") - (long)Field(job, "startTicks");
            Set(job, "endTicks", DateTime.UtcNow.Ticks - 1);
            Set(job, "startTicks", DateTime.UtcNow.Ticks - 1 - length);
        }
        return (int)Call("CardWorkshop", "Tick");
    }

    // ==================== FORMÜLLER ====================

    [Test]
    public void CostFormulaUsesAndSlots()
    {
        // Bedel = taban × (1 + 0,15 (sv − 1)), 10'a yuvarlı. Taban Et/altın: Sıradan 150/180, Nadir 360/430, Destansı 900/1080.
        Assert.AreEqual(150, W("Meat", 0, 1)); Assert.AreEqual(180, W("Gold", 0, 1));
        Assert.AreEqual(220, W("Meat", 0, 4), "150 × 1,45 = 217,5 → 220");
        Assert.AreEqual(260, W("Gold", 0, 4), "180 × 1,45 = 261 → 260");
        Assert.AreEqual(360, W("Meat", 1, 1)); Assert.AreEqual(900, W("Meat", 2, 1));
        Assert.AreEqual(1710, W("Meat", 2, 7), "900 × (1 + 0,15 × 6) = 1710");
        Assert.AreEqual(12, W("StockCap", 0)); Assert.AreEqual(5, W("StockCap", 2));
        Assert.AreEqual(300, W("Seconds", 0)); Assert.AreEqual(7200, W("Seconds", 2));
    }

    [Test]
    public void UsesPerCraftGrowWithLevelAndCostPerUseFalls()
    {
        CollectionAssert.AreEqual(new[] { 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5 },
            Enumerable.Range(1, 15).Select(level => W("UsesPerCraft", level)).ToArray(), "her renk kademesi +1 hak");
        float perUse1 = W("Meat", 0, 1) / (float)W("UsesPerCraft", 1);
        float perUse13 = W("Meat", 0, 13) / (float)W("UsesPerCraft", 13);
        Assert.Less(perUse13, perUse1 * 0.6f, "yükseltmenin ödülü: hak başına Et 150 → 84 (sv13)");
        Assert.AreEqual(0, W("SpecialSlots", 2)); Assert.AreEqual(1, W("SpecialSlots", 3)); Assert.AreEqual(2, W("SpecialSlots", 7));
        Assert.AreEqual(1, W("QueueSlots", 5)); Assert.AreEqual(2, W("QueueSlots", 6));
        Assert.AreEqual(5, (int)T("Progression").GetMethod("FirstUnlockLevel").Invoke(null, new[] { Enum.Parse(T("BuildingType"), "CardWorkshop") }));
    }

    [Test]
    public void CasualPlayerCraftsOneOrTwoCommonCardsInEarlyDays()
    {
        // island_base_v2 §17: sıradan oyuncu gün 1 ≈ 211 + 30 hediye Et, gün 2 ≈ 381 Et, gün 7 ≈ 752 Et (altın:Et 16,6 → 4,7).
        int common = W("Meat", 0, 1);
        Assert.AreEqual(1, 241 / common, "gün 1: 1 Sıradan kart");
        Assert.AreEqual(2, 381 / common, "gün 2: 2 Sıradan kart (ada da Et istediği için pratikte 1–2)");
        Assert.AreEqual(8, (int)Get("GemEconomy", "MeatPerGem"), "1 elmas = 8 Et (altın/5)");
        Assert.AreEqual(7, (int)Call("GemEconomy", "MeatTopUpCost", 50), "50 Et eksik → ⌈50/8⌉ = 7 elmas");
    }

    // ==================== ÜRETİM ====================

    [UnityTest]
    public IEnumerator FirstCraftFreeThenTimedCraftAddsStockAfterTime()
    {
        yield return Setup(workshopLevel: 1, meat: 400, gold: 1000);
        Assert.IsTrue((bool)Get("CardWorkshop", "Built"));
        // İlk üretim bedava ve 30 sn.
        Assert.IsTrue(Craft("HeroDamage"));
        Assert.AreEqual(400, Meat); Assert.AreEqual(1000, Gold);
        Assert.AreEqual(1, Queue.Count);
        Assert.AreEqual(0, (int)Call("CardWorkshop", "Tick"), "süre dolmadan stoğa girmez");
        Assert.AreEqual(1, ExpireAndTick());
        Assert.AreEqual(1, Stock("HeroDamage"), "sv1: üretim başına 1 hak");
        Assert.IsTrue((bool)Field(Workshop, "firstFreeUsed"));

        // İkinci üretim: 150 Et + 180 altın, 5 dk.
        Assert.IsTrue(Craft("HeroHealth"));
        Assert.AreEqual(250, Meat); Assert.AreEqual(820, Gold);
        var job = Queue[0];
        Assert.AreEqual(300d, new TimeSpan((long)Field(job, "endTicks") - (long)Field(job, "startTicks")).TotalSeconds, 0.5d);
        Assert.IsFalse(Craft("Fortify"), "sv1: kuyruk 1");
        Assert.AreEqual(1, ExpireAndTick(), "çevrimdışı biten üretim açılışta stoğa girer");
        Assert.AreEqual(1, Stock("HeroHealth"));
        Assert.IsFalse(Craft("SharpVolleys"), "Nadir kart sv3 ister");
    }

    [UnityTest]
    public IEnumerator AdFinishesCraftInstantlyIncludingEpicThreePerDay()
    {
        yield return Setup(workshopLevel: 7, meat: 10000, gold: 20000);
        Set(Workshop, "firstFreeUsed", true);
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(Craft(i == 0 ? "WardBreaker" : "HeroDamage"), $"üretim {i}");
            Assert.IsTrue((bool)Call("CardWorkshop", "TryFinishByAd", 0), $"reklam {i + 1}/3");
            Assert.AreEqual(0, Queue.Count);
        }
        Assert.AreEqual(3, Stock("WardBreaker"), "Destansı da reklamla anında biter (sv7: 3 hak)");
        Assert.AreEqual(6, Stock("HeroDamage"));
        Assert.AreEqual(0, (int)Get("AdRewards", "WorkshopAdsLeft"));
        Assert.IsTrue(Craft("HeroHealth"));
        Assert.IsFalse((bool)Call("CardWorkshop", "TryFinishByAd", 0), "günde en çok 3 atölye reklamı");
        Assert.AreEqual(1, Queue.Count);
    }

    [UnityTest]
    public IEnumerator MissingMeatToppedUpWithGemsOrNothingSpent()
    {
        yield return Setup(workshopLevel: 1, meat: 100, gold: 1000, gems: 10);
        Set(Workshop, "firstFreeUsed", true);
        Assert.AreEqual(50, (int)Call("CardWorkshop", "MeatShortfall", Card("HeroDamage")));
        Assert.AreEqual(7, (int)Call("CardWorkshop", "MeatShortfallGems", Card("HeroDamage")));
        Assert.IsFalse(Craft("HeroDamage", useGems: false), "Et eksik, elmas kullanılmazsa başlamaz");
        Assert.AreEqual(100, Meat); Assert.AreEqual(1000, Gold); Assert.AreEqual(10, Gems);
        Assert.AreEqual(0, Queue.Count, "hiçbir şey kısmen harcanmaz");
        Assert.IsTrue(Craft("HeroDamage", useGems: true));
        Assert.AreEqual(0, Meat, "eldeki Et harcanır"); Assert.AreEqual(3, Gems, "eksik 50 Et = 7 elmas");
        Assert.AreEqual(820, Gold);
        Assert.AreEqual(1, Queue.Count);
    }

    [UnityTest]
    public IEnumerator UsesPerCraftFollowWorkshopLevelAndLockAtStart()
    {
        yield return Setup(workshopLevel: 4, meat: 5000, gold: 5000);
        Set(Workshop, "firstFreeUsed", true);
        Assert.IsTrue(Craft("Fortify"));
        Assert.AreEqual(2, (int)Field(Queue[0], "uses"), "sv4: 2 hak");
        // Üretim sürerken seviye 7 olsa da bu iş 2 hak verir.
        var placed = (System.Collections.IList)Field(Save, "placedBuildings");
        foreach (var entry in placed) if ((string)Field(entry, "buildingType") == "CardWorkshop") Set(entry, "level", 7);
        ExpireAndTick();
        Assert.AreEqual(2, Stock("Fortify"));
        Assert.IsTrue(Craft("Fortify"));
        Assert.AreEqual(3, (int)Field(Queue[0], "uses"), "sv7: 3 hak");
    }

    // ==================== SAVAŞTA HAK ====================

    [UnityTest]
    public IEnumerator UseSpentOnVictoryAndOnDefeat()
    {
        yield return Setup(workshopLevel: 3);
        Call("CardWorkshop", "AddStock", Card("HeroDamage"), 2);
        Call("CardWorkshop", "AddStock", Card("SharpVolleys"), 2);
        Call("BattleSession", "Begin", 1, 1);
        var gm = Manager;

        // Zafer: 1 hak düşer, kart o savaşta etkindir.
        Assert.IsTrue((bool)Call("BattleCards", "TryUnlockWithoutAd", Card("HeroDamage")));
        Assert.IsTrue((bool)Call("BattleCards", "ChosenFromStock", Card("HeroDamage")));
        var args = new object[] { Card("SharpVolleys"), null };
        Assert.IsTrue((bool)T("BattleCards").GetMethod("TrySelectSpecial").Invoke(null, args), (string)args[1]);
        Assert.AreEqual(2, Stock("HeroDamage"), "seçmek hak harcamaz");
        Call("BattleCards", "BeginBattle");
        Assert.AreEqual(1, Stock("HeroDamage")); Assert.AreEqual(1, Stock("SharpVolleys"));
        Assert.IsTrue((bool)Call("BattleCards", "ActiveInBattle", Card("HeroDamage")));
        Assert.AreEqual(1.1f, (float)Call("BattleCards", "Multiplier", Card("HeroDamage")), 0.001f);
        gm.GetType().GetMethod("BattleWon").Invoke(gm, new object[] { 0 });
        Assert.IsFalse((bool)Call("BattleCards", "ActiveInBattle", Card("HeroDamage")), "savaş bitince kart kapanır");

        // Yenilgi: yine 1 hak düşer.
        Assert.IsTrue((bool)Call("BattleCards", "TryUnlockWithoutAd", Card("HeroDamage")));
        Call("BattleCards", "BeginBattle");
        gm.GetType().GetMethod("BattleLost").Invoke(gm, null);
        Assert.AreEqual(0, Stock("HeroDamage"), "yenilgide de hak düşer");
        Assert.AreEqual(1, Stock("SharpVolleys"), "seçilmeyen kart harcanmaz");

        // Stok yoksa atölye yolu kapanır (jeton/reklam gerekir).
        Assert.IsFalse((bool)Call("BattleCards", "TryUnlockWithoutAd", Card("HeroDamage")));
    }

    [UnityTest]
    public IEnumerator RoyalCompetitiveAndUselessWardBreakerDoNotSpendUses()
    {
        yield return Setup(workshopLevel: 7);
        Call("CardWorkshop", "AddStock", Card("HeroDamage"), 2);
        Call("CardWorkshop", "AddStock", Card("WardBreaker"), 2);
        Call("BattleSession", "Begin", 1, 1);   // kalkansız aşama

        var args = new object[] { Card("WardBreaker"), null };
        Assert.IsTrue((bool)T("BattleCards").GetMethod("TrySelectSpecial").Invoke(null, args));
        Call("BattleCards", "BeginBattle");
        Assert.AreEqual(2, Stock("WardBreaker"), "kalkansız aşamada Kalkan Kıran hak harcamaz");
        Assert.IsFalse((bool)Call("BattleCards", "ActiveInBattle", Card("WardBreaker")));
        Call("BattleCards", "EndBattle");

        T("BattleCards").GetProperty("Competitive").SetValue(null, true);
        Assert.IsTrue((bool)Call("BattleCards", "TryUnlockWithoutAd", Card("HeroDamage")));
        Call("BattleCards", "BeginBattle");
        Assert.AreEqual(2, Stock("HeroDamage"), "rekabet modunda hak düşmez ve kart etkisizdir");
        Assert.IsFalse((bool)Call("BattleCards", "ActiveInBattle", Card("HeroDamage")));
        Call("BattleCards", "EndBattle");
        T("BattleCards").GetProperty("Competitive").SetValue(null, false);

        Set(Save, "royalUntilTicks", DateTime.UtcNow.AddDays(1).Ticks);
        Call("BattleCards", "BeginBattle");
        Assert.IsTrue((bool)Call("BattleCards", "ActiveInBattle", Card("HeroDamage")), "Kraliyet temel kartları bedava açar");
        Assert.AreEqual(2, Stock("HeroDamage"), "Kraliyet açıkken atölye stoğu harcanmaz");
    }

    [Test]
    public void TowerMultiplierStacksUpToCap()
    {
        var cards = T("BattleCards");
        cards.GetMethod("ResetForTests").Invoke(null, null);
        var inBattle = (bool[])cards.GetField("inBattle", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        inBattle[(int)Card("SharpVolleys")] = true;
        inBattle[(int)Card("ArmorBreaker")] = true;
        object archer = Enum.Parse(T("DamageSource"), "Archer"), hero = Enum.Parse(T("DamageSource"), "Hero");
        object armored = Enum.Parse(T("EnemyVariant"), "Armored"), none = Enum.Parse(T("EnemyVariant"), "None");
        float M(object source, object variant) => (float)cards.GetMethod("TowerHitMultiplier").Invoke(null, new[] { source, variant });
        Assert.AreEqual(1.15f, M(archer, none), 0.001f, "Keskin Oklar");
        Assert.AreEqual(1.5f, M(archer, armored), 0.001f, "1,15 × 1,4 = 1,61 → tavan 1,5");
        Assert.AreEqual(1f, M(hero, armored), 0.001f, "kahramana işlemez");
        cards.GetMethod("ResetForTests").Invoke(null, null);
    }

    // ==================== KAYIT ====================

    [UnityTest]
    public IEnumerator OldV7SaveMigratesToWorkshopV8()
    {
        yield return Setup(workshopLevel: 0, mainBase: 6);
        Call("SaveSystem", "DeleteSave");
        string json = JsonUtility.ToJson(Save);
        json = Regex.Replace(json, @",""workshop"":\{[^{}]*\}", "");
        json = Regex.Replace(json, @"\{""buildingType"":5,[^{}]*\},?", "");
        json = json.Replace(",]", "]");
        json = Regex.Replace(json, "\"saveVersion\":\\d+", "\"saveVersion\":7");
        StringAssert.DoesNotContain("\"workshop\"", json);
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
        var loaded = Call("SaveSystem", "Load");
        Assert.NotNull(loaded, "v7 kaydı açılmalı");
        Assert.AreEqual(Get("PlayerSaveData", "CurrentVersion"), Field(loaded, "saveVersion"));
        var workshop = Field(loaded, "workshop");
        Assert.NotNull(workshop);
        Assert.AreEqual((int)Get("BattleCards", "Count"), ((int[])Field(workshop, "stock")).Length);
        Assert.IsFalse((bool)Field(workshop, "firstFreeUsed"));
        bool found = false;
        foreach (var entry in (System.Collections.IEnumerable)Field(loaded, "buildings"))
            if (Field(entry, "buildingType").ToString() == "CardWorkshop") { found = true; Assert.IsTrue((bool)Field(entry, "isUnlocked"), "Ana Üs 6 ≥ 5"); }
        Assert.IsTrue(found, "bina türü kaydı eklendi");

        // Stok ve kuyruk gidip gelir.
        var stock = (int[])Field(workshop, "stock");
        stock[(int)Card("SkyHunter")] = 4;
        var back = JsonUtility.FromJson(JsonUtility.ToJson(loaded), T("PlayerSaveData"));
        Assert.AreEqual(4, ((int[])Field(Field(back, "workshop"), "stock"))[(int)Card("SkyHunter")]);
    }

    // ==================== GERÇEK SAVAŞ ====================

    [UnityTest]
    public IEnumerator SharpVolleysRaisesTowerDamageInRealBattle()
    {
        Screen.SetResolution(390, 844, false);
        yield return Setup(workshopLevel: 3);
        Call("CardWorkshop", "AddStock", Card("SharpVolleys"), 1);
        Call("BattleSession", "Begin", 1, 1);
        var args = new object[] { Card("SharpVolleys"), null };
        Assert.IsTrue((bool)T("BattleCards").GetMethod("TrySelectSpecial").Invoke(null, args), (string)args[1]);
        Call("BattleCards", "BeginBattle");
        Assert.AreEqual(0, Stock("SharpVolleys"));
        SceneManager.LoadScene("BattleScene");
        for (int frame = 0; frame < 45; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.4f);

        var battle = Object.FindFirstObjectByType(T("BattleManager")) as Component;
        Vector3 far = (Object.FindFirstObjectByType(T("HeroController")) as Component).transform.position + new Vector3(0f, 14f);
        var enemy = battle.GetType().GetMethod("SpawnVariantAt").Invoke(battle,
            new[] { Enum.Parse(T("EnemyType"), "Orc"), far, Enum.Parse(T("EnemyVariant"), "None") }) as Component;
        yield return null;
        enemy.GetType().GetProperty("DamageTakenMultiplier").SetValue(enemy, 1f);
        float before = (float)Prop(enemy, "CurrentHealth");
        enemy.GetType().GetMethod("TakeDamage").Invoke(enemy, new[] { 20f, Enum.Parse(T("DamageSource"), "Archer") });
        Assert.AreEqual(before - 23f, (float)Prop(enemy, "CurrentHealth"), 0.01f, "Keskin Oklar: okçu hasarı ×1,15");
        before = (float)Prop(enemy, "CurrentHealth");
        enemy.GetType().GetMethod("TakeDamage").Invoke(enemy, new[] { 20f, Enum.Parse(T("DamageSource"), "Hero") });
        Assert.AreEqual(before - 20f, (float)Prop(enemy, "CurrentHealth"), 0.01f, "kahraman hasarı değişmez");

        CaptureScreen("workshop_battle_hud");
        Manager.GetType().GetMethod("BattleLost").Invoke(Manager, null);
    }

    // ==================== EKRAN GÖRÜNTÜLERİ ====================

    [UnityTest]
    public IEnumerator CaptureWorkshopScreens()
    {
        Screen.SetResolution(390, 844, false);
        yield return Setup(workshopLevel: 7, meat: 2000, gold: 2400, gems: 30);
        Call("CardWorkshop", "AddStock", Card("HeroDamage"), 3);
        Call("CardWorkshop", "AddStock", Card("ArmorBreaker"), 2);
        Call("CardWorkshop", "AddStock", Card("WardBreaker"), 1);
        Set(Workshop, "firstFreeUsed", true);
        Assert.IsTrue(Craft("SharpVolleys"));
        Set(Field(Save, "island"), "meat", 120);   // Et eksik ipucu görünsün

        SceneManager.LoadScene("BaseScene");
        for (int frame = 0; frame < 30; frame++) yield return null;
        yield return new WaitForSecondsRealtime(1f);
        CaptureScreen("workshop_base");

        Assert.IsTrue((bool)Call("CardWorkshopPanel", "Open", "workshop_1"));
        yield return new WaitForSecondsRealtime(0.4f);
        CaptureScreen("workshop_panel");
        var panel = Get("CardWorkshopPanel", "Current");
        panel.GetType().GetMethod("Select").Invoke(panel, new[] { Card("WardBreaker") });
        yield return null;
        CaptureScreen("workshop_panel_epic");
        Call("CardWorkshopPanel", "Close");

        // Sefer ön-savaş: Bölge 1, 5. aşama (boss) — temel kartlar + ATÖLYE KARTLARI satırı.
        SceneManager.LoadScene("CampaignScene");
        for (int frame = 0; frame < 30; frame++) yield return null;
        yield return new WaitForSecondsRealtime(0.8f);
        var setup = Object.FindFirstObjectByType(T("CampaignSceneSetup")) as Component;
        var region = T("CampaignCatalog").GetMethod("Get").Invoke(null, new object[] { 1 });
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        setup.GetType().GetMethod("ShowRegion", flags).Invoke(setup, new[] { region });
        yield return null;
        setup.GetType().GetMethod("SelectLevel", flags).Invoke(setup, new object[] { 5 });
        Call("BattleCards", "TryUnlockWithoutAd", Card("HeroDamage"));
        var args = new object[] { Card("ArmorBreaker"), null };
        T("BattleCards").GetMethod("TrySelectSpecial").Invoke(null, args);
        setup.GetType().GetMethod("RenderLevelDetail", flags).Invoke(setup, null);
        yield return new WaitForSecondsRealtime(0.5f);
        CaptureScreen("workshop_prebattle");
    }

    /// <summary>Ekranı (Overlay tuvaller dahil) <c>PlaytestOutput/workshop/name.png</c> olarak yazar.</summary>
    internal static void CaptureScreen(string name, string subfolder = "workshop")
    {
        const int W = 780, H = 1688;
        var camera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/" + subfolder));
        Directory.CreateDirectory(folder);
        var texture = new RenderTexture(W, H, 24);
        camera.targetTexture = texture;
        camera.aspect = (float)W / H;
        var changed = new System.Collections.Generic.List<(Canvas, RenderMode, Camera, int, bool)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            changed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.sortingOrder, canvas.overrideSorting));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000 + canvas.sortingOrder;   // gerçek oyunda Overlay: arayüz dünya sprite'larının üstündedir
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(W, H, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        texture.Release();
        Object.Destroy(texture);
        foreach (var (canvas, mode, cam, order, over) in changed)
        {
            if (canvas == null) continue;
            canvas.renderMode = mode; canvas.worldCamera = cam; canvas.sortingOrder = order; canvas.overrideSorting = over;
        }
    }
}
