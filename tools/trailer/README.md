# Game trailer

A ~62 s 1080p60 trailer of the default map: map flyovers, an army marching, the HUD, and
battles on five biomes with units casting their spells, to synthesized music.

Needs `pip install imageio-ffmpeg numpy scipy pillow` (ffmpeg comes with imageio-ffmpeg).
Everything is written to the git-ignored `recordings/` folder. Raw Movie Maker AVIs are big
(~45 MB/s), so each is converted to H.264 and deleted right after recording.

1. Shots, with `game/trailer/trailer.tscn` (TrailerDirector.cs) under Movie Maker. Raise the
   MJPEG quality first with a temporary `override.cfg` in the project root (delete it after):
   ```
   [editor]
   movie_writer/mjpeg_quality=0.92
   ```
   Then for `map` and each battle (`battle_forest`, `battle_mine`, `battle_village`,
   `battle_bridge`, `battle_plains`):
   ```bash
   "$G" --path . --windowed --resolution 1920x1080 --fixed-fps 60 --write-movie "$(pwd -W)/recordings/battle_mine.avi" res://game/trailer/trailer.tscn -- battle_mine | grep MARK > recordings/battle_mine.marks
   ```
   (`map` records to `trailer_map.avi`.) Convert each AVI:
   `ffmpeg -i X.avi -an -c:v libx264 -preset slow -crf 12 -pix_fmt yuv420p X.mp4`.
   The ending uses `battle_bridge_win`: the bridge battle recorded with `-- battle_bridge long`
   so it plays to the Victory screen. Battles are random; without `--write-movie` and with a
   folder after the shot name (`-- battle_mine <dir>`) it saves a screenshot every 3 s instead.
2. Music: `python tools/trailer/music.py recordings/music.wav`
3. Captions and title: `python tools/trailer/captions.py recordings/captions`
4. Edit: `python tools/trailer/make_trailer.py recordings recordings/DeathLand_trailer.mp4`.
   The `EDIT` list picks each clip as seconds after a shot's MARK; re-check the battle
   windows after re-recording (they are chosen for when spells fire).
