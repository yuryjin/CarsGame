#!/usr/bin/env python3
"""Build CarsGame sound effects from CC0 / public-domain recordings into Assets/CarsGame/Resources/Sfx/*.ogg.

Sources (all CC0, listed in Tools/audio/SFX_SOURCES.md) are downloaded into ~/.cache/cars-audio-src on
first run. Each clip is cut, layered, faded, peak-normalized and written as mono 22.05 kHz Ogg Vorbis, which
keeps the whole set small (the Yandex zip has a size budget). Clip names are what SfxLibrary loads; a missing
clip falls back to the procedural SfxSynth sound, so the game never goes silent.

Usage: python3 Tools/audio/make_sfx.py      (needs numpy, ffmpeg, curl, unzip)
"""
import os
import subprocess
import sys

import numpy as np

SR = 22050
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "CarsGame", "Resources", "Sfx")
SRC = os.path.expanduser("~/.cache/cars-audio-src")
RNG = np.random.default_rng(11)

KENNEY = {  # pack -> page (zip link is scraped from the page, as the site versions it)
    "sci-fi-sounds": "https://kenney.nl/assets/sci-fi-sounds",
    "impact-sounds": "https://kenney.nl/assets/impact-sounds",
    "interface-sounds": "https://kenney.nl/assets/interface-sounds",
    "digital-audio": "https://kenney.nl/assets/digital-audio",
}
OGA = {  # local dir -> (page, files)
    "oga-engine": ("https://opengameart.org/content/racing-car-engine-sound-loops", ["loop_0.wav"]),
    "oga-gunshots": ("https://opengameart.org/content/gunshots", ["22%20Pistol.wav", "Black%20Powder.wav", "Unkown.wav"]),
    "oga-gunshot-sounds": ("https://opengameart.org/content/gunshot-sounds", ["sounds.zip"]),
}


def fetch():
    os.makedirs(SRC, exist_ok=True)
    for pack, page in KENNEY.items():
        if os.path.isdir(os.path.join(SRC, pack)):
            continue
        html = subprocess.run(["curl", "-s", page], capture_output=True, text=True).stdout
        import re
        url = re.search(r"https://kenney\.nl/media/pages/assets/%s/[^']+\.zip" % pack, html).group(0)
        z = os.path.join(SRC, pack + ".zip")
        subprocess.run(["curl", "-s", "-o", z, url], check=True)
        subprocess.run(["unzip", "-oq", z, "-d", os.path.join(SRC, pack)], check=True)
    for d, (page, files) in OGA.items():
        os.makedirs(os.path.join(SRC, d), exist_ok=True)
        for f in files:
            p = os.path.join(SRC, d, f)
            if not os.path.exists(p):
                subprocess.run(["curl", "-s", "-o", p, "https://opengameart.org/sites/default/files/" + f], check=True)
                if f.endswith(".zip"):
                    subprocess.run(["unzip", "-oq", p, "-d", os.path.join(SRC, d)], check=True)


def load(rel, start=0.0, dur=None, speed=1.0):
    """Mono float at SR; speed > 1 raises pitch and shortens (tape-style resample)."""
    path = os.path.join(SRC, rel)
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(int(SR / speed)),
                          "-f", "f32le", "-"], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, "<f4").astype(np.float64)
    s = int(start * SR / speed)
    x = x[s:] if dur is None else x[s:s + int(dur * SR / speed)]
    return x - np.mean(x)


def fade(x, fin=0.002, fout=0.05):
    x = x.copy()
    a, b = max(1, int(fin * SR)), max(1, int(fout * SR))
    x[:a] *= np.linspace(0, 1, a)
    x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def lowpass(x, hz):
    spec = np.fft.rfft(np.pad(x, (0, 1024)))
    f = np.fft.rfftfreq(len(x) + 1024, 1 / SR)
    return np.fft.irfft(spec / np.sqrt(1 + (f / hz) ** 4), len(x) + 1024)[:len(x)]


def mix(*layers):
    n = max(len(l) for l, _ in layers)
    out = np.zeros(n)
    for l, g in layers:
        out[:len(l)] += l * g
    return out


def thump(freq=70, dur=0.3, drop=2.2):
    """Synth body for cannons/launchers: a falling sine, the part recordings of small guns lack."""
    t = np.arange(int(dur * SR)) / SR
    f = freq * (1 + drop * np.exp(-t * 35))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)


