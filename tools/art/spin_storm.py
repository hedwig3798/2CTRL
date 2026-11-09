"""회전 칼날(SpinBlade) / 전격 구체(StormOrb) 이미지 (외부 팩 없이 코드로 그린 픽셀 아트)
usage: spin_storm.py <out_dir>      out_dir = Assets/Resources/Textuer/Art
산출물 (Weapon/)
- D2_Weapon_SpinBlade_Blade.png  224x32  가로 칼날 (+X 가 끝, 왼쪽이 손잡이). 위쪽(이미지 위 = 로컬 +Y)이 날, 회전 진행 방향
- D2_Weapon_SpinBlade_Trail.png  256x256 칼날 뒤를 따르는 부채꼴 잔상 (로컬 원점 = 플레이어 중심이 좌측 중앙이 되도록 쓴다)
- D3_Weapon_StormOrb_Body.png    32x32   전기 구체
- D3_Weapon_StormOrb_Link.png    32x8    전기선 (가로 반복)
아이콘 (Icon/) Common_Icon_SpinBlade.png, Common_Icon_StormOrb.png (24x24)"""
import sys, os, math, random
from PIL import Image

OUT = sys.argv[1]
WEAPON = os.path.join(OUT, 'Weapon')
ICON = os.path.join(OUT, 'Icon')
OL = (20, 16, 28, 255)


def outline(img, col=OL):
    out = img.copy()
    src = img.load()
    px = out.load()
    for y in range(img.height):
        for x in range(img.width):
            if src[x, y][3] != 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < img.width and 0 <= ny < img.height and src[nx, ny][3] > 128:
                    px[x, y] = col
                    break
    return out


