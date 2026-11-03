"""Ninja Adventure TilesetFloor 의 16px 오토타일 블록으로 32px WFC 타일 시트를 만든다.
usage: tiles.py <TilesetFloor.png> <out_dir> [preview_dir]
타일 순서/소켓은 tiles_layout 과 Unity 빌더(ArtTileBuilder)가 공유한다."""
import sys, os, colorsys, random
from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]
PREVIEW = sys.argv[3] if len(sys.argv) > 3 else None
os.makedirs(OUT, exist_ok=True)
sheet = Image.open(SRC).convert('RGBA')

# 블록 안 상대 좌표 (16px 칸)
P = dict(TL=(0, 0), T=(1, 0), TR=(2, 0), L=(0, 1), C=(1, 1), R=(2, 1), BL=(0, 2), B=(1, 2), BR=(2, 2),
         iBR=(5, 1), iBL=(6, 1), iTR=(5, 2), iTL=(6, 2))
A_FILLS = [(0, 5), (0, 5), (0, 5), (1, 5), (2, 5), (3, 5), (4, 5), (2, 4), (3, 4)]
B_FILLS = [(1, 1), (0, 4), (1, 4)]

# (이름, [TL, TR, BL, BR]) : 'A' = A 채움, 'b' = B 중앙, 그 외 = 조각 이름
SHAPES = [
    ('A0', ['A', 'A', 'A', 'A']), ('A1', ['A', 'A', 'A', 'A']), ('A2', ['A', 'A', 'A', 'A']), ('A3', ['A', 'A', 'A', 'A']),
    ('B0', ['b', 'b', 'b', 'b']), ('B1', ['b', 'b', 'b', 'b']), ('B2', ['b', 'b', 'b', 'b']),
    ('OuterTL', ['A', 'A', 'A', 'TL']), ('EdgeT', ['A', 'A', 'T', 'T']), ('OuterTR', ['A', 'A', 'TR', 'A']),
    ('EdgeL', ['A', 'L', 'A', 'L']), ('EdgeR', ['R', 'A', 'R', 'A']),
    ('OuterBL', ['A', 'BL', 'A', 'A']), ('EdgeB', ['B', 'B', 'A', 'A']), ('OuterBR', ['BR', 'A', 'A', 'A']),
    ('InnerTL', ['A', 'L', 'T', 'iTL']), ('InnerTR', ['R', 'A', 'iTR', 'T']),
    ('InnerBL', ['B', 'iBL', 'A', 'L']), ('InnerBR', ['iBR', 'B', 'R', 'A']),
]


def recolor(img, fn):
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            nr, ng, nb = fn(h, s, v)
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


def hsv(h, s, v):
    return colorsys.hsv_to_rgb(h % 1.0, max(0, min(1, s)), max(0, min(1, v)))


def is_green(h):
    return 0.12 < h < 0.45


PANEL = (58, 66, 82)


# 공장/사이버: 풀(A) → 강철 패널 색, 흙(B) → 부서진 바닥 아래 녹슨 흙
def factory(h, s, v):
    if is_green(h):
        k = v / 0.75
        return tuple(min(1, c / 255 * k) for c in PANEL)
    return hsv(0.06, 0.30 + s * 0.2, 0.18 + v * 0.32)


# 폭풍: 풀(A) → 짙은 보라 암반, 흙(B) → 빗물 웅덩이
def storm(h, s, v):
    if is_green(h):
        return hsv(0.70, 0.30, v * 0.48)
    return hsv(0.60, 0.45, 0.20 + v * 0.30)


def factory_a(variant, rng):
    """32px 강철 패널. variant: 0 기본, 1 환풍구, 2 경고 줄무늬, 3 청록 조명"""
    t = Image.new('RGBA', (32, 32), PANEL + (255,))
    px = t.load()
    dark, light, rivet = (36, 40, 52, 255), (78, 88, 106, 255), (104, 114, 132, 255)
    for i in range(32):
        px[i, 31] = dark
        px[31, i] = dark
        px[i, 0] = light
        px[0, i] = light
    for x, y in [(3, 3), (28, 3), (3, 28), (28, 28)]:
        px[x, y] = rivet
        px[x + 1, y + 1] = dark
    if variant == 1:
        for y in range(10, 22, 3):
            for x in range(8, 24):
                px[x, y] = dark
                px[x, y + 1] = (46, 52, 66, 255)
    elif variant == 2:
        for y in range(12, 20):
            for x in range(4, 28):
                px[x, y] = (230, 186, 60, 255) if ((x + y) // 3) % 2 == 0 else (40, 38, 44, 255)
    elif variant == 3:
        for x in range(6, 26):
            px[x, 15] = (90, 220, 240, 255)
            px[x, 16] = (60, 170, 200, 255)
        px[5, 15] = px[26, 15] = (50, 120, 150, 255)
    return t


def storm_a(variant, rng, base):
    """기본 바위(base) 위에 번개 그을음 (variant 2, 3)"""
    t = base.copy()
    px = t.load()
    # 잔돌·금 (모든 변형)
    for _ in range(3 if variant == 0 else 6):
        x, y = rng.randrange(2, 30), rng.randrange(2, 30)
        r, g, b, a = px[x, y]
        px[x, y] = (int(r * 0.7), int(g * 0.7), int(b * 0.75), a)
        px[x + 1, y] = (min(255, r + 18), min(255, g + 18), min(255, b + 22), a)
    if variant == 1:
        x, y = 6, 20
        for _ in range(18):
            r, g, b, a = px[x, y]
            px[x, y] = (int(r * 0.6), int(g * 0.6), int(b * 0.65), a)
            x += 1
            y += rng.choice([-1, 0, 0, 1])
            y = max(1, min(30, y))
    if variant < 2:
        return t
    cx, cy = 16, 16
    for y in range(32):
        for x in range(32):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5 + rng.random() * 2.5
            if d < 8:
                r, g, b, a = px[x, y]
                k = 0.55 if d < 5.5 else 0.75
                px[x, y] = (int(r * k), int(g * k), int(b * (k + 0.05)), a)
    # 지그재그 균열
    x, y = 14 + (variant - 2) * 3, 11
    for _ in range(10):
        px[x, y] = (250, 230, 110, 255)
        if 0 < x < 31:
            px[x + 1, y] = (180, 140, 60, 255)
        y += 1
        x += rng.choice([-1, 0, 1, 1])
        x = max(1, min(30, x))
    return t


DIMS = {
    'D1': dict(origin=(0, 7), fn=None, a_tile=None),
    'D2': dict(origin=(0, 7), fn=factory, a_tile='factory'),
    'D3': dict(origin=(0, 7), fn=storm, a_tile='storm'),
}


def piece(block, cr):
    c, r = cr
    return block.crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16))


