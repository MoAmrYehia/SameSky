"""The sound of the animated concept trailer "I Saved You a Seat".

Everything here is made by this script, so the trailer has no outside audio in it:
  * the score: an original piece for a soft piano and a quiet pad, written out below note by note and synthesized
    (additive strings with a felt-hammer attack, a generated room reverb);
  * the foley and room tones (a cup on wood, keys in a bowl, water at a quay, taps, a chime...), synthesized;
  * the voices: the lines rendered by scripts/demo/trailer_voices.ps1 with Windows' own text-to-speech voices, cleaned,
    levelled and placed where scripts/demo/trailer_cut.json says. They are synthesized voices, not recordings of anyone.

Writes demo-build/film/audio/{music,effects,voices,mix}.wav (48 kHz stereo).

    python scripts/demo/trailer_audio.py        (needs numpy and scipy)
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from scipy import signal
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[2]
CUT = json.loads((ROOT / "scripts" / "demo" / "trailer_cut.json").read_text(encoding="utf-8"))
VOICE = ROOT / "demo-build" / "film" / "voice"
OUT = ROOT / "demo-build" / "film" / "audio"
SR = 48000
TOTAL = sum(s["seconds"] for s in CUT["shots"])
N = int(TOTAL * SR)
rng = np.random.default_rng(26)


def db(x: float) -> float:
    return 10.0 ** (x / 20.0)


def lowpass(x, hz, order=2):
    b, a = signal.butter(order, hz / (SR / 2), "low")
    return signal.lfilter(b, a, x, axis=0)


def highpass(x, hz, order=2):
    b, a = signal.butter(order, hz / (SR / 2), "high")
    return signal.lfilter(b, a, x, axis=0)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], "band")
    return signal.lfilter(b, a, x, axis=0)


def place(track: np.ndarray, sound: np.ndarray, at: float, gain: float = 1.0, pan: float = 0.0) -> None:
    """Adds a mono or stereo sound to a stereo track at a time, panned (-1 left .. 1 right)."""
    start = int(at * SR)
    if start >= len(track) or start + len(sound) <= 0:
        return
    if sound.ndim == 1:
        left, right = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
        sound = np.stack([sound * left, sound * right], axis=1) * np.sqrt(2) / 1.0
    s0 = max(0, -start)
    end = min(len(track), start + len(sound))
    track[max(0, start):end] += sound[s0:s0 + end - max(0, start)] * gain


def reverb_ir(seconds: float, damping: float, seed: int) -> np.ndarray:
    """A generated room: decaying noise, darker as it dies away, a little different in each ear."""
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = r.standard_normal((n, 2)) * np.exp(-t * 6.9 / seconds)[:, None]
    ir = lowpass(ir, damping)
    ir[: int(0.004 * SR)] *= 0.2
    return ir / np.sqrt((ir ** 2).sum(axis=0)).max()


def wet(x: np.ndarray, ir: np.ndarray, amount: float) -> np.ndarray:
    if x.ndim == 1:
        x = np.stack([x, x], axis=1)
    tail = np.stack([signal.fftconvolve(x[:, c], ir[:, c]) for c in range(2)], axis=1)
    out = np.zeros((len(tail), 2))
    out[: len(x)] = x
    return out + tail * amount


# ------------------------------------------------------------------------------------------- the score

def piano(midi: int, velocity: float, held: float) -> np.ndarray:
    """One note of a soft, felted piano: two slightly detuned strings per note, partials that die faster the higher
    they are, a quiet thump from the hammer."""
    f0 = 440.0 * 2 ** ((midi - 69) / 12)
    seconds = min(8.0, held + 4.5)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    stiff = 0.00011 * 2 ** ((midi - 60) / 14)
    parts = int(max(3, min(12, (SR * 0.42) // f0)))
    bright = 0.3 + 0.7 * velocity
    for k in range(1, parts + 1):
        fk = f0 * k * np.sqrt(1 + stiff * k * k)
        amp = 1.0 / k ** (2.2 - 0.9 * bright)
        slow = (0.32 + 0.2 * k) * (1 + max(0, midi - 50) / 34)
        env = 0.55 * np.exp(-slow * 3.2 * t) + 0.45 * np.exp(-slow * t)
        for cents in (-0.45, 0.5):
            out += 0.5 * amp * np.sin(2 * np.pi * fk * 2 ** (cents / 1200) * t + rng.uniform(0, 2 * np.pi)) * env
    thump = lowpass(rng.standard_normal(int(0.03 * SR)), 900) * np.exp(-np.arange(int(0.03 * SR)) / (0.006 * SR))
    out[: len(thump)] += thump * 0.5 * velocity
    out *= 1 - np.exp(-t / 0.006)
    release = np.clip(t - held, 0, None)
    out *= np.exp(-release * 1.6)          # the damper comes down gently: this is a pedalled, ringing piano
    return lowpass(out, 5200) * (0.25 + 0.75 * velocity)


CHORDS = {
    "Am": [45, 52, 57, 60, 64], "F": [41, 48, 53, 57, 60], "C": [48, 55, 60, 64, 67], "G": [43, 50, 55, 59, 62],
    "C/E": [40, 52, 55, 60, 64], "G/B": [47, 50, 55, 59, 62], "Dm7": [50, 57, 60, 65, 69], "Fadd9": [41, 48, 55, 57, 60],
}
BAR = 60.0 / 72.0 * 4   # four beats at 72 to the minute
BEAT = BAR / 4

# bar by bar: the chord, how the left hand plays it, and the tune over it as (beat, note, beats held, how hard)
SCORE = [
    # the empty seat: almost nothing
    ("Am", "sparse", [(1.0, 76, 3, 0.34)]),
    ("Fadd9", "hold", []),
    # two different days
    ("Am", "walk", [(0, 72, 2, 0.4), (2, 76, 2, 0.4)]),
    ("F", "walk", [(0, 77, 3, 0.42), (3, 76, 1, 0.36)]),
    ("C", "walk", [(0, 72, 2, 0.4), (2, 79, 2, 0.42)]),
    ("G", "walk", [(0, 74, 3, 0.4)]),
    ("Am", "walk", [(0, 72, 2, 0.38), (2.5, 69, 1.5, 0.34)]),
    # save the moment: a little more movement
    ("F", "roll", [(0, 69, 1, 0.42), (1, 72, 1, 0.42), (2, 77, 2, 0.46)]),
    ("C", "roll", [(0, 76, 2, 0.46), (2, 79, 2, 0.46)]),
    ("G", "roll", [(0, 74, 2, 0.44), (2, 71, 2, 0.4)]),
    ("Am", "roll", [(0, 72, 3, 0.44)]),
    ("F", "roll", [(0, 69, 1, 0.42), (1, 72, 1, 0.42), (2, 77, 2, 0.46)]),
    ("G", "walk", [(0, 74, 4, 0.4)]),
    # a message you can hold: the music stands back for her voice
    ("Am", "walk", [(0, 76, 2, 0.36)]),
    ("F", "hold", []),
    ("Fadd9", "rest", []),
    ("C/E", "sparse", [(2, 79, 2, 0.34)]),
    ("G", "walk", [(0, 74, 2, 0.38), (2, 79, 2, 0.4)]),
    # her day, shared
    ("F", "roll", [(0, 69, 2, 0.46), (2, 72, 2, 0.46)]),
    ("C", "roll", [(0, 76, 3, 0.5), (3, 74, 1, 0.42)]),
    ("G", "roll", [(0, 74, 2, 0.48), (2, 71, 2, 0.44)]),
    ("Am", "roll", [(0, 72, 4, 0.46)]),
    ("F", "roll", [(0, 69, 1, 0.46), (1, 72, 1, 0.46), (2, 77, 2, 0.5)]),
    ("C", "walk", [(0, 76, 2, 0.44), (2, 79, 2, 0.46)]),
    ("G", "hold", [(0, 74, 4, 0.4)]),
    # the seat is filled
    ("C", "roll", [(0, 79, 2, 0.56), (2, 76, 2, 0.5)]),
    ("G/B", "roll", [(0, 74, 2, 0.52), (2, 79, 2, 0.54)]),
    ("Am", "roll", [(0, 76, 2, 0.52), (2, 72, 2, 0.48)]),
    ("F", "roll", [(0, 69, 1, 0.5), (1, 72, 1, 0.5), (2, 77, 2, 0.54)]),
    ("C", "roll", [(0, 76, 4, 0.52)]),
    ("F", "roll", [(0, 77, 2, 0.54), (2, 81, 2, 0.56)]),
    ("G", "walk", [(0, 79, 4, 0.5)]),
    # a new ritual
    ("F", "walk", [(0, 69, 2, 0.44), (2, 72, 2, 0.44)]),
    ("C/E", "walk", [(0, 67, 4, 0.42)]),
    ("Dm7", "walk", [(0, 65, 2, 0.4), (2, 62, 2, 0.4)]),
    ("C", "end", []),
]

LEFT = {
    # (beat, which chord note, beats held, how hard)
    "sparse": [(0, 0, 4, 0.3), (2, 2, 2, 0.26)],
    "hold": [(0, 0, 4, 0.3), (0.02, 2, 4, 0.26), (0.04, 3, 4, 0.24)],
    "rest": [(0, 0, 4, 0.2)],
    "walk": [(0, 0, 2, 0.36), (1, 2, 1, 0.28), (2, 3, 1, 0.3), (3, 2, 1, 0.26)],
    "roll": [(0, 0, 1.5, 0.4), (0.5, 1, 1, 0.26), (1, 2, 1, 0.3), (1.5, 3, 1, 0.28), (2, 4, 1, 0.32), (2.5, 3, 1, 0.26), (3, 2, 1, 0.28), (3.5, 1, 0.5, 0.24)],
    "end": [(0, 0, 8, 0.36), (0.12, 1, 8, 0.3), (0.24, 2, 8, 0.32), (0.36, 3, 8, 0.34), (0.5, 4, 8, 0.36), (0.66, 4 + 100, 8, 0.38)],
}


def score() -> np.ndarray:
    dry = np.zeros((N, 2))
    pad = np.zeros((N, 2))
    for bar, (chord, style, tune) in enumerate(SCORE):
        t0 = bar * BAR
        tones = CHORDS[chord]
        for beat, which, held, hard in LEFT[style]:
            midi = tones[which] if which < 100 else tones[which - 100] + 12
            note = piano(midi, hard, held * BEAT)
            place(dry, note, t0 + beat * BEAT + rng.uniform(-0.008, 0.008), 0.9, (midi - 56) / 40)
        for beat, midi, held, hard in tune:
            note = piano(midi, hard, held * BEAT)
            place(dry, note, t0 + beat * BEAT + rng.uniform(-0.006, 0.01), 1.0, (midi - 60) / 36)
        # the pad: the chord's root and fifth, low and soft, swelling in and out with the bar
        level = {"rest": 0.5, "sparse": 0.6, "hold": 0.8, "end": 1.0}.get(style, 0.75)
        n = int((BAR + 2.6) * SR)
        t = np.arange(n) / SR
        env = np.clip(t / 1.8, 0, 1) * np.clip((BAR + 2.6 - t) / 2.4, 0, 1)
        tone = np.zeros(n)
        for midi, amp in ((tones[0], 1.0), (tones[1], 0.55), (tones[2] + 0, 0.5), (tones[3] + 12, 0.22)):
            f = 440.0 * 2 ** ((midi - 69) / 12)
            for cents in (-4, 3.5):
                tone += amp * np.sin(2 * np.pi * f * 2 ** (cents / 1200) * t + rng.uniform(0, 6.28))
                tone += amp * 0.18 * np.sin(2 * np.pi * 2 * f * 2 ** (cents / 1200) * t + rng.uniform(0, 6.28))
        place(pad, lowpass(tone * env, 1300) * 0.03 * level, t0 - 0.9)
    room = reverb_ir(3.0, 4200, 5)
    music = wet(dry * 0.32, room, 0.42)[:N] + wet(pad, room, 0.5)[:N]
    return music


# ------------------------------------------------------------------------------------------- foley and tones

def ping(freq, seconds, decay, phase=None):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    return np.sin(2 * np.pi * freq * t + (rng.uniform(0, 6.28) if phase is None else phase)) * np.exp(-t / decay)


def burst(seconds, lo, hi, decay=None):
    n = int(seconds * SR)
    x = bandpass(rng.standard_normal(n + 2000), lo, hi)[2000:]
    t = np.arange(n) / SR
    return x * (np.exp(-t / decay) if decay else np.hanning(n))


def mixdown(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def tone_bed(seconds, kind):
    n = int(seconds * SR) + SR
    noise = rng.standard_normal((n, 2))
    if kind == "water":
        t = np.arange(n) / SR
        wash = bandpass(noise, 350, 2600)
        swell = np.zeros(n)
        at = 0.0
        while at < seconds + 1:
            length = rng.uniform(2.6, 4.6)
            i0 = int(at * SR)
            m = min(n - i0, int(length * SR))
            if m <= 0:
                break
            shape = np.sin(np.linspace(0, np.pi, m)) ** 2.2
            swell[i0:i0 + m] += shape * rng.uniform(0.5, 1.0)
            at += length * rng.uniform(0.55, 0.85)
        low = lowpass(noise, 180) * 2.2
        bed = wash * (0.18 + 0.82 * swell)[:, None] * 0.05 + low * 0.012
        return bed
    if kind == "flat":
        return lowpass(noise, 240) * 0.02 + lowpass(noise, 2000) * 0.0016
    if kind == "desk":
        return lowpass(noise, 300) * 0.016 + lowpass(noise, 2500) * 0.0012
    return lowpass(noise, 320) * 0.014 + lowpass(noise, 3000) * 0.0011      # the lounge: quiet, a little air


def cloth(seconds=0.32):
    n = int(seconds * SR)
    x = lowpass(rng.standard_normal(n), 1700)
    wobble = np.abs(lowpass(rng.standard_normal(n), 30)) * 14
    return x * wobble * np.hanning(n) * 0.05


SOUNDS = {
    "cup-down": lambda: mixdown(ping(92, 0.16, 0.035, 0) * 0.5, burst(0.012, 800, 5000, 0.003) * 0.35, ping(2350, 0.14, 0.03) * 0.06, ping(3720, 0.1, 0.02) * 0.04),
    "cup-touch": lambda: mixdown(ping(2480, 0.1, 0.022) * 0.035, burst(0.03, 2500, 7000) * 0.02),
    "cup-lift": lambda: mixdown(burst(0.06, 1800, 6000) * 0.025, ping(3100, 0.12, 0.03) * 0.03),
    "tap": lambda: mixdown(ping(1250, 0.03, 0.006, 0) * 0.07, burst(0.008, 500, 3000, 0.002) * 0.06),
    "record": lambda: mixdown(ping(880, 0.09, 0.03, 0) * 0.06, np.concatenate([np.zeros(int(0.085 * SR)), ping(1320, 0.14, 0.045, 0) * 0.06])),
    "sent": lambda: mixdown(burst(0.4, 900, 5000) * 0.03, np.concatenate([np.zeros(int(0.12 * SR)), mixdown(ping(1760, 0.9, 0.22, 0) * 0.05, ping(2637, 0.7, 0.16, 0) * 0.03)])),
    "chime": lambda: mixdown(ping(1318.5, 0.9, 0.2, 0) * 0.09, ping(2637, 0.4, 0.07, 0) * 0.02, np.concatenate([np.zeros(int(0.13 * SR)), mixdown(ping(1760, 1.1, 0.26, 0) * 0.09, ping(3520, 0.4, 0.07, 0) * 0.02)])),
    "arrive": lambda: lowpass(sum(ping(f, 2.2, 0.55, 0) for f in (523.25, 659.25, 783.99)) * (1 - np.exp(-np.arange(int(2.2 * SR)) / (0.12 * SR))), 2500) * 0.03,
    "screen": lambda: mixdown(burst(0.7, 400, 2400) * 0.02, signal.chirp(np.arange(int(0.6 * SR)) / SR, 330, 0.6, 660) * np.hanning(int(0.6 * SR)) * 0.025),
    "frame": lambda: mixdown(ping(2093, 0.3, 0.06) * 0.025, ping(3136, 0.22, 0.045) * 0.016),
    "card": lambda: mixdown(burst(0.02, 1000, 4500, 0.005) * 0.09, ping(140, 0.06, 0.015, 0) * 0.08),
    "cloth": cloth,
    "breath": lambda: bandpass(rng.standard_normal(int(0.9 * SR)), 450, 1900) * np.sin(np.linspace(0, np.pi, int(0.9 * SR))) ** 1.5 * 0.02,
    "headset": lambda: mixdown(cloth(0.5), np.concatenate([np.zeros(int(0.3 * SR)), ping(310, 0.05, 0.012, 0) * 0.08]), np.concatenate([np.zeros(int(0.42 * SR)), burst(0.012, 900, 4000, 0.004) * 0.07])),
    "keepsake": lambda: lowpass(sum(ping(f, 2.4, d, 0) * a for f, d, a in ((783.99, 0.6, 1.0), (1174.66, 0.45, 0.6), (1567.98, 0.35, 0.4), (2349.3, 0.2, 0.15))), 6000) * 0.035,
}


def keys(landing: bool) -> np.ndarray:
    n = int((0.9 if landing else 0.85) * SR)
    out = np.zeros(n)
    count = 7 if landing else 9
    for i in range(count):
        at = rng.uniform(0.0, 0.16) if landing else rng.uniform(0.0, 0.7)
        f = rng.uniform(3200, 9200)
        p = mixdown(ping(f, 0.12, rng.uniform(0.012, 0.035)), ping(f * 1.47, 0.08, 0.012) * 0.6) * rng.uniform(0.025, 0.06)
        i0 = int(at * SR)
        out[i0:i0 + len(p)] += p[: n - i0]
    if landing:
        bowl = mixdown(ping(1130, 0.6, 0.11, 0) * 0.05, ping(2890, 0.35, 0.06, 0) * 0.03, ping(120, 0.08, 0.02, 0) * 0.12)
        out[: len(bowl)] += bowl[:n]
    return out


def effects() -> np.ndarray:
    track = np.zeros((N, 2))
    for cue in CUT["sounds"]:
        what = cue["what"]
        level = cue.get("level", 1.0)
        if "from" in cue:
            seconds = cue["to"] - cue["from"]
            bed = tone_bed(seconds, what)[: int(seconds * SR)]
            fade = int(0.35 * SR)
            ramp = np.ones(len(bed))
            ramp[:fade] = np.linspace(0, 1, fade)
            ramp[-fade:] = np.linspace(1, 0, fade)
            place(track, bed * ramp[:, None], cue["from"], level)
        elif what == "keys":
            place(track, keys(False), cue["at"], level, 0.1)
        elif what == "keys-bowl":
            place(track, keys(True), cue["at"], level, 0.15)
        else:
            place(track, SOUNDS[what](), cue["at"], level, cue.get("pan", 0.0))
    small = reverb_ir(0.7, 5000, 9)
    return wet(track, small, 0.12)[:N]


# ------------------------------------------------------------------------------------------- the voices

def load_voice(line_id: str) -> np.ndarray:
    rate, data = wavfile.read(VOICE / f"{line_id}.wav")
    x = data.astype(np.float64) / 32768.0
    if x.ndim > 1:
        x = x.mean(axis=1)
    if rate != SR:
        g = np.gcd(SR, rate)
        x = signal.resample_poly(x, SR // g, rate // g)
    return x


def level(x: np.ndarray, target_db: float) -> np.ndarray:
    """Brings a line to a speaking level, measured only where there is speech."""
    frame = int(0.03 * SR)
    power = np.array([np.mean(x[i:i + frame] ** 2) for i in range(0, len(x) - frame, frame)])
    loud = power[power > power.max() * 0.02]
    rms = np.sqrt(loud.mean()) if len(loud) else 1e-4
    return x * db(target_db) / max(rms, 1e-6)


def soften(x: np.ndarray, threshold_db=-14.0, ratio=3.0) -> np.ndarray:
    """A gentle compressor: evens out a synthesized voice's loud syllables."""
    env = np.sqrt(np.maximum(lowpass(x ** 2, 12), 1e-12))
    over = np.maximum(0.0, 20 * np.log10(env) - threshold_db)
    return x * db(-over * (1 - 1 / ratio))


