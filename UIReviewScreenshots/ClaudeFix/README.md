# Claude referans düzeltmeleri — 390×844 inceleme

1. Bölge paneli açılışta gizli; açık/tamamlanmış düğüme basınca açılıyor. Kapama, kilitli düğüm bildirimi ve kaydırma denetlendi. Görseller: `fix-01-region-hidden.png`, `fix-01-region-open.png`, `fix-01-region-closed.png`, `fix-01-region-locked.png`.
2. Ana Üs kilitli arazi bildirimi Türkçe ve pill biçiminde. Görsel: `fix-02-base-toast.png`.
3. Türkçe büyük harf dönüşümü: “Sisli Orman” → “SİSLİ ORMAN”, “Işık” → “IŞIK”. Görsel: `fix-03-map-uppercase.png`.
4. Savaşta dalga etiketi ve çubuk aynı mevcut seviye verisinden oluşuyor. Bölge 1, Seviye 1 verisi 1 dalga; görüntüde 1/1 ve bir parça var. Hasar sayısı tek satır. Görseller: `fix-04-battle-wave.png`, `fix-04-battle-damage.png`.
5. Ana Üs 21×32, iç alan 19×24; kamera merkezi (10.5, 16), görüş genişliği 21 hücre. Ahşap çit, yeni slotlar ve kırpılmış zemin alanı. Görseller: `fix-05-base-debug.png`, `fix-05-base.png`.
6. Sefer haritası bölge kartlarında mevcut bölge zemin dokusu %80 bölge rengiyle tonlandı. Görsel: `fix-06-map-cards.png`.

## Bu düzeltmelerde değişen kaynak dosyalar

- `Assets/Scripts/Campaign/CampaignSceneSetup.cs`
- `Assets/Scripts/Base/BaseSceneSetup.cs`
- `Assets/Scripts/Base/BaseWorldLayout.cs`
- `Assets/Scripts/Base/BaseMapManager.cs`
- `Assets/Scripts/Base/BaseDebugOverlay.cs`
- `Assets/Scripts/UI/BattleUI.cs`
- `Assets/Scripts/Battle/BattleVfxManager.cs`
- `Assets/Scripts/Polish/Battle/DamageNumber.cs`
- `Assets/Scripts/Polish/UI/DarkFantasyHudPolish.cs`
- `Assets/Scripts/Polish/UI/ReferenceUiVisuals.cs`
- `Assets/Scripts/Polish/UI/UiStrings.cs`

Bölge 1 düşmanları, boss, sprite/animasyon/stat ve `RegionData`/`LevelData` içeriği değiştirilmedi. Ana Üs sprite oranı 0,457: yeni en fazla 0,3 hücre taşma sınırı 21 hücrelik ekranda önceki 0,50–0,55 oranıyla aynı anda sağlanamıyor. Zemin görüntüsünde dikiş yaratan koyu kenarlar mevcut dokunun orta alanı kullanılarak giderildi; ayrı bir 64×64 kaynak dosya üretilmedi.
