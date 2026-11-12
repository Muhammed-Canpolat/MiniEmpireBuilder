# Kart Atölyesi / Card Workshop (hedef zorluk: ORTA)

Kaynak karar: `Docs/design/island_base.md` §11 madde 5. Bu belge Faz 3b'nin ayrıntılı tasarımıdır.
Sayılar uygulamada **yalnız** `Progression.Workshop` (`Data/GameData.cs`) ve `GemEconomy` içinde tutulur.

## 1. Amaç ve ilkeler
- **Et'e anlamlı bir harcama yeri açmak** ve Et'i altından belirgin biçimde kıt tutmak (hedef: günlük altın:Et ≥ 4:1).
- Kartları reklam izlemeden, **planlayarak** kazanmak. Reklam, jeton ve Kraliyet yolları **olduğu gibi kalır**.
- Okunaklılık: 1 savaş = 1 hak. Kart rozetinde "×3" yazar. Süre dolması ve duraklatma yoktur.
- Kazanmak için ödeme yok: elmas yalnız **süreyi** kısaltır (mevcut `GemEconomy.FinishCost`). Et elmasla alınamaz.
- ORTA: Bölge 1–2 kartsız da rahat geçilir. Atölye kartları Bölge 3+ planlamasına yardım eder, onun yerini almaz.

## 2. Açılış ve bina
- **Görsel:** Tiny Swords `Buildings/<Renk> Buildings/House2.png` (5 renk mevcut). Ana Üs renk kademesine uyar
  (`Progression.LevelsPerColorTier = 3`, `GameData.cs:306`).
- **Açılış:** Ana Üs 5 (= `Progression.Island.UnlockMainBaseLevel`). Ada açılınca "İnşa Et > Yapı" listesinde görünür.
  Adet 1. İnşa bedeli 150 altın, anında (sv 1). Ana Üs inşaatçısını kullanır (diğer Ana Üs binaları gibi).
