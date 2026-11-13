# DATA_SAFETY — Google Play "Veri güvenliği" formu için envanter

Durum: 13 Kasım 2026, dal `gece-gelistirme`. Kaynak: kod taraması (`Assets/Scripts`), `Packages/manifest.json`, `ProjectSettings/UnityConnectSettings.asset`.
**Bu belge mevcut sürümü anlatır. Reklam (AdMob), satın alma (Play Billing), bulut kaydı (Play Games) eklendiğinde formun
yeniden doldurulması gerekir** (aşağıda §4).

## 1. Uygulama hiçbir veriyi cihazdan dışarı göndermiyor
- Kodda ağ çağrısı yok: `UnityWebRequest`, `HttpClient`, soket kullanımı bulunamadı.
- Reklam SDK'sı yok (ödüllü reklamlar şimdilik 3 sn'lik deneme ekranı: `Systems/RewardedAds.cs`), analitik SDK'sı yok, Firebase
  kaldırıldı (10 Kasım), satın alma SDK'sı yok.
- Unity Services: Analytics, Crash Reporting (Cloud Diagnostics), Performance Reporting **kapalı** (`UnityConnectSettings.asset`).
  ⚠️ Unity motorunun kendi başlangıç telemetrisi: Unity 6'da kişisel/oyuncu verisi değil, yalnız motor kurulum istatistiği;
  Unity'nin kendi "Data Safety" yönergesi bunun beyan gerektirmediğini söyler — yayından önce Unity'nin güncel Google Play
  Data Safety sayfasıyla bir kez daha kontrol et (BENİM).
- Kayıt yalnız cihazda: PlayerPrefs + `persistentDataPath/save.json` (oyun ilerlemesi, ayarlar, ada adı).
- Bildirimler yalnız yerel (`com.unity.mobile.notifications`, sunucu yok). Android 13+ için `POST_NOTIFICATIONS` izni istenir.

## 2. Form cevapları (mevcut sürüm)
| Soru | Cevap |
|---|---|
| Uygulamanız gerekli kullanıcı veri türlerinden herhangi birini topluyor veya paylaşıyor mu? | **Hayır** |
| Veriler aktarım sırasında şifreleniyor mu? | (Veri aktarımı yok — "Hayır" cevabından sonra sorulmaz) |
| Kullanıcılar verilerinin silinmesini isteyebilir mi? | Veri sunucuda yok; uygulamayı silmek/verilerini temizlemek tüm kaydı siler |
| Hesap oluşturma | Yok |

Not: Oyuncunun yazdığı **ada adı** cihazda kalır, hiçbir yere gönderilmez → "toplanan veri" sayılmaz (Google tanımı: cihazdan
dışarı aktarılan veri).

## 3. İzinler (yayın AAB manifestinden; bkz. PLAY_RELEASE.md §5 AAB raporu)
- `POST_NOTIFICATIONS` (yerel bildirim), `RECEIVE_BOOT_COMPLETED` (zamanlanmış bildirimler yeniden başlatmadan sonra) — bildirim paketi.
- `INTERNET`: Unity varsayılan olarak ekleyebilir; kullanılmıyor. Kaldırmak gerekmez (beyan değiştirmez).

## 4. İleride eklenecekler ve formdaki karşılıkları (eklenince güncellenecek)
| Özellik | Toplanan / paylaşılan veri (Play kategorisi) | Amaç |
|---|---|---|
| AdMob ödüllü reklam | Cihaz veya diğer kimlikler (reklam kimliği), Uygulama etkileşimleri, Teşhis (çökme/performans), yaklaşık konum (IP'den) — **paylaşılır** (Google) | Reklam, analiz, dolandırıcılık önleme |
| UMP (izin) | İzin durumu cihazda | Yasal uyum (GDPR/KVKK) |
| Play Billing | Satın alma geçmişi — toplanır | Uygulama işlevi |
| Play Games bulut kaydı | Oyun ilerlemesi, kullanıcı kimliği (Play Games) — toplanır | Uygulama işlevi, hesap |
| Unity Cloud Diagnostics | Çökme günlükleri, teşhis — toplanır | Analiz |
