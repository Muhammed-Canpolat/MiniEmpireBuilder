# Ana Üs ve savaş ekranı görsel düzeltmeleri

22 Ekim 2026 · `ui/dark-fantasy-hud`

## Görseller

- [Ana Üs, normal görünüm](ux-base-clean.png)
- [Ana Üs, Sev. 5 görünümü](ux-base-level5-clean.png)
- [Ayarlar paneli açık](ux-settings-open.png)
- [Savaş görünümü](ux-battle-clean.png)

PNG'ler başsız Unity PlayMode'da 390×844 RenderTexture bileşimidir. Gerçek cihaz/Game view yakalaması için Editor arayüzü gerekir; bu oturumda yerel arayüz köprüsü açılamadı.

## Değişiklikler

| İstek | Uygulama | Dosyalar |
|---|---|---|
| Alt paragraf ve yeşil kare çizgiler | Bunların F1 tanı katmanından geldiği doğrulandı. Katman oyun açılışında kapalı, F1 ile açılabilir; yalnız Editor/Development derlemesinde bulunur. | `Assets/Scripts/Base/BaseDebugOverlay.cs` |
| Ağaç ve kayaların dağılımı | İlk kaynaklar bina footprint'lerinden ve diğer çarpışmalardan kaçarak dört arazi alanına dağıtılıyor. Tam ağaç dekor sprite'ı kullanılıyor. Eksilen ağaç/kaya, rastgele 10–18 saniyelik denetimle uygun rastgele noktada yeniden doğuyor. Kaynakların kaldırma işlevi duruyor. Sev. 5'teki düzenli üst/alt doğal bant kaldırıldı. | `Assets/Scripts/Base/BaseSceneSetup.cs`, `Assets/Scripts/Base/BaseMapManager.cs` |
| Üst sağ simge | Müzik işareti yerine fonta bağlı olmayan dişli sprite'ı kullanılıyor. Açılan **Ayarlar** panelinde müzik ve efekt anahtarları var. | `Assets/Scripts/Polish/Core/AudioSettingsUI.cs` |
| Ana Üs, kahraman ve Çırak boyutu | Savaşta yalnız çizilen sprite kopyaları büyütüldü. Ana aktör transformları, çarpışma alanları, saldırı menzilleri ve savaş kararları değişmedi. Çırak sprite'ının saydam boşluğu hesaba katılarak görünen figür büyütüldü. | `Assets/Scripts/Base/BattleCosmeticObserver.cs`, `Assets/Scripts/Base/BattleApprentice.cs` |
| Rastgele savaş dekoru | `Prop_` görselleri kozmetik katmanda kapatıldı; düşman nesnelerine ve davranışına müdahale edilmedi. | `Assets/Scripts/Base/BattleCosmeticObserver.cs` |

`Assets/Tests/PlayMode/ViewportRevisionTests.cs` güncellendi. Testler normal görünümde F1'in kapalı olmasını, Ayarlar panelini, kaynakların dört alanda görünmesini ve yeniden doğmasını, savaş aktörlerinin görsel kopyalarını ve savaş dekorlarının kapatılmasını kontrol ediyor.

## Doğrulama

- Tam PlayMode paketi: **16/16 geçti** — `ux-complete-results.xml`.
- Ayarlar paneli görsel yakalama sonrası ilgili testler: **2/2 geçti** — `ux-settings-results.xml`.
- Bölge 1 düşmanları/boss'u, sprite ve animasyonları, RegionData/LevelData içeriği, düşman davranışı ve savaş mantığı bu çalışmada değiştirilmedi. Kirli çalışma ağacı sıfırlanmadı.
