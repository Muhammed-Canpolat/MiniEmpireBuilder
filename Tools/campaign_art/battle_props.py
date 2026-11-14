"""
7. oturum (D91): bölge savaş haritası süsleri — projede karşılığı olmayan parçalar Tiny Swords görsellerinden türetilir.

  ruin_castle.png   Kırmızı Kale -> gri taş, çatısı ve üst duvarları kırık (testere kenar), önünde moloz      (Bölge 1)
  ruin_tower.png    Kırmızı Kule -> gri taş, üst yarısı yıkık                                                 (Bölge 1)
  ruin_wall.png     kale duvarından kesilmiş alçak kırık duvar parçası                                       (Bölge 1)
  mine_entrance.png kaya yığını (Tiny Swords kayaları 2x) + karanlık ağız + ahşap çerçeve                    (Bölge 3)
  bones_1..3.png    kemik yığını / kafatası (piksel çizim, Tiny Swords konturu)                               (Bölge 5)
  lava_pool.png     koyu kenarlı lav gölü (turuncu, sarı çekirdek) — ışıma ve kabarcıklar oyunda kodla        (Bölge 6)
  ice_rock.png      buzlu kaya (Rock4 -> buz mavisi + kar kapağı)                                             (Bölge 4)

Hepsi 64 PPU, alt-orta pivot (meta: Rock1.png.meta kopyası, pivot alt-orta). Gerçek çizimler gelirse (ART_NEEDS #9) aynı adla değiştirilir.
Çalıştırma: python Tools/campaign_art/battle_props.py
"""
import os
import re
import uuid

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TS = os.path.join(ROOT, "Assets", "Tiny Swords")
OUT = os.path.join(ROOT, "Assets", "Resources", "BattleMaps", "Props")
OUTLINE = (37, 33, 43, 255)


def load(rel):
    return Image.open(os.path.join(TS, rel)).convert("RGBA")


def trim(img):
    box = img.getbbox()
    return img.crop(box) if box else img


def stone(img, warm=0.0):
    """Renkli yapıyı gri taşa çevirir (kontur korunur)."""
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            l = int(0.3 * r + 0.55 * g + 0.15 * b)
            px[x, y] = (min(255, int(l * 0.95 + 18 + warm * 10)), min(255, int(l * 0.95 + 16)), min(255, int(l + 22)), a)
    return img


def jagged_cut(img, keep_from, seed, amp=10, step=8):
    """Görselin üst kısmını testere kenarla siler: y < keep_from(+gürültü) şeffaf olur; kesik kenara koyu kontur."""
    import random
    rnd = random.Random(seed)
    px = img.load()
    edge = []
    x = 0
    while x < img.width:
        edge.extend([keep_from + rnd.randint(-amp, amp)] * step)
        x += step
    for x in range(img.width):
        cut = edge[x]
        for y in range(0, max(0, cut)):
            px[x, y] = (0, 0, 0, 0)
        for y in range(cut, min(img.height, cut + 3)):
            if px[x, y][3] > 0:
                px[x, y] = OUTLINE
    return img


