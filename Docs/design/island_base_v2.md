# Martı Adası v2: yaşayan ikinci üs (hedef zorluk: ORTA)

Bu belge `Docs/design/island_base.md` (v1) ile çeliştiği her yerde **onun yerine geçer**. Kod içermez.
Sayılar yalnız `Progression.Island` (`Data/GameData.cs:346`) ve `GemEconomy` (`Systems/GemEconomy.cs`) içinde tutulur.
Karo kuralları (hangi karo nereye) bu belgede yok: `Docs/art/tiny_swords_island_kit.md` (ayrı ajan yazıyor).

**Korunan kullanıcı kararları:** açılış Ana Üs 5 · Akın merdiveni + zamanlı baskın · baskın hırsızlığı en çok **%20**
(yalnız toplanmamış ada üretimi; cüzdan ve binalar asla) · ATEŞ + oto-ateş düğmesi (×0,75, KK5) · ayrı Et ve Odun ·
Kışla adadan sonra · Kart Atölyesi Ana Üs'te ve Et harcar · 1 elmas = 8 Et (`GoldPerGem / 5`), Odun 25.

**Faz 1'in reddedilme nedenleri ve v2 cevabı:**

| # | Kullanıcı geri bildirimi | v2 kararı |
|---|---|---|
| 1 | Görünüm Tiny Swords tanıtım görseline benzemiyor, kodla çizilmiş ağaç/tekne var | Kodla çizilmiş sprite **sıfır**. Arazi editörde gerçek karolarla pişirilir (§10 Faz A). Tekneler Kenney Pirate Pack'ten gelir (`Assets/kenney_piratePack/`, CC0) |
| 2 | Düşmanların hepsi aynı ve savaşçıya benzemiyor | Kızıl Warrior/Archer/Lancer ve DungeonMonsters2D'den 17 canavar + 3 boss, rollere göre karışık (§7.3) |
| 3 | Tek Akın çok Et veriyor | Akın Eti 5 + 2N, tekrarda 0. Asıl Et kaynağı yavaş Ağıl üretimi (§6) |
| 4 | Adaya girince hemen "Akın başlat" ekranı açılıyor | Ada kendi kimliği olan **ikinci bir üs**. Girişte üs görünümü açılır; Akın yalnız AKIN düğmesinden başlar ve aynı yerleşimde oynanır (§2–3) |

## 1. Tasarım ilkeleri
- **Önce üs, sonra savaş.** Ada oturumlarının ≥%80'inde inşa/toplama/yerleşim eylemi olmalı. Akın isteğe bağlıdır, zamanlı baskın ise ritmi verir.
- **Yerleşim savunmadır.** Baskınlar oyuncunun kurduğu düzene saldırır. Kulenin yeri, evin merdivene uzaklığı ve
  Lancer bayrağı sonucu değiştirir. Yenilgi her zaman bir yerleşim ya da oyun hatasıyla açıklanabilir.
- **Yaşam görünür.** Her kaynak ekranda bir işçinin işi: piyon ağaç keser, koyun güder, altın kazar. Sayılar bu
  görüntünün özetidir. Görüntü maliyetsizdir; üretim UTC tick ile hesaplanır.
- **Ana Üs'ün kopyası değil** (tablo §2.3). Aynı sistemler (kamera, yerleştirme, inşaat görevi, toplama) kullanılır,
  ama arazi dikeydir, düşman denizden gelir, alan bulut açılarak kazanılır, kahraman tıkla-ateşle oynanır.
- ORTA: Bölge 1–2 eşdeğeri Akın 1–9 affedicidir. Yenilgide can düşmez, ödül kaybı yoktur, yalnız onarım beklenir.

## 2. Çekirdek döngü

### 2.1 Dakika dakika (tipik ada oturumu 2–5 dk)
| Sn | Oyuncu görür | Oyuncu yapar |
|---|---|---|
| 0–5 | Yelken geçişi (v1, 1,2 sn). Kamera kaleden açılır; piyonlar çalışıyor, koyunlar otluyor | — |
| 5–30 | Oduncu ve Ağıl önünde kaynak yığını (0–6 kütük/et), üstünde toplama baloncuğu (`GoldMineCollectIndicator` kalıbı) | Dokunarak toplar |
| 30–90 | Usta boşta ise kalede çekiç simgesi | Bir yükseltme başlatır, bina yerleştirir/taşır, bulut açar |
| 90–120 | Sağ üstte baskın rozeti: "Sakin · 3 sa 12 dk", ya da ufukta yelkenler | Kuleyi o kumsala bakan yar başına taşır |
| 120+ | AKIN düğmesinde "Akın 7 hazır" rozeti | İsterse Akın oynar (1,5–3 dk), sonra onarımı izler |

### 2.2 Oturumdan oturuma ve günden güne
- **Günde 3 oturum:** toplama + 1–2 yükseltme. Zamanlı baskın her 8 saatte bir (±45 dk) gelir; 30 dk önce uyarılır.
  Oyuncu canlı savunabilir ya da bırakabilir.
- **Birkaç günlük hedef:** Kıyı Kalesi'ni (KK) Ana Üs seviyesine yetiştirmek, bulutlu bölgeleri açmak (her bölge
  kaynak **ve** yeni bir çıkarma kumsalı getirir), adacıklara kule dikmek.
- **Uzun vade:** KK tavanı = Ana Üs seviyesi. Ada, Ana Üs'ü bekletmez (ayrı Usta), ama onu geçemez.
  Akın merdiveni Ana Üs ilerlemesine bağlı kalır: en yüksek Akın = 3 × KK.
- **Risk/ödül gerilimi:** yeni bölge = daha çok ağaç, çayır ve altın. Aynı zamanda baskınların inebileceği yeni kumsal demek.

### 2.3 Ana Üs ile fark
| Konu | Ana Üs | Martı Adası |
|---|---|---|
| Arazi | Düz 22×38, çitli | 22×36, **3 yükseklik**, yarlar, 3 merdiven, deniz, 4 adacık |
| Kaynak | Altın madeni (bina) | Odun (ağaç kesen piyon), Et (koyun güden piyon), az altın (altın taşı) |
| İşçi | İnşaatçı(lar), Ana Üs ile ortak | 1 **Ada Ustası** + Evlerden gelen piyonlar |
| Alan | Ana Üs 4/7/10/13'te kendiliğinden genişler | Bulutlu bölgeyi Odun + Usta süresiyle **sen açarsın** |
| Savunma | Seferde, başka haritada | **Bu haritada**: binaların savaşır ve hasar alır |
| Düşman | Kenarlardan yürür | Denizden teknelerle gelir; uçanlar buluttan, tırmananlar yardan |
| Kahraman | Warrior, dokun-yürü | Okçu Kaptan: atış yerleri, ATEŞ / dolu atış / oto-ateş |

## 3. Ekran modları ve akış

| Mod | Kamera | HUD | Çıkış |
|---|---|---|---|
| **Ada görünümü** (varsayılan) | `BaseCameraController` kaydırma/sıkıştırma; varsayılan zum = tam görünümün %70'i, en yakın %45 | Üst: kale arması (`LevelCrest`), Odun x/tavan, Et x/tavan, Elmas · sağ üst: baskın rozeti · alt dock: **İNŞA ET · AKIN · KAPTAN** · sol üst: **ANA ÜS** | — |
| Yerleştirme | Aynı; hayalet sürüklenirken kenar kaydırma | YERLEŞTİR / İPTAL (`PlacementController` UX'i). Kırmızı yürüyüş şeritleri ve kule menzil halkası (yükseklik bonusu dahil) yalnız bu modda görünür | Onay/iptal |
| Akın Panosu | Değişmez (panel) | Merdiven kartları: N, güç, kumsal iğneleri, düşman portreleri, ödül, şart; üstte bekleyen baskın varsa kırmızı kart | SAVUNMAYA BAŞLA |
| **Savunma** | 0,8 sn'de savaş çerçevesine kayar: genişlik 22 u sığar (9:16'da ortho ≈19,6), kaydırma ve zum kilitli | `IslandBattleHud`: ATEŞ (basılı = dolu atış), OTO, 3 yetenek, dalga afişi, kale canı, Çekil | Zafer / yenilgi |
| Sonuç | Savaş çerçevesi | Ödül, yenilgi etiketi, hasarlı binalar listesi | ADAYA DÖN: kamera önceki görünüme döner, sahne yeniden yüklenmez |

- **Akın nereden başlar (karar):** dock'taki **AKIN** düğmesi. Ana Üs'teki SEFER düğmesinin eşi. Bir binaya bağlanmaz,
  çünkü çekirdek döngü bir binanın arkasına kilitlenmemeli ve bu sette liman sprite'ı yok. Kaleye dokunmak da panelde "Akın" sekmesi açar.
