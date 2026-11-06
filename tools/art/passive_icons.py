"""추가 패시브 아이콘 7종 (Ninja Adventure Ui/Skill Icon 원본, fx.py icons() 와 같은 규칙)
- 버프 : 원본 아이콘 그대로
- 너프 : Disabled(회색) 아이콘 + 오른쪽 아래 작은 빨간 아래 화살표 (Downgrade)
- 몬스터 : 감소(버프) = 해골 + 파란 아래 화살표(Upgrade 뒤집기), 증가(너프) = 회색 해골 + 빨간 위 화살표(Downgrade 뒤집기)
usage: passive_icons.py <pack_root> <out_icon_dir>
결과는 <out_icon_dir>/Common_Icon_<Name>.png. 적용은 Builders.Visual.ArtResourceBuilder.Build()
"""
import sys, os
from PIL import Image

ROOT, OUT = sys.argv[1:3]
ICON = os.path.join(ROOT, 'Ui', 'Skill Icon')
os.makedirs(OUT, exist_ok=True)


def load(rel):
    return Image.open(os.path.join(ICON, rel + '.png')).convert('RGBA')


def save(img, name):
    img.save(os.path.join(OUT, f'Common_Icon_{name}.png'))


def bbox_crop(img):
    b = img.getbbox()
    return img.crop(b) if b else img


def badge(base, arrow):
    """오른쪽 아래에 화살표를 12x12 로 줄여 겹친다 (fx.py 너프 아이콘과 같은 위치·크기)"""
    out = base.copy()
    small = bbox_crop(arrow).resize((12, 12), Image.NEAREST)
    out.paste(small, (out.width - 12, out.height - 12), small)
    return out


BUFFS = {'DurationUp': 'Spell/Counter', 'ProjectileUp': 'Items & Weapon/Kunai', 'HPRegenUp': 'Job & Action/Potion'}
NUFFS = {'DurationDown': 'Spell/Counter', 'HPRegenDown': 'Job & Action/Potion'}

down = load('Spell/Downgrade')
up = load('Spell/Upgrade')

for name, rel in BUFFS.items():
    save(load(rel), name)

for name, rel in NUFFS.items():
    save(badge(load(rel + 'Disabled'), down), name)

# 몬스터 감소(버프): 파란 화살표를 아래로 / 몬스터 증가(너프): 빨간 화살표를 위로
save(badge(load('Spell/Death'), up.transpose(Image.FLIP_TOP_BOTTOM)), 'MonsterDown')
save(badge(load('Spell/DeathDisabled'), down.transpose(Image.FLIP_TOP_BOTTOM)), 'MonsterUp')

print('done')
