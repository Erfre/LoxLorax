"""Generate a deterministic neutral fur texture used by both Blender and Unity."""
from pathlib import Path
import math
import random
from PIL import Image, ImageDraw

random.seed(772)
size=1024
im=Image.new('RGB',(size,size),(233,233,233))
draw=ImageDraw.Draw(im)
for _ in range(57000):
    x=random.randrange(size); y=random.randrange(size)
    length=random.randint(5,28)
    drift=random.uniform(-3,3)
    shade=random.choice([173,190,205,216,223,242,249,255])
    points=[(x+drift*(t/4)**2,y+length*t/4) for t in range(5)]
    draw.line(points,fill=(shade,shade,shade),width=1)
    if y+length>=size:
        draw.line([(a,b-size) for a,b in points],fill=(shade,shade,shade),width=1)
path=Path(__file__).resolve().parents[1]/'assets'/'fur.png'
im.save(path)
im.point(lambda value: 210+int(value*45/255)).save(path.with_name('gold-fur.png'))
print(path)
