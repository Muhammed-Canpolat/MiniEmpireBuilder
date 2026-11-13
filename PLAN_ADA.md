# PLAN_ADA — 13 Kasım 2026 gece çalışması analizi

Kaynak: 4 tarama ajanının kod okuması (Sonnet, salt okunur) + belgeler. Satır numaraları yaklaşık (13 Kasım, `b6fe7bc`).
Kısaltmalar: KK = Kıyı Kalesi (ada kalesi, seviyesi Ana Üs'ü izler), MB = Ana Üs seviyesi.

## AŞAMA 1 — Düşük riskli düzeltmeler

### 1.1 Müzik (adada hep savaş müziği)
- **Kök neden:** Ada ayrı sahne değil; `GameManager.LoadIsland()` `IslandSession.Begin()` + "BattleScene" yükler.
  `AudioManager.OnSceneChanged` (Polish/Core/AudioManager.cs ~86-96) yalnız sahne adına bakar → `PlayBattleBgm()`.
  `SetMusicEnabled` (~253) aynı kontrolü yapar.
- **Plan:** `IslandSession.Active` ise `PlayIslandCalmBgm()` (baseMusic çalma listesi + rüzgâr/kuş katmanı);
  `IslandRaidManager.StateChanged` → Running: savaş müziği; Idle/Won/Lost: sakin. `PlayTrack` zaten 1 sn crossfade yapıyor
  (geçiş süresi config'ten). Bayrak: `DevSettings`/config `IslandCalmMusic` (varsayılan açık).
- **Dosyalar:** AudioManager.cs, IslandRaidManager/IslandSceneSetup (abonelik). **Risk:** düşük; aynı klip tekrar başlamaz.

### 1.2 Sabit kamera
- **Kök neden:** `IslandCameraController` kaydırma + pinch zoom; varsayılan boy `FullSize*0.70` (telefonda genişliğin %70'i).
- **Hesap:** dünya 22×36 u. Tam sığma boyu = max(11/aspect, 18) + HUD payı. 9:16 → 19,6; 19,5:9 → 23,8; 20:9 → 24,4;
  3:4 → 18 (yükseklik bağlar). Üst bar ve alt düğmeler adanın üstüne binmesin diye dikey pay eklenir.
- **Plan:** `IslandCameraController.FixedView` bayrağı (config, varsayılan açık): sabit kadraj, sürükleme/zoom kapalı.
  Yerleştirme kenar kaydırması ve savaş takip kamerası (EnterBattle/Follow) korunur ama savaş dışı sabit görünüme döner.
  `IslandCloud` ölçeği orthographicSize/19,5'e bağlı — kontrol.
- **Risk:** savaştaki HARİTA/takip akışı; testler (`IslandPhaseCTests` kamera beklentileri) güncellenebilir.

### 1.3 Ana Üs ↔ Ada geçişi (düz mavi ekran)
- **Kök neden:** `GameManager.FadeAndLoadScene` (~999-1031) siyah kararma + **senkron** `LoadScene`; kararma tuvali
  DontDestroyOnLoad değil → yeni sahnede açılma yok; ada kamerasının temizleme rengi (deniz mavisi) kurulum sırasında görünür.
- **Plan:** `SceneCloudTransition` (DontDestroyOnLoad, ScreenSpaceOverlay): bulutlar kenarlardan kapanır →
  `LoadSceneAsync` (allowSceneActivation) → sahne kurulduktan 1-2 kare sonra bulutlar açılır. Bulut sprite'ları
  `IslandVisualCatalog.clouds` (Resources'ta, ek kopya gerekmez). Bayrak: config `CloudTransition` (kapalıysa eski kararma).
  Önce yalnız Ana Üs↔Ada; diğer geçişler eski yolda (risk az).
- **Risk:** `DOTween.KillAll()` geçiş tween'lerini öldürür → geçiş tween'i DOTween yerine coroutine/unscaled zaman.

## AŞAMA 2 — Ada yapısı ve kadro
Bulgular:
- Hazır köy `IslandEconomy.EnsureSeeded` (~140-155, `villageSeeded` bayrağı) `IslandLayout.Buildings`'ten kurar:
  Ev, Oduncu, Okçuluk (3×2), Ağıl, 2 Gözcü Kulesi (adacıklar). Seviye `Progression.Island.PrebuiltLevel`:
  kule = 1 + KK/2 (KK 5 → Sv3), okçuluk = KK, diğerleri 1.
- Bedeller `Progression.Island.PlaceCost` (Ev 40 odun, Kule 60/10, Oduncu 300/60, Ağıl 600/0, Kışla 150/40, Manastır 400/80);
  Okçuluk kurulamaz. "İlk yapım bedava" yok.
- İşçi verisi yok; havuz hesaplanır: kale 2 + ev başına 2 (+Sv5, +Sv10). Ev sınırı KK'ya bağlı (1/2/3/4 @ KK1/3/6/9).
  Atama otomatik (en düşük doluluk). Kadro ekranı yok. Piyonlar kozmetik (oduncu ağaca yürür, keser; çoban; madenci).
- Ağaçlar statik (`IslandLayout.Trees`, 44 hücre), Reason.Nature ile yapı engeller; kesilince kütük + 180 sn'de büyür.
  Odun üretimi soyut sayaç; ağaçlar yalnız koşul/çarpan (yakın ağaç).
- Koyunlar 5 adet, çayır dikdörtgeninde rastgele; Ağıl'a bağlı değil.
- Baskın: manuel Akın merdiveni (güç 1+0,8N) + zamanlı baskın (Akın 5 sonrası; çevrimdışı D/R formülü, kule katkısı var).

Plan (bayrak: `IslandStartFresh` / `IslandStaffV2` / `IslandClearableTrees`, config'ten):
1. **Boş başlangıç:** yeni kayıtta yalnız kale (+ Okçuluk Alanı kaptanın atış yeri olduğu için kurulabilir yapılır).
   `villageSeeded` olan eski kayıt aynen korunur. **İlk yapım bedava:** kayıtta `freeBuildMask` (tür başına bit);
   eski kayıtta sahip olunan türler "kullanılmış" işaretlenir (göç).
2. **Bina bilgisi:** inşa kartında ve bina panelinde ikon + tek cümle (üretir/işe yarar) — `IslandBuildUi`.
3. **Kadro:** her üretim binası 1 kendi işçisi ile gelir; ev kapasitesi ek işçi yuvası verir; ev sayısı sınırı KK ile.
   Rastgele/boşta piyon üretimi kaldırılır: görünen piyon = atanmış işçi. "KADRO" paneli: işçi listesi → çalıştığı bina.
4. **Ağaçlar:** (a) temizlenebilir ağaç: dokun → küçük altın bedeli → biraz odun, hücre açılır (kayıtta `clearedTrees`);
   (b) Oduncu yarıçapındaki ağaçlar üretim ağacı (temizlenemez). Koyunlar Ağıl çevresinde. İşçi kaynağa yürür (mevcut döngü).
5. **Gözcü kuleleri:** yeni oyunda hazır gelmez, Sv1'den kurulur. Akın gücü kule seviye toplamı + KK ile ölçeklenir
   (`AkinScale`), kulesiz Akın 1–3 kaptan + kale ile geçilebilir olmalı. Eski kayıtlardaki yüksek kuleler: **korunur**
   (oyuncudan bir şey alınmaz; ölçekleme kule seviyesine bağlı olduğu için avantaj zamanla dengelenir) → DECISIONS.
6. Yeni oyuncu gibi ilk 30 dk bot/elle oynatma raporu.
- **Risk:** yüksek — kayıt göçü (v10), ada testleri (102 test) hazır köy varsayıyor; testler test kaydıyla yeni/eski iki yolu kapsamalı.

## AŞAMA 3 — UI/UX
- **Ortak üst bar:** Ada barı düz kahverengi (`IslandHud.BuildTopBar` ~105-131); Ana Üs barı `BaseHudRedesign` adla
  yeniden boyar (paylaşılamaz). Plan: yeni `ResourceTopBar` bileşeni (süslü şerit `BaseHudRedesign.OrnateStrip`,
  çipler: Altın, Elmas, Et, Odun; `NumberFormat.Short` 12.4K). Ana Üs'te Et/Odun çipi eklenir (MB<5 kilitli, dokununca bilgi).
  Adada aynı bileşen + ANA ÜS düğmesi + ada adı.
- **Ada adı:** kayıtta `island.name`, `nameChanges`, sağlama (basit hash) → ilk açılış sorusu (Unity UI InputField — projede yok, eklenecek),
  3–16 karakter, Türkçe harf, küfür filtresi (küçük TR/EN liste), bedel 0,0,50,100,150… (`GemEconomy`). Eski kayıt "Martı Adası" + teklif.
- **Sayaç:** `RaidBadgeText` "Sakin · …" → saat ikonu + "Sonraki akın: 8 sa 54 dk".
- **Ada alt düğmeleri:** 340×66 dock → daha küçük, ikonlu (ui_icon_hammer / ui_icon_sword, CampaignArt).
- **Kart Atölyesi:** ikonsuz sayılar: reklam düğmesi "REKLAM n/3", "+N kullanım", eksik Et elmas sayısı → ikonlu; bölümler başlıklı.
- **Ana Üs radyal menü:** `RadialDockMenu` (veri tabanlı `DockItem` listesi: ad, ikon, eylem, rozet sağlayıcı);
  BuildBtn/BattleBtn/HeroBtn adları korunur (FirstBaseGuide), `BottomDock` korunur. Ada düğmesi yuvarlak + ada ikonu
  (ikon sprite yok → kale sprite'ı ya da Tiny Swords ikonu; ART_NEEDS'e yaz). Durum PlayerPrefs; ilk açılış açık; ≥48dp.
  Rozet: ada baskını yaklaşırken / akın hazırken.
- **Risk:** orta — ViewportRevisionTests / HudExpansion beklentileri; kişisel cihazda bakılmadı.

## AŞAMA 4 — Ekonomi
Bulgular (ayrıntı ECONOMY.md'de):
- Kaynaklar: maden 1+0,5(L-1) altın/sn (Sv10'da 5,5/sn = ~20K/sa!) depo 200+150(L-1) (+Köylü Evi), Çırak +%10..50,
  zafer ödülü 35r+18(s-1) + öldürme altını, x2 reklam (günlük limit YOK), takvim/görev/sandık, Sonsuz kat 300+20k.
- Harcama: `MainBaseCosts` 150…6500 (×tür çarpanı), kahraman/Çırak üstel, atölye. Cüzdan tavanı 1,5×sıradaki MB bedeli.
- Hepsi koda gömülü; config yok (yalnız TimingConfig deseni).
- **Sonsuz Sefer kule bedeli hatası:** `BattleBuildCostScale(region)` = 1+0,2(r-1), Sonsuz region=99 → ×20,6
  (Okçu 1030/Topçu 1545/Manastır 1235). Düzeltme: Sonsuz'da bölge yerine kat temelli ölçek (koşu içi gelirle orantılı).
- **Sonsuz kart hatası:** `ShowEndlessPanel` → `ModeLaunchPanel` (kart arayüzü yok) → doğrudan `TryStartCampaignBattle`.
  İstila ve "sonraki kat" aynı. Düzeltme: sefer seviye panelindeki kart satırlarını (CreateCard/CreateWorkshopRow) ortak hale getirip
  Sonsuz/İstila paneline bağlamak.
- **Plan:** `EconomyConfig` ScriptableObject (`Resources/Config/Economy_Legacy.asset`, `Economy_V2.asset`, aktif profil
  `Resources/Config/EconomySettings` tek alan). Progression/GemEconomy değerleri config üzerinden. Önce simülasyon
  (`Tools/economy_sim_v2.py`), sonra ≤3× değişiklik. Elmas hızlandırma ve inşaatçı fiyatlarına dokunulmaz.
- **Risk:** yüksek — ilerleme testleri, bot ölçümleri; muhafazakâr.

## Yürütme ve model seçimi
- Uygulama: gameplay-engineer (Sonnet, orta) ajanları sırayla (aynı dosyalara iki ajan aynı anda dokunmaz; Unity batch tek seferde bir).
- Ekonomi modeli: game-designer (Opus 5.5, yüksek) yalnız tasarım/simülasyon.
- Kayıt göçü (v10): Opus.
- Denetim: ekran görüntüsü (PlayMode test görüntüleri) + benim kontrolüm.
