"""The sound of the animated film "Wish You Were Here".

Everything here is made by this script, so the film has no outside audio in it:
  * the score: an original piece at 96 beats a minute, written out below (a pulse, a bass, a pad, a four-bar tune and
    what is built on it) and synthesized: plucked strings, a soft kick, a warm pad, a bell-like lead, a felt piano
    (from scripts/demo/trailer_audio.py), shaker and claps made from noise;
  * the places: the harbour city at night, the coast at sunset, the two stairways and the rooftop terrace, each with
    its own air; and the small sounds in them (steps, a hand on a rail, a hat going past, a chair on stone);
  * the voices: the five lines rendered by scripts/demo/trailer_voices.ps1 (wish_lines.json) with Windows' own
    text-to-speech voices, cleaned, levelled and placed where scripts/demo/wish_cut.json says. They are synthesized
    voices, not recordings of anyone.

Writes demo-build/wish/audio/{music,places,voices,mix}.wav (48 kHz stereo).

    python scripts/demo/wish_audio.py        (needs numpy and scipy)
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from scipy import signal
from scipy.io import wavfile

import trailer_audio as ta

ROOT = Path(__file__).resolve().parents[2]
CUT = json.loads((ROOT / "scripts" / "demo" / "wish_cut.json").read_text(encoding="utf-8"))
OUT = ROOT / "demo-build" / "wish" / "audio"
ta.VOICE = ROOT / "demo-build" / "wish" / "voice"
SR = ta.SR
TOTAL = sum(s["seconds"] for s in CUT["shots"])
N = int(round(TOTAL * SR))
BEAT = 60.0 / CUT["bpm"]
BAR = BEAT * 4
rng = np.random.default_rng(75)


def hz(midi: float) -> float:
    return 440.0 * 2 ** ((midi - 69) / 12)


def env(n: int, attack: float, decay: float) -> np.ndarray:
    t = np.arange(n) / SR
    return (1 - np.exp(-t / max(attack, 1e-4))) * np.exp(-t / decay)


# ------------------------------------------------------------------------------------------- instruments

def kick(strength=1.0) -> np.ndarray:
    n = int(0.32 * SR)
    t = np.arange(n) / SR
    f = 46 + 70 * np.exp(-t / 0.035)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.11)
    click = ta.lowpass(rng.standard_normal(n), 2500) * np.exp(-t / 0.004) * 0.25
    return (x + click) * strength


def pluck(midi: float, seconds=0.7, bright=0.5, damp=0.996) -> np.ndarray:
    """A plucked string (a burst of noise going round a delay that loses its top each time round)."""
    f = hz(midi)
    period = SR / f
    whole = int(period)
    n = int(seconds * SR)
    burst = rng.uniform(-1, 1, whole)
    burst = ta.lowpass(np.concatenate([burst, burst]), 1500 + 9000 * bright)[whole:]
    x = np.zeros(n)
    x[:whole] = burst
    a = np.zeros(whole + 2)
    a[0] = 1.0
    frac = period - whole
    a[whole] = -damp * 0.5 * (1 - frac + 0.5)
    a[whole + 1] = -damp * 0.5 * (frac + 0.5)
    y = signal.lfilter([1.0], a, x)
    return y * np.minimum(1.0, np.arange(n) / (0.002 * SR)) * np.exp(-np.arange(n) / (seconds * 0.6 * SR))


def bass(midi: float, seconds: float) -> np.ndarray:
    n = int((seconds + 0.12) * SR)
    t = np.arange(n) / SR
    f = hz(midi)
    x = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * 2 * f * t + 0.3) + 0.12 * np.sin(2 * np.pi * 3 * f * t)
    e = (1 - np.exp(-t / 0.012)) * np.exp(-t / (seconds * 1.4)) * np.clip((seconds + 0.1 - t) / 0.1, 0, 1)
    return np.tanh(x * e * 1.6) / 1.6


def bell(midi: float, seconds=1.4, strength=1.0) -> np.ndarray:
    """The lead: a soft mallet tone, a little glassy at the start."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    f = hz(midi)
    index = 2.2 * np.exp(-t / 0.09) + 0.25
    x = np.sin(2 * np.pi * f * t + index * np.sin(2 * np.pi * f * 3.0 * t))
    x += 0.35 * np.sin(2 * np.pi * f * 2 * t) * np.exp(-t / 0.25)
    return x * (1 - np.exp(-t / 0.004)) * np.exp(-t / (seconds * 0.38)) * strength


