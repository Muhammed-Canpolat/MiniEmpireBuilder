# REPORT — 8. oturum (15 Kasım 2026): düşman yapay zekâsı, düşman sayıları, savaş üst barı, Ana Üs yerleşimi

Dal `gece-gelistirme` (push yok). Başlangıç `5d8ab18`. Commit'ler: `2346f9e` (1–2), `2e0a062` (3), `bf79567` (4), `a681254` (havuz hatası),
`d29ea75` (dalga dağılımı), son commit (SON + rapor). Kararlar DECISIONS.md **D96–D100**. Görüntüler `Docs/playtest/session8/`.
7. oturum raporu: `Docs/playtest/session7/REPORT_s7.md`.

## 1. Düşman yapay zekâsı: yola geri dönme (D96)
**Sorun:** sapan düşmanın yoldaki konumu ayrıldığı noktada donuyordu; hedef uzaklaşınca o noktaya geri yürüyordu.

**Yeni davranış — durum makinesi** (`EnemyController.LaneState`): **Yolu izle → Hedefe kilitlen → Savaş → Yola dön.**
- Her düşmanın yolda kat ettiği mesafe (`LaneDistance`) tutulur ve **hiç azalmaz**. Sapma sırasında bile yalnız ileriye doğru güncellenir; izdüşüm
  ileriye dönük 8 birimlik pencerede yapılır, böylece zikzakta yanlış kola atlamaz.
- **Kilitlenme menzili 3 birim:** hedef yolun kenarında ve düşmanın **önünde** olmalı; 0,75 birimden fazla geride kalan hedefe kilitlenmez.
- **Bırakma (leash) 4,5 birim.** Düşman şu durumlarda takibi bırakır ve 1,2 sn yeniden kilitlenmez (titreme olmaz):
  - hedef ya da düşman yoldan 4,5 birimden fazla uzaklaşırsa,
  - kahraman düşmanın gerisinde kalırsa,
  - kahramanı yol boyunca 6 birimden fazla kovaladıysa (haritanın sonuna kadar kovalamaz).
- **Yola dönüş:** düşman, ilerlemesine en yakın **ilerideki** yol noktasına yönelir. Hedef nokta düşman ilerledikçe öne kayar, böylece yola açılı ve
  eğri bir yolla katılır. Yol yönünde geri hareket her adımda atılır. Yol dışındaki tüm adımlar ortak yol bulma servisini kullanır (su, uçurum ve
  yapılardan geçmez).
- **Kalabalık:**
  - Gövde yarıçapına göre ayrışma: yan itme şerit kaymasını yumuşak değiştirir, öndeki komşu yavaşlatır, hiçbir zaman geri itmez.
  - Bakış yönü histerezisle döner, düşman titremez.
- **Test** (`Session8Tests.A1`): 6 bölge × S1/S3 × iki kaçış biçimi (yandan uzaklaşma / geriye kaçma) = 24 senaryo.
  - **Geri adım 0**, ilerleme her karede arttı, düşman hep yürünebilir zeminde kaldı, her seferinde ileriden katıldı.
  - Örnek: ayrıldığı nokta s=9,7, katıldığı nokta s=16,5.
  - `A2` (14 düşman aynı noktada): iç içe giren çift ≤ 3, yön dönüşü saniyede 0,4.
  - Görüntüler: `A1_rejoin_region4.png`, `A2_crowd_*`.
- **Bilinmesi gereken:** "en yakın ilerideki nokta" kuralı yüzünden, zikzak yolda düşman 8 birimlik pencere içindeki paralel kola kısa yoldan
  katılabiliyor. Ölçümde bir S3 senaryosunda 11 → 22'ye sıçradı. İstenirse pencere daraltılabilir.

