# Ana üs ve savaş UI düzeltme geçişi — 23.10.2026

Dal: `ui/dark-fantasy-hud`. Çalışma ağacı sıfırlanmadı. Bölge 1–5 düşman/boss sprite ve davranışları, `RegionData`/`LevelData` içerikleri ve savaş ekonomi formülü bu geçişte değiştirilmedi. Görüntüler Unity Game view 390×844 çözünürlüğünde test koşusundan alındı.

## 1. Maden arkasındaki siyah kare

F1 tanısına F2 Yama, F3 bina sprite'ı, F4 gölge anahtarları eklendi. Taşınmış maden üzerinde katmanlar tek tek kapatıldı. **Belirgin 4×4 karenin gerçek kaynağı**, boş Okçu Kulesi yeri için `CreateFutureBuildingPads()` tarafından üretilen opak `Foundation` SpriteRenderer idi: maden bu eski temelin üstüne taşınabiliyordu. `YamaTilemap` koyuluğu ayrıca artırıyordu. İkisi kaldırıldı. `diagnostic-*.png` ve [renderer dökümü](diagnostic-renderers.txt) tanı kayıtlarıdır.

Gölge artık editörde kaydedilen 64×64 gerçek alfaya sahip [blob_shadow.png](../../Assets/Art/FX/blob_shadow.png) ve [BaseGroundShadow.prefab](../../Assets/Resources/BaseGroundShadow.prefab) üzerinden `ZeminGolgesi` child'ı olarak ekleniyor. Bina ile taşınıyor/siliniyor/yükleniyor. Maden import ayarları ve bina PNG köşe alfa uyarısı editör kodunda. Üç farklı taşıma + her adımda yükleme + Maden/Okçu/Topçu/Manastır için RenderTexture piksel kontrolleri geçti.

