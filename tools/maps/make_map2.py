# Run: python tools/maps/make_map2.py   (overwrites maps/map2.json; fixed seeds, so reruns give the same map)
# Builds maps/map2.json: "Island kingdom" - a main island with a snowy north, a mountain spine
# in the east, a river from the mountains to the south coast, the Dark Lord's blight in the
# south-east, and two small islands reached by ship.
import json, math, os, random, sys, zlib
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from mapjson import write   # writes JSON in the map editor's own format

OUT = os.path.join(HERE, '..', '..', 'maps', 'map2.json')
W, H = 2300, 2000
rng = random.Random(2)

def r1(v): return round(v, 1)
def P(x, y): return [r1(x), r1(y)]

def blob(cx, cy, rx, ry, n=24, wob=0.12, seed=0):
    g = random.Random(seed)
    ph = [g.uniform(0, 6.28) for _ in range(3)]
    pts = []
    for i in range(n):
        a = math.tau * i / n
        k = 1 + wob * (math.sin(3 * a + ph[0]) * 0.6 + math.sin(5 * a + ph[1]) * 0.3 + math.sin(7 * a + ph[2]) * 0.2) + g.uniform(-wob, wob) * 0.25
        pts.append((cx + math.cos(a) * rx * k, cy + math.sin(a) * ry * k))
    return pts

def inside(p, poly):
    x, y = p; c = False
    for i in range(len(poly)):
        x1, y1 = poly[i]; x2, y2 = poly[i - 1]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            c = not c
    return c

def seg_dist(p, a, b):
    ax, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax, by - ay; L = dx * dx + dy * dy
    t = 0 if L == 0 else max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / L))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)

def line_dist(p, pts): return min(seg_dist(p, pts[i], pts[i + 1]) for i in range(len(pts) - 1))

def seg_cross(a, b, c, d):
    def ccw(p, q, r): return (r[1] - p[1]) * (q[0] - p[0]) - (q[1] - p[1]) * (r[0] - p[0])
    d1, d2, d3, d4 = ccw(c, d, a), ccw(c, d, b), ccw(a, b, c), ccw(a, b, d)
    if (d1 > 0) != (d2 > 0) and (d3 > 0) != (d4 > 0):
        t = d1 / (d1 - d2)
        return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
    return None

# ------------------------------------------------------------------ land and water
main = blob(1170, 990, 860, 740, n=48, wob=0.15, seed=93)
# a bay on the south-west coast, between the castle lands and the river mouth
main = [(x + (60 if (x < 800 and y > 1500) else 0), y - (50 if (x < 800 and y > 1500) else 0)) for x, y in main]
isle_sw = blob(300, 1800, 120, 85, n=16, wob=0.15, seed=21)
isle_ne = blob(2110, 300, 115, 90, n=16, wob=0.15, seed=31)
lands = [main, isle_sw, isle_ne]

snow = blob(900, 320, 430, 175, n=26, wob=0.16, seed=81)
for _ in range(40):
    snow = [p if inside(p, main) and min(math.hypot(p[0]-q[0], p[1]-q[1]) for q in main) > 22 else (900 + (p[0]-900)*0.95, 320 + (p[1]-320)*0.95) for p in snow]
blight = blob(1700, 1380, 230, 200, n=22, wob=0.18, seed=41)
for _ in range(40):
    blight = [p if inside(p, main) and min(math.hypot(p[0]-q[0], p[1]-q[1]) for q in main) > 35 else (1700 + (p[0]-1700)*0.95, 1380 + (p[1]-1380)*0.95) for p in blight]
lake = blob(1000, 960, 95, 65, n=16, wob=0.15, seed=51)
river = [(1585, 690), (1505, 735), (1420, 820), (1320, 930), (1240, 1040), (1190, 1180), (1120, 1390), (1060, 1580), (1020, 1720), (990, 1880)]
RIVER_W = 18
for k in range(len(river) - 1):
    if inside(river[k], main) and not inside(river[k + 1], main):
        a, b = river[k], river[k + 1]
        for t in [i / 50 for i in range(51)]:
            q = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
            if not inside(q, main):
                break
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        river = river[:k + 1] + [(q[0] + (b[0] - a[0]) / d * 25, q[1] + (b[1] - a[1]) / d * 25)]
        break
