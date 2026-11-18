# UI DÜZELTMELER 2 (Android + iOS)

Ekran görüntüleri: `Docs/UI/Screens/` (ui2-01 ... ui2-07). Hepsi Android tablet geliştirme build'inden. İlgili maddede yalnızca o görselleri aç (token tasarrufu).
Eski UI kuralları geçerli: `Docs/UI/UI_DEGISIKLIK.md` (MiniEmpireUI stili, Safe Area, 9-slice, dinamik yerleşim). Sadece görünüm değişir; oyun mantığı, ekonomi, kayıt, savaş DEĞİŞMEZ.

GENEL ŞART: Her düzeltme Android'de de iOS'ta da aynı doğrulukla çalışmalı. Platforma özel yama yok. Birinde düzeltip diğerinde bozulan çözüm kabul değil. Gerçek cihazı Claude Code test edemez: Editor'da iki platform için (Build Target Android ve iOS) yapılabilecek her denetimi yap, doğrulanamayanları DOĞRULANMADI diye raporla.

Sıra: 1 (yazılar) → 2 → 3 → 4. Yazı sorunu çözülmeden diğer ekranları yapma, çünkü hepsi aynı yazı altyapısını kullanıyor.

## Madde 1 + 5: Yazılar bozuk / bazen görünmüyor (KÖK NEDEN, EN ÖNEMLİSİ)
Görseller: ui2-04, 05, 06 (dalga yazıları), ui2-07 (İnşa Et sekmeleri). Kahraman/Çırak sekme adları için ekran görüntüsü yok, aynı sorun.

Belirtiler:
- Savaşlarda ekranda çıkan dalga bildirim kutusu (sarı çerçeveli): içinde yazı yerine gri/beyaz kutucuklar veya yarım harfler ("İ", "Ş", dik çizgiler). Hem ada üssü akınlarında hem ana üs seferlerinde.
- İnşa Et menüsünde Yapı/Dekor/Dükkan/Depo sekme yazıları: siyah kutular, içinde harf kırıntıları.
- Kahraman ve Çırak ekranlarındaki sekme/buton adları: BAZEN görünüyor, bazen görünmüyor. Aynı sorun daha önce (bug listesi A grubu: 2, 3, 5, 10, 24) "çözüldü" denmişti ama kalıcı çözülmedi. Şimdi yama değil KÖK NEDEN düzeltmesi yapılacak.
- "Bazen görünüyor bazen görünmüyor" = dinamik karakter/atlas veya shader sorunu kokuyor. İlk akla gelen: TMP font asset'inin dinamik atlas'ı çalışma anında karakter eklemeye çalışıyor ama atlas dolu/büyüyemiyor, karakter eksik, atlas dokusu geç/yanlış yükleniyor veya shader ayıklanıyor.

