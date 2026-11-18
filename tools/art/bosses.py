"""보스 전용 스프라이트 (Ninja Adventure Actor/Boss 원본, 확대·색 보정 없음)
각 원본 애니메이션 한 줄을 정사각 칸(원본 높이) 가로 스트립으로 저장한다. Unity 빌더가 칸 = 이미지 높이로 자른다.
usage: bosses.py <pack_root> <out_dir> [preview.png]      out_dir = Assets/Resources/Textuer/Art/Boss
보스 → 원본
- ForestBoss10  뿌리 거인       GiantBamboo/Walk
- ForestBoss20  도약하는 고목   GiantBamboo2/Walk
- FactoryBoss10 톱날 기계       DemonCyclop/Walk
- FactoryBoss20 폭주 기관       GiantFlam/Idle
- StormBoss10   폭풍의 눈       GiantSpirit/Idle
- StormBoss20   판정의 고리     TenguBlue/Walk
- FinalBoss     최종 보스       SquidRed/Idle"""
import sys, os
from PIL import Image

ROOT, OUT = sys.argv[1], sys.argv[2]
PREVIEW = sys.argv[3] if len(sys.argv) > 3 else None
os.makedirs(OUT, exist_ok=True)

# 출력 이름 : (원본 상대 경로, 원본 프레임 너비)
BOSSES = {
    'D1_Boss_GiantBamboo': ('GiantBamboo/Walk.png', 62),
    'D1_Boss_GiantBamboo2': ('GiantBamboo2/Walk.png', 62),
    'D2_Boss_DemonCyclop': ('DemonCyclop/Walk.png', 50),
    'D2_Boss_GiantFlam': ('GiantFlam/Idle.png', 50),
    'D3_Boss_GiantSpirit': ('GiantSpirit/Idle.png', 50),
    'D3_Boss_TenguBlue': ('TenguBlue/Walk.png', 82),
    'Final_Boss_SquidRed': ('SquidRed/Idle.png', 76),
}

previews = []
for name, (rel, fw) in BOSSES.items():
    src = Image.open(os.path.join(ROOT, 'Actor', 'Boss', rel)).convert('RGBA')
    n = src.width // fw
    cell = max(fw, src.height)
    strip = Image.new('RGBA', (cell * n, cell))
    for i in range(n):
        f = src.crop((i * fw, 0, i * fw + fw, src.height))
        # 칸 가운데, 발은 바닥에 맞춘다
        strip.paste(f, (i * cell + (cell - fw) // 2, cell - src.height), f)
    strip.save(os.path.join(OUT, f'{name}_Walk.png'))
    previews.append(strip)
    print(name, strip.size, n)

if PREVIEW:
    w = max(p.width for p in previews) * 2
    h = sum(p.height * 2 + 4 for p in previews)
    o = Image.new('RGBA', (w, h), (70, 90, 70, 255))
    y = 0
    for p in previews:
        r = p.resize((p.width * 2, p.height * 2), Image.NEAREST)
        o.paste(r, (0, y), r)
        y += r.height + 4
    o.save(PREVIEW)
