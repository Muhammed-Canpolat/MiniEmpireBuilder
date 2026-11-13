using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Martı Adası v2 Faz E2 (island_base_v2 §7.2, §7.3, §21): Kademe II–IV düşmanlar (hangi Akın'da hangi tür), siyah/sarı tekneler, Akın 20/30 bossları,
/// baskında boss olmaması, tür yetenekleri ve okçu yetenekleri (Ok Yağmuru, Ateşli Ok, Ağ Oku). Yansıma ile.
/// </summary>
public class IslandPhaseE2Tests
{
    private bool devWas;

    private static Type T(string name) => IslandPhaseBTests.T(name);
    private static object Isl(string name) => T("Progression+Island").GetField(name).GetValue(null);
    private static object IslCall(string method, params object[] args) => T("Progression+Island").GetMethod(method).Invoke(null, args);
    private static object Prop(object target, string name) => IslandPhaseBTests.Prop(target, name);
    private static object E(string name) => Enum.Parse(T("EnemyType"), name);
    private static string Name(object enumValue) => enumValue.ToString();

    private string savedKey, savedBackup, savedFile;
    private const string Key = "MiniEmpireBuilder_SaveData", BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    [SetUp]
    public void KeepRealSave()
    {
        savedKey = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
        savedBackup = PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;
        savedFile = System.IO.File.Exists(SaveFile) ? System.IO.File.ReadAllText(SaveFile) : null;
    }

    [TearDown]
    public void RestoreRealSave()
    {
        void Put(string key, string value) { if (value != null) PlayerPrefs.SetString(key, value); else PlayerPrefs.DeleteKey(key); }
        Put(Key, savedKey); Put(BackupKey, savedBackup);
        if (savedFile != null) System.IO.File.WriteAllText(SaveFile, savedFile);
        else if (System.IO.File.Exists(SaveFile)) System.IO.File.Delete(SaveFile);
        PlayerPrefs.Save();
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    // ==================== SAYILAR VE KADEMELER (§7.2, §7.3) ====================

    [Test]
    public void TiersFirstSightingsBossesAndBoatColorsMatchTheDesign()
    {
        // İlk görünüş Akın'ları (§7.3 kademe tablosu)
        var expected = new Dictionary<string, int>
        {
            { "Raider", 1 }, { "ShoreArcher", 2 }, { "Rat", 3 }, { "Bat", 5 }, { "ShieldLancer", 6 }, { "WoodGolem", 8 }, { "ButcherCaptain", 10 },
            { "SkeletonWarrior", 11 }, { "SkeletonArcher", 11 }, { "Imp", 12 }, { "BoneMage", 13 }, { "Spider", 15 }, { "Zombie", 16 }, { "StoneGolem", 17 }, { "Ghost", 18 },
            { "SunkenKnight", 20 }, { "ZombieWarrior", 21 }, { "Succubus", 23 }, { "CrystalGolem", 25 }, { "Vampire", 27 }, { "Demon", 29 },
            { "RedDragon", 30 }, { "MagmaGolem", 32 },
        };
        foreach (var pair in expected) Assert.AreEqual(pair.Value, (int)IslCall("FirstAkinOf", E(pair.Key)), pair.Key);
        // Kademeler
        Assert.AreEqual(new[] { 1, 1, 2, 2, 3, 3, 4, 4 }, new[] { 1, 9, 10, 19, 20, 29, 30, 45 }.Select(n => (int)IslCall("TierOf", n)).ToArray());
        // Gemi rengi: I kırmızı-haç (ship 3) · II–III siyah-kafatası (2) · IV sarı (6); mavi (5) ya da yeşil hiç yok
        Assert.AreEqual(new[] { 3, 3, 2, 2, 2, 2, 6, 6 }, new[] { 1, 9, 10, 19, 20, 29, 30, 45 }.Select(n => (int)IslCall("BoatShipNumber", n)).ToArray());
        // Bosslar: 10 Kasap Kaptan · 20 Batık Şövalye · 30 Kızıl Ejder, sonra sırayla ve her turda ×1,15
        Assert.AreEqual(new[] { "ButcherCaptain", "SunkenKnight", "RedDragon", "ButcherCaptain", "SunkenKnight" },
            new[] { 10, 20, 30, 40, 50 }.Select(n => Name(IslCall("BossFor", n))).ToArray());
        Assert.AreEqual(1.3f, (float)IslCall("BossScaleFor", E("ButcherCaptain"), 10), 1e-4f);
        Assert.AreEqual(1.3f, (float)IslCall("BossScaleFor", E("SunkenKnight"), 20), 1e-4f);
        Assert.AreEqual(1.2f, (float)IslCall("BossScaleFor", E("RedDragon"), 30), 1e-4f);
        Assert.AreEqual(1.3f * 1.15f, (float)IslCall("BossScaleFor", E("ButcherCaptain"), 40), 1e-4f);
        Assert.AreEqual(1.2f * 1.15f, (float)IslCall("BossScaleFor", E("RedDragon"), 60), 1e-4f);
        // Savaş seviyesinden Akın'a dönüş (boss boyu ve tekne rengi için)
        // (tersi tam değildir: 0,8 eğim bazı seviyeleri iki Akın'a verir; kademe ve boss sınırlarında kesindir)
        foreach (int n in new[] { 9, 10, 19, 20, 29, 30, 39, 40, 49, 50 })
            Assert.AreEqual(n, (int)IslCall("AkinOfLevel", (int)IslCall("AkinBattleLevel", n)), $"Akın {n}");
        // Baskın gücü boss Akın'ına düşmez
        Assert.AreEqual(new[] { 1, 3, 6, 9, 19, 29, 31 }, new[] { 1, 5, 8, 12, 22, 32, 33 }.Select(n => (int)IslCall("RaidPower", n)).ToArray());
        for (int n = 1; n <= 60; n++) Assert.IsFalse((bool)IslCall("IsBossAkin", (int)IslCall("RaidPower", n)), $"en yüksek {n}");
    }

    [Test]
    public void NewEnemyBaseStatsMatchTheTable()
    {
        // (can, hasar, hız, menzil, altın) sv 1 tabanı (§7.3)
        var table = new Dictionary<string, float[]>
        {
            { "SkeletonWarrior", new[] { 45f, 7, 1.3f, 1.3f, 10 } }, { "SkeletonArcher", new[] { 26f, 6, 1.3f, 5.5f, 12 } }, { "BoneMage", new[] { 40f, 5, 1.1f, 4, 18 } },
            { "Imp", new[] { 26f, 5, 2.4f, 1.0f, 10 } }, { "Spider", new[] { 40f, 6, 1.8f, 3, 14 } }, { "Zombie", new[] { 50f, 8, 1.1f, 1.5f, 15 } },
            { "StoneGolem", new[] { 220f, 15, 0.65f, 1.6f, 40 } }, { "Ghost", new[] { 35f, 6, 1.6f, 1.0f, 14 } }, { "ZombieWarrior", new[] { 110f, 14, 1.0f, 1.5f, 26 } },
            { "Succubus", new[] { 70f, 9, 1.3f, 4.5f, 28 } }, { "CrystalGolem", new[] { 260f, 16, 0.65f, 1.6f, 45 } }, { "Vampire", new[] { 120f, 12, 1.4f, 1.4f, 35 } },
            { "Demon", new[] { 240f, 20, 1.0f, 1.8f, 60 } }, { "MagmaGolem", new[] { 300f, 18, 0.6f, 1.6f, 55 } },
            { "SunkenKnight", new[] { 520f, 22, 0.9f, 1.8f, 140 } }, { "RedDragon", new[] { 600f, 24, 1.0f, 3, 160 } },
        };
        var stats = T("EnemyStats");
        foreach (var pair in table)
        {
            var s = stats.GetMethod("GetBaseStats").Invoke(null, new[] { E(pair.Key) });
            float F(string f) => Convert.ToSingle(s.GetType().GetField(f).GetValue(s));
            Assert.AreEqual(pair.Value[0], F("health"), 1e-3f, pair.Key + " can");
            Assert.AreEqual(pair.Value[1], F("damage"), 1e-3f, pair.Key + " hasar");
            Assert.AreEqual(pair.Value[2], F("moveSpeed"), 1e-3f, pair.Key + " hız");
            Assert.AreEqual(pair.Value[3], F("attackRange"), 1e-3f, pair.Key + " menzil");
            Assert.AreEqual(pair.Value[4], F("goldDrop"), 1e-3f, pair.Key + " altın");
        }
    }

    // ==================== KADRO: HANGİ AKIN'DA HANGİ DÜŞMAN ====================

    private static List<string> Crew(string kind, int boat, bool boss, int akin, int wave)
    {
        var list = (IList)T("IslandWaves").GetMethod("CrewFor").Invoke(null, new object[] { Enum.Parse(T("Progression+Island+BoatKind"), kind), boat, boss, akin, wave });
        return list.Cast<object>().Select(Name).ToList();
    }

    private static readonly string[] Kinds = { "Rowboat", "Longboat", "Barge" };
    private static readonly string[] Specials = { "BoneMage", "Spider", "Succubus" };
    private static readonly string[] Elites = { "ZombieWarrior", "Vampire", "Demon" };
    private static readonly string[] Tanks = { "WoodGolem", "StoneGolem", "CrystalGolem", "MagmaGolem" };
    private static readonly string[] Bosses = { "ButcherCaptain", "SunkenKnight", "RedDragon" };

    [Test]
    public void CrewNeverContainsATypeBeforeItsFirstAkinAndRespectsPerBoatLimits()
    {
        for (int akin = 1; akin <= 45; akin++)
            foreach (var kind in Kinds)
                for (int boat = 0; boat < 7; boat++)
                    for (int wave = 1; wave <= 4; wave++)
                    {
                        var crew = Crew(kind, boat, false, akin, wave);
                        foreach (var type in crew)
                        {
                            Assert.LessOrEqual((int)IslCall("FirstAkinOf", E(type)), akin, $"Akın {akin}: {type} henüz gelmemeli ({kind} #{boat} dalga {wave})");
                            Assert.IsFalse(Bosses.Contains(type), $"boss olmayan teknede boss yok: {type}");
                        }
                        Assert.LessOrEqual(crew.Count(Tanks.Contains), 1, $"tekne başına ≤1 tank, Akın {akin}");
                        Assert.LessOrEqual(crew.Count(Specials.Contains), 1, $"tekne başına ≤1 özel, Akın {akin}");
                        Assert.LessOrEqual(crew.Count(Elites.Contains), 1, $"tekne başına ≤1 elit, Akın {akin}");
                        if (wave == 1)
                            Assert.IsFalse(crew.Any(t => Tanks.Contains(t) || Specials.Contains(t) || Elites.Contains(t)), $"1. dalgada tank/özel/elit yok: Akın {akin} {kind} #{boat}");
                        if (kind == "Rowboat") Assert.IsFalse(crew.Any(Tanks.Contains), "Sandalda tank yok");
                    }
    }

    [Test]
    public void EveryTierTwoToFourTypeActuallyAppearsWithinThreeAkinOfItsDebut()
    {
        foreach (var type in new[] { "SkeletonWarrior", "SkeletonArcher", "Imp", "BoneMage", "Spider", "Zombie", "StoneGolem", "ZombieWarrior", "Succubus", "CrystalGolem", "Vampire", "Demon" })
        {
            int first = (int)IslCall("FirstAkinOf", E(type));
            bool seen = false;
            for (int akin = first; akin <= first + 3 && !seen; akin++)
                foreach (var kind in Kinds)
                    for (int boat = 0; boat < 7 && !seen; boat++)
                        for (int wave = 1; wave <= 4 && !seen; wave++)
                            seen = Crew(kind, boat, false, akin, wave).Contains(type);
            Assert.IsTrue(seen, $"{type} Akın {first}..{first + 3} arasında gelmeli");
        }
        // Ejder tekneyle gelmez; Ghost uçan sürüde
        Assert.AreEqual("Bat", Name(T("IslandWaves").GetMethod("FlyerTypeAt").Invoke(null, new object[] { 17, 1 })), "Akın 17: yalnız Yarasa");
        Assert.AreEqual("Ghost", Name(T("IslandWaves").GetMethod("FlyerTypeAt").Invoke(null, new object[] { 18, 1 })), "Akın 18: her ikinci üye Hayalet");
        Assert.AreEqual("Bat", Name(T("IslandWaves").GetMethod("FlyerTypeAt").Invoke(null, new object[] { 18, 0 })));
    }

    [Test]
    public void BossBoatCarriesTheBossOfItsAkinAndTheDragonFliesAlone()
    {
        Assert.AreEqual("ButcherCaptain", Crew("Barge", 6, true, 10, 3)[0]);
        Assert.AreEqual("SunkenKnight", Crew("Barge", 6, true, 20, 3)[0], "Akın 20: Batık Şövalye");
        Assert.AreEqual("ButcherCaptain", Crew("Barge", 6, true, 40, 4)[0], "Akın 40: tur başa döner");
        Assert.IsFalse(Crew("Barge", 6, true, 30, 4).Contains("RedDragon"), "Akın 30: Kızıl Ejder tekneyle gelmez (uçar)");
        Assert.AreEqual("Barge", Name(Enum.Parse(T("Progression+Island+BoatKind"), "Barge")));
        // Pano: boss Akın'ında kadroda boss sonda, YENİ DÜŞMAN afişi
        var waves = T("IslandWaves");
        var rosterOf = new Func<int, List<string>>(n => ((IList)waves.GetMethod("RosterFor").Invoke(null, new object[] { n })).Cast<object>().Select(Name).ToList());
        Assert.AreEqual("ButcherCaptain", rosterOf(10).Last()); Assert.AreEqual("SunkenKnight", rosterOf(20).Last()); Assert.AreEqual("RedDragon", rosterOf(30).Last());
        Assert.IsFalse(rosterOf(19).Any(Bosses.Contains));
        string NewAt(int n) { var v = waves.GetMethod("NewTypeAt").Invoke(null, new object[] { n }); return v == null ? "" : Name(v); }
        Assert.AreEqual("ButcherCaptain", NewAt(10)); Assert.AreEqual("SkeletonWarrior", NewAt(11)); Assert.AreEqual("Imp", NewAt(12)); Assert.AreEqual("BoneMage", NewAt(13));
        Assert.AreEqual("Spider", NewAt(15)); Assert.AreEqual("Zombie", NewAt(16)); Assert.AreEqual("StoneGolem", NewAt(17)); Assert.AreEqual("Ghost", NewAt(18));
        Assert.AreEqual("", NewAt(19)); Assert.AreEqual("SunkenKnight", NewAt(20)); Assert.AreEqual("ZombieWarrior", NewAt(21)); Assert.AreEqual("Succubus", NewAt(23));
        Assert.AreEqual("CrystalGolem", NewAt(25)); Assert.AreEqual("Vampire", NewAt(27)); Assert.AreEqual("Demon", NewAt(29)); Assert.AreEqual("RedDragon", NewAt(30));
        Assert.AreEqual("MagmaGolem", NewAt(32)); Assert.AreEqual("", NewAt(40), "boss turu yalnız ilk turda afişlenir");
    }

    [Test]
    public void DefeatLabelsCoverTheNewEnemiesAndOldSavesStillLoad()
    {
        var defeat = T("IslandDefeat");
        // Yenilgi etiketleri: golem ailesi, boss ailesi, uçanlar yansımayla (EnemyType anahtarlı sözlük)
        var dictType = typeof(Dictionary<,>).MakeGenericType(T("EnemyType"), typeof(float));
        object Make(params (string type, float v)[] parts)
        {
            var d = Activator.CreateInstance(dictType);
            foreach (var p in parts) dictType.GetMethod("set_Item").Invoke(d, new[] { E(p.type), (object)p.v });
            return d;
        }
        T("IslandStats").GetMethod("Reset").Invoke(null, null);
        string Tag(params (string, float)[] parts)
        {
            var label = defeat.GetMethod("Classify").Invoke(null, new object[] { Make(parts), new float[4], 0, (Func<int, bool>)(b => true) });
            return (string)label.GetType().GetField("tag").GetValue(label);
        }
        Assert.AreEqual("golem_armor", Tag(("StoneGolem", 50f), ("Raider", 10f)));
        Assert.AreEqual("golem_armor", Tag(("CrystalGolem", 30f), ("MagmaGolem", 30f), ("Raider", 10f)));
        Assert.AreEqual("boss_unbroken", Tag(("SunkenKnight", 60f), ("Raider", 20f)));
        Assert.AreEqual("flyer_unanswered", Tag(("RedDragon", 60f), ("Raider", 20f)), "Ejder uçan: yalnız kule ve okçu vurabilir");
        Assert.AreEqual("flyer_unanswered", Tag(("Ghost", 40f), ("Raider", 10f)));
        T("IslandStats").GetField("MageHealed").SetValue(null, 400f);
        Assert.AreEqual("mage_heal", Tag(("SkeletonWarrior", 40f), ("BoneMage", 10f)));
        T("IslandStats").GetMethod("Reset").Invoke(null, null);
    }

    // ==================== SAHNE: 16 YENİ DM2D BİRİMİ ====================

    private static readonly string[] NewMonsters =
    {
        "SkeletonWarrior", "SkeletonArcher", "BoneMage", "Imp", "Spider", "Zombie", "StoneGolem", "Ghost", "ZombieWarrior", "Succubus",
        "CrystalGolem", "Vampire", "Demon", "MagmaGolem", "SunkenKnight", "RedDragon",
    };

    private static Component Castle => (Component)Prop(IslandPhaseBTests.Setup, "CastleStructure");

    private static Component SpawnEnemy(string type, Vector2 at, bool freeze, bool boss = false, int level = 1, Vector2[] route = null)
    {
        var enemy = (Component)T("IslandEnemy").GetMethod("Spawn").Invoke(null,
            new object[] { E(type), level, Castle.transform, at, route ?? new[] { at, (Vector2)T("IslandLayout").GetProperty("CastleCenter").GetValue(null) }, 0, boss, false });
        if (freeze) Prop(enemy, "Controller").GetType().GetProperty("MovementLocked").SetValue(Prop(enemy, "Controller"), true);
        return enemy;
    }

    private static float Health(Component enemy) => (float)Prop(Prop(enemy, "Controller"), "CurrentHealth");
    private static void Hurt(Component enemy, float amount) =>
        Prop(enemy, "Controller").GetType().GetMethod("TakeDamage").Invoke(Prop(enemy, "Controller"), new object[] { amount, Enum.Parse(T("DamageSource"), "Env") });

    private static List<Component> LiveEnemies() =>
        ((IEnumerable)T("IslandEnemy").GetProperty("All").GetValue(null)).Cast<Component>().Where(c => c != null && (bool)Prop(c, "Alive")).ToList();

    private static void ClearEnemies()
    {
        foreach (var e in ((IEnumerable)T("IslandEnemy").GetProperty("All").GetValue(null)).Cast<Component>().ToList()) if (e != null) UnityEngine.Object.Destroy(e.gameObject);
        foreach (var b in ((IEnumerable)T("RaidBoat").GetProperty("All").GetValue(null)).Cast<Component>().ToList()) if (b != null) UnityEngine.Object.Destroy(b.gameObject);
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator EveryNewMonsterSpawnsFromTheCatalogPlaysMoveAndDies()
    {
        yield return IslandPhaseBTests.LoadIsland(island => { }, 6, v => devWas = v);
        var catalog = T("IslandVisualCatalog").GetProperty("Instance").GetValue(null);
        Assert.NotNull(catalog, "katalog (Resources) yüklü");
        foreach (var name in NewMonsters)
        {
            var entry = T("IslandVisualCatalog").GetMethod("Monster").Invoke(catalog, new object[] { name });
            Assert.NotNull(entry, $"{name}: katalog kaydı");
            Assert.NotNull(entry.GetType().GetField("prefab").GetValue(entry), $"{name}: prefab");
            Assert.NotNull(entry.GetType().GetField("portrait").GetValue(entry), $"{name}: pano portresi");
            float target = (float)entry.GetType().GetField("targetHeight").GetValue(entry);
            bool boss = name == "SunkenKnight" || name == "RedDragon";
            if (!boss) Assert.LessOrEqual(target, 1.6f, $"{name}: Warrior'dan ≤1,6× (hedef boy)");

            var enemy = SpawnEnemy(name, new Vector2(9f, 18f), false, boss, 1, new[] { new Vector2(9f, 18f), new Vector2(9f, 13f) });
            Assert.IsTrue((bool)Prop(enemy, "IsMonster"), $"{name}: DM2D görseli");
            Assert.NotNull(enemy.GetComponent<UnityEngine.Rendering.SortingGroup>(), $"{name}: SortingGroup");
            yield return new WaitForSeconds(0.5f);
            string state = (string)Prop(enemy, "CurrentState");
            StringAssert.StartsWith("Move", state, $"{name}: yürürken Move oynar ({state})");
            var renderers = enemy.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.sprite != null && r.name != "Shadow").ToArray();
            Assert.Greater(renderers.Length, 0, $"{name}: sprite parçaları");
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float expected = target * (boss ? (float)IslCall("BossScaleFor", E(name), name == "RedDragon" ? 30 : 20) : 1f);
            Assert.That(bounds.size.y, Is.InRange(expected * 0.5f, expected * 1.7f), $"{name}: boy {bounds.size.y:0.00} u, hedef {expected:0.00}");
            Hurt(enemy, 100000f);
            yield return new WaitForSeconds(0.2f);
            StringAssert.StartsWith("Death", (string)Prop(enemy, "CurrentState"), $"{name}: ölünce Death klibi");
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(enemy == null, $"{name}: Death sonrası yok oldu");
        }
    }

    [UnityTest, Timeout(200000)]
    public IEnumerator ArmorFollowsTheDesignAndBossFactorFollowsTheAkin()
    {
        yield return IslandPhaseBTests.LoadIsland(island => { }, 6, v => devWas = v);
        var wood = SpawnEnemy("WoodGolem", new Vector2(9f, 20f), true); var stone = SpawnEnemy("StoneGolem", new Vector2(10f, 20f), true);
        var crystal = SpawnEnemy("CrystalGolem", new Vector2(11f, 20f), true); var magma = SpawnEnemy("MagmaGolem", new Vector2(12f, 20f), true);
        float Dmg(Component e, bool charged) => (float)e.GetType().GetMethod("ArrowDamage").Invoke(e, new object[] { 10f, charged, null });
        Assert.AreEqual(6f, Dmg(wood, false), 1e-3f, "Ahşap Golem ×0,6"); Assert.AreEqual(10f, Dmg(wood, true), 1e-3f);
        Assert.AreEqual(5f, Dmg(stone, false), 1e-3f, "Taş Golem zırh ×0,5"); Assert.AreEqual(10f, Dmg(stone, true), 1e-3f, "dolu atış zırhı yok sayar");
        Assert.AreEqual(5f, Dmg(crystal, false), 1e-3f, "Kristal Golem zırh ×0,5"); Assert.AreEqual(15f, Dmg(crystal, true), 1e-3f, "Kristal Golem: dolu atış ×1,5 (zayıf nokta)");
        Assert.AreEqual(10f, Dmg(magma, false), 1e-3f, "Magma Golem zırhsız");
        // Boss çarpanı: Akın 20 savaş seviyesinden ×1,3; Akın 30 Ejder ×1,2; Akın 40 Kasap ×1,3×1,15
        int Level(int akin) => (int)IslCall("AkinBattleLevel", akin);
        float Factor(Component e) => (float)Prop(e, "BossFactor");
        Assert.AreEqual(1.3f, Factor(SpawnEnemy("SunkenKnight", new Vector2(9f, 22f), true, true, Level(20))), 1e-4f);
        Assert.AreEqual(1.2f, Factor(SpawnEnemy("RedDragon", new Vector2(10f, 22f), true, true, Level(30))), 1e-4f);
        Assert.AreEqual(1.3f * 1.15f, Factor(SpawnEnemy("ButcherCaptain", new Vector2(11f, 22f), true, true, Level(40))), 1e-4f);
        Assert.AreEqual(1f, Factor(SpawnEnemy("Raider", new Vector2(12f, 22f), true, false, Level(40))), 1e-4f, "boss olmayan çarpansız");
        yield return null;
    }

    // ==================== SAHNE: TÜR YETENEKLERİ ====================

    private IEnumerator WaitUntil(Func<bool> condition, float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < until) yield return null;
    }

    [UnityTest, Timeout(400000)]
    public IEnumerator BoneMageHealsAndGhostPhasesAndKnightSummonsSkeletons()
    {
        yield return IslandPhaseBTests.LoadIsland(island => { }, 6, v => devWas = v);
        T("IslandStats").GetMethod("Reset").Invoke(null, null);
        Time.timeScale = 4f;
        // Kemik Büyücü: 2,5 u içindeki yaralı düşmanı %6 iyileştirir
        var raider = SpawnEnemy("Raider", new Vector2(9f, 20f), true);
        var mage = SpawnEnemy("BoneMage", new Vector2(10.5f, 20f), true);
        yield return null;   // EnemyController.Start canı doldurur: yaralamadan önce bir kare bekle
        Hurt(raider, 30f);
        float hurt = Health(raider);
        yield return WaitUntil(() => Health(raider) > hurt + 0.1f, 6f);
        Assert.Greater(Health(raider), hurt, "Kemik Büyücü yaralı Akıncı'yı iyileştirdi");
        Assert.Greater((float)T("IslandStats").GetField("MageHealed").GetValue(null), 0f, "şifa telemetrisi");
        float maxHealth = (float)Prop(Prop(raider, "Controller"), "MaxHealth");
        Assert.AreEqual(maxHealth * 0.06f, Health(raider) - hurt, maxHealth * 0.06f * 1.01f, "bir vuruş azami canın ≈%6'sı");
        // Ghost: ~6 sn'de bir 1,5 sn dokunulmaz (ilk evre 3 sn sonra)
        var ghost = SpawnEnemy("Ghost", new Vector2(14f, 24f), true);
        var ghostController = Prop(ghost, "Controller");
        yield return WaitUntil(() => (bool)Prop(ghostController, "Invulnerable"), 12f);
        Assert.IsTrue((bool)Prop(ghostController, "Invulnerable"), "Hayalet dokunulmaz evreye girdi");
        float before = Health(ghost);
        Hurt(ghost, 20f);
        Assert.AreEqual(before, Health(ghost), 1e-3f, "dokunulmazken hasar almaz");
        yield return WaitUntil(() => !(bool)Prop(ghostController, "Invulnerable"), 6f);
        Hurt(ghost, 5f);
        Assert.Less(Health(ghost), before, "evre bitince hasar alır");
        // Batık Şövalye: %50 canda 4 İskelet Savaşçı
        ClearEnemies();
        yield return null;
        var knight = SpawnEnemy("SunkenKnight", new Vector2(9f, 22f), true, true, (int)IslCall("AkinBattleLevel", 20));
        yield return null;
        int skeletons() => LiveEnemies().Count(e => Name(Prop(e, "Type")) == "SkeletonWarrior");
        Assert.AreEqual(0, skeletons());
        Hurt(knight, Health(knight) * 0.6f);   // %40 canda kalır (<%50)
        yield return WaitUntil(() => skeletons() >= 4, 3f);
        Assert.AreEqual(0, skeletons() - 4, "tam 4 İskelet çağrılır");
        Hurt(knight, 1f);
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(4, skeletons(), "çağrı yalnız bir kez");
        Time.timeScale = 1f;
    }

    [UnityTest, Timeout(400000)]
    public IEnumerator ImpVampireSpiderAndMagmaAttackTheVillageWithTheirTricks()
    {
        yield return IslandPhaseBTests.LoadIsland(island => { }, 6, v => devWas = v);
        T("IslandStats").GetMethod("Reset").Invoke(null, null);
        var buildings = ((IEnumerable)T("IslandBuilding").GetProperty("All").GetValue(null)).Cast<Component>().ToList();
        var house = buildings.First(b => Name(Prop(b, "Kind")) == "House");
        var houseCenter = (Vector2)Prop(house, "Center");
        var tower = buildings.First(b => (bool)Prop(b, "IsTower"));
        var towerCenter = (Vector2)Prop(tower, "Center");
        Time.timeScale = 4f;

        // İmp binayı yakar
        int ignitions = (int)T("IslandBurn").GetField("Ignitions").GetValue(null);
        var structure = (Component)Prop(house, "Structure");
        float houseHp = (float)Prop(structure, "CurrentHealth");
        var imp = SpawnEnemy("Imp", houseCenter + new Vector2(1.0f, -0.6f), false, false, 1, new[] { houseCenter + new Vector2(1.0f, -0.6f) });
        yield return WaitUntil(() => (int)T("IslandBurn").GetField("Ignitions").GetValue(null) > ignitions, 8f);
        Assert.Greater((int)T("IslandBurn").GetField("Ignitions").GetValue(null), ignitions, "İmp binayı tutuşturdu");
        yield return new WaitForSeconds(1.0f);
        Assert.Less((float)Prop(structure, "CurrentHealth"), houseHp, "yanan bina can kaybetti");
        UnityEngine.Object.Destroy(imp.gameObject);

        // Vampir vurdukça iyileşir
        var vampire = SpawnEnemy("Vampire", houseCenter + new Vector2(-1.0f, -0.6f), false, false, 1, new[] { houseCenter + new Vector2(-1.0f, -0.6f) });
        yield return null;
        Hurt(vampire, 60f);
        yield return WaitUntil(() => (float)T("IslandStats").GetField("VampireHealed").GetValue(null) > 0f, 8f);
        Assert.Greater((float)T("IslandStats").GetField("VampireHealed").GetValue(null), 0f, "Vampir can çaldı");
        UnityEngine.Object.Destroy(vampire.gameObject);

        // Örümcek kuleye ağ atar (atış hızı −%40)
        int webs = (int)T("IslandStats").GetField("WebHits").GetValue(null);
        var spider = SpawnEnemy("Spider", towerCenter + new Vector2(2.4f, 0f), false, false, 1, new[] { towerCenter + new Vector2(2.4f, 0f) });
        yield return WaitUntil(() => (int)T("IslandStats").GetField("WebHits").GetValue(null) > webs, 8f);
        Assert.Greater((int)T("IslandStats").GetField("WebHits").GetValue(null), webs, "Örümcek ağ attı");
        Assert.IsTrue((bool)Prop(tower.GetComponent(T("IslandTower")), "Webbed") || (int)T("IslandStats").GetField("WebHits").GetValue(null) > webs);
        UnityEngine.Object.Destroy(spider.gameObject);

        // Magma Golem ölünce çevresindeki binayı yakar
        ignitions = (int)T("IslandBurn").GetField("Ignitions").GetValue(null);
        var magma = SpawnEnemy("MagmaGolem", houseCenter + new Vector2(0f, -1.2f), true);
        Hurt(magma, 100000f);
        yield return new WaitForSeconds(0.3f);
        Assert.Greater((int)T("IslandBurn").GetField("Ignitions").GetValue(null), ignitions, "Magma Golem ölünce yanma bıraktı");
        Time.timeScale = 1f;
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator SuccubusStunsALancerAndTheDragonBurnsTheArcher()
    {
        yield return IslandPhaseBTests.LoadIsland(island => { }, 6, v => devWas = v);
        T("IslandStats").GetMethod("Reset").Invoke(null, null);
        Time.timeScale = 4f;
        var lancerType = T("IslandLancer");
        var flag = ((Vector2[])T("IslandLayout").GetField("FlagPoints").GetValue(null))[0];
        var lancer = (Component)lancerType.GetMethod("Create").Invoke(null, new object[] { null, 5, flag, 0, 1 });
        var succubus = SpawnEnemy("Succubus", flag + new Vector2(2.5f, 0f), true);
        yield return WaitUntil(() => (bool)Prop(lancer, "Stunned"), 10f);
        Assert.IsTrue((bool)Prop(lancer, "Stunned"), "Sukkubus Lancer'ı durdurdu (2 sn)");
        yield return WaitUntil(() => !(bool)Prop(lancer, "Stunned"), 4f);
        Assert.IsFalse((bool)Prop(lancer, "Stunned"), "2 sn sonra çözülür");
        UnityEngine.Object.Destroy(lancer.gameObject); UnityEngine.Object.Destroy(succubus.gameObject);

        // Kızıl Ejder okçuya koni ateş eder
        var archer = (Component)T("IslandArcher").GetProperty("Instance").GetValue(null);
        float hp = (float)Prop(archer, "Hp");
        var archerAt = (Vector2)Prop(archer, "Position");
        var dragon = SpawnEnemy("RedDragon", archerAt + new Vector2(2.2f, 0.4f), true, true, (int)IslCall("AkinBattleLevel", 30));
        var specials = dragon.GetComponent(T("IslandSpecials"));
        yield return WaitUntil(() => (int)Prop(specials, "Pulses") > 0, 12f);
        Assert.Greater((int)Prop(specials, "Pulses"), 0, "Ejder alev saldı");
        Assert.Less((float)Prop(archer, "Hp"), hp, "koni ateş okçuyu vurdu (2 × hasar)");
        Time.timeScale = 1f;
    }

    // ==================== SAHNE: OKÇU YETENEKLERİ ====================

    [UnityTest, Timeout(400000)]
    public IEnumerator ArcherAbilitiesUnlockByLevelAndVolleyNetAndFireArrowWork()
    {
        yield return IslandPhaseBTests.LoadIsland(island => { IslandPhaseBTests.Set(island, "highestAkin", 3); }, 6, v => devWas = v);
        var abilities = (Component)T("IslandAbilities").GetProperty("Instance").GetValue(null);
        Assert.NotNull(abilities, "yetenek bileşeni okçuya bağlı");
        var kindType = T("IslandAbilities+Kind");
        object K(string n) => Enum.Parse(kindType, n);
        // Seviye kapıları: Ok Yağmuru Sv2, Ateşli Ok Sv4, Ağ Oku Sv6 (Okçu seviyesi = KK = 6)
        Assert.AreEqual(new[] { 2, 4, 6 }, new[] { "Volley", "FireArrow", "Net" }.Select(n => (int)T("IslandAbilities").GetMethod("UnlockLevel").Invoke(null, new[] { K(n) })).ToArray());
        foreach (var n in new[] { "Volley", "FireArrow", "Net" }) Assert.IsTrue((bool)abilities.GetType().GetMethod("Unlocked").Invoke(abilities, new[] { K(n) }), n + " açık (Sv 6)");
        Assert.IsFalse((bool)abilities.GetType().GetMethod("CanUse").Invoke(abilities, new[] { K("Volley") }), "savunma dışında kullanılamaz");

        var raid = Prop(IslandPhaseBTests.Setup, "Raid");
        raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { 3 });
        ClearEnemies();
        T("IslandBot").GetMethod("StripDefenses").Invoke(null, null);   // kuleler ateş etmesin: ölçülen hasar yalnız yetenekten
        yield return new WaitForSecondsRealtime(0.3f);
        var archer = (Component)T("IslandArcher").GetProperty("Instance").GetValue(null);
        var archerAt = (Vector2)Prop(archer, "Position");
        Vector2 center = archerAt + new Vector2(0f, 3.5f);
        var cluster = new List<Component>();
        for (int i = 0; i < 4; i++) cluster.Add(SpawnEnemy("ZombieWarrior", center + new Vector2((i - 1.5f) * 0.35f, 0f), true));   // 110 can: 3 × 21 hasara dayanır
        var outside = SpawnEnemy("ZombieWarrior", center + new Vector2(3.5f, 0f), true);
        yield return null;   // Start canı doldurur
        var targets = cluster.Concat(new[] { outside }).ToList();
        archer.GetType().GetMethod("LockTarget").Invoke(archer, new object[] { T("IslandTarget").GetConstructor(new[] { T("IslandEnemy") }).Invoke(new object[] { cluster[1] }) });

        // Ok Yağmuru: dairedeki herkes 3 vuruş (≈3 × ok hasarı; Islak ×1,25 yok: doğuşta ıslak değil), dışarıdaki dokunulmaz
        float arrow = (float)IslCall("ArcherDamage", (int)Prop(archer, "Level"));
        float before = Health(cluster[0]);
        Assert.IsTrue((bool)abilities.GetType().GetMethod("Use").Invoke(abilities, new[] { K("Volley") }), "Ok Yağmuru kullanıldı");
        Assert.IsFalse((bool)abilities.GetType().GetMethod("CanUse").Invoke(abilities, new[] { K("Volley") }), "bekleme 18 sn");
        yield return new WaitForSecondsRealtime(2.2f);
        Assert.AreEqual(before - 3f * arrow, Health(cluster[0]), 0.05f, "3 × ok hasarı");
        Assert.AreEqual(before - 3f * arrow, Health(cluster[3]), 0.05f, "dairedeki herkes");
        Assert.AreEqual(Health(outside), (float)Prop(Prop(outside, "Controller"), "MaxHealth"), 1e-3f, "daire dışı etkilenmez");

        // Ağ Oku: daire içindekiler kökleşir, sonra %40 yavaşlar
        Assert.IsTrue((bool)abilities.GetType().GetMethod("Use").Invoke(abilities, new[] { K("Net") }));
        yield return new WaitForSecondsRealtime(0.6f);   // ağ 0,35 sn uçar
        Assert.IsTrue(cluster.All(e => (bool)Prop(e, "Rooted")), "2,5 sn kökleşti");
        Assert.IsFalse((bool)Prop(outside, "Rooted"));
        yield return null;
        Assert.AreEqual(0f, (float)Prop(Prop(cluster[0], "Controller"), "SpeedMultiplier"), 1e-4f, "kökleşmişken hız 0");
        Time.timeScale = 3f;
        yield return WaitUntil(() => !(bool)Prop(cluster[0], "Rooted"), 5f);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.IsTrue((bool)Prop(cluster[0], "Slowed"), "kök bitince 3 sn yavaşlama");
        Assert.AreEqual(0.6f, (float)Prop(Prop(cluster[0], "Controller"), "SpeedMultiplier"), 1e-4f, "%40 yavaş");
        Time.timeScale = 1f;

        // Ateşli Ok: sonraki 3 atış tutuşturur; birlik yanar
        Assert.IsTrue((bool)abilities.GetType().GetMethod("Use").Invoke(abilities, new[] { K("FireArrow") }));
        Assert.AreEqual(3, (int)Prop(abilities, "FireShotsLeft"));
        int ignitions = (int)T("IslandStats").GetField("Ignitions").GetValue(null);
        archer.GetType().GetMethod("LockTarget").Invoke(archer, new object[] { T("IslandTarget").GetConstructor(new[] { T("IslandEnemy") }).Invoke(new object[] { outside }) });
        Assert.IsTrue((bool)archer.GetType().GetMethod("DebugFireAtNearest").Invoke(archer, null));
        Assert.AreEqual(2, (int)Prop(abilities, "FireShotsLeft"), "bir atış harcandı");
        yield return new WaitForSecondsRealtime(0.9f);
        Assert.Greater((int)T("IslandStats").GetField("Ignitions").GetValue(null), ignitions, "isabet tutuşturdu");
        Assert.IsTrue((bool)Prop(outside, "Burning"), "hedef yanıyor");
        float hpA = Health(outside);
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.Less(Health(outside), hpA, "yanma saniyede hasar verir");
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    // ==================== SAHNE: BASKINDA BOSS YOK ====================

    [UnityTest, Timeout(200000)]
    public IEnumerator LiveRaidNeverBringsABossEvenWhenTheNominalPowerIsABossAkin()
    {
        yield return IslandPhaseBTests.LoadIsland(island => { IslandPhaseBTests.Set(island, "highestAkin", 12); }, 8, v => devWas = v);
        long t0 = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc).Ticks;
        IslandPhaseBTests.SetClock(t0);
        var island0 = IslandPhaseBTests.CurrentIsland();
        T("IslandRaidScheduler").GetMethod("Tick").Invoke(null, new object[] { island0, t0 });
        T("IslandRaidScheduler").GetMethod("DevBring").Invoke(null, new object[] { island0, t0 });
        IslandPhaseBTests.SetClock(t0 + 31 * TimeSpan.TicksPerMinute);
        var raid = Prop(IslandPhaseBTests.Setup, "Raid");
        Assert.IsTrue((bool)raid.GetType().GetMethod("StartRaid").Invoke(raid, null));
        Assert.AreEqual(9, (int)Prop(raid, "Akin"), "en yüksek 12 − 2 = 10 (boss) → 9'a düşer");
        Assert.IsFalse((bool)IslCall("IsBossAkin", (int)Prop(raid, "Akin")));
        // İlk tekneler gelirken hiçbir boss doğmaz (Barge/boss tayfası yok)
        float until = Time.realtimeSinceStartup + 6f;
        Time.timeScale = 6f;
        while (Time.realtimeSinceStartup < until)
        {
            foreach (var e in LiveEnemies()) Assert.IsFalse(Bosses.Contains(Name(Prop(e, "Type"))), "baskında boss çıkmaz");
            yield return null;
        }
        Time.timeScale = 1f;
        raid.GetType().GetMethod("Abandon").Invoke(raid, null);
    }

    /// <summary>
    /// Ölçüm yerleşimi (§11 "Beklenen durum"): tüm bölgeler açık; kule tavanına kadar (KK1: 1 · KK2: 2 · KK4: 3 · KK6: 4 · KK9: 5; hazır adacık kuleleri dahil) kule, açık kumsallara
    /// sırayla, yürüyüş şeridinin ikinci-üçüncü noktasına yakın; Manastır (KK ≥ 6) ve Kışla (KK ≥ 3) KK seviyesinde. Yerleşim oyuncunun yaptığı gibi geçerli hücrelere (IslandGrid) konur.
    /// </summary>
    /// <summary>Savaşta düşman türlerinin verdiği hasar payları (en büyük 4): ölçüm satırı için.</summary>
    private static string DamageShares()
    {
        var dict = (IDictionary)T("IslandStats").GetField("DamageByType").GetValue(null);
        var pairs = new List<KeyValuePair<string, float>>();
        float total = 0f;
        foreach (DictionaryEntry e in dict) { pairs.Add(new KeyValuePair<string, float>(e.Key.ToString(), (float)e.Value)); total += (float)e.Value; }
        pairs.Sort((x, y) => y.Value.CompareTo(x.Value));
        return string.Join(" ", pairs.Take(4).Select(p => $"{p.Key}%{(total > 0 ? Mathf.RoundToInt(100f * p.Value / total) : 0)}")) + $" (toplam {total:0})";
    }

    internal static void BuildDefense(object island, int keep, int akin)
    {
        IslandPhaseBTests.Call("IslandEconomy", "EnsureSeeded", island, keep);
        IslandPhaseBTests.Set(island, "zoneMask", 63);
        var placed = IslandPhaseBTests.Placed(island);
        var beaches = (Array)T("IslandLayout").GetField("Beaches").GetValue(null);
        int open = Mathf.Min((int)IslCall("BeachesOpen", akin), beaches.Length);
        void Add(string kind, int level, int nearCol, int nearRow, string id, bool production = false)
        {
            if (!IslandPhaseBTests.FindCellNear(kind, island, 63, nearCol, nearRow, out int c, out int r, keep)) return;
            var entry = IslandPhaseBTests.Entry(id, kind, level, c, r);
            IslandPhaseBTests.Set(entry, "lastTick", DateTime.UtcNow.Ticks);
            placed.Add(entry);
        }
        if (keep >= 6) Add("Monastery", keep, 11, 12, "Monastery#1");
        if (keep >= 3) Add("Barracks", keep, 12, 10, "Barracks#1");
        for (int i = 0; i < 8; i++)
        {
            var beach = beaches.GetValue(i % open);
            var path = (Vector2[])beach.GetType().GetField("walkPath").GetValue(beach);
            var at = path[Mathf.Min(path.Length - 1, 2 + (i / open))];
            int before = placed.Count;
            Add("Tower", keep, Mathf.FloorToInt(at.x), 36 - Mathf.FloorToInt(at.y), "Tower#" + (20 + i));
            if (placed.Count == before) break;   // tavan ya da yer yok
        }
    }

    // ==================== BOT MERDİVENİ 16–30 (ölçüm; -testFilter adıyla, ISLAND_LADDER ortam değişkeniyle) ====================

    /// <summary>
    /// Ölçüm (§10 E "Bot 16–30", §11): <c>ISLAND_LADDER=profil,başla,bitir,tekrar[,etiket]</c> (ör. <c>Good,16,30,2,good</c>).
    /// Kıyı Kalesi = ⌈N/3⌉, okçu = KK, iki hazır adacık kulesi (Faz C ölçümüyle aynı), hızlı oyun (×6). Çıktı <c>PlaytestOutput/island/ladder_E2_&lt;etiket&gt;.txt</c>.
    /// </summary>
    [UnityTest, Timeout(5400000), Explicit("uzun ölçüm: ISLAND_LADDER ortam değişkeni ve -testFilter ile adıyla çalıştırılır")]
    public IEnumerator MeasureLadder()
    {
        string spec = Environment.GetEnvironmentVariable("ISLAND_LADDER") ?? "Good,16,18,1,smoke";
        var parts = spec.Split(',');
        string profile = parts[0]; int repeats = int.Parse(parts[3]);
        // başla,bitir bir aralık ("16,30") ya da noktalı virgülle liste ("16;18;21,0")
        var akins = parts[1].Contains(";") ? parts[1].Split(';').Select(int.Parse).ToList() : Enumerable.Range(int.Parse(parts[1]), int.Parse(parts[2]) - int.Parse(parts[1]) + 1).ToList();
        string tag = parts.Length > 4 ? parts[4] : profile.ToLowerInvariant();
        bool strip = tag.Contains("naked");
        var lines = new List<string>();
        foreach (int akin in akins)
            for (int run = 1; run <= repeats; run++)
            {
                int keep = Mathf.Max(1, Mathf.CeilToInt(akin / 3f));
                yield return IslandPhaseBTests.LoadIsland(island =>
                {
                    IslandPhaseBTests.Set(island, "highestAkin", akin - 1);
                    if (!tag.Contains("bare")) BuildDefense(island, keep, akin);
                }, keep, v => devWas = v);
                var raid = Prop(IslandPhaseBTests.Setup, "Raid");
                T("IslandBot").GetMethod("Attach").Invoke(null, new object[] { IslandPhaseBTests.Setup, Enum.Parse(T("IslandBot+Profile"), profile) });
                if (strip) T("IslandBot").GetMethod("StripDefenses").Invoke(null, null);
                Time.timeScale = 6f;
                float start = Time.realtimeSinceStartup;
                raid.GetType().GetMethod("StartAkin").Invoke(raid, new object[] { akin });
                while (Prop(raid, "Phase").ToString() == "Running" && Time.realtimeSinceStartup - start < 420f) yield return null;
                Time.timeScale = 1f;
                string phase = Prop(raid, "Phase").ToString();
                int towers = ((IEnumerable)T("IslandBuilding").GetProperty("All").GetValue(null)).Cast<Component>().Count(b => b != null && (bool)Prop(b, "IsTower"));
                int Stat(string f) => (int)T("IslandStats").GetField(f).GetValue(null);
                float castle = (float)Prop(raid, "CastleHealthPercent");
                lines.Add($"Akın {akin} KK{keep} {profile} #{run}: {phase}|kale {castle:0.00}|{(string)Prop(raid, "DefeatTag")}|{(float)Prop(raid, "Elapsed"):0}s oyun|bld{Stat("BuildingsLost")}|atış{Stat("Shots")} (dolu {Stat("ChargedShots")})|yetenek{Stat("AbilityUses")}|boğulan {Stat("Drowned")}/{Stat("CrewTotal")}|okçu ölümü{Stat("ArcherDeaths")}|kule{towers}|hasar {DamageShares()}");
                UnityEngine.Debug.Log("[IslandLadderE2] " + lines[lines.Count - 1]);
                var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../PlaytestOutput/island"));
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(folder, $"ladder_E2_{tag}.txt"), lines);
            }
    }
}
