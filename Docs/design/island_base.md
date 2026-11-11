# İkinci üs: Martı Adası / Gull Isle (hedef zorluk: ORTA)

Kaynak kararlar: `Docs/AI_README.md`, `Docs/design/round2.md`. Bu belge kod içermez. Tüm sayılar §8'deki
`Progression.Island` / `GemEconomy` sabitlerine gider; başka yerde sabit sayı olmaz.
Kullanıcı kararları (kesin): açılış Ana Üs 5 · merdiven Akın + zamanlı baskın · ATEŞ + otomatik nişan + basılı tutma
ile dolu atış + hedef kilidi + atış yerine dokunarak geçiş · ada kaynakları Et + Odun.

## 1. İsim, fantezi, açılış, yolculuk

| Aday | TR / EN | Not |
|---|---|---|
| A | **Martı Adası / Gull Isle** | Kısa, "ada" hemen anlaşılır, HUD'a sığar (ADA / ISLE) |
| B | Kıyı Kalesi / Coastal Keep | Kale vurgusu; ama "Ana Üs" ile karışır |
| C | Kartal Yuvası / Eagle's Nest | Okçuya uyar, deniz hissi zayıf |

- **Seçim:** ada = **Martı Adası / Gull Isle**, zirvedeki kale = **Kıyı Kalesi / Coastal Keep** (adanın Ana Üs'ü).
  Gerekçe: ikisi birlikte hem yeri hem savunulan yapıyı adlandırıyor.
- **Fantezi:** Tiny Swords tanıtım haritası. Hareketli suyun ortasında çimenli bir ada var: yarlar, merdivenler, köpük,
  bulutlar, koyunlar ve zirvede bir kale. Korsan tekneleri kumsallara yanaşır. Mavi **Okçu Kaptan / Archer Captain**
  yar başındaki atış yerlerinden onları denizde batırmaya çalışır.
- **Açılış:** Ana Üs 5 olunca bir tören afişi çıkar ("Ufukta bir ada!"). HUD alt dock'unda SEFER'in yanına
  **ADA** düğmesi eklenir (`Polish/UI/BaseHudRedesign.cs:144`). Ana Üs 5'ten önce düğme kilitli görünür ve "Ana Üs 5" yazar.
  Gerekçe: oyuncu 2. inşaatçıyı da 5'te alıyor; ada ikinci ilerleme hattı olarak aynı ana denk gelir.
- **Yolculuk:** ADA düğmesine basılınca 1,2 sn'lik yelken geçişi oynar (kararma + dalga), ardından ada görünümü açılır.
  Adanın HUD'unda **ANA ÜS** düğmesi geri götürür. Ana Üs'e liman binası **eklenmez**: Ana Üs haritası kıyıda değil ve
  düğme daha anlaşılır.
- **İlk ziyaret öğreticisi (3 adım, `FirstTimeTips` kalıbı):**
  1. "Tekne geliyor! **ATEŞ**'e dokun." Akın 1'in ilk teknesi yavaş gelir, tek sandal. Batınca "İçindekiler boğuldu!" yazar.
  2. "Doğu kumsalını görmek için **atış yerine dokun**." Okçu P3'ten P4'e koşar. İkinci tekne oradan gelir.
  3. Zaferden sonra: "Ödül: Et + Odun. **Ağıl**'ı yükselt." Ok ağılı gösterir. Öğretici burada biter.

## 2. Ada yerleşimi (tek, elle tasarlanmış; 1 karakter = 1 hücre = 1 birim = 64 px)

Harita 14 × 25 hücre. Savaş kamerası ortho 12,5'te 9:16 ekranda (14,06 × 25 birim) haritanın **tamamını** gösterir.
Gerekçe: savaşta kaydırma olmadığı için yenilgi her zaman görülebilir. Ev görünümünde yakınlaştırma
`BaseCameraController` ile yapılır.

```
     col 0123456789ABCD        Açıklama
r00      ~~~~~~~~~~~~~~        ~ deniz   : köpük/sığ   s kumsal (iniş)
r01      ~~~~:TTTTT:~~~        2 zirve (yükseklik 2)   1 yayla (yükseklik 1)   . ova (0)
r02      ~~~:T22222T:~~        # yar yüzü   H merdiven   K Kıyı Kalesi (3×3)
r03      ~~~:2P2KKK2P:~        P atış yeri   t kule parseli   b bina parseli
r04      ~~~:222KKK22:~        o Ağıl (koyunlar)   T orman (Oduncu)
r05      ~~~:2b2KKK22:~        A B C D: kumsal adları (tekne yanaşma)
r06      ~~~:###HH###:~        <- S1 orta merdiven (zirve <-> yayla), tüm yollar burada birleşir
r07      ~~:111111111:~
r08      ~~:1t1b1b1t1:~        t: T1 (batı), T2 (doğu)
r09      ~~:P1111111P:~        P3 (batı yar başı), P4 (doğu yar başı)
r10      ~~:111b1b111:~
r11      ~~:#H#####H#:~        <- S2 batı merdiven, S3 doğu merdiven
r12      ~:...........:
r13      As..oo..TT..sB        A batı kumsalı, B doğu kumsalı
r14      As..oo..TT..sB
r15      ~:...o..t.TT.:        t: T3 (güney ova)
r16      ~:.....P.....:        P5 güney burnu (alçak, açıkta)
r17      ~:s.........s:
r18      ~~ss.......ss~
r19      ~~~C:::::::D~~        C güneybatı kumsalı, D güneydoğu kumsalı
r20-24   ~~~~~~~~~~~~~~        tekne yaklaşma suyu (C/D teknelerinin doğduğu yer)
```

| Kumsal | Açılır (Akın) | Tekne doğuşu | Yürüyüş yolu (sabit ara noktalar) | Uzunluk |
|---|---|---|---|---|
| A batı | 1 | sol kenar, r13 | A, ova doğuya 2 hücre, S2, yayla, S1, kale | ≈15 u |
| B doğu | 4 | sağ kenar, r13 | B, S3, yayla, S1, kale | ≈15 u |
| C güneybatı | 9 | alt kenar, c3 | C, ova kuzeye, S2, S1, kale | ≈19 u |
| D güneydoğu | 15 | alt kenar, c11 | D, ova kuzeye, S3, S1, kale | ≈19 u |

- **Atış yerleri:**
  - P1 / P2: zirve batı / doğu. S1'i ve yaylayı görür.
  - P3: A, S2 ve batı denizini görür.
  - P4: B, S3 ve doğu denizini görür.
  - P5: C ve D tekneleri. Yüksek zemin bonusu yoktur ve Haydutlara açıktır.
  - Açılma: P1, P3 KK1; P4 KK2; P2 KK4; P5 KK5.
- **Parseller:**
  - b: zirvede Ambar; yaylada Kışla, Okçuluk Alanı, 2. Ağıl ve 2. Oduncu.
  - Ağıl ve Oduncu ovada sabit yerdedir: koyun ve ağaç görselleri orada.
  - t: T1 KK2, T2 KK4, T3 KK8.
- **Genişleme (KK seviyesi):**

  | KK | Açılan alan |
  |---|---|
  | 1 | zirve + yayla + batı ova |
  | 2 | doğu ova |
  | 3 | güneybatı ova |
  | 5 | güneydoğu + P5 |

  Kilitli alanların üstünde Tiny Swords `Clouds_0x` bulutları durur ve açılınca dağılır.
  Gerekçe: kumsal açılışları Akın kapısıyla (§5) aynı ana denk gelir.
- **Izgara:** `Base/BaseOccupancyGrid.cs` yeniden kullanılır. Ada binaları serbest yerleşmez, **sabit parsellere**
  oturur. Gerekçe: yollar ve atış yerleri sabit olunca savunma okunur kalır.

## 3. Savaş

**Akış:** her dalga **tekne → iniş → yürüyüş** evrelerinden geçer. Dalgalar arasında 8 sn mola ve "Sonraki dalga" afişi var.
Bir dalgadaki tekneler 2,5 sn arayla doğar. Savaş altını ve savaş içi inşaat **yoktur**: savunma kalıcı ada
binalarından gelir. Gerekçe: ada sefer savaşından ayrı bir oyun gibi hissettirmeli; yeni ekonomi gerekmesin.

**Tekne evresi.** Tekneler kenardan kumsala sabit hızla gider. Varış 6–10 sn sürer.

| Tekne | Can (× düşman can çarpanı) | Tayfa | Hız u/s | Özel | Görünür |
|---|---|---|---|---|---|
| Sandal | 60 | 2 | 1,0 | — | Akın 1+ |
| Akıncı Teknesi | 150 | 4 | 0,8 | — | Akın 5+ |
| Kalkanlı Mavna | 300 | 6 | 0,6 | Tek tık okları ×0,6 | Akın 15+ |
| Hayalet Gemi (etkinlik) | 500 | 8 iskelet | 0,5 | Normal oklara bağışık, önce Ateşli Ok gerekir | Etkinlik |

- **Ana karar: tekne batınca içindeki herkes boğulur** ve tayfanın tüm altın değeri sayılır.
  Gerekçe: bu modun becerisi ve fantezisi budur. Tekne canı, tayfa toplam canının ≈%70'i. Yani batırmak daha
  verimli ama sudaki süre kısa. Bu yüzden her tekne batmaz.
- Batış: 0,8 sn yatma, `Particle FX/Water Splash.png` ve "+N boğuldu" yazısı.

**İniş evresi.**
- Tekne kumsala yaslanınca tayfa 0,4 sn arayla iner. Boş tekne geri çekilir ve kaybolur.
- İnen birlik 1,5 sn boyunca **ıslak**tır: aldığı hasar ×1,25. İniş anı ödüllendirilir.

**Yürüyüş evresi.**
- Düşman sabit ara noktalarla yürür. Yolda başka hedef varsa ona saldırır: atış yerindeki okçu (yalnız menzilliler ve
  Haydut), Lancer'lar, kuleler.
- Kaleye varınca kaleyi vurur.
- Kod notu: hedef seçimi `EnemyController.cs:582` ve hareket `:622` düz çizgi. Rota kuyruğu eklenmesi gerekir (§10).

**ATEŞ düğmesi (Okçu Kaptan, seviye L, tavan = KK seviyesi):**

| Değer | Sayı | Gerekçe |
|---|---|---|
| Ok hasarı | 12 × (1 + 0,15(L−1)) → L1 12 · L5 19 · L10 28 · L15 37 | Kahramanla aynı eğim (`GameData.cs:108`) |
| Bekleme | 0,5 sn (2 atış/sn); fazladan 1 dokunuş sıraya alınır | Hızlı dokunan kayıp yaşamaz, sınır sabittir |
| Kritik | %10, ×2 | Küçük heyecan, dengeyi oynatmaz |
| Menzil | 7 u; yükseklik 1–2'deki yerden +1,5 u | Yar başı önemli olsun |
| Ok hızı | 14 u/s, hedefe güdümlü (ıska yok) | Okunabilirlik: hasar hep hedefe gider |
| Dolu atış | Basılı 0,35 sn'de başlar, 1,2 sn'de dolar. Bırakınca ×2,5 hasar, 3 hedef deler, teknelere ek ×1,5, ön kalkanı yok sayar | Tek hedefte DPS ≈ dokunmaya eşit; delme, tekne ve kalkan için seçilir |
| Okçu canı | 150 × (1 + 0,15(L−1)); düşerse 8 sn sonra P1'de doğar | `HeroController.cs:30` ile aynı |
| Koşu | 4 u/s; koşarken atış yok; en az 0,6 sn | Yer değiştirmenin bedeli var |

- **Otomatik nişan** (sıra):
  1. kilitli hedef menzildeyse o;
  2. kaleye 3 u'dan yakın düşman;
  3. menzildeki en yakın düşman ya da tekne.
- **Kilit:**
  - düşmana ya da tekneye dokunulunca kırmızı nişangâh çıkar; hedef ölünce veya menzilden çıkınca kalkar;
  - boş suya dokunmak kilidi kaldırır.
- **Atış yeri değiştirme:** P işaretine dokunulur. Okçu merdiven yolundan koşar. Komşu yerler arası ≈1,5–3 sn sürer.

**Yetenekler** (okçu seviyesiyle açılır; HUD'da ATEŞ'in üstünde 3 küçük düğme):

| Yetenek | Açılış | Etki | Bekleme |
|---|---|---|---|
| Ok Yağmuru / Volley | L2 | 1,8 u daire. 1,5 sn'de içindeki her düşmana 3 × ok hasarı; teknelere ×1 | 18 sn |
| Ateşli Ok / Fire Arrow | L4 | Sonraki 3 atış tutuşturur: tekne 5 sn boyunca azami canın %6'sı/sn; birlik 3 sn boyunca 0,5 × ok hasarı/sn. Hayalet Gemi kalkanını açar | 22 sn |
| Ağ Oku / Net | L6 | 1,5 u: birlikler 2,5 sn kökleşir, sonra 3 sn %40 yavaşlar; tekneler 4 sn %50 yavaşlar | 20 sn |

**Dostlar ve kuleler** (kalıcı ada binaları; §6):
- **Gözcü Kulesi** (Tiny Swords Tower, t parselleri): sefer Okçu Kulesi istatistiği, `Progression.TowerDamage/TowerUnits`.
  Teknelere de ateş eder. Gerekçe: denge zaten ölçülmüş eğri.
- **Kışla** (Barracks): 1 + L/5 mavi Lancer.
  - Toplanma bayrağı S1'de durur; S1, S2 ya da S3'e basılı tutularak taşınır.
  - Her Lancer 1 düşmanı tutar ve 12 sn'de yeniden doğar.
- **Kayıp:** Kıyı Kalesi canı 0. Can = 300 + 80(KK−1); Ana Üs savaş canına benzer, `GameData.cs:185`.
- **Zafer:** bütün dalgalar temizlenir. Okçunun düşmesi yenilgi değildir. Süre sınırı yok.
- **Yenilgi ekranı etiketleri** (okunabilirlik):
  - `beach_unwatched`: "B kumsalı izlenmedi";
  - `shield_front`: "Kalkanlıları yandan ya da dolu atışla vur";
  - `monk_heal`: "Önce Rahibi düşür";
  - `rogue_post`: "Haydut atış yerine tırmandı".

## 4. Düşman listesi (fraksiyonlar = Tiny Swords renkleri; mavi oyuncunun)

Yeni türler `EnemyType` enum'unun **sonuna** eklenir (`GameData.cs:38` kuralı). Hepsi `ScaleToLevel` ile ölçeklenir.

| Birim | Görsel | Can | Hasar | Hız | Altın | Rol / karşı oyun | Benzeri |
|---|---|---|---|---|---|---|---|
| Tayfa / Deckhand | Pawn Run/Interact Knife | 26 | 4 | 2,2 | 6 | Hızlı, sürü; Ok Yağmuru | Kurt 30/5/2,2 |
| Akıncı / Raider | Warrior | 55 | 8 | 1,3 | 14 | Kaleye yürür | Zombi 50/8/1,1 |
| Kıyı Okçusu / Shore Archer | Archer | 30 | 6 (menzil 5) | 1,4 | 14 | Durup atış yerini ya da kuleyi vurur; öncelikli hedef | — |
| Kalkanlı Mızrakçı / Shield Lancer | Lancer (8 yön) | 80 | 10 | 1,1 | 22 | Önden (±60°) tek tık oku ×0,3; dolu, yağmur ve ateş ×1. Çözüm: yandaki P'ye geç | Ork 80/12/1,5 |
| Rahip / Monk | Monk Heal + `Extra/Heal Effect` | 40 | 0 | 1,2 | 18 | 3 sn'de bir 2,5 u içindeki dostlara %6 can; kilitle ve düşür | — |
| Haydut / Rogue | Black/Purple Pawn bıçak, koyu ton | 35 | 7 | 2,6 | 16 | Yara tırmanır (merdiven atlar, yol −%30), atış yerine ve okçuya gider; Ağ Oku | Sabotajcı tarzı |
| Kara Kaptan (boss) / Black Captain | Black Warrior ×1,6 + sancak | 450 | 20 | 0,9 | 120 | 15 sn'de bir Nara (yakın dostlara 4 sn +%30 hız). Boss Ward'ı yalnız **dolu atış** kırar (`Progression.Counters` ward kuralları) | Kale Muhafızı 220 |

| Fraksiyon | Akın | Kadro |
|---|---|---|
| Kızıl Korsanlar (Red) | 1–9 | Tayfa, Akıncı; Kıyı Okçusu 4+ |
| Sarı Paralılar (Yellow) | 10–19 | + Kalkanlı Mızrakçı, Rahip |
| Mor Tarikat (Purple) | 20–29 | + Haydut; Rahip daha sık |
| Kara Armada (Black) | 30+ | Hepsi; her 10. Akın'da Kara Kaptan (10, 20, 30 … bossları renkli Kara Kaptan) |

- **Mighty Heroes (Rogue):** kullanılmaz, Haydut = renkli Pawn bıçak. Gerekçe: chibi kesme animasyonu 64 px piksel
  sanatla çatışır. Pawn bıçak kareleri her renkte zaten var.
- **DungeonMonsters2D:** boyalı karanlık stil, çatışır. Yalnız **Hayalet Gemi** etkinliğinde kullanılır (Skeleton,
  SkeletonArcher, Ghost). Etkinlik sis/gece tonuyla oynanır, böylece stil farkı gizlenir. Ayda 1 hafta sonu, İstila ile çakışmaz.
  Gerekçe: farklılık "doğaüstü" olarak okunur.

## 5. Akın merdiveni ve zamanlı baskınlar

**Akın N (oyuncu başlatır, can harcamaz):**

| Değer | Formül | Örnek N = 1 / 5 / 10 / 20 / 30 |
|---|---|---|
| Güç seviyesi (`ScaleToLevel`) | 1 + round(0,8·N) | 2 / 5 / 9 / 17 / 25 |
| Dalga | N≤2: 1 · ≤9: 2 · ≤24: 3 · sonrası 4 | 1 / 2 / 2 / 3 / 4 |
| Dalga başına tekne | min(1 + ⌈N/3⌉, 7) | 2 / 3 / 5 / 7 / 7 |
| Açık kumsal | A; B N≥4; C N≥9; D N≥15 | |
| Şart | KK ≥ ⌈N/3⌉ | KK 1 / 2 / 4 / 7 / 10 |
| Boss | N % 10 == 0 | |

- Kapı (KK ≥ ⌈N/3⌉, KK tavanı = Ana Üs) merdiveni Ana Üs ilerlemesine bağlar. En yüksek Akın = 3 × Ana Üs seviyesi.
  Gerekçe: MB5'te en çok Akın 15 (güç 13 ≈ Bölge 3). Sefer eğrisine (round2 C-metrics) yakın.
- **İlk zafer ödülü:**
  - Et 25 + 8N, Odun 25 + 8N;
  - **Ana Üs altını 20 + 5N**;
  - elmas: her 5. Akın +3; boss Akın'ında ek +5.
- **Tekrar:** ödülün %30'u (`BattleManager.ReplayRewardRate`, `BattleManager.cs:42`).
- Denge kontrolü: Akın 1–15 ilk zafer altını toplamı 900. Bu, MB 5→6 yükseltmesi (850) kadar. Ana Üs dengesini bozmaz.
  Elmas: Akın 45'e kadar 27 + 20 = 47. Sonsuz Sefer'in 5 katta +3'üyle aynı tempo.

**Zamanlı Korsan Baskını:**
- **Başlangıç:** Akın 5 kazanılınca açılır.
- **Sıklık:** 8 sa ± 45 dk. Varış `GameNotifications.AvoidNight` (`GameNotifications.cs:144`) ile 09–22 arasına kayar.
  Aynı anda en fazla 1 bekleyen baskın olur; birikmez. Oyun 24 sa+ açılmazsa saat durur: yokluk en fazla 1 baskın demek.
- **Uyarı:** 30 dk önce bildirim ("Ufukta korsan yelkenleri!"). Ana Üs'teki ADA düğmesinde kırmızı yelken rozeti görünür.
- **Pencere:** varıştan sonra 2 sa. Oyuncu baskını canlı oynayabilir: güç = en yüksek Akın − 2 (en az 1), 2 dalga.
  - Kazanırsa: bekleyen üretime +%10 bonus ve **10 + 2·KK Ana Üs altını**.
  - Kaybederse: aşağıdaki kayıp ×0,5. Gerekçe: oynamak hiçbir zaman yok saymaktan kötü olmamalı.
- **Çevrimdışı çözüm** (pencere biterse; yükleme anında, inşaatçı görevi gibi UTC tick ile):
  - `D = 2·Σ kule seviyeleri + 1,5·Kışla sev + 1,0·Okçu sev + 1,0·KK`
  - `R = 6·KK`
  - `Çalınan oran = 0,15 × clamp01((R − D) / R)`
  - Örnek: dengeli oyuncu (2 kule ≈ KK, Kışla ≈ KK) için D ≈ 7,5·KK ≥ R, yani savunulur. Kulesiz oyuncu için D ≈ 3,5·KK ve
    çalınan ≈ %6.
- **Tavan:** yalnız Ağıl/Oduncu'da **toplanmamış** üretimden alınır. En çok %15 ve en çok 4 saatlik üretim.
  Cüzdan, binalar, Ana Üs ve elmas **asla** etkilenmez.
- Savunulursa küçük ödül: 10 + 2·KK altın. Sonuç `AwayReport` penceresinde tek satırla gösterilir: "Korsanlar 2 kuleye
  takıldı, 80 et çaldı".

## 6. Ada binaları ve ekonomi

Kaynaklar: **Et / Meat** ve **Odun / Wood** (Tiny Swords `Meat Resource`, `Wood Resource` simgeleri). Ada üst barında
gösterilir. Ada binaları **hep mavi** kalır. Renk kademesi kullanılmaz, çünkü kırmızı/sarı/mor/siyah düşman rengidir.
Seviye arması ile gösterilir.

| Bina | Görsel | Açılır | Adet | İşlev | Bedel çarpanı (Odun / Et) |
|---|---|---|---|---|---|
| Kıyı Kalesi | Blue Castle | — | 1 | Kale canı; KK tavanı = Ana Üs sev; alan, yer ve parsel açar | 1,0 / 0,5 |
| Ağıl / Pasture | House1 + Sheep Idle/Grass/Move | KK1 (hazır) | 1 (2. KK7) | Et üretir | 0,45 / 0 |
| Oduncu / Lumber Camp | House2 + Tree1–4, Pawn Axe | KK1 (hazır) | 1 (2. KK7) | Odun üretir | 0,15 / 0,35 |
| Ambar / Storehouse | House3 | KK2 | 1 | Cüzdan tavanı | 0,4 / 0,2 |
| Gözcü Kulesi | Tower | KK2/4/8 (T1/T2/T3) | 3 | Kule (§3) | 0,5 / 0,15 |
| Kışla / Barracks | Barracks | KK3 | 1 | Lancer'lar (§3) | 0,35 / 0,4 |
| Okçuluk Alanı / Archery | Archery | KK1 (hazır) | 1 | Okçu yükseltmesi (anında, Et) | — |

- **Bedel:** `IslandCost(tür, hedef sev) = W[hedef] × çarpan`, 10'a yuvarlanır.

  | Hedef sev | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
  | W (Odun) | 80 | 150 | 250 | 380 | 550 | 760 | 1000 | 1300 | 1650 | 2050 | 2500 | 3000 | 3600 | 4300 |

- **Okçu yükseltmesi:** round(50 × 1,28^(L−1) / 5) × 5 Et. Anında yapılır; tavan KK. `HeroUpgradeCost` kalıbı (`GameData.cs:486`).
- **Üretim:**
  - Ağıl/Oduncu üretimi = 30 + 15(L−1) /sa (L1 30, L5 90, L10 165, L15 240).
  - Bina deposu = 8 sa üretim. Toplamak için dokunulur (maden kalıbı).
- **Ambar (cüzdan tavanı):** 500 + 400(L−1) her kaynak için. KK15 (4300 Odun) Ambar 11 ister; panel bunu açıkça yazar.
- **Süreler:** `TimingConfig` eğrisinin **×0,5**'i (`TimingConfig.cs:13`).

  | Hedef sev | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
  |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
  | Süre | 5 sn | 30 sn | 1,5 dk | 3 dk | 5 dk | 7,5 dk | 15 dk | 30 dk | 1 sa | 1,5 sa | 2 sa | 3 sa | 4 sa | 6 sa |

- **Ada Ustası:** adanın tek inşaatçısı (Blue Pawn Hammer). **Ana Üs inşaatçılarını kullanmaz.**
  Gerekçe: ada Ana Üs ilerlemesini yavaşlatmamalı. Elmasla bitirme aynı `GemEconomy.FinishCost` (2,2·√dk) ile yapılır.
  Eksik Et/Odun için elmas: 1 elmas = 25 kaynak.
- **Ana Üs → Ada:** açılış MB5. KK tavanı = Ana Üs seviyesi. Okçu tavanı = KK. Akın kapısı KK'ya bağlı.
- **Ada → Ana Üs** (hafif, isteğe bağlı; sefer dengesine dokunmaz):
  1. Akın ve baskın altını/elması doğrudan Ana Üs cüzdanına gider.
  2. **Ada Ticareti:** KK 3/6/9/12/15'te maden üretimi her kademede +%2 (en çok +%10, Kraliyet +%20'ye eklenir).
     Not: depo tavanı bağlayıcı olduğu için etkisi küçük; bu bilinçli.
  3. **Kereste Sevkiyatı:** 100 Odun = bir Ana Üs inşaat görevine −10 dk, günde 3 kez. Reklam hızlandırmasının
     (−30 dk/gün) aynısı.

