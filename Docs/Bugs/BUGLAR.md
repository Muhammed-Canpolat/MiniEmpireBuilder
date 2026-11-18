# BUGLAR (TestFlight / iPhone testi)

Genel kural: Oyun her cihazda (iOS, Android, tablet) sorunsuz çalışmalı. Düzeltmeler sağlam olmalı, tek cihaza özel yama yapılmasın.
Format: No | Ne oluyor | Ne olmalı | Yer | Görsel (Docs/Bugs/)

## A. iOS'ta görünmeyen yazılar
2 | İnşa Et menüsünde "Yapı/Dekor/Dükkan" sekme yazıları görünmüyor | Görünsün | İnşa Et menüsü | bug-02.png
3 | Kahraman ekranında / Ana Savaşçı-Çırak tıklanınca açılan ekranda "Kahraman" ve "Çırak" sekme adları görünmüyor | Görünsün | Kahraman ekranı | bug-03.png
5 | Savaşta "Dalga 1" ve sonraki dalga yazıları görünmüyor | Görünsün | Savaş HUD | bug-05.png
10 | Ana üs seviye atlayınca çıkan banner'da (kilit/bölge açıldı bilgisi) yazı görünmüyor | Görünsün | Seviye atlama banner'ı | bug-10.png
24 | Ada üssünde akın başlayınca dalga sayısı banner'ında yazı görünmüyor | Görünsün | Ada üssü akın banner'ı | -

## B. Oyundan atma (çökme)
4 | Ekran geçişlerinde oyundan atıyor (örn. sefer haritasında "savaşa başla" sonrası), çoğu zaman | Çökmesin | Sahne geçişleri | -
6 | Savaşta "Pes et" sonrası oyundan atıyor | Çökmesin | Savaş sahnesi | -
15 | Ada üssüne ilk giriş: tanıtım, isim verme, "ilk yapım ücretsiz" onayından hemen sonra oyundan atıyor | Çökmesin, okçu kaptan kulesi inşa edilebilsin | Ada üssü ilk giriş | -

## C. Çizim / katman
1 | Bina taşırken (özellikle maden) ekranın tamamı yeşil oluyor | Normal taşıma önizlemesi | Bina taşıma modu | bug-01.png
23 | Ağıl binası duvara yakın taşınınca koyun duvarın içinde görünüyor | Koyun duvarın önünde/doğru katmanda | Ada üssü, ağıl | bug-23.png

## D. Konum kalıcılığı
18 | Dekor kaldırmak için tıklayınca "Kaldır" butonu başka dekorun/arazinin üstünde çıkıyor | Buton tıklanan dekora ait yerde çıksın | Ana üs dekor kaldırma | -
19 | Okçuluk Alanı inşa edilince okçu kaptan orada beliriyor; çıkıp girince veya ana üse gidip gelince kıyı kalesinin yanında beliriyor | Okçu kaptan Okçuluk Alanı civarında yürüsün (animasyon eksiksiz). Savaştaki durum aynı kalsın | Ada üssü, okçu kaptan | -
29 | Akın sonrası okçu kaptan akını bitirdiği yerde kalıyor; çıkıp girince kıyı kalesinin yanında beliriyor | Madde 19 ile aynı mantık (Okçuluk Alanı civarı) | Ada üssü, okçu kaptan | -
31 | Ana üsteki dekorlar (oyuncunun koyduğu ve rastgele doğan) ana üsten çıkıp girince yer değiştiriyor | Dekorlar sabit kalsın | Ana üs kayıt/yükleme, spawn | -
33 | 2. ve sonraki inşaatçı evi bir öncekinin dibine kuruluyor | Piksel karo alanı gereği dip dibe kurulamasın | Ana üs, inşaatçı evi yerleşimi | -

## E. Savaş akışı ve kilitlenme
25 | Bulut altındaki bölgenin yuvarlak savaş noktasına okçu kaptan yürüyemiyor | Bulut altında olsa da yürüyebilsin | Ada üssü savaşı | -
28 | Bulut altındaki bölgeye gidip savaşma ilk 4 akında yok, 5. akında çalıştı | İlk akından itibaren çalışsın. Neden böyle olduğu bulunup kökten düzeltilsin | Ada üssü savaşı | -
30 | Akın 6'da güneybatı koyundan gelen yarasalar alt alanda savaşamadığım için orada takılıp oyunu kilitledi (alt alan için kıyı kalesi sv.5 gerekiyor, bu kalabilir) | Alt taraftan gelen düşman beklemesin, ada içine girip saldırsın. Genel olarak hiçbir savaşta kilitlenme olmasın (güvenlik ağı) | Ada üssü savaşı, düşman AI | bug-30.png

