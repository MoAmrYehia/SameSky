"""Dress Adam and Maya for the animated trailer "I Saved You a Seat".

The trailer's two characters are the app's seated adults (Microsoft Rocketbox avatars, MIT licence). This script
recolours copies of their own textures so they match the film's character designs, and writes the copies to
demo-build/film/look, where the film's renderer (native/quest/Assets/SameSky/Editor/Film/FilmLook.cs) picks them up.
The app's own assets are not touched.

    Maya: warm olive skin, dark brown hair, brown eyes, a muted terracotta cardigan over a cream top
    Adam: warm brown skin, darker hair, light stubble, a navy knit over slate, stone-coloured relaxed trousers

Run:  python scripts/demo/trailer_look.py        (needs numpy, scipy and Pillow)
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "native" / "quest" / "Assets" / "SameSky" / "Art" / "Characters" / "Rocketbox"
OUT = ROOT / "demo-build" / "film" / "look"


def load(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32) / 255.0


def save(path: Path, rgba: np.ndarray, alpha: bool = False) -> None:
    out = np.clip(rgba * 255.0 + 0.5, 0, 255).astype(np.uint8)
    Image.fromarray(out if alpha else out[..., :3], "RGBA" if alpha else "RGB").save(path, optimize=True)
    print("wrote", path.relative_to(ROOT))


def lum(rgb: np.ndarray) -> np.ndarray:
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def hsv(rgb: np.ndarray):
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    d = mx - mn
    sat = np.where(mx > 1e-5, d / np.maximum(mx, 1e-5), 0.0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.zeros_like(mx)
    m = d > 1e-5
    rc = np.where(m & (mx == r), ((g - b) / np.maximum(d, 1e-5)) % 6.0, 0.0)
    gc = np.where(m & (mx == g) & (mx != r), (b - r) / np.maximum(d, 1e-5) + 2.0, 0.0)
    bc = np.where(m & (mx == b) & (mx != r) & (mx != g), (r - g) / np.maximum(d, 1e-5) + 4.0, 0.0)
    hue = (rc + gc + bc) * 60.0
    return hue, sat, mx


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def mix(a, b, k):
    return a + (b - a) * k[..., None]


def ramp(l, lo, hi, gamma=1.0):
    """A colour for every brightness: lo at black, hi at white."""
    k = np.clip(l, 0.0, 1.0) ** gamma
    return np.asarray(lo, np.float32) + (np.asarray(hi, np.float32) - np.asarray(lo, np.float32)) * k[..., None]


def box(shape, x0, y0, x1, y1, feather=6.0):
    """A soft-edged rectangle mask, coordinates as fractions of the texture."""
    h, w = shape[:2]
    m = np.zeros((h, w), np.float32)
    m[int(y0 * h):int(y1 * h), int(x0 * w):int(x1 * w)] = 1.0
    return gaussian_filter(m, feather) if feather > 0 else m


def ellipse(shape, cx, cy, rx, ry, soft=0.25):
    h, w = shape[:2]
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((x / w - cx) / rx) ** 2 + ((y / h - cy) / ry) ** 2)
    return 1.0 - smooth(d, 1.0 - soft, 1.0)


# ---------------------------------------------------------------------------- heads

def hair_mask(rgb: np.ndarray, dark: float, sigma: float) -> np.ndarray:
    """Where the head texture is hair: places where most of the neighbourhood is dark (strands and their highlights
    together), away from the face. Brows and lashes are thin, so they stay with the skin."""
    l = lum(rgb)
    d = gaussian_filter((l < dark).astype(np.float32), sigma)
    # never the face itself (eye sockets, lips and the shade under the chin are dark too), nor the neck and chest
    face = np.maximum(ellipse(rgb.shape, 0.5, 0.335, 0.215, 0.205, 0.12), box(rgb.shape, 0.0, 0.455, 1.0, 0.8, feather=rgb.shape[0] / 170.0))
    return smooth(d, 0.42, 0.62) * (1.0 - np.clip(face, 0.0, 1.0))


def face_parts(shape):
    """The patches of the head texture that are not skin: the inside of the mouth and the eyeball (bottom left)."""
    return np.clip(np.maximum(box(shape, 0.0, 0.615, 0.135, 1.0, feather=3.0), box(shape, 0.1, 0.79, 0.37, 1.0, feather=3.0)), 0.0, 1.0)


def recolour_iris(rgb: np.ndarray, lo, hi) -> np.ndarray:
    """The eyeball patch: find the pupil, repaint the iris around it in brown, keep the pupil and the highlight."""
    h, w = rgb.shape[:2]
    y0, y1, x0, x1 = int(0.83 * h), int(0.995 * h), int(0.2 * w), int(0.36 * w)
    patch = rgb[y0:y1, x0:x1]
    l = lum(patch)
    # the white of the eye is the brightest large area; the iris sits at its middle
    white = gaussian_filter((l > 0.6).astype(np.float32), 6.0)
    ys, xs = np.nonzero(white > 0.5)
    if len(xs) == 0:
        return rgb
    cx, cy = xs.mean(), ys.mean()
    yy, xx = np.mgrid[0:patch.shape[0], 0:patch.shape[1]].astype(np.float32)
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    # the iris: the darker disc at the centre of the white
    dark = gaussian_filter((l < 0.5).astype(np.float32), 2.0)
    radius = 0.0
    for rr in range(8, patch.shape[1] // 2):
        ring = (r > rr - 1.5) & (r < rr + 1.5)
        if ring.any() and dark[ring].mean() < 0.35:
            radius = float(rr)
            break
    if radius <= 0:
        return rgb
    iris = 1.0 - smooth(r, radius * 0.86, radius * 1.02)
    pupil = 1.0 - smooth(r, radius * 0.30, radius * 0.42)
    radial = np.clip(r / radius, 0.0, 1.0)
    detail = l / max(float(l[(r < radius) & (r > radius * 0.45)].mean()), 1e-3)
    colour = ramp(np.clip(0.25 + 0.55 * radial, 0, 1) * np.clip(detail, 0.6, 1.5), lo, hi, 1.0)
    colour = mix(colour, np.zeros_like(colour) + 0.02, pupil)
    highlight = smooth(l, 0.82, 0.95)
    colour = mix(colour, patch, highlight)
    out = rgb.copy()
    out[y0:y1, x0:x1] = mix(patch, colour, iris)
    print(f"  iris at ({x0 + cx:.0f}, {y0 + cy:.0f}) radius {radius:.0f}")
    return out


def head(person: str, source: str, skin_tint, skin_gamma, hair_lo, hair_hi, hair_dark, iris=None, stubble=0.0) -> None:
    rgba = load(SRC / source)
    rgb = rgba[..., :3]
    hair = hair_mask(rgb, hair_dark, rgb.shape[0] / 150.0)
    parts = face_parts(rgb.shape)
    l = lum(rgb)
    # skin: the same face, a different complexion
    skin = np.clip(rgb, 0, 1) ** skin_gamma * np.asarray(skin_tint, np.float32)
    # lips keep a little more of their own colour
    hue, sat, val = hsv(rgb)
    lips = smooth(sat, 0.42, 0.6) * (1.0 - hair) * ellipse(rgb.shape, 0.5, 0.405, 0.09, 0.035, 0.5)
    skin = mix(skin, np.clip(rgb, 0, 1) ** skin_gamma * (np.asarray(skin_tint, np.float32) * 0.5 + 0.5 * np.asarray([0.86, 0.66, 0.62], np.float32)), lips)
    if stubble > 0:
        beard = np.clip(ellipse(rgb.shape, 0.5, 0.44, 0.2, 0.105, 0.55) - ellipse(rgb.shape, 0.5, 0.405, 0.07, 0.028, 0.5), 0, 1)
        rng = np.random.default_rng(7)
        speck = gaussian_filter(rng.random(rgb.shape[:2]).astype(np.float32), 0.8)
        speck = smooth(speck, 0.47, 0.56)
        shade = beard * stubble * (0.55 + 0.45 * speck)
        skin = skin * (1.0 - shade[..., None] * np.asarray([0.62, 0.6, 0.56], np.float32))
    locks = ramp(smooth(l, 0.02, 0.75), hair_lo, hair_hi, 1.1)
    out = mix(skin, locks, hair)
    out = mix(out, rgb, parts)
    if iris is not None:
        out = recolour_iris(out, *iris)
    rgba = rgba.copy()
    rgba[..., :3] = out
    # how glossy each part is (the renderer reads it from the alpha channel): skin a soft sheen, lips a little more,
    # the painted hair matte, the eyeball and the inside of the mouth wet
    eye = box(rgb.shape, 0.19, 0.83, 0.37, 1.0, feather=2.0)
    gloss = 0.4 + 0.22 * lips
    gloss = gloss * (1.0 - hair) + 0.1 * hair
    gloss = gloss * (1.0 - parts) + 0.6 * parts
    gloss = gloss * (1.0 - eye) + 1.0 * eye
    rgba[..., 3] = np.clip(gloss, 0.0, 1.0)
    save(OUT / f"{person}_head.png", rgba, alpha=True)
    glow = out * ((1.0 - hair) * (1.0 - parts))[..., None]
    small = Image.fromarray(np.clip(glow * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGB").resize((1024, 1024), Image.LANCZOS)
    small.save(OUT / f"{person}_head_glow.png", optimize=True)


def hair_cards(person: str, source: str, hair_lo, hair_hi) -> None:
    rgba = load(SRC / source)
    rgb = rgba[..., :3]
    alpha = rgba[..., 3]
    l = lum(rgb)
    locks = ramp(smooth(l, 0.02, 0.75), hair_lo, hair_hi, 1.1)
    # the texture's see-through parts carry stray colours that show as fringes once it is filtered: give them the
    # hair's own colour
    base = (np.asarray(hair_lo, np.float32) + np.asarray(hair_hi, np.float32)) * 0.4
    locks = mix(np.zeros_like(locks) + base, locks, smooth(alpha, 0.02, 0.3))
    rgba = rgba.copy()
    rgba[..., :3] = locks
    save(OUT / f"{person}_hair.png", rgba, alpha=True)


# ---------------------------------------------------------------------------- bodies

def cloth(rgb, mask, colour, mean, contrast=1.0):
    """Recolours a garment and keeps its weave: the new colour, shaded by the old texture's light and dark."""
    l = lum(rgb)
    shade = np.clip((l / mean) ** contrast, 0.0, 2.2)
    return mix(rgb, np.asarray(colour, np.float32) * shade[..., None], mask)


