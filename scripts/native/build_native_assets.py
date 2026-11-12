"""Builds the native Quest app's bundled art and offline sample from the repository's existing assets.

    api/.venv/Scripts/python scripts/native/build_native_assets.py

* Room props: the CC0 Poly Haven sources the web room was made from (1k glTF + JPG; fetched into the git-ignored
  .asset-cache/ by scripts/assets/fetch_polyhaven.py). The web copies use WebP-in-glTF, which Unity can't import.
* Tiling materials, the interior HDRI and the window plate: same CC0 sources (docs/ASSET_MANIFEST.md).
* Sample media: the companion's fictional sample day. The synthesized voice note is converted from AAC to Ogg
  Vorbis (Unity can't decode AAC audio clips); photos and the simulated clip are copied unchanged.
* Sample data: scripts/native/export_sample.mjs (from web/src/shared/sample.ts).

Everything written here is CC0 or original to this repository; provenance is listed in docs/ASSET_MANIFEST.md.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UNITY = ROOT / "native" / "quest" / "Assets"
ART = UNITY / "SameSky" / "Art"
SAMPLE_MEDIA = UNITY / "SameSky" / "Resources" / "Sample" / "Media"
CACHE = ROOT / ".asset-cache"
WEB = ROOT / "web" / "public" / "assets"

MODELS = {
    # Unity name: Poly Haven source folder
    "table": "round_wooden_table_01",
    "side-table": "round_wooden_table_02",
    "armchair": "ArmChair_01",
    "plant": "potted_plant_04",
    "vase": "ceramic_vase_04",
}
TEXTURES = ["herringbone_parquet", "clay_plaster", "american_walnut_veneer", "wool_boucle", "knitted_fleece"]
SAMPLE_FILES = [
    "maya-park.jpg", "maya-park-thumb.jpg", "adam-waterfront.jpg", "adam-waterfront-thumb.jpg",
    "adam-desert.jpg", "adam-desert-thumb.jpg", "lina-courtyard.jpg", "lina-courtyard-thumb.jpg",
    "adam-clip.mp4", "adam-clip-poster.jpg", "adam-clip-thumb.jpg",
]


def ffmpeg() -> str:
    try:
        import imageio_ffmpeg  # the API's media pipeline dependency

        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        found = shutil.which("ffmpeg")
        if not found:
            sys.exit("ffmpeg not found: run this with the API's virtualenv (api/.venv) or put ffmpeg on PATH")
        return found


def copy(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if not dst.exists() or dst.stat().st_size != src.stat().st_size or dst.read_bytes() != src.read_bytes():
        shutil.copy2(src, dst)


def main() -> None:
    if not CACHE.exists():
        sys.exit(".asset-cache/ is missing: run scripts/assets/fetch_polyhaven.py first (CC0 sources, ~290 MB)")

    for name, folder in MODELS.items():
        src = CACHE / "models" / folder
        dst = ART / "Models" / name
        gltf = next(src.glob("*_1k.gltf"))
        copy(gltf, dst / f"{name}.gltf")
        copy(next(src.glob("*.bin")), dst / gltf.name.replace("_1k.gltf", ".bin"))
        for tex in (src / "textures").glob("*.jpg"):
            copy(tex, dst / "textures" / tex.name)
        print("model", name)

    for name in TEXTURES:
        for tex in (CACHE / "textures" / name).glob("*.jpg"):
            copy(tex, ART / "Textures" / name / tex.name)
        print("texture", name)

    copy(CACHE / "hdri" / "lythwood_lounge_1k.hdr", ART / "Env" / "lythwood_lounge_1k.hdr")
    copy(WEB / "sample" / "window-skyline.jpg", ART / "Textures" / "window-skyline.jpg")

    for name in SAMPLE_FILES:
        copy(WEB / "sample" / name, SAMPLE_MEDIA / name)
    voice = SAMPLE_MEDIA / "maya-voice.ogg"
    if not voice.exists():
        subprocess.run(
            [ffmpeg(), "-y", "-loglevel", "error", "-i", str(WEB / "sample" / "maya-voice.m4a"),
             "-ac", "1", "-ar", "44100", "-c:a", "libvorbis", "-q:a", "5", str(voice)],
            check=True,
        )
    print("sample media", len(SAMPLE_FILES) + 1)

    node = shutil.which("node") or r"C:\Program Files\nodejs\node.exe"
    subprocess.run([node, str(ROOT / "scripts" / "native" / "export_sample.mjs")], check=True)


if __name__ == "__main__":
    main()
