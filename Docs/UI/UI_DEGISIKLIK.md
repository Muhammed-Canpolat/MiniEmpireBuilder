# UI DEĞİŞİKLİK: Ana Üs Yeni Tasarımı

Referans görseller (Docs/UIReference/):
- `ana-us-referans.png`: hedef ana üs görünümü (menü kapalı)
- `ana-us-droplist-acik.png`: sağ üst menü açıkken

AMAÇ: Ana üs ekranı referans görselle AYNI veya olabildiğince benzer görünsün. Sadece görünüm değişir, oyun mantığı, ekonomi, kayıt ve savaş DEĞİŞMEZ. Referanstaki sayılar (altın 2581, odun 227, et 43, elmas 8, inşaatçı 2/2, seviye 7) sadece temsilidir, gerçek değerler oyuncunun verisinden gelir. Referanstaki binalar/dekorlar oyunun mevcut binalarıdır, onları yeniden çizme/değiştirme.

Assetler: `Assets/MiniEmpireUI` (üst/alt bar ve butonlar), `Assets/PixelCastle` (duvar, kule, köşe), sesler `Assets/Audio/Music/SFX_Pack`. Klasör adı farklıysa Assets altında ara. Eksik asset'i uydurma, yer tutucuyla gizleme: GECE_DURUM.md'ye "EKSİK ASSET" yaz ve o kısmı atla.

## 1. Üst bar (MiniEmpireUI)
Soldan sağa: altın, odun, et | ortada kırmızı kalkan "ANA ÜS" + ana üs seviyesi (mavi bayrak süslemeli) | elmas, inşaatçı sayısı (müsait/toplam, örn. 2/2), ayarlar dişlisi.
- Tüm değerler mevcut veri kaynaklarına bağlı ve canlı güncellenir. Mevcut artış/azalış animasyonları korunsun.
- Büyük sayılar taşmasın (TMP Auto Size, ya da 12.5K gibi kısaltma varsa onu kullan; mevcut sayı biçimini bozma).
- Ayarlar dişlisi mevcut Ayarlar menüsünü açsın. Üst barda ve kalkandaki tıklama davranışları (varsa) aynen korunsun.
- Ana üs seviyesi kalkan içinde dinamik olsun.

