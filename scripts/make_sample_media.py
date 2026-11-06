"""Generate SameSky's original, fictional sample media (no real people, no stock assets).

Outputs into web/public/sample/:
  maya-park.jpg        Maya's phone photo: a quiet park path at golden hour
  adam-waterfront.jpg  Adam's phone photo: a waterfront promenade at sunset
  adam-ride.mp4        Adam's imported action-camera clip (H.264/AAC, 8 s) - same playback profile as real uploads
  adam-ride.jpg        poster frame
  lina-lanterns.jpg    Lina's family-circle photo: lanterns over a courtyard table
  adam-dunes.jpg       Adam's family-circle photo: dunes at dusk
  maya-voice.m4a       Maya's voice note, synthesized with the OS text-to-speech voice (clearly labelled as such)
  maya-voice.vtt       its captions

Run: api/.venv/Scripts/python scripts/make_sample_media.py
"""

from __future__ import annotations

import math
import random
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "web" / "public" / "sample"
W, H = 1280, 800
SS = 2  # supersampling factor


def ffmpeg() -> str:
    found = shutil.which("ffmpeg")
    if found:
        return found
    import imageio_ffmpeg

    return imageio_ffmpeg.get_ffmpeg_exe()


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def vgradient(size, stops):
    w, h = size
    img = Image.new("RGB", size)
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        for i in range(len(stops) - 1):
            (p0, c0), (p1, c1) = stops[i], stops[i + 1]
            if p0 <= t <= p1:
                col = lerp(c0, c1, (t - p0) / max(1e-6, p1 - p0))
                break
        for x in range(w):
            px[x, y] = col
    return img


def glow(size, center, radius, color, strength=1.0):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy = center
    for i in range(40, 0, -1):
        r = radius * i / 40
        a = int(255 * strength * (1 - i / 40) ** 2 * 0.55)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color + (a,))
    return layer.filter(ImageFilter.GaussianBlur(radius / 6))


def finish(img: Image.Image, name: str):
    img = img.resize((W, H), Image.LANCZOS)
    img.save(OUT / name, "JPEG", quality=88, optimize=True)
    print("wrote", name)


# ---------------------------------------------------------------------------


def tree_canopy(d: ImageDraw.ImageDraw, cx, cy, r, rng, palette):
    for _ in range(int(r * 0.9)):
        a = rng.uniform(0, math.tau)
        dist = rng.uniform(0, r) ** 0.85 * r ** 0.15
        x, y = cx + math.cos(a) * dist * 1.25, cy + math.sin(a) * dist * 0.8
        rr = rng.uniform(r * 0.08, r * 0.22)
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=rng.choice(palette))


