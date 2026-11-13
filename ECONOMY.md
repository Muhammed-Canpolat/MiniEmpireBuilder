# ECONOMY.md — Altın ekonomisi: envanter, teşhis ve Economy_V2 önerisi

> Durum: **öneri** (kod/asset değişmedi). Simülasyon: `Tools/economy_sim_v2.py` (yalnız stdlib).
> `python Tools/economy_sim_v2.py --write` bu dosyadaki `SIM` bloğunu gerçek çıktıyla değiştirir.
> **Not:** Bu belge yazılırken betik çalıştırılamadı (komut aracı yoktu). §8'deki sayılar formülden **elle** hesaplandı; ilk iş betiği çalıştırıp bloğu yenilemek.

## 0. Kısa özet
- **Sorun:** Orta oyunda altın, bina bedelinden 3–5 kat hızlı geliyor. İlerlemeyi altın değil, inşaat süresi ve inşaatçı sınırlıyor. Bu yüzden cüzdan sürekli tavanda kalıyor ve fazla altın boşa gidiyor.
- **Temel neden:** Maden çevrimiçi üretimi (her savaş dönüşü toplanıyor, depo 4 dk'da doluyor), çırağın %50'ye kadar maden bonusu, sınırsız 2x zafer reklamı ve yüksek bölgede ödülün 5–8 katı tutan öldürme altını.
- **V2'de neler değişiyor:** Ana Üs bedelleri 7. seviyeden sonra üstel artıyor (en çok ×2,12). Maden hızı ÷1,7–2,35, çırak maden bonusu ÷2. Öldürme altınının cüzdana geçen payı %75'e iniyor, bölge ödülü daha yavaş artıyor, 2x reklam günde 5 hakla sınırlanıyor. Bunlara 3 yeni yutak ve Sonsuz Sefer kule formülü ekleniyor.
- **Dokunulmayanlar:** İlk 6 Ana Üs bedeli (affedici giriş ve 2. inşaatçı = 850), süreler, elmas hızlandırma ve inşaatçı fiyatları, savaş içi dengeler.

## 1. Altın KAYNAKLARI (bugün)
| Kaynak | Formül / değer | Dosya:satır |
|---|---|---|
| Maden üretimi | gps = 1 + 0,5(L−1); × (1 + çırak %) × 2x takviye × Kraliyet 1,2 | `Assets/Scripts/Data/GameData.cs:263`, `Assets/Scripts/Managers/GameManager.cs:1043-1055` |
| Maden seviyesi tavanı | Ana Üs + 1 | `GameData.cs:302` |
| Maden deposu | (200 + 150(L−1)) × (1 + Köylü Evi bonusu 0,05 + 0,005(L−1) ev başına) | `GameManager.cs:147-157`, `GameData.cs:749` |
| Çevrimdışı üretim | depoya kadar (2x yalnız süresiyle örtüşen kısım) | `GameManager.cs:214-246` |
| Çırak maden bonusu | %{10,15,20,25,30,32,…,50} | `Assets/Resources/ApprenticeProgressionData.asset:18` |
| Kraliyet | +%20 maden | `Assets/Scripts/Systems/GemEconomy.cs:54` |
| Zafer ödülü | 35r + 18(s−1) (B6: 265–337), asset'te sabit | `Assets/Editor/CampaignContentGenerator.cs:78`, `Assets/Resources/Campaign/region_0X.asset` (`reward:`) |
| Net savaş altını | kalan savaş altını − başlangıç altını; düşman altını × (1 + 0,08(sev−1)) | `Assets/Scripts/Battle/BattleManager.cs:31`, `GameData.cs:1186` |
| Ödeme | BattleGoldNet + ödül; tekrar ×0,3 | `BattleManager.cs:961-963`, `:42` |
| Savaş başı altını | MB7: 25, MB10: 50, MB13: 75 (ödüle sayılmaz) | `GameData.cs:351-352`, `BattleManager.cs:118` |
| 2x zafer reklamı | ödemenin tamamı bir kez daha; **günlük sınır yok** (tekrar oyunda da) | `Assets/Scripts/Polish/UI/VictoryReferenceView.cs:139-147, 312` |
| Giriş takvimi | 1. gün 50+50L, 3. gün 100+100L, 7. gün 150L (+20 elmas), 5. gün 1 sa 2x maden | `Assets/Scripts/Systems/DailyRewards.cs:77, 79, 85` |
| Günlük görevler | 3 görev; altınlı olanlar 80 + 40L | `Assets/Scripts/Systems/DailyQuests.cs:14, 115` |
| Reklam sandığı | günde 1; %60: 100 + 50L | `Assets/Scripts/Systems/AdRewards.cs:71-84` |
| Reklam 2x maden | günde 1, 2 sa | `AdRewards.cs:53-61`, `GemEconomy.cs:47` |
| Ada: Altın Damarı | 6 + 3(k−1)/sa, 8 sa depo | `GameData.cs:709-711`, `Assets/Scripts/Island/IslandEconomy.cs:309-320` |
| Ada: Akın / baskın | 20 + 5n; baskın zaferi 10 + 2k; tekrar 5/gün | `GameData.cs:494, 485, 471`, `Assets/Scripts/Island/IslandSession.cs:93`, `IslandRaidManager.cs:339` |
| İstila | 400 + 40·sev | `Assets/Scripts/Systems/SpecialModes.cs:161` |
| Sonsuz Sefer | kat ödülü 300 + 20·kat; savaş sev. 30 + kat | `SpecialModes.cs:31-32` |
| Elmasla eksik altın | GoldPerGem 40 | `GemEconomy.cs:23`, `GameManager.cs:679` |
| Başlangıç | 50 altın | `Assets/Scripts/Data/PlayerSaveData.cs:13` |

## 2. Altın HARCAMALARI (bugün)
| Yutak | Formül / değer | Dosya:satır |
|---|---|---|
| Bina yükseltme | MainBaseCosts[hedef] × çarpan; MB {0,0,150,250,400,600,850,1150,1500,1900,2400,3000,3700,4500,5400,6500}; MB 1, Okçu 0,5, Topçu 0,7, Manastır 0,6, Maden 0,55, Kışla 0,65, Ev 0,4, Atölye 0,45 | `GameData.cs:896-917`, `GameManager.cs:399` |
| Yeni bina (Kışla/Ev) | UpgradeCost(tür, 2) | `GameData.cs:751`, `Assets/Scripts/Base/PlacementController.cs:277` |
| 2. inşaatçı | UpgradeCost(MB, 6) = 850 (3–5. elmas) | `GameData.cs:885-891`, `GameManager.cs:786` |
| Kahraman | 60 × 1,28^(L−1), anında, tavan = MB | `GameData.cs:947-948`, `GameManager.cs:854` |
| Çırak | 80 × 1,3^(L−1), anında, tavan = MB | `GameData.cs:951-952`, `GameManager.cs:876` |
| Kart Atölyesi | inşa 150; üretim 180/430/1080 × (1 + 0,15(sv−1)) | `GameData.cs:758, 778, 785-788`, `Assets/Scripts/Systems/CardWorkshop.cs:145` |
| Dekor | katalog fiyatı (iade var) | `Assets/Scripts/Base/DecorPlacementController.cs:270` |
| Doğa nesnesi | 5/8/10/15 | `GameData.cs:379`, `Assets/Scripts/Base/NatureObstacle.cs:48` |
| Savaş içi kule (cüzdandan değil, ödemeden düşer) | 50/75/60 × (1 + 0,2(bölge−1)) | `BattleManager.cs:19-21, 801-814, 746` |
| **Tavan kaybı (örtük yutak)** | cüzdan = max(500, ⌈1,5 × sonraki MB / 100⌉ × 100); fazlası silinir (2x reklam dahil) | `GameManager.cs:123-142` |
| Süreler (altın değil, kapı) | 10 sn, 1, 3, 6, 10, 15, 30 dk, 1, 2, 3, 4, 6, 8, 12 sa | `Assets/Resources/Config/TimingConfig.asset:15` |
| Can | 5, 20 dk'da 1 (yalnız yenilgide düşer) | `TimingConfig.asset:16`, `Assets/Scripts/Systems/LivesSystem.cs:10-12`, `GameManager.cs:943` |

## 3. Teşhis (sayılarla)
- **Maden çevrimiçi sızıntısı:** MB10 / maden 11 / çırak 10 durumunda 8,4 gps, yani ~30 bin altın/sa üretiliyor. Depo (1,9 bin) 4 dk'da doluyor ve oyuncu her savaştan sonra topladığı için depo sınır işlevi görmüyor. Günde 6 × 8 dk oturumla ~36 bin altın/gün çıkıyor. Bu seviyede sıradaki Ana Üs 3 bin, yani **6 dk'lık maden** ediyor.
- **Yüksek bölge savaşı:** B6-5 brüt öldürme altını ~3.360 (ödülün 10 katı). Tekrar oyunda bile ×0,3 sonrası ~900, 2x ile ~1.800 ediyor. Reklam sınırsız olduğu için B6 tekrarı 2 savaşta bir Ana Üs (MB15 = 6.500) ödetiyor.
- **Bedel eğrisi düz:** MB14→15 yalnız ×1,2 artıyor; gelir ise seviye başına doğrusal + çırak yüzdesiyle büyüyor.
- **Sonuç:** Ana Üs zinciri ~36 sa'lik süreyle sınırlı. Oyuncu ~3. günde MB15 oluyor; bundan sonra altının alacağı bir şey yok, yalnız süre bekleniyor.
- **Sonsuz Sefer hatası:** Bölge = 99 olduğu için `BattleBuildCostScale` 1 + 0,2·98 = ×20,6 veriyor (Okçu 1030, Topçu 1545, Manastır 1235). Kat 1'de başlangıç + 1. dalga altını 483, kat yalnız 2 dalga, yani ilk kule ancak 2. dalganın sonunda alınıyor. **İstila'da da aynı hata var** (bölge 98, ×20,4). Bkz. `BattleManager.cs:812-813`, `GameData.cs:369`, `SpecialModes.cs:14, 113`.

## 4. Economy_V2: hedef ve kurallar
**Hedefler (ORTA zorluk):**
1. Sıradaki Ana Üs, o aşamanın geliriyle **2–8 savaşta ya da 2–6 saatlik toplam gelirde** alınabilmeli.
2. Oturum süresinin cüzdan tavanında geçen kısmı **< %10** olmalı. "Altın ≥ sıradaki Ana Üs" süresi **%30–60** arasında kalmalı: altın ve süre aşağı yukarı aynı anda yetişsin.
3. Ana Üs hızı LEGACY'ye yakın ya da biraz yavaş olmalı: 1. gün en çok −1, 3. gün en çok −2 seviye, MB15 4.–6. günde.
4. İlk 6 Ana Üs seviyesi ve B1–B2 aynen affedici kalmalı.

**Kurallar:**
- Bedel eğrisi MB7'den sonra seviye başına ~×1,3–1,45 üstel artıyor (§7, en çok ×2,12).
- Maden "geri dön, topla" kaynağı olarak kalıyor. Hız en çok ÷2,35 düşüyor, depo aynı kalıyor. Böylece depo dolum süresi ~4 dk'dan ~10 dk'ya çıkıyor ve çevrimiçi üretim sızıntısı ~%60 azalıyor. Çevrimdışı 6 dolum/gün değişmiyor.
- Savaş: ödül = 40 + 30(r−1) + 12(s−1) (B1 aynı, B6 ~%30 düşük). Net öldürme altınının **%75'i** cüzdana geçiyor (savaş içi altın ve kule alımı **değişmiyor**).
- 2x zafer reklamı: **günde 5 hak**.
- Kahraman ve çırak bedeli L15'te ~×1,55. Çırak maden bonusu ×0,5 (bonus %5→%25; savaş katkısı değişmiyor).

## 5. Sonsuz Sefer (ve İstila) savaş içi kule bedeli
- **Formül:** `bedel = round5(taban × K × (1 + 0,08(savaşSev − 1)) × perWave(kat) / 12)`. Burada K = 1,0, savaşSev = 30 + kat, perWave = min(28, 12 + kat/2) (`SpecialModes.cs:76`).
- Bedel, düşman altınıyla aynı çarpanla ve dalga kalabalığıyla birlikte büyüyor. İlk Okçu her katta başlangıç + 1. dalga altınının **~%30–40'ı** ediyor, yani 1. dalgada alınabiliyor.
- **İstila:** Aynı formül; savaşSev = `DragonInvasion.BattleLevel`, perWave yerine 14.
- **Uygulama notu:** `GetBuildCost` içinde `BattleSession.IsEndless/IsInvasion` dalı eklenmeli; Sefer bölgeleri için `BattleBuildCostScale` olduğu gibi kalmalı.

| Kat | Sev | Başl. + dalga 1 | LEGACY Okçu / ilk kule kaçıncı öldürmede | V2 Okçu/Topçu/Manastır | V2 ilk Okçu |
|---|---|---|---|---|---|
| 1 | 31 | 483 | 1030 / 29. (dalga 2+) | 170 / 255 / 205 | 3. öldürme |
| 6 | 36 | 645 | 1030 / 26. (dalga 2+) | 240 / 355 / 285 | 5. |
| 11 | 41 | 789 | 1030 / 23. (dalga 2+) | 300 / 445 / 355 | 6. |
| 21 | 51 | 1175 | 1030 / 20. | 460 / 690 / 550 | 8. |
| 31 | 61 | 1641 | 1030 / 17. | ~650 / 980 / 785 | 10. |
| 51 | 81 | 2147 | 1030 / 13. | 865 / 1295 / 1035 | 11. |

## 6. Yeni altın yutakları (oyuna uyan 3 öneri)
1. **Sefer Erzakı:** Yeni aşamanın ilk denemesinden önce isteğe bağlı satın alınır; bedeli 30 + 30(r−1). Etkisi küçük ve okunur olmalı: örneğin +1 kart seçeneği ya da kahramana +%10 can. Simde +%4 kazanma olasılığı olarak modellendi. Altına savaş öncesi bir anlam katıyor ve yüksek bölgede ödüle orantılı geri çekiyor.
2. **Ada Tüccarı:** Altınla 50 Et/Odun paketi satar; bedeli 400 + 120(MB−5), günde 2 paket. Ana Üs altınını Martı Adası ekonomisine bağlar ve atölyedeki altın:Et ≈ 16:1 oranıyla (`GameData.cs:775-776`) tutarlıdır.
3. **Prestij Anıtları:** Güç vermeyen, kozmetik dekor; tek seferlik 1.500 / 4.000 / 9.000 / 16.000 (MB 6/9/12/14). Yalnız cüzdan sıradaki Ana Üs'ten fazlaysa alınır; dolayısıyla yalnız fazlayı emer. Mevcut dekor sistemini kullanır (`DecorPlacementController.cs`).
- **Kural:** Bu yutaklar emniyet supabıdır, ilerleme şartı değildir. Simdeki oyuncu bunlara yalnız "sıradaki Ana Üs bedelinin %50'si" yedeğinin üstündeki altını harcar.

## 7. Parametre listesi (EconomyConfig ScriptableObject için)
**R** işareti riskli değerdir; açıklaması §9'da.
| Ad | LEGACY | V2 | Oran |
|---|---|---|---|
| MainBaseCosts[2..6] | 150, 250, 400, 600, 850 | aynı | 1,00 |
| MainBaseCosts[7..15] | 1150, 1500, 1900, 2400, 3000, 3700, 4500, 5400, 6500 | 1250, 1800, 2500, 3500, 4800, 6400, 8400, 10800, 13800 | 1,09 … 2,12 **R** |
| UpgradeFactor (MB/Okçu/Topçu/Manastır/Maden/Kışla/Ev/Atölye) | 1 / .5 / .7 / .6 / .55 / .65 / .4 / .45 | aynı | 1,00 |
| Builder2GoldCost (yeni alan, sabit) | = UpgradeCost(MB, 6) = 850 | 850 (ayrık sabit) | 1,00 |
| MineGpsBase | 1,0 | 0,6 | 0,60 |
| MineGpsPerLevel | 0,5 | 0,2 | 0,40 **R** |
| MineStorageBase / PerLevel | 200 / 150 | aynı | 1,00 |
| HouseStorageBonus | 0,05 + 0,005(L−1) | aynı | 1,00 |
| ApprenticeMineBonus[1..15] (%) | 10, 15, 20, 25, 30, 32 … 50 | 5, 8, 10, 13, 15, 16 … 25 | 0,50–0,53 |
| WalletCapMult / Min | 1,5 / 500 | aynı | 1,00 |
| HeroCostBase / Growth | 60 / 1,28 | 75 / 1,30 | 1,25 / 1,016 (L15 ×1,55) **R** |
| ApprenticeCostBase / Growth | 80 / 1,30 | 100 / 1,32 | 1,25 / 1,015 (L15 ×1,55) |
| VictoryReward = a + b(r−1) + c(s−1) | 35 / 35 / 18 | 40 / 30 / 12 | 1,14 / 0,86 / 0,67 |
| KillGoldTransferRate (yeni) | 1,0 | 0,75 | 0,75 **R** (lider ayarı: 0,5 → 0,75, sim 1. gün MB 9→10) |
| EnemyGoldGrowthPerLevel (savaş içi) | 0,08 | aynı | 1,00 |
| ReplayRewardRate | 0,3 | aynı | 1,00 |
| VictoryDoubleAdsPerDay (yeni) | sınırsız | 5 | yeni **R** |
| BattleBuildCost (Sefer) | 50 / 75 / 60 × (1 + 0,2(r−1)) | aynı | 1,00 |
| EndlessBuildCostK (yeni formül, §5) | ×20,6 (hata) | 1,0 | yeni |
| InvasionBuildCost (yeni formül, §5) | ×20,4 (hata) | 1,0 | yeni |
| Takvim / görev / sandık / ada | §1 | aynı | 1,00 |
| Workshop craft gold | 180 / 430 / 1080 | aynı | 1,00 |
| Süreler, can, elmas fiyatları, GoldPerGem | §2 | aynı | 1,00 |
| Sefer Erzakı (yeni) | – | 30 + 30(r−1) | yeni |
| Ada Tüccarı (yeni) | – | 400 + 120(MB−5), 2/gün | yeni |
| Prestij Anıtları (yeni) | – | 1500 / 4000 / 9000 / 16000 | yeni |
- Hiçbir değer 3 kattan fazla değişmiyor (en büyük ×2,5 eğim azalması). Betik bunu `diff_table` ile kendisi denetliyor.
- **Uygulama yeri:** Zafer ödülü asset'te serileştirilmiş durumda. `GetVictoryBonus` (`BattleManager.cs:1022-1025`) EconomyConfig formülünü kullanmalı ya da asset'ler üretecle yeniden oluşturulmalı. 2. inşaatçı fiyatı `GameData.cs:889`'daki `UpgradeCost(MB, 6)` bağından koparılmalı.

## 8. Simülasyon
**Model (`Tools/economy_sim_v2.py`):**
- Oturumlar: günde 6 oturum (08, 11, 14, 17, 20, 22:30) × 8 dk. İlk oturum 60 dk.
- Maden: çevrimdışı depoya kadar birikiyor; oturumda her savaştan sonra toplanıyor.
- Can: yalnız yenilgide düşüyor.
- İnşaatçı: 1 tane; 2.'si MB5'te 850'ye alınıyor.
- Öncelik: MB → maden → kuleler/ev/kışla/atölye. Ana Üs bedelinin ≥%50'si eldeyse tek boş inşaatçı Ana Üs için bekletiliyor. Kahraman/çırak/atölye alımları için Ana Üs bedelinin %50'si yedek tutuluyor.
- Kazanma olasılığı aşama × bölge tablosundan geliyor; tekrar oyunda +, kule veya kahraman geri kaldığında − uygulanıyor.
- Reklam: 2x zafer (kullanım olasılığı 0,9), günlük sandık, 2 sa 2x maden, 3 hızlandırma.
- Öldürme altını asset'teki düşman sayıları ve `goldDrop` değerlerinden hesaplanıyor. Savaş içi kule harcaması sahip olunan kule sayısı kadar.
- Ölçümler:
  - % oturum süresi cüzdan tavanda
  - % süre altın ≥ sıradaki Ana Üs
  - tavan ve depo kaybı
  - gelir/harcama oranı
  - sıradaki Ana Üs = kaç savaş / kaç saat maden / kaç saat toplam gelir
  - kaynak ve kalem tabloları

<!-- SIM:BEGIN -->
_Uretildi: `python Tools/economy_sim_v2.py --seeds 40 --write` (tohum 40, 7 gun, oyuncu: PLAYER)._

#### Kontrol noktasi: 1 saat
| Metrik | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| Ana Us (medyan) | 9 | 8 | 8 |
| Sefer ilerleyisi (en sik) | B3-5 | B3-5 | B3-5 |
| Maden / Cirak / Kahraman (medyan) | 7 / 6 / 8 | 6 / 6 / 6 | 6 / 6 / 6 |
| Cuzdan / tavan (ort.) | 551 / 3460 | 698 / 3678 | 602 / 3645 |
| Siradaki Ana Us odemesi | 2478 | 2490 | 2510 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 473 | 280 | 307 |
| Siradaki Ana Us = kac savas | 5.5 | 9.3 | 8.5 |
| = kac saat cevrimici maden | 0.13 | 0.35 | 0.36 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 0.2 | 0.2 | 0.2 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 13446 | 11317 | 12001 |
| Oturum suresinin %'si cuzdan TAVANDA | 9.4 | 4.9 | 0.0 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 9.4 | 4.9 | 0.0 |
| Cuzdan tavanindan kaybolan altin (kum.) | 2111 | 114 | 13 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 68.5 | 20.2 | 15.8 |
| Toplam gelir / toplam harcama (kum.) | 1.04 | 1.06 | 1.05 |
| Savas / zafer (kum.) | 17 / 14 | 16 / 14 | 16 / 14 |

#### Kontrol noktasi: 1. gun
| Metrik | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| Ana Us (medyan) | 12 | 10 | 9 |
| Sefer ilerleyisi (en sik) | B5-1 | B5-1 | B5-1 |
| Maden / Cirak / Kahraman (medyan) | 11 / 12 / 12 | 10 / 7 / 9 | 10 / 7 / 8 |
| Cuzdan / tavan (ort.) | 6136 / 6770 | 2227 / 6402 | 2149 / 6155 |
| Siradaki Ana Us odemesi | 5042 | 4515 | 4475 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 767 | 346 | 342 |
| Siradaki Ana Us = kac savas | 6.9 | 13.6 | 13.6 |
| = kac saat cevrimici maden | 0.16 | 0.45 | 0.45 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 1.9 | 2.4 | 2.3 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 2619 | 1890 | 1918 |
| Oturum suresinin %'si cuzdan TAVANDA | 10.6 | 2.8 | 0.0 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 12.2 | 2.8 | 0.0 |
| Cuzdan tavanindan kaybolan altin (kum.) | 4684 | 114 | 13 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 93.5 | 85.0 | 84.7 |
| Toplam gelir / toplam harcama (kum.) | 1.17 | 1.08 | 1.07 |
| Savas / zafer (kum.) | 27 / 21 | 27 / 21 | 27 / 22 |

#### Kontrol noktasi: 3. gun
| Metrik | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| Ana Us (medyan) | 15 | 13 | 13 |
| Sefer ilerleyisi (en sik) | tamam | B6-1 | B6-1 |
| Maden / Cirak / Kahraman (medyan) | 15 / 15 / 15 | 13 / 13 / 13 | 13 / 12 / 12 |
| Cuzdan / tavan (ort.) | 9797 / 9800 | 7243 / 17370 | 6485 / 15705 |
| Siradaki Ana Us odemesi | 0 | 12990 | 12090 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 257 | 836 | 813 |
| Siradaki Ana Us = kac savas | 0.0 | 15.7 | 15.0 |
| = kac saat cevrimici maden | 0.00 | 0.98 | 0.91 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 0.0 | 7.9 | 7.4 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 646 | 1643 | 1633 |
| Oturum suresinin %'si cuzdan TAVANDA | 33.9 | 1.4 | 0.0 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 15.4 | 1.5 | 0.1 |
| Cuzdan tavanindan kaybolan altin (kum.) | 36723 | 186 | 27 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 97.4 | 90.3 | 90.2 |
| Toplam gelir / toplam harcama (kum.) | 1.12 | 1.08 | 1.07 |
| Savas / zafer (kum.) | 52 / 38 | 51 / 40 | 51 / 40 |

#### Kontrol noktasi: 7. gun
| Metrik | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| Ana Us (medyan) | 15 | 15 | 15 |
| Sefer ilerleyisi (en sik) | tamam | tamam | tamam |
| Maden / Cirak / Kahraman (medyan) | 15 / 15 / 15 | 15 / 15 / 15 | 15 / 15 / 15 |
| Cuzdan / tavan (ort.) | 9800 / 9800 | 20642 / 20700 | 18208 / 20700 |
| Siradaki Ana Us odemesi | 0 | 0 | 0 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 134 | 276 | 726 |
| Siradaki Ana Us = kac savas | 0.0 | 0.0 | 0.0 |
| = kac saat cevrimici maden | 0.00 | 0.00 | 0.00 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 0.0 | 0.0 | 0.0 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 487 | 849 | 1796 |
| Oturum suresinin %'si cuzdan TAVANDA | 51.8 | 14.1 | 1.2 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 7.2 | 0.9 | 0.1 |
| Cuzdan tavanindan kaybolan altin (kum.) | 101607 | 18636 | 1426 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 98.4 | 92.7 | 91.3 |
| Toplam gelir / toplam harcama (kum.) | 1.07 | 1.10 | 1.07 |
| Savas / zafer (kum.) | 100 / 71 | 100 / 74 | 99 / 74 |

#### Kaynak bazinda gelir (kumulatif, 1. gun)
| Kaynak | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| mine | 25646 | 20642 | 21027 |
| battle_reward | 2409 | 2110 | 2126 |
| battle_kills | 6577 | 5328 | 5397 |
| x2_ad | 6554 | 1070 | 1072 |
| calendar | 100 | 100 | 100 |
| quests | 297 | 718 | 694 |
| ad_chest | 64 | 64 | 64 |
| island | 253 | 203 | 200 |
| TOPLAM | 41900 | 30235 | 30680 |

#### Kalem bazinda harcama (kumulatif, 1. gun)
| Kalem | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| MainBase | 18620 | 10538 | 10425 |
| GoldMine | 7424 | 7273 | 7058 |
| Archer | 1581 | 4158 | 4066 |
| Cannon | 426 | 1708 | 1933 |
| Monastery | 2 | 31 | 38 |
| House | 74 | 256 | 281 |
| Barracks | 0 | 0 | 0 |
| Workshop | 0 | 0 | 0 |
| builder2 | 850 | 850 | 850 |
| hero | 2750 | 1635 | 1496 |
| apprentice | 4086 | 1611 | 1495 |
| craft | 0 | 0 | 0 |
| supplies | 0 | 0 | 404 |
| island_trade | 0 | 0 | 537 |
| monument | 0 | 0 | 0 |
| TOPLAM | 35813 | 28058 | 28582 |

#### Kaynak bazinda gelir (kumulatif, 7. gun)
| Kaynak | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| mine | 103456 | 165020 | 195721 |
| battle_reward | 4591 | 6340 | 6951 |
| battle_kills | 17525 | 30685 | 36377 |
| x2_ad | 13348 | 18730 | 22331 |
| calendar | 327 | 1841 | 3620 |
| quests | 843 | 4163 | 6772 |
| ad_chest | 525 | 1631 | 2455 |
| island | 1013 | 3072 | 4199 |
| TOPLAM | 141628 | 231481 | 278425 |

#### Kalem bazinda harcama (kumulatif, 7. gun)
| Kalem | LEGACY | V2 | V2+SINK |
|---|---|---|---|
| MainBase | 32300 | 55500 | 55500 |
| GoldMine | 17780 | 30540 | 30540 |
| Archer | 32460 | 53262 | 55122 |
| Cannon | 22710 | 38968 | 38965 |
| Monastery | 56 | 89 | 520 |
| House | 8903 | 7005 | 4946 |
| Barracks | 0 | 0 | 0 |
| Workshop | 0 | 11 | 25 |
| builder2 | 850 | 850 | 850 |
| hero | 6590 | 9590 | 9590 |
| apprentice | 10230 | 14920 | 14920 |
| craft | 0 | 153 | 138 |
| supplies | 0 | 0 | 1363 |
| island_trade | 0 | 0 | 17289 |
| monument | 0 | 0 | 30500 |
| TOPLAM | 131878 | 210888 | 260267 |

#### Sonsuz Sefer: savas ici kule bedeli
| Kat | Savas sev. | Baslangic + dalga 1 altini | LEGACY Okcu / ilk kule kacinci oldurmede | V2 Okcu/Topcu/Manastir | V2 ilk Okcu kacinci oldurmede | V2 Okcu / (bas.+dalga1) |
|---|---|---|---|---|---|---|
| 1 | 31 | 483 | 1030 / 29 (dalga 2+) | 170 / 255 / 205 | 3 | 35% |
| 6 | 36 | 645 | 1030 / 26 (dalga 2+) | 240 / 355 / 285 | 5 | 37% |
| 11 | 41 | 789 | 1030 / 23 (dalga 2+) | 300 / 445 / 355 | 6 | 38% |
| 21 | 51 | 1175 | 1030 / 20 | 460 / 690 / 550 | 8 | 39% |
| 26 | 56 | 1975 | 1030 / 13 | 560 / 845 / 675 | 7 | 28% |
| 31 | 61 | 1641 | 1030 / 17 | 650 / 980 / 785 | 10 | 40% |
| 51 | 81 | 2147 | 1030 / 13 | 865 / 1295 / 1035 | 11 | 40% |

#### Profil farki (V2+SINK vs LEGACY, otomatik; !! = 3x kisiti asildi)
| Parametre | LEGACY | V2 | Oran V2/LEGACY |
|---|---|---|---|
| mb_costs | [0, 0, 150, 250, 400, 600, 850, 1150, 1500, 1900, 2400, 3000, 3700, 4500, 5400, 6500] | [0, 0, 150, 250, 400, 600, 850, 1250, 1800, 2500, 3500, 4800, 6400, 8400, 10800, 13800] | 1.00..2.12 |
| mine_gps_base | 1.0 | 0.6 | 0.60 |
| mine_gps_slope | 0.5 | 0.2 | 0.40 |
| apprentice_mine_bonus | [10, 15, 20, 25, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50] | [5, 8, 10, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25] | 0.50..0.53 |
| hero_cost_base | 60 | 75 | 1.25 |
| hero_cost_growth | 1.28 | 1.3 | 1.02 |
| app_cost_base | 80 | 100 | 1.25 |
| app_cost_growth | 1.3 | 1.32 | 1.02 |
| reward_base | 35 | 40 | 1.14 |
| reward_per_region | 35 | 30 | 0.86 |
| reward_per_stage | 18 | 12 | 0.67 |
| kill_transfer | 1.0 | 0.75 | 0.75 |
| x2_daily_limit | None | 5 | yeni/bicim |
| endless_formula | legacy | v2 | yeni/bicim |
| sinks | None | {'supplies_base': 30, 'supplies_per_region': 30, 'supplies_win_bonus': 0.04, 'trade_base': 400, 'trade_per_mb': 120, 'trades_per_day': 2, 'monuments': [(6, 1500), (9, 4000), (12, 9000), (14, 16000)], 'monument_reserve': 1.0} | yeni/bicim |
<!-- SIM:END -->

**Ayar sırası (sim hedefleri tutmazsa):**
1. `KillGoldTransferRate` (0,4–0,7)
2. `MainBaseCosts[11..15]`
3. `MineGpsPerLevel` (0,2–0,3)
4. `VictoryDoubleAdsPerDay` (3–8)

## 9. Riskler ve AI_README ile çatışmalar (onay gerekir, üzerine yazılmadı)
- **ÇATIŞMA: 2x reklam sınırı.** `Docs/AI_README.md:87` ve `AdRewards.cs:7` zaferde 2x reklamı günlük haksız ("savaş başına 1") tanımlıyor. V2'deki 5/gün sınırı bu kararı değiştiriyor; kullanıcı onayı gerekir.
- **Belge tutarsızlığı (V2'den bağımsız):** `Docs/AI_README.md:84` can yenilenmesini "30 dk" diyor; `TimingConfig.asset:16` 20 dk.
- **R: KillGoldTransferRate 0,75.** Oyuncu "kazandığım altının yarısı nereye gitti" diye sorabilir. Sonuç ekranında "Ganimet %75" satırı gösterilmeli (`BattleUI.cs:268` metni). Savaş içi denge değişmiyor.
- **R: MainBaseCosts[11..15] ×1,6–2,1.** 4.–6. bölgelerde altın darboğazı yaratabilir. Kule veya kahraman geride kalırsa yenilgi artar; ORTA hedefinde "yenilgi açıklanabilir olmalı" ilkesi korunmalı.
- **R: MineGpsPerLevel 0,5→0,2.** Maden yükseltmesi daha az "hissedilir" olur. UI'da "+0,2/sn" yerine "+%X" ya da "saatlik" gösterim düşünülebilir.
- **R: Hero ×1,55.** Kahraman geri kalırsa B4–B6'da yenilgi artar; telemetride izlenmeli.
- **Elmas yan etkisi:** `GoldPerGem` 40 değişmiyor, ama V2'de geç bedeller ×2 olduğu için eksik altını elmasla kapatmak da ×2 pahalanıyor. Bu bir hızlandırma fiyatı değil, yine de gelir tarafını etkiler; bilinçli karar verilmeli.
- **Atölye:** Altın:Et 16:1 varsayımı (`GameData.cs:775-776`, island_base_v2 §17) V2 gelirinde kart üretimini görece pahalılaştırır. Atölye fiyatları değişmedi; ölçülmeli.
- **Tavan kaybı ve 2x reklam:** `VictoryReferenceView.cs:146` `AddGold` tavana kırpıyor; tavandaki oyuncu reklamı boşuna izleyebilir. V2'de tavana nadiren varılmalı, yine de düğme tavanda gizlenmeli ya da uyarı vermeli.
- **Model sınırı:** Öldürme altını sayaç varyantlarını (zırhlı / uçan ×1,3 / sabotajcı ×0,5) ve kart etkilerini yok sayıyor. Kazanma olasılıkları bot ölçümünden değil, tahminden geliyor.

## 10. Telemetriyle doğrulama
- **Eklenecek alanlar (PlaytestBot telemetrisi + canlı analitik):**
  - `wallet_at_cap_seconds`
  - `gold_lost_wallet_cap` (`AddGold` kırpması) ve `gold_lost_mine_storage`
  - `x2_ads_used_today`
  - Ana Üs yükseltmesi başlarken bekleme türü: `wait_gold_s` ve `wait_builder_s` (hangisi bağlayıcıydı)
  - Oynanan saat ve gün başına MB seviyesi
  - Bölge/aşama yenilgi oranı ve yenilgi anındaki kahraman/kule–MB farkı
- **Kabul ölçütleri:**
  - Tavan süresi < %10
  - `wait_gold_s / (wait_gold_s + wait_builder_s)` oranı %30–60
  - MB15 4.–6. günde
  - B1–B2 yenilgi < %10, B4–B6 yenilgi LEGACY'den en çok +5 puan
  - Sonsuz kat 1–10'da ilk kule ≤ 1. dalga
- **Koşum:** `-testFilter PlaytestBotTests` ve `PLAYTEST_SEEDS` ile LEGACY ve V2 eşlenik koşulur (CLAUDE.md komutu). Sonra `python Tools/economy_sim_v2.py --write` ile sim ve bot sonuçları yan yana konur.
