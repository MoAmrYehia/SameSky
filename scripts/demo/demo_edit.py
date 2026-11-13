"""Cuts the demo video "I Saved You a Seat" together from real footage of the app.

The picture is footage of the native Quest app running in Meta XR Simulator (demo-build/capture/*.mp4, filmed by
scripts/demo/film.ps1) and one insert of the companion web app (web/scripts/demo-companion.mjs), in the order
scripts/demo/demo_cut.json gives. Nothing in the app's picture is generated or retouched: the edit only trims, cuts
the waits out of the typing take, enlarges part of the frame where the cut says so, and frames the companion
recording as a phone. On top go the film's own text (subtitles, captions, labels that say what is sample or
simulated, the end card), set in Figtree (SIL OFL) and kept visually apart from the app's panels, and the sound mix
from scripts/demo/demo_audio.py. Writes:

    demo-build/SameSky_I_Saved_You_A_Seat_Demo.mp4       1920x1080, 30 fps, H.264 + AAC
    docs/demo/SameSky_I_Saved_You_A_Seat_Demo.srt        the subtitles as a file

    python scripts/demo/demo_edit.py [--only id,id] [--keep] [--clean]
        --only   rebuild just these clips before the final pass
        --keep   reuse the clips already built (demo-build/demo/parts)
        --clean  no burned-in subtitles (captions, labels and the end card stay)
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import demo_timeline as tl

ROOT, CUT, FPS, NAME = tl.ROOT, tl.CUT, tl.FPS, tl.NAME
PARTS = tl.DEMO / "parts"
EDIT = tl.DEMO / "edit"
FONTS = ROOT / "native" / "quest" / "Assets" / "SameSky" / "Art" / "Fonts" / "Figtree"
W, H = 1920, 1080
ENCODE = ["-an", "-c:v", "libx264", "-preset", "veryfast", "-crf", "12", "-pix_fmt", "yuv420p", "-r", str(FPS)]

# the phone the companion recording is shown in: where its screen sits in the frame
PHONE = {"x": 1170, "y": 31, "w": 470, "h": 1018, "bezel": 13, "radius": 40}


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


def run(args, cwd=None) -> None:
    done = subprocess.run([ffmpeg(), "-y", "-hide_banner", "-loglevel", "error", *args], cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    if done.returncode != 0:
        print(done.stdout)
        raise SystemExit(f"FFmpeg failed with exit code {done.returncode}")


def source_size(src: str) -> tuple[int, int]:
    info = json.loads((tl.CAPTURE / f"{src}.json").read_text(encoding="utf-8-sig"))
    return int(info["width"]), int(info["height"])


# ------------------------------------------------------------------------------------------- the clips

def view_filter(clip) -> str:
    """The part of the captured frame a clip shows: all of it, a fixed part ("at": [centre x, centre y, share of the
    width]), or a move from one part to another ("from"/"to" over "start".."end" seconds), eased."""
    v = clip.get("view")
    sw, sh = source_size(clip["src"])
    if not v:
        return f"scale={W}:{H}:flags=lanczos"
    if "at" in v:
        cx, cy, s = v["at"]
        w = int(round(s * sw / 2)) * 2
        h = int(round(s * sh / 2)) * 2
        x = int(min(max(cx * sw - w / 2, 0), sw - w))
        y = int(min(max(cy * sh - h / 2, 0), sh - h))
        return f"crop={w}:{h}:{x}:{y},scale={W}:{H}:flags=lanczos"
    (x0, y0, s0), (x1, y1, s1) = v["from"], v["to"]
    t0, t1 = v["start"], v["end"]
    ease = f"st(0,clip((on/{FPS}-{t0})/({t1}-{t0}),0,1));st(1,ld(0)*ld(0)*(3-2*ld(0)));"
    z = f"{ease}1/({s0}+({s1}-{s0})*ld(1))"
    x = f"{ease}({x0}+({x1}-{x0})*ld(1))*iw-iw/zoom/2"
    y = f"{ease}({y0}+({y1}-{y0})*ld(1))*ih-ih/zoom/2"
    # drawn from a frame twice the size, so the slow move lands between pixels instead of stepping
    return f"scale=iw*2:ih*2:flags=bicubic,zoompan=z='{z}':x='{x}':y='{y}':d=1:s={W}x{H}:fps={FPS}"


def fades(clip) -> list[str]:
    out = []
    if clip.get("fadeIn"):
        out.append(f"fade=t=in:st=0:d={clip['fadeIn']}")
    if clip.get("fadeOut"):
        out.append(f"fade=t=out:st={clip['seconds'] - clip['fadeOut']:.3f}:d={clip['fadeOut']}")
    return out


def build_plain(clip, out: Path) -> None:
    src = tl.CAPTURE / f"{clip['src']}.mp4"
    chain = []
    if clip.get("speed", 1.0) != 1.0:
        chain.append(f"setpts=PTS/{clip['speed']}")
    chain += [f"fps={FPS}", view_filter(clip)]
    if clip.get("hold"):
        chain.append(f"tpad=stop_mode=clone:stop_duration={clip['hold'] + 0.5}")
    chain += fades(clip)
    head = ["-ss", f"{clip['in']:.3f}", "-t", f"{clip['out'] - clip['in'] + 0.2:.3f}", "-i", str(src)]
    card = clip.get("card")
    if card:
        # the end card's ground: the same picture, softened and darkened, faded in over the shot
        graph = (f"[0:v]{','.join(chain)},split[a][b];"
                 f"[b]gblur=sigma=16,eq=brightness=-0.22:saturation=0.8,format=yuva420p,fade=t=in:st={card['at']}:d={card.get('fade', 1.4)}:alpha=1[soft];"
                 f"[a][soft]overlay=format=auto,format=yuv420p[v]")
        run([*head, "-filter_complex", graph, "-map", "[v]", "-frames:v", str(clip["frames"]), *ENCODE, str(out)])
    else:
        run([*head, "-vf", ",".join(chain + ["format=yuv420p"]), "-frames:v", str(clip["frames"]), *ENCODE, str(out)])


def build_keys(clip, out: Path) -> None:
    """The typing take with the waits between key presses cut out: each press at its real speed."""
    src = tl.CAPTURE / f"{clip['src']}.mp4"
    half = 0.5 / FPS
    picks = "+".join(f"between(t,{a - half:.4f},{b - half - 0.0005:.4f})" for a, b in clip["windows"])
    chain = [f"select='{picks}'", f"setpts=N/({FPS}*TB)", view_filter(clip), *fades(clip), "format=yuv420p"]
    last = clip["windows"][-1][1]
    run(["-t", f"{last + 1:.3f}", "-i", str(src), "-vf", ",".join(chain), "-frames:v", str(clip["frames"]), *ENCODE, str(out)])


def phone_art() -> tuple[Path, Path]:
    """The ground the phone stands on, and the phone's body with a hole where its screen shows."""
    EDIT.mkdir(parents=True, exist_ok=True)
    ground, body = EDIT / "phone-ground.png", EDIT / "phone-body.png"
    p = PHONE
    k = 3   # drawn large and scaled down, for smooth edges
    # the lounge itself, far out of focus and dimmed, so the insert stays in the film's light
    still = EDIT / "phone-still.png"
    run(["-ss", "2.0", "-i", str(tl.CAPTURE / "s02-away.mp4"), "-frames:v", "1", "-vf", f"scale={W}:{H}:flags=lanczos", str(still)])
    soft = np.asarray(Image.open(still).convert("RGB").filter(ImageFilter.GaussianBlur(70)), dtype=np.float32)
    grey = soft.mean(axis=-1, keepdims=True)
    soft = (grey + (soft - grey) * 0.8) * 0.5
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rim = 1 - 0.3 * np.clip((((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) * 0.6, 0, 1)
    img = Image.fromarray(np.clip(soft * rim[..., None], 0, 255).astype(np.uint8), "RGB")
    outer = (p["x"] - p["bezel"], p["y"] - p["bezel"], p["x"] + p["w"] + p["bezel"], p["y"] + p["h"] + p["bezel"])
    shadow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shadow).rounded_rectangle((outer[0] + 4, outer[1] + 22, outer[2] + 4, outer[3] + 22), p["radius"] + p["bezel"], fill=150)
    img.paste(Image.new("RGB", (W, H), (6, 8, 7)), (0, 0), shadow.filter(ImageFilter.GaussianBlur(26)))
    img.save(ground)

    big = Image.new("RGBA", (W * k, H * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    d.rounded_rectangle(tuple(v * k for v in outer), (p["radius"] + p["bezel"]) * k, fill=(74, 78, 76, 255))
    inset = 2
    d.rounded_rectangle(tuple((v + (inset if i < 2 else -inset)) * k for i, v in enumerate(outer)), (p["radius"] + p["bezel"] - inset) * k, fill=(17, 18, 19, 255))
    d.rounded_rectangle((p["x"] * k, p["y"] * k, (p["x"] + p["w"]) * k, (p["y"] + p["h"]) * k), p["radius"] * k, fill=(0, 0, 0, 0))
    big.resize((W, H), Image.LANCZOS).save(body)
    return ground, body


def build_phone(clip, out: Path) -> None:
    folder = tl.CAPTURE / clip["phone"]
    ground, body = phone_art()
    p = PHONE
    speed = clip.get("speed", 1.0)
    screen = [f"fps={FPS}", f"trim=start={clip['in']}:end={clip['out'] + 0.5}", f"setpts=(PTS-STARTPTS)/{speed}", f"fps={FPS}",
              f"scale={p['w']}:{p['h']}:flags=lanczos", "setsar=1"]
    graph = (f"[0:v]{','.join(screen)}[s];[1:v]fps={FPS}[g];[g][s]overlay={p['x']}:{p['y']}:shortest=1[a];"
             f"[a][2:v]overlay=0:0,{','.join(fades(clip) + ['format=yuv420p'])}[v]")
    run(["-f", "concat", "-safe", "0", "-i", "frames.txt", "-loop", "1", "-i", str(ground), "-loop", "1", "-i", str(body),
         "-filter_complex", graph, "-map", "[v]", "-frames:v", str(clip["frames"]), *ENCODE, str(out)], cwd=folder)


def build_parts(clips, only, keep) -> None:
    PARTS.mkdir(parents=True, exist_ok=True)
    for c in clips:
        out = PARTS / f"{c['id']}.mp4"
        if out.exists() and (keep or (only and c["id"] not in only)):
            continue
        print(f"  {c['id']:10s} {c['seconds']:6.2f} s")
        if "phone" in c:
            build_phone(c, out)
        elif "keys" in c:
            build_keys(c, out)
        else:
            build_plain(c, out)


# ------------------------------------------------------------------------------------------- the film's own text

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


def spoken(clips):
    """Everything said in the film, in order: (start, end, kind, text, clip id). kind: narration or recorded."""
    out = []
    pre = CUT.get("prelap")
    if pre:
        t = tl.at(clips, pre["clip"], pre["at"])
        out.append((t + 0.05, t + (pre["out"] - pre["in"]) + 0.55, "recorded", pre["text"], pre["clip"]))
    for n in CUT["narration"]:
        info = json.loads((tl.DEMO / "voice" / f"{n['line']}.json").read_text(encoding="utf-8-sig"))
        t = tl.at(clips, n["clip"], n["at"])
        out.append((t + 0.1, t + float(info["duration"]) + 0.3, "narration", n["text"], n["clip"]))
    note = CUT["voiceNote"]
    t = tl.at(clips, note["clip"], note["at"])
    for part in note["parts"]:
        out.append((t + part["from"], t + part["to"], "recorded", part["text"], note["clip"]))
    out.sort(key=lambda row: row[0])
    for i in range(len(out) - 1):   # never two at once
        if out[i][1] > out[i + 1][0] - 0.04:
            out[i] = (out[i][0], out[i + 1][0] - 0.04, *out[i][2:])
    return out


def write_srt(path: Path, clips) -> None:
    rows = []
    for i, (start, end, kind, text, _) in enumerate(spoken(clips), 1):
        text = f"<i>{text}</i>" if kind == "narration" else f"[Maya, recorded message] {text}"
        rows.append(f"{i}\n{stamp(start, ',')} --> {stamp(end, ',')}\n{text}\n")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(rows), encoding="utf-8")


def write_ass(path: Path, clips, total: float, subtitles: bool) -> None:
    head = f"""[Script Info]
ScriptType: v4.00+
PlayResX: {W}
PlayResY: {H}
WrapStyle: 0
ScaledBorderAndShadow: yes
YCbCr Matrix: TV.709

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Sub,Figtree SemiBold,42,&H00F8FAFB,&H00FFFFFF,&H5A14181C,&HFF000000,0,0,0,0,100,100,0.2,0,3,8,0,2,230,230,60,1
Style: Tag,Figtree SemiBold,22,&H00C2E4F6,&H00FFFFFF,&H5A14181C,&HFF000000,0,0,0,0,100,100,1.4,0,3,6,0,2,230,230,126,1
Style: Caption,Figtree SemiBold,62,&H00F4F8FB,&H00FFFFFF,&H2814181C,&H50000000,0,0,0,0,100,100,0.3,0,1,3,2.2,1,118,118,92,1
Style: Question,Figtree SemiBold,60,&H00F6F8FA,&H00FFFFFF,&H3C14181C,&H50000000,0,0,0,0,100,100,0.4,0,1,2.6,2,5,0,0,0,1
Style: Note,Figtree SemiBold,26,&H00D6ECF8,&H00FFFFFF,&H4614181C,&HFF000000,0,0,0,0,100,100,0.5,0,3,9,0,7,52,52,66,1
Style: Stand,Figtree SemiBold,21,&H1EFFFFFF,&H00FFFFFF,&H4614181C,&H78000000,0,0,0,0,100,100,0.6,0,1,1.8,0.8,7,44,44,22,1
Style: Step,Figtree SemiBold,50,&H00F4F8FB,&H00FFFFFF,&H5014181C,&H50000000,0,0,0,0,100,100,0.3,0,1,1.5,2,7,0,0,0,1
Style: Fine,Figtree,28,&H14E8F0F4,&H00FFFFFF,&H5014181C,&H64000000,0,0,0,0,100,100,0.4,0,1,1.2,1.2,7,0,0,0,1
Style: Title,Figtree Bold,156,&H00F2F6FA,&H00FFFFFF,&H50101418,&H64000000,0,0,0,0,100,100,1,0,1,0,3,5,0,0,0,1
Style: Small,Figtree SemiBold,54,&H0096CDF0,&H00FFFFFF,&H50101418,&H64000000,0,0,0,0,100,100,0.6,0,1,0,2,5,0,0,0,1
Style: Credit,Figtree,25,&H28DCE4EA,&H00FFFFFF,&H50101418,&H64000000,0,0,0,0,100,100,0.8,0,1,0,1,5,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    events = []

    def add(start, end, style, text, layer=0, left=0, right=0, vertical=0):
        events.append(f"Dialogue: {layer},{stamp(start, '.')},{stamp(end, '.')},{style},,{left},{right},{vertical},,{text}")

    phone = next((c for c in clips if "phone" in c), None)

    def beside_phone(clip_id):   # text during the phone insert keeps to the left of the phone
        return phone is not None and clip_id == phone["id"]

    if subtitles:
        for start, end, kind, text, clip_id in spoken(clips):
            side = "{\\an1}" if beside_phone(clip_id) else ""
            margins = dict(left=118, right=W - PHONE["x"] + 60) if beside_phone(clip_id) else {}
            if kind == "narration":
                add(start, end, "Sub", side + "{\\fad(160,200)\\i1}" + text, **margins)
            else:
                add(start, end, "Sub", "{\\fad(120,180)}" + text)
                add(start, end, "Tag", "{\\fad(120,180)}MAYA · RECORDED MESSAGE")
    for c in CUT["captions"]:
        a, b = tl.at(clips, c["clip"], c["from"]), tl.at(clips, c["clip"], c["to"])
        if c.get("style") == "Question":
            add(a, b, "Question", "{\\fad(700,600)\\pos(960,%d)}%s" % (c.get("y", 300), c["text"]), 1)
        else:   # bottom left, or bottom centre ("align": "center"), with its own distance from the edge if given
            side = "{\\an2}" if c.get("align") == "center" else ""
            add(a, b, "Caption", side + "{\\fad(450,450)}" + c["text"], 1, vertical=int(c.get("margin", 0)))
    for n in CUT["labels"]:
        a, b = tl.at(clips, n["clip"], n["from"]), tl.at(clips, n["clip"], n["to"])
        add(a, b, "Note", "{\\fad(300,300)}" + n["text"], 1)
    for s in CUT.get("steps", []):
        a, b = tl.at(clips, s["clip"], s["from"]), tl.at(clips, s["clip"], s["to"])
        add(a, b, s.get("style", "Step"), "{\\fad(350,300)\\pos(%d,%d)}%s" % (s["x"], s["y"], s["text"]), 1)
    # the standing label: where the footage comes from, on every shot of it
    card = CUT["endCard"]
    card_at = tl.at(clips, card["clip"], card["at"])
    runs = []
    for c in clips:
        if "phone" in c:
            continue
        a, b = c["start"], min(c["start"] + c["seconds"], card_at)
        if runs and abs(runs[-1][1] - a) < 1e-3:
            runs[-1][1] = b
        elif b > a:
            runs.append([a, b])
    for a, b in runs:
        add(a, b, "Stand", "{\\fad(300,300)}" + CUT["standing"], 0)
    add(card_at + card.get("titleAt", 0.5), total, "Title", "{\\fad(900,800)\\pos(960,452)}" + card["title"], 2)
    add(card_at + card.get("smallAt", 1.3), total, "Small", "{\\fad(800,800)\\pos(960,606)}" + card["small"], 2)
    add(card_at + card.get("creditAt", 2.0), total, "Credit", "{\\fad(700,800)\\pos(960,1044)}" + card["credit"], 2)
    path.write_text(head + "\n".join(events) + "\n", encoding="utf-8")


# ------------------------------------------------------------------------------------------- the film

def main() -> None:
    args = sys.argv[1:]
    clean = "--clean" in args
    keep = "--keep" in args
    only = set(args[args.index("--only") + 1].split(",")) if "--only" in args else set()
    clips, total = tl.timeline()
    missing = sorted({c["src"] for c in clips if "src" in c and not (tl.CAPTURE / f"{c['src']}.mp4").exists()})
    if missing:
        raise SystemExit("not captured yet (scripts/demo/film.ps1): " + ", ".join(missing))
    print(f"clips ({total:.2f} s):")
    build_parts(clips, only, keep)

    EDIT.mkdir(parents=True, exist_ok=True)
    fonts = EDIT / "fonts"
    fonts.mkdir(exist_ok=True)
    for f in FONTS.glob("*.ttf"):
        shutil.copy2(f, fonts / f.name)
    write_ass(EDIT / "demo.ass", clips, total, subtitles=not clean)
    write_srt(ROOT / "docs" / "demo" / f"{NAME}.srt", clips)
    (EDIT / "parts.txt").write_text("".join(f"file '../parts/{c['id']}.mp4'\n" for c in clips), encoding="utf-8")
    mix = tl.DEMO / "audio" / "mix.wav"
    if not mix.exists():
        raise SystemExit("no sound mix yet: run scripts/demo/demo_audio.py first")
    out = ROOT / "demo-build" / (NAME + ("_clean" if clean else "") + ".mp4")
    video = f"[0:v]fade=t=in:st=0:d=0.7,fade=t=out:st={total - 0.9:.2f}:d=0.9,ass=demo.ass:fontsdir=fonts,format=yuv420p[v]"
    print("final pass...")
    run(["-f", "concat", "-safe", "0", "-i", "parts.txt", "-i", "../audio/mix.wav", "-filter_complex", video, "-map", "[v]", "-map", "1:a",
         "-t", f"{total:.3f}", "-r", str(FPS), "-c:v", "libx264", "-preset", "slow", "-crf", "17", "-profile:v", "high", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-b:a", "256k", "-movflags", "+faststart",
         "-metadata", "title=I Saved You a Seat (SameSky demo)",
         "-metadata", "comment=Footage of the SameSky native Quest app in Meta XR Simulator, offline sample (fictional people, stock media, synthesized voice), with one insert of the companion web app in sample mode. Narration is a synthesized voice.",
         str(out)], cwd=EDIT)
    print(f"wrote {out.relative_to(ROOT)} ({out.stat().st_size / 1e6:.1f} MB, {total:.2f} s)")


if __name__ == "__main__":
    main()
