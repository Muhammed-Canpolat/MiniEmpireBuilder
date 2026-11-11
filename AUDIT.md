# AUDIT — 11 Kasım 2026 (gece denetimi)

Kaynaklar: PlaytestBot telemetrisi (12 aşama × 2 koşu, `PlaytestOutput/run2`), PlayMode testleri (50 test),
güvenlik incelemesi, sanat envanteri, denge notu (`Docs/design/balance_v1.md`).
Efor: S (<1 sa), M (yarım gün), L (1+ gün). Öncelik: P0 kırık/güvenlik → P3 cila.

## Puanlar (1–10)
| Alan | Puan | Kısa gerekçe |
|---|---|---|
| Çekirdek döngü | 7 | Üs → sefer → savaş → ödül döngüsü eksiksiz; 15 seviye, inşaatçı, can, kart, görev, takvim mevcut |
| Zorluk eğrisi | 4 | Aynı profil bir koşuda kazanıp diğerinde 40 sn'de kaybediyor; Bölge 4–5 kolay, 1–3 ve 6 oynak |
| Bölge çeşitliliği | 5 | Bölge 2 (sis) ve 3 (maden) belirgin; 1/4/5/6 aynı zemin, renk tonu %15 → neredeyse fark yok |
| Animasyon envanteri | 4 | Yalnız Ejder Savaşçısı tam kareli; düşmanlar tek kare + kodla hareket; kuleler saldırı animasyonsuz |
| Piksel/ölçek tutarlılığı | 5 | 64 PPU Tiny Swords ile 100 PPU HD düşman çizimleri karışık; zombi kahramandan uzun |
| İnşa sistemi | 7 | Bina seviyeleri, taşınabilir kulübe, dekor; savaşta yuva sistemi basit (tek tür seçimi) |
| Dekorasyon | 6 | 17 dekor; savaş bölgelerinde 5–6 dekorsuz |
| UI/UX | 7 | Tutarlı karanlık fantezi HUD, iki dil; savaşta saldırı yönü uyarısı yok (yenilgi açıklanamıyor) |
| Stabilite | 7 | Bot 24 savaşta 0 hata logu; 8 test eski beklentilerle kalıyordu |
| Güvenlik | 5 → 6 | Saat geri alma açığı kapatıldı (b203b41); kayıt imzasız, reklam taslağı sürümde ödül veriyor |
| Performans | ? | Ölçülmedi; botta `FindObjectsByType` her kare sorunsuz, cihaz profili gerekli |

## Bulgular

### P0 — kırık / güvenlik
| # | Sorun | Oyuncuya etkisi | Çözüm | Efor | Durum |
|---|---|---|---|---|---|
| S1 | Günlük sınırlar yerel saate bağlı: saat ileri/geri alınarak reklam hakkı, günlük sandık, takvim, İstila ödülü tekrar toplanıyor | Elmas enflasyonu, ileride satış ekonomisi çöker | `GameClock` en ileri saat | S | ✅ b203b41 |
| S2 | `AddGems` taşma | Oynanmış kayıtta eksi elmas | Üst sınır 999999 | S | ✅ b203b41 |
| T1 | 8 PlayMode testi kalıyor (eski beklentiler: ağaç sayısı, Çırak canı, gölge eşiği, hayalet maden boyu, belirsiz yansıma, beklenmeyen log) | Gerçek gerilemeler gürültüde kaybolur | Testleri güncel tasarıma uydur | M | 2/8 ✅ |

### P1 — denge ve his
| # | Sorun | Etki | Çözüm | Efor |
|---|---|---|---|---|
| B1 | Her düşman rastgele kenardan doğuyor | Yenilgi şansa bağlı, "haksız" hissi | Dalga başına 1–2 taraf + 2 sn yön uyarısı | M |
| B2 | Savaş altını 0'dan başlıyor | İlk dalgada kule yok, karar yok | Başlangıç altını `60+15(r-1)` (ödülden düşülür) | S |
| B3 | Bölge 6 ilk dalga 18 Ork (kahramanı yok sayar, üsse ~380 dps) | Bölge 6'da ilk 40 sn'de yenilgi | Dalga sırası Gölge Kurt→Zombi→Ork; ilk dalga sayıları düşer | S |
| B4 | İlk dalga büyüklüğü sınırsız | Bölge 5–6'da yığılma | `min(n, 8+r)` | S |
| J1 | Kule atışında geri tepme/namlu efekti yok | Kuleler "ölü" görünüyor | Atışta punch-scale + kıvılcım | S |
| J2 | Ana Üs yıkılışı animasyonsuz; düşük canda duman yok | Yenilgi ani, okunmuyor | Sarsıntı + toz + %30 altı duman | S |
| J3 | Düşman ölümünde altın uçuşu yok | Ödül hissi zayıf | HUD'a uçan altın | M |

### P2 — içerik çeşitliliği
| # | Sorun | Etki | Çözüm | Efor |
|---|---|---|---|---|
| C1 | Bölge 1/4/5/6 aynı zemin, ton %15 | Bölgeler tekrar ediyor | Ton %45 + bölge örtüsü (kar lekeleri, kızıl rün, lav çatlağı) | M |
| C2 | Bölge 4 kar fırtınası partikülsüz; 5–6 ortam efektsiz | Atmosfer zayıf | Kar, kül/kıvılcım partikülleri | S |
| C3 | 4 bölge bossu aynı Trol gövdesi | Bosslar ayırt edilmiyor | Boss başına farklı aura şekli + giriş efekti (sanat gelene kadar) | M |
| C4 | Savaş yuvasında yalnız kule; savunma çeşidi yok | Strateji sığ | Yeni savaş yapısı: Barikat (ucuz, yolu keser) veya Tuzak | L |

### P3 — güvenlik (karar gerekir) ve cila
| # | Sorun | Çözüm | Efor |
|---|---|---|---|
| S3 | Kayıt düz JSON, imzasız | HMAC (bulut kaydıyla birlikte tasarlanmalı) | M |
| S4 | `RewardedAds` taslağı sürüm derlemesinde de ödül veriyor | SDK gelene kadar sürümde kapat (karar: kullanıcı) | S |
| S5 | Kraliyet/satın alma bayrakları kayıtta | Makbuz doğrulama (IAP ile) | L |
| A1 | HD düşman çizimleri ile piksel kahraman stil çatışması | Sanat: ART_NEEDS.md #1 | — |
| A2 | `enemy_direction_arrow` 341 PPU aykırı | PPU 100'e çek (görsel test gerekir) | S |

## Telemetri özeti (beklenen oyuncu, 2 koşu)
Kazanılan/oynanan: Bölge 1: 3/4, 2: 3/4, 3: 3/4, 4: 4/4, 5: 4/4, 6: 2/4. Bütün yenilgiler **ilk dalgada, ~40 sn içinde**,
düşmanlar kahramanın olmadığı taraftan üsse yığılınca. Ortalama ölüm süresi (doğuştan ölüme): kurt 13–19 sn,
zombi 24–32 sn, ork 19–27 sn. Hata logu: 0.
