# Rapor — Round 2 (11 Kasım 2026)

Dal: **`gece-gelistirme`** — push edilmedi. Bu turun commit'leri `22db715` → `8e72057` (aşağıda).
Test paketi: **87 testten 86'sı geçiyor** (kalan: bilinen Topçu köşe gölgesi). Yeni testler: GridPlacement, CounterEnemy,
RegionMechanics, TimingConfig, GameClock, BattleBalance — hepsi geçti. Ölçümler bot ile, **normal ekonomi** (elmas 0).

> ⚠️ Push öncesi (önceki turdan): `d6e648e`+`483cd1d` ve `7853033`+`00afea4` çiftleri birleştirilmeli
> (`git rebase -i a8cc598`, ikinci commit'leri `fixup`). Bu turda korunan dosyalara dokunulmadı.

## A. Günlük görevler — korundu, genişletildi (`cffb20d`)
Yeni türler: **savaşta 3 yapı kur**, **üssüne dekor yerleştir**, **kahramanınla 15 düşman yen** (son vuruş kahraman/Çırak).
Enum sonuna eklendi: eski kayıtlar bozulmaz.

## B. İnşa ve yerleştirme (`ed0ed14`) — en yüksek öncelik
| İstek | Durum |
|---|---|
| Doluluk ızgarası, çakışma imkânsız | ✅ `BaseOccupancyGrid` (bina, kulübe, maden, dekor, kamp tek kontrol) |
| Eski kayıtta üst üste binen yapı | ✅ Yüklemede en yakın boş yere taşınır; sığmayan dekor iade |
| Yarı saydam hayalet, ızgaraya oturur, yeşil/kırmızı | ✅ Bina + dekor |
| Yerleştirirken kamera kaydırma/zoom, kenarda kayma | ✅ `BaseCameraController` (tek parmak kaydır, iki parmak/teker zoom, hayalet kenara gelince kayar) |
| Onay / iptal, taşıma aynı sistem | ✅ YERLEŞTİR/İPTAL; geçersizse düğmede sebep (yer dolu / altın yok / inşaatçı yok) |
| Bina + dekor tek sistem | ✅ Ortak doluluk + ortak sürükleme (`GhostPointerDrag`) |
| PlayMode kabul testi | ✅ `GridPlacementTests`: çakışma reddi, serbest yerleşim, taşıma, kayıt/yükleme, bozuk kayıt onarımı |

Kök neden de bulundu: binalar sabit yuvalara konuyordu; taşınan binanın yuvası "boş" göründüğü için üstüne yeni bina kurulabiliyordu.
**Cihazda bakılmadı:** dokunmatik sürükleme/pinch, kamera sınırları. HUD yeniden hizalanınca zoom sıfırlanıyor (zararsız, görünür).

## C. Savaş dengesi (`c1925de`, `2a47371`, `8e72057`)
- **Kök neden:** kule gücü seviyenin karesiyle (hasar × birim sayısı), kahraman doğrusal büyüyordu (12. seviyede 1 okçu ≈ 7 kahraman).
- **Kule eğrisi** kahramanla aynı eğimde; savaş içi kule bedeli bölgeyle artar (Bölge 6: 100/150/120).
- **Karşı düşmanlar:** Zırhlı (okçudan ×0,35), Uçan (topçu vuramaz), Sabotajcı (kuleye koşar, 1 sn fitil, kulenin %30'u patlar,
  kahraman tek vuruşta öldürür). İlk dalgada ve Bölge 1'de yok; ilk görünüşte "YENİ DÜŞMAN" afişi.
- **Boss kalkanı:** yalnız kahraman kırar; kalkan varken kule hasarı ×0,25–0,5; kırılınca 6 sn ×1,5. Yenilgide ipucu.

**Baskın strateji testi** (her bölgenin boss aşaması, 3 tohum):
| Strateji | Önce (kule eğrisi değişmeden) | Sonra |
|---|---|---|
| İyi oyuncu (kule + kahraman) | 36/36, kahraman payı düşük | aşama 1: 18/18, **boss 15/18 (%83)** |
| Sıradan oyuncu | boss 11/18 | aşama 1: 18/18, **boss 5/18 (%28)** |
| Yalnız kule (kahraman kıpırdamaz) | ölçülmedi; kullanıcı: "1–2 kuleyle geçiliyor" | **0/18** |
| Yalnız kahraman (yapı yok) | — | **0/18** |
| Kahraman hasar payı (iyi oyuncu) | — | %35–92 (hedef R3+ %30–50) |

Bölge hedefleri ve metrik tanımları: `Docs/design/round2.md` (C-metrics). Ortalama oyuncu iki profil arasında (~%55, hedef %60–75).

## D. Başlangıç parası (`c1925de`)
Varsayılan **0**. **Ana Üs seviyesiyle** açılır: 7 → 25, 10 → 50, 13 → 75 altın. Satın alınamaz; kart/Kraliyet/elmas etkilemez.
Gerekçe (game-designer): yükseltmeyi ödüllendirir, üç küçük adım (5. bölge sonrası tek sıçrama yalnız Bölge 6'ya yarardı),
hiçbir zaman tam bir kule ödemez. İlk dalga sınırı 6+bölge (altınsız ilk dalga kahramanla tutulabilsin).

## E. Bölge kimlikleri (`aee4db4` görsel, `35f1e17` mekanik, `7d90875` sanat)
| Bölge | Mekanik | Uyan düşman |
|---|---|---|
| 4 Buz | Kar fırtınası 1–2 kuleyi 5 sn dondurur; kahraman 1 sn yanında durursa çözer | Uçan + Zırhlı |
| 5 Karanlık | Lanetli Tapınaklar: 18 sn'de bir yakın kulelerin menzili ×0,6; yalnız kahraman yıkar (+30 altın) | Sabotajcı |
| 6 Ejder | Volkan: 24 sn'de bir lav bombaları, yanan zemin yer düşmanlarını ve kahramanı yakar (düşmanı ateşe çekmek taktik) | Uçan (lavdan etkilenmez) |
Görüntü: `Docs/playtest/round2_r5_shrine.png`, `round2_r6_eruption.png`. `ART_NEEDS.md`: bölge başına karo listesi (64×64, 64 PPU),
dekorlar, mekanik görselleri ve itch.io/Asset Store arama kelimeleri.

## F. Geliştirici ayarları (`22db715`)
`Systems/DevSettings.cs`: tek ana anahtar + **sınırsız elmas**, 5-dokunuş kısayolu, hata ayıklama katmanı. Sabit `Available`
`#if UNITY_EDITOR || DEVELOPMENT_BUILD` ile; yayında derleme anında `false`. DEV menüsünde AÇ/KAPA. Bot her zaman 0 elmasla.

**Ekonomi ilerleme hızı** (`Tools/economy_sim.py`, sürekli oynayan sıradan oyuncu, 20 tohum):
| | 1 saat | 3 saat |
|---|---|---|
| Önce (eski süreler, 30 dk can) | Ana Üs 4, Bölge 3'e takılı (Ana Üs 6 gerekir, 6. seviye 3 sa) | Ana Üs 5 |
| Sonra (yeni süreler, 20 dk can) | **Ana Üs 6**, Bölge 3 sonu | **Ana Üs 8–9**, Bölge 4–5 |

## H. Zamanlayıcılar (`6dc9569`)
- Süreler `Resources/Config/TimingConfig.asset`'ten (Inspector'dan değiştirilebilir). Tablo: `BALANCE.md`.
  2→15 toplam **268,5 sa → 37,1 sa**; ilk 7 seviye ≤ 15 dk. Elmasla bitir formülü aynı, bedeller kendiliğinden düştü (858 → 293).
- Devam eden yükseltmeler: kayıt yüklenince kalan süre yeni/eski oranıyla kısalır (`SaveSystem.MigrateTimings`, test edildi).
- Can: **30 → 20 dk** (config); sayaç `GameClock` ile (saat geri alınınca geri gitmez); ipucu metni config'ten.

## G. Yeni oyuncu ilk 30 dk (`7bc6c15`)
qa-playtester incelemesinden düzeltilenler: madene ilk dokunuş boştu (40 altınla başlıyor); Ana Üs 1–2'de yenilgide "üsse dön,
yükselt" önerisi ve ÜSSE DÖN öne çıkar; günlük ödül penceresi ilk gün kendiliğinden açılmaz; "Yeni Oyun" kayıt varken iki dokunuşla;
yeni oyunda ipuçları sıfırlanır; can ipucundaki "30 dk".
**Açık kalan:** ilk savaştan sonra rehber yok (Ana Üs'ü yükseltmeye yönlendiren 2. adım), üs HUD'unda etiketsiz "1/1" fişi,
günlük ödül penceresinin üstünde bina sprite'ları görünmesi (cihazda doğrula).

## Senin karar vermen gerekenler
1. **Kahramansız zafer** şu an hiç yok (yalnız kule 0/18). "Mümkün ama zor" için: Bölge 1–2'de kalkan çarpanı yükseltilebilir
   ya da Ana Üs 7+ başlangıç altını artırılabilir. Hangisi?
2. **Sıradan oyuncu boss'ta %28.** Orta zorluk için biraz düşük olabilir; tek ayar `Progression.BossStageExtraEnemies` /
   `Counters`. Cihazda oynayıp karar ver.
3. Elmasla hızlandırma talebi ~%66 düşecek (süreler kısaldı): inşaatçı 3–5 ve İnşaat İksiri fiyatları gözden geçirilsin mi?
4. Önceki turdan: reklam taslağı sürümde ödül veriyor; kayıt imzası (HMAC).

## Cihazda bakılması gerekenler
Yerleştirme (sürükle/pinch/kenar kayması), dalga ve sabotajcı uyarı okları, boss kalkan baloncuğu, uçan düşman yüksekliği,
donan kule halkası, tapınak/volkan görselleri, yenilgi ekranı ipuçları. (Batchmode görüntüleri arayüzü göstermez.)

## Sonraki oturum için ilk 5 iş
1. Cihaz geri bildirimiyle yerleştirme ve yeni mekaniklerin cilası.
2. İlk savaş sonrası rehber adımı (Ana Üs'ü yükselt → Okçu açılır).
3. Karar 1–2'ye göre denge ince ayarı (bot: `-testFilter PlaytestBotTests`).
4. Topçu köşe gölgesi + DOTween `SetLink` temizliği.
5. Sanat gelince: bölge karoları ve karşı düşman/boss gövdeleri (`ART_NEEDS.md`).
