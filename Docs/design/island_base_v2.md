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

## 17. Faz B uygulama notu (12 Kasım 2026): belgeden sapmalar ve seçimler
- **Yerleştirme (`IslandGrid`, `IPlacementHost`):** arayüz (`IslandGrid.cs`) ve ada ev sahibi (`IslandPlacementHost`, `IslandPlacementController`) eklendi; **Ana Üs `PlacementController` değişmedi** (refaktör riski, §12). Ortak parçalar yeniden kullanıldı:
  `GhostPointerDrag` (iki isteğe bağlı delege: `OverUi`, `EdgeScroller`; boşken Ana Üs davranışı aynı), `DecorPlacementController.MakeButton`, `ReferenceUiVisuals.ToastSprite`, `BaseCameraController.GhostDragActive`. Ada kamerasına `EdgeScroll/EnsureVisible/FocusUpper` eklendi.
  Hayalet = gerçek bina sprite'ı (yeşil/kırmızı ton); zemin kaplaması = Tiny Swords `RegularPaper` kâğıdı hücreye ölçeklenip renklendirilir (kodla çizilmiş sprite yok). Kırmızı şeritler yalnız yerleştirme modunda var.
  Geçersiz: su, yar yüzü/merdiven, merdiven çevresi (1 hücre), yürüyüş şeridi + kumsal iniş çevresi, ağaç/altın taşı/koyun çayırı (çalı ve kaya süstür, engel değil), kilitli bölge, çakışma, adet tavanı, farklı yükseklik.
  **Yerleşim alanı dar:** boş adada (KK15, tüm bölgeler açık) geçerli sol-üst hücre sayısı Ev 29 (Z0 29 · Z1 1 · Z2 1), Oduncu 23 (Z0), Ağıl 13 (Z0), Kule 29 + adacıklarda 1–2 (test günlüğü `[IslandGrid]`). Z1/Z2 keşfi şimdilik yeni bina yeri değil, ağaç/çayır sayısı ve kule yeri getirir; bölge sınırları ve süs yoğunluğu Faz E'de yeniden bakılmalı. Adacık (kaleden ayrı kara) yalnız Kule. Oduncu ≥1 ağaç (4 u), Ağıl ova/yayla + ≥1 çayır hücresi (3 u) ister; eksik doğa çarpanla cezalandırılır (§5).
  **D kumsalı yürüyüş yolu** hazır Ağıl'ın üstünden geçtiği için r20 boyunca yeniden çizildi (`IslandLayout.Beaches[3]`; Akın 15+, ölçüm etkilenmedi). Test: hazır köyün her binası geçerli hücrede.
