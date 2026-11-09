# Mini Empire Builder — Yapay Zekâ Devir Notu

Yeni bir sohbette projeye hızlıca devam edebilmek için güncel bağlam. Son güncelleme: 9 Kasım 2026.
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
  (mavi/sarı/kırmızı/mor/siyah), yükseltme süreleri (15 sn, 1 dk, 30 dk, 1/3/6/12/18/24 sa, 30–48 sa), bedeller.
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
- **Savaş öncesi 3 kart:** %10 kahraman hasarı, %10 kahraman canı, %10 kule+Ana Üs canı; ödüllü reklamla alınır.
- **7 günlük giriş takvimi** (7. gün az elmas).
- **İnşaatçılar:** 2. inşaatçı belli Ana Üs seviyesinde altınla; 3./4./5. = 100/200/300 elmas; en fazla 5.
  İnşaatçı binaları taşınabilir olacak.
- **Can:** 5 can, 30 dk'da bir yenilenir; sefer yenilgisi ve pes etme 1 can düşürür. Sınırsız can paketi (14/30 gün).
- **Satış:** Başlangıç Paketi, inşaatçılar (yukarıdaki), Reklamsız, Kraliyet Üyeliği (sınırsız can, +%20 maden, günlük elmas),
  Sezon Bileti, kozmetik (kahraman/kale görünümü, ücretli dekor), elmas paketleri.
- **Ödüllü reklamlar:** yenilgide devam (savaş başına 1), zaferde 2x ödül, inşaatta -30 dk (günde 3–5), madende 2 sa 2x,
  can bitince +1 can (günde 3), dükkânda günlük sandık. Geçiş reklamı yalnız sonuç ekranında, sık değil.
- **Yayın:** Google Play + App Store (kullanıcının iki geliştirici hesabı var; test: Android tablet + iPhone).
  Satıştan önce bulut kaydı, satın alma geri yükleme, makbuz doğrulama, KVKK/GDPR izni şart. iOS derlemesi Mac/Xcode ister.

## Açık konular / sıradaki fikirler
- Ses efektlerinin bağlanması (paket bekleniyor).
- Denge: düşman rolleri menzilleri, yıkılma sıklığı, 15 seviyelik bedel/süreler oynanarak ayarlanacak.
  Tek inşaatçı darboğaz olabilir (kilitli ikinci inşaatçı yeri `BuilderSystem.Builders[1]`).
- Bölge 4–5 için kimlik (zemin, dekor, özel mekanik "Don") ve boss'lar; Sonsuz Sefer henüz yok.
- Savaşta kamera yuvaları ekran kenarında kesebiliyor (kamera sınırı önerildi).
- APK ~118 MB: `Resources` içindeki büyük görseller ve kullanılmayan Firebase incelenebilir.
- Ayrıntılı geçmiş: `git log` ve `UIReviewScreenshots/*/README.md`, `*_PASS.md`.
