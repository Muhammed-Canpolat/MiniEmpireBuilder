# INTRO SAHNESİ ENTEGRASYONU

Referans: `Docs/UIReference/intro-referans.jpeg` (hedef görünüm).
Kurulum rehberi: `Docs/Intro/KURULUM.md` (hazır paketin kendi rehberi). Aşağıdaki maddeler KURULUM.md ile çelişirse BU DOSYA geçerlidir.

AMAÇ: Oyun açılınca ilk sahne bu intro olsun: referanstaki arka plan ve logo, altta yükleme çubuğu (%0 → %100) ve ipucu yazısı. Ana oyun sahnesi arka planda yüklenirken çubuk dolar, %100 olunca intro kararıp oyun açılır. Sadece açılış deneyimi eklenir, oyun mantığı, kayıtlar ve ekonomi DEĞİŞMEZ.

## Hazır paket (Assets/MiniEmpire altında)
- Sprites/Intro: intro_background (941x1672, logo dahil), loading_bar_frame, loading_bar_fill, logo_mini_empire_builder, icon_tip_bulb
- Scripts/Intro (namespace MiniEmpire.Intro): IntroSceneController, LoadingBar, TipRotator, UIIdleAnimation
Paketi yeniden üretme, uydurma sprite/script ekleme. Eksik/bozuk bir şey varsa GECE_DURUM.md'ye yaz.

