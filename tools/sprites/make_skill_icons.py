# Turns square skill art (tools/sprites/source/skill_icons/<name>.png) into round game icons
# (images/skills/<name>.png): centre crop, 256px, circular alpha with a soft edge. The skill tree
# draws its own rim around them (gold when learned, grey when locked).
#
#   python tools/sprites/make_skill_icons.py [name ...]     (no names = every source image)
import os, sys
from PIL import Image, ImageDraw

SRC = 'tools/sprites/source/skill_icons'
OUT = 'images/skills'
SIZE = 256
SUPERSAMPLE = 4
ZOOM = 1.0  # >1 crops tighter around the middle

def make(name):
    im = Image.open(os.path.join(SRC, name + '.png')).convert('RGB')
    side = int(min(im.size) / ZOOM)
    left, top = (im.width - side) // 2, (im.height - side) // 2
    icon = im.crop((left, top, left + side, top + side)).resize((SIZE, SIZE), Image.LANCZOS).convert('RGBA')
    mask = Image.new('L', (SIZE * SUPERSAMPLE, SIZE * SUPERSAMPLE), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, SIZE * SUPERSAMPLE - 1, SIZE * SUPERSAMPLE - 1), fill=255)
    icon.putalpha(mask.resize((SIZE, SIZE), Image.LANCZOS))
    os.makedirs(OUT, exist_ok=True)
    icon.save(os.path.join(OUT, name + '.png'))
    print('saved', name)

names = sys.argv[1:] or sorted(f[:-4] for f in os.listdir(SRC) if f.endswith('.png'))
for n in names:
    make(n)