Protokol (sırayla, kanıtla):
1. ENVANTER: Projedeki tüm TMP font asset'lerini, materyallerini ve shader'larını listele: atlas modu (Static/Dynamic), Multi Atlas Textures, atlas boyutu, "Include Font Data", "Clear Dynamic Data On Build", fallback listesi, kaynak font dosyası, hangi prefab/sahnelerde kullanıldığı. Bozuk yazıların hangi font asset'ini ve materyalini kullandığını bul (dalga bildirim kutusu, İnşa Et sekmeleri, Kahraman/Çırak sekmeleri, savaş HUD'ı).
2. HİPOTEZLERİ KANITLA (en az şunlara bak, sonuç yaz): atlas dolu veya tek atlasla sınırlı; Türkçe karakterler (ç ğ ı ö ş ü İ Ç Ğ Ö Ş Ü) ve küçük-büyük harf/small-caps fontlarında eksik glyph; fallback tanımsız; TMP SDF shader varyantlarının build'de ayıklanması (Always Included Shaders, Graphics API'ye göre Android Vulkan/GLES3 ve iOS Metal farkı); birden çok font asset'in aynı materyali paylaşması; atlas dokusu sıkıştırma/okunabilirlik ayarları; metin canvas'ta yanlış sıralama/mask; font asset'in sahne yüklemelerinde sıfırlanması.
3. KALICI ÇÖZÜM: Oyunda kullanılan TÜM karakterleri tara (kod içi metinler, prefab/sahne metinleri, yerelleştirme tablosu, sayılar ve semboller: % / · ▼ ✓ gibi) ve font asset'leri bu karakterlerle ÖNCEDEN hazırlanmış (static, ya da garantili dolu) olacak şekilde yeniden üret. Çoklu atlas ve fallback tanımla, Include Font Data açık olsun. Çalışma anında karakter üretimi gerekiyorsa yalnızca ek güvenlik ağı olsun. Projede tek bir güvenli font ailesi ve TMP Settings varsayılan/fallback ayarı olsun. Eski (A grubu) çözümünün kalıntılarını incele, gerekiyorsa değiştir.
4. DENETİM ARACI: Editor menüsüne "Font Denetimi" (MenuItem) ekle: tüm sahne ve prefab'lardaki TMP metinlerini, kullandıkları font asset'i ve metinlerini tarayıp eksik glyph ve fallback'e bağımlı karakterleri raporlasın. Geliştirme build'inde eksik karakter uyarıları (log) açık kalsın. Bu aracı çalıştır ve raporu sıfır eksik olana kadar düzelt.
5. KONTROL EKRANLARI (hepsi): dalga bildirim kutusu (ada akını + sefer), seviye atlama banner'ı, İnşa Et sekmeleri (Yapı, Dekor, Dükkan, Depo), Kahraman ve Çırak sekmeleri, savaş HUD butonları, Zafer/Yenilgi, yeni üst/alt barlar, intro, ayarlar.
6. İKİ PLATFORM: Build Target Android ve iOS için shader/Graphics API/stripping ayarlarını ayrı ayrı doğrula. Çözüm platformdan bağımsız olsun.

Ekran görüntülerinde ayrıca gördüğüm yazı TAŞMALARI (aynı çalışmada, Auto Size min/max + kontrast/outline ile düzelt): "HARİTA", "TAKİP", "OTO KAPALI" butonları taşıyor; "ATEŞ OK", "YAĞMUR", "Sv6" yetenek butonlarında yazı daire dışına taşıyor/kesiliyor; İnşa Et kartlarında "KART ATÖLYESİ" ve "MANASTIR" sığmıyor; sefer savaşında "Sonraki dalga 3" yazısı zemine karışıp okunmuyor.

