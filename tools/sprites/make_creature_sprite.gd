# Builds a pixel-art map sprite sheet for a creature from its concept art.
#
#   Godot --path . --headless -s res://tools/sprites/make_creature_sprite.gd -- <name> [preview_dir]
#
# Settings come from res://tools/sprites/creatures/<name>.json (see imp.json).
# Steps: remove the background (flat colour, or "mask" polygons for busy art), crop, downscale, reduce to a small palette,
# repaint the eyes, then animate by shifting body parts and add an outline.
#
# Writes <output_dir>/<name>_sheet.png (all frames in one row, in animation order)
# and <output_dir>/<name>_frames.tres (SpriteFrames; an existing file keeps its uid).
# With preview_dir it also writes <name>_preview.png (sheet at 5x) and
# <name>_grid.png (first frame at 10x with a grid every 4px, red every 8px).
extends SceneTree

var cfg: Dictionary
var size: int
var work_size: int              # masking happens at 4x, then box-filtered down
var bg_tolerance: float
var mask_polygons: Array[PackedVector2Array] = []   # source-pixel outlines, if given


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		push_error("Usage: -s make_creature_sprite.gd -- <name> [preview_dir]")
		quit(1)
		return
	var creature := args[0]
	var config_path := "res://tools/sprites/creatures/%s.json" % creature
	cfg = JSON.parse_string(FileAccess.get_file_as_string(config_path))
	if cfg == null:
		push_error("Could not read " + config_path)
		quit(1)
		return
	size = int(cfg.get("size", 64))
	work_size = size * 4
	bg_tolerance = cfg.get("bg_tolerance", 0.11)
	for poly in cfg.get("mask", []):
		var points := PackedVector2Array()
		for pt in poly:
			points.append(Vector2(pt[0], pt[1]))
		mask_polygons.append(points)

	var src := Image.load_from_file(cfg.source)
	src.convert(Image.FORMAT_RGBA8)
	var bg := _background_colour(src)

	# Find the character's bounding box on a coarse copy
	var coarse := src.duplicate() as Image
	coarse.resize(work_size, work_size * src.get_height() / src.get_width(), Image.INTERPOLATE_BILINEAR)
	var k := float(src.get_width()) / coarse.get_width()
	var box := _bbox(_background_mask(coarse, bg, Vector2.ZERO, k))
	box = Rect2i(Vector2i(Vector2(box.position) * k), Vector2i(Vector2(box.size) * k))

	# Square crop, bottom aligned, with a small margin for the outline
	var side := int(max(box.size.x, box.size.y) * 1.04)
	var crop := Rect2i(box.get_center().x - side / 2, box.end.y - side + side / 40, side, side)
	var work := Image.create(side, side, false, Image.FORMAT_RGBA8)
	work.fill(bg)
	work.blit_rect(src, crop, Vector2i.ZERO)
	work.resize(work_size, work_size, Image.INTERPOLATE_LANCZOS)

	var base := _downsample(work, _background_mask(work, bg, Vector2(crop.position), float(side) / work_size))
	_remove_ground(base, int(cfg.get("ground_y", size)))
	_remove_islands(base, int(cfg.get("min_island", 16)))
	_enhance(base, cfg.get("enhance", {}))
	_quantize(base, int(cfg.get("palette_size", 24)))
	var eye_colour := _colour(cfg.get("eye_colour", [255, 220, 70]))
	var eye_rim := _colour(cfg.get("eye_rim", [230, 60, 40]))
	for eye in cfg.get("eyes", []):
		var p := Vector2i(((Vector2(eye[0], eye[1]) - Vector2(crop.position)) * size / side).round())
		base.set_pixelv(p, eye_colour)
		base.set_pixelv(p + Vector2i(0, 1), eye_rim)

	var outline := _colour(cfg.get("outline", [20, 24, 28]))
	var frames: Array[Image] = []
	for anim in cfg.animations:
		for pose in anim.frames:
			frames.append(_outlined(_pose(base, pose), outline))
	var sheet := Image.create(size * frames.size(), size, false, Image.FORMAT_RGBA8)
	for i in frames.size():
		sheet.blit_rect(frames[i], Rect2i(0, 0, size, size), Vector2i(i * size, 0))

	var out_dir: String = cfg.get("output_dir", "res://images/Units/sprites")
	var sheet_path := out_dir.path_join(creature + "_sheet.png")
	sheet.save_png(sheet_path)
	print("Saved ", sheet_path)
	_write_sprite_frames(out_dir.path_join(creature + "_frames.tres"), sheet_path)

	if args.size() > 1:
		var preview := sheet.duplicate() as Image
		preview.resize(sheet.get_width() * 5, size * 5, Image.INTERPOLATE_NEAREST)
		preview.save_png(args[1].path_join(creature + "_preview.png"))
		var grid := _outlined(base, outline)
		grid.resize(size * 10, size * 10, Image.INTERPOLATE_NEAREST)
		for i in range(0, size * 10, 40):
			var col := Color(1, 0, 0, 0.6) if i % 80 == 0 else Color(1, 1, 1, 0.25)
			for j in size * 10:
				grid.set_pixel(i, j, col)
				grid.set_pixel(j, i, col)
		grid.save_png(args[1].path_join(creature + "_grid.png"))
	quit()


