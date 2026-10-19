extends SceneTree
# Prepares a generated ground texture (e.g. from ChatGPT) for images/terrain/:
# - makes it tile seamlessly: near its edges it is blended with a copy of itself shifted by half
#   (whose own seam sits in the middle, where the original is kept)
# - optionally moves its average color towards a target, keeping all detail
# - scales it to <size> x <size>
#
#   Godot --path . --headless -s res://tools/sprites/make_tileable.gd -- <in.png> <out.png> <size> [r g b strength]
#   r g b: target average color (0..1), strength: 0 = keep colors, 1 = exactly the target average

const BLEND := 0.22 # part of the size over which edges are blended

func _init() -> void:
	var a := OS.get_cmdline_user_args()
	var img := Image.load_from_file(a[0])
	img.convert(Image.FORMAT_RGB8)
	var size := int(a[2])
	img.resize(size, size, Image.INTERPOLATE_LANCZOS)

	var shifted := Image.create(size, size, false, Image.FORMAT_RGB8)
	var half := size / 2
	shifted.blit_rect(img, Rect2i(0, 0, size, size), Vector2i(half, half))
	shifted.blit_rect(img, Rect2i(0, 0, size, size), Vector2i(half - size, half))
	shifted.blit_rect(img, Rect2i(0, 0, size, size), Vector2i(half, half - size))
	shifted.blit_rect(img, Rect2i(0, 0, size, size), Vector2i(half - size, half - size))

	var factor := Vector3.ONE
	if a.size() >= 7:
		var avg := average(img)
		var target := Vector3(float(a[3]), float(a[4]), float(a[5]))
		var strength := float(a[6])
		factor = Vector3(pow(target.x / avg.x, strength), pow(target.y / avg.y, strength), pow(target.z / avg.z, strength))
		print("average ", avg, " -> color factor ", factor)

	var band := BLEND * size
	var out := Image.create(size, size, false, Image.FORMAT_RGB8)
	for y in size:
		var wy := 1.0 - smoothstep(0.0, band, minf(y, size - 1 - y))
		for x in size:
			var wx := 1.0 - smoothstep(0.0, band, minf(x, size - 1 - x))
			var c := img.get_pixel(x, y).lerp(shifted.get_pixel(x, y), maxf(wx, wy))
			out.set_pixel(x, y, Color(c.r * factor.x, c.g * factor.y, c.b * factor.z))
	out.save_png(a[1])
	print("saved ", a[1])
	quit()

func average(img: Image) -> Vector3:
	var sum := Vector3.ZERO
	var n := 0
	for y in range(0, img.get_height(), 4):
		for x in range(0, img.get_width(), 4):
			var c := img.get_pixel(x, y)
			sum += Vector3(c.r, c.g, c.b)
			n += 1
	return sum / n
