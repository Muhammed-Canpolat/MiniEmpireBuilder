# MiniEmpire UI: Üst bar ve alt menü asset paketi (Unity 2D)

## Kurulum (yaklaşık 2 dakika)
1. `MiniEmpireUI` klasörünü Unity'de `Assets/` içine sürükle. Klasörün adı aynı kalmalı.
2. TextMeshPro kurulu değilse şunu çalıştır: **Window > TextMeshPro > Import TMP Essential Resources**.
3. **Tools > MiniEmpire UI > Build Top Bar** ve ardından **Build Bottom Bar**.
   Bu iki komut sahnede Canvas'ı, SafeArea'yı, üst barı ve alt menüyü bağlantıları kurulmuş şekilde oluşturur.
   Canvas Scaler ayarları: Reference **886 x 1774**, Match = **0 (Width)**. Tasarım bu ölçüye göre yapıldı.
4. Sprite'lar düz görünüyor ya da kenarlar esniyorsa şunu çalıştır: **Tools > MiniEmpire UI > Reapply Sprite Import Settings**.

> Import ayarları otomatik yapılır: Sprite, sıkıştırma yok, mipmap yok, Full Rect.
> 9-slice kenar değerleri de otomatik girilir. Bunları Sprite Editor'da değiştirirsen senin değerlerin korunur.

## Klasör yapısı
```
Sprites/TopBar     topbar_bg* · res_pill_bg* · res_capsule_bg* · res_socket_empty · level_shield_empty
                   banner_left/right · btn_round_empty · btn_settings · ornament_diamond · rivet
Sprites/BottomBar  bottombar_bg* · menu_button_frame_noplate* · menu_button_frame* · menu_label_plate*
                   separator_banner* · arrow_tab_bg · btn_arrow_down · btn_arrow_up
Sprites/Icons      icon_gold · icon_wood · icon_meat · icon_gem · icon_builder · icon_gear
                   icon_build · icon_expedition · icon_hero · icon_island · icon_chevron_down · icon_chevron_up
Scripts/Runtime    NumberFormatter · ResourceCounter · TopBarController · LevelBadge
                   MenuItemData · MenuButton · BottomMenuBar · SafeArea
Scripts/Editor     MiniEmpireUIImporter (import + 9-slice) · MiniEmpireUIBuilder (sahneyi kurar)
Preview/           layout_preview.png · sprite_sheet_overview.png
```
`*` işaretli sprite'lar **9-slice**'tır (Image Type = Sliced). Genişlik değişse de köşeleri bozulmaz.

## 1. Değişken uzunluktaki sayılar (1 haneden 5+ haneye)
- Hiçbir sayı görselin içine gömülü değil, hepsi TextMeshPro metni.
- `res_pill_bg` kapsülü bir HorizontalLayoutGroup ile metne göre uzar ya da kısalır. Sol kenar (63 px) yuvarlak ikon yuvasını korur.
- Sol tarafta 3 kapsüle yaklaşık **330 px** yer var. Bu yüzden `NumberStyle.Auto` kullanılıyor:
  - `maxChars` (varsayılan 5) içine sığan sayı tam yazılır: `8`, `227`, `2.581`, `12.345`
  - Sığmayan sayı kısaltılır: `123.456 → 123B`, `1.250.000 → 1,2M`, `3.400.000.000 → 3,4Mr`. İngilizcede `123K / 1.2M / 3.4B` olur.
  - Kısaltma her zaman aşağı yuvarlar. Yani oyuncuya elindekinden fazlası gösterilmez.
- Seviye kalkanı 1, 2 ve 3 haneli sayılarda font boyutunu kendisi küçültür (`fontSizeByDigits`).

```csharp
topBar.Set("gold", 2581);              // anlık değişim, sayarak yükselir
topBar.Set("wood", 227, instant: true);
topBar.SetCurrentMax("builder", 2, 2); // "2/2"
topBar.SetLevel(7);
topBar.Get("gem").SetText("∞");        // istediğin özel metin
topBar.SettingsClicked += OpenSettings;
```
**Yeni kaynak eklemek için:** Bir `Res_xxx` objesini kopyala. İkonunu değiştir ve `TopBarController.counters` listesine bir id ile ekle.

