using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz C: ödül (Et düşük, tekrar 0), yenilgi etiketi kapsamı, DM2D prefab doğuşu (SortingGroup, Move, Death),
/// hedefleme, onarım ve bot merdiveni (Akın 1–5 kazanılır, savunmasız Akın 1 kaybedilir; 6–10 ölçülür). Yansıma ile.
/// </summary>
public class IslandPhaseCTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");

    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    private string savedKey, savedBackup, savedFile;
    private bool devWas;

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
        Type("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        Type("IslandSession").GetMethod("Clear").Invoke(null, null);
        Type("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static Component Setup => (Component)UnityEngine.Object.FindFirstObjectByType(Type("IslandSceneSetup"));
    private static object IslandConst(string name) => Type("Progression+Island").GetField(name).GetValue(null);
    private static object Call(string type, string method, params object[] args) => Type(type).GetMethod(method).Invoke(null, args);

    private IEnumerator LoadIsland(int keep, int highest)
    {
        var dev = Type("DevSettings").GetProperty("Enabled");
        devWas = (bool)dev.GetValue(null);
        dev.SetValue(null, true);
        var managerType = Type("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (manager == null) manager = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = Type("PlayerSaveData").GetMethod("CreateNew").Invoke(null, new[] { Enum.Parse(Type("WeaponType"), "Axe") });
        var island = save.GetType().GetField("island").GetValue(save) ?? Activator.CreateInstance(Type("IslandSaveData"));
        island.GetType().GetField("keepLevel").SetValue(island, keep);
        island.GetType().GetField("highestAkin").SetValue(island, highest);
        island.GetType().GetField("tutorialStep").SetValue(island, 4);
        save.GetType().GetField("island").SetValue(save, island);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(manager, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        Type("IslandSession").GetMethod("Begin").Invoke(null, null);
        SceneManager.LoadScene("BattleScene");
        for (int i = 0; i < 20; i++) yield return null;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            var setup = UnityEngine.Object.FindFirstObjectByType(Type("IslandSceneSetup"));
            if (setup != null && (bool)Prop(setup, "Ready")) break;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.3f);
    }

    // ==================== ÖDÜL ====================

    [Test]
    public void MeatRewardIsLowFirstClearOnlyAndReplayIsZero()
    {
        var session = Type("IslandSession");
        int F(object reward, string field) => (int)reward.GetType().GetField(field).GetValue(reward);
        for (int n = 1; n <= 30; n++)
        {
            Assert.AreEqual(5 + 2 * n, (int)Call("Progression+Island", "AkinMeat", n), $"Akın {n} Et = 5 + 2N");
            var first = session.GetMethod("RewardFor").Invoke(null, new object[] { n, n - 1 });
            var replay = session.GetMethod("RewardFor").Invoke(null, new object[] { n, n });
            Assert.AreEqual(5 + 2 * n, F(first, "meat"), $"ilk zafer Et N={n}");
            Assert.AreEqual(0, F(replay, "meat"), $"tekrarda Et 0 (N={n})");
            Assert.Greater(F(replay, "wood"), 0, "Odun tekrarda %30");
            Assert.AreEqual(Mathf.RoundToInt(((int)Call("Progression+Island", "AkinWood", n)) * 0.3f), F(replay, "wood"));
        }
        // Akın Et çeşmesi olmamalı: ilk 15 Akın'ın toplam Eti 315 (eski 15+4N'de 600+).
        int total = Enumerable.Range(1, 15).Sum(n => (int)Call("Progression+Island", "AkinMeat", n));
        Assert.AreEqual(315, total);
        Assert.Less(total, 400);
        Assert.AreEqual(5, IslandConst("ReplayRewardsPerDay"));
    }

    [UnityTest]
    public IEnumerator VictoryGrantsMeatOnceAndReplayGrantsNoMeat()
    {
        yield return LoadIsland(1, 0);
        var save = Call("IslandSession", "get_Save");
        int meat0 = (int)save.GetType().GetField("meat").GetValue(save);
        Call("IslandSession", "GrantVictory", 1);
        int meat1 = (int)save.GetType().GetField("meat").GetValue(save);
        Assert.AreEqual(7, meat1 - meat0, "ilk zafer 5 + 2·1");
        Call("IslandSession", "GrantVictory", 1);
        int meat2 = (int)save.GetType().GetField("meat").GetValue(save);
        Assert.AreEqual(meat1, meat2, "tekrar zaferde Et 0");
        // Günlük tekrar tavanı: 5 tekrardan sonra Odun/altın da 0
        for (int i = 0; i < 6; i++) Call("IslandSession", "GrantVictory", 1);
        Assert.AreEqual(0, (int)Call("IslandSession", "ReplayRewardsLeft"));
    }

    // ==================== YENİLGİ ETİKETLERİ ====================

    private static object Classify(Dictionary<string, float> damage, float[] beach, int shield, Func<int, bool> watched)
    {
        var enemyType = Type("EnemyType");
        var dict = (System.Collections.IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(enemyType, typeof(float)));
        foreach (var pair in damage) dict[Enum.Parse(enemyType, pair.Key)] = pair.Value;
        return Call("IslandDefeat", "Classify", dict, beach, shield, watched);
    }

    private static string TagOf(object label) => (string)label.GetType().GetField("tag").GetValue(label);
    private static bool Explained(object label) => (bool)label.GetType().GetField("explained").GetValue(label);

    [Test]
    public void DefeatLabelsMapDominantCauseAndCoverNinetyPercent()
    {
        var none = new[] { 0f, 0f, 0f, 0f };
        Assert.AreEqual("flyer_unanswered", TagOf(Classify(new Dictionary<string, float> { { "Bat", 60f }, { "Raider", 40f } }, none, 0, _ => true)));
        Assert.AreEqual("golem_armor", TagOf(Classify(new Dictionary<string, float> { { "WoodGolem", 70f }, { "Raider", 30f } }, none, 0, _ => true)));
        Assert.AreEqual("climber_shortcut", TagOf(Classify(new Dictionary<string, float> { { "Rat", 50f }, { "Raider", 50f } }, none, 0, _ => true)));
        Assert.AreEqual("shield_front", TagOf(Classify(new Dictionary<string, float> { { "ShieldLancer", 50f }, { "Raider", 50f } }, none, 9, _ => true)));
        Assert.AreEqual("boss_unbroken", TagOf(Classify(new Dictionary<string, float> { { "ButcherCaptain", 60f }, { "Raider", 40f } }, none, 0, _ => true)));
        Assert.AreEqual("beach_unwatched", TagOf(Classify(new Dictionary<string, float> { { "Raider", 100f } }, new[] { 100f, 0f, 0f, 0f }, 0, _ => false)));
        Assert.AreEqual("stair_undefended", TagOf(Classify(new Dictionary<string, float> { { "Raider", 100f } }, new[] { 0f, 100f, 0f, 0f }, 0, _ => false)));
        Assert.AreEqual("beach_overrun", TagOf(Classify(new Dictionary<string, float> { { "Raider", 100f } }, new[] { 100f, 0f, 0f, 0f }, 0, _ => true)));
        Assert.AreEqual("multi_beach", TagOf(Classify(new Dictionary<string, float> { { "Raider", 100f } }, new[] { 30f, 30f, 20f, 20f }, 0, _ => true)));
        Assert.IsFalse(Explained(Classify(new Dictionary<string, float>(), none, 0, _ => true)), "hiç hasar yok: etiketsiz");

        // Kapsam: rastgele (ama gerçekçi) hasar dağılımları; ≥%90 etiketli olmalı.
        var types = new[] { "Raider", "ShoreArcher", "ShieldLancer", "Rat", "Bat", "WoodGolem", "ButcherCaptain" };
        var rng = new System.Random(1234);
        int explained = 0, runs = 400;
        for (int i = 0; i < runs; i++)
        {
            var damage = new Dictionary<string, float>();
            int kinds = rng.Next(1, 5);
            for (int k = 0; k < kinds; k++) damage[types[rng.Next(types.Length)]] = 10f + (float)rng.NextDouble() * 200f;
            var beach = new float[4];
            for (int b = 0; b < 4; b++) beach[b] = (float)rng.NextDouble() * 100f;
            if (Explained(Classify(damage, beach, rng.Next(0, 12), _ => rng.Next(2) == 0))) explained++;
        }
        Assert.GreaterOrEqual(explained / (float)runs, 0.9f, "yenilgilerin ≥%90'ı etiketli olmalı");
    }

    // ==================== DM2D PREFAB DOĞUŞU ====================

    [UnityTest]
    public IEnumerator Dm2dMonstersSpawnWithSortingGroupPlayMoveAndDie()
    {
        yield return LoadIsland(1, 0);
        var catalogType = Type("IslandVisualCatalog");
        var catalog = catalogType.GetProperty("Instance").GetValue(null);
        Assert.NotNull(catalog, "katalog (Resources) yüklü");
        var castle = (Component)Prop(Setup, "CastleStructure");
        var spawn = Type("IslandEnemy").GetMethod("Spawn");
        foreach (var name in new[] { "Rat", "Bat", "WoodGolem", "ButcherCaptain" })
        {
            var entry = catalogType.GetMethod("Monster").Invoke(catalog, new object[] { name });
            Assert.NotNull(entry, $"{name}: katalog kaydı (prefab referansı)");
            Assert.NotNull(entry.GetType().GetField("prefab").GetValue(entry), $"{name}: prefab referansı");
            float target = (float)entry.GetType().GetField("targetHeight").GetValue(entry);
            if (name != "ButcherCaptain") Assert.LessOrEqual(target, 1.6f, $"{name}: Warrior'dan ≤1,6× (hedef boy)");

            var route = new[] { new Vector2(9f, 18f), new Vector2(9f, 13f) };
            var enemy = (Component)spawn.Invoke(null, new object[] { Enum.Parse(Type("EnemyType"), name), 1, castle.transform, new Vector2(9f, 18f), route, 0, name == "ButcherCaptain", false });
            Assert.IsTrue((bool)Prop(enemy, "IsMonster"), $"{name}: DM2D görseli");
            Assert.NotNull(enemy.GetComponent<UnityEngine.Rendering.SortingGroup>(), $"{name}: kökte SortingGroup");
            var animator = (Animator)Prop(enemy, "MonsterAnimator");
            Assert.NotNull(animator, $"{name}: Animator");
            string suffix = name == "WoodGolem" ? "Golem" : "";

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("Move" + suffix, Prop(enemy, "CurrentState"), $"{name}: yürürken Move oynar (animator.Play)");
            // Gövde boyu: tüm çocuk SpriteRenderer sınırları hedef yüksekliğin ±%45'i içinde (gövde normalize edilmiş)
            var renderers = enemy.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.sprite != null && r.name != "Shadow").ToArray();
            Assert.Greater(renderers.Length, 0, $"{name}: sprite parçaları");
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Assert.That(Mathf.Abs(bounds.center.x - enemy.transform.position.x), Is.LessThan(0.3f), $"{name}: gövde merkezi köke hizalı (|dx| {Mathf.Abs(bounds.center.x - enemy.transform.position.x):0.00} u)");
            float expected = target * (name == "ButcherCaptain" ? (float)IslandConst("BossScale") : 1f);
            Assert.That(bounds.size.y, Is.InRange(expected * 0.55f, expected * 1.6f), $"{name}: boy {bounds.size.y:0.00} u, hedef {expected:0.00}");

            // Ölüm: Death klibi, sonra yok olur
            var controller = Prop(enemy, "Controller");
            controller.GetType().GetMethod("TakeDamage").Invoke(controller, new object[] { 100000f, Enum.Parse(Type("DamageSource"), "Env") });
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("Death" + suffix, Prop(enemy, "CurrentState"), $"{name}: ölünce Death klibi");
            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(enemy == null, $"{name}: Death sonrası yok oldu");
        }
    }

    [UnityTest]
    public IEnumerator MonsterFlipsThroughLocalScaleXAndKeepsAnimatorParameterless()
    {
        yield return LoadIsland(1, 0);
        var castle = (Component)Prop(Setup, "CastleStructure");
        var spawn = Type("IslandEnemy").GetMethod("Spawn");
        // Doğuya (sağa) yürüyen Fare
        var east = (Component)spawn.Invoke(null, new object[] { Enum.Parse(Type("EnemyType"), "Rat"), 1, castle.transform, new Vector2(6f, 18f), new[] { new Vector2(12f, 18f) }, 0, false, false });
        var west = (Component)spawn.Invoke(null, new object[] { Enum.Parse(Type("EnemyType"), "Rat"), 1, castle.transform, new Vector2(16f, 19.5f), new[] { new Vector2(10f, 19.5f) }, 0, false, false });
        yield return new WaitForSeconds(0.6f);
        Assert.IsTrue((bool)Prop(east, "FacingRight"));
        Assert.IsFalse((bool)Prop(west, "FacingRight"));
        var eastScale = ((Transform)Prop(east, "MonsterVisual")).localScale.x;
        var westScale = ((Transform)Prop(west, "MonsterVisual")).localScale.x;
        Assert.AreNotEqual(Mathf.Sign(eastScale), Mathf.Sign(westScale), "yön localScale.x işaretiyle çevrilir");
    }

    // ==================== HEDEFLEME / YAPILAR / ONARIM ====================

    [UnityTest]
    public IEnumerator EveryBuildingIsAStructureAndRolesPickTheirTargets()
    {
        yield return LoadIsland(1, 0);
        var buildings = ((System.Collections.IEnumerable)Type("IslandBuilding").GetProperty("All").GetValue(null)).Cast<Component>().ToList();
        Assert.AreEqual(7, buildings.Count, "kale + ev + oduncu + ağıl + okçuluk + 2 adacık kulesi");
        foreach (var b in buildings)
        {
            Assert.NotNull(b.GetComponent(Type("BattleStructure")), $"{b.name}: BattleStructure");
            Assert.Greater((float)Prop(Prop(b, "Structure"), "MaxHealth"), 50f);
        }
        Assert.AreEqual(2, buildings.Count(b => b.GetComponent(Type("IslandTower")) != null), "iki adacık kulesi");
        // Kale canı CastleHp(1)
        var castle = (Component)Prop(Setup, "CastleStructure");
        Assert.AreEqual(300f, (float)Prop(castle, "MaxHealth"), 0.01f);

        var pick = Type("IslandTargeting").GetMethod("Pick");
        var enemyType = Type("EnemyType");
        Transform T(object result) => (Transform)result.GetType().GetField("target").GetValue(result);
        var house = buildings.First(b => b.name == "House");
        var pasture = buildings.First(b => b.name == "Pasture");
        var tower = buildings.First(b => b.name == "WatchTower");
        // Yakın: şeritte Eve 1,5 u içinde
        var nearHouse = (Vector2)Prop(house, "Center") + new Vector2(0f, 2.2f);
        Assert.AreEqual(house.transform, T(pick.Invoke(null, new object[] { Enum.Parse(enemyType, "Raider"), nearHouse, castle.transform, 1.4f })), "yakın: şerit üstündeki bina");
        Assert.IsNull(T(pick.Invoke(null, new object[] { Enum.Parse(enemyType, "Raider"), new Vector2(3f, 18f), castle.transform, 1.4f })), "uzakta: kale (varsayılan)");
        // Fare: en yakın üretim binası (≤9 u)
        var ratPick = T(pick.Invoke(null, new object[] { Enum.Parse(enemyType, "Rat"), (Vector2)Prop(pasture, "Center") + new Vector2(5f, -2f), castle.transform, 1f }));
        Assert.AreEqual(pasture.transform, ratPick, "Fare üretim binasına gider");
        // Menzilli: 5 u içindeki kule
        var towerCenter = (Vector2)Prop(tower, "Center");
        var shooterPick = T(pick.Invoke(null, new object[] { Enum.Parse(enemyType, "ShoreArcher"), towerCenter + new Vector2(0f, -3.5f), castle.transform, 5f }));
        Assert.NotNull(shooterPick, "Kıyı Okçusu kuleyi seçer");
        Assert.NotNull(shooterPick.GetComponent(Type("IslandBuilding")));
        // Yarasa: okçu; Golem: yalnız kale (null = varsayılan)
        var archer = (Component)Prop(Setup, "Archer");
        Assert.AreEqual(archer.transform, T(pick.Invoke(null, new object[] { Enum.Parse(enemyType, "Bat"), new Vector2(3f, 3f), castle.transform, 1f })), "Yarasa okçuya dalar");
        Assert.IsNull(T(pick.Invoke(null, new object[] { Enum.Parse(enemyType, "WoodGolem"), nearHouse, castle.transform, 1.6f })), "Tank yalnız kaleyi seçer");
    }

    [UnityTest]
    public IEnumerator DamagedBuildingsRepairForFreeBySecondsAndSlowProduction()
    {
        yield return LoadIsland(1, 0);
        long now = DateTime.UtcNow.Ticks;
        Type("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => now));
        var buildings = ((System.Collections.IEnumerable)Type("IslandBuilding").GetProperty("All").GetValue(null)).Cast<Component>().ToList();
        var lumber = buildings.First(b => b.name == "LumberCamp");
        var pasture = buildings.First(b => b.name == "Pasture");
        Assert.AreEqual(1f, (float)Call("IslandRepair", "ProductionMultiplier"), 0.001f);
        var lumberStructure = Prop(lumber, "Structure");
        float max = (float)Prop(lumberStructure, "MaxHealth");
        lumberStructure.GetType().GetMethod("TakeDamage").Invoke(lumberStructure, new object[] { max * 0.5f });
        var pastureStructure = Prop(pasture, "Structure");
        pastureStructure.GetType().GetMethod("TakeDamage").Invoke(pastureStructure, new object[] { max * 10f });   // yıkık
        Assert.IsTrue((bool)Prop(pastureStructure, "IsDestroyed"));
        Call("IslandRepair", "CommitBattle");
        Assert.IsTrue((bool)Prop(lumber, "UnderRepair"));
        Assert.IsTrue((bool)Prop(pasture, "UnderRepair"));
        Assert.AreEqual(20f + 100f * 0.5f, (float)Prop(lumber, "RepairSecondsLeft"), 0.6f, "20 sn + 100 sn × kayıp oran");
        Assert.AreEqual(180f, (float)Prop(pasture, "RepairSecondsLeft"), 0.6f, "yıkık bina 180 sn");
        Assert.AreEqual(0.5f, (float)Call("IslandRepair", "ProductionMultiplier"), 0.001f, "onarım sırasında üretim ×0,5");
        // Kayıt: tek satır / bina
        var save = Call("IslandSession", "get_Save");
        var list = (System.Collections.IList)save.GetType().GetField("buildings").GetValue(save);
        Assert.AreEqual(2, list.Count);
        // Süre dolar: tam can, simge yok, üretim normale döner
        now += (long)(200 * TimeSpan.TicksPerSecond);
        yield return new WaitForSeconds(0.3f);
        Assert.IsFalse((bool)Prop(lumber, "UnderRepair"));
        Assert.IsFalse((bool)Prop(pasture, "UnderRepair"));
        Assert.IsFalse((bool)Prop(pastureStructure, "IsDestroyed"), "yıkık bina yeniden kuruldu");
        Assert.AreEqual(max, (float)Prop(lumberStructure, "CurrentHealth"), 0.01f);
        Assert.AreEqual(1f, (float)Call("IslandRepair", "ProductionMultiplier"), 0.001f);
    }

    [UnityTest]
    public IEnumerator RaidBoardOpensFromAkinDock()
    {
        yield return LoadIsland(3, 8);
        var hud = Prop(Setup, "Hud");
        Assert.IsFalse((bool)Prop(hud, "BoardVisible"));
        var button = GameObject.Find("AkinButton");
        Assert.NotNull(button, "alt dock AKIN düğmesi");
        button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        Assert.IsTrue((bool)Prop(hud, "BoardVisible"), "AKIN düğmesi Akın Panosu'nu açar");
        Assert.NotNull(GameObject.Find("RaidBoard"));
    }

    // ==================== BOT MERDİVENİ ====================

    private IEnumerator RunRaid(int akin, string profile, bool strip, Action<string> result, int keepOverride = 0)
    {
        int keep = keepOverride > 0 ? keepOverride : Mathf.Max(1, Mathf.CeilToInt(akin / 3f));
        yield return LoadIsland(keep, akin - 1);
        var raid = Prop(Setup, "Raid");
        Type("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, Enum.Parse(Type("IslandBot+Profile"), profile) });
        if (strip) Type("IslandBot").GetMethod("StripDefenses").Invoke(null, null);
        Time.timeScale = 6f;
        float start = Time.realtimeSinceStartup;
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { akin });
        while (Prop(raid, "Phase").ToString() == "Running" && Time.realtimeSinceStartup - start < 150f) yield return null;
        Time.timeScale = 1f;
        string phase = Prop(raid, "Phase").ToString();
        float castle = (float)Prop(raid, "CastleHealthPercent");
        string tag = (string)Prop(raid, "DefeatTag");
        result($"{phase}|{castle:0.00}|{tag}|{Time.realtimeSinceStartup - start:0}s|bld{StaticInt("IslandStats", "BuildingsLost")}|shots{StaticInt("IslandStats", "Shots")}");
    }

    private static int StaticInt(string type, string field) => (int)Type(type).GetField(field).GetValue(null);

    [UnityTest, Timeout(900000)]
    public IEnumerator BotWinsRaidOneToFiveWithReasonableDefense()
    {
        for (int akin = 1; akin <= 5; akin++)
        {
            string outcome = null;
            yield return RunRaid(akin, "Good", false, r => outcome = r);
            UnityEngine.Debug.Log($"[IslandLadder] Akın {akin} (iyi bot): {outcome}");
            Assert.IsTrue(outcome.StartsWith("Won"), $"Akın {akin} makul savunmayla kazanılmalı: {outcome}");
        }
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator UndefendedRaidOneIsLostWithAnExplainedLabel()
    {
        string outcome = null;
        yield return RunRaid(1, "None", true, r => outcome = r);
        UnityEngine.Debug.Log($"[IslandLadder] Akın 1 (savunmasız): {outcome}");
        Assert.IsTrue(outcome.StartsWith("Lost"), $"savunmasız Akın 1 kaybedilmeli: {outcome}");
        var parts = outcome.Split('|');
        Assert.AreNotEqual("", parts[2], "yenilgi etiketi boş olmamalı");
        Assert.AreNotEqual("castle_fell", parts[2], "yenilgi açıklanmış olmalı");
    }

    /// <summary>Ölçüm (ölçüt yok, yalnız yenilgi etiketi kapsamı ≥%90): iyi bot 6–10, ilk dalga profilleri 1–10 ve savunmasız 2–9.</summary>
    [UnityTest, Timeout(3000000), Explicit("uzun ölçüm (≈6 dk): -testFilter ile adıyla çalıştırılır")]
    public IEnumerator MeasureLadderProfilesAndDefeatLabelCoverage()
    {
        var lines = new List<string>();
        int explained = 0, lost = 0;
        void Count(string outcome) { if (outcome.StartsWith("Lost")) { lost++; if (outcome.Split('|')[2] != "castle_fell") explained++; } }
        for (int akin = 6; akin <= 10; akin++)
        {
            string outcome = null;
            yield return RunRaid(akin, "Good", false, r => outcome = r);
            lines.Add($"Akın {akin} iyi bot (atış yeri + oto + dolu atış): {outcome}");
            Count(outcome);
        }
        for (int akin = 1; akin <= 10; akin++)
        {
            string outcome = null;
            yield return RunRaid(akin, "Casual", false, r => outcome = r);
            lines.Add($"Akın {akin} sıradan bot (yalnız oto-ateş): {outcome}");
            Count(outcome);
        }
        for (int akin = 1; akin <= 10; akin += 3)
        {
            string outcome = null;
            yield return RunRaid(akin, "None", false, r => outcome = r);
            lines.Add($"Akın {akin} yalnız kuleler (okçu yok): {outcome}");
            Count(outcome);
        }
        for (int akin = 2; akin <= 9; akin++)
        {
            string outcome = null;
            yield return RunRaid(akin, "None", true, r => outcome = r);
            lines.Add($"Akın {akin} savunmasız: {outcome}");
            Count(outcome);
        }
        lines.Add($"yenilgi etiket kapsamı: {explained}/{lost}");
        foreach (var line in lines) UnityEngine.Debug.Log("[IslandLadder] " + line);
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/island"));
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "ladder_profiles.txt"), lines);
        if (lost > 0) Assert.GreaterOrEqual(explained / (float)lost, 0.9f, "yenilgilerin ≥%90'ı etiketli: " + string.Join(" / ", lines));
    }

    /// <summary>Boss Akın'ı (10) ve 9. Akın tekrarlı ölçüm: iyi botun kazanma oranı (rastgelelik: kritik, dolu atış zamanlaması).</summary>
    [UnityTest, Timeout(3000000), Explicit("uzun ölçüm: -testFilter ile adıyla çalıştırılır")]
    public IEnumerator MeasureBossRaidWinRate()
    {
        var lines = new List<string>();
        foreach (var akin in new[] { 9, 10 })
        {
            foreach (int keep in new[] { Mathf.CeilToInt(akin / 3f), 5 })   // belge şartı (KK = ⌈N/3⌉) ve gerçek oyun (ada KK ≥5'te açılır)
            for (int run = 0; run < 3; run++)
            {
                string outcome = null;
                yield return RunRaid(akin, "Good", false, r => outcome = r, keep);
                lines.Add($"Akın {akin} KK{keep} iyi bot #{run + 1}: {outcome}");
            }
        }
        foreach (var line in lines) UnityEngine.Debug.Log("[IslandLadder] " + line);
        File.WriteAllLines(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/island")), "ladder_boss.txt"), lines);
    }

    [UnityTest, Timeout(600000), Explicit("tanı: -testFilter ile adıyla çalıştırılır")]
    public IEnumerator DiagnoseRaidTen()
    {
        yield return LoadIsland(4, 9);
        var raid = Prop(Setup, "Raid");
        Type("IslandBot").GetMethod("Attach").Invoke(null, new object[] { Setup, Enum.Parse(Type("IslandBot+Profile"), "Good") });
        Time.timeScale = 6f;
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 10 });
        float last = 0f, start = Time.time;
        var enemyList = Type("IslandEnemy").GetProperty("All");
        while (Prop(raid, "Phase").ToString() == "Running" && Time.time - start < 400f)
        {
            if (Time.time - last >= 10f)
            {
                last = Time.time;
                var all = ((System.Collections.IEnumerable)enemyList.GetValue(null)).Cast<Component>().Where(c => c != null).ToList();
                var kinds = string.Join(",", all.GroupBy(e => Prop(e, "Type").ToString()).Select(g => g.Key + "x" + g.Count()));
                UnityEngine.Debug.Log($"[Diag] t={Time.time - start:0} wave={Prop(raid, "Wave")} castle={(float)Prop(raid, "CastleHealthPercent"):0.00} enemies={all.Count} [{kinds}] landed={StaticInt("IslandStats", "Landed")} drowned={StaticInt("IslandStats", "Drowned")} shots={StaticInt("IslandStats", "Shots")} charged={StaticInt("IslandStats", "ChargedShots")}");
            }
            yield return null;
        }
        Time.timeScale = 1f;
        var dmg = (System.Collections.IDictionary)Type("IslandStats").GetField("DamageByType").GetValue(null);
        foreach (System.Collections.DictionaryEntry e in dmg) UnityEngine.Debug.Log($"[Diag] dmg {e.Key} = {e.Value}");
        var beach = (float[])Type("IslandStats").GetField("CastleDamageByBeach").GetValue(null);
        UnityEngine.Debug.Log($"[Diag] beachDmg {string.Join("/", beach)} phase={Prop(raid, "Phase")} tag={Prop(raid, "DefeatTag")} wave={Prop(raid, "Wave")}/{Prop(raid, "TotalWaves")}");
    }
}