def pad(midis, seconds: float, bright=1.0) -> np.ndarray:
    """A warm held chord: a few slightly detuned voices to each note, slow to speak."""
    n = int((seconds + 1.2) * SR)
    t = np.arange(n) / SR
    out = np.zeros((n, 2))
    for m in midis:
        f = hz(m)
        for cents, pan in ((-7, -0.6), (6, 0.6), (0, 0.0)):
            tone = np.zeros(n)
            for k in range(1, 9):
                tone += np.sin(2 * np.pi * f * k * 2 ** (cents / 1200) * t + rng.uniform(0, 6.28)) / k ** (1.9 - 0.35 * bright)
            left, right = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
            out[:, 0] += tone * left
            out[:, 1] += tone * right
    e = np.clip(t / 0.5, 0, 1) * np.clip((seconds + 1.2 - t) / 1.2, 0, 1)
    return ta.lowpass(out * e[:, None], 900 + 1500 * bright) / (len(midis) * 3)


def shaker(strength=1.0) -> np.ndarray:
    n = int(0.07 * SR)
    x = ta.bandpass(rng.standard_normal(n + 400), 5000, 11000)[400:]
    return x * np.hanning(n) ** 2 * strength


def clap() -> np.ndarray:
    n = int(0.22 * SR)
    x = ta.bandpass(rng.standard_normal(n + 400), 900, 3600)[400:]
    t = np.arange(n) / SR
    e = np.zeros(n)
    for start in (0.0, 0.011, 0.023):
        e += (t >= start) * np.exp(-np.maximum(t - start, 0) / 0.012) * 0.55
    e += (t >= 0.03) * np.exp(-np.maximum(t - 0.03, 0) / 0.06) * 0.35
    return x * e


def swell(seconds: float, lo0: float, lo1: float, power=2.0) -> np.ndarray:
    """Noise that opens up as it rises (a cymbal played backwards, more or less)."""
    n = int(seconds * SR)
    x = rng.standard_normal((n, 2))
    out = np.zeros((n, 2))
    parts = 24
    for i in range(parts):
        a, b = i * n // parts, (i + 1) * n // parts
        lo = lo0 * (lo1 / lo0) ** (i / (parts - 1))
        seg = ta.bandpass(x[max(0, a - 600):b], lo, min(lo * 5, SR * 0.45))[(a - max(0, a - 600)):]
        out[a:b] = seg
    return out * (np.linspace(0, 1, n) ** power)[:, None]


def boom() -> np.ndarray:
    n = int(2.6 * SR)
    t = np.arange(n) / SR
    low = np.sin(2 * np.pi * (38 + 22 * np.exp(-t / 0.08)) * t) * np.exp(-t / 0.55)
    wash = ta.bandpass(rng.standard_normal((n, 2)), 2500, 11000) * (np.exp(-t / 0.7) * (1 - np.exp(-t / 0.01)))[:, None] * 0.22
    return np.stack([low, low], axis=1) * 0.9 + wash


# ------------------------------------------------------------------------------------------- the score

