"""Cuts the screen-recording demo of the native app in Meta XR Simulator.

The picture is the simulator's own window, as FFmpeg recorded it during the acceptance runs of 13 November 2026
(scripts/native/acceptance.ps1: gdigrab of the Meta XR Simulator window while the app ran in Unity Play mode with
simulated hands, 15 frames a second). Those recordings are in docs/native/evidence/2026-11-13. This script only
cuts them: it trims, speeds up the stretches where the simulator moves a hand slowly (and says so on screen, with the
factor), and puts a column of text beside the window. It adds nothing to the window itself.

Three kinds of footage, each marked on screen:
  * hands: the app driven by simulated hands (REAL TIME or TIME-LAPSE xN);
  * test hook: screens that the acceptance run's render tour opens through the app's development probe, not by hand;
  * live mode: the same app signed in to a local server (acceptance-035823), for the hand interactions the sample-only
    run doesn't have (typing a reply, choosing furniture). Not part of the build for judges.

The sound: the screen recording has none. The voice note and the press click are the app's own sound, taken from the
captures of the first demo (demo-build/capture/*.wav, what the AudioListener heard), placed where the app played them;
the soft score and room tone are scripts/demo/trailer_audio.py's, as in the other films. Writes

    demo-build/SameSky_Simulator_Screen_Recording_Demo.mp4       1920x1080, 30 fps, H.264 + AAC

    python scripts/demo/screenrec_edit.py [--keep]        (--keep: reuse the parts already cut)
"""

from __future__ import annotations

import io
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from scipy.io import wavfile

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import demo_audio as da          # noqa: E402  (the score, the speech level, edges)
import trailer_audio as ta       # noqa: E402

ROOT = HERE.parents[1]
EVIDENCE = ROOT / "docs" / "native" / "evidence" / "2026-11-13"
CAPTURE = ROOT / "demo-build" / "capture"
OUTDIR = ROOT / "demo-build" / "screenrec"
PARTS = OUTDIR / "parts"
EDIT = OUTDIR / "edit"
AUDIO = OUTDIR / "audio"
FONTS = ROOT / "native" / "quest" / "Assets" / "SameSky" / "Art" / "Fonts" / "Figtree"
FINAL = ROOT / "demo-build" / "SameSky_Simulator_Screen_Recording_Demo.mp4"
SR = ta.SR
FPS = 30
W, H = 1920, 1080
WIN_W = 1436                      # the simulator window (1582 x 1190) scaled to the frame's height
BG = "0x11161A"

SRC = {
    "S2": EVIDENCE / "acceptance-033533" / "session2-sample.mp4",      # sample-only build: the sample day, by hand
    "T3": EVIDENCE / "acceptance-033533" / "session3-tracking.mp4",    # sample-only build: tracking lost mid-press
    "L3": EVIDENCE / "acceptance-035823" / "session3-live.mp4",        # live mode: a reply typed by hand
    "L5": EVIDENCE / "acceptance-035823" / "session5-live-api-restarted.mp4",   # live mode: furniture kept by hand
}
# seconds into each recording at which a probe event with this clock reading happened: the recorder started about 0.8 s
# after the probe clock reading taken when it was asked to start (measured from the visual changes in S2: 21.2)
OFFSET = {"S2": 21.2, "T3": 20.8, "L3": 13.6, "L5": 16.5}

CHAPTERS = ["Start", "The cup", "Our day and more", "Comfort and safety", "Live mode extras"]