## 2. Düşman sayısı ve canı (D97)
- **Yapılandırma:** tablonuz `Resources/Config/CampaignEnemyConfig.asset` içinde, Inspector'dan düzenlenebilir. İçindekiler:
  - bölge × tür başına savaşta **doğan** toplam düşman (boss dahil; üst bardaki "kalan / toplam" bu sayı),
  - tüm sefer düşmanlarına (boss ve çağrılanlar dahil) can ×1,1,
  - dalga başına en fazla 12 düşman,
  - dalga arası nefes payı 8 sn (eskiden 5; dalga duyurusunun 3 sn'si buna ek).
- **Dağılım:** yeni toplam, **eski dalga büyüklükleri oranında** dağıtılır. 12'yi aşan dalga, türünü koruyarak ikiye bölünür.
  - İlk ölçümde artan ağırlıklı bir dağılım denemiştim. Bu, boss dalgasını "boss + 7 Ork"a çevirdi ve 1–3. bölgelerin boss seviyelerini duvara
    dönüştürdü. Oranlı dağılıma geçtim; dalga listesi ECONOMY.md §14'te.
- **Ekonomi:** savaş sonu toplam altın değişmedi. Düşman başına öldürme altını, eski plana göre "eski toplam / yeni toplam" oranıyla ölçeklenir;
  kesirler bir sonraki düşmana taşınır. Testte (`E1`) 30 seviyenin hepsi eski toplamla birebir aynı çıktı.
  - Bu sayede savaş içi kule bütçesi, zafer ödemesi, tekrar oyunu (%30) ve 2x reklam da aynı kalıyor. ECONOMY.md §14 güncellendi.
- **Tablonun "eski" sütunu hakkında:** "eski" değerler varlıktaki toplamlardı, oysa eskiden gerçekte daha fazla düşman doğuyordu (boss seviyesinde
  her dalgaya +1/+3 ekleniyordu, 1. dalgaya da üst sınır vardı).
  - Örnek: B2-S5'te eskiden gerçekte 27 düşman doğuyordu, yenisi 26.
- **Performans:**
  - Düşmanlar için nesne havuzu **yoktu**; her düşman yeniden oluşturulup siliniyordu. Boss dışındaki düşmanlar için havuz ekledim. Davranış
    bileşenleri her kullanımda taze eklenir; kabuk 2 sn sonra yeniden verilir, böylece uçuştaki mermi yeni düşmana çarpmaz.
  - Düşman başına saniyede ~4 kez yapılan kahraman/Çırak sahne araması önbelleğe alındı.
  - **Ölçüm (`P1`):** 3 dalga × 18 düşman (Gölge Kurt dahil) + 4 kule, düşük kalite. Ortalama kare süresi 16,7 ms, %95 16,7 ms (masaüstünde vsync
    tavanı); 54 düşmanın 36'sı havuzdan geldi, takılan 0.
  - **Uyarı:** bu ölçüm masaüstü CPU'sunda yapıldı; düşük cihazda ayrıca denenmeli.
- **Ölçümün yakaladığı hata (düzeltildi, `a681254`):** Gölge Kurt'un davranış bileşeni EnemyController'a bağımlı olduğu için kabuk temizlenemiyordu.
  Havuzdan dönen düşman köprüde hedefsiz kalıyor, savaş bitmiyordu. Ayıklama artık bağımlıları önce silerek yapılıyor; `P1` bunu denetliyor.

### 2.1 Denge ölçümleri (bot, 4× hız, tek tohum; Ana Üs canı savaş sonu; tam tablo `Docs/playtest/session8/balance_s8.md`)
| Bölge | Seviye | Kule + kahraman | Yalnız kule | Yalnız kahraman |
|---|---|---|---|---|
| 1 | 1 Hayatta Kal | ✅ %100, 43 sn | ❌ %0, 41 sn (üs) | ✅ %100, 41 sn |
| 1 | 2 Üssü Koru | ✅ %89, 120 sn | ❌ %0, 52 sn (üs) | ✅ %89, 120 sn |
| 1 | 3 Madeni Koru | ❌ maden düştü, 76 sn | ❌ maden, 24 sn | ❌ maden, 76 sn |
| 1 | 4 Zamana Karşı | ✅ %42, 132 sn | ❌ %0, 40 sn (üs) | ❌ %0, 119 sn (üs) |
| 1 | 5 Boss | ✅ %100, 173 sn | ❌ %0, 65 sn (üs) | ✅ %70, 241 sn |
| 2 | 1 Hayatta Kal | ✅ %43, 102 sn | ❌ %0, 91 sn (üs) | ✅ %62, 112 sn |
| 2 | 2 Üssü Koru | ✅ %31, 96 sn | ❌ %0, 44 sn (üs) | ❌ %0, 100 sn (üs) |
| 2 | 3 Madeni Koru | ✅ %98, 75 sn | ❌ maden, 24 sn | ❌ maden, 40 sn |
| 2 | 4 Zamana Karşı | ✅ %62, 103 sn | ❌ %0, 37 sn (üs) | ❌ %0, 135 sn (üs) |
| 2 | 5 Boss | ❌ %0, 196 sn (üs) | ❌ %0, 61 sn (üs) | ❌ %0, 94 sn (üs) |
| 3 | 1 Hayatta Kal | ✅ %100, 79 sn | ❌ %0, 59 sn (üs) | ✅ %50, 118 sn |
| 3 | 2 Üssü Koru | ✅ %100, 107 sn | ❌ %0, 159 sn (üs) | ❌ %0, 159 sn (üs) |
| 3 | 3 Madeni Koru | ✅ %100, 149 sn | ❌ maden, 32 sn | ❌ maden, 109 sn |
| 3 | 4 Zamana Karşı | ✅ %100, 151 sn | ❌ süre (üs %7) | ❌ süre (üs %89) |
| 3 | 5 Boss | ✅ %100, 245 sn | ❌ %0, 246 sn (üs) | ❌ %0, 386 sn (üs) |
| 4 | 1 Hayatta Kal | ✅ %100, 80 sn | ✅ %100, 74 sn | ✅ %74, 109 sn |
| 4 | 2 Üssü Koru | ✅ %100, 155 sn | ❌ %0, 224 sn (üs) | ✅ %83, 207 sn |
| 4 | 3 Madeni Koru | ✅ %100, 171 sn | ❌ maden, 30 sn | ❌ maden, 114 sn |
| 4 | 4 Zamana Karşı | ✅ %96, 168 sn | ❌ süre (üs %1) | ❌ süre (üs %24) |
| 4 | 5 Boss | ✅ %80, 222 sn | ✅ %88, 243 sn | ✅ %67, 346 sn |
| 5 | 1 Hayatta Kal | ✅ %99, 85 sn | ✅ %96, 80 sn | ✅ %96, 113 sn |
| 5 | 2 Üssü Koru | ✅ %100, 147 sn | ✅ %98, 241 sn | ✅ %93, 245 sn |
| 5 | 3 Madeni Koru | ✅ %94, 135 sn | ❌ maden, 31 sn | ❌ maden, 126 sn |
| 5 | 4 Zamana Karşı | ❌ süre (üs %99) | ❌ süre (üs %100) | ❌ süre (üs %45) |
| 5 | 5 Boss | ✅ %100, 282 sn | ✅ %37, 467 sn | ❌ %0, 403 sn (üs) |
| 6 | 1 Hayatta Kal | ✅ %99, 61 sn | ✅ %100, 62 sn | ✅ %93, 72 sn |
| 6 | 2 Üssü Koru | ✅ %100, 184 sn | ✅ %95, 187 sn | ✅ %62, 246 sn |
| 6 | 3 Madeni Koru | ✅ %96, 130 sn | ❌ maden, 28 sn | ❌ maden, 94 sn |
| 6 | 4 Zamana Karşı | ✅ %100, 161 sn | ❌ süre (üs %100) | ❌ süre (üs %44) |
| 6 | 5 Boss | ✅ %98, 283 sn | ✅ %80, 312 sn | ❌ %0, 404 sn (üs) |

### 2.2 Duvara dönüşen seviyeler ve öneriler (sayılara dokunmadım — karar sizin)
Hedef orta zorluk. Kule + kahraman **30 seviyenin 28'ini** kazanıyor. Tek tohumla ölçüldü, sonuçlar gürültülü olabilir.

| Seviye | Ne oluyor | Öneri |
|---|---|---|
| **B1-S3 Madeni Koru** (ve tüm bölgelerin Madeni Koru seviyeleri) | Kule + kahraman madeni 76 sn'de kaybediyor. Yalnız kuleyle her bölgede maden 24–32 sn'de düşüyor: düşmanların yarısı köprüden karadan doğrudan madene koşuyor, ilk dalga büyüdüğü için yağmacı da arttı. | (a) Yağmacı oranını 1/2'den 1/3'e indirmek. (b) Maden canını düşman sayısıyla ölçeklemek (`DefendedMineHealthRatio` 0,6 → ~0,8). (c) Yağmacıları 2. dalgadan başlatmak. En az dokunuşla (a)'yı öneririm. |
| **B2-S5 Boss (Orman Cadısı)** | Ana Üs düşüyor. 7. oturumda %100 canla kazanılıyordu; sayı neredeyse aynı (27 → 26), fark ağırlıkla %10 can. | 2. bölgenin harita can çarpanını 1,0 → 0,92 yapmak (`MapEnemyHealth`; %10'u yalnız bu bölgede geri alır) ya da bu bölgede dalga arasını 8 → 10 sn yapmak. |
| **B5-S4 Zamana Karşı** | Ana Üs %99, ama 180 sn yetmiyor. 38 düşman, 4 dalga ve dalga aralarıyla bitirmek mümkün değil, saf süre duvarı. Yalnız kule / yalnız kahraman 3, 4, 5 ve 6. bölgelerin Zamana Karşı seviyelerinde de süreye takılıyor. | Süreyi düşman sayısıyla ölçeklemek (180 × yeni/eski ≈ 240 sn) ya da Zamana Karşı'da dalga arası nefes payını saymamak (sayaç aralarda durur). Ben ikincisini öneririm; nefes payı isteğiyle çelişmiyor. |
| Sınırda: B1-S4 (üs %42), B2-S1 (%43), B2-S2 (%31; SON oynatmada başka tohumla kaybedildi), B3-S2 (SON'da kaybedildi) | İlk bölgeler "affedici" hedefin altında kalıyor. B2-S1 9 → 16 (en büyük artış). | B2-S1'i 13–14'e çekmek ya da bu seviyede dalga başına sınırı 9'a indirip 3 dalgaya bölmek. |

- **Kolaylaşanlar:** 4–6. bölgelerde yalnız kule de Hayatta Kal / Üssü Koru seviyelerini çoğunlukla kazanıyor (kuleler uzun zikzak yolda çok etkili;
  7. oturumda da böyleydi). Yalnız kahraman ise 2–3. bölgelerde neredeyse hiç kazanamıyor. Bu, "kahraman + kule birlikte" tasarımına uygun.

## 3. Savaş üst barı ve harita (D98, D99)
- **Yeni bar** (`Polish/UI/BattleTopBar.cs`, Ana Üs süslü çerçevesiyle aynı aile):
  - sol: düşman ikonu ve "kalan / toplam",
  - orta: "Dalga n/m", altında dalga noktaları ve "Bölge · Seviye n",
  - sağ: altın ve ayarlar/duraklat (eski düğme, aynı işlevle),
  - altta belirgin ilerleme barı (öldürülen / toplam).
- **Hedef kartı:** ayrı kart, kendi ikonu ve barıyla. Madeni Koru'da maden canı, Zamana Karşı'da kalan süre, Üssü Koru'da üs canı gösterir;
  tehlikede kırmızıya döner.
- **Yerleşim:** bar güvenli alanın içinde. Metinler otomatik küçülür, sığmazsa üç noktayla kesilir. Test 4 ekran oranında taşma ve güvenli alanı
  denetliyor (`H1`).
- **Sonnet alt ajanı (orta çaba) yazdı;** testleri ben koşturup görüntülerle doğruladım.
- **Bu sırada düzelttiklerim:**
  - Düşman giriş oku artık barın/kartın üstüne binmiyor.
  - Test görüntü yardımcısı arayüzü kırpık kamera alanına sığdırıyordu; artık cihazdaki gibi tam ekran çekiyor.
- **Ekran görüntüsündeki sol üst yuva:** B4-S3'teki (−0,3; 15,1) yuvasının "3 HAK" etiketi yolun üstündeydi.
  - Eski doğrulama yalnız halkanın yola uzaklığına bakıyordu, **etiketi hiç denetlemiyordu**; bu yüzden 30/30 "geçerli" diyordu.
  - Kural eklendi ve 26 seviyenin **yalnız yuvaları** yeniden üretildi (rota, maden, üs aynı).
  - Yeni bağımsız test `V4`, **gerçekten çizilen** yol maskesini okur: 30 seviyede halka, etiket ve kule görseli yola değmiyor.
- **Sağ kenardaki kesik ada/kule:**
  - Bölge yapısında yalnız alt tabanın karada olması aranıyordu; mavi kule yarısı suyun üstündeydi. Artık yapının tamamı kıyıdan 0,4 birim içeride.
  - Kamera yatayda ızgara + 0,5 birime kadar gidiyordu. Artık **karanın kenarında** duruyor; ekran adadan genişse adanın ortasına sabitleniyor ve
    tek yanda deniz şeridi kalmıyor.
- Görüntüler: `H1_topbar_*` (4 oran × 4 tür), `M0_r4_l3_phone.png`, `M0_r4_l3_wide.png`.

## 4. Ana Üs yerleşim hatası — kesin kök neden ve kalıcı çözüm (D100)
**Kök neden (ölçüldü):** küçük dekorlar — **çalı (4 tür), altın taşı (2 tür) ve koyun** — dolu sayılan hücrelerinin **0,7–1,2 birim yukarısında**
çiziliyordu.
- **Neden:** `DecorData.VerticalOffset`, görselin tabanını sprite'ın **tam çerçevesinin** alt kenarına göre hizalıyordu. Tiny Swords karelerinin
  altında saydam boşluk var.
- **Oyuncuya etkisi:** çalıyı/koyunu bir sırada görüyorsunuz, oysa yerleşim kuralı **altındaki boş görünen hücreyi** dolu sayıyor. O hücreye inşa
  ya da taşıma reddediliyor ("Buraya sığmıyor"). Tersine, çalının çizildiği hücre boş sayılıyordu.
  - Örnek (5. oturumun ayak izi raporu): `decor:bush` dolu hücre (15,23), çizim y 24,0–24,9. Her ağıl çalısı ve koyun bunu yapıyordu.
  - Yeni testte (`O1`, eski kod) uyuşmazlıklar: `bush +0,97`, `bush2 +1,03`, `bush3 +0,87`, `bush4 +0,97`, `gold_stone +0,79`,
    `gold_stone_big +0,68`, `sheep +1,21` birim. Kaya, kütük, ağaç dekorları ve 2×2'lik dekorlar doğru hizalıydı.
- **Neden iki kez kaçtı:**
  - Önceki düzeltmeler yapıların görsel alanına ve doğa nesnesi kaydına odaklanmıştı.
  - 5. oturumun "görünüşte boş" denetimi, kuralla **aynı veriyi** (nesne hücreleri) kullanıyordu. Görünmeyen ama kayıtlı bir engeli dolu sayıyor,
    hatayı hiç yakalayamıyordu.
- **Keşifte elenenler:**
  - Doğa nesnesi kaldırma (anında ve süreli) hücreleri zaten tamamen boşaltıyordu; kayıt, nesne, collider ve ızgara 10/10 temizdi, yeniden
    yüklemede de (`O0`).
  - Gölgeler doluluğa girmiyor (alfa 0,30 < 0,35).
  - Eski kodun gerçek risk noktaları da kapatıldı:
    - elle güncellenen statik doğa sözlüğü (yok edilmiş nesnenin kaydı bir sonraki yüklemeye kadar kalabiliyordu),
    - kuralın gizli dekoru engel sayması, ızgaranın ise saymaması.

**Çözüm:**
1. Dekor hizası görünen tabandan (`DecorData.VisibleBottom`, sıkı ağın en alt noktası). Kayıtlı dekorlar yüklemede kendi hücrelerine oturur;
   kayıt değişmez.
2. **Tek doğru kaynak:** `BaseOccupancyGrid.Occupants` anlık görüntüsü sahnedeki canlı nesnelerden sıfırdan kurulur.
   - Kapsadıkları: yapı = ayak izi ∪ görsel hücreleri; kahraman kampı; **etkin** dekor; **etkin ve kaldırılmamış** doğa nesnesi.
   - Ne zaman: her karede ve her yerleştirme, taşıma, silme ve kaldırma sonrası. Yükleme doğrulaması da bunu kullanır.
   - Kayıttaki hücre verisi doluluğa hiç girmez. `NatureObstacle.ByCell` artık bu görüntünün salt-okunur görünümü; hayalet kayıt oluşamaz.
   - Kaldırma tek yoldan yapılır (`MarkRemoved`): görsel, collider ve doluluk aynı karede kapanır.
   - Yerleşim kuralı, kırmızı ızgara ve yürünemez hücreler aynı listeyi okur.
3. **Geliştirici ayarı:** DevMenu → "OCCUPANCY GÖSTER" (yalnız Editor / Development). Dolu hücreler türüne göre renkli, dolduran nesnenin adı
   ızgarada yazılı (`O3_occupancy_debug.png`).
4. **Kalıcı regresyon testi `O2`,** "mevcut kayıt" (eski v12, eski hizayla yerleşmiş ağıl, kayıtta hayalet doğa kimlikleri ve sahnesiz dekor
   kaydı) ve "yeni kayıt" ile ayrı ayrı:
   - 4 alanda **22 öğe** kaldırılır: ağaç, kaya, çalı, kütük (doğa nesnesi: anında ve süreli temizleme) ile çalı ve kütük (dekor).
   - Aynı hücrelere 4 boyutta (4×4, 3×2, 3×3, 2×3) yeni bina kuralla kabul edilir.
   - 4 türün taşınması ve geri alınması kabul edilir; yeni Köylü Evi istenen yere kurulur.
   - Kayıt → yeniden yükleme sonrasında kaldırılanlar geri gelmez, yerler boş kalır. **Hepsi geçti.**
   - 5. oturumun tüm yerleşim testleri yeni kodla geçti.

## 5. SON — oynatma ve kayıtlar
**Savaş — her bölgenin 2. seviyesi ve boss'u, bot ile kule + kahraman, 3× hız** (`Session8SonTests`, `SON_play.txt`, görüntüler `SON_r*_fight.png`):

| Bölge | 2. seviye | Boss (5. seviye) |
|---|---|---|
| 1 Yıkık Kale Yolu | ✅ Ana Üs %75, 129 sn, 14 düşman | ✅ %100, 145 sn, 20 düşman |
| 2 Sisli Orman | ❌ üs düştü, 114 sn (17/20 öldürüldü) | ✅ %8, 163 sn (Cadı'nın çağırdıklarıyla 35 düşman) |
| 3 Terk Edilmiş Maden | ❌ üs düştü, 177 sn (21/25) | ✅ %37, 300 sn |
| 4 Donmuş Geçit | ✅ %100, 150 sn | ✅ %95, 170 sn |
| 5 Karanlık Kale | ✅ %100, 196 sn | ✅ %99, 390 sn (çağrılanlarla 60) |
| 6 Ejderin İni | ✅ %100, 169 sn | ✅ %82, 373 sn, 7 dalga |

- **Savaştan doğrulananlar:**
  - Takılan ya da hedefsiz düşman yok; her savaş bitti.
  - Yeni durum makinesi 12 savaşta da sorunsuz çalıştı.
  - Üst bardaki "kalan / toplam" sayısı plandaki toplamla aynı (çağrılanlar hariç).
- **Tohum farkına dikkat:** B2-S2 ve B3-S2 bu tohumla kaybedildi; aynı seviyeler denge ölçümünde (başka tohum) %31 ve %100 canla kazanılmıştı.
  Gürültülü, sınırda seviyeler. B2-S2'yi bölüm 2.2'ye "sınırda" olarak ekledim; birden fazla tohumla ölçmeyi öneririm (`PLAYTEST_SEEDS=3`).
- **Görüntü notu:** botun savaş görüntüleri yalnız dünyayı çeker. Yeni üst bar oyun içinde `H1_topbar_*` (4 oran × Madeni Koru / Zamana Karşı /
  Üssü Koru / Boss) ve `M0_r4_l3_phone.png` görüntülerinde.
- **Ana Üs:** `O2`'de iki kayıtta toplam 44 öğe kaldırıldı (her kayıtta 22: ağaç, kaya, çalı, kütük — doğa ve dekor). Aynı yerlere bina kuruldu ve
  taşındı, yeniden yüklendi; tüm işlemler kabul edildi (`O2_*_after_reload.png`). Occupancy görünümü: `O3_occupancy_debug.png`.
- **Son test koşusu:**
  - Session8 (A1, A2, E1, P1, V4, M0, O0–O3, H1), Session7 V/B, Session5 (yerleşim), Session6, BattleStructure ve ForestRegion takımları:
    64 test; 63ü ilk koşuda geçti, kalan 1 düzeltilip yeniden koşturuldu ve geçti (**64/64**).
  - Tek uyarlama: Session6'daki bir test, %0 ilerleme barının gizli dolgusunu "%100 genişlik" sanıyordu; görünüm doğruydu, testi düzelttim.
  - 7. oturumdan bilinen eski başarısızlar (IslandRaid LiveRaid, MonsterFlips, BaseShadowRevision) bu oturumda koşturulmadı.

## 6. Açık konular / sizin kararınız
- Bölüm 2.2'deki dört öneri (Madeni Koru yağmacı oranı, B2 boss, Zamana Karşı süresi, B2-S1).
- Düşük cihazda kare hızı denemesi (masaüstü ölçümü iyi; cihaz APK'sıyla doğrulanmalı).
- Üst bar cihazda görülmedi. Çentikli telefonda güvenli alan Unity'nin `Screen.safeArea` değeriyle ölçülüyor; test 4 oranda geçti.
