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
