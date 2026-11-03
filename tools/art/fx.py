"""무기·이펙트·아이템·아이콘·UI·타이틀 이미지 생성 (Ninja Adventure 원본 + 직접 그린 픽셀)
usage: fx.py <pack_root> <out_dir> <tiles_dir> <actors_dir>"""
import sys, os, random
from PIL import Image, ImageDraw

ROOT, OUT, TILES, ACTORS = sys.argv[1:5]
for sub in ('weapon', 'item', 'icon', 'ui', 'title'):
    os.makedirs(os.path.join(OUT, sub), exist_ok=True)


def load(rel):
    return Image.open(os.path.join(ROOT, rel)).convert('RGBA')


def save(img, sub, name):
    img.save(os.path.join(OUT, sub, name + '.png'))


def cell(img, i, w, h=None):
    h = h or img.height
    return img.crop((i * w, 0, i * w + w, h))


def bbox_crop(img):
    b = img.getbbox()
    return img.crop(b) if b else img


def fit32(img, upscale=True):
    """32x32 캔버스 가운데. upscale 이면 들어가는 최대 정수배로 확대"""
    img = bbox_crop(img)
    k = 1
    if upscale:
        k = max(1, min(32 // img.width, 32 // img.height))
    elif img.width > 32 or img.height > 32:
        r = min(32 / img.width, 32 / img.height)
        img = img.resize((max(1, int(img.width * r)), max(1, int(img.height * r))), Image.NEAREST)
    img = img.resize((img.width * k, img.height * k), Image.NEAREST)
    c = Image.new('RGBA', (32, 32))
    c.paste(img, ((32 - img.width) // 2, (32 - img.height) // 2), img)
    return c


def tint(img, rgb, keep=0.35):
    """밝기는 유지하고 색을 rgb 쪽으로 (흰 이펙트 → 색 이펙트)"""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            l = (r * 0.3 + g * 0.59 + b * 0.11) / 255
            px[x, y] = tuple(int(min(255, c * l * (1 - keep) + c * keep * l + 255 * l * l * keep * 0.5)) for c in rgb) + (a,)
    return out


OUTLINE = (20, 16, 28, 255)


def outline(img):
    """투명 칸 중 불투명 칸과 맞닿은 곳에 1px 외곽선"""
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
                    px[x, y] = OUTLINE
                    break
    return out


def blob(size, fill, light, dark, seed, bubbles=True):
    """울퉁불퉁한 원형 웅덩이 (외곽선 포함)"""
    rng = random.Random(seed)
    img = Image.new('RGBA', (size, size))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if d < c - 1.5 + rng.random() * 0.8:
                px[x, y] = fill if d < c - 3.5 else dark
                if d < c - 3.5 and (x - c) + (y - c) < -c * 0.6:
                    px[x, y] = light
    if bubbles:
        for _ in range(size // 6):
            x, y = rng.randrange(size // 4, size * 3 // 4), rng.randrange(size // 4, size * 3 // 4)
            px[x, y] = light
            px[x + 1, y] = light
            px[x, y + 1] = dark
    return outline(img)


# ---------------- 무기 ----------------
def weapons():
    shur = cell(load('FX/Projectile/BigShuriken.png'), 0, 23)
    save(fit32(shur, upscale=False), 'weapon', 'D1_Weapon_Boomerang_Projectile')

    rock = cell(load('FX/Projectile/SpriteSheetRock.png'), 0, 16)
    save(fit32(rock), 'weapon', 'D1_Weapon_SplitShot_Projectile')
    save(fit32(rock), 'weapon', 'D1_Weapon_SplitShot_Fragment')
    smoke = cell(load('FX/Smoke/Smoke/SpriteSheet.png'), 2, 32)
    save(fit32(tint(smoke, (255, 200, 120)), upscale=False), 'weapon', 'D1_Weapon_SplitShot_Burst')

    slash = load('FX/Attack/SlashCurved/SpriteSheet.png')
    s = bbox_crop(cell(slash, 1, 32))
    save(fit32(s), 'weapon', 'D1_Weapon_Sweep_Slash')
    # 가로로 긴 쓸기: 세로 초승달을 90도 돌린다
    save(fit32(s.rotate(90, expand=True)), 'weapon', 'D1_Weapon_Sweep_Arc')

    save(blob(32, (120, 200, 70, 255), (190, 240, 120, 255), (70, 130, 50, 255), 1), 'weapon', 'D1_Weapon_PoisonZone_Area')

    # 레이저: x 방향으로 늘어나므로 y 방향 그라디언트
    beam = Image.new('RGBA', (32, 32))
    px = beam.load()
    rows = [(0, 0), (20, 60), (40, 140), (90, 210), (170, 245), (230, 255), (255, 255), (255, 255)]
    for y in range(32):
        d = abs(15.5 - y) / 16
        idx = min(7, int((1 - d) * 8))
        c, a = rows[idx]
        col = (int(60 + c * 0.75), int(200 + c * 0.2), 255, a)
        for x in range(32):
            px[x, y] = col
    save(beam, 'weapon', 'D2_Weapon_Laser_Beam')

    dungeon = load('Backgrounds/Tilesets/TilesetDungeon.png')
    drone = dungeon.crop((3 * 16, 3 * 16, 4 * 16, 4 * 16))
    save(fit32(drone), 'weapon', 'D2_Weapon_Drone_Body')
    energy = cell(load('FX/Projectile/EnergyBall.png'), 0, 16)
    save(fit32(tint(energy, (90, 230, 255))), 'weapon', 'D2_Weapon_Drone_Bullet')
    save(fit32(energy), 'weapon', 'Common_Weapon_Bullet')

    # 지뢰: 16px 직접 그림
    mine = Image.new('RGBA', (16, 16))
    d = ImageDraw.Draw(mine)
    d.ellipse([2, 4, 13, 12], fill=(70, 76, 90, 255))
    d.ellipse([3, 4, 12, 10], fill=(100, 108, 124, 255))
    d.rectangle([7, 5, 8, 6], fill=(255, 70, 60, 255))
    d.point([(7, 5)], fill=(255, 200, 190, 255))
    save(fit32(outline(mine)), 'weapon', 'D2_Weapon_Mine_Body')

    exp = load('FX/Elemental/Explosion/SpriteSheet.png')
    save(fit32(cell(exp, 3, 40), upscale=False), 'weapon', 'Common_FX_Explosion')

    ring = cell(load('FX/Magic/Circle/SpriteSheetWhite.png'), 3, 32)
    save(fit32(ring, upscale=False), 'weapon', 'D3_Weapon_Shockwave_Ring')

    ball = cell(load('FX/Projectile/BigEnergyBall.png'), 0, 24)
    save(fit32(tint(ball, (200, 120, 255)), upscale=False), 'weapon', 'D3_Weapon_Explosive_Projectile')

    # 체인 번개 라인 (LineRenderer Tile 용 32x8)
    line = Image.new('RGBA', (32, 8))
    lp = line.load()
    ys = [4, 3, 2, 3, 5, 6, 5, 4, 3, 2, 2, 3, 4, 5, 6, 6, 5, 4, 3, 3, 4, 5, 4, 3, 2, 3, 4, 5, 5, 4, 4, 4]
    for x, y in enumerate(ys):
        lp[x, y] = (255, 255, 255, 255)
        if y + 1 < 8:
            lp[x, y + 1] = (150, 220, 255, 220)
        if y - 1 >= 0:
            lp[x, y - 1] = (120, 190, 255, 140)
    line.save(os.path.join(OUT, 'weapon', 'D3_Weapon_Chain_Line.png'))
    spark = load('FX/Magic/Spark/SpriteSheet.png')
    save(fit32(cell(spark, 2, 30), upscale=False), 'weapon', 'D3_Weapon_Chain_Spark')

    # HazardZoneVisual 이 색을 곱하므로 밝은 회색으로 둔다
    save(blob(32, (225, 225, 230, 255), (255, 255, 255, 255), (160, 160, 175, 255), 7), 'weapon', 'Common_Monster_HazardZone')

    # 피격 이펙트 (Spark 첫 프레임), 사망 연기
    save(fit32(cell(spark, 0, 30), upscale=False), 'weapon', 'Common_FX_Hit')
    save(fit32(cell(load('FX/Smoke/Smoke/SpriteSheet.png'), 3, 32), upscale=False), 'weapon', 'Common_FX_DeathSmoke')


# ---------------- 아이템 ----------------
def items():
    for name, rel in (('Green', 'GemGreen'), ('Yellow', 'GemYellow'), ('Purple', 'GemPurple'), ('Red', 'GemRed')):
        gem = bbox_crop(load(f'Items/Resource/{rel}.png'))
        c = Image.new('RGBA', (16, 16))
        c.paste(gem, ((16 - gem.width) // 2, (16 - gem.height) // 2), gem)
        save(c, 'item', f'Common_Item_ExpGem_{name}')


# ---------------- 아이콘 ----------------
ICONS = {
    'Boomerang': 'Items & Weapon/Shuriken', 'PoisonZone': 'Spell/OrbPlant', 'Sweep': 'Spell/Cut',
    'SplitShot': 'Spell/RockSpike', 'Laser': 'Spell/OrbLight', 'Drone': 'Job & Action/Repair',
    'Mine': 'Spell/Explosion', 'Chain': 'Spell/BookThunder', 'Shockwave': 'Spell/BookWind',
    'Explosive': 'Spell/Fireball',
    'DamageUp': 'Spell/AttackUpgrade', 'HPUp': 'Spell/Heal', 'RangeUp': 'Spell/Vision',
    'SpeedUp': 'Items & Weapon/Boot', 'AttackSpeedUp': 'Spell/Upgrade',
}
NUFFS = {'DamageDown': 'DamageUp', 'HPDown': 'HPUp', 'RangeDown': 'RangeUp',
         'SpeedDown': 'SpeedUp', 'AttackSpeedDown': 'AttackSpeedUp'}


def icons():
    for name, rel in ICONS.items():
        save(load(f'Ui/Skill Icon/{rel}.png'), 'icon', f'Common_Icon_{name}')
    down = load('Ui/Skill Icon/Spell/Downgrade.png')
    for name, src in NUFFS.items():
        base = load(f'Ui/Skill Icon/{ICONS[src]}Disabled.png')
        # 오른쪽 아래에 너프 화살표 (Downgrade 아이콘을 반으로 줄여 겹침)
        small = bbox_crop(down).resize((12, 12), Image.NEAREST)
        base.paste(small, (base.width - 12, base.height - 12), small)
        save(base, 'icon', f'Common_Icon_{name}')


# ---------------- UI ----------------
def ui():
    wood = 'Ui/Theme/Theme Wood/'
    for src, dst in (('nine_path_panel', 'Panel'), ('nine_path_panel_2', 'Panel2'),
                     ('nine_path_focus', 'PanelFocus'), ('button_normal', 'Button_Normal'),
                     ('button_hover', 'Button_Hover'), ('button_pressed', 'Button_Pressed')):
        save(load(wood + src + '.png'), 'ui', f'Common_UI_{dst}')

    # 어두운 패널: Panel2 의 안쪽 색만 어둡게 (카드·결과창 바탕, 흰 글자 가독성)
    dark = load(wood + 'nine_path_panel_2.png')
    px = dark.load()
    remap = {(0x8d, 0x97, 0x7f): (44, 40, 56), (0x56, 0x63, 0x4d): (30, 27, 40)}
    for y in range(dark.height):
        for x in range(dark.width):
            r, g, b, a = px[x, y]
            if (r, g, b) in remap:
                px[x, y] = remap[(r, g, b)] + (a,)
    save(dark, 'ui', 'Common_UI_PanelDark')

    # 게이지 프레임 (9-slice, 테두리 3px) 과 채움 (9-slice, 테두리 2px)
    frame = Image.new('RGBA', (16, 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(frame)
    d.rectangle([0, 0, 15, 9], fill=OUTLINE)
    d.rectangle([1, 1, 14, 8], fill=(150, 112, 80, 255))
    d.line([(1, 1), (14, 1)], fill=(210, 168, 120, 255))
    d.rectangle([2, 2, 13, 7], fill=(0, 0, 0, 0))
    save(frame, 'ui', 'Common_UI_Bar_Frame')  # 가운데 투명: 채움 위에 겹쳐 그린다
    for name, base, light, dark in (('HP', (220, 60, 60), (255, 140, 120), (150, 30, 40)),
                                    ('EXP', (70, 190, 240), (170, 240, 255), (40, 110, 180)),
                                    ('MonsterHP', (230, 70, 60), (255, 150, 130), (150, 30, 40))):
        f = Image.new('RGBA', (8, 6), base + (255,))
        fd = ImageDraw.Draw(f)
        fd.line([(0, 0), (7, 0)], fill=light + (255,))
        fd.line([(0, 5), (7, 5)], fill=dark + (255,))
        save(f, 'ui', f'Common_UI_Bar_{name}_Fill')
    save(load('Ui/Receptacle/LifeBarMiniUnder.png'), 'ui', 'Common_UI_Bar_Monster_Frame')
    save(load('Ui/Receptacle/LifeBarMiniProgress.png'), 'ui', 'Common_UI_Bar_Monster_Fill')


# ---------------- 타이틀 ----------------
GLYPHS = {  # 5x7
    '2': ['01110', '10001', '00001', '00110', '01000', '10000', '11111'],
    'C': ['01111', '10000', '10000', '10000', '10000', '10000', '01111'],
    'T': ['11111', '00100', '00100', '00100', '00100', '00100', '00100'],
    'R': ['11110', '10001', '10001', '11110', '10100', '10010', '10001'],
    'L': ['10000', '10000', '10000', '10000', '10000', '10000', '11111'],
}


def logo():
    text = '2CTRL'
    w = len(text) * 6 - 1
    img = Image.new('RGBA', (w + 4, 7 + 5))
    px = img.load()
    top, bottom = (255, 236, 150), (240, 130, 60)
    for i, ch in enumerate(text):
        for y, row in enumerate(GLYPHS[ch]):
            for x, v in enumerate(row):
                if v == '1':
                    t = y / 6
                    col = tuple(int(top[k] * (1 - t) + bottom[k] * t) for k in range(3))
                    px[2 + i * 6 + x, 2 + y] = col + (255,)
    # 획을 2px 로 굵게 한 뒤 외곽선 (1px 획에 외곽선을 두르면 글자 안이 지저분해진다)
    img = outline(img.resize((img.width * 2, img.height * 2), Image.NEAREST))
    # 그림자: 외곽선까지 포함한 실루엣을 2px 아래에 어둡게 깔고 그 위에 글자
    shadow = Image.new('RGBA', (img.width, img.height + 2))
    sil = Image.new('RGBA', img.size, (60, 30, 40, 255))
    shadow.paste(sil, (0, 2), img)
    shadow.paste(img, (0, 0), img)
    big = shadow.resize((shadow.width * 4, shadow.height * 4), Image.NEAREST)
    save(big, 'title', 'Common_Title_Logo')


def title_bg():
    W, H = 480, 270
    bg = Image.new('RGBA', (W, H))
    sheets = {d: Image.open(os.path.join(TILES, f'{d}_Tile_Ground_Sheet.png')).convert('RGBA') for d in ('D1', 'D2', 'D3')}
    rng = random.Random(3)
    layers = {}
    for dim in ('D1', 'D2', 'D3'):
        layer = Image.new('RGBA', (W, H))
        for y in range(0, H, 32):
            for x in range(0, W, 32):
                idx = rng.choice([0] * 9 + [1, 2, 3])
                t = sheets[dim].crop(((idx % 8) * 32, (idx // 8) * 32, (idx % 8) * 32 + 32, (idx // 8) * 32 + 32))
                layer.paste(t, (x, y))
        layers[dim] = layer
    # 대각선으로 세 차원을 나눈다 (픽셀 단위)
    lp = {k: v.load() for k, v in layers.items()}
    bp = bg.load()
    for y in range(H):
        for x in range(W):
            k = x + y * 0.4
            dim = 'D1' if k < W * 0.42 else ('D2' if k < W * 0.78 else 'D3')
            bp[x, y] = lp[dim][x, y]
    # 경계에 어두운 선
    d = ImageDraw.Draw(bg)
    for frac in (0.42, 0.78):
        x0 = W * frac - H * 0.4
        d.line([(x0, H), (W * frac, 0)], fill=(20, 16, 28, 255), width=3)

    def actor(name, x, y, flip=False):
        s = Image.open(os.path.join(ACTORS, name + '.png')).convert('RGBA').crop((0, 0, 32, 32))
        s = bbox_crop(s)
        s = s.resize((s.width * 2, s.height * 2), Image.NEAREST)
        if flip:
            s = s.transpose(Image.FLIP_LEFT_RIGHT)
        bg.paste(s, (x, y), s)

    actor('D1_Player_Hunter_Idle', 70, 170, True)
    actor('D2_Player_RobotGrey_Idle', 225, 185, True)
    actor('D3_Player_NinjaThunder_Idle', 385, 170)
    for n, x, y in (('D1_Monster_Slime_Walk', 30, 210), ('D1_Monster_Golem_Walk', 130, 200), ('Common_Monster_Bat_Walk', 160, 140),
                    ('D2_Monster_Bomber_Walk', 190, 225), ('D2_Monster_Golem_Walk', 280, 150), ('D2_Monster_Slime_Walk', 300, 230),
                    ('D3_Monster_Wraith_Walk', 340, 215), ('D3_Monster_Shaman_Walk', 430, 220), ('Common_Monster_Bat_Walk', 420, 130)):
        actor(n, x, y, x < 240)
    # 로고 영역을 위해 위쪽을 어둡게
    shade = Image.new('RGBA', (W, H))
    sp = shade.load()
    for y in range(H):
        a = int(max(0, 170 - y * 1.1))
        for x in range(W):
            sp[x, y] = (12, 10, 24, a)
    bg = Image.alpha_composite(bg, shade)
    save(bg, 'title', 'Common_Title_Background')

    # 차원 선택 카드 미리보기 (64x48): 바닥 + 플레이어
    for dim, player in (('D1', 'D1_Player_Hunter_Idle'), ('D2', 'D2_Player_RobotGrey_Idle'), ('D3', 'D3_Player_NinjaThunder_Idle')):
        p = Image.new('RGBA', (96, 64))
        r2 = random.Random(dim)
        for y in range(0, 64, 32):
            for x in range(0, 96, 32):
                idx = r2.choice([0, 0, 1, 2, 3, 4, 5])
                t = sheets[dim].crop(((idx % 8) * 32, (idx // 8) * 32, (idx % 8) * 32 + 32, (idx // 8) * 32 + 32))
                p.paste(t, (x, y))
        s = bbox_crop(Image.open(os.path.join(ACTORS, player + '.png')).convert('RGBA').crop((0, 0, 32, 32)))
        s = s.resize((s.width * 2, s.height * 2), Image.NEAREST)
        p.paste(s, ((96 - s.width) // 2, (64 - s.height) // 2), s)
        save(p, 'title', f'{dim}_UI_Preview')


weapons()
items()
icons()
ui()
logo()
title_bg()
print('done')
