# Tiles a run of capture_battle.gd frames, cropped to one region, 4 per row.
#
#   Godot --headless -s <abs path>/contact_sheet.gd -- <frames dir> <out.png> <first> <count> <x> <y> <w> <h>
extends SceneTree
func _initialize() -> void:
	var a := OS.get_cmdline_user_args()
	var first := int(a[2]); var count := int(a[3])
	var r := Rect2i(int(a[4]), int(a[5]), int(a[6]), int(a[7]))
	var cols := 4
	var rows := int(ceil(count / float(cols)))
	var out := Image.create(r.size.x * cols, r.size.y * rows, false, Image.FORMAT_RGBA8)
	for i in count:
		var img := Image.load_from_file(a[0] + "/f%03d.png" % (first + i))
		img.convert(Image.FORMAT_RGBA8)
		out.blit_rect(img, r, Vector2i((i % cols) * r.size.x, (i / cols) * r.size.y))
	out.save_png(a[1])
	quit()
