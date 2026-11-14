"""Eylem menüsü simgeleri (5. oturum 2.1): TAŞI (dört yönlü ok) ve SİL (çöp kovası). check_icon.py ile aynı piksel stili:
kalın koyu kontur (#161C2E), alt gölge, ana renk + ışık vurgusu, kenar yumuşatması yok (alfa ikili).
Çıktı: Assets/Resources/CampaignArt/ui_icon_move.png ve ui_icon_trash.png (64x64). Çalıştır: python Tools/campaign_art/action_icons.py"""
from PIL import Image, ImageDraw

OUTLINE = (22, 28, 46, 255)


def finish(img, path):
    px = img.load()
    for y in range(64):
        for x in range(64):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255 if a >= 128 else 0)
    img.save(path)
    print('yazildi', path, img.getbbox())


def silhouette(shape_fn, color, shade, light, path):
    """shape_fn(draw, fill, dx, dy) şekli çizer; kontur için şişirilmiş (maske genişletme) kopya alınır."""
    mask = Image.new('L', (64, 64), 0)
    shape_fn(ImageDraw.Draw(mask), 255, 0, 0)
    from PIL import ImageFilter
    grown = mask.filter(ImageFilter.MaxFilter(7))
    img = Image.new('RGBA', (64, 64), (0, 0, 0, 0))
    out = Image.new('RGBA', (64, 64), OUTLINE)
    img.paste(out, (0, 3), grown)      # alt gölge konturu
    img.paste(out, (0, 0), grown)
    d = ImageDraw.Draw(img)
    # gövde: koyu ton, ana ton (1 px yukarı), ışık
    m2 = Image.new('L', (64, 64), 0); shape_fn(ImageDraw.Draw(m2), 255, 0, 0)
    img.paste(Image.new('RGBA', (64, 64), shade), (0, 1), m2)
    img.paste(Image.new('RGBA', (64, 64), color), (0, -1), m2)
    return img, d


def move():
    def shape(d, f, dx, dy):
        d.rectangle((28, 14, 35, 49), fill=f)
        d.rectangle((14, 28, 49, 35), fill=f)
        d.polygon([(32, 6), (22, 18), (42, 18)], fill=f)
        d.polygon([(32, 57), (22, 45), (42, 45)], fill=f)
        d.polygon([(6, 32), (18, 22), (18, 42)], fill=f)
        d.polygon([(57, 32), (45, 22), (45, 42)], fill=f)
    img, d = silhouette(shape, (119, 196, 86, 255), (67, 108, 103, 255), None, None)
    L = (186, 219, 122, 255)
    d.line([(30, 20), (30, 29)], fill=L, width=2)
    d.line([(20, 30), (29, 30)], fill=L, width=2)
    d.line([(31, 9), (25, 17)], fill=L, width=2)
    d.line([(9, 31), (16, 25)], fill=L, width=2)
    finish(img, 'Assets/Resources/CampaignArt/ui_icon_move.png')


def trash():
    def shape(d, f, dx, dy):
        d.rectangle((18, 22, 45, 55), fill=f)      # gövde
        d.rectangle((13, 15, 50, 21), fill=f)      # kapak
        d.rectangle((26, 9, 37, 15), fill=f)       # kulp
    img, d = silhouette(shape, (176, 184, 196, 255), (112, 122, 142, 255), None, None)
    DARK = (84, 94, 116, 255)
    LIGHT = (226, 232, 240, 255)
    for x in (26, 32, 38):                          # dikey oluklar
        d.rectangle((x, 28, x + 1, 49), fill=DARK)
    d.rectangle((13, 19, 50, 21), fill=(150, 158, 172, 255))
    d.line([(21, 26), (21, 40)], fill=LIGHT, width=2)
    d.line([(16, 17), (30, 17)], fill=LIGHT, width=1)
    finish(img, 'Assets/Resources/CampaignArt/ui_icon_trash.png')


move(); trash()
