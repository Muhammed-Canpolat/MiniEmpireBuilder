# Sabah raporu — 11 Kasım 2026 gece çalışması

Dal: **`gece-gelistirme`** (`ui/dark-fantasy-hud`'dan açıldı). **Push edilmedi.** Geri dönüş noktası: `a8cc598`.

## ⚠️ Push etmeden önce
- `d6e648e` commit'i yanlışlıkla **`Assets/Dragon Warrior Files/`** (3,1 MB, lisansı netleşmemiş) ve
  `ground_mine_rocks.png.meta` değişikliğini içeriyor. `483cd1d` bunları takipten çıkardı (diskte duruyorlar), ama
  geçmişte kaldılar. Push'tan önce iki commit'i birleştirmek önerilir:
  `git rebase -i a8cc598` → `483cd1d` satırını `fixup` yap (ya da GitHub Desktop/IDE ile "squash").

## Ne yapıldı (commit'ler)
| Commit | İş |
|---|---|
| `a8cc598` | Unity'nin ürettiği .meta dosyaları + bildirim paketinin kilit kaydı |
| `38531c3` | Firebase sonrası gradle şablonlarında Android Resolver blokları temizlendi (hedef yalnız ARM64) |
| `0084e5a`, `91f40a9` | **PlaytestBot**: savaşları kendisi oynayan otomatik oyuncu, JSON telemetri, kamera görüntüsü; CLAUDE.md, PROGRESS.md, 6 ajan |
| `b203b41` | **Güvenlik:** `GameClock`: telefon saati geri alınınca günlük ödüller yeniden alınamaz; elmas taşma sınırı |
| `0571445` | AUDIT.md (puanlı denetim), ROADMAP.md, denge tasarım notu |
| `dd5b349` | **Denge v1:** dalga başına 1–2 taraf + **3 sn kırmızı ok uyarısı**, başlangıç altını, ilk dalga sınırı, Bölge 6 dalga sırası, Ana Üs canı +100 |
| `ce9c343` | 8 eski testten 7'si güncel tasarıma uyduruldu |
| `aee4db4` | **His:** kule atışında geri tepme + topçu kıvılcımı; Ana Üs darbe sarsıntısı/toz, %30 altında duman, yıkılışta toz + kamera sarsıntısı. **Bölge 4–6 kimliği:** buz/kızıl/volkanik zemin örtüsü, kar/kül/kıvılcım partikülleri |
| `d6e648e` | Boss aşamalarına ek düşman, kule hasar artışı düşürüldü, bot "sıradan oyuncu" profili |
| `483cd1d` | Yanlış eklenen dosyalar takipten çıkarıldı |

## Öncesi / sonrası
| Ölçüm | Önce | Sonra |
|---|---|---|
| PlayMode testleri | 50 testte 42 geçti | 65 testte 64 geçti (bot hariç; kalan: Topçu köşe gölgesi, gerçek görsel sorun) |
| Yenilgi zamanı | Bütün yenilgiler **ilk dalgada ~40 sn**, şansa bağlı (rastgele kenar) | İlk dalgada şansa bağlı yenilgi yok; yenilgiler boss dalgasında |
| Aynı profilin iki koşusu | Bölge 1, 2, 3, 6'da kazan/kaybet sonucu tersine dönüyordu | Tohumlu ölçüm tutarlı |
| İyi oyuncu (bot) | — | 36/36 zafer |
| Sıradan oyuncu (bot) | — | Aşama 1: 18/18 (üs ~%80 canla); **boss: 11/18 = %61** (hedef %55–75 ✔) |
| Saat hilesi | Saat ileri/geri alınarak günlük ödüller sınırsız | Geri almak işe yaramaz |
| Bot hata logu | 0 | 0 |

Görüntüler: `Docs/playtest/*.png` (Bölge 1, 3, 4, 5, 6 boss savaşları). Not: batchmode kamera görüntüsü **arayüzü
(HUD, uyarı okları) içermez**.

## Oyunda görülmedi, cihazda kontrol et
- **Dalga uyarı okları** (`BattleVfxManager.ShowSpawnWarning`): ekran kenarında yanıp sönen kırmızı oklar; konum/boyut.
- Kule geri tepmesi ve Ana Üs sarsıntısı tabletin üzerinde fazla mı, az mı?
- Bölge 4 kar partikülleri + kar fırtınası birlikte fazla kalabalık olabilir.
- Başlangıç altınının (60+15×(bölge−1)) savaş HUD'unda "kazanılan altın" gibi görünmesi kafa karıştırırsa etiket değişebilir.

## Bilinen sorunlar
- `BaseShadowRevisionTests`: Topçu kulesi köşe gölgesi eşikten biraz koyu (0,176 < 0,18); eşik değiştirilmedi.
- Testler arasında sahne değişince binlerce DOTween "yok edilmiş hedef" uyarısı (hata değil; tween'lere `SetLink` eklenmeli).
- Bir koşuda 2. dalgada bir zombi uzun süre hiçbir şeye saldırmadan bekledi; tekrarlanamadı. Bot artık 60 sn
  ilerleme olmazsa düşman dökümü alıyor (`stallDump`).
- Tam test koşusu `UIReviewScreenshots/CurrentReview` görüntülerini yeniden yazıyor (gece her seferinde geri alındı).

## Senin karar vermen gerekenler
1. **Ödüllü reklam taslağı** (`RewardedAds`, 3 sn sahte ekran) sürüm derlemesinde de ödül veriyor. SDK gelene kadar
   sürümde kapatalım mı? (Kapalı test oyuncuları reklam ödüllerini alamaz.)
2. **Kayıt imzası (HMAC):** düz JSON kayıt düzenlenebiliyor. Bulut kaydıyla birlikte mi tasarlansın, şimdi mi?
3. **Zorluk hedefi:** sıradan oyuncu boss'ları %61 geçiyor. Daha zor mu, daha kolay mı istersin?
   (Tek ayar: `Progression.BossStageExtraEnemies`.)
4. Push öncesi `d6e648e` + `483cd1d` birleştirme (yukarıda).

## Senden beklenen sanat / ses
`ART_NEEDS.md`: düşman kare animasyonları (192×192, 64 PPU), 4 bölge bossu gövdesi, kule atış kareleri, Ana Üs hasar
evreleri, Bölge 4–6 zemin karoları ve dekorları. Ses efektleri (tıklama, vuruş, altın, zafer/yenilgi, dalga borusu).

## Sonraki oturum için ilk 5 iş
1. Cihazda dalga uyarı oklarını ve yeni efektleri kontrol et (ekran görüntüsüyle geri bildirim).
2. Düşman ölümünde HUD'a uçan altın (J3) + kritik vuruş renkleri.
3. Bölge bossları için farklı aura/giriş efekti (C3; sanat gelene kadar).
4. Yeni savaş yapısı tasarımı (Barikat/Tuzak; game-designer notu ile).
5. DOTween `SetLink` temizliği ve Topçu köşe gölgesi.
