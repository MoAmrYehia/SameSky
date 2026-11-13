"""Cuts the animated film "Wish You Were Here" together.

Takes the rendered shots (demo-build/wish/shots/<id>.mp4, from the Unity film renderer), puts them in the order
scripts/demo/wish_cut.json gives (every cut is on a beat of the score), opens from black, lays the title and the
tagline over the sky shot (set in Figtree, SIL OFL), cuts to black where the cut says, and adds the sound mix from
scripts/demo/wish_audio.py. Nothing else is put on the picture: the film carries no subtitles; they are written
beside it as a file. Writes:

    demo-build/SameSky_Wish_You_Were_Here_Cinematic.mp4       1920x1080, 24 fps, H.264 + AAC
    docs/cinematic/SameSky_Wish_You_Were_Here_Cinematic.srt   the dialogue as subtitles

    python scripts/demo/wish_edit.py [--preview]     (--preview: cut the small preview shots in demo-build/wish/preview)
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CUT = json.loads((ROOT / "scripts" / "demo" / "wish_cut.json").read_text(encoding="utf-8"))
WISH = ROOT / "demo-build" / "wish"
EDIT = WISH / "edit"
FONTS = ROOT / "native" / "quest" / "Assets" / "SameSky" / "Art" / "Fonts" / "Figtree"
NAME = "SameSky_Wish_You_Were_Here_Cinematic"
FPS = int(CUT["fps"])


def ffmpeg() -> str:
    found = shutil.which("ffmpeg")
    if found:
        return found
    for exe in (ROOT / "api" / ".venv" / "Lib" / "site-packages" / "imageio_ffmpeg" / "binaries").glob("ffmpeg*.exe"):
        return str(exe)
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception as e:   # noqa: BLE001
        raise SystemExit("FFmpeg not found: install it, or run scripts/native/bootstrap.ps1") from e


def stamp(t: float, sep: str) -> str:
    t = max(0.0, t)
    h, rem = divmod(t, 3600)
    m, s = divmod(rem, 60)
    if sep == ",":   # SRT: milliseconds
        whole = int(s)
        ms = int(round((s - whole) * 1000))
        if ms == 1000:
            whole, ms = whole + 1, 0
        return f"{int(h):02d}:{int(m):02d}:{whole:02d},{ms:03d}"
    return f"{int(h)}:{int(m):02d}:{s:05.2f}"   # ASS: centiseconds


def write_srt(path: Path) -> None:
    rows = []
    for i, line in enumerate(CUT["lines"], 1):
        info = json.loads((WISH / "voice" / f"{line['id']}.json").read_text(encoding="utf-8-sig"))
        # the file runs on after the last word; the last mouth shape says where the line really ends (the synthesizer
        # times its shapes on a 16 kHz clock: see FilmActor.Say)
        spoken = float(info["visemes"][-1]["t"]) if info.get("visemes") else float(info["duration"])
        if spoken > float(info["duration"]) * 1.15:
            spoken *= 16000 / 44100
        start, end = line["at"] + 0.1, line["at"] + spoken + 0.45
        rows.append(f"{i}\n{stamp(start, ',')} --> {stamp(end, ',')}\n{line['who'].upper()}: {line['text']}\n")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(rows), encoding="utf-8")


def write_ass(path: Path, width: int, height: int) -> None:
    title = CUT["title"]
    k = height / 1080.0
    head = f"""[Script Info]
ScriptType: v4.00+
PlayResX: {width}
PlayResY: {height}
WrapStyle: 2
ScaledBorderAndShadow: yes
YCbCr Matrix: TV.709

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Title,Figtree SemiBold,{118 * k:.0f},&H00F6F4F2,&H00FFFFFF,&H64281E28,&H78140F14,0,0,0,0,100,100,{34 * k:.1f},0,1,0,{2.2 * k:.1f},5,0,0,0,1
Style: Tag,Figtree SemiBold,{42 * k:.0f},&H00F0F2F6,&H00FFFFFF,&H64281E28,&H78140F14,0,0,0,0,100,100,{5 * k:.1f},0,1,0,{1.6 * k:.1f},5,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    cx = width / 2
    # the wide letter-spacing puts a gap after the last letter too: the title is nudged right by half of it to sit centred
    events = [
        f"Dialogue: 0,{stamp(title['at'], '.')},{stamp(title['until'], '.')},Title,,0,0,0,,{{\\fad(1100,450)\\pos({cx + 17 * k:.0f},{392 * k:.0f})}}{title['text']}",
        f"Dialogue: 0,{stamp(title['taglineAt'], '.')},{stamp(title['until'], '.')},Tag,,0,0,0,,{{\\fad(900,450)\\pos({cx + 2.5 * k:.0f},{494 * k:.0f})}}{title['tagline']}",
    ]
    path.write_text(head + "\n".join(events) + "\n", encoding="utf-8")


def main() -> None:
    preview = "--preview" in sys.argv
    shots_dir = WISH / ("preview" if preview else "shots")
    shots = CUT["shots"]
    missing = [s["id"] for s in shots if not (shots_dir / f"{s['id']}.mp4").exists()]
    if missing:
        raise SystemExit("not rendered yet: " + ", ".join(missing))
    width, height = (960, 540) if preview else (1920, 1080)
    EDIT.mkdir(parents=True, exist_ok=True)
    fonts = EDIT / "fonts"
    fonts.mkdir(exist_ok=True)
    for f in FONTS.glob("*.ttf"):
        shutil.copy2(f, fonts / f.name)
    write_ass(EDIT / "title.ass", width, height)
    if not preview:
        write_srt(ROOT / "docs" / "cinematic" / f"{NAME}.srt")
    total = sum(s["seconds"] for s in shots)
    (EDIT / "shots.txt").write_text("".join(f"file '../{shots_dir.name}/{s['id']}.mp4'\n" for s in shots), encoding="utf-8")
    mix = WISH / "audio" / "mix.wav"
    if not mix.exists():
        raise SystemExit("no sound mix yet: run scripts/demo/wish_audio.py first")
    out = WISH / "preview.mp4" if preview else ROOT / "demo-build" / f"{NAME}.mp4"
    black = float(CUT["black"])
    video = (f"[0:v]fps={FPS},fade=t=in:st=0:d=0.35,ass=title.ass:fontsdir=fonts,"
             f"drawbox=x=0:y=0:w=iw:h=ih:color=black:t=fill:enable='gte(t,{black:.3f})',format=yuv420p[v]")
    command = [ffmpeg(), "-y", "-hide_banner", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", "shots.txt", "-i", "../audio/mix.wav",
               "-filter_complex", video, "-map", "[v]", "-map", "1:a", "-t", f"{total:.3f}", "-r", str(FPS),
               "-c:v", "libx264", "-preset", "veryfast" if preview else "slow", "-crf", "20" if preview else "16", "-profile:v", "high", "-pix_fmt", "yuv420p",
               "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart",
               "-metadata", "title=Wish You Were Here (SameSky)",
               "-metadata", "comment=An animated short. Rendered in Unity from scenes built for it; score and sound synthesized; voices are synthesized text-to-speech.",
               str(out)]
    done = subprocess.run(command, cwd=EDIT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if done.returncode != 0:
        print(done.stdout)
        raise SystemExit(f"FFmpeg failed with exit code {done.returncode}")
    print(f"wrote {out.relative_to(ROOT)} ({out.stat().st_size / 1e6:.1f} MB, {total:.2f} s)")


if __name__ == "__main__":
    main()
