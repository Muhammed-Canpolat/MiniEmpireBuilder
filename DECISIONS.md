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