meadows = [blob(780, 1180, 140, 90, seed=61), blob(1250, 700, 120, 80, seed=62), blob(560, 1080, 90, 70, seed=63)]
dirts = [blob(1380, 1480, 120, 80, seed=71), blob(980, 1300, 90, 60, seed=72), blob(1680, 880, 130, 90, seed=73)]

def on_land(p, margin=0):
    if not any(inside(p, l) for l in lands):
        return False
    if margin and min(min(math.hypot(p[0] - q[0], p[1] - q[1]) for q in l) for l in lands if inside(p, l)) < margin:
        return False
    return True

def in_water(p, extra=0):
    return inside(p, lake) or line_dist(p, river) < RIVER_W / 2 + extra

# ------------------------------------------------------------------ fields
F = {}
def field(fid, ftype, x, y, perks=None, neutrals=None):
    f = {'id': fid, 'type': ftype, 'pos': P(x, y)}
    if perks: f['perks'] = perks
    if neutrals: f['neutrals'] = [{'template': t, 'count': c} for t, c in neutrals]
    F[fid] = f

field('Field1', 'Castle', 520, 1300, ['castle'])                      # the player's castle: first Castle
field('Field2', 'GrassPlains', 655, 1220)
field('Field3', 'Village', 720, 1420, None, [('villager_militia', 2), ('villager_archer', 1)])
field('Field4', 'SpringForest', 545, 1450, ['forest'], [('elf_archer', 1)])
field('Field5', 'GrassRoad', 800, 1300)
field('Field6', 'GrassCrossRoads', 920, 1200, ['trade_hub'])
field('Field7', 'Village', 860, 1070, None, [('villager_militia', 1), ('villager_archer', 1)])
field('Field8', 'AutumnForest', 720, 960, ['forest'], [('wolf', 2), ('elf_archer', 1)])
field('Field9', 'Cave', 610, 830, ['crystal_formations'], [('imp', 2)])
field('Field10', 'GrassPlains', 590, 1000)
field('Field11', 'SpringForest', 870, 850, ['old_growth'], [('wolf', 3), ('elf_archer', 1), ('treant', 1), ('druid', 1)])
field('Field12', 'GrassRoad', 960, 720)
field('Field13', 'Village', 840, 640, None, [('villager_militia', 2), ('villager_archer', 1)])
field('Field14', 'OreMine', 1010, 600, ['ore_vein'], [('orc', 2)])
field('Field15', 'GrassPlains', 1160, 720)
field('Field16', 'GrassRoad', 1090, 1100)
field('Field17', 'Bridge', 1150, 1160)
field('Field18', 'Village', 1320, 1240, ['trade_hub'], [('villager_militia', 2), ('villager_archer', 1)])                 # the town
field('Field19', 'GrassCrossRoads', 1300, 1390)
field('Field20', 'AutumnForest', 1190, 1490, ['forest'], [('elf_archer', 2), ('elf_blade_dancer', 1), ('treant', 1), ('druid', 1)])
field('Field21', 'Village', 1230, 1640, ['raided'], [('villager_militia', 2), ('villager_archer', 1)])
field('Field22', 'GrassPlains', 1440, 1560, None, [('wolf', 2)])
field('Field23', 'GrassRoad', 1540, 1290, None, [('orc', 2)])
field('Field24', 'Castle', 1720, 1450, ['ley_line'], [('orc', 5), ('witch', 3)])  # Dark Lord's tower
field('Field25', 'Cave', 1870, 1250, ['crystal_formations'], [('imp', 4), ('witch', 1)])
field('Field26', 'OreMine', 1500, 1060, ['deep_shafts'], [('orc', 2)])
field('Field27', 'GrassPlains', 1410, 960)
field('Field28', 'Cave', 1600, 640, None, [('imp', 3)])
field('Field29', 'Castle', 1660, 525, ['castle'], [('orc', 4), ('witch', 1)])
field('Field30', 'GrassRoad', 1780, 650)
field('Field31', 'Cave', 2110, 330, ['crystal_formations'], [('wolf', 4)])
field('Field32', 'Village', 300, 1810, None, [('villager_militia', 1), ('villager_archer', 1)])
field('Field33', 'Bridge', 1300, 880)
field('Field34', 'GrassRoad', 1430, 1150)
field('Field35', 'GrassRoad', 900, 1560)
field('Field36', 'Bridge', 1010, 1600)
field('Field37', 'SpringForest', 720, 1530, None, [('elf_archer', 1)])
field('Field38', 'GrassPlains', 1500, 800)