- **Seviye tavanı:** Ana Üs seviyesi (en çok 15). Yükseltme yalnız **altın** ile yapılır; Et kart para birimi olarak kalır.
- **Bedel:** `UpgradeCost(CardWorkshop, L)`, çarpan **0,45** (Okçu 0,5'in biraz altı). Süre: `TimingConfig` tablosu
  olduğu gibi (`TimingConfig.cs:13`). İlk seviyeler kısa sürer, geç seviyeler uzar.

| Hedef sv | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Altın (≈) | 70 | 110 | 180 | 270 | 380 | 520 | 680 | 860 | 1080 | 1350 | 1660 | 2020 | 2430 | 2920 |
| Süre | 10 sn | 1 dk | 3 dk | 6 dk | 10 dk | 15 dk | 30 dk | 1 sa | 2 sa | 3 sa | 4 sa | 6 sa | 8 sa | 12 sa |

**Seviyenin getirdikleri.** Her renk kademesi (3 seviye) +1 hak verir. Bu, görselden okunabilir.

| Atölye sv | 1–3 | 4–6 | 7–9 | 10–12 | 13–15 |
|---|---|---|---|---|---|
| Üretim başına hak `UsesPerCraft = 1 + (L−1)/3` | 1 | 2 | 3 | 4 | 5 |
| Açılan kademe | Sıradan (sv1) | Nadir (sv3) | Destansı (sv7) | — | — |
| Üretim kuyruğu | 1 | 2 (sv6) | 2 | 2 | 2 |
| Savaşta atölye özel kart yuvası | 1 (sv3) | 1 | 2 (sv7) | 2 | 2 |

## 3. Kart kataloğu
Mevcut 3 kart **Sıradan** kademedir. Bonusları değişmez (%10, Kraliyet %15). Yeni 4 kart enum'un **sonuna** eklenir.
Hiçbirinde başlangıç altını yok: `GameData.cs:278` "kart/Kraliyet/elmas başlangıç altınını etkilemez" der.

| # | Kart (TR / EN) | Kademe | Etki (yalnız o savaş) | Bağlantı noktası |
|---|---|---|---|---|
| 0 | Hasar / Damage | Sıradan | Savaşçı hasarı +%10 | `HeroController.cs:80` |
| 1 | Can / Health | Sıradan | Savaşçı canı +%10 | `HeroController.cs:82` |
| 2 | Kale / Fort | Sıradan | Kule ve Ana Üs canı +%10 | `BattleStructure.cs:41`, `MainBaseController.cs:38` |
| 3 | Keskin Oklar / Sharp Volleys | Nadir | Tüm kule hasarı +%15 | `Progression.TowerDamage` (`GameData.cs:289`) çağrıları |
| 4 | Zırh Delici / Armor Breaker | Nadir | Kulelerin Zırhlı'ya hasarı ×1,4 (Okçu 0,35→0,49, Topçu 0,6→0,84) | `EnemyController.SourceMultiplier` (`EnemyController.cs:98`) |
| 5 | Gök Avcısı / Sky Hunter | Nadir | Okçu kulelerinin Uçan'a hasarı +%50 (Topçu uçanı vuramaz, `TowerController.cs:151`) | aynı yer |
| 6 | Kalkan Kıran / Ward Breaker | Destansı | Boss Kalkanı havuzu −%30; açık kalma süresi 6→8 sn | `EnemyController.cs:173`, `:217` |

- Karşı kartlar Zırhlı/Uçan kimliğini korur: Zırh Delici'yle de Topçu doğru cevaptır (Okçu 0,49'da kalır).
- Kalkan Kıran yalnız kalkanlı bosslarda işe yarar. Ön-savaş ekranı, kalkansız aşamada kartı soluk gösterir ve hak harcamaz.
- **Reddedilenler:** başlangıç altını (yukarıdaki karar). Kahraman/çırak yetenek bekleme süresi (kodda böyle bir sistem yok).
  "Son Siper" (Ana Üs'ü bir kez onarma), elmasla/reklamla devam (`GemEconomy.ContinueCost`) ile çakışıyor.

## 4. Üretim bedeli ve süresi
`Bedel(kademe, L) = taban × (1 + 0,15(L−1))`, 10'a yuvarlanır. Hak sayısı daha hızlı arttığı için **hak başına bedel düşer**
(atölye yükseltmenin ödülü budur). Süre seviyeyle değişmez: seviye hak verir, süre vermez.

| Kademe | Taban Et / Altın | Süre | sv1 | sv4 | sv7 | sv10 | sv13 | Stok tavanı (hak) |
|---|---|---|---|---|---|---|---|---|
| Sıradan | 40 / 80 | 5 dk | 40/80 | 60/120 | 80/150 | 90/190 | 110/220 | 12 |
| Nadir | 120 / 200 | 30 dk | — | 170/290 | 230/380 | 280/470 | 340/560 | 8 |
| Destansı | 300 / 500 | 2 sa | — | — | 570/950 | 710/1180 | 840/1400 | 5 |

- **İlk üretim bedava:** Et 0, altın 0, 30 sn (öğretici; mevcut "ilk savaşta bedava kart" kalıbı). Kayıt bayrağıyla tek seferlik.
- **Stok tavanı:** stok + üretilecek hak tavanı aşacaksa "Üret" düğmesi kapalıdır ("Depo dolu"). Hiçbir şey boşa gitmez.
- Biten üretim stoğa **kendiliğinden** girer (çevrimdışı da). Bina üstünde "yeni kart" rozeti çıkar.
  Hak sayısı üretim başlarken kilitlenir; üretim sırasında seviye atlamak bu üretimi değiştirmez.
- Atölyenin kendi ustası var. Ana Üs inşaatçısını meşgul etmez.

## 5. Hızlandırma: reklam ve elmas (benim kararım)
- **Ödüllü reklam:** üretimi **anında** bitirir, kademe fark etmez. Günde **3** (`GemEconomy.DailyWorkshopAds = 3`),
  sayaç `AdRewards` içinde gün damgasıyla tutulur. İnşaatın −30 dk hakkından ayrıdır.
- **Elmas:** yalnız kalan süre için, mevcut `GemEconomy.FinishCost` ile (5 dk ≈ 5, 30 dk ≈ 12, 2 sa ≈ 24). Bitirme onayı
  `GemConfirm` ile yapılır. 1 dk'nın altı bedavadır. **Eksik Et elmasla tamamlanamaz.** Adadaki "1 elmas = 25 kaynak" kuralı
  (`island_base.md:272`) yalnız ada inşaatına uygulanır, cüzdana Et yazmaz. Eksik altın mevcut `GoldTopUpCost` ile tamamlanabilir.
- Gerekçe: Et kapısı günlük kart sayısını sınırlar. Elmas sadece sabırsızlığı satar, gücü satmaz.

## 6. Mevcut kart yollarıyla etkileşim (yığılma tavanı)
- **Aynı kart, birden çok kaynak:** bonuslar toplanmaz, **en yüksek olan** geçerlidir (reklam/jeton/elmas %10, atölye %10,
  Kraliyet %15).
- **Temel kart seçilirken kullanılan kaynak sırası:** Kraliyet (bedava, harcamaz) → ilk savaş bedava hakkı → **atölye stoğu** →
  kart jetonu → reklam / 5 elmas. Kraliyet açıkken atölye stoğundaki temel kartlar **harcanmaz**.
- **Kraliyet:** yalnız 3 temel kartı kapsamaya devam eder (`AI_README.md:73-75`). Nadir/Destansı kartlar Kraliyet'le gelmez.
  Atölyede Kraliyet indirimi yoktur. Kraliyet oyuncusu da atölyeyi özel kartlar için kurar.
- **Yuva tavanı:** 3 temel kart + atölye özel yuvası (sv3–6: 1, sv7+: 2). Savaş başına en çok 1 Destansı.
- **Çarpım tavanı:** bir hasar/can yolunda kartlardan gelen toplam çarpan **×1,5'i aşamaz** (`Workshop.CardStackCap = 0.5`).
  Örnek: Keskin Oklar × Zırh Delici = 1,61 olur, tavan bunu 1,50'ye indirir.
- **Hak ne zaman düşer:** `BattleCards.BeginBattle` anında, sonuçtan bağımsız (1 savaş = 1 hak). Reklam veya elmasla
  devam etmek ek hak harcamaz. `Competitive` savaşlarda hak düşmez ve etki yoktur (`BattleCards.cs:25`).
- **Kapsam:** Sefer, Sonsuz ve İstila (rekabetçi değilse). Ada Akın/Baskın savaşlarında kartlar kapalıdır; ada kendi
  ekonomisini korur.

## 7. Et kıtlığı: ada sayılarına önerilen düzeltme (§6'ya karşı **öneri**)
Şu anki §6'da Ağıl, Oduncu ile aynı hızda (30 + 15(L−1)/sa) ve 8 saatlik depoyla üretiyor. Altın madeni deposu birkaç
dakikada dolduğu için (`GameManager.cs:156` 200 + 150(L−1), `GameData.cs:210` 1 + 0,5(L−1)/sn) günde 3 oturumda altın,
depo tavanında kalır. Et ise 8 saat depolanır. Sonuç: seyrek giren oyuncu için Et **altından daha kolay** birikir.
Bu, kullanıcı kararına aykırı.

| Değer | §6 şimdi | Öneri |
|---|---|---|
| Ağıl üretimi /sa | 30 + 15(L−1) | **15 + 8(L−1)** (L1 15, L5 47, L10 87, L15 127) |
| 2. Ağıl | KK7 | **KK10** (2. Oduncu KK7'de kalır) |
| Akın Et ödülü | 25 + 8N | **15 + 4N** (Odun 25 + 8N kalır) |
| Ada binalarının Et çarpanı | Kale 0,5 · Oduncu 0,35 · Ambar 0,2 · Kule 0,15 · Kışla 0,4 | hepsi **×0,6** (0,3 · 0,2 · 0,12 · 0,1 · 0,25) |
| Okçu yükseltmesi | 50 · 1,28^(L−1) Et | **35** · 1,28^(L−1) Et |

Varsayım: günde 3 oturum, Ağıl her oturumda toplanır (~16 saatlik üretim yakalanır).

| Aşama | Et/gün | Adanın Et ihtiyacı/sv | Kartlara kalan | Altın/gün (maden + savaş + görev) | Altın:Et |
|---|---|---|---|---|---|
| MB5 / KK5 | ~750 | ~400 | ~350 (≈5 Sıradan) | ~3.500 | ~4,7 |
| MB10 / KK10 | ~1.600 | ~2.250 | oyuncu seçer | ~6.600 | ~4,1 |

Geç oyunda ada seviyesi ile kart arasında bilinçli bir seçim yapılır. Bu, istenen "Et değerlidir" hissidir.

## 8. Kayıt alanları (`PlayerSaveData`, sürüm 5 → 6)
- `int[] workshopStock`: dizin `BattleCard`, değer kalan hak. Uzunluk eksikse göçte büyütülür.
- `List<WorkshopJob> workshopQueue` `{ int card; long startTicks; long endTicks; int uses; }`
- `bool workshopFirstFreeUsed`, `int workshopAdDay`, `int workshopAdsToday`
- `bool[] workshopAutoPick`: ön-savaşta son seçim hatırlanır. Hak yoksa seçim kendiliğinden kapanır.
- Bina, `Buildings` sözlüğünde `BuildingType.CardWorkshop` olarak saklanır. Et ada cüzdanından (ada kaydı) harcanır.
- Göç: eski kayıtta bütün diziler boş başlar. `cardTokens`/`freeCardUsed` değişmez.

## 9. Arayüz (dikey, en çok 3 ekran)
1. **Atölye paneli** (binaya dokununca): üstte Et/altın; altında kuyruk yuvaları (ilerleme çubuğu, kalan süre,
   [Reklam: Hemen (2/3)], [Elmas 12]). Altta 7 kartlık ızgara (2 sütun): stok "×3", "+2 hak/üretim", kilitliyse "Atölye sv 3".
   Yükseltme her binadaki gibi mevcut bina panelinden yapılır.
2. **Kart ayrıntısı / Üret** (alt sayfa): etki, Et/altın bedeli, süre, verilecek hak sayısı, [Üret]. Et yoksa
   "Et: Martı Adası Ağılı" yazar ve ada kısayolu çıkar (elmas seçeneği yoktur).
3. **Sefer ön-savaş** (mevcut aşama ayrıntısı, `CampaignSceneSetup.cs:840`): temel kart satırı aynı kalır, kart rozetinde
   kaynak görünür (atölye ×N / jeton / reklam). Altına "Atölye" satırı gelir: özel kartlar, yuva sayacı (1/2), Destansı sınırı.
   Savaş HUD'ı aktif kartları listeler (`BattleObjectiveHud.cs:51`).
- Bildirim: 30 dk ve üzeri üretim bitince "Kart hazır" (`GameNotifications`, 22–09 sessiz).

## 10. Telemetri ve bot hedefleri
Olaylar: `workshop_start {card, tier, level, meat, gold, uses}`, `workshop_finish {method: wait|ad|gem, gems}`,
`card_consumed {card, source: royal|free|workshop|token|ad|gem, region, stage, result}`,
`meat_flow {source|sink, amount}` (günlük toplam). `PlaytestBot` Telemetry'ye (`PlaytestBot.cs:22`) şu alanlar eklenir:
`meatPerDay`, `goldPerDay`, `workshopCrafts`, `cardsPerBattle`.

| Ölçüt | Hedef (ORTA) | Sapınca |
|---|---|---|
| Günlük altın:Et | ≥ 4:1 | Ağıl katsayısı (§7) |
| Bölge 1–2 kazanma, tam kartlı − kartsız | ≤ +10 puan | Sıradan bonusu dokunulmaz; özel kart yuvasını sv3'ten sonraya al |
| Bölge 4–6 kazanma, tam kartlı − kartsız | +10…+20 puan, > +25 olmamalı | `CardStackCap`, Nadir bonusları |
| Kalkanlı boss, Kalkan Kıran'la fark | ≤ +15 puan | havuz −%30 → −%20 |
| Stoğu tavanda olan oyuncu (Nadir) | < %15 | Nadir bedeli ↑ |
| Aktif gün başına Destansı üretimi | 0,3–0,6 | Destansı Et tabanı |
| Atölye reklamı / gün (kullanan) | ortalama ≤ 2 | 3/gün sınırı yeterli |
| Ada seviyesi / gün (KK5–10) | §9 hedefinden en çok −%15 sapma | §7 Et çarpanları |

Testler: `CardWorkshopTests` (bedel/hak formülleri, stok tavanı, kaynak sırası, Kraliyet'te hak düşmemesi, ×1,5 tavanı,
`Competitive`, v5→v6 göçü). Bot: `PlaytestBotTests` içinde Bölge 3–6 "kartsız / temel / tam" profilleri.

## 11. Uygulama planı
1. `Data/GameData.cs`: `BuildingType`'a `CardWorkshop` **sona** eklenir. §7'deki `Barracks`/`House` hangisi önce gelirse
   o sıra korunur, asla yeniden sıralanmaz. `UpgradeCost` çarpanı 0,45 (`:452`), `FirstUnlockLevel` = 5 (`:463`).
   `BuildingData` başlangıç durumu eklenir (savaşa girmez). `Progression.Workshop` sınıfı: tabanlar, 0,15 eğimi, süreler,
   `UsesPerCraft`, kademe açılışları, yuvalar, stok tavanları, `CardStackCap`, `FirstCraftSeconds = 30`.
   `Progression.Island`: §7 değerleri.
2. `Systems/BattleCards.cs`: enum'a 4 kart **sona** eklenir. `Count = 7`, `BasicCount = 3` (UI döngüleri `BasicCount`
   kullanır). `Tier()`, kaynak sırası, `BeginBattle` içinde stoktan düşme. Yeni sorgular: `TowerDamageMult`,
   `VsArmoredMult`, `VsFlyingMult`, `WardPoolMult`, `WardExposedBonus` (hepsi tavanlı).
3. Yeni `Systems/CardWorkshop.cs`: kuyruk, `Tick`/çevrimdışı tamamlama, reklam ve elmasla bitirme. csproj'a
   `<Compile Include>` ile elle eklenir.
4. `GemEconomy.cs`: `DailyWorkshopAds = 3`. `Systems/AdRewards.cs`: atölye reklam sayacı.
5. `Data/PlayerSaveData.cs`: §8 alanları, `CurrentVersion = 6` (`:195`), göç.
6. `Base/BuildMenuController.cs:100`: 4. öğe (yerleşim şu an 3 sütun). `Managers/SpriteManager.cs`,
   `Editor/BuildBaseVisualCatalog.cs`: House2 renk kademeleri.
7. Yeni `UI/CardWorkshopPanel.cs` (ekran 1–2). `Campaign/CampaignSceneSetup.cs:840` (ekran 3),
   `Polish/Battle/BattleObjectiveHud.cs:51`.
8. Savaş bağlantıları: `EnemyController.cs:98/173/217`, kule hasarı. `DevMenu`'ye "+500 Et" ve "atölyeyi bitir" eklenir.
   `GameNotifications`'a "kart hazır" eklenir.
- **Bağımlılık:** ada Et cüzdanı (ada Faz 1) olmadan atölye yayına çıkmaz.

## Çakışmalar (üstüne yazılmadı, işaretlendi)
- `AI_README.md:73-75` "3 kart": temel 3 kart ve Kraliyet %15 aynen kalır. Katalog 7 karta çıkar, README güncellenmeli.
- `island_base.md:285` "House2 adada Oduncu olur": artık Ana Üs'te de kullanılıyor. İkisi ayrı sahnelerde ve renkleri farklı
  (ada hep mavi, atölye renk kademeli), yine de karışabilir. Gerekirse Oduncu'ya ağaç/kütük süsü eklenir.
- `island_base.md` §6 Et sayıları: §7'deki öneri ada dengesini değiştirir. Ada bot ölçümüyle birlikte onaylanmalı.
- `island_base.md:272` elmas → kaynak: atölyede Et için uygulanmaz.

## Açık sorular (en çok 3)
1. Yenilgide de hak düşsün mü (önerim: evet, 1 savaş = 1 hak), yoksa yalnız zaferde mi?
2. Destansı kart reklamla anında bitebilsin mi (önerim: evet, günde 3 sınırı içinde), yoksa reklam yalnız Sıradan/Nadir için mi?
3. §7 Et düzeltmesi kabul mü? Alternatif: §6'yı olduğu gibi bırakıp kart Et bedellerini ×2 yapmak. Bu yol seyrek giren
   oyuncunun Et biriktirme avantajını çözmez.

## Kullanıcı kararları (12 Kasım 2026)
1. **Yenilgide de** kullanım hakkı düşer (`BeginBattle`'da harcanır).
2. **Destansı** üretim de reklamla anında biter (günlük 3 reklam hakkının içinden).
3. §7 Et kıtlığı düzeltmesi kabul. **Ek:** eksik Et elmasla tamamlanabilir (§5'teki "Et elmasla alınamaz" kuralı kalkar),
   ama altından **5 kat pahalı**: `GemEconomy.MeatPerGem = GoldPerGem / 5` (şu an 40 / 5 = **8 Et/elmas**).
   `MeatTopUpCost(missing) = ceil(missing / MeatPerGem)`, `GoldTopUpCost` ile aynı kalıp; oran tek yerden (GoldPerGem'e bağlı).
   Ada binalarındaki "1 elmas = 25 kaynak" kuralı (island_base §6) Et için de bu orana çekilir; Odun 25 kalır.

## Uygulama notu (12 Kasım 2026)
Kod: `Systems/CardWorkshop.cs` (mantık), `UI/CardWorkshopPanel.cs` (ekran 1–2), `Systems/BattleCards.cs` (7 kart, kaynak sırası,
savaş çarpanları), `Progression.Workshop` (`Data/GameData.cs`, bütün sayılar), kayıt `WorkshopSaveData` (`Data/PlayerSaveData.cs`).
- **Bina:** Ana Üs 5'te açılır, 1 adet, 150 altın, yuva `workshop_1` (3×3). Görsel Tiny Swords `House2`, Ana Üs renk kademesine göre
  (`BaseVisualCatalog` tür 5; `Tools/Base Review/Add Card Workshop Visuals`). Dokununca üretim ekranı açılır; YÜKSELT bina paneline gider.
- **Bedeller belgeden farklı (§4):** ada v2 Et ekonomisi (island_base_v2 §17) belgedeki §7 varsayımından çok daha kıt çıktı
  (sıradan oyuncu gün 1 ≈ 241, gün 2 ≈ 381, gün 7 ≈ 752 Et). Bu yüzden tabanlar Et/altın **Sıradan 150/180, Nadir 360/430,
  Destansı 900/1080** (eğim 0,15/sv aynı). Sonuç: gün 1'de 1, ilk günlerde 1–2 Sıradan kart; hak başına Et sv1 150 → sv13 84.
- **Hak:** üretim başına `1 + (sv−1)/3` hak, üretim başlarken kilitlenir. Hak `BattleCards.BeginBattle`'da düşer (zafer/yenilgi fark etmez).
  Kraliyet açıkken temel kart, rekabet modu ve kalkansız aşamadaki Kalkan Kıran hak harcamaz.
- **Bitirme:** süre (çevrimdışı dahil, `GameClock`), ödüllü reklam (her kademe, günde 3, `GemEconomy.DailyWorkshopAds`) ya da
  `GemEconomy.FinishCost`. Eksik Et elmasla: 1 elmas = 8 Et (`MeatTopUpCost`); eksik altın `GemActions.OfferGoldTopUp`. İlk üretim bedava, 30 sn.
- **Kayıt v8** (belgedeki v6 değil: ada v6/v7'yi aldı). Eski kayıtta atölye boş başlar; bina türü kaydı Ana Üs seviyesine göre eklenir.
- **Arayüz:** ön-savaşta "ATÖLYE KARTLARI" satırı (yuva sayacı, Destansı sınırı, kalkansız aşamada soluk Kalkan Kıran);
  savaş HUD'ı "KART +%10" yalnız temel kart etkinse yazar. "Kart hazır" bildirimi 30 dk+ üretimlerde. DevMenu: +500 ET, ATÖLYEYİ BİTİR.
  Kalkan Kıran simgesi Tiny Swords Icon_09 (`CampaignArt/ui_icon_cross`).
- **Testler** `CardWorkshopTests` (13): formüller, sıradan oyuncunun gün 1–2 kart sayısı, ilk bedava + süreyle üretim, reklamla
  (Destansı dahil, günde 3), Et + elmasla tamamlama (eksikte hiçbir şey harcanmaz), seviyeye göre hak, zafer ve yenilgide hak,
  Kraliyet/rekabet/kalkansız aşama, ×1,5 tavanı, v7→v8 göçü, gerçek savaşta Keskin Oklar (okçu ×1,15, kahraman ×1).
  Görüntüler `Docs/playtest/card_workshop_*.png`.
- **Yapılmadı:** §10 telemetri olayları ve bot "kartsız/temel/tam" ölçümü (sonraki bot turunda), kart stoğu rozeti (bina üstü).
