extends SceneTree
# Cuts map icons out of a picture with a busy background, along outlines traced by hand
# (see tools/sprites/map_objects/*.json), and saves them to images/map_objects/<out>.png.
# - edges are anti-aliased; the base of each icon (the last outline points) fades out softly
# - icons of one type (mountain, mountain_1, ...) share one scale, so small ones stay small:
#   they get empty space on top, since the game draws every look of a type at the same height
#
#   Godot --path . --headless -s res://tools/sprites/extract_icons.gd -- <config.json> [preview.png]

const SUPERSAMPLE := 4
const FADE := 0.16 # part of the icon's height over which the base fades out

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var cfg: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	var source := Image.load_from_file(ProjectSettings.globalize_path(cfg.source))
	source.convert(Image.FORMAT_RGBA8)
	var zoom := float(cfg.zoom)

	# Cut every icon at source resolution
	var cuts := {}
	for icon in cfg.icons:
		var origin := Vector2(icon.origin[0], icon.origin[1])
		var poly := PackedVector2Array()
		for p in icon.outline:
			poly.append(origin + Vector2(p[0], p[1]) / zoom)
		cuts[icon.out] = cut(source, poly)

	# One scale per type, from its tallest icon
	var tallest := {}
	for out in cuts:
		var type := type_of(out)
		tallest[type] = maxi(tallest.get(type, 0), cuts[out].get_height())
	var preview := Image.create(1, 1, false, Image.FORMAT_RGBA8)
	for out in cuts:
		var img: Image = cuts[out]
		var type := type_of(out)
		var canvas_h: int = tallest[type]
		var padded := Image.create(img.get_width(), canvas_h, false, Image.FORMAT_RGBA8)
		padded.blit_rect(img, Rect2i(Vector2i.ZERO, img.get_size()), Vector2i(0, canvas_h - img.get_height()))
		var scale := float(cfg.height) / canvas_h
		padded.resize(maxi(1, int(round(padded.get_width() * scale))), int(cfg.height), Image.INTERPOLATE_CUBIC)
		var path: String = "res://images/map_objects/%s.png" % out
		padded.save_png(ProjectSettings.globalize_path(path))
		print("saved ", path, " ", padded.get_size())
	quit()

func type_of(out: String) -> String:
	var parts := out.rsplit("_", true, 1)
	return parts[0] if parts.size() == 2 and parts[1].is_valid_int() else out

func cut(source: Image, poly: PackedVector2Array) -> Image:
	var bounds := Rect2(poly[0], Vector2.ZERO)
	for p in poly:
		bounds = bounds.expand(p)
	var rect := Rect2i(bounds.position.floor(), bounds.size.ceil() + Vector2(2, 2)).intersection(Rect2i(Vector2i.ZERO, source.get_size()))
	var out := Image.create(rect.size.x, rect.size.y, false, Image.FORMAT_RGBA8)
	# Lowest outline point per column: where the base is
	var top := bounds.position.y
	var height := bounds.size.y
	for y in rect.size.y:
		for x in rect.size.x:
			var inside := 0
			for sy in SUPERSAMPLE:
				for sx in SUPERSAMPLE:
					var p := Vector2(rect.position.x + x + (sx + 0.5) / SUPERSAMPLE, rect.position.y + y + (sy + 0.5) / SUPERSAMPLE)
					if Geometry2D.is_point_in_polygon(p, poly):
						inside += 1
			if inside == 0:
				continue
			var a := float(inside) / (SUPERSAMPLE * SUPERSAMPLE)
			# Fade towards the bottom of the icon
			var py := rect.position.y + y + 0.5
			var t := (py - top) / height
			a *= clampf((1.0 - t) / FADE, 0.0, 1.0)
			var c := source.get_pixel(rect.position.x + x, rect.position.y + y)
			c.a = a
			out.set_pixel(x, y, c)
	# Trim
	var used := out.get_used_rect()
	return out.get_region(used)
