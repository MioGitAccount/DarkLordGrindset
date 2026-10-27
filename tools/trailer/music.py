"""Synthesizes the trailer's epic music (~62 s, D minor, 120 BPM) into a 48 kHz stereo WAV.

    python tools/trailer/music.py out.wav

One bar is 2 s; the trailer is cut to it (see make_trailer.py):
  0-22   intro: drone, low strings, sparse taiko, choir from bar 5, riser into the impact
  22     impact, then the battle section: taiko ostinato, string ostinato, brass, choir
  38     lift: higher strings and brass
  54     final impact under the title card, ring-out to the end
"""
import sys

import numpy as np
from scipy import signal

SR = 48000
BEAT = 0.5
BAR = 2.0
LENGTH = 62.0
N = int(SR * LENGTH)
rng = np.random.default_rng(7)

A4 = 440.0
NOTE = {n: i for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}


def hz(name):
    # "D3", "A#2" -> frequency
    pitch, octave = name[:-1], int(name[-1])
    return A4 * 2 ** ((NOTE[pitch] + 12 * (octave + 1) - 69) / 12)


# --- Oscillators ----------------------------------------------------------------

TABLE = 4096


def _saw_table(max_harmonic):
    phase = np.arange(TABLE) / TABLE
    wave = np.zeros(TABLE)
    for k in range(1, max_harmonic + 1):
        wave += np.sin(2 * np.pi * k * phase) / k
    return wave / np.abs(wave).max()


# Band-limited saws, one per octave of the fundamental, so high notes don't alias
SAW_TABLES = {}


def saw(freq, n, vibrato=0.0, vib_rate=5.0, phase0=None):
    octave = int(np.clip(np.floor(np.log2(freq / 27.5)), 0, 9))
    if octave not in SAW_TABLES:
        SAW_TABLES[octave] = _saw_table(max(1, int(12000 / (27.5 * 2 ** (octave + 1)))))
    table = SAW_TABLES[octave]
    t = np.arange(n) / SR
    f = freq * (1 + vibrato * np.sin(2 * np.pi * vib_rate * t + rng.uniform(0, 6.28)))
    phase = (np.cumsum(f) / SR + (rng.uniform() if phase0 is None else phase0)) % 1.0
    idx = phase * TABLE
    i0 = idx.astype(int)
    frac = idx - i0
    return table[i0] * (1 - frac) + table[(i0 + 1) % TABLE] * frac


def env(n, attack, release, sustain_level=1.0, decay=0.0, curve=1.0):
    t = np.arange(n) / SR
    length = n / SR
    e = np.ones(n) * sustain_level
    if decay > 0:
        e = sustain_level + (1 - sustain_level) * np.exp(-t / decay)
    if attack > 0:
        e *= np.clip(t / attack, 0, 1) ** curve
    if release > 0:
        e *= np.clip((length - t) / release, 0, 1)
    return e