## Madde 2: Ana üste alt bar kapanınca siyah zemin
Görsel: ui2-01.
Alt barın kırmızı oku ile bar aşağı kapanınca alt bölge siyah kalıyor (arazinin ve duvarın altında). İstenen: duvarların dışında kalan yeşil zemin ve dekorlar (ağaç, çalı, kaya, kütük) ekranın tamamını (üst barın altından en alta kadar, sol-sağ dahil) kaplasın, hiçbir siyah/boş alan kalmasın.
- Önce nedeni bul: kamera görüş alanı, arka plan rengi, bar panelinin siyah arka planı, kamera viewport ayarı veya zeminin bittiği sınır.
- Duvarın dışına doğru zemini ve dekoru, yukarı/aşağı/sol/sağ tüm ekran oranlarında (9:16, 9:21, 4:3 tablet) ve Safe Area dışında da kalan bölgeleri doldurana kadar uzat. Bar açıkken de kapalıyken de ekran dolu görünsün.
- Dış dekorlar SABİT olsun: sabit tohumla (seed) yerleşsin, her girişte yer değiştirmesin (bug 31). Oyun mantığına girmesin: tıklanamaz, inşaya açık alan değil, kayda yazılmaz, yürüme yolunu etkilemez. Mevcut dış orman görünümüyle (referans ui2-01'deki ağaçlar) tutarlı olsun.
- Performans: çok sayıda nesne yerine tilemap/toplu çizim, mobilde kare hızını düşürmesin.
- Bar kapalıyken kırmızı ok butonu ve üstündeki kırmızı nokta bildirimi korunsun, bar açılıp kapanma animasyonu bozulmasın.

## Madde 3: Ada üssü üst barı ana üstekiyle aynı stilde
Görsel: ui2-02 (şu an), ana üs referansı: `Docs/UIReference/ana-us-referans.png`. Alt bar ŞİMDİLİK değişmeyecek (sonraki iş).
- Üst bar ana üstteki MiniEmpireUI üst barının aynısı olsun (aynı prefab/bileşenleri mümkün olduğunca paylaş, tekrar kopya yapma): altın, odun, et | ortada kırmızı kalkan | elmas, (varsa) diğer sayaç | ayarlar dişlisi.
- Ortadaki kalkan ana üste "ANA ÜS + ana üs seviyesi" gösteriyor; ada üssünde "KIYI KALESİ + kıyı kalesi seviyesi" göstersin (dinamik, gerçek veri).
- Ada üssünde şu an üst barda bulunan ve KORUNMASI gereken işlevler: "ANA ÜS" (ana üsse dönüş) butonu, oyuncu/ada adı (CANPOLAT), ayarlar, sağ üstteki kalan süre sayacı (15sa 21dk kum saati). Bunlar yeni barda yer değiştirebilir ama işlevi kaybolmasın. Önerim: Ana Üs butonu barın altında sol tarafta MiniEmpireUI butonu olarak; ada adı küçük etiket olarak barın altında veya kalkanın altında. Daha iyi bir yerleşim bulursan uygula, kararı DECISIONS.md'ye yaz.
- Akın sırasında üst barda şu an "AKIN 6 · DALGA 1/2" yazısı gösteriliyor (ui2-04, 05). Bu bilgi yeni tasarımda da okunaklı şekilde gösterilsin (örn. kalkanın altında bir şerit/plaka), yazı sorunu (Madde 1) çözülmüş fontla.
- Ana üs barında olan her yuvanın ada üssünde karşılığı yoksa (örn. inşaatçı sayısı 2/2) uydurma veri koyma: yuvayı çıkar ve kalan elemanları dengele. Veri kaynakları ve davranışlar aynı kalsın.
- Safe Area, 9:16 / 9:21 / 4:3 ekran oranları ve savaş sırasında (akın) tutarlı görünüm kontrol edilsin.

## Madde 4: Ada üssü Zafer / Yenilgi ekranları
Görsel: ui2-03 (şu an: yarı saydam koyu panel, çok boşluk, düz yazı).
- MiniEmpireUI stilinde yeniden tasarla: çerçeveli panel, üstte kurdele/şerit başlık ("ZAFER!" altın, "YENİLGİ" kırmızı/koyu tema), içeriğe göre boyutlanan kompakt panel, arkada ekranı karartan ama okunurluğu bozmayan zemin (arkadaki oyunun yarım görünmesi yazıyı bozmasın).
- Ödüller ikonlu satırlar olsun: altın, odun, et ikonları (üst barda kullanılan ikonlar) + miktar. Metin: "Akın 5 temizlendi." alt başlık. "Onarımda (ücretsiz): Oduncu 1:20" satırı onarım ikonuyla, okunaklı.
- Butonlar yeni buton stilinde: "SONRAKİ AKIN" ana (altın) buton, "TEKRAR" ve "ADAYA DÖN" ikincil. Yenilgide uygun butonlar (mevcut mantığa göre: Tekrar ana, Adaya Dön ikincil).
- Kısa giriş animasyonu (ölçek/solma, unscaled time), mevcut ses yöneticisiyle buton sesi (yeni ses sistemi kurma).
- Tüm değerler (ödül miktarları, akın numarası, onarım süresi) mevcut mantıktan dinamik gelsin. Buton işlevleri değişmez.
- Telefon ve tablette taşma yok, Safe Area uyumlu, tüm yazılar Madde 1 çözümüyle sağlam fontla.
- Sefer (ana üs savaşı) sonuç ekranı aynı prefab'ı kullanıyorsa tutarlı güncelle, kullanmıyorsa dokunma ve GECE_DURUM.md'ye "sonraki iş" yaz.

## Kabul listesi
- [ ] Font Denetimi sıfır eksik, tüm ekranlarda yazılar okunuyor (dalga kutusu, sekmeler, Kahraman/Çırak, HUD)
- [ ] Çözüm Android ve iOS için aynı, platforma özel yama yok
- [ ] Alt bar kapalıyken siyah alan yok, dış zemin/dekor sabit, 3 ekran oranı kontrol edildi
- [ ] Ada üst barı ana üstle aynı stil, "Kıyı Kalesi" kalkanı, Ana Üs dönüş + ad + süre sayacı + akın bilgisi korunuyor
- [ ] Zafer/Yenilgi yeni tasarımda, ikonlu ödül satırları, butonlar çalışıyor
- [ ] Oyun mantığı, kayıtlar ve ekonomi bozulmadı, her adım ayrı commit
