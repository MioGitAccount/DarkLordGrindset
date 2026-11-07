# Castle screen art - plan

**Status (2026-11-07): the screen is built.** Art so far: the background, the 3 plots and one
placeholder building (a witch house) that every building uses until it gets its own picture. The tower uses the
map's `dark_tower.png` until `tower_1..3` exist. To add art: drop the raw image in `art_src/castle/<id>.png`,
run `python tools/castle/cut_buildings.py`, then `python tools/castle/make_castle_buildings.py`, and open
Godot once so it imports the new PNG. Code: `game/UI/BuildWindow.cs`, `game/UI/CastleSpot.cs`,
`game/UI/CastleLayout.cs`; spots in `resources/castle/castle_layout.json` (F9 in the castle window, in
debug builds: drag the buildings, wheel to resize, F9 again to save). Differences from the plan below:
the details sit in a column right of the scene (the game's base resolution is 1152x648), and the
background has one raised terrace with the tower on it, not a hill. When exporting the game, add
`*.json` to the export's non-resource include filter, so the layout ships.

The build window's rectangle cards become a painted castle screen: one big background, with each
building drawn on its own spot. Picked by the user:

- **Spots per slot.** Each faction layer has a base spot and 3 slot spots, and the tower sits in the
  middle (17 spots). Whatever is built in a slot is drawn on that slot's spot.
- **Level art for the tower and the bases.** Tower I/II/III and every base at levels 1/2/3 each get their own image.
- **One shared ground.** A dark fortress courtyard, not four themed districts. The buildings carry
  the faction look.
- **Flat backgrounds.** Buildings are generated on a flat solid colour, and a script cuts them out.

## How the screen works

```
 background 1920x1080 (scaled to the window)
 +------------------------------------------------------------------+
 |  Chaos      Stronghold      [  TOWER  ]     Dark Humans   Undead |   back terrace : slot 3 (smallest)
 |  slot 3     slot 3          [ on the  ]     slot 3        slot 3 |
 |  slot 2     slot 2          [  hill   ]     slot 2        slot 2 |   middle terrace: slot 2
 |  slot 1     slot 1          [         ]     slot 1        slot 1 |   front terrace : slot 1
 |  BASE       BASE                            BASE          BASE   |   front: bases (biggest)
 +------------------------------------------------------------------+
 | details panel (as today): info, effects, picker, Build/Recruit/Demolish |
 +------------------------------------------------------------------+
```

- The courtyard rises in **terraces** toward the tower. Rows further back are drawn smaller, so the
  scene has depth. The art is anchored bottom-centre on each spot.
- **Built** spot: the building's image. Hovering it brightens it and shows its name, and clicking it
  selects it (the details panel works as it does today).
- **Empty** slot: a construction plot (stakes, rope and a cleared patch). Clicking it opens the picker.
- **Locked** slot (base level too low): a faint rubble patch with a padlock badge showing the base level it needs.
- **Under construction**: the plot with scaffolding over it, plus a timer badge.
- A small faction banner is drawn by code over each column, and level dots under each base.
- **Spots live in data** (`resources/castle/castle_layout.json`: position and scale per spot, in
  background pixels). A debug layout mode lets the spots be dragged and saved. That way the
  background can be swapped without touching code.

## Image list (30 images)

The ids are the file names. Drop the raw generated images into `art_src/castle/<id>.png`.

| # | id | What | Generate at |
|---|---|---|---|
| 1 | `castle_background` | courtyard with terraces and the tower hill, **no buildings** | 1920x1080 (16:9) |
| 2 | `plot_empty` | cleared construction plot with stakes and rope | 1024x1024 |
| 3 | `plot_locked` | small rubble patch, overgrown | 1024x1024 |
| 4 | `plot_scaffold` | wooden scaffolding over a half-built foundation | 1024x1024 |
| 5-7 | `tower_1` `tower_2` `tower_3` | the dark tower, growing taller and grander | 1024x1536 (portrait) |
| 8-10 | `imp_den_1..3` | Chaos base | 1024x1024 |
| 11-13 | `orc_pit_1..3` | Stronghold base | 1024x1024 |
| 14-16 | `witch_hut_1..3` | Dark Humans base | 1024x1024 |
| 17-19 | `crypt_1..3` | Undead base | 1024x1024 |
| 20 | `torture_chamber` | Chaos | 1024x1024 |
| 21 | `imp_academy` | Chaos | 1024x1024 |
| 22 | `infernal_library` | Chaos | 1024x1024 |
| 23 | `arena` | Stronghold | 1024x1024 |
| 24 | `orc_war_camp` | Stronghold | 1024x1024 |
| 25 | `warlords_hall` | Stronghold | 1024x1024 |
| 26 | `prison` | Dark Humans | 1024x1024 |
| 27 | `healing_hut` | Dark Humans | 1024x1024 |
| 28 | `graveyard` | Undead | 1024x1024 |
| 29 | `bank` | neutral | 1024x1024 |
| 30 | `dark_throne` | neutral | 1024x1024 |

That is 1 background, 3 plots, 3 towers, 12 base levels and 11 slot buildings. The Item Shop gets an
image later, once it exists.

**Order to make them in:** the background first, then `tower_1`, `imp_den_1` and `plot_empty`. With
those four, the screen can be built and tested. The rest can follow in any order. Until a building
has art, the game shows its old card in its spot.

## Prompts

Every prompt is **style block + subject**. Keep the style block exactly the same each time, so
the buildings match each other and the background. If your generator takes a reference image,
pass `images/map_objects/dark_tower.png` (the map tower) or the finished background as the style reference.

### Style block (buildings and plots)

> Hand-painted dark fantasy strategy game art, town screen building, rich painterly detail, muted
> desaturated palette of charcoal stone, old wood and rust, with small accents of sickly green
> magical glow, soft dramatic light from the upper left, three-quarter front view from slightly
> above, a single building isolated and centred, the whole building visible including its ground
> footprint, sitting on a small patch of dark earth, on a flat solid magenta background (#FF00FF),
> no cast shadow on the background, no text, no border, no people.

Swap magenta for flat green (#00FF00) when the building itself has pink or purple in it. The cut-out
script reads the colour from the corners, so any flat colour works.

### Background

> Hand-painted dark fantasy strategy game town screen background, wide panoramic view of an empty
> dark fortress courtyard seen from the front and slightly above, three broad stone terraces rising
> toward a rocky hill in the centre back, the hill top is flat and empty, the terraces are flat,
> empty, cleared ground of packed dark earth and cracked flagstones with room for buildings,
> crumbling black stone walls with spikes on the far left and right edges, a stormy green-tinted
> sky with distant jagged mountains, ravens, faint mist, muted desaturated palette with small
> sickly green glow accents, soft light from the upper left, no buildings, no people, no text,
> 16:9.

The terraces must stay **empty**. If the generator keeps adding houses, add "completely empty, no
structures" or paint them out.

### Plots

- `plot_empty`: *a cleared square construction plot of dark earth marked with wooden stakes and
  rope, a few stones and a shovel*
- `plot_locked`: *a small low pile of rubble and broken black stone overgrown with dead grass, flat and unobtrusive*
- `plot_scaffold`: *a half-built stone foundation surrounded by rough wooden scaffolding, ladders and a small crane, building site*

### Tower (portrait 2:3; the tower is the centrepiece, so make it tall)

- `tower_1`: *a squat dark stone keep with one thick round tower, a few spikes, green-lit arrow slits, wooden roof, modest*
- `tower_2`: *a tall black gothic stone tower with pointed spires, flying buttresses, glowing green windows, larger and grander*
- `tower_3`: *a towering black spire of the dark lord, many jagged pinnacles and spikes, a glowing green crown of
  magical fire at the top, chains and banners, imposing and enormous, the grandest building in the land*

### Faction bases (each level a bigger version of the same building)

The base of each faction looks the same across its 3 levels and grows a bit at each one. Generate level 1 first,
then make levels 2 and 3 from it (image-to-image, or with "same building, upgraded:" before the subject).

- **Imp Den (Chaos):** red-black volcanic rock, fire and brimstone.
  - 1: *a smoky burrow dug into a mound of black volcanic rock, glowing orange cracks, small chimneys, little imp-sized doors*
  - 2: *a larger burrow complex in volcanic rock with several smoking chimneys, lava channels, a horned gate*
  - 3: *a great infernal warren carved into a volcanic crag, rivers of lava, horned towers belching smoke and flame*
- **Orc Pit (Stronghold):** rough logs, mud, hides, crude iron.
  - 1: *a mud pit ringed by a crude log fence, a hide tent, a skull totem, crude weapons rack*
  - 2: *a fortified orc camp, sharpened log palisade, a big hide longhouse, war drums, skull banners*
  - 3: *a massive orc war fort of logs and scrap iron, watchtowers with spikes, huge war drums, bone trophies*
- **Witch's Hut (Dark Humans):** crooked gothic timber, candles, herbs.
  - 1: *a crooked leaning timber hut with a mossy thatched roof, a bubbling cauldron, hanging herbs, candle-lit window*
  - 2: *a larger crooked two-storey witch house, a slanted tower, drying herbs, crows, green-glowing windows*
  - 3: *a sprawling gothic coven house with crooked spires, a greenhouse of poisonous plants, a big glowing cauldron*
- **Crypt (Undead):** pale grey stone, bones, cold blue-green mist.
  - 1: *a small stone crypt entrance with stairs going down, cracked pillars, skulls, cold mist*
  - 2: *a larger mausoleum with a domed roof, bone ornaments, iron gates, ghostly green-blue lanterns*
  - 3: *a grand necropolis temple of pale stone, tall bone-decorated spires, open tomb doors spilling cold mist*

### Slot buildings

- `torture_chamber`: *a squat windowless black stone building with iron-barred doors, chains, hooks and a rack outside, a glowing brazier of irons*
- `imp_academy`: *a lopsided volcanic-rock schoolhouse with a crooked bell tower, chalkboards and books outside, small horned gargoyles*
- `infernal_library`: *a tall dark tower of stacked black stone with stained glass of red fire, floating burning books, candles everywhere*
- `arena`: *a small round fighting pit of sand ringed with log stands and spikes, crude orc banners, weapons on racks*
- `orc_war_camp`: *a training ground with hide tents, straw target dummies, a forge glowing orange, weapon racks*
- `warlords_hall`: *a huge longhouse of dark logs and iron plates, a giant skull above the door, horned roof, war banners*
- `prison`: *a grim stone gaol with barred windows, a heavy iron door, a watch post and hanging cages*
- `healing_hut`: *a small round herbalist's hut with a turf roof, drying herbs, jars of leeches, a green-glowing remedy pot*
- `graveyard`: *a small walled graveyard with leaning tombstones, freshly dug graves, a bone archway, cold green mist*
- `bank`: *a squat fortified stone vault with an iron-banded door, gold coin piles visible through bars, a crooked scale sign*
- `dark_throne`: *an open-air black stone dais with a huge spiked throne, burning braziers, banners of the dark lord, green magical fire*

## Insertion pipeline (my side)

1. **`tools/castle/cut_buildings.py`**: reads `art_src/castle/*.png`, takes the key colour from the
   corners, flood-fills it away with tolerance and soft edges (removing the colour spill), trims to
   the content, scales so the tallest side is at most 512 px (768 px for the tower), and writes
   `images/castle/<id>.png`. The background is just resized and copied.
2. **Data**:
   - `BuildingDefinition` gains `LevelIcons` (one texture per level, for the bases).
   - The towers and slot buildings use the existing `Icon`.
   - `make_castle_buildings.py` fills them in from `images/castle/<id>.png` when the file exists.
3. **`resources/castle/castle_layout.json`**: the 17 spots `{id, x, y, scale}` in background pixels,
   plus the banner positions. A first guess is made from the background, then tuned.
4. **BuildWindow**:
   - The canvas becomes a `TextureRect` background scaled to fit, keeping its aspect.
   - Each spot is a `TextureButton` placed and scaled from the layout. Clicks only count on the
     building's pixels (a click mask from the image's alpha).
   - Hover brightens the building and shows a name tooltip. The badges (level dots, padlock,
     construction timer, faction banners) are drawn by code on top.
   - The details panel stays below and works as it does now.
   - A spot with no art yet falls back to the current card.
5. **Layout mode** (debug key): drag spots, change their scale with the scroll wheel, then save to the JSON.
6. **Test**: a temp scene takes screenshots of the screen in each state (start, all built, locked,
   under construction). Then I tune the spots and hand-check the clicks.

Note: `BuildWindow.cs` currently has uncommitted construction-timer edits from another chat. Commit
those before this work starts, so the rewrite builds on them.

## Open, my defaults

- Background size 1920x1080, with the details panel under it. On small windows the scene scales down.
- Building sizes on screen at the front row: base about 300 px tall, slot building about 260, tower about 700.
- The tower in the details panel shows its next level's image as a preview.