def build(dim, cfg):
    ox, oy = cfg['origin']
    block = sheet.crop((ox * 16, oy * 16, ox * 16 + 11 * 16, oy * 16 + 7 * 16))
    if cfg['fn']:
        block = recolor(block, cfg['fn'])
    rng = random.Random(dim)
    # A 채움 32px 타일 (A 칸 4개가 모두 같은 타일의 1/4 을 쓴다)
    a_base = Image.new('RGBA', (32, 32))
    for q in range(4):
        a_base.paste(piece(block, (0, 5)), ((q % 2) * 16, (q // 2) * 16))
    a_tiles = []
    for v in range(4):
        if cfg['a_tile'] == 'factory':
            a_tiles.append(factory_a(v, rng))
        elif cfg['a_tile'] == 'storm':
            a_tiles.append(storm_a(v, rng, a_base))
        else:
            a_tiles.append(None)
    tiles = []
    for i, (name, quads) in enumerate(SHAPES):
        t = Image.new('RGBA', (32, 32))
        for q, kind in enumerate(quads):
            pos = ((q % 2) * 16, (q // 2) * 16)
            if kind == 'A':
                variant = int(name[1]) if name[0] == 'A' else 0
                if a_tiles[variant] is not None:
                    t.paste(a_tiles[variant].crop((pos[0], pos[1], pos[0] + 16, pos[1] + 16)), pos)
                    continue
                cr = (0, 5) if name == 'A0' or name[0] != 'A' else rng.choice(A_FILLS)
            elif kind == 'b':
                cr = (1, 1) if name == 'B0' or q != 3 else B_FILLS[1 + (i % 2)]
            else:
                cr = P[kind]
            t.paste(piece(block, cr), pos)
        tiles.append(t)
    cols = 8
    rows = (len(tiles) + cols - 1) // cols
    out = Image.new('RGBA', (cols * 32, rows * 32))
    for i, t in enumerate(tiles):
        out.paste(t, ((i % cols) * 32, (i // cols) * 32))
    path = os.path.join(OUT, f'{dim}_Tile_Ground_Sheet.png')
    out.save(path)
    print(path, out.size, len(tiles))
    return tiles


if __name__ == '__main__':
    previews = []
    for d, c in DIMS.items():
        tiles = build(d, c)
        if PREVIEW:
            # 미리보기: 가운데에 B 영역 하나 (외곽·안쪽 모서리 확인)
            m = [[0] * 10 for _ in range(7)]
            # L 자 모양: 안쪽 모서리 InnerBR(18) 포함
            layout = {(2, 1): 7, (3, 1): 8, (4, 1): 8, (5, 1): 8, (6, 1): 9,
                      (2, 2): 10, (3, 2): 4, (4, 2): 5, (5, 2): 6, (6, 2): 11,
                      (2, 3): 10, (3, 3): 4, (4, 3): 18, (5, 3): 13, (6, 3): 14,
                      (2, 4): 10, (3, 4): 4, (4, 4): 11,
                      (2, 5): 12, (3, 5): 13, (4, 5): 14}
            img = Image.new('RGBA', (10 * 32, 7 * 32))
            rng = random.Random(1)
            for y in range(7):
                for x in range(10):
                    k = layout.get((x, y), rng.choice([0, 0, 0, 0, 1, 2, 3]))
                    img.paste(tiles[k], (x * 32, y * 32))
            previews.append(img.resize((img.width * 2, img.height * 2), Image.NEAREST))
    if PREVIEW:
        o = Image.new('RGBA', (previews[0].width, sum(p.height + 8 for p in previews)), (0, 0, 0, 255))
        y = 0
        for p in previews:
            o.paste(p, (0, y))
            y += p.height + 8
        o.save(os.path.join(PREVIEW, 'tiles_preview.png'))
