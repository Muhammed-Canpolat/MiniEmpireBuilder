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
using static IslandPhaseBTests;
using Object = UnityEngine.Object;

/// <summary>
/// AŞAMA 3 (arayüz): NumberFormat, ada adı kuralları + mühür + bedel, ortak üst bar (Ana Üs + Ada, kilit durumu), ada kompakt dock,
/// baskın hapı saat ikonu, Kart Atölyesi başlık/ikon düzeni, inşa menüsü kartı düzeltmeleri (§3G). Yalıtılmış test kaydı.
/// Ekran görüntüleri <c>PlaytestOutput/ui_night/N3_*.png</c>.
/// </summary>
public class UiNightTests
{
    private const string Key = "MiniEmpireBuilder_SaveData";
    private const string BackupKey = "MiniEmpireBuilder_SaveData_Backup";
    private static string SaveFile => Path.Combine(Application.persistentDataPath, "save.json");
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
        T("DevSettings").GetProperty("Enabled").SetValue(null, devWas);
        T("IslandNameUi").GetField("OfferInDev").SetValue(null, false);
        Call("IslandNameUi", "Close");
        T("IslandSession").GetMethod("Clear").Invoke(null, null);
        T("IslandRepair").GetField("Clock").SetValue(null, (Func<long>)(() => DateTime.UtcNow.Ticks));
        Time.timeScale = 1f;
    }

    [OneTimeTearDown]
    public void DropTestGameManager()
    {
        var manager = T("GameManager").GetProperty("Instance").GetValue(null) as Component;
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        PlayerPrefs.Save();
    }

    // ==================== SAF MANTIK ====================

    [Test]
    public void NumberFormatShortensLargeNumbers()
    {
        string S(long v) => (string)Call("NumberFormat", "Short", v, 10000L);
        Assert.AreEqual("0", S(0));
        Assert.AreEqual("6345", S(6345));
        Assert.AreEqual("9999", S(9999));
        Assert.AreEqual("10K", S(10000), "'.0' atılır");
        Assert.AreEqual("12.4K", S(12400));
        Assert.AreEqual("12.9K", S(12999), "aşağı yuvarlanır");
        Assert.AreEqual("999.9K", S(999999));
        Assert.AreEqual("1M", S(1000000));
        Assert.AreEqual("1.2M", S(1200000));
        Assert.AreEqual("-12.4K", S(-12400));
    }

    private static (bool ok, string clean, string error) Validate(string raw)
    {
        var args = new object[] { raw, null, null };
        bool ok = (bool)T("IslandNameRules").GetMethod("Validate").Invoke(null, args);
        return (ok, (string)args[1], (string)args[2]);
    }

    [Test]
    public void IslandNameRulesAcceptTurkishAndRejectBadNames()
    {
        foreach (var good in new[] { "Martı Adası", "Çiğdem Ada", "İstanbul", "ŞÜKRÜ Çağlar", "Ada-1", "O'Neil", "Klasik Ada", "Kamkale", "Abc", "1234567890123456" })
        {
            var r = Validate(good);
            Assert.IsTrue(r.ok, $"'{good}' geçerli olmalı: {r.error}");
        }
        var spaced = Validate("   Gül    Ada  ");
        Assert.IsTrue(spaced.ok);
        Assert.AreEqual("Gül Ada", spaced.clean, "kırp + boşlukları sıkıştır");
        Assert.IsFalse(Validate("").ok);
        Assert.IsFalse(Validate("    ").ok, "yalnız boşluk");
        Assert.IsFalse(Validate("ab").ok, "3 karakterden kısa");
        Assert.IsFalse(Validate("12345678901234567").ok, "16 karakterden uzun");
        Assert.IsFalse(Validate("Ada@#!").ok, "özel karakter");
        Assert.IsFalse(Validate("Ada😀").ok, "emoji");
        foreach (var bad in new[] { "orospu ada", "S1KT1R", "Fuck Isle", "Sİktir", "g ö t v e r e n", "y4rr4k", "amk", "Shit Ada" })
            Assert.IsFalse(Validate(bad).ok, $"'{bad}' küfür filtresine takılmalı");
        Assert.IsTrue(Validate("Sikke Adası").ok, "kısa kelimeler yalnız tam sözcük olarak engellenir");
        Assert.IsTrue(Validate("Grape Isle").ok);
        Assert.IsTrue(Validate("Nazire Ada").ok);
        Assert.AreEqual(3, (int)Field(Config, "islandNameMin"));
        Assert.AreEqual(16, (int)Field(Config, "islandNameMax"));
    }

    private static object Config => T("FeatureConfig").GetProperty("Instance").GetValue(null);

    [Test]
    public void RenameCostSequenceIsFreeTwiceThenFiftyPerStep()
    {
        int C(int n) => (int)Call("GemEconomy", "IslandRenameCost", n);
        CollectionAssert.AreEqual(new[] { 0, 0, 50, 100, 150, 200 }, Enumerable.Range(0, 6).Select(C).ToArray());
    }

    [Test]
    public void NameApplyCountsChangesAndTamperedSealLosesFreeRenames()
    {
        var rules = "IslandNameRules";
        var save = Activator.CreateInstance(T("IslandSaveData"));
        Assert.AreEqual(0, (int)Call(rules, "NextCost", save), "yeni oyuncunun ilk adı bedava");
        Assert.AreEqual(L("Martı Adası", "Gull Isle"), (string)Call(rules, "Display", save), "boş ad = varsayılan");
        Call(rules, "Apply", save, "Kuzey Ada");
        Assert.AreEqual(1, (int)Field(save, "nameChanges"), "ilk adlandırma 1. değişiklik");
        Assert.IsTrue((bool)Field(save, "nameOffered"));
        Assert.AreEqual(0, (int)Call(rules, "NextCost", save), "2. değişiklik de bedava");
        Call(rules, "Apply", save, "Güney Ada");
        Assert.AreEqual(50, (int)Call(rules, "NextCost", save), "3. değişiklik 50 elmas");
        Assert.AreEqual("Güney Ada", (string)Call(rules, "Display", save));
        Assert.IsTrue((bool)Call(rules, "SealValid", save));

        // Elle düzenleme: sayaç sıfırlanır ama mühür tutmaz → ücretsiz hak yok, çökme yok
        var hacked = Activator.CreateInstance(T("IslandSaveData"));
        Set(hacked, "islandName", "Hileli Ada"); Set(hacked, "nameChanges", 0); Set(hacked, "nameSeal", 12345);
        Assert.IsFalse((bool)Call(rules, "SealValid", hacked));
        Assert.GreaterOrEqual((int)Call(rules, "EffectiveChanges", hacked), 2);
        Assert.AreEqual(50, (int)Call(rules, "NextCost", hacked), "mühür bozuk: bedava değişiklik kalmaz");
        Set(hacked, "nameChanges", -7);
        Assert.DoesNotThrow(() => Call(rules, "NextCost", hacked));
        Assert.DoesNotThrow(() => Call(rules, "NextCost", (object)null));
    }

    [Test]
    public void SaveVersionIs11AndNameFieldsSerializeBeforeVillageSeeded()
    {
        var save = Activator.CreateInstance(T("IslandSaveData"));
        Call("IslandNameRules", "Apply", save, "Deneme Ada");
        string json = JsonUtility.ToJson(save);
        StringAssert.Contains("\"islandName\":\"Deneme Ada\"", json);
        Assert.Less(json.IndexOf("\"islandName\"", StringComparison.Ordinal), json.IndexOf("\"villageSeeded\"", StringComparison.Ordinal),
            "ad alanları villageSeeded…task bloğunun dışında (eski sürüm testleri regex'le keser)");
        var back = Activator.CreateInstance(T("IslandSaveData"));
        JsonUtility.FromJsonOverwrite(json, back);
        Assert.AreEqual("Deneme Ada", Field(back, "islandName"));
        Assert.IsTrue((bool)Call("IslandNameRules", "SealValid", back));
        var versionField = T("PlayerSaveData").GetField("CurrentVersion", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        Assert.AreEqual(11, (int)versionField.GetRawConstantValue());
    }


    private static string L(string tr, string en) => (string)T("L").GetMethod("T").Invoke(null, new object[] { tr, en });

    // ==================== ANA ÜS ÜST BARI ====================

    private static IEnumerator LoadBase(int mainBase, int meat = 0, int wood = 0, int gold = 6345, int gems = 111)
    {
        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", mainBase);
        Set(save, "gold", gold);
        Set(save, "gems", gems);
        Set(save, "firstBaseGuideDone", true);
        var island = Field(save, "island");
        Set(island, "meat", meat); Set(island, "wood", wood);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 45; i++) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private static Transform Hud => GameObject.Find("BaseCanvas").transform.Find("SafeArea/HudRoot");

    private static Rect ScreenRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    private static string TextOf(Component c)
    {
        var tmp = c.GetComponent("TextMeshProUGUI");
        return (string)tmp.GetType().GetProperty("text").GetValue(tmp);
    }

    [UnityTest]
    public IEnumerator BaseTopBarHasFourChipsAndLocksIslandResourcesBelowLevel5()
    {
        yield return LoadBase(mainBase: 3, meat: 500, wood: 400);
        var hud = Hud;
        foreach (var name in new[] { "GoldPill", "GemPill", "BuilderPill", "LevelCrest", "MeatPill", "WoodPill" })
            Assert.NotNull(hud.Find(name) ?? hud.Find("ResourceTopBar/" + name), name + " Ana Üs barında");
        var topBg = hud.Find("TopBarBG");
        Assert.NotNull(topBg, "TopBarBG adı korunur");
        var meat = (RectTransform)hud.Find("ResourceTopBar/MeatPill");
        var wood = (RectTransform)hud.Find("ResourceTopBar/WoodPill");
        Assert.AreEqual("0", TextOf(meat.Find("Value")), "kilitli: 0");
        Assert.IsTrue(meat.Find("Lock").gameObject.activeSelf && wood.Find("Lock").gameObject.activeSelf, "kilit simgesi");
        var bar = hud.Find("ResourceTopBar").GetComponent("ResourceTopBar");
        Assert.IsFalse((bool)bar.GetType().GetProperty("ToastVisible").GetValue(bar));
        meat.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.IsTrue((bool)bar.GetType().GetProperty("ToastVisible").GetValue(bar), "dokununca bilgi satırı");
        StringAssert.Contains(L("Ana Üs Sv.5", "Main Base Lv.5"), TextOf(hud.Find("ResourceTopBar/ResourceToast/Text")));
        AssertNoOverlap(hud);
        yield return CaptureBase("N3_base_topbar_locked");
    }

    [UnityTest]
    public IEnumerator BaseTopBarShowsAbbreviatedIslandResourcesWhenUnlocked()
    {
        yield return LoadBase(mainBase: 7, meat: 12400, wood: 60, gold: 1234567, gems: 12000);
        var hud = Hud;
        var meat = (RectTransform)hud.Find("ResourceTopBar/MeatPill");
        var wood = (RectTransform)hud.Find("ResourceTopBar/WoodPill");
        Assert.AreEqual("12.4K", TextOf(meat.Find("Value")));
        Assert.AreEqual("60", TextOf(wood.Find("Value")));
        Assert.IsFalse(meat.Find("Lock").gameObject.activeSelf, "Sv.5+ kilit yok");
        Assert.AreEqual("1.2M", TextOf(hud.Find("GoldPill/GoldText")), "altın kısaltılır");
        Assert.AreEqual("12K", TextOf(hud.Find("GemPill/GemText")), "elmas kısaltılır");
        AssertNoOverlap(hud);
        yield return CaptureBase("N3_base_topbar_unlocked");
    }

    private static void AssertNoOverlap(Transform hud)
    {
        var names = new[] { "GoldPill", "GemPill", "BuilderPill", "LevelCrest", "ResourceTopBar/MeatPill", "ResourceTopBar/WoodPill" };
        var rects = names.Select(n => (n, rect: ScreenRect((RectTransform)hud.Find(n)))).ToList();
        var gear = GameObject.Find("AudioSettingsButton");
        if (gear != null) rects.Add(("gear", ScreenRect((RectTransform)gear.transform)));
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
                Assert.IsFalse(rects[i].rect.Overlaps(rects[j].rect), $"{rects[i].n} ile {rects[j].n} üst barda çakışıyor: {rects[i].rect} / {rects[j].rect}");
    }

    // ==================== ADA ====================

    private static object Hud_ => Prop(Setup, "Hud");

    private static Transform IslandSafe => GameObject.Find("IslandCanvas").transform.Find("SafeArea");

    [UnityTest]
    public IEnumerator IslandTopBarCompactDockRaidPillAndNaming()
    {
        T("IslandNameUi").GetField("OfferInDev").SetValue(null, true);
        yield return LoadIsland(island =>
        {
            Set(island, "wood", 317); Set(island, "meat", 12400);
            Set(island, "nextRaidTicks", DateTime.UtcNow.AddHours(8).AddMinutes(54).Ticks);
            Set(island, "raidSeenTicks", DateTime.UtcNow.Ticks);
        }, 5, v => devWas = v);
        var safe = IslandSafe;
        var bar = safe.Find("TopBar");
        Assert.NotNull(bar, "TopBar adı korunur");
        foreach (var name in new[] { "HomeButton", "IslandNameButton", "GoldPill", "GemPill", "MeatPill", "WoodPill" })
            Assert.NotNull(bar.Find(name), name);
        Assert.AreEqual("12.4K", TextOf(bar.Find("MeatPill/Value")));
        Assert.AreEqual("317", TextOf(bar.Find("WoodPill/Value")));
        Assert.IsFalse(bar.Find("MeatPill/Lock").gameObject.activeSelf, "adada çip kilidi yok");
        Assert.NotNull(bar.Find("Ornament"), "Ana Üs'le aynı süslü çerçeve");
        var chipRects = new[] { "GoldPill", "GemPill", "MeatPill", "WoodPill" }.Select(n => ScreenRect((RectTransform)bar.Find(n))).ToList();
        for (int i = 0; i < chipRects.Count; i++) for (int j = i + 1; j < chipRects.Count; j++) Assert.IsFalse(chipRects[i].Overlaps(chipRects[j]), "çipler çakışmıyor");
        var home = ScreenRect((RectTransform)bar.Find("HomeButton")); var nameRect = ScreenRect((RectTransform)bar.Find("IslandNameButton"));
        Assert.IsFalse(home.Overlaps(nameRect)); Assert.IsFalse(nameRect.Overlaps(chipRects[0]));
        var gear = GameObject.Find("AudioSettingsButton");
        if (gear != null) foreach (var rect in chipRects) Assert.IsFalse(rect.Overlaps(ScreenRect((RectTransform)gear.transform)), "çip dişliye binmiyor");

        // Eski kayıt (freshStart yok): "Martı Adası" teklifi 1,2 sn sonra çıkar; mod Offer
        Assert.AreEqual(L("Martı Adası", "Gull Isle"), TextOf(bar.Find("IslandNameButton/Title")), "varsayılan ad tek satırda");
        float until = Time.realtimeSinceStartup + 4f;
        while (!(bool)T("IslandNameUi").GetProperty("IsOpen").GetValue(null) && Time.realtimeSinceStartup < until) yield return null;
        Assert.IsTrue((bool)T("IslandNameUi").GetProperty("IsOpen").GetValue(null), "ada adı teklifi açılır");
        var island0 = CurrentIsland();
        Assert.IsTrue((bool)Field(island0, "nameOffered"), "teklif bir kez işaretlenir");
        Assert.NotNull(GameObject.Find("OfferGroup"), "eski kayıt: DEĞİŞTİR / BÖYLE KALSIN");
        yield return null;
        yield return Capture("N3_island_naming_offer_old_save");

        // DEĞİŞTİR → giriş alanı; geçersiz ad hata verir, geçerli ad yazılır (ilk değişiklik bedava)
        var ui = T("IslandNameUi").GetProperty("Current").GetValue(null);
        GameObject.Find("ChangeButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        ui.GetType().GetProperty("FieldText").SetValue(ui, "ab");
        Assert.IsFalse((bool)ui.GetType().GetMethod("TryApply").Invoke(ui, null), "çok kısa ad reddedilir");
        Assert.IsNotEmpty((string)ui.GetType().GetProperty("ErrorText").GetValue(ui));
        ui.GetType().GetProperty("FieldText").SetValue(ui, "Çınar Adası");
        yield return null;
        yield return Capture("N3_island_naming_modal");
        Assert.IsTrue((bool)ui.GetType().GetMethod("TryApply").Invoke(ui, null), "geçerli ad kabul");
        Assert.AreEqual("Çınar Adası", Field(island0, "islandName"));
        Assert.AreEqual(1, (int)Field(island0, "nameChanges"));
        yield return null; yield return null;
        Assert.AreEqual("Çınar Adası", TextOf(bar.Find("IslandNameButton/Title")), "üst çubukta yeni ad");
        Assert.IsFalse((bool)T("IslandNameUi").GetProperty("IsOpen").GetValue(null));

        // Ada adına dokunma: yeniden adlandırma penceresi (2. değişiklik de bedava)
        bar.Find("IslandNameButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        Assert.IsTrue((bool)T("IslandNameUi").GetProperty("IsOpen").GetValue(null), "ada adına dokununca pencere");
        Call("IslandNameUi", "Close");

        // Baskın hapı: saat ikonu + "Sonraki akın: …"
        yield return new WaitForSecondsRealtime(0.7f);
        var pill = safe.Find("RaidBadge");
        Assert.NotNull(pill.Find("ClockIcon"), "saat ikonu");
        Assert.IsTrue(pill.gameObject.activeSelf, "baskın planlıyken hap görünür");
        StringAssert.StartsWith(L("Sonraki akın", "Next raid"), TextOf(pill.Find("Label")));

        // Kompakt dock
        var dock = safe.Find("Dock");
        var build = (RectTransform)dock.Find("BuildButton"); var akin = (RectTransform)dock.Find("AkinButton");
        Assert.LessOrEqual(build.rect.height, 60f, "düğme yüksekliği ≤ 60"); Assert.LessOrEqual(akin.rect.height, 60f);
        Assert.LessOrEqual(build.rect.width, 140f);
        Assert.NotNull(build.Find("Icon")); Assert.NotNull(akin.Find("Icon"), "ikonlu düğmeler");
        Assert.IsFalse(ScreenRect(build).Overlaps(ScreenRect(akin)));
        float top = ((RectTransform)dock).offsetMax.y / 844f;   // referans tuval yüksekliği
        Assert.LessOrEqual(top, (float)Field(Config, "islandViewBottomHudFraction") + 0.005f, "dock adanın güney ucunu örtmez");
        yield return null;
        yield return Capture("N3_island_topbar_dock_pill");
    }

    [UnityTest]
    public IEnumerator NewPlayerIsAskedForNameAndMonasteryCardShowsLockReason()
    {
        T("IslandNameUi").GetField("OfferInDev").SetValue(null, true);
        yield return LoadIsland(null, 5, v => devWas = v, fresh: true);
        float until = Time.realtimeSinceStartup + 4f;
        while (!(bool)T("IslandNameUi").GetProperty("IsOpen").GetValue(null) && Time.realtimeSinceStartup < until) yield return null;
        Assert.IsTrue((bool)T("IslandNameUi").GetProperty("IsOpen").GetValue(null), "yeni oyuncuya 'Adanızın adı ne olsun?'");
        Assert.IsFalse(GameObject.Find("OfferGroup") != null, "yeni oyuncuda teklif grubu yok: doğrudan giriş alanı");
        var ui = T("IslandNameUi").GetProperty("Current").GetValue(null);
        ui.GetType().GetProperty("FieldText").SetValue(ui, "Yeni Ada");
        Assert.IsTrue((bool)ui.GetType().GetMethod("TryApply").Invoke(ui, null));
        var island = CurrentIsland();
        Assert.AreEqual(1, (int)Field(island, "nameChanges"), "ilk adlandırma 1. değişiklik sayılır");
        Assert.AreEqual(0, (int)Call("IslandNameRules", "NextCost", island), "2. de bedava");

        // §3G: inşa menüsü kartları
        var hud = Hud_;
        hud.GetType().GetMethod("OpenBuildMenu").Invoke(hud, null);
        yield return null; yield return null;
        var menu = IslandSafe.Find("BuildMenu");
        Assert.IsTrue(menu.gameObject.activeInHierarchy);
        var cards = menu.Find("Cards");
        // kartlar: 0 Ev, 1 Oduncu, 2 Ağıl, 3 Kule, 4 Kışla, 5 Manastır, 6 Okçuluk
        var house = cards.Find("Card0"); var monastery = cards.Find("Card5");
        Assert.IsTrue(house.Find("Free").gameObject.activeSelf);
        StringAssert.Contains(L("BEDAVA", "FREE"), TextOf(house.Find("Free")), "Ev: ilk yapım bedava");
        Assert.IsTrue(monastery.Find("Free").gameObject.activeSelf);
        StringAssert.Contains(L("Kıyı Kalesi", "Keep"), TextOf(monastery.Find("Free")), "Manastır: kilit nedeni");
        StringAssert.Contains(L("6'da", "6"), TextOf(monastery.Find("Free")));
        StringAssert.DoesNotContain(L("BEDAVA", "FREE"), TextOf(monastery.Find("Free")), "kilitliyken 'bedava' yazmaz");
        Assert.IsFalse(monastery.Find("Wood").gameObject.activeSelf, "kilitli kartta bedel yok");
        // Bilgi cümlesinin 2. satırı kartın alt kenarlığına taşmaz
        for (int i = 0; i < 7; i++)
        {
            var card = (RectTransform)cards.Find("Card" + i);
            var info = card.Find("Info");
            var tmp = info.GetComponent("TextMeshProUGUI");
            tmp.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) }).Invoke(tmp, new object[] { true, true });
            var bounds = (Bounds)tmp.GetType().GetProperty("textBounds").GetValue(tmp);
            var rt = (RectTransform)info;
            Assert.GreaterOrEqual(bounds.min.y, rt.rect.yMin - 1.5f, $"Card{i} bilgi metni kutusunun altına taşıyor");
            Assert.GreaterOrEqual(rt.offsetMin.y, 8f, $"Card{i} bilgi kutusu alt kenarlıktan içeride");
        }
        yield return Capture("N3_build_menu_cards_fixed");
    }

    // ==================== KART ATÖLYESİ ====================

    [UnityTest]
    public IEnumerator CardWorkshopPanelHasSectionHeadersIconsAndFitsShortCanvas()
    {
        Assert.AreEqual(1f, (float)Call("CardWorkshopPanel", "FitFor", 390f, 844f), 0.001f);
        float fit34 = (float)Call("CardWorkshopPanel", "FitFor", 497f, 662f);
        Assert.Less(fit34, 0.9f, "3:4 kısa tuvalde panel küçülür");
        Assert.LessOrEqual(770f * fit34, 662f, "ölçekli panel tuvale sığar");

        var managerType = T("GameManager");
        var gm = managerType.GetProperty("Instance").GetValue(null) as Component;
        if (gm == null) gm = new GameObject("TestGameManager").AddComponent(managerType);
        yield return null;
        var save = NewSave();
        foreach (var entry in (System.Collections.IEnumerable)Field(save, "buildings"))
            if (Field(entry, "buildingType").ToString() == "MainBase") Set(entry, "level", 7);
        Set(save, "gold", 2400); Set(save, "gems", 30); Set(save, "firstBaseGuideDone", true); Set(save, "freeCardUsed", true);
        Set(Field(save, "island"), "meat", 2000);
        var placed = (System.Collections.IList)Field(save, "placedBuildings");
        var ws = Activator.CreateInstance(T("PlacedBuildingData"));
        Set(ws, "slotId", "workshop_1"); Set(ws, "buildingType", "CardWorkshop"); Set(ws, "level", 7);
        placed.Add(ws);
        managerType.GetProperty("PlayerData").GetSetMethod(true).Invoke(gm, new[] { save });
        managerType.GetMethod("RebuildBuildingData", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);
        Call("BattleCards", "ResetForTests");
        Call("CardWorkshop", "AddStock", Enum.Parse(T("BattleCard"), "HeroDamage"), 3);
        Set(Field(save, "workshop"), "firstFreeUsed", true);
        var args = new object[] { Enum.Parse(T("BattleCard"), "SharpVolleys"), false, null };
        Assert.IsTrue((bool)T("CardWorkshop").GetMethod("TryStartCraft").Invoke(null, args), "üretim kuyruğa girdi");
        Set(Field(save, "island"), "meat", 120);   // Et eksik ipucu görünsün
        SceneManager.LoadScene("BaseScene");
        for (int i = 0; i < 30; i++) yield return null;
        Assert.IsTrue((bool)Call("CardWorkshopPanel", "Open", "workshop_1"));
        yield return new WaitForSecondsRealtime(0.5f);
        var root = GameObject.Find("CardWorkshopPanel");
        var panel = root.transform.Find("SafeArea/Panel");
        string[] mustExist = { "Dynamic/QueueTitle", "Dynamic/SpeedUpHeader", "Dynamic/Detail/CostHeader", "Dynamic/Detail/TimeHeader",
            "Dynamic/Detail/UsesIcon", "Dynamic/Detail/MeatIcon", "Dynamic/Detail/GoldIcon", "Dynamic/Detail/ClockIcon", "Dynamic/Detail/HintGemIcon",
            "Dynamic/Queue0/Ad/Icon", "Dynamic/Queue0/Gem/GemIcon", "Dynamic/Detail/CraftAd/Icon", "Dynamic/Queue0/ClockIcon" };
        foreach (var path in mustExist) Assert.NotNull(panel.Find(path), path + " eksik");
        StringAssert.Contains(L("ÜRETİM KUYRUĞU", "PRODUCTION QUEUE"), TextOf(panel.Find("Dynamic/QueueTitle")));
        StringAssert.Contains(L("kullanım", "uses"), TextOf(panel.Find("Dynamic/Detail/Uses")), "'+N kullanım' ikonlu");
        // Detay içeriği panelin içinde kalır
        var panelRect = ScreenRect((RectTransform)panel);
        foreach (var rt in panel.Find("Dynamic/Detail").GetComponentsInChildren<RectTransform>())
        {
            var r = ScreenRect(rt);
            Assert.GreaterOrEqual(r.yMin, panelRect.yMin - 1f, rt.name + " panelin altına taşıyor");
        }
        yield return CaptureOverlay("N3_card_workshop_panel", 1080, 1920);
        yield return CaptureOverlay("N3_card_workshop_panel_3x4", 1080, 1440);
        yield return CaptureOverlay("N3F_workshop_fixed", 1080, 1920);
        Call("CardWorkshopPanel", "Close");
    }

    // ==================== EKRAN GÖRÜNTÜSÜ ====================

    private static IEnumerator CaptureBase(string name)
    {
        yield return null;
        CaptureCamera(name, Camera.main, 1080, 1920, false);
    }

    private static IEnumerator CaptureOverlay(string name, int w, int h)
    {
        yield return null;
        CaptureCamera(name, Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>(), w, h, false);
    }

    private static IEnumerator Capture(string name)
    {
        yield return null;
        var camera = (Camera)Prop(Setup, "WorldCamera");
        CaptureCamera(name, camera, 1080, 1920, true);
    }

    private static void CaptureCamera(string name, Camera camera, int w, int h, bool island)
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestOutput/ui_night"));
        Directory.CreateDirectory(folder);
        var texture = new RenderTexture(w, h, 24);
        var rect = camera.rect;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.targetTexture = texture;
        camera.aspect = (float)w / h;
        if (island)
        {
            var controller = Prop(Setup, "CameraController");
            controller.GetType().GetMethod("SetDefaultView").Invoke(controller, null);
        }
        var changed = new List<(Canvas canvas, RenderMode mode, Camera cam, int order, bool over)>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            changed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.sortingOrder, canvas.overrideSorting));
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000 + canvas.sortingOrder;
        }
        Canvas.ForceUpdateCanvases();
        // Kart Atölyesi paneli tuval boyuna göre ölçeklenir: hedef doku boyunu (CanvasScaler formülüyle) bildir
        var workshop = Object.FindFirstObjectByType(T("CardWorkshopPanel")) as Component;
        if (workshop != null) workshop.GetType().GetMethod("RefitForPixels").Invoke(workshop, new object[] { (float)w, (float)h });
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var pixels = new Texture2D(w, h, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        Object.Destroy(pixels);
        RenderTexture.active = previous;
        camera.targetTexture = null;
        camera.rect = rect;
        camera.ResetAspect();
        texture.Release();
        Object.Destroy(texture);
        foreach (var (canvas, mode, cam, order, over) in changed)
        {
            if (canvas == null) continue;
            canvas.renderMode = mode; canvas.worldCamera = cam; canvas.sortingOrder = order; canvas.overrideSorting = over;
        }
    }
}
