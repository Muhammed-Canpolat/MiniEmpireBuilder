# Claude Code Prompt'ları

Kullanım: Her grup için YENİ Claude Code oturumu aç (veya önce `/clear`). Önce BAŞLIK'ı, altına ilgili grup metnini yapıştır. Grup bitince derle, test et, commit, `/clear`.

## BAŞLIK (her prompt'un başına)

Mini Empire Builder (Unity 6000.3.9f1, dal gece-gelistirme). Türkçe konuş. CLAUDE.md, GECE_DURUM.md ve DECISIONS.md'yi oku, `git status` ve `git log --oneline -5` ile durumu kontrol et. CLAUDE.md'deki kurallara aynen uy: onay bekleme, kararları DECISIONS.md'ye yaz, her işten sonra derle → test → ekran görüntüsü → commit → GECE_DURUM.md güncelle. 2 denemede çözülmeyen işi geri al, ENGELLENDİ yaz, sonrakine geç. Mevcut kayıtlar bozulmasın. Hata ayrıntıları Docs/Bugs/BUGLAR.md'de, ekran görüntüleri Docs/Bugs/bug-NN.png olarak duruyor. Oyun her cihazda (iOS, Android, tablet) çalışmalı, tek cihaza özel yama yapma.

---

## 1) A grubu: iOS'ta görünmeyen yazılar (Sonnet, orta çaba)

BUGLAR.md'deki 2, 3, 5, 10 ve 24. maddeleri oku, bug-02, bug-03, bug-05, bug-10 görsellerine bak. Hepsi iOS'ta yazının görünmemesi. Önce ORTAK NEDENİ bul (TMP font asset'i/atlas, Türkçe karakterler, iOS build'inde shader'ın ayıklanması (Always Included Shaders), fallback font, dinamik atlas ayarları gibi). Beş yeri tek tek yamama, kök nedeni düzelt. Projedeki diğer tüm yazıların aynı riske sahip olup olmadığını da tara ve raporla. Plan moduyla başla.

## 2) B + I grubu: Çökmeler ve müzik (Opus, yüksek çaba)

BUGLAR.md'deki 4, 6, 15 (çökmeler) ve 27 (müzik). Hepsi sahne geçişi anında oluyor (savaşa başla, pes et, ada üssü ilk giriş, ana üse dönüş). iOS'ta olası nedenler: bellek baskısı, sahne yüklemede kaynak bırakılmaması, async yükleme yarışları, eksik null kontrolleri, ses kaynaklarının yok edilmesi. Önce her geçiş yolunu incele ve olası çökme noktalarını listele, sonra düzelt. Sahne geçişlerine try/catch ve hata günlüğü ekle (yakalanan hata oyunu düşürmesin). Müzikte sahne değişince önceki müzik kesinlikle durup yenisi başlasın. Bu sorunlar için TestFlight çökme raporum gerekirse sana veririm, tahmini düzeltmeleri ENGELLENDİ/belirsiz diye işaretle.

## 3) D grubu: Konum kalıcılığı (Opus, yüksek çaba)

BUGLAR.md'deki 18, 19, 29, 31, 33. Ortak kayıt/yükleme ve yerleşim mantığına bak. 31: dekorlar ana üse her girişte yer değiştiriyor, rastgele dekor üretimi bir kez (tohumla/kayıtla) yapılıp kaydedilmeli. 19 ve 29: okçu kaptan konumu kıyı kalesine değil Okçuluk Alanı civarına yerleşsin ve orada animasyonlu yürüsün. 33: inşaatçı evi yerleşiminde karo çakışma kontrolü. 18: "Kaldır" butonu tıklanan dekorun üstünde çıksın (dünya→ekran koordinat dönüşümünü kontrol et). Önce kök nedenleri listele, plan modunda sun, sonra uygula.

## 4) E grubu: Savaş akışı ve kilitlenme (Opus, yüksek çaba)

BUGLAR.md'deki 25, 28, 30 ve bug-30 görseli. Düşman yapay zekâsı ve savaş akışı. 28: bulut altı bölgeye gitme neden ilk 4 akında çalışmıyor da 5'te çalışıyor, nedenini bul, ilk akından itibaren çalışacak şekilde kökten düzelt (seviyeye bağlı yanlış koşul olabilir). 25: bulut altındaki savaş noktasına okçu kaptan yürüyebilsin. 30: erişilemeyen/kilitli bölgeye giden düşman beklemesin, ada içine girip saldırsın. Ayrıca genel bir kilitlenme önleyici (watchdog) ekle: hedefsiz/yola çıkamayan/belli süre ilerlemeyen düşman otomatik olarak yedek hedefe yönlensin, hiçbir akın bitmemezlik durumunda kalmasın. Plan modunda başla.

## 5) F grubu: Ekonomi ve kurallar (Sonnet, orta çaba)

BUGLAR.md'deki 16 ve 22. 16: ada başlangıcında odun ve et 0. 22: ada üssünde yükseltme malzeme yetersizken "elmasla tamamla" onayı sormadan elmas harcıyor. Ana üsteki yükseltme/inşa akışını referans al, iki bölge TEK ORTAK kod yolunu kullansın, yoksa birleştir. Mevcut oyuncu kayıtları bozulmasın (16 yalnız yeni adalar için).

## 6) C + H grubu: Çizim ve ada üssü arayüzü (Sonnet, orta çaba)

BUGLAR.md'deki 1, 23, 17, 20, 21 ve bug-01, bug-17, bug-23 görselleri. 1: bina (özellikle maden) taşırken tüm ekranın yeşil olması (iOS'a özgü olabilir, taşıma önizleme materyali/shader/ölçek kontrolü). 23: koyun duvarın içinde, sorting/katman düzeni. 17: bilgi kutularındaki üst üste binen sayıları düzelt ve projedeki TÜM benzer bilgi kutularını tara (layout/anchor sorunu). 20: ayarlar butonu. 21: yükseltmede kalan süre göstergesi (ana üstekiyle tutarlı).

