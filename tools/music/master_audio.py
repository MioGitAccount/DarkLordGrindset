"""Masters an already rendered song (mp3/wav/ogg) for the game: same loudness as the tracks
render_score.py makes, written as a looping OGG.

Usage: python master_audio.py <song.mp3> <out_basename>
"""
import sys

import numpy as np
import soundfile as sf

import render_score as rs


def main(src, out):
    audio, sr = sf.read(src, always_2d=True)
    if sr != rs.SR:
        from scipy.signal import resample_poly
        audio = resample_poly(audio, rs.SR, sr, axis=0)
    if audio.shape[1] == 1:
        audio = np.repeat(audio, 2, axis=1)
    loop = rs.level_quiet_passages(audio[:, :2].T).T
    loop = rs.limit(loop.T).T
    # a few ms of fade at both ends so the loop seam can't click
    fade = int(0.01 * rs.SR)
    ramp = np.linspace(0, 1, fade)[:, None]
    loop[:fade] *= ramp
    loop[-fade:] *= ramp[::-1]
    with sf.SoundFile(out + ".ogg", "w", rs.SR, 2, format="OGG", subtype="VORBIS") as fh:
        for i in range(0, len(loop), 32768):
            fh.write(loop[i:i + 32768])
    rms = 20 * np.log10(np.sqrt((loop ** 2).mean()))
    print("wrote %s.ogg, %.1f s, %.1f dBFS rms, peak %.2f" % (out, len(loop) / rs.SR, rms, np.abs(loop).max()))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
