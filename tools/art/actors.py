"""Ninja Adventure 캐릭터/몬스터 시트에서 왼쪽 방향(3번째 열) 4프레임을 뽑아 32x32 캔버스 가로 스트립으로 저장한다.
원본 16px 을 확대하지 않고 가운데 배치한다 (PPU 32 에서 1 원본 픽셀 = 1/32 유닛, 타일과 같은 밀도).
usage: actors.py <pack_root> <out_dir> [preview.png]"""
import sys, os
from PIL import Image

ROOT, OUT = sys.argv[1], sys.argv[2]
PREVIEW = sys.argv[3] if len(sys.argv) > 3 else None
os.makedirs(OUT, exist_ok=True)

LEFT_COL = 2

# 출력 이름 : (원본 상대 경로, idle 생성 여부)
ACTORS = {
    'D1_Player_Hunter': ('Actor/Character/Hunter/SpriteSheet.png', True),
    'D2_Player_RobotGrey': ('Actor/Character/RobotGrey/SpriteSheet.png', True),
    'D3_Player_NinjaThunder': ('Actor/Character/NinjaThunder/SpriteSheet.png', True),
    'D1_Monster_Slime': ('Actor/Monster/Slime/Slime.png', False),
    'D1_Monster_Golem': ('Actor/Monster/Bear/SpriteSheet.png', False),
    'D2_Monster_Bomber': ('Actor/Monster/Eye2/Eye2.png', False, 0),
    'D2_Monster_Golem': ('Actor/Character/RobotGreen/SpriteSheet.png', False),
    'D2_Monster_Slime': ('Actor/Monster/Slime2/Slime2.png', False),
    'D3_Monster_Wraith': ('Actor/Monster/Spirit/SpriteSheet.png', False),
    'D3_Monster_Shaman': ('Actor/Character/Shaman/SpriteSheet.png', False),
    'Common_Monster_Bat': ('Actor/Monster/BlueBat/SpriteSheet.png', False),
}


def frame(sheet, row):
    global LEFT_COL
    return sheet.crop((LEFT_COL * 16, row * 16, LEFT_COL * 16 + 16, row * 16 + 16))


def canvas(img16):
    c = Image.new('RGBA', (32, 32))
    c.paste(img16, (8, 8))
    return c


def bob(img16, dy):
    """발(아래 4줄)은 두고 몸통만 dy 픽셀 내린다"""
    if dy == 0:
        return img16
    out = img16.copy()
    body = img16.crop((0, 0, 16, 12))
    clear = Image.new('RGBA', (16, 12))
    out.paste(clear, (0, 0))
    out.paste(body, (0, dy), body)
    feet = img16.crop((0, 12, 16, 16))
    out.paste(feet, (0, 12), feet)
    return out


def strip(frames):
    s = Image.new('RGBA', (32 * len(frames), 32))
    for i, f in enumerate(frames):
        s.paste(canvas(f), (i * 32, 0))
    return s


previews = []
for name, spec in ACTORS.items():
    rel, idle = spec[0], spec[1]
    LEFT_COL = spec[2] if len(spec) > 2 else 2
    sheet = Image.open(os.path.join(ROOT, rel)).convert('RGBA')
    walk = [frame(sheet, r) for r in range(4)]
    s = strip(walk)
    s.save(os.path.join(OUT, f'{name}_Walk.png'))
    previews.append(s)
    if idle:
        base = walk[0]
        i = strip([base, base, bob(base, 1), bob(base, 1)])
        i.save(os.path.join(OUT, f'{name}_Idle.png'))
        previews.append(i)
    print(name)

if PREVIEW:
    o = Image.new('RGBA', (128 * 4, 32 * 4 * len(previews)), (70, 90, 70, 255))
    for k, p in enumerate(previews):
        r = p.resize((512, 128), Image.NEAREST)
        o.paste(r, (0, k * 128), r)
    o.save(PREVIEW)
