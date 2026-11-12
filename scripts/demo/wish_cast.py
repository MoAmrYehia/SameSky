"""Puts the downloaded Rocketbox clips and background characters where the film's Unity code finds them.

Reads .asset-cache/rocketbox-film (filled by scripts/demo/wish_fetch.py) and writes
native/quest/Assets/SameSky/Editor/Film/Cast:
    Clips/<clip>.fbx                       the motion clips, as they are
    People/<Name>/<Name>.fbx               the four background characters' models, as they are
    People/<Name>/{body,head}.jpg          their colour textures, 1024 px (from the 2048 px TGA files)
    People/<Name>/hair.png                 hair and lashes with their alpha, 1024 px, where the character has one
That folder is editor-only (nothing in it can reach a build of the app) and is not kept in git.

The downloaded files are only data to this script, and it should be run so that Python loads nothing from their
folders:

    python -I scripts/demo/wish_cast.py
"""

import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = json.loads((ROOT / "scripts" / "demo" / "wish_assets.json").read_text(encoding="utf-8"))
CACHE = ROOT / ".asset-cache" / "rocketbox-film" / "Assets"
CAST = ROOT / "native" / "quest" / "Assets" / "SameSky" / "Editor" / "Film" / "Cast"
SIZE = 1024


def copy(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    if not dst.exists() or dst.stat().st_size != src.stat().st_size:
        shutil.copyfile(src, dst)


def main() -> None:
    clips = 0
    for kind, names in MANIFEST["clips"].items():
        for name in names:
            copy(CACHE / "Animations" / f"all_animations_max_motextr_{kind}" / f"{name}.max.fbx", CAST / "Clips" / f"{name}.fbx")
            clips += 1
    for p in MANIFEST["people"]:
        src = CACHE / "Avatars" / "Adults" / p["name"]
        dst = CAST / "People" / p["name"]
        copy(src / "Export" / f"{p['name']}.fbx", dst / f"{p['name']}.fbx")
        for slot in ("body", "head"):
            with Image.open(src / "Textures" / f"{p['id']}_{slot}_color.tga") as im:
                im.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS).save(dst / f"{slot}.jpg", quality=92)
        if p.get("opacity"):
            with Image.open(src / "Textures" / f"{p['id']}_opacity_color.tga") as im:
                rgba = im.convert("RGBA")
                w, h = rgba.size
                scale = SIZE / max(w, h)
                rgba.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS).save(dst / "hair.png")
        print(f"  {p['name']}: {', '.join(sorted(f.name for f in dst.iterdir() if f.suffix != '.meta'))}")
    print(f"{clips} clips and {len(MANIFEST['people'])} people in {CAST.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