## 7. Ana Üs için yeni yapılar ("İnşa Et > Yapı")

- **Kullanılabilir sprite:** Castle, Archery, Tower ve Monastery Ana Üs / Okçu / Topçu / Manastır olarak **zaten
  kullanılıyor** (`Managers/SpriteManager.cs:21-23`, `Editor/BuildBaseVisualCatalog.cs:32`); House3 maden evi.
  Yeni yapı için boşta kalanlar yalnız **Barracks** ve **House1**. House2 adada Oduncu olur.
- **Fiyatlar:** `Progression.UpgradeCost` çarpanıyla (`GameData.cs:452`). Elmasla satın alınamaz; yalnız inşaatı
  hızlandırılabilir. Kazanmak için ödeme yok (P2W yok).

| Yapı | Açılış (2. adet) | Bedel çarpanı | İşlev (mevcut savaşa uyumlu) | Neden bu seviye |
|---|---|---|---|---|
| **Kışla / Barracks** (`BuildingType.Barracks`) | MB 7 (MB 11) | 0,65 | Sefer savaşında 1 + L/5 mavi Lancer, yalnız Ana Üs'ün 3,5 u çevresini korur (avlanmaz). Can 70·(1+0,15(L−1)), hasar 6·(1+0,15(L−1)), 1 atk/s, 15 sn'de yeniden doğar. Yeni `DamageSource.Ally` (enum sonuna) | Bölge 3 (MB6) ilk "planlama" bölgesi; daha önce gelirse Bölge 1–2'yi önemsizleştirir. Yalnız savunma yarıçapı olduğu için kahraman avcı rolünü korur (round2 ilkesi) |
| **Köylü Evi / Cottage** (`BuildingType.House`) | MB 3 (MB 9) | 0,4 | Maden **deposu** +%(5 + 0,5(L−1)) (L15 %12; iki ev en çok +%24). Üretim hızı değişmez | Seyrek giren oyuncuya yardım eder, aktif oyuncunun dengesini bozmaz |