def park():
    rng = random.Random(7)
    size = (W * SS, H * SS)
    img = vgradient(size, [(0, (255, 226, 170)), (0.45, (238, 214, 160)), (0.55, (150, 170, 90)), (1, (70, 96, 48))])
    img = Image.alpha_composite(img.convert("RGBA"), glow(size, (int(size[0] * 0.62), int(size[1] * 0.28)), 900, (255, 236, 180), 1.0))
    d = ImageDraw.Draw(img)
    # distant tree line
    for x in range(-100, size[0] + 100, 70):
        tree_canopy(d, x, size[1] * 0.42 + rng.uniform(-30, 30), rng.uniform(110, 180), rng, [(118, 140, 70, 255), (100, 125, 62, 255), (140, 156, 82, 255)])
    # lawn texture
    for _ in range(9000):
        x, y = rng.uniform(0, size[0]), rng.uniform(size[1] * 0.5, size[1])
        g = rng.randint(70, 140)
        d.line([x, y, x + rng.uniform(-3, 3), y - rng.uniform(6, 18)], fill=(g - 20, g + 30, 40, 255), width=2)
    # path with perspective
    vx, vy = size[0] * 0.52, size[1] * 0.5
    path = [(vx - 40, vy), (vx + 40, vy), (size[0] * 0.92, size[1]), (size[0] * 0.18, size[1])]
    d.polygon(path, fill=(206, 186, 156, 255))
    for i in range(60):
        t = (i / 60) ** 1.7
        y = vy + (size[1] - vy) * t
        half = 40 + (size[0] * 0.37) * t
        d.line([vx - half, y, vx + half * 1.08, y], fill=(190, 168, 138, 255), width=max(1, int(3 * t)))
    # dappled light on the path
    light = Image.new("RGBA", size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(light)
    for _ in range(140):
        t = rng.uniform(0.15, 1.0) ** 1.4
        y = vy + (size[1] - vy) * t
        x = vx + rng.uniform(-0.9, 0.9) * (40 + size[0] * 0.35 * t)
        r = 12 + 70 * t
        ld.ellipse([x - r * 1.6, y - r * 0.5, x + r * 1.6, y + r * 0.5], fill=(255, 236, 190, 70))
    img = Image.alpha_composite(img, light.filter(ImageFilter.GaussianBlur(10)))
    d = ImageDraw.Draw(img)
    # big foreground trees
    for cx, cy, r in [(size[0] * 0.08, size[1] * 0.18, 520), (size[0] * 0.92, size[1] * 0.12, 560), (size[0] * 0.3, size[1] * 0.25, 300), (size[0] * 0.76, size[1] * 0.27, 280)]:
        trunk_w = r * 0.12
        d.rectangle([cx - trunk_w / 2, cy, cx + trunk_w / 2, cy + r * 1.9], fill=(72, 56, 40, 255))
        tree_canopy(d, cx, cy, r, rng, [(60, 92, 44, 255), (78, 112, 52, 255), (96, 130, 60, 255), (120, 150, 70, 255), (52, 80, 40, 255)])
    # bench + lamp post
    bx, by = size[0] * 0.24, size[1] * 0.72
    d.rectangle([bx, by, bx + 300, by + 22], fill=(110, 78, 50, 255))
    d.rectangle([bx, by - 70, bx + 300, by - 52], fill=(110, 78, 50, 255))
    for lx in (bx + 20, bx + 270):
        d.rectangle([lx, by - 70, lx + 14, by + 90], fill=(40, 40, 40, 255))
    px_ = size[0] * 0.66
    d.rectangle([px_, size[1] * 0.4, px_ + 14, size[1] * 0.66], fill=(35, 35, 35, 255))
    d.ellipse([px_ - 18, size[1] * 0.38, px_ + 32, size[1] * 0.42], fill=(250, 230, 170, 255))
    # flowers
    for _ in range(500):
        x = rng.uniform(0, size[0] * 0.2) if rng.random() < 0.5 else rng.uniform(size[0] * 0.82, size[0])
        y = rng.uniform(size[1] * 0.62, size[1])
        r = rng.uniform(4, 9)
        d.ellipse([x - r, y - r, x + r, y + r], fill=rng.choice([(244, 240, 250, 255), (220, 200, 240, 255), (250, 220, 120, 255)]))
    finish(img.convert("RGB"), "maya-park.jpg")


def skyline(d, base_y, scale, color, rng, sx=1.0):
    x = 0
    while x < W * SS * sx:
        w = rng.uniform(40, 120) * scale
        h = rng.uniform(80, 360) * scale
        d.rectangle([x, base_y - h, x + w, base_y], fill=color)
        if rng.random() < 0.3:
            d.rectangle([x + w * 0.4, base_y - h - 40 * scale, x + w * 0.55, base_y - h], fill=color)
        x += w + rng.uniform(2, 18) * scale


def tower(d, cx, base_y, height, color):
    # A generic stepped supertall (not a replica of any real building).
    tiers = 9
    for i in range(tiers):
        t = i / tiers
        w = 120 * (1 - t) + 18
        y0 = base_y - height * (i + 1) / tiers
        y1 = base_y - height * i / tiers
        d.rectangle([cx - w / 2, y0, cx + w / 2, y1], fill=color)
    d.rectangle([cx - 5, base_y - height - 260, cx + 5, base_y - height], fill=color)


def palm(d, x, base_y, h, color, rng):
    pts = []
    for i in range(20):
        t = i / 19
        pts.append((x + math.sin(t * 2.2) * 40 * t, base_y - h * t))
    d.line(pts, fill=color, width=18)
    tx, ty = pts[-1]
    for k in range(9):
        a = -math.pi / 2 + (k - 4) * 0.42
        length = rng.uniform(150, 230)
        frond = [(tx, ty)]
        for j in range(1, 8):
            s = j / 7
            frond.append((tx + math.cos(a) * length * s, ty + math.sin(a) * length * s + (s ** 2) * 90))
        d.line(frond, fill=color, width=12)


def sunset_scene(size, sun_y_frac, shimmer_phase, rng_seed=3):
    rng = random.Random(rng_seed)
    w, h = size
    horizon = int(h * 0.58)
    sky = vgradient((w, horizon), [(0, (96, 92, 150)), (0.45, (238, 150, 120)), (0.8, (252, 190, 110)), (1, (255, 214, 140))])
    water = vgradient((w, h - horizon), [(0, (230, 160, 110)), (0.5, (120, 96, 120)), (1, (52, 50, 80))])
    img = Image.new("RGBA", size)
    img.paste(sky, (0, 0))
    img.paste(water, (0, horizon))
    sun = (int(w * 0.56), int(h * sun_y_frac))
    img = Image.alpha_composite(img, glow(size, sun, 520, (255, 220, 150), 1.1))
    d = ImageDraw.Draw(img)
    if sun[1] < horizon:
        d.ellipse([sun[0] - 48, sun[1] - 48, sun[0] + 48, sun[1] + 48], fill=(255, 238, 200, 255))
    far = Image.new("RGBA", size, (0, 0, 0, 0))
    fd = ImageDraw.Draw(far)
    skyline(fd, horizon, 0.7, (150, 104, 120, 235), rng)
    tower(fd, w * 0.43, horizon, h * 0.42, (120, 82, 104, 245))
    skyline(fd, horizon, 0.45, (110, 76, 96, 250), random.Random(9))
    img = Image.alpha_composite(img, far.filter(ImageFilter.GaussianBlur(1.5)))
    # reflections + shimmer
    refl = Image.new("RGBA", size, (0, 0, 0, 0))
    rd = ImageDraw.Draw(refl)
    srng = random.Random(11)
    for i in range(260):
        y = horizon + 6 + (h - horizon) * (srng.random() ** 1.6)
        spread = 40 + (y - horizon) * 0.5
        x = sun[0] + srng.uniform(-spread, spread)
        length = srng.uniform(20, 120)
        alpha = int(200 * (1 - (y - horizon) / (h - horizon)) * (0.6 + 0.4 * math.sin(shimmer_phase * 2 + i)))
        rd.line([x, y, x + length, y], fill=(255, 226, 170, max(0, alpha)), width=3)
    img = Image.alpha_composite(img, refl.filter(ImageFilter.GaussianBlur(1)))
    d = ImageDraw.Draw(img)
    # promenade + railing in the foreground
    d.polygon([(w * 0.45, h), (w, h * 0.68), (w, h)], fill=(120, 96, 92, 255))
    for i in range(14):
        t = i / 13
        x = w * 0.5 + (w * 0.5) * t
        y = h * (0.98 - 0.26 * t)
        d.line([x, y, x, y - 110 * (1 - t * 0.6)], fill=(40, 34, 40, 255), width=int(10 - 6 * t))
    d.line([(w * 0.5, h * 0.98 - 110), (w, h * 0.72 - 44)], fill=(40, 34, 40, 255), width=10)
    prng = random.Random(5)
    palm(d, w * 0.9, h * 0.75, h * 0.55, (34, 30, 38, 255), prng)
    palm(d, w * 0.78, h * 0.78, h * 0.42, (40, 34, 42, 255), prng)
    return img


def waterfront():
    finish(sunset_scene((W * SS, H * SS), 0.45, 0.0).convert("RGB"), "adam-waterfront.jpg")


def ride_clip():
    """8-second "action camera" clip: slow pan + setting sun + water shimmer, encoded like a real upload."""
    fw, fh, fps, seconds = 960, 540, 24, 8
    frames = fps * seconds
    base = sunset_scene((int(fw * 1.25 * 1.5), int(fh * 1.25 * 1.5)), 0.42, 0.0)
    cmd = [ffmpeg(), "-y", "-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{fw}x{fh}", "-r", str(fps), "-i", "-",
           "-f", "lavfi", "-i", f"anoisesrc=color=brown:amplitude=0.04:duration={seconds}",
           "-shortest", "-c:v", "libx264", "-profile:v", "high", "-pix_fmt", "yuv420p", "-crf", "23", "-c:a", "aac", "-b:a", "96k",
           "-movflags", "+faststart", str(OUT / "adam-ride.mp4")]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for i in range(frames):
        t = i / frames
        if i % 6 == 0:
            scene = sunset_scene((int(fw * 1.25 * 1.5), int(fh * 1.25 * 1.5)), 0.42 + 0.08 * t, i * 0.25)
        else:
            scene = scene if i else base
        bw, bh = scene.size
        cw, ch = int(bw / 1.25), int(bh / 1.25)
        x0 = int((bw - cw) * t)
        y0 = int((bh - ch) * (0.5 + 0.03 * math.sin(i / 5)))  # gentle bob, like a handheld/cycling camera
        frame = scene.crop((x0, y0, x0 + cw, y0 + ch)).resize((fw, fh), Image.BILINEAR).convert("RGB")
        proc.stdin.write(frame.tobytes())
        if i == frames // 3:
            frame.resize((640, 360)).save(OUT / "adam-ride.jpg", "JPEG", quality=85)
    proc.stdin.close()
    proc.wait()
    print("wrote adam-ride.mp4")


def lanterns():
    rng = random.Random(21)
    size = (W * SS, H * SS)
    img = vgradient(size, [(0, (34, 30, 58)), (0.55, (70, 52, 70)), (1, (120, 84, 60))]).convert("RGBA")
    d = ImageDraw.Draw(img)
    # courtyard walls
    d.rectangle([0, size[1] * 0.35, size[0], size[1]], fill=(150, 112, 82, 255))
    for x in range(0, size[0], 260):
        d.rounded_rectangle([x + 40, size[1] * 0.42, x + 200, size[1] * 0.75], radius=80, fill=(96, 70, 56, 255))
    # table
    d.rectangle([size[0] * 0.15, size[1] * 0.78, size[0] * 0.85, size[1] * 0.84], fill=(110, 70, 44, 255))
    for x in (0.22, 0.4, 0.6, 0.78):
        d.ellipse([size[0] * x - 70, size[1] * 0.74, size[0] * x + 70, size[1] * 0.8], fill=(236, 226, 210, 255))
    # string of lanterns
    for k in range(3):
        y0 = size[1] * (0.12 + 0.08 * k)
        pts = [(x, y0 + 60 * math.sin(x / size[0] * math.pi)) for x in range(0, size[0], 40)]
        d.line(pts, fill=(30, 26, 30, 255), width=4)
        for x, y in pts[2::3]:
            col = rng.choice([(255, 196, 110), (255, 160, 90), (255, 220, 150)])
            img = Image.alpha_composite(img, glow(size, (int(x), int(y + 50)), 120, col, 0.9))
            d = ImageDraw.Draw(img)
            d.ellipse([x - 26, y + 20, x + 26, y + 84], fill=col + (255,))
    finish(img.convert("RGB"), "lina-lanterns.jpg")


def dunes():
    size = (W * SS, H * SS)
    img = vgradient(size, [(0, (70, 80, 140)), (0.4, (220, 150, 130)), (0.55, (250, 200, 150)), (1, (200, 130, 80))]).convert("RGBA")
    img = Image.alpha_composite(img, glow(size, (int(size[0] * 0.3), int(size[1] * 0.5)), 600, (255, 210, 160), 0.8))
    d = ImageDraw.Draw(img)
    for k, (base, amp, col) in enumerate([(0.55, 60, (214, 150, 96)), (0.65, 90, (196, 128, 80)), (0.78, 120, (170, 104, 64)), (0.9, 80, (140, 84, 52))]):
        pts = [(0, size[1])]
        for x in range(0, size[0] + 20, 20):
            y = size[1] * base - amp * math.sin(x / size[0] * math.pi * (1.4 + k * 0.5) + k)
            pts.append((x, y))
        pts.append((size[0], size[1]))
        d.polygon(pts, fill=col + (255,))
    finish(img.convert("RGB"), "adam-dunes.jpg")


VOICE_TEXT = "Saved you a seat. The bench by the big oak, same as always. Tell me about your sunset when you get home."


def voice():
    wav = OUT / "maya-voice.wav"
    ps = (
        "Add-Type -AssemblyName System.Speech; "
        "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; "
        "$v = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Gender -eq 'Female' } | Select-Object -First 1; "
        "if ($v) { $s.SelectVoice($v.VoiceInfo.Name) }; $s.Rate = -1; "
        f"$s.SetOutputToWaveFile('{wav}'); $s.Speak('{VOICE_TEXT}'); $s.Dispose()"
    )
    ok = False
    if sys.platform == "win32":
        ok = subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True).returncode == 0 and wav.exists()
    if not ok:
        # Fallback: a soft two-tone chime so the sample still has a playable voice-note object.
        subprocess.run([ffmpeg(), "-y", "-loglevel", "error", "-f", "lavfi", "-i", "sine=frequency=523:duration=1", "-f", "lavfi", "-i", "sine=frequency=659:duration=1",
                        "-filter_complex", "[0][1]concat=n=2:v=0:a=1", str(wav)], check=True)
    subprocess.run([ffmpeg(), "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "aac", "-b:a", "96k", "-ac", "1", str(OUT / "maya-voice.m4a")], check=True)
    dur = float(subprocess.run([ffmpeg(), "-i", str(OUT / "maya-voice.m4a")], capture_output=True, text=True).stderr.split("Duration: ")[1].split(",")[0].split(":")[2])
    wav.unlink(missing_ok=True)
    parts = ["Saved you a seat.", "The bench by the big oak, same as always.", "Tell me about your sunset when you get home."]
    total = sum(len(p) for p in parts)
    cues, t = [], 0.0
    for p in parts:
        span = dur * len(p) / total
        cues.append((t, t + span, p))
        t += span
    fmt = lambda s: f"00:{int(s // 60):02d}:{s % 60:06.3f}"
    (OUT / "maya-voice.vtt").write_text("WEBVTT\n\n" + "\n\n".join(f"{fmt(a)} --> {fmt(b)}\nMaya: {txt}" for a, b, txt in cues) + "\n", encoding="utf-8")
    print(f"wrote maya-voice.m4a ({dur:.1f}s, synthesized={ok})")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    park()
    waterfront()
    lanterns()
    dunes()
    ride_clip()
    voice()
