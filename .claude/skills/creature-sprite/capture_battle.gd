# Opens the battle screen on a field and screenshots it, then starts the fight and saves
# a frame every 0.1s for 20s, to check battle sprites and their motion.
#
#   Godot --path . -s <abs path>/capture_battle.gd -- <out_dir> [field]
#
# Must run with a window (no --headless). The field (default Field14) needs player units
# and neutrals on it. Writes <out_dir>/start.png and <out_dir>/f000.png, f001.png, ...
# The -s run skips autoloads, so GlobalTickManager is added by hand.
extends SceneTree
func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var out := args[0]
	var field_name := args[1] if args.size() > 1 else "Field14"
	var tick = load("res://game/main/GlobalTickManager.cs").new()
	tick.name = "GlobalTickManager"
	root.add_child(tick)
	var game: Node = load("res://game/main/game.tscn").instantiate()
	root.add_child(game)
	for i in 20:
		await process_frame
	var battle = game.get_node("UI").find_child("Battle", true, false)
	battle.call("Open", game.get_node("Map/" + field_name))
	for i in 40:
		await process_frame
	root.get_texture().get_image().save_png(out + "/start.png")
	for b in battle.find_children("*", "Button", true, false):
		if b.text == "Start battle":
			b.emit_signal("pressed")
	var t0 := Time.get_ticks_msec()
	var n := 0
	while Time.get_ticks_msec() - t0 < 20000:
		await create_timer(0.1, true).timeout
		root.get_texture().get_image().save_png(out + "/f%03d.png" % n)
		n += 1
	quit()
