# GECE_DURUM — Otonom çalışma (14 Kasım 2026, 6. oturum: savaş hataları, kahraman/çırak/ekonomi, dükkân, ada ilerlemesi, arazi)

DURUM: SÜRÜYOR (6. oturum)
ŞU ANKİ İŞ: 1.5 Savaşta duraklatma
Kararlar: DECISIONS.md D68+ · Başlangıç commit'i: b2605ef · Testler: Session6Tests (editör kapalı, doğrudan projede)

6. OTURUM İŞ LİSTESİ:
- [x] 1.1 Tamamlanmamış inşaat savaşa yansımasın (D68) — Session6Tests.S1_1
- [x] 1.2 Menzil çemberleri (D69) · [x] 1.3 Can barları (D70) · [x] 1.4 Uzun yürüyüş animasyonu (D71) · [ ] 1.5 Savaşta duraklatma
- [ ] 2.1 Çırak maden katkısı · [ ] 2.2 Kahraman/Çırak yükseltme süresi + mevcut→sonraki · [ ] 2.3 Maden deposu · [ ] 2.4 Dükkân + IAP
- [ ] 3.1 Adaya ilk giriş · [ ] 3.2 Ada başlangıcı + öğretici · [ ] 3.3 Ada seviyeleri bağımsız · [ ] 3.4 Akın dengesi
- [ ] 4.1 Arazi genişletme şeffaflığı · [ ] 4.2 Süreli temizleme + kompakt menü
- [ ] 5 Savaş kartı butonları · [ ] 6 Bütünsel denetim · [ ] SON

ENGELLENENLER:
- (yok)

## Referans görsel — 6. oturum
- Sefer savaşı "Yıkık Kale Yolu · Seviye 4", DALGA 2/2, SÜRE 1:42, altın 110. Okçu Kulesi (mavi çatı) Ana Üs'ün sol üstünde; çevresindeki
  kesikli altın menzil çemberi kulenin ~1,4 birim ALTINDA ortalanmış: kuzey kenarı kulenin hemen üstünde, güney kenarı çok aşağıda (1.2).
  Ana Üs'ün can barı ~%10 dolu ama dolgu sol kenardan içeride (1.3). Alt kartlar: Savaşçı 179/260, Ana Üs 256/700, Çırak 48/165 —
  Ana Üs kartında sarı dolgu kısa/ortalanmış görünüyor (1.3). Sağ altta düşmanın üstünde "+ 2 HAK" inşa yuvası halkası.

---
# GECE_DURUM — Otonom çalışma (14 Kasım 2026, 5. oturum: yerleşim + yol bulma + Taşı/Sil/GERİ AL UI)

DURUM: TAMAMLANDI (5. oturum)
ŞU ANKİ İŞ: (yok) — rapor REPORT.md
SIRADAKİ İŞ: kullanıcı cihazda dener (özellikle yeni bina dokunuş menüsü); 2.1'in adaya bağlanması; sonra Google Play (PLAY_RELEASE §8)
Kararlar: DECISIONS.md D62+ · Başlangıç commit'i: 21bd545
Model notu: yerleşim/yol bulma = Opus yüksek çaba; UI işleri Sonnet sınıfı/orta çaba yeterli (kullanıcı).

5. OTURUM İŞ LİSTESİ:
- [x] 1.1 Ana üs yerleşim sistemi denetimi (D62) — Session5Tests tarama: ızgara uyuşmazlığı 0, çirkin kabul 0; NatureObstacle (0,0) hayalet hatası bulundu
- [x] 1.2 Hiçbir sahnede karakter yapı içinden geçmesin (D63) — Ana Üs/ada/savaş/Madeni Koru rota + yürütme testleri
- [x] 1.3 Dekor/hayvan ölçeği ortak tablodan (D64)
- [x] 2.1 Taşı/Sil eylem menüsü (D67; Sonnet alt ajanı) — Ana Üs dekor + bina/kule; ADA BAĞLANMADI (ada paneli zaten TAŞI'lı)
- [x] 2.2 ✓/✗ hayaletin yanında, geçersizken ✓ soluk/pasif (D66)
- [x] 2.3 GERİ AL küçük, sol alt, 3 sn + azalan halka + sönme (D66)
- [x] 2.4 "Önerilen düzen" bayrakla kapalı (D65)
- [x] SON: 12 bina taşı/geri al, dekor kur/taşı/sil, normal + Madeni Koru savaşı; REPORT.md. Session5Tests 18/18, Session5UiTests 3/3

ENGELLENENLER:
- (yok). Önceden var olan 3 başarısız test REPORT §5'te (başlangıç commit'inde de başarısız).

