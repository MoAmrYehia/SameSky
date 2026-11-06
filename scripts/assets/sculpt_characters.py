"""Sculpt SameSky's original stylized characters as a GLB kit (no third-party character assets).

Each part is a signed-distance-field "clay" sculpt (smooth-blended ellipsoids, capsules and round cones),
meshed with marching cubes, given gradient normals, UVs and vertex-colour shading cues, and written as a
named glTF node with a pivot. The runtime (web/src/shared/character.ts) assembles the parts, applies the
person's preset colours, and drives a seated pose, head, two-bone arm IK, blinks and breathing.

Run with a Python that has numpy + scikit-image (e.g. Anaconda):
    python scripts/assets/sculpt_characters.py            -> .asset-cache/characters_raw.glb
then:  node web/scripts/optimize-assets.mjs characters      -> web/public/assets/models/avatars/characters.glb

Coordinates: metres, +Y up, +Z forward (the character faces +Z).
"""

from __future__ import annotations

import json
import struct
import sys
import time
from pathlib import Path

import numpy as np
from skimage.measure import marching_cubes

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / ".asset-cache" / "characters_raw.glb"

# ---------------------------------------------------------------------------
# SDF primitives (vectorised over (..., 3) arrays)
# ---------------------------------------------------------------------------


def length(v):
    return np.sqrt(np.einsum("...i,...i->...", v, v))


def sphere(p, c, r):
    return length(p - np.asarray(c, np.float32)) - r


def ellipsoid(p, c, r):
    r = np.asarray(r, np.float32)
    q = (p - np.asarray(c, np.float32)) / r
    k0 = length(q)
    k1 = length(q / r)
    return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)


def capsule(p, a, b, r):
    a = np.asarray(a, np.float32)
    b = np.asarray(b, np.float32)
    pa = p - a
    ba = b - a
    h = np.clip((pa @ ba) / (ba @ ba), 0.0, 1.0)
    return length(pa - h[..., None] * ba) - r


def round_cone(p, a, b, r1, r2):
    """Inigo Quilez' exact round cone between points a (radius r1) and b (radius r2)."""
    a = np.asarray(a, np.float32)
    b = np.asarray(b, np.float32)
    ba = b - a
    l2 = float(ba @ ba)
    rr = r1 - r2
    a2 = l2 - rr * rr
    il2 = 1.0 / l2
    pa = p - a
    y = pa @ ba
    z = y - l2
    xv = pa * l2 - ba * y[..., None]
    x2 = np.einsum("...i,...i->...", xv, xv)
    y2 = y * y * l2
    z2 = z * z * l2
    k = np.sign(rr) * rr * rr * x2
    d3 = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - r1
    d1 = np.sqrt(x2 + z2) * il2 - r2
    d2 = np.sqrt(x2 + y2) * il2 - r1
    return np.where(np.sign(z) * a2 * z2 > k, d1, np.where(np.sign(y) * a2 * y2 < k, d2, d3))


def torus(p, c, R, r, axis="y"):
    q = p - np.asarray(c, np.float32)
    if axis == "y":
        planar, h = np.sqrt(q[..., 0] ** 2 + q[..., 2] ** 2), q[..., 1]
    elif axis == "x":
        planar, h = np.sqrt(q[..., 1] ** 2 + q[..., 2] ** 2), q[..., 0]
    else:
        planar, h = np.sqrt(q[..., 0] ** 2 + q[..., 1] ** 2), q[..., 2]
    return np.sqrt((planar - R) ** 2 + h * h) - r


def rounded_box(p, c, half, r):
    q = np.abs(p - np.asarray(c, np.float32)) - (np.asarray(half, np.float32) - r)
    return length(np.maximum(q, 0.0)) + np.minimum(np.max(q, axis=-1), 0.0) - r


def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1 - h) + a * h - k * h * (1 - h)


def smax(a, b, k):
    return -smin(-a, -b, k)


def union(*ds):
    out = ds[0]
    for d in ds[1:]:
        out = np.minimum(out, d)
    return out


def blend(k, *ds):
    out = ds[0]
    for d in ds[1:]:
        out = smin(out, d, k)
    return out


