# 390×844 oyun görüntüsü incelemesi

Görüntüler, ana projenin kayıt dosyasını değiştirmemek için ayrı bir Unity inceleme kopyasının Game view'ından alındı. Ana projedeki sahne ve kod değişiklikleri Assets altında bulunur.

## A — Ana Üs

| Kontrol | Görüntü |
| --- | --- |
| Son durum, Ana Üs seviye 1 | [normal](BaseRebuild/final-level1.png) · [F1 ızgara](BaseRebuild/final-level1-debug.png) |
| Son durum, Ana Üs seviye 3 | [normal](BaseRebuild/final-level3.png) · [F1 ızgara](BaseRebuild/final-level3-debug.png) |
| Sıralı ara kontroller | [başlangıç](BaseRebuild/00-base-before.png) · [dünya](BaseRebuild/01-world.png) · [yerleşim](BaseRebuild/03-slots.png) · [ölçek](BaseRebuild/04-scale.png) · [kahraman](BaseRebuild/05-hero.png) · [çit](BaseRebuild/06-fence.png) |

Son F1 ölçümü: Ana Üs sprite genişliği / ekran genişliği **0,545** (hedef 0,50–0,55); footprint / ekran **0,474** (hedef 0,46). Kamera 19×31,4 hücre görüyor; dünya 19×32, iç alan 17×24. Dört çit kenarı görünüyor. Eski konum içeren kaydın slotlara taşınması ayrı inceleme kopyasında doğrulandı.

## B — Sefer

| Faz | Görüntü |
| --- | --- |
| B1 Sefer haritası | [harita](Campaign/B1-sefer-haritasi.png) |
| B2 Bölge ayrıntısı | [Yıkık Kale Yolu](Campaign/B2-bolge.png) |
| B3 Savaş akışı | [savaş](Campaign/B3-battle.png) · [zafer](Campaign/B3-victory.png) · [yenilgi](Campaign/B3-defeat.png) · [bölgeye dönüş](Campaign/B3-region-return.png) · [tekrar dene](Campaign/B3-retry.png) |
| B4 Savaş inşası | [yaklaşan düşman](Campaign/B4-incoming.png) · [seçim](Campaign/B4-build-choice.png) · [kurulmuş kule](Campaign/B4-built-tower.png) |
| Düşman sanatı | [bölge 1 boss](Campaign/B5-boss.png) · [kurt boyut kontrolü](Campaign/B5-wolf.png) |

Sefer haritası → bölge → savaş → zafer/yenilgi → bölge akışı; seviye açılma, yıldız kaydı, altın düşümü ve yeniden deneme Unity oynatma kontrolünde çalıştı. Boss ve kurt görselleri ayrıca 390×844'te kontrol edildi. Üs kayıt geçişi de ayrı kontrolden geçti.

## Referanstan kalan farklar

- Haritada azami yıldız **75** gösterilir: 5 bölge × 5 seviye × 3 yıldız. Referanstaki `/45` bu veri yapısıyla uyuşmuyor.
- Üçüncü yıldız için `hiç kule kurmama` kuralı kullanılmaz. Kullanıcının tercihi doğrultusunda kahramanın kalan canı %50'nin üzerinde olmalıdır.
- Bölge 2–5 için düşman ve zemin geçicidir. Bu bölgelerin ayrı oynanış ve sanat üretimi sonraki aşamadır.
- Boss ve diğer yeni düşmanlar tek kare görsellerdir; özel hareket ve saldırı animasyonları henüz yoktur.
- Referanstaki bazı bölge kartı çizimleri ve savaş dekorları projede bulunmadığından mevcut zemin dokuları ve yeni düşman görselleri kullanıldı.

## Fazlara göre kaynaklar

- **A:** `Assets/Scripts/Base/BaseWorldLayout.cs`, `BaseDebugOverlay.cs`, `BaseMapManager.cs`, `BaseSceneSetup.cs`, `HeroMovementController.cs`, `BuilderVisualController.cs`, `PlacementController.cs`; kayıt geçişi için `Assets/Scripts/Data/PlayerSaveData.cs` ve `Assets/Scripts/Managers/GameManager.cs`. Çit ve kahraman sprite import ayarları da güncellendi.
- **B1–B2:** `Assets/Scripts/Campaign/RegionData.cs`, `BattleSession.cs`, `CampaignSceneSetup.cs`; `Assets/Editor/CampaignContentGenerator.cs`; beş `Assets/Resources/Campaign/region_*.asset`; `Assets/Scenes/CampaignScene.unity` ve `ProjectSettings/EditorBuildSettings.asset`.
- **B3:** `Assets/Scripts/Battle/BattleManager.cs`, `EnemyController.cs`, `HeroController.cs`, `Assets/Scripts/UI/BattleUI.cs`, `Assets/Scripts/Polish/UI/VictoryReferenceView.cs`; sahne geçişleri için `GameManager.cs`, `SceneAutoSetup.cs`, `BaseWorldUI.cs`.
- **B4 ve görsel son kontrol:** `Assets/Scripts/Battle/BattleBuildSlot.cs`, `BattleSceneSetup.cs`, `Assets/Scripts/UI/VirtualJoystick.cs`, `Assets/Scripts/Polish/Battle/BattleWorldMarkers.cs`, `EnemyVisuals.cs`, `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`, `Assets/Scripts/Data/GameData.cs`; dört `Assets/Resources/Sprites/enemy_*.png` ve import `.meta` dosyası.

Yeni prefab oluşturulmadı; sefer ekranları ve savaş menüleri çalışma anında uGUI nesneleri olarak kuruluyor. B aşamasında sefer akışı, kayıt ve savaş dalgaları için oyun kodu değiştirildi.

## Yeni düşman görseli üretim istemleri

Built-in image_gen ile şeffaf arka planlı, tek karakterli, yazısız, karanlık fantezi piksel sanatı olarak dört ayrı istem kullanıldı:

1. Kül grisi, dikenli sırtlı, deri koşumlu saldırgan kurt; üç çeyrek oyun sprite görünüşü.
2. Çürümüş zırh ve silah taşıyan ölü piyade; üç çeyrek oyun sprite görünüşü.
3. Yeşil tenli, ağır baltalı ork akıncısı; deri ve metal zırh, üç çeyrek oyun sprite görünüşü.
4. Siyah demir zırhlı, kırmızı pelerinli kale muhafızı boss; ağır topuz ve kalkan, üç çeyrek oyun sprite görünüşü.

Son varlıklar `Assets/Resources/Sprites/enemy_wolf.png`, `enemy_zombie.png`, `enemy_orc.png` ve `enemy_troll.png` konumuna kaydedildi.
