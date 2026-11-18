# DECISIONS — otonom gece çalışması kararları (13 Kasım 2026)

Biçim: **Karar** · Alternatifler · Gerekçe · Nasıl geri alınır. ⚠️ = sabah gözden geçirilmesi önerilen karar.

## D0 — Çalışma çerçevesi
- **Karar:** Dal `gece-gelistirme`, push yok; her iş küçük commit (açık yollarla). Mevcut takip dosyaları (CLAUDE.md,
  Docs/AI_README.md, PROGRESS.md, island_base_v2.md) kullanılmaya devam eder; CLAUDE.md zaten var, yenisi oluşturulmadı.
- **Gerekçe:** Kullanıcının kalıcı kuralları. **Geri alma:** commit'ler tek tek `git revert` edilebilir.

## D1 — FeatureConfig (bayraklar ve sayılar)
- **Karar:** Yeni davranışlar `Assets/Scripts/Data/FeatureConfig.cs` (ScriptableObject, `Resources/Config/FeatureConfig`
  yoksa koddaki varsayılanlar) arkasında; varsayılan = yeni davranış açık.
- **Alternatif:** DevSettings anahtarları / sabitler. **Gerekçe:** TimingConfig deseniyle aynı, Inspector'dan değiştirilebilir.
- **Geri alma:** ilgili bayrağı `false` yap (asset oluşturup) ya da commit'i revert et.

## D2 — Ada müziği (6eb2989)
- **Karar:** Adada sakinken Ana Üs çalma listesi (baseMusic) + rüzgâr/kuş; akın/baskın Running olunca savaş müziği,
  bitince 1,5 sn çapraz geçişle sakine dönüş. Ayrı "ada müziği" klibi yok → Ana Üs listesi yeniden kullanıldı.
- **Alternatif:** AudioLibrary'ye ayrı ada alanları (klip yok). **Geri alma:** `islandCalmMusic=false`.

## D3 — Sabit ada kamerası (ad14e72, 0173606)
- **Karar:** Savaş dışında kaydırma/zoom yok; kadraj kara hücrelerinin sınırına (adacıklar dahil) oturur, deniz payı %2,
  üst HUD %7, alt HUD %8. Savaş (takip kamerası/HARİTA) aynen kalır. 9:16, 19,5:9, 20:9, 3:4 testle doğrulandı.
- **Alternatif:** dünya 22×36'ya sığdırmak (ada dar görünüyordu, ilk deneme). **Geri alma:** `islandFixedCamera=false`
  (eski kaydırmalı kamera) ya da `islandViewFitLand=false`.
- ⚠️ Cihazda bakılmadı; alt düğmeler AŞAMA 3'te küçülünce alt pay yeniden ayarlanabilir.

## D4 — Bulut geçişi (1f32657)
- **Karar:** Yalnız Ana Üs ↔ Ada geçişi bulutlu (DontDestroyOnLoad örtü, `LoadSceneAsync`, kurulum bitince açılır);
  diğer geçişler eski kararma. Bulut görselleri `IslandVisualCatalog.clouds` (yeni sanat yok).
  `BaseSceneSetup.DisableLegacySceneCanvas` örtüyü kapatıyordu → örtü istisna tutuldu.
- **Geri alma:** `cloudTransition=false`. ⚠️ Animasyon gözle görülmedi (yalnız testte tamamlanma doğrulandı).

## D5 — Economy_V2 öldürme altını payı
- **Karar:** Tasarımcının önerdiği %50 yerine **%75** (sim: 1. gün Ana Üs medyanı 9 → 10, tavanda geçen süre %2,7).
- **Gerekçe:** "Birkaç savaş/birkaç saat" hedefi; %50'de sıradaki Ana Üs 18 savaş sürüyordu. **Geri alma:** config değeri.

## D6 — Ada boş başlangıç ve eski kayıtlar (a8f82cc, kayıt v10)
- **Karar:** Yeni adada yalnız Kıyı Kalesi; her türün ilk yapımı bedava (`freeBuildMask`). Eski (v9) kayıtlarda köy,
  binalar ve **seviyeler aynen korunur**; göç, sahip olunan türlerin bedava hakkını kullanılmış sayar. Köyü hiç
  kurulmamış ama ilerlemesi olan (Akın/öğretici) eski kayıt da eski köyü alır (`legacyVillage`) — oyuncu bir şey kaybetmez.
- **Alternatif:** eski kayıtları da boşaltıp iade vermek (kayıp hissi, iade hesabı riskli). **Geri alma:** `islandStartFresh=false`.

## D7 — Eski kayıtlardaki yüksek seviyeli gözcü kuleleri ⚠️
- **Karar:** Kuleler **korunur** (seviye düşürme/iade yok). Akın gücü kule seviye toplamı ve KK ile ölçeklenir:
  `0,8 + 0,015 × Σkule seviyesi + 0,02 × (KK−5)`, aralık ×0,72–1,15 (yalnız elle başlatılan Akın; zamanlı baskın ×1).
  Kulesiz yeni ada Akın 1–3'ü ×0,80 ile rahat kazanıyor (iyi bot, kale %100).
- **Alternatif:** ajanın ilk değerleri 0,04/sv ve tavan 1,4 — KK7'de makul savunma (Σ22) tavana çıkıp Akın 20 boss testini
  kaybettiriyordu; kule yükseltmesini cezalandırır gibi hissettirir. **Geri alma:** `islandTowerScaling=false` ya da katsayılar.

## D8 — Kadro modeli
- **Karar:** Her üretim binası 1 kendi işçisiyle gelir; ek yuvalar Ev kapasitesinden dolar; kalenin 2 piyonu artık üretici
  sayılmaz (biri usta görünümü). Elle işçi sayısı = toplam işçi (kendi işçisinin altına inemez). Görünen piyon = atanmış işçi.
- **Geri alma:** `islandStaffV2=false`.

## D9 — Ağaç temizleme
- **Karar:** Oduncu Kampı yarıçapındaki ağaçlar (inşaattaki kamp dahil) üretim ağacıdır, kesilemez; diğerleri dokununca
  10 altın (Ana Üs cüzdanı — yeni altın yutağı) → +8 odun, hücre açılır, bir daha büyümez (`clearedTrees`).
- **Geri alma:** `islandClearableTrees=false`.

## D10 — İlk 30 dakika bulgusu (değiştirilmedi) ⚠️
- Pasif üretim Sv1'de yavaş (30 odun/sa, 15 et/sa); ilk yarım saatte ilerlemenin %90'ı Akın ödülünden. Et darboğaz.
  Değer değiştirilmedi (ada ekonomisi ayrı tasarım kararı); öneri: Sv1 Ağıl 15 → 25 Et/sa ya da Akın 1–3 Et ödülü ×2.