def chain(p, pts, radii):
    """Smooth chain of round cones through points (for fingers, brows, curls)."""
    d = None
    for i in range(len(pts) - 1):
        seg = round_cone(p, pts[i], pts[i + 1], radii[i], radii[i + 1])
        d = seg if d is None else smin(d, seg, 0.004)
    return d


# ---------------------------------------------------------------------------
# Head presets
# ---------------------------------------------------------------------------

PRESETS = {
    # rounder face, small button nose, fuller lips, larger eyes
    "soft": dict(eye_r=0.0175, eye_x=0.0305, eye_y=0.104, eye_z=0.0715, jaw=(0.058, 0.05, 0.058), jaw_c=(0.0, 0.06, 0.03),
                 chin=(0.0, 0.031, 0.067, 0.019), cheek=(0.043, 0.079, 0.058, 0.027), brow_ridge=0.009,
                 nose_tip=(0.0, 0.0845, 0.0975, 0.0105), nose_bridge=0.0055, wing=0.008, lip_upper=0.0076, lip_lower=0.0094, lip_w=0.019),
    # stronger jaw and chin, larger nose, straighter heavier brows
    "defined": dict(eye_r=0.0165, eye_x=0.0315, eye_y=0.106, eye_z=0.0725, jaw=(0.065, 0.052, 0.06), jaw_c=(0.0, 0.058, 0.031),
                    chin=(0.0, 0.028, 0.069, 0.022), cheek=(0.045, 0.086, 0.056, 0.023), brow_ridge=0.0115,
                    nose_tip=(0.0, 0.0825, 0.1005, 0.0125), nose_bridge=0.0075, wing=0.0095, lip_upper=0.006, lip_lower=0.0074, lip_w=0.0185),
}


def eye_centers(pr):
    return [np.array([s * pr["eye_x"], pr["eye_y"], pr["eye_z"]], np.float32) for s in (-1, 1)]


def head_sdf(p, pr):
    cranium = ellipsoid(p, (0, 0.135, -0.008), (0.081, 0.088, 0.088))
    face = ellipsoid(p, (0, 0.096, 0.02), (0.07, 0.078, 0.07))
    jaw = ellipsoid(p, pr["jaw_c"], pr["jaw"])
    cx, cy, cz, cr = pr["chin"]
    chin = sphere(p, (cx, cy, cz), cr)
    kx, ky, kz, kr = pr["cheek"]
    cheeks = union(sphere(p, (-kx, ky, kz), kr), sphere(p, (kx, ky, kz), kr))
    brow = capsule(p, (-0.038, 0.127, 0.071), (0.038, 0.127, 0.071), pr["brow_ridge"])
    d = blend(0.03, cranium, face)
    d = smin(d, jaw, 0.03)
    d = smin(d, chin, 0.022)
    d = smin(d, cheeks, 0.024)
    d = smin(d, brow, 0.02)
    # nose: bridge -> tip, nostril wings
    tx, ty, tz, tr = pr["nose_tip"]
    nose = round_cone(p, (0, 0.113, 0.082), (tx, ty, tz), pr["nose_bridge"], tr)
    wings = union(sphere(p, (-0.0115, ty - 0.004, tz - 0.009), pr["wing"]), sphere(p, (0.0115, ty - 0.004, tz - 0.009), pr["wing"]))
    d = smin(d, smin(nose, wings, 0.006), 0.012)
    # eye sockets
    for c in eye_centers(pr):
        d = smax(d, -sphere(p, c + np.array([0, 0.001, 0.004], np.float32), pr["eye_r"] + 0.0035), 0.007)
    # mouth line (a soft crease behind the lips) and a philtrum groove
    smile = p.copy()
    smile[..., 1] -= 5.0 * p[..., 0] ** 2
    d = smax(d, -capsule(smile, (-0.017, 0.0582, 0.084), (0.017, 0.0582, 0.084), 0.0022), 0.003)
    d = smax(d, -capsule(p, (0, 0.0645, 0.0915), (0, 0.073, 0.0925), 0.0018), 0.003)
    # ears with an inner bowl
    for s in (-1, 1):
        ear = ellipsoid(p, (s * 0.081, 0.104, -0.006), (0.011, 0.025, 0.016))
        d = smin(d, ear, 0.008)
        d = smax(d, -ellipsoid(p, (s * 0.0875, 0.104, -0.003), (0.0055, 0.016, 0.0095)), 0.003)
    return d


