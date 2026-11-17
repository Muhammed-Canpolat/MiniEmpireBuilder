"""D121 (grup 9, Depo): DEPOYA düğmesi ve DEPO sekmesi simgesi. Yeni çizim yok: Cainos Village Props'taki
'TX Chest Animation Wooden 0' karesi (kapalı tahta sandık; .meta'daki dilim: x 31, y 448, 34x29, Unity alt-sol kökenli)
kesilir, 2x en-yakın-komşu büyütülür ve 68x68 saydam tuvalin ortasına konur (piksel sanatı bulanıklaşmaz).
Çıktı: Assets/Resources/CampaignArt/ui_icon_depot.png. Çalıştır: python Tools/campaign_art/depot_icon.py"""
from PIL import Image

SRC = 'Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Chest Animation.png'
OUT = 'Assets/Resources/CampaignArt/ui_icon_depot.png'
X, Y, W, H = 31, 448, 34, 29     # TX Chest Animation Wooden 0 (Unity sprite rect)
SCALE, CANVAS = 2, 68


def main():
    sheet = Image.open(SRC).convert('RGBA')
    top = sheet.height - (Y + H)          # Unity rect y alttan; PIL üstten
    chest = sheet.crop((X, top, X + W, top + H)).resize((W * SCALE, H * SCALE), Image.NEAREST)
    canvas = Image.new('RGBA', (CANVAS, CANVAS), (0, 0, 0, 0))
    canvas.paste(chest, ((CANVAS - chest.width) // 2, (CANVAS - chest.height) // 2), chest)
    canvas.save(OUT)
    print('yazildi', OUT, canvas.getbbox())


if __name__ == '__main__':
    main()
