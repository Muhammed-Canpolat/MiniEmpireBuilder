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
| Metrik | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| Ana Us (medyan) | 9 | 8 | 5 | 5 |
| Sefer ilerleyisi (en sik) | B3-5 | B3-5 | B3-1 | B3-1 |
| Maden / Cirak / Kahraman (medyan) | 7 / 6 / 8 | 6 / 6 / 6 | 6 / 1 / 3 | 6 / 1 / 3 |
| Cuzdan / tavan (ort.) | 551 / 3460 | 698 / 3678 | 64 / 1405 | 81 / 1405 |
| Siradaki Ana Us odemesi | 2478 | 2490 | 970 | 970 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 473 | 280 | 246 | 243 |
| Siradaki Ana Us = kac savas | 5.5 | 9.3 | 4.0 | 4.1 |
| = kac saat cevrimici maden | 0.13 | 0.35 | 0.93 | 0.94 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 0.2 | 0.2 | 0.2 | 0.2 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 13446 | 11317 | 5600 | 5544 |
| Oturum suresinin %'si cuzdan TAVANDA | 9.4 | 4.9 | 0.0 | 0.0 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 9.4 | 4.9 | 0.0 | 0.0 |
| Cuzdan tavanindan kaybolan altin (kum.) | 2111 | 114 | 139 | 127 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 68.5 | 20.2 | 0.0 | 0.0 |
| Toplam gelir / toplam harcama (kum.) | 1.04 | 1.06 | 1.00 | 1.01 |
| Savas / zafer (kum.) | 17 / 14 | 16 / 14 | 16 / 13 | 15 / 12 |

#### Kontrol noktasi: 1. gun
| Metrik | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| Ana Us (medyan) | 12 | 10 | 10 | 10 |
| Sefer ilerleyisi (en sik) | B5-1 | B5-1 | B4-3 | B4-3 |
| Maden / Cirak / Kahraman (medyan) | 11 / 12 / 12 | 10 / 7 / 9 | 9 / 7 / 8 | 9 / 7 / 8 |
| Cuzdan / tavan (ort.) | 6136 / 6770 | 2227 / 6402 | 2227 / 6820 | 2185 / 6678 |
| Siradaki Ana Us odemesi | 5042 | 4515 | 4920 | 4848 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 767 | 346 | 248 | 250 |
| Siradaki Ana Us = kac savas | 6.9 | 13.6 | 20.5 | 19.9 |
| = kac saat cevrimici maden | 0.16 | 0.45 | 3.52 | 3.48 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 1.9 | 2.4 | 3.0 | 2.9 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 2619 | 1890 | 1659 | 1651 |
| Oturum suresinin %'si cuzdan TAVANDA | 10.6 | 2.8 | 0.2 | 0.0 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 12.2 | 2.8 | 0.2 | 0.0 |
| Cuzdan tavanindan kaybolan altin (kum.) | 4684 | 114 | 154 | 127 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 93.5 | 85.0 | 0.0 | 0.0 |
| Toplam gelir / toplam harcama (kum.) | 1.17 | 1.08 | 1.09 | 1.09 |
| Savas / zafer (kum.) | 27 / 21 | 27 / 21 | 26 / 21 | 26 / 21 |

