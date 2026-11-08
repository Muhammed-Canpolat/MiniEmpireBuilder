# Ana Üs arayüz ve dünya incelemesi — 390×844

Her madde için Unity Game view görüntüsü alındı. Görüntüler mevcut kaydın Ana Üs seviye 5 durumunu gösterir; ilk seviye temiz alanı kodda 18×19 hücredir.

## 1. Paneller

Kapanış animasyonunun sonunda panel devre dışı kalır; `CanvasGroup` ışın ve etkileşimi keser. Simulator'da İnşa menüsü açılıp kapatıldı, alt kenarda artık panel çizgisi görünmüyor. [Kapalı panel görüntüsü](item-01.png).

Değişen dosyalar: `Assets/Scripts/UI/PanelController.cs`, `Assets/Scripts/Base/BuildMenuController.cs`, `Assets/Scripts/UI/BaseWorldUI.cs`, `Assets/Scripts/Campaign/CampaignSceneSetup.cs`, `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`, `Assets/Tests/PlayMode/BasePresentationTests.cs`.

## 2. Dünya düzeni

22×36 dünya, 18×32 iç alan, 18×19 ilk temiz alan ve istenen altı slot koordinatı uygulandı. Kapalı alanlar Tiny Swords engelleriyle dolu; dokununca seviye bildirimi açılır. Seviye artışında işçi temizleme, toz ve kısa kamera sarsıntısı uygulanır. [Dünya görüntüsü](item-02.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseWorldLayout.cs`, `Assets/Scripts/Base/BaseSceneSetup.cs`, `Assets/Scripts/Base/BaseMapManager.cs`, `Assets/Scripts/Base/BuilderVisualController.cs`, `Assets/Scripts/Base/HeroMovementController.cs`, `Assets/Scripts/Base/PlacementController.cs`.

## 3. Çit halkası

Yatay ve dikey segmentler ile köşeler bütün halkayı kaplar. Debug katmanında tüm çit hücreleri yeşil, **Çit boşluğu: 0**. [Çit debug görüntüsü](item-03.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseMapManager.cs`, `Assets/Scripts/Base/BaseDebugOverlay.cs`.

## 4. Etiketler

Sürekli bina adları kaldırıldı; dokunulan İnşaatçı etiketi ve seviye rozeti kısa süre görünür. Ana Üs'te küçük seviye rozeti kalır. [Dokunma görüntüsü](item-04.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseMapManager.cs`, `Assets/Scripts/Base/BuilderVisualController.cs`, `Assets/Scripts/Base/BaseSceneSetup.cs`.

## 5. Işık ve atmosfer

Binalara yumuşak elips gölge, sahneye hafif sıcak ton ve kenar vignette eklendi. Tiny Swords çim karoları iki yakın yeşil tonla karıştırıldı. [Atmosfer görüntüsü](item-05.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseSceneSetup.cs`, `Assets/Scripts/Base/BaseMapManager.cs`.

## Test ve sınırlar

`BasePresentationTests` PlayMode paketi dört çözünürlükte her panel için 10 açma/kapama döngüsünü, `RectTransform.GetWorldCorners` ile ekran dışında kapanmayı, `ExecuteEvents.Execute` ile açılış sırasında buton tıklamasını, ekonomi denkliğini ve footprint çakışması/buildable denetimini kapsar.

Unity Test Runner sonucu (19 Ekim 2026): **3 geçti, 0 başarısız, 0 atlandı**. `git diff --check` görev dosyalarında boşluk hatası bulmadı.

Görüntü alma aracı: `Assets/Editor/BaseReviewCapture.cs`. Önceden çalışma ağacında bulunan değişiklikler sıfırlanmadı. Bölge 1 düşmanları, boss, sprite/animasyon/stat, RegionData/LevelData ve savaş mantığına bu çalışmada dokunulmadı.
