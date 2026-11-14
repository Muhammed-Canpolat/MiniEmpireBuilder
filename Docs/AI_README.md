# Mini Empire Builder — Yapay Zekâ Devir Notu

Yeni bir sohbette projeye hızlıca devam edebilmek için güncel bağlam. Son güncelleme: 10 Kasım 2026.
Dal: `ui/dark-fantasy-hud` (GitHub: Muhammed-Canpolat/MiniEmpireBuilder). Unity 6000.3.9f1, URP 2D, Android (dikey).

## Çalışma şekli (kullanıcı tercihleri)
- Kullanıcıyla **Türkçe** konuşulur. Önce analiz/öneri sunulur, onay gelince uygulanır.
- Her iş: derleme kontrolü → anlamlı Türkçe commit → push. Commit sonu: `Co-Authored-By: Claude ...`.
- Unity editörü genelde açıktır: PlayMode testleri komut satırından koşturulamaz; derleme
  `dotnet build Assembly-CSharp.csproj` (+ `BasePresentationTests.csproj`, `Assembly-CSharp-Editor.csproj`) ile doğrulanır.
  Yeni .cs dosyaları Unity csproj'u yenileyene kadar csproj'a elle `<Compile Include>` eklenerek denenir.
- Kullanıcı cihazda (Android tablet) APK ile test edip ekran görüntüsü paylaşır.
  APK: Unity → **Tools → Mini Empire Builder → Android Test APK (Development)** (`Assets/Editor/AndroidTestBuild.cs`).
- Python düzenleme betiklerinde `\n` kaçışları kabukta bozulabiliyor: kaçış içeren düzenlemeler Edit aracıyla
  ya da betik dosyası yazılıp çalıştırılarak yapılmalı. `GlobalUiThemeApplier.cs` UTF-8 değil (bayt düzeyinde düzenle).

## Oyun özeti
Base-builder + dalga savunması. Akış: Ana menü → **Ana Üs** (BaseScene) → **Sefer haritası** (CampaignScene, 5 bölge × 5 seviye)
→ **Savaş** (BattleScene) → bölgeye dönüş. Sahneler kodla kurulur: `SceneAutoSetup` → `*SceneSetup`.
Kayıt: `PlayerSaveData` JSON → PlayerPrefs (sürüm 4 geçişleri `SaveSystem.Load`).

## İlerleme sistemi (15 seviye)
- Tek kaynak: `Progression` (`Assets/Scripts/Data/GameData.cs`): tavan 15, renk her 3 seviyede bir
  (mavi/sarı/kırmızı/mor/siyah), bedeller. Yükseltme süreleri ve can yenilenmesi `Resources/Config/TimingConfig.asset`
  (10 sn, 1/3/6/10/15/30 dk, 1/2/3/4/6/8/12 sa; can 20 dk) — BALANCE.md.
- Açılma: Okçu 2 (2. bina 5), Topçu 4 (8), Manastır 6 (10); her türden en fazla 2. Maden 1 adet.
- **Her binanın kendi seviyesi** var (`PlacedBuildingData.level`); sahnede `BuildingInstance` bileşeni.
  `GameManager.Buildings[type].level` = o türün en yüksek bina seviyesi.
- İnşaatçı görevi gerçek saatle `GameManager`'da (`BuilderTaskSave`, UTC tick): oyun kapalıyken/savaşta ilerler.
  `BuilderSystem` yalnız görünüm; bina üstünde sayaç `BuilderTaskMarker`.
- Kahraman ve Çırak tavanı = Ana Üs seviyesi. Arazi Ana Üs 4/7/10/13'te genişler (`BaseWorldLayout.CleanAtLevel`).
- Bölge şartı 1/3/6/9/12; savaş yuvası Bölge 1–2: 3, 3–4: 4, 5 ve Sonsuz: 5 (`CampaignCatalog.BattleSlotCount`).

## Ana Üs (BaseScene)
- `BaseSceneSetup`, `BaseMapManager` (binalar, çit, slotlar), `BaseWorldUI` (bina/kahraman panelleri).
- HUD: `BaseHudRedesign` (süslü üst şerit, seviye arması `LevelCrest`, ahşap dock; İnşa Et · SEFER · KAHRAMAN).
- Çit: kesintisiz döşenen kirişler + taş direkler, kaldırılamaz (`BaseMapManager.BuildBuildableFence`).
- Dekor: `DecorData` (Resources/Decor, 17 dekor; Cainos Village Props + Tiny Swords + özel sanat),
  `DecorPlacementController`, menüde kaydırılabilir ızgara (`BuildMenuController.CreateDecorGrid`).
- Kahramana dokunma görsel gövdeye/kamp bayrağına göre (`BaseSceneSetup.IsHeroTap`).

