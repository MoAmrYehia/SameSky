"""GLB (binary glTF 2.0) validation and rig inspection for avatar assets.

Runs before any model is stored or shown. It parses the container itself (no third-party loader), rejects
external or unsupported resources, bounds decoded geometry and texture sizes, and reports the rig
capabilities the client may rely on: a skinned humanoid skeleton (with the bones the seated rig needs),
morph targets such as eye blinks, and counts used for performance budgets.
"""

from __future__ import annotations

import hashlib
import json
import re
import struct
from dataclasses import dataclass, field

GLB_MAGIC = 0x46546C67  # "glTF"
CHUNK_JSON = 0x4E4F534A
CHUNK_BIN = 0x004E4942

# Extensions the room's loader can decode (GLTFLoader + the meshopt decoder that ships with the app).
SUPPORTED_EXTENSIONS = {
    "KHR_mesh_quantization",
    "KHR_texture_transform",
    "KHR_materials_emissive_strength",
    "KHR_materials_clearcoat",
    "KHR_materials_sheen",
    "KHR_materials_specular",
    "KHR_materials_ior",
    "KHR_materials_unlit",
    "EXT_meshopt_compression",
    "KHR_meshopt_compression",
}

COMPONENT_BYTES = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}


@dataclass(frozen=True)
class Limits:
    max_bytes: int = 40 * 1024 * 1024
    max_triangles: int = 250_000
    max_vertices: int = 400_000
    max_nodes: int = 3_000
    max_joints: int = 512
    max_images: int = 24
    max_texture_px: int = 4096
    max_morph_targets: int = 256


class GlbRejected(Exception):
    def __init__(self, code: str, message: str):
        super().__init__(code)
        self.code = code
        self.message = message


@dataclass
class Inspection:
    content_hash: str
    size_bytes: int
    triangles: int
    vertices: int
    nodes: int
    images: int
    max_texture_px: int
    extensions: list[str]
    rig: dict = field(default_factory=dict)


# --- humanoid bone detection (mirrors web/src/shared/avatarRig.ts) -------------------------------------

_PREFIX = re.compile(r"^(mixamorig\d*[:_]?|armature[|:_]?|j_bip_[clr]_|bip0?1[ _]?|def[-_]|cc_base_|rig[:_])", re.I)


def _norm(name: str) -> str:
    n = _PREFIX.sub("", name.strip())
    return re.sub(r"[\s.\-]", "_", n).lower()


def _side(n: str) -> str | None:
    if re.search(r"(^|_)(left|l)(_|$)|^left|left$|_l$|^l_", n):
        return "left"
    if re.search(r"(^|_)(right|r)(_|$)|^right|right$|_r$|^r_", n):
        return "right"
    return None


def _strip_side(n: str) -> str:
    n = re.sub(r"(^|_)(left|right)", r"\1", n)
    n = re.sub(r"(^|_)[lr](_|$)", r"\1", n)
    n = re.sub(r"(left|right)$", "", n)
    return n.strip("_")


def map_humanoid(names: list[str]) -> dict[str, str]:
    """Map skeleton joint names to the bones the seated rig drives. Returns {role: joint name}.

    Covers Mixamo/Avaturn/Ready-Player-Me style (Hips, Spine1, LeftForeArm), Unreal (pelvis, upperarm_l),
    VRM (J_Bip_L_UpperArm), Unity-humanoid (LeftUpperArm) and Biped (Bip01 L UpperArm) names. Spine:
    the first spine-like joint is `spine`, the last one before the neck is `chest`.
    """
    roles: dict[str, str] = {}
    spine_like: list[str] = []
    for raw in names:
        lowered = raw.strip().lower()
        vrm_side = "left" if re.match(r"^j_bip_l_", lowered) else "right" if re.match(r"^j_bip_r_", lowered) else None
        n = _norm(raw)
        side = vrm_side or _side(n)
        core = _strip_side(n) if side else n
        role = None
        if side is None:
            if core in ("hips", "pelvis", "root_hips", "hip"):
                role = "hips"
            elif re.fullmatch(r"neck(_?0?1)?", core):
                role = "neck"
            elif core == "head":
                role = "head"
            elif re.fullmatch(r"(spine(_?0?\d)?|chest|upper_?chest)", core):
                spine_like.append(raw)
        else:
            prefix = "left" if side == "left" else "right"
            if re.fullmatch(r"(upper_?arm|arm|up_?arm)", core):
                role = f"{prefix}UpperArm"
            elif re.fullmatch(r"(fore_?arm|lower_?arm|elbow)", core):
                role = f"{prefix}LowerArm"
            elif re.fullmatch(r"(hand|wrist)", core):
                role = f"{prefix}Hand"
            elif re.fullmatch(r"(up_?leg|upper_?leg|thigh)", core):
                role = f"{prefix}UpperLeg"
            elif re.fullmatch(r"(leg|lower_?leg|calf|shin|knee)", core):
                role = f"{prefix}LowerLeg"
            elif re.fullmatch(r"(foot|ankle)", core):
                role = f"{prefix}Foot"
            elif re.fullmatch(r"(shoulder|clavicle)", core):
                role = f"{prefix}Shoulder"
            elif core == "eye":
                role = f"{prefix}Eye"
        if role and role not in roles:
            roles[role] = raw
    if spine_like:
        roles["spine"] = spine_like[0]
        if len(spine_like) > 1:
            roles["chest"] = spine_like[-1]
    return roles


