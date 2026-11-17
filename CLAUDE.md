# Mini Empire Builder — CLAUDE.md

Detaylı mimari, kararlar ve tuzaklar: **`Docs/AI_README.md`** (önce onu oku). Gece çalışması durumu: `PROGRESS.md`.

## Özet
- Tür: mobil (Android dikey) base-builder + dalga savunması. Unity 6000.3.9f1, URP 2D.
- Çekirdek döngü: Ana Üs'te maden/altın → bina yükselt (15 seviye, gerçek zamanlı inşaatçı) → Sefer haritası
  (6 bölge × 5 aşama + Sonsuz/İstila) → Savaş (kahraman + kuleler, dalgalara karşı Ana Üs'ü koru) → ödül → Ana Üs.
- Hedef zorluk: **ORTA** (ilk bölgeler affedici, 4–6. bölgeler planlama ister; yenilgi her zaman açıklanabilir olmalı).
- Sanat: Tiny Swords (64 px/birim; Warrior ölçeği 0.64), Cainos Village Props, özel üretimler (`Tools/campaign_art`).
  Yeni sprite yoksa: kodla tween/renk/prosedürel animasyon; ihtiyaçlar `ART_NEEDS.md`.
- Klasörler: `Assets/Scripts/{Base,Battle,Campaign,Data,Managers,Polish,Systems,UI}`, testler `Assets/Tests/PlayMode`
  (asmdef `BasePresentationTests`, yansıma ile Assembly-CSharp'a erişir), editör araçları `Assets/Editor`.

## Kurallar
- Kullanıcıyla Türkçe. Yeni metin: `L.T("Türkçe", "English")`; asset metni `Systems/AssetText.cs`.
- Fiyat/denge tek yerden: `Systems/GemEconomy.cs`, `Progression` (`Data/GameData.cs`).
- `Dragon Warrior Files/` ve `Resources/Battle/ground_mine_rocks.png.meta`'ya dokunma.
- Asset/sahne silme yok (gerekirse `_Deprecated/`). Paket/Project Settings değiştirme.

## Komutlar
- Unity: `D:/Unity/6000.3.9f1/Editor/Unity.exe` (Hub: `D:/Unity/Unity Hub`).
- Derleme: `dotnet build Assembly-CSharp.csproj`, `BasePresentationTests.csproj`, `Assembly-CSharp-Editor.csproj`
  (yeni .cs dosyası csproj'a Unity yenileyene kadar elle `<Compile Include>` ile eklenir).
- PlayMode testleri (editör **kapalıyken**):
  `"D:/Unity/6000.3.9f1/Editor/Unity.exe" -batchmode -projectPath "D:/UnityProjects/MiniEmpireBuilder" -runTests -testPlatform PlayMode -testResults PlaytestOutput/results.xml -logFile PlaytestOutput/unity.log`
  Tek test grubu: `-testFilter PlaytestBotTests`. Log'u tümüyle okuma: `grep -E "error|Exception|Failed"`.
- **Dikkat:** tam test koşusu `UIReviewScreenshots/CurrentReview/*.png` üzerine yazar; commit'ten önce
  `git checkout -- UIReviewScreenshots` ile geri al (yeni görüntüleri istersen ayrı commit'le).
- Bot/telemetri çıktısı: `PlaytestOutput/` (git dışı); seçilmiş görüntüler `Docs/playtest/`.

## Hata düzeltme oturumu kuralları (Docs/Bugs/PROMPTLAR.md BAŞLIK)
- Çalışma dalı `gece-gelistirme`. Onay bekleme; kararları `DECISIONS.md`'ye yaz.
- Her işten sonra: derle → test → (gerekirse) ekran görüntüsü → commit → `GECE_DURUM.md` güncelle.
- 2 denemede çözülmeyen işi geri al, `ENGELLENDİ` yaz, sonrakine geç.
- Mevcut oyuncu kayıtları bozulmasın. iOS, Android ve tablette çalışmalı; tek cihaza özel yama yok.
- Hata ayrıntıları `Docs/Bugs/BUGLAR.md`, görseller `Docs/Bugs/bug-NN.png`. Yeni ana üs UI'sı (`Docs/UI/UI_DEGISIKLIK.md`) bozulmaz.
