---
name: creature-sprite
description: Turn a creature's concept art into an animated pixel-art world-map sprite (idle + walk) for DeathLand, and wire it into the unit template. Handles flat backgrounds automatically and busy backgrounds via a traced polygon mask. Use when the user wants a new unit or creature sprite, a map sprite from an image, or to redo or tune an existing one (imp, orc, wolf, knight, ...).
---

# Creature map sprite from concept art

The pipeline lives in `tools/sprites/`. The `tools/` folder has a `.gdignore`, so
Godot doesn't import it.

- `make_creature_sprite.gd`: generic generator, run by Godot (no Python or Pillow needed).
- `creatures/<name>.json`: per-creature settings. `imp.json` is the reference example.
- `source/<name>_concept.<ext>`: the concept art (png/jpg/webp).

Output goes to `images/Units/sprites/<name>_sheet.png` (all frames in one row) and
`<name>_frames.tres` (SpriteFrames with `idle` and `walk`). The `.tres` is written only
if it doesn't exist yet, so Godot's uid and references to it survive regeneration.

Godot 4.2 executable (use the console build to see output):
`/c/Users/Miomir/Desktop/Godot_v4.2.1-stable_mono_win64/Godot_v4.2.1-stable_mono_win64_console.exe`
(called `$G` below). Run everything from the project root.

## What the art needs

A full-body creature, fully in frame. Weapons and tails are fine. Painted or dark art is
fine, because the `enhance` step brightens it.

- **Flat solid background** (any colour that differs from the creature): removed
  automatically by colour. This is the easy path (see `imp.json`).
- **Busy background** (scenery, gradients, fire, ground): trace the outline as `mask`
  polygons instead (see "Busy backgrounds" below and `orc.json`). A plain-background
  version is still cleaner, so mention it, but don't block on it: tracing works well,
  because at 64px each sprite pixel covers ~16 source pixels.

## Steps

1. **Save the art** to `tools/sprites/source/<name>_concept.<ext>`. Images the user
   pastes in chat are saved under the session's `images/` folder; copy from there.
2. **Create the config**: copy `creatures/imp.json` to `creatures/<name>.json` and set
   `source`. Set `eyes` to the eye centres in *source image pixels*. Look at the image,
   estimate, and refine once you see the result. Use `[]` for creatures without
   visible eyes; repainting them matters because eyes average away at 64px.
3. **First run** with a preview directory (use the scratchpad):
   ```bash
   "$G" --path . --headless -s res://tools/sprites/make_creature_sprite.gd -- <name> <scratchpad>
   ```
   Then **look at** `<scratchpad>/<name>_grid.png` (one frame at 10x, grid every 4px,
   red lines every 8px) and `<name>_preview.png` (all frames).
4. **Fix the still image** using the table below, rerunning after each change.
   Judge it at the preview's 5x scale too, not only on the 10x grid: muddy-looking
   grids often read fine at real size.
5. **Set the animation parts** from the grid. `parts` are `[x, y, w, h]` rectangles in
   sprite pixels (0–63). Poses shift parts by `[dx, dy]`; `body` means everything else.
   - Legs: box only the **lower legs/feet**, below anything that crosses them (a staff,
     a weapon, the tail). Otherwise the moving box drags pieces of that object along
     and breaks it. Lifting a box by 2px reads as a step.
   - Tail, wings, cape: their own box with a 1px sway, plus the body's dy so they
     stay attached.
   - Order matters: each part skips pixels of the parts listed before it, and the body
     is drawn last (it covers the top of a raised leg).
   - Keep the imp's rhythm unless asked otherwise: idle = 2 frames (breathe down 1px);
     walk = left foot up, body down, right foot up, body down. Four-legged creatures
     (wolf): four leg boxes, and lift diagonal pairs together (front-left with back-right).
6. **Wire it into the game**: in `unit_templates/<name>.tres` (or
   `unit_templates/neutral_unit_templates/`), add an ext_resource for
   `res://images/Units/sprites/<name>_frames.tres` and set `MapSprite = ExtResource(...)`,
   bumping `load_steps` by one. `unit.cs` then uses it automatically: idle/walk
   switching, flipping when walking left, half-scale drawing (`unit.tscn` →
   `AnimatedSprite` scale 0.5, linear filter), and the shadow and selection ring.
   Units without a `MapSprite` keep showing their round `Icon`.
   Note: neutral units (`NeutralUnit` resources on fields) are only shown in the UI,
   not on the map, so their `MapSprite` has no visible effect yet; say so.
