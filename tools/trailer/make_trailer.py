"""Cuts the recorded shots into the trailer, adds captions and music, encodes 1080p60 H.264.

    python tools/trailer/make_trailer.py [recordings_dir] [out.mp4]

Needs, in recordings_dir: trailer_map.mp4 + .marks, battle_*.mp4 + .marks (TrailerDirector),
music.wav (music.py) and captions/ (captions.py). ffmpeg comes from the imageio-ffmpeg package.
The edit follows the music's bars (2 s each): map until 22 s, battles until 54 s, title to 62 s.
"""
import os
import subprocess
import sys

import imageio_ffmpeg

REC = sys.argv[1] if len(sys.argv) > 1 else "recordings"
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(REC, "DeathLand_trailer.mp4")
FPS = 60
LENGTH = 62.0

# (recording, shot mark, seconds after the mark, duration). Played back to back from 0 s.
EDIT = [
    ("trailer_map", "map_wide", 0.5, 6.0),        # 0
    ("trailer_map", "map_castle", 1.0, 4.0),      # 6
    ("trailer_map", "map_march", 1.5, 5.0),       # 10
    ("trailer_map", "map_hud", 1.0, 3.0),         # 15
    ("trailer_map", "map_forest", 1.5, 1.5),      # 18
    ("trailer_map", "map_mine", 1.5, 1.5),        # 19.5
    ("trailer_map", "map_village", 1.5, 1.0),     # 21
    ("battle_forest", "battle_forest", 5.7, 5.6),  # 22: impact
    ("battle_mine", "battle_mine", 7.5, 5.6),
    ("battle_village", "battle_village", 7.7, 5.6),
    ("battle_bridge", "battle_bridge", 9.7, 5.6),
    ("battle_plains", "battle_plains", 10.7, 5.6),  # until 50
    ("battle_bridge_win", "battle_bridge_end", -4.0, 4.0),  # 50: victory
]
# Behind the title card (54-62 s): the wide map, blurred and darkened
TITLE_BACKGROUND = ("trailer_map", "map_wide", 2.0, 8.0)
TITLE_START = 54.0

# (caption png, start, end) in trailer time
CAPTIONS = [
    ("cap_land", 1.0, 5.5),
    ("cap_army", 10.5, 14.5),
    ("cap_conquer", 18.2, 21.8),
    ("cap_crush", 22.4, 25.8),
    ("cap_magic", 39.0, 42.6),
]
CAPTION_FADE = 0.4


def marks(name):
    result = {}
    with open(os.path.join(REC, name + ".marks")) as f:
        for line in f:
            parts = line.split()
            if len(parts) == 3 and parts[0] == "MARK":
                result[parts[1]] = float(parts[2])
    return result


def main():
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    args = [ffmpeg, "-hide_banner", "-loglevel", "error", "-y"]
    filters = []
    labels = []
    input_index = 0

    def add_clip(recording, mark, offset, duration):
        nonlocal input_index
        start = marks(recording)[mark] + offset
        args.extend(["-ss", f"{start:.3f}", "-t", f"{duration:.3f}", "-i", os.path.join(REC, recording + ".mp4")])
        input_index += 1
        return input_index - 1

    total = 0.0
    for i, clip in enumerate(EDIT):
        index = add_clip(*clip)
        filters.append(f"[{index}:v]fps={FPS},scale=1920:1080,setsar=1,settb=AVTB,setpts=PTS-STARTPTS[c{i}]")
        labels.append(f"[c{i}]")
        total += clip[3]
    assert abs(total - TITLE_START) < 0.01, f"edit is {total}s, title should start at {TITLE_START}s"

    index = add_clip(*TITLE_BACKGROUND)
    filters.append(f"[{index}:v]fps={FPS},scale=1920:1080,setsar=1,settb=AVTB,setpts=PTS-STARTPTS,"
                   "gblur=sigma=22,eq=brightness=-0.30:saturation=0.5,vignette=angle=0.9,"
                   "fade=t=in:st=0:d=0.5:color=white[bg]")
    title_input = input_index
    args.extend(["-loop", "1", "-framerate", str(FPS), "-t", "8", "-i", os.path.join(REC, "captions", "title.png")])
    input_index += 1
    filters.append(f"[{title_input}:v]format=rgba,fade=t=in:st=0.15:d=1.0:alpha=1[ttl]")
    filters.append("[bg][ttl]overlay=format=auto[titlecard]")
    labels.append("[titlecard]")
    filters.append("".join(labels) + f"concat=n={len(labels)}:v=1:a=0[cut]")

    # Captions, faded in and out at their times
    current = "cut"
    for i, (name, start, end) in enumerate(CAPTIONS):
        cap_input = input_index
        duration = end - start
        args.extend(["-loop", "1", "-framerate", str(FPS), "-t", f"{duration:.3f}",
                     "-i", os.path.join(REC, "captions", name + ".png")])
        input_index += 1
        filters.append(f"[{cap_input}:v]format=rgba,fade=t=in:st=0:d={CAPTION_FADE}:alpha=1,"
                       f"fade=t=out:st={duration - CAPTION_FADE:.3f}:d={CAPTION_FADE}:alpha=1,"
                       f"setpts=PTS-STARTPTS+{start}/TB[cap{i}]")
        filters.append(f"[{current}][cap{i}]overlay=eof_action=pass:format=auto[v{i}]")
        current = f"v{i}"

    # Fade in from black, out to black at the end
    filters.append(f"[{current}]fade=t=in:st=0:d=1.2,fade=t=out:st={LENGTH - 1.5}:d=1.5,"
                   "scale=in_range=pc:out_range=tv:out_color_matrix=bt709,format=yuv420p[vout]")

    music_input = input_index
    args.extend(["-i", os.path.join(REC, "music.wav")])
    filters.append(f"[{music_input}:a]atrim=0:{LENGTH},asetpts=PTS-STARTPTS[aout]")

    args.extend([
        "-filter_complex", ";".join(filters),
        "-map", "[vout]", "-map", "[aout]",
        "-t", f"{LENGTH}",
        "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-profile:v", "high", "-r", str(FPS),
        "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
        "-c:a", "aac", "-b:a", "320k", "-ar", "48000",
        "-movflags", "+faststart",
        OUT,
    ])
    subprocess.run(args, check=True)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
