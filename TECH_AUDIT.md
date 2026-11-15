# TECH_AUDIT — Mini Empire Builder teknik denetimi (9. oturum, 15 Kasım 2026)

Dal `gece-gelistirme`, başlangıç commit'i `cbee273`. Önceki denetimler (AUDIT.md, D84, PLAY_RELEASE.md, REPORT.md) tekrarlanmadı; üzerine kuruldu.

**Etiketler.** Kanıt: **Kesin** (koddan doğrulandı) · **Muhtemel** (çıkarım) · **Ölçüm** (çalışma zamanı ölçümü gerekli).
Önem: **P0** yayın engeli / veri kaybı · **P1** oyuncu deneyimini ciddi bozar · **P2** kalite/bakım · **P3** iyileştirme. Zorluk: S (<1 sa) · M (yarım gün) · L (1+ gün).

**Yöntem.** Altı tarama hafif modelli alt ajanlara verildi (envanter, performans, olay/statik durum, durum verisi, platform, mağaza kuralları).
Rapora giren her bulguyu ilgili dosyayı açarak kendim doğruladım; doğrulayamadığım ya da ikinci turda dayanaksız kalan bulgular §11'de listeli.

---

## 1. Yönetici özeti

**Teknik sağlık: orta-iyi, yayına hazır değil.** Oyun oynanabilir, çekirdek kurallar tek yerde toplanmış (`Progression`, `Economy`, `BuildingLevels`,
`GameClock`), 66 test dosyası ve otomatik baştan oynatma var; olay abonelikleri temiz, hile yüzeyi yayın derlemesinde kapalı. Zayıf taraf:
kayıt katmanı bir hata anında oyuncunun ilerlemesini sessizce silebiliyor, aynı bilginin birden çok kopyası hâlâ var, ve iOS tarafında hiçbir hazırlık yok.

**En büyük 5 risk**
1. **Kayıt açılamazsa oyun sessizce yeni oyun başlatıp bütün kopyaların üstüne yazıyor** (K-1, P0). Göç sırasında bir hata ya da bozuk kayıt, oyuncunun
   bütün ilerlemesini geri dönüşsüz siler. Göçler de orijinal kaydı ara sürümlerle eziyor (K-2).
2. **Aynı hataların geri gelmesinin kök nedenleri hâlâ kısmen açık** (§3): kayıtlı türetilmiş değerler (bina kilidi, tür seviyesi), sahneye bağlı
   göçler, ve geliştirici derlemesinin oyun kuralını değiştirmesi (test ettiğiniz APK ile yayın APK'sı aynı oyunu oynatmıyor).
3. **Gelir modeli bağlı değil:** reklam SDK'sı yok (3 sn'lik deneme ekranı yayın derlemesinde de ödül veriyor), IAP makbuz anahtarı üretilmedi,
   izin (UMP) akışı yok. Bunlar olmadan mağazaya "reklamsız, satın almasız" çıkılabilir ama planlanan ekonomi çalışmaz (Y-1…Y-4).
4. **iOS hazırlığı sıfır:** paket kimliği, derleme hattı (Mac yok), bildirim kodu, gizlilik etiketi envanteri yok (Y-10…Y-13).
5. **Cihaz performansı ölçülmedi.** Savaşta her kare sahne taraması yapan 4+ nokta ve havuzsuz mermi/hasar yazısı var (§6); masaüstünde sorun
   görünmüyor, düşük Android cihazda bilinmiyor.

