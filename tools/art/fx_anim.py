"""프레임 애니메이션 이펙트 (Ninja Adventure 원본 시트를 그대로 가로 스트립으로)
usage: fx_anim.py <pack_root> <art_dir> [preview.png]      art_dir = Assets/Resources/Textuer/Art
- FX/ 아래 스트립은 Unity 빌더가 칸 = 이미지 높이로 자른다
- Common_FX_Explosion_Anim      폭발 9프레임 (40px)          폭발탄·지뢰·자폭 몬스터 폭발
- D3_Weapon_Lightning_Marker_Anim  마법진 Spark 6프레임 (32px)  낙뢰 예고
- D1_Weapon_PoisonZone_Puff     연기 6프레임 (32px) 초록      독 장판 구름
- Weapon/D1_Weapon_PoisonZone_Flask  WaterPot 물약 액체를 초록으로   독 장판 던지는 병"""
import sys, os, colorsys
from PIL import Image

ROOT, ART = sys.argv[1], sys.argv[2]
PREVIEW = sys.argv[3] if len(sys.argv) > 3 else None
FX = os.path.join(ART, 'FX')
os.makedirs(FX, exist_ok=True)


def load(rel):
    return Image.open(os.path.join(ROOT, rel)).convert('RGBA')


def hue_map(img, fn):
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            nr, ng, nb = colorsys.hsv_to_rgb(*fn(h, s, v))
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


def smoke_green(h, s, v):
    # 회청색 연기 → 독 초록. 밝은 칸은 연두, 어두운 칸은 짙은 초록
    return 0.27 + (1 - v) * 0.06, min(1, 0.45 + s * 1.2), min(1, v * 1.05)


def pot_green(h, s, v):
    # 하늘색 액체만 초록 (병 테두리·뚜껑 색은 그대로)
    if 0.45 < h < 0.62 and s > 0.15:
        return 0.30, min(1, s * 1.4), v
    return h, s, v


strips = {
    'Common_FX_Explosion_Anim': load('FX/Elemental/Explosion/SpriteSheet.png'),
    'D3_Weapon_Lightning_Marker_Anim': load('FX/Magic/Circle/SpriteSheetSpark.png'),
    'D1_Weapon_PoisonZone_Puff': hue_map(load('FX/Smoke/Smoke/SpriteSheet.png'), smoke_green),
}