## 7) G grubu: Ana üs düzeltmeleri (Sonnet, orta çaba)

BUGLAR.md'deki 7, 8, 9, 12, 13, 14, 34. Küçük, bağımsız işler, sırayla yap ve her biri için ayrı commit at. 9: mor ev assetini kullan. 12: yeni isim "Okçu Kışlası" (tüm metin ve yerelleştirme dosyalarında), tek okçu yeşil kare bölgede. 14: başlangıç arazisindeki dekorların ölçeği İnşa Et > Dekor ile kurulanla aynı olsun. 34: ana savaşçı ölçeği ana üs ve sefer savaşlarında tek kaynaktan (aynı değer) gelsin.

## 8) J grubu: Tanıtım ve e-posta (Sonnet, orta çaba)

BUGLAR.md'deki 26 ve 32. 32: "E-posta gönder" miniempirebuilder@gmail.com adresine gitsin. 26: mevcut tutorial sistemini kullanarak (yoksa en basit biçimde) ada üssünde ilk akında Harita, Takip butonları ve yuvarlak noktalara yürüyerek savaşma, ateş butonu (dokunma = saldırı, uzun basma = güçlü atış) tanıtımlarını ekle. Tanıtımlar bir kez gösterilsin, kayda yazılsın, mevcut kayıtlar bozulmasın.

## 9) K grubu: Depo özelliği (Opus, yüksek çaba, plan modu ŞART, EN SONA)

BUGLAR.md'deki 11. Bu büyük bir özellik: kayıt sistemi, savaş ve bina özellikleri etkileniyor. Önce plan modunda şunları çıkar: veri modeli, kayıt uyumluluğu (eski kayıtlar bozulmasın), arayüz (3. yuvarlak buton 📦), depodan ücretsiz kurma akışı, depodaki bina savaşta ve diğer bina özelliklerinde yok sayılması (örn. 2 okçu kulesinden biri depodaysa savaşta 1 kule), ana üs ve ada üssü için ortak mantık, inşaatçı evi ve ana üs hariç tutulması. Planı DECISIONS.md'ye yaz, ben onaylayınca uygula.