def head_colour(v, pr):
    """Vertex colour multipliers: blush on cheeks, warmer lips area, slightly darker ears/nostrils."""
    col = np.ones((len(v), 3), np.float32)
    kx, ky, kz, _ = pr["cheek"]
    for s in (-1, 1):
        w = np.exp(-(length(v - np.array([s * kx, ky - 0.004, kz + 0.02], np.float32)) / 0.017) ** 2)
        col *= 1 - w[:, None] * np.array([0.0, 0.14, 0.13], np.float32)
    tx, ty, tz, _ = pr["nose_tip"]
    w = np.exp(-(length(v - np.array([0, ty, tz], np.float32)) / 0.012) ** 2)
    col *= 1 - w[:, None] * np.array([0.0, 0.06, 0.06], np.float32)
    for s in (-1, 1):
        w = np.exp(-(length(v - np.array([s * 0.084, 0.104, -0.004], np.float32)) / 0.02) ** 2)
        col *= 1 - w[:, None] * np.array([0.04, 0.1, 0.1], np.float32)
    # shading in the eye sockets helps the eyes read at a distance
    for c in eye_centers(pr):
        w = np.exp(-(length(v - c) / (pr["eye_r"] * 1.5)) ** 2)
        col *= 1 - w[:, None] * np.array([0.04, 0.05, 0.05], np.float32)
    return col


def lips_sdf(p, pr):
    q = p.copy()
    q[..., 1] -= 5.0 * p[..., 0] ** 2  # gentle smile curve
    w = pr["lip_w"]
    upper = blend(0.004,
                  ellipsoid(q, (-0.0085, 0.0628, 0.0855), (w * 0.62, pr["lip_upper"], 0.0085)),
                  ellipsoid(q, (0.0085, 0.0628, 0.0855), (w * 0.62, pr["lip_upper"], 0.0085)),
                  ellipsoid(q, (0, 0.0637, 0.0885), (0.0055, pr["lip_upper"] * 0.85, 0.0065)))
    lower = ellipsoid(q, (0, 0.0525, 0.0845), (w * 0.95, pr["lip_lower"], 0.0092))
    return smin(upper, lower, 0.002)


def brows_sdf(p, pr, preset):
    d = None
    for s in (-1, 1):
        ex, ey, ez = s * pr["eye_x"], pr["eye_y"], pr["eye_z"]
        if preset == "soft":
            pts = [(ex - s * 0.017, ey + 0.021, ez + 0.012), (ex - s * 0.002, ey + 0.0265, ez + 0.0155), (ex + s * 0.019, ey + 0.0225, ez + 0.005)]
            radii = [0.0033, 0.0036, 0.0027]
        else:
            pts = [(ex - s * 0.018, ey + 0.02, ez + 0.013), (ex + s * 0.002, ey + 0.0235, ez + 0.0158), (ex + s * 0.02, ey + 0.021, ez + 0.005)]
            radii = [0.0047, 0.0046, 0.0034]
        c = chain(p, [np.asarray(x, np.float32) for x in pts], radii)
        d = c if d is None else np.minimum(d, c)
    return d


# ---------------------------------------------------------------------------
# Hair (head-local; solid sculpted volumes with groove detail)
# ---------------------------------------------------------------------------


def face_window(p):
    # The region in front of the face where hair must not grow (smoothly removed). Kept clear of the scalp
    # surface so the hair keeps a visible, rounded edge instead of thinning to a sliver.
    return blend(0.025,
                 ellipsoid(p, (0, 0.066, 0.104), (0.068, 0.092, 0.07)),
                 ellipsoid(p, (0, 0.025, 0.03), (0.088, 0.07, 0.092)))


def hair_base(p, expand=0.0):
    # Centred slightly behind the skull so the hair surface emerges from the forehead skin (a tapered,
    # natural hairline) instead of sitting on it as a thick shelf.
    return ellipsoid(p, (0, 0.14, -0.02), (0.0905 + expand, 0.099 + expand, 0.0945 + expand))


