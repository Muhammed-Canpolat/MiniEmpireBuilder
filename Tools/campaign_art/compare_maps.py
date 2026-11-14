"""
7. oturum: Sefer Haritası adası ↔ savaş haritası karşılaştırması. Sol: map_region_N (sefer haritası adası, yatay), sağ: savaşın tam haritası
(Session7Tests.G1 *_fullmap.png, 1. seviye = harita yolu). Yön aynı: haritanın alt köprüsü = savaşta Ana Üs ucu, üst köprü = düşman girişi;
savaş haritası yalnız dikeyde uzatılmış (döndürme yok).
Çıktı: Docs/playtest/session7/compare_regionN.png
"""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "Resources", "CampaignArt")
SHOTS = os.path.join(ROOT, "PlaytestOutput", "session7")
OUT = os.path.join(ROOT, "Docs", "playtest", "session7")
NAMES = {1: "Yıkık Kale Yolu", 2: "Sisli Orman", 3: "Terk Edilmiş Maden", 4: "Donmuş Geçit", 5: "Karanlık Kale", 6: "Ejderin İni"}


def main():
    os.makedirs(OUT, exist_ok=True)
    for region in range(1, 7):
        shot_path = os.path.join(SHOTS, f"G1_r{region}_l1_fullmap.png")
        if not os.path.exists(shot_path):
            print("yok:", shot_path)
            continue
        battle = Image.open(shot_path).convert("RGB")
        # Kamera alanının dışında kalan siyah şeritleri kırp
        mask = battle.convert("L").point(lambda v: 255 if v > 12 else 0)
        box = mask.getbbox()
        if box:
            battle = battle.crop(box)
        island = Image.open(os.path.join(ART, f"map_region_{region}.png")).convert("RGB")
        H = battle.height
        top = island.resize((560, int(island.height * 560 / island.width)))
        W = top.width + battle.width + 30
        sheet = Image.new("RGB", (W, H + 40), (34, 30, 28))
        d = ImageDraw.Draw(sheet)
        try:
            font = ImageFont.truetype("arial.ttf", 15)
        except OSError:
            font = None
        d.text((10, 10), f"Bölge {region} · {NAMES[region]} — sol: Sefer Haritası adası, sağ: savaş haritası (1. seviye)", fill=(240, 220, 160), font=font)
        sheet.paste(top, (10, 40))
        d.text((10, 50 + top.height), "Harita: alt köprü (Ana Üs ucu) → kıvrımlar → üst köprü (giriş)", fill=(220, 220, 220), font=font)
        d.text((10, 66 + top.height), "Savaş: aynı yön, dikeyde uzatılmış; düşman üstten girer", fill=(220, 220, 220), font=font)
        sheet.paste(battle, (top.width + 20, 40))
        sheet.save(os.path.join(OUT, f"compare_region{region}.png"))
        print("yazıldı compare_region", region)


if __name__ == "__main__":
    main()