## 2. Alt menü: her parça ayrı, yeni menü eklemek kolay
Her buton 4 ayrı katmandan oluşur ve her biri tek başına değiştirilebilir:
`Frame (menu_button_frame_noplate)` + `Icon` + `LabelPlate (menu_label_plate)` + `Label (TMP metin)`

- **İkonu değiştirmek:** `MenuItems/MenuItem_build.asset` dosyasındaki `icon` alanına yeni sprite'ı sürükle. Sahnede başka bir şeye dokunmana gerek yok.
- **Yazıyı değiştirmek:** Aynı asset'teki `labelTR` ve `labelEN` alanları. Türkçe ve İngilizce desteklenir, `SetLanguage(false)` İngilizceye geçirir.
- **Yeni menü eklemek:** Project penceresinde sağ tık > **Create > MiniEmpire > Menu Item**. Bir id, ikon ve yazı ver, sonra `BottomMenuBar.items` listesine sürükle.
  Ayırıcılar kendiliğinden eklenir. Buton fazla olursa butonlar daralır (min 110 px). Uzun yazılar otomatik küçülür.
- `order` sırayı belirler. `visible` butonu gizler. `locked` butonu gri ve tıklanamaz yapar, örneğin "5. seviyede açılır" durumu için.

```csharp
bottomMenu.ItemClicked += id => {
    switch (id) {
        case "build":      OpenBuild();  break;
        case "expedition": OpenSefer();  break;
        case "hero":       OpenHero();   break;
        case "island":     OpenIsland(); break;
    }
};
bottomMenu.SetBadge("hero", 3);        // badge objesi bağlıysa bildirim sayısını gösterir
bottomMenu.SetLocked("island", true);
```

## 3. Aşağı/yukarı ok ile menüyü açıp kapatma
- `btn_arrow_down`: menü açıkken görünür, basınca menü kapanır.
- `btn_arrow_up`: menü kapalıyken görünür, basınca menü açılır.
- Ok işaretleri ayrıca tek başına da var: `arrow_tab_bg` (boş kulakçık) + `icon_chevron_down` / `icon_chevron_up`. İstersen kendi butonunda kullanabilirsin.
- `BottomMenuBar.Toggle()` ya da `SetCollapsed(true/false, instant)` ile kodla da kontrol edilir. Kayma animasyonu hazır, `collapseDistance` ve `animTime` ayarlanabilir. Durum değişince `CollapsedChanged` event'i tetiklenir.

## Ek önlemler (ileride sorun çıkmasın diye)
- **Çentik ve kenarlar:** Barlar `SafeArea` altında, böylece iPhone çentiğine ve Android gesture çubuğuna girmez.
- **Font:** Görsellerdeki kalın ve yuvarlak yazıya en yakın ücretsiz fontlar *Lilita One* ve *Fredoka Bold* (Google Fonts). Font asset'ini **Dynamic** olarak oluştur. Ya da şu karakterleri mutlaka ekle: `İ ı Ş ş Ğ ğ Ü ü Ö ö Ç ç`. Eklenmezse "İNŞA ET" kutucuk olarak görünür.
  Kahverengi dış çizgi (outline) için `MiniEmpire_TextOutline_*.mat` materyali otomatik oluşturulur. Yeni fontta Builder'ı bir kez daha çalıştır ya da materyali elle kopyala.
- **Kendi ikonunu çizersen:** Yaklaşık 46x46 px boyut (kapsül ikonları) ve 100x90 px boyut (menü ikonları) uygun. Arka planı şeffaf PNG yapıp `Sprites/Icons` içine koy, ayarlar otomatik yapılır.
- **Daha büyük ekranlar:** Kaynak görsel 886 px genişliğindeydi. Sprite'lar bu ölçüde 1:1 net görünür. 1440p gibi yüksek çözünürlüklü telefonlarda hafif yumuşaklık olabilir. İleride ilk değiştirilecek sprite'lar `menu_button_frame` ve kalkan olmalı.
