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
- **Ayarlar (3.3) simgeleri, 64×64 Tiny Swords UI stilinde:** hoparlör/ses, titreşim (telefon), zil (bildirim), küre (dil), zarf (destek),
  kilit-kalkan (gizlilik), kaydırıcı izi + sapı (Bars klasöründeki çubuk ilerleme çubuğu, kaydırıcı değil). Şu an geçici: nota, kalp, "i",
  saat, dişli, kalkan; kaydırıcı hap + `ui_node_blue`.
- **ADA düğmesi (3.2):** şimdilik Tiny Swords parçalarından birleştirilmiş `ui_icon_island.png` (Tools/campaign_art/island_icon.py); el çizimi
  bir ada ikonu gelirse doğrudan değiştirilebilir.

### Kart Atölyesi
- **Kırık kalkan simgesi (Kalkan Kıran):** şimdilik çalışma zamanında `ui_icon_shield` + kırmızı `ui_icon_cross` üst üste (`BattleCards.ApplyIcon`). Tek parça, 64x64, Tiny Swords stilinde çatlak/kırık kalkan istenir.
- **Gök Avcısı simgesi:** şimdilik Tiny Swords `Human Avatars/Avatars_03` (başlıklı kafa) `ui_icon_archer.png` olarak kopyalandı; pakette okçu avatarı yok. Yay çeken / kuşlu 64x64 bir okçu simgesi istenir.
- **Keskin Oklar:** Tiny Swords `Units/Extra/Arrow/Arrow.png` 45° döndürülüp kırpıldı (`ui_icon_arrow.png`); 3 oklu demet simgesi daha iyi olur.
- **Bilgi (i) simgesi:** `Icons/Icon_11` `ui_icon_info.png` olarak kopyalandı. Kart başına "savaş hakkı" için özel simge (kılıç+kart) yok; şimdilik `ui_icon_sword`.
- Not: kopyalanan 3 PNG için .meta dosyaları `ui_icon_play.png.meta` şablonundan, yeni GUID ile elle yazıldı (Unity yeniden içe aktarır).
- **Onay (✓) simgesi:** Tiny Swords Icon_09 (✗) ile eş stilde el çizimi ✓. Şimdilik `ui_icon_check` (Icon_07 paletiyle kodla üretilen geçici sürüm).

## 5. oturum (14 Kasım 2026)
- `ui_icon_move` (dört yönlü ok) ve `ui_icon_trash` (çöp kovası): Tiny Swords ikon paketinde yok; `Tools/campaign_art/action_icons.py` ile aynı
  piksel stilinde üretildi. El çizimi sürüm gelirse `Assets/Resources/CampaignArt/` altında aynı adla değiştirilebilir.

## 7. oturum (15 Kasım 2026): bölge savaş haritaları — gerçek karo/süs ihtiyacı
Şu an: Bölge 4–6 zemini Tiny Swords karosundan **renk eşlemesiyle** (`Tools/campaign_art/recolor_tilesets.py`, D89), eksik süsler Tiny Swords
parçalarından **türetilmiş** (`Tools/campaign_art/battle_props.py`, D91). Gelecek çizimler aynı dosya adıyla değiştirilir (kod değişmez).
Ölçü: **64 px = 1 birim**, karo seti **Tiny Swords Tilemap_colorN düzeninde 576×384 (9×6 karo, 64×64)**: sütun 5–8 satır 0–3 yükseltilmiş zemin
(16 kenar durumu), satır 5 uçurum (alt kenar), sol blok düz zemin. Süsler alt-orta pivot, şeffaf PNG.