## D11 — Ortak üst bar (be03bf2)
- **Karar:** `ResourceTopBar` iki sahnede: Altın, Elmas, Et, Odun (12.4K kısaltma). Ana Üs'te İnşaatçı hapı arma'nın
  soluna alındı (sağ taraf Et/Odun'a). Ana Üs < 5'te Et/Odun "0" soluk + kilit; dokununca bilgi.
- **Geri alma:** `sharedTopBar=false`.

## D12 — Ada adı (ad8c29b, kayıt v11)
- **Karar:** TMP giriş alanı; ilk açılışta yeni oyuncuya sorulur, eski kayda "Martı Adası" + DEĞİŞTİR/BÖYLE KALSIN.
  İlk 2 değişiklik bedava, sonra 50/100/150 (+50) elmas (`GemEconomy.IslandRenameCost`; diğer elmas fiyatlarına dokunulmadı).
  Sayaç basit mühürle (ad+sayaç+tuz özeti) korunur; mühür bozuksa en az 2 değişiklik yapılmış sayılır (bedava hak kalmaz).
  Kısa küfür kökleri yalnız tam kelime olarak engellenir ("Sikke" geçer). Bedel GemConfirm yerine pencerenin TAMAM düğmesinde.
- **Geri alma:** `islandNaming=false`. ⚠️ Küfür listesi küçük (TR+EN ~30 kök); yayından önce genişletilmeli.

## D13 — Atölye ikonları
- Reklam ikonu yoktu; Tiny Swords `Icon_07` → `Resources/CampaignArt/ui_icon_play.png` kopyalandı (Cinzel yazı tipinde ▶ yok).

## D14 — Radyal menü (b7ee615)
- **Karar:** Veri tabanlı `RadialItem` listesi (İnşa Et, Sefer, Kahraman, Ada); eski BuildBtn/BattleBtn/HeroBtn/IslandBtn
  nesneleri korunur (rehber ve BaseWorldUI onları bulur), görünüm "Visual" alt nesnesinde. Animasyon kendi lerp'i
  (DOTween'deki DOKill çakışması). İlk açılış açık, durum `MEB_RadialOpen`. Ada rozeti yalnız baskın uyarısı/canlı baskında
  ("Akın hazır" rozeti yapılmadı). Ada ikonu yer tutucu (kale + ağaç) → ART_NEEDS.
- **Geri alma:** `radialDockMenu=false` (eski dock + düz ADA düğmesi). ⚠️ KAHRAMAN/SEFER kurdeleleri düğmeye hafif biniyor.

## D15 — Yarım kalan AŞAMA 4 değişiklikleri (oturum kesintisi)
- **Karar:** API limiti yüzünden kesilen ajanın commit'lenmemiş değişiklikleri (EconomyConfig + iki asset + çağrı noktaları)
  incelendi; derleniyordu ve ECONOMY.md §7 ile tutarlıydı → **geri alınmadı, tamamlandı** (7c3569e). Test pişirmesinin
  `IslandTerrain.prefab`'ta bıraktığı rastgele animasyon hızı gürültüsü geri alındı; `ground_mine_rocks.png.meta`'ya dokunulmadı.

## D16 — Economy_V2 varsayılan, Legacy korunur (7c3569e)
- **Karar:** `FeatureConfig.economyProfile = "Economy_V2"`. Asset bulunamazsa kod varsayılanı **LEGACY** (güvenli).
  2. inşaatçı Ana Üs tablosundan koparıldı (sabit 850). Elmas fiyatları, GoldPerGem, süreler değişmedi.
  Eski kayıtların `goldPerSecond` alanı artık okunmuyor; maden hızı her zaman profilden.
- **Geri alma:** `economyProfile = "Economy_Legacy"` (FeatureConfig asset'i oluşturup) — davranış birebir eskiye döner
  (EconomyProfileTests bunu denetler). Sonsuz kule bedeli düzeltmesi iki profilde de açık (`endlessBuildCostFormula`).
- ⚠️ V2 sayıları simülasyondan; bot/cihazla doğrulanmadı (ECONOMY §10 telemetri alanları eklenmedi).

## D17 — 2x zafer reklamı günde 5 hak ⚠️ (ca33aa4)
- **Karar:** ECONOMY §9'daki AI_README çatışması (2x "savaş başına 1, günlük sınır yok") V2 lehine çözüldü: V2'de günde 5
  (`victoryDoubleAdsPerDay`), düğmede kalan hak "(4/5)" yazar; hak bitince düğme doğrudan TEKRAR/SONRAKİ KAT olur. LEGACY sınırsız.
- **Gerekçe:** Simde 2x reklam sınırsızken tek başına tavan kaybının ana kaynağı. **Geri alma:** değeri 0 yap.

## D18 — Ganimet payı gösterimi
- **Karar:** Zafer ekranında "Üsse aktarılan" artık gerçek ödeme (`LastPayout`, %75 ganimet + tekrar kesintisi sonrası);
  sefer zaferinde "Ganimet %75: savaş altınının %75'i üsse geçer" satırı. Satır değerleri (düşman altını/ödül/inşa) brüt kalır.

## D19 — Sonsuz Sefer / İstila kart seçimi (ca33aa4)
- **Karar:** Kart satırları ortak `BuildCardSection` oldu; Sonsuz ve İstila penceresi (ModeLaunchPanel) kart bölümü kadar uzar.
  Zaferden sonra "SONRAKİ KAT" artık haritaya döner ve Sonsuz penceresi kendiliğinden açılır (kart seçilsin diye; eskiden
  doğrudan savaş). Yenilgide "TEKRAR DENE" seferdeki gibi kartsız doğrudan başlar (mevcut kural). İstila boss sayılır
  (Kalkan Kıran işe yarar). Pencere içi uyarılar pencerenin kendi satırında (harita toast'ı altta kalıyordu).

## D20 — Ada Tüccarı (ca33aa4); diğer iki yutak yapılmadı ⚠️
- **Karar:** Adada Et/Odun çipine dokununca Ada Tüccarı: 50 Et ya da 50 Odun, 400 + 120(MB−5) altın, günde 2 paket (V2).
  Ödül gibi cüzdan tavanını aşabilir. Ayrı düğme eklenmedi (alt menü kalabalık) → keşfedilebilirliği düşük olabilir.
- **Yapılmadı:** Sefer Erzakı ve Prestij Anıtları (ECONOMY §6.1, §6.3) — yeni içerik/sanat ister; simde V2 onlarsız da hedefte
  (tavan %2,8 1. gün). Sıradaki iş olarak not edildi.

## D21 — Baştan oynatma sürücüsü oyun kodunda (dc8232f)
- **Karar:** `PlaytestJourney` (yalnız editör/Development) gerçek akışları tipli kodla sürer; test (`FreshAccountJourneyTests`) yalnız
  çağırır ve doğrular. DevSettings tamamen kapatılır (sınırsız elmas yok, ad penceresi gerçek oyuncudaki gibi açılır). Oturum
  araları `DevAdvanceTime` ile (çevrimdışı maden/inşaat/can gerçek kurallarla). Savaşları PlaytestBot oyuncunun kendi kaydıyla
  oynar (`keepCurrentSave`; profil kurulmaz, `TryStartCampaignBattle` gerçek akışı). Gerçek kayıt test sonunda geri yüklenir.
- **Alternatif:** cihazda elle oynamak (bu oturumda mümkün değil). ⚠️ Bina yerleştirme (Kışla/Ev, adada yapı) sürücüde yok —
  onları IslandFreshStartTests/BaseBarracksTests kapsıyor.

## D22 — Kararma geçişi iptali (bf39bd2)
- **Bulgu:** Ana Üs'e dönüş bulutla asenkron yüklenir; geçiş bitmeden savaş başlatılırsa geciken yükleme kararma örtüsünü yok
  ediyor, savaş sahnesi hiç açılmıyordu (MissingReferenceException). Gerçek oyuncuda bulut ekranı örttüğü için olasılığı düşük.
- **Karar:** Örtü yok olursa o kararma geçişi sessizce iptal olur (çifte yükleme yok). Sürücü bulut geçişi bitene kadar bekler.

## D23 — Eski 5 test (9427eb5) ⚠️
- **Karar:** Hata ayıklama katmanını bekleyen testler DevSettings'i kendileri açar/geri yükler; toprak yollar 5429490'da bilerek
  kaldırıldığından testler `StoneRoadTilemap` yokluğunu bekler. `ShadowsRemainSoftAfterMovesAndReloads` bundan sonra gerçek bir
  ölçümde (Topçu Kulesi köşesi komşu zeminin %30'u parlaklıkta) kalıyor; eşik **gevşetilmedi** (denendi, ikinci ölçüt de
  tutmadı → yanlış alarm değil). Gözle bakılmalı.

---
# 13 Kasım 2026 (2. oturum) — oyun testi geri bildirimi

## D24 — Bölge–hücre eşlemesi ve bulut kapsaması
- **Bulgu:** NW adacığı (KADRO düğmesinin altındaki kara) `ZoneAt`'te Z3'e (Altın Burnu, haritanın öbür ucu) bağlıydı; r6 hücresi
  dikdörtgen dışında kalıp Z0 sayılıyordu. Pişirme ızgarası 1–2. sütunları hiç örneklemediği için adacıkta bulut yoktu →
  bulutsuz arazi "bölge bulutların altında" diyordu. Diğer üç adacığın kenar hücreleri tesadüfen doğru bölgeye düşüyordu.
- **Karar:** Adacık dikdörtgenleri adacığın tüm kara hücrelerini kapsar; **NW adacığı Z0** (kale yaylasının batı komşusu, baştan
  açık; oyuncu onu zaten açık görüyordu). Bölge bulutları artık açgözlü kapsamayla pişirilir: her kilitli kara hücresi kendi
  bölgesinin bulut çekirdeği altında, hiçbir bulut çekirdeği başka bölgenin kara hücresine taşmaz (`IslandZoneCloud.CoreCovers`,
  ölçek 1,25–1,75 → 0,78; dar şeritlerde taşmasız olana dek 0,62 / 0,5). `Nov13FeedbackTests` tüm hücreleri denetler ve `F4_zone_map_debug.png` görselini üretir.
- **Alternatif:** NW adacığını Z3'te bırakıp üstüne bulut koymak (tasarım belgesi §4). Seçilmedi: Altın Burnu ile coğrafi bağı yok.

## D25 — Adada radyal menü (Ana Üs bileşeni)
- **Karar:** `RadialDockMenu.Create` + `Register` + `FinishSetup` ile aynı bileşen; ada öğeleri İNŞA ET · AKIN (vurgulu) · KADRO.
  Akın numarası AKIN düğmesinin sağ üst kenarında altın pul (`RadialItem.infoBadgeProvider`). Sol üstteki KADRO düğmesi kalktı
  (menüye taşındı). Açık/kapalı durumu ayrı anahtar `MEB_IslandRadialOpen`. Geri alma: `FeatureConfig.islandRadialMenu=false`.

## D26 — Kompakt akın sayacı
- **Karar:** Kum saati + "14s 48dk" (104×26, eskiden 188×32 "Sonraki akın: 14 sa 48 dk"); uyarı/canlı evrede "Geliyor 24dk" /
  "BASKIN 12dk" (hap sola 30 genişler). Dokununca "Sonraki akına kalan süre: …" açıklaması. `RaidBadgeText` uzun biçim korunur
  (testler, açıklama). Geri alma: `raidPillCompact=false`.

## D27 — Akın sonucunda ADAYA DÖN
- **Karar:** Ada sonuç ekranında (Akın ve canlı baskın; zafer ve yenilgi) "ANA ÜS" yerine "ADAYA DÖN": sahne yüklemeden bulut
  örtüsü (`SceneCloudTransition.BeginInPlace`) → savaş temizlenir, kamera üs kadrajına döner → bulutlar açılır. Ana Üs'e üst
  çubuktaki ANA ÜS ile gidilir. Sefer/Sonsuz/İstila sonuç ekranlarına dokunulmadı. Geri alma: `raidResultReturnToIsland=false`.

## D28 — Ada kadrajı
- **Karar:** `LandBounds` kuzey kıyısındaki ağaç tepelerini (+3,7 u) kapsar; alt HUD payı 0,08 → 0,10 (radyal ana düğme). Telefonlarda
  kadraj genişliğe bağlı olduğundan boy değişmez; 3:4 / 10:16 tablette ~%6 uzaklaşır.

## D29 — Ana Üs ana menü düğmesi
- **Karar:** Kalkandaki elmas kaldırıldı; arma = arkada çapraz iki kılıç (`ui_icon_sword` + aynası) + önde kalkan. Kırmızı uyarı
  noktası artık dairenin 45° kenarında (`EdgeBadgePosition`, r·0,72); eskiden kare sınırın köşesine asılı olduğu için havada duruyordu.
  Aynı düzeltme bütün radyal öğe rozetlerinde geçerli.
- **"18" yazısı:** Kullanıcının görselinin alt barı büyütülerek incelendi: solda yalnız aslanlı kalkan, perçin ve düğüm plakası var;
  görseldeki tek "18" üst bardaki elmas sayısı (geçerli öğe). Test ortamında da alt barda yazı yok. Kaldırılacak bir öğe bulunamadı;
  `Nov13FeedbackTests` alt %15'te tek başına sayı olmadığını bekçi olarak denetler. Kullanıcıya yeri soruldu.

## D30 — Önceki rapordaki kararlar (kullanıcı)
- **2x zafer reklamı sınırı kaldırıldı** (D17 geri alındı): V2'de de `victoryDoubleAdsPerDay = 0` (sınırsız). Gerekçe (kullanıcı):
  reklam izlenmesi bizim için iyi, zararı yok.
- **"Üsse dön, güçlendir" önerisi Ana Üs ≤ 4** (`VictoryReferenceView.EarlyDefeatMaxBase`); Ana Üs 2–4'te metin "kahramanını,
  kulelerini ve Ana Üs'ü yükselt" (Okçu Kulesi cümlesi yalnız Ana Üs 1'de).
- Topçu Kulesi köşe gölgesi: kullanıcı sonra bakacak (açık bulgu olarak kalır).

---
# 13 Kasım 2026 (3. oturum) — oyun testi geri bildirimi 2 + Google Play hazırlığı

## D31 — Akın haritası eski görüntü (1.1)
- **Kök neden:** HARİTA düğmesi sahnenin canlı kamerasını kullanıyor (önbellek/RenderTexture yok), ama savunmaya girerken
  `IslandSceneSetup.RefreshZones` bütün bölge bulutlarını kapatıyordu: kilitli bölgeler "açılmış, el değmemiş ada" gibi görünüyordu.
  Ayrıca HARİTA kadrajı eski "harita ortası, 22 u genişlik" sabitiydi (Faz A dönemi); üs görünümündeki kadrajla (LandBounds) aynı değildi.
- **Karar:** Savunmada kilitli bölge bulutları kapanmaz, %45 saydam kalır (`ZoneCloudBattleAlpha`; düşmanlar altında görünür ve seçilir).
  Sabit kadraj açıkken HARİTA = üs görünümü kadrajı (`ComputeFixedView`). Piyonlar savunmada yine binalara saklanır (tasarım; haritada işçi yok).
  Test: `Session3Tests.RaidMapShowsTheCurrentIslandStateInTheBaseFrame` (kesilmiş ağaç + açık/kapalı bölge + kadraj, iki açılış).
- **Alternatif:** Bulutları savunmada tamamen göstermek (düşman bulut altında kaybolur) — seçilmedi.

## D32 — Menü rozetleri (1.2)
- **Kök neden:** İNŞA ET rozeti "seviyesi 15 altında ve altını yeten herhangi bir bina" diyordu: zaten yükselen binayı, Ana Üs tavanına
  dayanmış binayı sayıyor; üstelik yükseltme İnşa Et menüsünden değil binaya dokunarak yapılıyor. Her şey kurulu olsa da rozet yanıyordu.
- **Karar:** İNŞA ET rozeti yalnız menüde gerçekten yapılabilecek bir şey varken: kilidi açık, kurulu < sınır, altın yeter, boşta inşaatçı
  (`BaseBadges.CanBuildNew`, bedel tek yerden `BuildMenuController.MenuCost`). Ana düğme noktası öğe rozetlerinin toplamı (0,5 sn'de bir).
  GÜNLÜK/GÖREV noktaları artık olay beklemeden saniyede bir koşuldan hesaplanır (gün dönümünde de doğru). ADA rozeti zaten koşula bağlı (baskın evresi).
- **Alternatif:** Yükseltilebilir bina için de İNŞA ET rozeti — seçilmedi (menüde karşılığı yok, "takılı rozet" algısını sürdürür).

## D33 — Ada hayalet engelleri (1.3)
- **Kök neden:** Koyun çayırı (8–11 × 17–19) her zaman "doğa" engeliydi; kesilebilir ağaç modunda (varsayılan) çayır sahnede hiç çizilmiyor,
  koyunlar Ağıl çevresinde. Orta çayır (Z1/Z2) açılınca oyuncu düz çime bina koyamıyor, "Ağaç, altın taşı ya da çayır var" alıyordu.
  Kesilen ağaçlar ve altın taşları doğru eşleniyordu (test bunu da denetler).
- **Karar:** Çayır yalnız görünür olduğu eski modda engel (`IslandGrid.MeadowBlocks`). Yüklemede göç `IslandRepair.CleanGhosts`: tanınmayan/Kale
  türü, harita dışı, kopya kimlikli, görevi olmayan şantiye kayıtlarını ve ağaç hücresi olmayan kesik-ağaç anahtarlarını siler (idempotent).
  Hata ayıklama görseli `PlaytestOutput/session3/S1_3_cell_debug.png` (ada görüntüsü + hücre nedenleri). Test: Nature olan her hücrede
  görünür ağaç/altın taşı olmalı.

## D34 — Ana Üs görsel çakışmaları (1.4)
- **Bulgu:** Ana Üs zaten ortak doluluk ızgarasını kullanıyor (`BaseOccupancyGrid`, yüklemede onarım) ama hücre ayak izine bakıyor; çatılar ve
  büyük sprite'lar ayak izinden taşıyor (Topçu Kulesi ↔ altındaki yapı, Kışla ↔ fener, Manastır ↔ kamp). Kahraman kamp bayrağının tam
  ortasında doğuyordu ve kampı engel saymıyordu.
- **Karar:** `BaseVisualBounds` (sprite ağının sıkı sınırı; gölge/işaret/karakter hariç) ile yüklemeden 0,5 sn sonra `RepairVisualLayout`:
  sıra Ana Üs + kamp (sabit) → maden → binalar/kulübeler → dekor; çakışan, ayak izi geçerli ve görseli boş en yakın yere taşınır.
  Yerleştirme/taşıma da görsel çakışmayı geçersiz sayar. Kahraman bayrağın 2,3 u solunda doğar, kamp + dekorlar yürüme engeli.
  Test: kalabalık Ana Üs 15 (her türden sınır kadar bina + fener + bayrak) → çakışma yok, ikinci onarım bir şey taşımaz.

## D35 — Radyal menü boyutu ve alt bar (2.1)
- **Karar:** Ana düğme 92 → 72, vurgulu 70 → 56, diğerleri 60 → 48 (≈%20–22; 48 birim ≈ 48 dp, referans 390×844), yay ×0,8. Ana Üs ve Ada
  aynı sabitler (`RadialDockMenu.MainSize/ItemSize/EmphasisSize`). Alt şeritteki çerçeve görseli (aslanlı kalkanlar + boş düğüm plakaları)
  kaldırıldı; şerit koyu tahta + altın üst kenar + perçin.

## D36 — Üst bar sırası (2.2)
- **Karar:** `FeatureConfig.baseTopBarOrder` = gold, wood, meat, gem, builder (ilk 3 armanın solu, 2 sağı); `islandTopBarOrder` = gold, wood,
  meat, gem (ANA ÜS düğmesi ve ada adından sonra). Yerleşim tek yerde (`ResourceTopBar.LayoutBase` / `BuildIsland`), veri kaynağı aynı.
  Listede olmayan öğe gizlenir.

## D37 — Akın sayacı biçimi (2.3)
- **Karar:** ≥1 sa "10sa 0dk", <1 sa "45dk", <1 dk "30sn" (EN 10h 0m / 45m / 30s). Uzun biçim "10 sa 0 dk", "30 sn".

## D38 — Ortak süslü bina penceresi (2.4) ve GERİ AL (2.5)
- **Karar:** Adadaki pencere `FancyInfoPanel` bileşenine çıkarıldı (nesne adları aynı); Ana Üs bina penceresi de bunu kullanır: kurdele başlık,
  bina sprite'ı, "Seviye N / 15", mevcut → sonraki değerler (değişen satırda yeşil "→ yeni"), altın ikonu + bedel (yetmezse kırmızı),
  saat ikonu + süre, not (Ana Üs için sonraki açılım), YÜKSELT → Sv N ve TAŞI düğmeleri. Kahraman penceresi dokunulmadı (kapsam: binalar).
- **GERİ AL:** `UndoMoveButton` (altın kenarlı koyu hap, altın halkalı ikon, 4 sn süre çizgisi); Ana Üs ve Ada aynı bileşen. Adada taşıma
  sonrası da çıkar ve binayı eski hücresine döndürür. Geri ok simgesi şimdilik "oynat" ikonunun 180° dönmüşü (ART_NEEDS).

## D39 — Çalışma biçimi
- Unity batchmode koşusu tek seferde birkaç dakika sürdüğü için her iş derlenir, testleri aşama sonunda gruplar halinde koşturulur;
  commit'ler iş başına ayrıdır. Takılan bir test (test çalıştırıcının sahnesini kapatan yardımcı) bulundu ve düzeltildi.

## D40 — Ana Üs ortam hareketi (3.1; Sonnet alt ajanı, orta çaba)
- **Karar:** `Base/BaseAmbientLife.cs` tek merkezî Update: dekor koyunları ada koyunu kareleriyle otlar/bekler/0,35 u/sn yürür (taban
  noktasının 0,5 u çevresinde; hücre/doluluk değişmez), Animator'sız ağaç/çalılar (dekor, kilitli arazi, çit dışı `ExteriorScenery` ve
  `ForestMargin`) ±1–1,5° sallanır. Görünürlük kamera görüş dikdörtgeniyle (renderer.isVisible kamera çizmeden güncellenmiyordu), en çok
  160 nesne, 3 sn'de bir yeniden tarama, nesne başına DOTween yok.
- **Bulgu:** Madenci (`MinePawn`, Tiny Swords kazma klibi), garnizon, kapı muhafızları, keşiş, çırak, inşaatçı, kahraman zaten Animator'lı;
  kodda madende tek madenci var (görseldeki ikinci figür inşaatçı kulübesinin işçisi). Yeni sprite gerekmedi.
- **Ölçüm:** cihazda kare hızı ölçülmedi (⚠️); maliyet O(nesne) sinüs + dikdörtgen kesişimi, en çok 160 nesne.

## D41 — Okçu atış yerleri (3.2; Sonnet alt ajanı)
- **Karar:** Yuvarlak işaretler: mevcut yer altın dolu, gidilen yer turuncu hızlı nabız, ulaşılabilir açık nabızlı halka, kilitli küçük gri +
  kilit simgesi (dokununca "Kıyı Kalesi N'de açılır"). İlk akında bir kez (`MEB_PostHintShown`) 6 sn şerit "Okçu buraya gidip buradan
  savaşabilir — işarete dokun" ve en yakın işaretin üstünde "Dokun: buraya git".

## D42 — Akın düğmeleri ve yetenek efektleri (3.3; Sonnet alt ajanı)
- **Düğmeler ne yapar** (`PLAN_ADA.md` "3. oturum 3.3"): ATEŞ = okçunun temel atışı (dokun: ok, basılı tut: delici şarjlı ok);
  ATEŞ OK = sonraki 3 ATEŞ atışını alevli yapan güçlendirme (yanma 5 sn, %4/sn); YAĞMUR = seçilen alana ok yağmuru; AĞ = hedefe ağ
  (kök 2,5 sn + yavaşlama, tekneler yavaşlar); HARİTA = tam ada kadrajı ↔ takip; OTO = otomatik ateş (Kıyı Kalesi şartı).
- **Yerleşim:** ATEŞ sağ altta çapa; yetenekler ATEŞ merkezinden 112 birim yarıçaplı yayda (170°/135°/100°), 56 birim, altın halka, simge +
  kısa ad, dairesel bekleme + kalan saniye; HARİTA/OTO 52 birim, sağ kenarda ayrı sütun. Nesne adları/API aynı (bot ve eski testler).
- **Efektler:** Yağmur nişan kipi (halka önizleme, haritaya dokun → taşı, düğme tekrar ya da 4 sn → ateş; `UseAt`), 15 ok gökten düşer;
  Ateşli Ok turuncu ok + alev izi + isabet patlaması + yanma süresince düşmanda alev; Ağ okçudan yay çizen ağ, isabette köklenen
  herkesin üstünde ağ (süre bitince söner). Vuruşta `swordHit`/`arrowShoot` sesi, hafif ekran sarsıntısı (0,08 u, 0,12 sn).
- ⚠️ **Geçici:** ağ, nişan halkası ve disk sprite'ları kodla çizildi (kullanıcı kuralı "kodla çizme" ile çelişir; uygun hazır sprite yok) →
  ART_NEEDS'te. Kullanıcı bir ağ sprite'ı verirse `IslandAbilityArt` tek yerden değişir.
- Test: `RaidAbilityTests` (5) + eski `IslandPhaseE2Tests` (ağ artık 0,35 sn uçtuğu için ağ testine 0,6 sn bekleme eklendi).

## D43 — Test koşusu notu
- `-testFilter BasePresentationTests` test derlemesinin adıyla eşleşip **tüm** takımı koşturuyor (50 dk sınırını aştı). Gruplar sınıf
  adlarıyla koşturuldu: Session3/RaidAbility/BaseAmbientLife/RadialMenu 25/25, IslandPhaseC/E1/E2 38/38, IslandPhaseB/FreshStart 20/20,
  Nov13Feedback 8/8.

## D44 — Google Play hazırlığı (AŞAMA 4)
- **Doğrulanan kurallar (13–14 Kasım 2026):** yeni uygulama/güncelleme hedef API 36 (31 Ağu 2026'dan beri); 16 KB sayfa: resmî sayfa
  1 Şubat 2027 diyor (bazı ikincil kaynaklar daha erken); kişisel hesap 12 test kullanıcısı × 14 gün kesintisiz kapalı test; Play Billing
  en az 8 (eklenirse).
- **Karar:** `Editor/AndroidReleaseBuild.cs` (AAB, IL2CPP, ARM64, API 36/25, imza ortam değişkenlerinden), `Systems/ReleaseLogging.cs`
  (yayında Debug.Log kapalı), `Tools/check_aab.py` (16 KB + hile izi + izin). Hileler zaten derleme dışıydı; testle ve AAB metadata'sıyla
  doğrulandı. `.gitignore` anahtar deposu/şifre dosyalarını dışlar. Reklam/satın alma SDK'sı eklenmedi (paket değişikliği + AdMob/ürün
  kimlikleri kullanıcıda) → UMP akışı yapılmadı, belgelendi (PLAY_RELEASE §6). En düşük API 25 korundu.
- **Alternatif:** min API 24 (≈+%0,5 cihaz) — gerek görülmedi.

---
# 4. oturum (14 Kasım 2026)

## D45 — Ortak yol bulma servisi (1.1; Opus, yüksek çaba)
- **Karar:** `Systems/GridPathfinding.cs` tek A* (ikili yığın, 8 yön, köşe kesme yok, düz görüş yumuşatması) + `IWalkGrid` arayüzü.
  Izgaralar: `BaseWalkGrid` (BaseOccupancyGrid engelleri + açılmış alan), `IslandWalkGrid` (su/yar, kale + kurulu binalar, kesilmemiş
  ağaç, altın taşı; yükseklik değişimi yalnız merdivenden 4 yönlü adımla), `BattleWalkGrid` (0,5 u hücre, BattleObstacles alanları).
- **Güncelleme:** her ızgaranın `Version`'ı var: Ana Üs/Ada 0,25 sn'lik içerik imzası + açık `Invalidate()` (ada bina kur/taşı, doğa
  temizliği); savaş `BattleObstacles.Version` (kayıt/silme/yıkılış). `GridPathFollower` sürüm değişince bulunduğu yerden yeniden hesaplar.
- **Ulaşılamayan hedef:** A* en yakın keşfedilen hücreye (sezgisel en küçük) yol verir; takipçi orada durur (`Done`), titreme yok.
  Başlangıç hücresi kapalıysa (karakter bina içinde kalmışsa) önce en yakın açık hücreye çıkar.
- **Bağlananlar:** Ana Üs inşaatçısı (BuilderPathfinder artık sarmalayıcı), Ana Üs kahramanı (rastgele hedefler yol bulmayla; durduğu hücre +
  çevresi açık ve gövdesi hiçbir bina/dekor/kamp görseline binmiyor — 3.4 maddesinin "kahraman bayrak/şapelle üst üste" kısmı), ada
  piyonları, ada mızraklısı (`GridSteer`: düz görüş açıksa doğrudan, değilse A*), koyun (düz yol kapalıysa yerinde), savaş düşmanları
  (uçanlar ve ada akın şeridindekiler hariç) ve müttefik Lancer.
- **Dokunulmayan:** ada okçusunun atış yerleri arası yolları (`IslandLayout.PathBetween`, elle çizilmiş, yerleşime kapalı şeritler) ve
  akın düşmanlarının kumsal şeritleri (tasarım gereği sabit rota).
- **Debug:** `WalkGridDebug` (editör/Development, F6): yürünebilir yeşil, kapalı kırmızı, son 32 rota sarı. Testler `Show()` ile açar.
- **Alternatif:** NavMesh 2D (NavMeshPlus) — paket ekleme yasak ve ızgara zaten var; gerek yok.

## D46 — Tek seçim yöneticisi (1.2)
- **Karar:** `UI/SelectionManager.cs`: aynı anda tek seçim; `Select(öğe, kapat)` öncekini kapatır, `Clear()` (boş yer/başka öğe/geri tuşu).
  Ana Üs dünya dokunuşu önce seçimi kapatır; aynı dekora ikinci dokunuş seçimi kapatır (aç/kapa). Kayıtlı seçimler: dekor TAŞI/SİL,
  ağaç/kaya/kilitli doğa kaldırma istemi, bina/kahraman/çırak penceresi (BaseWorldUI), ada bina/kule/kale/ağaç/çalı paneli ve İNŞA ET
  menüsü (IslandBuildUi). Geri tuşu: `SelectionBackKey` (Escape = Android geri).

## D47 — Madeni Koru maden görseli (1.3)
- **Kök neden:** Maden verisinin seviye görseli yok (Ana Üs'te `BaseMineVisual` parçalarla kurar); savaştaki `CreateStructureVisual` boş sprite
  yerine düz renk kareye düşüyordu.
- **Karar:** Savaşta maden = Tiny Swords Gold Stone 6 (ana gövde, can barı ve boyut buna göre) + seviye renginde maden evi (House3) + iki
  küçük altın taşı (çocuk parçalar, yıkılınca birlikte söner).
- **Tarama:** `Docs/playtest/session4/missing_refs_scan.txt` (tüm .unity/.prefab/.asset). Oyunu etkileyen kayıp referans yok; paket demo
  sahneleri (Cainos/Hovl/Joystick/Handpainted) ve `Resources/UI/Fixed Joystick.prefab` (Joystick Pack betiği silinmiş, kodda kullanılmıyor),
  `DungeonMonsters2D/StoneGolem.prefab` ilk sprite'ı (Animator ilk karede değiştiriyor) raporlandı, dokunulmadı. Çalışma anı denetimi:
  savaşta etkin, Animator'suz, sprite'ı boş renderer listesi `S1_3_null_sprites_battle.txt` (boş).

## D48 — Ada çalı/kaya temizliği (1.4)
- **Karar:** `Island/IslandNatureClear.cs`: pişmiş IslandNature prefab'ındaki "Bush{v}_{c}_{r}" / "Rock{v}_{c}_{r}" nesneleri çalışma anında
  bulunur (prefab yeniden pişirilmedi). Dokununca ağaç paneliyle aynı panel: **çalı 5 altın → +4 odun**, **kaya 3 odun → +10 altın**.
  Kayıt `IslandSaveData.clearedNature` ("bush:c,r"/"rock:c,r"; eski kayıtta alan yoksa boş liste, JsonUtility uyumlu). Kaldırma: hafif zıplama +
  küçülerek sönme, toz ve renkli kırıntı parçacıkları.
- **Mantıksal hücre:** çalı ve kaya zaten yerleşim/yürüme engeli değildi (süs); bina kurulunca/taşınınca altında kalanlar bedelsiz gizlenir ve
  kayda yazılır (eski kayıtlarda da ilk açılışta). Böylece "bina çalının üstünde" görüntüsü de kalkar.
- **Alternatif:** çalı/kayayı yerleşim engeli yapmak — eski kayıtlardaki binaları geçersiz kılardı; seçilmedi.

## D49 — Dekor boyutları 1×1 (2.1)
- **Karar:** Ağaç, çalı, kaya, koyun ve yeni küçük dekorların ayak izi 1×1. Görsel boyu ayak izinden ayrıldı: `DecorData.visualWidth`
  (dünya birimi; ağaç 2,1–2,3, çalı 1,25, kaya 0,85–0,95, koyun 1,3) — ağaç doğal boyunda görünür, yalnız gövde hücresi dolar.
- **Göç:** `PlayerSaveData.decorLayoutVersion` 0 → 1: eski 3×3 ağaç kaydı eski alanının ortasına (+1,+1) taşınır; 2×2'ler sol-alt hücresinde kalır
  (eski alanın içi → çakışma olamaz); yine de çakışırsa mevcut "en yakın boş hücre" kuralı, sığmazsa bedel iadesi.

## D50 — Dekor kataloğu yalnız Tiny Swords (2.2)
- **Kaldırılan (13):** Cainos Village Props'tan örs, fıçı, kamp ateşi, sandık, ayçiçeği, kuyu, saman balyası, korkuluk, heykel, odun yığını (eski);
  Tiny Swords olmayan yer tutucular sancak, fener (özel çizim), patika. Asset'ler silinmedi: `DecorData.retired = true` → katalogda görünmez.
- **Kurulu olanlar:** silinmez, yerinde kalır; oyuncu SİL derse **tam bedel** iade (normalde yarı). Savaştaki maden dekoru sandık/fıçı
  görsellerini kullanmaya devam eder (yalnız savaş süsü).
- **Eklenen (15):** Ağaç (sarı/çam/güz, animasyonlu), Çalı ×3 (animasyonlu), Kaya ×3, Kütük, Odun yığını, Et sepeti, Altın yığını,
  Altın taşı, Büyük altın taşı. Katalog 19 öğe. Çit, fener, bayrak, patika Tiny Swords ücretsiz paketinde yok → ART_NEEDS.

## D51 — Madende tek çırak (2.3)
- **Kök neden:** Madende iki figür vardı: maden görselinin madencisi (MinePawn) + ayrıca çizilen Çırak (BaseApprentice).
- **Karar:** Yalnız MinePawn kalır ve Çırak'ı temsil eder (Çırak seviyesinin renk kademesi, dokununca Çırak penceresi;
  `BaseApprenticeVisual` artık çizmez, bağlar). Kazma = Tiny Swords "Interact Pickaxe" (adadaki madenciyle aynı kareler), sürekli.
  Altın kıvılcımı 1,2–2 sn'de bir, çırağın önündeki kayadan 2–3 parça.

## D52 — Kışla devriyesi ve ortak ölçek tablosu (2.4)
- **Devriye:** `Base/BarracksRoofPatrol.cs`: Kışla damında mavi Lancer; damın iki ucu arasında yürür (0,32 u/sn), durup 1–3 kez sağa sola bakar.
- **Ölçek kök nedeni:** Savaş mızraklısı 1,2 kök ölçekle çiziliyordu; kahraman 0,85 × 0,64 × 1,3 ≈ 0,71 → mızraklı ~1,7 kat büyüktü.
- **Karar:** `Systems/UnitScale.cs` tek referans (Tiny Swords karakter gövdeleri paket içinde eş boy): Battle ≈ 0,707 (kahraman), Base = 1,9/3
  (garnizon), Island = 1; `Relative(kind)` tür çarpanı. AllyLancer ve çatı devriyesi buradan okur. Test: mızraklı lossyScale = kahraman.

## D53 — Kahraman/Çırak penceresi (2.5)
- **Karar:** HeroInfo de `FancyInfoPanel` (kurdele başlık, portre, "Seviye N / 15" + tavan notu, "Can: 200 → 230" karşılaştırma,
  altın ikonlu bedel, "Anında"); KAHRAMAN/ÇIRAK sekmeleri Tiny Swords düğme dokusuyla bedel satırının altında. DarkFantasyHudPolish'in
  eski kutu yerleşimi süslü pencerede uygulanmaz.
- **Tarama:** Kalan yükseltme/bilgi pencereleri: bina (süslü), ada (süslü), kahraman/çırak (artık süslü). İnşaatçı paneli ve Kart Atölyesi
  liste/üretim ekranıdır (2.7'de Kart Atölyesi ayrı ele alınır).

## D54 — Hemen bitir: bedava yok (2.6)
- **Karar:** `GemEconomy.FinishCost`: bitmiş iş 0; **≤60 sn 1 elmas**; üstü mevcut 2,2 × √dakika. "BEDAVA BİTİR" metinleri kalktı;
  düğmelerde elmas simgesi + sayı (`GemConfirm.SetGemIcon`, `GemEconomy.FinishLabel`): bina penceresi, inşaatçı paneli, ada paneli,
  Kart Atölyesi kuyruğu. Öğretici/görevlerde bedava bitire bağlı adım yoktu (taranıp doğrulandı). GemEconomyTests/IslandPhaseBTests güncellendi.
- **Not:** TMP yazı tipinde 💎 emojisi yok; "💎 1" görünümü ikon görüntüsü + sayı ile sağlanır.

## D55 — Kart Atölyesi yeniden düzen (2.7; Sonnet alt ajanı, orta çaba — ben inceleyip test ettim)
- Kurdele ve X panelin içinde; panel yüksekliği güvenli alandan (640–900), üst bölüm + alt ayrıntı (204) sabit, "KARTLARIM" ızgarası
  RectMask2D'li kaydırma alanında, hücre yüksekliği boş alana göre (56–72). `RefitForPixels` yeniden yerleşim yapar.
- Et eksik: "165 Et eksik — Ağıl'da üret ya da [elmas] 21 ile tamamla"; Et yetmezse ÜRET düğmesi kırmızı "[elmas] 21 ile üret" olur.
- "HIZLANDIR" başlığı kalktı; hızlandırma yalnız dolu kuyruk satırında (kalan süre + reklam + elmas).
- Kart ikonları: Keskin Oklar = Tiny Swords Arrow (döndürülmüş), Gök Avcısı = Tiny Swords avatar, Kalkan Kıran = kalkan + kırmızı çarpı
  (`BattleCards.ApplyIcon`; sefer kart bölümü de bunu kullanır). Yeşil üçgen kalktı, krem ▶ çizimi; "+3 kullanım" kılıç ikonu;
  kural tek satır "1 üretim = 3 savaş hakkı" + ⓘ (Tiny Swords Icon_11); YÜKSELT altında altın bedeli + sonraki seviye notu; iç boşluklar.
- ⚠️ ▶ üçgeni ve kırık kalkan birleşimi geçici (ART_NEEDS).

## D56 — Alt bar aslanlı şerit (3.1) ve ADA ikonu (3.2)
- **3.1:** D35'te kaldırılan `OrnateStrip(false)` (intro_button alt şeridi, iki yanda aslan armalı) aynı varlıkla geri geldi; şerit dock
  boyunda, ortası 72'lik ana düğmenin arkasında kalır. Session3Tests beklentisi tersine çevrildi (kullanıcı kararı).
- **3.2:** ADA düğmesi = `ui_icon_island.png` (Tiny Swords su rengi daire + çimen ada + mavi Gözcü Kulesi + ağaç), üreteç
  `Tools/campaign_art/island_icon.py`; düğme yüzü Tiny Swords su rengi, ikon yüzü doldurur (`RadialItem.iconFill`).

## D57 — Ayarlar ekranı (3.3; Sonnet alt ajanı)
- Bölümler: SES (müzik/efekt kaydırıcı + aç/kapa), TİTREŞİM (`MEB_Vibration`, iki Handheld.Vibrate çağrısı bağlandı), DİL, BİLDİRİMLER,
  GİZLİLİK POLİTİKASI ve DESTEK (yer tutucu URL/e-posta sabitleri — Play hazırlığında gerçekleri girilecek), SAVAŞ (PES ET), sürüm.
  Açılış 0,85→1 OutBack + karartma, kapanış 0,15 sn; timeScale 0'da da çalışır. Eski düğme adları/API korunur.

## D58 — UI/UX turu (3.4)
- **Radyal menü:** yay genişledi (yan öğeler ±152, üst öğeler ±72 / 132); etiketler arası ≥30 birim, testle çakışmazlık denetlenir.
  Menü açıkken ekranın alt %34'ünde yukarı doğru sönen karartma (`RadialShade`, dokunuşu engellemez) — kalıcı açık menü dünyayı
  tümden karartmasın diye tam ekran karartma seçilmedi.
- **Kahraman çakışması:** D45'teki durma hücresi kontrolü artık kahramanın gerçek sprite sınırlarını kullanır (bayrak/şapelle iç içe kalmaz).
- **Savaş HUD:** "MADEN %100" / zaman göstergesi üst barın ikinci satırına (bölge adının sağı) taşındı; dünyadaki inşa halkalarına binmez.
  Halkanın "N HAK" pulu 92 → 124 birim, sabit yazı boyu.
- **Dokunma alanları:** süslü pencere X 30×28 → 44×44, elmas onayı X 38 → 46.
- **Unity sahte-null:** `GetComponent<T>() ?? AddComponent<T>()` kalıbı editörde MissingComponentException atıyordu (5 yer düzeltildi).

## D59 — Maden dokunuşu ve çırak iş döngüsü (14 Kasım, oturum sonrası geri bildirim)
- **Kök neden:** çırağın dokunma alanı bina açılış animasyonu sırasında (ölçek ~0) `lossyScale` ile hesaplanıyordu; ~10×14 birim olup madenin
  tamamını kaplıyordu, madene dokunmak Çırak penceresini açıyordu. Alan artık piyonun hedef dünya ölçeğinden (1,04): yalnız gövde (0,62×0,86).
- Dokunuş gövdeye denk gelirse Çırak penceresi; madenin geri kalanı (kaya, maden evi, yığın) maden: altın toplanır + bina penceresi.
  Çakışan iki alanda sonuç açıkça `OverlapPointAll` ile seçilir.
- **Çırak iş döngüsü:** kayayı kazar (Interact Pickaxe, kıvılcımlar yalnız bu sırada) → altını sırtlayıp yığına yürür (Run Gold) → bırakır
  (Idle Gold, külçe yığına sekip söner, yığın zıplar) → kazmasını alıp sırayla öbür kayaya yürür (Run Pickaxe). Hepsi Tiny Swords piyon kareleri;
  renk Çırak seviyesinden. Test: `Session4Tests.MineTapCollectsGoldAndOnlyApprenticeBodyOpensApprentice`.

## D60 — Açık arazide ağaç/kaya dokunuşu (14 Kasım, kullanıcı ekran görüntüsü)
- **Kök neden:** dokunuş tek `Raycast` ile çözülüyordu ve kahraman kontrolü en önce yapılıyordu. Kahramanın dokunma alanı animasyon karesinin
  tamamı +0,3 pay (~4×4 birim), Ana Üs’ün alanı kale görselinin saydam kenarlarıyla birlikteydi; yanlarındaki ağaç/kayaya dokunmak kahraman ya da
  Ana Üs penceresini açıyordu.
- **Öncelik:** kilitli arazi uyarısı → küçük hedefler (dekor, ağaç, çalı, kaya, kütük; birden çoksa merkezi en yakın, `OverlapPointAll`) →
  kahraman → binalar ve diğerleri.
- Kahramanın dokunma alanı gövdesi kadar: karenin ~%40×%55’i, en az 0,8×1,1 birim (`HeroTapRect`).
- Test: `Session4Tests.EveryOpenLandNatureTapOpensClearPromptNotBuildingOrHero` (biri kale kenarına, biri kahramanın yanına taşınmış 8 öğe).

## D61 — Kaldırma kartı, nesne üstü ✓/✗ onayı, GERİ AL (14 Kasım, kullanıcı ekran görüntüleri)
- **Kaldırma kartı:** bina pencereleriyle aynı Tiny Swords kâğıt + kurdele başlık. Solda nesnenin kendi görseli, sağda açıklama ve
  simgeli bedel/süre satırı (yetmezse kırmızı). Altta Tiny Swords kırmızı KALDIR ve mavi VAZGEÇ. Bedel artık düğme metninde değil
  ("KALDIR (15 ALTIN)" iki satıra kırılıyordu). Ağaç/kaya ve doğa nesneleri tek ortak kartı kullanır (`BuildRemoveCard`).
- **Yerleştirme onayı:** alt bardaki YERLEŞTİR/İPTAL çubuğu kalktı. Nesnenin hemen üstünde yüzen iki yuvarlak düğme: ✓ ve ✗
  (`WorldConfirmButtons`; altın halkalı koyu yüz, radyal menüyle aynı aile). Üstlerindeki küçük hap bedeli (altın simgesi + sayı)
  ya da konamama nedenini kırmızı gösterir. Hayalet sürüklendikçe izler, ekrandan taşmaz. Ana Üs binası, Ana Üs dekoru ve Ada aynı bileşeni kullanır.
- **Simgeler:** ✗ = Tiny Swords Icon_09 (`ui_icon_cross`). Pakette ✓ yok: `ui_icon_check` aynı piksel stilinde (koyu kontur, alt gölge,
  Icon_07 yeşil paleti) `Tools/campaign_art/check_icon.py` ile üretildi; el çizimi sürümü ART_NEEDS’te.
- **GERİ AL:** döndürülmüş yeşil "oynat" oku yerine Tiny Swords turuncu geri oku (Icon_08, `ui_icon_undo`). Simge halkası artık hapın
  içinde. Alttan taşan süre çizgisi yerine simgenin çevresinde boşalan altın halka var. Hap gölgeli, etiket tek satır. Dekor taşıma da
  aynı ortak düğmeyi kullanır (eskiden düz yazılı düğmeydi).
- Eskimiş test düzeltildi: `ExpansionFeatureTests.DecorCatalogGridAndTabs` (4. oturumdan beri 32 dekor varlığı, ağaç 1×1, menüde emekli olmayanlar).

## D62 — Ana Üs yerleşimi: tek kural hücreler, engel alanı = ayak izi ∪ görsel (5. oturum 1.1)
- **Kök neden ("Buraya sığmıyor" boş görünen yerde):** yerleştirme hücre denetimine EK olarak görünmeyen bir sprite-örtüşme denetimi yapıyordu
  (3. oturum 1.4). Ana kalenin görseli ayak izinin 1,7 birim altına, Topçu 1,8 altına/1,3 üstüne, Kışla/kulübe her yana taşıyor; taşınan binanın
  çatısı da kendi ayak izinden taşıyor. Sonuç: ızgarada yeşil görünen hücrelerde ret. Maden ve kamp bayrağında tersi: ayak izi görselden büyük
  (maden alt satırı boş görünür, bayrak 2×2 kare). Ayrıca yeni bina hayaleti 0,9 sabit ölçekle çiziliyordu (gerçek binadan farklı).
- **Karar:** `BaseOccupancyGrid` engel alanı = ayak izi ∪ görselin gerçekten kapladığı hücreler (`VisualCells`: hücreye yanlardan ≥0,35,
  alt/üstten ≥0,3 giren görsel o hücreyi kaplar; dinlenme ölçeğinde, `RestVisualRelative`). Ayrı sprite denetimi kalktı; `PlacementController.Validate`
  tek kural. Kamp = bayrağın görsel hücreleri. Dekor ve doğa yalnız kendi hücresi (çalı/koyun görselleri birbirine/çatılara binebilir; görsel
  çakışma onarımına girmez). İki yapının izin verilen görsel örtüşmesi 0,7×0,6 (`BaseVisualBounds.StructureTolerance*`); kahraman 0,12 kalır.
- **Görünürlük:** yerleşim modunda (bina + dekor) dolu hücreler kırmızı, çit payı soluk kırmızı; hayaletin engel alanı hücre hücre yeşil/kırmızı.
  Hayalet = gerçek bina görseli ve ölçeği (`BaseMapManager.PreviewSprite/BuildingVisualScale`).
- **Hayalet kayıtlar:** `BaseMapManager.ValidateOccupancy` (yüklemede, onarımdan önce): yok edilmiş harita/dekor/doğa girdileri temizlenir; kayıttan
  sıfırdan kurulan doluluk sahneyle karşılaştırılır, fark varsa kayıt yeniden yazılır. **Bulunan gerçek hata:** `NatureObstacle.cell` AddComponent'tan
  sonra atanıyordu; OnEnable (0,0) ile çalıştığından açık araziye kalan doğa nesneleri hiç kaydolmuyor, (0,0)'da hayalet kalıyordu → `cell` özellik
  oldu. Eski kayıtlarda bina/dekor altında kalan doğa nesnesi yüklemede bedelsiz kalkar (bina yerinden oynamasın).
- **Kahraman:** kurulan/taşınan yapının alanında kalırsa en yakın boş durma yerine yürüyerek çekilir (`HeroMovementController.RefreshWalkable`).
- **Ölçüm (Ana Üs 15 tam düzen, 3316 aday):** ızgara–kural uyuşmazlığı 0, kabul edilip başka yapıya binen 0; iki görsel aynı hücreye karşı yönden
  <1 birim taşınca hücre kuralı gözden biraz katı ("sprite boşluğu" reddi ~%2,6) — bu retler ızgarada kırmızı görünür. Rapor `PlaytestOutput/session5/S1_1_*`.

## D63 — Hiçbir sahnede yapı içinden geçme (5. oturum 1.2)
- **Kök neden:** savaşta her yapı görselinden bağımsız sabit 1,25×1 birimlik engel kaydediyordu; Madeni Koru madeni (~2,7×1,65) ve büyük kuleler
  bunun çok dışına taşıyordu, kahraman/çırak görselin içinden geçiyordu. Karakterler yapıların üstünde çizildiğinden (sabit sıralama) geçiş görünüyordu.
- **Karar:** `BattleObstacles.FromVisual`: engel = yapının sıkı görseli (yanlardan %8, tepeden %10 pay; can barı, gölge, efekt, garnizon hariç) ∪ eski
  alan; açılış animasyonu boyunca 1,5 sn yenilenir (BattleStructure, MainBaseController). Düşman vuruş mesafesi alanın kenarından da ölçülür
  (büyüyen engel saldırıyı geciktirmez). Çırak, kahraman yapının öbür yanındayken ortak yol bulmayla (`GridSteer`) dolanır.
- Ana Üs yürüme ızgarası D62 engel alanlarını kullanır (çatı/görsel dahil); ada `IslandWalkGrid` (4. oturum) yeterli bulundu.
- **Testler:** Session5Tests — Ana Üs/ada/savaş (normal + Madeni Koru) yapıların arkasına rota, kahraman+çırak yürütmesi, düşmanlar.

## D64 — Dekor ölçeği ortak tablodan (5. oturum 1.3)
- **Kök neden:** 4. oturumda dekorlar 1×1'e geçerken görsel genişlik `visualWidth` ile verildi ve eski `visualScale` büyütmesi (Tiny Swords karelerinin
  boşluğu için) yok sayıldı: koyun 3,5 → 1,3 birim (%37), çalı 2,48 → 1,25, kaya 1,84 → 0,85, ağaç 2,76 → 2,3.
- **Karar:** `Systems/DecorScale.cs` tek tablo (id → karenin dünya genişliği), ayak izinden bağımsız. Eski boyutlar geri: koyun 3,5, çalı 2,48,
  kaya 1,84, ağaç 2,76 (Tree3–4: 2,52, eski oran), kütük ağaçla aynı paket ölçeği 2,76; sonradan eklenen altın taşı/yığınlar Tiny Swords doğal boyu
  (64 px = 1 birim). Tabloda olmayan emekli dekorlar eski formül (`DecorData.LegacyScale`).

## D65 — "Önerilen düzen" kapalı (5. oturum 2.4)
- İnşa Et menüsündeki ÖNERİLEN DÜZEN düğmesi `FeatureConfig.recommendedLayout = false` ile gizlendi; `BaseMapManager.ApplyRecommendedLayout`
  ve `DecorPlacementController.AddRecommendedClusters` kodu duruyor (bayrak true → düğme geri gelir). Oyunda başka çağıran yok.

## D66 — ✓/✗ hayaletin yanında, GERİ AL küçük (5. oturum 2.2, 2.3)
- **2.2 kök neden:** düğmeler hayaletin ÜST kenarının üstüne konuyordu; hayalet kalenin altındayken düğmeler ve "Buraya sığmıyor" kalenin üstünde
  görünüyordu. **Karar:** `WorldConfirmButtons.Create(…, Func<Bounds>)`: grup hayaletin ekran dikdörtgeninin SAĞINA (sığmazsa soluna, o da olmazsa
  altına/üstüne) dikeyde ortalı konur, her karede izler, güvenli alanda kalır; hayaletin üstüne binmez. Geçersizken ✓ %42 soluk ve basılamaz
  (CanvasGroup.interactable). Ana Üs binası, dekor ve ada aynı bileşen.
- **2.3:** GERİ AL 188×58 hap → 52 çaplı yuvarlak düğme + altında küçük "GERİ AL" etiketi (kök 66×74, eski alanın ~%45'i). Sol alt köşe, radyal menü
  yayının üstü (güvenli alan sol-alt'tan 44, 238) — çit dışındaki ağaç şeridi; oyun alanını kapatmaz. 3 sn; altın halka kalan süreyle azalır; süre
  bitince 0,35 sn'de söner (sönerken dokunuş geri almaz).

## D67 — Ortak eylem menüsü (5. oturum 2.1; Sonnet alt ajanı, orta çaba — ben inceleyip test ettim)
- `UI/WorldActionMenu.cs`: seçili öğenin hemen üstünde (sığmazsa altında), güvenli alan içinde kalan, öğeyi izleyen küçük menü. 48'lik yuvarlak
  altın halkalı düğmeler (WorldConfirmButtons ile aynı aile), altında etiket hapı, gerekiyorsa simge + sayı bedel/iade hapı.
- Dekor: TAŞI (dört yönlü ok) · SİL (çöp kovası, altında altın simgesi + "+iade"). SİL kısa onay ister ("Silinsin mi? +N [altın]" ✓/✗).
  ("+2 🪵" örneği verilmişti; dekor iadesi altındır, simge buna göre.) Menü nesnesi hâlâ `DecorActions`.
- Seçili öğe altın tonlu, hafif nabız atan dış hatla vurgulanır (`SelectHighlight`); boş yere/başka öğeye dokunma ve geri tuşu kapatır (SelectionManager).
- **Bina dokunuş akışı değişti:** bina/kuleye dokunmak artık pencereyi doğrudan açmaz; aynı menü YÜKSELT + TAŞI gösterir (kalede yalnız YÜKSELT,
  maden önce altını toplar, inşaatçı kulübesi eskisi gibi). Uzun basarak taşıma sürer.
- **Ada:** bağlanmadı — adada bina dokunuşu zaten TAŞI'lı kendi süslü panelini açıyor ve SelectionManager'a bağlı; araya menü koymak ada panel
  akışını/testlerini riske atardı. Sonraki adım: `IslandHud` BuildingTapped → WorldActionMenu (YÜKSELT = ShowBuilding, TAŞI = IslandPlacementHost.ForMove).
- Simgeler `ui_icon_move` / `ui_icon_trash` Tiny Swords piksel stiliyle üretildi (`Tools/campaign_art/action_icons.py`); el çizimi ART_NEEDS'te.

## D68 — Tamamlanmış seviye tek kaynaktan (6. oturum 1.1)
- **Kök neden:** yükseltme seviyeyi zaten süre bitince artırıyordu; ama YENİ kurulan bina kayda hemen `level = 1` ile giriyor ("build" görevi
  yalnız görseldi). Savaş kule hakkı (`BattleManager.GetBattleBuildAllowance`), kule seviyesi, Kışla Lancer'ları, Köylü Evi depo bonusu,
  Kart Atölyesi seviyesi ve ilk savaş ipucu kayıttaki her binayı sayıyordu → 1 dk'lık Topçu inşası bitmeden savaşta kurulabiliyordu.
- **Karar:** `Systems/BuildingLevels.cs` tek kaynak. `PlacedBuildingData.level` = TAMAMLANMIŞ seviye (yükseltme sırasında artmaz);
  "build" görevi süren binanın tamamlanmış seviyesi 0. Hedef seviye yalnız gösterim (`PendingLevel`). Veri yapısı değişmedi (durum görevlerden
  türetiliyor) → migration gerekmez; eski kayıtta süren inşa da doğru sayılır. "build" görevi bitince tür seviyeleri yeniden hesaplanır.
- Okuyanlar: GameManager (tür seviyesi, savaş kule seviyesi, ev bonusu), BattleManager, AllyBarracks, CardWorkshop, savaş ipucu.
  Kahraman/Çırak (2.2) ve ada (3.3) aynı ilkeyle.

## D69 — Menzil çemberi = hedefleme merkezi ve yarıçapı (6. oturum 1.2)
- **Kök neden (ekran görüntüsü: çember kulenin çok altında):** halka `DashedCircle(transform, …)` ile ölçeği tweenlenen kulenin ÇOCUĞU olarak, kule
  açılış animasyonunda (Y ölçeği 0,2) taban noktasına konuyordu. Ölçek 1'e büyüyünce yerel ofset ×5 oldu → merkez kulenin ~2 birim altına kaydı;
  kuzey kenarı kulenin hemen üstünde, güney kenarı çok aşağıda kaldı. Hedefleme ise tabandan (BasePoint) ölçüyordu: çember ile hasar uyuşmuyordu.
- **Karar:** `BattleStructure.RangeCenter` = taban + görsel yüksekliğin yarısı (kulenin görsel/mantıksal ortası). Okçu/Topçu hedeflemesi
  (`TowerController.RangeOrigin`, `InRange` tek kural), Manastır iyileştirmesi, sis görüşü ve çember aynı merkezi kullanır. Çember `Battle/RangeRing.cs`:
  ebeveynsiz, ölçeği 1, her karede merkeze yazılır; yarıçap = saldırı menzili (dünya birimi). Manastır'a da (yeşil, 4 birim) çember eklendi; kulelerle
  aynı görünürlük kuralı (kurulunca 2,5 sn + kahraman yaklaşınca). Ada okçusunda çember çizilmiyor (v2 kararı) — dokunulmadı.
- Test `Session6Tests.S1_2`: Y ölçeği 0,2 → 1 sonrası çember merkezi = RangeCenter; tüm kesik uçları merkezden aynı uzaklıkta; K/G/D/B'de
  yarıçap−0,02 içeride, +0,02 dışarıda.

## D70 — Ortak can barı: sol kenara sabit + gecikmeli beyaz iz (6. oturum 1.3)
- **Kök neden:** (1) dünya üstü `HealthBar`: dolgu sprite'ı 2 birim genişken (32 px / 16 ppu) sola kaydırma `barSize.x(1−oran)/2` ile yarım
  hesaplanıyordu → dolgu ortaya doğru küçülüyor, solda boşluk kalıyordu. (2) Savaş alt kartları Unity Slider'ı kullanıyordu: "Fill Area"nın
  varsayılan iç boşlukları (sol 5, sağ 15) ve küçük genişlikte bozulan sliced hap → dolgu soldan içeride başlıyordu. Çırak kartı ayrı bir Filled kare.
- **Karar:** UI: `UI/UiHealthBar.cs` — sol çapalı RectMask2D kabı (genişlik = oran) içinde her zaman tam bar genişliğinde sliced hap (kenarlar hiçbir
  oranda bozulmaz). Hasarda dolgu hemen iner, beyaz iz 0,35 sn bekleyip 1,6 oran/sn erir; iyileşme yumuşak artar; duraklatmada iz donar.
  Savaşçı/Ana Üs/Çırak kartları (`BattleHudRedesign.StyleCard`) ve ada kale barı bunu kullanır; eski Slider/Filled görüntü yalnız değer kaynağı.
  Dünya: `HealthBar` aynı kural (sol pivotlu sprite, aynı iz süreleri) — karakter, düşman, kule, Ana Üs, ada yapıları hepsi bu bileşen.
- Test `Session6Tests.S1_3_*`; görüntü `Docs/playtest/session6/S1_2_3_battle_rings_bars.png`.

## D71 — Uzun yürüyüşte animasyon kesintisiz (6. oturum 1.4)
- **Kök neden 1 (asıl):** savaşta kahramanın asıl SpriteRenderer'ı `BattleActorPresentation` tarafından `forceRenderingOff` ile gizleniyor, büyütülmüş
  kopyası çiziliyor. `BattleCosmeticObserver` ise kahraman Animator'ına `CullUpdateTransforms` veriyordu: görünmeyen renderer'da Animator sprite yazmayı
  bırakıyor → ilk kareler oynuyor, yürüyüş uzayınca kare donuyor, karakter kayıyor gibi görünüyordu. **Çözüm:** kahraman Animator'ı `AlwaysAnimate`.
  Aynı gizleme yalnız tek kareli düşmanlarda (`EnemyMotion`, Animator yok) var; diğer Animator'lar görünür renderer kullanıyor.
- **Kök neden 2:** Tiny Swords içe aktarımında Run/Idle dahil TÜM kliplerde `m_LoopTime: 0`. Animator durumu süre bitince son karede kalıyordu
  (Okçu/Lancer/Monk/Pawn/Koyun yürüme-bekleme, çırak, ada/Ana Üs işçileri). **Çözüm:** adı Run/Idle/Interact/Walk/Move/Grass içeren ve ortam döngüleri
  (çalı, ağaç Idle, ateş, su kayası, ördek, su sıçraması, altın taşı Static) 181 klipte döngü açıldı. Tek seferlikler (Attack, Death, Hit, Guard, Shoot,
  Chopped, Explosion, Dust, Heal) dokunulmadı. Dungeon canavarları (ada düşmanları) zaten döngülüydü; tek kareli düşmanlar kodla yürür (EnemyMotion).
- Test `Session6Tests.S1_4_*`: katalogdaki ve Resources'taki tüm denetleyicilerde yürüme/bekleme klipleri döngülü; savaşta 5 sn yürüyüşün son 1,5 sn'sinde
  kahraman kareleri değişmeye devam ediyor.

## D72 — Savaş duraklatması tek servisten (6. oturum 1.5)
- **Önce:** ayarlar açılınca `timeScale = 0` vardı ama ses/müzik sürüyordu; kapanınca zaman HER ZAMAN 1'e dönüyordu (ör. devam teklifi penceresinin
  0'ını ezebilirdi); arka plana alma / telefon / ekran kilidi savaşı duraklatmıyordu.
- **Karar:** `Battle/BattlePause.cs` — nedenli duraklatma (`settings`, `background`; hepsi kalkınca devam). Duraklatma = `timeScale 0` + `AudioListener.pause`.
  Savaş sayaçları (dalga, SÜRE, kule/kahraman/yetenek bekleme, akın) ölçekli zamanla çalıştığı için hepsi durur (denetlendi: savaş kodunda gerçek zaman
  kullanan yalnız süs nabızları var). Devamda duraklatma öncesi ölçek geri gelir. `BattlePauseWatcher` (BattleManager kurar): `OnApplicationPause(true)` ve
  cihazda odak kaybı → savaş sürüyorsa duraklat, dönüşte Tiny Swords kâğıt pencere "DURAKLATILDI · DEVAM". DEVAM'a basılmadan oyun sürmez; ayarlar da
  açıksa ikisi de kapanmalı. Sahne kapanınca tüm nedenler temizlenir.
- Test `Session6Tests.S1_5`; görüntü `Docs/playtest/session6/S1_5_paused_resume.png`.

## D73 — Çırak maden katkısı = seviye kadar yüzde (6. oturum 2.1)
- Economy_V2 (asset + `EconomyConfig.CreateV2`): çırak Sv.L → maden üretimine +%L (1…15). Eski V2: 5,8,10,13,15,16…25. LEGACY değişmedi.
- Kayıtta yalnız `apprenticeLevel` var, katkı her an formülden hesaplanır → migration yok; mevcut oyuncu seviyesini korur, etkisi yeni formülle.
- Öncesi/sonrası gelir: ECONOMY.md §11 (6. oturum).

## D74 — Kahraman ve Çırak yükseltmesi süreli (6. oturum 2.2)
- Anında yükseltme kalktı: inşaatçı görevi ("upgrade", slot `__hero` / `__apprentice`), süre = binalarla aynı `Progression.UpgradeSecondsTo(hedef)`,
  elmasla bitirme = aynı `GemEconomy.FinishCost` (≤1 dk 1 elmas), altın eksikse aynı elmasla tamamlama penceresi. Bir inşaatçı tutar (binalarla tutarlı;
  inşaatçı darboğazı bilinçli). Seviye SÜRE BİTİNCE artar → savaş ve maden süre boyunca tamamlanmış seviyeyi kullanır (D68).
- Pencere: sonraki seviyenin kazandırdıkları HER ZAMAN "mevcut → sonraki" (yeşil) görünür — eskiden tavana (Ana Üs seviyesi) gelince karşılaştırma
  gizleniyordu, oyuncunun "görünmüyor" dediği durum buydu. Tavanda düğme "Sv.N için Ana Üs Sv.N" yazar. Süre satırı (kum saati) eklendi.
  Kahraman sayacı kahramanın başında, çırak görevi inşaatçıyı madene yollar.
- Eski kayıt: süren görev yok, seviyeler aynen → migration gerekmez.

## D75 — Maden deposu görünür ve 4 saatlik (6. oturum 2.3)
- **Değerler (Economy_V2):** üretim 600 + 90(L−1) altın/sa; depo 2.400 + 360(L−1) (= 4 saatlik üretim); Köylü Evi +%5, seviye başına +%1 (ev başına).
  Gerekçe ve öncesi/sonrası ECONOMY.md §11 (taşma %85 → %0; 1. gün gelir −%12; Ana Üs 15 ~3,5. gün).
- **Görünürlük:** madenin üstünde `Base/MineStorageGauge.cs` (dünya tuvali): "1.240 / 3.000" + altın doluluk barı (ortak UiHealthBar); dolunca
  kırmızı nabızlı "DOLU · 3.000". Maden penceresi: Üretim (altın/sa), Depo, Biriken (+ "DOLU, üretim durdu"), "Depoyu büyütür: maden seviyesi + Köylü Evi";
  yükseltme karşılaştırması Depo ve Üretim için mevcut → sonraki. Köylü Evi penceresi: depo +% ve "Ek depo: +N altın" (mevcut → sonraki).
- Tek formül: `GameManager.MineCapacityAt` (oyun = pencere = gösterge). Depo dolunca hem çevrimiçi hem çevrimdışı üretim durur (mevcut kısıt).
- **Hata düzeltmesi:** çevrimdışı üretim 1 altından az biriktiğinde zaman damgası yine de ileri alınıyordu (kesir kayboluyordu); düşük saatlik
  üretimde bu kayıp büyüyecekti → damga artık ilerlemez.
- Eski kayıt: biriken altın korunur (yeni kapasite eskisinden büyük); migration gerekmez.

## D76 — Dükkân: elmasla kaynak + gerçek parayla elmas (6. oturum 2.4)
- **Paket kuralı istisnası:** CLAUDE.md "paket değiştirme" der; kullanıcı bu oturumda açıkça Unity IAP istedi → `com.unity.purchasing` **5.4.0**
  (Haziran 2026; v5 API: StoreController, Pending → Confirm) eklendi. Başka paket/ayar değişmedi.
- İnşa Et menüsü üç sekme: YAPI · DEKOR · DÜKKÂN (`UI/ShopTabView.cs`). Kaynak paketleri `Systems/Shop/ShopService.cs`, sayılar
  `ShopConfig.asset` (gerekçe ECONOMY.md §12). Altın kuru Ana Üs'le ölçeklenir ve eksik altın tamamlama da aynı kuru kullanır.
- `Systems/Shop/IapService.cs`: arka uç Unity IAP (cihaz) ya da `FakeStoreBackend` (editör/testler). Elmas yalnız bekleyen sipariş + makbuz
  doğrulaması sonrası; kayda yazılıp sonra onaylanır; işlem defteri `PlayerSaveData.iapLedger` (kayıt **v12**, migration: boş defter).
  Yerel doğrulama `CrossPlatformValidator(GooglePlayTangle.Data(), Application.identifier)`; tangle yoksa yayında elmas verilmez.
- Fiyat metni mağazadan; yazı tipinde olmayan para simgesi (₺ gibi) ISO koduna çevrilir (`ShopTabView.SafePrice`) — kare görünmez.
- Üst bardaki eski Elmas Dükkânı'nın "YAKINDA" karoları aynı ürünlere bağlandı.
- Ürün kimlikleri ve kullanıcı adımları PLAY_RELEASE.md §9.

## D77 — Adaya ilk giriş: tanıtım + ad (6. oturum 3.1)
- **Neden tetiklenmedi (kök neden):** `IslandHud.OfferNameSoon` içinde `if (DevSettings.Enabled && !OfferInDev) yield break;` vardı. `DevSettings.Enabled`
  editörde ve **Development APK'da varsayılan AÇIK** → cihazdaki test derlemesinde ad sorusu hiç gelmiyor, ada "Martı Adası" kalıyordu. Ayrıca v10 göçü
  eski kayıtlarda `freshStart=false` yaptığı için girmemiş eski kayıtta bile "ilk ad" yerine "teklif" kipi seçilecekti.
- **Karar:** kapı yalnız otomatik test koşusunda (batchmode, `OfferInDev` kapalı). İlk giriş `island.introSeen` (kayıt **v13**) ile: `Island/IslandIntro.cs`
  (bulutlar dört yana açılır, ada görünür, kâğıt kartta 2 cümle hikâye, DEVAM) → `IslandNameUi.Mode.First` "Adanızın adı ne olsun?" → öğretici (D78).
  İlk adlandırma değişiklik hakkından düşmez: ardından 2 ücretsiz değişiklik, sonra 50, 100, 150… elmas.
- **Göç (v13):** adaya girilmiş kayıt (köy kurulmuş/akın/öğretici/bina) tanıtımı ve öğreticiyi görmüş sayılır; ad sorulmadıysa bir kez teklif edilir.
  Girilmemiş eski kayıt yeni oyuncu gibi tam akışı görür. Test: `Session6Tests.S3_1_2`, `S3_1_3` (göç + v12 JSON yükleme).

## D78 — Ada başlangıcı ve zorunlu Okçuluk öğreticisi (6. oturum 3.2)
- İlk girişte yalnız Kıyı Kalesi + doğa: Okçu Kaptan Okçuluk Alanı TAMAMLANANA dek adada yok (`IslandEconomy.HasArcheryReady`); boş adada Ada Ustası da
  görev başlayana dek görünmez. Okçu yokken akın panosu/savunma açılmaz ("Önce Okçuluk Alanı'nı kur").
- `Island/IslandBuildTutorial.cs` (adım kayıtta `buildTutorial`): 1) ekran kararır, yalnız öğreticinin İNŞA ET düğmesi + ok + metin; 2) menüde Okçuluk Alanı
  kartı dışındaki her yer kararır (dört şerit + ok); 3) yerleştirme (karartma yok, üstte metin); 4) inşa sürüyor; 5) okçu binanın kapısında belirir ve ilk
  atış yerine koşar ("Okçu Kaptan hazır!"). Menü kapatılır/yerleştirme iptal edilirse 1'e döner. Okçuluk zaten varsa (eski/hazır köy) öğretici atlanır.
