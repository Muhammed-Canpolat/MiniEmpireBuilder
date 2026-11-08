# Ana Üs referans görünümü — 22.10.2026

## Görseller

- [390×844 Ana Üs, seviye 5](reference-base-level5.png): taş direkli ahşap çit, aralıklı lambalar, çit dışındaki ağaç ve kayalar, çerçeveli alt düğmeler ve yeni maden.
- [390×844 maden yakın planı](reference-mine-closeup.png): altın damarları, destekli tünel, araba ve kazma animasyonundaki Pawn.

## Uygulama

- Dört özgün saydam piksel görseli `image_gen` ile üretildi: `mine_gold_arch.png`, `fence_oak_rail.png`, `fence_stone_pillar.png`, `fence_lantern.png`. Prompt seti: sıcak lambalı gri kaya/altın maden girişi ve arabası; demir bağlantılı çift meşe ray; yontulmuş gri taş direk; sıcak amber fener. Görseller `Assets/Resources/CustomBaseArt/` içindedir. Unity içe aktarma ayarı piksel görünümü ve alfa kesimini korur. Madenin `mainSprite` alanı Inspector'dan değiştirilebilir.
- Mevcut çit ID'leri, kaldırma kaydı ve fizik nesneleri korunarak görsel parçalar değiştirildi. Çit dışında çarpışmasız ağaç, çalı ve kaya oluşturuldu. Alt dock çelik çerçeve, perçin ve daha yüksek düğmelerle düzenlendi; mevcut düğme dinleyicileri korunuyor.
- Değişen kod: `Assets/Scripts/Base/BaseCustomArt.cs`, `BaseMineVisual.cs`, `BaseMapManager.cs`, `BaseSceneSetup.cs`, `Assets/Editor/BaseCustomArtImporter.cs`, `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`, `Assets/Tests/PlayMode/ViewportRevisionTests.cs`, `BaseVisualCaptureTests.cs`, `EnvironmentAudioReviewTests.cs`.

## Doğrulama

- Unity PlayMode `ViewportRevisionTests`: 2 geçti, 0 başarısız (`reference-art-final-results.xml`).
- Son ağaç ölçeğiyle Unity PlayMode `BaseViewportAndRecommendedLayout`: 1 geçti, 0 başarısız (`reference-base-final-results.xml`).
- Tüm Unity PlayMode testleri: 16 geçti, 0 başarısız (`reference-all-final-results.xml`). İki eski maden görsel testi yeni birleşik sprite yapısına göre güncellendi.
- Görüntüler Unity kamera ve Canvas çıktısından 390×844 RenderTexture ile alındı. Fiziksel cihazda dokunma ve performans ölçümü yapılmadı.
- Bölge 1 düşmanları/boss'u, sprite/animasyonları, `RegionData`/`LevelData` ve mevcut savaş davranışı bu çalışmada değiştirilmedi. Kirli çalışma ağacı sıfırlanmadı.
