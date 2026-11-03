"""Render game music to a seamlessly looping OGG.

Usage:
  python render_score.py <notes.json> <out_basename>   decoded PDF score -> .mid + .ogg
  python render_score.py <song.mid> <out_basename>     MIDI file -> .ogg

Score JSON entries: {staff, steps, dur, bar, onset} where steps are diatonic
steps above the staff's bottom line (D minor, one flat), as produced by
decoding the ReportLab score PDF. MIDI tracks are matched to synths by their
track names (see MIDI_PARTS). Instruments are synthesised with numpy
(additive/formant/pluck/drum models) and a convolution reverb whose tail is
wrapped back to the start so the track loops seamlessly.
"""
import json
import math
import struct
import sys

import numpy as np
from scipy.signal import butter, fftconvolve, lfilter

SR = 44100

# ---------------------------------------------------------------- score input
SCORE_BPM = 78
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

# ----------------------------------------------------------------- MIDI input
# track-name prefix -> (synth, gain, pan); velocity scales each note.
# "strings" plays short notes staccato and long notes legato (see synth).
MIDI_PARTS = {
    "Trombones": ("brass",   0.30,  0.25),
    "French horns": ("horn", 0.24, -0.15),
    "Violas and cellos": ("strings", 0.22, 0.35),
    "Violins": ("strings",   0.16, -0.40),
    "Double Bass": ("strings", 0.24, 0.50),
    "Male choir": ("choir",  0.26,  0.00),
    "Trumpets": ("trumpet",  0.16, -0.05),
    "War drums": ("drums",   0.55,  0.00),
    "Timpani": ("timpani",   0.55,  0.05),
    "Metallic": ("anvil",    0.20,  0.30),
    "Flute": ("flute",       0.34, -0.25),
    "Oboe": ("oboe",         0.26,  0.20),
    "Clarinet": ("clarinet", 0.24, -0.10),
    "Harp": ("harp",         0.30, -0.55),
    "Celesta": ("celesta",   0.22,  0.55),
}
# notes shorter than this (seconds) on a "strings" track are bowed staccato
STACCATO_BELOW = 0.35


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


def highpass(x, cutoff):
    b, a = butter(2, cutoff / (SR / 2), btype="high")
    return lfilter(b, a, x)


def bandpass(x, lo, hi):
    b, a = butter(2, [lo / (SR / 2), hi / (SR / 2)], btype="band")
    return lfilter(b, a, x)


RNG = np.random.default_rng(7)


def drum(m, n):
    """General MIDI drum note -> war-drum/metal hit."""
    t = np.arange(n) / SR
    if m in (35, 36):  # big taiko-style bass drum
        f = 48 + 40 * np.exp(-t * 25)
        ph = 2 * np.pi * np.cumsum(f) / SR
        x = np.sin(ph) * np.exp(-t * 4.5)
        # skin "body" an octave and a fifth up, so small speakers still hear the hit
        x += 0.6 * np.sin(2 * ph) * np.exp(-t * 9) + 0.3 * np.sin(3 * ph) * np.exp(-t * 14)
        x += bandpass(RNG.standard_normal(n), 120, 2500) * np.exp(-t * 35) * 0.9
        return x * 1.2
    if m in (41, 43, 45, 47, 48, 50):  # toms
        base = {41: 80, 43: 95, 45: 115, 47: 135, 48: 160, 50: 185}[m]
        f = base * (1 + 0.35 * np.exp(-t * 30))
        x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7)
        x += bandpass(RNG.standard_normal(n), 150, 1500) * np.exp(-t * 40) * 0.5
        return x
    if m in (49, 57, 52, 55):  # crash
        x = highpass(RNG.standard_normal(n), 3000) * np.exp(-t * 1.8)
        x += 0.3 * sum(np.sin(2 * np.pi * f * t) for f in (3150, 4870, 6630)) / 3 * np.exp(-t * 2.5)
        return x * 0.35 * np.minimum(1, t / 0.002)
    return anvil(m, n)


def anvil(m, n):
    t = np.arange(n) / SR
    f = hz(m) if m > 60 else 900
    x = sum(a * np.sin(2 * np.pi * f * r * t) * np.exp(-t * d)
            for r, a, d in ((1, 1, 5), (2.76, 0.6, 7), (5.40, 0.35, 10), (8.93, 0.2, 14)))
    x += highpass(RNG.standard_normal(n), 2000) * np.exp(-t * 60) * 0.4
    return x * np.minimum(1, t / 0.001)