## Savaş (BattleScene)
- `BattleManager` (dalgalar, yuvalar `SlotLayout`, yapılar `BuildTower`/`BuildMonastery`), `BattleBuildSlot`.
- Yapılar: `BattleStructure` (can, yıkılma, yıkıntı, yeniden inşa +%50/yıkılış), `TowerController`, `MonasteryHealer`
  (kahraman + Ana Üs + kuleleri iyileştirir), `BattleGarrison` (kulede asker).
- `BattleObstacles`: Ana Üs/kuleler yürünemez; kahraman/Çırak konumla hareket eder, engellerden itilir.
- Düşman rolleri (`EnemyController.Role`): Kurt/Gölge Kurt = Avcı (kahraman/Çırak), Zombi = Kuşatıcı (yakın kule),
  Ork = Yıkıcı (kuleler), Trol/boss = Kuşatma (üs). Kahraman vurduğu düşmanı 3 sn çeker (`Provoke`).
- Bölge 2 Sisli Orman: `FogOfWar` (görüş), Gölge Kurt, Orman Cadısı boss (`ForestWitchBoss`).
- Bölge 3 Terk Edilmiş Maden: kayalı zemin (`Resources/Battle/ground_mine_rocks.png`) ve maden dekoru
  (`BattleRegionEnvironment.AddMineScatter`).
- HUD: `BattleHudRedesign` (ahşap alt panel, portreli can kartları, afiş), `BattleOffscreenIndicators` (yöne göre gruplu).
- Görsel ölçek: kahraman 1.75, Ana Üs 2.6 birim (`BattleActorPresentation`). Çırak kahramanın sol önünde.

## Ses
- Tüm klipler `Assets/Resources/AudioLibrary.asset` üzerinden (AudioManager çalışma anında oluşur, Inspector'dan atanamaz).
- Müzik bağlı: menü/sefer Lyric & Fantasy, Ana Üs Emotional Themes çalma listesi, savaş Battle Loop (rastgele),
  boss Epic Battle Loop, sis katmanı Mystical Ambient. Parça başına ses çarpanı. Dosyalar Git LFS'te.
- **Eksik:** ses efektleri (tıklama, altın, vuruş, zafer, yenilgi, rüzgar/kuş). Kullanıcı ücretsiz paket arıyor
  (öneri: Asset Store "Casual Game Sounds", "Free Sound Effects Pack Starter", "Nature Sound FX"; ya da kenney.nl RPG/UI Audio).

## Testler
`Assets/Tests/PlayMode/*` (yansıma ile Assembly-CSharp'a erişir). Önemliler: `ProgressionTests`, `BattleStructureTests`,
`UiFixPassTests`, `ForestRegionMechanicsTests`, `ViewportRevisionTests`, `HudExpansionReviewTests`.

## Onaylanan yol haritası (9 Kasım 2026 kararları)
- **Maden:** Tiny Swords parçalarından (`BaseMineVisual`): Gold Stone kayaları (kademe başına +1, 2–6),
  renk kademeli maden evi (`BaseVisualCatalog.mineHouses` = House3), depo doluluğuna göre 0–6 külçe yığını,
  dolunca kayalar parlar. Eski `mine_stage_*` görselleri yalnız savaştaki maden bölgesi dekorunda.
- **Kahraman:** Tek kahraman = Tiny Swords Warrior (renk kademeli). Okçu/Mızrakçı seçimi kalkacak. Ninja paketi kullanılmayacak (stil/tema uyumsuz).
- **Dragon Warrior:** Yeni **6. bölgenin** boss'u + "Ejder İstilası" etkinlik boss'u (lisans kontrol edilecek).
- **Sosyal:** Gerçek zamanlı PvP yok. Eşzamansız: aynı tohumla Savunma Düellosu, arkadaşa can gönderme, haftalık skor tablosu.
- **Savaş öncesi 3 kart:** %10 kahraman hasarı, %10 kahraman canı, %10 kule+Ana Üs canı. Her kart 1 ödüllü
  reklamla açılır (0–3 arası). İlk savaşta 1 kart bedava (öğretici). **Kraliyet Üyeliği:** 3 kart da reklamsız
  ve otomatik, her biri **%15**. Eşzamansız rekabet modlarında (Düello, skor tablosu) kart/üyelik bonusu kapalı olacak.
- **7 günlük giriş takvimi** (bir gün kaçırılırsa **sıfırlanır**): 1) altın (Ana Üs seviyesine göre),
  2) 30 dk inşaat hızlandırma, 3) daha çok altın, 4) bedava savaş kartı jetonu, 5) 1 sa 2x maden,
  6) **2 saat sınırsız can**, 7) 20 elmas + küçük sandık.