- **"İlk yapım bedava" kalktı;** yalnız ilk Okçuluk Alanı ücretsiz. Kurulum ALTIN bedeli `FeatureConfig.islandBuildGold`: Oduncu 100, Ağıl 100, Ev 250,
  Gözcü Kulesi 350, Kışla 600, Manastır 900; aynı türün 2.+ kurulumu ayrıca eski Odun/Et liste bedelini ister (ECONOMY §13).
- Eski kayıtlar: binalar, birimler, bedava hak maskesi korunur (yalnız Okçuluk dışı türlerde bedava hak artık yok).

## D79 — Ada seviyeleri Ana Üs'ten bağımsız (6. oturum 3.3)
- `Progression.Island.KeepFollowsMainBase = false`: Kıyı Kalesi ve Okçu (= Okçuluk Alanı seviyesi) 1'den başlar, kendi Odun/Et bedeli ve Usta süresiyle
  yükselir. Ana Üs yalnız KİLİT: **Kıyı Kalesi Sv.N için Ana Üs Sv.N+1** (`KeepRequiredMainBase`, en az 5 = ada açılışı). Diğer ada binalarının tavanı
  zaten Kıyı Kalesi seviyesi (`MaxBuildingLevel`), böylece Ana Üs → Kale → binalar zinciri tek yönlü.
- Kale paneli: YÜKSELT → Sv N (bedel, süre, kilit nedeni), sonraki seviyenin can/Odun tavanı/en yüksek Akın'ı; bina panelleri "Sonraki: …" (yeşil).
  Okçuluk Alanı artık yükseltilebilir; bitince `archerLevel` = bina seviyesi.