#### Kontrol noktasi: 3. gun
| Metrik | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| Ana Us (medyan) | 15 | 13 | 14 | 14 |
| Sefer ilerleyisi (en sik) | tamam | B6-1 | tamam | tamam |
| Maden / Cirak / Kahraman (medyan) | 15 / 15 / 15 | 13 / 13 / 13 | 14 / 14 / 14 | 14 / 14 / 14 |
| Cuzdan / tavan (ort.) | 9797 / 9800 | 7243 / 17370 | 8453 / 20700 | 3343 / 20700 |
| Siradaki Ana Us odemesi | 0 | 12990 | 690 | 1035 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 257 | 836 | 831 | 900 |
| Siradaki Ana Us = kac savas | 0.0 | 15.7 | 0.9 | 1.5 |
| = kac saat cevrimici maden | 0.00 | 0.98 | 0.34 | 0.53 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 0.0 | 7.9 | 0.3 | 0.5 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 646 | 1643 | 2107 | 2117 |
| Oturum suresinin %'si cuzdan TAVANDA | 33.9 | 1.4 | 0.1 | 0.0 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 15.4 | 1.5 | 0.4 | 0.0 |
| Cuzdan tavanindan kaybolan altin (kum.) | 36723 | 186 | 156 | 127 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 97.4 | 90.3 | 17.5 | 17.5 |
| Toplam gelir / toplam harcama (kum.) | 1.12 | 1.08 | 1.08 | 1.03 |
| Savas / zafer (kum.) | 52 / 38 | 51 / 40 | 51 / 38 | 51 / 38 |