- **İnşaatçılar:** 2. inşaatçı **Ana Üs 5'te** açılır, bedeli = Ana Üs 5→6 yükseltme bedeli
  (`Progression.UpgradeCost(MainBase, 6)`, şu an 850 altın). 3./4./5. = 100/200/300 elmas; en fazla 5.
  İnşaatçı binaları (inşa edilmiş ve mevcut olan) taşınabilir olacak.
  ✅ Uygulandı (`b34bc5f`): `GameManager` builderTasks listesi (kayıt v5), `BuilderHut` (MapObjectType.BuilderHut,
  basılı tut → taşı), `BuilderPanel` (kiralama), `GemCounter`/`GemIcon` (üst bar). Dev APK: elmas hapına 5 hızlı dokunuş = +100.
- **Can:** 5 can, 30 dk'da bir yenilenir; sefer yenilgisi ve pes etme 1 can düşürür. Sınırsız can paketi (14/30 gün).
- **Satış:** Başlangıç Paketi, inşaatçılar (yukarıdaki), Reklamsız, Kraliyet Üyeliği (sınırsız can, +%20 maden, günlük elmas),
  Sezon Bileti, kozmetik (kahraman/kale görünümü, ücretli dekor), elmas paketleri.
- **Ödüllü reklamlar:** yenilgide devam (savaş başına 1), zaferde 2x ödül (**günlük sınır yok**, 13 Kasım kararı; V2'deki
  günde 5 sınırı kaldırıldı — `victoryDoubleAdsPerDay = 0`), inşaatta -30 dk (günde 3–5), madende 2 sa 2x,
  can bitince +1 can (günde 3), dükkânda günlük sandık. Geçiş reklamı yalnız sonuç ekranında, sık değil.
- **Yayın:** Google Play + App Store (kullanıcının iki geliştirici hesabı var; test: Android tablet + iPhone).
  Satıştan önce bulut kaydı, satın alma geri yükleme, makbuz doğrulama, KVKK/GDPR izni şart.
  **Mac yok** → iOS derlemesi bulut servisiyle (Codemagic / Unity Build Automation), TestFlight ile iPhone'a.
  **Play hesabı kişisel** → üretime çıkmadan önce 12+ test kullanıcısıyla 14 gün kesintisiz kapalı test zorunlu.

## 10 Kasım 2026: arkadaş testi düzeltmeleri (A–D paketleri)
- **A `8459dd2`:** Sefer yıldızları aşama başına 3 yuva olarak gösterilir. Ağaç/kayalar kaydedilir
  (`PlayerSaveData.terrainResources`); 6 ağaç / 3 kaya altındayken **18 saatte yalnız 1** tane çıkar
  (`BaseMapManager.GrowTerrainResources`). Tamamlanmış aşamayı tekrar oynamak toplam ödülün **%30**'unu verir
  (`BattleManager.ReplayRewardRate`). Arka planda maden üretimi artık kaybolmaz. Dekorlar 2x2 (kuyu/ağaç 3x3);
  çakışan eski dekor en yakın boş yere geçer.
- **B `ced9e12`:** Tek kahraman Warrior (`HeroLook`: seviye renk kademesi, darbe alınca Guard animasyonu;
  Tiny Swords kareleri 64 px/birim → `WarriorScale` 0.64). Kalkan `HeroData.ShieldForLevel` = %10 + seviye
  başına %2. Savaşçı düşünce **8 sn sonra dirilir**; yenilgi yalnız Ana Üs (ya da maden) yıkılınca / süre bitince.
  Savaşçı yıldızı: hiç düşmeden %50 üstü can. Warrior taban can 200; düşman hasar artışı aşama başına %6.
  "Zamana karşı" 3 dk sayaç; "Madeni koru" savaşta maden (`BattleManager.DefendedMine`, düşmanların yarısı
  `EnemyController.RaidTarget`). Göstergeler `BattleObjectiveHud`.
- **C `c85c84f`:** Sefer haritası/bölge ekranı resimli: `Resources/CampaignArt` (üreteç
  `Tools/campaign_art/generate.py`, Pillow). Aşama noktaları `CampaignSceneSetup.RoadNodes` = `NODES_UI`.
- **D `2146e7e`:** `EnemyMotion` (tek kareli düşmanlara kodla yürüme/atılma/geri tepme), ağaç sallanması,
  top patlaması ve enkaz ateşi (`TinyFx`, katalogda tree/explosion/fireControllers).
- Not: Yeni dosyaların `.meta`'ları Unity açılınca oluşur; sonraki commit'e eklenmeli.

## 10 Kasım 2026: yol haritası adımları
- **③ `4b1d50d` Can + savaş kartları:** `LivesSystem` (5 can, 30 dk, yenilgi/pes -1, reklamla günde 3 can,
  sınırsız can süresi). `BattleCards` (+%10 hasar/can/kale; ilk savaş 1 bedava, jeton, Kraliyet %15,
  `Competitive` bayrağı). `GameManager.TryStartCampaignBattle` can kontrolü + kartları savaşa geçirir.
  `RewardedAds` tüm ödüllü reklamların tek girişi; **şimdilik 3 sn'lik deneme ekranı** (6. adımda SDK).
