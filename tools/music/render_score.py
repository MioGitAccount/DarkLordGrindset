"""Render a decoded score (JSON note list) to MIDI and a looping OGG/WAV.

Usage: python render_score.py <notes.json> <out_basename>

Note JSON entries: {staff, steps, dur, bar, onset} where steps are diatonic
steps above the staff's bottom line (D minor, one flat), as produced by
decoding the ReportLab score PDF. Instruments are synthesised with numpy
(additive/formant/pluck models) and a convolution reverb whose tail is
wrapped back to the start so the track loops seamlessly.
"""
import json
import struct
import sys

import numpy as np
from scipy.signal import butter, fftconvolve, lfilter

SR = 44100
BPM = 78
BEAT = 60.0 / BPM
BAR_BEATS = 4

LETTERS = [0, 2, 4, 5, 7, 9, 11]  # C D E F G A B
KEY_FLATS = {6}                   # B -> Bb (D minor)

# Bottom staff line as (letter index, octave). Viola and horns were engraved
# with the bottom line meaning C, which is the only reading that fits the harmony.
BOTTOM = {
    "Flute": (2, 4), "Violin I": (2, 4), "Violin II": (2, 4),
    "Viola": (0, 3), "Cello": (4, 2), "Double Bass": (4, 2),
    "Horns": (0, 4), "Choir": (2, 4), "Harp": (2, 4),
    "Celesta": (2, 4), "Timpani": (4, 3),  # timpani raised an octave (A1 is unplayable)
}

# instrument -> (synth, gain, pan -1..1, GM program, channel)
PARTS = {
    "Flute":       ("flute",   0.30, -0.25, 73, 0),
    "Violin I":    ("strings", 0.20, -0.45, 48, 1),
    "Violin II":   ("strings", 0.13, -0.20, 48, 2),
    "Viola":       ("strings", 0.13,  0.20, 48, 3),
    "Cello":       ("strings", 0.20,  0.40, 48, 4),
    "Double Bass": ("strings", 0.22,  0.50, 48, 5),
    "Horns":       ("horn",    0.20,  0.10, 60, 6),
    "Choir":       ("choir",   0.20,  0.00, 52, 7),
    "Harp":        ("harp",    0.20, -0.55, 46, 8),
    "Timpani":     ("timpani", 0.40,  0.00, 47, 9),
    "Celesta":     ("celesta", 0.14,  0.55, 8, 10),
}


