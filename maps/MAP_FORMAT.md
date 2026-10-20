# Map file format

A world map is one JSON file (this folder: `maps/*.json`). The in-game **Map Editor**
(`game/editor/map_editor.tscn`, or F10 in the game) reads and writes it, and so can any
other program. The editor watches the open file and reloads it when another program saves it.

All positions are **world pixels**, `[x, y]`, with `(0, 0)` the top-left corner of the map
and `y` going **down**. For scale: a field marker is 32 px wide, a unit moves between
fields 80–120 px apart, a castle object is about 70 px tall.

```jsonc
{
  "version": 1,
  "name": "DeathLand",
  "width": 3561, "height": 2373,      // map size
  "base": "ocean",                    // terrain everywhere before any shape is painted
  "seed": 1,                          // changes the wobble of natural edges
  "underlay": "res://images/resources/DeathLandMap.jpg", // optional, editor only: image to trace over
  "terrain": [ ... ],                 // layer 1, painted in order (later on top)
  "objects": [ ... ],                 // layer 2, decoration
  "fields": [ ... ],                  // layer 3, nodes units walk on
  "connections": [["Field", "Field2"], ...]
}
```

## Layer 1: terrain

Each shape paints one terrain type. Shapes are painted **in list order**, so put big areas
first and details (lakes, rivers, roads) after them.

| `shape`     | keys                          | use for                                   |
|-------------|-------------------------------|-------------------------------------------|
| `"polygon"` | `points: [[x,y], ...]` (≥ 3)  | continents, islands, deserts, lakes       |
| `"stroke"`  | `points` (≥ 1), `width`       | rivers, roads, fjords, coast touch-ups; one point = round dab |
| `"circle"`  | `center: [x,y]`, `radius`     | ponds, round patches                      |

`"rough"` (default `true`) rounds the corners and wobbles the edges, so a polygon with
8–20 points already looks like a natural coastline. Use `"rough": false` for exact shapes.
Polygons must not cross themselves.

Terrain types (`type`):

| id       | kind    | notes                                                              |
|----------|---------|--------------------------------------------------------------------|
| `ocean`  | water   | deep sea, usually the `base`                                       |
| `fjord`  | water   | sea inlet; paint as a stroke (width 20–40) into the land           |
| `water`  | water   | lakes (polygon/circle) and rivers (stroke, width 12–25)            |
| `grass`  | land    | default land                                                       |
| `meadow` | land    | bright green patches                                               |
| `dirt`   | land    | brown patches, often around mountains                              |
| `desert` | land    | sand                                                               |
| `snow`   | land    | snowfields, e.g. around snowy mountains                            |
| `road`   | land    | stroke, width 8–12                                                 |
| `blight` | overlay | dark Dark-Lord corruption, drawn as a soft stain over what's below |

Every coast between water and land automatically gets shallow water, shore rings and a
coastline, so there's no need to draw beaches.

## Layer 2: objects

```json
{ "type": "mountain", "pos": [1200, 840], "scale": 1.1, "variant": 4, "flip": true, "rot": 0 }
```

`pos` is the point where the object **stands on the ground** (bottom middle). `scale` (default
1), `variant` (any number, picks a different look), `flip` (mirror) and `rot` (degrees, bridges
only) are optional. Objects are drawn sorted by `y`, so lower objects cover higher ones.

| type         | size (px tall) | notes                                                    |
|--------------|----------------|----------------------------------------------------------|
| `pine`       | 22  | forests: 20–60 of them, 10–14 px apart                              |
| `oak`        | 20  | leafy / autumn woods                                                |
| `spruce`     | 28  | tall pine: old dark forests, mix with `pine`                        |
| `birch`      | 24  | light woods, mix with pines or oaks                                 |
| `willow`     | 22  | by lakes and rivers                                                 |
| `dead_tree`  | 18  | blighted land                                                       |
| `bush`       | 8   |                                                                     |
| `mountain`   | 70  | ranges: overlapping rows ~40 px apart; even `variant` = snow cap    |
| `snow_mountain` | 70 | grey, snowy: for cold regions                               |
| `blight_mountain` | 70 | dark, purple-veined: inside `blight` areas                |
| `green_mountain` | 70 | grass-covered: green highlands, mix with `hill`            |
| `hill`       | 22  |                                                                     |
| `rocks`      | 12  | cluster of boulders                                                 |
| `mossy_rocks`| 14  | flat mossy stones, for grassland and hills                          |
| `rock_spire` | 26  | tall jagged rock, near mountains or in wastelands                   |
| `castle`     | 70  | even variant = red roofs, odd = blue                                |
| `dark_tower` | 80  |                                                                     |
| `town`       | 55  |                                                                     |
| `village`    | 32  |                                                                     |
| `tower`      | 46  | watchtower                                                          |
| `mine`       | 34  |                                                                     |
| `cave`       | 26  |                                                                     |
| `bridge`     | 14  | lies across a river: set `rot` to the river's direction + 90        |
| `ship`       | 40  | on water near a port                                                |

Objects are decoration only, but they change the ground around them automatically: trees
darken it into forest floor, mountains/caves/mines give rocky foothills, settlements wear the
grass into dirt (MapCatalog `Ground*`). Dense clusters show more than single objects. Their painted art is generated by code (`game/map/render/art/`,
6 looks per type, picked by `variant`) and cached in `user://map_art`. A PNG at
`images/map_objects/<type>.png` replaces it, and `images/terrain/<type>.png` replaces a
terrain texture (one tile covers 512 world px). Run new textures through
`tools/sprites/make_tileable.gd` first: it makes them tile and matches their colors to the palette. Land terrain (grass, meadow, dirt, desert, snow, road) fades softly into what's under it.

## Layer 3: fields and connections

```json
{ "id": "Field6", "type": "Village", "pos": [1001, 988],
  "perks": ["ore_vein"],
  "neutrals": [{ "template": "wolf", "count": 2 }] }
```

- `id`: unique, used by `connections`.
- `type`: a field type from `game/map/FieldTypeManager.cs`: `Castle`, `SpringForest`,
  `AutumnForest`, `Cave`, `OreMine`, `Bridge`, `GrassPlains`, `Village`, `GrassCrossRoads`, `GrassRoad`.
  The type decides what the field produces. Put it next to a matching object (a `Castle` field by a castle, an `OreMine` field by a mine, a forest field among trees).
- `perks` (optional): extra perks, files in `resources/field_perks/` without `.tres`.
- `neutrals` (optional): neutral units guarding the field; `template` is a file in
  `unit_templates/neutral_unit_templates/` or `unit_templates/` without `.tres`.
- The player's starting units begin on the **first `Castle` field** in the list.

`connections` are undirected pairs of field ids. Units walk only along connections, so every
field should be reachable. Keep connected fields roughly 70–150 px apart, and don't run
connections across open sea unless a `Bridge` field or a ship justifies it.

## Checking a map

- In the editor: **Map settings** lists problems (unknown types, broken connections, ...).
- Render a PNG without opening the editor (needs a window, not `--headless`):
  ```
  Godot_v4.2.1-stable_mono_win64_console.exe --path . res://game/editor/map_preview.tscn -- --map res://maps/x.json --out C:/tmp/x.png --width 2000
  ```
  It prints any problems and saves the picture. `--no-fields` hides layer 3.
- Trace a painted image into terrain shapes (rough first draft):
  ```
  Godot_..._console.exe --path . --headless -s res://tools/maps/trace_image.gd -- <image> <out.json> <world_width> <world_height>
  ```
