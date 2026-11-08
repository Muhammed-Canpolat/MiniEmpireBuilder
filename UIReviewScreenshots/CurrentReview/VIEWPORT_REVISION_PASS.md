# Kamera, yerleşim ve görsel düzeltme incelemesi

Tarih: 22 Ekim 2026. Dal: `ui/dark-fantasy-hud`.

## Sonuç ve ölçüm sınırı

- Tam PlayMode koşusu: **16/16 geçti** (`revision-final-results.xml`). Son nöbetçi ölçeği ve kapı hizası değişikliğinden sonra ilgili iki test **2/2 geçti** (`revision-visual-results.xml`).
- Başsız Unity Game view boyutu **640×480** kaldı. `Screen.SetResolution` burada Game view boyutunu değiştirmedi. 390×844, 360×800, 412×915 ve 768×1024 için kamera sığdırma hesabı PlayMode içinde ayrı doğrulandı; bu dört çözünürlükte gerçek Game view testi yapıldığı iddia edilmiyor. PNG'ler **390×844 RenderTexture bileşimidir**, Game view ekran yakalaması değildir. Unity Editor arayüz köprüsü kullanılamadığı için gerçek 390×844 Game view kareleri alınamadı.
- F1 gerçek başsız ölçümü: ekran ve safe area **640×480**; üst bar **61,8 px**, alt bar **88,9 px**; `Camera.pixelRect=(0, 88,88, 640, 329,29)`, `aspect=1,944`, `orthographicSize=19`; dünya+çit piksel sınırı **(225,89)–(415,418)**. Üst/alt çit boşluğu **0 px**. Raycast hedefleri: BuildBtn, AudioSettingsButton, BattleBtn, AvatarIcon, CoinIcon; tam dikdörtgenler `revision-final-test.log` içindeki `[ViewportDiagnosis]` satırında.
- Dört hedef boyut için hesaplanan ortografik boyut **19**, görünür dünya yüksekliği **38 hücre**; genişlik en az **22 hücre**. Gerçek 640×480 Game view'da HUD kenarları `pixelRect` ile ±1 px hizalı ve raycast hedeflerinin dünya alanını örtme payı %2 altında.
- Sisli Orman bileşik görüntü ortalama parlaklığı **0,304** (hedef ≥0,22).

## Madde bazında görseller ve değişiklikler

| Madde | Debug açık | Debug kapalı | Değişen dosyalar / doğrulama |
|---|---|---|---|
| 1. Kamera / viewport | [açık](revision-01-camera-debug.png) | [kapalı](revision-01-camera-clean.png) | `ReferenceUiVisuals.cs`, `BaseDebugOverlay.cs`, `BattleDebugOverlay.cs`, `BaseSceneSetup.cs`, `ViewportRevisionTests.cs`. Kamera `pixelRect` değerini HUD piksel kenarlarından kuruyor; F1 tanısı ve dört boyut hesap testi eklendi. Sev. 5 doğal sınır bandı ve tam açık iç alan kontrol edildi. |
| 2. Önerilen yerleşim | [açık](revision-02-layout-debug.png) | [kapalı](revision-02-layout-clean.png) | `BaseWorldLayout.cs`, `BaseMapManager.cs`, `BuildMenuController.cs`. Yeni oyun koordinatları, onaylı düzen butonu, kaydedilmiş konumların korunması, dekorların yerinde kalması ve madenin çitten bir hücre boşluğu doğrulandı. |
| 3. Taş yol | [açık](revision-03-road-debug.png) | [kapalı](revision-03-road-clean.png) | `BaseGroundPaths.cs`. Gri kare sprite yerine sürekli kahverengi taş döşeme; mevcut A* güzergâhı bina footprint'lerinden kaçıyor. |
| 4. Sisli Orman | [açık](revision-04-forest-debug.png) | [kapalı](revision-04-forest-clean.png) | `BattleRegionEnvironment.cs`, `DarkFantasyHudPolish.cs`. Bölge 2 zemin tonu, merkezde saydam sis ve sınırlı vignette; parlaklık 0,304. Bölge 1 içerik verisi değişmedi. |
| 5. Çırak | [açık](revision-05-apprentice-debug.png) | [kapalı](revision-05-apprentice-clean.png) | `BattleApprentice.cs`, `BattleDebugOverlay.cs`. Sprite, kahramanın %75 boyu, sol arka konum, sıralama ve F1 işareti test edildi. |
| 6. Garnizon | [açık](revision-06-garrison-debug.png) | [kapalı](revision-06-garrison-clean.png) | `BaseGarrisonVisual.cs`, `BaseMapManager.cs`, `Garrison_MainBase.prefab`. Aktörler büyütüldü, kapı önüne indirildi; çapa noktaları F1'de. Her aktörde sprite, Idle/eylem clip'leri ve 6 saniye içinde eylem sayacı kontrol edildi. |
| 7. Ana Üs UI | [açık](revision-07-base-ui-debug.png) | [kapalı](revision-07-base-ui-clean.png) | `AudioSettingsUI.cs`, `DarkFantasyHudPolish.cs`, `BaseDebugOverlay.cs`. Ses düğmesi üst bara taşındı, F1 yalnız Editor/Development için derleniyor. |
| 7. Savaş UI | [açık](revision-07-battle-ui-debug.png) | [kapalı](revision-07-battle-ui-clean.png) | `BattleApprentice.cs`, `BattleBuildSlot.cs`, `BattleWorldMarkers.cs`, `DarkFantasyHudPolish.cs`, `BattleDebugOverlay.cs`. Çırak sütunu, pill chip'ler, yuva etiketi ve üst bar ses düğmesi gözden geçirildi. |

