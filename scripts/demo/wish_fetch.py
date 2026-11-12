"""Downloads the third-party assets of the film "Wish You Were Here" that are not kept in git.

What: the motion clips and background characters listed in scripts/demo/wish_assets.json, from the Microsoft
Rocketbox avatar library (MIT licence). Where to: .asset-cache/rocketbox-film/<path in the library>. It only saves
the files and notes each one's size and SHA-256 in docs/cinematic/asset-provenance.json; nothing in them is opened
here. scripts/demo/wish_cast.py turns them into what the Unity project uses.

    python scripts/demo/wish_fetch.py            (skips files already there with the right size)
"""

from __future__ import annotations

import hashlib
import json
import sys
import time
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = json.loads((ROOT / "scripts" / "demo" / "wish_assets.json").read_text(encoding="utf-8"))
CACHE = ROOT / ".asset-cache" / "rocketbox-film"
RECORD = ROOT / "docs" / "cinematic" / "asset-provenance.json"


def wanted() -> list[str]:
    paths = []
    for kind, names in MANIFEST["clips"].items():
        paths += [f"Assets/Animations/all_animations_max_motextr_{kind}/{n}.max.fbx" for n in names]
    for p in MANIFEST["people"]:
        folder = f"Assets/Avatars/Adults/{p['name']}"
        paths.append(f"{folder}/Export/{p['name']}.fbx")
        paths += [f"{folder}/Textures/{p['id']}_body_color.tga", f"{folder}/Textures/{p['id']}_head_color.tga"]
        if p.get("opacity"):
            paths.append(f"{folder}/Textures/{p['id']}_opacity_color.tga")
    return paths


def fetch(path: str) -> dict:
    url = MANIFEST["base"] + urllib.parse.quote(path)
    out = CACHE / path
    out.parent.mkdir(parents=True, exist_ok=True)
    for attempt in range(4):
        try:
            if not out.exists():
                request = urllib.request.Request(url, headers={"User-Agent": "asset-fetch/1.0"})
                part = out.with_suffix(out.suffix + ".part")
                with urllib.request.urlopen(request, timeout=120) as response, open(part, "wb") as f:
                    while True:
                        block = response.read(1 << 16)
                        if not block:
                            break
                        f.write(block)
                part.replace(out)
            digest = hashlib.sha256(out.read_bytes()).hexdigest()
            return {"file": path, "url": url, "bytes": out.stat().st_size, "sha256": digest}
        except Exception as e:   # noqa: BLE001
            print(f"  retry {attempt + 1} for {path}: {type(e).__name__}", flush=True)
            time.sleep(3 + attempt * 4)
    raise SystemExit(f"could not download {path}")


def main() -> None:
    paths = wanted()
    print(f"{len(paths)} files from {MANIFEST['source']}", flush=True)
    started = time.time()
    done = []
    with ThreadPoolExecutor(max_workers=4) as pool:
        for i, row in enumerate(pool.map(fetch, paths), 1):
            done.append(row)
            print(f"  {i:2d}/{len(paths)}  {row['bytes'] / 1e6:6.1f} MB  {row['file'].rsplit('/', 1)[-1]}", flush=True)
    total = sum(r["bytes"] for r in done)
    RECORD.parent.mkdir(parents=True, exist_ok=True)
    RECORD.write_text(json.dumps({"source": MANIFEST["source"], "licence": MANIFEST["licence"], "files": done}, indent=2) + "\n", encoding="utf-8")
    print(f"{total / 1e6:.1f} MB in {time.time() - started:.0f} s; recorded in {RECORD.relative_to(ROOT)}")


if __name__ == "__main__":
    sys.exit(main())