links = [('Field1', 'Field2'), ('Field1', 'Field4'), ('Field2', 'Field5'), ('Field2', 'Field10'), ('Field4', 'Field3'),
         ('Field3', 'Field5'), ('Field5', 'Field6'), ('Field6', 'Field7'), ('Field6', 'Field16'), ('Field7', 'Field8'),
         ('Field8', 'Field10'), ('Field8', 'Field11'), ('Field10', 'Field9'), ('Field11', 'Field12'),
         ('Field12', 'Field13'), ('Field13', 'Field14'), ('Field14', 'Field15'), ('Field12', 'Field15'), ('Field15', 'Field33'),
         ('Field33', 'Field27'), ('Field27', 'Field26'), ('Field27', 'Field38'), ('Field38', 'Field28'), ('Field28', 'Field29'), ('Field29', 'Field30'),
         ('Field30', 'Field31'), ('Field16', 'Field17'), ('Field17', 'Field18'), ('Field18', 'Field19'), ('Field19', 'Field20'),
         ('Field20', 'Field21'), ('Field19', 'Field22'), ('Field21', 'Field22'), ('Field23', 'Field24'),
         ('Field24', 'Field25'), ('Field26', 'Field34'), ('Field34', 'Field18'), ('Field34', 'Field23'), ('Field3', 'Field37'),
         ('Field37', 'Field35'), ('Field35', 'Field36'), ('Field36', 'Field21'), ('Field37', 'Field32'), ('Field4', 'Field37')]
SHIP_LINKS = {('Field30', 'Field31'), ('Field37', 'Field32')}
ROAD_LINKS = [l for l in links if l not in SHIP_LINKS and l not in {('Field8', 'Field10')}]

def fp(fid): return tuple(F[fid]['pos'])

