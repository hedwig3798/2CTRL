"""회전 칼날(SpinBlade) / 전격 구체(StormOrb) 이미지 (Ninja Adventure 원본. 잔상 Trail 만 코드로 그린다)
usage: spin_storm.py <out_dir> <pack_root>      out_dir = Assets/Resources/Textuer/Art
산출물 (Weapon/)
- D2_Weapon_SpinBlade_Blade.png  224x32  가로 칼날 (+X 가 끝, 왼쪽이 손잡이). 위쪽(이미지 위 = 로컬 +Y)이 날, 회전 진행 방향
- D2_Weapon_SpinBlade_Trail.png  256x256 칼날 뒤를 따르는 부채꼴 잔상 (로컬 원점 = 플레이어 중심이 좌측 중앙이 되도록 쓴다)
- D3_Weapon_StormOrb_Body.png    32x32   전기 구체
- D3_Weapon_StormOrb_Link.png    32x8    전기선 (가로 반복)
아이콘 (Icon/) Common_Icon_SpinBlade.png, Common_Icon_StormOrb.png (24x24)"""
import sys, os, math
from PIL import Image

OUT, ROOT = sys.argv[1], sys.argv[2]
WEAPON = os.path.join(OUT, 'Weapon')
ICON = os.path.join(OUT, 'Icon')


def trail():
    S = 256
    img = Image.new('RGBA', (S, S))
    px = img.load()
    # 텍스처 중앙 = (128,128). 플레이어 중심은 좌측 중앙 (0,128)에 오도록 쓴다
    # 칼날은 각도 0 (+X), 잔상은 칼날 뒤쪽 = 이미지 아래쪽 (각도 0 ~ -75도)
    bands = [(8, 0.5), (18, 0.36), (32, 0.22), (46, 0.12), (56, 0.06)]
    for y in range(S):
        for x in range(S):
            dx = x + 0.5
            dy = y - 127.5  # 아래(+) = 뒤쪽
            r = math.hypot(dx, dy)
            if r < 40 or r > 238 or dy < 0:
                continue
            ang = math.degrees(math.atan2(dy, dx))
            if ang > bands[-1][0]:
                continue
            a = 0.0
            for lim, al in bands:
                if ang <= lim:
                    a = al
                    break
            # 바깥(칼날 끝 쪽)일수록 진하게, 안쪽은 흐리게
            a *= 0.25 + 0.75 * min(1.0, (r - 40) / 190)
            # 픽셀 디더 (가장자리 띠)
            if (x + y) % 2 == 0 and a < 0.2:
                a *= 0.5
            a = round(a * 4) / 4 if a > 0.1 else 0
            if a <= 0:
                continue
            col = (230, 240, 255) if ang < 24 else (170, 195, 235)
            px[x, y] = (col[0], col[1], col[2], int(255 * min(1.0, a * 1.1)))
    return img


def load(rel):
    return Image.open(os.path.join(ROOT, rel)).convert('RGBA')


def pack_blade():
    """224x32: BigKunai 원본(35x9)을 2배로 세 개 이어 붙인다. 손잡이 왼쪽, 끝 +X"""
    k = load('FX/Projectile/BigKunai.png')
    k = k.resize((k.width * 2, k.height * 2), Image.NEAREST)
    img = Image.new('RGBA', (224, 32))
    for x in (0, 77, 154):
        img.paste(k, (x, (32 - k.height) // 2), k)
    return img


def pack_orb():
    """32x32: Magic/Circle/SpriteSheetSpark2 첫 프레임(번개 고리) 원본"""
    return load('FX/Magic/Circle/SpriteSheetSpark2.png').crop((0, 0, 32, 32))


def pack_link():
    """32x8 가로 반복: Particle/Spark 원본(10x8) 불꽃 세 개"""
    sp = load('FX/Particle/Spark.png')
    img = Image.new('RGBA', (32, 8))
    for i, x in enumerate((0, 11, 22)):
        f = sp.crop(((i + 2) * 10, 0, (i + 2) * 10 + 10, 8))
        img.paste(f, (x, 0), f)
    return img


def main():
    pack_blade().save(os.path.join(WEAPON, 'D2_Weapon_SpinBlade_Blade.png'))
    trail().save(os.path.join(WEAPON, 'D2_Weapon_SpinBlade_Trail.png'))
    pack_orb().save(os.path.join(WEAPON, 'D3_Weapon_StormOrb_Body.png'))
    pack_link().save(os.path.join(WEAPON, 'D3_Weapon_StormOrb_Link.png'))
    # 아이콘은 다른 무기·패시브와 겹치지 않는 원본으로 (Kunai=투사체 수, OrbLight=레이저)
    load('Ui/Skill Icon/Spell/MagicWeapon.png').save(os.path.join(ICON, 'Common_Icon_SpinBlade.png'))
    load('Ui/Skill Icon/Spell/OrbWater.png').save(os.path.join(ICON, 'Common_Icon_StormOrb.png'))


if __name__ == '__main__':
    main()