def grooves(p, strength, freq=11.0):
    ang = np.arctan2(p[..., 0], p[..., 2] + 0.02)
    return strength * np.sin(ang * freq + p[..., 1] * 40.0)


def hair_sdf(p, style):
    above_nape = smax(p[..., 1] * 0 - 1, -(p[..., 1] - 0.075), 0.0)  # helper (unused)
    del above_nape
    if style == "short":
        d = hair_base(p, -0.001)
        quiff = ellipsoid(p, (0.012, 0.2, 0.045), (0.06, 0.03, 0.045))
        d = smin(d, quiff, 0.02)
        d = d + grooves(p, 0.0016, 14.0) * (p[..., 1] > 0.15)
        d = smax(d, -(p[..., 1] - 0.072), 0.01)  # nape
        for s in (-1, 1):  # tight above the ears
            d = smax(d, -ellipsoid(p, (s * 0.088, 0.1, -0.005), (0.02, 0.04, 0.03)), 0.01)
        return smax(d, -face_window(p), 0.012)
    if style == "buzz":
        d = hair_base(p, -0.0055)
        d = smax(d, -(p[..., 1] - 0.078), 0.008)
        for s in (-1, 1):
            d = smax(d, -ellipsoid(p, (s * 0.086, 0.1, -0.005), (0.018, 0.036, 0.028)), 0.008)
        return smax(d, -face_window(p), 0.01)
    if style in ("wavy", "long"):
        bottom = -0.05 if style == "wavy" else -0.16
        cap = hair_base(p, 0.004)
        part = capsule(p, (0.024, 0.23, 0.07), (0.02, 0.22, -0.06), 0.004)  # side parting groove
        cap = smax(cap, -part, 0.006)
        sides = None
        for s in (-1, 1):
            curtain = round_cone(p, (s * 0.07, 0.15, -0.01), (s * 0.082, bottom, -0.02), 0.04, 0.03 if style == "wavy" else 0.024)
            sides = curtain if sides is None else smin(sides, curtain, 0.02)
        back = round_cone(p, (0, 0.15, -0.045), (0, bottom, -0.06), 0.075, 0.05 if style == "wavy" else 0.04)
        d = blend(0.03, cap, sides, back)
        # side-swept fringe: a diagonal swoop from the parting down toward the opposite temple
        swoop = round_cone(p, (0.03, 0.2, 0.05), (-0.058, 0.158, 0.06), 0.022, 0.016)
        d = smin(d, swoop, 0.02)
        waves = 0.0042 * np.sin(p[..., 1] * 95.0 + np.arctan2(p[..., 0], p[..., 2]) * 3.0)
        d = d + waves * (p[..., 1] < 0.14) + grooves(p, 0.0013, 16.0)
        d = smax(d, -face_window(p), 0.014)
        d = smax(d, -ellipsoid(p, (0, bottom - 0.06, 0.06), (0.14, 0.08, 0.1)), 0.02)  # keep the chest clear
        return d
    if style == "bun":
        d = hair_base(p, 0.0)
        d = d + grooves(p, 0.0012, 18.0)
        bun = sphere(p, (0, 0.205, -0.07), 0.038)
        swirl = 0.002 * np.sin(np.arctan2(p[..., 1] - 0.205, p[..., 2] + 0.07) * 6.0 + p[..., 0] * 80)
        d = smin(d, bun + swirl, 0.012)
        d = smax(d, -(p[..., 1] - 0.07), 0.012)
        return smax(d, -face_window(p), 0.012)
    if style == "curly":
        d = hair_base(p, 0.002)
        rng = np.random.default_rng(7)
        for _ in range(70):
            th = rng.uniform(0, 2 * np.pi)
            ph = rng.uniform(0.15, 1.75)
            c = np.array([0.094 * np.sin(ph) * np.sin(th), 0.137 + 0.1 * np.cos(ph), -0.012 + 0.1 * np.sin(ph) * np.cos(th)], np.float32)
            if c[2] > 0.05 and c[1] < 0.17:
                continue
            d = smin(d, sphere(p, c, rng.uniform(0.016, 0.024)), 0.006)
        d = smax(d, -(p[..., 1] - 0.06), 0.012)
        return smax(d, -face_window(p), 0.012)
    raise ValueError(style)