# scenes: the chapter, the kind of footage, the caption, and the stretches of the recordings it is made of:
# (source, from, to, speed). Times are seconds in the source recording.
SCENES = [
    dict(ch=0, kind="hands", pieces=[("S2", 0.0, 3.4, 1)],
         text="The welcome panel of the build for judges. There is no server behind it: the sample day only."),
    dict(ch=0, kind="hands", pieces=[("S2", 3.4, 10.0, 2.5)],
         text="A simulated fingertip aims at Try a sample day. The simulator moves hands and head together, so the view travels."),
    dict(ch=0, kind="hands", pieces=[("S2", 10.0, 12.8, 1)],
         text="The press opens the sample day: a lounge at dusk, Maya's chair empty, and a cup on the table."),
    dict(ch=1, kind="hands", pieces=[("S2", 12.8, 15.4, 1)],
         text="A cue says what to do. The cup holds a message from Maya."),
    dict(ch=1, kind="hands", pieces=[("S2", 15.4, 19.6, 2.5)],
         text="The hand reaches for the cup."),
    dict(ch=1, kind="hands", pieces=[("S2", 19.6, 29.9, 1)], voice=("S2", 20.4),
         text="A palm grab lifts the cup, and Maya's recorded message plays from it, with captions: “Saved you a seat. The bench by the big oak, same as always.”"),
    dict(ch=1, kind="hands", pieces=[("S2", 29.9, 33.4, 1)],
         text="When the note ends, Maya's chair fills: simulated presence, labelled as such in the app. Letting go leaves the cup where it is."),
    dict(ch=2, kind="hands", pieces=[("S2", 33.4, 42.6, 4)],
         text="The hand travels to the dock at the table edge."),
    dict(ch=2, kind="hands", pieces=[("S2", 42.6, 47.0, 1)],
         text="Our day, pressed by hand: the story of what two people shared, with how each part was made."),
    dict(ch=2, kind="hook", pieces=[("S2", 72.0, 77.0, 1)],
         text="The next screens are opened by a test hook, not by hand, so each can be seen. The story plays on the room's screen, with captions."),
    dict(ch=2, kind="hook", pieces=[("S2", 50.6, 53.4, 1)],
         text="Share: who receives a moment is named before the last button, and the AI permission is its own switch."),
    dict(ch=2, kind="hook", pieces=[("S2", 53.8, 56.6, 1)],
         text="Together: both local times, and the next stretch you both usually have."),
    dict(ch=2, kind="hook", pieces=[("S2", 56.9, 59.8, 1), ("S2", 60.1, 62.9, 1)],
         text="Keepsakes: what two people have earned, and the shelf in the alcove."),
    dict(ch=2, kind="hook", pieces=[("S2", 95.4, 99.0, 1), ("S2", 113.8, 122.0, 1.4)],
         text="Market: try a table, chairs, rug, lamp, plant, walls and floor, or a whole look. Nothing is sold."),
    dict(ch=3, kind="hands", pieces=[("S2", 129.0, 145.6, 6), ("S2", 145.6, 149.8, 1)],
         text="Recenter: after the head has turned away, one press puts the table back in front of the person."),
    dict(ch=3, kind="hands", pieces=[("S2", 150.0, 177.8, 8), ("S2", 177.8, 183.0, 1)],
         text="Pause stops sound and stories. Recenter, settings and Leave stay within reach."),
    dict(ch=3, kind="hands", pieces=[("S2", 183.0, 204.2, 8), ("S2", 204.2, 207.6, 1)],
         text="Resume returns to the room."),
    dict(ch=3, kind="hands", pieces=[("T3", 4.5, 9.5, 1), ("T3", 9.5, 16.0, 3), ("T3", 16.0, 18.4, 1)],
         text="Hand tracking lost in the middle of a press: both simulated hands vanish, the press is cancelled and nothing fires. The hands return with tracking."),
    dict(ch=4, kind="live", pieces=[("L3", 8.0, 19.0, 6), ("L3", 19.0, 22.0, 1), ("L3", 22.0, 30.4, 6), ("L3", 30.4, 32.4, 1),
                                    ("L3", 32.4, 54.8, 10), ("L3", 54.8, 58.8, 1)],
         text="Not in the judged build. Reply: fingertip presses on the spatial keyboard (the test types one letter), then Done; the note stands on the table."),
    dict(ch=4, kind="live", pieces=[("L5", 8.0, 18.2, 6), ("L5", 18.2, 21.5, 1), ("L5", 21.5, 31.5, 6), ("L5", 31.5, 34.5, 1),
                                    ("L5", 34.5, 48.0, 6), ("L5", 48.0, 53.0, 1), ("L5", 53.0, 67.0, 8), ("L5", 67.0, 71.0, 1)],
         text="Not in the judged build. Market: a fingertip chooses a chair fabric, and Keep saves it for the room and everyone in it."),
]
# presses whose click is laid under the picture: (source, seconds in that recording)
PRESSES = [("S2", 10.8), ("S2", 43.7), ("S2", 146.5), ("S2", 179.0), ("S2", 205.3),
           ("L3", 19.0), ("L3", 31.0), ("L3", 55.5), ("L5", 18.7), ("L5", 31.8), ("L5", 48.3), ("L5", 67.6)]