- Okçu/Topçu/Manastır'a yeni renk varyantı **önerilmez**: renk zaten seviye kademesi anlamı taşıyor.

## 8. Sayılar tek yerde (eklenecek sabitler)

`Progression.Island` (yeni iç sınıf, `GameData.cs` içinde `RegionMechanics`/`Counters` yanına):
```
UnlockMainBaseLevel = 5
CastleHp(k) = 300 + 80(k−1)
ArcherDamage(L) = 12·(1+0.15(L−1));  ArcherHp(L) = 150·(1+0.15(L−1))
FireCooldown = 0.5; TapBuffer = 1; CritChance = 0.10; CritMult = 2
ChargeStart = 0.35; ChargeFull = 1.2; ChargedMult = 2.5; ChargedPierce = 3; ChargedHullMult = 1.5
ArcherRange = 7; HighGroundRangeBonus = 1.5; ArrowSpeed = 14; ArcherRunSpeed = 4; MinPostSwitch = 0.6
ArcherRespawn = 8; CastleDangerRadius = 3
Volley: Radius 1.8, DamageMult 3, Duration 1.5, Cooldown 18, UnlockLevel 2
FireArrow: Shots 3, HullBurn 0.06/s × 5, UnitBurn 0.5×dmg/s × 3, Cooldown 22, UnlockLevel 4
Net: Radius 1.5, Root 2.5, Slow 0.4 × 3, BoatSlow 0.5 × 4, Cooldown 20, UnlockLevel 6
WetSeconds = 1.5; WetDamageTaken = 1.25; DisembarkInterval = 0.4; BoatStagger = 2.5; WaveBreak = 8
Boat stats: Rowboat 60/2/1.0, Longboat 150/4/0.8, Barge 300/6/0.6 (TapTaken 0.6), Ghost 500/8/0.5
Enemy shield: FrontArc 60°, FrontTapTaken 0.3; MonkHeal 0.06 / 3 s / r 2.5; RogueShortcut 0.7
AkinBattleLevel(N) = 1 + round(0.8N); AkinWaves(N); AkinBoats(N) = min(1+ceil(N/3), 7)
AkinBeachUnlock = {A:1, B:4, C:9, D:15}; AkinRequiredCastle(N) = ceil(N/3); BossEvery = 10
AkinMeat(N) = AkinWood(N) = 25 + 8N; AkinGold(N) = 20 + 5N; GemsEvery5 = 3; BossGems = 5
ProducerPerHour(L) = 30 + 15(L−1); ProducerStorageHours = 8; StorageCap(L) = 500 + 400(L−1)
IslandCostW[2..15] (table §6); type factors (table §6); ArcherUpgradeMeat(L) = 50·1.28^(L−1)
IslandTimeFactor = 0.5; TowerPlotUnlock = {2,4,8}; BarracksUnlock = 3; SecondProducerUnlock = 7
PostUnlock = {P1:1, P3:1, P4:2, P2:4, P5:5}; AreaUnlock = {east:2, southwest:3, southeast:5}
IslandLancers(L) = 1 + L/5; LancerRespawn = 12
Raid: IntervalHours 8, JitterMinutes 45, WarnMinutes 30, WindowHours 2, FirstAfterAkin 5,
      TheftCap 0.15, TheftCapHours 4, LiveLossTheftMult 0.5, LiveWinBonus 0.10,
      DefenseWeights {tower 2, barracks 1.5, archer 1, castle 1}, RaidPowerPerCastle 6,
      DefendGold(k) = 10 + 2k, PauseAfterAwayHours 24
MainBasePerk: MineBonusPerTier 0.02 at KK {3,6,9,12,15}; WoodShipment 100 wood → 10 min, 3/day
```
Ana Üs yapıları (`Progression` kökü):
- Kışla: `BarracksUnlock = {7, 11}`, `BarracksCostFactor = 0.65`, `AllyLancerHp/Damage`, `AllyGuardRadius = 3.5`,
  `AllyRespawn = 15`.
