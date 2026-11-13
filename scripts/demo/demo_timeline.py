"""The timeline of the demo video "I Saved You a Seat", worked out from scripts/demo/demo_cut.json.

Both demo_audio.py (the sound) and demo_edit.py (the picture) read the cut through this module, so a clip is the same
length, to the frame, in each.

A clip in the cut is one of:
  * a stretch of a captured clip:  {"src": "<name in demo-build/capture>", "in": s, "out": s}  (+ "speed", "hold")
  * the key presses of the typing take, with the waits between them cut:  {"src": ..., "keys": {...}}
  * the companion recording, shown as a phone:  {"phone": "companion", "in": s, "out": s, "speed": x}
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CUT = json.loads((ROOT / "scripts" / "demo" / "demo_cut.json").read_text(encoding="utf-8"))
CAPTURE = ROOT / "demo-build" / "capture"
DEMO = ROOT / "demo-build" / "demo"
FPS = int(CUT.get("fps", 30))
NAME = "SameSky_I_Saved_You_A_Seat_Demo"


def frames(seconds: float) -> int:
    return int(round(seconds * FPS))


def snap(seconds: float) -> float:
    return frames(seconds) / FPS


def clip_events(src: str):
    """A captured clip's event log, with each time counted from the clip's first frame."""
    info = json.loads((CAPTURE / f"{src}.json").read_text(encoding="utf-8-sig"))
    events = json.loads((CAPTURE / f"{src}.events.json").read_text(encoding="utf-8-sig"))
    t0 = float(info["probeClockAtFirstFrame"])
    return [{**e, "t": float(e["t"]) - t0} for e in events]


def key_windows(clip) -> list[tuple[float, float]]:
    """The moments around each key press of a typing take: (start, end) in the take's own seconds, whole frames."""
    k = clip["keys"]
    presses = [e["t"] for e in clip_events(clip["src"]) if e["kind"] == "press" and e["detail"].startswith("key:") and e["detail"] != "key:done"]
    presses = presses[int(k.get("skipFirst", 0)):]
    n = frames(k["before"] + k["after"])
    out = []
    for t in presses:
        a = snap(t - k["before"])
        out.append((a, a + n / FPS))
    return out


def timeline():
    """The clips in order, each with its start and length in the film (seconds, whole frames), and the film's length."""
    clips = []
    t = 0.0
    for c in CUT["clips"]:
        c = dict(c)
        if "keys" in c:
            c["windows"] = key_windows(c)
            seconds = sum(b - a for a, b in c["windows"])
        else:
            seconds = (c["out"] - c["in"]) / c.get("speed", 1.0) + c.get("hold", 0.0)
        c["frames"] = frames(seconds)
        c["seconds"] = c["frames"] / FPS
        c["start"] = t
        t = round(t + c["seconds"], 6)
        clips.append(c)
    return clips, t


def at(clips, clip_id: str, offset: float = 0.0) -> float:
    """A time in the film: so many seconds into the named clip."""
    for c in clips:
        if c["id"] == clip_id:
            return c["start"] + offset
    raise KeyError(f"no clip called {clip_id} in the cut")


def clip_to_film(clip, source_time: float) -> float:
    """Where a moment of a clip's own source lands in the film (plain clips and the phone insert)."""
    return clip["start"] + (source_time - clip["in"]) / clip.get("speed", 1.0)


if __name__ == "__main__":
    cs, total = timeline()
    for c in cs:
        print(f"{c['start']:7.2f}  {c['seconds']:6.2f}  {c['id']:10s} {c.get('src', c.get('phone', ''))}")
    print(f"total {total:.2f} s ({int(total // 60)}:{total % 60:05.2f})")
