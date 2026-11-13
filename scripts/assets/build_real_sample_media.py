"""Build the sample day's photos and clip from real, freely licensed photographs and footage.

Sources: scripts/assets/sample_media.json, fetched by scripts/assets/fetch_sample_media.py into
.asset-cache/sample-media/<id>/ (git-ignored). This script only reads those files and writes the app's copies:

  photo  ->  web/public/assets/sample/<out>.jpg        1600x1000, cropped around the source's focus point
             web/public/assets/sample/<out>-thumb.jpg  640x400
  video  ->  <out>.mp4          the chosen seconds, 1280x720, 30 fps, H.264 High + AAC, +faststart
             <out>-poster.jpg   1280x720 (a frame of the clip)
             <out>-thumb.jpg    640x360

Nothing but pixels and sound is carried over: no EXIF, GPS position, camera serial or other metadata. Run it with
the API's virtualenv (Pillow and imageio-ffmpeg), in isolated mode because it reads downloaded files:

    api/.venv/Scripts/python.exe -I scripts/assets/build_real_sample_media.py

then copy to the headset app with scripts/native/build_native_assets.py.
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = Path(__file__).with_name("sample_media.json")
CACHE = ROOT / ".asset-cache" / "sample-media"
OUT = ROOT / "web" / "public" / "assets" / "sample"
Image.MAX_IMAGE_PIXELS = 200_000_000


def ffmpeg() -> str:
    found = shutil.which("ffmpeg")
    if found:
        return found
    try:
        import imageio_ffmpeg

        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        sys.exit("FFmpeg not found: install it, or use the API's virtualenv (imageio-ffmpeg)")


def crop_to(img: Image.Image, width: int, height: int, focus: tuple[float, float]) -> Image.Image:
    """The largest width:height window of the picture, placed so the focus point (0..1, 0..1) is as near its
    centre as the picture's edges allow, then resized."""
    w, h = img.size
    want = width / height
    if w / h > want:
        cw, ch = round(h * want), h
    else:
        cw, ch = w, round(w / want)
    left = min(max(round(focus[0] * w - cw / 2), 0), w - cw)
    top = min(max(round(focus[1] * h - ch / 2), 0), h - ch)
    return img.crop((left, top, left + cw, top + ch)).resize((width, height), Image.LANCZOS)


def save_jpeg(img: Image.Image, path: Path, quality: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, "JPEG", quality=quality, optimize=True, progressive=True)   # a fresh file: no metadata is copied


def photo(item: dict, source: Path) -> None:
    img = ImageOps.exif_transpose(Image.open(source)).convert("RGB")
    focus = tuple(item.get("focus", [0.5, 0.5]))
    full = crop_to(img, 1600, 1000, focus)
    save_jpeg(full, OUT / f"{item['out']}.jpg", 88)
    save_jpeg(full.resize((640, 400), Image.LANCZOS), OUT / f"{item['out']}-thumb.jpg", 84)
    print("photo", item["out"], "from", source.name, img.size)


def video(item: dict, source: Path) -> None:
    out = OUT / f"{item['out']}.mp4"
    start, seconds = float(item.get("start", 0)), float(item.get("seconds", 8))
    fit = "scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,fps=30,format=yuv420p"
    command = [ffmpeg(), "-y", "-loglevel", "error", "-ss", f"{start}", "-t", f"{seconds}", "-i", str(source)]
    if item.get("audio", "keep") == "keep":
        command += ["-map", "0:v:0", "-map", "0:a:0?"]
    else:
        # the footage's own sound isn't used: a silent track keeps the file the shape the players expect
        command += ["-f", "lavfi", "-t", f"{seconds}", "-i", "anullsrc=channel_layout=stereo:sample_rate=48000", "-map", "0:v:0", "-map", "1:a:0"]
    command += ["-vf", fit, "-c:v", "libx264", "-profile:v", "high", "-preset", "slow", "-crf", "22", "-c:a", "aac", "-b:a", "96k",
                "-map_metadata", "-1", "-movflags", "+faststart", "-shortest", str(out)]
    subprocess.run(command, check=True)
    poster = OUT / f"{item['out']}-poster.jpg"
    at = float(item.get("poster_at", min(1.0, seconds / 2)))
    subprocess.run([ffmpeg(), "-y", "-loglevel", "error", "-ss", f"{at}", "-i", str(out), "-frames:v", "1", "-q:v", "3", str(poster)], check=True)
    frame = Image.open(poster).convert("RGB")
    save_jpeg(frame, poster, 86)
    save_jpeg(frame.resize((640, 360), Image.LANCZOS), OUT / f"{item['out']}-thumb.jpg", 84)
    print("clip", item["out"], f"{seconds:g} s from {start:g} s of", source.name)


def main() -> None:
    items = json.loads(MANIFEST.read_text(encoding="utf-8"))["items"]
    missing = [i["id"] for i in items if not (CACHE / i["id"] / i["file"]).exists()]
    if missing:
        sys.exit("not fetched yet (scripts/assets/fetch_sample_media.py): " + ", ".join(missing))
    for item in items:
        source = CACHE / item["id"] / item["file"]
        (video if item["kind"] == "video" else photo)(item, source)
    print("done:", OUT)


if __name__ == "__main__":
    main()
