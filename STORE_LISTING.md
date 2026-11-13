# STORE_LISTING — Google Play mağaza sayfası taslağı

Paket adı: `com.kingdomforgegames.minikingdombuilder` (ProjectSettings; **ilk yüklemeden sonra değiştirilemez** — BENİM onayım gerekli).
Kategori önerisi: **Oyun → Strateji**. Etiketler: Kale kurma, Kule savunma, Çevrimdışı, Tek oyunculu, Piksel sanat.

## 1. Başlık (≤ 30 karakter)
- TR: **Mini Empire Builder** (19) · alternatif: "Mini İmparatorluk: Kale ve Ada" (30)
- EN: **Mini Empire Builder** (19) · alternatif: "Mini Empire: Keep & Isle" (24)

## 2. Kısa açıklama (≤ 80 karakter)
- TR: `Üssünü kur, kahramanını güçlendir, adanı akınlara karşı savun!` (63)
- EN: `Build your base, power up your hero and defend your island from raids!` (70)

## 3. Uzun açıklama (≤ 4000 karakter)

### Türkçe
Küçük bir krallığın başındasın. Madeninden altın topla, binalarını yükselt ve kahramanınla düşman dalgalarını durdur!

**ÜSSÜNÜ KUR**
• Ana Üs'ü 15 seviyeye kadar büyüt; Okçu ve Topçu kuleleri, Manastır, Kışla, Köylü Evi ve Kart Atölyesi kur.
• Altın madeni sen yokken de üretir; inşaatçıların gerçek zamanlı çalışır.
• Binalarını istediğin yere taşı, üssünü dekorlarla süsle.

**SEFERE ÇIK**
• 6 bölge ve 30 aşama: çayırlar, sisli orman, terk edilmiş maden, donmuş topraklar, karanlık diyarlar ve Ejderin İni.
• Her bölgenin kendine özgü düşmanları ve güçlü bir boss'u var.
• Kulelerini savaş alanına kur, kahramanınla öne atıl, Ana Üs'ünü koru.
• Sonsuz Sefer ve hafta sonu Ejder İstilası ile sınırlarını zorla.

**ADANI SAVUN**
• Ana Üs 5'te Martı Adası açılır: adana ad ver, oduncu kampı, ağıl ve gözcü kuleleri kur.
• Bulutların altındaki bölgeleri keşfet, ağaçları kes, yeni alanlar aç.
• Okçu Kaptan ile akınlara karşı savun: Ok Yağmuru, Ateşli Ok ve Ağ Oku yeteneklerini kullan.

**ÖZELLİKLER**
• Çevrimdışı oynanır, internet gerekmez.
• El yapımı piksel sanat, akıcı animasyonlar.
• Günlük ödül takvimi ve günlük görevler.
• Türkçe ve İngilizce.

### English
You rule a tiny kingdom. Mine gold, upgrade your buildings and stop the enemy waves with your hero!

**BUILD YOUR BASE**
• Grow your Main Base to level 15; build Archer and Cannon towers, a Monastery, Barracks, Cottages and a Card Workshop.
• Your gold mine keeps producing while you are away; builders work in real time.
• Move your buildings anywhere and decorate your base.

**GO ON CAMPAIGN**
• 6 regions and 30 stages: meadows, a misty forest, an abandoned mine, frozen lands, dark realms and the Dragon's Lair.
• Every region has its own enemies and a mighty boss.
• Place towers on the battlefield, lead with your hero and protect your Main Base.
• Push your limits in the Endless Campaign and the weekend Dragon Invasion.

**DEFEND YOUR ISLAND**
• At Main Base 5 Gull Island unlocks: name it, build a lumber camp, a pasture and watchtowers.
• Explore areas hidden under the clouds, clear trees and open new land.
• Defend against raids with the Archer Captain: use Volley, Fire Arrow and Net Arrow.

**FEATURES**
• Play offline, no internet needed.
• Hand-crafted pixel art and smooth animations.
• Daily reward calendar and daily quests.
• Turkish and English.

> ⚠️ Açıklamada yalnız oyunda gerçekten olan özellikler yazılmalı (Play "yanıltıcı açıklama" politikası). Satın alma/reklam eklenince
> "Uygulama içi satın alma içerir / Reklam içerir" etiketleri Play Console'dan işaretlenir.

## 4. Gerekli görseller
| Görsel | Ölçü / biçim | Zorunlu | Not |
|---|---|---|---|
| Uygulama ikonu | **512×512**, 32-bit PNG (alfa olabilir), ≤ 1 MB | Evet | Play köşeleri kendisi yuvarlar/gölge ekler; kenarlara metin koyma. Oyun içi ikon (adaptive icon: 432×432 ön plan + arka plan katmanı) ayrı |
| Öne çıkan görsel (feature graphic) | **1024×500**, JPG veya 24-bit PNG (alfa YOK) | Evet | Ortada logo + kale + kahraman; kenarlar kırpılabilir, önemli şeyi ortada tut |
| Telefon ekran görüntüleri | 2–8 adet; kısa kenar ≥ 320, uzun kenar ≤ 3840 px; en-boy ≤ 2:1. Öneri: **1080×1920** dikey (en az 4 adet → oyun önerilerinde öne çıkma şartı) | Evet (≥2) | PNG/JPG, alfa yok |
| 7" tablet görüntüleri | 1200×1920 önerilir, ≥ 1 adet | İsteğe bağlı (tablet listelemesi için önerilir) | Kullanıcının test cihazı tablet |
| 10" tablet görüntüleri | 1600×2560 önerilir | İsteğe bağlı | |
| Tanıtım videosu | YouTube bağlantısı | İsteğe bağlı | |

## 5. Oyundan alınabilecek ekran görüntüleri (öneri sırası)
1. **Ana Üs Sv 15** — dolu, düzenli üs, radyal menü açık (test: `Session3Tests` → `S2_base_radial_open_topbar`; gerçek cihazdan çekilmeli,
   "Development Build" yazısı olmadan → yayın APK'sı ile).
2. **Sefer savaşı** — kahraman + kuleler + dalga, boss can çubuğu (Bölge 2 Orman Cadısı ya da Bölge 6 Ejder).
3. **Ada üssü** — bulutlar dağılırken, piyonlar çalışırken (`S2_island_radial_topbar`).
4. **Ada akını** — Ok Yağmuru efekti, tekneler kıyıya yanaşırken.
5. **Sefer haritası** — 6 bölgenin resimli haritası.
6. **Yükseltme penceresi** — süslü pencere "Sv 7 → 8" değerleriyle (`S2_4_base_mainbase_panel`).
7. **Zafer ekranı** — yıldızlar ve ganimet.
8. **Kart Atölyesi** — kart seçimi.

Her görüntünün üstüne 3–5 kelimelik başlık bandı eklenebilir ("ÜSSÜNÜ KUR", "ADANI SAVUN"…) — Play'de serbest, metin okunaklı olmalı.
