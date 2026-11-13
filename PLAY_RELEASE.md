# PLAY_RELEASE — Google Play kapalı test hazırlığı

Durum: 13 Kasım 2026 (3. oturum). Etiketler: ✅ **hazır** · 🔧 **yapılacak (geliştirme)** · 👤 **BENİM yapmam gereken** (kullanıcı).
Kaynaklar (13 Kasım 2026'da doğrulandı): [Hedef API şartı](https://support.google.com/googleplay/android-developer/answer/11926878),
[16 KB sayfa boyutu](https://developer.android.com/guide/practices/page-sizes),
[Kişisel hesapta test şartı](https://support.google.com/googleplay/android-developer/answer/14151465).

## 1. Güncel kurallar (doğrulandı)
- **Hedef API:** 31 Ağustos 2026'dan itibaren yeni uygulamalar ve güncellemeler **Android 16 (API 36)** veya üstünü hedeflemeli
  (mevcut uygulamalar en az API 35). → Bizde hedef **36** (`AndroidReleaseBuild.TargetApi`). ✅
- **16 KB bellek sayfası:** Google'ın geliştirici sayfasına göre **1 Şubat 2027**'den itibaren 16 KB'yi desteklemeyen güncellemeler
  yayımlanamaz (bazı üçüncü taraf kaynaklarda daha eski tarihler geçiyor; resmî sayfa esas). Yerel kod içeren uygulamalarda tüm `.so`
  dosyalarının ELF LOAD segmentleri 16 KB hizalı olmalı. Unity 6000.3 + NDK r27c varsayılan olarak 16 KB hizalar; AAB'de denetlendi (§5). ✅
- **Kişisel geliştirici hesabı (13 Kasım 2023 sonrası açılmış):** üretime başvurmadan önce **en az 12 test kullanıcısı, kesintisiz
  14 gün** kapalı testte kalmalı; açık test sayılmaz; 14 gün sonunda "üretim erişimi" anketi doldurulur, otomatik açılmaz. 👤

## 2. Teknik kontrol listesi
| Madde | Durum | Not |
|---|---|---|
| Çıktı AAB (App Bundle) | ✅ | `Tools → Mini Empire Builder → Google Play Release AAB` ya da `-executeMethod AndroidReleaseBuild.BuildFromCommandLine` |
| IL2CPP + yalnız ARM64 | ✅ | Play 64-bit şartı; armv7 yok (eski 32-bit cihazlar listelenmez, bilinçli) |
| Hedef API 36 | ✅ | SDK platform 36 Unity kurulumunda var |
| En düşük API 25 (Android 7.1) | ✅ | Unity 6 en az 23 destekler; 25 mevcut ayardı, korunuyor (≈%99+ cihaz) |
| Sürüm adı / kodu düzeni | ✅ | Ad `MAJOR.MINOR.PATCH` (kapalı test `0.1.0` ile başlar); kod her yüklemede **+1** tamsayı (1, 2, 3…). Ortam: `MEB_VERSION_NAME`, `MEB_VERSION_CODE` |
| Development Build kapalı | ✅ | Yayın betiği `BuildOptions.None`, `development=false`, hata ayıklama kapalı |
| Debug.Log kapalı | ✅ | `Systems/ReleaseLogging.cs`: yayında yalnız uyarı/hata/istisna; Log/Warning yığın izi yok |
| Test kolaylıkları derleme dışı | ✅ | `DevSettings.Available` sabiti yayında `false`; DEV menüsü, bot, hata ayıklama katmanları, elmas +100 kısayolu `#if UNITY_EDITOR || DEVELOPMENT_BUILD` içinde. Test: `Session3Tests.CheatsAreCompiledOutOfReleaseBuilds` + AAB metadata taraması (§5) |
| 16 KB hizası | ✅ | `Tools/check_aab.py` (§5) |
| Paket adı | 👤 | `com.kingdomforgegames.minikingdombuilder` — ilk yüklemeden sonra değişmez; onayla |
| Ürün adı / şirket adı | 🔧👤 | `productName: MiniEmpireBuilder`, `companyName: DefaultCompany` → cihazda görünen ad "Mini Empire Builder" olmalı; şirket adını söyle |
| Uygulama ikonu (adaptive) | 🔧👤 | Player Settings'te Android ikonları boş/varsayılan olabilir; 512 px ikon + adaptive katmanlar gerekli (STORE_LISTING §4) |
| Yükleme anahtarı (upload key) | 👤 | §4 |
| Play App Signing | 👤 | Play Console varsayılan; kabul et (uygulama imza anahtarını Google saklar) |
| Reklam SDK + UMP | 🔧 | Şu an reklam SDK'sı yok (ödüllü reklamlar 3 sn deneme ekranı). Kapalı testte sorun değil; **gerçek reklam gelmeden "Reklam içerir: Hayır"** işaretlenir. AdMob eklenince UMP izin akışı şart (§6) |
| Uygulama içi satın alma | — | Yok → Play Billing şartı şimdilik yok. Eklenince Play Billing Library **en az 8** (7.x 31 Ağustos 2026'dan beri kabul edilmiyor), tercihen güncel **9.x** ([SSS](https://developer.android.com/google/play/billing/deprecation-faq)); Unity IAP sürümünün hangi PBL'yi paketlediği kontrol edilmeli 👤🔧 |
| Bildirim izni (Android 13+) | ✅ | `POST_NOTIFICATIONS` oyun içinden bir kez istenir |
| Ödüllü reklam deneme ekranı yayında | 🔧 | Kapalı testte ödül verir (deneme); gerçek SDK gelene dek mağaza açıklamasında reklamdan söz edilmez |

## 3. Politika ve mağaza sayfası
| Madde | Durum | Not |
|---|---|---|
| Gizlilik politikası URL'si | 👤 | Taslak `PRIVACY_POLICY.md`; GitHub Pages/Google Sites'ta yayımla, URL'yi Play Console'a gir |
| Veri güvenliği formu | ✅/👤 | Cevaplar `DATA_SAFETY.md` §2 ("veri toplanmıyor/paylaşılmıyor"); formu sen doldurursun |
| İçerik derecelendirmesi (IARC) anketi | 👤 | Hazır cevaplar §7 |
| Hedef kitle ve içerik | 👤 | §7: 13+ öner (çocuklara yönelik değil → Aileler politikası yükü yok) |
| Reklam beyanı | 👤 | "Hayır" (şimdilik) |
| Uygulama erişimi | 👤 | "Tüm işlevler özel erişim olmadan kullanılabilir" |
| Haber uygulaması / COVID / hükümet / finans beyanları | 👤 | Hepsi "Hayır" |
| Mağaza sayfası metin + görseller | ✅/👤 | Taslak `STORE_LISTING.md`; görselleri üret (ikon 512, öne çıkan 1024×500, ≥4 ekran görüntüsü) |
| Kapalı test kanalı + 12 test kullanıcısı | 👤 | Google Grubu ya da e-posta listesi; katılım bağlantısını paylaş, 14 gün boyunca hepsi kayıtlı kalsın |
| Geliştirici hesabı doğrulama (kimlik, telefon, adres) | 👤 | Play Console'un istediği doğrulamalar |

## 4. İmzalama — yükleme anahtarı (upload key) oluşturma 👤
1. JDK `keytool` (Unity'nin OpenJDK'sı: `D:/Unity/6000.3.9f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe`):
   ```
   keytool -genkeypair -v -keystore D:/Keys/meb-upload.keystore -alias upload -keyalg RSA -keysize 4096 -validity 10000
   ```
   Şifreyi güçlü seç; ad/kuruluş bilgilerini gir.
2. Dosyayı **proje klasörünün dışında** sakla (örn. `D:/Keys/`), şifreyle birlikte iki ayrı yerde yedekle (parola yöneticisi + harici disk).
   Kaybedersen Play Console'dan yükleme anahtarı sıfırlama istenebilir (Play App Signing açıkken), ama günler sürer.
3. **Asla git'e ekleme.** `.gitignore` artık `*.keystore`, `*.jks`, `keystore.properties`, `*.p12`, `*.pem` dosyalarını dışlar.
4. Derleme (PowerShell):
   ```
   $env:MEB_KEYSTORE_PATH="D:/Keys/meb-upload.keystore"; $env:MEB_KEYSTORE_PASS="…"; $env:MEB_KEY_ALIAS="upload"; $env:MEB_KEY_PASS="…"
   $env:MEB_VERSION_NAME="0.1.0"; $env:MEB_VERSION_CODE="1"
   & "D:/Unity/6000.3.9f1/Editor/Unity.exe" -batchmode -quit -projectPath "D:/UnityProjects/MiniEmpireBuilder" -executeMethod AndroidReleaseBuild.BuildFromCommandLine -logFile Builds/release.log
   python Tools/check_aab.py Builds/Release/MiniEmpireBuilder-0.1.0-1.aab
   ```
   Şifreler yalnız ortam değişkeninde; betik bitince proje ayarlarından silinir.
5. Play Console → Kapalı test → Yeni sürüm → AAB'yi yükle → Play App Signing'i kabul et.

## 5. Yayın AAB raporu (14 Kasım 2026, 02:14)
- Dosya: `Builds/Release/MiniEmpireBuilder-0.1.0-1.aab` — **92,6 MB** (sıkıştırılmış; açık hali 340 MB). Sürüm adı 0.1.0, kod 1.
- Derleme: Unity 6000.3.9f1, IL2CPP, **yalnız arm64-v8a**, hedef API **36**, en düşük API **25**, `debuggable` yok, Development kapalı.
- ⚠️ **İmza:** anahtar deposu verilmediği için Unity hata ayıklama anahtarıyla imzalandı → **Play Console'a bu dosya yüklenemez**; §4'teki
  upload key ile yeniden derlenmeli (aynı komut, ortam değişkenleri dolu).
- **16 KB denetimi** (`python Tools/check_aab.py …`): 7/7 `.so` dosyasının bütün ELF LOAD segmentleri `p_align = 16384` → UYGUN.
  `libil2cpp.so` 69,8 MB, `libunity.so` 22,2 MB, `libc++_shared.so`, `lib_burst_generated.so`, `libgame.so`, `libmain.so`, `libswappywrapper.so`.
  Üçüncü taraf eklenti `.so`'su yok (reklam/analitik SDK'sı yok).
- **Test kolaylığı izi:** `global-metadata.dat`'ta DevMenu, PlaytestBot, PlaytestJourney, Battle/BaseDebugOverlay, DevAdvanceTime,
  DevSetPlayerData **yok** → UYGUN. (`DevSettings` sınıfı bilinçli olarak var; `Available=false` sabitiyle bütün kolaylık anahtarları
  `false` döner.)
- **İzinler:** INTERNET (Unity varsayılanı; ağ kullanılmıyor), POST_NOTIFICATIONS (yerel bildirim), VIBRATE (taşıma onayı titreşimi).
- Boyutu küçültme fırsatları (isteğe bağlı): en büyük üç kaynak paketi 5,9 / 3,3 / 2,9 MB; IL2CPP "kod çıkarma (managed stripping)"
  düzeyi Yüksek'e alınabilir (önce testle) — Play mağazasında indirme boyutu cihaz için bölünmüş APK'larla daha küçük görünür.
- `ProjectSettings` değişiklikleri: `AndroidTargetSdkVersion 0 → 36`, `bundleVersion 1.0 → 0.1.0`, Log/Warning yığın izi kapalı
  (test APK betiği Development derlemede geri açar).

## 6. Reklam ve veri (UMP)
- Kullanılan reklam/analitik SDK'sı: **yok** (manifest: yalnız Unity paketleri; `ExternalDependencyManager` klasörü Firebase'den kalma, bağımlılık yok).
- Reklam eklendiğinde: Google Mobile Ads Unity eklentisi (güncel sürüm, 16 KB uyumlu) + **UMP SDK** izin formu (AEA/UK/İsviçre zorunlu;
  KVKK için de aynı form). Akış: açılışta `ConsentInformation.Update` → gerekirse form → izin gelince reklam başlat; Ayarlar'da
  "Gizlilik seçenekleri" düğmesi. Yapılmadı: SDK eklemek paket değişikliği gerektirir ve AdMob uygulama kimliği (👤) olmadan derlenemez.

## 7. Yaş derecelendirmesi (IARC) ve hedef kitle — hazır cevaplar 👤
- Kategori: **Oyun**.
- Şiddet: **Evet, fantastik/çizgi film tarzı şiddet** (kılıç/ok ile canavarlar, kan yok, gerçekçi insan şiddeti yok). Kan: Hayır.
- Cinsellik/çıplaklık: Hayır. Küfür/kaba dil: Hayır. Uyuşturucu/alkol/tütün: Hayır. Kumar/şans oyunu: Hayır (kart üretimi rastgele değil;
  gerçek parayla şans kutusu yok). Korku: Hafif (cadı, ejder) — "korkutucu öğe: Hayır/hafif".
- Kullanıcılar arası etkileşim/sohbet: Hayır. Konum paylaşımı: Hayır. Dijital satın alma: Hayır (şimdilik). Kullanıcı içeriği paylaşımı:
  Hayır (ada adı yalnız cihazda).
- Beklenen sonuç: PEGI 7 / ESRB Everyone 10+ / USK 6 civarı.
- Hedef kitle: **13–15, 16–17, 18+** seç (13 altını seçme → Aileler politikası, öğretmen onaylı reklam vb. gerekmez). "Çocuklara hitap
  edebilir mi?" sorusunda piksel sanat nedeniyle Google ek açıklama isteyebilir: "Oyun stratejik zorluğu ve sistemleri yetişkin/genç
  oyunculara yöneliktir" yaz.

## 8. BENİM yapmam gerekenler (özet)
1. Paket adını onayla; şirket/ürün adını söyle.
2. Upload keystore oluştur ve sakla (§4); Play App Signing'i kabul et.
3. Gizlilik politikasını bir URL'de yayımla.
4. Play Console'da: Veri güvenliği, İçerik derecelendirmesi, Hedef kitle, Reklam beyanı, Uygulama erişimi formları.
5. Mağaza görselleri (ikon 512, öne çıkan 1024×500, ≥4 ekran görüntüsü yayın APK'sıyla — "Development Build" yazısı olmadan).
6. 12+ test kullanıcısı bul, kapalı test kanalına ekle, 14 gün kesintisiz tut; sonra üretim erişimi anketini doldur.
7. Reklam/satın alma istenirse: AdMob hesabı + uygulama/ödül birimi kimlikleri, Play Console ürün kimlikleri.