# ---------------------------------------------------------------------------
# Body (torso-local: origin at the pelvis on the seat)
# ---------------------------------------------------------------------------

NECK = (0.0, 0.47, 0.012)
SHOULDERS = [(-0.158, 0.425, -0.005), (0.158, 0.425, -0.005)]
UPPER_ARM = 0.255
FOREARM = 0.235


def torso_sdf(p):
    rib = ellipsoid(p, (0, 0.33, 0.0), (0.148, 0.155, 0.1))
    chest = ellipsoid(p, (0, 0.36, 0.03), (0.13, 0.09, 0.075))
    waist = ellipsoid(p, (0, 0.17, 0.0), (0.13, 0.13, 0.092))
    hips = ellipsoid(p, (0, 0.05, -0.012), (0.155, 0.085, 0.115))
    shoulders = capsule(p, (-0.13, 0.415, -0.008), (0.13, 0.415, -0.008), 0.058)
    caps = union(sphere(p, SHOULDERS[0], 0.06), sphere(p, SHOULDERS[1], 0.06))
    d = blend(0.05, rib, chest, waist, hips)
    d = smin(d, shoulders, 0.05)
    d = smin(d, caps, 0.03)
    # ribbed crew collar and hem, knit folds
    d = smin(d, torus(p, (0, 0.462, 0.006), 0.05, 0.011), 0.012)
    d = d - 0.0007 * np.sin(np.arctan2(p[..., 0], p[..., 2]) * 60.0) * np.exp(-(((p[..., 1] - 0.462) / 0.008) ** 2))
    d = d + 0.0018 * np.sin(p[..., 1] * 70.0 + p[..., 0] * 9.0) * np.exp(-((p[..., 0] / 0.09) ** 2))
    d = smax(d, -capsule(p, (0, 0.47, 0.01), (0, 0.6, 0.02), 0.042), 0.01)  # neck opening
    return d


def neck_sdf(p):
    return round_cone(p, (0, 0.43, 0.004), (NECK[0], NECK[1] + 0.05, NECK[2] + 0.008), 0.05, 0.047)


def legs_sdf(p):
    d = None
    for s in (-1, 1):
        hip = (s * 0.085, 0.03, 0.0)
        knee = (s * 0.1, 0.05, 0.42)
        ankle = (s * 0.105, -0.36, 0.45)
        thigh = round_cone(p, hip, knee, 0.075, 0.055)
        shin = round_cone(p, knee, ankle, 0.052, 0.04)
        shoe = rounded_box(p, (s * 0.105, -0.395, 0.49), (0.045, 0.03, 0.085), 0.025)
        leg = smin(smin(thigh, shin, 0.02), shoe, 0.012)
        d = leg if d is None else np.minimum(d, leg)
    return d


def upper_arm_sdf(p):
    d = round_cone(p, (0, 0, 0), (0, -UPPER_ARM, 0), 0.054, 0.046)
    d = d + 0.0015 * np.sin(p[..., 1] * 80.0 + np.arctan2(p[..., 0], p[..., 2]) * 2.0)
    return d


def forearm_sdf(p):
    d = round_cone(p, (0, 0.012, 0), (0, -FOREARM + 0.01, 0), 0.046, 0.036)
    cuff = round_cone(p, (0, -FOREARM + 0.03, 0), (0, -FOREARM + 0.002, 0), 0.04, 0.04)
    return smin(d, cuff + 0.0006 * np.sin(np.arctan2(p[..., 0], p[..., 2]) * 28.0), 0.006)