def frames(rel, fw):
    im = load(rel)
    return [im.crop((i * fw, 0, i * fw + fw, im.height)) for i in range(im.width // fw)]


def strip(cells, size):
    out = Image.new('RGBA', (size * len(cells), size))
    for i, c in enumerate(cells):
        out.paste(c, (i * size, 0), c)
    return out


# 휘두르기 궤적: Slash02 6 프레임 (66x50). 초승달의 곡률 중심 (33,17) 을 68x68 칸 가운데에 둔다
# -> 피벗(칸 중심) = 휘두르는 중심(플레이어), 아래쪽이 조준 방향, 왼쪽 -> 오른쪽(반시계)으로 그어진다. 반지름 약 34px
SWEEP_CELL = 68
sweep_cells = []
for f in frames('FX/Slash/SpriteSheetSlash02.png', 66):
    c = Image.new('RGBA', (SWEEP_CELL, SWEEP_CELL))
    c.paste(f, (SWEEP_CELL // 2 - 33, SWEEP_CELL // 2 - 17), f)
    sweep_cells.append(c)


def squeeze_arc(cell, from_deg, to_deg):
    """칸 중심 기준 극좌표로 아래쪽을 가운데로 한 from_deg 폭의 호를 to_deg 폭으로 좁힌다 (반지름은 그대로, 최근접 샘플)"""
    import math
    out = Image.new('RGBA', cell.size)
    src = cell.load()
    dst = out.load()
    cx = cy = cell.width / 2 - 0.5
    k = from_deg / to_deg
    for y in range(cell.height):
        for x in range(cell.width):
            dx, dy = x - cx, y - cy
            r = math.hypot(dx, dy)
            phi = math.degrees(math.atan2(dx, dy))  # 0 = 아래, + = 오른쪽
            ps = phi * k
            if abs(ps) > 180:
                continue
            sx = int(round(cx + r * math.sin(math.radians(ps))))
            sy = int(round(cy + r * math.cos(math.radians(ps))))
            if 0 <= sx < cell.width and 0 <= sy < cell.height:
                dst[x, y] = src[sx, sy]
    return out


# 원본 초승달은 약 180 도로 그어지는데 실제 휘두르기 판정은 120 도(SweepMovement.arcAngle) 라 그림이 판정보다 넓어 보인다 -> 120 도로 좁힌다
def hollow(cell, inner):
    """칸 중심에서 inner px 안쪽을 지운다 (바깥 테두리 = 칼끝 쪽만 남김)"""
    out = cell.copy()
    px = out.load()
    c = cell.width / 2 - 0.5
    for y in range(cell.height):
        for x in range(cell.width):
            if (x - c) ** 2 + (y - c) ** 2 < inner * inner:
                px[x, y] = (0, 0, 0, 0)
    return out


# 칼날 끝 거리(Reach)가 강화로 4 유닛 넘게 커지면 두꺼운 초승달이 화면을 덮는다
# -> 바깥 반지름(34px = Reach)은 그대로 두고 안쪽 22px 를 비워 띠 두께를 절반쯤으로
strips['D1_Weapon_Sweep_Trail'] = strip([hollow(squeeze_arc(c, 180, 120), 22) for c in sweep_cells], SWEEP_CELL)

# 검기(★ 10 강): Arc 6 프레임 (38x34) 초승달을 반시계 90 도 돌려 볼록한 쪽이 +x (날아가는 방향) 가 되게 38x38 칸에
wave_cells = []
for f in frames('FX/Slash/SpriteSheetArc.png', 38):
    r = f.rotate(90, expand=True)
    c = Image.new('RGBA', (38, 38))
    c.paste(r, ((38 - r.width) // 2, (38 - r.height) // 2), r)
    wave_cells.append(c)
strips['D1_Weapon_Sweep_SlashWave'] = strip(wave_cells, 38)

# 화염 방사 불덩이: Flam 8 프레임 (25x30) 을 30x30 칸 가운데. 분사 입자 하나가 날아가며 한 번 재생한다
flam_cells = []
for f in frames('FX/Elemental/Flam/SpriteSheet.png', 25):
    c = Image.new('RGBA', (30, 30))
    c.paste(f, (2, 0), f)
    flam_cells.append(c)
strips['D2_Weapon_Flame_Puff'] = strip(flam_cells, 30)

# 폭풍 소용돌이 (구 회오리): QuickSand 8 프레임 나선을 폭풍 청색으로. 어두운 줄 = 소용돌이 팔(남보라), 밝은 바탕 = 하늘색
def storm_vortex(img):
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            v = (r * 0.3 + g * 0.59 + b * 0.11) / 255
            t = max(0.0, min(1.0, (v - 0.55) * 4 + 0.5))
            h = 0.72 + (0.58 - 0.72) * t
            nr, ng, nb = colorsys.hsv_to_rgb(h, 0.55 - 0.25 * t, 0.35 + 0.55 * t)
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


vortex = storm_vortex(load('Backgrounds/Animated/QuickSand/QuickSand32x32.png'))
strips['D3_Weapon_Vortex_Floor'] = vortex
# 빨려드는 파편: 바람에 구르는 잎 6 프레임 (12x7) 을 12x12 칸 가운데
leaf_cells = []
for f in frames('FX/Particle/Leaf.png', 12):
    c = Image.new('RGBA', (12, 12))
    c.paste(f, (0, 2), f)
    leaf_cells.append(c)
strips['D3_Weapon_Vortex_Debris'] = strip(leaf_cells, 12)
# 타격 번개: Thunder 7 프레임 (20x28, 검은 3 번 제외) 을 28x28 칸 가운데
zap_cells = []
for i, f in enumerate(frames('FX/Elemental/Thunder/SpriteSheet.png', 20)):
    if 3 == i:
        continue  # 검은 그림자 프레임은 뺀다
    c = Image.new('RGBA', (28, 28))
    c.paste(f, (4, 0), f)
    zap_cells.append(c)
strips['D3_Weapon_Vortex_Zap'] = strip(zap_cells, 28)

# 정령: Actor/Monster/Spirit2 (주황 불꽃 정령, 16px 4 방향 x 4 프레임) 를 숲 초록으로 칠하고 2 배 확대
# (Spirit 청색은 3 차원 적 Wraith 가 쓰므로 아군 정령은 색을 달리한다)
# 시트는 열 = 방향(아래, 위, 왼쪽, 오른쪽), 행 = 프레임. 출력 순서 = 방향 * 4 + 프레임
def spirit_green(h, s, v):
    if s < 0.25 or v < 0.3:
        return h, s, v  # 검은 외곽선·눈은 그대로
    if h > 0.5:
        # 붉은 테두리(0.9~1.0) -> 짙은 초록 (몸통과 구분되게 어둡게)
        return 0.33, min(1.0, s), v * 0.55
    return 0.30 + h * 0.4, min(1.0, s * 0.9), v


spirit_sheet = hue_map(load('Actor/Monster/Spirit2/SpriteSheet.png'), spirit_green)
spirit_cells = []
for col in range(4):
    for row in range(4):
        f = spirit_sheet.crop((col * 16, row * 16, col * 16 + 16, row * 16 + 16))
        spirit_cells.append(f.resize((32, 32), Image.NEAREST))
strips['D1_Weapon_Spirit_Walk'] = strip(spirit_cells, 32)

# 덩굴: Particle/Grass 풀잎 원본 6 장으로 만든 덤불 (extra_weapons.thicket 과 같은 배치) 을 프레임으로
# 0~6 = 풀잎이 하나씩 돋아남, 7~10 = 풀잎마다 엇갈려 1px 흔들림 반복, 11~13 = 풀잎이 빠지며 사라짐
grass = load('FX/Particle/Grass.png')
blades = []
for i in range(6):
    g = grass.crop((i * 12, 0, i * 12 + 12, grass.height))
    blades.append(g.crop(g.getbbox()))
SPOTS = ((0, -9), (8, -4), (8, 5), (0, 9), (-8, 5), (-8, -4), (0, 0))
ORDER = [1, 5, 3, 1, 5, 3, 4]
SWAY = [0, 1, 0, -1]


def thicket_frame(count, phase=None, keep=None):
    img = Image.new('RGBA', (32, 32))
    for k, (dx, dy) in enumerate(SPOTS):
        if k >= count or (keep is not None and k not in keep):
            continue
        f = blades[ORDER[k]]
        sx = SWAY[(phase + k) % 4] if phase is not None else 0
        img.paste(f, (16 + dx + sx - f.width // 2, 16 + dy - f.height // 2), f)
    return img


vine_cells = [thicket_frame(n) for n in range(1, 8)]
vine_cells += [thicket_frame(7, phase=p) for p in range(4)]
vine_cells += [thicket_frame(7, keep=k) for k in ({0, 2, 3, 4, 6}, {3, 6}, {6})]
strips['D1_Weapon_Vine_Thicket'] = strip(vine_cells, 32)

for name, img in strips.items():
    img.save(os.path.join(FX, name + '.png'))
    print(name, img.size, img.width // img.height)

pot = hue_map(load('Items/Potion/WaterPot.png'), pot_green)
pot = pot.resize((pot.width * 2, pot.height * 2), Image.NEAREST)
flask = Image.new('RGBA', (32, 32))
flask.paste(pot, ((32 - pot.width) // 2, (32 - pot.height) // 2), pot)
flask.save(os.path.join(ART, 'Weapon', 'D1_Weapon_PoisonZone_Flask.png'))

if PREVIEW:
    imgs = list(strips.values()) + [flask]
    w = max(i.width for i in imgs) * 3
    o = Image.new('RGBA', (w, sum(i.height * 3 + 6 for i in imgs)), (60, 80, 60, 255))
    y = 0
    for i in imgs:
        r = i.resize((i.width * 3, i.height * 3), Image.NEAREST)
        o.paste(r, (0, y), r)
        y += r.height + 6
    o.save(PREVIEW)
