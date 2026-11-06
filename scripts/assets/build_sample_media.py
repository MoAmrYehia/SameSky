"""Build SameSky's photographic sample media from CC0 Poly Haven panoramas (see docs/ASSET_MANIFEST.md).

Each "photo" is a rectilinear camera view reprojected from an equirectangular panorama (yaw/pitch/FOV
below), lightly graded. The sample clip is an 8-second handheld-style pan through one panorama, encoded to
SameSky's playback profile (H.264/AAC MP4). The window plate is a cylindrical band for the room's window.

Run with a Python that has numpy + scipy + Pillow (e.g. Anaconda), after scripts/assets/fetch_polyhaven.py:
    python scripts/assets/build_sample_media.py
"""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy.ndimage import gaussian_filter, map_coordinates

Image.MAX_IMAGE_PIXELS = None
ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / ".asset-cache" / "hdri"
OUT = ROOT / "web" / "public" / "assets" / "sample"
OLD = ROOT / "web" / "public" / "sample"

_panos: dict[str, np.ndarray] = {}


def pano(name: str) -> np.ndarray:
    if name not in _panos:
        _panos[name] = np.asarray(Image.open(CACHE / f"{name}_tonemapped.jpg").convert("RGB"), dtype=np.float32) / 255.0
    return _panos[name]


def view(name, yaw, pitch, fov, w, h, roll=0.0):
    """Rectilinear crop: yaw/pitch in degrees (yaw 0 = panorama centre, + to the right)."""
    img = pano(name)
    H, W, _ = img.shape
    yaw, pitch, roll = np.radians([yaw, pitch, roll])
    f = 0.5 * w / np.tan(np.radians(fov) / 2)
    xs, ys = np.meshgrid(np.arange(w) - w / 2 + 0.5, np.arange(h) - h / 2 + 0.5)
    d = np.stack([xs, -ys, np.full_like(xs, f)], -1)
    d /= np.linalg.norm(d, axis=-1, keepdims=True)
    cr, sr = np.cos(roll), np.sin(roll)
    d = d @ np.array([[cr, -sr, 0], [sr, cr, 0], [0, 0, 1]]).T
    cp, sp = np.cos(pitch), np.sin(pitch)
    d = d @ np.array([[1, 0, 0], [0, cp, sp], [0, -sp, cp]]).T
    cy, sy = np.cos(yaw), np.sin(yaw)
    d = d @ np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]).T
    lon = np.arctan2(d[..., 0], d[..., 2])
    lat = np.arcsin(np.clip(d[..., 1], -1, 1))
    px = (lon / (2 * np.pi) + 0.5) * W - 0.5
    py = (0.5 - lat / np.pi) * H - 0.5
    out = np.empty((h, w, 3), np.float32)
    for c in range(3):
        out[..., c] = map_coordinates(img[..., c], [py, px], order=1, mode="wrap")
    return out


def grade(img, warmth=0.0, contrast=1.0, lift=0.0, vignette=0.18, saturation=1.0, exposure=1.0):
    out = img * exposure
    lum = out.mean(-1, keepdims=True)
    out = lum + (out - lum) * saturation
    out = (out - 0.5) * contrast + 0.5 + lift
    out = out * np.array([1 + warmth * 0.08, 1 + warmth * 0.02, 1 - warmth * 0.08], np.float32)
    h, w, _ = out.shape
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)
    out *= (1 - vignette * np.clip(r - 0.35, 0, 1) ** 1.6)[..., None]
    return np.clip(out, 0, 1)


def save(img, path: Path, quality=86):
    im = Image.fromarray((img * 255 + 0.5).astype(np.uint8))
    im.save(path, "JPEG", quality=quality, optimize=True, progressive=True)  # fresh file: no EXIF/GPS
    return im


def photo(name, out, yaw, pitch, fov, **g):
    img = grade(view(name, yaw, pitch, fov, 1600, 1000), **g)
    im = save(img, OUT / f"{out}.jpg")
    im.resize((640, 400), Image.LANCZOS).save(OUT / f"{out}-thumb.jpg", "JPEG", quality=84, optimize=True)
    print("photo", out)


def ffmpeg() -> str:
    found = shutil.which("ffmpeg")
    if found:
        return found
    try:
        import imageio_ffmpeg

        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:  # fall back to the binary bundled in the API's virtualenv
        candidates = sorted((ROOT / "api" / ".venv").glob("**/imageio_ffmpeg/binaries/ffmpeg*"))
        if not candidates:
            raise SystemExit("ffmpeg not found: install it or `pip install imageio-ffmpeg`")
        return str(candidates[0])