## 2. Alt bar (MiniEmpireUI)
İNŞA ET, SEFER, KAHRAMAN, ADA butonları, ortada üstte kırmızı ok (V).
- Her buton mevcut işlevine bağlı. Butonlardaki mevcut bildirim/rozetler (varsa) yeni tasarımda da çalışsın.
- Kırmızı ok: Alt barda mevcut bir açma/kapama işlevi varsa ona bağla. Yoksa alt barı basitçe aşağı kaydırıp gizleyen/geri açan bir anahtar olsun (yalnızca görünüm, DECISIONS.md'ye yaz). Kapalıyken arazi tıklanabilirliği bozulmasın.
- Buton ikonları referanstaki gibi: çekiç (İnşa Et), çapraz kılıç (Sefer, DEĞİŞMEYECEK), miğferli kahraman, palmiyeli ada.

## 3. Duvarlar (PixelCastle)
Şu anki basit çit/duvar yerine PixelCastle duvar parçaları, kuleler ve köşe (corner outer left/right) parçaları ile arazi çevrilsin. Referans: dört köşede kule, aralarda duvar + mavi bayraklar, alt duvarın ortasında kapı.
- Duvarlar mevcut arazi sınırlarına dinamik oturmalı (arazi boyutu/genişleme değişirse duvar parçaları otomatik tekrarlanıp yerleşsin, elle sabit koyma yok).
- OYUN MANTIĞI DEĞİŞMEZ: inşa edilebilir alan, ızgara (karo) sınırları, birimlerin yürüme yolları, çarpışmalar, kayıtlı bina/dekor konumları aynı kalsın. Duvar sadece görsel olsun, gerekirse mevcut sınırın biraz dışına otursun. Oyuncu kayıtları bozulmasın.
- Sıralama (sorting): duvar/kule bina, dekor, birim ve karakterlerle doğru örtüşsün (arkadaki kule binayı kapatmasın, bina duvarın üstüne taşmasın). Duvar tıklamaları bina/dekor tıklamasını engellemesin.
- Dışarıda referanstaki gibi orman/ağaç çevresi varsa, mevcut dekor/ağaç sistemini bozmadan benzer görünüm sağla.
- Pixel art ayarları: Filter Mode Point, sıkıştırma yok/kayıpsız, Pixels Per Unit sprite'lar arasında tutarlı, bulanıklık/ince çizgi (seam) olmasın, parçalar arasında boşluk kalmasın.

## 4. Sağ üst açılır menü (droplist)
Kapalı durum: sağ üst köşede (üst barın altında) küçük ok butonu. Açılınca altında dikey liste açılır: Günlük, Görev, Etkinlik (referans: ana-us-droplist-acik.png).
- Eski sol üstteki Günlük Giriş ve Görev butonları KALDIRILSIN, işlevleri yeni menüye taşınsın: Günlük → mevcut günlük giriş ekranını, Görev → mevcut günlük görev ekranını açsın.
- İKONLAR: Günlük = takvim ikonu, Görev = parşömen ikonu, Etkinlik = kupa ikonu. Çapraz kılıç ikonu yalnızca "Sefer"de kalır (görevde KULLANMA). Takvim/parşömen/kupa ikonu MiniEmpireUI'da yoksa EKSİK ASSET yaz, yer tutucu uydurma.
- DİNAMİK ve GENİŞLETİLEBİLİR yapı: menü maddeleri koddaki sabit butonlar değil, bir liste/ScriptableObject/veri tanımı (id, ikon, etiket, açılacak panel, rozet kaynağı) üzerinden üretilsin. Yeni madde eklemek sadece listeye bir satır eklemek olsun, yerleşim (Vertical Layout) ve panel boyutu otomatik uyum sağlasın.
- Bildirim rozeti: kırmızı "!" . Kapalı ok butonunda, listedeki HERHANGİ bir maddenin bildirimi varsa görünsün (örn. alınmamış günlük giriş ödülü, tamamlanmış/ödülü alınmamış görev, yeni etkinlik). Her madde satırında da kendi rozeti olsun. Rozet kaynağı mevcut günlük/görev sistemlerinin verisi (yeni mantık kurma, var olan "alınabilir ödül" bilgisini oku). Etkinlik sistemi yoksa Etkinlik maddesi "Yakında" bilgisi veren basit bir panel/kutu açsın, rozeti kapalı olsun.
- Davranış: ok butonu menüyü açar/kapar (kısa animasyon, ok yönü döner). Menü dışına dokununca, başka bir paneli açınca veya sahne değişince menü kapansın. Menü KAPALIYKEN arazi dokunuşlarını engellemesin, açıkken liste dışındaki arazi dokunuşu önce sadece menüyü kapatsın.
- Tutorial/yönlendirme: Eski Günlük/Görev butonlarına işaret eden bir tutorial veya ok varsa yeni konuma güncelle.

## 5. Ses (SFX_Pack)
Kısa efektler (WAV): Bow Impact Hit 1, Sword Impact Hit 2, chop 2, mine 3, Fireball 1, Wave attack 1, Wave attack 2.
Döngü ses/ambiyans (OGG): Torch loop, River Stream loop, Sea Storm, Inside Night, Inside Night Storm, Inside Day, Inside Day Storm, Beach, Beach Storm.
- ART_NEEDS.md'de bu seslerin nerede kullanılacağı yazıyorsa ONU uygula. Yazmıyorsa varsayılan öneri: okçu isabeti = Bow Impact, yakın dövüş isabeti = Sword Impact, ağaç/odun toplama = chop, maden/altın toplama = mine, ateş topu yeteneği = Fireball, düşman dalgası başlangıcı/saldırısı = Wave attack 1/2; ambiyans: Ana üs = Inside Day/Night (gün-gece ve fırtına durumu varsa Storm varyantları, yoksa sadece Day), Ada üssü = Beach (fırtına varsa Beach Storm), Sea Storm ada fırtına/savaş ortamı. Torch loop ve River Stream loop için uygun nesne/alan yoksa bağlamadan bırak. Varsayımları DECISIONS.md'ye yaz.
- Mevcut ses yöneticisini kullan, yeni sistem kurma. Ayarlar'daki Müzik ve Efekt ses seviyesi/kapatma ayarlarına bağla (ambiyans efekt kanalından ya da kendi kanalından, ayara uysun).
- Sahne geçişlerinde ambiyans ve müzik önceki sahneden temizlenip yenisine yumuşak geçsin (üst üste binme, takılma, geriden çalma olmasın). Bu mevcut bug 27 ile birlikte düşünülsün.
- Import ayarları (mobil): kısa WAV'lar Decompress On Load (veya ADPCM), uzun OGG döngüler Compressed In Memory/Streaming, Vorbis; döngülerde kopma/klik olmasın. Aynı efekt hızlı tekrarlanınca sesler üst üste patlamasın (eşzamanlı ses sınırı/ses havuzu). Uygulama arka plana geçince sessizleşsin.

## 6. Unutulmaması gereken teknik kalite maddeleri
- Tüm cihazlar: Canvas Scaler "Scale With Screen Size", çapa (anchor) kullan, sabit piksel konumu yok. Safe Area (iPhone çentik/Dynamic Island, alt ev çubuğu) üst ve alt barı kesmesin. Telefon (9:16, 9:19.5, 9:21) ve tablet (4:3, 16:10) en-boy oranlarında ekran görüntüsü alıp kontrol et. Küçük ekranda üst bar sığmazsa elemanlar ölçeklensin, taşmasın.
- Bar ve çerçeveler 9-slice (Image Type: Sliced) ve doğru Sprite Editor border değerleriyle, esnek genişlikte olsun, ekran genişliğine göre gerilsin ama bozulmasın.
- iOS yazı sorunu (bug A grubu): yazılar için projede belirlenen font/TMP yapısını kullan, yeni font ekleme. Türkçe karakterleri (ç ğ ı ö ş ü İ) tüm yeni yazılarda test et.
- Bellek/performans: UI sprite'larını atlas'a koy, gereksiz büyük doku/mipmap olmasın, duvarları mümkün olduğunca toplu çiz (batching), kare başına bellek ayırma (allocation) yapma. Eski iPhone'larda akıcı kalsın.
- Zoom: ana üste zoom kaldırılacak (bug 13). Kamera yeni UI ile bar altında kalan alanı da hesaba katıp arazinin tamamını referanstaki gibi göstersin.
- Bar/menü/panel boyutları arazi tıklamasını engellemesin: yalnızca görünür UI alanı dokunuşu yutsun (Raycast Target'ları gereksiz elemanlarda kapat).
- Aynı üst/alt bar prefab'ı Ada üssü, sefer, savaş gibi başka sahnelerde kullanılıyorsa onları bozma, kontrol et ve hangisinin yeni tasarıma geçtiğini GECE_DURUM.md'ye yaz. Ada üssünün yeni tasarıma uyarlanması sonraki iş olarak not edilsin (bu görevde yapma).
- Mevcut hata düzeltmeleri bozulmasın: bina taşıma yeşil ekran düzeltmesi (bug 1), yazıların görünürlüğü (A), dekor/bina konum kalıcılığı (31, 33), altın madeni sayısının girişte büyük görünmemesi (7), ana üs savaşçı/okçu düzeni (8, 12).
- Her adımda ekran görüntüsü al (referansla aynı çözünürlükte, 9:16) ve referansla yan yana karşılaştır: renk, boyut, hizalama, çerçeve, yazı büyüklüğü farkı varsa düzelt (en fazla 3 tur).

## 7. Kabul listesi (bitti saymak için)
- [ ] Üst bar referansla aynı, tüm değerler canlı
- [ ] Alt bar referansla aynı, 4 buton çalışıyor, kırmızı ok çalışıyor
- [ ] Duvar/kule/köşe/kapı PixelCastle ile kurulu, arazi sınırına dinamik oturuyor, mantık değişmedi
- [ ] Sağ üst droplist: açılır/kapanır, Günlük/Görev/Etkinlik, doğru ikonlar, "!" rozeti, listeye yeni madde eklemek kolay
- [ ] Eski sol üst Günlük/Görev butonları kalktı, işlevleri korundu
- [ ] Safe Area ve en az 3 farklı en-boy oranında kontrol edildi
- [ ] Sesler bağlandı, sahne geçişinde müzik/ambiyans karışmıyor
- [ ] Derleme temiz, kayıtlar bozulmadı, her adım commit