CHORDS = {  # bass note, and the notes of the pad
    "D": (38, [50, 57, 62, 66, 69]), "A": (45, [49, 57, 61, 64, 69]), "Bm": (47, [50, 54, 59, 62, 66]), "G": (43, [50, 55, 59, 62, 67]),
    "A/C#": (37, [49, 57, 61, 64, 69]), "Asus": (45, [50, 57, 62, 64, 69]), "Em": (40, [52, 55, 59, 64, 67]), "D/F#": (42, [50, 57, 62, 66, 69]),
}
# the tune: over one bar of each chord, (beat, note, beats held)
TUNE = {
    "D": [(0, 78, 1.5), (1.5, 81, 0.5), (2, 83, 1), (3, 81, 1)],
    "A": [(0, 76, 1.5), (1.5, 78, 0.5), (2, 81, 2)],
    "Bm": [(0, 74, 1.5), (1.5, 78, 0.5), (2, 83, 1), (3, 81, 1)],
    "G": [(0, 79, 1.5), (1.5, 81, 0.5), (2, 78, 1), (3, 76, 1)],
}
# what each bar of the film plays: chord, and which layers
#   p pulse (kick)   o ostinato (plucks)   b bass   d pad   t tune   T tune an octave up as well   s shaker   c claps   r rising figure
#   q the push: shaker in sixteenths and the bass on every eighth   R the swell and roll into the arrival   ! the arrival itself
BARS = [
    ("D", "po"), ("D", "pob"),                                              # 0-5      the hook
    ("Bm", "pobd"), ("G", "pobd"), ("D", "pobdt"), ("A", "pobdt"),          # 5-15     two worlds
    ("D", "pobdtsc"), ("A", "pobdtsc"), ("Bm", "pobdtsc"), ("G", "pobdtsc"),  # 15-25   the world is alive
    ("D", "pobdtsc"), ("A", "pobdtsc"),                                     # 25-30
    ("Bm", "pobdsrc"), ("G", "pobdsrc"), ("D/F#", "pobdsrcq"), ("A", "pobdsrcq"), ("Asus", "pobdsrcqR"),   # 30-42.5 the distance collapses
    ("D", "pobdTsc!"), ("A", "pobdTsc"), ("Bm", "pobdTsc"), ("G", "pobdTsc"),   # 42.5-52.5 the reunion
    ("D", "pobdTsc"), ("A/C#", "pobdtsc"), ("G", "obdt"),                   # 52.5-60
    ("D", "d"), ("Bm", "d"),                                                # 60-65    the line: the music stands back
    ("Asus", "dx"), ("D", "obdF"), ("G", "obd"), ("D", "E"),                # 65-75    the title, the stinger
]