def synth(kind, m, dur):
    f = hz(m)
    if kind == "strings" and dur < STACCATO_BELOW:
        kind = "strings_stacc"
    if kind in ("strings", "strings_stacc", "choir", "horn", "flute", "brass", "trumpet",
                "oboe", "clarinet"):
        rel = {"strings": 0.45, "strings_stacc": 0.12, "choir": 0.9, "horn": 0.35,
               "flute": 0.25, "brass": 0.15, "trumpet": 0.12, "oboe": 0.2,
               "clarinet": 0.25}[kind]
        n = int((dur + rel) * SR)
        t = np.arange(n) / SR
        if kind in ("strings", "strings_stacc"):
            saw = [1 / k for k in range(1, 40)]
            x = sum(additive(f, t, saw, vib=0.004, vib_rate=5.2 + d * 30, detune=d)
                    for d in (-0.003, 0.0, 0.0035)) / 3
            if kind == "strings":
                x = lowpass(x, 2200 + f * 2)
                e = env(n, 0.18, rel, dur)
            else:  # short bowed attack with a little bite
                x = lowpass(x, 3000 + f * 3)
                e = env(n, 0.012, rel, dur) * (0.75 + 0.25 * np.exp(-t * 18))
        elif kind == "horn":
            w = [1 / k ** 1.2 for k in range(1, 16)]
            x = additive(f, t, w, vib=0.002) + 0.6 * additive(f, t, w, detune=0.002)
            x = lowpass(x, 1100 + f)
            e = env(n, 0.12, rel, dur)
        elif kind in ("brass", "trumpet"):
            # brassy: harmonics open up during the attack ("blat"), then settle
            w = [1 / k ** 0.9 for k in range(1, 30)]
            x = additive(f, t, w, vib=0.0015) + 0.5 * additive(f, t, w, detune=0.003)
            bright = (1800 if kind == "brass" else 3500) + f * 2
            x = lowpass(x, bright) * 0.7 + lowpass(x, bright * 2.2) * 0.3 * np.exp(-t * 6)
            e = env(n, 0.03, rel, dur) * (0.8 + 0.3 * np.exp(-t * 10))
        elif kind == "oboe":
            # reedy and nasal: strong upper harmonics, pushed through a formant near 1.3 kHz
            w = [1, 0.9, 0.8, 0.55, 0.4, 0.3, 0.2, 0.14, 0.1, 0.07]
            src = additive(f, t, w, vib=0.005, vib_rate=5.4)
            x = src * 0.6 + bandpass(src, 1000, 1700) * 1.2
            e = env(n, 0.05, rel, dur)
        elif kind == "clarinet":
            # hollow: odd harmonics dominate in the low register
            w = [1, 0.08, 0.5, 0.06, 0.3, 0.05, 0.18, 0.04, 0.1]
            x = lowpass(additive(f, t, w, vib=0.003, vib_rate=4.8), 2500 + f)
            e = env(n, 0.06, rel, dur)
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
        while f < 70:  # keep below-range notes on a playable drum
            f *= 2
        n = int((dur + 2.0) * SR)
        t = np.arange(n) / SR
        fp = f * (1 + 0.04 * np.exp(-t * 20))
        ph = 2 * np.pi * np.cumsum(fp) / SR
        x = (np.sin(ph) + 0.5 * np.sin(1.5 * ph) * np.exp(-t * 3)) * np.exp(-t * 1.3)
        x += lowpass(RNG.standard_normal(n), 400) * np.exp(-t * 30) * 0.6
        return x
    if kind == "drums":
        return drum(m, int(2.0 * SR))
    if kind == "anvil":
        return anvil(m, int(1.5 * SR))
    raise ValueError(kind)


def score_gain(beat_pos):
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
    head += b"\x00\xff\x51\x03" + int(60_000_000 / SCORE_BPM).to_bytes(3, "big")
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


