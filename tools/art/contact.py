"""폴더별 스프라이트 시트의 첫 칸(16x16)을 모아 이름표와 함께 한 장으로 만든다.
usage: contact.py <root> <glob> <out> [cell=16] [scale=4]"""
import sys, glob, os
from PIL import Image, ImageDraw

root, pattern, out = sys.argv[1], sys.argv[2], sys.argv[3]
cell = int(sys.argv[4]) if len(sys.argv) > 4 else 16
scale = int(sys.argv[5]) if len(sys.argv) > 5 else 4
files = sorted(glob.glob(os.path.join(root, pattern)))
files = [f for f in files if 'Faceset' not in f]
cols = 10
w = cell * scale + 20
h = cell * scale + 18
sheet = Image.new('RGB', (cols * w, ((len(files) + cols - 1) // cols) * h), (60, 60, 70))
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert('RGBA')
    # 아래 방향 걷기 2프레임 미리보기 = 첫 칸
    c = im.crop((0, 0, cell, cell)).resize((cell * scale, cell * scale), Image.NEAREST)
    x, y = (i % cols) * w + 10, (i // cols) * h
    sheet.paste(c, (x, y), c)
    d.text((x, y + cell * scale + 2), os.path.basename(os.path.dirname(f))[:12], fill=(255, 255, 255))
sheet.save(out)
print(len(files), sheet.size)