#### Kontrol noktasi: 7. gun
| Metrik | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| Ana Us (medyan) | 15 | 15 | 15 | 15 |
| Sefer ilerleyisi (en sik) | tamam | tamam | tamam | tamam |
| Maden / Cirak / Kahraman (medyan) | 15 / 15 / 15 | 15 / 15 / 15 | 15 / 15 / 15 | 15 / 15 / 15 |
| Cuzdan / tavan (ort.) | 9800 / 9800 | 20642 / 20700 | 20700 / 20700 | 20700 / 20700 |
| Siradaki Ana Us odemesi | 0 | 0 | 0 | 0 |
| Ort. savas geliri (son 12 savas, reklam dahil) | 134 | 276 | 5 | 23 |
| Siradaki Ana Us = kac savas | 0.0 | 0.0 | 0.0 | 0.0 |
| = kac saat cevrimici maden | 0.00 | 0.00 | 0.00 | 0.00 |
| = kac saat (son 24 sa toplam gelir hiziyla) | 0.0 | 0.0 | 0.0 | 0.0 |
| Toplam gelir hizi (altin/sa, son 24 sa) | 487 | 849 | 840 | 971 |
| Oturum suresinin %'si cuzdan TAVANDA | 51.8 | 14.1 | 37.8 | 20.5 |
| Oturum suresinin %'si altin >= siradaki Ana Us | 7.2 | 0.9 | 0.2 | 0.0 |
| Cuzdan tavanindan kaybolan altin (kum.) | 101607 | 18636 | 41391 | 23602 |
| Maden deposu tasmasi (uretimin %'si, kum.) | 98.4 | 92.7 | 40.3 | 31.5 |
| Toplam gelir / toplam harcama (kum.) | 1.07 | 1.10 | 1.10 | 1.08 |
| Savas / zafer (kum.) | 100 / 71 | 100 / 74 | 99 / 70 | 99 / 70 |

#### Kaynak bazinda gelir (kumulatif, 1. gun)
| Kaynak | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| mine | 25646 | 20642 | 18707 | 18628 |
| battle_reward | 2409 | 2110 | 1877 | 1863 |
| battle_kills | 6577 | 5328 | 3912 | 3885 |
| x2_ad | 6554 | 1070 | 960 | 969 |
| calendar | 100 | 100 | 100 | 100 |
| quests | 297 | 718 | 742 | 727 |
| ad_chest | 64 | 64 | 64 | 64 |
| island | 253 | 203 | 180 | 177 |
| TOPLAM | 41900 | 30235 | 26541 | 26412 |

#### Kalem bazinda harcama (kumulatif, 1. gun)
| Kalem | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| MainBase | 18620 | 10538 | 11660 | 11452 |
| GoldMine | 7424 | 7273 | 4617 | 4500 |
| Archer | 1581 | 4158 | 3104 | 3124 |
| Cannon | 426 | 1708 | 1141 | 1124 |
| Monastery | 2 | 31 | 64 | 57 |
| House | 74 | 256 | 393 | 342 |
| Barracks | 0 | 0 | 0 | 0 |
| Workshop | 0 | 0 | 28 | 0 |
| builder2 | 850 | 850 | 850 | 850 |
| hero | 2750 | 1635 | 1300 | 1302 |
| apprentice | 4086 | 1611 | 1179 | 1118 |
| craft | 0 | 0 | 28 | 0 |
| supplies | 0 | 0 | 0 | 256 |
| island_trade | 0 | 0 | 0 | 153 |
| monument | 0 | 0 | 0 | 0 |
| TOPLAM | 35813 | 28058 | 24364 | 24278 |

#### Kaynak bazinda gelir (kumulatif, 7. gun)
| Kaynak | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| mine | 103456 | 165020 | 187253 | 215530 |
| battle_reward | 4591 | 6340 | 5038 | 5840 |
| battle_kills | 17525 | 30685 | 18723 | 26704 |
| x2_ad | 13348 | 18730 | 10935 | 16486 |
| calendar | 327 | 1841 | 1500 | 1540 |
| quests | 843 | 4163 | 3003 | 4851 |
| ad_chest | 525 | 1631 | 1408 | 2086 |
| island | 1013 | 3072 | 2135 | 3241 |
| TOPLAM | 141628 | 231481 | 229994 | 276278 |

#### Kalem bazinda harcama (kumulatif, 7. gun)
| Kalem | LEGACY | V2-5.oturum | V2 | V2+SINK |
|---|---|---|---|---|
| MainBase | 32300 | 55500 | 55500 | 55500 |
| GoldMine | 17780 | 30540 | 30540 | 30540 |
| Archer | 32460 | 53262 | 50292 | 50638 |
| Cannon | 22710 | 38968 | 38975 | 38965 |
| Monastery | 56 | 89 | 78 | 82 |
| House | 8903 | 7005 | 8292 | 6527 |
| Barracks | 0 | 0 | 0 | 0 |
| Workshop | 0 | 11 | 28 | 0 |
| builder2 | 850 | 850 | 850 | 850 |
| hero | 6590 | 9590 | 9590 | 9590 |
| apprentice | 10230 | 14920 | 14920 | 14920 |
| craft | 0 | 153 | 280 | 0 |
| supplies | 0 | 0 | 0 | 1468 |
| island_trade | 0 | 0 | 0 | 16049 |
| monument | 0 | 0 | 0 | 30500 |
| TOPLAM | 131878 | 210888 | 209344 | 255628 |

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
| mine_gps_base | 1.0 | 0.16666666666666666 | 0.17 !! >3x |
| mine_gps_slope | 0.5 | 0.025 | 0.05 !! >3x |
| mine_storage_base | 200 | 2400 | 12.00 !! >3x |
| mine_storage_slope | 150 | 360 | 2.40 |
| house_bonus_slope | 0.005 | 0.01 | 2.00 |
| apprentice_mine_bonus | [10, 15, 20, 25, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50] | [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15] | 0.10..0.30 !! >3x |
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

## 11. 6. oturum (14 Kasım 2026): çırak katkısı (2.1) ve maden deposu (2.3) — Economy_V2'ye uygulandı
Simülasyon: `python Tools/economy_sim_v2.py --seeds 40` (yukarıdaki SIM bloğu yenilendi). Sütunlar: **V2-5.oturum** = önceki Economy_V2, **V2** = şimdiki.

### 11.1 Çırak katkısı (yalnız 2.1'in etkisi, maden hızı sabitken)
| Durum | Önce (V2-5) | Sonra | Fark |
|---|---|---|---|
| Çırak Sv1 / Sv5 / Sv10 / Sv15 | %5 / %15 / %20 / %25 | %1 / %5 / %10 / %15 | −4 … −10 puan |
| Maden Sv10 + çırak Sv10, eski hız (2,4/sn) | 10.368 altın/sa | 9.504 altın/sa | −%8,3 |
| Maden Sv15 + çırak Sv15, eski hız (3,4/sn) | 15.300 altın/sa | 14.076 altın/sa | −%8,0 |

### 11.2 Maden: depo görünür ve anlamlı (2.3)
- **Teşhis (eski V2):** depo 200 + 150(L−1) iken maden 0,6–3,4 altın/sn üretiyordu; depo 4–10 dakikada doluyor, çevrimdışı üretimin **%85–93'ü taşıp
  kayboluyordu** (SIM: "Maden deposu taşması"). Depo oyuncuya hiçbir şey söylemiyordu, Köylü Evi'nin etkisi fark edilmiyordu.
- **Yeni kural:** saatlik üretim **600 + 90(L−1)** (Sv1 600, Sv10 1.410, Sv15 1.860 altın/sa; çırak +%L). Depo = **4 saatlik üretim**:
  **2.400 + 360(L−1)** (Sv1 2.400, Sv5 3.840, Sv10 5.640, Sv15 7.440). Köylü Evi depo **+%5, seviye başına +%1** (Sv15 %19; iki ev en çok %38).
  Depo dolunca üretim durur, madenin üstünde "DOLU" yanar. 4 saat seçildi: oyuncu modelinde oturum arası ~3 saat; depo bunu karşılar ama gece
  (8+ saat) dolar → "geri gel, topla" döngüsü korunur, Köylü Evi gece kaybını azaltan anlamlı bir yatırım olur.
- Eğri denemeleri (V2 sütunu; 1. gün / 3. gün Ana Üs medyanı): 150+60 → 8/11 (çok cimri), 300+120 → 9/14, **600+90 → 10/14**, 900+60 → 10/14.
  600+90 eski V2'nin 1. gün hızını korur; düz eğri (düşük eğim) maden yükseltmesinin altını patlatmasını önler.

### 11.3 Öncesi / sonrası (SIM, 40 tohum)
| Metrik | V2-5.oturum | V2 (şimdi) |
|---|---|---|
| Toplam gelir hızı, 1. gün (altın/sa, son 24 sa) | 1.890 | 1.659 |
| Toplam gelir hızı, 3. gün | 1.643 | 2.107 |
| Toplam gelir hızı, 7. gün | 849 | 840 |
| Ana Üs medyanı 1 sa / 1. gün / 3. gün / 7. gün | 8 / 10 / 13 / 15 | 5 / 10 / 14 / 15 |
| Maden deposu taşması 1. gün / 7. gün | %85 / %93 | %0 / %40 |
| Oturumun % kaçı cüzdan TAVANDA (1. gün) | 2,8 | 0,2 |
| Sıradaki Ana Üs = kaç savaş (1. gün) | 13,6 | 20,5 |

- **Altın değerli kalıyor mu?** Evet: 1. gün gelir %12 düştü, sıradaki Ana Üs 20 savaşlık; cüzdan tavanında geçen süre ~0. 3. günde gelir arttı
  (artık taşma yok, uzun aralar boşa gitmiyor) ve Ana Üs 15'e ~3,5. günde ulaşılıyor (hedef 4–6. gün; sınırda). 7. günde zaten her şey 15'te.
- **İlk saat:** Ana Üs 8 → 5. Eski V2'de ilk saatin hızı çevrimiçi madenden geliyordu (0,6–1,6 altın/sn sürekli toplanıyordu); şimdi ilk saat
  savaşla ilerler, Ana Üs 5 (ada) ilk saatte yine açılıyor. Affedici giriş korunuyor ama "ilk saatte Ana Üs 8" artık yok — bilinçli.
- **Modelde olmayan:** 2.2 (kahraman/çırak süreli) simülasyonda hâlâ anında; gerçek oyunda inşaatçı kuyruğu biraz daha yavaşlatır.
- **Kayıt uyumu:** depodaki altın korunur (yeni kapasite eskisinden büyük). Maden seviyesi ve evler aynı; yalnız hız/depo formülü değişti.


## 12. 6. oturum: Dükkân kurları (2.4) — `Resources/Config/ShopConfig.asset`
- **Altın kuru Ana Üs'le ölçeklenir:** 1 elmas = (Ana Üs seviyesindeki madenin saatlik üretimi) / 15 (en az 40). Ana Üs 1: 40 (eski sabit kur),
  Ana Üs 5: 64, Ana Üs 10: 94, Ana Üs 15: 124. Eski sabit 40 altın/elmas Ana Üs 15'te bir yükseltmeyi 345 elmasa çıkarıyordu (anlamsız);
  yeni kurla ~110 elmas. **Eksik altın tamamlama aynı kuru kullanır** (iki ayrı fiyat yok).
- **"Pahalı ama makul":** 1 saatlik toplam gelir (SIM 1. gün ~1.660 altın/sa, Ana Üs 10) ≈ 18 elmas; 1 saatlik inşaat hızlandırma = 17 elmas
  (`GemEconomy.FinishCost`). Yani altın almak, beklemeyi elmasla atlamakla aynı pahalılıkta — ne kestirme ne ceza.
- **Paket miktarları:** Altın = sıradaki Ana Üs bedelinin %15 / %40 / %100'ü (büyük paket = bir Ana Üs yükseltmesi; cüzdan tavanı 1,5× olduğu için
  taşmaz; taşacaksa satılmaz, "CÜZDAN DOLU"). Et/Odun = ada cüzdan tavanının %15 / %40 / %100'ü, kur sabit (1 elmas = 8 Et — kullanıcı kararı;
  1 elmas = 25 Odun). Bonus: orta +%10, büyük +%25 ("En iyi değer"). Ada kaynakları Ana Üs 5'ten önce kilitli.
