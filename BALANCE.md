# BALANCE — bekleme süreleri ve denge tabloları

Kaynak: `Assets/Resources/Config/TimingConfig.asset` (Inspector'dan değiştirilebilir; kod `TimingConfig`).
Bütün binalar (Ana Üs, Okçu, Topçu, Manastır, Maden) aynı süre tablosunu kullanır. Elmasla hemen bitir:
`GemEconomy.FinishCost` = 2,2 × √dakika (1 dk altı bedava) — formül değişmedi, bedeller yeni sürelerle kendiliğinden düştü.

## Yükseltme süreleri (hedef seviye)

| Hedef sev. | Eski süre | Yeni süre | Eski elmas (hemen bitir) | Yeni elmas |
|---|---|---|---|---|
| 2 | 15 sn | 10 sn | 0 | 0 |
| 3 | 1 dk | 1 dk | 2 | 2 |
| 4 | 30 dk | 3 dk | 12 | 4 |
| 5 | 1 sa | 6 dk | 17 | 5 |
| 6 | 3 sa | 10 dk | 30 | 7 |
| 7 | 6 sa | 15 dk | 42 | 9 |
| 8 | 12 sa | 30 dk | 59 | 12 |
| 9 | 18 sa | 1 sa | 72 | 17 |
| 10 | 24 sa | 2 sa | 83 | 24 |
| 11 | 30 sa | 3 sa | 93 | 30 |
| 12 | 36 sa | 4 sa | 102 | 34 |
| 13 | 42 sa | 6 sa | 110 | 42 |
| 14 | 48 sa | 8 sa | 118 | 48 |
| 15 | 48 sa | 12 sa | 118 | 59 |
| **2→15 toplam** | **268.5 sa** | **37.1 sa** | 858 | 293 |

## Can
- Bir can: **30 dk → 20 dk** (`TimingConfig.livesRegenMinutes`). 5 can dolumu 2,5 sa → 1 sa 40 dk.
- Kapalıyken: son yenilenme zaman damgasından hesaplanır; sayaç `GameClock` ile (saat geri alınınca geri gitmez).

## Kayıt geçişi
- `PlayerSaveData.timingVersion` < 2 ise devam eden yükseltmelerin kalan süresi yeni/eski oranıyla kısalır
  (`SaveSystem.MigrateTimings`). Ör. eski 6 saatlik işin 3 saati kaldıysa, yeni süre 15 dk → 7,5 dk kalır.
- Can sayacı: geçen süre korunur, yeni 20 dk periyotla hesaplanır (bir sonraki can daha erken gelir).

## Etki (game-designer, round2.md)
- Elmasla hızlandırma talebi yaklaşık %66 düşer; inşaatçı 3–5 ve İnşaat İksiri değeri azalır (izlenecek).