**Yayına uzaklık (kod tarafı).** Google Play **kapalı test**: bu oturumdaki kayıt düzeltmelerinden sonra kod tarafı hazır; kalanlar sizde (imza anahtarı,
gizlilik politikası URL'si, 12 test kullanıcısı × 14 gün). Google Play **üretim**: reklam + izin + makbuz anahtarı kararlarına bağlı, kabaca 1–2 haftalık iş
+ 14 günlük zorunlu test. **App Store**: en az 2–3 hafta (bulut derleme hattı, iOS bildirimleri, ATT, mağaza formları) ve bir iPhone'da tam tur.

## 2. Mimari genel bakış
[ARCHITECTURE.md](ARCHITECTURE.md): klasörler, çalışma zamanı yapısı, ana döngüler, durum verisi haritası (§4.1 tek doğru kaynak tablosu), olaylar.

---

## 3. Sistemik kök nedenler

Soru: *bu proje hangi mimari zayıflıklar yüzünden aynı hataları üretmeye devam ediyor?* Yedi tekrarlayan hatanın DECISIONS.md'deki kök neden
kayıtlarını (D31–D34, D49, D62–D64, D68, D77, D86, D100) tek tek okuyup ortak nedenlere indirdim.

| Tekrarlayan hata | Kayıtlı kök neden | Ortak neden |
|---|---|---|
| Boş görünen yere "yer dolu" (3 kez) | D62: ızgara kuralından ayrı, görünmeyen ikinci bir çakışma denetimi · D33: elle güncellenen statik doğa sözlüğü sahneden kopuyor · D100: dekor, dolu sayılan hücrenin bir hücre üstünde çiziliyor; denetim aracı kuralla aynı veriyi okuduğu için hatayı göremiyor | KN-1, KN-2, KN-4 |
| Karakterler bina/maden içinden geçiyor (2 kez) | D45: üç ayrı hareket sistemi · D63: engel, görselden başka kaynaktan | KN-1, KN-2 |
| Ada adı/tanıtımı hiç tetiklenmedi | D77: `DevSettings.Enabled` (Development APK'da varsayılan açık) akışı kapatıyordu · D86: test düğmeyi doğrudan çağırdığı için dokunuşu yutan katmanı görmedi | KN-4 |
| Bitmemiş inşaat savaşa yansıdı | D68: seviye kayda inşaat başlarken yazılıyor, okuyanlar ham alanı okuyor | KN-1 |
| Akın haritası eski görüntü | D31: bir kez üretilen görüntü, değişiklikte yenilenmiyor | KN-1 |
| Rozet kaybolmadı | D32: rozet olayla yakılıyor, söndüren yol yok | KN-1 |
| Dekor alanı küçülünce görsel de küçüldü | D49/D64: görsel ölçek ayak izinden hesaplanıyor | KN-2 |

### KN-1 — Türetilmiş durum saklanıyor ve elle eşitleniyor
Bir bilgi başka bilgiden hesaplanabildiği hâlde ayrıca tutuluyor ve her değişiklikte elle güncelleniyor; bir güncelleme yolu unutulunca iki kopya ayrışıyor.

**Kapatılmış örnekler (doğrulandı):** Ana Üs doluluğu artık her sorguda sahneden kuruluyor (`Assets/Scripts/Base/BaseOccupancyGrid.cs:136-179`);
rozetler 0,5 sn'de bir kuraldan hesaplanıyor (`Assets/Scripts/UI/RadialDockMenu.cs:479-482`, `Assets/Scripts/UI/BaseBadges.cs:11-23`);
tamamlanmış seviye `BuildingLevels` üzerinden (`Assets/Scripts/Systems/BuildingLevels.cs:32-36`); akın canlı kaydı okuyor.

**Hâlâ açık örnekler:**

| # | Bulgu | Kanıt | Önem |
|---|---|---|---|
| KN-1a | **Bina kilidi kayıtta saklanıyor ve yalnız Ana Üs yükselirken yeniden hesaplanıyor.** `CheckUnlockBuildings` yalnız `ApplyInstanceUpgrade(MainBase)` içinden çağrılıyor (`Assets/Scripts/Managers/GameManager.cs:452-455`, tanım `:832-851`); yüklemede çağrılmıyor (`RebuildBuildingData`, `:318-340`). Bir güncellemede bir binanın açılış seviyesi düşürülürse, o seviyeyi çoktan geçmiş oyuncu binayı bir sonraki Ana Üs yükseltmesine kadar göremez; Ana Üs 15'teki oyuncu hiç göremez. | Kesin | P2 |
| KN-1b | **Tür seviyesi kayıtta iki kez duruyor:** `placedBuildings[].level` ve `buildings[].level`. Gerçek editör kaydında bu görünüyor: `buildings[MainBase].level = 15`. `RecomputeTypeLevel` (`GameManager.cs:342-350`) ikinciyi birinciden yeniden yazıyor ama yalnız `best > 0` ise; hiç tamamlanmış binası olmayan türde eski değer kalıyor. Okuyanların çoğu (`BattleManager.cs:1099` maden seviyesi, `GameManager.cs:1127` maden üretimi, `GetMainBaseLevel`) bu ikinci kopyayı okuyor. | Kesin | P2 |
| KN-1c | **Ada Et/Odun cüzdanının değişim olayı yok** (`Assets/Scripts/Island/IslandEconomy.cs:118-119, 126-127, 148-149`, `IslandNatureClear.cs:146`). Arayüz yoklamayla güncelleniyor; yoklamayı unutan yeni bir pencere eski sayıyı gösterir. | Kesin | P3 |
| KN-1d | **`BaseMapManager.SaveAll` kaydı sahneden baştan yazıyor** (`placedBuildings = list`, `Assets/Scripts/Base/BaseMapManager.cs:1470`). Sahne eksik kurulmuşken (yükleme hatası, yarım kurulum) çağrılırsa eksik liste kaydın üstüne yazılır. Koruma var mı diye bakılmalı. | Muhtemel | P1 |

### KN-2 — Görsel ile mantık birbirine bağlı
- Yapının engel alanı = ayak izi ∪ **sprite'ın kapladığı hücreler** (`BaseOccupancyGrid.BlockOf`, `:196-197`; `RestVisualRelative`, `:59-76`). Bu D62'de
  bilinçli seçildi (oyuncu gördüğü yere koyamıyorsa kırmızı görsün). Bedeli: bir binanın görseli değişince (seviye atlayınca renk kademesi, yeni çizim)
  **mantıksal alanı da değişir**; yan yana duran iki bina yükseltmeden sonra "çakışan" duruma düşebilir. Bunu yükleme onarımının yumuşak kuralı
  (`FootprintFree`, `:319-337`) örtüyor. **Kesin** (tasarım), risk **Muhtemel**, P2.
- Dekor ölçeği artık ayak izinden bağımsız (`Assets/Scripts/Systems/DecorScale.cs:38-43`), hiza görünen tabandan (D100). Kapandı.

### KN-3 — Kayıt göçleri dağınık ve geri dönüşsüz
- Göç zinciri `SaveSystem.Load` içinde, onarımlarla iç içe, 12 ayrı yerde ara kayıt yapıyor (`Assets/Scripts/Data/PlayerSaveData.cs:679-810`).
- Sürüm numarası dışında **yedi ayrı düzen işareti** var (`decorLayoutVersion`, `timingVersion`, `landOpened = −1`, `natureBaselineLevel`,
  `levelsIndependent`, `legacyVillage`, `villageSeeded`); bunların göçleri kayıt yüklenirken değil, **ilgili sahne açılınca** çalışıyor. Sonuç:
  bir göçün çalışıp çalışmadığı oyuncunun hangi ekrana girdiğine bağlı ve otomatik test ancak o sahneyi kurarsa yakalıyor.
- Ayrıntı ve çözüm: §5 K-1…K-5.

### KN-4 — Test ortamı gerçek oyuncu yolunu izlemiyor
| # | Bulgu | Kanıt | Önem |
|---|---|---|---|
| KN-4a | **Geliştirici derlemesi oyun kuralını değiştiriyor.** `IslandSession.AutoFireUnlocked` → `… || DevSettings.Enabled` (`Assets/Scripts/Island/IslandSession.cs:31-32`); ada kilidi `IslandSceneSetup.cs:65` → `&& !DevSettings.Enabled`. `DevSettings.Enabled` Development APK'da varsayılan **açık** (`Assets/Scripts/Systems/DevSettings.cs:33`). Yani tablette denediğiniz derlemede oto-ateş baştan açık, yayın derlemesinde Kıyı Kalesi 5'te açılıyor. D77'deki hatanın aynı kalıbı. | Kesin | P1 |
| KN-4b | Oyun kodu otomatik test koşusunu tanıyıp dallanıyor: `IslandHud.cs:203` (`Application.isBatchMode` iken tanıtım/ad akışı atlanır), `IapService.cs:54`. | Kesin | P2 |
| KN-4c | Testler oyun koduna yansımayla erişiyor ve çoğu zaman yöntemi doğrudan çağırıyor; dokunuşun gerçekten hedefe ulaştığını yalnız birkaç test denetliyor (D86 sonrası `S3_1_2`). Derleme hatası yerine çalışma anı hatası verir; yeniden adlandırmalar sessizce test kırar. | Kesin | P2 |
| KN-4d | Kayıt katmanının tek testi var (`Assets/Tests/PlayMode/SaveSystemTests.cs`, bozuk kopya senaryosu). Göç zinciri için uçtan uca test yalnız v12→v13. | Kesin | P1 |

### KN-5 — Kodla kurulan dev sahne sınıfları ve sahne taraması
`BaseMapManager` 1.938, `BaseSceneSetup` 1.742, `BattleManager` 1.430 satır; nesneler birbirini `FindFirstObjectByType` / `GameObject.Find` ile
buluyor (278 kullanım). Bir özelliğin kurulumu, kaydı, görseli ve arayüzü aynı sınıfta; değişiklik yapan kişi (ya da model) bütün etkileri göremiyor.
Bu doğrudan bir hata değil ama yukarıdaki dört nedenin zeminini hazırlıyor. **Kesin**, P2, çözüm yol haritasında (kademeli bölme; yeniden yazma değil).

### Kalıcı çözüm tasarımı
1. **Değişmezlik denetimi (`SaveInvariants`)** — kayıt için "her zaman doğru olmalı" kurallarını tek yerde tanımlar (tür seviyesi = tamamlanmış en yüksek
   bina seviyesi, kilit = kuralın söylediği, görevler var olan binaya bağlı, kaynaklar ≥ 0 ve tavan altında, ada binaları açık bölgede…). Yüklemeden
   sonra ve testlerde çalışır; güvenli olanları onarır, onaramadığını geliştirici derlemesinde hata olarak yazar. KN-1 sınıfındaki bir ayrışma artık
   oyuncu ekran görüntüsü atana kadar değil, ilk yüklemede görünür.
2. **Türetilmiş değer yüklemede yeniden hesaplanır:** kilitler ve tür seviyeleri kayıttan okunmaz, kuraldan üretilir (KN-1a, KN-1b).
3. **Sürümlü göç hattı:** göç adımları sıralı bir listede, bellekte çalışır, sonunda tek kayıt; göçten önce orijinal dosya ayrıca saklanır; her sürüm
   için sabit kayıt örneğiyle test (K-2, KN-4d).
4. **Tek saat:** oyun kurallarında ham `DateTime` yok (K-6).
5. **Geliştirici ayarı kural değiştirmez:** `DevSettings` yalnız araç açar (menü, katman, kısayol); kilit/kural istisnası yapmaz (KN-4a).

Uygulama durumu için bkz. §12 "Uygulama Sonucu".

---

## 4. Oyun sistemleri analizi

| Sistem | Durum | Bulgular |
|---|---|---|
| **Zaman / zamanlayıcılar** | İyi tasarlanmış: bitiş anı UTC tick olarak kayıtta, oyun kapalıyken ilerler, `GameClock` saat geri almayı engeller | K-6 (altı yerde ham saat), K-7 (saat ileri alma sunucusuz kapatılamaz — D84'te kayıtlı sınır) |
| **Ekonomi** | Formüller `EconomyConfig` profilinde, ECONOMY.md ile eşlenik testli | O-1 (yeni hesapta maden 0), O-4 (içerik 3,5 günde bitiyor) |
| **Yerleşim / doluluk** | 8. oturumda tek kaynağa indi; regresyon testleri O0–O3 | KN-2 (görsel → mantık bağı), P-9 (her karede sahne taraması) |
| **Yol bulma** | Ortak `GridPathfinding` (A*) + üç ızgara; engeller doluluktan türetiliyor | Bulgu yok. `BaseWalkGrid` 0,25 sn'lik imza denetimi emniyet ağı olarak kalmış (`Assets/Scripts/Base/BaseWalkGrid.cs:38-56`) |
| **Düşman YZ** | Yol durum makinesi, 24 senaryolu test (A1) | P-7 (ayrışma her düşman için bütün listeyi dolaşıyor) |
| **Savaş / hasar** | `BattleStructure`, `DamageSource`, karşı düşman çarpanları | E-3 (savaş ortasında kapatınca can düşmüyor) |
| **İlerleme / kilitler** | Kurallar `Progression` içinde | KN-1a, KN-1b; ada kilidi iki sabitte (`GameData.cs:411`, `SpecialModes.cs:116`) |
| **Öğreticiler** | Bayraklar kayıtta, sıra katman numarasıyla | O-1, KN-4a, KN-4b |
| **Kayıt / göç** | 3 kopya, bozuk kopyadan kurtarma var | K-1…K-5 |
| **Sahne geçişleri** | Kararma örtüsü dokunuşu engelliyor (`GameManager.cs:1090`), çifte yükleme koruması var (`:1098`) | E-2 (kayıt açılamazsa sahneler yeni oyun başlatıyor) |
| **UI durumu** | `SelectionManager` tek seçim; pencereler statik "açık" başvurusu tutuyor ve kapatırken sıfırlıyor | E-1 (geri tuşu), E-4 |
| **Satın alma** | Önce kayda yaz, sonra mağazaya onay; işlem defteri çifte vermeyi engelliyor (`Assets/Scripts/Systems/Shop/IapService.cs:137-173`) | Y-3, Y-4, E-5 |

Sistemler arası tutarsızlık noktaları: (1) maden zaman damgasını iki sınıf iki ayrı saatle yazıyor (K-6); (2) tür seviyesi iki kopya (KN-1b);
(3) Ana Üs yerleşimi sahneden, ada yerleşimi kayıttan yönetiliyor — iki ayrı model, ortak kod yok (bilinçli, ama her hata iki kez düzeltiliyor: D33 ↔ D100).

---

## 5. Kritik sorunlar

### 5.1 Kayıt güvenliği

**K-1 — Kayıt açılamazsa oyun yeni oyun başlatıp kaydın üstüne yazıyor** · P0 · Kesin · M
- **Problem.** `GameManager.LoadGame` (`Assets/Scripts/Managers/GameManager.cs:90-113`) `SaveSystem.Load()` `null` dönerse ya da göç sırasında istisna
  atarsa `PlayerData` boş kalır. Ana menüdeki DEVAM yine de Ana Üs'e gider (`Assets/Scripts/Intro/IntroSceneSetup.cs:805-810`). Savaş ve ada
  sahneleri boş veriyi "test modu" sayıp **`StartNewGame` çağırır** (`Assets/Scripts/Battle/BattleSceneSetup.cs:67-73`,
  `Assets/Scripts/Island/IslandSceneSetup.cs:58-63`); `StartNewGame` hemen kaydeder (`GameManager.cs:67-72`). İlk kayıt asıl kopyanın ve dosyanın,
  ikinci kayıt yedeğin üstüne yazar (`PlayerSaveData.cs:535-540`).
- **Bozuk kaydın saklanması yetersiz.** Yalnız "hiçbir kopya ayrıştırılamadı" durumunda ve yalnız ilk dolu kopya tek bir PlayerPrefs anahtarına yazılıyor
  (`PlayerSaveData.cs:614-619`); bir sonraki bozulma onu ezer. Göçte istisna durumunda hiçbir şey saklanmıyor.
- **Oyuncuya etkisi.** Bir güncellemedeki göç hatası ya da yarım yazılmış bir kayıt = bütün ilerlemenin (ve satın alınmış elmasların) kaybı; geri getirme yolu yok.
- **Çözüm.** Yükleme sonucu açık durum döndürsün (yok / açıldı / açılamadı). Açılamadıysa bütün kopyalar zaman damgalı dosyalara karantinaya alınır,
  oyun kaydın üstüne **yazmaz**; menü oyuncuya durumu söyler. Sahnelerdeki "veri yoksa yeni oyun" kısayolu yalnız kayıt gerçekten yokken çalışır.
- **Risk / bağımlılık.** Menü akışına dokunur; testle kilitlenmeli.

**K-2 — Göçler orijinal kaydı eziyor** · P0 · Kesin · M
- **Problem.** Her göç adımı `Save(data)` çağırıyor (`PlayerSaveData.cs:679, 687, 713, 731, 744, 756, 765, 772, 785, 795, 802, 810`). `Save`, bir önceki
  JSON'u yedeğe taşır (`:535-536`). İki adımdan sonra yedek de ara sürümdür: göç öncesi kayıt hiçbir yerde kalmaz. Adımların biri hatalıysa geri dönülemez.
- **Çözüm.** Göç bellekte çalışır, sonunda tek kayıt; göçten önce orijinal JSON `save.v<N>.premigration.json` olarak saklanır (son iki tanesi tutulur).
- **Zorluk** M; her sürüm için sabit kayıt örneği testi gerekir.

**K-3 — Dosya yazımı atomik değil; yarım kalan geçici dosya kullanılmıyor** · P1 · Kesin · S
- `WriteFileAtomic` eski dosyayı **siliyor**, sonra geçici dosyayı taşıyor (`PlayerSaveData.cs:552-554`). İkisinin arasında uygulama kapanırsa `save.json`
  yoktur; geçici dosya (sağlam olduğu hâlde) okunmaz (`ReadFile`, `:562-566`). Asıl kopya PlayerPrefs'te olduğu için tek başına veri kaybı değil, ama
  üç kopyalı tasarımın bir kopyasını boşa düşürüyor. **Çözüm:** üzerine yazarak taşıma + yüklemede sağlam `.tmp` aday sayılır.

**K-4 — "En yeni kopya" duvar saatine göre seçiliyor** · P2 · Kesin · S
- `lastSavedTicks = DateTime.UtcNow.Ticks` (`:533`), seçim en büyük tick'e göre (`:603-613`). Cihaz saati geri alınmışken yapılan ilk kayıttan sonra
  uygulama kapanırsa, bir sonraki açılışta **eski yedek** "daha yeni" görünür ve bir kayıt geriye düşülür. **Çözüm:** her kayıtta artan sayaç
  (`saveSerial`); seçim önce sayaca, eşitse tick'e bakar.

**K-5 — Gelecek sürümden kayıt ve "boş ama geçerli" JSON** · P2 · Kesin · S
- `saveVersion > CurrentVersion` denetimi yok: eski sürüme dönen oyuncuda (beta, yedekten geri yükleme) bilinmeyen alanlar ilk kayıtta sessizce silinir.
- `TryParse` (`:569-586`) `{}` gibi içeriği geçerli sayar (sürüm 0, bina listesi boş); tek kopya buysa göç zinciri boş veriyle çalışır.
- **Çözüm:** gelecek sürümde kayıttan önce yedek dosya; geçerlilik için en az Ana Üs girdisi şartı.

**K-6 — Tek saat kuralı altı yerde delinmiş** · P1 · Kesin · S
- Ham `DateTime.UtcNow` kullananlar: `PlayerController.cs:39, 58` (maden zaman damgası — `GameManager` aynı alanı `GameClock` ile okuyor, `:236-264`),
  `BattleCards.cs:42` (Kraliyet Üyeliği süresi), `GemEconomy.cs:119` (`Now`, geri sayım yazıları), `DailyRewards.cs:117` (2x maden),
  `BaseMapManager.cs:132, 154` (ağaç/kaya büyümesi), `AwayReport.cs:34`.
- **Etkisi.** Saat geri alınıp oyun açıkken beklenirse maden deposu bir anda dolar (iki saat arasındaki fark "geçen süre" sayılır; depo 4 saatlik
  kapasiteyle sınırlı). Kraliyet Üyeliği satışa çıktığında saat geri alınarak süresi uzatılabilir. D84 "bütün zaman okumaları GameClock" diyordu; altı yer kaçmış.
- **Çözüm:** hepsi `GameClock.UtcNow`; oyun kuralı dosyalarında ham saat kullanımını yakalayan test.

**K-7 — Saat ileri alma** · P2 · Kesin (D84'te kayıtlı) · L
- Sunucu saati olmadan kapatılamaz. Ek gözlem: 7 günden büyük geri sapmada yüksek su işareti sıfırlanıyor (`GameClock.cs:23-27`), yani "8 gün ileri al →
  her şeyi topla → geri al" cezasız. Bulut kaydı/sunucu saatiyle birlikte ele alınmalı; yol haritasında.

**K-8 — Kayıt düz metin, imzasız** · P2 · Kesin (AUDIT S3) · M
- Köklü cihazda ya da yedekten düzenlenebilir. Sunucu olmadan gerçek koruma yok; basit bütünlük imzası caydırır ve **bozulmayı** da yakalar. Bulut kaydıyla birlikte tasarlanmalı.

**K-9 — Ana Üs açıkken 10 saniyede bir tam kayıt** · P2 · Kesin; maliyeti Ölçüm · S
- `PlayerController.Update` (`Assets/Scripts/PlayerController.cs:35-41`): JSON üretimi + `PlayerPrefs.Save()` + dosya yazımı ana iş parçacığında, her 10 sn.
  Kayıt küçük (3–20 KB) ama eşzamanlı disk yazımı düşük cihazda takılma yapabilir. Profiler'da `SaveSystem.Save` süresine bakılmalı (§6).

### 5.2 Hata ve uç durumlar

| # | Bulgu | Kanıt | Önem | Çözüm | Zorluk |
|---|---|---|---|---|---|
| E-1 | **Android geri tuşu yayın derlemesinde hiçbir şey yapmıyor.** Tek işleyici (`SelectionManager.InstallBackKey`) `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` içinde (`Assets/Scripts/Base/BaseSceneSetup.cs:77-81`); Sefer haritasında yalnız açık paneli kapatıyor (`CampaignSceneSetup.cs:72-74`). Savaşta, adada, menüde geri tuşu ölü; çıkış onayı yok. Ayrıca `androidPredictiveBackSupport: 0` ve hedef API 36'da sistem geri hareketi varsayılan olarak yeni yoldan geliyor: Unity'nin tuşu oyuna iletip iletmediği **cihazda doğrulanmalı**. | Kesin (kod) + Ölçüm (API 36 davranışı) | P1 | Tek geri tuşu yöneticisi: açık pencere/seçim → kapat; savaş → duraklat menüsü; kök ekran → çıkış onayı | M |
| E-2 | Kayıt açılamazsa sahneler yeni oyun başlatıyor (K-1'in tetikleyicisi). | Kesin | P0 | K-1 ile | — |
| E-3 | **Savaş ortasında uygulamayı kapatmak can düşürmüyor.** Can yalnız `BattleLost` içinde düşer (`GameManager.cs:1011-1017`); savaş durumu kaydedilmez. Kaybedeceğini gören oyuncu uygulamayı kapatıp canını korur; atölye kartı hakkı ise savaş başında harcandığı için geri gelmez (`BattleCards.cs:241-242`). | Kesin | P2 (tasarım kararı) | Savaş başlarken "sürüyor" işareti kayda; açılışta işaret duruyorsa yenilgi say. Oyun tasarımı kararı → size bırakıldı | S |
| E-4 | **Ödüllü reklam kilidi geri çağrıya bağlı.** `RewardedAds.showing` yalnız geri çağrıda sıfırlanıyor (`Assets/Scripts/Systems/RewardedAds.cs:19-29`); reklam nesnesi geri çağrı olmadan yok olursa (sahne değişimi) reklam düğmeleri uygulama yeniden açılana kadar çalışmaz. Aynı kalıp `IapService.Busy` için: mağaza hiç yanıt vermezse satın alma düğmesi kilitli kalır (`IapService.cs:95-102`). | Muhtemel | P2 | Sahne değişiminde / zaman aşımında sıfırla | S |
| E-5 | IAP işlem defteri 300 girdide kırpılıyor (`IapService.cs:166`). Kuramsal: çok eski bir işlem yeniden bildirilirse ikinci kez elmas verilir. | Kesin (kod), olasılık çok düşük | P3 | Kırpmayı kaldır (girdi ~40 bayt) | S |
| E-6 | Yeni oyun onayı: 3 sn içinde ikinci dokunuş (`IntroSceneSetup.cs:820-826`). Yanlışlıkla çift dokunuş bütün kaydı siler; silinen kayıt saklanmıyor. | Kesin | P1 | Silmeden önce kaydı `save.deleted.json` olarak sakla | S |
| E-7 | Çift dokunuş korumaları: satın alma (`Busy`), günlük ödül, görev, reklam hakları durumdan korunuyor; elmas harcamaları tek kullanımlık onay penceresinden geçiyor. Sahne geçişinde örtü dokunuşu kesiyor. | Kesin | — | Bulgu yok | — |
| E-8 | Düşük bellek: `Application.lowMemory` işleyicisi yok; çalışma anında üretilen doku/sprite önbellekleri hiç boşaltılmıyor. | Kesin (yok), etkisi Ölçüm | P2 | Uyarıda `Resources.UnloadUnusedAssets` | S |
| E-9 | Ekran oranı / çentik: tuvallerin çoğu `ReferenceUiVisuals.SafeArea` kullanıyor; 4 oranda test var. Savaş efekt tuvalleri (`BattleVfxManager.cs:146, 224`, `BattleSceneSetup.cs:118`) 1080×1920 / eşleşme 1 ile ayrı ölçekleniyor (diğerleri 390×844 / 0,5). Geniş tablette dalga afişi farklı boyda görünebilir. | Kesin (kod), görsel etkisi Ölçüm | P3 | Ortak `ConfigureCanvas` | S |
| E-10 | Büyük ekranda yön kilidi: Android 16, ≥600dp ekranlarda yön/boyut kilidini yok sayıyor; oyunlar `appCategory=game` ile muaf. Projede `AndroidIsGame: 1` var; üretilen manifestte `android:appCategory="game"` olduğu AAB'den doğrulanmalı. | Ölçüm | P1 | AAB manifest denetimi (`Tools/check_aab.py`) | S |

---

## 6. Performans riskleri

Masaüstü ölçümü (8. oturum, `P1`): 54 düşman + 4 kule, ortalama 16,7 ms (vsync tavanı). **Cihaz ölçümü yok.** Aşağıdakiler koddan kesin; kare süresine
etkileri ölçülmeden P2'den yukarı çıkarılmadı.

| # | Bulgu (koddan kesin) | Yer | Çözüm | Zorluk |
|---|---|---|---|---|
| P-1 | Kule hedefi yokken **her karede** `FindObjectsByType<EnemyController>` (kule başına dizi ayırır) | `Assets/Scripts/Battle/TowerController.cs:134, 161` | Statik düşman kaydı (zaten `laneEnemies` var) ya da 0,15 sn kısıtı | S |
| P-2 | Kahraman her karede aynı tarama | `Assets/Scripts/Battle/HeroController.cs:240, 499` | aynı | S |
| P-3 | Ekran dışı göstergeler her `LateUpdate`'te tarama + her karede metin birleştirme | `Assets/Scripts/Polish/Battle/BattleOffscreenIndicators.cs:38, 78` | Kayıt + değer değişince yaz | S |
| P-4 | Müttefik mızrakçı hedefsizken her karede tarama | `Assets/Scripts/Battle/AllyLancer.cs:177, 212` | aynı | S |
| P-5 | Boş her yuva her karede `FindFirstObjectByType<HeroController>`; boss'lar ve Gölge Kurt da | `BattleBuildSlot.cs:54`, `ShadowWolfBehaviour.cs:39`, `RegionBoss.cs:80`, `ForestWitchBoss.cs:85`, `DragonBoss.cs:146` | `EnemyController.CachedHero` kalıbını ortaklaştır | S |
| P-6 | **Her ok atışında yeni doku + sprite + malzeme** (hiç yok edilmiyor; sahne değişimine kadar birikir) ve her atışta yeni nesne | `Assets/Scripts/Battle/ProjectileController.cs:49, 65, 145-153`; `TowerController.cs:177` | Statik paylaşılan sprite/malzeme; mermi havuzu | S / M |
| P-7 | Her düşman can barı için 3 doku üretiyor (yok edilmiyor) | `Assets/Scripts/Battle/HealthBar.cs:122, 130, 136, 225-243` | Statik paylaşılan sprite | S |
| P-8 | Her hasar yazısı için yeni dünya tuvali; hazır `DamageNumberPool` hiç çağrılmıyor | `Assets/Scripts/Battle/BattleVfxManager.cs:31-40, 88`; `Polish/Battle/DamageNumberPool.cs` | Havuzu bağla | M |
| P-9 | Ana Üs doluluğu sorgulanan her karede `FindObjectsByType<NatureObstacle>` ile baştan kuruluyor | `Assets/Scripts/Base/BaseOccupancyGrid.cs:136-139, 173` | Yalnız `MarkDirty` sonrası kur (doğruluk D100'e bağlı; testlerle) | M |
| P-10 | Düşman ayrışması her düşman için bütün listeyi dolaşıyor (n²); dalga ≤ 12 ile sınırlı | `Assets/Scripts/Battle/EnemyController.cs:1004-1044` | Şimdilik gerek yok | — |
| P-11 | Üst bar her karede metin yazıyor | `Assets/Scripts/Polish/UI/BattleTopBar.cs:401-406`, `BattleWorldMarkers.cs:103` | Değer değişince yaz | S |
| P-12 | `Debug.Log` yayında süzülüyor ama derlemeden çıkmıyor; dize birleştirme yine çalışıyor (84 çağrı; sıcak yolda yalnız düşman ölümü `EnemyController.cs:1196`) | `Assets/Scripts/Systems/ReleaseLogging.cs:16-19` | Sıcak yoldakileri koşullu yap | S |
| P-13 | Dokular: en büyük 15'in hepsinde Android'e özel ayar yok; `enemy_troll/wolf/orc.png` ve `mine_stage_*.png` **sıkıştırmasız** (her biri 1,2–1,7 MB kaynak; bellekte 2048² RGBA ≈ 16 MB) | `Assets/Resources/Sprites/*.meta`, `CustomBaseArt/*.meta` | ASTC 6×6 platform ayarı, gerçek boyuta `maxTextureSize` | S (görsel kontrol ister) |
| P-14 | Yalnız 2 sprite atlas (`UIAtlas`, `BaseVisuals`); savaş düşmanları, Tiny Swords karoları atlassız → çizim çağrısı | `Assets/Atlases`, `Assets/Resources` | Savaş atlası | M |
| P-15 | `managedStrippingLevel` ayarsız; `libil2cpp.so` 69,8 MB | `ProjectSettings/ProjectSettings.asset:712` | Orta/Yüksek + tam test turu | M |

**Unity Profiler'da neye bakmalı** (Development APK, cihaz USB ile bağlı, Bölge 5 boss seviyesi, 4 kule kurulu):
1. *CPU Usage → Hierarchy*, sütun **GC Alloc**: `TowerController.Update`, `HeroController.Update`, `BattleOffscreenIndicators.LateUpdate`,
   `ProjectileController`, `BattleVfxManager.ShowDamageNumber`. Kare başına toplam ayırma 1 KB'ın altında olmalı.
2. *CPU Usage → Timeline*: `GC.Collect` sivrileri (hedef: savaş boyunca < 1 / 10 sn) ve `SaveSystem.Save` süresi (Ana Üs'te 10 sn'de bir; hedef < 4 ms).
3. *Rendering*: **Batches / SetPass calls** (hedef < 150 / < 60), *Frame Debugger* ile atlassız sprite'ların böldüğü çağrılar.
4. *Memory → Unity Objects*: `Texture2D` sayısı savaş boyunca artıyor mu (P-6, P-7), toplam doku belleği (hedef < 350 MB).
5. Sahne yükleme: `SceneManager.LoadScene` → ilk kare süresi (hedef < 2 sn; `BaseSceneSetup.Start` kodla kurulum yapıyor).
6. Isınma/pil: 15 dk kesintisiz savaşta kare süresi kayıyor mu.

---

## 7. Kod kalitesi ve teknik borç

| # | Bulgu | Kanıt | Önem | Not |
|---|---|---|---|---|
| Q-1 | Tek derleme birimi; testler yansımayla erişiyor | Kesin | P2 | Oyun koduna asmdef + `InternalsVisibleTo` → testler derleme anında denetlenir. Büyük ama mekanik iş |
| Q-2 | Dev sınıflar (KN-5) | Kesin | P2 | Önce `BaseMapManager`: kayıt ↔ sahne eşitlemesini ayrı sınıfa al |
| Q-3 | `SaveSystem` `PlayerSaveData.cs` içinde, göçler onarımlarla karışık | Kesin | P1 | K-2 ile çözülür |
| Q-4 | Ölü / eski kod derlemede: `UI/MainMenuUI.cs` (hiçbir sahnede yok; "Yeni oyun" onaysız silme içeriyor, `:78-83`), `PlaceholderSpriteGenerator` 785 satır, `DamageNumberPool` (kullanılmıyor), 9 abonesiz olay, eski `builderState/builderTask` alanları | Kesin | P3 | `_Deprecated/` kuralına göre taşınabilir |
| Q-5 | Hata yönetimi: yalnız 10 `try/catch`; reklam ve bildirim kodunda hiç yok. Sessiz yutanlar: kayıt dosyası okuma (`PlayerSaveData.cs:565`), IAP doğrulama (`IapService.cs:352-355`) | Kesin | P2 | Kayıt tarafı K-1 ile; bildirim çağrıları sarılmalı (izin reddi, üretici kısıtları) |
| Q-6 | Yerelleştirilmemiş metinler: `IntroSceneSetup.cs:858, 860, 914, 925, 1104`, `BaseSceneSetup.cs:308`, `BattleSceneSetup.cs:175-186` (eski kahraman seçimi ekranı; bugün gösterilip gösterilmediği doğrulanmadı) | Kesin (metin), görünürlük Muhtemel | P3 | İngilizce mağaza sayfası açılacaksa P2 |
| Q-7 | Çöp ve kalıntılar: `Assets/ExternalDependencyManager` + `GvhProjectSettings.xml` (Firebase kalıntısı), 5 adet `Assets/InitTestScene*.unity`, git dışı iki varlık paketi (`Dragon Warrior Files`, `Mighty Heroes`), depo dışı büyüklük: `Builds` 3,4 GB, `PlaytestOutput` 1,2 GB | Kesin | P3 | Lisans: Dragon Warrior kullanılıyor (`Resources/Dragon`); Mighty Heroes kullanılmıyor (bellek notu) |
| Q-8 | Çalışma ağacında kaydedilmemiş `ProjectSettings.asset` değişikliği (`AndroidTargetSdkVersion 36 → 0`, yığın izi türleri) | Kesin | P3 | Test APK betiği yazıyor; yayın betiği derlemede 36'ya çekiyor. Zararsız ama kafa karıştırıcı |
| Q-9 | Ada kilidi iki sabitte; Manastır yorumunda "seviye 4" yazıyor, kural 6 (`PlayerSaveData.cs:184`, `GameData.cs:921`) | Kesin | P3 | — |

---

## 8. Oyuncu deneyimi bulguları

Kaynak: otomatik baştan oynatma (`Docs/playtest/session6/journey/journey.md`), 90 savaşlık denge ölçümü (REPORT §2), ECONOMY.md simülasyonu, kod.
**Cihazda gerçek oyuncu gözlemi yok**; aşağıdakiler ölçüm ve koddan çıkarımdır.

| # | Bulgu | Kanıt | Önem | Öneri |
|---|---|---|---|---|
| O-1 | **İlk öğretici adımı boşa çıkıyor.** Rehber "Madene dokun ve biriken altını topla" diyor (`Assets/Scripts/Base/FirstBaseGuide.cs:15`); yeni hesapta maden deposu **0**. `CreateNew` önce 40 yazıyor ("rehberin 'madene dokun' adımı boşa çıkmasın" yorumuyla, `PlayerSaveData.cs:140-141`), 55 satır aşağıda 0 ile eziyor (`:196`). Baştan oynatma kaydı doğruluyor: "[0 dk] … maden deposu 0". | Kesin | P1 | İkinci atamayı kaldır (uygulandı, §12) |
| O-2 | **11. dakikada duvar.** Bot B1-4'ü (Zamana Karşı) art arda 4 kez kaybediyor; bu arada 3 can gidiyor ve ilk oturum can bitimine yaklaşıyor. Denge ölçümünde aynı seviye sınırda (üs %42). İlk oturumda ilk yenilgi iyi, art arda dört yenilgi bırakma nedenidir. | Ölçüm (bot; tek tohum) | P1 | REPORT §2.2'deki öneriler (karar sizde): Zamana Karşı sayacı dalga aralarında dursun; B1 seviyelerinde ilk 2 yenilgi can düşürmesin |
| O-3 | Yenilgi sonrası yönlendirme var ("Üsse dön, güçlendir", Ana Üs ≤ 4) ama oyuncu **neyi** güçlendireceğini bilmiyor; rozet yalnız "kurulabilecek yeni bina" için yanıyor (`BaseBadges.CanBuildNew`), yükseltilebilecek kahraman/bina için değil. | Kesin (kod) | P2 | Yükseltme rozeti + yenilgi ekranında tek somut öneri |
| O-4 | **İçerik 3,5 günde bitiyor.** Simülasyon: Ana Üs 15'e ~3,5. günde ulaşılıyor, 7. günde her şey tavanda (ECONOMY §11.3). Sonrasında geri getiren döngüler: günlük takvim, 3 görev, Sonsuz Sefer, hafta sonu İstilası, ada baskını. Uzun vadeli hedef (sezon, lig, koleksiyon) yok. | Ölçüm (sim) | P1 (tutma) | Yayın sonrası ilk içerik: Sonsuz Sefer skor tablosu ya da sezon ödülü. Strateji kararı → size bırakıldı |
| O-5 | Zorluk eğrisi ters: 1–3. bölgeler sınırda/duvar, 4–6. bölgeler yalnız kuleyle bile kazanılıyor (REPORT §2.1). Hedef "ilk bölgeler affedici, 4–6 planlama ister" idi. | Ölçüm (bot) | P1 | REPORT §2.2 kararları + 4–6. bölge can çarpanı |
| O-6 | Ekonomi: V2'de altın değerli (cüzdan tavanında geçen süre ≈ %0, sıradaki Ana Üs ≈ 20 savaş). İlk saatte Ana Üs 5 ve ada açılıyor. | Ölçüm (sim) | — | Bulgu yok |
| O-7 | Para kazanma algısı: elmasla süre atlama, kaynak paketi, savaş içi devam. Rekabet/PvP olmadığı için "öde-kazan" baskısı düşük. Risk: "Ana Üs yıkılınca 25 elmasla devam" + "zaferde 2x reklam (sınırsız)" birlikte ödeyenin ilerlemesini çok hızlandırıyor; skor tablosu eklenirse bu ikisi rekabet dışı tutulmalı (kartlar için `Competitive` bayrağı zaten var). | Muhtemel | P2 | Skor tablosuyla birlikte ele al |
| O-8 | Deneme reklam ekranı oyuncuya "Deneme reklamı (gerçek reklam henüz bağlı değil)" yazıyor (`RewardedAds.cs:51`). Kapalı testte kabul edilebilir, üretimde değil. | Kesin | P0 (üretim için) | Y-1 |
| O-9 | Bildirimler iOS'ta hiç yok (inşaat bitti, canlar doldu, maden doldu — geri getiren en ucuz araç). | Kesin | P1 (iOS) | Y-12 |

---

## 9. Yayın hazırlığı

Kurallar 15 Kasım 2026'da resmî sayfalardan doğrulandı (Sonnet alt ajanı; kaynak bağlantıları her maddede). "Doğrulanamadı" yazanlar resmî sayfada
bulunamayan ya da açılamayan bilgilerdir; ezberden tamamlanmadı.

### 9.1 Google Play

**Kod tarafında**

| # | Madde | Durum | Önem | Kaynak / not |
|---|---|---|---|---|
| Y-0 | Hedef API 36, AAB, yalnız ARM64, IL2CPP | ✅ `Assets/Editor/AndroidReleaseBuild.cs:19-48` | — | 31 Ağustos 2026'dan beri yeni uygulama ve güncellemeler API 36 ([Play yardım](https://support.google.com/googleplay/android-developer/answer/11926878)) |
| Y-1 | **Ödüllü reklamlar deneme ekranı** ve yayın derlemesinde de ödül veriyor (`RewardedAds.cs:15-16, 63-64`) | 🔧 | **P0 (üretim)**; kapalı testte engel değil | Karar sizde: (a) SDK gelene kadar yayın derlemesinde reklam düğmelerini gizle, (b) AdMob bağla. (b) için AdMob uygulama kimliği gerekir |
| Y-2 | **İzin akışı (UMP) yok** | 🔧 | **P0, reklamla birlikte** | AEA/BK/İsviçre'de kişiselleştirilmiş reklam için Google onaylı CMP zorunlu ([AdMob](https://support.google.com/admob/answer/13554116)). KVKK için Google belgesinde ayrı kural **doğrulanamadı** |
| Y-3 | **Makbuz anahtarı (`GooglePlayTangle`) üretilmemiş**: yayın derlemesi gerçek satın almada elmas vermez (`IapService.cs:332-340`, bilinçli) | 👤 | **P0 (satın alma açılacaksa)** | PLAY_RELEASE §9.3 |
| Y-4 | Play Billing: Unity IAP 5.4.0 → Billing Library 9. Şu an en az 7; 31 Ağustos 2027'de en az 8 | ✅ | — | [Billing SSS](https://developer.android.com/google/play/billing/deprecation-faq), [Unity IAP 5](https://docs.unity.com/en-us/iap/upgrade-to-iap-v5). Bekleyen/ertelenen satın alma işleniyor, 3 gün içinde onay kuralına uygun |
| Y-5 | 16 KB sayfa boyutu: LOAD hizası 7/7 `.so` uygun (14 Kasım AAB'si). **Eksik denetim: GNU_RELRO hizası.** Unity 6000.3.x'te `libmain.so` RELRO hizası için hata kaydı ve Play reddi bildirimi var (forum; düzeltme sürümü **doğrulanamadı**). 1 Şubat 2027'den sonra uyumsuz güncelleme yayımlanamaz | 🔧 | **P0 (doğrulanana dek)** | [Android](https://developer.android.com/guide/practices/page-sizes). `Tools/check_aab.py`'ye RELRO denetimi eklenmeli; gerekirse 6000.3'ün son yamasına geçiş (Proje Ayarı/sürüm değişikliği → sizin onayınız) |
| Y-6 | Geri tuşu / tahmini geri hareketi | 🔧 | P1 | E-1. API 36'da `KEYCODE_BACK` varsayılan olarak iletilmiyor ([Android 16](https://developer.android.com/about/versions/16/behavior-changes-16)); Unity ayarı `androidPredictiveBackSupport: 0` |
| Y-7 | Kenardan kenara zorunlu (API 36'da kapatılamıyor) | ✅ muhtemel | P1 | `androidRenderOutsideSafeArea: 1` + güvenli alan bileşeni; cihazda gezinme çubuğu altında düğme kalıyor mu bakılmalı |
| Y-8 | Büyük ekranda yön kilidi (oyun muafiyeti) | 🔧 doğrula | P1 | E-10 |
| Y-9 | Bildirim izni (Android 13+), kesin alarm izni kullanılmıyor | ✅ | — | `GameNotifications.cs:107-123`; planlama kesin olmayan alarm |
| — | Ayarlar'daki gizlilik URL'si ve destek e-postası yer tutucu (`AudioSettingsUI.cs:16-18`: `example.com`) | 👤→🔧 | **P0** | Gizlilik politikası uygulama içinden de erişilebilir olmalı ([Play](https://support.google.com/googleplay/android-developer/answer/9859455)) |
| — | Ürün adı `MiniEmpireBuilder`, şirket `DefaultCompany`, uygulama simgesi | 👤→🔧 | **P0** | PLAY_RELEASE §2 |
| — | Uygulama içi "verilerimi sil" | gerekmez | — | Hesap yok → hesap silme şartı uygulanmaz; formdaki veri silme soruları yine yanıtlanır ([Play](https://support.google.com/googleplay/android-developer/answer/13327111)) |

**BENİM yapmam gerekenler (Google Play)**
1. Paket adını onayla (`com.kingdomforgegames.minikingdombuilder` — ilk yüklemeden sonra değişmez); ürün ve şirket adını ver.
2. Yükleme anahtarı oluştur, sakla; Play App Signing'i kabul et (PLAY_RELEASE §4).
3. Gizlilik politikasını herkese açık bir https adresinde yayımla (PDF olmaz); adresi ve destek e-postasını bana ver.
4. Kapalı test: en az 12 test kullanıcısı, kesintisiz 14 gün; sonra üretim erişimi başvurusu ([kural](https://support.google.com/googleplay/android-developer/answer/14151465)).
5. Uygulama içeriği formları: gizlilik politikası, reklam beyanı, hedef kitle (13+ önerisi: PLAY_RELEASE §7), içerik derecelendirmesi (IARC),
   veri güvenliği (DATA_SAFETY.md; reklam eklenince yeniden), **finansal özellikler beyanı** ("finansal özellik yok" — herkes için zorunlu), reklam kimliği beyanı.
6. **Tüccar durumu beyanı** (AB DSA): bütün hesaplar tüccar / tüccar değil beyan etmeli; tarih kaynaklarda çelişkili (Kasım 2026 ↔ 1 Şubat 2027) → şimdi yap.
   Tüccar beyanında adres ve telefon herkese açık görünür.
7. Ödeme profili: banka hesabı satıcının ülkesinde olmalı. Türkiye vergi ayrıntıları resmî sayfada **doğrulanamadı**.
8. Mağaza varlıkları: simge 512×512 PNG (≤ 1024 KB), öne çıkan görsel 1024×500, telefon ekran görüntüsü 2–8 adet (her kenar 320–3840 px, uzun kenar ≤ 2× kısa),
   kısa açıklama 80 karakter ([kural](https://support.google.com/googleplay/android-developer/answer/9866151)). Ekran görüntüleri yayın derlemesinden alınmalı.
9. Satın alma açılacaksa: 3 ürünü Play Console'da tanımla, lisans test hesabı ekle, makbuz anahtarını üret (PLAY_RELEASE §9).
10. Reklam açılacaksa: AdMob hesabı, uygulama ve ödüllü birim kimlikleri.

### 9.2 App Store

**Kod tarafında**

| # | Madde | Durum | Önem | Kaynak / not |
|---|---|---|---|---|
| Y-10 | **iOS paket kimliği tanımlı değil** (`applicationIdentifier` altında yalnız Android), imza/takım kimliği boş | 🔧👤 | **P0** | `ProjectSettings/ProjectSettings.asset:169-170` |
| Y-11 | **Derleme hattı yok** (Mac yok, iOS derleme betiği yok). Yüklemeler Xcode 26 / iOS 26 SDK ile yapılmalı (28 Nisan 2026'dan beri) | 🔧👤 | **P0** | [Apple yaklaşan şartlar](https://developer.apple.com/news/upcoming-requirements/). Bulut derleme (Unity Build Automation) iOS derliyor; TestFlight'a yükleme ve Mac'siz kurulum **doğrulanamadı** |
| Y-12 | **iOS bildirim kodu yok** (`GameNotifications` yalnız Android; iOS'ta sessizce hiçbir şey yapmaz) | 🔧 | P1 | `Assets/Scripts/Systems/GameNotifications.cs:5, 74-123`. Paket iOS'u destekliyor; yetki isteği + planlama eklenmeli |
| Y-13 | Gizlilik bildirimi (privacy manifest): Unity motoru kendi gerekçelerini otomatik ekliyor; Unity IAP ve Services Core paketleri kendi dosyalarını taşıyor. Projenin kendi dosyası yok; oyun kodu `PlayerPrefs` (UserDefaults, gerekçe CA92.1) ve dosya zaman damgası dışında "gerekçe isteyen API" kullanmıyor | ✅ muhtemel | P1 doğrula | [Unity](https://docs.unity.com/en-us/engine/current/manual/platform-specific/iphone/ios-building-and-delivering/apple-privacy-manifest-policy), [Apple SDK listesi](https://developer.apple.com/support/third-party-SDK-requirements/). İlk Xcode çıktısında birleşik dosya kontrol edilmeli. Google Mobile Ads'in kendi dosyası **doğrulanamadı** |
| Y-14 | App Tracking Transparency + SKAdNetwork kimlikleri | reklamla birlikte | **P0, reklamla** | Kılavuz 5.1.2; AdMob iOS kurulumu `SKAdNetworkItems` ve `NSUserTrackingUsageDescription` ister ([AdMob iOS](https://developers.google.com/admob/ios/quick-start)) |
| Y-15 | Satın alma: yalnız tüketilebilir ürün; Unity IAP 5.4 StoreKit 2 kullanıyor. "Satın alımları geri yükle" düğmesi yalnız geri yüklenebilir ürünler için (kılavuz 3.1.1) → elmas paketleri için gerekmez (**çıkarım**). İleride Reklamsız / Kraliyet Üyeliği eklenirse **zorunlu** | ✅ | — | [Kılavuz](https://developer.apple.com/app-store/review/guidelines/). iOS'ta yerel makbuz doğrulaması yapılmıyor (`IapService.cs:335`); StoreKit 2 işlemleri sistemce imzalı doğrulanır, sunucu doğrulaması yok |
| Y-16 | Şifreleme beyanı: `ITSAppUsesNonExemptEncryption` Info.plist'te yok | 🔧 | P2 | Her yüklemede sorulur; yalnız HTTPS kullanan oyun için `false` yazmak olağan uygulama (Apple sayfasında bu cümle **doğrulanamadı**) |
| Y-17 | Güvenli alan / ana ekran çubuğu | ✅ muhtemel | P1 doğrula | `Screen.safeArea` her tuvalde; iPhone'da alt çubukla düğme çakışması cihazda bakılmalı |
| Y-18 | En düşük iOS 15.0 | ✅ | — | Apple alt sınırı iOS 13; StoreKit 2 iOS 15+ |
| — | Ayarlar'da gizlilik politikası bağlantısı (yer tutucu) | 🔧 | **P0** | Kılavuz 5.1.1: bağlantı hem mağaza bilgisinde hem uygulama içinde |

**BENİM yapmam gerekenler (App Store)**
1. Paket kimliğini belirle, Apple Developer'da App ID + dağıtım sertifikası + sağlama profili oluştur.
2. Derleme yolunu seç: Unity Build Automation ya da Codemagic (ikisi de bulutta Mac kullanır). İmza dosyalarını o servise yükle. Alternatif: kiralık bulut Mac.
3. App Store Connect: uygulama kaydı, **Ücretli Uygulamalar Sözleşmesi**, banka ve vergi bilgileri (Türkiye için vergi kimlik no + vergi dairesi), Küçük İşletme Programı (%15) başvurusu.
4. **Yaş derecelendirme anketi** (yeni 4+/9+/13+/16+/18+ sistemi; yanıtlanmamış anket güncellemeyi engeller). Çocuk kategorisini seçme, "çocuklar için" ifadeleri kullanma.
5. **Gizlilik etiketi** (App Privacy): bugünkü sürüm için "veri toplanmıyor"; reklam eklenince Tanımlayıcılar / Kullanım Verisi / Tanılama + izleme beyanı.
6. Gizlilik politikası URL'si (zorunlu), destek URL'si.
7. AB'de dağıtım için **tüccar durumu** beyanı (yoksa uygulama AB mağazalarından kaldırılır).
8. Ekran görüntüleri: iPhone 1179×2556 ya da 1206×2622 (1–10 adet); iPad desteklenecekse 2064×2752 ya da 2048×2732. Ad ≤ 30, açıklama ≤ 4000 karakter, anahtar sözcükler ≤ 100 bayt.
9. Uygulama içi ürünleri App Store Connect'te tanımla (aynı kimlikler: `gems_small/medium/large`), sandbox test hesabı aç.
10. TestFlight ile iPhone'da tam tur (güvenli alan, bildirim izni, satın alma, arka plana alma).

### 9.3 Toplanan veri envanteri (her iki mağaza formu için)
| Veri | Bugün | Reklam (AdMob) eklenince | Bulut kaydı eklenince |
|---|---|---|---|
| Oyun ilerlemesi, ada adı | Yalnız cihazda | — | Sunucuya gider (hesap kimliğiyle) |
| Satın alma geçmişi | Mağaza işler; oyunda yalnız işlem kimliği (cihazda) | — | — |
| Reklam kimliği, IP, yaklaşık konum | Yok | Toplanır ve Google ile paylaşılır | — |
| Uygulama etkileşimi, tanılama | Yok (Unity Analytics, çökme raporu kapalı) | AdMob toplar | — |
Ayrıntı: DATA_SAFETY.md. AdMob'un resmî beyan sayfası: [Play veri beyanı](https://developers.google.com/admob/android/play-data-disclosure).

---

## 10. Önceliklendirilmiş yol haritası

| Sıra | İş | Bulgular | Önem | Zorluk | Durum |
|---|---|---|---|---|---|
| 1 | Kayıt yükleme hatasında üstüne yazmama + karantina | K-1, E-2, E-6 | P0 | M | bu oturum |
| 2 | Sürümlü göç hattı + göç öncesi yedek + göç testleri | K-2, K-5, KN-3, KN-4d | P0 | M | bu oturum |
| 3 | Atomik dosya yazımı, kayıt sayacı | K-3, K-4 | P1 | S | bu oturum |
| 4 | Tek saat | K-6 | P1 | S | bu oturum |
| 5 | Kilit ve tür seviyesi yüklemede kuraldan + değişmezlik denetimi | KN-1a, KN-1b | P1 | M | bu oturum |
| 6 | Geliştirici ayarı kural değiştirmez | KN-4a | P1 | S | bu oturum |
| 7 | Yeni hesapta maden altını | O-1 | P1 | S | bu oturum |
| 8 | Geri tuşu yöneticisi | E-1, Y-6 | P1 | M | bu oturum |
| 9 | AAB denetimine RELRO + manifest kontrolleri | Y-5, E-10 | P0 doğrulama | S | bu oturum |
| 10 | Gizlilik URL'si, ürün adı, simge, imza | — | P0 | S | **sizde** |
| 11 | Reklam kararı: gizle ya da AdMob + UMP (+ iOS ATT) | Y-1, Y-2, Y-14 | P0 (üretim) | L | **karar sizde** |
| 12 | Makbuz anahtarı | Y-3 | P0 (satın alma) | S | **sizde** |
| 13 | iOS: paket kimliği, bulut derleme, bildirimler, TestFlight turu | Y-10…Y-17 | P0 (App Store) | L | sizin hesap adımlarınızdan sonra |
| 14 | Denge: REPORT §2.2 dört öneri + ilk oturum duvarı | O-2, O-5 | P1 | S | **karar sizde** |
| 15 | Cihazda Profiler turu, ardından P-1…P-8 | §6 | P1/P2 | M | ölçümden sonra |
| 16 | `BaseMapManager.SaveAll` eksik sahne koruması | KN-1d | P1 | S | incelenecek |
| 17 | Savaş ortasında kapatma, reklam/satın alma kilidi zaman aşımı | E-3, E-4 | P2 | S | — |
| 18 | Doku sıkıştırma, savaş atlası, kod ayıklama düzeyi | P-13…P-15 | P2 | M | — |
| 19 | Oyun koduna asmdef, dev sınıfların bölünmesi | Q-1, Q-2 | P2 | L | — |
| 20 | Bulut kaydı + sunucu saati + kayıt imzası | K-7, K-8 | P2 | L | hesap gerekir |
| 21 | Uzun vadeli tutma içeriği | O-4 | P1 (ürün) | L | **karar sizde** |
| 22 | Temizlik: ölü kod, kalıntı dosyalar, yerelleştirme | Q-4, Q-6, Q-7 | P3 | S | — |

---

## 11. İkinci tur: elenen ve düşürülen bulgular

Taslaktan sonra her bulguya "gerçekten sorun mu, daha basit açıklaması var mı, önerim neyi bozar" diye yeniden baktım.

| Aday bulgu | Karar | Gerekçe |
|---|---|---|
| Sahne nesnesi tutan statik alanlar (`BaseOccupancyGrid.camp`, `EnemyController.cachedHero`) sahne değişiminde bayat kalır | **Elendi** | Kod `!= null` ile denetliyor; Unity yok edilmiş nesneyi null sayar ve yeniden arar (`BaseOccupancyGrid.cs:398`, `EnemyController.cs:598`) |
| Aboneliği bırakılmayan olaylar (`BattleUI`, `IslandHud`, `RaidBoat`…) bellek sızdırır | **Elendi** | Yayıncı ve abone aynı sahnede birlikte yok oluyor; kalıcı nesnelere (GameManager) abonelikler eşli |
| `BattlePause` duraklatması sahneler arasında takılı kalır | **Elendi** | `BattlePauseWatcher.OnDestroy` → `ResetAll` (`BattlePause.cs:88`) |
| `BattleCards.unlocked` yeni oyunda sıfırlanmıyor | **Düşürüldü (P3)** | Savaş başında ve sonunda sıfırlanıyor (`:244, :295`); kalan pencere yalnız "kart seçip savaşa girmeden yeni oyun" |
| Ana Üs tuvalinde güvenli alan nesnesi yok | **Elendi** | `DarkFantasyHudPolish` / `BaseHudRedesign` çalışma anında oluşturuyor; `FirstBaseGuide` ve yerleşim testleri bu nesneyle geçiyor |
| iOS'ta makbuz doğrulanmadan elmas veriliyor | **Düşürüldü** | StoreKit 2 işlemleri sistem tarafından imzalı doğrulanıyor; yerel doğrulama Android'e özgü bir gereksinim |
| Sahte mağaza yayın derlemesine giriyor | **Düşürüldü (P3)** | Kod derlemede var ama `UseFakeStore` yalnız editörde ya da `ForceFake` ile açılıyor; `ForceFake`'i hiçbir oyun kodu açmıyor |
| Unlock tablosundaki tamamlanmamış bina savaşta kullanılabiliyor | **Elendi** | `GetBattleBuildAllowance` → `BuildingLevels.CompletedCount` (`BattleManager.cs:1034-1038`); tür seviyesine düşen yol yalnız hak 0 iken ulaşılmaz |
| Düşman ayrışması n² | **P2'de bırakıldı, işlem yok** | Dalga ≤ 12; ölçüm sorun göstermedi |
| Her karede doluluk yeniden kurulumu performans sorunu | **Ölçüm'e çevrildi** | D100'ün doğruluk güvencesi bu; kaldırmak hata sınıfını geri getirebilir. Önce ölçülmeli |
| Kayıt sıklığı (10 sn) veri kaybı riski | **Tersine çevrildi** | Sık kayıt güvenlik için iyi; tek risk takılma (K-9, ölçüm) |
| "Raporda Ana Üs akın haritası eski görüntü hâlâ riskli" | **Elendi** | Akın kodu canlı kaydı okuyor; kopya yok |

**Emin olmadıklarım:** (1) API 36'da Unity 6000.3.9'un geri tuşunu oyuna iletip iletmediği; (2) 6000.3.9 `libmain.so` RELRO hizası; (3) deneme
reklamının ve sahte kahraman seçimi metinlerinin yayın derlemesinde gerçekten göründüğü ekranlar; (4) bütün performans maddelerinin cihazdaki etkisi;
(5) `BaseMapManager.SaveAll`'un eksik kurulmuş sahnede çağrılma olasılığı (KN-1d). Tablet kaydınıza erişimim yok: göç testleri editördeki kayıt ve
elle kurulmuş eski sürüm örnekleriyle yapıldı.
