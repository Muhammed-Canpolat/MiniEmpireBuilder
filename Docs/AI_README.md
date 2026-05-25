# Mini Empire Builder - AI Context README

Bu dosya yeni bir AI sohbetine proje baglamini hizli aktarmak icin hazirlanmistir.

## Kisa Ozet
- Oyun: mobil dikey (portrait) bir base-builder + wave-based battle oyunu.
- Akis: Intro/MainMenu -> Base (us dunyasi) -> Battle (savas) -> Base.
- Sistemlerin cogu sahnede programatik olarak olusturuluyor (prefab yerine kod).
- Kayit sistemi PlayerPrefs + JSON.
- UI revamp sistemi token tabanli (UiStyleToken + UiRevampApplier).

## Oyun Amaci ve Temel Loop
- Oyuncu imparatorlugunu yeniden kurar: altin biriktir, binalari gelistir, kahramani guclendir.
- Base sahnesinde binalara tiklayip upgrade yapar, altin madeninden birikim alir.
- Battle sahnesinde dalgalar gelir; kahraman hareket eder, savunma slotlarina giderek kule/duvar kurar.
- Savas kazaninca altin gelir ve battle level artar.

## Sahne Akisi
- MainMenuScene: intro/hero secimi ve continue.
- BaseScene: us dunyasi (bina/hero upgrade, altin maden).
- BattleScene: dalga bazli savas.

Otomatik kurulum: her sahne yuklenince SceneAutoSetup ilgili setup scriptini ekler.

## Ana Sistemler
### GameManager (Singleton)
- Save/Load, altin ekonomisi, bina/hero upgrade, battle level ilerlemesi.
- Olaylar: OnGoldChanged, OnBuildingUpgraded, OnHeroLevelUp, OnBattleLevelChanged.
- Sahne gecisleri (fade ile).

### Save System
- PlayerSaveData JSON olarak PlayerPrefs icinde tutulur.

### Base World
- BaseWorldController (PlayerController.cs icinde) altin madeninin online/offline uretimini yurutur.
- BaseSceneSetup sahneyi ve UI'yi kodla kurar.

### Battle
- BattleManager dalga uretimi, spawn, zafer/yenilgi ve battle-icinde altin havuzu.
- BattleBuildSlot: hero slot alana girdiginde yeterli altin varsa insa eder.
- HeroController: joystick input ile hareket, otomatik saldiri.
- EnemyController: hedef onceligi (hero/duvar/us) ve saldiri.
- TowerController / WallController / MainBaseController: savunma bilesenleri.

### UI Revamp
- UiStyleToken + UiRevampApplier ile isimlere gore otomatik tema uygulamasi.
- RevampTheme ScriptableObject ile palette/typography/motion tanimi.

## Kontroller
- Battle sahnesi: VirtualJoystick ile hero hareketi.
- Base sahnesi: bina/hero tiklamalari ve panel butonlari.

## Ekonomi ve Seviye
- Gold kapasitesi: MainBase level ile artar.
- GoldMine: saniyede uretim + offline birikim; ceket altin limitine takilinca maden depoda kalir.
- Hero max level: MainBase level + 1.
- Building unlock: MainBase level kosullarina bagli.

## Onemli Dosyalar
- Game state ve sahne akisi: [Assets/Scripts/Managers/GameManager.cs](Assets/Scripts/Managers/GameManager.cs)
- Bootstrapper ve auto setup: [Assets/Scripts/Managers/GameBootstrapper.cs](Assets/Scripts/Managers/GameBootstrapper.cs), [Assets/Scripts/Managers/SceneAutoSetup.cs](Assets/Scripts/Managers/SceneAutoSetup.cs)
- Veri model ve save: [Assets/Scripts/Data/GameData.cs](Assets/Scripts/Data/GameData.cs), [Assets/Scripts/Data/PlayerSaveData.cs](Assets/Scripts/Data/PlayerSaveData.cs)
- Intro kurulum: [Assets/Scripts/Intro/IntroSceneSetup.cs](Assets/Scripts/Intro/IntroSceneSetup.cs)
- Base kurulum ve UI: [Assets/Scripts/Base/BaseSceneSetup.cs](Assets/Scripts/Base/BaseSceneSetup.cs), [Assets/Scripts/UI/BaseWorldUI.cs](Assets/Scripts/UI/BaseWorldUI.cs)
- Base world controller: [Assets/Scripts/PlayerController.cs](Assets/Scripts/PlayerController.cs)
- Battle kurulum: [Assets/Scripts/Battle/BattleSceneSetup.cs](Assets/Scripts/Battle/BattleSceneSetup.cs)
- Battle core: [Assets/Scripts/Battle/BattleManager.cs](Assets/Scripts/Battle/BattleManager.cs)
- Battle actors: [Assets/Scripts/Battle/HeroController.cs](Assets/Scripts/Battle/HeroController.cs), [Assets/Scripts/Battle/EnemyController.cs](Assets/Scripts/Battle/EnemyController.cs)
- Defans: [Assets/Scripts/Battle/TowerController.cs](Assets/Scripts/Battle/TowerController.cs), [Assets/Scripts/Battle/WallController.cs](Assets/Scripts/Battle/WallController.cs), [Assets/Scripts/Battle/MainBaseController.cs](Assets/Scripts/Battle/MainBaseController.cs)
- Battle UI: [Assets/Scripts/UI/BattleUI.cs](Assets/Scripts/UI/BattleUI.cs)
- Build slot: [Assets/Scripts/Battle/BattleBuildSlot.cs](Assets/Scripts/Battle/BattleBuildSlot.cs)
- Joystick: [Assets/Scripts/UI/VirtualJoystick.cs](Assets/Scripts/UI/VirtualJoystick.cs)
- Sprite sistemi: [Assets/Scripts/Managers/SpriteManager.cs](Assets/Scripts/Managers/SpriteManager.cs)
- UI Revamp: [Assets/Scripts/Polish/Revamp/RevampTheme.cs](Assets/Scripts/Polish/Revamp/RevampTheme.cs), [Assets/Scripts/Polish/Revamp/UiStyleToken.cs](Assets/Scripts/Polish/Revamp/UiStyleToken.cs), [Assets/Scripts/Polish/Revamp/UiRevampApplier.cs](Assets/Scripts/Polish/Revamp/UiRevampApplier.cs), [Assets/Scripts/Polish/Revamp/UiRevampBootstrap.cs](Assets/Scripts/Polish/Revamp/UiRevampBootstrap.cs)
- Revamp checklist: [Docs/REVAMP_CHECKLIST.md](Docs/REVAMP_CHECKLIST.md)

## Kaynaklar ve Asset Yerleri
- Sprites: [Assets/Resources/Sprites](Assets/Resources/Sprites)
- VFX: [Assets/Resources/VFX](Assets/Resources/VFX)
- DOTween ayarlari: [Assets/Resources/DOTweenSettings.asset](Assets/Resources/DOTweenSettings.asset)

## Notlar / Teknik Detaylar
- Sahne objeleri buyuk oranda runtime olusturuluyor (kodla UI ve sprite yerlesimi).
- DOTween: UI/healthbar ve animasyonlarda kullaniliyor.
- Input System: EventSystem + InputSystemUIInputModule aktif.
- SpriteManager Resources uzerinden sprite yukler, bulamazsa placeholder uretir.

## Hedefler (Revamp)


---
Bu README, yeni bir AI sohbetinde hizli baglam saglamak icindir. Yeni gorevleri buradan referans alarak spesifik dosyalara odaklanabilirsiniz.