- **④ `a6b0fd6` 7 günlük giriş takvimi:** `DailyRewards` + `DailyRewardsUI` (Ana Üs solunda düğme,
  günde bir kez kendiliğinden açılır). Hızlandırma `GameManager.SpeedUpUpgrade` (yalnız "upgrade"
  görevleri; yoksa `pendingSpeedupMinutes`), 2x maden `mineBoostUntilTicks`.
- **⑦ `4379f41` Bölge 6 "Ejderin İni":** `region_06.asset` (Ana Üs 14), `EnemyType.DragonWarrior`,
  `DragonBoss` (kare sayfaları `Resources/Dragon`, Uçan Tekme + Yer Sarsıntısı). Sonsuz Sefer son bölgeye bağlı.
- **Kullanıcı yardımı bekleyenler:** ⑤ bulut kaydı (Firebase/Play Games hesabı) ve Play kapalı test,
  ⑥ reklam SDK + satın alma (AdMob/ürün kimlikleri), Dragon Warrior lisansı (graphictoon.com),
  eşzamansız sosyal (sunucu), ⑧ mağaza başvurusu.
- **Henüz yapılmayan (karar bekleyen):** Ejder İstilası etkinliği (takvim/ödül), Bölge 4–5 kimliği
  ("Don" mekaniği, kendi bossları), Sonsuz Sefer modu, savaş kamerası sınırı, APK boyutu (Firebase).
- **`3dc129d` ekran görüntüsü düzeltmeleri:** savaşta kahraman %30 büyük (`HeroLook.BattleScale`); renkli
  kutularda `SolidPillSprite` (`PillSprite` koyu zemin taşır, rengi siyaha boğar); savaş pulları dalga
  duyurusunu aşağı kaydırır.

## 10 Kasım 2026: elmas kullanımı
- Fiyatlar tek yerde: `Systems/GemEconomy.cs`. Kazanç: `Systems/GemRewards.cs` (yeni yıldız +1, ilk boss +10,
  yeni Ana Üs seviyesi +5, reklam +2 × günde 3). Ortak onay penceresi `UI/GemConfirm.cs` (kendi tuvali,
  elmas yetmezse Elmas Dükkanı açar), akışlar `UI/GemActions.cs`, dükkân `UI/GemShopUI.cs` (üst bar elmas hapı;
  Development APK'da dükkânın elmas hapına 5 hızlı dokunuş = +100).
- İnşaat: hemen bitir (`GameManager.FinishTaskNow`, 2,2×√dk, 1 dk altı bedava; bina paneli düğmesi ve
  inşaatçı paneli satırları), İnşaat İksiri (`ActivateBuildPotion`, 2 sa 4x, `GemEconomy.Boosted`),
  misafir inşaatçı (`RentTempBuilder`, 24 sa; `WorkingBuilderCount` = kalıcı + misafir, misafir elindeki işi
  bitirir; görünüm `BuilderVisualController.UpdateGuest`), eksik altın (`TopUpGold`, 1 elmas = 40 altın).
  `BuilderSystem` geri çağrıları artık görev nesnesine bağlı (bitiş anı elmasla değişebilir).
- Savaş: Ana Üs yıkılınca devam (`BattleManager.OfferContinue`, 25, oyun durur, üs %50 + 3 sn koruma,
  savaş başına 1), hemen kalk (`BattleObjectiveHud`, 3, onaysız), savaş kartı 5 elmas (reklam ikinci seçenek).
- Diğer: canları doldur 15 (`LivesSystem.RefillWithGems`; sefer can fişi), maden 2x 4 sa 20 (`BuyMineBoost`),
  seriyi kurtar 10 (`DailyRewards.SaveStreak`, yalnız tek gün kaçtıysa). Testler `GemEconomyTests`.

## 10 Kasım 2026: 2. tur (onaylı sıra tamamlandı)
- **Firebase kaldırıldı** (`e5a1a37`, kodda kullanılmıyordu, ~379 MB). Bulut kaydı Play Games Saved Games,
  çökme raporu Unity Cloud Diagnostics ile yapılacak (kullanıcı hesabı gerekir).
- **Kayıt güvenliği** (`88e082d`): 3 kopya (PlayerPrefs asıl + önceki yedek + `persistentDataPath/save.json`,
  geçici dosya → taşı); açılışta sağlam en yeni (`lastSavedTicks`); hepsi bozuksa `_Corrupt` anahtarı.
- **Test menüsü** `UI/DevMenu.cs` (yalnız editör/Development; sol kenarda "DEV"): altın/elmas, Ana Üs ±1,
  bölge aç, zaman +1/+8 sa (`GameManager.DevAdvanceTime`), takvim günü, canlar, Kraliyet, reklam hakları,
  Sonsuz +5 kat, İstila aç/kapat, savaşta düşmanları öldür.
- **Dil** (`a68c362`…`1d17eb5`): `Systems/L.cs` → `L.T("Türkçe", "English")`, `L.Upper`, `L.Asset` (asset metinleri
  `Systems/AssetText.cs` sözlüğünden). İlk açılışta telefon dili; Ayarlar'da "DİL" düğmesi (savaş dışı, sahne
  yeniden yüklenir). **Yeni metin her zaman L.T ile yazılmalı; yeni asset metni AssetText'e eklenmeli.**
