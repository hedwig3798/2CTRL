"""차원별 추가 무기 7종 이미지 (Ninja Adventure 원본 + 로컬 이미지 생성 결과)
usage: extra_weapons.py <pack_root> <gen_dir> <out_dir>
- gen_dir: 이미지 생성 원본 (마젠타 배경). Vine_Segment.png, Root_Area.png, Tornado_Body.png
- out_dir: Assets/Resources/Textuer/Art (weapon/ → Weapon, icon/ → Icon)"""
import sys, os
from PIL import Image

ROOT, GEN, OUT = sys.argv[1:4]
WEAPON = os.path.join(OUT, 'Weapon')
ICON = os.path.join(OUT, 'Icon')
OUTLINE = (20, 16, 28, 255)


def load(rel):
    return Image.open(os.path.join(ROOT, rel)).convert('RGBA')


def cell(img, i, w, h=None):
    h = h or img.height
    return img.crop((i * w, 0, i * w + w, h))


def bbox_crop(img):
    b = img.getbbox()
    return img.crop(b) if b else img


def fit(img, size=32, upscale=True):
    """size x size 캔버스 가운데. upscale 이면 들어가는 최대 정수배로 확대, 크면 축소"""
    img = bbox_crop(img)
    if img.width > size or img.height > size:
        r = min(size / img.width, size / img.height)
        img = img.resize((max(1, int(img.width * r)), max(1, int(img.height * r))), Image.NEAREST)
    elif upscale:
        k = max(1, min(size // img.width, size // img.height))
        img = img.resize((img.width * k, img.height * k), Image.NEAREST)
    c = Image.new('RGBA', (size, size))
    c.paste(img, ((size - img.width) // 2, (size - img.height) // 2), img)
    return c


def tint(img, rgb, keep=0.35):
    """밝기는 유지하고 색을 rgb 쪽으로"""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            l = (r + g + b) / 765
            px[x, y] = (int(rgb[0] * l * (1 - keep) + r * keep), int(rgb[1] * l * (1 - keep) + g * keep),
                        int(rgb[2] * l * (1 - keep) + b * keep), a)
    return out


def outline(img):
    out = img.copy()
    src = img.load()
    px = out.load()
    for y in range(img.height):
        for x in range(img.width):
            if src[x, y][3] != 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < img.width and 0 <= ny < img.height and src[nx, ny][3] > 128 and src[nx, ny][:3] != OUTLINE[:3]:
                    px[x, y] = OUTLINE
                    break
    return out


def from_gen(name, size=32, colors=16):
    """마젠타 배경 생성 이미지 → 투명 배경 size 픽셀 아트"""
    img = Image.open(os.path.join(GEN, name + '.png')).convert('RGB')
    w, h = img.size
    rgba = Image.new('RGBA', (w, h))
    src = img.load()
    px = rgba.load()
    for y in range(h):
        for x in range(w):
            r, g, b = src[x, y]
            # 마젠타(r,b 높고 g 낮음) 는 배경
            if r > 150 and b > 100 and g < 110 and r - g > 90 and b - g > 50:
                continue
            px[x, y] = (r, g, b, 255)
    rgba = bbox_crop(rgba)
    k = max(rgba.width, rgba.height) / (size - 2)
    small = rgba.resize((max(1, round(rgba.width / k)), max(1, round(rgba.height / k))), Image.BOX)
    # 반투명 가장자리 정리 후 색 수 줄이기
    sp = small.load()
    for y in range(small.height):
        for x in range(small.width):
            r, g, b, a = sp[x, y]
            sp[x, y] = (r, g, b, 255 if a >= 140 else 0)
    alpha = small.getchannel('A')
    q = small.convert('RGB').quantize(colors, method=Image.Quantize.MEDIANCUT).convert('RGBA')
    q.putalpha(alpha)
    return fit(q, size, upscale=False)


def save(img, folder, name):
    img.save(os.path.join(folder, name + '.png'))


def icon(base_rel, overlay=None, overlay_size=16):
    """Skill Icon(24x24) 바탕에 무기 그림을 겹친다"""
    base = load(base_rel)
    if overlay is not None:
        o = bbox_crop(overlay)
        r = min(overlay_size / o.width, overlay_size / o.height)
        o = o.resize((max(1, int(o.width * r)), max(1, int(o.height * r))), Image.NEAREST)
        base.paste(o, ((base.width - o.width) // 2, (base.height - o.height) // 2), o)
    return base


def main():
    # ---- 숲 ----
    vine = from_gen('Vine_Segment')
    save(vine, WEAPON, 'D1_Weapon_Vine_Segment')
    thorn = cell(load('FX/Projectile/PlantSpike.png'), 3, 24)
    save(fit(thorn, upscale=False), WEAPON, 'D1_Weapon_Vine_Thorn')
    spirit = tint(cell(load('FX/Magic/Spirit/SpriteSheet.png'), 2, 32), (140, 255, 150), keep=0.25)
    save(fit(outline(bbox_crop(spirit)), upscale=False), WEAPON, 'D1_Weapon_Spirit_Body')
    root = from_gen('Root_Area')
    save(root, WEAPON, 'D1_Weapon_Root_Area')

    # ---- 공장 ----
    saw = tint(cell(load('FX/Projectile/BigShuriken.png'), 0, 23), (200, 210, 225), keep=0.4)
    save(fit(saw), WEAPON, 'D2_Weapon_Saw_Blade')
    flame = cell(load('FX/Elemental/Flam/SpriteSheet.png'), 2, 40)
    save(fit(flame, upscale=False), WEAPON, 'D2_Weapon_Flame_Fire')

    # ---- 폭풍 ----
    tornado = from_gen('Tornado_Body')
    save(tornado, WEAPON, 'D3_Weapon_Tornado_Body')
    bolt = cell(load('FX/Elemental/Thunder/SpriteSheet.png'), 4, 20)
    save(fit(bolt), WEAPON, 'D3_Weapon_Lightning_Bolt')
    marker = cell(load('FX/Magic/Circle/SpriteSheetSpark.png'), 2, 32)
    save(fit(marker, upscale=False), WEAPON, 'D3_Weapon_Lightning_Marker')

    # ---- 보상 아이콘 (24x24) ----
    save(load('Ui/Skill Icon/Spell/Camouflage.png'), ICON, 'Common_Icon_Vine')
    save(icon('Ui/Skill Icon/Job & Action/Plant.png'), ICON, 'Common_Icon_Spirit')
    save(load('Ui/Skill Icon/Job & Action/Harvest.png'), ICON, 'Common_Icon_Root')
    save(icon('Ui/Skill Icon/Spell/Counter.png', saw, 18), ICON, 'Common_Icon_Saw')
    save(load('Ui/Skill Icon/Spell/BookFire.png'), ICON, 'Common_Icon_Flame')
    save(load('Ui/Skill Icon/Spell/Mist.png'), ICON, 'Common_Icon_Tornado')
    save(icon('Ui/Skill Icon/Meteo/Rain.png', bolt, 16), ICON, 'Common_Icon_Lightning')


if __name__ == '__main__':
    main()
