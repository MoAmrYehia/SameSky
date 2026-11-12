"""The sound of the demo video "I Saved You a Seat".

Four layers, mixed here:
  * the app's own sound, taken from the captured clips (each demo-build/capture/<clip>.wav is what the app played
    while it was filmed): the press clicks and Maya's recorded voice note. The voice note is brought up to a speaking
    level; nothing else is done to it. Its first words are also heard once early, over the opening shot;
  * the narration: the lines of scripts/demo/demo_lines.json, a synthesized voice (scripts/demo/trailer_voices.ps1);
  * the score: the trailer's soft piano and pad (scripts/demo/trailer_audio.py: every note is synthesized here),
    arranged to this film's sections and standing back under every voice;
  * a quiet room tone, synthesized. The app itself is silent between its sounds, so this bed is the edit's, not the
    app's.

Writes demo-build/demo/audio/{music,app,narration,mix}.wav (48 kHz stereo).

    python scripts/demo/demo_audio.py        (needs numpy and scipy)
"""

from __future__ import annotations

import numpy as np
from scipy import signal
from scipy.io import wavfile

import demo_timeline as tl
import trailer_audio as ta

CUT = tl.CUT
SR = ta.SR
OUT = tl.DEMO / "audio"
ta.VOICE = tl.DEMO / "voice"          # where this film's narration lines are

SPEECH_DB = -17.5                     # the level every voice is brought to (measured only where there is speech)
CLICK_GAIN = ta.db(5.0)               # the app's press clicks: a little up, so they read under the music
MUSIC_GAIN = 0.72
ROOM_GAIN = 0.9

# The score, section by section: from the named clip on, how the left hand plays, the chords in turn, and whether the
# right hand plays a tune (0: none). The film opens on the room and her voice; the piano comes in with the second shot.
SECTIONS = [
    ("hook", None, ["Am", "Fadd9"], 0.0, 0.45),
    ("away", "walk", ["Am", "F", "C", "G"], 0.9, 0.75),
    ("cup", "hold", ["F", "Am", "Fadd9", "C/E", "G", "F"], 0.0, 0.7),
    ("maya", "walk", ["C", "G"], 0.9, 0.75),
    ("play", "roll", ["F", "C", "G", "Am"], 1.0, 0.75),
    ("reply", "walk", ["F", "C", "G", "Am"], 0.9, 0.75),
    ("companion", "roll", ["F", "C", "G", "Am"], 1.0, 0.75),
    ("keepsakes", "walk", ["F", "C/E", "Dm7", "G"], 0.9, 0.75),
    ("payoff", "roll", ["C", "G/B", "Am", "F"], 1.1, 0.8),
]
TUNES = {   # over one bar of each chord, two ways: (beat, note, beats held, how hard)
    "Am": [[(0, 72, 2, 0.4), (2, 76, 2, 0.4)], [(0, 72, 3, 0.42)]],
    "F": [[(0, 77, 3, 0.42), (3, 76, 1, 0.36)], [(0, 69, 1, 0.42), (1, 72, 1, 0.42), (2, 77, 2, 0.46)]],
    "C": [[(0, 76, 2, 0.44), (2, 79, 2, 0.44)], [(0, 72, 2, 0.4), (2, 79, 2, 0.42)]],
    "G": [[(0, 74, 3, 0.4)], [(0, 74, 2, 0.44), (2, 71, 2, 0.4)]],
    "C/E": [[(0, 67, 4, 0.4)], [(2, 79, 2, 0.34)]],
    "Dm7": [[(0, 65, 2, 0.4), (2, 62, 2, 0.38)], [(0, 69, 4, 0.38)]],
    "G/B": [[(0, 74, 2, 0.46), (2, 79, 2, 0.48)], [(0, 74, 4, 0.44)]],
    "Fadd9": [[(2, 79, 2, 0.32)], [(1, 76, 3, 0.32)]],
}