- Köylü Evi: `HouseUnlock = {3, 9}`, `HouseCostFactor = 0.4`, `HouseStorageBonus(L) = 0.05 + 0.005(L−1)`.

`GemEconomy`: `IslandResourcePerGem = 25`. Bitirme ücreti mevcut `FinishCost` ile.

## 9. Telemetri ve bot hedefleri (ORTA)

**Bot profilleri** (`PlaytestBot`, `Systems/PlaytestBot.cs:22` Telemetry'ye ada alanları eklenir):
- `islandGood`: 2,5 dokunuş/sn; tekne ve kalkanlıya dolu atış; yetenekler hazır olunca; inişe en yakın P'ye geçer.
- `islandCasual`: 1,5 dokunuş/sn; dolu atış yok; yer değiştirmez.
- `islandTowerOnly`: okçu boşta.

Beklenen durum: KK = ⌈N/3⌉, okçu = KK, kuleler KK−1. Her basamak için N = 20 tohumlu koşu.

| Akın | Beklenen KK | good kazanma | casual kazanma | Medyan kale canı | Süre |
|---|---|---|---|---|---|
| 1–3 | 1 | ≥%98 | ≥%95 | ≥0,85 | 1–1,5 dk |
| 4–9 | 2–3 | ≥%92 | ≥%85 | 0,70 | 1,5–2,5 dk |
| 10 (boss) | 4 | ≥%75 | ≥%60 | 0,45 | 3 dk |
| 11–19 | 4–7 | ≥%85 | ≥%70 | 0,55 | 2–3 dk |
| 20 (boss) | 7 | ≥%70 | ≥%50 | 0,40 | 3–4 dk |
| 21–29 | 7–10 | ≥%78 | ≥%60 | 0,45 | 2,5–3,5 dk |
| 30+ | 10+ | ≥%70 | ≥%50 | 0,40 | 3–4 dk |

- **Savaş başına kayıt:**
  - boğulan tayfa oranı;
  - dokunuş/sn (medyan, p90);
  - dolu atış hasar payı;
  - yer değiştirme sayısı;
  - kaynağa göre hasar (Okçu / Kule / Lancer);
  - kumsala göre kale hasarı;
  - yenilgi etiketi;
  - yetenek kullanımı.
- **Geçme ölçütleri:**
  - Boğulan oranı %25–55 (çok yüksekse tekne canı artar, çok düşükse azalır).
  - good profilde okçu hasar payı %45–70. `islandTowerOnly` Akın 10+ için ≤%30 kazanır (round2 ilkesi: kahraman şart).
  - good için medyan dokunuş ≤3/sn (yorgunluk sınırı). Akın 15+ için yer değiştirme medyanı 2–6.
  - Yenilgilerin ≥%90'ında bir etiket olmalı (okunabilir yenilgi).
- **Zamanlı baskın (simülasyon `Tools/economy_sim.py` + canlı kayıt):**
  - çevrimdışı çalınan oranın medyanı ≤%6, p90 ≤%12;
  - günde en az bir hırsızlık yaşayan oyuncu ≤%50;
  - canlı savunma oranı izlenir.
- **Ana Üs etkisi:**
  - economy_sim ile MB 5–12 arası günlük altın artışı ≤ +%10 (Akın + baskın + Ada Ticareti + Köylü Evi);
  - Kışla sonrası sefer bot ölçümü round2 C-metrics tablosunu korumalı: kahraman payı R3+ %30–50, combined kazanma ±5 puan.

## 10. Uygulama planı

**Teknik karar: ayrı .unity sahnesi değil, BattleScene'i "ev sahibi" olarak kullanan bir ada modu.**
- Yeni sahne `ProjectSettings/EditorBuildSettings.asset`'e eklenmeyi gerektirir. CLAUDE.md "Project Settings
  değiştirme" diyor; ayrıca editör açıkken batchmode ile sahne kurulamaz.
- Sahneler zaten kodla kuruluyor (`Managers/SceneAutoSetup.cs:44` → `BattleSceneSetup`, `Battle/BattleSceneSetup.cs:41-78`).
  Bu yüzden `IslandSession.Active` ise SceneAutoSetup `BattleSetup` yerine **`IslandSceneSetup`** ekler.
- Ev ve savaş görünümü aynı sahnede iki moddur. Akın başlarken sahne yeniden yüklenmez; geçiş hızlı olur.
- `GameManager.cs:994` `LoadScene("BattleScene")` + oturum bayrağı yeterli.
- `SceneManager.CreateScene` (çalışma anında boş sahne) reddedildi: kamera ve URP 2D renderer kurulumu riskli.
- Kullanıcı ileride editörden `IslandScene` eklerse taşıma tek satırlık bir değişiklik.
- **Görseller:** Tiny Swords `Resources` altında değil. Mevcut kalıp `Editor/BuildBaseVisualCatalog.cs`
  (menü "Tools/Base Review/Build Visual Assets" → `Resources/BaseVisualCatalog.asset`). Aynısı ile
  `IslandVisualCatalog` kurulur: tileset dilimleri, `Water Tile animated.asset`, `Water Foam`, bulutlar, Archer kareleri,
  Pawn/Sheep, binalar. Editör açıkken kullanıcı menüden bir kez çalıştırır. Tilemap çalışma anında kodla kurulur
  (`BaseSceneSetup.cs:480-520` kalıbı).

| Faz | Kapsam | Yeni dosyalar | Yeniden kullanılanlar |
|---|---|---|---|
| **1 MVP** | Ada sahne modu + tilemap (yükseklik, merdiven, köpük, bulut) · Okçu + ATEŞ/dolu/kilit/oto nişan · 4 atış yeri (P1–P4) · tekneler + batınca boğulma · iniş/ıslak · Kızıl Korsanlar · Akın 1–10 + ödüller (altın/elmas Ana Üs'e) · HUD ADA/ANA ÜS · 3 adımlı öğretici | `Island/IslandSession`, `IslandSceneSetup`, `IslandLayout` (§2 ASCII'den ızgara), `IslandArcher`, `IslandFireButton`, `RaidBoat`, `IslandRaidManager`, `IslandHud`, `Editor/BuildIslandCatalog` | `EnemyController` (`Initialize` :251, `TakeDamage(dmg, DamageSource)` :671; **rota kuyruğu** :582/:622'ye eklenir), `BattleStructure` (kale), `BattleOffscreenIndicators`, `VictoryReferenceView`, `TinyFx`, `EnemyMotion`, `L.T` |
| **2 Ekonomi** | Et/Odun, Ağıl/Oduncu/Ambar/Okçuluk/Gözcü/Kışla, Ada Ustası, alan açılışı, yetenekler, Sarı fraksiyon, Akın 30 + Kara Kaptan | `IslandEconomy`, `IslandBuildingPanel`, `IslandAbilities` | `TowerController` (kule), `BuilderTaskSave` kalıbı (ayrı tek görev), `GemConfirm`/`GemActions` (bitir/tamamla), `BaseOccupancyGrid`, `RegionBoss` ward kodu |
| **3 Zamanlı baskın** | Zamanlayıcı, uyarı, 2 sa pencere, canlı baskın, çevrimdışı çözüm, rapor | `IslandRaidScheduler` | `GameClock`, `GameNotifications` (`AvoidNight` :144), `AwayReport`, `DevMenu` (+8 sa, "baskını şimdi getir") |
| **4 Ana Üs bağları** | Kışla + Köylü Evi (`BuildingType` sonuna), sefer Lancer'ları, Ada Ticareti, Kereste Sevkiyatı, Mor/Kara fraksiyon, Hayalet Gemi | `AllyLancer` | `BattleManager` (müttefik doğurma), `BuildMenuController`, `BuildingData.ApplyLevelStats`, `PlaytestBot` sefer profilleri (yeniden ölçüm) |

- **Kayıt:**
  - `PlayerSaveData`'ya `[Serializable] IslandSaveData island` eklenir: kilit, KK/okçu seviyeleri, Et, Odun, binalar,
    bekleyen üretim + son tick, en yüksek Akın, sonraki baskın tick'i, son rapor, öğretici adımı, usta görevi.
  - Yüklemede `island == null` ise yeni nesne oluşur (eski kayıt güvenli). `CurrentVersion` (`PlayerSaveData.cs:195`) 6 olur.
  - `BuildingType` / `EnemyType` / `DamageSource` (`GameData.cs:642`) değerleri **yalnız sona** eklenir.
- **Testler:** `IslandLadderTests` (formüller, kapı, ödül), `IslandRaidTests` (çalınan oran tavanı, gece kayması,
  birikmeme), `IslandSaveTests` (eski kayıt). Bot: `PlaytestBotTests` içine `IslandLadder1..30`.

## Çakışmalar / riskler
- **Project Settings kuralı:** yeni sahne gerekiyor gibi görünse de BattleScene ev sahibi modeli kuralı çiğnemiyor.
  Risk: `ScenePolishInstaller`/`UiRevampBootstrap` savaş nesnelerini arayabilir. IslandSession'da bunlar korunmalı/atlanmalı.
- **AI_README:70** ("tek kahraman Warrior, Okçu seçimi kalkacak"): Okçu Kaptan **yalnız adaya ait** ayrı bir birimdir.
  `HeroData` Bow yolu (`GameData.cs:130`) yeniden açılmaz; ayrı `IslandArcherData` kullanılır.
- **Renk kademesi:** ada binaları/okçu hep mavi kalır (düşman renkleriyle karışmasın diye). Bu, Ana Üs görsel kuralından bilinçli bir sapma.
- **Elmas harcaması:** Kereste Sevkiyatı ve ada bitirmeleri round2 H'nin daralttığı harcamayı biraz daha azaltır.
  Elmas gelir/gider telemetrisiyle izlenir.
- **Dokunma yorgunluğu:** sürekli dokunma 3–4 dk. Dokunuş/sn ölçütü ve soru 2.
- **Stil:** DungeonMonsters2D yalnız etkinlikte; Mighty Heroes kullanılmıyor. `Dragon Warrior Files/` ve
  `ground_mine_rocks.png.meta`'ya dokunulmaz.
- **Kışla sefer dengesini değiştirir:** Faz 4'te round2 bot ölçümü yeniden koşulmadan yayına çıkmaz.

## 11. Kullanıcıya açık sorular
1. İsim: ada **Martı Adası**, kale **Kıyı Kalesi** uygun mu?
2. Dokunma yorgunluğu için **Otomatik Ateş** (yarı hız) KK 5'te açılsın mı? Kraliyet'e bağlamak P2W olur, önermiyorum.
3. **Ziyafet:** 300 Et = 1 savaş kartı jetonu (günde 1) eklensin mi? Ada → sefer bağı güçlenir, ama kart reklam
   gelirini biraz azaltır.
4. Ana Üs **Kışla** (MB 7) ada ile birlikte mi gelsin, yoksa sefer dengesi sabit kalsın diye ada oturduktan sonra mı (Faz 4)?
5. Zamanlı baskında hırsızlık (toplanmamış üretimin en çok %15'i) kalsın mı, yoksa yalnız "savun → ödül" (cezasız) mi olsun?
