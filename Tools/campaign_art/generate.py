"""
Sefer haritası görsellerini Tiny Swords parçalarından üretir.

Çıktılar (Assets/Resources/CampaignArt/):
  map_region_1..5.png, map_endless.png  -> Sefer haritası (bölge listesi), her biri 780x440
  road_region_1..5.png                  -> Bölge ekranı (5 aşamalı yol), her biri 780x1080
  ui_*.png                              -> Şerit, parşömen, düğüm, simge (kum saati ve kilit çizilir)

Sahne önce Tiny Swords'un özgün çözünürlüğünde (64 px karo) kurulur, sonra yarıya indirilir.
UI tarafı 390 birim genişliğinde çalışır: görselin 2 pikseli = 1 UI birimi.
Aşama noktaları CampaignSceneSetup.RoadNodes ile aynı olmalıdır.

Çalıştırma: python Tools/campaign_art/generate.py   (Pillow gerekir)
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TS = os.path.join(ROOT, "Assets", "Tiny Swords")
OUT = os.environ.get("CAMPAIGN_ART_OUT") or os.path.join(ROOT, "Assets", "Resources", "CampaignArt")
T = 64  # karo boyu (özgün)
WATER = (71, 171, 169, 255)
DIRT = (206, 168, 112, 255)
DIRT_DARK = (120, 86, 52, 255)


def load(rel):
    return Image.open(os.path.join(TS, rel)).convert("RGBA")


def frame(rel, size, index=0):
    sheet = load(rel)
    w, h = size
    return sheet.crop((index * w, 0, index * w + w, h))


def trim(img):
    box = img.getbbox()
    return img.crop(box) if box else img


TILESETS = {i: load(f"Terrain/Tileset/Tilemap_color{i}.png") for i in range(1, 6)}


def tile(tileset, col, row):
    return tileset.crop((col * T, row * T, col * T + T, row * T + T))


def island(canvas, tileset, x, y, cols, rows, cliff=True):
    """Dikdörtgen ada (geriye dönük): maske ile land() çağırır."""
    land(canvas, tileset, [[True] * cols for _ in range(rows)], x, y)


def blob_mask(cols, rows, seed, rx=None, ry=None, power=3.2, jitter=0.18):
    """Kenarları düzensiz, köşeleri yuvarlak ada maskesi (süperelips + gürültü)."""
    rnd = random.Random(seed)
    rx = rx or cols / 2 - 0.5
    ry = ry or rows / 2 - 0.5
    cx, cy = (cols - 1) / 2, (rows - 1) / 2
    noise_r = [rnd.uniform(-jitter, jitter) for _ in range(rows)]
    noise_c = [rnd.uniform(-jitter, jitter) for _ in range(cols)]
    mask = []
    for r in range(rows):
        row = []
        for c in range(cols):
            v = abs((c - cx) / rx) ** power + abs((r - cy) / ry) ** power
            row.append(v <= 1 + noise_r[r] + noise_c[c])
        mask.append(row)
    # Tek karoluk çıkıntı/girintileri temizle (karo seti iç köşe içermez).
    for _ in range(2):
        for r in range(rows):
            for c in range(cols):
                n = sum(1 for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1))
                        if 0 <= r + dr < rows and 0 <= c + dc < cols and mask[r + dr][c + dc])
                if mask[r][c] and n <= 1:
                    mask[r][c] = False
                elif not mask[r][c] and n >= 3:
                    mask[r][c] = True
    return mask


def land(canvas, tileset, mask, ox, oy, shallow=True, tint_color=None, tint_amount=0.0):
    """Otomatik karo seçimli yükseltilmiş çimen + kayalık yamaç (Tiny Swords karo seti, sağ blok)."""
    rows, cols = len(mask), len(mask[0])

    def at(r, c):
        return 0 <= r < rows and 0 <= c < cols and mask[r][c]

    if shallow:
        # Adanın çevresinde sığ su halkası.
        layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        for r in range(rows):
            for c in range(cols):
                if mask[r][c]:
                    x, y = ox + c * T, oy + r * T
                    d.rounded_rectangle((x - 26, y - 22, x + T + 26, y + 2 * T + 18), 30, fill=(104, 196, 190, 255))
        canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(10)))

    def col_for(left, right):
        return 6 if left and right else 5 if right else 7 if left else 8

    # Kara ayrı katmanda çizilir: bölge tonu (kar, karanlık) suya taşmasın.
    target = canvas
    canvas = Image.new("RGBA", target.size, (0, 0, 0, 0))

    # Önce yamaç (altında kara olmayan hücrelerin altına), sonra çimen.
    for r in range(rows):
        for c in range(cols):
            if not mask[r][c] or at(r + 1, c):
                continue
            left = at(r, c - 1) and not at(r + 1, c - 1)
            right = at(r, c + 1) and not at(r + 1, c + 1)
            canvas.alpha_composite(tile(tileset, col_for(left, right), 5), (ox + c * T, oy + (r + 1) * T))
    for r in range(rows):
        for c in range(cols):
            if not mask[r][c]:
                continue
            col = col_for(at(r, c - 1), at(r, c + 1))
            up, down = at(r - 1, c), at(r + 1, c)
            row = 1 if up and down else 0 if down else 2 if up else 3
            if col == 8 and row == 3:
                col, row = 8, 3
            canvas.alpha_composite(tile(tileset, col, row), (ox + c * T, oy + r * T))
    if tint_color is not None and tint_amount > 0:
        tint(canvas, tint_color, tint_amount)
    target.alpha_composite(canvas)


def bridge(canvas, x, y0, y1, width=84):
    """Dikey ahşap köprü (adalar arası)."""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    top, bottom = min(y0, y1), max(y0, y1)
    d.rectangle((x - width / 2 + 6, top + 10, x + width / 2 + 6, bottom + 10), fill=(0, 0, 0, 60))
    y = top
    while y < bottom:
        h = min(22, bottom - y)
        if h < 6:
            break
        d.rectangle((x - width / 2, y, x + width / 2, y + h - 3), fill=(171, 120, 70, 255), outline=(92, 58, 30, 255), width=3)
        d.line((x - width / 2 + 8, y + 7, x + width / 2 - 8, y + 7), fill=(196, 146, 92, 255), width=2)
        y += 22
    for side in (-1, 1):
        rx = x + side * (width / 2 + 4)
        d.rectangle((rx - 6, top, rx + 6, bottom), fill=(110, 70, 36, 255), outline=(70, 42, 20, 255), width=2)
    canvas.alpha_composite(layer)


def shadow(canvas, cx, cy, w, h, alpha=70):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=(0, 0, 0, alpha))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(6)))


def put(canvas, img, cx, bottom, scale=1.0, shade=True):
    """Görseli alt orta noktasından yerleştirir (ağaç, bina gibi)."""
    img = trim(img)
    if scale != 1.0:
        img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.LANCZOS)
    if shade:
        shadow(canvas, cx, bottom - 4, img.width * 0.8, max(12, img.height * 0.12))
    canvas.alpha_composite(img, (int(cx - img.width / 2), int(bottom - img.height)))


def tree(canvas, cx, bottom, kind=1, scale=1.0, frame_index=0):
    size = (192, 256) if kind in (1, 2) else (192, 192)
    put(canvas, frame(f"Pawn and Resources/Wood/Trees/Tree{kind}.png", size, frame_index), cx, bottom, scale)


def bush(canvas, cx, bottom, kind=1, scale=1.0):
    put(canvas, frame(f"Terrain/Decorations/Bushes/Bush {kind}.png", (128, 128)), cx, bottom, scale, shade=False)


def rock(canvas, cx, bottom, kind=1, scale=1.0):
    put(canvas, load(f"Terrain/Decorations/Rocks/Rock{kind}.png"), cx, bottom, scale, shade=False)


def building(canvas, color, name, cx, bottom, scale=1.0):
    put(canvas, load(f"Buildings/{color} Buildings/{name}.png"), cx, bottom, scale)


def gold(canvas, cx, bottom, kind=4, scale=1.0):
    put(canvas, load(f"Pawn and Resources/Gold/Gold Stones/Gold Stone {kind}.png"), cx, bottom, scale)


def cloud(canvas, cx, cy, kind=1, scale=1.0, alpha=1.0):
    img = trim(load(f"Terrain/Decorations/Clouds/Clouds_{kind:02d}.png"))
    img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)
    if alpha < 1.0:
        a = img.getchannel("A").point(lambda v: int(v * alpha))
        img.putalpha(a)
    canvas.alpha_composite(img, (int(cx - img.width / 2), int(cy - img.height / 2)))


def path(canvas, points, width=26):
    """Toprak patika: koyu kenarlı, açık kahve şerit; uçlar yuvarlak."""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    smooth = catmull(points, 18)
    for color, w in ((DIRT_DARK, width + 8), (DIRT, width)):
        d.line(smooth, fill=color, width=w, joint="curve")
        for p in (smooth[0], smooth[-1]):
            d.ellipse((p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2), fill=color)
    # Hafif taş benekleri
    rnd = random.Random(len(points) * 31)
    for i in range(0, len(smooth), 3):
        x, y = smooth[i]
        if rnd.random() < 0.6:
            ox, oy = rnd.uniform(-width / 3, width / 3), rnd.uniform(-width / 4, width / 4)
            d.ellipse((x + ox - 3, y + oy - 2, x + ox + 3, y + oy + 2), fill=(170, 134, 88, 255))
    canvas.alpha_composite(layer)


def catmull(points, steps):
    if len(points) < 3:
        return points
    pts = [points[0]] + points + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[k]) + (-p0[k] + p2[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t2
                                    + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t3) for k in range(2)))
    out.append(points[-1])
    return out


def fog(canvas, rnd, count, area, alpha=90):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0, y0, x1, y1 = area
    for _ in range(count):
        cx, cy = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        w, h = rnd.uniform(160, 320), rnd.uniform(50, 90)
        d.ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=(235, 245, 240, alpha))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(18)))


def tint(img, color, amount, region=None):
    """Bir bölgeyi renge doğru boyar (kar, karanlık kale)."""
    overlay = Image.new("RGBA", img.size, color + (0,))
    mask = img.getchannel("A").point(lambda v: int(v * amount))
    if region is not None:
        m = Image.new("L", img.size, 0)
        m.paste(mask.crop(region), region[:2])
        mask = m
    overlay.putalpha(mask)
    img.alpha_composite(overlay)


def snow(canvas, rnd, area, count=60):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0, y0, x1, y1 = area
    for _ in range(count):
        cx, cy = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        w, h = rnd.uniform(40, 110), rnd.uniform(14, 30)
        d.ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=(248, 252, 255, 210))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(2)))


def water_canvas(w, h):
    img = Image.new("RGBA", (w, h), WATER)
    # Hafif dalga çizgileri
    d = ImageDraw.Draw(img)
    rnd = random.Random(w * 7 + h)
    for _ in range(w * h // 9000):
        x, y = rnd.uniform(0, w), rnd.uniform(0, h)
        d.arc((x - 18, y - 6, x + 18, y + 6), 200, 340, fill=(98, 192, 188, 255), width=3)
    return img


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    small = img.resize((img.width // 2, img.height // 2), Image.LANCZOS)
    small.save(os.path.join(OUT, name), optimize=True)
    print("yazıldı", name, small.size)


# ======================= SEFER HARİTASI (bölge listesi) =======================
MW, MH = 1560, 880  # -> 780x440
CX = MW // 2  # patika karoların alt/üst kenarında ortadan geçer: karolar üst üste dizilince birleşir
TILESET_OF = {1: 1, 2: 3, 3: 4, 4: 5, 5: 5, 6: 4}


def mask_cells(mask, ox, oy):
    return [(ox + c * T + T / 2, oy + r * T + T / 2) for r, row in enumerate(mask) for c, v in enumerate(row) if v]


def region_flavor(img, region, rnd, spots, avoid):
    """Bölgeye özgü süsler. spots: ada üstündeki boş noktalar (karıştırılmış)."""
    def take(n, margin=95):
        out = []
        for p in list(spots):
            if len(out) >= n:
                break
            if all(math.hypot(p[0] - a[0], p[1] - a[1]) > margin for a in avoid + out):
                out.append(p)
        for p in out:
            spots.remove(p)
        return out

    if region == 1:
        for x, y in take(7):
            tree(img, x, y + 40, rnd.choice([1, 2]), rnd.uniform(0.75, 0.95), rnd.randint(0, 5))
        for x, y in take(6, 70):
            bush(img, x, y + 30, rnd.randint(1, 4), 0.8)
        for x, y in take(3, 70):
            rock(img, x, y + 20, rnd.randint(1, 4), 1.1)
    elif region == 2:
        for x, y in take(22, 80):
            tree(img, x, y + 40, rnd.choice([1, 2, 3, 4]), rnd.uniform(0.7, 0.95), rnd.randint(0, 5))
    elif region == 3:
        for k, (x, y) in enumerate(take(7)):
            gold(img, x, y + 30, 1 + k % 6, 1.25)
        for x, y in take(10, 70):
            rock(img, x, y + 20, rnd.randint(1, 4), 1.35)
    elif region == 4:
        for x, y in take(8, 80):
            rock(img, x, y + 20, rnd.randint(1, 4), 1.5)
        for x, y in take(5):
            tree(img, x, y + 40, 3, 0.85, rnd.randint(0, 5))
    elif region == 6:
        for x, y in take(4, 140):
            lava(img, x, y, rnd.uniform(70, 120))
        for x, y in take(9, 70):
            rock(img, x, y + 20, rnd.randint(1, 4), 1.4)
        for x, y in take(4, 80):
            put(img, load(f"Pawn and Resources/Wood/Trees/Stump {rnd.randint(1, 4)}.png"), x, y + 24, 0.9)
    elif region == 5:
        for x, y in take(6, 80):
            put(img, load(f"Pawn and Resources/Wood/Trees/Stump {rnd.randint(1, 4)}.png"), x, y + 24, 0.9)
        for x, y in take(7, 70):
            rock(img, x, y + 20, rnd.randint(1, 4), 1.3)


REGION_TINT = {4: ((232, 244, 255), 0.40), 5: ((46, 18, 64), 0.55), 6: ((92, 30, 14), 0.55)}


def lava(img, cx, cy, size):
    """Parlayan lav gölü: dışta sıcak ışık, koyu kenar, turuncu-sarı iç."""
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((cx - size * 1.3, cy - size * 0.75, cx + size * 1.3, cy + size * 0.75),
                                 fill=(255, 120, 30, 120))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(18)))
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.ellipse((cx - size, cy - size * 0.55, cx + size, cy + size * 0.55), fill=(58, 30, 24, 255))
    d.ellipse((cx - size * 0.86, cy - size * 0.44, cx + size * 0.86, cy + size * 0.44), fill=(236, 92, 30, 255))
    d.ellipse((cx - size * 0.55, cy - size * 0.26, cx + size * 0.5, cy + size * 0.22), fill=(255, 190, 70, 255))
    img.alpha_composite(layer)


def region_land(img, region, mask, ox, oy):
    """Bölgenin karası, bölge tonuyla (kar/karanlık yalnız karaya uygulanır)."""
    color, amount = REGION_TINT.get(region, (None, 0.0))
    land(img, TILESETS[TILESET_OF[region]], mask, ox, oy, tint_color=color, tint_amount=amount)


def soft_snow(img, rnd, box, count):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        cx, cy = rnd.uniform(box[0] + 60, box[2] - 60), rnd.uniform(box[1] + 60, box[3] - 90)
        w, h = rnd.uniform(26, 70), rnd.uniform(10, 18)
        d.ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=(250, 253, 255, 170))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(3)))


def island_fog(img, rnd, box, count, alpha=85):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        cx, cy = rnd.uniform(box[0] + 160, box[2] - 160), rnd.uniform(box[1] + 60, box[3] - 80)
        w, h = rnd.uniform(200, 340), rnd.uniform(46, 80)
        d.ellipse((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=(236, 246, 240, alpha))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(22)))


LANDMARKS = {
    1: [("Red", "Castle", 430, 470, 0.85), ("Red", "House1", -470, 690, 0.8)],
    2: [("Purple", "House2", 470, 430, 0.8)],
    3: [("Yellow", "House3", -470, 440, 0.85), ("Yellow", "Tower", 500, 690, 0.75)],
    4: [("Blue", "Tower", -480, 450, 0.8), ("Blue", "Barracks", 460, 700, 0.75)],
    5: [("Black", "Castle", 400, 480, 1.05), ("Black", "Tower", -500, 460, 0.85)],
    6: [("Red", "Tower", 460, 440, 0.85), ("Black", "Barracks", -470, 690, 0.8)],
}


def map_region(region):
    rnd = random.Random(region * 101)
    img = water_canvas(MW, MH)
    cols, rows = 22, 10
    ox, oy = (MW - cols * T) // 2, 110
    mask = blob_mask(cols, rows, region * 13, power=3.0, jitter=0.22)
    # Köprü uçları için orta sütunlar üstte/altta kara olsun.
    for c in range(cols // 2 - 1, cols // 2 + 1):
        for r in (0, 1, rows - 2, rows - 1):
            mask[r][c] = True
    bridge(img, CX, MH, oy + rows * T + T - 24)
    bridge(img, CX, 0, oy + 24)
    region_land(img, region, mask, ox, oy)
    box = (ox, oy, ox + cols * T, oy + rows * T + T)
    side = 1 if region % 2 else -1
    pts = [(CX, oy + rows * T + 10), (CX, oy + rows * T - 60), (CX + side * 300, 610),
           (CX - side * 280, 380), (CX + side * 40, 200), (CX, oy + 30)]
    path(img, pts)
    smooth = catmull(pts, 12)
    marks = LANDMARKS[region]
    for color, name, dx, y, scale in marks:
        building(img, color, name, CX + side * dx, y, scale)
    cells = mask_cells(mask, ox, oy)
    rnd.shuffle(cells)
    spots = [p for p in cells if oy + 90 < p[1] < oy + rows * T - 40]
    avoid = smooth[::3] + [(CX + side * dx, y - 60) for _, _, dx, y, _ in marks]
    region_flavor(img, region, rnd, spots, avoid)
    if region == 4:
        soft_snow(img, rnd, box, 34)
    if region == 2:
        island_fog(img, rnd, box, 10)
    save(img, f"map_region_{region}.png")


def map_endless():
    rnd = random.Random(7)
    img = water_canvas(MW, MH)
    cols, rows = 12, 6
    ox, oy = (MW - cols * T) // 2, 300
    mask = blob_mask(cols, rows, 99, power=2.4, jitter=0.15)
    for c in range(cols // 2 - 1, cols // 2 + 1):
        mask[rows - 1][c] = mask[rows - 2][c] = True
    bridge(img, CX, MH, oy + rows * T + T - 24)
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((CX - 360, oy - 140, CX + 360, oy + 300), fill=(176, 132, 232, 170))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(70)))
    land(img, TILESETS[2], mask, ox, oy, tint_color=(120, 80, 170), tint_amount=0.30)
    path(img, [(CX, oy + rows * T + 10), (CX, oy + rows * T - 80), (CX - 60, oy + 230), (CX, oy + 150)])
    building(img, "Purple", "Monastery", CX, oy + 150, 0.95)
    for x, y in [(ox + 120, oy + 300), (ox + cols * T - 110, oy + 280)]:
        tree(img, x, y, 4, 0.75, 2)
    for k, (x, y, s) in enumerate([(230, 170, 1.05), (1320, 200, 1.15), (300, 640, 0.85), (1250, 650, 0.95),
                                   (120, 420, 0.75), (1470, 440, 0.75), (780, 60, 0.7)]):
        cloud(img, x, y, 1 + k % 8, s, 0.95)
    save(img, "map_endless.png")


# ======================= BÖLGE EKRANI (5 aşamalı yol) =======================
RW, RH = 1560, 2160  # -> 780x1080 (UI: 390x540)
# Aşama noktaları (UI birimi, içerik sol üstünden): CampaignSceneSetup.RoadNodes ile aynı.
NODES_UI = [(120, 470), (262, 372), (118, 274), (262, 176), (190, 76)]
HOUSES = {1: [("Red", "House1", 300, 1560), ("Red", "Tower", 1300, 1180)],
          2: [("Purple", "House2", 1280, 1600)],
          3: [("Yellow", "House3", 1260, 1560), ("Yellow", "Tower", 300, 1170)],
          4: [("Blue", "Tower", 300, 1200), ("Blue", "House1", 1280, 1620)],
          5: [("Black", "Tower", 300, 1220), ("Black", "Tower", 1290, 1620)],
          6: [("Red", "Tower", 300, 1220), ("Black", "House3", 1280, 1620)]}
CASTLE_COLOR = {1: "Red", 2: "Purple", 3: "Yellow", 4: "Blue", 5: "Black", 6: "Red"}


def to_px(p):
    return (p[0] * 4, p[1] * 4)


def road_region(region):
    rnd = random.Random(region * 977)
    img = water_canvas(RW, RH)
    cols, rows = 23, 30
    ox, oy = (RW - cols * T) // 2, 40
    mask = blob_mask(cols, rows, region * 71, power=4.0, jitter=0.12)
    nodes = [to_px(p) for p in NODES_UI]
    start_x = nodes[0][0] - 120
    sc = int((start_x - ox) // T)
    for r in (rows - 1, rows - 2):
        for c in (sc - 1, sc, sc + 1):
            mask[r][c] = True
    bridge(img, start_x, RH, oy + rows * T + T - 24)
    region_land(img, region, mask, ox, oy)
    box = (ox, oy, ox + cols * T, oy + rows * T + T)
    pts = [(start_x, oy + rows * T + 10), (start_x, oy + rows * T - 90)] + nodes
    path(img, pts, 30)
    smooth = catmull(pts, 12)
    boss = nodes[4]
    castle_at = (boss[0] + 380, boss[1] + 150)
    houses = HOUSES[region]
    avoid = smooth[::3] + list(nodes) + [(castle_at[0], castle_at[1] - 80)] + [(x, y - 60) for _, _, x, y in houses]
    cells = mask_cells(mask, ox, oy)
    rnd.shuffle(cells)
    spots = [p for p in cells if oy + 120 < p[1] < oy + rows * T - 30
             and all(math.hypot(p[0] - n[0], p[1] - n[1]) > 190 for n in nodes)]
    for color, name, x, y in houses:
        building(img, color, name, x, y, 0.85)
    building(img, CASTLE_COLOR[region], "Castle", castle_at[0], castle_at[1], 0.9)
    for _ in range(3):
        region_flavor(img, region, rnd, spots, avoid)
    for x, y in spots[:14]:
        if all(math.hypot(x - a[0], y - a[1]) > 80 for a in avoid):
            if region in (1, 2):
                bush(img, x, y + 30, rnd.randint(1, 4), 0.8)
            elif region == 3:
                gold(img, x, y + 26, rnd.randint(1, 6), 1.0)
            else:
                rock(img, x, y + 20, rnd.randint(1, 4), 1.2)
    if region == 4:
        soft_snow(img, rnd, box, 90)
    # Aşama meydanları: düğmenin oturduğu toprak alan.
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for n in nodes:
        d.ellipse((n[0] - 118, n[1] - 66, n[0] + 118, n[1] + 92), fill=DIRT_DARK)
        d.ellipse((n[0] - 110, n[1] - 58, n[0] + 110, n[1] + 84), fill=DIRT)
    img.alpha_composite(layer)
    if region == 2:
        island_fog(img, rnd, box, 26, 80)
    save(img, f"road_region_{region}.png")




# ======================= ARAYÜZ PARÇALARI =======================
UI_COPY = {
    "ui_ribbon_blue": "UI Elements/Ribbons/BigRibbons 1.png",
    "ui_ribbon_red": "UI Elements/Ribbons/BigRibbons 2.png",
    "ui_ribbon_gold": "UI Elements/Ribbons/BigRibbons 3.png",
    "ui_ribbon_purple": "UI Elements/Ribbons/BigRibbons 4.png",
    "ui_ribbon_dark": "UI Elements/Ribbons/BigRibbons 5.png",
    "ui_paper": "UI Elements/Papers/RegularPaper.png",
    "ui_scroll": "UI Elements/Banners/Banner.png",
    "ui_node_blue": "UI Elements/Buttons/SmallBlueRoundButton_Regular.png",
    "ui_node_red": "UI Elements/Buttons/TinyRoundRedButton.png",
    "ui_icon_coin": "UI Elements/Icons/Icon_03.png",
    "ui_icon_sword": "UI Elements/Icons/Icon_05.png",
    "ui_icon_shield": "UI Elements/Icons/Icon_06.png",
    "ui_icon_hammer": "UI Elements/Icons/Icon_01.png",
    "ui_sword_red": "UI Elements/Swords/Swords 2.png",
}


def save_raw(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"), optimize=True)
    print("yazıldı", name, img.size)


def outline_draw(size, draw_fn):
    """Tiny Swords tarzı koyu dış çizgili küçük simge."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(img), size)
    return img