# --- Animation -------------------------------------------------------------
# cfg.parts maps a part name to [x, y, w, h] in sprite pixels. A pose maps part
# names (and "body" = every pixel not in a part) to a [dx, dy] shift. Parts are
# drawn in config order, each skipping the parts listed before it; the body is
# drawn last so it covers the top of a raised leg.

func _pose(base: Image, pose: Dictionary) -> Image:
	var out := Image.create(size, size, false, Image.FORMAT_RGBA8)
	var drawn: Array[Rect2i] = []
	for part in cfg.get("parts", {}):
		var r: Array = cfg.parts[part]
		var rect := Rect2i(r[0], r[1], r[2], r[3])
		_copy(base, out, rect, _offset(pose, part), drawn)
		drawn.append(rect)
	_copy(base, out, Rect2i(0, 0, size, size), _offset(pose, "body"), drawn)
	return out


func _offset(pose: Dictionary, part: String) -> Vector2i:
	var o: Array = pose.get(part, [0, 0])
	return Vector2i(o[0], o[1])


func _copy(from: Image, to: Image, area: Rect2i, offset: Vector2i, exclude: Array[Rect2i]) -> void:
	for y in range(area.position.y, area.end.y):
		for x in range(area.position.x, area.end.x):
			var p := Vector2i(x, y)
			if exclude.any(func(r): return r.has_point(p)):
				continue
			var c := from.get_pixelv(p)
			var q := p + offset
			if c.a > 0 and q.x >= 0 and q.y >= 0 and q.x < size and q.y < size:
				to.set_pixelv(q, c)


# Writes a SpriteFrames resource with one AtlasTexture per frame. When the file
# exists its uid is kept, so the templates referencing it keep working.
func _write_sprite_frames(path: String, sheet_path: String) -> void:
	var uid := ""
	if FileAccess.file_exists(path):
		var header := FileAccess.get_file_as_string(path).get_slice("\n", 0)
		var at := header.find(' uid="')
		if at >= 0:
			uid = header.substr(at, header.find('"', at + 6) - at + 1)
	var subs := ""
	var anims: Array[String] = []
	var index := 0
	for anim in cfg.animations:
		var entries: Array[String] = []
		for _pose_cfg in anim.frames:
			subs += '[sub_resource type="AtlasTexture" id="AtlasTexture_%d"]\natlas = ExtResource("1_sheet")\nregion = Rect2(%d, 0, %d, %d)\n\n' % [index, index * size, size, size]
			entries.append('{\n"duration": 1.0,\n"texture": SubResource("AtlasTexture_%d")\n}' % index)
			index += 1
		var loop: bool = anim.get("loop", true)
		anims.append('{\n"frames": [%s],\n"loop": %s,\n"name": &"%s",\n"speed": %s\n}' % [", ".join(entries), str(loop).to_lower(), anim.name, float(anim.speed)])
	var text := '[gd_resource type="SpriteFrames" load_steps=%d format=3%s]\n\n' % [index + 2, uid]
	text += '[ext_resource type="Texture2D" path="%s" id="1_sheet"]\n\n' % sheet_path
	text += subs + "[resource]\nanimations = [%s]\n" % ", ".join(anims)
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	print("Saved ", path)