## Referans görseller — 5. oturum
⚠️ Mesajda 3 görsel tarif edildi; ulaşan 3 dosyanın **1. ve 2.'si aynı** (Taşı/Sil paneli), 3.'sü GERİ AL. "Buraya sığmıyor" hatasının
görseli **ulaşmadı**; 1.1/2.2 için kullanıcının yazılı tarifi esas: kalenin altındaki boş alan reddediliyor, ✓/✗ ve "Buraya sığmıyor"
etiketi taşınan binanın değil ana kalenin üstünde duruyor.

### Görsel 1 (=2) — Dekora dokununca açılan Taşı/Sil paneli (Ana Üs, 1280×2048, Development Build)
- Üst bar: altın 20.7K, odun 567, et 211, arma "15 ANA ÜS", elmas 46, inşaatçı 4/4, dişli. Solda GÜNLÜK ve GÖREV.
- Panel: sağ kenarda, ekran dışına taşan koyu yeşil-kahve dikdörtgen şerit (y≈580–740, x≈700→ekran dışı); içinde iki düz koyu
  dikdörtgen düğme "TAŞI" ve "SİL +2" (ikon yok, "+2"nin neyi iade ettiği belirsiz). Panel ağılın (sağ üst koyun/çalı alanı) üstünü
  tamamen kapatıyor; seçili öğe vurgulanmıyor.
- Alan düzeni: üst köşelerde iki taş kule, üst ortada iki kırmızı sancak arasında Kışla (mavi kenarlı taş bina), solda alt alta 4 mavi
  çatılı ev ("z" uyku işaretli), ortada koyu kilise (Kart Atölyesi?), merkezde büyük kale, sağda kırmızı çatılı ev, altında maden (altın
  kayalar + kırmızı kulübe + çırak), solda mavi kilise (manastır), ortada kahraman + sarı bayrak, sağda iki sarı çatılı ev, alt köşelerde
  iki okçuluk alanı, alt ortada ikinci Kışla. Radyal menü kapalı.