def voices():
    track = np.zeros((N, 2))
    spans = []
    lounge = reverb_ir(0.55, 5200, 3)
    for line in CUT["lines"]:
        x = highpass(load_voice(line["id"]), 85)
        kind = line["treatment"]
        if kind == "narrator":
            x = x + lowpass(x, 240) * 0.35                   # a little warmth; this voice has no top to tame
            x = soften(level(x, -17.5))
            y = wet(x, lounge, 0.03)
            gain = 1.0
        elif kind == "recorded":
            x = bandpass(x, 300, 3500, 3)                    # a message played back: a small speaker's voice
            x = np.tanh(level(x, -16.5) * 2.2) / 2.2
            y = wet(soften(x), lounge, 0.16)
            gain = 0.95
        elif kind == "outdoors":
            x = soften(level(highpass(x, 140), -18.5))
            y = np.stack([x, x], axis=1)
            gain = 0.9
        elif kind == "close":
            x = soften(level(x, -18.0))
            y = wet(x, lounge, 0.1)
            gain = 0.95
        else:                                                # in the lounge
            x = soften(level(x, -18.0))
            y = wet(x, lounge, 0.14)
            gain = 0.95
        place(track, y, line["at"], gain)
        spans.append((line["at"], line["at"] + len(x) / SR))
    return track, spans