# --- Image processing ------------------------------------------------------

func _colour(rgb: Array) -> Color:
	return Color8(int(rgb[0]), int(rgb[1]), int(rgb[2]))


# Average of the edge midpoints (the corners of the art can be darkened)
func _background_colour(img: Image) -> Color:
	var w := img.get_width()
	var h := img.get_height()
	var c := img.get_pixel(w / 2, 4) + img.get_pixel(w / 2, h - 5) + img.get_pixel(4, h / 2) + img.get_pixel(w - 5, h / 2)
	return c / 4.0


func _colour_distance(a: Color, b: Color) -> float:
	return Vector3(a.r - b.r, a.g - b.g, a.b - b.b).length()


# 1 marks background pixels. origin/scale map img pixels back to source pixels
# (only needed for polygon masks).
func _background_mask(img: Image, bg: Color, origin: Vector2, scale: float) -> PackedByteArray:
	var w := img.get_width()
	var h := img.get_height()
	var mask := PackedByteArray()
	mask.resize(w * h)
	if not mask_polygons.is_empty():
		for y in h:
			for x in w:
				var p := origin + (Vector2(x, y) + Vector2(0.5, 0.5)) * scale
				var inside := mask_polygons.any(func(poly): return Geometry2D.is_point_in_polygon(p, poly))
				mask[y * w + x] = 0 if inside else 1
		return mask
	# Flood fill from the borders
	var stack := PackedInt32Array()
	for x in w:
		stack.append(x)
		stack.append((h - 1) * w + x)
	for y in h:
		stack.append(y * w)
		stack.append(y * w + w - 1)
	while stack.size() > 0:
		var i := stack[stack.size() - 1]
		stack.resize(stack.size() - 1)
		if mask[i] == 1:
			continue
		var x := i % w
		var y := i / w
		if _colour_distance(img.get_pixel(x, y), bg) >= bg_tolerance:
			continue
		mask[i] = 1
		if x > 0: stack.append(i - 1)
		if x < w - 1: stack.append(i + 1)
		if y > 0: stack.append(i - w)
		if y < h - 1: stack.append(i + w)
	# Pockets enclosed by weapons or limbs: near-exact background colour anywhere
	for i in mask.size():
		if mask[i] == 0 and _colour_distance(img.get_pixel(i % w, i / w), bg) < bg_tolerance * 0.5:
			mask[i] = 1
	return mask


func _bbox(mask: PackedByteArray) -> Rect2i:
	var w := work_size
	var h := mask.size() / w
	var lo := Vector2i(w, h)
	var hi := Vector2i(-1, -1)
	for y in h:
		for x in w:
			if mask[y * w + x] == 0:
				lo = Vector2i(min(lo.x, x), min(lo.y, y))
				hi = Vector2i(max(hi.x, x), max(hi.y, y))
	return Rect2i(lo, hi - lo + Vector2i.ONE)


# Box filter work_size -> size; a pixel is opaque when at least half its block is foreground
func _downsample(img: Image, mask: PackedByteArray) -> Image:
	var out := Image.create(size, size, false, Image.FORMAT_RGBA8)
	var f := work_size / size
	for y in size:
		for x in size:
			var sum := Color(0, 0, 0, 0)
			var n := 0
			for dy in f:
				for dx in f:
					var sx := x * f + dx
					var sy := y * f + dy
					if mask[sy * work_size + sx] == 0:
						sum += img.get_pixel(sx, sy)
						n += 1
			if n * 2 >= f * f:
				var c := sum / n
				c.a = 1.0
				out.set_pixel(x, y, c)
	return out