def lowpass(x, cutoff, order=2):
    sos = signal.butter(order, min(cutoff, SR / 2 - 100), "low", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def highpass(x, cutoff, order=2):
    sos = signal.butter(order, cutoff, "high", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def bandpass(x, low, high, order=2):
    sos = signal.butter(order, [low, high], "band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


# --- Mix bus ------------------------------------------------------------------

class Bus:
    def __init__(self):
        self.l = np.zeros(N)
        self.r = np.zeros(N)

    def add(self, x, start, gain=1.0, pan=0.0):
        i = int(start * SR)
        if i >= N:
            return
        x = x[: N - i]
        left = np.cos((pan + 1) * np.pi / 4)
        right = np.sin((pan + 1) * np.pi / 4)
        self.l[i:i + len(x)] += x * gain * left * 1.414
        self.r[i:i + len(x)] += x * gain * right * 1.414


dry = Bus()   # drums, close sounds
wet = Bus()   # orchestra, sent through the big reverb


# --- Instruments -------------------------------------------------------------

def strings(note, start, dur, gain, bus=wet, attack=0.6, release=0.8, bright=2500, voices=5, pan_spread=0.7):
    n = int(SR * (dur + release))
    f = hz(note)
    for v in range(voices):
        detune = 1 + (v - (voices - 1) / 2) * 0.0025
        x = saw(f * detune, n, vibrato=0.003, vib_rate=5 + v * 0.3)
        x = lowpass(x, bright)
        x *= env(n, attack, release, curve=1.5)
        pan = (v / max(1, voices - 1) * 2 - 1) * pan_spread
        bus.add(x, start, gain / voices, pan)


def staccato(note, start, gain, bus=wet, length=0.18, bright=3500):
    n = int(SR * (length + 0.15))
    f = hz(note)
    for v, pan in enumerate((-0.5, 0.0, 0.5)):
        x = saw(f * (1 + (v - 1) * 0.003), n)
        x = lowpass(x, bright)
        x *= env(n, 0.008, 0.12, sustain_level=0.0, decay=length * 0.7)
        bus.add(x, start, gain / 3, pan)


def brass(note, start, dur, gain, bus=wet, swell=0.25, release=0.5):
    n = int(SR * (dur + release))
    f = hz(note)
    out = np.zeros(n)
    for v in range(3):
        out += saw(f * (1 + (v - 1) * 0.002), n, vibrato=0.002)
    # Filter that opens with the note: crossfade a dark and a bright version
    e = env(n, swell, release, curve=1.2)
    dark = lowpass(out, 500)
    bright = lowpass(out, 2600)
    x = (dark * (1 - e) + bright * e) * e
    x = np.tanh(x * 1.5) / 1.5
    bus.add(x, start, gain / 3, 0.0)


FORMANTS_AH = [(800, 1.0, 80), (1150, 0.5, 90), (2900, 0.25, 120)]


def choir(note, start, dur, gain, bus=wet, attack=1.2, release=1.5):
    n = int(SR * (dur + release))
    f = hz(note)
    src = np.zeros(n)
    for v in range(6):
        src += saw(f * (1 + (v - 2.5) * 0.004), n, vibrato=0.006, vib_rate=4.5 + v * 0.2)
    src += rng.normal(0, 0.15, n)  # breath
    x = np.zeros(n)
    for freq, amp, bw in FORMANTS_AH:
        x += bandpass(src, freq - bw, freq + bw) * amp
    x *= env(n, attack, release, curve=1.5)
    for pan in (-0.6, 0.6):
        bus.add(x, start + (0.012 if pan > 0 else 0), gain / 2, pan)


def taiko(start, gain, pitch=58.0, bus=dry, pan=0.0, decay=0.45):
    n = int(SR * 1.2)
    t = np.arange(n) / SR
    f = pitch * (1 + 0.8 * np.exp(-t / 0.03))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay)
    skin = bandpass(rng.normal(0, 1, n), 150, 900) * np.exp(-t / 0.04) * 0.6
    click = highpass(rng.normal(0, 1, n), 2000) * np.exp(-t / 0.006) * 0.25
    x = np.tanh((body + skin + click) * 1.6)
    bus.add(x, start, gain, pan)
    wet.add(x, start, gain * 0.25, pan)


def snare_roll_hit(start, gain):
    n = int(SR * 0.4)
    t = np.arange(n) / SR
    x = bandpass(rng.normal(0, 1, n), 800, 6000) * np.exp(-t / 0.08)
    dry.add(x, start, gain, rng.uniform(-0.3, 0.3))


def impact(start, gain):
    n = int(SR * 6)
    t = np.arange(n) / SR
    f = 30 + 70 * np.exp(-t / 0.15)
    sub = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 1.6)
    crack = lowpass(rng.normal(0, 1, n), 5000) * np.exp(-t / 0.25) * 0.5
    x = np.tanh((sub * 1.2 + crack) * 1.4)
    dry.add(x, start, gain)
    wet.add(x, start, gain * 0.6)
    # Low cymbal wash
    wash = bandpass(rng.normal(0, 1, n), 3000, 12000) * np.exp(-t / 1.4) * 0.12
    wet.add(wash, start, gain, -0.3)
    wet.add(np.roll(wash, 300), start, gain, 0.3)


def riser(start, dur, gain):
    n = int(SR * dur)
    t = np.arange(n) / SR
    k = t / dur
    noise = rng.normal(0, 1, n)
    # Sweep a band upward in pieces
    x = np.zeros(n)
    pieces = 24
    for p in range(pieces):
        a, b = int(n * p / pieces), int(n * (p + 1) / pieces)
        centre = 300 * (30 ** (p / pieces))
        seg = bandpass(noise[max(0, a - 2000):b], centre * 0.7, min(centre * 1.4, 20000))
        x[a:b] = seg[-(b - a):]
    x *= k ** 2.2
    # Rising tone underneath
    f = 80 * (8 ** k)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * k ** 3 * 0.25
    wet.add(x * 0.5 + tone, start, gain)
    # Reverse-cymbal swell
    sw = bandpass(rng.normal(0, 1, n), 4000, 14000) * k ** 4
    dry.add(sw, start, gain * 0.5)


def drone(note, start, dur, gain):
    n = int(SR * dur)
    f = hz(note)
    x = np.sin(2 * np.pi * f * np.arange(n) / SR)
    x += 0.5 * lowpass(saw(f, n), 300)
    x *= env(n, 3.0, 4.0)
    dry.add(x, start, gain * 0.6)
    wet.add(x, start, gain * 0.4)


# --- The score -----------------------------------------------------------------

# Chords as (bass, [upper voices]); one per bar
Dm = ("D2", ["D3", "F3", "A3"])
Bb = ("A#1", ["D3", "F3", "A#3"])
F = ("F2", ["C3", "F3", "A3"])
C = ("C2", ["C3", "E3", "G3"])
Gm = ("G1", ["D3", "G3", "A#3"])
A = ("A1", ["C#3", "E3", "A3"])


def up(note, octaves=1):
    return note[:-1] + str(int(note[-1]) + octaves)


# Intro, bars 0-10 (0-22 s)
drone("D1", 0, 24, 0.35)
intro = [Dm, Dm, Bb, Bb, F, F, C, C, Gm, A, A]
for bar, (bass, chord) in enumerate(intro):
    t = bar * BAR
    strings(bass, t, BAR, 0.18 + bar * 0.012, attack=1.0, release=1.0, bright=900)
    if bar >= 2:
        for v in chord:
            strings(v, t, BAR, 0.05 + bar * 0.006, attack=1.0, release=1.0, bright=1600)
    if bar >= 5:
        for v in chord:
            choir(up(v), t, BAR, 0.05 + (bar - 5) * 0.012, attack=1.0, release=1.2)
    # Sparse taiko: bar starts, then building
    taiko(t, 0.55, pitch=52)
    if bar >= 4:
        taiko(t + 1.5, 0.35, pitch=60, pan=0.3)
    if bar >= 7:
        taiko(t + 1.0, 0.4, pitch=56, pan=-0.3)
        taiko(t + 1.75, 0.3, pitch=64, pan=0.3)
# Build into the impact: taiko roll and riser
for i in range(16):
    taiko(18 + i * 0.25, 0.25 + i * 0.03, pitch=60 + (i % 2) * 6, pan=(-0.4 if i % 2 else 0.4))
for i in range(16):
    snare_roll_hit(20 + i * 0.125, 0.05 + i * 0.012)
riser(14, 8, 0.35)

# Battle section, bars 11-26 (22-54 s)
impact(22, 0.9)
battle = [Dm, Dm, Bb, Bb, F, F, C, A,   # 22-38
          Dm, Bb, F, C, Gm, Gm, A, A]  # 38-54 (lift)
ostinato = [0, 0, 1, 0, 2, 0, 1, 2]  # which chord tone, per 8th note
for bar, (bass, chord) in enumerate(battle):
    t = 22 + bar * BAR
    lift = bar >= 8
    # Bass: long low strings + brass
    strings(bass, t, BAR, 0.3, attack=0.05, release=0.4, bright=1200)
    strings(up(bass), t, BAR, 0.14, attack=0.05, release=0.4, bright=1500)
    for v in chord:
        brass(v, t, BAR * 0.9, 0.11 if not lift else 0.15, swell=0.3)
        choir(up(v), t, BAR, 0.11 if not lift else 0.15, attack=0.4, release=0.8)
    if lift:
        for v in chord:
            strings(up(v), t, BAR, 0.06, attack=0.2, release=0.5, bright=4000, voices=4)
    # String ostinato in 8th notes
    octave = 1 if not lift else 2
    for i, idx in enumerate(ostinato * 2):
        staccato(up(chord[idx], octave), t + i * BEAT / 2, 0.11 if not lift else 0.13)
    # Taiko ostinato: low on 1 and 3, mid hits between, 16th fill on the last beat of every 2nd bar
    for b in range(4):
        taiko(t + b * BEAT, 0.7 if b % 2 == 0 else 0.45, pitch=50 if b % 2 == 0 else 62, pan=0.0)
        taiko(t + b * BEAT + 0.375, 0.3, pitch=70, pan=0.35)
    taiko(t + 0.75, 0.35, pitch=66, pan=-0.35)
    if bar % 2 == 1:
        for i in range(4):
            taiko(t + 1.5 + i * 0.125, 0.3 + i * 0.08, pitch=72 - i * 3, pan=-0.2 + i * 0.13)
    if bar in (8,):
        impact(t, 0.5)
# Last bars swell into the title hit
riser(50, 4, 0.3)
for i in range(16):
    taiko(52 + i * 0.125, 0.25 + i * 0.035, pitch=58 + (i % 3) * 5, pan=(-0.4 if i % 2 else 0.4))

# Title, 54 s: final chord and ring-out
impact(54, 1.0)
taiko(54, 0.9, pitch=45, decay=0.9)
for v in ["D2", "A2", "D3", "F3", "A3", "D4"]:
    strings(v, 54, 4.5, 0.12, attack=0.05, release=3.5, bright=2200)
for v in ["D4", "F4", "A4"]:
    choir(v, 54, 4.5, 0.14, attack=0.2, release=3.5)
for v in ["D3", "A3", "F3"]:
    brass(v, 54, 3.0, 0.14, swell=0.15, release=2.0)

# --- Mixdown ------------------------------------------------------------------

def reverb_ir(seconds, seed):
    r = np.random.default_rng(seed)
    n = int(SR * seconds)
    t = np.arange(n) / SR
    ir = r.normal(0, 1, n) * np.exp(-t / (seconds / 6.5))
    ir = lowpass(ir, 6000)
    ir[: int(SR * 0.02)] *= np.linspace(0, 1, int(SR * 0.02))  # pre-delay-ish fade
    return ir / np.sqrt(np.sum(ir ** 2))


ir_l, ir_r = reverb_ir(3.2, 1), reverb_ir(3.2, 2)
wet_l = signal.fftconvolve(wet.l, ir_l)[:N]
wet_r = signal.fftconvolve(wet.r, ir_r)[:N]
mix_l = dry.l + wet.l * 0.75 + wet_l * 0.55
mix_r = dry.r + wet.r * 0.75 + wet_r * 0.55
mix = np.stack([mix_l, mix_r], axis=1)
mix = highpass(mix.T, 25).T

# Gentle glue compression (RMS-based gain), then soft clip and normalize
rms = np.sqrt(lowpass(np.mean(mix ** 2, axis=1), 8) + 1e-9)
threshold = np.percentile(rms, 60)
gain = np.where(rms > threshold, (threshold / rms) ** 0.22, 1.0)
mix *= gain[:, None]
mix /= np.abs(mix).max()
mix = np.tanh(mix * 1.3) / np.tanh(1.3)
fade = np.ones(N)
fade[-int(SR * 2):] = np.linspace(1, 0, int(SR * 2)) ** 2
mix *= fade[:, None]
mix *= 0.89 / np.abs(mix).max()  # about -1 dBFS

out = sys.argv[1] if len(sys.argv) > 1 else "music.wav"
from scipy.io import wavfile
wavfile.write(out, SR, (mix * 32767).astype(np.int16))
print("wrote", out, f"{LENGTH:.0f}s")
