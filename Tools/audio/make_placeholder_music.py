#!/usr/bin/env python3
"""Render PLACEHOLDER music for CarsGame into Assets/StreamingAssets/Music/*.mp3.

Stand-ins so the music system can be built and tested before licensed tracks exist; replace the
files with real ones under the same names. Everything is synthesized with numpy (no samples, no
third-party audio), so the output carries no licensing questions.

Loops are rendered as exact bar counts and filtered circularly (FFT), so reverb and filter tails
wrap around to the start and the loop point is seamless in the rendered PCM.

Usage: python3 Tools/audio/make_placeholder_music.py [out_dir]   (needs numpy and ffmpeg)
"""
import os
import subprocess
import sys

import numpy as np

SR = 44100
RNG = np.random.default_rng(7)


def note_hz(n):
    """MIDI note number to Hz."""
    return 440.0 * 2 ** ((n - 69) / 12)


def saw(f, t):
    # Band-limited-ish saw: sum of the first harmonics under Nyquist/2.
    out = np.zeros_like(t)
    k = 1
    while k * f < SR / 4 and k <= 24:
        out += np.sin(2 * np.pi * k * f * t) / k
        k += 1
    return out * 0.6


def square(f, t):
    out = np.zeros_like(t)
    k = 1
    while k * f < SR / 4 and k <= 23:
        out += np.sin(2 * np.pi * k * f * t) / k
        k += 2
    return out * 0.8


def env(n, attack, release, total=None):
    """Linear attack, exponential decay over n samples."""
    a = max(1, int(attack * SR))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a)
    e[a:] = np.exp(-np.arange(n - a) / (release * SR))
    return e