def whoosh(dur=0.28, lo=500, hi=2500):
    n = int(dur * SR)
    noise = RNG.standard_normal(n)
    out = np.zeros(n)
    blocks = 14
    for i in range(blocks):  # swept band: filter the whole buffer per block, keep that block
        a, b = i * n // blocks, (i + 1) * n // blocks
        c = lo + (hi - lo) * np.sin(np.pi * (i + 0.5) / blocks)
        out[a:b] = lowpass(noise, c)[a:b] - lowpass(noise, c * 0.5)[a:b]
    return out * np.sin(np.pi * np.arange(n) / n) ** 1.5


def buzz(dur=0.4, f=95):
    t = np.arange(int(dur * SR)) / SR
    v = sum(np.sin(2 * np.pi * f * k * t) / k for k in range(1, 30)) * (0.6 + 0.4 * np.sin(2 * np.pi * 31 * t))
    return lowpass(v + RNG.standard_normal(len(t)) * 0.25, 3500)


def seamless_loop(x, xf=0.08):
    """Crossfade the tail into the head: the result loops without a click."""
    L = int(xf * SR)
    y = x[:len(x) - L].copy()
    w = np.linspace(0, 1, L)
    y[:L] = x[:L] * np.sqrt(w) + x[len(x) - L:] * np.sqrt(1 - w)
    return y


K_SCI, K_IMP, K_UI, K_DIG = "sci-fi-sounds/Audio/", "impact-sounds/Audio/", "interface-sounds/Audio/", "digital-audio/Audio/"
GUN, GUNS = "oga-gunshots/", "oga-gunshot-sounds/sounds/"


def build():
    c = {}
    # --- weapons: fire ---
    c["fire_cannon"] = fade(mix((lowpass(load(GUN + "Black%20Powder.wav", 0.13, 0.45), 7000), 1.0), (thump(65, 0.35), 0.7)), fout=0.15)
    c["fire_heavy"] = fade(mix((lowpass(load(GUN + "Unkown.wav", 0.25, 0.7, speed=0.8), 5000), 1.0), (thump(48, 0.5, 2.8), 0.9)), fout=0.25)
    c["fire_rapid"] = fade(load(GUNS + "sks.wav", 2.29, 0.2), fout=0.06)
    c["fire_auto"] = fade(mix((load(GUNS + "cz.wav", 0.22, 0.3), 1.0), (thump(90, 0.15), 0.4)), fout=0.1)
    c["fire_pistol"] = fade(load(GUN + "22%20Pistol.wav", 0.13, 0.22), fout=0.08)
    c["fire_shotgun"] = fade(load(GUNS + "shotty.wav", 0.02, 0.62), fout=0.2)
    c["fire_sniper"] = fade(load(GUNS + "mosin.wav", 0.43, 1.0), fout=0.45)
    c["fire_laser"] = fade(load(K_SCI + "laserSmall_001.ogg"), fout=0.05)
    c["fire_freeze"] = fade(mix((load(K_SCI + "laserRetro_002.ogg", speed=0.8), 0.8), (load(K_SCI + "forceField_001.ogg", 0, 0.35, speed=1.4), 0.5)), fout=0.1)
    c["fire_zap"] = fade(load(K_DIG + "zap1.ogg", 0, 0.5), fout=0.15)
    c["fire_pulse"] = fade(load(K_DIG + "phaserDown1.ogg"), fout=0.1)
    c["fire_spray"] = fade(load(K_SCI + "thrusterFire_000.ogg", 0.5, 0.45), fin=0.03, fout=0.2)
    c["fire_launch"] = fade(mix((load(K_SCI + "lowFrequency_explosion_001.ogg", 0, 0.35, speed=1.5), 0.8), (thump(85, 0.25), 0.8)), fout=0.1)
    c["fire_rocket"] = fade(load(K_SCI + "thrusterFire_002.ogg", 0.4, 0.6, speed=1.2), fin=0.02, fout=0.3)
    c["fire_swing"] = fade(whoosh(), fout=0.05)
    c["fire_saw"] = fade(buzz(), fin=0.02, fout=0.12)
    c["fire_trap"] = fade(mix((load(K_UI + "drop_003.ogg"), 0.7), (load(K_IMP + "impactPlate_light_002.ogg", 0, 0.3), 0.6)), fout=0.08)
    # --- hits ---
    c["hit_metal_0"] = fade(load(K_IMP + "impactMetal_medium_000.ogg"))
    c["hit_metal_1"] = fade(load(K_IMP + "impactMetal_medium_003.ogg"))
    c["hit_metal_2"] = fade(load(K_IMP + "impactMetal_light_001.ogg"))
    c["hit_heavy"] = fade(load(K_IMP + "impactMetal_heavy_001.ogg"))
    c["hit_punch"] = fade(load(K_IMP + "impactPunch_heavy_001.ogg"))
    c["hit_pan"] = fade(load(K_IMP + "impactBell_heavy_004.ogg"))
    c["crash_light"] = fade(load(K_IMP + "impactPlate_medium_000.ogg"), fout=0.15)
    c["crash_heavy"] = fade(mix((load(K_IMP + "impactPlate_heavy_002.ogg"), 1.0), (load(K_IMP + "impactMetal_heavy_003.ogg"), 0.6)), fout=0.15)
    # --- explosions, shield, nitro, pickup ---
    c["explosion"] = fade(mix((load(K_SCI + "explosionCrunch_000.ogg"), 1.0), (load(K_SCI + "lowFrequency_explosion_001.ogg", 0, 0.8), 0.8)), fout=0.3)
    c["shield_break"] = fade(load(K_SCI + "forceField_000.ogg", 0, 0.4, speed=1.3), fout=0.15)
    c["nitro"] = fade(load(K_SCI + "thrusterFire_001.ogg", 0.3, 1.2), fin=0.04, fout=0.5)
    c["pickup"] = fade(load(K_DIG + "powerUp2.ogg"))
    # --- engine: public-domain recording, pitched so its 43 Hz fundamental matches the old 55 Hz synth loop ---
    eng = seamless_loop(load("oga-engine/loop_0.wav", speed=55 / 42.8))
    c["engine_loop"] = eng / (np.sqrt(np.mean(eng ** 2)) + 1e-9) * 0.2
    return c


