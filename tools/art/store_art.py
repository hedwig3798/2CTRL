"""스토브 상점 이미지 생성 (타이틀 아트 재조합)
usage: python tools/art/store_art.py
- docs/store-images/Store_Banner_1920x520.png        : 상품 페이지 배경 (로고 포함)
- docs/store-images/Store_Banner_1920x520_NoLogo.png : 같은 배경, 로고 없음
- docs/store-images/Store_Logo_500x500.png           : 1:1 로고 이미지
모든 그림은 저해상도 캔버스에 그린 뒤 정수배 NEAREST 로 키운다 (픽셀이 고르게 보이도록).
"""
import os
import random
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(PROJ, 'Assets', 'Resources', 'Textuer', 'Art')
TILES = os.path.join(ART, 'TileMap')
OUT = os.path.join(PROJ, 'docs', 'store-images')
os.makedirs(OUT, exist_ok=True)

LINE = (20, 16, 28, 255)
SHEETS = {d: Image.open(os.path.join(TILES, f'{d}_Tile_Ground_Sheet.png')).convert('RGBA') for d in ('D1', 'D2', 'D3')}

# 타이틀 로고(4배 확대본)를 원래 픽셀 단위로 되돌린다
_big_logo = Image.open(os.path.join(ART, 'Title', 'Common_Title_Logo.png')).convert('RGBA')
LOGO = _big_logo.resize((_big_logo.width // 4, _big_logo.height // 4), Image.NEAREST)


def find_actor(name):
    for sub in ('Player', 'Monster'):
        p = os.path.join(ART, sub, name + '.png')
        if os.path.exists(p):
            return p
    raise FileNotFoundError(name)


def actor_sprite(name, scale=2, flip=False):
    s = Image.open(find_actor(name)).convert('RGBA').crop((0, 0, 32, 32))
    b = s.getbbox()
    if b:
        s = s.crop(b)
    s = s.resize((s.width * scale, s.height * scale), Image.NEAREST)
    if flip:
        s = s.transpose(Image.FLIP_LEFT_RIGHT)
    return s


def ground(W, H, cuts, seed):
    """세 차원 바닥을 대각선으로 나눈 캔버스. cuts: 경계 위치(가로 비율 두 개)"""
    rng = random.Random(seed)
    layers = {}
    for dim in ('D1', 'D2', 'D3'):
        layer = Image.new('RGBA', (W, H))
        for y in range(0, H, 32):
            for x in range(0, W, 32):
                idx = rng.choice([0] * 9 + [1, 2, 3])
                t = SHEETS[dim].crop(((idx % 8) * 32, (idx // 8) * 32, (idx % 8) * 32 + 32, (idx // 8) * 32 + 32))
                layer.paste(t, (x, y))
        layers[dim] = layer.load()
    bg = Image.new('RGBA', (W, H))
    bp = bg.load()
    slope = 0.4
    for y in range(H):
        for x in range(W):
            k = x + (H - y) * slope - H * slope / 2
            dim = 'D1' if k < W * cuts[0] else ('D2' if k < W * cuts[1] else 'D3')
            bp[x, y] = layers[dim][x, y]
    d = ImageDraw.Draw(bg)
    for frac in cuts:
        cx = W * frac
        d.line([(cx + H * slope / 2, 0), (cx - H * slope / 2, H)], fill=LINE, width=3)
    return bg


def shade(img, fn):
    """fn(x, y) -> 0~255 알파로 어두운 막을 덮는다"""
    W, H = img.size
    sh = Image.new('RGBA', (W, H))
    sp = sh.load()
    for y in range(H):
        for x in range(W):
            sp[x, y] = (12, 10, 24, int(max(0, min(255, fn(x, y)))))
    return Image.alpha_composite(img, sh)


def place(img, name, x, y, flip=False, scale=2):
    s = actor_sprite(name, scale, flip)
    img.paste(s, (x, y), s)


# ---------------- 1920x520 배경 (480x130 x4) ----------------
def banner():
    W, H = 480, 130
    bg = ground(W, H, (0.36, 0.66), seed=7)
    # 왼쪽 숲: 사냥꾼과 숲 몬스터 / 가운데 공장: 로봇 / 오른쪽 폭풍: 닌자
    for n, x, y, f in (
            ('D1_Monster_Slime_Walk', 18, 88, True),
            ('Common_Monster_Bat_Walk', 40, 14, True),
            ('D1_Monster_Golem_Walk', 100, 76, True),
            ('D1_Player_Hunter_Idle', 58, 50, True),
            ('Common_Monster_Bat_Walk', 132, 26, True),
            ('D2_Monster_Bomber_Walk', 196, 96, True),
            ('D2_Player_RobotGrey_Idle', 226, 86, True),
            ('D2_Monster_Slime_Walk', 270, 100, False),
            ('D2_Monster_Golem_Walk', 330, 18, False),
            ('D3_Monster_Wraith_Walk', 352, 80, False),
            ('D3_Player_NinjaThunder_Idle', 400, 48, False),
            ('D3_Monster_Shaman_Walk', 446, 86, False),
            ('Common_Monster_Bat_Walk', 444, 12, False)):
        place(bg, n, x, y, f)
    # 가운데 로고 자리를 타원으로 어둡게, 위아래 가장자리도 살짝
    cx, cy = W / 2, H * 0.42

    def fn(x, y):
        dx, dy = (x - cx) / 120, (y - cy) / 52
        r = (dx * dx + dy * dy) ** 0.5
        center = 150 * max(0, 1 - r)
        edge = max(0, 40 - y * 2) + max(0, (y - (H - 20)) * 3)
        return center + edge

    bg = shade(bg, fn)
    bg.resize((W * 4, H * 4), Image.NEAREST).save(os.path.join(OUT, 'Store_Banner_1920x520_NoLogo.png'))
    logo = LOGO.resize((LOGO.width * 2, LOGO.height * 2), Image.NEAREST)
    bg.paste(logo, ((W - logo.width) // 2, int(cy - logo.height / 2)), logo)
    bg.resize((W * 4, H * 4), Image.NEAREST).save(os.path.join(OUT, 'Store_Banner_1920x520.png'))


# ---------------- 500x500 로고 (100x100 x5) ----------------
def square():
    # 스토브 1:1 가이드: Safe 500x500 / Main 370x400 (가운데) / LOGO 325x132 (x 87~412, y 308~440)
    # 125x125 를 4배 하면 정확히 500. 로고 66x26 x4 = 264x104 로 LOGO 영역 안에 들어간다 (5배면 330 으로 넘침)
    W = H = 125
    S = 4
    bg = ground(W, H, (0.34, 0.68), seed=11)
    logo_cx, logo_cy = (87 + 412) / 2 / S, (308 + 440) / 2 / S

    def fn(x, y):
        dx, dy = (x - logo_cx) / 46, (y - logo_cy) / 22
        return 160 * max(0, 1 - (dx * dx + dy * dy) ** 0.5) + 30
    bg = shade(bg, fn)
    logo = LOGO
    bg.paste(logo, (round(logo_cx - logo.width / 2), round(logo_cy - logo.height / 2)), logo)
    bg.resize((W * S, H * S), Image.NEAREST).save(os.path.join(OUT, 'Store_Logo_500x500.png'))


# ---------------- 16:9 키 이미지 (757x426, 860x483) ----------------
# 절반 크기 캔버스에 그려 2배 한 뒤 홀수 픽셀만큼 잘라낸다.
# 바닥은 2배, 캐릭터는 스프라이트 3배(최종 6배)·몬스터 2배(최종 4배), 로고 4배(최종 8배)
# 배치 좌표는 379x213 기준으로 적고, 가로는 비율·세로는 아래쪽 기준으로 옮긴다
def wide(out_w, out_h):
    S = 2
    W, H = -(-out_w // S), -(-out_h // S)
    bg = ground(W, H, (0.36, 0.68), seed=5)

    def at(x, y):
        return round(x * W / 379), y + (H - 213) if y > 100 else y

    for n, x, y, f, k in (
            ('Common_Monster_Bat_Walk', 14, 16, True, 2),
            ('Common_Monster_Bat_Walk', 334, 16, False, 2),
            ('D1_Monster_Slime_Walk', 8, 178, True, 2),
            ('D2_Monster_Bomber_Walk', 112, 172, True, 2),
            ('D3_Monster_Wraith_Walk', 236, 172, False, 2),
            ('D3_Monster_Shaman_Walk', 336, 170, False, 2),
            ('D1_Player_Hunter_Idle', 52, 146, True, 3),
            ('D2_Player_RobotGrey_Idle', 166, 152, True, 3),
            ('D3_Player_NinjaThunder_Idle', 282, 146, False, 3)):
        px, py = at(x, y)
        place(bg, n, px, py, f, scale=k)
    logo = LOGO.resize((LOGO.width * 4, LOGO.height * 4), Image.NEAREST)
    lx, ly = (W - logo.width) // 2, 22 + (H - 213) // 3
    cx, cy = W / 2, ly + logo.height / 2

    def fn(x, y):
        dx, dy = (x - cx) / (190 * W / 379), (y - cy) / 80
        return 150 * max(0, 1 - (dx * dx + dy * dy) ** 0.5) + max(0, 30 - y)
    bg = shade(bg, fn)
    bg.paste(logo, (lx, ly), logo)
    big = bg.resize((W * S, H * S), Image.NEAREST)
    mx, my = (W * S - out_w) // 2, (H * S - out_h) // 2
    big.crop((mx, my, mx + out_w, my + out_h)).save(os.path.join(OUT, f'Store_Key_{out_w}x{out_h}.png'))


banner()
square()
wide(757, 426)
wide(860, 483)
print('logo base', LOGO.size)
for f in sorted(os.listdir(OUT)):
    print(f, Image.open(os.path.join(OUT, f)).size)
