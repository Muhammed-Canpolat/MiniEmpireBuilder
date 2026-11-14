"""
7. oturum: savaş haritası yerleşimlerinin önizlemesi (Resources/BattleMaps .asset YAML'ından): kara maskesi, rotalar, kule yuvaları
(Topçu menzil çemberiyle), Ana Üs, maden, kahraman. Çıktı: Docs/playtest/session7/layouts_regionN.png (5 seviye yan yana).
Çalıştırma: python Tools/campaign_art/layout_preview.py
"""
import os
import re

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Resources", "BattleMaps")
OUT = os.path.join(ROOT, "Docs", "playtest", "session7")
S = 16  # px / birim
COLS, ROWS, TOP, LEFT = 16, 32, 26.0, -8.0


def vecs(block):
    return [(float(x), float(y)) for x, y in re.findall(r"\{x: ([-\d.e]+), y: ([-\d.e]+)\}", block)]


def parse_level(path):
    text = open(path, encoding="utf-8").read()
    routes = [vecs(m) for m in re.findall(r"- points:\n((?:\s+- \{x:.*\}\n)+)", text)]
    slots = vecs(re.search(r"slots:\n((?:\s+- \{x:.*\}\n)*)", text).group(1)) if "slots:" in text else []
    one = lambda key: vecs(re.search(key + r": (\{.*\})", text).group(1))[0]
    kind = int(re.search(r"kind: (\d)", text).group(1))
    return routes, slots, one("basePosition"), one("minePosition"), one("heroSpawn"), kind


def catmull(points, steps=16):
    pts = [points[0]] + points + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(steps):
            t = s / steps
            out.append(tuple(0.5 * (2 * p1[k] + (-p0[k] + p2[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t * t
                                    + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t ** 3) for k in range(2)))
    out.append(points[-1])
    return out


def px(p):
    return ((p[0] - LEFT) * S, (TOP + 4 - p[1]) * S)


def render(region):
    text = open(os.path.join(SRC, f"Region_{region}.asset"), encoding="utf-8").read()
    mask = re.search(r"mask:\n((?:\s+- .*\n)+)", text).group(1)
    mask = [line.strip()[2:].strip("'\"") for line in mask.strip().splitlines()]
    W, H = COLS * S, (ROWS + 4) * S
    sheet = Image.new("RGB", (W * 5 + 40, H + 30), (40, 40, 40))
    names = ["Hayatta Kal", "Üssü Koru", "Madeni Koru", "Zamana Karşı", "Boss"]
    for level in range(1, 6):
        routes, slots, base, mine, hero, kind = parse_level(os.path.join(SRC, "Levels", f"R{region}_L{level}.asset"))
        img = Image.new("RGB", (W, H), (71, 171, 169))
        d = ImageDraw.Draw(img)
        for r, line in enumerate(mask):
            for c, ch in enumerate(line):
                if ch == "#":
                    x, y = c * S, (r + 4) * S
                    d.rectangle((x, y, x + S - 1, y + S - 1), fill=(118, 170, 74))
        for route in routes:
            pts = [px(p) for p in catmull(route)]
            d.line(pts, fill=(120, 86, 52), width=int(1.45 * S))
            d.line(pts, fill=(206, 168, 112), width=int(1.15 * S))
        for i, s in enumerate(slots):
            cx, cy = px((s[0], s[1] + 1.0))
            d.ellipse((cx - 5 * S, cy - 5 * S, cx + 5 * S, cy + 5 * S), outline=(255, 214, 60))
            x, y = px(s)
            d.ellipse((x - S, y - S, x + S, y + S), fill=(255, 214, 60), outline=(60, 40, 10), width=2)
            d.text((x - 3, y - 6), str(i + 1), fill=(0, 0, 0))
        bx, by = px(base)
        d.rectangle((bx - 1.9 * S, by - 1.6 * S, bx + 1.9 * S, by + 1.0 * S), fill=(200, 60, 50))
        hx, hy = px(hero)
        d.ellipse((hx - 6, hy - 6, hx + 6, hy + 6), fill=(70, 120, 255))
        if kind == 2:
            mx, my = px(mine)
            d.rectangle((mx - S, my - S, mx + S, my + S), fill=(255, 190, 20), outline=(0, 0, 0))
        sheet.paste(img, ((level - 1) * (W + 10), 30))
        ImageDraw.Draw(sheet).text(((level - 1) * (W + 10) + 4, 8), f"B{region} S{level} {names[kind]}", fill=(255, 255, 255))
    os.makedirs(OUT, exist_ok=True)
    sheet.save(os.path.join(OUT, f"layouts_region{region}.png"))


if __name__ == "__main__":
    for r in range(1, 7):
        render(r)
    print("tamam")