def write(name, x):
    if name != "engine_loop":
        # Source files often start with ~0.1 s of silence: a gun must sound on the frame it fires.
        lead = max(0, int(np.argmax(np.abs(x) > 0.02 * np.max(np.abs(x)))) - int(0.001 * SR))
        x = x[lead:]
        x = x / (np.max(np.abs(x)) + 1e-9) * 0.85   # Vorbis overshoots a little
    path = os.path.join(OUT, name + ".ogg")
    pcm = np.clip(x, -1, 1).astype("<f4").tobytes()
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "3", path], input=pcm, check=True)
    return os.path.getsize(path), len(x) / SR


META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 6
  defaultSettings:
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.5
    conversionMode: 0
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def main():
    fetch()
    os.makedirs(OUT, exist_ok=True)
    clips = build()
    total = 0
    for name, x in sorted(clips.items()):
        size, secs = write(name, x)
        total += size
        meta = os.path.join(OUT, name + ".ogg.meta")
        if not os.path.exists(meta):
            import uuid
            open(meta, "w").write(META.format(guid=uuid.uuid4().hex))
        print(f"{name:14s} {secs:5.2f} s {size // 1024:4d} KB")
    print(f"total {total // 1024} KB, {len(clips)} clips")
    with open(os.path.join(ROOT, "Tools", "audio", "SFX_SOURCES.md"), "w") as f:
        f.write("# Sound effect sources\n\nBuilt by `Tools/audio/make_sfx.py`. All inputs are CC0 (public domain dedication).\n\n")
        f.write("| Source | License | Used for |\n|---|---|---|\n")
        f.write("| Kenney — Sci-Fi Sounds, Impact Sounds, Interface Sounds, Digital Audio (kenney.nl) | CC0 1.0 | lasers, zaps, explosions, impacts, shield, nitro, pickup, trap drop |\n")
        f.write("| domasx2 — racing car engine sound loops (opengameart.org/content/racing-car-engine-sound-loops), cut from a pdsounds.org public-domain recording | CC0 1.0 | engine loop |\n")
        f.write("| Gunshots (opengameart.org/content/gunshots) | CC0 1.0 | cannon, heavy cannon, pistol |\n")
        f.write("| Gunshot Sounds (opengameart.org/content/gunshot-sounds) | CC0 1.0 | machine gun, autocannon, shotgun, sniper |\n")
        f.write("\nSynthesized layers (numpy, no source): cannon/launcher thump, melee whoosh, saw buzz.\n")


if __name__ == "__main__":
    sys.exit(main())
