"""Onay (✓) simgesi — Tiny Swords Icon_09 (kırmızı ✗) ile aynı piksel stilinde: kalın koyu kontur (#161C2E), alt gölge tonu, ana renk ve
küçük ışık vurgusu; renkler Icon_07 (yeşil ok) paletinden. Paket aynı stilde bir ✓ içermiyor (ART_NEEDS'te).
Çıktı: Assets/Resources/CampaignArt/ui_icon_check.png (64×64, kenar yumuşatmasız). Çalıştır: python Tools/campaign_art/check_icon.py"""
from PIL import Image, ImageDraw

OUT = 'Assets/Resources/CampaignArt/ui_icon_check.png'
OUTLINE = (22, 28, 46, 255)
SHADE = (67, 108, 103, 255)
DARK = (76, 151, 104, 255)
MAIN = (119, 196, 86, 255)
LIGHT = (186, 219, 122, 255)
PATH = [(13, 33), (26, 46), (51, 17)]


def stroke(draw, path, width, color, dx=0, dy=0):
    pts = [(x + dx, y + dy) for x, y in path]
    draw.line(pts, fill=color, width=width, joint='curve')
    r = width / 2 - 0.5
    for x, y in (pts[0], pts[-1]):
        draw.ellipse((x - r, y - r, x + r, y + r), fill=color)


img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
stroke(d, PATH, 17, OUTLINE, 0, 3)     # alt gölge konturu (paketteki gibi aşağıda kalın)
stroke(d, PATH, 17, OUTLINE)
stroke(d, PATH, 11, SHADE, 0, 1)
stroke(d, PATH, 10, DARK)
stroke(d, PATH, 8, MAIN, 0, -1)
# ışık vurgusu: uzun kolun üst ucuna yakın kısa çizgi + kısa kolda bir piksel kümesi
d.line([(40, 26), (47, 18)], fill=LIGHT, width=3)
d.rectangle((15, 31, 17, 33), fill=LIGHT)
# Kenar yumuşatması yok: alfa ikili
px = img.load()
for y in range(64):
    for x in range(64):
        r, g, b, a = px[x, y]
        px[x, y] = (r, g, b, 255 if a >= 128 else 0)
img.save(OUT)
print('yazildi', OUT, img.getbbox())
