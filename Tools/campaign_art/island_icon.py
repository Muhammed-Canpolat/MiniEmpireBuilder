"""4. oturum (3.2): Radyal menü ADA ikonu — Tiny Swords parçalarından: su zemini + çimen kara parçası (Tilemap_color1 3×3 düz zemin
bloğu) + mavi Gözcü Kulesi + ağaç. Çıktı: Assets/Resources/CampaignArt/ui_icon_island.png (256×256, saydam köşeler, daire içine sığar).
Çalıştır: python Tools/campaign_art/island_icon.py (proje kökünden)."""
from PIL import Image, ImageDraw

TS = 'Assets/Tiny Swords/'
OUT = 'Assets/Resources/CampaignArt/ui_icon_island.png'
S = 256

water = Image.open(TS + 'Terrain/Tileset/Water Background color.png').convert('RGBA').getpixel((10, 10))
tiles = Image.open(TS + 'Terrain/Tileset/Tilemap_color1.png').convert('RGBA')
tower = Image.open(TS + 'Buildings/Blue Buildings/Tower.png').convert('RGBA')
tree_sheet = Image.open(TS + 'Pawn and Resources/Wood/Trees/Tree1.png').convert('RGBA')
tree = tree_sheet.crop((0, 0, 192, 192))
foam = Image.open(TS + 'Terrain/Tileset/Water Foam.png').convert('RGBA')

canvas = Image.new('RGBA', (S, S), (0, 0, 0, 0))
# Su: daire (radyal düğmenin yüzüne oturur)
mask = Image.new('L', (S, S), 0)
ImageDraw.Draw(mask).ellipse((4, 4, S - 4, S - 4), fill=255)
sea = Image.new('RGBA', (S, S), water)
canvas.paste(sea, (0, 0), mask)

# Köpük halkası (kara kenarı) — Water Foam ilk kare (192×192)
island_box = (36, 110, 220, 214)
foam0 = foam.crop((0, 0, 192, 192)).resize((island_box[2] - island_box[0] + 30, island_box[3] - island_box[1] + 30), Image.NEAREST)
# (köpük karesi kullanılmıyor: halka elipsle çiziliyor)

# Kara: 3×3 düz zemin bloğu dokusu, elips (ada) biçiminde; altında açık köpük halkası
w, h = island_box[2] - island_box[0], island_box[3] - island_box[1]
ring = Image.new('L', (w + 16, h + 14), 0)
ImageDraw.Draw(ring).ellipse((0, 0, w + 15, h + 13), fill=210)
canvas.paste(Image.new('RGBA', ring.size, (236, 248, 240, 255)), (island_box[0] - 8, island_box[1] - 4), ring)
block = tiles.crop((64, 64, 128, 128))   # orta çimen karosu (kenarsız)
land = block.resize((w, h), Image.NEAREST)
lmask = Image.new('L', (w, h), 0)
ImageDraw.Draw(lmask).ellipse((0, 0, w - 1, h - 1), fill=255)
land.putalpha(lmask)
canvas.alpha_composite(land, (island_box[0], island_box[1]))

# Ağaç (sol) ve kule (sağ-orta)
t = tree.resize((96, 96), Image.NEAREST)
canvas.alpha_composite(t, (50, 78))
tw = tower.resize((80, 160), Image.NEAREST)
canvas.alpha_composite(tw, (112, 26))

# Daire dışını kırp
final = Image.new('RGBA', (S, S), (0, 0, 0, 0))
final.paste(canvas, (0, 0), mask)
final.save(OUT)
print('yazıldı', OUT)