| # | Varlık | Dosya (değiştirilecek) | Ölçü | Not |
|---|---|---|---|---|
| 15 | Kar karo seti (zemin kar + buz mavisi uçurum) | Assets/Art/BattleMaps/Tileset_snow.png | 576×384 | Donmuş Geçit; ayak izi/kar dokusu, uçurumda buz sarkıtı |
| 16 | Karanlık taş karo seti (kayrak + kızıl rün çatlakları) | Assets/Art/BattleMaps/Tileset_dark.png | 576×384 | Karanlık Kale |
| 17 | Volkanik karo seti (bazalt + lav çatlağı) | Assets/Art/BattleMaps/Tileset_ash.png | 576×384 | Ejderin İni; lav çatlakları 4 kare animasyon ayrı katman olabilir |
| 18 | Yıkık kale / yıkık kule / yıkık duvar | Resources/BattleMaps/Props/ruin_castle, ruin_tower, ruin_wall.png | 320×192 / 128×128 / 128×96 | Bölge 1; sarmaşık ve çatlak taş |
| 19 | Maden girişi (kayaya oyulmuş, ahşap destek, ray, vagon) | …/Props/mine_entrance.png | 224×160 | Bölge 3 |
| 20 | Kemik yığını ×3, kafatası, kara sancak, kırık sütun | …/Props/bones_1..3.png (+ yeni) | 64×48 | Bölge 5 |
| 21 | Lav gölü (6 kare kabarcık animasyonu), ejder kafatası | …/Props/lava_pool.png | 160×96 (×6) | Bölge 6; şu an parıltı/kabarcık kodla |
| 22 | Karlı çam, buzlu kaya ×2, kar yığını | …/Props/ice_rock.png (+ yeni) | 192×256 / 128×96 | Bölge 4 (şu an güz ağacı beyaz tonlu) |
| 23 | Sis bulutu dokusu (yumuşak, döşenebilir) | — (kodla üretiliyor) | 256×128 | Bölge 2 |

## 10. oturum (17 Kasım 2026): Ana Üs PixelCastle duvarları (D108)
| # | Varlık | Dosya | Ölçü | Not |
|---|---|---|---|---|
| 24 | **EKSİK ASSET:** mavi konik çatılı yuvarlak köşe kulesi (referans `Docs/UIReference/ana-us-referans.png` dört köşe) | Assets/PixelCastle/PixelCastle/Sprites/tower_round_blue_roof.png | ≈200×250 px, 84 PPU, alt-orta pivot, tek kare | Şimdilik altta `tower_small_closed` boyasız, üstte `corner_outer_left/right`; renk/tint ile uydurma YOK. Gelince `BaseCastleWalls` köşe parçası değişir. |

## 10. oturum (17 Kasım 2026): Ana Üs sağ üst droplist (D109)
| # | Varlık | Dosya | Ölçü | Not |
|---|---|---|---|---|
| 25 | **EKSİK ASSET:** kupa simgesi `icon_trophy` (referans `Docs/UIReference/ana-us-droplist-acik.png` Etkinlik satırı) | Assets/MiniEmpireUI/MiniEmpireUI/Sprites/Icons/icon_trophy.png | ≈80×80 px, MiniEmpireUI stili, şeffaf zemin, tek kare, Bilinear | Gelene kadar Etkinlik satırı İKONSUZ (yalnız etiket); yer tutucu/başka ikon kullanılmaz. Görev satırı mevcut parşömen `ui_scroll`; referanstaki çapraz kılıç yalnız Sefer'de. |

## Grup 9 (18 Kasım 2026): Depo (D121, bug 11)
| # | Varlık | Dosya | Ölçü | Not |
|---|---|---|---|---|
| 26 | Depo simgesi (DEPOYA düğmesi, DEPO sekmesi/satırı) | Assets/Resources/CampaignArt/ui_icon_depot.png | 68×68 (34×29 kare ×2, en-yakın-komşu) | KARŞILANDI (karar 7): yeni çizim yok, Cainos `TX Chest Animation Wooden 0` karesi `Tools/campaign_art/depot_icon.py` ile kopyalandı (Point filtre, sıkıştırmasız). İstenirse ileride HUD ailesinde (koyu kontur) 64×64 sandık simgesi çizilebilir; o zamana kadar bu kullanılır, dosya yoksa kod `ui_icon_hammer`'a düşer. |