def duck(spans) -> np.ndarray:
    """How far the music stands back while someone speaks."""
    g = np.zeros(N)
    for start, end in spans:
        i0, i1 = int((start - 0.35) * SR), int((end + 0.1) * SR)
        g[max(0, i0):min(N, i1)] = 1.0
    g = lowpass(g, 1.2, 1)
    return np.clip(g, 0, 1)


def write(name: str, x: np.ndarray) -> None:
    wavfile.write(OUT / name, SR, np.clip(x * 32767.0, -32768, 32767).astype(np.int16))


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    print("score...")
    music = score()
    print("effects...")
    fx = effects()
    print("voices...")
    speech, spans = voices()
    d = duck(spans)
    # the score is restrained on its own, and stands well back under a voice: speech stays a good ten decibels clear
    music_in = music * 0.72 * (1.0 - 0.74 * d)[:, None]
    fx_in = fx * (1.0 - 0.35 * d)[:, None]
    # the film opens out of silence and the last chord rings into black
    fade = np.ones(N)
    fade[: int(0.5 * SR)] = np.linspace(0, 1, int(0.5 * SR))
    tail = int(1.5 * SR)
    fade[-tail:] = np.linspace(1, 0, tail)
    mix = (music_in + fx_in + speech) * fade[:, None]
    peak = np.abs(mix).max()
    mix = np.tanh(mix / max(peak, 1e-6) * 1.3) / np.tanh(1.3) * db(-1.0)       # a soft ceiling a decibel under full scale
    write("music.wav", music * 0.72 / max(np.abs(music).max() * 0.72, 1.0))
    write("effects.wav", fx / max(np.abs(fx).max(), 1.0))
    write("voices.wav", speech / max(np.abs(speech).max(), 1.0))
    write("mix.wav", mix)
    rms = 20 * np.log10(np.sqrt(np.mean(mix ** 2)) + 1e-9)
    print(f"mix: {TOTAL:.1f} s, peak {20 * np.log10(np.abs(mix).max()):.1f} dBFS, rms {rms:.1f} dBFS")
    for name, part in (("music", music_in), ("effects", fx_in), ("voices", speech)):
        print(f"  {name}: peak {20 * np.log10(np.abs(part).max() + 1e-9):.1f} dB, rms {20 * np.log10(np.sqrt(np.mean(part ** 2)) + 1e-9):.1f} dB")
    print("  each line against what plays under it (speech should stay about 10 dB clear):")
    for (start, end), line in zip(spans, CUT["lines"]):
        a, b = int(start * SR), min(N, int(end * SR))
        under = music_in[a:b] + fx_in[a:b]
        clear = 20 * np.log10(np.sqrt(np.mean(speech[a:b] ** 2)) + 1e-9) - 20 * np.log10(np.sqrt(np.mean(under ** 2)) + 1e-9)
        print(f"    {start:6.1f} s  {line['id']:14s} {clear:5.1f} dB")


if __name__ == "__main__":
    main()