## F. Ekonomi / kurallar
16 | Adaya ilk girişte 60 odun veriliyor | Ada açılınca odun 0, et 0 | Ada üssü başlangıç | -
22 | Ada üssünde malzeme eksikken "Yükselt"e basınca "elmasla tamamla" sorusu sorulmadan elmas harcanıp yükseltme başlıyor | Ana üsteki kural ada üssünde de geçerli olsun (onay sor). Ortak kod yolu kullanılsın, her değişiklik iki bölgeyi de gözetsin | Ada üssü yükseltme | -

## G. Ana üs görsel düzeltmeleri
7 | Oyuna girince / ana üse dönünce ilk 1-2 sn altın madeni sayıları büyük görünüyor | Büyük görünmesin | Ana üs açılışı | bug-07.png
8 | Ana üs binasının önündeki savaşçılar duruyor | Kaldırılsın. Ana üs üstündeki okçular kalsın, boyutları biraz büyüsün | Ana üs | -
9 | Köylü Evi, İnşaatçı Evi ile aynı renk | Köylü Evi assetlerdeki mor ev olsun | Ana üs, Köylü Evi | -
12 | "Okçu Kulesi" önünde 2 okçu duruyor (kötü görünüyor) | İsim "Okçu Kışlası" olsun. Tek okçu kalsın ve kuledeki yeşil kare bölgeye konumlansın | Okçu Kulesi | -
13 | Ana üste zoom yapılabiliyor | Zoom kaldırılsın | Ana üs kamera | -
14 | Bazı ağaçlar çok küçük | Tüm dekorlar (ağaç, kaya, çalı vb.) yeni oyuncunun arazisinde de "İnşa Et > Dekor" ile kurulunca olan boyutta olsun | Başlangıç arazisi dekorları | -
34 | Ana savaşçı ana üste sefer savaşlarındakinden büyük | Sefer haritası savaşlarında da ana üstekiyle aynı boyutta olsun | Ana savaşçı ölçeği | -

## H. Ada üssü arayüzü
17 | Genişletme/yükseltme bilgi kutularında sayılar cümlelerin üstüne biniyor (örn. Altın Burun, et sayısı) | Tüm bu kaymalar düzeltilsin (tüm benzer kutular taransın) | Ada üssü bilgi kutuları | bug-17.png
20 | Ada üssünde Ayarlar butonu çalışmıyor | Menü açılsın | Ada üssü | -
21 | Yükseltilen binada kalan süre görünmüyor | Kalan süre gösterilsin | Ada üssü yükseltme | -

## I. Müzik
27 | Ada üssünde savaşı yarıda bırakıp ana üsse dönünce savaş müziği kısık da olsa çalmaya devam ediyor | Her sahnenin müziği belli, geçişte müzik değişsin, tıkanma/karışma olmasın | Ses yönetimi | -

## J. Tanıtım ve iletişim
26 | Ada üssünde ilk akında tanıtım yok | İlk akında "Harita" ve "Takip" butonları tanıtılsın, yuvarlak noktalara yürüyüp savaşılabildiği gösterilsin. Ateş butonuna dokunma = saldırı, uzun basma = daha etkili atış tanıtılsın (özel vuruşlar kilidi açılınca gösterilsin) | Ada üssü akın tutorial | -
32 | Ayarlar > Destek/İletişim > "E-posta gönder" | miniempirebuilder@gmail.com adresine gitsin | Ayarlar | -

## K. Yeni özellik: Depo
11 | Depo yok | Ana üs/Ada üssünde bina/dekor/yapıya tıklayınca (ana üs ve inşaatçı evleri hariç) üstünde 3 yuvarlak buton: mevcut ikisi (Yükselt, Taşı) + yeni "Depoya Taşı" (📦 simgesi). Depoya taşınan nesne araziden kalkar, oyuncu istediğinde depodan araziye ÜCRETSİZ kurabilir. Depodaki nesne savaşta da yok sayılır (örn. 2 okçu kulesinden biri depoda ise savaşta 1 kule çalışır), diğer binaların özellikleri de aynı şekilde etkilenir | Ana üs + Ada üssü, kayıt sistemi, savaş | -
