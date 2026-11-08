# Ana Üs sınırı ve seviye bazlı maden — 23.10.2026

## Ekran görüntüleri

- [390×844 Ana Üs](mine-boundary-base-final.png): koyu dış şerit ve otomatik kahverengi yol görünmüyor; çit daha dolgun, dış çim açık yeşil.
- [Maden seviye 1 yakın plan](mine-boundary-level1-final.png)
- [Maden seviye 8 yakın plan](mine-boundary-level8-final.png)

Bu test görüntülerinde yeni seviye 5 Ana Üs yerleşimi kullanıldı. Kullanıcının kayıtlı seviye 6 bina konumları değiştirilmedi.

## Yapılanlar

- Ana Üs dışındaki koyu zemin döşemesi, dört kenardaki kahverengi gölge şeritleri ve vignette kaldırıldı. İç/dış çim renkleri daha canlı iki yakın yeşil tona çekildi. Ahşap çit ve taş direklerin görünür eni artırıldı; çarpışma kalınlığı yeni görüntüye uyarlandı. Dış ağaç, çalı ve kaya sprite sınırları çitin dışındaki açık yeşil şeritle sınırlandı.
- `StoneRoadTilemap` A* yol hücrelerini hesaplamaya devam eder, fakat `TilemapRenderer` devre dışıdır. Bina ekleme veya taşıma sonrasında kahverengi yol görünmez.
- Kullanıcının 11 basamaklı maden görseli tasarım referansı alındı. Oyunda madenin mevcut üst sınırı 8 olduğundan `mine_stage_1.png`–`mine_stage_8.png` olarak sekiz özgün saydam piksel görseli üretildi; ilerleme ve kayıt verisi değiştirilmedi. `mine_lift_box.png` ayrı hareket eden altın kutusudur. Kutu her seviyede yukarı/aşağı gider; depo doluluğu kutunun parlaklığını ve üstündeki altın katmanını etkiler. Kazma yapan Pawn, toz/parıltı, dolu depo balonu ve toplama akışı korunur.
- Üretim aracı: yerleşik `image_gen`. Prompt seti: 1 açık mağara ve kaba ahşap; 2–4 giderek kapanan çatı, taş taban ve demir bağlantı; 5–7 vinç ve metal donatı basamakları; 8 bacalı/dişlili gelişmiş maden; ayrı açık üstlü, altın dolu küçük vinç kutusu. Tüm görseller üç çeyrek açıyla, kahverengi ahşap, gri taş, mavi gri demir ve amber altın paletiyle, saydam arka planda üretildi. Dosyalar `Assets/Resources/CustomBaseArt/` içindedir. Inspector'daki `mainSprite`, `levelSprites` ve `liftBoxSprite` alanları değiştirilebilir.

## Doğrulama

- `ViewportRevisionTests.BaseViewportAndRecommendedLayout`: 1 geçti, 0 başarısız (`mine-layout-final-results.xml`). Sekiz seviyede doğru sprite seçimi, kutu hareketi, görünmeyen yol ve dış dekor sınırları denetlendi.
- Tüm Unity PlayMode testleri: 16 geçti, 0 başarısız (`mine-complete-results.xml`).
- Görüntüler Unity kamera/Canvas çıktısından 390×844 RenderTexture ile alındı; fiziksel cihazda dokunma ve performans ölçümü yapılmadı.
- Bölge 1 düşmanları/boss'u, sprite/animasyonları, `RegionData`/`LevelData` ve mevcut savaş davranışı değiştirilmedi. Kirli çalışma ağacı sıfırlanmadı.

Referanstaki görsellerin birebir kullanımı için bunların kaynak sprite dosyaları ve kullanım hakkı gerekir. Bu çalışma için kullanıcıdan ek dosya gerekmiyor.