- **Reklam noktaları** (`5a9dea5`, `Systems/AdRewards.cs`): zaferde 2x altın, Ana Üs yıkılınca reklamla devam,
  hemen bitir penceresinde −30 dk (günde 3), dükkânda maden 2 sa (günde 1) ve günlük sandık. Kraliyet:
  sınırsız can, +%20 maden, günde +10 elmas (satın alma 6. adımda).
- **Bildirimler** (`539ea47`, `Systems/GameNotifications.cs`, paket `com.unity.mobile.notifications` 2.4.2):
  inşaat bitti, canlar doldu, maden doldu, günlük ödül 19:00, 3 gün hatırlatma; 22–09 sessiz; Ayarlar'da aç/kapa.
  Android kısmı `#if UNITY_ANDROID && !UNITY_EDITOR` (dotnet build ile doğrulanamaz).
- **Günlük görevler / Sen yokken / İpuçları** (`9769a0b`): `DailyQuests`(+UI, takvim altında "GÖREV"),
  `AwayReport`(+UI, 15 dk+ ayrılık), `FirstTimeTips` (PlayerPrefs `MiniEmpireBuilder_Tip_*`).
- **Bölge bossları** (`0e05b27`, `Battle/RegionBoss.cs`): Kale Muhafızı / Maden Gardiyanı / Buz Hükümdarı /
  Karanlık Kral (Trol gövdesi + renk + aura + 2 yetenek). "Don" = `Battle/BattleWeather.cs` kar fırtınası.
  Savaş kamerası `BattleSceneSetup.CameraSizeFor(aspect)` 8–9,6.
- **Sonsuz Sefer + Ejder İstilası** (`282bc0c`, `Systems/SpecialModes.cs`): `BattleSession.Special`
  (Endless id 99 / Invasion id 98) sefer akışını kullanır; `BattleSession.BattleLevel` tek güç kaynağı.
  Sonsuz: 5 katta bir tema/boss, 30+kat güç, 5'in katlarında +3 elmas. İstila: hafta sonu, Ana Üs 5,
  ilk zafer +15 elmas.
