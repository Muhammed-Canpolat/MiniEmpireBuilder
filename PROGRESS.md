# PROGRESS — gece geliştirme (11 Kasım 2026)

**Geri dönüş noktası:** `a8cc598` (dal `ui/dark-fantasy-hud`). Gece dalı: `gece-gelistirme` (push yok; sabah kullanıcı push eder).

## Kararlar (gerekçeli)
- Push yok, yalnız commit: gece talimatı "asla push" diyor; sabah gözden geçirip kullanıcı push eder.
- CLAUDE.md kısa; ayrıntı `Docs/AI_README.md`'de (iki kopya birbirinden kopmasın).
- Bot oyun kodunda (`Systems/PlaytestBot.cs`, yalnız `UNITY_EDITOR || DEVELOPMENT_BUILD`): test asmdef'i
  Assembly-CSharp'a yalnız yansıma ile erişebildiği için tipli kod oyun tarafında, test ince sarmalayıcı.
- Bot "beklenen oyuncu": Ana Üs = bölge şartı + (aşama-1)/2, kahraman = Ana Üs, kuleler Ana Üs-1; elmas 0
  (devam teklifi = yenilgi). Zorluk ORTA hedefi: beklenen oyuncu 1. aşamaları rahat, 5. aşamaları zorlanarak kazanmalı.
- Gradle şablonlarındaki Android Resolver blokları silindi: hedef yalnız ARM64, ABI dışlama bloğu gereksizdi.

## Yapılanlar
- `a8cc598` .meta + packages-lock; `38531c3` gradle temizliği.
- CLAUDE.md, `.claude/agents/` (explorer, game-designer, art-director, gameplay-engineer, qa-playtester, security-reviewer).
- PlaytestBot + `PlaytestBotTests` (bölge 1–6 × aşama 1/5, JSON telemetri + kamera görüntüsü).

## Başlangıç ölçümü
- PlayMode: 50 test, 42 geçti, 8 kaldı (ayrıntı AUDIT.md "Kırık testler").

- `91f40a9` bot yapılara takılınca yana kayar (ilk Bölge 1 kayıpları bot hatasıydı).
- `b203b41` GameClock (saat geri alma açığı), elmas taşma sınırı, SaveSystemTests düzeltmesi.
- `0571445` AUDIT.md, ROADMAP.md, Docs/design/balance_v1.md (game-designer).
- Denge paketi v1 (B1–B4): dalga başına 1–2 taraf + 3 sn kırmızı ok uyarısı, başlangıç altını 60+15(r-1)
  (zafer ödülünden düşülür → ekonomi değişmez), ilk dalga sınırı 8+r, doğma aralığı max(0.9, 1.25-0.07(r-1)),
  Bölge 6 dalga sırası Gölge Kurt→Zombi→Ork ve ilk dalga 12–14.

## Kararlar (devam)
- Güvenlik S3 (kayıt imzası) ve S4 (reklam taslağı sürümde) kullanıcı kararına bırakıldı: S3 bulut kaydıyla birlikte
  tasarlanmalı; S4 kapalı test oyuncularını etkiler.
- GameClock 7 günden fazla geri sapmada sıfırlanır: yanlış saat ayarı oyuncuyu kilitlemesin (ileri sıçrama istismarı
  her seferinde yeni bir 7+ gün sıçraması ister, takvim serisini ve zamanlayıcıları bozar → caydırıcı).
- Ana Üs canı (+100 taban) ve Bölge 4–5 sıkılaştırması önce ölçülüp sonra karar verilecek (designer: önce 1–4).

- `ce9c343` 7 eski test güncellendi; `aee4db4` his + Bölge 4–6 kimliği; `d6e648e` boss ekleri, kule hasar artışı düşük,
  bot sıradan oyuncu profili; `483cd1d` yanlış eklenen Dragon Warrior dosyaları takipten çıkarıldı.
- Son ölçüm: iyi oyuncu 36/36; sıradan oyuncu S1 18/18, boss 11/18 (%61).

## Kararlar (gece sonu)
- Botun ilk sürümü kötü oynuyordu (takılma, uyarıyı yok sayma, iyileşmeden bekleme); her biri düzeltildi ve
  "iyi" + "sıradan" iki profil ölçüldü. Orta zorluk = iyi oyuncu rahat, sıradan oyuncu boss'ta %55–75.
- Ana Üs canı +100 yerine yalnız boss ekleriyle oynanabilirdi; Bölge 1 öğretici olduğu için ikisi birlikte tutuldu.

## Sıradakiler
MORNING_REPORT.md "Sonraki oturum için ilk 5 iş".

## Round 2 (11 Kasım 2026) — kullanıcı bulguları A–H
Commit'ler: 22db715 (F DevSettings), 6dc9569 (H süreler), c1925de (C kule eğrisi + D başlangıç altını), 25886df (ekonomi sim),
ed0ed14 (B ızgara yerleşim), 2a47371 (C karşı düşman + kalkan), 7d90875 (E sanat), 35f1e17 (E mekanik), 7bc6c15 (G ilk oturum),
cffb20d (A görevler), 8e72057 (denge yumuşatma + bot profilleri).
Kararlar: başlangıç altını Ana Üs seviyesine bağlı (round2.md D); lav boss'lara işlemez (yüzde hasar boss'u eritiyordu);
sabotajcı fitili 1,0 sn ve R3+ kalkan çarpanı 0,25 (sıradan oyuncu boss'u hiç geçemiyordu); süre geçişi oransal.
Ajanlar: explorer (yerleşim haritası), game-designer (round2.md), 3× gameplay-engineer (B, C, E), qa-playtester (ilk 30 dk).
Dersler: commit'te asla `git add Assets` / `-a` kullanma (korunan dosyalar giriyor); sed satır numarasıyla düzenleme yapma.
