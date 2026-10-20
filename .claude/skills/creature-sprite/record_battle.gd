# Plays a scripted battle on a field like a player would, with a visible cursor, for
# recording with Godot's Movie Maker:
#
#   Godot --path . --windowed --resolution 1280x720 --fixed-fps 30 --write-movie <out>.avi \
#         -s <abs path>/record_battle.gd -- [field] [shots_dir]
#
# Without --write-movie it runs in real time; with shots_dir it also saves a screenshot at
# each step, to check the script before recording. The field (default Field14) needs
# player units (the Test* units start there) and neutrals.
# Steps: select the field on the map, open the battle, move the Imp to the back row,
# start, order the Orc onto a wolf and the Imp's Firebolt onto the enemy Imp, speed up
# to 2x and 3x, then return to the map after the result. Stops after about 75s of game time.
extends SceneTree

const MAX_SECONDS := 75.0

var game: Node
var battle: Control
var cursor: Node2D
var shots := ""
var shot_n := 0
var game_time := 0.0 # Movie Maker runs at a fixed fps, not in real time


class Arrow extends Node2D:
	func _draw() -> void:
		var pts := PackedVector2Array([Vector2(0, 0), Vector2(0, 22), Vector2(6, 17), Vector2(10, 26), Vector2(14, 24), Vector2(10, 16), Vector2(17, 16)])
		draw_colored_polygon(pts, Color.WHITE)
		pts.append(pts[0])
		draw_polyline(pts, Color.BLACK, 1.5, true)


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var field_name := args[0] if args.size() > 0 else "Field14"
	shots = args[1] if args.size() > 1 else ""

	# -s runs skip autoloads
	var tick = load("res://game/main/GlobalTickManager.cs").new()
	tick.name = "GlobalTickManager"
	root.add_child(tick)
	game = load("res://game/main/game.tscn").instantiate()
	root.add_child(game)
	var layer := CanvasLayer.new()
	layer.layer = 128
	cursor = Arrow.new()
	layer.add_child(cursor)
	root.add_child(layer)
	await frames(10)

	var field: Node2D = game.get_node("Map/" + field_name)
	var camera: Camera2D = game.get_node("CameraController")
	camera.position = field.position + Vector2(120, 40)
	battle = game.get_node("UI").find_child("Battle", true, false)
	cursor.position = screen_size() * Vector2(0.75, 0.7)
	await wait(1.5)
	await shot()

	# Pick the field on the map and open the battle
	await move_to(world_to_screen(field.global_position), 1.0)
	await wait(0.3)
	pulse()
	game.get_node("Map").call("OnNodeClicked", field)
	await wait(1.5)
	await shot()
	await click_control(game.get_node("UI").get("BattleButton"))
	await wait(2.0)
	await shot()

	# Positioning: move the Imp from the front row to a back slot
	await click_control(view_of("Imp", true))
	await wait(0.6)
	var back := slot_screen_pos(0)
	await move_to(back, 0.8)
	await wait(0.2)
	await click()
	await wait(1.2)
	await shot()
	await click_control(button("Start battle"))
	await wait(0.6)

	# Orders (allowed during the countdown and the fight)
	await click_control(view_of("Orc", true))
	await wait(0.3)
	await click_control(view_of("Wolf", false))
	await wait(0.6)
	await shot()
	await click_control(view_of("Imp", true))
	await wait(0.5)
	var firebolt := button_starting("Firebolt")
	if firebolt:
		await click_control(firebolt)
		await wait(0.4)
		await click_control(view_of("Imp", false))
		await wait(0.6)
		await shot()
	await move_to(screen_size() * Vector2(0.5, 0.93), 1.0)

	# Watch, then speed up to 2x and 3x so the fight ends within about a minute
	await wait(9)
	await shot()
	for i in 2:
		var speed := button_starting("Speed")
		if speed:
			await click_control(speed)
		await move_to(screen_size() * Vector2(0.55, 0.93), 0.8)
		if i == 0:
			await wait(9)

	# Wait for the result, then go back to the map
	var done := button("Return to map")
	while done == null or not done.is_visible_in_tree():
		if elapsed() > MAX_SECONDS - 6:
			break
		await wait(0.5)
		done = button("Return to map")
	var won := battle.find_children("*", "Label", true, false).any(func(l): return l.text == "Victory!" and l.is_visible_in_tree())
	print("outcome=", "victory" if won else "defeat or unfinished")
	await wait(2.5)
	await shot()
	if done and done.is_visible_in_tree():
		await click_control(done)
		await wait(3.0)
		await shot()
	quit()


# --- Helpers -----------------------------------------------------------------

func _process(delta: float) -> bool:
	game_time += delta
	return false


func elapsed() -> float:
	return game_time


func frames(n: int) -> void:
	for i in n:
		await process_frame


# Game time, so it matches the recording at a fixed fps
func wait(seconds: float) -> void:
	await create_timer(seconds, true, false, false).timeout


func shot() -> void:
	if shots == "":
		return
	await process_frame
	root.get_texture().get_image().save_png(shots + "/step%02d.png" % shot_n)
	shot_n += 1


func screen_size() -> Vector2:
	return root.get_visible_rect().size


func world_to_screen(p: Vector2) -> Vector2:
	return game.get_viewport().get_canvas_transform() * p


# Moves the drawn cursor, sending mouse motion so hover effects react
func move_to(target: Vector2, seconds: float) -> void:
	var from := cursor.position
	var t := 0.0
	while t < seconds:
		await process_frame
		t += root.get_process_delta_time()
		var k := ease(clampf(t / seconds, 0, 1), -2.2)
		cursor.position = from.lerp(target, k)
		var motion := InputEventMouseMotion.new()
		motion.position = cursor.position
		motion.global_position = cursor.position
		root.push_input(motion, true)
	cursor.position = target


func pulse() -> void:
	var tween := cursor.create_tween()
	tween.tween_property(cursor, "scale", Vector2(0.8, 0.8), 0.06)
	tween.tween_property(cursor, "scale", Vector2.ONE, 0.1)


func click() -> void:
	pulse()
	for pressed in [true, false]:
		var ev := InputEventMouseButton.new()
		ev.button_index = MOUSE_BUTTON_LEFT
		ev.pressed = pressed
		ev.position = cursor.position
		ev.global_position = cursor.position
		root.push_input(ev, true)
		await frames(2)


func click_control(control: Control) -> void:
	if control == null:
		return
	await move_to(control.get_global_rect().get_center(), 0.7)
	await wait(0.15)
	await click()


func button(text: String) -> Button:
	for b in battle.find_children("*", "Button", true, false):
		if b.text == text and b.is_visible_in_tree():
			return b
	return null


func button_starting(text: String) -> Button:
	for b in battle.find_children("*", "Button", true, false):
		if b.text.begins_with(text) and b.is_visible_in_tree():
			return b
	return null


# A living fighter on the given side whose tooltip starts with the name; the body
# part of the view (below the header) so the click lands on the creature
func view_of(unit_name: String, player: bool) -> Control:
	var centre := battle.get_global_rect().get_center().x
	for c in battle.find_children("*", "Control", true, false):
		if c.size != Vector2(150, 160) or not c.tooltip_text.begins_with(unit_name):
			continue
		if (c.get_global_rect().get_center().x < centre) == player:
			return c
	return null


func slot_screen_pos(slot: int) -> Vector2:
	var p: Vector2 = battle.call("SlotPosition", 0, slot)
	return battle.get_global_transform() * (p + Vector2(0, -35))
