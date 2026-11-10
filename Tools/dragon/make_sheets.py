"""
Dragon Warrior karelerini (512x256, çoğu boş alan) ortak bir kutuya kırpıp küçültür ve
animasyon başına tek bir ızgara sayfası üretir: Assets/Resources/Dragon/dragon_<anim>.png

Sayfa düzeni: 6 sütun, satır satır soldan sağa; her hücre FRAME_W x FRAME_H piksel.
DragonBoss.cs aynı sabitleri kullanır (sütun sayısı ve hücre boyu sayfa boyundan hesaplanır).

Çalıştırma: python Tools/dragon/make_sheets.py   (Pillow gerekir)
"""
import glob
import os
import re

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Dragon Warrior Files", "Dragon Warrior PNG")
OUT = os.path.join(ROOT, "Assets", "Resources", "Dragon")
ANIMS = ["idle", "walk", "attack", "strike", "flyKick", "hurt", "die"]
COLUMNS = 6
SCALE = 0.75


def frames(anim):
    files = glob.glob(os.path.join(SRC, anim + "_*.png"))
    files = [f for f in files if re.match(re.escape(anim) + r"_\d+\.png$", os.path.basename(f))]
    return sorted(files, key=lambda f: int(re.findall(r"(\d+)\.png$", f)[0]))


def main():
    all_frames = {a: [Image.open(f).convert("RGBA") for f in frames(a)] for a in ANIMS}
    # Tüm karelerin ortak kutusu: ayak hizası ve konum animasyonlar arasında sabit kalsın.
    boxes = [im.getbbox() for ims in all_frames.values() for im in ims if im.getbbox()]
    left = min(b[0] for b in boxes) - 4
    top = min(b[1] for b in boxes) - 4
    right = max(b[2] for b in boxes) + 4
    bottom = max(b[3] for b in boxes) + 2
    w, h = int((right - left) * SCALE), int((bottom - top) * SCALE)
    os.makedirs(OUT, exist_ok=True)
    for anim, ims in all_frames.items():
        rows = (len(ims) + COLUMNS - 1) // COLUMNS
        sheet = Image.new("RGBA", (COLUMNS * w, rows * h), (0, 0, 0, 0))
        for i, im in enumerate(ims):
            cell = im.crop((left, top, right, bottom)).resize((w, h), Image.LANCZOS)
            sheet.alpha_composite(cell, ((i % COLUMNS) * w, (i // COLUMNS) * h))
        sheet.save(os.path.join(OUT, f"dragon_{anim}.png"), optimize=True)
        print(anim, len(ims), "kare", sheet.size, "hücre", (w, h))


if __name__ == "__main__":
    main()