Görüntüler: [ilk görünüm](item-01-base-game-view.png), [taşıma 1](item-01-mine-move-1.png), [taşıma 2](item-01-mine-move-2.png), [taşıma 3](item-01-mine-move-3.png), [dört yapı gölgesi](item-01-all-building-shadows.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseDebugOverlay.cs`, `BaseGroundPaths.cs`, `BaseMapManager.cs`, `BaseSceneSetup.cs`, `BuilderVisualController.cs`, `Assets/Editor/BaseShadowAssetGenerator.cs`, `Assets/Art/FX/blob_shadow.png`, `Assets/Resources/BaseGroundShadow.prefab`, `Assets/Tests/PlayMode/BaseShadowRevisionTests.cs` ve maden PNG importer `.meta` dosyaları.

## 2. Savaş ayarları ve pes etme

Dişli 48×48 dokunma alanıyla modalı açıyor. Müzik/efekt, Devam Et ve kırmızı Pes Et; arka blocker ve `Time.timeScale = 0`; onay; tek seferlik pes etme koruması eklendi. Pes etme mevcut yenilgi akışını kullanıyor; düşman/mermiler temizleniyor, zaman yeniden akıyor. Sonuçta yıldız yok, Tekrar Dene ve Bölgeye Dön çalışıyor. Testte altın, mevcut yenilgi kuralıyla aynı kaldı.

Görüntüler: [ayarlar](item-02-settings-modal.png), [pes etme onayı](item-02-forfeit-confirm.png), [yenilgi](item-02-defeat.png).

Değişen dosyalar: `Assets/Scripts/Polish/Core/AudioSettingsUI.cs`, `Assets/Scripts/Battle/BattleManager.cs`, `BattleBuildSlot.cs`, `Assets/Scripts/UI/VirtualJoystick.cs`, `Assets/Scripts/Polish/UI/VictoryReferenceView.cs`, `Assets/Tests/PlayMode/BattleSettingsRevisionTests.cs`.

## 3. Savaş HUD

Yuvalar büyütüldü, pill etiket ve yakınlık nabzı eklendi. Mini harita kaldırıldı; kamera dışı düşman için kırmızı yön oku editörde kaydedilmiş sprite ile çiziliyor. Dalga geri sayımı, düşman chip ikonu, kayda bağlı ilk kule ipucu, çırak ayrımı ve zemin kontrastı düzenlendi. Ok, HUD başlığından ve ekran kırpılmasından uzak tutuldu.

Görüntüler: [savaş HUD](item-03-battle-ui.png), [kamera dışı yön oku](item-03-offscreen-arrows.png).

Değişen dosyalar: `Assets/Scripts/Battle/BattleBuildSlot.cs`, `BattleSceneSetup.cs`, `BattleManager.cs`, `Assets/Scripts/Base/BattleApprentice.cs`, `Assets/Scripts/Polish/Battle/BattleWorldMarkers.cs`, `BattleOffscreenIndicators.cs`, `Assets/Editor/BattleArrowAssetGenerator.cs`, `Assets/Resources/Battle/enemy_direction_arrow.png`, `Assets/Scripts/UI/BattleUI.cs`, `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`, `Assets/Scripts/Data/PlayerSaveData.cs`, `Assets/Tests/PlayMode/BattleSettingsRevisionTests.cs`.

## 4. Ana dünya

Ana Üs kapısından yapılara sürekli taş yol; bina taşıma sonrası A* yeniden hesaplama; çit payı ve eski kayıt yerleşim onarımı; üst/alt çit yüksekliği; alt bardaki gri mavi `BottomDock` şeridi; Ana Üs seviye tavanı 5 ve kayıt göçü; sabit tohumlu üç çim tonu/döndürme/yansıma ve %6 küçük leke; önerilen düzene dört dekor kümesi eklendi. Pembe lekelerin kaynağı `flowers_placeholder.png` idi; gerçek çim/taş görseliyle değiştirildi.

Görüntü: [yol, çim ve çevre](item-04-base-roads-grass.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseGroundPaths.cs`, `BaseMapManager.cs`, `BaseSceneSetup.cs`, `DecorPlacementController.cs`, `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`, `Assets/Scripts/Data/GameData.cs`, `Assets/Scripts/Managers/GameManager.cs`, `Assets/Tests/PlayMode/HudExpansionReviewTests.cs`, `EnvironmentAudioReviewTests.cs`, `ViewportRevisionTests.cs`.

## PlayMode doğrulaması

Son tam koşu: **19 geçti, 0 başarısız**. Ham sonuç: [final-all-3-results.xml](final-all-3-results.xml).

| Test | Sonuç |
|---|---|
| BuildingFootprintsStayInsideInteriorAndNeverOverlap | Geçti |
| EconomyNetEqualsEnemyGoldPlusRewardMinusBuildCost | Geçti |
| PanelsCloseCompletelyAndButtonsWorkAtMobileResolutions | Geçti |
| DiagnoseMovedMineLayers | Geçti |
| ShadowsRemainSoftAfterMovesAndReloads | Geçti |
| BattleCosmeticsAttachWithoutCombatChanges | Geçti |
| CaptureCurrentBaseGameView | Geçti |
| ForfeitUsesDefeatFlowAndKeepsGold | Geçti |
| AudioSettingsShowsAndPersistsControls | Geçti |
| CaptureMineAndGarrison | Geçti |
| CapturePatchesAndRoads | Geçti |
| ForestIdentityLeavesRegionOneUntouched | Geçti |
| LevelVisualsUseCatalogSprites | Geçti |
| DecorCatalogGridAndTabs | Geçti |
| MonasteryAndApprenticeFriendlyBehavior | Geçti |
| BaseHudLandMenuMoveAndApprenticeReview | Geçti |
| OwnershipBattleCardAndLevelVisual | Geçti |
| BaseViewportAndRecommendedLayout | Geçti |
| ForestBattleViewportAndApprentice | Geçti |

## Referanstan kalan farklar

- Yol, kesintisiz kahverengi zemin üzerinde mevcut taş yolu dekor sprite'ını kullanıyor; referanstaki özel organik taş çizimi değil.
- Çiçek dekorunun kaynak sprite'ı pembe placeholder olduğu için önerilen dört kümede çiçek yerine kaya kullanıldı. Gerçek çiçek sprite'ı geldiğinde küme tekrar çiçek içerebilir.
- Yapılar bu projede sahnede kodla oluşturulduğundan gölge, bina oluşturulurken kayıtlı gölge prefab'ından child olarak ekleniyor; ayrı ayrı serileştirilmiş bina prefab'ları yok.
- Madenin mevcut çizimi, önce paylaşılan 11 seviyeli referansın birebir kopyası değil.