END_SECONDS = 6.0


def ffmpeg() -> str:
    found = shutil.which("ffmpeg")
    if found:
        return found
    for exe in (ROOT / "api" / ".venv" / "Lib" / "site-packages" / "imageio_ffmpeg" / "binaries").glob("ffmpeg*.exe"):
        return str(exe)
    raise SystemExit("FFmpeg not found")


def run(args, cwd=None) -> None:
    done = subprocess.run([ffmpeg(), "-y", "-hide_banner", "-loglevel", "error", *args], cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if done.returncode != 0:
        print(done.stdout)
        raise SystemExit(f"FFmpeg failed with exit code {done.returncode}")


def duration(path: Path) -> float:
    done = subprocess.run([ffmpeg(), "-hide_banner", "-i", str(path)], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    m = re.search(r"Duration: (\d+):(\d+):(\d+\.\d+)", done.stdout)
    return int(m.group(1)) * 3600 + int(m.group(2)) * 60 + float(m.group(3))


# ------------------------------------------------------------------------------------------------ the picture

def timeline():
    """Every stretch in order, with where it falls in the film: a list of dicts (scene index, source, a, b, speed, start, seconds)."""
    pieces, t = [], 0.0
    for i, scene in enumerate(SCENES):
        for src, a, b, speed in scene["pieces"]:
            seconds = round((b - a) / speed * FPS) / FPS
            pieces.append(dict(scene=i, src=src, a=a, b=b, speed=speed, start=t, seconds=seconds))
            t += seconds
    return pieces, t


def build_pieces(pieces, keep: bool) -> None:
    PARTS.mkdir(parents=True, exist_ok=True)
    for k, p in enumerate(pieces):
        out = PARTS / f"p{k:02d}.mp4"
        p["file"] = out
        if keep and out.exists():
            continue
        vf = (f"setpts=(PTS-STARTPTS)/{p['speed']},fps={FPS},scale={WIN_W}:{H}:flags=lanczos,"
              f"pad={W}:{H}:0:0:color={BG},format=yuv420p")
        run(["-ss", f"{p['a']}", "-t", f"{p['b'] - p['a']}", "-i", str(SRC[p["src"]]), "-vf", vf, "-an",
             "-c:v", "libx264", "-preset", "veryfast", "-crf", "10", "-r", str(FPS), str(out)])
    end = PARTS / "end.mp4"
    if not (keep and end.exists()):
        run(["-f", "lavfi", "-i", f"color=c={BG}:s={W}x{H}:r={FPS}:d={END_SECONDS}", "-vf", "format=yuv420p",
             "-c:v", "libx264", "-preset", "veryfast", "-crf", "10", str(end)])
    for p in pieces:   # the real lengths, as FFmpeg cut them
        p["seconds"] = duration(p["file"])
    t = 0.0
    for p in pieces:
        p["start"] = t
        t += p["seconds"]


# ------------------------------------------------------------------------------------------------ the text

def stamp(t: float) -> str:
    h, rem = divmod(max(0.0, t), 3600)
    m, s = divmod(rem, 60)
    return f"{int(h)}:{int(m):02d}:{s:05.2f}"


def tag_for(scene, piece):
    speed = piece["speed"]
    factor = f"{speed:g}"
    kind = scene["kind"]
    if kind == "hands":
        return ("TagReal", "REAL TIME") if speed == 1 else ("TagLapse", f"TIME-LAPSE ×{factor}")
    if kind == "hook":
        return ("TagHook", "TEST HOOK · NOT BY HAND" + ("" if speed == 1 else f" · ×{factor}"))
    return ("TagLive", "LIVE MODE · LOCAL SERVER · " + ("REAL TIME" if speed == 1 else f"×{factor}"))


def write_ass(path: Path, pieces, total: float) -> None:
    head = f"""[Script Info]
ScriptType: v4.00+
PlayResX: {W}
PlayResY: {H}
WrapStyle: 0
ScaledBorderAndShadow: yes
YCbCr Matrix: TV.709

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Brand,Figtree Bold,54,&H00F8F6F2,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.4,0,1,0,0,7,1470,34,0,1
Style: Dim,Figtree SemiBold,23,&H0091877D,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.3,0,1,0,0,7,1470,34,0,1
Style: ChapOff,Figtree SemiBold,33,&H0091877D,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.2,0,1,0,0,7,1470,34,0,1
Style: ChapOn,Figtree Bold,33,&H00F8F6F2,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.2,0,1,0,0,7,1470,34,0,1
Style: Cap,Figtree SemiBold,34,&H00F8F6F2,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.1,0,1,0,0,7,1470,40,0,1
Style: TagReal,Figtree SemiBold,21,&H00FFFFFF,&H00FFFFFF,&H004E6F2F,&H00000000,0,0,0,0,100,100,1.2,0,3,8,0,7,1470,34,0,1
Style: TagLapse,Figtree SemiBold,21,&H00FFFFFF,&H00FFFFFF,&H001F6A8A,&H00000000,0,0,0,0,100,100,1.2,0,3,8,0,7,1470,34,0,1
Style: TagHook,Figtree SemiBold,21,&H00FFFFFF,&H00FFFFFF,&H00935F2B,&H00000000,0,0,0,0,100,100,1.2,0,3,8,0,7,1470,34,0,1
Style: TagLive,Figtree SemiBold,21,&H00FFFFFF,&H00FFFFFF,&H00934C6A,&H00000000,0,0,0,0,100,100,1.2,0,3,8,0,7,1470,34,0,1
Style: Foot,Figtree,18,&H0091877D,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.2,0,1,0,0,1,1470,34,30,1
Style: EndTitle,Figtree Bold,132,&H00F8F6F2,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,1,0,1,0,0,8,0,0,0,1
Style: EndSub,Figtree SemiBold,44,&H0084B55D,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.4,0,1,0,0,8,0,0,0,1
Style: EndText,Figtree,30,&H00D8DEE2,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.3,0,1,0,0,8,360,360,0,1
Style: EndLink,Figtree SemiBold,34,&H00F8F6F2,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0.4,0,1,0,0,8,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    ev = []

    def add(a, b, style, text, layer=0):
        ev.append(f"Dialogue: {layer},{stamp(a)},{stamp(b)},{style},,0,0,0,,{text}")

    film_end = total - END_SECONDS
    add(0, film_end, "Brand", "{\\pos(1470,40)}SameSky")
    add(0, film_end, "Dim", "{\\pos(1470,108)}Meta XR Simulator, simulated Meta Quest 3")
    add(0, film_end, "Dim", "{\\pos(1470,140)}Simulated hand tracking · no controllers")
    add(0, film_end, "Dim", "{\\pos(1470,186)\\1c&H00403A33&\\p1}m 0 0 l 416 0 416 2 0 2{\\p0}")
    add(0, film_end, "Foot", "{\\an1\\pos(1470,1056)}Recorded 13 November 2026 \\N Sample day: fictional people, stock photos and footage, a synthesized voice")
    # the chapter list: one block of lines for each run of scenes in the same chapter
    runs, scene_start = [], {}
    for p in pieces:
        scene_start.setdefault(p["scene"], p["start"])
    scene_end = {}
    for p in pieces:
        scene_end[p["scene"]] = p["start"] + p["seconds"]
    for i, scene in enumerate(SCENES):
        a, b = scene_start[i], scene_end[i]
        if runs and runs[-1][0] == scene["ch"]:
            runs[-1][2] = b
        else:
            runs.append([scene["ch"], a, b])
    for ch, a, b in runs:
        for k, name in enumerate(CHAPTERS):
            y = 226 + 54 * k
            style = "ChapOn" if k == ch else "ChapOff"
            add(a, b, style, f"{{\\pos(1512,{y})}}{name}")
            colour = "&H0084B55D&" if k == ch else "&H00403A33&"
            add(a, b, style, f"{{\\pos(1470,{y + 6})\\1c{colour}\\p1}}m 0 0 l 22 0 22 22 0 22{{\\p0}}")
    # a caption for each scene and, for each stretch of it, the tag that says what kind of footage it is
    for i, scene in enumerate(SCENES):
        add(scene_start[i], scene_end[i], "Cap", "{\\fad(220,160)\\pos(1470,580)}" + scene["text"])
    for p in pieces:
        style, text = tag_for(SCENES[p["scene"]], p)
        add(p["start"], p["start"] + p["seconds"], style, "{\\pos(1478,524)}" + text)
    # the end card
    a = total - END_SECONDS
    add(a + 0.3, total, "EndTitle", "{\\fad(700,500)\\pos(960,250)}SameSky")
    add(a + 0.9, total, "EndSub", "{\\fad(700,500)\\pos(960,420)}Native Quest app · recorded in Meta XR Simulator")
    add(a + 1.5, total, "EndText", "{\\fad(700,500)\\pos(960,520)}Screen recordings of the real app, made on 13 November 2026. Simulated hands, offline sample day "
                                   "(fictional people, stock photos and footage, a synthesized voice). Not a headset test. "
                                   "Time-lapse and the kind of footage are marked beside the window throughout.")
    add(a + 2.2, total, "EndLink", "{\\fad(700,500)\\pos(960,800)}github.com/MoAmrYehia/SameSky")
    path.write_text(head + "\n".join(ev) + "\n", encoding="utf-8")


# ------------------------------------------------------------------------------------------------ the sound

def source_to_film(pieces, src: str, t: float):
    """Where in the film a moment of a source recording falls (None when the film doesn't show that moment)."""
    for p in pieces:
        if p["src"] == src and p["a"] <= t <= p["b"]:
            return p["start"] + (t - p["a"]) / p["speed"]
    return None


def wav_clip(name: str, a: float, b: float) -> np.ndarray:
    x = da.read_wav(CAPTURE / f"{name}.wav")
    return x[int(a * SR):int(b * SR)].copy()


def build_audio(pieces, total: float) -> Path:
    AUDIO.mkdir(parents=True, exist_ok=True)
    n = int(round(total * SR))
    film_end = total - END_SECONDS
    # the score: the first demo's piano and pad, a bar at a time, with the left hand changing with the chapters
    starts = {}
    for p in pieces:
        starts.setdefault(SCENES[p["scene"]]["ch"], p["start"])
    sections = [
        (0.0, None, ["Am", "Fadd9"], 0.0, 0.45),
        (starts[0] + 3.0, "walk", ["F", "C", "G", "Am"], 0.7, 0.75),
        (starts[2], "roll", ["F", "C", "G", "Am"], 0.75, 0.75),
        (starts[3], "walk", ["F", "C/E", "Dm7", "G"], 0.7, 0.75),
        (starts[4], "roll", ["C", "G/B", "Am", "F"], 0.75, 0.8),
    ]
    end_bar = int(round((film_end + 0.8) / ta.BAR))
    rows, count = [], {}
    for bar in range(end_bar):
        t = bar * ta.BAR + 0.5 * ta.BAR
        index = max(i for i, s in enumerate(sections) if s[0] <= t or i == 0)
        _, style, chords, tune, pad = sections[index]
        k = count.get(index, 0)
        count[index] = k + 1
        chord = chords[k % len(chords)]
        notes = [(b, m, h, v * tune) for b, m, h, v in da.TUNES[chord][(k // len(chords)) % 2]] if tune else []
        rows.append((chord, style, notes, pad))
    rows.append(("C", "end", [], 1.0))
    music = da.score(rows, n)
    tone = ta.tone_bed(n / SR + 1, "lounge")[:n] * da.ROOM_GAIN
    app = np.zeros((n, 2))
    spans = []
    # the press click, from the first demo's capture of the Our day press
    info = json.loads((CAPTURE / "d20-day.json").read_text(encoding="utf-8-sig"))
    events = json.loads((CAPTURE / "d20-day.events.json").read_text(encoding="utf-8-sig"))
    press_at = next(e["t"] for e in events if e["kind"] == "press" and e["detail"] == "dock:day") - info["probeClockAtFirstFrame"]
    click = da.edge(wav_clip("d20-day", press_at - 0.03, press_at + 0.22), 0.004) * da.CLICK_GAIN
    for src, t in PRESSES:
        at = source_to_film(pieces, src, t)
        if at is not None:
            ta.place(app, click, at - 0.03)
    # Maya's voice note, as the app played it: from the pick-up to the end of her last word
    cup = json.loads((CAPTURE / "s04-cup.json").read_text(encoding="utf-8-sig"))
    cev = json.loads((CAPTURE / "s04-cup.events.json").read_text(encoding="utf-8-sig"))
    clock = cup["probeClockAtFirstFrame"]
    grab = next(e["t"] for e in cev if e["kind"] == "grab") - clock
    voice_end = next(e["t"] for e in cev if e["kind"] == "voice" and e["detail"] == "finished") - clock
    voice = wav_clip("s04-cup", grab - 0.15, voice_end + 0.6)
    voice = da.edge(da.narrow(voice) * da.speech_gain(voice), 0.01)
    scene = next(s for s in SCENES if s.get("voice"))
    at = source_to_film(pieces, *scene["voice"])
    ta.place(app, voice, at - 0.05)   # the pick-up tick leads the card that opens on screen by a tenth of a second
    spans.append((at, at + voice_end - grab))
    d = da.duck(spans, n)
    music_in = music * da.MUSIC_GAIN * (1.0 - 0.74 * d)[:, None]
    # how far the voice stands above what plays under it, measured where it speaks
    i0, i1 = int(spans[0][0] * SR), int(spans[0][1] * SR)
    voice_rms, under_rms = da.level_db(app[i0:i1]), da.level_db((music_in + tone)[i0:i1])
    print(f"voice note {voice_rms:.1f} dBFS over {under_rms:.1f} dBFS of music and room: {voice_rms - under_rms:.1f} dB clear")
    fade = np.ones(n)
    fade[: int(0.4 * SR)] = np.linspace(0, 1, int(0.4 * SR))
    tail = int(1.8 * SR)
    fade[-tail:] = np.linspace(1, 0, tail)
    mix = (music_in + tone + app) * fade[:, None]
    peak = np.abs(mix).max()
    mix = np.tanh(mix / max(peak, 1e-6) * 1.3) / np.tanh(1.3) * ta.db(-1.0)
    path = AUDIO / "mix.wav"
    wavfile.write(path, SR, np.clip(mix * 32767.0, -32768, 32767).astype(np.int16))
    print(f"sound: voice note at {at:.2f} s of the film, {sum(1 for s, t in PRESSES if source_to_film(pieces, s, t) is not None)} clicks, "
          f"peak {20 * np.log10(np.abs(mix).max()):.1f} dBFS")
    return path


# ------------------------------------------------------------------------------------------------ the film

def main() -> None:
    keep = "--keep" in sys.argv
    missing = [str(p) for p in SRC.values() if not p.exists()]
    if missing:
        raise SystemExit("recordings missing: " + ", ".join(missing))
    pieces, total_pieces = timeline()
    print(f"{len(pieces)} stretches of the recordings, {total_pieces:.1f} s of picture")
    build_pieces(pieces, keep)
    total = sum(p["seconds"] for p in pieces) + END_SECONDS
    print(f"film: {total:.2f} s")
    EDIT.mkdir(parents=True, exist_ok=True)
    fonts = EDIT / "fonts"
    fonts.mkdir(exist_ok=True)
    for f in FONTS.glob("*.ttf"):
        shutil.copy2(f, fonts / f.name)
    write_ass(EDIT / "overlay.ass", pieces, total)
    mix = build_audio(pieces, total)
    names = [p["file"].name for p in pieces] + ["end.mp4"]
    (EDIT / "parts.txt").write_text("".join(f"file '../parts/{n}'\n" for n in names), encoding="utf-8")
    vf = (f"fps={FPS},ass=overlay.ass:fontsdir=fonts,fade=t=in:st=0:d=0.4,fade=t=out:st={total - 1.2:.2f}:d=1.2,format=yuv420p")
    run(["-f", "concat", "-safe", "0", "-i", "parts.txt", "-i", str(mix), "-vf", vf, "-map", "0:v", "-map", "1:a",
         "-t", f"{total:.3f}", "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-profile:v", "high", "-pix_fmt", "yuv420p",
         "-r", str(FPS), "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart",
         "-metadata", "title=SameSky: screen recording of the native app in Meta XR Simulator",
         "-metadata", "comment=Cut from FFmpeg recordings of the Meta XR Simulator window (13 November 2026). Simulated hands; sample day; sound is the app's own voice note and click plus a synthesized score.",
         str(FINAL)], cwd=EDIT)
    print(f"wrote {FINAL.relative_to(ROOT)} ({FINAL.stat().st_size / 1e6:.1f} MB, {total:.2f} s)")
    (OUTDIR / "timeline.json").write_text(json.dumps(
        [dict(scene=p["scene"], src=p["src"], a=p["a"], b=p["b"], speed=p["speed"], start=round(p["start"], 3), seconds=round(p["seconds"], 3)) for p in pieces],
        indent=1), encoding="utf-8")


if __name__ == "__main__":
    main()