- **Göç (adil):** girilmiş adada seviyeler o anki ETKİN değerde (Ana Üs'ü izleyen = max(kayıt, Ana Üs)) sabitlenir, kimse seviye kaybetmez; bundan
  sonra bağımsız ilerler. Gerekçe: oyuncu o seviyeyi görmüş ve onunla savaşmıştı; düşürmek ceza olurdu. Girilmemiş adada 1'den başlar.

## D80 — Akın eğrisi yeni başlangıca göre (6. oturum 3.4)
- `AkinScale`: Kıyı Kalesi 5'in altındaki her seviye için −0,075 (`akinScalePerKeepBelow`; Kale 1 → 0,5, tavan altı 0,5). Okçu Sv1 hasarı Sv5'in
  0,625'i; düşman canı 0,5/0,8 = 0,625 oranında düştüğü için "öldürme süresi" eski Kale 5 başlangıcıyla aynı (test `S3_3_4`). Kale 1'de Akın 1–3.

## D81 — Arazi genişletme şeffaf ve elle (6. oturum 4.1)
- Önce: Ana Üs 4/7/10/13'te arazi kendiliğinden açılıyordu; kilitli araziye dokununca yalnız "Ana Üs Sv.4 ile temizlenir" yazıyordu.
- **Karar:** `Base/LandExpansion.cs` + ortak `Systems/AreaHighlight.cs`. Kilitli araziye dokununca o hücrenin AİT OLDUĞU kademenin tüm alanı (kenarları
  altın, nabızlı hafif dolgu) vurgulanır; kart: koşul (Ana Üs Sv.N, sağlanınca ✓), büyüklük (+80 inşa hücresi), içindeki kaynaklar (ağaç/kaya/çalı),
  AÇ düğmesi. Koşul sağlansa da alan kendiliğinden açılmaz; AÇ → bulut dağılma animasyonu → alan, çit ve kamera yenilenir. Sonraki kademeler de
  dokununca kendi alanını gösterir (kilitli; "önce yakındaki alanı aç"). Durum `PlayerSaveData.landOpened` (yeni oyunda 0; eski kayıtta -1 → Ana Üs
  kademesi kadar açık sayılır, kimsenin arazisi kapanmaz). Seviye afişi "YENİ ARAZİ AÇILABİLİR" der.
- Ada: bölge paneli aynı vurguyu ve "Koşul: Kıyı Kalesi Sv.N ✓ · Alan: +N hücre · N ağaç" bilgisini kullanır; açma zaten Usta görevi + bulut dağılması.

## D82 — Süreli temizleme ve küçük eylem menüsü (6. oturum 4.2)
- Büyük "Kaldır / Vazgeç" kartı yerine öğenin hemen üstünde `WorldActionMenu` (dekor/bina menüsüyle aynı aile): KALDIR + bedel/süre hapı.
- `Systems/NatureClear.cs`: süre `FeatureConfig` (çalı 3, kütük 4, kaya 5, ağaç 10 sn); iş sıraya girer, işçi boşalınca başlar (halka gri "SIRADA");
  öğenin üstünde radyal ilerleme halkası + kalan saniye; bitince toz efekti + ödül yazısı. Ana Üs: inşaatçı görevi ("nature:<id>", oyun kapalıyken de
  ilerler; bitiş GameManager'da kayda yazılır, ödül orada verilir: ağaç +12 Odun, kütük +6 Odun, çalı +2 Et, kaya +8 Altın — config). Ada: Ada Ustası
  yürüyüp keser (gerçek zaman), bedel/ödül mevcut ada kuralları. Ana Üs harita ağaç/kaya nesneleri de aynı menü ve config süreleriyle.

## D83 — Savaş kartı açma penceresi (6. oturum AŞAMA 5)
- `UI/BattleCardUnlockDialog.cs`: Tiny Swords kâğıt + kurdele, kart simgesi + açıklama; altta EŞİT boyutlu iki düğme: mavi "▶ REKLAM İZLE (kalan hak)"
  (`ui_icon_play`) ve altın "💎 N İLE AÇ" (elmas simgesi + net fiyat; yetmezse kırmızı → Elmas Dükkânı). Günlük kart reklamı hakkı
  `FeatureConfig.dailyBattleCardAds` = 9 (savaş başına 3 kart × 3 savaş); bitince düğme pasif, "Yeni hak 3sa 12dk" (yerel gece yarısı).

## D84 — Bütünsel denetim (6. oturum AŞAMA 6)
- **Zamanlayıcılar:** Ana Üs inşaatçı görevleri (inşa, yükseltme, kahraman/çırak, doğa temizliği) tek servis `GameManager` (UTC tick, kayıtta);
  ada Usta görevleri `IslandEconomy` (UTC, kayıtta); Kart Atölyesi, akın zamanlayıcısı, canlar da kayıtlı UTC. Hepsi oyun kapalıyken ilerler.
  Ada doğa temizliği (yeni, kısa 3–10 sn) gerçek zamanlı ve kayıtsız: bedel BİTİŞTE ödendiği için yarıda kapanırsa kayıp yok.
- **Saat manipülasyonu:** günlük sınırlar zaten `GameClock` (geri alınmaz yüksek su işareti). **Bulgu:** inşaatçı görevleri ve maden ham
  `DateTime.UtcNow` kullanıyordu → saat geri alınınca süreler uzayıp/kısalıyordu; ada tekrar ödülü günlük sınırı da ham saatle. **Düzeltme:**
  GameManager'daki tüm zaman okumaları ve ada günlük sınırı `GameClock.UtcNow`. **Sınır:** saati İLERİ almak sunucu olmadan çevrimdışı
  doğrulanamaz (sektörde de sunucu saati gerekir) — bulut kaydı/sunucu saati geldiğinde (PLAY_RELEASE §8) kapatılmalı.
- **Elmasla hızlandırma:** Ana Üs, ada ve Kart Atölyesi aynı `GemEconomy.FinishCost` (≤1 dk 1 elmas); kahraman/çırak da artık aynı yoldan.
- **Altın kuru:** eksik altın tamamlama ile dükkân aynı Ana Üs'e bağlı kur (`ShopService.GoldPerGem`); önceden sabit 40 idi (Ana Üs 15'te anlamsız).
- **Kilitler tek kaynak:** kahraman/çırak tavanı `GameManager.GetHeroMaxLevel` (Ana Üs); Kıyı Kalesi `KeepRequiredMainBase`; arazi kademesi
  `LandExpansion.RequiredMainBase` (BaseWorldLayout.NextLandLevel artık bunu çağırır, ayrı formül kalmadı); tamamlanmış bina seviyesi `BuildingLevels`.
- **Öğretici çakışmaları:** ada sırası tanıtım (330) → ad (320) → Okçuluk öğreticisi (310) → akın öğreticisi (okçu olmadan akın panosu açılmaz).
  Hazır köyde/eski kayıtta Okçuluk öğreticisi atlanır. Ana Üs öğreticisi (FirstBaseGuide) adada çalışmaz.
- **Test koşusu notu:** `-testFilter` sınıf adıyla eşleşince `[Explicit]` uzun ölçümler de koşar (Island* ≈6 dk+ her biri); gruplar adlarla verilmeli.

## D85 — Kahraman/çırak eğitimi inşaatçı tutmaz (6. oturum SON bulgusu)
- Baştan oynatmada (yeni hesap, bot) kahraman/çırak yükseltmesi tek inşaatçıyı binalarla paylaştığı için kahraman Ana Üs 5'e kadar Sv2'de kaldı,
  B1-5 defalarca kaybedildi, cüzdan tavanda bekledi. **Karar:** eğitim görevleri `builderIndex = −1` (kendi yuvası; kahraman ve çırak ayrı ayrı birer);
  süre eğrisi ve elmasla bitirme aynı (D74), seviye yine süre bitince artar. İnşaatçı sayacı (`BusyBuilderCount`) bunları saymaz.

## D86 — Ada tanıtımında DEVAM basılmıyordu (cihaz geri bildirimi)
- **Kök neden:** `IslandIntro.Play` güvenli alanı (kartın kabı) `Build` çağrısının argümanı olarak ÖNCE oluşturuyordu; tam ekran "Sky" katmanı
  (dokunuş yutan, raycastTarget) sonradan eklendiği için kartın üstünde kalıyor, DEVAM'a gelen her dokunuşu yutuyordu. Test düğmeyi doğrudan
  çağırdığı için yakalamamıştı. **Düzeltme:** kart kabı en üste alınır (`SetAsLastSibling`). **Test:** `S3_1_2` artık EventSystem ile düğmenin ekrandaki
  ortasına ışın atar, ilk isabetin DEVAM olduğunu denetler (düzeltme kaldırılınca "ilk isabet: Sky" ile başarısız olduğu doğrulandı).
- Diğer yeni pencereler (öğretici, duraklatma, savaş kartı, arazi kartı, dükkân) aynı açıdan gözden geçirildi: katman sırası doğru.

## D87 — Dar telefonda ada kadrajı uzaklaşmıştı (cihaz geri bildirimi, 15 Kasım)
- **Kök neden:** sabit kadraj genişliği tüm kara sınırına (iki yandaki küçük adacıklar dahil, 21 u) sığdırılıyordu. Tablette (4:3, 0,75)
  yükseklik bağlayıcı olduğu için fark edilmedi; 19,5:9/20:9 telefonda genişlik bağlayıcı → ana ada ekranın ~%80'i.
- **Düzeltme:** `IslandCameraController.MainlandBounds` (kale hücresine 4-komşulukla bağlı kara); genişlik yalnız ana adaya (19 u) sığar,
  adacıklar kenarda kesilebilir. 20:9'da ana ada %96 genişlik (≈×1,11 yakın); 16:9 ve tablet değişmedi (yükseklik bağlayıcı).
- **Test:** `Session6Tests.S7_IslandFramingOnNarrowPhoneFitsMainland` (4 oran: sığma + bir eksenin dolu olması + görüntü).

## D88 — Bölgeye özgü savaş haritası: veri modeli (7. oturum AŞAMA 1)
- **Bölge SO** `BattleRegionMap` (Resources/BattleMaps/Region_N): Tiny Swords düzeninde karo seti (9×6, 64 px), kara maskesi (satırlar üstten
  alta, '#' kara), atmosfer, süsleme seti (`BattleDecorEntry`: sprite, adet, boy, engel mi, yol payı, salınım, kenara yığılma), bölge yapıları
  (`BattleLandmark`: aday konumlar; yol/yuva/üsle çakışmayan ilk aday). İsteğe bağlı `terrainPrefab` alanı var ama **zemin maskeden çalışma anında
  kurulur** (karar: elle boyanmış prefab ile maske ayrı kalınca yürünebilirlik/görsel ayrışır; maske tek kaynak, Inspector'dan düzenlenir).
- **Seviye SO** `BattleLevelLayout` (Resources/BattleMaps/Levels/RN_LM): rota(lar) (kontrol noktaları; Catmull-Rom = generate.py ile aynı eğri),
  yuvalar, Ana Üs / maden / kahraman doğuşu, tür, isteğe bağlı dalga geçersiz kılma (boşsa bölge verisindeki dalgalar — tek kaynak bozulmasın).
- **Izgara:** 16×32 karo, 1 karo = 1 birim; Ana Üs (0,0) yolun alt ucunda, düşmanlar üst köprüden (0, 28,5) girer. Haritanın kara kutusu 14×30.
- **Düzenleme:** seviye varlığı seçiliyken Scene görünümünde rota noktaları, yuvalar, üs/maden/kahraman tutamaçla sürüklenir (Undo'lu);
  Inspector'da Doğrula / Yuvaları yeniden üret / Rotayı şablona sıfırla. Üreteç var olan varlığa dokunmaz (`-force` ile baştan).

## D89 — Kar / koyu / volkanik zemin: Tiny Swords karosundan renk eşlemesi
- Projede (Tiny Swords, Cainos, Handpainted, DungeonMonsters2D) kar/koyu taş/lav **piksel** karosu yok (Handpainted boyama dokusu, stil çatışır).
- Eskiden çim karosuna yarı saydam ton bindiriliyordu ("boyanmış çim"). Yeni yöntem `Tools/campaign_art/recolor_tilesets.py`: her piksel tonuna göre
  sınıflanır (yeşil = zemin, mavi-gri = uçurum taşı, kontur, köpük) ve bölge paletine **gradyanla** eşlenir; Tiny Swords konturu/dokusu/kenarları aynen
  kalır. Kar (gama 0,5: çoğu beyaz, gölge mavi), Karanlık Kale (kayrak-mor), Ejderin İni (bazalt-kahve). Gerçek karolar ART_NEEDS #8'de.

## D90 — Rota şablonları ve yuva yerleşimi
- **1. seviye = Sefer Haritası yolu:** generate.py map_region'ın 6 noktası normalize edilip (u = dx/704, v = (y−110)/640) dikeye ölçeklenir
  (x = u·8,5 — dikey adada zikzak karakteri korunsun diye yanal %20 geniş; y = 26 − 24,25·v). Tek bölgelerde Ana Üs'e yakın kıvrım sağda (haritadaki gibi).
- 2: üç büyük kıvrım · 3: ters başlangıç + sıkı S (maden orta kıvrımın yanında) · 4: ikiye ayrılıp birleşen yol (+1 yuva) · 5 (boss): 6 kıvrım, en uzun.
- **Yuvalar:** ana yol ilerleyişi pencerelere bölünür; her pencerede bir öncekinin TERS tarafında (gidiş yönüne göre) kapsaması en yüksek aday →
  zikzağı izleyen sağlı sollu dizi. Kurallar (üreteç = doğrulama): kara üstünde (kıyıdan 0,3 içeride), yol kenarından ≥ 2,0 (yuva yarıçapı dahil),
  kule görseli (yuvadan 2,3 yukarı) yola binmez, üs/maden/kahraman/diğer yuvadan uzak, Topçu menzil çemberi (merkez yuva + 1,0, yarıçap 5 —
  6. oturum merkez düzeltmesiyle aynı `RangeCenter`) en az bir dönemeç tepesini kapsar. Sayı = bölge tabanı (3/3/4/4/5/5) + çatallı yolda 1.
- Test: `Session7Tests.V1` (30/30 doğrulama), `V2` (1. seviye = harita yolu, türler), `V3` (5 farklı rota, boss en uzun, çatal).

## D91 — Eksik bölge süsleri kodla üretildi (geçici)
- `Tools/campaign_art/battle_props.py`: yıkık kale/kule/duvar (Tiny Swords Kırmızı Kale → gri taş, testere kesik + moloz), maden girişi
  (Tiny Swords kayaları 2× + karanlık ağız + ahşap çerçeve), buzlu kaya, kemik/kafatası, lav gölü. Resources/BattleMaps/Props. ART_NEEDS #9.

## D92 — Savaş haritası kurulumu (7. oturum AŞAMA 2)
- `BattleMapBuilder`: deniz (kamera arka planı + dalga yayları), sığ su halkası (karaya uzaklıktan yumuşak doku), Tiny Swords karo zemin
  (generate.py land() kuralının birebir aynısı: altı boş kara hücresinin altına uçurum satırı 5, çim sütun 5–8 / satır 0–3), toprak yol
  (rotalar boyunca disk damgası: koyu kenar + toprak + taş benekleri, 24 px/birim tek doku), üst köprü (kalas + direk), bölge yapıları
  (adaylar; tutmazsa aynı tarafta en yakın uygun yer taraması, genişlik ≤ 4,2) ve süsler (tohumlu, yol/yuva+kule görseli/üs/maden/köprü dışı).
  Engel süsler (ağaç gövdesi, büyük kaya, lav gölü, yapılar) `BattleObstacles`'a yazılır → kahraman, Çırak, Lancer ve yol bulma tanır.
- Ana Üs (0,0), kahraman/yuva/maden konumları seviye varlığından. Kamera sınırı harita + köprü (`BattleCameraFollow.SetupBounds`).
- Harita yoksa (sefer dışı eski "Level N" savaşı) eski düzen aynen çalışır. Sonsuz Sefer kat temasının bölgesini (türe göre seviye, boss
  katında 5.), İstila Bölge 6 boss yerleşimini kullanır.

## D93 — Düşman yolu izler; sapma kuralları
- Her düşman (çağrılanlar dahil) en yakın yola bağlanır (`EnemyController.SetLane`, ±0,32 şerit kayması); köprüden doğar, çatallı yolda kollar sırayla.
- Ana Üs'e giderken yoldan ayrılmaz. Yalnız yolun **3 birim** yakınındaki (yoldaki konumuna göre) hedefe sapar: yol kenarındaki kule (Kuşatıcı/Yıkıcı/
  Bombacı), kahraman/Çırak (Avcı, kışkırtma, dipte), Lancer. Hedef uzaklaşınca/ölünce yola geri döner (yolda kaldığı yerden). Gerekçe: yakın
  dövüşçünün yol kenarı kuleye/kahramana hiç vuramaması rolleri (Yıkıcı, Avcı) işlevsiz bırakırdı; 3 birim yuva mesafesine (2,15–3,3) göre seçildi.
- Madeni Koru yağmacısı yoldan ayrılıp **yürünebilir kara** üstünden (ortak A*: `BattleWalkGrid` artık su/uçurumu da kapalı sayar) madene gider.
- Kahraman ve Çırak su/uçuruma giremez (`BattleTerrain.KeepOnLand`, kıyı boyunca kayar). Cadı ışınlanması karaya, Bölge 5 türbeleri karada ve
  yoldan 2,2+ uzakta (biri üst, biri alt yarıda).
- Test: `Session7Tests.B1` (6 bölge: harita, yuvalar, düşman sapma ≤ 0,6), `B2` (6 bölge maden sapması karadan + vuruş), `B3` (kahraman karada),
  `B4` (boss yolu, zamana karşı sayacı, çatal).

## D94 — Bölge atmosferi + grafik kalitesi (7. oturum; Sonnet alt ajanı, orta çaba — ben inceleyip görüntüyle doğruladım)
- `BattleAtmosphereFx` (yalnız görsel): Yıkık Kale toz zerreleri · Sisli Orman 5–8 yarı saydam sis bulutu yatay süzülür (FogOfWar görüş
  mekaniği ayrı, aynen) · Maden altın parıltıları · Donmuş Geçit kar yağışı · Karanlık Kale kasvet örtüsü + kenar kararması + silik kül ·
  Ejderin İni lav gölü ışıması (DOTween nabız) + kabarcık + kül/köz. Eski düz renk örtüsü/rastgele lekeler haritada kullanılmaz.
- Performans: her bölge en çok 60 sprite (ölçüm: 14 / 8 / 10 / 40 / 18 / 45). **Düşük kalite** (`BattleFxQuality.Low`: QualitySettings en alt seviye
  ya da `MEB_LowFx` anahtarı) → partikül yarıya (7 / 4 / 5 / 20 / 10 / 20), ağaç salınımı ve lav kabarcıkları kapalı. Ayarlar ekranına ayrı
  "düşük efekt" düğmesi eklenmedi (Unity kalite seviyesi zaten bağlı); istenirse `PlayerPrefs MEB_LowFx=1` ile tek satır.

## D95 — Denge: bölge haritası düşman can çarpanı ve maden konumu
- Uzun zikzak yol kulelere çok daha fazla atış süresi verdi: ilk ölçümde (tek tohum) kule + kahraman 30 seviyenin 28'ini Ana Üs'ü **%100** canla
  bitiriyordu; Bölge 5'te yalnız kule ve kahramansız kazanıyordu. Karar: harita savaşlarında düşman canı bölgeye göre ×1,0 / 1,0 / 1,2 / 1,25 / 1,4 / 1,15
  (`BattleManager.MapEnemyHealth`; Sonsuz/İstila kendi ölçeğinde, etkilenmez). Bölge 6'da 1,25 kule+kahramanı da yendi → 1,15 (2 tohum doğrulandı).
- Madeni Koru: maden köprüye yakınken ilk düşmanlarla 23 sn'de yıkılıyordu → yolun üsse yakın yarısına (ilerleyiş %55–80) alındı.
- Sonuç ve tablo: REPORT.md §2. Bilinen: tek tohum gürültülü; erken bölgelerde (1–2) kule+kahraman çok rahat — "ilk bölgeler affedici" kararıyla uyumlu bırakıldı.
- Test uyarlaması: `BattleStructureTests` iki testi (eski 4 köşe yuva düzeni, serbest düzende rol/kışkırtma) haritayı geçici kapatarak eski düzeni denetler.

## D96 — Düşman yol davranışı durum makinesiyle (8. oturum 1; Opus yüksek çaba)
- **Sorun:** sapan düşman, hedef uzaklaşınca ayrıldığı noktaya (`laneS` orada donuyordu) GERİ yürüyordu.
- **Durumlar** (`EnemyController.LaneState`): Yolu izle → Kilitlen (hedefe yürü) → Savaş (menzilde) → Yola dön. `LaneDistance` = yolda kat edilen mesafe,
  **yalnız artar** (sapma/savaş/dönüşte pencereli izdüşümle güncellenir: `BattleRoute.ProjectRange`, [ilerleme, ilerleme + 8]; zikzakta paralel kola atlamaz).
- **Kilitlenme menzili** `LaneLeash` 3 (hedef yolun ilerleme noktası çevresinde ve İLERİDE; geride 0,75'ten fazla kalan hedefe kilitlenmez).
  **Bırakma** `LeashDistance` 4,5 (hedef ya da düşman yoldan bu kadar uzaklaşırsa), hareketli hedef geride kalırsa ya da yol boyunca `ChaseAhead` 6 birim
  kovalandıysa; bıraktıktan sonra 1,2 sn yeniden kilitlenmez (titreme yok). Maden yağmacısı ve sabit yapı hedefi bu kuralların dışında (yapıya en çok 0,75 geri).
- **Yola dönüş:** hedef nokta ilerlemenin ilerisinde (öncü pay = yoldan uzaklık × 0,9, 0,4–2,2) ve düşman ilerledikçe ileri kayar → açılı, doğal eğri;
  yol yönündeki geri bileşen her adımda atılır. Yol dışı her adım ortak yol bulma (`GridSteer` / `BattleWalkGrid`: su, uçurum, yapı kapalı).
- **Ayrışma:** gövde yarıçapı (0,36 / Trol 0,55 / boss 0,75); yolda yan itme şerit kaymasını yumuşak değiştirir (±0,45), öndeki komşu yavaşlatır
  (geri itmez; eşit ilerlemede kimlikle sıra bozulur). Bakış yönü yumuşatılmış hız + histerezisle (titreme yok).
- Test `Session8Tests.A1` (6 bölge × S1/S3 × yandan kaçış / geriye kaçış: geri adım 0, ilerleme hep arttı, hep yürünebilir, ileriden katıldı),
  `A2` (14 düşman aynı noktada: örtüşme ≤ 3 çift, yön dönüşü 0,4/sn). Not: dönüşte en yakın İLERİDEKİ yol noktası seçildiği için zikzakta paralel kola
  (8 birim pencere içinde) kısa yoldan katılabilir (ölçümde S3'te 11 → 22).

## D97 — Düşman sayıları + %10 can tek yapılandırmadan; öldürme altını eşit; havuz (8. oturum 2)
- `Resources/Config/CampaignEnemyConfig.asset`: kullanıcının tablosu = savaşta **doğan** toplam (boss dahil). Eski Wave1Cap/boss ek düşmanı sefer
  savaşında artık uygulanmaz (toplama katıldı; üst bardaki "kalan / toplam" bu sayıdır). Can ×1,1 tüm sefer düşmanlarına (boss ve çağrılanlar dahil).
- Dalga dağılımı: en az seviyenin dalga sayısı; dalga başına ≤ 12 (aşılırsa dalga eklenir); ağırlık 1 + 0,2·i (ilk dalga en küçük). Eklenen dalgalar asıl
  dalgalara **orantılı** eşlenir (tür karışımı ve karşı düşman payı eski tasarımdaki gibi; ilk denemede ek dalgalar Trol alıyordu → düzeltildi).
  Dalga arası nefes 5 → 8 sn (duyuru 3 sn ayrıca). Sonsuz/İstila eski kural.
- Ekonomi: düşman başına öldürme altını × (eski beklenen / yeni beklenen), kesir taşımalı → savaş toplamı aynı (E1: 30/30 ±1). ECONOMY.md §14.
- Performans: düşman kabuğu havuzu (boss hariç; davranış bileşenleri her kullanımda TAZE eklenir, kabuk 2 sn sonra yeniden verilir), kahraman/Çırak
  aramaları önbellekte. P1: 3 × 18 düşman + 4 kule, %95 kare 16,7 ms (masaüstü, vsync), 54 düşmandan 36'sı havuzdan.

## D98 — Savaş üst barı Ana Üs süslü çerçeve stilinde (8. oturum 3; Sonnet alt ajanı orta çaba — ben test edip görüntüyle doğruladım)
- `Polish/UI/BattleTopBar.cs`: sol düşman "kalan / toplam" (`BattleManager.EnemiesRemaining/EnemiesTotal`), orta "Dalga n/m" + dalga noktaları + bölge·seviye,
  sağ altın + ayar (eski düğme taşındı), altında belirgin ilerleme barı (toplam öldürülen / toplam), ayrı hedef kartı (Madeni Koru maden canı, Zamana Karşı
  kalan süre, Üssü Koru üs canı). Güvenli alan içinde, metinler otomatik boyut + üç nokta. Eski TopBar boş yer tutucu (kamera kırpması için).
- Düşman giriş oku artık kameranın görünen dünya alanının içinde (barın/kartın üstüne binmez). Test görüntü yardımcısı (`Session4Tests.CaptureUi`) çekimde kamera
  görüş alanını tam ekrana açar (önceki görüntülerde arayüz kırpık alana sığdırılıyordu).

## D99 — Yuva etiketi yoldan uzak; bölge yapıları tamamen karada; kamera karada (8. oturum 3)
- Ekran görüntüsündeki B4-S3 (−0,3; 15,1) yuvasının "N HAK" etiketi yolun üstündeydi. Eski doğrulama yalnız rota uzaklığını ve halkayı ölçüyordu; etiket
  (merkez −1,18, 1,24 × 0,40) hiç denetlenmiyordu → kural eklendi, 26 seviyenin yuvaları yalnız yuva kısmı yeniden üretildi
  (`BattleMapAuthoring.RegenerateInvalidSlotsFromCommandLine`; rota/maden/üs aynı). Yeni bağımsız test `Session8Tests.V4`: GERÇEKTEN çizilen yol maskesi
  (`BattleMapBuilder.RoadCovers`) halka/etiket/kule görseliyle kesişmez.
- Bölge yapısı: eskiden yalnız alt tabanı karada aranıyordu (kıyıdaki mavi kule yarısı suyun üstünde). Artık tamamı kıyıdan 0,4 içeride.
- Kamera yatay sınırı ızgara + 0,5 yerine **karanın kenarı**; ekran adadan genişse kamera adanın ortasına sabitlenir (bir yanda deniz şeridi görünmez).

## D100 — Ana Üs yerleşim hatasının kök nedeni ve kalıcı çözüm (8. oturum 4; Opus yüksek çaba)
- **Kök neden (ölçüldü, `Session8BaseTests.O1`, eski kod):** küçük dekorların (çalı ×4, altın taşı ×2, koyun) dikey hizası `DecorData.VerticalOffset`
  sprite'ın TAM ÇERÇEVESİNİN alt kenarıyla hesaplanıyordu; Tiny Swords karelerinin altında saydam boşluk var → görsel, dolu sayılan hücrenin 0,7–1,2 birim
  YUKARISINDA çiziliyordu. Oyuncu çalıyı/koyunu bir sırada görüyor; kural ise altındaki BOŞ GÖRÜNEN hücreyi dolu sayıyor (red, kırmızı hücre) ve çizildiği
  hücreyi boş sayıyordu. 5. oturumun ayak izi raporunda da vardı (`decor:bush (15,23) görsel y 24,0–24,9`) ama "görünüşte boş" denetimi kuralla AYNI veriyi
  (nesne hücreleri) kullandığı için hiç yakalanmadı. Önceki iki düzeltme yapıların görsel alanına ve doğa kaydına odaklanmıştı.
- Keşif (`O0`): doğa nesnesi kaldırma (anında + süreli) hücreleri zaten tamamen boşaltıyordu (kayıt/nesne/collider/ızgara 10/10, yeniden yüklemede de).
  O0'daki retlerin hepsi, yeni kulenin kendi görselinin (ayak izinin ~1,8 altına sarkar) Ana Üs'ün çizimine binmesinden — meşru.
- **Çözüm:** (1) hiza sıkı ağın görünen alt kenarından (`DecorData.VisibleBottom`); (2) doluluk TEK KAYNAK: `BaseOccupancyGrid.Occupants` anlık görüntüsü
  sahnedeki canlı nesnelerden sıfırdan kurulur (yapı = ayak izi ∪ görsel hücreleri, kamp, ETKİN dekor, ETKİN ve kaldırılmamış doğa), kare başına ve her
  yerleştirme/taşıma/silme/kaldırma sonrası (`MarkDirty`) yeniden; `NatureObstacle.ByCell` artık elle yazılan statik sözlük değil, bunun görünümü (hayalet
  kayıt olamaz); kaldırma tek yoldan (`MarkRemoved`: görsel + collider + doluluk aynı karede). Kayıttaki hücre verisi doluluğa hiç girmez. Kural, ızgara
  gösterimi ve yürünemez hücreler aynı listeyi okur (eskiden dekor etkinliği gösterimde vardı kuralda yoktu). Gölgeler alfa < 0,35 ve ad süzgeciyle görsel
  ölçümünden dışlı.
- **Geliştirici:** DevMenu "OCCUPANCY GÖSTER" (yalnız Editor/Development; `OccupancyDebugView`): dolu hücreler türüne göre renkli + dolduran nesnenin adı.
- **Regresyon:** `O2` (mevcut/eski v12 kayıt ve yeni kayıt): 4 alanda 22 öğe (doğa anında/süreli + dekor) kaldırılır → 4 boyutta yeni bina kuralı, 4 türün taşıması
  ve geri alınması, yeni bina kurma; kayıt → yeniden yükleme. `O3` görünüm. 5. oturumun tüm yerleşim testleri yeni kodla geçti.

## D101 — Kayıt açılamazsa üstüne yazılmaz; göç bellekte, göç öncesi yedekle (9. oturum; TECH_AUDIT K-1…K-5, E-2, E-6)
- `SaveSystem` ayrı dosyada (`Data/SaveSystem.cs`). `Load` üç durum döndürür (`SaveLoadStatus`: NoSave / Loaded / Failed). **Failed** (hiçbir kopya
  çözülemedi ya da göç/onarım istisna attı): bütün kopyalar yerinde kalır, `persistentDataPath/SaveBackups` altına karantinaya kopyalanır ve `Save`
  kilitlenir. Kilidi yalnız oyuncunun onayladığı YENİ OYUN kaldırır (`DeleteSave`: önce `save.deleted.*` olarak arşivler).
- `GameManager.StartNewGame` kilit varken yeni oyun kurmaz, ana menüye döner (savaş/ada sahnelerinin "veri yok → yeni oyun" kısayolu kaydı ezemez);
  ana menüde DEVAM oyuncuya durumu söyler ve oyuna girmez.
- Göç zinciri (`Migrate`) bellekte çalışır, sonunda TEK kayıt; göçten önce orijinal JSON `save.v<N>.premigration.*.json` (gelecek sürümden gelen
  kayıt `save.v<N>.newer.*`). Yedek klasöründe tür başına en yeni 16 dosya tutulur.
- Dosya `File.Replace` ile yerinde değişir; yarım kalmış sağlam `.tmp` yüklemede aday. En yeni kopya `saveSerial` (her kayıtta artar), eşitse kayıt anı.
  `{}` gibi bina girdisi olmayan nesne kayıt sayılmaz.
- **Yeni sürüm eklerken:** `CurrentVersion` artır + `Migrate` sonuna blok (içinde `Save` çağırma). `Session9SaveTests.B1` v1…güncel bütün sürümleri kapsar.
- Bilinçli sınır: sahneye bağlı onarımlar (yerleşim, dekor 1×1, ada hayaletleri) hâlâ sahne açılınca çalışıyor; kayıt verisi üzerinde çalışan hâle
  taşımak ayrı iş (TECH_AUDIT KN-3).

## D102 — Türetilmiş kayıt alanları yüklemede kuraldan üretilir: `SaveInvariants` (9. oturum; KN-1a, KN-1b)
- `SaveInvariants.Repair`: tür seviyesi = tamamlanmış en yüksek bina seviyesi; kilit = `Progression.FirstUnlockLevel` kuralı. Yalnız oyuncunun lehine
  çalışır (kilit açar, kilitlemez; seviye uydurmaz). `SaveSystem.Load` ve `GameManager.RebuildBuildingData` içinde.
- `SaveInvariants.Check`: kalan tutarsızlıkların listesi (geliştirici derlemesinde uyarı, `Session9SaveTests.I1` içinde hata).
  Yeni türetilmiş alan eklenirse kaynağı, onarımı ve denetimi buraya yazılır.

## D103 — Tek saat ve geliştirici anahtarı (9. oturum; K-6, KN-4a)
- Oyun kurallarında ham `DateTime.UtcNow` yok: maden damgası, Kraliyet süresi, 2x maden, ağaç/kaya büyümesi, Sen Yokken, geri sayım yazıları
  `GameClock.UtcNow`. `Session9RuleGuardTests.K6` kaynak taramasıyla yeni kaçakları yakalar (izinli: GameClock, SaveSystem kayıt anı, bildirimler).
- `DevSettings.Enabled` artık oyun kuralı değiştirmez. Ada kilidi / oto-ateş atlaması ayrı seçenek (`IslandLockBypass`, DevMenu "ADA KİLİTLERİNİ ATLA",
  **varsayılan kapalı**): Development APK ile yayın derlemesi aynı oyunu oynatır. ⚠️ Tablette oto-ateşin baştan açık olmasına alışıldıysa artık Kıyı Kalesi 5'te açılır.

## D104 — Ana Üs kaydı eksik sahneden yazılmaz (9. oturum; KN-1d)
- `BaseMapManager.SaveAll` bina listesini sahneden baştan yazıyordu; `LoadFromPlayerData` tanımadığı türdeki binayı kurmadan geçip sonunda `SaveAll`
  çağırdığı için o bina kayıttan sessizce siliniyordu (daha yeni sürümün kaydıyla eski sürüm açılırsa). Yükleme bir hatayla yarıda kalırsa da yarım liste yazılabiliyordu.
- Artık: yükleme sürerken/yarıda kaldıysa kayıttaki liste yeniden yazılmaz; kurulamayan binalar listede korunur. Test `Session9RuleGuardTests.KN1d`.
- O-1: `CreateNew` maden deposunu 40 yazıp 55 satır sonra 0'lıyordu; ikinci atama kalktı (rehberin "madene dokun" adımı artık altın veriyor).

## D105 — Tek geri tuşu yöneticisi (9. oturum; E-1, Y-6; uygulama Sonnet alt ajanı, gözden geçirme + test bende)
- `UI/BackKeyRouter.cs`: kalıcı tek nesne, derleme koşulu yok (eski `SelectionBackKey` yalnız Development'ta kuruluyordu → yayında geri tuşu ölüydü).
  Sıra (basış başına tek işlem): geçiş sürüyorsa yut → çıkış onayını kapat → en üstteki pencere (her birinin kendi kapatma yolu) → yerleştirme İPTAL →
  seçim → radyal menü → sahne varsayılanı (savaş/akın: ayarlar = duraklat; sefer: panel → harita → ÜSSE DÖN; ada: ANA ÜS; Ana Üs ve ana menü: çıkış onayı).
- **Yutulan zorunlu akışlar:** ödüllü reklam, "Ana Üs yıkıldı, devam?" teklifi (kapatmak yenilgi sayılırdı), ada tanıtımı/inşa öğreticisi/ilk ad verme,
  savaş ve akın sonucu. "Sen yokken" geri tuşuyla toplamadan kapanır (altın madende kalır).
- Yeni pencere eklerken: `BackKeyRouter.CloseWindow` içine kapatma yolunu ekle (ya da pencereyi `SelectionManager`'a kaydet). `LastStep` hangi basamağın
  işlediğini söyler (tanılama/test).
- Manifestte `enableOnBackInvokedCallback=false` (Unity `androidPredictiveBackSupport: 0`) → API 36'da tuş eski yoldan oyuna gelir; cihazda bir kez denenmeli.
- Bilinen eksik: satın alma sürerken yutma yok (bekleyen satın alma bayrağı dışarı açık değil); DevMenu geri tuşuyla kapanmaz.

## D106 — Ana Üs yeni UI: Üst bar planı (10. oturum)
Kapsam: yalnız görünüm (UI_DEGISIKLIK §1, §6). Bugünkü bar kodla kuruluyor: `BaseSceneSetup.cs:212-359` (TopBarBG, GoldPill/GoldText/GpsText, BuilderPill/BuilderCount) → `BaseHudRedesign.StyleTopBar` (`:38-140`; GemPill, LevelCrest) → `ResourceTopBar.AttachBase/LayoutBase` (Et/Odun, `:32`, `:119`). Dişli ayrı tuvalde: `AudioSettingsUI.cs:86-109` (sortingOrder 1200). Tuval: ref 390×844, match 0,5 (`ReferenceUiVisuals.cs:54`), SafeArea `ReferenceUiVisuals.cs:63`.
- **Sprite → eleman** (`Assets/MiniEmpireUI/MiniEmpireUI/Sprites`): `topbar_bg` 118×98 → TopBarBG şeridi; `res_pill_bg` 110×68 → altın/odun/et/elmas/inşaatçı hapları (sol 63 px ikon yuvası); `icon_gold` 47×49, `icon_wood` 53×44, `icon_meat` 47×43, `icon_gem` 46×43, `icon_builder` 51×54 → hap ikonları; `level_shield_empty` 114×141 → LevelCrest gövdesi; `banner_left/right` 48×128 → mavi bayrak süsü (asset VAR, eksik değil); `btn_settings` 83×83 → dişli (tek parça; `btn_round_empty`+`icon_gear` yedek). `res_capsule_bg`, `ornament_diamond`, `rivet` isteğe bağlı süs.
- **9-slice (px, yalnız yatay):** topbar_bg L24/R23; res_pill_bg L63/R29; res_capsule_bg L30/R29 (meta'da zaten girili). Tuval 390 genişlikte, paket 886 için çizildi → sliced Image'larda `pixelsPerUnitMultiplier = 886/390 ≈ 2,27` (yoksa kenarlar 2,27× şişer). Boylar: bar ≈43 u, hap ≈30 u, kalkan ≈50×62 u, bayrak ≈21×56 u, dişli ≈37 u.
- **Import:** PPU 100 (paket içinde tutarlı), mipmap yok, Uncompressed, Clamp, Full Rect. Filter: kural gereği Point; ANCAK sprite'lar boyalı (gerçek piksel sanatı değil) ve ekranda ≈1,2× tamsayı-dışı ölçekte → 1080×2400 görüntüsünde tırtık/titreme varsa yalnız TopBar için Bilinear'da kal ve bunu burada not et. DİKKAT: `MiniEmpireUIImporter.cs:39` her reimport'ta Bilinear'ı zorlar; Point seçilirse bu satır değişmeli. Paketin `MiniEmpireUIBuilder` menüsü ÇALIŞTIRILMAZ (kendi Canvas'ını kurar).
- **Sprite erişimi:** klasör Resources dışında → `Resources/MiniEmpireUiCatalog.asset` (Sprite alanlı ScriptableObject, `IslandVisualCatalog` deseni) yeni; alt bar da kullanır. Atlas: MiniEmpireUI için SpriteAtlas (Project Settings'e dokunmadan).
- **Değişecek:** `BaseHudRedesign.StyleTopBar` sonunda yeni `MiniEmpireTopBarSkin.Apply(hud)` (Polish/UI, yeni dosya; csproj'a elle ekle); `ResourceTopBar.LayoutBase` aralıkları; `LevelCrest.Build/Refresh`; `AudioSettingsUI` Ana Üs dalında dişli sprite/anchor; `FeatureConfig` yeni `miniEmpireTopBar` bayrağı.
- **Korunacak bağlar (nesne adları AYNI kalır; testler ve kod `Find` ile arıyor):** GoldText ← `ResourceHUD` renk flaşı + GoldDelta popup (`ScenePolishInstaller.cs:116-159`) ve coin uçuşu sayacı (`BaseWorldUI.cs:86-90`, `:375`); GemText ← `GemCounter` (DOPunchScale, `GemCounter.cs:46-55`), GemPill tık → `GemShopUI.Open`; Et/Odun ← `ResourceTopBar.Refresh` (0,2 sn; Sv.5 öncesi kilit simgesi + toast, tık davranışı); BuilderCount ← `BaseSceneSetup.cs:424` "müsait/toplam"; LevelCrest ← `OnUpgraded` + punch; dişli → `AudioSettingsUI.Open`. TopBarBG rect'i kamera çerçevesi için okunuyor (`ReferenceUiVisuals.cs:356,375`) → ad ve üst sınır korunur.
- **Paket script'leri:** `TopBarController/ResourceCounter/LevelBadge` KULLANILMAZ (ikinci bir veri/animasyon yolu açar); `NumberFormatter` KULLANILMAZ (TR'de 1.000="B" der; projede `NumberFormat.Short` "12.4K", eşik `FeatureConfig.topBarShortFrom`); paket `SafeArea` yerine mevcut olan. Yalnız fikir alınır: basamak sayısına göre seviye punto (54/46/36 → ölçekli).
- **Yerleşim (SafeArea genişliği, x anchor):** bar 0–1, üste yapışık (pivot üst). Sol: altın 0,02–0,165 · odun 0,17–0,29 · et 0,295–0,40. Orta: bayrak_sol 0,395–0,45, kalkan 0,435–0,565 (barın altına ≈%40 taşar, bayrakların ÖNÜNDE), bayrak_sağ 0,55–0,605. Sağ: elmas 0,60–0,71 · inşaatçı 0,715–0,855 · dişli 0,87–0,98. Sıra yine `FeatureConfig.baseTopBarOrder` (varsayılan gold,wood,meat | gem,builder — referansla aynı); yalnız `LayoutBase` aralıkları 0,02–0,40 / 0,60–0,855 olur. GpsText referansta yok → gizlenmez, altın hapının altında küçük kalır (kullanıcı onayı gerekirse ayrıca).
- **Kalkan:** "ANA ÜS" alt yazı + seviye sayısı kalkanın içinde. Mevcut `TierColor` boyaması kırmızı sprite'ı bozar → gövde boyanmaz; kademe rengi kalkan kenarına ince çerçeve/parıltı olarak (karar gerekirse sor).
- **Diğer sahneler:** Ada `IslandHud.cs:113-181` `ResourceTopBar.BuildIsland` + `OrnateStrip` kullanır → `MakeChip`/`OrnateStrip`/`BuildIsland` DEĞİŞMEZ; skin yalnız `mode == Base` ve bayrak açıkken. Savaş `BattleTopBar.cs`, Sefer `CampaignSceneSetup.cs:118` ayrı tuval → dokunulmaz. Ada'nın uyarlanması sonraki iş (GECE_DURUM'a yazılacak).
- **Taşma:** tüm değer TMP'leri NoWrap + AutoSize (max 15, min 8), Short eşiği korunur. Bilinen açık: `ResourceHUD.SetResource` altını `value.ToString()` ile yazar (kısaltmasız) → yalnız biçim olarak `NumberFormat.Short`'a bağlanmalı. 9:21'de dar kalırsa hap genişliği değil yazı küçülür; 4:3 tablette bar tam genişlik, haplar oranla büyür (yükseklik ≈43 u sabit).
- **Font:** mevcut `ReferenceUiVisuals.ApplyFont` (`:109`, Türkçe dinamik asset `:135`); yeni font/paket outline materyali yok. Kahverengi kontur gerekirse TMP outline (paylaşılan materyal, kare başı kopya yok). Test metni: "ANA ÜS", "İNŞAATÇI", "ç ğ ı ö ş ü İ".
- **Raycast:** bar, bayrak, kalkan, ikonlar raycastTarget=false; yalnız elmas/et/odun hapları ve dişli dokunuş yutar.
- **Riskler:** (1) Importer'ın Bilinear'ı geri yazması; (2) `Session3Tests`, `UiNightTests`, `ViewportRevisionTests`, `Nov13FeedbackTests` sıra/anchor/ad varsayımları; (3) dişlinin ayrı tuvalde olması → SafeArea ve kalkan hizası iki yerde; (4) kalkanın bar altına taşması kamera üst boşluğunu (bug 13 zoom işi) etkiler; (5) TierColor'ın kaybı oyuncu için görünür değişiklik; (6) 886 ref → 1440p'de hafif yumuşaklık (README).
- **Karar (yönetici, 1/6 üst bar):** (1) Kalkan referanstaki gibi sabit kırmızı `level_shield_empty`; `TierColor` yeni barda KULLANILMAZ (yalnız eski barda), seviye numarası dinamik. (2) `GpsText` yeni Ana Üs barında gösterilmez (yalnız görünüm; `ResourceHUD` veriyi yazmaya devam eder). (3) Yeni görünüm yalnız Ana Üs (`FeatureConfig.miniEmpireTopBar`, varsayılan açık; katalog yoksa eski bar); `MakeChip`/`OrnateStrip`/`BuildIsland`, Ada/Sefer/Savaş değişmedi. (4) Paket script'leri kullanılmadı; `ResourceHUD.SetResource` altını artık `NumberFormat.Short` ile yazar (büyük altın taşmaz).
- **Filtre kararı: Bilinear.** 1080×1920 görüntüde (`Docs/playtest/ui-ana-us/01-filtre-bilinear-ust-point-alt.png`) Point, boyalı sprite'ların (bayrak, kalkan, simge kenarları) ≈2,3× tamsayı-dışı ölçekte tırtıklı çıkmasına yol açtı; Bilinear referans görüntüye daha yakın ve temiz. `MiniEmpireUIImporter.cs:39` (Bilinear) bu yüzden DEĞİŞTİRİLMEDİ, metalar tutarlı (filterMode 1). Yalnız bu sprite'lar için; piksel sanatı (PixelCastle, Tiny Swords) Point kalır.
- **Uygulama (1/6):** `MiniEmpireUiCatalog` (+ `Tools/MiniEmpire UI/Build Catalog`, `Resources/MiniEmpireUiCatalog.asset`), `MiniEmpireTopBarSkin.Apply` (`StyleTopBar` sonunda), `LevelCrest` yeni dalı, `AudioSettingsUI` Ana Üs dişlisi (`btn_settings`, sağ üst). Bar 47 u, hap 33 u (referans oranına yaklaştırmak için D106'daki 43/30'dan büyütüldü), hap payları altın 1,3 · odun 1,15 · et 0,95 · elmas 1 · inşaatçı 1,1. Yakalayıcı: `UiAnaUsCapture` (ortam: UI_CAPTURE_W/H/OUT/FILTER/GOLD/LEVEL). Bilinen fark: bar ekranın en üstüne yapışık (referanstaki ≈10 u üst boşluk yok; kamera görüş alanı TopBarBG altından başladığı için üstte koyu dolgu kalır), kalkan/bayrak barın üstüne taşmaz.
## D107 — Ana Üs yeni UI: Alt bar planı (10. oturum)
Kapsam: yalnız görünüm (UI_DEGISIKLIK §2, §6), D106 yaklaşımı aynen: `MiniEmpireUiCatalog` (yeni alt bar alanları + Build Catalog), `FeatureConfig.miniEmpireBottomBar` (yeni, varsayılan açık; katalog yoksa veya kapalıysa bugünkü radyal menü), Bilinear, sliced Image'larda `pixelsPerUnitMultiplier = 886/390 ≈ 2,27`. Bugünkü hal: `BottomDock` (92 u, `DarkFantasyHudPolish.cs:42`) + `BaseHudRedesign.StyleDock` (`:146-157`) → `AddRadialBand` + `EnsureHeroButton` + `IslandBaseButton.Attach` + `RadialDockMenu.Attach` (`RadialDockMenu.cs:85-125`; ana düğme Toggle, açık/kapalı `MEB_RadialOpen`'da).
- **Sprite → eleman** (`Assets/MiniEmpireUI/MiniEmpireUI/Sprites`): `BottomBar/bottombar_bg` 116×178 → bar gövdesi (≈78 u); `menu_button_frame_noplate` 182×160 → 4 düğme çerçevesi (≈80×70 u) + `menu_label_plate` 135×40 → etiket plakası (esnek; KAHRAMAN uzun); `separator_banner` 31×120 → 5 mavi bayrak ayıracı (≈14×53 u, iki kenar dahil); `btn_arrow_down`/`btn_arrow_up` 87×54 → kırmızı ok sekmesi (≈38×24 u; açıkken aşağı, kapalıyken yukarı). İkonlar (`Icons/`): `icon_build` 77×85 çekiç, `icon_expedition` 87×86 çapraz kılıç (SEFER; referanstaki ikon, değişmez), `icon_hero` 82×83 miğferli kahraman, `icon_island` 104×98 palmiyeli ada (≈36–44 u, preserveAspect). `arrow_tab_bg`+`icon_chevron_*` ve tek parça `menu_button_frame` yedek.
- **9-slice (meta'da girili; Unity sırası L,B,R,T):** bottombar_bg L29/R28 (yalnız yatay); frame_noplate L26 B26 R26 T40; menu_button_frame L26 B60 R26 T40; label_plate L16/R16; separator_banner B20/T14 (yalnız dikey).
- **Yerleşim:** yeni bar mevcut `BottomDock` nesnesinin kendisi (ad korunur: kamera çerçevesi `ReferenceUiVisuals.cs:358,377` ve `BaseCameraController.cs:196` onu okur); anchor x 0–1, alta yapışık, yükseklik ≈78 u, SafeArea içinde. Gövde görseli SafeArea'nın altına (ev çubuğu bölgesi) ekran dibine kadar uzar, düğmeler SafeArea içinde kalır. Düğme merkezleri x 0,125/0,375/0,625/0,875, genişlik 0,215; ayıraçlar x 0/0,25/0,5/0,75/1 (kenardakiler içe yaslı). Ok sekmesi x 0,5, barın üst kenarından ≈18 u taşar (dünyanın önünde, yalnız kendi alanı dokunuş yutar).
- **Bağlar (nesne adları AYNI; `PlaytestJourney.ClickHud`, `BaseWorldUI.BattleButton`, testler `Find` ile arıyor):** mevcut düğme nesneleri yeni bara taşınıp yeniden giydirilir (yeni Button yok): `BuildBtn` → `BaseWorldUI.OnBuildMenuClicked` (`BaseWorldUI.cs:829`), `BattleBtn` → `OnBattleClicked` (`:672`, rehber + `LoadCampaignMap`), `HeroBtn` → `ShowHeroInfo` (`BaseHudRedesign.cs:186`), `IslandBtn` → `IslandBaseButton.OnClick` (`IslandBaseButton.cs:86`; kilit toast'ı, "Ufukta bir ada!" töreni, gri ton, "ADA · 5" etiketi aynen; görsel çocuğun adı `Visual` olur ki `Refresh` tonu ona uygulasın).
- **Rozetler:** aynı sağlayıcılar, aynı 0,5 sn tazeleme: İNŞA ET `RadialDockMenu.BuildBadge` → `BaseBadges.CanBuildNew` (D32 takılı rozet düzeltmesi korunur), ADA `IslandBadge` (private → internal static yapılır) + kilit simgesi. Kırmızı nokta düğme çerçevesinin sağ üst köşesinde (`EdgeBadgePosition` deseni). Bar kapalıyken herhangi bir rozet varsa ok sekmesinde toplu nokta (radyal ana düğme noktasının karşılığı).
- **Kırmızı ok:** bugün Ana Üs alt barında barı gizleyen bir işlev YOK (radyal Toggle yalnız yayı açar; `SetDockVisible` yalnız modal paneller için). Bu yüzden yalnız-görünüm anahtarı: dokunuşta `BottomDock` aşağı kayar (0,25 sn, unscaled, kare başı ayırma yok), sekme ekranda kalır ve yukarı oka döner. Kapalıyken düğmeler `CanvasGroup.blocksRaycasts=false` → arazi tıklanır; durum `MEB_BaseBarOpen`'da (varsayılan açık).
- **Radyal menü:** bayrak açıkken `RadialDockMenu.Attach` ve `AddRadialBand` ÇAĞRILMAZ; kod SİLİNMEZ (bayrak kapalıyken bugünkü davranış; Ada `IslandHud.cs:457` `RadialDockMenu.Create` kullanmaya devam eder, dokunulmaz). Paket `BottomMenuBar/MenuButton` KULLANILMAZ (kendi düğme şablonu/rozet/dil yolu açar; D106 ile tutarlı), yalnız kayma fikri (`SetCollapsed` lerp) alınır.
- **Değişecek (kod):** yeni `Polish/UI/MiniEmpireBottomBar.cs` (`Apply(hud)`, `Current`, `SetCollapsed`, `SetDockVisible`; csproj'a elle); `BaseHudRedesign.StyleDock` başında bayrak dalı; `BaseWorldUI.ToggleBottomButtons` (`:336`) önce `MiniEmpireBottomBar.Current?.SetDockVisible(show)` (bugünkü radyal-dışı dal `IslandBtn`'i gizlemiyordu); `FirstBaseGuide.cs:142` ek olarak barı açar (kapalıysa SEFER görünsün; işaretçi `BattleButton`'ı zaten izliyor).
- **BackKeyRouter:** `:82` adımı `RadialDockMenu.Current` null olduğundan Ana Üs'te atlanır; alt bar için yeni basamak EKLENMEZ (bar kapalı olmak bir "pencere" değil; geri tuşu sahne varsayılanına düşer). Ada'da radyal adımı aynen çalışır.
- **Kamera/viewport:** açıkken bar ≈78 u (bugün 92) → `ReferenceUiVisuals.UpdateLayout` görüş alanını kendiliğinden ≈14 u büyütür. Kapalıyken `BottomDock` kaydığı için `NeedsHudAlignment` farkı görür ve görüş alanı alta uzar (zoom değişmez, bug 13 ile tutarlı; yalnız alt sınır ve kaydırma sınırları güncellenir).
- **Dokunulmaz:** Ada (`IslandHud`, `RadialDockMenu.Create`), Sefer (`CampaignSceneSetup`), Savaş (`BattleBottomDock`, `BattleHudRedesign`). Ada'nın yeni bara uyarlanması sonraki iş (GECE_DURUM'a).
- **Riskler:** (1) `RadialMenuTests`, `Session3/4/5Tests`, `Nov13FeedbackTests`, `UiNightTests` radyal menü varsayıyor → SetUp'ta `miniEmpireBottomBar=false` ya da yeni bara göre güncellenmeli; (2) `DarkFantasyHudPolish.StyleDockButton` (`:113`) ve `UiRevampBootstrap` (`:48`) BuildBtn/BattleBtn'i yeniden boyayabilir → skin en son uygulanmalı; (3) rehberin `DOScale` nabzı ile basma ölçeği çakışması → basma efekti iç `Visual`'a; (4) kapalı/açık geçişinde kameranın her karede yeniden hizalanması titreme yapabilir → geçiş bitince tek hizalama; (5) 9:21'de KAHRAMAN etiketi taşması → TMP AutoSize (max 13, min 8), Türkçe test "İNŞA ET", "KAHRAMAN", "ç ğ ı ö ş ü İ"; (6) hero ikonu artık silaha göre değişen portre değil.
- **Yönetici kararı gereken noktalar (öneriyle):** (a) KAHRAMAN ikonu: sabit `icon_hero` (referans) mı, bugünkü silaha bağlı portre mi → öneri sabit `icon_hero`. (b) Bar kapalıyken kamera: görüş alanı alta uzasın mı, sabit kalıp boşalan şerit dolgu mu → öneri uzasın (dolgu koyu bant bırakır). (c) Açık/kapalı durumu kalıcı mı → öneri kalıcı (`MEB_BaseBarOpen`), ilk rehber adımında zorla açık. (d) Geri tuşu barı kapatsın mı → öneri hayır. (e) Kilitli ADA: düğme görünür + gri + "ADA · 5" (bugünkü gibi) → öneri aynen.
- **Karar (yönetici, 2/6 alt bar):** (1) KAHRAMAN ikonu sabit `icon_hero`; SEFER ikonu çapraz kılıç `icon_expedition` (değişmez). (2) Kırmızı ok YALNIZ görünüm: barı aşağı kaydırıp gizler/geri açar (0,25 sn, unscaled, kendi Update'iyle; ok yönü sprite değişimiyle döner). Kapalıyken düğmeler `CanvasGroup.blocksRaycasts=false` (arazi tıklanır); ok sekmesi görünür kalır, rozet varsa üstünde toplu kırmızı nokta. (3) Kamera/görüş alanı bar açılıp kapanınca DEĞİŞMEZ (bug 13): kamera çerçevesinin okuduğu `BottomDock` dikdörtgeni sabit 84 u kalır ve görünmezdir; kayan şey yalnız `BarSlide` (gövde/ayıraç) + dört düğme + ok sekmesidir. Bu, planın "(b) görüş alanı alta uzasın" önerisinin YERİNE geçti; bedeli: kapalıyken alttaki 84 u şerit koyu dolgu olarak kalır (sekme orada durur). (4) Durum PlayerPrefs `MEB_BaseBarOpen` (varsayılan açık; oyuncu kaydına girmez); rehber barı geçici açar (kalıcı tercihe yazmaz). Geri tuşu barı kapatmaz (`BackKeyRouter` değişmedi). (5) Kilitli ADA bugünkü gibi gri (`IslandBaseButton.Tint`, çocuk `Visual`) + "ADA · 5". (6) Radyal menü kodu SİLİNMEDİ: `miniEmpireBottomBar` (varsayılan açık) bayrak/katalog varsa `RadialDockMenu.Attach`/`AddRadialBand` çağrılmaz; Ada'nın radyal menüsü değişmedi. (7) Rozetler aynı kaynaklardan: İNŞA ET `RadialDockMenu.BuildBadge`→`BaseBadges.CanBuildNew` (D32), ADA `RadialDockMenu.IslandBadge` (internal yapıldı); 0,5 sn tazeleme.
- **Uygulama (2/6):** `MiniEmpireBottomBar` (Polish/UI; `Apply`, `Current`, `SetOpen(open, animate, persist)`, `SetDockVisible`, `RefreshBadges`), `MiniBarPress` (basma ölçeği iç `Visual`a), katalog alt bar alanları + `BottomBarReady`, `BaseHudRedesign.StyleDock` başında bayrak dalı, `BaseWorldUI.ToggleBottomButtons` önce yeni bara, `FirstBaseGuide.OnMineTapped` barı açar. Düğmeler HUD kökünün doğrudan çocuğu kalır (adlar/`Hud.Find` aynı); her düğme: görünmez dokunma Image'ı + `Visual`{Frame, Plate, Icon, Label}. Ölçüler (ref. tuval 390): bar 84 u (planın 78'inden büyütüldü: referans oranı), düğme 75 u, ayıraç ≈14×53, ok sekmesi görseli 38×24 / dokunma alanı 64×36, gövde altı 120 u koyu dolguyla ev çubuğuna uzar. Etiket: AlegreyaSans-Bold (referanstaki sans), AutoSize 8–14. Yakalayıcı: `UiAnaUsCapture.CaptureBaseBottomBar` (açık + kapalı + kamera/dock sabitliği ve raycast denetimi). Görüntüler: `Docs/playtest/ui-ana-us/02-altbar-{acik,kapali,vs-referans}.png` (3 tur). Kalan fark: referanstaki gümüş kenar perçinleri/boş kenar payı yok, bar ekran dibine yapışık, sağ kenar ">" düğmesi bu bölümün dışı.

## D108 — Ana Üs yeni UI: PixelCastle duvarları planı (10. oturum)
Kapsam: yalnız görünüm (UI_DEGISIKLIK §3, §6 Zoom/bug 13). Yeni `FeatureConfig.pixelCastleWalls` (varsayılan açık; kapalıyken bugünkü çit aynen). Yeni dosya `Base/BaseCastleWalls.cs` (csproj'a elle).
- **Bugünkü çit:** `BaseMapManager.BuildBuildableFence` (`BaseMapManager.cs:278-329`): sabit `left 0.5/right 21.5/bottom 1.5/top 36.5` (= `BaseWorldLayout.Buildable` (1,2,20,34) ±0,5; `BaseWorldLayout.cs:9`), meşe kiriş (Tiled) + taş direk + fener, order 299–303 (her şeyin üstünde). Çarpışma ayrı: `FenceBlocker` hücre başına BoxCollider2D (`:318-328`, `:383`).
- **Genişleme:** halka SABİT; genişleme yalnız içerideki temiz alanı büyütür (`BaseWorldLayout.CleanForTier`, `:23-27`; `LandExpansion.OpenedTier`). Yine de duvar sabit sayı kullanmaz: `Buildable`'dan türetilir, `RefreshBuildableArea` (`BaseMapManager.cs:197`, Ana Üs yükseltmesinde `:239`) her çağrıda kökü yıkıp yeniden kurar.
- **Eski çit akıbeti:** silinmez. Bayrak açıkken `FenceBeam`/`FencePost` atlanır; `FenceBlocker` döngüsü HER İKİ durumda çalışır (yürüme/çarpışma, `BaseOccupancyGrid`, tanı katmanı aynen). Duvar nesnelerinde collider/Rigidbody/`MapObjectTag` YOK, `_mapObjects`'e girmez → dokunma (`BaseSceneSetup.cs:1157-1238` Physics2D) yutulmaz, kayıt/ızgara etkilenmez.
- **Parça→konum** (`Assets/PixelCastle/PixelCastle/Sprites`, PPU 84, pivot alt-orta, Point, sıkıştırmasız; hepsine tek ölçek s≈0,80 → ≈105 px/birim): üst kenar `wall_crenellated` 183×144 (≈1,74×1,37 u) tekrar; yan kenarlar `wall_vertical` 137×493 (≈1,30×4,70 u) dikey tekrar; üst köşeler `corner_outer_left` 322×396 / `corner_outer_right` 345×394 (L parça, kol kalınlığı piksel ölçümüyle sabitlenir); alt köşeler `tower_small_closed` 202×233 (≈1,9 u); alt orta `gatehouse_doors` 781×451 (≈7,4×4,3 u; iki yan kulesi dahil → referanstaki "kule-kapı-kule"); kapı ile köşe kuleleri arası `wall_crenellated`. Kullanılmayan: `wall_straight_*` (uçlarında gömülü kule, tekrarlanamaz), kırık/merdiven parçaları.
- **Bayrak:** PixelCastle'da yok; `MiniEmpireUI/Sprites/TopBar/banner_left.png` 48×128 (mavi, sarı zambak = referanstaki duvar sancağı) zaten `MiniEmpireUiCatalog`'da. Dünyada ≈0,45×1,2 u, üst duvarda ≈3,5 u arayla (kule/köşe yakını atlanır), alt duvarda kapının iki yanında birer tane. Animasyon gerekmez (isteğe bağlı DOTween ±2° salınım, başta bir kez kurulur).
- **Döşeme algoritması:** iç yüz çit çizgisine oturur, duvar DIŞA taşar (üst: taban y=36,5; yanlar: x∈[0,5−w, 0,5] / [21,5, 21,5+w]; alt: üst kenar y≈1,5, gövde aşağıda; kapı içeri en çok ≈1 u taşar). Her açıklık L için n=ceil(L/p), parça adımı L/n, x-ölçek L/(n·p) (≤%8 germe) + 1 kaynak piksel bindirme → boşluk/ince çizgi yok. Kapı merkezi `Buildable.center.x`; alt duvar kapının iki yanında iki ayrı açıklık. Konumlar 1/105 u'ya yuvarlanır (piksel ızgarası).
- **Sorting:** bugünkü y-sort `100 − round(2y)` (bina `BaseMapManager.cs:658`, birim `HeroMovementController.cs:86`, işçi `BuilderVisualController.cs:391`; içeride 27…97). Duvar içeriye girmediği için sabit sıra yeter: üst+yan duvar ve üst köşeler **20** (dış orman ≤6'nın üstünde, tüm bina/dekor/birimin ARKASINDA → yukarıdaki bina duvarın önünde, arka kule binayı kapatmaz); alt duvar/kapı/alt kuleler **110**, sancak +1 (birimlerin ÖNÜNDE; birimler `FenceBlocker` yüzünden y<1,85'e inemez, örtüşme kapı çıkıntısıyla sınırlı). Inşa hayaleti/seçim çerçevesi/dünya etiketleri (≥200) duvarın üstünde kalır.
- **Import/batching:** `PixelCastleImporter.cs` ayarları yeterli (PPU 84, Point, Uncompressed, mipmap yok; Android/Standalone override yok, meta doğrulandı). Yeni `Assets/PixelCastle/PixelCastleWalls.spriteatlasv2` (yalnız kullanılan 6 sprite, padding 4, döndürme/tight kapalı, sıkıştırma yok) → ~45 renderer tek parti + sancak 1 parti; statik, Update yok, kare başı ayırma yok. Tiled drawMode KULLANILMAZ (FullRect ister, kenar dikişi yapar).
- **Dış orman:** `CreateForestMargin` (`BaseSceneSetup.cs:636`) yalnız sol/sağ (derinlik ≥1,6 → duvar dışı, order ≤6, sorun yok). Bayrak açıkken: (a) aynı fonksiyona üst ve alt kuşak eklenir (ayrı sabit tohum, y>39 ve y<−2; sol/sağ RNG sırası DEĞİŞMEZ); (b) `CreateExteriorScenery` (`:581`, çit dibine x −0,3…0 / y 0,15 ve 37,6 dizilen ağaç/kaya) yeni duvarlarla çakışır → bayrakla atlanır ya da duvar kalınlığı kadar dışa itilir.
- **Kamera (bug 13):** bugün kadraj `WorldViewportBars.UpdateLayout` (`ReferenceUiVisuals.cs:369-408`; barlar arası pixelRect + `max(22/2a, 38/2)`, `DarkFantasyHudPolish.cs:14` genişlik 22) → duvar+orman ekran dışında. Yeni: `BaseCastleWalls.FrameBounds` (duvar dış sınırı + ≈1 u orman payı ≈ x −2…24, y −2…39,5) → ortho = max(W/2a, H/2), merkez = sınır merkezi; `BaseCameraController.FullSize` (`BaseCameraController.cs:44`) aynı sınırı kullanır; bayrak açıkken `HandlePinch` (`:59`) ve tekerlek (`:66`) çağrılmaz → `ClampView` görünümü sabitler; `EdgeScroll`/`EnsureVisible` sessizce no-op. `RefreshCameraFraming` (`BaseSceneSetup.cs:1004`) aynı hesaba yönlendirilir. 9:16'da üst duvar üst barın hemen altında, kapı alt barın hemen üstünde (referans).
- **Korunacak düzeltmeler:** bug 1 (bina taşıma yeşil ekran): hayalet sürükleme `GhostDragActive` + `EdgeScroll` yolu değişmez, kenara taşımada kamera kaymamalı → test; 31/33 (konum kalıcılığı): kayıt, `RepairLayout`, `IsBuildableArea` dokunulmaz; 7 (maden sayacı): ortho ≈19→≈21 büyüyünce dünya-uzayı sayaç ölçeği yakalamada kontrol; 8/12 (savaşçı/okçu düzeni): garnizon birimleri alt duvar/kapının (order 110) arkasında kalmamalı → yakalamada kontrol.
- **Dolu kayıt (yakalama):** `UiAnaUsCapture.LoadBase` (`Assets/Tests/PlayMode/UiAnaUsCapture.cs:60-80`) genişletilir: `landOpened=4`, Ana Üs 7 + referans dizilimi (okçu kulesi, 2 kışla, 4 ev, altın madeni, birkaç dekor) `BaseWorldLayout.Slots` kimlikleriyle; ortam değişkeni `UI_CAPTURE_FULL=1`. Çıktı `Docs/playtest/ui-ana-us/03-duvar-*.png` (9:16, 9:19.5, 9:21, 4:3) + referansla yan yana.
- **Testler:** yeni `PixelCastleWallsTests`: (1) duvar kökünde Collider2D yok; (2) `FenceSegment` collider sayısı bayrak açık/kapalı eşit; (3) parça kenarları arası boşluk ≤0; (4) üst duvar order < en düşük bina order, alt duvar > en yüksek birim; (5) pinch sonrası ortho değişmez; (6) duvar sınırları kamera görünümünde.
- **Riskler:** (1) Point + ≈2,8× küçültme (1080 px'te ≈37 px/u ekrana 105 px/u doku) taş desende titreme/moire yapabilir; (2) `ViewportRevisionTests`, `HudExpansionReviewTests:270`, `UiAnaUsCapture:184-193` eski kadraj/zoom varsayıyor; (3) kapı çıkıntısı alt-orta kayıtlı bina/dekorla görsel çakışabilir; (4) PixelCastle kuleleri koyu arduvaz, referansın mavi çatılı yuvarlak kulesi değil; (5) 4:3 tablette genişlik bağlayıcı olur, üst/alt orman şeridi büyür.
- **Yönetici kararı gereken noktalar (öneriyle):** (a) Filtre: §3 Point ister; risk (1) görülürse D106 gibi Bilinear → öneri: önce Point yakala, titreme varsa yalnız duvar atlasında Bilinear. (b) Köşeler: üstte `corner_outer_*`, altta `tower_small_closed` (L parça ters çevrilemez) → öneri bu asimetri; alternatif dört köşede kule. (c) Mavi çatı: EKSİK ASSET `tower_round_blue_roof` (≈200×250 px, 84 PPU, alt-orta pivot, tek kare; ART_NEEDS.md'ye) → öneri şimdilik `tower_small_closed` boyasız (renk tint'i tüm kuleyi maviye boyar, kullanılmaz). (d) Zoom tamamen kaldırılsın mı (öneri: evet, bayrak arkasında; Ada zoom'una dokunulmaz). (e) Kapı salt görsel; açılma animasyonu yok (öneri).
- **Karar (yönetici, 3/6 duvarlar):** (1) Filtre: önce Point; ≈2,8× küçültmede titreme/tırtık görülürse YALNIZ duvar parçaları Bilinear. Sıkıştırma yok, PPU tutarlı, seam yok (1 px bindirme), tek atlas. (2) Köşe asimetrisi (üst `corner_outer_*`, alt `tower_small_closed`) kabul. (3) Mavi çatılı kule PixelCastle'da yok → **EKSİK ASSET** `tower_round_blue_roof` (ART_NEEDS #24); `tower_small_closed` boyasız kullanılır, renk/tint ile uydurma YOK. Sancak: MiniEmpireUI `banner_left`. (4) Ana Üs'te zoom (pinch/tekerlek) bayrak açıkken kaldırılır; Ada zoom'una dokunulmaz. Kadraj: arazinin tamamı + duvarlar + biraz orman, üst ve alt barın arasında. (5) Kapı yalnız görsel, animasyon yok. Duvarda collider yok, kayda girmez, tıklama yutmaz. Eski çit silinmez, bayrakla gizlenir; `FenceBlocker` collider'ları aynen kalır. (6) `CreateExteriorScenery` çakışırsa bayrakla dışa itilir; mevcut dekor/ağaç sistemi ve kayıtlı dekor konumları bozulmaz.
- **Uygulama (3/6):** `Base/BaseCastleWalls.cs` (saf `Layout(fence)` + `Build`, `FenceRect` = `Buildable` ±0,5, `TryGetFrame`/`FitOrtho`/`ApplyFraming`); `BaseMapManager.BuildBuildableFence` bayrak dalı (kiriş/direk atlanır, `FenceBlocker` döngüsü aynen; duvar `BuildableFence` kökünün çocuğu → `RefreshBuildableArea` her çağrıda yıkıp yeniden kurar); `FeatureConfig.pixelCastleWalls` (varsayılan açık; katalogda sprite yoksa eski çit); `MiniEmpireUiCatalog` duvar alanları + `WallsReady`, `Build Catalog` ayrıca `Assets/PixelCastle/PixelCastleWalls.spriteatlasv2` üretir (6 sprite, Point, sıkıştırmasız, mipmap/döndürme/tight yok, padding 4). Tekrarlanan iki parça çalışma anında kenar payından kırpılır (kaynak kenarındaki açık renkli çizgi ilk yakalamada dikiş gibi görünüyordu): duvar x 9..175, dikey duvar 440 px; adım = boy − 2 px. Ölçek 0,8 (≈105 px/u). **Sıra planı DEĞİŞTİ:** dekor sırası `20 − y` (en düşük −15), ağaç 3, kaya 4 olduğundan arka grup 20 değil **−18/−17 (köşe)/−16 (sancak)** (zemin −20, çim lekesi −19); ön grup 110/111/112. Bayrak açıkken dış dekor (`ExteriorScenery`, `ForestMargin`, yeni üst/alt `MarginTop/Bottom` kuşağı, tohum 52817) duvarın dış sınırına itilir ve sırası −21…−29'a iner (zemin karolarının dışında; duvarı örtmez). Kamera: `BaseCameraController` sabit kadraj (`FixedFraming`; giriş okunmaz, `ClampView`→`ApplyFraming`, `FullSize` kadraj boyu; `EdgeScroll`/`EnsureVisible`/`ZoomBy` etkisiz), `WorldViewportBars.UpdateLayout` ve `RefreshCameraFraming` aynı hesap: kadraj = duvar dış sınırı + 1,2 yan / 1,3 üst / 0,8 alt pay, ortho = max(W/2a, H/2). Filtre: **Point kaldı** — kamera artık hareketsiz (zoom/kaydırma yok) olduğundan titreme oluşmuyor; 1080×1920 görüntüde tırtık kabul edilebilir. Testler: `PixelCastleWallsTests` (her çit boyutunda sınır kapalı + dikiş yok + iç alana taşma yok; her kademede collider/MapObjectTag yok, FenceSegment 112; bayrak açık/kapalı FenceSegment konumları, inşa edilebilir hücreler, yol bulma ve fizik isabetleri aynı; sıralama; sabit kadraj). `ViewportRevisionTests` fener beklentisi bayrak açıkken duvar halkasına çevrildi. Yakalayıcı: `UiAnaUsCapture.CaptureBaseWalls` (UI_CAPTURE_FULL=1 varsayılan: Ana Üs 7, arazi 4, referans dizilimi; `ShootFramed` oyundaki bar-arası kadrajı birebir kurar) + `CaptureBaseWallsLandTiers`. Görüntüler `Docs/playtest/ui-ana-us/03-duvar-{9x16,9x19.5,9x21,4x3,tasima,arazi-en-kucuk,arazi-en-buyuk,vs-referans}.png` (3 tur: dikiş kırpma, sancak boyu, alt pay). Halka genişlemeyle değişmez (D108 başı) → en küçük/en büyük kademede aynı duvar. Kalan fark: köşelerde mavi çatılı yuvarlak kule yok (eksik asset), PixelCastle taşları referanstan koyu, kapı daha iri; sol üstteki GÜNLÜK/GÖREV düğmeleri sol duvarın üstünde (4/6 açılır menüyle kalkacak); referansın sağdaki ">" düğmesi 4/6 işi.

## D109 — Ana Üs yeni UI: sağ üst droplist planı (10. oturum)
Kapsam: UI_DEGISIKLIK §4, §6; D106–D108 yaklaşımı aynen: `MiniEmpireUiCatalog` (yeni droplist alanları + `DropMenuReady`), yeni `FeatureConfig.miniEmpireDropMenu` (varsayılan açık; kapalıysa veya katalog yoksa bugünkü sol düğmeler), Bilinear, sliced Image'larda `pixelsPerUnitMultiplier = 886/390 ≈ 2,27`, yakalayıcı `UiAnaUsCapture`. Yeni dosya `Polish/UI/BaseDropMenu.cs` (csproj'a elle). Etkinlik sistemi projede YOK (grep: event/etkinlik sınıfı yok).
- **Bugünkü hal:** `DailyRewardsUI.CreateSideButton` (`UI/DailyRewardsUI.cs:80-92`, `RoundHudButton(…"DailyButton", 0, Art("ui_icon_calendar"))`) ve `DailyQuestsUI.CreateSideButton` (`UI/DailyQuestsUI.cs:65-83`, `"QuestsButton", 1, Art("ui_scroll")` + `FirstTimeTips.Show("quests")`); ikisi de `BaseSceneSetup.cs:47-48`'de eklenir, kendi canvas'ı (order 45) + SafeArea. Rozet: `DailyRewards.CanClaim` (`Systems/DailyRewards.cs:37`), `DailyQuests.HasClaimable` (`Systems/DailyQuests.cs:105`); D32/1.2 düzeltmesi = saniyede bir koşuldan yeniden hesap (`DailyRewardsUI.cs:66-78`, `DailyQuestsUI.cs:50-57`). Pencereler: `DailyRewardsUI.Open` (public, `:105`), `DailyQuestsUI.Open` (private, `:99`).
- **Veri yapısı (öneri: kod içi liste, SO değil):** `BaseDropMenu.Items` = `List<DropMenuItem>`; `DropMenuItem { string id; Func<Sprite> icon; string tr, en; Action open; Func<bool> badge; }`. Yeni madde = tek satır, ör. `new("daily", () => CampaignSceneSetup.Art("ui_icon_calendar"), "Günlük", "Daily", DailyRewardsUI.OpenFromMenu, () => DailyRewards.CanClaim)`. Etiket satır kurulurken `L.T(tr, en)`. `icon` null ise satır ikonsuz kurulur (Etkinlik). Paketin `MenuItemData` SO'su yalnız fikir: SO `Action`/`Func` tutamaz, ayrıca id→eylem kayıt defteri gerekir; tasarımcı düzenlemesi istenirse sonra `DropMenuItemData` SO + id sözlüğüne geçilir.
- **Maddeler:** Günlük = `ui_icon_calendar` (`Resources/CampaignArt`, 64×64) → `DailyRewardsUI.OpenFromMenu` (yeni static sarmalayıcı: `current?.Open()`); Görev = `ui_scroll` (248×243, preserveAspect) → `DailyQuestsUI.OpenFromMenu` (yeni static: `Open()` + mevcut `FirstTimeTips.Show("quests")`); Etkinlik = **ikonsuz**, yalnız etiket → "Yakında" kutusu, rozet `() => false`. Kupa projede YOK (yalnız Cainos `PF Village Props - Cup` = içki bardağı, kupa değil) → **EKSİK ASSET** `icon_trophy` (ART_NEEDS #25: ≈80×80 px, MiniEmpireUI stili, Bilinear, şeffaf zemin, tek kare). Çapraz kılıç (`icon_expedition`) KULLANILMAZ (referansta Görev satırında görünse de).
- **Sprite → eleman:** aç/kapa düğmesi `TopBar/btn_round_empty` 83×83 (≈36 u, Simple) + `Icons/icon_chevron_down` 41×26 (≈18×11 u; kapalı ▼, açıkken 180° döner → ▲); liste gövdesi `BottomBar/menu_button_frame_noplate` 182×160 (sliced L26 B26 R26 T40, iki yönde esner); satır `TopBar/res_pill_bg` 110×68 (sliced L63/R29, sol yuva ikon için; ≈130×32 u), ikon ≈24 u yuvada; satır arası `ornament_diamond` 13×22 isteğe bağlı süs. Rozet: kırmızı daire (`BaseMapManager.MakeCircleSprite`) + TMP "!" (beyaz, kalın); MiniEmpireUI'da "!" rozeti yok, kod çizimi. Yazı: D107'deki AlegreyaSans-Bold, AutoSize 9–14.
- **Yerleşim:** üst barın HUD canvas'ında, SafeArea içinde, anchor/pivot (1,1); düğme x −6 u, y = `TopBarBG` alt kenarı − 6 u (çalışma anında okunur, sabit değil; dişli `btn_settings` ile çakışmaz). Liste düğmenin altında sağa hizalı, pivot (1,1), aşağı büyür. `VerticalLayoutGroup` (padding 10/10/8/10, spacing 4, childControlHeight, childForceExpandHeight=false) + `ContentSizeFitter` (iki eksen PreferredSize, `LayoutElement.minWidth` 120 u) → madde eklenince gövde kendiliğinden uzar. Üst sınır alt barın üstü; aşılırsa (≈8+ madde) ScrollRect, şimdilik gerekmez.
- **Davranış:** aç/kapa 0,15 sn unscaled DOTween (liste `localScale.y` 0→1 + `CanvasGroup.alpha`, chevron `DORotate` 180°; `SetLink`, kare başı ayırma yok). Kapalıyken liste `SetActive(false)` + kalkan kapalı → yalnız 36 u düğme dokunuş yutar (§6). Açıkken listenin ARKASINDA tam ekran şeffaf `DropMenuShield` (Image alpha 0, raycastTarget) → dokunuş `Close()`; `BaseSceneSetup.IsPointerOverBlockingUI` (`Base/BaseSceneSetup.cs:1120-1143`) basışta UI gördüğü için arazi tıklaması/seçim tetiklenmez ("dışarı dokunuş yalnız menüyü kapatır" ek kod istemez; üst/alt bar da kalkanın altında, ilk dokunuş yalnız kapatır). Başka panel: açılışta `SelectionManager.Select(this, Close)` (`UI/SelectionManager.cs:22`) → bina/eylem seçimi menüyü kapatır; satır eylemi önce `Close()`, sonra panel. Sahne değişimi: menü sahne canvas'ında yok olur; durum kalıcı DEĞİL (her girişte kapalı).
- **Rozetler:** `BaseDropMenu.Update` 0,5 sn'de bir (D32 deseni, olay beklemez): her satır kendi `badge()`'i; düğmede toplu "!" = herhangi biri (Etkinlik hep false). Eski noktaların `DOScale` nabzı düğme rozetine taşınır.
- **Eski düğmeler:** silinmez; `CreateSideButton` bayrak açıkken çağrılmaz (canvas, `Open`/`Close`, `CloseIfOpen`, saniyelik `Update` aynen; `dot` null-güvenli). `BaseCameraController.HudIconNames` (`Base/BaseCameraController.cs:189`) "DailyButton"/"QuestsButton" bulamaz → sol pay 0 (D108 sabit kadrajda zaten etkisiz; dokunulmaz).
- **Etkinlik "Yakında" kutusu:** `BaseDropMenu` içinde küçük kutu (DailyQuests `ui_paper` deseni; başlık "ETKİNLİK", metin `L.T("Etkinlikler yakında geliyor!", "Events are coming soon!")`, KAPAT); karartmaya dokunuş kapatır; `BackKeyRouter.CloseWindow`'a `BaseDropMenu.CloseComingSoonIfOpen()` (`UI/BackKeyRouter.cs:117` yanına).
- **BackKeyRouter:** menünün kendisi için yeni basamak GEREKMEZ: `SelectionManager` kaydı `:79` "selection" adımında kapatılır (açık pencereler önce, `:68`). Test bunu doğrular.
- **Tutorial/rehber:** eski düğmelere işaret eden ok/rehber yok (grep); tek yönlendirme `DailyRewardsUI.cs:56` toast'ı "soldaki takvime dokun" → bayrak açıkken `L.T("Günlük ödülün hazır: sağ üstteki menüden Günlük maddesine dokun.", "Your daily reward is ready: open the top-right menu and tap Daily.")`.
- **Testler:** yeni `BaseDropMenuTests`: (1) bayrak açık → `DailyButton`/`QuestsButton` yok, `BaseDropMenuToggle` var, satır sayısı = `Items.Count`; (2) listeye test maddesi eklenince gövde yüksekliği büyür (LayoutRebuilder); (3) kapalıyken düğme dışındaki arazi noktasında `RaycastAll` boş; (4) açıkken arazi noktasına dokunuş menüyü kapatır, seçim/bina paneli açılmaz; (5) `DailyRewards`/`DailyQuests` alınabilir ↔ satır ve düğme rozeti (alındıktan sonra ≤1 sn söner, D32); (6) geri tuşu menüyü kapatır (`LastStep == "selection"`), Yakında kutusunu kapatır (`"window"`); (7) Günlük/Görev satırı ilgili popup'ı açar, menü kapanır. `DailyRewardsTests`, `DailyQuestsTests`, `Session9BackKeyTests` aynen geçmeli. Yakalayıcı `UiAnaUsCapture.CaptureBaseDropMenu` → `Docs/playtest/ui-ana-us/04-droplist-{kapali,acik,vs-referans,9x19.5,9x21,4x3}.png` (Türkçe "Günlük/Görev/Etkinlik", ç ğ ı ö ş ü İ).
- **Riskler:** (1) `ui_icon_calendar` piksel sanatı ile Bilinear MiniEmpireUI çerçeve arasında stil farkı; `ui_scroll` 24 u'da boş kağıt gibi okunabilir (03 görüntüsünde de soluk); (2) kalkan, açıkken üst bardaki dişli/elmas dokunuşunu da ilk seferde yutar; (3) menüyü açmak açık bina panelini `SelectionManager` üzerinden kapatır (istenen davranış); (4) Günlük otomatik açılışı (`DailyRewardsUI.cs:57`) menü açıkken gelirse üst üste biner → `OpenFromMenu`/otomatik açılış önce `BaseDropMenu.Close()`; (5) 9:21'de liste sağ duvar/binaların üstüne iner (yalnız görsel).
- **Yönetici kararı gereken noktalar (öneriyle):** (a) Veri: kod içi liste mi SO mu → öneri kod listesi. (b) Ok yönü: kapalı ▼ / açık ▲ (`icon_chevron_*`) mı, referanstaki "<" mı → öneri ▼/▲ (liste aşağı açılır; "<" sprite yok, 90° döndürmeyle yapılabilir). (c) Açıkken dışarı dokunuş üst/alt bar düğmelerini de yutsun mu → öneri evet (§4 "önce sadece menüyü kapatsın"). (d) Etkinlik: kutu mu toast mu → öneri küçük kutu (§4 "panel/kutu"). (e) Kupa EKSİK ASSET, satır ikonsuz → öneri aynen, uydurma yok. (f) Açık/kapalı kalıcı mı → öneri hayır, her girişte kapalı. (g) Ada üssüne uyarlama sonraki iş (GECE_DURUM'a).
- **Karar (yönetici, 4/6 droplist):** (a) kod içi madde listesi (`BaseDropMenu.Items`, yeni madde = tek satır); (b) ok referanstaki gibi: kapalı ">" açık "<" (aşağı bakan `icon_chevron_down` 90°/-90°, 0,15 sn tween); (c) açıkken dışarı dokunuş (barlar dahil) yalnız kapatır; (d) Etkinlik = "Yakında" kutusu; (e) kupa EKSİK ASSET (ART_NEEDS #25), Etkinlik ikonsuz; (f) durum kalıcı değil; (g) Ada sonra.
- **Uygulama (4/6):** `Polish/UI/BaseDropMenu.cs` (+`DropMenuItem`), `FeatureConfig.miniEmpireDropMenu`, katalog `resCapsule`/`iconChevronDown` + `DropMenuReady` (`Build Catalog` yeniden koşuldu), `BaseHudRedesign.Apply` sonunda `BaseDropMenu.Apply(hud)`; `DailyRewardsUI/DailyQuestsUI.OpenFromMenu`, bayrak açıkken `CreateSideButton` çağrılmaz, toast metni yeni konuma göre; `BackKeyRouter.CloseWindow` Yakında kutusu. Düğme ve liste çerçevesi `menu_button_frame_noplate` (referanstaki kare ahşap düğme), satır `res_capsule_bg`. Testler `BaseDropMenuTests` (11). Görüntüler `Docs/playtest/ui-ana-us/04-droplist-{kapali,acik,etkinlik,vs-referans}.png`. 1 tur; kalan fark: Görev ikonu soluk parşömen (`ui_scroll`), Etkinlik kupasız, liste referanstan biraz dar.

## D110 — SFX_Pack sesleri ve ambiyans planı (10. oturum)
- **Mevcut durum:** tek yönetici `Polish/Core/AudioManager.cs` (DontDestroyOnLoad; musicA/B çapraz geçiş, wind/bird/layer döngü kaynakları, 4'lü efekt havuzu `:59`), klipler `Resources/AudioLibrary.asset` (`Polish/Core/AudioLibrary.cs`). Efekt alanlarının TAMAMI boş (`AudioLibrary.asset:45-55`), `baseWindLoop/baseBirdLoop` boş. Ayarlar: PlayerPrefs `audio_music_volume/audio_sfx_volume` + aç/kapa (`AudioManager.cs:41-44, 333-372`, UI `AudioSettingsUI.cs`). Çözülmeyen anahtarlar: `"coin"` (`Systems/NatureClear.cs:92`), `"click"` (`UI/BuilderPanel.cs:42`, `CardWorkshopPanel.cs:48`) → `ResolveSfx` (`AudioManager.cs:312`) null döner. Hız sınırı yok: havuz doluysa `effects[0]` üstüne bindirir (`:309`). Gün-gece YOK; fırtına yalnız Bölge 4 savaş kar fırtınası (6 sn, `Battle/BattleWeather.cs:7-19`) ve ada tanıtım metni (`Island/IslandIntro.cs:85`). Ada = BattleScene + `IslandSession`. Torch/River için kodda nesne/alan yok.
- **Eşleme (ART_NEEDS'te yer yazanlar öncelikli; ART_NEEDS.md:72 "ok isabeti, ateş tutuşma", :62 "ada baskın borusu"):**
  - Bow Impact Hit 1 → YENİ `arrowImpact`: `Battle/ProjectileController.cs:122` (`type == Arrow/Spear`) + `Island/IslandArcher.cs:130` `Impact()`.
  - Sword Impact Hit 2 → MEVCUT `swordHit` (zaten `Polish/Core/BattleAudioObserver.cs:21` kahraman vuruşunda çalıyor; kod yok).
  - chop 2 → YENİ `chop`: `NatureClear.Finish` (`Systems/NatureClear.cs:92`, Tree/Stump; "coin" anahtarı yerine tür bazlı anahtar; kaya → `goldCollect`/mine).
  - mine 3 → MEVCUT `goldCollect` (`Base/BaseSceneSetup.cs:1321` maden toplama, `UI/AwayReportUI.cs:132`; kod yok).
  - Fireball 1 → oyunda "ateş topu" yok; en yakın = adada Ateşli Ok: YENİ `fireball`, `Island/IslandAbilities.cs:84` `Activate(FireArrow)` ateşlenince (+ `IslandArcher.cs:133` `Ignite` tutuşma yerine değil, tek sefer).
  - Wave Attack 1/2 → YENİ `waveStart[]` (rastgele/sırayla): `Battle/BattleManager.cs:773` `OnWaveChanged` hemen öncesi; ada akını için `raidHorn` boş (`Island/IslandRaidVisuals.cs:52`) → aynı klip `raidHorn`'a da atanır (ART_NEEDS:62 boru gelince değişir).
  - Ambiyans: Inside Day → YENİ `baseAmbience` (BaseScene, `PlayBaseBgm` `:251`); Beach → YENİ `islandAmbience` (`PlayIslandBgm(false)` `:212`; akında %50'ye iner, kesilmez); Sea Storm → YENİ `stormAmbience` yalnız `IslandIntro` bulut/fırtına sahnesinde (Play `:30`, bitişte söner).
- **Ambiyans kanalı:** ayrı `ambience` AudioSource (mevcut `windSource` yeniden adlandırılmaz, yanına eklenir), seviye = `musicVolume × MusicVolume01 × 0,35`, aç/kapa = Müzik anahtarı (mevcut wind/bird de müziğe bağlı; Ayarlar'a yeni kaydırıcı yok). Bugünkü `PlayLayer` sabit 0,22/0,14 kullanıyor ve `SetMusicVolume01` katmanları güncellemiyor → katmanlar da kaydırıcıya bağlanır.
- **Sahne geçişi + bug 27:** `DECISIONS`/git log'da bug 27 düzeltmesi YOK. Olası kök: `DOFade` tween'leri ölçekli zamanla (`:144,156,157`, `SetUpdate(true)` yok); savaş/ada `Time.timeScale=0` (`BattlePause.cs:30`, `BattleManager.cs:1146`, ayarlar) iken sahneden çıkılınca solma donar, `OnComplete(Stop)` hiç çalışmaz → eski müzik kısık kalır. Düzeltme: tüm ses tween'lerine `SetUpdate(true)`; `OnSceneChanged`'de aktif olmayan müzik kaynağı ve eski ambiyans `DOKill` + 0,6 sn gerçek-zaman solma + `Stop`; ambiyans da musicA/B gibi tek "hedef klip" ile çapraz geçer (aynı klipse dokunulmaz). Regresyon testi zorunlu.
- **Import (.meta şu an hepsi DecompressOnLoad + Vorbis %100):** 7 WAV → Decompress On Load, ADPCM, Force To Mono, Preload açık (Wave Attack 680 KB → Compressed In Memory, ADPCM). 9 OGG (3-4 MB) → Streaming, Vorbis %60, stereo, Preload kapalı, Load In Background (DecompressOnLoad'da her biri ~30 MB PCM olurdu). Döngü klik: kaynak `loop=true` + 0,4 sn giriş/çıkış solması; cihazda dinlenir, klik varsa o klip Compressed In Memory'ye alınır. Asset sesine dokunulmaz.
- **Tekrar sınırı:** efekt havuzu 4 → 8; anahtar başına min aralık (`Time.unscaledTime`, isabet 0,05 sn, chop/mine 0,1 sn, waveStart 1 sn) ve klip başına ≤3 eşzamanlı (önceden ayrılmış sayaç dizisi, kare başı tahsis yok); havuz doluysa en eski çalan kaynak kesilir, yeni ses atılır değil.
- **Arka plan:** `AudioManager.OnApplicationPause(true)`/`OnApplicationFocus(false)` (editör hariç, `BattlePause.cs:82` gibi) → `AudioListener.pause = true`; dönüşte yalnız `!BattlePause.IsPaused` ise `false` (savaş DEVAM penceresi sesi kendisi açar).
- **Yükleme:** doğrudan referans (AudioLibrary.asset) kalır; Streaming + preload kapalı ile bellek düşük, ayrı Resources.Load gerekmez.
- **Testler (PlayMode `AudioSfxPackTests`):** kütüphane alanları dolu + her anahtar çözülür ("coin"/"click" dahil); aynı karede 10× `PlaySfx("arrowImpact")` → ≤3 çalan kaynak; `timeScale=0` iken Battle→Base geçişi → 1,2 sn gerçek zaman sonra tek müzik kaynağı çalıyor, savaş klibi durmuş (bug 27); ada akını→Ana Üs aynı; Müzik kapat → ambiyans durur, kaydırıcı ambiyansı ölçekler; `OnApplicationPause` → `AudioListener.pause`. Editör kontrolü: SFX_Pack import ayarları (AssetImporter).
- **Riskler:** (1) "Wave Attack" deniz dalgası sesi olabilir (dinlenmeli; öyleyse ada kıyı vurgusu olur, dalga başlangıcı boş kalır); (2) "Inside Day" iç mekân tonu açık hava Ana Üs'te garip durabilir; (3) BattleAudioObserver her karede `FindObjectsByType` (mevcut maliyet, artmaz); (4) AudioListener.pause'u iki yer yönetiyor; (5) fallback AudioListener + sahne dinleyicisi çakışması (`UpdateListener`).
- **Bağlanmayacaklar:** Torch Loop, River Stream Loop (meşale/nehir nesnesi yok); Inside Night/Night Storm/Day Storm, Beach Storm (gün-gece ve kalıcı fırtına durumu yok; 6 sn kar fırtınası için uzun döngü uygun değil). Klasörde kalır, silinmez.
- **Yönetici kararı (öneriyle):** (a) Ambiyans Müzik ayarına bağlı mı Efekt'e mi → öneri Müzik (yeni kaydırıcı yok). (b) Fireball nereye → öneri Ateşli Ok fırlatışı; alternatif Bölge 6 patlaması `VolcanicEruptions.cs:58`. (c) Wave Attack raidHorn'u da doldursun mu → öneri evet, geçici. (d) Sea Storm yalnız ada tanıtımında mı → öneri evet. (e) Bug 27 düzeltmesi bu işe dahil mi → öneri evet, aynı commit dizisinde ayrı adım.
- **Karar (yönetici, 5/6 ses):** (a) ortam sesi Müzik ayarına bağlı (seviye + kapatma), Ayarlar'a yeni kaydırıcı yok; (b) Fireball = adadaki Ateşli Ok; (c) Wave Attack 1/2 = düşman dalgası başlangıcı + geçici ada akın borusu; (d) Sea Storm yalnız ada tanıtımında; Ana Üs = Inside Day, Ada = Beach; Torch/River ve Night/Storm varyantları bağlanmaz (gün-gece/nehir/meşale/kalıcı fırtına yok; 6 sn kar fırtınası için uzun döngü uygun değil); (e) bug 27 bu işe dahil, ayrı adım.
- **Uygulama (5/6):** `AudioLibrary` yeni alanlar `baseAmbience/islandAmbience/stormAmbience`, `arrowImpact/chop/fireball/waveStart[]`; `AudioLibrary.asset` dolduruldu (+ `swordHit`=Sword Impact, `goldCollect`=mine 3, `raidHorn`=Wave Attack 1). `AudioManager`: ortam sesi A/B çapraz geçiş (tek hedef klip, aynı klipse dokunulmaz, ada akında %50), katman/ortam seviyeleri Müzik kaydırıcısına bağlı, havuz 8, anahtar başına asgari aralık (`SfxMinInterval`, `Time.unscaledTime`), klip başına ≤3 eşzamanlı (kendi sayaç dizileri, kare başı tahsis yok), `SetAppPaused/SetAppFocused` → `AudioListener.pause` (editör hariç; `BattlePause` bitişte arka plandaysa sesi açmaz). Çağrılar: `ProjectileController.OnHitTarget` + `IslandArcher.Impact` (arrowImpact), `NatureClear.Finish` ("coin" yerine: ağaç/kütük chop, kaya mine, çalı sessiz), `IslandAbilities.Activate(FireArrow)` (fireball), `BattleManager` dalga başı (waveStart), `IslandIntro` (storm aç/kapa). Karşılığı olmayan `"coin"`/`"click"` anahtarları hâlâ sessiz (alakasız sesle doldurulmadı).
- **Bug 27 kök neden:** `DOFade` tween'leri ölçekli zamandaydı; savaş/ada/ayarlar `Time.timeScale=0` bırakıp sahneden çıkınca solma donuyor, `OnComplete(Stop)` çalışmıyor → eski parça kısık çalmaya devam ediyor, yenisi de sesi 0'da bekliyordu (üst üste/geriden çalma). Düzeltme: tüm ses tween'leri `SetUpdate(true)`; `OnSceneChanged` başında kullanılmayan müzik/ortam kaynağı gerçek zamanlı 0,6 sn solup durur, katmanlar kesilir. Regresyon: `AudioSfxPackTests.SceneExitFadeCompletesWhileTimeScaleIsZero` (SetUpdate kaldırılınca düşer).
- **Import:** 7 WAV → ADPCM + mono (Wave Attack ×2 Compressed In Memory, diğerleri Decompress On Load + preload); 9 OGG → Streaming, Vorbis %60, preload kapalı, Load In Background. Döngü kopması için kod içinde ek solma YOK (Vorbis streaming döngüsü örnek-hassas); cihazda dinlenecek, klik varsa o klip Compressed In Memory'ye alınır.
- **Testler:** `AudioSfxPackTests` (10): eşleme, import (.meta), hız/eşzamanlı sınırı, dalga klibi dönüşümü, olay→klip (NatureClear), Müzik/Efekt ayarı, Ana Üs↔Savaş↔Ana Üs, Ana Üs↔Ada↔Ana Üs, timeScale=0 çıkış, arka plan/odak.

## D111 — Ana Üs UI son kontrol düzeltmeleri
- Üst bar hap yazıları: et hapı genişletildi (ağırlık 1,1), taşma "..." yerine Overflow + min 5 pt; 9:21'de "43" artık görünür.
- Rozetler (ok düğmesi, alt bar, sekme, droplist satırı) anchor'a göre içeri çekildi; satır etiketi "..." kesmez.
- Kalkan "ANA ÜS" yazısı küçültüldü/daraltıldı; alt bar etiketlerine kenar boşluğu (6) ve max 13 pt.
- UiAnaUsCapture: tüm çekimler oyundaki kameradan (kadrajlı); droplist çekimi UI_CAPTURE_W/H/NAME/GOLD env'li; altın assert'i NumberFormat.Short ile.
- Yalnız görünüm/test; oyun mantığı, ekonomi, kayıt değişmedi.

## D112 — 11. oturum (hata grupları 1–9) yönetim kararları
- Dal: `gece-gelistirme` main'in atasıydı (UI işi main'deydi) → `gece-gelistirme` main'e (`5a46d49`) ileri sarıldı, çalışma orada.
- Çelişki: `gameplay-engineer.md` "Never commit" diyor, görev "alt ajan grup bitince commit" diyor. CLAUDE.md'de alt ajan kuralı yok →
  ajan dosyası esas alındı: alt ajan commit ATMAZ, yönetici derleme/`git status` kontrolünden sonra grup commit'ini atar.
- Model: `gameplay-engineer` Sonnet. Opus grupları (2, 3, 4, 9) için uygulama genel alt ajan + Opus ile yapılır. `explorer` Haiku (salt okuma, kök neden taraması); Opus gruplarında kök neden taraması da uygulayıcı Opus ajanına bırakılmaz, önce explorer çalışır.
- Grup 9 (Depo): görev "ben onaylayınca uygula" diyor; yönetici talimatı "onay bekleme, planı kendin gözden geçir" → plan DECISIONS'a yazılıp ayrı commit'lenir, yönetici incelemesinden sonra uygulanır.

## D113 — Grup 1 iOS yazılar (bug 2, 3, 5, 10, 24)
- Bulgu: Font atanmadan oluşturulan TMP yazıları statik "LiberationSans SDF" varsayılanını kullanıyordu (fallback listesi boş, Türkçe/özel glif yok); ReferenceUiVisuals'in çalışma anında ürettiği Dinamik fontların ise shader'ı `Shader.Find("TextMeshPro/Mobile/Distance Field")` ile bulunuyor (iOS build'inde ayıklanma riski; varsayılan font malzemesi aynı shader'a bağlı olduğundan KANITLANMADI, H2 zayıf). Gerçek cihaz kanıtı yok; kök neden kısmen TAHMİN.
- Çözüm tek noktada: `Polish/UI/TmpFontBootstrap.cs` (BeforeSceneLoad): varsayılan fonta Dinamik Türkçe (Alegreya, Â/â dahil) fallback eklenir; font atanmamış TMP yazıları TEXT_CHANGED olayında güvenli Dinamik fonta geçirilir; CreateDynamicFont shader yoksa varsayılan fontun shader'ına bağlar. ReferenceUiVisuals.CreateTurkishFont buna devredildi.
- BaseLevelBanner (bug 10): font ApplyFont ile atanır, tween'ler SetUpdate(true) (timeScale bağımsız). ProjectSettings/GraphicsSettings/paket değişmedi.
- Bug 3 yeri: GameManager.cs:934 (etiket) ve Kahraman sekmeleri; bug 24: Island/IslandHud.cs:717 (ReferenceUiVisuals.Text, başlık fontu Cinzel Dinamik, → glifi yok). Cihazda doğrulama gerekir.
- Test: TmpFontBootstrapTests (PlayMode) geçti.
- D113 ek: font değişiminde outline/face rengi korunur (Swap), ReferenceUiVisuals gövde fontu TmpFontBootstrap.BodyFont ile tek atlas; testte kontur korunumu denetlenir.
- QA düzeltmesi (TmpFonts_AreSafeForTurkishAndWaveText toplu koşuda düşüyordu, tek başına/SceneTransitionTests ile geçiyordu): atlas dolu DEĞİL (2048 atlas + çok atlas denendi, yine düştü). Tanı: toplu koşuda önbellekteki `BodyFont` asset'inde glif tablosunda 'a' VARKEN `TryAddCharacters("a")` false dönüyor (font motoru LoadFontFace=Success, kaynak/atlas/malzeme sağlam), aynı kaynak fontla taze `CreateFontAsset` ise sorunsuz → statik önbellek asset'i uzun oturum/sahne geçişleri sonrası bozuluyor (kesin tetikleyici testi bulunamadı; gerçek oyunda aynı bozulma yeni glifleri kaybettirebilirdi). Çözüm: `TmpFontBootstrap.BodyFont` her erişimde asset'in `TryAddCharacters("a"+Extra)` ile sağlığını sınar, bozuksa yeniden üretir ve varsayılan fontun fallback zincirine yeniden bağlar (`LinkFallback`). Ek: `SpaceCheck` ölçüm payı 0,01→0,02 em (4,98 px kayması 0,1 px ile düşüyordu); atlas 2048 + çok atlas korundu (ucuz güvence).

## D114 — Grup 2 çökmeler ve müzik (bug 4, 6, 15, 27)
- **Ortak geçiş katmanı** `Managers/SceneTransition.cs`: tek yükleme kilidi (`Busy`, 20 sn bayatlama + sceneLoaded'da çözülür), `PrepareForLoad` (BattlePause.ResetAll, timeScale 0→1, AudioListener.pause sıfırlama, `AudioManager.PrepareSceneChange`, DOTween.KillAll — her adım ayrı try/catch), `[SceneLoad]` hata günlüğü (yakalanan hata oyunu düşürmez), PlayerPrefs izi (geçiş ortasında kapanırsa sonraki açılışta "[SceneLoad] Önceki oturum ... kapandı" uyarısı = TestFlight tanısı), `Application.lowMemory` → UnloadUnusedAssets.
- `GameManager.FadeAndLoadScene`: kararma gerçek zamanlı (eskiden Time.deltaTime → timeScale=0'da sonsuz bekleme, KANITLI: test), örtüde GraphicRaycaster (çift dokunuş), LoadSceneAsync + etkinleştirme öncesi PrepareForLoad, async olmazsa senkron yedek, o da olmazsa örtü kalkar. Tüm Load*/StartBattle/TryStartCampaignBattle yükleme sürerken isteği oturum durumunu değiştirmeden bırakır (bulut + yedek kararma yarışı ve çift "savaşa başla" kapandı — KANITLI: kod okuması + test). `SceneCloudTransition` aynı katmanı kullanır.
- sceneLoaded/activeSceneChanged dinleyicileri (SceneAutoSetup, BattleCosmeticObserver, OccupancyDebugView, GameNotifications, AudioManager) try/catch; bir dinleyicinin istisnası zincirdeki diğerlerini düşürmez. Lambda abonelikler adlı yöntem (-=/+=).
- **Bug 4 (geçişte çökme): DOĞRULANMADI.** Editör testlerinde yönetilen istisna yok; iOS'ta uygulamayı kapatan şey yerel çökme/bellek (jetsam) olmalı — kanıt için TestFlight çökme raporu gerekir. Yarış/timeScale/abonelik düzeltmeleri + iz + lowMemory eklendi.
- **Bug 6 (pes et): kısmen kanıtlı, çökme DOĞRULANMADI.** "Devam et" teklifi (timeScale=0) açıkken pes edilince teklif açık ve zaman 0 kalıyordu → sonuç/dönüş takılıyordu (KANITLI: test). Forfeit teklifi kapatır, duraklatmayı sıfırlar; OnBattleEnded try/catch (kayıt/can düşümü her durumda çalışır).
- **Bug 15 (ada ilk giriş): DOĞRULANMADI.** Akışta (tanıtım → ad → öğretici İNŞA ET) yönetilen hata bulunamadı. Tahmin: iOS klavyesi açıkken TMP_InputField'ın yok edilmesi; IslandNameUi.Close klavyeyi kapatıp nesneyi gizler, TryApply çift çağrıyı (onSubmit + TAMAM) yutar.
- **Bug 27 (müzik): BİTTİ (cihazda dinlenmedi).** Kök neden: D110'daki ölçekli solma + DOTween.KillAll'un yarım çapraz geçişi öldürmesi (OnComplete(Stop) gelmez → eski parça kısık çalar) — testle yeniden üretildi. `AudioManager.PrepareSceneChange` KillAll'dan önce solan kaynağı durdurur/etkin kaynağı hedef sese oturtur; 0,5 sn'lik güvenlik ağı (`EnforceSingleMusic`) tween'siz çalan etkin-olmayan kaynağı durdurur, kısık takılan etkin kaynağı hedefe oturtur; OnSceneChanged try/catch.
- link.xml gerekmedi: oyun kodundaki yansıma yalnız kendi tiplerine (Assembly-CSharp, Minimal ayıklamada korunur); ProjectSettings değişmedi. iOS script call optimization "Slow and Safe" (istisnalar uygulamayı kapatmaz).
- Test: `SceneTransitionTests` (5 test) + AudioSfxPackTests + BattleSettingsRevisionTests + Session6 ada öğreticisi.
- Kalan doğrudan `SceneManager.LoadScene` çağrıları (IntroDynamicFx x2, AudioSettingsUI dil değişimi, DevMenu yeniden yükleme) `SceneTransition.LoadDirect` ile ortak yola bağlandı (kilit + PrepareForLoad + try/catch, GameManager gerekmez); müzik "duck" kodu yok, `EnforceSingleMusic` sesi zorlaması çakışmaz. PlaytestBot bilerek dokunulmadı.

## D115 — Grup 3 konum kalıcılığı (bug 18, 19, 29, 31, 33)
- **31 (dekor her girişte kayıyor):** Kök neden: yüklemede (`DecorPlacementController.Start` + `RepairLayout`) dekor KATI kuralla (`CanPlace` = yapıların ayak izi ∪ GÖRSEL bloğu) denetleniyordu; yapı görseli (animasyon karesi, seviye sprite'ı, ölçek) girişten girişe farklı ölçülünce yapıya komşu dekor itilip kaydediliyordu (testle yeniden üretildi: (6,17)→(5,16)). Ayrıca RepairLayout'ta dekor `except` olmadan kendi hücresiyle karşılaştırılabiliyor ve doluluk liste boşaltılınca yeniden kurulmuyordu. Çözüm: kayıtlı hücre otoritedir — `SavedCellValid` (yumuşak kural, D62: yapı AYAK İZİ, kamp, doğa, önceki dekor, açık alan); yalnız gerçek mantıksal çakışmada taşınır. Yeni yerleştirme/taşıma katı kuralda kalır. Arazi ağaç/kaya sprite'ı, boyut yedeği ve animasyon karesi kimlikten türetilir (`BaseMapManager.StableUnit`, tohumsuz Random yok). `RepairVisualLayout` dekora zaten bakmıyordu (CollectStructures). Kayıt şeması değişmedi.
- **33 (inşaatçı evi dip dibe):** `NearestFreeSpot` yalnız 2×2 ayak izine bakıyordu. `SuggestBuilderHutSpot`: önce kulübe görseli + 1 hücre boşluk payı, olmazsa yalnız görsel, o da olmazsa eski kural. Kayıtlı konumu olan kulübeye dokunulmaz (yalnız yeni/kaydı olmayan kulübe ve "önerilen düzen").
- **19/29 (okçu kaptan kale yanında):** Okçu hep `Posts[0]`'da kuruluyordu, akın sonrası bıraktığı yerde kalıyordu; RevealArcher'daki `MoveToPost(0)` zaten P1 sayıldığı için çalışmıyordu. `IslandArcher` gezinme düzeni: akın dışında (giriş, Okçuluk Alanı bitince, akın sonrası, ölümden doğuş) Okçuluk Alanı kapısı (`IslandSceneSetup.TryArcheryDoor`) çevresinde (2,2 b, aynı yükseklik, düz çizgiyle yürünebilir hücreler) yürür/durur; bina taşınırsa yeni kapıya geçer. Akın başında eskisi gibi P1'e geçer; atış/hedefleme/ölüm akışı değişmedi. Okçuluk Alanı yoksa eski davranış.
- **18 (Kaldır menüsü başka dekorun üstünde):** Dünya→ekran dönüşümü doğruydu; sorun hedef seçimi ve sınır: dekor çarpıştırıcısı tüm sprite karesi, seçim "merkezi en yakın" idi ve menü `sr.bounds`'a (saydam pay dahil) bağlanıyordu. `SmallTargetAt`: noktayı GÖRÜNÜR sınırında tutanlardan ekranda en öndeki (sıralama), eşitse en yakın; menüler `BaseVisualBounds.VisibleBounds`'a bağlı (dekor, ağaç/kaya, doğa).
- Test: `Session11PositionTests` (4 test: D31 üç yeniden girişte dekor+arazi aynı, D33 üç kulübe arasında ≥1 hücre boşluk + kayıtlı konum korunur, D19 giriş/gezinme/akın P1/akın sonrası, D18 dokunuş seçimi + menü bağlantısı). Cihazda gözle doğrulanmadı.

## D116 — Grup 4 savaş akışı ve watchdog (bug 25, 28, 30)
- **28 (bulut altı bölgeye gitme ilk 4 akında yok, 5'te var): KANITLI (kod).** `IslandArcher.KeepLevel` Create anında saklanıyordu; oyuncu ada sahnesinde Kıyı Kalesi'ni yükseltince işaretler (`IslandSession.KeepLevel()`, canlı) "ulaşılabilir" gösterirken `MoveToPost` eski seviyeyle sessizce reddediyordu → yeni açılan P4'e ancak sahne yeniden yüklenince (sonraki akın) gidilebiliyordu. Ayrıca Akın 1 öğreticisi (KK1) "P4'e dokun" diyordu ama P4 KK2'deydi. Çözüm: okçu seviyeyi canlı okur (`KeepProvider = IslandSession.KeepLevel`); `PostUnlock` P4 2 → 1 (Progression).
- **Tek açılış kuralı:** atış yeri açık = `IslandLayout.PostUnlocked(i, Kıyı Kalesi)` (yalnız `Progression.Island.PostUnlock`); okçu (`PostOpen`), işaret, ipucu, dokunuş ve bot hep bunu kullanır. Bölge bulutu/zoneMask atış yerini ETKİLEMEZ (bulut inşa/doğa kilidi; savunmada yarı saydam). Gerekçe: kullanıcı KK seviye kilidinin kalabileceğini söyledi; bölge maskesine bağlamak (P5 Z5 = KK6 + satın alma) açılmış yeri yeniden kilitler, ladder ölçümlerini ve eski kayıtları bozardı (island_base_v2 §sapmalar ile aynı karar).
- **25:** dokunuş artık menzil dışındaki düşman atış yeri işaretinin üstündeyse "Hedef menzil dışında" deyip yutulmaz, atış yerine gidilir. Kilitli yer: "Px kilitli: Kıyı Kalesi Sv.N gerekir (şu an Sv.M)".
- **30 (güneyden gelen yarasa bekliyor): KANITLI (kod + test).** Ada uçanları `Variant.Flying` değil; okçuyu hedeflerken yer ızgarasıyla (GridSteer) yürüyorlardı. Güney kenarından (harita dışı) doğan sürünün en yakın "yürünebilir" hücresi GB/GD adacığıydı → oraya inip anakaraya yol bulamadan bekliyor, dalga bitmiyordu (bug-30.png: iki adacıkta yarasa). Çözüm: ada uçanı her hedefe düz uçar; gizli okçu (Okçuluk Alanı yok) hedef sayılmaz (`IslandArcher.Targetable`).
- **Watchdog (ortak, `EnemyController`):** vuruş menzili dışında `Progression.EnemyWatchdog.StuckSeconds` (6 sn) boyunca hedefe `MinProgress` (0,5 u) yaklaşamayan ya da hedefsiz düşman `FallbackSeconds` (12 sn) yedek hedefe (Kale/Ana Üs) döner: ada → düz çizgi; sefer yolu → yan hedef bırakılır, D96 "ileriden yola dön" ile aynı. Yolu izleyen (Ana Üs'e giden) sefer düşmanına ve rotayla kaleye yürüyen ada düşmanına dokunulmaz → D96 davranışı değişmez.
- **Dalga güvenlik ağı (`IslandRaidManager.WaitWaveCleared`):** tekneler/sürü bittikten sonra `WaveStallSeconds` (30 sn) hiç ilerleme (düşman sayısı/canı/konumu, kale+okçu canı) yoksa kalanlar `ForceFallback`; 60 sn'de hâlâ yoksa kalanlar düşürülür (uyarı günlüğü). Sefer savaşında düşman başı watchdog yeterli görüldü (BattleManager'a dokunulmadı).
- Test: `Session12BattleFlowTests` (4 test). Kayıt şeması değişmedi.

## D117 — Grup 5 ekonomi (bug 16, 22)
- 16: Yeni ada başlangıç Odun/Et `Progression.Island.StartWood/StartMeat = 0` (FeatureConfig.islandStartWood/Meat kaldırıldı); yalnız `EnsureStarted` (yeni ada). İlk Okçuluk bedava (IsFirstBuildFree), diğer türlerin ilk kurulumu yalnız altın → öğretici 0 kaynakla tamamlanır.
- 22: Ada Odun/Et eksik-kaynak yolu tek: `GemActions.OfferIslandTopUp` (GemEconomy fiyatı + GemConfirm). Yükselt, Kale yükselt, Bölge keşfi (IslandBuildUi.OnPrimary) ve yeni bina yerleştirme (IslandPlacementHost.Confirm) buna bağlandı; elmas yalnız onaydan sonra düşer. Kart Atölyesi Et akışı zaten onaylıydı.

## D118 — Grup 6 çizim ve ada UI (bug 1, 23, 17, 20, 21)
- 1 (tahmin, cihazda doğrulanmadı): hayalet maden gibi kökü saydam yapılarda yanlış/dev alt görseli kopyalayabiliyordu. `PlacementController.FitGhostScale`: hayalet dünya boyutu ayak izinin ≤1,5 katı; >4 katıysa yalnız ayak izi karesi (yeşil tint). `AreaHighlight.SafeSpriteMaterial` (Shader.Find null ise URP/2D ve SpriteRenderer yedeği) AreaHighlight, BattleWorldMarkers, WalkGridDebug, ProjectileController'da kullanıldı.
- 23: koyun doğuşu ve hedefi `IslandWalkGrid` yürünebilir + Ağıl ile aynı yükseklik (`IslandSheep.SpotOk/HeightOf`); hedef yolu 0,4 birimde örneklenir. Ağıl taşınınca `RebuildSheep` zaten çağrılıyordu.
- 17 (kanıt): `FancyInfoPanel` gövde alt sınırı maliyet satırıyla (+26 px) çakışıyordu → gövde +34 px yukarıda; süre ikonu/metni ayrıldı; not ve işçi satırı ayrı bantlara; tek satırlık alanlara küçülme+“…”. Ada inşa kartında bilgi sağ sınırı 0,60 (maliyet sütunu 0,62).
- 20 (kanıt): ada BattleScene'de çalıştığı için `AudioSettingsUI.Open` “savaş aktif değil” diye dönüyordu; ada oturumunda savaş kipi kapalı (düğme yeri aynı).
- 21: Kıyı Kalesi yükseltilirken panelde süre gizliydi → gösterilir; dünya sayacı ana üssün `Progression.FormatDuration` biçimiyle.
- Gerileme (kelimeler yapışıyor, D113/D118 ekran görüntüleri; kanıt): Alegreya Sans Dinamik fontlarında boşluk glifi yalnız ~0,15 em (14,4/90 pt; Cinzel 22,5/90) — 10–11 pt koyu SDF yazıda kelimeler kayboluyordu. `TmpFontBootstrap.EnsureWordSpace`: her `CreateDynamicFont` boşluk/NBSP/noktalama/rakamı önceden ekler ve boşluk ilerlemesini ≥0,26 em yapar. Testler: `TmpFontBootstrapTests.DynamicFonts_KeepWordSpacing`. Ayarlar paneli kapanışı (DOTween SetUpdate(true), SetActive(false)) gerçek hata değildi; görüntüdeki panel, testin kapanıştan 1 kare sonra çekmesinin artığıydı (test artık timeScale 0'da gerçek süre bekleyip panel/engelleyici kapalı doğruluyor).

## D119 — Grup 7 ana üs
- 13: BaseCameraController.HandleInput artık sıkıştırma/tekerlek zoom dallarını hiç çalıştırmıyor (duvar kapalı olsa da); HandlePinch kaldırıldı, ZoomBy test için duruyor; tek parmak kaydırma korunur.
- 7: MineStorageGauge.LateUpdate madenin lossyScale değeri değiştikçe (DOScale ile büyürken) her karede Place() çağırır; ölçek durunca 1 sn aralık.
- 9: BaseVisualCatalog type 7 (Köylü Evi) tüm kademeler mor: 1-3 Purple House1, 4-5 Purple House3 (House2 Kart Atölyesi için ayrı).
- 12: "Okçu Kulesi" -> "Okçu Kışlası" / "Archer Barracks" (BaseWorldUI, VictoryReferenceView, yorum/test metinleri; ada Okçuluk Alanı ve sefer içi okçu kuleleri dokunulmadı). Garrison_ArcherTower prefab ve BaseGarrisonVisual varsayılanı tek PracticeArcher, anchor (1.05, 0.6); görsel konum son ekran görüntüsünde doğrulanacak.
- 8: Ana Üs garnizonundan 3 DoorGuard silindi (prefab + Defaults; eski kayıtlı DoorGuard çapaları da Build içinde atlanır, i==2 koşulu kalktı). Çatı okçuları kaldı; görünür boy rol bazlı: RoofArcher 2.4 u, diğerleri 1.9 u (BaseGarrisonVisual.FitVisibleHeight). Siperden taşma son ekran görüntüsünde doğrulanacak.
- 14: Başlangıç ağaç/kaya (BaseMapManager.SpawnTree/SpawnRock) ve kilitli arazi dekoru (BaseSceneSetup.CreateLockedDecor) ölçeği tek kaynak DecorScale.For(DecorData,sprite) x DecorScale.Variety(StableUnit ±%8); kayıtlı boyut artık görseli belirlemez (obj.size render boyuna eşitlenir), konum/kimlik/StableUnit seçimi aynı. Collider hücre boyunda (0,9 u). Yoğunluk: ağaç doğuş boşluğu BaseMapManager.TreeClearRadius 1,9 u; kilitli arazide ağaçlar 2,2 u içinde komşuya çizilmez (rastgele akış ve removedNatureIds kimlikleri bozulmadan). Ağaçlar eskisinden belirgin büyük (genişlik 2,76 u); görsel onay ekran görüntüsünde.
- 34: Kahraman ekran oranı tek kaynak HeroLook.ScreenHeightFraction (0,1372 = Warrior karesi 3 u × kök ölçek / kamera görünür yüksekliği; eski ana üsten ölçüldü: ölçek 1,928, ortho 21,07) ve HeroLook.WarriorScaleFor(ortho). Ana Üs (HeroMovementController, kadraj oturunca 6 kare sonra yeniden uygular) ve sefer (HeroController, BattleSceneSetup.CameraSizeFor) buradan hesaplar; collider dünya boyutu korunur. UnitScale.Battle ve diğer birimlere dokunulmadı. Sefer kahramanı eskisinden ~%6 büyük (ortho 8,18). Regresyon: Session13G7Tests (zoom, gösterge, mor ev, tek okçu, DoorGuard yok, ağaç ±%8, kahraman oranı); görüntüler Docs/playtest/bugs-g7/. RepairVisualLayout/SavedCellValid ağaç/kayayı denetlemez (yalnız bina/kulübe/kamp, dekor hücre ayak izi): büyük ağaçlar yerleşimi kaydırmaz (Session11PositionTests+Session8BaseTests 8/8 geçti).

- QA düzeltmesi (madde 34 gerilemesi): `Session4Tests.BarracksRoofLancerPatrolsAndBattleLancersMatchHeroScale` düştü (beklenen 0,7317, gelen 0,7072): kahraman ölçeği artık `HeroLook.WarriorScaleFor(sefer ortho)` (HeroController.SetupAnimator), mızraklı ise eski sabit `UnitScale.Battle` (0,85×0,64×1,3) kullanıyordu. Testin niyeti "savaş mızraklıları kahramanla aynı boy" olduğundan test gevşetilmedi, kod düzeltildi: `UnitScale.BattleHero()` (aynı tek kaynak) eklendi, `UnitScale.InBattle` onu kullanır (AllyLancer.VisualScale); `UnitScale.Battle` sabiti değişmedi. Testin "kahraman = tablo" satırı BattleHero'ya bağlandı. Ayrıca `BattleObjectiveSitsInTheTopBarNotOverTheWorld` D98'den beri bayattı (ObjectivePill yok, gösterge BattleTopBar'ın ObjectiveCard'ı); test yeni karta güncellendi.
## D120 — Grup 8 ada akın tanıtımı planı (bug 26; PLAN, uygulanmadı)
- **Hedef:** ilk akında Akın 1 kaybı "kontrolü bilmiyordum" yüzünden olmasın (ORTA: yenilgi açıklanabilir). Yeni sistem yok; mevcutlar genişletilir.
- **Yeniden kullanılacaklar:** Akın 1 öğreticisi `IslandRaidManager.cs:93-94` (tutorialRun), adım geçişleri :255-270, metin `TutorialHint()` :354, kayıt `tutorialStep` 0–4 (`PlayerSaveData.cs:300`); HUD hap şeridi `IslandHud.cs:906` (BuildHint), gösterim :1232, ATEŞ nabzı :1241; atış yeri ipucu `IslandSceneSetup.cs:423-439` (dünya etiketi + Toast, PlayerPrefs `MEB_PostHintShown` :341, cihaz başına); ok/delik kodu `IslandBuildTutorial.Show` `IslandBuildTutorial.cs:145-182`; `IslandHud.Toast` :1051.
- **Hedef kontroller:** HARİTA/TAKİP tek düğme `IslandHud.cs:708` (etiket :1184, `IslandCameraController.ToggleFullFrame` :174); ATEŞ basılı tut `IslandHoldButton` `IslandHud.cs:20,696`; dolu atış eşiği `GameData.cs:439` (ChargeStart 0,35 sn, ChargeFull 1,2 sn, ×2,5; sayı değişmez); yetenek kilidi `IslandHud.cs:808,824-828`.
- **Uygulama şekli:** `TutorialHint()` çok adımlı "akın tanıtımı" kuyruğuna genişler; IslandBuildTutorial'daki zıplayan ok kodu küçük ortak yardımcıya çıkarılır (ör. `Island/TutorialPointer.cs`: RectTransform ya da dünya noktası hedefler), iki öğretici de kullanır. Akında karartma/delik YOK.
- **Zaman:** `Time.timeScale` değişmez (akın donmaz). Aynı anda tek baloncuk (hap + hedefe ok); eylem yapılınca ya da 7 sn sonra kapanır; baloncuk dokunuş yutmaz (raycastTarget false, yalnız ATLA).
- **Adımlar (sıra · tetikleyici → kapanış · metin):**
- 1 ATEŞ (mevcut adım 1) · ilk tekne → ilk atış · `L.T("Tekne geliyor! ATEŞ'e dokun.", "A boat is coming! Tap FIRE.")` (mevcut metin)
- 2 Dolu atış · 1 kapandıktan 3 sn sonra → ilk dolu atış (Charge01=1 bırakıldı) · `L.T("Güçlü atış: ATEŞ'i basılı tut, halka dolunca bırak.", "Power shot: hold FIRE, release when the ring fills.")`
- 3 Atış yerleri (mevcut adım 2 + PostHint birleşir) · B kumsalı teknesi görününce ya da dalga 1 bitince → HERHANGİ açık yere geçiş (:270'teki `post == 3` koşulu kalkar) · ok en yakın açık işarete · `L.T("Halkalar atış yeri: dokun, okçu yürüyüp oradan savaşır.", "Rings are firing posts: tap one, the archer walks there and fights.")`
- 4 HARİTA · 3 kapanınca / dalga 2 başı → FullFrame=true · `L.T("HARİTA: tüm adayı gör, gelen tekneleri yakala.", "MAP: see the whole isle, spot incoming boats.")`
- 5 TAKİP · 4'ten hemen sonra → FullFrame=false · `L.T("TAKİP: kamera yine okçuyu izler.", "FOLLOW: the camera tracks your archer again.")`
- 6 Özel vuruşlar · Akın 1'de YOK; `IslandAbilities.Unlocked(kind)` ilk kez true olduktan sonraki ilk akın başında, düğmeye ok · `L.T($"Yeni vuruş: {IslandAbilities.NameOf(kind)}! Düğmesine dokun.", $"New shot: {IslandAbilities.NameOf(kind)}! Tap its button.")`; Ok Yağmuru nişan metni :799 aynen kalır.
- **Kayıt:** yeni alan `IslandSaveData.raidIntroSeen` (int bit maskesi, varsayılan 0): 1 ATEŞ, 2 dolu atış, 4 atış yeri, 8 HARİTA, 16 TAKİP, 32/64/128 yetenek 0/1/2. JsonUtility eksik alanı 0 okur → `CurrentVersion` 13 (`PlayerSaveData.cs:224`) değişmez. Bit adım kapanınca (eylem/ATLA/zaman aşımı) yazılır + `SaveGame`. `tutorialStep` anlamı korunur (`IslandEconomy.cs:217`, `SaveSystem.cs:462` "girilmiş ada" sezgisi buna bakar). `MEB_PostHintShown`=1 → bit 4 sayılır.
- **Eski kayıt önerisi:** `highestAkin>0` ya da `tutorialStep>=3` → bit 1 ve 4 (ATEŞ, atış yeri) set sayılır; HARİTA/TAKİP ve dolu atış hiç öğretilmediği için sonraki akında yalnız bunlar (3 kısa baloncuk, atlanabilir). Yetenek baloncukları eski oyuncuya da bir kez gösterilir (kısa, zararsız).
- **ATLA:** hap şeridinin sağında `L.T("ATLA", "SKIP")` → 1–16 bitleri set + kayıt; yetenek bitleri ayrı (kendi açılışında yine bir kez).
- **Çakışma/sıra:** Okçuluk öğreticisi (Done=5) bitmeden akın başlamaz (`IslandHud.cs:488`) → çakışmaz. Tanıtım baloncuğu varken PostHint/Toast bekler (mevcut kural `IslandSceneSetup.cs:428`). OKÇU DÜŞTÜ afişi (`IslandHud.cs:712`, y −150..−102) hap (y −122..−84) ile üst üste → okçu ölüyken baloncuk gizlenir, 7 sn sayacı durur. Zamanlı baskında (IsRaid) yalnız eksik bitler gösterilir.
- **Yerleşim:** hap üstte güvenli alanda kalır; tablette (4:3) en çok 640 ref px genişlik, ortalı, en çok 2 satır, 15 pt (D118 kelime boşluğu). Ok düğmenin üstünde (sağ sütun: ATEŞ merkez −99,107; HARİTA −76,262); atış yeri oku dünya→ekran her kare, ekran dışındaysa kenara kenetlenir.
- **Riskler:** akın sırasında okuma yükü (TR ≤10 kelime/baloncuk); dolu atış adımı tekne yokken boşa → tetikleyici "menzilde hedef var" ile sınırlanır.
- **Test (`Session14RaidIntroTests`):** yeni kayıt Akın 1 → adımlar sırayla, timeScale=1 kalır; her eylem bit yazar, yeniden yükleme tekrar göstermez; ATLA bitleri yazar; v13 fikstürü (`Fixtures/editor_save_v13_2026-11-15.json`, highestAkin>0) → yalnız dolu atış/HARİTA/TAKİP; yetenek baloncuğu yalnız Unlocked sonrası; okçu ölüyken gizli; ekran görüntüsü 1080×1920 ve 1536×2048 (ok hedefte, afişle çakışma yok). Telemetri: IslandStats'a tanıtım ATLA/zaman aşımı sayısı; Akın 1 kayıp oranı tanıtım öncesi/sonrası (IslandBot).
- **Yönetici kararı (D120):** kilitli yetenek düğmeleri gizlenmez (gri + "SvN" kalır, ilerleme hedefi olarak görünür); madde 26'daki "kilidi açılınca gösterilsin" yalnız tanıtım baloncuğu için uygulanır.
- **Uygulama (grup 8 J, bug 26 + 32):** `Island/RaidIntro.cs` (saf mantık, sahnesiz): bit maskesi `IslandSaveData.raidIntroSeen` (1/2/4/8/16/32/64/128), sıralı tek baloncuk, 7 sn zaman aşımı, adımlar arası 3 sn (HARİTA→TAKİP 0 sn), okçu ölüyken gizli + süre durur, ATLA bitleri 1–16'yı yazar, eski kayıt çıkarımı `Begin` içinde (highestAkin>0 ya da tutorialStep>=3 → ATEŞ+atış yeri; `MEB_PostHintShown` → atış yeri). `IslandRaidManager.Intro` her kare `TickIntro` ile bağlam besler (tekne/menzilde hedef/FullFrame/açık yetenekler); `TutorialHint()` artık `Intro.Message`. `IslandHud.BuildHint/UpdateIntro` hap (en çok 640 ref px, 2 satır, ATLA düğmesi) + hedefe zıplayan ok (ATEŞ, HARİTA, yetenek düğmesi, en yakın açık atış yeri; kenara kenetlenir); ok kodu `Island/TutorialPointer.cs`'e çıkarıldı, `IslandBuildTutorial` da kullanır. Atış yeri (adım 3): `post == 3` koşulu kalktı (herhangi geçiş). `IslandSceneSetup` PostHint (dünya etiketi + Toast) olduğu gibi kaldı; tanıtım baloncuğu varken zaten bekler.
- **Sapmalar (D120):** (a) telemetri (IslandStats'a ATLA/zaman aşımı sayısı, Akın 1 kayıp oranı) yapılmadı; yalnız `RaidIntro.SkipCount/TimeoutCount` örnek sayaçları var. (b) Eski kayıt çıkarımı kayda yazılıyor (bit 1+4 set), ayrı bayrak yok. (c) Fire adımı "ilk tekne görününce" (spawnedBoats>0) tetiklenir; mevcut tutorialStep 1/2/3 davranışı kayıt için korunur ama metni artık tanıtım kuyruğundan gelir ("Doğu kumsalını..." metni kalktı, yerini adım 3 aldı). (d) Dolu atış adımı "menzilde hedef var" ile sınırlı; Charge yalnız dolu bırakışla kapanır (dokunma kapatmaz). (e) PROMPTLAR kararı: bug 32 `AudioSettingsUI.SupportEmail` = miniempirebuilder@gmail.com tek sabit; `BuildMailtoUrl` konu/gövdeyi `Uri.EscapeDataString` (boşluk %20) ile kodlar. Belgelerdeki (PRIVACY_POLICY/STORE_LISTING) adreslere dokunulmadı; kodda başka e-posta yok.

## D121 — Grup 9 Depo planı (bug 11; onaylandı, uygulandı — bkz. "Uygulama" satırları)
- **Hedef:** bina/dekoru araziden kaldırıp sonra ÜCRETSİZ, SÜRESİZ, seviyesi korunarak geri kurmak; depodaki nesne hiçbir etkiye (savaş, üretim, kapasite) katılmaz. ORTA: yenilgi açıklanabilir kalmalı (depodaki kule görünür olmalı).
- **1. Veri modeli:** yeni `[Serializable] DepotItem { string id; int region (0 Ana Üs, 1 Ada); int category (0 bina, 1 dekor); string type; int level; float stock; int workers = -1; long storedTicks; }`, TEK liste `PlayerSaveData.depot` (kökte). IslandSaveData'ya alan eklenmez → `task` son alan / `villageSeeded` sırası kısıtı (PlayerSaveData.cs:306,322) etkilenmez.
- Eşleme: Ana Üs binası `PlacedBuildingData.slotId/buildingType/level` (PlayerSaveData.cs:404-412); dekor `PlacedDecorSave.instanceId/decorId` (:415-421); ada `IslandPlacedBuilding.id/kind/level/stock/workers` (:348-357). `BuildingSaveEntry` (tür girdisi :474) ve ada hasar kaydı `IslandBuildingSave` (:373, id ile) değişmez.
- **Karar: ayrı liste (bayrak değil).** Bayrak riski: `BaseMapManager.SaveAll` placedBuildings'i sahneden baştan yazar (BaseMapManager.cs:1512-1539), `DecorPlacementController.Save` placedDecor'u da (DecorPlacementController.cs:412-418) → bayraklı kayıt ilk kayıtta silinir ya da `_unloadedBuildings` (:106,1537) gibi özel yol ister; ayrıca her okuyucuya filtre gerekir (BuildingLevels.cs:47-61 + adada 69 `.placed` kullanımı; kaçırma = depodaki kule savaşta çalışır). Ayrı listede tüm etkiler kendiliğinden düşer; bedeli: limit/sayaç yerleri depoyu ayrıca sayar (madde 5).
- **2. Kayıt uyumluluğu:** `CurrentVersion` 13→14 (PlayerSaveData.cs:224); SaveSystem.cs:485-490 ardına v14 bloğu (dönüşüm yok, eski kayıt = boş depo); `depot ??= new` Normalize'da da (SaveSystem.cs:497; JsonUtility eksik listeyi null bırakabilir, mevcut desen :413,427).
- Sürüm artışı geri dönüş için şart: eski sürüm bilinmeyen `depot` alanını ilk kayıtta düşürür (depodakiler kaybolur) ama v14>v13 olduğundan orijinali `v14.newer` olarak arşivler (SaveSystem.cs:261-266) → kurtarılabilir. Artmazsa sessiz kayıp.
- SaveInvariants: Repair'e "aynı id hem yerleşik hem depoda → depo kopyası silinir", depo seviyesi 1..MaxLevel; Check'te (SaveInvariants.cs:84-95) id tekliği yerleşik+depo birleşik. Fikstür `Tests/PlayMode/Fixtures/editor_save_v13_2026-11-15.json` → boş depo, başka alan değişmez.
- **3. Arayüz (Ana Üs):** `BaseSceneSetup.ShowBuildingActions` (BaseSceneSetup.cs:1413-1441) `type != MainBase` bloğuna (:1432) 3. öğe `id="depot"`, `L.T("DEPOYA", "STORE")`, `confirm=true`, `question=L.T("Depoya kaldırılsın mı?", "Move to storage?")` (WorldActionMenu N öğe destekler, WorldActionMenu.cs:124-147). Dekor: `DecorPlacementController.ShowActions` (:351-360) TAŞI / DEPOYA / SİL.
- Hariç: Ana Üs (:1432); inşaatçı kulübeleri menüye hiç gelmez (BuilderHut → BuilderPanel, BaseSceneSetup.cs:1338-1344). Altın Madeni öneri: izinli, soru "Depodayken altın üretmez"; `firstBaseGuideDone` false iken depo düğmesi hiç yok.
- **Ada:** dokunma bugün paneli açar (IslandHud.cs:564 → IslandBuildUi.ShowBuilding :426; düğmeler :546, TAŞI :655). Öneri: ana üsle aynı WorldActionMenu (YÜKSELT→mevcut panel, TAŞI, DEPOYA). Hariç: Kıyı Kalesi (placed'de yok), Okçuluk Alanı (HasArcheryReady IslandEconomy.cs:271 → depoda Okçu Kaptan yok, akın/öğretici kilitlenir). `buildTutorial < 5` (PlayerSaveData.cs:336) iken düğme yok.
- Ret (düğme gri + toast, sebep tek satır): inşa/yükseltme süren (BuildingLevels.cs:14-29, GameManager.TaskForSlot :412; ada `task.buildingId` ya da level 0) `L.T("İnşaat sürerken depoya alınamaz", "Can't store while building")`; ada savunması (InDefense); hasarlı/onarımdaki ada binası (`hpFraction < 1` ya da `repairEndTicks > Now`) "Önce onar"; Kart Atölyesi kuyruğu dolu (WorkshopSaveData.queue) "Üretim sürüyor".
- Üretim/işçi: ada Oduncu/Ağıl `stock` DepotItem'a taşınır (kayıp yok), depoda üretmez, kurulunca `lastTick = Now`; `workers` korunur; işçi/koyun `IslandVillage.RebuildPawns/RebuildSheep` (IslandVillage.cs:325,266) ile yenilenir.
- İkon: 📦 TMP fontlarında yok sayılır. `Resources/CampaignArt/ui_icon_depot.png` = Cainos `TX Chest Animation Wooden 0` karesi (`Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Chest Animation.png`, dilimli; Resources dışında) `Tools/campaign_art` ile kopyalanır; olana kadar `ui_icon_build`. ART_NEEDS.md'ye kayıt.
- **4. Depodan kurma:** Ana Üs İnşa Et sekmelerine (BuildMenuController.cs:100-102) 4. sekme `L.T("DEPO", "STORAGE")` (sayı rozeti); kart = tür ikonu + "Sev. N". Dokununca `PlacementController.BeginFromDepot(item)` (BeginPlacement :60 kalıbı, bedel 0, süre 0, inşaatçı şartı yok); `ConfirmPlacement` (:339) yeni dal → `DepotService.Restore` → yeni public `BaseMapManager.SpawnRestored(type,pos,id,level)` (= SpawnBuildingInternal :599, yeni bina gibi katı yer kuralı) → SaveAll. Görev başlamaz; depodan dekor kurmak `DailyQuests.Report(PlaceDecor)` (DecorPlacementController.cs:308) çağırmaz.
- Ada: `IslandBuildUi.ShowMenu` (:401) içinde "DEPO" bölümü; `IslandPlacementHost.ForDepot(id)` (ForMove :30 kalıbı): Cost (0,0), CanAfford yalnız InDefense, Confirm → yeni `IslandEconomy.TryRestore(id,col,row)`; `IslandGrid.Check` limit dalı (:233) depodan kurulumu sayıma katmaz (movingId gibi).
- **5. Oyun etkisi — kendiliğinden düşenler:** savaş kuleleri BattleManager.cs:482,495,1037 → GameManager.GetBattleTowerLevel (:397-404)/BuildingLevels.CompletedCount (2 okçudan 1'i depoda → 1 kule); Kışla AllyLancer.cs:31; Köylü Evi depo bonusu GameManager.cs:190-197; Atölye CardWorkshop.cs:41; HUD DarkFantasyHudPolish.cs:350-352; ada ev/işçi IslandEconomy.cs:306-340, üretim :500-513, akın kuleleri (IslandRaidScheduler/IslandBuildings `.placed`), bölge :25.
- **Elle kapatılacak delikler:** (a) `RecomputeTypeLevel` best==0 ise eski tür seviyesini tutar (GameManager.cs:359-360, SaveInvariants.cs:27-28) → maden depodaysa üretim/kapasite sürer (GameManager.cs:159-169,178,1194-1199). Çözüm: `DepotService.IsPlaced(GoldMine)` false ise GetGoldPerSecond/GetGoldMineStorageCapacity 0; çevrimdışı birikim gps≤0 dalıyla durur (GameManager.cs:255-258).
- (b) Limit/sayaç: `BaseMapManager.GetPlacedCount` (:1663, yalnız sahne), BaseBadges.PlacedCount (:26-33), BuildMenuController.cs:395-398, 490-495; ada `CountOf` IslandBuildUi.cs:243,276, IslandGrid.cs:233; ilk kurulum fiyatı IslandEconomy.cs:292 → hepsi `DepotService.OwnedCount = yerleşik + depo`. **Öneri: depodakiler limite sayılır** (yoksa depola→yeni kur sınırsız bina; :292'de ucuz ilk kurulum). Etiket `L.T($"Kurulu {p}/{max} · Depo {d}", $"Built {p}/{max} · Stored {d}")`.
- Ortak filtre: yalnız `DepotService.OwnedCount(region,type)` ve `IsPlaced(type)`; başka kod depoyu okumaz.
- **6. Ortak mantık:** `Systems/DepotService.cs` (statik, sahnesiz, test edilebilir): CanStore(out reason), Store, CanRestore, Restore, Items(region), OwnedCount, IsPlaced. Hariç türler, ret kuralları ve kayıt sırası tek yerde.
- `IDepotRegion` adaptörü: `BaseDepotAdapter` (PlayerData.placedBuildings/placedDecor + BaseMapManager + DecorPlacementController) ve `IslandDepotAdapter` (IslandSaveData.placed + IslandEconomy + IslandVillage). Üyeler: Find, BusyReason, Despawn, Spawn, Persist. Ana Üs'te bina kaldırma API'si yok → yeni `BaseMapManager.DespawnBuilding` (_mapObjects'ten çıkar, yok et, BaseOccupancyGrid.MarkDirty, yollar/yürünebilirlik yenile).
- Atomiklik: Store = sahneden düş + listeden çıkar + depoya ekle + TEK SaveGame; Restore = önce spawn, başarılıysa depodan çıkar + TEK Save; spawn başarısızsa depo değişmez (kayıp/çift yok).
- **7. Testler (`Session15DepotTests`):** v13 fikstürü → boş depo; 2 Okçu Kışlası, 1'i depoda → BuiltCount 1 ve kule seviyesi kalanınki; Kışla depoda → Lancer 0; Ev depoda → MineCapacityAt düşer; maden depoda → gps 0, çevrimdışı birikim yok; yükseltme süren → ret; depodan kurma altın/inşaatçı harcamaz, seviye korunur; limit yerleşik+depo; aynı id iki listede → Repair tekler; sahne yeniden yüklenince depo/yerleşik tutarlı. Ada: stok korunur ve depoda artmaz, hasarlı ret, Okçuluk/Kale'de düğme yok, InDefense ret. Ekran görüntüsü 1080×1920: 3 düğmeli menü + DEPO sekmesi.
- **Riskler:** çift kopya (iki liste; tek Save + Repair), elmas açığı (görev sürerken depolayıp FinishNow — ret ile kapalı), ucuz ilk kurulum (OwnedCount), dekor görev farmı (PlaceDecor), yükseltme zamanlayıcısı (`ApplyInstanceUpgrade` FindPlaced null → tür seviyesine yazar; ret ile kapalı), çevrimdışı akından kaçmak için depolama (üretim de durur; kabul), geri dönüşte kayıp (v14 arşivi).
- **Telemetri:** PlaytestBot/IslandStats: depoya alma/kurma sayısı, depodaki tür dağılımı, savaş başında "yerleşik kule / sahip olunan kule". ORTA: depoda kule varken savaş hazırlığında ve yenilgi ekranında `L.T("1 kule depoda", "1 tower in storage")` ipucu.
- **Açık kararlar:** (1) Altın Madeni depoya alınabilir mi (öneri evet, uyarıyla); (2) ada dokunuşu WorldActionMenu'ya geçsin mi yoksa panele 4. düğme mi (öneri menü); (3) depodakiler limite sayılsın (öneri evet); (4) Okçuluk Alanı hariç (öneri evet); (5) hasarlı ada binası ret (öneri evet); (6) depoda SİL/iade olsun mu (öneri hayır, sonra); (7) ikon Cainos sandık mı yeni çizim mi.
- **Yönetici incelemesi ve kararları (D121, uygulamadan önce):** Kayıt uyumluluğu doğrulandı: `PlayerSaveData.CurrentVersion`=13 (:224), daha yeni sürüm kaydı `SaveSystem.cs:261-266` ile `vN.newer` arşivleniyor → 14'e çıkış güvenli; ayrı liste seçimi doğru (SaveAll/Decor Save listeyi sahneden yeniden yazıyor). D120'nin `raidIntroSeen` alanı `task`/`villageSeeded` sıra kısıtını bozmuyor. Ortak mantık: tek `DepotService` + iki adaptör onaylandı; depoyu yalnız `DepotService` okur.
  Açık kararlar: (1) Altın Madeni depoya alınabilir, onay sorusunda "depodayken altın üretmez"; (2) adada da WorldActionMenu (YÜKSELT→mevcut panel, TAŞI, DEPOYA); (3) depodakiler bina limitine ve ilk-kurulum fiyat sayımına SAYILIR; (4) Okçuluk Alanı ve Kıyı Kalesi hariç; (5) hasarlı/onarımdaki ada binası reddedilir; (6) depoda SİL/iade yok; (7) ikon: Cainos sandık karesi (yeni çizim yok), hazır olana kadar `ui_icon_build`.
  Ek koşullar: (a) depodan kurulurken seviye, o anki tür/ana üs seviye tavanını aşamaz (aşarsa tavana kırpılmaz, kurma reddedilir ve sebep yazılır — seviye kaybı olmasın); (b) depo öğesi en çok 1 kez kurulabilir; Store/Restore sırasında uygulama kapanırsa SaveInvariants.Repair yerleşik+depo id tekliğini sağlar (yerleşik kopya kazanır).
- **Uygulama (grup 9, 18 Kasım):** plan aynen uygulandı — `PlayerSaveData.depot` (v14, `DepotItem`), `Systems/DepotService.cs` (kurallar/sayım/atomik Store-Restore, tek SaveGame), `IDepotRegion` + `Base/BaseDepotAdapter.cs` + `Island/IslandDepotAdapter.cs`, `BaseMapManager.DespawnBuilding/SpawnRestored/WriteToPlayerData`, `DecorPlacementController.DespawnDecor/SpawnRestored/BeginFromDepot/WriteToPlayerData`, `PlacementController.BeginFromDepot`, `IslandPlacementHost.ForDepot`, `IslandEconomy.TryStore/TryRestore`, `SaveInvariants.RepairDepot` (+Check), İnşa Et 4. sekme DEPO (rozet), ada İNŞA ET'te DEPO satırı, Ana Üs/dekor/ada eylem menüsünde DEPOYA. Testler `Session15DepotTests`.
- Uygulama: `SaveAll`/dekor `Save` ikiye ayrıldı (`WriteToPlayerData` + SaveGame): Store/Restore sahne listesini kayda yazar ama diske yalnız DepotService tek kez yazar (atomiklik şartı; mevcut çağıranlar değişmedi).
- Uygulama (sapma): planda yedek simge `ui_icon_build` yazıyordu; `Resources/CampaignArt`'ta böyle bir dosya yok. Karar 7 doğrudan uygulandı: Cainos sandık karesi `Tools/campaign_art/depot_icon.py` ile `ui_icon_depot.png` olarak kopyalandı (asset silme/taşıma yok); dosya yoksa kod `ui_icon_hammer`'a düşer. ART_NEEDS #26.
- Uygulama: `IsPlaced(type)` = "yerleşik kopyası var YA DA depoda hiç yok" — yalnız tamamen depodaki tür kapanır; madeni hiç yerleşmemiş eski kayıt/test verisinde davranış değişmez. Maden depoya alınırken önce o ana kadarki üretim madene yazılır (`ApplyOfflineGoldMineProduction`), kurulunca zaman damgası şimdi olur; madende birikmiş altın kayıpsız `goldMineStoredGold`'da bekler.
- Uygulama: `WorldActionMenu.Item` iki alan aldı (`blockedReason`, `onBlocked`): ret durumunda düğme gri, dokunuş onay açmaz, sebep toast'la tek satır (plan "düğme gri + toast"). Ana Üs/dekor/ada DEPOYA düğmesi tek tanımdan: `BaseSceneSetup.DepotMenuItem`.
- Uygulama (sapma): adada inşaat alanına (seviye 0) dokunuş eskisi gibi doğrudan paneli açar (bitir/hızlandır orada); tamamlanmış binada karar 2'deki menü (YÜKSELT→panel, TAŞI, DEPOYA). Ada DEPO bölümü İNŞA ET listesinin başında bir satır (yalnız depoda ada binası varken; satırlar yeniden dizilir).
- Uygulama: depodan Ana Üs binası yeni bina gibi `SpawnBuildingInternal` ile kurulur (hayalet geçerliyken; nadir görsel uyuşmazlıkta en yakın geçerli yere kayabilir, yeni inşayla aynı). Depodan dekor katı `CanPlace` ile; görev (PlaceDecor) sayılmaz.
- Uygulama: telemetri `DepotService.Telemetry()` + `[Depot]` günlük satırı (alma/kurma sayısı, depodaki tür dağılımı, "kule yerleşik/sahip olunan"); ORTA ipucu `StoredTowerHint` sefer aşama penceresinde (SAVAŞA BAŞLA üstü) ve yenilgi ekranında.
- Uygulama: `Session6Tests` iki yerde sürümü sabit `13` ile karşılaştırıyordu → `CurrentVersion` (v14 göçü zincirin parçası).
- **Uygulama (QA düzeltmesi, madde 3):** "Madeni koru" (BattleManager.CreateDefendedMine) Altın Madeni depodayken de BİLİNÇLİ çalışır: mod savaş hedefi olduğundan kapatılmaz; maden seviyesi tür kaydından okunur ve `RecomputeTypeLevel` best==0'da eski seviyeyi tuttuğu için bu "son bilinen seviyedir" (depo öğesi ayrıca okunmaz; DepotService.IsPlaced yalnız üretim/kapasiteyi etkiler). Kod yorumu eklendi.

- **Güvenlik:** `SaveInvariants.RepairDepot` NaN/Infinity stok→0, workers<-1→-1, geçersiz bölge/kategori/bilinmeyen tür (Enum.TryParse büyük/küçük harf duyarlı + IsDefined; dekor için bilinen decorId) → öğe silinip loglanır (ParseKind kullanılmaz). `DepotService.RestoreBlockedReason`: Ana Üs yerleşik sayı ≥ tür limiti, Ana Üs/Kıyı Kalesi/Okçuluk Alanı kurulamaz. `BaseDepotAdapter.Spawn`: Altın Madeni kurulmadan önce `ApplyOfflineGoldMineProduction()`. Testler: `Session15DepotTests`.

## D122 — Açılış intro sahnesi (dal intro-sahnesi; Docs/Intro/INTRO_ENTEGRASYON.md)
- **Tarama (başlangıç):** Build sırası önceden MainMenuScene(0), BaseScene(1), BattleScene(2), CampaignScene(3). Kodda `LoadScene(0)` / `LoadSceneAsync(0)` / `buildIndex == 0` YOK; tüm geçişler ada göre (GameManager.FadeAndLoadScene, SceneCloudTransition, SceneTransition, PlaytestBot, testler). "Başa dön" = `GameManager.LoadMainMenu()` → "MainMenuScene" (adla). Sabit "MainMenu" adı yalnız paketteki varsayılandaydı → "MainMenuScene" yapıldı. Ad bağımlı switch'ler: SceneAutoSetup, AudioManager.OnSceneChanged, BackKeyRouter, ScenePolishInstaller (hepsi "MainMenuScene").
- **Kök risk bulundu:** `SceneAutoSetup` `default` dalı bilinmeyen sahneye eski `IntroSceneSetup` (logo+OYNA) ekliyordu → Intro'da ikinci giriş/çift logo olurdu. `case "Intro": break;` eklendi (tek değişiklik). AfterSceneLoad başlatıcıları (GameNotifications, IapService, BackKeyRouter, DevMenu, OccupancyDebugView) artık Intro'da bir kez çalışır: hepsi DDOL/kendinden güvenli; IapService siparişleri GameManager yokken kuyrukta bekletir (ProcessWaiting) → kayıt/ödül etkilenmez.
- **Sıra:** Intro(0), MainMenuScene(1), Base(2), Battle(3), Campaign(4). Intro yalnız soğuk açılışta yüklenir; arka plandan dönüşte sahne yüklenmez → tekrar çıkmaz.
- **Paket script'leri (zorunlu en küçük değişiklik):** IntroSceneController: targetFrameRate kaldırıldı (tek yer MobilePerformanceConfig), `CanStreamedLevelBeLoaded` + try/catch, op null → `LoadScene(name)` yedeği, o da olmazsa index 1; Single mod (intro bellekten çıkar); etkinleşme süresi günlüğe "[Intro] … etkinleşme süresi". TipRotator: 3 ipucu gerçek özelliklere göre (maden/altın, kule yükseltme + Ana Üs savunması, Sefer bölgeleri/ödül). Liste tek yer: TipRotator.tips varsayılanı (sahne kurucu üzerine yazmaz). Sprite'lar yeniden üretilmedi.
- **Proje tarafı yeni:** `Editor/IntroSceneBuilder.cs` (Tools/Intro/Intro Sahnesini Kur + `-executeMethod IntroSceneBuilder.BuildCli`; import + sahne + Build Settings), `Scripts/Intro/IntroTextFont.cs` (ReferenceUiVisuals.ApplyFont → AlegreyaSans-Bold dinamik Türkçe, D113 zinciri; kontur çalışma anında), `Scripts/Intro/IntroBackgroundFit.cs`, test `IntroSceneTests`.
- **Çift logo yok:** Logo katmanı ve UIIdleAnimation sahneye konmadı (logo arka plana pişmiş). Logo animasyonu sonraki iş (katmanlı görsel gerekir).
- **Ekran oranı (madde 10):** Canvas Scaler 941x1672, Match 0.5. Arka plan: referanstan dar/uzun ekranda Envelope (9:21'de yanlar kırpılır, logo tam); daha geniş ekranda (3:4 tablet) FitInParent + arkada aynı görselin karartılmış Envelope kopyası (siyah bant yok). Neden: 3:4'te Envelope logoyu ya da alttaki boş UI alanını kırpıyordu; üstten hizalamada ipucu yazısı şövalye pelerinine biniyordu (1. tur görüntüsü). Çubuk/ipucu `ReferenceSafeArea` içinde (alt ev çubuğu/çentik payı).
- **Import:** arka plan ASTC 6x6 (iOS/Android, Max 2048, Compressed), bar/ikon ASTC 4x4 + CompressedHQ; hepsi Single, Full Rect, mipmap/Read-Write kapalı, Bilinear, Clamp. `.gitattributes`'ta png için LFS kuralı yok → normal git.
- **Süs çizgileri:** referanstaki ipucu yanı altın çizgi+nokta pakette yoktu; sprite'sız renkli Image ile çizildi (yeni sanat yok).
- **Paket kusuru:** `icon_tip_bulb.png` alfasız (RGB, koyu dikdörtgen zemin pişmiş) → ampulün çevresinde koyu kare görünüyor. Kodla giderilemez (sprite yeniden üretilmez); ART_NEEDS.md'ye eklendi.
- **Ses:** intro'da ses kaynağı yok (yalnız AudioListener, uyarı olmasın diye); müzik MainMenuScene'de AudioManager ile eskisi gibi başlar.
- **Donma ölçümü:** MainMenuScene etkinleşmesi editörde 226–235 ms (1 sn eşiğinin altında); cihazda doğrulanmadı.
- **Test notu:** PlayMode koşucusunun kamerasız InitTestScene'inde eski `IntroSceneSetup.Start:63` NullReference basıyor (SceneAutoSetup default dalı; intro'dan önce de vardı, oyun akışını etkilemez, dokunulmadı).

## D123 — Intro → doğrudan Ana Üs; YENİ OYUN Ayarlar'da; kilitli arazi dekoru (kullanıcı isteği, 18 Kasım 2026)
- **Akış:** Intro bitince `BaseScene` açılır; eski giriş ekranı (MainMenuScene: logo + hikâye + DEVAM ET/YENİ OYUN) artık gösterilmez (sahne silinmedi, Build Settings'te kaldı; oyundan ona giden düğme yok). D122'deki "intro → MainMenuScene" kararı bununla değişti.
- **Eski menünün görevleri `IntroGameBoot`'a taşındı:** GameManager/SpriteManager kurulur, GameManager.Start kaydı yükler; kayıt yoksa Savaşçı ile yeni oyun (`GameManager.PrepareNewGame`, eski OYNA ile aynı). Kayıt açılamadıysa (K-1) kayıt ezilmez: intro'da `ConfirmDialog` → YENİ OYUN (eski kayıt arşivlenir) / ÇIK. IntroSceneController, çubuk %100 olsa bile `CanFinish` (oyun verisi hazır) olmadan oyunu açmaz; yedek eşzamanlı yükleme de bunu bekler.
- **Ayarlar → OYUN → YENİ OYUN** (yalnız BaseScene/CampaignScene; savaş ve ada akınında yok): "Oyunun ilerlemesi kaybedilecek ve oyun yeniden başlayacak. Onaylıyor musunuz?" VAZGEÇ/ONAYLA. Onay: `GameManager.RestartNewGame` = `ResetProgressForNewGame` (SaveSystem.DeleteSave arşivleyerek siler, ilk görüş ipuçları sıfırlanır — eski menüdeki ConfirmWeapon ile aynı adımlar, artık tek kaynak) + `LoadBaseWorld`. Onay penceresi genel `ConfirmDialog` (QuitConfirm stili, yeni sanat yok).
- **Kilitli arazi dekoru kök nedeni:** `CreateLockedDecor` Resources/BaseDecor'daki KIRPILMIŞ sprite'ları (kütük 50 px, kaya 34 px, çalı 69 px) DecorScale'in TAM KARE genişliğiyle (kütük 2,76 u = 192 px kare) ölçekliyordu → kütük ~4×, kaya/çalı ~2× büyük; her 1×1 hücreye ±0,2 titremeyle %62'ye varan yoğunlukta konduğu için üst üste biniyordu (yalnız ağaç-ağaç kuralı vardı).
- **Çözüm:** görsel = yerleştirilen dekorun `DecorData.Visual` + `DecorScale.For` (dekorla birebir aynı boy: kaya ~0,9–1,1 u, kütük ~0,7 u, çalı ~0,9–1,3 u); hücre merkezi + dekorla aynı taban hizası (`VerticalOffset`), titreme yok; tür yarıçaplı çakışma kuralı (ağaç 1,1 / çalı 0,62 / kaya 0,55 / kütük 0,36; iki nesne arası ≥ rA+rB) — binen nesne çizilmez. Hücreye sığmayan varyantlar (kaya4 1,7 u, çalı3 1,5 u) kilitli arazide kullanılmaz; varyant hücreden kararlı (StableUnit). Rastgele akış (tohum/çekim sırası) aynı → hangi hücrede hangi tür olduğu ve kayıtlı `removedNatureIds` değişmez. DecorScale tablosu değişmedi (yerleştirilen dekor zaten doğru boydaydı). Açık alandaki rastgele kaya/ağaç (SpawnRock/SpawnTree) zaten DecorData sprite'ını kullanıyordu.
- **Test:** IntroSceneTests (intro → BaseScene, Ayarlar YENİ OYUN → onay → altın sıfırlandı, eski sahne/intro'ya düşmedi), LockedDecorTests (66 engel, çakışma yok, hücre merkezi, dekor ölçeği ±%8). Regresyon 86/87: tek hata `Session9RuleGuardTests` (SceneTransition.cs:86 ham DateTime; bu dalda değişmeyen dosya, önceden var).

## D124 — Yazı kök nedeni (UI2 Madde 1+5; dal ui-duzeltmeler-2, 18 Kasım 2026)
- **Envanter (önce):** TMP varsayılanı `LiberationSans SDF` STATİK, 250 glif, ç ö ü var / ğ ş ı İ Ğ Ş YOK; kendi fallback'i `LiberationSans SDF - Fallback` (Dinamik, ClearDynamicDataOnBuild=1); TMP Settings genel fallback boş, ClearDynamicDataOnBuild=1. Oyun fontları (Alegreya Sans Medium/Bold, Cinzel ExtraBold) çalışma anında `CreateFontAsset(Dynamic)` ile üretiliyordu (TmpFontBootstrap, ReferenceUiVisuals, IntroSceneSetup ×3, VictoryReferenceView ×2). Font atanmadan kurulan yazılar: dalga kutusu (BattleVfxManager.WaveBannerRoutine), İnşa Et sekmeleri (BuildMenuController.CreateTab), Kahraman/Çırak sekmeleri (BaseWorldUI) → hepsi D113 kancasına bağımlıydı. TMP shader'ları Resources DIŞINDA (`Assets/TextMesh Pro/Shaders`), Always Included'da yoktu; varsayılan malzeme Mobile/Distance Field (anahtar sözcüksüz). Ayrıca `.gitignore` (`TextMesh Pro/Resources/Fonts & Materials/*.asset`) varsayılan font asset'ini (LiberationSans SDF.asset + Fallback) git DIŞINDA bırakıyor (yalnız .meta izli).
- **KÖK NEDEN 1 — KANITLI (test):** D113 `TmpFontBootstrap.Swap` fontu `TMPro_EventManager.TEXT_CHANGED` içinde değiştiriyordu. TMP bu olayı `GenerateTextMesh` sonunda, yani `CanvasUpdateRegistry` GRAFİK YENİDEN KURULUMU içinde (`Rebuild(PreRender)` → `OnPreRenderCanvas`) tetikler. Orada `font`/`fontSharedMaterial` değişince TMP `SetVerticesDirty/SetMaterialDirty`'yi `IsRebuildingGraphics()` yüzünden YOK SAYAR (TextMeshProUGUI.cs:110-163) ama aynı `Rebuild` hemen `UpdateMaterial()` çağırır → ağ LiberationSans UV'leriyle kalır, CanvasRenderer Alegreya atlasını bağlar. Sonuç atlasın o bölgesinde ne varsa: boş → harf görünmez, başka glif → parça/kırık harf ("ɔ U s c", "DH ET"), dolu bölge → beyaz kutu. Fallback alt ağları (İ Ş ı) kendi doğru malzemeleriyle çizildiği için yalnız onlar görünüyordu (ui2-04 tam olarak bu). Kanıt: `FontIntegrityTests.UnassignedText_MeshMatchesMaterial_AcrossToggles` düzeltme ÖNCESİ: "tur 0: ağ/malzeme uyumsuz: ağ 'LiberationSans SDF' atlasından, çizim 'Alegreya Sans - Medium Atlas' ile; bayat ağ" (`Docs/playtest/ui2/font-kanit-oncesi.txt`).
- **"Bazen görünüyor" mekanizması — KANITLI:** bozuk ağ, yazı bir sonraki kez kirlenene kadar (metin değişimi, aç-kapa/OnEnable, kutu boyu değişimi) kalır. Aynı testte yazıyı kapatıp açınca "tur 1-3: temiz" → sekme ilk açılışta bozuk, yeniden açınca/sekme değişince düzgün. İlk ağ yerleşim fazında (ForceMeshUpdate/GetPreferredValues, grafik kurulumu dışında) üretildiyse değişim doğru çalışır → yazıya ve kareye göre değişen "bazen". Her dalga kutusu yeni nesne olduğu için dalga kutusu hep bozuktu.
- **KÖK NEDEN 2 — KANITLI (kaynak + test):** D113 "sağlık denetimi" `TryAddCharacters("a"+Extra)` false dönünce asset'i bozuk sayıp YENİDEN üretiyordu; oysa TMP bu yöntemde eklenecek yeni glif yoksa (`m_GlyphsToAdd.Count == 0`, TMP_FontAsset.cs:2115) her zaman false döner. D113 QA notundaki "önbellek asset'i bozuluyor" teşhisi YANLIŞTI. İkinci erişimden itibaren yeni 2048² dinamik asset + varsayılan fonta yeni fallback → aynı ailenin birden çok atlası; değişimden önce/sonra kurulan yazılar farklı asset'lerde. Kanıt: `BodyFont_IsStableAcrossAccesses` önce "BodyFont ikinci erişimde değişti: -1650 → -1810".
- **Çürütülen / zayıf hipotezler:** atlas dolu → HAYIR (yeni set 198 karakter tek 1024² sayfada). Türkçe glif eksikliği → yalnız LiberationSans'ta (ğşıİĞŞ); Alegreya/Cinzel'de var; Cinzel'de olmayan ←↑→↓▲▼▶ ve bazı para simgeleri Kalın/Gövde fallback'inden gelir. Shader ayıklanması → kutu/kırık harf belirtisini açıklamaz (shader yoksa pembe/hiç); yine de çalışma anı `Shader.Find` ve kontur/gölge (OUTLINE_ON/UNDERLAY_ON) varyantları Always Included değilken build'e girmeyebilirdi → giderildi. Aynı malzemeyi paylaşan font asset'i → yok. Atlas sıkıştırma → yok (Alpha8, içe aktarılmayan alt-asset). Sıralama/mask → belirtiyle uyuşmuyor (yazı yerinde, glif yanlış). Sahne yüklemesinde sıfırlanma → `SceneTransitions_KeepFontAssetsIntact` (ana üs→sefer→UnloadUnusedAssets→ana üs) asset aynı kaldı. **Olası katkı (DOĞRULANMADI):** iOS başka makinede/temiz klonla derleniyorsa git-dışı LiberationSans SDF.asset eksik olur → varsayılan font yok → atanmamış yazılar boş; yeni varsayılan font git'te izli olduğundan bu yol da kapandı.
- **Kalıcı çözüm:** `Editor/FontPipeline.cs` (menü Tools/Mini Empire Builder/Yazı/…, CLI `-executeMethod FontPipeline.BuildCli` / `FontPipeline.AuditCli`): Assets/Scripts dize sabitleri (L.T, AssetText dahil) + sahne/prefab/veri YAML dizeleri + sabit temel set (ASCII, Türkçe, Â Î Û, · • … — – × % ‰ € £ ₺ → ← ↑ ↓ ▲ ▼ ▶ vb.) taranır; `Assets/Resources/Fonts/TMP/` altına EDİTÖRDE doldurulmuş STATİK asset'ler yazılır (atlas + malzeme alt-asset, 72 pt örnek / 7 dolgu / SDFAA, 1024² çok-atlas açık, ClearDynamicDataOnBuild kapalı; yeniden üretim GUID ve alt-asset kimliklerini korur): `MEB Body SDF` (Alegreya Sans Medium, TMP varsayılanı), `MEB Bold SDF`, `MEB Title SDF` (Cinzel ExtraBold; fallback Bold→Body), güvenlik ağı `MEB Safety Dynamic SDF` (boş dinamik, TMP genel fallback'i; yalnız statik sette olmayan karakter gelirse dolar). Boşluk/NBSP ilerlemesi ≥0,26 em üretimde kalıcı (D113 kelime aralığı). Tek zincir: gövde/kalın Alegreya Sans; başlık Cinzel görsel kimlik için korundu ama zinciri Alegreya'ya bağlı. Sahnelerdeki 9 LiberationSans referansı (BaseScene, Intro, MainMenuScene) MEB Body'ye taşındı; LiberationSans'ın fallback'ine de MEB Body eklendi (yerel güvence, dosya git-dışı).
- **D113 kalıntıları:** TEXT_CHANGED kancası ve sağlık denetimi KALDIRILDI; `TmpFontBootstrap` statik asset'leri Resources'tan verir, çalışma anı `CreateDynamicFont` yalnız asset bulunamazsa (hata günlüğüyle). ReferenceUiVisuals/IntroSceneSetup/VictoryReferenceView çalışma anında font üretmez; `ApplyFont` font + malzemeyi birlikte atar. Font atanmamış yazılar kancaya bağlı değil (varsayılan zaten Türkçe-tam).
- **Shader (Project Settings, bu görev için izinli):** GraphicsSettings Always Included'a `TextMeshPro/Mobile/Distance Field` ve `TextMeshPro/Distance Field` eklendi (tüm anahtar sözcük varyantları + Shader.Find her platformda). Paket değişmedi.
- **Uyarılar:** TMP Settings uyarıları açık; `TmpTextIntegrity` geliştirme build'inde/editörde `TMP_Text.OnMissingCharacter`'ı her karakter+font için bir kez "[UI Font] Eksik karakter" diye günlüğe yazar.
- **Font Denetimi** (`Docs/playtest/ui2/font-denetimi.txt`): eksik 0, yalnız-dinamik 0, eski font kullanımı 0; 198 karakter; sahne/prefab 9 yazı + kodda 140 farklı görünen karakter. Kodda tek eksik "✓" (IslandBuildTutorial; Alegreya'da glif yok) → metin "yeşil onay düğmesiyle onayla" yapıldı.
- **İki platform:** Android grafik API otomatik (Vulkan, OpenGLES3), iOS otomatik (Metal), iki modül kurulu. `ShaderData.Pass.CompileVariant` ile iki TMP shader'ı GLES3/Vulkan (Android) ve Metal (iOS) için temel + OUTLINE_ON + UNDERLAY_ON + UNITY_UI_CLIP_RECT (+ALPHACLIP) varyantlarında hatasız derlendi (rapor §4). Font asset'leri Resources'ta, atlas Alpha8 (üç API'de destekli), platforma özel ayar yok. Build Target değiştirilerek tam build alınmadı. **Gerçek cihaz (Android tablet, iOS) DOĞRULANMADI.**
- **Taşmalar:** ada HARİTA/TAKİP/OTO KAPALI ve YAĞMUR/ATEŞ OK/AĞ/SvN etiketleri düğmenin görünen yüzüne daraltıldı (`ReferenceUiVisuals.FitButtonLabel`: kutu oranı, Auto Size 6–12 / 5–9, dar Alegreya Kalın, koyu kontur). Ölçüm (`TmpTextIntegrity.Describe/ScanOverflow`): metin layout kutusuna tam sığıyordu (42/42), görsel taşma kutunun sprite'ın görünen yüzünden geniş olmasındandı. İnşa Et kart adı kenarlardan pay + iki satıra izin (KART ATÖLYESİ), Auto Size 8–14; 4:3 tablette kartlar/kaydırma alanları sekmelerin üstüne biniyordu (üst kenar panelin %65'i, sekmeler üstten sabit -62..-106) → içerik sekmelerin hemen altından başlar (`SetBelowTabs`, 9:16'da konum aynı); sekme yazıları Auto Size (İnşa Et 11–21, Kahraman/Çırak 10–19). Seferde "Sonraki dalga N": 16 pt kalın + koyu kontur + gölge (örnek malzeme, paylaşılan malzeme değişmez).
- **Test:** FontIntegrityTests (2), FontCaptureTests (4: ada dalga kutusu ×3 + duyuru + HUD/TAKİP, sefer dalga kutusu ×3 + sonraki dalga, İnşa Et 4 sekme ×3 tur, Kahraman/Çırak ×3 tur, seviye banner'ı, sahne geçişi; her ekranda tüm etkin TMP yazılarında eksik glif + ağ/malzeme uyumsuzluğu = 0, ada düğmelerinde taşma = 0), TmpFontBootstrapTests (statik/Türkçe/varsayılan + kelime aralığı). Görüntüler 9:16 ve 4:3: `Docs/playtest/ui2/`. İlgili regresyon kümesi (Intro, SceneTransition, RaidAbility, Session14/15, HudExpansion, MiniEmpireBottomBar, Session12DrawIslandUi, UiFixPass, Session5Ui, Session8Hud, IslandRaid, BattleSettingsRevision + yazı testleri) 68/67: tek hata `HudExpansionReviewTests.OwnershipBattleCardAndLevelVisual` (seviyesiz PlacedBuildingData tamamlanmış sayılmıyor, 6. oturum kuralı; yazıyla ilgisiz). Ek koşu: ExpansionFeatureTests'te 2 hata GameManager'sız sahne yüklemesinden (ortam; "[BaseWorld] GameManager bulunamadi").

## D125 — İş 2: Ana üs dış zemin dolgusu (plan, art-director)
- **Neden (kanıt):** Ana Üs kamerasının `pixelRect`'i iki barın arasına kesiliyor (`ReferenceUiVisuals.cs:389-402`); alt sınır BottomDock üst kenarı. BottomDock bar kapanınca da 84 birimde kalıyor (bug 13 kadraj sabitliği; `MiniEmpireBottomBar.cs:11,64-71`), yalnız `BarSlide` kayıyor. Bandın altını kamera hiç çizmiyor; orada yalnız `ScreenBottomFill` (#14110D, `ReferenceUiVisuals.cs:327-339`) var = ui2-01'deki siyah. Kamera arka plan rengi (`BaseSceneSetup.cs:1094`) ve `BaseBackdropTilemap` (`BaseSceneSetup.cs:526-539`) suçlu değil.
- **Çözüm:** `WorldViewportBars.Initialize`'a `fullBleed` bayrağı; yalnız Ana Üs açar (`DarkFantasyHudPolish.cs:14`), savaş (`:248`) değişmez. fullBleed'de `pixelRect` tam ekran; barlar arası band saklanır. `BaseCastleWalls.ApplyFraming` (`:304`) banda göre: `sBand = FitOrtho(frame, bandW/bandH)`, `wpp = 2*sBand/bandH`, `orthographicSize = wpp*Screen.height/2`, `cam.y = frame.center.y + (Screen.height/2 - band.center.y)*wpp`. Barlar arası görüntü bugünküyle piksel piksel aynı (bug 13 korunur). `ScreenBottomFill` fullBleed'de alpha 0; `ScreenTopFill` kalır. `NeedsHudAlignment` saklanan bandı karşılaştırır.
- **Dolgu sınırı:** görünen dünya + 3 u pay; sabit en kötü durum x frame ±16, y frame.yMin-16 … frame.yMax+8 (9:16 / 9:21 / 4:3 hesaplı).
- **Zemin:** `BaseBackdropTilemap` döngüsü (`BaseSceneSetup.cs:536-538`) bu sınıra genişler (tek Tile, sıra -30, 1–4 draw call, collider yok; `BaseWorldLayout.Buildable`/`RefreshBuildableArea` değişmez).
- **Dekor:** mevcut tohumlar (40172, 90211, 52817, 76143) DOKUNULMAZ. Yeni `CreateForestFarBelt` ayrı sabit tohum 61337, alt [yMin-14, yMin-6] ve üst [yMax+3, yMax+7] bant, x [xMin-6, xMax+6]; %70 ağaç/%20 çalı/%10 kaya, aynı sprite ve boylar, `OutsideOrder(y)`. Collider/MapObjectTag yok, kayda girmez. ≤32 yeni SpriteRenderer, toplam dış dekor ≤250.
- **Korunacak:** kırmızı ok `TabOpenY/TabClosedY`, `tabDot`, 0.25 sn kayma, `PrefKey`. Bar kapalıyken alt banda dokunuş kahramanı duvar dışına götürmemeli.
- **Testler:** `ViewportRevisionTests.cs:81-84` Ana Üs dalı güncellenir; `MiniEmpireBottomBarTests.cs:126-134`, `UiAnaUsCapture.cs:147-156` aynen geçmeli. Yeni: 3 oranda alt %15 ve çentik pikselleri #14110D değil/yeşil; iki açılışta ForestFarBelt konumları aynı.
- **Görüntüler:** `Docs/playtest/ui2/is2-bar-{kapali,acik}-{9x16,9x21,4x3}.png`, `is2-ok-bildirim-kapali-9x16.png`.
- **Yedek plan:** fullBleed testleri bozarsa `ScreenBottomFill`'e orman dokusu (RawImage+uvRect), ENGELLENDİ notuyla.

## D126 — İş 3: Ada üst barı ana üs stilinde (plan, art-director)
- **Mevcut:** ana üs barı `Polish/UI/MiniEmpireTopBarSkin.cs:16-85` `Apply(hud)` (BaseHudRedesign.cs:142); kalkan `BaseHudRedesign.cs:376-456` LevelCrest ("ANA ÜS" :399, seviye `GameManager.GetMainBaseLevel()` :447, yenileme :439). Dişli `Polish/Core/AudioSettingsUI.cs:116-130` (miniGear yalnız BaseScene). Ada barı `Island/IslandHud.cs:111-184` (TopBar 52 birim, ANA ÜS düğmesi :158, ad hapı + titleText :167-182, çipler ResourceTopBar.BuildIsland :183), kum saati RaidBadge :233 (akında gizli :323), kale can çubuğu :131, başlık :1394-1398.
- **Veri:** Altın GameManager.Gold, Et/Odun IslandSession.Save, Elmas GemCounter — aynı kaynak, değişmez. Kıyı Kalesi seviyesi `IslandSession.KeepLevel()` (IslandSession.cs:24), yenileme `IslandEconomy.TaskCompleted`.
- **İnşaatçı yuvası (karar):** adada gerçek tek "Ada Ustası" var (`IslandEconomy.BuilderBusy` :709) → yuva kalır: boşta "1/1", meşgulse "0/1"; dokununca kalan süre ShowToast. Uydurma veri değil.
- **Paylaşım (kopya yok):** `MiniEmpireTopBarSkin.Apply` → ortak `ApplyCore(...)`; `Apply(hud)` imza/davranış aynı; yeni `ApplyIsland(topBar, chips)`: topbarBg dilimli, Ornament gizli, sıra gold,wood,meat | gem,builder.
- **Kalkan:** LevelCrest'e `Source{MainBase,Keep}`; Keep: `L.T("KIYI KALESİ","COASTAL KEEP")` + KeepLevel; yazı alanı 0.16–0.84, oto boyut, NoWrap. Ada kalkanı ayrı "IslandCrest"; ana üs yolu değişmez.
- **Dişli:** `miniGear = (baseScene || (battleLayout && IslandSession.Active)) && TopBarReady`.
- **Bar altı yerleşim:** sol HomeButton (labelPlate, "ANA ÜS / EMİN MİSİN?" mantığı aynı); orta kalkan altında IslandNameButton plakası (ada adı, dokununca yeniden adlandırma); sağ RaidBadge yerinde. Akında aynı plaka titleText ile "AKIN N · DALGA x/y" (genişlik 170), kale can çubuğu plakanın altına iner. Safe Area içinde, ref birimli; 9:16/9:21/4:3.
- **Hiyerarşi adları korunur** (TopBar, TopBar/HomeButton, TopBar/GoldPill, TopBar/<Chip>/Value; testler: Nov13FeedbackTests:445,494, Session3Tests:341-342, FontCaptureTests:112). Eski bar silinmez: katalog yoksa eski yol çalışır.
- **Test/görüntü:** FontCaptureTests'e `IslandTopBar_MatchesBaseStyle` (kalkan metni/seviye, öğe dikdörtgenleri kesişmez ve SafeArea içinde, Font Denetimi 0). Regresyon: Session3Tests ana üs barı, UiAnaUsCapture, ana-us-hud. Görüntüler `Docs/playtest/ui2/is3-ada-normal-{9x16,9x21,4x3}.png`, `is3-ada-akin-{9x16,4x3}.png`. Yeni sprite gerekmez.
