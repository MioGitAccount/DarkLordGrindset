# Screenshots a unit on the world map: idle+selected, then walking.
#
#   Godot --path . -s <abs path>/capture_unit.gd -- <UnitNodeName> <out_prefix> [target_field]
#
# Must run with a window (no --headless) so the viewport renders. Writes
# <out_prefix>_selected.png and <out_prefix>_walk.png (5x crops around the unit)
# and <out_prefix>_full.png, and prints the current animation name.
extends SceneTree


func _crop(path: String, center: Vector2) -> void:
	var img := root.get_texture().get_image()
	var region := img.get_region(Rect2i(Vector2i(center) - Vector2i(60, 40), Vector2i(120, 80)))
	region.resize(600, 400, Image.INTERPOLATE_NEAREST)
	region.save_png(path)


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var unit_name := args[0]
	var out := args[1]
	var target := args[2] if args.size() > 2 else "Field2"

	var game: Node = load("res://game/main/game.tscn").instantiate()
	root.add_child(game)
	for i in 20:
		await process_frame
	var map := game.get_node("Map")
	var unit: Node2D = map.get_node("UnitList/" + unit_name)
	map.call("SelectUnit", unit)
	for i in 5:
		await process_frame
	_crop(out + "_selected.png", root.get_canvas_transform() * unit.global_position)
	root.get_texture().get_image().save_png(out + "_full.png")

	map.get_node("UnitManager").call("MoveSelectedUnitTo", map.get_node(target))
	await create_timer(1.5).timeout
	_crop(out + "_walk.png", root.get_canvas_transform() * unit.global_position)
	print("animation=", unit.get_node("AnimatedSprite").animation)
	quit()
