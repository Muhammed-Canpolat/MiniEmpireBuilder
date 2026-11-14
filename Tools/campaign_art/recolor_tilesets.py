"""
7. oturum (D89): Bölge 4–6 zemin karoları — Tiny Swords karo setinden RENK EŞLEMESİ (gradient map).

Neden: Projede (Tiny Swords, Cainos, Handpainted…) kar / koyu taş / volkanik piksel-sanat karosu yok. Eskiden yeşil çim karosuna
yarı saydam renk bindiriliyordu (gri-yeşil, "boyanmış çim" görünümü). Burada her piksel tonuna göre sınıflandırılır:
  * yeşil/sarı (çim)      -> bölgenin ZEMİN paleti (luminansa göre 3 duraklı gradyan)
  * mavi-gri (uçurum taşı) -> bölgenin TAŞ paleti
  * çok açık (kıyı köpüğü) ve çok koyu (kontur) -> hafif tonlanır, kontrast korunur
Böylece Tiny Swords'un kontur, doku ve kenar biçimi aynen kalır; renk gerçek kar/kayrak/kül gibi olur.
Gerçek karolar gelirse (ART_NEEDS #8) bu dosyalar değiştirilir, kod aynı kalır.

Çıktı: Assets/Art/BattleMaps/Tileset_<ad>.png (576x384, Tilemap_colorN ile aynı dilimleme; .meta Tilemap_color1'den kopyalanır).
Çalıştırma: python Tools/campaign_art/recolor_tilesets.py
"""
import colorsys
import os
import re
import uuid

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Tiny Swords", "Terrain", "Tileset")
OUT = os.path.join(ROOT, "Assets", "Art", "BattleMaps")

# ad: (kaynak renk, zemin paleti [koyu, orta, açık], taş paleti [koyu, orta, açık])
PALETTES = {
    "snow": (2, [(158, 178, 204), (228, 237, 247), (255, 255, 255)],
                [(52, 78, 112), (104, 140, 176), (170, 200, 226)]),
    "dark": (2, [(36, 32, 54), (80, 72, 108), (128, 118, 156)],
                [(26, 26, 42), (64, 64, 92), (112, 112, 146)]),
    "ash":  (4, [(38, 24, 20), (86, 58, 44), (128, 92, 68)],
                [(36, 28, 28), (82, 66, 62), (130, 112, 104)]),
}


def ramp(stops, t):
    t = max(0.0, min(1.0, t))
    if t < 0.5:
        a, b, k = stops[0], stops[1], t / 0.5
    else:
        a, b, k = stops[1], stops[2], (t - 0.5) / 0.5
    return tuple(int(round(a[i] + (b[i] - a[i]) * k)) for i in range(3))


# Zemin gradyanı gaması (<1 açar): kar çoğunlukla beyaz, gölgeler mavi kalsın.
GAMMA = {"snow": 0.5, "dark": 0.9, "ash": 1.0}


def recolor(src, ground, stone, gamma=1.0):
    img = src.copy()
    px = img.load()
    # Kaynak zemin/taş luminans aralıklarını ölç (gradyanı tüm aralığa yay).
    gl, sl = [], []
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a < 10:
                continue
            h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
            hue = h * 360
            if s > 0.18 and 40 <= hue <= 150:
                gl.append(l)
            elif 150 < hue <= 240:
                sl.append(l)
    def span(v):
        v = sorted(v)
        return (v[int(len(v) * 0.02)], v[int(len(v) * 0.98)]) if v else (0.0, 1.0)
    g0, g1 = span(gl)
    s0, s1 = span(sl)
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a < 10:
                continue
            h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
            hue = h * 360
            if l < 0.16:   # kontur: koyulaştırılmış taş tonu
                c = stone[0]
                px[x, y] = (int(c[0] * 0.55), int(c[1] * 0.55), int(c[2] * 0.55), a)
            elif l > 0.82 and s < 0.5:   # kıyı köpüğü / parıltı: açık taş tonu
                c = ramp(stone, 1.0)
                px[x, y] = (min(255, c[0] + 20), min(255, c[1] + 20), min(255, c[2] + 20), a)
            elif s > 0.18 and 40 <= hue <= 150:
                c = ramp(ground, max(0.0, min(1.0, (l - g0) / max(0.01, g1 - g0))) ** gamma)
                px[x, y] = c + (a,)
            else:
                c = ramp(stone, (l - s0) / max(0.01, s1 - s0))
                px[x, y] = c + (a,)
    return img


def write_meta(name):
    src_meta = os.path.join(SRC, "Tilemap_color1.png.meta")
    dst_meta = os.path.join(OUT, name + ".meta")
    if os.path.exists(dst_meta):
        return   # GUID sabit kalsın (Unity referansları bozulmasın)
    text = open(src_meta, encoding="utf-8").read()
    text = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, text, count=1)
    open(dst_meta, "w", encoding="utf-8", newline="\n").write(text)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (source, ground, stone) in PALETTES.items():
        src = Image.open(os.path.join(SRC, f"Tilemap_color{source}.png")).convert("RGBA")
        out = recolor(src, ground, stone, GAMMA.get(name, 1.0))
        fname = f"Tileset_{name}.png"
        out.save(os.path.join(OUT, fname), optimize=True)
        write_meta(fname)
        print("yazıldı", fname)


if __name__ == "__main__":
    main()