Asker yakın planları: [okçu 1](revision-06-soldier-00-Garrison_PracticeArcher.png), [çatı okçusu 1](revision-06-soldier-01-Garrison_RoofArcher.png), [kapı nöbetçisi 1](revision-06-soldier-02-Garrison_DoorGuard.png), [çatı okçusu 2](revision-06-soldier-03-Garrison_RoofArcher.png), [okçu 2](revision-06-soldier-04-Garrison_PracticeArcher.png), [topçu askeri](revision-06-soldier-05-Garrison_CannonCrew.png), [kapı nöbetçisi 2](revision-06-soldier-06-Garrison_DoorGuard.png), [keşiş](revision-06-soldier-07-Garrison_Monk.png). Görseller eylem karesine denk gelmek zorunda değildir; eylem oynatımı sayaçla doğrulandı.

## PlayMode testleri

`revision-final-results.xml` içindeki testlerin tamamı geçti: `BuildingFootprintsStayInsideInteriorAndNeverOverlap`, `EconomyNetEqualsEnemyGoldPlusRewardMinusBuildCost`, `PanelsCloseCompletelyAndButtonsWorkAtMobileResolutions`, `BattleCosmeticsAttachWithoutCombatChanges`, `CaptureCurrentBaseGameView`, `AudioSettingsShowsAndPersistsControls`, `CaptureMineAndGarrison`, `CapturePatchesAndRoads`, `ForestIdentityLeavesRegionOneUntouched`, `LevelVisualsUseCatalogSprites`, `DecorCatalogGridAndTabs`, `MonasteryAndApprenticeFriendlyBehavior`, `BaseHudLandMenuMoveAndApprenticeReview`, `OwnershipBattleCardAndLevelVisual`, `BaseViewportAndRecommendedLayout`, `ForestBattleViewportAndApprentice`. Son görsel düzeltmeyi `revision-visual-results.xml` içindeki son iki test yeniden doğruladı.

Test uyarlamaları: `HudExpansionReviewTests.cs` Sev. 5 tam açık alan beklentisine, `EnvironmentAudioReviewTests.cs` madenin yeni geçerli konumuna, `ExpansionFeatureTests.cs` yeni Çırak HUD adına güncellendi. Yeni `ViewportRevisionTests.cs` kamera, yerleşim, yol, garnizon, orman, Çırak ve HUD denetimini içerir.

## Referansa göre kalan farklar

- Ekran görüntüleri gerçek 390×844 Game view'dan değil, 390×844 RenderTexture bileşiminden alındı. Nihai cihaz/Game view onayı hâlâ gerekir.
- 640×480 başsız ortamda dikey telefon düzeninin tüm gerçek HUD aralıkları ölçülemedi; dört boyut için yalnız kamera sığdırma matematiği sınandı.
- Asker eylemleri çalışıyor; tekil PNG'ler anlık görüntü olduğu için animasyonun hareketini göstermiyor.

Bölge 1 düşmanları/boss'u, sprite ve animasyonları, RegionData/LevelData içeriği, düşman davranışı ve savaş mantığı bu geçişte değiştirilmedi. Çalışma ağacı sıfırlanmadı.
