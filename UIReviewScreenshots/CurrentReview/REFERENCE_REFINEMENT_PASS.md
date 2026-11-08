# Ana Üs ekran görüntüsü sonrası görsel düzeltme

Kullanıcının seviye 6 Ana Üs görüntüsündeki yoğun çit kenarı, sık dış dekor ve küçük maden görünümü incelendi. Kayıtlı bina konumları değiştirilmedi.

## Yeni 390×844 görüntüler

- [Ana Üs genel görünüm](reference-refined-base.png)
- [Maden yakın planı](reference-refined-mine.png)

## Düzenlemeler

- Maden ana görseli 3,9×3,45 yerine 5,0×4,35 dünya birimi olarak gösteriliyor; 4×4 yerleşim footprint'i aynı. Arabadaki altın doluluk görseli yeni ölçeğe hizalandı.
- Madenin altındaki koyu zemin yaması inceltildi. Diğer bina yamalarının opaklığı da azaltıldı; Yama tilemap'i ve yol hesaplaması çalışmaya devam ediyor.
- Çit taş destekleri altı hücre aralığına alındı. Lambalar her kenarda seçili noktalara indirildi ve görsel boyutları küçültüldü. Çit parçası kimlikleri, kaldırma kaydı ve çarpışma düzeni aynı.
- Çit dışı ağaç, çalı ve kaya sayısı azaltıldı, konumları aralık içine rastgele dağıtıldı.

## Doğrulama

- `ViewportRevisionTests.BaseViewportAndRecommendedLayout`: 1 geçti, 0 başarısız (`reference-refinement-results.xml`).
- Tüm Unity PlayMode paketi: 16 geçti, 0 başarısız (`reference-refined-all-results.xml`).
- Görüntüler Unity kamera/Canvas çıktısından RenderTexture ile alındı. Test yeni seviye 5 haritası kullanır; kullanıcının kayıtlı seviye 6 bina yerleşimi değiştirilmedi.
- Bölge 1 düşmanları, boss, sprite/animasyonları, `RegionData`/`LevelData` ve savaş davranışı değiştirilmedi. Çalışma ağacı sıfırlanmadı.