def events_from_score(src, out):
    """-> (events [(kind, gain, pan, midi, start_s, dur_s)], loop length in seconds)"""
    notes = json.load(open(src))
    for nt in notes:
        nt["midi"] = midi_pitch(nt["staff"], nt["steps"])
    write_midi(notes, out + ".mid")
    beat_s = 60.0 / SCORE_BPM
    events = []
    for nt in notes:
        kind, gain, pan, _, _ = PARTS[nt["staff"]]
        beat = nt["bar"] * BAR_BEATS + nt["onset"]
        events.append((kind, gain * score_gain(beat), pan, nt["midi"], beat * beat_s, nt["dur"] * beat_s))
    bars = max(n["bar"] for n in notes) + 1
    return events, bars * BAR_BEATS * beat_s


def events_from_midi(src):
    import mido
    mid = mido.MidiFile(src)
    tempo, bar_beats = 500000, BAR_BEATS
    for msg in mid.tracks[0]:
        if msg.type == "set_tempo":
            tempo = msg.tempo
        elif msg.type == "time_signature":
            bar_beats = msg.numerator * 4 / msg.denominator
    tick_s = tempo / 1e6 / mid.ticks_per_beat
    bar_ticks = mid.ticks_per_beat * bar_beats
    events, last_tick = [], 0
    for track in mid.tracks:
        part = next((v for k, v in MIDI_PARTS.items() if track.name.startswith(k)), None)
        if part is None:
            continue
        kind, gain, pan = part
        tick, held = 0, {}
        for msg in track:
            tick += msg.time
            if msg.type == "note_on" and msg.velocity > 0:
                held.setdefault(msg.note, []).append((tick, msg.velocity))
            elif msg.type in ("note_on", "note_off") and held.get(msg.note):
                start, vel = held[msg.note].pop(0)
                events.append((kind, gain * vel / 100, pan, msg.note,
                               start * tick_s, max(tick - start, 1) * tick_s))
                last_tick = max(last_tick, tick)
    # loop at the end of the last bar that has notes in it
    return events, math.ceil(last_tick / bar_ticks - 0.05) * bar_ticks * tick_s


def level_quiet_passages(loop, max_boost_db=12.0, keep=0.4):
    """Lift passages far below the track's average loudness (e.g. a sparse intro) so they
    stay audible in game, while keeping some of the dynamics (`keep` of the dB difference).
    The loudness envelope is computed circularly, so the loop seam stays seamless."""
    mono = (loop ** 2).mean(axis=0)
    win = int(2.0 * SR)
    padded = np.concatenate([mono[-win:], mono, mono[:win]])
    kernel = np.hanning(2 * win + 1)
    kernel /= kernel.sum()
    rms = np.sqrt(fftconvolve(padded, kernel, mode="same")[win:-win] + 1e-12)
    target = np.sqrt(mono.mean())
    boost_db = np.clip(20 * np.log10(target / rms) * (1 - keep), 0, max_boost_db)
    return loop * 10 ** (boost_db / 20)


def limit(loop, target_rms_db=-12.5, ceiling=0.89):
    """Brings the track to a normal music loudness. Drum and brass peaks would otherwise cap
    the level, so a smooth look-ahead limiter turns down only the moments that would clip."""
    from scipy.ndimage import minimum_filter1d
    from scipy.signal import filtfilt
    rms = np.sqrt((loop ** 2).mean())
    x = loop * (10 ** (target_rms_db / 20) / rms)
    peak = np.abs(x).max(axis=0)
    gain = np.minimum(1.0, ceiling / np.maximum(peak, 1e-9))
    gain = minimum_filter1d(gain, int(0.012 * SR), mode="wrap")
    # one-pole smoothing run forwards and backwards: ~30 ms ramps around each peak
    a = np.exp(-1 / (0.03 * SR))
    gain = filtfilt([1 - a], [1, -a], gain)
    x *= np.minimum(gain, ceiling / np.maximum(np.abs(x).max(axis=0), 1e-9))
    return x


def main(src, out):
    if src.lower().endswith(".mid"):
        events, loop_s = events_from_midi(src)
    else:
        events, loop_s = events_from_score(src, out)

    length = int(loop_s * SR)
    pad = 4 * SR
    mix = np.zeros((2, length + pad))
    for kind, gain, pan, m, start_s, dur_s in events:
        x = synth(kind, m, dur_s) * gain
        s = int(start_s * SR)
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
    loop = level_quiet_passages(loop)
    loop = limit(loop)
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
    print("%.1f s, %d notes" % (length / SR, len(events)))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
