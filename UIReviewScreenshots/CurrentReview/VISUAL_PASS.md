# Ana Üs görsel geçişi — 390×844

Bu görüntüler Unity PlayMode sahnesinin 390×844 kamera ve Canvas çıktılarıdır. Mevcut kayıt Ana Üs seviye 5 durumundadır; kule örnekleri ve ilk oyun oku yalnızca yakalama testinde, kaydı değiştirmeden gösterilir.

## 1. Seviye renkleri

`BuildingData.levels[]` beş Tiny Swords renk/sprite/animasyon setini taşır: mavi, sarı, kırmızı, mor, siyah. Seviye atlama geçişi, ölçek zıplaması, toz, kamera sarsıntısı ve Ana Üs açılım banner'ı eklendi. [390×844 görüntü](visual-item-01.png).

Değişen dosyalar: `Assets/Scripts/Data/GameData.cs`, `Assets/Scripts/Base/BaseVisualCatalog.cs`, `Assets/Scripts/Base/BaseBuildingVisual.cs`, `Assets/Scripts/Base/BaseLevelBanner.cs`, `Assets/Scripts/Base/BaseMapManager.cs`, `Assets/Editor/BuildBaseVisualCatalog.cs`, `Assets/Resources/BaseVisualCatalog.asset`, `Assets/Resources/BaseVisualAnimations/`.

## 2. Kule askerleri

Okçu ve Topçu kuleleriyle Ana Üs için garnizon çapa prefab'ı kullanılır. Savaşta karakter mevcut mermiyi gözleyerek hedefe döner ve saldırı animasyonu oynatır; Topçu için duman eklenir. [390×844 görüntü](visual-item-02.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseGarrisonVisual.cs`, `Assets/Scripts/Base/BattleCosmeticObserver.cs`, `Assets/Scripts/Base/BaseMapManager.cs`, `Assets/Resources/BaseGarrisonAnchor.prefab`.

## 3. Animasyon akışları

Kahramanın idle/run/attack/hit/death durumları, Pawn inşa ve maden animasyonları, bina yapım inişi ve dokunma tepkisi eklendi. Görsel animatörler `CullUpdateTransforms` kullanır. Sprite Atlas, piksel okuyan mevcut kahraman portresini korumak için kahraman sprite'larını dışarıda tutar. [390×844 görüntü](visual-item-03.png).

Değişen dosyalar: `Assets/Scripts/Base/HeroMovementController.cs`, `Assets/Scripts/Base/BuilderVisualController.cs`, `Assets/Scripts/Base/BaseBuildingVisual.cs`, `Assets/Scripts/Base/BaseMapManager.cs`, `Assets/Scripts/Base/PlacementController.cs`, `Assets/Scripts/Managers/SpriteManager.cs`, `Assets/Editor/BuildBaseVisualCatalog.cs`, `Assets/Resources/BaseVisualAnimations/`, `Assets/Resources/BaseVisuals.spriteatlas`. Bina düşüşü, toz ve ölçek tepkisi artık inşaat görevi tamamlanınca oynar; yerleştirme anında oynatılmaz.

## 4. Atmosfer animasyonları

Üç hafif bulut gölgesi, aralıklı dört kuş, animasyonlu çalılar ve kamp bayrağı hareketi eklendi. [390×844 görüntü](visual-item-04.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseAmbientMotion.cs`, `Assets/Scripts/Base/BaseSceneSetup.cs`, `Assets/Scripts/Base/BaseVisualCatalog.cs`.

## 5. Maden

Eski Gemini görseli kullanılmaz; Tiny Swords altın kayaları ve kazma animasyonlu Pawn ile 3×3 küme oluşturulur. Kayaların kaynak dosyalarındaki şeffaf kenarlar hesaba katılarak görünür boyutları büyütüldü ve hafif parıltı eklendi. Depo doluluğu işareti ve altının üst çubuğa uçuşu eklendi; uçuş sayısı cüzdana gerçekten geçen altını kullanır. [390×844 görüntü](visual-item-05.png).

Değişen dosyalar: `Assets/Scripts/Base/BaseMineVisual.cs`, `Assets/Scripts/Base/BaseMapManager.cs`, `Assets/Scripts/Base/BaseSceneSetup.cs`, `Assets/Scripts/UI/BaseWorldUI.cs`, `Assets/Scripts/Managers/SpriteManager.cs`. Kaldırılan görsel: `Assets/Resources/Sprites/building_gold_mine_new.png`.

## 6. İlk oyun yönlendirmesi

Yeni oyuncuda Maden dokunuşu ve Savaşa Git düğmesi sırayla işaretlenir. Tamamlanma kaydedilir; eski kayıtlarda rehber tekrar başlamaz. [390×844 görüntü](visual-item-06.png).

Değişen dosyalar: `Assets/Scripts/Base/FirstBaseGuide.cs`, `Assets/Scripts/Base/BaseSceneSetup.cs`, `Assets/Scripts/UI/BaseWorldUI.cs`, `Assets/Scripts/Data/PlayerSaveData.cs`.

## Doğrulama

Görsel yakalama testi: `Assets/Tests/PlayMode/BaseVisualCaptureTests.cs`. Bu test, batch çalışmada Game view penceresi dosya yazmadığı için aynı kamera ve Canvas'ı 390×844 render hedefinde çizip PNG üretir. `Assets/Tests/PlayMode/BasePresentationTests.cs` önceki panel, ekonomi ve footprint denetimlerini içerir.

Unity Test Runner önceki tam koşu: **4 geçti, 0 başarısız, 0 atlandı** (`visual-final-2-results.xml`). Son inşaat ve maden düzeltmelerinden sonra 390×844 görsel yakalama testi: **1 geçti, 0 başarısız** (`visual-capture-mine-results.xml`). Savaş kozmetiği testi: **1 geçti, 0 başarısız** (`visual-battle-final-results.xml`). `Assembly-CSharp-Editor.csproj` son derlemesi: **0 hata, 34 uyarı**. Beş testi birlikte iki kez `-nographics` ile çalıştırırken Unity 6000.3.9f1 yerel render aşamasında çöktü (`visual-all-final.log`, `visual-all-reordered.log`). Grafik arka ucuyla tekrar koşu da sonuç XML'i üretmeden sonlandı (`visual-all-graphics.log`). Birleşik beş test koşusu doğrulanamadı; ayrı testlerin geçmesi bu sınırı çözülmüş saydırmaz.

Bölge 1 düşmanları, Kale Muhafızı, sprite/statları, `RegionData`/`LevelData` ve savaş kararları bu görsel geçişte değiştirilmedi. Çalışma ağacındaki önceki değişiklikler korunmuştur.
