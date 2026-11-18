# Mini Empire Builder – Intro Sahnesi (Unity)

## Klasör yapısı
Assets/MiniEmpire/Sprites/Intro/
  intro_background.png        941x1672  (bar + ipucu yazısı silinmiş arka plan, logo dahil)
  logo_mini_empire_builder.png            (logo kırpması – dikdörtgen)
  loading_bar_frame.png       605x64    (boş çubuk: çerçeve + koyu zemin)
  loading_bar_fill.png        584x42    (sarı dolgu)
  icon_tip_bulb.png                       (ampul ikonu)
Assets/MiniEmpire/Scripts/Intro/
  LoadingBar.cs, IntroSceneController.cs, TipRotator.cs, UIIdleAnimation.cs

## Sprite import ayarları
Texture Type: Sprite (2D and UI) | Compression: None/High Quality | Generate Mip Maps: kapalı

## Canvas
Canvas: Screen Space - Overlay | Canvas Scaler: Scale With Screen Size, Reference Resolution 941 x 1672, Match = 0.5
Canvas'a CanvasGroup ekle (IntroSceneController -> Root Group).

## Hiyerarşi
IntroCanvas (CanvasGroup)
 ├─ Background      (Image: intro_background, anchor: stretch/stretch, Preserve Aspect KAPALI; farklı ekran oranları için "Aspect Ratio Fitter: Envelope Parent")
 ├─ Logo            (Image: logo_mini_empire_builder, anchor: top-center)  + UIIdleAnimation
 ├─ LoadingBar      (anchor: bottom-center, + LoadingBar.cs)
 │   ├─ Frame       (Image: loading_bar_frame, 605x64)
 │   ├─ Fill        (Image: loading_bar_fill, 584x42, pozisyon: sol=9px, üst=10px; Pivot x=0)
 │   └─ PercentText (TMP: Bold, beyaz, siyah Outline 0.25, ortalı)
 └─ TipArea         (anchor: bottom-center, + TipRotator.cs)
     ├─ Icon        (Image: icon_tip_bulb)
     └─ TipText     (TMP: Bold, beyaz, siyah Outline; Türkçe karakter için Font Asset Atlas'a ğ ü ş ı İ ö ç ekle)

Boş bir GameObject'e IntroSceneController.cs ekle, referansları bağla,
"Game Scene Name" = yükleyeceğin ana sahne. İntro sahnesi Build Settings'te index 0 olmalı.

## Çalışma mantığı
Fade-in -> LoadSceneAsync (allowSceneActivation=false) -> çubuk %0'dan %100'e dolar
-> %100'de kısa bekleme -> fade-out -> oyun sahnesi açılır.
Minimum gösterim süresi (varsayılan 2.5 sn) sayesinde yükleme çok hızlı olsa bile intro görünür.