- **Savaş başlarken:** boru sesi (ses varlığı yoksa sessiz, `AudioLibrary`'ye eklenecek). Piyonlar en yakın Eve ya da
  kaleye koşup kaybolur. Koyunlar çayır ortasında toplanır. Savaşta inşa ve taşıma kilitlidir.
- **Zamanlı baskın akışı:**
  1. **T−30 dk:** bildirim "Ufukta korsan yelkenleri!". Ada görünümünde hedef kumsalın açığında 1–3 küçük tekne
     sallanır (`BattleOffscreenIndicators` okuyla), kumsalda kırmızı işaret çıkar. Ana Üs'te ADA düğmesinde kırmızı yelken rozeti görünür.
  2. **T0:** "BASKIN!" Tekneler kıyının açığında demirler. Rozette 2 sa geri sayım başlar; teknelere ya da rozete dokununca savunma başlar.
  3. **Pencere biterse:** yerleşime göre çevrimdışı çözüm yapılır (§7.8). Sonuç `AwayReport` satırıyla gösterilir.
     Binalar hasar **almaz** (kullanıcı kararı).
- **İlk ziyaret (üs önce, 6 adım, `FirstTimeTips` `UI/AwayReportUI.cs:126` kalıbı):**
  1. Kale, Oduncu, Ağıl ve Okçuluk Alanı **hazır kurulu** gelir (taşınabilir); 2 piyon çalışmaktadır. Kamera denizden adaya kayar.
  2. "Odunu topla" (yığına dokun).
  3. "Ev kur: daha çok piyon" (yerleştirme öğretilir; Ev L1 anında biter).
  4. "Gözcü Kulesi'ni batı yar başına koy" (yeşil bölge gösterilir; menzil halkası A kumsalını kapsar).
  5. Boru sesi: öğretici Akın 1 hemen gelir (tek sandal, 0,6 hız). ATEŞ'e dokunulur, kule yardım eder.
  6. Zafer: "Ödül: Odun. Et yavaş gelir, Ağıl'ı yükselt." Öğretici biter. Bundan sonra Akın yalnız AKIN düğmesiyle başlar.

## 4. Harita (dikey, 22 × 36 hücre; 1 karakter = 1 hücre = 1 u = 64 px)

```
     col 0123456789012345678901   Gösterim
r00      ~~~~~~~~~~~~~~~~~~~~~~   ~ deniz (animasyonlu)   r sudaki kaya
r01      ~~~~~~~~~~r~~~~~~~~~~~   . ova (yük. 0)   1 yayla (1)   2 zirve (2)
r02      ~~~~~~~T111111T~~~~~~~   # yar yüzü (taş, yürünmez)   H merdiven
r03      ~~~~~T12KKKKK221T~~~~~   K Kıyı Kalesi 5×3 (sabit)
r04      ~ii~1T12KKKKK221T1~~~~   T ağaç   o çayır (koyun)   g altın taşı
r05      ~ii~1T12KKKKK221g1~~~~   i adacık (yük. 0, yalnız kule)
r06      ~~~~11122222222g11~~~~   A B C D çıkarma kumsalları
r07      ~~~~1T122222222gg1~~~~
r08      ~~~~1T1###HH###111~~~~   <- S1 zirve merdiveni (sütun 10–11)
r09      ~~~~11111111111111~~~~
r10      ~~~~1TT11111111111~ii~
r11      ~~~~1TT111111111TT~ii~
r12      ~~~~1T1111111111T1~~~~
r13      ~~~~11111111111111~~~~
r14      ~~~~##H########H##~~~~   <- S2 batı (sütun 6), S3 doğu (sütun 15)
r15      ~~~................~~~
r16      ~~.TT............TT.~~
r17      ~~.TT...oooo.....TT.~~
r18      ~~A.....oooo.......B~~   A batı kumsalı (sütun 2, r18–20)
r19      ~~A.....oooo...ooo.B~~   B doğu kumsalı (sütun 19, r18–20)
r20      ~~A...........ooo..B~~
r21      ~~~................~~~
r22      ~~~.TTT..........T.~~~
r23      ~~~.TTT.........TT.~~~
r24      ~~~..T..........TT.~~~
r25      ~~~~......oo......~~~~
r26      ~~~~......oo......~~~~
r27      ~~~~~.CCC....DDD.~~~~~   C güneybatı, D güneydoğu kumsalı
r28      ~~~~~~~~~~~~~~~~~~~~~~
r29      ~ii~~~~~~~~~r~~~~~ii~~   SW ve SE adacıkları
r30      ~ii~~~~r~~~~~~~~~~ii~~
r31-35   ~~~~~~~~~~~~~~~~~~~~~~   güney tekne yaklaşma suyu (savaşta alt HUD altında)
```

- **Süs (yerleşim alanı dışında, kit belgesine göre serpilir):** her bölgede 2–3 çalı (`Bushes/Bush 1–4`),
  ova kenarında kayalar (`Rocks/Rock1–4`), açık denizde ≥6 `Water Rocks_01–04`. Ada görünümünde ekranın sol ve sağ
  kenarında sürekli süzülen `Clouds_01–08` vardır. Bunlar tanıtım görselindeki kenar bulutlarıdır; savaşta %40 saydamlığa iner.
- **Merdivenler darboğazdır:** yerde yürüyen her düşman S2 ya da S3'ten, sonra S1'den geçer. İstisnalar: tırmananlar
  (Fare, Örümcek) herhangi bir `#` hücresini 1,2 sn'de tırmanır; uçanlar düz çizgide gelir.
- **Yürüyüş şeritleri** (1 hücre geniş, bina konamaz, yalnız yerleştirmede kırmızı görünür):
  - A → r19 boyunca doğuya, c6 → S2 → yayla çaprazı → S1 → kale kapısı (10,5; 6,8).
  - B → c15 → S3 → S1.
  - C → c7 kuzeye → S2.
  - D → c14 kuzeye → c15 → S3.
  - `EnemyController.SetRoute` (`Battle/EnemyController.cs:234`) zaten var.
- **Tekne rotaları** (denizde kalma süresi = batırma fırsatı; adacık kuleleri bu kanallara bakar):

  | Kumsal | Rota | Görünür süre (Sandal / Mavna) |
  |---|---|---|
  | A | KB köşesinden (1,5; −1) girer, batı kanalından güneye iner (c0–3, r6–17; NW adacığının doğusundan geçer), r19'da doğuya dönüp A'ya yanaşır | ≈18 / 30 sn |
  | B | KD köşesinden girer, doğu kanalından iner (c18–21; E adacığını batısından geçer), B'ye yanaşır | ≈16 / 27 sn |
  | C / D | Güneyden (r35) kuzeye çıkar. r28–30 arası görünürdür ve SW/SE adacık kulelerinin menzilindedir | ≈4 / 7 sn (kısa: P5 ve adacık kuleleri şart) |
- **Okçu Kaptan atış yerleri** (gerçek UI sprite'ı, `UI Elements/Cursors`; halka çizimi yok):

  | Yer | Konum | Kapsadığı | Açılış |
  |---|---|---|---|
  | P1 | zirve batı (7,5; 6,5) | S1, batı yayla | KK1 |
  | P3 | batı yar başı (5,5; 13,5) | S2, A, C yolu | KK1 |
  | P4 | doğu yar başı (16,5; 13,5) | S3, B, D yolu | KK2 |
  | P2 | zirve doğu (13,5; 6,5) | S1, doğu yayla | KK4 |
  | P5 | güney çayırı (10,5; 24,5) | C/D tekneleri; açıkta, yükseklik bonusu yok | KK6 |

**Bölgeler (bulut altında başlar, sırayla açılır; açılınca bulutlar 1,5 sn'de kayıp dağılır):**

| Bölge | Kapsam (sütun × satır) | KK şartı | Keşif bedeli (Odun / Et, Usta süresi) | Getirdiği | Yeni risk |
|---|---|---|---|---|---|
| Z0 Kale Yaylası | c4–12 × r2–14 + c2–9 × r15–21 | — | başlangıç | Kale, S1, S2, ≈14 ağaç, 6 çayır hücresi, P1, P3 | A kumsalı |
| Z1 Doğu Kıyısı | c13–17 × r2–14 (Z3 hariç) + c10–19 × r15–21 + adacık E | 2 | 150 / 0, 30 sn | S3, ≈10 ağaç, 10 çayır, P4 | B kumsalı (Akın 4+) |
| Z2 Güneybatı Koyu | c2–8 × r22–30 + adacık SW | 3 | 300 / 30, 3 dk | ≈7 ağaç | C kumsalı (Akın 9+) |
| Z3 Altın Burnu | c15–17 × r2–8 + adacık NW | 4 | 500 / 50, 5 dk | Altın Damarı, P2 | — (güvenli ödül) |
| Z4 Güneydoğu Koyu | c13–19 × r22–30 + adacık SE | 5 | 800 / 80, 15 dk | ≈5 ağaç | D kumsalı (Akın 15+) |
| Z5 Orta Çayır | c9–12 × r22–27 | 6 | 1.500 / 150, 1 sa | 4 çayır (+%15 Et), 2. Oduncu yeri, P5 | açık ova |

- Akın N'in kumsalı ancak bölgesi açıksa kullanılır. KK şartları Akın kapısıyla (KK ≥ ⌈N/3⌉) aynı ana denk gelir.
  Panel eksik bölgeyi açıkça yazar: "Doğu Kıyısı'nı keşfet".
- **Savaş çerçevesi:** genişlik 22 u. Üst HUD ≈3 u, alt HUD ≈6 u kapar. r00–r30 görünür, güneyden gelen tekneler r30'da görünür olur.
  Ana Üs'ün tam zum görünümüyle (22 u) aynı ölçek. Yenilgi her zaman ekranda görülür.

## 5. Binalar ve piyonlar (hepsi Tiny Swords **mavi** set; renk kademesi yok, v1 kuralı)

| Bina | Sprite | Ayak izi | Yer kuralı | Adet (KK) | Kurulum (Odun / Et) | Yükseltme çarpanı (Odun / Et) | İşlev | Savaş canı |
|---|---|---|---|---|---|---|---|---|
| Kıyı Kalesi | Castle | 5×3 | zirvede sabit | 1 | hazır | 1,0 / 0,3 | Tüm tavanlar, cüzdan tavanı, 2 piyon | 300 + 80(k−1) |
| Ev / Cottage | House1 | 2×2 | kara, adacık hariç | 1 · 2 (KK3) · 3 (KK6) · 4 (KK9) | 40 / 0 | 0,25 / 0 | +2 piyon (L5'te 3, L10'da 4) | 80 + 20(L−1) |
| Oduncu Kampı | House2 | 2×2 | kara; 4 u içinde ağaç | 1 · 2 (KK7) | hazır · 2.: 300 / 60 | 0,15 / 0,2 | Odun | 80 + 20(L−1) |
| Ağıl / Pasture | House3 | 2×2 | ova/yayla; 3 u içinde çayır | 1 · 2 (KK10) | hazır · 2.: 600 / 0 | 0,45 / 0 | Et | 80 + 20(L−1) |
| Gözcü Kulesi | Tower | 2×2 | kara **ve adacık**; yükseklik ≥1'de +1,5 menzil | 1 · 2 (KK2) · 3 (KK4) · 4 (KK6) · 5 (KK9) | 60 / 10 | 0,5 / 0,1 | Kule (§7.5) | sefer Okçu Kulesi eğrisi |
| Okçuluk Alanı | Archery | 3×2 | kara | 1 | hazır | seviye = Okçu (§7.4) | Okçu Kaptan'ın evi, yükseltme ve yetenek | 100 + 25(L−1) |
| Kışla | Barracks | 3×2 | yayla/ova | 1 (KK3) | 150 / 40 | 0,35 / 0,25 | Lancer'lar + toplanma bayrağı | 120 + 30(L−1) |
| Manastır | Monastery | 3×3 | kara | 1 (KK6) | 400 / 80 | 0,4 / 0,15 | Şifa, onarım ×0,5 | 90 + 25(L−1) |
| Altın Damarı | Gold Stone 1–6 | sabit (Z3) | doğa nesnesi | 1 | bölge açılınca | seviye = KK | Ana Üs'e altın | — |

- **Kaldırılan bina:** v1 **Ambar**. Cüzdan tavanını artık kale verir: daha az bina, House3 Ağıl'a kalır.
- **Yerleşim:** serbest 1 hücre ızgara (Ana Üs gibi). Geçersiz hücreler: yar yüzü, merdiven ve önündeki 1 hücre,
  yürüyüş şeritleri, ağaç, çayır, altın taşı, kumsal, kapalı bölge. Adacığa yalnız kule konur.
  Taşıma bedava ve anında. Basılı tut ile taşınır (`LongPressHandler`). Savaşta taşıma yoktur.
- **Ada Ustası:** 1 adet (Blue Pawn Hammer), Ana Üs inşaatçılarını kullanmaz (v1). Görev UTC tick ile ilerler
  (`BuilderTaskSave` kalıbı, tek görev). Elmasla bitirme `GemEconomy.FinishCost`. Süreler `TimingConfig` ×0,5:
  5 sn, 30 sn, 1,5 / 3 / 5 / 7,5 / 15 / 30 dk, 1 / 1,5 / 2 / 3 / 4 / 6 sa (sv 2→15).
- **Piyonlar:**
  - Sayı = 2 (kale) + Ev başına 2/3/4.
  - İşçi yuvası: Oduncu 2/3/4 (L1/L5/L10), Ağıl 1/2/3, Damar 1.
  - Yeni piyon en düşük doluluk oranlı binaya gider (eşitlikte Ağıl). Bina panelinde "İşçi 2/3 [−][+]" var.
  - Üretim × (işçi / yuva). Boştaki piyon kalede dolaşır.
  - **Görsel döngüler** (yalnız görüntü):
    - Oduncu: koş → ağaç → `Interact Axe` 3 vuruş → her 3. ziyarette ağaç kütüğe döner (3 dk'da yeniden büyür) → `Run Wood` → yığın.
    - Ağıl: koyunlar `Idle/Grass/Move` ile çayırda dolaşır; piyon `Interact Knife` 1,2 sn (kırkma) → `Run Meat`.
    - Damar: `Interact Pickaxe` → `Run Gold` → kale.
  - Yığın, depo doluluğuna göre 0–6 `Wood Resource` / `Meat Resource` gösterir (`BaseMineVisual` kalıbı).
- **Doğa kısıtı** (yerleşimi anlamlı kılar, cezalandırmaz):
  - Oduncu, 4 u içinde yuva × 2 ağaç bulamazsa üretimi ×(ağaç / (2 × yuva)) olur.
  - Ağıl, 3 u içinde 6 çayır hücresi bulamazsa ×0,5 olur.
  - Panel bunu yazar: "Yakın ağaç 5/8".

## 6. Ekonomi

**Formüller** (L = bina seviyesi, k = KK):

| Değer | Formül | Örnek |
|---|---|---|
| Odun/sa (tam kadro) | 30 + 15(L−1) | L1 30 · L5 90 · L10 165 |
| Et/sa (tam kadro) | 15 + 8(L−1) (atölye §7, kabul edildi) | L1 15 · L5 47 · L10 87 · L15 127 |
| 2. Ağıl | KK10, üretimi × `SecondPastureMult` = 1,0 (ayar düğmesi, §11) | |
| Orta Çayır (Z5) | Ağıl üretimi +%15 | |
| Altın Damarı → Ana Üs altını/sa | 6 + 3(k−1), 1 işçi | KK4 15 · KK5 18 · KK10 33 |
| Bina deposu | 8 sa üretim; toplamak için dokunulur | |
| Cüzdan tavanı | Odun 600 + 400(k−1) · Et 300 + 200(k−1) | KK15 Odun 6.200 · Et 3.100 |
| Yükseltme bedeli | W[L] × çarpan (§5), 10'a yuvarlanır. W = 80, 150, 250, 380, 550, 760, 1000, 1300, 1650, 2050, 2500, 3000, 3600, 4300 (sv 2→15, v1) | KK5: 380 Odun + 110 Et |
| Okçu yükseltmesi (Et, anında, tavan KK) | 35 × 1,28^(L−1), 5'e yuvarlanır (L → L+1) | 1→2 35 · 4→5 75 · 9→10 250 · 14→15 870 |
| **Akın ilk zafer** | Odun 25 + 8N · **Et 5 + 2N** · Ana Üs altını 20 + 5N · elmas her 5. Akın'da +3, boss'ta +5 | N10: 105 Odun, 25 Et, 70 altın |
| Akın tekrar | Odun ve altının %30'u, **Et 0**. Günde ilk 5 tekrar ödüllü (`ReplayRewardsPerDay`) | |
| Zamanlı baskın savunuldu | bekleyen üretime +%10 · 10 + 2k Ana Üs altını (v1) | |
| Kereste Sevkiyatı | 100 Odun = bir Ana Üs inşaatına −10 dk, günde 3 (v1, Odun fazlası buraya akar) | |
| Elmas | Eksik Et: ⌈eksik / 8⌉ (`MeatPerGem = GoldPerGem / 5`) · Eksik Odun: ⌈eksik / 25⌉ · Usta bitirme `FinishCost` | |

- **Et kıtlığı ölçüsü:** Akın 15'in Eti (35), o andaki Ağıl'ın (L5, 47/sa) 45 dk üretimidir. Akın 1–15'in tüm ilk
  zaferleri 315 Et verir; bu, bir günlük üretimin altındadır. Hedef: Et'in ≥%85'i Ağıl'dan gelsin.
- **İlk 3 gün** (varsayım: açılış günü ≈10 sa, sonraki günler 16 sa yakalanan üretim, günde 3 oturum; başlangıç hediyesi 150 Odun + 30 Et):

| Gün | KK · Ağıl · Oduncu | Odun geliri (üretim + Akın) | Et geliri (üretim + Akın) | Et gideri (bina + okçu) | Karta kalan Et | Adadan Ana Üs'e altın | Altın:Et (Ana Üs ≈3.500/gün) |
|---|---|---|---|---|---|---|---|
| 1 | 1→2 · 1→2 · 1→2 | 300 + 123 (+150) | 150 + 27 (+30) = 207 | ≈75 | ≈130 (ilk bedava + 3 Sıradan) | 90 | ≈17 : 1 |
| 2 | 2→3 · 2→3 · 2→3 | 840 + 195 | 430 + 45 = 475 | ≈195 | ≈280 | 135 + ≈30 baskın | ≈7,4 : 1 |
| 3 | 3→4 · 3→4 · 3→4 | 1.080 + 267 | 560 + 63 = 623 | ≈215 | ≈410 | 180 + ≈35 | ≈5,6 : 1 |
| Kararlı KK5 | 5 · 5 · 5 | ≈1.440 | ≈750 + ≤35 | seçime bağlı | seçime bağlı | 288 damar + ≈60 | ≈4,5 : 1 |
| Kararlı KK10 | 10 · 10 + 2. Ağıl L1 · 2 Oduncu | ≈4.000 | ≈1.630 | seçime bağlı | seçime bağlı | 528 + ≈90 | ≈4,0 : 1 (Ana Üs ≈6.600) |

- **Ana Üs etkisi:** adadan gelen altın ≈%5 (gün 2) ile %9 (KK10) arasında kalır. Hedef ≤ %10. v1'in görünmez
  "Ada Ticareti +%2/kademe" fikri yerine görünür Altın Damarı gelir (soru 3).

## 7. Savaş (aynı yerleşimde)

### 7.1 Akış
Dalga = **tekne → iniş → yürüyüş** (v1). Tekneler 2,5 sn arayla gelir, dalgalar arası 8 sn bekleme var. İnen birlik
1,5 sn **ıslak** olur (×1,25 hasar alır). Tekne batınca tayfa boğulur. Merdiven (v1 §5): güç = 1 + round(0,8N); dalga 1/2/3/4
(N ≤2 / ≤9 / ≤24 / sonrası); dalga başına tekne min(1 + ⌈N/3⌉, 7); boss her 10. Akın'da. **Uçanlar teknesiz gelir:**
ekran kenarından 3–5'lik sürüler halinde, 2 sn önce yön okuyla. Savaş altını yoktur, savaşta inşa yoktur.

### 7.2 Tekneler
**Sanat: Kenney Pirate Pack** (`Assets/kenney_piratePack/PNG/Retina/Ships/`, CC0, üstten görünüm).
- **İçe aktarma:** Retina PNG, **64 PPU**, Filter Bilinear (vektör stil; Tiny Swords'un Point ayarı burada bulanık
  kenar verir), sıkıştırma yok, mipmap kapalı. Varsayılan boy ship 66×113 px; Retina 132×226 px = 64 PPU'da 2,06 × 3,53 u.
- **Yön:** sprite'ta pruva aşağı (−y) bakar. Dönüş = hız yönü açısı + 90°. Tekneler kumsala pruvayla yanaşır.
- **Renk dizini** (ship 1–6): beyaz, siyah-kafatası, **kırmızı-haç**, yeşil, mavi, sarı. +6 hasarlı, +12 ağır hasarlı,
  +18 batık (gri) halidir. Mavi oyuncunun rengi olduğu için düşmanda **kullanılmaz**.

| Tekne | Can (× düşman can çarpanı) | Tayfa | Hız | Sprite (Retina, 64 PPU) | Ölçek | Son boy (en × boy, u) | Görünür |
|---|---|---|---|---|---|---|---|
| Sandal | 60 | 2 | 1,0 | `dinghyLarge1–3` (40×76 px), rastgele | 1,25 | 0,78 × 1,48 | Akın 1+ |
| Akıncı Teknesi | 150 | 4 | 0,8 | Kademe I `ship (3)` kırmızı · II–III `ship (2)` siyah · IV `ship (6)` sarı | 0,70 | 1,44 × 2,47 | 5+ (golem taşıyabilir) |
| Kalkanlı Mavna | 300 | 6 | 0,6 | aynı renkler, ölçek büyük; yanına `cannon.png` ×2 süs (64 PPU'da 0,9 × 0,5 u) | 1,00 | 2,06 × 3,53 | 15+ (tek atış ×0,6 alır) |

- **Hasar durumu:** can %66'nın altına inince sprite +6'ya, %33'ün altında +12'ye geçer (ör. kırmızı 3 → 9 → 15).
  Böylece oyuncu teknenin batmak üzere olduğunu okur. Sandalın hasar karesi yoktur: %50'de hafif yalpalar.
- **Batış (0,8 sn):** +18 batık sprite (kırmızı 21, siyah 20, sarı 24), %70 ölçeğe küçülme + saydamlaşma,
  Tiny Swords `Particle FX/Water Splash` ve "+N boğuldu" yazısı. Sandal yalnız yatar ve kaybolur.
- **Güverte:** tayfa sprite'ı güvertede gösterilmez (bakış açısı çatışır). Pruvada tayfa sayısı rozeti ("×4") durur.
  İnişte Tiny Swords / DM2D birimleri pruva ucundan 0,4 sn arayla çıkar.
- **İsteğe bağlı süs:** Z4 açıklarında yarı batık `ship (20)` (gri) 1 adet; ufuk teknelerinde (§3 T−30) `ship (3)`
  ölçek 0,45. Kenney `Effects/explosion*`, `fire*` **kullanılmaz**; efektler Tiny Swords'tan gelir (stil birliği).
  `cannonBall` ileride bir "Kıyı Topu" yeteneği için ayrılır.

### 7.3 Düşman listesi (sv 1 tabanı, sonra `ScaleToLevel`; `GameData.cs:717`)
Kızıl insanlar (Tiny Swords kareleri) ile DungeonMonsters2D (iskeletli prefablar; Animator tetikleyicileri
`MoveTrigger/AttackTrigger/DeathTrigger/StunedTrigger/ClimbTrigger/SpecialATrigger`) karışık gelir.
DM2D boyutu gövde yüksekliğine göre normalize edilir: Kızıl Warrior gövdesi = 1,0 u.

| Birim (TR / EN) | Görsel | Rol | Can | Hasar | Hız | Menzil | Altın | Özel | Benzeri |
|---|---|---|---|---|---|---|---|---|---|
| Akıncı / Raider (var) | Red Warrior | yakın; şerit, 1,5 u içindeki binayı vurur | 55 | 8 | 1,3 | 1,4 | 14 | — | Zombi 50/8/1,1 |
| Kıyı Okçusu / Shore Archer (var) | Red Archer | menzilli; kule/okçu/adacık kulesi | 30 | 6 | 1,4 | 5 | 14 | adacık kulesini ilk vuran | — |
| Kalkanlı Mızrakçı / Shield Lancer | Red Lancer (Defence) | kalkan | 80 | 10 | 1,1 | 1,6 | 22 | önden ±60° tek atış ×0,3 | Ork 80/12/1,5 |
| Fare / Rat | Rat | hızlı, tırmanır (`Climb`) | 22 | 4 | 2,6 | 1,0 | 6 | üretim binasına gider; baskında çalar | Kurt 30/5/2,2 |
| Yarasa / Bat | Bat | uçan | 20 | 4 | 2,0 | 1,0 | 8 | Okçu Kaptan'a dalar; Lancer vuramaz | Gölge Kurt 26/6/2,5 |
| İskelet Savaşçı | SkeletonWarrior | yakın sürü | 45 | 7 | 1,3 | 1,3 | 10 | — | Zombi |
| İskelet Okçu | SkeletonArcher | menzilli | 26 | 6 | 1,3 | 5,5 | 12 | — | Kıyı Okçusu |
| Kemik Büyücü / Bone Mage | SkeletonMage | şifacı | 40 | 5 | 1,1 | 4 | 18 | 3 sn'de bir 2,5 u içine %6 can | v1 Rahip |
| Ahşap Golem | WoodGolem | tank, kuşatma (yalnız kale) | 160 | 12 | 0,7 | 1,6 | 30 | zırh: ok/kule ×0,6, dolu atış ×1 | Trol 200/20/0,8 |
| İmp | Imp | sabotaj | 26 | 5 | 2,4 | 1,0 | 10 | binayı tutuşturur: 3 sn, %4/sn (`Fire_01–03`) | Bombacı |
| Örümcek / Spider | Spider | tırmanır, ağ atar | 40 | 6 | 1,8 | 3 | 14 | hedef kule/okçu 2 sn −%40 atış hızı | — |
| Zombi (var, DM2D görünümü) | Zombie | yavaş sürü | 50 | 8 | 1,1 | 1,5 | 15 | — | aynısı |
| Taş Golem | StoneGolem | tank | 220 | 15 | 0,65 | 1,6 | 40 | zırh ×0,5 | — |
| Hayalet / Ghost | Ghost | uçan | 35 | 6 | 1,6 | 1,0 | 14 | 6 sn'de bir 1,5 sn hasar almaz | — |
| Zombi Savaşçı | ZombieWarrior | elit yakın | 110 | 14 | 1,0 | 1,5 | 26 | — | Ork ×1,4 |
| Sukkubus | Succubus | menzilli büyü | 70 | 9 | 1,3 | 4,5 | 28 | 8 sn'de bir 1 Lancer'ı 2 sn durdurur | — |
| Kristal Golem | CrystalGolem | tank | 260 | 16 | 0,65 | 1,6 | 45 | zırh ×0,5, **dolu atış ×1,5** (zayıf nokta) | — |
| Vampir | Vampire | elit | 120 | 12 | 1,4 | 1,4 | 35 | %30 can çalma | — |
| İblis / Demon | Demon | elit ağır | 240 | 20 | 1,0 | 1,8 | 60 | — | bölge boss'u ≈ |
| Magma Golem | MagmaGolem | tank | 300 | 18 | 0,6 | 1,6 | 55 | ölünce 1,5 u içinde 3 sn yanma | — |
| **Kasap Kaptan** (boss 10, 40 …) | Butcher ×1,3 | boss | 450 | 20 | 0,9 | 1,8 | 120 | 15 sn'de bir Nara (+%30 hız 4 sn); Kalkan'ı yalnız dolu atış kırar | v1 Kara Kaptan |
| **Batık Şövalye** (boss 20, 50 …) | GhostKnight ×1,3 | boss | 520 | 22 | 0,9 | 1,8 | 140 | %50 canda 4 İskelet çağırır | — |
| **Kızıl Ejder** (boss 30, 60 …) | DragonRed ×1,2 | uçan boss | 600 | 24 | 1,0 | 3 | 160 | 10 sn'de bir 3 u koni ateş (kule/okçu) | Ejder Savaşçısı 320 |

- **Kademeler ve ilk görünüş** (her yeni tür "YENİ DÜŞMAN" afişiyle gelir; round2 kuralı: **1. dalgada tank/özel yok**):

  | Kademe | Akın | Yeni türler (Akın) |
  |---|---|---|
  | I Kızıl Korsanlar | 1–9 | Akıncı 1 · Kıyı Okçusu 2 · Fare 3 · Yarasa 5 · Kalkanlı Mızrakçı 6 · Ahşap Golem 8 |
  | II Lanetli Tayfa | 10–19 | Kasap Kaptan 10 · İskelet Savaşçı ve Okçu 11 · İmp 12 · Kemik Büyücü 13 · Örümcek 15 · Zombi 16 · Taş Golem 17 · Hayalet 18 |
  | III Batık Lejyon | 20–29 | Batık Şövalye 20 · Zombi Savaşçı 21 · Sukkubus 23 · Kristal Golem 25 · Vampir 27 · İblis 29 |
  | IV Ejder Armadası | 30+ | Kızıl Ejder 30 · Magma Golem 32 · bosslar sırayla döner, her turda ×1,15 boy |

- **Dalga karışımı:**
  - Oranlar: yakın %50 · menzilli %20 · hızlı/uçan %20 · tank %10 (Akın 8+; tekne başına en çok 1).
  - Özel birim (büyücü/sukkubus/örümcek) tekne başına en çok 1.
  - Kademe içinde son açılan türler daha sık gelir.
  - Zamanlı baskın gücü = en yüksek Akın − 2, 2 dalga.
- **Enum:** yeni türler `EnemyType`'ın **sonuna** eklenir (`GameData.cs:38`). `Deckhand` kalır ama kullanılmaz.
  Zombi mevcut değeri yeniden kullanır; ada modunda DM2D görünümü seçilir.

### 7.4 Okçu Kaptan (kullanıcının onayladığı v1 mekaniği aynen)
- Hasar 12·(1 + 0,15(L−1)), can 150·(…), 2 atış/sn, kritik %10 ×2, menzil 7 (+1,5 yükseklik ≥1), ok güdümlü.
- Dolu atış: 0,35 sn'de başlar, 1,2 sn'de dolar. ×2,5 hasar, 3 hedef deler, teknelere ×1,5 ve ön kalkanı yok sayar.
- Oto-ateş KK5'te açılır, elle en hızlı atışın ×0,75'i hızındadır.
- Kilit, atış yerine dokunarak geçiş ve otomatik nişan sırası v1'deki gibidir.
- Düşerse 8 sn sonra P1'de kalkar.
- Ada görünümünde Okçuluk Alanı önünde durur. Dokununca KAPTAN paneli açılır: seviye (Et), yetenekler, OTO.
- Yetenekler v1 tablosu: Ok Yağmuru L2, Ateşli Ok L4, Ağ Oku L6. Ağ, DM2D'de `Stuned` oynatır, Kızıl birimlerde kareyi dondurur.

### 7.5 Yerleşimin savaştaki rolü
- **Her ada binası** savaşta bir `BattleStructure` olur (can §5). Kalıcı can kayıtta tutulur (§7.7).
- **Gözcü Kulesi:** sefer Okçu Kulesi istatistiği (`Progression.TowerDamage/TowerUnits`), menzil 6 (+1,5 yükseklik ≥1).
  Tekneleri ve uçanları vurur. **Adacık kulesine** yakın dövüşçü ulaşamaz; yalnız menzilliler ve uçanlar vurabilir.
- **Kışla:** 1 + ⌊L/5⌋ mavi Lancer. Can 70·(1 + 0,15(L−1)), hasar 6·(…), 1 vuruş/sn, 12 sn'de yeniden doğar.
  Her Lancer 1 düşmanı tutar, uçanı vuramaz. **Toplanma bayrağı** ada görünümünde S1/S2/S3'ten birine sürüklenir ve kayıtta kalır.
- **Manastır:** mavi Monk, 4 sn'de bir 3 u içindeki dostlara ve binalara %8 can verir (`Extra/Heal Effect`). Onarım süresi ×0,5.
- **Düşman hedef kuralları** (yerleşimi okunur kılar):

  | Rol | Hedef |
  |---|---|
  | Yakın | şerit üstünde ilerler, 1,5 u içindeki bina ya da Lancer'ı vurur, sonra kale |
  | Menzilli | 5 u içine giren ilk kule, okçu ya da adacık kulesi |
  | Hızlı | en yakın üretim binası veya Ev; Fare yar tırmanır |
  | Uçan | Yarasa Okçu Kaptan'a, Hayalet ve Ejder kaleye düz çizgiyle |
  | Tank | yalnız kale; Lancer yolunu keserse onu vurur |
  | Şifacı | en kalabalık dost grubunu izler |

### 7.6 Zafer, yenilgi, etiketler
- **Zafer:** tüm dalgalar temizlenir. **Yenilgi:** Kıyı Kalesi canı 0. Okçunun düşmesi yenilgi sayılmaz. Süre sınırı yoktur.
- **Yenilgi etiketleri** (yenilgilerin ≥%90'ında biri çıkmalı). v1'den kalanlar: `beach_unwatched`, `shield_front`, `rogue_post` yerine `climber_shortcut`. Yeniler:

  | Etiket | Metin |
  |---|---|
  | `climber_shortcut` | "Fareler yardan tırmandı: yar başına kule koy" |
  | `flyer_unanswered` | "Yarasaları yalnız kuleler ve okçu vurabilir" |
  | `golem_armor` | "Golemlere dolu atış kullan" |
  | `mage_heal` | "Önce Kemik Büyücü'yü düşür" |
  | `stair_undefended` | "S3 merdiveni boştu: bayrağı oraya taşı" |

### 7.7 Onarım (karar: ücretsiz, yalnız bekleme; soru 2)
- Savaştan sonra canı eksik bina çekiç simgesi gösterir. Boştaki piyonlar koşup `Interact Hammer` oynatır.
- Süre = 20 sn + 100 sn × kayıp oran. Yıkık bina 180 sn. Manastır varsa ×0,5.
- Onarım sırasında üretim ×0,5. Yıkık bina üretmez: soluk sprite, `Dust_01/02` ve `Rock1–4` moloz gösterilir.
- Yeni savaş onarım bitmeden başlarsa bina o anki canıyla savaşır.
- Gerekçe: oynamak hiçbir zaman yok saymaktan kötü olmamalı (çevrimdışı baskın binaya dokunmuyor), ama hasarın görünür bir bedeli de olmalı.

### 7.8 Zamanlı baskın çözümü (v1 + yerleşim + %20 tavan)
- Sıklık 8 sa ± 45 dk. İlki Akın 5 kazanılınca gelir. `GameNotifications.AvoidNight` ile 09–22 arasına kaydırılır.
  Bekleyen baskın birikmez; oyun 24 sa+ açılmazsa saat durur.
- Hedef kumsal açık kumsallardan rastgele seçilir. T−30 dk'da gösterilir; oyuncu kuleyi taşıyarak karşılık verebilir.
- Formül:
  - `D = Σ_kule 2·L·kapsar + 1,5·L_Kışla·(bayrak hedef yolda ? 1 : 0,5) + 1,0·L_Okçu + 1,0·k`
  - `kapsar` = kule, hedef şeridin herhangi bir hücresine menzildeyse 1, değilse 0,5.
  - `R = 6k` · `Çalınan = 0,20 × clamp01((R − D) / R)`.
  - Yalnız toplanmamış üretimden alınır, en çok 4 saatlik.
- Canlı savunma: kazanırsa +%10 ve 10 + 2k altın alır; kaybederse çalınan ×0,5. Canlı savaşta kumsala dönen her Fare %2 çalar; toplam aynı tavana bağlıdır.
- Rapor satırı örneği: "Korsanlar B kumsalına çıktı, 2 kuleye takıldı, 60 Et çaldı."

## 8. Sayılar tek yerde (`Progression.Island` v2; v1 değerleri yorumla belirtilmedikçe kalır)
```
Map: Width 22, Height 36; Zones Z0..Z5 {KK —,2,3,4,5,6}; ZoneCost {—,150/0,300/30,500/50,800/80,1500/150}
ZoneSeconds {—,30,180,300,900,3600}; PostUnlock {P1 1, P2 4, P3 1, P4 2, P5 6}
CameraDefaultZoom 0.70, CameraMinZoom 0.45; BattleFrameWidth 22
Castle: CastleHp(k)=300+80(k−1); CastlePawns 2; WalletWood(k)=600+400(k−1); WalletMeat(k)=300+200(k−1)
Counts: House {1:1,3:2,6:3,9:4}; Tower {1:1,2:2,4:3,6:4,9:5}; Barracks 3; Monastery 6; SecondLumber 7; SecondPasture 10
PlaceCost: House 40/0, Tower 60/10, Barracks 150/40, Monastery 400/80, Lumber2 300/60, Pasture2 600/0
UpgradeFactor (wood/meat): Castle 1.0/0.3, House 0.25/0, Lumber 0.15/0.2, Pasture 0.45/0, Tower 0.5/0.1,
  Barracks 0.35/0.25, Monastery 0.4/0.15; IslandCostW[2..15] (v1); IslandTimeFactor 0.5
HousePawns(L)=2+(L>=5)+(L>=10); LumberSlots(L)=2+(L>=5)+(L>=10); PastureSlots(L)=1+(L>=5)+(L>=10); VeinSlots 1
WoodPerHour(L)=30+15(L−1); MeatPerHour(L)=15+8(L−1); SecondPastureMult 1.0; MeadowBonus 0.15
GoldVeinPerHour(k)=6+3(k−1); ProducerStorageHours 8; TreesPerSlot 2, TreeRadius 4; MeadowCells 6, MeadowRadius 3, MeadowShortMult 0.5
ArcherUpgradeMeat(L)=round5(35·1.28^(L−1))
AkinWood(N)=25+8N; AkinMeat(N)=5+2N; AkinGold(N)=20+5N; ReplayRate 0.3 (Meat 0); ReplayRewardsPerDay 5
Boat (Kenney Retina, 64 PPU): RowboatScale 1.25, LongboatScale 0.70, BargeScale 1.00; DamageSpriteAt {0.66, 0.33}
BoatSpriteByTier {I: ship 3, II–III: ship 2, IV: ship 6}; HorizonBoatScale 0.45; FlyerSwarm 3..5, FlyerTelegraph 2
ClimbSeconds 1.2; LaneAggroRadius 1.5; RangedAggro 5
Repair: Base 20, PerLostFraction 100, Destroyed 180, MonasteryMult 0.5, RepairProductionMult 0.5
Monastery: Heal 0.08 / 4 s / r 3; Lancer: Hp 70, Dmg 6, Growth 0.15, Respawn 12, Count 1+L/5
Raid: TheftCap 0.20 (v1 0.15), CoverFull 1, CoverPartial 0.5, FlagOffRoute 0.5, RatTheft 0.02; diğerleri v1
Tower: Range 6, HighGround +1.5
```
`GemEconomy`: `MeatPerGem = GoldPerGem / 5` (8), `WoodPerGem = 25`, `MeatTopUpCost`, `WoodTopUpCost` (`GoldTopUpCost` kalıbı, `GemEconomy.cs:69`).

## 9. Faz 1 kodu: koru / yeniden yaz / kaldır
Silinecek **asset yok**. `Resources/IslandVisualCatalog.asset` aynı yolda yeniden üretilir.

| Dosya | Karar | Ayrıntı |
|---|---|---|
| `Island/IslandSession.cs` | Koru + genişlet | Mod durumu (Görünüm/Savunma). `RewardFor` (:41) v2 sayıları, Et tekrarda 0, günlük tekrar sayacı |
| `Island/IslandSceneSetup.cs` | İnce önyükleyiciye indir | Kalanlar: `Awake/Start` ev sahibi kapatma (:32–63), `DisableHostLeftovers` (:103), EventSystem. **Kaldırılanlar:** `BuildTerrain/BuildFoam/BuildDecor/BuildClouds` (:167–350, renkli kare ve `IslandArt.Tree` :275). Yerlerine `IslandTerrain` prefab + `IslandNature` gelir. `CameraSizeFor` (:129) savaş çerçevesi olur |
| `Island/IslandLayout.cs` | Yeniden yaz, API'yi koru | `Rows` (:15–42) 22×36 olur. Kalan API: `HeightAt`, `Posts`, `Beaches` (şeritlerle), `Areas` (Zones olur), `PathBetween` (yeni düğümler). Testler aynı API'yi kullanır |
| `Island/IslandRaidManager.cs` | Koru, uyarla | `CrewFor` (:152) kadro tablosuna bağlanır. Öğretici (:214) üs öğreticisine taşınır. `Win` onarım listesini yazar. `ComputeDefeatReason` (:254) yeni etiketleri alır |
| `Island/IslandHud.cs` | Böl | `IslandTouchSurface` ve `IslandHoldButton` kalır. `BuildFireControls` (:134) `IslandBattleHud` olur, `BuildPrepPanel` (:107) `IslandRaidBoard` olur, `BuildTopBar` (:83) `IslandBaseHud` olur |
| `Island/IslandArcher.cs` | Koru | Onaylı mekanik. Atış yerleri yeni yerleşimden gelir. Görünüm modunda boşta durur |
| `Island/IslandEnemy.cs` | Görseli yeniden yaz | DM2D prefab + Animator tetikleyicileri, Kızıl kare setleri, boy normalizasyonu. `EnemyController` kullanımı kalır |
| `Island/RaidBoat.cs` | Mantığı koru | `IslandArt.Boat` yerine katalogdaki Kenney sprite'ı. Pruva yönüne döner, hasar karesi değişir (§7.2), rota yeni kanallardan geçer |
| `Island/IslandFx.cs` | Koru | Ok için `Units/Extra/Arrow`. Daire yedekleri kaldırılır |
| `Island/IslandArt.cs` | Daralt | `Boat` (:77), `Stairs` (:66) ve ağaç kaldırılır. `Square/Ring` yalnız yerleştirme kaplaması ve Dev bindirmesi için kalır |
| `Island/IslandVisualCatalog.cs` + `Editor/BuildIslandCatalog.cs` | Genişlet | Su animasyon karosu, köpük, gölge, yükseklik karoları, çalı/kaya/su kayası/bulut, ağaç + kütük, koyun, mavi piyon setleri, mavi binalar, Kızıl birimler (Lancer 8 yön), DM2D prefabları, Kenney tekneleri (`dinghyLarge1–3`, `ship (2,3,6)` + hasar/batık kareleri, `cannon`). İçe aktarma ayarlarını (64 PPU, Bilinear) da betik yapar. Batchmode için statik giriş: `-executeMethod BuildIslandCatalog.BuildFromCommandLine` |
| `Island/IslandBaseButton.cs` | Koru | — |
| Yeni | — | `Editor/BakeIslandTerrain.cs`, `IslandGrid`, `IslandBuildings`, `IslandEconomy`, `IslandPawns`, `IslandRaidScheduler`, `IslandRaidBoard`, `IslandBaseHud`, `IslandBattleHud` |
| Testler | Güncelle | `IslandLadderTests` (ödül/Et, yerleşim v2, tekne `IslandArt`'sız). `IslandSaveTests` v7 alanları |

**Ana Üs sistemlerini yeniden kullanma** (adaptörle, kopya yok):

| Sistem | Yöntem |
|---|---|
| `BaseCameraController` | Sınırlar sabit (`:45`, `:165–166` `BaseWorldLayout`). Bunun yerine `WorldBounds` ve `MinZoomFraction` örnek alanları eklenir, varsayılan değerler değişmez. **Dikkat:** dosyada commit'lenmemiş yerel değişiklik var (git status), önce o birleştirilmeli |
| `PlacementController` | 30 yerde `BaseMapManager`/`BuildingType`/altın çağrısı var (ör. `:68–77`, `:207`, `:277`). `IPlacementHost` arayüzü eklenir (ayak izi, `CanPlace`, bedel, işleme). Ana Üs varsayılan host olarak kalır |
| `BaseOccupancyGrid` / `BuilderPathfinder` | `IslandGrid` aynı sorguları sunar, ayrıca yükseklik, şerit ve bölge bilgisini taşır. A* (`BuilderPathfinder.cs:26–27`) `IWalkGrid` alır; komşu geçişine yalnız aynı yükseklikte ya da merdivende izin verilir. Piyonlar ve düşman tırmanma rotası bunu kullanır |
| Diğer kalıplar | `BuilderTaskSave` (Usta), `BaseMineVisual` (yığın), `GoldMineCollectIndicator`, `BuilderTaskMarker`, `BuildMenuController` (ada listesi), `BaseHudRedesign`/`LevelCrest`, `NatureObstacle` |

- **Ev sahibi sahne:** BattleScene kalır (v1 §10; Project Settings değişmez). Görünüm ve savunma aynı sahnede iki moddur.
- **Kayıt v7:** `IslandSaveData` içine binalar (id, tür, sv, x, y, can, onarım bitişi), bölge bit maskesi, Usta görevi,
  üretici son tick ve stok, işçi dağılımı, bayrak konumu, bekleyen baskın (kumsal, varış, pencere sonu) ve tekrar sayacı eklenir.
  Kart Atölyesi alanlarıyla aynı sürüm atlamasında birleştirilir.

## 10. Uygulama fazları (her biri tek başına yayınlanabilir ve kullanıcıya görünür)

| Faz | Kapsam | Görsel kabul | İşlevsel kabul |
|---|---|---|---|
| **A Yaşayan ada** | IslandLayout v2. Editör pişirici `Tools/Island/Bake Island Terrain`: yükseklik haritasından kit kurallarıyla gerçek karolar → `Resources/Island/IslandTerrain.prefab` (su, köpük, sv0, gölge, yar, sv1, sv2, merdiven katmanları; kullanıcı elle düzeltebilir; pişirici elle düzenlenmiş prefabı karma ile tanır ve sormadan üzerine yazmaz). Doğa nesneleri, hazır köy, piyon döngüleri, kamera, ada HUD'u. AKIN, Faz 1 savaşını yeni haritada Kızıl Warrior/Archer/Lancer ve Kenney tekneleriyle açar | Referans kontrol listesi (aşağıda), Dev "tüm bölgeler açık" ekran görüntüsü + varsayılan görünüm | Giriş üs görünümünde açılır. **Kod çizimi sprite testi:** ada sahnesindeki tüm `SpriteRenderer`/`Tilemap` sprite'larının asset yolu olmalı, `isl_` dokusu sıfır olmalı. Kaydırma/zum sınırda kalmalı |
| **B İnşa ve ekonomi** | `IslandGrid` + `IPlacementHost`, Ev/Oduncu/Ağıl/Kule/Okçuluk yükseltmeleri, Usta, üretim + toplama (çevrimdışı), işçi dağılımı, Z0–Z2 keşfi, kayıt v7, elmas tamamlama. Kart Atölyesi bundan sonra paralel başlayabilir | Hayalet yeşil/kırmızı, şerit kaplaması, bulut dağılması, yığınların dolması | Formül testleri (§8), 8 sa çevrimdışı üretim, eski kayıt güvenli |
| **C Aynı haritada savunma** | Binalar `BattleStructure` olur, kuleler ve adacık kuleleri, onarım, şerit rotası + tırmanma + uçanlar, Kademe I + Kasap Kaptan, DM2D katalog, Akın Panosu, yenilgi etiketleri, ödül v2 | Savaş çerçevesi tüm haritayı gösterir. DM2D boyları Warrior'la uyumlu. Piyonlar saklanır | Bot merdiveni 1–15 (§11). Yenilgilerin ≥%90'ı etiketli |
| **D Zamanlı baskın** | Zamanlayıcı, ufuk tekneleri, boru, 2 sa pencere, yerleşime duyarlı çözüm, %20 tavan, `AwayReport`, bildirim, DevMenu "+8 sa / baskını getir" | T−30 teknesi ve kırmızı kumsal işareti görünür | `IslandRaidTests`: tavan, gece kayması, birikmeme, kapsama |
| **E Derinlik** | Kışla + bayrak, Manastır, Z3–Z5, Altın Damarı, yetenekler, Kademe II–IV (siyah/sarı tekneler), bosslar 20/30, Kereste Sevkiyatı | Adacık kuleleri, Ejder | Bot 16–30, Ana Üs altın etkisi ≤%10 |
| **F Ana Üs bağları** | Ana Üs Kışlası + Köylü Evi (v1 §7) round2 bot ölçümü yeniden yapılarak (Kart Atölyesi B'den sonra) | — | round2 C-metrics ±5 puan |

**Batchmode akışı** (editör kapalıyken; her faz sonunda ajan kendisi koşar):
1. Katalog: `Unity.exe -batchmode -quit -projectPath … -executeMethod BuildIslandCatalog.BuildFromCommandLine -logFile PlaytestOutput/catalog.log`.
2. Arazi: aynı biçimde `-executeMethod BakeIslandTerrain.BakeFromCommandLine`. Elle düzenlenmiş prefab varsa durur ve logda bildirir.
3. Ekran görüntüleri ve testler: CLAUDE.md'deki PlayMode komutu, `-testFilter IslandReviewScreenshots`.
   - Yeni test görünümleri çeker: varsayılan görünüm, tam zum, Dev "tüm bölgeler açık", savaş çerçevesi, Akın 10 boss anı.
   - Çıktı `PlaytestOutput/island/*.png`; `UIReviewScreenshots`'a yazmaz.
   - Tam koşudan sonra `git checkout -- UIReviewScreenshots` yapılır.
4. Log kontrolü: yalnız `grep -E "error|Exception|Failed"`.

**Referans görsel kontrol listesi** (Faz A ve sonraki her fazda tekrarlanır; görüntüler `PlaytestOutput/island/`,
seçilenler `Docs/playtest/`):
1. Ekranın tamamında animasyonlu turkuaz su var, düz kamera rengi görünmüyor.
2. Her kara–su kenarında (adacıklar dahil) hareketli köpük var.
3. En az 2 yükseltilmiş seviye var; her güney kenarda taş yar yüzü ve altında gölge görünüyor.
4. 3 merdiven gerçek karolarla çizili.
5. ≥24 ağaç (≥2 tür, sallanıyor), ≥8 çalı, ≥6 kaya ve ≥6 su kayası var.
6. ≥4 koyun otluyor ya da yürüyor; ≥3 piyon farklı işler yapıyor.
7. Kale zirvede; ≥3 mavi ev ve ≥1 adacıkta mavi kule görünüyor.
8. Kenar bulutları ve kilitli bölge bulutları görünüyor.
9. Ölçek tutarlı: piyon ≈1 hücre, ev 2, kale 5 hücre geniş. DM2D canavarları Warrior'dan ≤1,6 kat büyük (boss hariç).
   Akıncı Teknesi ≈2,5 u boyunda, Tower'dan kısa. Hiçbir düşman teknesi mavi değil.

## 11. Telemetri ve doğrulama (ORTA)
- **Savaş botları:** v1 profilleri (`islandGood`, `islandCasual`, `islandTowerOnly`), v1 §9 kazanma tablosu aynen kalır.
  Beklenen durum: KK = ⌈N/3⌉, kuleler KK−1, en az 1 kule hedef kumsalı kapsıyor. Yeni ölçütler:
  - medyan zaferde yıkılan bina 0;
  - Akın başına onarım beklemesi medyanı ≤60 sn;
  - Akın 8+ için `islandTowerOnly` golem yenilgisinin ≥%50'sinde `golem_armor` etiketi çıkmalı.
- **Ekonomi** (`Tools/economy_sim.py` + canlı `meat_flow`/`wood_flow`/`gold_to_main` olayları):

  | Ölçüt | Hedef | Sapınca düzeltme |
  |---|---|---|
  | Günlük Et, gün 1 / 2 / 3 | 150–250 / 400–550 / 500–700 | `MeatPerHour` |
  | Et içinde Akın payı | ≤%15 | `AkinMeat` |
  | Kararlı altın:Et (gün 7+, KK = MB) | 3,5–5 | MB12+ altında 3,5 ise `SecondPastureMult` 0,6 |
  | Odun cüzdanı tavanda geçen gün oranı | ≤%50 | Odun bedelleri ×1,2 |
  | Adadan Ana Üs'e altın / Ana Üs günlük altını | ≤%10 | `GoldVeinPerHour` |
  | Çevrimdışı çalınan oran medyanı / p90 | ≤%6 / ≤%14 (tavan %20) | `R` katsayısı |
- **"Üs mü, savaş ekranı mı" ölçütü:**
  - eylem içeren ada oturumu (inşa/toplama/taşıma/keşif) ≥%80;
  - Akın başlatılan oturum ≤%60;
  - medyan ada oturumu 2–5 dk;
  - hedef kumsalı kuleyle kapsanmış baskın oranı günler içinde artmalı (yerleşim öğreniliyor).
- **Performans:** ≥20 DM2D birimi ekrandayken test tabletinde p10 ≥45 fps. Altında kalırsa eşzamanlı DM2D tavanı 20'ye iner.

## 12. Çakışmalar ve riskler (üstüne yazılmadı, işaretlendi)
- **`card_workshop.md` §7 (kabul edildi) "Akın Et 15 + 4N":** v2'de **5 + 2N**, tekrarda 0. Kullanıcının yeni
  geri bildirimi 3'e dayanıyor; onay gerekli. 2. Ağıl KK10 kararı korundu, ayar düğmesi eklendi.
- **v1 §4 "DungeonMonsters yalnız Hayalet Gemi etkinliğinde":** kullanıcı kararıyla kalktı; Hayalet Gemi etkinliği şimdilik çıkarıldı.
  Stil farkı riski: boyalı DM2D ile piksel Tiny Swords yan yana. Boy normalizasyonu ve y-sıralaması uygulanır; görsel kontrol 9.
- **v1 sabit parseller → serbest yerleşim + ayrılmış şeritler.** Bu benim (tasarımcı) değişikliğim. Okunurluk merdiven
  darboğazı ve şeritlerle korunuyor.
- **v1 Ambar ve "Ada Ticareti" kaldırıldı:** yerlerine kale tavanı ve Altın Damarı geldi (soru 3).
- **"Kışla adadan sonra":** Ana Üs Kışlası Faz F'de (sefer dengesi). Ada Kışlası Faz E'de, yalnız adayı etkiler (soru 4).
- **AI_README.md:70** (tek kahraman Warrior, Mızrakçı seçimi kalkacak): Okçu Kaptan yalnız adaya ait. Mavi Lancer yalnız Kışla müttefiki, kahraman değil.
- **Paketler:** DM2D için `com.unity.2d.animation` 13.0.4 ve `psdimporter` 12.0.1 zaten manifest'te. Değişiklik gerekmiyor.
  Kenney Pirate Pack CC0. **Stil riski:** düz vektör üstten tekneler piksel 3/4 Tiny Swords'un yanında durur. Bunu
  yumuşatmak için tekneler yalnız suda kalır, güvertede birim gösterilmez, ölçek binalardan küçük tutulur. Görsel kontrol 9.
- **Riskler:**
  - `IPlacementHost` refaktörü Ana Üs yerleşimini bozabilir. Faz B'de `UiFixPassTests` ve yerleşim testleri tam koşulur.
  - Pişirilmiş prefab ile çalışma anı yerleşimi kayabilir. Test: her `#`/`H` hücresinde karo var, yürünebilirlik haritası eşleşiyor.
  - DM2D iskelet animasyonu mobil maliyeti (§11 fps ölçütü).
  - Tekrar Akın ile altın kasma riski `ReplayRewardsPerDay` ile kapatıldı (yeni kural).
  - Kayıt sürümü Kart Atölyesi ile çakışabilir. Tek sürüm atlamasında birleştirilir.

## 13. Açık sorular (kullanıcıya; tasarımı gerçekten değiştirenler)
1. **Hazır köy:** ilk ziyarette kale, Oduncu, Ağıl ve Okçuluk Alanı kurulu ve piyonlar çalışır halde mi gelsin (önerim:
   ilk bakışta "yaşayan ada"), yoksa ada yalnız kaleyle boş mu başlasın (daha çok kurma hissi, ama ilk görüntü sönük)?
2. **Onarım:** ücretsiz ve yalnız bekleme (önerim, §7.7) mi, yoksa Odun bedelli mi (savaş daha ağır, daha cezalandırıcı)?
3. **Altın Damarı:** ada Ana Üs'e günde ≈%8 altın versin mi (görünür, §6), yoksa ada altını yalnız Akın/baskından mı gelsin?
4. **Kışla:** "adadan sonra" kararı Ana Üs Kışlası için mi (önerim; ada Kışlası Faz E'de kalır), yoksa ada Kışlası da mı ertelensin?
5. **Akın Eti 5 + 2N** (kabul edilen 15 + 4N yerine) onaylanıyor mu?

## 14. Varsayılan kararlar (12 Kasım 2026, kullanıcı aksini demedikçe)
1. Ada **hazır küçük bir köyle** başlar (Kale + 1 Ev + Ağıl + Oduncu): ilk görüş referans görüntüye benzesin.
2. Akın sonrası onarım **ücretsiz, onarım süresince üretim yavaş** (cezasız ama anlamlı).
3. Altın damarı Ana Üs'e günlük ~%8 ek altın: **evet** (görünür, küçük).
4. "Kışla adadan sonra" = **Ana Üs Kışlası** (Faz F); ada Kışlası Faz E'de.
5. Akın Et ödülü **5 + 2N**, tekrar oynamada 0 (kullanıcı "akından çok Et geliyor" dedi).

## 15. Faz A uygulama notu (12 Kasım 2026): belgeden sapmalar
- **Merdivenler yana dikilir.** Kit §4.3 merdivenleri plato YANINDA (SL/SR, 2 hücre) tanımlar; §4 haritasındaki ortadaki 2 geniş
  `HH` merdiven karo kuralıyla çizilemez. S1 zirvenin doğu ucunda (c15, r7), S2 yaylanın batı (c3, r13), S3 doğu (c18, r13) alt köşesindedir.
  Yürüyüş yolları buna göre yeniden çizildi (`IslandLayout.Beaches/PathBetween`). Yarlar türetilir (`IslandTerrainRules`), yazılmaz.
- **Harita çentikleri:** yayla batı (r9–11, c4–5) ve doğu (r11–12, c16–17) çentik, koy (r14–15, c9–12) suya bakan yar gösterir.
- **Kenney gemileri 66×113 / 20×38 px** (belgedeki boyların yarısı): son boy (Sandal 1,48 · Akıncı 2,47 · Mavna 3,53 u) korunarak ölçekler
  `Progression.Island` içinde 2,5 / 1,4 / 2,0 oldu. Pruva aşağı bakar; dönüş = hız açısı + 90°.
- **Okçu koşu hızı 4 → 9 u/sn:** harita 14×25'ten 22×36'ya büyüdü; atış yeri geçişleri 1–3 sn kalsın.
- **Kamera:** Ana Üs kamerası (`BaseWorldLayout`) değiştirilmedi; ada için ayrı `IslandCameraController` (kaydırma/zum/savaş çerçevesi) var.
- **Kenar bulutları ekrana sabittir** (kamera ile gelir, boyu zumla orantılı); bölge bulutları dünyaya sabittir ve bölge açılınca kaybolur.
- Kodla çizilmiş SpriteRenderer yoktur; `IslandArt` yalnız arayüz ilkelleri (düğme dairesi, halka, dolgu) için kaldı.
- Komutlar: `-executeMethod BuildIslandCatalog.BuildFromCommandLine`, `-executeMethod BakeIslandTerrain.BakeFromCommandLine [-force]`,
  test `-testFilter IslandReviewScreenshots` (çıktı `PlaytestOutput/island/`).
- **Bölge sınırı (A.2 sonrası):** hazır köyün tamamı (Okçuluk, Ağıl, altın taşları dahil) Z0'dadır: Z1 yalnız doğu şeridi (c≥16, r≤22),
  Z2/Z4/Z5 r≥23. Bölge bulutu Z0 binasının 4,3 birim yakınına konmaz; kenar bulutları ekran dışına taşar (köyü örtmesin).
  Açılış görüntüsü: `Docs/playtest/island_v2_phaseA_default.png`.


## 16. Faz C uygulama notu (12 Kasım 2026): belgeden sapmalar ve seçimler
- **Katalog:** DM2D prefabları `IslandVisualCatalog.monsters` (`MonsterEntry`: anahtar = `EnemyType` adı, prefab referansı, ölçülmüş gövde yüksekliği, hedef boy, portre) içinde tutulur; `Resources.Load(yol)` yoktur.
  `BuildIslandCatalog` prefabı editörde ölçer. Kullanılan canavarlar: **Fare, Yarasa, Ahşap Golem, Kasap Kaptan** (§7.3 Kademe I + boss 10). Diğer DM2D canavarları Kademe II–IV (Faz E).
  Gerçek bağımlılık: 4 prefab + 4 PSB (Butcher PSB'si `MeatGolemPSB`) + 4 AnimatorController + 40 klip + `CleaveEffect.png`; yalnız bunlar commit'lendi (`Mighty Heroes` ve kalan DM2D klasörleri değil).
- **Boyut:** DM2D PPU 100, prefab kendi ölçeğinde (0,55–0,9) gelir; çalışma anı ölçeği = hedef boy / ölçülmüş gövde. Hedef boylar: Fare 0,5 · Yarasa 0,75 · Golem 1,5 · Kasap 1,3 ×`BossScale` (1,5 → **1,3**, tasarım). Kızıl Warrior gövdesi ≈ 1,2 u olduğundan Golem Warrior'dan ≈1,25×, boss ≈1,35× (ekran görüntüsü `13_enemy_scale_lineup`). Tüm DM2D prefabları **sola** bakar (`facesRight=false`, görsel doğrulandı); yön `localScale.x` ile çevrilir.
- **SortingGroup:** kökte; `IslandYSort` grup varsa `sortingOrder`'ı grupta sürer (iç parça sırası korunur).
- **Animasyon:** `animator.Play(durumAdı)`, parametre yok. Golem takısı `Golem` (`MoveGolem`…). Durumlar: Idle/Move/Attack/SpecialAttack(Kasap Nara)/Death; Fare yar hücresindeyken `Climb`. Ölüm: DM2D `Death` klibi + solma + `Dust_02`; Tiny Swords (ölüm karesi yok): `Dust_01` + solma. `EnemyController.SuppressDeathEffect` bunun için eklendi (ada dışı davranış değişmedi).
- **Kızıl kadro:** Akıncı = Kızıl **Warrior** (eskiden Lancer görünümü), Kalkanlı Mızrakçı = Kızıl **Lancer** (Defence karesi), Kıyı Okçusu = Kızıl Archer. `EnemyType` sonuna `ShieldLancer, Rat, Bat, WoodGolem, ButcherCaptain` eklendi. Purple/Yellow yalnız Kademe III–IV (kullanılmadı).
- **Kadro formülü (`IslandWaves`):** deterministik; ilk görünüşler belgeye uyar (Akıncı 1 · Okçu 2 · Fare 3 · Yarasa 5 · Mızrakçı 6 · Golem 8 · Kasap 10). Golem yalnız Uzun Tekne+ üstünde, ilk dalgada yok, tekne başına ≤1, iki tekneden birine dönerek. **Yarasalar** teknesiz gelir: 2. dalgadan itibaren 3–5'lik sürü, kenardan, 2 sn önce `IslandFx.Telegraph` yön oku.
- **Hedefleme (`IslandTargeting`, §7.5):** yakın → şeritte 1,5 u içindeki bina, sonra kale; menzili → 5 u içinde kule/okçu; Fare → 9 u içindeki en yakın üretim binası (yarı düz çizgi, `Climb`); Yarasa → okçu; Golem → yalnız kale. Kalkanlı Mızrakçı önden ±60° tek atışı ×0,3, Golem tek atışı ×0,6 alır; **dolu atış ikisini de yok sayar** (`IslandEnemy.ArrowDamage`). Boss'un Kalkan'ı (`Kalkan'ı yalnız dolu atış kırar`) uygulanmadı: Kasap Kaptan yalnız Nara'yı kullanır.
- **Binalar (`IslandBuilding`):** hazır köyün her binası `BattleStructure` (can §5), seviye `IslandBuilding.LevelOf` arkasındadır (Faz B gerçek seviyeyi buraya bağlar). Hazır köy sabit seviyeleri: Ev/Oduncu/Ağıl 1, Okçuluk = KK, **adacık kulesi 1 + KK/2** (iki kule tek başına tüm tekne yollarını kapatmasın diye; KK seviyesi çok kolay yapıyordu).
  Kule `IslandTower`: sefer Okçu Kulesi eğrisi, menzil 6 (+1,5), tekne/kara/uçan hedef alır. Hazır köyde **mainland kulesi yok** (belgedeki "KK−1 kule" Faz B yerleştirmesine kalır); bot ölçümü bu yüzden iki adacık kulesiyle yapıldı.
- **Onarım (§7.7):** savaş sonrası `20 + 100 × kayıp oran` sn, yıkık 180 sn, Manastır yok (×0,5 hazır, kullanılmıyor). Kayıt `IslandSaveData.buildings` (id, can oranı, bitiş UTC tick) — eski kayıt boş listeyle açılır. Bina yıkılınca yok olmaz: soluk sprite + Tiny Swords `Fire` kalıntısı; hasarlı binada Tiny Swords çekiç simgesi, sayaç ve çekiç vuran Usta piyon. Üretim yok → yalnız durum+zamanlayıcı; `IslandRepair.ProductionMultiplier()` (×0,5) Faz B için hazır. **Kale istisnası:** savaş sonrası tam canla döner (yenilgiden sonra Akın tekrar denenebilsin).
- **Ödül v2:** `AkinMeat = 5 + 2N`, yalnız ilk zaferde; tekrarda Et **0**, Odun/altın %30; günde en çok 5 tekrar ödülü (`ReplayRewardsPerDay`, kayıtta `replayDay/replaysToday`). İlk 15 Akın'ın toplam Eti 315.
- **Yenilgi etiketleri (`IslandDefeat`):** `flyer_unanswered, golem_armor, boss_unbroken (yeni), climber_shortcut, shield_front, stair_undefended (B/D kumsalı, S3), beach_unwatched, beach_overrun (izleniyor ama bastırıldı), multi_beach`. Yalnız hiç hasar yoksa `castle_fell` (etiketsiz). `mage_heal` Kemik Büyücü gelince (Faz E). `stair_undefended` metni bayrağa (Kışla, Faz E) değil kule/atış yerine yönlendirir.
- **Akın Panosu (`IslandRaidBoard`):** 4 kartlık merdiven (N, güç, dalga/tekne, düşman portreleri, kumsal iğneleri A–D, ödül, durum), Tiny Swords kâğıt/kurdele/düğme sprite'ları; AKIN düğmesi açar. Zamanlı baskın kırmızı kartı Faz D.
- **Savunma modu:** kamera `BattleFrameWidth` ile tüm haritayı gösterir; piyonlar gizlenir; yenilgide kalan düşman/tekneler donar, ada görünümüne dönerken temizlenir.
- **Efektler:** Tiny Swords `Dust/Explosion/Fire` (bina yıkımı, ölüm, kalıntı), Kenney `explosion1–3` (batan tekne) ve `fire1–2` (≤%33 canlı teknede güverte yangını). Kenney `cannonBall` kataloğa alındı, "Kıyı Topu" için ayrılı (kullanılmıyor).
- **Bot (`IslandBot`):** Good (atış yerine koşar, oto + dokunuş, zırhlıya dolu atış) / Casual (yalnız oto) / None. Ölçümler `IslandPhaseCTests` ve `PlaytestOutput/island/ladder_*.txt`.
- **Bot ölçümü (`IslandPhaseCTests`, ×6 hız, iki adacık kulesi + okçu; ayrıntı `PlaytestOutput/island/ladder_*.txt`):**
  - İyi bot: Akın 1–9 hepsi **kazanılır** (9'da 2–4 bina yıkılır, kale sağlam); **Akın 10 (boss) KK4'te 0/8 kaybedilir** (`shield_front`/`beach_unwatched`), **gerçek oyun durumunda (ada KK ≥5'te açılır) KK5'te 3/3 kazanılır**, 4 bina yıkılır (onarım gerekir).
  - Sıradan bot (yalnız oto-ateş, yer değiştirmez): 1–4 kazanır, 5–10 kaybeder (`stair_undefended`). Yalnız kuleler (okçu yok): 4 kazanır, 1/7/10 kaybeder. Savunmasız (kule+okçu yok): Akın 1–9 hepsi kaybedilir.
  - Yenilgi etiketi kapsamı: **18/18 (%100)**; testte rastgele hasar dağılımlarıyla ≥%90 şartı ayrıca doğrulanır.
  - Akın 11–15 ölçülmedi: Kademe II türleri (İskelet, İmp, …) Faz E'dedir; 10–19 şimdilik Kademe I kadrosu + Kasap Kaptan'la oynanır. "Bot merdiveni 1–15" ölçütü Faz B (KK−1 yerleştirilmiş kule) ve E ile tamamlanır.