def hand_sdf(p):
    """Right hand, wrist at origin, fingers along -Y, palm facing -Z (back of the hand +Z)."""
    wrist = round_cone(p, (0, 0.03, 0), (0, -0.02, 0), 0.026, 0.024)
    palm = rounded_box(p, (0.0, -0.052, 0.0), (0.038, 0.04, 0.0125), 0.011)
    d = smin(wrist, palm, 0.012)
    fingers = [  # (x at knuckle, segment lengths, radius)
        (-0.026, (0.034, 0.022, 0.018), 0.0085),
        (-0.0085, (0.04, 0.025, 0.02), 0.009),
        (0.009, (0.038, 0.024, 0.019), 0.0087),
        (0.025, (0.03, 0.019, 0.016), 0.0078),
    ]
    for x, segs, r in fingers:
        pts = [np.array([x, -0.088, 0.0], np.float32)]
        ang = 0.0
        for i, seg in enumerate(segs):
            ang += 0.18 + 0.1 * i  # relaxed curl toward the palm (-Z)
            prev = pts[-1]
            pts.append(prev + np.array([x * 0.08, -seg * np.cos(ang), -seg * np.sin(ang)], np.float32))
        radii = [r, r * 0.95, r * 0.88, r * 0.8]
        d = smin(d, chain(p, pts, radii), 0.006)
    thumb = chain(p, [np.array(v, np.float32) for v in [(0.03, -0.03, -0.004), (0.05, -0.055, -0.014), (0.058, -0.078, -0.022), (0.06, -0.095, -0.026)]], [0.013, 0.011, 0.0095, 0.0085])
    d = smin(d, thumb, 0.012)
    # knuckle definition on the back of the hand
    d = d + 0.0008 * np.cos(p[..., 0] * 190.0) * np.exp(-(((p[..., 1] + 0.086) / 0.01) ** 2)) * (p[..., 2] > 0)
    return d


def scarf_sdf(p):
    ring = torus(p, (0, 0.462, 0.008), 0.062, 0.022)
    tail = round_cone(p, (0.03, 0.44, 0.07), (0.045, 0.28, 0.105), 0.022, 0.018)
    knot = sphere(p, (0.025, 0.44, 0.075), 0.026)
    return blend(0.012, ring, knot, tail) + 0.001 * np.sin(p[..., 1] * 150.0)


def cap_sdf(p):
    crown = smax(ellipsoid(p, (0, 0.142, -0.012), (0.093, 0.1, 0.1)), -(p[..., 1] - 0.155), 0.006)
    brim = rounded_box(p, (0, 0.158, 0.1), (0.075, 0.006, 0.055), 0.005)
    button = sphere(p, (0, 0.245, -0.01), 0.008)
    return union(smin(crown, brim, 0.006), button)


# ---------------------------------------------------------------------------
# Meshing and glTF writing
# ---------------------------------------------------------------------------


def mesh_part(name, fn, lo, hi, voxel, colour=None, uv="sphere", uv_center=(0, 0, 0)):
    t0 = time.time()
    lo = np.asarray(lo, np.float32)
    hi = np.asarray(hi, np.float32)
    n = np.ceil((hi - lo) / voxel).astype(int) + 1
    xs = lo[0] + np.arange(n[0], dtype=np.float32) * voxel
    ys = lo[1] + np.arange(n[1], dtype=np.float32) * voxel
    zs = lo[2] + np.arange(n[2], dtype=np.float32) * voxel
    vol = np.empty(tuple(n), np.float32)
    for i, x in enumerate(xs):  # one slab at a time keeps memory bounded
        Y, Z = np.meshgrid(ys, zs, indexing="ij")
        p = np.stack([np.full_like(Y, x), Y, Z], -1)
        vol[i] = fn(p)
    verts, faces, _, _ = marching_cubes(vol, level=0.0, spacing=(voxel, voxel, voxel))
    verts = (verts + lo).astype(np.float32)
    # gradient normals from the SDF itself (smooth, outward)
    e = voxel * 0.5
    nrm = np.zeros_like(verts)
    for axis in range(3):
        off = np.zeros(3, np.float32)
        off[axis] = e
        nrm[:, axis] = fn(verts + off) - fn(verts - off)
    nrm /= np.maximum(length(nrm)[:, None], 1e-9)
    # make the winding agree with the outward normals
    tri = verts[faces]
    fn_ = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    if np.mean(np.einsum("ij,ij->i", fn_, nrm[faces[:, 0]])) < 0:
        faces = faces[:, ::-1]
    # UVs: spherical around a centre (heads, hair) or cylindrical around Y (sleeves, torso, legs)
    q = verts - np.asarray(uv_center, np.float32)
    if uv == "sphere":
        u = 0.5 + np.arctan2(q[:, 0], q[:, 2]) / (2 * np.pi)
        v = 0.5 + np.arcsin(np.clip(q[:, 1] / np.maximum(length(q), 1e-9), -1, 1)) / np.pi
    else:
        u = 0.5 + np.arctan2(q[:, 0], q[:, 2]) / (2 * np.pi)
        v = q[:, 1] * 4.0
    uvs = np.stack([u * (4.0 if uv == "cyl" else 1.0), v], -1).astype(np.float32)
    cols = colour(verts) if colour else None
    print(f"  {name:16} {len(verts):7d} verts {len(faces):7d} tris  {time.time() - t0:5.1f}s", flush=True)
    return {"name": name, "pos": verts, "nrm": nrm.astype(np.float32), "uv": uvs, "col": cols, "idx": faces.astype(np.uint32)}