- **Taşıma:** bedava ve anında, ama **bina panelindeki TAŞI düğmesiyle** (basılı tutma `LongPressHandler` yapılmadı). Savaşta kilitli.
- **Maliyet yalnız Odun + Et** (§6); bina/bölge bedelinde altın yok. Altın yalnız Faz E'deki Altın Damarı'ndan gelecek (görev metnindeki "altın" bu yüzden uygulanmadı). Elmasla tamamlama: eksik Et ⌈eksik/8⌉, Odun ⌈eksik/25⌉ (`GemEconomy.MeatTopUpCost/WoodTopUpCost`, `MeatPerGem` o zamana kadar kodda yoktu, eklendi); Usta bitirme `GemEconomy.FinishCost` (1 dk altı bedava).
- **Kıyı Kalesi hâlâ Ana Üs'ü izler** (`KeepFollowsMainBase`): kale yükseltmesi ve KK şartlı bölge kapıları fiilen hep açık (KK ≥ 5). Bina seviye tavanı = KK, adet tavanları §8 tablosu (`MaxCount`). **Okçuluk Alanı yükseltilmez** (seviyesi Okçu Kaptan = KK); `ArcherUpgradeMeat` formülü ve testi var, KK'dan bağımsız Okçu seviyesi gelince kullanılacak. `ArcherUpgradeMeat(14)` = 865 (belgedeki 870 yaklaşık; formül 5'e yuvarlar).
- **Bölge keşfi:** Z1 (150 Odun, 30 sn) ve Z2 (300 Odun + 30 Et, 3 dk) Usta görevi olarak açılır; bulut dokunuşu ya da İNŞA ET menüsündeki satır paneli açar. Bulutlar 1,5 sn'de dışa süzülüp solar (`IslandZoneCloud.Dissolve`). Z3–Z5 bulutlu kalır (dokununca "sonraki güncellemede" der). Kumsal kapısı bölgeye değil Akın numarasına bağlı kaldı (Faz C). **v6'dan gelen oyuncunun bölge maskesi yalnız Z0**: Faz C'de bulutlar KK'ya göre kendiliğinden açılıyordu, artık Z1/Z2'yi ödeyip açar (ucuz). Not: Faz A sınırlarıyla Z1 yalnız dar bir doğu şeridi ve E adacığı getiriyor (yeni bina yeri: yalnız Kule); asıl yerleşim alanı Z2'de. Z1 sınırı Faz E'de gözden geçirilmeli.
- **Kayıt v7 (`IslandSaveData`):** `villageSeeded`, `placed` (id, tür, seviye, sol-üst hücre, stok, son tick, elle işçi), `nextBuildingSerial`, `zoneMask`, `task` (Usta görevi). v5/v6 kaydı boş listeyle açılır; ilk açılışta hazır köy **Faz C seviyeleriyle** (Kule 1 + KK/2, Ev/Oduncu/Ağıl 1, stok 0) `placed`'a yazılır: kayıp ve bedava üretim yok. Eski onarım kaydı (`buildings`) ayrıdır ve aynı bina kimliklerini kullanır (hazır köyde `Tür@col,row`, yeni binada `Tür#sıra`).
  `IslandBuilding.LevelOf` kayıttan okur (Kale = KK, Okçuluk = Okçu, diğerleri `placed`).
- **Üretim:** `IslandEconomy.Accumulate` UTC tick ile (saat `IslandRepair.Clock`, varsayılan `GameClock`: telefon saatini geri alma hilesi üretmez). Saatlik = tam kadro × işçi/yuva × doğa çarpanı (Oduncu ağaç/(2×yuva), Ağıl çayır ≥6 yoksa ×0,5) × 2. Ağıl çarpanı. Depo = 8 sa **tam kadro** üretim (doğa kısıtı depoyu küçültmez). **Onarım çarpanı gerçek üretime işler:** `IslandRepair.EffectiveSeconds` onarım penceresinde (en geç biten onarıma kadar) süreyi ×0,5 sayar; çevrimdışı hesap da buradan geçer. Cüzdan tavanı yalnız **toplamada** uygulanır; Akın ödülü tavanı aşabilir (ödül kaybolmasın).
- **İşçi dağılımı:** piyon havuzu = 2 + tamamlanmış Ev başına 2/3/4; elle ayar (−/+) önce, kalanlar en düşük doluluk oranlı üreticiye (eşitlikte Ağıl). Panelde "İşçi 2/3"; piyon sahneleri dağılıma göre yeniden kurulur (Oduncu piyonları kendi kampının ağaçlarına, çoban kendi Ağıl'ın koyunlarına, Usta şantiyeye). Boştaki piyon ≤3, kalede dolaşır. **Madenci piyonu yalnız Dev "hepsi açık" görünümünde** (Altın Damarı Faz E'de; ada artık olmayan bir kaynağı göstermesin).
- **Yığınlar:** Oduncu/Ağıl yanında 0–6 gerçek Tiny Swords Odun/Et sprite'ı (doluluk oranına göre), dokununca toplanır (yukarı uçan simge + bildirim); cüzdan doluysa "depo dolu". Yığınlar piyon köküne bağlıdır (savunmada gizli).
- **Ada Ustası:** tek görev (`IslandBuilderTask`), süreler TimingConfig × 0,5 (§5 listesiyle birebir; test), yeni bina 5 sn. İnşaat alanı = gerçek bina sprite'ı soluk + Tiny Swords çekiç simgesi + süre; Usta piyon şantiyeye koşup çekiç vurur. Savaşta görev bitmez (savaş sonrası tamamlanır). Boştaki Usta için kalede çekiç simgesi (§2.1) **yapılmadı**.
- **HUD:** dock **İNŞA ET · AKIN** (KAPTAN düğmesi Faz E). Üstte canlı Et/Odun sayacı (cüzdan tavanında sarı). Bina/kale/bölge panelleri Tiny Swords kâğıt, kurdele, düğme sprite'larıyla; panel açılınca kamera binayı panelin üstüne süzer. Kaydırma bırakılışı dokunuş sayılmaz (24 px eşik).
- **Ekonomi simülasyonu (`IslandEconomySimTests`, gerçek `IslandEconomy` kodu, sanal saat; Ana Üs altını varsayımı KK5 ≈ 3.500/gün, başlangıç hediyesi 150 Odun + 30 Et; fazla Et Kart Atölyesi'ne harcanır, cüzdan dolup üretimi durdurmasın):**
  - Sıradan (günde 2 oturum, 8 sa depo tavanı yüzünden ≈16 sa yakalanan üretim): gün 1 Et 211 (+30 hediye) → **16,6 : 1**; gün 2 381 → 9,2 : 1; gün 4 496 → 7,1 : 1; **gün 7 Et 752 → 4,7 : 1** (Ağıl/Oduncu Sv5, KK tavanı). Akın Eti payı gün 3'ten sonra 0.
  - Düzenli (günde 3 oturum, ≈21 sa): gün 1 **13,7 : 1**, gün 7 **3,4 : 1** (Et 1.034/gün). Et erken çok kıt, gelişince ≈4 : 1'e iner; test aralık denetimidir (10–30 / 3,5–6; düzenli 2,8–6).
  - Bu sonuç belge tablosundaki KK1'den başlayan gün 2–3 sayılarıyla birebir değil: ada Ana Üs 5'ten açıldığı için seviye tavanı (KK) bağlayıcı değil; erken Et'i belgedeki 400–550 aralığında tutan şey 8 sa depo tavanı ve işçi yuvasıdır. Gün 2 Et 381, aralığın (380–600) alt ucunda.
- **Testler:** `IslandPhaseBTests` (formüller §8, bedeller/süreler, elmas, 8 sa çevrimdışı + tavan + onarım ×0,5 + kısmi onarım, işçi dağılımı, yerleştirme geçerliliği, hazır köy geçerliliği, sahnede inşa → yükseltme → elmasla bitirme → yığın/toplama → bölge keşfi + bulut dağılması), `IslandEconomySimTests`, `IslandSaveTests` (v7 gidiş-dönüş, v6→v7 + tohumlama, v5), `IslandReviewScreenshotsB` (`PlaytestOutput/island/B*.png`).

## §18 Ada cilası (12 Kasım 2026)
- **Yasak hücreler kırmızı:** proje doğrusal renk uzayında; %55 saydam kırmızı yeşil çimle kahverengiye kayıyordu.
  Şerit/merdiven kaplaması artık %90 opak kırmızı, hücreden biraz küçük (`IslandPlacementController.LaneColor`).
- **Z1/Z2 genişledi (kullanıcı kararı: sınırları yeniden çiz):** orta çayır (r15–22) bölündü: c ≤ 8 → Z2, c ≥ 14 → Z1
  (`IslandLayout.MeadowSplitWest/East`). Geçerli Ev hücresi Z1 1 → 3, Z2 1 → 6; Z0 22. Hazır köy (Ağıl c12–13 dahil) Z0'da.
  Z1 hâlâ şeritle dolu: daha fazla yer için sınır Ağıl'ı kilitlerdi; kalıcı çözüm Faz E'de kara genişletme.
- Eski kayıtta oyuncunun binası yeni kilitli bölgeye düşerse o bölge yüklemede bedava açılır (`IslandEconomy.OpenZonesUnderBuildings`;
  hazır köy kuleleri sayılmaz). Bölge bulutları yeniden pişirildi; pişirici artık bölge haritasını hash'e katar, köy çevresi boşluğu 4,3 → 3,2.
  Bilinen küçük kusur: Ağıl'ın sağ kenarına bir Z1 bulutu biraz biniyor. Görüntü `Docs/playtest/island_v2_polish_zones.png`.
- **Kereste Sevkiyatı (Odun gideri):** "HEMEN BİTİR" penceresinde reklamın üstünde üçüncü seçenek: 100 ada Odun'u süren Ana Üs
  inşaatını 10 dk kısaltır, günde 3 (`GemEconomy.LumberShipment*`, `IslandEconomy.TryShipLumber`, kayıt `lumberShipDay/Count`).
  Ana Üs 5'ten (ada) önce görünmez; Odun yetmezse soluk. Günlük gider ≤ 300 Odun: depo tavanı (KK1 600) hâlâ 4. günden sonra
  dolabilir; bot ölçümünde yeterli değilse sevkiyat hakkı ya da Odun bedeli artırılabilir. Testler `LumberShipmentTests` (3).
  Ayrıca düğmelerdeki "▶" kaldırıldı (Cinzel'de glif yok, "□" görünüyordu).

## §19 Faz D uygulama notu (12 Kasım 2026): zamanlı baskın
- **Zamanlayıcı (`Island/IslandRaidScheduler.cs`, saf mantık):** saat `IslandRepair.Now` (GameClock) ya da testten enjekte; yerel saat `ToLocal` ile (testte sabit ofset). Kayıtta `nextRaidTicks` = bekleyen baskının varışı T0 (0: yok), `raidBeach/raidSeed/raidSerial/raidsResolved/raidSeenTicks`, okunmamış rapor `raidReport*`. **Kayıt sürümü 8'de kaldı** (yalnız yeni alanlar; eski kayıtta hepsi 0/boş, ilk Tick'te Akın ≥ 5 ise baskın planlanır). Sayılar `Progression.Island` içinde (Faz D bloğu).
- **Ritim:** 8 sa ± 45 dk, `NotificationPlanner.AvoidNight` ile 09–22 yerel; ilki Akın 5 kazanılınca (şimdiden +8 sa). Hedef kumsal tohumdan, en yüksek Akın'a göre açık kumsallardan. **Birikmez:** pencere (T0..T0+2 sa) bitince çözüm olur ve sonraki baskın *şimdiden* (pencere bitişinden değil) planlanır. **24 sa+ kapalı kalırsa** saat durur: kapalı kalma payı (süre − 24 sa) planlı varışa eklenir (gece kuralı yeniden uygulanır). `GameManager.Update` ~2 sn'de bir `TickIfAny` çağırır (ada dışında da çalışır); `AwayReport.Begin` de Tick eder, "Sen yokken" penceresine "Baskın: ..." satırı eklenir; ada HUD'u okunmamış raporu toast ile bir kez gösterir.
- **Çözüm (§7.8 birebir):** `D = Σ kule 2·L·kapsar + 1,5·L_Kışla·(bayrak?1:0,5) + L_Okçu + k`, `R = 6k`, `çalınan = 0,20 × clamp01((R−D)/R)`. Kapsama: kule menzili (6, yar +1,5) hedef kumsalın `walkPath` şeridinin (0,5 u örnekli, hücre merkezi) herhangi bir hücresine yetiyorsa 1, yoksa 0,5. Kışla ve bayrak Faz E'de olmadığından 0 / yolda değil. Yalnız Oduncu/Ağıl'ın toplanmamış stoğu, üretici başına en çok 4 saatlik üretim (`FullRate × 4`) üzerinden; çevrimdışı çözümde stok pencere bitişi anına getirilip hesaplanır. **%20 tavan** üç yerde zorlanır (`TheftFraction`, `ApplyTheft` kırpması, canlı toplam) ve testlidir.
- **Canlı savunma:** `IslandRaidManager.StartRaid()`; güç = en yüksek Akın − 2 (`RaidPower`), 2 dalga, tüm tekneler hedef kumsala; Akın ilerlemesi/ödülü/`lastAkin` değişmez. Kazanırsa çalıntı yalnız kumsala çıkan Fare'lerden (her Fare %2); bekleyen üretime +%10 (depo tavanına kırpılır) ve 10 + 2k Ana Üs altını; kaybederse çevrimdışı çalıntının ×0,5'i + Fare payı, toplam ≤ %20. "Kumsala dönen Fare" = **bir üretim binasına vurabilen Fare** (sahnede Fare'nin geri dönüşü yok; `IslandStats.RecordEnemyHit` kancası). Çekil (`Abandon`) baskını bekletir; pencere dolarsa çevrimdışı çözülür.
- **Görünüm:** `Island/IslandRaidVisuals.cs` — T−30'dan itibaren hedef kumsalın açığında 1–3 Kenney teknesi (ilki kırmızı-haç gemi, diğerleri sandal; `HorizonBoatScale` 0,9: §8'deki 0,45'in Faz A'da yarım boy gelen sprite'lara göre ×2'si), kumsalda kırmızı Tiny Swords oku + "B KUMSALI / BASKIN! B" yazısı; savunmada/Akın'da gizli; pencere açıkken tekneye/işarete dokunmak savunmayı başlatır. Sağ üstte rozet ("Sakin · 3 sa 12 dk" / "Baskın geliyor · 24 dk" / "BASKIN! · 1 sa 48 dk"; dokununca bilgi ya da savunma). Akın Panosu'nda üstte kırmızı kart (başlık, güç, süre, pencere açıkken SAVUN düğmesi). Boru sesi: `AudioLibrary.raidHorn` alanı eklendi ama **projede uygun ses dosyası yok** (boş: sessiz); T−30'a girişte çalınır.
- **Dev:** "BASKINI GETİR" baskını T−30'a alır; mevcut "ZAMAN +1/+8 SAAT" artık baskın zamanlarını ve ada üretim damgalarını da kaydırır, ardından Tick eder (baskın pencereye girer/çözülür).
- **Bildirim:** `NotificationPlanner.Plan` T−30 dk'ya "Ufukta korsan yelkenleri!" ekler (id 600; 22–09 sessiz kuralı `Add` içinde, 09:00'a kayar).
- **Sapma/bilinen eksikler:** Kışla/bayrak (Faz E) yok; "boştaki Usta için çekiç simgesi" gibi Faz B eksikleri değişmedi; savunma başlarken tutorial'daki boru efekti yok (ses dosyası yok); Akın Panosu'nda satır açık pencerede SAVUN, T−30'da yalnız bilgidir; rozet Ana Üs'teki ADA düğmesinde **yok** (§3'teki "ADA düğmesinde kırmızı yelken rozeti" yapılmadı, yalnız ada içi rozet). Güç = en yüksek Akın − 2 boss Akın'ına denk gelirse (10, 20…) Kasap Kaptan baskında da çıkar.
- **Testler:** `IslandRaidTests` (9: %20 tavan [D=0, Fare/yenilgi toplamı], 4 sa üretim sınırı, gece kayması [ofset 0/+3/−5], birikmeme [48 sa yokluk → 1 çözüm, saat saat 72 sa], 24 sa+ saat durması, kapsama [kapsayan kule daha az çaldırır, D farkı 3, Sv0 kule sayılmaz], canlı zafer/yenilgi/Fare sayıları, eski kayıt göçü, sahnede canlı baskın: güç = en yüksek Akın − 2, Akın ilerlemesi değişmez, zaferde +10+2k altın). Tüm ada testleri (`-testFilter "^Island"`) **65/65 geçti** (önceki 54 + 9 + 2 ekran görüntüsü grubu; bir kez `IslandSaveTests.OldVersionFive...` regex'i raid alanları island'ın sonuna eklenince kırıldı: alanlar `lastReport`'tan sonraya taşındı, `task` hâlâ sonda). Ayrıca `^IslandSaveTests;^CardWorkshopTests;^BuilderSystemTests` geçti.
- **Görüntüler** (`PlaytestOutput/island/D01–D07*.png`; seçilenler `Docs/playtest/island_v2_phaseD_*.png`): D01/D02 T−24 dk varsayılan/tam görünüm + rozet, D03 B kumsalı yakın (tekne + kırmızı ok + yazı), D04 canlı pencere rozeti, D05 Akın Panosu kırmızı kart + SAVUN, D06 ada rapor bildirimi, D07 "Sen yokken" penceresinde baskın satırı.

## §20 Faz E1 uygulama notu (12 Kasım 2026): Kışla + bayrak, Manastır, Altın Damarı, Z3–Z5
E2 (Kademe II–IV düşmanlar, siyah/sarı tekneler, bosslar 20/30, yetenekler) bu parçada yok. Kereste Sevkiyatı §18'de yapıldı.
- **Yeni bina türleri:** `IslandLayout.BuildingKind` sonuna `Barracks, Monastery`. Ayak izi `IslandGrid.Size` (Kışla 3×2, Manastır 3×3), kurulum/yükseltme/adet/can `Progression.Island`
  (§5 tablosu birebir: 150/40 · 400/80, çarpan 0,35/0,25 · 0,4/0,15, adet KK3 / KK6, can 120+30(L−1) / 90+25(L−1)). Kışla zirveye (yükseklik 2) kurulmaz (`Reason.Height`).
  İNŞA ET menüsü 8 satıra çıktı (panel %66 → %80 yükseklik); Kışla paneli Lancer sayısı/can/hasar, bayrak ve **BAYRAK → S2** düğmesi gösterir.
  Ada KK ≥ 5'te açıldığından KK şartları (3, 6) fiilen hep sağlanır; kod yine de adet tavanıyla uygular (test: KK2'de Kışla `Limit`).
- **Kışla + bayrak (`Island/IslandRally.cs`):** savaş başlayınca `IslandBarracks` 1 + ⌊L/5⌋ mavi Lancer (`IslandLancer`) doğurur; ada görünümünde Lancer yok, yalnız bayrak.
  Lancer: can 70·(1+0,15(L−1)), hasar 6·(…), 1 vuruş/sn, 12 sn'de Kışla kapısında yeniden doğar, kayıtlı bayrak noktasına koşar, **bayrağa 4 u içindeki** karadaki düşmanı tutar
  (her Lancer ayrı düşman; boştakiler önce), peşinde bayraktan en çok 5,5 u uzaklaşır, uçanı (Yarasa) vuramaz. Düşman tarafı `IslandTargeting`: yakın dövüşçü 1,5 u içindeki Lancer'ı binadan **önce** hedefler,
  Tank yalnız 1 u içinde yolunu keserse (§7.5); `EnemyController.AttackTarget` `IslandLancer.TakeDamage`'a gider (`DamageSource.IslandLancer`). Lancer hasarı yenilgi etiketi paylarına girmez (`IslandStats.LancerDamageTaken`).
  Yıkık Kışla yeni Lancer doğurmaz; zafer/yenilgi sonrası yeni doğuş yok; ada görünümüne dönünce Lancer'lar silinir.
- **Bayrak:** merdivenlerde üç nokta: 0 = S1 (zirve; **her yürüyüş yolu buradan geçer**), 1 = S2 (A, C), 2 = S3 (B, D) (`IslandLayout.FlagPoints/FlagOnRoute`; "yolda" = bayrak noktası kumsalın `walkPath`'ine ≤1,5 u). Kayıt: `IslandSaveData.flagStair`.
  Cainos Village Props sancağı (direk + bayrak, katalogda `rally_pole/rally_banner`) + Tiny Swords köşe imleci. **Sapma:** sürükleme yerine bayrağa ya da panelden BAYRAK düğmesine dokununca sıradaki merdivene geçer (S1→S2→S3→S1, anında, bedava, savaşta kilitli).
  Tasarım notu: S1 her yolda "yolda" sayıldığı için baskın formülünde hep tam katkı verir; S2/S3 yalnız kendi yollarında tam, diğerinde ×0,5. Dengeyi bot ölçümüyle gözden geçirmek E2'ye kalır.
- **Baskın formülü (§7.8) bağlandı:** `IslandRaidScheduler.BarracksTerm(save, beach)` tamamlanmış Kışla'nın seviyesini ve bayrağın hedef şeritte olup olmadığını verir; çevrimdışı çözüm ve canlı yenilgi (`Assess`) `1,5·L·(yolda ? 1 : 0,5)` ekler (eskiden 0). Kışla yoksa/inşaattaysa 0. %20 tavan değişmedi.
- **Manastır (`IslandMonastery`):** Monk kapıda durur (ada görünümünde de); savaşta 4 sn'de bir 3 u içindeki binalara, Lancer'lara ve Okçu Kaptan'a %8 can verir (Tiny Swords `Heal` efekti, `IslandBuilding.Structure.Heal`).
  **Onarım ×0,5:** `IslandBuilding.CommitAfterBattle` artık `RepairSeconds(…, monastery: HasMonastery())` kullanır (tamamlanmış Manastır varsa). Yıkık Manastır Monk'u gizler/şifa vermez. Manastır seviyesi yalnız can ve (kayıtta) seviye; şifa oranı belgedeki gibi sabit %8.
- **Altın Damarı:** doğa nesnesi (kurulmaz, `IslandLayout.GoldStones` taşları), Z3 açılınca çalışır. Ana Üs altını/sa = 6 + 3(k−1) (KK5 18 · KK10 33), depo 8 sa (`veinStock/veinTick`, UTC tick, çevrimdışı dahil), **taşların önündeki altın yığınına dokununca Ana Üs altınına eklenir** (`IslandEconomy.CollectVein`; Ana Üs altın tavanına uyar).
  **Sapma:** damar işçisi piyon havuzundan düşmez (madenci piyon ek olarak taşı kazar); havuzdan düşseydi 2 piyonluk başlangıçta Odun/Et üretimini kesip altını sıfırda bırakırdı. **Ana Üs altın etkisi:** günde ≈16 sa yakalanan üretimle KK5 288 / 3.500 = %8,2, KK10 528 / 6.600 = %8,0, KK15 %7,9 (≤%10, test `GoldVeinStaysUnderTenPercent…`).
  Üç toplama/gün (24 sa) varsayımı %12'ye çıkar: sınır "günde iki toplama" varsayımına dayanır; depo 8 sa olduğundan gerçekte gece boyunca birikmez. Aşılırsa `GoldVeinPerHour` düşürülür (§11).
- **Z3–Z5 keşfi:** `LastRevealableZone = 5`; bedel/süre §4 tablosu (Z3 500/50 · 5 dk, Z4 800/80 · 15 dk, Z5 1.500/150 · 1 sa), KK şartı 4/5/6 (`ZoneBlockedReason`: "Kıyı Kalesi 6 gerekir"), Usta görevi, bulut dağılması, bulut paneli bölgenin getirisini yazar (`IslandBuildUi.ZoneBenefit`). Z1/Z2 akışıyla aynı kod. Sınırlara/bulutlara dokunulmadı (yeniden pişirme yok).
  Getirdikleri: **Z3** Altın Damarı (yeni bina yeri yok: zirve şeritli/doğayla dolu, geçerli hücre 0); **Z4** ağaç + D kumsalının bölgesi (yeni bina yeri 0); **Z5** Ağıl üretimi +%15 (`MeadowBonus`, yalnız Ağıl) ve 1 Kışla hücresi.
  **Sapmalar:** atış yerleri (P2/P4/P5) bölgeye değil Kıyı Kalesi seviyesine bağlı kaldı (bölgeye bağlamak ladder/bot ölçümlerini ve eski kaydı bozardı); kumsalın açılması Akın numarasına bağlı kaldı (§17), bölgeye değil.
- **Yer darlığı (önemli bulgu):** boş köy üstünde, tüm bölgeler açıkken geçerli sol-üst hücre sayısı Kışla Z0 5 · Z1 1 · Z2 4 · Z5 1; **Manastır yalnız Z0'da 1** (testteki `[IslandGrid] E1` günlüğü). Yalnız Z0'da ikisi birlikte sığmaz; Z1/Z2 açıkken sığar (Kışla Z2'ye, Manastır Z0'ın tek hücresine; test doğrular).
  Oyuncu Kışla'yı yanlış yere kurarsa Manastır'a yer kalmaz (taşıma bedava). Kalıcı çözüm §18'de zaten işaretli "kara genişletme" (arazi/harita değişikliği): E2 ya da ayrı iş olarak ele alınmalı; bu işte arazi/bölge sınırı değiştirilmedi.
- **Kayıt:** sürüm 8'de kaldı; yeni alanlar `raidReportMeat` ile `buildings` arasında (`flagStair`, `veinStock`, `veinTick`); eski kayıtta 0 (bayrak S1, damar boş). `placed` listesi yeni türleri `kind` adıyla saklar (eski kayıt etkilenmez).
- **Katalog (`BuildIslandCatalog`):** `blue_lancer_idle/run/attack`, `monk_idle/heal`, `heal_fx` (Tiny Swords mavi birimler/Heal Effect), `rally_pole/rally_banner` (Cainos Village Props, `NamedSprite` ile çok-dilimli dokudan). Yeni sprite çizimi yok.
- **Testler:** `IslandPhaseE1Tests` (formüller §5/§6/§8; damar birikimi/tavan/KK; ≤%10 sınırı; Z5 +%15; yerleşim/limit/çakışma/birlikte sığma; kayıt gidiş-dönüş + eski kayıt; sahnede Kışla → bayrak döngüsü → Lancer'lar bayrakta düşmanı tutar/hedef alınır/12 sn'de yeniden doğar/savaş sonrası yok;
  uçan ve uzak düşman tutulmaz; Manastır şifası (%8, 3 u dışı etkilenmez, zamanlı vuruş, onarım ≈35 sn); damar toplama + yığın + madenci; Z3–Z5 bedel/süre/bulut/KK şartı/kilit), `IslandRaidTests` +3 (bayrak-yol matrisi, Kışla terimi D/çalıntı, çevrimdışı çözüm kayıttan okur), `IslandReviewScreenshotsE1` (4 test).
- **Görüntüler** (`PlaytestOutput/island/E1_*.png`; seçilenler `Docs/playtest/island_v2_phaseE1_*.png`): 01/01b Kışla ve S1 bayrağı, 02 Kışla paneli, 03 bayrak S2'de, 04 İNŞA ET menüsü (Kışla, Manastır), 05/06 Manastır + Monk, 07 Z3–Z5 kapalı, 08 Z3 paneli, 09/10 Z3 keşfi ve dağılma, 11 altın yığını, 12/13 Z4–Z5 açık, 14/15 savaşta Lancer'lar bayrakta, 16 Manastır şifası.
- **Bilinen eksikler:** bayrak sürüklenmiyor (dokunarak dönüyor); Kışla/Manastır için bot ölçümü yok (profiller Kışla kurmaz; yalnız birim testleri); Lancer'lar düz çizgiyle yürür (yar/merdiven yol bulma yok); Z3/Z4 yeni bina yeri vermiyor.

## §21 Faz E2 uygulama notu (12 Kasım 2026): Kademe II–IV, bosslar 20/30, yetenekler, baskında boss yok, bot 16–30
E2 ile Faz E tamamlandı (Kışla/Manastır/Damar/Z3–Z5 §20'de). Commit'ler: DM2D varlıkları `4fbadf3`, kod `e05ef96`, ayar + testler + belge sonraki commit'ler.
- **Düşmanlar (`EnemyType` sonuna 15 tür; Zombi mevcut `Zombie`'yi yeniden kullanır):** §7.3 tablosu birebir (can/hasar/hız/menzil/altın, `GameData.cs` `EnemyStats`); ilk görünüş Akın'ları `Progression.Island.FirstAkinOf`
  (İskelet Savaşçı/Okçu 11 · İmp 12 · Kemik Büyücü 13 · Örümcek 15 · Zombi 16 · Taş Golem 17 · Hayalet 18 · Zombi Savaşçı 21 · Sukkubus 23 · Kristal Golem 25 · Vampir 27 · İblis 29 · Magma Golem 32; bosslar 10/20/30).
  Roller (`IslandTargeting.RoleOf`): menzilli = Okçu/Kemik Büyücü/Örümcek/Sukkubus; hızlı = Fare/İmp; uçan = Yarasa/Hayalet/Ejder; tank = dört Golem; diğerleri yakın.
  **Kadro (`IslandWaves`):** Akın ≤10 eski kural aynen; 11+ yakın slotlar {Akıncı, Mızrakçı, İskelet, Zombi}, menzili {Kıyı Okçusu, İskelet Okçu}, hızlı {Fare, İmp}, tank {4 Golem} havuzundan; "son açılan tür daha sık" = en yeni ×3, bir önceki ×2.
  Tekne başına ≤1 tank, ≤1 özel (Büyücü/Örümcek/Sukkubus), ≤1 elit (Zombi Savaşçı/Vampir/İblis); 1. dalgada tank/özel/elit yok; Sandalda tank yok (testli: Akın 1–45 × 3 tekne türü × 7 tekne × 4 dalga).
  Uçan sürüde Akın 18'den itibaren her ikinci üye Hayalet. Pano portreleri en yeni 6 türü gösterir; YENİ DÜŞMAN afişi her yeni türde (Kasap Kaptan 10, Batık Şövalye 20, Kızıl Ejder 30 dahil).
- **Tür yetenekleri (`Island/IslandSpecials.cs`, sayılar `Progression.Island`):** Kemik Büyücü 3 sn'de bir 2,5 u içine azami canın %4'ü (belgede %6, aşağıdaki ayar) · Hayalet 6 sn'de bir 1,5 sn dokunulmaz (`Invulnerable`, saydam) · Vampir vuruşunun %30'u kadar can ·
  İmp vurduğu binayı 3 sn %3/sn yakar (belgede %4) · Örümcek vurduğu kule/okçunun atış hızını 2 sn −%40 · Sukkubus 8 sn'de bir menzilindeki Lancer'ı 2 sn durdurur · Magma Golem ölünce 1,5 u içindeki binalar 3 sn yanar (%5/sn; belgede yalnız "yanma") ·
  Taş/Kristal Golem oku ×0,5, Kristal'e dolu atış ×1,5 (zayıf nokta; Magma zırhsız). Zırh kule oklarına da uygulanır (hepsi `IslandEnemy.ArrowDamage`).
  Yeni etiket `mage_heal` (≥150 can şifa); `golem_armor` dört Golem'in toplamıyla, `flyer_unanswered` Yarasa+Hayalet+Ejder toplamıyla, `boss_unbroken` üç bossun toplamıyla ve boss adıyla.
- **Bosslar (`Progression.Island.BossFor/BossScaleFor`):** 10 Kasap Kaptan · **20 Batık Şövalye** (GhostKnight ×1,3, siyah Mavnayla, %50 canda 4 İskelet çağırır; çağrılanlar yürüyüş rotasının kendi noktasından devam eder) ·
  **30 Kızıl Ejder** (DragonRed ×1,2, uçan: tekneye binmez, son dalgada kenardan telegraph okuyla gelir; 10 sn'de bir 3 u'luk 35° konide kule/okçuya 2 × vuruş hasarı, okçu 4,5 u içindeyse ona, değilse en yakın kuleye) · sonra sırayla döner, her tam turda ×1,15 (40 Kasap ×1,495).
  Boss boyu savaş seviyesinden türetilir (`AkinOfLevel`: 10'un katlarında ve kademe sınırlarında kesin; 0,8 eğimi düşük Akın'larda iki Akın'ı aynı seviyeye verdiğinden tersi her yerde tam değildir; tek kullanım yeri boss çarpanı ve tekne rengi).
- **Tekneler (`BoatShipNumber`):** Kademe I kırmızı-haç (ship 3) · II–III siyah-kafatası (ship 2) · IV sarı (ship 6); hasar/batık karesi +6/+12/+18 eskisi gibi; mavi/yeşil yok. Kod Faz C'de renkleri zaten seçiyordu, E2 yalnız tek fonksiyona topladı ve görsel doğruladı (`E2_04`, `E2_09`).
  **Yapılmadı:** Mavna yanına `cannon.png` ×2 süs (belgede "yanına"; Faz C'de de yoktu).
- **Baskında boss yok (D'nin bilinen eksiği):** `RaidPower` en yüksek Akın − 2 boss Akın'ına (10/20/30…) denk gelirse bir alta düşer (en yüksek 12 → 9, 22 → 19, 32 → 29); `IslandRaidManager` baskında boss teknesi ve Ejder üretmez (testli: birim + sahnede canlı baskın).
- **Okçu yetenekleri (`Island/IslandAbilities.cs`):** v2 §7.4 "Yetenekler v1 tablosu" der; asıl tanım `island_base.md` §3 (satır 146–152). Ok Yağmuru Sv2 (1,8 u daire, 1,5 sn'de 3 vuruş × ok hasarı = "3 × ok hasarı", teknelere ×1, 18 sn) ·
  Ateşli Ok Sv4 (sonraki 3 atış tutuşturur: tekne 5 sn %6/sn azami can, birlik 3 sn 0,5 × ok hasarı/sn, 22 sn) · Ağ Oku Sv6 (1,5 u: birlik 2,5 sn kökleşir + 3 sn %40 yavaş, tekne 4 sn %50 yavaş, 20 sn). Okçu seviyesi = KK (`KeepFollowsMainBase`) olduğundan KK 2/4/6'da açılır.
  Hedef = okçunun kilidi / otomatik nişanı (hedef yoksa kullanılamaz, bekleme harcanmaz; Ateşli Ok hedefsiz de). HUD: ATEŞ'in üstünde 3 yuvarlak düğme (Tiny Swords mavi daire), dolum halkası, kilitliyse "Sv N", Ateşli Ok'ta kalan atış "×3". Bot (`IslandBot.UseAbilities`): Yağmur ≥3 hedefte, Ateşli Ok + Ağ zırhlı/boss/Golem/elit hedefte.
  Belgedeki "Hayalet Gemi kalkanını açar" v1'e ait bir etkinlik düşmanıdır, v2'de yok (yapılmadı).
- **Ayar (ölçüm turları, `Progression.Island`/`IslandWaves`):** Tur 0 (hepsi belge sayısı): iyi bot Akın 16–30, KK−1 kule + Kışla + Manastır, 2 koşu: **21/30** (16–19: 3/8). Etiketler `mage_heal`, `multi_beach`, `shield_front`; bina kaybı 10/10.
  Tur 1: Büyücü şifası %6→**%4**, İmp yangını %4→**%3**/sn, özel birim sıklığı (tekne+dalga)%2→%3 → 16/18/21: 3/6. Tur 2: elit yalnız uygun teknelerin yarısında ((tekne+dalga)%2==1) → 19/21/24: 5/6.
  Tüm ayar `Progression.Island` ve `IslandWaves` içindedir; belgedeki asıl değerler (%6, %4/sn) yorumda durur. Ham satırlar: `ladder_E2_good_round0/diag1_round0/diag2_round1/diag3_round2/good_final.txt`.
- **Bot ölçümü (`IslandPhaseE2Tests.MeasureLadder`, `ISLAND_LADDER=profil,başla,bitir,tekrar,etiket`; çıktı `PlaytestOutput/island/ladder_E2_*.txt`):**
  düzen = tüm bölgeler açık, KK = ⌈N/3⌉, okçu = KK, kule tavanına kadar kule (hazır 2 adacık kulesi dahil; KK6–8: 4, KK9+: 5) açık kumsallara sırayla, Manastır (KK≥6) ve Kışla KK seviyesinde (bayrak S1), hız ×6. **Her Akın için yalnız 2 koşu** (tasarım 20 tohum der; 30 koşu ≈ 25 dk).
  **İyi bot, son hâl: 29/30 (%97).** 16–19: 8/8 · 20 (boss): 2/2 · 21–29: 17/18 · 30 (boss): 2/2. (Hedefler: 11–19 ≥%85, 20 ≥%70, 21–29 ≥%78, 30+ ≥%70.)

| Akın | KK | kazanma (2 koşu) | süre (oyun sn) | yıkılan bina | boğulan % | yetenek kullanımı | yenilgi etiketi |
|---|---|---|---|---|---|---|---|
| 16 | 6 | 2/2 | 184/193 | 5/5 | 31 | 15/14 | - |
| 17 | 6 | 2/2 | 187/204 | 4/6 | 29 | 16/17 | - |
| 18 | 6 | 2/2 | 201/227 | 6/9 | 24 | 18/21 | - |
| 19 | 7 | 2/2 | 191/190 | 7/6 | 27 | 18/15 | - |
| 20 (boss) | 7 | 2/2 | 224/219 | 6/6 | 27 | 19/21 | - |
| 21 | 7 | 1/2 | 193/185 | 10/4 | 25 | 19/19 | multi_beach |
| 22 | 8 | 2/2 | 180/165 | 6/4 | 32 | 16/14 | - |
| 23 | 8 | 2/2 | 174/170 | 4/4 | 32 | 15/14 | - |
| 24 | 8 | 2/2 | 185/193 | 5/7 | 32 | 17/18 | - |
| 25 | 9 | 2/2 | 190/247 | 3/5 | 36 | 13/21 | - |
| 26 | 9 | 2/2 | 251/187 | 5/2 | 36 | 23/14 | - |
| 27 | 9 | 2/2 | 188/187 | 2/3 | 44 | 13/14 | - |
| 28 | 10 | 2/2 | 173/175 | 1/1 | 44 | 11/11 | - |
| 29 | 10 | 2/2 | 179/175 | 2/2 | 45 | 13/12 | - |
| 30 (boss) | 10 | 2/2 | 207/209 | 3/2 | 38 | 18/18 | - |

  Özet: medyan süre 189 oyun sn (≈3,1 dk; hedef 2–4 dk) · boğulan tayfa %34 (hedef %25–55) · iyi bot atış hızı ort. 0,89/sn (≤3) · yetenek ort. 16,2 kullanım/savaş · okçu ort. 2,1 kez düşer.
  Düşmana göre hasar payı: Kıyı Okçusu %21–48 ilk sırada (Kademe I okçusu her havuzda kalıyor), Akın 30'da Kızıl Ejder %49–52.
  **Sıradan bot** (yalnız oto-ateş, dolu atış/yetenek yok) 16–30, 1 koşu: **3/15** (23, 28, 29 kazanıldı; yenilgi etiketleri `shield_front` 8, `beach_unwatched` 2, `multi_beach` 1, `flyer_unanswered` 1 (30); hedef %50–70) —
  Faz C'de de aynıydı (sıradan bot 5–10 kaybediyordu): Kalkanlı Mızrakçı'nın ×0,3'ü dolu atışsız aşılmıyor, E2 bunu çözmedi. **Yalnız kuleler** (okçu yok) 16/17/20/25/30: 0/5 (hedef ≤%30 ✓). Golem yenilgisi çıkmadı (`golem_armor` ≥%50 ölçütü sınanamadı).
  **Hedeflerden sapma (düzeltilmedi, §11 yeni ölçütleri):** "medyan zaferde yıkılan bina 0" **sağlanmıyor**: zaferlerde medyan 4 bina (hazır köyün 10 yapısından) yıkılıyor, her yıkık bina 180 sn onarım demektir → "Akın başına onarım beklemesi medyanı ≤60 sn" de **sağlanmıyor**
  (Faz C ölçümünde de 2–4 bina vardı). Denge kolu (kule/yapı canı, İmp/Magma yangını, Ejder) bir sonraki dengede ele alınmalı. Ölçülmeyenler: okçu hasar payı %45–70, yer değiştirme medyanı, p10 ≥45 fps (cihaz yok).
- **Kayıt:** yeni kayıt alanı yok (sürüm 8 aynen); yetenek durumu yalnız çalışma anında; `IslandSaveTests` değişmedi. Eski kayıtta yetenekler Okçu seviyesine (KK'den) göre doğrudan açılır.
- **Katalog/varlık:** `BuildIslandCatalog` 16 yeni `MonsterEntry`: yalnız kullanılan DM2D dosyaları commit'lendi (16 prefab + 16 PSB + 16 controller + klipler + `VampireBat0–4`); `Scene`, `UserInterface`, `Arrow/StoneProjectile/BatFly` commit'lenmedi.
  `SkeletonMage 1.prefab` (paketin adı) kullanıldığı için commit'lendi. **Varlık değişikliği:** `SkeletonArcher` Attack/SpecialAttack klipleri işlevsiz, adsız bir `AnimationEvent` taşıyordu (her atışta "no function name" hata günlüğü, testi düşürüyordu): olay kaldırıldı.
  Golem denetleyicilerinde durum adları karışık (`IdleGolem`/`Idle`/`MoveGolem`): `IslandEnemy.Play` üç adı da dener.
- **Testler:** `IslandPhaseE2Tests` (13 + explicit ölçüm): kademe/ilk görünüş/gemi rengi/boss/baskın gücü tabloları, taban istatistikler, kadro kuralları, 16 canavar katalogdan doğar/Move/Death/boy, zırh çarpanları + boss çarpanı, Büyücü/Hayalet/Şövalye, İmp/Vampir/Örümcek/Magma, Sukkubus/Ejder, okçu yetenekleri (seviye kapısı, Yağmur 3 × ok, Ağ kök+yavaş, Ateşli Ok yanma), baskında boss yok.
  `IslandReviewScreenshotsE2` (5 test, 13 görüntü). Sonuç: `^Island` **102/102**, `^CardWorkshopTests|^LumberShipmentTests` **16/16** (ayrı koşular; ikisi tek koşuda birleştirilince `IslandEconomySimTests` bir kez sıra etkisiyle düştü, tek başına ve ayrı koşuda geçer).
- **Görüntüler** (`PlaytestOutput/island/E2_*.png`; seçilenler `Docs/playtest/island_v2_phaseE2_*.png`): 01 Kademe II dizilişi, 02 Kademe III, 03 bosslar (Kasap/Şövalye/Ejder yan yana), 04 Akın 12 siyah tekne + yetenek düğmeleri, 05 Kademe II dalgası kumsalda, 06–08 Akın 20 Batık Şövalye (savaş/yakın/iskelet çağırışı),
  09 Akın 30 sarı tekne, 10–11 Kızıl Ejder (savaş/yakın), 12–13 Akın Panosu 30/20.
- **Bilinen eksikler:** Mavna süs topları yok; Sıradan bot hedefleri tutmuyor (Faz C'den kalma); yetenek düğmeleri simgesiz (yalnız yazı); Ejder'in koni ateşi Tiny Swords `Fire` karelerinden dizilir (özel koni sprite'ı yok); ağ ve yanma için ayrı durum simgesi yok; ölçüm her Akın için 2 koşu.
## §22 Faz F uygulama notu (13 Kasım 2026): Ana Üs Kışlası + Köylü Evi
Faz F ile sefer tarafı bağları tamamlandı (v1 §7 tablosu). Commit'ler: görseller `fe6180e`, veri/kayıt `0655dad`, savaş `22856eb`, Ana Üs arayüzü `a1f256c`, sonraki commit'ler ölçüm anahtarı + testler + belge.
- **Veri (`GameData.cs`):** `BuildingType` sonuna `Barracks`, `House` (CardWorkshop'tan sonra; eski türlerin sayısal değeri değişmedi), `DamageSource` sonuna `Ally`. `Progression` kökünde: `BarracksUnlock {7,11}`, `BarracksCostFactor 0,65`,
  `AllyLancerHp/Damage(L) = 70/6·(1+0,15(L−1))`, `AllyLancerCount(L) = 1+⌊L/5⌋`, `AllyHitInterval 1`, `AllyGuardRadius 3,5`, `AllyHomeRadius 2,1`, `AllyRespawn 15`; `HouseUnlock {3,9}`, `HouseCostFactor 0,4`, `HouseStorageBonus(L) = 0,05+0,005(L−1)`.
  Kurulum bedeli `Progression.BuildCost(type)` = 1. seviyeye yükseltme bedeli (Kışla 100, Ev 60 altın); yalnız altınla (elmasla satın alma yok), inşaat süresi diğer yapılar gibi 60 sn varsayılanı, hızlandırma mevcut inşaatçı/`GemActions` yolunda (dokunulmadı).
- **Kayıt v9:** `PlayerSaveData.EnsureBaseBondEntries` tür girdilerini ekler (yeni oyun, `RebuildBuildingData`, v8 göçü): Ana Üs seviyesi açılış seviyesindeyse açık/seviye 1, değilse kilitli. Mevcut veriye dokunulmaz.
- **Köylü Evi:** `GameManager.GetGoldMineStorageCapacity()` = `(200+150(L−1))·(1+Σ ev bonusu)`; ev başına bonus yerleşik evin kendi seviyesinden (`placedBuildings`), üretim hızı (`goldPerSecond`) değişmez. İki ev L15: +%24.
- **Kışla Lancer'ları (`Battle/AllyLancer.cs`):** `AllyBarracks` sefer savaşında (`BattleSession.IsCampaign`; Sonsuz/İstila dahil) kurulu her Kışla için 1+⌊L/5⌋ Lancer doğurur (iki Kışla toplanır), `AllyLancer` Ana Üs'ün 3,5 u içindeki en yakın boştaki karadaki düşmana gider (her Lancer bir düşman, uçan yok, sisle gizli düşman yok),
  bölge dışına çıkan hedefi bırakır, 1 vuruş/sn `DamageSource.Ally`; ölünce 15 sn sonra yeniden doğar (`AllyBarracks.Advance`, test edilebilir). Görsel Ada Lancer'ının Tiny Swords kareleri (`IslandVisualCatalog` `blue_lancer_*`; yeni çizim yok), ölçek 1,2 (kahramanla aynı boy).
  `EnemyController`: vuruş menzili ≤2,5 u olan, uçmayan düşman menzilindeki Lancer'a vurur (kahraman kuralından sonra, rol seçiminden önce); menzilliler Ana Üs'e devam eder. Ward'lı bossa Lancer hasarı kule gibi (×kalkan çarpanı) sayılır; Bombacı'yı öldürürse "kule" sayılır.
  `DamageSource.Ally` kahraman payına girmez (payda büyür). Kart etkileri (`TowerHitMultiplier`) Lancer'a uygulanmaz.
- **Ana Üs arayüzü:** `BuildMenuController` 6 sütun (Okçu, Topçu, Manastır, Kart Atölyesi, **Köylü Evi, Kışla**; yazılar otomatik küçülür), `DarkFantasyHudPolish` 6 sütun düzeni, bina adı/rengi (`BaseMapManager`, `BaseWorldUI`), bilgi paneli metinleri, "sonraki Ana Üs seviyesi açar" listesi, `SpriteManager`, yerleşim slotları `barracks_1/2`, `house_1/2` (`BaseWorldLayout`; ev 2×3, kışla 3×3; örtüşme testi geçer).
  Katalog: `BuildBaseVisualCatalog.AddBaseBondVisuals` (Tiny Swords `Barracks.png`, `House1.png`, 5 renk; boşta kalan iki sprite) batchmode ile koşuldu. `GameNotifications` değişmedi (inşaat bitiş bildirimi bina adını `GetBuildingDisplayName`'den alır); `DevMenu` değişmedi (ANA ÜS +1 ile açılış seviyeleri sınanır).
- **Bot:** `PlaytestBot.CreateProfile` Kışla/Ev'i açıldığı Ana Üs'te kurar (seviye = Ana Üs−1; 2. Kışla/Ev Ana Üs 11/9'dan itibaren Ana Üs−2). Telemetri: `allyDamageShare`, `barracks`, `lancersSpawned`, `lancerDeaths`.
  Ölçüm anahtarları: `PLAYTEST_SEEDS=N` (varsayılan 3), `PLAYTEST_NO_BARRACKS=1` (Kışla/Ev kurmaz = Faz F öncesine eşlenik; aynı kod, aynı tohumlar).
- **Ölçüm (3 tur; `PlaytestBotTests`, bot hızı ×4, ham tablolar `PlaytestOutput/phaseF/*_table.txt`):**
  Tur 0 (temel, kod değişmeden; okçu 8+2/sev ve Kart Atölyesi dahil) · Tur 1 (Kışla'lı, tam takım, 3 tohum) · Tur 2 (eşlenik: Kışla'lı ve Kışla'sız, 6 tohum, Bölge 4–6 + Sıradan 5–6 + Yalnız-kahraman 3–4 + Yalnız-kule 4) · Tur 3 (Lancer hasarı 6→4 denemesi, 6 tohum, Tur 2 altkümesi).

  **İyi bot (combined), medyan; "hedef" round2.md C-metrics (S5 = boss aşaması):**

  | Bölge (Ana Üs) | hedef kazanma S1 / S5 | hedef taban canı S5 | hedef kahraman payı | 11 Kas round2 sonu S5 kazanma / pay | Tur 0 temel S1 / S5 kazanma | Tur 0 S5 can / pay | Tur 1 Kışla'lı S1 / S5 | Tur 1 S5 can / pay (Lancer payı) |
  |---|---|---|---|---|---|---|---|---|
  | R1 (1–3) | ≥%95 / 75 | 0,45 | ≥%20 | 100 / 0,87 | 100 / 67 | 0,54 / 0,76 | 100 / 100 | 0,70 / 0,84 (0) |
  | R2 (3–5) | ≥%95 / 72 | 0,40 | ≥%20 | 33 / 0,68 | 100 / 67 | 0,41 / 0,59 | 100 / 33 | 0,00 / 0,64 (0) |
  | R3 (6–8) | ≥%95 / 68 | 0,35 | %30–50 | 100 / 0,60 | 100 / 100 | 1,00 / 0,49 | 100 / 100 | 1,00 / 0,51 (0,08) |
  | R4 (9–11) | ≥%92 / 65 | 0,30 | %30–50 | 100 / 0,50 | 100 / 100 | 1,00 / 0,45 | 100 / 100 | 1,00 / 0,43 (0,06) |
  | R5 (12–14) | ≥%90 / 62 | 0,25 | %30–50 | 100 / 0,43 | 100 / 100 | 1,00 / 0,44 | 100 / 100 | 0,97 / 0,33 (0,07) |
  | R6 (14–15) | ≥%90 / 60 | 0,20 | %30–50 | 100 / 0,33 | 100 / 100 | 1,00 / 0,36 | 100 / 100 | 1,00 / 0,29 (0,12) |

  Tur 0 ile 11 Kasım round2 sonu arasında (okçu değişikliği + Kart Atölyesi) kazanma oranı aynı (R3–R6 S5 %100) ve kahraman payı R1–R3'te 0,09–0,11 düştü, R4–R6'da ±0,07 içinde: okçu eğimi sapma yaratmadı. **Not:** 3 tohumla her hücre 33 puanlık adımlarla ölçülür; Kışla'nın kilitli olduğu R1–R2'de bile S5 %67↔%33 oynuyor (Tur 0 R2 67, Tur 1 R2 33): bu gürültüdür, ±5 puan 3 tohumla doğrulanamaz.

  **Eşlenik tur (Tur 2, 6 tohum; Kışla'sız → Kışla'lı), Ana Üs = bölge S5 Ana Üs'ü:**

  | Profil | Bölge | kazanma % | medyan taban canı | kahraman payı | Lancer payı |
  |---|---|---|---|---|---|
  | İyi S5 | R4 | 100 → 100 | 0,45 → 0,91 | 0,49 → 0,45 | 0 → 0,09 |
  | İyi S5 | R5 | 100 → 100 | 1,00 → 1,00 | 0,38 → 0,34 | 0 → 0,09 |
  | İyi S5 | R6 | 100 → 100 | 1,00 → 1,00 | 0,33 → 0,31 | 0 → 0,10 |
  | İyi S1 | R4 / R5 / R6 | 100 → 100 (üçü) | 0,87→1,00 / 1,00 / 1,00 | 0,62→0,59 / 0,60→0,52 / 0,55→0,55 | 0 → 0,16 / 0,22 / 0,18 |
  | Sıradan S5 | R5 | **33 → 100** | 0,00 → 0,75 | 0,53 → 0,38 | 0 → 0,24 |
  | Sıradan S5 | R6 | **0 → 83** | 0,00 → 0,55 | 0,51 → 0,36 | 0 → 0,25 |
  | Yalnız-kahraman S5 | R3 / R4 | 0 / 0 → 0 / **33** | 0 | 1,00 → 0,79 / 0,56 | 0 → 0,21 / 0,44 |
  | Yalnız-kule S5 | R4 | 0 → 33 (4/6 zaman aşımı; Faz F öncesi de vardı: Tur 0 R4 medyan 10 dk) | 0 → 0,99 | 1,00 → 0,11 | 0 → 0,27 |

  Tur 3 (Lancer hasarı 6→4, aynı 6 tohum): iyi S5 R4/R5/R6 hep %100, pay 0,43/0,38/0,31; Sıradan R5/R6 %100/%83 (değişmedi); Yalnız-kahraman R3/R4 %0/**%67**; Yalnız-kule R4 %33; Lancer payı yalnız 0,44→0,37 düştü. Hasarın 4'e inmesi sonuçları hareket ettirmedi (Lancer'ın asıl etkisi hasarı değil, düşmanı Ana Üs'ten uzak tutan ve vuruş yiyen can havuzu olması); sapma zaten tohum gürültüsü içinde olduğundan **ayar geri alındı, `AllyLancerDamage` belge değeri 6'da kaldı.**
- **Değerlendirme / sapmalar (düzeltilmedi, kullanıcı kararı gerekir):**
  - **Kazanma ±5 puan:** iyi botun combined kazanması Kışla'dan önce de R3–R6 S5'te %100'dü (hedef ≥%60–68) ve medyan taban canı 0,45–1,00 (hedef 0,20–0,35): bot "beklenen oyuncu"dan güçlü, tavan etkisiyle Kışla bu hücreleri oynatamıyor (100→100). Yani tablo "korunuyor" ama anlamlı bir sınama değil. İnsan oyuncu ölçümü yok; cihaz denemesi öneririm.
  - **Kahraman payı R3+ %30–50:** iyi bot S5'te Kışla'lı 0,51 / 0,43 / 0,33 / 0,29 (Tur 1), 0,45 / 0,34 / 0,31 (Tur 2, R4–R6); Kışla payı 2–4 puan düşürüyor. R6 S5 Tur 1'de 0,29 (eşiğin 1 puan altı, gürültü içinde; Tur 2 0,31). S1'de pay 0,48–0,75 (Kışla'sız da 0,48–0,73).
  - **Sıradan oyuncu çok yükseliyor (R5 33→100, R6 0→83):** Kışla seyrek/zayıf oynayanı Ana Üs'te ayakta tutuyor; bu profil için tabloda hedef yok, ama Sıradan botun 11 Kasım'da boss'ta ~%28 olması "biraz düşük" diye işaretlenmişti (MORNING_REPORT) → Kışla bunu düzeltiyor, fakat iyi bot zaten tavanda olduğundan "ortalama oyuncu" (iki profil arası) R5–R6'da hedef %60–75'in üstüne çıkar (~%90+). Cihazda gözlenmeli; ayar kolu `AllyLancerCount` / `AllyRespawn` (hasar değil).
  - **Yalnız-kahraman R4 %33 (hedef ≤%20), yalnız-kule R4 %33 (hedef ≤%30):** 6 tohumda 2 galibiyet, binom gürültüsü içinde (gerçek oran %20 iken ≥2/6 olasılığı ≈%35); Tur 3'te %67'ye çıkması da gürültü. Kışla'sız bu hücreler %0'dı: Kışla "yapı yok" oyuncuyu da bir ölçüde korur.
  - Bot 2. Kışla'yı Ana Üs−2 seviyesinde kuruyor (gerçek oyuncu Ana Üs 11'de bu seviyeye ancak çok altın harcayarak gelir): R4–R6'da 5–6 Lancer test edildi (en kötü durum). Yalnız tek Kışla ile ölçülmedi.
- **Yapılmayanlar:** `Tools/economy_sim.py` Köylü Evi ile yeniden koşulmadı (ev yalnız depoyu büyütür; üretim değişmez); Kışla yıkılması/onarımı sefer savaşında yok (sefer yapıları savaşa girmez); Lancer'a Manastır şifası (`AllyLancer.Heal` var, sefer Manastırı bağlanmadı); 2. Kışla için ayrı bayrak/toplanma noktası yok.
- **Testler:** `BaseBarracksTests` (12): açılış seviyeleri ve en çok bina sayısı, bedel çarpanları (L2–L15), enum değerleri, Lancer sayı/can/hasar formülleri (L1–L15), koruma yarıçapı (bölge dışı düşman avlanmaz, içi vurulur, kaynak `Ally`), 15 sn yeniden doğma, yakın dövüşçü Lancer'a vurur, iki Kışla toplanır, Kışla yok = Lancer yok,
  ev deposu bonusu + üretim değişmez, v8 kaydı göçü + gidiş-dönüş, sahnede Kışla + Lancer ve inşa menüsü ekran görüntüleri. Sonuç: `BaseBarracksTests;CardWorkshopTests;LumberShipmentTests;BuilderSystemTests;IslandSaveTests;GridPlacementTests;ProgressionTests;SaveSystemTests;BattleStructureTests;GemEconomyTests` + `BasePresentationTests.BuildingFootprints…` **50/50**; `^Island` **102/102** (ayrı koşu; kayıt kirlenmesi giderildikten sonra).
- **Test notu (kayıt kirlenmesi):** `GameManager` sahne geçişlerinde yaşar ve `OnApplicationQuit`'te son oyuncu verisini gerçek kayda yazar. Ana Üs 11–12 kuran bir test sınıfı (bu fazın ilk sürümü) kapanışta gerçek kaydı Ana Üs 11 + Kışla ile bırakıyor, sonraki `^Island` koşusunda `IslandEconomySimTests` (etkin Kıyı Kalesi = Ana Üs) "Ağıl Sv6, beklenen 5" diye düşüyordu (tek başına temiz kayıtta geçer; E2'deki "sıra etkisi" de bu olabilir).
  `BaseBarracksTests` artık `[OneTimeTearDown]`'da test GameManager'ını yok eder (kayıt değişmez, doğrulandı). `CardWorkshopTests` aynı kalıbı kullanıyor, dokunulmadı.
- **Görüntüler** (`PlaytestOutput/phaseF/`; seçilenler `Docs/playtest/island_v2_phaseF_*.png`): `lancers_idle` (3 Lancer Ana Üs'ün altında), `lancers_fight` (iki Orc'a karşı), `base` (Kışla L10 mor kademe + iki Köylü Evi), `buildmenu` (6 sütunlu inşa menüsü).
