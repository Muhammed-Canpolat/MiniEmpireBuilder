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
