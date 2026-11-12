"""Cuts the animated concept trailer "I Saved You a Seat" together.

Takes the rendered shots (demo-build/film/shots/<id>.mp4, from the Unity film renderer), puts them in the order
scripts/demo/trailer_cut.json gives, adds the one dissolve, the fade in and out, the English subtitles, the three
feature captions and the end card (text set in the app's own typeface, Figtree, SIL OFL), and the sound mix from
scripts/demo/trailer_audio.py. Writes:

    demo-build/SameSky_I_Saved_You_A_Seat_Animated_Trailer.mp4        1920x1080, 24 fps, H.264 + AAC
    docs/trailer/SameSky_I_Saved_You_A_Seat_Animated_Trailer.srt      the same subtitles as a file

    python scripts/demo/trailer_edit.py [--clean]      (--clean: no burned-in subtitles; captions and end card stay)
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CUT = json.loads((ROOT / "scripts" / "demo" / "trailer_cut.json").read_text(encoding="utf-8"))
FILM = ROOT / "demo-build" / "film"
EDIT = FILM / "edit"
FONTS = ROOT / "native" / "quest" / "Assets" / "SameSky" / "Art" / "Fonts" / "Figtree"
NAME = "SameSky_I_Saved_You_A_Seat_Animated_Trailer"


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
        return f"{int(h):02d}:{int(m):02d}:{int(s):02d},{int(round((s - int(s)) * 1000)):03d}"
    return f"{int(h)}:{int(m):02d}:{s:05.2f}"   # ASS: centiseconds


def spoken():
    """Each line with when it is on screen: from just after it starts to a moment after it ends."""
    out = []
    for line in CUT["lines"]:
        info = json.loads((FILM / "voice" / f"{line['id']}.json").read_text(encoding="utf-8-sig"))
        start = line["at"] + 0.12
        end = line["at"] + float(info["duration"]) + 0.3
        out.append((start, end, line))
    for i in range(len(out) - 1):   # never two at once
        if out[i][1] > out[i + 1][0] - 0.05:
            out[i] = (out[i][0], out[i + 1][0] - 0.05, out[i][2])
    return out


def write_srt(path: Path) -> None:
    rows = []
    for i, (start, end, line) in enumerate(spoken(), 1):
        who = line["who"]
        text = line["text"]
        if who.startswith("Narrator"):
            text = f"<i>{text}</i>"
        elif "recorded" in who:
            text = f"[Maya, recorded message] {text}"
        rows.append(f"{i}\n{stamp(start, ',')} --> {stamp(end, ',')}\n{text}\n")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(rows), encoding="utf-8")


def write_ass(path: Path, subtitles: bool) -> None:
    card = CUT["endCard"]
    head = """[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 0
ScaledBorderAndShadow: yes
YCbCr Matrix: TV.709

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Sub,Figtree SemiBold,46,&H00F4F8FA,&H00FFFFFF,&HB4101418,&H96000000,0,0,0,0,100,100,0.2,0,1,2.2,1.2,2,200,200,62,1
Style: Tag,Figtree SemiBold,27,&H00C2E4F6,&H00FFFFFF,&HB4101418,&H96000000,0,0,0,0,100,100,1.2,0,1,1.8,1,2,200,200,124,1
Style: Caption,Figtree SemiBold,58,&H00F2F6FA,&H00FFFFFF,&HA0101418,&H78000000,0,0,0,0,100,100,0.3,0,1,2.2,2,1,128,128,150,1
Style: Title,Figtree Bold,156,&H00F2F6FA,&H00FFFFFF,&H50101418,&H64000000,0,0,0,0,100,100,1,0,1,0,3,5,0,0,0,1
Style: Line,Figtree,50,&H00E4ECF2,&H00FFFFFF,&H50101418,&H64000000,0,0,0,0,100,100,0.4,0,1,0,2,5,0,0,0,1
Style: Small,Figtree SemiBold,40,&H0096CDF0,&H00FFFFFF,&H50101418,&H64000000,0,0,0,0,100,100,0.6,0,1,0,2,5,0,0,0,1
Style: Credit,Figtree,24,&H50DCE4EA,&H00FFFFFF,&H50101418,&H64000000,0,0,0,0,100,100,0.8,0,1,0,1,5,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    events = []

    def add(start, end, style, text, layer=0):
        events.append(f"Dialogue: {layer},{stamp(start, '.')},{stamp(end, '.')},{style},,0,0,0,,{text}")

    if subtitles:
        for start, end, line in spoken():
            text = line["text"]
            who = line["who"]
            if who.startswith("Narrator"):
                add(start, end, "Sub", "{\\fad(160,200)\\i1}" + text)
            else:
                add(start, end, "Sub", "{\\fad(120,180)}" + text)
                if "recorded" in who:
                    add(start, end, "Tag", "{\\fad(120,180)}MAYA · RECORDED MESSAGE")
    for c in CUT["captions"]:
        add(c["from"], c["to"], "Caption", "{\\fad(450,450)}" + c["text"], 1)
    t0, t1 = card["from"], card["to"]
    add(t0 + 0.5, t1, "Title", "{\\fad(900,700)\\pos(960,452)}" + card["title"], 2)
    add(t0 + 1.2, t1, "Line", "{\\fad(800,700)\\pos(960,590)}" + card["line"], 2)
    add(t0 + 1.9, t1, "Small", "{\\fad(800,700)\\pos(960,676)}" + card["small"], 2)
    add(t0 + 2.3, t1, "Credit", "{\\fad(700,700)\\pos(960,1012)}" + card["credit"], 2)
    path.write_text(head + "\n".join(events) + "\n", encoding="utf-8")


def main() -> None:
    clean = "--clean" in sys.argv
    EDIT.mkdir(parents=True, exist_ok=True)
    fonts = EDIT / "fonts"
    fonts.mkdir(exist_ok=True)
    for f in FONTS.glob("*.ttf"):
        shutil.copy2(f, fonts / f.name)
    write_ass(EDIT / "trailer.ass", subtitles=not clean)
    write_srt(ROOT / "docs" / "trailer" / f"{NAME}.srt")

    # the shots, in two runs either side of the one dissolve (the shot before it is rendered that much longer)
    shots = CUT["shots"]
    cut_at = next((i for i, s in enumerate(shots) if s.get("dissolve")), None)
    total = sum(s["seconds"] for s in shots)
    missing = [s["id"] for s in shots if not (FILM / "shots" / f"{s['id']}.mp4").exists()]
    if missing:
        raise SystemExit("not rendered yet: " + ", ".join(missing))

    def listing(name, part):
        (EDIT / name).write_text("".join(f"file '../shots/{s['id']}.mp4'\n" for s in part), encoding="utf-8")

    inputs = []
    if cut_at is None:
        listing("all.txt", shots)
        inputs += ["-f", "concat", "-safe", "0", "-i", "all.txt"]
        video = "[0:v]"
        audio_index = 1
        chain = ""
    else:
        d = float(shots[cut_at]["dissolve"])
        listing("a.txt", shots[:cut_at])
        listing("b.txt", shots[cut_at:])
        inputs += ["-f", "concat", "-safe", "0", "-i", "a.txt", "-f", "concat", "-safe", "0", "-i", "b.txt"]
        before = sum(s["seconds"] for s in shots[:cut_at])
        chain = f"[0:v]settb=AVTB,fps=24[a];[1:v]settb=AVTB,fps=24[b];[a][b]xfade=transition=fade:duration={d}:offset={before:.3f}[x];"
        video = "[x]"
        audio_index = 2
    out = ROOT / "demo-build" / (NAME + ("_clean" if clean else "") + ".mp4")
    filters = (chain + f"{video}fade=t=in:st=0:d=0.8,fade=t=out:st={total - 0.75:.2f}:d=0.75,"
               f"ass=trailer.ass:fontsdir=fonts,format=yuv420p[v]")
    command = [ffmpeg(), "-y", "-hide_banner", "-loglevel", "error", *inputs, "-i", "../audio/mix.wav",
               "-filter_complex", filters, "-map", "[v]", "-map", f"{audio_index}:a",
               "-t", f"{total:.3f}", "-r", "24", "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-profile:v", "high",
               "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart",
               "-metadata", "title=I Saved You a Seat (SameSky, animated concept trailer)",
               "-metadata", "comment=Animated concept trailer rendered from the SameSky Unity project. Voices are synthesized. Not gameplay footage.",
               str(out)]
    done = subprocess.run(command, cwd=EDIT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if done.returncode != 0:
        print(done.stdout)
        raise SystemExit(f"FFmpeg failed with exit code {done.returncode}")
    print(f"wrote {out.relative_to(ROOT)} ({out.stat().st_size / 1e6:.1f} MB, {total:.1f} s)")


if __name__ == "__main__":
    main()
