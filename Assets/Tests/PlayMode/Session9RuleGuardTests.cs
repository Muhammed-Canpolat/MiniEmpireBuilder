using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 9. oturum: kök nedenlerin yeniden açılmasını yakalayan koruma testleri (TECH_AUDIT K-1, K-6, KN-1a, KN-4a).
/// Kaynak taraması yapan testler yalnız editörde anlamlıdır (PlayMode testleri editörde koşar).
/// </summary>
public class Session9RuleGuardTests : Session9SaveFixture
{
    private static string ScriptsRoot => Path.Combine(Application.dataPath, "Scripts");

    /// <summary>Yorum olmayan satırlarda deseni arar; izinli dosyalar hariç. Dönüş: "dosya:satır: metin".</summary>
    private static string[] Scan(string pattern, params string[] allowedFiles)
    {
        var regex = new Regex(pattern);
        return Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !allowedFiles.Contains(Path.GetFileName(path)))
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => (line, index))
                .Where(x => !x.line.TrimStart().StartsWith("//") && regex.IsMatch(x.line))
                .Select(x => Path.GetFileName(path) + ":" + (x.index + 1) + ": " + x.line.Trim()))
            .ToArray();
    }

    private static Component Manager()
    {
        var managerType = T("GameManager");
        var manager = managerType.GetProperty("Instance").GetValue(null) as Component;
        return manager != null ? manager : new GameObject("S9GameManager").AddComponent(managerType);
    }

    private static FieldInfo Fault => T("SaveSystem").GetField("MigrationFaultForTests",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

    [Test]
    public void K6_GameRulesReadTheSingleGameClock()
    {
        // Ham cihaz saati yalnız şuralarda: saatin kendisi, kayıt damgası/göç, bildirim planı (bilinçli: gerçek saat),
        // geliştirici araçları ve görsel gün-gece tonu. Oyun kuralı (süre, günlük sınır, üyelik) GameClock okur.
        var hits = Scan(@"DateTime\.(UtcNow|Now|Today)",
            "GameClock.cs", "SaveSystem.cs", "PlayerSaveData.cs", "GameNotifications.cs", "DevMenu.cs",
            "DecorPlacementController.cs", "PlaytestBot.cs", "PlaytestJourney.cs");
        Assert.IsEmpty(hits, "GameClock yerine ham saat okunuyor:\n" + string.Join("\n", hits));
    }

    [Test]
    public void KN4_DevMasterSwitchDoesNotChangeGameRules()
    {
        // Ana anahtar yalnız araç açar. Kural atlayan her kolaylık DevSettings içinde ayrı, varsayılan KAPALI bir ayar olmalı.
        var hits = Scan(@"DevSettings\.Enabled", "DevSettings.cs", "DevMenu.cs", "PlaytestJourney.cs");
        Assert.IsEmpty(hits, "DevSettings.Enabled oyun kodunda kullanılıyor:\n" + string.Join("\n", hits));
    }

    [UnityTest]
    public IEnumerator K6_RolledBackDeviceClockDoesNotExtendTimers()
    {
        var manager = Manager();
        yield return null;
        var save = NewSave();
        manager.GetType().GetMethod("DevSetPlayerData").Invoke(manager, new[] { save });
        // Oyun yarını görmüş, cihaz saati bugüne geri alınmış.
        long seen = DateTime.UtcNow.AddDays(1).Ticks;
        Set(save, "clockHighWaterTicks", seen);
        Assert.AreEqual(seen, (long)Static("GemEconomy", "Now"), "geri sayımlar oyunun saatini kullanır");
        // Kraliyet Üyeliği oyunun saatine göre 12 saat önce bitti; cihaz saatine göre hâlâ 12 saat var görünürdü.
        Set(save, "royalUntilTicks", DateTime.UtcNow.AddHours(12).Ticks);
        Assert.IsFalse((bool)Static("BattleCards", "IsRoyal"), "saat geri alınarak üyelik uzatılamaz");
        manager.GetType().GetMethod("DevSetPlayerData").Invoke(manager, new[] { NewSave() });
    }

    [UnityTest]
    public IEnumerator KN1_LocksAreDerivedFromTheRuleOnLoad()
    {
        // Ana Üs 9'a çıkmış ama kilit bayrakları eski kalmış kayıt (ör. bir güncellemede açılış seviyesi düşürüldü).
        var save = NewSave();
        var placed = (IList)Get(save, "placedBuildings");
        var main = Activator.CreateInstance(T("PlacedBuildingData"));
        Set(main, "slotId", "main_base");
        Set(main, "buildingType", "MainBase");
        Set(main, "level", 9);
        placed.Add(main);
        foreach (var entry in (IList)Get(save, "buildings"))
        {
            string type = Get(entry, "buildingType").ToString();
            if (type == "MainBase" || type == "GoldMine") continue;
            Set(entry, "isUnlocked", false);
            Set(entry, "level", 0);
        }
        WriteAllCopies(Json(save));

        var manager = Manager();
        var managerType = manager.GetType();
        yield return null;
        managerType.GetMethod("LoadGame").Invoke(manager, null);
        Assert.AreEqual(9, managerType.GetMethod("GetMainBaseLevel").Invoke(manager, null));
        var buildings = (IDictionary)managerType.GetProperty("Buildings").GetValue(manager);
        var firstUnlock = T("Progression").GetMethod("FirstUnlockLevel");
        foreach (DictionaryEntry pair in buildings)
        {
            int required = (int)firstUnlock.Invoke(null, new[] { pair.Key });
            bool unlocked = (bool)pair.Value.GetType().GetField("isUnlocked").GetValue(pair.Value);
            Assert.AreEqual(9 >= required, unlocked, pair.Key + " (açılış seviyesi " + required + ")");
        }
        managerType.GetMethod("DevSetPlayerData").Invoke(manager, new[] { NewSave() });   // test verisi oyunda kalmasın
    }

    [UnityTest]
    public IEnumerator KN1d_BaseSceneSaveKeepsBuildingsItCouldNotLoad()
    {
        // Bu sürümün tanımadığı türde bir bina (daha yeni sürümün kaydı): sahneye kurulamaz; SaveAll listeyi sahneden baştan
        // yazarken kayıttan düşürmemeli (eskiden sessizce siliniyordu).
        var save = NewSave();
        var placed = (IList)Get(save, "placedBuildings");
        placed.Clear();
        var future = Activator.CreateInstance(T("PlacedBuildingData"));
        Set(future, "slotId", "future_1");
        Set(future, "buildingType", "FutureTower");
        Set(future, "level", 3);
        placed.Add(future);

        var manager = Manager();
        var managerType = manager.GetType();
        yield return null;
        managerType.GetMethod("DevSetPlayerData").Invoke(manager, new[] { save });
        var mapObject = new GameObject("S9BaseMapManager");
        try
        {
            var map = mapObject.AddComponent(T("BaseMapManager"));
            yield return null;
            map.GetType().GetMethod("LoadFromPlayerData").Invoke(map, null);
            var after = (IList)Get(managerType.GetProperty("PlayerData").GetValue(manager), "placedBuildings");
            Assert.AreEqual(1, after.Count, "tanınmayan bina kayıtta kalmalı");
            Assert.AreEqual("FutureTower", Get(after[0], "buildingType"));
            Assert.AreEqual(3, Get(after[0], "level"));
        }
        finally
        {
            UnityEngine.Object.Destroy(mapObject);
            managerType.GetMethod("DevSetPlayerData").Invoke(manager, new[] { NewSave() });
        }
    }

    private static void Boom() => throw new InvalidOperationException("test: göç hatası");

    [UnityTest]
    public IEnumerator K1_GameManagerDoesNotStartANewGameOverAnUnreadableSave()
    {
        string original = AsVersion(Json(PlayedSave()), 9);
        WriteAllCopies(original);
        Fault.SetValue(null, Delegate.CreateDelegate(typeof(Action),
            typeof(Session9RuleGuardTests).GetMethod(nameof(Boom), BindingFlags.NonPublic | BindingFlags.Static)));
        var manager = Manager();
        var managerType = manager.GetType();
        yield return null;
        LogAssert.ignoreFailingMessages = true;   // açılamayan kayıt bilinçli olarak hata günlüğü yazar
        try
        {
            managerType.GetMethod("LoadGame").Invoke(manager, null);
            Assert.IsNull(managerType.GetProperty("PlayerData").GetValue(manager));
            Assert.IsTrue((bool)managerType.GetProperty("SaveLoadFailed").GetValue(manager));
            // Sahnelerin "veri yok → yeni oyun" kısayolu (BattleSceneSetup / IslandSceneSetup) bunu çağırır.
            managerType.GetMethod("StartNewGame").Invoke(manager, new[] { Enum.Parse(T("WeaponType"), "Axe") });
            yield return null;
            Assert.IsNull(managerType.GetProperty("PlayerData").GetValue(manager), "yeni oyun başlamamalı");
            Assert.AreEqual(original, PlayerPrefs.GetString(Key), "asıl kopya");
            Assert.AreEqual(original, File.ReadAllText(FilePath), "dosya kopyası");
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Fault.SetValue(null, null);
            // Koruma ana menüye dönüş geçişi başlatır: sonraki testlerin sahnesini değiştirmesin.
            ((MonoBehaviour)manager).StopAllCoroutines();
            var fade = GameObject.Find("SceneFade");
            if (fade != null) UnityEngine.Object.Destroy(fade);
            Wipe();
            managerType.GetMethod("DevSetPlayerData").Invoke(manager, new[] { NewSave() });
        }
    }
}