def clock_icon():
    def fn(d, s):
        # Kum saati: ahşap çerçeve, iki cam hazne, akan kum.
        dark, wood, glass, sand = (40, 30, 26, 255), (176, 120, 66, 255), (214, 238, 240, 255), (238, 196, 92, 255)
        d.rounded_rectangle((10, 6, s - 10, 16), 4, fill=wood, outline=dark, width=3)
        d.rounded_rectangle((10, s - 16, s - 10, s - 6), 4, fill=wood, outline=dark, width=3)
        d.polygon([(16, 16), (s - 16, 16), (s // 2 + 3, s // 2), (s - 16, s - 16), (16, s - 16), (s // 2 - 3, s // 2)],
                  fill=glass, outline=dark)
        d.line([(16, 16), (s // 2 - 3, s // 2), (16, s - 16)], fill=dark, width=3)
        d.line([(s - 16, 16), (s // 2 + 3, s // 2), (s - 16, s - 16)], fill=dark, width=3)
        d.polygon([(20, 19), (s - 20, 19), (s // 2, s // 2 - 4)], fill=sand)
        d.polygon([(s // 2, s // 2 + 8), (s - 19, s - 19), (19, s - 19)], fill=sand)
    return outline_draw(64, fn)


def lock_icon():
    def fn(d, s):
        dark, metal, body = (40, 30, 26, 255), (168, 176, 186, 255), (222, 170, 64, 255)
        d.arc((18, 6, s - 18, 40), 180, 360, fill=dark, width=11)
        d.arc((18, 6, s - 18, 40), 180, 360, fill=metal, width=6)
        d.line((21, 22, 21, 30), fill=dark, width=8)
        d.line((s - 21, 22, s - 21, 30), fill=dark, width=8)
        d.rounded_rectangle((12, 28, s - 12, s - 6), 7, fill=body, outline=dark, width=4)
        d.ellipse((s // 2 - 5, 38, s // 2 + 5, 48), fill=dark)
        d.rectangle((s // 2 - 2, 44, s // 2 + 2, 54), fill=dark)
    return outline_draw(64, fn)


def heart_icon():
    def fn(d, s):
        dark, red, light = (40, 22, 22, 255), (214, 64, 64, 255), (246, 150, 140, 255)
        # Basit kalp: iki daire + üçgen.
        d.ellipse((8, 10, s / 2 + 2, s / 2 + 8), fill=dark)
        d.ellipse((s / 2 - 2, 10, s - 8, s / 2 + 8), fill=dark)
        d.polygon([(9, s / 2 - 2), (s - 9, s / 2 - 2), (s / 2, s - 7)], fill=dark)
        d.ellipse((12, 14, s / 2 - 1, s / 2 + 5), fill=red)
        d.ellipse((s / 2 + 1, 14, s - 12, s / 2 + 5), fill=red)
        d.polygon([(13, s / 2 - 2), (s - 13, s / 2 - 2), (s / 2, s - 12)], fill=red)
        d.ellipse((18, 19, 27, 27), fill=light)
    return outline_draw(64, fn)


def calendar_icon():
    def fn(d, s):
        dark, paper, red, ink = (40, 30, 26, 255), (246, 236, 210, 255), (200, 64, 52, 255), (120, 90, 60, 255)
        d.rounded_rectangle((8, 12, s - 8, s - 6), 7, fill=paper, outline=dark, width=4)
        d.rounded_rectangle((8, 12, s - 8, 26), 7, fill=red, outline=dark, width=4)
        d.rectangle((10, 22, s - 10, 26), fill=red)
        for x in (20, s - 20):
            d.rounded_rectangle((x - 3, 6, x + 3, 18), 2, fill=dark)
        for r in range(2):
            for c in range(3):
                x, y = 17 + c * 11, 32 + r * 11
                d.rectangle((x, y, x + 7, y + 6), fill=ink)
    return outline_draw(64, fn)


def ui_parts():
    for name, rel in UI_COPY.items():
        save_raw(trim(load(rel)), name)
    save_raw(clock_icon(), "ui_icon_clock")
    save_raw(lock_icon(), "ui_icon_lock")
    save_raw(heart_icon(), "ui_icon_heart")
    save_raw(calendar_icon(), "ui_icon_calendar")
    # Kahraman işareti: haritada bulunulan yerde duran mavi Warrior (bekleme karesi).
    hero = trim(frame("Units/Blue Units/Warrior/Warrior_Idle.png", (192, 192), 0))
    save_raw(hero, "ui_hero_marker")


if __name__ == "__main__":
    for r in range(1, 7):
        map_region(r)
        road_region(r)
    map_endless()
    ui_parts()
