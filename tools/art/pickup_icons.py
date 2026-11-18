"""아이템 획득 범위 패시브 아이콘 2종 (원본 팩 없이 저장소의 아트로 합성)
- 버프 PickupRangeUp : 초록 둥근 칸 + 점선 원(자석 범위) + 가운데 EXP 보석
- 너프 PickupRangeDown : 버프 아이콘을 회색(Disabled 톤)으로 + RangeDown 의 오른쪽 아래 빨간 화살표 배지
usage: pickup_icons.py <Art 폴더> (예: Assets/Resources/Textuer/Art)
결과는 <Art>/Icon/Common_Icon_PickupRange{Up,Down}.png. 적용은 Builders.Visual.ArtResourceBuilder.Build()
"""
import sys, os, math
from PIL import Image

ART = sys.argv[1]
ICON = os.path.join(ART, 'Icon')

base = Image.open(os.path.join(ICON, 'Common_Icon_RangeUp.png')).convert('RGBA')
nerf_ref = Image.open(os.path.join(ICON, 'Common_Icon_RangeDown.png')).convert('RGBA')
gem = Image.open(os.path.join(ART, 'Item', 'Common_Item_ExpGem_Yellow.png')).convert('RGBA')

W, H = base.size
BG = (72, 160, 96, 255)
BG_LIGHT = (120, 200, 130, 255)
RING = (240, 255, 235, 255)

icon = Image.new('RGBA', (W, H))
src = base.load()
px = icon.load()
# RangeUp 의 둥근 칸 모양(알파)만 빌린다
for y in range(H):
    for x in range(W):
        if src[x, y][3] > 0:
            px[x, y] = BG

# 점선 원 (반경 10, 12 조각 중 짝수만)
cx, cy, r = (W - 1) / 2, (H - 1) / 2, 10.0
for i in range(96):
    a = 2 * math.pi * i / 96
    if (i // 8) % 2:
        continue
    x, y = round(cx + r * math.cos(a)), round(cy + r * math.sin(a))
    if 0 <= x < W and 0 <= y < H and px[x, y][3] > 0:
        px[x, y] = RING

# 원 안쪽 옅은 반짝임 4점
for dx, dy in ((-6, -6), (6, -6), (-6, 6), (6, 6)):
    x, y = round(cx + dx), round(cy + dy)
    px[x, y] = BG_LIGHT

g = gem.crop(gem.getbbox())
icon.paste(g, ((W - g.width) // 2, (H - g.height) // 2), g)
icon.save(os.path.join(ICON, 'Common_Icon_PickupRangeUp.png'))

# 너프: 회색 톤 + 빨간 배지 (RangeDown 오른쪽 아래 12x12 를 그대로)
gray = icon.copy()
gp = gray.load()
for y in range(H):
    for x in range(W):
        r_, g_, b_, a_ = gp[x, y]
        if a_ == 0:
            continue
        l = (r_ * 299 + g_ * 587 + b_ * 114) // 1000
        v = 92 + l * 110 // 255
        gp[x, y] = (v, v, v - 4, a_)
badge = nerf_ref.crop((W - 12, H - 12, W, H))
gray.paste(badge, (W - 12, H - 12), badge)
gray.save(os.path.join(ICON, 'Common_Icon_PickupRangeDown.png'))
print('done', icon.size)