def write_glb(path, parts, nodes, extras):
    buf = bytearray()
    views, accessors, meshes = [], [], []

    def add(arr, target, comp, typ, minmax=False):
        nonlocal buf
        while len(buf) % 4:
            buf.append(0)
        data = arr.tobytes()
        views.append({"buffer": 0, "byteOffset": len(buf), "byteLength": len(data), **({"target": target} if target else {})})
        buf += data
        acc = {"bufferView": len(views) - 1, "componentType": comp, "count": int(arr.shape[0]), "type": typ}
        if minmax:
            acc["min"] = arr.min(0).tolist()
            acc["max"] = arr.max(0).tolist()
        accessors.append(acc)
        return len(accessors) - 1

    materials = []
    mat_index = {}
    for part in parts:
        mat = part.get("material", "default")
        if mat not in mat_index:
            mat_index[mat] = len(materials)
            materials.append({"name": mat, "pbrMetallicRoughness": {"baseColorFactor": [0.8, 0.8, 0.8, 1], "metallicFactor": 0.0, "roughnessFactor": 0.6}})
        attrs = {
            "POSITION": add(part["pos"], 34962, 5126, "VEC3", True),
            "NORMAL": add(part["nrm"], 34962, 5126, "VEC3"),
            "TEXCOORD_0": add(part["uv"], 34962, 5126, "VEC2"),
        }
        if part["col"] is not None:
            attrs["COLOR_0"] = add(part["col"].astype(np.float32), 34962, 5126, "VEC3")
        idx = add(part["idx"].reshape(-1), 34963, 5125, "SCALAR")
        meshes.append({"name": part["name"], "primitives": [{"attributes": attrs, "indices": idx, "material": mat_index[part.get("material", "default")]}]})
    mesh_by_name = {m["name"]: i for i, m in enumerate(meshes)}
    gltf_nodes = []
    for node in nodes:
        entry = {"name": node["name"], "translation": list(map(float, node.get("t", (0, 0, 0))))}
        if node.get("mesh"):
            entry["mesh"] = mesh_by_name[node["mesh"]]
        gltf_nodes.append(entry)
    gltf = {
        "asset": {"version": "2.0", "generator": "SameSky sculpt_characters.py", "extras": extras},
        "scene": 0,
        "scenes": [{"nodes": list(range(len(gltf_nodes)))}],
        "nodes": gltf_nodes,
        "meshes": meshes,
        "materials": materials,
        "accessors": accessors,
        "bufferViews": views,
        "buffers": [{"byteLength": len(buf)}],
    }
    js = json.dumps(gltf, separators=(",", ":")).encode()
    js += b" " * ((4 - len(js) % 4) % 4)
    while len(buf) % 4:
        buf.append(0)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(buf)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        f.write(struct.pack("<II", len(buf), 0x004E4942) + bytes(buf))


