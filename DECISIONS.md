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
