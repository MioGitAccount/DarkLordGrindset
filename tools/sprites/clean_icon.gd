extends SceneTree
# Prepares a generated map icon (e.g. from ChatGPT) for the game:
# - removes a fake checkerboard / white background painted into the image (flood fill from the
#   edges over light, grey pixels; the icon's dark outline stops the fill), with a soft edge
# - trims empty margins, keeping the object standing on the bottom edge
# - scales it so it's `height` pixels tall (sharp at in-game zoom, small in memory)
#
#   Godot --path . --headless -s res://tools/sprites/clean_icon.gd -- <in.png> <out.png> <height>

const TOLERANCE := 0.16   # how far from pure light grey a background pixel may be
const MIN_LIGHT := 0.78   # background pixels are at least this bright

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 3:
		printerr("usage: -- <in.png> <out.png> <height>")
		quit(1)
		return
	var image := Image.load_from_file(args[0])
	image.convert(Image.FORMAT_RGBA8)
	var w := image.get_width()
	var h := image.get_height()
	if not has_transparency(image):
		remove_background(image, w, h)
		print("removed baked background")
	var used := visible_rect(image)
	for y in h:
		for x in w:
			if image.get_pixel(x, y).a <= 0.1:
				image.set_pixel(x, y, Color(0, 0, 0, 0))
	if used.size.x <= 0:
		printerr("image is empty after cleaning")
		quit(1)
		return
	var margin := 2
	used = used.grow(margin).intersection(Rect2i(0, 0, w, h))
	image = image.get_region(used)
	var target := int(args[2])
	var scale := float(target) / image.get_height()
	if scale < 1.0:
		# Shrink in halving steps for a clean result from big images
		while image.get_height() * 0.5 > target:
			image.resize(image.get_width() / 2, image.get_height() / 2, Image.INTERPOLATE_LANCZOS)
		image.resize(maxi(1, int(round(image.get_width() * float(target) / image.get_height()))), target, Image.INTERPOLATE_LANCZOS)
	image.save_png(args[1])
	print("saved ", args[1], " ", image.get_width(), "x", image.get_height())
	quit()

# Bounds of the pixels that are really visible (generators leave faint alpha in "empty" areas)
func visible_rect(image: Image) -> Rect2i:
	var w := image.get_width()
	var h := image.get_height()
	var x0 := w
	var y0 := h
	var x1 := -1
	var y1 := -1
	for y in h:
		for x in w:
			if image.get_pixel(x, y).a > 0.1:
				x0 = mini(x0, x)
				y0 = mini(y0, y)
				x1 = maxi(x1, x)
				y1 = maxi(y1, y)
	if x1 < 0:
		return Rect2i()
	return Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1)

func has_transparency(image: Image) -> bool:
	for p in [Vector2i(0, 0), Vector2i(image.get_width() - 1, 0), Vector2i(0, image.get_height() - 1), Vector2i(image.get_width() - 1, image.get_height() - 1)]:
		if image.get_pixelv(p).a < 0.5:
			return true
	return false

func is_background(c: Color) -> bool:
	var grey := (c.r + c.g + c.b) / 3.0
	return grey >= MIN_LIGHT and absf(c.r - grey) < TOLERANCE * 0.5 and absf(c.g - grey) < TOLERANCE * 0.5 and absf(c.b - grey) < TOLERANCE * 0.5

func remove_background(image: Image, w: int, h: int) -> void:
	var bg := PackedByteArray()
	bg.resize(w * h)
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
		if bg[i] == 1:
			continue
		var x := i % w
		var y := i / w
		if not is_background(image.get_pixel(x, y)):
			continue
		bg[i] = 1
		if x > 0: stack.append(i - 1)
		if x < w - 1: stack.append(i + 1)
		if y > 0: stack.append(i - w)
		if y < h - 1: stack.append(i + w)
	# Background becomes clear; pixels right next to it get partly see-through for a soft edge
	for y in h:
		for x in w:
			var i := y * w + x
			if bg[i] == 1:
				image.set_pixel(x, y, Color(0, 0, 0, 0))
				continue
			var near := 0
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var xx: int = x + d.x
				var yy: int = y + d.y
				if xx >= 0 and yy >= 0 and xx < w and yy < h and bg[yy * w + xx] == 1:
					near += 1
			if near > 0:
				var c := image.get_pixel(x, y)
				c.a = 1.0 - near * 0.2
				image.set_pixel(x, y, c)