def main():
    fine = 0.0011 if "--draft" not in sys.argv else 0.002
    parts, nodes = [], []
    head_lo, head_hi = (-0.1, -0.005, -0.11), (0.1, 0.235, 0.12)
    print("heads", flush=True)
    for preset, pr in PRESETS.items():
        parts.append({**mesh_part(f"head_{preset}", lambda p, pr=pr: head_sdf(p, pr), head_lo, head_hi, fine,
                                  colour=lambda v, pr=pr: head_colour(v, pr), uv_center=(0, 0.11, 0)), "material": "skin"})
        parts.append({**mesh_part(f"lips_{preset}", lambda p, pr=pr: lips_sdf(p, pr), (-0.025, 0.04, 0.07), (0.025, 0.075, 0.1), fine * 0.45), "material": "lips"})
        parts.append({**mesh_part(f"brows_{preset}", lambda p, pr=pr, n=preset: brows_sdf(p, pr, n), (-0.06, 0.11, 0.06), (0.06, 0.145, 0.1), fine * 0.45), "material": "brows"})
    print("hair", flush=True)
    for style in ("short", "wavy", "long", "bun", "curly", "buzz"):
        lo = (-0.12, -0.2 if style == "long" else -0.08, -0.14)
        parts.append({**mesh_part(f"hair_{style}", lambda p, s=style: hair_sdf(p, s), lo, (0.12, 0.27, 0.13), fine * 1.2, uv_center=(0, 0.12, 0)), "material": "hair"})
    print("body", flush=True)
    parts.append({**mesh_part("torso", torso_sdf, (-0.23, -0.06, -0.15), (0.23, 0.52, 0.14), fine * 2.2, uv="cyl"), "material": "top"})
    parts.append({**mesh_part("neck", neck_sdf, (-0.06, 0.38, -0.06), (0.06, 0.56, 0.07), fine * 1.2, uv="cyl"), "material": "skin"})
    parts.append({**mesh_part("legs", legs_sdf, (-0.2, -0.45, -0.1), (0.2, 0.15, 0.6), fine * 3.0, uv="cyl"), "material": "pants"})
    parts.append({**mesh_part("upper_arm", upper_arm_sdf, (-0.07, -0.32, -0.07), (0.07, 0.07, 0.07), fine * 1.6, uv="cyl"), "material": "top"})
    parts.append({**mesh_part("forearm", forearm_sdf, (-0.06, -0.26, -0.06), (0.06, 0.07, 0.06), fine * 1.4, uv="cyl"), "material": "top"})
    parts.append({**mesh_part("hand", hand_sdf, (-0.05, -0.18, -0.06), (0.08, 0.05, 0.035), fine * 0.55), "material": "skin"})
    parts.append({**mesh_part("scarf", scarf_sdf, (-0.1, 0.25, -0.08), (0.1, 0.5, 0.15), fine * 1.6, uv="cyl"), "material": "accent"})
    parts.append({**mesh_part("cap", cap_sdf, (-0.11, 0.1, -0.13), (0.11, 0.26, 0.17), fine * 1.3), "material": "accent"})

    # Every part is written in its own authored local frame at the origin; the runtime rig places it
    # (pivots are in the extras below). Keeping node transforms empty means the only node transform after
    # optimisation is meshopt's de-quantisation, which the loader bakes into float geometry.
    for part in parts:
        nodes.append({"name": part["name"], "mesh": part["name"], "t": (0, 0, 0)})

    extras = {
        "units": "metres",
        "neckPivot": [0, NECK[1] + 0.035, NECK[2] + 0.004],
        "shoulders": SHOULDERS,
        "upperArm": UPPER_ARM,
        "forearm": FOREARM,
        "eyes": {k: {"centers": [c.tolist() for c in eye_centers(v)], "radius": v["eye_r"]} for k, v in PRESETS.items()},
        "earrings": [[-0.084, 0.078, 0.002], [0.084, 0.078, 0.002]],
        "glasses": {"y": 0.104, "z": 0.093, "lens": 0.021},
        "labelHeight": 0.32,
        "seatToEye": NECK[1] + 0.035 + 0.104 * 1.3,
        "parts": [p["name"] for p in parts],
    }
    OUT.parent.mkdir(exist_ok=True)
    write_glb(OUT, parts, nodes, extras)
    tris = sum(len(p["idx"]) for p in parts)
    print(f"wrote {OUT} ({OUT.stat().st_size / 1e6:.1f} MB, {tris} raw triangles)")


if __name__ == "__main__":
    main()