### Görsel 3 — Taşıma sonrası GERİ AL
- Ekranın ortasında (y≈1290–1440, x≈380–900, ekran genişliğinin ~%40'ı) büyük koyu hap: solda halkalı turuncu geri oku, sağda altın
  "GERİ AL". Kahramanı ve bayrağı tamamen kapatıyor. Radyal menü açık (SEFER, KAHRAMAN, İNŞA ET, ADA).
- Bu görüntüde **ağıl (sağ üst)**: 4×5 çalı + 6 koyun **çok küçük** (çalılar ~25 px, koyunlar ~15 px) → 1.3 gerilemesi.

---
# GECE_DURUM — Otonom çalışma (14 Kasım 2026, 4. oturum: hatalar + temel sistemler + UI/UX)

DURUM: TAMAMLANDI (4. oturum)
ŞU ANKİ İŞ: (yok) — rapor REPORT.md
SIRADAKİ İŞ: kullanıcı cihazda dener; görselleri yeniden gönderirse karşılaştırma; sonra Google Play (PLAY_RELEASE §8)
Kararlar: DECISIONS.md D45+ · Başlangıç commit'i: d1d8d5a
BEKLEYEN (kullanıcı kararıyla ertelendi): Google Play işleri (PLAY_RELEASE §8) — hatalar ve UI/UX bittikten sonra.
Test notu: Unity editörü açık olduğu için PlayMode testleri proje kopyasında (`D:/UnityProjects/MEB_TestRunner`, robocopy ile eşitlenir) koşturuluyor.

4. OTURUM İŞ LİSTESİ:
- [x] 1.1 Ortak yol bulma (Ana Üs / Ada / Savaş) + yeniden rota + debug görsel (D45)
- [x] 1.2 Tek seçim yöneticisi (boş yer / başka öğe / geri tuşu) (D46)
- [x] 1.3 Madeni Koru beyaz kare + kayıp sprite taraması (D47)
- [x] 1.4 Ada çalı/kaya temizliği (D48) — testler Session4Tests 7/7, görüntüler Docs/playtest/session4/S1_*
- [x] 2.1 Dekor 1x1 (D49) · 2.2 Dekor kataloğu (D50) · 2.3 Maden çırağı (D51) · 2.4 Kışla devriyesi + ölçek tablosu (D52) · 2.5 Yükseltme pencereleri (D53) · 2.6 Elmas bitirme (D54) — Session4+GemEconomy 17/17
- [x] 2.7 Kart Atölyesi (D55; Sonnet alt ajanı)
- [x] 3.1 Alt bar aslan motifi · 3.2 ADA düğmesi (D56) · 3.3 Ayarlar (D57; Sonnet alt ajanı) · 3.4 Genel UI/UX turu (D58)
- [x] SON: baştan oynatma 49/49 + son düzeltmeler 39/39, REPORT.md

ENGELLENENLER:
- (yok)

## Referans görseller — 4. oturum
⚠️ Mesajda bahsedilen 3 görsel (ve 2.7'deki 4. görsel) bu oturuma **ulaşmadı**; yalnız metin geldi. Aşağıdaki detaylar kullanıcının yazılı
tariflerinden derlendi; görseller gelirse bu bölüm güncellenmeli.

### Görsel 1 — Sefer "Madeni Koru" savaş ekranı
- Madeni temsil eden yerde **düz beyaz kare** (sprite atanmamış/kayıp; 1.3).
- Kışla kurulu: Ana Üs yakınında beliren mızraklı savaşçılar (Lancer) **ana savaşçıya göre çok büyük** (2.4).
- HUD: **"MADEN %100"** yazısı üstteki inşa halkasının üzerine biniyor; halkanın **"6 HAK"** etiketi sıkışmış (3.4).

### Görsel 2 — Ada üssü
- İşçiler binaların üzerinden geçip yürüyor (1.1).
- Ağaç dışındaki küçük çalı, kaya, ot gibi arazi öğeleri kaldırılamıyor (1.4).

### Görsel 3 — Ana Üs (alt radyal menü açık)
- Radyal menü açıkken **Sefer** ve **Kahraman** düğmeleri binaların üstüne biniyor; **"KAHRAMAN" etiketi Sefer düğmesinin altına kayıyor**,
  etiketler birbirine çok yakın; arka plan kararmıyor (3.4).
- Kahraman şapel ve bayrakla üst üste duruyor (3.4).
- Alt barda iki yanında aslan arması olan eski motif yok, sade duvar dokusu var (3.1). ADA düğmesi kötü görünüyor (3.2).

### Görsel 4 — Kart Atölyesi (2.7)
- Başlık şeridi ve X ekran dışına taşıyor; "Et eksik (165): Ağıl ya da 💎 21" yarım; kuyruk boşken "HIZLANDIR" başlığı;
  kart ikonları ada uymuyor; "▶ Reklamla üret (3/3)" + yeşil mücevher; "+3 kullanım" ikonu görev parşömeniyle aynı; kural metni yoğun;
  YÜKSELT maliyet/kazanç göstermiyor; boş yuva metni sola yapışık; nadirlik etiketleri çerçeveye değiyor.

---
# GECE_DURUM — Otonom çalışma (13 Kasım 2026, 3. oturum: oyun testi geri bildirimi 2 + Google Play hazırlığı)

DURUM: TAMAMLANDI (3. oturum)
ŞU ANKİ İŞ: (yok) — rapor REPORT.md
SIRADAKİ İŞ: kullanıcı: cihaz denemesi + PLAY_RELEASE §8 (upload key, gizlilik URL'si, formlar, 12 test kullanıcısı)
Kararlar: DECISIONS.md D31+ · Başlangıç commit'i: ce2f308 · AŞAMA 1: b730d34 · AŞAMA 2: 1c0a19e · AŞAMA 3: 52b5856 · AŞAMA 4: ef28f4c

3. OTURUM İŞ LİSTESİ:
- [x] 1.1 Akın haritası eski görüntü
- [x] 1.2 Takılı bildirim rozeti (tüm menü rozetleri)
- [x] 1.3 "Ağaç, Altın Taşı ya da Çayır var" hayalet engel + migration + debug görsel
- [x] 1.4 Ana Üs yerleşim çakışmaları
- [x] 2.1 Radyal menü %20-25 küçük, alt bar boş süs yuvaları
- [x] 2.2 Üst bar sıralaması (config)
- [x] 2.3 Akın sayacı "10sa 0dk / 45dk / 30sn"
- [x] 2.4 Ortak yükseltme penceresi
- [x] 2.5 Süslü Geri Al + adada taşıma
- [x] 3.1 Ana Üs animasyonları
- [x] 3.2 Okçu noktaları
- [x] 3.3 Akın yetenek butonları + efektler
- [x] 4 Google Play hazırlığı (AAB 0.1.0/1, 92,6 MB, 16 KB 7/7 uygun; hata ayıklama imzası) (PLAY_RELEASE, DATA_SAFETY, PRIVACY_POLICY, STORE_LISTING, AAB)
- [x] SON: görüntüler Docs/playtest/session3, baştan oynatma (Nov13 Journey), REPORT.md

ENGELLENENLER:
- (yok)

## Referans görseller — 3. oturum (kullanıcının mesajı; tek referans bu tariflerdir)

### Görsel A — Ana Üs (dikey ~1280×2048, Development Build)
- **Üst bar** (süslü koyu ahşap/metal, altın köşeler, Kelt düğüm): soldan altın sikke "9309", mavi elmas "20", işçi ikonu "4/4",
  ortada aşağı sarkan kalkan arma "15 / ANA ÜS", sağında et "76", odun kütüğü "261", en sağda kalkan çerçeveli altın dişli.
  → İstenen sıra: Altın, Odun, Et, Elmas, İnşaatçı (2.2).
- **Sol kenar:** yuvarlak altın çerçeveli "GÜNLÜK" (takvim) ve "GÖREV" (parşömen) düğmeleri.
- **Alan:** çitli dikdörtgen çayır. Üst köşelerde iki taş gözcü kulesi (içinde okçu), üst ortada mavi çatılı ev; sol tarafta
  iki taş bina (yassı kahverengi çatı) üst üste, hemen altlarında mavi çatılı ev → **taş bina ile alt ev değiyor** (1.4);
  ortada manastır/kilise; sağ üstte 4 koyun + 9 çalı (hareketsiz); merkezde büyük kale (Ana Üs, kapıda askerler); sağda ev;
  sol orta kırmızı çatılı ev, altında mavi çatılı kilise; ortada maden (altın kayalar, küçük kırmızı kulübe, 2 işçi, külçeler);
  maden altında kahraman şövalye (kılıç+kalkan) ile sarı bayrak **iç içe** (1.4); sağda iki ev yan yana; alt köşelerde iki
  okçuluk alanı (hedef tahtalı, çitli) — **sol alttaki fener okçuluk alanının üstünde** (1.4), yanlarında kırmızı sancaklar ve
  fenerler; alt ortada ev. Bazı binaların üstünde "z z" uyku işaretleri. Çit dışında çamlar, çalılar, taşlar.
- **Alt bar:** koyu ahşap şerit; ortada büyük yuvarlak altın çerçeveli ana düğme (çapraz kılıç + kalkan arması), sağ üst
  kenarında kırmızı rozet (**takılı**, 1.2). Şeridin solunda ve sağında aslanlı kalkan süsleri ve iki boş düğüm plakası
  (**işlevsiz süs yuvaları**, 2.1). Ana düğme çok büyük (ekran genişliğinin ~%19'u).

### Görsel B — Ada üssü
- **Üst bar:** soldan "ANA ÜS" düğmesi, altın harf "CANPOLAT" (ada adı), altın "9309", elmas "20", et "76", odun "261", dişli.
  → İstenen sıra: Ana Üs, Ada adı, Altın, Odun, Et, Elmas (2.2).
- **Sayaç:** sağ üstte kum saatli hap "10s 0dk" ("s" saniye gibi okunuyor → "10sa 0dk", 2.3).
- **Ada:** kuzeyde kayalık plato üzerinde mavi taş kale, yanında işçi ve okçu; sol üstte ayrı adacıkta gözcü kulesi, sağda
  ayrı adacıkta ikinci gözcü kulesi; taş yarlarla ayrılmış orta teras: kulübe (oduncu), ev, okçuluk alanı, altın taşı;
  koyunlar; ortada ev + et; güney yarısında açılmış geniş çayır ve yoğun çam/sarı ağaç ormanı; alt köşelerde iki küçük
  adacık; bulut örtüsü yalnız kenarlarda.
- **Alt:** ortada büyük yuvarlak radyal ana düğme (adanın alt ucunu kapatıyor; %20-25 küçülecek, 2.1).

---
# GECE_DURUM — Otonom çalışma (13 Kasım 2026, 2. oturum: oyun testi geri bildirimi)

DURUM: TAMAMLANDI (2. oturum)
ŞU ANKİ İŞ: (yok)
SIRADAKİ İŞ: kullanıcı cihazda dener; "18" yazısının yeri sorulacak (D29 notu)
TESTLER: ada/radyal/UiNight/ekonomi/baştan oynatma/Nov13 grupları 150/150; görüntüler Docs/playtest/feedback13

2. OTURUM (13 Kasım geri bildirimi, kararlar D24–D30):
- [x] 1 Akın sonu: ADAYA DÖN + yerinde bulut geçişi (D27)
- [x] 2 Ada radyal menü (İNŞA ET · AKIN pul · KADRO) (D25)
- [x] 3 Kompakt akın sayacı + dokunma açıklaması (D26)
- [x] 4 KADRO menüye taşındı; NW adacığı Z0, bulut kapsaması yeniden pişirildi + test/görsel (D24)
- [x] 5 Ada kadrajı: ağaç tepeleri + alt pay 0,10 (D28)
- [x] 6 Ana Üs alt barı: arma + rozet hizası (D29); "18" bulunamadı (kullanıcının görselinde de alt barda yok) → soruldu
- [x] 7 Veri kaynağı aynı (test iki yönde karşılaştırır) + baştan oynatma J1–J6
- [x] 8 2x reklam sınırsız, öneri eşiği Ana Üs 4 (D30); AI_README güncellendi

---
# (önceki) 1. oturum

# GECE_DURUM — Otonom gece çalışması (13 Kasım 2026)

DURUM (1. oturum): TAMAMLANDI
SON AKTİVİTE: 2026-11-13 21:00
ŞU ANKİ İŞ: (yok) — rapor: REPORT.md
SIRADAKİ İŞ: sabah gözden geçirme (REPORT.md §1 ⚠️ ve §6)

TAMAMLANANLAR:
- AŞAMA 0 analiz → PLAN_ADA.md (e1d1b4c)
- ECONOMY.md + Tools/economy_sim_v2.py (95968d8) — V2 önerisi simüle edildi
- AŞAMA 1: müzik (6eb2989), sabit kamera (ad14e72, 0173606), bulut geçişi (1f32657); Island testleri 107/107
- AŞAMA 2: boş başlangıç/bedava hak/bilgi, kadro, ağaç/koyun, Akın ölçeği, kayıt v10 (a8f82cc, 5851b4d); Island 115/115; ilk 30 dk: Docs/playtest/gece/first30_island.md
- AŞAMA 3A–E,G: ortak üst bar, ada adı (v11), sayaç, ada düğmeleri, atölye ikonları (be03bf2..475fbdb); 146 testten 144 (2 bilinen eski)
- AŞAMA 3F: radyal alt menü + atölye kozmetik (b7ee615, d3b84ce, 11fd74f); RadialMenuTests 8/8
- AŞAMA 4: yarım kalan değişiklikler incelendi ve tamamlandı (D15): EconomyConfig Legacy/V2 (7c3569e), 2x günde 5, ganimet
  satırı, Sonsuz/İstila kartları, Ada Tüccarı (ca33aa4), EconomyProfileTests 7/7 (7ec5ad1); ekonomi grupları 21/22 (1 bilinen eski)
- SON: yeni hesapla baştan oynatma (sınırsız elmas kapalı) kırılmadan bitti — 6 sa 48 dk simüle, Ana Üs 5, ada (ad, Akın 1,
  Tüccar), sefer paneli (dc8232f; Docs/playtest/gece/son); kararma geçişi hatası (bf39bd2); eski 5 testten 4'ü düzeldi (9427eb5);
  bot testi gerilemesi düzeltildi; REPORT.md yazıldı

ENGELLENENLER:
- (yok) — açık bulgular REPORT.md §6 (Topçu köşe gölgesi testi, erken oyunda B1-5)

Dal: `gece-gelistirme` (push yok). Başlangıç commit'i: `b6fe7bc`.
Kararlar: `DECISIONS.md`. Plan: `PLAN_ADA.md`. Ekonomi: `ECONOMY.md`. Son rapor: `REPORT.md`.

## Mevcut takip/dokümantasyon dosyaları (okunur ve kullanılmaya devam edilir)
- `CLAUDE.md` — kısa proje özeti, komutlar, kurallar (var; yenisi oluşturulmadı).
- `Docs/AI_README.md` — ayrıntılı mimari/devir notu (ana referans).
- `PROGRESS.md` — eski gece çalışması kararları/ilerlemesi.
- `AUDIT.md`, `ROADMAP.md`, `MORNING_REPORT.md`, `BALANCE.md`, `ART_NEEDS.md`.
- `Docs/design/island_base_v2.md` (§14–§22 Ada v2 fazları), `island_base.md`, `card_workshop.md`, `balance_v1.md`, `round2.md`.
- `Docs/REVAMP_CHECKLIST.md`, `Docs/playtest/` (seçilmiş görüntüler), `Tools/economy_sim.py`.
- Hafıza: `~/.claude/projects/.../memory/` (project-autonomous-queue, feedback-workflow).

## Referans görseller (kullanıcının 13 Kasım mesajı; oturum kesilirse tek referans bu tariflerdir)

### Görsel 1 — Adanın hedef görünümü (sabit kamera kadrajı), dikey 1280×2048 civarı
- **Üst bar (şimdiki ada barı, koyu petrol yeşili, ~%7 yükseklik):** solda kahverengi çerçeveli "ANA ÜS" düğmesi; yanında
  altın sarısı iki satır "MARTI / ADASI"; ortada Et ikonu "0", Odun kütüğü "317", altın noktası "6345"; sağda yuvarlak koyu
  zeminli altın dişli (ayarlar). (Kullanıcı bu barın Ana Üs'teki süslü çerçeveyle değişmesini istiyor — AŞAMA 3.)
- **Sayaç hapı:** barın hemen altında sağ üstte, koyu yarı saydam yuvarlatılmış hap: "SAKIN · 8 SA 54 DK" (anlamı belirsiz →
  "Sonraki akın: 8 sa 54 dk" + ikon olacak).
- **Kadraj:** Ada ekranın neredeyse tüm genişliğini kaplar; yanlarda yalnız ince deniz payı (~%5–8). Dikeyde adanın kuzey ucu
  (kale platosu) barın hemen altından başlar, güney ucu alt düğmelerin hemen üstünde/altında biter; ada TAMAMEN görünür,
  kaydırma/zoom yok. Deniz turkuaz, kıyıda köpük ve küçük kayalar; kenarlarda bulut kümeleri.
- **Ada içeriği (kuzeyden güneye):** üstte kayalık platoda büyük mavi taş kale (Ana Üs benzeri) + çevresinde çam/sarı ağaçlar,
  sol üstte ayrı küçük adacık; ortada taş yarlarla ayrılmış teras: solda kulübe+işçi, ortada ev, yuvarlak gözcü kulesi,
  okçuluk alanı (hedef tahtalı çitli bina); sağda ayrı küçük adacıkta ikinci gözcü kulesi; altta geniş çayır: koyunlar,
  ağıl/ev, işçiler, yoğun çam ormanı; en güneyde bulutla kaplı (keşfedilmemiş) bölgeler.
- **Alt düğmeler:** iki büyük dikdörtgen düğme ekranın ~%10'u kadar yüksek: solda koyu kahverengi "İNŞA ET", sağda altın
  "AKIN 5". Adanın alt kısmını kapatıyorlar → daha küçük, ikonlu, Ana Üs stil ailesinde olacak.

### Görsel 2 — Ana Üs'ün mevcut hali
- **Üst bar (hedef ortak stil):** süslü koyu metal/ahşap çerçeve, altın köşe süsleri, Kelt düğüm deseni. Soldan: altın sikke
  ikonu + "6345", mavi elmas + "111", ortada aşağı sarkan kalkan biçimli seviye arması "15 / ANA ÜS", sağda işçi ikonu "4/4",
  en sağda kalkan çerçeveli altın dişli.
- **Sol kenar:** yuvarlak altın çerçeveli düğmeler: takvim "GÜNLÜK", parşömen "GÖREV".
- **Alan:** ahşap çit + taş direklerle çevrili dikdörtgen çayır; dışında çamlar. İçeride: üst köşelerde iki gözcü kulesi,
  üst ortada ev, ortada kilise/manastır, merkezde büyük kale (Ana Üs), iki yanda evler, solda kırmızı çatılı ev, ortada
  maden (altın kayaları + kırmızı kulübe + işçiler), sarı bayrak, altta iki okçuluk alanı, kahraman şövalye, fenerler,
  kırmızı sancaklar, alt ortada ev; sağ üstte koyunlar ve çalılar.
- **Sağ alt (dock üstü):** düz dikdörtgen koyu "ADA" düğmesi (kaldırılacak).
- **Alt dock:** koyu ahşap/tuğla şerit; solda "İNŞA ET" (çekiç+harita ikonu), ortada büyük altın "SEFER" (çapraz kılıç +
  aslanlı kalkan), sağda "KAHRAMAN" (şövalye portresi). Kare, altın çerçeveli düğmeler.

### Görsel 3 — Ana Üs alt menüsü hedef tasarımı (üç adım, üstten alta)
1. **KAPALI:** Ekran altında, tüm genişlikte koyu süslü bir şerit/kurdele (altın kenarlı, ortası koyu). Ortada şeridin
   üstüne taşan büyük YUVARLAK süslü altın çerçeveli ana düğme: içinde altın kalkan amblemi + ortada mavi/turkuaz mücevher.
   Başka düğme görünmez; harita (ev, kuleler) tam görünür.
2. **TIKLAMA / AÇILIŞ:** Ana düğmeye dokununca etrafında altın ışıltı halkası/ışın patlaması (radyal parıltı, parlayan
   kıvılcımlar), düğme hafif büyür/parlar; alt butonlar ana düğmenin arkasından çıkmaya başlar.
3. **AÇIK:** Ana düğmenin üstünde YAY şeklinde 4 yuvarlak düğme (soldan sağa, yayın alt-sol ucundan alt-sağ ucuna):
   **İNŞA ET** (en solda, en alçakta; harita+çekiç ikonu), **SEFER** (sol üst; çapraz kılıçlı aslan kalkanı, vurgu),
   **KAHRAMAN** (sağ üst; şövalye portresi), **ADA** (en sağda, alçakta; palmiyeli küçük ada ikonu).
   Her biri altın süslü yuvarlak çerçeve, koyu iç zemin, ikon; altında koyu kurdele üzerinde beyaz/krem büyük harf etiket.
   Ana düğme yayın merkezinde altta, alt butonlardan büyük. Şerit ekranın altında kalır.
- Stil: koyu fantezi, altın kenar, Ana Üs üst barıyla aynı aile. Animasyon: kapalı→açık yay boyunca dışa doğru yayılma
  (ölçek 0→1 + konum), kapanırken geri toplanma.
