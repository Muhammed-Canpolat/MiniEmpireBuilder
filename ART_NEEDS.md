# ART_NEEDS — çizilmesi gereken görseller

Ölçek: Tiny Swords ile aynı görünüm, **64 px/birim**, yatay şerit (her kare ayrı hücre). Öncelik sırası yukarıdan aşağı.
Bunlar gelene kadar oyunda kod tabanlı yedek kullanılıyor (EnemyMotion, renk tonu, tween, partikül).

| # | Varlık | Kare boyu | Kareler | Neden |
|---|---|---|---|---|
| 1 | Kurt, Zombi, Ork, Trol | 192×192 | idle 6, run 6–8, attack 6, hit 2, death 6 | Şu an tek kare HD çizim (100 PPU); piksel sanat kahramanla stil çatışıyor |
| 2 | 4 bölge bossu (Kale Muhafızı, Maden Gardiyanı, Buz Hükümdarı, Karanlık Kral) | 320×320 | idle 6, walk 6, attack 8, hurt 2, death 8 | Dördü de renklendirilmiş Trol gövdesi |
| 3 | Orman Cadısı | 192×192 | idle 6, move 6, cast 8, hit 2, death 6 | Ödünç Tiny Swords Monk gövdesi |
| 4 | Okçu kulesi / Topçu kulesi | 192×256 / 128×256 | atış 4 (+ idle 4) / ateş 5 | Kulelerin saldırı animasyonu yok |
| 5 | Manastır (savaş) | 192×256 | sabit + iyileştirme parıltısı 6 | Savaşta sprite yüklenmiyor olabilir (doğrulanacak) |
| 6 | Ana Üs (savaş) | 320×256 | hasar evreleri 3 (%75/50/25), yıkıntı 1, çöküş 6 | Yıkılış animasyonu yok |
| 7 | Çırak | 192×192 | hit 2, death 6 | Hasar/ölüm tepkisi yok |
| 8 | Zemin karoları (Bölge 4 kar/buz, 5 kara taş + kızıl rün, 6 volkanik/lav çatlağı) | 256×256 döşenebilir, 3 varyant | — | Bölge 1/4/5/6 aynı zemini kullanıyor |
| 9 | Bölge dekorları (donmuş ağaç/buz kristali, kemik yığını/kara sancak, lav kayası/ejder kafatası) | 64–128 px | bölge başına 6–8 | Bölge 5–6 dekorsuz |

## Ses (bekleniyor)
Tıklama, altın, vuruş (kılıç/ok/top), düşman ölümü, kule yıkılışı, zafer, yenilgi, dalga borusu, rüzgar/kuş ortam sesi.