## Dikkat edilecekler (paket bunları kendi başına çözmüyor)
1. **Çift logo:** Logo arka planın içine pişmiş durumda. Ayrı "Logo" katmanını arka planın üstüne KOYMA ve UIIdleAnimation'ı logoya BAĞLAMA (üst üste iki logo/gölge çıkar). Referansla aynı görünüm esas. Logo animasyonu istenirse sonraki iş olarak notla (katmanlı görsel gerekir).
2. **İlk sahne ve build index:** Şu an Build Settings'te index 0 hangi sahne? Intro index 0 olacak. Eski ilk sahne `gameSceneName` olarak intro'dan yüklensin ve her şey (kayıt yükleme, ses yöneticisi, DontDestroyOnLoad nesneleri, başlatıcılar) eskisi gibi çalışsın. Koddaki `LoadScene(0)`, `buildIndex == 0` veya "ilk sahneye dön" mantıkları varsa bul: oyuncu menüden/ana üsten "başa dönünce" intro'ya düşmesin, eski ilk sahneye gitsin. Sahne adlarını sabit "MainMenu" varsayma, projede gerçek adı bul.
3. **Güvenli sahne geçişi (iOS çökme geçmişi var, bug 4/6/15):** Async yüklemeyi hata yakalamayla sar: sahne adı Build Settings'te yoksa veya op null ise günlüğe yaz ve doğrudan `SceneManager.LoadScene` ile yedek yükleme yap, intro'da takılı kalma olmasın. Yükleme sonrası intro sahnesi bellekten tamamen çıkmalı.
4. **Gerçek yükleme:** Çubuk, gerçek yükleme ilerlemesini ve minimum gösterim süresini birlikte yansıtsın (paket böyle yapıyor, koru). Çubuk %100 olduktan sonra oyun sahnesi etkinleşirken uzun donma (1 sn+) oluyorsa nedenini (ağır Awake/Start) bul ve raporla, oyun mantığına dokunma.
5. **Süre:** minimumShowTime (2.5 sn), fade süreleri ve %100 bekleme süresi SerializeField kalsın. Toplam açılış yaklaşık 4 sn. Uygulama arka plandan dönünce intro TEKRAR çıkmasın.
6. **Application.targetFrameRate:** Paket Start içinde 60 ayarlıyor. Projede bu ayar başka yerde varsa çakışmasın, tek yerde kalsın (mevcut ayarı koru, intro'dan kaldır).
7. **Yazılar (iOS'ta yazı görünmeme sorunu):** Yüzde ve ipucu yazıları için projede A grubunda belirlenen iOS-güvenli TMP font asset'ini kullan, yeni font ekleme. Türkçe karakterler (ç ğ ı ö ş ü İ) font atlasında olduğunu doğrula. Yazılar sprite'a pişirilmesin.
8. **İpuçları (TipRotator):** İlk ipucu referanstaki: "Ordunu güçlendir, imparatorluğunu büyüt!". Pakettekiler dışındaki 3 ipucu örnek olarak yazılmış: oyunda GERÇEKTEN var olan özelliklere göre güncelle (yanlış bilgi yazma), kısa ve Türkçe olsun. Liste tek yerde, kolay düzenlenebilir dursun.
9. **Import ayarları (mobil):** Arka plan: ASTC (iOS) / ETC2 veya ASTC (Android), Max Size 2048, mipmap kapalı, Filter Bilinear (illüstrasyon). "Compression: None" kullanma (bellek israfı). Bar ve ikon sprite'ları: kalite kaybı olmasın (yüksek kalite), Mesh Type Full Rect, Fill için sprite Single. UI sprite sıralaması için gereksiz Read/Write kapalı.
10. **Ekran oranları ve Safe Area:** Canvas Scaler Scale With Screen Size (referans 941x1672, Match 0.5). 9:16, 9:21 ve 4:3 (tablet) için kontrol et: logo kesilmesin, çubuk ve ipucu iPhone alt ev çubuğunun ve çentiğin altında/üstünde kalmasın (Safe Area). Arka plan ekranı tam kaplasın (kenar boşluğu/siyah bant olmasın), tabletlerde logo kırpılıyorsa çözümü seç (örn. genişliğe sığdır + üst/alt kenar rengiyle doldur) ve DECISIONS.md'ye yaz.
11. **Ses:** Yeni ses/müzik ekleme. Müzik, oyun sahnesi yüklenince mevcut ses yöneticisiyle başlasın. İntro sırasında önceki sahneden kalan ses olmamalı.
12. **Git/LFS:** Yeni .png ve .meta dosyalarını commit'e dahil et, `.gitattributes`'taki mevcut LFS kuralına uy. Sahne, `.meta` ve tüm bağımlılıklar commit'lensin.

## Yapılacaklar
1. Sahne: projenin sahne klasörüne `Intro.unity` oluştur (hiyerarşi KURULUM.md'deki gibi, YALNIZCA Logo katmanı ve UIIdleAnimation hariç). IntroSceneController referanslarını (LoadingBar, CanvasGroup) ve gameSceneName'i bağla. Sahneyi Editor script (MenuItem) veya projedeki mevcut Unity aracıyla oluştur. Elle yapılması gereken bir adım kalırsa ENGELLENDİ deme: `Docs/Intro/MANUEL_ADIMLAR.md` dosyasına adım adım yaz.
2. Build Settings: Intro index 0, eski ilk sahne index 1 (diğer sıralar bozulmasın).
3. Yukarıdaki 12 maddeyi uygula.
4. Test: Editor'da Intro sahnesinden başlayıp oyuna geçişi dene (Game view 9:16 ekran görüntüsü, referansla yan yana karşılaştır, fark varsa düzelt, en fazla 3 tur). 9:21 ve 4:3 görüntüleri de al.
5. Derleme temiz, console'da hata/uyarı (yeni) yok.

## Kabul listesi
- [ ] Oyun intro ile açılıyor, görünüm referansa çok benziyor (tek logo, temiz)
- [ ] Çubuk %0'dan %100'e doluyor, yüzde yazısı doğru, bitince oyun açılıyor
- [ ] İpucu yazısı döngüsü çalışıyor, Türkçe karakterler görünüyor
- [ ] Oyun içinden "başa dönme" intro'ya düşürmüyor, kayıtlar ve ana üs aynı
- [ ] 9:16, 9:21, 4:3 ve Safe Area kontrol edildi
- [ ] Sahne geçişinde hata yakalama ve yedek yükleme var
- [ ] Hiçbir eski ayar/kayıt bozulmadı, her şey commit'lendi
