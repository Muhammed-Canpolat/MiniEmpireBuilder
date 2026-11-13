# Ada: yeni oyuncunun ilk 30 dakikası (AŞAMA 2 denetimi)

Kaynak: PlayMode testi `IslandFreshStartTests.FirstThirtyMinutesOfANewPlayerOnTheIsland`
(`Assets/Tests/PlayMode/IslandFreshStartTests.cs`), ham çıktı `PlaytestOutput/first30_island.txt`.
Koşul: boş başlangıç bayrağı açık, Kıyı Kalesi 5 (adanın açıldığı seviye), öğretici bitmiş sayıldı (`tutorialStep 4`),
Akınlar İyi bot (`IslandBot.Profile.Good`, ×6 zaman) ile oynandı, saat `IslandRepair.Clock` ile ileri alındı
(Ada Ustası her yeni bina için 5 sn; 10/20/30. dakikada yığınlar toplandı).

## Akış ve sayılar

| An | Olay | Odun | Et | Altın (Ana Üs) |
|---|---|---|---|---|
| 0 dk | Yeni ada: yalnız Kıyı Kalesi | 60 | 0 | 50 |
| ~1 dk | Oduncu Kampı, Ev, Ağıl, Okçuluk Alanı, Gözcü Kulesi kuruldu (hepsi **ilk yapım bedava**) | 60 | 0 | 50 |
| | Akın 1 (ölçek ×0,84): **Zafer**, kale %100 · +33 Odun, +7 Et, +25 altın | | | |
| | Akın 2 (ölçek ×0,84): **Zafer**, kale %100 · +41 Odun, +9 Et, +30 altın | | | |
| | Akın 3 (ölçek ×0,84): **Zafer**, kale %100 · +49 Odun, +11 Et, +35 altın | | | |
| Akınlar sonrası | en yüksek Akın 3 | 183 | 27 | 140 |
| 10 dk | toplanan 6 | 187 | 29 | 140 |
| 20 dk | toplanan 7 | 192 | 31 | 140 |
| 30 dk | toplanan 8 | 197 | 34 | 140 |

Binalar (30. dk): Oduncu Kampı Sv1 (işçi 2/2), Ev Sv1, Ağıl Sv1 (işçi 1/1), Okçuluk Alanı Sv1, Gözcü Kulesi Sv1.
Kadro: çalışan 3 / kapasite 4 (1 Ev işçisi boşta: Oduncu ve Ağıl yuvaları dolu).

30. dakikada alınabilecekler: Oduncu Sv2 (10 Odun / 20 Et) ✓, 2. Gözcü Kulesi (60 / 10) ✓, 2. Ev (40 / 0) ✓,
Doğu Kıyısı (Z1) keşfi (150 Odun) ✓.

## Takılma noktaları / gözlemler

- **Takılma yok:** beş binanın hepsi bedava ve ilk dakikada kurulabiliyor; Akın 1–3 kulesiz de kazanılıyor
  (`EmptyIslandWinsAkinOneWithArcherAndCastle`, ölçek ×0,80), tek kuleyle ×0,84.
- **Pasif üretim çok yavaş:** Sv1 Oduncu 30 Odun/sa, Sv1 Ağıl 15 Et/sa → 30 dakikada yalnız ~21 kaynak toplandı.
  İlk yarım saatte ilerlemenin %90'ı Akın ödülünden geliyor. Et en dar boğaz (34 Et; Oduncu Sv2 20 Et ister).
  Öneri (karar lead'de): ilk saat için Sv1 üretimini artırmak ya da Akın 1–3 Et ödülünü yükseltmek.
- **Bedava hakkı sırası önemli değil** ama Okçuluk Alanı'nın kurulması zorunlu değil: Okçu Kaptan Okçuluk olmadan da
  kale/atış yerlerinden savaşıyor (test: boş adada Akın 1 zafer).
- Akın 1–3 iyi botla çok kolay (kale %100). Orta zorluk hedefi için Akın 4–6 ayrıca ölçülmeli (bu koşuda yok).
- Gerçek oyuncuda Akın 1 öğreticisi (ATEŞ / atış yeri) bu testte atlandı; öğretici adımları Okçuluk Alanı'na bağlı değil.
