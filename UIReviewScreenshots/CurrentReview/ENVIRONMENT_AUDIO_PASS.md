# Maden, garnizon, zemin ve ses incelemesi

Tarih: 22.10.2026. Dal: `ui/dark-fantasy-hud`. Her madde için 390×844 kamera/Canvas karesi alındı. Ana Üs karelerinde F1 ölçüm katmanı açık.

## 1. Maden yeniden kurulum

Görsel: [4×4 maden](new-item-01-mine.png). Eski altın kaya kümesi kaldırıldı; gri kaya kütlesi, tünel, araba, gömülü altın, kazmalı Pawn ve doluluk balonu ayrı katmanlar. `BaseMineVisual.prefab` üzerinde `tunnelSprite`, `cartSprite`, `greyRockSprite`, `goldVeinSprite`, `minerController` ve madenci için `minerAnchorCell` Inspector alanları var. Özel görseller gelene kadar gri kutu ve siyah elips kullanılıyor. Depo üretimi ve toplama miktarı GameManager akışında kalıyor; toplama ikonlarının üst bara uçuşu mevcut UI animasyonuyla sürüyor. Eski varsayılan 3×3 maden koordinatları 4×4 merkeze taşınıyor, oyuncunun taşıdığı konumlar korunuyor.

Dosyalar: `Assets/Scripts/Base/BaseMineVisual.cs`, `BaseWorldLayout.cs`, `BaseMapManager.cs`, `BaseApprenticeVisual.cs`, `Assets/Resources/BaseMineVisual.prefab`, `Assets/Editor/BasePresentationPrefabBuilder.cs`, `Assets/Tests/PlayMode/BaseVisualCaptureTests.cs`.

## 2. Garnizon

Görseller: [garnizon ve F1 çapaları](new-item-02-garrison.png), [temiz garnizon karesi](new-item-02-garrison-clean.png). Ana Üs kapı nöbetçileri, iki kule okçusu, Okçu Evi talimcileri, Topçu personeli ve Manastır Monk’u footprint koordinatlarıyla yerleşiyor. Çapa noktaları ilgili `Garrison_*.prefab` dosyalarında Inspector’dan değiştirilebilir; F1’de renkli noktalar olarak görünüyor. Var olan Tiny Swords Animator Controller’larında idle/eylem durumları kullanılıyor; Cull Update Transforms açık.

Dosyalar: `Assets/Scripts/Base/BaseGarrisonVisual.cs`, `BaseMapManager.cs`, `BaseDebugOverlay.cs`, `Assets/Resources/Garrison_*.prefab`.

## 3. Zemin yaması ve taş yol

Görsel: [yama ve yol](new-item-03-patches-roads.png). `YamaTilemap`, footprint çevresine 0,6 hücre taşan yuvarlak koyu toprak çiziyor. `StoneRoadTilemap`, mevcut Taş yol dekor sprite’ını kullanıyor. Ana Üs kapısından binalara A* ile, footprint’lere girmeyen yollar çiziliyor; yer değiştirme ve kaldırma sonrası yenileniyor. Çim iki yakın ton arasında sabit hücre karmasıyla dağıtılıyor.

Dosyalar: `Assets/Scripts/Base/BaseGroundPaths.cs`, `BaseSceneSetup.cs`, `BaseMapManager.cs`, `Assets/Tests/PlayMode/EnvironmentAudioReviewTests.cs`.

## 4. Seviye rengi

Görsel: [Ana Üs seviye 5 sprite](new-item-04-level-five.png). Animator’ın eski sprite karesini geri yazması düzeltildi. Her seviyede `BuildingData.levels` sprite’ı görsel katmanda sabitleniyor; test Ana Üs, Okçu Evi, Topçu Kulesi ve Manastır için 1–5 seviyelerini, madenin de seviye renk temasını denetliyor. Bu karede test sprite’ı seviye 5’e geçirir; kayıt/UI test kurulumu seviye 1’dir.

Dosyalar: `Assets/Scripts/Base/BaseBuildingVisual.cs`, `Assets/Tests/PlayMode/EnvironmentAudioReviewTests.cs`.

## 5. Savaş bölge kimliği

Görsel: [Sisli Orman](new-item-05-forest.png). Bölge 2 zemini mevcut `RegionData.groundTile` kaynağından okunuyor, koyu yeşil ton, yaprak/ağaç gölgeleri ve merkezde dört hücre net kalan radial sis uygulanıyor. Bölge 1 için görsel bileşen hiçbir zemin değişikliği yapmıyor; diğer bölgeler `colorTone` ile tonlanıyor.

Dosyalar: `Assets/Scripts/Battle/BattleRegionEnvironment.cs`, `BattleSceneSetup.cs`, `Assets/Tests/PlayMode/EnvironmentAudioReviewTests.cs`.

## 6. Ses

Görsel: [ses ayarları](new-item-06-audio-settings.png). AudioManager UI, yerleşme, altın, seviye, kahraman vuruşu, düşman ölümü ve zafer sesleri için boş Inspector clip alanları sunuyor. Ana Üs müziği ile rüzgar/kuş döngüleri ve savaş müziği için bir saniyelik geçiş hazır. Müzik ve efekt aç/kapa PlayerPrefs ile saklanıyor. Savaş ses gözlemcisi yalnızca mevcut durumları okuyor.

Dosyalar: `Assets/Scripts/Polish/Core/AudioManager.cs`, `AudioSettingsUI.cs`, `BattleAudioObserver.cs`, `ScenePolishInstaller.cs`, `Assets/Scripts/Base/PlacementController.cs`, `BaseSceneSetup.cs`, `BaseGarrisonVisual.cs`, `Assets/Scripts/Managers/GameManager.cs`, `Assets/Scripts/UI/BattleUI.cs`.

## Doğrulama

Tam PlayMode koşusu: **14 geçti, 0 başarısız**. Sonuç: [verified-final-pass-results.xml](verified-final-pass-results.xml). Slot çakışması, yolun taşınma sonrası yenilenmesi, bölge 1 zemininin korunması, ses paneli ve seviye görselleri denetlendi. Son ses dinleyicisi değişikliği ayrıca [hedefli testte](audio-listener-results.xml) geçti (1/1). Testler ayrı Unity kopyasında, farklı PlayerPrefs ürün adıyla çalıştırıldı. Görseller batch PlayMode içinde 390×844 RenderTexture çıktılarıdır. Fiziksel cihazda dokunma, çentik ve ses karışımı henüz ölçülmedi. Özel maden sprite’ları ve ses dosyaları atanmadığı için yer tutucu görseller ve sessiz clip alanları kullanılıyor.

Bölge 1 düşmanları/boss’u, sprite ve animasyonları, `RegionData`/`LevelData` içeriği ve düşman davranışı değiştirilmedi.