def midi_pitch(staff, steps):
    letter, octave = BOTTOM[staff]
    i = letter + octave * 7 + steps
    l = i % 7
    return 12 * (i // 7 + 1) + LETTERS[l] - (1 if l in KEY_FLATS else 0)


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def env(n, attack, release, total):
    """Attack/sustain/release envelope over n samples; release starts at `total`."""
    t = np.arange(n) / SR
    e = np.minimum(1.0, t / max(attack, 1e-4))
    rel = np.clip((t - total) / release, 0, 1)
    return e * (1 - rel) ** 2


def additive(f, t, weights, vib=0.0, vib_rate=5.0, detune=0.0):
    phase = 2 * np.pi * f * (t + vib / (2 * np.pi * vib_rate) * np.sin(2 * np.pi * vib_rate * t)) * (1 + detune)
    out = np.zeros_like(t)
    for k, w in enumerate(weights, 1):
        if f * k > 9000:
            break
        out += w * np.sin(k * phase)
    return out


def lowpass(x, cutoff):
    b, a = butter(2, min(cutoff, SR * 0.45) / (SR / 2))
    return lfilter(b, a, x)


def bandpass(x, lo, hi):
    b, a = butter(2, [lo / (SR / 2), hi / (SR / 2)], btype="band")
    return lfilter(b, a, x)


RNG = np.random.default_rng(7)


def synth(kind, m, dur):
    f = hz(m)
    if kind in ("strings", "choir", "horn", "flute"):
        rel = {"strings": 0.45, "choir": 0.9, "horn": 0.35, "flute": 0.25}[kind]
        n = int((dur + rel) * SR)
        t = np.arange(n) / SR
        if kind == "strings":
            saw = [1 / k for k in range(1, 40)]
            x = sum(additive(f, t, saw, vib=0.004, vib_rate=5.2 + d * 30, detune=d)
                    for d in (-0.003, 0.0, 0.0035)) / 3
            x = lowpass(x, 2200 + f * 2)
            e = env(n, 0.18, rel, dur)
        elif kind == "horn":
            w = [1 / k ** 1.2 for k in range(1, 16)]
            x = additive(f, t, w, vib=0.002) + 0.6 * additive(f, t, w, detune=0.002)
            x = lowpass(x, 1100 + f)
            e = env(n, 0.12, rel, dur)
        elif kind == "flute":
            x = additive(f, t, [1, 0.25, 0.12, 0.04], vib=0.006, vib_rate=5.0)
            breath = bandpass(RNG.standard_normal(n), f * 0.8, min(f * 3, 15000)) * 0.08
            x = x + breath
            e = env(n, 0.07, rel, dur)
        else:  # choir: detuned saws through "aah" formants
            saw = [1 / k for k in range(1, 50)]
            src = sum(additive(f, t, saw, vib=0.006, vib_rate=4.6 + d * 40, detune=d)
                      for d in (-0.006, -0.002, 0.002, 0.006)) / 4
            x = (bandpass(src, 600, 900) * 1.0 + bandpass(src, 1000, 1300) * 0.6
                 + bandpass(src, 2400, 2900) * 0.25)
            e = env(n, 0.6, rel, dur)
        return x * e
    if kind == "harp":
        n = int((dur + 1.6) * SR)
        t = np.arange(n) / SR
        x = np.zeros(n)
        for k in range(1, 14):
            if f * k > 9000:
                break
            x += np.sin(2 * np.pi * f * k * t) * np.exp(-t * (1.2 + 0.9 * k)) / k ** 1.4
        return x * np.minimum(1, t / 0.003)
    if kind == "celesta":
        n = int((dur + 2.0) * SR)
        t = np.arange(n) / SR
        f2 = f * 2  # celesta sounds an octave higher
        x = (np.sin(2 * np.pi * f2 * t) * np.exp(-t * 1.6)
             + 0.35 * np.sin(2 * np.pi * f2 * 4 * t) * np.exp(-t * 6)
             + 0.15 * np.sin(2 * np.pi * f2 * 7.1 * t) * np.exp(-t * 10))
        return x * np.minimum(1, t / 0.002)
    if kind == "timpani":
        n = int((dur + 2.0) * SR)
        t = np.arange(n) / SR
        fp = f * (1 + 0.04 * np.exp(-t * 20))
        ph = 2 * np.pi * np.cumsum(fp) / SR
        x = (np.sin(ph) + 0.5 * np.sin(1.5 * ph) * np.exp(-t * 3)) * np.exp(-t * 1.3)
        x += lowpass(RNG.standard_normal(n), 400) * np.exp(-t * 30) * 0.6
        return x
    raise ValueError(kind)


def section_gain(beat_pos, total_beats):
    """Gentle arc: soft intro, fuller middle, easing out so the loop reconnects."""
    bar = beat_pos / BAR_BEATS
    pts = [(0, 0.55), (4, 0.7), (16, 0.85), (24, 1.0), (32, 0.9), (40, 0.75),
           (48, 0.95), (56, 0.6)]
    return float(np.interp(bar, *zip(*pts)))


def write_midi(notes, path):
    events = []
    for nt in notes:
        _, _, _, prog, ch = PARTS[nt["staff"]]
        start = int((nt["bar"] * BAR_BEATS + nt["onset"]) * 480)
        end = start + int(nt["dur"] * 480) - 10
        m = nt["midi"]
        events.append((start, 1, bytes([0x90 | ch, m, 80])))
        events.append((end, 0, bytes([0x80 | ch, m, 0])))
    head = bytearray()
    head += b"\x00\xff\x51\x03" + int(60_000_000 / BPM).to_bytes(3, "big")
    head += b"\x00\xff\x58\x04\x04\x02\x18\x08"
    head += b"\x00\xff\x59\x02\xff\x01"  # D minor
    for name, (_, _, pan, prog, ch) in PARTS.items():
        head += bytes([0, 0xC0 | ch, prog])
        head += bytes([0, 0xB0 | ch, 10, int(64 + pan * 50)])
        head += bytes([0, 0xB0 | ch, 91, 70])
    body = bytearray()
    last = 0
    for tick, _, data in sorted(events, key=lambda e: (e[0], e[1])):
        delta = tick - last
        last = tick
        vlq = [delta & 0x7F]
        delta >>= 7
        while delta:
            vlq.insert(0, (delta & 0x7F) | 0x80)
            delta >>= 7
        body += bytes(vlq) + data
    track = head + body + b"\x00\xff\x2f\x00"
    with open(path, "wb") as fh:
        fh.write(b"MThd" + struct.pack(">IHHH", 6, 0, 1, 480))
        fh.write(b"MTrk" + struct.pack(">I", len(track)) + track)


def main(src, out):
    notes = json.load(open(src))
    for nt in notes:
        nt["midi"] = midi_pitch(nt["staff"], nt["steps"])
    write_midi(notes, out + ".mid")

    total_beats = (max(n["bar"] for n in notes) + 1) * BAR_BEATS
    length = int(total_beats * BEAT * SR)
    pad = 4 * SR
    mix = np.zeros((2, length + pad))
    for nt in notes:
        kind, gain, pan, _, _ = PARTS[nt["staff"]]
        beat = nt["bar"] * BAR_BEATS + nt["onset"]
        x = synth(kind, nt["midi"], nt["dur"] * BEAT)
        x *= gain * section_gain(beat, total_beats)
        s = int(beat * BEAT * SR)
        x = x[: mix.shape[1] - s]
        mix[0, s:s + len(x)] += x * np.sqrt((1 - pan) / 2)
        mix[1, s:s + len(x)] += x * np.sqrt((1 + pan) / 2)

    # convolution reverb (hall-ish, 2.8 s)
    n_ir = int(2.8 * SR)
    t = np.arange(n_ir) / SR
    wet = np.zeros_like(mix)
    for c in range(2):
        ir = RNG.standard_normal(n_ir) * np.exp(-t * 2.4)
        ir = lowpass(ir, 5000)
        ir[: int(0.02 * SR)] = 0
        ir /= np.sqrt(np.sum(ir ** 2))
        wet[c] = fftconvolve(mix[c], ir)[: mix.shape[1]]
    out_sig = 0.75 * mix + 0.55 * wet

    # wrap everything past the loop point back onto the start -> seamless loop
    loop = out_sig[:, :length].copy()
    tail = out_sig[:, length:]
    loop[:, : tail.shape[1]] += tail
    loop /= np.max(np.abs(loop)) / 0.89
    loop = loop.T.astype(np.float32)

    try:
        import soundfile as sf
        # libsndfile's vorbis encoder crashes on big single writes; feed it chunks
        with sf.SoundFile(out + ".ogg", "w", SR, 2, format="OGG", subtype="VORBIS") as fh:
            for i in range(0, len(loop), 32768):
                fh.write(loop[i:i + 32768])
        print("wrote", out + ".ogg")
    except Exception as e:  # noqa: BLE001
        from scipy.io import wavfile
        wavfile.write(out + ".wav", SR, (loop * 32767).astype(np.int16))
        print("ogg failed (%s); wrote %s.wav" % (e, out))
    print("wrote", out + ".mid", "| %.1f s, %d notes" % (length / SR, len(notes)))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