def knit(shape, period=7.0, depth=0.1):
    """A fine rib and stitch pattern, as a brightness to multiply by."""
    h, w = shape[:2]
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    rib = 0.5 + 0.5 * np.sin(x * 2 * np.pi / period)
    stitch = 0.5 + 0.5 * np.sin((y + 2.0 * np.sin(x * 2 * np.pi / period)) * 2 * np.pi / (period * 0.7))
    return 1.0 - depth * (0.65 * (1 - rib) + 0.35 * (1 - stitch))


def maya_body() -> None:
    rgba = load(SRC / "Female_Adult_14" / "f017_body_color.jpg")
    rgb = rgba[..., :3]
    hue, sat, val = hsv(rgb)
    hands = np.maximum(box(rgb.shape, 0.0, 0.0, 0.2, 0.27), box(rgb.shape, 0.8, 0.0, 1.0, 0.27)) * smooth(val, 0.45, 0.6) * smooth(sat, 0.12, 0.22)
    # the cardigan: every pale, barely coloured knit pixel
    cardigan = smooth(val, 0.2, 0.34) * (1.0 - smooth(sat, 0.3, 0.42)) * (1.0 - hands)
    cardigan = gaussian_filter(cardigan, 0.7)
    out = cloth(rgb, cardigan, (0.56, 0.325, 0.26), 0.5, 1.15)
    # the top under it: the dark patch at the neckline, in cream
    top = box(rgb.shape, 0.43, 0.41, 0.57, 0.53, feather=4.0) * (1.0 - smooth(val, 0.22, 0.34))
    l = lum(rgb)
    out = mix(out, np.asarray([0.9, 0.85, 0.74], np.float32) * (0.8 + 1.3 * l[..., None]), top)
    out = mix(out, np.clip(rgb, 0, 1) ** 1.04 * np.asarray(MAYA_SKIN, np.float32), hands)
    rgba = rgba.copy()
    rgba[..., :3] = out
    save(OUT / "maya_body.png", rgba)
    Image.fromarray(np.clip(out * hands[..., None] * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGB").save(OUT / "maya_body_glow.png", optimize=True)


def adam_body() -> None:
    rgba = load(SRC / "Male_Adult_07" / "m013_body_color.jpg")
    rgb = rgba[..., :3]
    hue, sat, val = hsv(rgb)
    l = lum(rgb)
    hands = np.clip(np.maximum(box(rgb.shape, 0.0, 0.825, 0.232, 1.0, feather=2.0), box(rgb.shape, 0.768, 0.825, 1.0, 1.0, feather=2.0)), 0, 1) * smooth(val, 0.25, 0.4)
    # the trousers: the two panels at the top, all of them but the belt
    legs = np.clip(np.maximum(box(rgb.shape, 0.0, 0.0, 0.3, 0.44, feather=2.0), box(rgb.shape, 0.72, 0.0, 1.0, 0.44, feather=2.0)), 0, 1)
    belt = smooth(sat, 0.3, 0.45) * (hue < 45).astype(np.float32)
    denim = legs * smooth(val, 0.04, 0.1) * (1.0 - belt)
    shirt = smooth(sat, 0.38, 0.5) * ((hue < 32) | (hue > 345)).astype(np.float32) * smooth(val, 0.12, 0.2) * (1.0 - hands) * (1.0 - legs)
    shirt = gaussian_filter(shirt, 0.8)
    jacket = (1.0 - smooth(sat, 0.2, 0.3)) * smooth(val, 0.07, 0.13) * (1.0 - smooth(val, 0.6, 0.75)) * (1.0 - hands) * (1.0 - legs)
    jacket = gaussian_filter(jacket, 0.7)
    out = cloth(rgb, jacket, np.asarray((0.075, 0.105, 0.21)) * 1.0, 0.27, 0.9)
    out = mix(out, out * knit(rgb.shape)[..., None], jacket)
    out = cloth(out, shirt, (0.33, 0.39, 0.5), 0.3, 0.8)
    stone = np.asarray((0.5, 0.455, 0.385), np.float32) * np.clip((l / 0.26) ** 0.75, 0, 2.0)[..., None]
    out = mix(out, stone, denim)
    out = mix(out, np.clip(rgb, 0, 1) ** 1.08 * np.asarray(ADAM_SKIN, np.float32), hands)
    rgba = rgba.copy()
    rgba[..., :3] = out
    save(OUT / "adam_body.png", rgba)
    Image.fromarray(np.clip(out * hands[..., None] * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGB").save(OUT / "adam_body_glow.png", optimize=True)


MAYA_SKIN = (0.9, 0.845, 0.74)
MAYA_HAIR = ((0.07, 0.042, 0.03), (0.56, 0.38, 0.26))
ADAM_SKIN = (0.74, 0.63, 0.55)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    print("Maya")
    head("maya", "Female_Adult_14/f017_head_color.jpg", MAYA_SKIN, 1.04, MAYA_HAIR[0], MAYA_HAIR[1], 0.5,
         iris=((0.1, 0.05, 0.025), (0.62, 0.38, 0.18)))
    hair_cards("maya", "Female_Adult_14/f017_opacity_color.png", *MAYA_HAIR)
    maya_body()
    print("Adam")
    head("adam", "Male_Adult_07/m013_head_color.jpg", ADAM_SKIN, 1.08, (0.015, 0.012, 0.01), (0.2, 0.15, 0.12), 0.3, stubble=0.34)
    hair_cards("adam", "Male_Adult_07/m013_opacity_color.png", (0.015, 0.012, 0.01), (0.2, 0.15, 0.12))
    adam_body()


if __name__ == "__main__":
    main()