- **Örnek (Ana Üs 10):** Altın Kese 720 = 8 elmas, Sandık 1.900 = 19, Hazine 4.800 = 41 elmas.
- **Gerçek para:** 100 / 550 / 1.250 elmas (taban 100 = 0,99 USD'ye göre +%10 / +%25 bonus). 550 elmas ≈ 32 saatlik inşaat hızlandırması.
- **Altın değerli kalıyor mu?** Evet: elmasla altın, oynayarak kazanmaktan kestirme değildir (aynı saat değeri); büyük paket bile tek bir Ana Üs
  yükseltmesini karşılar, süre hâlâ inşaatçı/sürelerle sınırlı.


## 13. 6. oturum: ada kurulum bedelleri (3.2) — `FeatureConfig.islandBuildGold`
| Bina | Altın | İkinci+ kurulumda ayrıca (Odun/Et) | Gerekçe |
|---|---|---|---|
| Okçuluk Alanı | 0 (ilk, öğretici) | 80 Odun | savunma şart; tek adet |
| Oduncu Kampı | 100 | 300 / 60 | ilk üretim zinciri; ada Odun'suz başlar |
| Ağıl | 100 | 600 / 0 | Et kıt; ilk ağıl ucuz |
| Ev | 250 | 40 / 0 | işçi kapasitesi (üretimi ölçekler) |
| Gözcü Kulesi | 350 | 60 / 10 | savunma |
| Kışla | 600 | 150 / 40 | Kale 3 |
| Manastır | 900 | 400 / 80 | Kale 6 |
- Ada Ana Üs 5'te açılır; o noktada SIM medyan cüzdanı ~250–650 altın (Ana Üs 5–6 bedeli 600–850). Oduncu + Ağıl (200) ilk oturumda alınabilir,
  pahalılar birkaç savaşlık birikim ister — ada altını "boş" bırakmaz, Ana Üs yükseltmesiyle hafifçe yarışır. Yükseltmeler Odun/Et ile (değişmedi).