REQUIRED_ROLES = (
    "hips", "neck", "head",
    "leftUpperArm", "leftLowerArm", "leftHand", "rightUpperArm", "rightLowerArm", "rightHand",
)
BLINK_NAMES = ({"eyeblinkleft", "eye_blink_left", "blink_l", "eyeblink_l", "eyesclosed_l"}, {"eyeblinkright", "eye_blink_right", "blink_r", "eyeblink_r", "eyesclosed_r"})


def _image_size(data: bytes) -> tuple[int, int] | None:
    if data[:8] == b"\x89PNG\r\n\x1a\n" and len(data) >= 24:
        return struct.unpack(">II", data[16:24])
    if data[:3] == b"\xff\xd8\xff":
        i = 2
        while i + 9 < len(data):
            if data[i] != 0xFF:
                i += 1
                continue
            marker = data[i + 1]
            if marker in (0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF):
                h, w = struct.unpack(">HH", data[i + 5 : i + 9])
                return w, h
            length = struct.unpack(">H", data[i + 2 : i + 4])[0]
            i += 2 + length
        return None
    if data[:4] == b"RIFF" and data[8:12] == b"WEBP":
        if data[12:16] == b"VP8X" and len(data) >= 30:
            w = 1 + int.from_bytes(data[24:27], "little")
            h = 1 + int.from_bytes(data[27:30], "little")
            return w, h
        if data[12:16] == b"VP8 " and len(data) >= 30:
            w, h = struct.unpack("<HH", data[26:30])
            return w & 0x3FFF, h & 0x3FFF
        if data[12:16] == b"VP8L" and len(data) >= 25:
            b = data[21:25]
            w = 1 + (((b[1] & 0x3F) << 8) | b[0])
            h = 1 + (((b[3] & 0xF) << 10) | (b[2] << 2) | ((b[1] & 0xC0) >> 6))
            return w, h
    return None