# Drops neutral-grey pixels (stones, ground shadow) from ground_y down
func _remove_ground(img: Image, ground_y: int) -> void:
	for y in range(ground_y, size):
		for x in size:
			var c := img.get_pixel(x, y)
			if c.a > 0 and max(c.r, c.g, c.b) - min(c.r, c.g, c.b) < 0.06:
				img.set_pixel(x, y, Color(0, 0, 0, 0))


# Drops small blobs not connected to the character (stone chips, stray pixels)
func _remove_islands(img: Image, min_size: int) -> void:
	var seen := {}
	for y in size:
		for x in size:
			var start := Vector2i(x, y)
			if seen.has(start) or img.get_pixelv(start).a == 0:
				continue
			var blob: Array[Vector2i] = [start]
			seen[start] = true
			var i := 0
			while i < blob.size():
				var p := blob[i]
				i += 1
				for dy in range(-1, 2):
					for dx in range(-1, 2):
						var q := p + Vector2i(dx, dy)
						var inside := q.x >= 0 and q.y >= 0 and q.x < size and q.y < size
						if inside and not seen.has(q) and img.get_pixelv(q).a > 0:
							seen[q] = true
							blob.append(q)
			if blob.size() < min_size:
				for p in blob:
					img.set_pixelv(p, Color(0, 0, 0, 0))


# Lift saturation and brightness/contrast so a painted, dark concept reads at small size
func _enhance(img: Image, e: Dictionary) -> void:
	var saturation: float = e.get("saturation", 1.25)
	var offset: float = e.get("value_offset", 0.04)
	var gain: float = e.get("value_gain", 1.45)
	# Optional [from_hue, to_hue, amount]: shifts hues in that band (fading out at its
	# edges), e.g. to pull a colour-graded skin back to green without touching leather
	var shift: Array = e.get("hue_shift", [0.0, 0.0, 0.0])
	var mid: float = (shift[0] + shift[1]) / 2.0
	var half: float = max((shift[1] - shift[0]) / 2.0, 0.0001)
	for y in size:
		for x in size:
			var c := img.get_pixel(x, y)
			if c.a == 0:
				continue
			var h: float = c.h + shift[2] * clamp(1.0 - abs(c.h - mid) / half, 0.0, 1.0)
			c = Color.from_hsv(fposmod(h, 1.0), min(c.s * saturation, 1.0), clamp((c.v - offset) * gain, 0.0, 1.0))
			img.set_pixel(x, y, c)


# k-means palette reduction over the opaque pixels
func _quantize(img: Image, k: int) -> void:
	var pixels: Array[Vector3] = []
	for y in size:
		for x in size:
			var c := img.get_pixel(x, y)
			if c.a > 0:
				pixels.append(Vector3(c.r, c.g, c.b))
	var centers: Array[Vector3] = []
	for i in k:
		centers.append(pixels[i * pixels.size() / k])
	var assign := PackedInt32Array()
	assign.resize(pixels.size())
	for _iter in 12:
		for i in pixels.size():
			var best := 0
			var best_d := INF
			for j in k:
				var d := pixels[i].distance_squared_to(centers[j])
				if d < best_d:
					best_d = d
					best = j
			assign[i] = best
		var sums: Array[Vector3] = []
		var counts := PackedInt32Array()
		sums.resize(k)
		counts.resize(k)
		for j in k:
			sums[j] = Vector3.ZERO
		for i in pixels.size():
			sums[assign[i]] += pixels[i]
			counts[assign[i]] += 1
		for j in k:
			if counts[j] > 0:
				centers[j] = sums[j] / counts[j]
	var n := 0
	for y in size:
		for x in size:
			if img.get_pixel(x, y).a > 0:
				var c := centers[assign[n]]
				img.set_pixel(x, y, Color(c.x, c.y, c.z))
				n += 1


func _outlined(img: Image, outline: Color) -> Image:
	var out := img.duplicate() as Image
	for y in size:
		for x in size:
			if img.get_pixel(x, y).a > 0:
				continue
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var q: Vector2i = Vector2i(x, y) + d
				if q.x >= 0 and q.y >= 0 and q.x < size and q.y < size and img.get_pixelv(q).a > 0:
					out.set_pixel(x, y, outline)
					break
	return out
