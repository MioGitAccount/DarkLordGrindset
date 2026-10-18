extends SceneTree
# Traces a painted map image into terrain shapes for a map file (rough first draft to refine
# in the Map Editor). Classifies each pixel by color as sea / grass / meadow / dirt / desert /
# blight and writes the outlines as polygons.
#
# Run from the project folder (console build of Godot 4.2):
#   Godot_v4.2.1-stable_mono_win64_console.exe --path . --headless -s res://tools/maps/trace_image.gd -- <image> <out.json> <world_width> <world_height>
# Writes {"terrain": [...]} - paste the list into a map file's "terrain".

const CELL := 4          # image pixels per grid cell
const MIN_CELLS := 6     # smaller blobs are dropped
const MIN_BLIGHT_CELLS := 400 # dark buildings and forests also read as blight; keep only big stains
const EPSILON := 1.2     # outline simplification, in cells

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 4:
		printerr("usage: -- <image> <out.json> <world_width> <world_height>")
		quit(1)
		return
	var image := Image.load_from_file(args[0])
	if image == null:
		printerr("can't load ", args[0])
		quit(1)
		return
	var world := Vector2(float(args[2]), float(args[3]))
	var w := image.get_width() / CELL
	var h := image.get_height() / CELL
	# Two halving steps average the colors of trees/details into the ground below
	image.resize(w * 2, h * 2, Image.INTERPOLATE_BILINEAR)
	image.resize(w, h, Image.INTERPOLATE_BILINEAR)

	var classes := PackedByteArray()
	classes.resize(w * h)
	for y in h:
		for x in w:
			classes[y * w + x] = classify(image.get_pixel(x, y))
	classes = majority(classes, w, h, 1)
	classes = majority(classes, w, h, 2)

	var sea := mask_of(classes, w, h, [SEA])
	var ocean := reachable_from_border(sea, w, h)
	var lakes := PackedByteArray()
	lakes.resize(w * h)
	for i in w * h:
		lakes[i] = 1 if sea[i] == 1 and ocean[i] == 0 else 0
	var land := PackedByteArray()
	land.resize(w * h)
	for i in w * h:
		land[i] = 1 - ocean[i]

	var scale := world / Vector2(w, h)
	var shapes := []
	shapes.append_array(outline("grass", land, w, h, scale))
	shapes.append_array(outline("meadow", fill_holes(mask_of(classes, w, h, [MEADOW]), w, h), w, h, scale))
	shapes.append_array(outline("dirt", fill_holes(mask_of(classes, w, h, [DIRT]), w, h), w, h, scale))
	shapes.append_array(outline("desert", fill_holes(mask_of(classes, w, h, [DESERT]), w, h), w, h, scale))
	shapes.append_array(outline("water", lakes, w, h, scale))
	shapes.append_array(outline("blight", fill_holes(mask_of(classes, w, h, [BLIGHT]), w, h), w, h, scale, MIN_BLIGHT_CELLS))

	var file := FileAccess.open(args[1], FileAccess.WRITE)
	file.store_string(JSON.stringify({"terrain": shapes}))
	print("traced ", shapes.size(), " shapes into ", args[1])
	quit()

enum { GRASS, SEA, MEADOW, DIRT, DESERT, BLIGHT }

func classify(c: Color) -> int:
	if c.b > c.r + 0.06 and c.b >= c.g - 0.03:
		return SEA
	if c.v < 0.36 and c.s < 0.6:
		return BLIGHT
	if c.r > 0.74 and c.g > 0.62 and c.b > 0.38 and c.g / c.r > 0.8:
		return DESERT
	if c.g > 0.58 and c.g > c.r + 0.06:
		return MEADOW
	if c.r > c.g and c.r - c.b > 0.22 and c.r > 0.58:
		return DIRT
	return GRASS

# Each cell becomes the most common class around it
func majority(classes: PackedByteArray, w: int, h: int, r: int) -> PackedByteArray:
	var result := classes.duplicate()
	var counts := PackedInt32Array()
	counts.resize(6)
	for y in h:
		for x in w:
			counts.fill(0)
			for dy in range(-r, r + 1):
				for dx in range(-r, r + 1):
					var xx := clampi(x + dx, 0, w - 1)
					var yy := clampi(y + dy, 0, h - 1)
					counts[classes[yy * w + xx]] += 1
			var best := classes[y * w + x]
			for k in 6:
				if counts[k] > counts[best]:
					best = k
			result[y * w + x] = best
	return result

func mask_of(classes: PackedByteArray, w: int, h: int, wanted: Array) -> PackedByteArray:
	var mask := PackedByteArray()
	mask.resize(w * h)
	for i in w * h:
		mask[i] = 1 if wanted.has(int(classes[i])) else 0
	return mask

# Cells of the mask connected to the map edge
func reachable_from_border(mask: PackedByteArray, w: int, h: int) -> PackedByteArray:
	var seen := PackedByteArray()
	seen.resize(w * h)
	var stack := PackedInt32Array()
	for x in w:
		stack.append(x)
		stack.append((h - 1) * w + x)
	for y in h:
		stack.append(y * w)
		stack.append(y * w + w - 1)
	while stack.size() > 0:
		var i: int = stack[stack.size() - 1]
		stack.remove_at(stack.size() - 1)
		if seen[i] == 1 or mask[i] == 0:
			continue
		seen[i] = 1
		var x := i % w
		var y := i / w
		if x > 0: stack.append(i - 1)
		if x < w - 1: stack.append(i + 1)
		if y > 0: stack.append(i - w)
		if y < h - 1: stack.append(i + w)
	return seen

# Fills enclosed gaps so every blob has only an outer edge (later shapes paint over the gaps)
func fill_holes(mask: PackedByteArray, w: int, h: int) -> PackedByteArray:
	var inverse := PackedByteArray()
	inverse.resize(w * h)
	for i in w * h:
		inverse[i] = 1 - mask[i]
	var outside := reachable_from_border(inverse, w, h)
	var result := PackedByteArray()
	result.resize(w * h)
	for i in w * h:
		result[i] = 1 if outside[i] == 0 else 0
	return result

func outline(type: String, mask: PackedByteArray, w: int, h: int, scale: Vector2, min_cells := MIN_CELLS) -> Array:
	var bitmap := BitMap.new()
	bitmap.create(Vector2i(w, h))
	var count := 0
	for y in h:
		for x in w:
			if mask[y * w + x] == 1:
				bitmap.set_bit(x, y, true)
				count += 1
	if count == 0:
		return []
	var shapes := []
	for poly in bitmap.opaque_to_polygons(Rect2i(0, 0, w, h), EPSILON):
		if poly.size() < 3 or absf(area(poly)) < min_cells:
			continue
		var points := []
		for p in poly:
			var v: Vector2 = p * scale
			points.append([snappedf(v.x, 0.1), snappedf(v.y, 0.1)])
		shapes.append({"type": type, "shape": "polygon", "points": points})
	return shapes

func area(poly: PackedVector2Array) -> float:
	var a := 0.0
	for i in poly.size():
		a += poly[i].cross(poly[(i + 1) % poly.size()])
	return a / 2.0