- Bash notu: Bash aracı heredoc içindeki `\\` çiftini teke indiriyor; kaçışlı düzenlemeler için betiği Write ile
  dosyaya yazıp çalıştır (scratchpad `addcs.py` csproj'a dosya ekler).

## Açık konular / sıradaki fikirler
- Ses efektlerinin bağlanması (paket bekleniyor).
- Denge: düşman rolleri menzilleri, yıkılma sıklığı, 15 seviyelik bedel/süreler oynanarak ayarlanacak.
  Tek inşaatçı darboğaz olabilir (kilitli ikinci inşaatçı yeri `BuilderSystem.Builders[1]`).
- Bölge 4–5 için kimlik (zemin, dekor, özel mekanik "Don") ve boss'lar; Sonsuz Sefer henüz yok.
- Savaşta kamera yuvaları ekran kenarında kesebiliyor (kamera sınırı önerildi).
- APK ~118 MB: `Resources` içindeki büyük görseller ve kullanılmayan Firebase incelenebilir.
- Ayrıntılı geçmiş: `git log` ve `UIReviewScreenshots/*/README.md`, `*_PASS.md`.

## 11 Kasım 2026: round 2 (gece-gelistirme)
- Ortak ızgara yerleşimi `Base/BaseOccupancyGrid.cs`, `PlacementController` (serbest ızgara, YERLEŞTİR/İPTAL),
  `Base/BaseCameraController.cs` (kaydırma/yakınlaştırma, kenar kaydırma), yüklemede çakışma onarımı.
- Karşı düşmanlar (`EnemyVariant`, `DamageSource`, `Progression.Counters/CounterMix`), boss kalkanı (yalnız kahraman kırar).
- Bölge mekanikleri: `BattleWeather` (donan kuleler), `ShadowShrines.cs`, `VolcanicEruptions.cs` (`Progression.RegionMechanics`).
- Başlangıç altını Ana Üs 7/10/13'te 25/50/75; kule eğrisi `Progression.TowerDamage/TowerUnits`.
- `Systems/DevSettings.cs` (test kolaylıkları tek anahtar), `Tools/economy_sim.py`, bot profilleri (good/casual/towerOnly/heroOnly).
- Tasarım: `Docs/design/round2.md`; rapor: `MORNING_REPORT.md`.

## 12 Kasım 2026: Kart Atölyesi
- Ana Üs 5 binası (House2), Et + altınla 7 kart (3 Sıradan + Nadir Keskin Oklar/Zırh Delici/Gök Avcısı + Destansı Kalkan Kıran).
  Kullanım hakkı modeli (1 savaş = 1 hak, yenilgide de). Kayıt v8. Ayrıntı: `Docs/design/card_workshop.md` sonundaki uygulama notu.

## 13 Kasım 2026: Ana Üs bağları (Faz F: Kışla + Köylü Evi)
- `BuildingType.Barracks/House` (enum sonuna), açılış `Progression.BarracksUnlock {7,11}` / `HouseUnlock {3,9}`, bedel çarpanı 0,65 / 0,4; elmasla alınmaz. Kayıt v9 (tür girdileri `EnsureBaseBondEntries`).
- Sefer savaşında `Battle/AllyLancer.cs` (`AllyBarracks` + `AllyLancer`): kurulu her Kışla 1 + ⌊L/5⌋ mavi Lancer, yalnız Ana Üs'ün 3,5 u çevresini korur, 15 sn'de yeniden doğar; hasar `DamageSource.Ally`.
  Yakın dövüşçü düşman vuruş menzilindeki Lancer'a vurur (`EnemyController.FindBestTarget`). Köylü Evi `GameManager.GetGoldMineStorageCapacity` içinde maden deposunu büyütür.
- Bot: `PLAYTEST_NO_BARRACKS=1` Kışla/Ev kurmaz (eşlenik ölçüm), `PLAYTEST_SEEDS=N` tohum sayısı. Ayrıntı ve ölçümler: `Docs/design/island_base_v2.md` §22.

## 13 Kasım 2026 (2. oturum): oyun testi geri bildirimi (D24–D30)
- **Ada alt menüsü** = Ana Üs'ün `RadialDockMenu` bileşeni (`Create` → `Register` → `FinishSetup`): İNŞA ET · AKIN (Akın numarası
  pulu, `infoBadgeProvider`) · KADRO. Ayrı İNŞA ET/AKIN düğmeleri ve sol üst KADRO kalktı. Durum anahtarı `MEB_IslandRadialOpen`.
- **Akın sayacı** kompakt (`IslandHud.RaidPillText`, "14s 48dk"), dokununca açıklama. **Akın sonucu** "ADAYA DÖN"
  (`SceneCloudTransition.BeginInPlace`: sahne yüklemeden bulut örtüsü).
- **Bölgeler:** adacıklar bütün olarak tek bölgede; NW adacığı Z0. Bölge bulutları kapsama ile pişirilir
  (`IslandZoneCloud.CoreCovers`, `BakeIslandTerrain.CoverZone`); `Nov13FeedbackTests` her hücreyi denetler + `F4_zone_map_debug.png`.
  `ZoneAt` değişirse doğa prefab'ı yeniden pişirilmeli: `-executeMethod BakeIslandTerrain.BakeFromCommandLine` (terrain prefab'ındaki
  rastgele `m_AnimationSpeed` gürültüsünü `git checkout` ile geri al).
- **Ada kadrajı** ağaç tepelerini kapsar (`IslandCameraController.TreeTopHeight`), alt HUD payı 0,10.
- **Ana Üs ana düğmesi:** çapraz kılıç + kalkan arması (elmas yok); rozetler dairenin kenarında (`RadialDockMenu.EdgeBadgePosition`).
- **Yenilgi önerisi** "Üsse dön, güçlendir" Ana Üs ≤ 4 (`VictoryReferenceView.EarlyDefeatMaxBase`).
- Üst bar veri kaynağı: altın `GameManager.Gold`, elmas `GameManager.Gems` (+`OnGemsChanged`), Et/Odun `PlayerData.island` — Ana Üs
  ve Ada aynı; `Nov13FeedbackTests.JourneyIslandRaidReturnBaseIslandWithSharedTopBarValues` iki yönde karşılaştırır.

## 13–14 Kasım 2026 (3. oturum): geri bildirim 2 + Google Play (D31–D44)
- Ada: savunmada kilitli bölge bulutları yarı saydam; HARİTA = üs kadrajı. Çayır engel değil; yükleme göçü `IslandRepair.CleanGhosts`.
- Ana Üs: `BaseVisualBounds` + `BaseMapManager.RepairVisualLayout` (sprite çakışması); rozet kuralları `UI/BaseBadges.cs`.
- Ortak UI: `RadialDockMenu` sabitleri (72/56/48), üst bar sırası `FeatureConfig.baseTopBarOrder/islandTopBarOrder` (`ResourceTopBar`),
  süslü pencere `UI/FancyInfoPanel.cs` (ada + Ana Üs), GERİ AL `UI/UndoMoveButton.cs`.