def inspect(data: bytes, limits: Limits | None = None) -> Inspection:
    """Validate a GLB and describe it. Raises GlbRejected with an actionable, safe message."""
    limits = limits or Limits()
    if len(data) > limits.max_bytes:
        raise GlbRejected("too_large", f"This model is larger than {limits.max_bytes // (1024 * 1024)} MB.")
    if len(data) < 20:
        raise GlbRejected("not_glb", "This file isn't a GLB (binary glTF) model.")
    magic, version, length = struct.unpack("<III", data[:12])
    if magic != GLB_MAGIC:
        raise GlbRejected("not_glb", "This file isn't a GLB (binary glTF) model.")
    if version != 2:
        raise GlbRejected("unsupported_version", "Only glTF 2.0 models are supported.")
    if length != len(data):
        raise GlbRejected("truncated", "This model file is incomplete. Export it again.")
    offset = 12
    doc = None
    bin_chunk = b""
    while offset + 8 <= len(data):
        chunk_len, chunk_type = struct.unpack("<II", data[offset : offset + 8])
        body = data[offset + 8 : offset + 8 + chunk_len]
        if len(body) != chunk_len:
            raise GlbRejected("truncated", "This model file is incomplete. Export it again.")
        if chunk_type == CHUNK_JSON and doc is None:
            try:
                doc = json.loads(body.decode("utf-8"))
            except (UnicodeDecodeError, json.JSONDecodeError):
                raise GlbRejected("bad_json", "This model's structure couldn't be read.") from None
        elif chunk_type == CHUNK_BIN and not bin_chunk:
            bin_chunk = body
        offset += 8 + chunk_len
    if not isinstance(doc, dict):
        raise GlbRejected("bad_json", "This model's structure couldn't be read.")

    # --- external resources: only GLB-embedded data (or inline data: URIs) is accepted ---
    for buf in doc.get("buffers", []):
        uri = buf.get("uri")
        if uri is not None and not str(uri).startswith("data:application/"):
            raise GlbRejected("external_resource", "This model references files outside itself. Export it as a single self-contained GLB.")
    for img in doc.get("images", []):
        uri = img.get("uri")
        if uri is not None and not re.match(r"^data:image/(png|jpeg|webp);base64,", str(uri)):
            raise GlbRejected("external_resource", "This model references image files outside itself. Export it with embedded textures.")

    required = set(doc.get("extensionsRequired", []))
    unsupported = sorted(required - SUPPORTED_EXTENSIONS)
    if unsupported:
        raise GlbRejected("unsupported_extension", f"This model needs {', '.join(unsupported)}, which the room can't decode. Export it without that compression.")

    accessors = doc.get("accessors", [])
    views = doc.get("bufferViews", [])

    # --- geometry bounds ---
    triangles = 0
    vertices = 0
    morph_names: list[str] = []
    for mesh in doc.get("meshes", []):
        names = (mesh.get("extras") or {}).get("targetNames") or []
        for prim in mesh.get("primitives", []):
            pos = prim.get("attributes", {}).get("POSITION")
            if pos is None or pos >= len(accessors):
                continue
            count = int(accessors[pos].get("count", 0))
            vertices += count
            mode = prim.get("mode", 4)
            idx = prim.get("indices")
            n = int(accessors[idx].get("count", 0)) if idx is not None and idx < len(accessors) else count
            if mode == 4:
                triangles += n // 3
            elif mode in (5, 6):
                triangles += max(0, n - 2)
            if len(prim.get("targets", [])) > limits.max_morph_targets:
                raise GlbRejected("too_many_morphs", "This model has more expression shapes than the room supports.")
        morph_names.extend(str(x) for x in names)
    if triangles > limits.max_triangles or vertices > limits.max_vertices:
        raise GlbRejected("too_detailed", f"This model has {triangles:,} triangles; the room supports up to {limits.max_triangles:,}. Export a lower level of detail.")
    nodes = doc.get("nodes", [])
    if len(nodes) > limits.max_nodes:
        raise GlbRejected("too_complex", "This model has too many parts for the room.")

    # --- textures: count and decoded size (read from the embedded image headers) ---
    images = doc.get("images", [])
    if len(images) > limits.max_images:
        raise GlbRejected("too_many_textures", "This model has too many textures for the room.")
    max_px = 0
    for img in images:
        size = None
        view_index = img.get("bufferView")
        if view_index is not None and view_index < len(views):
            view = views[view_index]
            start = int(view.get("byteOffset", 0))
            size = _image_size(bin_chunk[start : start + min(int(view.get("byteLength", 0)), 65536)])
        if size:
            max_px = max(max_px, *size)
    if max_px > limits.max_texture_px:
        raise GlbRejected("texture_too_large", f"This model has a {max_px} px texture; the room supports up to {limits.max_texture_px} px.")

    # --- accessors must stay inside their buffer views (guards against malformed or hostile files) ---
    for acc in accessors:
        view_index = acc.get("bufferView")
        if view_index is None:
            continue
        if view_index >= len(views):
            raise GlbRejected("bad_accessor", "This model's data is inconsistent. Export it again.")
        view = views[view_index]
        comp = COMPONENT_BYTES.get(acc.get("componentType"), 4)
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16}.get(acc.get("type"), 1)
        need = int(acc.get("byteOffset", 0)) + int(acc.get("count", 0)) * comp * width
        if need > int(view.get("byteLength", 0)) + 3 and not view.get("extensions"):
            raise GlbRejected("bad_accessor", "This model's data is inconsistent. Export it again.")

    # --- rig ---
    skins = doc.get("skins", [])
    if not skins:
        raise GlbRejected("no_skeleton", "This model has no skeleton, so it can't sit at the table or move its hands. Export the rigged (skinned) version.")
    joint_names: list[str] = []
    for skin in skins:
        for j in skin.get("joints", []):
            if j < len(nodes):
                joint_names.append(str(nodes[j].get("name", f"joint_{j}")))
    if len(joint_names) > limits.max_joints:
        raise GlbRejected("too_many_joints", "This model's skeleton is too complex for the room.")
    roles = map_humanoid(joint_names)
    missing = [r for r in REQUIRED_ROLES if r not in roles]
    if missing:
        raise GlbRejected("not_humanoid", "The skeleton isn't a standard humanoid rig (missing " + ", ".join(missing[:4]) + "). Export the full-body rigged model.")
    lowered = {m.lower() for m in morph_names}
    blink = any(n in lowered for n in BLINK_NAMES[0]) and any(n in lowered for n in BLINK_NAMES[1])
    arkit = {"jawopen", "mouthsmileleft", "browinnerup", "eyeblinkleft"} <= lowered
    rig = {
        "skinned": True,
        "skins": len(skins),
        "joints": len(joint_names),
        "humanoid": True,
        "bones": roles,
        "morphTargets": sorted(set(morph_names))[:200],
        "blink": blink,
        "expressions": "arkit" if arkit else ("blink" if blink else "static"),
        "triangles": triangles,
        "vertices": vertices,
        "textures": len(images),
        "maxTexturePx": max_px,
        "generator": str((doc.get("asset") or {}).get("generator", ""))[:80],
    }
    return Inspection(
        content_hash=hashlib.sha256(data).hexdigest(),
        size_bytes=len(data),
        triangles=triangles,
        vertices=vertices,
        nodes=len(nodes),
        images=len(images),
        max_texture_px=max_px,
        extensions=sorted(set(doc.get("extensionsUsed", []))),
        rig=rig,
    )