# ------------------------------------------------------------------ roads (gently bent) and bridges
roads, bridges = [], []
for a, b in ROAD_LINKS:
    pa, pb = fp(a), fp(b)
    mx, my = (pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2
    nx, ny = -(pb[1] - pa[1]), pb[0] - pa[0]; L = math.hypot(nx, ny) or 1
    g = random.Random(zlib.crc32((a + b).encode()))
    off = g.uniform(-0.08, 0.08) * L
    roads.append([pa, (mx + nx / L * off, my + ny / L * off), pb])
for pts in roads:
    for i in range(len(pts) - 1):
        for j in range(len(river) - 1):
            c = seg_cross(pts[i], pts[i + 1], river[j], river[j + 1])
            if c:
                dx, dy = river[j + 1][0] - river[j][0], river[j + 1][1] - river[j][1]
                bridges.append((c, (math.degrees(math.atan2(dy, dx)) + 90) % 360))

# ------------------------------------------------------------------ objects
objects = []
blocked = []   # (point, radius) where nothing else may go
def obj(t, x, y, scale=None, variant=None, flip=None, rot=None, block=0):
    o = {'type': t, 'pos': P(x, y)}
    if scale is not None and abs(scale - 1) > 0.01: o['scale'] = round(scale, 2)
    if variant is not None: o['variant'] = variant
    if flip: o['flip'] = True
    if rot is not None: o['rot'] = round(rot, 1)
    objects.append(o)
    if block: blocked.append(((x, y), block))

def free(p, r=10):
    if not on_land(p, margin=14) or in_water(p, 6):
        return False
    if any(line_dist(p, rd) < 9 for rd in roads):
        return False
    if any(math.hypot(p[0] - f['pos'][0], p[1] - f['pos'][1]) < 26 for f in F.values()):
        return False
    return not any(math.hypot(p[0] - q[0], p[1] - q[1]) < max(r, br) for q, br in blocked)

# Buildings first, so nature keeps clear of them
for (x, y), rot in bridges:
    obj('bridge', x, y, rot=rot)
obj('castle', 520, 1265, 1.0, 0, block=60)
obj('dark_tower', 1720, 1415, 1.0, 0, block=55)
obj('castle', 1650, 490, 0.95, 1, block=60)
obj('town', 1320, 1205, 1.0, 0, block=55)
for (x, y, v) in [(720, 1390, 0), (860, 1040, 1), (840, 610, 2), (1230, 1610, 3), (300, 1785, 4)]:
    obj('village', x, y, 0.95, v, block=40)
for (x, y) in [(590, 970), (1160, 690), (1785, 612), (920, 1170)]:
    obj('tower', x, y, 0.9, block=30)
obj('mine', 1010, 570, 1.0, 0, block=38)
obj('mine', 1500, 1028, 1.0, 1, block=38)
for (x, y, s) in [(610, 800, 1.0), (1600, 612, 1.0), (1870, 1220, 1.05), (2110, 305, 0.95)]:
    obj('cave', x, y, s, block=34)
obj('ship', 1980, 480, 1.0, flip=True)
obj('ship', 470, 1720, 0.95)

def scatter(t, cx, cy, rx, ry, n, spacing, scale=(0.85, 1.15), types=None, tries=40):
    placed = 0
    for _ in range(n * tries):
        if placed >= n: break
        a = rng.uniform(0, math.tau); d = math.sqrt(rng.random())
        x, y = cx + math.cos(a) * rx * d, cy + math.sin(a) * ry * d
        if not free((x, y), spacing):
            continue
        tt = rng.choice(types) if types else t
        obj(tt, x, y, rng.uniform(*scale), rng.randrange(60), rng.random() < 0.5, block=spacing)
        placed += 1

def ridge(t, a, b, rows, step, scale=(0.85, 1.25), jitter=14, types=None):
    L = math.hypot(b[0] - a[0], b[1] - a[1]); n = max(2, int(L / step))
    for row in range(rows):
        for i in range(n + 1):
            k = i / n
            x = a[0] + (b[0] - a[0]) * k + rng.uniform(-jitter, jitter) + (row - rows / 2) * 18
            y = a[1] + (b[1] - a[1]) * k + rng.uniform(-jitter, jitter) + (row - (rows - 1) / 2) * 36
            if not free((x, y), 20):
                continue
            tt = rng.choice(types) if types else t
            obj(tt, x, y, rng.uniform(*scale), rng.randrange(40), rng.random() < 0.5, block=22)

# Mountains: snowy spine in the north, brown range in the east, grassy hills in the west,
# blighted peaks around the Dark Lord's tower
ridge('snow_mountain', (560, 380), (1380, 360), 2, 46, (0.85, 1.25))
ridge('mountain', (1560, 760), (1720, 1130), 2, 44, (0.85, 1.25))
ridge('mountain', (1460, 380), (1560, 560), 1, 44, (0.8, 1.1))
ridge('green_mountain', (440, 760), (560, 690), 2, 46, (0.8, 1.15))
ridge('green_mountain', (1300, 1840), (1520, 1720), 1, 50, (0.75, 1.0))
ridge('blight_mountain', (1850, 1330), (1960, 1500), 2, 44, (0.85, 1.2))
ridge('blight_mountain', (1600, 1520), (1660, 1600), 1, 44, (0.8, 1.0))
for (x, y) in [(680, 1120), (1000, 1420), (1560, 1430), (430, 1150), (1180, 860)]:
    if free((x, y), 30): obj('hill', x, y, rng.uniform(0.9, 1.2), rng.randrange(10), block=30)

# Forests
scatter('pine', 560, 1520, 130, 90, 55, 12)
scatter('pine', 900, 820, 120, 110, 50, 12, types=['pine', 'pine', 'spruce'])
scatter('spruce', 760, 470, 230, 70, 45, 13, types=['spruce', 'pine'])
scatter('pine', 1240, 470, 160, 50, 30, 13, types=['pine', 'spruce'])
scatter('oak', 720, 960, 110, 80, 40, 13)
scatter('oak', 1200, 1520, 120, 90, 40, 13, types=['oak', 'oak', 'birch'])
scatter('birch', 1420, 780, 110, 60, 22, 13, types=['birch', 'pine'])
scatter('pine', 1820, 760, 90, 110, 30, 12)
scatter('oak', 1000, 1720, 110, 60, 22, 13, types=['oak', 'birch'])
scatter('pine', 1450, 1680, 90, 60, 20, 12)
scatter('dead_tree', 1760, 1370, 230, 190, 45, 15)
scatter('pine', 2120, 360, 70, 50, 10, 12)
scatter('pine', 1330, 580, 120, 60, 30, 12, types=['pine', 'spruce', 'birch'])
scatter('oak', 420, 1000, 70, 130, 25, 13, types=['oak', 'birch'])
scatter('birch', 1040, 1200, 70, 50, 12, 13)
scatter('pine', 1640, 1080, 70, 60, 14, 12)
scatter('oak', 820, 1320, 80, 50, 12, 13)
scatter('pine', 1000, 1480, 60, 50, 14, 12)
scatter('oak', 300, 1830, 80, 40, 8, 13)
# Willows along the lake and the river
for i in range(16):
    a = math.tau * i / 16
    x, y = 1000 + math.cos(a) * 120, 960 + math.sin(a) * 88
    if free((x, y), 14): obj('willow', x, y, rng.uniform(0.85, 1.1), rng.randrange(20), rng.random() < 0.5, block=14)
for j in range(3, len(river) - 2):
    for side in (-1, 1):
        x, y = river[j]; nx, ny = river[j + 1][1] - river[j][1], -(river[j + 1][0] - river[j][0]); L = math.hypot(nx, ny)
        px, py = x + nx / L * 26 * side, y + ny / L * 26 * side
        if free((px, py), 14): obj('willow', px, py, rng.uniform(0.8, 1.05), rng.randrange(20), side > 0, block=14)
# Small stuff
scatter('bush', 1170, 990, 760, 650, 70, 16, (0.8, 1.2))
scatter('rocks', 1170, 990, 760, 650, 18, 30, (0.8, 1.2), types=['rocks', 'mossy_rocks'])
scatter('rock_spire', 1650, 950, 160, 200, 6, 40, (0.85, 1.2))
scatter('rock_spire', 1820, 1370, 160, 140, 6, 40, (0.85, 1.2))
scatter('rocks', 1000, 420, 400, 80, 8, 30, types=['rocks'])

# Nothing green grows in the blight
GREEN = {'pine', 'spruce', 'oak', 'birch', 'willow', 'bush', 'hill', 'green_mountain', 'mossy_rocks'}
objects = [o for o in objects if not (o['type'] in GREEN and inside(tuple(o['pos']), blight))]
objects.sort(key=lambda o: o['pos'][1])

# ------------------------------------------------------------------ terrain shapes, in paint order
def poly(t, pts, rough=None):
    s = {'type': t, 'shape': 'polygon', 'points': [P(*p) for p in pts]}
    if rough is False: s['rough'] = False
    return s
terrain = [poly('grass', main), poly('grass', isle_sw), poly('grass', isle_ne)]
terrain += [poly('meadow', m) for m in meadows]
terrain += [poly('dirt', d) for d in dirts]
terrain.append(poly('snow', snow))
terrain.append(poly('water', lake))
terrain.append({'type': 'water', 'shape': 'stroke', 'points': [P(*p) for p in river], 'width': RIVER_W})
for pts in roads:
    terrain.append({'type': 'road', 'shape': 'stroke', 'points': [P(*p) for p in pts], 'width': 9})
terrain.append(poly('blight', blight))

m = {'version': 1, 'name': 'Island Kingdom', 'width': W, 'height': H, 'base': 'ocean', 'seed': 2,
     'terrain': terrain, 'objects': objects, 'fields': list(F.values()), 'connections': [list(l) for l in links], 'size': {}}

# ------------------------------------------------------------------ checks
for o in objects:
    if o['type'] in ('castle', 'dark_tower', 'town', 'village', 'tower', 'mine', 'cave'):
        p = tuple(o['pos'])
        d = min(math.hypot(p[0]-q[0], p[1]-q[1]) for l in lands for q in l)
        if d < 35 or not on_land(p, margin=30): print('building near coast:', o['type'], p, round(d))
bad = [f['id'] for f in F.values() if not on_land(tuple(f['pos'])) or in_water(tuple(f['pos']))]
far = [(a, b, round(math.dist(fp(a), fp(b)))) for a, b in links if (a, b) not in SHIP_LINKS and not 60 <= math.dist(fp(a), fp(b)) <= 260]
seen, todo = set(), ['Field1']
while todo:
    c = todo.pop()
    if c in seen: continue
    seen.add(c); todo += [b for a, b in links if a == c] + [a for a, b in links if b == c]
print('fields', len(F), 'objects', len(objects), 'terrain', len(terrain), 'bridges', len(bridges))
print('fields off land / in water:', bad)
print('links out of 60-230 px:', far)
print('unreachable:', sorted(set(F) - seen))
write(m, OUT)
