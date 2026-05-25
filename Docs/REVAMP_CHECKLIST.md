# Mini Empire Builder - Revamp Checklist

## Phase 0 - Stabilite (Tamamlandi)
1. Compile/runtime blocking hatalari temizlendi.
2. Input System uyumlulugu saglandi.
3. DOTween temel animasyonlari calisiyor.

## Phase 1 - Theme Foundation (Bu tur hazirlandi)
1. [x] RevampTheme ScriptableObject altyapisi.
2. [x] UiStyleToken ile eleman seviyesinde stil token mantigi.
3. [x] UiRevampApplier ile tema uygulama + motion.
4. [x] UiRevampBootstrap ile sahne acilisinda otomatik tagleme ve apply.
5. [x] Theme asset yoksa runtime fallback tema.
6. [x] Panel/topbar icin shadow + accent altyapisi.
7. [x] Button text color ve button state standardizasyonu.

## Phase 2 - Intro / HeroSelect (Siradaki uygulama)
1. [ ] Intro katmanlarini temiz kompozisyonla yeniden oranlamak.
2. [ ] Hero kartlari: secili state + inactive state + glow standardizasyonu.
3. [ ] Font hiyerarsisi: Title/Sub/Body netlestirme.
4. [ ] CTA butonlari tek stil setine gecis.

## Phase 3 - Base HUD Revamp
1. [ ] TopBar bilgi mimarisini yeniden duzenleme (gold, gps, battle lv, base lv).
2. [ ] Building/Hero panellerinde yeni spacing+border+shadow sistemi.
3. [ ] Tooltip ve resource popup gorunur feedback guclendirme.
4. [ ] Button states (normal/hover/pressed/disabled) unify.

## Phase 4 - Battle HUD Revamp
1. [ ] Top combat info readability pass (boyut/kontrast/spacing).
2. [ ] Hero/Base HP panel redesign.
3. [ ] Wave banner dramatizasyon pass.
4. [ ] Damage number visual language pass (normal/critical).

## Phase 5 - Audio Layer (Assetler hazir olunca)
1. BGM/SFX clip mapping.
2. Scene-based BGM routing.
3. UI/combat SFX hooks.

## Phase 6 - Final Polish + QA
1. Mobile safe-area ve farklı aspect ratio kontrolü.
2. FPS, GC alloc, draw call hızlı kontrol.
3. Ekran görüntüsü turu ile visual approval.
4. Store-ready checklist kapanis.

## Notes
- Bu belge aktif uygulama sirasidir; her faz bittikce isaretlenir.
- Buyuk visual degisimi Theme + Token + Applier altyapisi ustunden ilerletilecek.
