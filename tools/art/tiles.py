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


# 폭풍: 풀(A) → 짙은 보라 암반, 흙(B) → 빗물 웅덩이
def storm(h, s, v):
    if is_green(h):
        return hsv(0.70, 0.30, v * 0.48)
    return hsv(0.60, 0.45, 0.20 + v * 0.30)


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


# 공장: Interior/TilesetInteriorFloor 의 회녹색 판재 블록 (11,6) 부터. 색 보정 없이 원본 그대로
# A = 리벳 판재 2x2 반복, B = 테두리 패널(3x3 블록). 안쪽 모서리 그림이 없어 패널 가운데 칸으로 채운다
INTERIOR = os.path.join(os.path.dirname(SRC), 'Interior', 'TilesetInteriorFloor.png')
PLATE_P = dict(P, iBR=(1, 1), iBL=(1, 1), iTR=(1, 1), iTL=(1, 1))
PLATE_B_FILLS = [(1, 1), (0, 4), (3, 5)]

DIMS = {
    'D1': dict(origin=(0, 7), fn=None, a_tile=None),
    'D2': dict(src=INTERIOR, origin=(11, 6), fn=None, a_tile='plate', pieces=PLATE_P, b_fills=PLATE_B_FILLS),
    'D3': dict(origin=(0, 7), fn=storm, a_tile='storm'),
}


def piece(block, cr):
    c, r = cr
    return block.crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16))


def plate_a(variant, block):
    """32px 리벳 판재 (블록 (9,2)~(10,3) 2x2). variant 2 만 작은 패널 2x2 (금·문양 칸은 바탕색이 달라 얼룩져 보여 뺐다)"""
    t = Image.new('RGBA', (32, 32))
    for q, cr in enumerate([(9, 2), (10, 2), (9, 3), (10, 3)]):
        t.paste(piece(block, (3, 3) if variant == 2 else cr), ((q % 2) * 16, (q // 2) * 16))
    return t


def build(dim, cfg):
    ox, oy = cfg['origin']
    src = Image.open(cfg['src']).convert('RGBA') if 'src' in cfg else sheet
    block = src.crop((ox * 16, oy * 16, ox * 16 + 11 * 16, oy * 16 + 7 * 16))
    if cfg['fn']:
        block = recolor(block, cfg['fn'])
    pieces = cfg.get('pieces', P)
    b_fills = cfg.get('b_fills', B_FILLS)
    rng = random.Random(dim)
    # A 채움 32px 타일 (A 칸 4개가 모두 같은 타일의 1/4 을 쓴다)
    a_base = Image.new('RGBA', (32, 32))
    for q in range(4):
        a_base.paste(piece(block, (0, 5)), ((q % 2) * 16, (q // 2) * 16))
    a_tiles = []
    for v in range(4):
        if cfg['a_tile'] == 'plate':
            a_tiles.append(plate_a(v, block))
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
                cr = (1, 1) if name == 'B0' or q != 3 else b_fills[1 + (i % 2)]
            else:
                cr = pieces[kind]
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
