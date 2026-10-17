# Helpers for tracing a "mask" by hand when the concept art has a busy background.
#
#   Godot --path . --headless -s <abs path>/trace_helpers.gd -- grid <source res:// path> <out.png>
#       Source image with a grid every 32px (red every 128px) for reading off coordinates.
#   Godot --path . --headless -s <abs path>/trace_helpers.gd -- mask <creature> <out.png>
#       Source at half size with everything OUTSIDE the config's mask polygons tinted
#       magenta, to check the trace.
extends SceneTree


func _initialize() -> void:
	var a := OS.get_cmdline_user_args()
	match a[0]:
		"grid":
			_grid(a[1], a[2])
		"mask":
			_mask(a[1], a[2])
	quit()


func _grid(source: String, out: String) -> void:
	var img := Image.load_from_file(source)
	img.convert(Image.FORMAT_RGBA8)
	print("size ", img.get_size())
	var w := img.get_width()
	var h := img.get_height()
	for i in range(0, max(w, h), 32):
		var col := Color(1, 0.2, 0.2) if i % 128 == 0 else Color(0.3, 0.9, 1, 0.5)
		if i < w:
			for j in h:
				img.set_pixel(i, j, img.get_pixel(i, j).lerp(col, col.a))
		if i < h:
			for j in w:
				img.set_pixel(j, i, img.get_pixel(j, i).lerp(col, col.a))
	img.save_png(out)


func _mask(creature: String, out: String) -> void:
	var cfg: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://tools/sprites/creatures/%s.json" % creature))
	var img := Image.load_from_file(cfg.source)
	img.convert(Image.FORMAT_RGBA8)
	var half := img.get_size() / 2
	img.resize(half.x, half.y)
	var polys: Array[PackedVector2Array] = []
	for poly in cfg.mask:
		var pts := PackedVector2Array()
		for p in poly:
			pts.append(Vector2(p[0], p[1]) / 2.0)
		polys.append(pts)
	for y in half.y:
		for x in half.x:
			var p := Vector2(x, y)
			if not polys.any(func(q): return Geometry2D.is_point_in_polygon(p, q)):
				img.set_pixel(x, y, img.get_pixel(x, y).lerp(Color(1, 0, 1), 0.6))
	img.save_png(out)