def read_wav(path) -> np.ndarray:
    rate, data = wavfile.read(path)
    x = data.astype(np.float64) / (32768.0 if data.dtype == np.int16 else 1.0)
    if x.ndim == 1:
        x = np.stack([x, x], axis=1)
    if rate != SR:
        g = np.gcd(SR, rate)
        x = signal.resample_poly(x, SR // g, rate // g, axis=0)
    return x


def edge(x: np.ndarray, seconds: float = 0.008) -> np.ndarray:
    """A short fade at each end of a piece, so a cut in the sound never clicks."""
    n = min(int(seconds * SR), len(x) // 2)
    if n <= 0:
        return x
    x = x.copy()
    ramp = np.linspace(0.0, 1.0, n)[:, None]
    x[:n] *= ramp
    x[-n:] *= ramp[::-1]
    return x


def speech_gain(x: np.ndarray) -> float:
    """The gain that brings a stretch of speech to the film's speaking level."""
    mono = x.mean(axis=1) if x.ndim > 1 else x
    frame = int(0.03 * SR)
    power = np.array([np.mean(mono[i:i + frame] ** 2) for i in range(0, len(mono) - frame, frame)])
    loud = power[power > power.max() * 0.02]
    rms = np.sqrt(loud.mean()) if len(loud) else 1e-4
    return ta.db(SPEECH_DB) / max(rms, 1e-6)


def narrow(x: np.ndarray, amount: float = 0.3) -> np.ndarray:
    """Brings a sound that the app placed to one side a little toward the middle."""
    left, right = x[:, 0], x[:, 1]
    return np.stack([left * (1 - amount) + right * amount, right * (1 - amount) + left * amount], axis=1)


def app_sound(clips, n: int):
    """The app's own sound under each clip, and where its recorded voice plays (for the music to stand back)."""
    track = np.zeros((n, 2))
    spans, report = [], []
    cache = {}

    def source(name):
        if name not in cache:
            cache[name] = read_wav(tl.CAPTURE / f"{name}.wav")
        return cache[name]

    for c in clips:
        if "src" not in c or c.get("sound", 1.0) == 0 or c.get("speed", 1.0) != 1.0:
            continue
        if not (tl.CAPTURE / f"{c['src']}.wav").exists():
            continue
        x = source(c["src"])
        if "windows" in c:
            piece = np.concatenate([edge(x[int(a * SR):int(b * SR)], 0.004) for a, b in c["windows"]])
            piece = piece * CLICK_GAIN
        else:
            a, b = int(c["in"] * SR), int(c["out"] * SR)
            piece = x[a:b].copy()
            gain = np.full(len(piece), CLICK_GAIN)
            for v0, v1 in c.get("voice", []):          # stretches of the clip where the app plays a voice
                i0, i1 = max(0, int((v0 - c["in"]) * SR)), min(len(piece), int((v1 - c["in"]) * SR))
                if i1 <= i0:
                    continue
                g = speech_gain(piece[i0:i1])
                gain[i0:i1] = g
                spans.append((c["start"] + i0 / SR, c["start"] + i1 / SR))
                report.append((c["id"], c["start"] + i0 / SR, 20 * np.log10(g)))
            gain = ta.lowpass(np.concatenate([np.full(2400, gain[0]), gain, np.full(2400, gain[-1])]), 40, 1)[2400:-2400]
            piece = narrow(piece) * gain[:, None]
        piece = edge(piece * c.get("sound", 1.0))
        ta.place(track, piece, c["start"])
    pre = CUT.get("prelap")
    if pre:
        x = source(pre["src"])
        piece = x[int(pre["in"] * SR):int(pre["out"] * SR)]
        piece = edge(narrow(piece, 0.4) * speech_gain(piece) * ta.db(-1.5), 0.03)
        t = tl.at(clips, pre["clip"], pre["at"])
        room = ta.reverb_ir(0.9, 4800, 11)
        ta.place(track, ta.wet(piece, room, 0.16), t)
        spans.append((t, t + len(piece) / SR))
        report.append(("prelap", t, 0.0))
    return track, spans, report


def narration(clips, n: int):
    track = np.zeros((n, 2))
    spans = []
    lounge = ta.reverb_ir(0.55, 5200, 3)
    for line in CUT["narration"]:
        x = ta.highpass(ta.load_voice(line["line"]), 85)
        x = x + ta.lowpass(x, 240) * 0.35                    # a little warmth; this voice has no top to tame
        x = ta.soften(ta.level(x, SPEECH_DB))
        t = tl.at(clips, line["clip"], line["at"])
        ta.place(track, ta.wet(x, lounge, 0.03), t)
        spans.append((t, t + len(x) / SR))
    return track, spans


def score_rows(clips, total: float):
    """One row for each bar of the film: (chord, left-hand style or None, the tune, how loud the pad is)."""
    starts = [(tl.at(clips, first), style, chords, tune, pad) for first, style, chords, tune, pad in SECTIONS]
    card = CUT["endCard"]
    end_bar = int(round((tl.at(clips, card["clip"], card["at"]) + 0.6) / ta.BAR))
    rows = []
    count = {}
    for bar in range(end_bar):
        t = bar * ta.BAR + 0.5 * ta.BAR                     # the section this bar mostly lies in
        index = max(i for i, s in enumerate(starts) if s[0] <= t or i == 0)
        _, style, chords, tune, pad = starts[index]
        k = count.get(index, 0)
        count[index] = k + 1
        chord = chords[k % len(chords)]
        notes = [(b, m, h, v * tune) for b, m, h, v in TUNES[chord][(k // len(chords)) % 2]] if tune else []
        rows.append((chord, style, notes, pad))
    rows.append(("C", "end", [], 1.0))
    return rows


def score(rows, n: int) -> np.ndarray:
    """The trailer's piano and pad (trailer_audio.py), playing this film's bars."""
    dry = np.zeros((n, 2))
    pad = np.zeros((n, 2))
    rng = np.random.default_rng(31)
    for bar, (chord, style, tune, pad_level) in enumerate(rows):
        t0 = bar * ta.BAR
        tones = ta.CHORDS[chord]
        for beat, which, held, hard in (ta.LEFT[style] if style else []):
            midi = tones[which] if which < 100 else tones[which - 100] + 12
            ta.place(dry, ta.piano(midi, hard, held * ta.BEAT), t0 + beat * ta.BEAT + rng.uniform(-0.008, 0.008), 0.9, (midi - 56) / 40)
        for beat, midi, held, hard in tune:
            ta.place(dry, ta.piano(midi, hard, held * ta.BEAT), t0 + beat * ta.BEAT + rng.uniform(-0.006, 0.01), 1.0, (midi - 60) / 36)
        m = int((ta.BAR + 2.6) * SR)
        t = np.arange(m) / SR
        env = np.clip(t / 1.8, 0, 1) * np.clip((ta.BAR + 2.6 - t) / 2.4, 0, 1)
        tone = np.zeros(m)
        for midi, amp in ((tones[0], 1.0), (tones[1], 0.55), (tones[2], 0.5), (tones[3] + 12, 0.22)):
            f = 440.0 * 2 ** ((midi - 69) / 12)
            for cents in (-4, 3.5):
                tone += amp * np.sin(2 * np.pi * f * 2 ** (cents / 1200) * t + rng.uniform(0, 6.28))
                tone += amp * 0.18 * np.sin(2 * np.pi * 2 * f * 2 ** (cents / 1200) * t + rng.uniform(0, 6.28))
        ta.place(pad, ta.lowpass(tone * env, 1300) * 0.03 * pad_level, t0 - 0.9)
    room = ta.reverb_ir(3.0, 4200, 5)
    return ta.wet(dry * 0.32, room, 0.42)[:n] + ta.wet(pad, room, 0.5)[:n]


def room_tone(clips, n: int) -> np.ndarray:
    """A quiet bed under the footage of the room (lower under the phone insert), so the film never falls to digital
    silence. It is the edit's sound: the app has no room tone of its own."""
    bed = ta.tone_bed(n / SR + 1, "lounge")[:n]
    level = np.zeros(n)
    for c in clips:
        i0, i1 = int(c["start"] * SR), min(n, int((c["start"] + c["seconds"]) * SR))
        level[i0:i1] = 0.35 if "phone" in c else 1.0
    return bed * ta.lowpass(level, 2.0, 1)[:, None]


def duck(spans, n: int) -> np.ndarray:
    g = np.zeros(n)
    for start, end in spans:
        g[max(0, int((start - 0.35) * SR)):min(n, int((end + 0.12) * SR))] = 1.0
    return np.clip(ta.lowpass(g, 1.2, 1), 0, 1)


def write(name: str, x: np.ndarray) -> None:
    wavfile.write(OUT / name, SR, np.clip(x * 32767.0, -32768, 32767).astype(np.int16))


def level_db(x: np.ndarray) -> float:
    return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    clips, total = tl.timeline()
    n = int(round(total * SR))
    print(f"film: {total:.2f} s")
    print("score...")
    music = score(score_rows(clips, total), n)
    print("the app's sound...")
    app, voice_spans, report = app_sound(clips, n)
    print("narration...")
    speech, line_spans = narration(clips, n)
    tone = room_tone(clips, n) * ROOM_GAIN
    d = duck(voice_spans + line_spans, n)
    music_in = music * MUSIC_GAIN * (1.0 - 0.74 * d)[:, None]
    fade = np.ones(n)
    fade[: int(0.4 * SR)] = np.linspace(0, 1, int(0.4 * SR))
    tail = int(1.6 * SR)
    fade[-tail:] = np.linspace(1, 0, tail)
    mix = (music_in + tone + app + speech) * fade[:, None]
    peak = np.abs(mix).max()
    mix = np.tanh(mix / max(peak, 1e-6) * 1.3) / np.tanh(1.3) * ta.db(-1.0)       # a soft ceiling a decibel under full scale
    write("music.wav", music * MUSIC_GAIN / max(np.abs(music).max() * MUSIC_GAIN, 1.0))
    write("app.wav", app / max(np.abs(app).max(), 1.0))
    write("narration.wav", speech / max(np.abs(speech).max(), 1.0))
    write("mix.wav", mix)
    print(f"mix: peak {20 * np.log10(np.abs(mix).max()):.1f} dBFS, rms {level_db(mix):.1f} dBFS")
    for name, part in (("music", music_in), ("room tone", tone), ("app", app), ("narration", speech)):
        print(f"  {name:10s} peak {20 * np.log10(np.abs(part).max() + 1e-9):6.1f} dB, rms {level_db(part):6.1f} dB")
    for who, t, gain in report:
        print(f"  app voice in {who} at {t:.2f} s: brought up {gain:.1f} dB")
    print("  each voice against what plays under it (speech should stay about 10 dB clear):")
    under_all = music_in + tone
    for (start, end), label in [(s, "recorded voice") for s in voice_spans] + [(s, line["line"]) for s, line in zip(line_spans, CUT["narration"])]:
        a, b = int(start * SR), min(n, int(end * SR))
        voice = app[a:b] if label == "recorded voice" else speech[a:b]
        print(f"    {start:6.1f} s  {label:15s} {level_db(voice) - level_db(under_all[a:b]):5.1f} dB")
    print("  the app's sound, clip by clip (peak):")
    for c in clips:
        a, b = int(c["start"] * SR), min(n, int((c["start"] + c["seconds"]) * SR))
        pk = np.abs(app[a:b]).max() if b > a else 0
        if pk > 0:
            print(f"    {c['start']:6.1f} s  {c['id']:10s} {20 * np.log10(pk):6.1f} dB")


if __name__ == "__main__":
    main()