def blade():
    W, H = 224, 32
    img = Image.new('RGBA', (W, H))
    px = img.load()
    cy = 15
    # 색
    steel_edge = (245, 250, 255, 255)
    steel_hi = (205, 216, 232, 255)
    steel_mid = (150, 164, 186, 255)
    steel_lo = (98, 110, 132, 255)
    steel_dk = (64, 72, 94, 255)
    gold = (226, 176, 70, 255)
    gold_dk = (150, 100, 40, 255)
    grip = (110, 44, 52, 255)
    grip_dk = (74, 28, 38, 255)

    # 칼날 몸통: x 30..222
    bx0, bx1 = 30, 222
    for x in range(bx0, bx1 + 1):
        t = (x - bx0) / (bx1 - bx0)
        # 위(날) 끝선: 빠르게 올라오는 어깨 후 끝으로 모인다
        top = cy - 9 * (1 - max(0.0, t - 0.08) / 0.92) ** 0.9 if t > 0.08 else cy - 9 * (t / 0.08) * 0.4 - 6
        top = cy - 9.5 * (1 - t) ** 0.62 - 0.5 * (1 - t)
        bot = cy + 8.5 * (1 - t) ** 0.62 + 0.5 * (1 - t)
        # 등(아래)에 뒤쪽으로 누운 톱니
        if 0.18 < t < 0.9:
            ph = (x - bx0) % 11
            bot += max(0, 3.2 * (1 - t) - 0.4) * (ph / 11.0)
        for y in range(int(math.floor(top)), int(math.ceil(bot)) + 1):
            if y < 0 or y >= H:
                continue
            if y < top - 0.5 or y > bot + 0.5:
                continue
            u = (y - top) / max(1e-6, bot - top)  # 0 = 날, 1 = 등
            if u < 0.12:
                c = steel_edge
            elif u < 0.38:
                c = steel_hi
            elif u < 0.55:
                c = steel_mid
            elif u < 0.78:
                c = steel_lo
            else:
                c = steel_dk
            px[x, y] = c
        # 홈(fuller)
        if 0.06 < t < 0.78:
            fy = int(round(cy + 1.5 * (1 - t)))
            if (x // 2) % 8 != 7:
                px[x, fy] = steel_dk if px[x, fy][3] else px[x, fy]
    # 손잡이 / 가드 / 마구리
    for x in range(6, 24):
        for y in range(cy - 3, cy + 4):
            px[x, y] = grip if (x // 3) % 2 == 0 else grip_dk
    for y in range(cy - 3, cy - 2):
        for x in range(6, 24):
            px[x, y] = gold_dk
    # 가드: 양끝이 앞으로 휜 십자
    for x in range(24, 30):
        for y in range(cy - 13, cy + 14):
            if abs(y - cy) > 12 - (x - 24) * 0.5:
                continue
            px[x, y] = gold if (y + x) % 7 else gold_dk
    for k in range(3):
        px[30 + k, cy - 13 - k // 2] = gold
        px[30 + k, cy + 13 + k // 2] = gold
    # 마구리
    cx0, cy0 = 3, cy
    for y in range(cy0 - 4, cy0 + 5):
        for x in range(0, 8):
            if (x - cx0) ** 2 + (y - cy0) ** 2 <= 14:
                px[x, y] = gold if x + y < cx0 + cy0 + 1 else gold_dk
    img = outline(img)
    return img


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


def orb():
    S = 32
    img = Image.new('RGBA', (S, S))
    px = img.load()
    c = 15.5
    rnd = random.Random(7)
    # 스파이크 (지그재그 전기)
    for k in range(6):
        ang = math.radians(k * 60 + 15)
        x, y = c, c
        for step in range(7):
            ang += math.radians(rnd.choice([-35, 35, -20, 20]))
            for s in range(2):
                x += math.cos(ang)
                y += math.sin(ang)
                if step >= 2:
                    ix, iy = int(round(x)), int(round(y))
                    if 0 <= ix < S and 0 <= iy < S:
                        px[ix, iy] = (190, 225, 255, 255)
    # 구체
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c)
            if d <= 7.2:
                if d < 2.6:
                    col = (255, 255, 255, 255)
                elif d < 4.4:
                    col = (205, 236, 255, 255)
                elif d < 6.0:
                    col = (110, 172, 255, 255)
                else:
                    col = (64, 92, 232, 255)
                px[x, y] = col
    img = outline(img, (28, 24, 96, 255))
    return img


def link():
    W, H = 32, 8
    img = Image.new('RGBA', (W, H))
    px = img.load()
    offs = [0, -1, -2, -1, 0, 1, 2, 1]
    for x in range(W):
        y = 3 + offs[x % 8]
        for dy, col in ((-2, (80, 100, 255, 110)), (-1, (130, 160, 255, 210)), (0, (255, 255, 255, 255)),
                        (1, (190, 215, 255, 255)), (2, (130, 160, 255, 210)), (3, (80, 100, 255, 110))):
            yy = y + dy
            if 0 <= yy < H:
                if px[x, yy][3] < col[3]:
                    px[x, yy] = col
    return img


def icon_base(bg, light, dark):
    img = Image.new('RGBA', (24, 24), bg + (255,))
    px = img.load()
    for (x, y) in ((0, 0), (23, 0), (0, 23), (23, 23)):
        px[x, y] = (96, 96, 112, 255)
    rnd = random.Random(3)
    for _ in range(9):
        x, y = rnd.randrange(1, 23), rnd.randrange(1, 23)
        px[x, y] = light + (255,)
    for _ in range(5):
        x, y = rnd.randrange(1, 23), rnd.randrange(1, 23)
        px[x, y] = dark + (255,)
    return img


def paste_fit(base, o, size):
    b = o.getbbox()
    o = o.crop(b)
    r = min(size / o.width, size / o.height)
    o = o.resize((max(1, round(o.width * r)), max(1, round(o.height * r))), Image.NEAREST)
    base.paste(o, ((base.width - o.width) // 2, (base.height - o.height) // 2), o)


def main():
    b = blade()
    b.save(os.path.join(WEAPON, 'D2_Weapon_SpinBlade_Blade.png'))
    trail().save(os.path.join(WEAPON, 'D2_Weapon_SpinBlade_Trail.png'))
    o = orb()
    o.save(os.path.join(WEAPON, 'D3_Weapon_StormOrb_Body.png'))
    link().save(os.path.join(WEAPON, 'D3_Weapon_StormOrb_Link.png'))

    # 칼날 아이콘: 대각선으로 눕힌 칼날
    ic = icon_base((192, 84, 84), (240, 150, 140), (150, 56, 66))
    diag = b.crop(b.getbbox()).rotate(45, expand=True, resample=Image.NEAREST)
    paste_fit(ic, outline(diag), 22)
    ic.save(os.path.join(ICON, 'Common_Icon_SpinBlade.png'))
    ic2 = icon_base((92, 104, 224), (190, 210, 255), (60, 66, 176))
    paste_fit(ic2, o, 22)
    ic2.save(os.path.join(ICON, 'Common_Icon_StormOrb.png'))


if __name__ == '__main__':
    main()
