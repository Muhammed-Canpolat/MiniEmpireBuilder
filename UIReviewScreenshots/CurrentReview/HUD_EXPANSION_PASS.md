# Sahiplik, arazi ve HUD incelemesi

Tarih: 22.10.2026. Dal: `ui/dark-fantasy-hud`. 390×844 kamera ve Canvas çıktıları aşağıdadır. F1 katmanı Ana Üs görsellerinde açıktır.

## 1. Bina sahipliği ve savaş

Görsel: [Savaş yuvası ve 0/0 hak](ownership-item-01.png). Kule hakkı kayıtlı üs binalarından hesaplanır. Savaş altını maliyeti ve net kazanç akışı korunur. Savaş kulesi en yüksek kayıtlı tür seviyesinin görselini kullanır. Okçu/Topçu/Manastır üst limitleri `BuildingData.maxCountByBaseLevel` üzerinden gelir. Eski kayıtta yerleştirilmiş kule yoksa hak sıfırdır.

Dosyalar: `Assets/Scripts/Data/GameData.cs`, `Assets/Scripts/Managers/GameManager.cs`, `Assets/Scripts/Battle/BattleManager.cs`, `Assets/Scripts/Battle/BattleBuildSlot.cs`, `Assets/Tests/PlayMode/HudExpansionReviewTests.cs`.

## 2. Ekran kenarları ve alt bar

Görsel: [Ana Üs HUD](viewport-item-02.png). Üst/alt kenar dolumu ve kamera dikdörtgeni safe area üzerinden hesaplanır. Alt bar 92 birimdir; butonlar ortadadır. Üst bar sprite kaynaklı koyu blok kaldırıldı.

Dosyalar: `Assets/Scripts/Polish/UI/ReferenceUiVisuals.cs`, `DarkFantasyHudPolish.cs`, `Assets/Scripts/Base/BaseSceneSetup.cs`.

## 3. Çit ve kapalı arazi

Görsel: [Arazi ve F1 ızgarası](land-item-03.png). Dünya 22×38, iç alan 18×34; çit 1/20 sütunlarında ve 1/36 satırlarındadır. Açılabilir satırlar üs seviyesi 5'te tamamen temizlenir; uçlardaki ikişer satır kalıcı kalır. Sabit tohumlu kümeler, karartma, sis, temizleme tozu ve banner uygulanır.

Dosyalar: `Assets/Scripts/Base/BaseWorldLayout.cs`, `BaseMapManager.cs`, `BaseSceneSetup.cs`, `BaseDebugOverlay.cs`, `BaseLevelBanner.cs`.

## 4. İnşa menüsü

Görsel: [YAPI menüsü](build-menu-item-04.png). Başlık, ayrı sekme satırı, 44×44 kapama, %50 blocker ve kartların kurulu sayısı eklendi. Kilit bilgisi yalnızca kilitli yapı bulunduğunda görünür.

Dosyalar: `Assets/Scripts/Base/BuildMenuController.cs`, `Assets/Scripts/UI/BaseWorldUI.cs`, `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`.

## 5. Taşıma modu

Görsel: [Bina taşıma önizlemesi](move-item-05.png). 0,4 sn basma ile bina/dekor sürüklenir. Ana Üs sabittir. Yerleşim temiz alana ve çakışmayan footprint'e göre denetlenir; konum kayda yazılır. Geçersiz bırakma geri döner, geçerli bırakma üç saniyelik geri alma sunar.

Dosyalar: `Assets/Scripts/Data/PlayerSaveData.cs`, `Assets/Scripts/Base/BaseMapManager.cs`, `HeroMovementController.cs`, `LongPressHandler.cs`, `PlacementController.cs`, `DecorPlacementController.cs`, `BaseSceneSetup.cs`.

## 6. Savaş düzeni

Görsel: [Savaş ve üçüncü Çırak sütunu](battle-item-06.png). Kamera HUD ile alt panel arasına kırpıldı, yuvalar içeri alındı. Çırak HUD etiketi alt panelde; takip mesafesi ve boyu küçültüldü.

Dosyalar: `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`, `Assets/Scripts/Battle/CameraFollow.cs`, `BattleManager.cs`, `BattleBuildSlot.cs`, `Assets/Scripts/Base/BattleApprentice.cs`.

## 7. Çırak statları

Görsel: [Çırak yükseltme önizlemesi](apprentice-item-07.png). Can, hasar, seviye çarpanları ve maden yüzdeleri `ApprenticeProgressionData` varlığından okunur. Panel ve savaş aynı hesaplamayı kullanır.

Dosyalar: `Assets/Scripts/Base/ApprenticeProgressionData.cs`, `Assets/Resources/ApprenticeProgressionData.asset`, `Assets/Scripts/Base/BattleApprentice.cs`, `Assets/Scripts/Managers/GameManager.cs`, `Assets/Scripts/UI/BaseWorldUI.cs`.

## Doğrulama

- `HudExpansionReviewTests`: 2 geçti, 0 başarısız. Hak sayısı, tek kule sınırı, seviye görseli, 22×38 alan, kamera kırpma, footprint reddi, konum kaydı/yüklemesi, Çırak çarpanları ve panel değeri denetlendi. Sonuç: `hud-expansion-results.xml`.
- Son tam PlayMode koşusu: **9 geçti, 0 başarısız**. Sonuç: [verified-playmode-results.xml](verified-playmode-results.xml). Eski görsel ve dekor testleri, önceki testlerin kayıt durumuna bağımlı olmadan kendi sahne ön koşullarını kuracak biçimde güncellendi (`Assets/Tests/PlayMode/BaseVisualCaptureTests.cs`, `ExpansionFeatureTests.cs`).
- Görseller, batch PlayMode içinde sahne kamerası ve Canvas'ın 390×844 RenderTexture çıktılarıdır; cihaz dokunması, çentik ve performans fiziksel cihazda ölçülmedi.
- Bölge 1 düşmanları/boss'u, sprite ve animasyonları, `RegionData`/`LevelData` içeriği ve `EnemyController` değiştirilmedi.
