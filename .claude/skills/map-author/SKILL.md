---
name: map-author
description: Create or change a DeathLand world map file (maps/*.json) - terrain, objects (trees, mountains, castles, towns...), fields and their connections - and render a preview to check it. Use when the user asks to make a new map, generate a map from a description or image, or edit an existing map (add an island, a forest, fields, roads...).
---

# Writing DeathLand maps

Maps are JSON files in `maps/`. Read `maps/MAP_FORMAT.md` first: it lists every terrain type,
object type, field type and the conventions. The in-game Map Editor (`game/editor/map_editor.tscn`)
reloads a map automatically when its file changes, so the user can watch your edits live.

Godot 4.2 (console build, prints output):
`G=/c/Users/Miomir/Desktop/Godot_v4.2.1-stable_mono_win64/Godot_v4.2.1-stable_mono_win64_console.exe`

## Workflow

1. **Plan on paper first**: map size (3000-4000 x 2000-2500 is a good default), where the
   land, seas, rivers, mountain ranges, forests, castles and towns go, and a rough route network
   of 25-60 fields. Keep the player's castle (first `Castle` field) away from the strongest
   neutrals.
2. **Write the file with a script**, not by hand: for anything bigger than a few edits,
   write a small Python script in your scratchpad that builds the JSON (helpers for "forest
   blob of N pines around (x, y)", "mountain range along a line", "chain of fields along a
   road"). Use a fixed random seed so reruns are stable. Round coordinates to 1 decimal.
   - Terrain: one or a few big `grass` polygons on an `ocean` base (15-40 points each, `rough` on),
     then `desert`/`meadow`/`dirt` areas, then `water` lakes (polygons) and rivers (strokes,
     width 12-25, from inland to the coast), then `road` strokes (width 8-12) between towns,
     then `blight` around the dark places.
   - Objects: forests as dense clusters (pines ~12 px apart); mountain ranges as overlapping
     rows ~40 px apart with varied `scale` 0.8-1.3; one building object per settlement; a
     `bridge` where a road crosses a river (`rot` = river direction + 90 degrees).
     Don't put objects in water (check against your polygons).
   - Fields: next to the object that matches their type, 70-150 px apart along roads and
     through passes; connect them into one network; add `neutrals` on contested fields.
3. **Validate and look at it**:
   ```bash
   "$G" --path . res://game/editor/map_preview.tscn -- --map res://maps/<name>.json --out <scratchpad>/<name>.png --width 2000
   ```
   Fix every `map problem:` line it prints, then Read the PNG and check: coastlines look
   natural, rivers reach the sea, no objects floating in the sea, buildings not buried in
   forests/mountains, fields sit next to their objects, the connection network makes sense.
   Iterate until it looks good. For detail, render a crop by making a temporary copy with
   smaller `width`/`height` - or just ask the user to look in the editor.
4. **Editing an existing map**: load the JSON, change only what was asked, keep field ids and
   everything else as it was (the user may have hand-edited parts in the editor). Shapes are
   painted in order, so a new lake goes *after* the land it sits in.
5. To play it, the game loads `res://maps/deathland.json` by default (`MapSelection.DefaultMap`
   in `game/main/MapSelection.cs`); otherwise open it in the editor and press Play.

## Tracing a picture

To start from a painted map image (like `images/resources/DeathLandMap.jpg`), trace it into
terrain shapes, then place objects and fields by looking at the picture:
```bash
"$G" --path . --headless -s res://tools/maps/trace_image.gd -- "<abs image path>" <scratchpad>/traced.json <world_width> <world_height>
```
Set the map's `underlay` to the image so the user can compare it in the editor (Reference checkbox).