def clip():
    """8 s handheld-style pan along the promenade, encoded like a processed SameSky upload."""
    w, h, fps, seconds = 1280, 720, 30, 8
    frames = fps * seconds
    cmd = [ffmpeg(), "-y", "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{w}x{h}", "-r", str(fps), "-i", "-",
           "-f", "lavfi", "-i", f"anoisesrc=color=brown:amplitude=0.03:duration={seconds}", "-shortest",
           "-c:v", "libx264", "-profile:v", "high", "-pix_fmt", "yuv420p", "-crf", "22", "-preset", "slow",
           "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart", str(OUT / "adam-clip.mp4")]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    rng = np.random.default_rng(4)
    shake = np.cumsum(rng.normal(0, 0.05, (frames, 2)), 0)
    shake -= np.linspace(shake[0], shake[-1], frames)
    for i in range(frames):
        t = i / (frames - 1)
        ease = t * t * (3 - 2 * t)
        yaw = 98 + 52 * ease
        pitch = 1.5 + 0.5 * np.sin(i / 9) + shake[i, 0] * 0.6
        img = grade(view("golden_bay", yaw, pitch, 68, w, h, roll=shake[i, 1] * 0.5), warmth=0.25, contrast=1.04, vignette=0.12)
        frame = (img * 255 + 0.5).astype(np.uint8)
        proc.stdin.write(frame.tobytes())
        if i == frames // 3:
            Image.fromarray(frame).save(OUT / "adam-clip-poster.jpg", "JPEG", quality=86)
            Image.fromarray(frame).resize((640, 360), Image.LANCZOS).save(OUT / "adam-clip-thumb.jpg", "JPEG", quality=84)
    proc.stdin.close()
    proc.wait()
    print("clip adam-clip.mp4")


def window_plate():
    """Cylindrical band (200 degrees wide) for the room's curved window, graded toward sunset."""
    img = pano("dusseldorf_bridge")
    H, W, _ = img.shape
    w, h = 2048, 512  # within the 2K texture budget; the distant view is meant to read soft
    lon = np.radians(np.linspace(-60, 140, w))
    tan_lat = np.linspace(np.tan(np.radians(30)), np.tan(np.radians(-14)), h)
    lat = np.arctan(tan_lat)  # cylindrical projection: straight verticals, correct heights
    LON, LAT = np.meshgrid(lon, lat)
    px = (LON / (2 * np.pi) + 0.5) * W - 0.5
    py = (0.5 - LAT / np.pi) * H - 0.5
    out = np.stack([map_coordinates(img[..., c], [py, px], order=1, mode="wrap") for c in range(3)], -1)
    # sunset grade: warm low sky and water, deeper top, lights a touch brighter
    y = np.linspace(0, 1, h)[:, None, None]
    sky_tint = np.concatenate([np.full((h, 1, 1), 1.0), np.full((h, 1, 1), 1.0), np.full((h, 1, 1), 1.0)], -1)
    warm = np.array([1.18, 0.9, 0.72], np.float32)
    cool = np.array([0.86, 0.82, 1.02], np.float32)
    mixk = np.clip((y - 0.15) / 0.5, 0, 1)
    tint = cool * (1 - mixk) + warm * mixk
    out = out * tint * sky_tint
    out = grade(out, contrast=1.06, saturation=1.1, vignette=0.0, exposure=1.02)
    # a touch of softness: the skyline sits ~9 m away, beyond the room's focus
    out = np.stack([gaussian_filter(out[..., c], 0.7) for c in range(3)], -1)
    save(out, OUT / "window-skyline.jpg", quality=86)
    print("window plate")


def backdrops():
    img = view("lythwood_lounge", -150, 2, 80, 1920, 1080)
    img = grade(img, warmth=0.4, contrast=0.95, vignette=0.0, exposure=0.95)
    im = Image.fromarray((img * 255 + 0.5).astype(np.uint8)).filter(ImageFilter.GaussianBlur(28))
    im.save(OUT.parent / "backdrops" / "lounge.jpg", "JPEG", quality=78, optimize=True)
    dark = Image.eval(im, lambda v: int(v * 0.42))
    dark.save(OUT.parent / "backdrops" / "lounge-dark.jpg", "JPEG", quality=78, optimize=True)
    print("backdrops")


def main():
    import sys

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT.parent / "backdrops").mkdir(parents=True, exist_ok=True)
    if "--skip-photos" in sys.argv:
        clip()
        window_plate()
        backdrops()
        return
    photo("ballawley_park", "maya-park", yaw=18, pitch=4, fov=64, warmth=0.9, contrast=1.05, lift=-0.01, saturation=1.05)
    photo("golden_bay", "adam-waterfront", yaw=142, pitch=2, fov=62, warmth=0.2, contrast=1.04)
    photo("courtyard_night", "lina-courtyard", yaw=-92, pitch=9, fov=74, warmth=0.15, contrast=1.03)
    photo("rogland_sunset", "adam-desert", yaw=24, pitch=3, fov=62, warmth=0.8, contrast=1.06)
    clip()
    window_plate()
    backdrops()
    # the synthesized voice note and its captions move with the rest of the sample
    for f in ("maya-voice.m4a", "maya-voice.vtt"):
        if (OLD / f).exists():
            shutil.copy2(OLD / f, OUT / f)
    print("done")


if __name__ == "__main__":
    main()
