# Dekor, Manastır ve Çırak incelemesi

Tarih: 21.10.2026. Çalışma dalı: `ui/dark-fantasy-hud`.

## 1. Dekor sistemi

390×844 görüntü: [Dekor menüsü](expansion-decor-menu.png).

YAPI/DEKOR sekmeleri, altın seçili çizgisi ve sekiz veri tabanlı dekor eklendi. Ağaç, çalı ve kaya Tiny Swords görsellerini kullanır; diğer beş dekor geçici sprite kullanır. Yerleştirme önizlemesi ve footprint hücreleri uygunluğa göre yeşil/kırmızı görünür. Onay/iptal çubuğu, dolu veya kapalı hücre denetimi, kayıt ve uzun basarak taşıma/silme vardır. Silme maliyetin yarısını iade eder. Fener cihaz saati 19:00–06:00 arasında hafif titreşen ışık verir.

Dosyalar: `Assets/Scripts/Base/DecorData.cs`, `DecorPlacementController.cs`, `BuildMenuController.cs`, `BaseSceneSetup.cs`; `Assets/Scripts/Data/PlayerSaveData.cs`; `Assets/Editor/BuildDecorAssets.cs`; `Assets/Resources/Decor/` (8 veri varlığı ve 5 geçici PNG); `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`.

## 2. Manastır

390×844 görüntü: [Savaş sahnesi](expansion-battle-friendly.png). Manastır ve Monk kahramanın sağ altında görünür.

Duvarcı bina verisi, kartı ve sprite bağlantısı kaldırıldı. Ana Üs seviye 4'te açılan Manastırın maliyeti 60. Savaş yuvasında Okçu 50, Topçu 80, Manastır 60 seçenekleri var. Manastır 4 hücre içindeki kahraman ve Ana Üs'e üç saniyede bir azami canlarının %3'ünü iyileştirir. Bölge 1 düşmanları, boss, bölge/seviye verileri ve düşman davranışı bu geçiş için düzenlenmedi. Eski `WallController` sınıfı düşman kodunun bağımlılığı olduğu için bırakıldı; Duvarcı verisine veya kartına bağlı değildir.

Dosyalar: `Assets/Scripts/Data/GameData.cs`, `PlayerSaveData.cs`; `Assets/Scripts/Base/BuildMenuController.cs`, `BaseMapManager.cs`, `BaseWorldLayout.cs`, `MonasteryHealer.cs`; `Assets/Scripts/Battle/BattleBuildSlot.cs`, `BattleManager.cs`, `HeroController.cs`, `MainBaseController.cs`; `Assets/Scripts/Managers/SpriteManager.cs`, `GameManager.cs`; `Assets/Scripts/UI/BaseWorldUI.cs`; `Assets/Editor/BuildBaseVisualCatalog.cs`; `Assets/Resources/BaseVisualCatalog.asset` ve `Assets/Resources/BaseVisualAnimations/` içindeki Manastır varlıkları.

## 3. Çırak

390×844 görüntüler: [Ana Üs yükseltme paneli](expansion-apprentice-panel.png), [savaş can çubuğu](expansion-battle-friendly.png).

Çırak madende sabit çalışır. Maden gelirine seviyeye göre %10, %15, %20, %25 veya %30 katkı verir. Yükseltme bedelleri 80, 160, 320, 640 altın; renk dizisi mavi, sarı, kırmızı, mor, siyahtır. Kahraman/Çırak sekmeli paneli vardır. Savaşta kahramanı yaklaşık iki hücre mesafeden izler, yakındaki düşmana kahraman hasarının %25'iyle saldırır, kahramanın %50 canıyla başlar. Çok yakındaki düşmandan temas hasarı alabilir; öldüğünde savaşın kalanı için pasif kalır, yeni savaşta yeniden oluşturulur. Küçük can çubuğu savaş HUD'ında görünür.

Dosyalar: `Assets/Scripts/Base/BaseApprenticeVisual.cs`, `BattleApprentice.cs`, `BattleCosmeticObserver.cs`, `BaseSceneSetup.cs`; `Assets/Scripts/UI/BaseWorldUI.cs`; `Assets/Scripts/Managers/GameManager.cs`; `Assets/Scripts/Data/PlayerSaveData.cs`; `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`.

## Doğrulama

- Unity PlayMode `ExpansionFeatureTests`: 2 geçti, 0 başarısız (`expansion-all-results.xml`). Katalog maliyeti/kilidi, grid çakışması, kayıt JSON dönüşümü, sekmeler, Çırak maliyet ve gelir basamakları, Manastır iyileştirmesi ve dost can durumu denetlendi.
- `dotnet build Assembly-CSharp-Editor.csproj`: 0 hata, 36 uyarı (eski Unity API uyarıları dahil).
- Görüntüler sahnenin kamera ve Canvas çıktısından 390×844 RenderTexture ile alındı; fiziksel cihaz dokunma ve kare hızı ölçümü yapılmadı.
- Çalışma ağacındaki önceki değişiklikler korunmuştur.

Test dosyası: `Assets/Tests/PlayMode/ExpansionFeatureTests.cs`.