## Bölge tileset'leri (round 2, E maddesi)
Mevcut savaş zemini tek parça döşenen bir karo (100 PPU). Yeni setler **Tiny Swords ile aynı ölçekte: 64 px = 1 birim
(64 PPU), 64×64 karo**, kenar geçişleri (9-slice / autotile 47 ya da en az 16'lık "blob") ile gelmeli. Her bölge için:

| Bölge | Zemin (64×64) | Yol / özel karo | Engel ve dekor (64–192 px) | Mekanik görseli (şu an kodla) |
|---|---|---|---|---|
| 4 Buz Hükümdarı | kar (3 varyant), buz (2 varyant, kaygan parlak) | buzlu taş yol, donmuş göl kenarı | donmuş çam (128×192), buz kristali (64×128), karla kaplı kaya, kardan yığın | donmuş kule kaplaması (192×256 üst katman), çözülme halkası |
| 5 Karanlık Kral | kara taş (3), çatlak kül toprak (2) | kızıl rün işli kaldırım, kan izi | kemik yığını, kara sancak (64×128), kırık sütun, ölü ağaç | **Lanetli Tapınak** 128×128 (idle 4 kare + nabız 6 kare), menzil düşürme dalgası |
| 6 Ejderin İni | volkanik kaya (3), kül (2) | **lav kanalı** (düz/köşe/T/son, 6 kare akış animasyonu) | lav kayası, ejder kafatası (192×128), kömürleşmiş ağaç, buhar deliği | lav bombası (64×64, 6 kare düşüş), yanan zemin (128×64, 6 kare döngü), patlama |

**Arama anahtar kelimeleri** (itch.io / Unity Asset Store; "top-down", "16x16 değil 64x64" ya da 32x32 ise 2× ölçeklenebilir):
- Genel stil: `tiny swords compatible`, `top down pixel art tileset 64x64`, `pixel art rpg tileset autotile`, `pixel frog tileset`
- Bölge 4: `snow tileset pixel art top down`, `winter tileset rpg`, `ice cave tileset`, `frozen forest pixel`
- Bölge 5: `dark fantasy tileset pixel`, `graveyard tileset top down`, `cursed ruins pixel art`, `necromancer dungeon tileset`
- Bölge 6: `lava tileset pixel art`, `volcano tileset top down`, `hell tileset rpg`, `fire dungeon tiles animated lava`
- Lisans: ticari kullanıma izin (CC0 / "commercial use allowed") ve yeniden dağıtım yasağına dikkat.

## Martı Adası: tekneler (Docs/design/island_base.md §3)
Hiçbir pakette tekne yok. Tiny Swords ile aynı **3/4 üstten** bakış, 64 px/birim, yatay şerit; yelken/bayrak fraksiyon
renginde (kırmızı/sarı/mor/siyah) ya da gri + kodla renk tonu.

| # | Varlık | Kare boyu | Kareler | Neden |
|---|---|---|---|---|
| 10 | Sandal (2 kişilik) | 128×64 | sallanma 4, kürek 4, batış 6 | Akın 1+ tekne evresi |
| 11 | Akıncı Teknesi (4 kişilik, tek yelken) | 192×96 | sallanma 4, yelken dalgalanma 4, batış 6, karaya oturma 1 | Akın 5+ |
| 12 | Kalkanlı Mavna (6 kişilik, yanlarda kalkan dizisi) | 256×128 | sallanma 4, batış 8, yanma üst katmanı 6 | Akın 15+, Ateşli Ok görseli |
| 13 | Hayalet Gemi | 256×160 | süzülme 6, batış 8 (yeşil sis) | Etkinlik |
| 14 | Dümen suyu / pruva köpüğü | 64×32 | 6 döngü | Tekne hareketi (şimdilik `Water Foam.png` yeniden kullanılır) |

**Ara çözüm (sırayla):**
1. **Kodla (MVP):** gövde = koyu ahşap yamuk + kalas çizgileri + kenar, yelken = fraksiyon renginde üçgen, sallanma tween'i,
   batış = eğilme + alfa + `Particle FX/Water Splash.png`.
2. **`Tools/campaign_art/generate.py` (Pillow):** aynı biçimleri piksel sanat PNG şeridi olarak pişir (64 PPU, Tiny Swords
   paleti), `Resources/Island/boats/` altına.
3. **Hazır paket** (itch.io / Asset Store): `pixel art boat top down`, `rowboat pixel sprite`, `pirate ship pixel art top down`,
   `viking longship pixel art`, `tiny swords compatible boat`, `pixel frog ship` (Pixel Frog "Treasure Hunters" yan
   görünüşlü, açı uymayabilir). Lisans kuralları yukarıdaki gibi.

## Gece çalışması (13 Kasım 2026) ihtiyaçları
- **Ada ikonu** (radyal menü ADA düğmesi): 128×128, palmiyeli/kumsallı küçük ada, Tiny Swords stilinde. Şu an yer tutucu:
  kale sprite'ı + ağaç, deniz rengi zemin (`UI/RadialDockMenu.cs`).
- **Ses:** ada baskın borusu (`AudioLibrary.raidHorn` boş), ayrı sakin ada müziği (şu an Ana Üs çalma listesi).

## 3. oturum (13 Kasım 2026) ihtiyaçları
- **Geri al simgesi** (GERİ AL düğmesi, `UI/UndoMoveButton.cs`): 64×64 kıvrık geri ok, altın/krem, Tiny Swords UI stili.
  Şimdi: `ui_icon_play` 180° döndürülmüş.
- **Ağ (net) sprite'ı** (Ağ Oku yeteneği, düşmanın üstünde 2,5 sn duran ağ): 96×96 saydam, kahverengi ip ızgarası, kenarlarda
  düğümler, 3–4 karelik "kapanma" animasyonu + fırlatma sırasında açık ağ (64×64). Şimdi: kodla çizilmiş ip çizgileri (geçici).
- **Ok yağmuru hedef halkası**: 128×128 kırmızı/altın kesikli daire (yerde). Şimdi: Tiny Swords `cursor_brackets` + dolgu.
- **Yetenek ikonları** (akın düğmeleri, 64×64): Ok Yağmuru (yukarıdan düşen oklar), Ateşli Ok (alevli ok), Ağ (ağ), Harita
  (dürülmüş harita), Oto (dönen oklar/A). Şimdi: mevcut kılıç/kalkan/saat ikonları + metin.
- **Ses efektleri:** ok isabeti, ateş tutuşma, ağ fırlatma, ok yağmuru ıslığı (şu an `AudioLibrary`'de karşılığı olanlar çalınır,
  yoksa sessiz).
- **Ana Üs animasyonu:** madenci kazma karesi (Tiny Swords Pawn "interact pickaxe" var, adada kullanılıyor; Ana Üs madenci
  sprite'ı farklı ise eşlenmeli), koyun otlama kareleri Ana Üs koyun dekorunda.

## 4. oturum (14 Kasım 2026) ihtiyaçları
- **Dekor (Tiny Swords stilinde, 64 px/birim, 1×1 hücreye oturan):** ahşap çit (düz/köşe/kapı, 64×64), fener direği (64×128, yanık/sönük
  2 kare), sancak/bayrak (64×128, 4 kare dalgalanma; mavi/sarı/kırmızı/mor/siyah), taş patika karosu (64×64, 3 varyant), çiçek tarhı (64×64),
  kuyu (128×128), heykel (64×128). Ücretsiz Tiny Swords paketinde yok; şu an katalogdan kaldırıldılar (D50).
- **Lancer renk kademeleri:** Kışla çatısı ve savaş mızraklıları yalnız mavi (catalog'da sarı/kırmızı/mor/siyah Lancer kareleri yok).