def fft_filter(x, lo=None, hi=None):
    """Circular low/high-pass (smooth 2nd-order-like magnitude); circular keeps loops seamless."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    g = np.ones_like(f)
    if hi is not None:
        g /= np.sqrt(1 + (f / hi) ** 4)
    if lo is not None:
        g *= (f / lo) ** 2 / np.sqrt(1 + (f / lo) ** 4)
    return np.fft.irfft(spec * g, len(x))


def circ_reverb(x, seconds=1.6, mix=0.25):
    """Exponentially decaying noise impulse, convolved circularly."""
    n = int(seconds * SR)
    ir = RNG.standard_normal(n) * np.exp(-np.arange(n) / (0.3 * seconds * SR))
    ir = fft_filter(np.pad(ir, (0, len(x) - n)) if len(x) > n else ir[: len(x)], hi=5000)
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    wet = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(ir), len(x))
    return x * (1 - mix) + wet * mix


def place(buf, start_s, sig):
    """Add sig at start_s, wrapping past the end (loop-safe)."""
    i = int(start_s * SR) % len(buf)
    n = len(sig)
    end = i + n
    if end <= len(buf):
        buf[i:end] += sig
    else:
        first = len(buf) - i
        buf[i:] += sig[:first]
        rest = sig[first:]
        while len(rest):
            m = min(len(rest), len(buf))
            buf[:m] += rest[:m]
            rest = rest[m:]


def kick(level=1.0):
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    f = 45 + 110 * np.exp(-t * 30)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * np.exp(-t * 9) * level


def snare(level=0.6):
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    noise = fft_filter(RNG.standard_normal(n), lo=900, hi=7000)
    return (noise * 0.7 + np.sin(2 * np.pi * 185 * t) * 0.5) * np.exp(-t * 18) * level


def hat(level=0.25, open_=False):
    n = int((0.25 if open_ else 0.05) * SR)
    t = np.arange(n) / SR
    return fft_filter(RNG.standard_normal(n), lo=7000) * np.exp(-t * (10 if open_ else 70)) * level


def tone(fn, midi, dur, attack=0.005, release=0.2, level=0.3):
    n = int(dur * SR)
    t = np.arange(n) / SR
    return fn(note_hz(midi), t) * env(n, attack, release) * level


def pad(chord, dur, level=0.12):
    n = int(dur * SR)
    t = np.arange(n) / SR
    s = np.zeros(n)
    for m in chord:
        for det in (-0.08, 0.08):
            s += saw(note_hz(m) * 2 ** (det / 12), t)
    fade = min(n // 4, int(0.4 * SR))
    e = np.ones(n)
    e[:fade] = np.linspace(0, 1, fade)
    e[-fade:] = np.linspace(1, 0, fade)
    return s * e * level / len(chord)


def render_loop(bpm, bars, progression, style):
    beat = 60 / bpm
    bar = 4 * beat
    total = bars * bar
    n = int(round(total * SR))
    drums, bass, lead, pads = (np.zeros(n) for _ in range(4))
    for b in range(bars):
        root, chord = progression[b % len(progression)]
        t0 = b * bar
        pads_sig = pad([m + 12 for m in chord], bar)
        place(pads, t0, pads_sig)
        for s in range(16):          # sixteenth steps
            ts = t0 + s * beat / 4
            if style == "race":
                if s % 4 == 0: place(drums, ts, kick(1.0))
                if s in (4, 12): place(drums, ts, snare(0.55))
                place(drums, ts, hat(0.18 if s % 2 else 0.28))
                if s % 2 == 0:
                    octave = 12 if s in (6, 14) else 0
                    place(bass, ts, tone(saw, root - 12 + octave, beat / 2, release=0.09, level=0.5))
                arp = chord[(s * 3 + b) % len(chord)] + (24 if s % 4 == 3 else 12)
                if b % 4 != 3 or s < 8:
                    place(lead, ts, tone(square, arp, beat / 4, release=0.05, level=0.13))
            else:                    # menu: relaxed
                if s in (0, 10): place(drums, ts, kick(0.7))
                if s in (4, 12): place(drums, ts, snare(0.3))
                if s % 4 == 2: place(drums, ts, hat(0.12))
                if s % 8 == 0: place(bass, ts, tone(saw, root - 12, beat * 1.8, release=0.5, level=0.35))
                if s % 2 == 0 and (s // 2 + b) % 3 != 2:
                    arp = chord[(s // 2) % len(chord)] + 12
                    place(lead, ts, tone(lambda f, t: np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t), arp, beat, release=0.25, level=0.18))
    bass = fft_filter(bass, hi=900 if style == "race" else 500)
    lead = fft_filter(lead, hi=3500 if style == "race" else 2500)
    pads = fft_filter(pads, hi=1800)
    music = circ_reverb(lead + pads, mix=0.3) + bass + drums * (0.9 if style == "race" else 0.6)
    # A little width: delay the lead slightly on the right.
    d = int(0.011 * SR)
    left = music
    right = music + np.roll(circ_reverb(lead, 1.0, 0.5), d) * 0.3 - lead * 0.3
    return np.stack([left, right], axis=1)


def render_sting(chords, bpm, fall=False):
    beat = 60 / bpm
    total = len(chords) * beat + 2.2
    n = int(total * SR)
    out = np.zeros(n)
    for i, chord in enumerate(chords):
        last = i == len(chords) - 1
        dur = (2.4 if last else beat)
        for m in chord:
            sig = tone(saw, m + 12, dur, release=0.9 if last else 0.18, level=0.16)
            place_s = int(i * beat * SR)
            out[place_s:place_s + len(sig)] += sig[: n - place_s]
        sig = tone(saw, chord[0] - 12, dur, release=0.8 if last else 0.2, level=0.35)
        out[int(i * beat * SR):int(i * beat * SR) + len(sig)] += sig[: n - int(i * beat * SR)]
        k = kick(0.8 if not fall else 0.5)
        s0 = int(i * beat * SR)
        out[s0:s0 + len(k)] += k[: n - s0]
    out = fft_filter(np.pad(out, (0, SR)), hi=2800 if not fall else 1400)[:n]
    fade = int(0.5 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    out = circ_reverb(np.pad(out, (0, SR)), 1.8, 0.3)[:n]
    return np.stack([out, out], axis=1)


def write_mp3(path, stereo):
    peak = np.max(np.abs(stereo)) + 1e-9
    x = np.tanh(stereo / peak * 1.3) * 0.89
    pcm = (x * 32767).astype("<i2").tobytes()
    subprocess.run(
        ["ffmpeg", "-y", "-loglevel", "error", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-",
         "-c:a", "libmp3lame", "-b:a", "96k", "-write_xing", "1", path],
        input=pcm, check=True)
    print(f"{path}: {len(x) / SR:.1f} s, {os.path.getsize(path) // 1024} KB")


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.path.dirname(__file__), "..", "..", "Assets", "StreamingAssets", "Music")
    os.makedirs(out_dir, exist_ok=True)
    Am, F, C, G, Em, D, Dm, Bb = ([57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62],
                                  [52, 55, 59], [50, 54, 57], [50, 53, 57], [46, 50, 53])
    menu = [(45, Am), (41, F), (48, C), (43, G)]
    race_a = [(40, Em), (40, Em), (48, C), (50, D)]
    race_b = [(38, Dm), (46, Bb), (48, C), (45, Am)]
    write_mp3(os.path.join(out_dir, "menu.mp3"), render_loop(96, 16, menu, "menu"))
    write_mp3(os.path.join(out_dir, "race_a.mp3"), render_loop(140, 16, race_a, "race"))
    write_mp3(os.path.join(out_dir, "race_b.mp3"), render_loop(150, 16, race_b, "race"))
    write_mp3(os.path.join(out_dir, "win.mp3"), render_sting([[60, 64, 67], [62, 65, 69], [64, 67, 71], [65, 69, 72, 76]], 180))
    write_mp3(os.path.join(out_dir, "lose.mp3"), render_sting([[64, 67, 71], [62, 65, 69], [60, 63, 67], [57, 60, 63]], 110, fall=True))


if __name__ == "__main__":
    main()