7. **Import, then check in game**. New PNGs must be imported before the game can load them:
   ```bash
   "$G" --path . --headless --editor --quit
   ```
   If the creature has a unit node under `Map/UnitList` in `game/main/game.tscn`,
   screenshot it (needs a window, so no `--headless`):
   ```bash
   "$G" --path . -s "<abs path to>/.claude/skills/creature-sprite/capture_unit.gd" -- <UnitNodeName> <scratchpad>/<name>
   ```
   Look at `<name>_selected.png` and `<name>_walk.png`, and check it prints `animation=walk`.

## Busy backgrounds: tracing a mask

`trace_helpers.gd` in this skill folder runs with `--headless`:

1. `-- grid <source res:// path> <scratchpad>/grid.png` draws a grid on the source every
   32px (red every 128px). Look at it and write polygons as `[x, y]` source-pixel
   points, going around the outline in order. Use one polygon per piece: body, weapon
   shaft (a thin quad), weapon head, and so on. Overlapping polygons are fine. Leave
   out ground shadows and rocks; include boots and feet.
2. Put them in the config as `"mask": [[[x, y], ...], ...]`. With a mask, the colour
   settings `bg_tolerance` and `ground_y` are ignored or unneeded.
3. `-- mask <name> <scratchpad>/mask.png` tints everything outside the polygons magenta.
   Check that no limb or weapon tip is cut off and no big chunk of background is
   inside, adjust points, and repeat. Slivers of a few source pixels don't matter.

## Fixing the still image

| What you see | Change in the config |
|---|---|
| Background left around the creature | raise `bg_tolerance` (0.11 → 0.15) |
| Parts of the creature eaten away (similar colour to the background) | lower `bg_tolerance` |
| Grey stones/shadow left under the feet | lower `ground_y` to just above the feet (in sprite px) |
| Small specks or chips left over | raise `min_island` |
| Muddy, dark, low contrast | raise `enhance.value_gain` (1.45 → 1.6) or `saturation` |
| Washed out | lower `value_gain` toward 1.2 |
| Colour-graded art (warm/sepia light) turned the skin the same brown as leather | `enhance.hue_shift: [from, to, amount]` shifts only hues in that band, e.g. orc olive skin `[0.09, 0.2, 0.09]`. Sample the material hues first (average a 9x9 patch with `Image.get_pixelv` and print `Color.h`) so the band misses leather and wood |
| Colours banded or blotchy | raise `palette_size` (24 → 32); lower it for a flatter, more pixel-art look |
| Eyes misplaced | adjust `eyes` (source pixels; 1 sprite px ≈ crop side / 64 source px) |

The background colour comes from the middle of each image edge, not the corners,
because some art has darkened corners. Enclosed pockets (between an arm and a
weapon) are removed by colour match, not flood fill.

## Examples

- `imp.json`: flat background, glowing eyes repainted, legs plus tail parts.
- `orc.json`: busy painted scene, so a traced `mask` (body, pickaxe handle, pickaxe
  head), a strong `value_gain` of 1.9 because the painting is low-key, and a `hue_shift`
  to bring the skin back to green. No eyes are repainted, because the orc's aren't glowing.

## Gotchas

- Godot 4.2 GDScript can't infer types from untyped expressions: write
  `var q: Vector2i = ...` rather than `var q := ...` when the right side involves
  Variants (array elements, dictionary values, JSON data).
- To change the on-map size, change the `AnimatedSprite` scale in `unit.tscn`, not the
  sprite resolution. The shadow and ring follow that scale. Nearest filtering at 0.5
  scale drops pixels; keep the linear filter.
- Moving or renaming an asset that is referenced by uid can break loading until the
  editor rescans. Keep paths stable.
- Don't delete and regenerate an existing `<name>_frames.tres` unless the frame count
  or animation names changed. If you must, the template's ext_resource will need its
  uid removed or updated.