- Hareket: `Base/BaseAmbientLife.cs`; akın: `IslandAbilities.UseAt` + nişan kipi, `Island/IslandAbilityFx.cs` (ağ/alev/sarsıntı).
- Yayın: `Editor/AndroidReleaseBuild.cs` (-executeMethod AndroidReleaseBuild.BuildFromCommandLine; imza MEB_KEYSTORE_* ortam değişkeni),
  `Systems/ReleaseLogging.cs`, `Tools/check_aab.py`; belgeler PLAY_RELEASE / DATA_SAFETY / PRIVACY_POLICY / STORE_LISTING.
- Test notu: `-testFilter BasePresentationTests` tüm takımı koşturur (derleme adı); sınıf adlarıyla gruplayın.

## 14 Kasım 2026 (4. oturum): hatalar + temel sistemler + UI/UX (D45–D58)
- **Yol bulma:** `Systems/GridPathfinding.cs` (A*, `GridPathFollower` yeniden rota, `GridSteer` kovalama) + `BaseWalkGrid` / `IslandWalkGrid`
  (yükseklik yalnız merdivenden) / `BattleWalkGrid` (BattleObstacles.Version). F6 debug `WalkGridDebug`. Yeni yürüyen birim bunları kullanmalı.
- **Seçim:** `UI/SelectionManager.cs` (tek seçim; boş yer/başka öğe/geri tuşu kapatır). Yeni seçilebilir öğe `Select(öğe, kapat)` çağırmalı.
- **Ölçek:** `Systems/UnitScale.cs` tek referans (Battle ≈0,707 = kahraman, Base 1,9/3, Island 1).
- **Dekor:** küçükler 1×1 (`DecorData.visualWidth`), emekli dekorlar `retired` (katalog `DecorPlacementController.Catalog`), kayıt `decorLayoutVersion`.
- **Ada:** `Island/IslandNatureClear.cs` (çalı/kaya temizliği, kayıt `clearedNature`). Madende tek figür = Çırak (`BaseApprenticeVisual` bağlayıcı).
- **Ekonomi:** bedava bitir yok; ≤1 dk 1 elmas (`GemEconomy.FinishCost`), `GemConfirm.SetGemIcon`.
- **UI:** Kahraman/Çırak penceresi `FancyInfoPanel`; Kart Atölyesi ve Ayarlar yeniden (Ayarlar'da gizlilik URL'si/destek e-postası YER TUTUCU).
- **Test:** editör açıkken testler `D:/UnityProjects/MEB_TestRunner` kopyasında (robocopy /MIR + batchmode; yeni .meta'ları geri kopyala).
  `Session4Tests` 4 ekran oranında (`Ratios`) UI görüntüsü alır (`CaptureUi`). Unity sahte-null: `GetComponent() ?? AddComponent()` kullanma.

## 14 Kasım 2026 (5. oturum): yerleşim + yol bulma + eylem menüsü (D62–D67)
- **Ana Üs yerleşimi:** tek kural hücreler. Yapı engel alanı = ayak izi ∪ görsel hücreleri (`BaseOccupancyGrid.BlockOf/VisualCells`, dinlenme
  ölçeği `RestVisualRelative`); oyun ve test aynı fonksiyonu kullanır (`PlacementController.Validate`). Izgarada dolu hücreler kırmızı.
  Yükleme onarımı ve GERİ AL YUMUŞAK (`FootprintFree`, `RestoreObjectTo`): eski düzen kıpırdamaz. Denetim/tarama `Base/BasePlacementAudit.cs`.
  Yüklemede `BaseMapManager.ValidateOccupancy` (hayalet kayıt temizliği). `NatureObstacle.cell` artık özellik (kayıt hatası düzeldi).
- **Savaş engeli görselden** (`BattleObstacles.FromVisual`; ada yapıları `VisualObstacle=false`); düşman vuruş mesafesi engel kenarından.
- **Dekor ölçeği** `Systems/DecorScale.cs` tablosundan (ayak izinden bağımsız). Yeni dekor eklerken tabloya satır ekle.
- **UI:** `UI/WorldActionMenu.cs` (dekor + bina dokunuş menüsü; bina dokunuşu artık önce menü açar), `WorldConfirmButtons.Create(…, Func<Bounds>)`
  (hayaletin yanında), `UndoMoveButton` (küçük, sol alt, 3 sn). `FeatureConfig.recommendedLayout` (kapalı).
- **Test:** `Session5Tests`, `Session5UiTests`. Bilinen eski başarısızlar: IslandRaidTests.LiveRaid…, IslandPhaseCTests.MonsterFlips…, BaseShadowRevisionTests.

## 14 Kasım 2026 (6. oturum): savaş hataları + ekonomi + dükkân + ada ilerlemesi + arazi (D68–D84)
- **Tamamlanmış seviye tek kaynak:** `Systems/BuildingLevels.cs` ("build" görevi süren bina 0). Kahraman/çırak yükseltmesi de inşaatçı görevi
  (`GameManager.HeroTaskSlot/ApprenticeTaskSlot`), seviye süre bitince artar.
- **Savaş:** menzil `BattleStructure.RangeCenter` + `Battle/RangeRing.cs`; can barı `UI/UiHealthBar.cs` (UI) ve `HealthBar` (dünya) sola sabit + beyaz iz;
  duraklatma `Battle/BattlePause.cs` (nedenli; ayarlar + arka plan, ses dahil). Tiny Swords Run/Idle kliplerinde döngü açık; kahraman Animator AlwaysAnimate.
- **Ekonomi:** Economy_V2 maden 600+90(L−1)/sa, depo 4 sa, çırak %L, Köylü Evi +%5/+%1 (`Base/MineStorageGauge.cs`). Sim `Tools/economy_sim_v2.py`
  (V2-5.oturum vs V2). Dükkân `Systems/Shop/*` (ShopConfig.asset, ShopService, IapService = Unity IAP 5.4 + FakeStoreBackend), `UI/ShopTabView.cs`.
- **Ada:** `Island/IslandIntro.cs`, `Island/IslandBuildTutorial.cs`; `KeepFollowsMainBase=false`, kilit `KeepRequiredMainBase`; kale yükseltmesi
  `IslandEconomy.TryStartKeepUpgrade`; ada kurulum altını `FeatureConfig.islandBuildGold`; akın ölçeği `akinScalePerKeepBelow`.
- **Arazi/temizleme:** `Base/LandExpansion.cs` (`landOpened`), `Systems/AreaHighlight.cs`, `Systems/NatureClear.cs` (süre/ödül FeatureConfig).
- **Kayıt v13** (v12 IAP defteri, v13 ada göçü `IslandEconomy.MigrateV13`). Test `Session6Tests` (S1…S7).
- **Test notu:** `-testFilter "Island"` gibi geniş filtre `[Explicit]` uzun ölçümleri de koşar (saatlerce sürebilir) — sınıf/test adlarıyla grupla.

## 15 Kasım 2026 (7. oturum): bölgeye özgü savaş haritaları (D87–D95)
- **Sefer savaşları bölge haritasında** (`Assets/Scripts/Battle/Map/`): `BattleRegionMap` (Resources/BattleMaps/Region_N: karo seti, kara maskesi,
  atmosfer, süsler, bölge yapıları) + `BattleLevelLayout` (Levels/RN_LM: rotalar, yuvalar, üs/maden/kahraman). Kurulum `BattleMapBuilder`,
  çalışma anı arazi `BattleTerrain` (IsLand/Walkable/KeepOnLand; `BattleWalkGrid` su/uçurumu kapalı sayar), katalog `BattleMapCatalog`
  (`Enabled=false` → eski merkez-üs düzeni; sefer dışı savaş da eski düzen). Izgara 16×32, 1 karo = 1 birim, Ana Üs (0,0), giriş köprüsü (0, 28,5).
- Üreteç/doğrulama `BattleLayoutGenerator` (1. seviye = generate.py harita yolu). Editör: `Editor/BattleMapAuthoring.cs`
  (`-executeMethod BattleMapAuthoring.GenerateFromCommandLine [-force]`; var olan varlığa dokunmaz), `Editor/BattleLevelLayoutEditor.cs` (Scene tutamaçları).
  **ScriptableObject sınıfı kendi adını taşıyan dosyada olmalı** (iki SO tek dosyadayken varlıklar yüklenmiyordu).
- Düşman: `EnemyController.SetLane` (yol izleme, `LaneLeash` 3 birim sapma); maden yağmacısı karadan A*. Denge çarpanı `BattleManager.MapEnemyHealth`.
- Atmosfer `BattleAtmosphereFx` + `BattleFxQuality` (düşük kalite: QualitySettings 0 ya da `MEB_LowFx`). Karolar `Tools/campaign_art/recolor_tilesets.py`,
  süsler `battle_props.py`, önizleme `layout_preview.py`, karşılaştırma `compare_maps.py`; denge özeti `Tools/balance_s7.py`.
- Testler `Session7Tests` (V/B/G), `Session7BalanceTests` ([Explicit]). Test yardımcısı `Session4Tests.StartBattle` artık ada kipini temizler.