def rubble(img, rnd, count, y0, y1):
    d = ImageDraw.Draw(img)
    for _ in range(count):
        cx, cy = rnd.uniform(16, img.width - 16), rnd.uniform(y0, y1)
        w, h = rnd.uniform(8, 18), rnd.uniform(6, 12)
        d.rectangle((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=(150, 150, 166, 255), outline=OUTLINE, width=2)
        d.line((cx - w / 2 + 2, cy - h / 2 + 2, cx + w / 2 - 3, cy - h / 2 + 2), fill=(196, 196, 210, 255), width=1)


def ruin_castle():
    import random
    rnd = random.Random(11)
    src = trim(load("Buildings/Red Buildings/Castle.png"))
    img = Image.new("RGBA", (src.width, src.height + 24), (0, 0, 0, 0))
    img.alpha_composite(stone(src.copy()), (0, 0))
    img = jagged_cut(img, int(src.height * 0.42), 3, amp=14, step=10)
    rubble(img, rnd, 14, src.height - 6, src.height + 18)
    return trim(img)


def ruin_tower():
    import random
    rnd = random.Random(12)
    src = trim(load("Buildings/Red Buildings/Tower.png"))
    img = Image.new("RGBA", (src.width + 30, src.height + 16), (0, 0, 0, 0))
    img.alpha_composite(stone(src.copy()), (15, 0))
    img = jagged_cut(img, int(src.height * 0.5), 7, amp=12, step=8)
    rubble(img, rnd, 8, src.height - 4, src.height + 12)
    return trim(img)


def ruin_wall():
    import random
    rnd = random.Random(13)
    src = trim(load("Buildings/Red Buildings/Castle.png"))
    band = stone(src.crop((0, int(src.height * 0.55), int(src.width * 0.45), src.height)).copy())
    img = Image.new("RGBA", (band.width, band.height + 14), (0, 0, 0, 0))
    img.alpha_composite(band, (0, 0))
    img = jagged_cut(img, 18, 5, amp=12, step=9)
    rubble(img, rnd, 6, band.height - 6, band.height + 10)
    return trim(img)


def ice_rock():
    rock = trim(load("Terrain/Decorations/Rocks/Rock4.png"))
    rock = rock.resize((rock.width * 2, rock.height * 2), Image.NEAREST)
    px = rock.load()
    for y in range(rock.height):
        for x in range(rock.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            l = int(0.3 * r + 0.55 * g + 0.15 * b)
            px[x, y] = (min(255, l + 40), min(255, l + 70), min(255, l + 110), a) if l > 40 else (r, g, b, a)
    d = ImageDraw.Draw(rock)
    # Kar kapağı
    d.ellipse((rock.width * 0.18, 2, rock.width * 0.82, rock.height * 0.36), fill=(250, 252, 255, 255))
    d.arc((rock.width * 0.18, 2, rock.width * 0.82, rock.height * 0.36), 180, 360, fill=OUTLINE, width=2)
    return rock


def mine_entrance():
    w, h = 224, 168
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    rocks = [trim(load(f"Terrain/Decorations/Rocks/Rock{k}.png")) for k in (1, 2, 3, 4)]
    placements = [(3, 0.0, 40, 3.2), (3, 0.0, 40, 3.2), (2, -62, 70, 2.6), (1, 64, 72, 2.6), (4, -88, 108, 2.0), (4, 92, 110, 2.0), (2, 0, 30, 3.4)]
    for k, dx, top, scale in placements:
        r = rocks[k - 1].resize((int(rocks[k - 1].width * scale), int(rocks[k - 1].height * scale)), Image.NEAREST)
        img.alpha_composite(r, (int(w / 2 + dx - r.width / 2), int(top)))
    d = ImageDraw.Draw(img)
    # Karanlık ağız (kemer)
    d.rectangle((w / 2 - 30, 92, w / 2 + 30, h - 8), fill=(22, 16, 18, 255))
    d.ellipse((w / 2 - 30, 64, w / 2 + 30, 124), fill=(22, 16, 18, 255))
    # Ahşap çerçeve: iki dikme + lento
    for x0 in (w / 2 - 38, w / 2 + 26):
        d.rectangle((x0, 84, x0 + 12, h - 6), fill=(150, 98, 52, 255), outline=OUTLINE, width=2)
        d.line((x0 + 3, 90, x0 + 3, h - 12), fill=(196, 140, 82, 255), width=2)
    d.rectangle((w / 2 - 44, 74, w / 2 + 44, 88), fill=(150, 98, 52, 255), outline=OUTLINE, width=2)
    d.line((w / 2 - 40, 78, w / 2 + 40, 78), fill=(196, 140, 82, 255), width=2)
    # Raylar
    d.line((w / 2 - 14, h - 8, w / 2 - 20, h - 2), fill=(90, 90, 100, 255), width=3)
    d.line((w / 2 + 14, h - 8, w / 2 + 20, h - 2), fill=(90, 90, 100, 255), width=3)
    return trim(img)


def bones(kind):
    img = Image.new("RGBA", (72, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    bone_fill, shade = (232, 226, 206, 255), (186, 176, 150, 255)

    def bone(x0, y0, x1, y1):
        d.line((x0, y0, x1, y1), fill=OUTLINE, width=8)
        for x, y in ((x0, y0), (x1, y1)):
            d.ellipse((x - 6, y - 6, x + 6, y + 6), fill=OUTLINE)
        d.line((x0, y0, x1, y1), fill=bone_fill, width=4)
        for x, y in ((x0, y0), (x1, y1)):
            d.ellipse((x - 4, y - 4, x + 4, y + 4), fill=bone_fill)
            d.point((x + 1, y + 2), fill=shade)

    def skull(cx, cy):
        d.ellipse((cx - 12, cy - 13, cx + 12, cy + 9), fill=bone_fill, outline=OUTLINE, width=2)
        d.rectangle((cx - 7, cy + 4, cx + 7, cy + 12), fill=bone_fill, outline=OUTLINE, width=2)
        d.ellipse((cx - 8, cy - 5, cx - 2, cy + 1), fill=OUTLINE)
        d.ellipse((cx + 2, cy - 5, cx + 8, cy + 1), fill=OUTLINE)
        d.line((cx - 3, cy + 7, cx - 3, cy + 11), fill=OUTLINE, width=1)
        d.line((cx + 3, cy + 7, cx + 3, cy + 11), fill=OUTLINE, width=1)

    if kind == 1:
        bone(12, 34, 44, 24); bone(26, 40, 58, 38); skull(50, 22)
    elif kind == 2:
        bone(14, 26, 50, 36); bone(18, 40, 40, 18)
    else:
        skull(36, 26); bone(10, 40, 30, 38)
    return trim(img)


def lava_pool():
    w, h = 160, 96
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((2, 2, w - 2, h - 2), fill=(58, 30, 24, 255), outline=OUTLINE, width=3)
    d.ellipse((14, 12, w - 14, h - 12), fill=(236, 92, 30, 255))
    d.ellipse((36, 24, w - 44, h - 30), fill=(255, 168, 52, 255))
    d.ellipse((54, 32, w - 70, h - 40), fill=(255, 222, 110, 255))
    # Kabuk parçaları
    for x, y in ((28, 40), (118, 58), (96, 26)):
        d.polygon([(x, y), (x + 10, y - 4), (x + 16, y + 3), (x + 6, y + 8)], fill=(72, 40, 30, 255), outline=OUTLINE)
    return img


def write_meta(name, pivot=(0.5, 0.0)):
    dst_meta = os.path.join(OUT, name + ".meta")
    if os.path.exists(dst_meta):
        return
    text = open(os.path.join(TS, "Terrain", "Decorations", "Rocks", "Rock1.png.meta"), encoding="utf-8").read()
    text = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, text, count=1)
    # Alt-orta pivot (alignment 7 = BottomCenter)
    text = text.replace("  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}", f"  alignment: 7\n  spritePivot: {{x: {pivot[0]}, y: {pivot[1]}}}", 1)
    open(dst_meta, "w", encoding="utf-8", newline="\n").write(text)


def main():
    os.makedirs(OUT, exist_ok=True)
    items = {
        "ruin_castle.png": ruin_castle(), "ruin_tower.png": ruin_tower(), "ruin_wall.png": ruin_wall(),
        "mine_entrance.png": mine_entrance(), "ice_rock.png": ice_rock(), "lava_pool.png": lava_pool(),
        "bones_1.png": bones(1), "bones_2.png": bones(2), "bones_3.png": bones(3),
    }
    for name, img in items.items():
        img.save(os.path.join(OUT, name), optimize=True)
        write_meta(name, (0.5, 0.5) if name == "lava_pool.png" else (0.5, 0.0))
        print("yazıldı", name, img.size)


if __name__ == "__main__":
    main()
