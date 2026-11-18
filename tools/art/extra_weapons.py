"""차원별 추가 무기 7종 이미지 (Ninja Adventure 원본만 사용)
usage: extra_weapons.py <pack_root> <out_dir>
- out_dir: Assets/Resources/Textuer/Art (weapon/ → Weapon, icon/ → Icon)"""
import sys, os
from PIL import Image

ROOT, OUT = sys.argv[1:3]
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


def thicket(frames, seed_order):
    """풀 덤불 장판 (32x32): Particle 원본 프레임을 고리 6개 + 가운데 1개로 겹친다"""
    img = Image.new('RGBA', (32, 32))
    spots = ((0, -9), (8, -4), (8, 5), (0, 9), (-8, 5), (-8, -4), (0, 0))
    for k, (dx, dy) in enumerate(spots):
        f = frames[seed_order[k % len(seed_order)]]
        img.paste(f, (16 + dx - f.width // 2, 16 + dy - f.height // 2), f)
    return img


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
    grass = load('FX/Particle/Grass.png')
    blades = [bbox_crop(cell(grass, i, 12)) for i in range(6)]
    save(thicket(blades, [1, 5, 3, 1, 5, 3, 4]), WEAPON, 'D1_Weapon_Vine_Segment')
    thorn = cell(load('FX/Projectile/PlantSpike.png'), 3, 24)
    save(fit(thorn, upscale=False), WEAPON, 'D1_Weapon_Vine_Thorn')
    spirit = cell(load('FX/Magic/Spirit/SpriteSheet.png'), 2, 32)
    save(fit(outline(bbox_crop(spirit)), upscale=False), WEAPON, 'D1_Weapon_Spirit_Body')
    # 뿌리 장판 (현재 미사용): 덩굴과 같은 방식, 나뭇잎 원본
    leaf = load('FX/Particle/Leaf.png')
    leaves = [bbox_crop(cell(leaf, i, 12)) for i in range(6)]
    save(thicket(leaves, [0, 2, 4, 1, 3, 5, 2]), WEAPON, 'D1_Weapon_Root_Area')

    # ---- 공장 ----
    saw = cell(load('FX/Projectile/Shuriken.png'), 1, 16)
    save(fit(saw), WEAPON, 'D2_Weapon_Saw_Blade')
    flame = cell(load('FX/Elemental/Flam/SpriteSheet.png'), 2, 40)
    save(fit(flame, upscale=False), WEAPON, 'D2_Weapon_Flame_Fire')

    # ---- 폭풍 ----
    # 회오리: 원형 베기 시트의 C 자 호(x 128~188) 원본과 그 180도 회전본을 겹쳐 소용돌이 (64x64, 빌더가 0.8 배로 써서 1.6 유닛)
    arc = load('FX/Slash/SpriteSheetCircular.png').crop((128, 0, 189, 55))
    whirl = Image.new('RGBA', (64, 64))
    whirl.paste(arc, (2, 4), arc)
    whirl = Image.alpha_composite(whirl.rotate(180), whirl)
    save(whirl, WEAPON, 'D3_Weapon_Tornado_Body')
    bolt = cell(load('FX/Elemental/Thunder/SpriteSheet.png'), 4, 20)
    save(fit(bolt), WEAPON, 'D3_Weapon_Lightning_Bolt')
    marker = cell(load('FX/Magic/Circle/SpriteSheetSpark.png'), 2, 32)
    save(fit(marker, upscale=False), WEAPON, 'D3_Weapon_Lightning_Marker')

    # ---- 보상 아이콘 (24x24) ----
    save(load('Ui/Skill Icon/Spell/Camouflage.png'), ICON, 'Common_Icon_Vine')
    save(icon('Ui/Skill Icon/Job & Action/Plant.png'), ICON, 'Common_Icon_Spirit')
    save(load('Ui/Skill Icon/Job & Action/Harvest.png'), ICON, 'Common_Icon_Root')
    # 아이콘은 다른 무기·패시브와 겹치지 않는 원본으로 (Shuriken=부메랑, BookThunder=체인)
    save(load('Ui/Skill Icon/Items & Weapon/Guard.png'), ICON, 'Common_Icon_Saw')
    save(load('Ui/Skill Icon/Spell/BookFire.png'), ICON, 'Common_Icon_Flame')
    save(load('Ui/Skill Icon/Spell/Mist.png'), ICON, 'Common_Icon_Tornado')
    save(load('Ui/Skill Icon/Meteo/Rain.png'), ICON, 'Common_Icon_Lightning')


if __name__ == '__main__':
    main()