def music() -> np.ndarray:
    dry = np.zeros((N + SR * 4, 2))
    pads = np.zeros((N + SR * 4, 2))
    low = np.zeros((N + SR * 4, 2))
    side = np.ones(N + SR * 4)                                 # the pad and bass dip under each kick
    for bar, (chord, layers) in enumerate(BARS):
        t0 = bar * BAR
        root, tones = CHORDS[chord]
        loud = 1.0 if bar < 17 else 1.3
        if 12 <= bar <= 16:
            loud = 1.0 + 0.085 * (bar - 12)                    # the climb gathers as it goes
        if bar >= 23:
            loud = 1.0
        if bar < 2:
            loud = 1.7                                         # the film starts on the pulse alone: it has to carry
        if "p" in layers:
            for beat in range(4):
                ta.place(low, kick(0.8 if beat % 2 == 0 else 0.62), t0 + beat * BEAT, 0.5 * loud)
                i = int((t0 + beat * BEAT) * SR)
                side[i:i + int(0.22 * SR)] *= 1 - 0.45 * np.exp(-np.arange(int(0.22 * SR)) / (0.07 * SR))
        if "o" in layers:
            # the pulse the film starts on: eighth notes on the chord, the off-beats lighter
            pattern = [tones[2], tones[4], tones[3], tones[4], tones[2], tones[4], tones[3] + 12, tones[4]]
            for k, m in enumerate(pattern):
                ta.place(dry, pluck(m, 0.55, 0.45 if k % 2 else 0.6), t0 + k * BEAT / 2, (0.2 if k % 2 else 0.27) * loud, 0.35 * (-1) ** k)
        if "b" in layers:
            for beat, held in ((0, 1.4), (1.5, 0.45), (2.5, 0.45), (3, 0.9)):
                ta.place(low, bass(root, held * BEAT), t0 + beat * BEAT, 0.36 * loud)
        if "d" in layers:
            quiet = 24 <= bar <= 26                             # under the two lines the pad is barely there
            ta.place(pads, pad(tones, BAR, 0.5 if quiet else (1.0 if bar >= 17 else 0.75)), t0 - 0.25, (0.16 if quiet else 0.42) * loud)
        base = chord if chord in TUNE else {"A/C#": "A", "Asus": "A", "D/F#": "D", "Em": "G"}[chord]
        if "t" in layers or "T" in layers:
            for beat, m, held in TUNE[base]:
                ta.place(dry, bell(m, 0.5 + held * BEAT, 1.0), t0 + beat * BEAT, 0.2 * loud, 0.1)
                ta.place(dry, ta.piano(m - 12, 0.5, held * BEAT), t0 + beat * BEAT, 0.5 * loud, -0.15)
                if "T" in layers:
                    ta.place(dry, bell(m + 12, 0.4 + held * BEAT, 1.0), t0 + beat * BEAT, 0.085 * loud, 0.3)
                    ta.place(dry, pluck(m, 0.9, 0.7), t0 + beat * BEAT, 0.2, -0.3)
        if "s" in layers:
            for k in range(8):
                ta.place(dry, shaker(0.9 if k % 2 else 0.5), t0 + k * BEAT / 2 + 0.004, 0.11 * loud, 0.5)
            for k in (3, 7, 11, 15):
                ta.place(dry, shaker(0.35), t0 + k * BEAT / 4, 0.08, -0.5)
        if "c" in layers:
            for beat in (1, 3):
                ta.place(dry, clap(), t0 + beat * BEAT, 0.17 * loud, -0.1)
        if "r" in layers:
            # the climb: the chord going up in sixteenths, a note higher each bar
            run = [tones[1], tones[2], tones[3], tones[4]]
            for k in range(16):
                m = run[k % 4] + 12 * (k // 8) + (12 if bar >= 15 else 0)
                ta.place(dry, pluck(m, 0.3, 0.75), t0 + k * BEAT / 4, 0.1 + 0.012 * (bar - 12), 0.6 * np.sin(k * 0.8))
        if "q" in layers:
            for k in range(16):
                ta.place(dry, shaker(0.7), t0 + k * BEAT / 4 + 0.003, 0.07 * loud, 0.4 * (-1) ** k)
            for k in range(8):
                ta.place(low, bass(root + 12, 0.36 * BEAT), t0 + k * BEAT / 2, 0.17 * loud)
        if "R" in layers:
            ta.place(dry, swell(BAR - 0.18, 500, 6000), t0, 0.16)
            for k in range(1, 14):                                  # a roll into the arrival
                ta.place(dry, clap(), t0 + BAR - 0.18 - 1.5 + 1.5 * (k / 14) ** 0.6, 0.02 + 0.006 * k, 0.2 * (-1) ** k)
        if "!" in layers:
            ta.place(low, boom(), t0, 0.5)
        if "F" in layers:
            # the last phrase of the tune, slowed into the title
            for beat, m, held in ((0, 78, 2), (2, 81, 1), (3, 86, 5)):
                ta.place(dry, bell(m, 1.2 + held * BEAT, 1.0), t0 + beat * BEAT, 0.2, 0.1)
                ta.place(dry, ta.piano(m - 12, 0.45, held * BEAT), t0 + beat * BEAT, 0.55, -0.15)
        if "E" in layers:
            # the stinger's last word: two plucked notes
            at = CUT["black"] - 0.42
            ta.place(dry, pluck(81, 1.2, 0.7), at, 0.3, -0.2)
            ta.place(dry, pluck(86, 1.6, 0.7), at + BEAT / 2, 0.34, 0.2)
            ta.place(low, bass(38, 1.0), at + BEAT / 2, 0.3)
    # the pick-up into the title: three notes rising out of the quiet after the joke
    title = CUT["title"]["at"]
    for k, m in enumerate((69, 74, 76)):
        ta.place(dry, bell(m, 1.0, 1.0), title - (3 - k) * BEAT / 2, 0.15, 0.0)
        ta.place(dry, ta.piano(m - 12, 0.4, 0.4), title - (3 - k) * BEAT / 2, 0.42, -0.1)
    room = ta.reverb_ir(2.4, 5200, 7)
    hall = ta.reverb_ir(3.4, 3800, 11)
    out = ta.wet(dry[:N], room, 0.3)[:N] + ta.wet(pads[:N] * side[:N, None], hall, 0.45)[:N] + low[:N] * (0.55 + 0.45 * side[:N, None])
    return out


# ------------------------------------------------------------------------------------------- the places

def air(seconds: float, kind: str) -> np.ndarray:
    n = int(seconds * SR) + SR
    noise = rng.standard_normal((n, 2))
    t = np.arange(n) / SR
    if kind == "city":
        rumble = ta.lowpass(noise, 140) * 0.05
        hiss = ta.bandpass(noise, 600, 2600) * 0.006 * (0.7 + 0.3 * np.sin(t * 0.7))[:, None]
        return rumble + hiss
    if kind in ("street", "balcony"):
        rumble = ta.lowpass(noise, 180) * (0.03 if kind == "balcony" else 0.035)
        murmur = ta.bandpass(noise, 300, 1400) * (0.004 if kind == "balcony" else 0.012) * (0.6 + 0.4 * np.abs(ta.lowpass(rng.standard_normal(n), 3)) * 8)[:, None]
        return rumble + murmur
    if kind == "stairs-city":
        return ta.lowpass(noise, 200) * 0.022 + ta.bandpass(noise, 400, 1500) * 0.004
    if kind in ("coast", "stairs-coast", "terrace"):
        wash = ta.bandpass(noise, 300, 2800)
        swell_ = np.zeros(n)
        at = -1.0
        while at < seconds + 1:
            length = rng.uniform(3.2, 5.2)
            i0 = max(0, int(at * SR))
            m = min(n - i0, int(length * SR))
            if m > 0:
                swell_[i0:i0 + m] += np.sin(np.linspace(0, np.pi, m)) ** 2.2 * rng.uniform(0.5, 1.0)
            at += length * rng.uniform(0.55, 0.85)
        far = {"coast": 1.0, "stairs-coast": 0.6, "terrace": 0.38}[kind]
        wind = ta.bandpass(noise, 150, 900) * (0.5 + 0.5 * np.abs(ta.lowpass(rng.standard_normal(n), 0.8)) * 5)[:, None] * (0.012 if kind != "coast" else 0.008)
        return wash * (0.2 + 0.8 * swell_)[:, None] * 0.04 * far + ta.lowpass(noise, 160) * 0.01 + wind
    raise ValueError(kind)


def step(hard: float) -> np.ndarray:
    n = int(0.12 * SR)
    t = np.arange(n) / SR
    thud = np.sin(2 * np.pi * (95 + 40 * np.exp(-t / 0.01)) * t) * np.exp(-t / 0.022)
    tap = ta.bandpass(rng.standard_normal(n + 300), 900, 4200 + 2500 * hard)[300:] * np.exp(-t / (0.006 + 0.006 * hard))
    return thud * 0.5 + tap * (0.25 + 0.5 * hard)


def steps(track, start, end, every, hard, gain, pan=0.0, sway=0.25):
    k = 0
    t = start
    while t < end:
        ta.place(track, step(hard) * rng.uniform(0.8, 1.0), t + rng.uniform(-0.012, 0.012), gain, pan + sway * (-1) ** k)
        t += every
        k += 1


def scrape(seconds: float) -> np.ndarray:
    """A chair's feet over stone."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    x = ta.bandpass(rng.standard_normal(n + 500), 250, 2400)[500:]
    judder = 0.55 + 0.45 * np.sign(np.sin(2 * np.pi * (38 + 14 * np.sin(t * 9)) * t))
    return x * judder * np.sin(np.linspace(0, np.pi, n)) ** 0.7 * 0.5 + np.sin(2 * np.pi * 150 * t) * np.exp(-t / 0.03) * 0.25


def whoosh(seconds: float, lo: float, hi: float) -> np.ndarray:
    n = int(seconds * SR)
    x = ta.bandpass(rng.standard_normal((n + 500, 2)), lo, hi)[500:]
    return x * (np.sin(np.linspace(0, np.pi, n)) ** 2)[:, None]


def strum(midis, down=True) -> np.ndarray:
    notes = midis if down else midis[::-1]
    out = np.zeros(int(1.2 * SR))
    for k, m in enumerate(notes):
        p = pluck(m, 1.0, 0.75)
        i0 = int(k * 0.012 * SR)
        out[i0:i0 + len(p)] += p[: len(out) - i0] * (0.9 - 0.06 * k)
    return out


def places() -> np.ndarray:
    track = np.zeros((N, 2))
    t = 0.0
    starts = {}
    lap = 0.3                                                   # each place is heard a little before it is seen
    spans = []
    for shot in CUT["shots"]:
        starts[shot["id"]] = t
        if spans and spans[-1][2] == shot["place"]:
            spans[-1] = (spans[-1][0], t + shot["seconds"], shot["place"])
        else:
            spans.append((t, t + shot["seconds"], shot["place"]))
        t += shot["seconds"]
    for a, b, kind in spans:
        a0, b0 = max(0.0, a - lap), min(TOTAL, b + 0.12)
        bed = air(b0 - a0, kind)[: int((b0 - a0) * SR)]
        ramp = np.ones(len(bed))
        n_in, n_out = int(min(lap * 1.6, 0.6) * SR), int(0.25 * SR)
        ramp[:n_in] = np.linspace(0, 1, n_in) ** 1.5
        ramp[-n_out:] = np.linspace(1, 0, n_out)
        ta.place(track, bed * ramp[:, None], a0, 1.0)
    s = starts
    # the city: a boat's horn far off, cars going by
    horn = np.sin(2 * np.pi * 110 * np.arange(int(1.6 * SR)) / SR) + 0.5 * np.sin(2 * np.pi * 165 * np.arange(int(1.6 * SR)) / SR)
    ta.place(track, ta.lowpass(horn * np.hanning(len(horn)), 600), 0.25, 0.035, -0.4)
    for at, pan in ((s["w05"] + 0.4, -0.6), (s["w05"] + 1.9, 0.5), (s["w09a"] + 0.2, 0.4)):
        ta.place(track, whoosh(1.5, 120, 900), at, 0.05, pan)
    # steps: his on the city's stone (harder), hers on the walk; quicker on the stairs
    steps(track, s["w04"] + 1.45, s["w04"] + 3.1, 0.52, 0.3, 0.05, 0.1)
    steps(track, s["w05"] + 0.1, s["w05"] + 1.4, 0.5, 0.8, 0.06, 0.3)
    steps(track, s["w05"] + 2.5, s["w05"] + 3.1, 0.5, 0.8, 0.06, 0.3)
    steps(track, s["w06"], s["w06"] + 2.5, 0.42, 0.8, 0.07)
    steps(track, s["w07"], s["w07"] + 2.5, 0.55, 0.3, 0.04, -0.3)
    steps(track, s["w10b"], s["w10b"] + 1.25, 0.36, 1.0, 0.13, 0.0, 0.12)
    steps(track, s["w11"], s["w11"] + 1.0, 0.5, 0.3, 0.045)
    steps(track, s["w11"] + 1.15, s["w11"] + 2.2, 0.22, 0.7, 0.08, 0.5, 0.1)          # the hat's owner, running
    steps(track, s["w12"], s["w12"] + 2.5, 0.5, 0.9, 0.08)
    steps(track, s["w13"], s["w13"] + 2.5, 0.5, 0.5, 0.07)
    steps(track, s["w14"], s["w14"] + 1.25, 0.4, 0.9, 0.09)
    steps(track, s["w15"], s["w15"] + 1.25, 0.4, 0.5, 0.08)
    steps(track, s["w16"], s["w16"] + 1.0, 0.36, 0.9, 0.09)
    # the terrace: someone coming up behind her; then the two of them crossing it
    steps(track, s["w18"] - 0.2, s["w18"] + 1.2, 0.46, 0.7, 0.05, -0.5, 0.05)
    steps(track, s["w19"], s["w19"] + 1.0, 0.46, 0.7, 0.07, -0.2, 0.08)
    steps(track, s["w19"] + 1.6, s["w19"] + 4.3, 0.34, 0.6, 0.07, -0.1, 0.3)
    steps(track, s["w22"], s["w22"] + 1.875, 0.4, 0.5, 0.05)
    # the players on the walk: a guitar on the film's own chords, hands clapping on the beat
    chord = [50, 57, 62, 66, 69, 74]
    for k in range(8):
        ta.place(track, strum(chord, k % 2 == 0), s["w07"] + k * BEAT / 2, 0.05 if k % 2 == 0 else 0.03, 0.4)
    for k in range(4):
        ta.place(track, clap(), s["w07"] + k * BEAT, 0.035, 0.6)
    # small things: a hand along iron and along stone, cloth in the wind, the hat, a pat on a bench
    ta.place(track, whoosh(1.2, 1800, 6000) * 0.6, s["w09a"], 0.02, 0.0)
    ta.place(track, whoosh(1.2, 700, 3000) * 0.6, s["w09b"], 0.03, 0.0)
    for k in range(3):
        ta.place(track, whoosh(0.5, 200, 1400), s["w10a"] + 0.1 + k * 0.38, 0.06, -0.3 + 0.3 * k)
    ta.place(track, whoosh(0.7, 500, 3500), s["w11"] + 0.72, 0.09, 0.5)
    for at in (1.08, 1.34):
        ta.place(track, step(0.2) * 0.8, s["w08"] + at, 0.07, -0.2)
    ta.place(track, whoosh(0.4, 200, 1200), s["w17c"] + 0.1, 0.07, -0.4)
    ta.place(track, whoosh(0.6, 300, 1600), s["w20"] + 0.1, 0.03, 0.0)                 # cloth against cloth
    # chairs on the stone
    ta.place(track, scrape(0.6), s["w24"] + 0.25, 0.13, -0.2)
    ta.place(track, scrape(0.38), s["w29"] + 1.52, 0.2, 0.25)
    small = ta.reverb_ir(0.8, 5200, 9)
    return ta.wet(track, small, 0.14)[:N]


# ------------------------------------------------------------------------------------------- the voices

def voices():
    track = np.zeros((N, 2))
    spans = []
    for line in CUT["lines"]:
        x = ta.highpass(ta.load_voice(line["id"]), 110)
        x = ta.soften(ta.level(x, -17.0))
        ir = ta.reverb_ir(0.45 if line["place"] != "balcony" else 0.6, 5000, 3)
        y = ta.wet(x, ir, 0.07 if line["place"] == "terrace" else 0.05)
        ta.place(track, y, line["at"], 1.0)
        spans.append((line["at"], line["at"] + len(x) / SR, line["id"]))
    return track, spans


def duck(spans, depth_before=0.3, after=0.2) -> np.ndarray:
    g = np.zeros(N)
    for start, end, _ in spans:
        g[max(0, int((start - depth_before) * SR)):min(N, int((end + after) * SR))] = 1.0
    return np.clip(ta.lowpass(g, 1.5, 1), 0, 1)


def write(name: str, x: np.ndarray) -> None:
    wavfile.write(OUT / name, SR, np.clip(x * 32767.0, -32768, 32767).astype(np.int16))


def level_db(x: np.ndarray) -> float:
    return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    assert len(BARS) * BAR == TOTAL, (len(BARS) * BAR, TOTAL)
    print(f"film: {TOTAL:.2f} s, {len(BARS)} bars at {CUT['bpm']} beats a minute")
    print("score...")
    m = music()
    print("places...")
    p = places()
    print("voices...")
    v, spans = voices()
    d = duck(spans)
    m_in = m * 0.4 * (1.0 - 0.72 * d)[:, None]
    p_in = p * 2.6 * (1.0 - 0.3 * d)[:, None]
    mix = m_in + p_in + v
    fade = np.ones(N)
    fade[: int(0.15 * SR)] = np.linspace(0, 1, int(0.15 * SR))
    # the cut to black takes the places with it; the last notes ring into it
    black = int(CUT["black"] * SR)
    tail = np.ones(N)
    tail[black:] = np.linspace(1, 0, N - black) ** 2
    mix = (m_in * tail[:, None] + (p_in + v) * np.where(np.arange(N) < black, 1.0, np.clip(1 - (np.arange(N) - black) / (0.03 * SR), 0, 1))[:, None]) * fade[:, None]
    peak = np.abs(mix).max()
    mix = np.tanh(mix / max(peak, 1e-6) * 1.35) / np.tanh(1.35) * ta.db(-1.0)
    write("music.wav", m / max(np.abs(m).max(), 1.0))
    write("places.wav", p / max(np.abs(p).max(), 1.0))
    write("voices.wav", v / max(np.abs(v).max(), 1.0))
    write("mix.wav", mix)
    print(f"mix: peak {20 * np.log10(np.abs(mix).max()):.1f} dBFS, rms {level_db(mix):.1f} dBFS")
    for name, part in (("music", m_in), ("places", p_in), ("voices", v)):
        print(f"  {name:7s} peak {20 * np.log10(np.abs(part).max() + 1e-9):6.1f} dB, rms {level_db(part):6.1f} dB")
    print("  each line against what plays under it (speech should stay about 10 dB clear):")
    for start, end, name in spans:
        a, b = int(start * SR), min(N, int(end * SR))
        print(f"    {start:6.2f} s  {name:18s} {level_db(v[a:b]) - level_db(m_in[a:b] + p_in[a:b]):5.1f} dB")
    print("  the score, section by section (rms):")
    for name, a, b in (("hook", 0, 5), ("two worlds", 5, 15), ("alive", 15, 30), ("collapses", 30, 42.5), ("reunion", 42.5, 60), ("the line", 60, 66.25), ("title", 66.25, 72.5), ("stinger", 72.5, 75)):
        print(f"    {name:12s} {level_db(m_in[int(a * SR):int(b * SR)]):6.1f} dB")


if __name__ == "__main__":
    main()
